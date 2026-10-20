# Wordbound PvP asset production

STATUS: STAGED AND VERIFIED — UNITY INTEGRATION PENDING

Owner: James (SethyPagna). Authorized Wordbound equipment batch, 2026-10-20.

This production collection is isolated from the existing game art and catalogues. It contains the twelve approved equipment concept sheets and editable Blender meshes. No original art, Lunar work, shared catalogues or scenes are overwritten. Its verified staging files were copied into the dedicated `sethypagna/pvp-assets` worktree; Unity integration uses a separate collection importer.

## Production contract

Concepts use the built-in OpenAI image generation tool, one call per asset. Every sheet supplies front, side and three-quarter views of one selected design plus silhouette alternatives. Concepts are visual design references, not game-ready meshes or screenshots.

The subsequent meshes are concept-guided deterministic Blender geometry with editable named parts, not a provider image-to-3D conversion. Source, FBX and GLB exports, icons, previews and independent validation accompany the collection.

Maintain the existing catalogue dimensions, held scales and `Grip_R` positions. Use at most two materials, textures no larger than 512 pixels, and an initial 2,000–6,000 triangle budget per model. All geometry is grounded and measured in metres; the game owns its colliders.

Palette: honey oak, warm cream, sage, muted terracotta and restrained brass. Prioritize readable household-object silhouettes and softly rounded toy proportions.

## Files

- `prompts.json`: exact generation prompts and requested asset facts.
- `provenance.json`: actual returned image paths, copied destinations, hashes, dimensions and inspection status.
- `Concepts/`: original generated image bytes copied without manipulation.
- `References/`: approved project style reference, if used.
- `BuildNotes.md`: progress, decisions, verification and next recoverable action.
- `Source/WordboundPvP.blend`: twelve item collections with named editable components; collection roots are laid out in a four-column working grid.
- `Exports/`: individual FBX/GLB meshes and the shared 512×64 palette.
- `Previews/`: front, side and three-quarter renders of actual meshes.
- `Icons/`: 512×512 transparent renders of actual meshes.
- `manifest.json`: dimensions, authored grips, geometry statistics and production hashes.
- `export-verification.json`: independent Blender reimport checks for both file formats.
- `delivery-verification.json`: complete file, hash, geometry contract and icon transparency checks.
- `visual-review.json`: individual actual-mesh review paths and producer findings.
- `FutureConcepts/`: separate ROPE and MAGNET design studies only; no meshes or working mechanics.

Do not claim an ungenerated concept or unverified mesh is complete. Native Unity validation remains a separate integration stage.

## Reproduce and verify

Run from this staging/project root with Blender 5.2.2 LTS:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python-exit-code 1 --python Tools/AssetPipeline/build_wordbound_pvp.py -- --no-render
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python-exit-code 1 --python Tools/AssetPipeline/build_wordbound_pvp.py -- --verify-only
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python-exit-code 1 --python Tools/AssetPipeline/build_wordbound_pvp.py -- --render-only
node Tools/AssetPipeline/verify_wordbound_delivery.mjs
```

The first command intentionally regenerates this collection's generated source and exports. Preserve manual Blender edits under a separate source filename before rebuilding. `--render-only` checks the saved source hash and does not modify it. No command invokes the old global art builders.

`--only BAT` is an exemplar build and produces an exemplar-only manifest/source. Omit `--only` for the full release collection.

## Design selections and integration

| Item | Selected construction | Triangles |
| --- | --- | ---: |
| BAT | Rounded oak barrel, cream grip wrap, sage collar | 2,724 |
| BROOM | Broad terracotta head, grouped cream bristles, oak shaft | 1,676 |
| HAMMER | Padded head, brass caps, sage-wrapped oak handle | 2,372 |
| SPEAR | Blunt cream leaf tip, mid grip, narrow oak shaft | 2,512 |
| SHIELD | Rounded diamond, sage face, cream rim, rear wooden grip | 1,748 |
| PLATE | Shallow circular ceramic form, sage ring, brass rim | 2,108 |
| TABLE | Rounded rectangular top, four legs, lower shelf | 2,324 |
| SOFA | Three padded sections, sage arms, cream piping | 4,296 |
| BED | Thick padded mattress, sage quilt, oak frame | 3,152 |
| STOOL | Round padded seat, four legs, compact brace | 1,752 |
| FAN | Separate three-blade rotor, sparse grille, cream base | 3,060 |
| CLOCK | Separate hands, twelve ticks, round oak body | 2,848 |

Lower counts are accepted where the silhouette and editable structure need fewer than 2,000 triangles; no artificial subdivision is added.

An integration owner admits these as a separate visual collection. Retain original gameplay IDs, statistics, dimensions, held scales, ownership and letter accounting. Preserve imported FBX axis conversion under the placement parent. Runtime colliders remain game-owned. Do not point existing material families at these two collection materials without considering Classic/Candy/Arcade skin behavior.

The Blender working grid is for authoring only. Each individual exported file is grounded at the origin with its own `Grip_R`. Source markers have item prefixes to avoid Blender name collisions; the exported marker is named exactly `Grip_R`. FAN rotor and CLOCK hands remain separate editable parts.

Both formats passed hash, triangle, bounds, grip, UV, material and texture checks in a fresh Blender process. Native Unity material remapping, renderer appearance, animated hand contact, collision behavior and gameplay states are **NOT RUN** in this staging lane.

Delivery checks passed for twelve original concepts, twelve editable models, twenty-four exports, thirty-six model views and twelve transparent icons. All twelve three-quarter views were visually inspected. Table top overlap and the plate's recessed rim were corrected and their views rendered and inspected again. These are producer/agent checks; human owner acceptance remains pending.
