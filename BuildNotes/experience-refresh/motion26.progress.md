# Motion26 — actual Windows BAT sequence

STATUS: IN PROGRESS — source implemented and frozen for review; native capture NOT-RUN.

## Scope and recovery

- Parent authorizes exclusive writes to the original-equipment QA scenario/view, optional new `WordboundMeleeCapture.cs`, and this report in `wordbound-integration`, branch `sethypagna/wordbound-integration`.
- Preserve the five existing inspection steps, their timeouts, exact command receipts, physics observations, screenshots, draft verification and fallback shutdown. Add one bounded attack step after BAT and before HAMMER.
- Preserve unrelated dirty shop importer/diagnostics/resources. No Tools/kit changes, hand-authored metadata, Unity/Blender/player jobs or commits in this lane until parent reports R0 and authorizes commit.
- Read root CLAUDE/harness and portable core. `.harness/brief.md` is absent; parent owns the active integration/checkpoint and gates. `game-studio-run`/`game-dev` unavailable; use existing project capture infrastructure. Applied game-visual-debugging evidence guidance; no adapter installed.

## Acceptance

- [~] Submit real BAT `WordboundAction.Attack` and require exact payload-matching `Ok` receipt plus canonical matching attack start/weapon.
- [~] Capture bounded live rendered frames through windup, active and recovery; retain exact tick/frame, hand/held matrices, grip distance and per-frame PNG hashes. Source implemented; native run pending.
- [ ] Buffer GPU screen copies before readback/PNG work so encoding cannot consume the short attack phases. No simulation pause, manual pose, tick or combat-stat writes.
- [ ] Require natural attack completion with unchanged held BAT; reject missed phases, unchanged pose, lost readiness, wrong command or interruption.
- [ ] Cleanup render resources and restore original QA state on success and failure.
- [ ] Parent source review/R0, then native Windows capture and visual frame review; these remain unrun until parent supplies evidence.

## Implemented source checkpoint

- Added `equipment_bat_motion` (75 seconds) between the existing BAT/HAMMER steps. Original step names, timeouts and checks remain; successful runs now retain 14 exact command receipts including Attack.
- `WordboundMeleeCapture` uses at most eight screen-sized ARGB32 GPU buffers and rejects a window exceeding its 128 MiB budget. Baseline, up to two distinct canonical ticks in each phase, and completion are copied at `WaitForEndOfFrame` after the assigned camera rendered. Readback/PNG work begins after capture stops. No time/pose/tick/combat writes or manual re-render.
- Requires the real Attack sequence/epoch/payload, `Ok` receipt/weapon, canonical `StartedTick == receipt.Tick`, exact catalogue cooldown, unchanged weapon and uninterrupted actor. All three canonical phases must have rendered samples, then attack state must clear and cooldown must expire naturally.
- Each captured frame records original mesh/material/frustum checks, hand/held/avatar/grip world matrices, the held transform relative to avatar, tick/frame/time, camera matrix and grip separation (at most 5 mm). Requires measurable hand and held movement relative to avatar, nonblank images, at least two distinct attack PNG hashes different from baseline and the same BAT after readback. Every SHA is retained; a legitimate return to baseline or repeated phase pixels is allowed. Phase labels describe canonical time; the actual interpolated visual pose can lead within a tick and remains subject to visual review.
- Observation owns cleanup because the kit manually drives enumerators without disposing them on timeout. Leaving the motion step, restoring the view or ending QA disposes capture subscriptions/buffers and writes a FAIL receipt if interrupted. Existing camera/HUD restoration, physics evidence and client fallback shutdown remain.
- Actual output: `equipment-bat-motion.json`, 5–8 `wordbound_original_bat_motion_NN_PHASE.png` images, plus the existing command receipt file. Failure keeps observed facts/any saved images; no retry or substitution masks a missed phase.

Source-only verification: `git diff --check` passed. Read authority command ordering, exact receipt types, original grip placement, LateUpdate presentation and QA runner lifecycle. Story26 reviewed source and agreed with the parent's SHA false-failure finding: fixed the universal uniqueness requirement because the completed pose may match baseline. No other source blocker found in allocation, timeout or cleanup. Pose movement alone does not prove the melee playable was active; the scope is captured canonical attack plus observed motion, pending visual review. Native compilation, GPU capture, Windows timing and visual acceptance are NOT-RUN by this lane. Parent owns R0 and subsequent gates.

Evidence limit agreed with parent/reviewer: this player capture verifies accepted command + rendered movement + grip; direct inspection of its frame sequence remains required. The earlier 13 native motion tests reported by the parent are separate playable-behavior evidence and are not rerun or substituted by this capture. No runtime/reflection hook was added solely to claim an active clip.

Frozen source at integration HEAD `c16bbd7a` (dirty source, not committed):

| File | SHA-256 |
| --- | --- |
| WordboundMeleeCapture.cs | 98acb93e54ca3743874e4f683a0b031b312f1713fc8c3c47563a204ea60e6989 |
| WordboundOriginalEquipmentQa.cs | 93d46f851f9da26b79487ed78a8897921eb3b9f577f178bd630ee9f2161bf61d |
| WordboundOriginalEquipmentView.cs | 6d867567fc5779b0d31f4fb5991583b914047230e140766782041b2e29b9433b |

Story26 independently re-read the final SHA correction at hash `98acb93e54ca3743874e4f683a0b031b312f1713fc8c3c47563a204ea60e6989`: no further source blocker found; required frame/tick, receipt, phase, movement, grip and cleanup checks remain. This is source review only.

Next: parent R0; fix concrete findings, then commit only when parent authorizes. Parent must build/capture the actual Windows scenario and inspect the entire frame sequence before motion acceptance.
