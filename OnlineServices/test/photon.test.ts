import { env, exports } from "cloudflare:workers";
import { reset } from "cloudflare:test";
import { afterEach, describe, expect, it, vi } from "vitest";
import { SignJWT, decodeJwt } from "jose";
import { type ServiceEnv } from "../src/model";

const bindings = env as ServiceEnv;
afterEach(async () => { vi.restoreAllMocks(); await reset(); });
async function post(path: string, body: unknown, access?: string) {
  const r = await exports.default.fetch(`https://local.test/v1${path}`, {
    method: "POST", headers: { "content-type": "application/json", ...(access ? { authorization: `Bearer ${access}` } : {}) },
    body: JSON.stringify(body),
  });
  return { status: r.status, data: await r.json() as Record<string, any> };
}
async function credentials() {
  const guest = await post("/guests", { displayName: "Photon test" });
  const issued = await post("/photon/tickets", {}, guest.data.accessToken);
  expect(issued.status).toBe(200);
  return { guest: guest.data, issued: issued.data };
}
function provider(query = `appId=${bindings.PHOTON_APP_ID}&providerKey=${bindings.PHOTON_PROVIDER_SECRET}`) { return `/photon/auth?${query}`; }
describe("Photon custom authentication provider", () => {
  it("returns the authenticated coordinator identity and consumes the ticket exactly once", async () => {
    const { guest, issued } = await credentials();
    const responses = await Promise.all([post(provider(), { ticket: issued.ticket }), post(provider(), { ticket: issued.ticket })]);
    expect(responses.map(r => r.status)).toEqual([200, 200]);
    expect(responses.map(r => r.data.ResultCode).sort()).toEqual([1, 2]);
    expect(responses.find(r => r.data.ResultCode === 1)!.data).toEqual({ ResultCode: 1, UserId: guest.playerId });
    expect(responses.find(r => r.data.ResultCode === 2)!.data.UserId).toBeUndefined();
  });
  it("issues tickets only to authenticated guests and never accepts supplied identity", async () => {
    expect((await post("/photon/tickets", {})).status).toBe(401);
    const { guest, issued } = await credentials();
    expect((await post("/photon/tickets", { playerId: guest.playerId }, guest.accessToken)).status).toBe(400);
    expect((await post(provider(), { ticket: issued.ticket, UserId: crypto.randomUUID() })).data.ResultCode).toBe(3);
    expect((await post(provider(), { ticket: issued.ticket })).data.UserId).toBe(guest.playerId);
  });
  it("requires the dashboard provider secret and exact configured app", async () => {
    const { issued } = await credentials();
    expect((await post(provider(`appId=${crypto.randomUUID()}&providerKey=${bindings.PHOTON_PROVIDER_SECRET}`), { ticket: issued.ticket })).data.ResultCode).toBe(2);
    expect((await post(provider(`appId=${bindings.PHOTON_APP_ID}&providerKey=wrong`), { ticket: issued.ticket })).data.ResultCode).toBe(2);
    expect((await post(provider(`appId=${bindings.PHOTON_APP_ID}&appId=${bindings.PHOTON_APP_ID}&providerKey=${bindings.PHOTON_PROVIDER_SECRET}`), { ticket: issued.ticket })).data.ResultCode).toBe(3);
    expect((await post(provider(), { ticket: issued.ticket })).data.ResultCode).toBe(1);
  });
  it("rejects signature tampering, access-token substitution, wrong audience and wrong signed app", async () => {
    const { guest, issued } = await credentials();
    expect((await post(provider(), { ticket: `${issued.ticket}x` })).data.ResultCode).toBe(2);
    expect((await post(provider(), { ticket: guest.accessToken })).data.ResultCode).toBe(2);
    const claims = decodeJwt(issued.ticket);
    for (const change of [{ aud: "wordbound-api" }, { appId: crypto.randomUUID() }]) {
      const wrong = await new SignJWT({ ...claims, ...change }).setProtectedHeader({ alg: "HS256", typ: "JWT" })
        .sign(new TextEncoder().encode(bindings.ACCESS_SECRET));
      expect((await post(provider(), { ticket: wrong })).data.ResultCode).toBe(2);
    }
    expect((await post(provider(), { ticket: issued.ticket })).data.ResultCode).toBe(1);
  });
  it("rejects tickets at their exact expiry and after refresh revokes the identity version", async () => {
    const { guest, issued } = await credentials();
    vi.spyOn(Date, "now").mockReturnValue(issued.expiresAt);
    expect((await post(provider(), { ticket: issued.ticket })).data.ResultCode).toBe(2);
    vi.restoreAllMocks();
    const next = await post("/photon/tickets", {}, guest.accessToken);
    expect((await post("/auth/refresh", { refreshToken: guest.refreshToken })).status).toBe(200);
    expect((await post(provider(), { ticket: next.data.ticket })).data.ResultCode).toBe(2);
  });
  it("rejects malformed provider payloads without successful identity fields", async () => {
    for (const body of [{}, { ticket: 3 }, { ticket: "broken" }]) {
      const r = await post(provider(), body);
      expect(r.status).toBe(200); expect([2, 3]).toContain(r.data.ResultCode);
      expect(r.data.UserId).toBeUndefined();
    }
  });
});
