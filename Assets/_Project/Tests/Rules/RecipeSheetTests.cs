using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class RecipeSheetTests
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

        static string Row(RecipeSheetText sheet, string label)
        {
            var rows = sheet.Rows.Where(r => r.Label == label).ToList();
            Assert.AreEqual(1, rows.Count, $"{sheet.Name} has one '{label}' row: {string.Join(" | ", sheet.Rows)}");
            return rows[0].Value;
        }

        static readonly SheetBox Wide = new SheetBox(0f, 0f, 1920f, 1080f);

        [Test]
        public void TheSheetOpensRightOfACardWithRoomAndLeftOfACardAtTheEdge()
        {
            var card = new SheetBox(900f, 500f, 1099f, 720f);
            var spot = SheetPlacement.Place(card, RecipeSheet.Width, 420f, Wide);
            Assert.AreEqual(SheetSide.Right, spot.Side);
            Assert.IsTrue(spot.Beside);
            Assert.AreEqual(card.Right + SheetPlacement.Gap, spot.Box.Left, 1e-3f);
            Assert.AreEqual(card.Top, spot.Box.Top, 1e-3f, "level with the card's top");
            Assert.IsFalse(spot.Box.Overlaps(card));

            var edge = new SheetBox(1600f, 500f, 1799f, 720f);
            spot = SheetPlacement.Place(edge, RecipeSheet.Width, 420f, Wide);
            Assert.AreEqual(SheetSide.Left, spot.Side);
            Assert.IsTrue(spot.Beside);
            Assert.AreEqual(edge.Left - SheetPlacement.Gap, spot.Box.Right, 1e-3f);
            Assert.IsFalse(spot.Box.Overlaps(edge));
            Assert.IsTrue(spot.Box.Inside(Wide));
        }

        [Test]
        public void TheSheetIsClampedInsideTheSafeAreaTopAndBottom()
        {
            var low = new SheetBox(400f, 20f, 599f, 240f);
            var spot = SheetPlacement.Place(low, RecipeSheet.Width, 500f, Wide);
            Assert.AreEqual(Wide.Bottom + SheetPlacement.Margin, spot.Box.Bottom, 1e-3f, "a low card lifts the sheet");
            Assert.IsTrue(spot.Box.Inside(Wide));
            Assert.IsFalse(spot.Box.Overlaps(low));

            var high = new SheetBox(400f, 900f, 599f, 1120f);
            spot = SheetPlacement.Place(high, RecipeSheet.Width, 300f, Wide);
            Assert.AreEqual(Wide.Top - SheetPlacement.Margin, spot.Box.Top, 1e-3f, "a card scrolled half out of the top keeps the sheet on screen");
            Assert.IsTrue(spot.Box.Inside(Wide));

            var tall = SheetPlacement.Place(low, RecipeSheet.Width, 2000f, Wide);
            Assert.AreEqual(Wide.Top - SheetPlacement.Margin, tall.Box.Top, 1e-3f, "too tall for the window: pinned to the top");
        }

        [TestCase(1440f, 1080f), TestCase(1920f, 1080f), TestCase(2119.6f, 978.3f)]
        public void EveryCardPositionGetsASheetBesideItAtSupportedWindows(float width, float height)
        {
            var safe = new SheetBox(0f, 0f, width, height);
            const float card = 199f, cardTall = 220f, sheetTall = 520f;
            Assert.IsTrue(SheetPlacement.AlwaysBeside(width, card, RecipeSheet.Width), $"{width} x {height}");
            for (float x = SheetPlacement.Margin; x + card <= width - SheetPlacement.Margin; x += 25f)
                for (float y = 0f; y + cardTall <= height; y += 60f)
                {
                    var box = new SheetBox(x, y, x + card, y + cardTall);
                    var spot = SheetPlacement.Place(box, RecipeSheet.Width, sheetTall, safe);
                    Assert.IsTrue(spot.Beside, $"{box} at {width}");
                    Assert.IsFalse(spot.Box.Overlaps(box), $"{box} at {width}");
                    Assert.IsTrue(spot.Box.Inside(safe, 1e-3f), $"{spot.Box} inside {width} x {height}");
                }
        }

        [Test]
        public void WhenNeitherSideFitsTheSheetTakesTheRoomierSideAndStaysOnScreen()
        {
            var narrow = new SheetBox(0f, 0f, 700f, 600f);
            var card = new SheetBox(260f, 200f, 459f, 420f);
            Assert.IsFalse(SheetPlacement.AlwaysBeside(narrow.Width, card.Width, RecipeSheet.Width));
            var spot = SheetPlacement.Place(card, RecipeSheet.Width, 300f, narrow);
            Assert.IsFalse(spot.Beside, "this window is narrower than any the game supports");
            Assert.IsTrue(spot.Box.Inside(narrow, 1e-3f));
        }

        [Test]
        public void EveryEnabledRecipeGetsAKindRowsAndEffectsThatFitThePanel()
        {
            int labelChars = RecipeSheet.MaxChars(RecipeSheet.LabelWidth, RecipeSheet.RowFont);
            int valueChars = RecipeSheet.MaxChars(RecipeSheet.ValueWidth, RecipeSheet.RowFont);
            int effectChars = RecipeSheet.EffectLines * RecipeSheet.MaxChars(RecipeSheet.TextWidth, RecipeSheet.EffectFont);
            int kindChars = RecipeSheet.MaxChars(RecipeSheet.KindWidth, RecipeSheet.KindFont);
            Assert.GreaterOrEqual(RecipeSheet.MinFont, 15f);
            var families = new HashSet<HandlingFamily>();
            foreach (var item in Items.Enabled)
            {
                var sheet = RecipeSheet.Build(item, Tuning);
                families.Add(item.Family);
                Assert.AreEqual(item.Id, sheet.Name);
                Assert.AreNotEqual("Gear", sheet.Kind, item.Id + " has a family case");
                Assert.LessOrEqual(sheet.Kind.Length, kindChars, item.Id + " kind: " + sheet.Kind);
                Assert.AreEqual(RecipeDifficulty.TierOf(item.Id), sheet.Tier);
                Assert.AreEqual(RecipeStars.Count(sheet.Tier), sheet.Stars);
                Assert.GreaterOrEqual(sheet.Rows.Count, 3, item.Id + " shows its numbers");
                Assert.LessOrEqual(sheet.Rows.Count, 7, item.Id + " stays readable in a few seconds");
                Assert.AreEqual(sheet.Rows.Count, sheet.Rows.Select(r => r.Label).Distinct().Count(), item.Id + " no repeated label");
                foreach (var row in sheet.Rows)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(row.Label), item.Id);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(row.Value), item.Id + " " + row.Label);
                    Assert.LessOrEqual(row.Label.Length, labelChars, $"{item.Id}: '{row.Label}'");
                    Assert.LessOrEqual(row.Value.Length, valueChars, $"{item.Id}: '{row.Value}'");
                }
                Assert.IsNotEmpty(sheet.Effects, item.Id);
                foreach (string effect in sheet.Effects)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(effect), item.Id);
                    Assert.LessOrEqual(effect.Length, effectChars, $"{item.Id}: '{effect}'");
                    Assert.IsTrue(!char.IsLower(effect[0]) && effect.EndsWith("."), $"{item.Id}: a sentence: '{effect}'");
                }
                Assert.AreEqual(item.IsTwoHanded ? "Two hands" : "One hand", Row(sheet, RecipeSheet.Hand));
                if (item.Consumable) Assert.AreEqual("Once", Row(sheet, RecipeSheet.Uses));
            }
            foreach (var family in new[] { HandlingFamily.MeleeSwing, HandlingFamily.MeleeThrust, HandlingFamily.Thrown, HandlingFamily.Shield,
                HandlingFamily.DeployCover, HandlingFamily.DeployPad, HandlingFamily.DeploySpeed, HandlingFamily.DeployZone,
                HandlingFamily.Buff, HandlingFamily.Heal, HandlingFamily.Ranged, HandlingFamily.Utility })
                Assert.IsTrue(families.Contains(family), family + " is covered by a recipe");
        }

        [Test]
        public void EveryNumberShownComesStraightFromTheItemData()
        {
            foreach (var item in Items.Enabled)
            {
                var allowed = Allowed(item);
                foreach (var row in RecipeSheet.Build(item, Tuning).Rows)
                    foreach (System.Text.RegularExpressions.Match number in System.Text.RegularExpressions.Regex.Matches(row.Value, @"\d+(\.\d+)?"))
                        Assert.IsTrue(allowed.Contains(number.Value), $"{item.Id} {row.Label} shows {number.Value}, which is not in its data");
            }
        }

        static HashSet<string> Allowed(ItemDefinition item)
        {
            var values = new List<float> { item.Durability };
            if (item.Melee is { } m) values.AddRange(new[] { m.Damage, m.Reach, m.Cycle });
            if (item.Thrown is { } t) values.AddRange(new[] { t.Damage, t.Speed, t.FuseSeconds, t.Radius, t.EdgeDamage });
            if (item.Shield is { } s) values.AddRange(new[] { s.FrontArcDegrees, s.DamageReduction * 100f, s.MoveSpeedMultiplier * 100f, s.RaiseSeconds });
            if (item.Deploy is { } d) values.AddRange(new[] { d.FootprintX, d.FootprintZ, d.PlaceSeconds, d.Strength, d.Strength * 100f, d.LifetimeSeconds, d.Radius, d.ArcDegrees });
            if (item.Use is { } u) values.AddRange(new[] { u.Amount, (u.Amount - 1f) * 100f, u.Seconds, u.ChannelSeconds });
            if (item.Ranged is { } r) values.AddRange(new[] { r.Damage, r.FullDamage, r.Speed, r.FullSpeed, r.DrawSeconds });
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (float v in values) { set.Add(RecipeSheet.N(v)); set.Add(RecipeSheet.N(v, "0")); }
            return set;
        }

        [Test]
        public void OneRecipeOfEachFamilyReadsItsOwnNumbers()
        {
            var blade = Sheet("BLADE");
            Assert.AreEqual("Swing weapon", blade.Kind);
            Assert.AreEqual(RecipeSheet.N(Items.Get("BLADE").Melee.Damage) + " a hit", Row(blade, "Damage"));
            Assert.AreEqual("0.76 s", Row(blade, "Swing time"));
            Assert.AreEqual("1.4 m", Row(blade, "Reach"));
            Assert.AreEqual("16 swings", Row(blade, "Lasts"));
            CollectionAssert.AreEqual(new[] { RecipeSheet.Sentence(Tuning.Describe("BLADE")) }, blade.Effects);
            StringAssert.StartsWith("Bleeds 2/s for 3 s", blade.Effects[0]);

            var torch = Sheet("TORCH");
            Assert.AreEqual("Reach weapon", torch.Kind);
            Assert.AreEqual("0.58 s", Row(torch, "Jab time"));
            Assert.AreEqual(2, torch.Effects.Count, "every trait gets its own sentence");

            var bomb = Sheet("BOMB");
            Assert.AreEqual("Thrown blast", bomb.Kind);
            Assert.AreEqual("45 centre, 15 edge", Row(bomb, "Damage"));
            Assert.AreEqual("3 m after 2.5 s", Row(bomb, "Blast"));
            Assert.AreEqual("11 m/s, lobbed", Row(bomb, "Throw"));
            Assert.AreEqual("Once", Row(bomb, RecipeSheet.Uses));
            CollectionAssert.AreEqual(new[] { RecipeSheet.NoEffect }, bomb.Effects);

            var ball = Sheet("BALL");
            Assert.AreEqual("Pick it up again", Row(ball, "After a throw"));
            Assert.AreEqual("30", Row(ball, RecipeSheet.Toughness));

            var bow = Sheet("BOW");
            Assert.AreEqual("Ranged weapon", bow.Kind);
            Assert.AreEqual("8 to 20", Row(bow, "Damage"));
            Assert.AreEqual("14 to 30 m/s", Row(bow, "Shot speed"));
            Assert.AreEqual("0.9 s", Row(bow, "Full draw"));
            Assert.AreEqual("14 shots", Row(bow, "Lasts"));

            var plate = Sheet("PLATE");
            Assert.AreEqual("100% of damage", Row(plate, "Blocks"));
            Assert.AreEqual("120 degrees", Row(plate, "Front arc"));
            Assert.AreEqual("70% speed", Row(plate, "Move while up"));
            Assert.AreEqual("60 damage blocked", Row(plate, "Lasts"));
            Assert.AreEqual("All round", Row(Sheet("SHIELD"), "Front arc"));

            var table = Sheet("TABLE");
            Assert.AreEqual("Cover to place", table.Kind);
            Assert.AreEqual("1.2 x 0.69 m", Row(table, RecipeSheet.Size));
            Assert.AreEqual("0.8 s", Row(table, RecipeSheet.SetUp));
            Assert.AreEqual("120", Row(table, RecipeSheet.Toughness));
            Assert.AreEqual("22 a hit", Row(table, "Swing damage"));
            Assert.AreEqual("Two hands", Row(table, RecipeSheet.Hand));

            Assert.AreEqual("11 m/s up", Row(Sheet("BED"), "Bounce"));
            Assert.AreEqual("x1.6 speed", Row(Sheet("MAT"), "Boost"));

            var clock = Sheet("CLOCK");
            Assert.AreEqual("Slow field to place", clock.Kind);
            Assert.AreEqual("60% speed", Row(clock, "Slows to"));
            Assert.AreEqual("2.75 m radius", Row(clock, RecipeSheet.Size));
            Assert.AreEqual("10 s", Row(clock, RecipeSheet.Lasts));
            var fan = Sheet("FAN");
            Assert.AreEqual("up to 8 m/s", Row(fan, "Push"));
            Assert.AreEqual("90 degrees, 3.2 m", Row(fan, "Cone"));
            Assert.AreEqual("Slippery patch", Sheet("SOAP").Kind);

            var foam = Sheet("FOAM");
            Assert.AreEqual("Bubble shield", foam.Kind);
            Assert.AreEqual("35 HP shield", Row(foam, "Bubble"));
            Assert.AreEqual("10 s", Row(foam, RecipeSheet.Lasts));
            Assert.AreEqual("0.4 s", Row(foam, "Use time"));
            Assert.AreEqual("+35%", Row(Sheet("SODA"), "Speed"));
            Assert.AreEqual("30 HP", Row(Sheet("APPLE"), "Heals"));
            Assert.AreEqual("Team stash to place", Sheet("CHEST").Kind);
        }

        [Test]
        public void HeldLettersAreMarkedOncePerTileAndTheMissingCountFollows()
        {
            var held = RecipeSheet.Held("BALL", "LAB", false);
            CollectionAssert.AreEqual(new[] { true, true, true, false }, held);
            Assert.AreEqual(1, RecipeSheet.Missing(held));
            Assert.AreEqual("1 letter missing", RecipeSheet.LettersLine(1, false));
            Assert.AreEqual("3 letters missing", RecipeSheet.LettersLine(3, false));
            Assert.AreEqual("Ready to spell", RecipeSheet.LettersLine(RecipeSheet.Missing(RecipeSheet.Held("BAT", "TAXB", false)), false));
            var free = RecipeSheet.Held("BAT", "", true);
            Assert.IsTrue(free.All(h => h));
            Assert.AreEqual("Free to spell", RecipeSheet.LettersLine(0, true));
            Assert.IsEmpty(RecipeSheet.Held("", "ABC", false));
        }

        [Test]
        public void EffectSentencesStartWithACapitalAndEndWithAStop()
        {
            Assert.AreEqual("While held: -10% damage.", RecipeSheet.Sentence("while held: -10% damage"));
            Assert.AreEqual("Every 3rd hit crushes x1.75.", RecipeSheet.Sentence("every 3rd hit crushes x1.75"));
            Assert.AreEqual("Done.", RecipeSheet.Sentence("Done."));
            Assert.AreEqual("", RecipeSheet.Sentence("  "));
        }
    }
}
