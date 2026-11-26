using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class RecipeSheetReversalTests
    {
        static readonly HouseTuning Tuning = HouseTuning.FromJson(TestData.Read("items_house.json"));
        static readonly ItemCatalogue Items = Tuned();

        static ItemCatalogue Tuned()
        {
            var items = TestData.Catalogue();
            items.Expand(ItemCatalogue.FromJson(TestData.Read("items_house.json"), "items_house.json"));
            Tuning.Apply(items, TestData.LoadRuleBook());
            return items;
        }

        static RecipeSheetText Sheet(string id) => RecipeSheet.Build(Items.Get(id), Tuning);

        [Test]
        public void EachReversingItemSaysWhoseDirectionsTurnAndForHowLong()
        {
            Assert.AreEqual("A hit swaps the target's left and right for 3 s.", Sheet("SPINNER").Effects[0]);
            Assert.AreEqual("Every 3rd hit swaps the target's forward and back for 3 s.", Sheet("FLIPPER").Effects[0]);
            Assert.AreEqual("Reverses directions for others inside, and for 1 s after.", Sheet("HOOP").Effects.Single());
            Assert.AreEqual("While raised, reverses directions within 3 m, yours too.", Sheet("COMPASS").Effects[0]);
            Assert.AreEqual(3, Sheet("FLIPPER").Effects.Count, "the other traits keep their own sentences");
            StringAssert.StartsWith("Knocks back", Sheet("FLIPPER").Effects[1]);
        }

        [Test]
        public void TheReversingSentencesFitThePanel()
        {
            int effectChars = RecipeSheet.EffectLines * RecipeSheet.MaxChars(RecipeSheet.TextWidth, RecipeSheet.EffectFont);
            foreach (string id in new[] { "SPINNER", "FLIPPER", "HOOP", "COMPASS" })
                foreach (string effect in Sheet(id).Effects)
                {
                    Assert.LessOrEqual(effect.Length, effectChars, $"{id}: '{effect}'");
                    Assert.IsTrue(char.IsUpper(effect[0]) && effect.EndsWith("."), $"{id}: a sentence: '{effect}'");
                }
        }

        [Test]
        public void TheHoopIsAReverseZoneNotASlowField()
        {
            var hoop = Sheet("HOOP");
            Assert.AreEqual("Reverse zone to place", hoop.Kind);
            Assert.IsFalse(hoop.Rows.Any(r => r.Label == "Slows to"), "the reverse ring replaces the slow field: " + string.Join(" | ", hoop.Rows));
            Assert.AreEqual("1.6 m radius", hoop.Rows.Single(r => r.Label == RecipeSheet.Size).Value);
            Assert.AreEqual("8 s", hoop.Rows.Single(r => r.Label == RecipeSheet.Lasts).Value);
            Assert.AreEqual("Slow field to place", Sheet("CLOCK").Kind, "a plain slow field keeps its own case");
            Assert.AreEqual("Slow field to place", RecipeSheet.Build(Items.Get("HOOP"), null).Kind, "without house tuning there is no reversal to show");
        }

        [Test]
        public void EveryDeliveryAndModeReadsAsASentence()
        {
            Assert.AreEqual("25% of hits swap the target's left and right for 2 s.",
                Reversal.Sentence(new ItemTrait { Kind = TraitKind.Reverse, Turn = ReverseMode.Mirror, Seconds = 2f, Chance = .25f }));
            Assert.AreEqual("A hit reverses the target's directions for 1.5 s.",
                Reversal.Sentence(new ItemTrait { Kind = TraitKind.Reverse, Turn = ReverseMode.Turnabout, Seconds = 1.5f }));
            Assert.AreEqual("Swaps forward and back for others inside, and for 0.5 s after.",
                Reversal.Sentence(new ItemTrait { Kind = TraitKind.Reverse, Turn = ReverseMode.Flip, Seconds = .5f, Inside = true }));
            Assert.AreEqual("", Reversal.Sentence(new ItemTrait { Kind = TraitKind.Reverse, Turn = ReverseMode.None, Seconds = 1f }));
            Assert.AreEqual("", Reversal.Sentence(new ItemTrait { Kind = TraitKind.Heavy, Amount = 1.2f }));
            Assert.AreEqual("", Reversal.Sentence(null));
        }
    }
}
