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
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    /// <summary>Explicit native frames of saved worlds through the actual runtime lighting and camera.</summary>
    [Explicit, Category("EnvironmentCapture")]
    public sealed class EnvironmentCaptureTests
    {
        static readonly string[] Maps = { "pinwheel", "courtyard", "flat", "terrace", "walkup" };
        static readonly string[] DetailNames = { "Bulb glow", "Window glass", "Ceiling panel", "Roof cap", "Print surface" };
        readonly List<Frame> frames = new();
        string directory;
        bool previousAsyncCompilation, completed;
        bool? previousFocusPause;
        GameViewScope gameView;

        sealed class CaptureBinding : InputBinding
        {
            static int next;
            readonly string id = "environment-capture-" + ++next;
            public override string Id => id;
            public override bool CanLook => true;
            public override void Read(ref PlayerCommands command) => command = default;
            public override bool JoinPressed() => false;
            public override bool StartPressed() => false;
        }

        [Serializable]
        sealed class Frame
        {
            public string file, scene, map, room, view, skybox, target, graphicsPreset, lightingProfile;
            public int width, height, localSeats, visibleDetails, selectedLights, lightBudget;
            public bool thirdPerson, orthographic, targetInFrame;
            public float cameraPitch, sunIntensity;
            public Vector3 cameraPosition, cameraForward, playerPosition;
        }

        [Serializable]
        sealed class Manifest
        {
            public string engine, utc, capture = "Native Game view at end of frame";
            public bool completed;
            public Frame[] frames;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            directory = Environment.GetEnvironmentVariable("WRECKABULARY_ENVIRONMENT_CAPTURE_DIRECTORY") ??
                Environment.GetEnvironmentVariable("WRECKABULARY_CAPTURE_DIRECTORY") ??
                Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/reviews/evidence/unity-environment-2026-10-09"));
            Directory.CreateDirectory(directory);
            frames.Clear();
            completed = false;
            previousAsyncCompilation = EditorSettings.asyncShaderCompilation;
            previousFocusPause = GameHud.PauseOnFocusLossOverride;
            EditorSettings.asyncShaderCompilation = false;
            GameHud.PauseOnFocusLossOverride = false;
            gameView = new GameViewScope();
            gameView.Select(1600, 900);
            yield return TestScenes.Reset();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            EditorSettings.asyncShaderCompilation = previousAsyncCompilation;
            GameHud.PauseOnFocusLossOverride = previousFocusPause;
            try { gameView?.Dispose(); }
            finally
            {
                gameView = null;
                if (!string.IsNullOrEmpty(directory))
                    File.WriteAllText(Path.Combine(directory, "environment-journey.json"), JsonUtility.ToJson(new Manifest
                    {
                        engine = Application.unityVersion, utc = DateTime.UtcNow.ToString("O"),
                        completed = completed, frames = frames.ToArray()
                    }, true));
            }
            yield return TestScenes.Reset();
        }

        [UnityTest]
        public IEnumerator CaptureFiveWorldsLightingAndFloorTransitions()
        {
            Assert.NotNull(Resources.Load<ScriptableObject>("Environment/SunlitHouse"), "The editable lighting profile must be imported.");
            foreach (string map in Maps)
            {
                Session.Clear();
                Session.SelectMap(map);
                yield return TestScenes.Load(Session.DibsScene);
                var builder = Object.FindAnyObjectByType<RoomBuilder>();
                Assert.NotNull(builder);
                Assert.NotNull(builder.AuthoredWorld, map + " must use a saved editable world.");
                Assert.AreEqual(map, builder.AuthoredWorld.MapId);
                Assert.IsTrue(builder.AuthoredWorld.IsCurrent);
                var lens = Camera.main;
                var rig = lens.GetComponent<CameraRig>();
                Assert.NotNull(rig);
                yield return Settle();
                Assert.IsFalse(rig.IsThirdPerson, "The unoccupied house uses its overview camera.");
                yield return Capture(map, "overview", "", lens, null, builder.AuthoredWorld);

                var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
                var player = joins.Join(new CaptureBinding());
                player.Inventory.Collects = false;
                var lower = InteriorRoom(builder.Layout, 0);
                Place(player, lower);
                yield return Settle();
                Assert.AreSame(player, rig.Target, map + " uses the local player's third-person camera.");
                Assert.IsFalse(lens.orthographic);
                Assert.AreEqual(CameraClearFlags.Skybox, lens.clearFlags, "The runtime camera must show the authored sky.");
                Assert.NotNull(RenderSettings.skybox);
                yield return Capture(map, "interior", lower.Name, lens, player, builder.AuthoredWorld);
                yield return CaptureDetail(map, lower, lens, player, builder.AuthoredWorld);

                if (map == "pinwheel")
                {
                    player.LookPitch = ShoulderView.DefaultPitch;
                    CatalogGear.ApplyUse(player, GameConfig.Current.Items.Get("FOAM"));
                    yield return Settle();
                    Assert.Greater(player.Health.Bubble, 0f);
                    Assert.IsTrue(player.GetComponentsInChildren<MeshRenderer>().Any(renderer => renderer.name == "Foam film"));
                    yield return Capture(map, "foam-daylight", lower.Name, lens, player, builder.AuthoredWorld);

                    var rounds = RoundManager.Instance;
                    Assert.IsNotNull(rounds);
                    rounds.CountdownTime = .15f;
                    rounds.StartMatch();
                    yield return TestScenes.WaitUntil(() => rounds.Phase == Phase.Playing, 3f, "active Pinwheel round");
                    Place(player, lower);
                    player.Frozen = false;
                    yield return Settle();
                    var hud = Object.FindAnyObjectByType<GameHud>();
                    var warmup = hud.UiCanvas.transform.Find("Safe HUD/Mode actions").GetComponent<CanvasGroup>();
                    Assert.AreEqual(Phase.Playing, rounds.Phase);
                    Assert.IsTrue(hud.AcceptsGameplayInput);
                    Assert.AreEqual(0f, warmup.alpha, "The active-gameplay frame must not contain the warmup start overlay.");
                    Assert.IsFalse(warmup.interactable || warmup.blocksRaycasts);
                    yield return Capture(map, "active-gameplay", lower.Name, lens, player, builder.AuthoredWorld);
                }

                if (map == "courtyard")
                {
                    var garden = builder.Layout.Rooms.First(room => room.Name == "Garden");
                    Place(player, garden);
                    yield return Settle();
                    var sun = RenderSettings.sun;
                    Assert.NotNull(sun, "Daylight identifies the directional sun used by the sky.");
                    Vector3 towardSun = -sun.transform.forward;
                    player.LookYaw = Mathf.Atan2(towardSun.x, towardSun.z);
                    player.LookPitch = ShoulderView.MinPitch;
                    yield return Settle();
                    var sunPoint = lens.WorldToViewportPoint(lens.transform.position + towardSun * 100f);
                    Assert.That(sunPoint.z, Is.GreaterThan(0f), "The garden sky frame faces the sun.");
                    Assert.That(sunPoint.x, Is.InRange(0f, 1f), "The authored sun is inside the garden frame.");
                    Assert.That(sunPoint.y, Is.InRange(0f, 1f), "The authored sun is inside the normal upward look range.");
                    yield return Capture(map, "garden-sky", garden.Name, lens, player, builder.AuthoredWorld);
                }

                int storeys = builder.Layout.StoreyFloors().Count;
                if (storeys > 1)
                {
                    var upper = InteriorRoom(builder.Layout, storeys - 1);
                    Place(player, upper);
                    yield return Settle();
                    Assert.AreEqual(storeys - 1, StoreyCutaway.Instance.StoreyOfPlayer(player));
                    yield return Capture(map, "upper-interior", upper.Name, lens, player, builder.AuthoredWorld);
                    yield return CaptureDetail(map, upper, lens, player, builder.AuthoredWorld, "upper-detail");

                    var buddy = joins.Join(new CaptureBinding());
                    Place(buddy, lower);
                    player.LookPitch = ShoulderView.DefaultPitch;
                    gameView.Select(2100, 900);
                    yield return new WaitForSecondsRealtime(1f);
                    Assert.IsFalse(rig.IsThirdPerson, "Two actual local seats use the shared couch camera.");
                    Assert.IsTrue(lens.orthographic);
                    Assert.AreEqual(0, StoreyCutaway.Instance.StoreyOfPlayer(buddy));
                    Assert.AreEqual(storeys - 1, StoreyCutaway.Instance.StoreyOfPlayer(player));
                    yield return Capture(map, "mixed-floor-couch-21x9", lower.Name + " / " + upper.Name,
                        lens, player, builder.AuthoredWorld);
                    gameView.Select(1600, 900);
                }
            }

            Session.Clear();
            yield return TestScenes.Load(Session.HubScene);
            yield return new WaitForSecondsRealtime(.35f);
            yield return Capture("hub", "lobby", "", Camera.main, null, null);
            yield return TestScenes.ExploreHouse();
            var hubPlayer = Object.FindAnyObjectByType<PlayerJoinManager>().Join(new CaptureBinding());
            yield return new WaitForSecondsRealtime(1f);
            Assert.AreSame(hubPlayer, CameraRig.Instance.Target, "Hub exploration also uses the actual local third-person camera.");
            yield return Capture("hub", "exploration", "Hub", Camera.main, hubPlayer, null);
            completed = true;
        }

        static RoomBox InteriorRoom(HouseLayout layout, int storey) => layout.Rooms
            .Where(room => room.Name != "Garden" && layout.StoreyOf(room) == storey)
            .OrderByDescending(room => (room.MaxX - room.MinX) * (room.MaxZ - room.MinZ)).First();

        static void Place(PlayerController player, RoomBox room)
        {
            var centre = new Vector3((room.MinX + room.MaxX) * .5f, room.FloorY + .08f, (room.MinZ + room.MaxZ) * .5f);
            Physics.SyncTransforms();
            var spot = ShoulderView.RoomySpot(centre, at => at.x > room.MinX + .6f && at.x < room.MaxX - .6f &&
                at.z > room.MinZ + .6f && at.z < room.MaxZ - .6f);
            player.Respawn(spot);
            player.Frozen = true;
            Physics.SyncTransforms();
        }

        static IEnumerator Settle() { yield return new WaitForSecondsRealtime(.55f); }

        IEnumerator CaptureDetail(string map, RoomBox room, Camera lens, PlayerController player, AuthoredHouse world, string name = "upward-detail")
        {
            var details = world.GetComponentsInChildren<Renderer>()
                .Where(renderer => DetailNames.Contains(renderer.name) && InRoom(renderer.bounds.center, room)).ToArray();
            Assert.IsNotEmpty(details, map + " / " + room.Name + " contains authored architectural detail.");
            var target = details.OrderBy(renderer => renderer.name == "Bulb glow" ? 0 : renderer.name == "Window glass" ? 1 : 2)
                .ThenBy(renderer => Vector3.Distance(renderer.bounds.center, player.transform.position)).First();
            var aim = target.bounds.center - (player.transform.position + Vector3.up * ShoulderView.PivotHeight);
            player.LookYaw = Mathf.Atan2(aim.x, aim.z);
            player.LookPitch = Mathf.Clamp(-Mathf.Atan2(aim.y, World.Flat(aim).magnitude), ShoulderView.MinPitch, -.05f);
            yield return Settle();
            yield return Capture(map, name, room.Name, lens, player, world, target);
        }

        static bool InRoom(Vector3 at, RoomBox room) => at.x >= room.MinX - .25f && at.x <= room.MaxX + .25f &&
            at.z >= room.MinZ - .25f && at.z <= room.MaxZ + .25f && at.y > room.FloorY + .2f && at.y < room.FloorY + 3.5f;

        IEnumerator Capture(string map, string view, string room, Camera lens, PlayerController player, AuthoredHouse world, Renderer target = null)
        {
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            var cutaway = lens.GetComponent<CameraCutaway>();
            if (world) Assert.NotNull(cutaway, "Each runtime world camera owns its visibility plan.");
            if (cutaway) cutaway.RefreshVisibility();
            var planes = GeometryUtility.CalculateFrustumPlanes(lens);
            bool Visible(Renderer renderer) => renderer.enabled && renderer.gameObject.activeInHierarchy &&
                (!cutaway || cutaway.IsVisible(renderer)) && GeometryUtility.TestPlanesAABB(planes, renderer.bounds);
            int detailCount = world ? world.GetComponentsInChildren<Renderer>()
                .Count(renderer => DetailNames.Contains(renderer.name) && Visible(renderer)) : 0;
            var lighting = EnvironmentLighting.Active;
            string file = $"{frames.Count + 1:00}-{map}-{view}.png";
            var pixels = ScreenCapture.CaptureScreenshotAsTexture();
            Assert.NotNull(pixels);
            try
            {
                Assert.That(pixels.width, Is.EqualTo(view.Contains("21x9") ? 2100 : 1600));
                Assert.AreEqual(900, pixels.height);
                File.WriteAllBytes(Path.Combine(directory, file), pixels.EncodeToPNG());
                frames.Add(new Frame
                {
                    file = file, map = map, room = room, view = view, scene = SceneManager.GetActiveScene().name,
                    width = pixels.width, height = pixels.height, localSeats = World.Players.Count(CameraRig.IsHuman),
                    thirdPerson = lens.GetComponent<CameraRig>() && lens.GetComponent<CameraRig>().IsThirdPerson,
                    orthographic = lens.orthographic, cameraPosition = lens.transform.position,
                    cameraForward = lens.transform.forward, cameraPitch = player ? player.LookPitch : 0f,
                    playerPosition = player ? player.transform.position : Vector3.zero,
                    skybox = RenderSettings.skybox ? RenderSettings.skybox.name : "",
                    sunIntensity = RenderSettings.sun ? RenderSettings.sun.intensity : 0f,
                    graphicsPreset = GraphicsOptions.Preset,
                    lightingProfile = lighting && lighting.Profile ? lighting.Profile.name : "",
                    selectedLights = lighting ? lighting.SelectedLightCount : 0,
                    lightBudget = lighting ? lighting.LightBudget : 0,
                    target = target ? target.name : "", targetInFrame = target && Visible(target), visibleDetails = detailCount
                });
            }
            finally { Object.Destroy(pixels); }
            Debug.Log("[EnvironmentCapture] " + file);
        }

        /// <summary>Creates only this fixture's resolutions and removes them after restoring the editor selection.</summary>
        sealed class GameViewScope : IDisposable
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
                        new object[] { Enum.Parse(kindType, "FixedResolution"), width, height, "Environment capture temporary" }, null);
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
                Assert.AreEqual(previousTotal, (int)Call("GetTotalCount"), "Capture must remove only its temporary Game view sizes.");
                if (view) Assert.AreEqual(previousIndex, (int)selected.GetValue(view), "Capture must restore the original Game view selection.");
            }
        }
    }
}
