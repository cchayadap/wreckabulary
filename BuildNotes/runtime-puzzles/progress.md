# Physical puzzle runtime

STATUS: SOURCE VERIFIED; NATIVE PHYSICS ACCEPTANCE PENDING

Owner: `wordbound/runtime-gameplay/physical-puzzles`. Baseline `9d69fa1`; exclusive new `Scripts/Online/Puzzles`, its tests and this directory. Root owns native execution and existing rule/schema files.

The first slice supports only an explicit built-in M1 course: real deployed BED/STOOL placement sockets with continuous supported hold, catalogue jump-pad effects, canonical-tick moving platforms, and host-observed checkpoint proximity. It must use the actual item catalogue and runner-local registered colliders. It must not turn the existing `BuildRoute`, `RedirectWind`, `BalanceWeight` or `TimedMechanism` labels into guessed success conditions.

The course schema currently omits socket/goal/profile data; the item snapshot omits deployment tick/expiry even though FAN and CLOCK catalogue entries have finite lifetimes. Root approved a constrained proof plan first and a coordinated canonical expiry follow-up. This is not acceptance of arbitrary workshop physical puzzles.

## Integration contract

`WordboundPuzzleProof.Create(actualItemsJson, modeId, format, seed)` supports TimeRun, TeamRace and BrawlRace. Its `Mode`, `Course`, `Geometry`, `Items`, `Plan` and `CatalogueHash` are the proof's complete content inputs. `Items` returns a fresh parse to avoid leaking mutable catalogue state. The signed admission content bundle must use this proof's `CatalogueHash`.

The bundle is `SHA256("wordbound.proof-content|1|" + Geometry.Hash + "|" + SHA256(exactItemsJson) + "|" + Plan.Hash)`. Existing admission and MatchBinding already pin an opaque gameplay catalogue bundle hash; no shared interpretation or wire schema changes were made. The plan hash pins its version, exact source/manifest hashes, sockets, timings and platform identifiers. A changed item JSON byte sequence deliberately changes this bundle even when its values are equivalent. The built-in plan cannot be constructed from an arbitrary imported workshop document.

Create `WordboundPuzzleRuntime(controller, proof)` before controller initialization and assign these existing gameplay option delegates:

- `PuzzleEvaluator = runtime.Evaluate`
- `ObserveHost = runtime.ObserveHost`
- `BeforeHostPhysics = runtime.BeforeHostPhysics`

Initialize the controller with the proof content, attach the real session and start normally. The runtime validates the signed mode, course, manifest and bundle on host calls. It only queries the runner's isolated PhysicsScene and registered body/static colliders.

The lower socket `BedBridge` is at `(0,0,0)` and uses BED; the upper template ID `FanDoor` is at `(0,6,0)` and uses STOOL. Both are explicit PlaceObject goals, with quarter-turn yaw zero, 0.25 m centre tolerance, 15-degree yaw tolerance, upright tolerance five degrees and 30 continuous completed ticks of supported unobstructed placement. The stable template ID `FanDoor` does not imply fan/wind behavior in this proof. Item selection checks actual crafted, deployed, active, same-team catalogue identity. Removal, replacement or interrupted sampling resets progress; only a subsequent authenticated UsePuzzle command can commit completion.

UpA and UpB platforms travel along their exact resolved endpoints over 360 ticks, dwelling 60 ticks at each end. Their phase derives exclusively from canonical match tick; restored physics retains the saved pose, and the following simulation step targets the next phase. There are no local elapsed-time accumulators. These lifts accompany the existing traversable course ramps; this slice does not claim physical gate barriers or team-specific door collision.

Permanent deployed catalogue JumpPad items apply their real strength to nearby admitted players through host Rigidbody velocity, with registered obstruction and team collision checks. Timed field forces are not yet implemented in this adapter. The subsequent canonical lifecycle change in deployment.md now supplies activation/expiry state for that future extension. A local launch timer is not introduced. Checkpoint/finish observations require actual supported player proximity to authored recovery anchors and the current objective order.

## Verification

- Installed Unity 6000.6/Fusion SDK reference build: PASS, zero warnings/errors, 4.76 seconds.
- Focused real NUnit tests: PASS 9/9, 622 ms. Includes all three race cards against actual items.json, exact content binding, immutable catalogue copies, socket orientation/activity bounds, periodic and large-tick platform reconstruction, interrupted/replaced-item timer reset, and a real collect/craft/deploy authority flow whose complete snapshot round trip preserves and resumes mechanism progress.
- Native Unity physics, moving-platform riding, callback ordering, jump-pad interaction and multi-peer recovery: NOT RUN in this lane.

## Necessary follow-ups

Course JSON needs explicit bounded authored socket/profile/target fields before arbitrary workshop puzzle export can be accepted. BalanceWeight needs a defined mass/goal; BuildRoute needs a physical route objective; RedirectWind needs target/cone data. These are separate from the built-in proof and are not silently inferred from preset labels. Deployment time/expiry is now covered by the schema-2 follow-up documented in deployment.md; current clients should pin MatchSnapshotCodec.ProtocolVersion (wordbound-v2).

Reliable WordboundAction currently has no player attack/smash intent. Existing host damage is an authoritative observation seam, not an interactive smash control. A future combat slice must add authenticated bounded intent with host timing, arc, reach, ownership and hit queries; this runtime never invents damage from client labels.

Next recoverable action: root integrates this source and the schema-2 lifecycle follow-up, then runs native proof with the exact content bundle and delegates. The 9-case proof build above predates schema 2; its nine policies have also passed in the subsequent 128-case source suite. Native schema-1 evidence is historical and does not establish the new checkpoint format.
