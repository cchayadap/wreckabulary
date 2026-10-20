# Workshop viewport picking correction

STATUS: SOURCE CHECK PASS; NATIVE RERUN PENDING

Root's integration gate `TestResults/editmode_20261020T122803Z.xml` reported four of five workshop tests passing. `ViewportProjectsAndPicksTheActiveFloorWithoutChangingCourseData` failed its unchanged 5 mm tolerance with an X error of 334 mm.

The preview stage is deliberately placed at world Y = -20000. Projection previously transformed authored local coordinates into that distant world space, called native camera projection/unprojection, then intersected and transformed back into the authoring frame. The round trip exposed world-space precision in a tool whose document uses millimetres.

Projection and picking now use the camera's stage-local pose and the same perspective field of view/aspect for both directions. Surface planes, module bounds and fixed-object bounds stay in that same local frame. Rendered geometry remains in the isolated preview stage; the change removes the large world translation from authoring calculations. The original 5 mm test threshold is retained. The regression now covers the original view plus landscape/portrait aspect ratios and two orbit angles.

Verification performed without launching Unity:

```text
dotnet build BuildNotes/map-workshop/WordboundWorkshopTypecheck.csproj --no-restore --nologo -v minimal
Build succeeded: 0 errors, 6 CS0436 warnings.
```

The existing source-check project also references integration's compiled `Wreckabulary` assembly for shared art facilities; it reports duplicate workshop-type names and explicitly selects the edited local source. These are compile warnings, not native acceptance. No native rendering, camera or picking test was executed by this lane. Root must rerun the three viewport cases and full workshop fixture in Unity before declaring the observed failure resolved.
