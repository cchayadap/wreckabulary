using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;
using Assert = NUnit.Framework.Assert;

namespace Wreckabulary.OnlineGameplayTests
{
    public sealed partial class WordboundNativeGameplayTests
    {
        const string Gate = "WORDBOUND_EXTERNAL_GAMEPLAY";
        const string Prefix = "WordboundNativeGameplay-";
        const string PrefabFolder = "Assets/_Project/Prefabs/Wordbound/Network/";
        const string ProofScene = "Assets/_Project/Tests/Photon/WordboundPhotonProof.unity";
        const string PriorScenesKey = "Wreckabulary.NativeGameplay.PriorScenes";
        const string PlayStartedKey = "Wreckabulary.NativeGameplay.PlayStarted";
        readonly List<Peer> peers = new List<Peer>();
        readonly List<Task> pending = new List<Task>();
        WordboundLiveMatch live;
        CancellationTokenSource lifetime;
        WordboundNativeProfiler profiler;
        WordboundNativePhases phases;
        WordboundNativeTickBatches tickBatches;
        WordboundGameplayPrefabs prefabs;
        NetworkPrefabRef carrier;
        int sceneIndex;

        sealed class Peer
        {
            public WordboundGameplayController Controller;
            public WordboundFusionSession Session;
            public string Actor;
            public bool Move;
            public WordboundMovementInput InteractionInput;
            public Func<WordboundMovementInput> ScriptedMovement;
            public int Advanced;
            public int PhysicalSteps, PhysicalObservations, MovementQueries;
            public int RunnerCreations, CredentialRequests;
            public int InputReads, MovingInputReads;
            public Func<CancellationToken, Task> BeforeJoin;
            public readonly Dictionary<long, OnlineSnapshotMark> Received = new Dictionary<long, OnlineSnapshotMark>();
            public readonly Dictionary<string, OnlineSnapshotMark> Acknowledged = new Dictionary<string, OnlineSnapshotMark>();
            public readonly Dictionary<string, int> CommandObservations = new Dictionary<string, int>();
            public NetworkRunner Runner => Session.Runner;
        }

        [UnityTest, Category("ExternalPhotonGameplay"), Timeout(600000)]
        public IEnumerator TwoAuthenticatedPeersComposeCanonicalBodiesAndCommitClientCommands() => Run(2, false);

        [UnityTest, Category("ExternalPhotonGameplay"), Timeout(600000)]
        public IEnumerator FourAuthenticatedPeersComposeCanonicalBodiesAndCommitClientCommands() => Run(4, false);

        [UnityTest, Category("ExternalPhotonGameplay"), Timeout(600000)]
        public IEnumerator EightAuthenticatedPeersComposeCanonicalBodiesAndCommitClientCommands() => Run(8, false);

        [UnityTest, Category("ExternalPhotonGameplay"), Timeout(600000)]
        public IEnumerator AuthenticatedGameplayRestoresAfterHostDeparture() => Run(2, true);

        [UnityTest, Category("ExternalPhotonGameplay"), Timeout(600000)]
        public IEnumerator EightAuthenticatedPeersComposeFlagshipTeamRaceSquad() =>
            Run(8, false, productionMode: "TeamRace", productionFormat: RaceFormat.Squad);

        [UnityTest, Category("ExternalPhotonGameplay"), Timeout(600000)]
        public IEnumerator FourAuthenticatedPeersRestoreFlagshipBrawlDuoAfterHostDeparture() =>
            Run(4, true, productionMode: "BrawlRace", productionFormat: RaceFormat.Duo);

        IEnumerator Run(int count, bool migrate, bool interactions = false,
            string productionMode = null, RaceFormat productionFormat = RaceFormat.Solo,
            WordboundPublicCourseCatalog publicCatalogue = null, bool hudInteractions = false, bool authoredWorkshop = false,
            RecoveryEnding recoveryEnding = RecoveryEnding.None, bool migrationPreflightFailure = false,
            Func<string, WordboundClientContent> authoredContentFactory = null, Func<Peer, IEnumerator> afterStarted = null)
        {
            if (authoredContentFactory != null && (!authoredWorkshop || productionMode != null || publicCatalogue != null))
                throw new ArgumentException("A custom authored fixture must use only the private workshop route.");
            if (Environment.GetEnvironmentVariable(Gate) != "1")
                Assert.Ignore("Set WORDBOUND_EXTERNAL_GAMEPLAY=1 after configuring production Photon custom authentication.");
            Assert.IsFalse(Application.isPlaying, "Run this gated Editor fixture outside Play Mode.");
            prefabs = new WordboundGameplayPrefabs(Load("WordboundPlayerBody"), Load("WordboundItemBody"), Load("WordboundPlatformBody"));
            Load("WordboundSnapshotChunk");
            carrier = NetworkPrefabRef.Parse(AssetDatabase.AssetPathToGUID(PrefabFolder + "WordboundSnapshotChunk.prefab"));
            sceneIndex = PrepareScene();
            SessionState.SetBool(PlayStartedKey, true);
            yield return new EnterPlayMode();
            Assert.IsTrue(Application.isPlaying, "The native gameplay fixture must enter Play Mode before creating guests or Runners.");
            lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(8));
            profiler = WordboundNativeProfiler.StartIfEnabled();
            phases = WordboundNativePhases.StartIfEnabled();
            tickBatches = WordboundNativeTickBatches.StartIfEnabled();
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            WordboundClientContent production = authoredWorkshop ? (authoredContentFactory ?? CreateAuthoredWorkshop)(projectRoot) : null;
            if (productionMode != null)
            {
                string folder = Path.Combine(projectRoot, "Assets/_Project/Data/Config");
                production = new WordboundClientContent(File.ReadAllText(Path.Combine(folder, "items.json")),
                    File.ReadAllText(Path.Combine(folder, "rules.json")), productionMode, productionFormat.ToString(),
                    "0123456789abcdef");
            }
            live = new WordboundLiveMatch(projectRoot, hudInteractions ? HudCourse() : interactions ? WordboundInteractionCourse.Create() : null, production, publicCatalogue);
            yield return Wait(live.Prepare(count, lifetime.Token), "Prepare real " + (live.PublicQueue ? "public" : "private") + " coordinator match", 120);
            if (live.PublicQueue)
            {
                Assert.AreEqual(publicCatalogue.MapPoolVersion, live.ApprovedRelease);
                Assert.IsTrue(live.Settings.MapPoolVersion.StartsWith("pub-", StringComparison.Ordinal));
                Assert.AreNotEqual(live.ApprovedRelease, live.Settings.MapPoolVersion,
                    "The disposable public queue check must not join the populated release pool.");
                Assert.AreEqual("BuiltIn", live.Verified.Match.Content.Kind);
                Debug.Log("WORDBOUND_PUBLIC_ENTRY_OBSERVED: exact approved release " + live.ApprovedRelease +
                    "; isolated pool " + live.Settings.MapPoolVersion + "; whole-party certificate verified.");
            }
            int hostIndex = live.Clients.FindIndex(c => c.Credentials.PlayerId == live.Verified.Roster[0].PlayerId);
            Assert.GreaterOrEqual(hostIndex, 0);
            var first = CreatePeer(hostIndex);
            yield return Wait(first.Session.Start(lifetime.Token), "Authenticate and start first Runner", 65);
            Assert.IsTrue(first.Runner.IsServer, "The first private participant must own the new host session.");
            for (int index = 0; index < count; index++)
            {
                if (index == hostIndex) continue;
                var peer = CreatePeer(index);
                yield return Wait(peer.Session.Start(lifetime.Token), "Authenticate and start client Runner", 65);
            }
            var second = peers[1];
            yield return Until(() => peers.All(Ready), "Prepare every scene and durable gameplay start", 65);
            phases?.Ready(first.Controller.Frame);
            AssertIdentities(first);
            AssertScenes(first);
            yield return Until(() => peers.All(ExactBodies), "Replicate every canonical body exactly once", 25);
            foreach (var peer in peers)
            {
                AssertBodies(peer, peer == first);
                Assert.IsFalse(peer.Controller.IsSimulationPaused);
                if (peer == first) continue;
                Assert.IsFalse(peer.Runner.IsServer);
                Assert.AreEqual(0, peer.Controller.Authority.Match.Tick, "A replica must not advance its local authoritative domain.");
                Assert.Throws<InvalidOperationException>(() => peer.Controller.Authority.Advance(WordboundLiveMatch.Now()));
            }

            var initial = first.Controller.Frame.Bodies.Single(b => b.Kind == WorldBodyKind.Player && b.SourceId == second.Actor).Position;
            second.Move = true;
            yield return Until(() => first.Controller.Frame.Bodies.Single(b => b.Kind == WorldBodyKind.Player &&
                b.SourceId == second.Actor).Position.Z > initial.Z + .25f, "Apply client movement through admitted Fusion input", 10);
            second.Move = false;
            var command = second.Controller.Submit(WordboundAction.CancelCraft);
            yield return Until(() => first.Controller.Frame.Commands.Frontiers[second.Actor] == command.Sequence,
                "Apply authenticated client command on host", 15);
            var receipt = first.Controller.Frame.Commands.Receipts.Single(r => r.ActorId == second.Actor && r.Sequence == command.Sequence);
            Assert.AreEqual(command.PayloadHash, receipt.PayloadHash);
            Assert.AreEqual(0, second.Controller.Authority.Match.Tick);
            second.Session.SubmitCommand(WordboundGameplayPlan.Command(command));
            yield return Until(() => first.CommandObservations.TryGetValue(Key(second.Actor, command.Sequence), out int seen) && seen >= 2 &&
                peers.Skip(1).All(peer => peer.Controller.Frame.Commands.Frontiers[second.Actor] == command.Sequence &&
                peer.Received.Values.Any(mark => mark.Tick >= receipt.Tick &&
                    first.Acknowledged.TryGetValue(Key(peer.Actor, mark.Sequence), out var ack) && ack.Same(mark))),
                "Commit and acknowledge the exact command-bearing snapshot", 25);
            Assert.AreEqual(1, first.Controller.Frame.Commands.Receipts.Count(r => r.ActorId == second.Actor && r.Sequence == command.Sequence),
                "The repeated command must retain one receipt.");
            Assert.Greater(first.Advanced, 0);
            foreach (var peer in peers.Skip(1))
            {
                Assert.AreEqual(0, peer.Advanced, "Client composition must never emit an authoritative tick.");
                Assert.AreEqual(0, peer.Controller.Authority.Match.Tick);
            }
            foreach (var peer in peers) AssertBodies(peer, peer == first);
            if (production != null && authoredContentFactory == null) AssertProductionContent(first, peers);
            if (authoredWorkshop) AssertDownloadedWorkshop();
            Debug.Log("WORDBOUND_GAMEPLAY_OBSERVED: authenticated real bodies, client input and committed command; cleanup pending.");

            if (hudInteractions) yield return HudInteractions(first);
            if (interactions) yield return Interactions(first);
            if (recoveryEnding != RecoveryEnding.None) yield return Reconnect(first, second, recoveryEnding);
            if (afterStarted != null) yield return afterStarted(first);

            if (migrate)
            {
                long oldEpoch = second.Controller.Frame.Epoch;
                var survivors = peers.Skip(1).ToArray();
                var originalRunners = survivors.ToDictionary(peer => peer, peer => peer.Runner);
                var physicalFrontier = production == null ? null : second.Controller.Frame.Mechanisms
                    .Where(mechanism => mechanism.Physical.MotionUnits > 0)
                    .ToDictionary(MechanismKey, mechanism => mechanism.Physical.MotionUnits);
                if (production != null) Assert.IsNotEmpty(physicalFrontier,
                    "Recovery must preserve a real committed moving-fixture phase, not only an empty physical ledger.");
                var firstRestoredFrames = new Dictionary<Peer, MatchSnapshot>();
                var advancesAtFirstRestore = new Dictionary<Peer, int>();
                foreach (var survivor in survivors)
                    survivor.Controller.FrameChanged += frame => {
                        if (firstRestoredFrames.ContainsKey(survivor) || survivor.Runner == null ||
                            survivor.Runner == originalRunners[survivor] || !survivor.Runner.IsServer ||
                            frame.Epoch != oldEpoch + 1) return;
                        firstRestoredFrames.Add(survivor, frame);
                        advancesAtFirstRestore.Add(survivor, survivor.Advanced);
                    };
                Peer replacement = null;
                using var timing = new RecoveryTiming(survivors, oldEpoch + 1, migrationPreflightFailure ?
                    "injected-disconnect-notification-then-graceful-host-stop-with-credential-503" : "graceful-host-controller-stop");
                if (migrationPreflightFailure) yield return DelayMigrationWithFailedPreflight(survivors, originalRunners);
                timing.MarkHostStopRequested();
                yield return Wait(first.Controller.Stop(), "Depart original host", 25);
                yield return Until(() =>
                {
                    var elected = survivors.Where(peer => peer.Runner != null &&
                        peer.Runner != originalRunners[peer] && peer.Runner.IsServer && peer.Runner.IsRunning).ToArray();
                    replacement = elected.Length == 1 ? elected[0] : null;
                    return replacement != null && survivors.All(peer => peer.Runner != originalRunners[peer] &&
                        Ready(peer) && peer.Controller.Frame.Epoch == oldEpoch + 1);
                }, "Restore authenticated gameplay on the elected replacement host and every survivor", 75,
                    allowStoppedFirst: true);
                timing.MarkReadyObserved();
                Assert.IsTrue(firstRestoredFrames.TryGetValue(replacement, out var firstRestored),
                    "Capture the replacement host's first reconciled frame before gameplay advances.");
                Assert.AreEqual(0, advancesAtFirstRestore[replacement],
                    "The initial restored frame must precede the replacement host's first authoritative advance.");
                if (physicalFrontier != null)
                    foreach (var phase in physicalFrontier)
                        Assert.GreaterOrEqual(firstRestored.Mechanisms.Single(m => MechanismKey(m) == phase.Key)
                            .Physical.MotionUnits, phase.Value,
                            "The first restored physical motion phase must retain its committed frontier before time can advance.");
                yield return Until(() => survivors.All(ExactBodies), "Reconcile every surviving peer's complete restored body ledger",
                    25, allowStoppedFirst: true);
                foreach (var peer in survivors)
                {
                    AssertBodies(peer, peer == replacement);
                    Assert.AreEqual(survivors.Length, peer.Runner.ActivePlayers.Count());
                    CollectionAssert.AreEquivalent(survivors.Select(p => p.Actor),
                        peer.Runner.ActivePlayers.Select(peer.Runner.GetPlayerUserId));
                    Assert.IsTrue(peer.Controller.Frame.Progress.Players.Where(p => p.Id != first.Actor).All(p => p.Connected));
                    Assert.IsFalse(peer.Controller.Frame.Progress.Players.Single(p => p.Id == first.Actor).Connected);
                    Assert.AreEqual(command.Sequence, peer.Controller.Frame.Commands.Frontiers[second.Actor]);
                    Assert.AreEqual(1, peer.Controller.Frame.Commands.Receipts.Count(r =>
                        r.ActorId == second.Actor && r.Sequence == command.Sequence && r.PayloadHash == command.PayloadHash));
                }
                long resumedTick = replacement.Controller.Frame.Progress.Tick;
                yield return Until(() => replacement.Controller.Frame.Progress.Tick > resumedTick + 5,
                    "Advance restored host after canonical body application", 10, allowStoppedFirst: true);
                Assert.AreEqual(1, peers.Count(p => p.Runner != null && p.Runner.IsRunning && p.Runner.IsServer));
                if (production != null)
                {
                    AssertProductionContent(replacement, survivors);
                    var resumedClient = survivors.First(peer => peer != replacement);
                    var resumedCommand = resumedClient.Controller.Submit(WordboundAction.CancelCraft);
                    yield return Until(() => replacement.Controller.Frame.Commands.Frontiers[resumedClient.Actor] == resumedCommand.Sequence &&
                        survivors.Where(peer => peer != replacement).All(peer =>
                            peer.Controller.Frame.Commands.Receipts.Any(r => r.ActorId == resumedClient.Actor &&
                                r.Sequence == resumedCommand.Sequence && r.PayloadHash == resumedCommand.PayloadHash) &&
                            peer.Received.Values.Any(mark => mark.Epoch == oldEpoch + 1 &&
                                mark.Tick >= peer.Controller.Frame.Commands.Receipts.Single(r => r.ActorId == resumedClient.Actor &&
                                    r.Sequence == resumedCommand.Sequence).Tick &&
                                replacement.Acknowledged.TryGetValue(Key(peer.Actor, mark.Sequence), out var ack) && ack.Same(mark))),
                        "Commit a fresh admitted client command through the elected host", 25, allowStoppedFirst: true);
                    foreach (var peer in survivors.Where(peer => peer != replacement))
                    {
                        Assert.AreEqual(0, peer.Advanced, "Surviving replicas must not advance an authority.");
                        Assert.AreEqual(0, peer.Controller.Authority.Match.Tick);
                    }
                }
                Debug.Log("WORDBOUND_GAMEPLAY_OBSERVED: authenticated host replacement restored committed command and canonical bodies; cleanup pending.");
            }
        }

        Peer CreatePeer(int index)
        {
            var peer = new Peer { Actor = live.Clients[index].Credentials.PlayerId };
            var root = new GameObject(Prefix + "Controller-" + index);
            peer.Controller = root.AddComponent<WordboundGameplayController>();
            var options = new WordboundGameplayOptions {
                GameplayScene = SceneRef.FromIndex(sceneIndex), CheckpointMilliseconds = 500,
                ReadMovement = () => {
                    peer.InputReads++;
                    var input = peer.Move ? new WordboundMovementInput { MoveZ = 1 } :
                        peer.ScriptedMovement != null ? peer.ScriptedMovement() : peer.InteractionInput;
                    if (input.MoveX != 0 || input.MoveZ != 0) peer.MovingInputReads++;
                    return input;
                }
            };
            var content = live.ContentFor(index);
            peer.Controller.Initialize(live.Verified, content?.Mode ?? live.Mode, content?.Course ?? live.Course,
                content?.Geometry ?? live.Geometry, content?.Items ?? live.Items, content?.Rules ?? live.Rules, peer.Actor, prefabs,
                options);
            var physical = content?.PhysicalPuzzles ?? live.PhysicalPuzzles;
            if (physical != null)
            {
                physical.Validate(peer.Controller.Authority.Match.Binding, content?.Rules ?? live.Rules);
                var puzzles = new WordboundPuzzleRuntime(peer.Controller, physical);
                options.BeforeHostPhysics = (frame, bodies) => { puzzles.BeforeHostPhysics(frame, bodies); peer.PhysicalSteps++; };
                options.ObserveHost = (frame, poses) => { var observations = puzzles.ObserveHost(frame, poses); peer.PhysicalObservations++; return observations; };
                options.PuzzleEvaluator = puzzles.Evaluate;
                options.MovementEnvironment = actor => { var environment = puzzles.EvaluateMovementEnvironment(actor); peer.MovementQueries++; return environment; };
            }
            peer.Session = new WordboundFusionSession(() => {
                Assert.IsTrue(peer.Runner == null || !peer.Runner.IsRunning,
                    "A replacement Runner must not overlap the original transport.");
                peer.RunnerCreations++;
                var runnerRoot = new GameObject(Prefix + "Runner-" + index + "-" + Guid.NewGuid().ToString("N"));
                var runner = runnerRoot.AddComponent<NetworkRunner>();
                runnerRoot.AddComponent<NetworkSceneManagerDefault>();
                tickBatches?.Attach(runner, index, peer.RunnerCreations);
                return runner;
            }, new FusionAppSettings { AppIdFusion = WordboundLiveMatch.AppId, UseNameServer = true },
                Configuration(), carrier, live.Verifier, live.Expected, async ct => {
                    peer.CredentialRequests++;
                    if (peer.BeforeJoin != null) await peer.BeforeJoin(ct);
                    ct.ThrowIfCancellationRequested();
                    return await live.FreshJoin(index, ct);
                },
                peer.Controller.Authority, live.Connections(index),
                new OnlineCheckpointService(live.Clients[index], live.Expected, peer.Controller.Authority.IdentityHash));
            peer.Controller.AttachSession(peer.Session);
            peer.Controller.Faulted += reason => Debug.Log("WORDBOUND_GAMEPLAY_FAULT: peer " + index + " " + reason);
            peer.Session.Failed += reason => Debug.Log("WORDBOUND_SESSION_FAULT: peer " + index + " " + reason +
                " stage=" + peer.Session.FailureStage + " code=" + peer.Session.FailureCode +
                " facts=" + peer.Session.FailureFacts + " origin=" + peer.Session.FailureOrigin);
            peer.Controller.HostAdvanced += result => {
                if (!result.Advanced) return;
                peer.Advanced++;
                foreach (var receipt in result.Receipts)
                {
                    string id = Key(receipt.ActorId, receipt.Sequence);
                    peer.CommandObservations.TryGetValue(id, out int observed);
                    peer.CommandObservations[id] = observed + 1;
                }
            };
            peer.Session.ReplicaReceived += (json, mark) => {
                Assert.AreEqual(mark.StateHash, MatchSnapshotCodec.ComputeHash(peer.Controller.Frame),
                    "Gameplay must apply the exact verified replica before observers receive it.");
                peer.Received[mark.Sequence] = mark;
            };
            peer.Session.ReplicaAcknowledged += (sender, mark) =>
                peer.Acknowledged[Key(peer.Runner.GetPlayerUserId(sender), mark.Sequence)] = mark;
            peers.Add(peer);
            return peer;
        }

        static NetworkProjectConfig Configuration()
        {
            var config = new NetworkProjectConfig { PeerMode = NetworkProjectConfig.PeerModes.Multiple };
            foreach (var source in NetworkProjectConfig.Global.PrefabTable.Prefabs) config.PrefabTable.AddSource(source);
            config.HostMigration.EnableAutoUpdate = false;
            config.Network.ReliableDataTransferModes = NetworkConfiguration.ReliableDataTransfers.ClientToServer;
            config.Simulation.TickRateSelection = new TickRate.Selection {
                Client = 60, ClientSendInterval = 1, ServerTickInterval = 1, ServerSendInterval = 1
            };
            Assert.AreEqual(TickRate.ValidateResult.Ok, config.Simulation.TickRateSelection.Validate());
            return config;
        }
        static bool Ready(Peer peer) => peer.Runner != null && peer.Runner.IsRunning &&
            peer.Session.GameplayReady && peer.Controller.Frame != null &&
            peer.Controller.CourseWorld != null && (peer.Runner.IsServer ? peer.Controller.PhysicsWorld != null : true) &&
            !peer.Controller.IsSimulationPaused;
        static string Key(string actor, long sequence) => actor + "/" + sequence;
        static string MechanismKey(MatchMechanismState state) => state.Team + "/" + state.Lap + "/" + state.PuzzleId;
        static WordboundNetworkBody[] Bodies(Peer peer) => peer.Runner.GetAllNetworkObjects()
            .Select(obj => obj.GetComponent<WordboundNetworkBody>()).Where(body => body != null).ToArray();
        static bool ExactBodies(Peer peer)
        {
            if (peer.Controller.Frame == null) return false;
            var ids = Bodies(peer).Select(b => b.BodyId.ToString()).ToArray();
            return ids.Length == peer.Controller.Frame.Bodies.Count && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length &&
                new HashSet<string>(ids).SetEquals(peer.Controller.Frame.Bodies.Select(b => b.Id));
        }
        static void AssertBodies(Peer peer, bool host)
        {
            Assert.IsTrue(ExactBodies(peer), "Native network bodies must cover the exact canonical ID set.");
            foreach (var body in Bodies(peer))
            {
                var canonical = peer.Controller.Frame.Bodies.Single(b => b.Id == body.BodyId.ToString());
                Assert.AreEqual(host, body.Object.HasStateAuthority);
                Assert.AreEqual((int)canonical.Kind, body.BodyKind);
                Assert.AreEqual(canonical.SourceId, body.SourceId.ToString());
                Assert.AreEqual(canonical.OwnerTeam, body.OwnerTeam);
                if (host && canonical.Kind == WorldBodyKind.Player && canonical.Active)
                {
                    Assert.IsTrue(peer.Session.TryGetAdmittedActor(body.Object.InputAuthority, out var actor));
                    Assert.AreEqual(canonical.SourceId, actor);
                }
                else if (canonical.Kind != WorldBodyKind.Player)
                    Assert.AreEqual(PlayerRef.None, body.Object.InputAuthority);
            }
            if (host) CollectionAssert.AreEquivalent(peer.Controller.Frame.Bodies.Select(b => b.Id), peer.Controller.Bodies.Keys);
        }
        void AssertIdentities(Peer host)
        {
            CollectionAssert.AreEquivalent(peers.Select(p => p.Actor), host.Runner.ActivePlayers.Select(host.Runner.GetPlayerUserId));
            foreach (var peer in peers)
            {
                Assert.AreEqual(peers.Count, peer.Runner.ActivePlayers.Count());
                Assert.AreEqual(peer.Actor, peer.Runner.GetPlayerUserId(peer.Runner.LocalPlayer));
            }
        }
        void AssertScenes(Peer host)
        {
            var first = host.Controller.CourseWorld.gameObject.scene.GetPhysicsScene();
            var scenes = peers.Select(peer => peer.Controller.CourseWorld.gameObject.scene.GetPhysicsScene()).ToArray();
            Assert.IsTrue(scenes.All(scene => scene.IsValid() && scene != UnityEngine.Physics.defaultPhysicsScene) &&
                scenes.Distinct().Count() == peers.Count, "Each peer needs its own isolated 3D physics scene.");
            Assert.AreEqual(first, host.Controller.PhysicsWorld.PhysicsScene);
            Assert.IsTrue(peers.All(peer => peer.Controller.CourseWorld.Surfaces.Count > 0));
        }
        void AssertProductionContent(Peer host, IEnumerable<Peer> active)
        {
            Assert.IsNotNull(live.ProductionContent);
            Assert.IsTrue(live.ProductionContent.Matches(live.Verified.Match.Content));
            Assert.AreEqual(live.Mode.Format.ToString(), live.Settings.Format);
            foreach (var peer in active) {
                Assert.AreEqual(live.Course.Hash, peer.Controller.Frame.Binding.ManifestHash);
                Assert.AreEqual(live.Content.CatalogHash, peer.Controller.Frame.Binding.CatalogueHash);
                Assert.AreEqual(live.Mode.Hash, peer.Controller.Frame.Binding.ModeHash);
                Assert.LessOrEqual(peer.Controller.Frame.Bodies.Count, WordboundEntityBudget.MaximumBodies);
            }
            if (live.Mode.Id == "Freestyle") {
                Assert.AreEqual(0, live.Mode.TimeLimitSeconds); Assert.IsFalse(live.Mode.EnemyDamage);
                Assert.IsFalse(live.Mode.HazardDamage); Assert.AreEqual(WordboundObjective.Explore, live.Mode.Objective);
                Assert.IsTrue(active.All(peer => peer.Controller.Frame.Progress.Result == null));
                return;
            }
            if (!live.Mode.IsRace) return;
            Assert.Greater(live.Course.PhysicalSites.Count, 0);
            Assert.Greater(host.PhysicalSteps, 0, "The actual production fixture-motion callback must run.");
            Assert.Greater(host.PhysicalObservations, 0, "The actual production host-observation callback must run.");
            Assert.Greater(host.MovementQueries, 0, "Movement must use the actual production environment callback.");
            Assert.IsTrue(host.Controller.Frame.Mechanisms.Any(m => m.Physical.MotionUnits > 0),
                "Real authored moving machinery must advance its canonical phase.");
            var expectedFixtures = live.Course.PhysicalSites.SelectMany(site => site.Fixtures)
                .SelectMany(fixture => Enumerable.Range(0, live.Mode.TeamCount)
                    .Select(team => CourseFixtureMotion.BodyId(fixture.Id, team))).ToArray();
            Assert.Greater(expectedFixtures.Length, 0, "The flagship must contain its authored moving fixtures.");
            foreach (var peer in active)
            {
                var frame = peer.Controller.Frame;
                Assert.AreEqual(live.Course.Hash, frame.Binding.ManifestHash);
                Assert.AreEqual(live.Course.SourceHash, frame.Binding.CourseHash);
                Assert.AreEqual(live.Mode.Hash, frame.Binding.ModeHash);
                Assert.AreEqual(live.Content.CatalogHash, frame.Binding.CatalogueHash);
                Assert.AreEqual(live.Expected.RosterHash, frame.Binding.RosterHash);
                Assert.AreEqual(live.Course.Hash, peer.Controller.CourseWorld.ManifestHash);
                Assert.AreEqual(live.Course.Puzzles.Count * live.Mode.TeamCount * 3, frame.Mechanisms.Count);
                Assert.LessOrEqual(frame.Bodies.Count, WordboundEntityBudget.MaximumBodies);
                foreach (string id in expectedFixtures)
                {
                    var canonical = frame.Bodies.Single(body => body.Id == id);
                    Assert.AreEqual(WorldBodyKind.Platform, canonical.Kind);
                    Assert.IsTrue(canonical.Active && canonical.Kinematic && canonical.Protected);
                    Assert.AreEqual(1, Bodies(peer).Count(body => body.BodyId.ToString() == id));
                }
            }
        }
        void Healthy(bool allowStoppedFirst = false)
        {
            foreach (var peer in allowStoppedFirst ? peers.Skip(1) : peers)
            {
                Assert.AreEqual("", peer.Session.Failure, "The actual session failed.");
                Assert.AreEqual("", peer.Controller.Fault, "The actual gameplay composition failed.");
            }
        }
        IEnumerator Until(Func<bool> complete, string operation, double seconds, bool allowStoppedFirst = false)
        {
            double started = Time.realtimeSinceStartupAsDouble, previous = started, maximumGap = 0;
            double deadline = started + seconds;
            ObserveOperation(operation, "begin", started, 0);
            try
            {
                while (!complete() && Time.realtimeSinceStartupAsDouble < deadline && !lifetime.IsCancellationRequested)
                {
                    Healthy(allowStoppedFirst);
                    yield return null;
                    double now = Time.realtimeSinceStartupAsDouble;
                    maximumGap = Math.Max(maximumGap, now - previous); previous = now;
                }
            }
            finally { ObserveOperation(operation, "end", started, maximumGap); }
            Healthy(allowStoppedFirst);
            Assert.IsTrue(complete(), operation + " exceeded its deadline.");
        }
        void ObserveOperation(string operation, string phase, double started, double maximumGap)
        {
            try
            {
                Debug.Log(FormattableString.Invariant($"WORDBOUND_GAMEPLAY_OPERATION: {operation}; phase={phase}; elapsed={Time.realtimeSinceStartupAsDouble - started:F3}; maxFrameGap={maximumGap:F3}; lifetimeCancelled={lifetime.IsCancellationRequested}; ") + string.Join("; ", peers.Select((peer, index) =>
                    DescribePeer(peer, index) + (phase == "end" ? DescribeReadiness(peer) : ""))));
            }
            catch (Exception error) { Debug.Log("WORDBOUND_GAMEPLAY_OPERATION_UNAVAILABLE: " + error.GetType().Name); }
        }
        static string DescribePeer(Peer peer, int index)
        {
            try
            {
                var snapshot = peer.Controller.Frame;
                var actor = snapshot?.Bodies.FirstOrDefault(body => body.Kind == WorldBodyKind.Player && body.SourceId == peer.Actor);
                var progress = snapshot?.Progress.Players.FirstOrDefault(player => player.Id == peer.Actor);
                bool canMove = peer.Controller.PhysicsWorld != null && peer.Controller.PhysicsWorld.CanMove(peer.Actor, out _);
                string position = actor == null ? "none" : FormattableString.Invariant($"{actor.Position.X:F3}/{actor.Position.Y:F3}/{actor.Position.Z:F3}");
                return $"peer={index},host={peer.Runner != null && peer.Runner.IsServer},tick={snapshot?.Progress.Tick},advanced={peer.Advanced},inputReads={peer.InputReads},movingReads={peer.MovingInputReads},position={position},connected={progress?.Connected},canMove={canMove},paused={peer.Controller.IsSimulationPaused},failure={peer.Session.Failure}";
            }
            catch (Exception error) { return $"peer={index},diagnosticUnavailable={error.GetType().Name}"; }
        }
        string DescribeReadiness(Peer peer)
        {
            var facts = new List<string>();
            try
            {
                var controller = peer.Controller;
                bool alive = controller != null;
                facts.Add($"controllerAlive={alive},controllerEnabled={alive && controller.enabled},controllerActive={alive && controller.gameObject.activeInHierarchy},courseWorldPresent={alive && controller.CourseWorld != null}");
            }
            catch (Exception error) { facts.Add("controllerFactsUnavailable=" + error.GetType().Name); }
            try
            {
                var session = peer.Session;
                facts.Add($"sessionPresent={session != null},sessionRunnerStarted={session != null && session.RunnerStarted},sessionGameplayReady={session != null && session.GameplayReady},committedMarkPresent={session?.Committed != null}");
            }
            catch (Exception error) { facts.Add("sessionFactsUnavailable=" + error.GetType().Name); }
            try
            {
                var runner = peer.Runner;
                bool present = runner != null;
                facts.Add($"runnerPresent={present},runnerRunning={present && runner.IsRunning},runnerHost={present && runner.IsServer},runnerClient={present && runner.IsClient}");
                if (present)
                {
                    bool hasInfo = runner.TryGetSceneInfo(out var info);
                    bool hasScene = hasInfo && Enumerable.Range(0, info.SceneCount)
                        .Any(index => info.Scenes[index] == SceneRef.FromIndex(sceneIndex));
                    var manager = runner.SceneManager;
                    bool physicsAvailable = false, physicsValid = false, physicsIsolated = false;
                    if (manager != null && manager.TryGetPhysicsScene3D(out var physicsScene))
                    {
                        physicsAvailable = true; physicsValid = physicsScene.IsValid();
                        physicsIsolated = physicsValid && physicsScene != UnityEngine.Physics.defaultPhysicsScene;
                    }
                    facts.Add($"sceneInfoAvailable={hasInfo},gameplayScenePresent={hasScene},sceneManagerPresent={manager != null},sceneManagerBusy={manager != null && manager.IsBusy},physicsSceneAvailable={physicsAvailable},physicsSceneValid={physicsValid},physicsSceneIsolated={physicsIsolated}");
                }
            }
            catch (Exception error) { facts.Add("runnerSceneFactsUnavailable=" + error.GetType().Name); }
            return "," + string.Join(",", facts);
        }
        IEnumerator Wait(Task task, string operation, double seconds)
        {
            pending.Add(task);
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline && !lifetime.IsCancellationRequested)
            { Healthy(); yield return null; }
            if (!task.IsCompleted) { lifetime.Cancel(); Assert.Fail(operation + " exceeded its deadline."); }
            Assert.IsFalse(task.IsCanceled, operation + " was cancelled.");
            if (task.IsFaulted)
            {
                var error = task.Exception?.GetBaseException();
                string code = error is OnlineServiceException service ? "HTTP " + service.Status + " " + service.Code : error?.GetType().Name;
                string origin = "unavailable";
                try
                {
                    origin = string.Join(" > ", (new System.Diagnostics.StackTrace(error, false).GetFrames() ??
                        Array.Empty<System.Diagnostics.StackFrame>()).Take(4).Select(frame => frame.GetMethod())
                        .Where(method => method != null).Select(method => method.DeclaringType?.FullName + "." + method.Name));
                }
                catch { }
                Assert.Fail(operation + " failed: " + code + "; origin=" + origin + ". Raw responses and exception messages are omitted.");
            }
        }
        static NetworkObject Load(string name)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + name + ".prefab");
            Assert.IsNotNull(root, "Root must create and register the native " + name + " prefab first.");
            var obj = root.GetComponent<NetworkObject>();
            Assert.IsNotNull(obj);
            return obj;
        }
        static int PrepareScene()
        {
            Assert.IsTrue(File.Exists(ProofScene), "Create the native Photon proof scene before this gated test.");
            Assert.AreEqual("", SessionState.GetString(PriorScenesKey, ""), "Prior proof scene settings require recovery.");
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
            if (phases != null)
            {
                MatchSnapshot boundary = null;
                try { boundary = peers.FirstOrDefault(peer => peer.Runner != null && peer.Runner.IsServer)?.Controller.Frame; }
                catch (Exception error) { Debug.Log("WORDBOUND_PHASE_UNAVAILABLE: native-boundary-" + error.GetType().Name); }
                finally { phases.Finish(boundary, "teardown"); phases = null; }
            }
            profiler?.Dispose(); profiler = null;
            tickBatches?.Dispose(); tickBatches = null;
            lifetime?.Cancel();
            int failures = 0;
            foreach (var peer in peers.OrderBy(p => p.Runner != null && p.Runner.IsServer))
            {
                Task stop = null;
                try { stop = peer.Controller.Stop(); } catch { failures++; }
                if (stop != null)
                {
                    double deadline = Time.realtimeSinceStartupAsDouble + 20;
                    while (!stop.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    if (!stop.IsCompleted || stop.IsCanceled) failures++;
                    if (stop.IsFaulted) { _ = stop.Exception; failures++; }
                }
            }
            double pendingDeadline = Time.realtimeSinceStartupAsDouble + 5;
            while (pending.Any(t => !t.IsCompleted) && Time.realtimeSinceStartupAsDouble < pendingDeadline) yield return null;
            failures += pending.Count(t => !t.IsCompleted);
            foreach (var task in pending.Where(t => t.IsFaulted)) _ = task.Exception;
            pending.Clear();
            if (live != null)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                var task = live.Cleanup(cleanup.Token);
                while (!task.IsCompleted && !cleanup.IsCancellationRequested) yield return null;
                if (!task.IsCompleted || task.IsCanceled) failures++;
                else if (task.IsFaulted) { _ = task.Exception; failures++; }
                else failures += task.Result;
                live = null;
            }
            foreach (var root in Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(g => g && g.scene.IsValid() && g.name.StartsWith(Prefix, StringComparison.Ordinal)).ToArray())
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
            if (Application.isPlaying) yield return null;
            bool leaked = Resources.FindObjectsOfTypeAll<GameObject>()
                .Any(g => g && g.scene.IsValid() && g.name.StartsWith(Prefix, StringComparison.Ordinal));
            peers.Clear(); lifetime?.Dispose(); lifetime = null;
            if (Application.isPlaying && SessionState.GetBool(PlayStartedKey, false)) yield return new ExitPlayMode();
            SessionState.EraseBool(PlayStartedKey);
            string prior = SessionState.GetString(PriorScenesKey, "");
            if (prior != "")
            {
                var saved = JsonUtility.FromJson<SavedScenes>(prior);
                EditorBuildSettings.scenes = saved.paths.Select((path, i) => new EditorBuildSettingsScene(path, saved.enabled[i])).ToArray();
                SessionState.EraseString(PriorScenesKey);
            }
            Assert.AreEqual(0, failures, "Proof session/service cleanup must finish without failures.");
            Assert.IsFalse(leaked, "Proof-owned native objects remained after destruction.");
        }
    }
}
