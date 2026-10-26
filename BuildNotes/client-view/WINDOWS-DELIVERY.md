# Windows delivery preparation

## Current baseline — 2026-10-26

Windows `20261026T125941Z`, source `78ac4514810e7267887e2ca7fa549f427d3474de`, Unity6000.6.4f1 passes Home3/3 (`130820`), Workshop5/5 (`130906`) and original equipment6/6 (`130132`), each with exit0, no timeout and zero reported console errors/exceptions/warnings. Eight BAT phases have same-frame measured upright orientation. [Receipt and artifact hashes](windows-reviewed-baseline-20261026.json), [launch instructions](TRY-WORDBOUND.md).

James identified unacceptable holding poses, weak facial/action responses and excessive text panels. This build is the preserved baseline for those corrections, not visual or production acceptance. Current source/draft work and remaining online scope are tracked in [progress](../../progress.md). Earlier entries below are historical; their failures and build folders remain preserved.

## Current tested review build — 2026-10-24T01:01:26Z

Original Wreckabulary models remain the default. Windows QA build `20261024T005254Z` passes on exact source `c7b0d8a933c2235ba7cca7c195799c7964ae3b33`. Its normal-window workshop run `20261024T005757Z-visible-wordbound_workshop_flow` passes **5/5**, exits normally and records no console errors, exceptions or warnings. Exact draft, return and Undo/Redo checks pass. Root personally inspected all six images: compact party card, original cream/teal/Nunito interface, readable Search/footer and grounded original character remain visible. [Build instructions](TRY-WORDBOUND.md).

The 90-update observation lasts 5.004 seconds with a 678.386 ms worst gap, versus 9.371 seconds in the preceding run. This single comparison has different canonical tick counts and does not establish a causal speedup or meet the 60 FPS target. All 89 assigned-camera callback pairs match; callback timing is not GPU or presented FPS.

The first separate equipment run `005453` passes opening and the four-prop world capture, then correctly rejects the first collection as `OutOfReach`. Its image shows an airborne character with artwork root Y=2.548599 m at tick 40. Original mesh identity, unit scale and original prop materials are confirmed; held BAT/HAMMER and successful return are **not verified**. Freestyle's initial keepsake/player overlap is under investigation; no mesh, reach limit or physics workaround has been applied. [First-run evidence](windows-original-equipment-first-20261024.json).

Latest native public Team Race Solo `003910` still fails movement and records seven client frames above one second. The prior `003140` completes movement and a command, then rejects an overdue snapshot transfer. Public reliability across all modes, larger parties, complete match outcomes, recovery targets, final stripped release and human acceptance remain open. All prior work and failed evidence remain preserved.

Build gate: 109.5 seconds; player build: 108.6 seconds, nondevelopment `release-qa`, Unity 6000.6.3f1. [Windows record and 48 artifact hashes](windows-synchronize-workshop-20261024.json). The equipment failure record binds 45 artifacts from the same executable. These are supplemental visible diagnostics, not an official smoke replacement or production acceptance.

## Initial preparation plan (historical)

The gate configuration names `Assets/Settings/Build Profiles/Win.asset`, `Win-QA.asset` and `Win-Dev.asset`. That directory is absent in this checkout as of `49ca736`. Actual Windows builds must create and validate these project assets through Unity before using the configured gate; no gate configuration or kit source was changed.

Unity's public [BuildProfile API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Build.Profile.BuildProfile.html) exposes scene and scripting-define overrides. Its [scriptingDefines documentation](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Build.Profile.BuildProfile-scriptingDefines.html) states that the profile must be active for its defines to apply. The installed 6000.6.3f1 editor remains authoritative for creation and platform APIs; newer reference source is not proof that an API exists locally.

Next:

1. Inspect the installed editor's profile creation API and create missing Windows profiles through a project editor helper invoked by the gate. Preserve the existing scene order and append-only Wordbound scene registration. Verify the Windows platform and exact scene list on each saved asset.
2. Keep `GSK_QA` in the QA profile only. Add a project-owned QA scenario for opening Wordbound, editing a draft, launching the exact local playtest, returning with history intact, and capturing the visible UI. The kit's generic front-end smoke alone does not establish this behavior.
3. Run the required source/native checks, then build and smoke through the gate. Inspect actual screenshots and measured results before reporting delivery. The stripped player and the QA player require separate evidence.

The entries below record subsequent profile creation. Player binaries and published releases remain unverified.

## Prepared source — 2026-10-20T17:23:33Z

The installed `6000.6.3f1` editor metadata was inspected with its bundled Mono.Cecil reader. Its private `BuildProfile.CreateInstance(BuildTarget, StandaloneBuildSubtarget)` factory and the target, subtarget, platform settings and development properties exist. The project-owned `WordboundBuildProfiles.Create` helper uses that factory only under an exact editor-version guard. It creates only missing profiles, rejects conflicting existing assets, validates Windows Player targets and global scene order, and checks that active-profile identity is preserved. No SDK or kit source was modified.

The project-owned `wordbound_workshop_flow` QA scenario is ready for source/native checks. It uses actual lobby/workshop handlers, edits an isolated draft, verifies the exact resolved manifest during local play, captures the party/workshop/world/equipment/returned-workshop screens, awaits shutdown, and verifies the same undo/redo history. Its assembly is restricted to editor or `GSK_QA` builds. The independent bounded review is in `BuildNotes/native-proof-review/REPORT.md`.

Both new components remain **NOT-RUN** as of this entry. Next: finish the queued lane compilation, integrate the body-budget and route controls, run R0/core, execute the profile helper through the gate, and build/smoke the actual QA player.

## Profile creation — 2026-10-20T17:34:32Z

`game_gate exec Wreckabulary.EditorTools.WordboundBuildProfiles.Create --json` returned **PASS**, exit 0, 269.9 seconds, clean shutdown. Evidence: `Logs/gate_exec_Create_20261020T173007Z.log` and `Logs/exec/20261020T173007Z.json`. This native run compiled the new source and created all three profiles through Unity. It verified Windows 64-bit Player, unchanged global scene order and active profile, empty release defines, `GSK_QA` in QA/development, and development enabled only in Win-Dev.

The saved assets were read back from disk. Their targets, global scene-list setting, defines and development values agree with the helper's checks. The profile's internal `m_HasScriptingDefines` flag is initially false even with a nonempty array; installed editor IL shows `ValidateDataConsistency` sets it on load and active-profile compilation reads the scripting-defines array. This is not evidence of a missing QA define; the actual QA player still needs build and execution proof.

Preceding assembled checks: R0 **PASS** in 85.2 seconds (`Logs/gate_check_all_20261020T172654Z.log`); core **372/372 PASS** in 72.3 seconds (`TestResults/core_20261020T172842Z.trx`). The focused native behavior batch is running separately. Windows build and smoke remain **NOT-RUN**.

## First QA build — 2026-10-20T18:30:37Z

`game_gate build --profile release-qa --timeout 1800 --json` returned **FAIL**, exit 1, 1926.7 seconds including shutdown. Build ID `20261020T175830Z`, source `4a0bbfafb4b5ff942736637ce116974beef8c2e9`, log `Logs/gate_build_release-qa_20261020T175830Z.log`, manifest `Builds/qa-20261020T175830Z/build.json`. No playable build or smoke result is claimed.

The build exhausted its 1800-second limit. A Burst queue timeout occurred earlier during script compilation (line 2643); Unity continued compiling shaders and extracting player features afterward. The expensive URP/Lit ForwardLit fragment batch completed for both graphics APIs, and Bee reached feature extraction 292/572 before shutdown. Preserve the shader/Bee caches for the retry. No graphics API, rendering feature, worker count or SDK setting was reduced.

Unity serialized URP prefilter/runtime settings, new version fields and the existing InputSystem_Actions preload reference during this build. These generated changes were inspected and retained. The newly generated Resources folder metadata is retained; only the four Performance Testing measurement files are ignored. No Unity, shader compiler or Bee process remained after gate exit.

Next: integrate and verify the queued public catalogue and physical fixture observation changes, retry the cached QA build, then run `wordbound_workshop_flow` against its exact successful build ID and inspect every capture.

## Cache recovery — 2026-10-20T18:38:00Z

The following native export attempt (`Logs/gate_exec_ExportAndAssignFromEnvironment_20261020T183516Z.log`) returned **FAIL**, exit 1, 36 seconds. Bee could not rename its digest/state cache; no C# diagnostic or exported catalogue was produced. A later process inspection found surviving build backend PID40444, created at18:28:03Z with the original player DAG `Player511a1575` and dead parent51276. The original build log records that parent through `Upm-51276` and that exact DAG invocation. This supersedes the earlier absence observation: a child did remain after the timed-out build.

Only this positively identified orphan backend was stopped by PID after checking its name, parent, creation time, command line and the parent's absence. No editor, unrelated process or cache file was removed. Native retry remains necessary.

## First built Windows player — 2026-10-23T17:21:08.800Z

Exact release-QA build `20261023T170613Z`, source `d7b5a1b`, passes the gate in 491.3 seconds. Both build.json and packed_assets.json exist; binary reports GSK_QA true and development false. The first normal presented player smoke `20261023T171517Z-wordbound_workshop_flow` fails in 104.8 seconds. Opening Wordbound reaches the scenario state, but its actual 1280×720 capture is black. Workshop construction then throws ArgumentNullException for a missing shader: runtime-only Shader.Find dependencies were stripped. Exact local playtest/return/Undo were not reached successfully. Owner workshop remains empty and unchanged; QA writes only its isolated evidence child. Full hashes/results: `windows-first-player-20261023.json`.

Root added explicit opaque/transparent Resources material templates and a Unity editor creator; workshop and guide clone those saved assets. R0 passes (`Logs/gate_check_all_20261023T171900Z.log`). Assets still need native creation and a rebuilt player smoke. Black party capture is being investigated separately. The first successful build is not a production-ready delivery.
