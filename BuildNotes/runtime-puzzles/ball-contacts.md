# BALL host contact resolution

STATUS: SOURCE VERIFIED — NATIVE VERIFICATION PENDING

Catalogue correction discovered by the first real-catalogue regression run: PLATE is a shield and has no ThrownStats. BALL owns the recoverable speed16/damage12 stats previously misattributed to PLATE. Root approved the corrected BALL scope, explicit ModeDefinition recipe/version/hash update and host thrown ContinuousDynamic behavior. The initial focused run compiled but reported 18 new flight failures because the draft correctly refused to treat PLATE as a projectile; 46 existing cases passed. No invented item stats were added.

Root authorized a bounded BALL-only contact slice from integrated `f336696`. Ownership: Rules Wordbound state/authority/codec/validation/tests plus ModeDefinition; PhysicsWorld and NetworkBody contact collection; additive Gameplay observations/controller and FusionAuthority forwarding. No Session, service, command wire, HUD or native asset changes. Unity runs remain root-owned.

Contracts sent to root and online owner: immutable `MatchThrownFlight(ItemBodyId, LaunchRevision, StartedTick, ThrowerId, HitBodyIds)` persisted in required schema-2 payload `thrownFlights`; host-only `HostThrownContact(ItemBodyId, LaunchRevision, TargetBodyId, ContactPoint, RelativeSpeed, Tick)`. No client damage/actor/target assertion is accepted. The contact collector must use registered colliders in the isolated runner's actual physics step.

BALL uses the actual enabled recoverable catalogue definition (speed 16, damage 12 in current content), a 120-tick legacy flight window, one successful hit per body per flight, mode/team/protection checks and canonical knockback. Generic furniture damage and other projectile families remain unsupported. Apply contacts before settling sleeping items so the thrower and hit ledger survive that last collision; canonical rollback and migration retain the exact ledger. ModeDefinition version 2 adds BALL to the recipe allowlist, changing every mode hash and therefore admission bindings. PLATE retains its shield definition unchanged.

Implemented the bounded flight DTO, canonical codec, exact-set validation, transactional authority and runner-local collision collector. ContinuousDynamic is enabled for thrown physical bodies and reset before kinematic pause/held transitions. The collector requires a completed host physics step, deduplicates body pairs and fails the step on saturation.

Verification checkpoint: the first corrected BALL suite compiled and ran 226 actual NUnit cases: 225 passed and one failed. The failure identified a real between-tick Return path omission; Return now settles an existing flight before capture. Focused follow-up adds Return idempotency, Brawl damage, protected supply, break-power and pickup/rethrow causality cases. Mode limits fixture now expects the approved 13 recipes. Source review did not find additional caller edits beyond the scoped forwarding paths.

Source review also found a lethal-disconnect edge: MatchProgress originally required a currently active attacker to resolve every knockout. Root authorized a narrow internal exact-event credit path. Only the specific Knockout observation produced by validated BALL damage receives that exception; crafting, checkpoints, unrelated observations, protected targets, teammates and returned/expired seats retain their guards. Temporary disconnect preserves the launched flight; permanent Return or expired reconnect grace settles it, including recovery after that deadline.

Final verification:

- Actual NUnit, `TestResults/runtime-puzzles/ball-focused-final.trx`: **101/101 PASS**, covering thrown contacts/buffer, combat, Return, mode contracts and progression. This supersedes the earlier Return failure. A subsequent protected-supply test initially looked for a protected furniture entity; current authority materializes those supplies as letter tiles. The corrected fixture tests those real protected tiles, with no production behavior weakened.
- Fresh netstandard Rules build: **PASS, zero warnings/errors**.
- Updated local Fusion bridge compiled against installed Unity 6000.6.3f1 / Fusion SDK and Photon.Realtime: **PASS, zero warnings/errors**.
- Physics, Gameplay and Puzzles source compiled against that fresh Rules/bridge and installed Unity/Fusion SDK: **PASS, zero warnings/errors**.
- Native Unity collision callbacks, CCD transitions, two/eight-peer play and actual migration behavior: **NOT RUN in this lane**.

Build recipes remain ignored local evidence under this directory. Fresh outputs are `Logs/runtime-puzzles/rules-bin/Wreckabulary.Rules.dll`, `bridge-bin/Wreckabulary.Online.Fusion.dll` and `reference-bin/ReferenceCheck.dll`. The broad earlier affected run passed 225 cases before the Return correction; the final focused suite verifies all subsequent changed behavior and should not be represented as a fresh complete-project run.

Integration: cherry-pick the source increment onto the current integration branch. Preserve root's independent GameplayController AttachSession/PrepareScene lifecycle fixes; this increment changes only its AdvanceHost forwarding hunk. Required schema2/canonical2 `thrownFlights` and ModeDefinition version2 invalidate historical online content/checkpoints that omit these semantics. Protocol stays `wordbound-v2` under the approved pre-release contract. Regenerate the real C# cross-language checkpoint fixture from the updated codec.

Next recoverable action: root runs `WordboundBodyMotionTests`, native runtime/physics tests, and an actual host BALL collision/recovery scenario. Native prefab metadata and weaving remain root-owned. The physics collector requires real completed host-step evidence; directly calling AdvanceHost without such a completed step fails closed. No client damage, arbitrary target label, generic furniture projectile damage or unsupported projectile family was enabled.
