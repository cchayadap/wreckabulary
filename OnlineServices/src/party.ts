import { Store } from "./store";
import {
  code, digest, equalSecret, poolKey, requireThat, NOW, validatePartySize,
  type PartySettings, type PartyState, type QueueInput, type Attempt,
} from "./model";

type Mutation = { kind: "settings"; expectedRevision: string; settings: PartySettings }
  | { kind: "ready"; expectedRevision: string; ready: boolean }
  | { kind: "leave"; expectedRevision: string }
  | { kind: "cancel"; expectedRevision: string };
export class Party extends Store<PartyState> {
  create(id: string, playerId: string, settings: PartySettings) {
    return this.transaction(() => {
      const existing = this.read();
      if (existing) { requireThat(existing.leaderId === playerId, "FORBIDDEN", 403); return this.view(existing); }
      validatePartySize(settings, 1);
      const s: PartyState = { id, revision: "1", leaderId: playerId, members: [{ playerId, ready: false }], settings, invite: code(), receipts: {} };
      this.write(s); return this.view(s);
    });
  }
  private view(s: PartyState) {
    return { id: s.id, revision: s.revision, leaderId: s.leaderId, members: s.members, settings: s.settings,
      invite: s.invite, state: s.operation ? "UPDATING" : s.active ? "QUEUED" : "OPEN", activeAttempt: s.active };
  }
  private member(s: PartyState, actor: string) { requireThat(s.members.some(m => m.playerId === actor), "NOT_PARTY_MEMBER", 403); }
  private begin(actor: string, key: string, body: unknown, kind: string, joining = false) {
    return this.transaction(() => {
      const s = this.read(); requireThat(s, "PARTY_NOT_FOUND", 404);
      const hash = digest({ actor, body, kind });
      const receipt = s.receipts[key];
      if (receipt) { requireThat(receipt.hash === hash, "IDEMPOTENCY_CONFLICT"); return { replay: true as const, result: receipt.result }; }
      if (!joining) this.member(s, actor);
      requireThat(!s.operation || (s.operation.key === key && s.operation.hash === hash), "PARTY_BUSY");
      requireThat(Object.keys(s.receipts).length < 2048, "PARTY_OPERATION_LIMIT");
      s.operation ??= { key, hash, kind }; this.write(s);
      return { replay: false as const, state: s };
    });
  }
  private finish(key: string, update: (s: PartyState) => void) {
    return this.transaction(() => {
      const s = this.read()!;
      if (s.receipts[key]) return s.receipts[key].result;
      requireThat(s.operation?.key === key, "OPERATION_LOST");
      update(s);
      const hash = s.operation.hash; delete s.operation;
      const result = this.view(s); s.receipts[key] = { hash, result };
      this.write(s); return result;
    });
  }
  private unlock(key: string, error: unknown) {
    if (error instanceof Error && /WB:/.test(error.message)) {
      this.transaction(() => {
        const s = this.read()!;
        if (s.operation?.key === key) { delete s.operation; this.write(s); }
      });
    }
    throw error;
  }
  private async cancelActive(s: PartyState) {
    if (s.active) await this.env.POOLS.getByName(s.active.poolKey).cancel(s.active.id, s.id);
  }
  async join(actor: string, key: string, invite: string) {
    const op = this.begin(actor, key, { invite }, "join", true);
    if (op.replay) return op.result;
    try {
      requireThat(equalSecret(op.state.invite, invite), "INVALID_INVITE", 403);
      requireThat(op.state.members.length < 4, "PARTY_FULL");
      validatePartySize(op.state.settings, op.state.members.length + 1);
      await this.cancelActive(op.state);
      return this.finish(key, s => {
        requireThat(!s.members.some(m => m.playerId === actor), "ALREADY_IN_PARTY");
        s.members.push({ playerId: actor, ready: false }); this.changed(s);
      });
    } catch (e) { return this.unlock(key, e); }
  }
  private changed(s: PartyState) {
    delete s.active; s.revision = (BigInt(s.revision) + 1n).toString();
    s.members.forEach(m => m.ready = false);
  }
  async mutate(actor: string, key: string, body: Mutation) {
    const op = this.begin(actor, key, body, "mutate");
    if (op.replay) return op.result;
    try {
      requireThat(op.state.revision === body.expectedRevision, "REVISION_CONFLICT");
      if (body.kind === "settings" || body.kind === "cancel") requireThat(op.state.leaderId === actor, "LEADER_REQUIRED", 403);
      if (body.kind === "settings") validatePartySize(body.settings, op.state.members.length);
      if (body.kind === "ready") requireThat(!op.state.active, "ALREADY_QUEUED");
      else await this.cancelActive(op.state);
      return this.finish(key, s => {
        if (body.kind === "ready") s.members.find(m => m.playerId === actor)!.ready = body.ready;
        else {
          if (body.kind === "settings") s.settings = body.settings;
          if (body.kind === "leave") {
            s.members = s.members.filter(m => m.playerId !== actor);
            s.leaderId = s.members[0]?.playerId ?? "";
          }
          this.changed(s);
        }
      });
    } catch (e) { return this.unlock(key, e); }
  }
  async enqueueParty(actor: string, key: string, body: QueueInput) {
    const op = this.begin(actor, key, body, "queue");
    if (op.replay) return op.result;
    try {
      const s = op.state;
      requireThat(s.leaderId === actor, "LEADER_REQUIRED", 403);
      requireThat(s.revision === body.expectedRevision, "REVISION_CONFLICT");
      requireThat(!s.active && s.members.every(m => m.ready), "PARTY_NOT_READY");
      validatePartySize(s.settings, s.members.length);
      requireThat(s.settings.mode !== "Freestyle" || body.channel === "PrivateCreate" || body.channel === "PrivateJoin", "PRIVATE_ONLY");
      requireThat(body.channel !== "CustomFill" || s.settings.fill, "FILL_CONSENT_REQUIRED");
      let target = poolKey(s.settings);
      if (body.channel === "PrivateJoin") {
        const locator = await this.env.POOLS.getByName(`code:${body.code}`).resolveCode();
        requireThat(locator.poolKey === target, "PRIVATE_SETTINGS_MISMATCH");
      }
      const attempt: Attempt = s.operation!.attempt ?? {
        id: crypto.randomUUID(), partyId: s.id, partyRevision: s.revision,
        members: s.members.map(m => m.playerId), settings: s.settings,
        poolKey: target, channel: body.channel, content: body.content, code: body.code,
        createdAt: NOW(),
      };
      this.transaction(() => { const current = this.read()!; current.operation!.attempt = attempt; this.write(current); });
      const result = await this.env.POOLS.getByName(target).enqueue(attempt);
      if (body.channel === "PrivateCreate" && result.privateCode)
        await this.env.POOLS.getByName(`code:${result.privateCode}`).registerCode(target, result.matchId!, attempt.id);
      return this.finish(key, current => { current.active = attempt; });
    } catch (e) { return this.unlock(key, e); }
  }
  async get(actor: string) {
    const s = this.read(); requireThat(s, "PARTY_NOT_FOUND", 404); this.member(s, actor);
    const assignment = s.active ? await this.env.POOLS.getByName(s.active.poolKey).status(s.active.id, s.id, actor) : undefined;
    return { ...this.view(s), assignment };
  }
}
