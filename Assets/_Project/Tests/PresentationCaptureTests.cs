using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Wreckabulary.Tests
{
    /// <summary>Explicit rendered-frame acceptance journey; run in a graphics-enabled editor without batchmode.</summary>
    [Explicit, Category("PresentationCapture")]
    public sealed class PresentationCaptureTests
    {
        string directory;
        bool asyncCompilation;

        sealed class CaptureBinding : InputBinding
        {
            static int nextId;
            readonly string id = "presentation-local-" + ++nextId;
            public override string Id => id;
            public override bool CanLook => true;
            public override void Read(ref PlayerCommands commands) => commands = default;
            public override bool JoinPressed() => false;
            public override bool StartPressed() => false;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            directory = Environment.GetEnvironmentVariable("WRECKABULARY_CAPTURE_DIRECTORY") ??
                Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/reviews/evidence/unity-2026-10-08"));
            Directory.CreateDirectory(directory);
            asyncCompilation = EditorSettings.asyncShaderCompilation;
            EditorSettings.asyncShaderCompilation = false;
            SetGameViewSize(1600, 900);
            yield return TestScenes.Reset();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            EditorSettings.asyncShaderCompilation = asyncCompilation;
            yield return TestScenes.Reset();
        }

        [UnityTest]
        public IEnumerator CaptureMenuGameplayPauseAndCouchPlay()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return new WaitForSecondsRealtime(1.2f);
            var hud = UnityEngine.Object.FindAnyObjectByType<GameHud>();
            Assert.NotNull(hud.StateController);
            Assert.AreEqual(UIState.MainMenu, hud.StateController.CurrentState);
            yield return Capture("01-main-menu-16x9");

            var inventoryPreview = new GameObject("Inventory layout capture");
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.sortingOrder = 1000;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.match = 1f;
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/Inventory/InventoryTheme");
            var document = inventoryPreview.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.sortingOrder = 1000;
            document.visualTreeAsset = Resources.Load<VisualTreeAsset>("UI/Inventory/Inventory");
            Assert.NotNull(document.visualTreeAsset);
            yield return new WaitForSecondsRealtime(.4f);
            yield return Capture("08-inventory-16x9");
            SetGameViewSize(2100, 900);
            yield return new WaitForSecondsRealtime(.4f);
            yield return Capture("09-inventory-21x9");
            UnityEngine.Object.Destroy(inventoryPreview);
            UnityEngine.Object.Destroy(panel);
            SetGameViewSize(1600, 900);

            hud.StateController.TransitionToState(UIState.GameplayHUD);
            var joins = UnityEngine.Object.FindAnyObjectByType<PlayerJoinManager>();
            joins.Join(new CaptureBinding());
            yield return new WaitForSecondsRealtime(1f);
            yield return Capture("02-hub-explore-16x9");

            Session.Clear();
            Match.ModeOverride = "Dibs";
            yield return TestScenes.Load(Session.DibsScene);
            joins = UnityEngine.Object.FindAnyObjectByType<PlayerJoinManager>();
            var input = new CaptureBinding();
            var player = joins.Join(input);
            RoundManager.Instance.CountdownTime = .15f;
            RoundManager.Instance.StartMatch();
            yield return new WaitForSeconds(1.3f);
            foreach (var bot in UnityEngine.Object.FindObjectsByType<BotController>(FindObjectsSortMode.None))
                bot.enabled = false;
            foreach (var other in joins.Players)
                if (other != player) other.Frozen = true;
            player.Inventory.Set("BATPLATEFOAMSOAP");
            Assert.IsTrue(player.Summoner.Summon("BAT"));
            Assert.IsTrue(player.Summoner.Summon("PLATE"));
            player.Combat.SwitchGear();
            yield return new WaitForSeconds(2f);
            var camera = Camera.main;
            Assert.IsFalse(camera.orthographic, "A single local player uses perspective third-person framing.");
            var focus = camera.WorldToViewportPoint(player.transform.position + Vector3.up * .8f);
            Assert.That(focus.x, Is.InRange(.37f, .63f), "The player stays in the center of the third-person view.");
            Assert.Greater(focus.z, 0f);
            yield return Capture("03-gameplay-behind-16x9");

            hud = UnityEngine.Object.FindAnyObjectByType<GameHud>();
            hud.TogglePause();
            yield return new WaitForSecondsRealtime(.35f);
            Assert.AreEqual(UIState.PauseMenu, hud.StateController.CurrentState);
            Assert.AreEqual(0f, Time.timeScale);
            yield return Capture("04-pause-16x9");
            hud.TogglePause();
            yield return new WaitForSecondsRealtime(.35f);
            Assert.AreEqual(1f, Time.timeScale);

            CatalogGear.ApplyUse(player, GameConfig.Current.Items.Get("FOAM"));
            yield return new WaitForSeconds(.8f);
            yield return Capture("05-foam-skill-16x9");
            SetGameViewSize(2100, 900);
            yield return new WaitForSecondsRealtime(.5f);
            yield return Capture("06-gameplay-behind-21x9");

            Session.Clear();
            Match.ModeOverride = "Duos";
            yield return TestScenes.Load(Session.DibsScene);
            joins = UnityEngine.Object.FindAnyObjectByType<PlayerJoinManager>();
            var first = joins.Join(new CaptureBinding());
            var second = joins.Join(new CaptureBinding());
            RoundManager.Instance.CountdownTime = .15f;
            RoundManager.Instance.StartMatch();
            yield return new WaitForSeconds(1.1f);
            Assert.AreEqual(2, joins.HumanCount);
            Assert.AreEqual(4, joins.Players.Count);
            Assert.AreEqual(2, joins.Players.Count(p => p.Team == first.Team));
            Assert.AreEqual(2, joins.Players.Count(p => p.Team == second.Team));
            yield return Capture("07-local-duos-21x9");
            File.WriteAllText(Path.Combine(directory, "journey.json"),
                "{\"engine\":\"" + Application.unityVersion + "\",\"capture\":\"ScreenCapture.CaptureScreenshotAsTexture at end of frame\",\"utc\":\"" +
                DateTime.UtcNow.ToString("O") + "\",\"screenshots\":9,\"humanSeats\":2,\"duosSeats\":4}");
        }

        IEnumerator Capture(string name)
        {
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            Assert.NotNull(texture);
            Assert.Greater(texture.width, 1000);
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
            Debug.Log("[PresentationCapture] " + name);
        }

        [UnityTest]
        public IEnumerator CaptureFurnitureLifecycleAndMiniatureGrip()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Presentation stage floor";
            floor.transform.position = new Vector3(0f, -.12f, 0f);
            floor.transform.localScale = new Vector3(15f, .24f, 12f);
            floor.GetComponent<Renderer>().sharedMaterial = GameAssets.I.Tinted(new Color(.64f, .58f, .47f));
            var camera = new GameObject("Presentation stage camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.backgroundColor = new Color(.12f, .16f, .17f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.fieldOfView = 48f;
            camera.transform.position = new Vector3(0f, 4.5f, 10.8f);
            camera.transform.LookAt(new Vector3(0f, .7f, 0f));
            var light = new GameObject("Presentation stage key").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            RenderSettings.ambientLight = new Color(.62f, .68f, .70f);
            TilePool.Ensure();
            var intact = FurnitureCatalog.Spawn("TABLE", new Vector3(-3.2f, 0f, 0f), 0f, null);
            var damaged = FurnitureCatalog.Spawn("TABLE", Vector3.zero, 0f, null);
            var broken = FurnitureCatalog.Spawn("TABLE", new Vector3(3.2f, 0f, 0f), 0f, null);
            intact.GetComponent<Rigidbody>().isKinematic = damaged.GetComponent<Rigidbody>().isKinematic = true;
            damaged.TakeHit(damaged.Health * .75f);
            Assert.NotNull(damaged.GetComponent<Art.FurnitureDamageView>());
            StageLabel("INTACT", -3.2f, camera);
            StageLabel("DAMAGED", 0f, camera);
            StageLabel("LETTERS", 3.2f, camera);
            yield return new WaitForSeconds(.3f);
            broken.Break();
            yield return new WaitForSeconds(.12f);
            Assert.AreEqual(5, TilePool.Instance.Active.Count());
            yield return Capture("10-furniture-break-stages");
            yield return new WaitForSeconds(1.1f);
            camera.transform.position = new Vector3(3.2f, 1.6f, 4.3f);
            camera.transform.LookAt(new Vector3(3.2f, .2f, 0f));
            yield return new WaitForSeconds(.5f);
            yield return Capture("11-letter-pickup-glow");

            UnityEngine.Object.Destroy(intact.gameObject);
            UnityEngine.Object.Destroy(damaged.gameObject);
            foreach (var label in UnityEngine.Object.FindObjectsByType<TMPro.TextMeshPro>())
                if (label.name == "Presentation stage label") UnityEngine.Object.Destroy(label.gameObject);
            TilePool.Instance.ReleaseAll();
            var player = UnityEngine.Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, new CaptureBinding());
            player.Respawn(Vector3.zero);
            player.FaceTowards(Vector3.forward);
            player.Inventory.Set("TABLE");
            Assert.IsTrue(player.Summoner.Summon("TABLE"));
            camera.transform.position = new Vector3(.95f, 1.2f, 2.2f);
            camera.transform.LookAt(new Vector3(0f, .75f, 0f));
            yield return new WaitForSeconds(.8f);
            var held = player.Combat.Weapon;
            Assert.AreEqual("TABLE", held.word);
            Assert.Less(held.transform.localScale.x, 1f);
            var appearance = player.GetComponent<PlayerAppearance>();
            Debug.Log($"[PresentationGrip] held={held.transform.position} scale={held.transform.lossyScale} grip={appearance.RightGrip.position}");
            foreach (var renderer in held.GetComponentsInChildren<Renderer>(true))
                Debug.Log($"[PresentationGrip] {renderer.name} enabled={renderer.enabled} active={renderer.gameObject.activeInHierarchy} forceOff={renderer.forceRenderingOff} center={renderer.bounds.center} size={renderer.bounds.size}");
            yield return Capture("12-character-miniature-table");
            player.GetComponent<PlayerAppearance>().Play("Celebrate", 1.5f);
            yield return new WaitForSeconds(.3f);
            yield return Capture("13-character-celebrate");
        }

        static void StageLabel(string value, float x, Camera camera)
        {
            var label = new GameObject("Presentation stage label").AddComponent<TMPro.TextMeshPro>();
            label.font = GameAssets.I.font;
            label.text = value;
            label.fontSize = 3f;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.color = new Color(1f, .95f, .8f);
            label.rectTransform.sizeDelta = new Vector2(3f, .7f);
            label.transform.position = new Vector3(x, 2.2f, 0f);
            label.gameObject.AddComponent<WorldSpaceBillboard>().SetCamera(camera);
        }

        static void SetGameViewSize(int width, int height)
        {
            var assembly = typeof(Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var kindType = assembly.GetType("UnityEditor.GameViewSizeType");
            var size = Activator.CreateInstance(sizeType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, new object[] { Enum.Parse(kindType, "FixedResolution"), width, height, "Presentation " + width }, null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            var viewType = assembly.GetType("UnityEditor.GameView");
            var view = EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(view, count - 1);
            view.Focus();
        }
    }
}
