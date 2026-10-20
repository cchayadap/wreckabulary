import { Store } from "./store";
import { accessToken, photonConfiguration, signPhotonTicket } from "./auth";
import { rpcValue } from "./rpc";
import { digest, equalSecret, requireThat, secret, NOW, type PartySettings } from "./model";

type IdentityState = {
  playerId: string; displayName: string; version: string;
  refreshHash: string; refreshExpires: number;
  membership?: { partyId: string; operationId: string; requestHash: string; status: "PENDING" | "ACTIVE"; kind: "CREATE" | "JOIN" };
  leave?: { operationId: string; requestHash: string; partyId: string; complete: boolean; result?: unknown };
  photonTickets?: Record<string, { version: string; appId: string; expiresAt: number }>;
};
export class Identity extends Store<IdentityState> {
  async create(playerId: string, displayName: string) {
    const refresh = secret();
    this.transaction(() => {
      requireThat(!this.read(), "IDENTITY_EXISTS");
      this.write({ playerId, displayName, version: "1", refreshHash: digest(refresh), refreshExpires: NOW() + 30 * 86400_000 });
    });
    return { playerId, accessToken: await accessToken(this.env, playerId, "1"), refreshToken: `${playerId}.${refresh}`, expiresIn: 900 };
  }
  async refresh(refresh: string) {
    const next = secret();
    const state = this.transaction(() => {
      const s = this.read();
      requireThat(s && s.refreshExpires > NOW() && equalSecret(s.refreshHash, digest(refresh)), "INVALID_REFRESH", 401);
      s.refreshHash = digest(next); s.refreshExpires = NOW() + 30 * 86400_000;
      s.version = (BigInt(s.version) + 1n).toString();
      this.write(s); return s;
    });
    return { playerId: state.playerId, accessToken: await accessToken(this.env, state.playerId, state.version), refreshToken: `${state.playerId}.${next}`, expiresIn: 900 };
  }
  check(version: string) {
    requireThat(this.read()?.version === version, "INVALID_TOKEN", 401);
  }
  async photonTicket() {
    const appId = photonConfiguration(this.env); const issuedAt = Math.floor(NOW() / 1000);
    const nonce = crypto.randomUUID();
    const data = this.transaction(() => {
      const s = this.read(); requireThat(s, "UNKNOWN_IDENTITY", 401);
      s.photonTickets ??= {};
      for (const [id, ticket] of Object.entries(s.photonTickets))
        if (ticket.expiresAt <= issuedAt || ticket.version !== s.version) delete s.photonTickets[id];
      requireThat(Object.keys(s.photonTickets).length < 16, "AUTH_TICKET_LIMIT", 429);
      s.photonTickets[nonce] = { appId, version: s.version, expiresAt: issuedAt + 60 };
      this.write(s); return { playerId: s.playerId, version: s.version };
    });
    return { ticket: await signPhotonTicket(this.env, data.playerId, data.version, nonce, issuedAt), appId, expiresAt: (issuedAt + 60) * 1000 };
  }
  consumePhotonTicket(nonce: string, version: string, appId: string, expiresAt: number) {
    return this.transaction(() => {
      const s = this.read(); const ticket = s?.photonTickets?.[nonce];
      requireThat(s && ticket && s.version === version && ticket.version === version && ticket.appId === appId
        && ticket.expiresAt === expiresAt && NOW() < expiresAt * 1000, "INVALID_PHOTON_TICKET", 401);
      delete s.photonTickets![nonce]; this.write(s);
      return { ResultCode: 1, UserId: s.playerId };
    });
  }
  profile() {
    const s = this.read(); requireThat(s, "UNKNOWN_IDENTITY", 401);
    return { playerId: s.playerId, displayName: s.displayName, partyId: s.membership?.partyId };
  }
  async enter(operationId: string, kind: "CREATE" | "JOIN", partyId: string, settings?: PartySettings, invite?: string) {
    const requestHash = digest({ kind, partyId, settings, invite });
    this.transaction(() => {
      const s = this.read(); requireThat(s, "UNKNOWN_IDENTITY", 401);
      requireThat(!s.leave || s.leave.complete, "MEMBERSHIP_BUSY");
      requireThat(!s.membership || (s.membership.partyId === partyId && s.membership.operationId === operationId && s.membership.kind === kind), "ALREADY_IN_PARTY");
      requireThat(!s.membership || s.membership.requestHash === requestHash, "IDEMPOTENCY_CONFLICT");
      s.membership ??= { partyId, operationId, kind, requestHash, status: "PENDING" }; this.write(s);
    });
    const s = this.read()!;
    try {
      const party = this.env.PARTIES.getByName(partyId);
      const result = kind === "CREATE"
        ? await rpcValue(party.create(partyId, s.playerId, settings!))
        : await rpcValue(party.join(s.playerId, operationId, invite!));
      this.transaction(() => {
        const current = this.read()!;
        if (current.membership?.requestHash === requestHash && current.membership.status === "PENDING") {
          current.membership.status = "ACTIVE"; this.write(current);
        }
      });
      return result;
    } catch (e) {
      if (e instanceof Error && /WB:/.test(e.message)) {
        this.transaction(() => {
          const current = this.read()!;
          if (current.membership?.operationId === operationId && current.membership.status === "PENDING") {
            delete current.membership; this.write(current);
          }
        });
      }
      throw e;
    }
  }
  async leave(partyId: string, operationId: string, expectedRevision: string) {
    const requestHash = digest({ partyId, expectedRevision });
    const op = this.transaction(() => {
      const s = this.read(); requireThat(s, "UNKNOWN_IDENTITY", 401);
      if (s.leave?.operationId === operationId) {
        requireThat(s.leave.requestHash === requestHash, "IDEMPOTENCY_CONFLICT");
        if (s.leave.complete) return { replay: true as const, result: s.leave.result };
      }
      requireThat(s.membership?.partyId === partyId, "NOT_PARTY_MEMBER", 403);
      requireThat(s.membership.status === "ACTIVE", "MEMBERSHIP_BUSY");
      requireThat(!s.leave || s.leave.complete || s.leave.operationId === operationId, "MEMBERSHIP_BUSY");
      s.leave = { operationId, requestHash, partyId, complete: false }; this.write(s);
      return { replay: false as const, playerId: s.playerId };
    });
    if (op.replay) return op.result;
    try {
      const result = await rpcValue(this.env.PARTIES.getByName(partyId).mutate(op.playerId, operationId, { kind: "leave", expectedRevision }));
      return this.transaction(() => {
        const current = this.read()!;
        if (current.leave?.operationId === operationId && current.leave.complete) return current.leave.result;
        requireThat(current.leave?.operationId === operationId, "OPERATION_LOST");
        if (current.membership?.partyId === partyId) delete current.membership;
        current.leave.complete = true; current.leave.result = result; this.write(current);
        return result;
      });
    } catch (e) {
      if (e instanceof Error && /WB:/.test(e.message)) this.transaction(() => {
        const current = this.read()!;
        if (current.leave?.operationId === operationId && !current.leave.complete) {
          delete current.leave; this.write(current);
        }
      });
      throw e;
    }
  }
}
