import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";

const variants = new Set(["TimeRun/Solo", "TeamRace/Solo", "TeamRace/Duo", "TeamRace/Squad",
  "BrawlRace/Solo", "BrawlRace/Duo", "BrawlRace/Squad", "FreeForAll/Solo", "Freestyle/Solo"]);
const contentFields = ["kind", "courseId", "courseHash", "manifestHash", "modeHash", "catalogHash"];
const hashFields = ["courseHash", "manifestHash", "modeHash", "catalogHash"];
const sha256 = text => createHash("sha256").update(text).digest("hex");
export function canonical(value) {
  if (value === null || typeof value !== "object") return JSON.stringify(value);
  if (Array.isArray(value)) return `[${value.map(canonical).join(",")}]`;
  return `{${Object.keys(value).sort().map(key => `${JSON.stringify(key)}:${canonical(value[key])}`).join(",")}}`;
}
function fields(value, expected) {
  if (!value || typeof value !== "object" || Array.isArray(value) ||
      Object.keys(value).sort().join(",") !== [...expected].sort().join(","))
    throw new Error("PUBLIC_CATALOGUE_FIELDS");
}
function text(value, pattern) { return typeof value === "string" && pattern.test(value); }

export function parsePublicCatalogue(source) {
  if (typeof source !== "string" || Buffer.byteLength(source) > 32768)
    throw new Error("PUBLIC_CATALOGUE_SIZE");
  let catalogue;
  try { catalogue = JSON.parse(source); } catch { throw new Error("PUBLIC_CATALOGUE_JSON"); }
  fields(catalogue, ["schema", "protocolVersion", "mapPoolVersion", "courses"]);
  if (catalogue.schema !== 1 || catalogue.protocolVersion !== "wordbound-v3" ||
      !text(catalogue.mapPoolVersion, /^[A-Za-z0-9_-]{1,24}$/) ||
      !Array.isArray(catalogue.courses) || catalogue.courses.length < 1 || catalogue.courses.length > 128)
    throw new Error("PUBLIC_CATALOGUE_VERSION_OR_COUNT");
  const selections = new Set(), bindings = new Set(), modeHashes = new Map();
  for (const row of catalogue.courses) {
    fields(row, ["mode", "format", "seed", "content"]);
    const variant = `${row.mode}/${row.format}`;
    if (!variants.has(variant) || !text(row.seed, /^[a-f0-9]{16}$/))
      throw new Error("PUBLIC_CATALOGUE_METADATA");
    fields(row.content, contentFields);
    if (row.content.kind !== "BuiltIn" || !text(row.content.courseId, /^[A-Za-z0-9_-]{1,64}$/) ||
        hashFields.some(key => !text(row.content[key], /^[a-f0-9]{64}$/)))
      throw new Error("PUBLIC_CATALOGUE_BINDING");
    const key = `${variant}/${row.seed}`, binding = canonical(row.content);
    if (selections.has(key) || bindings.has(binding)) throw new Error("PUBLIC_CATALOGUE_DUPLICATE");
    if (modeHashes.has(variant) && modeHashes.get(variant) !== row.content.modeHash)
      throw new Error("PUBLIC_CATALOGUE_MODE_HASH");
    selections.add(key); bindings.add(binding); modeHashes.set(variant, row.content.modeHash);
  }
  const ordered = [...catalogue.courses].sort((a, b) =>
    ordinal(a.mode, b.mode) || ordinal(a.format, b.format) || ordinal(a.seed, b.seed));
  if (source !== canonical({ ...catalogue, courses: ordered }))
    throw new Error("PUBLIC_CATALOGUE_NOT_CANONICAL_EXPORT");
  const builtinContentJson = canonical(ordered.map(row => row.content));
  return {
    catalogue, sourceSha256: sha256(source), builtinContentJson,
    bindingsSha256: sha256(builtinContentJson),
  };
}
function ordinal(a, b) { return a < b ? -1 : a > b ? 1 : 0; }
export function readPublicCatalogue(path) { return parsePublicCatalogue(readFileSync(path, "utf8")); }
export function requireConfigParity(projected, config) {
  const configured = config?.vars?.BUILTIN_CONTENT_JSON;
  if (typeof configured !== "string") throw new Error("PUBLIC_ALLOWLIST_MISSING");
  let parsed;
  try { parsed = JSON.parse(configured); } catch { throw new Error("PUBLIC_ALLOWLIST_JSON"); }
  if (canonical(parsed) !== projected.builtinContentJson) throw new Error("PUBLIC_ALLOWLIST_PARITY");
}
export function activationProjection(projected) {
  return {
    schema: 1, catalogueSha256: projected.sourceSha256,
    protocolVersion: projected.catalogue.protocolVersion,
    mapPoolVersion: projected.catalogue.mapPoolVersion,
    courseCount: projected.catalogue.courses.length,
    bindingsSha256: projected.bindingsSha256,
    builtinContentJson: projected.builtinContentJson,
  };
}
