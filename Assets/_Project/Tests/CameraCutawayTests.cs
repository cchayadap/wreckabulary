using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    public sealed class CameraCutawayTests
    {
        [UnitySetUp] public IEnumerator SetUp() => TestScenes.Reset();
        [UnityTearDown] public IEnumerator TearDown() => TestScenes.Reset();

        static PlayerController Player(int seat, Vector3 position)
        {
            var player = Object.Instantiate(GameAssets.I.playerPrefab, position, Quaternion.identity);
            player.Setup(seat, new ScriptedBinding());
            player.Respawn(position);
            player.Body.useGravity = false;
            player.Body.constraints = RigidbodyConstraints.FreezeAll;
            return player;
        }

        static Renderer Box(string name, Transform parent, Vector3 at, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = at;
            box.transform.localScale = size;
            box.GetComponent<Collider>().enabled = false;
            return box.GetComponent<Renderer>();
        }

        static Renderer Ceiling(Vector3 at, Vector3 size, Transform parent = null)
        {
            var renderer = Box("Authored ceiling", parent, at, size);
            renderer.gameObject.AddComponent<CutawaySurface>().Configure(CutawayKind.Ceiling, 0, "Test room", new[] { renderer });
            return renderer;
        }

        static CameraCutaway View(string name, StoreyCutaway world, PlayerController player, Vector3 position)
        {
            var camera = new GameObject(name).AddComponent<Camera>();
            camera.transform.position = position;
            var view = camera.gameObject.AddComponent<CameraCutaway>();
            view.SetView(view, world, player);
            view.RefreshVisibility();
            return view;
        }

        [UnityTest]
        public IEnumerator CamerasOnDifferentFloorsRestoreExactRendererStateThroughNestedRenders()
        {
            var world = new GameObject("Two-storey house").AddComponent<StoreyCutaway>();
            var lower = new GameObject(RoomBuilder.StoreyName(0)).transform;
            lower.SetParent(world.transform);
            var upper = new GameObject(RoomBuilder.StoreyName(1)).transform;
            upper.SetParent(world.transform);
            var floor = Box("Upstairs floor", upper, new Vector3(0f, 2.9f, 0f), new Vector3(8f, .2f, 8f));
            floor.shadowCastingMode = ShadowCastingMode.TwoSided;
            var downstairs = Player(0, Vector3.zero);
            var upstairs = Player(1, Vector3.up * 3f);
            world.Configure(GameConfig.Current.HouseFor("terrace"), world.transform);
            var a = View("Downstairs camera", world, downstairs, new Vector3(0f, 1.5f, -3f));
            var b = View("Upstairs camera", world, upstairs, new Vector3(0f, 4.5f, -3f));
            var editorOrSecondary = new GameObject("Unregistered secondary camera").AddComponent<Camera>();
            Assert.IsTrue(a.WouldHide(floor));
            Assert.IsFalse(b.WouldHide(floor));
            Assert.IsFalse(floor.forceRenderingOff);
            using (CameraCutaway.BeginCameraVisibility(a.GetComponent<Camera>()))
            {
                Assert.IsTrue(floor.forceRenderingOff);
                using (CameraCutaway.BeginCameraVisibility(b.GetComponent<Camera>()))
                {
                    Assert.IsFalse(floor.forceRenderingOff, "The upstairs camera retains its own floor.");
                    Assert.AreEqual(ShadowCastingMode.TwoSided, floor.shadowCastingMode);
                }
                Assert.IsTrue(floor.forceRenderingOff);
                using (CameraCutaway.BeginCameraVisibility(editorOrSecondary))
                    Assert.IsFalse(floor.forceRenderingOff, "An unregistered camera sees authored geometry.");
                Assert.IsTrue(floor.forceRenderingOff);
            }
            Assert.IsFalse(floor.forceRenderingOff);
            Assert.AreEqual(ShadowCastingMode.TwoSided, floor.shadowCastingMode);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StandaloneCanopyShowsFromInsideAndCutsOnlyForTheOverviewCamera()
        {
            var ceiling = Ceiling(new Vector3(0f, 2.8f, 0f), new Vector3(8f, .15f, 8f));
            var player = Player(0, Vector3.zero);
            var solo = View("Interior view without RoomBuilder", null, player, new Vector3(0f, 1.5f, -2f));
            var overview = View("Lobby overview without RoomBuilder", null, null, new Vector3(0f, 12f, -8f));
            Assert.IsTrue(solo.IsVisible(ceiling), "Looking upward from inside must reveal a real ceiling.");
            Assert.IsTrue(overview.WouldHide(ceiling), "A same-storey canopy must not cover the lobby's overhead view.");
            using (CameraCutaway.BeginCameraVisibility(overview.GetComponent<Camera>()))
            {
                Assert.AreEqual(ShadowCastingMode.ShadowsOnly, ceiling.shadowCastingMode);
                using (CameraCutaway.BeginCameraVisibility(solo.GetComponent<Camera>()))
                    Assert.AreEqual(ShadowCastingMode.On, ceiling.shadowCastingMode);
            }
            Assert.AreEqual(ShadowCastingMode.On, ceiling.shadowCastingMode);
            solo.transform.position = new Vector3(0f, 4f, -2f);
            solo.RefreshVisibility();
            Assert.IsTrue(solo.WouldHide(ceiling), "The roof cuts away if it actually crosses the camera-to-player line.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisableAndReusedScopeHandlesCannotLeakRendererOverrides()
        {
            var ceiling = Ceiling(new Vector3(0f, 2.8f, 0f), new Vector3(8f, .15f, 8f));
            var view = View("Overview", null, null, new Vector3(0f, 12f, -8f));
            var camera = view.GetComponent<Camera>();
            var previous = CameraCutaway.BeginCameraVisibility(camera);
            previous.Dispose();
            var current = CameraCutaway.BeginCameraVisibility(camera);
            previous.Dispose();
            Assert.AreEqual(ShadowCastingMode.ShadowsOnly, ceiling.shadowCastingMode, "Disposing an old pooled handle must not end a newer render.");
            view.enabled = false;
            Assert.AreEqual(ShadowCastingMode.On, ceiling.shadowCastingMode);
            Assert.IsFalse(ceiling.forceRenderingOff);
            current.Dispose();
            view.enabled = true;
            view.SetView(view, null, null);
            ceiling.forceRenderingOff = true;
            ceiling.shadowCastingMode = ShadowCastingMode.Off;
            using (CameraCutaway.BeginCameraVisibility(camera)) Assert.IsTrue(ceiling.forceRenderingOff);
            Assert.IsTrue(ceiling.forceRenderingOff, "Artist-hidden renderers must remain hidden after the camera completes.");
            Assert.AreEqual(ShadowCastingMode.Off, ceiling.shadowCastingMode);
            view.ClearView(view);
            Assert.IsFalse(view.IsVisible(ceiling), "The visibility query must respect artist-hidden renderers even without a cutaway plan.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator CameraClimbAndBoomStayBelowNonCollidingCeilingsAndRespectOpenings()
        {
            var ceiling = Ceiling(new Vector3(0f, 2.1f, 0f), new Vector3(4f, .15f, 8f));
            var view = View("Interior clearance", null, null, Vector3.zero);
            var wall = Box("Wall behind player", null, new Vector3(0f, 2f, -1f), new Vector3(8f, 4f, .2f));
            wall.GetComponent<Collider>().enabled = true;
            Physics.SyncTransforms();
            var boom = new ShoulderView { Cutaway = view };
            for (int i = 0; i < 100; i++)
                boom.Place(view.GetComponent<Camera>(), Vector3.zero, 0f, ShoulderView.MaxPitch, .02f);
            Assert.LessOrEqual(view.transform.position.y + ShoulderView.ProbeRadius, ceiling.bounds.min.y + .01f,
                "The minimum boom distance must also fit below the ceiling while climbing a wall.");
            Assert.IsFalse(ceiling.GetComponent<Collider>().enabled, "Ceiling presentation must not alter gameplay collision.");
            Assert.Less(view.ProbeCeilings(Vector3.up, Vector3.up, 3f, .2f), 1f);
            Assert.AreEqual(3f, view.ProbeCeilings(new Vector3(4f, 1f, 0f), Vector3.up, 3f, .2f),
                "Independent renderer bounds preserve stair openings instead of filling the whole room.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyedCutawayPropsLeaveTheRegistryWhileTheCameraKeepsRunning()
        {
            var view = View("Persistent camera across rounds", null, null, Vector3.up * 6f);
            int baseline = CameraCutaway.PreparedRendererCount;
            for (int round = 0; round < 3; round++)
            {
                var props = new Renderer[8];
                for (int i = 0; i < props.Length; i++)
                    props[i] = Box("Transient delivery or effect", null, Vector3.right * i, Vector3.one);
                view.SetOccluders(props);
                view.RefreshVisibility();
                Assert.AreEqual(baseline + props.Length, CameraCutaway.PreparedRendererCount);
                foreach (var renderer in props) Object.Destroy(renderer.gameObject);
                yield return null;
                Assert.IsTrue(view.isActiveAndEnabled);
                Assert.AreEqual(baseline, CameraCutaway.PreparedRendererCount, "Destroyed props must not accumulate across rounds.");
            }
            var survivor = Box("Authored prop survives camera release", null, Vector3.zero, Vector3.one);
            view.SetOccluders(new[] { survivor });
            view.RefreshVisibility();
            Assert.IsNotNull(survivor.GetComponent<CameraCutawayRenderer>());
            view.enabled = false;
            yield return null;
            Assert.AreEqual(baseline, CameraCutaway.PreparedRendererCount);
            Assert.IsNotNull(survivor);
            Assert.IsNull(survivor.GetComponent<CameraCutawayRenderer>());
            Assert.AreEqual(ShadowCastingMode.On, survivor.shadowCastingMode);
        }

        [UnityTest]
        public IEnumerator NativeRenderingCutsTheRoofForOneCameraAndKeepsItForOthers()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("This regression requires a native graphics device.");
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            Assert.IsNotNull(shader);
            var roofMaterial = new Material(shader) { enableInstancing = true };
            var floorMaterial = new Material(shader) { enableInstancing = true };
            roofMaterial.SetColor("_BaseColor", Color.red);
            floorMaterial.SetColor("_BaseColor", Color.green);
            var ceiling = Ceiling(Vector3.up * 2.8f, new Vector3(8f, .15f, 8f));
            ceiling.sharedMaterial = roofMaterial;
            var floor = Box("Green floor", null, Vector3.zero, new Vector3(8f, .1f, 8f));
            floor.sharedMaterial = floorMaterial;
            var overview = View("Native overview", null, null, Vector3.up * 7f);
            var player = Player(0, Vector3.right * 2f);
            var interior = View("Native interior", null, player, Vector3.up * 1.4f);
            var secondary = new GameObject("Unregistered native camera").AddComponent<Camera>();
            var cameras = new[] { overview.GetComponent<Camera>(), secondary, interior.GetComponent<Camera>() };
            foreach (var camera in cameras)
            {
                camera.orthographic = true;
                camera.orthographicSize = 2f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            }
            overview.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            secondary.transform.SetPositionAndRotation(overview.transform.position, overview.transform.rotation);
            interior.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            var targets = new RenderTexture[cameras.Length];
            var pixels = new Texture2D(128, 128, TextureFormat.RGB24, false);
            var originalTarget = RenderTexture.active;
            try
            {
                for (int i = 0; i < cameras.Length; i++)
                {
                    targets[i] = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
                    targets[i].Create();
                    cameras[i].targetTexture = targets[i];
                }
                // Use normal camera frames, including GPU batch uploads and per-camera culling.
                // Reading the completed prior frame also works in graphics-enabled batch mode.
                yield return null;
                yield return null;
                yield return null;
                var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "reviews", "evidence", "camera-cutaway-native"));
                Directory.CreateDirectory(directory);
                Color Capture(int cameraIndex, string name)
                {
                    RenderTexture.active = targets[cameraIndex];
                    pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0, false);
                    pixels.Apply();
                    File.WriteAllBytes(Path.Combine(directory, name + ".png"), pixels.EncodeToPNG());
                    return pixels.GetPixel(64, 64);
                }
                var open = Capture(0, "overview-floor-visible");
                Assert.Greater(open.g, open.r + .25f, "The native overview must contain the green floor, not the red roof.");
                Assert.AreEqual(ShadowCastingMode.On, ceiling.shadowCastingMode);
                Assert.IsFalse(ceiling.forceRenderingOff);
                var authored = Capture(1, "secondary-roof-preserved");
                Assert.Greater(authored.r, authored.g + .25f, "An independent camera must still render the authored red roof.");
                var indoors = Capture(2, "interior-ceiling-visible");
                Assert.Greater(indoors.r, indoors.g + .25f, "Looking up from the interior must render the red ceiling.");
                yield return null;
                var restored = Capture(0, "overview-after-secondary");
                Assert.Greater(restored.g, restored.r + .25f, "The overview must remain cut away after another camera renders.");
            }
            finally
            {
                RenderTexture.active = originalTarget;
                for (int i = 0; i < cameras.Length; i++)
                {
                    if (cameras[i]) cameras[i].targetTexture = null;
                    if (!targets[i]) continue;
                    targets[i].Release();
                    Object.Destroy(targets[i]);
                }
                Object.Destroy(pixels);
                Object.Destroy(roofMaterial); Object.Destroy(floorMaterial);
            }
        }
    }
}
