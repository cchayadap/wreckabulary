using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wreckabulary.Tests
{
    public static class TestScenes
    {
        public const string EmptyScenePath = "Assets/_Project/Tests/Empty.unity";

        public static IEnumerator Reset()
        {
            Session.Clear();
            Time.timeScale = 1f;
            SceneManager.sceneLoaded -= AddFixtureListener;
            SceneManager.sceneLoaded += AddFixtureListener;
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                EmptyScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
        }

        static void AddFixtureListener(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != EmptyScenePath || UnityEngine.Object.FindAnyObjectByType<AudioListener>()) return;
            var listener = new GameObject("Test scene audio listener", typeof(AudioListener));
            SceneManager.MoveGameObjectToScene(listener, scene);
        }

        public static IEnumerator ExploreHouse()
        {
            var hud = UnityEngine.Object.FindAnyObjectByType<GameHud>();
            Assert.IsNotNull(hud);
            Assert.IsNotNull(hud.StateController);
            hud.StateController.TransitionToState(UIState.GameplayHUD);
            yield return WaitUntil(() => hud.AcceptsGameplayInput, 1f, "gameplay HUD transition");
        }

        public static IEnumerator Load(string scene)
        {
            yield return SceneManager.LoadSceneAsync(scene);
            yield return null;
        }

        public static IEnumerator WaitForActive(string scene, float timeout = 5f)
        {
            float until = Time.realtimeSinceStartup + timeout;
            while (SceneManager.GetActiveScene().name != scene)
            {
                if (Time.realtimeSinceStartup > until) Assert.Fail($"Timed out waiting for scene {scene}");
                yield return null;
            }
            yield return null;
        }

        public static IEnumerator WaitUntil(Func<bool> condition, float timeout, string what)
        {
            float until = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > until) Assert.Fail($"Timed out waiting for: {what}");
                yield return null;
            }
        }
    }
}
