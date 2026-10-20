import { describe, it, expect } from "vitest";
import { canonical, capacity, digest, logicalFull, teamFor, validatePartySize, fullRacePacking, type Attempt, type Match, type PartySettings } from "../src/model";

const settings = (format: PartySettings["format"], fill = true): PartySettings =>
  ({ mode: "TeamRace", format, fill, region: "asia", protocolVersion: "1", mapPoolVersion: "1" });
function attempt(size: number, fill = true): Attempt {
  return { id: crypto.randomUUID(), partyId: crypto.randomUUID(), partyRevision: "1", members: Array.from({ length: size }, () => crypto.randomUUID()),
    settings: settings("Squad", fill), poolKey: "", channel: "QuickPlay", createdAt: 0 };
}
function match(): Match {
  return { id: crypto.randomUUID(), generation: "1", settings: settings("Squad"), content: {
    kind: "BuiltIn", courseId: "fixture", courseHash: "a".repeat(64), modeHash: "b".repeat(64), manifestHash: "c".repeat(64), catalogHash: "d".repeat(64),
  }, ownerId: "", createdAt: 0, phase: "FORMING", reservations: [], seats: [] };
}
describe("pure allocation rules", () => {
  it("packs all arrival permutations without splitting or reusing a party", () => {
    const permutations = (values: number[]): number[][] => values.length === 0 ? [[]] :
      values.flatMap((value, i) => permutations(values.filter((_, j) => i !== j)).map(rest => [value, ...rest]));
    for (const order of permutations([1, 1, 1, 3, 3])) {
      const result = fullRacePacking(order.map(size => attempt(size)), 4)!;
      expect(result).toBeDefined();
      expect(new Set(result.map(r => r.attempt.id)).size).toBe(result.length);
      for (const team of [0, 1]) expect(result.filter(r => r.teamId === team).reduce((n, r) => n + r.attempt.members.length, 0)).toBe(4);
    }
  });
  it.each([["Solo", 1], ["Duo", 2], ["Squad", 4]] as const)("caps %s teams at %i", (format, expected) => {
    expect(capacity(settings(format))).toBe(expected);
    expect(() => validatePartySize(settings(format), expected + 1)).toThrow("PARTY_TOO_LARGE");
  });
  it("rejects non-race formats, FFA parties and unsupported Fill", () => {
    expect(() => validatePartySize({ ...settings("Duo"), mode: "TimeRun" }, 1)).toThrow("FORMAT_NOT_SUPPORTED");
    expect(() => validatePartySize({ ...settings("Solo", false), mode: "FreeForAll" }, 2)).toThrow("PARTY_TOO_LARGE");
    expect(() => validatePartySize({ ...settings("Solo"), mode: "Freestyle" }, 1)).toThrow("FILL_NOT_SUPPORTED");
  });
  it("does not split a two-person party to fill separate one-person vacancies", () => {
    const m = match(); m.reservations = [0, 1].map(teamId => ({ teamId, attempt: attempt(3) }));
    expect(teamFor(m, attempt(2))).toBeUndefined();
    expect(teamFor(m, attempt(1))).toBe(0);
  });
  it("Fill Off reserves logical capacity and blocks Fill On entry", () => {
    const m = match(); m.reservations = [0, 1].map(teamId => ({ teamId, attempt: attempt(1, false) }));
    expect(logicalFull(m)).toBe(true); expect(teamFor(m, attempt(1))).toBeUndefined();
  });
  it("canonical hashes ignore property order while retaining sequence order and colon boundaries", () => {
    expect(canonical({ b: 1, a: "2" })).toBe(canonical({ a: "2", b: 1 }));
    expect(digest(["a:b", "c"])).not.toBe(digest(["a", "b:c"]));
    expect(digest([1, 2])).not.toBe(digest([2, 1]));
  });
});
