# Genuine public mode capacity fixtures

STATUS: SOURCE READY, independent SOURCE PASS — source implementation only; compiler, Unity, gates, core, network and live tests NOT RUN. Root owns execution and integration.

## Scope and baseline

- Integration inspected at `e9908d7cbc923e2af2bf83b798ea8b9219a8d7ab`; owned branch `sethypagna/online` starts at `47f1f8f5`.
- The authorized `Assets/_Project/Tests/OnlineGameplay/WordboundNativePublicEntryTests.cs` is identical in both trees (Git blob `78c1a7faf337cba4c34b66f5d2b7633de4474d77`). No baseline-only synchronization is needed.
- Preserve unrelated dirty `ModeDefinition.cs`, `ProtocolV3/` and checkpoint logs. Write only the existing public-entry fixture and this progress file; no new partial file is currently needed.

## Intended change

Added separate `ExternalPhotonPublicCapacityGameplay` cases for real public TimeRun 4, FreeForAll 8 and Freestyle 8. All nine existing public methods, their category/default behavior and the shared Run limits remain unchanged. A default-false capacity option retains the original two-client/non-race assertion unless the new methods opt in; the new branch requires the selected actual mode maximum. After the unchanged real public Run, the new helper asserts exact signed maximum roster, expected team/seat/party shape and agreement of each live Runner and canonical player roster. The existing full-FFA caller's positional `afterStarted` argument remains unchanged.

The existing preparation already supports these counts: TimeRun creates one four-member party; FFA creates eight one-member parties; public Freestyle creates eight one-member Fill parties. All use QuickPlay, installed catalogue/release/content identity and actual authenticated clients. No private fallback, direct spawn, request retry or deadline/config change is planned.

The service seals FFA/Freestyle at full capacity or after 30 seconds with at least two seats. Existing preparation queues parties sequentially. If actual service timings seal a smaller match before all eight arrive, the exact-count guard must fail; source review cannot establish that the full roster will fit that window. Any preparation change would require a concrete additional-scope proposal, not an implicit timeout extension.

## Verification handoff

Expected new category count: 3. Existing public-entry category remains 9. Use the existing native Editor assembly, `test editmode --cold`, `WORDBOUND_EXTERNAL_GAMEPLAY=1`, `WORDBOUND_EXTERNAL_PUBLIC_GAMEPLAY=1` and the exact current installed public catalogue; no new external opt-in or approval gate. Every new case retains NUnit 600000 ms and shared eight-minute lifetime, plus all existing readiness, movement, command, exact checkpoint ACK and cleanup assertions. These cases establish maximum-roster entry/replication, not full outcomes, every participant's combat or separate-machine capacity.

New exact method suffixes, all under `Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests`:

- `PublicTimeRunAllocatesFourAuthenticatedCooperativePlayers`
- `PublicFreeForAllAllocatesEightAuthenticatedIndividuals`
- `PublicFreestyleFillsEightAuthenticatedCooperativePlayers`

Root execution filter: `--assemblies Wreckabulary.Online.NativeGameplay.Tests --category '^ExternalPhotonPublicCapacityGameplay$'`, via existing `node Tools/kit/gate/game_gate.mjs test editmode --cold --json`, with an outer batch allowance such as 3600 seconds. This outer allowance does not change any internal deadline. The unchanged shared Run moves/submits for one remote client; the new coverage is the complete maximum roster/body/ACK path, not every participant's input/combat.

Static inspection confirms public preparation permits all requested counts, signed-seat `Slot` and canonical `Team`/`Slot` are public getters, and all former `PublicEntry` callers retain the old default. `git diff --check` passed. No baseline commit or new asmdef/meta file.

Independent `recover_input` review of exact source commit `b943a28e498de00939a0ff1530763317aee2cabc`: bounded SOURCE PASS. Reviewer traced current root Run/Prepare, signed mode/party shapes, optional argument and iterator forwarding, assertions before teardown, unchanged movement/receipt/body/ACK checks, and public assembly APIs. No compiler or live execution by either lane. The sequential queue sealing risk and selected-remote-only interaction limit remain explicit. Next: root integrates the source/report chain, runs compilation and the exact three live cases after the current runtime job, and records final XML/cleanup with zero skipped cases before any capacity acceptance claim.
