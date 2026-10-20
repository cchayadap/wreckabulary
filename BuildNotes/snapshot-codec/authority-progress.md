# Authoritative match controller

STATUS: VERIFIED INITIAL SLICE

Owned source: new `AuthoritativeMatch.cs` and `WordboundAuthorityTests.cs` in the shared puzzle-race checkout. Root owns Git mutations and Unity. No shared legacy Economy.cs edits; narrow puzzle-spend methods live in the new file's partial Economy declaration.

The facade implements stable journal batches, identity/range/team checks, trusted host pose and mechanism queries, inventory craft/cancel/drop/throw/deploy/transfer, protected supplies, keepsakes, ordered lap progression, falls, mode damage, reconnect, canonical publication, durable acknowledgement, and atomic recovery into the next epoch. Client-shaped puzzle completion events are ignored; the trusted query must approve a valid recipe object or letter inventory. Physical movement, contacts, wind, balance and timed machinery are supplied by the Unity host adapter; no Unity or real online claim is made here.

Snapshot slice: real NUnit R1 core Wordbound **87/87 PASS**, 16.6 s; evidence `TestResults/core_20261020T111958Z.trx`, `Logs/gate_core_20261020T111958Z.log`. This includes 46 focused snapshot cases.

Initial controller verification: lightweight harness **21/21 PASS**, 1.81 s. Checks all five mode starts, ordered contested pickup and deduplication, team resource isolation, range/LOS, unforgeable pose metadata, craft and puzzle conservation, transactional failure, fall recovery, ACK freeze, exact committed migration, reconnect deadline, damage policy and protection.

Initial two-seat proof-map snapshot sizes (UTF-8): Time Run 11,613 bytes; Team Race 18,820; Brawl Race 18,222; FFA 7,352; Freestyle 5,604. These are initial sizes, not worst-case bounds; receipt/world growth remains bounded by the codec.

Final initial-slice verification: full real NUnit R1 core **412/412 PASS**, 222.3 s, evidence `TestResults/core_20261020T113146Z.trx`, `Logs/gate_core_20261020T113146Z.log`. This includes the complete three-lap delivery, exact result ACK, partial supply recovery and mid-craft migration cases. Two final epoch/reentrancy guards then passed focused real NUnit R1 **27/27**, 197.3 s wall time under heavy host load (test execution 26 s), evidence `TestResults/core_20261020T113701Z.trx`, `Logs/gate_core_20261020T113701Z.log`.

Frozen files ready for root's commit: `AuthoritativeMatch.cs`, `WordboundAuthorityTests.cs`, this file, `authority-contract.md`, and the updated snapshot `progress.md`.

Next recoverable action: root commits the frozen verified slice. Parent approved a subsequent bounded extension for host-only continuous unsolved mechanism timing, kinematic platform pose capture, and thrown-item settling. No new compilation while root's loaded native two-peer test is exiting.
