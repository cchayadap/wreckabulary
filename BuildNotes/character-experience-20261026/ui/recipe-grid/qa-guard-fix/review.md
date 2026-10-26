# Recipe guard QA correction independent review

STATUS: COMPLETE — no blocking source finding; native Windows rerun required.

Patch `46b732af8888239bc5b523b1d66f0d90f5c09534acea50f8cd14b37f241cad0e` and candidate `c2d6cbae20138527e2d25ba6f13a1c9152a54b4529537ed1b4c7092402860e4c` independently match the manifest. Exact `git apply --check --whitespace=error-all` passes. C#9 Editor/QA/test parsing has zero errors. The failed Windows run 144544 remains unchanged.

The original assertion cannot succeed: EconomySnapshot.Players builds a new array of deep-copied PlayerInventory values on every getter call. Comparing two such inventories by reference falsely reports an authoritative mutation. WordboundGameplayController.Frame, by contrast, directly returns its retained MatchSnapshot reference. No frame is yielded while the guard injections and assertions execute.

The correction asserts the exact retained snapshot reference, full MatchSnapshotCodec.Export content equality and letter values, while preserving the no-command sequence check and open-menu check. It strengthens useful preservation evidence beyond letter-only comparison without weakening the authoritative-state requirement. Both injected failure paths remain separate: empty HUD inventory and localReady=false. Each check receives its own receipt note and named failure. Finally still restores the exact HUD field reference and readiness value; separate restore notes and assertions make cleanup diagnosable.

All positive-dispatch receipt counts, craft timing, focus-loss checks and equipment/motion assertions remain in place. No runtime gameplay logic, authority, test deadline, capture requirement or Tools/kit code changes. Export equality is deterministic over the retained snapshot here; it does not sample a live timer or manufacture an accepted command.

## Companion icon presentation review

Viewed the actual 144717 workshop equipment-menu screenshot. The 0.6 alpha multiplier visibly washes out every unavailable recipe image. Removing only that multiplier is appropriate: recipe tiles remain selectable for inspection, while the selected Need-letters detail and disabled Craft button convey availability. Original image colors should remain intact. No texture recoloring or loss of the craft eligibility guard is needed. A new actual screenshot must verify the resulting vividness; this review does not claim the modified pixels have already been seen.

Read-only source/image review; no live/native changes by this reviewer.
