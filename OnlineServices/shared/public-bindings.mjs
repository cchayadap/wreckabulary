import { createHash } from "node:crypto";

export const PUBLIC_BINDING_BYTES = 4096;
export const PUBLIC_BINDING_SHARDS = 8;
const primary = "BUILTIN_CONTENT_JSON", prefix = primary + "_";
const contentFields = ["catalogHash", "courseHash", "courseId", "kind", "manifestHash", "modeHash"];
const hashFields = ["catalogHash", "courseHash", "manifestHash", "modeHash"];
const variants = new Set(["TimeRun/Solo", "TeamRace/Solo", "TeamRace/Duo", "TeamRace/Squad",
  "BrawlRace/Solo", "BrawlRace/Duo", "BrawlRace/Squad", "FreeForAll/Solo", "Freestyle/Solo"]);
const hash = value => createHash("sha256").update(value).digest("hex");
export function canonical(value) {
  if (value === null || typeof value !== "object") return JSON.stringify(value);
  if (Array.isArray(value)) return `[${value.map(canonical).join(",")}]`;
  return `{${Object.keys(value).sort().map(key => `${JSON.stringify(key)}:${canonical(value[key])}`).join(",")}}`;
}
function fail(code) { throw new Error("PUBLIC_ALLOWLIST_" + code); }
function fields(value, expected) {
  if (!value || typeof value !== "object" || Array.isArray(value) ||
      Object.keys(value).sort().join(",") !== expected.join(",")) fail("FIELDS");
}
function parse(value) {
  if (typeof value !== "string") fail("MISSING");
  if (Buffer.byteLength(value, "utf8") > PUBLIC_BINDING_BYTES) fail("SIZE");
  try { return JSON.parse(value); } catch { fail("JSON"); }
}
function validate(rows) {
  if (!Array.isArray(rows) || rows.length > 128) fail("COUNT");
  const seen = new Set();
  for (const row of rows) {
    fields(row, ["content", "format", "mode"]);
    if (typeof row.mode !== "string" || typeof row.format !== "string" || !variants.has(`${row.mode}/${row.format}`))
      fail("SETTINGS");
    const content = row.content;
    fields(content, contentFields);
    if (content.kind !== "BuiltIn" || typeof content.courseId !== "string" || !/^[A-Za-z0-9_-]{1,64}$/.test(content.courseId) ||
        hashFields.some(key => typeof content[key] !== "string" || !/^[a-f0-9]{64}$/.test(content[key]))) fail("BINDING");
    const identity = canonical(content);
    if (seen.has(identity)) fail("DUPLICATE");
    seen.add(identity);
  }
  return rows;
}
export function readPublicBindings(bindings) {
  const values = bindings ?? {};
  const value = parse(values[primary]);
  const shardKeys = Object.keys(values).filter(key => key.startsWith(prefix));
  if (Array.isArray(value)) {
    if (shardKeys.length) fail("SHARDS");
    if (values[primary] !== canonical(value)) fail("CANONICAL");
    return validate(value);
  }
  fields(value, ["count", "schema", "sha256", "shards"]);
  if (values[primary] !== canonical(value)) fail("MANIFEST");
  if (value.schema !== 2 || !Number.isInteger(value.shards) || value.shards < 1 || value.shards > PUBLIC_BINDING_SHARDS ||
      !Number.isInteger(value.count) || value.count < 1 || value.count > 128 ||
      typeof value.sha256 !== "string" || !/^[a-f0-9]{64}$/.test(value.sha256)) fail("MANIFEST");
  const expected = Array.from({ length: value.shards }, (_, i) => prefix + i);
  if (shardKeys.sort().join(",") !== expected.sort().join(",")) fail("SHARDS");
  const rows = [];
  for (let i = 0; i < value.shards; i++) {
    const source = values[prefix + i], part = parse(source);
    if (!Array.isArray(part) || !part.length || source !== canonical(part)) fail("SHARD_JSON");
    rows.push(...part);
  }
  validate(rows);
  if (rows.length !== value.count) fail("COUNT");
  if (hash(canonical(rows)) !== value.sha256) fail("HASH");
  return rows;
}
export function publicBindingVars(rows) {
  validate(rows);
  const source = canonical(rows);
  if (Buffer.byteLength(source, "utf8") <= PUBLIC_BINDING_BYTES) return { [primary]: source };
  const parts = []; let current = [];
  for (const row of rows) {
    if (Buffer.byteLength(canonical([...current, row]), "utf8") > PUBLIC_BINDING_BYTES) {
      if (!current.length) fail("SIZE");
      parts.push(canonical(current)); current = [];
    }
    current.push(row);
  }
  if (current.length) parts.push(canonical(current));
  if (parts.length > PUBLIC_BINDING_SHARDS) fail("SHARDS");
  const bindings = { [primary]: canonical({ schema: 2, count: rows.length, shards: parts.length, sha256: hash(source) }) };
  parts.forEach((part, i) => { bindings[prefix + i] = part; });
  readPublicBindings(bindings);
  return bindings;
}
export function replacePublicBindingVars(existing, rows) {
  return { ...Object.fromEntries(Object.entries(existing ?? {}).filter(([key]) => key !== primary && !key.startsWith(prefix))),
    ...publicBindingVars(rows) };
}
export function validateDeploymentVars(vars, reservedSecrets = 3) {
  if (!vars || typeof vars !== "object" || Array.isArray(vars) ||
      !Number.isInteger(reservedSecrets) || reservedSecrets < 0 || Object.keys(vars).length + reservedSecrets > 64)
    throw new Error("DEPLOYMENT_VARIABLE_COUNT");
  if (["ACCESS_SECRET", "ADMISSION_PRIVATE_JWK", "PHOTON_PROVIDER_SECRET"].some(name => name in vars))
    throw new Error("DEPLOYMENT_SECRET_IN_PLAIN_VARS");
  for (const value of Object.values(vars))
    if (typeof value !== "string" || Buffer.byteLength(value, "utf8") > PUBLIC_BINDING_BYTES)
      throw new Error("DEPLOYMENT_VARIABLE_SIZE_OR_TYPE");
  readPublicBindings(vars);
}
