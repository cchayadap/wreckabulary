import { inflateRawSync } from "node:zlib";
import { createHash } from "node:crypto";
import { env, exports } from "cloudflare:workers";
import { evictDurableObject, reset } from "cloudflare:test";
import { afterEach, expect, it } from "vitest";
import { poolKey, type PartySettings } from "../src/model";
import { snapshotCanonical } from "../src/checkpoints";
import { workshopFixture } from "./fixtures/workshop";

afterEach(async () => { await reset(); });
type Guest = { playerId: string; accessToken: string };
async function call(path: string, body: unknown, guest?: Guest) {
  const response = await exports.default.fetch(`https://local.test/v1${path}`, {
    method: body === undefined ? "GET" : "POST",
    headers: { "content-type": "application/json", "idempotency-key": crypto.randomUUID(),
      ...(guest ? { authorization: `Bearer ${guest.accessToken}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  expect(response.status).toBe(200);
  return await response.json() as any;
}

it("preserves a real C# authored document and exact resolved preview through authenticated HTTP and both SQLite evictions", async () => {
  const d = workshopFixture.descriptor;
  const guest = await call("/guests", { displayName: "Actual authored interop" }) as Guest;
  const settings: PartySettings = { mode: d.mode, format: d.format, fill: false, region: "asia",
    protocolVersion: d.protocolVersion, mapPoolVersion: "interop-" + crypto.randomUUID().slice(0, 8) };
  let party = await call("/parties", { settings }, guest);
  const base = `/parties/${party.id}`, revision = party.revision;
  await call(`${base}/content-begin`, { expectedRevision: revision, descriptor: d }, guest);
  for (let index = 0; index < workshopFixture.chunks.length; index++)
    await call(`${base}/content-chunk`, { expectedRevision: revision, bundleId: d.bundleId,
      index, data: workshopFixture.chunks[index] }, guest);
  party = await call(`${base}/content-finalize`, { expectedRevision: revision, bundleId: d.bundleId }, guest);
  await evictDurableObject(env.PARTIES.getByName(party.id));
  expect((await call(`${base}/content-info`, {}, guest)).descriptor).toEqual(d);
  await call(`${base}/ready`, { expectedRevision: party.revision, ready: true }, guest);
  await call(`${base}/queue`, { expectedRevision: party.revision, channel: "PrivateCreate", content: d.content }, guest);
  party = await call(base, undefined, guest);
  const match = `/pools/${encodeURIComponent(poolKey(settings))}/matches/${party.assignment.matchId}`;
  await evictDurableObject(env.POOLS.getByName(poolKey(settings)));
  expect((await call(`${match}/content-info`, {}, guest)).descriptor).toEqual(d);
  const downloaded: Buffer[] = [];
  for (let index = 0; index < d.chunkCount; index++) {
    const result = await call(`${match}/content-read`, { bundleId: d.bundleId, index }, guest);
    expect(result.data).toBe(workshopFixture.chunks[index]); downloaded.push(Buffer.from(result.data, "base64url"));
  }
  const packet = Buffer.concat(downloaded), raw = inflateRawSync(packet.subarray(40), { maxOutputLength: 8388608 });
  expect(packet.length).toBe(d.totalBytes); expect(raw.length).toBe(d.rawLength);
  const shared = JSON.parse(raw.toString("utf8"));
  const hash = (value: unknown) => createHash("sha256").update(snapshotCanonical(value)).digest("hex");
  expect(hash(shared.document)).toBe(workshopFixture.documentSha256);
  expect(hash(shared.manifest)).toBe(workshopFixture.manifestSha256);
  expect(shared.document.id).toBe("authored-interop-course");
  expect(shared.manifest.seed).toBe("fedcba9876543210");
  expect(shared.manifest.hash).toBe(d.content.manifestHash);
  expect(shared.document.physicalSites.length).toBeGreaterThan(0);
  await call(`${match}/launch`, {}, guest); party = await call(base, undefined, guest);
  await call(`${match}/ready`, { reservationGeneration: party.assignment.reservationGeneration,
    rosterHash: party.assignment.rosterHash, manifestHash: d.content.manifestHash }, guest);
  await call(`${match}/return`, {}, guest);
  party = await call(`${base}/cancel`, { expectedRevision: party.revision }, guest);
  await call(`${base}/leave`, { expectedRevision: party.revision }, guest);
  expect((await call("/me", undefined, guest)).partyId).toBeUndefined();
});
