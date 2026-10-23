import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { resolve } from "node:path";
import { parseArgs } from "node:util";
import { readPublicCatalogue, requireConfigParity, activationProjection, canonical } from "./public-catalogue.mjs";

const { values } = parseArgs({ options: {
  catalogue: { type: "string" }, output: { type: "string" }, "verify-config": { type: "string" },
} });
if (!values.catalogue || !values.output)
  throw new Error("Provide --catalogue <native export> and --output <derived activation artifact>.");
const projected = readPublicCatalogue(resolve(values.catalogue));
if (values["verify-config"])
  requireConfigParity(projected, JSON.parse(readFileSync(resolve(values["verify-config"]), "utf8")));
const output = resolve(values.output);
const json = canonical(activationProjection(projected));
if (existsSync(output)) {
  if (readFileSync(output, "utf8") !== json) throw new Error("Existing activation artifact differs; choose a new output path.");
} else writeFileSync(output, json, { flag: "wx" });
console.log(JSON.stringify({
  prepared: true, catalogueSha256: projected.sourceSha256, bindingsSha256: projected.bindingsSha256,
  courseCount: projected.catalogue.courses.length, release: projected.catalogue.mapPoolVersion,
  configParityChecked: !!values["verify-config"], deployed: false,
}));
