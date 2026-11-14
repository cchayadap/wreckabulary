using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Wreckabulary.Tests
{
    public sealed class LobbyNavigationLayoutTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly string[] Keys = { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey };
        string[] saved;
        float volume;
        int vsync;
        LobbyMenu menu;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saved = Keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            volume = AudioListener.volume; vsync = QualitySettings.vSyncCount;
            yield return TestScenes.Reset();
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.WaitUntil(() => LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning,
                2f, "responsive lobby ready");
            menu = LobbyMenu.Instance;
            menu.Career.Coins = 0;
            menu.RefreshBar();
            menu.Open(LobbyMenu.Home);
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.Reset();
            for (int i = 0; i < Keys.Length; i++)
                if (saved[i] == null) PlayerPrefs.DeleteKey(Keys[i]); else PlayerPrefs.SetString(Keys[i], saved[i]);
            PlayerPrefs.Save(); AudioListener.volume = volume; QualitySettings.vSyncCount = vsync;
        }

        T Field<T>(string name) => (T)typeof(LobbyMenu).GetField(name, Private).GetValue(menu);
        void Layout() => typeof(LobbyMenu).GetMethod("LayoutBar", Private).Invoke(menu, null);

        [Test]
        public void ReflowKeepsAllNavigationTargetsSeparateInsideTheSafeAreaAndRaycastable()
        {
            WithLayout(width =>
            {
                var safe = Field<RectTransform>("safe");
                var bar = Field<RectTransform>("topBar");
                var buttons = bar.GetComponentsInChildren<Button>().Where(button => button.IsInteractable()).ToArray();
                CollectionAssert.AreEquivalent(new[] { "Home", "Settings", "Quit", "LOADOUT", "PLAY", "CAREER", "Leaderboard", "Shop", "Party" },
                    buttons.Select(button => button.name));
                var bounds = buttons.Select(button => Bounds(safe, (RectTransform)button.transform)).ToArray();
                for (int i = 0; i < buttons.Length; i++)
                {
                    AssertInside(safe.rect, bounds[i], width + " safe bounds " + buttons[i].name);
                    AssertInside(Bounds(safe, bar), bounds[i], width + " bar bounds " + buttons[i].name);
                    for (int j = i + 1; j < buttons.Length; j++)
                        Assert.IsFalse(Inset(bounds[i], .1f).Overlaps(Inset(bounds[j], .1f)), width + " overlap: " + buttons[i].name + " / " + buttons[j].name);
                    var rect = (RectTransform)buttons[i].transform;
                    var hits = new List<RaycastResult>();
                    var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                    EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
                    Assert.IsNotEmpty(hits, width + " raycast " + buttons[i].name);
                    Assert.AreSame(buttons[i], hits[0].gameObject.GetComponentInParent<Button>(), width + " top raycast " + buttons[i].name);
                }
            });
        }

        [Test]
        public void ReflowMovesPagesPartyFeedAndCountdownBelowTheWholeNavigationBar()
        {
            WithLayout(width =>
            {
                var safe = Field<RectTransform>("safe");
                float barBottom = Bounds(safe, Field<RectTransform>("topBar")).yMin;
                foreach (string name in new[] { "pageHost", "rail", "feed", "status" })
                {
                    var rect = Field<RectTransform>(name);
                    Assert.LessOrEqual(Bounds(safe, rect).yMax, barBottom - 4f, width + " keeps " + name + " below all navigation");
                }
                float expected = width < 1760 ? 140 : 76;
                Assert.That(Field<RectTransform>("topBar").rect.height, Is.EqualTo(expected).Within(.01f));
            });
        }

        [Test]
        public void ReflowPreservesSelectedControlAndLocalChoiceWithVisibleShopAndLocalLabels()
        {
            var selected = Field<RectTransform>("pageBar").GetComponentsInChildren<Button>().Single(button => button.name == "LOADOUT");
            EventSystem.current.SetSelectedGameObject(selected.gameObject);
            string mode = menu.Mode, map = menu.Map, queue = menu.Queue;
            int humans = menu.PartySize;
            int previousCoins = menu.Career.Coins;
            try
            {
                menu.Career.Coins = 999999;
                menu.RefreshBar();
                WithLayout(width =>
                {
                    Assert.AreSame(selected.gameObject, EventSystem.current.currentSelectedGameObject, width + " focus");
                    Assert.AreEqual(mode, menu.Mode); Assert.AreEqual(map, menu.Map); Assert.AreEqual(queue, menu.Queue); Assert.AreEqual(humans, menu.PartySize);
                    Assert.AreEqual("CUSTOMIZE", selected.GetComponentInChildren<TMP_Text>().text);
                    Assert.AreEqual("Local " + humans + "/4", Field<TextMeshProUGUI>("partyCount").text);
                    var shop = Field<Button>("shopButton");
                    Assert.IsTrue(shop.GetComponentsInChildren<TMP_Text>().Any(text => text.text == "SHOP"));
                    foreach (var label in shop.GetComponentsInChildren<TMP_Text>())
                    {
                        label.ForceMeshUpdate();
                        Assert.IsFalse(label.isTextOverflowing, width + " shop text " + label.text);
                        AssertInside(((RectTransform)shop.transform).rect, Bounds((RectTransform)shop.transform, label.rectTransform), width + " shop label bounds");
                    }
                });
            }
            finally { menu.Career.Coins = previousCoins; menu.RefreshBar(); Canvas.ForceUpdateCanvases(); }
        }

        void WithLayout(Action<float> check)
        {
            var safe = Field<RectTransform>("safe");
            var canvas = menu.GetComponent<Canvas>();
            var scaler = menu.GetComponent<CanvasScaler>();
            var min = safe.anchorMin; var max = safe.anchorMax; var size = safe.sizeDelta; var position = safe.anchoredPosition; var pivot = safe.pivot;
            bool scalerEnabled = scaler.enabled;
            float scale = canvas.scaleFactor;
            var selected = EventSystem.current.currentSelectedGameObject;
            try
            {
                scaler.enabled = false;
                canvas.scaleFactor = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
                safe.anchorMin = safe.anchorMax = safe.pivot = new Vector2(.5f, .5f);
                safe.anchoredPosition = Vector2.zero;
                foreach (float width in new[] { 1920f, 1728f, 1440f, 1760f, 1759f, 1920f })
                {
                    safe.sizeDelta = new Vector2(width, 1080);
                    Canvas.ForceUpdateCanvases(); Layout(); Canvas.ForceUpdateCanvases();
                    Assert.AreSame(selected, EventSystem.current.currentSelectedGameObject, width + " layout alone preserves focus");
                    check(width);
                }
            }
            finally
            {
                safe.anchorMin = min; safe.anchorMax = max; safe.pivot = pivot; safe.sizeDelta = size; safe.anchoredPosition = position;
                canvas.scaleFactor = scale; scaler.enabled = scalerEnabled;
                Canvas.ForceUpdateCanvases(); Layout(); Canvas.ForceUpdateCanvases();
            }
        }

        static Rect Bounds(RectTransform parent, RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var points = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
        }

        static Rect Inset(Rect rect, float amount) => Rect.MinMaxRect(rect.xMin + amount, rect.yMin + amount, rect.xMax - amount, rect.yMax - amount);

        static void AssertInside(Rect outer, Rect inner, string label)
        {
            Assert.GreaterOrEqual(inner.xMin, outer.xMin - .1f, label + " left");
            Assert.GreaterOrEqual(inner.yMin, outer.yMin - .1f, label + " bottom");
            Assert.LessOrEqual(inner.xMax, outer.xMax + .1f, label + " right");
            Assert.LessOrEqual(inner.yMax, outer.yMax + .1f, label + " top");
        }
    }
}
