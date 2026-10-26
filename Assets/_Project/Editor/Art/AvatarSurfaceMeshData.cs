using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Wreckabulary.EditorTools
{
    public static class AvatarSurfaceMeshData
    {
        public static string Fingerprint(Mesh mesh, bool includeSurface = true)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(mesh.vertexCount); writer.Write((int)mesh.indexFormat); writer.Write(mesh.subMeshCount);
            Write(writer, mesh.bounds.center); Write(writer, mesh.bounds.extents);
            var vertices = mesh.vertices; writer.Write(vertices.Length); foreach (var value in vertices) Write(writer, value);
            if (includeSurface)
            { var normals = mesh.normals; writer.Write(normals.Length); foreach (var value in normals) Write(writer, value); }
            var attributes = includeSurface ? DescribeVertexAttributes(mesh) : mesh.GetVertexAttributes().Where(a => a.attribute != VertexAttribute.Normal && a.attribute != VertexAttribute.Tangent).Select(a => a.attribute + ":" + a.format + "x" + a.dimension + " stream" + a.stream).ToArray(); writer.Write(attributes.Length); foreach (string value in attributes) writer.Write(value);
            if (includeSurface) { var tangents = mesh.tangents; writer.Write(tangents.Length); foreach (var value in tangents) Write(writer, value); }
            var colors = mesh.colors; writer.Write(colors.Length);
            foreach (var value in colors) { writer.Write(value.r); writer.Write(value.g); writer.Write(value.b); writer.Write(value.a); }
            var uv = new List<Vector4>();
            for (int channel = 0; channel < 8; channel++)
            { uv.Clear(); mesh.GetUVs(channel, uv); writer.Write(uv.Count); foreach (var value in uv) Write(writer, value); }
            var bindposes = mesh.bindposes; writer.Write(bindposes.Length);
            foreach (var matrix in bindposes) for (int i = 0; i < 16; i++) writer.Write(matrix[i]);
            var counts = mesh.GetBonesPerVertex(); writer.Write(counts.Length);
            for (int i = 0; i < counts.Length; i++) writer.Write(counts[i]);
            var weights = mesh.GetAllBoneWeights(); writer.Write(weights.Length);
            for (int i = 0; i < weights.Length; i++) { writer.Write(weights[i].boneIndex); writer.Write(weights[i].weight); }
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                var descriptor = mesh.GetSubMesh(i);
                writer.Write((int)descriptor.topology); writer.Write(descriptor.indexStart); writer.Write(descriptor.indexCount);
                writer.Write(descriptor.baseVertex); writer.Write(descriptor.firstVertex); writer.Write(descriptor.vertexCount);
                Write(writer, descriptor.bounds.center); Write(writer, descriptor.bounds.extents);
                var indices = mesh.GetIndices(i); writer.Write(indices.Length); foreach (int index in indices) writer.Write(index);
            }
            writer.Write(mesh.blendShapeCount);
            var positions = new Vector3[mesh.vertexCount]; var shapeNormals = new Vector3[mesh.vertexCount]; var shapeTangents = new Vector3[mesh.vertexCount];
            for (int shape = 0; shape < mesh.blendShapeCount; shape++)
            {
                writer.Write(mesh.GetBlendShapeName(shape)); int frames = mesh.GetBlendShapeFrameCount(shape); writer.Write(frames);
                for (int frame = 0; frame < frames; frame++)
                {
                    writer.Write(mesh.GetBlendShapeFrameWeight(shape, frame));
                    mesh.GetBlendShapeFrameVertices(shape, frame, positions, shapeNormals, shapeTangents);
                    for (int i = 0; i < mesh.vertexCount; i++) { Write(writer, positions[i]); if (includeSurface) { Write(writer, shapeNormals[i]); Write(writer, shapeTangents[i]); } }
                }
            }
            writer.Flush(); return Hash(stream.ToArray());
        }

        static void Write(BinaryWriter writer, Vector3 value) { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); }
        static void Write(BinaryWriter writer, Vector4 value) { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); writer.Write(value.w); }
        static string[] DescribeVertexAttributes(Mesh mesh) => mesh.GetVertexAttributes()
            .Select(value => value.attribute + ":" + value.format + "x" + value.dimension + " stream" + value.stream).ToArray();
        public static bool Finite(Vector3 value) => !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z)
            || float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));

        public static string Hash(byte[] bytes) { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    }
}
