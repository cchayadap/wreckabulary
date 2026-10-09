using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Art;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    [Explicit, Category("SeasonalRoomCapture")]
    public sealed class SeasonalRoomCaptureTests
    {
        sealed class LocalInput : InputBinding
        {
            readonly string id = Guid.NewGuid().ToString();
            public Vector2 Move;
            public override string Id => id;
            public override bool CanLook => true;
            public override void Read(ref PlayerCommands command) => command = new PlayerCommands { move = Move };
            public override bool JoinPressed() => false;
            public override bool StartPressed() => false;
        }

        [Serializable] sealed class Frame
        {
            public string file, mode;
            public int width, height, seats, visibleDecor, renderers, triangles;
            public bool thirdPerson;
        }
        [Serializable] sealed class Manifest { public string engine, utc; public bool completed; public Frame[] frames; }
        readonly List<Frame> frames = new();
        UIRefinementCaptureTests.GameViewScope view;
        bool priorAsync, completed;
        bool? priorFocus;
        float priorCapture;
        string directory;
        int width = 1600;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            priorAsync = EditorSettings.asyncShaderCompilation;
            priorFocus = GameHud.PauseOnFocusLossOverride;
            priorCapture = Time.captureDeltaTime;
            EditorSettings.asyncShaderCompilation = false; GameHud.PauseOnFocusLossOverride = false;
            Time.captureDeltaTime = 1f / 60f;
            view = new UIRefinementCaptureTests.GameViewScope(); view.Select(width, 900);
            directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/reviews/evidence/unity-seasonal-room-2026-10-09"));
            Directory.CreateDirectory(directory);
            yield return TestScenes.Reset();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.Reset();
            EditorSettings.asyncShaderCompilation = priorAsync; GameHud.PauseOnFocusLossOverride = priorFocus;
            Time.captureDeltaTime = priorCapture;
            view?.Dispose();
            File.WriteAllText(Path.Combine(directory, "seasonal-room-journey.json"), JsonUtility.ToJson(new Manifest
            { engine = Application.unityVersion, utc = DateTime.UtcNow.ToString("O"), completed = completed, frames = frames.ToArray() }, true));
        }

        [UnityTest]
        public IEnumerator SavedCornerSurvivesPlayResetsAndCouchCameras()
        {
            Session.SelectMap("pinwheel"); Match.ModeOverride = "Dibs";
            yield return TestScenes.Load(Session.DibsScene);
            var builder = Object.FindAnyObjectByType<RoomBuilder>();
            Assert.NotNull(builder.AuthoredWorld);
            var corner = builder.AuthoredWorld.GetComponentsInChildren<SeasonalRoomDressing>().Single();
            Assert.AreEqual("winter-house-party", corner.CollectionId);
            var matrices = corner.GetComponentsInChildren<Transform>().ToDictionary(t => t, t => t.localToWorldMatrix);
            Assert.IsEmpty(corner.GetComponentsInChildren<Collider>());
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var input = new LocalInput();
            var player = joins.Join(input);
            var rounds = RoundManager.Instance;
            rounds.CountdownTime = .1f; rounds.StartMatch();
            yield return TestScenes.WaitUntil(() => rounds.Phase == Phase.Playing, 3f, "active seasonal room");
            foreach (var other in World.Players.Where(p => p != player))
            { other.Frozen = true; if (other.TryGetComponent<BotController>(out var bot)) bot.enabled = false; }
            player.Inventory.Collects = false;
            Place(player, new Vector3(-6.8f, .08f, .4f), new Vector3(-5.6f, 1f, 3f));
            yield return Capture("corner-third-person", corner);
            width = 2100; view.Select(width, 900);
            yield return Capture("corner-third-person-ultrawide", corner);
            width = 1600; view.Select(width, 900);
            Place(player, new Vector3(-6.15f, .08f, 1.45f), new Vector3(-4.95f, 2.3f, 3.78f));
            yield return Capture("wreath-garland-upward", corner);

            foreach (var route in new[]
            {
                (new Vector3(-7f, .08f, 2.3f), new Vector3(-7f, .08f, 5.1f)),
                (new Vector3(-5.8f, .08f, 0f), new Vector3(-2.7f, .08f, 0f))
            })
            {
                player.Respawn(route.Item1); player.Frozen = false; Physics.SyncTransforms();
                float until = Time.realtimeSinceStartup + 5f;
                while (World.Flat(player.transform.position - route.Item2).magnitude > .22f)
                {
                    Assert.Less(Time.realtimeSinceStartup, until, "The actual player must cross the existing doorway.");
                    var direction = route.Item2 - player.transform.position;
                    player.LookYaw = Mathf.Atan2(direction.x, direction.z); input.Move = Vector2.up;
                    yield return null;
                }
                input.Move = Vector2.zero;
                player.Frozen = true;
            }

            int originalCount = builder.Originals.Count;
            string[] supply = builder.Originals.Select(s => s.Word).OrderBy(word => word).ToArray();
            var prop = builder.Originals.First(s => s.Word == "CHAIR");
            prop.TakeHit(10000f);
            yield return null;
            Assert.IsTrue(!prop || !prop.gameObject.activeInHierarchy, "Existing furniture still breaks.");
            Assert.Greater(TilePool.Instance.Active.Count, 0, "Broken furniture produces real letter tiles.");
            for (int reset = 0; reset < 2; reset++)
            {
                builder.ResetRoom(); yield return null;
                Assert.AreEqual(originalCount, builder.Originals.Count);
                CollectionAssert.AreEqual(supply, builder.Originals.Select(s => s.Word).OrderBy(word => word).ToArray());
                Assert.AreSame(corner, builder.AuthoredWorld.GetComponentsInChildren<SeasonalRoomDressing>().Single());
                foreach (var pair in matrices) Assert.AreEqual(pair.Value, pair.Key.localToWorldMatrix, pair.Key.name);
            }
            Place(player, new Vector3(-6.8f, .08f, .4f), new Vector3(-5.6f, 1f, 3f));
            yield return Capture("corner-after-furniture-reset", corner);

            // Couch seats join before a match, while the available seats have not been filled by bots.
            yield return TestScenes.Reset(); Session.SelectMap("pinwheel"); Match.ModeOverride = "Duos";
            yield return TestScenes.Load(Session.DibsScene);
            builder = Object.FindAnyObjectByType<RoomBuilder>();
            corner = builder.AuthoredWorld.GetComponentsInChildren<SeasonalRoomDressing>().Single();
            joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            player = joins.Join(new LocalInput());
            Place(player, new Vector3(-6.8f, .08f, .4f), new Vector3(-5.6f, 1f, 3f));
            for (int seat = 1; seat < 4; seat++)
            {
                var buddy = joins.Join(new LocalInput());
                Assert.NotNull(buddy);
                Place(buddy, new Vector3(-7.5f - .3f * seat, .08f, .4f - .5f * seat), Vector3.forward);
                if (seat == 1) yield return Capture("two-local-seats-overview", corner);
            }
            rounds = RoundManager.Instance; rounds.CountdownTime = .1f; rounds.StartMatch();
            yield return TestScenes.WaitUntil(() => rounds.Phase == Phase.Playing, 3f, "four-seat Duos match");
            for (int seat = 0; seat < 4; seat++)
            {
                Place(joins.Players[seat], new Vector3(-7.5f - .3f * seat, .08f, .4f - .5f * seat), Vector3.forward);
                CatalogGear.ApplyUse(joins.Players[seat], GameConfig.Current.Items.Get("FOAM"));
            }
            width = 2100; view.Select(width, 900);
            yield return Capture("four-local-seats-overview", corner);
            Assert.IsFalse(CameraRig.Instance.IsThirdPerson);
            Assert.IsTrue(Camera.main.orthographic);
            Assert.AreEqual(4, joins.Players.Count(p => CameraRig.IsHuman(p)));

            yield return TestScenes.Reset(); Session.SelectMap("pinwheel"); Match.ModeOverride = "MovingDay";
            yield return TestScenes.Load(Session.MovingDayScene);
            builder = Object.FindAnyObjectByType<RoomBuilder>();
            corner = builder.AuthoredWorld.GetComponentsInChildren<SeasonalRoomDressing>().Single();
            player = Object.FindAnyObjectByType<PlayerJoinManager>().Join(new LocalInput());
            var movingDay = Object.FindAnyObjectByType<MovingDayDirector>();
            yield return TestScenes.WaitUntil(() => movingDay.Current == MovingDayDirector.State.Playing, 6f, "active Moving Day level");
            yield return new WaitForSeconds(1.2f);
            Place(player, new Vector3(-6.8f, .08f, .4f), new Vector3(-5.6f, 1f, 3f));
            yield return Capture("moving-day-corner", corner);
            movingDay.Retry(); yield return null;
            Assert.AreSame(corner, builder.AuthoredWorld.GetComponentsInChildren<SeasonalRoomDressing>().Single());
            Assert.IsEmpty(builder.Originals, "Moving Day resets gameplay furniture without clearing room decoration.");
            completed = true;
        }

        static void Place(PlayerController player, Vector3 position, Vector3 target)
        {
            player.Respawn(position); player.Frozen = true;
            var aim = target - (position + Vector3.up * ShoulderView.PivotHeight);
            player.LookYaw = Mathf.Atan2(aim.x, aim.z);
            player.LookPitch = Mathf.Clamp(-Mathf.Atan2(aim.y, World.Flat(aim).magnitude), ShoulderView.MinPitch, ShoulderView.MaxPitch);
            Physics.SyncTransforms();
        }

        IEnumerator Capture(string name, SeasonalRoomDressing corner)
        {
            yield return new WaitForSecondsRealtime(.65f);
            Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame();
            var lens = Camera.main;
            var visibility = lens.GetComponent<CameraCutaway>(); visibility.RefreshVisibility();
            var planes = GeometryUtility.CalculateFrustumPlanes(lens);
            int visible = corner.GetComponentsInChildren<Renderer>().Count(r => r.enabled && visibility.IsVisible(r) && GeometryUtility.TestPlanesAABB(planes, r.bounds));
            Assert.Greater(visible, 0, "Seasonal meshes are actually within this camera's visible region.");
            var pixels = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Assert.AreEqual(width, pixels.width); Assert.AreEqual(900, pixels.height);
                string file = $"{frames.Count + 1:00}-{name}.png";
                File.WriteAllBytes(Path.Combine(directory, file), pixels.EncodeToPNG());
                frames.Add(new Frame { file = file, mode = Match.ModeOverride, width = width, height = 900,
                    seats = World.Players.Count(CameraRig.IsHuman), thirdPerson = CameraRig.Instance.IsThirdPerson,
                    visibleDecor = visible, renderers = corner.GetComponentsInChildren<Renderer>().Length,
                    triangles = corner.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3) });
                Debug.Log("[SeasonalRoomCapture] " + file);
            }
            finally { Object.Destroy(pixels); }
        }
    }
}
