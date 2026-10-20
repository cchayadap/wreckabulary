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

## Follow-up verification — COMPLETE

Integration owner extended review to committed `4dfce93`, including independent verification of A1/A2/A3 and the new timing/platform/settling/permanent-return boundaries. Bounded pure harness checks are permitted; no Unity or source edits. Exact revision, checks and remaining limits will be recorded here before the follow-up report commit.

Verified revision: `4dfce93b70b224049cabd2817a02906714651e7e`. The worktree was clean at the final source check. Existing real-NUnit test build:

`Tools/kit/gate/CoreTests/bin/0/Debug/net9.0/CoreTests.dll`

SHA-256: `92192E1471EEDA44FB05D82408BE092DEB83F4D5C569F9CEFA92C09912BB9CB6`. The implementation owner's matching gate evidence is `TestResults/core_20261020T115557Z.trx`; this reviewer loaded the existing assembly rather than compiling or invoking Unity.

Independent A1 reproduction rerun: completed lap zero through ordinary recipe/puzzle/keepsake commands, then spent another 601 ticks on lap one with periodic acknowledgements.

```text
Lap              : 1
LapEnteredTick   : 139
Tick             : 740
OldPendingTimers : 0
PublishedTick    : 740
ImportSuccess    : True
Paused           : False
```

Independent rehashed A2/A3 mutations now reject:

```text
futureOmissionRejected=True
futureOmissionError=match-snapshot.json: $: Snapshot must include every static source, team and lap exactly once.
inactiveTileRejected=True
inactiveTileError=match-snapshot.json: $: Live letter tile has no active world body.
```

The fixed source clears inactive-lap timers, prevents old-scope scheduling, requires every static resource scope and lap-specific generation, and rejects inactive live tiles. A1/A2/A3 are verified corrected at this revision.

The reviewer also invoked all 16 existing mechanism/return cases directly from the fresh NUnit assembly: `focusedPassed=16`, `focusedFailed=0`. These are re-executions of implementation-owner tests, distinct from the independently constructed probes above. They cover causal timing/reset and rollback, migrated unsolved phase, platform pose/authority metadata, removed-item timer reset, thrown-item settling/revision, inventory/keepsake return exactly once, respawn cancellation and committed migration, terminal reconnect rejection, and contradictory returned/connected flags.

### A4 — P2: active unsolved mechanism snapshots bypass live timing preconditions

At `4dfce93`, `MatchStateValidation.ValidateMechanisms` validates names, solved state and numeric timing, but an active unsolved mechanism is not required to be in the current objective or have its required deployed recipe item. Lines 248–251 only check that a referenced item exists if the reference is nonempty. `AuthoritativeMatch.ApplyMechanism` enforces current lap/checkpoint, prerequisites, recipe ownership, item state and location for live observations; import does not compose those same guarantees.

Concrete rehashed-snapshot probes: start TimeRun, advance to tick one, leave the player on lap zero with no crafted item, and set BedBridge's unsolved state to `ActivatedTick=1`, `PhaseTick=1`, `ProgressTicks=1`, `ItemEntityId=""`. Import succeeds both for the current lap and for an unstarted future lap:

```text
mechanismLap=0 activeMissingRecipeItemAccepted=True
mechanismLap=1 activeMissingRecipeItemAccepted=True
```

These states cannot be produced by a valid `HostMechanismObservation` for this recipe. They can restore progress for an unstarted mechanism without its physical prerequisite; the ordinary cleanup loop also skips an empty item reference. This is a snapshot consistency defect, not a claim that an untrusted client can publish the durable checkpoint.

Required correction: for unsolved states with `ActivatedTick >= 0`, require the active lap/checkpoint and completed prerequisites; if the puzzle requires a recipe, require a matching active deployed crafted item owned by that team in its module. Preserve the separate rules for solved historical item references, including consumed-item tombstones. Add canonical rehashed tests for future/off-objective activation and missing/wrong/held/rival/out-of-module recipe items.

### A4 fix verification

Verified correction: `f3c50d6` (`Reject impossible active mechanism recovery snapshots`), atop `4dfce93`. The reviewed validator and test files have no working-tree differences from that commit. Validator SHA-256: `F1D11DF5395F5E15C2545A3FED33D0CF16F6124C22DF8228CCEF2C736D63ED5B`. Fresh existing real-NUnit `CoreTests.dll` SHA-256: `72116B157DCF1956A749957C3C48825B28B13FF8A1DDE108A756E19CF883269B`; owner's matching gate is `TestResults/core_20261020T120607Z.trx`.

The independent current/future-lap missing-item probes now reject. Two additional independent BrawlRace probes reference (a) a matching BED still held and (b) a matching BED deployed by the rival team; both reject. Each mutation was exported through the real codec to recompute the canonical hash, then imported with unchanged trusted dependencies.

```text
missingRecipeItemLap0=REJECTED
missingRecipeItemLap1=REJECTED
heldRecipeItem=REJECTED
rivalRecipeItem=REJECTED
focusedPassed=23
focusedFailed=0
```

The 23 focused cases are direct re-executions of the owner's 18 mechanism and five return cases, separate from the four independent mutations. Source review confirms active unsolved states require the current lap/objective, completed prerequisites, and the appropriate active deployed crafted item, team, recipe and module bounds. Solved historical references retain their separate consumed-item rules.

A1–A4 are independently verified corrected at the revisions above. No additional confirmed defect was found in the reviewed platform pose, throw settling, permanent return, rollback and recovery boundaries. This is bounded source and engine-free verification: engine collision/movement, live Fusion authority, actual checkpoint durability and native migration acceptance were not run by this reviewer. Only this report was edited; implementation fixes belong to the source owner and native integration remains with the root agent.
