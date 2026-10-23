import { createHash } from "node:crypto";
import { inflateRawSync } from "node:zlib";
import { z } from "zod";
import { Content, Hash, Mode, Revision, digest, requireThat, type PartySettings } from "./model";
import { checkpointPreflight, snapshotCanonical } from "./checkpoints";

export const BundleDescriptor = z.object({
  bundleId: Hash, content: Content.refine(c => c.kind === "Workshop"), mode: Mode,
  format: z.enum(["Solo", "Duo", "Squad"]), seed: z.string().regex(/^[a-f0-9]{16}$/),
  protocolVersion: z.literal("wordbound-v3"), geometryHash: Hash, itemHash: Hash, rulesHash: Hash,
  wireHash: Hash, rawLength: z.number().int().min(1).max(8388608),
  totalBytes: z.number().int().min(41).max(262144), chunkCount: z.number().int().min(1).max(64),
}).strict().refine(d => d.chunkCount === Math.ceil(d.totalBytes / 4096));
export type WorkshopDescriptor = z.infer<typeof BundleDescriptor>;
const RevisionBody = { expectedRevision: Revision };
export const BundleRequests = {
  "content-begin": z.object({ ...RevisionBody, descriptor: BundleDescriptor }).strict(),
  "content-chunk": z.object({ ...RevisionBody, bundleId: Hash, index: z.number().int().min(0).max(63),
    data: z.string().regex(/^[A-Za-z0-9_-]+$/).max(5462) }).strict(),
  "content-finalize": z.object({ ...RevisionBody, bundleId: Hash }).strict(),
  "content-info": z.object({}).strict(),
  "content-read": z.object({ bundleId: Hash, index: z.number().int().min(0).max(63) }).strict(),
};
export type BundleAction = keyof typeof BundleRequests;
export type BundleTransfer = { descriptor: WorkshopDescriptor; chunks: string[] };
type Stored = { descriptor: WorkshopDescriptor; revision: string; expiresAt: number; committed: boolean };
const hash = (v: string | Uint8Array) => createHash("sha256").update(v).digest("hex");
export function bundleId(d: WorkshopDescriptor | Omit<WorkshopDescriptor, "bundleId">) {
  const { bundleId: _id, ...unsigned } = d as WorkshopDescriptor; return digest(unsigned);
}
export function bindBundle(d: WorkshopDescriptor, settings: PartySettings) {
  requireThat(d.bundleId === bundleId(d), "CONTENT_DESCRIPTOR_HASH", 422);
  requireThat(d.mode === settings.mode && d.format === settings.format && d.protocolVersion === settings.protocolVersion,
    "CONTENT_SETTINGS_MISMATCH", 422);
}
export function validateBundle(transfer: BundleTransfer) {
  const d = BundleDescriptor.parse(transfer.descriptor);
  requireThat(d.bundleId === bundleId(d) && transfer.chunks.length === d.chunkCount, "CONTENT_DESCRIPTOR_HASH", 422);
  const chunks = transfer.chunks.map((encoded, i) => {
    const bytes = Buffer.from(encoded, "base64url");
    requireThat(bytes.toString("base64url") === encoded && bytes.length === Math.min(4096, d.totalBytes - i * 4096), "CONTENT_CHUNK", 422);
    return bytes;
  });
  const packet = Buffer.concat(chunks);
  requireThat(packet.length === d.totalBytes && hash(packet) === d.wireHash, "CONTENT_WIRE_HASH", 422);
  requireThat(packet.readUInt32LE(0) === 0x31534257 && packet.readInt32LE(4) === d.rawLength, "CONTENT_HEADER", 422);
  let raw: Buffer;
  try { raw = inflateRawSync(packet.subarray(40), { maxOutputLength: 8388608 }); }
  catch { requireThat(false, "CONTENT_INFLATE", 422); }
  requireThat(raw!.length === d.rawLength && hash(raw!) === packet.subarray(8, 40).toString("hex"), "CONTENT_RAW_HASH", 422);
  let text: string;
  try { text = new TextDecoder("utf-8", { fatal: true, ignoreBOM: true }).decode(raw!); }
  catch { requireThat(false, "CONTENT_JSON", 422); }
  checkpointPreflight(text!);
  let root: any;
  try { root = JSON.parse(text!); } catch { requireThat(false, "CONTENT_JSON", 422); }
  requireThat(snapshotCanonical(root) === text!, "CONTENT_CANONICAL_JSON", 422);
  requireThat(root && Object.keys(root).sort().join(",") ===
    "content,document,format,geometryHash,itemHash,manifest,mode,protocolVersion,rulesHash,schema,seed,teamFormat" &&
    root.format === "wreckabulary.workshop-bundle" && root.schema === 1 && root.protocolVersion === d.protocolVersion &&
    root.mode === d.mode && root.teamFormat === d.format && root.seed === d.seed && root.geometryHash === d.geometryHash &&
    root.itemHash === d.itemHash && root.rulesHash === d.rulesHash && digest(root.content) === digest(d.content), "CONTENT_BINDING", 422);
  requireThat(root.document && typeof root.document === "object" && !Array.isArray(root.document), "CONTENT_DOCUMENT", 422);
  const doc = snapshotCanonical(root.document), manifest = root.manifest;
  requireThat(Buffer.byteLength(doc) <= 2097152 && hash(doc) === d.content.courseHash && root.document.id === d.content.courseId,
    "CONTENT_DOCUMENT_HASH", 422);
  requireThat(manifest && manifest.sourceHash === d.content.courseHash && manifest.catalogueHash === d.geometryHash &&
    manifest.seed === d.seed && manifest.hash === d.content.manifestHash, "CONTENT_MANIFEST_BINDING", 422);
  requireThat(root.document.format === "wreckabulary.course" && manifest.format === "wreckabulary.resolved-course" &&
    hash(snapshotCanonical({ ...manifest.document, format: root.document.format })) === d.content.courseHash,
    "CONTENT_MANIFEST_DOCUMENT", 422);
  const { hash: _manifestHash, ...unsignedManifest } = manifest;
  requireThat(hash(snapshotCanonical(unsignedManifest)) === d.content.manifestHash,
    "CONTENT_MANIFEST_HASH", 422);
}

export class BundleStore {
  constructor(private sql: SqlStorage) {
    sql.exec("CREATE TABLE IF NOT EXISTS workshop_bundles(scope_id TEXT NOT NULL,bundle_id TEXT NOT NULL,value TEXT NOT NULL,PRIMARY KEY(scope_id,bundle_id))");
    sql.exec("CREATE TABLE IF NOT EXISTS workshop_chunks(scope_id TEXT NOT NULL,bundle_id TEXT NOT NULL,idx INTEGER NOT NULL,data TEXT NOT NULL,PRIMARY KEY(scope_id,bundle_id,idx))");
  }
  private get(scope: string, id: string): Stored | undefined {
    const row = this.sql.exec<{ value: string }>("SELECT value FROM workshop_bundles WHERE scope_id=? AND bundle_id=?", scope, id).toArray()[0];
    return row ? JSON.parse(row.value) : undefined;
  }
  private save(scope: string, u: Stored) {
    this.sql.exec("INSERT INTO workshop_bundles VALUES(?,?,?) ON CONFLICT(scope_id,bundle_id) DO UPDATE SET value=excluded.value", scope, u.descriptor.bundleId, JSON.stringify(u));
  }
  private erase(scope: string, id: string) {
    this.sql.exec("DELETE FROM workshop_chunks WHERE scope_id=? AND bundle_id=?", scope, id);
    this.sql.exec("DELETE FROM workshop_bundles WHERE scope_id=? AND bundle_id=?", scope, id);
  }
  deleteScope(scope: string) {
    this.sql.exec("DELETE FROM workshop_chunks WHERE scope_id=?", scope);
    this.sql.exec("DELETE FROM workshop_bundles WHERE scope_id=?", scope);
  }
  cleanup(now: number) {
    for (const row of this.sql.exec<{ scope_id: string; bundle_id: string; value: string }>("SELECT * FROM workshop_bundles")) {
      const u: Stored = JSON.parse(row.value); if (u.expiresAt !== 0 && now >= u.expiresAt) this.erase(row.scope_id, row.bundle_id);
    }
  }
  nextExpiry() {
    const times = this.sql.exec<{ value: string }>("SELECT value FROM workshop_bundles").toArray()
      .map(r => (JSON.parse(r.value) as Stored).expiresAt).filter(t => t > 0);
    return times.length ? Math.min(...times) : undefined;
  }
  info(scope: string, id: string) { return this.get(scope, id)?.committed ? this.get(scope, id)!.descriptor : null; }
  begin(scope: string, revision: string, d: WorkshopDescriptor, now: number, selectedId?: string) {
    const existing = this.get(scope, d.bundleId);
    if (existing) {
      requireThat(digest(existing.descriptor) === digest(d) && (existing.committed || existing.revision === revision), "CONTENT_UPLOAD_CONFLICT");
      if (existing.committed) { existing.revision = revision; existing.expiresAt = now + 86400000; this.save(scope, existing); }
      return { descriptor: d, committed: existing.committed };
    }
    const retained = this.sql.exec<{ bundle_id: string; value: string }>("SELECT bundle_id,value FROM workshop_bundles WHERE scope_id=?", scope).toArray();
    let count = retained.length;
    for (const old of retained) {
      if (count < 3) break;
      if (old.bundle_id !== selectedId && (JSON.parse(old.value) as Stored).committed) {
        this.erase(scope, old.bundle_id); count--;
      }
    }
    requireThat(count < 3, "CONTENT_UPLOAD_LIMIT", 429);
    this.save(scope, { descriptor: d, revision, expiresAt: now + 60000, committed: false });
    return { descriptor: d, committed: false };
  }
  private upload(scope: string, id: string, revision: string, now: number) {
    const u = this.get(scope, id);
    requireThat(u && u.revision === revision && (u.expiresAt === 0 || now < u.expiresAt), "CONTENT_UPLOAD_EXPIRED"); return u;
  }
  chunk(scope: string, revision: string, id: string, index: number, data: string, now: number) {
    const u = this.upload(scope, id, revision, now); const bytes = Buffer.from(data, "base64url");
    requireThat(index < u.descriptor.chunkCount && bytes.toString("base64url") === data &&
      bytes.length === Math.min(4096, u.descriptor.totalBytes - index * 4096), "CONTENT_CHUNK", 422);
    const prior = this.sql.exec<{ data: string }>("SELECT data FROM workshop_chunks WHERE scope_id=? AND bundle_id=? AND idx=?", scope, id, index).toArray()[0];
    requireThat(!prior || prior.data === data, "CONTENT_CHUNK_CONFLICT");
    if (!prior) this.sql.exec("INSERT INTO workshop_chunks VALUES(?,?,?,?)", scope, id, index, data);
    return { bundleId: id, index };
  }
  read(scope: string, id: string, index: number) {
    const d = this.info(scope, id); requireThat(d && index < d.chunkCount, "CONTENT_NOT_FOUND", 404);
    const row = this.sql.exec<{ data: string }>("SELECT data FROM workshop_chunks WHERE scope_id=? AND bundle_id=? AND idx=?", scope, id, index).toArray()[0];
    requireThat(row, "CONTENT_INCOMPLETE"); return { bundleId: id, index, data: row.data };
  }
  transfer(scope: string, id: string): BundleTransfer {
    const d = this.info(scope, id); requireThat(d, "CONTENT_NOT_FOUND", 404);
    return { descriptor: d, chunks: Array.from({ length: d.chunkCount }, (_, i) => this.read(scope, id, i).data) };
  }
  finalize(scope: string, revision: string, id: string, now: number) {
    const u = this.upload(scope, id, revision, now);
    if (!u.committed) {
      const rows = this.sql.exec<{ idx: number; data: string }>("SELECT idx,data FROM workshop_chunks WHERE scope_id=? AND bundle_id=? ORDER BY idx", scope, id).toArray();
      requireThat(rows.length === u.descriptor.chunkCount && rows.every((r, i) => r.idx === i), "CONTENT_INCOMPLETE");
      validateBundle({ descriptor: u.descriptor, chunks: rows.map(r => r.data) });
      requireThat(Date.now() < u.expiresAt, "CONTENT_UPLOAD_EXPIRED");
      u.committed = true; u.expiresAt = now + 86400000; this.save(scope, u);
    }
    return u.descriptor;
  }
  install(scope: string, transfer: BundleTransfer) {
    validateBundle(transfer);
    const d = transfer.descriptor, old = this.info(scope, d.bundleId);
    if (old) { requireThat(digest(old) === digest(d), "CONTENT_UPLOAD_CONFLICT"); return; }
    this.deleteScope(scope);
    this.save(scope, { descriptor: d, revision: "0", committed: true, expiresAt: 0 });
    transfer.chunks.forEach((data, i) => this.sql.exec("INSERT INTO workshop_chunks VALUES(?,?,?,?)", scope, d.bundleId, i, data));
  }
}
