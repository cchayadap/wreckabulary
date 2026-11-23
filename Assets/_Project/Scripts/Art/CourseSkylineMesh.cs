using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Rules.Surroundings;

namespace Wreckabulary.Art
{
    public static class CourseSkylineMesh
    {
        public const string RootName = "Skyline", ShaderName = "Wreckabulary/Skyline";

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), SmoothnessId = Shader.PropertyToID("_Smoothness"), EmissionId = Shader.PropertyToID("_EmissionColor");

        public static Color Glow => new Color(CourseSkyline.GlowRed, CourseSkyline.GlowGreen, CourseSkyline.GlowBlue) * CourseSkyline.GlowStrength;

        public static MaterialPropertyBlock Fallback()
        {
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, Color.white);
            block.SetFloat(SmoothnessId, CourseSkyline.WallSmoothness);
            block.SetColor(EmissionId, Color.black);
            return block;
        }

        public static Material MaterialOf(CourseBackdropKit kit, out bool fallback)
        {
            var material = kit.MaterialFor(CourseSkyline.SkylineSlot);
            fallback = !material;
            return material ? material : kit.MaterialFor(CourseSkyline.FallbackSlot);
        }

        public static GameObject Build(CourseSkylineResult skyline, CourseBackdropKit kit, string name, bool readable, List<Mesh> owned)
        {
            if (skyline == null) throw new ArgumentNullException(nameof(skyline));
            if (!kit) throw new ArgumentNullException(nameof(kit));
            var material = MaterialOf(kit, out bool fallback);
            if (!material) throw new InvalidOperationException($"The course backdrop kit has no material for {CourseSkyline.SkylineSlot} or {CourseSkyline.FallbackSlot}.");
            var block = fallback ? Fallback() : null;
            var root = new GameObject(RootName);
            foreach (var part in CourseSkyline.Parts(skyline))
            {
                var mesh = new Mesh { name = $"{name} {RootName} {part.Label}" };
                owned.Add(mesh);
                mesh.indexFormat = part.Vertices > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(Vectors(part.Positions));
                mesh.SetNormals(Vectors(part.Normals));
                mesh.SetColors(Colours(part.Colours));
                mesh.SetUVs(0, Quads(part.Facade));
                mesh.SetUVs(1, Quads(part.Grid));
                mesh.SetUVs(2, Quads(part.Glass));
                mesh.SetUVs(3, Pairs(part.Extra));
                mesh.SetTriangles(part.Indices, 0, false);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(!readable);
                var go = new GameObject(part.Label, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root.transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                if (block != null) renderer.SetPropertyBlock(block);
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

        static Vector4[] Quads(float[] flat)
        {
            var vectors = new Vector4[flat.Length / 4];
            for (int i = 0; i < vectors.Length; i++) vectors[i] = new Vector4(flat[i * 4], flat[i * 4 + 1], flat[i * 4 + 2], flat[i * 4 + 3]);
            return vectors;
        }

        static Vector2[] Pairs(float[] flat)
        {
            var vectors = new Vector2[flat.Length / 2];
            for (int i = 0; i < vectors.Length; i++) vectors[i] = new Vector2(flat[i * 2], flat[i * 2 + 1]);
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
