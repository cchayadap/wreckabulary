using System;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class BagRowsTests
    {
        static readonly (int width, int height)[] Screens = { (1280, 720), (1920, 1080), (1248, 576) };

        const float BagTop = 105f, BagBottom = 35f, BagGap = 24f, BagColumn = 375f, MapFloor = 325f;
        const float LetterMargin = 40f, LetterWide = 55f, LetterTall = 60f, LetterStepX = 62.5f, LetterStepY = 67.5f;
        const float LetterFoot = 27.5f, HandTall = 145f, LetterFont = 33f, TrayFont = 34f;
        const float GridWide = 4f * LetterStepX + LetterWide;

        static (float width, float height) Logical(int width, int height)
        {
            float scale = BagRows.CanvasScale(width, height);
            return (width / scale, height / scale);
        }

        [Test]
        public void ABonusRowAddsFiveSlotsOnTopOfTheModesBag()
        {
            Assert.AreEqual(10, BagRows.Capacity(10, 0));
            Assert.AreEqual(15, BagRows.Capacity(10, 1));
            Assert.AreEqual(25, BagRows.Capacity(20, 1), "Escape the House: 20 to 25");
            Assert.AreEqual(15, BagRows.Capacity(10, 4), "the limit holds even if asked for more");
            Assert.AreEqual(10, BagRows.Capacity(10, -1));
        }

        [Test]
        public void OnlyOneBonusRowPerPlayerAndNeverPastFiveRows()
        {
            Assert.AreEqual(1, BagRows.MostBonusRows, "the owner's limit lives in one constant");
            Assert.IsTrue(BagRows.CanAddRow(10, 0));
            Assert.IsFalse(BagRows.CanAddRow(10, 1), "a second bag is refused");
            Assert.IsTrue(BagRows.CanAddRow(20, 0), "Escape grows to 25");
            Assert.IsFalse(BagRows.CanAddRow(25, 0), "never more than five rows of five");
            Assert.IsFalse(BagRows.CanAddRow(10, -1));
            Assert.AreEqual(1, BagRows.AddRow(10, 0));
            Assert.AreEqual(1, BagRows.AddRow(10, 1), "a refused row changes nothing");
            Assert.AreEqual(25, BagRows.MostCells);
        }

        [Test]
        public void LettersAboveCapacityStayAndBlockPickUpsUntilThereIsRoom()
        {
            int capacity = BagRows.Capacity(10, 0);
            Assert.IsFalse(BagRows.CanPickUp(13, capacity), "13 letters kept in a 10-slot bag after the row is cleared");
            Assert.IsFalse(BagRows.CanPickUp(10, capacity));
            Assert.IsTrue(BagRows.CanPickUp(9, capacity));
            Assert.AreEqual(13, BagRows.ShownCells(capacity, 13), "every kept letter stays on screen");
            Assert.AreEqual(3, BagRows.RowsFor(BagRows.ShownCells(capacity, 13)));
            Assert.AreEqual(10, BagRows.ShownCells(10, 3));
            Assert.AreEqual(25, BagRows.ShownCells(40, 40));
        }

        [Test]
        public void RowsFollowTheBagFromTwoToFive()
        {
            Assert.AreEqual(2, BagRows.RowsFor(0));
            Assert.AreEqual(2, BagRows.RowsFor(10));
            Assert.AreEqual(3, BagRows.RowsFor(11));
            Assert.AreEqual(3, BagRows.RowsFor(15));
            Assert.AreEqual(4, BagRows.RowsFor(20), "Escape the House shows four rows");
            Assert.AreEqual(5, BagRows.RowsFor(25));
            Assert.AreEqual(5, BagRows.RowsFor(99));
            Assert.AreEqual(1, BagRows.RowOf(9));
            Assert.AreEqual(2, BagRows.RowOf(10));
        }

        [Test]
        public void TwoRowsKeepTodaysTrayNumbers()
        {
            Assert.AreEqual(176f, BagRows.TrayHeight(2));
            Assert.AreEqual(0f, BagRows.TrayLift(2));
            Assert.AreEqual(94f, BagRows.HandY(0, 2));
            Assert.AreEqual(0f, BagRows.HandY(1, 2));
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(i % 5 * 62f, BagRows.CellX(i), "cell " + i);
                Assert.AreEqual(-(i / 5) * 70f, BagRows.CellTop(i), "cell " + i);
            }
            Assert.AreEqual(276f, BagRows.VitalsAt(true, false, 2));
            Assert.AreEqual(38f, BagRows.VitalsAt(false, false, 2));
            Assert.AreEqual(250f, BagRows.VitalsAt(true, true, 5));
            Assert.AreEqual(302f, BagRows.LettersWide);
        }

        [Test]
        public void TheThirdRowTakesTheOldHintLineAndTheStackGrowsUp()
        {
            Assert.AreEqual(198f, BagRows.TrayHeight(3));
            Assert.AreEqual(268f, BagRows.TrayHeight(4));
            Assert.AreEqual(338f, BagRows.TrayHeight(5));
            for (int rows = 3; rows <= 5; rows++)
            {
                float lift = BagRows.TrayLift(rows);
                Assert.AreEqual(BagRows.TrayHeight(rows) - 176f, lift);
                Assert.AreEqual(94f + lift, BagRows.HandY(0, rows), "the item slots move up with the stack");
                Assert.AreEqual(lift, BagRows.HandY(1, rows));
                Assert.AreEqual(276f + lift, BagRows.VitalsAt(true, false, rows), "the health bar moves up with it");
                float lastBottom = BagRows.TrayHeight(rows) + BagRows.CellTop(rows * 5 - 1) - BagRows.TrayTileTall;
                Assert.AreEqual(0f, lastBottom, 1e-3f, rows + " rows: the last row sits on the tray floor, where the hint chips were");
            }
        }

        [Test]
        public void EveryRowCountFitsAtTheThreeSizesWithoutCoveringTheHealthBarSlotsOrMap()
        {
            foreach (var (w, h) in Screens)
            {
                var (width, height) = Logical(w, h);
                var map = BagRows.Minimap(width, height);
                float centre = height * .5f, promptBottom = centre - 11f - 22f - 15f;
                for (int rows = 2; rows <= 5; rows++)
                {
                    foreach (bool aside in new[] { false, true })
                    {
                        foreach (bool touch in new[] { false, true })
                        {
                            string at = $"{w}x{h} rows {rows} aside {aside} touch {touch}";
                            var f = BagRows.Frame(rows, width, aside, touch);
                            Assert.IsTrue(f.Tray.Inside(width, height), at + " tray " + f.Tray);
                            Assert.IsTrue(f.SpellHint.Inside(width, height), at + " hint " + f.SpellHint);
                            Assert.IsFalse(f.Tray.Overlaps(f.Vitals), at + " tray vs health " + f.Tray + " " + f.Vitals);
                            Assert.IsFalse(f.SpellHint.Overlaps(f.Vitals), at + " hint vs health");
                            Assert.IsFalse(f.Tray.Overlaps(map), at + " tray vs map");
                            Assert.IsFalse(f.Vitals.Overlaps(map), at + " health vs map");
                            Assert.IsFalse(f.Letters.Overlaps(f.Hands), at + " letters vs item slots");
                            Assert.LessOrEqual(f.SpellHint.Top, promptBottom, at + " the stack stays under the aim prompt");
                            Assert.LessOrEqual(f.Letters.Height, f.Tray.Height + 1e-3f, at);
                            Assert.GreaterOrEqual(f.Letters.Y, f.Tray.Y - 1e-3f, at + " no row hangs below the tray");
                            Assert.GreaterOrEqual(TrayFont * f.Tray.Width / BagRows.TrayWidth, 15f, at + " letters stay readable");
                        }
                    }
                }
            }
        }

        [Test]
        public void TwoRowsInTheBagPanelKeepTodaysMapAndLetterScale()
        {
            float gridTall = BagRows.GridTall(2, LetterStepY, LetterTall);
            Assert.AreEqual(127.5f, gridTall);
            foreach (var (w, h) in Screens)
            {
                var (_, height) = Logical(w, h);
                foreach (float effects in new[] { 0f, 52f })
                {
                    float rest = LetterFoot + BagGap + HandTall + (effects > 0f ? BagGap + effects : 0f);
                    foreach (float most in new[] { float.MaxValue, 400f, 330f })
                    {
                        float free = height - BagTop - BagBottom;
                        float map = BagDrag.MapSide(free, rest, BagGap, MapFloor, most, BagColumn, LetterMargin, GridWide, gridTall);
                        float scale = BagDrag.LetterScale(Math.Max(map, BagColumn), LetterMargin, GridWide);
                        var fit = BagRows.Fit(free, rest, BagGap, MapFloor, most, BagColumn, LetterMargin, GridWide, gridTall);
                        Assert.AreEqual(map, fit.Map, $"{w}x{h} map");
                        Assert.AreEqual(scale, fit.Scale, $"{w}x{h} letter scale");
                    }
                }
            }
        }

        [Test]
        public void ThreeToFiveRowsInTheBagPanelFitTheColumnAndStayReadable()
        {
            foreach (var (w, h) in Screens)
            {
                var (_, height) = Logical(w, h);
                float free = height - BagTop - BagBottom;
                for (int rows = 2; rows <= 5; rows++)
                {
                    float gridTall = BagRows.GridTall(rows, LetterStepY, LetterTall);
                    foreach (float effects in new[] { 0f, 52f })
                    {
                        float rest = LetterFoot + BagGap + HandTall + (effects > 0f ? BagGap + effects : 0f);
                        var fit = BagRows.Fit(free, rest, BagGap, MapFloor, float.MaxValue, BagColumn, LetterMargin, GridWide, gridTall);
                        string at = $"{w}x{h} rows {rows} effects {effects}";
                        float column = fit.Map + BagGap + gridTall * fit.Scale + rest;
                        Assert.LessOrEqual(column, free + 1f, at + " the column fits under the map");
                        Assert.GreaterOrEqual(fit.Map, MapFloor - 1e-3f, at + " the map keeps its floor");
                        Assert.GreaterOrEqual(LetterFont * fit.Scale, 15f, at + " letters at least 15 pt");
                        Assert.LessOrEqual(GridWide * fit.Scale + LetterMargin, Math.Max(fit.Map, BagColumn) + 1e-3f, at + " the grid stays in its column");
                    }
                }
            }
        }

        [Test]
        public void TheNewRowPopsOnceAndSettles()
        {
            Assert.AreEqual(1f, BagRows.RowPop(-1f));
            Assert.AreEqual(1f, BagRows.RowPop(BagRows.RowPopSeconds));
            Assert.Greater(BagRows.RowPop(BagRows.RowPopSeconds * .5f), 1.1f);
            Assert.LessOrEqual(BagRows.RowPop(BagRows.RowPopSeconds * .5f), 1f + BagRows.RowPopGrow + 1e-4f);
        }

        [Test]
        public void ThePlayerTextIsShortAndPlain()
        {
            Assert.AreEqual("Adds a row of letter slots for this round. Takes no hand.", BagRows.CardLine);
            foreach (string line in new[] { BagRows.CardLine, BagRows.FullLine, BagRows.Kind, BagRows.Chip, BagRows.GainedPopup, BagRows.FullPopup })
            {
                Assert.IsFalse(line.Contains("—") || line.Contains("–"), line);
                Assert.LessOrEqual(line.Length, 60, line);
            }
        }
    }
}
