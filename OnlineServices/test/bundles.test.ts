import { createHash } from "node:crypto";
import { deflateRawSync } from "node:zlib";
import { env, exports } from "cloudflare:workers";
import { evictDurableObject, reset, runInDurableObject, runDurableObjectAlarm } from "cloudflare:test";
import { afterEach, describe, expect, it, vi } from "vitest";
import { bundleId, type BundleTransfer } from "../src/bundles";
import { snapshotCanonical } from "../src/checkpoints";
import { poolKey, type PartySettings } from "../src/model";
import { rpcValue } from "../src/rpc";

afterEach(async () => { vi.restoreAllMocks(); await reset(); });
const hash = (text: string | Buffer) => createHash("sha256").update(text).digest("hex");
// Structural HTTP adversaries use tiny deterministic documents; real C# semantic interoperability has its own fixture.
function bundle(id = "authored-course", document: unknown = { format: "wreckabulary.course", id, schema: 2 }): BundleTransfer {
  const courseHash = hash(snapshotCanonical(document)), geometryHash = "e".repeat(64);
  const manifest: any = { format: "wreckabulary.resolved-course", schema: 2, seed: "0123456789abcdef",
    catalogueHash: geometryHash, sourceHash: courseHash, document: { id, schema: 2 } };
  manifest.hash = hash(snapshotCanonical(manifest));
  const content = { kind: "Workshop" as const, courseId: id, courseHash, manifestHash: manifest.hash, modeHash: "c".repeat(64), catalogHash: "d".repeat(64) };
  const raw = Buffer.from(snapshotCanonical({ format: "wreckabulary.workshop-bundle", schema: 1, protocolVersion: "wordbound-v3",
    mode: "Freestyle", teamFormat: "Solo", seed: manifest.seed, geometryHash, itemHash: "f".repeat(64), rulesHash: "a".repeat(64),
    content, document, manifest }));
  const header = Buffer.alloc(40); header.writeUInt32LE(0x31534257); header.writeInt32LE(raw.length, 4); Buffer.from(hash(raw), "hex").copy(header, 8);
  const packet = Buffer.concat([header, deflateRawSync(raw)]);
  const descriptor: any = { content, mode: "Freestyle", format: "Solo", seed: manifest.seed, protocolVersion: "wordbound-v3", geometryHash,
    itemHash: "f".repeat(64), rulesHash: "a".repeat(64), wireHash: hash(packet), rawLength: raw.length, totalBytes: packet.length, chunkCount: Math.ceil(packet.length / 4096) };
  descriptor.bundleId = bundleId(descriptor);
  return { descriptor, chunks: Array.from({ length: descriptor.chunkCount }, (_, i) => packet.subarray(i * 4096, (i + 1) * 4096).toString("base64url")) };
}
type Guest = { playerId: string; accessToken: string };
type Party = { id: string; revision: string; invite: string; workshop?: BundleTransfer["descriptor"]; activeAttempt?: any; assignment?: any };
async function call<T = any>(path: string, value?: unknown, actor?: Guest, key = crypto.randomUUID(), method = "POST") {
  const response = await exports.default.fetch(`https://local.test/v1${path}`, {
    method: value === undefined ? "GET" : method,
    headers: { "content-type": "application/json", "idempotency-key": key, ...(actor ? { authorization: `Bearer ${actor.accessToken}` } : {}) },
    body: value === undefined ? undefined : JSON.stringify(value),
  });
  return { status: response.status, data: await response.json() as T };
}
async function create() {
  const actor = (await call<Guest>("/guests", { displayName: "Bundle test" })).data;
  const settings: PartySettings = { mode: "Freestyle", format: "Solo", fill: false, region: "asia", protocolVersion: "wordbound-v3", mapPoolVersion: crypto.randomUUID().slice(0, 8) };
  const party = (await call<Party>("/parties", { settings }, actor)).data; return { actor, settings, party };
}
async function upload(g: Awaited<ReturnType<typeof create>>, b: BundleTransfer, revision = g.party.revision) {
  expect((await call(`/parties/${g.party.id}/content-begin`, { expectedRevision: revision, descriptor: b.descriptor }, g.actor)).status).toBe(200);
  for (let index = 0; index < b.chunks.length; index++)
    expect((await call(`/parties/${g.party.id}/content-chunk`, { expectedRevision: revision, bundleId: b.descriptor.bundleId, index, data: b.chunks[index] }, g.actor)).status).toBe(200);
  const final = await call<Party>(`/parties/${g.party.id}/content-finalize`, { expectedRevision: revision, bundleId: b.descriptor.bundleId }, g.actor);
  expect(final.status).toBe(200); g.party = final.data; return final.data;
}
describe("authenticated exact workshop bundle transfer", () => {
  it.each([null, [], false, "map"])("rejects a non-document payload without selecting it: %j", async document => {
    const g = await create(), b = bundle("invalid-document", document), path = `/parties/${g.party.id}`;
    expect((await call(`${path}/content-begin`, { expectedRevision: g.party.revision, descriptor: b.descriptor }, g.actor)).status).toBe(200);
    for (let index = 0; index < b.chunks.length; index++)
      expect((await call(`${path}/content-chunk`, { expectedRevision: g.party.revision, bundleId: b.descriptor.bundleId,
        index, data: b.chunks[index] }, g.actor)).status).toBe(200);
    const rejected = await call(`${path}/content-finalize`, { expectedRevision: g.party.revision, bundleId: b.descriptor.bundleId }, g.actor);
    expect(rejected.status).toBe(422); expect(rejected.data.error.code).toBe("CONTENT_DOCUMENT");
    expect((await call(`${path}/content-info`, {}, g.actor)).data.descriptor).toBeNull();
  });
  it("fences upload to the current leader/revision and keeps pending bytes unreadable", async () => {
    const g = await create(), outsider = (await create()).actor, b = bundle();
    const path = `/parties/${g.party.id}`;
    expect((await call(`${path}/content-begin`, { expectedRevision: "1", descriptor: b.descriptor }, outsider)).status).toBe(403);
    expect((await call(`${path}/content-begin`, { expectedRevision: "0", descriptor: b.descriptor }, g.actor)).status).toBe(409);
    expect((await call(`${path}/content-begin`, { expectedRevision: "1", descriptor: b.descriptor }, g.actor)).status).toBe(200);
    expect((await call(`${path}/content-read`, { bundleId: b.descriptor.bundleId, index: 0 }, g.actor)).status).toBe(404);
    expect((await call(`${path}/content-finalize`, { expectedRevision: "1", bundleId: b.descriptor.bundleId }, g.actor)).status).toBe(409);
    expect((await call(`${path}/content-chunk`, { expectedRevision: "1", bundleId: b.descriptor.bundleId, index: 0, data: b.chunks[0], playerId: outsider.playerId }, g.actor)).status).toBe(400);
  });
  it("persists complete bytes through Party/Pool eviction and limits reads to owning or assigned humans", async () => {
    const g = await create(), b = bundle(), outsider = (await create()).actor;
    const originalRevision = g.party.revision; await upload(g, b);
    expect(g.party.revision).not.toBe(originalRevision);
    expect((await call(`/parties/${g.party.id}/content-finalize`, { expectedRevision: originalRevision, bundleId: b.descriptor.bundleId }, g.actor)).status).toBe(200);
    await evictDurableObject(env.PARTIES.getByName(g.party.id));
    expect((await call(`/parties/${g.party.id}/content-read`, { bundleId: b.descriptor.bundleId, index: 0 }, g.actor)).data.data).toBe(b.chunks[0]);
    await call(`/parties/${g.party.id}/ready`, { expectedRevision: g.party.revision, ready: true }, g.actor);
    expect((await call(`/parties/${g.party.id}/queue`, { expectedRevision: g.party.revision, channel: "QuickPlay", content: b.descriptor.content }, g.actor)).status).toBe(409);
    expect((await call(`/parties/${g.party.id}/queue`, { expectedRevision: g.party.revision, channel: "PrivateCreate", content: b.descriptor.content }, g.actor)).status).toBe(200);
    let view = (await call<Party>(`/parties/${g.party.id}`, undefined, g.actor)).data;
    const matchPath = `/pools/${encodeURIComponent(poolKey(g.settings))}/matches/${view.assignment.matchId}`;
    await evictDurableObject(env.POOLS.getByName(poolKey(g.settings)));
    expect((await call(`${matchPath}/content-read`, { bundleId: b.descriptor.bundleId, index: 0 }, outsider)).status).toBe(403);
    expect((await call(`${matchPath}/content-read`, { bundleId: b.descriptor.bundleId, index: 0 }, g.actor)).data.data).toBe(b.chunks[0]);
    await call(`${matchPath}/launch`, {}, g.actor);
    view = (await call<Party>(`/parties/${g.party.id}`, undefined, g.actor)).data;
    await call(`${matchPath}/ready`, { reservationGeneration: view.assignment.reservationGeneration, rosterHash: view.assignment.rosterHash, manifestHash: b.descriptor.content.manifestHash }, g.actor);
    await call(`${matchPath}/return`, {}, g.actor);
    expect((await call(`${matchPath}/content-info`, {}, g.actor)).status).toBe(403);
  });
  it("rejects mixed chunks, descriptor substitutions and expired partial uploads", async () => {
    const g = await create(), b = bundle();
    const begin = `/parties/${g.party.id}/content-begin`;
    const changed = { ...b.descriptor, seed: "ffffffffffffffff" };
    expect((await call(begin, { expectedRevision: "1", descriptor: changed }, g.actor)).status).toBe(422);
    expect((await call(begin, { expectedRevision: "1", descriptor: b.descriptor }, g.actor)).status).toBe(200);
    const path = `/parties/${g.party.id}/content-chunk`, part = { expectedRevision: "1", bundleId: b.descriptor.bundleId, index: 0, data: b.chunks[0] };
    expect((await call(path, part, g.actor)).status).toBe(200);
    const corrupt = Buffer.from(b.chunks[0], "base64url"); corrupt[40] ^= 1;
    expect((await call(path, { ...part, data: corrupt.toString("base64url") }, g.actor)).status).toBe(409);
    vi.spyOn(Date, "now").mockReturnValue(Date.now() + 60000);
    expect((await call(`/parties/${g.party.id}/content-finalize`, { expectedRevision: "1", bundleId: b.descriptor.bundleId }, g.actor)).status).toBe(409);
  });
  it("retains authored identity after source expiry and denies readiness instead of becoming a builtin party", async () => {
    const g = await create(), b = bundle(); await upload(g, b);
    const stub = env.PARTIES.getByName(g.party.id);
    vi.spyOn(Date, "now").mockReturnValue(Date.now() + 86400001);
    expect(await runDurableObjectAlarm(stub)).toBe(true);
    const view: Party = await rpcValue(stub.get(g.actor.playerId));
    expect(view.workshop).toEqual(b.descriptor);
    await expect(rpcValue(stub.content(g.actor.playerId, "content-read", { bundleId: b.descriptor.bundleId, index: 0 })))
      .rejects.toThrow("CONTENT_NOT_FOUND");
    await expect(rpcValue(stub.mutate(g.actor.playerId, crypto.randomUUID(), {
      kind: "ready", expectedRevision: view.revision, ready: true,
    }))).rejects.toThrow("CONTENT_SOURCE_EXPIRED");
    const after: Party = await rpcValue(stub.get(g.actor.playerId));
    expect(after.workshop).toEqual(b.descriptor);
  });
  it("can reselect an immutable completed bundle after another map without old-revision replay changing selection", async () => {
    const g = await create(), a = bundle("map-a"), b = bundle("map-b");
    const first = g.party.revision; await upload(g, a); await upload(g, b);
    expect((await call(`/parties/${g.party.id}/content-finalize`, { expectedRevision: first, bundleId: a.descriptor.bundleId }, g.actor)).status).toBe(409);
    await upload(g, a); expect(g.party.workshop?.bundleId).toBe(a.descriptor.bundleId);
  });
  it("rebinds completed bytes after mode changes and evicts old completed selections within the three-record bound", async () => {
    const g = await create(), a = bundle("map-a"); await upload(g, a);
    for (const mode of ["TimeRun", "Freestyle"] as const) {
      const changed = await call<Party>(`/parties/${g.party.id}`, {
        expectedRevision: g.party.revision, settings: { ...g.settings, mode },
      }, g.actor, crypto.randomUUID(), "PATCH");
      expect(changed.status).toBe(200); g.party = changed.data;
      expect(g.party.workshop).toBeUndefined();
    }
    await upload(g, a);
    for (const id of ["map-b", "map-c", "map-d"]) await upload(g, bundle(id));
    const rows = await runInDurableObject(env.PARTIES.getByName(g.party.id), (_instance, state) =>
      state.storage.sql.exec<{ bundle_id: string }>("SELECT bundle_id FROM workshop_bundles").toArray());
    expect(rows).toHaveLength(3);
    expect(rows.some(row => row.bundle_id === g.party.workshop!.bundleId)).toBe(true);
    expect((await call(`/parties/${g.party.id}/content-read`, { bundleId: a.descriptor.bundleId, index: 0 }, g.actor)).status).toBe(404);
  });
  it("does not leave copied matches orphaned when an enqueue response is lost and source bytes expire", async () => {
    const g = await create(), b = bundle(); await upload(g, b);
    await call(`/parties/${g.party.id}/ready`, { expectedRevision: g.party.revision, ready: true }, g.actor);
    const key = crypto.randomUUID(), request = { expectedRevision: g.party.revision, channel: "PrivateCreate", content: b.descriptor.content };
    const queued = await call<Party>(`/parties/${g.party.id}/queue`, request, g.actor, key); expect(queued.status).toBe(200);
    const attempt = queued.data.activeAttempt;
    // Reconstruct the precise crash point after Pool commit but before Party.finish, using real SQLite state.
    await runInDurableObject(env.PARTIES.getByName(g.party.id), (_instance, state) => {
      const row = state.storage.sql.exec<{ value: string }>("SELECT value FROM state WHERE id=1").toArray()[0];
      const party = JSON.parse(row.value); const receipt = party.receipts[key]; delete party.receipts[key]; delete party.active;
      party.operation = { key, hash: receipt.hash, kind: "queue", attempt };
      state.storage.sql.exec("UPDATE state SET value=? WHERE id=1", JSON.stringify(party));
      state.storage.sql.exec("DELETE FROM workshop_chunks"); state.storage.sql.exec("DELETE FROM workshop_bundles");
    });
    const retried = await call<Party>(`/parties/${g.party.id}/queue`, request, g.actor, key);
    expect(retried.status).toBe(200); expect(retried.data.activeAttempt.id).toBe(attempt.id);
    const view = (await call<Party>(`/parties/${g.party.id}`, undefined, g.actor)).data;
    expect(view.assignment.workshop.bundleId).toBe(b.descriptor.bundleId);
  });
  it("requires complete bytes for fresh authored attempts and rejects changed bundle identity on assigned replay", async () => {
    const g = await create(), b = bundle(); await upload(g, b);
    await call(`/parties/${g.party.id}/ready`, { expectedRevision: g.party.revision, ready: true }, g.actor);
    const queued = await call<Party>(`/parties/${g.party.id}/queue`, {
      expectedRevision: g.party.revision, channel: "PrivateCreate", content: b.descriptor.content,
    }, g.actor);
    expect(queued.status).toBe(200);
    const pool = env.POOLS.getByName(poolKey(g.settings)), attempt = queued.data.activeAttempt;
    await expect(rpcValue(pool.enqueue({ ...attempt, workshopId: "0".repeat(64) }))).rejects.toThrow("IDEMPOTENCY_CONFLICT");
    const other = await create();
    await expect(rpcValue(pool.enqueue({ ...attempt, id: crypto.randomUUID(), partyId: other.party.id,
      partyRevision: other.party.revision, members: [other.actor.playerId], createdAt: Date.now() }))).rejects.toThrow("CONTENT_SOURCE_EXPIRED");
    const view = (await call<Party>(`/parties/${g.party.id}`, undefined, g.actor)).data;
    expect(view.assignment.workshop.bundleId).toBe(b.descriptor.bundleId);
  });
  it("alarms an abandoned started match closed and removes its copied bundle at existing retention expiry", async () => {
    const g = await create(), b = bundle(); await upload(g, b);
    await call(`/parties/${g.party.id}/ready`, { expectedRevision: g.party.revision, ready: true }, g.actor);
    await call(`/parties/${g.party.id}/queue`, { expectedRevision: g.party.revision, channel: "PrivateCreate", content: b.descriptor.content }, g.actor);
    let view = (await call<Party>(`/parties/${g.party.id}`, undefined, g.actor)).data;
    const matchId = view.assignment.matchId, path = `/pools/${encodeURIComponent(poolKey(g.settings))}/matches/${matchId}`;
    await call(`${path}/launch`, {}, g.actor);
    view = (await call<Party>(`/parties/${g.party.id}`, undefined, g.actor)).data;
    expect((await call(`${path}/ready`, { reservationGeneration: view.assignment.reservationGeneration,
      rosterHash: view.assignment.rosterHash, manifestHash: b.descriptor.content.manifestHash }, g.actor)).status).toBe(200);
    const stub = env.POOLS.getByName(poolKey(g.settings));
    const state = await runInDurableObject(stub, async (_instance, storage) => {
      const pool = JSON.parse(storage.storage.sql.exec<{ value: string }>("SELECT value FROM state WHERE id=1").toArray()[0].value);
      return { match: pool.matches[matchId], alarm: await storage.storage.getAlarm() };
    });
    expect(state.alarm).toBe(state.match.seats[0].reconnectDeadline);
    vi.spyOn(Date, "now").mockReturnValue(state.match.seats[0].reconnectDeadline);
    expect(await runDurableObjectAlarm(stub)).toBe(true);
    const closed = await runInDurableObject(stub, (_instance, storage) =>
      JSON.parse(storage.storage.sql.exec<{ value: string }>("SELECT value FROM state WHERE id=1").toArray()[0].value).matches[matchId]);
    expect(closed.phase).toBe("CLOSED");
    vi.mocked(Date.now).mockReturnValue(state.match.createdAt + 86400001);
    expect(await runDurableObjectAlarm(stub)).toBe(true);
    const count = await runInDurableObject(stub, (_instance, storage) =>
      storage.storage.sql.exec<{ count: number }>("SELECT COUNT(*) AS count FROM workshop_bundles WHERE scope_id=?", matchId).toArray()[0].count);
    expect(count).toBe(0);
  });
});
