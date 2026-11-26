using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class HouseItemsFlipTests
    {
        const int EarlierManifestEntries = 50;
        static readonly string[] Words = { "SPINNER", "FLIPPER", "HOOP", "COMPASS" };
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

        static ItemTrait Reverse(string id) => Tuning.Trait(id, TraitKind.Reverse);

        [Test]
        public void TheFourAreNewHouseOnlyAndCraftable()
        {
            var shared = TestData.Catalogue();
            var house = ItemCatalogue.FromJson(TestData.Read("items_house.json"), "items_house.json");
            Assert.GreaterOrEqual(house.All.Count, 77, "73 earlier House recipes, the helpers included, plus the four that reverse directions");
            Assert.AreEqual(24 + house.All.Count, Items.Enabled.Count(), "24 shared recipes plus every House recipe");
            foreach (string id in Words)
            {
                Assert.IsFalse(shared.TryGet(id, out _), id + " stays out of items.json");
                Assert.IsFalse(shared.All.Any(i => i.LegacyWords.Contains(id)), id + " is no legacy alias");
                Assert.IsTrue(house.TryGet(id, out var authored) && authored.Enabled, id + " is authored in items_house.json");
                Assert.AreEqual("Items/" + id, Get(id).Model, id);
                Assert.IsEmpty(Items.Validate(10).Where(p => p.StartsWith(id + ":")), id);
                Assert.IsTrue(SurfaceMaterials.TryForWord(id, out _), id + " has an impact sound");
            }
        }

        [TestCase("SPINNER", RecipeTier.Rare), TestCase("FLIPPER", RecipeTier.Epic), TestCase("HOOP", RecipeTier.Uncommon), TestCase("COMPASS", RecipeTier.Epic)]
        public void EachCarriesAReversalWithinItsDifficulty(string id, RecipeTier tier)
        {
            Assert.AreEqual(tier, RecipeDifficulty.TierOf(id), id);
            Assert.IsNotNull(Reverse(id), id + " reverses directions");
            Assert.LessOrEqual(Tuning.TraitsOf(id).Count, RecipeDifficulty.TraitBudget(tier), id);
        }

        [Test]
        public void EveryHouseCanSupplyTheFour()
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
                Assert.IsFalse(id.Contains("BELT"), id);
                Assert.That(id.Length, Is.InRange(3, 7), id);
                Assert.IsFalse(id.Any(c => "JQYZGUWVX".IndexOf(c) >= 0), id + " avoids letters the houses barely hold");
                Assert.LessOrEqual(id.Count(c => "DFKM".IndexOf(c) >= 0), 1, id + " uses at most one of D, F, K and M");
            }
            Assert.LessOrEqual(Words.Max(w => w.Length), Items.Enabled.Where(i => !Words.Contains(i.Id)).Max(i => i.Id.Length), "no new word is longer than the longest card word already shown");
        }

        [Test]
        public void StatsStayInsideTheirTierBudgetsAndBeatEasierWords()
        {
            foreach (string id in Words)
            {
                var item = Get(id);
                var tier = RecipeDifficulty.TierOf(id);
                if (item.Melee != null) Assert.LessOrEqual(Tuning.MeleeDps(item), RecipeDifficulty.MaxMeleeDps(tier), id);
                if (item.Thrown != null) Assert.LessOrEqual(item.Thrown.Damage, RecipeDifficulty.MaxThrownDamage(tier), id);
            }
            Assert.IsEmpty(Tuning.Validate(Items, TestData.LoadRuleBook().Defaults));
            var flipper = Get("FLIPPER");
            Assert.GreaterOrEqual(Tuning.MeleeDps(flipper), Tuning.MeleeDps(Get("PADDLE")), "the Epic flipper never slaps softer than the Rare paddle");
            Assert.Less(flipper.Melee.Cycle, 0.6f, "a flipper slaps quickly");
            Assert.GreaterOrEqual(Get("SPINNER").Thrown.Damage, Get("BARREL").Thrown.Damage, "a Rare top lands at least as hard as the best Uncommon throw");
            Assert.IsTrue(Get("SPINNER").Thrown.Recoverable && !Get("SPINNER").Consumable, "the top can be picked up again");
            Assert.IsFalse(Get("HOOP").Consumable, "the hoop is put down, not spent");
            Assert.Greater(Get("HOOP").Deploy.Radius, Get("HOOP").Deploy.FootprintX, "the zone reaches past the hoop itself so stepping near it counts");
            Assert.Less(Get("COMPASS").Shield.FrontArcDegrees, 359f, "the compass blocks only in front");
        }

        [Test]
        public void EachDeliveryFamilyIsProvenOnce()
        {
            Assert.AreEqual(HandlingFamily.Thrown, Get("SPINNER").Family);
            Assert.AreEqual(TraitDelivery.EveryHit, Reverse("SPINNER").Delivery, "the top reverses whoever it hits");
            Assert.AreEqual(HandlingFamily.MeleeSwing, Get("FLIPPER").Family);
            Assert.AreEqual(TraitDelivery.BuildUp, Reverse("FLIPPER").Delivery);
            Assert.AreEqual(3, Reverse("FLIPPER").Every, "every third slap flips");
            Assert.AreEqual(HandlingFamily.DeployZone, Get("HOOP").Family);
            Assert.AreEqual(TraitDelivery.InZone, Reverse("HOOP").Delivery);
            Assert.AreEqual(HandlingFamily.Shield, Get("COMPASS").Family);
            Assert.AreEqual(TraitDelivery.WhileRaised, Reverse("COMPASS").Delivery);
            CollectionAssert.AreEquivalent(new[] { ReverseMode.Mirror, ReverseMode.Flip, ReverseMode.Turnabout }, Words.Select(w => Reverse(w).Turn).Distinct(),
                "every mode is used at least once");
            Assert.AreEqual(ReverseMode.Mirror, Reverse("SPINNER").Turn);
            Assert.AreEqual(ReverseMode.Flip, Reverse("FLIPPER").Turn);
        }

        [Test]
        public void ReversalsAreShortAndHonourTheirCaps()
        {
            foreach (string id in new[] { "SPINNER", "FLIPPER" })
            {
                var t = Reverse(id);
                var others = Tuning.TraitsOf(id).Where(o => o != t && o.OnHit).Select(o => o.Kind);
                Assert.That(t.Seconds, Is.InRange(Reversal.MinHitSeconds, Reversal.MaxSeconds(RecipeDifficulty.TierOf(id), others)), id + " lasts a few seconds");
            }
            Assert.LessOrEqual(Reverse("HOOP").Seconds, Reversal.MaxZoneTailSeconds, "the hoop lets go soon after stepping out");
            Assert.LessOrEqual(Reverse("COMPASS").Seconds, Reversal.MaxRaisedTailSeconds);
            Assert.That(Reverse("COMPASS").Radius, Is.InRange(1f, Reversal.MaxRaisedRadius));
            Assert.Less(Reverse("SPINNER").Seconds, Reversal.BaseSeconds(RecipeTier.Rare), "the top is shortened by its knockback");
        }

        [Test]
        public void TheBatchPlaysTheRolesItsCardsDescribe()
        {
            foreach (string id in new[] { "SPINNER", "FLIPPER" }) Assert.AreEqual(SpellRole.Weapon, SpellChoice.RoleOf(Get(id)), id);
            Assert.AreEqual(SpellRole.Shield, SpellChoice.RoleOf(Get("COMPASS")));
            Assert.AreEqual(SpellRole.Gadget, SpellChoice.RoleOf(Get("HOOP")));
            Assert.IsTrue(BotTactics.Throws(Get("SPINNER")));
            Assert.IsTrue(BotTactics.Swings(Get("FLIPPER")));
        }

        [Test]
        public void CardsSayWhatGetsSwapped()
        {
            string[] Chips(string id) => RecipeFacts.Chips(Get(id), Tuning).Select(c => c.Label).ToArray();
            CollectionAssert.AreEqual(new[] { "Mirror", "Heavy" }, Chips("SPINNER"));
            CollectionAssert.AreEqual(new[] { "Flip 3rd", "Heavy" }, Chips("FLIPPER"));
            CollectionAssert.AreEqual(new[] { "Reverse" }, Chips("HOOP"));
            CollectionAssert.AreEqual(new[] { "Reverse", "Guard" }, Chips("COMPASS"));
            Assert.IsTrue(RecipeFacts.Chips(Get("HOOP"), Tuning).All(c => c.Kind == FactKind.Reverse));
            StringAssert.StartsWith("swaps left and right for 3 s", Tuning.Describe("SPINNER"));
            StringAssert.StartsWith("every 3rd hit swaps forward and back for 3 s", Tuning.Describe("FLIPPER"));
            Assert.AreEqual("inside: reverses every direction, and for 1 s after", Tuning.Describe("HOOP"));
            StringAssert.StartsWith("while raised: reverses every direction for you and everyone within 3 m", Tuning.Describe("COMPASS"));
        }

        [Test]
        public void EnumsTheOnlineCodeComparesKeepTheirValues()
        {
            CollectionAssert.AreEqual(new[] { "Cover", "JumpPad", "SpeedStrip", "SlipZone", "WindField", "SlowField", "Stash" }, Enum.GetNames(typeof(DeployEffect)));
            CollectionAssert.AreEqual(new[] { "None", "Bubble", "Heal", "Speed" }, Enum.GetNames(typeof(UseEffect)));
            CollectionAssert.AreEqual(new[] { "Unarmed", "Melee", "Thrown", "Explosion", "Hazard", "Bleed" }, Enum.GetNames(typeof(HitSource)));
            CollectionAssert.AreEqual(new[] { "Objective", "Weapon", "Shot", "Shield", "Heal", "Boost", "Gadget" }, Enum.GetNames(typeof(SpellRole)));
            Assert.AreEqual(TraitKind.Reverse, Enum.GetValues(typeof(TraitKind)).Cast<TraitKind>().Last(), "the house-only trait is appended last");
            Assert.AreEqual(ReverseMode.None, default(ReverseMode), "an unparsed trait reverses nothing");
        }

        [Test]
        public void TheModelManifestListsTheFourAfterTheHelpers()
        {
            var entries = Json.Parse(File.ReadAllText(Path.Combine(TestData.Root, "ArtSource/Collections/HouseItemsV2/manifest.json")), "manifest.json")["entries"].Items.ToArray();
            Assert.GreaterOrEqual(entries.Length, EarlierManifestEntries + Words.Length, "the four follow the fifty earlier V2 models");
            CollectionAssert.AreEqual(Words, entries.Skip(EarlierManifestEntries).Take(Words.Length).Select(e => e["word"].String()));
            foreach (var entry in entries.Skip(EarlierManifestEntries).Take(Words.Length))
            {
                string id = entry["word"].String();
                var slots = entry["materials"].Strings();
                Assert.That(slots.Count, Is.InRange(3, 5), id + " has three to five materials");
                Assert.LessOrEqual(entry["maxTriangles"].Int(), Get(id).Thrown != null ? 3000 : 4500, id + " stays inside its triangle budget");
            }
        }
    }
}
