# Gameplay presentation

STATUS: SOURCE VERIFIED; NATIVE ACCEPTANCE PENDING

Scope: new `Scripts/Online/Presentation` and this evidence directory only, on `sethypagna/map-workshop`. Root owns integration, Unity execution, prefabs, scenes, metadata and shared model/character APIs.

Contract inspected: Gameplay composition `9d69fa141bcd88419dbe2f0c63cace5610983bcc`; integration's opt-in `ModelVisual.Spawn(..., wordbound: true)`, imported `WordboundEquipmentCollection`, `PlayerAppearance.Dress`, `Outfit`, and the authored Avatar/letter/environment/item models.

Implementation target: late-bound artwork under each body's `VisualRoot`; canonical world dimensions and ground pivot; separate runner-local held visual; authoritative snapshot outfit presentation without local preference writes; no added physics children. Missing models are explicit failures, never completed primitive substitutes.

Implemented: resolver, owned visual instances, runner-scoped gameplay presenter, existing Generic avatar clip playback, wardrobe visibility preservation, and twenty native asset test cases. Integration details and known presentation limits are in `INTEGRATION.md`.

Verification: initial runtime reference compilation PASS (zero warnings/errors, 6.02 seconds); final runtime plus all native test source compilation PASS (zero warnings/errors, 2.70 seconds). Inputs are the actual installed Unity 6000.6.3f1 and integration's Gameplay/Physics/Fusion assemblies. JSON parsing of both asmdefs and `git diff --check` passed. The installed Unity rules linter scanned 286 files: zero findings in the new Presentation scope; 126 pre-existing/out-of-scope project findings caused its overall exit 1 and were not changed. Native visuals, native test execution, networked held-state rendering and human acceptance are NOT RUN here.

Next action: root integrates the new Presentation directory after Gameplay, imports metadata, wires ConfigureVisual/Bind and terminal failure handling, then runs WordboundPresentationTests and the host/client visual acceptance described in INTEGRATION.md. No Unity process, hand-authored metadata, cloud action or shared catalogue/character edits were performed.
