import { env, exports } from "cloudflare:workers";
import { runInDurableObject, evictDurableObject, reset, runDurableObjectAlarm } from "cloudflare:test";
import { describe, it, expect, afterEach, vi } from "vitest";
import { randomBytes, createHash } from "node:crypto";
import { deflateRawSync, inflateRawSync } from "node:zlib";
import { checkpointBinding, checkpointPreflight, checkpointUploadId, snapshotCanonical, validateCheckpointPacket, type CheckpointAction, type Manifest } from "../src/checkpoints";
import { schema2Fixture } from "./fixtures/schema2";
import { canonical, digest, emptyPool, poolKey, secret, publicRoster, type Match } from "../src/model";
import { rpcValue } from "../src/rpc";
afterEach(async () => { vi.restoreAllMocks(); await reset(); });
type Guest = { playerId: string; accessToken: string };
async function http(path: string, data: unknown, guest?: Guest) {
  const response = await exports.default.fetch("https://local.test/v1" + path, { method: "POST", headers: { "content-type": "application/json", ...(guest ? { authorization: "Bearer " + guest.accessToken } : {}) }, body: JSON.stringify(data) });
  return { status: response.status, data: await response.json() as any };
}
async function setup() {
  const guests = await Promise.all([0, 1, 2].map(async () => (await http("/guests", { displayName: "Checkpoint test" })).data as Guest));
  const now = Date.now(); const match: Match = { id: crypto.randomUUID(), generation: "1", ownerId: guests[0].playerId, createdAt: now, startedAt: now, phase: "STARTED", reservations: [],
    settings: { mode: "Freestyle", format: "Solo", fill: false, region: "asia", protocolVersion: "wordbound-v2", mapPoolVersion: crypto.randomUUID().slice(0, 8) },
    content: { kind: "Workshop", courseId: "checkpoint-test", courseHash: "a".repeat(64), manifestHash: "b".repeat(64), modeHash: "c".repeat(64), catalogHash: "d".repeat(64) },
    seats: guests.slice(0, 2).map((g, i) => ({ playerId: g.playerId, partyId: crypto.randomUUID(), partyRevision: "1", attemptId: crypto.randomUUID(), teamId: 0, seat: i, connectionSecret: secret(), accepted: true, reconnectDeadline: now + 120_000 })) };
  match.rosterHash = digest(publicRoster(match)); const stub = env.POOLS.getByName(poolKey(match.settings));
  await runInDurableObject(stub, async (_, state) => { const pool = emptyPool(); pool.matches[match.id] = match; state.storage.sql.exec("INSERT INTO state VALUES(1,?)", JSON.stringify(pool)); });
  const call = (action: string, data: unknown = {}, actor = 0) => http(`/pools/${encodeURIComponent(poolKey(match.settings))}/matches/${match.id}/${action}`, data, guests[actor]);
  const claimInput = () => ({ claimId: crypto.randomUUID(), reservationGeneration: "1", rosterHash: match.rosterHash });
  return { match, stub, guests, call, claimInput, now };
}
type Fixture = Awaited<ReturnType<typeof setup>>;
function packet(f: Fixture, term = "1", epoch = "0", sequence = "1", tick = "1", previousHeadId: string | null = null, padding = 0, mutate?: (root: any) => void) {
  const { binding, identityHash } = checkpointBinding(f.match);
  const noise = Buffer.concat(Array.from({ length: Math.ceil(padding / 60000) }, (_, i) => randomBytes(Math.min(60000, padding - i * 60000))));
  const text = noise.toString("base64");
  const unsigned: any = { format: "wreckabulary.match-snapshot", schema: 2, canonicalVersion: 2, payload: { binding, epoch, sequence, progress: { tick }, commands: { epoch, identityHash }, testPadding: Array.from({ length: Math.ceil(text.length / 16000) }, (_, i) => text.slice(i * 16000, (i + 1) * 16000)) } };
  mutate?.(unsigned); const stateHash = digest(unsigned); const raw = Buffer.from(canonical({ ...unsigned, hash: stateHash }));
  const header = Buffer.alloc(40); header.writeUInt32LE(0x31534257); header.writeInt32LE(raw.length, 4); createHash("sha256").update(raw).digest().copy(header, 8);
  const bytes = Buffer.concat([header, deflateRawSync(raw)]);
  const manifest: Manifest = { uploadId: "", term, previousHeadId, stamp: { matchId: f.match.id, identityHash, epoch, sequence, tick, stateHash }, rawLength: raw.length, wireHash: createHash("sha256").update(bytes).digest("hex"), totalBytes: bytes.length, chunkCount: Math.ceil(bytes.length / 4096) };
  manifest.uploadId = checkpointUploadId(manifest);
  return { manifest, chunks: Array.from({ length: manifest.chunkCount }, (_, i) => bytes.subarray(i * 4096, (i + 1) * 4096).toString("base64url")) };
}
async function upload(f: Fixture, p: ReturnType<typeof packet>, actor = 0) {
  expect((await f.call("checkpoint-begin", p.manifest, actor)).status).toBe(200);
  const responses = await Promise.all(p.chunks.map((data, index) => f.call("checkpoint-chunk", { term: p.manifest.term, uploadId: p.manifest.uploadId, index, data }, actor)));
  expect(responses.map(r => r.status)).toEqual(p.chunks.map(() => 200));
}
function finish(f: Fixture, p: ReturnType<typeof packet>, actor = 0) { return f.call("checkpoint-finalize", { term: p.manifest.term, uploadId: p.manifest.uploadId }, actor); }
function realFixture(f: Fixture) {
  const root = JSON.parse(schema2Fixture.json), b = root.payload.binding;
  f.match.id = b.matchId; f.match.rosterHash = b.rosterHash; f.match.ownerId = b.seats[0].playerId;
  f.match.settings.mode = "BrawlRace";
  Object.assign(f.match.content, { modeHash: b.modeHash, courseHash: b.courseHash, manifestHash: b.manifestHash, catalogHash: b.catalogueHash });
  f.match.seats.forEach((seat, i) => Object.assign(seat, { playerId: b.seats[i].playerId, teamId: b.seats[i].team, seat: b.seats[i].slot }));
  const manifest: Manifest = { uploadId: "", term: "1", previousHeadId: null,
    stamp: { matchId: b.matchId, identityHash: schema2Fixture.identityHash, epoch: root.payload.epoch,
      sequence: root.payload.sequence, tick: root.payload.progress.tick, stateHash: root.hash },
    rawLength: schema2Fixture.rawLength, wireHash: schema2Fixture.wireHash, totalBytes: schema2Fixture.totalBytes, chunkCount: schema2Fixture.chunks.length };
  manifest.uploadId = checkpointUploadId(manifest);
  return { root, manifest, chunks: [...schema2Fixture.chunks] };
}

describe("canonical checkpoints in actual SQLite Durable Objects", () => {
  it("requires authenticated roster host and one exclusive writer; rejects supplied identity", async () => {
    const f = await setup();
    expect((await f.call("checkpoint-claim", f.claimInput(), 2)).status).toBe(403);
    expect((await f.call("checkpoint-claim", f.claimInput(), 1)).data.error.code).toBe("CHECKPOINT_INITIAL_HOST");
    const input = f.claimInput(); const a = await f.call("checkpoint-claim", input);
    expect(a.status).toBe(200); expect(a.data.writer.term).toBe("1"); expect(a.data.head).toBeNull();
    expect((await f.call("checkpoint-claim", input)).data.writer).toEqual(a.data.writer);
    expect((await f.call("checkpoint-claim", f.claimInput(), 1)).data.error.code).toBe("CHECKPOINT_WRITER_BUSY");
    expect((await f.call("checkpoint-renew", { term: "1", playerId: f.guests[0].playerId }, 1)).status).toBe(400);
    expect((await f.call("checkpoint-renew", { term: "1" }, 1)).status).toBe(409);
    expect((await f.call("checkpoint-begin", packet(f, "1", "0", "0").manifest)).status).toBe(400);
  });
  it("opens admission only for the live writer and its claimed restore head", async () => {
    const f = await setup(); const c = await f.call("checkpoint-claim", f.claimInput());
    expect(c.data.writer.admissionOpen).toBe(false);
    expect((await f.call("checkpoint-open", { term: "1", headId: null }, 1)).status).toBe(409);
    expect((await f.call("checkpoint-open", { term: "1", headId: "a".repeat(64) })).status).toBe(409);
    expect((await f.call("checkpoint-open", { term: "1", headId: null })).data.writer.admissionOpen).toBe(true);
    vi.spyOn(Date, "now").mockReturnValue(c.data.writer.expiresAt);
    expect((await f.call("checkpoint-open", { term: "1", headId: null })).status).toBe(409);
    const next = await f.call("checkpoint-claim", f.claimInput(), 1);
    expect(next.data.writer.admissionOpen).toBe(false);
  });
  it("never exposes partial uploads and durably retains every33chunk byte across eviction", async () => {
    const f = await setup(); await f.call("checkpoint-claim", f.claimInput()); const p = packet(f, "1", "0", "1", "1", null, 130_000);
    expect(inflateRawSync(Buffer.concat(p.chunks.map(c => Buffer.from(c, "base64url"))).subarray(40), { maxOutputLength: 8388608 }).length).toBe(p.manifest.rawLength);
    expect(p.chunks.length).toBeGreaterThanOrEqual(32); await upload(f, p);
    expect((await f.call("checkpoint-head")).data.head).toBeNull();
    expect((await f.call("checkpoint-read", { uploadId: p.manifest.uploadId, index: 0 })).status).toBe(404);
    expect((await finish(f, p)).data.head).toEqual(p.manifest); await evictDurableObject(f.stub);
    expect((await f.call("checkpoint-head")).data.head).toEqual(p.manifest);
    const chunks = await Promise.all(p.chunks.map((_, index) => f.call("checkpoint-read", { uploadId: p.manifest.uploadId, index })));
    expect(chunks.map(r => r.data.data)).toEqual(p.chunks);
  });
  it("rejects incomplete/mutated uploads and permits exact immutable retries", async () => {
    const f = await setup(); await f.call("checkpoint-claim", f.claimInput()); const p = packet(f, "1", "0", "1", "1", null, 9000);
    expect((await f.call("checkpoint-begin", p.manifest)).status).toBe(200);
    expect((await f.call("checkpoint-begin", p.manifest)).status).toBe(200);
    expect((await f.call("checkpoint-begin", { ...p.manifest, wireHash: "0".repeat(64) })).data.error.code).toBe("CHECKPOINT_UPLOAD_ID");
    const chunk = { term: "1", uploadId: p.manifest.uploadId, index: 0, data: p.chunks[0] };
    expect((await f.call("checkpoint-chunk", chunk)).status).toBe(200); expect((await f.call("checkpoint-chunk", chunk)).status).toBe(200);
    const changed = Buffer.from(p.chunks[0], "base64url"); changed[50] ^= 1;
    expect((await f.call("checkpoint-chunk", { ...chunk, data: changed.toString("base64url") })).data.error.code).toBe("IDEMPOTENCY_CONFLICT");
    expect((await finish(f, p)).data.error.code).toBe("CHECKPOINT_INCOMPLETE"); expect((await f.call("checkpoint-head")).data.head).toBeNull();
  });
  it("linearizes competing complete finalizes by exact prior-head CAS", async () => {
    const f = await setup(); await f.call("checkpoint-claim", f.claimInput()); const a = packet(f), b = packet(f, "1", "0", "2"); await upload(f, a); await upload(f, b);
    const responses = await Promise.all([finish(f, a), finish(f, b)]); expect(responses.map(r => r.status).sort()).toEqual([200, 409]);
    const winner = responses.find(r => r.status === 200)!.data.head; expect((await f.call("checkpoint-head")).data.head).toEqual(winner);
    expect((await finish(f, winner.uploadId === a.manifest.uploadId ? a : b)).data.head).toEqual(winner);
  });
  it("keeps prior complete head and resolves a lost finalize response after exact lease expiry", async () => {
    const f = await setup(); const claimed = await f.call("checkpoint-claim", f.claimInput()); const a = packet(f); await upload(f, a); await finish(f, a);
    const b = packet(f, "1", "0", "2", "2", a.manifest.uploadId); await upload(f, b); expect((await f.call("checkpoint-head")).data.head).toEqual(a.manifest); await finish(f, b);
    vi.spyOn(Date, "now").mockReturnValue(claimed.data.writer.expiresAt);
    expect((await finish(f, b)).status).toBe(409);
    const head = await f.call("checkpoint-head"); expect(head.data.head).toEqual(b.manifest); expect(head.data.previous).toEqual(a.manifest);
    expect((await f.call("checkpoint-read", { uploadId: b.manifest.uploadId, index: 0 })).data.data).toBe(b.chunks[0]);
  });
  it("retries an entire committed upload and prevents retired handle reuse with other bytes", async () => {
    const f = await setup(); await f.call("checkpoint-claim", f.claimInput()); const a = packet(f);
    await upload(f, a); await finish(f, a); await upload(f, a); expect((await finish(f, a)).data.head).toEqual(a.manifest);
    const b = packet(f, "1", "0", "2", "2", a.manifest.uploadId); await upload(f, b); await finish(f, b);
    const c = packet(f, "1", "0", "3", "3", b.manifest.uploadId); await upload(f, c); await finish(f, c);
    expect((await f.call("checkpoint-read", { uploadId: a.manifest.uploadId, index: 0 })).status).toBe(404);
    const d = packet(f, "1", "0", "4", "4", c.manifest.uploadId);
    expect((await f.call("checkpoint-begin", { ...d.manifest, uploadId: a.manifest.uploadId })).data.error.code).toBe("CHECKPOINT_UPLOAD_ID");
    expect((await f.call("checkpoint-begin", a.manifest)).data.error.code).toBe("CHECKPOINT_HEAD_CONFLICT");
  });
  it("fences delayed old writes; failed takeover claims do not skip gameplay epochs", async () => {
    const f = await setup(); const input = f.claimInput(); const c = await f.call("checkpoint-claim", input); const a = packet(f); await upload(f, a); await finish(f, a);
    const pending = packet(f, "1", "0", "2", "2", a.manifest.uploadId); await upload(f, pending);
    const time = vi.spyOn(Date, "now").mockReturnValue(c.data.writer.expiresAt);
    expect((await f.call("checkpoint-renew", { term: "1" })).status).toBe(409); expect((await f.call("checkpoint-claim", input)).status).toBe(409);
    const candidates = await Promise.all([f.call("checkpoint-claim", f.claimInput(), 1), f.call("checkpoint-claim", f.claimInput(), 1)]); expect(candidates.map(r => r.status).sort()).toEqual([200, 409]);
    const next = candidates.find(r => r.status === 200)!.data; expect(next.writer.term).toBe("2"); expect(next.writer.epoch).toBe("1"); expect(next.head).toEqual(a.manifest);
    expect((await finish(f, pending)).status).toBe(409);
    expect((await f.call("checkpoint-begin", packet(f, "2", "0", "2", "2", a.manifest.uploadId).manifest, 1)).data.error.code).toBe("CHECKPOINT_BINDING");
    time.mockReturnValue(next.writer.expiresAt); const third = await f.call("checkpoint-claim", f.claimInput()); expect(third.data.writer.term).toBe("3"); expect(third.data.writer.epoch).toBe("1");
    const good = packet(f, "3", "1", "2", "2", a.manifest.uploadId); await upload(f, good); expect((await finish(f, good)).status).toBe(200);
  });
  it("terminal Return atomically denies writes and permits eligible replacement with empty epoch0", async () => {
    const f = await setup(); await f.call("checkpoint-claim", f.claimInput()); const p = packet(f); await upload(f, p); expect((await f.call("return")).status).toBe(200);
    expect((await finish(f, p)).data.error.code).toBe("RECONNECT_EXPIRED"); expect((await f.call("checkpoint-renew", { term: "1" })).status).toBe(409); expect((await f.call("checkpoint-claim", f.claimInput())).status).toBe(409);
    const other = await f.call("checkpoint-claim", f.claimInput(), 1); expect(other.status).toBe(200); expect(other.data.writer.epoch).toBe("0");
  });
  it.each(["wire", "raw", "schema", "binding", "state"])("rejects corrupted %s without advancing head", async kind => {
    const f = await setup(); await f.call("checkpoint-claim", f.claimInput()); const p = packet(f, "1", "0", "1", "1", null, 0, root => { if (kind === "schema") root.schema = 1; if (kind === "binding") root.payload.binding.seats[0].playerId = crypto.randomUUID(); });
    if (kind === "wire") p.manifest.wireHash = "0".repeat(64); if (kind === "state") p.manifest.stamp.stateHash = "0".repeat(64);
    if (kind === "raw") { const bytes = Buffer.from(p.chunks[0], "base64url"); bytes[8] ^= 1; p.chunks[0] = bytes.toString("base64url"); p.manifest.wireHash = createHash("sha256").update(bytes).digest("hex"); }
    p.manifest.uploadId = checkpointUploadId(p.manifest);
    await upload(f, p); expect((await finish(f, p)).status).toBe(409); expect((await f.call("checkpoint-head")).data.head).toBeNull();
  });
  it("bounds orphan upload count and removes uncommitted chunks on the actual alarm", async () => {
    const f = await setup(); await f.call("checkpoint-claim", f.claimInput()); const a = packet(f), b = packet(f, "1", "0", "2"), c = packet(f, "1", "0", "3"); await upload(f, a); await upload(f, b);
    expect((await f.call("checkpoint-begin", c.manifest)).status).toBe(429); vi.spyOn(Date, "now").mockReturnValue(f.now + 61_000); await runDurableObjectAlarm(f.stub);
    await runInDurableObject(f.stub, async (_, state) => { expect(state.storage.sql.exec<{ n: number }>("SELECT COUNT(*) AS n FROM checkpoint_uploads").one().n).toBe(0); expect(state.storage.sql.exec<{ n: number }>("SELECT COUNT(*) AS n FROM checkpoint_chunks").one().n).toBe(0); });
  });
  it("accepts real C# codec+Deflate bytes including control characters and supplementary Unicode", async () => {
    const f = await setup(); const { root, manifest } = realFixture(f);
    expect(snapshotCanonical(root)).toBe(schema2Fixture.json);
    expect(schema2Fixture.json).toContain("\\u000a");
    expect(schema2Fixture.json).toContain("🧸");
    expect(root.payload.mechanisms.length).toBeGreaterThan(0);
    expect(root.payload.mechanisms.every((m: any) => Object.keys(m.physical).sort().join(",") ===
      "actorId,auxiliaryBodyId,motionUnits,siteHash,stage,stageTick")).toBe(true);
    const complete = root.payload.mechanisms.find((m: any) => m.physical.stage === "Complete");
    expect(complete.physical.actorId).toBe(root.payload.binding.seats[0].playerId);
    expect(complete.physical.siteHash).toMatch(/^[a-f0-9]{64}$/);
    expect(BigInt(complete.physical.stageTick)).toBeGreaterThan(0n);
    expect(root.payload.thrownFlights).toHaveLength(1);
    const flight = root.payload.thrownFlights[0];
    expect(flight.throwerId).toBe(root.payload.binding.seats[0].playerId);
    expect(flight.launchRevision).toBeGreaterThan(0);
    expect(flight.hitBodyIds).toEqual(["player:1"]);
    expect(root.payload.economy.items.find((i: any) => "item:" + i.id === flight.itemBodyId)).toMatchObject({ word: "BALL", state: "Thrown" });
    const bytes = Buffer.concat(schema2Fixture.chunks.map(c => Buffer.from(c, "base64url")));
    expect(() => validateCheckpointPacket(f.match, manifest, bytes)).not.toThrow();
    const duplicate = schema2Fixture.json.replace('"schema":2', '"schema":2,"schema":2');
    const raw = Buffer.from(duplicate), header = Buffer.alloc(40);
    header.writeUInt32LE(0x31534257); header.writeInt32LE(raw.length, 4); createHash("sha256").update(raw).digest().copy(header, 8);
    const changed = Buffer.concat([header, deflateRawSync(raw)]);
    expect(() => validateCheckpointPacket(f.match, { ...manifest, totalBytes: changed.length, rawLength: raw.length,
      wireHash: createHash("sha256").update(changed).digest("hex") }, changed)).toThrow("CHECKPOINT_CANONICAL_JSON");
  });
  it("retains the exact real C# physical and flight checkpoint across SQLite Durable Object eviction", async () => {
    const f = await setup(), p = realFixture(f);
    const stub = env.POOLS.getByName(poolKey(f.match.settings));
    await runInDurableObject(stub, async (_, state) => {
      const pool = emptyPool(); pool.matches[f.match.id] = f.match;
      state.storage.sql.exec("INSERT INTO state VALUES(1,?)", JSON.stringify(pool));
    });
    const call = async (action: CheckpointAction, data: unknown = {}) =>
      rpcValue(stub.checkpoint(f.match.id, f.match.ownerId, action, data)) as Promise<any>;
    expect((await call("checkpoint-claim", f.claimInput())).writer.epoch).toBe("0");
    await call("checkpoint-begin", p.manifest);
    for (let index = 0; index < p.chunks.length; index++)
      await call("checkpoint-chunk", { term: "1", uploadId: p.manifest.uploadId, index, data: p.chunks[index] });
    expect((await call("checkpoint-head")).head).toBeNull();
    expect((await call("checkpoint-finalize", { term: "1", uploadId: p.manifest.uploadId })).head).toEqual(p.manifest);
    await evictDurableObject(stub);
    expect((await call("checkpoint-head")).head).toEqual(p.manifest);
    const reads = await Promise.all(p.chunks.map((_, index) => call("checkpoint-read", { uploadId: p.manifest.uploadId, index })));
    expect(reads.map(r => r.data)).toEqual(p.chunks);
    const restored = inflateRawSync(Buffer.concat(reads.map(r => Buffer.from(r.data, "base64url"))).subarray(40)).toString("utf8");
    expect(restored).toBe(schema2Fixture.json);
    expect(JSON.parse(restored).payload.thrownFlights).toEqual(p.root.payload.thrownFlights);
  });
  it("rejects escaped unpaired surrogates but preserves valid paired characters", () => {
    expect(() => snapshotCanonical(JSON.parse('"\\ud800"'))).toThrow("CHECKPOINT_UNICODE");
    expect(() => snapshotCanonical(JSON.parse('"\\udc00"'))).toThrow("CHECKPOINT_UNICODE");
    expect(snapshotCanonical("🧸\n\t\b\u0001")).toBe('"🧸\\u000a\\u0009\\u0008\\u0001"');
  });
  it("rejects oversized structure before JSON object allocation", () => {
    expect(() => checkpointPreflight("[".repeat(25) + "]".repeat(25))).toThrow("CHECKPOINT_DEPTH");
    expect(() => checkpointPreflight('"' + "x".repeat(24579) + '"')).toThrow("CHECKPOINT_STRING");
    expect(() => checkpointPreflight("[" + "0,".repeat(250000) + "0]")).toThrow("CHECKPOINT_TOKENS");
  });
});
