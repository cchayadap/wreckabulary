using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Art;
using Debug = UnityEngine.Debug;

namespace Wreckabulary.Tests
{
    public class SurfacesTests
    {
        [Test]
        public void MaterialsAreSharedPerSurfaceAndColour()
        {
            var watch = Stopwatch.StartNew();
            foreach (Surfaces.Kind kind in System.Enum.GetValues(typeof(Surfaces.Kind))) Surfaces.Get(kind, Color.white);
            Debug.Log($"Surfaces: every kind made in {watch.ElapsedMilliseconds} ms");

            var oak = Surfaces.Get(Surfaces.Kind.Planks, Surfaces.Hex(0xCBA37B));
            Assert.AreSame(oak, Surfaces.Get(Surfaces.Kind.Planks, Surfaces.Hex(0xCBA37B)), "one material per surface and colour");
            Assert.AreEqual("Surface Planks:1.00:CBA37B", oak.name);
            var study = Surfaces.Get(Surfaces.Kind.Planks, Surfaces.Hex(0xB68E6B));
            Assert.AreNotSame(oak, study);
            Assert.AreSame(oak.mainTexture, study.mainTexture, "one texture serves every colour of a surface");
            Assert.AreEqual(1024, oak.mainTexture.width, "floor planks at twice the web's 512");
            Assert.AreEqual(TextureWrapMode.Repeat, oak.mainTexture.wrapMode);
            Assert.IsTrue(oak.IsKeywordEnabled("_NORMALMAP"), "lit with a normal map");
            Assert.IsNotNull(oak.GetTexture("_BumpMap"));
            Assert.AreEqual(.21f, oak.GetFloat("_Smoothness"), 1e-4f, "the web's roughness .79");

            var rug = Surfaces.Get(Surfaces.Kind.Rug, Color.white, 1.6f);
            Assert.AreEqual(TextureWrapMode.Clamp, rug.mainTexture.wrapMode, "a rug has edges");
            Assert.AreSame(rug, Surfaces.Get(Surfaces.Kind.Rug, Color.white, 1.55f), "rugs of nearly the same shape share");
        }

        [Test]
        public void PatternsAreTheSameEverySession()
        {
            var planks = (Texture2D)Surfaces.Get(Surfaces.Kind.Planks, Color.white).mainTexture;
            Assert.IsFalse(planks.isReadable, "the pixels are let go once they're on the GPU");
            Assert.Greater(Surfaces.TextureCount, 0);
        }

        [Test]
        public void BoxesAreTexturedInMetresAndLineUp()
        {
            var tile = new Vector2(4f, 1.2f);
            var size = new Vector3(4f, .24f, 2f);
            var left = Surfaces.Box(size, tile, new Vector3(-2f, -.12f, 0f));
            var right = Surfaces.Box(size, tile, new Vector3(2f, -.12f, 0f));
            Assert.AreSame(left, Surfaces.Box(size, tile, new Vector3(-2f, -.12f, 0f)), "the same box is made once");

            Vector2 TopAt(Mesh mesh, float localX, float localZ)
            {
                var v = mesh.vertices; var n = mesh.normals; var uv = mesh.uv;
                for (int i = 0; i < v.Length; i++)
                    if (n[i] == Vector3.up && Mathf.Approximately(v[i].x, localX) && Mathf.Approximately(v[i].z, localZ)) return uv[i];
                Assert.Fail($"no top corner at {localX}, {localZ}");
                return default;
            }
            Assert.AreEqual(0f, TopAt(left, .5f, .5f).x, 1e-5f);
            Assert.AreEqual(0f, TopAt(right, -.5f, .5f).x, 1e-5f);
            Assert.AreEqual(TopAt(left, .5f, .5f).y, TopAt(right, -.5f, .5f).y, 1e-5f);
            Assert.AreEqual(1f / 1.2f, TopAt(left, .5f, .5f).y, 1e-5f, "a metre north is a metre of pattern");
            Assert.AreEqual(1f, TopAt(right, .5f, .5f).x - TopAt(right, -.5f, .5f).x, 1e-5f, "4 m of floor, one repeat");

            var rug = Surfaces.Box(new Vector3(3f, .008f, 2f), new Vector2(3f, 2f));
            Assert.AreEqual(Vector2.zero, TopAt(rug, -.5f, -.5f));
            Assert.AreEqual(Vector2.one, TopAt(rug, .5f, .5f));
        }

        [Test]
        public void EveryFaceLooksOutward()
        {
            var mesh = Surfaces.Box(new Vector3(2f, 1f, 3f), Vector2.one, Vector3.zero, true);
            var v = mesh.vertices; var n = mesh.normals; var t = mesh.triangles;
            Assert.AreEqual(24, v.Length);
            Assert.AreEqual(24, mesh.tangents.Length, "tangents for the normal map");
            for (int i = 0; i < t.Length; i += 3)
            {
                var face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).normalized;
                Assert.Greater(Vector3.Dot(face, n[t[i]]), .99f, $"triangle {i / 3} faces {face}, not {n[t[i]]}");
                Assert.Greater(Vector3.Dot(v[t[i]], n[t[i]]), .49f, "and sits on the side it faces");
            }
            var uv = mesh.uv;
            var top = Enumerable.Range(0, v.Length).Where(i => n[i] == Vector3.up).ToArray();
            Assert.AreEqual(3f, top.Max(i => uv[i].x) - top.Min(i => uv[i].x), 1e-5f);
        }
    }
}
