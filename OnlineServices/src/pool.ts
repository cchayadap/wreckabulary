import { z } from "zod";
import { Store } from "./store";
import { signGrant } from "./auth";
import { CheckpointStore, type CheckpointAction } from "./checkpoints";
import { BundleStore, BundleRequests, bindBundle, type BundleTransfer } from "./bundles";
import {
  Content, NOW, RECONNECT_MS, capacity, code, digest, emptyPool, logicalFull, poolKey,
  publicRoster, requireThat, seal, secret, teamFor, validatePartySize, fullRacePacking, equalSecret,
  type Attempt, type ContentBinding, type Match, type PoolState, type Seat,
} from "./model";

export class MatchPool extends Store<PoolState> {
  private checkpointStore = new CheckpointStore(this.ctx.storage.sql);
  private bundles = new BundleStore(this.ctx.storage.sql);
  content(matchId: string, actor: string, action: "content-info" | "content-read", value: unknown) {
    const data = BundleRequests[action].parse(value) as any;
    return this.run((s, now) => {
      const m = s.matches[matchId], seat = this.member(m, actor);
      requireThat(!seat.returned && s.assignments[seat.attemptId] === matchId && m.phase !== "CLOSED", "CONTENT_ACCESS_EXPIRED", 403);
      if (m.phase === "STARTED") this.checkReconnect(seat, now);
      if (action === "content-info") return { descriptor: m.workshop ?? null };
      requireThat(m.workshop?.bundleId === data.bundleId, "CONTENT_NOT_FOUND", 404);
      return this.bundles.read(matchId, data.bundleId, data.index);
    });
  }
  checkpoint(matchId: string, actor: string, action: CheckpointAction, data: unknown) {
    return this.run((s, now) => {
      const match = s.matches[matchId];
      requireThat(match, "MATCH_NOT_FOUND", 404);
      return this.checkpointStore.execute(match, actor, action, data, now);
    });
  }
  private run<R>(fn: (s: PoolState, now: number) => R): R {
    return this.transaction(() => {
      const s = this.read() ?? emptyPool(); const now = NOW();
      this.checkpointStore.cleanupExpired(now);
      this.expire(s, now);
      const result = fn(s, now); this.write(s);
      const deadlines = Object.values(s.matches).flatMap(m =>
        m.phase === "ADMITTING" ? [m.deadline!] :
          m.phase === "FORMING" && !m.privateCode && (m.settings.mode === "FreeForAll" || m.settings.mode === "Freestyle") && m.seats.length >= 2
            ? [m.createdAt + 30_000] : []);
      deadlines.push(...Object.values(s.matches).filter(m => m.phase === "FORMING" || m.phase === "ABORTED" || m.phase === "CLOSED")
        .map(m => m.createdAt + 86400_000));
      deadlines.push(...Object.values(s.matches).filter(m => m.phase === "STARTED")
        .map(m => Math.max(...m.seats.filter(seat => !seat.returned).map(seat => seat.reconnectDeadline!))));
      deadlines.push(...Object.values(s.tickets).map(a => a.createdAt + 86400_000));
      const checkpointExpiry = this.checkpointStore.nextExpiry();
      if (checkpointExpiry !== undefined) deadlines.push(checkpointExpiry);
      if (deadlines.length) this.ctx.waitUntil(this.ctx.storage.setAlarm(Math.max(now + 1, Math.min(...deadlines))));
      return result;
    });
  }
  private approve(content: ContentBinding) {
    if (content.kind === "BuiltIn") {
      const allowed = z.array(Content).parse(JSON.parse(this.env.BUILTIN_CONTENT_JSON));
      requireThat(allowed.some(c => digest(c) === digest(content)), "CONTENT_NOT_APPROVED", 422);
    }
  }
  registerCode(target: string, matchId: string, ownerAttempt: string) {
    return this.run(s => {
      requireThat(!s.codeTarget || s.codeTarget.ownerAttempt === ownerAttempt, "CODE_COLLISION");
      s.codeTarget = { poolKey: target, matchId, ownerAttempt };
    });
  }
  resolveCode() {
    const s = this.read(); requireThat(s?.codeTarget, "PRIVATE_NOT_FOUND", 404); return s.codeTarget;
  }
  enqueue(a: Attempt, bundle?: BundleTransfer) {
    return this.run((s, now) => {
      requireThat(!s.tombstones[a.id], "ATTEMPT_CANCELLED");
      requireThat(a.createdAt >= now - 86400_000 && a.createdAt <= now + 5000, "ATTEMPT_EXPIRED");
      requireThat(poolKey(a.settings) === a.poolKey, "POOL_MISMATCH");
      validatePartySize(a.settings, a.members.length);
      requireThat(a.channel !== "QuickPlay" || a.settings.fill ||
        (a.settings.mode !== "TimeRun" && a.settings.mode !== "Freestyle") || a.members.length >= 2,
      "PUBLIC_COOP_REQUIRES_HUMANS");
      requireThat(new Set(a.members).size === a.members.length, "DUPLICATE_MEMBER");
      const assigned = s.assignments[a.id];
      if (assigned) {
        const m = s.matches[assigned];
        const stored = m.reservations.find(r => r.attempt.id === a.id)?.attempt;
        requireThat(stored && digest(stored) === digest(a), "IDEMPOTENCY_CONFLICT");
        return { matchId: m.id, privateCode: m.privateCode };
      }
      if (s.tickets[a.id]) { requireThat(digest(s.tickets[a.id]) === digest(a), "IDEMPOTENCY_CONFLICT"); return {}; }
      requireThat(Object.keys(s.tickets).length < 1000 && Object.keys(s.matches).length < 1000, "POOL_BUSY", 429);
      const active = [...Object.values(s.tickets), ...Object.values(s.matches)
        .filter(m => m.phase !== "ABORTED" && m.phase !== "CLOSED")
        .flatMap(m => m.reservations.filter(r => s.assignments[r.attempt.id] === m.id).map(r => r.attempt))];
      requireThat(!active.some(other => other.id !== a.id && other.members.some(p => a.members.includes(p))), "PLAYER_ALREADY_QUEUED");
      if (a.content) this.approve(a.content);
      requireThat(!a.workshopId || (a.channel === "PrivateCreate" && bundle?.descriptor.bundleId === a.workshopId), "CONTENT_SOURCE_EXPIRED");
      if (bundle) {
        requireThat(a.channel === "PrivateCreate" && digest(a.content) === digest(bundle.descriptor.content), "CONTENT_BINDING", 422);
        bindBundle(bundle.descriptor, a.settings);
      }
      if (a.channel === "QuickPlay") requireThat(a.content?.kind === "BuiltIn", "QUICK_PLAY_BUILTIN_ONLY", 422);
      if (a.channel === "CustomFill") requireThat(a.settings.fill && !a.content, "CUSTOM_FILL_CONSENT_REQUIRED", 422);
      if (a.channel === "PrivateCreate") {
        requireThat(a.content, "CONTENT_REQUIRED", 422);
        const m = this.newMatch(a, now, true); s.matches[m.id] = m;
        if (bundle) { this.bundles.install(m.id, bundle); m.workshop = bundle.descriptor; }
        this.enroll(s, m, a, 0);
      } else if (a.channel === "PrivateJoin") {
        const m = Object.values(s.matches).find(m => m.privateCode === a.code && m.phase === "FORMING");
        requireThat(m, "PRIVATE_NOT_OPEN", 404);
        const team = teamFor(m, a, true); requireThat(team !== undefined, "PRIVATE_TEAM_FULL");
        this.enroll(s, m, a, team);
      } else s.tickets[a.id] = a;
      this.drain(s, now);
      const m = s.matches[s.assignments[a.id]];
      return { matchId: m?.id, privateCode: m?.privateCode };
    });
  }
  private newMatch(a: Attempt, now: number, isPrivate: boolean): Match {
    return {
      id: crypto.randomUUID(), generation: "1", settings: a.settings, content: a.content!,
      privateCode: isPrivate ? code() : undefined, ownerId: a.members[0],
      createdAt: now, phase: "FORMING", reservations: [], seats: [],
    };
  }
  private enroll(s: PoolState, m: Match, a: Attempt, teamId: number) {
    const offset = m.seats.filter(seat => seat.teamId === teamId).length;
    m.reservations.push({ attempt: a, teamId });
    m.seats.push(...a.members.map((playerId, i) => ({
      playerId, partyId: a.partyId, partyRevision: a.partyRevision, attemptId: a.id,
      teamId, seat: offset + i,
      connectionSecret: secret(), accepted: false,
    })));
    s.assignments[a.id] = m.id; delete s.tickets[a.id];
  }
  private drain(s: PoolState, now: number) {
    this.packPublicFillRaces(s, now);
    const tickets = Object.values(s.tickets).sort((a, b) => a.createdAt - b.createdAt || a.id.localeCompare(b.id));
    for (const a of tickets) {
      const candidates = Object.values(s.matches).filter(m => m.phase === "FORMING"
        && (a.channel === "CustomFill"
          ? !!m.privateCode && m.content.kind === "Workshop"
          : !m.privateCode && m.content.kind === "BuiltIn" && digest(m.content) === digest(a.content)))
        .sort((a, b) => a.createdAt - b.createdAt || a.id.localeCompare(b.id));
      let match = candidates.find(m => teamFor(m, a) !== undefined);
      if (!match && a.channel === "QuickPlay") {
        match = this.newMatch(a, now, false); s.matches[match.id] = match;
      }
      if (match) {
        const team = teamFor(match, a);
        if (team !== undefined) this.enroll(s, match, a, team);
      }
    }
    for (const m of Object.values(s.matches)) {
      if (m.phase === "FORMING" && (logicalFull(m)
        || (!m.privateCode && (m.settings.mode === "FreeForAll" || m.settings.mode === "Freestyle") &&
          m.seats.length >= 2 && now - m.createdAt >= 30_000))) seal(m, now);
    }
  }
  private packPublicFillRaces(s: PoolState, now: number) {
    const eligible = (a: Attempt) => a.channel === "QuickPlay" && a.settings.fill &&
      (a.settings.mode === "TeamRace" || a.settings.mode === "BrawlRace");
    const open = Object.values(s.matches).filter(m => m.phase === "FORMING" && !m.privateCode &&
      m.reservations.length > 0 && m.reservations.every(r => eligible(r.attempt)));
    const candidates = [...Object.values(s.tickets).filter(eligible), ...open.flatMap(m => m.reservations.map(r => r.attempt))]
      .sort((a, b) => a.createdAt - b.createdAt || a.id.localeCompare(b.id));
    const groups = new Map<string, Attempt[]>();
    for (const a of candidates) {
      const key = digest({ settings: a.settings, content: a.content });
      const group = groups.get(key) ?? []; group.push(a); groups.set(key, group);
    }
    for (let available of groups.values()) {
      let packing;
      while ((packing = fullRacePacking(available, capacity(available[0].settings)))) {
        const selected = new Set(packing.map(r => r.attempt.id));
        for (const old of open) {
          if (!old.reservations.some(r => selected.has(r.attempt.id))) continue;
          old.reservations = old.reservations.filter(r => !selected.has(r.attempt.id));
          old.seats = old.seats.filter(seat => !selected.has(seat.attemptId));
          if (!old.reservations.length) delete s.matches[old.id];
          else {
            old.generation = (BigInt(old.generation) + 1n).toString();
            for (const team of [0, 1]) old.seats.filter(seat => seat.teamId === team).forEach((seat, i) => { seat.seat = i; });
          }
        }
        const match = this.newMatch(packing[0].attempt, now, false); s.matches[match.id] = match;
        for (const reservation of packing) this.enroll(s, match, reservation.attempt, reservation.teamId);
        seal(match, now);
        available = available.filter(a => !selected.has(a.id));
        if (available.length === 0) break;
      }
    }
  }
  private abort(s: PoolState, m: Match, failedAttempts: Set<string>, now: number) {
    m.phase = "ABORTED";
    for (const r of m.reservations) {
      const a = r.attempt; delete s.assignments[a.id];
      if (!failedAttempts.has(a.id) && (a.channel === "QuickPlay" || a.channel === "CustomFill")) s.tickets[a.id] = a;
      else s.tombstones[a.id] = now;
    }
  }
  private expire(s: PoolState, now: number) {
    for (const [id, at] of Object.entries(s.tombstones)) if (at + 2 * 86400_000 < now) delete s.tombstones[id];
    for (const [id, ticket] of Object.entries(s.tickets)) {
      if (ticket.createdAt + 86400_000 < now) { delete s.tickets[id]; s.tombstones[id] = now; }
    }
    for (const m of Object.values(s.matches)) {
      if (m.phase === "FORMING" && m.createdAt + 86400_000 < now)
        this.abort(s, m, new Set(m.reservations.map(r => r.attempt.id)), now);
      if (m.phase === "ADMITTING" && m.deadline! <= now) {
        const failed = new Set(m.seats.filter(seat => !seat.accepted).map(seat => seat.attemptId));
        this.abort(s, m, failed, now);
      }
      if (m.phase === "STARTED" && m.seats.every(seat => seat.returned || seat.reconnectDeadline! <= now))
        m.phase = "CLOSED";
      if ((m.phase === "ABORTED" || m.phase === "CLOSED") && m.createdAt + 86400_000 < now) {
        for (const r of m.reservations) {
          if (s.assignments[r.attempt.id] === m.id) {
            delete s.assignments[r.attempt.id]; s.tombstones[r.attempt.id] = now;
          }
        }
        this.checkpointStore.deleteMatch(m.id);
        this.bundles.deleteScope(m.id);
        delete s.matches[m.id];
      }
    }
    this.drain(s, now);
  }
  cancel(attemptId: string, partyId: string) {
    return this.run((s, now) => {
      const m = s.matches[s.assignments[attemptId]];
      if (m) {
        requireThat(m.reservations.some(r => r.attempt.id === attemptId && r.attempt.partyId === partyId), "NOT_PARTY_MEMBER", 403);
        if (m.phase === "STARTED" || m.phase === "CLOSED") {
          const ownSeats = m.seats.filter(seat => seat.attemptId === attemptId);
          requireThat(ownSeats.every(seat => seat.returned || seat.reconnectDeadline! <= now), "MATCH_ALREADY_STARTED");
          delete s.assignments[attemptId];
        } else this.abort(s, m, new Set([attemptId]), now);
      } else if (s.tickets[attemptId]) requireThat(s.tickets[attemptId].partyId === partyId, "NOT_PARTY_MEMBER", 403);
      delete s.tickets[attemptId]; s.tombstones[attemptId] = now;
      this.drain(s, now);
      return { state: "CANCELLED" };
    });
  }
  status(attemptId: string, partyId: string, actor: string) {
    return this.run(s => {
      const m = s.matches[s.assignments[attemptId]];
      const a = m?.reservations.find(r => r.attempt.id === attemptId)?.attempt ?? s.tickets[attemptId];
      if (a) requireThat(a.partyId === partyId && a.members.includes(actor), "NOT_PARTY_MEMBER", 403);
      if (!m) return { state: s.tombstones[attemptId] ? "CANCELLED" : "QUEUED" };
      return this.view(m);
    });
  }
  private view(m: Match) {
    return { matchId: m.id, state: m.phase, reservationGeneration: m.generation,
      content: m.content, settings: m.settings, privateCode: m.privateCode,
      rosterHash: m.rosterHash, roster: publicRoster(m), deadline: m.deadline, workshop: m.workshop };
  }
  launch(matchId: string, actor: string) {
    return this.run((s, now) => {
      const m = s.matches[matchId]; requireThat(m && m.ownerId === actor, "OWNER_REQUIRED", 403);
      requireThat(m.privateCode && m.phase === "FORMING", "NOT_FORMING");
      requireThat(m.settings.mode === "Freestyle" || (m.settings.mode === "FreeForAll" && m.seats.length >= 2) || logicalFull(m), "MATCH_NOT_FULL");
      seal(m, now); return this.view(m);
    });
  }
  private member(m: Match | undefined, actor: string): Seat {
    requireThat(m && m.phase !== "ABORTED", "MATCH_NOT_FOUND", 404);
    const seat = m.seats.find(s => s.playerId === actor); requireThat(seat, "NOT_MATCH_MEMBER", 403);
    return seat;
  }
  async admission(matchId: string, actor: string) {
    const claims = this.run((s, now) => {
      const m = s.matches[matchId]; const seat = this.member(m, actor);
      requireThat(m.phase === "ADMITTING" || m.phase === "STARTED", "ROSTER_NOT_LOCKED");
      if (m.phase === "STARTED") this.checkReconnect(seat, now);
      return { version: 1, sub: actor, playerId: actor, partyId: seat.partyId, partyRevision: seat.partyRevision,
        attemptId: seat.attemptId, matchId, reservationGeneration: m.generation,
        teamId: seat.teamId, seat: seat.seat, protocolVersion: m.settings.protocolVersion,
        modeHash: m.content.modeHash, courseHash: m.content.courseHash, manifestHash: m.content.manifestHash,
        catalogHash: m.content.catalogHash, rosterHash: m.rosterHash!, connectionSecretHash: digest(seat.connectionSecret),
        connectionSecret: seat.connectionSecret,
      };
    });
    const { connectionSecret, ...publicClaims } = claims;
    return { grant: await signGrant(this.env, publicClaims), connectionSecret, claims: publicClaims };
  }
  async certificate(matchId: string, actor: string) {
    const claims = this.run((s, now) => {
      const m = s.matches[matchId]; const seat = this.member(m, actor);
      requireThat(m.phase === "STARTED", "MATCH_NOT_STARTED");
      this.checkReconnect(seat, now);
      return { version: 1, matchId, reservationGeneration: m.generation,
        startedAt: m.startedAt!, settings: m.settings, content: m.content,
        rosterHash: m.rosterHash!, roster: publicRoster(m) };
    });
    return { certificate: await signGrant(this.env, claims, "wordbound-match-start"), claims };
  }
  ready(matchId: string, actor: string, generation: string, manifestHash: string, rosterHash: string) {
    return this.run((s, now) => {
      const m = s.matches[matchId]; const seat = this.member(m, actor);
      requireThat(m.generation === generation && m.content.manifestHash === manifestHash && m.rosterHash === rosterHash, "CONTENT_OR_ROSTER_MISMATCH");
      requireThat(m.phase === "ADMITTING" || m.phase === "STARTED", "NOT_ADMITTING");
      seat.accepted = true;
      if (m.phase === "ADMITTING" && m.seats.every(s => s.accepted)) {
        m.phase = "STARTED"; m.startedAt = now;
        m.seats.forEach(s => s.reconnectDeadline = now + RECONNECT_MS);
      }
      return this.view(m);
    });
  }
  private checkReconnect(seat: Seat, now: number) {
    requireThat(!seat.returned && seat.reconnectDeadline !== undefined && now < seat.reconnectDeadline, "RECONNECT_EXPIRED");
  }
  connectionTicket(matchId: string, actor: string) {
    return this.run((s, now) => {
      const m = s.matches[matchId]; const seat = this.member(m, actor);
      requireThat(m.phase === "STARTED", "MATCH_NOT_STARTED"); this.checkReconnect(seat, now);
      const ticket = secret();
      seat.connectionTicket = { hash: digest(ticket), expiresAt: now + 15000 };
      return { ticket, expiresAt: seat.connectionTicket.expiresAt };
    });
  }
  async authorizeConnection(matchId: string, hostId: string, playerId: string, ticket: string, connectionSecret: string, connectionId: string) {
    const claims = this.run((s, now) => {
      const m = s.matches[matchId]; const host = this.member(m, hostId); const seat = this.member(m, playerId);
      requireThat(m.phase === "STARTED", "MATCH_NOT_STARTED"); this.checkReconnect(host, now); this.checkReconnect(seat, now);
      const requestHash = digest({ hostId, playerId, ticket, connectionSecret, connectionId });
      if (seat.connection?.id === connectionId) {
        requireThat(seat.connection.hostId === hostId && seat.connection.requestHash === requestHash, "IDEMPOTENCY_CONFLICT");
        requireThat(seat.connection.leaseExpiresAt > now, "CONNECTION_EXPIRED");
      } else {
        requireThat(seat.connectionTicket && seat.connectionTicket.expiresAt > now &&
          equalSecret(seat.connectionTicket.hash, digest(ticket)) && equalSecret(seat.connectionSecret, connectionSecret),
        "INVALID_CONNECTION_TICKET", 403);
        delete seat.connectionTicket;
        seat.connectionEpoch = (BigInt(seat.connectionEpoch ?? "0") + 1n).toString();
        m.connectionRevision = (BigInt(m.connectionRevision ?? "0") + 1n).toString();
        seat.connection = { id: connectionId, hostId, requestHash, epoch: seat.connectionEpoch,
          revision: m.connectionRevision, leaseExpiresAt: now + 15000 };
        delete seat.disconnectedAt; seat.reconnectDeadline = now + RECONNECT_MS;
      }
      return { version: 1, matchId, reservationGeneration: m.generation, rosterHash: m.rosterHash!,
        playerId, hostId, connectionId, connectionEpoch: seat.connection!.epoch,
        leaseExpiresAt: seat.connection!.leaseExpiresAt, connectionRevision: seat.connection!.revision };
    });
    return { authorization: await signGrant(this.env, claims, "wordbound-connection", 15), claims };
  }
  connectionState(matchId: string, actor: string, renew: Array<{ playerId: string; connectionId: string; connectionEpoch: string }>) {
    return this.run((s, now) => {
      const m = s.matches[matchId]; const caller = this.member(m, actor);
      requireThat(m.phase === "STARTED", "MATCH_NOT_STARTED"); this.checkReconnect(caller, now);
      requireThat(new Set(renew.map(c => c.playerId)).size === renew.length, "DUPLICATE_CONNECTION", 400);
      for (const request of renew) {
        const seat = this.member(m, request.playerId); const current = seat.connection;
        if (!seat.returned && current?.hostId === actor && current.id === request.connectionId &&
          current.epoch === request.connectionEpoch && current.leaseExpiresAt > now && seat.reconnectDeadline! > now) {
          current.leaseExpiresAt = now + 15000; seat.reconnectDeadline = now + RECONNECT_MS;
        }
      }
      return { matchId, reservationGeneration: m.generation, rosterHash: m.rosterHash!,
        revision: m.connectionRevision ?? "0", serverTime: now,
        seats: m.seats.map(seat => ({ playerId: seat.playerId, returned: !!seat.returned,
          connectionEpoch: seat.connectionEpoch ?? "0", connectionId: seat.connection?.id ?? null,
          hostId: seat.connection?.hostId ?? null,
          leaseExpiresAt: seat.returned ? 0 : seat.connection?.leaseExpiresAt ?? 0,
          reconnectDeadline: seat.reconnectDeadline! })) };
    });
  }
  presence(matchId: string, actor: string, connected: boolean) {
    return this.run((s, now) => {
      const m = s.matches[matchId]; const seat = this.member(m, actor);
      requireThat(m.phase === "STARTED", "MATCH_NOT_STARTED");
      this.checkReconnect(seat, now);
      requireThat(!connected || !seat.connection, "TRANSPORT_PRESENCE_REQUIRED");
      if (connected) {
        delete seat.disconnectedAt; seat.reconnectDeadline = now + RECONNECT_MS;
      } else if (seat.disconnectedAt === undefined) {
        seat.disconnectedAt = now; seat.reconnectDeadline = now + RECONNECT_MS;
        if (seat.connection) {
          seat.connection.leaseExpiresAt = 0;
          seat.connectionEpoch = (BigInt(seat.connectionEpoch ?? "0") + 1n).toString();
          m.connectionRevision = (BigInt(m.connectionRevision ?? "0") + 1n).toString();
        }
      }
      return { playerId: actor, seat: seat.seat, reconnectDeadline: seat.reconnectDeadline };
    });
  }
  returnToLobby(matchId: string, actor: string) {
    return this.run(s => {
      const m = s.matches[matchId];
      requireThat(m && (m.phase === "STARTED" || m.phase === "CLOSED"), "MATCH_NOT_STARTED");
      const seat = m.seats.find(seat => seat.playerId === actor);
      requireThat(seat, "NOT_MATCH_MEMBER", 403);
      if (!seat.returned) {
        seat.returned = true; delete seat.connectionTicket;
        seat.connectionEpoch = (BigInt(seat.connectionEpoch ?? "0") + 1n).toString();
        if (seat.connection) seat.connection.leaseExpiresAt = 0;
        m.connectionRevision = (BigInt(m.connectionRevision ?? "0") + 1n).toString();
      }
      if (m.seats.every(seat => seat.returned)) m.phase = "CLOSED";
      return { state: "RETURNED", playerId: actor };
    });
  }
  async alarm() { this.run(() => undefined); }
}
