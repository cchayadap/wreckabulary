using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    [Explicit, Category("LobbyNavigationCapture")]
    public sealed class LobbyNavigationCaptureTests
    {
        readonly Dictionary<string, string> preferences = new();
        readonly List<string> frames = new();
        UIRefinementCaptureTests.GameViewScope view;
        bool priorAsync, completed;
        bool? priorFocus;
        float priorCapture;
        string directory;
        int width = 1600;

        [Serializable] sealed class Manifest { public string engine, utc; public bool completed; public string[] frames; }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            foreach (string key in new[] { LobbyThemes.PreferenceKey, LobbyMenu.OutfitKey, MatchTally.CareerKey, LobbyMenu.TurnHintKey })
                preferences[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
            priorAsync = EditorSettings.asyncShaderCompilation;
            priorFocus = GameHud.PauseOnFocusLossOverride;
            priorCapture = Time.captureDeltaTime;
            EditorSettings.asyncShaderCompilation = false;
            GameHud.PauseOnFocusLossOverride = false;
            Time.captureDeltaTime = 1f / 60f;
            view = new UIRefinementCaptureTests.GameViewScope(); view.Select(width, 900);
            directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/reviews/evidence/unity-lobby-navigation-2026-10-09"));
            Directory.CreateDirectory(directory);
            yield return TestScenes.Reset();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.Reset();
            foreach (var entry in preferences)
                if (entry.Value == null) PlayerPrefs.DeleteKey(entry.Key); else PlayerPrefs.SetString(entry.Key, entry.Value);
            PlayerPrefs.Save(); LobbyThemes.Restore();
            EditorSettings.asyncShaderCompilation = priorAsync;
            GameHud.PauseOnFocusLossOverride = priorFocus;
            Time.captureDeltaTime = priorCapture;
            view?.Dispose();
            File.WriteAllText(Path.Combine(directory, "navigation-journey.json"), JsonUtility.ToJson(new Manifest
            { engine = Application.unityVersion, utc = DateTime.UtcNow.ToString("O"), completed = completed, frames = frames.ToArray() }, true));
        }

        [UnityTest]
        public IEnumerator CaptureUnobscuredThemesAndNavigation()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return new WaitForSecondsRealtime(.8f);
            var menu = LobbyMenu.Instance;
            menu.Open(LobbyMenu.Home);
            foreach (var theme in LobbyThemes.All)
            {
                Assert.IsTrue(LobbyThemes.Preview(theme.Id));
                foreach (int frameWidth in new[] { 1600, 2100 })
                {
                    width = frameWidth; view.Select(width, 900);
                    yield return Capture("home-" + theme.Id + "-" + width);
                }
            }
            width = 1600; view.Select(width, 900);
            foreach (var target in new[] { ("Shop", LobbyMenu.Shop), ("Leaderboard", LobbyMenu.Trophy), ("PLAY", LobbyMenu.Play) })
            {
                menu.Open(LobbyMenu.Home);
                var button = menu.GetComponentsInChildren<Button>().Single(b => b.name == target.Item1);
                button.onClick.Invoke();
                Assert.AreEqual(target.Item2, menu.Current);
                yield return Capture("open-" + target.Item2);
            }
            menu.Choose(queue: LobbyMenu.Practice, mode: "MovingDay"); menu.Open(LobbyMenu.Home);
            Assert.AreEqual("MovingDay", menu.Mode);
            yield return Capture("home-selected-mode");
            completed = true;
        }

        IEnumerator Capture(string name)
        {
            Canvas.ForceUpdateCanvases(); yield return new WaitForSecondsRealtime(.4f);
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Assert.AreEqual(width, texture.width); Assert.AreEqual(900, texture.height);
                string file = $"{frames.Count + 1:00}-{name}.png";
                File.WriteAllBytes(Path.Combine(directory, file), texture.EncodeToPNG()); frames.Add(file);
                Debug.Log("[LobbyNavigationCapture] " + file);
            }
            finally { Object.Destroy(texture); }
        }
    }
}
