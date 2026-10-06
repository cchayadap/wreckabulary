using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wreckabulary.Art
{
    [AddComponentMenu("")]
    public sealed class TactileMaterials : MonoBehaviour
    {
        const int Resolution = 64;
        const int CacheLimit = 32;
        const string LitShader = "Universal Render Pipeline/Lit";
        enum Surface { Wood, Ceramic, Cloth }

        sealed class Detail
        {
            public Texture2D Albedo, Normal;
            public float NormalScale, SmoothnessVariation;
            public Vector2 Tiling;
        }

        static readonly Dictionary<Material, Material> variants = new Dictionary<Material, Material>();
        static readonly HashSet<Material> generatedMaterials = new HashSet<Material>();
        static readonly Dictionary<Material, Material> originals = new Dictionary<Material, Material>();
        static readonly HashSet<TactileMaterials> scopes = new HashSet<TactileMaterials>();
        static readonly Dictionary<Surface, Detail> details = new Dictionary<Surface, Detail>();
        static readonly List<Texture2D> textures = new List<Texture2D>();
        bool registered;

        public static int SharedMaterialCount => variants.Count;
        public static int GeneratedTextureCount => textures.Count;
        public static int RegisteredScopeCount { get { PruneDeadScopes(); return scopes.Count; } }
        public static int EstimatedTextureBytes => textures.Count * 21844;

        public static bool IsEligible(Material material) =>
            material && material.shader && material.shader.name == LitShader && TrySurface(material.name, out _);

        public static void Apply(GameObject root)
        {
            if (!root) return;
            var scope = root.GetComponent<TactileMaterials>();
            if (!scope) scope = root.AddComponent<TactileMaterials>();
            scope.hideFlags = HideFlags.HideInInspector;
            scope.Register();
            scope.RefreshMaterials();
        }

        public static void Refresh(GameObject root)
        {
            if (!root) return;
            foreach (var scope in root.GetComponentsInChildren<TactileMaterials>(true))
                if (scope.registered) scope.RefreshMaterials();
            CollectUnused();
        }

        public static void Release(GameObject root)
        {
            if (!root) return;
            foreach (var scope in root.GetComponentsInChildren<TactileMaterials>(true))
            {
                foreach (var renderer in scope.GetComponentsInChildren<Renderer>(true))
                {
                    var shared = renderer.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < shared.Length; i++)
                        if (shared[i] && originals.TryGetValue(shared[i], out var original))
                        { shared[i] = original; changed = true; }
                    if (changed) renderer.sharedMaterials = shared;
                }
                scopes.Remove(scope);
                scope.registered = false;
            }
            CollectUnused();
        }

        public static void CollectUnused()
        {
            PruneDeadScopes();
            if (scopes.Count == 0) ClearCache();
        }

        static void PruneDeadScopes() => scopes.RemoveWhere(scope => !scope);

        static void OnSceneUnloaded(Scene scene) => CollectUnused();

        void OnDestroy()
        {
            scopes.Remove(this);
            registered = false;
            CollectUnused();
        }

        void Register()
        {
            PruneDeadScopes();
            if (registered && scopes.Contains(this)) return;
            registered = true;
            scopes.Add(this);
        }

        void RefreshMaterials()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is SkinnedMeshRenderer) continue;
                var shared = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < shared.Length; i++)
                {
                    var next = Variant(shared[i]);
                    if (next == shared[i]) continue;
                    shared[i] = next;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = shared;
            }
        }

        static Material Variant(Material source)
        {
            if (!IsEligible(source) || generatedMaterials.Contains(source)) return source;
            if (variants.TryGetValue(source, out var existing)) return existing;
            if (variants.Count >= CacheLimit) return source;
            if (source.IsKeywordEnabled("_SPECULAR_SETUP") || source.IsKeywordEnabled("_DETAIL_MULX2") ||
                source.IsKeywordEnabled("_DETAIL_SCALED")) return source;
            foreach (string property in new[] { "_DetailAlbedoMap", "_DetailNormalMap", "_DetailMask" })
                if (!source.HasProperty(property) || source.GetTexture(property)) return source;
            if (!source.HasProperty("_MetallicGlossMap") || !source.HasProperty("_Smoothness")) return source;
            if (!TrySurface(source.name, out var surface)) return source;
            var detail = Details(surface);
            var material = new Material(source) { name = source.name, hideFlags = HideFlags.DontSave };
            material.SetTexture("_DetailAlbedoMap", detail.Albedo);
            material.SetTexture("_DetailNormalMap", detail.Normal);
            material.SetTextureScale("_DetailAlbedoMap", detail.Tiling);
            material.SetTextureScale("_DetailNormalMap", detail.Tiling);
            material.SetFloat("_DetailAlbedoMapScale", 1f);
            material.SetFloat("_DetailNormalMapScale", detail.NormalScale);
            material.DisableKeyword("_DETAIL_SCALED");
            material.EnableKeyword("_DETAIL_MULX2");
            if (!source.GetTexture("_MetallicGlossMap") &&
                !source.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"))
            {
                float average = Mathf.Clamp01(source.GetFloat("_Smoothness"));
                float amplitude = Mathf.Min(detail.SmoothnessVariation, average, 1f - average);
                float maximum = average + amplitude;
                float metallic = source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0f;
                var pixels = new Color[Resolution * Resolution];
                for (int y = 0; y < Resolution; y++)
                    for (int x = 0; x < Resolution; x++)
                    {
                        float u = x / (float)Resolution, v = y / (float)Resolution;
                        float variation = Mathf.Sin(2f * Mathf.PI * u) * Mathf.Cos(4f * Mathf.PI * v);
                        float alpha = maximum > 0f ? (average + amplitude * variation) / maximum : 1f;
                        pixels[y * Resolution + x] = new Color(metallic, 0f, 0f, alpha);
                    }
                material.SetTexture("_MetallicGlossMap", Texture(source.name + " tactile finish", pixels));
                material.SetFloat("_Smoothness", maximum);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            variants[source] = material;
            generatedMaterials.Add(material);
            originals[material] = source;
            return material;
        }

        static bool TrySurface(string name, out Surface surface)
        {
            surface = default;
            int split = name.LastIndexOf('_');
            if (split < 0) return false;
            string skin = name.Substring(split + 1);
            if (skin != "Classic" && skin != "Candy" && skin != "Arcade") return false;
            switch (name.Substring(0, split))
            {
                case "wood": case "wood_dark": case "wood_light": surface = Surface.Wood; return true;
                case "ceramic": surface = Surface.Ceramic; return true;
                case "cream": surface = Surface.Cloth; return true;
                default: return false;
            }
        }

        static Detail Details(Surface surface)
        {
            if (details.TryGetValue(surface, out var existing)) return existing;
            var albedo = new Color[Resolution * Resolution];
            var normal = new Color[albedo.Length];
            for (int y = 0; y < Resolution; y++)
                for (int x = 0; x < Resolution; x++)
                {
                    float u = x / (float)Resolution * 2f * Mathf.PI;
                    float v = y / (float)Resolution * 2f * Mathf.PI;
                    float signal, nx, ny, colourAmplitude;
                    if (surface == Surface.Wood)
                    {
                        float grain = 9f * u + .35f * Mathf.Sin(v);
                        signal = Mathf.Sin(grain) * .7f + Mathf.Sin(2f * u) * Mathf.Cos(v) * .3f;
                        nx = .25f * Mathf.Cos(grain); ny = .025f * Mathf.Sin(v);
                        colourAmplitude = .008f;
                    }
                    else if (surface == Surface.Cloth)
                    {
                        signal = Mathf.Sin(8f * u) * Mathf.Sin(8f * v);
                        nx = .16f * Mathf.Cos(8f * u) * Mathf.Sin(8f * v);
                        ny = .16f * Mathf.Sin(8f * u) * Mathf.Cos(8f * v);
                        colourAmplitude = .005f;
                    }
                    else
                    {
                        signal = Mathf.Sin(3f * u) * Mathf.Cos(5f * v);
                        nx = .10f * Mathf.Cos(3f * u) * Mathf.Cos(5f * v);
                        ny = .10f * Mathf.Sin(3f * u) * Mathf.Sin(5f * v);
                        colourAmplitude = .0025f;
                    }
                    float shade = .5f + signal * colourAmplitude;
                    albedo[y * Resolution + x] = new Color(shade, shade, shade, 1f);
                    var direction = new Vector3(nx, ny, 1f).normalized;
                    normal[y * Resolution + x] = new Color(direction.x * .5f + .5f,
                        direction.y * .5f + .5f, direction.z * .5f + .5f, 1f);
                }
            var detail = new Detail
            {
                Albedo = Texture(surface + " tactile albedo", albedo),
                Normal = Texture(surface + " tactile normal", normal),
                NormalScale = surface == Surface.Wood ? .18f : surface == Surface.Cloth ? .12f : .03f,
                SmoothnessVariation = surface == Surface.Wood ? .035f : surface == Surface.Cloth ? .015f : .018f,
                Tiling = surface == Surface.Ceramic ? Vector2.one : Vector2.one * 2f
            };
            details[surface] = detail;
            return detail;
        }

        static Texture2D Texture(string name, Color[] pixels)
        {
            var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, true, true)
            {
                name = name, wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear, anisoLevel = 1, hideFlags = HideFlags.DontSave
            };
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            textures.Add(texture);
            return texture;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            ClearCache();
            scopes.Clear();
            Application.quitting -= ClearCache;
            Application.quitting += ClearCache;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        static void ClearCache()
        {
            foreach (var material in generatedMaterials) Dispose(material);
            foreach (var texture in textures) Dispose(texture);
            variants.Clear(); generatedMaterials.Clear(); originals.Clear(); details.Clear(); textures.Clear();
        }

        static void Dispose(UnityEngine.Object value)
        {
            if (!value) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

    }
}
