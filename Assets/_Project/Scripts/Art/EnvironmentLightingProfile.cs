using UnityEngine;

namespace Wreckabulary.Art
{
    [CreateAssetMenu(menuName = "Wreckabulary/Environment Lighting", fileName = "SunlitHouse")]
    public sealed class EnvironmentLightingProfile : ScriptableObject
    {
        [Header("Daylight")]
        public Material Skybox;
        public Color SunColor = new(1f, .91f, .76f);
        [Min(0f)] public float SunIntensity = 1.1f;
        public Vector3 SunEuler = new(48f, -32f, 0f);
        public Color FillColor = new(.70f, .83f, 1f);
        [Min(0f)] public float FillIntensity = .22f;
        public Vector3 FillEuler = new(35f, 145f, 0f);
        public Color AmbientSky = new(.67f, .76f, .88f);
        public Color AmbientEquator = new(.53f, .53f, .51f);
        public Color AmbientGround = new(.33f, .29f, .25f);

        [Header("Camera atmosphere")]
        public Color HorizonColor = new(.72f, .84f, .89f);
        [Min(0f)] public float FogStart = 52f;
        [Min(1f)] public float FogEnd = 160f;
        [Range(-2f, 2f)] public float Exposure = .15f;
        [Range(-30f, 30f)] public float Contrast = 8f;
        [Range(-30f, 30f)] public float Saturation = 10f;
        [Range(0f, 1f)] public float Bloom = .12f;

        [Header("Practical lights")]
        [Range(0, 8)] public int HighLightBudget = 4;
        [Range(.1f, 1f)] public float SelectionInterval = .25f;
    }
}
