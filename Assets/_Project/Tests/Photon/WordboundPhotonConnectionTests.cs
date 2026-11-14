using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using NUnit.Framework;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;

namespace Wreckabulary.PhotonTests
{
    public sealed class WordboundPhotonConnectionTests
    {
        const string RunnerPrefix = "WordboundPhotonProof-";
        const string ProofScene = "Assets/_Project/Tests/Photon/WordboundPhotonProof.unity";
        const string PriorScenesKey = "Wreckabulary.PhotonProof.PriorScenes";

        [UnityTest, Category("ExternalPhoton")]
        public IEnumerator TwoPeersExchangeCompleteReliablePayloads() => Run(2);

        [UnityTest, Category("ExternalPhoton")]
        public IEnumerator FourPeersExchangeCompleteReliablePayloads() => Run(4);

        [UnityTest, Category("ExternalPhoton")]
        public IEnumerator EightPeersExchangeCompleteReliablePayloads() => Run(8);

        IEnumerator Run(int count)
        {
            string appId = Environment.GetEnvironmentVariable("WORDBOUND_PHOTON_APP_ID");
            if (!Guid.TryParse(appId, out _)) Assert.Ignore("Supply the owner's Fusion App ID to run external Photon tests.");
            int sceneIndex = PrepareProofScene();
            yield return new EnterPlayMode();
            var sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(SceneRef.FromIndex(sceneIndex), LoadSceneMode.Additive);
            string room = "wordbound-proof-" + Guid.NewGuid().ToString("N");
            byte[] token = Encoding.ASCII.GetBytes(Guid.NewGuid().ToString("N"));
            var runners = new List<NetworkRunner>();
            var callbacks = new List<ProofCallbacks>();
            double started = Time.realtimeSinceStartupAsDouble;
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(180)))
            {
                for (int index = 0; index < count; index++)
                {
                    var root = new GameObject(RunnerPrefix + index);
                    var runner = root.AddComponent<NetworkRunner>();
                    var handler = new ProofCallbacks(token, index == 0);
                    runner.AddCallbacks(handler);
                    runners.Add(runner); callbacks.Add(handler);
                    var task = runner.StartGame(new StartGameArgs
                    {
                        GameMode = index == 0 ? GameMode.Host : GameMode.Client,
                        SessionName = room,
                        PlayerCount = count,
                        IsVisible = false,
                        IsOpen = true,
                        EnableClientSessionCreation = false,
                        DisableNATPunchthrough = true,
                        ConnectionToken = token,
                        AuthValues = new AuthenticationValues("proof-" + Guid.NewGuid().ToString("N")),
                        Config = new NetworkProjectConfig { PeerMode = NetworkProjectConfig.PeerModes.Multiple },
                        Scene = sceneInfo,
                        SceneManager = root.AddComponent<NetworkSceneManagerDefault>(),
                        CustomPhotonAppSettings = new FusionAppSettings
                        {
                            AppIdFusion = appId, AppVersion = "wordbound-proof-v1",
                            FixedRegion = "asia", UseNameServer = true
                        },
                        StartGameCancellationToken = cancellation.Token
                    });
                    while (!task.IsCompleted && !cancellation.IsCancellationRequested) yield return null;
                    Assert.IsTrue(task.IsCompleted, "Photon session startup timed out.");
                    var result = task.GetAwaiter().GetResult();
                    Assert.IsTrue(result.Ok, "Photon startup failed: " + result.ShutdownReason + " / " + result.ErrorMessage);
                }
                double deadline = Time.realtimeSinceStartupAsDouble + 20;
                while (runners.Any(r => r.ActivePlayers.Count() != count) && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
                Assert.IsTrue(runners.All(r => r.ActivePlayers.Count() == count), "Every peer must see the complete private roster.");
                runners[0].SessionInfo.IsOpen = false;
                var payload = Enumerable.Range(0, 65536).Select(i => (byte)(i * 31 + 7)).ToArray();
                for (int index = 1; index < count; index++)
                    runners[index].SendReliableDataToServer(ReliableKey.FromInts(1, index, 0, 0), payload);
                deadline = Time.realtimeSinceStartupAsDouble + 45;
                while ((callbacks[0].Received.Count < count - 1 || callbacks.Skip(1).Any(c => c.Received.Count != 1)) &&
                       Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.AreEqual(count - 1, callbacks[0].Received.Count, "Host must receive one full message from each client.");
                foreach (var handler in callbacks)
                    foreach (var received in handler.Received)
                        CollectionAssert.AreEqual(payload, received, "Reliable payload must remain byte-exact.");
                Assert.IsTrue(callbacks.Skip(1).All(c => c.Received.Count == 1), "All clients must receive the host echo.");
                Assert.AreEqual(count - 1, callbacks[0].Senders.Distinct().Count(), "Reliable sender identity must remain distinct.");
                Directory.CreateDirectory("Logs/wordbound");
                File.WriteAllText("Logs/wordbound/photon-proof-" + count + ".json", JsonUtility.ToJson(new ProofReport
                {
                    utc = DateTime.UtcNow.ToString("o"), peers = count, payloadBytes = payload.Length,
                    clientMessagesReceived = callbacks[0].Received.Count, echoesReceived = callbacks.Skip(1).Sum(c => c.Received.Count),
                    region = runners[0].SessionInfo.Region, seconds = Time.realtimeSinceStartupAsDouble - started,
                    result = "PASS", scope = "Private relayed host/client sessions and reliable data only; no gameplay or migration acceptance."
                }, true));
                Debug.Log("[Wreckabulary] Photon private connection proof PASS: " + count + " peers, byte-exact 64 KiB payloads both ways.");
            }
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var runner in NetworkRunner.Instances.Where(r => r && r.name.StartsWith(RunnerPrefix, StringComparison.Ordinal))
                         .OrderBy(r => r.IsServer).ToArray())
            {
                var shutdown = runner.Shutdown();
                double deadline = Time.realtimeSinceStartupAsDouble + 15;
                while (!shutdown.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (runner) UnityEngine.Object.Destroy(runner.gameObject);
            }
            if (Application.isPlaying) yield return new ExitPlayMode();
            string prior = SessionState.GetString(PriorScenesKey, "");
            if (prior != "")
            {
                var saved = JsonUtility.FromJson<SavedScenes>(prior);
                EditorBuildSettings.scenes = saved.paths.Select((path, i) => new EditorBuildSettingsScene(path, saved.enabled[i])).ToArray();
                SessionState.EraseString(PriorScenesKey);
            }
        }

        internal static int PrepareProofScene()
        {
            Assert.IsTrue(File.Exists(ProofScene),
                "Create the native proof scene first with the WordboundPhotonSetup.CreateProofScene gate.");
            var previous = EditorBuildSettings.scenes;
            Assert.AreEqual("", SessionState.GetString(PriorScenesKey, ""), "A previous probe did not restore its scene settings.");
            SessionState.SetString(PriorScenesKey, JsonUtility.ToJson(new SavedScenes
            {
                paths = previous.Select(s => s.path).ToArray(), enabled = previous.Select(s => s.enabled).ToArray()
            }));
            var scenes = previous.Where(s => s.path != ProofScene).Concat(new[] { new EditorBuildSettingsScene(ProofScene, true) }).ToArray();
            EditorBuildSettings.scenes = scenes;
            return scenes.Count(s => s.enabled) - 1;
        }

        [Serializable]
        sealed class SavedScenes
        {
            public string[] paths;
            public bool[] enabled;
        }

        [Serializable]
        sealed class ProofReport
        {
            public string utc, region, result, scope;
            public int peers, payloadBytes, clientMessagesReceived, echoesReceived;
            public double seconds;
        }

        internal class ProofCallbacks : INetworkRunnerCallbacks
        {
            readonly byte[] connectionToken;
            readonly bool host;
            public readonly List<byte[]> Received = new List<byte[]>();
            public readonly List<PlayerRef> Senders = new List<PlayerRef>();
            public ProofCallbacks(byte[] token, bool isHost) { connectionToken = token; host = isHost; }
            public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
            {
                if (token != null && token.SequenceEqual(connectionToken)) request.Accept();
                else request.Refuse();
            }
            public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data)
            {
                Received.Add(data.ToArray()); Senders.Add(player);
                if (host) runner.SendReliableDataToPlayer(player, key, data);
            }
            public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
            public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
            public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
            public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
            public void OnInput(NetworkRunner runner, NetworkInput input) { }
            public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
            public void OnShutdown(NetworkRunner runner, ShutdownReason reason) { }
            public void OnConnectedToServer(NetworkRunner runner) { }
            public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
            public void OnConnectFailed(NetworkRunner runner, NetAddress address, NetConnectFailedReason reason) { }
            public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessions) { }
            public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
            public virtual void OnHostMigration(NetworkRunner runner, HostMigrationToken token) { }
            public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
            public void OnSceneLoadStart(NetworkRunner runner) { }
            public void OnSceneLoadDone(NetworkRunner runner) { }
        }
    }
}
