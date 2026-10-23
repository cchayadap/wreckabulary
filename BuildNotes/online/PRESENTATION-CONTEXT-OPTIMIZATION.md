# Per-frame presentation lookup optimization

STATUS: Native behavioral tests 25/25 PASS; three original native allocation probes failed because Unity Mono returned unavailable zero counters. The byte probes are now moved to a standalone .NET measurement project; new execution is PENDING. Independent bounded source review PASS by recover_input against `c14c202` covered the context implementation. Root owns all compiler, native and performance execution. No network timeout, writer lease, validation or physics policy change is included in this slice.

Confirmed source defect: `WordboundGameplayController.Reconcile` presents every canonical body on every completed host physics step. `WordboundGameplayPlan.Presentation` gets `snapshot.Economy.Items` for each item, but that defensive getter copies the entire item array and every item. This makes N item presentations create N² item copies per step. Tiles also repeatedly clone their full array; exact-body membership and content placement/fixture lookup scan their lists repeatedly.

Implemented correction: capture one private presentation context per exact completed frame, copy each economy view once, index exact body references and immutable content identities, and reuse it for every body in that reconciliation. The public one-body presentation implementation remains unchanged, including nullable dependencies unused by a particular body kind. Only the batched reconciliation opts into the new context. Both paths reject a same-ID copied body because membership requires the exact object reference; context construction/lookup additionally rejects duplicate ledger IDs and missing item entries. Private item copies are never exposed. A new frame creates a new context, so pickup/carrier changes cannot reuse the previous frame's inventory.

All full match validation, authority/epoch fencing, body reconciliation, authenticated input ownership, physics restoration and publication remain unchanged. This removes one source of quadratic allocation; it does not resolve or claim to resolve all full-content cost. Separate source observations include two full `MatchStateValidation.Validate` calls per completed host tick (Reconcile and ApplySnapshot), additional sorted snapshots for Update/PrepareHostPhysics comparisons, and repeated content scans in physical validation. Those are outside this change.

## Verification

The original `WordboundGameplayPlanTests` change gained 12 cases (28 total): parity with the unchanged standalone algorithm for all five modes; nullable-unused-dependency compatibility for player/tile/keepsake/platform; exact reference membership and old/new carrier state across a real pickup with separate defensive-copy references/values; authored moving fixture and connection-platform dimensions; duplicate/missing-ledger rejection; and three bounded allocation probes. No Unity engine object, network or filesystem setup is used by the added cases. After the measurement correction below, this native fixture retains 25 behavioral cases and the three byte cases execute separately under real .NET.

The probe uses 32, 128 and 256 synthetic item bodies, warms each path once, then runs each four times. It compares the retained prior item lookup algorithm against the new context and verifies equal presentation output. New-context construction and its once-per-frame indexes are included in measured allocation/time. It asserts less than half the managed allocated bytes, with no wall-time assertion. It emits `WORDBOUND_PRESENTATION_PROBE` including item count, iterations, actual allocated bytes and elapsed milliseconds. Its ledger is deliberately a bounded presentation fixture, not proof of a valid complete match or network gameplay.

Native gate filter: `WordboundGameplayPlanTests` in the existing `Wreckabulary.Online.Gameplay.Tests` assembly through the native EditMode gate, after the parent's assembled compile. The configured core gate includes Rules/Determinism tests only and cannot execute this gameplay assembly. The later measurement-only move does not alter the kit/core configuration or relocate production source. Compare actual native public tick/input/frame-gap evidence separately, retaining raw probe output before claiming an observed runtime improvement. This lane runs no heavy jobs.

Integrate the bounded commit delta only, never copy entire lane source files over root. The lane retains older surrounding baseline details (including root's later entity budget assertion and Controller changes); this patch does not change those lines. No baseline-copy commit is required. Existing dirty `ModeDefinition.cs` is untouched and excluded.

## Native compile correction

Root's native gate `Logs/gate_test_editmode_20261023T145212Z.log` failed before executing these tests: the real gameplay test assembly cannot assign the internal setters on `ItemInstance.State`, `HolderId` and `Word`. Earlier assembled R0 typechecking did not catch this assembly access boundary. The fixture now uses the real approved pickup operation to change inventory, checks the retained old/new context results and exact body references, and proves repeated public economy getters return distinct copies with equal readable values. No reflection, internal access exposure, production change or assertion bypass is introduced. At that checkpoint all 28 cases and allocation results were still pending; the subsequent result is retained below.

## Native measurement correction

The subsequent real Unity result `TestResults/editmode_20261023T145714Z.xml` ran 28 cases in a 60.8-second gate: all 25 behavioral cases passed, and all three byte probes failed because `GC.GetAllocatedBytesForCurrentThread()` returned zero for both allocating paths. That is unavailable measurement, not zero allocation and not evidence of a behavior regression. Keep this XML and failed history.

Installed `com.unity.test-framework.performance@b6023decb532/Runtime/Measurements/MethodMeasurement.cs` explicitly documents `GC.Alloc` as a count of allocations, not byte quantity, and reads `ProfilerRecorderSample.Count`. Substituting that count or the marker's timing value would change the assertion. Frame-wide byte counters also include unrelated Editor work. The byte gate therefore moves, with root authorization, to `Tools/RulesHarness/PresentationProbe/PresentationProbe.csproj`, which directly compiles the real `WordboundGameplayPlan.cs` and references the existing real Rules project. No NUnit shim, Unity stub, copied presentation implementation, package dependency, production-code move or kit/config edit is used.

The standalone project keeps all three 32/128/256-body cases, four measured iterations, full output parity, equal consumed checksums, and the strict less-than-50% allocated-byte assertion. It includes context construction in the indexed measurement and checks a live 65,536-byte `GC.KeepAlive` control allocation both before and after each comparison. Both measured allocating paths must report positive bytes. All available byte/timing/calibration metadata is printed before assertions, including unsupported-counter failure. Runtime/architecture and `unity-runtime=false` are explicit; this does not establish Unity CPU, native GC, network or Windows player performance.

The [Microsoft API contract](https://learn.microsoft.com/en-us/dotnet/api/system.gc.getallocatedbytesforcurrentthread?view=net-9.0) measures cumulative managed allocation on the current thread, including subsequently collected allocations, and excludes native allocation. The probe uses differences over a synchronous action; it does not report retained heap size.

Run from the exact integration checkout, retaining stdout and exit code:

```powershell
dotnet run --project Tools/RulesHarness/PresentationProbe/PresentationProbe.csproj --configuration Release
```

Expected: three measured cases and `passed=3; failed=0`, or a nonzero exit with the measurement/behavior reason. The native filter now has exactly 25 behavioral cases. Source-only migration is complete; .NET execution and assembled compile are pending root's next gate.

Independent input-lane source review PASS against `24b04fb`: linked source paths and current integration APIs, retained 25 behavioral cases, real unchanged comparator, complete context construction, output parity/checksum, positive controls, strict byte threshold and failure reporting were checked. No compile/run was performed by either lane; native/runtime acceptance remains separate.
# Measured follow-up — 2026-10-23

Root executed the reviewed standalone probe on .NET 9.0.15 / Windows X64: **3 passed, 0 failed, exit 0**. Positive 65,536-byte controls reported 65,560 bytes before and after every case. Four complete passes including context construction allocated 467,680 versus 42,400 bytes at 32 bodies; 6,590,176 versus 149,472 at 128; and 25,763,552 versus 300,448 at 256. Every result satisfies the unchanged less-than-half allocation assertion and full output parity. Exact source/probe hashes and observations are in `presentation-probe-20261023.json`.

This establishes reduced managed allocation in the specified synthetic pure-presentation workload. It is not a Unity frame-rate, whole-game allocation or multiplayer result. Native behavior already passed 25/25; the unsupported native byte-counter failures remain recorded. Native profiling of the physical scenario places presentation at 1,613.030 ms across 3,481 invocations, while validation and PhysicsApply dominate that run; optimizing presentation alone does not complete the performance goal.

