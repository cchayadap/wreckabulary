# Windows delivery preparation

STATUS: IN PROGRESS — QA BUILD PASSES; PLAYER WORKSHOP SMOKE FAILS; REPAIRS UNDER VERIFICATION

Current checkpoint (2026-10-23T20:53Z): QA build `20261023T203920Z` passes in102.6 seconds, source d8eb1804. Actual visible204132 reaches local readiness in5.855 seconds and passes exact workshop saving, return/history and Undo. It fails the unchanged90-update-wait playtest requirement at81 completed waits. Startup and ready timing windows close with invalid=None/inFlight=0; ready records tick50→4970 and closes at return-boundary before teardown. FullValidation totals19,601.250 ms/5,061 calls across84.3499108 seconds; these inclusive elapsed phases are not additive exclusive CPU or FPS. Root and independent reviewer inspected the ready image: original artwork and readable compact HUD remain. `windows-phase-capture-20261023.json` binds19 actual artifacts including shipping assemblies; `QA-PHASE-CAPTURE-REVIEW.md` records independent review. Console reports0 errors/exceptions/warnings, perf is empty, and no native crash occurred in this run. The earlier native crash and official hidden rendering remain open. The bounded smoke-launch change and Unity6000.6.4 installation owner actions remain pending; kit/editor versions are unchanged. No tested delivery or production acceptance is claimed. Historical entries retain earlier attempts.

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
