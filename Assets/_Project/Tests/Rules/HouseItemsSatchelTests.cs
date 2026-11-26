using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class HouseItemsSatchelTests
    {
        const string Id = "SATCHEL";
        const int HouseRecipes = 78, SharedRecipes = 24, ManifestEntries = 55, ModelBudget = 6000;
        static readonly string House = TestData.Read("items_house.json");
        static readonly HouseTuning Tuning = HouseTuning.FromJson(House);
        static readonly HelperBook Helpers = HelperBook.FromJson(House);
        static readonly BagRowBook Bags = BagRowBook.FromJson(House);
        static readonly ItemCatalogue Items = Tuned();

        static ItemCatalogue Tuned()
        {
            var items = TestData.Catalogue();
            items.Expand(ItemCatalogue.FromJson(House, "items_house.json"));
            Tuning.Apply(items, TestData.LoadRuleBook());
            return items;
        }

        static ItemDefinition Satchel => Items.Get(Id);

        static JsonNode[] Manifest() =>
            Json.Parse(File.ReadAllText(Path.Combine(TestData.Root, "ArtSource/Collections/HouseItemsV2/manifest.json")), "manifest.json")["entries"].Items.ToArray();

        [Test]
        public void TheSatchelIsNewHouseOnlyAndCraftable()
        {
            var shared = TestData.Catalogue();
            var house = ItemCatalogue.FromJson(House, "items_house.json");
            Assert.AreEqual(HouseRecipes, house.All.Count, "77 earlier House recipes plus the satchel");
            Assert.AreEqual(SharedRecipes + HouseRecipes, Items.Enabled.Count(), "24 shared recipes plus 78 House recipes");
            Assert.IsFalse(shared.TryGet(Id, out _), "the satchel stays out of items.json");
            Assert.IsFalse(shared.All.Any(i => i.LegacyWords.Contains(Id)), "no legacy alias");
            Assert.IsTrue(house.TryGet(Id, out var authored) && authored.Enabled, "authored in items_house.json");
            Assert.AreEqual("Items/" + Id, Satchel.Model);
            Assert.IsEmpty(Items.Validate(10).Where(p => p.StartsWith(Id + ":")));
            Assert.AreEqual(SurfaceMaterial.Soft, SurfaceMaterials.ForWord(Id), "a soft bag thud");
        }

        [Test]
        public void ItIsAnEpicWordWithNoTraits()
        {
            Assert.AreEqual(14, RecipeDifficulty.Score(Id), "S1 A1 T1 C3 H4 E1 L1 plus 2 for seven letters");
            Assert.AreEqual(RecipeTier.Epic, RecipeDifficulty.TierOf(Id));
            Assert.IsEmpty(Tuning.TraitsOf(Id), "the bag carries no combat trait");
            Assert.IsEmpty(Tuning.Validate(Items, TestData.LoadRuleBook().Defaults));
        }

        [Test]
        public void EveryHouseCanSupplyIt()
        {
            string folder = Path.Combine(TestData.Root, "Assets/_Project/Data/Config");
            foreach (string file in Directory.GetFiles(folder, "house_*.json"))
            {
                var layout = HouseLayout.FromJson(File.ReadAllText(file), Path.GetFileName(file));
                var available = new LetterBag();
                foreach (var room in layout.Rooms) available.Add(layout.LettersIn(room.Name));
                Assert.IsTrue(available.Contains(Satchel.Letters), layout.Name + " cannot supply " + Id);
            }
        }

        [Test]
        public void TheWordStaysClearOfFurnitureAndScarceLettersAndFitsTheSmallBag()
        {
            string folder = Path.Combine(TestData.Root, "Assets/_Project/Data/Config");
            var furniture = Directory.GetFiles(folder, "house_*.json").SelectMany(f => HouseLayout.FromJson(File.ReadAllText(f), Path.GetFileName(f)).Furniture)
                .Select(f => f.Word).ToHashSet();
            Assert.IsFalse(furniture.Contains(Id), "not a furniture word");
            Assert.IsFalse(Id.Contains("BELT"));
            Assert.IsFalse(Id.Any(c => "JQYZGUWVX".IndexOf(c) >= 0), "avoids letters the houses barely hold");
            Assert.LessOrEqual(Id.Count(c => "DFKM".IndexOf(c) >= 0), 1);
            Assert.LessOrEqual(Id.Length, BagRows.BaseCells, "spelled from the plain ten-slot bag");
            Assert.LessOrEqual(Id.Length, Items.Enabled.Where(i => i.Id != Id).Max(i => i.Id.Length), "no longer than the longest card word already shown");
        }

        [Test]
        public void ItIsABagUpgradeAndNotAToyThatActsAlone()
        {
            var item = Satchel;
            Assert.AreEqual(HandlingFamily.Helper, item.Family, "the stat-less family the shared catalogue accepts");
            Assert.IsTrue(item.Consumable, "used up the moment it is spelled");
            Assert.AreEqual(1, item.Hands);
            Assert.IsNull(item.Melee);
            Assert.IsNull(item.Thrown);
            Assert.IsNull(item.Shield);
            Assert.IsNull(item.Deploy);
            Assert.IsNull(item.Use);
            Assert.IsNull(item.Ranged);
            Assert.IsFalse(Helpers.TryGet(Id, out _), "no helper block");
            Assert.IsTrue(Bags.TryGet(Id, out var spec), "a bag block");
            Assert.AreEqual(1, spec.Rows);
            Assert.AreEqual(BagRows.MostBonusRows, spec.Rows);
            CollectionAssert.IsEmpty(Bags.Validate(Items, Helpers));
            CollectionAssert.IsEmpty(Helpers.Validate(Items, Bags));
            Assert.AreEqual(BagRows.CardLine, Bags.CardLine(item));
            var sheet = RecipeSheet.Build(item, Tuning, Helpers, Bags);
            Assert.AreEqual(BagRows.Kind, sheet.Kind);
            Assert.AreEqual("No hand", sheet.Rows.Single(r => r.Label == RecipeSheet.Hand).Value);
            CollectionAssert.AreEqual(new[] { BagRows.Chip }, RecipeFacts.Chips(item, Tuning, Bags).Select(c => c.Label));
            Assert.IsTrue(BagRowBook.Offered(HouseModes.Dibs));
            Assert.IsTrue(BagRowBook.Offered(HouseModes.Escape));
            Assert.IsFalse(BagRowBook.Offered("Hub"), "not in the Hub");
        }

        [Test]
        public void BotsNeverSpellItAndNoListAsksForIt()
        {
            var role = SpellChoice.RoleOf(Satchel);
            Assert.AreEqual(SpellRole.Gadget, role);
            Assert.IsFalse(SpellChoice.BotWants(role, SpellNeeds.Unarmed));
            Assert.IsFalse(SpellChoice.BotWants(role, new SpellNeeds(true, false, false)));
            Assert.IsNull(ShoppingList.KindOf(Satchel), "never a Spell the List goal");
        }

        [Test]
        public void TheModelIsTheFiftyFifthV2EntryAndMatchesTheCatalogue()
        {
            var entries = Manifest();
            Assert.AreEqual(ManifestEntries, entries.Length, "55 V2 models");
            var entry = entries[ManifestEntries - 1];
            Assert.AreEqual(Id, entry["word"].String());
            var slots = entry["materials"].Strings();
            Assert.That(slots.Count, Is.InRange(3, 5), "three to five materials");
            Assert.LessOrEqual(entry["maxTriangles"].Int(), ModelBudget);
            Assert.AreEqual("scarlet_Classic", entry["main"].String(), "a vivid red school satchel");
            Assert.AreEqual("cobalt_Classic", entry["accent"].String());
            CollectionAssert.Contains(slots, "yellow_Classic", "gold buckles");
            var size = entry["size"].Floats(3);
            var grip = entry["grip"].Floats(3);
            for (int axis = 0; axis < 3; axis++)
            {
                Assert.AreEqual(size[axis], Satchel.Size[axis], 1e-4f);
                Assert.AreEqual(grip[axis], Satchel.Grip[axis], 1e-4f);
            }
            Assert.AreEqual(entry["heldScale"].Float(), Satchel.HeldScale, 1e-4f);
            Assert.Greater(grip[1], size[1] * .9f, "held by the handle on top");
            Assert.Less(grip[2], 0f, "the handle sits behind the letter tiles");
        }

        [Test]
        public void ItLooksApartFromBoxesAndBooksAndHasAHoldRow()
        {
            var palette = ItemPalette.FromJson(File.ReadAllText(Path.Combine(TestData.Root, ItemPalette.SourceFile)), "item_palette.json");
            Assert.IsTrue(palette.Items.ContainsKey(Id), "a palette row");
            Assert.AreEqual("scarlet", palette.MainFamily(Id));
            foreach (string other in new[] { "BOX", "CRATE", "CHEST", "BOOK", "BOOT", "SOCK" })
                Assert.AreNotEqual(palette.MainFamily(other), palette.MainFamily(Id), other);
            Assert.IsTrue(palette.Similar.Any(g => g.Contains(Id) && g.Contains("BOX")), "grouped with the box-like things");
            var hold = ItemHolds.For(Id);
            Assert.IsNotNull(hold, "a hold row so no pose code special-cases it");
            CollectionAssert.AreEqual(new[] { 0f, 180f, 0f }, hold.Turn);
            Assert.IsNull(hold.Grip, "the catalogue grip on the handle applies");
            CollectionAssert.IsEmpty(ItemHolds.Validate(Items).Where(p => p.StartsWith(Id + ":")));
        }
    }
}
