import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import { resolve } from "node:path";
import { createHash, randomUUID } from "node:crypto";
import { deflateRawSync } from "node:zlib";
import { DatabaseSync } from "node:sqlite";

const sourceRoot = resolve(process.argv[2] ?? "../wordbound-online/OnlineServices");
const requireDependency = createRequire(resolve(sourceRoot, "package.json"));
const ts = requireDependency("typescript");
const modules = new Map();
const sourceHashes = {};
const sha = value => createHash("sha256").update(value).digest("hex");
function load(relative) {
  if (modules.has(relative)) return modules.get(relative);
  const source = readFileSync(resolve(sourceRoot, relative), "utf8");
  sourceHashes[relative] = sha(source);
  const compiled = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  }).outputText;
  const module = { exports: {} };
  new Function("require", "module", "exports", compiled)(
    name => name === "./model" ? load("src/model.ts") : requireDependency(name),
    module, module.exports);
  modules.set(relative, module.exports);
  return module.exports;
}
const model = load("src/model.ts");
const checkpoints = load("src/checkpoints.ts");
const { schema2Fixture: fixture } = load("test/fixtures/schema2.ts");
const envelope = JSON.parse(fixture.json);
const binding = envelope.payload.binding;
const now = Date.now();
let clock = now;
const originalClock = Date.now;
Date.now = () => clock;
const match = {
  id: binding.matchId, generation: "1", phase: "STARTED", rosterHash: binding.rosterHash,
  settings: { protocolVersion: binding.protocolVersion },
  content: { modeHash: binding.modeHash, courseHash: binding.courseHash,
    manifestHash: binding.manifestHash, catalogHash: binding.catalogueHash },
  seats: binding.seats.map(s => ({ playerId: s.playerId, teamId: s.team, seat: s.slot,
    reconnectDeadline: now + 120000 })),
};
const actors = match.seats.map(s => s.playerId);
const database = new DatabaseSync(":memory:");
const sql = { exec(query, ...parameters) {
  const rows = database.prepare(query).all(...parameters);
  return { toArray: () => rows, one: () => {
    assert.equal(rows.length, 1);
    return rows[0];
  } };
} };
const store = new checkpoints.CheckpointStore(sql);
const tests = [];
function test(name, action) {
  action();
  tests.push(name);
}
function call(action, data = {}, actor = actors[0]) {
  database.exec("BEGIN IMMEDIATE");
  try {
    store.cleanupExpired(clock);
    const result = store.execute(match, actor, action, data, clock);
    database.exec("COMMIT");
    return structuredClone(result);
  } catch (error) {
    database.exec("ROLLBACK");
    throw error;
  }
}
function identifier(manifest) {
  const candidate = model.digest(manifest);
  return checkpoints.CheckpointManifest.safeParse({ ...manifest, uploadId: candidate }).success
    ? candidate : randomUUID();
}
function packet(sequence, parent = null, term = "1", epoch = "0", mutate) {
  const root = structuredClone(envelope);
  root.payload.sequence = sequence;
  root.payload.epoch = epoch;
  root.payload.commands.epoch = epoch;
  mutate?.(root);
  delete root.hash;
  root.hash = sha(checkpoints.snapshotCanonical(root));
  const raw = Buffer.from(checkpoints.snapshotCanonical(root));
  const header = Buffer.alloc(40);
  header.writeUInt32LE(0x31534257);
  header.writeInt32LE(raw.length, 4);
  Buffer.from(sha(raw), "hex").copy(header, 8);
  const bytes = Buffer.concat([header, deflateRawSync(raw)]);
  const manifest = {
    term, previousHeadId: parent,
    stamp: { matchId: match.id, identityHash: fixture.identityHash, epoch, sequence,
      tick: root.payload.progress.tick, stateHash: root.hash },
    wireHash: sha(bytes), rawLength: raw.length, totalBytes: bytes.length,
    chunkCount: Math.ceil(bytes.length / 4096),
  };
  manifest.uploadId = identifier(manifest);
  return { manifest, bytes, chunks: Array.from({ length: manifest.chunkCount },
    (_, i) => bytes.subarray(i * 4096, (i + 1) * 4096).toString("base64url")) };
}
function stage(p, actor = actors[0]) {
  call("checkpoint-begin", p.manifest, actor);
  for (let index = 0; index < p.chunks.length; index++)
    call("checkpoint-chunk", { term: p.manifest.term, uploadId: p.manifest.uploadId,
      index, data: p.chunks[index] }, actor);
}
function rawPacket(raw, basis, declaredLength = raw.length) {
  const header = Buffer.alloc(40);
  header.writeUInt32LE(0x31534257);
  header.writeInt32LE(declaredLength, 4);
  Buffer.from(sha(raw), "hex").copy(header, 8);
  const bytes = Buffer.concat([header, deflateRawSync(raw)]);
  const manifest = { ...basis, rawLength: declaredLength, totalBytes: bytes.length,
    chunkCount: Math.ceil(bytes.length / 4096), wireHash: sha(bytes) };
  return { manifest, bytes };
}
const finish = (p, actor = actors[0]) =>
  call("checkpoint-finalize", { term: p.manifest.term, uploadId: p.manifest.uploadId }, actor);
const claim = () => ({ claimId: randomUUID(), reservationGeneration: "1", rosterHash: match.rosterHash });
try {
  test("actual C# fixture bytes use exact controls and supplementary Unicode", () => {
    assert.equal(checkpoints.snapshotCanonical(envelope), fixture.json);
    assert(fixture.json.includes("\\u000a") && fixture.json.includes("🧸"));
    const p = packet("1");
    const nativeBytes = Buffer.concat(fixture.chunks.map(c => Buffer.from(c, "base64url")));
    const nativeManifest = { ...p.manifest, wireHash: fixture.wireHash,
      rawLength: fixture.rawLength, totalBytes: fixture.totalBytes, chunkCount: fixture.chunks.length };
    checkpoints.validateCheckpointPacket(match, nativeManifest, nativeBytes);
  });
  test("all controls match C# spelling; unpaired surrogates reject", () => {
    const controls = Array.from({ length: 32 }, (_, i) => String.fromCharCode(i)).join("");
    const expected = '"' + Array.from({ length: 32 }, (_, i) => "\\u" + i.toString(16).padStart(4, "0")).join("") + '"';
    assert.equal(checkpoints.snapshotCanonical(controls), expected);
    assert.throws(() => checkpoints.snapshotCanonical("\ud800"), /CHECKPOINT_UNICODE/);
    assert.throws(() => checkpoints.snapshotCanonical("\udc00"), /CHECKPOINT_UNICODE/);
  });
  test("preflight bounds tokens, string length and depth before JSON allocation", () => {
    assert.throws(() => checkpoints.checkpointPreflight("[" + Array(250000).fill("0").join(",") + "]"), /CHECKPOINT_TOKENS/);
    assert.throws(() => checkpoints.checkpointPreflight('"' + "a".repeat(24578) + '"'), /CHECKPOINT_STRING/);
    assert.throws(() => checkpoints.checkpointPreflight("[".repeat(25) + "0" + "]".repeat(25)), /CHECKPOINT_DEPTH/);
  });
  test("correctly rehashed transport still rejects invalid UTF8 and inflation over 8MiB", () => {
    const basis = packet("1").manifest;
    const malformed = rawPacket(Buffer.from([0xc0, 0x80]), basis);
    assert.throws(() => checkpoints.validateCheckpointPacket(match, malformed.manifest, malformed.bytes), /CHECKPOINT_JSON/);
    const inflated = rawPacket(Buffer.alloc(8388609, 65), basis, 8388608);
    assert(inflated.bytes.length < 262144);
    assert.throws(() => checkpoints.validateCheckpointPacket(match, inflated.manifest, inflated.bytes), /CHECKPOINT_INFLATE/);
  });
  test("duplicate canonical keys cannot be hidden by recomputing packet hashes", () => {
    const duplicated = fixture.json.replace('"schema":2', '"schema":2,"schema":2');
    const p = rawPacket(Buffer.from(duplicated), packet("1").manifest);
    assert.throws(() => checkpoints.validateCheckpointPacket(match, p.manifest, p.bytes), /CHECKPOINT_CANONICAL_JSON/);
  });
  test("initial writer is signed roster first; membership is not exclusive ownership", () => {
    assert.throws(() => call("checkpoint-claim", claim(), actors[1]), /CHECKPOINT_INITIAL_HOST/);
    assert.throws(() => call("checkpoint-claim", claim(), randomUUID()), /NOT_MATCH_MEMBER/);
    call("checkpoint-claim", claim());
    assert.throws(() => call("checkpoint-claim", claim(), actors[1]), /CHECKPOINT_WRITER_BUSY/);
  });
  const first = packet("1");
  test("complete actual schema2 packet commits and replays identically", () => {
    stage(first);
    assert.equal(call("checkpoint-head").head, null);
    assert.deepEqual(finish(first).head, first.manifest);
    stage(first);
    assert.deepEqual(finish(first).head, first.manifest);
  });
  const second = packet("2", first.manifest.uploadId);
  const competing = packet("3", first.manifest.uploadId);
  test("competing complete uploads cannot both replace one head", () => {
    stage(second); stage(competing);
    finish(second);
    assert.throws(() => finish(competing), /CHECKPOINT_HEAD_CONFLICT/);
    assert.deepEqual(call("checkpoint-head").head, second.manifest);
  });
  const third = packet("3", second.manifest.uploadId);
  test("retention preserves current and prior complete bytes", () => {
    stage(third); finish(third);
    const heads = call("checkpoint-head");
    assert.deepEqual(heads.head, third.manifest);
    assert.deepEqual(heads.previous, second.manifest);
    assert.throws(() => call("checkpoint-read", { uploadId: first.manifest.uploadId, index: 0 }), /CHECKPOINT_NOT_COMMITTED/);
    assert.equal(call("checkpoint-read", { uploadId: second.manifest.uploadId, index: 0 }).data, second.chunks[0]);
  });
  test("retired upload handle cannot identify different future bytes", () => {
    const changed = packet("4", third.manifest.uploadId);
    changed.manifest.uploadId = first.manifest.uploadId;
    assert.throws(() => call("checkpoint-begin", changed.manifest), /CHECKPOINT|IDEMPOTENCY/);
  });
  test("exact expiry fences old writes without losing committed outcome", () => {
    clock = call("checkpoint-head").writer.expiresAt;
    assert.throws(() => call("checkpoint-renew", { term: "1" }), /CHECKPOINT_WRITER_EXPIRED/);
    assert.throws(() => finish(third), /CHECKPOINT_WRITER_EXPIRED/);
    assert.deepEqual(call("checkpoint-head").head, third.manifest);
    const replacement = call("checkpoint-claim", claim(), actors[1]);
    assert.equal(replacement.writer.term, "2");
    assert.equal(replacement.writer.epoch, "1");
    assert.deepEqual(replacement.head, third.manifest);
    assert.throws(() => finish(competing), /CHECKPOINT_WRITER_EXPIRED/);
  });
  test("terminal writer cannot renew and empty claims do not consume domain epochs", () => {
    match.seats[1].returned = true;
    assert.throws(() => call("checkpoint-renew", { term: "2" }, actors[1]), /RECONNECT_EXPIRED/);
    const replacement = call("checkpoint-claim", claim());
    assert.equal(replacement.writer.term, "3");
    assert.equal(replacement.writer.epoch, "1");
  });
  console.log(JSON.stringify({ status: "PASS", tests, sourceHashes,
    scope: "Actual TypeScript helpers and store with in-memory Node SQLite; no HTTP auth, Cloudflare persistence or Unity claim." }, null, 2));
} finally {
  Date.now = originalClock;
  database.close();
}
