# Packing Club — first geometry collection

Status: source authored; Blender execution, export round-trip, Unity import, motion review and owner style approval are **NOT RUN**. This document describes the candidate, not completed assets.

The collection adds two optional equipment shapes while preserving Classic. Its reference is the original Wreckabulary household language: soft outlines, pale timber, cream cloth, powder-blue paint and clear useful construction. No distressed metal, generic medieval trim, noisy surface aging, paid generation or new gameplay statistics.

## Two forms

| Item | Shape and details | Editable source parts | Hard triangle limit, LOD0 / LOD1 |
| --- | --- | --- | --- |
| BAT | Narrow round grip rising into four softened barrel flats and a rounded-square crown. Two broad timber laminations run up the barrel; the cream grip is one continuous seven-turn raised wrap. A rounded knob closes the base. | BarrelAndHandle, RoundedKnob, ContinuousCreamWrap, original Grip_R transform | 3,000 / 1,200 |
| SHIELD | Shallow oval blue board with a rounded timber perimeter. A cream five-point star projects from its front. One curved rear strap connects back to the board and preserves the original hand location. | OvalBoardAndTimberRim, InsetCreamStar, BentBackStrap, original Grip_R transform | 1,200 / 600 |

LOD1 is a separately exported source option, not an implemented distance switch. Both resolutions retain the exact envelope and socket. Source parts stay separate for editing; each exported level combines them into one rigid mesh, at most two materials. The entire collection shares one 512 × 512 five-swatch palette. No extra rig, bone, light, particle or per-object behavior is required. These budgets are per source model; they do not prove runtime frame rate or draw count after import.

## Preserved measurements

The generator reads actual reference vertices and the **composed world socket matrix** after GLB import. It checks each candidate against that envelope with a 0.00001 m tolerance, and also checks the world socket against the item catalogue. It never uses the raw child node translation as the world grip.

| Contract | BAT | SHIELD |
| --- | --- | --- |
| Canonical gameplay size, metres | .172 × 1.018 × .172 | .46 × .57 × .1877 |
| Original exported visual size, metres | .172 × 1.018 × .172 | .46 × .57 × .187645, rounded manifest value |
| Composed game-space Grip_R, metres | (0, .17, 0) | (0, .29, -.11) |
| Held scale | .5894 | .6316 |

The shield's child socket has local Y .32 and its mesh parent Y -.03; together they produce .29. This resolves the earlier art report's apparent mismatch. The shield's original visual depth differs from the rounded gameplay depth by approximately .000055 m. The candidate preserves the measured original envelope, so the existing fit receives the same small scale factor.

Original input hashes:

- `Web/public/art/items/BAT.glb`: `f79c6a1465e6b1627953dc4baab9ef7a83b0cabd57b5c3ba8dbee11fc027ab15`
- `Web/public/art/items/SHIELD.glb`: `c52b3c7c23f5cdd664bba168e11b1c803bfed57aee9cdd6a4b049f0fb58f5552`

The script fails if these bytes change. Reference meshes are imported for measurements and matched comparisons, not overwritten or replaced. New source geometry is local parameterized authoring; no new third-party license or provider job is introduced. The original reference rights remain those of the existing owner-provided project.

## Reproducible generation

Root schedules these commands serially with the project's existing Blender installation. `OUTPUT` must be a **new** child folder under this checkout's `ArtSource/Collections/PackingClub`, for example `V1/build-20261026-a`. `build` refuses an existing folder, including an incomplete previous attempt. It neither writes under Assets nor generates metadata.

```text
blender --background --python Tools/AssetPipeline/collections/packing_club.py -- build --repo REPO --output OUTPUT
blender --background --python Tools/AssetPipeline/collections/packing_club.py -- verify --repo REPO --output OUTPUT
blender --background --python Tools/AssetPipeline/collections/packing_club.py -- render --repo REPO --output OUTPUT --size 512 --samples 64 --threads 6
```

Generated structure:

```text
Source/PackingClub.blend
Source/packing_club.py
Source/original-BAT.glb
Source/original-SHIELD.glb
Exports/{BAT,SHIELD}.{fbx,glb}
Exports/{BAT,SHIELD}_LOD1.{fbx,glb}
Exports/PackingClubPalette.png
Previews/<item>-<view>-<original|candidate>.png
Previews/<item>-<view>-<original|candidate>.json
Icons/{BAT,SHIELD}.png
manifest.json
export-verification.json
```

The saved blend retains original reference collections, separate candidate LOD0/LOD1 collections, named mesh parts, socket empties, packed palette, lights and a preview camera. Existing generated output is preserved. Render resumes only when the saved blend, script, settings and already rendered image hashes match; it cannot overwrite a different render. A source edit should be saved as a new revision with a new evidence record.

The Blender source and GLB share the original reference basis: +Z up and front -Y in Blender; +Y up and front +Z in GLB. The FBX exporter applies a temporary half-turn about Blender Z, then exports -Z forward / Y up, matching `build_assets.face_unity_forward`. Keep the imported FBX hierarchy's transforms in Unity. Round-trip verification reverses that half-turn only when comparing FBX back to the Blender/GLB source basis.

## Checks and integration handoff

Build validates original hashes, catalogue/world socket agreement, world envelope, closed positive-volume geometry, degenerate triangles, UV presence, triangle budgets and material counts. Export verification reimports all eight deliverables, compares envelope and full socket matrix, checks one combined mesh per export and excludes rigs, cameras and lights. Geometry is welded only in a temporary validation mesh to account for UV/normal seams; neither source nor delivery geometry is silently modified by that check. Failed round-trip checks are written as FAIL in a retained receipt.

Render produces original and candidate images at identical camera matrices, orthographic scale, lighting and sample counts in front, side, rear and three-quarter views. Icons are exact copies of the candidate three-quarter render. Alpha checks require opaque geometry, transparent background and clear image borders; they do not replace human inspection. No price, name or statistics are baked into images.

Root must add a presentation-only `(itemId, skinId)` binding for `PackingClub`, with the actual imported model, palette materials, thumbnail, measured bounds and original socket/held scale. It must resolve consistently in shop preview, equipped, world, dropped and thrown states, with a defined Classic fallback. Canonical collision and combat definitions remain separate. The current per-course visual override is not a per-player skin store, and the current Classic equipment collection cannot alone provide this feature. Preserve all existing Classic/Candy/Arcade choices.

Import via Unity editor tooling, generate native metadata there, map `PackingClub_Surface` and `PackingClub_Wrap` to the palette with roughness .68/.86, validate positive transforms and material references, and verify the imported FBX's actual size/grip before registering anything. Rendering lower-detail exports does not enable LODGroup switching by itself.

Required visual acceptance: original/candidate source comparisons; actual Unity shop card and preview; held idle/walk/run/swing or shield raise; drop, throw and re-pickup; remote-player appearance; eight-player scene performance. Review contact, reach readability, clipping and palette at the real camera. Source generation alone establishes none of those outcomes.
