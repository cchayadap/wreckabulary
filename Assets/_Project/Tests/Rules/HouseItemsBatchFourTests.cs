using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class HouseItemsBatchFourTests
    {
        const int EarlierManifestEntries = 30;
        static readonly string[] Words = { "ANCHOR", "POTION", "FENCE", "CAMERA", "LATTE", "PLANE", "PAINT", "CASTLE", "MALLET", "CRAB" };
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
            Assert.AreEqual(63, house.All.Count, "23 earlier House recipes plus four batches of ten");
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

        [TestCase("ANCHOR", RecipeTier.Rare), TestCase("POTION", RecipeTier.Uncommon), TestCase("FENCE", RecipeTier.Rare), TestCase("CAMERA", RecipeTier.Rare),
         TestCase("LATTE", RecipeTier.Common), TestCase("PLANE", RecipeTier.Uncommon), TestCase("PAINT", RecipeTier.Uncommon), TestCase("CASTLE", RecipeTier.Uncommon),
         TestCase("MALLET", RecipeTier.Uncommon), TestCase("CRAB", RecipeTier.Uncommon)]
        public void ItemsThatHitCarryOneEffectPerTierAndOthersNone(string id, RecipeTier tier)
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
        public void TheWordsStayClearOfFurnitureAndScarceLetters()
        {
            string folder = Path.Combine(TestData.Root, "Assets/_Project/Data/Config");
            var furniture = Directory.GetFiles(folder, "house_*.json").SelectMany(f => HouseLayout.FromJson(File.ReadAllText(f), Path.GetFileName(f)).Furniture)
                .Select(f => f.Word).ToHashSet();
            foreach (string id in Words)
            {
                Assert.IsFalse(furniture.Contains(id), id + " is not a furniture word");
                Assert.AreNotEqual("BELT", id);
                Assert.That(id.Length, Is.InRange(3, 7), id);
                Assert.IsFalse(id.Any(c => "JQYZGUWVX".IndexOf(c) >= 0), id + " avoids letters the houses barely hold");
                Assert.LessOrEqual(id.Count(c => "DFKM".IndexOf(c) >= 0), 1, id + " uses at most one of D, F, K and M");
            }
            Assert.LessOrEqual(Words.Max(w => w.Length), Items.Enabled.Max(i => i.Id.Length), "no batch word is longer than the longest recipe card word already shown");
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
            foreach (string id in new[] { "ANCHOR", "CAMERA", "MALLET", "PLANE", "CRAB" }) Assert.AreEqual(SpellRole.Weapon, SpellChoice.RoleOf(Get(id)), id);
            Assert.AreEqual(SpellRole.Heal, SpellChoice.RoleOf(Get("POTION")), "a shield bubble counts as a heal, like FOAM");
            Assert.AreEqual(SpellRole.Boost, SpellChoice.RoleOf(Get("LATTE")));
            foreach (string id in new[] { "FENCE", "PAINT", "CASTLE" }) Assert.AreEqual(SpellRole.Gadget, SpellChoice.RoleOf(Get(id)), id);
            foreach (string id in new[] { "ANCHOR", "CAMERA", "MALLET" }) Assert.IsTrue(BotTactics.Swings(Get(id)), id);
            foreach (string id in new[] { "PLANE", "CRAB" }) Assert.IsTrue(BotTactics.Throws(Get(id)), id);
        }

        [Test]
        public void HarderWordsBeatTheEasierItemsTheyResemble()
        {
            var anchor = Get("ANCHOR");
            var mallet = Get("MALLET");
            Assert.Greater(Tuning.MeleeDps(anchor), Tuning.MeleeDps(mallet), "the Rare anchor out-hits the Uncommon mallet");
            Assert.Greater(Tuning.MeleeLifetime(anchor), Tuning.MeleeLifetime(Get("BROOM")), "and outlasts the best Uncommon swing");
            Assert.Less(Tuning.MeleeDps(anchor), Tuning.MeleeDps(Get("HAMMER")), "but stays below the Epic HAMMER");
            Assert.Greater(anchor.Melee.Cycle, 1f, "an anchor swings slowly");
            Assert.Greater(anchor.Melee.Damage, Get("HAMMER").Melee.Damage * 0.9f, "and hits nearly as hard as HAMMER per blow");
            Assert.Greater(mallet.Melee.Cycle, 1f, "a mallet swings slowly too");
            Assert.Greater(mallet.Melee.Damage, Get("SPOON").Melee.Damage);
            Assert.Less(Tuning.MeleeDps(mallet), Tuning.MeleeDps(Get("TORCH")), "an Uncommon swing stays below every Rare one");
            var camera = Get("CAMERA");
            Assert.Less(camera.Melee.Cycle, 0.6f, "a camera bops quickly");
            Assert.Less(camera.Melee.Damage, anchor.Melee.Damage);
            Assert.GreaterOrEqual(Tuning.MeleeDps(camera), Tuning.MeleeDps(Get("BLADE")), "a Rare swing never hits softer than the best Uncommon BLADE");
            Assert.IsTrue(Get("PLANE").Thrown.Recoverable && !Get("PLANE").Consumable);
            Assert.IsFalse(Get("PLANE").Thrown.Lob, "the glider flies flat");
            Assert.Greater(Get("PLANE").Thrown.Speed, Get("DISC").Thrown.Speed, "and faster than DISC");
            Assert.IsTrue(Get("CRAB").Thrown.Recoverable && !Get("CRAB").Consumable);
            Assert.Greater(Get("CRAB").Thrown.Damage, Get("BEAR").Thrown.Damage, "a pinching crab lands harder than the teddy");
            var potion = Get("POTION").Use;
            var foam = Get("FOAM").Use;
            Assert.AreEqual(UseEffect.Bubble, potion.Effect);
            Assert.Less(potion.Amount, foam.Amount, "a potion bubble is smaller than FOAM's");
            Assert.Less(potion.Seconds, foam.Seconds);
            Assert.Less(potion.ChannelSeconds, foam.ChannelSeconds, "but quicker to drink");
            var latte = Get("LATTE").Use;
            Assert.AreEqual(UseEffect.Speed, latte.Effect);
            Assert.Less(latte.Amount, Get("SODA").Use.Amount, "a latte is a gentler boost than SODA");
            Assert.Less(latte.Seconds, Get("TONIC").Use.Seconds);
            var fence = Get("FENCE");
            Assert.AreEqual(DeployEffect.Cover, fence.Deploy.Effect);
            Assert.AreEqual(1, fence.Hands, "a picket fence goes down in one hand");
            Assert.Greater(fence.Deploy.FootprintX, Get("CRATE").Deploy.FootprintX, "a fence is wider than a crate");
            Assert.Less(fence.Size[1], Get("SHELF").Size[1], "and lower than a shelf");
            Assert.Greater(fence.Deploy.FootprintX, fence.Deploy.FootprintZ * 4f, "wide and thin");
            var castle = Get("CASTLE");
            Assert.AreEqual(DeployEffect.Cover, castle.Deploy.Effect);
            Assert.AreEqual(2, castle.Hands);
            Assert.Greater(castle.Durability, Get("CRATE").Durability, "a toy castle is sturdier than a crate");
            Assert.Less(castle.Durability, Get("PIANO").Durability, "but not as sturdy as PIANO");
            var paint = Get("PAINT").Deploy;
            var soap = Get("SOAP").Deploy;
            Assert.AreEqual(DeployEffect.SlipZone, paint.Effect);
            Assert.IsTrue(Get("PAINT").Consumable, "spilling the tin spends it");
            Assert.Greater(paint.Radius, soap.Radius, "wet paint spreads wider than SOAP");
            Assert.Greater(paint.LifetimeSeconds, soap.LifetimeSeconds);
            Assert.Less(paint.Radius, Get("MARBLE").Deploy.Radius, "but less than the Rare MARBLE");
        }

        [Test]
        public void TraitsLandTheWayTheCardsSay()
        {
            Assert.AreEqual(1.5f, Tuning.Trait("ANCHOR", TraitKind.Heavy).Amount, 1e-5f);
            Assert.AreEqual(TraitDelivery.BuildUp, Tuning.Trait("ANCHOR", TraitKind.Crush).Delivery);
            Assert.AreEqual(3, Tuning.Trait("ANCHOR", TraitKind.Crush).Every);
            Assert.AreEqual(1.75f, Tuning.Trait("ANCHOR", TraitKind.Crush).Amount, 1e-5f);
            Assert.AreEqual(TraitDelivery.EveryHit, Tuning.Trait("CAMERA", TraitKind.Daze).Delivery, "the flash dazes on every hit");
            Assert.AreEqual(TraitDelivery.WhileHeld, Tuning.Trait("CAMERA", TraitKind.Swift).Delivery);
            Assert.AreEqual(1.05f, Tuning.Trait("CAMERA", TraitKind.Swift).Amount, 1e-5f);
            Assert.AreEqual(0.3f, Tuning.Trait("PLANE", TraitKind.Daze).Chance, 1e-5f);
            Assert.AreEqual(TraitDelivery.BuildUp, Tuning.Trait("MALLET", TraitKind.Crush).Delivery);
            Assert.AreEqual(2, Tuning.Trait("MALLET", TraitKind.Crush).Every);
            Assert.AreEqual(1.5f, Tuning.Trait("MALLET", TraitKind.Crush).Amount, 1e-5f);
            Assert.AreEqual(TraitDelivery.OverTime, Tuning.Trait("CRAB", TraitKind.Bleed).Delivery, "the pinch stings for a moment");
            Assert.AreEqual("bleeds 1/s for 2 s", Tuning.Describe("CRAB"));
            foreach (string id in new[] { "POTION", "FENCE", "LATTE", "PAINT", "CASTLE" })
                Assert.IsEmpty(Tuning.TraitsOf(id), id + " is plain");
        }

        [Test]
        public void CardsShowTheBatchEffects()
        {
            string[] Chips(string id) => RecipeFacts.Chips(Get(id), Tuning).Select(c => c.Label).ToArray();
            CollectionAssert.AreEqual(new[] { "Heavy", "Crush 3rd" }, Chips("ANCHOR"));
            CollectionAssert.AreEqual(new[] { "Bubble" }, Chips("POTION"));
            CollectionAssert.AreEqual(new[] { "Cover" }, Chips("FENCE"));
            CollectionAssert.AreEqual(new[] { "Daze", "Swift" }, Chips("CAMERA"));
            CollectionAssert.AreEqual(new[] { "Speed boost" }, Chips("LATTE"));
            CollectionAssert.AreEqual(new[] { "Daze 30%" }, Chips("PLANE"));
            CollectionAssert.AreEqual(new[] { "Slippery" }, Chips("PAINT"));
            CollectionAssert.AreEqual(new[] { "Cover" }, Chips("CASTLE"));
            CollectionAssert.AreEqual(new[] { "Crush 2nd" }, Chips("MALLET"));
            CollectionAssert.AreEqual(new[] { "Bleed" }, Chips("CRAB"));
        }

        [Test]
        public void TheModelManifestListsTheBatchAfterTheFirstThree()
        {
            var entries = Json.Parse(File.ReadAllText(Path.Combine(TestData.Root, "ArtSource/Collections/HouseItemsV2/manifest.json")), "manifest.json")["entries"].Items.ToArray();
            CollectionAssert.AreEqual(Words, entries.Skip(EarlierManifestEntries).Take(Words.Length).Select(e => e["word"].String()));
            foreach (var entry in entries.Skip(EarlierManifestEntries).Take(Words.Length))
            {
                string id = entry["word"].String();
                var slots = entry["materials"].Strings();
                Assert.GreaterOrEqual(slots.Count, 3, id + " has at least three materials");
                Assert.LessOrEqual(entry["maxTriangles"].Int(), Get(id).Thrown != null || Get(id).Use != null ? 3000 : 6000,
                    id + " stays inside the small-item budget when it is thrown or drunk");
            }
        }

        [Test]
        public void PotionAndTonicLookAndActDifferently()
        {
            var palette = ItemPalette.FromJson(File.ReadAllText(Path.Combine(TestData.Root, ItemPalette.SourceFile)), "item_palette.json");
            Assert.AreNotEqual(palette.MainFamily("TONIC"), palette.MainFamily("POTION"));
            Assert.AreNotEqual(Get("TONIC").Use.Effect, Get("POTION").Use.Effect);
            Assert.Less(Get("POTION").Size[1] / Get("POTION").Size[0], Get("TONIC").Size[1] / Get("TONIC").Size[0], "a round flask is squatter than the tall tonic bottle");
        }
    }
}
