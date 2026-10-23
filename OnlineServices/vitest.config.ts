import { cloudflareTest } from "@cloudflare/vitest-plugin";
import { defineConfig } from "vitest/config";
import { generateKeyPairSync, randomBytes } from "node:crypto";
import { publicBindingVars } from "./shared/public-bindings.mjs";
import { publicRows } from "./test/fixtures/public-content";

const { privateKey } = generateKeyPairSync("rsa", { modulusLength: 2048 });
export default defineConfig({
  plugins: [cloudflareTest({
    wrangler: { configPath: "./wrangler.jsonc" },
    miniflare: {
      bindings: {
        ACCESS_SECRET: randomBytes(48).toString("base64url"),
        PHOTON_APP_ID: "11111111-1111-4111-a111-111111111111",
        PHOTON_PROVIDER_SECRET: randomBytes(48).toString("base64url"),
        ADMISSION_PRIVATE_JWK: JSON.stringify(privateKey.export({ format: "jwk" })),
        ...publicBindingVars(publicRows),
      },
    },
  })],
  test: { include: ["test/**/*.test.ts"], testTimeout: 20_000, hookTimeout: 30_000 },
});
