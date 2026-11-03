using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    public sealed class AuthoredHouseTests
    {
        [UnitySetUp] public IEnumerator SetUp() => TestScenes.Reset();
        [UnityTearDown] public IEnumerator TearDown() => TestScenes.Reset();

        [UnityTest]
        public IEnumerator SnapshotNeverEnablesBehavioursUntilFurnitureIsRestored()
        {
            AuthoredFurnitureLifecycleProbe.Awakes = 0;
            AuthoredFurnitureLifecycleProbe.Enables = 0;
            AuthoredFurnitureLifecycleProbe.Starts = 0;
            var root = new GameObject("World with authored behaviour");
            var furniture = new GameObject("Artist furniture").transform;
            furniture.SetParent(root.transform, false);
            furniture.gameObject.AddComponent<AuthoredFurnitureLifecycleProbe>();
            var world = root.AddComponent<AuthoredHouse>();
            world.Configure("pinwheel", null, furniture);
            Assert.AreEqual(1, AuthoredFurnitureLifecycleProbe.Awakes);
            Assert.AreEqual(1, AuthoredFurnitureLifecycleProbe.Enables);

            world.PrepareForPlay();
            world.PrepareForPlay();
            yield return null;
            Assert.AreEqual(1, AuthoredFurnitureLifecycleProbe.Awakes, "An inactive template must not execute authored Awake callbacks.");
            Assert.AreEqual(1, AuthoredFurnitureLifecycleProbe.Enables);
            Assert.AreEqual(1, AuthoredFurnitureLifecycleProbe.Starts);

            var restored = world.ResetFurniture(true);
            Assert.IsNotNull(restored.GetComponent<AuthoredFurnitureLifecycleProbe>());
            yield return null;
            Assert.AreEqual(2, AuthoredFurnitureLifecycleProbe.Awakes);
            Assert.AreEqual(2, AuthoredFurnitureLifecycleProbe.Enables);
            Assert.AreEqual(2, AuthoredFurnitureLifecycleProbe.Starts);
            world.ResetFurniture(false);
            yield return null;
            Assert.AreEqual(2, AuthoredFurnitureLifecycleProbe.Enables, "Empty resets must keep template behaviours dormant.");
        }

        [UnityTest]
        public IEnumerator SceneEditsSurvivePlayAndRoundReset()
        {
            var root = new GameObject("Edited saved world");
            var geometry = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            geometry.name = "Artist wall";
            geometry.SetParent(root.transform, false);
            geometry.localPosition = new Vector3(3f, 1.1f, -2f);
            geometry.localScale = new Vector3(.3f, 2.2f, 4f);
            var furniture = new GameObject("Edited furniture").transform;
            furniture.SetParent(root.transform, false);
            var table = FurnitureCatalog.Spawn("TABLE", new Vector3(2f, .2f, 1f), 37f, furniture);
            table.GetComponent<Rigidbody>().isKinematic = true;
            table.transform.localScale = new Vector3(.8f, .9f, 1.1f);
            table.Init("TABLE", 77f);
            var material = GameAssets.I.Tinted(new Color(.22f, .43f, .61f));
            table.GetComponentInChildren<Renderer>().sharedMaterial = material;
            var world = root.AddComponent<AuthoredHouse>();
            world.Configure("pinwheel", geometry, furniture);
            var position = table.transform.position;
            var rotation = table.transform.rotation;
            var scale = table.transform.localScale;
            var builder = new GameObject("Authored game").AddComponent<RoomBuilder>();

            Assert.AreSame(world, builder.AuthoredWorld);
            Assert.AreEqual(1, builder.Originals.Count);
            Assert.AreSame(table, builder.Originals[0]);
            Assert.AreEqual(position, table.transform.position);
            table.transform.position += Vector3.forward * 4f;
            table.TakeHit(18f);
            builder.ResetRoom();
            yield return null;

            Assert.AreSame(world, builder.AuthoredWorld);
            Assert.AreEqual(new Vector3(3f, 1.1f, -2f), geometry.localPosition);
            Assert.AreEqual(new Vector3(.3f, 2.2f, 4f), geometry.localScale);
            Assert.AreEqual(1, builder.Originals.Count);
            var restored = builder.Originals[0];
            Assert.Less(Vector3.Distance(position, restored.transform.position), .01f);
            Assert.Less(Quaternion.Angle(rotation, restored.transform.rotation), .001f);
            Assert.AreEqual(scale, restored.transform.localScale);
            Assert.AreEqual(77f, restored.Health);
            Assert.AreSame(material, restored.GetComponentInChildren<Renderer>().sharedMaterial);
            Assert.IsTrue(restored.GetComponentsInChildren<Renderer>(true).All(renderer => renderer.sharedMaterials.All(shared => shared)),
                "Destroying the original furniture must not release tactile variants still used by its restored copy.");
            builder.ResetRoom(false);
            Assert.AreEqual(0, builder.Originals.Count);
            builder.ResetRoom();
            Assert.AreEqual(1, builder.Originals.Count, "An empty Moving Day reset must not discard the authored snapshot.");
            Assert.Less(Vector3.Distance(position, builder.Originals[0].transform.position), .01f);
        }

        [UnityTest]
        public IEnumerator MapSelectionLoadsItsAuthoredResourceInsteadOfRegeneratingTheOtherMap()
        {
            foreach (string map in new[] { "pinwheel", "courtyard", "flat", "terrace", "walkup" })
            {
                yield return TestScenes.Reset();
                var saved = new GameObject("Saved different map").AddComponent<AuthoredHouse>();
                saved.Configure(map == "pinwheel" ? "courtyard" : "pinwheel", null, null);
                Session.SelectMap(map);
                var prefab = Resources.Load<GameObject>(AuthoredHouse.ResourcePath(map));
                Assert.IsNotNull(prefab, "Run the current world migration before this integration test: " + map);
                var builder = new GameObject("Selected map game").AddComponent<RoomBuilder>();
                yield return null;
                Assert.IsNotNull(builder.AuthoredWorld, map);
                Assert.AreEqual(map, builder.AuthoredWorld.MapId);
                Assert.IsTrue(builder.AuthoredWorld.IsCurrent);
                Assert.IsFalse(saved.gameObject.activeSelf);
                Assert.IsTrue(builder.AuthoredWorld.GeometryRoot.gameObject.activeInHierarchy);
                Assert.AreEqual(builder.Layout.Furniture.Count, builder.Originals.Count);
                var presentation = builder.AuthoredWorld.GeometryRoot.GetComponent<Art.HousePresentation>();
                Assert.IsNotNull(presentation);
                Assert.IsTrue(presentation.IsAuthored, "Persistent colors and meshes must not be repainted on load.");
                for (int floor = 0; floor < builder.Layout.StoreyFloors().Count; floor++)
                    Assert.IsNotNull(builder.AuthoredWorld.GeometryRoot.Find(RoomBuilder.StoreyName(floor)), map);
                if (builder.Layout.StoreyFloors().Count > 1) Assert.IsNotNull(builder.GetComponent<StoreyCutaway>(), map);
            }
        }

        static Rules.HomeLayout TestHome(string map)
        {
            var designer = new Rules.HomeDesigner(GameConfig.Current.Houses, GameConfig.Current.Items);
            var house = GameConfig.Current.HouseFor(map);
            var batch = designer.AddWords(designer.CreateLayout(map, "Match Test Home"), "SOFA LAMP PLANT", house.Rooms[0].Name);
            Assert.IsTrue(batch.Ok, string.Join("\n", batch.Errors));
            Assert.AreEqual(3, batch.Added.Count);
            batch.Layout.Props[0].Skin = "Candy";
            return batch.Layout;
        }

        static void AssertFurnished(RoomBuilder builder, Rules.HomeLayout home)
        {
            CollectionAssert.AreEquivalent(home.Props.Select(prop => prop.Word), builder.Originals.Select(prop => prop.Word));
            foreach (var prop in home.Props)
                Assert.IsTrue(builder.Originals.Any(original => original.Word == prop.Word &&
                    Mathf.Abs(original.transform.position.x - (float)prop.X) < .5f && Mathf.Abs(original.transform.position.z - (float)prop.Z) < .5f),
                    prop.Word + " stands where the home builder put it");
        }

        [UnityTest]
        public IEnumerator ASavedHomeFurnishesDibsAndReturnsAfterARoundReset()
        {
            foreach (string map in new[] { "pinwheel", "courtyard" })
            {
                yield return TestScenes.Reset();
                var home = TestHome(map);
                Session.LoadMode("Dibs", map, home);
                yield return TestScenes.WaitForActive(Session.DibsScene);
                var builder = Object.FindAnyObjectByType<RoomBuilder>();
                Assert.AreEqual(home.Name, builder.HomeName, map);
                Assert.AreEqual(map, builder.AuthoredWorld.MapId);
                AssertFurnished(builder, home);
                Assert.AreSame(builder.AuthoredWorld.FurnitureRoot, builder.Originals[0].transform.parent);
                builder.ResetRoom();
                yield return null;
                AssertFurnished(builder, home);
            }
        }

        [UnityTest]
        public IEnumerator ObjectiveModesAndOtherMapsKeepTheirOwnFurniture()
        {
            var home = TestHome("pinwheel");
            Session.LoadMode("MovingOut", "pinwheel", home);
            Assert.IsNull(Session.Home, "Moving Out needs keepsakes a home save does not have");
            yield return TestScenes.WaitForActive(Session.DibsScene);
            Assert.IsNull(Object.FindAnyObjectByType<RoomBuilder>().HomeName);
            yield return TestScenes.Reset();
            Session.LoadMode("Duos", "courtyard", home);
            Assert.IsNull(Session.Home, "a home only furnishes the map it was built on");
            yield return TestScenes.WaitForActive(Session.DibsScene);
            Assert.IsNull(Object.FindAnyObjectByType<RoomBuilder>().HomeName);
            Session.LoadMode("Dibs", "pinwheel", home);
            Assert.IsNotNull(Session.Home);
            yield return null;
            yield return null;
            Session.Load(Session.TutorialScene);
            Assert.IsNull(Session.Home, "any other scene load forgets the home");
            yield return TestScenes.WaitForActive(Session.TutorialScene);
        }

        [UnityTest]
        public IEnumerator ObsoleteSceneWorldCannotOverrideTheCurrentMap()
        {
            var obsolete = new GameObject("Pre-migration Pinwheel").AddComponent<AuthoredHouse>();
            Assert.IsFalse(obsolete.IsCurrent);
            Session.SelectMap("pinwheel");
            var builder = new GameObject("Current map game").AddComponent<RoomBuilder>();
            yield return null;
            Assert.IsFalse(obsolete.gameObject.activeSelf);
            Assert.IsNotNull(builder.AuthoredWorld);
            Assert.AreNotSame(obsolete, builder.AuthoredWorld);
            Assert.IsTrue(builder.AuthoredWorld.IsCurrent);
            Assert.IsNotEmpty(builder.AuthoredWorld.GeometryRoot.GetComponentsInChildren<TallWall>(true));
        }
    }

    public sealed class AuthoredFurnitureLifecycleProbe : MonoBehaviour
    {
        public static int Awakes, Enables, Starts;
        void Awake() => Awakes++;
        void OnEnable() => Enables++;
        void Start() => Starts++;
    }
}
