# Editor icon additions

STATUS: FROZEN DRAFT — SEMANTIC SOURCE CHECK PASS; NATIVE/VISUAL CHECK NOT RUN

Integrate only the two files listed in `manifest.json` after checking their baseline hashes. `editor-icons.patch` contains the exact diff; `baseline/` and `candidate/` preserve original/proposed source. This lane changed no live source, Workshop code, meta or asset files.

| Constant | ID | Shape |
| --- | --- | --- |
| `LobbyIcons.Undo` | `undo` | Curved return arrow pointing left. |
| `LobbyIcons.Redo` | `redo` | Exact horizontal mirror of Undo. |
| `LobbyIcons.Move` | `move` | Four-way arrow. |
| `LobbyIcons.Duplicate` | `duplicate` | Two overlapping outlined sheets. |
| `LobbyIcons.Delete` | `delete` | Bin with lid, handle and two slots. |
| `LobbyIcons.Group` | `group` | Two objects enclosed by one set of selection corners. |
| `LobbyIcons.Ungroup` | `ungroup` | Separated objects with outward corner marks. |
| `LobbyIcons.Layers` | `layers` | Three stacked diamond layers. |
| `LobbyIcons.Frame` | `frame` | Four framing corners. |
| `LobbyIcons.Dice` | `dice` | Rounded die with five pips. |
| `LobbyIcons.Eye` | `eye` | Almond outline and pupil. |
| `LobbyIcons.EyeOff` | `eye-off` | Matching eye crossed by a separated diagonal slash. |

The existing 20 IDs retain their order at the start of `All`, and the 12 new IDs are appended. Existing Shape case bodies, rasterization, caching and ownership are unchanged. New symbols reuse the current 24-unit design grid, `Half` stroke width and existing Arc/Lines/Box/Circle/Union/Cut primitives. The curve and eye helpers are shared only by new cases. All new coordinates stay inside the original canvas with stroke padding. No downloads, fonts, licences, runtime cameras or external artwork were added.

One test in the existing `LobbyTests.cs` fixture checks that every public string constant is registered exactly once in `All`, every registered icon resolves and is cached, and an unknown name is rejected. This catches a missing registration or missing Shape branch without a second mirrored table of expected symbols. It does not claim that a glyph is visually understandable.

`check-source.ps1` passed Wreckabulary178 sources/302 references and Tests69 sources/292 references with zero errors. See `source-check.json`. No Unity or player execution occurred. Root should run the bounded icon test and inspect the actual Workshop controls at their intended size, especially Group versus Ungroup, Undo versus Redo, and Eye versus EyeOff. Keep accessible names/tooltips and clear selected/disabled state. This draft supplies symbols; root owns their precise command semantics, selection behaviour and placement.
