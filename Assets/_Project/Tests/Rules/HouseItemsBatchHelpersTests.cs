using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class HouseItemsBatchHelpersTests
    {
        const int EarlierManifestEntries = 40;
        const int EarlierHouseRecipes = 63;
        static readonly string[] Words = { "ROBOT", "RABBIT", "TERRIER", "BLASTER", "CHOMPER", "PATROL", "CONE", "BLOCKS", "SPIKES", "TRAP" };
        static readonly string[] Helpers = { "ROBOT", "RABBIT", "TERRIER", "BLASTER", "CHOMPER", "PATROL", "SPIKES", "TRAP" };
        static readonly string[] Walls = { "CONE", "BLOCKS" };
        static readonly HouseTuning Tuning = HouseTuning.FromJson(TestData.Read("items_house.json"));
        static readonly HelperBook Book = HelperBook.FromJson(TestData.Read("items_house.json"));
        static readonly ItemCatalogue Items = Tuned();

        static ItemCatalogue Tuned()
        {
            var items = TestData.Catalogue();
            items.Expand(ItemCatalogue.FromJson(TestData.Read("items_house.json"), "items_house.json"));
            Tuning.Apply(items, TestData.LoadRuleBook());
            return items;
        }

        static ItemDefinition Get(string id) => Items.Get(id);

        static HelperSpec Spec(string id)
        {
            Assert.IsTrue(Book.TryGet(id, out var spec), id + " has a helper block");
            return spec;
        }

        [Test]
        public void TheBatchIsNewHouseOnlyAndCraftable()
        {
            var shared = TestData.Catalogue();
            var house = ItemCatalogue.FromJson(TestData.Read("items_house.json"), "items_house.json");
            Assert.GreaterOrEqual(house.All.Count, EarlierHouseRecipes + Words.Length, "23 earlier House recipes, four batches of ten and the ten helpers");
            foreach (string id in Words)
            {
                Assert.IsFalse(shared.TryGet(id, out _), id + " stays out of items.json");
                Assert.IsFalse(shared.All.Any(i => i.LegacyWords.Contains(id)), id + " is no legacy alias");
                Assert.IsTrue(house.TryGet(id, out var authored) && authored.Enabled, id + " is authored in items_house.json");
                Assert.AreEqual("Items/" + id, Get(id).Model, id);
                Assert.IsEmpty(Items.Validate(10).Where(p => p.StartsWith(id + ":")), id);
                Assert.IsTrue(SurfaceMaterials.TryForWord(id, out _), id + " has an impact sound");
                Assert.IsNotNull(ItemHolds.For(id), id + " has a hold row");
                Assert.IsEmpty(Tuning.TraitsOf(id), id + " acts through its helper block or wall, not hit traits");
            }
        }

        [TestCase("ROBOT", RecipeTier.Uncommon), TestCase("RABBIT", RecipeTier.Rare), TestCase("TERRIER", RecipeTier.Uncommon), TestCase("BLASTER", RecipeTier.Rare),
         TestCase("CHOMPER", RecipeTier.Epic), TestCase("PATROL", RecipeTier.Uncommon), TestCase("CONE", RecipeTier.Common), TestCase("BLOCKS", RecipeTier.Epic),
         TestCase("SPIKES", RecipeTier.Epic), TestCase("TRAP", RecipeTier.Common)]
        public void EachWordHasThePlannedDifficulty(string id, RecipeTier tier) => Assert.AreEqual(tier, RecipeDifficulty.TierOf(id), id);

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
            Assert.LessOrEqual(Words.Max(w => w.Length), Items.Enabled.Where(i => !Words.Contains(i.Id)).Max(i => i.Id.Length),
                "no batch word is longer than the longest recipe card word already shown");
        }

        [Test]
        public void HelpersAreOneHandedConsumablesThatActOnlyThroughTheirBlock()
        {
            foreach (string id in Helpers)
            {
                var item = Get(id);
                Assert.AreEqual(HandlingFamily.Helper, item.Family, id);
                Assert.IsTrue(item.Consumable, id + " is used up when it is let go");
                Assert.AreEqual(1, item.Hands, id);
                Assert.IsTrue(item.Melee == null && item.Thrown == null && item.Deploy == null && item.Use == null && item.Shield == null && item.Ranged == null,
                    id + " carries no other stats block");
                Assert.AreEqual(SpellRole.Gadget, SpellChoice.RoleOf(item), id + " is a gadget for the bots");
            }
            CollectionAssert.IsSubsetOf(Helpers, Book.All.Select(s => s.Id), "every helper of the batch has its block");
            Assert.IsEmpty(Book.Validate(Items), "the batch passes HelperBook.Validate, the caps and the no-inversion rule");
            Assert.IsEmpty(Tuning.Validate(Items, TestData.LoadRuleBook().Defaults), "and HouseTuning.Validate");
        }

        [TestCase("ROBOT", HelperBody.Walks, HelperPick.Aimed, HelperBite.Enemies, HelperAttack.Swing, 8f, 3, 10f, 22f)]
        [TestCase("RABBIT", HelperBody.Hops, HelperPick.Nearest, HelperBite.Enemies, HelperAttack.Touch, 7f, 4, 12f, 24f)]
        [TestCase("TERRIER", HelperBody.Walks, HelperPick.Provoker, HelperBite.Enemies, HelperAttack.Swing, 7f, 3, 10f, 20f)]
        [TestCase("BLASTER", HelperBody.Stays, HelperPick.Nearest, HelperBite.Enemies, HelperAttack.Shot, 6f, 5, 18f, 30f)]
        [TestCase("CHOMPER", HelperBody.Hops, HelperPick.Nearest, HelperBite.Anyone, HelperAttack.Swing, 10f, 5, 12f, 30f)]
        [TestCase("PATROL", HelperBody.Walks, HelperPick.Nearest, HelperBite.Enemies, HelperAttack.Touch, 6f, 4, 10f, 24f)]
        [TestCase("SPIKES", HelperBody.Stays, HelperPick.Stepper, HelperBite.Enemies, HelperAttack.Touch, 7f, 6, 20f, 40f)]
        [TestCase("TRAP", HelperBody.Stays, HelperPick.Stepper, HelperBite.Enemies, HelperAttack.Touch, 8f, 1, 12f, 12f)]
        public void EachHelperPlaysThePlannedPart(string id, HelperBody body, HelperPick pick, HelperBite bites, HelperAttack attack, float damage, int strikes,
            float seconds, float health)
        {
            var spec = Spec(id);
            Assert.AreEqual(body, spec.Body, id);
            Assert.AreEqual(pick, spec.Pick, id);
            Assert.AreEqual(bites, spec.Bites, id);
            Assert.AreEqual(attack, spec.Attack, id);
            Assert.AreEqual(damage, spec.Damage, 1e-4f, id);
            Assert.AreEqual(strikes, spec.Strikes, id);
            Assert.AreEqual(seconds, spec.Seconds, 1e-4f, id);
            Assert.AreEqual(health, spec.Health, 1e-4f, id);
            Assert.IsEmpty(HelperCaps.Check(spec), id + " fits the caps of its tier");
        }

        [Test]
        public void TheHelpersBehaveTheWayTheirToysSuggest()
        {
            Assert.Greater(Spec("RABBIT").Speed, Spec("ROBOT").Speed, "a rabbit hops faster than a tin robot marches");
            Assert.AreEqual(HelperCaps.MaxSpeed(RecipeTier.Rare), Spec("RABBIT").Speed, 1e-4f);
            Assert.IsTrue(Spec("TERRIER").HomeIsMaker, "the pup's home is its maker, so it trots at heel");
            Assert.Greater(Spec("BLASTER").Radius, Spec("SPIKES").Radius, "a turret reaches further than a spike mat");
            Assert.Greater(Spec("BLASTER").ShotSpeed, 0f);
            Assert.Less(Spec("PATROL").Leash, Spec("ROBOT").Leash, "a patrol stays round its post");
            Assert.Less(Spec("PATROL").Sight, Spec("ROBOT").Sight);
            Assert.Greater(Spec("CHOMPER").TotalDamage, HelperCaps.MaxTotalDamage(RecipeTier.Epic, HelperBite.Enemies),
                "biting anyone, its maker too, pays for the extra damage");
            Assert.LessOrEqual(Spec("CHOMPER").TotalDamage, HelperCaps.MaxTotalDamage(RecipeTier.Epic, HelperBite.Anyone));
            Assert.IsTrue(Spec("TRAP").Slows && Spec("TRAP").Strikes == 1, "a mousetrap snaps once and holds");
            Assert.IsTrue(Spec("SPIKES").IsTrap && Spec("TRAP").IsTrap);
            Assert.Greater(Spec("SPIKES").Radius, Spec("TRAP").Radius, "the mat covers more floor than the mousetrap");
            Assert.Greater(Spec("SPIKES").Strength, Spec("BLASTER").Strength, "the Epic stayer outdoes the Rare one");
            Assert.Greater(Spec("BLASTER").Strength, Spec("TRAP").Strength);
            Assert.Greater(Spec("CHOMPER").Strength, Spec("RABBIT").Strength, "the Epic mover outdoes the Rare one");
            foreach (string id in new[] { "ROBOT", "TERRIER", "PATROL" })
                Assert.Greater(Spec("RABBIT").Strength, Spec(id).Strength, "the Rare RABBIT outdoes the Uncommon " + id);
        }

        [Test]
        public void WallsAreTimedCoverAndTheHarderWordIsTheBetterWall()
        {
            foreach (string id in Walls)
            {
                var item = Get(id);
                Assert.IsTrue(HelperBook.IsWall(item), id + " is timed cover");
                Assert.IsFalse(item.Consumable, id);
                Assert.AreEqual(DeployEffect.Cover, item.Deploy.Effect, id);
                Assert.AreEqual(item.Size[0], item.Deploy.FootprintX, 1e-4f, id);
                Assert.AreEqual(item.Size[2], item.Deploy.FootprintZ, 1e-4f, id);
                Assert.IsEmpty(HelperCaps.CheckWall(item), id);
                Assert.AreEqual(SpellRole.Gadget, SpellChoice.RoleOf(item), id);
                Assert.IsFalse(Book.TryGet(id, out _), id + " has no helper block");
            }
            var cone = Get("CONE");
            var blocks = Get("BLOCKS");
            Assert.AreEqual(8f, cone.Deploy.LifetimeSeconds, 1e-4f);
            Assert.AreEqual(15f, blocks.Deploy.LifetimeSeconds, 1e-4f);
            Assert.AreEqual(1, cone.Hands);
            Assert.AreEqual(2, blocks.Hands, "a big block wall takes both hands");
            Assert.Less(cone.Size[1], .6f, "cones are low: shots and throws fly over them");
            Assert.Greater(blocks.Size[1], 1.2f, "the block wall is tall enough to stop shots");
            Assert.Greater(HelperCaps.WallStrength(blocks), HelperCaps.WallStrength(cone));
            Assert.Greater(cone.Size[0], cone.Size[2] * 4f, "a barrier is wide and thin");
        }

        [Test]
        public void CardsSayWhatEachItemDoes()
        {
            Assert.AreEqual("Chases the one you aim at", Book.CardLine(Get("ROBOT")));
            Assert.AreEqual("Chases the nearest foe", Book.CardLine(Get("RABBIT")));
            Assert.AreEqual("Follows you and bites whoever hurts you", Book.CardLine(Get("TERRIER")));
            Assert.AreEqual("Guards this spot from foes", Book.CardLine(Get("BLASTER")));
            Assert.AreEqual("Chases anyone near. Even you!", Book.CardLine(Get("CHOMPER")));
            Assert.AreEqual("Chases the nearest foe", Book.CardLine(Get("PATROL")));
            Assert.AreEqual("Snaps at foes who step on it", Book.CardLine(Get("SPIKES")));
            Assert.AreEqual("Snaps at foes who step on it", Book.CardLine(Get("TRAP")));
            Assert.AreEqual("Blocks the way for 8 s", Book.CardLine(Get("CONE")));
            Assert.AreEqual("Blocks the way for 15 s", Book.CardLine(Get("BLOCKS")));
            Assert.AreEqual("8 x 1 · 12 s · 12 health · slows to 60% for 1 s", Spec("TRAP").Details());
            foreach (string id in Walls) CollectionAssert.AreEqual(new[] { "Cover" }, RecipeFacts.Chips(Get(id), Tuning).Select(c => c.Label), id);
        }

        [Test]
        public void ModesOnlyOfferHelpersThatHaveSomeoneToFight()
        {
            foreach (string id in Helpers)
            {
                var spec = Spec(id);
                Assert.IsTrue(HelperBook.AllowedIn(HouseModes.Dibs, spec), id + " in Last One Standing");
                Assert.IsTrue(HelperBook.AllowedIn(HouseModes.War, spec), id + " in Team Battle");
                Assert.AreEqual(spec.IsTrap, HelperBook.AllowedIn(HouseModes.MovingOut, spec), id + ": only traps go to the co-op modes");
                Assert.AreEqual(spec.Bites == HelperBite.Enemies, HelperBook.AllowedIn(HouseModes.Duos, spec), id + ": nothing that bites a partner in Duos");
            }
            foreach (string id in Walls) Assert.IsTrue(HelperBook.AllowedIn(HouseModes.Escape, null), id + " walls work everywhere");
        }

        [Test]
        public void TheModelManifestListsTheBatchAfterTheFirstFour()
        {
            var entries = Json.Parse(File.ReadAllText(Path.Combine(TestData.Root, "ArtSource/Collections/HouseItemsV2/manifest.json")), "manifest.json")["entries"].Items.ToArray();
            CollectionAssert.AreEqual(Words, entries.Skip(EarlierManifestEntries).Take(Words.Length).Select(e => e["word"].String()));
            foreach (var entry in entries.Skip(EarlierManifestEntries).Take(Words.Length))
            {
                string id = entry["word"].String();
                var slots = entry["materials"].Strings();
                Assert.That(slots.Count, Is.InRange(4, 5), id + " has four or five materials");
                Assert.LessOrEqual(entry["maxTriangles"].Int(), 6000, id);
            }
            var warning = entries.Single(e => e["word"].String() == "CHOMPER")["materials"].Strings();
            CollectionAssert.IsSubsetOf(new[] { "scarlet_Classic", "white_Classic" }, warning, "the bite-anyone toy wears red and white warning stripes");
        }

        [Test]
        public void LookAlikesKeepApartInColour()
        {
            var palette = ItemPalette.FromJson(File.ReadAllText(Path.Combine(TestData.Root, ItemPalette.SourceFile)), "item_palette.json");
            Assert.AreNotEqual(palette.MainFamily("BLOCKS"), palette.MainFamily("BRICK"), "a block wall is not the thrown brick");
            Assert.AreNotEqual(palette.MainFamily("RABBIT"), palette.MainFamily("BEAR"));
            Assert.AreNotEqual(palette.MainFamily("CHOMPER"), palette.MainFamily("CRAB"));
            Assert.AreNotEqual(palette.MainFamily("ROBOT"), palette.MainFamily("BLASTER"));
            foreach (string id in Words) Assert.IsTrue(palette.Items.ContainsKey(id), id + " has a palette row");
        }
    }
}
