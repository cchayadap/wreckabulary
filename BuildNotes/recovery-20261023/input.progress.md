# Wordbound input recovery

STATUS: SOURCE REVIEWED; COMPILATION AND NATIVE VERIFICATION NOT RUN

- [x] Read project and portable harness instructions; preserve the recovered dirty input draft and its eight tests in the root recovery snapshot before editing.
- [x] Confirm branch baseline `2893a03`; it only imports the root PlayerView baseline and must not be cherry-picked. Root already integrated the HUD/native defense changes through `b4b0b5c`.
- [x] Implement Wordbound-only automatic keyboard/gamepad input, safe focus/device/menu transitions, Tab slot cycling and held shield/throw controls.
- [x] Implement controller navigation for gameplay inventory, recipes, story and return: visible focus, bounded repeat, A confirm/B back, right-stick scroll and selection reveal. Mouse/keyboard and original input implementations remain intact.
- [x] Remove repeated per-frame input LINQ and unnecessary binding allocation; cache recipe cards by actual letters rather than rebuilding them for every IMGUI pass. This is a structural allocation reduction, not a measured FPS improvement.
- [x] Add 20 targeted regression cases: 13 synthetic InputSystem/View cases, 4 focus/activation cases, 1 card-cache invalidation case and 2 deferred IMGUI action cases. Extend authenticated native shield test to use slot cycling for selection release.
- [x] Resolve independent review's Layout/Repaint topology finding: defer menu actions and recipe filtering until Update, clear pending actions on menu/focus/back transitions, and capture selected slots before deferred sends. The editor-window regression uses actual journal focus confirmation through MenuButton, not a manually queued journal action.
- [x] `git diff --check` passed; source review traced device removal, focus loss, typing/blocked controls, held input, mouse takeover, changed/disabled recipe selection and cleanup.
- [ ] Run the assigned source/reference check when the shared compiler slot is available.
- [ ] Root: run actual Input System and HUD native regressions, then Windows build/smoke and human control review.

No Unity/compiler run has occurred in this recovery lane. Original InputBinding/DesktopBinding/GamepadBinding, lobby, Launcher, Workshop and PartyMenu are outside this lane's write scope. Wordbound has no implemented dodge action; no controls documentation will claim one. Source review and automated native input are not proof of human controller feel.

Next recoverable action: root integrates this isolated delta (not baseline commit `2893a03`), runs assembled R0/core as applicable, then native filters `WordboundInputBindingTests|WordboundMenuFocusTests|WordboundRecipeCacheTests|WordboundMenuGuiTests` and `HostAndRemoteHudRefreshDefenseAndReleaseForKeySpellMenuFocusAndSelection`. Root owns Unity, Windows build and visual/controller acceptance. Coordinate the shared compiler slot with root before any native run.

The View focus regression passes an explicit focus flag into the same private path called by Update; it also invokes Unity's actual focus callback. This proves handler behavior when run, not operating-system focus automation. The menu focus tests exercise stable enabled-control order and one-shot activation. The new IMGUI regression creates an EditorWindow and sends Layout/Repaint with a synthetic selected gamepad; focused state is explicit. Native execution must confirm these passes execute and succeed; rendered player scrolling and human controller review remain separate. Initial controller selection or resume requires held action buttons to be released once; movement/look stay responsive. Wordbound uses RT interact, Y spell, X attack/held shield, RB + X throw, LT block, hold LB drop and right-stick click equipment. No dodge is dispatched.

Independent reviewer `/root/recover_assets` confirmed the deferred handlers and revised real-button regression close the bounded source findings. This is source review only; all 20 cases remain native-pending in this lane.

No paid generation, deployment, package edits, legacy input edits, handwritten Unity metadata or cleanup/deletion occurred in this lane.
