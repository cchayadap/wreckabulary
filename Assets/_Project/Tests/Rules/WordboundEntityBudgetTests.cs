using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundEntityBudgetTests
    {
        static WordboundAuthorityTests.Fixture Create(bool race, bool pressure = true, int optionalSources = 0)
        {
            var f = WordboundAuthorityTests.Create(race ? "TeamRace" : "Freestyle");
            f.Source.Mode = race ? ModeDefinition.For("TeamRace", RaceFormat.Squad) : ModeDefinition.For("Freestyle");
            f.Source.Rules = f.Source.Mode.CreateGameplayRules(f.Source.Rules);
            f.Source.Rules.CraftBaseSeconds = f.Source.Rules.CraftPerLetterSeconds = 0;
            var draft = race ? CourseTemplates.RaceCircuit() : CourseTemplates.FreestyleWorld();
            for (int i = 0; i < optionalSources; i++)
                draft.Supplies.Add(new CourseSupply { Id = "Practice" + i.ToString("D3"), ModuleId = "Start",
                    Position = new CoursePoint(0, 0, -4500), Letters = "BED", Renewable = i != optionalSources - 1 });
            var resolved = CourseResolver.Resolve(draft, CourseTemplates.ProofCatalogue(),
                new CourseResolveRequest(f.Source.Course.Seed, MatchStateValidation.Requirements(f.Source.Mode)));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            f.Source.Course = resolved.Manifest;
            var budget = new WordboundEntityBudget(f.Source.Mode, f.Source.Course, CourseTemplates.ProofCatalogue(),
                f.Source.Catalogue, f.Source.Rules);
            if (pressure)
            {
                int capacity = race ? f.Source.Mode.TeamCapacity : f.Source.Mode.MaximumPlayers;
                f.Source.Rules.MaxLetters += Math.Max(0, (budget.OptionalBodies - 8) / capacity);
            }
            var b = f.Source.Binding;
            f.Source.Binding = new MatchBinding(b.MatchId, MatchSnapshotCodec.ProtocolVersion, f.Source.Mode.Hash,
                f.Source.Course.SourceHash, f.Source.Course.Hash, b.RosterHash, b.CatalogueHash, b.Seats);
            f.Match = new AuthoritativeMatch(f.Source.Binding, f.Source.Mode, f.Source.Course,
                CourseTemplates.ProofCatalogue(), f.Source.Catalogue, f.Source.Rules, f.World);
            return f;
        }

        static string Craft(WordboundAuthorityTests.Fixture f, string actor = "a", string sourceId = "BedSupply")
        {
            var seat = f.Source.Binding.Seats.Single(s => s.PlayerId == actor);
            var source = f.Source.Course.Supplies.Single(s => s.SourceId == sourceId &&
                (!f.Source.Mode.IsRace || s.TeamIndex == seat.Team));
            var tiles = f.State.Bodies.Where(b => b.SourceId == source.Id).ToArray();
            Assert.AreEqual(3, tiles.Length, sourceId);
            foreach (var tile in tiles) Assert.AreEqual("Ok", f.Collect(actor, tile.Id).Effect.Code);
            Assert.AreEqual("Ok", f.Step(f.Command(actor, WordboundAction.BeginCraft, value: "BED")).Receipts.Single().Effect.Code);
            f.Step();
            return "item:" + f.State.Economy.Items.Last(i => i.Origin == ItemOrigin.Crafted && i.HolderId == (actor == "a" ? 0 : 1)).Id;
        }

        static void Refill(WordboundAuthorityTests.Fixture f)
        {
            f.Advance(601);
            Assert.LessOrEqual(f.State.Bodies.Count, WordboundEntityBudget.MaximumBodies);
        }

        [Test]
        public void BudgetIsPinnedAndRejectsRulesThatCannotReserveMandatoryDrops()
        {
            var f = Create(true, false);
            var budget = f.Match.EntityBudget;
            Assert.AreEqual(512, WordboundEntityBudget.MaximumBodies);
            Assert.AreEqual(8192, WordboundEntityBudget.MaximumCanonicalEntities);
            Assert.IsTrue(WordboundIdentity.IsHash(WordboundEntityBudget.Hash));
            Assert.AreEqual(2, budget.PoolCount);
            Assert.Greater(budget.OptionalBodies, 0);
            Assert.LessOrEqual(budget.FixedBodies + budget.PoolCount * budget.PoolBodies, 512);
            f.Source.Rules.MaxLetters = 10000;
            Assert.Throws<ArgumentException>(() => new AuthoritativeMatch(f.Source.Binding, f.Source.Mode,
                f.Source.Course, CourseTemplates.ProofCatalogue(), f.Source.Catalogue, f.Source.Rules, f.World));
        }

        [Test]
        public void PinnedThrowsRefuseOptionalReleaseWithoutBlockingCriticalCraftOrRivalTeam()
        {
            var f = Create(true);
            int allowance = f.Match.EntityBudget.OptionalBodies;
            for (int i = 0; i < allowance; i++)
            {
                Craft(f);
                Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.Throw, slot: 0)).Receipts.Single().Effect.Code);
                Refill(f);
            }
            string held = Craft(f);
            var command = f.Command("a", WordboundAction.Throw, slot: 0);
            var refusal = f.Step(command).Receipts.Single();
            Assert.AreEqual("Capacity", refusal.Effect.Code);
            Assert.AreEqual(ItemState.Held, f.State.Economy.Items.Single(i => "item:" + i.Id == held).State);
            Assert.AreEqual(allowance, f.State.Economy.Items.Count(i => i.State == ItemState.Thrown && i.CrafterId == 0));
            Assert.AreEqual(refusal.Tick, f.Step(command).Receipts.Single().Tick);
            Craft(f, "b");
            Assert.AreEqual("Ok", f.Step(f.Command("b", WordboundAction.Throw, slot: 0)).Receipts.Single().Effect.Code);
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.Deploy, slot: 0)).Receipts.Single().Effect.Code);
            Assert.IsTrue(f.State.Bodies.Single(b => b.Id == held).Active);
            f.Ack();
        }

        [Test]
        public void OptionalLooseEquipmentRecyclesInStableOrderWithoutReusingIds()
        {
            var f = Create(true);
            var ids = new List<string>();
            for (int i = 0; i < f.Match.EntityBudget.OptionalBodies + 6; i++)
            {
                string id = Craft(f);
                ids.Add(id);
                Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.Drop, slot: 0)).Receipts.Single().Effect.Code);
                Refill(f);
            }
            Assert.IsFalse(f.State.Bodies.Any(b => b.Id == ids[0]));
            Assert.IsFalse(f.State.Economy.Items.Any(i => "item:" + i.Id == ids[0]));
            Assert.IsTrue(f.State.Bodies.Any(b => b.Id == ids.Last()));
            Assert.GreaterOrEqual(f.State.Economy.Spent, 18);
            Assert.Greater(f.State.Economy.NextItemId, int.Parse(ids.Last().Substring(5)));
            f.Ack();
        }

        [Test]
        public void OptionalInitialBatchesDeferAndNonrenewableBatchStillAppearsExactlyOnce()
        {
            var f = Create(false, optionalSources: 12);
            Assert.LessOrEqual(f.State.Economy.Tiles.Count, f.Match.EntityBudget.OptionalBodies);
            var initiallyDeferred = f.State.Resources.Where(r => r.RecoveryAtTick >= 0).Select(r => r.SourceId).ToArray();
            Assert.IsNotEmpty(initiallyDeferred);
            int initialFrontier = f.State.Economy.NextTileId;
            f.Advance(181);
            Assert.AreEqual(initialFrontier, f.State.Economy.NextTileId);
            var last = f.Source.Course.Supplies.Single(s => s.SourceId == "Practice011");
            for (int attempt = 0; attempt < 16 && !f.State.Bodies.Any(b => b.SourceId == last.Id); attempt++)
            {
                var available = f.Source.Course.Supplies.First(s => f.State.Bodies.Count(b => b.SourceId == s.Id) == 3);
                foreach (var tile in f.State.Bodies.Where(b => b.SourceId == available.Id).ToArray())
                    Assert.AreEqual("Ok", f.Collect("a", tile.Id).Effect.Code);
                f.Advance(61);
            }
            Assert.IsTrue(f.State.Bodies.Any(b => b.SourceId == last.Id));
            if (f.State.Economy.Players[0].LetterCount + 3 > f.Source.Rules.MaxLetters)
            {
                Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.BeginCraft, value: "BED")).Receipts.Single().Effect.Code);
                f.Step();
            }
            foreach (var tile in f.State.Bodies.Where(b => b.SourceId == last.Id).ToArray())
                Assert.AreEqual("Ok", f.Collect("a", tile.Id).Effect.Code);
            f.Advance(661);
            Assert.IsFalse(f.State.Bodies.Any(b => b.SourceId == last.Id));
            Assert.AreEqual(-1, f.State.Resources.Single(r => r.SourceId == last.Id).RecoveryAtTick);
            f.Ack();
        }

        [Test]
        public void DeferredSourcesSurviveCommittedHostRecovery()
        {
            var f = Create(false, optionalSources: 12);
            var stamp = f.Match.PublishSnapshot(out string json);
            var imported = f.Source.Import(json);
            Assert.IsTrue(imported.Success, imported.Error);
            var expected = f.State.Resources.Select(r => r.SourceId + "/" + r.Generation + "/" + r.RecoveryAtTick).ToArray();
            f.Match.RestoreCommitted(imported.Snapshot, stamp, 1, f.State.Progress.WallClockMs + 17);
            CollectionAssert.AreEqual(expected, f.State.Resources.Select(r => r.SourceId + "/" + r.Generation + "/" + r.RecoveryAtTick).ToArray());
            int frontier = f.State.Economy.NextTileId;
            f.Advance(121);
            Assert.AreEqual(frontier, f.State.Economy.NextTileId);
            f.Ack();
        }

        [Test]
        public void RepeatedFreestyleCraftExpiryAndRefillStayBoundedWithNoTombstoneGrowth()
        {
            var f = Create(false, optionalSources: 12);
            f.Source.Catalogue.Get("BED").Deploy.LifetimeSeconds = .025f;
            var available = f.Source.Course.Supplies.First(s => f.State.Bodies.Count(b => b.SourceId == s.Id) == 3);
            Craft(f, sourceId: available.SourceId);
            int priorItem = 0;
            for (int cycle = 0; cycle < 64; cycle++)
            {
                if (cycle % 8 == 0) f.Ack();
                var held = f.State.Economy.Items.Single(i => i.State == ItemState.Held);
                Assert.Greater(held.Id, priorItem);
                priorItem = held.Id;
                Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.Deploy, slot: 0)).Receipts.Single().Effect.Code);
                f.Advance(3);
                Assert.IsFalse(f.State.Economy.Items.Any(i => i.Id == held.Id));
                foreach (var tile in f.State.Bodies.Where(b => b.Kind == WorldBodyKind.Tile && b.SourceId == "").ToArray())
                    Assert.AreEqual("Ok", f.Collect("a", tile.Id).Effect.Code);
                Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.BeginCraft, value: "BED")).Receipts.Single().Effect.Code);
                f.Step();
                Assert.LessOrEqual(f.State.Bodies.Count, 512);
                Assert.AreEqual(1, f.State.Economy.Items.Count(i => i.Origin == ItemOrigin.Crafted));
            }
            f.Ack();
        }

        [Test]
        public void FailedCommandBatchRollsBackPressureRetirementAndItsConservationChange()
        {
            var f = Create(true, optionalSources: 12);
            f.Source.Catalogue.Get("BED").Deploy.LifetimeSeconds = 0;
            string placed = Craft(f);
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.Deploy, slot: 0)).Receipts.Single().Effect.Code);
            Refill(f);
            Craft(f);
            string before = MatchSnapshotCodec.Export(f.State);
            f.World.ThrowQuery = true;
            var drop = f.Command("a", WordboundAction.Drop, slot: 0);
            var solve = f.Command("a", WordboundAction.UsePuzzle, "BedBridge", value: placed);
            Assert.Throws<InvalidOperationException>(() =>
                f.Match.Advance(f.State.Progress.WallClockMs + 17, new[] { drop, solve }));
            Assert.AreEqual(before, MatchSnapshotCodec.Export(f.State));
            f.World.ThrowQuery = false;
            var result = f.Match.Advance(f.State.Progress.WallClockMs + 17, new[] { drop, solve });
            Assert.IsTrue(result.Receipts.All(r => r.Effect.Code == "Ok"));
            f.Ack();
        }

        [Test]
        public void RehashedSnapshotCannotDuplicateCriticalRecoveryLetters()
        {
            var f = Create(true);
            var s = f.State;
            var source = f.Source.Course.Supplies.First(p => p.SourceId == "BedSupply" && p.TeamIndex == 0);
            var body = s.Bodies.First(b => b.SourceId == source.Id);
            char letter = s.Economy.Tiles.Single(t => "tile:" + t.Key == body.Id).Value;
            var e = s.Economy;
            var duplicate = new KeyValuePair<int, char>(e.NextTileId, letter);
            var changedEconomy = (EconomySnapshot)Activator.CreateInstance(typeof(EconomySnapshot),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] {
                    e.NextItemId, e.NextTileId + 1, e.Minted + 1, e.Spent, e.Players, e.Items, e.Tiles.Concat(new[] { duplicate }) }, null);
            var extra = new WorldBodyState("tile:" + duplicate.Key, WorldBodyKind.Tile, body.Position,
                ownerTeam: body.OwnerTeam, protectedResource: body.Protected, sourceId: body.SourceId,
                recoveryGeneration: body.RecoveryGeneration);
            var changed = new MatchSnapshot(s.Binding, s.Epoch, s.Sequence, s.NextWorldEntityId + 1, s.Progress,
                changedEconomy, s.Commands, s.Actors, s.Bodies.Concat(new[] { extra }), s.Resources,
                s.Mechanisms, s.RandomStreams, s.NarrativeFlags, s.ThrownFlights);
            var result = f.Source.Import(MatchSnapshotCodec.Export(changed));
            Assert.IsFalse(result.Success);
            StringAssert.Contains("authored batch", result.Error);
        }

        [TestCase(false), TestCase(true)]
        public void RehashedSnapshotCannotExceedGlobalOrTeamBodyBudget(bool teamOnly)
        {
            var f = Create(teamOnly, false);
            var s = f.State;
            int add = teamOnly ? f.Match.EntityBudget.PoolBodies + 1 : 513;
            var e = s.Economy;
            var tiles = Enumerable.Range(e.NextTileId, add).Select(i => new KeyValuePair<int, char>(i, 'A')).ToArray();
            var changedEconomy = (EconomySnapshot)Activator.CreateInstance(typeof(EconomySnapshot),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] {
                    e.NextItemId, e.NextTileId + add, e.Minted + add, e.Spent, e.Players, e.Items, e.Tiles.Concat(tiles) }, null);
            var extra = tiles.Select(t => new WorldBodyState("tile:" + t.Key, WorldBodyKind.Tile,
                default, ownerTeam: teamOnly ? 0 : -1));
            var changed = new MatchSnapshot(s.Binding, s.Epoch, s.Sequence, s.NextWorldEntityId + add, s.Progress,
                changedEconomy, s.Commands, s.Actors, s.Bodies.Concat(extra), s.Resources,
                s.Mechanisms, s.RandomStreams, s.NarrativeFlags, s.ThrownFlights);
            var result = f.Source.Import(MatchSnapshotCodec.Export(changed));
            Assert.IsFalse(result.Success);
            StringAssert.Contains(teamOnly ? "reserved team capacity" : "wire bounds", result.Error);
        }

        [Test]
        public void RehashedPinnedThrowsCannotOccupyReservedRecoveryCapacity()
        {
            var f = Create(true, false);
            string thrownId = Craft(f);
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.Throw, slot: 0)).Receipts.Single().Effect.Code);
            var s = f.State;
            var e = s.Economy;
            var sourceBody = s.Bodies.Single(b => b.Id == thrownId);
            var budget = f.Match.EntityBudget;
            var recoveryIds = f.Source.Course.Supplies.Where(p => p.TeamIndex == 0 &&
                f.Source.Course.Puzzles.Any(puzzle => puzzle.RecoverySupplyIds.Contains(p.SourceId)))
                .Select(p => p.Id).ToArray();
            var dynamicBodies = s.Bodies.Where(b => b.OwnerTeam == 0 && (b.Kind == WorldBodyKind.Tile ||
                b.Kind == WorldBodyKind.Item && e.Items.Single(i => "item:" + i.Id == b.Id).Origin == ItemOrigin.Crafted)).ToArray();
            int core = dynamicBodies.Count(b => !recoveryIds.Contains(b.SourceId));
            int add = budget.PoolBodies - budget.CriticalRecoveryBodies - budget.PhysicalAuxiliaryBodies - core + 1;
            var copies = Enumerable.Range(e.NextItemId, add).Select(id =>
            {
                var copy = e.Items.Single(i => "item:" + i.Id == thrownId);
                typeof(ItemInstance).GetProperty(nameof(ItemInstance.Id)).SetValue(copy, id);
                return copy;
            }).ToArray();
            var changedEconomy = (EconomySnapshot)Activator.CreateInstance(typeof(EconomySnapshot),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] {
                    e.NextItemId + add, e.NextTileId, e.Minted + add * 3, e.Spent,
                    e.Players, e.Items.Concat(copies), e.Tiles }, null);
            var extra = copies.Select(i => new WorldBodyState("item:" + i.Id, WorldBodyKind.Item,
                sourceBody.Position, velocity: sourceBody.Velocity, ownerTeam: sourceBody.OwnerTeam)).ToArray();
            Assert.LessOrEqual(add + dynamicBodies.Length, budget.PoolBodies,
                "The forged bodies fit the gross pool but consume its essential reserve.");
            var changed = new MatchSnapshot(s.Binding, s.Epoch, s.Sequence, s.NextWorldEntityId + add, s.Progress,
                changedEconomy, s.Commands, s.Actors, s.Bodies.Concat(extra), s.Resources,
                s.Mechanisms, s.RandomStreams, s.NarrativeFlags, s.ThrownFlights);
            var result = f.Source.Import(MatchSnapshotCodec.Export(changed));
            Assert.IsFalse(result.Success);
            StringAssert.Contains("reserved recovery", result.Error);
            Assert.IsTrue(f.Step().Advanced, "Rejecting a forged checkpoint must leave the live authority usable.");
            f.Ack();
        }
    }
}
