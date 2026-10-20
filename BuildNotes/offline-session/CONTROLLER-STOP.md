# Controller shutdown lifecycle

STATUS: SOURCE REFERENCE PASS — EIGHT NATIVE REGRESSIONS PREPARED, NOT RUN

Scope: cache one controller shutdown task and route the existing failure-finally path through it. Root observed that a presentation failure could start shutdown, then Return called Stop again and received an already-completed task before the Runner finished.

The controller publishes its task before cleanup or session callbacks can reenter Stop. Every caller receives that exact task, including after completion, failure or cancellation. Existing failure notification stays before the finally-triggered shutdown. Cleanup errors cannot skip session shutdown: the task waits for shutdown before exposing a cleanup failure and preserves both exceptions if shutdown also fails. No Session, MatchClient, admission or gameplay-authority behavior changes.

Eight targeted native edit-mode tests use the actual controller with a deferred session interface implementation. They cover simultaneous callers, synchronous reentry, failure-triggered shutdown followed by Return, shared shutdown error/cancellation, throwing application cleanup, combined cleanup/shutdown errors and an uninitialized controller. They do not claim native Runner/network shutdown proof.

Independent review by Sethy identified that caching a cleanup exception before invoking Session.Stop would make shutdown permanently unattempted. The current source addresses that finding and adds the two cleanup regressions.

Verification: `dotnet build BuildNotes/offline-session/ReferenceCheck.csproj --no-restore --nologo --verbosity minimal` passed against installed Unity/Fusion references with zero warnings/errors in 8.47 seconds. The build includes the actual controller and all eight new native-test sources. `STOP-VERIFICATION.json` pins the source and output hashes. The compiler slot was released directly to James after this one bounded build.

Next recoverable action: root integrates this narrow follow-up after the offline session commit, imports metadata and runs `Wreckabulary.OfflineGameplayTests.WordboundGameplayStopTests`. These eight edit-mode tests require no external-service gate. Root owns Unity execution; the native tests and real Runner shutdown acceptance have not run in this lane.
