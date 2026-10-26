#if UNITY_EDITOR || GSK_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GameStudioKit.Qa;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;
using Object = UnityEngine.Object;

namespace Wreckabulary.Qa
{
    public sealed class WordboundSoloQa : IQaScenario
    {
        public string Name => "wordbound_solo";
        LobbyExperienceQa.Preference[] preferences;
        InputBinding[] originalBindings;
        string originalQueue, originalMode, originalMap, originalPage, originalSolo, originalTheme;
        bool originalFamily;
        WordboundLocalMatchClient client;
        readonly Dictionary<Object, string> sourceMemory = new();
        readonly Dictionary<TextAsset, string> sourceText = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register() => QaScenarios.Register("wordbound_solo", () => new WordboundSoloQa());

        public IEnumerable<QaStep> BuildSteps(QaContext context)
        {
            yield return QaStep.Named("solo_selection").Timeout(30)
                .When(_ => LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning).Do(Setup);
            yield return QaStep.Named("time_run").Timeout(180).Do(value => Guard(value, Run(value, "TimeRun")));
            yield return QaStep.Named("freestyle").Timeout(180).Do(value => Guard(value, Run(value, "Freestyle")));
        }

        IEnumerator Setup(QaContext context)
        {
            Require(Application.platform == RuntimePlatform.WindowsPlayer && !Application.isBatchMode,
                "Solo visual QA requires a normal Windows window.");
            var menu = LobbyMenu.Instance;
            Require(menu.PartySize == 1 && !menu.Starting, "Keep the real local party intact; this route requires one human.");
            preferences = LobbyExperienceQa.Preference.Capture();
            originalBindings = Session.Bindings.ToArray();
            originalQueue = menu.Queue; originalMode = menu.Mode; originalMap = menu.Map; originalPage = menu.Current;
            originalSolo = WordboundSoloEntry.SelectedMode; originalFamily = menu.Page<PlayPage>().WordboundSolo;
            originalTheme = LobbyThemes.Current.Id;
            var assets = Resources.Load<WordboundLaunchAssets>(WordboundLaunchAssets.ResourcePath);
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            Require(assets && data && data.items && data.rules, "Installed local launch assets are required.");
            foreach (var asset in new Object[] { assets, assets.CameraProfile, assets.VisualProfile, assets.UiTheme, assets.StoryProfile })
                if (asset) sourceMemory.Add(asset, JsonUtility.ToJson(asset));
            sourceText.Add(data.items, data.items.text); sourceText.Add(data.rules, data.rules.text);
            context.Note("scope", "Normal Windows production UI Submit launch and actual local return handler, installed flagship, two modes. No physical-input, full-finish, network, bot or team acceptance claim.");
            yield return Shot(context, "solo_initial_lobby");
            VerifyPreserved(context);
        }

        IEnumerator Run(QaContext context, string mode)
        {
            var menu = LobbyMenu.Instance;
            Require(menu && menu.PartySize == 1, "The single local human must remain available.");
            Submit(menu.transform.Find("Safe area/Top bar"), "PLAY");
            yield return Settle();
            Submit(menu.Page<PlayPage>().Root, "Queue " + LobbyMenu.Practice);
            yield return Settle();
            Submit(menu.Page<PlayPage>().Root, "Family Wordbound");
            yield return Settle();
            Submit(menu.Page<PlayPage>().Root, "Solo " + mode);
            yield return Settle();
            string queue = menu.Queue, house = menu.Mode, map = menu.Map;
            yield return Shot(context, "solo_" + mode.ToLowerInvariant() + "_selected");
            Submit(menu.Page<PlayPage>().Root, "Solo GO");
            yield return context.WaitUntil(() => {
                client = Object.FindFirstObjectByType<WordboundLocalMatchClient>();
                if (client && client.Gameplay && client.Gameplay.Fault != "") throw new QaFailException("The solo gameplay controller faulted.");
                return client && client.Gameplay && client.Gameplay.IsPresentationReady && !client.Gameplay.IsSimulationPaused;
            }, 75, "the installed solo flagship");
            var gameplay = client.Gameplay;
            var launcher = Object.FindFirstObjectByType<WordboundLauncher>();
            var view = client.GetComponent<WordboundPlayerView>();
            var hud = client.GetComponent<WordboundMatchHud>();
            var guide = client.GetComponent<WordboundPuzzleGuide>();
            var story = client.GetComponent<WordboundStoryView>();
            Require(launcher && view && view.Player && view.Camera && hud && guide && story, "The actual world, view, guide and story must be composed.");
            Require(!Object.FindFirstObjectByType<WordboundPartyMenu>() && !Object.FindFirstObjectByType<WordboundWorkshop>() &&
                !Object.FindFirstObjectByType<WordboundMatchClient>(), "Solo must not create a party or Workshop.");
            var course = Read<ResolvedCourse>(hud, "course");
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            var installed = new WordboundClientContent(data.items.text, data.rules.text, mode, "Solo", course.Seed);
            Require(course.CopyDocument().Id == WordboundFlagshipContent.CourseId && course.SourceHash == installed.Course.SourceHash &&
                course.Hash == installed.Course.Hash && gameplay.CourseWorld.ManifestHash == installed.Course.Hash &&
                gameplay.Frame.Binding.CatalogueHash == installed.PublicBinding.CatalogHash &&
                gameplay.Frame.Binding.ModeHash == installed.Mode.Hash, "The actual installed flagship binding differs.");
            Require(course.CopyDocument().PhysicalSites.Count == 72 && gameplay.Frame.Progress.Players.Count == 1 &&
                gameplay.Frame.Progress.Result == null && Read<ModeDefinition>(hud, "mode").Id == mode, "Unexpected local content, seats or fabricated result.");
            Require(Read<string>(hud, "returnLabel") == "Return to lobby", "The normal play HUD has a Workshop return label.");
            var session = Read<object>(client, "session");
            var runner = session.GetType().GetProperty("Runner").GetValue(session);
            Require(runner != null && runner.GetType().GetProperty("GameMode").GetValue(runner).ToString() == "Single",
                "The local route must use the actual Single runner.");
            var controls = Session.Bindings.ToArray();
            Require(controls.Length == 1 && Session.LobbyQueue == queue && Session.LobbyMode == house && Session.MapId == map,
                "Local entry changed the house selection or seats.");
            var camera = view.Camera;
            var world = gameplay.CourseWorld;
            long tick = gameplay.Frame.Progress.Tick;
            int cameraFrames = 0, lastFrame = -1;
            void CameraEnded(Camera seen)
            {
                if (seen == camera && Time.frameCount != lastFrame) { lastFrame = Time.frameCount; cameraFrames++; }
            }
            void PipelineEnded(ScriptableRenderContext _, Camera seen) { if (GraphicsSettings.currentRenderPipeline) CameraEnded(seen); }
            void BuiltinEnded(Camera seen) { if (!GraphicsSettings.currentRenderPipeline) CameraEnded(seen); }
            RenderPipelineManager.endCameraRendering += PipelineEnded;
            Camera.onPostRender += BuiltinEnded;
            try
            {
                view.SetMenuOpen(false);
                int expectedMarkers = -1;
                bool RenderReady()
                {
                    var frame = gameplay.Frame;
                    expectedMarkers = WordboundPuzzleGuidePlan.Build(course, frame, frame.Progress.Players.Single().Id).Count;
                    return cameraFrames >= 3 && frame.Progress.Tick > tick + 5 && guide.Fault == "" &&
                        guide.isActiveAndEnabled && Read<object>(guide, "view") != null &&
                        guide.MarkerCount == expectedMarkers && (mode != "TimeRun" || expectedMarkers > 0);
                }
                try
                {
                    yield return context.WaitUntil(RenderReady, 12, "fresh assigned camera frames, simulation and the exact mode guide");
                }
                finally
                {
                    context.Note("solo_render_readiness_" + mode, new Dictionary<string, object> {
                        ["expected_markers"] = expectedMarkers, ["rendered_markers"] = guide ? guide.MarkerCount : -1,
                        ["camera_frames"] = cameraFrames, ["canonical_ticks"] = gameplay.Frame.Progress.Tick - tick,
                        ["guide_fault"] = guide ? guide.Fault : "guide unavailable",
                        ["scope"] = "Guide count matches the current canonical mode plan. Freestyle may have no nearby or selected practice station." });
                }
                Require(camera.enabled && camera.targetTexture == null && guide.Fault == "", "The assigned game camera or guide is unavailable.");
                yield return Shot(context, "solo_" + mode.ToLowerInvariant() + "_world");
                view.SetMenuOpen(true);
                yield return context.WaitFrames(3);
                yield return Shot(context, "solo_" + mode.ToLowerInvariant() + "_menu");
                context.Note("solo_" + mode, new Dictionary<string, object> {
                    ["source"] = course.SourceHash, ["manifest"] = course.Hash, ["mode"] = mode,
                    ["physical_sites"] = course.CopyDocument().PhysicalSites.Count, ["humans"] = 1,
                    ["runner"] = "Single", ["actual_camera_frames"] = cameraFrames, ["journal_entries"] = story.Journal.Count,
                    ["canonical_ticks_observed"] = gameplay.Frame.Progress.Tick - tick, ["return_label"] = Read<string>(hud, "returnLabel") });
            }
            finally
            {
                RenderPipelineManager.endCameraRendering -= PipelineEnded;
                Camera.onPostRender -= BuiltinEnded;
            }
            client.SendMessage("RequestReturn", SendMessageOptions.RequireReceiver);
            yield return context.WaitUntil(() => SceneManager.GetActiveScene().name == Session.HubScene &&
                LobbyMenu.Instance && LobbyMenu.Instance.Current == LobbyMenu.Play, 30, "the real local return handler and Hub");
            yield return Settle();
            Require(!client && !world && !camera && !Object.FindFirstObjectByType<WordboundLocalMatchClient>(), "The stopped game retained owned world objects.");
            menu = LobbyMenu.Instance;
            Require(menu.Page<PlayPage>().WordboundSolo && WordboundSoloEntry.SelectedMode == mode && menu.Mode == house && menu.Map == map &&
                menu.Queue == queue && menu.PartySize == 1 && Session.Bindings.SequenceEqual(controls), "Return lost the solo or original house/device state.");
            yield return Shot(context, "solo_" + mode.ToLowerInvariant() + "_returned");
        }

        IEnumerator Guard(QaContext context, IEnumerator body)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(body);
            Exception failure = null;
            bool settled = false;
            double workDeadline = Time.realtimeSinceStartupAsDouble + 145;
            try
            {
            while (stack.Count != 0 && failure == null)
            {
                if (Time.realtimeSinceStartupAsDouble >= workDeadline)
                { failure = new QaFailException("Solo QA exceeded its owned 145-second work budget."); break; }
                object current = null; bool next = false;
                try
                {
                    next = stack.Peek().MoveNext();
                    if (next) current = stack.Peek().Current;
                    else (stack.Pop() as IDisposable)?.Dispose();
                }
                catch (Exception error) { failure = error; }
                if (failure != null || !next) continue;
                if (current is IEnumerator nested) stack.Push(nested); else yield return current;
            }
            while (stack.Count != 0)
                try { (stack.Pop() as IDisposable)?.Dispose(); }
                catch (Exception error) { failure ??= error; }
            Task stop = null;
            try { if (client) stop = client.ReturnToMenu(); }
            catch (Exception error) { failure ??= error; }
            if (stop != null)
            {
                double deadline = Time.realtimeSinceStartupAsDouble + 25;
                while (!stop.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (!stop.IsCompleted || stop.IsCanceled || stop.IsFaulted)
                {
                    if (!stop.IsCompleted) _ = stop.ContinueWith(done => { _ = done.Exception; }, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    else if (stop.IsFaulted) _ = stop.Exception;
                    failure ??= new QaFailException("Solo cleanup did not confirm the local stop.");
                }
                yield return null;
            }
            try
            {
                VerifyPreserved(context);
            }
            catch (Exception error) { failure ??= error; }
            try { RestoreSelection(); }
            catch (Exception error) { failure ??= error; }
            context.Note("solo_cleanup", new Dictionary<string, object> { ["client_released"] = !client,
                ["bindings_restored"] = Session.Bindings.SequenceEqual(originalBindings), ["failure"] = failure?.GetType().Name ?? "none",
                ["work_budget_seconds"] = 145, ["stop_budget_seconds"] = 25, ["outer_budget_seconds"] = 180 });
            settled = true;
            if (failure != null) throw failure;
            }
            finally
            {
                var cleanupErrors = new List<string>();
                while (stack.Count != 0)
                    try { (stack.Pop() as IDisposable)?.Dispose(); }
                    catch (Exception error) { cleanupErrors.Add("iterator: " + error.GetType().Name); }
                if (!settled)
                {
                    bool stopRequested = false, restored = false;
                    try
                    {
                        if (client)
                        {
                            Task abort = client.ReturnToMenu();
                            stopRequested = true;
                            _ = abort.ContinueWith(done => { _ = done.Exception; }, CancellationToken.None,
                                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                        }
                    }
                    catch (Exception error) { cleanupErrors.Add("stop: " + error.GetType().Name); }
                    try
                    {
                        var changed = (preferences ?? Array.Empty<LobbyExperienceQa.Preference>()).Where(value => !value.Matches()).ToArray();
                        foreach (var value in changed) value.Restore();
                        if (changed.Length != 0) PlayerPrefs.Save();
                        RestoreSelection(); restored = true;
                    }
                    catch (Exception error) { cleanupErrors.Add("restore: " + error.GetType().Name); }
                    context.Note("solo_abandoned_cleanup", new Dictionary<string, object> {
                        ["stop_requested_not_confirmed"] = stopRequested, ["selection_restored"] = restored,
                        ["errors"] = cleanupErrors.ToArray() });
                }
            }
        }

        void RestoreSelection()
        {
            Session.Bindings.Clear(); Session.Bindings.AddRange(originalBindings);
            WordboundSoloEntry.Select(originalSolo);
            var lobby = LobbyMenu.Instance;
            if (lobby)
            {
                lobby.Choose(originalQueue, originalMode, originalMap);
                lobby.Page<PlayPage>().WordboundSolo = originalFamily;
                lobby.Open(originalPage);
            }
        }

        void VerifyPreserved(QaContext context)
        {
            var changed = preferences.Where(value => !value.Matches()).ToArray();
            foreach (var value in changed) value.Restore();
            if (changed.Length != 0) PlayerPrefs.Save();
            Require(changed.Length == 0, "Solo QA changed saved preferences; original values restored.");
            Require(LobbyThemes.Current.Id == originalTheme, "The selected original theme changed.");
            foreach (var pair in sourceMemory) Require(pair.Key && JsonUtility.ToJson(pair.Key) == pair.Value, "An installed launch/content/profile object changed.");
            foreach (var pair in sourceText) Require(pair.Key && pair.Key.text == pair.Value, "Installed item or rule bytes changed.");
            context.Note("solo_source_preferences_preserved", true);
        }
        static T Read<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        static void Require(bool condition, string message) { if (!condition) throw new QaFailException(message); }
        static void Submit(Transform root, string name)
        {
            var button = root.GetComponentsInChildren<Button>().Single(item => item.name == name);
            Require(button.IsInteractable() && EventSystem.current, "The real UI action is unavailable: " + name);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }
        static IEnumerator Settle()
        {
            yield return new WaitForSecondsRealtime(LobbyPop.Seconds + .03f);
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
        }
        static IEnumerator Shot(QaContext context, string name)
        {
            yield return context.CaptureScreen(name);
            string path = Path.Combine(context.OutDir, name + ".png");
            Require(File.Exists(path), "The requested actual-window capture is missing.");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                Require(ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false) && texture.width == Screen.width && texture.height == Screen.height,
                    "The capture dimensions differ from the actual window.");
                var pixels = texture.GetPixels32(); var first = pixels[0];
                Require(pixels.Any(pixel => pixel.r != first.r || pixel.g != first.g || pixel.b != first.b), "The actual frame is uniform.");
                context.Note(name + "_capture", new Dictionary<string, object> { ["width"] = texture.width,
                    ["height"] = texture.height, ["rendered_frame"] = Time.renderedFrameCount, ["focused"] = Application.isFocused });
            }
            finally { Object.Destroy(texture); }
        }
    }
}
#endif
