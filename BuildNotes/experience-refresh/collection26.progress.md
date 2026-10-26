# Packing Club source authoring — collection26

STATUS: IN PROGRESS

Scope: source-only Blender authoring for two cosmetic geometry variants, BAT and SHIELD, in the original Wreckabulary style. Root owns all Blender/Unity/player execution, catalogue bindings, imports and acceptance. No provider jobs or spend, no changes to original artwork, physics, recipes or statistics.

Baseline: `wordbound-map-workshop`, branch `sethypagna/hud-readability`, existing `BuildNotes/flagship-content/focused-first-attempt.log` remains untracked and excluded. Git identity verified as SethyPagna / 139676971+SethyPagna@users.noreply.github.com.

Owned paths: `Tools/AssetPipeline/collections/packing_club.py`, `ArtSource/Collections/PackingClub/V1/spec.md`, this report and `collection26.done.json`. No binaries or metadata are produced in this lane.

Read: project CLAUDE/harness rules, portable core workflow, prior art26 source/visual review and game-asset-production skill. The skill is applied to specifications, provenance and validation boundaries; local `game-dev` is unavailable, so existing AssetPipeline patterns will be followed. Heavy execution remains NOT-RUN.

Next: inspect original GLB bounds, matrices, materials and sockets plus the existing export/render pipeline. Then author a reproducible, non-overwriting script with explicit envelope/triangle/material checks, saved editable source and transparent matched-angle previews.

## Confirmed reference contract and source delivery

Viewed original `docs/art/SUPPLIED_ITEMS_CONTACT_SHEET.png` and `Web/public/art/supplied-showcase.png` directly. The shapes are rounded household objects with pale timber, cream wrap, powder blue and a clear star motif. Read `build_assets.py`, `export_web.py`, existing original-tool pilots, WordboundVisualResolver and ModelVisual; inspected BAT/SHIELD GLB JSON, parent transforms, materials, SHA-256 hashes and the web manifest.

Correction to the prior art26 report: SHIELD's local Grip_R Y .32 composes with the mesh parent Y -.03. Its world Y is .29, matching the catalogue and web manifest. Root received this correction. The new script compares the full imported world matrix, not the raw local translation.

Authored `Tools/AssetPipeline/collections/packing_club.py` with independent build, verify and render commands. No Blender or generator execution occurred. New output must be a child folder of this checkout's PackingClub source root and must not already exist. Script and original inputs are copied/hashes recorded into each generated source bundle. Build retains named editable parts in one blend; temporary export scenes combine each LOD into one rigid mesh and preserve the original Grip_R matrix. GLB uses source/reference orientation; FBX applies the same half-turn convention as the original import pipeline. Reimport verification compares all eight exports and records failures instead of claiming a completed import.

Two geometric designs and lower-detail counterparts are specified: softened square-section BAT with broad laminate strips and continuous cream grip; oval blue SHIELD with rounded timber rim, cream star and one curved rear strap. Two shared material definitions use a single 512px swatch palette. LOD0/LOD1 limits remain 3000/1200 BAT and 1200/600 SHIELD. These are enforced limits, not executed triangle measurements. No new animation, rig, physics, statistics or runtime LOD switch is included.

Matched front/side/rear/three-quarter original/candidate rendering and transparent icons are provided as source logic. Fixed camera/light settings and per-render hashes are retained. Existing output cannot be overwritten with different settings. Alpha/content/border checks are implemented, but no image has been rendered or approved.

Static checks: Python AST parse PASS and `git diff --check` PASS. Blender API compatibility, actual generation, material appearance, counts, topology, export round-trip and native import remain NOT-RUN. An independent source reviewer could not be spawned because the collaboration tool returned agent thread limit; this is a self-reviewed source delivery, not independent approval.

Runtime request to root: additive `(itemId, skinId)` binding for PackingClub model/thumbnail/bounds/grip/held scale; map shared palette materials in Unity; define one identity through shop/held/world/drop/throw and retain Classic/Candy/Arcade. Existing per-course Overrides and Classic-only equipment collection are insufficient. `spec.md` contains bounded commands and import/visual acceptance steps. Root alone schedules Blender and Unity.
