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

    public sealed class WordboundFusionSession : INetworkRunnerCallbacks
    {
        sealed class RunScope
        {
            public readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
            public readonly HashSet<PlayerRef> Authorizing = new HashSet<PlayerRef>();
            public NetworkRunner Runner;
            public WordboundFusionJoin Join;
            public bool Publishing, Beginning, Renewing;
            public long RenewAt;
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
        public NetworkRunner Runner { get; private set; }
        public bool GameplayReady { get; private set; }
        public string Failure { get; private set; } = "";
        public OnlineSnapshotMark Committed => committedMark;
        public event Action GameplayStarted;
        public event Action<string, OnlineSnapshotMark> ReplicaReceived;
        public event Action<PlayerRef, OnlineSnapshotMark> ReplicaAcknowledged;
        public event Action<string> Failed;

        public WordboundFusionSession(Func<NetworkRunner> runnerFactory, FusionAppSettings appSettings,
            NetworkProjectConfig projectConfig, NetworkPrefabRef snapshotChunkPrefab,
            OnlineAdmissionVerifier admissionVerifier, OnlineMatchExpectation expectation,
            Func<CancellationToken, Task<WordboundFusionJoin>> freshCredentials,
            IWordboundFusionAuthority matchAuthority, OnlineConnectionService connectionService, Func<long> wallClock = null)
        {
            createRunner = runnerFactory ?? throw new ArgumentNullException(nameof(runnerFactory));
            app = appSettings?.GetCopy() ?? throw new ArgumentNullException(nameof(appSettings));
            config = projectConfig ?? throw new ArgumentNullException(nameof(projectConfig));
            verifier = admissionVerifier ?? throw new ArgumentNullException(nameof(admissionVerifier));
            expected = expectation ?? throw new ArgumentNullException(nameof(expectation));
            credentials = freshCredentials ?? throw new ArgumentNullException(nameof(freshCredentials));
            authority = matchAuthority ?? throw new ArgumentNullException(nameof(matchAuthority));
            connections = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            clock = wallClock ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            chunkPrefab = snapshotChunkPrefab;
            if (config.HostMigration.EnableAutoUpdate)
                throw new ArgumentException("Automatic migration snapshots must be disabled to preserve complete checkpoints.");
            if ((config.Network.ReliableDataTransferModes & NetworkConfiguration.ReliableDataTransfers.ClientToServer) == 0)
                throw new ArgumentException("Client-to-server reliable data must be enabled.");
            if ((config.Network.ReliableDataTransferModes & NetworkConfiguration.ReliableDataTransfers.ClientToClientWithServerProxy) != 0)
                throw new ArgumentException("Client-to-client reliable forwarding must be disabled.");
            app.FixedRegion = expected.Settings.Region;
            app.AppVersion = expected.Settings.ProtocolVersion;
        }

        public Task Start(CancellationToken cancellationToken = default)
        {
            if (initialStarted) throw new InvalidOperationException("A session may only start once.");
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
        RunScope BeginRun()
        {
            lifetime.Token.ThrowIfCancellationRequested();
            InvalidateRun();
            return activeRun = new RunScope();
        }
        async Task StartRunner(HostMigrationToken migration, CancellationToken cancellationToken)
        {
            var run = BeginRun();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token, run.Cancellation.Token);
            try {
            var join = await credentials(linked.Token);
            RequireCurrent(run);
            run.Join = join;
            long now = clock();
            if (join.PhotonTicket.AppId != app.AppIdFusion || join.PhotonTicket.ExpiresAtMilliseconds <= now)
                throw new InvalidOperationException("Photon authentication ticket is expired or belongs to another app.");
            var match = verifier.VerifyStart(join.Certificate, expected, now / 1000);
            verifier.VerifyAdmission(join.Grant, match, join.PlayerId, join.ConnectionSecret, now / 1000);
            if (gate == null) gate = new OnlineSessionGate(match);
            var authentication = new AuthenticationValues { AuthType = CustomAuthenticationType.Custom };
            authentication.SetAuthPostData(join.PhotonTicket.PhotonPostData());
            Runner = createRunner() ?? throw new InvalidOperationException("Runner factory returned no runner.");
            run.Runner = Runner;
            run.Runner.AddCallbacks(this);
            var result = await run.Runner.StartGame(new StartGameArgs {
                GameMode = migration?.GameMode ?? GameMode.AutoHostOrClient,
                SessionName = "wb-" + expected.MatchId + "-" + expected.Generation,
                PlayerCount = match.Roster.Count, IsVisible = false, IsOpen = true,
                EnableClientSessionCreation = false, CustomPhotonAppSettings = app,
                AuthValues = authentication, Config = config, ConnectionToken = Encoding.ASCII.GetBytes(join.ConnectionSecret),
                HostMigrationToken = migration, HostMigrationResume = RestoreMigration,
                StartGameCancellationToken = linked.Token
            });
            RequireCurrent(run);
            if (!result.Ok) throw new InvalidOperationException("Fusion startup failed: " + result.ShutdownReason);
            if (run.Runner.GetPlayerUserId(run.Runner.LocalPlayer) != join.PlayerId)
                throw new InvalidOperationException("Photon did not authenticate the coordinator identity.");
            if (connections.LocalPlayerId != join.PlayerId) throw new InvalidOperationException("Coordinator guest changed during startup.");
            if (run.Runner.IsServer) await AdmitLocal(run);
            else await SendAdmission(run);
            RequireCurrent(run);
            }
            catch (Exception error) { if (IsCurrent(run)) Fail(error); throw; }
        }
        async Task AdmitLocal(RunScope run)
        {
            RequireCurrent(run);
            if (migrating && !restored) return;
            string ticket = await connections.Ticket(run.Cancellation.Token);
            RequireCurrent(run);
            await Admit(run, run.Runner.LocalPlayer, run.Join.Certificate, run.Join.Grant, ticket);
            RequireCurrent(run);
            if (localReady) MarkLocalReady();
        }
        async Task SendAdmission(RunScope run)
        {
            string ticket = await connections.Ticket(run.Cancellation.Token);
            RequireCurrent(run);
            SendServer(OnlineWireKind.Admission, new Dictionary<string, object> {
                ["certificate"] = run.Join.Certificate, ["grant"] = run.Join.Grant, ["connectionTicket"] = ticket });
        }
        async Task Admit(RunScope run, PlayerRef player, string certificate, string grant, string ticket)
        {
            if (!IsCurrent(run) || run.Authorizing.Contains(player) || gate.Actor(Peer(player)) != null) return;
            run.Authorizing.Add(player);
            var runner = run.Runner;
            try {
            string authenticated = runner.GetPlayerUserId(player);
            string secret = player == runner.LocalPlayer ? run.Join.ConnectionSecret :
                OnlineJson.DecodeUtf8(runner.GetPlayerConnectionToken(player) ?? Array.Empty<byte>());
            long now = clock();
            var match = verifier.VerifyStart(certificate, expected, now / 1000);
            var admission = verifier.VerifyAdmission(grant, match, authenticated, secret, now / 1000);
            var lease = await connections.Authorize(match, authenticated, ticket, secret, Guid.NewGuid().ToString(), run.Cancellation.Token);
            if (!IsCurrent(run) || !runner.IsRunning || !runner.ActivePlayers.Contains(player) ||
                runner.GetPlayerUserId(player) != authenticated) return;
            now = clock();
            if (lease.ExpiresAtMilliseconds <= now) throw new InvalidOperationException("Transport authorization expired.");
            gate.Admit(admission, Peer(player), now);
            if (gate.Started && !authority.Reconnect(authenticated, now))
            {
                gate.Disconnect(Peer(player), now);
                throw new InvalidOperationException("Original seat is no longer reconnectable.");
            }
            leases[player] = lease;
            pending.Remove(player);
            if (player != Runner.LocalPlayer) Send(player, OnlineWireKind.Admitted, Roster());
            }
            catch (Exception error) {
                if (IsCurrent(run)) {
                    if (player != runner.LocalPlayer) runner.Disconnect(player);
                    else Fail(error);
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
                gate.Ready(Peer(Runner.LocalPlayer)); TryBegin();
            }
            else if (localAdmitted) SendServer(OnlineWireKind.Ready, Roster());
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
            GameplayReady = true; run.Beginning = false;
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
            if (!gate.Started && clock() >= gate.Match.StartedAtMilliseconds + 90000)
            { Fail(new TimeoutException("The complete approved roster did not become ready.")); return; }
            foreach (var peer in pending.Where(p => p.Value <= clock()).Select(p => p.Key).ToArray())
            { pending.Remove(peer); Runner.Disconnect(peer); }
            foreach (var peer in leases.Where(p => p.Value.ExpiresAtMilliseconds <= clock()).Select(p => p.Key).ToArray())
                Revoke(peer, false);
            if (!run.Renewing && clock() >= run.RenewAt) _ = RenewConnections(run);
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
            if (!IsCurrent(run) || Runner == null || !Runner.IsServer || (!GameplayReady && !bootstrap) || run.Publishing || migrating || Failure != "") return false;
            run.Publishing = true;
            var runner = run.Runner;
            try
            {
                var mark = authority.Publish(out string json);
                authority.ValidateSnapshot(json, mark);
                var packet = OnlineSnapshotPacket.Compress(json);
                foreach (var carrier in carriers) if (carrier != null && carrier.Object != null) runner.Despawn(carrier.Object);
                carriers.Clear();
                for (int index = 0; index < packet.ChunkCount; index++)
                {
                    int captured = index;
                    var obj = runner.Spawn(chunkPrefab, onBeforeSpawned: (_, value) =>
                        value.GetComponent<WordboundFusionSnapshotChunk>().Write(mark, packet, captured));
                    carriers.Add(obj.GetComponent<WordboundFusionSnapshotChunk>());
                }
                int writtenTick = runner.Tick.Raw;
                long waitDeadline = checked(clock() + 10000);
                while (IsCurrent(run) && runner.IsRunning && runner.Tick.Raw <= writtenTick)
                {
                    cancellationToken.ThrowIfCancellationRequested(); run.Cancellation.Token.ThrowIfCancellationRequested();
                    if (clock() >= waitDeadline) throw new TimeoutException("Migration carrier did not reach a simulation tick.");
                    await Task.Yield();
                }
                if (!IsCurrent(run) || migrating || !runner.IsRunning) return false;
                bool pushed = await runner.PushHostMigrationSnapshot();
                if (!pushed || !IsCurrent(run) || migrating || !runner.IsRunning) return false;
                if (!authority.AcknowledgeDurable(mark)) throw new InvalidOperationException("Durable checkpoint did not match its publication.");
                committedMark = mark; committedPacket = packet;
                foreach (var player in runner.ActivePlayers)
                {
                    if (player == runner.LocalPlayer || gate.Actor(Peer(player)) == null) continue;
                    SendCommitted(player);
                }
                return true;
            }
            catch (Exception error) { if (IsCurrent(run)) Fail(error); return false; }
            finally { run.Publishing = false; }
        }
        void SendCommitted(PlayerRef player)
        {
            if (committedPacket == null || committedMark == null || sentMarks.ContainsKey(player)) return;
            var packet = committedPacket; var mark = committedMark; sentMarks[player] = mark;
            for (int index = 0; index < packet.ChunkCount; index++)
                Send(player, OnlineWireKind.Snapshot, new Dictionary<string, object> {
                    ["mark"] = mark.ToValue(), ["index"] = index, ["count"] = packet.ChunkCount, ["length"] = packet.Length,
                    ["wireHash"] = packet.WireHash, ["data"] = OnlineJson.Base64Url(packet.Chunk(index)) });
        }
        void ReceiveSnapshot(JsonNode body)
        {
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
            GameplayReady = true; migrating = false; authority.SetTransportReady(true, false); GameplayStarted?.Invoke();
        }
        void RestoreMigration(NetworkRunner runner)
        {
            if (!IsCurrent(activeRun) || activeRun.Runner != runner) return;
            try
            {
                var chunks = runner.GetResumeSnapshotNetworkObjects()
                    .Select(obj => obj.GetComponent<WordboundFusionSnapshotChunk>()).Where(c => c != null).ToArray();
                string json = WordboundFusionSnapshotChunk.Restore(chunks, out var mark);
                if (mark.MatchId != expected.MatchId || mark.IdentityHash != authority.IdentityHash)
                    throw new FormatException("Migration binding mismatch.");
                authority.ValidateSnapshot(json, mark);
                authority.RestoreCommitted(json, mark, checked(mark.Epoch + 1), clock());
                gate.RestoreForMigration(authority.Presence(), clock());
                carriers.Clear(); committedMark = mark; committedPacket = OnlineSnapshotPacket.Compress(json); restored = true;
            }
            catch (Exception error) { Fail(error); throw; }
        }
        void Fail(Exception error)
        {
            if (Failure != "") return;
            Failure = error.GetType().Name;
            InvalidateRun();
            GameplayReady = false; gate?.Freeze(); authority.PauseForMigration();
            authority.SetTransportReady(false, Runner != null && Runner.IsServer);
            Failed?.Invoke(Failure);
        }
        public async Task Stop()
        {
            InvalidateRun(); lifetime.Cancel(); GameplayReady = false; gate?.Freeze();
            authority.SetTransportReady(false, Runner != null && Runner.IsServer);
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
                                message.Body["connectionTicket"].String()); break;
                        case OnlineWireKind.Ready:
                            if (message.Body["roster"].String() != expected.RosterHash) throw new FormatException("Roster mismatch.");
                            gate.Ready(Peer(player));
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
                    if (message.Kind == OnlineWireKind.Admitted)
                    {
                        if (message.Body["roster"].String() != expected.RosterHash) throw new FormatException("Admission roster mismatch.");
                        localAdmitted = true;
                        if (localReady) SendServer(OnlineWireKind.Ready, Roster());
                    }
                    else if (message.Kind == OnlineWireKind.Begin)
                    {
                        if (!localReady || message.Body["roster"].String() != expected.RosterHash) throw new FormatException("Unexpected match start.");
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
        }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!IsCurrent(activeRun) || runner != activeRun.Runner || !runner.IsServer) return;
            pending.Remove(player); rates.Remove(player); sentMarks.Remove(player); leases.Remove(player);
            string actor = gate.Disconnect(Peer(player), clock());
            if (actor != null) authority.Disconnect(actor, clock());
        }
        public async void OnHostMigration(NetworkRunner runner, HostMigrationToken token)
        {
            if (!IsCurrent(activeRun) || runner != activeRun.Runner || migrating || Failure != "") return;
            long recoveryGeneration = InvalidateRun();
            migrating = true; restored = false; localAdmitted = false; GameplayReady = false; gate.Freeze(); authority.PauseForMigration();
            authority.SetTransportReady(false, runner.IsServer); beginReceived = false; committedMark = null; committedPacket = null;
            receivedMark = null; receivedChunks = null; pending.Clear(); rates.Clear(); sentMarks.Clear(); leases.Clear();
            try
            {
                await runner.Shutdown(false, ShutdownReason.HostMigration);
                if (runGeneration != recoveryGeneration || lifetime.IsCancellationRequested) return;
                UnityEngine.Object.Destroy(runner.gameObject);
                await StartRunner(token, lifetime.Token);
            }
            catch (Exception error) { if (runGeneration == recoveryGeneration && !lifetime.IsCancellationRequested) Fail(error); }
        }
        public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
        {
            if (IsCurrent(activeRun) && runner == activeRun.Runner && reason != ShutdownReason.HostMigration)
            { InvalidateRun(); GameplayReady = false; authority.SetTransportReady(false, runner.IsServer); }
        }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        { if (IsCurrent(activeRun) && runner == activeRun.Runner) { GameplayReady = false; authority.SetTransportReady(false, false); } }
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
