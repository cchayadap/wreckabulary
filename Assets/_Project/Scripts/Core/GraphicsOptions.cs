using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Wreckabulary.Art;

namespace Wreckabulary
{
    public static class GraphicsOptions
    {
        public static float PostExposure = .85f;
        public const int LookPriority = 5;

        public static Color SunColour = new(1f, .918f, .820f), FillColour = new(.843f, .922f, .949f);
        public static Color SkyColour = new(1f, .945f, .863f), GroundColour = new(.275f, .369f, .376f);
        public static float Sun = 2.85f / Mathf.PI, Fill = 1.05f / Mathf.PI, Hemisphere = 1.65f / Mathf.PI, Environment = .25f;
        public static Vector3 SunFrom = new(-8f, 19f, -8f), FillFrom = new(8f, 6f, 9f);

        public const string ScaleKey = "wv.gfx.scale", SmoothingKey = "wv.gfx.aa", ShadowsKey = "wv.gfx.shadows", CapKey = "wv.gfx.cap";
        public static readonly string[] Keys = { ScaleKey, SmoothingKey, ShadowsKey, CapKey };
        public static readonly string[] Presets = { "Low", "Medium", "High", "Ultra" };
        public const string DefaultPreset = "High", Custom = "Custom";
        public static readonly string[] Smoothings = { "Off", "FXAA", "SMAA", "MSAA2", "MSAA4", "MSAA8" };
        public static readonly string[] ShadowLevels = { "Off", "Low", "Medium", "High", "Ultra" };
        public static readonly int[] FrameCaps = { 30, 60, 120, 144, 0 };
        public const float MinScale = .5f, MaxScale = 2f, ScaleStep = .25f;

        public static float RenderScale { get; private set; } = 1f;
        public static string Smoothing { get; private set; } = "MSAA4";
        public static string Shadows { get; private set; } = "High";
        public static int FrameCap { get; private set; }

        static RenderPipelineAsset shipped;
        static UniversalRenderPipelineAsset copy;
        static bool swapped;
        static Volume look;
        static Light fill;

        public static Volume Look => look;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= OnScene;
            SceneManager.sceneLoaded += OnScene;
            Application.quitting -= Restore;
            Application.quitting += Restore;
            Load();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnPlay() => OnScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);

        static void OnScene(Scene scene, LoadSceneMode mode)
        {
            if (Camera.main) ApplyTo(Camera.main);
            ApplyLights();
        }

        public static void ApplyLights()
        {
            var environment = EnvironmentLighting.Active;
            if (environment && environment.Profile)
            {
                environment.ApplySceneSettings();
                if (fill) fill.enabled = false;
                ApplyEnvironmentGrade();
                return;
            }
            Light sun = null;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (light.type == LightType.Directional && light != fill && (!sun || light.name == "Sun")) sun = light;
            if (sun)
            {
                sun.color = SunColour; sun.intensity = Sun;
                sun.transform.rotation = Quaternion.LookRotation(-SunFrom.normalized);
            }
            if (!fill)
            {
                fill = new GameObject("Fill light").AddComponent<Light>();
                Object.DontDestroyOnLoad(fill.gameObject);
                fill.type = LightType.Directional; fill.shadows = LightShadows.None;
            }
            fill.color = FillColour; fill.intensity = Fill;
            fill.enabled = true;
            fill.transform.rotation = Quaternion.LookRotation(-FillFrom.normalized);
            Color sky = SkyColour.linear * Hemisphere, ground = GroundColour.linear * Hemisphere, even = Color.white * Environment;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Opaque(sky + even).gamma;
            RenderSettings.ambientEquatorColor = Opaque((sky + ground) * .5f + even).gamma;
            RenderSettings.ambientGroundColor = Opaque(ground + even).gamma;
            ApplyEnvironmentGrade();
        }

        static Color Opaque(Color c) => new(c.r, c.g, c.b, 1f);

        public static void SetExposure(float stops)
        {
            PostExposure = stops;
            if (look && look.sharedProfile.TryGet<ColorAdjustments>(out var adjust)) adjust.postExposure.Override(stops);
        }

        public static void ApplyTo(Camera camera)
        {
            if (!camera) return;
            camera.allowMSAA = true;
            camera.allowHDR = true;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            Smooth(camera, data);
            data.dithering = true;
            EnsureLook();
        }

        public static string Preset => Presets.FirstOrDefault(Matches) ?? Custom;

        static bool Matches(string preset)
        {
            var (scale, smoothing, shadows) = PresetOf(preset);
            return Mathf.Approximately(scale, RenderScale) && smoothing == Smoothing && shadows == Shadows;
        }

        public static (float scale, string smoothing, string shadows) PresetOf(string preset) => preset switch
        {
            "Low" => (.75f, "FXAA", "Low"),
            "Medium" => (1f, "SMAA", "Medium"),
            "Ultra" => (1.5f, "MSAA4", "Ultra"),
            _ => (1f, "MSAA4", "High"),
        };

        static (int resolution, float distance, int cascades) ShadowSetup(string level) => level switch
        {
            "Low" => (1024, 30f, 1),
            "Medium" => (2048, 40f, 2),
            "Ultra" => (4096, 60f, 4),
            _ => (2048, 50f, 4),
        };

        static int Samples(string smoothing) => smoothing switch { "MSAA2" => 2, "MSAA4" => 4, "MSAA8" => 8, _ => 1 };

        public static bool Customised => Keys.Any(PlayerPrefs.HasKey);

        public static UniversalRenderPipelineAsset Shipped
        {
            get
            {
                var asset = swapped ? shipped : QualitySettings.renderPipeline;
                return (asset ? asset : GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            }
        }

        public static void UsePreset(string preset)
        {
            (RenderScale, Smoothing, Shadows) = PresetOf(preset);
            Save();
        }

        public static void SetRenderScale(float scale)
        {
            RenderScale = Mathf.Clamp(Mathf.Round(scale / ScaleStep) * ScaleStep, MinScale, MaxScale);
            Save();
        }

        public static void SetSmoothing(string smoothing)
        {
            if (!Smoothings.Contains(smoothing)) return;
            Smoothing = smoothing;
            Save();
        }

        public static void SetShadows(string level)
        {
            if (!ShadowLevels.Contains(level)) return;
            Shadows = level;
            Save();
        }

        public static void SetFrameCap(int cap)
        {
            if (!FrameCaps.Contains(cap)) return;
            FrameCap = cap;
            Save();
        }

        public static void ResetToDefaults()
        {
            foreach (var key in Keys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            Load();
        }

        public static void Load()
        {
            var (scale, smoothing, shadows) = PresetOf(DefaultPreset);
            RenderScale = float.TryParse(PlayerPrefs.GetString(ScaleKey, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out float saved)
                ? Mathf.Clamp(saved, MinScale, MaxScale) : scale;
            string aa = PlayerPrefs.GetString(SmoothingKey, "");
            Smoothing = Smoothings.Contains(aa) ? aa : smoothing;
            string level = PlayerPrefs.GetString(ShadowsKey, "");
            Shadows = ShadowLevels.Contains(level) ? level : shadows;
            FrameCap = int.TryParse(PlayerPrefs.GetString(CapKey, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cap) &&
                       FrameCaps.Contains(cap) ? cap : 0;
            Apply();
        }

        static void Save()
        {
            PlayerPrefs.SetString(ScaleKey, RenderScale.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.SetString(SmoothingKey, Smoothing);
            PlayerPrefs.SetString(ShadowsKey, Shadows);
            PlayerPrefs.SetString(CapKey, FrameCap.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
            Apply();
        }

        public static void Apply()
        {
            ApplyPipeline();
            Application.targetFrameRate = FrameCap > 0 ? FrameCap : -1;
            foreach (var camera in Camera.allCameras)
                if (camera.TryGetComponent(out UniversalAdditionalCameraData data) && data.renderPostProcessing) Smooth(camera, data);
            if (EnvironmentLighting.Active) EnvironmentLighting.Active.RefreshPracticalLights();
        }

        static void ApplyPipeline()
        {
            var original = Shipped;
            if (!original) return;
            var (resolution, distance, cascades) = ShadowSetup(Shadows);
            int samples = Samples(Smoothing);
            bool asShipped = Mathf.Approximately(original.renderScale, RenderScale) && original.msaaSampleCount == samples &&
                (Shadows == "Off" || (original.mainLightShadowmapResolution == resolution &&
                                      Mathf.Approximately(original.shadowDistance, distance) && original.shadowCascadeCount == cascades));
            if (!Customised || asShipped) { Restore(); return; }
            if (!copy || copy.name != original.name + " (player)")
            {
                copy = Object.Instantiate(original);
                copy.name = original.name + " (player)";
            }
            copy.renderScale = RenderScale;
            copy.msaaSampleCount = samples;
            if (Shadows != "Off")
            {
                copy.mainLightShadowmapResolution = resolution;
                copy.shadowDistance = distance;
                copy.shadowCascadeCount = cascades;
            }
            if (!swapped) { shipped = QualitySettings.renderPipeline; swapped = true; }
            QualitySettings.renderPipeline = copy;
        }

        public static void Restore()
        {
            if (!swapped) return;
            QualitySettings.renderPipeline = shipped;
            swapped = false;
        }

        static void Smooth(Camera camera, UniversalAdditionalCameraData data)
        {
            camera.allowMSAA = Samples(Smoothing) > 1;
            data.antialiasing = Smoothing == "FXAA" ? AntialiasingMode.FastApproximateAntialiasing :
                Smoothing == "SMAA" ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = Shadows != "Off";
        }

        public static Volume EnsureLook()
        {
            if (look) return look;
            var go = new GameObject("Look");
            Object.DontDestroyOnLoad(go);
            look = go.AddComponent<Volume>();
            look.isGlobal = true;
            look.priority = LookPriority;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Look";
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            profile.Add<ColorAdjustments>(true).postExposure.Override(PostExposure);
            look.sharedProfile = profile;
            ApplyEnvironmentGrade();
            return look;
        }

        internal static void ApplyEnvironmentGrade()
        {
            if (!look || !look.sharedProfile) return;
            var lighting = EnvironmentLighting.Active ? EnvironmentLighting.Active.Profile : null;
            if (look.sharedProfile.TryGet<ColorAdjustments>(out var grade))
            {
                grade.postExposure.Override(lighting ? lighting.Exposure : PostExposure);
                grade.contrast.Override(lighting ? lighting.Contrast : 0f);
                grade.saturation.Override(lighting ? lighting.Saturation : 0f);
            }
            if (!look.sharedProfile.TryGet<Bloom>(out var bloom)) bloom = look.sharedProfile.Add<Bloom>();
            bloom.active = lighting && lighting.Bloom > 0f;
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(lighting ? lighting.Bloom : 0f);
        }
    }
}
