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
    public sealed class WordboundNativeGameplayTests
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
        WordboundGameplayPrefabs prefabs;
        NetworkPrefabRef carrier;
        int sceneIndex;

        sealed class Peer
        {
            public WordboundGameplayController Controller;
            public WordboundFusionSession Session;
            public string Actor;
            public bool Move;
            public int Advanced;
            public readonly Dictionary<long, OnlineSnapshotMark> Received = new Dictionary<long, OnlineSnapshotMark>();
            public readonly Dictionary<string, OnlineSnapshotMark> Acknowledged = new Dictionary<string, OnlineSnapshotMark>();
            public readonly Dictionary<string, int> CommandObservations = new Dictionary<string, int>();
            public NetworkRunner Runner => Session.Runner;
        }

        [UnityTest, Category("ExternalPhotonGameplay")]
        public IEnumerator TwoAuthenticatedPeersComposeCanonicalBodiesAndCommitClientCommands() => Run(2, false);

        [UnityTest, Category("ExternalPhotonGameplay")]
        public IEnumerator FourAuthenticatedPeersComposeCanonicalBodiesAndCommitClientCommands() => Run(4, false);

        [UnityTest, Category("ExternalPhotonGameplay")]
        public IEnumerator EightAuthenticatedPeersComposeCanonicalBodiesAndCommitClientCommands() => Run(8, false);

        [UnityTest, Category("ExternalPhotonGameplay")]
        public IEnumerator AuthenticatedGameplayRestoresAfterHostDeparture() => Run(2, true);

        IEnumerator Run(int count, bool migrate)
        {
            if (Environment.GetEnvironmentVariable(Gate) != "1")
                Assert.Ignore("Set WORDBOUND_EXTERNAL_GAMEPLAY=1 after configuring production Photon custom authentication.");
            Assert.IsFalse(Application.isPlaying, "Run this gated Editor fixture outside Play Mode.");
            prefabs = new WordboundGameplayPrefabs(Load("WordboundPlayerBody"), Load("WordboundItemBody"), Load("WordboundPlatformBody"));
            Load("WordboundSnapshotChunk");
            carrier = NetworkPrefabRef.Parse(AssetDatabase.AssetPathToGUID(PrefabFolder + "WordboundSnapshotChunk.prefab"));
            sceneIndex = PrepareScene();
            SessionState.SetBool(PlayStartedKey, true);
            yield return new EnterPlayMode();
            lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(8));
            live = new WordboundLiveMatch(Path.GetFullPath(Path.Combine(Application.dataPath, "..")));
            yield return Wait(live.Prepare(count, lifetime.Token), "Prepare real private coordinator match", 120);
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
            Debug.Log("WORDBOUND_GAMEPLAY_OBSERVED: authenticated real bodies, client input and committed command; cleanup pending.");

            if (migrate)
            {
                long oldEpoch = second.Controller.Frame.Epoch;
                var originalRunner = second.Runner;
                yield return Wait(first.Controller.Stop(), "Depart original host", 25);
                yield return Until(() => second.Runner != originalRunner && second.Runner != null && second.Runner.IsServer &&
                    Ready(second) && second.Controller.Frame.Epoch == oldEpoch + 1, "Restore authenticated gameplay on replacement host", 75,
                    allowStoppedFirst: true);
                yield return Until(() => ExactBodies(second), "Reconcile the complete restored body ledger", 20, allowStoppedFirst: true);
                AssertBodies(second, true);
                Assert.IsTrue(second.Controller.Frame.Progress.Players.Single(p => p.Id == second.Actor).Connected);
                Assert.IsFalse(second.Controller.Frame.Progress.Players.Single(p => p.Id == first.Actor).Connected);
                Assert.AreEqual(command.Sequence, second.Controller.Frame.Commands.Frontiers[second.Actor]);
                Assert.AreEqual(1, second.Controller.Frame.Commands.Receipts.Count(r => r.ActorId == second.Actor && r.Sequence == command.Sequence));
                long resumedTick = second.Controller.Frame.Progress.Tick;
                yield return Until(() => second.Controller.Frame.Progress.Tick > resumedTick + 5,
                    "Advance restored host after canonical body application", 10, allowStoppedFirst: true);
                Assert.AreEqual(1, peers.Count(p => p.Runner != null && p.Runner.IsRunning && p.Runner.IsServer));
                Debug.Log("WORDBOUND_GAMEPLAY_OBSERVED: authenticated host replacement restored committed command and canonical bodies; cleanup pending.");
            }
        }

        Peer CreatePeer(int index)
        {
            var peer = new Peer { Actor = live.Clients[index].Credentials.PlayerId };
            var root = new GameObject(Prefix + "Controller-" + index);
            peer.Controller = root.AddComponent<WordboundGameplayController>();
            peer.Controller.Initialize(live.Verified, live.Mode, live.Course, live.Geometry, live.Items, live.Rules, peer.Actor, prefabs,
                new WordboundGameplayOptions {
                    GameplayScene = SceneRef.FromIndex(sceneIndex), CheckpointMilliseconds = 500,
                    ReadMovement = () => new WordboundMovementInput { MoveZ = peer.Move ? 1 : 0 }
                });
            peer.Session = new WordboundFusionSession(() => {
                var runnerRoot = new GameObject(Prefix + "Runner-" + index + "-" + Guid.NewGuid().ToString("N"));
                var runner = runnerRoot.AddComponent<NetworkRunner>();
                runnerRoot.AddComponent<NetworkSceneManagerDefault>();
                return runner;
            }, new FusionAppSettings { AppIdFusion = WordboundLiveMatch.AppId, UseNameServer = true },
                Configuration(), carrier, live.Verifier, live.Expected, ct => live.FreshJoin(index, ct),
                peer.Controller.Authority, live.Connections(index),
                new OnlineCheckpointService(live.Clients[index], live.Expected, peer.Controller.Authority.IdentityHash));
            peer.Controller.AttachSession(peer.Session);
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
        static bool Ready(Peer peer) => peer.Session.GameplayReady && peer.Controller.Frame != null &&
            peer.Controller.CourseWorld != null && (peer.Runner.IsServer ? peer.Controller.PhysicsWorld != null : true) &&
            !peer.Controller.IsSimulationPaused;
        static string Key(string actor, long sequence) => actor + "/" + sequence;
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
        void Healthy(bool allowStoppedFirst = false)
        {
            foreach (var peer in allowStoppedFirst ? peers.Skip(1) : peers)
            {
                Assert.AreEqual("", peer.Session.Failure, "The actual session failed.");
                Assert.AreEqual("", peer.Controller.Fault == "" ? "" : peer.Controller.Fault.Split(':')[0], "The actual gameplay composition failed.");
            }
        }
        IEnumerator Until(Func<bool> complete, string operation, double seconds, bool allowStoppedFirst = false)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!complete() && Time.realtimeSinceStartupAsDouble < deadline && !lifetime.IsCancellationRequested)
            { Healthy(allowStoppedFirst); yield return null; }
            Healthy(allowStoppedFirst);
            Assert.IsTrue(complete(), operation + " exceeded its deadline.");
        }
        IEnumerator Wait(Task task, string operation, double seconds)
        {
            pending.Add(task);
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline && !lifetime.IsCancellationRequested) yield return null;
            if (!task.IsCompleted) { lifetime.Cancel(); Assert.Fail(operation + " exceeded its deadline."); }
            Assert.IsFalse(task.IsCanceled, operation + " was cancelled.");
            if (task.IsFaulted)
            {
                var error = task.Exception?.GetBaseException();
                string code = error is OnlineServiceException service ? "HTTP " + service.Status + " " + service.Code : error?.GetType().Name;
                Assert.Fail(operation + " failed: " + code + ". Raw responses and exceptions are omitted.");
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
                UnityEngine.Object.Destroy(root);
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
