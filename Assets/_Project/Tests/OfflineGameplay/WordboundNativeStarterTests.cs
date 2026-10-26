using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Art;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;

namespace Wreckabulary.OfflineGameplayTests
{
    public sealed class WordboundNativeStarterTests
    {
        const string Actor = WordboundLocalMatchClient.LocalActor;
        const string Puzzle = "ParcelStand";
        GameObject root;
        WordboundLocalMatchClient client;
        WordboundGameplayController controller;
        WordboundGameplayOptions options;
        Func<WordboundMovementInput> previousMovement, scriptedMovement;
        CancellationTokenSource lifetime;
        Action<MatchSnapshot> observation;
        ResolvedCourse course;
        bool enteredPlay;
        int movementReads;
        string folder, observationError = "";
        readonly Dictionary<int, long> lapTicks = new Dictionary<int, long>();
        readonly HashSet<int> objectives = new HashSet<int>();
        readonly HashSet<string> collected = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> crafted = new HashSet<string>(StringComparer.Ordinal);
        MatchSnapshot Frame => controller.Frame;
        TeamProgress Team => Frame.Progress.Teams.Single();
        PlayerInventory Inventory => Frame.Economy.Players[0];
        WorldBodyState Player => Frame.Bodies.Single(body => body.Kind == WorldBodyKind.Player && body.SourceId == Actor);
        MatchMechanismState Mechanism(int lap) => Frame.Mechanisms.Single(state => state.Team == 0 && state.Lap == lap && state.PuzzleId == Puzzle);
        WorldBodyState Body(string id) => Frame.Bodies.Single(body => body.Id == id);

        [UnityTest, Category("LocalWordboundStarter"), Timeout(480000)]
        public IEnumerator ExactRaceStarterCompletesThreePhysicalLapsAndReturnsToSameEditableDraft()
        {
            if (Environment.GetEnvironmentVariable("WORDBOUND_LOCAL_STARTER") != "1")
                Assert.Ignore("Set WORDBOUND_LOCAL_STARTER=1 for scripted native starter completion and real launcher return.");
            Assert.IsFalse(Application.isPlaying);
            enteredPlay = true;
            yield return new EnterPlayMode();
            lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(6));
            folder = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "wb-starter", Guid.NewGuid().ToString("N")));
            root = new GameObject("WordboundNativeStarter-" + Guid.NewGuid().ToString("N"));
            var cameraRoot = new GameObject("Starter proof lobby camera");
            cameraRoot.transform.SetParent(root.transform, false);
            var lobbyCamera = cameraRoot.AddComponent<Camera>();
            var launcher = root.AddComponent<WordboundLauncher>();
            launcher.LobbyCamera = lobbyCamera;
            launcher.ConfigureQaWorkshopStorage(folder);
            yield return null;
            var menu = root.GetComponent<WordboundPartyMenu>();
            Assert.IsNotNull(menu); Assert.IsNotNull(menu.Coordinator);
            launcher.SendMessage("OpenWorkshop", SendMessageOptions.RequireReceiver);
            var workshop = root.GetComponent<WordboundWorkshop>();
            Assert.IsNotNull(workshop); Assert.IsFalse(workshop.RecoveryChoicePending);
            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit, folder);
            var preset = WorkshopTemplates.Create(WorkshopTemplate.RaceCircuit);
            Assert.AreEqual("TimeRun", workshop.PlayRequirements.ModeId);
            Assert.AreEqual(3, preset.Snapshot.Course().PhysicalSites.Count);
            var history = workshop.History;
            string exactDraft = history.Current.RecoveryJson;
            Assert.AreEqual(preset.Snapshot.CourseJsonText, history.Current.CourseJsonText);
            history.Execute("Starter history witness", draft => draft.Course.Title = "Temporary history witness");
            string redoDraft = history.Current.RecoveryJson;
            Assert.IsTrue(history.Undo());
            Assert.AreEqual(exactDraft, history.Current.RecoveryJson);
            Assert.IsTrue(history.CanRedo);
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            Assert.IsNotNull(data);
            string itemsJson = data.items.text, rulesJson = data.rules.text;
            var items = ItemCatalogue.FromJson(itemsJson);
            float originalDurability = items.Get("STOOL").Durability;
            string primary = Path.Combine(folder, "templates", "RaceCircuit", "race-proof.autosave.json");
            yield return Until(() => Saved(primary, exactDraft), "Save the exact starter through the Workshop autosave", 15);
            workshop.RequestPlaytest();
            Assert.IsTrue(workshop.LastPreview.Success, string.Join("\n", workshop.LastPreview.Issues));
            course = workshop.LastPreview.Manifest;
            Assert.AreEqual("", WorkshopPhysicalSites.PlaytestIssue(course));
            Assert.AreEqual(history.Current.CourseJsonText, CourseJson.ExportDocument(course.CopyDocument()));
            yield return Until(() => {
                client = root.GetComponentInChildren<WordboundLocalMatchClient>();
                controller = client ? client.Gameplay : null;
                return controller && controller.Frame != null && controller.CourseWorld &&
                    !controller.IsSimulationPaused && !lobbyCamera.enabled;
            }, "Start the exact starter through the production local launcher", 60);
            Assert.IsFalse(workshop.Visible); Assert.AreSame(history.Current, workshop.LastPlaytestDraft);
            Assert.AreEqual(course.Hash, controller.CourseWorld.ManifestHash);
            Assert.AreEqual(course.SourceHash, Frame.Binding.CourseHash);
            Assert.AreEqual(course.Hash, Frame.Binding.ManifestHash);
            Assert.AreEqual(0, Team.Lap); Assert.AreEqual(0, Team.OrderedObjectives);
            Assert.IsNull(Frame.Progress.Result);
            var view = client.GetComponent<WordboundPlayerView>();
            var presentation = client.GetComponent<WordboundGameplayPresentation>();
            var guide = client.GetComponent<WordboundPuzzleGuide>();
            Assert.IsNotNull(view); Assert.IsNotNull(view.Camera); Assert.IsNotNull(view.Player);
            Assert.IsNotNull(client.GetComponent<WordboundMatchHud>());
            Assert.IsNotNull(presentation); Assert.IsNotNull(guide);
            yield return Until(() => guide.MarkerCount > 0, "Show actual starter puzzle markers", 10);
            Assert.AreEqual("", guide.Fault);
            var field = typeof(WordboundGameplayController).GetField("options", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field); Assert.AreEqual(typeof(WordboundGameplayOptions), field.FieldType);
            options = (WordboundGameplayOptions)field.GetValue(controller);
            Assert.IsNotNull(options); Assert.IsNotNull(options.ReadMovement);
            previousMovement = options.ReadMovement;
            scriptedMovement = () => default;
            options.ReadMovement = ReadScriptedMovement;
            var finish = Metres(course.RecoveryAnchors.Single(anchor => anchor.Id == "DeliverySafe").Position);
            long priorTick = Frame.Progress.Tick;
            int priorLap = 0, priorObjectives = 0, priorCheckpoint = 0;
            observation = frame => {
                var team = frame.Progress.Teams.Single();
                if (frame.Progress.Tick < priorTick || team.Lap < priorLap || team.Lap > priorLap + 1 ||
                    team.OrderedObjectives != team.Lap * 3 + team.NextCheckpoint || team.OrderedObjectives < priorObjectives ||
                    team.Lap == priorLap && (team.NextCheckpoint < priorCheckpoint || team.NextCheckpoint > priorCheckpoint + 1) ||
                    team.Lap == priorLap + 1 && (priorCheckpoint < 1 || team.NextCheckpoint != 0 || team.OrderedObjectives - priorObjectives > 2))
                    observationError = "Natural starter progression skipped or regressed.";
                objectives.Add(team.OrderedObjectives);
                if (team.Lap > priorLap)
                {
                    var actor = frame.Bodies.Single(body => body.Kind == WorldBodyKind.Player && body.SourceId == Actor);
                    if (team.KeepsakeCarrier != Actor || actor.Position.DistanceSquared(finish) > .8f * .8f)
                        observationError = "Lap transition did not contain the actual keepsake carrier at the authored finish.";
                    lapTicks[team.Lap] = frame.Progress.Tick;
                }
                priorTick = frame.Progress.Tick; priorLap = team.Lap; priorObjectives = team.OrderedObjectives; priorCheckpoint = team.NextCheckpoint;
            };
            observation(Frame); controller.FrameChanged += observation;
            yield return Walk(() => Body("keepsake:0").Position, .8f, "Reach the real keepsake");
            yield return Command(WordboundAction.TakeKeepsake, "keepsake:0");
            Assert.AreEqual(Actor, Team.KeepsakeCarrier);
            var source = course.Supplies.Single(supply => supply.SourceId == "StoolLetters" && supply.TeamIndex == 0);
            Assert.IsTrue(source.Protected && source.Renewable);
            for (int lap = 0; lap < 3; lap++)
            {
                int currentLap = lap;
                Assert.AreEqual(lap, Team.Lap); Assert.AreEqual(0, Team.NextCheckpoint);
                Assert.IsNull(Frame.Progress.Result); Assert.IsTrue(Inventory.Letters.IsEmpty);
                yield return Walk(() => new WorldVector(-4, 0, -3), .25f, "Leave the previous finish through admitted movement");
                yield return Until(() => Frame.Bodies.Count(body => body.Kind == WorldBodyKind.Tile && body.SourceId == source.Id) == 5,
                    "Expose this lap's renewable STOOL letter batch", 15);
                var batch = Frame.Bodies.Where(body => body.Kind == WorldBodyKind.Tile && body.SourceId == source.Id).ToArray();
                Assert.AreEqual(1, batch.Select(body => body.RecoveryGeneration).Distinct().Count());
                Assert.AreEqual(lap, batch[0].RecoveryGeneration % 3);
                foreach (var letter in batch)
                {
                    Assert.IsTrue(letter.Protected); Assert.AreEqual(0, letter.OwnerTeam);
                    Assert.IsTrue(collected.Add(letter.Id), "A new lap must supply fresh real letter entities.");
                    yield return Walk(() => Body(letter.Id).Position, .85f, "Walk to each actual letter");
                    yield return Command(WordboundAction.CollectLetter, letter.Id);
                    Assert.IsFalse(Frame.Bodies.Any(body => body.Id == letter.Id));
                }
                Assert.IsTrue(Inventory.Letters.Equals(LetterBag.FromWord("STOOL")));
                int firstItem = Frame.Economy.NextItemId;
                yield return Command(WordboundAction.BeginCraft, value: "STOOL");
                yield return Until(() => Frame.Economy.Items.Any(item => item.Id >= firstItem && item.Word == "STOOL" &&
                    item.Origin == ItemOrigin.Crafted && item.HolderId == 0 && item.State == ItemState.Held),
                    "Finish the unchanged installed STOOL crafting timer", 20);
                var stool = Frame.Economy.Items.Single(item => item.Id >= firstItem && item.Word == "STOOL" &&
                    item.Origin == ItemOrigin.Crafted && item.HolderId == 0 && item.State == ItemState.Held);
                string id = "item:" + stool.Id;
                Assert.IsTrue(crafted.Add(id)); Assert.AreEqual(originalDurability, stool.Durability);
                Assert.IsTrue(Inventory.Reserved.IsEmpty); Assert.IsTrue(Inventory.Letters.IsEmpty);
                var site = course.PhysicalSites.Single(value => value.Lap == lap + 1);
                Assert.AreEqual(Puzzle, site.PuzzleId); Assert.AreEqual(0, site.Yaw);
                var target = Metres(site.Position);
                yield return Walk(() => new WorldVector(target.X, target.Y, target.Z - 1.5f), .08f,
                    "Approach the authored socket through the real motor");
                scriptedMovement = () => new WordboundMovementInput { HasFacing = true, FacingYaw = 0 };
                yield return Until(() => Vector3.Dot(PlayerBody().Rigidbody.rotation * Vector3.forward, Vector3.forward) > .999f &&
                    HorizontalSpeedSquared() < .01f, "Face the socket through admitted facing input", 10);
                int slot = Array.IndexOf(Inventory.Slots, stool.Id);
                Assert.GreaterOrEqual(slot, 0);
                yield return Command(WordboundAction.Deploy, slot: slot);
                scriptedMovement = () => default;
                Assert.AreEqual(ItemState.Deployed, Frame.Economy.Items.Single(item => item.Id == stool.Id).State);
                Assert.IsTrue(controller.Bodies[id].Rigidbody.isKinematic);
                Assert.IsNotNull(controller.Bodies[id].GetComponent<BoxCollider>());
                yield return Until(() => Mechanism(currentLap).Physical.Stage == MatchPhysicalStage.Complete,
                    "Hold the real native STOOL in the unchanged physical socket", 15);
                Assert.AreEqual(id, Mechanism(lap).ItemEntityId);
                Assert.GreaterOrEqual(Mechanism(lap).ProgressTicks, site.HoldTicks);
                Assert.AreEqual(site.Hash, Mechanism(lap).Physical.SiteHash);
                yield return Until(() => OriginalStoolVisible(id, presentation), "Present the deployed original STOOL mesh", 10);
                yield return Command(WordboundAction.UsePuzzle, Puzzle, value: id);
                Assert.IsTrue(Mechanism(lap).Solved); Assert.AreEqual(id, Mechanism(lap).ItemEntityId);
                Assert.IsTrue(Frame.Economy.Items.Single(item => item.Id == stool.Id).IsGone);
                Assert.IsFalse(Frame.Bodies.Any(body => body.Id == id));
                Assert.IsFalse(controller.Bodies.ContainsKey(id));
                Assert.IsFalse(Frame.Mechanisms.Any(state => state.Physical.AuxiliaryBodyId == id || !state.Solved && state.ItemEntityId == id));
                var checkpoint = Metres(course.RecoveryAnchors.Single(anchor => anchor.Id == "StartSafe").Position);
                yield return Walk(() => checkpoint, .25f, "Cross the required ordered checkpoint with the keepsake");
                yield return Until(() => Team.NextCheckpoint == 1, "Observe the native first checkpoint", 10);
                Assert.AreEqual(lap, Team.Lap); Assert.IsNull(Frame.Progress.Result);
                yield return Walk(() => finish, .4f, "Deliver the keepsake through native movement and support",
                    () => Team.Lap == currentLap + 1);
                yield return Until(() => Team.Lap == currentLap + 1, "Observe exactly one natural lap transition", 10);
                Assert.IsEmpty(observationError);
                Debug.Log("WORDBOUND_STARTER_LAP: lap=" + (lap + 1) + "; tick=" + lapTicks[lap + 1] +
                    "; item=" + id + "; renewableGeneration=" + batch[0].RecoveryGeneration +
                    "; physicalSocket=true; originalMesh=true; nativeCarrierDelivery=true");
            }
            yield return Until(() => Frame.Progress.Result?.Confirmed == true, "Confirm the natural local terminal result", 20);
            Assert.AreEqual("Win", Frame.Progress.Result.Outcome);
            CollectionAssert.AreEqual(new[] { 0 }, Frame.Progress.Result.WinningTeams);
            Assert.AreEqual(3, Team.Lap); Assert.IsTrue(Team.Finished); Assert.AreEqual(9, Team.OrderedObjectives);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, lapTicks.Keys);
            foreach (int required in new[] { 0, 1, 3, 4, 6, 7, 9 }) Assert.IsTrue(objectives.Contains(required),
                "Each lap must expose its start, solved first checkpoint and delivery; the final checkpoint and finish may share one native tick.");
            Assert.AreEqual(15, collected.Count); Assert.AreEqual(3, crafted.Count);
            Assert.AreEqual(3, Frame.Mechanisms.Count(state => state.Solved));
            Assert.Greater(movementReads, 0); Assert.IsEmpty(observationError);
            Assert.AreEqual(itemsJson, data.items.text); Assert.AreEqual(rulesJson, data.rules.text);
            Assert.AreEqual(exactDraft, history.Current.RecoveryJson); Assert.IsTrue(Saved(primary, exactDraft));
            RestoreMovement(); DetachObservation();
            var ownedCamera = view.Camera;
            yield return Complete(client.ReturnToWorkshop(), "Return through the production local client after the confirmed win", 30);
            yield return null;
            Assert.IsTrue(client == null); Assert.IsTrue(ownedCamera == null); Assert.IsTrue(guide == null);
            Assert.IsTrue(workshop.Visible); Assert.IsFalse(menu.Visible); Assert.IsTrue(lobbyCamera.enabled);
            Assert.AreSame(history, workshop.History); Assert.AreEqual(exactDraft, history.Current.RecoveryJson);
            Assert.IsTrue(Saved(primary, exactDraft));
            Assert.IsTrue(history.Redo()); Assert.AreEqual(redoDraft, history.Current.RecoveryJson);
            Assert.IsTrue(history.Undo()); Assert.AreEqual(exactDraft, history.Current.RecoveryJson);
            Debug.Log("WORDBOUND_STARTER_COMPLETE: exact RaceCircuit template; three real STOOL batches/crafts/socket holds/consumptions; " +
                "three native carrier deliveries; confirmed local Win; actual ReturnToWorkshop; same saved draft and Undo/Redo history; " +
                "scripted admitted motor input and submitted commands; not physical device/menu-selection or public matchmaking acceptance; cleanup pending.");
        }

        WordboundMovementInput ReadScriptedMovement()
        {
            if (lifetime == null || lifetime.IsCancellationRequested) return default;
            movementReads++;
            return scriptedMovement?.Invoke() ?? default;
        }

        IEnumerator Walk(Func<WorldVector> target, float radius, string stage, Func<bool> delivered = null)
        {
            scriptedMovement = () => {
                if (delivered?.Invoke() == true) return default;
                var at = PlayerBody().Rigidbody.position; var goal = target();
                var delta = new Vector2(goal.X - at.x, goal.Z - at.z);
                float distance = delta.magnitude;
                if (distance <= radius) return default;
                var move = delta.normalized * Mathf.Clamp((distance - radius * .5f) * .07f, .06f, .22f);
                return new WordboundMovementInput { MoveX = move.x, MoveZ = move.y };
            };
            try { yield return Until(() => delivered?.Invoke() == true || HorizontalDistance(Player.Position, target()) <= radius, stage, 35); }
            finally { scriptedMovement = () => default; }
            if (Frame.Progress.Result == null)
                yield return Until(() => HorizontalSpeedSquared() < .01f, "Settle through the unchanged motor and friction", 10);
            Assert.IsTrue(delivered?.Invoke() == true || HorizontalDistance(Player.Position, target()) <= radius + .2f);
            Assert.AreEqual(0, Frame.Progress.Players.Single(player => player.Id == Actor).Knockouts);
        }

        IEnumerator Command(WordboundAction action, string target = "", int revision = 0, int slot = -1, string value = "")
        {
            Healthy();
            var command = controller.Submit(action, target, revision, slot, value);
            yield return Until(() => Frame.Commands.Frontiers.TryGetValue(Actor, out long frontier) && frontier >= command.Sequence,
                "Commit real " + action + " command", 10);
            var receipt = Frame.Commands.Receipts.Single(item => item.ActorId == Actor && item.Sequence == command.Sequence);
            Assert.AreEqual(command.PayloadHash, receipt.PayloadHash);
            Assert.AreEqual("Ok", receipt.Effect.Code, action + " must pass the unchanged authority and physical rules.");
        }

        IEnumerator Until(Func<bool> done, string stage, double seconds)
        {
            double started = Time.realtimeSinceStartupAsDouble;
            Debug.Log("WORDBOUND_STARTER_STAGE: " + stage);
            while (!done() && Time.realtimeSinceStartupAsDouble - started < seconds)
            { Healthy(); yield return null; }
            Healthy();
            Assert.IsTrue(done(), stage + " exceeded " + seconds + " seconds; tick=" + (controller?.Frame?.Progress.Tick ?? -1));
        }

        void Healthy()
        {
            Assert.IsTrue(lifetime != null && !lifetime.IsCancellationRequested, "Starter fixture cancelled or exceeded its six-minute budget.");
            if (controller) Assert.AreEqual("", controller.Fault);
            Assert.IsEmpty(observationError);
        }

        static IEnumerator Complete(Task task, string stage, double seconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsTrue(task.IsCompleted, stage + " exceeded its shutdown deadline.");
            Assert.IsFalse(task.IsCanceled, stage + " cancelled.");
            if (task.IsFaulted) Assert.Fail(stage + ": " + task.Exception.GetBaseException());
        }

        bool OriginalStoolVisible(string id, WordboundGameplayPresentation presentation)
        {
            Assert.AreEqual("Items/STOOL", presentation.ArtworkResolver.ModelKey("STOOL"));
            var source = ModelLibrary.Load()?.Find("Items/STOOL");
            Assert.IsNotNull(source);
            var visual = controller.Bodies[id].GetComponent<WordboundGameplayBody>().VisualRoot;
            var art = visual ? visual.Find("Wordbound artwork STOOL") : null;
            if (!art || !art.gameObject.activeInHierarchy) return false;
            CollectionAssert.AreEqual(Meshes(source), Meshes(art.gameObject));
            return art.GetComponentsInChildren<Renderer>().Any(renderer => renderer.enabled && !renderer.forceRenderingOff);
        }

        static Mesh[] Meshes(GameObject value) => value.GetComponentsInChildren<MeshFilter>(true).Select(filter => filter.sharedMesh)
            .Concat(value.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(renderer => renderer.sharedMesh)).ToArray();
        WordboundNetworkBody PlayerBody() => controller.Bodies[Player.Id];
        float HorizontalSpeedSquared() { var velocity = Player.Velocity; return velocity.X * velocity.X + velocity.Z * velocity.Z; }
        static WorldVector Metres(CoursePoint point) => new WorldVector(point.X / 1000f, point.Y / 1000f, point.Z / 1000f);
        static float HorizontalDistance(WorldVector a, WorldVector b) => new Vector2(a.X - b.X, a.Z - b.Z).magnitude;
        static bool Saved(string path, string expected)
        {
            try { return File.Exists(path) && File.ReadAllText(path) == expected; }
            catch (IOException) { return false; }
        }
        void RestoreMovement()
        {
            scriptedMovement = null;
            if (options != null && previousMovement != null)
            {
                bool owned = options.ReadMovement == (Func<WordboundMovementInput>)ReadScriptedMovement;
                if (owned) options.ReadMovement = previousMovement;
                Assert.IsTrue(owned, "Only the fixture-owned input delegate may be restored.");
            }
            options = null; previousMovement = null;
        }
        void DetachObservation()
        {
            if (controller && observation != null) controller.FrameChanged -= observation;
            observation = null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var failures = new List<string>();
            lifetime?.Cancel();
            try { RestoreMovement(); DetachObservation(); }
            catch (Exception error) { failures.Add("input/observer: " + error.GetType().Name); }
            Task stop = null;
            try { if (client) stop = client.ReturnToWorkshop(); }
            catch (Exception error) { failures.Add("stop start: " + error.GetType().Name); }
            if (stop != null)
            {
                double deadline = Time.realtimeSinceStartupAsDouble + 30;
                while (!stop.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (!stop.IsCompleted || stop.IsCanceled)
                {
                    failures.Add("local shutdown incomplete");
                    if (!stop.IsCompleted) _ = stop.ContinueWith(done => { _ = done.Exception; }, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
                else if (stop.IsFaulted) { failures.Add("local shutdown: " + stop.Exception.GetBaseException().GetType().Name); }
            }
            if (root) UnityEngine.Object.Destroy(root);
            if (Application.isPlaying) yield return null;
            if (root) failures.Add("owned launcher root retained");
            client = null; controller = null;
            lifetime?.Dispose(); lifetime = null;
            if (Application.isPlaying && enteredPlay) yield return new ExitPlayMode();
            enteredPlay = false;
            Debug.Log("WORDBOUND_STARTER_CLEANUP: failures=" + failures.Count + "; autosaveEvidence=" + folder);
            Assert.IsEmpty(failures, string.Join("; ", failures));
        }
    }
}
