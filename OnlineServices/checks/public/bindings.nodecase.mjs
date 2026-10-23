import { test } from "node:test";
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdtempSync, writeFileSync, readFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { canonical, readPublicBindings, publicBindingVars, replacePublicBindingVars, validateDeploymentVars } from "../../shared/public-bindings.mjs";
import { parsePublicCatalogue, configurePublicCatalogue, requireConfigParity } from "../../scripts/public-catalogue.mjs";

const digest = text => createHash("sha256").update(text).digest("hex");
const variants = ["BrawlRace/Duo", "BrawlRace/Solo", "BrawlRace/Squad", "FreeForAll/Solo", "Freestyle/Solo",
  "TeamRace/Duo", "TeamRace/Solo", "TeamRace/Squad", "TimeRun/Solo"];
const courses = variants.flatMap(variant => Array.from({ length: 4 }, (_, i) => {
  const [mode, format] = variant.split("/"), seed = i.toString(16).padStart(16, "0");
  return { mode, format, seed, content: { kind: "BuiltIn", courseId: "unit-fixture",
    courseHash: digest("source"), manifestHash: digest(variant + seed), modeHash: digest(variant), catalogHash: digest("catalogue") } };
}));
const source = canonical({ schema: 1, protocolVersion: "wordbound-v3", mapPoolVersion: "unit-fixture", courses });
const rows = courses.map(row => row.content), vars = publicBindingVars(rows);
const copy = () => ({ ...vars });

test("36 synthetic binding units roundtrip losslessly in bounded exact shards", () => {
  assert.equal(rows.length, 36);
  assert.ok(Buffer.byteLength(canonical(rows)) > 5000);
  assert.ok(Object.keys(vars).length > 2);
  assert.ok(Object.values(vars).every(value => Buffer.byteLength(value) <= 4096));
  assert.deepEqual(readPublicBindings(vars), rows);
  validateDeploymentVars(vars);
});
test("explicit closed lists and small legacy lists retain strict identities", () => {
  assert.deepEqual(readPublicBindings({ BUILTIN_CONTENT_JSON: "[]" }), []);
  assert.deepEqual(readPublicBindings(publicBindingVars(rows.slice(0, 1))), rows.slice(0, 1));
  assert.throws(() => readPublicBindings({}), /MISSING/);
  assert.throws(() => readPublicBindings({ BUILTIN_CONTENT_JSON: "[]", BUILTIN_CONTENT_JSON_0: "[]" }), /SHARDS/);
  assert.throws(() => publicBindingVars([rows[0], rows[0]]), /DUPLICATE/);
  assert.throws(() => publicBindingVars([{ ...rows[0], kind: "Workshop" }]), /BINDING/);
  assert.throws(() => publicBindingVars([{ ...rows[0], extra: true }]), /FIELDS/);
  assert.throws(() => publicBindingVars([{ ...rows[0], manifestHash: "oops" }]), /BINDING/);
});
test("missing, excess, mixed, tampered and noncanonical shards fail closed", () => {
  for (const mutate of [
    v => { delete v.BUILTIN_CONTENT_JSON_0; },
    v => { v.BUILTIN_CONTENT_JSON_8 = "[]"; },
    v => { v.BUILTIN_CONTENT_JSON_00 = v.BUILTIN_CONTENT_JSON_0; },
    v => { v.BUILTIN_CONTENT_JSON_bad = "[]"; },
    v => { v.BUILTIN_CONTENT_JSON_0 = "[]"; },
    v => { v.BUILTIN_CONTENT_JSON_0 += "\n"; },
    v => { v.BUILTIN_CONTENT_JSON_0 = " ".repeat(4097); },
    v => { const part = JSON.parse(v.BUILTIN_CONTENT_JSON_0); part[0].courseId = "changed"; v.BUILTIN_CONTENT_JSON_0 = canonical(part); },
    v => { const manifest = JSON.parse(v.BUILTIN_CONTENT_JSON); manifest.count--; v.BUILTIN_CONTENT_JSON = canonical(manifest); },
    v => { v.BUILTIN_CONTENT_JSON = "[]"; },
    v => { v.BUILTIN_CONTENT_JSON += "\n"; },
  ]) { const value = copy(); mutate(value); assert.throws(() => readPublicBindings(value)); }
});
test("config replacement removes stale shards and preserves all unrelated settings", () => {
  const original = { account_id: "unchanged", keep_vars: true,
    vars: { TOKEN_ISSUER: "unchanged", ...vars, BUILTIN_CONTENT_JSON_7: "stale" },
    durable_objects: { bindings: [{ name: "POOLS", class_name: "MatchPool" }] } };
  const projected = parsePublicCatalogue(source), updated = configurePublicCatalogue(projected, original);
  assert.equal(original.vars.BUILTIN_CONTENT_JSON_7, "stale");
  assert.equal(updated.vars.BUILTIN_CONTENT_JSON_7, undefined);
  assert.equal(updated.vars.TOKEN_ISSUER, original.vars.TOKEN_ISSUER);
  assert.deepEqual(updated.durable_objects, original.durable_objects);
  assert.equal(updated.account_id, original.account_id);
  requireConfigParity(projected, updated);
  assert.deepEqual(replacePublicBindingVars(updated.vars, []), { TOKEN_ISSUER: "unchanged", BUILTIN_CONTENT_JSON: "[]" });
});
test("deployment preflight checks variable bounds and prevents secret replacement", () => {
  assert.throws(() => validateDeploymentVars({ ...vars, bad: "é".repeat(2049) }), /SIZE/);
  assert.throws(() => validateDeploymentVars({ ...vars, ACCESS_SECRET: "never plaintext" }), /SECRET/);
  assert.throws(() => validateDeploymentVars({ ...vars, ...Object.fromEntries(Array.from({ length: 64 }, (_, i) => ["X" + i, "ok"])) }), /COUNT/);
  assert.throws(() => readPublicBindings({ BUILTIN_CONTENT_JSON: canonical(rows) }), /SIZE/);
});
test("CLI writes only catalogue vars and repeats idempotently without touching secret files", () => {
  const folder = mkdtempSync(join(tmpdir(), "wordbound-binding-unit-"));
  const catalogue = join(folder, "catalogue.json"), config = join(folder, "config.json"), output = join(folder, "activation.json");
  const privateFile = join(folder, "private.json"), secret = "synthetic-private-file-must-stay-unchanged";
  writeFileSync(catalogue, source); writeFileSync(privateFile, secret);
  writeFileSync(config, JSON.stringify({ name: "unit-worker", vars: { TOKEN_ISSUER: "unit", BUILTIN_CONTENT_JSON: "[]" },
    durable_objects: { bindings: [{ name: "POOLS", class_name: "MatchPool" }] } }));
  const cli = new URL("../../scripts/project-public-catalogue.mjs", import.meta.url);
  const args = [fileURLToPath(cli), "--catalogue", catalogue, "--output", output, "--write-config", config];
  execFileSync(process.execPath, args);
  const first = readFileSync(config, "utf8");
  execFileSync(process.execPath, args);
  assert.equal(readFileSync(config, "utf8"), first);
  assert.equal(readFileSync(privateFile, "utf8"), secret);
  const activation = JSON.parse(readFileSync(output, "utf8"));
  assert.equal(activation.schema, 2); assert.equal(activation.courseCount, 36);
  assert.deepEqual(activation.builtinContentVars, vars);
  requireConfigParity(parsePublicCatalogue(source), JSON.parse(first));
});
