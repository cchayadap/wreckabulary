# Original Wreckabulary artwork default

STATUS: Source implementation complete; independent source review PASS; compiler/native verification NOT RUN. Root owns execution. No original model, material, grip, collider, avatar, skin, asset or meta file was edited, and no Unity/compiler job was started in this lane.

## Implemented behavior

- [x] `WordboundVisualProfile.BaseArtwork` is an ordinary Unity enum dropdown: **Original Wreckabulary = 0**, **Wordbound Equipment = 1**. New profiles initialize to zero; the existing `Presentation.asset` omits the field and requires no YAML rewrite. Invalid enum values fail profile validation.
- [x] The resolver uses the collection only when its existing `useWordboundCollection` flag is true and a supplied profile explicitly selects Wordbound Equipment. A null profile retains the prior flag-only behavior.
- [x] Custom per-item Overrides retain priority over either source. ModelLibrary, item dimensions/grip/held-scale data, avatar/letter mappings, skin selection and physics are unchanged.
- [x] The guide identifies `Art/Imported/Items` plus ModelLibrary as the original game artwork, and distinguishes it from both optional Wordbound V1 and DetailV2. All revisions remain available.
- [~] Actual imported-mesh, bounds and grip verification is implemented but awaits native execution.

The source choice is captured when constructing the resolver. Recreate the playtest after changing Base Artwork; existing model instances are not rebuilt live. Original models continue through the existing `ModelVisual` skin and TactileMaterials handling; this does not promise raw imported material bytes are used unchanged at runtime. The change selects base 3D models and does not alter icon lookup or redesign other UI.

## Verification scope

`WordboundPresentationTests` now has 48 cases by source count:

| Cases | Coverage |
| ---: | --- |
| 12 | Load the actual assigned `Presentation.asset`; require the original default; spawn every original BAT/BROOM/HAMMER/SPEAR/SHIELD/PLATE/TABLE/SOFA/BED/STOOL/FAN/CLOCK model; compare imported mesh references, read real vertices, verify canonical fit/grounding, grip contact, held scale and no physics components. |
| 12 | Explicit profile opt-in to Wordbound Equipment; compare actual collection meshes, fitted vertices and retained collection materials. |
| 4 | Actual mesh selection for explicit collection opt-in with the caller flag enabled/disabled and null-profile legacy behavior. |
| 1 | New profile and serialized Inspector enum defaults/labels; reject an unsupported enum. |
| 8 | Per-item and custom-key overrides retain their own materials/bounds/grips under both artwork choices. |
| 11 | Existing held artwork, non-item mappings, wardrobe and missing-mapping checks retained. |

These tests do not substitute object names or mock flags for mesh identity. They do not provide a visual approval screenshot; root still needs the selected artwork checked in the actual Unity/Windows view.

Independent review by `/root/recover_input` confirmed the zero-valued default, enum validation, combined caller/profile policy, override priority, null compatibility, actual root Presentation/LaunchAssets binding, 48-case count, vertex/mesh/grip assertions and guide wording. No concrete blocking source finding; native and rendered acceptance remain pending.

Root native command after integrating and passing its assembled compile gate:

```powershell
node Tools/kit/gate/game_gate.mjs test editmode --cold --assemblies Wreckabulary.Online.Presentation.Tests --filter Wreckabulary.Tests.WordboundPresentationTests --timeout 1200 --json
```

Require 48 passed, zero failed/skipped. Inspect actual original equipment front/side/behind in the game camera and verify an explicit optional-collection choice plus a custom override. Do not call the default change visually accepted from source review alone.

Baseline-only `0b8df7c` aligns four source/document files with root `88ba883`; do not integrate that baseline. The final delta is confined to VisualProfile, VisualResolver, the existing presentation test file, this report and `docs/wordbound/UNITY-EDITING-GUIDE.md`. Original dirty `ModeDefinition.cs` remains untouched.
