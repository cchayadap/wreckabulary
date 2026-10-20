# Engine adapter contract

`AuthoritativeMatch` is an engine-free rules controller. It is not a network transport or a complete Unity scene.

## Construction and identity

Construct with the pinned `MatchBinding`, `ModeDefinition`, resolved course, geometry and item catalogues, gameplay rules produced by `ModeDefinition.CreateGameplayRules`, and an `IWordboundWorldQuery`.

The binding sorts player IDs. That sorted index is the economy player ID and `player:<index>` world body. The transport supplies `AuthenticatedCommand.ActorId`; never read actor identity from a client command payload.

The world query is a trusted host-only interface:

- `HasLineOfSight(actorId, from, to)` supplies obstruction checks. The controller separately enforces a three-metre interaction range.
- `CanDeploy(actorId, recipeId, position)` supplies collision/support/placement rules.
- `PuzzleSatisfied(actorId, puzzle, placedItem)` supplies the physical predicate for wind, balance, route and timing puzzles. The controller separately checks the current ordered objective, prerequisites, team ownership, position, recipe and letter cost.

Only the Unity authority may supply `hostBodyStates`, `hostObservations` and `HostDamage`. Do not expose these arrays as unverified client RPC payloads.

## Tick and commands

Call `Advance(wallClockMs, commands, hostBodyStates, hostObservations, hostDamage)` once per 60 Hz simulation tick. It returns `MatchTickResult { Advanced, Receipts }`. If `Advanced` is false, do not advance host physics or discard queued commands. `IsPaused` covers migration, a completed result, and a full two-second provisional window.

The journal orders a batch by authenticated actor ID and command sequence. Retained receipts survive migration. Replay returns the prior effect without applying it again. An exception rolls back the entire tick, including pose updates, reservations, world objects, pending puzzle completion and the journal.

| Action | Fields |
| --- | --- |
| CollectLetter | `Target = tile:<id>` |
| BeginCraft | `Value = allowed recipe ID` |
| CancelCraft | No target |
| PickUp | `Target = item:<id>`, `Revision = observed item revision` |
| Drop / Throw | `Slot >= 0` for a carried recipe; `Slot = -1` for carried map furniture |
| Deploy | `Slot >= 0`; placement uses authoritative facing and the host placement query |
| GiveLetter | `Target = teammate player ID`, `Value = one A–Z letter` |
| Transfer | `Target = teammate player ID`, `Slot >= 0` |
| TakeKeepsake / DropKeepsake | Team is derived from the authenticated seat |
| UsePuzzle | `Target = puzzle ID`; `Value = deployed item:<id>` for recipe puzzles |

Host checkpoint and finish observations still require the actor to be inside the correct authored module and to satisfy the ordered progress state. Input `PuzzleSatisfied` and `Knockout` events are ignored. Puzzle commands create validated puzzle events; trusted damage creates knockouts after mode and protection checks.

World pose inputs only update physical values. Existing ownership, source, recovery generation, active state and kinematic classification remain authoritative.

## Durable publication and recovery

1. At a completed tick, call `PublishSnapshot(out json)`. Keep the returned `SnapshotStamp` with that exact immutable JSON.
2. Upload the candidate through the transport's durable migration mechanism.
3. Only after the durable operation succeeds, call `Acknowledge(stamp)`. A peer receipt alone does not establish durable storage.
4. On migration, call `PauseForMigration()`. Select the transport/coordinator's actual durable committed candidate, import it with `MatchSnapshotCodec.Import`, then call `RestoreCommitted(snapshot, stamp, nextEpoch, wallClockMs)`.
5. The restore requires the matching full-state hash, binding, sequence, tick and prior epoch; it advances exactly one epoch. Restore the engine's physical bodies and mechanism components from the returned controller state before resuming simulation.

An acknowledgement of a snapshot before the result tick cannot confirm that result. The runtime must publish the result-containing snapshot and await its acknowledgement. Migration restores elapsed race time from simulation ticks and preserves absolute reconnect deadlines.

`Capture(sequence)` is useful for rendering/inspection outside a tick. Capture, publish, migration and connection changes are not reentrant tick operations.

## Remaining engine work

The Unity adapter must supply movement and combat contacts, collision filtering by mode/team, accurate visual placement, interaction prompts, continuous wind/balance/timed mechanism state and restoration, and camera/UI. The controller's initial mechanism handling records solved timing; continuous unsolved mechanism timing needs its own validated host observation seam when those components are implemented.

The controller uses small proof templates. Initial two-seat payloads are approximately 5.6–18.8 KiB, before receipt and entity growth. The codec bounds total input at 8 MiB. A transport may enforce a stricter explicit size limit; it must not truncate snapshots.
