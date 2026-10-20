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
        const string ShutdownFailure = "The playtest could not close. Restart Wordbound to continue.";
        public WordboundGameplayController Gameplay => gameplay;
        public bool ShutdownConfirmed => !started || stopped;
        public event Action ReturnedToWorkshop;
        public event Action<string> Failed;

        public async Task StartLocal(IWordboundPuzzleContent content, WordboundGameplayPrefabs prefabs,
            SceneRef gameplayScene, InputBinding controls = null)
        {
            if (started) throw new InvalidOperationException("Create a fresh local client for each playtest.");
            if (content == null || !content.BindsRules || !gameplayScene.IsValid)
                throw new ArgumentException("A complete local course and registered gameplay scene are required.");
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
                playerView.Initialize(gameplay, controls: controls);
                artwork.Initialize(new WordboundVisualResolver(content.Items));
                artwork.Bind(gameplay);
                artwork.Failed += DisplayFailed;
                hud = gameObject.AddComponent<WordboundMatchHud>();
                hud.Initialize(gameplay, playerView, content.Items, content.Mode, content.Course,
                    LocalActor, "Return to workshop");
                hud.ReturnRequested += RequestReturn;
                session = new WordboundLocalSession(CreateRunner, WordboundMatchClient.Configuration(),
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
            var root = new GameObject("Wordbound local playtest");
            var runner = root.AddComponent<NetworkRunner>();
            root.AddComponent<NetworkSceneManagerDefault>();
            return runner;
        }

        void EnterWorld() { if (!leaving && playerView) playerView.SetMenuOpen(false); }
        void GameplayFailed(string reason) => Failed?.Invoke("The playtest stopped. Return to your map to continue editing.");
        void DisplayFailed(string reason)
        {
            if (playerView) playerView.SetMenuOpen(true);
            if (hud) hud.ShowFailure("The playtest world could not be displayed. Return to your map.");
            _ = StopGameplay();
            Failed?.Invoke("The playtest world could not be displayed. Return to your map.");
        }
        public void ShowStartupFailure()
        {
            shutdownFailed = true;
            if (hud) hud.ShowFailure(ShutdownFailure);
            if (playerView) playerView.SetMenuOpen(true);
        }
        async void RequestReturn()
        {
            try { await ReturnToWorkshop(); }
            catch
            {
                ShowStartupFailure();
                Failed?.Invoke(ShutdownFailure);
            }
        }
        public async Task ReturnToWorkshop()
        {
            if (leaving || returned) return;
            leaving = true;
            try
            {
                await StopGameplay();
                returned = true;
                ReturnedToWorkshop?.Invoke();
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
