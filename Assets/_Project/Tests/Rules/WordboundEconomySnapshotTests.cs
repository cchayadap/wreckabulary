using System;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public sealed class WordboundEconomySnapshotTests
    {
        [Test]
        public void CanonicalCountsRetainDestroyedItemsAtTheLimitAndSeparateCollectedLetters()
        {
            var economy = TestData.NewEconomy();
            int limit = WordboundEntityBudget.MaximumCanonicalEntities;
            for (int i = 0; i < limit; i++) economy.Break(economy.PlaceFurniture("A").Id);
            Assert.AreEqual(limit, economy.ItemCount);
            Assert.AreEqual(limit, economy.Tiles.Count);
            Assert.IsTrue(economy.Items.All(item => item.IsGone), "Destroyed ledger entries still consume canonical capacity.");
            var retained = economy.Capture();
            Assert.AreEqual(retained.Items.Count, economy.ItemCount);
            Assert.AreEqual(retained.Tiles.Count, economy.Tiles.Count);
            int notifications = 0;
            economy.ItemChanged += _ => notifications++;
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(limit, economy.ItemCount);
                Assert.AreEqual(limit, economy.Tiles.Count);
            }
            Assert.AreEqual(0, notifications);
            Assert.IsTrue(economy.CollectTile(0, economy.Tiles.Keys.First()).Ok);
            Assert.AreEqual(limit - 1, economy.Tiles.Count);
            Assert.AreEqual(1, economy.Player(0).LetterCount);
            economy.PlaceFurniture("A");
            Assert.AreEqual(limit + 1, economy.ItemCount);
            Assert.Greater(economy.ItemCount, limit);
            Assert.AreEqual(limit - 1, economy.Tiles.Count);
            economy.MintTiles("AA");
            Assert.AreEqual(limit + 1, economy.Tiles.Count);
            Assert.Greater(economy.Tiles.Count, limit);
            Assert.AreEqual(limit, retained.Items.Count);
            Assert.AreEqual(limit, retained.Tiles.Count);
            var recovered = TestData.NewEconomy();
            recovered.Restore(retained);
            Assert.AreEqual(limit, recovered.ItemCount);
            Assert.AreEqual(limit, recovered.Tiles.Count);
            Assert.AreEqual(0, recovered.Player(0).LetterCount);
            Assert.AreEqual(limit + 1, economy.ItemCount);
            Assert.AreEqual(limit + 1, economy.Tiles.Count);
            TestData.AssertConserved(economy);
            TestData.AssertConserved(recovered);
        }

        [Test]
        public void CanonicalItemCountFollowsCraftCompletionAndRestoreWithoutCountingReservations()
        {
            var economy = TestData.NewEconomy();
            TestData.GiveLetters(economy, 0, "BAT");
            Assert.IsTrue(economy.BeginCraft(0, "BAT", 10).Ok);
            var pending = economy.Capture();
            Assert.AreEqual(0, economy.ItemCount);
            Assert.AreEqual(0, economy.Tiles.Count);
            Assert.AreEqual(3, economy.Player(0).Reserved.Count);
            var crafted = economy.CompleteCraft(0, 100);
            Assert.IsTrue(crafted.Ok);
            Assert.AreEqual(ItemState.Held, crafted.Item.State);
            Assert.AreEqual(1, economy.ItemCount);
            economy.Break(crafted.Item.Id);
            Assert.AreEqual(1, economy.ItemCount);
            Assert.AreEqual(3, economy.Tiles.Count);
            economy.Restore(pending);
            Assert.AreEqual(0, economy.ItemCount);
            Assert.AreEqual(0, economy.Tiles.Count);
            Assert.AreEqual(3, economy.Player(0).Reserved.Count);
            Assert.IsTrue(economy.CompleteCraft(0, 100).Ok);
            Assert.AreEqual(1, economy.ItemCount);
            Assert.AreEqual(0, pending.Items.Count);
            Assert.AreEqual(3, pending.Players[0].Reserved.Count);
            TestData.AssertConserved(economy);
        }

        [Test]
        public void MidCraftRestorePreservesReservationAndCreatesExactlyOneItem()
        {
            var original = TestData.NewEconomy();
            TestData.GiveLetters(original, 0, "BATXL");
            original.BeginCraft(0, "BAT", 10);
            var snapshot = original.Capture();
            var recovered = TestData.NewEconomy();
            recovered.Restore(snapshot);
            Assert.AreEqual("ABT", recovered.Player(0).Reserved.ToString());
            Assert.AreEqual("LX", recovered.Player(0).Letters.ToString());
            Assert.AreEqual(original.Player(0).Craft.ReadyAt, recovered.Player(0).Craft.ReadyAt);
            Assert.AreEqual(Refusal.TooEarly, recovered.CompleteCraft(0, 10).Refusal);
            var done = recovered.CompleteCraft(0, 20);
            Assert.IsTrue(done.Ok);
            Assert.AreEqual(snapshot.NextItemId, done.Item.Id);
            Assert.AreEqual(Refusal.NotCrafting, recovered.CompleteCraft(0, 20).Refusal);
            Assert.AreEqual(1, recovered.Items.Count());
            TestData.AssertConserved(recovered);
        }

        [Test]
        public void CompletedCraftRestoreDoesNotSpendOrSpawnAgain()
        {
            var original = TestData.NewEconomy();
            var crafted = TestData.Craft(original, 0, "BAT");
            var snapshot = original.Capture();
            var recovered = TestData.NewEconomy();
            int notifications = 0;
            recovered.ItemChanged += _ => notifications++;
            recovered.Restore(snapshot);
            Assert.AreEqual(0, notifications, "restore must not replay external spawn callbacks");
            Assert.AreEqual(crafted.Id, recovered.Player(0).Slots[0]);
            Assert.AreEqual(crafted.Revision, recovered.Item(crafted.Id).Revision);
            Assert.AreEqual(Refusal.NotCrafting, recovered.CompleteCraft(0, 100).Refusal);
            Assert.AreEqual(1, recovered.Items.Count());
            Assert.AreEqual(snapshot.Minted, recovered.Minted);
            TestData.AssertConserved(recovered);
        }

        [Test]
        public void DestroyedItemsAndCollectedTilesKeepTheirAllocationFrontiers()
        {
            var original = TestData.NewEconomy();
            var furniture = original.PlaceFurniture("TABLE");
            var loose = original.Break(furniture.Id);
            original.CollectTile(0, loose.Tiles[0].TileId);
            var snapshot = original.Capture();
            var recovered = TestData.NewEconomy();
            recovered.Restore(snapshot);
            Assert.IsTrue(recovered.Item(furniture.Id).IsGone);
            Assert.AreEqual(0, recovered.Break(furniture.Id).Count);
            Assert.AreEqual(Refusal.NoSuchTile, recovered.CollectTile(1, loose.Tiles[0].TileId).Refusal);
            var next = recovered.MintTiles("A").Tiles.Single();
            Assert.AreEqual(snapshot.NextTileId, next.TileId);
            Assert.AreEqual(snapshot.NextItemId, recovered.PlaceFurniture("STOOL").Id);
            TestData.AssertConserved(recovered);
        }

        [Test]
        public void SnapshotAndPublicReadModelsDoNotAliasLiveState()
        {
            var original = TestData.NewEconomy();
            TestData.GiveLetters(original, 0, "BAT");
            original.BeginCraft(0, "BAT", 2);
            var snapshot = original.Capture();
            var exposed = snapshot.Players[0];
            exposed.Reserved.TakeAll();
            exposed.Slots[0] = 999;
            exposed.Craft.ReadyAt = 100000;
            original.CancelCraft(0);
            var recovered = TestData.NewEconomy();
            recovered.Restore(snapshot);
            Assert.AreEqual("ABT", recovered.Player(0).Reserved.ToString());
            Assert.AreEqual(-1, recovered.Player(0).Slots[0]);
            Assert.IsTrue(recovered.CompleteCraft(0, 10).Ok);
            TestData.AssertConserved(recovered);
        }

        [Test]
        public void InvalidReservationAndOwnershipRejectWithoutChangingDestination()
        {
            var destination = TestData.NewEconomy();
            var held = TestData.Craft(destination, 0, "BAT");
            var malformed = TestData.NewEconomy();
            TestData.GiveLetters(malformed, 0, "BAT");
            malformed.BeginCraft(0, "BAT", 10);
            malformed.Player(0).Craft.ReadyAt = double.NaN;
            Assert.Throws<ArgumentException>(() => destination.Restore(malformed.Capture()));
            Assert.AreEqual(held.Id, destination.Player(0).Slots[0]);
            Assert.AreEqual(1, destination.Items.Count());
            var badOwner = TestData.NewEconomy();
            TestData.Craft(badOwner, 0, "BAT");
            badOwner.Player(1).Slots[0] = badOwner.Player(0).Slots[0];
            Assert.Throws<ArgumentException>(() => destination.Restore(badOwner.Capture()));
            TestData.AssertConserved(destination);
        }

        [Test]
        public void ForeignRosterAndUnbalancedLettersRejectBeforeMutation()
        {
            var destination = TestData.NewEconomy();
            var foreign = TestData.NewEconomy(players: 2, teams: new[] { 1, 0 });
            Assert.Throws<ArgumentException>(() => destination.Restore(foreign.Capture()));
            var counterfeit = TestData.NewEconomy();
            counterfeit.Player(0).Letters.Add('A');
            Assert.Throws<ArgumentException>(() => destination.Restore(counterfeit.Capture()));
            Assert.AreEqual(0, destination.Minted);
            TestData.AssertConserved(destination);
        }

        [Test]
        public void EliminationRestoreKeepsDroppedItemsAndReturnedReservationConsistent()
        {
            var original = TestData.NewEconomy();
            var held = TestData.Craft(original, 0, "BAT");
            TestData.GiveLetters(original, 0, "FAN");
            original.BeginCraft(0, "FAN", 10);
            original.Eliminate(0);
            var snapshot = original.Capture();
            var recovered = TestData.NewEconomy();
            recovered.Restore(snapshot);
            Assert.IsFalse(recovered.Player(0).CanAct);
            Assert.IsNull(recovered.Player(0).Craft);
            Assert.AreEqual(ItemState.World, recovered.Item(held.Id).State);
            Assert.AreEqual(3, recovered.Tiles.Count);
            Assert.AreEqual(0, recovered.Eliminate(0).Count);
            Assert.AreEqual(3, recovered.Tiles.Count);
            TestData.AssertConserved(recovered);
        }
    }
}
