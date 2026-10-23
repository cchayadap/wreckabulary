# Bounded phase measurements

STATUS: SOURCE CHECKPOINT; COMPILE/TEST/NATIVE/QA NOT RUN. Root owns every heavy job.

Approved scope is the timing-only slice of HOST-COST-MEASUREMENT-PLAN.md. Input-flow observations remain unimplemented. Baseline alignment f58c60f4 is excluded from integration; it copies only five required files from root 5feb4e7d, preserving the unrelated ModeDefinition changes and prior logs.

The collector is System-only and compiled only under UNITY_EDITOR || GSK_QA. Nine fixed phases use preallocated per-capture arrays and a bounded 256-entry nesting stack. Thread-local ownership rejects overlapping capture; scopes retain the exact capture object/token and use Stopwatch only when active. Mismatched, cross-thread, unfinished, late, clock-invalid or overflow observations invalidate evidence without replacing gameplay exceptions. Reporting occurs after stop and labels inclusive elapsed time, frequency, generation, unfinished scopes and unknown/invalid values. It is usable in a non-development GSK_QA player without ProfilerMarker.Auto.

Six Rules scopes cover rollback Capture, Synchronize, RetireInactiveResources, complete validation, fresh mutable validation and economy validation. Three Controller scopes cover ObserveHost, the whole prepare callback and FrameChanged subscribers. Original call order, guards, error conversion, callbacks, rollback and distinct controller/physics rules remain. All new timing call sites compile out of normal releases. Disabled diagnostic-capable builds make a null-owner check but read no clock and allocate no scope object.

Remaining checkpoint work: focused collector/semantic parity cases; explicit WORDBOUND_PROFILE_PHASES=1 native and QA startup/ready windows; abandoned QA coroutine cleanup; independent source review. No performance claim before root executes an enabled/disabled comparison on the same workload.
