using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Assert = NUnit.Framework.Assert;

namespace Wreckabulary.OfflineGameplayTests
{
    public sealed partial class WordboundNativePuzzleFlow
    {
        const string Prefix = "WordboundNativePuzzles-";
        const string Actor = "native-puzzle-player";
        const string ProofScene = "Assets/_Project/Tests/Photon/WordboundPhotonProof.unity";
        const string SavedScenesKey = "Wreckabulary.NativePuzzles.BuildScenes";
        const string PlayStartedKey = "Wreckabulary.NativePuzzles.PlayStarted";
        WordboundGameplayController controller;
        WordboundLocalSession session;
        WordboundAuthoredPuzzleContent content;
        WordboundPuzzleRuntime runtime;
        CancellationTokenSource lifetime;
        WordboundMovementInput movement;
        Task pending;
        readonly Queue<Action> setup = new Queue<Action>();
        readonly Dictionary<string, long> priorMotion = new Dictionary<string, long>();
        readonly Dictionary<string, int> slowedTicks = new Dictionary<string, int>();
        readonly HashSet<string> observedStages = new HashSet<string>();
        readonly HashSet<string> observedActorArrivals = new HashSet<string>();
        int setupMoves;
        double lifetimeStartedAt;
        string sourceJson;
        CommandReceipt lastReceipt;
        string lastCrafted;
        SetupObservation pendingSetup;
        LaunchObservation pendingLaunch;

        sealed class SetupObservation
        {
            public string BodyId;
            public Vector3 Requested;
            public long Epoch, AppliedAt = -1, ObservedAt = -1;
            public string Failure = "";
        }

        sealed class LaunchObservation
        {
            public ResolvedCoursePhysicalSite Site;
            public string ItemId;
            public float MinimumVelocity;
            public long Epoch, AfterTick, ObservedAt = -1;
        }

        MatchSnapshot Frame => controller.Frame;
        TeamProgress Team => Frame.Progress.Teams.Single();
        WorldBodyState Player => Frame.Bodies.Single(b => b.Kind == WorldBodyKind.Player && b.SourceId == Actor);
        PlayerInventory Inventory => Frame.Economy.Players[0];
        ItemInstance Item(string id) => Frame.Economy.Items.Single(i => "item:" + i.Id == id);
        WorldBodyState Body(string id) => Frame.Bodies.Single(b => b.Id == id);
        MatchMechanismState Mechanism(ResolvedCoursePhysicalSite site) =>
            Frame.Mechanisms.Single(m => m.Team == 0 && m.Lap == site.Lap - 1 && m.PuzzleId == site.PuzzleId);

        [UnityTest, Timeout(600000), Category("LocalWordboundPhysicalPuzzles")]
        public IEnumerator SixAuthoredFamiliesUseNativeSensorsAndCanonicalCheckpoints()
        {
            if (Environment.GetEnvironmentVariable("WORDBOUND_NATIVE_PUZZLES") != "1")
                Assert.Ignore("Set WORDBOUND_NATIVE_PUZZLES=1 for the native authored six-family interaction proof.");
            Assert.IsFalse(Application.isPlaying);
            var prefabs = new WordboundGameplayPrefabs(Load("Player"), Load("Item"), Load("Platform"));
            int sceneIndex = PrepareScene();
            SessionState.SetBool(PlayStartedKey, true);
            yield return new EnterPlayMode();
            Debug.Log("WORDBOUND_PHYSICAL_STAGE: entered Play Mode");
            lifetimeStartedAt = Time.realtimeSinceStartupAsDouble;
            lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(6));
            string configFolder = Path.Combine(Application.dataPath, "_Project/Data/Config");
            string itemJson = File.ReadAllText(Path.Combine(configFolder, "items.json"));
            string ruleJson = File.ReadAllText(Path.Combine(configFolder, "rules.json"));
            var flagship = WordboundFlagshipContent.Create(ItemCatalogue.FromJson(itemJson));
            var mode = ModeDefinition.For("TimeRun");
            var resolved = flagship.Resolve("0123456789abcdef", MatchStateValidation.Requirements(mode));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            content = new WordboundAuthoredPuzzleContent(mode, flagship.Geometry, resolved.Manifest, itemJson, ruleJson);
            sourceJson = CourseJson.ExportDocument(content.Course.CopyDocument());
            Debug.Log("WORDBOUND_PHYSICAL_STAGE: resolved and validated exact flagship");
            var binding = WordboundLocalSession.CreateBinding(mode, content.Course, content.CatalogueHash, Actor);
            controller = new GameObject(Prefix + "Controller").AddComponent<WordboundGameplayController>();
            runtime = new WordboundPuzzleRuntime(controller, content);
            controller.InitializeLocal(binding, mode, content.Course, content.Geometry, content.Items, content.Rules,
                Actor, prefabs, new WordboundGameplayOptions {
                    GameplayScene = SceneRef.FromIndex(sceneIndex),
                    CheckpointMilliseconds = 500,
                    ReadMovement = () => movement,
                    BeforeHostPhysics = BeforePhysics,
                    ObserveHost = ObservePhysics,
                    PuzzleEvaluator = runtime.Evaluate,
                    MovementEnvironment = runtime.EvaluateMovementEnvironment
                });
            controller.FrameChanged += RecordFrame;
            var config = new NetworkProjectConfig { PeerMode = NetworkProjectConfig.PeerModes.Multiple };
            foreach (var prefab in NetworkProjectConfig.Global.PrefabTable.Prefabs) config.PrefabTable.AddSource(prefab);
            config.Simulation.TickRateSelection = new TickRate.Selection {
                Client = 60, ClientSendInterval = 1, ServerTickInterval = 1, ServerSendInterval = 1
            };
            session = new WordboundLocalSession(() => {
                var root = new GameObject(Prefix + "Runner");
                var runner = root.AddComponent<NetworkRunner>();
                root.AddComponent<NetworkSceneManagerDefault>();
                return runner;
            }, config, controller.Authority, Actor);
            controller.AttachSession(session);
            Debug.Log("WORDBOUND_PHYSICAL_STAGE: starting local runner");
            yield return Wait(session.StartLocal(lifetime.Token), "Start isolated offline runner", 40);
            Debug.Log("WORDBOUND_PHYSICAL_STAGE: local runner started");
            yield return Until(() => session.GameplayReady && controller.PhysicsWorld != null &&
                Frame != null && !controller.IsSimulationPaused, "Build actual authored physical world", 40);
            Debug.Log("WORDBOUND_PHYSICAL_STAGE: physical world ready at tick " + Frame.Progress.Tick);
            Assert.AreEqual(GameMode.Single, session.Runner.GameMode);
            Assert.IsFalse(session.Runner.IsCloudReady);
            Assert.AreNotEqual(UnityEngine.Physics.defaultPhysicsScene, controller.PhysicsWorld.PhysicsScene);
            Assert.AreEqual(content.Course.Hash, controller.CourseWorld.ManifestHash);
            Assert.AreEqual(1f / 60f, session.Runner.DeltaTime, .000001f);
            CollectionAssert.AreEquivalent(Frame.Bodies.Select(b => b.Id), controller.Bodies.Keys);
            yield return Ticks(3);
            yield return SetPlayer(Vector(Body("keepsake:0").Position) + Vector3.back + Vector3.up * .03f, 0);
            yield return Until(() => Reachable("keepsake:0"),
                "Observe the canonical player within keepsake range and native line of sight", 5,
                () => InteractionDetails("keepsake:0"));
            yield return Command(WordboundAction.TakeKeepsake, "keepsake:0");
            Debug.Log("WORDBOUND_PHYSICAL_STAGE: keepsake acquired at tick " + Frame.Progress.Tick);
            Assert.AreEqual(Actor, Team.KeepsakeCarrier);
            var firstSites = content.Course.Laps[0].Checkpoints.SelectMany(cp => cp.PuzzleIds)
                .Take(6).Select(id => content.Course.PhysicalSites.Single(s => s.Lap == 1 && s.PuzzleId == id)).ToArray();
            CollectionAssert.AreEquivalent(new[] { "CollectLetters", "PlaceObject", "RedirectWind",
                "TimedMechanism", "BuildRoute", "BalanceWeight" },
                firstSites.Select(s => content.Course.Puzzles.Single(p => p.Id == s.PuzzleId).PresetId));
            foreach (var site in firstSites)
            {
                Debug.Log("WORDBOUND_PHYSICAL_STAGE: approaching " + site.PuzzleId);
                yield return ReachObjective(site);
                yield return CompleteSite(site);
                yield return SaveAndCheck(site);
            }
            Assert.AreEqual(6, firstSites.Count(s => Team.CompletedPuzzles.Contains(s.PuzzleId)));
            Assert.AreEqual(0, Team.Lap);
            Assert.IsFalse(Team.Finished);
            Assert.Greater(setupMoves, 0);
            Assert.AreEqual(sourceJson, CourseJson.ExportDocument(content.Course.CopyDocument()));
            Debug.Log("WORDBOUND_SIX_FAMILIES_OBSERVED: six actual authored physical mechanisms completed through normal commands " +
                "and native sensors; canonical checkpoints imported. Setup repositioned the player " + setupMoves +
                " times. This is not a human traversal or a full three-lap completion; teardown pending.");
        }

        void BeforePhysics(MatchSnapshot frame, IReadOnlyDictionary<string, WordboundNetworkBody> bodies)
        {
            while (setup.Count != 0) setup.Dequeue()();
            runtime.BeforeHostPhysics(frame, bodies);
        }

        WordboundHostObservations ObservePhysics(MatchSnapshot frame, IReadOnlyList<WorldBodyState> poses)
        {
            var observed = runtime.ObserveHost(frame, poses);
            foreach (var mechanism in observed.Mechanisms)
                if (mechanism.Physical.Stage != MatchPhysicalStage.Waiting)
                    observedStages.Add(mechanism.PuzzleId + "/" + mechanism.Physical.Stage);
            return observed;
        }

        void RecordFrame(MatchSnapshot frame)
        {
            RecordSetup(frame);
            var launch = pendingLaunch;
            if (launch != null && launch.ObservedAt < 0 && frame.Epoch == launch.Epoch &&
                frame.Progress.Tick > launch.AfterTick)
            {
                var actor = frame.Bodies.FirstOrDefault(b => b.Kind == WorldBodyKind.Player && b.SourceId == Actor);
                var state = frame.Mechanisms.FirstOrDefault(m => m.Team == 0 && m.Lap == launch.Site.Lap - 1 &&
                    m.PuzzleId == launch.Site.PuzzleId);
                if (actor != null && actor.Active && actor.Velocity.Y > launch.MinimumVelocity && state != null &&
                    state.Physical.Stage != MatchPhysicalStage.Waiting && state.Physical.ActorId == Actor &&
                    state.Physical.SiteHash == launch.Site.Hash && state.ItemEntityId == launch.ItemId)
                    launch.ObservedAt = frame.Progress.Tick;
            }
            foreach (var mechanism in frame.Mechanisms.Where(m => m.Team == 0 && m.Lap == 0))
            {
                if (priorMotion.TryGetValue(mechanism.PuzzleId, out long previous))
                {
                    long delta = mechanism.Physical.MotionUnits - previous;
                    if (delta > 0 && delta < CourseFixtureMotion.UnitsPerTick)
                        slowedTicks[mechanism.PuzzleId] = slowedTicks.TryGetValue(mechanism.PuzzleId, out int count) ? count + 1 : 1;
                }
                priorMotion[mechanism.PuzzleId] = mechanism.Physical.MotionUnits;
                if (mechanism.Physical.Stage != MatchPhysicalStage.Waiting && mechanism.Physical.ActorId == Actor)
                {
                    var site = content.Course.PhysicalSites.FirstOrDefault(s => s.Lap == 1 &&
                        s.PuzzleId == mechanism.PuzzleId && s.Hash == mechanism.Physical.SiteHash);
                    var actor = frame.Bodies.FirstOrDefault(b => b.Kind == WorldBodyKind.Player && b.SourceId == Actor);
                    if (site != null && actor != null && actor.Active && Inside(site.ArrivalBounds, Vector(actor.Position)))
                        observedActorArrivals.Add(site.Hash);
                }
            }
        }

        IEnumerator SetPlayer(Vector3 position, float yaw)
        {
            movement = default;
            var observation = BeginSetup(Player.Id, position);
            setup.Enqueue(() => {
                if (!ReferenceEquals(pendingSetup, observation)) return;
                var body = controller.Bodies[Player.Id].Rigidbody;
                body.position = position;
                body.rotation = Quaternion.Euler(0, yaw, 0);
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
                setupMoves++;
                observation.Epoch = Frame.Epoch;
                observation.AppliedAt = Frame.Progress.Tick;
            });
            try { yield return AwaitSetup(observation); }
            finally { if (ReferenceEquals(pendingSetup, observation)) pendingSetup = null; }
        }

        SetupObservation BeginSetup(string bodyId, Vector3 requested)
        {
            Assert.IsNull(pendingSetup, "Fixture setup observations must be sequential.");
            return pendingSetup = new SetupObservation { BodyId = bodyId, Requested = requested };
        }

        void RecordSetup(MatchSnapshot frame)
        {
            var observation = pendingSetup;
            if (observation == null || observation.AppliedAt < 0 || observation.ObservedAt >= 0 ||
                observation.Failure != "") return;
            if (frame.Epoch != observation.Epoch)
            {
                observation.Failure = "Epoch changed before the setup pose was captured.";
                return;
            }
            if (frame.Progress.Tick <= observation.AppliedAt) return;
            var state = frame.Bodies.FirstOrDefault(b => b.Id == observation.BodyId);
            if (frame.Progress.Tick == observation.AppliedAt + 1 && state != null &&
                controller.Bodies.TryGetValue(observation.BodyId, out var body) && body != null &&
                SetupPoseObserved(observation.Requested, state, body.Rigidbody))
                observation.ObservedAt = frame.Progress.Tick;
            else
                observation.Failure = "First post-setup frame did not capture the requested pose: appliedAt=" +
                    observation.AppliedAt + "; capturedAt=" + frame.Progress.Tick +
                    "; requested=" + observation.Requested.ToString("F3") +
                    "; canonical=" + (state == null ? "absent" : Vector(state.Position).ToString("F3"));
        }

        IEnumerator AwaitSetup(SetupObservation observation)
        {
            yield return Until(() => observation.ObservedAt >= 0 || observation.Failure != "",
                "Capture fixture setup in the first completed host physics frame", 5,
                () => "requested=" + observation.Requested.ToString("F3") + "; appliedAt=" +
                    observation.AppliedAt + "; " + InteractionDetails(observation.BodyId));
            Assert.AreEqual("", observation.Failure, InteractionDetails(observation.BodyId));
            Assert.AreEqual(observation.AppliedAt + 1, observation.ObservedAt);
        }

        static bool SetupPoseObserved(Vector3 requested, WorldBodyState canonical, Rigidbody body)
        {
            Vector3 at = Vector(canonical.Position);
            return new Vector2(at.x - requested.x, at.z - requested.z).sqrMagnitude <= .0225f &&
                Mathf.Abs(at.y - requested.y) <= .4f && (at - body.position).sqrMagnitude <= .01f;
        }

        bool Reachable(string target)
        {
            var state = Frame.Bodies.SingleOrDefault(b => b.Id == target);
            return state != null && state.Active && Player.Active &&
                content.Mode.CanInteract(0, state.OwnerTeam, state.Protected) &&
                Player.Position.DistanceSquared(state.Position) <= 9 &&
                controller.PhysicsWorld.HasLineOfSight(Actor, Player.Position, state.Position);
        }

        string InteractionDetails(string target)
        {
            var player = Player;
            var playerBody = controller.Bodies[player.Id];
            string details = "tick=" + Frame.Progress.Tick + "; player=" + Vector(player.Position).ToString("F3") +
                "; playerEngine=" + playerBody.Rigidbody.position.ToString("F3") +
                "; playerActive=" + player.Active + "; playerVelocity=" + Vector(player.Velocity).ToString("F3");
            var state = Frame.Bodies.SingleOrDefault(b => b.Id == target);
            if (state == null) return details + "; target=" + target + " absent";
            details += "; target=" + target + " at=" + Vector(state.Position).ToString("F3") +
                "; active=" + state.Active + "; owner=" + state.OwnerTeam +
                "; distance=" + Mathf.Sqrt(player.Position.DistanceSquared(state.Position)).ToString("F3") +
                "; LOS=" + controller.PhysicsWorld.HasLineOfSight(Actor, player.Position, state.Position);
            if (controller.Bodies.TryGetValue(target, out var targetBody))
                details += "; targetEngine=" + targetBody.Rigidbody.position.ToString("F3");
            var origin = Vector(player.Position) + Vector3.up * .75f;
            var delta = Vector(state.Position) + Vector3.up * .75f - origin;
            var hits = new RaycastHit[16];
            int count = delta.sqrMagnitude < .000001f ? 0 : controller.PhysicsWorld.PhysicsScene.Raycast(
                origin, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            return details + "; rayHits=" + string.Join(",", hits.Take(count).Select(hit =>
                hit.collider.name + "@" + hit.distance.ToString("F3")));
        }

        IEnumerator Command(WordboundAction action, string target = "", int revision = 0, int slot = -1,
            string value = "", string expected = "Ok")
        {
            string before = InteractionDetails(target);
            var command = controller.Submit(action, target, revision, slot, value);
            yield return Until(() => Frame.Commands.Frontiers[Actor] >= command.Sequence,
                "Apply actual " + action + " command", 8);
            lastReceipt = Frame.Commands.Receipts.Single(r => r.ActorId == Actor && r.Sequence == command.Sequence);
            Assert.AreEqual(command.PayloadHash, lastReceipt.PayloadHash);
            Assert.AreEqual(expected, lastReceipt.Effect.Code,
                action + " receipt; before: " + before + "; after: " + InteractionDetails(target));
        }

        IEnumerator Ticks(int count)
        {
            long target = Frame.Progress.Tick + count;
            yield return Until(() => Frame.Progress.Tick >= target, "Advance " + count + " native ticks", Math.Max(5, count / 30d));
        }

        IEnumerator Until(Func<bool> complete, string operation, double seconds, Func<string> details = null)
        {
            double startedAt = Time.realtimeSinceStartupAsDouble;
            long startedTick = controller?.Frame?.Progress.Tick ?? -1;
            double deadline = startedAt + seconds;
            while (!complete() && Time.realtimeSinceStartupAsDouble < deadline && !lifetime.IsCancellationRequested)
            { Healthy(); yield return null; }
            Healthy();
            if (!complete())
                Assert.Fail(operation + (lifetime.IsCancellationRequested ?
                    " stopped because the six-minute fixture lifetime was cancelled." : " exceeded its deadline.") +
                    " Wait seconds=" + (Time.realtimeSinceStartupAsDouble - startedAt).ToString("F3") +
                    "; budgetSeconds=" + seconds + "; startedTick=" + startedTick + "; " + RunDetails() +
                    (details == null ? "" : "; " + details()));
        }

        string RunDetails() => "tick=" + (controller?.Frame?.Progress.Tick ?? -1) +
            "; fixtureSeconds=" + (Time.realtimeSinceStartupAsDouble - lifetimeStartedAt).ToString("F3") +
            "; lifetimeCancelled=" + (lifetime?.IsCancellationRequested ?? false) +
            "; runnerStarted=" + (session?.RunnerStarted ?? false) +
            "; runnerRunning=" + (session?.Runner != null && session.Runner.IsRunning) +
            "; gameplayReady=" + (session?.GameplayReady ?? false) +
            "; simulationPaused=" + (controller?.IsSimulationPaused ?? true) +
            "; timeScale=" + Time.timeScale.ToString("F3");

        IEnumerator Wait(Task task, string operation, double seconds)
        {
            pending = task;
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline && !lifetime.IsCancellationRequested)
            { Healthy(); yield return null; }
            Assert.IsTrue(task.IsCompleted, operation + (lifetime.IsCancellationRequested ?
                " stopped because the six-minute fixture lifetime was cancelled. " : " exceeded its deadline. ") + RunDetails());
            Assert.IsFalse(task.IsCanceled, operation + " was cancelled.");
            if (task.IsFaulted) Assert.Fail(operation + " failed: " + task.Exception.GetBaseException());
            pending = null;
        }

        void Healthy()
        {
            Assert.AreEqual("", session?.Failure ?? "");
            Assert.AreEqual("", controller?.Fault ?? "");
        }

        static Vector3 Vector(WorldVector p) => new Vector3(p.X, p.Y, p.Z);
        static Vector3 Metres(CoursePoint p) => new Vector3(p.X, p.Y, p.Z) / 1000f;
        static bool Inside(CourseBox box, Vector3 p) =>
            p.x >= box.Min.X / 1000f && p.x <= box.Max.X / 1000f &&
            p.y >= box.Min.Y / 1000f && p.y <= box.Max.Y / 1000f &&
            p.z >= box.Min.Z / 1000f && p.z <= box.Max.Z / 1000f;
        static NetworkObject Load(string kind)
        {
            string path = "Assets/_Project/Prefabs/Wordbound/Network/Wordbound" + kind + "Body.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, "The root must generate the native prefab first: " + path);
            return prefab.GetComponent<NetworkObject>();
        }

        static int PrepareScene()
        {
            Assert.IsTrue(File.Exists(ProofScene));
            Assert.AreEqual("", SessionState.GetString(SavedScenesKey, ""));
            var prior = EditorBuildSettings.scenes;
            SessionState.SetString(SavedScenesKey, JsonUtility.ToJson(new SavedScenes {
                paths = prior.Select(s => s.path).ToArray(), enabled = prior.Select(s => s.enabled).ToArray()
            }));
            var next = prior.Where(s => s.path != ProofScene)
                .Concat(new[] { new EditorBuildSettingsScene(ProofScene, true) }).ToArray();
            EditorBuildSettings.scenes = next;
            return next.Count(s => s.enabled) - 1;
        }

        [Serializable] sealed class SavedScenes { public string[] paths; public bool[] enabled; }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            lifetime?.Cancel();
            movement = default;
            setup.Clear();
            pendingSetup = null;
            pendingLaunch = null;
            int failures = 0;
            Task stop = null;
            if (controller != null) controller.FrameChanged -= RecordFrame;
            try { stop = controller != null ? controller.Stop() : session?.Stop(); } catch { failures++; }
            if (stop != null)
            {
                double deadline = Time.realtimeSinceStartupAsDouble + 20;
                while (!stop.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (!stop.IsCompleted || stop.IsCanceled) failures++;
                if (stop.IsFaulted) { _ = stop.Exception; failures++; }
            }
            if (pending != null)
            {
                double deadline = Time.realtimeSinceStartupAsDouble + 5;
                while (!pending.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (!pending.IsCompleted) failures++;
                if (pending.IsFaulted) _ = pending.Exception;
            }
            foreach (var root in Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(g => g && g.scene.IsValid() && g.name.StartsWith(Prefix, StringComparison.Ordinal)).ToArray())
                UnityEngine.Object.Destroy(root);
            if (Application.isPlaying) yield return null;
            bool leaked = Resources.FindObjectsOfTypeAll<GameObject>()
                .Any(g => g && g.scene.IsValid() && g.name.StartsWith(Prefix, StringComparison.Ordinal));
            controller = null; session = null; runtime = null; pending = null;
            lifetime?.Dispose(); lifetime = null;
            if (Application.isPlaying && SessionState.GetBool(PlayStartedKey, false)) yield return new ExitPlayMode();
            SessionState.EraseBool(PlayStartedKey);
            string saved = SessionState.GetString(SavedScenesKey, "");
            if (saved != "")
            {
                var prior = JsonUtility.FromJson<SavedScenes>(saved);
                EditorBuildSettings.scenes = prior.paths.Select((path, i) => new EditorBuildSettingsScene(path, prior.enabled[i])).ToArray();
                SessionState.EraseString(SavedScenesKey);
            }
            if (content != null) Assert.AreEqual(sourceJson, CourseJson.ExportDocument(content.Course.CopyDocument()));
            content = null; sourceJson = null; lastReceipt = null; lastCrafted = null;
            setupMoves = 0; priorMotion.Clear(); slowedTicks.Clear(); observedStages.Clear(); observedActorArrivals.Clear();
            Assert.AreEqual(0, failures, "Every started operation and local runner must finish cleanup.");
            Assert.IsFalse(leaked, "Native puzzle test roots must not survive teardown.");
        }
    }
}
