import { createHash, randomUUID } from "node:crypto";
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { parseArgs } from "node:util";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { importJWK, jwtVerify } from "jose";
import { readPublicCatalogue, canonical } from "./public-catalogue.mjs";

const { values } = parseArgs({ options: {
  live: { type: "boolean", default: false }, catalogue: { type: "string" }, evidence: { type: "string" },
} });
if (!values.live || !values.catalogue || !values.evidence)
  throw new Error("Explicit --live, --catalogue <native export>, and --evidence <new output> are required.");
if (existsSync(resolve(values.evidence))) throw new Error("Choose a new evidence output before any live request.");
const serviceRoot = fileURLToPath(new URL("../", import.meta.url));
const projected = readPublicCatalogue(resolve(values.catalogue));
const selected = projected.catalogue.courses.find(row => row.mode === "TeamRace" && row.format === "Duo");
if (!selected) throw new Error("The exported release has no TeamRace/Duo binding.");
const trusted = JSON.parse(readFileSync(resolve(serviceRoot, "production-client.json"), "utf8"));
const origin = "https://wreckabulary-wordbound-coordinator.pagna.workers.dev";
const fingerprint = "43ea939c715a181912177bff3e98c647d5690ec2b0b40085a8bb6c047ce41dce";
const key = trusted.jwks?.keys?.[0];
if (trusted.origin !== origin || trusted.issuer !== "wreckabulary-wordbound" ||
    trusted.publicKeyFingerprint !== fingerprint || trusted.jwks.keys.length !== 1 ||
    createHash("sha256").update(canonical({ e: key.e, kty: key.kty, n: key.n })).digest("hex") !== fingerprint)
  throw new Error("The independently pinned production public trust does not match.");
const settings = { mode: selected.mode, format: selected.format, fill: false, region: "asia",
  protocolVersion: projected.catalogue.protocolVersion, mapPoolVersion: `pub-${randomUUID().slice(0, 12)}` };
const evidence = {
  startedAt: new Date().toISOString(), origin, catalogueSha256: projected.sourceSha256,
  bindingsSha256: projected.bindingsSha256, approvedRelease: projected.catalogue.mapPoolVersion,
  isolatedPoolVersion: settings.mapPoolVersion, productionPopulationTested: false,
  checks: [], cleanup: false, success: false,
};
const groups = [], guests = [];
const deadline = AbortSignal.timeout(180000);
let stage = "public key", match, failure;
const assert = (condition, code) => { if (!condition) throw new Error(code); };
async function call(path, body, guest, { signal = deadline, status = 200 } = {}) {
  const response = await fetch(`${origin}/v1${path}`, {
    method: body === undefined ? "GET" : "POST", redirect: "error",
    signal: AbortSignal.any([signal, AbortSignal.timeout(15000)]),
    headers: { "content-type": "application/json", "idempotency-key": randomUUID(),
      ...(guest ? { authorization: `Bearer ${guest.accessToken}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  assert(response.status === status, `HTTP_${response.status}_EXPECTED_${status}`);
  return response.json();
}
function mark(value) { evidence.checks.push(value); console.log(`PASS ${value}`); }
function matchPath(id) {
  return `/pools/${encodeURIComponent([settings.region, settings.protocolVersion, settings.mapPoolVersion,
    settings.mode, settings.format].join(":"))}/matches/${id}`;
}
try {
  assert(canonical(await call("/keys")) === canonical(trusted.jwks), "PUBLIC_KEY_PARITY");
  const signingKey = await importJWK(key, "RS256");
  mark("live signing key matches pinned production trust");
  stage = "whole party setup";
  for (let team = 0; team < 2; team++) {
    const leader = await call("/guests", { displayName: "Public queue check" });
    guests.push(leader);
    let party = await call("/parties", { settings }, leader);
    const group = { partyId: party.id, members: [leader] }; groups.push(group);
    const member = await call("/guests", { displayName: "Public queue check" });
    guests.push(member);
    party = await call("/party-joins", { partyId: party.id, invite: party.invite }, member);
    group.members.push(member);
    for (const player of group.members)
      party = await call(`/parties/${party.id}/ready`, { expectedRevision: party.revision, ready: true }, player);
  }
  stage = "public queue";
  for (const group of groups) {
    const party = await call(`/parties/${group.partyId}`, undefined, group.members[0]);
    await call(`/parties/${party.id}/queue`, {
      expectedRevision: party.revision, channel: "QuickPlay", content: selected.content,
    }, group.members[0]);
  }
  match = (await call(`/parties/${groups[0].partyId}`, undefined, groups[0].members[0])).assignment;
  assert(match.state === "ADMITTING" && match.roster.length === 4 && !match.privateCode, "PUBLIC_ROSTER_NOT_LOCKED");
  assert(canonical(match.content) === canonical(selected.content) && canonical(match.settings) === canonical(settings), "CONTENT_OR_SETTINGS_CHANGED");
  assert(new Set(match.roster.map(seat => seat.teamId)).size === 2, "PUBLIC_TEAM_COUNT");
  for (const group of groups) {
    const seats = match.roster.filter(seat => seat.partyId === group.partyId);
    assert(canonical(seats.map(seat => seat.playerId).sort()) ===
      canonical(group.members.map(member => member.playerId).sort()) &&
      new Set(seats.map(seat => seat.teamId)).size === 1, "PUBLIC_PARTY_SPLIT_OR_REASSIGNED");
  }
  evidence.matchId = match.matchId;
  mark("Quick Play preserves two exact whole parties and exported binding in an isolated pool");
  stage = "signed public admission";
  const path = matchPath(match.matchId);
  for (const member of groups.flatMap(group => group.members)) {
    const admission = await call(`${path}/admission`, {}, member);
    const grant = (await jwtVerify(admission.grant, signingKey,
      { issuer: trusted.issuer, audience: "wordbound-admission", algorithms: ["RS256"] })).payload;
    const seat = match.roster.find(seat => seat.playerId === member.playerId);
    assert(grant.playerId === member.playerId && grant.matchId === match.matchId &&
      grant.sub === member.playerId && grant.partyId === seat.partyId && grant.partyRevision === seat.partyRevision &&
      grant.attemptId === seat.attemptId && grant.teamId === seat.teamId && grant.seat === seat.seat &&
      grant.reservationGeneration === match.reservationGeneration && grant.protocolVersion === settings.protocolVersion &&
      grant.rosterHash === match.rosterHash && grant.manifestHash === selected.content.manifestHash &&
      grant.courseHash === selected.content.courseHash && grant.modeHash === selected.content.modeHash &&
      grant.catalogHash === selected.content.catalogHash &&
      grant.connectionSecretHash === createHash("sha256").update(canonical(admission.connectionSecret)).digest("hex"),
      "ADMISSION_BINDING");
    await call(`${path}/ready`, { reservationGeneration: match.reservationGeneration,
      manifestHash: selected.content.manifestHash, rosterHash: match.rosterHash }, member);
  }
  const certificate = await call(`${path}/certificate`, {}, groups[0].members[0]);
  const started = (await jwtVerify(certificate.certificate, signingKey,
    { issuer: trusted.issuer, audience: "wordbound-match-start", algorithms: ["RS256"] })).payload;
  assert(started.matchId === match.matchId && started.reservationGeneration === match.reservationGeneration &&
    started.rosterHash === match.rosterHash && canonical(started.roster) === canonical(match.roster) &&
    canonical(started.content) === canonical(selected.content) && canonical(started.settings) === canonical(settings), "START_BINDING");
  mark("all real guests accept exact content and receive a verified public start certificate");
} catch (error) {
  failure = { stage, code: error instanceof Error && /^[A-Z0-9_]+$/.test(error.message) ? error.message : "REQUEST_OR_VERIFICATION_FAILED" };
} finally {
  const cleanupDeadline = AbortSignal.timeout(90000);
  const options = { signal: cleanupDeadline };
  const unresolved = [];
  let cleanupFailures = 0;
  const cleanupGroups = new Map();
  for (const guest of guests) {
    try {
      const profile = await call("/me", undefined, guest, options);
      if (profile.partyId) {
        const group = cleanupGroups.get(profile.partyId) ?? { partyId: profile.partyId, members: [] };
        group.members.push(guest); cleanupGroups.set(profile.partyId, group);
      }
    } catch {
      cleanupFailures++;
      unresolved.push(...groups.filter(group => group.members.includes(guest)).map(group => group.partyId));
    }
  }
  for (const group of cleanupGroups.values()) {
    try {
      let party = await call(`/parties/${group.partyId}`, undefined, group.members[0], options);
      assert(canonical(party.members.map(member => member.playerId).sort()) ===
        canonical(group.members.map(member => member.playerId).sort()), "CLEANUP_MEMBERSHIP_CHANGED");
      const leader = group.members.find(member => member.playerId === party.leaderId);
      assert(leader, "CLEANUP_LEADER_UNAVAILABLE");
      if (party.assignment?.state === "STARTED")
        for (const member of group.members) await call(`${matchPath(party.assignment.matchId)}/return`, {}, member, options);
      party = await call(`/parties/${group.partyId}`, undefined, leader, options);
      party = await call(`/parties/${group.partyId}/cancel`, { expectedRevision: party.revision }, leader, options);
      for (const member of [...group.members].reverse()) {
        party = await call(`/parties/${group.partyId}/leave`, { expectedRevision: party.revision }, member, options);
        assert(!(await call("/me", undefined, member, options)).partyId, "CLEANUP_MEMBERSHIP");
      }
    } catch { cleanupFailures++; unresolved.push(group.partyId); }
  }
  evidence.cleanup = cleanupFailures === 0;
  evidence.cleanupFailures = cleanupFailures;
  evidence.unresolvedPartyIds = [...new Set(unresolved)];
  evidence.failure = failure ?? (evidence.cleanup ? null : { stage: "cleanup", code: "CLEANUP_INCOMPLETE" });
  evidence.success = !evidence.failure;
  evidence.completedAt = new Date().toISOString();
  writeFileSync(resolve(values.evidence), JSON.stringify(evidence, null, 2), { flag: "wx" });
  console.log(JSON.stringify({ success: evidence.success, cleanup: evidence.cleanup, failure: evidence.failure }));
  if (!evidence.success) process.exitCode = 1;
}
