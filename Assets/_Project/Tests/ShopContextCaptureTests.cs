using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    [Explicit, Category("ShopContextCapture")]
    public sealed class ShopContextCaptureTests
    {
        static readonly string[] Keys = { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey, LobbyThemes.PreferenceKey };
        readonly List<Frame> frames = new();
        readonly Dictionary<string, FileProof> files = new(StringComparer.Ordinal);
        readonly List<(ScriptableObject asset, string json, bool dirty)> assets = new();
        string[] preferences;
        string directory, priorTheme;
        UIRefinementCaptureTests.GameViewScope gameView;
        bool priorAsync, captured, preferencesRestored, filesPreserved, globalsRestored;
        bool? priorFocus;
        float priorTimeScale, priorCaptureStep, priorVolume;
        int priorVsync, priorTargetFrameRate;
        MatchRecord priorResult;
        LobbyMenu menu;
        ShopPage shop;

        [Serializable] sealed class Frame
        {
            public string file, state, category, selected, recipe, title, ownership, action, theme, outfitSha256, sha256;
            public int width, height, frame, coins, bytes;
            public bool itemPreview, selectedActionEnabled, applicationFocused;
            public Rect panel, viewport, selectedFooter;
        }
        [Serializable] sealed class FileProof { public string path, sha256, dependency; }
        [Serializable] sealed class Receipt
        {
            public string status, engine, utc, scope;
            public bool syntheticProfile = true, preferencesRestored, filesPreserved, globalsRestored;
            public Frame[] frames;
            public FileProof[] inputs;
            public string[] cleanupErrors;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            preferences = Keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            priorTheme = LobbyThemes.Current.Id;
            priorAsync = EditorSettings.asyncShaderCompilation; priorFocus = GameHud.PauseOnFocusLossOverride;
            priorTimeScale = Time.timeScale; priorCaptureStep = Time.captureDeltaTime;
            priorVolume = AudioListener.volume; priorVsync = QualitySettings.vSyncCount;
            priorTargetFrameRate = Application.targetFrameRate; priorResult = MatchTally.LastResult;
            Assert.AreNotEqual(UnityEngine.Rendering.GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType,
                "Shop capture needs the graphics-enabled native Play test gate.");
            directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../BuildNotes/creator-clarity-20261029/shop/capture/runs/" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8)));
            Assert.IsFalse(Directory.Exists(directory));
            Directory.CreateDirectory(directory);
            WriteNew("started.json", System.Text.Encoding.UTF8.GetBytes("{\"status\":\"IN PROGRESS\"}"));
            foreach (string path in new[] {
                "Assets/_Project/Scripts/UI/Lobby/ShopPage.cs", "Assets/_Project/Scripts/UI/Lobby/LobbyShopPageView.cs",
                "Assets/_Project/Scripts/UI/Lobby/LobbyShopOfferView.cs", "Assets/_Project/Scripts/UI/Lobby/LobbyPages.cs",
                "Assets/_Project/Scripts/UI/Lobby/LobbyStage.cs", "Assets/_Project/Scripts/Rules/Career.cs",
                "Assets/_Project/Scripts/Rules/CosmeticBundles.cs", "Assets/_Project/Tests/ShopContextCaptureTests.cs",
                "Assets/_Project/Tests/UIRefinementCaptureTests.cs", "Assets/_Project/Tests/TestScenes.cs",
                "Assets/_Project/Data/Config/items.json", "Assets/_Project/Data/Config/wardrobe.json" }) Protect(path);
            ProtectResource(Resources.Load<LobbyShopLayout>(LobbyShopLayout.ResourcePath));
            ProtectResource(Resources.Load<LobbyTypographyProfile>(LobbyTypographyProfile.ResourcePath));
            ProtectResource(Resources.Load<LobbyInterfaceTheme>(LobbyInterfaceTheme.ResourcePath));
            ProtectResource(ModelLibrary.Load()); ProtectResource(MaterialLibrary.Load());
            var layout = Resources.Load<LobbyShopLayout>(LobbyShopLayout.ResourcePath);
            layout.Validate(); Protect(AssetDatabase.GetAssetPath(layout.Page)); Protect(AssetDatabase.GetAssetPath(layout.Offer));
            foreach (var look in CosmeticBundles.All) Protect(AssetDatabase.GetAssetPath(Resources.Load<Texture2D>("UI/Shop/Looks/" + look.Id)));
            foreach (var theme in LobbyThemes.All) Protect(AssetDatabase.GetAssetPath(theme.Background));
            EditorSettings.asyncShaderCompilation = false; GameHud.PauseOnFocusLossOverride = false;
            Time.captureDeltaTime = 1f / 60f;
            gameView = new UIRefinementCaptureTests.GameViewScope(); gameView.Select(1280, 720);
            yield return TestScenes.Reset();
            foreach (string key in Keys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.SetString(MatchTally.CareerKey, new Career { Coins = 0 }.Serialize());
            PlayerPrefs.SetString(LobbyMenu.TurnHintKey, "seen");
            LobbyThemes.Restore(); MatchTally.LastResult = null;
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.WaitUntil(() => LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning, 5f, "Shop capture lobby");
            menu = LobbyMenu.Instance; menu.Open(LobbyMenu.Shop); shop = menu.Page<ShopPage>();
        }

        [UnityTest]
        public IEnumerator CaptureShopSelectionOwnershipAndAllCategories()
        {
            Assert.AreEqual("look:sunroom", shop.Spotlit);
            string before = menu.Outfit.Serialize();
            yield return Pair("looks-default", ShopPage.Looks, "look:sunroom");
            Assert.AreEqual(before, menu.Outfit.Serialize());
            Submit("Selected shop action");
            Assert.AreEqual(0, menu.Career.Coins);
            Assert.AreNotEqual(before, menu.Outfit.Serialize(), "The free owned look is equipped only through its explicit action.");
            string equipped = menu.Outfit.Serialize();
            yield return Pair("looks-equipped", ShopPage.Looks, "look:sunroom");
            Assert.IsFalse(Find("Selected shop action").interactable);
            Submit("Offer look:candy");
            yield return Pair("looks-locked-preview", ShopPage.Looks, "look:candy");
            Assert.IsFalse(menu.Career.Owns("look", "candy"));
            Assert.AreEqual(equipped, menu.Outfit.Serialize());
            Assert.IsTrue(shop.SelectRecipe("SHIELD")); Submit("Offer skin:Arcade");
            yield return Pair("gear-shield", ShopPage.Gear, "skin:Arcade");
            Assert.IsNotNull(menu.Stage.ItemPreview); Assert.AreEqual("SHIELD", shop.SelectedRecipe);
            Submit("Shop category colours");
            yield return Pair("colours", ShopPage.Colours, "colour:sky");
            Assert.IsNull(menu.Stage.ItemPreview);
            Submit("Shop category rooms"); Submit("Offer theme:candy");
            yield return Pair("rooms-preview", ShopPage.Rooms, "theme:candy");
            Assert.AreEqual("candy", LobbyThemes.Current.Id);
            Submit("Shop category extras");
            yield return Pair("extras", ShopPage.Extras, null);
            Assert.AreEqual("sunroom", LobbyThemes.Current.Id);
            Assert.AreEqual(equipped, menu.Outfit.Serialize()); Assert.AreEqual(0, menu.Career.Coins);
            Assert.AreEqual("SHIELD", shop.SelectedRecipe); Assert.IsNull(menu.Stage.ItemPreview);
            Assert.AreEqual(14, frames.Count);
            CollectionAssert.AreEquivalent(frames.Select(frame => frame.file).ToArray(), Directory.GetFiles(directory, "*.png").Select(Path.GetFileName).ToArray());
            captured = true;
        }

        IEnumerator Pair(string state, string category, string selected)
        {
            yield return Capture(state, category, selected, 1280, 720);
            yield return Capture(state, category, selected, 854, 480);
        }

        IEnumerator Capture(string state, string category, string selected, int width, int height)
        {
            int before = Time.frameCount;
            gameView.Select(width, height);
            yield return TestScenes.WaitUntil(() => Screen.width == width && Screen.height == height && Time.frameCount >= before + 3,
                5f, "actual GameView dimensions and three rendered frames " + width + "x" + height);
            yield return new WaitForSecondsRealtime(.35f);
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            Assert.AreEqual(width, Screen.width); Assert.AreEqual(height, Screen.height);
            Assert.AreEqual(LobbyMenu.Shop, menu.Current); Assert.AreEqual(category, shop.Category); Assert.AreEqual(selected, shop.Spotlit);
            var page = shop.Root.GetComponentInChildren<LobbyShopPageView>();
            Assert.IsNotNull(page);
            var panel = Pixels(page.Panel); var viewport = Pixels(page.Scroll.viewport);
            InScreen(panel, width, height, "Shop panel");
            var footer = shop.Root.GetComponentsInChildren<RectTransform>().SingleOrDefault(rect => rect.name == "Selected shop offer");
            var footerPixels = footer ? Pixels(footer) : default;
            Button action = null;
            if (selected != null)
            {
                Assert.IsNotNull(footer); action = Find("Selected shop action");
                InScreen(footerPixels, width, height, "Selected offer");
                Assert.LessOrEqual(footerPixels.yMax, viewport.yMin + .5f, "Selection must not cover the scrolling offers.");
                InScreen(Pixels((RectTransform)action.transform), width, height, "Selected action");
                foreach (var label in footer.GetComponentsInChildren<TMP_Text>())
                { label.ForceMeshUpdate(); Assert.IsFalse(label.isTextTruncated, label.name + " at " + width); }
            }
            else Assert.IsNull(footer, "Extras keeps its direct free-accessory controls.");
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            Assert.IsNotNull(texture);
            try
            {
                Assert.AreEqual(width, texture.width); Assert.AreEqual(height, texture.height);
                var colours = new HashSet<Color32>();
                for (int y = 1; y < 6; y++) for (int x = 1; x < 9; x++) colours.Add(texture.GetPixel(x * width / 9, y * height / 6));
                Assert.Greater(colours.Count, 8, "Capture needs actual varied native pixels.");
                byte[] png = texture.EncodeToPNG();
                string file = frames.Count.ToString("00") + "-" + state + "-" + width + "x" + height + ".png";
                WriteNew(file, png);
                frames.Add(new Frame { file = file, state = state, category = category, selected = selected,
                    recipe = shop.SelectedRecipe, title = Label("Selected shop name"), ownership = Label("Selected shop state"),
                    action = action ? action.GetComponentInChildren<TMP_Text>().text : "", selectedActionEnabled = action && action.interactable,
                    applicationFocused = Application.isFocused,
                    theme = LobbyThemes.Current.Id, itemPreview = menu.Stage.ItemPreview, coins = menu.Career.Coins,
                    outfitSha256 = Hash(System.Text.Encoding.UTF8.GetBytes(menu.Outfit.Serialize())),
                    width = texture.width, height = texture.height, frame = Time.frameCount, bytes = png.Length, sha256 = Hash(png),
                    panel = panel, viewport = viewport, selectedFooter = footerPixels });
            }
            finally { Object.Destroy(texture); }
        }

        Button Find(string name) => shop.Root.GetComponentsInChildren<Button>().Single(button => button.name == name);
        void Submit(string name)
        {
            var button = Find(name); Assert.IsTrue(button.interactable, name);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }
        string Label(string name) => shop.Root.GetComponentsInChildren<TMP_Text>().SingleOrDefault(text => text.name == name)?.text ?? "";
        static Rect Pixels(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>();
            var camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var a = RectTransformUtility.WorldToScreenPoint(camera, corners[0]); var b = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }
        static void InScreen(Rect rect, int width, int height, string name)
        {
            Assert.Greater(rect.width, 0, name); Assert.Greater(rect.height, 0, name);
            Assert.GreaterOrEqual(rect.xMin, -.5f, name); Assert.GreaterOrEqual(rect.yMin, -.5f, name);
            Assert.LessOrEqual(rect.xMax, width + .5f, name); Assert.LessOrEqual(rect.yMax, height + .5f, name);
        }
        void ProtectResource(ScriptableObject asset)
        {
            Assert.IsNotNull(asset); Protect(AssetDatabase.GetAssetPath(asset));
            assets.Add((asset, JsonUtility.ToJson(asset), EditorUtility.IsDirty(asset)));
        }
        void Protect(string path)
        {
            Assert.IsFalse(string.IsNullOrEmpty(path)); Assert.IsTrue(File.Exists(path), path);
            files[path] = new FileProof { path = path, sha256 = Hash(File.ReadAllBytes(path)), dependency = AssetDatabase.GetAssetDependencyHash(path).ToString() };
            if (File.Exists(path + ".meta")) files[path + ".meta"] = new FileProof { path = path + ".meta", sha256 = Hash(File.ReadAllBytes(path + ".meta")), dependency = "" };
        }
        static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        void WriteNew(string name, byte[] bytes) { using var stream = new FileStream(Path.Combine(directory, name), FileMode.CreateNew, FileAccess.Write); stream.Write(bytes, 0, bytes.Length); }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var errors = new List<string>();
            bool resetCompleted = false;
            void Attempt(Action action) { try { action(); } catch (Exception error) { errors.Add(error.ToString()); } }
            try { yield return TestScenes.Reset(); resetCompleted = true; }
            finally
            {
                if (!resetCompleted) errors.Add("The owned test-scene reset did not complete.");
                Attempt(() => {
                    if (preferences == null) return;
                    for (int i = 0; i < Keys.Length; i++)
                        if (preferences[i] == null) PlayerPrefs.DeleteKey(Keys[i]); else PlayerPrefs.SetString(Keys[i], preferences[i]);
                    PlayerPrefs.Save(); LobbyThemes.Restore(); LobbyThemes.Preview(priorTheme);
                    CollectionAssert.AreEqual(preferences, Keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray());
                    Assert.AreEqual(priorTheme, LobbyThemes.Current.Id); preferencesRestored = true;
                });
                Attempt(() => gameView?.Dispose());
                Attempt(() => {
                    EditorSettings.asyncShaderCompilation = priorAsync; GameHud.PauseOnFocusLossOverride = priorFocus;
                    Time.captureDeltaTime = priorCaptureStep; Time.timeScale = priorTimeScale;
                    AudioListener.volume = priorVolume; QualitySettings.vSyncCount = priorVsync;
                    Application.targetFrameRate = priorTargetFrameRate; MatchTally.LastResult = priorResult;
                    Assert.AreEqual(priorAsync, EditorSettings.asyncShaderCompilation); Assert.AreEqual(priorFocus, GameHud.PauseOnFocusLossOverride);
                    Assert.AreEqual(priorCaptureStep, Time.captureDeltaTime); Assert.AreEqual(priorTimeScale, Time.timeScale);
                    Assert.AreEqual(priorVolume, AudioListener.volume); Assert.AreEqual(priorVsync, QualitySettings.vSyncCount);
                    Assert.AreEqual(priorTargetFrameRate, Application.targetFrameRate); Assert.AreSame(priorResult, MatchTally.LastResult);
                    globalsRestored = true;
                });
                Attempt(() => {
                    foreach (var file in files.Values)
                    {
                        Assert.AreEqual(file.sha256, Hash(File.ReadAllBytes(file.path)), file.path);
                        if (file.dependency.Length != 0) Assert.AreEqual(file.dependency, AssetDatabase.GetAssetDependencyHash(file.path).ToString(), file.path);
                    }
                    foreach (var saved in assets)
                    { Assert.AreEqual(saved.json, JsonUtility.ToJson(saved.asset)); Assert.AreEqual(saved.dirty, EditorUtility.IsDirty(saved.asset)); }
                    filesPreserved = true;
                });
                if (directory != null) Attempt(() => WriteNew("receipt.json", System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Receipt {
                    status = captured && errors.Count == 0 && preferencesRestored && filesPreserved && globalsRestored ? "CAPTURED_NOT_VISUALLY_APPROVED" : "FAILED_OR_INCOMPLETE",
                    engine = Application.unityVersion, utc = DateTime.UtcNow.ToString("O"),
                    scope = "Actual saved Shop UI and native stage; temporary zero-coin fixture profile; normal GameView rendering at recorded dimensions. No physical-input, Windows-player, purchase-persistence or online acceptance claim. TestScenes resets the isolated test session.",
                    preferencesRestored = preferencesRestored, filesPreserved = filesPreserved, globalsRestored = globalsRestored,
                    frames = frames.ToArray(), inputs = files.Values.OrderBy(file => file.path, StringComparer.Ordinal).ToArray(), cleanupErrors = errors.ToArray()
                }, true))));
            }
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }
    }
}
