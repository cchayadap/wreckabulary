using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class TrayDragTests
    {
        [Test]
        public void ATileOrSlotUnderThePointerIsATarget()
        {
            Assert.AreEqual((BagSpot.Letter, 3), BagDrag.TraySpot(3, -1, true));
            Assert.AreEqual((BagSpot.Item, 1), BagDrag.TraySpot(-1, 1, true));
            Assert.AreEqual((BagSpot.Letter, 0), BagDrag.TraySpot(0, 1, true), "a letter tile wins if both report a hit");
        }

        [Test]
        public void InsideTheBlockButOffATileGoesBack()
        {
            Assert.AreEqual((BagSpot.None, -1), BagDrag.TraySpot(-1, -1, true));
            Assert.AreEqual(BagMoveKind.Back, BagDrag.Resolve(BagThing.Letter, 2, BagSpot.None, -1, 4).Kind);
            Assert.AreEqual(BagMoveKind.Back, BagDrag.Resolve(BagThing.Item, 0, BagSpot.None, -1, 0).Kind);
        }

        [Test]
        public void AnywhereOutsideTheBlockDrops()
        {
            var (spot, index) = BagDrag.TraySpot(-1, -1, false);
            Assert.AreEqual(BagSpot.Drop, spot);
            Assert.AreEqual(-1, index, "no drop column is named, so none lights");
            Assert.AreEqual(BagMoveKind.Drop, BagDrag.Resolve(BagThing.Letter, 2, spot, index, 4).Kind);
            Assert.AreEqual(2, BagDrag.Resolve(BagThing.Letter, 2, spot, index, 4).To, "the dragged letter is the one dropped");
            Assert.AreEqual(BagMoveKind.Drop, BagDrag.Resolve(BagThing.Item, 1, spot, index, 0).Kind);
        }

        [Test]
        public void TheTrayReordersAndSwapsLikeTheBag()
        {
            var (letter, at) = BagDrag.TraySpot(0, -1, true);
            Assert.AreEqual(BagMoveKind.Reorder, BagDrag.Resolve(BagThing.Letter, 2, letter, at, 3).Kind);
            var (item, slot) = BagDrag.TraySpot(-1, 1, true);
            Assert.AreEqual(BagMoveKind.Swap, BagDrag.Resolve(BagThing.Item, 0, item, slot, 0).Kind);
            Assert.AreEqual(BagMoveKind.Back, BagDrag.Resolve(BagThing.Letter, 1, item, slot, 3).Kind, "a letter on an item slot goes back");
        }

        [Test]
        public void TheBinShowsOnlyWhenLettingGoWouldDrop()
        {
            Assert.IsTrue(BagDrag.ShowsBin(BagSpot.Drop, true));
            Assert.IsFalse(BagDrag.ShowsBin(BagSpot.Drop, false), "a refused drop shows no bin");
            Assert.IsFalse(BagDrag.ShowsBin(BagSpot.None, true));
            Assert.IsFalse(BagDrag.ShowsBin(BagSpot.Letter, true));
            Assert.IsFalse(BagDrag.ShowsBin(BagSpot.Item, true));
        }

        [Test]
        public void TheTrayTakesDragsOnlyWhenThePointerIsFreeAndPlayIsOn()
        {
            Assert.IsTrue(BagDrag.TrayAccepts(false, true, false, false, false, true));
            Assert.IsFalse(BagDrag.TrayAccepts(true, true, false, false, false, true), "the open bag has its own drag");
            Assert.IsFalse(BagDrag.TrayAccepts(false, false, false, false, false, true), "paused or another screen");
            Assert.IsFalse(BagDrag.TrayAccepts(false, true, true, false, false, true), "a result card covers the HUD");
            Assert.IsFalse(BagDrag.TrayAccepts(false, true, false, true, false, true), "the spell box is open");
            Assert.IsFalse(BagDrag.TrayAccepts(false, true, false, false, true, true), "the cursor is locked to the camera");
            Assert.IsFalse(BagDrag.TrayAccepts(false, true, false, false, false, false), "a pad seat has no pointer");
        }

        [Test]
        public void APressThatStartsOnTheHudBelongsToItUntilLetGo()
        {
            bool owned = DesktopScheme.HudOwnsPress(true, true, true, false);
            Assert.IsTrue(owned, "pressed on the HUD");
            owned = DesktopScheme.HudOwnsPress(false, true, false, owned);
            Assert.IsTrue(owned, "still held after leaving the HUD");
            owned = DesktopScheme.HudOwnsPress(false, false, false, owned);
            Assert.IsFalse(owned, "let go ends it");
            owned = DesktopScheme.HudOwnsPress(true, true, false, owned);
            Assert.IsFalse(owned, "a press in the world is an attack");
            owned = DesktopScheme.HudOwnsPress(false, true, true, owned);
            Assert.IsFalse(owned, "sliding onto the HUD later does not take it");
        }
    }
}
