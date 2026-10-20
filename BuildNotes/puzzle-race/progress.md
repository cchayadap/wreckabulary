# Puzzle race implementation

STATUS: IN PROGRESS

Base: 12842c75608694f36819edd3bd4dc104ece7f79c. Branch: sethypagna/puzzle-race.

Owned by root: new Wordbound mode, match, authority, snapshot and runtime components. Other lanes own Course-prefixed rules, online services and equipment sources. Shared integration occurs in the integration checkout.

First unit: explicit five-mode definitions and pure ordered progression, keepsakes, scoring, recovery and reconnect state. Physics observations are host-only inputs; this rules unit does not authenticate client requests or prove physical reach. Command transactions and complete match snapshots will wrap this unit before runtime integration.

First unit verified 2026-10-20: `node Tools/kit/gate/game_gate.mjs core --json` passed 248/248 tests in 35.7 s, including 23 new mode/progression cases. Evidence: `Logs/gate_core_20261020T103337Z.log`, `TestResults/core_20261020T103337Z.trx`.

Cases cover all five limits, partial human race rosters, format separation, team ownership/combat, culture-independent mode hashes, ordered puzzle/checkpoint gates, carrier-only delivery, independent laps, simultaneous-finish draws, one-time recovery and pickup cancellation, absolute reconnect deadlines across migration, snapshot isolation and foreign-definition rejection, invalid batches without partial mutation, same-tick KO trades versus previously eliminated attackers, score and time limits, and finish-before-timeout ordering.

Native Unity verification is NOT RUN for this unit. A progress snapshot is bound to mode, roster and route; the full authoritative transaction/physics snapshot is still required. Host observations require validated physics and authenticated command processing in the runtime adapter.

Peer review found a route identity ambiguity because legal IDs may contain colons. Replaced delimiter concatenation with length-prefixed structural fields and explicit counts; a cross-route rejection regression passes. Reconnect now expires at `now >= deadline`, matching the coordinator. Follow-up core run passed 249/249 in 22.9 s (`TestResults/core_20261020T103655Z.trx`).

Economy snapshots now preserve the existing gameplay economy rather than introducing a separate letter/craft implementation. Captures contain allocation frontiers, item revisions and destroyed-item tombstones, inventories, reserved craft letters/timing/slot, carried relationships, minted/spent totals and loose tiles. Restore builds and validates a replacement before modifying the live economy, verifies conservation and ownership, and does not replay ItemChanged side effects. Public snapshot read models are copies.

Seven snapshot regressions pass for mid-craft migration, completed-craft replay, allocation frontiers/tombstones, copy isolation, invalid reservations/duplicate ownership, foreign roster/counterfeit letters, and elimination. Latest core run: PASS 256/256 in 24.7 s (`Logs/gate_core_20261020T104101Z.log`, `TestResults/core_20261020T104101Z.trx`). This is an in-memory economy snapshot; protocol/content binding, strict wire codec, command receipts and physical world state still belong to the complete match wrapper.

Next: add the authoritative command journal and exact snapshot acknowledgement; integrate source into the Unity checkout and generate metadata there. Complete route-domain validation before exposing a snapshot wire decoder.
