# Wordbound held defense

STATUS: SOURCE IMPLEMENTED — COMPILATION, TESTS AND NATIVE ACCEPTANCE NOT RUN

Root assigned acceptance finding A4 after the FFA content checkpoint. James completed practice/schema 3 in `27e0c53` and explicitly released the actor, codec, authority, physics and fingerprint hooks to this lane. Online owns protocol 3 wire/service/export. Root owns PlayerView, HUD and Guide. The exact imported baselines and integration instructions are in `BASELINE.md` and `HANDOFF.md`.

Existing catalogue/legacy contract is unchanged: PLATE raises after0.12s, protects120degrees, blocks100percent and uses its move-speed multiplier; SHIELD raises after0.22s, protects360degrees, blocks100percent and slows to0.55. HealthModel.ApplyHit reduces knockback to0.3 on a block, suppresses hit-stun and wears the shield by the blocked damage. PlayerCombat requires a selected shield, free hands/actions and continuous held input, and treats attack-held with a shield as block-held. Movement slows only after the raise delay. No stamina or new damage-cost rule is introduced.

## Frozen proposed data and input

- Append `Block` to the command enum after `PracticePuzzle`. Envelope: empty Target, Revision0, Slot-1, Value exactly `Hold` or `Release`. Actor identity remains the authenticated command sender. Root HUD supplies `blockHeld || (attackHeld && selectedItem.Shield != null)` and releases on menu/focus loss.
- Required actor JSON property `defense` is an empty array or exactly one object containing `itemEntityId`, `startedTick`, `untilTick`; ticks use the existing canonical signed-integer string representation. `MatchActorState.Defense` is nullable immutable `MatchDefenseState`.
- Hold binds the currently selected live Held shield item; `StartedTick=Tick+1`, `UntilTick=Tick+1+30`. Same-item refresh extends only UntilTick and preserves StartedTick, so raising is not restarted. HUD refreshes approximately every 10/60 seconds while held; absence of a refresh expires after 30 authoritative ticks. It must not wait solely for coarse replica snapshot updates. A repeated journal sequence cannot extend the lease. Release clears immediately.
- Snapshot invariants: `1<=StartedTick<=snapshotTick<UntilTick<=snapshotTick+30`; selected slot/holder/item match, item has valid existing shield stats, actor is connected/alive/actionable, no craft/carried furniture/dodge/stun/attack/Return/result/migration. Finished race teams cannot hold defense. Expiry is normalized every authoritative tick; recovery preserves exact timing rather than granting a new lease.
- Movement input gains optional `HasFacing` and `FacingYaw`, finite degrees in[-180,180]. Default false preserves existing movement-facing behavior. True allows the admitted host motor to face the camera while stationary; damage uses the canonical body's actual rotation. Invalid yaw rejects that input rather than overwriting pose. Root owns PlayerView emission; physics file ownership remains coordinated separately.

## Narrow integration hooks required

1. Actor constructor/property/copy helper preserve nullable Defense; strict codec requires the new key and exactly0/1 entries. Domain validation calls the defense validator. A new policy hash must enter the rules fingerprint before the final v3 catalogue/export.
2. Command switch delegates Block to the new partial. Equipment normalization clears defense when the selected shield changes or becomes unavailable; per-tick normalization clears expired leases and incompatible action/progression states before damage and before snapshot publication. BeginAttack cannot coexist with held defense.
3. ApplyDamage obtains the new partial's defense result only after ordinary friendly-fire/protection/ownership checks. It applies remaining health damage, suppresses stun if blocked, and wears the held shield exactly once by blocked damage. A broken shield releases normal catalogue letters and clears selection/defense; the breaking hit retains its computed block.
4. Melee knockback uses the result's0.3 multiplier and no upward kick for a block, matching legacy behavior. BALL uses the incoming projectile side/contact geometry rather than the distant thrower's location. Hazard damage without an attacker/direction remains unblockable. Actual accepted-hit ledgers remain owned by the existing attack/flight paths.
5. Physics CanMove multiplies the existing scale by the selected shield's catalogue multiplier only while raised. Presentation/HUD show raising versus raised using the canonical state; they do not supply authority.

Online confirmed the string action/value envelope and required actor field fit the pending v3 boundary. Its service has no actor-field allowlist; strict C# validation remains authoritative. The golden C# fixture/export waits for the combined practice+defense green assembly, including an actual active PLATE block created through ordinary collect/craft/equip/Block commands.

Acceptance must cover frontal/rear/omnidirectional hits, exact raise and lease boundaries, movement multiplier, wear/break/conservation, protected/disabled actors, malicious payloads, retransmission, equipment/craft/disconnect/Return transitions, strict active/empty-state round-trip and resumed expiry. Native facing, controller input and visual acceptance remain separate root-owned gates.

Source checkpoint: both additive production files and the shared hooks are implemented. Prepared tests cover the policy, actual authoritative acquisition/combat/state transitions and the movement input policy. They are NOT compiled or integrated yet. Root retains HUD/PlayerView/Guide. The compiler hold was not released before root's status handoff, so the next action is the focused gate documented in `HANDOFF.md` after root grants the next slot.
