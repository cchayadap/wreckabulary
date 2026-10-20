# Canonical deployed fields

STATUS: SOURCE VERIFIED; NATIVE ACCEPTANCE PENDING

Baseline: `653f53b`. Root granted Online/Puzzles policy/runtime/tests, the bounded PhysicsWorld/NetworkBody movement hook, a new movement environment helper, minimal GameplayOptions/Controller forwarding, and the necessary deployment-facing rules regression. The four existing hook files were compared against integration `5951865`; only line endings differed. Native execution remains root-owned.

Goal: actual catalogue FAN wind and CLOCK slowdown, checked against host runner-local positions, radius, arc, height and obstruction. Derive lifetime from canonical DeploymentTick, with no local countdown or stale post-migration effect. CLOCK changes desired movement speed rather than repeatedly damping velocity; FAN retains the legacy slippery acceleration multiplier so ordinary movement friction does not cancel the field each tick. Fields respect clean-race separation and critical-resource protection.

Implemented:

- `WordboundMovementEnvironment` has validated speed and acceleration multipliers in `(0, 1]`, `Neutral`, and an idempotent minimum `Combine`. Invalid/default values or callback failures freeze the physics world and surface a controller fault.
- `WordboundGameplayOptions.MovementEnvironment` forwards the trusted callback to PhysicsWorld. NetworkBody applies it once to desired speed and acceleration/friction. Craft movement scaling remains independent.
- `WordboundPuzzleRuntime.EvaluateMovementEnvironment(actorId)` evaluates actual registered source/actor poses, canonical next-tick expiry, radius, cone, vertical band, runner-local obstruction and live admission. CLOCK applies the catalogue speed multiplier; FAN applies the existing slippery acceleration factor `0.1`. Overlapping clocks do not multiply repeatedly.
- Existing `BeforeHostPhysics` applies FAN acceleration at 60 Hz to eligible registered dynamic bodies, capped by catalogue forward strength. It excludes inactive/held/kinematic bodies and applies clean-race ownership/protected-resource collision policy. Walls and registered intervening bodies block fields.
- Deploy now writes the authoritative player's quaternion into the placed item's canonical pose; a field no longer retains its stale held-item facing. Snapshot round-trip preserves that orientation.
- The companion placement check rotates its overlap box and four support corners by that same host orientation, and includes the placing player's collider in clearance. It does not validate an axis-aligned footprint and then place a rotated collider or overlap its owner.
- Built-in physical proof plan/content version is now **2**, with explicit FAN and CLOCK practice supplies. The BED/STOOL placement objectives retain their meanings. Source/manifest/catalogue hashes change through their existing canonical binding; online protocol and snapshot schema remain 2.

Wiring:

```csharp
options.MovementEnvironment = actor => puzzles.EvaluateMovementEnvironment(actor);
options.BeforeHostPhysics = (frame, bodies) => puzzles.BeforeHostPhysics(frame, bodies);
```

Construct `puzzles` with the initialized controller and exact `WordboundPuzzleProof` before starting the session, as for the existing puzzle hooks.

Verification: **180/180 real NUnit cases PASS**, 15 seconds (`TestResults/runtime-puzzles/fields.trx`). Includes 15 new field/environment cases and the host-facing deployment regression, plus the prior 164 cases. Rules netstandard2.1 build: zero warnings/errors, 5.37 seconds. Actual installed Unity 6000.6.3/Fusion SDK reference compilation of Physics, Gameplay and Puzzles: zero warnings/errors, 4.78 seconds. Native physics/multiplayer checks: **NOT RUN** in this lane.

Limits: no lingering local contact timer, full field visualization or generalized authored workshop socket profile was introduced. The current proof implements physical fields and its explicit placement sockets; it does not claim arbitrary `RedirectWind` puzzle completion. Thrown items still lack a complete host-contact consumption/impact contract. Root also identified release-at-player-feet collider overlap for the next bounded fix.

Next: root integrates and wires the movement callback, then verifies actual FAN cone/wall exclusion, CLOCK speed and exact expiry/restoration in the native isolated runner. Inspect safe release clearance before the thrown-contact follow-up.
