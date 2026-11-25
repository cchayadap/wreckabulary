using System.Collections.Generic;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class BagDragTests
    {
        static List<char> Letters(string text) => new(text.ToCharArray());

        static string Text(List<char> letters) => new(letters.ToArray());

        [Test]
        public void ADragStartsOnlyPastTheThreshold()
        {
            Assert.IsFalse(BagDrag.Started(0f, 0f), "a plain click is not a drag");
            Assert.IsFalse(BagDrag.Started(BagDrag.StartPixels * .6f, BagDrag.StartPixels * .6f));
            Assert.IsTrue(BagDrag.Started(BagDrag.StartPixels, 0f));
            Assert.IsTrue(BagDrag.Started(0f, -BagDrag.StartPixels * 2f));
        }

        [Test]
        public void OnlyHeldLettersAndHeldItemsCanBeDragged()
        {
            Assert.IsTrue(BagDrag.CanDrag(BagThing.Letter, 0, 3, false));
            Assert.IsTrue(BagDrag.CanDrag(BagThing.Letter, 2, 3, false));
            Assert.IsFalse(BagDrag.CanDrag(BagThing.Letter, 3, 3, false), "an empty or reserved slot has nothing to drag");
            Assert.IsFalse(BagDrag.CanDrag(BagThing.Letter, -1, 3, false));
            Assert.IsTrue(BagDrag.CanDrag(BagThing.Item, 1, 0, true));
            Assert.IsFalse(BagDrag.CanDrag(BagThing.Item, 0, 5, false), "an empty hand has nothing to drag");
            Assert.IsFalse(BagDrag.CanDrag(BagThing.None, 0, 5, true));
        }

        [Test]
        public void TheDropColumnTakesLettersAndItems()
        {
            Assert.AreEqual(BagMoveKind.Drop, BagDrag.Resolve(BagThing.Letter, 2, BagSpot.Drop, -1, 4).Kind);
            Assert.AreEqual(2, BagDrag.Resolve(BagThing.Letter, 2, BagSpot.Drop, -1, 4).To);
            Assert.AreEqual(BagMoveKind.Drop, BagDrag.Resolve(BagThing.Item, 1, BagSpot.Drop, -1, 0).Kind);
            Assert.AreEqual(BagMoveKind.Back, BagDrag.Resolve(BagThing.None, 0, BagSpot.Drop, -1, 4).Kind);
        }

        [Test]
        public void LettersReorderAmongTheLetterSlots()
        {
            var move = BagDrag.Resolve(BagThing.Letter, 3, BagSpot.Letter, 0, 4);
            Assert.AreEqual(BagMoveKind.Reorder, move.Kind);
            Assert.AreEqual(0, move.To);
            var past = BagDrag.Resolve(BagThing.Letter, 0, BagSpot.Letter, 8, 4);
            Assert.AreEqual(BagMoveKind.Reorder, past.Kind, "an empty slot after the letters means the end");
            Assert.AreEqual(3, past.To);
            Assert.AreEqual(BagMoveKind.Back, BagDrag.Resolve(BagThing.Letter, 1, BagSpot.Letter, 1, 4).Kind, "onto itself goes back");
            Assert.AreEqual(BagMoveKind.Back, BagDrag.Resolve(BagThing.Letter, 3, BagSpot.Letter, 9, 4).Kind, "the last letter onto the end goes back");
        }

        [Test]
        public void ItemsSwapBetweenTheTwoSlots()
        {
            var swap = BagDrag.Resolve(BagThing.Item, 0, BagSpot.Item, 1, 0);
            Assert.AreEqual(BagMoveKind.Swap, swap.Kind);
            Assert.AreEqual(1, swap.To);
            Assert.AreEqual(BagMoveKind.Back, BagDrag.Resolve(BagThing.Item, 1, BagSpot.Item, 1, 0).Kind);
        }

        [Test]
        public void AnythingElseGoesBack()
        {
            Assert.AreEqual(BagMoveKind.Back, BagDrag.Resolve(BagThing.Letter, 0, BagSpot.None, -1, 4).Kind);
            Assert.AreEqual(BagMoveKind.Back, BagDrag.Resolve(BagThing.Letter, 0, BagSpot.Item, 1, 4).Kind, "a letter is not an item");
            Assert.AreEqual(BagMoveKind.Back, BagDrag.Resolve(BagThing.Item, 0, BagSpot.Letter, 2, 4).Kind, "an item is not a letter");
            Assert.AreEqual(BagMoveKind.Back, BagDrag.Resolve(BagThing.Item, 0, BagSpot.None, -1, 0).Kind);
        }

        [Test]
        public void AnUnarrangedBagIsAlphabetical()
        {
            var order = new List<char>();
            bool arranged = BagDrag.Arrange(order, Letters("TAB"), false, new List<char>());
            Assert.IsFalse(arranged);
            Assert.AreEqual("ABT", Text(order));
        }

        [Test]
        public void AnArrangedBagKeepsItsOrderAndAddsNewLettersAtTheEnd()
        {
            var spare = new List<char>();
            var order = Letters("TAB");
            Assert.IsTrue(BagDrag.Arrange(order, Letters("ABT"), true, spare));
            Assert.AreEqual("TAB", Text(order));
            Assert.IsTrue(BagDrag.Arrange(order, Letters("ABTL"), true, spare));
            Assert.AreEqual("TABL", Text(order));
            Assert.IsTrue(BagDrag.Arrange(order, Letters("BL"), true, spare), "spent letters leave their slots");
            Assert.AreEqual("BL", Text(order));
        }

        [Test]
        public void AnEmptyBagForgetsItsArrangement()
        {
            var order = Letters("TAB");
            Assert.IsFalse(BagDrag.Arrange(order, Letters(""), true, new List<char>()));
            Assert.AreEqual(0, order.Count);
        }

        [Test]
        public void TwinLettersKeepTheirPlaces()
        {
            var order = Letters("LALB");
            BagDrag.Take(order, 2);
            Assert.IsTrue(BagDrag.Arrange(order, Letters("ABL"), true, new List<char>()));
            Assert.AreEqual("LAB", Text(order), "the dragged L leaves, the other stays first");
        }

        [Test]
        public void MovingALetterInsertsItAtTheTarget()
        {
            var order = Letters("ABT");
            Assert.IsTrue(BagDrag.Move(order, 2, 0));
            Assert.AreEqual("TAB", Text(order));
            Assert.IsTrue(BagDrag.Move(order, 0, 2));
            Assert.AreEqual("ABT", Text(order));
            Assert.IsTrue(BagDrag.Move(order, 0, 9));
            Assert.AreEqual("BTA", Text(order), "past the end appends");
            Assert.IsFalse(BagDrag.Move(order, 1, 1));
            Assert.IsFalse(BagDrag.Move(order, 5, 0));
            Assert.AreEqual("BTA", Text(order));
        }

        [Test]
        public void TakingALetterRemovesThatSlot()
        {
            var order = Letters("TAB");
            Assert.AreEqual('A', BagDrag.Take(order, 1));
            Assert.AreEqual("TB", Text(order));
            Assert.AreEqual('\0', BagDrag.Take(order, 4));
            Assert.AreEqual("TB", Text(order));
        }

        [Test]
        public void TheHintNamesTheDropKeyFromTheTokens()
        {
            Assert.AreEqual("A · hold R or drag to a bin to drop", BagDrag.Hint("A", "R", false));
            Assert.AreEqual("BAT · hold LB or drag to a bin to drop", BagDrag.Hint("BAT", "LB", false));
            Assert.AreEqual("A · drag to a bin to drop", BagDrag.Hint("A", "HOLD DROP", true), "touch drags with a finger");
            Assert.AreEqual("A · drag to a bin to drop", BagDrag.Hint("A", "", false));
            Assert.AreEqual("Drag to a bin to drop", BagDrag.Hint("", "", false));
        }

        [Test]
        public void TheDropColumnShrinksBeforeTheBookAndMapDo()
        {
            Assert.AreEqual(BagDrag.ColumnMax, BagDrag.ColumnWidth(500f));
            Assert.AreEqual(60f, BagDrag.ColumnWidth(60f));
            Assert.AreEqual(BagDrag.ColumnMin, BagDrag.ColumnWidth(10f));
            Assert.AreEqual(BagDrag.ColumnEdge + 60f + BagDrag.ColumnGap, BagDrag.ColumnReserve(60f));
            Assert.GreaterOrEqual(BagDrag.ColumnMin, 30f, "room for the bin icon");
        }

        [Test]
        public void BothDropColumnsShareWhatTheMapColumnAndBookLeave()
        {
            float middle = 375f + 24f + 614f;
            Assert.AreEqual(BagDrag.ColumnMax, BagDrag.SideColumnWidth(1920f, middle));
            Assert.AreEqual(BagDrag.ColumnMax, BagDrag.SideColumnWidth(1440f, middle));
            float tight = middle + 2f * BagDrag.ColumnReserve(50f);
            Assert.AreEqual(50f, BagDrag.SideColumnWidth(tight, middle), 1e-3f, "the leftover is split evenly");
            Assert.AreEqual(BagDrag.ColumnMin, BagDrag.SideColumnWidth(middle, middle));
        }

        [Test]
        public void TheMapKeepsTheSizeItHadBesideTheOldInventoryColumn()
        {
            const float left = 35f + 375f + 24f, gap = 24f, floor = 325f, comfort = 614f;
            Assert.AreEqual(514f, BagDrag.MapTarget(1920f, left, gap, floor, comfort, 188f, 840f), .01f, "1280x720 and 1920x1080");
            Assert.AreEqual(713.6f, BagDrag.MapTarget(2119.6f, left, gap, floor, comfort, 188f, 840f), .01f, "1248x576");
            Assert.AreEqual(325f, BagDrag.MapTarget(1440f, left, gap, floor, comfort, 188f, 840f), .01f, "1440x1080");
            Assert.AreEqual(floor, BagDrag.MapTarget(1000f, left, gap, floor, comfort, 188f, 840f), "never below the floor");
        }

        [Test]
        public void TheMapShrinksFirstToFitTheInventoryUnderIt()
        {
            const float rest = 196.5f, gap = 24f, floor = 325f, column = 375f, margin = 40f, wide = 305f, tall = 127.5f;
            Assert.AreEqual(514f, BagDrag.MapSide(940f, rest, gap, floor, 514f, column, margin, wide, tall), .01f, "room to spare keeps the target");
            Assert.AreEqual(447.47f, BagDrag.MapSide(838.3f, rest, gap, floor, 713.6f, column, margin, wide, tall), .01f, "a short screen shrinks the map and the letters together");
            Assert.AreEqual(465.59f, BagDrag.MapSide(940f, rest + gap + 52f, gap, floor, 514f, column, margin, wide, tall), .01f, "a row of effects takes its share");
            Assert.AreEqual(339.46f, BagDrag.MapSide(700f, rest, gap, floor, 514f, column, margin, wide, tall), .01f, "narrower than the column, the letters stop shrinking");
            Assert.AreEqual(floor, BagDrag.MapSide(600f, rest, gap, floor, 514f, column, margin, wide, tall), "never below its floor");
        }

        [Test]
        public void TheLettersFillTheWidthOfTheMapAndStillFitUnderIt()
        {
            const float rest = 196.5f, gap = 24f, floor = 325f, column = 375f, margin = 40f, wide = 305f, tall = 127.5f;
            Assert.AreEqual(1.554f, BagDrag.LetterScale(514f, margin, wide), .001f, "1280x720 and 1920x1080");
            Assert.AreEqual(1.098f, BagDrag.LetterScale(column, margin, wide), .001f, "the narrowest column still fills its width");
            for (float height = 560f; height <= 1400f; height += 7f)
                foreach (float most in new[] { 325f, 514f, 713.6f })
                {
                    float map = BagDrag.MapSide(height, rest, gap, floor, most, column, margin, wide, tall);
                    float footprint = System.Math.Max(map, column), scale = BagDrag.LetterScale(footprint, margin, wide);
                    Assert.AreEqual(footprint, margin + wide * scale, .01f, height + " the grid ends at the right edge");
                    Assert.GreaterOrEqual(scale, 1f, height + " never smaller than before");
                    if (map > floor) Assert.LessOrEqual(map + gap + rest + tall * scale, height + .01f, height + " map, letters and hands fit the height");
                }
        }

        [Test]
        public void TheBinIconFillsMostOfItsColumn()
        {
            Assert.AreEqual(BagDrag.ColumnMax * BagDrag.IconShare, BagDrag.IconSize(BagDrag.ColumnMax), 1e-4f, "60% of a full column");
            Assert.AreEqual(BagDrag.IconMax, BagDrag.IconSize(200f), "never huge");
            Assert.AreEqual(BagDrag.IconMin, BagDrag.IconSize(50f), "never tiny while it fits");
            Assert.AreEqual(BagDrag.ColumnMin - 2f * BagDrag.IconInset, BagDrag.IconSize(BagDrag.ColumnMin), "and never wider than a narrow column");
            Assert.GreaterOrEqual(BagDrag.IconSize(BagDrag.ColumnMax), BagDrag.IconMin);
        }

        [Test]
        public void TheBinHasALidAHandleAndThreeGrooves()
        {
            Assert.IsTrue(TrashGlyph.Inside(0, (TrashGlyph.LidBottom + TrashGlyph.LidTop) * .5), "a lid");
            Assert.IsTrue(TrashGlyph.Inside((TrashGlyph.HandleHalf + TrashGlyph.HandleHoleHalf) * .5, (TrashGlyph.LidTop + TrashGlyph.HandleTop) * .5), "a handle on the lid");
            Assert.IsFalse(TrashGlyph.Inside(0, (TrashGlyph.LidTop + TrashGlyph.HandleHoleTop) * .5), "with a hole to grip");
            Assert.IsFalse(TrashGlyph.Inside(0, (TrashGlyph.BodyTop + TrashGlyph.LidBottom) * .5), "a gap under the lid");
            double mid = (TrashGlyph.GrooveTop + TrashGlyph.GrooveBottom) * .5;
            for (int i = 0; i < TrashGlyph.Grooves; i++)
                Assert.IsFalse(TrashGlyph.Inside((i - 1) * TrashGlyph.GrooveStep, mid), $"groove {i + 1}");
            Assert.IsTrue(TrashGlyph.Inside(TrashGlyph.GrooveStep * .5, mid), "body between grooves");
            Assert.IsTrue(TrashGlyph.Inside(0, (TrashGlyph.BodyBottom + TrashGlyph.GrooveBottom) * .5), "a solid foot");
            Assert.IsFalse(TrashGlyph.Inside(.98, .98), "clear corners");
            Assert.IsFalse(TrashGlyph.Inside(-.98, -.98));
        }

        [Test]
        public void TheBinIsMirroredAndRasterisesCleanly()
        {
            const int size = 48;
            int filled = 0;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    double px = (x + .5) / size * 2.0 - 1.0, py = (y + .5) / size * 2.0 - 1.0;
                    bool here = TrashGlyph.Inside(px, py);
                    if (here) filled++;
                    Assert.AreEqual(here, TrashGlyph.Inside(-px, py), $"({x},{y}) mirrored");
                }
            Assert.Greater(filled, size * size / 6);
            Assert.Less(filled, size * size * 2 / 3);
            Assert.AreEqual(0f, TrashGlyph.Coverage(0, 0, 0, 4), "no size, no ink");
            Assert.AreEqual(0f, TrashGlyph.Coverage(0, size - 1, size, 4), "a clear corner pixel");
            int footY = (int)(((TrashGlyph.BodyBottom + TrashGlyph.GrooveBottom) * .5 + 1.0) * .5 * size);
            Assert.AreEqual(1f, TrashGlyph.Coverage(size / 2, footY, size, 4), "a pixel on the body is fully inked");
        }
    }
}
