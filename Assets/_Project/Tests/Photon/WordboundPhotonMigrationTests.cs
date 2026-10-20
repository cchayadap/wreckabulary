using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Fusion;
using Fusion.Photon.Realtime;
using NUnit.Framework;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Assert = NUnit.Framework.Assert;

namespace Wreckabulary.PhotonTests
{
    public sealed class WordboundPhotonMigrationTests
    {
        const string PrefabPath = "Assets/_Project/Prefabs/Wordbound/Network/WordboundSnapshotChunk.prefab";

        [UnityTest, Category("ExternalPhoton")]
        public IEnumerator CompleteCanonicalMatchSurvivesHostReplacement() => Run(false);

        [UnityTest, Category("ExternalPhoton")]
        public IEnumerator LargeChunkedPacketSurvivesHostReplacement() => Run(true);

        IEnumerator Run(bool large)
        {
            string appId = Environment.GetEnvironmentVariable("WORDBOUND_PHOTON_APP_ID");
            if (!Guid.TryParse(appId, out _)) Assert.Ignore("Supply the owner's Fusion App ID to run external Photon tests.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "Run WordboundFusionSetup.CreateSnapshotPrefab first.");
            var networkPrefab = prefab.GetComponent<NetworkObject>();
            Assert.IsNotNull(networkPrefab.GetComponent<WordboundFusionSnapshotChunk>());
            int sceneIndex = WordboundPhotonConnectionTests.PrepareProofScene();
            var fixture = new CanonicalFixture();
            string json = fixture.Json;
            var mark = fixture.Mark;
            if (large)
            {
                var bytes = new byte[131072];
                new System.Random(71).NextBytes(bytes);
                json = "{\"nativeCarrierStress\":\"" + Convert.ToBase64String(bytes) + "\"}";
                using var sha = SHA256.Create();
                string hash = string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(json)).Select(b => b.ToString("x2")));
                mark = new OnlineSnapshotMark(mark.MatchId, mark.IdentityHash, 7, 23, 1234, hash);
            }
            var packet = OnlineSnapshotPacket.Compress(json);
            if (large) Assert.Greater(packet.ChunkCount, 20);
            yield return new EnterPlayMode();
            var scene = new NetworkSceneInfo();
            scene.AddSceneRef(SceneRef.FromIndex(sceneIndex), LoadSceneMode.Additive);
            string room = "wordbound-migration-" + Guid.NewGuid().ToString("N");
            byte[] secret = Encoding.ASCII.GetBytes(Guid.NewGuid().ToString("N"));
            var handlers = new[] { new MigrationCallbacks(secret, true), new MigrationCallbacks(secret, false) };
            var runners = new NetworkRunner[2];
            string[] users = { "migration-" + Guid.NewGuid().ToString("N"), "migration-" + Guid.NewGuid().ToString("N") };
            double started = Time.realtimeSinceStartupAsDouble;
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(180));
            for (int index = 0; index < 2; index++)
            {
                runners[index] = NewRunner(index, handlers[index]);
                var task = runners[index].StartGame(Arguments(index == 0 ? GameMode.Host : GameMode.Client,
                    runners[index], users[index], appId, room, secret, scene, cancellation.Token));
                while (!task.IsCompleted && !cancellation.IsCancellationRequested) yield return null;
                Assert.IsTrue(task.IsCompleted, "Migration proof startup timed out.");
                var result = task.GetAwaiter().GetResult();
                Assert.IsTrue(result.Ok, "Migration proof startup failed: " + result.ShutdownReason);
            }
            var host = runners[0];
            var client = runners[1];
            Assert.AreEqual(2, host.ActivePlayers.Count());
            for (int index = 0; index < packet.ChunkCount; index++)
            {
                int captured = index;
                host.Spawn(networkPrefab, onBeforeSpawned: (_, obj) =>
                    obj.GetComponent<WordboundFusionSnapshotChunk>().Write(mark, packet, captured));
            }
            double deadline = Time.realtimeSinceStartupAsDouble + 35;
            WordboundFusionSnapshotChunk[] replicas;
            do
            {
                yield return null;
                replicas = UnityEngine.Object.FindObjectsByType<WordboundFusionSnapshotChunk>()
                    .Where(c => c.Object != null && c.Object.IsValid && c.Runner == client && c.Count == packet.ChunkCount).ToArray();
            } while (replicas.Length != packet.ChunkCount && Time.realtimeSinceStartupAsDouble < deadline);
            Assert.AreEqual(packet.ChunkCount, replicas.Length, "All chunks must replicate before saving the cloud checkpoint.");
            Assert.AreEqual(json, WordboundFusionSnapshotChunk.Restore(replicas, out var replicaMark));
            Assert.IsTrue(mark.Same(replicaMark));
            int writtenTick = host.Tick.Raw;
            deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (host.IsRunning && host.Tick.Raw <= writtenTick && Time.realtimeSinceStartupAsDouble < deadline &&
                   !cancellation.IsCancellationRequested) yield return null;
            Assert.IsTrue(host.IsRunning && host.Tick.Raw > writtenTick, "Checkpoint carrier did not reach the next simulation tick.");
            var push = host.PushHostMigrationSnapshot();
            deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (!push.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsTrue(push.IsCompleted && push.GetAwaiter().GetResult(), "Photon must confirm the exact checkpoint upload.");
            var shutdown = host.Shutdown(false);
            while (!shutdown.IsCompleted && !cancellation.IsCancellationRequested) yield return null;
            Assert.IsTrue(shutdown.IsCompleted, "Original host shutdown timed out.");
            deadline = Time.realtimeSinceStartupAsDouble + 35;
            while (handlers[1].Migration == null && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsNotNull(handlers[1].Migration, "Remaining client did not receive a host migration token.");
            var migration = handlers[1].Migration;
            Assert.AreEqual(GameMode.Host, migration.GameMode);
            shutdown = client.Shutdown(false, ShutdownReason.HostMigration);
            while (!shutdown.IsCompleted && !cancellation.IsCancellationRequested) yield return null;
            Assert.IsTrue(shutdown.IsCompleted, "Old client shutdown timed out.");
            UnityEngine.Object.Destroy(host.gameObject);
            UnityEngine.Object.Destroy(client.gameObject);
            var replacement = NewRunner(2, new MigrationCallbacks(secret, true));
            string recoveredJson = null;
            OnlineSnapshotMark recoveredMark = null;
            Exception resumeError = null;
            bool resumeCompleted = false;
            var args = Arguments(migration.GameMode, replacement, users[1], appId, room, secret, scene, cancellation.Token);
            args.HostMigrationToken = migration;
            args.HostMigrationResume = runner =>
            {
                var chunkDiagnostics = new System.Collections.Generic.List<string>();
                int temporaryReuses = 0;
                try
                {
                    WordboundFusionSnapshotChunk previous = null;
                    System.Collections.Generic.IEnumerable<WordboundFusionSnapshotChunk> ReadSaved()
                    {
                        foreach (var obj in runner.GetResumeSnapshotNetworkObjects())
                        {
                            var chunk = obj.GetComponent<WordboundFusionSnapshotChunk>();
                            if (chunk == null) continue;
                            if (ReferenceEquals(previous, chunk)) temporaryReuses++;
                            previous = chunk;
                            chunkDiagnostics.Add(chunk.Index + "/" + chunk.Count + ":bytes=" + chunk.PayloadBytes +
                                ":total=" + chunk.TotalBytes + ":mark=" + chunk.Mark().Same(mark) +
                                ":wire=" + (chunk.WireHash.ToString() == packet.WireHash));
                            yield return chunk;
                        }
                    }
                    recoveredJson = WordboundFusionSnapshotChunk.Restore(ReadSaved(), out recoveredMark);
                    var recoveredPacket = OnlineSnapshotPacket.Compress(recoveredJson);
                    for (int index = 0; index < recoveredPacket.ChunkCount; index++)
                    {
                        int captured = index;
                        runner.Spawn(networkPrefab, onBeforeSpawned: (_, obj) =>
                            obj.GetComponent<WordboundFusionSnapshotChunk>().Write(recoveredMark, recoveredPacket, captured));
                    }
                }
                catch (Exception error) { resumeError = error; }
                finally
                {
                    Debug.Log("[Wreckabulary] Resume carrier count=" + chunkDiagnostics.Count +
                        ", reused temporary objects=" + temporaryReuses + ", manifests=" + string.Join(";", chunkDiagnostics));
                    resumeCompleted = true;
                }
            };
            var resume = replacement.StartGame(args);
            while (!resume.IsCompleted && !cancellation.IsCancellationRequested) yield return null;
            Assert.IsTrue(resume.IsCompleted, "Replacement host startup timed out.");
            Assert.IsTrue(resume.GetAwaiter().GetResult().Ok, "Replacement host could not start.");
            deadline = Time.realtimeSinceStartupAsDouble + 35;
            while (!resumeCompleted && replacement.IsRunning && Time.realtimeSinceStartupAsDouble < deadline &&
                   !cancellation.IsCancellationRequested) yield return null;
            Assert.IsTrue(resumeCompleted, "Replacement host started but did not complete its migration resume callback.");
            Assert.IsNull(resumeError, resumeError?.ToString());
            Assert.IsTrue(string.Equals(json, recoveredJson, StringComparison.Ordinal),
                "Cloud recovery must preserve every canonical byte.");
            Assert.IsTrue(mark.Same(recoveredMark), "Epoch, sequence, tick and all identity hashes must survive.");
            var rebuilt = UnityEngine.Object.FindObjectsByType<WordboundFusionSnapshotChunk>()
                .Where(c => c.Object != null && c.Object.IsValid && c.Runner == replacement).ToArray();
            Assert.AreEqual(packet.ChunkCount, rebuilt.Length, "Replacement must recreate the exact complete carrier set.");
            Assert.IsTrue(string.Equals(json, WordboundFusionSnapshotChunk.Restore(rebuilt, out var rebuiltMark), StringComparison.Ordinal));
            Assert.IsTrue(mark.Same(rebuiltMark));
            if (!large) fixture.VerifyAndRestore(recoveredJson, recoveredMark);
            Directory.CreateDirectory("Logs/wordbound");
            File.WriteAllText("Logs/wordbound/photon-migration-" + (large ? "large" : "canonical") + ".json",
                JsonUtility.ToJson(new Report
                {
                    utc = DateTime.UtcNow.ToString("o"), rawBytes = packet.RawLength, packetBytes = packet.Length,
                    chunks = packet.ChunkCount, seconds = Time.realtimeSinceStartupAsDouble - started,
                    result = "PASS", completeMatchValidated = !large,
                    scope = "One PC, two native peers, explicit cloud snapshot and original host shutdown; session authentication and gameplay physics not covered."
                }, true));
        }

        static NetworkRunner NewRunner(int index, MigrationCallbacks callbacks)
        {
            var root = new GameObject("WordboundPhotonProof-Migration-" + index);
            var runner = root.AddComponent<NetworkRunner>();
            runner.AddCallbacks(callbacks);
            root.AddComponent<NetworkSceneManagerDefault>();
            return runner;
        }

        static StartGameArgs Arguments(GameMode mode, NetworkRunner runner, string user, string appId, string room,
            byte[] secret, NetworkSceneInfo scene, CancellationToken cancellation)
        {
            var config = new NetworkProjectConfig { PeerMode = NetworkProjectConfig.PeerModes.Multiple };
            foreach (var source in NetworkProjectConfig.Global.PrefabTable.Prefabs)
                config.PrefabTable.AddSource(source);
            config.HostMigration.EnableAutoUpdate = false;
            config.Network.ReliableDataTransferModes = NetworkConfiguration.ReliableDataTransfers.ClientToServer;
            return new StartGameArgs
            {
                GameMode = mode, SessionName = room, PlayerCount = 2, IsVisible = false, IsOpen = true,
                EnableClientSessionCreation = false, DisableNATPunchthrough = true, ConnectionToken = secret,
                AuthValues = new AuthenticationValues(user), Config = config, Scene = scene,
                SceneManager = runner.GetComponent<NetworkSceneManagerDefault>(),
                CustomPhotonAppSettings = new FusionAppSettings
                { AppIdFusion = appId, AppVersion = "wordbound-migration-proof-v1", FixedRegion = "asia", UseNameServer = true },
                StartGameCancellationToken = cancellation
            };
        }

        [UnityTearDown]
        public IEnumerator Cleanup() => new WordboundPhotonConnectionTests().Cleanup();

        sealed class MigrationCallbacks : WordboundPhotonConnectionTests.ProofCallbacks
        {
            public HostMigrationToken Migration;
            public MigrationCallbacks(byte[] token, bool host) : base(token, host) { }
            public override void OnHostMigration(NetworkRunner runner, HostMigrationToken token) => Migration = token;
        }

        sealed class CanonicalFixture : IWordboundWorldQuery
        {
            readonly ModeDefinition mode = ModeDefinition.For("TimeRun");
            readonly ItemCatalogue catalogue;
            readonly GameRules rules;
            readonly ResolvedCourse course;
            readonly MatchBinding binding;
            public readonly string Json;
            public readonly OnlineSnapshotMark Mark;
            public CanonicalFixture()
            {
                const string config = "Assets/_Project/Data/Config/";
                catalogue = ItemCatalogue.FromJson(File.ReadAllText(config + "items.json"), "items.json");
                rules = mode.CreateGameplayRules(RuleBook.FromJson(File.ReadAllText(config + "rules.json"), "rules.json").For("Dibs"));
                var resolved = CourseResolver.Resolve(CourseTemplates.RaceCircuit(), CourseTemplates.ProofCatalogue(),
                    new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(mode)));
                Assert.IsTrue(resolved.Success);
                course = resolved.Manifest;
                binding = new MatchBinding("33333333-3333-4333-a333-333333333333", "1", mode.Hash, course.SourceHash,
                    course.Hash, new string('a', 64), new string('b', 64), new[] {
                        new WordboundSeat("11111111-1111-4111-a111-111111111111", 0, 0),
                        new WordboundSeat("22222222-2222-4222-a222-222222222222", 0, 1) });
                var match = NewMatch();
                for (int tick = 1; tick <= 7; tick++) Assert.IsTrue(match.Advance(tick * 17).Advanced);
                var stamp = match.PublishSnapshot(out Json);
                Mark = new OnlineSnapshotMark(stamp.MatchId, stamp.IdentityHash, stamp.Epoch, stamp.Sequence, stamp.Tick, stamp.StateHash);
            }
            AuthoritativeMatch NewMatch() => new AuthoritativeMatch(binding, mode, course, CourseTemplates.ProofCatalogue(), catalogue, rules, this);
            public void VerifyAndRestore(string json, OnlineSnapshotMark mark)
            {
                var imported = MatchSnapshotCodec.Import(json, binding, mode, course, catalogue, rules);
                Assert.IsTrue(imported.Success, imported.Error);
                Assert.AreEqual(mark.StateHash, imported.Hash);
                var replacement = NewMatch();
                replacement.RestoreCommitted(imported.Snapshot,
                    new SnapshotStamp(mark.MatchId, mark.IdentityHash, mark.Epoch, mark.Sequence, mark.Tick, mark.StateHash),
                    mark.Epoch + 1, 1000);
                Assert.AreEqual(mark.Epoch + 1, replacement.Epoch);
                Assert.AreEqual(mark.Tick, replacement.Tick);
                Assert.AreEqual(imported.Snapshot.Bodies.Count, replacement.Capture(1).Bodies.Count);
            }
            public bool HasLineOfSight(string actor, WorldVector from, WorldVector to) => false;
            public bool CanDeploy(string actor, string recipe, WorldVector position) => false;
            public bool PuzzleSatisfied(string actor, ResolvedCoursePuzzle puzzle, WorldBodyState body) => false;
        }

        [Serializable]
        sealed class Report
        {
            public string utc, result, scope;
            public int rawBytes, packetBytes, chunks;
            public double seconds;
            public bool completeMatchValidated;
        }
    }
}
