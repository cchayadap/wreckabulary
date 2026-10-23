# FAN floor-boundary sensing

Status: SOURCE READY; independent online source review PASS for `07ff789` and the two-case fractional-edge follow-up; new tests NOT RUN; prior native candidate remains FAILED; no material assignment. Root owns all compilation/native runs. This lane is based directly on frozen root `d7b5a1b`, independent of pending contact-setup commit `60f7780`.

## Observed run

`TestResults/editmode_20261023T165742Z.xml`: candidate case failed its arrival assertion in 7.375632 seconds. Matching log records all three comparisons before acceptance checks:

- Stationary: no travel, no arrival, final speed/drift zero.
- Candidate wind: maximum forward travel 3.95314026 m; 75 pushes; field exit tick 75; no later pushes; stopped at `(38.9567451,16,-16.29721)`; last-second speed/drift and non-floor proximity all zero; **exact arrival never reported**.
- Inertia: maximum travel 0.902187347 m, arrival tick 33 at Y=16.0000019, then rest.

At wind tick 60 and exit tick 75, Y=15.9999886: the float is about **0.0114441 mm below** the authored Y=16 floor. X/Z cross the target region, but the saved log samples every 60 steps and does not show every point inside its horizontal range. The floor-boundary explanation is strongly supported; the rerun will retain the first horizontal-crossing point and both exact/shared containment results to close that measurement gap. Stop metrics meet the intended thresholds, but those later assertions did not run after the first arrival failure; this is not a passed candidate test.

XML SHA-256: `E93E04260D23A8F0560897E8A98F4006A5ABAAA8F2B4F7BA7131D3AE23DF2D01`.
Log SHA-256: `49D003E4311C729610D587365C87950EEA155E51EC2C5F75C35951D05057A5C6`.

## Production path confirms exposure

`WordboundAuthoredRuntime.FamilyReady` uses exact `Inside` tests for RedirectWind's canonical receiver-start foot position (line 140) and current native arrival foot position (line 146). `Inside` compares all three components directly with authored integer-millimetre bounds converted to float metres. The same helper serves BuildRoute/TimedMechanism player arrival. A foot slightly below the lower plane is rejected even while the actual item rests on the floor.

`WordboundNetworkBody.CaptureHost` and `WordboundPhysicsPolicy.Capture` retain the native float position. `MatchSnapshotCodec` writes and reads the IEEE float bits; no millimetre quantization corrects the boundary. This is an actual production predicate defect, not just the calibration's `Bounds.Contains` assertion. The earlier Y=0 full-run stall had positive foot Y and remains a separate measured friction failure.

`WordboundEntityBudget.ReceiverCandidate` independently applies exact bounds to a crafted, world-state receiver before arming. Tiny lower-plane undershoot can remove its auxiliary-body reservation; callers use that reservation for capacity/recycling and snapshot validation. An already armed mechanism retains its auxiliary ID. No actual recycling from this edge was observed; the callsites establish the exposure.

## Approved bounded semantics

One authoritative Rules helper classifies a **foot point**, retaining the authored horizontal target and upper height. X/Z and upper Y have no added tolerance. Only the lower Y plane permits at most **one authored millimetre** of numerical undershoot. The lower gap is computed using double millimetres against the original integer bound, so subtracting a float epsilon at large world coordinates cannot widen the band. The measured undershoot is roughly 87 times smaller than this cap. This margin is also smaller than the item's existing 10 mm physics contact offset.

This does not use collider-leading-edge overlap, which would complete the STOOL target roughly 0.26 m early. The +0.800 m horizontal foot travel, three-dimensional sensor height, course content, sensor size/hash, protocol, wind strength and same-tick wind requirement stay intact. Native `Inside`, receiver reservation and the calibration call the same helper. The calibration additionally retains raw exact `Bounds.Contains` and first-XZ-crossing diagnostics.

Regressions must cover authored boundary/just inside/tiny undershoot, more than 1 mm below, explicit upper-Y and X/Z rejection, negative-world Y, maximum supported coordinate magnitudes, and non-finite input. Cases use actual representable float points and assert the measured accepted lower gap never exceeds 1 mm. Native candidate and then the full physical route remain required before assigning the proposed contact material.

## Implemented source and next gate

- New `Rules/Wordbound/CourseSensorBoundary.cs`: float-coordinate and WorldVector overloads, explicit finite-input rejection, `FloorContactToleranceMm = 1`. Native X/Z and upper-Y comparisons are unchanged. The lower comparison is `box.Min.Y - (double)y * 1000 <= 1`.
- `WordboundAuthoredRuntime.Inside` delegates to that helper; `WordboundEntityBudget.ReceiverCandidate` retains its existing recipe/origin/state/activity checks and delegates containment. Reservation now uses the same bound-to-metres horizontal comparisons as native sensing, replacing its separate float multiply-to-millimetres expressions.
- `CourseSensorBoundaryTests`: **19 cases** cover floor 0, ±16 m, supported authored Y limits (−256 m and a sensor ending at +256 m), exact representable points immediately inside/outside the 1 mm cutoff, X/Z edges at ±2000 m, upper-Y rejection, all non-finite axes, the recorded solver position and actual internal receiver-reservation eligibility. Two focused cases also exercise ±127 mm X/Z edges and positive/negative upper-Y bounds in both sensing and reservation, rejecting the very next representable float outside each edge. Authored coordinate limits come from `CourseLimits`; they are narrower than the general physics pose limit. Reflection in the reservation test accesses existing internal constructors/setters/predicate without widening runtime APIs.
- Native calibration keeps the original +0.800 m travel requirement, wind/stop/obstacle controls and 300 steps. It now uses the same production containment helper. `WB_WIND_SENSOR` and `WB_WIND_BOUNDARY` retain raw exact containment/arrival tick, first X/Z-crossing position, lower-plane gap, shared result and actual wind push.

Root filters: `Wreckabulary.Tests.CourseSensorBoundaryTests`, then `Wreckabulary.Tests.WordboundNativeWindPhysicsTests.CandidateItemContactReachesWindArrivalAndStopsWithoutObstacles`, followed by `Wreckabulary.OfflineGameplayTests.WordboundNativePuzzleFlow.SixAuthoredFamiliesUseNativeSensorsAndCanonicalCheckpoints` when the material candidate and prefab setup are validated. The older deliberately failing default-contact fixture is unchanged in this independent delta; pending `60f7780` replaces it with actual-prefab regression after candidate approval. Its fixture change will need merging with these added diagnostics/helper calls. No dependency on that pending setup is required for this boundary test.

`git diff --check` passed. No compiler, test, Unity or Blender process was started by this lane; no handwritten metadata or asset changes. Prior contact-setup/docs branch and unrelated untracked log are preserved.

Independent online source review identified a real rounding-normalization difference in the reservation callsite: `.127f * 1000` is greater than integer 127, whereas `.127f <= 127 / 1000f` is true. Root explicitly accepted using the existing native float-space comparisons consistently. This is not bit-for-bit conservation of the old reservation arithmetic and is not an added horizontal/upper-Y margin. The focused cases verify the exact native boundary and immediate next-float rejection in both callsites. Lower-plane authority remains the shared double-millimetre cap.

The independent online reviewer rechecked the stable follow-up and found no remaining concrete source blocker: positive/negative 127 mm upper-Y bounds, both horizontal edges, immediate next-float rejection, the unchanged lower cap and raw native diagnostics are covered. This is source review only; compilation and native acceptance remain pending.
