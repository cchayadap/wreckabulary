using System.Collections.Generic;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    /// <summary>The held-skin lifecycle (brief §4).</summary>
    [TestFixture]
    public class AppearanceTests
    {
        Economy e;
        Dictionary<int, string> batSkins;

        [SetUp]
        public void Make()
        {
            e = TestData.NewEconomy("Duos", 4, new[] { 0, 1, 0, 1 });
            // Player 0 likes Candy, 2 likes Arcade, 1 picked a skin the BAT doesn't have, 3 picked nothing.
            batSkins = new Dictionary<int, string> { { 0, "Candy" }, { 1, "Gold" }, { 2, "Arcade" } };
        }

        string Chosen(int player, string word) => word == "BAT" && batSkins.TryGetValue(player, out string s) ? s : null;

        ItemVisual Look(ItemInstance item) => Appearance.Resolve(item, e.Catalogue.Get(item.Word), Chosen);

        [Test]
        public void EveryStateShowsTheRightSkinAndSize()
        {
            var bat = TestData.Craft(e, 0, "BAT");
            AssertLook(Look(bat), "Candy", true, "held by its crafter");

            Assert.IsTrue(e.Transfer(0, 2, 0).Ok);
            AssertLook(Look(bat), "Arcade", true, "transferred: the new holder's skin");

            Assert.IsTrue(e.Throw(2, 0, Chosen(2, "BAT")).Ok);
            AssertLook(Look(bat), "Arcade", true, "thrown: the thrower's skin");
            batSkins[2] = "Candy";
            AssertLook(Look(bat), "Arcade", true, "in flight it keeps the skin it was thrown with");

            Assert.IsTrue(e.Settle(bat.Id).Ok);
            AssertLook(Look(bat), "Classic", false, "settled: standard");

            Assert.IsTrue(e.PickUp(0, bat.Id, bat.Revision).Ok);
            AssertLook(Look(bat), "Candy", true, "picked up again");

            Assert.IsTrue(e.Drop(0, 0).Ok);
            AssertLook(Look(bat), "Classic", false, "dropped: standard");
        }

        [Test]
        public void DeployedItemsAreStandardAndFullSize()
        {
            var table = TestData.Craft(e, 0, "TABLE");
            Assert.IsTrue(Look(table).Miniature);
            Assert.IsTrue(e.Deploy(0, 0).Ok);
            AssertLook(Look(table), "Classic", false, "deployed");
        }

        [Test]
        public void MissingOrUnknownSkinsFallBackToStandard()
        {
            var bat = TestData.Craft(e, 1, "BAT");
            AssertLook(Look(bat), "Classic", true, "BAT has no Gold skin");
            Assert.IsTrue(e.Transfer(1, 3, 0).Ok);
            AssertLook(Look(bat), "Classic", true, "player 3 chose nothing");
            Assert.AreEqual("Classic", Appearance.Pick(null, "Candy"), "no definition at all");
            Assert.AreEqual("Classic", Appearance.Resolve(bat, e.Catalogue.Get("BAT"), null).Skin, "no skin lookup at all");
        }

        [Test]
        public void ThrownFurnitureStaysStandard()
        {
            var chair = e.PlaceFurniture("CHAIR");
            Assert.IsTrue(e.PickUp(0, chair.Id, chair.Revision).Ok);
            AssertLook(Appearance.Resolve(chair, e.Catalogue.Get("CHAIR"), Chosen), "Classic", false, "carried furniture");
            Assert.IsTrue(e.Throw(0, -1, "Candy").Ok);
            AssertLook(Appearance.Resolve(chair, e.Catalogue.Get("CHAIR"), Chosen), "Classic", false, "thrown furniture");
        }

        [Test]
        public void CollisionNeverDependsOnTheSkin()
        {
            var bat = TestData.Craft(e, 0, "BAT");
            string held = Look(bat).CollisionProfile;
            Assert.IsTrue(e.Transfer(0, 2, 0).Ok);
            Assert.AreEqual(held, Look(bat).CollisionProfile, "Candy and Arcade collide the same");
            Assert.IsTrue(e.Transfer(2, 0, 0).Ok);
            batSkins[0] = null;
            Assert.AreEqual(held, Look(bat).CollisionProfile, "Classic collides the same");
            Assert.IsTrue(e.Drop(0, 0).Ok);
            Assert.AreEqual("BAT_world", Look(bat).CollisionProfile);
            Assert.AreEqual("BAT_held", held);
        }

        static void AssertLook(ItemVisual v, string skin, bool miniature, string when)
        {
            Assert.AreEqual(skin, v.Skin, when + ": skin");
            Assert.AreEqual(miniature, v.Miniature, when + ": size");
        }
    }
}
