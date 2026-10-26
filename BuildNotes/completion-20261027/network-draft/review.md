# Independent recovery-roster fixture review

STATUS: COMPLETE — source review only.

Scope: read-only review of network-draft/recovery-roster.patch and its exact baseline/candidate, installed Fusion SDK identity contract, existing admission/canonical binding checks. No live edits, native jobs, cloud calls or secret reads. Review does not grant recovery-performance acceptance.

## Verdict

**No source blocker found for native verification.** This is a bounded correction to one native fixture assertion, not a production recovery fix. It removes an unsupported remote-user-ID lookup from client runners while strengthening the native and canonical roster checks.

Verified artifacts:

- Patch SHA-256 `b5ef2f50876d04ea5fd20ae7897a594fe3ec11c8b949113d97e58ff48161acdb`.
- Candidate SHA-256 `1b61e62770732a170ee848721e432485dce07b861b1965c97ede21b4541d6133`.
- Baseline SHA-256 `40e427ad4ebabb1d3c506b0c196dc5a9c361d8c95ee87d4b49000a2b43ed59d6`.
- Independent `git apply --check --whitespace=error-all` passed against the current checkout. No patch was applied by this reviewer. The author's `syntax.json` reports C# 9 syntax with no errors; that receipt is not a new semantic compilation or native pass.

## Contract checks

1. Installed `Assets/Photon/Fusion/Assemblies/Fusion.Runtime.xml`, `GetPlayerUserId` at line 15424, restricts full Client/Server identity lookup to the server. The old loop asked each surviving client to resolve all remote UserIDs. Candidate resolves the full survivor mapping only through the elected replacement server. This matches `WordboundNativePublicEntryTests` lines 101–121 and existing `AssertIdentities` local-identity checks; it does not invent a new identity API.
2. Unique `LocalPlayer` refs are derived from every expected survivor. Each survivor's `ActivePlayers` must be exactly equivalent to that expected set. This retains cardinality and disallows extra/missing native players, including a stale departed host. Every running peer must have the expected server/client role; exactly-one-server verification remains later in the original fixture.
3. The replacement's `TryGetAdmittedActor` is called for every survivor and must equal the expected authenticated actor. The production implementation checks running server state, active admission lease, expiry, SDK authenticated identity, gate identity and lease host. No fake admission or looser fallback was introduced.
4. Canonical identity becomes stronger: every survivor must retain the original verified `RosterHash`, complete `(PlayerId, Team, Slot)` binding and progress seats. The departed host stays in the signed canonical roster and must be disconnected. Every remaining player must be connected and not returned. Tuple types/shape match the already-used capacity assertions.
5. Restored complete body ledgers, first restored moving-fixture frontier, absence of pre-restore authority advancement, exact retained command frontier and single matching receipt are unchanged. Resumed host advancement, fresh post-recovery client command, exact durable acknowledgement and replica-no-authority checks also remain unchanged. No timeout, transport, score, map, service or runtime mutation is part of the patch.

## Remaining verification

Run the existing two-peer graceful control, then four-peer Brawl Duo recovery (where client survivors expose the original fixture error), transient-election/preflight and reconnect cases. Confirm no duplicate or missing player/body/command and original signed roster retention. The local UserID behavior is already used by passing capacity fixtures, but only these new native runs can confirm it after migration.

This source review does **not** establish hard-loss recovery, repeated migration safety, the <=10-second detection target, <=2-second rewind, full-match recovery or separate-machine correctness. The 75-second fixture wait is unchanged and is not the product target. The historical 18.57-second detected-to-ready observation and earlier failures remain evidence; a roster assertion correction must not erase them.

STATUS: COMPLETE — source review only; native outcome pending root.

