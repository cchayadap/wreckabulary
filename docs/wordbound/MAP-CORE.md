# Wordbound course core

Status: implemented and verified by 75 course test cases in the real NUnit core gate (300 total tests passed), including 11,000 seeds across 11 proof configurations. Engine geometry and physics validation remain separate.

The engine-free API is in `Wreckabulary.Rules.Wordbound`. Existing `HomeLayout` schema 1 remains untouched. The separate format `wreckabulary.course` has schema 1. Authoring DTO fields are mutable, and `CourseDocument.Clone()` makes a deep copy. Workshop drafts may have incomplete topology; `ValidateStructure` and the strict codec still require bounded, non-null typed data and valid stable entity IDs.

## Public entry points

- `CourseJson.ImportDocument(string)` returns `CourseImportResult` with `Success`, `Document`, `Validation` and `Issues`.
- `CourseJson.ExportDocument(CourseDocument)` emits canonical JSON, or throws for invalid structure.
- `CourseValidator.Validate(document, catalogue, play)` checks authored geometry, references, graph topology and mode compatibility.
- `CourseResolver.Resolve(document, catalogue, new CourseResolveRequest(seedHex16, play, maxAttempts: 16))` returns `CourseResolution` with immutable `Manifest`, `AttemptsUsed` and structured issues.
- `CourseJson.ExportManifest(ResolvedCourse)` and `ImportManifest(string, catalogue)` serialize and validate an exact resolved course. Import validates choices and placement constraints rather than accepting a hash alone.
- `CourseValidator.ValidateResolved(manifest, catalogue, play)` also checks the expected match projection.
- `CourseTemplates.RaceCircuit()`, `CombatArena()`, `FreestyleWorld()` and `ProofCatalogue()` supply small proof fixtures. These are not the flagship course or the production asset catalogue.

`CoursePlayRequirements(modeId, capability, teamCount, teamCapacity, maxOccupants, ownership)` is independent of mode definition and match snapshot types. `TimeRun` uses Circuit/1/4/4/TeamIsolated; TeamRace uses Circuit/2/(1,2,4)/(2,4,8)/TeamIsolated; BrawlRace uses the same capacities with TeamProtected. FreeForAll uses Arena/0/1/(2–8)/Individual; Freestyle uses Explore/0/0/(1–8)/Shared. Capacity describes the map contract; an undersized locked party still uses the chosen format. Every required puzzle has a one-player solution.

## Coordinate and data model

Positions and bounds use integer millimetres. Module transforms have quarter-turn yaw. A module references an approved module definition and a layer of the same height. Connections explicitly declare floor endpoints, width, direction, kind and optional gate puzzle. Stairs ascend with a maximum 35 degree slope and clear approaches/landings; traversal presets still require later physical acceptance.

Route sections contain weighted alternatives with ordered connection and checkpoint IDs. Every circuit lap names an ordered section sequence; all three lap plans are resolved before a match. Geometry is the immutable union of authored modules and connections. A checkpoint may use `EntryConnectionId` to distinguish revisiting the same module. The last checkpoint reaches the lap finish.

Puzzles declare prerequisites, exact letter multiplicities, consumption, reward letters and protected recovery supplies. Validation checks each selected progression prefix: resources and their recovery must be reachable before consuming them, including from the preceding safe checkpoint, and letters must be carryable back to the puzzle before its gate unlocks. Recovery anchors and completed remote puzzles must allow a return to the current checkpoint. Supplies and critical resource opportunities are copied identically per team. Props are also copied for clean races; Brawl shares ordinary props and protects team resources.

Random zones specify one owning module, allowed layer IDs, bounds, purpose, weighted approved assets, count range, probability, grid, spacing and allowed yaw. `Placement="Fixed"` additionally specifies `FixedPosition` and `FixedYaw`, with exactly one asset, one guaranteed instance and a matching single yaw. Both paths validate actual asset footprints against modules, reserves, connection corridors, starts, recovery and existing placements.

## Versions, bounds and determinism

Schema, generator, canonical encoding and RNG versions are independent. Current versions are 1/1/1 and `gsk-pcg32-1`. Seeds are exactly 16 lowercase hexadecimal characters. Streams use installed GameStudioKit `Pcg32` and `Hash64`, keyed by seed, generator namespace, purpose, stable scope ID, lap and attempt. Document hashes never seed randomness. Purposes are Routes, Props, Resources and Decoration.

Canonical JSON sorts keys and unordered entity collections by ordinal stable ID; ordered route, checkpoint and puzzle sequences retain their order. SHA-256 hashes canonical UTF-8. The manifest excludes only its own hash field while hashing, and includes source hash, catalogue hash, versions, play policy, authoring document and all resolved choices/placements/supplies.

Import caps documents at 2 MiB, manifests at 8 MiB and nesting at 24. It rejects duplicate/unknown/missing fields, nulls, invalid Unicode and non-integer or out-of-range numeric literals; exponent and decimal syntax are rejected rather than rounded. Structure-only document import can retain unfinished references; full validation and manifest import reject unknown content IDs. Coordinates are bounded to ±2 km horizontally and ±256 m vertically; there are at most 16 layers, 256 modules, 1024 connections/puzzles/supplies, 256 zones/checkpoints, 128 sections and 16 alternatives per section. Resolved entities are capped at 8192. Route selection has at most 16 attempts and placement at most 64 samples per item. Failure returns structured issues and never relaxes author constraints.

## Integration limits

Hash validation is integrity validation, not authentication. The authoritative host must validate the manifest against its installed catalogue and expected mode, then send that exact manifest. Ownership domains, spawn assignment, protected recovery cooldowns, checkpoint scoring, object physics and snapshot recovery are runtime responsibilities. The three proof templates use authored module transforms; the resolver selects routes and prop/resource placement within that static union. A later composition stage can author larger module arrangements before resolution. Layer masks constrain a zone's owning module rather than moving occupied geometry. No online editing, gameplay reroll, cross-version replay promise or browser parity is introduced here.
