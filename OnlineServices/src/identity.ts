import { Store } from "./store";
import { accessToken } from "./auth";
import { digest, equalSecret, requireThat, secret, NOW, type PartySettings } from "./model";

type IdentityState = {
  playerId: string; displayName: string; version: string;
  refreshHash: string; refreshExpires: number;
  membership?: { partyId: string; operationId: string; requestHash: string; status: "PENDING" | "ACTIVE"; kind: "CREATE" | "JOIN" };
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
  profile() {
    const s = this.read(); requireThat(s, "UNKNOWN_IDENTITY", 401);
    return { playerId: s.playerId, displayName: s.displayName, partyId: s.membership?.partyId };
  }
  async enter(operationId: string, kind: "CREATE" | "JOIN", partyId: string, settings?: PartySettings, invite?: string) {
    const requestHash = digest({ kind, partyId, settings, invite });
    this.transaction(() => {
      const s = this.read(); requireThat(s, "UNKNOWN_IDENTITY", 401);
      requireThat(!s.membership || (s.membership.partyId === partyId && s.membership.operationId === operationId && s.membership.kind === kind), "ALREADY_IN_PARTY");
      requireThat(!s.membership || s.membership.requestHash === requestHash, "IDEMPOTENCY_CONFLICT");
      s.membership ??= { partyId, operationId, kind, requestHash, status: "PENDING" }; this.write(s);
    });
    const s = this.read()!;
    try {
      const party = this.env.PARTIES.getByName(partyId);
      const result = kind === "CREATE"
        ? await party.create(partyId, s.playerId, settings!)
        : await party.join(s.playerId, operationId, invite!);
      this.transaction(() => {
        const current = this.read()!; current.membership!.status = "ACTIVE"; this.write(current);
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
  async leave(operationId: string, expectedRevision: string) {
    const s = this.read(); requireThat(s?.membership, "NO_PARTY", 404);
    const partyId = s.membership.partyId;
    const result = await this.env.PARTIES.getByName(partyId).mutate(s.playerId, operationId, { kind: "leave", expectedRevision });
    this.transaction(() => {
      const current = this.read()!;
      if (current.membership?.partyId === partyId) { delete current.membership; this.write(current); }
    });
    return result;
  }
}
