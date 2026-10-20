# Windows delivery preparation

STATUS: IN PROGRESS — PROFILES VERIFIED; BUILD NOT RUN

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
