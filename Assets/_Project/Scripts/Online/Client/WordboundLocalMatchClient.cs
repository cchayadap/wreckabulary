using System;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

namespace Wreckabulary.Online
{
    [DisallowMultipleComponent]
    public sealed class WordboundLocalMatchClient : MonoBehaviour
    {
        public const string LocalActor = "local-player";
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        WordboundGameplayController gameplay;
        WordboundLocalSession session;
        WordboundPlayerView playerView;
        WordboundMatchHud hud;
        bool started, leaving, returned, stopped;
        bool shutdownFailed;
        const string ShutdownFailure = "The local game could not close. Restart Wordbound to continue.";
        string returnLabel = "Return to workshop";
        public WordboundGameplayController Gameplay => gameplay;
        public bool ShutdownConfirmed => !started || stopped;
        public event Action Returned;
        public event Action ReturnedToWorkshop { add => Returned += value; remove => Returned -= value; }
        public event Action<string> Failed;

        public async Task StartLocal(IWordboundPuzzleContent content, WordboundGameplayPrefabs prefabs,
            SceneRef gameplayScene, InputBinding controls = null,
            WordboundCameraProfile cameraProfile = null, WordboundVisualProfile visualProfile = null, WordboundUiTheme uiTheme = null,
            WordboundStoryProfile storyProfile = null, string returnLabel = "Return to workshop")
        {
            if (started) throw new InvalidOperationException("Create a fresh local client for each game.");
            if (content == null || !content.BindsRules || !gameplayScene.IsValid)
                throw new ArgumentException("A complete local course and registered gameplay scene are required.");
            if (string.IsNullOrWhiteSpace(returnLabel)) throw new ArgumentException("A return destination is required.", nameof(returnLabel));
            this.returnLabel = returnLabel;
            started = true;
            try
            {
                gameplay = gameObject.AddComponent<WordboundGameplayController>();
                playerView = gameObject.AddComponent<WordboundPlayerView>();
                var artwork = gameObject.AddComponent<WordboundGameplayPresentation>();
                var options = new WordboundGameplayOptions {
                    GameplayScene = gameplayScene,
                    ReadMovement = () => playerView.ReadMovement(),
                    LocalPlayerChanged = body => playerView.LocalPlayerChanged(body),
                    ConfigureVisual = body => artwork.ConfigureVisual(body)
                };
                var binding = WordboundLocalSession.CreateBinding(content.Mode, content.Course,
                    content.CatalogueHash, LocalActor);
                content.Validate(binding, content.Rules);
                gameplay.InitializeLocal(binding, content.Mode, content.Course, content.Geometry,
                    content.Items, content.Rules, LocalActor, prefabs, options);
                var puzzles = new WordboundPuzzleRuntime(gameplay, content);
                options.BeforeHostPhysics = puzzles.BeforeHostPhysics;
                options.ObserveHost = puzzles.ObserveHost;
                options.PuzzleEvaluator = puzzles.Evaluate;
                options.MovementEnvironment = puzzles.EvaluateMovementEnvironment;
                playerView.Initialize(gameplay, controls: controls, cameraProfile: cameraProfile);
                gameObject.AddComponent<WordboundPuzzleGuide>().Bind(gameplay, content.Course,
                    LocalActor, playerView.Camera);
                artwork.Initialize(new WordboundVisualResolver(content.Items, profile: visualProfile));
                artwork.Bind(gameplay);
                artwork.Failed += DisplayFailed;
                hud = gameObject.AddComponent<WordboundMatchHud>();
                hud.Theme = uiTheme;
                hud.StoryProfile = storyProfile;
                hud.Initialize(gameplay, playerView, content.Items, content.Mode, content.Course,
                    LocalActor, returnLabel);
                hud.ReturnRequested += RequestReturn;
                session = new WordboundLocalSession(CreateRunner, WordboundFusionConfiguration.CreateLocal(),
                    gameplay.Authority, LocalActor);
                gameplay.AttachSession(session);
                gameplay.Faulted += GameplayFailed;
                session.GameplayStarted += EnterWorld;
                await session.StartLocal(lifetime.Token);
            }
            catch { await StopGameplay(); throw; }
        }

        NetworkRunner CreateRunner()
        {
            var root = new GameObject("Wordbound local game");
            var runner = root.AddComponent<NetworkRunner>();
            root.AddComponent<NetworkSceneManagerDefault>();
            return runner;
        }

        void EnterWorld() { if (!leaving && playerView) playerView.SetMenuOpen(false); }
        void GameplayFailed(string reason) => Failed?.Invoke("The local game stopped. " + returnLabel + " to continue.");
        void DisplayFailed(string reason)
        {
            if (playerView) playerView.SetMenuOpen(true);
            if (hud) hud.ShowFailure("The local world could not be displayed. " + returnLabel + ".");
            _ = StopGameplay();
            Failed?.Invoke("The local world could not be displayed. " + returnLabel + ".");
        }
        public void ShowStartupFailure()
        {
            shutdownFailed = true;
            if (hud) hud.ShowFailure(ShutdownFailure);
            if (playerView) playerView.SetMenuOpen(true);
        }
        async void RequestReturn()
        {
            try { await ReturnToMenu(); }
            catch
            {
                ShowStartupFailure();
                Failed?.Invoke(ShutdownFailure);
            }
        }
        public Task ReturnToWorkshop() => ReturnToMenu();

        public async Task ReturnToMenu()
        {
            if (leaving || returned) return;
            leaving = true;
            try
            {
                await StopGameplay();
                returned = true;
                Returned?.Invoke();
            }
            finally { leaving = false; }
        }
        async Task StopGameplay()
        {
            if (playerView) playerView.SetMenuOpen(true);
            if (gameplay) await gameplay.Stop();
            else if (session != null) await session.Stop();
            stopped = true;
        }
        void OnGUI()
        {
            if (!shutdownFailed || hud) return;
            float width = Mathf.Min(540, Screen.width - 32);
            GUILayout.BeginArea(new Rect((Screen.width - width) * .5f, 40, width, 100), GUI.skin.box);
            GUILayout.Label(ShutdownFailure, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.EndArea();
        }
        void OnDestroy()
        {
            lifetime.Cancel();
            if (gameplay) gameplay.Faulted -= GameplayFailed;
            if (session != null) session.GameplayStarted -= EnterWorld;
            _ = StopGameplay();
        }
    }
}
