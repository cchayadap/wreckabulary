import { createHash } from "node:crypto";
import { inflateRawSync } from "node:zlib";
import { z } from "zod";
import { Hash, Id, Revision, digest, requireThat, type Match } from "./model";

const Counter = Revision.refine(v => BigInt(v) < 9223372036854775807n);
const Term = z.object({ term: Counter }).strict();
export const CheckpointStamp = z.object({ matchId: Id, identityHash: Hash, epoch: Counter, sequence: Counter.refine(v => v !== "0"), tick: Counter, stateHash: Hash }).strict();
export const CheckpointManifest = z.object({
  uploadId: Hash, term: Counter, previousHeadId: Hash.nullable(), stamp: CheckpointStamp,
  wireHash: Hash, rawLength: z.number().int().min(1).max(8388608),
  totalBytes: z.number().int().min(41).max(262144), chunkCount: z.number().int().min(1).max(64),
}).strict().refine(v => v.chunkCount === Math.ceil(v.totalBytes / 4096));
export const CheckpointRequests = {
  "checkpoint-claim": z.object({ claimId: Id, reservationGeneration: Revision, rosterHash: Hash }).strict(),
  "checkpoint-renew": Term,
  "checkpoint-open": z.object({ term: Counter, headId: Hash.nullable() }).strict(),
  "checkpoint-begin": CheckpointManifest,
  "checkpoint-chunk": z.object({ term: Counter, uploadId: Hash, index: z.number().int().min(0).max(63), data: z.string().regex(/^[A-Za-z0-9_-]+$/).max(5462) }).strict(),
  "checkpoint-finalize": z.object({ term: Counter, uploadId: Hash }).strict(),
  "checkpoint-head": z.object({}).strict(),
  "checkpoint-read": z.object({ uploadId: Hash, index: z.number().int().min(0).max(63) }).strict(),
};
export type CheckpointAction = keyof typeof CheckpointRequests;
export type Manifest = z.infer<typeof CheckpointManifest>;
export function checkpointUploadId(manifest: Omit<Manifest, "uploadId"> | Manifest): string {
  const { uploadId: _ignored, ...value } = manifest as Manifest; return digest(value);
}
type Writer = { playerId: string; claimId: string; term: string; epoch: string; expiresAt: number; baseHeadId: string | null; admissionOpen: boolean };
type State = { term: string; writer?: Writer; head?: Manifest; previous?: Manifest };
type Upload = { manifest: Manifest; expiresAt: number; committed: boolean };
const LEASE_MS = 15_000;
const UPLOAD_MS = 60_000;
const hashBytes = (bytes: Uint8Array | string) => createHash("sha256").update(bytes).digest("hex");
export function snapshotCanonical(value: any): string {
  if (typeof value === "string") {
    let result = "\"";
    for (let i = 0; i < value.length; i++) {
      const code = value.charCodeAt(i), c = value[i];
      if (code < 32) result += "\\u" + code.toString(16).padStart(4, "0");
      else if (c === "\"" || c === "\\") result += "\\" + c;
      else {
        if (code >= 0xd800 && code <= 0xdbff) {
          requireThat(i + 1 < value.length && value.charCodeAt(i + 1) >= 0xdc00 && value.charCodeAt(i + 1) <= 0xdfff, "CHECKPOINT_UNICODE");
          result += c + value[++i];
        } else { requireThat(code < 0xdc00 || code > 0xdfff, "CHECKPOINT_UNICODE"); result += c; }
      }
    }
    return result + "\"";
  }
  if (value === null || typeof value !== "object") return JSON.stringify(value);
  if (Array.isArray(value)) return "[" + value.map(snapshotCanonical).join(",") + "]";
  return "{" + Object.keys(value).sort().map(k => snapshotCanonical(k) + ":" + snapshotCanonical(value[k])).join(",") + "}";
}
export function checkpointPreflight(text: string) {
  let depth = 0, tokens = 0;
  for (let i = 0; i < text.length; i++) {
    let c = text[i];
    if (" \t\r\n:,".includes(c)) continue;
    requireThat(++tokens <= 250000, "CHECKPOINT_TOKENS");
    if (c === "{" || c === "[") { requireThat(++depth <= 24, "CHECKPOINT_DEPTH"); continue; }
    if (c === "}" || c === "]") { requireThat(--depth >= 0, "CHECKPOINT_JSON"); continue; }
    if (c === "\"") {
      const start = i; let closed = false;
      while (++i < text.length) {
        c = text[i]; requireThat(i - start <= 24578, "CHECKPOINT_STRING");
        if (c === "\"") { closed = true; break; }
        requireThat(c.charCodeAt(0) >= 32, "CHECKPOINT_JSON");
        if (c !== "\\") continue;
        requireThat(++i < text.length, "CHECKPOINT_JSON"); c = text[i];
        if (c === "u") {
          requireThat(i + 4 < text.length, "CHECKPOINT_JSON");
          for (let n = 0; n < 4; n++) requireThat(/^[a-fA-F0-9]$/.test(text[++i]), "CHECKPOINT_JSON");
        } else requireThat("\"\\/bfnrt".includes(c), "CHECKPOINT_JSON");
      }
      requireThat(closed, "CHECKPOINT_JSON"); continue;
    }
    const start = i;
    while (i < text.length && !" \t\r\n,:]}".includes(text[i])) i++;
    const token = text.slice(start, i--);
    requireThat(token === "true" || token === "false" || token === "null" ||
      (token.length <= 32 && /^-?(0|[1-9][0-9]*)$/.test(token)), "CHECKPOINT_JSON");
  }
  requireThat(depth === 0, "CHECKPOINT_JSON");
}
export function checkpointBinding(m: Match) {
  const seats = m.seats.map(s => ({ playerId: s.playerId, team: s.teamId, slot: s.seat })).sort((a, b) => a.playerId < b.playerId ? -1 : 1);
  const binding = { matchId: m.id, protocolVersion: m.settings.protocolVersion, modeHash: m.content.modeHash,
    courseHash: m.content.courseHash, manifestHash: m.content.manifestHash, rosterHash: m.rosterHash!, catalogueHash: m.content.catalogHash, seats };
  const identityHash = digest({ matchId: m.id, protocol: m.settings.protocolVersion, mode: m.content.modeHash,
    course: m.content.courseHash, manifest: m.content.manifestHash, roster: m.rosterHash!, catalogue: m.content.catalogHash,
    tickRate: 60, seats: seats.map(s => [s.playerId, s.team, s.slot]) });
  return { binding, identityHash };
}
export function validateCheckpointPacket(m: Match, manifest: Manifest, packet: Buffer) {
  requireThat(packet.length === manifest.totalBytes && hashBytes(packet) === manifest.wireHash, "CHECKPOINT_WIRE_HASH");
  requireThat(packet.readUInt32LE(0) === 0x31534257 && packet.readInt32LE(4) === manifest.rawLength, "CHECKPOINT_HEADER");
  let raw: Buffer;
  try { raw = inflateRawSync(packet.subarray(40), { maxOutputLength: 8388608 }); }
  catch { requireThat(false, "CHECKPOINT_INFLATE"); }
  requireThat(raw!.length === manifest.rawLength && hashBytes(raw!) === packet.subarray(8, 40).toString("hex"), "CHECKPOINT_RAW_HASH");
  let text: string;
  try { text = new TextDecoder("utf-8", { fatal: true, ignoreBOM: false }).decode(raw!); }
  catch { requireThat(false, "CHECKPOINT_JSON"); }
  checkpointPreflight(text!);
  let root: any;
  try { root = JSON.parse(text!); } catch { requireThat(false, "CHECKPOINT_JSON"); }
  const bounded = (value: any, depth = 0): void => {
    requireThat(depth <= 64, "CHECKPOINT_DEPTH");
    if (value && typeof value === "object") for (const child of Object.values(value)) bounded(child, depth + 1);
    if (typeof value === "number") requireThat(Number.isSafeInteger(value), "CHECKPOINT_NUMBER");
  };
  bounded(root);
  requireThat(snapshotCanonical(root) === raw!.toString("utf8"), "CHECKPOINT_CANONICAL_JSON");
  requireThat(root && Object.keys(root).sort().join(",") === "canonicalVersion,format,hash,payload,schema" &&
    root.format === "wreckabulary.match-snapshot" && root.schema === 2 && root.canonicalVersion === 2 && root.hash === manifest.stamp.stateHash,
    "CHECKPOINT_SCHEMA");
  const { hash, ...unsigned } = root;
  requireThat(hashBytes(snapshotCanonical(unsigned)) === hash, "CHECKPOINT_STATE_HASH");
  const { binding, identityHash } = checkpointBinding(m);
  const p = root.payload;
  requireThat(p && digest(p.binding) === digest(binding) && p.epoch === manifest.stamp.epoch && p.sequence === manifest.stamp.sequence &&
    p.progress?.tick === manifest.stamp.tick && p.commands?.identityHash === identityHash && p.commands?.epoch === p.epoch,
    "CHECKPOINT_BINDING");
}

export class CheckpointStore {
  constructor(private sql: SqlStorage) {
    sql.exec("CREATE TABLE IF NOT EXISTS checkpoint_state (match_id TEXT PRIMARY KEY, value TEXT NOT NULL)");
    sql.exec("CREATE TABLE IF NOT EXISTS checkpoint_claims (match_id TEXT NOT NULL, claim_id TEXT NOT NULL, term TEXT NOT NULL, PRIMARY KEY(match_id,claim_id))");
    sql.exec("CREATE TABLE IF NOT EXISTS checkpoint_uploads (match_id TEXT NOT NULL, upload_id TEXT NOT NULL, value TEXT NOT NULL, PRIMARY KEY(match_id,upload_id))");
    sql.exec("CREATE TABLE IF NOT EXISTS checkpoint_chunks (match_id TEXT NOT NULL, upload_id TEXT NOT NULL, idx INTEGER NOT NULL, data TEXT NOT NULL, PRIMARY KEY(match_id,upload_id,idx))");
  }
  private state(id: string): State {
    const row = this.sql.exec<{ value: string }>("SELECT value FROM checkpoint_state WHERE match_id=?", id).toArray()[0];
    return row ? JSON.parse(row.value) : { term: "0" };
  }
  private save(id: string, s: State) {
    this.sql.exec("INSERT INTO checkpoint_state VALUES(?,?) ON CONFLICT(match_id) DO UPDATE SET value=excluded.value", id, JSON.stringify(s));
  }
  private upload(id: string, uploadId: string): Upload {
    const row = this.sql.exec<{ value: string }>("SELECT value FROM checkpoint_uploads WHERE match_id=? AND upload_id=?", id, uploadId).toArray()[0];
    requireThat(row, "CHECKPOINT_UPLOAD_NOT_FOUND", 404); return JSON.parse(row.value);
  }
  private saveUpload(id: string, u: Upload) {
    this.sql.exec("INSERT INTO checkpoint_uploads VALUES(?,?,?) ON CONFLICT(match_id,upload_id) DO UPDATE SET value=excluded.value", id, u.manifest.uploadId, JSON.stringify(u));
  }
  private eraseUpload(id: string, uploadId: string) {
    this.sql.exec("DELETE FROM checkpoint_chunks WHERE match_id=? AND upload_id=?", id, uploadId);
    this.sql.exec("DELETE FROM checkpoint_uploads WHERE match_id=? AND upload_id=?", id, uploadId);
  }
  deleteMatch(id: string) {
    this.sql.exec("DELETE FROM checkpoint_chunks WHERE match_id=?", id);
    this.sql.exec("DELETE FROM checkpoint_uploads WHERE match_id=?", id);
    this.sql.exec("DELETE FROM checkpoint_claims WHERE match_id=?", id);
    this.sql.exec("DELETE FROM checkpoint_state WHERE match_id=?", id);
  }
  cleanupExpired(now: number) {
    const rows = this.sql.exec<{ match_id: string; upload_id: string }>(
      "SELECT match_id,upload_id FROM checkpoint_uploads WHERE json_extract(value,'$.committed')=0 AND json_extract(value,'$.expiresAt')<=?", now).toArray();
    for (const row of rows) this.eraseUpload(row.match_id, row.upload_id);
  }
  nextExpiry(): number | undefined {
    const row = this.sql.exec<{ expiry: number | null }>(
      "SELECT MIN(json_extract(value,'$.expiresAt')) AS expiry FROM checkpoint_uploads WHERE json_extract(value,'$.committed')=0").one();
    return row.expiry ?? undefined;
  }
  private clean(m: Match, s: State, now: number) {
    const uploads = this.sql.exec<{ upload_id: string; value: string }>("SELECT upload_id,value FROM checkpoint_uploads WHERE match_id=?", m.id).toArray();
    for (const row of uploads) {
      const u: Upload = JSON.parse(row.value);
      if (row.upload_id !== s.head?.uploadId && row.upload_id !== s.previous?.uploadId && (u.committed || u.expiresAt <= now)) this.eraseUpload(m.id, row.upload_id);
    }
  }
  private writer(s: State, actor: string, term: string, now: number): Writer {
    const w = s.writer;
    requireThat(w && w.playerId === actor && w.term === term && now < w.expiresAt, "CHECKPOINT_WRITER_EXPIRED"); return w;
  }
  execute(m: Match, actor: string, action: CheckpointAction, data: unknown, now: number) {
    requireThat(m.phase === "STARTED", "MATCH_NOT_STARTED");
    const seat = m.seats.find(s => s.playerId === actor);
    requireThat(seat, "NOT_MATCH_MEMBER", 403);
    requireThat(!seat.returned && seat.reconnectDeadline !== undefined && now < seat.reconnectDeadline, "RECONNECT_EXPIRED");
    requireThat(m.settings.protocolVersion === "wordbound-v2", "CHECKPOINT_PROTOCOL");
    const s = this.state(m.id); this.clean(m, s, now);
    const reply = () => ({ writer: s.writer ?? null, head: s.head ?? null, previous: s.previous ?? null });
    if (action === "checkpoint-claim") {
      const d = CheckpointRequests[action].parse(data);
      requireThat(d.reservationGeneration === m.generation && d.rosterHash === m.rosterHash, "CONTENT_OR_ROSTER_MISMATCH");
      const receipt = this.sql.exec<{ term: string }>("SELECT term FROM checkpoint_claims WHERE match_id=? AND claim_id=?", m.id, d.claimId).toArray()[0];
      if (receipt) { this.writer(s, actor, receipt.term, now); requireThat(s.writer!.claimId === d.claimId, "CHECKPOINT_CLAIM_RETIRED"); return reply(); }
      requireThat(!s.writer || now >= s.writer.expiresAt || m.seats.find(p => p.playerId === s.writer!.playerId)?.returned, "CHECKPOINT_WRITER_BUSY");
      requireThat(s.writer || actor === m.seats[0].playerId, "CHECKPOINT_INITIAL_HOST", 403);
      const count = this.sql.exec<{ n: number }>("SELECT COUNT(*) AS n FROM checkpoint_claims WHERE match_id=?", m.id).one().n;
      requireThat(count < 256, "CHECKPOINT_CLAIM_LIMIT", 429);
      s.term = (BigInt(s.term) + 1n).toString();
      s.writer = { playerId: actor, claimId: d.claimId, term: s.term, expiresAt: now + LEASE_MS, admissionOpen: false,
        epoch: s.head ? (BigInt(s.head.stamp.epoch) + 1n).toString() : "0", baseHeadId: s.head?.uploadId ?? null };
      this.sql.exec("INSERT INTO checkpoint_claims VALUES(?,?,?)", m.id, d.claimId, s.term); this.save(m.id, s); return reply();
    }
    if (action === "checkpoint-head") { CheckpointRequests[action].parse(data); return reply(); }
    if (action === "checkpoint-read") {
      const d = CheckpointRequests[action].parse(data);
      requireThat(d.uploadId === s.head?.uploadId || d.uploadId === s.previous?.uploadId, "CHECKPOINT_NOT_COMMITTED", 404);
      const u = this.upload(m.id, d.uploadId); requireThat(d.index < u.manifest.chunkCount, "CHECKPOINT_CHUNK_INDEX");
      const row = this.sql.exec<{ data: string }>("SELECT data FROM checkpoint_chunks WHERE match_id=? AND upload_id=? AND idx=?", m.id, d.uploadId, d.index).one();
      return { uploadId: d.uploadId, index: d.index, data: row.data };
    }
    if (action === "checkpoint-renew") {
      const d = CheckpointRequests[action].parse(data); const w = this.writer(s, actor, d.term, now);
      w.expiresAt = now + LEASE_MS; this.save(m.id, s); return reply();
    }
    if (action === "checkpoint-open") {
      const d = CheckpointRequests[action].parse(data); const w = this.writer(s, actor, d.term, now);
      requireThat(d.headId === w.baseHeadId, "CHECKPOINT_HEAD_CONFLICT");
      w.admissionOpen = true; this.save(m.id, s); return reply();
    }
    if (action === "checkpoint-begin") {
      const d = CheckpointManifest.parse(data); const w = this.writer(s, actor, d.term, now);
      requireThat(d.uploadId === checkpointUploadId(d), "CHECKPOINT_UPLOAD_ID");
      requireThat(d.stamp.matchId === m.id && d.stamp.identityHash === checkpointBinding(m).identityHash && d.stamp.epoch === w.epoch, "CHECKPOINT_BINDING");
      const old = this.sql.exec<{ value: string }>("SELECT value FROM checkpoint_uploads WHERE match_id=? AND upload_id=?", m.id, d.uploadId).toArray()[0];
      if (old) { requireThat(digest(JSON.parse(old.value).manifest) === digest(d), "IDEMPOTENCY_CONFLICT"); return { manifest: d }; }
      requireThat(d.previousHeadId === (s.head?.uploadId ?? null), "CHECKPOINT_HEAD_CONFLICT");
      requireThat(!s.head || BigInt(d.stamp.sequence) > BigInt(s.head.stamp.sequence), "CHECKPOINT_SEQUENCE");
      requireThat(!s.head || BigInt(d.stamp.tick) >= BigInt(s.head.stamp.tick), "CHECKPOINT_TICK");
      const rows = this.sql.exec<{ value: string }>("SELECT value FROM checkpoint_uploads WHERE match_id=?", m.id).toArray();
      requireThat(rows.filter(row => !(JSON.parse(row.value) as Upload).committed).length < 2, "CHECKPOINT_UPLOAD_LIMIT", 429);
      this.saveUpload(m.id, { manifest: d, expiresAt: now + UPLOAD_MS, committed: false }); return { manifest: d };
    }
    if (action === "checkpoint-chunk") {
      const d = CheckpointRequests[action].parse(data); this.writer(s, actor, d.term, now);
      const u = this.upload(m.id, d.uploadId); requireThat(u.manifest.term === d.term && (u.committed || now < u.expiresAt), "CHECKPOINT_UPLOAD_EXPIRED");
      const bytes = Buffer.from(d.data, "base64url");
      requireThat(d.index < u.manifest.chunkCount && bytes.length === Math.min(4096, u.manifest.totalBytes - d.index * 4096) && bytes.toString("base64url") === d.data, "CHECKPOINT_CHUNK_SIZE");
      const old = this.sql.exec<{ data: string }>("SELECT data FROM checkpoint_chunks WHERE match_id=? AND upload_id=? AND idx=?", m.id, d.uploadId, d.index).toArray()[0];
      requireThat(!old || old.data === d.data, "IDEMPOTENCY_CONFLICT");
      requireThat(!u.committed || old, "CHECKPOINT_INCOMPLETE");
      if (!old) this.sql.exec("INSERT INTO checkpoint_chunks VALUES(?,?,?,?)", m.id, d.uploadId, d.index, d.data);
      return { uploadId: d.uploadId, index: d.index };
    }
    const d = CheckpointRequests["checkpoint-finalize"].parse(data); this.writer(s, actor, d.term, now);
    const u = this.upload(m.id, d.uploadId); requireThat(u.manifest.term === d.term, "CHECKPOINT_WRITER_EXPIRED");
    if (u.committed) return { head: u.manifest };
    requireThat(now < u.expiresAt, "CHECKPOINT_UPLOAD_EXPIRED");
    requireThat(u.manifest.previousHeadId === (s.head?.uploadId ?? null), "CHECKPOINT_HEAD_CONFLICT");
    const chunks = this.sql.exec<{ idx: number; data: string }>("SELECT idx,data FROM checkpoint_chunks WHERE match_id=? AND upload_id=? ORDER BY idx", m.id, d.uploadId).toArray();
    requireThat(chunks.length === u.manifest.chunkCount && chunks.every((c, i) => c.idx === i), "CHECKPOINT_INCOMPLETE");
    validateCheckpointPacket(m, u.manifest, Buffer.concat(chunks.map(c => Buffer.from(c.data, "base64url"))));
    const finalizedAt = Date.now();
    this.writer(s, actor, d.term, finalizedAt);
    requireThat(finalizedAt < u.expiresAt && finalizedAt < seat.reconnectDeadline, "CHECKPOINT_UPLOAD_EXPIRED");
    u.committed = true; this.saveUpload(m.id, u); s.previous = s.head; s.head = u.manifest; this.save(m.id, s); this.clean(m, s, now);
    return { head: u.manifest };
  }
}
