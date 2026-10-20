# Freestyle practice

STATUS: SOURCE VERIFIED — NATIVE PRACTICE NOT RUN

Base: `026cd3f`. Root owns Unity/native execution and the current compiler slot.

## Approved contract

Freestyle keeps its shared exploratory world, damage disabled, no match deadline, no winner and no race checkpoint progression. An authenticated player may select one nearby authored physical station for the group. Selection is bound to the exact site hash and host range/line of sight. Race prerequisites do not restrict practice selection. Only the active station's designated recovery supplies are enabled, in addition to ordinary sandbox supplies.

Each station can be completed once per session using its actual authored recipe and existing host physics sensors. Completion and active selection survive checkpoints and host recovery. Fresh sessions reset practice; this increment does not claim repeatable source restoration. Existing collected inventory is preserved when changing stations; inactive loose recovery material is retired through the existing conservation ledger.

A real shared keepsake and exact authored moving fixtures support timed transport practice. Static source geometry and all existing course/home schemas remain unchanged. The 512 body / 8192 canonical-state bounds remain enforced.

The snapshot gains required canonical practice selection data, therefore snapshot schema and canonical version advance to 3; protocol is `wordbound-v3`. Old v2 snapshots are rejected. Online owns service/wire/client protocol coordination; root owns HUD and launcher bindings. Course/home schemas are unchanged.

The required payload object is `practice: { siteHash: "", selectedTick: "-1" }` before selection and in other modes. A selected Freestyle station has its exact authored 64-character hash and a positive decimal tick no later than the completed snapshot tick. Active mechanism evidence must belong to that selected station and cannot predate selection. Solved stations retain real recipe cost and completed physical evidence; one consumed recipe cannot claim multiple station completions.

`PracticePuzzle` uses Target=site.Hash, revision0, slot-1, empty Value. Existing `UsePuzzle` uses Target=selected.PuzzleId and Value=deployed body ID. Shared crafted bodies remain owner -1; actor/evidence remains team0. Selection enables the selected puzzle's declared recovery supplies without race prerequisites, then existing source conservation retires only inactive loose tiles.

Identity changes: only Freestyle gains an extra practice policy hash in ModeDefinition.Hash; other mode hashes remain unchanged. EntityBudget advances to version3, authored physical plan/content prefixes advance to2, and the existing effective-rules fingerprint incorporates the new budget hash in root integration.

## Verification

2026-10-21:

- Separate production Rules reference build: PASS, zero warnings/errors, 18.9 seconds.
- Real NUnit practice: 16/16 PASS. Includes host reach/LOS, later authored-lap identity without race prerequisites, selected-source retirement preserving collected value, strict evidence before solve, durable restore, no race progress/result, one completion per session, forged selection rejection, whole-tick rollback, shared keepsake recovery, and schema3/v2 rejection.
- Related no-build mode/snapshot/budget regression: 30/30 PASS, 43 seconds.
- Authored-content and guide source checks: 17/17 PASS, including all Explore sites, exact hash/recipe/rules bindings, and existing race content regression.
- Installed Unity6000.6.3f1/Fusion reference build of Puzzles, Physics, Gameplay and new guide helper: PASS, zero warnings/errors, 10.6 seconds.
- Rules DLL SHA256: `F641BBAE9C068BC197BE81B8E92565843A56F840E6C244887DA68A7C1DC2EB49`.
- Existing practice test source initially reused gated RaceCircuit; Explore correctly rejected it as `geometry.spawnTrap`. The test-authored document now explicitly removes its race gates. Production validation was preserved. No other test assertion was weakened.

Local evidence: `Logs/freestyle-practice/{practice,regression,content}.trx`, `Logs/entity-lifecycle/rules-bin/Wreckabulary.Rules.dll`, and `Logs/freestyle-practice/reference-bin/ReferenceCheck.dll`. Ignored reproducible harness projects are in this BuildNotes directory. Only one compiler/test process ran at a time. Compiler slot returned to root after the reference build exited.

Native practice and human traversal: NOT RUN. Unit tests supply trusted host observations to verify domain handling; they do not prove collision sensors in Unity. Existing six-family native fixture and root's timeout/stage diagnostics were untouched.

## Root integration seams

1. In `WordboundPuzzleGuidePlan.Build(course, MatchSnapshot, actor)`, dispatch Freestyle to new `WordboundPracticeGuidePlan.Build` before the race-progress overload.
2. HUD may use `Nearest(course, frame, actor)` (default3m), `Completed` and `Status` to display the selection action and honest fresh-session replay label. Selection is still validated by host range/LOS.
3. Offer real TakeKeepsake/DropKeepsake in Freestyle. Initial keepsake is at normal spawn; recovery uses the active station's module anchor.
4. Upgrade clients, service/checkpoint pool and canonical interop fixtures together to protocol/schema3. Do not activate a public catalogue built with old content/mode hashes.
5. Preserve root's existing content preflight, runtime collider cache, HUD/narrative and native fixture diagnostics while cherry-picking.

Next recoverable action: root integrates and performs native practice acceptance; Online regenerates actual C# v3 interop snapshots. After this checkpoint, shared actor/codec/command/validation/damage/physics hooks transfer to Sethy's defensive-equipment lane. No more edits to those shared paths from this lane without coordination.
