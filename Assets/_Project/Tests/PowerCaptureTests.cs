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
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    /// <summary>Opt-in rendered acceptance journey. Requires a graphics-enabled editor and focused Game view.</summary>
    [Explicit, Category("PowerCapture")]
    public sealed class PowerCaptureTests
    {
        static readonly string[] Items = { "SHIELD", "SODA", "APPLE", "WATER", "FAN", "CLOCK" };
        readonly List<Frame> frames = new();
        string directory;
        bool previousAsyncCompilation, completed;
        float previousCaptureDelta;
        bool? previousFocusPause;
        GameViewScope gameView;

        sealed class CaptureBinding : InputBinding
        {
            static int next;
            readonly string id = "power-capture-" + ++next;
            public PlayerCommands Next;
            public override string Id => id;
            public override bool CanLook => true;
            public override void Read(ref PlayerCommands command) => command = Next;
            public override bool JoinPressed() => false;
            public override bool StartPressed() => false;
        }

        [Serializable]
        sealed class Frame
        {
            public string file, scene, view, item, phase, clip, artAsset, pinnedRecipe;
            public int width, height, localSeats, renderedItems, fields;
            public bool thirdPerson, orthographic, usingItem, blocking, shieldAura, hasHeldItem, targetInFrame;
            public float elapsedSinceAction, health, slowLeft, boostLeft;
            public Vector3 cameraPosition, cameraForward, playerPosition, heldSize, windPoint, matChevron;
            public Quaternion clockHand;
        }

        [Serializable]
        sealed class Manifest
        {
            public string engine, utc, capture = "Native ScreenCapture.CaptureScreenshotAsTexture at end of frame; normal camera rendering";
            public float simulationStep = 1f / 60f;
            public bool completed;
            public Frame[] frames;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            directory = Environment.GetEnvironmentVariable("WRECKABULARY_POWER_CAPTURE_DIRECTORY") ??
                Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/reviews/evidence/unity-powers-2026-10-09"));
            Directory.CreateDirectory(directory);
            frames.Clear();
            completed = false;
            previousAsyncCompilation = EditorSettings.asyncShaderCompilation;
            previousFocusPause = GameHud.PauseOnFocusLossOverride;
            previousCaptureDelta = Time.captureDeltaTime;
            EditorSettings.asyncShaderCompilation = false;
            GameHud.PauseOnFocusLossOverride = false;
            Time.captureDeltaTime = 1f / 60f;
            gameView = new GameViewScope();
            gameView.Select(1600, 900);
            yield return TestScenes.Reset();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            EditorSettings.asyncShaderCompilation = previousAsyncCompilation;
            GameHud.PauseOnFocusLossOverride = previousFocusPause;
            Time.captureDeltaTime = previousCaptureDelta;
            try { gameView?.Dispose(); }
            finally
            {
                gameView = null;
                if (!string.IsNullOrEmpty(directory))
                    File.WriteAllText(Path.Combine(directory, "power-journey.json"), JsonUtility.ToJson(new Manifest
                    {
                        engine = Application.unityVersion, utc = DateTime.UtcNow.ToString("O"),
                        completed = completed, frames = frames.ToArray()
                    }, true));
            }
            yield return TestScenes.Reset();
        }

        [UnityTest]
        public IEnumerator CaptureGeneratedRecipesMiniaturesAndPlayablePowers()
        {
            yield return CaptureRecipes();
            yield return TestScenes.Reset();
            Camera lens = CreateStage();
            var input = new CaptureBinding();
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, input);
            player.Respawn(Vector3.zero);
            player.FaceTowards(Vector3.forward);
            player.Inventory.Collects = false;
            yield return new WaitForSeconds(.7f);

            foreach (string id in Items)
            {
                var gear = Equip(player, id);
                yield return new WaitForSeconds(.35f);
                Assert.AreSame(gear, player.Combat.Weapon);
                Assert.That(gear.transform.localScale.x, Is.EqualTo(gear.Definition.HeldScale).Within(.001f));
                Assert.Greater(VisibleRenderers(gear.gameObject, lens), 0, id + " miniature must render in the native demo frame.");
                yield return Capture("miniature-" + id.ToLowerInvariant(), id, lens, player);
                if (id != "SHIELD") continue;
                input.Next.blockHeld = true;
                yield return WaitForScaled(() => player.Combat.IsBlocking, 1f, "normal SHIELD raise");
                yield return new WaitForSeconds(.1f);
                var aura = gear.GetComponent<HeldShieldAura>();
                Assert.NotNull(aura);
                Assert.IsTrue(aura.Visible);
                Assert.That(gear.Shield.FrontArcDegrees, Is.GreaterThanOrEqualTo(359f));
                yield return Capture("shield-raised-360", id, lens, player);
                yield return new WaitForSeconds(.45f);
                Assert.IsTrue(player.Combat.IsBlocking && aura.Visible, "The held guard remains raised after its entry pose.");
                yield return Capture("shield-raised-held", id, lens, player);
                input.Next = default;
                yield return null;
                yield return null;
                Assert.IsFalse(player.Combat.IsBlocking);
            }

            foreach (string id in new[] { "APPLE", "WATER", "SODA" })
                yield return CaptureConsumption(player, lens, id);

            player.Combat.ResetForRound();
            yield return null;
            yield return CaptureFields(player, lens);
            yield return CaptureCouchMatch();
            yield return CaptureSoloMatch();
            completed = true;
        }

        IEnumerator CaptureRecipes()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return new WaitForSecondsRealtime(.6f);
            var menu = LobbyMenu.Instance;
            Assert.NotNull(menu);
            menu.Open(LobbyMenu.Loadout);
            var loadout = menu.Page<LoadoutPage>();
            Assert.NotNull(loadout);
            loadout.ShowRecipes(true);
            foreach (string id in Items)
            {
                loadout.PinRecipe(id);
                yield return new WaitForSecondsRealtime(.35f);
                Canvas.ForceUpdateCanvases();
                Assert.AreEqual(id, loadout.Pinned);
                var detail = menu.GetComponentsInChildren<RectTransform>().Single(t => t.name == "Recipe detail");
                var art = detail.GetComponentsInChildren<RawImage>().Single(t => t.name == "Art " + id);
                Assert.NotNull(art.texture, id + " recipe detail has actual generated pixels.");
                string asset = AssetDatabase.GetAssetPath(art.texture);
                Assert.That(asset.Replace('\\', '/'), Does.EndWith("/" + id.ToLowerInvariant() + "-v1.png"),
                    "Recipe detail must use its generated item art, not the model thumbnail fallback.");
                var card = menu.GetComponentsInChildren<Button>().Single(t => t.name == "Recipe " + id);
                var cardArt = card.GetComponentsInChildren<RawImage>().Single(t => t.name == "Art " + id);
                Assert.AreSame(art.texture, cardArt.texture);
                yield return Capture("recipe-" + id.ToLowerInvariant(), id, menu.Stage.Camera, null, asset, id);
            }
        }

        static HeldWeapon Equip(PlayerController player, string id)
        {
            player.Combat.ResetForRound();
            var definition = GameConfig.Current.Items.Get(id);
            Assert.NotNull(definition);
            Assert.IsTrue(definition.Enabled, id + " must be an enabled recipe.");
            var gear = CatalogGear.Create(definition);
            Assert.IsTrue(player.Combat.TryEquip(gear), "Equip " + id);
            return gear;
        }

        IEnumerator CaptureConsumption(PlayerController player, Camera lens, string id)
        {
            var gear = Equip(player, id);
            var use = gear.Definition.Use;
            Assert.NotNull(use);
            if (use.Effect == UseEffect.Heal)
            {
                yield return WaitForScaled(() => !player.Health.IsInvulnerable, 3f, "spawn protection expires before heal setup");
                Assert.IsTrue(player.Health.ApplyDamage(Hits.Of(null, Vector3.back, HitSource.Hazard, 35f, 0f, hitStun: 0f)));
                yield return new WaitForSeconds(.3f);
            }
            yield return new WaitForSeconds(.15f);
            float beforeHealth = player.Health.Current;
            float started = Time.time;
            player.Combat.Attack();
            Assert.IsTrue(gear.IsUsing, id + " begins its real use channel.");
            yield return Capture("use-" + id.ToLowerInvariant() + "-start", id, lens, player, actionStarted: started);
            yield return WaitUntilScaled(started + use.ChannelSeconds * .55f);
            Assert.IsTrue(gear && gear.IsUsing, id + " still exists during its channel.");
            yield return Capture("use-" + id.ToLowerInvariant() + "-middle", id, lens, player, actionStarted: started);
            yield return WaitForScaled(() => !player.Combat.Weapon, use.ChannelSeconds + 1f, id + " consumed after channel");
            if (use.Effect == UseEffect.Heal) Assert.Greater(player.Health.Current, beforeHealth);
            if (use.Effect == UseEffect.Speed) Assert.Greater(player.BoostLeft, 0f);
            yield return Capture("use-" + id.ToLowerInvariant() + "-complete", id, lens, player, actionStarted: started);
            yield return new WaitForSeconds(.65f);
        }

        IEnumerator CaptureFields(PlayerController player, Camera lens)
        {
            var assistant = Object.Instantiate(GameAssets.I.playerPrefab, new Vector3(0f, .04f, -3f), Quaternion.identity);
            assistant.Setup(1, new CaptureBinding());
            assistant.Inventory.Collects = false;
            var placements = new[] { ("FAN", -4f), ("MAT", 0f), ("CLOCK", 4f) };
            foreach (var (id, x) in placements)
            {
                var owner = id == "MAT" ? assistant : player;
                owner.Respawn(new Vector3(x, .04f, -1.6f));
                owner.FaceTowards(Vector3.forward);
                Physics.SyncTransforms();
                var gear = Equip(owner, id);
                Assert.IsTrue(owner.Combat.DeployHeld(), "Normal placement channel starts for " + id);
                yield return WaitForScaled(() => !owner.Combat.IsDeploying && !owner.Combat.Weapon,
                    gear.Definition.Deploy.PlaceSeconds + 1f, id + " deployment");
                Assert.NotNull(gear.GetComponent<DeployedGear>());
                Assert.That(DeployedGear.CountFor(owner), Is.LessThanOrEqualTo(owner.Health.Rules.MaxDeployed));
            }
            player.Respawn(new Vector3(0f, .04f, -3f));
            player.FaceTowards(Vector3.forward);
            assistant.Respawn(new Vector3(1.5f, .04f, -3f));
            assistant.FaceTowards(Vector3.forward);
            var fields = Object.FindObjectsByType<DeployedPowerField>(FindObjectsSortMode.None);
            Assert.AreEqual(2, fields.Length);
            Assert.IsTrue(fields.Any(f => f.Effect == DeployEffect.WindField));
            Assert.IsTrue(fields.Any(f => f.Effect == DeployEffect.SlowField));
            yield return new WaitForSeconds(.2f);
            FrameDeployedEffects(lens);
            var fan = fields.Single(field => field.Effect == DeployEffect.WindField);
            var clock = fields.Single(field => field.Effect == DeployEffect.SlowField);
            Assert.NotNull(fan.VisualRoot);
            Assert.NotNull(clock.VisualRoot);
            var wind = fan.VisualRoot.GetComponentsInChildren<LineRenderer>()
                .Where(line => line.name == "Moving wind ribbon").OrderBy(line => line.transform.GetSiblingIndex()).ToArray();
            Assert.AreEqual(3, wind.Length, "The FAN has three live breeze ribbons.");
            var hand = clock.VisualRoot.Find("Moving clock hand");
            Assert.NotNull(hand);
            var clockLine = hand.GetComponent<LineRenderer>();
            var mat = Object.FindObjectsByType<HeldWeapon>(FindObjectsSortMode.None)
                .Single(gear => gear.word == "MAT" && gear.GetComponent<DeployedGear>());
            var chevrons = mat.GetComponentsInChildren<LineRenderer>()
                .Where(line => line.name == "Direction chevron").OrderBy(line => line.transform.GetSiblingIndex()).ToArray();
            Assert.AreEqual(2, chevrons.Length, "The MAT has two live direction chevrons.");
            var liveLines = wind.Concat(new[] { clockLine }).Concat(chevrons).ToArray();
            void ValidateEffects()
            {
                Assert.IsTrue(fan && fan.isActiveAndEnabled && fan.VisualRoot && fan.VisualRoot.gameObject.activeInHierarchy,
                    "The deployed FAN and its visual root must survive both capture frames.");
                Assert.IsTrue(clock && clock.isActiveAndEnabled && clock.VisualRoot && clock.VisualRoot.gameObject.activeInHierarchy,
                    "The deployed CLOCK and its visual root must survive both capture frames.");
                Assert.IsTrue(mat && mat.GetComponent<DeployedGear>() && mat.gameObject.activeInHierarchy,
                    "The deployed MAT must survive both capture frames.");
                var planes = GeometryUtility.CalculateFrustumPlanes(lens);
                foreach (var line in liveLines)
                {
                    Assert.IsTrue(line && line.enabled && line.gameObject.activeInHierarchy && !line.forceRenderingOff,
                        "Every captured field renderer must remain live, active and enabled.");
                    Assert.AreNotEqual(UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly, line.shadowCastingMode, line.name);
                    Assert.GreaterOrEqual(line.positionCount, 2, line.name + " has drawable geometry.");
                    Assert.Greater(Mathf.Max(line.startWidth, line.endWidth), 0f, line.name + " has visible width.");
                    Assert.IsTrue(GeometryUtility.TestPlanesAABB(planes, line.bounds), line.name + " remains in the capture camera frustum.");
                }
            }
            ValidateEffects();
            var firstWind = wind[0].GetPosition(0);
            var firstClock = hand.localRotation;
            var firstMat = chevrons[0].transform.localPosition;
            float started = Time.time;
            yield return Capture("deployed-fields-time-a", "FAN CLOCK MAT", lens, player, actionStarted: started, validateFrame: ValidateEffects);
            yield return new WaitForSeconds(.37f);
            ValidateEffects();
            Assert.Greater((wind[0].GetPosition(0) - firstWind).sqrMagnitude, .00001f, "The same live FAN ribbon animates during scaled gameplay time.");
            Assert.Greater(Quaternion.Angle(hand.localRotation, firstClock), 1f, "The same live CLOCK hand advances during scaled gameplay time.");
            Assert.Greater((chevrons[0].transform.localPosition - firstMat).sqrMagnitude, .00001f, "The same live MAT chevron moves during scaled gameplay time.");
            yield return Capture("deployed-fields-time-b", "FAN CLOCK MAT", lens, player, actionStarted: started, validateFrame: ValidateEffects);
        }

        static void FrameDeployedEffects(Camera lens)
        {
            var renderers = Object.FindObjectsByType<DeployedGear>(FindObjectsSortMode.None)
                .SelectMany(gear => gear.GetComponentsInChildren<Renderer>())
                .Concat(World.Players.SelectMany(player => player.GetComponentsInChildren<Renderer>()))
                .Where(renderer => renderer.enabled && !renderer.forceRenderingOff).ToArray();
            Assert.IsNotEmpty(renderers);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var from = new Vector3(6f, 7f, 10f).normalized;
            lens.transform.rotation = Quaternion.LookRotation(-from, Vector3.up);
            lens.fieldOfView = 44f;
            float vertical = Mathf.Tan(lens.fieldOfView * .5f * Mathf.Deg2Rad) * .84f;
            float horizontal = vertical * lens.aspect;
            float distance = 1f;
            for (int corner = 0; corner < 8; corner++)
            {
                var offset = Vector3.Scale(bounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                float depth = Vector3.Dot(offset, lens.transform.forward);
                distance = Mathf.Max(distance, Mathf.Abs(Vector3.Dot(offset, lens.transform.right)) / horizontal - depth,
                    Mathf.Abs(Vector3.Dot(offset, lens.transform.up)) / vertical - depth, lens.nearClipPlane - depth + .1f);
            }
            lens.transform.position = bounds.center + from * distance;
        }

        IEnumerator CaptureCouchMatch()
        {
            Session.Clear();
            Session.SelectMap("pinwheel");
            Match.ModeOverride = "Dibs";
            yield return TestScenes.Load(Session.DibsScene);
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var firstInput = new CaptureBinding();
            var secondInput = new CaptureBinding();
            var first = joins.Join(firstInput);
            var second = joins.Join(secondInput);
            var rounds = RoundManager.Instance;
            rounds.CountdownTime = .15f;
            rounds.StartMatch();
            yield return WaitForScaled(() => rounds.Phase == Phase.Playing, 3f, "two-local active round");
            var room = Object.FindAnyObjectByType<RoomBuilder>().Layout.Rooms
                .OrderByDescending(r => (r.MaxX - r.MinX) * (r.MaxZ - r.MinZ)).First();
            var center = new Vector3((room.MinX + room.MaxX) * .5f, room.FloorY + .08f, (room.MinZ + room.MaxZ) * .5f);
            bool Inside(Vector3 at) => at.x > room.MinX + .6f && at.x < room.MaxX - .6f &&
                at.z > room.MinZ + .6f && at.z < room.MaxZ - .6f;
            first.Respawn(ShoulderView.RoomySpot(center + Vector3.left * 1.4f, Inside));
            second.Respawn(ShoulderView.RoomySpot(center + Vector3.right * 1.4f, Inside));
            Equip(first, "SHIELD");
            Equip(second, "FAN");
            firstInput.Next.blockHeld = true;
            first.Frozen = second.Frozen = false;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.55f);
            var lens = Camera.main;
            Assert.AreEqual(2, joins.HumanCount);
            Assert.AreEqual(Phase.Playing, rounds.Phase);
            Assert.IsFalse(lens.GetComponent<CameraRig>().IsThirdPerson);
            Assert.IsTrue(lens.orthographic);
            Assert.IsTrue(first.Combat.IsBlocking);
            Assert.IsTrue(Object.FindAnyObjectByType<GameHud>().AcceptsGameplayInput);
            yield return Capture("active-local-couch-16x9", "SHIELD FAN", lens, first);
            gameView.Select(2100, 900);
            yield return new WaitForSecondsRealtime(.4f);
            yield return Capture("active-local-couch-21x9", "SHIELD FAN", lens, first);
        }

        IEnumerator CaptureSoloMatch()
        {
            gameView.Select(1600, 900);
            Session.Clear();
            Session.SelectMap("pinwheel");
            Match.ModeOverride = "Dibs";
            yield return TestScenes.Load(Session.DibsScene);
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var input = new CaptureBinding();
            var player = joins.Join(input);
            var rounds = RoundManager.Instance;
            rounds.CountdownTime = .15f;
            rounds.StartMatch();
            yield return WaitForScaled(() => rounds.Phase == Phase.Playing, 3f, "single-local active round");
            var room = Object.FindAnyObjectByType<RoomBuilder>().Layout.Rooms
                .OrderByDescending(r => (r.MaxX - r.MinX) * (r.MaxZ - r.MinZ)).First();
            var center = new Vector3((room.MinX + room.MaxX) * .5f, room.FloorY + .08f, (room.MinZ + room.MaxZ) * .5f);
            bool Inside(Vector3 at) => at.x > room.MinX + .6f && at.x < room.MaxX - .6f &&
                at.z > room.MinZ + .6f && at.z < room.MaxZ - .6f;
            player.Respawn(ShoulderView.RoomySpot(center, Inside));
            player.FaceTowards(Vector3.forward);
            Equip(player, "SHIELD");
            input.Next.blockHeld = true;
            player.Frozen = false;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.75f);
            var lens = Camera.main;
            Assert.AreEqual(1, joins.HumanCount);
            Assert.IsTrue(lens.GetComponent<CameraRig>().IsThirdPerson);
            Assert.IsFalse(lens.orthographic);
            Assert.IsTrue(player.Combat.IsBlocking);
            Assert.IsTrue(Object.FindAnyObjectByType<GameHud>().AcceptsGameplayInput);
            yield return Capture("active-centered-third-person", "SHIELD", lens, player);
        }

        static IEnumerator WaitUntilScaled(float time)
        {
            while (Time.time < time) yield return null;
        }

        static IEnumerator WaitForScaled(Func<bool> condition, float seconds, string what)
        {
            float deadline = Time.time + seconds, realtimeLimit = Time.realtimeSinceStartup + 30f;
            while (!condition())
            {
                if (Time.time > deadline || Time.realtimeSinceStartup > realtimeLimit)
                    Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }

        static Camera CreateStage()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Power presentation stage floor";
            floor.transform.position = new Vector3(0f, -.12f, 0f);
            floor.transform.localScale = new Vector3(24f, .24f, 22f);
            floor.GetComponent<Renderer>().sharedMaterial = GameAssets.I.Tinted(new Color(.72f, .66f, .53f));
            var lens = new GameObject("Power presentation camera").AddComponent<Camera>();
            lens.tag = "MainCamera";
            lens.backgroundColor = new Color(.10f, .17f, .17f);
            lens.clearFlags = CameraClearFlags.SolidColor;
            lens.fieldOfView = 44f;
            lens.transform.position = new Vector3(1.15f, 1.5f, 2.7f);
            lens.transform.LookAt(new Vector3(0f, .78f, 0f));
            var key = new GameObject("Power presentation key").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.2f;
            key.transform.rotation = Quaternion.Euler(42f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.67f, .70f, .71f);
            TilePool.Ensure();
            return lens;
        }

        static int VisibleRenderers(GameObject root, Camera lens)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(lens);
            return root.GetComponentsInChildren<Renderer>().Count(r => r.enabled && !r.forceRenderingOff &&
                GeometryUtility.TestPlanesAABB(planes, r.bounds));
        }

        static Vector3 WindPoint()
        {
            var line = Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).FirstOrDefault(r => r.name == "Moving wind ribbon");
            return line && line.positionCount > 0 ? line.GetPosition(0) : Vector3.zero;
        }

        static Quaternion ClockHand()
        {
            var field = Object.FindObjectsByType<DeployedPowerField>(FindObjectsSortMode.None).FirstOrDefault(f => f.Effect == DeployEffect.SlowField);
            var hand = field && field.VisualRoot ? field.VisualRoot.Find("Moving clock hand") : null;
            return hand ? hand.localRotation : Quaternion.identity;
        }

        static Vector3 MatChevron()
        {
            var mat = Object.FindObjectsByType<HeldWeapon>(FindObjectsSortMode.None).FirstOrDefault(g => g.word == "MAT" && g.GetComponent<DeployedGear>());
            var chevron = mat ? mat.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Direction chevron") : null;
            return chevron ? chevron.localPosition : Vector3.zero;
        }

        IEnumerator Capture(string view, string item, Camera lens, PlayerController player, string artAsset = "", string pinnedRecipe = "", float actionStarted = -1f, Action validateFrame = null)
        {
            Assert.NotNull(lens);
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            validateFrame?.Invoke();
            var gear = player ? player.Combat.Weapon : null;
            var renderers = gear ? gear.GetComponentsInChildren<Renderer>() : Array.Empty<Renderer>();
            var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds();
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            string file = $"{frames.Count + 1:00}-{view}.png";
            var pixels = ScreenCapture.CaptureScreenshotAsTexture();
            Assert.NotNull(pixels);
            try
            {
                Assert.AreEqual(view.Contains("21x9") ? 2100 : 1600, pixels.width);
                Assert.AreEqual(900, pixels.height);
                File.WriteAllBytes(Path.Combine(directory, file), pixels.EncodeToPNG());
                var rig = lens.GetComponent<CameraRig>();
                var appearance = player ? player.GetComponent<PlayerAppearance>() : null;
                frames.Add(new Frame
                {
                    file = file, scene = SceneManager.GetActiveScene().name, view = view, item = item,
                    phase = RoundManager.Instance ? RoundManager.Instance.Phase.ToString() : "Demo stage",
                    width = pixels.width, height = pixels.height, localSeats = World.Players.Count(CameraRig.IsHuman),
                    cameraPosition = lens.transform.position, cameraForward = lens.transform.forward,
                    playerPosition = player ? player.transform.position : Vector3.zero,
                    thirdPerson = rig && rig.IsThirdPerson, orthographic = lens.orthographic,
                    hasHeldItem = gear, usingItem = gear && gear.IsUsing, blocking = player && player.Combat.IsBlocking,
                    shieldAura = gear && gear.TryGetComponent<HeldShieldAura>(out var aura) && aura.Visible,
                    renderedItems = gear ? VisibleRenderers(gear.gameObject, lens) : 0,
                    targetInFrame = gear && VisibleRenderers(gear.gameObject, lens) > 0, heldSize = bounds.size,
                    clip = appearance && appearance.CurrentAnimationClip ? appearance.CurrentAnimationClip.name : "",
                    health = player ? player.Health.Current : 0f, slowLeft = player ? player.SlowLeft : 0f,
                    boostLeft = player ? player.BoostLeft : 0f,
                    elapsedSinceAction = actionStarted < 0f ? -1f : Time.time - actionStarted,
                    artAsset = artAsset, pinnedRecipe = pinnedRecipe,
                    fields = Object.FindObjectsByType<DeployedPowerField>(FindObjectsSortMode.None).Length,
                    windPoint = WindPoint(), clockHand = ClockHand(), matChevron = MatChevron()
                });
            }
            finally { Object.Destroy(pixels); }
            Debug.Log("[PowerCapture] " + file);
        }

        /// <summary>Leaves the user's resolution list and selection exactly as they were.</summary>
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
                        new object[] { Enum.Parse(kindType, "FixedResolution"), width, height, "Power capture temporary" }, null);
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
