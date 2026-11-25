using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class SpellChoiceTests
    {
        static ItemCatalogue House()
        {
            var items = TestData.Catalogue();
            items.Expand(ItemCatalogue.FromJson(TestData.Read("items_house.json"), "items_house.json"));
            return items;
        }

        static readonly ItemCatalogue Items = House();
        static readonly SpellNeeds Armed = new SpellNeeds(true, false, false);

        static SpellRole Role(string word) => SpellChoice.RoleOf(Items.Get(word));

        static string[] Order(SpellNeeds needs, params string[] words) => SpellChoice.Order(words, w => w, Role, needs).ToArray();

        [Test]
        public void EveryHouseRecipeHasTheRoleItPlays()
        {
            var expected = new (SpellRole role, string[] ids)[]
            {
                (SpellRole.Weapon, new[] { "BAT", "PAN", "SPEAR", "LAMP", "BLADE", "BROOM", "RACK", "PILLOW", "HAMMER", "ARROW", "BALL", "POT", "TOASTER", "BEAR", "BARREL", "BOOK", "BOW", "RAKE", "PADDLE", "DISC", "TORCH", "SPOON", "SOCK", "RACKET", "BRICK", "PENCIL", "BOOT", "ANCHOR", "CAMERA", "PLANE", "MALLET", "CRAB" }),
                (SpellRole.Shot, new[] { "BOMB", "PIE", "VASE", "TOMATO", "ROCKET", "LEMON", "MELON" }),
                (SpellRole.Shield, new[] { "SHIELD", "PLATE", "MIRROR", "LID", "HELMET", "DOOR" }),
                (SpellRole.Heal, new[] { "APPLE", "WATER", "CAKE", "FOAM", "MUG", "TEAPOT", "PEAR", "POTION" }),
                (SpellRole.Boost, new[] { "SODA", "CHILI", "TONIC", "LATTE" }),
                (SpellRole.Gadget, new[] { "PLANT", "CLOCK", "FAN", "SPEAKER", "SOAP", "MAT", "BED", "SOFA", "TABLE", "CRATE", "SHELF", "DESK", "CABINET", "CHAIR", "STOOL", "BENCH", "BOX", "CHEST", "MARBLE", "LADDER", "CARPET", "SLIME", "TENT", "HOSE", "PIANO", "RAMP", "OIL", "FENCE", "PAINT", "CASTLE" }),
            };
            foreach (var (role, ids) in expected)
                foreach (string id in ids)
                    Assert.AreEqual(role, Role(id), id);
            CollectionAssert.AreEquivalent(Items.Enabled.Select(i => i.Id), expected.SelectMany(e => e.ids), "every spellable House item has a role");
        }

        [Test]
        public void AnEmptyHandGetsAWeaponBeforeCover()
        {
            Assert.Greater(RecipeDifficulty.Score("PLANT"), RecipeDifficulty.Score("PAN"), "PLANT scores higher, which used to make it the default");
            CollectionAssert.AreEqual(new[] { "PAN", "PLANT" }, Order(SpellNeeds.Unarmed, "PLANT", "PAN"));
        }

        [Test]
        public void DeployablesNeverJumpAheadOfSomethingToFightWith()
        {
            CollectionAssert.AreEqual(new[] { "BAT", "SPEAKER", "CLOCK", "FAN" }, Order(SpellNeeds.Unarmed, "FAN", "SPEAKER", "CLOCK", "BAT"));
            Assert.AreEqual("PIE", Order(SpellNeeds.Unarmed, "SPEAKER", "PIE")[0], "even a one-shot throw beats a deployable");
            Assert.AreEqual("BAT", Order(Armed, "SPEAKER", "BAT")[0], "a second weapon still beats a deployable");
            Assert.AreEqual("PLANT", Order(SpellNeeds.Unarmed, "PLANT")[0], "a deployable is offered when it is the only word ready");
        }

        [Test]
        public void HarderWordsComeFirstWithinARole()
        {
            CollectionAssert.AreEqual(new[] { "HAMMER", "SPEAR", "BAT", "BOMB" }, Order(SpellNeeds.Unarmed, "BAT", "BOMB", "HAMMER", "SPEAR"));
        }

        [Test]
        public void AnArmedHandReachesForAShieldNext()
        {
            CollectionAssert.AreEqual(new[] { "SHIELD", "HAMMER", "BOMB", "BAT" }, Order(Armed, "BAT", "SHIELD", "HAMMER", "BOMB"));
            Assert.AreEqual("HAMMER", Order(new SpellNeeds(true, true, false), "PLATE", "HAMMER")[0], "one shield is enough");
        }

        [Test]
        public void AHurtPlayerReachesForAHealFirst()
        {
            var hurt = new SpellNeeds(false, false, true);
            CollectionAssert.AreEqual(new[] { "APPLE", "HAMMER" }, Order(hurt, "HAMMER", "APPLE"));
            CollectionAssert.AreEqual(new[] { "HAMMER", "APPLE" }, Order(SpellNeeds.Unarmed, "HAMMER", "APPLE"));
        }

        [Test]
        public void ChecklistWordsComeFirst()
        {
            var order = SpellChoice.Order(new[] { "HAMMER", "BED" }, w => w, w => w == "BED" ? SpellRole.Objective : Role(w), SpellNeeds.Unarmed);
            CollectionAssert.AreEqual(new[] { "BED", "HAMMER" }, order);
        }

        [Test]
        public void BotsOnlySpellWhatTheyCanFightWith()
        {
            var unarmed = Items.Enabled.Where(i => SpellChoice.BotWants(SpellChoice.RoleOf(i), SpellNeeds.Unarmed)).Select(i => i.Id).ToList();
            CollectionAssert.IsSubsetOf(new[] { "BAT", "HAMMER", "ARROW", "BOMB" }, unarmed);
            CollectionAssert.IsEmpty(unarmed.Intersect(new[] { "CLOCK", "FAN", "SPEAKER", "PLANT", "SHIELD", "APPLE", "SODA", "BED" }), "nothing a bot can't hit with");
            var armed = Items.Enabled.Where(i => SpellChoice.BotWants(SpellChoice.RoleOf(i), Armed)).Select(i => i.Id).ToList();
            CollectionAssert.AreEquivalent(new[] { "SHIELD", "PLATE", "MIRROR", "LID", "HELMET", "DOOR" }, armed, "an armed bot only adds a shield to Hand 2");
            Assert.IsFalse(SpellChoice.BotWants(SpellRole.Shield, new SpellNeeds(true, true, false)));
        }

        [Test]
        public void AWordGoalComesFirstAndIsAllABotWants()
        {
            var goal = SpellChoice.WithGoal(null, "CLOCK");
            var order = SpellChoice.Order(new[] { "HAMMER", "CLOCK", "BAT" }, w => w, w => goal.Contains(w) ? SpellRole.Objective : Role(w), SpellNeeds.Unarmed);
            CollectionAssert.AreEqual(new[] { "CLOCK", "HAMMER", "BAT" }, order);
            Assert.IsTrue(SpellChoice.BotWants(Role("CLOCK"), SpellNeeds.Unarmed, "CLOCK", "CLOCK"), "a bot spells its goal even when it is not a weapon");
            Assert.IsFalse(SpellChoice.BotWants(Role("HAMMER"), SpellNeeds.Unarmed, "HAMMER", "CLOCK"), "and nothing else while it has one");
            Assert.IsTrue(SpellChoice.BotWants(Role("HAMMER"), SpellNeeds.Unarmed, "HAMMER", null), "without a goal the usual choice applies");
        }

        [Test]
        public void AWordGoalJoinsTheChecklistWithoutChangingIt()
        {
            var checklist = new System.Collections.Generic.HashSet<string> { "BED" };
            var both = SpellChoice.WithGoal(checklist, "BAT");
            CollectionAssert.AreEquivalent(new[] { "BED", "BAT" }, both);
            CollectionAssert.AreEquivalent(new[] { "BED" }, checklist);
            Assert.AreSame(checklist, SpellChoice.WithGoal(checklist, "BED"));
            Assert.AreSame(checklist, SpellChoice.WithGoal(checklist, null));
            Assert.IsNull(SpellChoice.WithGoal(null, ""));
        }

        [Test]
        public void NeedsCompareByValue()
        {
            Assert.AreEqual(new SpellNeeds(true, false, true), new SpellNeeds(true, false, true));
            Assert.AreNotEqual(Armed.Bits, SpellNeeds.Unarmed.Bits);
            Assert.AreEqual(0, SpellNeeds.Unarmed.Bits);
        }
    }
}
