# Controller delivery repair

STATUS: SOURCE IMPLEMENTED; INDEPENDENT SOURCE REVIEW PASS; NATIVE VERIFICATION PENDING

- [x] Preserve exact root `34a70ea` PartyMenu, Workshop, WorkshopInteraction and authoring-guide sources in private `.harness/input-review-baseline`; isolated import commit `f2152ba` is baseline only and must not be cherry-picked.
- [x] Add party controller focus, selection reveal, repeat, confirm/back/scroll and neutral gating. Controls route to existing coordinator methods; mouse controls and root map-choice caching/Fill policy remain.
- [x] Defer all party button, toggle and selection changes to Update. Capture menu context and discard stale queued actions on focus/device/visibility changes.
- [x] Gate actual workshop controller editing on focus, typed-field state and valid device. Cancel uncommitted controller gestures and require neutral after transitions; routine absent-pad checks leave mouse placement intact.
- [x] Correct guide record-copy wording, document exact private workshop hosting, actual desktop/gamepad mappings and the camera Inspector's degrees/metres presentation.
- [x] Add nine native regression cases: real Input System → actual party IMGUI controls → fake coordinator transport (connect/select/create/ready/Quick Play, private/retry/back, focus-loss discard, new typing/global-input-lease discard), explicit queued mouse action on pad-to-desktop handoff, plus actual workshop ControllerInput undo/typing/replacement/drag paths.
- [x] Independent review found that a pad confirmation queued during Repaint could run after typing or a global input lease began before Update. Pending pad commands now recheck those blockers before execution; explicit pointer commands remain accepted. Two actual queued-control regression cases cover the blocker transition and held-confirm neutral gate.
- [x] `git diff --check` passed.
- [x] Independent online-lane bounded source review PASS for deferred party actions/context gating/device transitions and workshop focus/drag cancellation, including the blocker-order follow-up `7a7aa50`. This is source evidence only.
- [ ] Root: assembled source/reference check and targeted native `WordboundPartyMenuInputTests|WordboundWorkshopInputSafetyTests`.
- [ ] Root: actual Windows controller navigation, focus changes, keyboard/pointer interaction and private/public connections.

No compiler, Unity or native job ran in this lane. The party tests use a local fake transport to verify UI routing, not real authentication or matchmaking. The focus tests pass explicit focus to the same production handler, not an operating-system focus automation proof. Party text editing still uses a keyboard; default guest name and clipboard paste controls provide pad-only routes for existing invites. Workshop pad editing remains the existing tool palette; this change addresses safety rather than claiming every form can be typed with a controller.

Only owned source/doc paths are changed. Root retains coordinator/network semantics, asset preparation, package/scene/profile registration, authoring editor and all heavy verification. Deliver as a clean patch against the private root baseline, including the playtest guide's existing root version; do not overwrite root files using this lane's older history.

## Recoverable handoff

Source commits are `dd8cadd`, `9be8660`, `c102ba4` and `7a7aa50`; baseline import `f2152ba` must not be integrated. Generate the clean combined patch at `.harness/input-review-baseline/controller-delivery.patch`, replacing the lane's added PLAYTEST diff with a diff against the private root copy. Root should use `git apply --check` before applying it, then run the assembled R0 and native input suites. The patch owns PartyMenu, its new input partial, the three workshop input files, the new neutral gate, two new regression fixtures and the two user guides. Existing input test assembly references already cover these files; Unity creates metadata during root import.

The prior audit remains separately recoverable in report-only commit `8533f46`. Serialized public catalogue/profile assignment and real Windows acceptance remain root delivery tasks.
