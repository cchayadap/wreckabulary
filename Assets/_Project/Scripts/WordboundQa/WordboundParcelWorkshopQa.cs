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
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Wreckabulary.Art;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;
using Wreckabulary.Workshop;
using Object = UnityEngine.Object;

namespace Wreckabulary.Qa
{
    public sealed class WordboundParcelWorkshopQa : IQaScenario
    {
        readonly bool inspectorViews;
        public WordboundParcelWorkshopQa(bool captureInspectorViews = false) { inspectorViews = captureInspectorViews; }
        public string Name => inspectorViews ? "wordbound_parcel_workshop_inspector" : "wordbound_parcel_workshop";
        WordboundLauncher launcher;
        WordboundWorkshop workshop;
        WordboundLocalMatchClient client;
        WordboundLaunchAssets assets;
        WordboundCourseAsset parcelSource;
        WorkshopHistory assigned, parcel;
        WorkshopSnapshot assignedEdited, parcelEdited;
        string assignedInitial, parcelInitial, folder, parcelFolder, assignedPrimary, parcelPrimary, manifest;
        int assignedRevision, parcelRevision;
        bool assignedUndo, assignedRedo, parcelUndo, parcelRedo, stateCaptured, enteredWordbound;
        Camera gameplayCamera, previewCamera;
        WordboundCourseWorld gameplayWorld;
        WordboundInputBinding ownedInput;
        LobbyExperienceQa.Preference[] preferences;
        InputBinding[] bindings;
        string queue, house, map, page, solo, soloMap, theme;
        bool soloFamily;
        readonly Dictionary<Object, string> memory = new();
        readonly Dictionary<TextAsset, string> text = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register()
        {
            QaScenarios.Register("wordbound_parcel_workshop", () => new WordboundParcelWorkshopQa());
            QaScenarios.Register("wordbound_parcel_workshop_inspector", () => new WordboundParcelWorkshopQa(true));
        }

        public IEnumerable<QaStep> BuildSteps(QaContext context)
        {
            yield return QaStep.Named("parcel_workshop_sources").Timeout(180)
                .When(_ => LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning)
                .Do(value => Guard(value, Open(value)));
            yield return QaStep.Named("parcel_workshop_save_and_focus").Timeout(180).Do(value => Guard(value, Edit(value)));
            if (inspectorViews)
                yield return QaStep.Named("parcel_workshop_inspector_views").Timeout(180).Do(value => Guard(value, InspectorViews(value)));
            yield return QaStep.Named("parcel_workshop_exact_playtest").Timeout(180).Do(value => Guard(value, Play(value)));
            yield return QaStep.Named("parcel_workshop_return_and_history").Timeout(180).Do(value => Guard(value, Return(value)));
        }

        IEnumerator Open(QaContext context)
        {
            Require(Application.platform == RuntimePlatform.WindowsPlayer && !Application.isBatchMode,
                "Parcel Workshop QA requires a normal Windows player window.");
            Require(!Object.FindFirstObjectByType<WordboundLauncher>() && !Object.FindFirstObjectByType<WordboundLocalMatchClient>(),
                "Start this scenario from a fresh main lobby without a Wordbound session.");
            var lobby = LobbyMenu.Instance;
            Require(lobby.PartySize == 1 && !lobby.Starting, "Keep the original single local human intact.");
            preferences = LobbyExperienceQa.Preference.Capture(); bindings = Session.Bindings.ToArray();
            queue = lobby.Queue; house = lobby.Mode; map = lobby.Map; page = lobby.Current;
            solo = WordboundSoloEntry.SelectedMode; soloMap = WordboundSoloEntry.SelectedMap;
            soloFamily = lobby.Page<PlayPage>().WordboundSolo; theme = LobbyThemes.Current.Id; stateCaptured = true;
            assets = Resources.Load<WordboundLaunchAssets>(WordboundLaunchAssets.ResourcePath);
            Require(assets && assets.WorkshopCourse && assets.ParcelServiceCourse, "Install both saved Workshop source assignments.");
            parcelSource = assets.ParcelServiceCourse;
            Require(parcelSource.ReadDocument().Id == WordboundSoloEntry.ParcelServiceCourseId, "Parcel Service has the wrong saved course ID.");
            Remember(assets); Remember(assets.CameraProfile); Remember(assets.VisualProfile); Remember(assets.UiTheme); Remember(assets.StoryProfile);
            RememberCourse(assets.WorkshopCourse); RememberCourse(parcelSource);
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            Require(data && data.items && data.rules, "Installed game inputs are missing.");
            RememberText(data.items); RememberText(data.rules);
            context.Note("scope", "Actual Windows Workshop source, transactional draft edit, autosave readback, focused original prop view, exact Single playtest and return. Public game actions and existing launcher QA hook/handlers; no physical pointer/device, online matchmaking, route completion or visual acceptance claim.");
            enteredWordbound = true; lobby.OpenWordbound();
            yield return context.WaitUntil(() => {
                launcher = Object.FindFirstObjectByType<WordboundLauncher>();
                return launcher && launcher.GetComponent<WordboundPartyMenu>()?.Coordinator != null;
            }, 45, "the actual installed Wordbound launcher");
            folder = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "wbqa", Guid.NewGuid().ToString("N")));
            parcelFolder = Path.Combine(folder, "local-courses", "ParcelServiceV1");
            parcelPrimary = Path.Combine(parcelFolder, WordboundSoloEntry.ParcelServiceCourseId + ".autosave.json");
            Require(parcelPrimary.Length + 37 < 260, "The isolated atomic-save path is too long for Windows.");
            launcher.ConfigureQaWorkshopStorage(folder);
            context.Note("isolated_workshop_storage", folder);
            launcher.SendMessage("OpenWorkshop", SendMessageOptions.RequireReceiver);
            workshop = launcher.GetComponent<WordboundWorkshop>();
            Require(workshop && workshop.UsesAssignedCourse && workshop.HasAssignedCourse && !workshop.RecoveryChoicePending,
                "The original Assigned source did not open in isolated storage.");
            assigned = workshop.History; assignedInitial = assigned.Current.RecoveryJson;
            Require(assigned.Execute("QA assigned-source history marker", draft => draft.Course.Title += " · QA retained"),
                "The isolated Assigned draft edit did not enter history.");
            workshop.ResumeEditing();
            assignedEdited = assigned.Current; assignedRevision = assigned.Revision;
            assignedUndo = assigned.CanUndo; assignedRedo = assigned.CanRedo;
            assignedPrimary = Path.Combine(folder, assignedEdited.Course().Id + ".autosave.json");
            yield return Saved(context, assignedPrimary, assignedEdited, folder, "parcel_workshop_assigned_autosave");
            yield return PreviewShot(context, "parcel_workshop_assigned");
            workshop.OpenParcelService();
            Require(workshop.UsesParcelService && !workshop.UsesAssignedCourse && !ReferenceEquals(workshop.History, assigned),
                "Parcel Service replaced or reused the Assigned history.");
            parcel = workshop.History; parcelInitial = parcel.Current.RecoveryJson;
            Require(parcel.Current.Course().Id == WordboundSoloEntry.ParcelServiceCourseId &&
                parcel.Current.CourseJsonText == parcelSource.DocumentJson && parcel.Current.Seed == parcelSource.Seed,
                "The separate Parcel source is not the exact installed saved course and seed.");
        }

        IEnumerator Edit(QaContext context)
        {
            Require(workshop && workshop.UsesParcelService && ReferenceEquals(workshop.History, parcel), "Open the separate Parcel draft first.");
            Require(parcel.Execute("QA Parcel Service saved draft", draft => {
                draft.Course.Title = "Parcel Service · QA edit"; draft.Seed = "0314159265358979"; draft.SeedLocked = true;
                var room = draft.Course.Modules.Single(module => module.Id == "Sorting");
                draft.ActiveLayer = room.LayerId; draft.Selection.Clear(); draft.Selection.Add(room.Id);
            }), "The Parcel edit did not enter its own history.");
            workshop.SelectMode("TimeRun"); workshop.ResumeEditing();
            parcelEdited = parcel.Current; parcelRevision = parcel.Revision;
            parcelUndo = parcel.CanUndo; parcelRedo = parcel.CanRedo;
            var preview = workshop.Preview();
            Require(preview.Success && preview.Manifest.CopyDocument().PhysicalSites.Count == 6 && preview.Manifest.Placements.Count == 21,
                "The exact edited Parcel course did not resolve with its six sites and complete original prop arrangement.");
            manifest = preview.Manifest.Hash;
            yield return Saved(context, parcelPrimary, parcelEdited, parcelFolder, "parcel_workshop_parcel_autosave");
            Require(File.ReadAllText(assignedPrimary) == assignedEdited.RecoveryJson, "Saving Parcel changed the isolated Assigned autosave.");
            yield return PreviewShot(context, "parcel_workshop_saved");
            var view = Read<WordboundWorkshopViewport>(workshop, "view");
            var room = parcelEdited.Course().Modules.Single(module => module.Id == "Sorting");
            view.Frame(new[] { room }); view.Zoom(-3);
            RequireOriginalProps(view);
            context.Note("parcel_workshop_focus", new Dictionary<string, object> {
                ["room"] = room.Id, ["camera_distance"] = view.Distance, ["selected"] = parcelEdited.Selection.ToArray(),
                ["scope"] = "Normal rendered Workshop viewport; public Frame/Zoom actions, unchanged full-window screenshot." });
            yield return PreviewShot(context, "parcel_workshop_sorting_close");
            RequireParcelHistory();
            Require(File.ReadAllText(parcelPrimary) == parcelEdited.RecoveryJson, "Focusing the room changed the saved draft.");
        }

        IEnumerator InspectorViews(QaContext context)
        {
            RequireParcelHistory();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(WordboundWorkshop);
            FieldInfo Field(string name) => type.GetField(name, flags) ?? throw new QaFailException("Missing inspector field: " + name);
            void Set(string name, object value) => Field(name).SetValue(workshop, value);
            void Section(string name) => type.GetMethod("ShowStep", flags).Invoke(workshop,
                new[] { Enum.Parse(Field("step").FieldType, name) });
            foreach (string name in new[] { "dragging", "marquee", "panning", "drawingZone", "orbiting", "controllerMoving", "workshopTyping" })
                Require(!(bool)Field(name).GetValue(workshop), "Inspector capture requires an idle editor: " + name);
            foreach (string name in new[] { "siteDrag", "gesture", "placementGesture", "resizingZone", "placing" })
                Require(Field(name).GetValue(workshop) == null, "Inspector capture must not cancel a gesture: " + name);
            Require(parcelEdited.Selection.SequenceEqual(new[] { "Sorting" }), "Keep the existing Sorting room selection.");
            var saved = new Dictionary<FieldInfo, object>();
            foreach (string name in new[] { "step", "inspectorScroll", "inspectorChoiceOpen", "inspectorChoiceSearch",
                "siteChoiceOpen", "siteChoiceSearch", "routeChoiceOpen", "routeChoiceSearch", "workshopTyping", "newPuzzle",
                "recipeSearch", "recipeId", "presetIndex", "puzzleId", "sitePuzzle", "inspectorAdvancedIds", "fillAdvanced",
                "rulesAdvanced", "advanced", "routesExpanded", "routeTab", "routeModule", "routeRecovery", "routeLoadKey", "routeHistory",
                "routeRevision", "routeX", "routeY", "routeZ", "routeRadius", "routeBranchLoadRevision", "routeBranchLoadId" })
            { var field = Field(name); saved.Add(field, field.GetValue(workshop)); }
            var routeAfter = Read<List<string>>(workshop, "routeAfter");
            var after = routeAfter.ToArray();
            var preview = workshop.LastPreview;
            byte[] parcelBytes = File.ReadAllBytes(parcelPrimary), assignedBytes = File.ReadAllBytes(assignedPrimary);
            var cleanupErrors = new List<string>();
            bool preserved = false;
            try
            {
                Section("Puzzles"); Set("newPuzzle", true); Set("recipeSearch", ""); Set("inspectorAdvancedIds", false);
                Set("fillAdvanced", false); Set("inspectorScroll", Vector2.zero);
                string recipe = workshop.Catalogue.Assets.Values.Where(value => value.CanCraft)
                    .Select(value => value.Id).OrderBy(value => value, StringComparer.Ordinal).First();
                Set("recipeId", recipe); Set("presetIndex", workshop.Catalogue.PuzzlePresets.ToList().IndexOf("PlaceObject"));
                Require(Read<int>(workshop, "presetIndex") >= 0, "The Parcel source must expose PlaceObject.");
                yield return PreviewShot(context, "parcel_workshop_new_puzzle");
                Require(Read<object>(workshop, "step").ToString() == "Puzzles" && Read<bool>(workshop, "newPuzzle"),
                    "The recipe image capture lost its intended inspector context.");
                NoteInspector(context, "parcel_workshop_new_puzzle_view", "Puzzles", "New puzzle", recipe);
                Section("Rules"); Set("newPuzzle", false); Set("advanced", false); Set("rulesAdvanced", false); Set("routesExpanded", true);
                Set("routeTab", 0); Set("routeModule", "Sorting");
                string recovery = parcelEdited.Course().RecoveryAnchors.First(value => value.ModuleId == "Sorting").Id;
                Set("routeRecovery", recovery); Set("routeLoadKey", ""); Set("inspectorScroll", Vector2.zero);
                yield return PreviewShot(context, "parcel_workshop_room_routes");
                Require(Read<object>(workshop, "step").ToString() == "Rules" && Read<bool>(workshop, "routesExpanded") &&
                    Read<int>(workshop, "routeTab") == 0, "The routes capture lost its intended inspector context.");
                NoteInspector(context, "parcel_workshop_room_routes_view", "Rules", "Start & finish / Safe-return points", recovery);
            }
            finally
            {
                foreach (var pair in saved)
                    try { pair.Key.SetValue(workshop, pair.Value); }
                    catch (Exception error) { cleanupErrors.Add(pair.Key.Name + ": " + error.GetType().Name); }
                try { routeAfter.Clear(); routeAfter.AddRange(after); }
                catch (Exception error) { cleanupErrors.Add("routeAfter: " + error.GetType().Name); }
                try
                {
                    RequireParcelHistory();
                    var resolved = parcel.Preview(workshop.Catalogue, workshop.PlayRequirements);
                    preserved = saved.All(pair => Equals(pair.Key.GetValue(workshop), pair.Value)) && routeAfter.SequenceEqual(after) &&
                        ReferenceEquals(workshop.LastPreview, preview) && resolved.Success && resolved.Manifest.Hash == preview.Manifest.Hash &&
                        File.ReadAllBytes(parcelPrimary).SequenceEqual(parcelBytes) && File.ReadAllBytes(assignedPrimary).SequenceEqual(assignedBytes) &&
                        ReferenceEquals(assigned.Current, assignedEdited) && assigned.Revision == assignedRevision &&
                        assigned.CanUndo == assignedUndo && assigned.CanRedo == assignedRedo;
                    VerifySources();
                }
                catch (Exception error) { cleanupErrors.Add("preservation: " + error.GetType().Name); }
                context.Note("parcel_workshop_inspector_preservation", new Dictionary<string, object> {
                    ["fields_history_seed_autosaves_sources_preserved"] = preserved && cleanupErrors.Count == 0,
                    ["cleanup_errors"] = cleanupErrors.ToArray() });
            }
            Require(preserved && cleanupErrors.Count == 0, "Inspector capture changed editor state, source data, history or autosaves.");
        }

        void NoteInspector(QaContext context, string note, string section, string panel, string reference)
        {
            Rect viewport = Read<Rect>(workshop, "viewport");
            float width = Read<float>(workshop, "inspectorWidth");
            context.Note(note, new Dictionary<string, object> {
                ["section"] = section, ["panel"] = panel, ["reference_id"] = reference,
                ["selected_rooms"] = parcelEdited.Selection.ToArray(), ["source_course_id"] = parcelEdited.Course().Id,
                ["inspector_rect"] = new[] { 8, viewport.y, width, viewport.height },
                ["scroll"] = Read<Vector2>(workshop, "inspectorScroll").y,
                ["scope"] = "View-only existing section handler and fields; no Apply, Add, physical click or layout-fit claim. Inspect unchanged full PNG." });
        }

        IEnumerator Play(QaContext context)
        {
            RequireParcelHistory();
            Require(File.ReadAllText(parcelPrimary) == parcelEdited.RecoveryJson, "Playtest requires exact verified Parcel autosave bytes.");
            workshop.RequestPlaytest();
            yield return context.WaitUntil(() => {
                client = launcher.GetComponentInChildren<WordboundLocalMatchClient>();
                if (client && client.Gameplay && client.Gameplay.Fault != "") throw new QaFailException("Parcel Workshop gameplay faulted.");
                return client && client.Gameplay && client.Gameplay.IsPresentationReady && !client.Gameplay.IsSimulationPaused;
            }, 75, "the actual edited Parcel local playtest");
            var gameplay = client.Gameplay; var view = client.GetComponent<WordboundPlayerView>();
            var hud = client.GetComponent<WordboundMatchHud>(); var guide = client.GetComponent<WordboundPuzzleGuide>();
            var artwork = client.GetComponent<WordboundGameplayPresentation>();
            Require(view && view.Player && view.Camera && hud && guide && artwork, "The playtest presentation is incomplete.");
            gameplayCamera = view.Camera; gameplayWorld = gameplay.CourseWorld;
            var resolved = Read<ResolvedCourse>(hud, "course");
            var expected = WordboundWorkshopPlaytest.Create(parcelEdited, resolved, workshop.Catalogue, workshop.PlayRequirements,
                parcelSource.Items.text, parcelSource.RuleBook.text);
            Require(resolved.CopyDocument().Id == WordboundSoloEntry.ParcelServiceCourseId && resolved.Hash == manifest &&
                resolved.SourceHash == CourseJson.Sha256(parcelEdited.CourseJsonText) && gameplayWorld.ManifestHash == manifest &&
                gameplay.Frame.Binding.CatalogueHash == expected.CatalogueHash && gameplay.Frame.Binding.ModeHash == expected.Mode.Hash &&
                gameplay.Frame.Progress.Players.Count == 1 && gameplay.Frame.Progress.Result == null,
                "The playtest differs from the exact edited saved Parcel draft.");
            expected.Validate(gameplay.Frame.Binding, Read<Wreckabulary.Rules.GameRules>(gameplay, "rules"));
            Require(ReferenceEquals(view.Profile, parcelSource.CameraProfile ? parcelSource.CameraProfile : assets.CameraProfile) &&
                ReferenceEquals(artwork.ArtworkResolver.Profile, parcelSource.VisualProfile ? parcelSource.VisualProfile : assets.VisualProfile),
                "The Workshop playtest lost the saved source profiles.");
            Require(Read<string>(hud, "returnLabel") == "Return to workshop", "The playtest has the wrong return destination.");
            var session = Read<object>(client, "session"); var runner = session.GetType().GetProperty("Runner").GetValue(session);
            Require(runner != null && runner.GetType().GetProperty("GameMode").GetValue(runner).ToString() == "Single" &&
                Object.FindObjectsByType<WordboundLocalMatchClient>(FindObjectsSortMode.None).Length == 1,
                "The Workshop playtest must use one real Single runner.");
            ownedInput = Read<InputBinding>(view, "effectiveBinding") as WordboundInputBinding;
            Require(ownedInput != null && ownedInput.BorrowedSource == null && Session.Bindings.SequenceEqual(bindings),
                "This launcher route must own its playtest controls while preserving lobby devices.");
            view.SetMenuOpen(false);
            long tick = gameplay.Frame.Progress.Tick;
            yield return Rendered(context, gameplayCamera, () => gameplay.Frame.Progress.Tick > tick + 5 && guide.Fault == "" &&
                guide.MarkerCount == WordboundPuzzleGuidePlan.Build(resolved, gameplay.Frame, gameplay.Frame.Progress.Players.Single().Id).Count &&
                guide.MarkerCount > 0, "parcel_workshop_playtest_render");
            Require(!workshop.Visible && !Object.FindFirstObjectByType<WordboundMatchClient>(), "Local playtest left its editor visible or created an online match.");
            yield return Shot(context, "parcel_workshop_playtest_world");
            view.SetMenuOpen(true); yield return context.WaitFrames(3);
            yield return Shot(context, "parcel_workshop_playtest_menu");
            RequireParcelHistory();
            context.Note("parcel_workshop_exact_playtest", new Dictionary<string, object> {
                ["source_hash"] = resolved.SourceHash, ["manifest_hash"] = manifest,
                ["catalogue_hash"] = expected.CatalogueHash, ["mode"] = expected.Mode.Id,
                ["runner"] = "Single", ["canonical_ticks"] = gameplay.Frame.Progress.Tick - tick,
                ["saved_draft_unchanged"] = File.ReadAllText(parcelPrimary) == parcelEdited.RecoveryJson });
        }

        IEnumerator Return(QaContext context)
        {
            Require(client, "Return requires the actual local playtest client.");
            yield return Completed(context, client.ReturnToWorkshop(), "the actual Workshop return handler", 30);
            yield return context.WaitFrames(3);
            Require(!client && !gameplayWorld && !gameplayCamera && workshop && workshop.Visible,
                "Returning retained the playtest client/world/camera or failed to restore the editor.");
            Require(ownedInput != null && Read<bool>(ownedInput, "disposed") && ownedInput.ControlHint(WordboundControl.Spell) == "",
                "The playtest's owned input binding was not disposed.");
            RequireParcelHistory();
            yield return PreviewShot(context, "parcel_workshop_returned");
            workshop.OpenAssignedCourse();
            RequireAssignedHistory();
            Require(File.ReadAllText(assignedPrimary) == assignedEdited.RecoveryJson, "Source switching changed Assigned autosave bytes.");
            Require(assigned.Undo() && assigned.Current.RecoveryJson == assignedInitial &&
                assigned.Redo() && assigned.Current.RecoveryJson == assignedEdited.RecoveryJson,
                "Assigned Undo/Redo did not retain the original edit across the Parcel playtest.");
            workshop.ResumeEditing();
            yield return PreviewShot(context, "parcel_workshop_assigned_returned");
            workshop.OpenParcelService(); RequireParcelHistory();
            Require(parcel.Undo() && parcel.Current.RecoveryJson == parcelInitial &&
                parcel.Redo() && parcel.Current.RecoveryJson == parcelEdited.RecoveryJson,
                "Parcel Undo/Redo did not retain its exact edit after the source switch and playtest.");
            workshop.ResumeEditing();
            yield return Saved(context, parcelPrimary, parcel.Current, parcelFolder, "parcel_workshop_returned_autosave");
            yield return PreviewShot(context, "parcel_workshop_parcel_returned");
            context.Note("parcel_workshop_histories", new Dictionary<string, object> {
                ["assigned_history_same"] = ReferenceEquals(assigned, Read<object>(Read<object>(workshop, "assignedDraft"), "History")),
                ["parcel_history_same"] = ReferenceEquals(parcel, workshop.History),
                ["assigned_undo_redo_exact"] = true, ["parcel_undo_redo_exact"] = true,
                ["separate_autosaves"] = assignedPrimary != parcelPrimary });
            yield return Leave(context);
            Require(!previewCamera && !workshop && !launcher, "Leaving Wordbound retained its Workshop preview or launcher.");
            VerifySources(); RestoreSelection();
            yield return context.WaitFrames(3); yield return Shot(context, "parcel_workshop_lobby");
        }

        void RequireAssignedHistory() => Require(workshop.UsesAssignedCourse && ReferenceEquals(workshop.History, assigned) &&
            ReferenceEquals(assigned.Current, assignedEdited) && assigned.Revision == assignedRevision &&
            assigned.CanUndo == assignedUndo && assigned.CanRedo == assignedRedo, "Assigned source history, selection or edit changed.");
        void RequireParcelHistory() => Require(workshop.UsesParcelService && ReferenceEquals(workshop.History, parcel) &&
            ReferenceEquals(parcel.Current, parcelEdited) && parcel.Revision == parcelRevision &&
            parcel.CanUndo == parcelUndo && parcel.CanRedo == parcelRedo && workshop.PlayRequirements.ModeId == "TimeRun",
            "Parcel source history, selection, mode or edit changed.");

        IEnumerator Saved(QaContext context, string path, WorkshopSnapshot snapshot, string storage, string note)
        {
            yield return context.WaitUntil(() => FileMatches(path, snapshot.RecoveryJson), 15, "exact isolated primary autosave");
            var recovered = new WorkshopStorage(storage).Recover(snapshot.Course().Id, out bool backup);
            Require(!backup && recovered.RecoveryJson == snapshot.RecoveryJson &&
                Directory.GetFiles(storage, snapshot.Course().Id + ".autosave.json.*.tmp").Length == 0,
                "The scoped autosave did not recover its exact primary draft cleanly.");
            string evidence = context.PathInRun(note + ".json"); File.Copy(path, evidence, false);
            context.Note(note, new Dictionary<string, object> { ["primary"] = path,
                ["recovery_sha256"] = WordboundIdentity.Sha256(snapshot.RecoveryJson), ["used_backup"] = backup,
                ["evidence"] = context.ToWorkspacePath(evidence) });
        }
        static bool FileMatches(string path, string expected)
        {
            if (!File.Exists(path)) return false;
            try { return File.ReadAllText(path) == expected; }
            catch (FileNotFoundException) { return false; }
            catch (IOException error) when ((error.HResult & 0xffff) == 32 || (error.HResult & 0xffff) == 33) { return false; }
        }
        IEnumerator PreviewShot(QaContext context, string name)
        {
            Require(workshop && workshop.Visible && !workshop.RecoveryChoicePending, "The actual Workshop must be visible.");
            var view = Read<WordboundWorkshopViewport>(workshop, "view");
            Require(view != null && view.Texture is RenderTexture, "The Workshop has no ordinary preview render texture.");
            var lens = Read<Camera>(view, "lens");
            previewCamera = lens;
            yield return Rendered(context, lens, () => lens.targetTexture == view.Texture && workshop.Visible, name + "_render");
            yield return Shot(context, name);
        }
        static IEnumerator Rendered(QaContext context, Camera camera, Func<bool> ready, string note)
        {
            int frames = 0, last = -1;
            void End(Camera seen) { if (seen == camera && last != Time.frameCount) { last = Time.frameCount; frames++; } }
            void Pipeline(ScriptableRenderContext _, Camera seen) { if (GraphicsSettings.currentRenderPipeline) End(seen); }
            void Builtin(Camera seen) { if (!GraphicsSettings.currentRenderPipeline) End(seen); }
            RenderPipelineManager.endCameraRendering += Pipeline; Camera.onPostRender += Builtin;
            try {
                yield return context.WaitUntil(() => camera && camera.isActiveAndEnabled && frames >= 3 && ready(), 15, "fresh assigned camera frames");
                context.Note(note, new Dictionary<string, object> { ["camera_frames"] = frames, ["camera"] = camera.name,
                    ["screen_camera"] = camera.targetTexture == null, ["active"] = camera.isActiveAndEnabled });
            }
            finally { RenderPipelineManager.endCameraRendering -= Pipeline; Camera.onPostRender -= Builtin; }
        }
        static void RequireOriginalProps(WordboundWorkshopViewport view)
        {
            var fixedProps = Read<Dictionary<string, Transform>>(view, "fixedVisuals"); var models = ModelLibrary.Load();
            Require(models, "Original model library is missing.");
            foreach (var pair in new[] { ("SortingBenchProp", "TABLE"), ("SortingShelfWest", "SHELF"), ("SortingCrateNorth", "CRATE") })
            {
                Require(fixedProps.TryGetValue(pair.Item1, out var prop) && prop && prop.gameObject.activeInHierarchy,
                    "A visible original sorting prop is missing: " + pair.Item1);
                var model = models.Find("Items/" + pair.Item2); Require(model, "The original prop model is missing.");
                var expected = Meshes(model); var actual = Meshes(prop.gameObject);
                Require(expected.Length > 0 && expected.All(actual.Contains), "The Workshop prop lost original mesh identity: " + pair.Item2);
            }
        }
        static Mesh[] Meshes(GameObject value) => value.GetComponentsInChildren<MeshFilter>(true).Select(item => item.sharedMesh)
            .Concat(value.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(item => item.sharedMesh)).Where(item => item).ToArray();
        static IEnumerator Shot(QaContext context, string name)
        {
            yield return context.CaptureScreen(name);
            string path = Path.Combine(context.OutDir, name + ".png"); Require(File.Exists(path), "The requested screen capture is missing.");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try {
                Require(ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false) && texture.width == Screen.width && texture.height == Screen.height,
                    "The screenshot differs from the actual player window.");
                var pixels = texture.GetPixels32(); var first = pixels[0];
                Require(pixels.Any(pixel => pixel.r != first.r || pixel.g != first.g || pixel.b != first.b), "The actual screenshot is uniform.");
                context.Note(name + "_capture", new Dictionary<string, object> { ["width"] = texture.width,
                    ["height"] = texture.height, ["rendered_frame"] = Time.renderedFrameCount, ["focused"] = Application.isFocused });
            }
            finally { Object.Destroy(texture); }
        }
        void Remember(Object value) { if (value && !memory.ContainsKey(value)) memory.Add(value, JsonUtility.ToJson(value)); }
        void RememberText(TextAsset value) { Require(value, "A saved course input is missing."); if (!text.ContainsKey(value)) text.Add(value, value.text); }
        void RememberCourse(WordboundCourseAsset value)
        { Remember(value); Remember(value.CameraProfile); Remember(value.VisualProfile); RememberText(value.Items); RememberText(value.RuleBook); }
        void VerifySources()
        {
            if (!stateCaptured) return;
            Require(Session.Bindings.SequenceEqual(bindings), "The Workshop changed borrowed lobby device bindings.");
            Require(LobbyThemes.Current.Id == theme, "The selected lobby theme changed.");
            foreach (var pair in memory) Require(pair.Key && JsonUtility.ToJson(pair.Key) == pair.Value, "A borrowed source/profile object changed.");
            foreach (var pair in text) Require(pair.Key && pair.Key.text == pair.Value, "A borrowed source item/rule text changed.");
            Require(preferences.All(value => value.Matches()), "The Workshop changed saved preferences.");
        }
        void RestoreSelection()
        {
            if (!stateCaptured) return;
            var changed = preferences.Where(value => !value.Matches()).ToArray();
            foreach (var preference in changed) preference.Restore();
            if (changed.Length != 0) PlayerPrefs.Save();
            Session.Bindings.Clear(); Session.Bindings.AddRange(bindings);
            WordboundSoloEntry.Select(solo); WordboundSoloEntry.SelectMap(soloMap);
            var lobby = LobbyMenu.Instance;
            if (lobby) { lobby.Choose(queue, house, map); lobby.Page<PlayPage>().WordboundSolo = soloFamily; lobby.Open(page); }
        }
        IEnumerator Leave(QaContext context)
        {
            if (!enteredWordbound) yield break;
            Require(!client || client.ShutdownConfirmed, "Confirm playtest shutdown before leaving Wordbound.");
            if (workshop) workshop.RequestClose();
            if (launcher) launcher.SendMessage("BackToLobby", SendMessageOptions.RequireReceiver);
            yield return context.WaitUntil(() => SceneManager.GetActiveScene().name == Session.HubScene && LobbyMenu.Instance &&
                !Object.FindFirstObjectByType<WordboundLauncher>() && !Object.FindFirstObjectByType<WordboundLocalMatchClient>(), 20, "the real return to the main lobby");
            enteredWordbound = false;
        }
        static IEnumerator Completed(QaContext context, Task task, string label, int seconds)
        {
            yield return context.WaitUntil(() => task.IsCompleted, seconds, label);
            if (task.IsFaulted) { _ = task.Exception; throw new QaFailException(label + " faulted."); }
            Require(!task.IsCanceled, label + " was cancelled.");
        }
        IEnumerator Guard(QaContext context, IEnumerator body)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(body);
            Exception failure = null; bool settled = false;
            double deadline = Time.realtimeSinceStartupAsDouble + 120;
            try
            {
                while (stack.Count != 0 && failure == null)
                {
                    if (Time.realtimeSinceStartupAsDouble >= deadline) { failure = new QaFailException("Parcel Workshop exceeded its 120-second work budget."); break; }
                    object current = null; bool next = false;
                    try { next = stack.Peek().MoveNext(); if (next) current = stack.Peek().Current; else (stack.Pop() as IDisposable)?.Dispose(); }
                    catch (Exception error) { failure = error; }
                    if (failure != null || !next) continue;
                    if (current is IEnumerator nested) stack.Push(nested); else yield return current;
                }
                while (stack.Count != 0) try { (stack.Pop() as IDisposable)?.Dispose(); } catch (Exception error) { failure ??= error; }
                try { VerifySources(); } catch (Exception error) { failure ??= error; }
                if (failure != null)
                {
                    Task stop = null;
                    try { if (!client && launcher) client = launcher.GetComponentInChildren<WordboundLocalMatchClient>(); if (client) stop = client.ReturnToWorkshop(); }
                    catch (Exception error) { context.Note("parcel_workshop_stop_error", error.GetType().Name); }
                    if (stop != null)
                    {
                        double stopDeadline = Time.realtimeSinceStartupAsDouble + 25;
                        while (!stop.IsCompleted && Time.realtimeSinceStartupAsDouble < stopDeadline) yield return null;
                        ObserveFailure(stop);
                        context.Note("parcel_workshop_failure_shutdown", stop.IsCompleted && !stop.IsFaulted && !stop.IsCanceled);
                        yield return null;
                    }
                    if (!client || client.ShutdownConfirmed)
                    {
                        var leave = Leave(context);
                        try { stack.Push(leave); } catch { (leave as IDisposable)?.Dispose(); throw; }
                        while (stack.Count != 0)
                        {
                            object current = null; bool next = false;
                            try { next = stack.Peek().MoveNext(); if (next) current = stack.Peek().Current; else (stack.Pop() as IDisposable)?.Dispose(); }
                            catch (Exception error) { context.Note("parcel_workshop_leave_error", error.GetType().Name); break; }
                            if (!next) continue;
                            if (current is IEnumerator nested) stack.Push(nested); else yield return current;
                        }
                    }
                    try { RestoreSelection(); } catch (Exception error) { context.Note("parcel_workshop_restore_error", error.GetType().Name); }
                }
                context.Note("parcel_workshop_preservation", new Dictionary<string, object> {
                    ["sources_preferences_devices_preserved"] = failure == null, ["failure"] = failure?.GetType().Name ?? "none",
                    ["work_budget_seconds"] = 120, ["outer_budget_seconds"] = 180 });
                settled = true; if (failure != null) throw failure;
            }
            finally
            {
                var disposalErrors = new List<string>();
                while (stack.Count != 0) try { (stack.Pop() as IDisposable)?.Dispose(); }
                    catch (Exception error) { disposalErrors.Add(error.GetType().Name); }
                if (disposalErrors.Count != 0) context.Note("parcel_workshop_iterator_disposal_errors", disposalErrors.ToArray());
                if (!settled)
                {
                    bool requested = false;
                    try { if (!client && launcher) client = launcher.GetComponentInChildren<WordboundLocalMatchClient>();
                        if (client) { ObserveFailure(client.ReturnToWorkshop()); requested = true; } }
                    catch (Exception error) { context.Note("parcel_workshop_abort_error", error.GetType().Name); }
                    try { RestoreSelection(); } catch (Exception error) { context.Note("parcel_workshop_abort_restore_error", error.GetType().Name); }
                    context.Note("parcel_workshop_abandoned_cleanup", new Dictionary<string, object> { ["stop_requested_not_confirmed"] = requested });
                }
            }
        }
        static void ObserveFailure(Task task)
        {
            if (task.IsFaulted) { _ = task.Exception; return; }
            if (!task.IsCompleted) _ = task.ContinueWith(done => { _ = done.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        static T Read<T>(object owner, string name) => (T)owner.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(owner);
        static void Require(bool condition, string message) { if (!condition) throw new QaFailException(message); }
    }
}
#endif
