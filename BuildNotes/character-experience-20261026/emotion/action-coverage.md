# Facial action coverage

STATUS: COMPLETE for this bounded read-only action audit and seven-image review. Full gameplay-action acceptance remains open.

Scope: current local `PlayerAppearance` and Wordbound presentation mapping, shared two-channel timeline/driver and five expression tests. Identify actual action signals and gaps; no live edits or native jobs.

## Confirmed source mapping

The original face has `BrowRelax` and `Blink`, so different actions intentionally share a small expression vocabulary. This is action-state coverage, not nine distinct anatomical expressions or proof that every gameplay trigger has been exercised.

| Action | Local house play (`PlayerAppearance`) | Wordbound (`WordboundGameplayPresentation`) | Remaining limit |
| --- | --- | --- | --- |
| Idle / walk / run | Idle or Moving from horizontal speed squared > .04. | Same threshold from canonical body velocity. | Capture grid has Idle; Moving is not visually exercised there. |
| Equip / hold | Equipped from `Combat.IsHolding`, including furniture; Moving takes priority. | Equipped from selected inventory item only. Carrying furniture clears that selection. | Confirmed gap: stationary furniture-only carry remains Idle online, although the body uses a carrying pose. |
| Melee / block / dodge | Focus from channel/block/dodge or Swing/Thrust clips. | Focus from authoritative `Attack`, `Defense` or `DodgeUntilTick`. | Rendered Focus exists; actual attack/block/dodge trigger integration still needs a targeted run. |
| Consume food / drink | Focus while `Weapon.IsUsing`; `HeldWeapon.ConsumeAfterChannel` sets it and plays Drink_Consumable. | No consumable-use command/channel is present in `WordboundAction` or `MatchActorState`; do not label this an implemented online face cue. | Local consumption shares the combat Focus pose; a gentler use cue is a design choice still to review. Online consumption requires checking gameplay support before adding expression claims. |
| Throw / ordinary release | Holding→not holding plays Throw_OneHand unless release is suppressed; this clip maps to Focus. | Throw changes the economy/body and returns a receipt, but does not set the expression's Attack/Defense/Dodge inputs. | Online throw has no explicit facial pulse. Local drop/release can also look like a focused throw: current body transition does not distinguish the reason. |
| Pickup / summoned item | Plays Pickup or Present_Item; expression falls through to Equipped/Moving/Idle. | Pickup updates economy/body; expression falls through to selected item/Moving/Idle. | No pickup-specific cue; a picked item may not be selected. Local Present_Item is not a separate facial state. |
| Deploy / place | `deploying` contributes to IsChanneling→Focus, with Place clip. | Deploy updates item deployment state/body; no dedicated facial signal is read. | Online placement has no explicit cue. |
| Craft / spell / puzzle / letter transfer | Crafting affects Inspect_OneHand body pose, but IsCrafting/IsSpelling are not read by facial mapping. | Craft state, puzzle use and transfers are not read by facial mapping. | Fall through to baseline unless another mapped condition is active. Do not claim coverage from the existence of command receipts. |
| Hit / stun | Hurt from stagger/Hit_Reaction or observed health loss. Blocked hit alone does not imply Hurt. | Hurt from authoritative stun or observed health loss. | Hurt overrides Focus. A missed transient between snapshots cannot be recovered from the current sample alone. |
| Downed / eliminated | Downed from IsKnockedOut (not alive). | Downed from zero health or eliminated player. | Highest priority; confirmed victory does not override a downed face. This preserves injury priority but needs explicit outcome design review for a downed winning teammate. |
| Falling / landing | Falling below -2.5 vertical speed when not grounded; Recover on observed falling→grounded. | Falling below -2.5 from active body; no grounded flag is supplied. | Online ordinary landing does not trigger Recover; only the recovery-generation/downed transitions below do. |
| Respawn / revive / checkpoint recovery | Recover on observed downed→alive; no recovery-generation input. | Recover on downed→alive or changed PlayerProgress.RecoveryGeneration. | Local revive-help action itself has no specific cue. Online generation is authoritative; no guessed ground contact was added. |
| Victory | Celebrate only if another caller plays the Celebrate clip. Source search found no production caller of `Play("Celebrate")`; RoundWon/MatchWon are emitted but not wired here. | Celebrate on a confirmed result whose WinningTeams contains the player's team. | Local automatic victory expression is absent. Online bounded timer/duplicate suppression is implemented; non-winners fall back, with no defeat expression. |
| Pause / migration / teardown | Scaled `Time.deltaTime` stops blending/timers when local game pauses; disable resets and destroy disposes. | Step freezes when controller IsSimulationPaused, except result faces deliberately continue. Migration/disconnection/unavailable sample resets the original two channels; disable/fault/disposal reset. | Client pause predicate excludes the host-only checkpoint pause flag; under a host commit stall without migration/result the client facial timer can keep advancing. This is the existing presentation contract, not proof of synchronized emotion time. |

Priority is Downed → Hurt → Falling → Focus → Recover → Celebrate → Moving → Equipped → Idle. Higher-priority conditions can consume the duration of a lower-priority timed cue; a late injury/recovery can hide victory until its bounded timer has elapsed. This is explicit current behavior, not full narrative outcome coverage.

### Source anchors

- `Assets/_Project/Scripts/Player/PlayerAppearance.cs:267` (body action selection), `:377` (facial sample).
- `Assets/_Project/Scripts/Player/PlayerCombat.cs:58` (IsChanneling), `:579` (Place); `Assets/_Project/Scripts/Summons/HeldWeapon.cs:165` (consume channel).
- `Assets/_Project/Scripts/Online/Presentation/WordboundGameplayPresentation.cs:101` (selection suppresses furniture), `:198` (facial mapping), `:257` (clock policy).
- `Assets/_Project/Scripts/Rules/Wordbound/CommandJournal.cs:7` (actual command list), `MatchState.cs:103` (actor fields), `AuthoritativeMatch.cs:409` (pickup/release/deploy), `EconomySnapshot.cs:32` (craft/furniture snapshot).
- `Assets/_Project/Scripts/Art/AvatarExpressionTimeline.cs:20` (priority and observed transitions), `AvatarExpressionDriver.cs:43` (blend/clock), `Assets/_Project/Scripts/Online/Gameplay/WordboundGameplayPlan.cs:122` (pause predicate).
- `Assets/_Project/Scripts/Game/RoundManager.cs:187` (local victory events); bounded `rg` over all project scripts found no automatic Celebrate call or subscription to these events.

## Existing tests and next meaningful checks

The five `AvatarExpressionTests` cover timeline priority/recovery, same-tick confirmed victory and duplicate suppression, unavailable/new-epoch/stale handling, a synthetic renderer's two-channel ownership/reset, and invalid profile data. They do **not** test the actual action-to-sample mappings or camera-visible gameplay, and the synthetic mesh does not prove a facial shape.

Prioritized next checks (not implemented by this read-only audit):

1. Exercise real local consume, throw, drop and deploy through existing action callers; assert face state while the action runs and returns after cancellation/completion. Distinguish a deliberate throw from a drop before changing that mapping.
2. Add a bounded Wordbound mapping fixture using real snapshots for selected weapon, furniture carry, accepted throw/deploy and rejected command. Repeated snapshots/replayed receipts must not retrigger a transient, and remote players must receive the same accepted-action cue. Avoid predicting a successful action from button input.
3. Test falling→grounded local recovery, authoritative respawn generation online, and no false recovery on migration/rejoin/new epoch. Ordinary online landing needs an actual reliable grounded signal if it is to gain a cue.
4. Test paused Step and resume without timer jumps, plus confirmed results during pause, host commit stall versus client, and injury/downed priority over a winner. Keep rejected/unconfirmed results from celebrating. Native clips should show the transition, not just its final numeric weight.
5. Wire and exercise a confirmed local winner event if automatic local victory is wanted; subscribe/unsubscribe with object lifetime, once per round/result, and verify a loser never receives it.

## Native visual review

Inspected the remaining seven actual `capture-03` images on 2026-10-26: Idle three-quarter; Focus front/three-quarter; Downed front/three-quarter; Recover front/three-quarter. Root separately inspected Idle front and Hurt front/three-quarter. I also read the capture receipt and independently rehashed all ten image files: every image matches; all 322 input hashes match before/after; all ten owned-object cleanup flags are true. Scene/global/profile preservation flags are true. This lane ran no native job.

| Viewed images | Visible result | Acceptance limit |
| --- | --- | --- |
| Idle three-quarter | Near-horizontal brows and fully open eyes look calmer than Focus; original red/ivory/charcoal design remains coherent. | Reads attentive, not smiling; mouthless source topology is preserved. |
| Focus front / three-quarter | Inward-sloping brows clearly distinguish effort; sampled swing pose reinforces the cue. The nearer eye and brow stay readable in the turned view. | It is a sampled swing, not proof of block/dodge/consume timing. |
| Downed front / three-quarter | Narrowed eyes, sloped brows and head/body cant communicate injury or daze; materially distinct from Focus/Idle. | The crop does not show a full-body fallen pose; no claim that a downed character lies correctly on the floor. |
| Recover front / three-quarter | Eyes reopen and brows relax from Downed. Reads a restrained return toward neutral. | The .25-second sample is a transition (actual Brow 55.85 / Blink 18.40, target 62 / 10), not a fully settled pose. Subtle at this crop; moving footage and gameplay distance remain necessary. |

Evidence: `BuildNotes/character-experience-20261026/tuned-expressions-20261026.json` records R0 135705, applied/reloaded saved profile 135740, capture 135900, 17-case combined Play run 140057, and idempotent no-save repeat 140221 as PASS. The capture receipt is `emotion/capture-03/receipt.json` (SHA-256 `bb37d92b960e8ac634040973b197b650ce071cb495a19aa51c9a68fe1881d3cb`). The 17 cases include existing appearance/animation/surface checks; that count must not be presented as 17 gameplay emotion actions.

Source and still-image review support the selected calm Idle and stronger Hurt defaults. They do not establish complete action coverage, live remote timing, all camera distances, all skins, or subjective player feel. Highest-value next work is the explicit online furniture/throw/place mapping, local winner/release distinction, and integration transitions listed above.

## Follow-up source status after the audit

Root integrated the independently reviewed `action-mapping/action-mapping.patch` (`c3873a0b…`). This supersedes the initial matrix's **source-level** furniture/craft gaps: online carried furniture now supplies Equipped; current CanAct+Craft supplies Focus; local actual IsSpelling/IsCrafting supplies Focus. The same injury/availability/timer priority still applies. Three new mapping tests are prepared, but this audit does not yet claim their native result or rendered transitions.

Root also authorized a separate bounded RoundManager victory increment (`action-mapping/victory/victory.patch`, `a686c83b…`): play the existing Celebrate clip once for living winning teammates in the already-confirmed FinishRound branch. At this entry it is a source draft awaiting independent review/root execution. Its Duos test includes an alive winner, a downed winning teammate and losing team. The separate Escape/MovingOut/MovingDay directors, throw/deploy, puzzle reactions and ordinary online landing remain outside this change.

Follow-up native evidence: independently read root's `TestResults/editmode_20261026T142701Z.xml`, 80/80 PASS with zero failed/skipped. Both new WordboundExpressionActionTests passed. This verifies the snapshot-to-driver mapping and priority under those test inputs; it does not upgrade the furniture/craft cues to remote rendered gameplay acceptance. Local Summoner/victory native results are still pending in this audit.

Final bounded follow-up evidence: independently read `TestResults/playmode_20261026T143003Z.xml`, 19/19 PASS, zero failed/skipped. ActualSpellingAndCraftingDriveFocusAndCancelOrDisableReleasesIt and ConfirmedDuosVictoryCelebratesTheAliveWinnerButPreservesDownedTeammateAndLosers both pass. The victory source was independently reviewed and integrated by root. Thus local real-controller spelling/craft transitions and RoundManager's confirmed team result are now natively exercised. The initial pending notes above are retained as chronology; remote rendered action timing, separate directors, throw/deploy/puzzle cues and general landing coverage remain open.
