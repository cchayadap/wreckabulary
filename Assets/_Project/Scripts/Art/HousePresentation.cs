using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Wreckabulary.Art
{
    /// <summary>Paint, surface detail and daylight only; authored room transforms and colliders stay intact.</summary>
    public sealed class HousePresentation : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        readonly List<Mesh> generatedMeshes = new();
        [SerializeField] bool authored;
        public bool IsAuthored => authored;

        public static Color FloorColor(int room) => (room % 5) switch
        {
            0 => new Color(.86f, .67f, .43f),
            1 => new Color(.49f, .72f, .69f),
            2 => new Color(.92f, .75f, .50f),
            3 => new Color(.66f, .59f, .79f),
            _ => new Color(.85f, .51f, .39f)
        };

        public static void Apply(GameObject root, bool hub = false)
        {
            if (!root || root.GetComponent<HousePresentation>()) return;
            var look = root.AddComponent<HousePresentation>();
            look.Paint(hub);
            ApplyDefaultLighting();
        }

        public static void ApplyDefaultLighting()
        {
            var authoredLighting = Object.FindAnyObjectByType<EnvironmentLighting>();
            if (authoredLighting && authoredLighting.Profile)
            {
                if (Application.isPlaying) authoredLighting.ApplyRuntime();
                else authoredLighting.ApplySceneSettings();
                return;
            }
            if (Application.isPlaying)
            {
                GraphicsOptions.ApplyLights();
                return;
            }
            Color sky = GraphicsOptions.SkyColour.linear * GraphicsOptions.Hemisphere;
            Color ground = GraphicsOptions.GroundColour.linear * GraphicsOptions.Hemisphere;
            Color even = Color.white * GraphicsOptions.Environment;
            Color Opaque(Color color) => new Color(color.r, color.g, color.b, 1f).gamma;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Opaque(sky + even);
            RenderSettings.ambientEquatorColor = Opaque((sky + ground) * .5f + even);
            RenderSettings.ambientGroundColor = Opaque(ground + even);
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var light in lights)
            {
                if (light.type != LightType.Directional || light.gameObject.scene != SceneManager.GetActiveScene()) continue;
                bool fill = light.name == "Fill light";
                light.color = fill ? GraphicsOptions.FillColour : GraphicsOptions.SunColour;
                light.intensity = fill ? GraphicsOptions.Fill : GraphicsOptions.Sun;
                light.transform.rotation = Quaternion.LookRotation(-(fill ? GraphicsOptions.FillFrom : GraphicsOptions.SunFrom).normalized);
            }
            var camera = Camera.main;
            if (camera && camera.gameObject.scene == SceneManager.GetActiveScene()) camera.backgroundColor = new Color(.10f, .23f, .24f);
        }

        /// <summary>Call after the editor has replaced generated mesh references with persistent asset copies.</summary>
        public void MarkAuthored()
        {
            authored = true;
            ReleaseGeneratedMeshes();
        }

        void Paint(bool hub)
        {
            var properties = new MaterialPropertyBlock();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                string name = renderer.gameObject.name;
                if (name == "Floor" || name.EndsWith(" floor"))
                {
                    if (name == "Garden floor") continue;
                    Color color = renderer.sharedMaterial ? renderer.sharedMaterial.color : FloorColor(0);
                    if (hub) color = FloorColor(0);
                    Paint(renderer, properties, color, .22f);
                    FloorSeams(renderer, color, name.Contains("Kitchen"));
                }
                else if (name.StartsWith("Wall") || name.StartsWith("Half Wall"))
                {
                    Paint(renderer, properties, name.Contains("Back") ? new Color(.96f, .86f, .69f) : new Color(.62f, .76f, .66f), .12f);
                }
                else if (name == "Rug") Paint(renderer, properties, new Color(.18f, .46f, .43f), .05f);
                else if (name.StartsWith("Baseboard") || name == "Window Frame") Paint(renderer, properties, new Color(.64f, .29f, .19f), .3f);
                else if (name == "Window") Paint(renderer, properties, new Color(.42f, .81f, .89f), .65f);
            }
        }

        static void Paint(Renderer renderer, MaterialPropertyBlock properties, Color color, float smoothness)
        {
            renderer.GetPropertyBlock(properties);
            properties.SetColor(BaseColor, color);
            properties.SetColor("_Color", color);
            properties.SetFloat(Smoothness, smoothness);
            renderer.SetPropertyBlock(properties);
            properties.Clear();
        }

        void FloorSeams(Renderer floor, Color color, bool tile)
        {
            var bounds = floor.bounds;
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            float width = bounds.size.x, depth = bounds.size.z;
            for (float z = -depth * .5f + 1f; z < depth * .5f - .05f; z += 1f)
                Strip(vertices, indices, new Vector2(-width * .5f, z - .009f), new Vector2(width * .5f, z + .009f));
            float step = tile ? 1f : 2.5f;
            for (float z = -depth * .5f; z < depth * .5f - .05f; z += 1f)
            {
                float stagger = tile ? 0f : Mathf.Repeat(z + depth * .5f, 2f) * 1.25f;
                for (float x = -width * .5f + step + stagger; x < width * .5f - .05f; x += step)
                    Strip(vertices, indices, new Vector2(x - .009f, z), new Vector2(x + .009f, Mathf.Min(z + 1f, depth * .5f)));
            }
            if (vertices.Count == 0) return;
            var mesh = new Mesh { name = "House floor joinery" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            generatedMeshes.Add(mesh);
            var seams = new GameObject("Floor joinery");
            seams.transform.SetParent(transform, false);
            seams.transform.position = new Vector3(bounds.center.x, bounds.max.y + .009f, bounds.center.z);
            seams.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = seams.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GameAssets.I.Tinted(Color.Lerp(color, new Color(.34f, .24f, .17f), .18f));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        static void Strip(List<Vector3> vertices, List<int> indices, Vector2 min, Vector2 max)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(min.x, 0f, min.y));
            vertices.Add(new Vector3(min.x, 0f, max.y));
            vertices.Add(new Vector3(max.x, 0f, max.y));
            vertices.Add(new Vector3(max.x, 0f, min.y));
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
            indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
        }

        void OnDestroy()
        {
            ReleaseGeneratedMeshes();
        }

        void ReleaseGeneratedMeshes()
        {
            foreach (var mesh in generatedMeshes)
            {
                if (!mesh) continue;
                if (Application.isPlaying) Destroy(mesh);
                else DestroyImmediate(mesh);
            }
            generatedMeshes.Clear();
        }
    }
}
