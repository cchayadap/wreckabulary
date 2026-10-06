using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    /// <summary>
    /// Renders screenshots of a staged moment in the Living Room, for reviewing the look without opening the editor.
    /// Explicit: run with -testFilter Wreckabulary.Tests.CaptureTests. Output goes to WRECK_CAPTURE_DIR (or Temp/Captures).
    /// </summary>
    [Explicit, Category("Capture")]
    public class CaptureTests
    {
        ShaderCompilationScope shaderCompilationScope;

        [UnityTest]
        public IEnumerator CaptureLivingRoom() => RunWithSynchronousShaders(CaptureLivingRoomSequence());

        [UnityTest]
        public IEnumerator CaptureHubAndTutorial() => RunWithSynchronousShaders(CaptureHubAndTutorialSequence());

        [UnityTest]
        public IEnumerator CaptureLobby() => RunWithSynchronousShaders(CaptureLobbySequence());

        [UnityTest]
        public IEnumerator CaptureMaps() => RunWithSynchronousShaders(CaptureMapsSequence());

        [UnityTest]
        public IEnumerator CaptureMatchCards() => RunWithSynchronousShaders(CaptureMatchCardsSequence());

        [TearDown]
        public void RestoreShaderCompilation()
        {
            shaderCompilationScope?.Dispose();
            shaderCompilationScope = null;
        }

        internal IEnumerator RunWithSynchronousShaders(IEnumerator sequence)
        {
            if (shaderCompilationScope != null)
                throw new InvalidOperationException("A capture is already running on this fixture.");

            var scope = new ShaderCompilationScope();
            shaderCompilationScope = scope;
            try
            {
                while (sequence.MoveNext()) yield return sequence.Current;
            }
            finally
            {
                try
                {
                    (sequence as IDisposable)?.Dispose();
                }
                finally
                {
                    scope.Dispose();
                    if (ReferenceEquals(shaderCompilationScope, scope)) shaderCompilationScope = null;
                }
            }
        }

        sealed class ShaderCompilationScope : IDisposable
        {
#if UNITY_EDITOR
            readonly bool previousValue;
#endif
            bool disposed;

            public ShaderCompilationScope()
            {
#if UNITY_EDITOR
                previousValue = UnityEditor.EditorSettings.asyncShaderCompilation;
                // Otherwise lit objects are skipped while their shaders compile in the background.
                UnityEditor.EditorSettings.asyncShaderCompilation = false;
#endif
            }

            public void Dispose()
            {
                if (disposed) return;
#if UNITY_EDITOR
                UnityEditor.EditorSettings.asyncShaderCompilation = previousValue;
#endif
                disposed = true;
            }
        }

        IEnumerator CaptureLivingRoomSequence()
        {
            string dir = Environment.GetEnvironmentVariable("WRECK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.dataPath, "../Temp/Captures");
            Directory.CreateDirectory(dir);
            Session.Clear();
            yield return SceneManager.LoadSceneAsync("LivingRoom");
            yield return new WaitForSeconds(0.5f);
            Capture(Path.Combine(dir, "1_lobby.png"));

            var joins = UnityEngine.Object.FindAnyObjectByType<PlayerJoinManager>();
            var inputs = new ScriptedBinding[3];
            for (int i = 0; i < 3; i++) joins.Join(inputs[i] = new ScriptedBinding());
            RoundManager.Instance.CountdownTime = 0.1f;
            RoundManager.Instance.StartMatch();
            yield return new WaitForSeconds(0.5f);

            var p2 = World.Players[1];
            var p3 = World.Players[2];

            // The table bursts and P1 and P2 run for the letters.
            foreach (var s in UnityEngine.Object.FindObjectsByType<Smashable>())
                if (s.Word == "TABLE") s.Break();
            inputs[0].Next.move = new Vector2(0.6f, 0.8f);
            inputs[1].Next.move = new Vector2(-0.7f, 0.3f);
            yield return new WaitForSeconds(0.8f);
            inputs[0].Next.move = Vector2.zero;
            inputs[1].Next.move = Vector2.zero;

            // P3 summons a SWORD, P2 opens the word wheel.
            p3.Inventory.Set("SWORDE");
            p3.Summoner.Summon("SWORD");
            p2.Inventory.Set("BEESTA");
            inputs[1].Next.spellHeld = true;
            inputs[1].Next.spellDown = true;
            yield return new WaitForSeconds(0.4f);
            Capture(Path.Combine(dir, "2_action.png"));

            inputs[1].Next.spellHeld = false;
            inputs[1].Next.spellUp = true;
            yield return new WaitForSeconds(0.6f);
            Capture(Path.Combine(dir, "3_bees.png"));

            CameraRig.Instance.Follow(World.Players[0]);
            yield return new WaitForSeconds(0.8f);
            yield return CaptureFramed(Path.Combine(dir, "4_third_person.png"));
            CameraRig.Instance.Follow(null);
        }

        IEnumerator CaptureHubAndTutorialSequence()
        {
            string dir = Environment.GetEnvironmentVariable("WRECK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.dataPath, "../Temp/Captures");
            Directory.CreateDirectory(dir);
            Session.Clear();

            yield return SceneManager.LoadSceneAsync(Session.HubScene);
            yield return null;
            if (LobbyMenu.Instance) UnityEngine.Object.Destroy(LobbyMenu.Instance.gameObject);
            yield return null;
            var joins = UnityEngine.Object.FindAnyObjectByType<PlayerJoinManager>();
            var a = joins.Join(new ScriptedBinding());
            yield return new WaitForSeconds(0.3f);
            Capture(Path.Combine(dir, "4_hub_arrival.png"));
            joins.Join(new ScriptedBinding());
            yield return new WaitForSeconds(1f);
            var typewriter = UnityEngine.Object.FindAnyObjectByType<Typewriter>();
            a.Respawn(typewriter.transform.position + Vector3.back * 1.2f + Vector3.down * 0.82f);
            typewriter.Open(a);
            yield return new WaitForSeconds(0.3f);
            Capture(Path.Combine(dir, "5_hub_typewriter.png"));

            // Tutorial, a few steps in.
            Session.Clear();
            yield return SceneManager.LoadSceneAsync(Session.TutorialScene);
            yield return null;
            joins = UnityEngine.Object.FindAnyObjectByType<PlayerJoinManager>();
            var input = new ScriptedBinding();
            joins.Join(input);
            input.Next.move = new Vector2(1f, 0.3f);
            yield return new WaitForSeconds(1.2f);
            input.Next.move = Vector2.zero;
            yield return new WaitForSeconds(1.5f);
            Capture(Path.Combine(dir, "6_tutorial.png"));

            // Moving Day: the bed is in, boxes are arriving.
            Session.Clear();
            yield return SceneManager.LoadSceneAsync(Session.MovingDayScene);
            yield return null;
            joins = UnityEngine.Object.FindAnyObjectByType<PlayerJoinManager>();
            joins.Join(new ScriptedBinding());
            joins.Join(new ScriptedBinding());
            var director = UnityEngine.Object.FindAnyObjectByType<MovingDayDirector>();
            yield return new WaitForSeconds(4.5f);
            FurnitureCatalog.Spawn("BED", director.RoomNamed("Bedroom").Centre + new Vector3(0f, 0.3f, 2f), 0f, World.Transient);
            yield return new WaitForSeconds(2.5f);
            Capture(Path.Combine(dir, "7_moving_day.png"));
            Session.Clear();
        }

        static Rules.Career SampleCareer()
        {
            var career = new Rules.Career { Name = "Roomie" };
            long at = 1759750000;
            foreach (var (mode, map, won, score) in new[]
            {
                ("Dibs", "pinwheel", true, 920), ("Duos", "flat", false, 410), ("MovingOut", "terrace", true, 660),
                ("Dibs", "courtyard", false, 380), ("MovingDay", "walkup", true, 540), ("Dibs", "terrace", true, 610),
            })
            {
                career.Record(new Rules.MatchRecord
                {
                    Mode = mode, Map = map, Won = won, Score = score, Coins = score / 10 + (won ? 20 : 5), Xp = score / 2 + (won ? 60 : 25), EndedAt = at,
                });
                at += 5400;
            }
            return career;
        }

        IEnumerator CaptureLobbySequence()
        {
            string dir = Environment.GetEnvironmentVariable("WRECK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.dataPath, "../Temp/Captures");
            Directory.CreateDirectory(dir);
            TryLights();
            Session.Clear();
            string realCareer = PlayerPrefs.HasKey(MatchTally.CareerKey) ? PlayerPrefs.GetString(MatchTally.CareerKey) : null;
            PlayerPrefs.SetString(MatchTally.CareerKey, SampleCareer().Serialize());
            yield return SceneManager.LoadSceneAsync(Session.HubScene);
            yield return null;
            yield return null;
            if (realCareer != null) PlayerPrefs.SetString(MatchTally.CareerKey, realCareer);
            else PlayerPrefs.DeleteKey(MatchTally.CareerKey);
            PlayerPrefs.Save();
            yield return new WaitForSeconds(0.5f);
            var menu = LobbyMenu.Instance;
            var pages = new[]
            {
                (LobbyMenu.Home, "home"), (LobbyMenu.Play, "play"), (LobbyMenu.Loadout, "loadout"), (LobbyMenu.CareerPage, "career"),
                (LobbyMenu.Shop, "shop"), (LobbyMenu.Trophy, "trophy"), (LobbyMenu.Settings, "settings"),
            };
            for (int i = 0; i < pages.Length; i++)
            {
                menu.Open(pages[i].Item1);
                yield return new WaitForSeconds(1f);
                yield return CaptureFramed(Path.Combine(dir, $"lobby_{i + 1}_{pages[i].Item2}.png"));
            }
            menu.Open(LobbyMenu.Settings);
            var settings = menu.Page<SettingsPage>();
            foreach (string tab in new[] { "graphics", "controls", "how" })
            {
                settings.ShowTab(tab);
                yield return new WaitForSeconds(.5f);
                yield return CaptureFramed(Path.Combine(dir, $"lobby_7_settings_{tab}.png"));
            }
            settings.ShowTab("general");
            menu.Open(LobbyMenu.Home);
            var quit = menu.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == "Quit");
            quit.onClick.Invoke();
            yield return new WaitForSeconds(.5f);
            yield return CaptureFramed(Path.Combine(dir, "lobby_7_quit.png"));
            menu.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == "STAY").onClick.Invoke();
            menu.Open(LobbyMenu.Loadout);
            var loadout = menu.Page<LoadoutPage>();
            loadout.ShowRecipes(true);
            yield return new WaitForSeconds(1f);
            yield return CaptureFramed(Path.Combine(dir, "lobby_3b_recipes.png"));
            loadout.ShowRecipes(false);
            menu.OpenShop("colour:grape");
            yield return new WaitForSeconds(1f);
            yield return CaptureFramed(Path.Combine(dir, "lobby_5b_shop_spotlight.png"));
            var couch = new KeyboardBinding(KeyboardBinding.Side.Right);
            menu.Join(couch);
            menu.Open(LobbyMenu.Home);
            menu.ToggleParty();
            yield return new WaitForSeconds(1f);
            yield return CaptureFramed(Path.Combine(dir, $"lobby_{pages.Length + 1}_party.png"));
            menu.ToggleParty();
            menu.Leave(couch);
            menu.Open(LobbyMenu.Home);
            foreach (string map in GameConfig.Current.Houses.Keys)
            {
                menu.Choose(map: map);
                menu.Open(LobbyMenu.Home);
                yield return new WaitForSeconds(1f);
                yield return CaptureFramed(Path.Combine(dir, $"lobby_map_{map}.png"));
            }
            Session.Clear();
        }

        IEnumerator CaptureMapsSequence()
        {
            TryLights();
            string dir = Environment.GetEnvironmentVariable("WRECK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.dataPath, "../Temp/Captures");
            Directory.CreateDirectory(dir);
            foreach (string map in GameConfig.Current.Houses.Keys.ToList())
            {
                Session.Clear();
                Session.SelectMap(map);
                Match.ModeOverride = "Dibs";
                Session.Remember(new ScriptedBinding());
                Session.Remember(new ScriptedBinding());
                yield return SceneManager.LoadSceneAsync(Session.DibsScene);
                yield return new WaitForSeconds(1f);
                yield return CaptureFramed(Path.Combine(dir, $"map_{map}.png"));
                Session.Clear();
                Session.SelectMap(map);
                Match.ModeOverride = "Dibs";
                Session.Remember(DesktopBinding.Shared);
                yield return SceneManager.LoadSceneAsync(Session.DibsScene);
                yield return new WaitForSeconds(1.5f);
                yield return CaptureFramed(Path.Combine(dir, $"map_{map}_tps.png"));
                var hud = GameHud.Active;
                if (hud && hud.LocalPlayer)
                {
                    hud.LocalPlayer.Inventory.Set("BALLSOAP");
                    hud.transform.Find("Safe HUD/Letter bag/Bag link").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    yield return new WaitForSecondsRealtime(.3f);
                    yield return CaptureFramed(Path.Combine(dir, $"map_{map}_bag.png"));
                    hud.transform.Find("Safe HUD/Letter bag/Bag link").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                }
                if (GameConfig.Current.HouseFor(map).StoreyFloors().Count < 2) continue;
                Session.Clear();
                Session.SelectMap(map);
                Match.ModeOverride = "Dibs";
                Session.Remember(new ScriptedBinding());
                yield return SceneManager.LoadSceneAsync(Session.DibsScene);
                yield return new WaitForSeconds(1.5f);
                yield return CaptureFramed(Path.Combine(dir, $"map_{map}_solo.png"));
            }
            Session.Clear();
        }

        IEnumerator CaptureMatchCardsSequence()
        {
            string dir = Environment.GetEnvironmentVariable("WRECK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.dataPath, "../Temp/Captures");
            Directory.CreateDirectory(dir);
            Session.Clear();
            Match.ModeOverride = "Dibs";
            Session.Remember(DesktopBinding.Shared);
            yield return SceneManager.LoadSceneAsync(Session.DibsScene);
            yield return new WaitForSeconds(.6f);
            yield return CaptureFramed(Path.Combine(dir, "cards_1_countdown.png"));
            float until = Time.realtimeSinceStartup + 8f;
            while (RoundManager.Instance.Phase != Phase.Playing && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSeconds(.3f);
            yield return CaptureFramed(Path.Combine(dir, "cards_2_go_toast.png"));

            var hud = GameHud.Active;
            var player = hud.LocalPlayer;
            player.Inventory.Set("BATLESO");
            player.Summoner.Open();
            yield return null;
            hud.TypeWord("bat");
            yield return new WaitForSecondsRealtime(.2f);
            yield return CaptureFramed(Path.Combine(dir, "cards_3_composer.png"));
            player.Summoner.Close();
            yield return null;

            var safe = hud.transform.Find("Safe HUD");
            safe.Find("Brand").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.3f);
            yield return CaptureFramed(Path.Combine(dir, "cards_4_pause.png"));
            safe.Find("Pause/Pause card/Pause controls").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.3f);
            yield return CaptureFramed(Path.Combine(dir, "cards_5_help.png"));
            safe.Find("Pause/Pause help/Help done").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;

            hud.ShowResult(new HudResult { Round = 2, Won = true, Heading = "You called dibs!", Broken = 6, Crafted = 3, Damage = 48 }, () => { });
            yield return new WaitForSecondsRealtime(.5f);
            yield return CaptureFramed(Path.Combine(dir, "cards_6_round_result.png"));
            hud.ShowResult(new HudResult { Final = true, Won = true, Heading = "You called dibs!", Broken = 14, Crafted = 6, Damage = 120,
                Reward = new Wreckabulary.Rules.MatchRecord { Coins = 40, Score = 185, Xp = 60 }, Best = true }, () => { });
            yield return new WaitForSecondsRealtime(.5f);
            yield return CaptureFramed(Path.Combine(dir, "cards_7_final_result.png"));
            hud.HideResult();
            Session.Clear();
        }

        static void TryLights()
        {
            string asked = Environment.GetEnvironmentVariable("WRECK_LIGHT");
            if (string.IsNullOrEmpty(asked)) return;
            foreach (var pair in asked.Split(';'))
            {
                var kv = pair.Split('=');
                if (kv.Length != 2 || !float.TryParse(kv[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)) continue;
                switch (kv[0].Trim())
                {
                    case "sun": GraphicsOptions.Sun = v; break;
                    case "fill": GraphicsOptions.Fill = v; break;
                    case "hemi": GraphicsOptions.Hemisphere = v; break;
                    case "env": GraphicsOptions.Environment = v; break;
                    case "exp": GraphicsOptions.SetExposure(v); break;
                }
            }
            Debug.Log($"Capture lights: {asked}");
        }

        static Vector2Int Size()
        {
            string asked = Environment.GetEnvironmentVariable("WRECK_CAPTURE_SIZE");
            var parts = (asked ?? "").Split('x');
            return parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h) && w > 0 && h > 0
                ? new Vector2Int(w, h) : new Vector2Int(1600, 900);
        }

        const int UiLayer = 31;

        static IEnumerator CaptureFramed(string path)
        {
            var cam = Camera.main;
            var size = Size();
            var rt = new RenderTexture(size.x, size.y, 24);
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>()
                .Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            cam.targetTexture = rt;
            var layers = ToCamera(canvases, cam, Mathf.Max(.3f, cam.nearClipPlane + .05f));
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var shot = RenderWithUi(cam, rt);
            cam.targetTexture = null;
            ToOverlay(canvases, layers);
            Save(shot, rt, path);
        }

        static void Capture(string path)
        {
            var cam = Camera.main;
            var size = Size();
            var rt = new RenderTexture(size.x, size.y, 24);
            // Overlay canvases aren't drawn by cameras, so render the HUD through the camera for the capture.
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var layers = ToCamera(canvases, cam, 1f);
            Canvas.ForceUpdateCanvases();
            var shot = RenderWithUi(cam, rt);
            ToOverlay(canvases, layers);
            Save(shot, rt, path);
        }

        static (GameObject go, int layer)[] ToCamera(Canvas[] canvases, Camera cam, float planeDistance)
        {
            var layers = canvases.SelectMany(c => c.GetComponentsInChildren<Transform>(true)).Select(t => (t.gameObject, t.gameObject.layer)).ToArray();
            foreach (var (go, _) in layers) go.layer = UiLayer;
            foreach (var c in canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = planeDistance;
            }
            return layers;
        }

        static void ToOverlay(Canvas[] canvases, (GameObject go, int layer)[] layers)
        {
            foreach (var c in canvases)
                if (c) c.renderMode = RenderMode.ScreenSpaceOverlay;
            foreach (var (go, layer) in layers)
                if (go) go.layer = layer;
        }

        static Texture2D RenderWithUi(Camera cam, RenderTexture rt)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            int mask = cam.cullingMask;
            var clear = cam.clearFlags;
            var background = cam.backgroundColor;
            bool post = data.renderPostProcessing, hdr = cam.allowHDR;
            cam.cullingMask = mask & ~(1 << UiLayer);
            Submit(cam, rt);
            var world = Read(rt);
            cam.cullingMask = 1 << UiLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            data.renderPostProcessing = false;
            cam.allowHDR = false;
            cam.backgroundColor = Color.black;
            Submit(cam, rt);
            var black = Read(rt);
            cam.backgroundColor = Color.white;
            Submit(cam, rt);
            var white = Read(rt);
            cam.cullingMask = mask;
            cam.clearFlags = clear;
            cam.backgroundColor = background;
            data.renderPostProcessing = post;
            cam.allowHDR = hdr;

            var linear = new float[256];
            for (int i = 0; i < 256; i++) linear[i] = Mathf.GammaToLinearSpace(i / 255f);
            var pixels = world.GetPixels32();
            var overBlack = black.GetPixels32();
            var overWhite = white.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                var w = pixels[i];
                var b = overBlack[i];
                var o = overWhite[i];
                pixels[i] = new Color32(Over(linear, w.r, b.r, o.r), Over(linear, w.g, b.g, o.g), Over(linear, w.b, b.b, o.b), 255);
            }
            world.SetPixels32(pixels);
            world.Apply();
            UnityEngine.Object.Destroy(black);
            UnityEngine.Object.Destroy(white);
            return world;
        }

        static byte Over(float[] linear, byte world, byte overBlack, byte overWhite)
        {
            float ui = linear[overBlack];
            float through = Mathf.Clamp01(linear[overWhite] - ui);
            return (byte)Mathf.RoundToInt(Mathf.LinearToGammaSpace(Mathf.Clamp01(ui + linear[world] * through)) * 255f);
        }

        static Texture2D Read(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        static void Submit(Camera cam, RenderTexture rt)
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request))
            {
                RenderPipeline.SubmitRenderRequest(cam, request);
                return;
            }
            var target = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = target;
        }

        static void Save(Texture2D tex, RenderTexture rt, string path)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            rt.Release();
            Debug.Log("[Capture] " + path);
        }
    }
}
