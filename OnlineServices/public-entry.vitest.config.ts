import { cloudflareTest } from "@cloudflare/vitest-plugin";
import { defineConfig } from "vitest/config";
import { generateKeyPairSync, randomBytes } from "node:crypto";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { parsePublicCatalogue } from "./scripts/public-catalogue.mjs";
import { publicBindingVars } from "./shared/public-bindings.mjs";

const path = process.env.WORDBOUND_PUBLIC_CATALOGUE;
if (!path) throw new Error("Provide WORDBOUND_PUBLIC_CATALOGUE pointing to the actual native exporter output.");
const source = readFileSync(resolve(path), "utf8");
const projected = parsePublicCatalogue(source);
const { privateKey } = generateKeyPairSync("rsa", { modulusLength: 2048 });
export default defineConfig({
  plugins: [cloudflareTest({
    wrangler: { configPath: "./wrangler.jsonc" },
    miniflare: { bindings: {
      ACCESS_SECRET: randomBytes(48).toString("base64url"),
      PHOTON_APP_ID: "11111111-1111-4111-a111-111111111111",
      PHOTON_PROVIDER_SECRET: randomBytes(48).toString("base64url"),
      ADMISSION_PRIVATE_JWK: JSON.stringify(privateKey.export({ format: "jwk" })),
      ...publicBindingVars(projected.catalogue.courses.map(row => row.content)),
      PUBLIC_CATALOGUE_JSON: source,
    } },
  })],
  test: { include: ["checks/public/*.case.ts"], testTimeout: 30_000, hookTimeout: 30_000 },
});
