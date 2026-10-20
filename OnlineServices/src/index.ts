import { z } from "zod";
import { authenticate, publicKey } from "./auth";
import { rpcValue } from "./rpc";
import { Hash, Id, QueueRequest, Revision, Settings, digest, requireThat, type ServiceEnv } from "./model";
export { Identity } from "./identity";
export { Party } from "./party";
export { MatchPool } from "./pool";

const Empty = z.object({}).strict();
const Key = z.string().regex(/^[a-zA-Z0-9_-]{8,96}$/);
const PoolKey = z.string().regex(/^[a-zA-Z0-9_-]{1,24}(:[a-zA-Z0-9_-]{1,24}){4}$/);
async function body<T extends z.ZodType>(request: Request, schema: T): Promise<z.infer<T>> {
  requireThat(request.headers.get("content-type")?.split(";")[0] === "application/json", "JSON_REQUIRED", 415);
  requireThat(!request.headers.has("content-length") || Number(request.headers.get("content-length")) <= 16_384, "BODY_TOO_LARGE", 413);
  const reader = request.body?.getReader(); const chunks: Uint8Array[] = []; let size = 0;
  if (reader) {
    while (true) {
      const part = await reader.read(); if (part.done) break;
      size += part.value.length;
      if (size > 16_384) { await reader.cancel(); requireThat(false, "BODY_TOO_LARGE", 413); }
      chunks.push(part.value);
    }
  }
  const bytes = new Uint8Array(size); let position = 0;
  for (const chunk of chunks) { bytes.set(chunk, position); position += chunk.length; }
  let parsed: unknown;
  try { parsed = JSON.parse(new TextDecoder("utf-8", { fatal: true, ignoreBOM: false }).decode(bytes)); }
  catch { requireThat(false, "INVALID_JSON", 400); }
  return schema.parse(parsed);
}
function operation(request: Request) { return Key.parse(request.headers.get("idempotency-key")); }
function deterministicPartyId(actor: string, key: string) {
  const h = digest({ actor, key, type: "party" });
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-4${h.slice(13, 16)}-a${h.slice(17, 20)}-${h.slice(20, 32)}`;
}
async function route(request: Request, env: ServiceEnv): Promise<unknown> {
  const path = new URL(request.url).pathname.split("/").filter(Boolean).map(decodeURIComponent);
  requireThat(path[0] === "v1", "NOT_FOUND", 404);
  const method = request.method;
  if (method === "GET" && path.join("/") === "v1/keys") return publicKey(env);
  if (method === "POST" && path.join("/") === "v1/guests") {
    const data = await body(request, z.object({ displayName: z.string().trim().min(1).max(32) }).strict());
    const playerId = crypto.randomUUID();
    return env.IDENTITIES.getByName(playerId).create(playerId, data.displayName);
  }
  if (method === "POST" && path.join("/") === "v1/auth/refresh") {
    const data = await body(request, z.object({ refreshToken: z.string().max(100) }).strict());
    const [id, value, extra] = data.refreshToken.split(".");
    requireThat(!extra && value?.length === 43, "INVALID_REFRESH", 401);
    return env.IDENTITIES.getByName(Id.parse(id)).refresh(value);
  }
  const actor = await authenticate(request, env);
  const identity = env.IDENTITIES.getByName(actor);
  if (method === "GET" && path.join("/") === "v1/me") return identity.profile();
  if (method === "POST" && path.join("/") === "v1/parties") {
    const data = await body(request, z.object({ settings: Settings }).strict());
    const op = operation(request);
    return identity.enter(op, "CREATE", deterministicPartyId(actor, op), data.settings);
  }
  if (method === "POST" && path.join("/") === "v1/party-joins") {
    const data = await body(request, z.object({ partyId: Id, invite: z.string().regex(/^[a-f0-9]{16}$/) }).strict());
    return identity.enter(operation(request), "JOIN", data.partyId, undefined, data.invite);
  }
  if (path[1] === "parties" && (path.length === 3 || path.length === 4)) {
    const id = Id.parse(path[2]); const party = env.PARTIES.getByName(id);
    if (method === "GET" && path.length === 3) return party.get(actor);
    const op = operation(request);
    if (method === "PATCH" && path.length === 3) {
      const data = await body(request, z.object({ expectedRevision: Revision, settings: Settings }).strict());
      return party.mutate(actor, op, { kind: "settings", ...data });
    }
    if (method === "POST" && path[3] === "ready") {
      const data = await body(request, z.object({ expectedRevision: Revision, ready: z.boolean() }).strict());
      return party.mutate(actor, op, { kind: "ready", ...data });
    }
    if (method === "POST" && path[3] === "queue") return party.enqueueParty(actor, op, await body(request, QueueRequest));
    if (method === "POST" && path[3] === "cancel") {
      const data = await body(request, z.object({ expectedRevision: Revision }).strict());
      return party.mutate(actor, op, { kind: "cancel", ...data });
    }
    if (method === "POST" && path[3] === "leave") {
      const data = await body(request, z.object({ expectedRevision: Revision }).strict());
      return identity.leave(id, op, data.expectedRevision);
    }
  }
  if (path[1] === "pools" && path[3] === "matches" && path.length === 6) {
    const pool = env.POOLS.getByName(PoolKey.parse(path[2])); const matchId = Id.parse(path[4]);
    requireThat(method === "POST", "METHOD_NOT_ALLOWED", 405);
    if (path[5] === "admission" || path[5] === "reconnect") {
      await body(request, Empty); return pool.admission(matchId, actor);
    }
    if (path[5] === "launch") { await body(request, Empty); return pool.launch(matchId, actor); }
    if (path[5] === "certificate") { await body(request, Empty); return pool.certificate(matchId, actor); }
    if (path[5] === "return") { await body(request, Empty); return pool.returnToLobby(matchId, actor); }
    if (path[5] === "ready") {
      const data = await body(request, z.object({ reservationGeneration: Revision, manifestHash: Hash, rosterHash: Hash }).strict());
      return pool.ready(matchId, actor, data.reservationGeneration, data.manifestHash, data.rosterHash);
    }
    if (path[5] === "presence") {
      const data = await body(request, z.object({ connected: z.boolean() }).strict());
      return pool.presence(matchId, actor, data.connected);
    }
  }
  requireThat(false, "NOT_FOUND", 404);
}
export default {
  async fetch(request: Request, env: ServiceEnv): Promise<Response> {
    try {
      return Response.json(await rpcValue(route(request, env)), { headers: { "cache-control": "no-store" } });
    } catch (e) {
      const match = e instanceof Error ? e.message.match(/WB:(\d{3}):([A-Z0-9_]+)/) : null;
      const status = match ? Number(match[1]) : e instanceof z.ZodError ? 400 : 500;
      const code = match?.[2] ?? (e instanceof z.ZodError ? "INVALID_REQUEST" : "INTERNAL_ERROR");
      if (status === 500) console.error(JSON.stringify({ event: "coordinator_failure", errorType: e instanceof Error ? e.name : "unknown" }));
      return Response.json({ error: { code } }, { status, headers: { "cache-control": "no-store" } });
    }
  },
} satisfies ExportedHandler<ServiceEnv>;
