using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class CombatFeedbackTests
    {
        sealed class LocalInput : InputBinding
        {
            public override string Id => "combat-feedback-local";
            public override bool CanLook => true;
            public override void Read(ref PlayerCommands commands) { }
            public override bool JoinPressed() => false;
            public override bool StartPressed() => false;
        }
        readonly Dictionary<string, int?> saved = new();
        bool? focus;
        [UnitySetUp] public IEnumerator SetUp()
        {
            foreach (string key in new[] { CombatFeedbackOptions.DamageKey, CombatFeedbackOptions.PointsKey })
                saved[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : null;
            focus = GameHud.PauseOnFocusLossOverride; GameHud.PauseOnFocusLossOverride = false;
            CombatFeedbackOptions.ResetToDefaults();
            yield return TestScenes.Reset();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            yield return TestScenes.Reset();
            foreach (var pair in saved)
                if (pair.Value.HasValue) PlayerPrefs.SetInt(pair.Key, pair.Value.Value); else PlayerPrefs.DeleteKey(pair.Key);
            PlayerPrefs.Save(); GameHud.PauseOnFocusLossOverride = focus;
        }
        static void Click(Transform root, string name) => root.GetComponentsInChildren<Button>().Single(b => b.name == name).onClick.Invoke();
        static string[] Popups() => Object.FindObjectsByType<Popup>(FindObjectsSortMode.None).Select(p => p.GetComponent<TMP_Text>().text).ToArray();
        static void ClearPopups() { foreach (var popup in Object.FindObjectsByType<Popup>(FindObjectsSortMode.None)) Object.DestroyImmediate(popup.gameObject); }

        [UnityTest] public IEnumerator SettingsPersistIndependentlyAcrossPauseAndLobbyAndReset()
        {
            yield return TestScenes.Load(Session.DibsScene);
            var hud = Object.FindAnyObjectByType<GameHud>();
            hud.StateController.FadeDuration = 0;
            hud.TogglePause(); Click(hud.transform, "Pause settings"); Click(hud.transform, "Pause tab gameplay");
            Click(hud.transform, "Damage numbers toggle");
            Assert.IsFalse(CombatFeedbackOptions.ShowDamage); Assert.IsTrue(CombatFeedbackOptions.ShowPoints);
            Assert.AreEqual(0, PlayerPrefs.GetInt(CombatFeedbackOptions.DamageKey));
            Click(hud.transform, "Points per hit toggle");
            Assert.IsFalse(CombatFeedbackOptions.ShowPoints);
            Click(hud.transform, "Settings back"); Click(hud.transform, "Pause settings"); Click(hud.transform, "Pause tab gameplay");
            Assert.AreEqual("OFF", hud.GetComponentsInChildren<Button>().Single(b => b.name == "Damage numbers toggle").GetComponentInChildren<TMP_Text>().text);
            hud.TogglePause();
            yield return TestScenes.Load(Session.HubScene);
            var menu = Object.FindAnyObjectByType<LobbyMenu>(); menu.Open(LobbyMenu.Settings); menu.Page<SettingsPage>().ShowTab("gameplay");
            Assert.IsFalse(CombatFeedbackOptions.ShowDamage); Assert.IsFalse(CombatFeedbackOptions.ShowPoints);
            Click(menu.transform, "Reset feedback");
            Assert.IsTrue(CombatFeedbackOptions.ShowDamage && CombatFeedbackOptions.ShowPoints);
            Assert.IsFalse(PlayerPrefs.HasKey(CombatFeedbackOptions.DamageKey));
            Assert.IsFalse(PlayerPrefs.HasKey(CombatFeedbackOptions.PointsKey));
        }

        [UnityTest] public IEnumerator ActualHitsRespectBothSwitchesWithoutChangingDamageOrEarnedScore()
        {
            yield return TestScenes.Load(Session.DibsScene);
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var player = joins.Join(new LocalInput());
            var victim = joins.Join(new ScriptedBinding());
            var rules = Match.Rules.Clone(); rules.SpawnProtectionSeconds = 0;
            victim.Health.UseRules(rules);
            player.Frozen = victim.Frozen = true;
            yield return null;
            var hit = HitInfo.From(player.Index, player.Team, rules.Unarmed, null, 0, 1);
            ClearPopups();
            Assert.IsTrue(victim.Health.ApplyDamage(hit));
            Assert.That(Popups(), Has.Some.Contains("DMG")); Assert.That(Popups(), Has.Some.Contains("PTS"));
            CombatFeedbackOptions.ShowDamage = false; ClearPopups();
            victim.Health.ApplyDamage(hit);
            Assert.That(Popups(), Has.None.Contains("DMG")); Assert.That(Popups(), Has.Some.Contains("PTS"));
            CombatFeedbackOptions.ShowPoints = false; CombatFeedbackOptions.ShowDamage = true; ClearPopups();
            victim.Health.ApplyDamage(hit);
            Assert.That(Popups(), Has.Some.Contains("DMG")); Assert.That(Popups(), Has.None.Contains("PTS"));
            CombatFeedbackOptions.ShowDamage = false; ClearPopups();
            victim.Health.ApplyDamage(hit);
            Assert.IsEmpty(Popups());
            Assert.AreEqual(100 - 4 * rules.Unarmed.Damage, victim.Health.Current);
            Assert.AreEqual(4 * rules.Unarmed.Damage, MatchTally.RoundDamage);
            var prop = new GameObject("Feedback prop", typeof(Rigidbody)).AddComponent<Smashable>(); prop.Init("BOX", 100);
            prop.TakeHit(12); Assert.IsEmpty(Popups());
            CombatFeedbackOptions.ShowDamage = true; prop.TakeHit(12);
            Assert.That(Popups(), Has.Some.Contains("12 <size=60%>DMG"));
        }

        [UnityTest] public IEnumerator ControllerReticleShowsItsActualGrabTrigger()
        {
            var pad = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Gamepad>();
            try
            {
                yield return TestScenes.Load(Session.DibsScene);
                var player = Object.FindAnyObjectByType<PlayerJoinManager>().Join(new GamepadBinding(pad));
                var rounds = RoundManager.Instance; rounds.CountdownTime = .1f; rounds.StartMatch();
                yield return TestScenes.WaitUntil(() => rounds.Phase == Phase.Playing, 3, "controller round playing");
                foreach (var other in World.Players)
                    if (other != player) { other.Frozen = true; if (other.TryGetComponent<BotController>(out var bot)) bot.enabled = false; }
                player.Respawn(new Vector3(0, .05f, -7)); player.FaceTowards(Vector3.forward); player.ResetLook();
                yield return new WaitForSeconds(.4f);
                var camera = CameraRig.Instance.ViewCamera;
                var ray = camera.ViewportPointToRay(new Vector3(.5f, .5f));
                var ball = CatalogGear.Create(GameConfig.Current.Items.Get("BALL"));
                var at = ray.GetPoint(Vector3.Dot(player.transform.position + Vector3.up * .8f + player.Facing - ray.origin, ray.direction));
                var body = ball.GetComponent<Rigidbody>(); body.useGravity = false; body.position = ball.transform.position = at;
                Physics.SyncTransforms(); yield return new WaitForSeconds(.2f);
                Assert.AreSame(body, player.Combat.GrabTarget());
                Assert.AreEqual("RT", Object.FindAnyObjectByType<GameHud>().transform.Find("Crosshair/Interaction key").GetComponent<TMP_Text>().text);
            }
            finally { if (pad.added) UnityEngine.InputSystem.InputSystem.RemoveDevice(pad); }
        }

        [UnityTest] public IEnumerator NearbyHitNumbersReserveSeparateScreenPositions()
        {
            yield return TestScenes.Load(Session.DibsScene);
            var player = Object.FindAnyObjectByType<PlayerJoinManager>().Join(new LocalInput());
            yield return new WaitForSeconds(.2f);
            ClearPopups();
            var camera = CameraRig.Instance.ViewCamera;
            var at = camera.ViewportToWorldPoint(new Vector3(.5f, .5f, 5));
            Popup.Damage(12, at); Popup.Damage(8, at); Popup.Points(8, at);
            var popups = Object.FindObjectsByType<Popup>(FindObjectsSortMode.None);
            Assert.AreEqual(3, popups.Length);
            for (int i = 0; i < popups.Length; i++)
                for (int j = i + 1; j < popups.Length; j++)
                    Assert.Greater(Vector2.Distance(camera.WorldToScreenPoint(popups[i].transform.position), camera.WorldToScreenPoint(popups[j].transform.position)), camera.pixelHeight * .055f);
        }

        [UnityTest] public IEnumerator HudShowsSpentStaminaBesideHealthAndFreezesRecoveryInPause()
        {
            yield return TestScenes.Load(Session.DibsScene);
            var player = Object.FindAnyObjectByType<PlayerJoinManager>().Join(new LocalInput());
            yield return new WaitForSeconds(.15f);
            var hud = Object.FindAnyObjectByType<GameHud>();
            Assert.AreSame(player, hud.LocalPlayer);
            var stamina = hud.transform.Find("Safe HUD/Vitals/Stamina").GetComponent<TMP_Text>();
            Assert.That(stamina.text, Does.StartWith("100"));
            Assert.IsTrue(player.Health.Dodge());
            yield return new WaitForSeconds(.15f);
            Assert.That(stamina.text, Does.StartWith("70"));
            Assert.AreEqual(91, hud.transform.Find("Safe HUD/Vitals/Stamina track/Stamina fill").GetComponent<RectTransform>().rect.width, .01f);
            hud.TogglePause(); yield return new WaitForSecondsRealtime(.2f);
            Assert.AreEqual(70, player.Health.Stamina);
            hud.TogglePause(); yield return new WaitForSeconds(1.3f);
            Assert.Greater(player.Health.Stamina, 70);
        }

        [UnityTest, Explicit, Category("InteractionCapture")]
        public IEnumerator CaptureCrosshairHandFeedbackAndSettings()
        {
            var view = new UIRefinementCaptureTests.GameViewScope();
            bool priorAsync = EditorSettings.asyncShaderCompilation;
            EditorSettings.asyncShaderCompilation = false;
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/reviews/evidence/unity-interaction-feedback-2026-10-09"));
            Directory.CreateDirectory(directory);
            var files = new List<string>();
            IEnumerator Capture(string name, float settle = .15f)
            {
                Canvas.ForceUpdateCanvases(); yield return new WaitForSecondsRealtime(settle); yield return new WaitForEndOfFrame();
                var texture = ScreenCapture.CaptureScreenshotAsTexture();
                try { File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG()); files.Add(name + ".png"); }
                finally { Object.Destroy(texture); }
            }
            try
            {
                view.Select(1600, 900);
                yield return TestScenes.Load(Session.DibsScene);
                var player = Object.FindAnyObjectByType<PlayerJoinManager>().Join(new LocalInput());
                var rounds = RoundManager.Instance; rounds.CountdownTime = .1f; rounds.StartMatch();
                yield return TestScenes.WaitUntil(() => rounds.Phase == Phase.Playing, 3, "playing round");
                foreach (var other in World.Players)
                    if (other != player) { other.Frozen = true; if (other.TryGetComponent<BotController>(out var bot)) bot.enabled = false; }
                player.Respawn(new Vector3(0, .05f, -7)); player.FaceTowards(Vector3.forward); player.ResetLook();
                player.Inventory.Set("APPLEWATER");
                yield return new WaitForSeconds(3f);
                var hud = Object.FindAnyObjectByType<GameHud>();
                var rig = CameraRig.Instance;
                Assert.IsTrue(rig.IsThirdPerson);
                var ball = CatalogGear.Create(GameConfig.Current.Items.Get("BALL"));
                var ray = rig.ViewCamera.ViewportPointToRay(new Vector3(.5f, .5f));
                float distance = Vector3.Dot(player.transform.position + Vector3.up * .8f + player.Facing - ray.origin, ray.direction);
                ball.transform.position = ball.GetComponent<Rigidbody>().position = ray.GetPoint(distance);
                ball.GetComponent<Rigidbody>().useGravity = false;
                Physics.SyncTransforms();
                yield return new WaitForSeconds(.2f);
                Assert.AreSame(ball.GetComponent<Rigidbody>(), player.Combat.GrabTarget());
                yield return Capture("01-crosshair-reachable");
                Assert.IsTrue(player.Combat.TryGrab());
                ball.GetComponent<Rigidbody>().useGravity = true;
                yield return new WaitForSeconds(.6f);
                yield return Capture("02-held-at-hand");
                player.Combat.Throw();
                yield return Capture("03-released-from-hand", .05f);
                var victim = World.Players.First(p => p != player);
                victim.Respawn(player.transform.position + player.Facing * 2.5f + Vector3.right * .6f);
                var rules = Match.Rules.Clone(); rules.SpawnProtectionSeconds = 0; victim.Health.UseRules(rules);
                yield return new WaitForSeconds(.3f);
                Assert.IsTrue(player.Health.Dodge());
                victim.Health.ApplyDamage(HitInfo.From(player.Index, player.Team, rules.Unarmed, null, 0, 1));
                Assert.That(Popups(), Has.Some.Contains("PTS"));
                yield return Capture("04-damage-points-stamina", .12f);
                hud.TogglePause(); Click(hud.transform, "Pause settings"); Click(hud.transform, "Pause tab gameplay");
                yield return Capture("05-feedback-settings");
                Click(hud.transform, "Damage numbers toggle"); Click(hud.transform, "Points per hit toggle");
                yield return Capture("06-feedback-off");
                Click(hud.transform, "Settings back"); hud.TogglePause();
                ClearPopups(); victim.Health.ApplyDamage(HitInfo.From(player.Index, player.Team, rules.Unarmed, null, 0, 1));
                Assert.IsEmpty(Popups()); yield return Capture("07-clean-hit-feedback-off");
                view.Select(2100, 900); yield return Capture("08-ultrawide-stamina");
                view.Select(1280, 720); yield return Capture("09-compact-stamina");
                File.WriteAllText(Path.Combine(directory, "verified.txt"), Application.unityVersion + "\n" + DateTime.UtcNow.ToString("O") + "\n" + string.Join("\n", files));
            }
            finally { view.Dispose(); EditorSettings.asyncShaderCompilation = priorAsync; }
        }
    }
}
