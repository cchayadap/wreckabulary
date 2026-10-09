using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Art;

namespace Wreckabulary.Tests
{
    public class WorldSurfacesTests
    {
        [UnitySetUp] public IEnumerator SetUp() => TestScenes.Reset();
        [UnityTearDown] public IEnumerator TearDown() => TestScenes.Reset();

        static RoomBuilder Build(string map)
        {
            Match.ModeOverride = "Dibs";
            Session.SelectMap(map);
            var room = new GameObject("Surfaces test house").AddComponent<RoomBuilder>();
            foreach (var prop in room.Originals) prop.gameObject.SetActive(false);
            return room;
        }

        static Renderer[] Named(Component root, string name) =>
            root.GetComponentsInChildren<Renderer>(true).Where(r => r.name == name).ToArray();

        static void AssertSurface(Component root, string name, Surfaces.Kind kind, uint tint) =>
            AssertSurface(Named(root, name).First().sharedMaterial, kind, tint, name);

        static void AssertSurface(Material actual, Surfaces.Kind kind, uint tint, string context)
        {
            var expected = Surfaces.Get(kind, Surfaces.Hex(tint));
            Assert.IsNotNull(actual, context);
            Assert.AreSame(expected.shader, actual.shader, context + " uses the surface shader");
            Assert.AreEqual(ColorUtility.ToHtmlStringRGBA(expected.color), ColorUtility.ToHtmlStringRGBA(actual.color), context + " tint");
            Assert.AreEqual(expected.GetFloat("_Smoothness"), actual.GetFloat("_Smoothness"), 1e-5f, context + " finish");
            Assert.AreEqual(expected.GetFloat("_BumpScale"), actual.GetFloat("_BumpScale"), 1e-5f, context + " normal strength");
            Assert.IsTrue(actual.IsKeywordEnabled("_NORMALMAP"), context + " normal shading enabled");
            foreach (string property in new[] { "_BaseMap", "_BumpMap" })
            {
                Assert.AreEqual(expected.GetTextureScale(property), actual.GetTextureScale(property), context + property + " tiling");
                Assert.AreEqual(expected.GetTextureOffset(property), actual.GetTextureOffset(property), context + property + " offset");
                AssertTexturePattern(expected.GetTexture(property), actual.GetTexture(property), context + property);
            }
        }

        static void AssertTexturePattern(Texture expected, Texture actual, string context)
        {
            Assert.IsNotNull(actual, context);
            Assert.AreEqual(expected.width, actual.width, context + " width");
            Assert.AreEqual(expected.height, actual.height, context + " height");
            Assert.AreEqual(expected.graphicsFormat, actual.graphicsFormat, context + " colour space");
            Assert.AreEqual(expected.wrapMode, actual.wrapMode, context + " wrap");
            Assert.AreEqual(expected.filterMode, actual.filterMode, context + " filtering");
            var expectedPixels = SampleTexture(expected);
            var actualPixels = SampleTexture(actual);
            for (int i = 0; i < expectedPixels.Length; i++)
            {
                Assert.AreEqual(expectedPixels[i].r, actualPixels[i].r, 2, context + " red sample " + i);
                Assert.AreEqual(expectedPixels[i].g, actualPixels[i].g, 2, context + " green sample " + i);
                Assert.AreEqual(expectedPixels[i].b, actualPixels[i].b, 2, context + " blue sample " + i);
                Assert.AreEqual(expectedPixels[i].a, actualPixels[i].a, 2, context + " alpha sample " + i);
            }
        }

        static Color32[] SampleTexture(Texture texture)
        {
            const int size = 16;
            var target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var pixels = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var previous = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            try
            {
                GL.sRGBWrite = false;
                Graphics.Blit(texture, target);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, size, size), 0, 0, false);
                return pixels.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                GL.sRGBWrite = previousSrgb;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(pixels);
            }
        }

        [UnityTest]
        public IEnumerator FloorsFollowRoomNames()
        {
            var house = Build("courtyard");
            yield return null;
            AssertSurface(house, "Garden floor", Surfaces.Kind.Lawn, 0x86A17A);
            AssertSurface(house, "Kitchen floor", Surfaces.Kind.Checker, 0xFFFFFF);
            AssertSurface(house, "Study floor", Surfaces.Kind.Planks, 0xB68E6B);
            AssertSurface(house, "LivingRoom floor", Surfaces.Kind.Planks, 0xCBA37B);
            Assert.AreSame(Named(house, "LivingRoom floor")[0].sharedMaterial, Named(house, "Bedroom floor")[0].sharedMaterial, "oak floors share one material");
            Assert.IsNotNull(Named(house, "LivingRoom floor")[0].GetComponent<BoxCollider>());
            Assert.IsFalse(Named(house, "Rug").Any(r => house.Layout.RoomAt(r.bounds.center.x, r.bounds.center.y, r.bounds.center.z) == "Garden"), "no rug on the lawn");

            var flat = Build("flat");
            yield return null;
            AssertSurface(flat, "Bathroom floor", Surfaces.Kind.Tile, 0xFFFFFF);

            var walkup = Build("walkup");
            yield return null;
            AssertSurface(walkup, "Garage floor", Surfaces.Kind.Plaster, 0xA7A39A);
            AssertSurface(walkup, "Landing1 floor", Surfaces.Kind.Planks, 0xCBA37B);
        }

        [UnityTest]
        public IEnumerator WallsArePlasterWithAWoodCapThatFollowsTheirHeight()
        {
            var house = Build("terrace");
            yield return null;
            var walls = house.GetComponentsInChildren<TallWall>(true);
            Assert.IsNotEmpty(walls);
            Assert.AreEqual(1, walls.Select(w => w.GetComponent<Renderer>().sharedMaterial).Distinct().Count(), "every wall shares one plaster");
            AssertSurface(walls[0].GetComponent<Renderer>().sharedMaterial, Surfaces.Kind.Plaster, 0xEDDFC4, "walls");
            AssertSurface(walls[0].Trim.GetComponent<Renderer>().sharedMaterial, Surfaces.Kind.Wood, 0xB58760, "wall cap");
            foreach (bool tall in new[] { true, false, true })
            {
                house.SetTallWalls(tall);
                foreach (var wall in walls)
                {
                    Assert.IsNotNull(wall.Trim, "a cap on every wall");
                    Assert.IsNull(wall.Trim.GetComponent<Collider>(), "the cap isn't solid");
                    Assert.AreEqual(wall.FloorY + wall.VisualHeight(tall) + .04f, wall.Trim.position.y, 1e-4f, "the cap sits on top");
                    var mesh = wall.GetComponent<MeshFilter>().sharedMesh;
                    var normals = mesh.normals; var uv = mesh.uv;
                    var up = Enumerable.Range(0, normals.Length).Where(i => normals[i].y == 0f).Select(i => uv[i].y).ToArray();
                    Assert.AreEqual(wall.VisualHeight(tall) / 3f, up.Max() - up.Min(), 1e-3f);
                }
            }
        }

        [UnityTest]
        public IEnumerator RugsAndSurroundingsAreOnlyToLookAt()
        {
            var house = Build("terrace");
            yield return null;
            var layout = house.Layout;
            var rugs = Named(house, "Rug");
            Assert.Greater(rugs.Length, 3, "a rug in most rooms");
            foreach (var rug in rugs)
            {
                var b = rug.bounds;
                Assert.IsNull(rug.GetComponent<Collider>(), "nobody trips on a rug");
                var room = layout.Room(layout.RoomAt(b.center.x, b.center.y, b.center.z));
                Assert.GreaterOrEqual(b.min.x, room.MinX + .49f, room.Name); Assert.LessOrEqual(b.max.x, room.MaxX - .49f, room.Name);
                Assert.GreaterOrEqual(b.min.z, room.MinZ + .49f, room.Name); Assert.LessOrEqual(b.max.z, room.MaxZ - .49f, room.Name);
                foreach (var s in layout.Stairs.Where(s => s.Lower == room.Name || s.Upper == room.Name))
                    Assert.IsFalse(b.min.x < s.MaxX && b.max.x > s.MinX && b.min.z < s.MaxZ && b.max.z > s.MinZ, $"the {room.Name} rug clears the stairs");
            }

            foreach (var name in new[] { "Lawn", "Plinth" })
                Assert.IsNull(Named(house, name).Single().GetComponent<Collider>(), name + " isn't solid");
            Assert.Less(Named(house, "Lawn")[0].bounds.max.y, -.3f, "the lawn lies below the floors");
            Assert.Greater(Named(house, "Lawn")[0].bounds.size.x, 200f, "out into the haze");
            Assert.AreEqual(16, house.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Items/PLANT"), "plants all round");

            Assert.IsTrue(RenderSettings.fog);
            Assert.AreEqual(FogMode.Linear, RenderSettings.fogMode);
            Assert.AreEqual("173A3D", ColorUtility.ToHtmlStringRGB(RenderSettings.fogColor), "the generated fixture retains its fallback horizon");
            float overhead = RenderSettings.fogStartDistance;
            RoomBuilder.ApplyFog(true);
            Assert.Less(RenderSettings.fogStartDistance, overhead, "nearer round a third-person camera");

            var bare = RoomBuilder.CreateGeometry(layout, "terrace", null, false);
            yield return null;
            var parts = bare.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToList();
            CollectionAssert.DoesNotContain(parts, "Rug");
            CollectionAssert.DoesNotContain(parts, "Lawn");
            CollectionAssert.DoesNotContain(parts, "Items/PLANT");
            CollectionAssert.Contains(parts, "Wall trim");
            Object.Destroy(bare.gameObject);
        }
    }
}
