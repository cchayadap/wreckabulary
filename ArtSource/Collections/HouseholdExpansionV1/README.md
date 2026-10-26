# Household Expansion V1

Eight **unchanged source models** from the owner's local Kenney Furniture Kit 2.0, staged for an editable Wordbound room kit. These files are not imported into Unity and do not replace current game assets.

`Source/Kenney/Models/` contains the original FBX bytes. `Source/Kenney/License.txt` is the original supplied CC0 license. `manifest.json` records exact source/destination SHA-256 hashes, source GLB measurements, category, child-part names and outstanding physics work. No textures are included: these selected files use material colors and declare no texture objects.

| Area | Exact source model | Source GLB dimensions X × Y × Z | Triangles |
|---|---|---|---:|
| Kitchen | kitchenStove | 0.430 × 0.450 × 0.450 | 830 |
| Kitchen | kitchenFridge | 0.430 × 0.920 × 0.292 | 250 |
| Kitchen | kitchenCabinet | 0.430 × 0.450 × 0.450 | 114 |
| Attic | bookcaseClosedDoors | 0.400 × 0.850 × 0.250 | 296 |
| Attic | cabinetBedDrawer | 0.266 × 0.263 × 0.217 | 182 |
| Rooftop | pottedPlant | 0.212 × 0.654 × 0.241 | 60 |
| Rooftop | bench | 0.400 × 0.470 × 0.200 | 170 |
| Packing Yard | cardboardBoxClosed | 0.212 × 0.281 × 0.212 | 60 |

Dimensions are calculated from the GLB counterpart's actual vertices and node transforms, in source coordinates. They are **not yet verified Unity FBX dimensions**. All selected FBXs declare Y-up and UnitScaleFactor 100; normalization against the Wreckabulary character and map grid remains pending.

The drawer cabinet is not a hinged chest. The closed cardboard box is a parcel, not a wooden crate or trolley. Several files have separate rigid doors/drawers; none includes a skeleton, deformation setup or animation clip. Colliders, mass, hinge/slide limits, breakage and interaction are not authored.

Import only into a new project-owned folder through Unity, preserve the original model bytes, and create editable wrapper prefabs and URP materials. Retain separate door/drawer nodes. Keep repeat setup missing-only so designer changes survive. Require scale, palette, collision, motion and gameplay-camera review before connecting these to the map catalogue. CC0 permits modification; optional supplier credit is retained here for provenance.
