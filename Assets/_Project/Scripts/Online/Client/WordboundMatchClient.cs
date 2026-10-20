using System;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    [DisallowMultipleComponent]
    public sealed class WordboundMatchClient : MonoBehaviour
    {
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        OnlineCoordinatorClient coordinator;
        OnlineMatchExpectation expected;
        WordboundFusionSession session;
        WordboundGameplayController gameplay;
        WordboundPlayerView playerView;
        WordboundMatchHud hud;
        GameObject matchRoot;
        bool started, leaving, returned;
        public WordboundGameplayController Gameplay => gameplay;
        public WordboundFusionSession Session => session;
        public bool Leaving => leaving;
        public bool HasMatchContext => expected != null;
        public event Action ReturnedToParty;
        public event Action<string> Failed;

        public async Task StartMatch(OnlineCoordinatorClient client, OnlineAdmissionVerifier verifier,
            VerifiedOnlineMatch verified, ModeDefinition mode, ResolvedCourse course,
            CourseCatalogue geometry, ItemCatalogue items, GameRules rules,
            WordboundGameplayPrefabs prefabs, NetworkPrefabRef snapshotPrefab,
            string photonAppId, SceneRef gameplayScene,
            Func<CancellationToken, Task<WordboundFusionJoin>> freshCredentials,
            IWordboundPuzzleContent physicalPuzzles = null, InputBinding controls = null,
            WordboundCameraProfile cameraProfile = null, WordboundVisualProfile visualProfile = null)
        {
            if (started) throw new InvalidOperationException("Create a fresh match client for each match.");
            if (client?.Credentials == null || verified == null || verifier == null || freshCredentials == null)
                throw new ArgumentException("A verified match and authenticated guest are required.");
            if (!gameplayScene.IsValid || !Guid.TryParse(photonAppId, out _))
                throw new ArgumentException("A registered gameplay scene and Photon app are required.");
            started = true;
            coordinator = client;
            expected = verified.Match;
            matchRoot = new GameObject("Wordbound live match");
            matchRoot.transform.SetParent(transform, false);
            gameplay = matchRoot.AddComponent<WordboundGameplayController>();
            playerView = matchRoot.AddComponent<WordboundPlayerView>();
            var artwork = matchRoot.AddComponent<WordboundGameplayPresentation>();
            var options = new WordboundGameplayOptions {
                GameplayScene = gameplayScene,
                ReadMovement = () => playerView.ReadMovement(),
                LocalPlayerChanged = body => playerView.LocalPlayerChanged(body),
                ConfigureVisual = body => artwork.ConfigureVisual(body)
            };
            try
            {
                gameplay.Initialize(verified, mode, course, geometry, items, rules,
                    client.Credentials.PlayerId, prefabs, options);
                if (physicalPuzzles != null)
                {
                    if (!physicalPuzzles.BindsRules)
                        throw new ArgumentException("Physical courses must bind their gameplay rules.");
                    physicalPuzzles.Validate(gameplay.Authority.Match.Binding, rules);
                    var puzzles = new WordboundPuzzleRuntime(gameplay, physicalPuzzles);
                    options.BeforeHostPhysics = puzzles.BeforeHostPhysics;
                    options.ObserveHost = puzzles.ObserveHost;
                    options.PuzzleEvaluator = puzzles.Evaluate;
                    options.MovementEnvironment = puzzles.EvaluateMovementEnvironment;
                }
                playerView.Initialize(gameplay, controls: controls, cameraProfile: cameraProfile);
                matchRoot.AddComponent<WordboundPuzzleGuide>().Bind(gameplay, course,
                    client.Credentials.PlayerId, playerView.Camera);
                artwork.Initialize(new WordboundVisualResolver(items, profile: visualProfile));
                artwork.Bind(gameplay);
                artwork.Failed += PresentationFailed;
                hud = matchRoot.AddComponent<WordboundMatchHud>();
                hud.Initialize(gameplay, playerView, items, mode, course, client.Credentials.PlayerId);
                hud.ReturnRequested += RequestReturn;
                session = new WordboundFusionSession(CreateRunner,
                    new FusionAppSettings { AppIdFusion = photonAppId, UseNameServer = true },
                    Configuration(), snapshotPrefab, verifier, expected, freshCredentials,
                    gameplay.Authority, new OnlineConnectionService(client, verifier, expected),
                    new OnlineCheckpointService(client, expected, gameplay.Authority.IdentityHash));
                gameplay.AttachSession(session);
                gameplay.Faulted += GameplayFailed;
                session.GameplayStarted += EnterWorld;
                await session.Start(lifetime.Token);
            }
            catch
            {
                await StopGameplay();
                throw;
            }
        }

        NetworkRunner CreateRunner()
        {
            var runnerRoot = new GameObject("Wordbound session");
            var runner = runnerRoot.AddComponent<NetworkRunner>();
            runnerRoot.AddComponent<NetworkSceneManagerDefault>();
            return runner;
        }

        internal static NetworkProjectConfig Configuration()
        {
            var config = new NetworkProjectConfig { PeerMode = NetworkProjectConfig.PeerModes.Multiple };
            foreach (var source in NetworkProjectConfig.Global.PrefabTable.Prefabs)
                config.PrefabTable.AddSource(source);
            config.HostMigration.EnableAutoUpdate = false;
            config.Network.ReliableDataTransferModes = NetworkConfiguration.ReliableDataTransfers.ClientToServer;
            config.Simulation.TickRateSelection = new TickRate.Selection {
                Client = 60, ClientSendInterval = 1, ServerTickInterval = 1, ServerSendInterval = 1
            };
            return config;
        }

        void EnterWorld()
        {
            if (!leaving && playerView) playerView.SetMenuOpen(false);
        }
        void GameplayFailed(string reason) => Failed?.Invoke("The match connection stopped. Return to your party to reconnect.");
        void PresentationFailed(string reason)
        {
            if (playerView) playerView.SetMenuOpen(true);
            _ = StopGameplay();
            Failed?.Invoke("The world could not be displayed. Return to your party and try again.");
        }
        void RequestReturn() => _ = ReturnToParty();

        public async Task ReturnToParty()
        {
            if (leaving || returned || expected == null) return;
            leaving = true;
            try
            {
                await StopGameplay();
                await coordinator.MatchAction(expected, "return", lifetime.Token);
                returned = true;
                ReturnedToParty?.Invoke();
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            catch { Failed?.Invoke("Could not confirm leaving the match. Check your connection and try Return to party again."); }
            finally { leaving = false; }
        }

        async Task StopGameplay()
        {
            if (playerView) playerView.SetMenuOpen(true);
            if (gameplay) await gameplay.Stop();
            else if (session != null) await session.Stop();
        }

        void OnDestroy()
        {
            lifetime.Cancel();
            if (hud) hud.ReturnRequested -= RequestReturn;
            if (gameplay) gameplay.Faulted -= GameplayFailed;
            if (session != null) session.GameplayStarted -= EnterWorld;
            _ = StopGameplay();
        }
    }
}
