# Single-choice card width correction

STATUS: FROZEN DRAFT — SOURCE CHECK PASS; NATIVE/VISUAL CHECK NOT RUN

Recommended diff: four source-line replacements across two production files. A single Random house or Wordbound editor card should use the same left-aligned footprint as a normal house card at the same viewport width. It should not stretch to fill the entire row merely because it is the only available choice.

| File | Exact intent |
| --- | --- |
| `PlayPage.cs` | Random house and Wordbound editor grids use the normal house-grid capacity and minimum width. Shorten editor caption to `Build & playtest` so it fits the compact card. |
| `LobbyPlayPageView.cs` | Compute columns from available width and configured capacity, without capping to current child count. Actual row count still depends on real children; no empty/fake controls are created. |
| `LobbyPlayChoiceTests.cs` | Add one focused test in the existing fixture. At1868/1388/900 widths, compare single-card width/height/left edge against the actual Pinwheel card, check exactly one real choice, unchanged header/footer/action positions, Escape selection, editor caption fit and no creator launch. Existing assertions remain. |

`manifest.json` pins the exact current baseline and candidates. `single-card-width.patch` is the recommended integration diff; copy only its three target files after verifying baseline hashes. Existing saved fields, handlers, IDs, role bindings, full-width pane, no-avatar direction and fixed footer are unchanged. No assets/meta/YAML or native commands are involved.

Roslyn candidate substitution passed Wreckabulary178 sources/302 references and Tests69 sources/292 references with zero errors; see `source-check.json`. The added test is meaningful because the current stretched single cards fail its comparison with the multi-card reference. It has not run in Unity yet. Root should run the focused fixture and inspect actual Escape/Create Wordbound screenshots after integration.

This does **not** remove the remaining vertical blank space, generate actual map previews, redesign the tutorial's room panel or unify the Online entry form. Those findings remain in `../review/windows170019-play.md`. The current tested170019 delivery stays preserved while this draft is reviewed.
