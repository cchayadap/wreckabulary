# Independent compact Play source review

STATUS: COMPLETE — no blocking source finding for bounded native testing.

Frozen patch `4c269f8f539cc480313f0121d80ce8bb0438d8a4a4f8cab45ab01675df7f3d59`, all three candidate hashes and both existing-file baselines independently match. Strict forward `git apply --check --whitespace=error-all` passes. Independent Roslyn C#9 semantic compilation passes Wreckabulary 172 sources/302 references and Wreckabulary.Tests 68/292; main assembly is emitted only to memory. No Unity/player execution or live source edits by this reviewer.

- The exact Practice+WordboundSolo and Workshop+WordboundMaps conditions create the component. Full House games and Home & tutorial panels continue through the original full-page path. No mode/map/seat/callback/scene/gameplay authority code changes.
- The fit component is attached to the disposable Panel. It clamps to the current parent dimensions, anchors top-right, has a reentrancy guard and never recreates choices or dispatches input. Refresh clears the old child panel/component, leaving the page root intact. Existing focus therefore survives a dimension refit; native callbacks still need the proposed regression.
- The existing geometry gives 574 logical units minimum height: 158 header/bottom  + 104 footer reserve  + 44 family strip  + 8 gap  + 260 cards. The 600 cap leaves 26 units; the 580 test leaves 6. At 900 width the two 348-unit cards plus 16 gap fit the 836-unit body, and the fixed queue strip retains separation from PLAY and close controls. These are source geometry checks, not a rendered readability verdict.
- The test loads a fresh Hub after TestScenes.Reset clears Session, then explicitly chooses Practice/Duos/flat and adds its owned second binding. The PlayPage family flags are new per-page fields, not restored settings. `flat` is selected only; no match starts. Both resize loops use the same page root, which Refresh does not recreate. The fixture's UnityTearDown resets Session/scenes even after an assertion, so its page dimensions/owned seat do not leak into following cases.
- Tests cover width/height shrink and grow-back, both compact families, all button bounds and TMP overflow, card/footer separation, focus identity, the two-seat Solo block and restoration of original full house grids. They do not assert physical input, arbitrary width below roughly 860, height below 574, a new window resolution, or subjective image quality.

Root must run the native fixture and inspect actual new 1280×720 Solo/Maps screenshots before accepting the smaller composition. No new scene/material/font/metadata is authored by this draft. Independent evidence: `independent-integrity.json`, `independent-check-source.ps1`, `independent-source-check.json`.

## Revision 2: parent-resize fallback

STATUS: SOURCE REVIEW COMPLETE — native regression remains required.

The first native run passed 9 of 10 tests; its resize case failed because a fixed-anchor child did not reliably receive the parent dimension callback; the 900-width case retained 1100 width. This is a concrete native behavior gap beyond the initial source review. Its failed evidence and revision 1 sources remain preserved.

Reviewed incremental `99c5c909e6c1443e2a014c3e0b1ef77f28ea155aba9df616152891dd71bb92fa`, full patch `7398bc4a7cf26bcb0fdab67e47126cbdb5f859e6da366dc31d3fb0251fc2ce75`, component `aeedbb8ae236d39f3fbf751d563ed7dead4a06bb499d6bfd0b626e69f72b4c92`. Exact candidate hashes and strict incremental apply independently pass. Repeated in-memory Main 172/302 and Tests 68/292 semantic checks have zero errors. The test file is byte-identical to revision 1; no predicate or deadline was weakened.

The component now retains its actual RectTransform parent, last parent size and applied maximum. LateUpdate compares two value-type vectors and calls Fit only on a difference; it does not allocate or recreate UI/focus each frame. OnTransformParentChanged refreshes the cached parent. Caching the parent before the zero-size early return allows a not-yet-laid-out page to recover on a later frame. Existing OnEnable/dimension callbacks and reentrancy guard remain. This is a bounded fallback for the observed missing callback, with no change to house routes, card geometry or input.

Native rerun and exact rendered window comparison remain required; source clearance does not assert the fallback's timing has passed. The original semantic receipt is retained in `independent-source-check-revision1.json`; the latest check is `independent-source-check.json`.
