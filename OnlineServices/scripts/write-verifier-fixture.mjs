import { generateKeyPairSync, randomBytes, createHash } from "node:crypto";
import { writeFile, mkdir } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { SignJWT } from "jose";

if (!process.argv[2]) throw new Error("Supply the ignored local fixture output path.");
const output = resolve(process.argv[2]);
const canonical = value => value === null || typeof value !== "object" ? JSON.stringify(value)
  : Array.isArray(value) ? `[${value.map(canonical).join(",")}]`
    : `{${Object.keys(value).sort().map(key => `${JSON.stringify(key)}:${canonical(value[key])}`).join(",")}}`;
const hash = value => createHash("sha256").update(canonical(value)).digest("hex");
const { privateKey, publicKey } = generateKeyPairSync("rsa", { modulusLength: 2048 });
const now = Math.floor(Date.now() / 1000);
const playerId = "11111111-1111-4111-a111-111111111111";
const matchId = "33333333-3333-4333-a333-333333333333";
const secret = randomBytes(32).toString("base64url");
const settings = { mode: "TimeRun", format: "Solo", fill: false, region: "asia", protocolVersion: "1", mapPoolVersion: "1" };
const content = { kind: "BuiltIn", courseId: "interop", courseHash: "a".repeat(64), manifestHash: "b".repeat(64), modeHash: "c".repeat(64), catalogHash: "d".repeat(64) };
const seat = { playerId, partyId: "44444444-4444-4444-a444-444444444444", partyRevision: "1",
  attemptId: "55555555-5555-4555-a555-555555555555", teamId: 0, seat: 0, connectionSecretHash: hash(secret) };
const roster = [seat]; const rosterHash = hash(roster);
const common = { version: 1, matchId, reservationGeneration: "1", rosterHash };
const sign = (claims, audience) => new SignJWT(claims).setProtectedHeader({ alg: "RS256", typ: "JWT", kid: "interop-key" })
  .setIssuer("wordbound-interop").setAudience(audience).setIssuedAt(now).setExpirationTime(now + 90).sign(privateKey);
const certificate = await sign({ ...common, startedAt: now * 1000, settings, content, roster }, "wordbound-match-start");
const grant = await sign({ ...common, ...seat, sub: playerId, protocolVersion: "1",
  courseHash: content.courseHash, manifestHash: content.manifestHash, modeHash: content.modeHash, catalogHash: content.catalogHash }, "wordbound-admission");
const jwks = { keys: [{ ...publicKey.export({ format: "jwk" }), alg: "RS256", use: "sig", kid: "interop-key" }] };
await mkdir(dirname(output), { recursive: true });
await writeFile(output, JSON.stringify({ now, settings, content, playerId, matchId, rosterHash, secret, certificate, grant, jwks }));
process.stdout.write("Generated ephemeral JOSE fixture; private key remains in process memory.\n");
