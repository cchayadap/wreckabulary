# Asset expansion — visual direction and acceptance

STATUS: REVIEW COMPLETE; proposed production work is NOT RUN.

Reviewed 2026-10-26 against integration commit `0327e4739c5bfa74c70169f727172eaa70518ca9`. Scope: existing image/receipt/source inspection only. No Blender, Unity, generator, importer or gameplay test was launched by this lane. No live art or original source was changed. The user’s request establishes the direction; it does not make these candidates owner-approved.

## Finding

Keep the original Wreckabulary proportions, playful household identity and readable material families. Add construction detail where a player can see or use it, then improve paint, material separation and controlled lighting. More tiny geometry is not, by itself, better art. The previous FAN construction pilot makes useful mechanical improvements but does not resolve the pale presentation. The older olive/cream replacement changes the original identity too much.

The supplied furniture is a useful foundation for different rooms. Its broad shapes and clear color blocks suit the game. It is not yet a detailed, animated or destruction-ready Wreckabulary collection. Use it as curated source material, not as an automatic replacement for the original library.

## Evidence inspected

All paths below are relative to `wordbound-integration`, except the explicitly marked sibling `../assets` paths. Every listed PNG was opened and visually inspected.

| Evidence | Observation | Limit |
| --- | --- | --- |
| `BuildNotes/equipment-polish/native-originals/original-baseline-2157/original-four-world.png` and its `receipt.json` | Original red-hooded figure, blue/silver fan, gold-accented hammer, wooden bat and green sofa read as one soft, playful family. Silhouettes are distinct. | Unity 6000.6.3f1 PreviewRenderUtility, not gameplay, and marked not visually approved. |
| Same folder, `original-bat-held.png` | Hoodie cuff, mitten, grip and curved bat retain the original character language. The face and pale trim read lighter than the red clothing. | Static held preview; does not establish collision, swing or online behavior. |
| `ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV1/Previews/FAN-detail-original.png` and `FAN-detail-candidate.png` | Candidate adds motor shell, recessed ventilation, tilt supports, fasteners and guard structure. Both images remain light blue, pale ivory and soft silver with weak separation in the light areas. | Matched camera/light setup; this is a rear-biased close-up. The new motor occupies space that exposes blades in the original, so it is not a material-only comparison. No full front/side/held candidate review exists in this folder. |
| Same folder, `build-receipt.json` and render JSONs | Receipt records 16,006 candidate triangles, 68 mesh components versus 11 original components; identical outer bounds and original grip preserved. Both images use 960×960, 64 samples and the same camera. | Recorded construction is not proof of contact quality, animated clearance, runtime draw calls, exported topology or performance. |
| `ArtSource/Collections/WordboundPvP/DetailV2/Previews/FAN-three-quarter.png` | Olive body/blades and cream/gold guard produce a generic retro fan. This loses the original blue/silver/gold identity; base and stand also change the proportions. | Different render setup from the original baseline; cannot attribute the difference to materials alone. |
| `../assets/KayKit_Furniture_Bits_1.0_FREE/contents.png` and `sample.png` | Consistent rounded edges, warm timber, blue upholstery and saturated yellow sofa. Bed/table/cabinet/chair families are immediately readable. | Supplier promotional renders, not imported game evidence. |
| `../assets/Preview.png` | Kenney provides broader kitchen, bathroom, stair, door, office and bedroom variety. Flat, clear forms and coral/blue/wood blocks support room readability. | Supplier overview, not proof that every listed model meets engine or physical requirements. License evidence belongs in the intake report. |

The FAN source GLB was also read as structured data. It has one mesh, three nodes, no skins and no animations. Its blue material has base-color factor `(0.09, 0.35, 0.71)` plus a texture; ivory is `(0.94, 0.88, 0.72)`, gold `(0.91, 0.58, 0.17)`. Those are GLTF material factors, not display-space paint swatches. The source is not inherently a gray asset. The Blender studio uses AgX, a bright environment and three broad area lights. These observations justify a controlled material/lighting comparison, not a claim that lighting alone caused the pale result.

## First pilot: original-based FAN V2

Use a new sibling `ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV2/`. Preserve V1, all original GLBs, the current Unity prefab and all existing material files. Do not regenerate the whole equipment collection.

Inputs already present:

- `OriginalBasedPilotFANV1/Source/FAN.original.glb`, SHA-256 `67bd384322b83617ac801211fb487f6be4e57c9d712f1b294660c13957700cd7`.
- `OriginalBasedPilotFANV1/FAN-original-based-pilot.blend` and `build-receipt.json`.
- `OriginalBasedPilotFANV1/Source/build_wordbound_tool_pilots.py` and its adjacent helper `build_wordbound_original_pilot.py`. The tool builder rejects an existing output directory, checks source hashes, preserves original bounds and grip, and already supports front/side/rear/clay/scale/held views.

Create three explicitly labeled variants, all with the same geometry and camera for the first comparison:

1. Original blue/silver/gold reference.
2. Material correction: rich blue enamel, warm ivory blades, brushed steel support, gold hub, dark recessed vent and rubber feet. Preserve recognizable value separation. Proposed **display-space reference swatches**, requiring correct conversion in the material authoring tool: blue `#2362C3`, shadow blue `#173C74`, brass `#EAB44B`, ivory `#F7E7BB`, rubber `#202A34`. These are starting references, not approved values or instructions to paste sRGB values into linear GLTF factors.
3. Optional fantasy skin, **Storm Courier**: blue enamel, copper coil housing and painted amber chevrons that indicate airflow. Keep the guard, rotor, bearing, yoke and weighted base visibly understandable. Add a contained glow only when powered; do not use emission to hide a weak base material.

First render reference and corrected materials in the existing neutral rig, then in the actual Unity room lighting with fixed exposure. Only after that add or remove geometry. This isolates paint/material, geometry and lighting effects. A sharper front guard is useful only if the blades still read at gameplay distance and fine wires do not shimmer in motion.

Useful construction to retain from V1: motor attached to shaft, yoke attached to stand, recessed vents, guard clips and speed dial. Inspect front/back clearance before retaining the whole 68-object assembly. Join static pieces by material for export when it preserves editing and pivot needs. Object count is not draw-call count; measure imported renderers/material passes.

FAN does not need a humanoid skeleton. Use rigid named transforms for `Base`, `TiltYoke`, `Motor`, `Rotor` and `Guard`; animate rotor around its real shaft, tilt around the two side bearings, and keep guards fixed. Use an anchored local visual hierarchy so network physics does not need to synchronize each blade. Do not change catalogue size, grip, damage, letter cost or held scale as an art side effect.

## Comparison rubric

Score each criterion 0–4: 0 broken, 1 weak, 2 usable with visible defects, 3 good, 4 excellent. Record the screenshots/clip and reviewer observation for each score. No score has been assigned to a newly produced candidate in this review.

| Criterion | Weight | Required comparison |
| --- | ---: | --- |
| Original identity and silhouette | 20% | Original/candidate together at equal scale; front, side, rear, held and a 96-pixel-high gameplay crop. Item must remain instantly distinguishable from nearby props. |
| Paint and material richness | 20% | Fixed exposure, neutral and game lighting; base-color and roughness views. No unexplained pale wash, crushed black cavities or identical metal/fabric/plastic response. |
| Believable construction | 15% | Full rotation plus close-ups of load-bearing joins, pivots, surfaces, thickness and clearance. Fantasy elements still communicate what supports, powers or moves them. |
| Animation and interaction | 15% | Idle/equip/use/drop and relevant locomotion transitions. Examine hand fit, deformation, clipping, pivot motion and floor contact. |
| Gameplay clarity and fairness | 15% | Busy scene, dark area, color-vision/grayscale check and PvP distance. Cosmetic silhouettes cannot imply a larger hitbox, hidden team identity or stronger item. |
| Engine cost and editability | 15% | Imported mesh/material/texture/renderer counts, frame-time comparison, prefab/Animator usability and organized references. |

Candidate promotion requires no criterion below 3, a weighted score at least 3.2/4, and no critical clipping, invalid topology, missing textures, unfair visibility or authoring failure. These are proposed acceptance targets, not measured results or a substitute for James’s appearance review. Do not repeatedly rerender without a named defect hypothesis and changed variable. Preserve each revision and its evidence; stop and reassess the approach after three unsuccessful revisions.

## Physical and technical acceptance

| Area | Required check / proposed starting target |
| --- | --- |
| Units and placement | Compare exported mesh-space bounds to the existing catalogue within its existing 1 mm tolerance; floor contact, positive transforms, original grip and held scale preserved. Check actual vertices, not transformed local AABB estimates. |
| Mesh | No NaN/Inf coordinates, zero-area faces or duplicate hidden shells. Intended hard/soft normals and UV seams verified. Closed volume where needed for collision; decorative open surfaces are explicitly identified. Do not demand manifold topology for every cloth/guard surface without a reason. |
| Material / texture | Paint is dielectric; exposed steel/brass has a distinct metal response; recess darkness is not broad dirty AO. Inspect UV stretching, seams, normal handedness, mip filtering and sRGB/data import settings. A fully painted skin covers every intended visible side, including inside folds and hidden surfaces exposed during motion. |
| Collision | Keep existing gameplay collider contract. Separate visual detail from compound simple collision; never use tiny guard wires as dynamic colliders. Test rest stability, dropping from representative height, side impacts, held contact and a crowded multiplayer pile. |
| Mass / balance | Art changes cannot silently change gameplay mass. For any deliberate mass revision, document game balance separately from a real-world estimate; test center of mass, tipping, inertia, pickup and throw impulse at equal conditions. |
| Moving clearance | Rotor clears both guards through a full revolution; tilt stays clear of neck/yoke at both limits. Review 0/25/50/75/100% clip frames and a continuous clip, not just endpoints. |
| FAN cost | V1's 16,006 triangles is a comparison baseline, not a budget grant. Start with at most 16k LOD0, 6k LOD1 and 1.5k LOD2, then reduce if measured scenes require it. Target at most four visible material families/renderer groups for this item, with moving rotor kept separate. Confirm transitions in motion. |
| Character rigs | Preserve original compatible skeleton/Avatar mapping, bind pose and existing clip names. Validate weight sums, finite weights, sensible influence limits, shoulders/wrists/hips/knees in extreme poses, accessories bound to the right sockets and animated bounds. Clothing must not merely look correct in a T-pose. |
| Character motions | Walk/run/start/stop/turn/jump/fall/land/equip/use/hit/recover. Include one-handed and two-handed holds. Review multiple body/skin/accessory combinations; no new skeleton needed for every cosmetic. |
| Runtime performance | Compare the same build/scene/camera/entity count before/after. Measure CPU/GPU frame times, allocations, material passes and resident texture memory. Isolated editor render times are not gameplay FPS. |

## Curated next batch from the supplied furniture

Start with six source roles rather than bulk-importing hundreds of duplicates: KayKit `table_medium_long`, `chair_A`, `shelf_B_large`, `cabinet_small`, `lamp_standing` and `rug_rectangle_stripes_A` from `../assets/KayKit_Furniture_Bits_1.0_FREE/Assets/fbx (unity)/`. Intake must confirm each exact source and license. Use existing game recipes where appropriate; a decorative prop must not silently create a new spellable item.

Build one readable **courier sorting room**: long sorting table, letter-shelf wall, lockable cabinet, warm task lamps, patterned rug directing the route. Add painted parcel marks, edge trim, handle hardware and restrained wear tied to use. Keep navigation lanes and interactive objects readable; do not cover every surface with scratches. Kenney doors/stairs/kitchen fixtures can extend adjacent rooms once exact files and pivots pass intake.

For a larger map, repeat coherent room kits with distinct landmarks and circulation, not repeated clutter. A warm workshop, blue storm gallery and green rooftop conservatory can share believable furniture construction while differing in light, props and movement. Room size, sightlines, reconnect routes and puzzle visibility must be tested with players; more square metres does not establish better play.

Suggested source-to-engine organization for the integrator: `ArtSource/Collections/<Collection>/Source|Textures|Exports|Previews|Receipts`; imported models/materials/textures under a clearly named `Assets/_Project/Art/Collections/<Collection>/`; authored interactive prefabs under `Assets/_Project/Prefabs/...`. Unity must create metadata and prefab variants. Keep source pack names and licenses with the intake; retain original authoring files and do not copy unreviewed scripts or demo systems into the game.

## Handoff

- [x] Inspect original game, prior FAN pilot/replacement and supplied furniture images.
- [x] Identify exact reusable source/pilot scripts and separate geometry, material and lighting evidence.
- [x] Define a small pilot, practical construction/animation checks and promotion rubric.
- [ ] Produce V2 variants with source/provenance receipts and unchanged-original hashes.
- [ ] Render matched full-object and game-light comparisons; validate geometry/UVs/rigid pivots.
- [ ] Import only the accepted pilot through Unity, preserve catalogue/gameplay contracts, test held/placed/thrown behavior and motion.
- [ ] Produce the six-prop room kit, verify licenses/imports/collision/performance, then expand map variety.
- [ ] Ask James to review an actual playable comparison before calling the art style approved.

This report is evidence and an actionable production specification. It is not a claim that improved meshes, new skins, imported packs or an expanded map have already been delivered.

### Prepared execution artifact

At the integrator's request, `BuildNotes/asset-expansion/fan-v2/build_fan_v2.py` is prepared for root-controlled execution. SHA-256: `39349fc8b450a61a2aa3a288e5a50c3a10d7886d777c990b51c84fd802dd4f86`. Python AST parsing passed (419 lines); Blender execution and output inspection are **NOT RUN** by this lane.

The script pins the reviewed V1 blend and original GLB hashes, refuses an existing V2 directory, keeps a third V1 construction reference for material-only comparison, copies materials before changing them and adds no mesh detail. It prepares exact-identity rigid controls, static validation and selected-only GLB/FBX exports with Blender reimport checks. Build and rendering are separate stages. The default preview budget is 640 pixels, 16 samples and two CPU threads; all five views produce three variants each. This is a bounded diagnostic candidate, not the final painted/optimized FAN. The five material families, 68 mesh parts, missing texture painting, lack of LODs and untested motion remain explicit limitations.
