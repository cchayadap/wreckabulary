using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class HouseItemsBatchTwoTests
    {
        static readonly string[] BatchOne = { "RAKE", "TEAPOT", "LID", "TOMATO", "ROCKET", "MARBLE", "LADDER", "PADDLE", "CHILI", "DISC" };
        static readonly string[] Words = { "HELMET", "LEMON", "TORCH", "CARPET", "SLIME", "SPOON", "TENT", "PEAR", "SOCK", "HOSE" };
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

        static bool Lands(ItemDefinition item) => item.Use == null && item.Deploy == null && (item.Shield != null || item.Melee != null || item.Thrown != null);

        [Test]
        public void TheBatchIsNewHouseOnlyAndCraftable()
        {
            var shared = TestData.Catalogue();
            var house = ItemCatalogue.FromJson(TestData.Read("items_house.json"), "items_house.json");
            Assert.GreaterOrEqual(house.All.Count, 43, "23 earlier House recipes plus at least two batches of ten");
            foreach (string id in Words)
            {
                Assert.IsFalse(shared.TryGet(id, out _), id + " stays out of items.json");
                Assert.IsFalse(shared.All.Any(i => i.LegacyWords.Contains(id)), id + " is no legacy alias");
                Assert.IsTrue(house.TryGet(id, out var authored) && authored.Enabled, id + " is authored in items_house.json");
                Assert.IsTrue(Get(id).Enabled, id);
                Assert.AreEqual("Items/" + id, Get(id).Model, id);
                Assert.IsEmpty(Items.Validate(10).Where(p => p.StartsWith(id + ":")), id);
                Assert.IsTrue(SurfaceMaterials.TryForWord(id, out _), id + " has an impact sound");
            }
        }

        [TestCase("HELMET", RecipeTier.Rare), TestCase("LEMON", RecipeTier.Uncommon), TestCase("TORCH", RecipeTier.Rare), TestCase("CARPET", RecipeTier.Rare),
         TestCase("SLIME", RecipeTier.Uncommon), TestCase("SPOON", RecipeTier.Uncommon), TestCase("TENT", RecipeTier.Common), TestCase("PEAR", RecipeTier.Common),
         TestCase("SOCK", RecipeTier.Rare), TestCase("HOSE", RecipeTier.Uncommon)]
        public void ItemsThatHitOrGuardCarryOneEffectPerTierAndOthersNone(string id, RecipeTier tier)
        {
            Assert.AreEqual(tier, RecipeDifficulty.TierOf(id), id);
            int expected = Lands(Get(id)) ? RecipeDifficulty.TraitBudget(tier) : 0;
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
            Assert.IsEmpty(Tuning.Validate(Items, TestData.LoadRuleBook().Defaults));
        }

        [Test]
        public void TheBatchPlaysTheRolesItsCardsDescribe()
        {
            foreach (string id in new[] { "TORCH", "SPOON", "SOCK" }) Assert.AreEqual(SpellRole.Weapon, SpellChoice.RoleOf(Get(id)), id);
            Assert.AreEqual(SpellRole.Shot, SpellChoice.RoleOf(Get("LEMON")));
            Assert.AreEqual(SpellRole.Shield, SpellChoice.RoleOf(Get("HELMET")));
            Assert.AreEqual(SpellRole.Heal, SpellChoice.RoleOf(Get("PEAR")));
            foreach (string id in new[] { "CARPET", "SLIME", "TENT", "HOSE" }) Assert.AreEqual(SpellRole.Gadget, SpellChoice.RoleOf(Get(id)), id);
            Assert.IsTrue(BotTactics.Swings(Get("TORCH")));
            Assert.IsTrue(BotTactics.Swings(Get("SPOON")));
            Assert.IsTrue(BotTactics.Throws(Get("LEMON")));
            Assert.IsTrue(BotTactics.Throws(Get("SOCK")));
        }

        [Test]
        public void HarderWordsBeatTheEasierItemsTheyResemble()
        {
            var carpet = Get("CARPET").Deploy;
            var mat = Get("MAT").Deploy;
            Assert.AreEqual(DeployEffect.SpeedStrip, carpet.Effect);
            Assert.Greater(carpet.FootprintZ, 2f * mat.FootprintZ, "the strip runs along its length, more than twice as far as MAT");
            Assert.Greater(carpet.Strength, mat.Strength);
            var hose = Get("HOSE").Deploy;
            var fan = Get("FAN").Deploy;
            Assert.AreEqual(DeployEffect.WindField, hose.Effect);
            Assert.Greater(hose.Radius, fan.Radius, "a water jet reaches further than a fan");
            Assert.Greater(hose.Strength, fan.Strength);
            Assert.Less(hose.ArcDegrees, fan.ArcDegrees, "but only along a narrow line");
            var slime = Get("SLIME").Deploy;
            var clock = Get("CLOCK").Deploy;
            Assert.AreEqual(DeployEffect.SlowField, slime.Effect);
            Assert.Less(slime.Strength, clock.Strength, "goo holds harder than a clock");
            Assert.Less(slime.Radius, clock.Radius, "but the Uncommon patch is smaller");
            Assert.Less(slime.LifetimeSeconds, clock.LifetimeSeconds, "and gone sooner than the Epic CLOCK");
            Assert.Less(Get("PEAR").Use.ChannelSeconds, Get("MUG").Use.ChannelSeconds, "a bite is quicker than a drink");
            Assert.Less(Get("PEAR").Use.ChannelSeconds, Get("APPLE").Use.ChannelSeconds);
            Assert.Less(Get("PEAR").Use.Amount, Get("APPLE").Use.Amount);
            Assert.Greater(Tuning.Trait("HELMET", TraitKind.Guard).Amount, Tuning.Trait("MIRROR", TraitKind.Guard).Amount);
            Assert.Less(Tuning.Trait("HELMET", TraitKind.Guard).Amount, Tuning.Trait("SHIELD", TraitKind.Guard).Amount);
            Assert.Greater(Get("HELMET").Shield.MoveSpeedMultiplier, Get("SHIELD").Shield.MoveSpeedMultiplier);
            Assert.Less(Get("TENT").Durability, Get("CRATE").Durability, "a play tent is light cover");
            Assert.AreEqual(1, Get("TENT").Hands);
            Assert.IsTrue(Get("LEMON").Consumable && !Get("LEMON").Thrown.Lob);
            Assert.IsTrue(Get("SOCK").Thrown.Recoverable && !Get("SOCK").Consumable);
        }

        [Test]
        public void HeldTraitsAndBuildUpsLandTheWayTheCardsSay()
        {
            Assert.AreEqual(TraitDelivery.BuildUp, Tuning.Trait("TORCH", TraitKind.Daze).Delivery);
            Assert.AreEqual(2, Tuning.Trait("TORCH", TraitKind.Daze).Every, "every second jab dazzles");
            foreach (string id in new[] { "HELMET", "TORCH", "SOCK" })
                Assert.AreEqual(TraitDelivery.WhileHeld, Tuning.Trait(id, TraitKind.Swift).Delivery, id);
            Assert.AreEqual(1.05f, Tuning.Trait("HELMET", TraitKind.Swift).Amount, 1e-5f);
            Assert.AreEqual(1.1f, Tuning.Trait("SOCK", TraitKind.Swift).Amount, 1e-5f);
            Assert.AreEqual(0.5f, Tuning.Trait("LEMON", TraitKind.Daze).Chance, 1e-5f);
            Assert.AreEqual(1.2f, Tuning.Trait("SPOON", TraitKind.Heavy).Amount, 1e-5f);
            Assert.AreEqual("while held: -12% damage · while held: +5% speed", Tuning.Describe("HELMET"));
        }

        [Test]
        public void CardsShowTheBatchEffects()
        {
            string[] Chips(string id) => RecipeFacts.Chips(Get(id), Tuning).Select(c => c.Label).ToArray();
            CollectionAssert.AreEqual(new[] { "Guard", "Swift" }, Chips("HELMET"));
            CollectionAssert.AreEqual(new[] { "Daze 50%" }, Chips("LEMON"));
            CollectionAssert.AreEqual(new[] { "Daze 2nd", "Swift" }, Chips("TORCH"));
            CollectionAssert.AreEqual(new[] { "Speed strip" }, Chips("CARPET"));
            CollectionAssert.AreEqual(new[] { "Slow field" }, Chips("SLIME"));
            CollectionAssert.AreEqual(new[] { "Heavy" }, Chips("SPOON"));
            CollectionAssert.AreEqual(new[] { "Cover" }, Chips("TENT"));
            CollectionAssert.AreEqual(new[] { "+15 HP" }, Chips("PEAR"));
            CollectionAssert.AreEqual(new[] { "Daze 50%", "Swift" }, Chips("SOCK"));
            CollectionAssert.AreEqual(new[] { "Wind" }, Chips("HOSE"));
        }

        [Test]
        public void TheModelManifestListsTheBatchAfterBatchOne()
        {
            var entries = Json.Parse(File.ReadAllText(Path.Combine(TestData.Root, "ArtSource/Collections/HouseItemsV2/manifest.json")), "manifest.json")["entries"].Items.ToArray();
            CollectionAssert.AreEqual(BatchOne.Concat(Words), entries.Select(e => e["word"].String()).Take(BatchOne.Length + Words.Length));
            foreach (var entry in entries.Skip(BatchOne.Length).Take(Words.Length))
            {
                var slots = entry["materials"].Strings();
                Assert.GreaterOrEqual(slots.Count, 3, entry["word"].String() + " has at least three materials");
                Assert.LessOrEqual(entry["maxTriangles"].Int(), Get(entry["word"].String()).Thrown != null || Get(entry["word"].String()).Use != null ? 3000 : 6000,
                    entry["word"].String() + " stays inside the small-item budget when it is thrown or eaten");
            }
        }
    }
}
