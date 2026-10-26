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

## Optional Unity comparison importer — source in progress

Root generated `build-20261026-a` and requested a separate bounded editor importer. Read the actual integration manifest and passing export-verification receipt: eight exports passed; measured BAT LOD0/1 counts are 1628/924 and SHIELD 1044/548. These are Blender measurements, not Unity import evidence. Root subsequently viewed BAT's three-quarter comparison and rejected its current finish for integration; build-a, source, receipt and images remain preserved. The importer remains optional comparison tooling and is not a style approval or default-artwork assignment.

New owned source: `Assets/_Project/Editor/Online/WordboundPackingClubSetup.cs`. No other editor/runtime/configuration/assembly files are edited. It provides parameterless `Configure` and read-only `Validate` gate entries, plus `ConfigureFrom(string sourceRoot)` for editor callers. `WORDBOUND_PACKING_CLUB_SOURCE_ROOT` optionally selects a new `ArtSource/Collections/PackingClub/V1/build-*` revision inside this checkout; default is build-a. `KitCli` was read directly and accepts only parameterless entry methods, so the environment override supplies later build-b selection without modifying the locked harness.

Source preflight verifies exact item/LOD/export identities and counts, manifest and verification binding, source/export/palette hashes, actual candidate icon PNGs and their original render receipts, palette bindings and catalogue dimensions/held scale/grip. Destination conflicts are checked before creating assets. Only LOD0 FBX, palette and icons are copied into the new PackingClub/V1 art folder; no GLB/LOD1 runtime registration is implied. Unity creates metadata through AssetDatabase imports.

New materials use URP Lit, the shared palette and smoothness .32/.14 (roughness .68/.86). New prefabs are one combined rigid mesh with precisely the two remapped materials, no scripts/physics/lights/cameras/rigs. Native validation reads real mesh vertices, triangles, UV/normal presence, unique socket and the full composed transform. The expected conversion is `swapYZ * BlenderHalfTurnZ * sourceGrip * swapYZ`, preserving the authored FBX root rotation. This formula must be checked by root's native import; the code fails on mismatch rather than flattening transforms to make it pass.

The optional `PackingClubPreview.asset` uses OriginalWreckabulary base artwork and BAT/SHIELD overrides on first creation. Reruns never reset existing profile, material, prefab or importer values; conflicting bytes or invalid assets fail and remain intact. Neither LaunchAssets nor a current/default profile is assigned. This supports a course-wide comparison only; selectable per-player online geometry skins remain separate implementation work.

Required root evidence: R0 compile with root's existing Online.Editor Rules/Presentation references; a verified candidate render set; gate Configure using the chosen source revision; actual imported matrix/bounds/count checks; second Configure/Validate with unchanged asset hashes; intentional edited profile/material/prefab preservation; corrupt/missing source or incomplete export matrix rejected before any asset write. No Unity job, binary import, .meta file or asset was produced in this lane. A source review has been requested from story26 before the scoped handoff.

Importer source review completed by story26: no concrete blocking C# or preservation regression found; bounded source selection, orphan metadata collision handling and no default/authority writes confirmed in source. Staged whitespace check passed. Compilation and native matrix/material behavior remain root checks. Root has requested a separate build-b geometry revision after direct review of all source comparisons: restore SHIELD's angular heraldic outline and add a layered softened rim/panel border; finish BAT's crown, laminations and fuller grip. All build-a artifacts remain preserved.

## Build-b geometry refinement — source only

Directly viewed the actual root build-a BAT and SHIELD three-quarter original/candidate PNGs. BAT's large planar crown and thin grip read less finished than the original, and its material-only laminations do not read clearly. SHIELD's oval outline loses the original six-corner heraldic character and its face has little edge construction. Root separately confirmed the build-a rear strap reads well and should remain unchanged.

Updated only candidate generator geometry and this specification/report for a separate new output folder. BAT keeps the original envelope and .17 m world grip: exponent-three softened square cross-sections, a six-stage rounded crown tapering to a 7 mm cap radius, fuller wrapped grip/knob, two simple cuffs, two conforming timber laminations with real edge thickness, and one small flush cream star. The star uses a center-triangulated cap so its inlay conforms to the convex barrel instead of crossing through it. No extra decorative hardware or noisy texture was added.

SHIELD returns to the exact original six-corner outline coordinates read from the original gold primitive: top (0,.57), shoulders (+/-.23,.50), lower sides (+/-.22,.18), point (0,0), in metres. Corner softening is normalized to the original overall envelope. A layered timber perimeter and narrow rounded cream fabric binding separate the blue panel from the rim; the cream star has a beveled face. Build-a's rear strap path, width and depth are preserved after root's review. The full original composed grip remains unchanged.

Source-derived triangle estimates are BAT 2308/1172 and SHIELD 968/472 for LOD0/LOD1; these are analytic estimates, not a Blender measurement. All existing native budget, manifold, envelope, full grip, material, palette and export checks stay in force. Build-a, its passing receipts and all sixteen renders are unchanged. Root must generate into `V1/build-20261026-b`, verify, then inspect new matched images. Re-rendering build-a requires its preserved copied script, because the current generator hash differs. This revision is not style acceptance and no Unity/default artwork is changed.
