# Asset pipeline (Blender → Unity)

Turns the Wreckabulary source packs (the `.glb` deliveries in `Wreckabulary.zip`) into
Unity-ready FBX files plus a material library. Unity never reads the packs directly.

| Step | Script | What it does |
|---|---|---|
| 1 | `build_assets.py` | Imports each chosen GLB, strips cameras, lights and object clips, restores the authored rest transform, puts item models on the floor, and writes FBX. Merges the avatar and every wardrobe module onto one 22-bone rig with all 17 clips. Writes `materials.json` (authored glTF material values) and the textures (max 512 px). |
| 2 | `verify_assets.py` | Re-imports every FBX and checks triangle count, bounds (±1 mm), authored item size (±2 cm), `Grip_R` on items, no camera or light, every material in the library, and all avatar bones and clips. |

`selection.json` lists what gets converted (40 items, 26 letter tiles, 21 house modules,
10 VFX meshes, the avatar and 6 wardrobe modules).

## Run it

Run one step at a time, and not alongside a Unity build or test run.

```bash
BLENDER="C:/Program Files/Blender Foundation/Blender 5.2/blender.exe"
"$BLENDER" -b --factory-startup --python-exit-code 1 --python Tools/AssetPipeline/build_assets.py -- --packs <folder with vault-v2/ and quality-pass-01/> --repo .
"$BLENDER" -b --factory-startup --python-exit-code 1 --python Tools/AssetPipeline/verify_assets.py -- --repo .
```

Tested with Blender 5.2.2 LTS. Add `--only items,letters` to rebuild part of the set.

## Outputs

- `Assets/_Project/Art/Imported/{Items,Letters,Environment,VFX,Avatar}/*.fbx`
- `Assets/_Project/Art/Imported/Textures/*.png`
- `Assets/_Project/Data/Generated/materials.json`: one entry per material name. Item
  materials end in `_Classic`, `_Candy` or `_Arcade`; `family` + `skin` give the held-skin swap.
- `Assets/_Project/Data/Generated/build_report.json` and `verify_report.json`.

Generated files: don't edit them by hand. Change `selection.json` or the scripts and rerun.

## Notes

- FBX settings: metres, `-Z` forward, `Y` up, no leaf bones, no embedded textures. In Unity,
  the importer (`ImportedArtPostprocessor`) turns on *Bake Axis Conversion* and binds
  materials by name to the library materials.
- Facing: Unity puts Blender (x, y, z) at (x, z, y). This was measured on every model. The
  packs face glTF +Z, which is Blender -Y, so the build turns each scene a half turn about Z
  before export. Models then face Unity's +Z, and the report's bounds are in Unity axes.
- Single-mesh models keep Blender's axis conversion (and the half turn) as a rotation on their root.
  In Unity, put a model under its own parent and move or rotate the parent, never the model
  root, or the model ends up lying down.
- Held items are not separate files. At runtime a held item is the world model scaled by
  the recipe's `held_scale`, moved so `Grip_R` sits in the hand, with the holder's skin.
- The pack's house floors (7,344 triangles per 2 m tile), ceiling lamp and door panels are
  left out on purpose. See `selection.json`.

## Browser derivatives and actual-model previews

The cloud audit uses Blender 4.3.2 to read the existing hydrated FBXs. It does not
rebuild or overwrite the original Unity delivery. `export_web.py` writes 98 GLBs
to `Web/public/art/` and records mesh facts, source hashes, bounds, item grips and
canonical animations in `audit.json` / `manifest.json`. `gltf_materials.py` restores
authoritative PBR factors only after proving the embedded textures are unchanged;
this corrects a Blender interchange bug that dropped tint through Multiply nodes.
`render_supplied.py` and `render_icons.py` render the actual supplied models.

`export_mobile_avatar.py` creates a separate clothing LOD, retaining the head and
both facial expressions. `gltf_avatar_animation.py` preserves exact original
animation and inverse-bind bytes after checking the rest skeleton is equivalent.
`verify_web.py` independently checks 99 exports, including sparse morph accessors,
weights, joint indices, geometry, PBR, hashes, default outfit counts and animation
byte preservation. `promote_mobile_avatar.py` adds `avatar.mobilePath` only after
the verifier passes and the comparison render references current GLB hashes.

```sh
blender -b --factory-startup --python-exit-code 1 --python Tools/AssetPipeline/export_web.py -- --repo .
blender -b --factory-startup --python-exit-code 1 --python Tools/AssetPipeline/render_supplied.py
blender -b --factory-startup --python-exit-code 1 --python Tools/AssetPipeline/render_icons.py
blender -b --factory-startup --python-exit-code 1 --python Tools/AssetPipeline/export_mobile_avatar.py -- --repo .
python3 Tools/AssetPipeline/verify_web.py --repo .
blender -b --factory-startup --python-exit-code 1 --python Tools/AssetPipeline/render_avatar_lod.py
python3 Tools/AssetPipeline/promote_mobile_avatar.py --repo .
```

Run these sequentially. Re-exporting the full avatar invalidates the mobile source
hash and comparison; regenerate and review the derivative before promoting it.
The mobile default outfit has 17,660 triangles versus 31,696 full detail, with the
same six renderers and nine material slots. Neither file verification nor the
comparison render proves phone frame time or Unity import/build acceptance. See
`docs/art/SUPPLIED_ASSET_AUDIT.md` for the critical visual assessment and limitations.
