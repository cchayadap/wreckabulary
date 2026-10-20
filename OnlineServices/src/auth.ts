import { importJWK, SignJWT, jwtVerify, type JWTPayload, type JWK } from "jose";
import { Id, NOW, requireThat, type ServiceEnv } from "./model";

function accessKey(env: ServiceEnv) {
  requireThat(env.ACCESS_SECRET && env.ACCESS_SECRET.length >= 43, "SERVICE_NOT_CONFIGURED", 503);
  return new TextEncoder().encode(env.ACCESS_SECRET);
}
export async function accessToken(env: ServiceEnv, playerId: string, version: string) {
  return new SignJWT({ version }).setProtectedHeader({ alg: "HS256", typ: "JWT" })
    .setIssuer(env.TOKEN_ISSUER).setAudience("wordbound-api").setSubject(playerId)
    .setIssuedAt().setExpirationTime("15m").sign(accessKey(env));
}
export async function authenticate(request: Request, env: ServiceEnv) {
  const value = request.headers.get("authorization");
  requireThat(value && value.startsWith("Bearer "), "AUTH_REQUIRED", 401);
  try {
    const { payload } = await jwtVerify(value.slice(7), accessKey(env), {
      issuer: env.TOKEN_ISSUER, audience: "wordbound-api", algorithms: ["HS256"],
    });
    requireThat(typeof payload.sub === "string" && typeof payload.version === "string", "INVALID_TOKEN", 401);
    await env.IDENTITIES.getByName(payload.sub).check(payload.version);
    return payload.sub;
  } catch (e) {
    if (e instanceof Error && e.message.includes("SERVICE_NOT_CONFIGURED")) throw e;
    requireThat(false, "INVALID_TOKEN", 401);
  }
}
export async function signGrant(env: ServiceEnv, claims: JWTPayload, audience = "wordbound-admission") {
  requireThat(env.ADMISSION_PRIVATE_JWK, "SERVICE_NOT_CONFIGURED", 503);
  const key = await importJWK(JSON.parse(env.ADMISSION_PRIVATE_JWK), "RS256");
  return new SignJWT(claims).setProtectedHeader({ alg: "RS256", typ: "JWT", kid: env.SIGNING_KEY_ID })
    .setIssuer(env.TOKEN_ISSUER).setAudience(audience)
    .setIssuedAt().setExpirationTime("90s").sign(key);
}
export function photonConfiguration(env: ServiceEnv) {
  requireThat(Id.safeParse(env.PHOTON_APP_ID).success && env.PHOTON_PROVIDER_SECRET && env.PHOTON_PROVIDER_SECRET.length >= 43,
    "SERVICE_NOT_CONFIGURED", 503);
  return env.PHOTON_APP_ID!;
}
export async function signPhotonTicket(env: ServiceEnv, playerId: string, version: string, nonce: string, issuedAt: number) {
  const appId = photonConfiguration(env);
  return new SignJWT({ version, appId }).setProtectedHeader({ alg: "HS256", typ: "JWT" })
    .setIssuer(env.TOKEN_ISSUER).setAudience(`wordbound-photon:${appId}`).setSubject(playerId)
    .setJti(nonce).setIssuedAt(issuedAt).setExpirationTime(issuedAt + 60).sign(accessKey(env));
}
export async function verifyPhotonTicket(env: ServiceEnv, ticket: string) {
  const appId = photonConfiguration(env);
  try {
    const { payload } = await jwtVerify(ticket, accessKey(env), {
      issuer: env.TOKEN_ISSUER, audience: `wordbound-photon:${appId}`, algorithms: ["HS256"], currentDate: new Date(NOW()),
      requiredClaims: ["sub", "jti", "iat", "exp", "version", "appId"],
    });
    requireThat(Id.safeParse(payload.sub).success && Id.safeParse(payload.jti).success
      && payload.appId === appId && typeof payload.version === "string"
      && Number.isSafeInteger(payload.iat) && Number.isSafeInteger(payload.exp)
      && payload.exp! - payload.iat! === 60 && payload.iat! <= Math.floor(NOW() / 1000),
    "INVALID_PHOTON_TICKET", 401);
    return { playerId: payload.sub!, nonce: payload.jti!, version: payload.version, appId, expiresAt: payload.exp! };
  } catch { requireThat(false, "INVALID_PHOTON_TICKET", 401); }
}
export function publicKey(env: ServiceEnv) {
  requireThat(env.ADMISSION_PRIVATE_JWK, "SERVICE_NOT_CONFIGURED", 503);
  const { n, e, kty } = JSON.parse(env.ADMISSION_PRIVATE_JWK) as JWK;
  requireThat(kty === "RSA" && n && e, "SERVICE_NOT_CONFIGURED", 503);
  return { keys: [{ n, e, kty, kid: env.SIGNING_KEY_ID, use: "sig", alg: "RS256" }] };
}
