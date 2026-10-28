# Room architecture and furnished arrival draft

STATUS: SOURCE-VERIFIED DRAFT. No live source or asset changes were made by this lane. Unity rendering, collision/navigation execution and visual approval remain pending.

## Confirmed problem

The reviewed actual Solo frame is `Docs/qa/runs/20261026T233412Z-wordbound_solo/solo_timerun_world.png`. Its cream walls, repeated wooden ceiling and empty entry are produced by the current authored map and `WordboundCourseWorld`, not by a missing nearby-object render pass. Parcel Gate is 24 × 24 × 6 metres; its original document has eight starts, one recovery anchor, zero prop zones and zero supplies. `WordboundFlagshipLayout.AddRoom` returns before adding its prop zone for this entry. The remaining destination rooms mostly have one distant corner prop plus their puzzle entities.

## Changes ready to integrate

Copy the nine `.cs` files from `candidate/` to their same project-relative paths after checking `manifest.json` against the two baseline files. Do not copy anything from `probe/` into Unity.

| File | Change |
| --- | --- |
| Scripts/WordboundRuntime/WordboundCourseNode.cs | Adds optional room-style profile and presentation enable switch to world options. |
| Scripts/WordboundRuntime/WordboundCourseWorld.cs | Calls the architectural presentation pass after constructing canonical geometry. |
| Scripts/WordboundRuntime/WordboundRoomStyleProfile.cs | Saved Inspector-editable destination colours, material references, optional original art, inset size and decoration budget. |
| Scripts/WordboundRuntime/WordboundRoomArchitecture.cs | Distinct destination architecture; recessed rounded window/art panels, frames and sills; separates floor tops from ceilings; batches wall dressing. |
| Editor/Authoring/WordboundRoomStyleSetup.cs | Creates or selects a profile without overwriting an existing edited asset. |
| Scripts/Rules/Wordbound/WordboundArrivalVariant.cs | Creates a separate canonical Parcel Hall document with six functional furniture placements. |
| Scripts/Online/Authoring/Editor/WordboundArrivalVariantSetup.cs | Saves/selects an editable local course variant, preserving the original course and all launch/public assignments. |
| Tests/Editor/WordboundRoomArchitectureTests.cs | Three focused Unity tests for geometry/manifest invariants, materials/UVs/disposal, and invalid Inspector data. |
| Tests/Rules/WordboundArrivalVariantTests.cs | Twelve official engine-free cases covering all supported formats, source preservation, deterministic bindings and normal furniture/letter behavior. Automatically included by the existing Rules test assembly and harness glob. |

The visual pass activates only when a valid room profile is assigned or the saved resource profile exists. Missing or disabled profiles retain the previous presentation. Explicit per-build floor/wall/connector material assignments take precedence. The original collision objects, openings, manifests, anchors and authority remain untouched. All added wall details stay inside the original solid wall thickness; they cannot become walk-through floor obstacles. Blue glazed-looking surfaces remain closed windows, with a sill and mullions, rather than implying an open passage. There are no patterned stripe backgrounds.

Packing uses teal plaster, warm floor tones and brass details; Kitchen uses terracotta, cream and mint; Attic uses indigo, warm archive tones and the existing townhouse illustration; Rooftop uses garden green, pale stone and sky blue. Materials and colours are editable. The optional artwork is the existing `Assets/_Project/Art/Generated/Environment/townhouse-print-v1.png`; no generated replacement, source-art modification or new external licence is introduced.

The pass bounds decorative wall pieces to 256 by default (Inspector maximum 512), uses at most three insets per wall piece, and splits combined meshes before the 16-bit vertex limit. It adds no lights, rigidbodies or colliders. Wall batches replace existing wall renderers; floor meshes retain their positions and UVs, with separate material slots for top, underside and edge. Runtime-owned meshes/materials are released; borrowed profile materials and source textures are preserved. Actual frame time, draw counts and peak memory still need native measurement.

## No-code editing and review

1. Run `Wreckabulary.EditorTools.WordboundRoomStyleSetup.CreateOrSelect` once, or choose **Tools → Wordbound → Presentation → Create or select room architecture**. Unity creates `Assets/_Project/Resources/Wordbound/RoomArchitecture.asset` and its own metadata. The function is create-or-verify and will not reset existing edits.
2. Select that asset. Edit its destination palette colours, drag materials or the original illustration, adjust inset dimensions, or disable the presentation. Preview/re-enter Play Mode to rebuild the course with changes. No source editing is needed.
3. Run the existing Inspector authoring setup first if `FlagshipCourse.asset` is missing. Choose **Tools → Wordbound → Courses → Create or select furnished Parcel Hall** (`Wreckabulary.Online.Editor.WordboundArrivalVariantSetup.CreateOrSelect`). This creates `Assets/_Project/Data/Wordbound/Authoring/Variants/ParcelHallArrival.asset` with its own identity `wordbound-parcel-hall-arrival-v1`.
4. Select the saved variant. Click **Apply form edits and save asset** if you changed fields, then **Validate and preview exact seed**, followed by **Play exact preview locally** (or **Play this exact preview locally** in the preview window). Move and rotate its real fixed prop placements using the existing course authoring controls. Its cloned camera, visual profile, item catalogue and rule book references remain assigned. Existing variants are selected and validated, never regenerated over user edits.

Creating the architecture resource changes local Wordbound presentation on subsequent construction. Creating the furnished variant does **not** select it as the game/public default, export a catalogue, change `WordboundFlagshipContent.Version`, or deploy a matchmaking release. Admission to public play needs an explicitly versioned map catalogue and the normal exact-manifest validation/release sequence.

The variant factory preserves edits in the supplied Lost Address copy; it checks the source identity and empty entry, not a release hash. This supports the editable local workflow. A reproducible packaged Solo preset should call it on a fresh canonical `WordboundFlagshipContent.CopyDocument()` and the matching geometry catalogue. Public admission always binds the resulting exact document/manifest hashes, never only its display identity.

## Furnished arrival design

Six actual canonical `Props` zones use original TABLE, SOFA and STOOL assets. These become normal authoritative furniture, with collision, health, carry/throw and letter-break behavior from the existing gameplay path. They are not cosmetic clones. Positions are relative to Parcel Gate centre:

| Fixture | X/Z metres | Purpose |
| --- | --- | --- |
| Sorting table | −8 / −9 | First small letter experiment near the arrival edge. |
| Waiting sofa | +8 / −9 | Recognizable household silhouette opposite the table. |
| Return table | −8 / +9 | Gives the return side an equivalent interactive resource. |
| Return sofa | +8 / +9 | Balances entry and return corners. |
| West stool | −10 / −5 | Small carryable object clear of spawn slots. |
| East stool | +10 / +5 | Counterpart near the opposite outer edge. |

A protected renewable HAMMER supply sits at −5 / −9 metres. Cross-room navigation, all eight starts, central recovery and ramp approaches remain reserved by the canonical resolver. No challenge objective or ending is added by this narrow variant. Its purpose is to introduce the game's object → letters → craft loop physically before the first puzzle room. Later room furnishing, distinct major landmarks, new maps and narrative changes remain separate backlog work; this draft does not claim those are complete.

## Evidence and remaining acceptance

`source-check.json`: semantic C#9 compile passed for Rules, main runtime, Editor, Online.Authoring.Editor, Editor.Tests and Rules.Tests against the existing Unity response files (no native/editor execution). One test helper initially used `CourseResolution` where `ResolvedCourse` was required; fixed before the final green result. Independent review identified missing tangents on normal-mapped batched meshes; the draft now recalculates them and checks finite tangent arrays in its Unity contract test.

`probe-result.json`: eight supported mode/format configurations × 24 seeds = 192 successful resolutions, 1,584 team-scoped furniture placements, repeatable manifests and canonical round trips. Six furniture instances break into exactly their letters once, with conservation checked. The original flagship source hash remains unchanged. An initial probe wrongly expected `ResolvedCoursePlacement.Letters` on `Props`; the actual authority reads furniture letters from the geometry catalogue. The probe was corrected to test that real path rather than changing the canonical representation.

`official-rules-result.json`: `WordboundArrivalVariantTests` ran through the existing unchanged RulesHarness runner and assertion shim, from a draft-only project compiling the candidate source and tests. **12 passed, 0 failed, 32.13 seconds.** These are the same test source files that will be discovered by the normal Rules assembly/harness after integration. The focused command was `dotnet run --project BuildNotes/redesign-20261028/map/probe/OfficialFocused.csproj -- WordboundArrivalVariantTests`. No harness source was changed. After integration, root should use the project-prescribed official core gate rather than the draft substitutions.

Root-owned next checks:

- Run `WordboundRoomArchitectureTests` plus existing `WordboundCourseWorldTests` and traversal tests in Unity.
- Create/save the resource profile and local variant through the menus. Reopen assets and confirm edits persist; preview and play the variant, then open the original course and confirm its source hash remains unchanged.
- Capture actual entry and all four destination angles, both third-person and close camera distances. Compare against the saved Solo frame and original Wreckabulary art. Check panel sizes, repetition, colours, shadows, adjacent wall seams and the lack of wooden ceiling surfaces. Do not call the art approved before looking at these frames.
- Verify every canonical route opening/ramp clearance, hand-held/carrying clearance around the six props, real collision, furniture break/letter collection/recraft, safe return and team isolation. The engine-free checks prove content resolution and economy semantics, not these physical/rendered outcomes.
- Measure draw calls, CPU/GPU frame time and memory for the same map/seed. Exercise repeated preview/build/disposal and confirm no growth in owned runtime meshes/materials.
- Keep the variant local until explicitly included in a new public map-pool release and tested in actual multiplayer.
