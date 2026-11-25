using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class PlaceGridTests
    {
        const float Spacing = 12, Inset = 8, Caption = 40, Wide = 1830, Phone = 2250, Room = 657, Smallest = 264;

        static PlaceGridPlan Fit(int count, float width, float height = Room) => PlaceGrid.Fit(count, width, height, Spacing, Inset, Caption, Smallest);

        static void AssertFills(PlaceGridPlan plan, int count, float width, float height, string where)
        {
            Assert.IsTrue(plan.Fits, where);
            Assert.AreEqual((count + plan.Columns - 1) / plan.Columns, plan.Rows, where);
            Assert.LessOrEqual(plan.Rows * plan.Height + (plan.Rows - 1) * Spacing, height + .01f, where + ": every card shows without scrolling");
            Assert.LessOrEqual(plan.Columns * plan.Width + (plan.Columns - 1) * Spacing, width + .01f, where + ": the row fits");
            Assert.LessOrEqual(plan.Width, PlaceGrid.CappedWidth(width, Spacing) + .01f, where + ": never wider than three to a row");
            Assert.GreaterOrEqual(plan.Width, Smallest - .01f, where + ": never under the readable width");
            Assert.That((plan.Width - Inset * 2) / (plan.Height - Caption - Inset), Is.EqualTo(PlaceGrid.PictureAspect).Within(.001f), where + ": a whole 16:9 picture");
            for (int columns = 1; columns <= count; columns++)
            {
                int rows = (count + columns - 1) / columns;
                float card = System.Math.Min(PlaceGrid.CappedWidth(width, Spacing), (width - (columns - 1) * Spacing) / columns);
                float tall = PlaceGrid.CardHeight(card, Inset, Caption);
                if (card >= Smallest && rows * tall + (rows - 1) * Spacing <= height) Assert.LessOrEqual(card, plan.Width + .01f, where + ": no other column count gives a bigger card");
            }
        }

        [Test]
        public void ThePlaceStepFillsTheRowWithTheBiggestCardsThatFitWithoutScrolling()
        {
            foreach (var width in new[] { Wide, Phone })
                foreach (int count in new[] { 1, 2, 3, 5, 6, 9 })
                    AssertFills(Fit(count, width), count, width, Room, width + " with " + count);
            var three = Fit(3, Wide);
            Assert.AreEqual(3, three.Columns, "three maps take one full row at 1280x720");
            Assert.That(three.Width * 3 + Spacing * 2, Is.EqualTo(Wide).Within(.01f), "and fill it");
            Assert.AreEqual(4, Fit(5, Wide).Columns, "five houses take four and one at 1280x720");
            Assert.AreEqual(5, Fit(9, Wide).Columns, "nine take five and four");
            Assert.AreEqual(3, Fit(3, Phone).Columns);
            Assert.AreEqual(5, Fit(5, Phone).Columns, "the wide phone window fits five houses in one row");
            Assert.AreEqual(5, Fit(9, Phone).Columns);
        }

        [Test]
        public void OneOrTwoCardsStopAtTheThreeColumnWidth()
        {
            Assert.That(Fit(1, Wide).Width, Is.EqualTo(PlaceGrid.CappedWidth(Wide, Spacing)).Within(.01f));
            Assert.That(Fit(2, Wide).Width, Is.EqualTo(PlaceGrid.CappedWidth(Wide, Spacing)).Within(.01f));
            Assert.AreEqual(2, Fit(2, Wide).Columns, "two cards share one row");
        }

        [Test]
        public void NothingFitsWhenTheViewportIsTooShort()
        {
            Assert.IsFalse(Fit(9, 400, 40).Fits, "a strip shorter than one caption fits no card");
            Assert.IsFalse(Fit(5, 860, 250).Fits, "a short window keeps readable cards and scrolls instead of shrinking them");
            Assert.GreaterOrEqual(Fit(9, Wide).Width, Smallest, "cards never drop under the readable width");
            Assert.IsFalse(Fit(0, Wide).Fits);
            Assert.IsFalse(Fit(3, 0).Fits);
        }
    }
}
