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
using Wreckabulary.WordboundRuntime;
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

        [UnityTest, Category("LocalWordboundGameplay")]
        public IEnumerator RunningFrameStopPreventsCheckpointAndLocalViewContinuation() => Run("Freestyle", "running-frame-stop");

        [UnityTest, Category("LocalWordboundGameplay")]
        public IEnumerator AtomicCollisionRegistrationKeepsSharedNativePairsAndAbortRecovery() => Run("Freestyle", "collision-pairs");

        [UnityTest, Category("LocalWordboundGameplay")]
        public IEnumerator AtomicCollisionRegistrationKeepsIsolatedNativePairsAndAbortRecovery() => Run("TimeRun", "collision-pairs");

        [UnityTest, Category("LocalWordboundGameplay")]
        public IEnumerator PresentationTupleReusePreservesImmediateShapeCallbacksAndVisibility() => Run("Freestyle", "presentation-cache");

        [UnityTest, Category("LocalWordboundGameplay")]
        public IEnumerator InitialFreestyleKeepsakeHasNativeFloorSupportAndClearanceBeforeSimulation() => Run("Freestyle", "initial-keepsake");

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
            int activeViewsAfterCallback = 0, clockReadsAfterCallback = 0;
            MatchSnapshot frameAtCallback = null;
            bool initialKeepsakeObserved = false;
            Exception initialKeepsakeError = null;
            controller.FrameChanged += snapshot => {
                if (atomicCase != "initial-keepsake" || initialKeepsakeObserved || controller.PhysicsWorld == null) return;
                initialKeepsakeObserved = true;
                try { CheckInitialKeepsake(snapshot); }
                catch (Exception error) { initialKeepsakeError = error; }
            };
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
                    Clock = () => {
                        if (visualCallbacks > 0) clockReadsAfterCallback++;
                        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    },
                    ReadMovement = () => movement,
                    LocalPlayerChanged = body => { if (visualCallbacks > 0 && body != null) activeViewsAfterCallback++; },
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
                Assert.AreEqual(0, activeViewsAfterCallback);
                Assert.AreEqual(0, clockReadsAfterCallback);
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

            if (atomicCase == "initial-keepsake")
            {
                Assert.IsTrue(initialKeepsakeObserved, "Observe the first applied frame before native settling can conceal initial overlap.");
                Assert.IsNull(initialKeepsakeError, initialKeepsakeError?.ToString());
                long tick = controller.Frame.Progress.Tick;
                yield return Until(() => controller.Frame.Progress.Tick > tick, "Advance the unchanged local runner after initial geometry checks", 10);
                Assert.AreEqual("", controller.Fault);
                yield break;
            }

            if (atomicCase == "presentation-cache")
            {
                CheckPresentationTupleReuse(mode, course, geometry, items, rules, prefabs);
                long tick = controller.Frame.Progress.Tick;
                yield return Until(() => controller.Frame.Progress.Tick > tick, "Resume after isolated presentation checks", 10);
                Assert.AreEqual("", controller.Fault);
                yield break;
            }

            if (atomicCase == "collision-pairs")
            {
                CheckCollisionRegistrations(mode, course, items, rules, prefabs);
                long tick = controller.Frame.Progress.Tick;
                yield return Until(() => controller.Frame.Progress.Tick > tick, "Resume the original world after isolated pair checks", 10);
                Assert.AreEqual("", controller.Fault);
                yield break;
            }

            if (atomicCase == "running-frame-stop")
            {
                var committed = session.Committed;
                controller.FrameChanged += snapshot => {
                    visualCallbacks++;
                    frameAtCallback = snapshot;
                    _ = controller.Stop();
                };
                Assert.IsTrue(controller.Authority.Match.Disconnect(Actor, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
                    "An actual canonical presence change must make Poll reconcile a new frame.");
                controller.Poll();
                yield return null;
                controller.Poll();
                Assert.AreEqual(1, visualCallbacks);
                Assert.AreSame(frameAtCallback, controller.Frame);
                Assert.AreSame(committed, session.Committed, "Stopping publication cannot create another durable checkpoint.");
                Assert.AreEqual(0, framesAfterCallback);
                Assert.AreEqual(0, advancesAfterCallback);
                Assert.AreEqual(0, activeViewsAfterCallback, "The required null cleanup notification is allowed; a new active view is not.");
                Assert.AreEqual(0, clockReadsAfterCallback, "Poll must not reach checkpoint scheduling after the stop callback.");
                Assert.IsTrue(controller.IsSimulationPaused);
                Assert.IsFalse(session.GameplayReady);
                Assert.AreEqual("", controller.Fault);
                yield break;
            }

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

        void CheckInitialKeepsake(MatchSnapshot snapshot)
        {
            Assert.AreEqual(0, snapshot.Progress.Tick);
            Assert.IsTrue(controller.IsSimulationPaused, "Check the first applied native frame before host simulation is enabled.");
            UnityEngine.Physics.SyncTransforms();
            var canonical = snapshot.Bodies.Single(b => b.Id == "keepsake:0");
            var keepsake = controller.Bodies[canonical.Id];
            var box = keepsake.GetComponent<BoxCollider>();
            Assert.IsNotNull(box); Assert.IsTrue(box.enabled); Assert.IsFalse(box.isTrigger);
            Assert.AreSame(keepsake.Rigidbody, box.attachedRigidbody);
            Assert.AreEqual(new Vector3(.45f, .45f, .45f), box.size);
            Assert.AreEqual(new Vector3(0, .225f, 0), box.center);
            Assert.AreEqual(Vector3.one, box.transform.lossyScale);
            Assert.AreEqual(new Vector3(canonical.Position.X, canonical.Position.Y, canonical.Position.Z), keepsake.Rigidbody.position);
            Assert.IsTrue(canonical.Active); Assert.IsFalse(canonical.Kinematic);
            var start = controller.CourseWorld.Course.Spawns.First(s => s.Kind == "Start");
            var anchor = controller.CourseWorld.Course.RecoveryAnchors.Single(a => a.Id == "StartSafe");
            Assert.AreEqual(start.ModuleId, anchor.ModuleId);
            Assert.AreEqual(controller.CourseWorld.ToWorld(anchor.Position), keepsake.Rigidbody.position);
            foreach (var player in controller.Bodies.Values.Where(b => b.Canonical.Kind == WorldBodyKind.Player))
            {
                var capsule = player.GetComponent<CapsuleCollider>();
                Assert.IsNotNull(capsule); Assert.AreEqual(.3f, capsule.radius); Assert.AreEqual(1.8f, capsule.height);
                Assert.AreEqual(new Vector3(0, .9f, 0), capsule.center); Assert.AreEqual(1, capsule.direction);
                Assert.AreEqual(Vector3.one, capsule.transform.lossyScale);
                Assert.IsFalse(UnityEngine.Physics.GetIgnoreCollision(box, capsule), "Clearance must preserve the actual player/keepsake collision policy.");
                Assert.IsTrue(UnityEngine.Physics.ComputePenetration(box, player.Rigidbody.position, keepsake.Rigidbody.rotation,
                    capsule, player.Rigidbody.position, player.Rigidbody.rotation, out _, out float oldOverlap));
                Assert.Greater(oldOverlap, 0, "The old shared-root placement really overlaps these loaded native shapes.");
            }
            foreach (var other in controller.Bodies.Values.SelectMany(b => b.OwnedColliders).Concat(controller.CourseWorld.Surfaces))
            {
                if (other == box || !other.enabled || other.isTrigger) continue;
                bool overlap = UnityEngine.Physics.ComputePenetration(box, keepsake.Rigidbody.position, keepsake.Rigidbody.rotation,
                    other, other.transform.position, other.transform.rotation, out _, out float depth);
                Assert.IsFalse(overlap && depth > .0001f, "Initial keepsake penetrates an actual body or authored surface.");
            }
            var floors = controller.CourseWorld.Surfaces.Where(c => c.GetComponent<WordboundCourseNode>()?.Kind == WordboundCourseNodeKind.Floor).ToArray();
            Assert.IsNotEmpty(floors);
            foreach (var offset in new[] { Vector3.zero, new Vector3(-.225f, 0, -.225f), new Vector3(-.225f, 0, .225f),
                new Vector3(.225f, 0, -.225f), new Vector3(.225f, 0, .225f) })
            {
                Vector3 bottom = keepsake.Rigidbody.position + keepsake.Rigidbody.rotation * offset;
                var ray = new Ray(bottom + Vector3.up * .05f, Vector3.down);
                Assert.IsTrue(floors.Any(floor => floor.Raycast(ray, out var hit, .1f) && Math.Abs(hit.point.y - bottom.y) <= .001f),
                    "The centre and every actual box corner require native authored floor support.");
            }
            Debug.Log("WORDBOUND_INITIAL_KEEPSAKE: firstTick=0; nativeShapeParity=true; oldRootOverlap=true; currentOverlap=false; floorSupportSamples=5.");
        }

        void CheckPresentationTupleReuse(ModeDefinition mode, ResolvedCourse course, CourseCatalogue geometry,
            ItemCatalogue items, GameRules rules, WordboundGameplayPrefabs prefabs)
        {
            var snapshot = controller.Frame;
            var runner = session.Runner;
            var created = new List<NetworkObject>();
            var world = new WordboundPhysicsWorld(runner, snapshot.Binding, mode, course, items, rules,
                peer => peer == runner.LocalPlayer ? Actor : null, () => true);
            WordboundGameplayBody observed = null;
            Action<WordboundGameplayBody> callback = null;
            WordboundGameplayBody Add(WorldBodyState state)
            {
                PlayerRef peer = state.Kind == WorldBodyKind.Player ? runner.LocalPlayer : PlayerRef.None;
                var obj = runner.Spawn(prefabs.For(state.Kind), new Vector3(state.Position.X, state.Position.Y, state.Position.Z),
                    new Quaternion(state.RotationXyz.X, state.RotationXyz.Y, state.RotationXyz.Z, state.RotationW),
                    peer == PlayerRef.None ? (PlayerRef?)null : peer);
                Assert.IsNotNull(obj); created.Add(obj);
                var body = obj.GetComponent<WordboundNetworkBody>();
                world.Register(body, state, peer);
                var view = obj.GetComponent<WordboundGameplayBody>();
                Assert.IsNotNull(view);
                Assert.AreEqual(0, view.SuccessfulPresentationValidations, "An unconfigured spawn must not reuse a prior validation.");
                return view;
            }
            try
            {
                var context = new WordboundPresentationContext(snapshot, course, geometry, items);
                var item = snapshot.Bodies.First(body => body.Kind == WorldBodyKind.Item && body.Active);
                observed = Add(item);
                var original = context.Presentation(item);
                var expected = original;
                Assert.IsNotNull(observed.VisualRoot);
                var rendered = new GameObject("Presentation cache renderer").AddComponent<MeshRenderer>();
                rendered.transform.SetParent(observed.VisualRoot, false);
                rendered.enabled = false;
                int callbacks = 0;
                bool throwOnce = false, reenterOnce = false;
                var failure = new FormatException("presentation callback sentinel");
                callback = view => {
                    callbacks++;
                    Assert.AreSame(observed, view);
                    Assert.AreEqual(expected.VisualKey, view.VisualKey.ToString());
                    Assert.AreEqual(expected.CarrierSeat, view.CarrierSeat);
                    Assert.AreEqual(new Vector3(expected.Size.X, expected.Size.Y, expected.Size.Z), view.GetComponent<BoxCollider>().size);
                    if (callbacks == 1) Assert.IsFalse(rendered.enabled, "Shape and callback precede the visibility loop.");
                    if (throwOnce) { throwOnce = false; throw failure; }
                    if (reenterOnce) { reenterOnce = false; view.Configure(expected); }
                };
                observed.PresentationChanged += callback;
                observed.Configure(expected);
                Assert.AreEqual(1, observed.SuccessfulPresentationValidations);
                Assert.AreEqual(1, callbacks); Assert.IsTrue(rendered.enabled);
                for (int i = 0; i < 4; i++)
                {
                    observed.Configure(expected); observed.FixedUpdateNetwork();
                    rendered.enabled = false; observed.Render();
                    Assert.IsTrue(rendered.enabled, "An unchanged tuple must still restore current visibility.");
                }
                Assert.AreEqual(1, observed.SuccessfulPresentationValidations); Assert.AreEqual(1, callbacks);
                var nested = new GameObject("Late presentation renderer").AddComponent<MeshRenderer>();
                nested.transform.SetParent(rendered.transform, false); nested.enabled = false;
                observed.Render(); Assert.IsTrue(nested.enabled, "Nested replacement discovery remains live.");
                observed.Body.ApplyCanonical(item.At(item.Position, active: false)); observed.Render();
                Assert.IsFalse(rendered.enabled); Assert.IsFalse(nested.enabled);
                observed.Body.ApplyCanonical(item); observed.Render();
                Assert.IsTrue(rendered.enabled); Assert.IsTrue(nested.enabled);
                Assert.AreEqual(1, observed.SuccessfulPresentationValidations);

                expected = new WordboundBodyPresentation(original.Size, original.VisualKey, 0);
                observed.Configure(expected);
                Assert.AreEqual(2, observed.SuccessfulPresentationValidations); Assert.AreEqual(2, callbacks);
                float nextX = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(original.Size.X) + 1);
                expected = new WordboundBodyPresentation(new WorldVector(nextX, original.Size.Y, original.Size.Z), original.VisualKey, 0);
                observed.Configure(expected);
                Assert.AreEqual(nextX, observed.GetComponent<BoxCollider>().size.x);
                Assert.AreEqual(3, observed.SuccessfulPresentationValidations); Assert.AreEqual(2, callbacks);
                expected = new WordboundBodyPresentation(expected.Size, "letter:Q", 0);
                observed.Configure(expected);
                Assert.AreEqual(4, observed.SuccessfulPresentationValidations); Assert.AreEqual(3, callbacks);
                expected = original; observed.Configure(expected);
                Assert.AreEqual(5, observed.SuccessfulPresentationValidations); Assert.AreEqual(4, callbacks);

                expected = new WordboundBodyPresentation(original.Size, original.VisualKey, 1);
                throwOnce = true; rendered.enabled = false;
                Assert.AreSame(failure, Assert.Throws<FormatException>(() => observed.Configure(expected)));
                Assert.IsFalse(rendered.enabled, "Callback exceptions must still stop the later visibility loop.");
                Assert.AreEqual(6, observed.SuccessfulPresentationValidations); Assert.AreEqual(5, callbacks);
                observed.Configure(expected);
                Assert.IsTrue(rendered.enabled);
                Assert.AreEqual(6, observed.SuccessfulPresentationValidations); Assert.AreEqual(5, callbacks);
                expected = new WordboundBodyPresentation(original.Size, original.VisualKey, 2);
                reenterOnce = true; observed.Configure(expected);
                Assert.AreEqual(7, observed.SuccessfulPresentationValidations); Assert.AreEqual(6, callbacks);

                var player = Add(snapshot.Bodies.Single(body => body.Kind == WorldBodyKind.Player));
                var playerPresentation = new WordboundBodyPresentation(new WorldVector(.6f, 1.8f, .6f), "player");
                player.Configure(playerPresentation);
                var badCapsule = new WordboundBodyPresentation(new WorldVector(.7f, 1.8f, .6f), "player");
                const string shapeError = "Player capsule differs from its canonical shape.";
                Assert.AreEqual(shapeError, Assert.Throws<InvalidOperationException>(() => player.Configure(badCapsule)).Message);
                Assert.AreEqual(shapeError, Assert.Throws<InvalidOperationException>(() => player.Configure(badCapsule)).Message);
                Assert.AreEqual(2, player.SuccessfulPresentationValidations, "Pure tuple validity must not cache collider success.");
                player.Configure(playerPresentation);
                Assert.AreEqual(3, player.SuccessfulPresentationValidations);
                Assert.AreEqual(.3f, player.GetComponent<CapsuleCollider>().radius);

                observed.PresentationChanged -= callback;
                var retired = observed.Object; runner.Despawn(retired); created.Remove(retired); observed = null;
                var replacement = Add(item); replacement.Configure(original);
                Assert.AreEqual(1, replacement.SuccessfulPresentationValidations);
                Debug.Log("WORDBOUND_PRESENTATION_TUPLE: actual isolated spawned bodies; exact tuple reuse, immediate shape/callback, exception/reentry, nested visibility and new spawn observed; no allocation/timing claim.");
            }
            finally
            {
                if (observed != null && callback != null) observed.PresentationChanged -= callback;
                world.Dispose();
                foreach (var obj in created) if (obj != null && obj.IsValid) runner.Despawn(obj);
            }
        }

        void CheckCollisionRegistrations(ModeDefinition mode, ResolvedCourse course, ItemCatalogue items,
            GameRules rules, WordboundGameplayPrefabs prefabs)
        {
            var snapshot = controller.Frame;
            var runner = session.Runner;
            var states = snapshot.Bodies.OrderBy(body => body.Kind == WorldBodyKind.Player).ToArray();
            Assert.GreaterOrEqual(states.Count(state => state.Kind != WorldBodyKind.Player), 2,
                "The actual local course must provide multiple native non-player bodies.");
            for (int mutationCase = 0; mutationCase < 2; mutationCase++)
            {
                var registered = new Dictionary<string, WordboundNetworkBody>(StringComparer.Ordinal);
                var created = new List<NetworkObject>();
                var world = new WordboundPhysicsWorld(runner, snapshot.Binding, mode, course, items, rules,
                    peer => peer == runner.LocalPlayer ? Actor : null, () => true);
                world.HostStepCompleted += _ => { };
                world.RegisterStatic(controller.CourseWorld);
                PlayerRef Authority(WorldBodyState state) => state.Kind == WorldBodyKind.Player ? runner.LocalPlayer : PlayerRef.None;
                WordboundNetworkBody Add(WorldBodyState state)
                {
                    var obj = runner.Spawn(prefabs.For(state.Kind), new Vector3(state.Position.X, state.Position.Y, state.Position.Z),
                        Quaternion.identity, state.Kind == WorldBodyKind.Player ? (PlayerRef?)runner.LocalPlayer : null);
                    Assert.IsNotNull(obj);
                    created.Add(obj);
                    var body = obj.GetComponent<WordboundNetworkBody>();
                    world.Register(body, state, Authority(state));
                    registered.Add(state.Id, body);
                    CheckPairs();
                    return body;
                }
                void Remove(string id, bool checkRebind = false)
                {
                    var body = registered[id];
                    world.Unregister(body);
                    world.Unregister(body);
                    if (checkRebind)
                        Assert.Throws<InvalidOperationException>(() => world.Register(body, body.Canonical, Authority(body.Canonical)));
                    registered.Remove(id);
                    created.Remove(body.Object);
                    runner.Despawn(body.Object);
                }
                void CheckPairs()
                {
                    var all = registered.Values.ToArray();
                    for (int i = 0; i < all.Length; i++)
                        for (int j = i + 1; j < all.Length; j++)
                            foreach (var first in all[i].OwnedColliders)
                                foreach (var second in all[j].OwnedColliders)
                                    Assert.AreEqual(!WordboundPhysicsPolicy.ShouldCollide(mode, all[i].Canonical, all[j].Canonical),
                                        UnityEngine.Physics.GetIgnoreCollision(first, second),
                                        "Native pair permissions must already match when Register returns to a callback.");
                }
                try
                {
                    Assert.IsTrue(world.ReconcileAndApplySnapshot(snapshot, rules, () => {
                        for (int i = 0; i < states.Length; i++)
                        {
                            Add(states[i]);
                            if (mutationCase == 1 && i == 1)
                            {
                                for (int change = 0; change < 2; change++)
                                {
                                    var state = states[change];
                                    var changed = new WorldBodyState(state.Id, state.Kind, state.Position,
                                        state.RotationXyz, state.RotationW, state.Velocity, state.AngularVelocity,
                                        state.Sleeping, state.Kinematic, state.Active, change, true, state.SourceId, state.RecoveryGeneration);
                                    registered[state.Id].ApplyCanonical(changed);
                                }
                                Assert.IsFalse(WordboundPhysicsPolicy.ShouldCollide(mode,
                                    registered[states[0].Id].Canonical, registered[states[1].Id].Canonical));
                            }
                            if (mutationCase == 0) Assert.AreEqual(0, world.CollisionPairFullPasses);
                            if (mutationCase == 1 && i == 2) Assert.AreEqual(1, world.CollisionPairFullPasses,
                                "A changed immutable canonical reference must invalidate the incremental baseline.");
                        }
                        Assert.IsTrue(world.ShouldFreeze, "A partially composed world cannot simulate before validated apply.");
                        Assert.Throws<ArgumentException>(() => world.Register(registered[states[0].Id], states[0], Authority(states[0])));
                        return true;
                    }));
                    Assert.IsFalse(world.ShouldFreeze);
                    CheckPairs();
                    if (mutationCase == 0)
                    {
                        Assert.AreEqual(states.Length, world.CollisionPairIncrementalPasses);
                        Assert.AreEqual(1, world.CollisionPairFullPasses);
                        Assert.AreEqual((long)states.Length * (states.Length - 1), world.CollisionPairDecisions,
                            "Each pair is visited once as its body is added and once in final apply.");
                    }
                    else Assert.AreEqual(2, world.CollisionPairFullPasses);

                    long passes = world.CollisionPairFullPasses;
                    Assert.IsFalse(world.ReconcileAndApplySnapshot(snapshot, rules, () => false));
                    Assert.IsFalse(world.ShouldFreeze, "An abort without registry mutation retains resumability.");
                    Assert.AreEqual(passes, world.CollisionPairFullPasses);

                    var replacement = states[0];
                    Remove(replacement.Id, true);
                    passes = world.CollisionPairFullPasses;
                    Add(replacement);
                    Assert.AreEqual(passes + 1, world.CollisionPairFullPasses, "Standalone Register still refreshes immediately.");
                    Assert.IsFalse(world.ShouldFreeze);

                    foreach (bool throws in new[] { false, true })
                    {
                        var expected = new FormatException("collision registration callback failed");
                        bool Prepare()
                        {
                            Remove(replacement.Id, true);
                            Assert.IsTrue(world.ShouldFreeze);
                            Assert.Throws<InvalidOperationException>(() => world.ApplySnapshot(snapshot));
                            Assert.Throws<InvalidOperationException>(() => world.ReconcileAndApplySnapshot(snapshot, rules, () => true));
                            if (throws) throw expected;
                            return false;
                        }
                        if (throws) Assert.AreSame(expected, Assert.Throws<FormatException>(() =>
                            world.ReconcileAndApplySnapshot(snapshot, rules, Prepare)));
                        else Assert.IsFalse(world.ReconcileAndApplySnapshot(snapshot, rules, Prepare));
                        Assert.IsTrue(world.ShouldFreeze, "Partial abort/throw remains frozen after returning to its caller.");
                        CheckPairs();
                        Assert.Throws<ArgumentException>(() => world.ApplySnapshot(snapshot),
                            "A still-incomplete registry cannot clear the pending composition freeze.");
                        Assert.IsTrue(world.ShouldFreeze);
                        Assert.IsTrue(world.ReconcileAndApplySnapshot(snapshot, rules, () => { Add(replacement); return true; }));
                        Assert.IsFalse(world.ShouldFreeze, "Successful retry restores a usable world without a permanent fault.");
                        CheckPairs();
                    }
                    Debug.Log($"WORDBOUND_COLLISION_PAIRS: mode={mode.Id}; mutationCase={mutationCase}; bodies={states.Length}; fullPasses={world.CollisionPairFullPasses}; incrementalPasses={world.CollisionPairIncrementalPasses}; pairDecisions={world.CollisionPairDecisions}; nativePairs=verified; noTimingClaim=true");
                }
                finally
                {
                    world.Dispose();
                    Assert.IsTrue(world.ShouldFreeze);
                    foreach (var obj in created) if (obj != null) runner.Despawn(obj);
                }
            }
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
