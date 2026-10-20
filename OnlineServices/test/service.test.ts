import { env, exports } from "cloudflare:workers";
import { describe, it, expect, vi } from "vitest";
import { importJWK, jwtVerify } from "jose";
import { poolKey, type ContentBinding, type PartySettings } from "../src/model";

const content: ContentBinding = { kind: "BuiltIn", courseId: "test-circuit", courseHash: "a".repeat(64), manifestHash: "b".repeat(64), modeHash: "c".repeat(64), catalogHash: "d".repeat(64) };
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
async function queue(g: Group, channel = "QuickPlay", extra: object = { content }) {
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
    try { await env.POOLS.getByName(poolKey(g.s)).enqueue(attempt); }
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

describe("private sessions and reconnect leases", () => {
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
