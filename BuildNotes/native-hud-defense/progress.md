# Native HUD defense regression

STATUS: SOURCE READY — COMPILATION AND NATIVE EXECUTION NOT RUN.

- [x] Reuse the existing authenticated two-peer `WordboundNativeGameplayTests.Run` and its cleanup. No new harness or production changes.
- [x] Add distant SHIELD/PLATE letter supplies to the existing HUD proof course, outside the wall-occlusion test's interaction range.
- [x] Gather real supplies through ordinary admitted movement/collection, run actual crafting timers, and select equipment through `WordboundMatchHud.ReadCommands`.
- [x] For both the host and remote peer, drive held block through the actual HUD handler and its existing real-time throttle. Observe every host `FrameChanged`: the selected shield and raise-start tick must remain unchanged, with no expired/missing lease after the first accepted hold. Require more than 30 authoritative ticks and at least three distinct successful Hold receipt ticks.
- [x] Test key-up, spell menu, pause-menu handler, Unity focus-loss callback plus HUD Update, and selection of a different held item. Each path requires an authenticated successful Release receipt before the prior lease deadline, replicated null defense, and no later Hold receipt after release. Selection additionally verifies Release precedes successful Equip.
- [x] Unsubscribe the frame observer in `finally`; destroy temporary UI in `finally`; use existing fixture teardown for runners, service parties, scenes and pending tasks.
- [ ] Separate-assembly reference compilation.
- [ ] Native test: `Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests.HostAndRemoteHudRefreshDefenseAndReleaseForKeySpellMenuFocusAndSelection`.

The new test depends on the previously delivered HUD occlusion fixture and Online's `hudInteractions` Run seam. It is intended for the assembled integration tree containing schema3 defense, not this lane's older Rules files alone.

This is native UI-handler/command testing, not simulated human keyboard or operating-system focus automation. HUD/View components are disabled to keep unattended Editor focus and IMGUI rendering from supplying unrelated input. The test explicitly invokes their actual input/menu/focus handlers. Preparation collection/crafting uses the existing authenticated command helper; every tested Hold, Release and Equip is submitted by the real HUD. Native body interaction, transport authentication, host journal and replica checkpoint remain real. The test does not prove shield damage reduction, art quality, human control feel or full-course completion.

Next recoverable action: integrate the test-only delta after the current native batch ends, run the bounded reference check, then run this explicit gated native filter with `WORDBOUND_EXTERNAL_GAMEPLAY=1`. Preserve failures and their authoritative tick/receipt diagnostics.
