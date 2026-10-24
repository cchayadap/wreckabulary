# Gameplay renderer storage review

STATUS: SOURCE PASS — independent bounded source review. Native compilation/execution and allocation measurements NOT RUN by this lane. No source edits.

Scope: fresh hierarchy traversal into reusable `List<Renderer>` storage; callback/reentry ordering; visibility behavior, cleanup and installed Unity API compatibility. Existing focused native test coverage will be read directly; no passing execution is inferred.

## Inspected delta and findings

File: `Assets/_Project/Scripts/Online/Gameplay/WordboundGameplayBody.cs`, exact current SHA-256 `35729e457cc960f42cdf0d4d4f873c46cbc4f34547b031de8d1cd93c1a7a062f`. The root uncommitted change adds the generic collection import and one instance-owned list, replaces the array-returning hierarchy query with `GetComponentsInChildren(true, visibilityRenderers)`, and clears the list after the existing visibility loop.

- The query remains inside every `Apply`, after current `VisualRoot` lookup. It still traverses that root and inactive descendants, so it discovers renderers introduced by presentation changes or later hierarchy changes. This is storage reuse, not a retained renderer/hierarchy cache.
- `PresentationChanged` runs before the list is filled. Its supported reentrant `Configure` completes its own query, iteration and clear before the outer call queries the current root. No outer enumerator is active across that callback. Existing applied tuple fields still update before callback dispatch, preventing unchanged-tuple redispatch exactly as before.
- A presentation callback exception still propagates before the visibility query. No catch now suppresses it, and no visibility writes move ahead of it. Shape validation, authority, tuple validation and callback conditions are untouched.
- Every normal visibility pass assigns the current `Body.BodyActive` to each renderer and then clears references; retained list capacity belongs to this component. If an unexpected native-query/renderer exception occurs inside this block, `Clear` is not guaranteed, but the next pass still performs a new query and does not intentionally reuse those references. No new callback or asynchronous yield occurs during the loop, and no concrete newly introduced exception/lifetime defect was found.
- Installed `6000.6.3f1` CoreModule XML documents `Component.GetComponentsInChildren(System.Boolean, List<T>)` and its inactive-child behavior (lines 5929–5933). The candidate .4 CoreModule was independently found byte-identical in the package diagnosis. The Transform call therefore resolves to an available public Unity API; no SDK or reflection dependency is introduced.

## Existing meaningful coverage and limits

`WordboundNativeLocalGameplayTests.PresentationTupleReusePreservesImmediateShapeCallbacksAndVisibility` routes to the actual isolated spawned-body fixture. Its current source checks unchanged-tuple visibility restoration through `Configure`/`FixedUpdateNetwork`/`Render`, a late nested MeshRenderer, canonical body deactivation/reactivation, exception before visibility, same-tuple recovery, reentrant callback and a fresh spawned component. Those assertions are preserved by this delta. The adjacent `WordboundPresentationTests.WardrobeVisibilitySurvivesBodyRendererRefreshWithoutChangingSavedOutfits` checks that the original avatar's wardrobe uses `forceRenderingOff` independently of enabled-state refresh.

These sources do not establish allocation savings, complete absence of native/internal allocations, or player performance. The list avoids the explicit per-call result array in this C# path; actual runtime comparison still belongs to root. Native tests must pass on the repaired editor before calling this implementation verified. No additional production change is requested by this review.
