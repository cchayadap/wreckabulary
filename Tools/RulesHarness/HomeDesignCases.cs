using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Tests;

namespace Wreckabulary.RulesHarness
{
    [TestFixture]
    public sealed class HomeDesignCases
    {
        HomeDesigner designer;
        ItemCatalogue catalogue;

        [SetUp]
        public void Load()
        {
            catalogue = TestData.Catalogue();
            designer = new HomeDesigner(new Dictionary<string, HouseLayout>
            {
                ["pinwheel"] = TestData.House(),
                ["courtyard"] = HouseLayout.FromJson(TestData.Read("house_courtyard.json"))
            }, catalogue);
        }

        HomeLayout Empty(string map = "pinwheel") => designer.CreateLayout(map, "Cozy");
        static HomeProp Prop(string word = "BALL", double x = 0, double z = 0, int yaw = 0, string id = "p1") =>
            new HomeProp { Id = id, Word = word, X = x, Z = z, Yaw = yaw, Skin = "Classic" };
        static void Refused(HomeValidation result, string code)
        {
            Assert.IsFalse(result.Ok);
            Assert.IsNull(result.Layout, "Rejected operations cannot produce a replacement design.");
            Assert.IsTrue(result.Errors.Any(e => e.StartsWith(code, StringComparison.Ordinal)), string.Join("\n", result.Errors));
        }
        HomeDesigner InRoom(float minX = -5, float minZ = -5, float maxX = 5, float maxZ = 5,
            Doorway door = null, SpawnPoint spawn = null)
        {
            var house = new HouseLayout { Name = "Geometry fixture" };
            house.Rooms.Add(new RoomBox { Name = "Room", MinX = minX, MinZ = minZ, MaxX = maxX, MaxZ = maxZ });
            if (door != null) house.Doors.Add(door);
            if (spawn != null) house.Spawns.Add(spawn);
            return new HomeDesigner(new Dictionary<string, HouseLayout> { ["pinwheel"] = house }, catalogue);
        }

        [Test]
        public void HomeDesignPortableImportsAgreeWithCheckedInFixtures()
        {
            foreach (var c in Fixtures()["imports"].Items)
            {
                var result = designer.Import(c["json"].String());
                Assert.AreEqual(c["ok"].Bool(), result.Ok, c["name"].String() + ": " + string.Join("; ", result.Errors));
                if (result.Ok)
                    Assert.AreEqual(result.Layout.ToJson(), designer.Import(result.Layout.ToJson()).Layout.ToJson());
                else
                {
                    Assert.IsNull(result.Layout, c["name"].String());
                    if (c.Has("errorPrefix")) Assert.IsTrue(result.Errors.Any(e => e.StartsWith(c["errorPrefix"].String(), StringComparison.Ordinal)), c["name"].String());
                }
            }
        }

        [Test]
        public void HomeDesignPortableBatchesHaveTheSameExactUnscaledOutput()
        {
            foreach (var c in Fixtures()["batches"].Items)
            {
                var layout = Empty(c["map"].String());
                var before = layout.ToJson();
                var result = designer.AddWords(layout, c["text"].String(), c["room"].String());
                Assert.IsTrue(result.Ok, c["name"].String());
                Assert.AreEqual(before, layout.ToJson(), "Batch input is immutable.");
                Assert.AreEqual(c["expected"].Items.Count, result.Added.Count, c["name"].String());
                for (int i = 0; i < result.Added.Count; i++)
                {
                    var expected = c["expected"].Items[i]; var actual = result.Added[i];
                    Assert.AreEqual(expected["id"].String(), actual.Id);
                    Assert.AreEqual(expected["word"].String(), actual.Word);
                    Assert.AreEqual(expected["x"].Number(), actual.X);
                    Assert.AreEqual(expected["z"].Number(), actual.Z);
                    Assert.AreEqual(expected["yaw"].Int(), actual.Yaw);
                    Assert.AreEqual(expected["skin"].String(), actual.Skin);
                }
                CollectionAssert.AreEqual(c["rejected"].Strings(), result.Rejected.Select(r => r.Word));
                Assert.IsTrue(designer.Validate(result.Layout).Ok, c["name"].String());
            }
        }

        static JsonNode Fixtures() => Json.Parse(File.ReadAllText(Path.Combine(TestData.Root, "Tools/HomeDesign/fixtures.json")));

        [Test]
        public void HomeDesignCreationWhitespaceMatchesPortableFixtures()
        {
            foreach (var c in Fixtures()["creations"].Items)
            {
                var layout = designer.CreateLayout(c["map"].String(), c["input"].String());
                Assert.AreEqual(c["expectedName"].String(), layout.Name, c["name"].String());
                Assert.IsTrue(designer.Validate(layout).Ok, c["name"].String());
            }
        }

        [Test]
        public void HomeDesignFirstFitRotatesWhenTheRoomRequiresIt()
        {
            var shallow = InRoom(-2, -.75f, 2, .75f);
            var result = shallow.AddWords(Empty(), "BED", "Room");
            Assert.IsTrue(result.Ok); Assert.AreEqual(1, result.Added.Count);
            Assert.AreEqual(-1d, result.Added[0].X); Assert.AreEqual(0d, result.Added[0].Z);
            Assert.AreEqual(90, result.Added[0].Yaw);
        }

        [Test]
        public void HomeDesignAllowsAllFortySuppliedModelsRegardlessOfCraftEligibility()
        {
            var supplied = catalogue.All.Where(i => i.Model != null).ToList();
            Assert.AreEqual(40, supplied.Count);
            foreach (var item in supplied)
            {
                var layout = Empty(); layout.Props.Add(Prop(item.Id));
                Assert.IsTrue(designer.Validate(layout).Ok, item.Id);
            }
            foreach (var item in catalogue.All.Where(i => i.Model == null))
            {
                var layout = Empty(); layout.Props.Add(Prop(item.Id));
                Refused(designer.Validate(layout), "word:");
            }
        }

        [Test]
        public void HomeDesignQuarterTurnSwapsTheWholeFootprint()
        {
            var narrow = InRoom(-.75f, -2, .75f, 2);
            var layout = Empty(); layout.Props.Add(Prop("BED"));
            Assert.IsTrue(narrow.Validate(layout).Ok);
            layout.Props[0].Yaw = 90;
            Refused(narrow.Validate(layout), "room:");
            layout.Props[0].Yaw = 180;
            Assert.IsTrue(narrow.Validate(layout).Ok);
            layout.Props[0].Yaw = 270;
            Refused(narrow.Validate(layout), "room:");
        }

        [Test]
        public void HomeDesignReservesDoorFootprintsInsteadOfOnlyObjectCentres()
        {
            var door = InRoom(door: new Doorway { X = 0, Z = 0, Width = 1.4f });
            var layout = Empty(); layout.Props.Add(Prop(x: 1, z: .5));
            Refused(door.Validate(layout), "door:");
            layout.Props[0].Z = 1;
            Assert.IsTrue(door.Validate(layout).Ok, "A clear rectangle corner is permitted.");
        }

        [Test]
        public void HomeDesignReservesSpawnFootprintsAndAppliesMinimumObjectSize()
        {
            var spawn = InRoom(spawn: new SpawnPoint { X = 0, Z = 0 });
            var layout = Empty(); layout.Props.Add(Prop("APPLE", .5, .5));
            Refused(spawn.Validate(layout), "spawn:");
            layout.Props[0].X = 1; layout.Props[0].Z = 0;
            Assert.IsTrue(spawn.Validate(layout).Ok);
        }

        [Test]
        public void HomeDesignClearanceAllowsExactlyOneTenthMetreAndRejectsOverlap()
        {
            var layout = Empty(); layout.Props.Add(Prop("APPLE")); layout.Props.Add(Prop("APPLE", .5, 0, id: "p2"));
            Assert.IsTrue(designer.Validate(layout).Ok);
            layout.Props[1].X = 0;
            Refused(designer.Validate(layout), "overlap:");
        }

        [Test]
        public void HomeDesignReturnsDetachedSnapshotsAndDoesNotChangeInputsOnRefusal()
        {
            var current = Empty(); current.Props.Add(Prop()); var before = current.ToJson();
            var accepted = designer.Validate(current);
            accepted.Layout.Props[0].Skin = "Candy";
            Assert.AreEqual(before, current.ToJson());
            var bad = current.Clone(); bad.Props[0].X = .25;
            Refused(designer.Import(bad.ToJson()), "grid:");
            Assert.AreEqual(before, current.ToJson());
            var batch = designer.AddWords(current, "SOFA", "LivingRoom");
            batch.Added[0].Skin = "Arcade";
            Assert.AreEqual("Classic", batch.Layout.Props.Last().Skin, "Preview results do not alias the committed snapshot.");
            Assert.AreEqual(before, current.ToJson());
        }

        [Test]
        public void HomeDesignBatchSkipsUnknownWordsAndFillsTheLowestUnusedId()
        {
            var layout = Empty(); layout.Props.Add(Prop(id: "p1")); layout.Props.Add(Prop(x: 1, id: "p3"));
            var batch = designer.AddWords(layout, "axE SOFA unknown PLANT", "LivingRoom");
            Assert.IsTrue(batch.Ok);
            CollectionAssert.AreEqual(new[] { "p2", "p4" }, batch.Added.Select(p => p.Id));
            CollectionAssert.AreEqual(new[] { "AXE", "UNKNOWN" }, batch.Rejected.Select(p => p.Word));
            Assert.AreEqual(2, layout.Props.Count);
        }

        [Test]
        public void HomeDesignBatchBoundsAndUnknownRoomRejectAtomically()
        {
            var layout = Empty(); var before = layout.ToJson();
            Refused(designer.AddWords(layout, new string('a', 2049), "LivingRoom"), "text:");
            Refused(designer.AddWords(layout, string.Join(" ", Enumerable.Repeat("BALL", 65)), "LivingRoom"), "text:");
            Refused(designer.AddWords(layout, "SOFA", "Attic"), "room:");
            Assert.AreEqual(before, layout.ToJson());
        }

        [Test]
        public void HomeDesignFullLayoutCanBeEditedButCannotAcceptA65thObject()
        {
            var filled = designer.AddWords(Empty("courtyard"), string.Join(" ", Enumerable.Repeat("APPLE", 64)), "Garden");
            Assert.IsTrue(filled.Ok); Assert.AreEqual(64, filled.Added.Count);
            Assert.IsTrue(designer.PlacementCheck(filled.Layout, filled.Layout.Props[0].Clone(), "p1").Ok);
            Refused(designer.PlacementCheck(filled.Layout, Prop(x: 3, z: 3, id: "new")), "props:");
            var result = designer.AddWords(filled.Layout, "APPLE", "Garden");
            Assert.IsTrue(result.Ok); Assert.AreEqual(0, result.Added.Count); Assert.AreEqual(1, result.Rejected.Count);
            var oversized = filled.Layout.Clone(); oversized.Props.Add(Prop(id: "overflow"));
            Refused(designer.Validate(oversized), "props:");
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(1e308)]
        [TestCase(.500000001)]
        public void HomeDesignRejectsNonFiniteAndAlmostSnappedCoordinates(double x)
        {
            var layout = Empty(); layout.Props.Add(Prop(x: x)); Refused(designer.Validate(layout), "grid:");
        }

        [TestCase("")]
        [TestCase("has space")]
        [TestCase("p1\n")]
        public void HomeDesignRejectsInvalidIds(string id)
        {
            var layout = Empty(); layout.Props.Add(Prop(id: id)); Refused(designer.Validate(layout), "id:");
        }

        [Test]
        public void HomeDesignNameLimitsTrimAtCreationButNeverDuringImport()
        {
            Assert.AreEqual("Cozy", designer.CreateLayout("pinwheel", "\ufeff Cozy \u00a0").Name);
            Assert.IsTrue(designer.Validate(designer.CreateLayout("pinwheel", new string('a', 48))).Ok);
            Refused(designer.Validate(designer.CreateLayout("pinwheel", new string('a', 49))), "name:");
            Refused(designer.Validate(designer.CreateLayout("pinwheel", "   ")), "name:");
            var layout = Empty(); layout.Name = " Cozy "; Refused(designer.Import(layout.ToJson()), "name:");
        }

        [Test]
        public void HomeDesignExportRoundtripPreservesEscapesRotationsAndFinishes()
        {
            var layout = Empty("courtyard"); layout.Name = "My \"Cozy\" \\ House 🏡";
            var prop = Prop("SOFA", -11, -8, 270); prop.Skin = "Arcade"; layout.Props.Add(prop);
            var export = designer.Export(layout); Assert.IsTrue(export.Ok, string.Join("; ", export.Errors));
            var reload = designer.Import(export.Json); Assert.IsTrue(reload.Ok);
            Assert.AreEqual(layout.ToJson(), reload.Layout.ToJson());
        }

        [Test]
        public void HomeDesignImportRejectsOversizeDeepAndIncompleteDocuments()
        {
            Refused(designer.Import(new string(' ', 65537)), "json:");
            Refused(designer.Import(new string('[', 17) + "0" + new string(']', 17)), "json:");
            Refused(designer.Import("{\"schema\":1,\"map\":\"pinwheel\",\"name\":\"Cozy\",\"props\":null}"), "json:");
            Refused(designer.Import("{\"schema\":1,\"map\":\"pinwheel\",\"name\":\"Cozy\",\"props\":[{}]}"), "json:");
        }

        [Test]
        public void HomeDesignNoSpaceRejectsThatWordWithoutMovingExistingDecor()
        {
            var tiny = InRoom(-.5f, -.5f, .5f, .5f); var layout = Empty(); layout.Props.Add(Prop("APPLE"));
            var before = layout.ToJson(); var result = tiny.AddWords(layout, "APPLE SOFA", "Room");
            Assert.IsTrue(result.Ok); Assert.AreEqual(0, result.Added.Count); Assert.AreEqual(2, result.Rejected.Count);
            Assert.AreEqual(before, result.Layout.ToJson()); Assert.AreEqual(before, layout.ToJson());
        }
    }
}
