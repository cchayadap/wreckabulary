# Wreckabulary

Project facts for every session. Keep this file short (80 lines at most) and true. The harness procedure lives in `Docs/kit/HARNESS_RULES.md`, imported by the managed block at the end of this file; the build spec is `Docs/TASK.md`.

## Project

- Game: Wreckabulary, a physics word game: smash furniture, collect its letters, spell equipment, and solve or disrupt.
- Engine: Unity 6000.6.3f1, URP 17.6, Input System 1.20. Online networking is under evaluation; no Photon SDK is installed.
- Unity project folder, relative to this workspace: `.`.
- Player build name: `Wreckabulary.exe`. Log and console prefix: `[Wreckabulary]`.
- Authority: direct owner instructions, then the approved `docs/wordbound/PLAN.md`. `docs/wordbound/STATUS.md` records actual implementation and evidence. Kit templates must not change approved scope or add approval requirements.

## Layout

| Path | What |
|---|---|
| `Docs/` | spec, plan, state files, gate cards and ledgers |
| `Tools/kit/` | kit tools (hooks, harness, gate, QA, generation); do not edit mid-run |
| `BuildNotes/<wave>/` | lane progress files, done markers, context packs |
| `ArtSource/` | generation inputs, receipts and outputs |
| `Assets/_Project/` | Unity gameplay, engine-free rules, editor tooling, tests and content |
| `Web/` | existing website and browser game; separate from the new online coordinator |

## Verification ladder (measured times go here after M0)

| Rung | Command | Time |
|---|---|---|
| R0 type-check | `node Tools/kit/gate/game_gate.mjs check all --json` | 6–14 s |
| R1 engine-free tests | `node Tools/kit/gate/game_gate.mjs core --json` | 39 s, 225 baseline tests |
| R2/R3 compile | `node Tools/kit/gate/game_gate.mjs compile --json` | 1041 s initial cold import; later gates reuse Library |
| R2/R3 tests | `node Tools/kit/gate/game_gate.mjs test editmode --json` / `test playmode` | 307 s for 353 editor tests; 309 s for 115 focused runtime tests |
| R4 player | `node Tools/kit/gate/game_gate.mjs build --json`, then `smoke --json` | (measure) |

## Code rules

- Preserve `Wreckabulary` namespaces and engine-free `Wreckabulary.Rules`. Keep course documents separate from schema-1 home saves.
- Version deterministic generators and send resolved manifests online. Preserve stable entity IDs and all-or-nothing snapshots.
- Only the integrator changes shared catalogs, packages, scene registration and lobby/player hooks. Let Unity generate asset metadata.

## Owner boundaries

- The owner explicitly authorized the implementation plan, isolated `sethypagna/*` worktrees and commits after verified steps on 2026-10-20. This authorization takes precedence over the newly installed kit's default commit restriction. Use existing SethyPagna Git identity; no assistant coauthor trailers.
- The original `wreckabulary` checkout's dirty font and Lunar work belong to another direction. Never alter them.
- The owner alone approves: style lock, feel gates, golden images, spend caps, licences, releases, store pages.
- Human-only steps are listed in `Docs/HUMAN_STEPS.md`.

## Machine notes

- Windows PowerShell; Intel Core Ultra 7 155H / Intel Arc. Blender 5.2.2, .NET 9 and Node 24 are installed.
- The app workspace parent is not a Git repository. Use the actual isolated checkout path explicitly.
- Use native collaboration agents when the kit's workflow runtime is unavailable; preserve exclusive path ownership and durable progress reports.

<!-- >>> game-studio-kit (managed; edit outside this block) -->
@Docs/kit/HARNESS_RULES.md
<!-- <<< game-studio-kit -->
