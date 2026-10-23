import assert from "node:assert/strict";
import { readFileSync, writeFileSync } from "node:fs";
import { readPublicBindings, canonical } from "../../../OnlineServices/shared/public-bindings.mjs";
import { readPublicCatalogue, requireConfigParity } from "../../../OnlineServices/scripts/public-catalogue.mjs";

const read = name => JSON.parse(readFileSync(new URL(name, import.meta.url), "utf8"));
const before = read("pre-deployment.json"), after = read("post-deployment.json");
const catalogue = readPublicCatalogue(new URL("../../../Assets/_Project/Resources/Wordbound/public-catalogue.json", import.meta.url));
assert.equal(after.success, true);
const vars = Object.fromEntries(after.settings.bindings.filter(b => b.type === "plain_text").map(b => [b.name, b.text]));
requireConfigParity(catalogue, { vars });
const rows = readPublicBindings(vars);
assert.equal(rows.length, 36);
assert.equal(canonical(rows), catalogue.publicBindingsJson);
assert.equal(after.module.sha256, "3329fc54ebe2373c1f50a776b6731f243127fdcb24a502376102af8ef0be55f5");
assert.equal(after.module.bytes, 918845);
const names = bindings => bindings.map(x => x.name).sort();
assert.deepEqual(names(after.secrets), names(before.settings.secrets));
const namespaces = settings => settings.bindings.filter(b => b.type === "durable_object_namespace")
  .map(b => ({ name: b.name, class_name: b.class_name, namespace_id: b.namespace_id })).sort((a, b) => a.name.localeCompare(b.name));
assert.deepEqual(namespaces(after.settings), namespaces(before.settings.settings));
assert.equal(after.settings.compatibility_date, before.settings.settings.compatibility_date);
assert.deepEqual(after.settings.compatibility_flags, before.settings.settings.compatibility_flags);
assert.equal(after.settings.logpush, false);
assert.deepEqual(after.subdomain, before.settings.subdomain);
assert.deepEqual(after.website, before.targets.website);
for (const name of ["TOKEN_ISSUER", "SIGNING_KEY_ID", "PHOTON_APP_ID"])
  assert.equal(vars[name], before.settings.settings.bindings.find(b => b.name === name).text);
assert.deepEqual(after.deployment.versions, [{ version_id: "05384420-7e14-40c2-b965-0e8a91681df7", percentage: 100 }]);
const result = { success: true, recordedUtc: new Date().toISOString(), catalogueSha256: catalogue.sourceSha256,
  bindingsSha256: catalogue.bindingsSha256, moduleSha256: after.module.sha256, courseCount: rows.length,
  variants: [...new Set(rows.map(row => row.mode + "/" + row.format))].sort(),
  deploymentId: after.deployment.id, versionId: after.deployment.versions[0].version_id,
  secretNamesRetained: true, namespacesRetained: true, websiteUnchanged: true,
  observabilityReadback: "Settings endpoint omits observability; staged upload explicitly disables logs and traces and redacts query strings.",
  liveHttpAcceptance: "NOT RUN", photonAcceptance: "NOT RUN" };
writeFileSync(new URL("readback-verification.json", import.meta.url), JSON.stringify(result, null, 2) + "\n", { flag: "wx" });
console.log(JSON.stringify(result));
