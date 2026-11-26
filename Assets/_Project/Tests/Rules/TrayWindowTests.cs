using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class TrayWindowTests
    {
        static readonly (int width, int height)[] Screens = { (1280, 720), (1920, 1080), (1248, 576) };

        static (float width, float height) Logical(int width, int height)
        {
            float scale = BagRows.CanvasScale(width, height);
            return (width / scale, height / scale);
        }

        static int RowsOf(int cells) => BagRows.RowsFor(cells);

        [Test]
        public void TheTrayShowsAtMostThreeRows()
        {
            Assert.AreEqual(3, TrayWindow.MostShownRows, "the owner: three rows at most");
            Assert.AreEqual(2, TrayWindow.ShownRows(RowsOf(10)));
            Assert.AreEqual(3, TrayWindow.ShownRows(RowsOf(15)));
            Assert.AreEqual(3, TrayWindow.ShownRows(RowsOf(20)), "Escape the House");
            Assert.AreEqual(3, TrayWindow.ShownRows(RowsOf(25)), "Escape the House with a SATCHEL");
            Assert.IsFalse(TrayWindow.Scrolls(RowsOf(10)));
            Assert.IsFalse(TrayWindow.Scrolls(RowsOf(15)));
            Assert.IsTrue(TrayWindow.Scrolls(RowsOf(20)));
            Assert.IsTrue(TrayWindow.Scrolls(RowsOf(25)));
        }

        [Test]
        public void TheWindowStaysInsideTheBag()
        {
            Assert.AreEqual(0, TrayWindow.MostTop(RowsOf(10)));
            Assert.AreEqual(0, TrayWindow.MostTop(RowsOf(15)));
            Assert.AreEqual(1, TrayWindow.MostTop(RowsOf(20)));
            Assert.AreEqual(2, TrayWindow.MostTop(RowsOf(25)));
            Assert.AreEqual(0, TrayWindow.Clamp(-3, 5));
            Assert.AreEqual(2, TrayWindow.Clamp(9, 5));
            Assert.AreEqual(0, TrayWindow.Clamp(1, 3), "fifteen cells never scroll");
            Assert.AreEqual(1, TrayWindow.Clamp(2, 4), "a shrinking bag pulls the window back");
        }

        [Test]
        public void ScrollingMovesAWholeRowAndStopsAtTheEnds()
        {
            Assert.AreEqual(1, TrayWindow.Scroll(0, 1, 5));
            Assert.AreEqual(2, TrayWindow.Scroll(1, 1, 5));
            Assert.AreEqual(2, TrayWindow.Scroll(2, 1, 5), "clamped at the bottom");
            Assert.AreEqual(0, TrayWindow.Scroll(0, -1, 5), "clamped at the top");
            Assert.AreEqual(1, TrayWindow.Scroll(0, 3, 4));
            Assert.AreEqual(0, TrayWindow.Scroll(0, 1, 3), "no scrolling with three rows");
            Assert.IsFalse(TrayWindow.CanUp(0, 5));
            Assert.IsTrue(TrayWindow.CanDown(0, 5));
            Assert.IsTrue(TrayWindow.CanUp(2, 5));
            Assert.IsFalse(TrayWindow.CanDown(2, 5));
            Assert.IsFalse(TrayWindow.CanUp(0, 3));
            Assert.IsFalse(TrayWindow.CanDown(0, 3));
        }

        [Test]
        public void TheWheelMovesOneRowAgainstItsDirection()
        {
            Assert.AreEqual(-1, TrayWindow.WheelStep(120f), "wheel up shows rows above");
            Assert.AreEqual(1, TrayWindow.WheelStep(-120f), "wheel down shows rows below");
            Assert.AreEqual(1, TrayWindow.WheelStep(-.01f));
            Assert.AreEqual(0, TrayWindow.WheelStep(0f));
        }

        [Test]
        public void VisibleCellsAreTheWindowRowsOnly()
        {
            Assert.IsTrue(TrayWindow.Shows(0, 4, 0));
            Assert.IsTrue(TrayWindow.Shows(0, 4, 14));
            Assert.IsFalse(TrayWindow.Shows(0, 4, 15), "row four is below the window");
            Assert.IsFalse(TrayWindow.Shows(1, 4, 4), "row one is above it after a scroll");
            Assert.IsTrue(TrayWindow.Shows(1, 4, 19));
            Assert.IsFalse(TrayWindow.Shows(0, 4, -1));
            for (int cell = 0; cell < 25; cell++)
                Assert.AreEqual(BagRows.CellTop(cell), TrayWindow.CellTop(cell, 0), "at the top the cells sit where they always did: " + cell);
            Assert.AreEqual(0f, TrayWindow.CellTop(15, 3));
            Assert.AreEqual(-140f, TrayWindow.CellTop(24, 2), "the fifth row is the third visible row");
        }

        [Test]
        public void ALetterLandingOutOfViewIsScrolledIntoView()
        {
            Assert.AreEqual(1, TrayWindow.Reveal(0, 4, 15));
            Assert.AreEqual(2, TrayWindow.Reveal(0, 5, 24));
            Assert.AreEqual(0, TrayWindow.Reveal(2, 5, 3));
            Assert.AreEqual(1, TrayWindow.Reveal(1, 5, 12), "already visible: no jump");
            Assert.AreEqual(1, TrayWindow.Follow(0, 4, 15, 16, 16, 15), "the sixteenth letter scrolls row four in");
            Assert.AreEqual(0, TrayWindow.Follow(2, 5, 20, 21, 21, 0), "an A sorted to the front scrolls back up");
            Assert.AreEqual(2, TrayWindow.Follow(2, 5, 20, 21, 21, 22), "landing inside the window keeps it");
            Assert.AreEqual(1, TrayWindow.Follow(0, 4, 15, 16, 16, -1), "no landing cell: the newest cell is shown");
        }

        [Test]
        public void SpendingLettersBringsTheWindowBackToFilledRows()
        {
            Assert.AreEqual(0, TrayWindow.Follow(2, 5, 20, 6, 6, -1), "only empty rows were showing");
            Assert.AreEqual(1, TrayWindow.Follow(1, 4, 20, 9, 9, -1), "row two still holds letters: stay");
            Assert.AreEqual(0, TrayWindow.Follow(1, 4, 20, 4, 4, -1), "only row one is filled: back to the top");
            Assert.AreEqual(2, TrayWindow.Follow(2, 5, 20, 18, 18, -1), "filled cells still show: stay");
            Assert.AreEqual(0, TrayWindow.Follow(2, 5, 20, 0, 0, -1), "an empty bag goes to the top");
            Assert.AreEqual(2, TrayWindow.Follow(2, 5, 20, 8, 12, -1), "reserved letters of a spell count as filled");
            Assert.AreEqual(2, TrayWindow.Follow(2, 5, 8, 8, 8, -1), "a manual scroll is left alone while nothing changes");
        }

        [Test]
        public void TheLandingCellIsTheFirstCellThatChanged()
        {
            Assert.AreEqual(3, TrayWindow.Landed("ABC".ToCharArray(), 3, "ABCD".ToCharArray()));
            Assert.AreEqual(0, TrayWindow.Landed("BCD".ToCharArray(), 3, "ABCD".ToCharArray()));
            Assert.AreEqual(1, TrayWindow.Landed("ACD".ToCharArray(), 3, "ABCD".ToCharArray()));
            Assert.AreEqual(-1, TrayWindow.Landed("ABCD".ToCharArray(), 4, "ABC".ToCharArray()));
            Assert.AreEqual(0, TrayWindow.Landed(null, 0, "A".ToCharArray()));
            Assert.AreEqual(-1, TrayWindow.Landed("A".ToCharArray(), 1, null));
            var before = new char[25];
            "ABCDEFGHIJKLMNOPQRST".ToCharArray().CopyTo(before, 0);
            Assert.AreEqual(20, TrayWindow.Landed(before, 20, "ABCDEFGHIJKLMNOPQRSTZ".ToCharArray()), "a snapshot array longer than the bag");
        }

        [Test]
        public void TheThumbShowsWhereTheWindowIs()
        {
            float track = TrayWindow.TrackTall;
            Assert.Greater(track, 0f);
            var full = TrayWindow.Thumb(0, 3);
            Assert.AreEqual(0f, full.Offset);
            Assert.AreEqual(track, full.Length);
            var top = TrayWindow.Thumb(0, 5);
            Assert.AreEqual(0f, top.Offset);
            Assert.AreEqual(track * 3f / 5f, top.Length, 1e-4f);
            var bottom = TrayWindow.Thumb(2, 5);
            Assert.AreEqual(track, bottom.Offset + bottom.Length, 1e-4f, "at the bottom the thumb reaches the end");
            var four = TrayWindow.Thumb(1, 4);
            Assert.AreEqual(track, four.Offset + four.Length, 1e-4f);
            Assert.AreEqual(BagRows.GridTall(3, BagRows.TrayStepY, BagRows.TrayTileTall), TrayWindow.ColumnTall);
        }

        [Test]
        public void TwoAndThreeRowsKeepTheirApprovedNumbers()
        {
            foreach (var (w, h) in Screens)
            {
                var (width, _) = Logical(w, h);
                foreach (bool aside in new[] { false, true })
                {
                    foreach (bool touch in new[] { false, true })
                    {
                        string at = $"{w}x{h} aside {aside} touch {touch}";
                        var two = BagRows.Frame(2, width, aside, touch);
                        var three = BagRows.Frame(3, width, aside, touch);
                        float scale = two.Tray.Width / BagRows.TrayWidth;
                        Assert.AreEqual(176f * scale, two.Tray.Height, 1e-3f, at + " two rows: 176 tall");
                        Assert.AreEqual(198f * scale, three.Tray.Height, 1e-3f, at + " three rows: 198 tall");
                        Assert.AreEqual(BagRows.HandY(1, 2) * scale + two.Tray.Y, two.Hands.Y, 1e-3f, at);
                        Assert.AreEqual(BagRows.HandY(1, 3) * scale + three.Tray.Y, three.Hands.Y, 1e-3f, at);
                        Assert.AreEqual(BagRows.VitalsAt(aside && !touch, touch, 3), three.Vitals.Y, 1e-3f, at);
                    }
                }
            }
        }

        [Test]
        public void MoreThanFifteenCellsMovesNothing()
        {
            foreach (var (w, h) in Screens)
            {
                var (width, _) = Logical(w, h);
                foreach (bool aside in new[] { false, true })
                {
                    foreach (bool touch in new[] { false, true })
                    {
                        var three = BagRows.Frame(3, width, aside, touch);
                        foreach (int rows in new[] { 4, 5 })
                        {
                            string at = $"{w}x{h} rows {rows} aside {aside} touch {touch}";
                            var f = BagRows.Frame(rows, width, aside, touch);
                            Assert.AreEqual(three.Tray.ToString(), f.Tray.ToString(), at + " tray");
                            Assert.AreEqual(three.Hands.ToString(), f.Hands.ToString(), at + " item slots");
                            Assert.AreEqual(three.SpellHint.ToString(), f.SpellHint.ToString(), at + " spell hint");
                            Assert.AreEqual(three.Vitals.ToString(), f.Vitals.ToString(), at + " health bar");
                            Assert.AreEqual(three.Letters.ToString(), f.Letters.ToString(), at + " letter grid");
                        }
                    }
                }
            }
        }

        [Test]
        public void TheScrollColumnFitsBesideTheGridAtTheThreeSizes()
        {
            foreach (var (w, h) in Screens)
            {
                var (width, height) = Logical(w, h);
                var map = BagRows.Minimap(width, height);
                foreach (int rows in new[] { 4, 5 })
                {
                    foreach (bool aside in new[] { false, true })
                    {
                        foreach (bool touch in new[] { false, true })
                        {
                            string at = $"{w}x{h} rows {rows} aside {aside} touch {touch}";
                            var f = BagRows.Frame(rows, width, aside, touch);
                            var column = TrayWindow.Column(rows, width, aside, touch);
                            Assert.IsTrue(column.Inside(width, height), at + " column " + column);
                            Assert.IsFalse(column.Overlaps(f.Letters), at + " column vs letters");
                            Assert.IsFalse(column.Overlaps(f.Hands), at + " column vs item slots");
                            Assert.IsFalse(column.Overlaps(f.Vitals), at + " column vs health bar");
                            Assert.IsFalse(column.Overlaps(f.SpellHint), at + " column vs spell hint");
                            Assert.IsFalse(column.Overlaps(map), at + " column vs map");
                            Assert.AreEqual(f.Letters.Top, column.Top, 1e-3f, at + " the column lines up with the top row");
                            Assert.AreEqual(f.Letters.Height, column.Height, 1e-3f, at + " and is as tall as three rows");
                        }
                    }
                }
            }
        }

        [Test]
        public void TheSlotWearBarFollowsDurability()
        {
            Assert.IsTrue(SlotWear.Shows(true, false, 30));
            Assert.IsFalse(SlotWear.Shows(false, false, 30), "an empty slot has no bar");
            Assert.IsFalse(SlotWear.Shows(true, true, 30), "carried furniture has no bar");
            Assert.IsFalse(SlotWear.Shows(true, false, 0), "helpers, deployables and one-use items have no bar");
            Assert.AreEqual(1f, SlotWear.Fraction(30f, 30));
            Assert.AreEqual(.5f, SlotWear.Fraction(15f, 30), 1e-5f);
            Assert.AreEqual(0f, SlotWear.Fraction(-4f, 30));
            Assert.AreEqual(1f, SlotWear.Fraction(45f, 30), "never past full");
            Assert.AreEqual(0f, SlotWear.Fraction(1f, 0));
            Assert.IsFalse(SlotWear.Low(.5f));
            Assert.IsTrue(SlotWear.Low(SlotWear.LowFraction), "the warning starts at the threshold");
            Assert.IsTrue(SlotWear.Low(.1f));
        }

        [Test]
        public void TheSlotWearBarSitsUnderTheIconAndTheWord()
        {
            Assert.AreEqual(152f, SlotWear.SlotWide(BagRows.HandWide));
            float top = SlotWear.SlotBottom + SlotWear.SlotTall;
            Assert.Less(top, SlotWear.SlotIconBottom, "the icon (52 tall, centred 3 below the middle) stays above the bar");
            Assert.Less(top, SlotWear.SlotWordBottom, "the word stays above the bar");
            Assert.GreaterOrEqual(SlotWear.SlotBottom, 3f, "inside the 3 px ring");
            Assert.AreEqual(BagRows.HandTall * .5f - 3f - 26f, SlotWear.SlotIconBottom, "icon bottom from MakeHand");
            Assert.AreEqual(BagRows.HandTall * .5f - 3f - 16f, SlotWear.SlotWordBottom, "word bottom from MakeHand");
        }
    }
}
