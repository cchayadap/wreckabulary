# Windows QA source review

STATUS: SOURCE REVIEW COMPLETE; bounded storage repair independently source-reviewed; native execution pending.

Reviewed root source at `7c45655` on 2026-10-23. The repair is based on that exact checkpoint in the preserved `wordbound-map-workshop` checkout, branch `sethypagna/windows-qa-delivery`. No Unity, Blender, compiler, build, test or player was run for this review. This is not Windows acceptance or an art-quality approval.

## Concrete blocker and bounded repair

Both scenarios opened the launcher workshop before selecting their QA folder. `WordboundLauncher.OpenWorkshop` calls `WordboundWorkshop.OpenAuthored` for the assigned course. Existing owner autosave files therefore set `RecoveryChoicePending`. The subsequent `OpenFlagship(content, isolatedFolder)` calls `RequireRecoveryChoice` and throws before it can use that folder. A clean QA output directory did not isolate this first read of owner storage.

The repair adds `ConfigureQaWorkshopStorage` to `WordboundLauncher` only under `UNITY_EDITOR || GSK_QA`. It requires a fully qualified, empty or nonexistent folder, rejects filesystem roots and the normal owner workshop folder and its ancestors/descendants, and refuses configuration after a workshop/match has opened or destruction has begun. It does not create, archive, delete or recover files. Normal `OpenWorkshop` forwards the selected folder through the existing `OpenAuthored`/`OpenFlagship` APIs; ordinary release builds retain the existing null/default folder behavior and do not expose the QA setup API.

`WordboundWorkshopQa.EditWorkshop` and `WordboundPerformanceQa.Prepare` now choose a fresh GUID-named child of `context.OutDir` before calling the same real launcher `OpenWorkshop` action. They record that folder, fail on unexpected pending recovery, and let the later flagship selection inherit the already selected storage. Launcher event wiring, assigned source validation, recovery guards, source/profile selection, preview, playtest and return remain on their production paths. No automatic recovery choice or owner-file overwrite is introduced. The output folder is retained as QA evidence.

Changed source is limited to:

- `Assets/_Project/Scripts/Online/Client/WordboundLauncher.cs`
- `Assets/_Project/Scripts/WordboundQa/WordboundWorkshopQa.cs`
- `Assets/_Project/Scripts/WordboundQa/WordboundPerformanceQa.cs`

Independent source reviewer `/root/recover_input` confirmed the real launcher route and owner recovery preservation. Their filesystem-root finding was repaired: a drive/UNC root is rejected before trimmed storage can become drive-relative. Final bounded source review passed; `git diff --check` passed. Neither is a compiler or native test result.

## What the Windows scenarios actually cover

| Exact scenario | Source-level coverage | Limits |
| --- | --- | --- |
| `wordbound_workshop_flow` | Real lobby and party entry; launcher workshop creation; exact seeded flagship preview; actual local match and puzzle guide; equipment-menu screenshot; return; same draft/history; client/camera destruction; exact Undo/Redo. Captures `wordbound_party`, `wordbound_workshop`, `wordbound_local_world`, `wordbound_equipment_menu`, `wordbound_returned_workshop`. | Calls methods and edits `WorkshopHistory` directly. Does not click the Workshop button, select/drag/rotate modules, draw randomness zones, navigate controller menus, import/export or reload an autosave. Presented window required; the scenario itself does not reject Editor execution, so Windows evidence must come from the player invocation. |
| `wordbound_synthetic8_low` | Presented Windows `release-qa` only; actual local flagship; eight animated synthetic avatars with eight held visuals plus the existing one local actor; live artwork identity; 1920×1080, Low, 0.75 render scale, uncapped/vsync off; at least 3 seconds and 120 warmup frames, then 60-second sample; 16.6 ms p95 gate; focus/display/health checks; settings restoration and local shutdown. Captures `synthetic_start`, `synthetic_end`, `synthetic_reference_pose`. | Zero remote peers and no active synthetic combat/collision. Fixed QA camera with gameplay view/HUD disabled during measurement. Does not establish network performance, ordinary gameplay-camera usability, eight-player physics or all twelve equipment assets. |

The performance scenario captures monotonic/UTC sample boundaries before reporting and screenshots. Per-frame health checks do not perform scene-wide searches; mesh/material inspection and scene inventory are outside the measured loop. The workload has guarded partial-setup restoration, idempotent cleanup and quit/destroy restoration. No additional concrete cleanup blocker was found in this bounded source review.

## Original artwork coverage

`Resources/Wordbound/LaunchAssets.asset` references `Data/Wordbound/Authoring/Presentation.asset`. That profile has no model overrides and uses `WordboundVisualProfile`'s `OriginalWreckabulary` default. `WordboundLauncherWorkshopSource.InputsForWorkshop` uses the launcher profiles for the selected global flagship, preserving the separate assigned-course profile rules.

`WordboundPerformanceWorkload.Prepare` uses the same initialized `WordboundGameplayPresentation.ArtworkResolver` instance as the actual local world. `ObserveArtwork` compares spawned mesh identities with the selected original ModelLibrary, optional collection or explicit override, and records actual material/shader/texture facts. It no longer forces the rejected DetailV2 collection. With the current shipped defaults this exercises original `BAT`, `BROOM`, `HAMMER`, `SPEAR`, `SHIELD`, `PLATE`, `FAN` and `CLOCK`, plus original avatars. A deliberately changed profile is recorded as such; the scenario is not a hard-coded original-only assertion. SOFA/BED/TABLE/STOOL held appearance and close-up quality need separate views.

The workshop flow uses the actual local presentation but does not assert or record individual model identities. A screenshot must be inspected; simply passing its manifest/history assertions does not establish visual quality. Original models remain the default, DetailV2 remains owner-rejected optional work, and the original-based pilots remain unapproved experiments.

## Existing focused regressions and remaining execution

Relevant exact fixtures/methods already in source:

- `Wreckabulary.OfflineGameplayTests.WordboundNativeAuthoredLauncherTests.SavedInspectorCourseRulesProfilesAndEditedGeometryReachLocalPlayAndReturn`: saved Inspector course, custom data/profiles, edited geometry and local play/return. It does not drive the Inspector or workshop mouse UI.
- `Wreckabulary.Online.Tests.WordboundWorkshopSourceBindingTests.ActualLauncherUsesCapturedAssignedBytesAndProfilesOnlyForTheAssignedSession` and `AssignedOpeningAlsoOffersTheGlobalFlagshipAndCanReturnWithoutResetting`: assigned/global profile binding and retained history.
- `Wreckabulary.EditorTests.WordboundWorkshopRecoveryTests.ExistingDraftBlocksAutosavePreviewTemplateAndShutdownUntilChoice`: the intended recovery guard; it must remain intact.
- `Wreckabulary.Online.Tests.WordboundWorkshopInputSafetyTests.ActualUndoPathIgnoresUnfocusedAndHeldResumeInputUntilNeutral`, `ActualUndoPathIsSuppressedWhileEditingTextAndAfterPadReplacement`, and `UnplugCancelsUncommittedControllerDragWithoutCancellingAnIdleMousePlacement`: native sampled input safety, not a full Windows interaction walkthrough.
- `Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests.TwoAuthenticatedPeersDownloadAndPlayExactAuthoredWorkshopMap`: opt-in exact private authored-content transfer; not public matchmaking or Windows workshop navigation.

Root must compile the QA and ordinary release configurations, then run both named Windows scenarios with an existing owner draft present and compare those recovery files before/after. Confirm the recorded QA storage is the fresh output child and the performance artwork records `original_wreckabulary` / `original_model_library` for the unchanged shipped profile. Rerunning with the same output parent must choose another child, not silently recover an older QA edit. These executions are NOT RUN here.

The no-code acceptance already described in [UNITY-EDITING-GUIDE.md](../../docs/wordbound/UNITY-EDITING-GUIDE.md) still needs actual mouse/controller operation: enter Workshop, choose recovery explicitly when offered, select/drag/rotate with Undo, set/draw placement bounds and seed, preview/play/return, save/export/reload, and inspect follow-camera movement/focus recovery. Neither automated Windows scenario proves those interactions. No speculative redesign or asset deletion is proposed.
