using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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

        static string MaterialOf(Component root, string name) => Named(root, name).First().sharedMaterial.name;

        [UnityTest]
        public IEnumerator FloorsFollowRoomNames()
        {
            var house = Build("courtyard");
            yield return null;
            Assert.AreEqual("Surface Lawn:1.00:86A17A", MaterialOf(house, "Garden floor"));
            Assert.AreEqual("Surface Checker:1.00:FFFFFF", MaterialOf(house, "Kitchen floor"), "the kitchen's own two-tone checker");
            Assert.AreEqual("Surface Planks:1.00:B68E6B", MaterialOf(house, "Study floor"), "the study's darker oak");
            Assert.AreEqual("Surface Planks:1.00:CBA37B", MaterialOf(house, "LivingRoom floor"));
            Assert.AreSame(Named(house, "LivingRoom floor")[0].sharedMaterial, Named(house, "Bedroom floor")[0].sharedMaterial, "oak floors share one material");
            Assert.IsNotNull(Named(house, "LivingRoom floor")[0].GetComponent<BoxCollider>());
            Assert.IsFalse(Named(house, "Rug").Any(r => house.Layout.RoomAt(r.bounds.center.x, r.bounds.center.y, r.bounds.center.z) == "Garden"), "no rug on the lawn");

            var flat = Build("flat");
            yield return null;
            Assert.AreEqual("Surface Tile:1.00:FFFFFF", MaterialOf(flat, "Bathroom floor"), "bath tile");

            var walkup = Build("walkup");
            yield return null;
            Assert.AreEqual("Surface Plaster:1.00:A7A39A", MaterialOf(walkup, "Garage floor"), "a concrete garage");
            Assert.AreEqual("Surface Planks:1.00:CBA37B", MaterialOf(walkup, "Landing1 floor"));
        }

        [UnityTest]
        public IEnumerator WallsArePlasterWithAWoodCapThatFollowsTheirHeight()
        {
            var house = Build("terrace");
            yield return null;
            var walls = house.GetComponentsInChildren<TallWall>(true);
            Assert.IsNotEmpty(walls);
            Assert.AreEqual(1, walls.Select(w => w.GetComponent<Renderer>().sharedMaterial).Distinct().Count(), "every wall shares one plaster");
            StringAssert.StartsWith("Surface Plaster", walls[0].GetComponent<Renderer>().sharedMaterial.name);
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
            Assert.AreEqual("173A3D", ColorUtility.ToHtmlStringRGB(RenderSettings.fogColor), "the camera's own background colour");
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
