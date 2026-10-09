using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Wreckabulary.Art;

namespace Wreckabulary.Tests
{
    public sealed class EnvironmentLightingTests
    {
        readonly Dictionary<string, string> saved = new();
        EnvironmentLightingProfile profile;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saved.Clear();
            foreach (string key in GraphicsOptions.Keys) saved[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
            yield return TestScenes.Reset();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.Reset();
            if (profile) Object.Destroy(profile);
            foreach (var item in saved)
            {
                if (item.Value == null) PlayerPrefs.DeleteKey(item.Key);
                else PlayerPrefs.SetString(item.Key, item.Value);
            }
            PlayerPrefs.Save();
            GraphicsOptions.Load();
        }

        EnvironmentLighting CreateEnvironment()
        {
            var camera = new GameObject("Fixture camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = Vector3.up;
            GraphicsOptions.ApplyTo(camera);
            profile = ScriptableObject.CreateInstance<EnvironmentLightingProfile>();
            var root = new GameObject("Fixture environment");
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(root.transform);
            sun.type = LightType.Directional;
            var fill = new GameObject("House fill").AddComponent<Light>();
            fill.transform.SetParent(root.transform);
            fill.type = LightType.Directional;
            var environment = root.AddComponent<EnvironmentLighting>();
            environment.Configure(profile, sun, fill);
            return environment;
        }

        [UnityTest]
        public IEnumerator PracticalLightsRespectDistanceBudgetAndQualityWithoutHidingBulbs()
        {
            GraphicsOptions.UsePreset("High");
            var environment = CreateEnvironment();
            var fixtures = new List<PracticalLight>();
            for (int i = 0; i < 9; i++)
            {
                var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.transform.position = new Vector3(i * .4f, 1.6f, 0f);
                var fixture = bulb.AddComponent<PracticalLight>();
                fixture.Configure(Color.yellow, 1f, 5f);
                fixtures.Add(fixture);
            }
            environment.RefreshPracticalLights();
            Assert.AreEqual(4, fixtures.Count(fixture => fixture.Lamp.enabled));
            Assert.IsTrue(fixtures.Take(4).All(fixture => fixture.Lamp.enabled), "nearest lamps get the budget");
            Assert.IsTrue(fixtures.All(fixture => fixture.Lamp.shadows == LightShadows.None));
            GraphicsOptions.UsePreset("Low");
            Assert.AreEqual(0, fixtures.Count(fixture => fixture.Lamp.enabled));
            Assert.IsTrue(fixtures.All(fixture => fixture.GetComponent<Renderer>().enabled), "visible bulbs survive Low quality");
            GraphicsOptions.UsePreset("High");
            fixtures[0].gameObject.SetActive(false);
            environment.RefreshPracticalLights();
            Assert.AreEqual(4, fixtures.Count(fixture => fixture.Lamp.enabled));
            Assert.IsFalse(fixtures[0].Lamp.enabled);
            environment.enabled = false;
            Assert.AreEqual(0, fixtures.Count(fixture => fixture.Lamp.enabled));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProfilesDriveSunFogAndGradeWithoutMutatingFallbackSettings()
        {
            float fallbackExposure = GraphicsOptions.PostExposure;
            var environment = CreateEnvironment();
            profile.Exposure = -.2f;
            profile.SunIntensity = 1.3f;
            environment.ApplyRuntime();
            Assert.AreEqual(profile.SunIntensity, environment.Sun.intensity);
            Assert.AreSame(environment.Sun, RenderSettings.sun);
            Assert.AreEqual(profile.HorizonColor, RenderSettings.fogColor);
            Assert.AreEqual(profile.FogEnd, RenderSettings.fogEndDistance);
            environment.ApplyFog(false);
            float overheadStart = RenderSettings.fogStartDistance;
            GraphicsOptions.ApplyLights();
            Assert.AreEqual(overheadStart, RenderSettings.fogStartDistance, "a late lighting refresh preserves overview fog");
            environment.ApplyFog(true);
            Assert.Less(RenderSettings.fogStartDistance, overheadStart);
            GraphicsOptions.ApplyLights();
            Assert.AreEqual(profile.FogStart, RenderSettings.fogStartDistance);
            Assert.IsTrue(GraphicsOptions.Look.sharedProfile.TryGet<ColorAdjustments>(out var grade));
            Assert.AreEqual(-.2f, grade.postExposure.value);
            Assert.AreEqual(fallbackExposure, GraphicsOptions.PostExposure);
            environment.enabled = false;
            Assert.IsNull(EnvironmentLighting.Active);
            Assert.AreEqual(fallbackExposure, grade.postExposure.value);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LobbyReleasesItsSkyBackToTheAuthoredDaylight()
        {
            yield return TestScenes.Load(Session.HubScene);
            var environment = EnvironmentLighting.Active;
            Assert.IsNotNull(environment);
            Assert.AreEqual(CameraClearFlags.SolidColor, Camera.main.clearFlags, "lobby stage keeps its own background");
            yield return TestScenes.ExploreHouse();
            Assert.AreEqual(CameraClearFlags.Skybox, Camera.main.clearFlags);
            Assert.AreSame(environment.Profile.Skybox, RenderSettings.skybox);
        }
    }
}
