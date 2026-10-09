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
using UnityEngine.InputSystem;
using TMPro;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    [Explicit, Category("LobbyPartyCapture")]
    public sealed class LobbyPartyCaptureTests
    {
        readonly Dictionary<string, string> preferences = new();
        readonly List<string> frames = new();
        readonly List<Gamepad> pads = new();
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
            foreach (string key in new[] { LobbyThemes.PreferenceKey, LobbyMenu.OutfitKey, MatchTally.CareerKey, LobbyMenu.TurnHintKey, "wv.outfit.1", "wv.outfit.2", "wv.outfit.3" })
                preferences[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
            priorAsync = EditorSettings.asyncShaderCompilation;
            priorFocus = GameHud.PauseOnFocusLossOverride;
            priorCapture = Time.captureDeltaTime;
            EditorSettings.asyncShaderCompilation = false;
            GameHud.PauseOnFocusLossOverride = false;
            Time.captureDeltaTime = 1f / 60f;
            view = new UIRefinementCaptureTests.GameViewScope(); view.Select(width, 900);
            directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/reviews/evidence/unity-lobby-party-2026-10-09"));
            Directory.CreateDirectory(directory);
            yield return TestScenes.Reset();
            foreach (string key in preferences.Keys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.SetString(LobbyMenu.TurnHintKey, "seen");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.Reset();
            foreach (var pad in pads) if (pad.added) InputSystem.RemoveDevice(pad);
            pads.Clear();
            foreach (var entry in preferences)
                if (entry.Value == null) PlayerPrefs.DeleteKey(entry.Key); else PlayerPrefs.SetString(entry.Key, entry.Value);
            PlayerPrefs.Save(); LobbyThemes.Restore();
            EditorSettings.asyncShaderCompilation = priorAsync;
            GameHud.PauseOnFocusLossOverride = priorFocus;
            Time.captureDeltaTime = priorCapture;
            view?.Dispose();
            File.WriteAllText(Path.Combine(directory, "party-journey.json"), JsonUtility.ToJson(new Manifest
            { engine = Application.unityVersion, utc = DateTime.UtcNow.ToString("O"), completed = completed, frames = frames.ToArray() }, true));
        }

        [UnityTest]
        public IEnumerator CapturePaintedControlsAndOneToFourAnimatedPartyMembers()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return new WaitForSecondsRealtime(.8f);
            var menu = LobbyMenu.Instance;
            menu.Open(LobbyMenu.Home);
            var bindings = new List<InputBinding>();
            for (int i = 0; i < 3; i++)
            {
                var pad = InputSystem.AddDevice<Gamepad>(); pads.Add(pad);
                bindings.Add(new GamepadBinding(pad));
            }
            foreach (var theme in LobbyThemes.All)
            {
                Assert.IsTrue(LobbyThemes.Preview(theme.Id));
                foreach (var binding in menu.Party.ToArray()) menu.Leave(binding);
                width = 1600; view.Select(width, 900);
                yield return Capture("solo-" + theme.Id);
                foreach (var binding in bindings) menu.Join(binding);
                foreach (int frameWidth in new[] { 1600, 2100 })
                {
                    width = frameWidth; view.Select(width, 900);
                    yield return Capture("four-" + theme.Id + "-" + width);
                    AssertParty(menu, 4);
                    AssertCompactChrome(menu);
                }
            }
            Assert.IsTrue(LobbyThemes.Equip("winter", menu.Career));
            width = 1600; view.Select(width, 900);
            menu.Leave(bindings[2]);
            yield return Capture("winter-three"); AssertParty(menu, 3);
            menu.Leave(bindings[1]);
            yield return Capture("winter-two"); AssertParty(menu, 2);
            foreach (string page in new[] { LobbyMenu.Loadout, LobbyMenu.Shop, LobbyMenu.Play })
            {
                menu.Open(page); yield return new WaitForSecondsRealtime(.3f);
                AssertParty(menu, 1);
                menu.Close(); yield return new WaitForSecondsRealtime(.3f);
                AssertParty(menu, 2);
            }
            var cachedGuest = menu.Stage.transform.Find("Lobby guest 2");
            menu.Leave(bindings[0]);
            yield return Capture("winter-restored-solo"); AssertParty(menu, 1);
            Assert.AreEqual("winter", LobbyThemes.Current.Id);
            Assert.AreEqual(Vector3.one, menu.Stage.Avatar.localScale);
            menu.Join(bindings[0]); yield return null;
            Assert.AreSame(cachedGuest, menu.Stage.transform.Find("Lobby guest 2"), "rejoining reuses the cached avatar");
            menu.Stage.Release(); yield return null;
            Assert.IsFalse(cachedGuest.gameObject.activeSelf);
            menu.Stage.TakeCamera(); yield return null; AssertParty(menu, 2);
            menu.Choose(queue: LobbyMenu.Practice, mode: "MovingDay");
            var mode = menu.Page<HomePage>().Root.GetComponentsInChildren<Button>().Single(b => b.name == "Choose mode");
            Assert.AreEqual(LobbyMenu.ModeName("MovingDay"), mode.GetComponentInChildren<TMP_Text>().text);
            mode.onClick.Invoke(); yield return null;
            Assert.AreEqual(LobbyMenu.Play, menu.Current);
            menu.Close();
            yield return Capture("winter-selected-mode");
            completed = true;
        }

        static void AssertParty(LobbyMenu menu, int count)
        {
            Assert.AreEqual(count, menu.Stage.PresentedPartySize);
            var roots = new List<Transform> { menu.Stage.Avatar };
            foreach (Transform child in menu.Stage.transform)
                if (child.name.StartsWith("Lobby guest ") && child.gameObject.activeSelf) roots.Add(child);
            Assert.AreEqual(count, roots.Count);
            if (menu.Current != LobbyMenu.Home) return;
            var rectangles = new List<Rect>();
            foreach (var root in roots)
            {
                var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r => r.enabled).ToArray();
                Assert.IsNotEmpty(renderers);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                for (int corner = 0; corner < 8; corner++)
                {
                    var sign = new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                    var point = menu.Stage.Camera.WorldToViewportPoint(bounds.center + Vector3.Scale(bounds.extents, sign));
                    min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                }
                Assert.Greater(min.x, .02f, root.name); Assert.Less(max.x, .98f, root.name);
                Assert.Greater(min.y, .15f, root.name); Assert.Less(max.y, .90f, root.name);
                rectangles.Add(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
            }
            for (int i = 0; i < rectangles.Count; i++)
                for (int j = i + 1; j < rectangles.Count; j++)
                    Assert.IsFalse(rectangles[i].Overlaps(rectangles[j]), "party silhouettes remain separate");
        }

        static void AssertCompactChrome(LobbyMenu menu)
        {
            var bar = menu.transform.Find("Safe area/Top bar");
            var nav = (RectTransform)bar.Find("Pages");
            Assert.LessOrEqual(nav.rect.height, 64);
            Assert.Greater(nav.GetComponent<Image>().color.a, .95f);
            foreach (var label in bar.GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();
                Assert.IsFalse(label.isTextTruncated, label.text);
                Assert.Greater(label.textInfo.characterInfo.Count(c => c.isVisible), 0, label.text);
            }
            var dock = menu.Page<HomePage>().Root.Find("Next match") as RectTransform;
            Assert.LessOrEqual(dock.rect.height, 180);
            Assert.AreEqual(300, dock.rect.width);
            var decor = menu.Stage.GetComponent<LobbyCollectionDecor>();
            if (decor.Visible)
                foreach (var renderer in decor.Instance.GetComponentsInChildren<Renderer>())
                    for (int i = 0; i < 8; i++)
                    {
                        var sign = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                        var point = menu.Stage.Camera.WorldToViewportPoint(renderer.bounds.center + Vector3.Scale(renderer.bounds.extents, sign));
                        Assert.That(point.x, Is.InRange(.005f, .995f), renderer.name + " remains inside the frame");
                    }
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
                Debug.Log("[LobbyPartyCapture] " + file);
            }
            finally { Object.Destroy(texture); }
        }
    }
}
