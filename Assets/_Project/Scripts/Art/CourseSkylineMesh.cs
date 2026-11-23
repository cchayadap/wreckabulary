using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Rules.Surroundings;

namespace Wreckabulary.Art
{
    public static class CourseSkylineMesh
    {
        public const string RootName = "Skyline";

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), SmoothnessId = Shader.PropertyToID("_Smoothness"), EmissionId = Shader.PropertyToID("_EmissionColor");

        public static MaterialPropertyBlock Surface(string slot)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, Color.white);
            block.SetFloat(SmoothnessId, CourseSkyline.SmoothnessOf(slot));
            block.SetColor(EmissionId, slot == CourseSkyline.LitSlot ? new Color(CourseSkyline.GlowRed, CourseSkyline.GlowGreen, CourseSkyline.GlowBlue) : Color.black);
            return block;
        }

        public static GameObject Build(CourseSkylineResult skyline, CourseBackdropKit kit, string name, bool readable, List<Mesh> owned)
        {
            if (skyline == null) throw new ArgumentNullException(nameof(skyline));
            if (!kit) throw new ArgumentNullException(nameof(kit));
            var root = new GameObject(RootName);
            var surfaces = new Dictionary<string, MaterialPropertyBlock>(StringComparer.Ordinal);
            foreach (var part in CourseSkyline.Parts(skyline))
            {
                var material = kit.MaterialFor(part.Slot);
                if (!material) throw new InvalidOperationException($"The course backdrop kit has no material for {part.Slot}.");
                var mesh = new Mesh { name = $"{name} {RootName} {part.Label}" };
                owned.Add(mesh);
                mesh.indexFormat = part.Vertices > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(Vectors(part.Positions));
                mesh.SetNormals(Vectors(part.Normals));
                mesh.SetColors(Colours(part.Colours));
                mesh.SetTriangles(part.Indices, 0, false);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(!readable);
                var go = new GameObject(part.Label, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root.transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                if (!surfaces.TryGetValue(part.Slot, out var surface)) surfaces[part.Slot] = surface = Surface(part.Slot);
                renderer.SetPropertyBlock(surface);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
                renderer.allowOcclusionWhenDynamic = true;
                renderer.staticShadowCaster = false;
            }
            return root;
        }

        static Vector3[] Vectors(float[] flat)
        {
            var vectors = new Vector3[flat.Length / 3];
            for (int i = 0; i < vectors.Length; i++) vectors[i] = new Vector3(flat[i * 3], flat[i * 3 + 1], flat[i * 3 + 2]);
            return vectors;
        }

        static Color32[] Colours(uint[] tints)
        {
            var colours = new Color32[tints.Length];
            for (int i = 0; i < tints.Length; i++) colours[i] = new Color32((byte)(tints[i] >> 16), (byte)(tints[i] >> 8), (byte)tints[i], 0);
            return colours;
        }
    }
}
