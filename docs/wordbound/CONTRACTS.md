# Wordbound implementation contracts

Status: implementation contract; features and acceptance remain governed by PLAN.md.

## Ownership

| Lane | Owned additions | Shared changes requested from integrator |
| --- | --- | --- |
| map-core | `Scripts/Rules/Wordbound/Course*.cs`, matching rules tests | Rules assembly and harness determinism references |
| puzzle-race | Mode definitions, authoritative match state, snapshot codec, race runtime | Existing players, inventory, combat, session, HUD and lobby |
| map-workshop | Course edit history, storage, workshop tools and UI | Entry point in existing Creative Workshop |
| online | Separate `OnlineServices/`, transport adapters and service contracts | Package installation and runtime authority integration |
| pvp-assets | WordboundPvP source collection and additive Blender builder/validator | Shared item/model/material catalogs and import registration |

Only root runs Unity or changes shared contracts. Native collaboration agents keep a progress file, verification evidence and a next action. No lane writes to the original maintained checkout.

## Mode identifiers

Use `TimeRun`, `TeamRace`, `BrawlRace`, `FreeForAll`, `Freestyle` as stable serialized IDs. Display names come from the mode definition. Race formats are `Solo`, `Duo`, `Squad`, with capacities 1, 2 and 4. Time Run is one team of up to four; FFA is individuals; Freestyle is private, up to eight.

`ModeDefinition` owns objectives, timer, team allocation, combat/collision policy and permitted recipes. Map-core accepts a `CoursePlayRequirements` projection: mode ID, Circuit/Arena/Explore capability, team count/capacity, maximum occupants and ownership policy. It does not reference match state or networking classes.

Every supported race format needs a one-person solution because Fill Off permits undersized parties. Do not scale objective difficulty silently with the number of occupied seats.

## Course data

- Keep schema-1 `HomeLayout` semantics and storage unchanged.
- New editable files identify themselves as `wreckabulary.course`, schema 1.
- Resolved manifests identify themselves as `wreckabulary.resolved-course`, with independent manifest, generator, RNG and canonicalization versions.
- Use bounded data, allowlisted IDs and typed presets. Reject scripts, URLs, unknown fields, duplicate IDs, invalid references, non-finite values and oversized inputs.
- Use integer millimetres for authored transforms and a fixed hexadecimal 64-bit seed.
- Use the installed `GameStudioKit.Determinism` implementation. Derive streams from seed, generator version, purpose, lap, stable scope ID and attempt. Do not seed streams from the entire source-document hash.
- Canonical payload hashes use SHA-256 over UTF-8, excluding the hash field. Preserve ordered checkpoints; sort unordered entities by stable ID.
- Return an immutable complete manifest or structured validation failures. Never return a partially playable map or silently relax constraints.
- Resolve static geometry once as the union of branches. Three lap plans select routes and state without replacing occupied geometry.
- Validate resource multiplicity and consumption at each progression prefix, including recovery supplies reachable before the objective they support.
- Static validation proves declared relationships. Physics reach, timing, comfort and visual collision agreement require Unity tests and playtests.

## Authority and snapshots

The authoritative simulation owns pickup, crafting, damage, inventories, puzzle completion, keepsakes and match results. Network clients request actions using authenticated player identity and a monotonic command sequence. Repeated requests must not create duplicate effects.

The transport supplies actor identity outside the request body. Position, contact, range, damage and mechanism observations come from the trusted host physics adapter. A client cannot report an authoritative puzzle completion or inventory total.

Resolve one simulation tick as a batch. Order contested actions by stable player ID and command sequence, never transport arrival order. Resolve valid finishes together before applying that tick's timeout; equal valid finish ticks draw. Public race timeouts compare completed ordered objectives, then the tick at which that progress was achieved. Persist command receipts and payload hashes with the processed frontier; reconnect never resets the sequence. A reused sequence with different content is rejected. Epochs fence stale connections without erasing deduplication history.

Snapshots bind match ID, roster hash, resolved-course hash, protocol version, epoch, sequence and simulation tick. Include players, inventories, stable entities, carried relationships, pending transactions, platform timing, teams/laps, puzzles, keepsakes, narrative flags and the last applied command sequence per player. Restore one consistent state.

Also bind the mode-definition hash and tick rate. Include RNG states when used, the next entity-ID allocator, despawn tombstones, resource revisions, reserved craft letters, committed spend and spawned-item IDs. Snapshot acknowledgements identify match, epoch, sequence and state hash. A stale or wrong-hash acknowledgement cannot confirm results. The two-second provisional window is measured from the last acknowledged tick; exceeding it pauses authoritative progression rather than silently increasing the rewind budget. Serialize 64-bit counters as decimal strings at the JSON boundary.

Race time uses simulation ticks and pauses during migration. Reconnect expiry uses wall-clock time and a 120-second grace. Final results remain provisional until the snapshot containing them is acknowledged. No code-only test establishes successful real host migration.

Restoring a snapshot preserves the original reconnect deadline, player entity and seat; migration does not renew grace. Recovery supplies and keepsakes have stable IDs, objective scope and recovery generations so repeated requests cannot duplicate them. Team ownership is checked by every interaction and collision adapter, including pickup, grab, placement, crafting and puzzle operations.

## Coordinator boundary

The new Cloudflare service is independent of the existing Web/score service and transport SDK. It mints authenticated guest identities, revisioned parties, idempotent queue attempts, atomic whole-party team reservations and signed admission grants.

Fill Off reserves the party's entire team capacity, leaving unused seats empty. Fill On only admits consenting humans before match start. Do not split parties, introduce bots, change format silently or reopen disconnected seats to strangers.

Public Quick Play uses only approved built-in maps. Private workshop maps with random teammate Fill use a separate explicit Custom Fill opt-in pool and content-manifest acceptance. Ordinary Quick Play users cannot be silently assigned to workshop maps.

The coordinator cannot prove that a client claiming to be the new game host is the elected Fusion host. Transport authentication, host election, physics synchronization and snapshot transfer need a separate SDK integration proof.

Queue cancellation and match start are linearized in the same pool object. Cancellation tombstones prevent delayed enqueue from reviving an old attempt. Cross-object transitions are durable and retryable; party revision changes follow successful cancellation. Admission grants bind player, party revision, attempt, reservation generation, match/team/seat, protocol, mode hash, course hash and roster hash. Every party member accepts that exact content manifest before whole-party admission completes.

## Current network dependency

The approved plan names Photon Fusion 2.1.3 with Physics Addon 2.1.2, conditional on compatibility with Unity 6000.6.3f1. No SDK is installed and no account is configured. Keep service and simulation work independent until that proof is possible; do not label a local transport substitute as completed online play.
