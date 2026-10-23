# HostStep timing scope — independent source review

STATUS: SOURCE PASS; compiler/native/player NOT RUN by reviewer.

Reviewed final Input source commit `0bd4c455`, unchanged after the stable two-file review, after excluded baseline `96cc4883`, against root `ac4071d3`: `WordboundGameplayController.cs` and `WordboundPhaseCapture.cs`. Exact delta is one conditional value-type scope in AdvanceHost, appended enum index 15, and PhaseCount 16. No runtime files were edited by this reviewer.

The existing admission/reentry guard remains before the scope. Measurement starts before `handlingStep = true` and covers the unchanged entire try/catch/finally. The original handlingStep reset runs before scope disposal on normal completion, early reconciliation return, and exception. Poll, guards, callback evaluation, Advance, capture, reconciliation, HostAdvanced event, Fail and rethrow ordering are unchanged. No added snapshot/query or await exists. Normal release preprocessing removes the scope; default-disabled Editor/QA captures remain the existing null path. Previous phase indices 0–14 are unchanged.

Counts mean completed calls that passed the initial guard, including calls that later fail authorization, pose validation or reconciliation. They are not inherently successful canonical ticks or durable publications. The elapsed scope includes Poll, pose-ledger checks, ObserveHost, Advance, capture/reconciliation and synchronous HostAdvanced callbacks. It excludes BeforeHostPhysics, PhysX simulation, the preceding pose capture, rendering, waits, and reconciliations occurring elsewhere. Nested phases must not be added to this parent; all instances on the captured thread contribute. It measures inclusive elapsed time, not CPU or FPS.

Existing collector lifecycle coverage is relevant but does not establish this real controller boundary's runtime count. Root's native/player enabled capture must finish with invalid=None/inFlight=0 and actual HostStep observations before drawing a cost conclusion. No behavior test or deadline changed, and no native gate was run by reviewer. Integrate Input source `0bd4c455` only; baseline `96cc4883` must not be integrated.
