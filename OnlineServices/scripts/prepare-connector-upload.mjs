import { createHash, generateKeyPairSync, randomBytes } from "node:crypto";
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { resolve } from "node:path";
import { gzipSync } from "node:zlib";
import { parseArgs } from "node:util";

const serviceRoot = fileURLToPath(new URL("../", import.meta.url));
const { values } = parseArgs({ options: {
  mode: { type: "string" }, "private-dir": { type: "string" },
} });
if (values.mode !== "initial" && values.mode !== "update")
  throw new Error("Select --mode initial or --mode update after inspecting the deployment target.");
const privateRoot = resolve(values["private-dir"] ?? resolve(serviceRoot, "../.harness/online-secrets"));
const config = JSON.parse(readFileSync(resolve(serviceRoot, "wrangler.production.jsonc"), "utf8"));
if (config.account_id !== "d105a82bc26b6913575355352c2d1bb1" ||
    config.name !== "wreckabulary-wordbound-coordinator" || config.routes || config.route)
  throw new Error("Deployment target does not match the approved isolated service.");
if (!existsSync(privateRoot)) throw new Error("Create and restrict the private directory before preparing secrets.");
const secretPath = resolve(privateRoot, "production.json");
if (!existsSync(secretPath)) {
  if (values.mode !== "initial") throw new Error("An update requires the existing signing-key record.");
  const { privateKey } = generateKeyPairSync("rsa", { modulusLength: 3072, publicExponent: 65537 });
  writeFileSync(secretPath, JSON.stringify({
    ACCESS_SECRET: randomBytes(32).toString("base64url"),
    PHOTON_PROVIDER_SECRET: randomBytes(32).toString("base64url"),
    ADMISSION_PRIVATE_JWK: JSON.stringify(privateKey.export({ format: "jwk" })),
  }), { flag: "wx", mode: 0o600 });
}
const secrets = JSON.parse(readFileSync(secretPath, "utf8"));
if (Object.keys(secrets).sort().join(",") !== "ACCESS_SECRET,ADMISSION_PRIVATE_JWK,PHOTON_PROVIDER_SECRET" ||
    !/^[A-Za-z0-9_-]{43}$/.test(secrets.ACCESS_SECRET) ||
    !/^[A-Za-z0-9_-]{43}$/.test(secrets.PHOTON_PROVIDER_SECRET))
  throw new Error("Invalid stored secret schema.");
const key = JSON.parse(secrets.ADMISSION_PRIVATE_JWK);
if (key.kty !== "RSA" || Buffer.from(key.n, "base64url").length < 256 || key.e !== "AQAB" || !key.d)
  throw new Error("Invalid stored signing key.");
const jwk = { n: key.n, e: key.e, kty: key.kty, kid: config.vars.SIGNING_KEY_ID, use: "sig", alg: "RS256" };
const publicKeyFingerprint = createHash("sha256").update(JSON.stringify({ e: jwk.e, kty: jwk.kty, n: jwk.n })).digest("hex");
const module = readFileSync(resolve(serviceRoot, ".wrangler/production-preview/index.js"), "utf8");
const metadata = {
  main_module: "index.js",
  compatibility_date: config.compatibility_date,
  compatibility_flags: config.compatibility_flags,
  observability: config.observability,
  logpush: false,
  bindings: [
    ...Object.entries(config.vars).map(([name, text]) => ({ type: "plain_text", name, text })),
    ...config.durable_objects.bindings.map(({ name, class_name }) => ({ type: "durable_object_namespace", name, class_name })),
    ...(values.mode === "initial"
      ? Object.entries(secrets).map(([name, text]) => ({ type: "secret_text", name, text }))
      : []),
  ],
  ...(values.mode === "initial"
    ? { migrations: { new_tag: "v1", new_sqlite_classes: ["Identity", "Party", "MatchPool"] } }
    : { keep_bindings: ["secret_text"] }),
};
const payload = { accountId: config.account_id, worker: config.name, metadata, module };
writeFileSync(resolve(privateRoot, "upload.json.gz"), gzipSync(JSON.stringify(payload)));
const handoff = {
  mode: values.mode, accountId: config.account_id, worker: config.name, issuer: config.vars.TOKEN_ISSUER,
  keyId: config.vars.SIGNING_KEY_ID, photonAppId: config.vars.PHOTON_APP_ID,
  publicKeyFingerprint, jwks: { keys: [jwk] },
  moduleSha256: createHash("sha256").update(module).digest("hex"),
};
writeFileSync(resolve(privateRoot, "public-handoff.json"), JSON.stringify(handoff, null, 2));
console.log(JSON.stringify({ prepared: true, mode: values.mode, accountId: config.account_id, worker: config.name,
  moduleBytes: Buffer.byteLength(module), moduleSha256: handoff.moduleSha256, publicKeyFingerprint }));
