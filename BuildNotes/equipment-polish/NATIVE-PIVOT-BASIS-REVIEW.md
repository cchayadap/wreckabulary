# Native articulated-pivot basis investigation

STATUS: SOURCE REPAIR — native matrices confirm retained local basis; focused native rerun pending

Root `TestResults/editmode_20261023T122055Z.xml` records three failed basis comparisons: FAN_Rotor 1.25027609, CLOCK_HourHand 1.24342656 and CLOCK_MinuteHand 1.24197376 at basis column 1. Origin comparisons ran first and did not fail. The independent detailed dimensions/grips/material budgets, map settings and exact 27 renderer totals passed. This separates the basis disagreement from demonstrated geometry/budget failure; it does not yet establish the correct conversion.

Root commit `6e7d645` adds full expected/native/model matrices for a focused rerun. This lane will not weaken tolerances or mutate assets before comparing those actual matrices with exporter behavior. Blender/Unity/compiler execution is NOT RUN here.

## Exporter and frozen FBX evidence

`DetailV2/Source/build_wordbound_pvp.base.py:440` exports with `axis_forward="-Z"`, `axis_up="Y"`, `bake_space_transform=False`, unit scaling and no animation. The detail builder records each empty's Blender `matrix_world` before calling that export function. The recorded manifest matrices therefore describe Blender-space pivot bases, not already converted FBX local bases.

Installed Blender 5.2 primary exporter source (`scripts/addons_core/io_scene_fbx/fbx_utils.py:1754–1818`) applies the global conversion to the root. It only post-multiplies the inverse global conversion when `use_bake_space_transform` is true. The frozen export disabled that option. `export_fbx_bin.py:3654` also identifies the matching -Z/Y global conversion as a -90° X rotation. Unity importer source and generated model metadata both set `bakeAxisConversion=true`; that does not establish that every retained local basis equals a Blender-to-Unity conjugation.

A bounded, read-only binary FBX parser inspected the actual two frozen FBX files without loading Blender or decompressing geometry arrays. Both file versions are 7400. FAN and CLOCK roots have `Lcl Rotation=(-90.0000093347,0,0)`. FAN's normalized scale is `(1,.7552869916,.9963591695)`; rotor local translation is `(0,.03400000185,.5600000024)`. CLOCK's normalized scale is `(1,.7484662533,.9815950990)`; both hand origins are `(0,.08100000024,.2490000129)`, with local Y rotations -51.5662138561° and +45.8366174633°. These retained local axes match the exporter's disabled transform-baking setting.

## Prediction awaiting native matrices

Let `C` swap Y/Z (the known source-world point conversion) and `F=diag(1,1,-1,1)` reflect local Z. The current test expects `C*M*C`. A retained FBX local basis predicts `C*M*F`, with a native root equivalent to +90° X. Its column-1 disagreement with the current expectation is exactly the length of source column1 minus source column2. The predicted magnitudes agree with the three existing failure magnitudes to float rounding. This remains a sign/basis hypothesis until the focused native output confirms all matrix columns. Origin/bounds alone cannot settle that distinction.

## Native confirmation and bounded fixture correction

`TestResults/editmode_20261023T123439Z.xml` now records each full matrix. The model root is +90° X to float rounding. FAN_Rotor columns are `(1,0,0)`, `(0,0,.7552871)` and `(0,-.9963592,0)`; the origin is `(0,.5579612,.0562688)`. Both CLOCK hands also retain their local Y-rotation basis and match the independently predicted `C*M*F`. The previous `C*M*C` expectation incorrectly assumed the exporter had baked the global conversion into each retained local basis.

The fixture now computes `C*M*F`. The 0.001 m origin tolerance, 0.001 basis tolerance, all three basis comparisons and separate articulated mesh-ownership checks remain unchanged. No models, import settings, source transforms or material files are modified. Maps, dimensions and renderer counts passed again in this native run; the three pivot tests still require their focused rerun. This technical correction does not change the owner's rejection of DetailV2 appearance.
