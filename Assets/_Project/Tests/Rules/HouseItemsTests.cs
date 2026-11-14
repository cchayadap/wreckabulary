using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class HouseItemsTests
    {
        const string SharedCatalogueSha = "79363c8f1f14e7aca553718b552468d34b3140fe4ba88688fd4ded8330cf2416";

        static ItemCatalogue House() => ItemCatalogue.FromJson(TestData.Read("items_house.json"), "items_house.json");

        static ItemCatalogue Expanded()
        {
            var catalogue = TestData.Catalogue();
            catalogue.Expand(House());
            return catalogue;
        }

        [Test]
        public void SharedCatalogueBytesStayFixedForWordboundAndOnline()
        {
            using var sha = SHA256.Create();
            var bytes = File.ReadAllBytes(Path.Combine(TestData.Root, "Assets/_Project/Data/Config/items.json"));
            string hash = string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
            Assert.AreEqual(SharedCatalogueSha, hash, "Add House recipes to items_house.json; items.json is bound by public Wordbound catalogue hashes.");
        }

        [Test]
        public void HouseItemsAreCompleteAndValidInEveryMode()
        {
            var house = House();
            Assert.IsTrue(house.All.Count > 0);
            Assert.IsTrue(house.All.All(i => i.Enabled));
            var catalogue = Expanded();
            foreach (var mode in TestData.LoadRuleBook().Modes)
                CollectionAssert.IsEmpty(catalogue.Validate(TestData.RulesFor(mode).MaxLetters), mode);
            foreach (var item in house.All.Where(i => i.Deploy != null))
            {
                Assert.AreEqual(item.Size[0], item.Deploy.FootprintX, 1e-4f, item.Id);
                Assert.AreEqual(item.Size[2], item.Deploy.FootprintZ, 1e-4f, item.Id);
            }
        }

        [Test]
        public void HouseItemsOnlyAddWordsOrEnableDisabledOnes()
        {
            var shared = TestData.Catalogue();
            foreach (var item in House().All)
                Assert.IsFalse(shared.TryGet(item.Id, out var existing) && existing.Enabled, item.Id + " would replace an enabled shared item");
            var catalogue = Expanded();
            Assert.AreEqual(shared.Enabled.Count() + House().All.Count, catalogue.Enabled.Count());
            Assert.IsTrue(shared.Enabled.All(i => ReferenceEquals(i, shared.Get(i.Id))));
        }

        [Test]
        public void ExpandRefusesToReplaceAnEnabledItemOrAddADisabledOne()
        {
            var catalogue = TestData.Catalogue();
            var bat = new ItemCatalogue();
            bat.Add(new ItemDefinition { Id = "BAT", Enabled = true });
            Assert.Throws<ArgumentException>(() => catalogue.Expand(bat));
            var off = new ItemCatalogue();
            off.Add(new ItemDefinition { Id = "KITE", Enabled = false });
            Assert.Throws<ArgumentException>(() => catalogue.Expand(off));
        }

        [Test]
        public void EveryHouseCanSupplyEveryHouseItem()
        {
            string folder = Path.Combine(TestData.Root, "Assets/_Project/Data/Config");
            var house = House();
            foreach (string file in Directory.GetFiles(folder, "house_*.json"))
            {
                var layout = HouseLayout.FromJson(File.ReadAllText(file), Path.GetFileName(file));
                var available = new LetterBag();
                foreach (var room in layout.Rooms) available.Add(layout.LettersIn(room.Name));
                foreach (var item in house.All)
                    Assert.IsTrue(available.Contains(item.Letters), layout.Name + " cannot supply " + item.Id);
            }
        }

        [Test]
        public void HouseMeleeStaysInsideTheShippedEnvelope()
        {
            foreach (var item in House().All.Where(i => i.Melee != null))
            {
                Assert.LessOrEqual(MeleeFairness.DamagePerSecond(item.Melee), MeleeFairness.MaxDamagePerSecond, item.Id);
                Assert.LessOrEqual(MeleeFairness.Threat(item.Melee), MeleeFairness.MaxThreat, item.Id);
                Assert.LessOrEqual(item.Melee.Reach, MeleeFairness.MaxSwingReach + 0.7f, item.Id);
            }
        }

        [Test]
        public void KenneyItemsMatchTheirImportedModels()
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestData.Root, "ArtSource/Collections/HouseItemsV1/manifest.json")));
            var house = House();
            foreach (var entry in manifest.RootElement.GetProperty("entries").EnumerateArray())
            {
                var item = house.Get(entry.GetProperty("word").GetString());
                Assert.AreEqual("Items/" + item.Id, item.Model);
                Assert.AreEqual(entry.GetProperty("heldScale").GetSingle(), item.HeldScale, 1e-4f, item.Id);
                var size = entry.GetProperty("size").EnumerateArray().Select(v => v.GetSingle()).ToArray();
                var grip = entry.GetProperty("grip").EnumerateArray().Select(v => v.GetSingle()).ToArray();
                for (int axis = 0; axis < 3; axis++)
                {
                    Assert.AreEqual(size[axis], item.Size[axis], 1e-4f, item.Id + " size");
                    Assert.AreEqual(grip[axis], item.Grip[axis], 1e-4f, item.Id + " grip");
                }
            }
        }

        [Test]
        public void HouseWordsHaveAMaterialSound()
        {
            foreach (var item in House().All)
                Assert.IsTrue(SurfaceMaterials.TryForWord(item.Id, out _), item.Id + " needs a surface material in SurfaceMaterials");
        }
    }
}
