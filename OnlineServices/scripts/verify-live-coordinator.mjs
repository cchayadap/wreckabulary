import { createHash, randomUUID } from "node:crypto";
import { readFileSync, writeFileSync } from "node:fs";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { importJWK, jwtVerify } from "jose";

const serviceRoot = fileURLToPath(new URL("../", import.meta.url));
const privateRoot = resolve(serviceRoot, "../.harness/online-secrets");
const origin = "https://wreckabulary-wordbound-coordinator.pagna.workers.dev";
const trusted = JSON.parse(readFileSync(resolve(privateRoot, "public-handoff.json"), "utf8"));
const secrets = JSON.parse(readFileSync(resolve(privateRoot, "production.json"), "utf8"));
const evidence = { origin, accountId: trusted.accountId, startedAt: new Date().toISOString(), checks: [], cleanup: false };
let stage = "public key";
function requireCheck(value, message) { if (!value) throw new Error(message); }
function passed(name) { evidence.checks.push(name); console.log(`PASS ${name}`); }
async function call(path, body, guest, expected = 200, method = body === undefined ? "GET" : "POST", key = randomUUID()) {
  const response = await fetch(origin + "/v1" + path, {
    method, redirect: "error", signal: AbortSignal.timeout(20000),
    headers: { "content-type": "application/json", "idempotency-key": key,
      ...(guest ? { authorization: `Bearer ${guest.accessToken}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  requireCheck(response.status === expected, `Unexpected HTTP status in ${stage}: ${response.status}`);
  return response.json();
}
async function provider(ticket, appId = trusted.photonAppId, extra = {}) {
  const url = new URL(origin + "/v1/photon/auth");
  url.searchParams.set("appId", appId); url.searchParams.set("providerKey", secrets.PHOTON_PROVIDER_SECRET);
  const response = await fetch(url, { method: "POST", redirect: "error", signal: AbortSignal.timeout(20000),
    headers: { "content-type": "application/json" }, body: JSON.stringify({ ticket, ...extra }) });
  requireCheck(response.status === 200, "Provider did not return its documented HTTP status.");
  return response.json();
}
const groups = [];
let match;
try {
  const jwks = await call("/keys");
  requireCheck(JSON.stringify(jwks) === JSON.stringify(trusted.jwks), "Live public key differs from independently prepared key.");
  const signingKey = await importJWK(jwks.keys[0], "RS256");
  const verify = async (jwt, audience) => (await jwtVerify(jwt, signingKey,
    { issuer: trusted.issuer, audience, algorithms: ["RS256"] })).payload;
  passed("live HTTPS public key matches independent RSA3072 key");
  stage = "guest validation";
  await call("/me", undefined, undefined, 401);
  await call("/guests", { displayName: "WB check", playerId: randomUUID() }, undefined, 400);
  const outsider = await call("/guests", { displayName: "WB deploy check" });
  const old = outsider.accessToken;
  Object.assign(outsider, await call("/auth/refresh", { refreshToken: outsider.refreshToken }));
  await call("/me", undefined, { accessToken: old }, 401);
  requireCheck((await call("/me", undefined, outsider)).playerId === outsider.playerId, "Refresh identity changed.");
  passed("server guest identity and rotating refresh fail closed");
  stage = "Photon provider";
  const photon = await call("/photon/tickets", {}, outsider);
  const good = await provider(photon.ticket);
  requireCheck(good.ResultCode === 1 && good.UserId === outsider.playerId, "Provider identity mismatch.");
  requireCheck((await provider(photon.ticket)).ResultCode === 2, "Provider accepted replay.");
  const wrongApp = await call("/photon/tickets", {}, outsider);
  requireCheck((await provider(wrongApp.ticket, randomUUID())).ResultCode === 2, "Provider accepted wrong app.");
  requireCheck((await provider(wrongApp.ticket, trusted.photonAppId, { userId: randomUUID() })).ResultCode === 3,
    "Provider accepted caller identity field.");
  passed("live Photon endpoint authenticates one-use ticket and rejects replay/wrong app/caller identity");
  stage = "private party setup";
  const settings = { mode: "TeamRace", format: "Squad", fill: false, region: "asia",
    protocolVersion: "check-" + randomUUID().slice(0, 8), mapPoolVersion: "deployment-check" };
  const hash = text => createHash("sha256").update(text).digest("hex");
  const content = { kind: "Workshop", courseId: "deployment-api-check",
    courseHash: hash("deployment API check course"), manifestHash: hash("deployment API check manifest"),
    modeHash: hash("deployment API check mode"), catalogHash: hash("deployment API check catalogue") };
  for (let team = 0; team < 2; team++) {
    const members = await Promise.all(Array.from({ length: 4 },
      () => call("/guests", { displayName: "WB deploy check" })));
    let party = await call("/parties", { settings }, members[0]);
    const group = { members, party }; groups.push(group);
    for (const member of members.slice(1))
      group.party = await call("/party-joins", { partyId: party.id, invite: party.invite }, member);
    for (const member of members)
      group.party = await call(`/parties/${party.id}/ready`, { expectedRevision: group.party.revision, ready: true }, member);
  }
  await call(`/parties/${groups[0].party.id}/queue`,
    { expectedRevision: groups[0].party.revision, channel: "QuickPlay", content: { ...content, kind: "BuiltIn" } }, groups[0].members[0], 422);
  await call(`/parties/${groups[0].party.id}/queue`,
    { expectedRevision: groups[0].party.revision, channel: "PrivateCreate", content }, groups[0].members[0]);
  const initial = (await call(`/parties/${groups[0].party.id}`, undefined, groups[0].members[0])).assignment;
  await call(`/parties/${groups[1].party.id}/queue`,
    { expectedRevision: groups[1].party.revision, channel: "PrivateJoin", code: initial.privateCode }, groups[1].members[0]);
  match = (await call(`/parties/${groups[0].party.id}`, undefined, groups[0].members[0])).assignment;
  requireCheck(match.state === "ADMITTING" && match.roster.length === 8, "Private whole-party roster did not lock.");
  for (const group of groups) {
    const seats = match.roster.filter(p => group.members.some(member => member.playerId === p.playerId));
    requireCheck(seats.length === 4 && new Set(seats.map(p => p.teamId)).size === 1, "A party was split.");
  }
  const pool = [settings.region, settings.protocolVersion, settings.mapPoolVersion, settings.mode, settings.format].join(":");
  const base = `/pools/${encodeURIComponent(pool)}/matches/${match.matchId}`;
  evidence.matchId = match.matchId;
  passed("two private parties of four reserve exact whole teams; unapproved Quick Play denied");
  stage = "match admission";
  await call(base + "/certificate", {}, groups[0].members[0], 409);
  await call(base + "/admission", {}, outsider, 403);
  const admissions = new Map();
  for (const member of groups.flatMap(group => group.members)) {
    const admission = await call(base + "/admission", {}, member);
    const grant = await verify(admission.grant, "wordbound-admission");
    requireCheck(grant.playerId === member.playerId && grant.matchId === match.matchId &&
      grant.rosterHash === match.rosterHash, "Admission binding mismatch.");
    admissions.set(member.playerId, admission);
    await call(base + "/ready", { reservationGeneration: match.reservationGeneration,
      manifestHash: content.manifestHash, rosterHash: match.rosterHash }, member);
  }
  const certificate = await call(base + "/certificate", {}, groups[0].members[0]);
  const started = await verify(certificate.certificate, "wordbound-match-start");
  requireCheck(started.roster.length === 8 && started.rosterHash === match.rosterHash, "Start roster mismatch.");
  passed("all eight exact seats accept content; signed grants and start certificate verify");
  stage = "connection authorization";
  const host = groups[0].members[0]; const peer = groups[1].members[0];
  const ticket = await call(base + "/connection-tickets", {}, peer);
  const request = { playerId: peer.playerId, ticket: ticket.ticket,
    connectionSecret: admissions.get(peer.playerId).connectionSecret, connectionId: randomUUID() };
  await call(base + "/connections", request, outsider, 403);
  const connection = await call(base + "/connections", request, host);
  const lease = await verify(connection.authorization, "wordbound-connection");
  requireCheck(lease.hostId === host.playerId && lease.playerId === peer.playerId &&
    lease.connectionId === request.connectionId && lease.connectionEpoch === "1", "Connection binding mismatch.");
  const retry = await call(base + "/connections", request, host);
  requireCheck(JSON.stringify(retry.claims) === JSON.stringify(connection.claims), "Exact connection retry changed lease.");
  await call(base + "/connections", { ...request, connectionId: randomUUID() }, host, 403);
  const renewal = { playerId: peer.playerId, connectionId: request.connectionId, connectionEpoch: "1" };
  const renewed = await call(base + "/connection-state", { renew: [renewal] }, host);
  requireCheck(renewed.seats.length === 8 && renewed.rosterHash === match.rosterHash, "Lease roster mismatch.");
  await call(base + "/return", {}, peer);
  const revoked = await call(base + "/connection-state", { renew: [renewal] }, host);
  const returned = revoked.seats.find(seat => seat.playerId === peer.playerId);
  requireCheck(returned.returned && returned.connectionEpoch === "2" && returned.leaseExpiresAt === 0, "Return did not revoke.");
  await call(base + "/reconnect", {}, peer, 409);
  passed("one-use connection lease, exact retry, outsider rejection and terminal Return revocation");
  stage = "cleanup";
  for (const member of groups.flatMap(group => group.members))
    if (member.playerId !== peer.playerId) await call(base + "/return", {}, member);
  for (const group of groups) {
    let party = await call(`/parties/${group.party.id}`, undefined, group.members[0]);
    party = await call(`/parties/${party.id}/cancel`, { expectedRevision: party.revision }, group.members[0]);
    for (const member of [...group.members].reverse())
      party = await call(`/parties/${party.id}/leave`, { expectedRevision: party.revision }, member);
  }
  evidence.cleanup = true;
  passed("all test match seats returned and both parties left");
  evidence.completedAt = new Date().toISOString();
  evidence.success = true;
} catch (error) {
  evidence.success = false; evidence.failedStage = stage;
  console.error(`FAIL ${stage}: ${error instanceof Error && !error.message.includes("http") ? error.message : "request failed"}`);
  process.exitCode = 1;
} finally {
  writeFileSync(resolve(privateRoot, "live-check-result.json"), JSON.stringify(evidence, null, 2));
  if (!evidence.cleanup) writeFileSync(resolve(privateRoot, "live-check-recovery.json"),
    JSON.stringify({ origin, groups, match }), { mode: 0o600 });
}
