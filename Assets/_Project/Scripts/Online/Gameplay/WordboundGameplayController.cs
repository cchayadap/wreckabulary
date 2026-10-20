using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using Fusion.Addons.Physics;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;

namespace Wreckabulary.Online
{
    [DisallowMultipleComponent]
    public sealed class WordboundGameplayController : MonoBehaviour, INetworkRunnerCallbacks
    {
        sealed class QueryRelay : IWordboundWorldQuery, IWordboundReleaseQuery
        {
            public IWordboundWorldQuery Target;
            public bool HasLineOfSight(string actor, WorldVector from, WorldVector to) => Target?.HasLineOfSight(actor, from, to) ?? false;
            public bool CanDeploy(string actor, string recipe, WorldVector at) => Target?.CanDeploy(actor, recipe, at) ?? false;
            public bool PuzzleSatisfied(string actor, ResolvedCoursePuzzle puzzle, WorldBodyState item) =>
                Target?.PuzzleSatisfied(actor, puzzle, item) ?? false;
            public bool TryRelease(WorldBodyState actor, WorldBodyState item, bool throwing, out WorldVector at)
            {
                at = default;
                return Target is IWordboundReleaseQuery release && release.TryRelease(actor, item, throwing, out at);
            }
        }
        readonly Dictionary<string, WordboundNetworkBody> bodies = new Dictionary<string, WordboundNetworkBody>(StringComparer.Ordinal);
        readonly Dictionary<string, WordboundGameplayBody> presentations = new Dictionary<string, WordboundGameplayBody>(StringComparer.Ordinal);
        readonly QueryRelay query = new QueryRelay();
        IWordboundGameplaySession session;
        NetworkRunner runner;
        ResolvedCourse course;
        CourseCatalogue geometry;
        ItemCatalogue items;
        GameRules rules;
        WordboundGameplayPrefabs prefabs;
        WordboundGameplayOptions options;
        WordboundCourseWorld courseWorld;
        WordboundPhysicsWorld physics;
        WordboundGameplayDriver driver;
        MatchSnapshot frame;
        WordboundNetworkBody localBody;
        string localActor;
        long runnerStarted, nextCheckpoint, lastSequence, sentEpoch = -1;
        bool initialized, localReady, hostReady, publishing, stopping, handlingStep;
        Task stopTask;
        public WordboundFusionAuthority Authority { get; private set; }
        public MatchSnapshot Frame => frame;
        public WordboundCourseWorld CourseWorld => courseWorld;
        public WordboundPhysicsWorld PhysicsWorld => physics;
        public IReadOnlyDictionary<string, WordboundNetworkBody> Bodies => bodies;
        public bool IsSimulationPaused => !initialized || Fault != "" || stopping ||
            WordboundGameplayPlan.IsPaused(localReady, session?.GameplayReady ?? false, runner != null && runner.IsServer,
                hostReady, Authority?.Match.IsPaused ?? true, frame);
        public string Fault { get; private set; } = "";
        public event Action<string> Faulted;
        public event Action<MatchSnapshot> FrameChanged;
        public event Action<MatchTickResult> HostAdvanced;

        public void Initialize(VerifiedOnlineMatch online, ModeDefinition mode, ResolvedCourse resolved,
            CourseCatalogue courseCatalogue, ItemCatalogue itemCatalogue, GameRules gameRules, string localPlayerId,
            WordboundGameplayPrefabs bodyPrefabs, WordboundGameplayOptions gameplayOptions = null)
        {
            if (online == null) throw new ArgumentNullException(nameof(online));
            var expected = online.Match;
            var binding = new MatchBinding(expected.MatchId, expected.Settings.ProtocolVersion, expected.Content.ModeHash,
                expected.Content.CourseHash, expected.Content.ManifestHash, expected.RosterHash, expected.Content.CatalogHash,
                online.Roster.Select(s => new WordboundSeat(s.PlayerId, s.TeamId, s.Slot)));
            InitializeCore(binding, mode, resolved, courseCatalogue, itemCatalogue, gameRules, localPlayerId, bodyPrefabs, gameplayOptions,
                match => new WordboundFusionAuthority(match, online, resolved, itemCatalogue, gameRules));
        }

        public void InitializeLocal(MatchBinding binding, ModeDefinition mode, ResolvedCourse resolved,
            CourseCatalogue courseCatalogue, ItemCatalogue itemCatalogue, GameRules gameRules, string localPlayerId,
            WordboundGameplayPrefabs bodyPrefabs, WordboundGameplayOptions gameplayOptions = null)
        {
            InitializeCore(binding, mode, resolved, courseCatalogue, itemCatalogue, gameRules, localPlayerId, bodyPrefabs, gameplayOptions,
                match => WordboundFusionAuthority.CreateLocal(match, resolved, itemCatalogue, gameRules, localPlayerId));
        }

        void InitializeCore(MatchBinding binding, ModeDefinition mode, ResolvedCourse resolved,
            CourseCatalogue courseCatalogue, ItemCatalogue itemCatalogue, GameRules gameRules, string localPlayerId,
            WordboundGameplayPrefabs bodyPrefabs, WordboundGameplayOptions gameplayOptions,
            Func<AuthoritativeMatch, WordboundFusionAuthority> createAuthority)
        {
            if (initialized) throw new InvalidOperationException("Gameplay controller is already initialized.");
            options = gameplayOptions ?? new WordboundGameplayOptions();
            if (options.Clock == null || options.CheckpointMilliseconds < 250 || options.CheckpointMilliseconds > 1000)
                throw new ArgumentException("Proof checkpoint cadence must be 250–1000 ms.");
            if (options.GameplayScene.HasValue && !options.GameplayScene.Value.IsValid)
                throw new ArgumentException("Gameplay scene must be a registered Fusion scene.");
            course = resolved ?? throw new ArgumentNullException(nameof(resolved));
            geometry = courseCatalogue ?? throw new ArgumentNullException(nameof(courseCatalogue));
            items = itemCatalogue ?? throw new ArgumentNullException(nameof(itemCatalogue));
            rules = gameRules?.Clone() ?? throw new ArgumentNullException(nameof(gameRules));
            prefabs = bodyPrefabs ?? throw new ArgumentNullException(nameof(bodyPrefabs));
            localActor = localPlayerId;
            WordboundGameplayPlan.Seat(binding, localActor);
            var match = new AuthoritativeMatch(binding, mode, course, geometry, items, rules, query, options.Clock());
            Authority = createAuthority(match);
            initialized = true;
        }

        public void AttachSession(IWordboundGameplaySession fusionSession)
        {
            if (!initialized || session != null || fusionSession == null) throw new InvalidOperationException("Initialize then attach one session.");
            if (options.GameplayScene.HasValue) fusionSession.ConfigureScene(options.GameplayScene.Value);
            session = fusionSession;
            session.GameplayStarted += GameplayStarted;
            session.ReplicaReceived += ReceiveReplica;
            session.Failed += SessionFailed;
        }

        void Update() => Poll();
        public void Poll()
        {
            if (!initialized || session == null || stopping || Fault != "") return;
            try
            {
                session.Poll();
                if (!ReferenceEquals(runner, session.Runner)) ChangeRunner(session.Runner);
                if (runner == null || !runner.IsRunning || !session.RunnerStarted) return;
                if (!localReady && !PrepareScene())
                {
                    if (options.Clock() - runnerStarted >= 30000) throw new TimeoutException("The runner-local gameplay scene did not become ready.");
                    return;
                }
                if (session.GameplayReady && runner.IsServer && !hostReady) BeginHost();
                if (hostReady && runner.IsServer)
                {
                    var current = Authority.Match.Capture(1);
                    if (!SameFrame(frame, current)) Reconcile(current);
                    if (!publishing && options.Clock() >= nextCheckpoint) _ = PublishCheckpoint();
                }
                UpdateClientBodies();
            }
            catch (Exception error) { Fail(error); }
        }

        void ChangeRunner(NetworkRunner next)
        {
            CleanupRunner();
            runner = next;
            runnerStarted = -1; localReady = false; hostReady = false;
            frame = null; publishing = false; lastSequence = 0; sentEpoch = -1;
            if (runner == null) return;
            runner.ProvideInput = true;
            runner.AddCallbacks(this);
        }

        bool PrepareScene()
        {
            if (runnerStarted < 0) runnerStarted = options.Clock();
            if (Math.Abs(runner.DeltaTime - 1f / ModeDefinition.TickRate) > .000001f)
                throw new InvalidOperationException("Wordbound requires an actual 60 Hz Fusion simulation.");
            if (options.GameplayScene.HasValue)
            {
                bool loaded = runner.TryGetSceneInfo(out var info) &&
                    Enumerable.Range(0, info.SceneCount).Any(i => info.Scenes[i] == options.GameplayScene.Value);
                if (!loaded) return false;
            }
            if (runner.SceneManager == null || runner.SceneManager.IsBusy ||
                !runner.SceneManager.TryGetPhysicsScene3D(out var scene) || !scene.IsValid() || scene == UnityEngine.Physics.defaultPhysicsScene) return false;
            if (courseWorld == null)
            {
                courseWorld = WordboundCourseWorld.Build(course, geometry, options: options.World);
                if (!runner.MoveGameObjectToScene(courseWorld.gameObject, options.GameplayScene ?? default) ||
                    courseWorld.gameObject.scene.GetPhysicsScene() != scene)
                { courseWorld.Dispose(); courseWorld = null; return false; }
            }
            localReady = true;
            session.MarkLocalReady();
            return true;
        }

        void GameplayStarted()
        {
            if (stopping || Fault != "") return;
            try
            {
                if (!ReferenceEquals(runner, session.Runner)) ChangeRunner(session.Runner);
                if (!localReady && !PrepareScene()) return;
                if (runner.IsServer) BeginHost();
            }
            catch (Exception error) { Fail(error); }
        }

        void BeginHost()
        {
            if (hostReady) return;
            if (!session.GameplayReady || !runner.IsServer || session.Committed == null)
                throw new InvalidOperationException("Host composition requires a validated committed session start.");
            var simulator = runner.GetComponent<RunnerSimulatePhysics>();
            if (simulator == null) { simulator = runner.gameObject.AddComponent<RunnerSimulatePhysics>(); runner.AddGlobal(simulator); }
            simulator.Update3DPhysicsScene = true;
            physics = new WordboundPhysicsWorld(runner, Authority.Match.Binding, Authority.Match.Mode, course, items, rules,
                peer => session.TryGetAdmittedActor(peer, out var actor) ? actor : null,
                () => hostReady && Fault == "" && !stopping && session.GameplayReady && !Authority.Match.IsPaused,
                options.PuzzleEvaluator, options.MovementEnvironment);
            physics.RegisterStatic(courseWorld);
            physics.HostStepCompleted += AdvanceHost;
            physics.MovementFaulted += Fail;
            query.Target = physics;
            driver = runner.gameObject.AddComponent<WordboundGameplayDriver>(); driver.Controller = this; runner.AddGlobal(driver);
            Reconcile(Authority.Match.Capture(1));
            hostReady = true;
            nextCheckpoint = options.Clock() + options.CheckpointMilliseconds;
        }

        public void PrepareHostPhysics(NetworkRunner source)
        {
            if (!hostReady || source != runner || Fault != "" || !session.GameplayReady) return;
            try
            {
                session.Poll();
                if (Fault != "" || !session.GameplayReady || !hostReady) return;
                var current = Authority.Match.Capture(1);
                if (!SameFrame(frame, current)) Reconcile(current);
                if (!Authority.Match.IsPaused) options.BeforeHostPhysics?.Invoke(frame, bodies);
            }
            catch (Exception error) { Fail(error); }
        }

        public void AdvanceHost(IReadOnlyList<WorldBodyState> poses)
        {
            if (handlingStep || !hostReady || runner == null || !runner.IsServer || !session.GameplayReady || Fault != "")
                throw new InvalidOperationException("Only one admitted host physics step may advance the match.");
            handlingStep = true;
            try
            {
                session.Poll();
                if (Fault != "" || !session.GameplayReady || !hostReady)
                    throw new InvalidOperationException("Host authorization changed during the physics step.");
                if (poses == null || poses.Count != bodies.Count || !new HashSet<string>(poses.Select(p => p.Id), StringComparer.Ordinal).SetEquals(bodies.Keys))
                    throw new ArgumentException("Host pose capture must cover exactly the reconciled body ledger.");
                var observations = options.ObserveHost?.Invoke(frame, poses) ?? new WordboundHostObservations();
                var result = Authority.Advance(options.Clock(), poses, observations.Progress, observations.Damage, observations.Mechanisms,
                    physics.CompletedThrownContacts.Concat(observations.ThrownContacts ?? Array.Empty<HostThrownContact>()));
                Reconcile(Authority.Match.Capture(1));
                HostAdvanced?.Invoke(result);
            }
            catch (Exception error) { Fail(error); throw; }
            finally { handlingStep = false; }
        }

        void Reconcile(MatchSnapshot next, int presenceRetries = 0)
        {
            if (presenceRetries > 8) throw new InvalidOperationException("Canonical presence did not settle within the signed roster.");
            string error = MatchStateValidation.Validate(next, Authority.Match.Binding, Authority.Match.Mode, course, items, rules);
            if (error != "") throw new ArgumentException(error);
            var inputs = new Dictionary<string, PlayerRef>(StringComparer.Ordinal);
            bool disconnected = false;
            foreach (var state in next.Bodies)
            {
                var peer = InputAuthority(state);
                inputs.Add(state.Id, peer);
                if (state.Kind == WorldBodyKind.Player && state.Active && peer == PlayerRef.None)
                {
                    Authority.Disconnect(state.SourceId, options.Clock());
                    disconnected = true;
                }
            }
            if (disconnected) { Reconcile(Authority.Match.Capture(1), presenceRetries + 1); return; }
            var plan = new WordboundReconciliationPlan(bodies.Keys, next);
            var views = next.Bodies.ToDictionary(b => b.Id, b => WordboundGameplayPlan.Presentation(b, next, course, geometry, items), StringComparer.Ordinal);
            foreach (var id in plan.Remove)
            {
                var body = bodies[id]; physics.Unregister(body);
                Unwatch(id);
                bodies.Remove(id);
                if (body != null && body.Object != null) runner.Despawn(body.Object);
            }
            foreach (var state in plan.Add)
            {
                PlayerRef peer = inputs[state.Id];
                var obj = runner.Spawn(prefabs.For(state.Kind), Position(state.Position), Rotation(state),
                    peer == PlayerRef.None ? (PlayerRef?)null : peer);
                if (obj == null) throw new InvalidOperationException("Canonical body spawn failed.");
                var body = obj.GetComponent<WordboundNetworkBody>();
                try { physics.Register(body, state, peer); }
                catch (ArgumentException) when (state.Kind == WorldBodyKind.Player && state.Active &&
                    !session.TryGetAdmittedActor(peer, out _))
                {
                    runner.Despawn(obj);
                    Authority.Disconnect(state.SourceId, options.Clock());
                    Reconcile(Authority.Match.Capture(1), presenceRetries + 1);
                    return;
                }
                bodies.Add(state.Id, body);
                Watch(state.Id, obj.GetComponent<WordboundGameplayBody>());
            }
            foreach (var state in next.Bodies)
            {
                var body = bodies[state.Id];
                PlayerRef peer = inputs[state.Id];
                if (body.Object.InputAuthority != peer)
                {
                    if (peer == PlayerRef.None) body.Object.RemoveInputAuthority();
                    else body.Object.AssignInputAuthority(peer);
                }
                if (peer != PlayerRef.None) runner.SetPlayerObject(peer, body.Object);
                presentations[state.Id].Configure(views[state.Id]);
            }
            if (bodies.Count != next.Bodies.Count || !new HashSet<string>(bodies.Keys).SetEquals(next.Bodies.Select(b => b.Id)))
                throw new InvalidOperationException("Canonical reconciliation left missing or duplicate bodies.");
            physics.ApplySnapshot(next);
            frame = next;
            FrameChanged?.Invoke(frame);
        }

        PlayerRef InputAuthority(WorldBodyState state)
        {
            if (state.Kind != WorldBodyKind.Player || !state.Active) return PlayerRef.None;
            var peers = runner.ActivePlayers.Where(p => session.TryGetAdmittedActor(p, out var actor) && actor == state.SourceId).ToArray();
            if (peers.Length > 1) throw new InvalidOperationException("An active canonical player has duplicate admitted peers.");
            return peers.Length == 0 ? PlayerRef.None : peers[0];
        }

        void ReceiveReplica(string json, OnlineSnapshotMark mark)
        {
            try
            {
                if (stopping || Fault != "") return;
                if (!ReferenceEquals(runner, session.Runner)) ChangeRunner(session.Runner);
                var imported = MatchSnapshotCodec.Import(json, Authority.Match.Binding, Authority.Match.Mode, course, items, rules);
                if (!imported.Success || imported.Hash != mark.StateHash || imported.Snapshot.Epoch != mark.Epoch ||
                    imported.Snapshot.Sequence != mark.Sequence || imported.Snapshot.Progress.Tick != mark.Tick)
                    throw new FormatException("Gameplay replica failed its exact canonical binding.");
                frame = imported.Snapshot;
                FrameChanged?.Invoke(frame);
            }
            catch (Exception error) { Fail(error); }
        }

        public PlayerCommand Submit(WordboundAction action, string target = "", int revision = 0, int slot = -1, string value = "")
        {
            if (Fault != "" || stopping || !localReady || session == null || !session.GameplayReady || frame == null)
                throw new InvalidOperationException("Gameplay is not ready for commands.");
            long sequence = WordboundGameplayPlan.NextSequence(frame, localActor, lastSequence, sentEpoch);
            var command = new PlayerCommand(sequence, frame.Epoch, action, target, revision, slot, value);
            session.SubmitCommand(WordboundGameplayPlan.Command(command));
            lastSequence = sequence; sentEpoch = frame.Epoch;
            return command;
        }

        async Task PublishCheckpoint()
        {
            publishing = true;
            var source = runner;
            try { await session.PublishCheckpoint(); }
            catch (Exception error) { if (ReferenceEquals(source, runner) && !stopping) Fail(error); }
            finally
            {
                if (ReferenceEquals(source, runner))
                { publishing = false; nextCheckpoint = options.Clock() + options.CheckpointMilliseconds; }
            }
        }

        void UpdateClientBodies()
        {
            if (!runner.IsServer)
            {
                var present = new HashSet<string>(StringComparer.Ordinal);
                foreach (var obj in runner.GetAllNetworkObjects())
                {
                    var view = obj.GetComponent<WordboundGameplayBody>();
                    if (view == null || view.Body.BodyId.ToString() == "") continue;
                    string id = view.Body.BodyId.ToString();
                    if (!present.Add(id)) throw new InvalidOperationException("Replicated canonical body ID is duplicated.");
                    if (!presentations.ContainsKey(id)) Watch(id, view);
                }
                foreach (var id in presentations.Keys.Where(id => !present.Contains(id)).ToArray()) Unwatch(id);
            }
            string localId = "player:" + WordboundGameplayPlan.Seat(Authority.Match.Binding, localActor);
            WordboundNetworkBody next = presentations.TryGetValue(localId, out var local) && local != null &&
                local.Object != null && local.Object.HasInputAuthority && local.Body.BodyActive ? local.Body : null;
            if (next != localBody) { localBody = next; options.LocalPlayerChanged?.Invoke(next); }
        }
        void Watch(string id, WordboundGameplayBody view)
        {
            if (view == null) throw new InvalidOperationException("Spawned prefab lacks replicated gameplay presentation.");
            presentations.Add(id, view); view.PresentationChanged += VisualChanged; VisualChanged(view);
        }
        void Unwatch(string id)
        {
            if (presentations.TryGetValue(id, out var view) && view != null) view.PresentationChanged -= VisualChanged;
            presentations.Remove(id);
        }
        void VisualChanged(WordboundGameplayBody view) { if (view.VisualKey.ToString() != "") options.ConfigureVisual?.Invoke(view); }

        static bool SameFrame(MatchSnapshot a, MatchSnapshot b) => a != null && a.Epoch == b.Epoch &&
            a.Progress.Tick == b.Progress.Tick && a.Progress.Players.Zip(b.Progress.Players, (x, y) =>
                x.Id == y.Id && x.Connected == y.Connected && x.Returned == y.Returned).All(x => x);
        void SessionFailed(string reason) => Fail(new InvalidOperationException("Session failed: " + reason));
        void Fail(Exception error)
        {
            if (Fault != "") return;
            Fault = error.GetType().Name + ": " + error.Message;
            hostReady = false; query.Target = null;
            if (physics != null) { physics.MovementFaulted -= Fail; physics.Dispose(); physics = null; }
            try { Faulted?.Invoke(Fault); }
            finally { _ = Stop(); }
        }
        public Task Stop()
        {
            if (stopTask != null) return stopTask;
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            stopTask = completion.Task;
            stopping = true;
            _ = StopCore(completion);
            return stopTask;
        }
        async Task StopCore(TaskCompletionSource<bool> completion)
        {
            Exception failure = null;
            try { CleanupRunner(); }
            catch (Exception error) { failure = error; }
            try
            {
                if (session != null) await session.Stop();
            }
            catch (Exception error) { failure = failure == null ? error : new AggregateException(failure, error); }
            if (failure is OperationCanceledException canceled) completion.TrySetCanceled(canceled.CancellationToken);
            else if (failure != null) completion.TrySetException(failure);
            else completion.TrySetResult(true);
        }
        void CleanupRunner()
        {
            query.Target = null; hostReady = false;
            if (physics != null) { physics.HostStepCompleted -= AdvanceHost; physics.MovementFaulted -= Fail; physics.Dispose(); physics = null; }
            if (runner != null) runner.RemoveCallbacks(this);
            if (driver != null)
            {
                driver.Controller = null;
                if (runner != null && runner.IsRunning) runner.RemoveGlobal(driver);
                Destroy(driver); driver = null;
            }
            foreach (var id in presentations.Keys.ToArray()) Unwatch(id);
            bodies.Clear();
            if (courseWorld != null) { courseWorld.Dispose(); courseWorld = null; }
            if (localBody != null) { localBody = null; options?.LocalPlayerChanged?.Invoke(null); }
        }
        void OnDestroy()
        {
            if (session != null)
            {
                session.GameplayStarted -= GameplayStarted; session.ReplicaReceived -= ReceiveReplica; session.Failed -= SessionFailed;
            }
            _ = Stop();
        }
        static Vector3 Position(WorldVector value) => new Vector3(value.X, value.Y, value.Z);
        static Quaternion Rotation(WorldBodyState state) => new Quaternion(state.RotationXyz.X, state.RotationXyz.Y, state.RotationXyz.Z, state.RotationW);

        public void OnInput(NetworkRunner source, NetworkInput input)
        {
            if (source != runner || frame == null || stopping || Fault != "" || !session.GameplayReady) return;
            try
            {
                var movement = options.ReadMovement?.Invoke() ?? default;
                movement.Epoch = frame.Epoch;
                if (!WordboundPhysicsPolicy.TryMovement(movement, frame.Epoch, out _))
                    movement = new WordboundMovementInput { Epoch = frame.Epoch };
                input.Set(movement);
            }
            catch (Exception error) { Fail(error); }
        }
        public void OnInputMissing(NetworkRunner r, PlayerRef p, NetworkInput i) { }
        public void OnPlayerJoined(NetworkRunner r, PlayerRef p) { }
        public void OnPlayerLeft(NetworkRunner r, PlayerRef p) { }
        public void OnConnectedToServer(NetworkRunner r) { }
        public void OnDisconnectedFromServer(NetworkRunner r, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner r, NetAddress address, NetConnectFailedReason reason) { }
        public void OnShutdown(NetworkRunner r, ShutdownReason reason) { }
        public void OnSessionListUpdated(NetworkRunner r, List<SessionInfo> list) { }
        public void OnCustomAuthenticationResponse(NetworkRunner r, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner r, HostMigrationToken token) { }
        public void OnReliableDataReceived(NetworkRunner r, PlayerRef p, ReliableKey key, ReadOnlySpan<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner r, PlayerRef p, ReliableKey key, float progress) { }
        public void OnObjectEnterAOI(NetworkRunner r, NetworkObject obj, PlayerRef p) { }
        public void OnObjectExitAOI(NetworkRunner r, NetworkObject obj, PlayerRef p) { }
        public void OnSceneLoadStart(NetworkRunner r) { }
        public void OnSceneLoadDone(NetworkRunner r) { }
#pragma warning disable CS0618
        public void OnUserSimulationMessage(NetworkRunner r, SimulationMessagePtr message) { }
#pragma warning restore CS0618
    }
}
