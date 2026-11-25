using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class HouseItemsBatchThreeTests
    {
        const int EarlierManifestEntries = 20;
        static readonly string[] Words = { "RACKET", "MELON", "PIANO", "TONIC", "BRICK", "PENCIL", "RAMP", "OIL", "DOOR", "BOOT" };
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
            Assert.AreEqual(53, house.All.Count, "23 earlier House recipes plus three batches of ten");
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

        [TestCase("RACKET", RecipeTier.Epic), TestCase("MELON", RecipeTier.Uncommon), TestCase("PIANO", RecipeTier.Uncommon), TestCase("TONIC", RecipeTier.Uncommon),
         TestCase("BRICK", RecipeTier.Epic), TestCase("PENCIL", RecipeTier.Rare), TestCase("RAMP", RecipeTier.Uncommon), TestCase("OIL", RecipeTier.Common),
         TestCase("DOOR", RecipeTier.Common), TestCase("BOOT", RecipeTier.Common)]
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
            foreach (string id in new[] { "RACKET", "PENCIL", "BRICK", "BOOT" }) Assert.AreEqual(SpellRole.Weapon, SpellChoice.RoleOf(Get(id)), id);
            Assert.AreEqual(SpellRole.Shot, SpellChoice.RoleOf(Get("MELON")));
            Assert.AreEqual(SpellRole.Shield, SpellChoice.RoleOf(Get("DOOR")));
            Assert.AreEqual(SpellRole.Boost, SpellChoice.RoleOf(Get("TONIC")));
            foreach (string id in new[] { "PIANO", "RAMP", "OIL" }) Assert.AreEqual(SpellRole.Gadget, SpellChoice.RoleOf(Get(id)), id);
            Assert.IsTrue(BotTactics.Swings(Get("RACKET")));
            Assert.IsTrue(BotTactics.Swings(Get("PENCIL")));
            foreach (string id in new[] { "MELON", "BRICK", "BOOT" }) Assert.IsTrue(BotTactics.Throws(Get(id)), id);
        }

        [Test]
        public void HarderWordsBeatTheEasierItemsTheyResemble()
        {
            Assert.Greater(Tuning.MeleeDps(Get("RACKET")), Tuning.MeleeDps(Get("PADDLE")), "the Epic racket out-hits the Rare paddle");
            Assert.Greater(Tuning.MeleeLifetime(Get("RACKET")), Tuning.MeleeLifetime(Get("PADDLE")));
            Assert.Greater(Get("PENCIL").Melee.Reach, Get("TORCH").Melee.Reach, "a long pencil jabs from further than a flashlight");
            Assert.Greater(Get("BRICK").Thrown.Damage, Get("BOOT").Thrown.Damage);
            Assert.GreaterOrEqual(Get("BRICK").Thrown.Damage, Get("BOMB").Thrown.Damage, "an Epic throw never hits softer than the Rare BOMB");
            Assert.IsTrue(Get("BRICK").Thrown.Recoverable && !Get("BRICK").Consumable);
            Assert.IsTrue(Get("BOOT").Thrown.Recoverable && !Get("BOOT").Consumable);
            Assert.IsTrue(Get("MELON").Consumable && Get("MELON").Thrown.Lob);
            Assert.Greater(Get("MELON").Thrown.Damage, Get("TOMATO").Thrown.Damage, "a melon lands harder than a tomato");
            var tonic = Get("TONIC").Use;
            var soda = Get("SODA").Use;
            Assert.AreEqual(UseEffect.Speed, tonic.Effect);
            Assert.Greater(tonic.Seconds, soda.Seconds, "the tonic lasts longer than SODA");
            Assert.Less(tonic.Amount, Get("CHILI").Use.Amount, "but runs slower than the Rare CHILI");
            var piano = Get("PIANO");
            Assert.AreEqual(DeployEffect.Cover, piano.Deploy.Effect);
            Assert.AreEqual(2, piano.Hands);
            Assert.Greater(piano.Durability, Get("CRATE").Durability, "a piano is sturdier than a crate");
            Assert.Greater(piano.Deploy.FootprintX, Get("CRATE").Deploy.FootprintX * 0.9f);
            var ramp = Get("RAMP").Deploy;
            Assert.AreEqual(DeployEffect.JumpPad, ramp.Effect);
            Assert.Greater(ramp.Strength, Get("STOOL").Deploy.Strength);
            Assert.Greater(ramp.Strength, Get("BED").Deploy.Strength);
            Assert.AreEqual(1, Get("RAMP").Hands, "a toy ramp goes down in one hand");
            var oil = Get("OIL").Deploy;
            var soap = Get("SOAP").Deploy;
            Assert.AreEqual(DeployEffect.SlipZone, oil.Effect);
            Assert.IsTrue(Get("OIL").Consumable, "pouring the can spends it");
            Assert.Less(oil.Radius, soap.Radius, "a little oil spill is smaller than SOAP's");
            Assert.LessOrEqual(oil.LifetimeSeconds, soap.LifetimeSeconds);
            var door = Get("DOOR");
            Assert.AreEqual(2, door.Hands);
            Assert.Greater(door.Shield.FrontArcDegrees, Get("LID").Shield.FrontArcDegrees, "a door covers more than a pot lid");
            Assert.Less(door.Shield.FrontArcDegrees, Get("SHIELD").Shield.FrontArcDegrees, "but only from the front, unlike the Rare SHIELD");
            Assert.Greater(door.Durability, Get("HELMET").Durability);
            Assert.Less(door.Shield.MoveSpeedMultiplier, Get("LID").Shield.MoveSpeedMultiplier, "a door is heavy to carry");
        }

        [Test]
        public void TraitsLandTheWayTheCardsSay()
        {
            Assert.AreEqual(TraitDelivery.WhileHeld, Tuning.Trait("RACKET", TraitKind.Swift).Delivery);
            Assert.AreEqual(1.08f, Tuning.Trait("RACKET", TraitKind.Swift).Amount, 1e-5f);
            Assert.AreEqual(0.35f, Tuning.Trait("RACKET", TraitKind.Daze).Chance, 1e-5f);
            Assert.AreEqual(1.3f, Tuning.Trait("RACKET", TraitKind.Heavy).Amount, 1e-5f);
            Assert.AreEqual(1.3f, Tuning.Trait("MELON", TraitKind.Heavy).Amount, 1e-5f);
            Assert.AreEqual(0.25f, Tuning.Trait("BRICK", TraitKind.Daze).Chance, 1e-5f);
            Assert.AreEqual(TraitDelivery.OverTime, Tuning.Trait("BRICK", TraitKind.Bleed).Delivery);
            Assert.IsNull(Tuning.Trait("BRICK", TraitKind.Crush), "a crush is a melee build-up, so a thrown brick stings instead");
            Assert.AreEqual(TraitDelivery.OverTime, Tuning.Trait("PENCIL", TraitKind.Bleed).Delivery);
            Assert.AreEqual(TraitDelivery.WhileHeld, Tuning.Trait("PENCIL", TraitKind.Swift).Delivery);
            Assert.AreEqual("bleeds 1.5/s for 2 s · while held: +5% speed", Tuning.Describe("PENCIL"));
            foreach (string id in new[] { "PIANO", "TONIC", "RAMP", "OIL", "DOOR", "BOOT" })
                Assert.IsEmpty(Tuning.TraitsOf(id), id + " is plain");
        }

        [Test]
        public void CardsShowTheBatchEffects()
        {
            string[] Chips(string id) => RecipeFacts.Chips(Get(id), Tuning).Select(c => c.Label).ToArray();
            CollectionAssert.AreEqual(new[] { "Heavy", "Daze 35%" }, Chips("RACKET"));
            CollectionAssert.AreEqual(new[] { "Heavy" }, Chips("MELON"));
            CollectionAssert.AreEqual(new[] { "Cover" }, Chips("PIANO"));
            CollectionAssert.AreEqual(new[] { "Speed boost" }, Chips("TONIC"));
            CollectionAssert.AreEqual(new[] { "Heavy", "Daze 25%" }, Chips("BRICK"));
            CollectionAssert.AreEqual(new[] { "Bleed", "Swift" }, Chips("PENCIL"));
            CollectionAssert.AreEqual(new[] { "Jump pad" }, Chips("RAMP"));
            CollectionAssert.AreEqual(new[] { "Slippery" }, Chips("OIL"));
            CollectionAssert.AreEqual(new[] { "Block front" }, Chips("DOOR"));
            CollectionAssert.AreEqual(new[] { "Throw" }, Chips("BOOT"));
        }

        [Test]
        public void TheModelManifestListsTheBatchAfterTheFirstTwo()
        {
            var entries = Json.Parse(File.ReadAllText(Path.Combine(TestData.Root, "ArtSource/Collections/HouseItemsV2/manifest.json")), "manifest.json")["entries"].Items.ToArray();
            CollectionAssert.AreEqual(Words, entries.Skip(EarlierManifestEntries).Select(e => e["word"].String()));
            foreach (var entry in entries.Skip(EarlierManifestEntries))
            {
                string id = entry["word"].String();
                var slots = entry["materials"].Strings();
                Assert.GreaterOrEqual(slots.Count, 3, id + " has at least three materials");
                Assert.LessOrEqual(entry["maxTriangles"].Int(), Get(id).Thrown != null || Get(id).Use != null ? 3000 : 6000,
                    id + " stays inside the small-item budget when it is thrown or drunk");
            }
        }
    }
}
