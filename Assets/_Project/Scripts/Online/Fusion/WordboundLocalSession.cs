using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine.SceneManagement;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed class WordboundLocalSession : IWordboundGameplaySession, INetworkRunnerCallbacks
    {
        public const string ProtocolVersion = "wordbound-local-v2";
        readonly Func<NetworkRunner> createRunner;
        readonly NetworkProjectConfig config;
        readonly WordboundFusionAuthority authority;
        readonly string localActor;
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        NetworkSceneInfo? initialScene;
        bool startRequested, started, stopping;
        Task stopTask;
        public NetworkRunner Runner { get; private set; }
        public bool RunnerStarted => started && !stopping && Failure == "" && !lifetime.IsCancellationRequested;
        public bool GameplayReady { get; private set; }
        public string Failure { get; private set; } = "";
        public OnlineSnapshotMark Committed { get; private set; }
        public string CommittedJson { get; private set; }
        public event Action GameplayStarted;
        public event Action<string, OnlineSnapshotMark> ReplicaReceived;
        public event Action<string> Failed;

        public WordboundLocalSession(Func<NetworkRunner> runnerFactory, NetworkProjectConfig projectConfig,
            WordboundFusionAuthority matchAuthority, string localPlayerId)
        {
            createRunner = runnerFactory ?? throw new ArgumentNullException(nameof(runnerFactory));
            config = projectConfig ?? throw new ArgumentNullException(nameof(projectConfig));
            authority = matchAuthority ?? throw new ArgumentNullException(nameof(matchAuthority));
            localActor = localPlayerId;
            ValidateBinding(authority.Match.Binding, authority.Match.Mode, localActor);
            if (config.PeerMode != NetworkProjectConfig.PeerModes.Multiple)
                throw new ArgumentException("Local gameplay requires an isolated Multiple-peer physics scene.");
            authority.SetTransportReady(false, true);
        }

        public static MatchBinding CreateBinding(ModeDefinition mode, ResolvedCourse course, string catalogueHash, string localPlayerId)
        {
            if (mode == null || course == null) throw new ArgumentNullException(mode == null ? nameof(mode) : nameof(course));
            RequireMode(mode);
            if (!WordboundIdentity.IsId(localPlayerId)) throw new ArgumentException("Local player identity is invalid.", nameof(localPlayerId));
            return new MatchBinding(Guid.NewGuid().ToString(), ProtocolVersion, mode.Hash, course.SourceHash, course.Hash,
                RosterHash(localPlayerId), catalogueHash, new[] { new WordboundSeat(localPlayerId, 0, 0) });
        }

        internal static void ValidateBinding(MatchBinding binding, ModeDefinition mode, string localPlayerId)
        {
            if (binding == null || mode == null) throw new ArgumentNullException(binding == null ? nameof(binding) : nameof(mode));
            RequireMode(mode);
            if (binding.ProtocolVersion != ProtocolVersion || binding.ModeHash != mode.Hash || binding.Seats.Count != 1 ||
                binding.Seats[0].PlayerId != localPlayerId || binding.Seats[0].Team != 0 || binding.Seats[0].Slot != 0 ||
                binding.RosterHash != RosterHash(localPlayerId) || mode.ValidateRoster(binding.Seats).Count != 0)
                throw new ArgumentException("Local gameplay requires its own exact one-human binding.");
        }

        static void RequireMode(ModeDefinition mode)
        {
            if (mode.Id != "TimeRun" && mode.Id != "Freestyle")
                throw new ArgumentException("Select Time Trial or Freeplay before resolving a local practice course.");
        }

        static string RosterHash(string playerId) => WordboundIdentity.Sha256("wordbound.local.roster|" +
            (playerId?.Length ?? 0).ToString(CultureInfo.InvariantCulture) + ":" + playerId + "|0|0");

        public void ConfigureScene(SceneRef scene)
        {
            if (startRequested || stopping) throw new InvalidOperationException("Configure the local scene before starting.");
            if (!scene.IsValid) throw new ArgumentException("Gameplay scene must be registered.", nameof(scene));
            var info = new NetworkSceneInfo();
            info.AddSceneRef(scene, LoadSceneMode.Additive);
            initialScene = info;
        }

        public async Task StartLocal(CancellationToken cancellationToken = default)
        {
            if (startRequested || stopping) throw new InvalidOperationException("A local session may only start once.");
            if (!initialScene.HasValue) throw new InvalidOperationException("Configure an isolated gameplay scene before starting.");
            startRequested = true;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                Runner = createRunner() ?? throw new InvalidOperationException("Runner factory returned no runner.");
                Runner.AddCallbacks(this);
                var result = await Runner.StartGame(new StartGameArgs {
                    GameMode = GameMode.Single, PlayerCount = 1, Config = config, Scene = initialScene,
                    StartGameCancellationToken = linked.Token
                });
                linked.Token.ThrowIfCancellationRequested();
                if (stopping || Failure != "") throw new OperationCanceledException("Local startup was superseded.");
                if (!result.Ok) throw new InvalidOperationException("Local Fusion startup failed: " + result.ShutdownReason);
                ValidateRunner();
                started = true;
            }
            catch (Exception error)
            {
                if (!stopping && Failure == "") Fail(error);
                throw;
            }
        }

        void ValidateRunner()
        {
            if (Runner == null || !Runner.IsRunning || !Runner.IsServer || Runner.GameMode != GameMode.Single ||
                Runner.LocalPlayer == PlayerRef.None || Runner.ActivePlayers.Count() != 1 ||
                !Runner.ActivePlayers.Contains(Runner.LocalPlayer))
                throw new InvalidOperationException("Local runner must contain exactly its one local player.");
        }

        public void MarkLocalReady()
        {
            if (!RunnerStarted) throw new InvalidOperationException("Local scene readiness requires completed runner startup.");
            if (GameplayReady) return;
            try
            {
                ValidateRunner();
                CommitCheckpoint();
                GameplayReady = true;
                authority.SetTransportReady(true, true);
                GameplayStarted?.Invoke();
            }
            catch (Exception error) { Fail(error); throw; }
        }

        public void Poll()
        {
            if (!RunnerStarted) return;
            try { ValidateRunner(); }
            catch (Exception error) { Fail(error); }
        }

        public bool TryGetAdmittedActor(PlayerRef peer, out string actorId)
        {
            actorId = null;
            if (!GameplayReady || !RunnerStarted || !Runner.IsRunning || !Runner.IsServer ||
                peer == PlayerRef.None || peer != Runner.LocalPlayer || !Runner.ActivePlayers.Contains(peer)) return false;
            actorId = localActor;
            return true;
        }

        public void SubmitCommand(Dictionary<string, object> command)
        {
            if (!TryGetAdmittedActor(Runner?.LocalPlayer ?? PlayerRef.None, out var actor))
                throw new InvalidOperationException("Local gameplay is not accepting commands.");
            var decoded = OnlineFusionWire.Decode(OnlineFusionWire.Encode(OnlineWireKind.Command, command));
            authority.AcceptCommand(actor, decoded.Body);
        }

        public Task<bool> PublishCheckpoint(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!GameplayReady || !RunnerStarted) return Task.FromResult(false);
            try { ValidateRunner(); CommitCheckpoint(); return Task.FromResult(true); }
            catch (Exception error) { Fail(error); throw; }
        }

        void CommitCheckpoint()
        {
            var mark = authority.Publish(out var json);
            authority.ValidateSnapshot(json, mark);
            if (!authority.AcknowledgeDurable(mark)) throw new InvalidOperationException("Local checkpoint acknowledgement did not match.");
            CommittedJson = json;
            Committed = mark;
            ReplicaReceived?.Invoke(json, mark);
        }

        void Fail(Exception error)
        {
            if (Failure != "" || stopping) return;
            Failure = error.GetType().Name;
            started = false; GameplayReady = false;
            lifetime.Cancel();
            authority.SetTransportReady(false, true);
            Failed?.Invoke(Failure);
        }

        public Task Stop()
        {
            if (stopTask != null) return stopTask;
            stopping = true; started = false; GameplayReady = false;
            lifetime.Cancel();
            authority.SetTransportReady(false, true);
            return stopTask = StopRunner();
        }

        async Task StopRunner()
        {
            if (Runner != null)
            {
                Runner.RemoveCallbacks(this);
                await Runner.Shutdown();
            }
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
        {
            if (ReferenceEquals(runner, Runner) && !stopping)
                Fail(new InvalidOperationException("The local runner stopped."));
        }
        public void OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) => request.Refuse();
        public void OnPlayerJoined(NetworkRunner r, PlayerRef p) { }
        public void OnPlayerLeft(NetworkRunner r, PlayerRef p) { if (ReferenceEquals(r, Runner)) Poll(); }
        public void OnInput(NetworkRunner r, NetworkInput i) { }
        public void OnInputMissing(NetworkRunner r, PlayerRef p, NetworkInput i) { }
        public void OnConnectedToServer(NetworkRunner r) { }
        public void OnDisconnectedFromServer(NetworkRunner r, NetDisconnectReason reason) { }
        public void OnConnectFailed(NetworkRunner r, NetAddress address, NetConnectFailedReason reason) { }
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
