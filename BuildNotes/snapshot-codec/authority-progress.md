# Authoritative match controller

STATUS: VERIFIED FOLLOW-UP; READY FOR INTEGRATION

Owned source: new `AuthoritativeMatch.cs` and `WordboundAuthorityTests.cs` in the shared puzzle-race checkout. Root owns Git mutations and Unity. No shared legacy Economy.cs edits; narrow puzzle-spend methods live in the new file's partial Economy declaration.

The facade implements stable journal batches, identity/range/team checks, trusted host pose and mechanism queries, inventory craft/cancel/drop/throw/deploy/transfer, protected supplies, keepsakes, ordered lap progression, falls, mode damage, reconnect, canonical publication, durable acknowledgement, and atomic recovery into the next epoch. Client-shaped puzzle completion events are ignored; the trusted query must approve a valid recipe object or letter inventory. Physical movement, contacts, wind, balance and timed machinery are supplied by the Unity host adapter; no Unity or real online claim is made here.

Snapshot slice: real NUnit R1 core Wordbound **87/87 PASS**, 16.6 s; evidence `TestResults/core_20261020T111958Z.trx`, `Logs/gate_core_20261020T111958Z.log`. This includes 46 focused snapshot cases.

Initial controller verification: lightweight harness **21/21 PASS**, 1.81 s. Checks all five mode starts, ordered contested pickup and deduplication, team resource isolation, range/LOS, unforgeable pose metadata, craft and puzzle conservation, transactional failure, fall recovery, ACK freeze, exact committed migration, reconnect deadline, damage policy and protection.

Initial two-seat proof-map snapshot sizes (UTF-8): Time Run 11,613 bytes; Team Race 18,820; Brawl Race 18,222; FFA 7,352; Freestyle 5,604. These are initial sizes, not worst-case bounds; receipt/world growth remains bounded by the codec.

Final initial-slice verification: full real NUnit R1 core **412/412 PASS**, 222.3 s, evidence `TestResults/core_20261020T113146Z.trx`, `Logs/gate_core_20261020T113146Z.log`. This includes the complete three-lap delivery, exact result ACK, partial supply recovery and mid-craft migration cases. Two final epoch/reentrancy guards then passed focused real NUnit R1 **27/27**, 197.3 s wall time under heavy host load (test execution 26 s), evidence `TestResults/core_20261020T113701Z.trx`, `Logs/gate_core_20261020T113701Z.log`.

Root committed the frozen verified slice as `cd0fdea`.

Timing follow-up source now adds bounded `HostMechanismObservation` as an optional sixth `Advance` argument. It preserves causal unsolved phases, resets interrupted recipe mechanisms, moves platform poses while preserving authority metadata, and settles sleeping thrown items through the existing economy transition. Added focused tests; not yet compiled because parent is controlling host load.

Next recoverable action: run the focused mechanism/Wordbound gate once root's native two-peer pipeline is launched. Root then commits this coherent slice. Separately approved next slice: permanent Returned state and trusted Return operation with one-time inventory/keepsake drop, no respawn/reconnect, and snapshot persistence.

Parent subsequently requested source-only Return work while the native pipeline runs. Added `PlayerProgress.Returned`, `MatchProgress.Return`, `AuthoritativeMatch.Return`, schema-1 pre-release wire persistence, and five regressions. Return is separate from elimination, drops a pending reservation/held inventory once, disables the physical actor, cancels respawn and cannot reconnect. Coordinator admission must still deny the returned seat.

Independent review of `cd0fdea` identified a historical-lap supply deadline that could expire after the race advanced and make future publication invalid. Follow-up clears inactive-lap deadlines, prevents rescheduling them from leftover tiles, validates that invariant, and makes the complete-race test wait over 600 ticks after its first lap.

After root released the focused-test hold, all current Wordbound regressions passed real NUnit R1 **133/133**, 18.4 s. Evidence: `TestResults/core_20261020T115557Z.trx`, `Logs/gate_core_20261020T115557Z.log`. This verifies mechanism/Return behavior, delayed next-lap publication, exact all-lap resource scopes, future-generation rejection and active live-letter bodies. No new broad seed sweep or Unity launch.

Follow-up source/test paths: `AuthoritativeMatch.cs`, `MatchProgress.cs`, `MatchSnapshotCodec.cs`, `MatchStateValidation.cs`, `WordboundAuthorityTests.cs`, `WordboundSnapshotCodecTests.cs`, `WordboundSnapshotValidationTests.cs`, new `WordboundMechanismTests.cs`, new `WordboundReturnTests.cs`. Documentation: this file and `authority-contract.md`.

Next recoverable action: root commits this verified follow-up and integrates the online reader's Returned/hostMechanisms forwarding changes. Coordinator decisions newer than the committed gameplay snapshot must be reapplied before resuming a migrated host. Parent owns Unity compilation and physical/network acceptance.
