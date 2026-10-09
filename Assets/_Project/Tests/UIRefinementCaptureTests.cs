using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    [Explicit, Category("UIRefinementCapture")]
    public sealed class UIRefinementCaptureTests
    {
        static readonly string[] PreferenceKeys = { LobbyThemes.PreferenceKey, MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey,
            KeyBindings.OverridesKey, KeyBindings.LegacyBackupKey };
        readonly List<Frame> frames = new();
        readonly Dictionary<string, string> preferences = new();
        GameViewScope gameView;
        bool priorAsync, completed;
        bool? priorFocus;
        float priorCaptureStep;
        string directory, priorPreview;
        int width = 1600, height = 900;

        sealed class LocalInput : InputBinding
        {
            public override string Id => "ui-native-capture-local";
            public override bool CanLook => true;
            public override void Read(ref PlayerCommands commands) { }
            public override bool JoinPressed() => false;
            public override bool StartPressed() => false;
        }

        [Serializable] sealed class Frame
        {
            public string file, scene, page, theme, uiState, phase, view;
            public int width, height, localSeats, mapRooms;
            public bool artwork, itemPreview, thirdPerson, bagOpen, paused, pauseSettings, listening;
        }

        [Serializable] sealed class Manifest
        {
            public string engine, utc, capture = "Normal camera rendering; native ScreenCapture at end of frame";
            public bool completed;
            public Frame[] frames;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            foreach (string key in PreferenceKeys) preferences[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
            priorPreview = LobbyThemes.Current.Id;
            priorAsync = EditorSettings.asyncShaderCompilation;
            priorFocus = GameHud.PauseOnFocusLossOverride;
            priorCaptureStep = Time.captureDeltaTime;
            EditorSettings.asyncShaderCompilation = false;
            GameHud.PauseOnFocusLossOverride = false;
            Time.captureDeltaTime = 1f / 60f;
            directory = Environment.GetEnvironmentVariable("WRECKABULARY_UI_CAPTURE_DIRECTORY") ??
                Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/reviews/evidence/unity-ui-2026-10-09"));
            Directory.CreateDirectory(directory);
            frames.Clear(); completed = false;
            gameView = new GameViewScope();
            Resize(1600, 900);
            yield return TestScenes.Reset();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            KeyBindings.Stop();
            yield return TestScenes.Reset();
            foreach (var pair in preferences)
                if (pair.Value == null) PlayerPrefs.DeleteKey(pair.Key); else PlayerPrefs.SetString(pair.Key, pair.Value);
            PlayerPrefs.Save();
            LobbyThemes.Restore(); LobbyThemes.Preview(priorPreview);
            EditorSettings.asyncShaderCompilation = priorAsync;
            GameHud.PauseOnFocusLossOverride = priorFocus;
            Time.captureDeltaTime = priorCaptureStep;
            try { gameView?.Dispose(); }
            finally
            {
                gameView = null;
                if (!string.IsNullOrEmpty(directory)) File.WriteAllText(Path.Combine(directory, "ui-journey.json"), JsonUtility.ToJson(new Manifest
                { engine = Application.unityVersion, utc = DateTime.UtcNow.ToString("O"), completed = completed, frames = frames.ToArray() }, true));
            }
        }

        [UnityTest]
        public IEnumerator CaptureThemesShopRecipesAndPauseSettings()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return new WaitForSecondsRealtime(.8f);
            var menu = LobbyMenu.Instance;
            Assert.NotNull(menu);
            menu.Open(LobbyMenu.Home);
            foreach (var theme in LobbyThemes.All)
            {
                Assert.IsTrue(LobbyThemes.Preview(theme.Id));
                yield return Settle();
                Assert.IsTrue(menu.Stage.ArtworkShown);
                Assert.NotNull(menu.Stage.Avatar);
                Assert.AreSame(theme.Background, menu.Stage.GetComponentsInChildren<RawImage>().Single(i => i.name == "Theme artwork").texture);
                yield return Capture("home-" + theme.Id);
            }
            LobbyThemes.Preview("sunroom");
            menu.Open(LobbyMenu.Shop);
            var shop = menu.Page<ShopPage>();
            yield return Settle();
            yield return Reveal(menu.transform, "Offer theme:candy");
            yield return Capture("shop-themes");
            shop.Preview(Career.Shop.First(offer => offer.Id == "look:candy"));
            yield return Settle();
            yield return Reveal(menu.transform, "Offer look:candy");
            yield return Capture("shop-wardrobe-candy-preview");
            Assert.IsTrue(shop.SelectRecipe("SHIELD"));
            shop.Preview(Career.Shop.First(offer => offer.Id == "skin:Arcade"));
            yield return Settle();
            yield return Reveal(menu.transform, "Offer skin:Arcade");
            Assert.NotNull(menu.Stage.ItemPreview);
            var planes = GeometryUtility.CalculateFrustumPlanes(menu.Stage.Camera);
            Assert.IsTrue(menu.Stage.ItemPreview.GetComponentsInChildren<Renderer>().Any(r => r.enabled && !r.forceRenderingOff && GeometryUtility.TestPlanesAABB(planes, r.bounds)));
            yield return Capture("shop-shield-arcade-native-preview");
            menu.Open(LobbyMenu.Settings);
            menu.Page<SettingsPage>().ShowTab("controls");
            yield return Settle();
            yield return Capture("lobby-controls");

            Session.Clear(); Session.SelectMap("pinwheel"); Match.ModeOverride = "Dibs";
            yield return TestScenes.Load(Session.DibsScene);
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var player = joins.Join(new LocalInput());
            var rounds = RoundManager.Instance;
            rounds.CountdownTime = .1f; rounds.StartMatch();
            yield return TestScenes.WaitUntil(() => rounds.Phase == Phase.Playing, 3f, "active solo round");
            foreach (var p in World.Players)
                if (p != player) { p.Frozen = true; if (p.TryGetComponent<BotController>(out var bot)) bot.enabled = false; }
            var builder = Object.FindAnyObjectByType<RoomBuilder>();
            var room = builder.Layout.Rooms.OrderByDescending(r => (r.MaxX - r.MinX) * (r.MaxZ - r.MinZ)).First();
            var centre = new Vector3((room.MinX + room.MaxX) * .5f, room.FloorY + .08f, (room.MinZ + room.MaxZ) * .5f);
            bool Inside(Vector3 at) => at.x > room.MinX + .6f && at.x < room.MaxX - .6f && at.z > room.MinZ + .6f && at.z < room.MaxZ - .6f;
            player.Respawn(ShoulderView.RoomySpot(centre, Inside)); player.Frozen = false;
            player.Inventory.Set("APPLEWATER");
            Assert.IsTrue(player.Combat.TryEquip(CatalogGear.Create(GameConfig.Current.Items.Get("SHIELD"))));
            Assert.IsTrue(player.Combat.TryEquip(CatalogGear.Create(GameConfig.Current.Items.Get("SODA"))));
            Physics.SyncTransforms();
            yield return new WaitForSeconds(3.2f);
            var hud = Object.FindAnyObjectByType<GameHud>();
            Assert.AreSame(player, hud.LocalPlayer);
            Assert.IsTrue(Camera.main.GetComponent<CameraRig>().IsThirdPerson);
            Assert.AreEqual(Phase.Playing, rounds.Phase);
            yield return Capture("solo-tray-detailed-minimap");
            Click(hud.transform, "Bag link");
            yield return Settle();
            Assert.IsTrue(hud.BagOpen);
            var recipes = hud.transform.Find("Safe HUD/Bag panel/Recipe book/View").GetComponent<ScrollRect>();
            foreach (int w in new[] { 1600, 2100 })
            {
                Resize(w, 900); yield return Settle();
                recipes.verticalNormalizedPosition = 1f; yield return Settle();
                yield return Capture("recipes-first-" + w);
                recipes.verticalNormalizedPosition = 0f; yield return Settle();
                Assert.Greater(recipes.content.rect.height, recipes.viewport.rect.height);
                yield return Capture("recipes-last-" + w);
            }
            Resize(1280, 720); yield return Settle();
            yield return Capture("recipes-compact-1280");
            Resize(1600, 900); yield return Settle();
            Click(hud.transform, "Close bag"); yield return Settle();
            Assert.IsFalse(hud.BagOpen);
            hud.TogglePause(); yield return Settle();
            Assert.IsTrue(hud.Paused);
            yield return Capture("pause-main");
            Click(hud.transform, "Pause controls"); yield return Settle();
            Assert.IsTrue(hud.PauseSettingsShown);
            yield return Capture("pause-controls");
            yield return Reveal(hud.transform, "Rebind Jump");
            Click(hud.transform, "Rebind Jump"); yield return null;
            var controls = hud.GetComponentsInChildren<ControlsPanel>().Single(panel => panel.IsListening);
            Assert.IsTrue(KeyBindings.Listening);
            yield return Capture("pause-controls-listening");
            Assert.IsTrue(controls.CancelRebind());
            Assert.IsFalse(KeyBindings.Listening);
            Click(hud.transform, "Pause tab audio"); yield return Settle();
            yield return Capture("pause-audio");
            Click(hud.transform, "Pause tab graphics"); yield return Settle();
            yield return Capture("pause-graphics");
            completed = true;
        }

        void Resize(int w, int h) { width = w; height = h; gameView.Select(w, h); }
        static IEnumerator Settle() { Canvas.ForceUpdateCanvases(); yield return new WaitForSecondsRealtime(.3f); }

        static void Click(Transform root, string name)
        {
            var button = root.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == name && b.IsInteractable());
            Assert.NotNull(button, name + " must be a real interactable UI button");
            button.onClick.Invoke();
        }

        static IEnumerator Reveal(Transform root, string name)
        {
            var target = root.GetComponentsInChildren<RectTransform>().FirstOrDefault(t => t.name == name);
            Assert.NotNull(target, name);
            Canvas.ForceUpdateCanvases();
            var scroll = target.GetComponentInParent<ScrollRect>();
            if (scroll && scroll.content.rect.height > scroll.viewport.rect.height)
            {
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.content, target);
                float belowTop = scroll.content.rect.yMax - bounds.center.y;
                scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01((belowTop - scroll.viewport.rect.height * .5f) / (scroll.content.rect.height - scroll.viewport.rect.height));
            }
            yield return Settle();
        }

        IEnumerator Capture(string view)
        {
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            Assert.NotNull(Camera.main);
            var pixels = ScreenCapture.CaptureScreenshotAsTexture();
            Assert.NotNull(pixels);
            try
            {
                Assert.AreEqual(width, pixels.width); Assert.AreEqual(height, pixels.height);
                var colours = new HashSet<Color32>();
                for (int y = 1; y < 6; y++) for (int x = 1; x < 9; x++) colours.Add(pixels.GetPixel(x * width / 9, y * height / 6));
                Assert.Greater(colours.Count, 8, "Native pixels must contain a rendered scene and UI.");
                string file = $"{frames.Count + 1:00}-{view}.png";
                File.WriteAllBytes(Path.Combine(directory, file), pixels.EncodeToPNG());
                var hud = GameHud.Active;
                var menu = LobbyMenu.Instance;
                var map = hud ? hud.transform.Find("Safe HUD/Side column/Minimap/Floor/House plan/Rooms") : null;
                frames.Add(new Frame
                {
                    file = file, scene = SceneManager.GetActiveScene().name, view = view, page = menu ? menu.Current : "gameplay",
                    theme = LobbyThemes.Current.Id, uiState = hud && hud.StateController ? hud.StateController.CurrentState.ToString() : "none",
                    phase = RoundManager.Instance ? RoundManager.Instance.Phase.ToString() : "lobby",
                    width = width, height = height, localSeats = World.Players.Count(CameraRig.IsHuman), mapRooms = map ? map.childCount : 0,
                    artwork = menu && menu.Stage && menu.Stage.ArtworkShown, itemPreview = menu && menu.Stage && menu.Stage.ItemPreview,
                    thirdPerson = Camera.main.TryGetComponent<CameraRig>(out var rig) && rig.IsThirdPerson,
                    bagOpen = hud && hud.BagOpen, paused = hud && hud.Paused, pauseSettings = hud && hud.PauseSettingsShown, listening = KeyBindings.Listening
                });
                Debug.Log("[UIRefinementCapture] " + file);
            }
            finally { Object.Destroy(pixels); }
        }

        internal sealed class GameViewScope : IDisposable
        {
            const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            readonly object group;
            readonly EditorWindow view, previousFocus;
            readonly PropertyInfo selected;
            readonly Type sizeType, kindType;
            readonly int previousIndex, previousTotal;
            readonly List<(int index, int width, int height)> created = new();

            public GameViewScope()
            {
                previousFocus = EditorWindow.focusedWindow;
                var assembly = typeof(Editor).Assembly;
                var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
                var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
                group = sizesType.GetMethod("GetGroup", Members).Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
                previousTotal = (int)Call("GetTotalCount");
                sizeType = assembly.GetType("UnityEditor.GameViewSize");
                kindType = assembly.GetType("UnityEditor.GameViewSizeType");
                var viewType = assembly.GetType("UnityEditor.GameView");
                view = EditorWindow.GetWindow(viewType);
                selected = viewType.GetProperty("selectedSizeIndex", Members);
                previousIndex = (int)selected.GetValue(view);
            }

            object Call(string method, params object[] arguments) => group.GetType().GetMethod(method, Members).Invoke(group, arguments);

            public void Select(int width, int height)
            {
                int found = created.FindIndex(size => size.width == width && size.height == height);
                int index;
                if (found >= 0) index = created[found].index;
                else
                {
                    index = (int)Call("GetTotalCount");
                    var size = Activator.CreateInstance(sizeType, Members, null,
                        new object[] { Enum.Parse(kindType, "FixedResolution"), width, height, "UI refinement capture temporary" }, null);
                    Call("AddCustomSize", size);
                    created.Add((index, width, height));
                }
                selected.SetValue(view, index);
                view.Focus();
            }

            public void Dispose()
            {
                if (view) selected.SetValue(view, previousIndex);
                for (int i = created.Count - 1; i >= 0; i--) Call("RemoveCustomSize", created[i].index);
                created.Clear();
                if (previousFocus) previousFocus.Focus();
                Assert.AreEqual(previousTotal, (int)Call("GetTotalCount"), "Capture removes only its temporary sizes.");
                if (view) Assert.AreEqual(previousIndex, (int)selected.GetValue(view));
            }
        }
    }
}
