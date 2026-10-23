# Controller menu and workshop source review

STATUS: PASS — BOUNDED SOURCE REVIEW ONLY; NATIVE VERIFICATION NOT-RUN

Reviewer: online lane. Date: 2026-10-23. Producer: input lane in `wordbound-puzzle-race`. Reviewed `dd8cadd`, `9be8660`, `c102ba4` and the final correction `7a7aa50` against the producer's exact root-file baseline. The producer's baseline import `f2152ba` is not an implementation change to integrate.

Inspected: `WordboundPartyMenu.cs`, `WordboundPartyMenuInput.cs`, `WordboundPadFocusGate.cs`, `WordboundWorkshop.cs`, `WordboundWorkshopInteraction.cs`, `WordboundWorkshopInputSafety.cs`, both new input test classes, and the existing `WordboundInputBinding`/`WordboundMenuFocus` handlers.

Confirmed finding: a controller confirmation queued from a real Repaint control could execute at the next Update after text capture or a global gameplay input lease began, because the new blocker check originally happened after deferred action delivery. The producer moved blocker evaluation before delivery and clears pending pad actions while retaining explicit pointer actions. Two new tests queue the actual Connect control, activate text capture or `KeyBindings.BlockGameplay()`, assert no request/authentication, require neutral after release, and then activate afresh. The final source closes the finding.

Other reviewed behavior:

- Party buttons, selection changes and toggles queue work outside Layout/Repaint. The context guards visibility, startup/return state, coordinator state, party revision and selected mode/region/format/seed. Disabled controls are not registered for controller confirmation. A queued controller action is discarded on device loss; a deliberate pointer action survives the pad-to-desktop switch.
- Focus loss/disable clears queued actions and confirmation. Controller reading requires a present enabled device and neutral after transitions. Back uses the existing return/leave/retry paths. Input disposal is retained.
- Root's public map-choice caching and fallback to the first valid seed remain intact. Public/private selection still calls the existing coordinator methods, and authored hosting normalizes Fill against the mode capability.
- Workshop controller input is blocked while hidden, unfocused, sharing, starting online, typing or globally blocked. It cancels uncommitted controller gestures on removal/blocking and requires neutral on resumption. Routine absent-pad checks cancel only an active controller gesture/tools state, so an idle mouse placement remains intact. Focus-loss cancellation intentionally abandons active uncommitted gestures.

The tests exercise native Input System and actual GUI-control paths with a fake HTTP transport, plus the real workshop controller handler. There are nine new cases. They have not executed in this lane and do not prove actual authentication, Windows focus automation, rendered scrolling, controller feel or gamepad-only text editing. Root must run the exact integrated source through R0 and the two native input classes, then inspect player keyboard/pointer/controller behavior. This review does not change the separate live public, native Photon or authored-transfer acceptance requirements.
