using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class HouseItemsBatchOneTests
    {
        static readonly string[] Words = { "RAKE", "TEAPOT", "LID", "TOMATO", "ROCKET", "MARBLE", "LADDER", "PADDLE", "CHILI", "DISC" };
        static readonly HouseTuning Tuning = HouseTuning.FromJson(TestData.Read("items_house.json"));
        static readonly ItemCatalogue Items = Tuned();

        static ItemCatalogue Tuned()
        {
            var items = TestData.Catalogue();
            items.Expand(ItemCatalogue.FromJson(TestData.Read("items_house.json"), "items_house.json"));
            Tuning.Apply(items, TestData.LoadRuleBook());
            return items;
        }

        static ItemDefinition Get(string id) => Items.Get(id);

        static bool Hits(ItemDefinition item) => item.Use == null && item.Deploy == null && item.Shield == null && (item.Melee != null || item.Thrown != null);

        [Test]
        public void TheBatchIsNewHouseOnlyAndCraftable()
        {
            var shared = TestData.Catalogue();
            var house = ItemCatalogue.FromJson(TestData.Read("items_house.json"), "items_house.json");
            foreach (string id in Words)
            {
                Assert.IsFalse(shared.TryGet(id, out _), id + " stays out of items.json");
                Assert.IsTrue(house.TryGet(id, out var authored) && authored.Enabled, id + " is authored in items_house.json");
                Assert.IsTrue(Get(id).Enabled, id);
                Assert.AreEqual("Items/" + id, Get(id).Model, id);
                Assert.IsEmpty(Items.Validate(10).Where(p => p.StartsWith(id + ":")), id);
                Assert.IsTrue(SurfaceMaterials.TryForWord(id, out _), id + " has an impact sound");
            }
        }

        [TestCase("RAKE", RecipeTier.Uncommon), TestCase("TEAPOT", RecipeTier.Uncommon), TestCase("LID", RecipeTier.Common), TestCase("TOMATO", RecipeTier.Uncommon),
         TestCase("ROCKET", RecipeTier.Epic), TestCase("MARBLE", RecipeTier.Rare), TestCase("LADDER", RecipeTier.Uncommon), TestCase("PADDLE", RecipeTier.Rare),
         TestCase("CHILI", RecipeTier.Rare), TestCase("DISC", RecipeTier.Uncommon)]
        public void HitItemsCarryOneEffectPerTierAndOthersNone(string id, RecipeTier tier)
        {
            Assert.AreEqual(tier, RecipeDifficulty.TierOf(id), id);
            var item = Get(id);
            int expected = Hits(item) ? RecipeDifficulty.TraitBudget(tier) : 0;
            Assert.AreEqual(expected, Tuning.TraitsOf(id).Count, id + " carries as many effects as its difficulty allows");
        }

        [Test]
        public void EveryHouseCanSupplyTheBatch()
        {
            string folder = Path.Combine(TestData.Root, "Assets/_Project/Data/Config");
            foreach (string file in Directory.GetFiles(folder, "house_*.json"))
            {
                var layout = HouseLayout.FromJson(File.ReadAllText(file), Path.GetFileName(file));
                var available = new LetterBag();
                foreach (var room in layout.Rooms) available.Add(layout.LettersIn(room.Name));
                foreach (string id in Words)
                    Assert.IsTrue(available.Contains(Get(id).Letters), layout.Name + " cannot supply " + id);
            }
        }

        [Test]
        public void StatsStayInsideTheirTierBudgets()
        {
            foreach (string id in Words)
            {
                var item = Get(id);
                var tier = RecipeDifficulty.TierOf(id);
                if (item.Melee != null) Assert.LessOrEqual(Tuning.MeleeDps(item), RecipeDifficulty.MaxMeleeDps(tier), id);
                if (item.Thrown != null) Assert.LessOrEqual(item.Thrown.Damage, RecipeDifficulty.MaxThrownDamage(tier), id);
            }
            var rules = TestData.LoadRuleBook();
            Assert.IsEmpty(Tuning.Validate(Items, rules.Defaults));
        }

        [Test]
        public void TheBatchPlaysTheRolesItsCardsDescribe()
        {
            Assert.AreEqual(SpellRole.Weapon, SpellChoice.RoleOf(Get("RAKE")));
            Assert.AreEqual(SpellRole.Weapon, SpellChoice.RoleOf(Get("PADDLE")));
            Assert.AreEqual(SpellRole.Weapon, SpellChoice.RoleOf(Get("DISC")));
            Assert.AreEqual(SpellRole.Shot, SpellChoice.RoleOf(Get("TOMATO")));
            Assert.AreEqual(SpellRole.Shot, SpellChoice.RoleOf(Get("ROCKET")));
            Assert.AreEqual(SpellRole.Shield, SpellChoice.RoleOf(Get("LID")));
            Assert.AreEqual(SpellRole.Heal, SpellChoice.RoleOf(Get("TEAPOT")));
            Assert.AreEqual(SpellRole.Boost, SpellChoice.RoleOf(Get("CHILI")));
            Assert.AreEqual(SpellRole.Gadget, SpellChoice.RoleOf(Get("MARBLE")));
            Assert.AreEqual(SpellRole.Gadget, SpellChoice.RoleOf(Get("LADDER")));
            Assert.IsTrue(BotTactics.Swings(Get("RAKE")));
            Assert.IsTrue(BotTactics.Throws(Get("ROCKET")));
        }

        [Test]
        public void HarderWordsBeatTheEasierItemsTheyResemble()
        {
            Assert.Greater(Get("TEAPOT").Use.Amount, Get("APPLE").Use.Amount);
            Assert.Less(Get("TEAPOT").Use.Amount, Get("CAKE").Use.Amount);
            Assert.Greater(Get("CHILI").Use.Amount, Get("SODA").Use.Amount);
            Assert.Greater(Get("MARBLE").Deploy.Radius, Get("SOAP").Deploy.Radius);
            Assert.Greater(Get("MARBLE").Deploy.LifetimeSeconds, Get("SOAP").Deploy.LifetimeSeconds);
            Assert.Greater(Get("LADDER").Deploy.Strength, Get("BED").Deploy.Strength);
            Assert.Greater(Get("LID").Shield.MoveSpeedMultiplier, Get("MIRROR").Shield.MoveSpeedMultiplier, "a plain lid is lighter to carry");
            Assert.Less(Get("LID").Shield.DamageReduction, Get("MIRROR").Shield.DamageReduction, "but it blocks less");
            Assert.GreaterOrEqual(Get("ROCKET").Thrown.Damage, Get("BOMB").Thrown.Damage, "the Epic rocket is never weaker than the Rare bomb");
            Assert.AreEqual(0f, Get("ROCKET").Thrown.Radius, "the rocket hits directly so its effects land");
            Assert.IsTrue(Get("DISC").Thrown.Recoverable);
            Assert.IsTrue(Get("TOMATO").Consumable && Get("TOMATO").Thrown.Lob);
        }

        [Test]
        public void CardsShowTheBatchEffects()
        {
            string[] Chips(string id) => RecipeFacts.Chips(Get(id), Tuning).Select(c => c.Label).ToArray();
            CollectionAssert.AreEqual(new[] { "Daze 3rd" }, Chips("RAKE"));
            CollectionAssert.AreEqual(new[] { "Heavy", "Daze 40%" }, Chips("PADDLE"));
            CollectionAssert.AreEqual(new[] { "Heavy", "Daze 50%" }, Chips("ROCKET"));
            CollectionAssert.AreEqual(new[] { "Daze 40%" }, Chips("TOMATO"));
            CollectionAssert.AreEqual(new[] { "Daze 30%" }, Chips("DISC"));
            CollectionAssert.AreEqual(new[] { "+35 HP" }, Chips("TEAPOT"));
            CollectionAssert.AreEqual(new[] { "Speed boost" }, Chips("CHILI"));
            CollectionAssert.AreEqual(new[] { "Block front" }, Chips("LID"));
            CollectionAssert.AreEqual(new[] { "Slippery" }, Chips("MARBLE"));
            CollectionAssert.AreEqual(new[] { "Jump pad" }, Chips("LADDER"));
            StringAssert.Contains("bleeds 2/s for 2 s", Tuning.Describe("ROCKET"));
        }

        static JsonNode[] Manifest() =>
            Json.Parse(File.ReadAllText(Path.Combine(TestData.Root, "ArtSource/Collections/HouseItemsV2/manifest.json")), "manifest.json")["entries"].Items.ToArray();

        static (float hue, float saturation, float value) Colour(string material)
        {
            var library = Json.Parse(File.ReadAllText(Path.Combine(TestData.Root, "Assets/_Project/Data/Generated/materials.json")), "materials.json");
            var spec = library["materials"].Items.Single(m => m["name"].String() == material);
            var c = spec["baseColor"].Floats(4).Take(3).Select(Gamma).ToArray();
            float max = c.Max(), min = c.Min(), d = max - min;
            float hue = d <= 0f ? 0f : max == c[0] ? 60f * ((c[1] - c[2]) / d % 6f) : max == c[1] ? 60f * ((c[2] - c[0]) / d + 2f) : 60f * ((c[0] - c[1]) / d + 4f);
            return ((hue + 360f) % 360f, max <= 0f ? 0f : d / max, max);
        }

        static float Gamma(float linear) => linear <= .0031308f ? 12.92f * linear : 1.055f * (float)System.Math.Pow(linear, 1 / 2.4) - .055f;

        [Test]
        public void TheModelManifestMatchesTheCatalogueRows()
        {
            var entries = Manifest();
            CollectionAssert.AreEqual(Words, entries.Select(e => e["word"].String()).Take(Words.Length), "batch 1 leads the HouseItemsV2 manifest");
            var library = Json.Parse(File.ReadAllText(Path.Combine(TestData.Root, "Assets/_Project/Data/Generated/materials.json")), "materials.json");
            var names = library["materials"].Items.Select(m => m["name"].String()).ToList();
            foreach (var entry in entries)
            {
                var item = Get(entry["word"].String());
                var size = entry["size"].Floats(3);
                var grip = entry["grip"].Floats(3);
                for (int axis = 0; axis < 3; axis++)
                {
                    Assert.AreEqual(size[axis], item.Size[axis], 1e-4f, item.Id + " size");
                    Assert.AreEqual(grip[axis], item.Grip[axis], 1e-4f, item.Id + " grip");
                }
                Assert.AreEqual(entry["heldScale"].Float(), item.HeldScale, 1e-4f, item.Id);
                Assert.AreEqual(size.Max(), entry["targetLongest"].Float(), 1e-4f, item.Id);
                Assert.LessOrEqual(entry["maxTriangles"].Int(), 6000, item.Id + " stays inside the vault item budget");
                if (item.Deploy != null)
                {
                    Assert.AreEqual(size[0], item.Deploy.FootprintX, 1e-4f, item.Id);
                    Assert.AreEqual(size[2], item.Deploy.FootprintZ, 1e-4f, item.Id);
                }
                var slots = entry["materials"].Strings();
                Assert.LessOrEqual(slots.Count, 5, item.Id);
                foreach (string slot in slots)
                {
                    StringAssert.EndsWith("_Classic", slot, item.Id);
                    CollectionAssert.Contains(names, slot, item.Id + " uses a library material");
                    foreach (string finish in new[] { "_Candy", "_Arcade" })
                        CollectionAssert.Contains(names, slot.Replace("_Classic", finish), item.Id + " keeps every finish");
                }
                CollectionAssert.Contains(slots, entry["main"].String(), item.Id);
                CollectionAssert.Contains(slots, entry["accent"].String(), item.Id);
            }
        }

        [Test]
        public void MainColoursAreVividAndAccentsStandApart()
        {
            foreach (var entry in Manifest())
            {
                string id = entry["word"].String();
                var main = Colour(entry["main"].String());
                var accent = Colour(entry["accent"].String());
                Assert.That(main.saturation, Is.InRange(.6f, .85f), id + " main saturation");
                Assert.That(main.value, Is.InRange(.7f, .92f), id + " main value");
                Assert.GreaterOrEqual(accent.saturation, .7f, id + " accent saturation");
                float gap = System.Math.Abs(main.hue - accent.hue);
                Assert.GreaterOrEqual(System.Math.Min(gap, 360f - gap), 60f, id + " accent hue sits at least 60 degrees from the main colour");
            }
            var mains = Manifest().ToDictionary(e => e["word"].String(), e => e["main"].String());
            Assert.AreNotEqual("red_Classic", mains["TOMATO"], "TOMATO must not wear APPLE's red");
            Assert.AreNotEqual("red_Classic", mains["CHILI"], "CHILI must not wear SODA's red");
        }
    }
}
