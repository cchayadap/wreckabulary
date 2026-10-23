using System;
using System.Collections;
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
    public sealed class WordboundNativeLocalGameplayTests
    {
        const string Prefix = "WordboundNativeLocal-";
        const string ProofScene = "Assets/_Project/Tests/Photon/WordboundPhotonProof.unity";
        const string PriorScenesKey = "Wreckabulary.NativeLocal.PriorScenes";
        const string PlayStartedKey = "Wreckabulary.NativeLocal.PlayStarted";
        const string Actor = "local-workshop-player";
        WordboundGameplayController controller;
        WordboundLocalSession session;
        CancellationTokenSource lifetime;
        Task pending;
        WordboundMovementInput movement;
        WorkshopSnapshot draft;
        string originalDraft;

        [UnityTest, Category("LocalWordboundGameplay")]
        public IEnumerator FreestyleUsesOfflineRunnerAndExactWorkshopManifest() => Run("Freestyle");

        [UnityTest, Category("LocalWordboundGameplay")]
        public IEnumerator TimeRunUsesOfflineRunnerAndExactWorkshopManifest() => Run("TimeRun");

        [UnityTest, Category("LocalWordboundGameplay")]
        public IEnumerator AtomicPhysicsApplyRetainsValidationAndAbortGuards() => Run("Freestyle", "atomic");

        [UnityTest, Category("LocalWordboundGameplay")]
        public IEnumerator PresentationStopCannotPublishAnAbandonedReconciliation() => Run("Freestyle", "stop");

        [UnityTest, Category("LocalWordboundGameplay")]
        public IEnumerator PresentationFailureKeepsItsExceptionAndCannotPublishAFrame() => Run("Freestyle", "throw");

        [UnityTest, Category("LocalWordboundGameplay")]
        public IEnumerator PresentationPollCannotReenterInitialHostComposition() => Run("Freestyle", "reenter");

        [UnityTest, Category("LocalWordboundGameplay")]
        public IEnumerator FirstAppliedFrameCannotReenterInitialHostComposition() => Run("Freestyle", "frame-reenter");

        IEnumerator Run(string selectedMode, string atomicCase = "")
        {
            if (Environment.GetEnvironmentVariable("WORDBOUND_LOCAL_GAMEPLAY") != "1")
                Assert.Ignore("Set WORDBOUND_LOCAL_GAMEPLAY=1 to run the native offline workshop proof.");
            Assert.IsFalse(Application.isPlaying);
            var prefabs = new WordboundGameplayPrefabs(Load("Player"), Load("Item"), Load("Platform"));
            int sceneIndex = PrepareScene();
            SessionState.SetBool(PlayStartedKey, true);
            yield return new EnterPlayMode();
            lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var mode = ModeDefinition.For(selectedMode);
            var geometry = CourseTemplates.ProofCatalogue();
            draft = new WorkshopSnapshot(new WorkshopDraft {
                Course = selectedMode == "TimeRun" ? CourseTemplates.RaceCircuit() : CourseTemplates.FreestyleWorld(),
                Seed = "0314159265358979", SeedLocked = true
            });
            originalDraft = draft.RecoveryJson;
            var resolved = CourseResolver.Resolve(draft.Course(), geometry,
                new CourseResolveRequest(draft.Seed, MatchStateValidation.Requirements(mode)));
            Assert.IsTrue(resolved.Success, "The explicitly selected local mode must validate before launch.");
            var course = resolved.Manifest;
            string folder = Path.Combine(Application.dataPath, "_Project/Data/Config");
            string itemJson = File.ReadAllText(Path.Combine(folder, "items.json"));
            string ruleJson = File.ReadAllText(Path.Combine(folder, "rules.json"));
            var items = ItemCatalogue.FromJson(itemJson, "native-local-items");
            var rules = mode.CreateGameplayRules(RuleBook.FromJson(ruleJson, "native-local-rules").For("Dibs"));
            string contentHash = WordboundIdentity.Sha256(geometry.Hash + "|" + WordboundIdentity.Sha256(itemJson) +
                "|" + WordboundIdentity.Sha256(ruleJson));
            var binding = WordboundLocalSession.CreateBinding(mode, course, contentHash, Actor);
            controller = new GameObject(Prefix + "Controller").AddComponent<WordboundGameplayController>();
            int visualCallbacks = 0, framesAfterCallback = 0, advancesAfterCallback = 0;
            MatchSnapshot frameAtCallback = null;
            controller.FrameChanged += _ => { if (visualCallbacks > 0) framesAfterCallback++; };
            controller.HostAdvanced += _ => { if (visualCallbacks > 0) advancesAfterCallback++; };
            controller.FrameChanged += _ => {
                if (atomicCase != "frame-reenter" || controller.PhysicsWorld == null) return;
                visualCallbacks++;
                frameAtCallback = controller.Frame;
                controller.Poll();
            };
            controller.InitializeLocal(binding, mode, course, geometry, items, rules, Actor, prefabs,
                new WordboundGameplayOptions {
                    GameplayScene = SceneRef.FromIndex(sceneIndex), CheckpointMilliseconds = 500,
                    ReadMovement = () => movement,
                    ConfigureVisual = body => {
                        if (atomicCase != "stop" && atomicCase != "throw" && atomicCase != "reenter") return;
                        visualCallbacks++;
                        frameAtCallback = controller.Frame;
                        if (atomicCase == "throw") throw new FormatException("atomic native presentation failure");
                        if (atomicCase == "reenter") { controller.Poll(); return; }
                        _ = controller.Stop();
                    }
                });
            var config = new NetworkProjectConfig { PeerMode = NetworkProjectConfig.PeerModes.Multiple };
            foreach (var source in NetworkProjectConfig.Global.PrefabTable.Prefabs) config.PrefabTable.AddSource(source);
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
            yield return Wait(session.StartLocal(lifetime.Token), "Start local SDK runner", 30);
            if (atomicCase == "stop" || atomicCase == "throw" || atomicCase == "reenter" || atomicCase == "frame-reenter")
            {
                double deadline = Time.realtimeSinceStartupAsDouble + 30;
                while (visualCallbacks == 0 && Time.realtimeSinceStartupAsDouble < deadline && !lifetime.IsCancellationRequested)
                    yield return null;
                Assert.AreEqual(1, visualCallbacks, "The actual native callback must run once before aborting.");
                Assert.AreEqual(0, framesAfterCallback);
                Assert.AreEqual(0, advancesAfterCallback);
                Assert.AreSame(frameAtCallback, controller.Frame);
                Assert.IsTrue(controller.IsSimulationPaused);
                Assert.IsFalse(session.GameplayReady);
                string expectedFault = atomicCase == "throw" ? "FormatException: atomic native presentation failure" :
                    atomicCase == "reenter" || atomicCase == "frame-reenter" ? "InvalidOperationException: Canonical reconciliation cannot reenter an active attempt." : "";
                Assert.AreEqual(expectedFault, controller.Fault);
                yield break;
            }
            yield return Until(() => session.GameplayReady && controller.PhysicsWorld != null &&
                controller.Frame != null && !controller.IsSimulationPaused, "Build local canonical world", 30);
            Assert.AreEqual(GameMode.Single, session.Runner.GameMode);
            Assert.IsFalse(session.Runner.IsCloudReady, "The local runner must never connect to Photon Cloud.");
            Assert.AreEqual(1, session.Runner.ActivePlayers.Count());
            Assert.AreEqual(binding.Hash, controller.Frame.Binding.Hash);
            Assert.AreEqual(course.Hash, controller.CourseWorld.ManifestHash);
            Assert.AreNotEqual(UnityEngine.Physics.defaultPhysicsScene, controller.PhysicsWorld.PhysicsScene);
            CollectionAssert.AreEquivalent(controller.Frame.Bodies.Select(b => b.Id), controller.Bodies.Keys);

            if (atomicCase == "atomic")
            {
                CheckAtomicPhysics(rules);
                long tick = controller.Frame.Progress.Tick;
                yield return Until(() => controller.Frame.Progress.Tick > tick, "Resume normal host ticks after guarded applications", 10);
                var world = controller.PhysicsWorld;
                bool stopped = world.ReconcileAndApplySnapshot(controller.Frame, rules, () => { _ = controller.Stop(); return true; });
                Assert.IsFalse(stopped, "Disposal during preparation must abort the old world's apply.");
                Assert.IsFalse(world.IsHost);
                Assert.Throws<InvalidOperationException>(() => world.ApplySnapshot(controller.Frame));
                Assert.IsTrue(controller.IsSimulationPaused);
                Assert.AreEqual("", controller.Fault);
                yield break;
            }

            var initial = Player().Position;
            movement = new WordboundMovementInput { MoveZ = 1 };
            yield return Until(() => Player().Position.DistanceSquared(initial) > .25f, "Move using the actual local motor", 10);
            movement = default;
            long stableSince = -1;
            float stableHeight = 0;
            yield return Until(() => {
                var body = Player();
                if (Math.Abs(body.Velocity.Y) >= .1f || Math.Abs(body.Position.Y - stableHeight) > .03f) {
                    stableSince = controller.Frame.Progress.Tick; stableHeight = body.Position.Y; return false;
                }
                if (stableSince < 0) stableSince = controller.Frame.Progress.Tick;
                return controller.Frame.Progress.Tick - stableSince >= 20;
            }, "Settle on the actual native floor", 10);
            float floor = Player().Position.Y;
            movement = new WordboundMovementInput { Jump = true };
            yield return Until(() => Player().Position.Y > floor + .3f, "Jump through normal local input", 8);
            movement = default;
            yield return Until(() => Player().Position.Y < floor + .12f && Math.Abs(Player().Velocity.Y) < .1f,
                "Land through local physics", 10);
            var command = controller.Submit(WordboundAction.CancelCraft);
            yield return Until(() => controller.Frame.Commands.Frontiers[Actor] == command.Sequence,
                "Apply local command through the real journal", 10);
            var receipt = controller.Frame.Commands.Receipts.Single(r => r.ActorId == Actor && r.Sequence == command.Sequence);
            Assert.AreEqual(command.PayloadHash, receipt.PayloadHash);
            yield return Wait(session.PublishCheckpoint(), "Validate complete memory checkpoint", 5);
            Assert.GreaterOrEqual(session.Committed.Tick, receipt.Tick);
            var imported = MatchSnapshotCodec.Import(session.CommittedJson, binding, mode, course, items, rules);
            Assert.IsTrue(imported.Success, imported.Error);
            Assert.AreEqual(imported.Hash, session.Committed.StateHash);
            Assert.AreEqual(originalDraft, draft.RecoveryJson, "Playtest must not rewrite the authored document or seed.");
            Assert.IsFalse(session.Runner.IsCloudReady);
            Debug.Log("WORDBOUND_LOCAL_OBSERVED: exact workshop manifest, native bodies, local movement/jump and validated memory checkpoint; cleanup pending.");
        }

        void CheckAtomicPhysics(GameRules initialRules)
        {
            var world = controller.PhysicsWorld;
            var original = controller.Frame;
            var player = original.Bodies.Single(body => body.Kind == WorldBodyKind.Player);
            var native = controller.Bodies[player.Id];
            var prior = native.Canonical;
            var priorPosition = native.Rigidbody.position;
            var priorRotation = native.Rigidbody.rotation;
            var priorVelocity = native.Rigidbody.linearVelocity;
            void Unchanged()
            {
                Assert.AreSame(prior, native.Canonical);
                Assert.AreEqual(priorPosition, native.Rigidbody.position);
                Assert.AreEqual(priorRotation, native.Rigidbody.rotation);
                Assert.AreEqual(priorVelocity, native.Rigidbody.linearVelocity);
            }
            var moved = player.At(new WorldVector(player.Position.X + 1, player.Position.Y, player.Position.Z));
            MatchSnapshot Replace(System.Collections.Generic.IEnumerable<WorldBodyState> bodies) =>
                new MatchSnapshot(original.Binding, original.Epoch, original.Sequence, original.NextWorldEntityId,
                    original.Progress, original.Economy, original.Commands, original.Actors, bodies, original.Resources,
                    original.Mechanisms, original.RandomStreams, original.NarrativeFlags, original.ThrownFlights, original.Practice);
            var candidate = Replace(original.Bodies.Select(body => body.Id == player.Id ? moved : body));
            var invalid = Replace(original.Bodies.Where(body => body.Id != player.Id));
            Assert.Throws<ArgumentException>(() => world.ApplySnapshot(invalid));
            bool prepared = false;
            Assert.Throws<ArgumentException>(() => world.ReconcileAndApplySnapshot(invalid, initialRules, () => { prepared = true; return true; }));
            Assert.IsFalse(prepared);
            Unchanged();
            Assert.IsFalse(world.ReconcileAndApplySnapshot(candidate, initialRules, () => false));
            Unchanged();

            var callbackError = new FormatException("native prepare failure");
            Assert.AreSame(callbackError, Assert.Throws<FormatException>(() =>
                world.ReconcileAndApplySnapshot(candidate, initialRules, () => throw callbackError)));
            Unchanged();
            Assert.IsFalse(world.ReconcileAndApplySnapshot(candidate, initialRules, () => {
                Assert.Throws<InvalidOperationException>(() => world.ApplySnapshot(candidate));
                Assert.Throws<InvalidOperationException>(() => world.ReconcileAndApplySnapshot(candidate, initialRules, () => true));
                return false;
            }));
            Unchanged();

            float health = world.Rules.MaxHealth;
            try
            {
                Assert.Throws<ArgumentException>(() => world.ReconcileAndApplySnapshot(candidate, initialRules, () => {
                    world.Rules.MaxHealth = 1;
                    return true;
                }));
                Unchanged();
                Assert.Throws<ArgumentException>(() => world.ApplySnapshot(candidate));
                Unchanged();
            }
            finally { world.Rules.MaxHealth = health; }
            Assert.IsTrue(world.ReconcileAndApplySnapshot(candidate, initialRules, () => true));
            Assert.AreSame(moved, native.Canonical);
            Assert.AreEqual(new Vector3(moved.Position.X, moved.Position.Y, moved.Position.Z), native.Rigidbody.position);
            world.ApplySnapshot(original);
            Assert.AreSame(player, native.Canonical);
            Assert.AreEqual(new Vector3(player.Position.X, player.Position.Y, player.Position.Z), native.Rigidbody.position);
            Debug.Log("WORDBOUND_ATOMIC_APPLY: actual local runner; direct validation, initial rejection, abort, callback exception, reentry, distinct live rules and later successful apply observed.");
        }

        WorldBodyState Player() => controller.Frame.Bodies.Single(b => b.Kind == WorldBodyKind.Player && b.SourceId == Actor);
        void Healthy()
        {
            Assert.AreEqual("", session?.Failure ?? "");
            Assert.AreEqual("", controller?.Fault ?? "");
        }
        IEnumerator Until(Func<bool> complete, string operation, double seconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!complete() && Time.realtimeSinceStartupAsDouble < deadline && !lifetime.IsCancellationRequested)
            { Healthy(); yield return null; }
            Healthy();
            Assert.IsTrue(complete(), operation + " exceeded its deadline.");
        }
        IEnumerator Wait(Task task, string operation, double seconds)
        {
            pending = task;
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline && !lifetime.IsCancellationRequested)
            { Healthy(); yield return null; }
            Assert.IsTrue(task.IsCompleted, operation + " exceeded its deadline.");
            Assert.IsFalse(task.IsCanceled, operation + " was cancelled.");
            if (task.IsFaulted) Assert.Fail(operation + " failed: " + task.Exception.GetBaseException());
            pending = null;
        }
        static NetworkObject Load(string kind)
        {
            string path = "Assets/_Project/Prefabs/Wordbound/Network/Wordbound" + kind + "Body.prefab";
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(root, "Create the native body prefab before the local proof: " + path);
            return root.GetComponent<NetworkObject>();
        }
        static int PrepareScene()
        {
            Assert.IsTrue(File.Exists(ProofScene));
            Assert.AreEqual("", SessionState.GetString(PriorScenesKey, ""));
            var previous = EditorBuildSettings.scenes;
            SessionState.SetString(PriorScenesKey, JsonUtility.ToJson(new SavedScenes {
                paths = previous.Select(s => s.path).ToArray(), enabled = previous.Select(s => s.enabled).ToArray()
            }));
            var scenes = previous.Where(s => s.path != ProofScene).Concat(new[] { new EditorBuildSettingsScene(ProofScene, true) }).ToArray();
            EditorBuildSettings.scenes = scenes;
            return scenes.Count(s => s.enabled) - 1;
        }
        [Serializable] sealed class SavedScenes { public string[] paths; public bool[] enabled; }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            lifetime?.Cancel();
            int failures = 0;
            Task stop = null;
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
            controller = null; session = null; pending = null; movement = default;
            lifetime?.Dispose(); lifetime = null;
            if (Application.isPlaying && SessionState.GetBool(PlayStartedKey, false)) yield return new ExitPlayMode();
            SessionState.EraseBool(PlayStartedKey);
            string prior = SessionState.GetString(PriorScenesKey, "");
            if (prior != "")
            {
                var saved = JsonUtility.FromJson<SavedScenes>(prior);
                EditorBuildSettings.scenes = saved.paths.Select((path, i) => new EditorBuildSettingsScene(path, saved.enabled[i])).ToArray();
                SessionState.EraseString(PriorScenesKey);
            }
            if (draft != null) Assert.AreEqual(originalDraft, draft.RecoveryJson);
            draft = null; originalDraft = null;
            Assert.AreEqual(0, failures);
            Assert.IsFalse(leaked);
        }
    }
}
