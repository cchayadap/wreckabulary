# Native articulated-pivot basis investigation

STATUS: CLOSED — native matrices confirm retained FBX local basis; root corrected fixture only and six detail checks pass

Root `TestResults/editmode_20261023T122055Z.xml` records three failed basis comparisons: FAN_Rotor 1.25027609, CLOCK_HourHand 1.24342656 and CLOCK_MinuteHand 1.24197376 at basis column 1. Origin comparisons ran first and did not fail. The independent detailed dimensions/grips/material budgets, map settings and exact 27 renderer totals passed. This separates the basis disagreement from demonstrated geometry/budget failure; it does not yet establish the correct conversion.

Root commit `6e7d645` adds full expected/native/model matrices for a focused rerun. This lane will not weaken tolerances or mutate assets before comparing those actual matrices with exporter behavior. Blender/Unity/compiler execution is NOT RUN here.

## Exporter and frozen FBX evidence

`DetailV2/Source/build_wordbound_pvp.base.py:440` exports with `axis_forward="-Z"`, `axis_up="Y"`, `bake_space_transform=False`, unit scaling and no animation. The detail builder records each empty's Blender `matrix_world` before calling that export function. The recorded manifest matrices therefore describe Blender-space pivot bases, not already converted FBX local bases.

Installed Blender 5.2 primary exporter source (`scripts/addons_core/io_scene_fbx/fbx_utils.py:1754–1818`) applies the global conversion to the root. It only post-multiplies the inverse global conversion when `use_bake_space_transform` is true. The frozen export disabled that option. `export_fbx_bin.py:3654` also identifies the matching -Z/Y global conversion as a -90° X rotation. Unity importer source and generated model metadata both set `bakeAxisConversion=true`; that does not establish that every retained local basis equals a Blender-to-Unity conjugation.

A bounded, read-only binary FBX parser inspected the actual two frozen FBX files without loading Blender or decompressing geometry arrays. Both file versions are 7400. FAN and CLOCK roots have `Lcl Rotation=(-90.0000093347,0,0)`. FAN's normalized scale is `(1,.7552869916,.9963591695)`; rotor local translation is `(0,.03400000185,.5600000024)`. CLOCK's normalized scale is `(1,.7484662533,.9815950990)`; both hand origins are `(0,.08100000024,.2490000129)`, with local Y rotations -51.5662138561° and +45.8366174633°. These retained local axes match the exporter's disabled transform-baking setting.

## Prediction awaiting native matrices

Let `C` swap Y/Z (the known source-world point conversion) and `F=diag(1,1,-1,1)` reflect local Z. The current test expects `C*M*C`. A retained FBX local basis predicts `C*M*F`, with a native root equivalent to +90° X. Its column-1 disagreement with the current expectation is exactly the length of source column1 minus source column2. The predicted magnitudes agree with the three existing failure magnitudes to float rounding. This remains a sign/basis hypothesis until the focused native output confirms all matrix columns. Origin/bounds alone cannot settle that distinction.

## Measured closure

Read-only inspection of root `TestResults/editmode_20261023T123439Z.xml` confirms the prediction across the actual recorded matrices. FAN's native columns are approximately `(1,0,0)`, `(0,0,.7552871)`, `(0,-.9963592,0)`; its origin is `(0,.5579612,.0562688)`. CLOCK hour columns are `(.6216097,.7689101,0)`, `(0,0,.7484663)`, `(.7833270,-.6101692,0)`; minute columns are `(.6967067,-.7041532,0)`, `(0,0,.7484663)`, `(-.7173560,-.6838840,0)`. Both clock origins are approximately `(0,.2444173,.0695461)`. All three native model matrices show the expected +90° X root. These are `C*M*F`, including orientation and nonuniform scale, within float rounding; they are not `C*M*C`.

Root commit `5e9ad10` changes only the fixture's conversion to `axes * blender * Matrix4x4.Scale(new Vector3(1,1,-1))`; the strict origin and basis tolerances and mesh-ownership checks remain. Root `TestResults/editmode_20261023T125443Z.xml`, inspected directly in this lane, records **6/6 equipment detail cases passed**: all three articulated pivots, maps, dimensions/grips/material budget and exact renderer counts. No model or importer transformation repair was needed for this issue. The overall native batch had unrelated input failures, so this is not a full-suite pass.

This lane ran no native processes and changed no test assertions or assets; root owns the executed tests and converter repair. DetailV2 remains **owner-rejected in appearance**. A technical pivot/map/budget pass does not approve its visual design or justify replacing the original game artwork.
