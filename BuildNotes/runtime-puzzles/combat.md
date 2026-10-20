# Authenticated combat and equipment

STATUS: SOURCE VERIFIED; NATIVE ACCEPTANCE PENDING

Baseline: `3633046`, branch `sethypagna/puzzle-race`.

Scope: Rules Wordbound command, authority, state, codec and validation; Items.cs if needed; focused rules tests; owned Online/Puzzles source. Keep pre-release snapshot schema 2. Do not edit Online session, Gameplay, Presentation, scenes, metadata or SDK files. Root owns native verification.

Acceptance: authenticated commands select one held hand item and request an attack; host derives weapon statistics, range, arc, timing and damage. Carried furniture remains separate from held equipment. Complete canonical state must preserve selection and any active attack across snapshot restore without duplicate hits or client-supplied damage. Meaningful rules tests cover invalid equipment, causal timing, opposing teams, geometry checks, duplicate command/advance, rollback and migration.

## Implemented contract

`WordboundAction.Equip`: `Slot` selects one occupied Held inventory slot; `-1` selects unarmed. Target/value must be empty and revision zero. Selection changes cancel an in-flight attack without resetting recovery. A selected non-melee item returns `NoMelee`, rather than silently substituting fists.

`WordboundAction.Attack`: all payload fields must be defaults. The authenticated actor comes from the existing transport wrapper. Attack facing comes from the host body rotation; there is no client damage, target, aim, hit list or timing input.

`MatchActorState` adds `EquippedSlot`, `Attack` and `StaggerImmuneUntilTick`. Its existing constructor appends those optional arguments. `MatchAttackState` contains `StartedTick`, `WeaponEntityId` (empty for fists), and an immutable sorted unique `HitBodyIds` ledger. `WordboundCombatPolicy.EquippedItemId(snapshot, playerId)` returns the selected body ID, or empty while unarmed/carrying furniture.

Wire schema and canonical version remain the explicitly approved pre-release version 2, protocol `wordbound-v2`. Actor fields are required: `equippedSlot` integer, `staggerImmuneUntilTick` decimal string, and `attack` as an empty array or one object containing decimal-string `startedTick`, string `weaponEntityId` and bounded `hitBodyIds`. Earlier development schema-2 packets without these fields are rejected; there is no implicit migration.

## Authoritative behavior

- Each phase uses ceiling seconds × 60 ticks. Wind-up, active window and recovery derive from the selected catalogue melee statistics or `GameRules.Unarmed`. Existing rules have no attack stamina cost.
- Each active tick checks host-captured body positions/rotation, 3D root distance, front arc, runner-local line of sight, mode/team/protected-resource permissions and target immunity. One swing can hit multiple eligible bodies, once each, including a target entering late.
- Player damage uses `Melee.Damage`; item durability uses `BreakPower` directly in economy durability units. Hit stun is capped by rules and has a persisted stagger-immunity deadline. Knockback uses the existing two-times speed convention and stays within canonical velocity bounds.
- A held weapon wears by one durability on its first active tick, including a missed swing, matching existing held-weapon behavior. Breaking returns letters once, clears its slot and cancels the remaining swing.
- Carried map furniture is separate from Held slots: it holsters the weapon, blocks equip/attack and remains available through existing carry/drop behavior. Drop/transfer, crafting, stagger, death, disconnect, return and selection change cancel incompatible swings without refunding recovery.
- The complete tick remains transactional. Restored attacks retain their start, selection, hit ledger and weapon wear. Migration wall time never advances combat simulation ticks. Actual disconnected players cancel their attacks.

## Integration hooks and limits

Root owns Gameplay/Presentation integration. Only the Held body selected by `EquippedItemId` should appear in the hand; Carried furniture keeps its distinct full-size presentation. A replicated selected flag may be added by that owner for immediate replica visuals, derived from the same canonical selection.

The proof hit volume is deliberately conservative: it measures canonical body origins, not nearest points on large collider surfaces. It still uses actual host physics poses and registered runner-local obstruction checks. Native large-prop reach/animation feel is unverified and may need an additive closest-point physics query. Shield/block/dodge commands, client aim, full thrown impact simulation and ranged weapons are outside this slice.

## Verification

Final affected real-NUnit suite: **164/164 PASS**, 13 seconds, including **36 combat cases** plus 128 existing authority/snapshot/economy/deployment/physical-plan cases. Evidence: `TestResults/runtime-puzzles/combat-final.trx`. The protected-resource fixture was corrected to exercise actual renewable letter tiles; the runtime does not create protected furniture for these sources.

Current Rules netstandard2.1 compilation: **PASS, zero warnings/errors**, 4.84 seconds, using `Tools/RulesHarness/Rules/Rules.csproj`. Fresh DLL: `Logs/runtime-puzzles/rules-bin/Wreckabulary.Rules.dll`. No Unity or SDK import was started.

Native Unity and live multiplayer combat: NOT RUN in this lane. Root owns those checks.

Next: root integrates this commit, wires selected Held visibility/client equip controls and verifies native range/line-of-sight, impacts, held equipment and multiplayer recovery with the new required schema-2 actor fields.
