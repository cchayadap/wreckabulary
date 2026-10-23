# Body source validation measurement

STATUS: SOURCE CHECKPOINT — independent review pending; compiler/native NOT RUN.

Root's same public workload after the per-call mechanism lookup did not demonstrate improvement: the 210425 run completed 20 collector tests but failed its public movement case. Read integration `BuildNotes/client-view/native-body-lookup-20261023.json` (source `10d8c68a`, XML/log stamp `20261023T210425Z`, 21 total / 20 passed / 1 failed / 0 skipped). Movement ended after 11.054 s. The ready window was valid (`None`, zero in-flight), 11.1796268 s, canonical ticks 226→503. Bodies was 1273.522 ms / 285 calls (4.468 ms/call), versus the earlier 1765.926 / 427 (4.136 ms/call); Full was 2753.003 / 285 (9.660), versus 3994.866 / 427 (9.356). Unequal work windows and machine scheduling prevent a causal speed comparison. Keep the raw failure and zero-error cleanup evidence; do not reinterpret collector passes as gameplay acceptance.

The next approved change only appends `BodySourceValidation` to the fixed phase collector and measures the existing private ValidateSource invocation, which has exactly two call sites in ValidateBodies: Item and Tile branches. It does not change source/resource lookups, validation order, exceptions, gameplay, timeouts, services or SDKs. System-only collector and call site remain guarded by UNITY_EDITOR || GSK_QA; existing explicit opt-in is unchanged. Normal release contains neither site nor collector.

Existing three source files match integration including the latest lookup, ignoring line endings; no baseline copy is required. Extend existing 20 collector tests with exact successful source-call counts and nesting/non-mutable assertions. Root owns all R0/native execution.

## Implemented boundary

The new phase is appended at enum index 14, with PhaseCount 15; all previous indices/names are unchanged. A single conditional value-type using declaration surrounds the existing entire ValidateSource body, including its early returns and throwing Need calls. There are no added queries, captures, callback APIs or policy branches. Full validation reaches it once per successfully checked Item/Tile body. Fresh mutable validation does not call it.

The five existing real-mode parity cases now require a nonempty Item/Tile workload and an exact count equal to those bodies, despite also executing atomic fresh mutable validation. Existing atomic abort/prepare-error/apply-error cases assert the same exact full-only count; the earlier resource rejection requires zero source calls. Native case count remains 20. Public APIs only; no internal setter or test assembly change.

`BodySourceValidation` is nested inside `BodiesValidation`, itself inside `FullValidation`. Its elapsed totals must not be added to either parent or to the five disjoint FullChildren sum. Tests keep that sum unchanged and separately require Bodies total >= Source total. Stopwatch measures inclusive elapsed time for all instances on the captured thread, not CPU, network latency or FPS. Added instrumentation overhead remains part of the enabled measurement; normal release preprocessing removes it, and the existing explicit opt-in/cleanup contract is unchanged.

Root follow-up: R0; retain affected engine-free behavior gates as appropriate; run native `Wreckabulary.Tests.WordboundPhaseCaptureTests` (20) and the unchanged public TeamRace Solo case (1), using the prior flags/cold gate command and WORDBOUND_PROFILE_PHASES=1. Default core projection does not compile these diagnostic tests. Require actual 21-case XML and valid completed captures, preserving failures rather than interpreting missing phases as zero cost.

Local verification: `git diff --check` plus exact API/call-site comparison only. Compiler/native NOT RUN. Next: independent review, then root gate execution. Source validation cost remains a hypothesis until the new measured data arrives.
