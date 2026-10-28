using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Art;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.WordboundRuntime
{
    [DisallowMultipleComponent]
    public sealed class WordboundRoomArchitecture : MonoBehaviour, IDisposable
    {
        const float Recess = .012f;
        const int MaximumVerticesPerBatch = 60000;
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly Dictionary<Material, Batch> batches = new Dictionary<Material, Batch>();
        readonly List<KeyValuePair<Material, Batch>> completedBatches = new List<KeyValuePair<Material, Batch>>();
        readonly Dictionary<WordboundRoomPalette, PaletteMaterials> materials = new Dictionary<WordboundRoomPalette, PaletteMaterials>();
        WordboundRoomStyleProfile profile;
        WordboundCourseWorld world;
        Material artwork;
        int decoratedWallPieces;

        public int InsetCount { get; private set; }
        public int RenderBatchCount { get; private set; }

        public static void Apply(WordboundCourseWorld world, WordboundCourseWorldOptions options)
        {
            if (!options.EnableRoomArchitecture) return;
            var profile = options.RoomStyle ? options.RoomStyle : Resources.Load<WordboundRoomStyleProfile>(WordboundRoomStyleProfile.ResourcePath);
            if (!profile || !profile.Enabled) return;
            profile.Validate();
            var view = world.gameObject.AddComponent<WordboundRoomArchitecture>();
            view.world = world; view.profile = profile;
            view.Build(options);
        }

        void Build(WordboundCourseWorldOptions options)
        {
            var modules = world.Course.Geometry.ToDictionary(m => m.Id, StringComparer.Ordinal);
            foreach (var collider in world.Surfaces)
            {
                var node = collider.GetComponent<WordboundCourseNode>();
                if (!modules.TryGetValue(node.ModuleId, out var module)) continue;
                if (node.Kind == WordboundCourseNodeKind.Floor && options.FloorMaterial ||
                    node.Kind == WordboundCourseNodeKind.Wall && options.WallMaterial ||
                    node.Kind == WordboundCourseNodeKind.Connection && options.ConnectorMaterial) continue;
                var palette = Materials(profile.ForLayer(module.LayerId));
                if (node.Kind == WordboundCourseNodeKind.Floor && !options.FloorMaterial)
                    Floor(collider, palette);
                else if (node.Kind == WordboundCourseNodeKind.Wall && !options.WallMaterial)
                    Wall(collider, module, palette);
                else if (node.Kind == WordboundCourseNodeKind.Connection && !options.ConnectorMaterial)
                    collider.GetComponent<Renderer>().sharedMaterial = palette.Frame;
            }
            foreach (var pair in completedBatches.Concat(batches))
            {
                var mesh = new Mesh { name = "Room architecture " + RenderBatchCount };
                mesh.SetVertices(pair.Value.Vertices); mesh.SetNormals(pair.Value.Normals);
                mesh.SetUVs(0, pair.Value.Uvs); mesh.SetTriangles(pair.Value.Triangles, 0);
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                ownedMeshes.Add(mesh);
                var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = pair.Key;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                RenderBatchCount++;
            }
            batches.Clear(); completedBatches.Clear();
        }

        void Floor(Collider collider, PaletteMaterials palette)
        {
            var filter = collider.GetComponent<MeshFilter>();
            var mesh = Instantiate(filter.sharedMesh);
            mesh.name = "Room floor and ceiling " + collider.name;
            var original = mesh.triangles; var normals = mesh.normals;
            var indices = new[] { new List<int>(), new List<int>(), new List<int>() };
            for (int i = 0; i < original.Length; i += 3)
            {
                float y = normals[original[i]].y;
                int slot = y > .9f ? 0 : y < -.9f ? 1 : 2;
                indices[slot].Add(original[i]); indices[slot].Add(original[i + 1]); indices[slot].Add(original[i + 2]);
            }
            mesh.subMeshCount = 3;
            for (int i = 0; i < 3; i++) mesh.SetTriangles(indices[i], i);
            ownedMeshes.Add(mesh); filter.sharedMesh = mesh;
            collider.GetComponent<Renderer>().sharedMaterials = new[] { palette.Floor, palette.Ceiling, palette.Frame };
        }

        void Wall(Collider collider, ResolvedCourseModule module, PaletteMaterials palette)
        {
            Vector3 center = world.transform.InverseTransformPoint(collider.transform.position);
            Vector3 size = collider.transform.localScale;
            bool fixedX = Mathf.Abs(center.x - module.Bounds.Min.X / 1000f) < .001f ||
                Mathf.Abs(center.x - module.Bounds.Max.X / 1000f) < .001f;
            Vector3 inward = fixedX ? Vector3.right * (center.x < module.Position.X / 1000f ? 1 : -1) :
                Vector3.forward * (center.z < module.Position.Z / 1000f ? 1 : -1);
            float thickness = fixedX ? size.x : size.z;
            if (thickness <= Recess * 2 || size.y < .001f) return;
            collider.GetComponent<Renderer>().enabled = false;
            var backing = size;
            if (fixedX) backing.x -= Recess * 2; else backing.z -= Recess * 2;
            Box(center, backing, palette.Wall);
            Vector3 right = Vector3.Cross(Vector3.up, inward);
            Vector3 face = center + inward * thickness * .5f;
            float width = fixedX ? size.z : size.x;
            float bottom = center.y - size.y * .5f;
            float roomFloor = module.Bounds.Min.Y / 1000f;

            // Trim and display surfaces occupy the recessed original solid-wall volume.
            if (bottom <= roomFloor + .001f)
                FlatBox(face + Vector3.up * (-size.y * .5f + .13f), right, inward,
                    width, .26f, Recess, palette.Frame);
            if (size.y < 4.1f || width < 3.5f || bottom > roomFloor + .001f ||
                decoratedWallPieces >= profile.MaximumDecoratedWallPieces) return;
            int count = Mathf.Min(profile.MaximumInsetsPerWallPiece, Mathf.FloorToInt(width / 6f));
            if (count == 0 && width >= 4f && profile.MaximumInsetsPerWallPiece > 0) count = 1;
            if (count == 0) return;
            decoratedWallPieces++;
            float insetWidth = Mathf.Min(profile.InsetWidth, width / count - 1.2f);
            float insetHeight = Mathf.Min(profile.InsetHeight, size.y - 1.8f);
            float frameWidth = Mathf.Min(profile.FrameWidth, Mathf.Min(insetWidth, insetHeight) * .2f);
            for (int i = 0; i < count; i++)
            {
                float x = -width * .5f + width * (i + .5f) / count;
                Vector3 at = face + right * x;
                at.y = roomFloor + 1.1f + insetHeight * .5f;
                RoundedPanel(at - inward * .006f, right, inward, insetWidth, insetHeight,
                    Mathf.Min(.8f, insetWidth * .22f), palette.Frame);
                RoundedPanel(at - inward * .003f, right, inward, insetWidth - frameWidth * 2,
                    insetHeight - frameWidth * 2, Mathf.Min(.66f, insetWidth * .18f), palette.Inset);
                FlatBox(at - Vector3.up * (insetHeight * .5f + .07f), right, inward,
                    insetWidth + .2f, .14f, .006f, palette.Accent);
                if (profile.WallArtwork && i == 0 && (module.LayerId == "Attic" || module.Id.EndsWith("ParcelGate", StringComparison.Ordinal)))
                    Artwork(at - inward * .001f, right, inward, insetWidth - .8f, insetHeight - .8f);
                else
                {
                    float barWidth = Mathf.Min(.12f, frameWidth);
                    FlatBox(at, right, inward, barWidth, insetHeight - .45f, .002f, palette.Frame);
                    FlatBox(at + Vector3.up * .12f, right, inward, insetWidth - .45f, barWidth, .002f, palette.Frame);
                }
                InsetCount++;
            }
        }

        void Artwork(Vector3 at, Vector3 right, Vector3 normal, float width, float height)
        {
            if (!artwork)
            {
                var template = GameAssets.I && GameAssets.I.tintBase ? GameAssets.I.tintBase :
                    Surfaces.Get(Surfaces.Kind.Plaster, Color.white);
                artwork = new Material(template) { name = "Original townhouse wall illustration", color = Color.white };
                artwork.mainTexture = profile.WallArtwork;
                if (artwork.HasProperty("_BaseMap")) artwork.SetTexture("_BaseMap", profile.WallArtwork);
                if (artwork.HasProperty("_BumpMap")) { artwork.SetTexture("_BumpMap", null); artwork.DisableKeyword("_NORMALMAP"); }
                ownedMaterials.Add(artwork);
            }
            float ratio = (float)profile.WallArtwork.width / profile.WallArtwork.height;
            float fittedWidth = Mathf.Min(width, height * ratio), fittedHeight = fittedWidth / ratio;
            Quad(at, right * fittedWidth * .5f, Vector3.up * fittedHeight * .5f, normal, artwork);
        }

        void FlatBox(Vector3 face, Vector3 right, Vector3 inward, float width, float height, float depth, Material material)
        {
            Vector3 size = new Vector3(Mathf.Abs(right.x) * width + Mathf.Abs(inward.x) * depth,
                height, Mathf.Abs(right.z) * width + Mathf.Abs(inward.z) * depth);
            Box(face - inward * depth * .5f, size, material);
        }

        void RoundedPanel(Vector3 center, Vector3 right, Vector3 normal, float width, float height, float radius, Material material)
        {
            radius = Mathf.Min(radius, Mathf.Min(width, height) * .49f);
            var batch = BatchFor(material, 37);
            int first = batch.Vertices.Count;
            batch.Vertices.Add(center); batch.Normals.Add(normal); batch.Uvs.Add(new Vector2(.5f, .5f));
            for (int corner = 0; corner < 4; corner++)
            {
                float sx = corner < 2 ? 1 : -1, sy = corner == 0 || corner == 3 ? 1 : -1;
                var origin = new Vector2(sx * (width * .5f - radius), sy * (height * .5f - radius));
                for (int step = 0; step <= 8; step++)
                {
                    float angle = (90 - corner * 90 - step * 90f / 8) * Mathf.Deg2Rad;
                    Vector2 point = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    batch.Vertices.Add(center + right * point.x + Vector3.up * point.y);
                    batch.Normals.Add(normal); batch.Uvs.Add(new Vector2(point.x / width + .5f, point.y / height + .5f));
                }
            }
            int total = 36;
            for (int i = 0; i < total; i++)
            { batch.Triangles.Add(first); batch.Triangles.Add(first + 1 + (i + 1) % total); batch.Triangles.Add(first + 1 + i); }
        }

        void Box(Vector3 center, Vector3 size, Material material)
        {
            Quad(center + Vector3.right * size.x * .5f, Vector3.back * size.z * .5f, Vector3.up * size.y * .5f, Vector3.right, material, true);
            Quad(center - Vector3.right * size.x * .5f, Vector3.forward * size.z * .5f, Vector3.up * size.y * .5f, Vector3.left, material, true);
            Quad(center + Vector3.forward * size.z * .5f, Vector3.right * size.x * .5f, Vector3.up * size.y * .5f, Vector3.forward, material, true);
            Quad(center - Vector3.forward * size.z * .5f, Vector3.left * size.x * .5f, Vector3.up * size.y * .5f, Vector3.back, material, true);
            Quad(center + Vector3.up * size.y * .5f, Vector3.right * size.x * .5f, Vector3.back * size.z * .5f, Vector3.up, material, true);
            Quad(center - Vector3.up * size.y * .5f, Vector3.right * size.x * .5f, Vector3.forward * size.z * .5f, Vector3.down, material, true);
        }

        void Quad(Vector3 center, Vector3 right, Vector3 up, Vector3 normal, Material material, bool metreUv = false)
        {
            var batch = BatchFor(material, 4); int first = batch.Vertices.Count;
            batch.Vertices.AddRange(new[] { center - right - up, center + right - up, center + right + up, center - right + up });
            batch.Normals.AddRange(new[] { normal, normal, normal, normal });
            float u = metreUv ? right.magnitude * 2 / 3 : 1, v = metreUv ? up.magnitude * 2 / 3 : 1;
            batch.Uvs.AddRange(new[] { Vector2.zero, new Vector2(u, 0), new Vector2(u, v), new Vector2(0, v) });
            batch.Triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
        }

        Batch BatchFor(Material material, int additional)
        {
            if (!batches.TryGetValue(material, out var batch)) batches.Add(material, batch = new Batch());
            if (batch.Vertices.Count + additional > MaximumVerticesPerBatch)
            {
                completedBatches.Add(new KeyValuePair<Material, Batch>(material, batch));
                batch = new Batch(); batches[material] = batch;
            }
            return batch;
        }

        PaletteMaterials Materials(WordboundRoomPalette palette)
        {
            if (materials.TryGetValue(palette, out var found)) return found;
            found = new PaletteMaterials {
                Floor = Make(palette.FloorMaterial, palette.Floor, "Floor"), Wall = Make(palette.WallMaterial, palette.Wall, "Wall"),
                Ceiling = Make(palette.CeilingMaterial, palette.Ceiling, "Ceiling"), Frame = Make(palette.FrameMaterial, palette.Frame, "Frame"),
                Inset = Make(palette.InsetMaterial, palette.Inset, "Inset"), Accent = Make(palette.AccentMaterial, palette.Accent, "Accent")
            };
            materials.Add(palette, found); return found;
        }

        Material Make(Material assigned, Color colour, string role)
        {
            if (assigned) return assigned;
            var value = new Material(Surfaces.Get(Surfaces.Kind.Plaster, colour)) { name = "Room " + role, color = colour };
            if (value.HasProperty("_Smoothness")) value.SetFloat("_Smoothness", role == "Inset" ? .45f : .16f);
            ownedMaterials.Add(value); return value;
        }

        void OnDestroy() => Dispose();

        public void Dispose()
        {
            foreach (var mesh in ownedMeshes) Release(mesh);
            foreach (var material in ownedMaterials) Release(material);
            ownedMeshes.Clear(); ownedMaterials.Clear();
        }
        static void Release(UnityEngine.Object value)
        { if (!value) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }

        sealed class PaletteMaterials { public Material Floor, Wall, Ceiling, Frame, Inset, Accent; }
        sealed class Batch
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();
        }
    }
}
