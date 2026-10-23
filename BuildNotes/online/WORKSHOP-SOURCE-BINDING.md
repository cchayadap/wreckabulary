# Workshop source binding and assigned draft return

STATUS: SOURCE AND REGRESSIONS COMPLETE — compiler and native verification NOT RUN in this lane.

Root assigned this work after reviewing recovery `27042b5`. Asset lane transferred ownership without starting the follow-up. Scope: workshop draft/source selection and launcher local/private-online item/rule/profile binding, plus focused regressions. Art, public matchmaking, in-game recovery format and editor Inspector recovery are separate work.

Preserve the assigned course's runtime edits and Undo/Redo across template selection; offer an explicit Assigned course action. Templates use default game item/rule bytes and profiles. Assigned or explicitly recovered authored drafts use captured matching assigned inputs. Keep same-ID template autosaves separate from assigned recovery files. Save failures must prevent switching rather than discard the outgoing draft.

Baseline-only alignment commit `f2fb43e` combines current root-owned source with reviewed recovery `27042b5`; do not integrate this baseline. Deliver only the following repair delta after root has `27042b5`.

Source checkpoint: assigned and template sessions retain the same history, catalogue, selected mode, storage and folder. Assigned inputs capture exact item/rule bytes at opening; local and private online composition use the same source selection. Template saves use templates/<template>/; an unassigned legacy template is read compatibly and preserved. Source choices run after IMGUI and are cancelled on focus loss or disable. Compiler/native checks NOT RUN. Additional excluded baseline: b7795e9 (current integrated input-safety source). Focused regression fixture is next.

Delivery consists of source checkpoint `2e2b378` plus the following focused regression/closure commit. Do not integrate the two baseline-only commits. Root must already include recovery `27042b5` and its current input-safety source. Neither source commit edits the pre-existing dirty `ModeDefinition.cs` or historical logs.

- [x] Each assigned/template session retains its exact `WorkshopHistory`, catalogue, selected mode/format and storage. Returning does not reopen the asset or erase Undo/Redo. Pending authored recovery still blocks source switching.
- [x] Assigned item/rule strings are captured with the assigned geometry at opening. Later TextAsset reassignment does not mix new bytes with the old draft. Assigned optional profiles use their assigned references or defaults; templates and the global Flagship use global item/rule bytes and default profiles. Profile objects remain editable Unity assets.
- [x] Assigned opening configures the global Flagship action. The initial non-authored opening reuses that one generated Flagship definition instead of constructing it twice.
- [x] Local and private online content use the same input-selection function. Missing assigned capture or different geometry fails closed. The existing exact online bundle/profile association is retained.
- [x] Template saves use `templates/<template>/`; same-ID assigned recovery is neither loaded as a template nor overwritten. Without an assigned source, legacy template recovery is read and preserved; later edits save to the scoped path. Outgoing save failure aborts source switching.
- [x] Deferred choices discard on focus loss, disable, global typing or gameplay capture. A queued controller return is also discarded when workshop text takes focus. Explicit pointer selection retains its existing semantics.
- [x] Added `WordboundWorkshopSourceBindingTests`: 25 expected cases, covering retained edits/selection/seed/Undo/Redo, mode/format, same-ID files, recovered assigned draft, both save-failure directions, deferred cancellation, controller typing capture, assigned/global bytes and profiles, actual local/private-online manifest composition, global Flagship availability, missing/mismatched input capture, required bytes, optional profiles and legacy storage.
- [x] Added direct Authoring and Puzzles test-assembly references for the concrete asset and composition return types. No reliance on transitive assembly references.
- [x] Source/API review against current integration plus `git diff --check`; no native or heavy process started.
- [ ] Root R0 and 25-case native source-binding fixture.
- [ ] Root rendered workshop selection and local/private online playtest with an assigned course, a template and return to assigned.

Follow-up, in the integrated checkout after both repair commits:

```powershell
node Tools/kit/gate/game_gate.mjs check all --json
node Tools/kit/gate/game_gate.mjs test editmode --filter 'Wreckabulary.Online.Tests.WordboundWorkshopSourceBindingTests' --cold --json --timeout 660
```

Root may batch this class with its affected input fixtures to avoid repeated Unity startup. Require 25 executed, zero failed/skipped, and inspect teardown. Root reported the existing authoring 8, recovery 11 and workshop 9 cases passed before this source-binding delta; that historical result does not validate the delta. The new fixture invokes real workshop, storage, launcher input selection and local/private composition; it does not execute Photon, rendered clicks or a Windows player.

Independent review follow-up: the restored Flagship action initially still used generic base storage, sharing the default assigned flagship ID wordbound-lost-address. This was a real default-course overwrite risk, not only a custom ID collision. Flagship now has its own retained session fenced by original content hash and its own templates/Flagship storage; the palette action is deferred. Returning and reopening preserve its edits, selected mode and Undo/Redo. Added six cases: same-ID primary-byte preservation plus retained history/profile, focus cancellation, failed Flagship save, and three direct/previously queued source choices during online preparation. Existing enum-template and assigned behavior is unchanged. Compiler/native remain NOT RUN.
