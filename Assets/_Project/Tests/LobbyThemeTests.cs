using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class LobbyThemeTests
    {
        bool hadPreference;
        string preference, previousPreview;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            hadPreference = PlayerPrefs.HasKey(LobbyThemes.PreferenceKey);
            preference = PlayerPrefs.GetString(LobbyThemes.PreferenceKey, "");
            previousPreview = LobbyThemes.Current.Id;
            yield return TestScenes.Reset();
            PlayerPrefs.DeleteKey(LobbyThemes.PreferenceKey);
            LobbyThemes.Restore();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.Reset();
            if (hadPreference) PlayerPrefs.SetString(LobbyThemes.PreferenceKey, preference);
            else PlayerPrefs.DeleteKey(LobbyThemes.PreferenceKey);
            PlayerPrefs.Save();
            LobbyThemes.Restore();
            LobbyThemes.Preview(previousPreview);
        }

        [Test]
        public void PreviewCannotSpendCoinsPersistSelectionOrEquipUnownedThemes()
        {
            var career = new Career { Coins = 999 };
            Assert.AreEqual("sunroom", LobbyThemes.Current.Id);
            Assert.IsTrue(LobbyThemes.Preview("candy"));
            Assert.AreEqual("candy", LobbyThemes.Current.Id);
            Assert.IsFalse(PlayerPrefs.HasKey(LobbyThemes.PreferenceKey));
            Assert.IsFalse(LobbyThemes.Equip("candy", career));
            Assert.AreEqual(999, career.Coins);
            Assert.IsEmpty(career.Owned);
            Assert.IsFalse(LobbyThemes.Preview("unknown"));
            Assert.AreEqual("candy", LobbyThemes.Current.Id);
            LobbyThemes.Restore();
            Assert.AreEqual("sunroom", LobbyThemes.Current.Id);
        }

        [Test]
        public void OwnedSelectionPersistsAndRestoreDiscardsAnotherPreview()
        {
            var career = new Career();
            career.Owned.Add("theme:candy");
            int changes = 0;
            void Changed() => changes++;
            LobbyThemes.Changed += Changed;
            try
            {
                Assert.IsTrue(LobbyThemes.Equip("candy", career));
                Assert.AreEqual("candy", PlayerPrefs.GetString(LobbyThemes.PreferenceKey));
                Assert.IsTrue(LobbyThemes.Preview("lantern"));
                Assert.AreEqual("candy", PlayerPrefs.GetString(LobbyThemes.PreferenceKey));
                LobbyThemes.Restore();
                Assert.AreEqual("candy", LobbyThemes.Current.Id);
                Assert.AreEqual(3, changes, "equip, preview and restore each update live consumers");
                Assert.IsFalse(LobbyThemes.Equip("unknown", career));
                Assert.IsFalse(LobbyThemes.Equip("lantern", null));
                Assert.AreEqual("candy", LobbyThemes.Current.Id);
                Assert.IsTrue(LobbyThemes.Equip("sunroom", null), "the starter theme needs no purchase");
            }
            finally { LobbyThemes.Changed -= Changed; }
        }

        [Test]
        public void InvalidSavedThemeFallsBackToTheFreeSunroom()
        {
            PlayerPrefs.SetString(LobbyThemes.PreferenceKey, "removed-theme");
            LobbyThemes.Restore();
            Assert.AreEqual("sunroom", LobbyThemes.Current.Id);
            CollectionAssert.AreEqual(new[] { 0, 300, 350, 0 }, LobbyThemes.All.Select(theme => theme.Price));
        }

        [UnityTest]
        public IEnumerator ArtworkUsesRealAvatarAndRestoresTheSameMapRenderers()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return null;
            var stage = LobbyMenu.Instance.Stage;
            stage.ShowArtwork(false);
            var map = stage.GetComponentsInChildren<Transform>(true).First(t => t.name.StartsWith("Lobby backdrop"));
            var renderers = map.GetComponentsInChildren<Renderer>(true);
            Assert.IsNotEmpty(renderers);
            var original = renderers.Select(renderer => renderer.forceRenderingOff).ToArray();
            stage.ShowArtwork(true);
            Assert.IsTrue(map.gameObject.activeSelf, "art mode retains the map and its authored structure for preview");
            Assert.IsTrue(renderers.All(renderer => renderer.forceRenderingOff));
            Assert.IsTrue(stage.Avatar.GetComponentsInChildren<Renderer>().Any(renderer => renderer.enabled && !renderer.forceRenderingOff));
            var art = stage.GetComponentsInChildren<RawImage>().Single(image => image.name == "Theme artwork");
            Assert.AreSame(LobbyThemes.Current.Background, art.texture);
            Assert.NotNull(art.texture);
            foreach (var theme in LobbyThemes.All)
            {
                Assert.IsTrue(LobbyThemes.Preview(theme.Id));
                yield return null;
                Assert.AreSame(theme.Background, art.texture);
                Assert.AreEqual(theme.Surface, stage.Camera.backgroundColor);
            }
            stage.ShowArtwork(false);
            for (int i = 0; i < renderers.Length; i++) Assert.AreEqual(original[i], renderers[i].forceRenderingOff);
            Assert.AreSame(map, stage.GetComponentsInChildren<Transform>(true).First(t => t.name.StartsWith("Lobby backdrop")));
            stage.ShowArtwork(true);
            stage.SetMap("terrace");
            yield return null;
            var replacement = stage.GetComponentsInChildren<Transform>(true).First(t => t.name.StartsWith("Lobby backdrop"));
            Assert.AreNotSame(map, replacement);
            Assert.IsTrue(replacement.GetComponentsInChildren<Renderer>(true).All(renderer => renderer.forceRenderingOff));
            stage.ShowArtwork(false);
            Assert.IsTrue(replacement.GetComponentsInChildren<Renderer>(true).Any(renderer => !renderer.forceRenderingOff));
        }

        [UnityTest]
        public IEnumerator BoundedAtmosphereCanStopAndReleaseDisablesTheEntireShowroom()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return null;
            var stage = LobbyMenu.Instance.Stage;
            stage.ShowArtwork(true);
            var effects = stage.GetComponentsInChildren<RectTransform>(true).Single(t => t.name == "Theme atmosphere");
            Assert.AreEqual(14, effects.childCount);
            var child = (RectTransform)effects.GetChild(0);
            stage.SetThemeEffects(false);
            Vector2 at = child.anchorMin;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.IsFalse(effects.gameObject.activeSelf);
            Assert.AreEqual(at, child.anchorMin);
            stage.SetThemeEffects(true);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.IsTrue(effects.gameObject.activeSelf);
            Assert.AreNotEqual(at, child.anchorMin);
            Assert.AreEqual(14, effects.childCount, "theme changes reuse a bounded set of visuals");
            var profile = stage.GetComponentsInChildren<Volume>().Single().sharedProfile;
            Assert.IsTrue(profile.TryGet<Vignette>(out var vignette));
            Assert.AreEqual(0f, vignette.intensity.value);
            Assert.IsTrue(profile.TryGet<DepthOfField>(out var depth));
            Assert.AreEqual(DepthOfFieldMode.Off, depth.mode.value);
            stage.Release();
            Assert.IsFalse(stage.GetComponentsInChildren<Canvas>(true).Single().enabled);
            Assert.IsTrue(stage.GetComponentsInChildren<Light>().All(light => !light.enabled));
            stage.TakeCamera();
            yield return null;
            Assert.AreSame(LobbyThemes.Current.Background, stage.GetComponentsInChildren<RawImage>().Single(image => image.name == "Theme artwork").texture);
            Assert.AreEqual(LobbyThemes.Current.Surface, stage.Camera.backgroundColor);
        }

        [UnityTest]
        public IEnumerator NativeItemAndStandStayClearOfTheActualShopPanelWhileTurning()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return null;
            var menu = LobbyMenu.Instance;
            menu.Open(LobbyMenu.Shop);
            yield return new WaitForSecondsRealtime(.5f);
            var stage = menu.Stage;
            var panel = (RectTransform)menu.Page<ShopPage>().Root.Find("Panel");
            Assert.IsNotNull(panel);
            var corners = new Vector3[4];
            panel.GetWorldCorners(corners);
            var canvas = panel.GetComponentInParent<Canvas>();
            var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            float panelLeft = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]).x;
            Vector3 avatarPosition = stage.Avatar.position;
            Assert.IsTrue(stage.PreviewItem("SHIELD", "Arcade"));
            foreach (float turn in new[] { 0f, 120f, -240f })
            {
                stage.Turn(turn);
                yield return new WaitForSecondsRealtime(.8f);
                foreach (var mesh in stage.ItemPreview.GetComponentsInChildren<MeshFilter>())
                {
                    var bounds = mesh.sharedMesh.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var sign = new Vector3((i & 1) == 0 ? -1f : 1f,
                            (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                        var point = mesh.transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, sign));
                        var screen = stage.Camera.WorldToScreenPoint(point);
                        Assert.Greater(screen.z, 0f);
                        Assert.Greater(screen.x, 0f, mesh.name + " remains on screen");
                        Assert.Less(screen.x, panelLeft - 2f, mesh.name + " clears the actual shop panel, including the stand");
                    }
                }
                Assert.AreEqual(avatarPosition, stage.Avatar.position, "fitting the preview never moves the avatar");
            }
        }

        [UnityTest]
        public IEnumerator ItemFinishPreviewUsesRealMaterialsWithoutGameplayComponentsAndCleansUp()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return null;
            var stage = LobbyMenu.Instance.Stage;
            Assert.IsTrue(stage.PreviewItem("BAT", "Candy"));
            var first = stage.ItemPreview;
            Assert.NotNull(first);
            Assert.IsFalse(first.GetComponentsInChildren<Collider>().Any(collider => collider.enabled));
            Assert.IsEmpty(first.GetComponentsInChildren<HeldWeapon>());
            Assert.IsTrue(first.GetComponentsInChildren<Renderer>().SelectMany(renderer => renderer.sharedMaterials)
                .Any(material => material && material.name.Contains("Candy")), "the preview uses native Candy materials");
            Assert.IsTrue(stage.PreviewItem("BAT", "Candy"));
            Assert.AreSame(first, stage.ItemPreview, "refreshing the card does not rebuild the miniature");
            Assert.IsFalse(stage.PreviewItem("MISSING_ITEM", "Candy"));
            Assert.AreSame(first, stage.ItemPreview);
            Assert.IsTrue(stage.PreviewItem("BAT", "Arcade"));
            Assert.AreNotSame(first, stage.ItemPreview);
            yield return null;
            Assert.IsFalse(first, "replacing the preview disposes the old native model");
            var replacement = stage.ItemPreview;
            stage.Release();
            Assert.IsNull(stage.ItemPreview);
            yield return null;
            Assert.IsFalse(replacement, "leaving the showroom disposes item models and their materials scope");
        }
    }
}
