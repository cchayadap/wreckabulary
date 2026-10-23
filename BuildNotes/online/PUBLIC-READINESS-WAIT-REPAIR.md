# Public body/readiness wait correction

STATUS: SOURCE FIX, COMPILE/NATIVE NOT RUN. Scope is one native fixture predicate; no production behavior, pause policy, timeout or assertion removal.

Root native 223028 on 32306015 completed 58/60: 57 OnlineTests passed (including 17 bootstrap barrier cases), real host-departure case passed, public Brawl/Team failed the immediate `Assert.IsFalse(peer.Controller.IsSimulationPaused)` at Run line 142. All three initial-start observers had already recorded exact bootstrap imports/ACKs at tick 0. This is not evidence that the initial bootstrap barrier failed.

Evidence: `TestResults/editmode_20261023T223028Z.xml` and matching log. The public body-only wait returned after 3.591 s for Brawl and 3.932 s for Team. Their hosts had canonical ticks 145/137 and paused=true, while session readiness, live Runner, isolated scene and world remained present and session/controller fault strings were empty. Client canonical frame remained 0. The tests therefore reached an ordinary body-membership condition while the earlier readiness observation was no longer current.

Source explains why readiness is not monotonic. `Ready` includes `!Controller.IsSimulationPaused`; host pause includes `AuthoritativeMatch.IsPaused`, whose commit window stops advancing when nextTick exceeds acknowledgedTick by 120 ticks (2 canonical seconds at 60 Hz). Full replication and durable commit completion are independent. The trace is consistent with this bounded backpressure; it does not independently record the acknowledgedTick or prove exact cause for every pause.

The existing 25 s body wait now requires every peer to satisfy both `Ready(peer)` and `ExactBodies(peer)` in the same predicate observation. It retains the existing 65 s initial-start wait, unchanged 25 s deadline, fault checks, exact body assertions, immediate not-paused assertions, replica authority checks and later movement/command/ACK deadlines. A permanent pause still fails this 25 s wait; it is not ignored or deferred indefinitely. No extra wait is added. Timing collector startup/ready boundaries remain unchanged.

Follow-up: independent Input source review, root R0, then the exact public Brawl/Team methods with the existing flags/budgets and heap policy chain. Success must include their actual movement, command and exact ACK assertions, not only the repaired waiter. The two prior native failures remain recorded; no rerun was performed here.
