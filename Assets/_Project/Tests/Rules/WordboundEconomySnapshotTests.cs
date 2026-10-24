using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Tests;

namespace Wreckabulary.Rules.Tests
{
    public sealed class WordboundEconomySnapshotTests
    {
        static MatchSnapshot WithEconomy(MatchSnapshot snapshot, EconomySnapshot economy) =>
            new MatchSnapshot(snapshot.Binding, snapshot.Epoch, snapshot.Sequence, snapshot.NextWorldEntityId,
                snapshot.Progress, economy, snapshot.Commands, snapshot.Actors, snapshot.Bodies, snapshot.Resources,
                snapshot.Mechanisms, snapshot.RandomStreams, snapshot.NarrativeFlags, snapshot.ThrownFlights, snapshot.Practice);

        static Economy Destination(WordboundSnapshotCodecTests.Fixture f)
        {
            var result = new Economy(f.Catalogue, f.Rules);
            for (int i = 0; i < f.Binding.Seats.Count; i++) result.AddPlayer(i, f.Binding.Seats[i].Team);
            return result;
        }

        [Test]
        public void ValidationOnlyEconomyAcceptsRetainedCraftAndConsumptionStatesWithoutInstallingThem()
        {
            var f = WordboundAuthorityTests.Create();
            var retained = new List<(MatchSnapshot Snapshot, string Json)>();
            void Check()
            {
                var snapshot = f.State;
                string before = MatchSnapshotCodec.Export(snapshot);
                var restored = Destination(f.Source);
                restored.MintTiles("Q");
                restored.Restore(snapshot.Economy);
                Assert.AreEqual(before, MatchSnapshotCodec.Export(WithEconomy(snapshot, restored.Capture())));
                Assert.AreEqual("", MatchStateValidation.Validate(snapshot, f.Source.Binding, f.Source.Mode,
                    f.Source.Course, f.Source.Catalogue, f.Source.Rules));
                Assert.AreEqual(before, MatchSnapshotCodec.Export(f.State));
                retained.Add((snapshot, before));
            }
            Check();
            string source = f.Source.Course.Supplies.Single(s => s.SourceId == "BedSupply").Id;
            foreach (var body in f.State.Bodies.Where(b => b.SourceId == source).ToArray())
                Assert.AreEqual("Ok", f.Collect("a", body.Id).Effect.Code);
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.BeginCraft, value: "BED")).Receipts.Single().Effect.Code);
            Assert.IsNotNull(f.State.Economy.Players[0].Craft);
            Check();
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.CancelCraft)).Receipts.Single().Effect.Code);
            Assert.IsNull(f.State.Economy.Players[0].Craft);
            Check();
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.BeginCraft, value: "BED")).Receipts.Single().Effect.Code);
            f.Advance(61);
            var item = f.State.Economy.Items.Single(i => i.Word == "BED" && i.Origin == ItemOrigin.Crafted);
            Assert.AreEqual(ItemState.Held, item.State);
            Check();
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.Deploy, slot: 0)).Receipts.Single().Effect.Code);
            Assert.AreEqual(ItemState.Deployed, f.State.Economy.Items.Single(i => i.Id == item.Id).State);
            Check();
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.UsePuzzle, "BedBridge", value: "item:" + item.Id)).Receipts.Single().Effect.Code);
            Assert.IsTrue(f.State.Economy.Items.Single(i => i.Id == item.Id).IsGone);
            Check();
            string current = MatchSnapshotCodec.Export(f.State);
            foreach (var earlier in retained)
            {
                Assert.AreEqual("", MatchStateValidation.Validate(earlier.Snapshot, f.Source.Binding, f.Source.Mode,
                    f.Source.Course, f.Source.Catalogue, f.Source.Rules));
                Assert.AreEqual(earlier.Json, MatchSnapshotCodec.Export(earlier.Snapshot));
            }
            Assert.AreEqual(current, MatchSnapshotCodec.Export(f.State));
        }

        [TestCase("count", "counts or allocation frontiers")]
        [TestCase("roster", "roster or inventory")]
        [TestCase("slot", "roster or inventory")]
        [TestCase("reservation", "orphan reserved letters")]
        [TestCase("craft", "pending craft reservation")]
        [TestCase("ownership", "letter conservation or item ownership")]
        [TestCase("conservation", "letter conservation or item ownership")]
        public void ValidationOnlyEconomyRetainsRestoreFailuresBeforeLaterSnapshotErrors(string mutation, string expected)
        {
            var f = WordboundSnapshotCodecTests.Create("TimeRun");
            var original = f.Snapshot();
            string originalBytes = MatchSnapshotCodec.Export(original);
            Assert.AreEqual("", MatchStateValidation.Validate(original, f.Binding, f.Mode, f.Course, f.Catalogue, f.Rules));
            var source = Destination(f);
            if (mutation == "count")
            {
                source = new Economy(f.Catalogue, f.Rules);
                source.AddPlayer(0, f.Binding.Seats[0].Team);
            }
            else if (mutation == "roster")
            {
                source = new Economy(f.Catalogue, f.Rules);
                for (int i = 0; i < f.Binding.Seats.Count; i++) source.AddPlayer(i, f.Binding.Seats[i].Team + 1);
            }
            else if (mutation == "slot") source.Player(0).Slots[0] = 0;
            else if (mutation == "reservation") source.Player(0).Reserved.Add('X');
            else if (mutation == "craft")
            {
                TestData.GiveLetters(source, 0, "BAT");
                Assert.IsTrue(source.BeginCraft(0, "BAT", 0).Ok);
                source.Player(0).Craft.ReadyAt = -1;
            }
            else if (mutation == "ownership")
            {
                var item = TestData.Craft(source, 0, "BAT");
                source.Player(1).Slots[0] = item.Id;
            }
            else source.Player(0).Letters.Add('X');
            var malformed = source.Capture();
            var destination = Destination(f);
            TestData.Craft(destination, 0, "BAT");
            int notifications = 0;
            destination.ItemChanged += _ => notifications++;
            string destinationBytes = MatchSnapshotCodec.Export(WithEconomy(original, destination.Capture()));
            var failure = Assert.Throws<ArgumentException>(() => destination.Restore(malformed));
            Assert.AreEqual("Invalid economy snapshot: " + expected + ".", failure.Message);
            Assert.AreEqual(destinationBytes, MatchSnapshotCodec.Export(WithEconomy(original, destination.Capture())));
            Assert.AreEqual(0, notifications);
            f.Resources.Clear();
            var competing = WithEconomy(f.Snapshot(), malformed);
            string malformedBytes = MatchSnapshotCodec.Export(competing);
            for (int i = 0; i < 2; i++)
                Assert.AreEqual(failure.Message, MatchStateValidation.Validate(competing, f.Binding, f.Mode,
                    f.Course, f.Catalogue, f.Rules));
            Assert.AreEqual(malformedBytes, MatchSnapshotCodec.Export(competing));
            Assert.AreEqual(originalBytes, MatchSnapshotCodec.Export(original));
            Assert.AreEqual("", MatchStateValidation.Validate(original, f.Binding, f.Mode, f.Course, f.Catalogue, f.Rules));
        }

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
