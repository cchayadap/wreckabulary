import { readFileSync } from "node:fs";
import { test } from "node:test";
import assert from "node:assert/strict";
import { parsePublicCatalogue, canonical, requireConfigParity, activationProjection, configurePublicCatalogue } from "../../scripts/public-catalogue.mjs";
import { publicBindingVars } from "../../shared/public-bindings.mjs";

if (!process.env.WORDBOUND_PUBLIC_CATALOGUE) throw new Error("Actual native-exported WORDBOUND_PUBLIC_CATALOGUE is required.");
const source = readFileSync(process.env.WORDBOUND_PUBLIC_CATALOGUE, "utf8");
const projected = parsePublicCatalogue(source);
const clone = () => JSON.parse(source);

test("projects the exact native bytes and preserves approved release provenance", () => {
  assert.match(projected.catalogue.mapPoolVersion, /^[A-Za-z0-9_-]{1,24}$/);
  const seeds = new Set(projected.catalogue.courses.map(row => row.seed));
  assert.equal(projected.catalogue.courses.length, seeds.size * 9);
  for (const seed of seeds) assert.equal(new Set(projected.catalogue.courses
    .filter(row => row.seed === seed).map(row => `${row.mode}/${row.format}`)).size, 9);
  assert.equal(projected.builtinContentJson, canonical(projected.catalogue.courses.map(row => row.content)));
  const result = activationProjection(projected);
  assert.equal(result.catalogueSha256, projected.sourceSha256);
  assert.equal(result.bindingsSha256, projected.bindingsSha256);
  assert.equal(result.builtinContentJson, projected.builtinContentJson);
  requireConfigParity(projected, configurePublicCatalogue(projected, { vars: {} }));
});
test("rejects a missing, closed or changed deployment allowlist", () => {
  assert.throws(() => requireConfigParity(projected, { vars: {} }), /MISSING/);
  assert.throws(() => requireConfigParity(projected, { vars: { BUILTIN_CONTENT_JSON: "[]" } }), /PARITY/);
  const rows = JSON.parse(projected.builtinContentJson); rows[0].manifestHash = "0".repeat(64);
  assert.throws(() => requireConfigParity(projected, { vars: publicBindingVars(rows) }), /PARITY/);
});
test("rejects duplicate keys, noncanonical order and legacy closed arrays as activation artifacts", () => {
  assert.throws(() => parsePublicCatalogue(source.replace("{", '{"schema":1,')), /CANONICAL/);
  const data = clone(); data.courses.reverse();
  assert.throws(() => parsePublicCatalogue(canonical(data)), /CANONICAL/);
  assert.throws(() => parsePublicCatalogue("[]"), /FIELDS/);
  assert.throws(() => parsePublicCatalogue(source + "\n"), /CANONICAL/);
});
test("rejects metadata, private binding, duplicate binding and bounded-schema violations", () => {
  for (const mutate of [
    d => { d.schema = 2; }, d => { d.protocolVersion = "wordbound-v2"; },
    d => { d.courses[0].mode = "Freestyle"; }, d => { d.courses[0].seed = "0123456789ABCDEF"; },
    d => { d.courses[0].content.kind = "Workshop"; }, d => { d.courses[0].extra = true; },
    d => { d.courses.push({ ...d.courses[0], seed: "fedcba9876543210" }); },
    d => { d.courses = Array.from({ length: 129 }, () => d.courses[0]); },
  ]) {
    const data = clone(); mutate(data);
    assert.throws(() => parsePublicCatalogue(canonical(data)));
  }
  assert.throws(() => parsePublicCatalogue(" ".repeat(32769)), /SIZE/);
});
