# Authoritative match review

STATUS: COMPLETE — THREE CONFIRMED FINDINGS; FIX VERIFICATION PENDING

Lane: `wordbound/authority-review`. Independent reviewer: workshop/assets lane.

Requested source revisions: authority `cd0fdeaccd6279c11c4559b5e7eb638e49dbb346` and snapshot base `0c45d74ca972752ef958c28dde0996e352b80256` in `wordbound-puzzle-race`. Review completed 2026-10-20. Review focuses on atomic rollback, resource conservation, keepsake/fall/disconnect recovery, exact durable checkpoint migration, command access/ownership, mode progression and snapshot validation composition.

Only this report may be edited by the reviewer. No Unity launch, implementation edits or heavy compilation while the integration owner runs native two-peer validation. Bounded in-memory source reproductions are permitted. Implementation-owner follow-up timing/platform/throw-settling and permanent-return changes are separate from the committed slice under review.

## Findings

### A1 — P1: completed-lap recovery timers eventually prevent snapshot publication

At `cd0fdea`, `AuthoritativeMatch.cs:455` skips every resource whose lap differs from `CurrentLap`, without clearing a previously scheduled recovery deadline. `ScheduleSource` at lines 525–534 also permits scheduling an old generation if a leftover tile is collected later. `MatchStateValidation.cs:106` checks every resource record, including previous laps, and rejects any pending deadline at or before the snapshot tick.

Concrete normal-flow reproduction: use the TimeRun proof course, carry the keepsake, collect/craft/deploy the required BED and FAN, solve both ordered checkpoints, finish lap zero, then remain on lap one until the old lap's ten-second recovery timer expires. Continue acknowledging valid snapshots at 60-tick intervals. At tick 602, the next snapshot cannot publish:

```text
lapAfterFinish=1
oldDeadline=602
tick=602
publishError=Recovery source, team, lap or deadline is invalid.

SourceId              Lap RecoveryAtTick
s_BedSupply_0000_t0     0            602
s_FanSupply_0000_t0     0            670
```

The stale timer is never processed after the lap switch, so future publication remains invalid. The two-second unacknowledged window then prevents further advancement. This affects an otherwise valid race that continues beyond the old lap's recovery deadline.

Reproduced without compilation by loading the existing engine-free rules and harness assemblies and driving their real fixture/controller through ordinary commands and trusted test-host positions. Source causality was checked against `git show cd0fdea`; the loaded initial-slice assemblies predate the two final reentrancy/epoch guards, which are unrelated to this resource path. Assembly SHA-256:

- `Wreckabulary.Rules.dll`: `199E1E8811D92054B4CE1D18320D9DA26BEABA84406A74BA9035E1F8E6FC8CC2`
- `RulesHarness.dll`: `02EA0679014623EC9E0DBA788C8CC6FCA2E246873A1B286205282E7E9B29E18A`

Required correction: retire pending recovery work when leaving its lap, preserve historical resource generations, and prevent collecting old-lap leftovers from scheduling a new inactive-lap timer. Add a regression that remains on the next lap past all previous deadlines and successfully publishes/imports a snapshot. The implementation owner independently confirmed the source issue and is addressing it in the follow-up slice.

### A2 — P2: snapshot validation permits missing future-lap supply state

At snapshot base `0c45d74`, `MatchStateValidation.cs:108–112` requires only the current lap's source record after validating each supplied record. The constructor establishes one record for every static source/team/lap, and `AuthoritativeMatch.Synchronize` depends on those pre-existing records when minting the next lap. Missing future records are never recreated.

Concrete reproduction on the existing TimeRun fixture: capture a valid initial authority snapshot; construct a copy retaining only lap-zero resource records; export it through the actual codec to recompute its canonical hash; import it with the unchanged trusted binding, mode, course, catalogue and rules.

```text
OriginalResourceRecords  : 9
RemainingResourceRecords : 3
FutureRecords            : 0
ImportSuccess            : True
ImportError              :
```

This is a structurally incomplete yet accepted recovery candidate. On a later lap, the authority has no matching resource state to iterate or mint. The reproduction demonstrates import acceptance; the later-lap consequence follows directly from the inspected `Synchronize` loop, rather than a second full race simulation. It does not claim an ordinary client can forge a durable host checkpoint.

Required correction: require the exact static source/team/lap key set (all three laps for race modes, lap zero for others), just as mechanism validation already requires its complete static set. Add rehashed-snapshot omission tests for every inactive and future scope. Existing in-memory assemblies and hashes are the same as A1; no compilation or source edits.

### A3 — P2: an inactive live letter tile passes snapshot validation

`MatchStateValidation.cs:169–175` requires a matching economy tile and correct source, but does not require its body to be active. In this authority wrapper, collecting a tile removes both its economy entry and body; no legitimate inactive live-tile state is produced. `Reachable` rejects inactive bodies, while `AuthoritativeMatch.RecoverSource` at lines 505–521 reuses an existing tile with its current `Active` value and subtracts its letter from the missing-letter budget.

Concrete reproduction: copy the valid TimeRun initial snapshot, change one live tile body's `Active` field to false, export through the codec to recompute the hash, then import with all trusted dependencies unchanged.

```text
HiddenTile          : tile:1
HiddenTileSource    : p_SideResources_0000_t0
HiddenImportSuccess : True
HiddenImportError   :
```

The imported state conserves the letter numerically while making it unavailable to collection. For renewable sources, the inspected recovery algorithm also preserves the inactive flag and treats that letter as already present, rather than repairing its availability. This is snapshot validation hardening, not a claim of client access to trusted checkpoint publication.

Required correction: reject inactive live tiles (or define and validate a recoverable inactive-tile lifecycle if deliberately introduced). Add a canonical rehashed tamper regression. The implementation owner confirmed that this state is invalid for the wrapper and is adding the guard.

## Coverage and evidence

Inspected committed source through `git show`, so the implementation owner's subsequent working-tree changes did not alter the reviewed baseline. Only this report was written by the reviewer.

| Boundary | Work actually performed |
| --- | --- |
| Atomic ticks and command batches | Traced `AuthoritativeMatch.Advance`, per-batch capture/restore, outer rollback and reentrancy guards; inspected `CommandJournal` identity/epoch/sequence/payload checks and contiguous retained receipts; inspected regression bodies for failed host queries, conflicting commands and migration replay. No additional confirmed issue in this path. |
| Inventory conservation | Traced craft reservation/cancellation/completion, puzzle letter/item spend, damage-to-tiles, elimination drops, `Economy.Restore` replacement validation, `Audit`/ownership checks and body mappings. Identified A3 where numerical conservation alone leaves an unavailable live tile. |
| Ownership and access | Inspected transport-derived actor lookup; range/LOS; team/protected resource checks; observed item revision; craft recipe allowlist; puzzle ordering/prerequisites/deployed item/creator-team checks; teammate transfer and mode damage policy. Raw host pose, contact, damage and puzzle predicates are trusted inputs, not client-authorized requests. |
| Recovery and progression | Traced fall elimination/respawn, keepsake drop/delayed recovery, disconnect/craft cancel, absolute reconnect lease, lap change and resource generations. Reproduced A1 through a normal completed lap plus delayed publication. |
| Durable checkpoint composition | Inspected publication/hash/stamp identity, exact snapshot sequence/tick/epoch/hash comparison, next-epoch fencing, acknowledgement-driven result confirmation and two-second provisional bound. The durable storage acknowledgement itself is a caller trust boundary, not proof created by this rules class. |
| Snapshot parsing and semantic validation | Read bounded codec/schema/field/counter/float-bit decoding, canonical hashing, economy/progress/journal restoration validators, world/source/mechanism checks and malformed-snapshot tests. Reproduced A2 and A3 using correctly rehashed snapshots. |
| Mode behavior | Read race checkpoint/finish ordering, three-lap keepsake completion, timeout ranking, FFA knockout scoring/protection, non-race state restrictions and result validation. Existing tests cover five mode starts and race/FFA outcomes; this review did not rerun that complete suite. |

No Unity, compiler, package install, service or full test suite was launched. Bounded reproductions ran entirely in process using the already-built initial-slice assemblies listed above. They do not establish that those binaries include the final two guard-only changes; exact committed-source review covers those guards. A first PowerShell lap probe collided with a reserved shell variable name and was discarded; the corrected probe used stop-on-error and produced the A1 output above.

The implementation owner acknowledged A1, A2 and A3 and is changing the owned authority/validation sources. Their follow-up is not independently verified by this report. Continuous mechanism timing, platform motion, thrown-item settling, permanent return, engine movement/combat queries, actual checkpoint durability and native host migration remain separate work.

## Next action

After the implementation owner's fix commit and host-load release, independently rerun the slow-next-lap publication case and rehashed future-resource/inactive-tile rejection cases against the fixed revision. Integration then runs its required rules/native gates. This report does not claim live multiplayer or production acceptance.
