# Wordbound equipment collection

The twelve approved Wordbound PvP assets are an independent visual collection. Existing recipe IDs, combat statistics, letter costs, grip positions, held scales, dimensions and the original ninety-eight-model library remain authoritative.

The batch contains BAT, BROOM, HAMMER, SPEAR, SHIELD, PLATE, TABLE, SOFA, BED, STOOL, FAN and CLOCK. Each has an original generated concept, a named editable Blender hierarchy, FBX and GLB exports, three model views and a transparent icon. ROPE and MAGNET live separately in `ArtSource/Collections/WordboundPvP/FutureConcepts/` as future concepts only.

## Import and verification

Run from the project root:

```powershell
node Tools/AssetPipeline/verify_wordbound_delivery.mjs
node Tools/kit/gate/game_gate.mjs exec Wreckabulary.EditorTools.WordboundEquipmentImporter.Build
node Tools/kit/gate/game_gate.mjs test editmode --filter WordboundEquipmentTests
```

The first command checks original image hashes, source/export signatures, exact catalogue measurements and transparent icon pixels. The importer checks source hashes and catalogue values before copying only its FBXs, icons and palette. It creates two URP/Lit materials, remaps them on the imported FBXs, verifies imported bounds/grips/UVs/triangle counts and creates `Assets/_Project/Resources/Collections/WordboundPvP.asset`. It does not rebuild a global library or change a catalogue.

Unity generates `.meta`, material and ScriptableObject files through AssetDatabase. They must be committed by the integration lane after the importer succeeds. No manual YAML or metadata is provided by the asset production lane.

Native Unity validation is **NOT RUN in this lane**. The root integration lane owns that gate and records its result separately. Blender reimport and complete file delivery validation already pass. Producer and root agent inspected all twelve three-quarter previews; human owner acceptance is still pending.

## Runtime interface

`Wreckabulary.Art.WordboundEquipmentCollection.Load()` loads `Collections/WordboundPvP`. `Entries` exposes the collection; `Find(itemId)` and `FindByModelKey(modelKey)` return a matching entry or null. Each entry has:

| Field | Contract |
| --- | --- |
| `itemId` | Existing catalogue ID, such as `BAT` |
| `modelKey` | Existing model key, such as `Items/BAT` |
| `model` | Independent imported FBX GameObject |
| `icon` | Independent 512×512 transparent Sprite |
| `size` | Existing width, height and depth in metres |
| `grip` | Existing `Grip_R` coordinates in Unity space |
| `heldScale` | Existing catalogue held scale |

The runtime integration owner adds an explicit opt-in. Missing collection or entry must fall back to the original model/icon. Instantiate the imported model under a placement parent using `Instantiate(entry.model, parent, false)` and preserve the imported child's local transform. Apply held scale to the normal gameplay placement parent; do not zero the FBX's axis conversion or rescale its geometry independently.

The collection does not register a recipe skin and has no Candy/Arcade variations. Preserve the existing choice path for those skin requests. Its material names (`WordboundPvPPaint`, `WordboundPvPBrass`) do not have a legacy skin suffix; current `MaterialLibrary.ApplySkin` and `TactileMaterials` therefore leave their references intact. An editor regression test covers both calls. No extra material opt-out is required with current naming.

Runtime colliders and damage behavior continue to come from the game. FAN rotor and CLOCK hands remain separate authorable transforms; this lane does not add new animation or gameplay behavior.

## Production and provenance

Source: `ArtSource/Collections/WordboundPvP/Source/WordboundPvP.blend`, Blender 5.2.2 LTS. Concepts use the built-in OpenAI image tool; exact prompts and original file hashes are in the collection's `prompts.json` and `provenance.json`. Meshes are concept-guided deterministic Blender geometry authored for this project, with no external image-to-3D service or stock asset source. No exclusive copyright or stock licence claim is inferred from generation.

Two materials and a shared 512×64 palette serve the full batch. Mesh counts range from 1,676 to 4,296 triangles. Simple objects below 2,000 triangles retain their compact geometry. The editable source uses a working grid; individual exports are grounded at the origin with their authored `Grip_R` marker.

`Tools/AssetPipeline/build_wordbound_pvp.py` only regenerates this collection. Preserve manual Blender edits under a new source filename before rebuilding. Source regeneration, export validation and renders are separate commands documented in the collection README. `--only BAT` is an exemplar build that replaces the generated manifest/source with BAT only; omit it for the full collection. Do not run the older global art builders to import this collection.
