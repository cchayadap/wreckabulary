using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using NUnit.Framework;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;
using Assert = NUnit.Framework.Assert;
using LogType = UnityEngine.LogType;

namespace Wreckabulary.PhotonAuthTests
{
    public sealed class WordboundPhotonAuthenticationTests
    {
        const string Gate = "WORDBOUND_EXTERNAL_PHOTON_AUTH";
        const string Origin = "https://wreckabulary-wordbound-coordinator.pagna.workers.dev";
        const string AppId = "f6fb7f4c-b8a9-44a3-ac63-bee1c5f23552";
        const string RunnerPrefix = "WordboundPhotonAuthProof-";
        const string ProofScene = "Assets/_Project/Tests/Photon/WordboundPhotonProof.unity";
        const string PriorScenesKey = "Wreckabulary.PhotonAuthProof.PriorScenes";
        const string PlayStartedKey = "Wreckabulary.PhotonAuthProof.PlayStarted";
        readonly List<WordboundHttpTransport> transports = new List<WordboundHttpTransport>();
        readonly List<OnlineCoordinatorClient> clients = new List<OnlineCoordinatorClient>();
        readonly List<Task> pending = new List<Task>();
        CancellationTokenSource lifetime;
        AuthenticationLogScope expectedLogs;

        enum Probe { ValidIdentities, Anonymous, InvalidTicket }

        [UnityTest, Category("ExternalPhotonAuth")]
        public IEnumerator CustomAuthenticationUsesCoordinatorIdentities() => Run(Probe.ValidIdentities);

        [UnityTest, Category("ExternalPhotonAuth")]
        public IEnumerator AnonymousConnectionIsRejected() => Run(Probe.Anonymous);

        [UnityTest, Category("ExternalPhotonAuth")]
        public IEnumerator InvalidCustomTicketIsRejected() => Run(Probe.InvalidTicket);

        IEnumerator Run(Probe probe)
        {
            if (Environment.GetEnvironmentVariable(Gate) != "1")
                Assert.Ignore("NOT RUN: configure the production Photon custom provider, then set " + Gate + "=1.");
            Assert.IsFalse(EditorApplication.isPlayingOrWillChangePlaymode, "Start the authentication fixture from Edit mode.");
            var config = ReadConfig();
            int sceneIndex = PrepareScene();
            SessionState.SetBool(PlayStartedKey, true);
            yield return new EnterPlayMode();
            lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(180));
            var sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(SceneRef.FromIndex(sceneIndex), LoadSceneMode.Additive);
            string room = "wordbound-auth-proof-" + Guid.NewGuid().ToString("N");
            byte[] token = Encoding.ASCII.GetBytes(Guid.NewGuid().ToString("N"));

            var hostClient = NewCoordinator(config.origin);
            yield return Wait(hostClient.CreateGuest("AuthProofHost", lifetime.Token), "Create host guest", 20);
            var hostTicketTask = hostClient.PhotonTicket(lifetime.Token);
            yield return Wait(hostTicketTask, "Issue host Photon ticket", 20);
            var hostAuth = Authentication(hostTicketTask.Result, config.photonAppId);
            var hostCallbacks = new AuthCallbacks(token);
            var host = NewRunner("Host", hostCallbacks);
            var hostStart = Start(host, GameMode.Host, hostAuth, config.photonAppId, room, token, sceneInfo);
            yield return Wait(hostStart, "Start authenticated host", 60);
            Assert.IsTrue(hostStart.Result.Ok, "Authenticated host failed: " + hostStart.Result.ShutdownReason);
            Assert.IsTrue(host.IsServer && host.IsRunning, "Positive authentication control did not start a host.");
            yield return WaitFor(() => hostCallbacks.Joined.ContainsKey(host.LocalPlayer), "Observe authenticated host identity", 10);
            Assert.IsTrue(host.GetPlayerUserId(host.LocalPlayer) == hostClient.Credentials.PlayerId &&
                hostCallbacks.Joined[host.LocalPlayer] == hostClient.Credentials.PlayerId,
                "Photon's authenticated host identity must equal the coordinator guest UUID.");

            if (probe == Probe.ValidIdentities)
            {
                var peerClient = NewCoordinator(config.origin);
                yield return Wait(peerClient.CreateGuest("AuthProofClient", lifetime.Token), "Create client guest", 20);
                var ticketTask = peerClient.PhotonTicket(lifetime.Token);
                yield return Wait(ticketTask, "Issue client Photon ticket", 20);
                var auth = Authentication(ticketTask.Result, config.photonAppId);
                string untrustedUserId = "client-asserted-" + Guid.NewGuid().ToString("N");
                auth.UserId = untrustedUserId;
                var peerCallbacks = new AuthCallbacks(token);
                var peer = NewRunner("Client", peerCallbacks);
                var start = Start(peer, GameMode.Client, auth, config.photonAppId, room, token, sceneInfo);
                yield return Wait(start, "Start authenticated client", 60);
                Assert.IsTrue(start.Result.Ok, "Authenticated client failed: " + start.Result.ShutdownReason);
                yield return WaitFor(() => host.ActivePlayers.Count() == 2 && peer.ActivePlayers.Count() == 2 &&
                    hostCallbacks.Joined.Count == 2, "Observe both authenticated seats", 15);
                var expected = new[] { hostClient.Credentials.PlayerId, peerClient.Credentials.PlayerId };
                Assert.IsTrue(expected.Distinct(StringComparer.Ordinal).Count() == 2, "The coordinator must issue distinct guests.");
                Assert.IsTrue(hostCallbacks.Joined.Values.OrderBy(x => x, StringComparer.Ordinal)
                    .SequenceEqual(expected.OrderBy(x => x, StringComparer.Ordinal)),
                    "Host callbacks must expose exactly the two server-minted coordinator identities.");
                Assert.IsTrue(host.ActivePlayers.Select(host.GetPlayerUserId).OrderBy(x => x, StringComparer.Ordinal)
                    .SequenceEqual(expected.OrderBy(x => x, StringComparer.Ordinal)),
                    "The host's live player map must match the authenticated callback identities.");
                Assert.IsTrue(peer.GetPlayerUserId(peer.LocalPlayer) == peerClient.Credentials.PlayerId &&
                    peer.GetPlayerUserId(peer.LocalPlayer) != untrustedUserId,
                    "A caller-supplied UserId must not replace the custom provider's coordinator identity.");
                Debug.Log("WORDBOUND_PHOTON_AUTH_OBSERVED: two authenticated coordinator identities; cleanup pending.");
            }
            else
            {
                var denied = new AuthenticationValues("untrusted-" + Guid.NewGuid().ToString("N"));
                if (probe == Probe.InvalidTicket)
                {
                    denied.AuthType = CustomAuthenticationType.Custom;
                    denied.SetAuthPostData(new Dictionary<string, object> { ["ticket"] = "invalid-" + Guid.NewGuid().ToString("N") });
                }
                else denied.AuthType = CustomAuthenticationType.None;
                var callbacks = new AuthCallbacks(token);
                var peer = NewRunner(probe.ToString(), callbacks);
                int priorRequests = hostCallbacks.ConnectRequests;
                StartGameResult result;
                expectedLogs = new AuthenticationLogScope();
                try
                {
                    var start = Start(peer, GameMode.Client, denied, config.photonAppId, room, token, sceneInfo);
                    yield return Wait(start, "Reject " + probe, 60);
                    result = start.Result;
                }
                finally { expectedLogs.Dispose(); }
                Assert.AreEqual(0, expectedLogs.UnexpectedErrors, "Unexpected non-authentication engine errors occurred during the negative probe.");
                Assert.IsFalse(result.Ok, probe + " unexpectedly connected; production custom authentication is not enforced.");
                Assert.IsTrue(result.ShutdownReason == ShutdownReason.CustomAuthenticationFailed ||
                    result.ShutdownReason == ShutdownReason.InvalidAuthentication,
                    "Rejection must be authentication-specific, not timeout, capacity, room policy or cancellation: " + result.ShutdownReason);
                Assert.IsTrue(!peer || !peer.IsRunning, "Rejected credentials must not leave a running client.");
                Assert.AreEqual(0, callbacks.Joined.Count, "Rejected credentials must never acquire a player seat.");
                Assert.AreEqual(priorRequests, hostCallbacks.ConnectRequests, "Invalid credentials must be rejected before host connection admission.");
                Assert.IsTrue(host.IsRunning && host.ActivePlayers.Count() == 1, "The authenticated positive-control host must remain healthy.");
                Debug.Log("WORDBOUND_PHOTON_AUTH_OBSERVED: " + probe + " rejected as " + result.ShutdownReason + "; cleanup pending.");
            }
        }

        OnlineCoordinatorClient NewCoordinator(string origin)
        {
            var transport = new WordboundHttpTransport(new Uri(origin));
            transports.Add(transport);
            var client = new OnlineCoordinatorClient(transport);
            clients.Add(client);
            return client;
        }

        static AuthenticationValues Authentication(OnlinePhotonTicket ticket, string appId)
        {
            Assert.IsTrue(ticket.AppId == appId, "Coordinator ticket belongs to another Photon app.");
            Assert.IsTrue(ticket.ExpiresAtMilliseconds > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 5000,
                "Coordinator ticket must be fresh before starting a Runner.");
            var result = new AuthenticationValues { AuthType = CustomAuthenticationType.Custom };
            result.SetAuthPostData(ticket.PhotonPostData());
            return result;
        }

        static NetworkRunner NewRunner(string label, AuthCallbacks callbacks)
        {
            var root = new GameObject(RunnerPrefix + label + "-" + Guid.NewGuid().ToString("N"));
            var runner = root.AddComponent<NetworkRunner>();
            runner.AddCallbacks(callbacks);
            root.AddComponent<NetworkSceneManagerDefault>();
            return runner;
        }

        Task<StartGameResult> Start(NetworkRunner runner, GameMode mode, AuthenticationValues auth, string appId,
            string room, byte[] token, NetworkSceneInfo scenes)
        {
            try
            {
                return runner.StartGame(new StartGameArgs
                {
                    GameMode = mode, SessionName = room, PlayerCount = 2, IsVisible = false, IsOpen = true,
                    EnableClientSessionCreation = false, DisableNATPunchthrough = true, ConnectionToken = token,
                    AuthValues = auth, Config = new NetworkProjectConfig { PeerMode = NetworkProjectConfig.PeerModes.Multiple },
                    Scene = scenes, SceneManager = runner.GetComponent<NetworkSceneManagerDefault>(),
                    CustomPhotonAppSettings = new FusionAppSettings
                    {
                        AppIdFusion = appId, AppVersion = "wordbound-auth-proof-v1",
                        FixedRegion = "asia", UseNameServer = true
                    },
                    StartGameCancellationToken = lifetime.Token
                });
            }
            catch (Exception error)
            {
                Assert.Fail("Runner startup threw " + error.GetType().Name + "; raw SDK error text is intentionally omitted.");
                throw new InvalidOperationException("Unreachable.");
            }
        }

        IEnumerator Wait(Task task, string operation, double seconds)
        {
            pending.Add(task);
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline && !lifetime.IsCancellationRequested)
                yield return null;
            if (!task.IsCompleted)
            {
                lifetime.Cancel();
                Assert.Fail(operation + " exceeded its deadline.");
            }
            Assert.IsFalse(task.IsCanceled, operation + " was cancelled.");
            if (task.IsFaulted)
            {
                var error = task.Exception?.GetBaseException();
                string detail = error is OnlineServiceException service ? "HTTP " + service.Status + " " + service.Code :
                    error?.GetType().Name ?? "Unknown";
                Assert.Fail(operation + " failed: " + detail + ". Raw request/response and exception text are omitted.");
            }
        }

        IEnumerator WaitFor(Func<bool> complete, string operation, double seconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!complete() && Time.realtimeSinceStartupAsDouble < deadline && !lifetime.IsCancellationRequested)
                yield return null;
            Assert.IsTrue(complete(), operation + " exceeded its deadline.");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            expectedLogs?.Dispose();
            lifetime?.Cancel();
            foreach (var client in clients) client.ClearCredentials();
            foreach (var transport in transports) transport.Dispose();
            clients.Clear(); transports.Clear();
            double pendingDeadline = Time.realtimeSinceStartupAsDouble + 5;
            while (pending.Any(task => !task.IsCompleted) && Time.realtimeSinceStartupAsDouble < pendingDeadline) yield return null;
            int unfinished = pending.Count(task => !task.IsCompleted);
            foreach (var task in pending.Where(task => task.IsFaulted)) _ = task.Exception;
            pending.Clear();
            int failures = 0;
            foreach (var runner in OwnedRunners().OrderBy(r => r.IsServer).ToArray())
            {
                Task shutdown = null;
                try { shutdown = runner.Shutdown(); }
                catch { failures++; }
                if (shutdown != null)
                {
                    double deadline = Time.realtimeSinceStartupAsDouble + 15;
                    while (!shutdown.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    if (!shutdown.IsCompleted || shutdown.IsCanceled) failures++;
                    if (shutdown.IsFaulted) { _ = shutdown.Exception; failures++; }
                }
                if (runner) UnityEngine.Object.Destroy(runner.gameObject);
            }
            if (Application.isPlaying) yield return null;
            bool leaked = OwnedRunners().Any();
            lifetime?.Dispose(); lifetime = null;
            if (Application.isPlaying && SessionState.GetBool(PlayStartedKey, false)) yield return new ExitPlayMode();
            SessionState.EraseBool(PlayStartedKey);
            string prior = SessionState.GetString(PriorScenesKey, "");
            if (prior != "")
            {
                var saved = JsonUtility.FromJson<SavedScenes>(prior);
                EditorBuildSettings.scenes = saved.paths.Select((path, index) => new EditorBuildSettingsScene(path, saved.enabled[index])).ToArray();
                SessionState.EraseString(PriorScenesKey);
            }
            Assert.AreEqual(0, unfinished, "Cancelled authentication/startup work did not finish within cleanup's bound.");
            Assert.AreEqual(0, failures, "A proof-owned Runner did not shut down cleanly.");
            Assert.IsFalse(leaked, "A proof-owned Runner remained after destruction.");
        }

        static IEnumerable<NetworkRunner> OwnedRunners() => Resources.FindObjectsOfTypeAll<NetworkRunner>()
            .Where(r => r && r.gameObject.scene.IsValid() && r.name.StartsWith(RunnerPrefix, StringComparison.Ordinal));

        static ProductionClient ReadConfig()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../OnlineServices/production-client.json"));
            Assert.IsTrue(File.Exists(path), "Integrate the verified non-secret production-client.json before this gated fixture.");
            var config = JsonUtility.FromJson<ProductionClient>(File.ReadAllText(path));
            Assert.IsTrue(config != null && config.origin == Origin && config.photonAppId == AppId &&
                config.issuer == "wreckabulary-wordbound" && config.keyId == "wordbound-rs256-20261020-01",
                "The native proof requires the approved production coordinator and Photon app.");
            return config;
        }

        static int PrepareScene()
        {
            Assert.IsTrue(File.Exists(ProofScene), "Create the native proof scene with WordboundPhotonSetup.CreateProofScene first.");
            Assert.AreEqual("", SessionState.GetString(PriorScenesKey, ""), "Previous authentication proof scene settings need recovery.");
            var previous = EditorBuildSettings.scenes;
            SessionState.SetString(PriorScenesKey, JsonUtility.ToJson(new SavedScenes
            {
                paths = previous.Select(s => s.path).ToArray(), enabled = previous.Select(s => s.enabled).ToArray()
            }));
            var scenes = previous.Where(s => s.path != ProofScene).Concat(new[] { new EditorBuildSettingsScene(ProofScene, true) }).ToArray();
            EditorBuildSettings.scenes = scenes;
            return scenes.Count(s => s.enabled) - 1;
        }

        [Serializable]
        sealed class ProductionClient
        {
            public string origin = "", issuer = "", keyId = "", photonAppId = "";
        }
        [Serializable]
        sealed class SavedScenes
        {
            public string[] paths;
            public bool[] enabled;
        }

        sealed class AuthenticationLogScope : IDisposable
        {
            readonly bool previous = LogAssert.ignoreFailingMessages;
            bool disposed;
            public int UnexpectedErrors { get; private set; }
            public AuthenticationLogScope()
            {
                Application.logMessageReceived += Observe;
                LogAssert.ignoreFailingMessages = true;
            }
            void Observe(string message, string stack, LogType type)
            {
                if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
                if (message.IndexOf("authenticat", StringComparison.OrdinalIgnoreCase) < 0) UnexpectedErrors++;
            }
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                Application.logMessageReceived -= Observe;
                LogAssert.ignoreFailingMessages = previous;
            }
        }

        sealed class AuthCallbacks : INetworkRunnerCallbacks
        {
            readonly byte[] token;
            public readonly Dictionary<PlayerRef, string> Joined = new Dictionary<PlayerRef, string>();
            public int ConnectRequests { get; private set; }
            public AuthCallbacks(byte[] token) { this.token = token; }
            public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { Joined[player] = runner.GetPlayerUserId(player); }
            public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] supplied)
            {
                ConnectRequests++;
                if (supplied != null && supplied.SequenceEqual(token)) request.Accept(); else request.Refuse();
            }
            public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
            public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
            public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
            public void OnInput(NetworkRunner runner, NetworkInput input) { }
            public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
            public void OnShutdown(NetworkRunner runner, ShutdownReason reason) { }
            public void OnConnectedToServer(NetworkRunner runner) { }
            public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
            public void OnConnectFailed(NetworkRunner runner, NetAddress address, NetConnectFailedReason reason) { }
            public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessions) { }
            public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
            public void OnHostMigration(NetworkRunner runner, HostMigrationToken token) { }
            public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
            public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
            public void OnSceneLoadStart(NetworkRunner runner) { }
            public void OnSceneLoadDone(NetworkRunner runner) { }
        }
    }
}
