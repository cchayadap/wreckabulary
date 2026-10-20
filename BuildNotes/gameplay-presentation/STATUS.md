# Gameplay presentation

STATUS: SOURCE VERIFIED; NATIVE ACCEPTANCE PENDING

Scope: new `Scripts/Online/Presentation` and this evidence directory only, on `sethypagna/map-workshop`. Root owns integration, Unity execution, prefabs, scenes, metadata and shared model/character APIs.

Contract inspected: Gameplay composition `9d69fa141bcd88419dbe2f0c63cace5610983bcc`; integration's opt-in `ModelVisual.Spawn(..., wordbound: true)`, imported `WordboundEquipmentCollection`, `PlayerAppearance.Dress`, `Outfit`, and the authored Avatar/letter/environment/item models.

Implementation target: late-bound artwork under each body's `VisualRoot`; canonical world dimensions and ground pivot; separate runner-local held visual; authoritative snapshot outfit presentation without local preference writes; no added physics children. Missing models are explicit failures, never completed primitive substitutes.

Implemented: resolver, owned visual instances, runner-scoped gameplay presenter, existing Generic avatar clip playback, wardrobe visibility preservation, and twenty native asset test cases. Integration details and known presentation limits are in `INTEGRATION.md`.

Verification: initial runtime reference compilation PASS (zero warnings/errors, 6.02 seconds); final runtime plus all native test source compilation PASS (zero warnings/errors, 2.70 seconds). Inputs are the actual installed Unity 6000.6.3f1 and integration's Gameplay/Physics/Fusion assemblies. JSON parsing of both asmdefs and `git diff --check` passed. The installed Unity rules linter scanned 286 files: zero findings in the new Presentation scope; 126 pre-existing/out-of-scope project findings caused its overall exit 1 and were not changed. Native visuals, native test execution, networked held-state rendering and human acceptance are NOT RUN here.

Next action: root integrates the new Presentation directory after Gameplay, imports metadata, wires ConfigureVisual/Bind and terminal failure handling, then runs WordboundPresentationTests and the host/client visual acceptance described in INTEGRATION.md. No Unity process, hand-authored metadata, cloud action or shared catalogue/character edits were performed.

## Native horizontal-center correction

Root's `TestResults/editmode_20261020T134553Z.xml` executed 29 cases: all nine puzzle cases and 17/20 presentation cases passed. SHIELD, SOFA and CLOCK failed the horizontal-center assertion at line 42; their dimensions and floor-height assertions at lines 40–41 passed. The observed offsets were 0.0277424678, 0.0148500502 and 0.0279000103 metres.

The source asset builder intentionally offsets those models' depth centers by -0.030825, 0.0165 and 0.031 metres. The test's 0.9 depth scale predicts all three failures. The resolver had incorrectly synthesized a zero horizontal center from size alone.

With root's explicit scope extension, `Entry.boundsCenter` now stores the exact vertex-bounds center produced by the existing native importer. The resolver fits that full center and size, then recomputes the authored grip in its resulting presentation space. The collection validates that bounds remain finite and grounded; the resolver rejects unpopulated metadata with a reimport message. Models, source geometry, authored Grip_R and shared catalogue dimensions are unchanged. The 0.002-metre test tolerances are unchanged. Three additional held-grip cases cover the corrected items.

Reference compilation including the modified importer/collection succeeded (zero errors, ten existing CS0649 warnings for JsonUtility-populated importer DTO fields, 2.40 seconds). The reference project includes these two current source types alongside the older integration assembly and suppresses only the expected CS0436 duplicate-source-type diagnostic; native compilation has one actual definition of each type. This is a source check, not native execution of the correction.

Root must run `Wreckabulary.EditorTools.WordboundEquipmentImporter.Build` once to generate bounds metadata, then rerun `Wreckabulary.Tests.WordboundPresentationTests` (23 cases). Native correction acceptance remains pending. Local baseline commit `21d1f0c` only records unchanged importer/collection files from integration `b68c3e6`, because this workshop branch predates asset integration; root should cherry-pick the subsequent fix commit only.
