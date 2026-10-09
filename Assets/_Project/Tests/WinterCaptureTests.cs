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
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    [Explicit, Category("WinterCapture")]
    public sealed class WinterCaptureTests
    {
        readonly Dictionary<string, string> preferences = new();
        readonly List<Frame> frames = new();
        UIRefinementCaptureTests.GameViewScope view;
        bool priorAsync, completed;
        bool? priorFocus;
        float priorCapture;
        string directory;
        int width = 1600, height = 900;
        [Serializable] sealed class Frame { public string file, page, theme; public int width, height; public bool emote, decor, item; }
        [Serializable] sealed class Manifest { public string engine, utc; public bool completed; public Frame[] frames; }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            foreach (string key in new[] { LobbyThemes.PreferenceKey, LobbyMenu.OutfitKey, MatchTally.CareerKey,
                LobbyMenu.TurnHintKey, GameSoundPacks.PreferenceKey })
                preferences[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
            priorAsync = EditorSettings.asyncShaderCompilation;
            priorFocus = GameHud.PauseOnFocusLossOverride;
            priorCapture = Time.captureDeltaTime;
            EditorSettings.asyncShaderCompilation = false; GameHud.PauseOnFocusLossOverride = false;
            Time.captureDeltaTime = 1f / 60f;
            view = new UIRefinementCaptureTests.GameViewScope(); view.Select(width, height);
            directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/reviews/evidence/unity-winter-2026-10-09"));
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
            EditorSettings.asyncShaderCompilation = priorAsync; GameHud.PauseOnFocusLossOverride = priorFocus;
            Time.captureDeltaTime = priorCapture;
            view?.Dispose();
            File.WriteAllText(Path.Combine(directory, "winter-journey.json"), JsonUtility.ToJson(new Manifest
                { engine = Application.unityVersion, utc = DateTime.UtcNow.ToString("O"), completed = completed, frames = frames.ToArray() }, true));
        }

        [UnityTest]
        public IEnumerator CaptureWinterHomeEmoteFinishesAndSoundSelection()
        {
            Assert.NotNull(SeasonalCollection.Winter);
            var outfit = CosmeticBundles.Find("winter").Apply(GameConfig.Current.Wardrobe.Default, GameConfig.Current.Wardrobe);
            PlayerPrefs.SetString(LobbyMenu.OutfitKey, outfit.Serialize());
            LobbyThemes.Equip("winter", new Career());
            yield return TestScenes.Load(Session.HubScene);
            yield return new WaitForSecondsRealtime(1f);
            var menu = LobbyMenu.Instance;
            menu.Open(LobbyMenu.Home);
            yield return Settle();
            Assert.IsTrue(menu.Stage.GetComponent<LobbyCollectionDecor>().Visible);
            yield return Capture("winter-home");
            Assert.IsTrue(menu.Stage.PlayEmote(SeasonalCollection.Winter.emote));
            yield return new WaitForSecondsRealtime(.35f);
            yield return Capture("winter-shuffle-step");
            yield return new WaitForSecondsRealtime(.32f);
            yield return Capture("winter-shuffle-clap");
            yield return TestScenes.WaitUntil(() => !menu.Stage.IsEmoting, 4f, "dance returns to idle");
            width = 2100; view.Select(width, height); yield return Settle();
            yield return Capture("winter-home-ultrawide");
            width = 1600; view.Select(width, height); yield return Settle();
            menu.Open(LobbyMenu.Shop); yield return Settle();
            yield return Reveal(menu.transform, "Play winter shuffle");
            yield return Capture("winter-collection-shop");
            Click(menu.transform, "Play winter shuffle");
            yield return new WaitForSecondsRealtime(.58f);
            Assert.IsTrue(menu.Stage.IsEmoting);
            yield return Capture("winter-shop-dance");
            Click(menu.transform, "Stop winter shuffle");
            var shop = menu.Page<ShopPage>();
            foreach (string word in new[] { "SHIELD", "SODA" })
            {
                Assert.IsTrue(shop.SelectRecipe(word));
                shop.Preview(new ShopOffer("skin", "Winter", "Winter", 0));
                yield return Settle();
                yield return Reveal(menu.transform, "Offer skin:Winter");
                Assert.NotNull(menu.Stage.ItemPreview);
                yield return Capture("winter-" + word.ToLowerInvariant() + "-preview");
                Click(menu.transform, "Wear skin:Winter");
                Assert.AreEqual("Winter", menu.Outfit.SkinFor(word));
            }
            menu.Open(LobbyMenu.Settings); yield return Settle();
            GameSoundPacks.Select("winter"); menu.Page<SettingsPage>().Refresh();
            yield return Settle();
            yield return Capture("winter-sound-settings");
            menu.Open(LobbyMenu.Play); yield return Settle();
            Assert.IsFalse(menu.Stage.GetComponent<LobbyCollectionDecor>().Visible);
            yield return Capture("winter-map-preview-restored");
            completed = true;
        }

        static IEnumerator Settle() { Canvas.ForceUpdateCanvases(); yield return new WaitForSecondsRealtime(.35f); }
        static void Click(Transform root, string name)
        {
            var button = root.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == name && b.IsInteractable());
            Assert.NotNull(button, name); button.onClick.Invoke();
        }
        static IEnumerator Reveal(Transform root, string name)
        {
            Canvas.ForceUpdateCanvases();
            var target = root.GetComponentsInChildren<RectTransform>().FirstOrDefault(t => t.name == name);
            Assert.NotNull(target, name);
            var scroll = target.GetComponentInParent<ScrollRect>();
            if (scroll && scroll.content.rect.height > scroll.viewport.rect.height)
            {
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.content, target);
                float offset = scroll.content.rect.yMax - bounds.center.y;
                scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01((offset - scroll.viewport.rect.height * .5f) / (scroll.content.rect.height - scroll.viewport.rect.height));
            }
            yield return Settle();
        }
        IEnumerator Capture(string name)
        {
            Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Assert.AreEqual(width, texture.width); Assert.AreEqual(height, texture.height);
                string file = $"{frames.Count + 1:00}-{name}.png";
                File.WriteAllBytes(Path.Combine(directory, file), texture.EncodeToPNG());
                var menu = LobbyMenu.Instance;
                frames.Add(new Frame { file = file, page = menu.Current, theme = LobbyThemes.Current.Id, width = width, height = height,
                    emote = menu.Stage.IsEmoting, decor = menu.Stage.GetComponent<LobbyCollectionDecor>().Visible, item = menu.Stage.ItemPreview });
                Debug.Log("[WinterCapture] " + file);
            }
            finally { Object.Destroy(texture); }
        }
    }
}
