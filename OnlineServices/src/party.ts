import { Store } from "./store";
import { rpcValue } from "./rpc";
import { BundleStore, BundleRequests, bindBundle, type BundleAction, type BundleTransfer } from "./bundles";
import {
  code, digest, equalSecret, poolKey, requireThat, NOW, validatePartySize,
  type PartySettings, type PartyState, type QueueInput, type Attempt,
} from "./model";

type Mutation = { kind: "settings"; expectedRevision: string; settings: PartySettings }
  | { kind: "ready"; expectedRevision: string; ready: boolean }
  | { kind: "leave"; expectedRevision: string }
  | { kind: "cancel"; expectedRevision: string };
export class Party extends Store<PartyState> {
  private bundles = new BundleStore(this.ctx.storage.sql);
  private contentCleanup(now: number) {
    this.bundles.cleanup(now);
  }
  private contentAlarm() {
    const expiry = this.bundles.nextExpiry();
    if (expiry !== undefined) this.ctx.waitUntil(this.ctx.storage.setAlarm(Math.max(NOW() + 1, expiry)));
  }
  content(actor: string, action: BundleAction, value: unknown) {
    const data = BundleRequests[action].parse(value) as any;
    return this.transaction(() => {
      const now = NOW(); this.contentCleanup(now);
      const s = this.read(); requireThat(s, "PARTY_NOT_FOUND", 404); this.member(s, actor);
      if (action === "content-info") return { descriptor: s.workshop ? this.bundles.info(s.id, s.workshop.bundleId) : null };
      if (action === "content-read") {
        requireThat(s.workshop?.bundleId === data.bundleId, "CONTENT_NOT_FOUND", 404);
        return this.bundles.read(s.id, data.bundleId, data.index);
      }
      requireThat(s.leaderId === actor, "LEADER_REQUIRED", 403);
      requireThat(!s.active && !s.operation, "PARTY_BUSY");
      if (action === "content-finalize" && s.workshop?.bundleId === data.bundleId) {
        this.bundles.finalize(s.id, data.expectedRevision, data.bundleId, now); return this.view(s);
      }
      requireThat(s.revision === data.expectedRevision, "REVISION_CONFLICT");
      let result: unknown;
      if (action === "content-begin") {
        bindBundle(data.descriptor, s.settings);
        result = this.bundles.begin(s.id, s.revision, data.descriptor, now, s.workshop?.bundleId);
      } else if (action === "content-chunk")
        result = this.bundles.chunk(s.id, s.revision, data.bundleId, data.index, data.data, now);
      else {
        const descriptor = this.bundles.finalize(s.id, s.revision, data.bundleId, now);
        bindBundle(descriptor, s.settings); s.workshop = descriptor; this.changed(s); this.write(s); result = this.view(s);
      }
      this.contentAlarm(); return result;
    });
  }
  async alarm() { this.transaction(() => this.contentCleanup(NOW())); this.contentAlarm(); }
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
      invite: s.invite, state: s.operation ? "UPDATING" : s.active ? "QUEUED" : "OPEN", activeAttempt: s.active, workshop: s.workshop };
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
      const leaving = kind === "mutate" && (body as Mutation).kind === "leave";
      requireThat(leaving || Object.keys(s.receipts).length < 2048, "PARTY_OPERATION_LIMIT");
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
    if (s.active) await rpcValue(this.env.POOLS.getByName(s.active.poolKey).cancel(s.active.id, s.id));
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
      if (body.kind === "ready") {
        requireThat(!op.state.active, "ALREADY_QUEUED");
        if (body.ready && op.state.workshop) {
          this.transaction(() => {
            this.contentCleanup(NOW());
            requireThat(this.bundles.info(op.state.id, op.state.workshop!.bundleId), "CONTENT_SOURCE_EXPIRED");
          });
        }
      }
      else await this.cancelActive(op.state);
      return this.finish(key, s => {
        if (body.kind === "ready") s.members.find(m => m.playerId === actor)!.ready = body.ready;
        else {
          if (body.kind === "settings") {
            if (s.settings.mode !== body.settings.mode || s.settings.format !== body.settings.format ||
              s.settings.protocolVersion !== body.settings.protocolVersion) delete s.workshop;
            s.settings = body.settings;
          }
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
      requireThat(body.channel !== "QuickPlay" || s.settings.fill ||
        (s.settings.mode !== "TimeRun" && s.settings.mode !== "Freestyle") || s.members.length >= 2,
      "PUBLIC_COOP_REQUIRES_HUMANS");
      requireThat(body.channel !== "CustomFill" || s.settings.fill, "FILL_CONSENT_REQUIRED");
      let bundle: BundleTransfer | undefined;
      if (s.workshop) {
        requireThat(body.channel === "PrivateCreate", "AUTHORED_PRIVATE_CREATE_ONLY");
        requireThat(digest(body.content) === digest(s.workshop.content), "CONTENT_BINDING", 422);
        bundle = this.transaction(() => {
          this.contentCleanup(NOW());
          return this.bundles.info(s.id, s.workshop!.bundleId) ? this.bundles.transfer(s.id, s.workshop!.bundleId) : undefined;
        });
      }
      let target = poolKey(s.settings);
      if (body.channel === "PrivateJoin") {
        const locator = await rpcValue(this.env.POOLS.getByName(`code:${body.code}`).resolveCode());
        requireThat(locator.poolKey === target, "PRIVATE_SETTINGS_MISMATCH");
      }
      const locked = this.transaction(() => {
        const current = this.read()!;
        if (current.receipts[key]) return { replay: true as const, result: current.receipts[key].result };
        requireThat(current.operation?.key === key, "OPERATION_LOST");
        const pending: Attempt = current.operation.attempt ?? {
          id: crypto.randomUUID(), partyId: current.id, partyRevision: current.revision,
          members: current.members.map(m => m.playerId), settings: current.settings,
          poolKey: target, channel: body.channel, content: body.content, code: body.code,
          workshopId: current.workshop?.bundleId,
          createdAt: NOW(),
        };
        current.operation.attempt = pending; this.write(current);
        return { replay: false as const, attempt: pending };
      });
      if (locked.replay) return locked.result;
      const attempt = locked.attempt;
      const result = await rpcValue(this.env.POOLS.getByName(target).enqueue(attempt, bundle));
      if (body.channel === "PrivateCreate" && result.privateCode)
        await this.env.POOLS.getByName(`code:${result.privateCode}`).registerCode(target, result.matchId!, attempt.id);
      return this.finish(key, current => { current.active = attempt; });
    } catch (e) { return this.unlock(key, e); }
  }
  async get(actor: string) {
    const s = this.read(); requireThat(s, "PARTY_NOT_FOUND", 404); this.member(s, actor);
    const assignment = s.active ? await rpcValue(this.env.POOLS.getByName(s.active.poolKey).status(s.active.id, s.id, actor)) : undefined;
    return { ...this.view(s), assignment };
  }
}
