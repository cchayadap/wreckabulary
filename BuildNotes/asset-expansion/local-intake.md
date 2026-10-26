# Local asset intake

STATUS: PASS — static intake completed 2026-10-26 after the PC restart. Owner: asset_intake, continuing collection26's preserved draft. This verdict covers inventory, local license text, ZIP consistency and glTF metadata only. Unity import, visual acceptance, animation, physics and production readiness are NOT-RUN.

Scope: `C:/Users/mrkl6/Downloads/Projects/Wreckabulary/assets` and `C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-assets-staging`. Preserve every source. Inventory packages, formats, sizes, license/texture/rig/animation evidence, map/equipment/skin candidates and existing preview paths. Static evidence is not engine, style or production acceptance.

Integration checkpoint: branch `sethypagna/wordbound-integration`, HEAD `0327e4739c5bfa74c70169f727172eaa70518ca9`.

Initial evidence was preserved and expanded into [local-intake.json](local-intake.json). It records every source file's size and SHA-256, every model's glTF metadata, the Kenney ZIP member hashes, texture sizes and candidate transforms. No source files were changed. No scripts from either pack or the staging folder were executed.

Final preservation check at 2026-10-26T10:17:25Z rehashed all 1,985 inventoried files across both roots: zero changed files. All inspected model primitive modes are triangles; no material in the 193 glTF payloads references normal, metallic/roughness, occlusion or emissive texture maps.

## What was supplied

| Source | Distinct model designs | Supplied formats | Local license evidence | Static geometry |
|---|---:|---|---|---|
| `assets/Models` — Kenney Furniture Kit 2.0 | 140 | FBX, GLB, OBJ/MTL, DAE, STL | `assets/License.txt`: CC0; personal, educational and commercial use; credit optional | 12–992 triangles per GLB; 26,737 triangles across all models |
| `assets/KayKit_Furniture_Bits_1.0_FREE` — KayKit Furniture Bits 1.0 | 53 | FBX, separate Unity FBX, glTF/BIN, OBJ/MTL | Its `License.txt`: CC0; personal, educational and commercial use; credit optional | 44–1,018 triangles per glTF; 15,344 triangles across all models |

There are **193 distinct source designs**; alternate formats repeat the same objects. The whole `assets` folder contains 1,875 files / 29,455,634 bytes, including previews and the ZIP. All 1,546 file members of `kenney_furniture-kit.zip` match their extracted counterparts byte for byte. The ZIP is 5,130,729 bytes. There are no executable scripts, DLLs or installers in this supplied folder.

Both packs are inexpensive static geometry, useful for variety, readable silhouettes and room dressing. The viewed supplier previews show KayKit's rounded warm wood, bright yellow upholstery and blue bedding; Kenney offers a broader, more angular household set with coral and blue furniture, stairs, walls, kitchen and bathroom fixtures. Those previews are supplier renders, not evidence of how the assets look in Wreckabulary. Neither pack by itself satisfies the owner's request for intricate hero equipment, fully painted character skins or a new animation collection.

## Material, rig and construction findings

- All 193 inspected glTF payloads contain UV0 and normal attributes. They contain **zero skins, zero animation clips and zero morph-target primitives**. FBX rig contents were not independently parsed; the glTF exports provide no evidence of a supplied character rig or motion library.
- All inspected Kenney GLBs use material color factors with no image textures. Names such as `metal` do not imply a realistic metal shader: the sampled cabinet's metal material has metallic 0 / roughness 1. Map these intentionally to the game's URP materials instead of importing a misleading physical response.
- KayKit uses one shared 1,024 × 1,024 base-color atlas. The five supplied copies are identical SHA-256 `de62db37a80d1801c3d9eb674890ae04b04469e0c149481a51fa250562c628dc`. No inspected material in either pack has normal, occlusion, metallic/roughness or emissive texture maps. Painting surface detail and roughness variation would be new work, not present content.
- Some Kenney models have useful rigid child parts: `doorway` has `door`; `bookcaseClosedDoors` has `doorLeft` and `doorRight`; `chairDesk` has `chair`. These are candidates for hinge/swivel setup, not already animated or physics-tested mechanisms. `ceilingFan` is one mesh/node and needs separation before blade-only animation.
- Scale and origins differ markedly. For example, Kenney `table` spans approximately 0.84 × 0.33 × 0.45 in its local coordinates, while KayKit `table_medium_long` spans 3 × 1 × 2. Kenney's table occupies a positive-X/negative-Z corner origin; KayKit centers X/Z. Normalize against the existing character, hand grips, door clearance and map grid in a wrapper prefab. Do not apply one blind scale to all packs.
- Static mesh counts do not establish watertightness, face orientation, UV padding, LOD quality, stable stacking, collision behavior, break fragments or credible mass. Those checks remain required before use as interactive gameplay objects.

## First useful selection

All paths below are relative to `C:/Users/mrkl6/Downloads/Projects/Wreckabulary/assets`. These are proposed intake candidates, not imported assets.

| Use | Concrete candidates | Why / next work |
|---|---|---|
| Layered routes and shortcuts | `Models/GLTF format/stairs.glb`, `stairsCorner.glb`, `stairsOpen.glb`, `stairsOpenSingle.glb` | Four silhouettes for route variety. Build smooth navigation colliders separately from visible treads; test step height, head clearance, chain snagging and falls. |
| Doors and readable room boundaries | `Models/GLTF format/doorway.glb`, `wallDoorway.glb`, `wallWindow.glb` | Separate door child can support a real hinge. Standardize openings and preserve exit clearance. |
| Archive / lost-property dressing | `Models/GLTF format/bookcaseClosedDoors.glb`, `cardboardBoxClosed.glb`; `KayKit_Furniture_Bits_1.0_FREE/Assets/gltf/shelf_B_large_decorated.gltf`, `book_set.gltf`, `cabinet_medium_decorated.gltf` | Match shelves, books and parcel-like boxes to physical letter recovery. Decorated KayKit cabinet is 1,018 triangles; separate fragile props before allowing destruction. |
| Waiting room / lobby | KayKit `Assets/gltf/couch.gltf`, `armchair_pillows.gltf`, `lamp_standing.gltf`, `pictureframe_large_A.gltf` | Warm recognizable shapes. Add cohesive vivid upholstery/material variants, readable lighting and destination symbols. Keep furniture silhouettes distinct from equipment pickups. |
| Puzzle workbench | KayKit `Assets/gltf/table_medium_long.gltf`, `table_small.gltf`; Kenney `Models/GLTF format/chairDesk.glb` | Stable supporting surfaces and believable equipment assembly context. Test table collision, stack stability, chair swivel limits and interaction reach. |
| Everyday fantasy accents | Kenney `Models/GLTF format/ceilingFan.glb`, `radio.glb`, `televisionVintage.glb` | Good story props; need authored moving parts, response cues and game-specific paint before serving as hero props. |

Recommended import source: KayKit's `Assets/fbx (unity)` where suitable, Kenney's FBX equivalents, with preserved source licenses and wrapper prefabs. This avoids adding a new runtime glTF importer merely for static furniture. This is a workflow recommendation; Unity FBX import and axis/material behavior still need testing.

## Existing staging is earlier work

`wordbound-assets-staging` has 110 files / 35,865,639 bytes. It contains the previous WordboundPvP twelve-item set: BAT, BED, BROOM, CLOCK, FAN, HAMMER, PLATE, SHIELD, SOFA, SPEAR, STOOL and TABLE, each with FBX/GLB exports, plus the editable `Source/WordboundPvP.blend`, concepts, icons, renders and verification records. MAGNET and ROPE exist as future concept images. This is not an additional furniture or character pack. Its source scripts and reports were inventoried without execution; prior verification reports were not reinterpreted as current acceptance.

## Intake boundary and next action

Keep both suppliers' original folders unchanged. Select a small matching room kit, import into distinct source folders, and build game-owned wrapper prefabs/material variants. Compare original and revised versions using the same game camera, exposure and lighting; include close-up, gameplay distance, back/underside and moving tests. Require readability, construction, vivid color separation, collision/weight, deformation where relevant, and measured rendering cost. Do not add skeletons to rigid furniture unless deformation calls for them; use actual joints/pivots for rigid motion and the existing character rig for wearable skin tests.

Next recoverable action: integrate a bounded lobby/archive selection only after the root's Unity recovery check, preserve source hashes, create saved preview scenes and prefab variants, then run import, physics and visual checks. Character skins, skeletons and animation variety remain a separate production task.
