import { existsSync, readFileSync, writeFileSync, renameSync } from "node:fs";
import { randomUUID } from "node:crypto";
import { resolve } from "node:path";
import { parseArgs } from "node:util";
import { readPublicCatalogue, requireConfigParity, configurePublicCatalogue, activationProjection, canonical } from "./public-catalogue.mjs";

const { values } = parseArgs({ options: {
  catalogue: { type: "string" }, output: { type: "string" }, "verify-config": { type: "string" },
  "write-config": { type: "string" },
} });
if (!values.catalogue || !values.output)
  throw new Error("Provide --catalogue <native export> and --output <derived activation artifact>.");
const projected = readPublicCatalogue(resolve(values.catalogue));
let configPath, previousConfig, nextConfig;
if (values["write-config"]) {
  configPath = resolve(values["write-config"]);
  if (configPath === resolve(values.catalogue) || configPath === resolve(values.output))
    throw new Error("Catalogue, output and config must be separate files.");
  previousConfig = readFileSync(configPath, "utf8");
  nextConfig = JSON.stringify(configurePublicCatalogue(projected, JSON.parse(previousConfig)), null, 2) + "\n";
}
if (values["verify-config"])
  requireConfigParity(projected, JSON.parse(readFileSync(resolve(values["verify-config"]), "utf8")));
const output = resolve(values.output);
const json = canonical(activationProjection(projected));
if (existsSync(output)) {
  if (readFileSync(output, "utf8") !== json) throw new Error("Existing activation artifact differs; choose a new output path.");
} else writeFileSync(output, json, { flag: "wx" });
if (configPath && previousConfig !== nextConfig) {
  if (readFileSync(configPath, "utf8") !== previousConfig) throw new Error("Config changed while preparing the catalogue. Retry.");
  const temporary = configPath + ".public-" + randomUUID();
  writeFileSync(temporary, nextConfig, { flag: "wx" });
  renameSync(temporary, configPath);
}
console.log(JSON.stringify({
  prepared: true, catalogueSha256: projected.sourceSha256, bindingsSha256: projected.bindingsSha256,
  courseCount: projected.catalogue.courses.length, release: projected.catalogue.mapPoolVersion,
  configParityChecked: !!values["verify-config"], deployed: false,
  configUpdated: !!configPath,
}));
