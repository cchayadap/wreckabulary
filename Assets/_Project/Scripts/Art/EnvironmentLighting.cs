using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Wreckabulary.Art
{
    /// <summary>Scene-owned lighting settings and a bounded, shared pool of nearby practical lights.</summary>
    [DefaultExecutionOrder(-220), DisallowMultipleComponent]
    public sealed class EnvironmentLighting : MonoBehaviour
    {
        public static EnvironmentLighting Active { get; private set; }
        [SerializeField] EnvironmentLightingProfile profile;
        [SerializeField] Light sun;
        [SerializeField] Light fill;
        readonly List<(PracticalLight fixture, float distance)> candidates = new();
        Camera view;
        Material previousSky;
        float nextSelection;
        bool thirdPersonFog;

        public EnvironmentLightingProfile Profile => profile;
        public Light Sun => sun;
        public Light Fill => fill;
        public int SelectedLightCount { get; private set; }
        public int LightBudget => !profile ? 0 : GraphicsOptions.Preset switch
        {
            "Low" => 0,
            "Medium" => Mathf.Min(2, profile.HighLightBudget),
            "Ultra" => Mathf.Min(8, profile.HighLightBudget + 2),
            _ => profile.HighLightBudget
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetActive() => Active = null;

        public void Configure(EnvironmentLightingProfile settings, Light key, Light bounce)
        {
            profile = settings;
            sun = key;
            fill = bounce;
            if (Application.isPlaying && isActiveAndEnabled) ApplyRuntime();
        }

        void OnEnable()
        {
            if (!Application.isPlaying || !profile) return;
            previousSky = RenderSettings.skybox;
            Active = this;
            view = Camera.main;
            ApplyRuntime();
        }

        void OnDisable()
        {
            if (Active != this) return;
            foreach (var fixture in PracticalLight.Registered) if (fixture) fixture.SetSelected(false);
            SelectedLightCount = 0;
            Active = null;
            RenderSettings.skybox = previousSky;
            if (Application.isPlaying) GraphicsOptions.ApplyEnvironmentGrade();
        }

        public void ApplyRuntime()
        {
            if (!profile) return;
            if (Application.isPlaying)
            {
                if (Active != this) previousSky = RenderSettings.skybox;
                Active = this;
            }
            ApplySceneSettings();
            if (Application.isPlaying) GraphicsOptions.ApplyLights();
            if (!view) view = Camera.main;
            if (view && (!view.TryGetComponent<CameraRig>(out var rig) || rig.enabled)) ApplyCamera(view);
            RefreshPracticalLights();
        }

        /// <summary>Editor tools call this explicitly; merely opening a prefab never repaints the scene.</summary>
        public void ApplySceneSettings()
        {
            if (!profile) return;
            if (sun)
            {
                sun.color = profile.SunColor;
                sun.intensity = profile.SunIntensity;
                sun.transform.rotation = Quaternion.Euler(profile.SunEuler);
                sun.shadows = LightShadows.Soft;
                RenderSettings.sun = sun;
            }
            if (fill)
            {
                fill.color = profile.FillColor;
                fill.intensity = profile.FillIntensity;
                fill.transform.rotation = Quaternion.Euler(profile.FillEuler);
                fill.shadows = LightShadows.None;
            }
            RenderSettings.skybox = profile.Skybox;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = profile.AmbientSky;
            RenderSettings.ambientEquatorColor = profile.AmbientEquator;
            RenderSettings.ambientGroundColor = profile.AmbientGround;
            ApplyFog(Application.isPlaying ? thirdPersonFog : true);
        }

        public void ApplyCamera(Camera camera)
        {
            if (!camera || !profile) return;
            camera.clearFlags = profile.Skybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            camera.backgroundColor = profile.HorizonColor;
        }

        public void ApplyFog(bool thirdPerson)
        {
            if (!profile) return;
            thirdPersonFog = thirdPerson;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = profile.HorizonColor;
            RenderSettings.fogStartDistance = profile.FogStart + (thirdPerson ? 0f : 16f);
            RenderSettings.fogEndDistance = Mathf.Max(RenderSettings.fogStartDistance + 1f, profile.FogEnd);
        }

        void Update()
        {
            if (!profile || Active != this || Time.unscaledTime < nextSelection) return;
            nextSelection = Time.unscaledTime + profile.SelectionInterval;
            RefreshPracticalLights();
        }

        public void RefreshPracticalLights()
        {
            candidates.Clear();
            foreach (var fixture in PracticalLight.Registered)
            {
                if (!fixture || !fixture.isActiveAndEnabled || fixture.gameObject.scene != gameObject.scene) continue;
                fixture.SetSelected(false);
                float nearest = float.PositiveInfinity;
                foreach (var player in World.Players)
                {
                    if (!player || player.IsEliminated || player.Binding is BotBinding) continue;
                    nearest = Mathf.Min(nearest, (player.transform.position + Vector3.up - fixture.transform.position).sqrMagnitude);
                }
                if (float.IsPositiveInfinity(nearest) && view) nearest = (view.transform.position - fixture.transform.position).sqrMagnitude;
                float range = fixture.Lamp ? fixture.Lamp.range : 0f;
                if (nearest <= range * range * 2.25f) candidates.Add((fixture, nearest));
            }
            candidates.Sort((a, b) => a.distance.CompareTo(b.distance));
            SelectedLightCount = Mathf.Min(LightBudget, candidates.Count);
            for (int i = 0; i < SelectedLightCount; i++) candidates[i].fixture.SetSelected(true);
        }

        void OnValidate()
        {
            if (Application.isPlaying && Active == this) ApplyRuntime();
        }
    }
}
