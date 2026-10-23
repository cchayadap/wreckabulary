# Workshop source binding and assigned draft return

STATUS: IN PROGRESS — source implementation only; no compiler, native or heavy tests authorized in this lane.

Root assigned this work after reviewing recovery `27042b5`. Asset lane transferred ownership without starting the follow-up. Scope: workshop draft/source selection and launcher local/private-online item/rule/profile binding, plus focused regressions. Art, public matchmaking, in-game recovery format and editor Inspector recovery are separate work.

Preserve the assigned course's runtime edits and Undo/Redo across template selection; offer an explicit Assigned course action. Templates use default game item/rule bytes and profiles. Assigned or explicitly recovered authored drafts use captured matching assigned inputs. Keep same-ID template autosaves separate from assigned recovery files. Save failures must prevent switching rather than discard the outgoing draft.

Baseline-only alignment commit `f2fb43e` combines current root-owned source with reviewed recovery `27042b5`; do not integrate this baseline. Deliver only the following repair delta after root has `27042b5`.

Source checkpoint: assigned and template sessions retain the same history, catalogue, selected mode, storage and folder. Assigned inputs capture exact item/rule bytes at opening; local and private online composition use the same source selection. Template saves use templates/<template>/; an unassigned legacy template is read compatibly and preserved. Source choices run after IMGUI and are cancelled on focus loss or disable. Compiler/native checks NOT RUN. Additional excluded baseline: b7795e9 (current integrated input-safety source). Focused regression fixture is next.
