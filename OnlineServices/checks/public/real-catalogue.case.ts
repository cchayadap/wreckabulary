import { env, exports } from "cloudflare:workers";
import { afterEach, describe, expect, it } from "vitest";
import { evictDurableObject, reset } from "cloudflare:test";
import { importJWK, jwtVerify, type JWK } from "jose";
import { canonical, poolKey, type ContentBinding, type PartySettings } from "../../src/model";
import type { PublicCatalogueProjection } from "../../scripts/public-catalogue.mjs";
import { readPublicBindings } from "../../shared/public-bindings.mjs";

type Guest = { playerId: string; accessToken: string };
type Seat = { playerId: string; partyId: string; teamId: number; seat: number };
type Assignment = {
  matchId: string; state: string; reservationGeneration: string; rosterHash: string;
  roster: Seat[]; content: ContentBinding; settings: PartySettings; privateCode?: string;
};
type Party = { id: string; revision: string; invite: string; assignment: Assignment };
const source = (env as unknown as { PUBLIC_CATALOGUE_JSON: string }).PUBLIC_CATALOGUE_JSON;
const catalogue = JSON.parse(source) as PublicCatalogueProjection["catalogue"];
afterEach(async () => { await reset(); });

async function call<T>(path: string, body?: unknown, guest?: Guest) {
  const response = await exports.default.fetch(`https://local.test/v1${path}`, {
    method: body === undefined ? "GET" : "POST",
    headers: { "content-type": "application/json", "idempotency-key": crypto.randomUUID(),
      ...(guest ? { authorization: `Bearer ${guest.accessToken}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  return { status: response.status, data: await response.json() as T };
}
async function ok<T>(path: string, body?: unknown, guest?: Guest) {
  const result = await call<T>(path, body, guest);
  expect(result.status).toBe(200); return result.data;
}
function settings(row: typeof catalogue.courses[number]): PartySettings {
  return { mode: row.mode, format: row.format, fill: false, region: "asia",
    protocolVersion: catalogue.protocolVersion, mapPoolVersion: `pub-${crypto.randomUUID().slice(0, 12)}` };
}
async function group(s: PartySettings, count = 1) {
  const members: Guest[] = [];
  for (let i = 0; i < count; i++) members.push(await ok<Guest>("/guests", { displayName: "Public catalogue check" }));
  let party = await ok<Party>("/parties", { settings: s }, members[0]);
  for (const member of members.slice(1))
    party = await ok<Party>("/party-joins", { partyId: party.id, invite: party.invite }, member);
  for (const member of members)
    party = await ok<Party>(`/parties/${party.id}/ready`, { expectedRevision: party.revision, ready: true }, member);
  return { members, party };
}
type Group = Awaited<ReturnType<typeof group>>;
async function queue(g: Group, content: ContentBinding) {
  return call<Party>(`/parties/${g.party.id}/queue`,
    { expectedRevision: g.party.revision, channel: "QuickPlay", content }, g.members[0]);
}

describe("actual native catalogue projected into the Worker", () => {
  it("uses all nine exported variants per seed and keeps the release provenance", () => {
    expect(catalogue.schema).toBe(1);
    expect(catalogue.protocolVersion).toBe("wordbound-v3");
    expect(catalogue.mapPoolVersion).toMatch(/^[A-Za-z0-9_-]{1,24}$/);
    const seeds = new Set(catalogue.courses.map(row => row.seed));
    expect(catalogue.courses).toHaveLength(seeds.size * 9);
    expect(canonical(readPublicBindings(env))).toBe(canonical(catalogue.courses.map(row => row.content)));
    for (const seed of seeds) expect(catalogue.courses.filter(row => row.seed === seed)
      .map(row => `${row.mode}/${row.format}`).sort()).toEqual([
      "BrawlRace/Duo", "BrawlRace/Solo", "BrawlRace/Squad", "FreeForAll/Solo", "Freestyle/Solo",
      "TeamRace/Duo", "TeamRace/Solo", "TeamRace/Squad", "TimeRun/Solo",
    ]);
  });

  it.each(catalogue.courses)("accepts the exact exported $mode/$format public binding", async row => {
    const s = settings(row), g = await group(s, row.mode === "Freestyle" || row.mode === "TimeRun" ? 2 : 1);
    expect((await queue(g, row.content)).status).toBe(200);
    const view = await ok<Party>(`/parties/${g.party.id}`, undefined, g.members[0]);
    expect(view.assignment.content).toEqual(row.content);
    expect(view.assignment.settings).toEqual(s);
    expect(view.assignment.privateCode).toBeUndefined();
    expect(view.assignment.roster.map(seat => seat.playerId)).toEqual(g.members.map(member => member.playerId));
  });

  it("rejects changed content and a private binding without admitting it to Quick Play", async () => {
    const row = catalogue.courses.find(row => row.mode === "TeamRace" && row.format === "Duo")!;
    const changed = { ...row.content, manifestHash: (row.content.manifestHash[0] === "a" ? "b" : "a") + row.content.manifestHash.slice(1) };
    const a = await group(settings(row));
    expect((await queue(a, changed)).status).toBe(422);
    const b = await group(settings(row));
    expect((await queue(b, { ...row.content, kind: "Workshop" })).status).toBe(422);
  });

  it("keeps exact whole parties through SQLite eviction, signed start and explicit cleanup", async () => {
    const row = catalogue.courses.find(row => row.mode === "TeamRace" && row.format === "Duo")!;
    const s = settings(row), groups = [await group(s, 2), await group(s, 2)];
    for (const g of groups) expect((await queue(g, row.content)).status).toBe(200);
    const before = (await ok<Party>(`/parties/${groups[0].party.id}`, undefined, groups[0].members[0])).assignment;
    expect(before.state).toBe("ADMITTING"); expect(before.roster).toHaveLength(4);
    expect(before.privateCode).toBeUndefined();
    expect(new Set(before.roster.map(seat => seat.teamId)).size).toBe(2);
    for (const g of groups) {
      const seats = before.roster.filter(seat => seat.partyId === g.party.id);
      expect(new Set(seats.map(seat => seat.playerId))).toEqual(new Set(g.members.map(member => member.playerId)));
      expect(new Set(seats.map(seat => seat.teamId)).size).toBe(1);
    }
    await evictDurableObject(env.POOLS.getByName(poolKey(s)));
    expect((await ok<Party>(`/parties/${groups[1].party.id}`, undefined, groups[1].members[0])).assignment).toEqual(before);
    const path = `/pools/${encodeURIComponent(poolKey(s))}/matches/${before.matchId}`;
    for (const member of groups.flatMap(g => g.members))
      await ok(`${path}/ready`, { reservationGeneration: before.reservationGeneration,
        manifestHash: row.content.manifestHash, rosterHash: before.rosterHash }, member);
    const signed = await ok<{ certificate: string }>(`${path}/certificate`, {}, groups[0].members[0]);
    const jwks = await ok<{ keys: JWK[] }>("/keys");
    const verified = await jwtVerify(signed.certificate, await importJWK(jwks.keys[0], "RS256"),
      { issuer: "wordbound-local", audience: "wordbound-match-start", algorithms: ["RS256"] });
    expect(verified.payload.content).toEqual(row.content);
    expect(verified.payload.settings).toEqual(s);
    expect(verified.payload.roster).toEqual(before.roster);
    expect(verified.payload.matchId).toBe(before.matchId);
    expect(verified.payload.reservationGeneration).toBe(before.reservationGeneration);
    expect(verified.payload.rosterHash).toBe(before.rosterHash);
    for (const member of groups.flatMap(g => g.members)) await ok(`${path}/return`, {}, member);
    for (const g of groups) {
      let party = await ok<Party>(`/parties/${g.party.id}`, undefined, g.members[0]);
      party = await ok<Party>(`/parties/${party.id}/cancel`, { expectedRevision: party.revision }, g.members[0]);
      for (const member of [...g.members].reverse()) {
        party = await ok<Party>(`/parties/${party.id}/leave`, { expectedRevision: party.revision }, member);
        expect((await ok<{ partyId?: string }>("/me", undefined, member)).partyId).toBeUndefined();
      }
    }
  });
});
