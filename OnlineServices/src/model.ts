import { createHash, randomBytes, timingSafeEqual } from "node:crypto";
import { z } from "zod";

export const Id = z.string().uuid();
export const Revision = z.string().regex(/^(0|[1-9][0-9]{0,18})$/);
export const Hash = z.string().regex(/^[a-f0-9]{64}$/);
export const Mode = z.enum(["TimeRun", "TeamRace", "BrawlRace", "FreeForAll", "Freestyle"]);
export const Settings = z.object({
  mode: Mode,
  format: z.enum(["Solo", "Duo", "Squad"]),
  fill: z.boolean(),
  region: z.enum(["asia", "eu", "us"]),
  protocolVersion: z.string().regex(/^[a-zA-Z0-9_-]{1,24}$/),
  mapPoolVersion: z.string().regex(/^[a-zA-Z0-9_-]{1,24}$/),
}).strict();
export const Content = z.object({
  kind: z.enum(["BuiltIn", "Workshop"]),
  courseId: z.string().regex(/^[a-zA-Z0-9_-]{1,64}$/),
  courseHash: Hash,
  manifestHash: Hash,
  modeHash: Hash,
  catalogHash: Hash,
}).strict();
export const QueueRequest = z.object({
  expectedRevision: Revision,
  channel: z.enum(["QuickPlay", "CustomFill", "PrivateCreate", "PrivateJoin"]),
  content: Content.optional(),
  code: z.string().regex(/^[a-f0-9]{16}$/).optional(),
}).strict().superRefine((v, c) => {
  if ((v.channel === "QuickPlay" || v.channel === "PrivateCreate") !== !!v.content)
    c.addIssue({ code: "custom", message: "Content required only for QuickPlay or PrivateCreate" });
  if ((v.channel === "PrivateJoin") !== !!v.code)
    c.addIssue({ code: "custom", message: "Code required only for PrivateJoin" });
});
export type PartySettings = z.infer<typeof Settings>;
export type ContentBinding = z.infer<typeof Content>;
export type QueueInput = z.infer<typeof QueueRequest>;
export type ServiceEnv = Env & {
  ACCESS_SECRET?: string; ADMISSION_PRIVATE_JWK?: string;
  PHOTON_APP_ID?: string; PHOTON_PROVIDER_SECRET?: string;
};
export type Member = { playerId: string; ready: boolean };
export type Attempt = {
  id: string; partyId: string; partyRevision: string; members: string[];
  settings: PartySettings; poolKey: string; channel: QueueInput["channel"];
  content?: ContentBinding; code?: string; createdAt: number;
};
export type PartyState = {
  id: string; revision: string; leaderId: string; members: Member[];
  settings: PartySettings; invite: string; active?: Attempt;
  operation?: { key: string; hash: string; kind: string; attempt?: Attempt };
  receipts: Record<string, { hash: string; result: unknown }>;
};
export type Seat = {
  playerId: string; partyId: string; partyRevision: string; attemptId: string;
  teamId: number; seat: number; connectionSecret: string;
  accepted: boolean; disconnectedAt?: number; reconnectDeadline?: number;
  returned?: boolean;
};
export type Reservation = { attempt: Attempt; teamId: number };
export type Match = {
  id: string; generation: string; settings: PartySettings; content: ContentBinding;
  privateCode?: string; ownerId: string; createdAt: number; deadline?: number;
  phase: "FORMING" | "ADMITTING" | "STARTED" | "ABORTED" | "CLOSED";
  reservations: Reservation[]; seats: Seat[]; rosterHash?: string; startedAt?: number;
};
export type PoolState = {
  tickets: Record<string, Attempt>;
  matches: Record<string, Match>;
  assignments: Record<string, string>;
  tombstones: Record<string, number>;
  codeTarget?: { poolKey: string; matchId: string; ownerAttempt: string };
};
export const emptyPool = (): PoolState => ({ tickets: {}, matches: {}, assignments: {}, tombstones: {} });
export const NOW = () => Date.now();
export const RESERVATION_MS = 90_000;
export const RECONNECT_MS = 120_000;
export function fail(code: string, status = 409): never { throw new Error(`WB:${status}:${code}`); }
export function requireThat(condition: unknown, code: string, status = 409): asserts condition {
  if (!condition) fail(code, status);
}
export function canonical(value: unknown): string {
  if (value === null || typeof value !== "object") return JSON.stringify(value);
  if (Array.isArray(value)) return `[${value.map(canonical).join(",")}]`;
  const record = value as Record<string, unknown>;
  return `{${Object.keys(record).filter(k => record[k] !== undefined).sort().map(k => `${JSON.stringify(k)}:${canonical(record[k])}`).join(",")}}`;
}
export const digest = (value: unknown) => createHash("sha256").update(canonical(value)).digest("hex");
export const secret = () => randomBytes(32).toString("base64url");
export const code = () => randomBytes(8).toString("hex");
export function equalSecret(a: string, b: string) {
  const x = Buffer.from(a); const y = Buffer.from(b);
  return x.length === y.length && timingSafeEqual(x, y);
}
export function capacity(s: PartySettings): number {
  if (s.mode === "TimeRun") return 4;
  if (s.mode === "Freestyle") return 8;
  if (s.mode === "FreeForAll") return 1;
  return { Solo: 1, Duo: 2, Squad: 4 }[s.format];
}
export function teams(s: PartySettings): number {
  return s.mode === "FreeForAll" ? 8 : s.mode === "TimeRun" || s.mode === "Freestyle" ? 1 : 2;
}
export function poolKey(s: PartySettings): string {
  return [s.region, s.protocolVersion, s.mapPoolVersion, s.mode, s.format].join(":");
}
export function validatePartySize(s: PartySettings, size: number) {
  requireThat(s.mode === "TeamRace" || s.mode === "BrawlRace" || s.format === "Solo", "FORMAT_NOT_SUPPORTED");
  requireThat(size >= 1 && size <= Math.min(4, capacity(s)), "PARTY_TOO_LARGE");
  requireThat(!(s.mode === "FreeForAll" || s.mode === "Freestyle") || !s.fill, "FILL_NOT_SUPPORTED");
}
export function teamFor(m: Match, a: Attempt, invited = false): number | undefined {
  if (m.phase !== "FORMING") return;
  for (let team = 0; team < teams(m.settings); team++) {
    const reservations = m.reservations.filter(r => r.teamId === team);
    const used = reservations.reduce((n, r) => n + r.attempt.members.length, 0);
    if (m.settings.mode === "Freestyle") {
      if (invited && used + a.members.length <= capacity(m.settings)) return team;
      continue;
    }
    if (used === 0) {
      if (invited || !m.privateCode) return team;
    } else if (a.settings.fill && reservations.every(r => r.attempt.settings.fill)
      && used + a.members.length <= capacity(m.settings)) return team;
  }
}
export function logicalFull(m: Match): boolean {
  if (m.settings.mode === "Freestyle") return false;
  if (m.settings.mode === "FreeForAll") return m.reservations.length === 8;
  return Array.from({ length: teams(m.settings) }, (_, team) => {
    const rs = m.reservations.filter(r => r.teamId === team);
    return rs.some(r => !r.attempt.settings.fill)
      || rs.reduce((n, r) => n + r.attempt.members.length, 0) === capacity(m.settings);
  }).every(Boolean);
}
export function publicRoster(m: Match) {
  return m.seats.map(s => ({
    playerId: s.playerId, partyId: s.partyId, partyRevision: s.partyRevision,
    attemptId: s.attemptId, teamId: s.teamId, seat: s.seat,
    connectionSecretHash: digest(s.connectionSecret),
  }));
}
export function seal(m: Match, now: number) {
  m.phase = "ADMITTING"; m.deadline = now + RESERVATION_MS;
  m.rosterHash = digest(publicRoster(m));
}
