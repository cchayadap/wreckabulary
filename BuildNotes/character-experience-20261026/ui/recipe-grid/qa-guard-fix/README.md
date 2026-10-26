# Equipment QA snapshot-copy assertion repair

STATUS: SOURCE FIX FROZEN; rebuild and native retest pending.

One project-owned QA file changes. Patch SHA-256 `46b732af8888239bc5b523b1d66f0d90f5c09534acea50f8cd14b37f241cad0e`; candidate `c2d6cbae20138527e2d25ba6f13a1c9152a54b4529537ed1b4c7092402860e4c`. Strict patch applicability passes; installed CSharp9 Editor/QA syntax reports zero errors. These do not prove the next Windows run.

## Proven cause

`Assets/_Project/Scripts/Rules/Wordbound/EconomySnapshot.cs:16` returns `Array.AsReadOnly(PlayerStates.Select(Copy).ToArray())` on each `Players` access. `Copy` creates a new PlayerInventory and clones its slots, letter bags and optional CraftJob. `WordboundOriginalEquipmentQa.Inventory` invokes that getter each time. Therefore `ReferenceEquals(Inventory, authoritativeInventory)` necessarily fails even when the canonical snapshot and inventory values are intact. The failed guard composite cannot by itself establish a real command or state mutation.

The existing `WordboundEconomySnapshotTests.SnapshotAndPublicReadModelsDoNotAliasLiveState` explicitly mutates an exposed inventory's reservation, slot and CraftJob, then verifies the captured economy restores unaffected. This draft respects that established defensive-copy contract; it does not change it.

Failed Windows run `144544` is preserved. Its checks SHA-256 is `a3f25348b2d885ee210c75e7882d445971928f65c23018e7d3b5d217d254c400`; equipment_open and equipment_world passed, equipment_bat failed at that guard composite, and later dependent steps failed because BAT never completed. No failed evidence was overwritten or reclassified as passed.

## Replacement proof

Capture the actual MatchSnapshot reference once before the synchronous selection/guard operations, plus its full canonical `MatchSnapshotCodec.Export` string and letter values. After each fault-injected deferred Craft, require unchanged local command sequence, menu still open, the same canonical snapshot reference, identical complete serialized snapshot content, and unchanged authoritative letter values. There is no yielded frame during either injection, so the stable snapshot reference assertion is appropriate here.

Each guard records named booleans and before/after sequence values before throwing a distinct failure for submission, menu closure, snapshot replacement, content mutation or letter mutation. The temporary HUD inventory pointer and readiness flag still restore in `finally`; restoration is recorded separately and checked separately. These are temporary QA injections, not edits to authoritative inventory.

No gameplay source, command acceptance, timeout, capture, original equipment, motion, cleanup or exact one-received/one-accepted command assertion is removed or relaxed. Root must rebuild and rerun the existing equipment scenario before positive grid Craft can be marked passed. Independent source review was requested from visual_plan.
