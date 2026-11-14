import { env, exports } from "cloudflare:workers";
import { describe, it, expect, vi, afterEach } from "vitest";
import { runInDurableObject, runDurableObjectAlarm, evictDurableObject, reset } from "cloudflare:test";
import { importJWK, jwtVerify } from "jose";
import { digest, poolKey, type ContentBinding, type PartySettings, type PartyState, type Attempt } from "../src/model";
import { rpcValue } from "../src/rpc";
import { publicContent } from "./fixtures/public-content";

afterEach(async () => { vi.restoreAllMocks(); await reset(); });

const content: ContentBinding = publicContent("TeamRace", "Squad");
const settings = (fill = false, format: PartySettings["format"] = "Squad"): PartySettings =>
  ({ mode: "TeamRace", format, fill, region: "asia", protocolVersion: crypto.randomUUID().slice(0, 8), mapPoolVersion: "1" });
type Guest = { playerId: string; accessToken: string; refreshToken: string };
type Assignment = { matchId: string; state: string; privateCode?: string; rosterHash: string; reservationGeneration: string; roster: Array<{ playerId: string; partyId: string; teamId: number; seat: number }> };
type PartyView = { id: string; revision: string; invite: string; members: Array<{ playerId: string; ready: boolean }>; activeAttempt?: { id: string }; assignment: Assignment };
async function call<T>(path: string, value?: unknown, guest?: Guest, method = value === undefined ? "GET" : "POST", key = crypto.randomUUID()) {
  const response = await exports.default.fetch(`https://local.test/v1${path}`, {
    method, headers: { "content-type": "application/json", "idempotency-key": key, ...(guest ? { authorization: `Bearer ${guest.accessToken}` } : {}) },
    body: value === undefined ? undefined : JSON.stringify(value),
  });
  return { status: response.status, data: await response.json() as T };
}
async function guest() {
  const r = await call<Guest>("/guests", { displayName: "Tester" }); expect(r.status).toBe(200); return r.data;
}
async function party(s: PartySettings, count = 1) {
  const members = await Promise.all(Array.from({ length: count }, guest));
  let r = await call<PartyView>("/parties", { settings: s }, members[0]); expect(r.status).toBe(200);
  for (const member of members.slice(1)) {
    r = await call<PartyView>("/party-joins", { partyId: r.data.id, invite: r.data.invite }, member); expect(r.status).toBe(200);
  }
  for (const member of members) {
    r = await call<PartyView>(`/parties/${r.data.id}/ready`, { expectedRevision: r.data.revision, ready: true }, member);
    expect(r.status).toBe(200);
  }
  return { members, p: r.data, s };
}
type Group = Awaited<ReturnType<typeof party>>;
async function queue(g: Group, channel = "QuickPlay", extra: object = { content: publicContent(g.s.mode, g.s.format) }) {
  return call<PartyView>(`/parties/${g.p.id}/queue`, { expectedRevision: g.p.revision, channel, ...extra }, g.members[0]);
}
async function status(g: Group) { return (await call<PartyView>(`/parties/${g.p.id}`, undefined, g.members[0])).data.assignment; }
function matchPath(g: Group, a: Assignment, action: string) { return `/pools/${encodeURIComponent(poolKey(g.s))}/matches/${a.matchId}/${action}`; }
async function admit(g: Group, a: Assignment) {
  for (const member of g.members) {
    const r = await call<Assignment>(matchPath(g, a, "ready"), { reservationGeneration: a.reservationGeneration, manifestHash: content.manifestHash, rosterHash: a.rosterHash }, member);
    expect(r.status).toBe(200);
  }
}

describe("authenticated guests and HTTP validation", () => {
  it("mints identities and rejects caller identity fields", async () => {
    expect((await call("/guests", { displayName: "A", playerId: crypto.randomUUID() })).status).toBe(400);
    const a = await guest(); const b = await guest(); expect(a.playerId).not.toBe(b.playerId);
    expect((await call("/me")).status).toBe(401);
    expect((await call("/me", undefined, { ...a, accessToken: `${a.accessToken}x` })).status).toBe(401);
  });
  it("atomically rotates refresh credentials under concurrent requests", async () => {
    const a = await guest();
    const rs = await Promise.all([call<Guest>("/auth/refresh", { refreshToken: a.refreshToken }), call<Guest>("/auth/refresh", { refreshToken: a.refreshToken })]);
    expect(rs.map(r => r.status).sort()).toEqual([200, 401]);
    expect((await call("/me", undefined, a)).status).toBe(401);
    expect((await call("/me", undefined, rs.find(r => r.status === 200)!.data)).status).toBe(200);
  });
  it("rejects non-members, unknown fields and oversized request bodies", async () => {
    const g = await party(settings()); const outsider = await guest();
    expect((await call(`/parties/${g.p.id}`, undefined, outsider)).status).toBe(403);
    expect((await call("/guests", { displayName: "x".repeat(20_000) })).status).toBe(413);
    expect((await call(`/parties/${g.p.id}/ready`, { expectedRevision: g.p.revision, ready: true, playerId: outsider.playerId }, g.members[0])).status).toBe(400);
  });
});

describe("transactional parties and cancellation", () => {
  it("retires successful join operations after leaving without phantom or later membership", async () => {
    const g = await party(settings()); const joiner = await guest(); const key = crypto.randomUUID();
    const input = { partyId: g.p.id, invite: g.p.invite };
    const joined = await call<PartyView>("/party-joins", input, joiner, "POST", key);
    expect(joined.status).toBe(200);
    expect((await call(`/parties/${g.p.id}/leave`, { expectedRevision: joined.data.revision }, joiner)).status).toBe(200);
    const replay = await call<{ error: { code: string } }>("/party-joins", input, joiner, "POST", key);
    expect(replay.status).toBe(409);
    expect(replay.data.error.code).toBe("MEMBERSHIP_OPERATION_RETIRED");
    expect((await call<{ partyId?: string }>("/me", undefined, joiner)).data.partyId).toBeUndefined();
    const next = await call<PartyView>("/parties", { settings: g.s }, joiner);
    expect(next.status).toBe(200);
    expect((await call("/party-joins", input, joiner, "POST", key)).status).toBe(409);
    expect((await call<{ partyId: string }>("/me", undefined, joiner)).data.partyId).toBe(next.data.id);
  });
  it("retires creation keys after leaving and still permits a fresh operation", async () => {
    const a = await guest(); const s = settings(); const key = crypto.randomUUID();
    const created = await call<PartyView>("/parties", { settings: s }, a, "POST", key);
    expect((await call(`/parties/${created.data.id}/leave`, { expectedRevision: created.data.revision }, a)).status).toBe(200);
    expect((await call("/parties", { settings: s }, a, "POST", key)).status).toBe(409);
    expect((await call("/parties", { settings: s }, a)).status).toBe(200);
  });
  it("replays leaving after a lost response without removing a later party membership", async () => {
    const g = await party(settings(), 2); const key = crypto.randomUUID();
    const path = `/parties/${g.p.id}/leave`; const input = { expectedRevision: g.p.revision };
    const results = await Promise.all([0, 1].map(() => call<PartyView>(path, input, g.members[0], "POST", key)));
    expect(results.map(r => r.status)).toEqual([200, 200]);
    expect(results[0].data).toEqual(results[1].data);
    const remaining = await call<{ leaderId: string }>(`/parties/${g.p.id}`, undefined, g.members[1]);
    expect(remaining.data.leaderId).toBe(g.members[1].playerId);
    const next = await call<PartyView>("/parties", { settings: g.s }, g.members[0]);
    expect(next.status).toBe(200);
    expect((await call(path, input, g.members[0], "POST", key)).status).toBe(200);
    expect((await call<{ partyId: string }>("/me", undefined, g.members[0])).data.partyId).toBe(next.data.id);
    expect((await call(path, { expectedRevision: "0" }, g.members[0], "POST", key)).status).toBe(409);
  });
  it("binds party creation retries to the original settings", async () => {
    const a = await guest(); const s = settings(); const key = crypto.randomUUID();
    const result = await call<PartyView>("/parties", { settings: s }, a, "POST", key);
    expect(result.status).toBe(200);
    expect((await call("/parties", { settings: { ...s, fill: true } }, a, "POST", key)).status).toBe(409);
    expect((await call<PartyView>("/parties", { settings: s }, a, "POST", key)).data.id).toBe(result.data.id);
  });
  it("claims membership only once across concurrent invitations", async () => {
    const s = settings(); const a = await party(s); const b = await party(s); const joiner = await guest();
    const rs = await Promise.all([a, b].map(g => call("/party-joins", { partyId: g.p.id, invite: g.p.invite }, joiner)));
    expect(rs.map(r => r.status).sort()).toEqual([200, 409]);
  });
  it("serializes same-party settings changes and validates revisions", async () => {
    const g = await party(settings());
    const rs = await Promise.all([false, true].map(fill => call(`/parties/${g.p.id}`, { expectedRevision: g.p.revision, settings: { ...g.s, fill } }, g.members[0], "PATCH")));
    expect(rs.filter(r => r.status === 200)).toHaveLength(1);
    expect(rs.filter(r => r.status === 409)).toHaveLength(1);
  });
  it("replays queue idempotently and rejects reused keys with different settings", async () => {
    const g = await party(settings()); const key = crypto.randomUUID();
    const data = { expectedRevision: g.p.revision, channel: "QuickPlay", content };
    const a = await call<PartyView>(`/parties/${g.p.id}/queue`, data, g.members[0], "POST", key);
    const b = await call<PartyView>(`/parties/${g.p.id}/queue`, data, g.members[0], "POST", key);
    expect(a.status).toBe(200); expect(b.data.activeAttempt?.id).toBe(a.data.activeAttempt?.id);
    expect((await call(`/parties/${g.p.id}/queue`, { ...data, channel: "PrivateCreate" }, g.members[0], "POST", key)).status).toBe(409);
  });
  it("tombstones cancelled attempts and rejects delayed resurrection", async () => {
    const g = await party(settings()); const queued = await queue(g); expect(queued.status).toBe(200);
    const view = (await call<{ activeAttempt: import("../src/model").Attempt }>(`/parties/${g.p.id}`, undefined, g.members[0])).data;
    const attempt = view.activeAttempt;
    expect((await call(`/parties/${g.p.id}/cancel`, { expectedRevision: g.p.revision }, g.members[0])).status).toBe(200);
    let rejection = "";
    try { await rpcValue(env.POOLS.getByName(poolKey(g.s)).enqueue(attempt)); }
    catch (e) { rejection = (e as Error).message; }
    expect(rejection).toContain("ATTEMPT_CANCELLED");
  });
});

describe("whole-party allocation, Fill and admission", () => {
  it("keeps Fill Off undersized teams empty and never splits a party", async () => {
    const s = settings(); const a = await party(s, 2); const b = await party(s, 1);
    await Promise.all([queue(a), queue(b)]);
    const m = await status(a); expect(m.state).toBe("ADMITTING"); expect(m.roster).toHaveLength(3);
    const team = m.roster.filter(x => x.partyId === a.p.id).map(x => x.teamId);
    expect(new Set(team).size).toBe(1);
    const stranger = await party({ ...s, fill: true }); await queue(stranger);
    expect((await status(stranger)).matchId).not.toBe(m.matchId);
  });
  it("handles concurrent last slots without exceeding eight or splitting parties", async () => {
    const s = settings(true); const groups = await Promise.all([3, 3, 1, 1, 1].map(n => party(s, n)));
    const rs = await Promise.all(groups.map(g => queue(g))); expect(rs.every(r => r.status === 200)).toBe(true);
    const matches = await Promise.all(groups.map(status));
    const full = matches.find(m => m.state === "ADMITTING")!;
    expect(full.roster).toHaveLength(8);
    for (const team of [0, 1]) expect(full.roster.filter(s => s.teamId === team)).toHaveLength(4);
    expect(new Set(full.roster.map(s => s.playerId)).size).toBe(8);
  });
  it("packs whole Fill On parties after solo-first arrival would fragment both teams", async () => {
    const s = settings(true);
    const groups = [];
    for (const count of [1, 1, 1, 3, 3]) {
      const g = await party(s, count); groups.push(g); expect((await queue(g)).status).toBe(200);
    }
    const assignments = await Promise.all(groups.map(status));
    const full = assignments.find(a => a.state === "ADMITTING");
    expect(full?.roster).toHaveLength(8);
    for (const g of groups) {
      const seats = full!.roster.filter(seat => seat.partyId === g.p.id);
      expect(seats.length === 0 || seats.length === g.members.length).toBe(true);
      expect(new Set(seats.map(seat => seat.teamId)).size).toBeLessThanOrEqual(1);
    }
  });
  it("binds signed admission to the entire roster, exact content and seat", async () => {
    const s = settings(false, "Solo"); const a = await party(s); const b = await party(s);
    await queue(a); await queue(b); const m = await status(a);
    const r = await call<{ grant: string; connectionSecret: string }>(matchPath(a, m, "admission"), {}, a.members[0]);
    expect(r.status).toBe(200);
    const keys = await call<{ keys: Array<import("jose").JWK> }>("/keys");
    expect(keys.data.keys[0].d).toBeUndefined();
    const key = await importJWK(keys.data.keys[0], "RS256");
    const verified = await jwtVerify(r.data.grant, key, { issuer: "wordbound-local", audience: "wordbound-admission", algorithms: ["RS256"] });
    expect(verified.payload).toMatchObject({ playerId: a.members[0].playerId, rosterHash: m.rosterHash, manifestHash: content.manifestHash, matchId: m.matchId });
    await expect(jwtVerify(`${r.data.grant}x`, key)).rejects.toThrow();
    await expect(jwtVerify(r.data.grant, key, { audience: "different" })).rejects.toThrow();
    await expect(jwtVerify(r.data.grant, key, { currentDate: new Date(Date.now() + 91_000) })).rejects.toThrow();
    expect((await call(matchPath(a, m, "ready"), { reservationGeneration: "1", manifestHash: "e".repeat(64), rosterHash: m.rosterHash }, a.members[0])).status).toBe(409);
    await admit(a, m); expect((await status(a)).state).toBe("ADMITTING");
    await admit(b, m); expect((await status(a)).state).toBe("STARTED");
    expect((await call(`/parties/${a.p.id}/cancel`, { expectedRevision: a.p.revision }, a.members[0])).status).toBe(409);
  });
  it("rolls back incomplete party admission at the deadline", async () => {
    const s = settings(false); const a = await party(s, 2); const b = await party(s);
    await queue(a); await queue(b); const m = await status(a); await admit(b, m);
    const time = Date.now(); vi.spyOn(Date, "now").mockReturnValue(time + 90_001);
    try {
      expect((await status(a)).state).toBe("CANCELLED");
      expect((await status(b)).state).toBe("FORMING");
    } finally { vi.restoreAllMocks(); }
  });
});

describe("durable transitions, alarms and return to lobby", () => {
  it("issues start certificates only after all admissions commit and separates JWT audiences", async () => {
    const s = settings(false, "Solo"); const a = await party(s); const b = await party(s);
    await queue(a); await queue(b); const m = await status(a);
    expect((await call(matchPath(a, m, "certificate"), {}, a.members[0])).status).toBe(409);
    await admit(a, m); await admit(b, m);
    const result = await call<{ certificate: string }>(matchPath(a, m, "certificate"), {}, a.members[0]);
    expect(result.status).toBe(200);
    const keys = await call<{ keys: Array<import("jose").JWK> }>("/keys");
    const key = await importJWK(keys.data.keys[0], "RS256");
    const verified = await jwtVerify(result.data.certificate, key, {
      issuer: "wordbound-local", audience: "wordbound-match-start", algorithms: ["RS256"],
    });
    expect(verified.payload).toMatchObject({ matchId: m.matchId, rosterHash: m.rosterHash, roster: m.roster, settings: s,
      content: publicContent(s.mode, s.format) });
    await expect(jwtVerify(result.data.certificate, key, { audience: "wordbound-admission" })).rejects.toThrow();
    expect((await call(matchPath(a, m, "certificate"), {}, await guest())).status).toBe(403);
  });
  it("cannot obtain a start certificate for cancelled staging even with a previously issued grant", async () => {
    const s = settings(false, "Solo"); const a = await party(s); const b = await party(s);
    await queue(a); await queue(b); const m = await status(a);
    expect((await call(matchPath(a, m, "admission"), {}, a.members[0])).status).toBe(200);
    expect((await call(`/parties/${a.p.id}/cancel`, { expectedRevision: a.p.revision }, a.members[0])).status).toBe(200);
    expect((await call(matchPath(a, m, "certificate"), {}, a.members[0])).status).toBe(404);
  });
  it("preserves reservations in SQLite after object eviction", async () => {
    const s = settings(false, "Duo"); const a = await party(s, 2); const b = await party(s, 2);
    await queue(a); await queue(b);
    const before = await status(a); const stub = env.POOLS.getByName(poolKey(s));
    const stored = await runInDurableObject(stub, (_instance, state) => ({
      rows: state.storage.sql.exec<{ n: number }>("SELECT COUNT(*) AS n FROM state").one().n,
      bytes: state.storage.sql.databaseSize,
    }));
    expect(stored.rows).toBe(1); expect(stored.bytes).toBeGreaterThan(0);
    await evictDurableObject(stub);
    expect(await status(a)).toEqual(before);
    expect(before.roster.every(seat => seat.seat >= 0 && seat.seat < 2)).toBe(true);
  });
  it("runs admission expiry through a real Durable Object alarm", async () => {
    const s = settings(false, "Solo"); const a = await party(s); const b = await party(s);
    await queue(a); await queue(b); const m = await status(a); await admit(b, m);
    const stub = env.POOLS.getByName(poolKey(s));
    const alarm = await runInDurableObject(stub, (_instance, state) => state.storage.getAlarm());
    expect(alarm).not.toBeNull();
    vi.spyOn(Date, "now").mockReturnValue(alarm! + 1);
    expect(await runDurableObjectAlarm(stub)).toBe(true);
    expect((await status(a)).state).toBe("CANCELLED");
    expect((await status(b)).state).toBe("FORMING");
  });
  it("resumes an interrupted cross-object queue operation using its original attempt", async () => {
    const g = await party(settings()); const opKey = crypto.randomUUID();
    const input = { expectedRevision: g.p.revision, channel: "QuickPlay" as const, content };
    const attempt: Attempt = { id: crypto.randomUUID(), partyId: g.p.id, partyRevision: g.p.revision, members: g.members.map(m => m.playerId),
      settings: g.s, poolKey: poolKey(g.s), channel: "QuickPlay", content, createdAt: Date.now() };
    await rpcValue(env.POOLS.getByName(poolKey(g.s)).enqueue(attempt));
    const stub = env.PARTIES.getByName(g.p.id);
    await runInDurableObject(stub, (_instance, state) => {
      const data = JSON.parse(state.storage.sql.exec<{ value: string }>("SELECT value FROM state WHERE id=1").one().value) as PartyState;
      data.operation = { key: opKey, hash: digest({ actor: g.members[0].playerId, body: input, kind: "queue" }), kind: "queue", attempt };
      state.storage.sql.exec("UPDATE state SET value=? WHERE id=1", JSON.stringify(data));
    });
    await evictDurableObject(stub);
    const r = await call<PartyView>(`/parties/${g.p.id}/queue`, input, g.members[0], "POST", opKey);
    expect(r.status).toBe(200); expect(r.data.activeAttempt?.id).toBe(attempt.id);
    expect((await status(g)).roster).toHaveLength(1);
  });
  it("linearizes final readiness against cancellation", async () => {
    const s = settings(false, "Solo"); const a = await party(s); const b = await party(s);
    await queue(a); await queue(b); const m = await status(a); await admit(a, m);
    const [ready, cancel] = await Promise.all([
      call(matchPath(b, m, "ready"), { reservationGeneration: m.reservationGeneration, manifestHash: content.manifestHash, rosterHash: m.rosterHash }, b.members[0]),
      call(`/parties/${a.p.id}/cancel`, { expectedRevision: a.p.revision }, a.members[0]),
    ]);
    expect([ready.status, cancel.status].filter(status => status === 200)).toHaveLength(1);
    if (ready.status === 200) expect((await status(a)).state).toBe("STARTED");
    else expect((await call<PartyView>(`/parties/${a.p.id}`, undefined, a.members[0])).data.activeAttempt).toBeUndefined();
  });
  it("requires explicit return before a party can queue again and never reopens old seats", async () => {
    const s = settings(false, "Solo"); const a = await party(s); const b = await party(s);
    await queue(a); await queue(b); const m = await status(a); await admit(a, m); await admit(b, m);
    expect((await call(matchPath(a, m, "return"), {}, a.members[0])).status).toBe(200);
    expect((await call(matchPath(a, m, "reconnect"), {}, a.members[0])).status).toBe(409);
    expect((await call(`/parties/${a.p.id}/cancel`, { expectedRevision: a.p.revision }, a.members[0])).status).toBe(200);
    const newParty = (await call<PartyView>(`/parties/${a.p.id}`, undefined, a.members[0])).data;
    const next = { ...a, p: newParty };
    await call(`/parties/${next.p.id}/ready`, { expectedRevision: next.p.revision, ready: true }, a.members[0]);
    expect((await queue(next)).status).toBe(200);
    expect((await status(next)).matchId).not.toBe(m.matchId);
  });
  it("replays concurrent identical queue operations as one attempt", async () => {
    const g = await party(settings()); const key = crypto.randomUUID();
    const input = { expectedRevision: g.p.revision, channel: "QuickPlay", content };
    const rs = await Promise.all([0, 1].map(() => call<PartyView>(`/parties/${g.p.id}/queue`, input, g.members[0], "POST", key)));
    expect(rs.map(r => r.status)).toEqual([200, 200]);
    expect(rs[0].data.activeAttempt?.id).toBe(rs[1].data.activeAttempt?.id);
  });
  it("keeps one attempt for concurrent private joins across asynchronous code lookup", async () => {
    const s = settings(false); const owner = await party(s); await queue(owner, "PrivateCreate");
    const match = await status(owner); const joiner = await party(s); const key = crypto.randomUUID();
    const input = { expectedRevision: joiner.p.revision, channel: "PrivateJoin", code: match.privateCode };
    const rs = await Promise.all([0, 1].map(() => call<PartyView>(`/parties/${joiner.p.id}/queue`, input, joiner.members[0], "POST", key)));
    expect(rs.map(r => r.status)).toEqual([200, 200]);
    expect(rs[0].data.activeAttempt?.id).toBe(rs[1].data.activeAttempt?.id);
    expect((await status(joiner)).roster).toHaveLength(2);
  });
});

describe("non-race modes", () => {
  it.each(["mode", "format"] as const)("rejects approved content submitted with another %s", async field => {
    const s = settings(false, field === "format" ? "Duo" : "Squad");
    if (field === "mode") s.mode = "BrawlRace";
    const g = await party(s);
    const result = await queue(g, "QuickPlay", { content });
    expect(result.status).toBe(422);
    expect(result.data).toEqual({ error: { code: "CONTENT_SETTINGS_MISMATCH" } });
    const view = (await call<PartyView>(`/parties/${g.p.id}`, undefined, g.members[0])).data;
    expect(view.activeAttempt).toBeUndefined();
    expect(view.assignment).toBeUndefined();
  });

  it("starts TimeRun with the whole Fill Off party or fills to four with consent", async () => {
    const s: PartySettings = { ...settings(false, "Solo"), mode: "TimeRun" };
    const solo = await party(s);
    const denied = await queue(solo);
    expect(denied.status).toBe(409);
    expect(denied.data).toEqual({ error: { code: "PUBLIC_COOP_REQUIRES_HUMANS" } });
    const pair = await party(s, 2); await queue(pair);
    expect((await status(pair)).state).toBe("ADMITTING");
    expect((await status(pair)).roster).toHaveLength(2);
    const fill = { ...s, fill: true };
    const a = await party(fill, 3); await queue(a);
    expect((await status(a)).state).toBe("FORMING");
    const b = await party(fill); await queue(b);
    const m = await status(a);
    expect(m.state).toBe("ADMITTING"); expect(m.roster).toHaveLength(4);
    expect(m.roster.map(seat => seat.teamId)).toEqual([0, 0, 0, 0]);
  });
  it("starts public FFA after the actual 30-second alarm with two individual players", async () => {
    const s: PartySettings = { ...settings(false, "Solo"), mode: "FreeForAll" };
    const a = await party(s); const b = await party(s);
    await queue(a); await queue(b);
    const m = await status(a); expect(m.state).toBe("FORMING");
    const stub = env.POOLS.getByName(poolKey(s));
    const alarm = await runInDurableObject(stub, (_instance, state) => state.storage.getAlarm());
    expect(alarm).not.toBeNull(); vi.spyOn(Date, "now").mockReturnValue(alarm!);
    expect(await runDurableObjectAlarm(stub)).toBe(true);
    const sealed = await status(a);
    expect(sealed.state).toBe("ADMITTING"); expect(sealed.roster).toHaveLength(2);
    expect(sealed.roster.map(seat => seat.seat)).toEqual([0, 0]);
    expect(new Set(sealed.roster.map(seat => seat.teamId)).size).toBe(2);
    const late = await party(s); await queue(late);
    expect((await status(late)).matchId).not.toBe(m.matchId);
  });
  it("keeps private Freestyle manual and allows eight invited players", async () => {
    const s: PartySettings = { ...settings(false, "Solo"), mode: "Freestyle" };
    const owner = await party(s, 4); const other = await party(s, 4);
    expect((await queue(owner, "PrivateCreate")).status).toBe(200);
    const first = await status(owner);
    expect((await queue(other, "PrivateJoin", { code: first.privateCode })).status).toBe(200);
    const m = await status(owner);
    expect(m.state).toBe("FORMING"); expect(m.roster).toHaveLength(8);
    expect((await call(matchPath(owner, m, "launch"), {}, other.members[0])).status).toBe(403);
    expect((await call(matchPath(owner, m, "launch"), {}, owner.members[0])).status).toBe(200);
    const late = await party(s);
    expect((await queue(late, "PrivateJoin", { code: first.privateCode })).status).toBe(404);
    const locked = await status(owner); await admit(owner, locked); await admit(other, locked);
    expect((await status(owner)).state).toBe("STARTED");
  });
  it("public Freestyle never starts one human and coalesces two at the elapsed deadline", async () => {
    const s: PartySettings = { ...settings(true, "Solo"), mode: "Freestyle" };
    const a = await party(s); await queue(a);
    const first = await status(a); expect(first.state).toBe("FORMING");
    const now = Date.now(); vi.spyOn(Date, "now").mockReturnValue(now + 31000);
    expect((await status(a)).state).toBe("FORMING");
    const b = await party(s); await queue(b);
    const m = await status(a);
    expect(m.state).toBe("ADMITTING"); expect(m.roster).toHaveLength(2);
    expect(m.roster.map(x => x.teamId)).toEqual([0, 0]);
    expect(m.roster.map(x => x.playerId).sort()).toEqual([a.members[0].playerId, b.members[0].playerId].sort());
  });
  it("public Freestyle Fill Off uses only the existing whole party and rejects lone humans", async () => {
    const s: PartySettings = { ...settings(false, "Solo"), mode: "Freestyle" };
    const solo = await party(s); expect((await queue(solo)).status).toBe(409);
    const a = await party(s, 2); await queue(a); const m = await status(a);
    expect(m.state).toBe("ADMITTING");
    expect(m.roster.map(x => x.playerId).sort()).toEqual(a.members.map(x => x.playerId).sort());
    const b = await party({ ...s, fill: true }, 2); await queue(b);
    expect((await status(b)).matchId).not.toBe(m.matchId);
    expect((await queue(solo, "PrivateCreate")).status).toBe(200);
    const privateMatch = await status(solo);
    expect((await call(matchPath(solo, privateMatch, "launch"), {}, solo.members[0])).status).toBe(200);
  });
  it("atomically fills Freestyle to eight without splitting a party or using private built-in rooms", async () => {
    const s: PartySettings = { ...settings(true, "Solo"), mode: "Freestyle" };
    const privateGroup = await party(s, 2); await queue(privateGroup, "PrivateCreate");
    const privateMatch = await status(privateGroup);
    const groups = await Promise.all([4, 4].map(n => party(s, n)));
    await Promise.all(groups.map(g => queue(g)));
    const views = await Promise.all(groups.map(status));
    expect(views.every(v => v.matchId !== privateMatch.matchId)).toBe(true);
    const full = views.find(v => v.state === "ADMITTING")!; expect(full.roster).toHaveLength(8);
    for (const g of groups) {
      const ids = full.roster.filter(x => x.partyId === g.p.id).map(x => x.playerId).sort();
      expect(ids.length === 0 || ids.join() === g.members.map(x => x.playerId).sort().join()).toBe(true);
    }
    expect((await status(privateGroup)).roster).toHaveLength(2);
  });
});

describe("private sessions and reconnect leases", () => {
  it("linearizes one-use connection tickets, exact retry and terminal return revocation", async () => {
    const s = settings(false, "Solo"); const a = await party(s); const b = await party(s);
    await queue(a); await queue(b); const m = await status(a); await admit(a, m); await admit(b, m);
    const ticket = await call<{ ticket: string }>(matchPath(a, m, "connection-tickets"), {}, b.members[0]);
    const admission = await call<{ connectionSecret: string }>(matchPath(a, m, "admission"), {}, b.members[0]);
    const request = { playerId: b.members[0].playerId, ticket: ticket.data.ticket,
      connectionSecret: admission.data.connectionSecret, connectionId: crypto.randomUUID() };
    expect((await call(matchPath(a, m, "connections"), { ...request, playerId: a.members[0].playerId }, a.members[0])).status).toBe(403);
    const connected = await call<{ claims: { connectionEpoch: string; leaseExpiresAt: number } }>(
      matchPath(a, m, "connections"), request, a.members[0]);
    expect(connected.status).toBe(200); expect(connected.data.claims.connectionEpoch).toBe("1");
    const retry = await call<{ claims: unknown }>(matchPath(a, m, "connections"), request, a.members[0]);
    expect(retry.data.claims).toEqual(connected.data.claims);
    expect((await call(matchPath(a, m, "connections"), { ...request, connectionId: crypto.randomUUID() }, a.members[0])).status).toBe(403);
    expect((await call(matchPath(a, m, "presence"), { connected: true }, b.members[0])).status).toBe(409);
    expect((await call(matchPath(a, m, "return"), {}, b.members[0])).status).toBe(200);
    expect((await call(matchPath(a, m, "connections"), request, a.members[0])).status).toBe(409);
    const state = await call<{ seats: Array<{ playerId: string; returned: boolean; connectionEpoch: string; leaseExpiresAt: number }> }>(
      matchPath(a, m, "connection-state"), { renew: [{ playerId: request.playerId, connectionId: request.connectionId, connectionEpoch: "1" }] }, a.members[0]);
    const seat = state.data.seats.find(p => p.playerId === request.playerId)!;
    expect(seat.returned).toBe(true); expect(seat.connectionEpoch).toBe("2"); expect(seat.leaseExpiresAt).toBe(0);
  });
  it("expires connection tickets and leases exclusively and rejects an outsider consuming them", async () => {
    const s = settings(false, "Solo"); const a = await party(s); const b = await party(s);
    await queue(a); await queue(b); const m = await status(a); await admit(a, m); await admit(b, m);
    const now = Date.now(); vi.spyOn(Date, "now").mockReturnValue(now);
    const ticket = await call<{ ticket: string }>(matchPath(a, m, "connection-tickets"), {}, b.members[0]);
    const grant = await call<{ connectionSecret: string }>(matchPath(a, m, "admission"), {}, b.members[0]);
    const request = { playerId: b.members[0].playerId, ticket: ticket.data.ticket,
      connectionSecret: grant.data.connectionSecret, connectionId: crypto.randomUUID() };
    const outsider = await guest();
    expect((await call(matchPath(a, m, "connections"), request, outsider)).status).toBe(403);
    vi.mocked(Date.now).mockReturnValue(now + 15000);
    expect((await call(matchPath(a, m, "connections"), request, a.members[0])).status).toBe(403);
    const fresh = await call<{ ticket: string }>(matchPath(a, m, "connection-tickets"), {}, b.members[0]);
    request.ticket = fresh.data.ticket;
    expect((await call(matchPath(a, m, "connections"), request, a.members[0])).status).toBe(200);
    vi.mocked(Date.now).mockReturnValue(now + 30000);
    const state = await call<{ seats: Array<{ playerId: string; leaseExpiresAt: number }> }>(
      matchPath(a, m, "connection-state"), { renew: [{ playerId: request.playerId, connectionId: request.connectionId, connectionEpoch: "1" }] }, a.members[0]);
    expect(state.data.seats.find(p => p.playerId === request.playerId)!.leaseExpiresAt).toBe(now + 30000);
    expect((await call(matchPath(a, m, "connections"), request, a.members[0])).status).toBe(409);
  });
  it("requires Custom Fill opt-in and never sends Quick Play users to workshop maps", async () => {
    const s = settings(true);
    const owner = await party(s); await queue(owner, "PrivateCreate", { content: { ...content, kind: "Workshop", courseId: "custom" } });
    const before = await status(owner); const ordinary = await party(s); await queue(ordinary);
    expect((await status(ordinary)).matchId).not.toBe(before.matchId);
    const custom = await party(s); expect((await queue(custom, "CustomFill", {})).status).toBe(200);
    expect((await status(custom)).matchId).toBe(before.matchId);
    const opposing = await party({ ...s, fill: false });
    expect((await queue(opposing, "PrivateJoin", { code: before.privateCode })).status).toBe(200);
    const outsiders = await party(s, 2); await queue(outsiders, "CustomFill", {});
    expect((await status(owner)).state).toBe("ADMITTING");
    expect((await status(owner)).roster).toHaveLength(5);
  });
  it("does not allocate random opponents into a private empty team", async () => {
    const s = settings(true, "Solo"); const owner = await party(s); await queue(owner, "PrivateCreate");
    const q = await party(s); await queue(q);
    expect((await status(q)).matchId).not.toBe((await status(owner)).matchId);
  });
  it("preserves grace across repeated disconnect reports and rejects reclaim at 120s", async () => {
    const s = settings(false, "Solo"); const a = await party(s); const b = await party(s);
    await queue(a); await queue(b); const m = await status(a); await admit(a, m); await admit(b, m);
    const now = Date.now(); vi.spyOn(Date, "now").mockReturnValue(now);
    try {
      const disconnected = await call<{ reconnectDeadline: number }>(matchPath(a, m, "presence"), { connected: false }, a.members[0]);
      vi.mocked(Date.now).mockReturnValue(now + 90_000);
      const repeated = await call<{ reconnectDeadline: number }>(matchPath(a, m, "presence"), { connected: false }, a.members[0]);
      expect(repeated.data.reconnectDeadline).toBe(disconnected.data.reconnectDeadline);
      vi.mocked(Date.now).mockReturnValue(now + 119_000);
      expect((await call(matchPath(a, m, "reconnect"), {}, a.members[0])).status).toBe(200);
      vi.mocked(Date.now).mockReturnValue(now + 120_000);
      expect((await call(matchPath(a, m, "reconnect"), {}, a.members[0])).status).toBe(409);
    } finally { vi.restoreAllMocks(); }
  });
});
