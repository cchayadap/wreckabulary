# Game Studio Kit Core (`com.gamestudiokit.core`)

Runtime and editor code that the Game Studio Kit's agents and gates rely on in a Unity 6.3 project. `kit.mjs` installs
it as an embedded package in `Packages/com.gamestudiokit.core` and replaces it on update. Do not edit it here: change
the kit sources and re-run `node skills/game-studio-run/scripts/assemble_package.mjs --project <U>` (or `kit install`).
The assembler keeps a manifest (`.gsk-package.json`, ignored by Unity) and never overwrites a file you changed or
deletes a file it did not install; `--force` overwrites changed files after copying them to
`<U>/Logs/gsk-package-backups/<stamp>/`.

## Assemblies

| Assembly | Where | What |
|---|---|---|
| `GameStudioKit.Runtime` | every platform | QA runner and probes, perf probe, locomotion helpers, capture, audio event probe |
| `GameStudioKit.Determinism` | every platform, engine-free | `Pcg32` streams and `Hash64` (FNV-1a 64) for seeds and state hashes; define `GSK_NO_DETERMINISM` to drop it |
| `GameStudioKit.Editor` | Editor | import postprocessors, `AvatarProbe`, `AnimSheetCapture`, `WorldAuditEditor`, `BuildReportExport`, `KitAudioImport`, `KitCli` |
| `GameStudioKit.Editor.Recorder` | Editor, only with `com.unity.recorder` ≥ 4.0 | `KitRecorder` (MP4 or PNG sequence from a tagged camera, Play Mode only) |
| `GameStudioKit.Editor.Pipeline` | Editor, only with `com.unity.pipeline` ≥ 0.8.0-exp.1 | the `kit_exec` command for warm `unity command` calls |

## QA builds and shipped builds

`QaRunner` and QA scenarios compile only under `GSK_QA || UNITY_EDITOR`. Define `GSK_QA` in the Release-QA and dev
build profiles, never in the shipped release profile: shipped players then contain no QA runner and ignore
`-qa-scenario`. `PerfProbe` compiles everywhere and does nothing unless the player is started with `-qa-perf`.

Player arguments: `-qa-scenario <name> -qa-seed <int> -qa-out <abs dir> [-qa-cameras <abs path>]` run a scenario and
write `<qa-out>/checks.json`, then quit with 0 (PASS), 1 (FAIL) or 2 (ERROR). `-qa-perf [-qa-perf-seconds <n>]`
writes `<qa-out>/perf.json`. Optional: `-qa-probes-config`, `-qa-timeout`, `-qa-commit`, `-qa-build-id`,
`-qa-perf-warmup` and `-qa-mem-min-seconds`. The `GSK_QA` compile guard is the only lock on QA runs: the interim
`-qa-token` is dropped (CONTRACTS §3.6). A run that builds no steps ends in ERROR, a run whose steps were all skipped
in FAIL, and a player run stuck past `-qa-timeout` is ended by a watchdog that still writes `checks.json`.

## Editor entry points

Run through `game_gate exec <Method> -- <json>` (they read `KitCli.Args` and answer with `KitCli.WriteResult`):
`GameStudioKit.Editor.Anim.AvatarProbe.Run`, `GameStudioKit.Editor.Qa.AnimSheetCapture.Run`,
`GameStudioKit.Editor.Qa.WorldAuditEditor.Run` / `.Snapshot` / `.Diff` / `.TextureAudit`,
`GameStudioKit.Editor.Audio.KitAudioImport.Audit`.

## Clean-room note

The locomotion and probe code is original work written from the kit's own specifications and public
documentation. It contains no code from, and nothing derived from decompiled builds of, any other game.
