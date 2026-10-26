# Packing Club source authoring — collection26

STATUS: SOURCE DELIVERED — generation and native acceptance NOT-RUN

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

Handoff at 2026-10-26T02:29:02Z: source commit `de9dcaa2d32c4466e69b1d1c5573428c6ea76b15`; exact three new paths, correct configured SethyPagna identity. Staged diff whitespace check PASS after the new files were staged. The unrelated focused-first-attempt log remains untouched/untracked. Script SHA-256 at handoff: `dd4955bb5564aa41f5c0658a9b5e93470c495b8019de15bcac42e8c0418fd479`. Root can integrate this commit without source baseline commits. Next recoverable action: root reviews source and schedules build into a new `ArtSource/Collections/PackingClub/V1/build-20261026-a` folder, then verifies exports before rendering/importing. No binary, image, runtime skin, performance or owner-approval claim is made.

## Verification hardening after independent review

At 2026-10-26T02:37:55Z, root requested a bounded source repair before any Blender build. Independent source review by story26 identified two P2 verification gaps: incomplete manifest export lists could produce a vacuous PASS, and external palette/material bindings were not adequately verified. This later review supersedes the earlier lack of an available independent reviewer for those findings; it does not establish native execution.

The generator now requires exactly four unique BAT/SHIELD × LOD0/1 records and exactly eight GLB/FBX exports, canonical path names, the correct skin/version/schema, nonzero counts within the specified budget, and original/export/source/blend/palette hashes. Duplicate JSON keys and duplicate item/LOD/format entries fail. Verification requires eight checks and the exact identity set before PASS. Missing or invalid manifest/palette data produces a retained FAIL receipt with expected and checked counts; the manifest and original artifacts remain unchanged.

Every face must reference an existing non-null material slot, and assigned materials must be precisely the two collection identities. Palette verification checks the active output's connected Principled Base Color image, not an arbitrary unused texture node. Each imported image must decode to the same 512px sRGB palette content within one 8-bit channel step. RGB and RGBA storage are normalized to RGBA so equivalent opaque GLB re-encoding is allowed. FBX must resolve to the canonical verified external palette file; embedded GLB images are compared by decoded content instead of filename or compressed bytes.

Only the generator and this report changed for the repair. Root retains responsibility for all Blender/Unity execution. Static AST and whitespace checks are repeated before delivery; actual shader graph/importer compatibility and all asset outputs remain NOT-RUN.

Independent follow-up from story26 confirmed both requested findings closed in source and found no concrete new false-failure issue. The added per-mesh guard also rejects duplicate or unused slot aliases, not just duplicate material names. This is a source review only; root must still run build and round-trip verification.
