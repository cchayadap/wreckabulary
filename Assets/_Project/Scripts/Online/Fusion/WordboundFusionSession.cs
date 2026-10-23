using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public interface IWordboundFusionAuthority
    {
        string IdentityHash { get; }
        long Epoch { get; }
        void AcceptCommand(string authenticatedActor, JsonNode command);
        OnlineSnapshotMark Publish(out string json);
        void ValidateSnapshot(string json, OnlineSnapshotMark mark);
        bool AcknowledgeDurable(OnlineSnapshotMark mark);
        void PauseForMigration();
        void RestoreCommitted(string json, OnlineSnapshotMark mark, long nextEpoch, long wallClockMilliseconds);
        IReadOnlyList<OnlineSeatPresence> Presence();
        void SetTransportReady(bool ready, bool host);
        void Disconnect(string actor, long wallClockMilliseconds);
        bool Reconnect(string actor, long wallClockMilliseconds);
        void Return(string actor, long wallClockMilliseconds);
    }

    public sealed class WordboundFusionJoin
    {
        public string PlayerId { get; }
        public string Certificate { get; }
        public string Grant { get; }
        public string ConnectionSecret { get; }
        public OnlinePhotonTicket PhotonTicket { get; }
        public WordboundFusionJoin(string playerId, string certificate, string grant, string connectionSecret, OnlinePhotonTicket ticket)
        {
            PlayerId = playerId; Certificate = certificate; Grant = grant; ConnectionSecret = connectionSecret;
            PhotonTicket = ticket ?? throw new ArgumentNullException(nameof(ticket));
        }
    }

    public sealed partial class WordboundFusionSession : IWordboundGameplaySession, INetworkRunnerCallbacks
    {
        sealed class RunScope
        {
            public readonly CancellationTokenSource Cancellation;
            public RunScope(CancellationToken request, CancellationToken lifetime)
            { Cancellation = CancellationTokenSource.CreateLinkedTokenSource(request, lifetime); }
            public readonly HashSet<PlayerRef> Authorizing = new HashSet<PlayerRef>();
            public readonly HashSet<PlayerRef> Readied = new HashSet<PlayerRef>();
            public NetworkRunner Runner;
            public WordboundFusionJoin Join;
            public string ConnectionId, HostId;
            public bool Publishing, Beginning, Renewing, Started;
            public long RenewAt;
            public OnlineCheckpointWriter Writer;
            public OnlineCheckpointManifest Head;
            public OnlineCheckpointUpload Upload;
            public bool CheckpointReady, WriterRenewing, HostAvailableSeen, CheckingHost;
            public int HostPromptChecks;
            public long WriterRenewAt;
        }
        const int WireChannel = 0x57424e44;
        readonly Func<NetworkRunner> createRunner;
        readonly Func<CancellationToken, Task<WordboundFusionJoin>> credentials;
        readonly Func<long> clock;
        readonly OnlineAdmissionVerifier verifier;
        readonly OnlineMatchExpectation expected;
        readonly FusionAppSettings app;
        readonly NetworkProjectConfig config;
        readonly NetworkPrefabRef chunkPrefab;
        readonly IWordboundFusionAuthority authority;
        readonly OnlineConnectionService connections;
        readonly OnlineCheckpointService checkpoints;
        readonly Dictionary<PlayerRef, long> pending = new Dictionary<PlayerRef, long>();
        readonly Dictionary<PlayerRef, (long Window, int Count)> rates = new Dictionary<PlayerRef, (long, int)>();
        readonly Dictionary<PlayerRef, OnlineSnapshotMark> sentMarks = new Dictionary<PlayerRef, OnlineSnapshotMark>();
        readonly Dictionary<PlayerRef, OnlineConnectionLease> leases = new Dictionary<PlayerRef, OnlineConnectionLease>();
        readonly List<WordboundFusionSnapshotChunk> carriers = new List<WordboundFusionSnapshotChunk>();
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        OnlineSessionGate gate;
        RunScope activeRun;
        long runGeneration;
        OnlineSnapshotMark receivedMark, committedMark;
        byte[][] receivedChunks;
        string receivedWireHash;
        int receivedBytes;
        long incomingStarted;
        bool localReady, localAdmitted, migrating, restored, initialStarted, beginReceived;
        OnlineSnapshotPacket committedPacket;
        int wireSequence;
        Task stopTask;
        NetworkSceneInfo? initialScene;
        public NetworkRunner Runner { get; private set; }
        public bool RunnerStarted => IsCurrent(activeRun) && activeRun.Started;
        public bool GameplayReady { get; private set; }
        public string Failure { get; private set; } = "";
        public string FailureStage { get; private set; } = "";
        public string FailureCode { get; private set; } = "";
        public string FailureOrigin { get; private set; } = "";
        public string FailureFacts { get; private set; } = "";
        public OnlineSnapshotMark Committed => committedMark;
        public event Action GameplayStarted;
        public event Action<string, OnlineSnapshotMark> ReplicaReceived;
        public event Action<PlayerRef, OnlineSnapshotMark> ReplicaAcknowledged;
        public event Action<string> Failed;

        public WordboundFusionSession(Func<NetworkRunner> runnerFactory, FusionAppSettings appSettings,
            NetworkProjectConfig projectConfig, NetworkPrefabRef snapshotChunkPrefab,
            OnlineAdmissionVerifier admissionVerifier, OnlineMatchExpectation expectation,
            Func<CancellationToken, Task<WordboundFusionJoin>> freshCredentials,
            IWordboundFusionAuthority matchAuthority, OnlineConnectionService connectionService,
            OnlineCheckpointService checkpointService, Func<long> wallClock = null)
        {
            createRunner = runnerFactory ?? throw new ArgumentNullException(nameof(runnerFactory));
            app = appSettings?.GetCopy() ?? throw new ArgumentNullException(nameof(appSettings));
            config = projectConfig ?? throw new ArgumentNullException(nameof(projectConfig));
            verifier = admissionVerifier ?? throw new ArgumentNullException(nameof(admissionVerifier));
            expected = expectation ?? throw new ArgumentNullException(nameof(expectation));
            credentials = freshCredentials ?? throw new ArgumentNullException(nameof(freshCredentials));
            authority = matchAuthority ?? throw new ArgumentNullException(nameof(matchAuthority));
            connections = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            checkpoints = checkpointService ?? throw new ArgumentNullException(nameof(checkpointService));
            clock = wallClock ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            chunkPrefab = snapshotChunkPrefab;
            if (config.HostMigration.EnableAutoUpdate)
                throw new ArgumentException("Automatic Photon snapshots must be disabled; canonical durability belongs to the coordinator.");
            if ((config.Network.ReliableDataTransferModes & NetworkConfiguration.ReliableDataTransfers.ClientToServer) == 0)
                throw new ArgumentException("Client-to-server reliable data must be enabled.");
            if ((config.Network.ReliableDataTransferModes & NetworkConfiguration.ReliableDataTransfers.ClientToClientWithServerProxy) != 0)
                throw new ArgumentException("Client-to-client reliable forwarding must be disabled.");
            app.FixedRegion = expected.Settings.Region;
            app.AppVersion = expected.Settings.ProtocolVersion;
        }

        public void ConfigureScene(SceneRef scene)
        {
            if (initialStarted) throw new InvalidOperationException("Configure the gameplay scene before starting the session.");
            if (!scene.IsValid) throw new ArgumentException("Gameplay scene must be registered.", nameof(scene));
            var info = new NetworkSceneInfo();
            info.AddSceneRef(scene, LoadSceneMode.Additive);
            initialScene = info;
        }

        public Task Start(CancellationToken cancellationToken = default)
        {
            if (initialStarted) throw new InvalidOperationException("A session may only start once.");
            if (config.PeerMode == NetworkProjectConfig.PeerModes.Multiple && !initialScene.HasValue)
                throw new InvalidOperationException("Isolated gameplay requires a scene before starting the session.");
            initialStarted = true;
            return StartRunner(null, cancellationToken);
        }
        bool IsCurrent(RunScope run) => run != null && ReferenceEquals(run, activeRun) &&
            !run.Cancellation.IsCancellationRequested && !lifetime.IsCancellationRequested;
        void RequireCurrent(RunScope run)
        {
            if (!IsCurrent(run)) throw new OperationCanceledException("Runner operation was superseded.");
        }
        long InvalidateRun()
        {
            var previous = activeRun;
            activeRun = null;
            runGeneration = checked(runGeneration + 1);
            previous?.Cancellation.Cancel();
            return runGeneration;
        }
        RunScope BeginRun(CancellationToken cancellationToken)
        {
            lifetime.Token.ThrowIfCancellationRequested();
            InvalidateRun();
            localReady = false; localAdmitted = false; beginReceived = false;
            return activeRun = new RunScope(cancellationToken, lifetime.Token);
        }
        async Task StartRunner(HostMigrationToken migration, CancellationToken cancellationToken, bool reconnect = false,
            Action consumeMigration = null)
        {
            var run = BeginRun(cancellationToken);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token, run.Cancellation.Token);
            try {
            var join = await credentials(linked.Token);
            RequireCurrent(run);
            run.Join = join;
            if (localPlayerId != null && join.PlayerId != localPlayerId)
                throw new InvalidOperationException("The original match identity changed during recovery.");
            localPlayerId = join.PlayerId;
            long now = clock();
            if (join.PhotonTicket.AppId != app.AppIdFusion || join.PhotonTicket.ExpiresAtMilliseconds <= now)
                throw new InvalidOperationException("Photon authentication ticket is expired or belongs to another app.");
            var match = verifier.VerifyStart(join.Certificate, expected, now / 1000);
            verifier.VerifyAdmission(join.Grant, match, join.PlayerId, join.ConnectionSecret, now / 1000);
            if (gate == null) gate = new OnlineSessionGate(match);
            var existing = await checkpoints.Head(linked.Token);
            RequireCurrent(run);
            bool initialHost = existing.Writer == null && existing.Head == null && join.PlayerId == match.Roster[0].PlayerId;
            var authentication = new AuthenticationValues { AuthType = CustomAuthenticationType.Custom };
            authentication.SetAuthPostData(join.PhotonTicket.PhotonPostData());
            Runner = createRunner() ?? throw new InvalidOperationException("Runner factory returned no runner.");
            run.Runner = Runner;
            run.Runner.AddCallbacks(this);
            if (migration != null) consumeMigration?.Invoke();
            var result = await run.Runner.StartGame(new StartGameArgs {
                GameMode = migration?.GameMode ?? (!reconnect && initialHost ? GameMode.Host : GameMode.Client),
                SessionName = "wb-" + expected.MatchId + "-" + expected.Generation,
                PlayerCount = match.Roster.Count, IsVisible = false, IsOpen = true,
                EnableClientSessionCreation = false, CustomPhotonAppSettings = app,
                AuthValues = authentication, Config = config, ConnectionToken = Encoding.ASCII.GetBytes(join.ConnectionSecret),
                HostMigrationToken = migration, HostMigrationResume = RestoreMigration,
                Scene = initialScene,
                StartGameCancellationToken = linked.Token
            });
            RequireCurrent(run);
            if (!result.Ok) throw new WordboundRunnerStartException(result.ShutdownReason);
            if (run.Runner.GetPlayerUserId(run.Runner.LocalPlayer) != join.PlayerId)
                throw new InvalidOperationException("Photon did not authenticate the coordinator identity.");
            if (connections.LocalPlayerId != join.PlayerId) throw new InvalidOperationException("Coordinator guest changed during startup.");
            run.Started = true;
            if (run.Runner.IsServer)
            {
                await RestoreWriter(run, migration != null);
                RequireCurrent(run);
                await AdmitLocal(run);
                RequireCurrent(run);
                foreach (var peer in run.Runner.ActivePlayers)
                    if (peer != run.Runner.LocalPlayer) PromptAdmission(run, peer);
            }
            RequireCurrent(run);
            }
            catch (Exception error) { if (IsCurrent(run) && !reconnect) Fail(error); throw; }
        }
        bool HasWriter(RunScope run) => IsCurrent(run) && run.Writer != null &&
            run.Writer.PlayerId == checkpoints.LocalPlayerId && run.Writer.ExpiresAtMilliseconds > clock();
        void RequireWriter(RunScope run, string term)
        {
            RequireCurrent(run);
            if (!HasWriter(run) || run.Writer.Term != term)
                throw new InvalidOperationException("Canonical writer lease expired or changed.");
        }
        async Task RestoreWriter(RunScope run, bool recovery)
        {
            string claimId = Guid.NewGuid().ToString();
            long deadline = checked(clock() + 60000);
            OnlineCheckpointState state;
            while (true)
            {
                RequireCurrent(run);
                try { state = await checkpoints.Claim(claimId, run.Cancellation.Token); break; }
                catch (OnlineServiceException error) when (error.Code == "CHECKPOINT_WRITER_BUSY")
                {
                    RequireCurrent(run);
                    if (clock() >= deadline) throw new TimeoutException("Previous host still owns the canonical writer lease.");
                    await Task.Delay(500, run.Cancellation.Token);
                }
            }
            RequireCurrent(run);
            run.Writer = state.Writer; run.Head = state.Head; run.WriterRenewAt = clock() + 5000;
            RequireWriter(run, state.Writer.Term);
            if (state.Head != null)
            {
                string json = await checkpoints.Download(state.Head, run.Cancellation.Token);
                RequireWriter(run, state.Writer.Term);
                authority.ValidateSnapshot(json, state.Head.Mark);
                authority.RestoreCommitted(json, state.Head.Mark, state.Writer.Epoch, clock());
                gate.RestoreForMigration(authority.Presence(), clock());
                committedMark = state.Head.Mark; committedPacket = OnlineSnapshotPacket.Compress(json);
                migrating = true; restored = true;
            }
            else if (recovery)
                throw new InvalidOperationException("The departing host never committed the initial gameplay state.");
            await RefreshConnectionState(run);
            RequireWriter(run, state.Writer.Term);
            var opened = await checkpoints.OpenAdmission(run.Writer, run.Cancellation.Token);
            RequireWriter(run, state.Writer.Term);
            run.Writer = opened.Writer; run.CheckpointReady = true;
        }
        void PromptAdmission(RunScope run, PlayerRef peer)
        {
            if (!run.CheckpointReady || !HasWriter(run)) return;
            pending[peer] = clock() + 15000;
            Send(peer, OnlineWireKind.HostAvailable, new Dictionary<string, object> {
                ["roster"] = expected.RosterHash, ["host"] = run.Writer.PlayerId, ["term"] = run.Writer.Term });
        }
        async Task ReceiveHostAvailable(RunScope run, JsonNode body)
        {
            if (run.HostAvailableSeen || run.CheckingHost || run.HostPromptChecks >= 4 || localAdmitted) return;
            run.CheckingHost = true; run.HostPromptChecks++;
            try
            {
                if (body["roster"].String() != expected.RosterHash) return;
                var state = await checkpoints.Head(run.Cancellation.Token);
                RequireCurrent(run);
                var writer = state.Writer;
                if (writer == null || !writer.AdmissionOpen || writer.ExpiresAtMilliseconds <= clock() ||
                    writer.PlayerId != body["host"].String() || writer.Term != body["term"].String() ||
                    !gate.Match.Roster.Any(s => s.PlayerId == writer.PlayerId)) return;
                run.HostId = writer.PlayerId; run.HostAvailableSeen = true;
                await SendAdmission(run);
            }
            catch (Exception error) { if (IsCurrent(run)) Fail(error); }
            finally { run.CheckingHost = false; }
        }
        async Task AdmitLocal(RunScope run)
        {
            RequireCurrent(run);
            if (migrating && !restored) return;
            string ticket = await connections.Ticket(run.Cancellation.Token);
            RequireCurrent(run);
            run.ConnectionId = Guid.NewGuid().ToString();
            await Admit(run, run.Runner.LocalPlayer, run.Join.Certificate, run.Join.Grant, ticket, run.ConnectionId);
            RequireCurrent(run);
            if (localReady) MarkLocalReady();
        }
        async Task SendAdmission(RunScope run)
        {
            run.ConnectionId = Guid.NewGuid().ToString();
            string ticket = await connections.Ticket(run.Cancellation.Token);
            RequireCurrent(run);
            SendServer(OnlineWireKind.Admission, new Dictionary<string, object> {
                ["certificate"] = run.Join.Certificate, ["grant"] = run.Join.Grant, ["connectionTicket"] = ticket,
                ["connectionId"] = run.ConnectionId });
        }
        async Task Admit(RunScope run, PlayerRef player, string certificate, string grant, string ticket, string connectionId)
        {
            if (!IsCurrent(run) || !run.CheckpointReady || !HasWriter(run) || run.Authorizing.Contains(player) || gate.Actor(Peer(player)) != null) return;
            run.Authorizing.Add(player);
            var runner = run.Runner;
            string stage = "transport-identity";
            try {
            string authenticated = runner.GetPlayerUserId(player);
            string secret = player == runner.LocalPlayer ? run.Join.ConnectionSecret :
                OnlineJson.DecodeUtf8(runner.GetPlayerConnectionToken(player) ?? Array.Empty<byte>());
            long now = clock();
            stage = "start-certificate";
            var match = verifier.VerifyStart(certificate, expected, now / 1000);
            stage = "admission-grant";
            var admission = verifier.VerifyAdmission(grant, match, authenticated, secret, now / 1000);
            stage = "connection-authorization";
            var lease = await connections.Authorize(match, authenticated, ticket, secret, connectionId, run.Cancellation.Token);
            if (!IsCurrent(run) || !HasWriter(run) || !runner.IsRunning || !runner.ActivePlayers.Contains(player) ||
                runner.GetPlayerUserId(player) != authenticated) return;
            now = clock();
            stage = "activate-connection";
            if (lease.ExpiresAtMilliseconds <= now) throw new InvalidOperationException("Transport authorization expired.");
            gate.Admit(admission, Peer(player), now);
            leases[player] = lease;
            pending.Remove(player);
            if (player != Runner.LocalPlayer) Send(player, OnlineWireKind.Admitted, new Dictionary<string, object> {
                ["roster"] = expected.RosterHash, ["authorization"] = lease.Authorization });
            }
            catch (Exception error) {
                if (IsCurrent(run)) {
                    if (player != runner.LocalPlayer) runner.Disconnect(player);
                    else FailAt(error, stage);
                }
            }
            finally { run.Authorizing.Remove(player); }
        }
        public void MarkLocalReady()
        {
            localReady = true;
            if (!IsCurrent(activeRun) || Runner == null || !Runner.IsRunning) return;
            if (Runner.IsServer)
            {
                if (gate.Actor(Peer(Runner.LocalPlayer)) == null) return;
                ReadyAdmittedPeer(activeRun, Runner.LocalPlayer); TryBegin();
            }
            else if (localAdmitted) SendServer(OnlineWireKind.Ready, Roster());
        }
        void ReadyAdmittedPeer(RunScope run, PlayerRef player)
        {
            RequireCurrent(run);
            string actor = gate.Actor(Peer(player));
            long now = clock();
            if (actor == null || !run.Runner.IsRunning || !run.Runner.ActivePlayers.Contains(player) ||
                run.Runner.GetPlayerUserId(player) != actor || !leases.TryGetValue(player, out var lease) ||
                lease.PlayerId != actor || lease.ExpiresAtMilliseconds <= now)
                throw new InvalidOperationException("Readiness requires a current authenticated connection lease.");
            if (run.Readied.Contains(player)) return;
            if (gate.Started && !authority.Reconnect(actor, now))
            {
                gate.Disconnect(Peer(player), now);
                throw new InvalidOperationException("Original seat is no longer reconnectable.");
            }
            gate.Ready(Peer(player));
            run.Readied.Add(player);
        }
        void TryBegin()
        {
            var run = activeRun;
            if (!IsCurrent(run)) return;
            bool requiresCompleteRoster = !gate.Started;
            bool begin = gate.TryStart();
            if (migrating && restored && gate.Frozen && gate.ConnectedCount > 0)
            {
                gate.ResumeAfterRestore(); migrating = false; begin = true;
            }
            if (!begin) return;
            if (run.Beginning) return;
            run.Beginning = true;
            _ = BeginAfterCheckpoint(run, requiresCompleteRoster);
        }
        async Task BeginAfterCheckpoint(RunScope run, bool requiresCompleteRoster)
        {
            try { await RefreshConnectionState(run); }
            catch (Exception error) { if (IsCurrent(run)) Fail(error); return; }
            if (!IsCurrent(run)) return;
            if (Failure != "") return;
            if (requiresCompleteRoster && !gate.AllReady) { Fail(new InvalidOperationException("The starting roster changed.")); return; }
            bool published = await PublishCheckpointCore(run, true, run.Cancellation.Token);
            if (!IsCurrent(run)) return;
            if (!published) { Fail(new InvalidOperationException("Initial checkpoint was not durable.")); return; }
            await SeedElection(run);
            if (!IsCurrent(run) || !HasWriter(run)) return;
            GameplayReady = true; run.Beginning = false;
            gameplayObserved = true;
            authority.SetTransportReady(true, true);
            foreach (var player in Runner.ActivePlayers)
                if (player != Runner.LocalPlayer && gate.Actor(Peer(player)) != null) Send(player, OnlineWireKind.Begin, Roster());
            GameplayStarted?.Invoke();
        }
        Dictionary<string, object> Roster() => new Dictionary<string, object> { ["roster"] = expected.RosterHash };
        static string Peer(PlayerRef player) => player.RawEncoded.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ReliableKey NextKey() => ReliableKey.FromInts(WireChannel, 1, 0, unchecked(++wireSequence));
        void Send(PlayerRef player, OnlineWireKind kind, object body) =>
            Runner.SendReliableDataToPlayer(player, NextKey(), OnlineFusionWire.Encode(kind, body));
        void SendServer(OnlineWireKind kind, object body) =>
            Runner.SendReliableDataToServer(NextKey(), OnlineFusionWire.Encode(kind, body));
        public void SubmitCommand(Dictionary<string, object> command)
        {
            if (!GameplayReady || migrating) throw new InvalidOperationException("Match is not accepting input.");
            var bytes = OnlineFusionWire.Encode(OnlineWireKind.Command, command);
            if (Runner.IsServer) ReceiveCommand(Runner.LocalPlayer, OnlineFusionWire.Decode(bytes).Body);
            else Runner.SendReliableDataToServer(NextKey(), bytes);
        }
        void ReceiveCommand(PlayerRef player, JsonNode body)
        {
            if (!TryGetAdmittedActor(player, out string actor)) throw new InvalidOperationException("Peer cannot command this match.");
            long window = clock() / 1000;
            rates.TryGetValue(player, out var rate);
            int count = rate.Window == window ? rate.Count + 1 : 1;
            if (count > 60) throw new InvalidOperationException("Command rate exceeded.");
            rates[player] = (window, count);
            authority.AcceptCommand(actor, body);
        }
        public bool TryGetAdmittedActor(PlayerRef peer, out string actorId)
        {
            actorId = null;
            var run = activeRun;
            if (!IsCurrent(run) || !GameplayReady || migrating || Failure != "" || run.Runner == null ||
                !HasWriter(run) ||
                !run.Runner.IsRunning || !run.Runner.IsServer || !run.Runner.ActivePlayers.Contains(peer) ||
                !gate.CanCommand(Peer(peer)) || !leases.TryGetValue(peer, out var lease) || lease.ExpiresAtMilliseconds <= clock())
                return false;
            string authenticated = run.Runner.GetPlayerUserId(peer);
            if (authenticated != lease.PlayerId || authenticated != gate.Actor(Peer(peer))) return false;
            try { if (lease.HostId != connections.LocalPlayerId) return false; }
            catch (InvalidOperationException) { return false; }
            actorId = authenticated;
            return true;
        }
        public void Poll()
        {
            var run = activeRun;
            if (!IsCurrent(run) || Runner == null || !Runner.IsRunning || !Runner.IsServer) return;
            if (Failure != "") return;
            if (run.Writer != null && !HasWriter(run)) { Fail(new TimeoutException("Canonical writer lease expired.")); return; }
            if (run.Writer != null && !run.WriterRenewing && clock() >= run.WriterRenewAt) _ = RenewWriter(run);
            if (!run.CheckpointReady) return;
            if (!gate.Started && clock() >= gate.Match.StartedAtMilliseconds + 90000)
            { Fail(new TimeoutException("The complete approved roster did not become ready.")); return; }
            foreach (var peer in pending.Where(p => p.Value <= clock()).Select(p => p.Key).ToArray())
            { pending.Remove(peer); Runner.Disconnect(peer); }
            foreach (var peer in leases.Where(p => p.Value.ExpiresAtMilliseconds <= clock()).Select(p => p.Key).ToArray())
                Revoke(peer, false);
            if (!IsCurrent(run)) return;
            if (!run.Renewing && clock() >= run.RenewAt) _ = RenewConnections(run);
        }
        async Task RenewWriter(RunScope run)
        {
            run.WriterRenewing = true; run.WriterRenewAt = clock() + 5000;
            string term = run.Writer.Term;
            try
            {
                var renewed = await checkpoints.Renew(run.Writer, run.Cancellation.Token);
                RequireWriter(run, term);
                run.Writer = renewed.Writer;
            }
            catch { }
            finally { run.WriterRenewing = false; }
        }
        async Task RenewConnections(RunScope run)
        {
            run.Renewing = true; run.RenewAt = clock() + 5000;
            try { await RefreshConnectionState(run); }
            catch { }
            finally { run.Renewing = false; }
        }
        async Task RefreshConnectionState(RunScope run)
        {
            RequireCurrent(run);
            var runner = run.Runner;
            var current = leases.ToDictionary(p => p.Key, p => p.Value);
            var state = await connections.Renew(gate.Match, current.Values, run.Cancellation.Token);
            if (!IsCurrent(run) || !runner.IsRunning) return;
            foreach (var seat in state)
            {
                if (seat.Returned) { gate.Return(seat.PlayerId); authority.Return(seat.PlayerId, clock()); }
            }
            foreach (var pair in current)
            {
                if (!leases.TryGetValue(pair.Key, out var active) || active != pair.Value) continue;
                var seat = state.Single(s => s.PlayerId == active.PlayerId);
                if (seat.Returned || seat.ConnectionId != active.ConnectionId || seat.Epoch != active.Epoch ||
                    seat.HostId != connections.LocalPlayerId || seat.ExpiresAtMilliseconds <= clock())
                    Revoke(pair.Key, seat.Returned);
            }
        }
        void Revoke(PlayerRef peer, bool returned)
        {
            if (!leases.TryGetValue(peer, out var lease)) return;
            leases.Remove(peer);
            if (returned) { gate.Return(lease.PlayerId); authority.Return(lease.PlayerId, clock()); }
            else { gate.Disconnect(Peer(peer), clock()); authority.Disconnect(lease.PlayerId, clock()); }
            if (peer == Runner.LocalPlayer) Fail(new InvalidOperationException("Host connection authorization expired or was revoked."));
            else Runner.Disconnect(peer);
        }

        public Task<bool> PublishCheckpoint(CancellationToken cancellationToken = default) => PublishCheckpointCore(activeRun, false, cancellationToken);
        async Task<bool> PublishCheckpointCore(RunScope run, bool bootstrap, CancellationToken cancellationToken)
        {
            if (!IsCurrent(run) || Runner == null || !Runner.IsServer || (!GameplayReady && !bootstrap) ||
                !run.CheckpointReady || !HasWriter(run) || run.Publishing || migrating || Failure != "") return false;
            run.Publishing = true;
            string term = run.Writer.Term;
            try
            {
                if (run.Upload == null)
                {
                    var mark = authority.Publish(out string json);
                    authority.ValidateSnapshot(json, mark);
                    run.Upload = checkpoints.Prepare(run.Writer, run.Head, mark, json);
                }
                var upload = run.Upload;
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, run.Cancellation.Token);
                var receipt = await checkpoints.Commit(upload, linked.Token);
                RequireWriter(run, term);
                if (!run.Runner.IsRunning || migrating || !receipt.Same(upload.Manifest)) return false;
                CommitPublication(run, upload, receipt);
                return true;
            }
            catch (Exception error)
            {
                if (!IsCurrent(run) || !HasWriter(run) || run.Writer.Term != term) return false;
                if (run.Upload != null)
                {
                    try
                    {
                        var state = await checkpoints.Head(run.Cancellation.Token);
                        RequireWriter(run, term);
                        if (state.Writer?.Term == term && state.Writer.PlayerId == run.Writer.PlayerId &&
                            run.Upload.Manifest.Same(state.Head))
                        {
                            CommitPublication(run, run.Upload, state.Head);
                            return true;
                        }
                    }
                    catch { }
                }
                if (IsCurrent(run) && (error is FormatException || error is InvalidOperationException ||
                    error is OnlineServiceException service && service.Status < 500 && service.Status != 429)) Fail(error);
                return false;
            }
            finally { run.Publishing = false; }
        }
        void CommitPublication(RunScope run, OnlineCheckpointUpload upload, OnlineCheckpointManifest receipt)
        {
            RequireWriter(run, receipt.Term);
            if (!ReferenceEquals(run.Upload, upload) || !receipt.Same(upload.Manifest))
                throw new InvalidOperationException("Checkpoint publication was superseded.");
            if (!authority.AcknowledgeDurable(receipt.Mark)) throw new InvalidOperationException("Durable checkpoint did not match its publication.");
            run.Head = receipt; run.Upload = null; committedMark = receipt.Mark; committedPacket = upload.Packet;
            foreach (var player in run.Runner.ActivePlayers)
                if (player != run.Runner.LocalPlayer && gate.Actor(Peer(player)) != null) SendCommitted(player);
        }
        async Task SeedElection(RunScope run)
        {
            string term = run.Writer.Term;
            try
            {
                RequireWriter(run, term);
                foreach (var carrier in carriers) if (carrier != null && carrier.Object != null) run.Runner.Despawn(carrier.Object);
                carriers.Clear();
                var marker = OnlineSnapshotPacket.Compress(OnlineJson.Encode(new Dictionary<string, object> {
                    ["format"] = "wordbound-election-marker", ["match"] = expected.MatchId, ["roster"] = expected.RosterHash }));
                var obj = run.Runner.Spawn(chunkPrefab, onBeforeSpawned: (_, value) =>
                    value.GetComponent<WordboundFusionSnapshotChunk>().Write(committedMark, marker, 0));
                carriers.Add(obj.GetComponent<WordboundFusionSnapshotChunk>());
                int writtenTick = run.Runner.Tick.Raw;
                long deadline = checked(clock() + 10000);
                while (run.Runner.Tick.Raw <= writtenTick)
                {
                    RequireWriter(run, term);
                    if (clock() >= deadline) throw new TimeoutException("Election marker did not reach a simulation tick.");
                    await Task.Yield();
                }
                while (true)
                {
                    RequireWriter(run, term);
                    if (clock() >= deadline) throw new TimeoutException("Photon election marker send did not complete.");
                    var push = run.Runner.PushHostMigrationSnapshot();
                    while (!push.IsCompleted)
                    {
                        RequireWriter(run, term);
                        if (clock() >= deadline) throw new TimeoutException("Photon election marker send did not complete.");
                        await Task.WhenAny(push, Task.Delay(100, run.Cancellation.Token));
                    }
                    bool queued = await push;
                    RequireWriter(run, term);
                    if (clock() >= deadline) throw new TimeoutException("Photon election marker send completed after its deadline.");
                    if (queued) return;
                    if (clock() >= deadline) throw new TimeoutException("Photon did not accept the election marker send.");
                    await Task.Delay(100, run.Cancellation.Token);
                }
            }
            catch (Exception error) { if (IsCurrent(run)) Fail(error); }
        }
        void SendCommitted(PlayerRef player)
        {
            if (committedPacket == null || committedMark == null || sentMarks.ContainsKey(player) || !gate.CanCommand(Peer(player))) return;
            var packet = committedPacket; var mark = committedMark; sentMarks[player] = mark;
            for (int index = 0; index < packet.ChunkCount; index++)
                Send(player, OnlineWireKind.Snapshot, new Dictionary<string, object> {
                    ["mark"] = mark.ToValue(), ["index"] = index, ["count"] = packet.ChunkCount, ["length"] = packet.Length,
                    ["wireHash"] = packet.WireHash, ["data"] = OnlineJson.Base64Url(packet.Chunk(index)) });
        }
        void ReceiveSnapshot(JsonNode body)
        {
            if (!localAdmitted) throw new FormatException("Host authorization must precede a snapshot.");
            var mark = OnlineSnapshotMark.Read(body["mark"]);
            if (mark.MatchId != expected.MatchId || mark.IdentityHash != authority.IdentityHash ||
                (committedMark != null && (mark.Epoch < committedMark.Epoch ||
                (mark.Epoch == committedMark.Epoch && mark.Sequence <= committedMark.Sequence))))
                throw new FormatException("Snapshot identity or frontier mismatch.");
            int count = (int)body["count"].Number(); int index = (int)body["index"].Number();
            int length = (int)body["length"].Number(); string hash = body["wireHash"].String();
            if (receivedMark == null)
            {
                receivedMark = mark; receivedChunks = new byte[count][]; receivedBytes = length; receivedWireHash = hash; incomingStarted = clock();
            }
            if (!receivedMark.Same(mark) || receivedChunks.Length != count || receivedBytes != length || receivedWireHash != hash ||
                clock() - incomingStarted >= 10000 || receivedChunks[index] != null)
                throw new FormatException("Mixed, duplicate or stale snapshot transfer.");
            receivedChunks[index] = OnlineJson.DecodeBase64Url(body["data"].String(), OnlineSnapshotPacket.ChunkBytes);
            if (receivedChunks.Any(c => c == null)) return;
            string json = OnlineSnapshotPacket.Restore(receivedChunks, length, hash);
            authority.ValidateSnapshot(json, mark); committedMark = mark;
            receivedMark = null; receivedChunks = null;
            ReplicaReceived?.Invoke(json, mark);
            SendServer(OnlineWireKind.SnapshotAck, mark.ToValue());
            TryClientBegin();
        }
        void TryClientBegin()
        {
            if (GameplayReady || !beginReceived || !localAdmitted || !localReady || committedMark == null) return;
            GameplayReady = true; gameplayObserved = true; migrating = false;
            authority.SetTransportReady(true, false); GameplayStarted?.Invoke();
        }
        void RestoreMigration(NetworkRunner runner)
        {
            if (!IsCurrent(activeRun) || activeRun.Runner != runner) return;
            carriers.Clear();
        }
        void Fail(Exception error)
        {
            if (Failure != "") return;
            if (recovery.Active && TransientRecoveryError(error))
            {
                awaitTransportDecision = false;
                FreezeRecovery();
                return;
            }
            CancelRecovery();
            Failure = error.GetType().Name;
            InvalidateRun();
            GameplayReady = false; gate?.Freeze(); authority.PauseForMigration();
            authority.SetTransportReady(false, Runner != null && Runner.IsServer);
            try
            {
                FailureCode = DiagnosticCode(error);
                FailureOrigin = string.Join(" > ", (new System.Diagnostics.StackTrace(error, false).GetFrames() ??
                    Array.Empty<System.Diagnostics.StackFrame>()).Take(4).Select(frame => frame.GetMethod())
                    .Where(method => method != null).Select(method => method.DeclaringType?.FullName + "." + method.Name));
                FailureFacts = string.Join(",", new[] { "issuedOffsetSeconds", "expiresOffsetSeconds",
                    "tokenLifetimeSeconds", "leaseRemainingMilliseconds" }.Where(key => error.Data[key] is long)
                    .Select(key => key + "=" + ((long)error.Data[key]).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
            catch { FailureCode = "diagnostic-unavailable"; FailureOrigin = ""; FailureFacts = ""; }
            Failed?.Invoke(Failure);
        }
        void FailAt(Exception error, string stage)
        {
            if (Failure != "") return;
            FailureStage = stage;
            Fail(error);
        }
        static string DiagnosticCode(Exception error) => error.Message switch
        {
            "Expired, future or unsupported token." => "token-time-or-version",
            "Refresh the match certificate." => "start-certificate-expired",
            "Match start is in the future." => "match-start-in-future",
            "Connection authorization is expired or bound to another transport." => "connection-time-or-binding",
            "Invalid JWT signature." => "invalid-signature",
            "Untrusted JWT header." => "untrusted-header",
            "Wrong JWT issuer or audience." => "wrong-issuer-or-audience",
            "Match identity mismatch." => "match-identity",
            "Transport identity does not match the admission." => "transport-identity",
            "Wrong connection secret." => "connection-secret",
            "Admission content mismatch." => "admission-content",
            "Connection ticket expired." => "connection-ticket-expired",
            "Coordinator clock is outside the supported tolerance." => "clock-outside-tolerance",
            _ => "unclassified"
        };
        public Task Stop() => stopTask ??= StopCore();
        async Task StopCore()
        {
            CancelRecovery();
            InvalidateRun(); lifetime.Cancel(); GameplayReady = false; gate?.Freeze();
            authority.SetTransportReady(false, Runner != null && Runner.IsServer);
            if (recoveryTask != null) await recoveryTask;
            if (Runner != null) await Runner.Shutdown();
        }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data)
        {
            if (!IsCurrent(activeRun) || runner != activeRun.Runner || Failure != "") return;
            try
            {
                var message = OnlineFusionWire.Decode(data);
                if (runner.IsServer)
                {
                    switch (message.Kind)
                    {
                        case OnlineWireKind.Admission:
                            _ = Admit(activeRun, player, message.Body["certificate"].String(), message.Body["grant"].String(),
                                message.Body["connectionTicket"].String(), message.Body["connectionId"].String()); break;
                        case OnlineWireKind.Ready:
                            if (message.Body["roster"].String() != expected.RosterHash) throw new FormatException("Roster mismatch.");
                            ReadyAdmittedPeer(activeRun, player);
                            if (GameplayReady) { Send(player, OnlineWireKind.Begin, Roster()); SendCommitted(player); }
                            else TryBegin();
                            break;
                        case OnlineWireKind.Command: ReceiveCommand(player, message.Body); break;
                        case OnlineWireKind.SnapshotAck:
                            var ack = OnlineSnapshotMark.Read(message.Body);
                            if (gate.Actor(Peer(player)) == null || !sentMarks.TryGetValue(player, out var sent) || !ack.Same(sent))
                                throw new FormatException("Unknown snapshot acknowledgement.");
                            sentMarks.Remove(player); ReplicaAcknowledged?.Invoke(player, ack);
                            if (!ack.Same(committedMark)) SendCommitted(player);
                            break;
                        default: throw new FormatException("Peer sent a host-only message.");
                    }
                }
                else
                {
                    if (message.Kind == OnlineWireKind.HostAvailable)
                        _ = ReceiveHostAvailable(activeRun, message.Body);
                    else if (message.Kind == OnlineWireKind.Admitted)
                    {
                        if (!activeRun.HostAvailableSeen || activeRun.HostId == null)
                            throw new FormatException("Canonical writer confirmation must precede host authorization.");
                        if (message.Body["roster"].String() != expected.RosterHash) throw new FormatException("Admission roster mismatch.");
                        var proof = verifier.VerifyHostAuthorization(message.Body["authorization"].String(), gate.Match,
                            activeRun.Join.PlayerId, activeRun.ConnectionId, clock());
                        if (activeRun.HostId != null && activeRun.HostId != proof.HostId)
                            throw new FormatException("Established host identity changed without migration.");
                        activeRun.HostId = proof.HostId;
                        localAdmitted = true;
                        if (localReady) SendServer(OnlineWireKind.Ready, Roster());
                    }
                    else if (message.Kind == OnlineWireKind.Begin)
                    {
                        if (!localAdmitted || !localReady || message.Body["roster"].String() != expected.RosterHash) throw new FormatException("Unexpected match start.");
                        beginReceived = true; TryClientBegin();
                    }
                    else if (message.Kind == OnlineWireKind.Snapshot) ReceiveSnapshot(message.Body);
                    else throw new FormatException("Server sent a client-only message.");
                }
            }
            catch (Exception error)
            {
                if (runner.IsServer && player != runner.LocalPlayer) runner.Disconnect(player);
                else Fail(error);
            }
        }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            try
            {
                if (IsCurrent(activeRun) && runner == activeRun.Runner && Failure == "" && token != null && token.Length == 43 &&
                    gate.RecognizesSecret(OnlineJson.DecodeUtf8(token))) request.Accept();
                else request.Refuse();
            }
            catch (FormatException) { request.Refuse(); }
        }
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!IsCurrent(activeRun) || runner != activeRun.Runner || !runner.IsServer) return;
            if (pending.Count >= 8) { runner.Disconnect(player); return; }
            pending[player] = checked(clock() + 15000);
            if (player != runner.LocalPlayer && activeRun.CheckpointReady) PromptAdmission(activeRun, player);
        }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!IsCurrent(activeRun) || runner != activeRun.Runner || !runner.IsServer) return;
            activeRun.Readied.Remove(player);
            pending.Remove(player); rates.Remove(player); sentMarks.Remove(player); leases.Remove(player);
            string actor = gate.Disconnect(Peer(player), clock());
            if (actor != null) authority.Disconnect(actor, clock());
        }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken token) => RecoverTransport(runner, token);
        public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
        {
            if (reason == ShutdownReason.HostMigration || !OwnsRecoveryCallback(runner)) return;
            if (gameplayObserved && TransientShutdown(reason)) RecoverTransport(runner, null);
            else if (gameplayObserved || recovery.Active) Fail(new WordboundRunnerStartException(reason));
            else { InvalidateRun(); GameplayReady = false; authority.SetTransportReady(false, runner.IsServer); }
        }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            if (ReferenceEquals(runner, retiringRunner) || !OwnsRecoveryCallback(runner)) return;
            if (gameplayObserved && (reason == NetDisconnectReason.Timeout || reason == NetDisconnectReason.ByRemote ||
                reason == NetDisconnectReason.Unknown)) RecoverTransport(runner, null, true);
            else if (gameplayObserved || recovery.Active) Fail(new WordboundRunnerStartException(ShutdownReason.ConnectionRefused));
            else { GameplayReady = false; authority.SetTransportReady(false, false); }
        }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress address, NetConnectFailedReason reason) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> list) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
#pragma warning disable CS0618
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
#pragma warning restore CS0618
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
    }
}
