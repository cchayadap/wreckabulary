#if UNITY_EDITOR || GSK_QA
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GameStudioKit.Capture;
using GameStudioKit.Qa;
using UnityEngine;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;

namespace Wreckabulary.Qa
{
    public sealed class WordboundWorkshopQa : IQaScenario
    {
        public string Name => "wordbound_workshop_flow";
        WordboundLauncher launcher;
        WordboundWorkshop workshop;
        WordboundLocalMatchClient client;
        WorkshopHistory history;
        string initial, edited, manifest;
        Camera ownedCamera;
        WordboundPhaseObservation phases;
        WordboundFrameObservation frames;
        bool savedDraftVerified, editCompleted;
        string playtestStage = "not_started";
        int playtestStartFrame = -1, playtestReadyFrame = -1, playtestWaitedFrames;
        double playtestStartedAt = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register() => QaScenarios.Register("wordbound_workshop_flow", () => new WordboundWorkshopQa());

        public IEnumerable<QaStep> BuildSteps(QaContext context)
        {
            yield return QaStep.Named("open_wordbound").Timeout(60)
                .When(_ => Object.FindFirstObjectByType<LobbyMenu>() != null)
                .Do(OpenWordbound);
            yield return QaStep.Named("edit_workshop").Timeout(60).Do(EditWorkshop);
            yield return QaStep.Named("exact_playtest").Timeout(90).Do(Playtest)
                .Measure(_ => new Dictionary<string, object> {
                    ["exact_manifest"] = client && client.Gameplay.CourseWorld.ManifestHash == manifest,
                    ["draft_unchanged"] = history.Current.RecoveryJson == edited,
                    ["markers"] = client ? client.GetComponent<WordboundPuzzleGuide>().MarkerCount : 0
                })
                .Expect(QaAssert.IsTrue("exact_manifest")).Expect(QaAssert.IsTrue("draft_unchanged"))
                .Expect(QaAssert.AtLeast("markers", 1));
            yield return QaStep.Named("return_to_workshop").Timeout(45).Do(Return)
                .Measure(_ => new Dictionary<string, object> {
                    ["same_history"] = workshop && ReferenceEquals(workshop.History, history),
                    ["draft_unchanged"] = history.Current.RecoveryJson == edited,
                    ["client_destroyed"] = client == null,
                    ["camera_destroyed"] = ownedCamera == null,
                    ["editor_visible"] = workshop && workshop.Visible
                })
                .Expect(QaAssert.IsTrue("same_history")).Expect(QaAssert.IsTrue("draft_unchanged"))
                .Expect(QaAssert.IsTrue("client_destroyed")).Expect(QaAssert.IsTrue("camera_destroyed"))
                .Expect(QaAssert.IsTrue("editor_visible"));
            yield return QaStep.Named("undo_redo_preserved").Timeout(15).Do(UndoRedo);
        }

        IEnumerator OpenWordbound(QaContext context)
        {
            if (Application.isBatchMode) throw new QaFailException("This visual scenario requires a presented player window.");
            float deadline = Time.realtimeSinceStartup + 45;
            Object.FindFirstObjectByType<LobbyMenu>().OpenWordbound();
            yield return context.WaitUntil(() => {
                launcher = Object.FindFirstObjectByType<WordboundLauncher>();
                return launcher && launcher.GetComponent<WordboundPartyMenu>()?.Coordinator != null;
            }, 45, "the Wordbound party menu");
            var observation = launcher.gameObject.AddComponent<WordboundPartyFrameObservation>();
            try
            {
                observation.Begin(context, launcher.GetComponent<WordboundPartyMenu>(), launcher.LobbyCamera);
                while (!observation.Ready)
                {
                    if (Time.realtimeSinceStartup >= deadline)
                    {
                        observation.Record();
                        throw new QaFailException("The party did not finish splash, two menu repaints and assigned-camera rendering within 45 seconds; see party_frame probes.");
                    }
                    yield return null;
                }
                observation.Record();
                yield return context.CaptureScreen("wordbound_party");
                observation.Record();
                RequireScreenPixels(context, "wordbound_party", "party");
                if (!observation.Ready) throw new QaFailException("The party stopped rendering while its first PNG was captured.");
            }
            finally { if (observation) observation.Dispose(); }
        }

        static void RequireScreenPixels(QaContext context, string name, string label)
        {
            string path = Path.Combine(context.OutDir, name + ".png");
            if (!File.Exists(path)) throw new QaFailException("The " + label + " PNG was not written.");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false))
                    throw new QaFailException("The " + label + " PNG could not be decoded.");
                var pixels = texture.GetPixels32();
                int nonblack = 0, nonzeroAlpha = 0;
                int minR = 255, minG = 255, minB = 255, minA = 255, maxR = 0, maxG = 0, maxB = 0, maxA = 0;
                bool variedRgb = false;
                var first = pixels[0];
                foreach (var pixel in pixels)
                {
                    if (pixel.r != 0 || pixel.g != 0 || pixel.b != 0) nonblack++;
                    if (pixel.a != 0) nonzeroAlpha++;
                    if (pixel.r != first.r || pixel.g != first.g || pixel.b != first.b) variedRgb = true;
                    minR = Mathf.Min(minR, pixel.r); minG = Mathf.Min(minG, pixel.g);
                    minB = Mathf.Min(minB, pixel.b); minA = Mathf.Min(minA, pixel.a);
                    maxR = Mathf.Max(maxR, pixel.r); maxG = Mathf.Max(maxG, pixel.g);
                    maxB = Mathf.Max(maxB, pixel.b); maxA = Mathf.Max(maxA, pixel.a);
                }
                context.Note(label + "_capture_width", texture.width); context.Note(label + "_capture_height", texture.height);
                context.Note(label + "_capture_nonblack_pixels", nonblack);
                context.Note(label + "_capture_nonzero_alpha_pixels", nonzeroAlpha);
                context.Note(label + "_capture_varied_rgb", variedRgb);
                context.Note(label + "_capture_min_rgba", $"{minR},{minG},{minB},{minA}");
                context.Note(label + "_capture_max_rgba", $"{maxR},{maxG},{maxB},{maxA}");
                if (texture.width != Screen.width || texture.height != Screen.height)
                    throw new QaFailException("The " + label + " PNG dimensions differ from the presented screen.");
                if (nonblack == 0 || nonzeroAlpha == 0 || !variedRgb)
                    throw new QaFailException("The " + label + " PNG is blank or a uniform fill; the first capture is preserved.");
            }
            finally { Object.Destroy(texture); }
        }

        IEnumerator EditWorkshop(QaContext context)
        {
            savedDraftVerified = editCompleted = false;
            context.Note("workshop_autosave_verified", false);
            context.Note("workshop_edit_completed", false);
            string folder = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "wbqa", System.Guid.NewGuid().ToString("N")));
            string scopedFolder = Path.Combine(folder, "templates", "Flagship");
            string primary = Path.Combine(scopedFolder, WordboundFlagshipContent.CourseId + ".autosave.json");
            int temporaryPathCharacters = primary.Length + 37;
            context.Note("workshop_atomic_path_characters", temporaryPathCharacters);
            if (temporaryPathCharacters >= 260)
                throw new QaFailException("The QA temporary storage path is too long for the Windows atomic save; use a shorter temporary directory.");
            launcher.ConfigureQaWorkshopStorage(folder);
            context.Note("workshop_storage_folder", folder);
            launcher.SendMessage("OpenWorkshop", SendMessageOptions.RequireReceiver);
            workshop = launcher.GetComponent<WordboundWorkshop>();
            if (!workshop) throw new QaFailException("The launcher did not open its workshop.");
            if (workshop.RecoveryChoicePending) throw new QaFailException("The isolated QA workshop unexpectedly requires recovery.");
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            workshop.OpenFlagship(WordboundFlagshipContent.Create(ItemCatalogue.FromJson(data.items.text)));
            history = workshop.History;
            initial = history.Current.RecoveryJson;
            history.Execute("QA draft edit", draft => {
                draft.Course.Title = "Wordbound playtest draft";
                draft.Seed = "0314159265358979";
                draft.SeedLocked = true;
            });
            edited = history.Current.RecoveryJson;
            workshop.ResumeEditing();
            var preview = workshop.Preview();
            if (!preview.Success) throw new QaFailException("The exact edited flagship did not validate.");
            manifest = preview.Manifest.Hash;
            context.Note("manifest_hash", manifest);
            context.Note("seed", history.Current.Seed);
            yield return context.WaitUntil(() => SavedPrimaryMatches(primary, edited), 15, "the workshop's exact edited primary autosave");
            var recovered = new WorkshopStorage(scopedFolder).Recover(WordboundFlagshipContent.CourseId, out bool usedBackup);
            bool exact = File.ReadAllText(primary) == edited && recovered.RecoveryJson == edited;
            bool unchanged = ReferenceEquals(workshop.History, history) && history.Current.RecoveryJson == edited;
            int temporaryFiles = Directory.GetFiles(scopedFolder, WordboundFlagshipContent.CourseId + ".autosave.json.*.tmp").Length;
            context.Note("workshop_autosave_primary", primary);
            context.Note("workshop_autosave_bytes", new FileInfo(primary).Length);
            context.Note("workshop_autosave_used_backup", usedBackup);
            context.Note("workshop_autosave_exact", exact);
            context.Note("workshop_autosave_draft_unchanged", unchanged);
            context.Note("workshop_autosave_temporary_files", temporaryFiles);
            context.Note("workshop_recovered_seed", recovered.Seed);
            if (usedBackup || !exact || !unchanged || temporaryFiles != 0)
                throw new QaFailException("The workshop did not atomically save and recover the exact edited draft from its primary file.");
            string evidence = context.PathInRun("workshop-autosave.json");
            File.Copy(primary, evidence, false);
            context.Note("workshop_autosave_evidence", context.ToWorkspacePath(evidence));
            savedDraftVerified = true;
            context.Note("workshop_autosave_verified", true);
            yield return context.WaitFrames(3);
            string capture = context.PathInRun("wordbound_workshop.png"), captureError = null;
            yield return StoryCamera.CaptureScreen(capture, error => captureError = error);
            if (captureError != null) throw new QaFailException("Workshop screen capture failed: " + captureError);
            context.AddCapture("wordbound_workshop", capture, "png", "screen");
            RequireScreenPixels(context, "wordbound_workshop", "workshop");
            editCompleted = true;
            context.Note("workshop_edit_completed", true);
        }

        static bool SavedPrimaryMatches(string path, string expected)
        {
            if (!File.Exists(path)) return false;
            try { return File.ReadAllText(path) == expected; }
            catch (FileNotFoundException) { return false; }
            catch (IOException error) when ((error.HResult & 0xffff) == 32 || (error.HResult & 0xffff) == 33)
            { return false; }
        }

        IEnumerator Playtest(QaContext context)
        {
            if (!savedDraftVerified || !editCompleted || !ReferenceEquals(workshop.History, history) || history.Current.RecoveryJson != edited)
                throw new QaFailException("Playtest requires verified primary autosave recovery and a completed capture of the exact edited draft.");
            playtestStartedAt = Time.realtimeSinceStartupAsDouble;
            playtestStartFrame = Time.frameCount; playtestReadyFrame = -1; playtestWaitedFrames = 0;
            playtestStage = "request_playtest";
            RecordPlaytest(context, "start");
            phases = WordboundPhaseObservation.StartIfEnabled(launcher, context);
            try
            {
                workshop.RequestPlaytest();
                client = launcher.GetComponentInChildren<WordboundLocalMatchClient>();
                playtestStage = "wait_readiness";
                RecordPlaytest(context, "requested");
                int lastReadinessFrame = Time.frameCount;
                yield return context.WaitUntil(() => {
                    client = launcher.GetComponentInChildren<WordboundLocalMatchClient>();
                    if (Time.frameCount - lastReadinessFrame >= 15)
                    { RecordPlaytest(context, "readiness_progress"); lastReadinessFrame = Time.frameCount; }
                    if (!client || !client.Gameplay) return false;
                    if (client.Gameplay.Fault != "")
                    { RecordPlaytest(context, "gameplay_fault"); throw new QaFailException("The local playtest faulted."); }
                    var guide = client.GetComponent<WordboundPuzzleGuide>();
                    if (guide && guide.Fault != "")
                    { RecordPlaytest(context, "guide_fault"); throw new QaFailException("The puzzle guide faulted."); }
                    return client.Gameplay.CourseWorld && client.Gameplay.Frame != null &&
                        !client.Gameplay.IsSimulationPaused && guide && guide.MarkerCount > 0;
                }, 60, "the local flagship and puzzle markers");
                playtestReadyFrame = Time.frameCount;
                if (phases) phases.Ready(client.Gameplay.Frame);
                playtestStage = "capture_ready";
                context.Note("playtest_ready_elapsed_seconds", Time.realtimeSinceStartupAsDouble - playtestStartedAt);
                context.Note("playtest_ready_canonical_tick", client.Gameplay.Frame.Progress.Tick);
                RecordPlaytest(context, "ready");
                ownedCamera = client.GetComponent<WordboundPlayerView>().Camera;
                yield return CaptureVerified(context, "wordbound_local_ready", "local_ready");
                playtestStage = "wait_90_frames";
                RecordPlaytest(context, "begin_frame_wait");
                frames = WordboundFrameObservation.StartIfEnabled(launcher, client.Gameplay, ownedCamera, context);
                for (int frame = 1; frame <= 90; frame++)
                {
                    yield return null;
                    playtestWaitedFrames = frame;
                    if (frame % 15 == 0) RecordPlaytest(context, "frame_wait_progress");
                }
                FinishFrames("frame-wait-completed");
                playtestStage = "capture_world";
                RecordPlaytest(context, "world_capture_begin");
                yield return context.CaptureScreen("wordbound_local_world");
                playtestStage = "capture_equipment";
                RecordPlaytest(context, "world_capture_returned");
                client.GetComponent<WordboundPlayerView>().SetMenuOpen(true);
                yield return context.WaitFrames(3);
                yield return context.CaptureScreen("wordbound_equipment_menu");
                playtestStage = "completed";
                RecordPlaytest(context, "playtest_returned");
            }
            finally
            {
                FinishFrames("iterator-exit");
                FinishPhases(playtestStage == "completed" ? "completed" : "iterator-exit");
            }
        }

        IEnumerator Return(QaContext context)
        {
            FinishFrames("return-boundary");
            FinishPhases("return-boundary");
            RecordPlaytest(context, "before_return");
            if (!client) throw new QaFailException("Return requires the local playtest client; inspect the preceding playtest result.");
            yield return Complete(context, client.ReturnToWorkshop());
            yield return context.WaitFrames(3);
            yield return context.CaptureScreen("wordbound_returned_workshop");
        }

        void FinishFrames(string outcome)
        {
            var observation = frames; frames = null;
            if (observation) observation.Finish(outcome);
        }

        void FinishPhases(string outcome)
        {
            var observation = phases; phases = null;
            if (observation) observation.Finish(outcome);
        }

        void RecordPlaytest(QaContext context, string observation)
        {
            var gameplay = client ? client.Gameplay : null;
            var guide = client ? client.GetComponent<WordboundPuzzleGuide>() : null;
            var presentation = client ? client.GetComponent<WordboundGameplayPresentation>() : null;
            var playerView = client ? client.GetComponent<WordboundPlayerView>() : null;
            var camera = playerView ? playerView.Camera : null;
            bool hasFrame = gameplay && gameplay.Frame != null;
            bool hasWorld = gameplay && gameplay.CourseWorld;
            bool paused = !gameplay || gameplay.IsSimulationPaused;
            bool ready = hasWorld && hasFrame && !paused && guide && guide.MarkerCount > 0;
            long tick = hasFrame ? gameplay.Frame.Progress.Tick : -1;
            double elapsed = playtestStartedAt < 0 ? 0 : Time.realtimeSinceStartupAsDouble - playtestStartedAt;
            context.Note("playtest_observation", observation);
            context.Note("playtest_stage", playtestStage);
            context.Note("playtest_start_frame", playtestStartFrame);
            context.Note("playtest_ready_frame", playtestReadyFrame);
            context.Note("playtest_current_frame", Time.frameCount);
            context.Note("playtest_post_ready_waited_frames", playtestWaitedFrames);
            context.Note("playtest_elapsed_seconds", elapsed);
            context.Note("playtest_canonical_tick", tick);
            context.Note("playtest_has_client", client != null);
            context.Note("playtest_has_controller", gameplay != null);
            context.Note("playtest_controller_active", gameplay && gameplay.isActiveAndEnabled);
            context.Note("playtest_has_frame", hasFrame);
            context.Note("playtest_has_world", hasWorld);
            context.Note("playtest_paused", paused);
            context.Note("playtest_ready_condition", ready);
            context.Note("playtest_gameplay_fault", gameplay && gameplay.Fault != "");
            context.Note("playtest_guide_fault", guide && guide.Fault != "");
            context.Note("playtest_presentation_fault", presentation && presentation.Fault != "");
            context.Note("playtest_marker_count", guide ? guide.MarkerCount : 0);
            context.Note("playtest_visual_count", presentation ? presentation.VisualCount : 0);
            context.Note("playtest_camera_exists", camera != null);
            context.Note("playtest_camera_active", camera && camera.isActiveAndEnabled);
            context.Note("playtest_menu_open", playerView && playerView.MenuOpen);
            context.Note("playtest_time_scale", Time.timeScale);
            context.Note("playtest_focused", Application.isFocused);
            Debug.Log("WORDBOUND_QA_PLAYTEST: observation=" + observation + "; stage=" + playtestStage +
                "; elapsed=" + elapsed.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                "; startFrame=" + playtestStartFrame + "; readyFrame=" + playtestReadyFrame +
                "; frame=" + Time.frameCount + "; waitedFrames=" + playtestWaitedFrames + "; tick=" + tick +
                "; ready=" + ready + "; paused=" + paused);
        }

        static IEnumerator CaptureVerified(QaContext context, string name, string label)
        {
            string path = context.PathInRun(name + ".png"), error = null;
            yield return StoryCamera.CaptureScreen(path, value => error = value);
            if (error != null) throw new QaFailException("The " + label + " screen capture failed: " + error);
            context.AddCapture(name, path, "png", "screen");
            RequireScreenPixels(context, name, label);
        }

        IEnumerator UndoRedo(QaContext context)
        {
            if (!history.Undo() || history.Current.RecoveryJson != initial)
                throw new QaFailException("The original draft was not restored by Undo after playtest.");
            if (!history.Redo() || history.Current.RecoveryJson != edited)
                throw new QaFailException("Redo did not recover the edited draft after playtest.");
            context.Note("undo_redo_exact", true);
            yield return context.WaitFrames(2);
        }

        static IEnumerator Complete(QaContext context, Task task)
        {
            yield return context.WaitUntil(() => task.IsCompleted, 30, "confirmed local shutdown");
            if (task.IsCanceled || task.IsFaulted) throw new QaFailException("Local shutdown did not complete successfully.");
        }
    }
}
#endif
