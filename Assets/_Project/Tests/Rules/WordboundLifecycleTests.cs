using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundLifecycleTests
    {
        static WordboundAuthorityTests.Fixture Create(Action<CourseDocument> edit = null, string mode = "TimeRun")
        {
            var f = WordboundAuthorityTests.Create(mode);
            if (edit == null) return f;
            var draft = f.Source.Course.CopyDocument();
            edit(draft);
            var resolved = CourseResolver.Resolve(draft, CourseTemplates.ProofCatalogue(),
                new CourseResolveRequest(f.Source.Course.Seed, f.Source.Course.Play));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            f.Source.Course = resolved.Manifest;
            var b = f.Source.Binding;
            f.Source.Binding = new MatchBinding(b.MatchId, b.ProtocolVersion, b.ModeHash,
                f.Source.Course.SourceHash, f.Source.Course.Hash, b.RosterHash, b.CatalogueHash, b.Seats);
            f.Match = new AuthoritativeMatch(f.Source.Binding, f.Source.Mode, f.Source.Course,
                CourseTemplates.ProofCatalogue(), f.Source.Catalogue, f.Source.Rules, f.World);
            return f;
        }

        static string Source(WordboundAuthorityTests.Fixture f, string source = "BedSupply", int team = 0) =>
            f.Source.Course.Supplies.Single(s => s.SourceId == source && s.TeamIndex == team).Id;

        static AuthenticatedCommand SolveBed(WordboundAuthorityTests.Fixture f, string id)
        {
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.Deploy, slot: 0)).Receipts.Single().Effect.Code);
            var command = f.Command("a", WordboundAction.UsePuzzle, "BedBridge", value: id);
            Assert.AreEqual("Ok", f.Step(command).Receipts.Single().Effect.Code);
            return command;
        }

        [TestCase("BED"), TestCase("BBEEDD")]
        public void CompletedPuzzleRetiresItsRefillButPreservesRivalSupplyAndSolvedEvidence(string letters)
        {
            var f = Create(d => d.Supplies.Single(s => s.Id == "BedSupply").Letters = letters, mode: "TeamRace");
            string id = f.CraftBed();
            f.Advance(601);
            string mine = Source(f), rival = Source(f, team: 1);
            var before = f.State;
            string beforeJson = MatchSnapshotCodec.Export(before);
            var retired = before.Bodies.Where(b => b.Kind == WorldBodyKind.Tile && b.SourceId == mine).ToArray();
            var tiles = before.Economy.Tiles.ToDictionary(t => "tile:" + t.Key, t => t.Value);
            string[] rivalIds = before.Bodies.Where(b => b.SourceId == rival).Select(b => b.Id).ToArray();
            string[] otherTiles = before.Bodies.Where(b => b.Kind == WorldBodyKind.Tile && b.SourceId != mine).Select(b => b.Id).ToArray();
            Assert.AreEqual(letters.Length, retired.Length);
            CollectionAssert.AreEquivalent(letters.ToCharArray(), retired.Select(b => tiles[b.Id]).ToArray());
            Assert.IsTrue(retired.All(b => b.RecoveryGeneration == 3), "This must retire the newly recovered batch, not its collected predecessor.");
            var replay = SolveBed(f, id);
            Assert.IsFalse(f.State.Bodies.Any(b => b.SourceId == mine || b.Id == id));
            CollectionAssert.AreEqual(rivalIds, f.State.Bodies.Where(b => b.SourceId == rival).Select(b => b.Id).ToArray());
            CollectionAssert.AreEqual(otherTiles, f.State.Bodies.Where(b => b.Kind == WorldBodyKind.Tile).Select(b => b.Id).ToArray());
            Assert.AreEqual(before.Economy.Spent + 3 + retired.Length, f.State.Economy.Spent);
            Assert.AreEqual(before.Economy.Tiles.Count - retired.Length, f.State.Economy.Tiles.Count);
            Assert.AreEqual(letters.Length - 3, f.State.Economy.Players.Single(p => p.PlayerId == 0).Letters.Count);
            Assert.IsTrue(f.State.Economy.Items.Single(i => "item:" + i.Id == id).IsGone);
            Assert.AreEqual(id, f.State.Mechanisms.Single(m => m.Team == 0 && m.Lap == 0 && m.PuzzleId == "BedBridge").ItemEntityId);
            Assert.AreEqual(-1, f.State.Resources.Single(r => r.SourceId == mine && r.Lap == 0).RecoveryAtTick);
            Assert.AreEqual(beforeJson, MatchSnapshotCodec.Export(before));
            int spent = f.State.Economy.Spent, frontier = f.State.Economy.NextTileId;
            long firstTick = f.Match.Tick;
            Assert.AreEqual(firstTick, f.Step(replay).Receipts.Single().Tick);
            f.Advance(601);
            Assert.IsFalse(f.State.Bodies.Any(b => b.SourceId == mine));
            Assert.AreEqual(spent, f.State.Economy.Spent, "Repeated retirement must not spend the same bucket twice.");
            Assert.AreEqual(frontier, f.State.Economy.NextTileId);
            f.Ack();
        }

        [Test]
        public void SupplyWithAnotherUnsolvedConsumerRetainsItsLooseLetters()
        {
            var f = Create(d => d.Puzzles.Single(p => p.Id == "FanDoor").RecoverySupplyIds.Add("BedSupply"));
            string id = f.CraftBed();
            f.Advance(601);
            SolveBed(f, id);
            Assert.AreEqual(3, f.State.Bodies.Count(b => b.SourceId == Source(f)));
            Assert.AreEqual(3, f.State.Economy.Spent);
            f.Ack();
        }

        [Test]
        public void GeneralSuppliesAndNonconsumedDeployedItemsArePreserved()
        {
            var f = Create(d => {
                d.Puzzles.Single(p => p.Id == "BedBridge").ConsumeLetters = false;
                d.Supplies.Add(new CourseSupply { Id = "Practice", ModuleId = "Start",
                    Position = new CoursePoint(0, 0, -7000), Letters = "BED" });
            });
            string id = f.CraftBed();
            string general = Source(f, "Practice");
            string[] expected = f.State.Bodies.Where(b => b.SourceId == general).Select(b => b.Id).ToArray();
            SolveBed(f, id);
            Assert.AreEqual(ItemState.Deployed, f.State.Economy.Items.Single(i => "item:" + i.Id == id).State);
            Assert.IsTrue(f.State.Bodies.Single(b => b.Id == id).Active);
            CollectionAssert.AreEqual(expected, f.State.Bodies.Where(b => b.SourceId == general).Select(b => b.Id).ToArray());
            f.Ack();
        }

        [Test]
        public void ExpiredUnreferencedCraftIsCompactedAndReplayDoesNotRecreateItsIdentity()
        {
            var f = Create();
            f.Source.Catalogue.Get("BED").Deploy.LifetimeSeconds = .125f;
            string id = f.CraftBed();
            var command = f.Command("a", WordboundAction.Deploy, slot: 0);
            f.Step(command);
            long deployed = f.Match.Tick;
            f.Advance(8);
            Assert.IsFalse(f.State.Bodies.Any(b => b.Id == id));
            Assert.IsFalse(f.State.Economy.Items.Any(i => "item:" + i.Id == id));
            int tiles = f.State.Economy.Tiles.Count;
            Assert.AreEqual(deployed, f.Step(command).Receipts.Single().Tick);
            Assert.AreEqual(tiles, f.State.Economy.Tiles.Count);
            foreach (var tile in f.State.Bodies.Where(b => b.Kind == WorldBodyKind.Tile && b.SourceId == "").ToArray())
                Assert.AreEqual("Ok", f.Collect("a", tile.Id).Effect.Code);
            f.Step(f.Command("a", WordboundAction.BeginCraft, value: "BED"));
            f.Advance(61);
            Assert.Greater(f.State.Economy.Items.Single(i => i.Origin == ItemOrigin.Crafted).Id,
                int.Parse(id.Substring(5)));
            f.Ack();
        }

        [Test]
        public void DestroyedHitTargetStaysUntilTheActiveAttackLedgerEnds()
        {
            var f = Create();
            f.Source.Catalogue.Get("BED").Durability = 1;
            string id = f.CraftBed();
            f.Step(f.Command("a", WordboundAction.Drop, slot: 0));
            var target = f.State.Bodies.Single(b => b.Id == id);
            f.Step(f.Command("a", WordboundAction.Attack),
                new[] { f.Pose("a", new WorldVector(target.Position.X, target.Position.Y, target.Position.Z - 1)) });
            f.Advance(WordboundCombatPolicy.WindupTicks(f.Source.Rules.Unarmed));
            Assert.IsTrue(f.State.Actors.Single(a => a.PlayerId == "a").Attack.HitBodyIds.Contains(id));
            Assert.IsFalse(f.State.Bodies.Single(b => b.Id == id).Active);
            Assert.IsTrue(f.State.Economy.Items.Single(i => "item:" + i.Id == id).IsGone);
            f.Ack();
            f.Advance(WordboundCombatPolicy.ActiveTicks(f.Source.Rules.Unarmed) + 1);
            Assert.IsFalse(f.State.Bodies.Any(b => b.Id == id));
            Assert.IsFalse(f.State.Economy.Items.Any(i => "item:" + i.Id == id));
            f.Ack();
        }

        [Test]
        public void CompletedLapRetiresGeneralScopeAndFreshLapStillMintsTheSameSources()
        {
            var f = Create();
            var keepsake = f.State.Bodies.Single(b => b.Kind == WorldBodyKind.Keepsake);
            f.Step(f.Command("a", WordboundAction.TakeKeepsake), new[] { f.Pose("a", keepsake.Position) });
            int previousTileFrontier = f.State.Economy.NextTileId;
            for (int lap = 0; lap < 3; lap++)
            {
                foreach (var checkpoint in f.Source.Course.Laps[lap].Checkpoints)
                {
                    foreach (string puzzleId in checkpoint.PuzzleIds)
                    {
                        var puzzle = f.Source.Course.Puzzles.Single(p => p.Id == puzzleId);
                        string source = Source(f, puzzle.RecoverySupplyIds[0]);
                        foreach (var tile in f.State.Bodies.Where(b => b.SourceId == source).ToArray())
                            Assert.AreEqual("Ok", f.Collect("a", tile.Id).Effect.Code);
                        f.Step(f.Command("a", WordboundAction.BeginCraft, value: puzzle.RecipeId));
                        f.Advance(puzzleId == "BedBridge" ? 601 : 61);
                        var item = f.State.Economy.Items.Last(i => i.Origin == ItemOrigin.Crafted && !i.IsGone && i.Word == puzzle.RecipeId);
                        f.Step(f.Command("a", WordboundAction.Deploy, slot: 0));
                        Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.UsePuzzle, puzzleId,
                            value: "item:" + item.Id)).Receipts.Single().Effect.Code);
                    }
                    var module = f.Source.Course.Geometry.Single(m => m.Id == checkpoint.ModuleId);
                    f.Step(poses: new[] { f.Pose("a", new WorldVector(module.Position.X / 1000f,
                        module.Position.Y / 1000f, module.Position.Z / 1000f)) },
                        observations: new[] { new ProgressEvent(ProgressEventKind.CheckpointReached, "a", checkpoint.Id, lap) });
                }
                f.Step(observations: new[] {
                    new ProgressEvent(ProgressEventKind.FinishReached, "a", f.Source.Course.Laps[lap].FinishModuleId, lap) });
                Assert.AreEqual(lap + 1, f.State.Progress.Teams[0].Lap);
                Assert.IsFalse(f.State.Bodies.Any(b => b.Kind == WorldBodyKind.Tile && b.Protected &&
                    b.RecoveryGeneration % 3 <= lap));
                Assert.IsTrue(f.State.Resources.Where(r => r.Lap <= lap).All(r => r.RecoveryAtTick == -1));
                if (lap < 2)
                {
                    Assert.IsTrue(f.State.Bodies.Any(b => b.Kind == WorldBodyKind.Tile &&
                        b.SourceId == Source(f) && b.RecoveryGeneration == lap + 1));
                    Assert.Greater(f.State.Economy.NextTileId, previousTileFrontier);
                    previousTileFrontier = f.State.Economy.NextTileId;
                }
                f.Ack();
            }
        }

        [Test]
        public void RetiredPuzzleLettersStayRetiredAcrossCommittedHostRestoration()
        {
            var f = Create();
            string id = f.CraftBed(), source = Source(f);
            f.Advance(601);
            Assert.AreEqual(3, f.State.Bodies.Count(b => b.SourceId == source));
            SolveBed(f, id);
            var stamp = f.Match.PublishSnapshot(out string json);
            var saved = f.Source.Import(json);
            Assert.IsTrue(saved.Success, saved.Error);
            int spent = saved.Snapshot.Economy.Spent;
            int frontier = saved.Snapshot.Economy.NextTileId;
            Assert.AreEqual(6, spent);
            f.Match = new AuthoritativeMatch(f.Source.Binding, f.Source.Mode, f.Source.Course,
                CourseTemplates.ProofCatalogue(), f.Source.Catalogue, f.Source.Rules, f.World);
            f.Match.RestoreCommitted(saved.Snapshot, stamp, saved.Snapshot.Epoch + 1,
                saved.Snapshot.Progress.WallClockMs + 17);
            f.Advance(601);
            Assert.AreEqual(spent, f.State.Economy.Spent);
            Assert.AreEqual(frontier, f.State.Economy.NextTileId);
            Assert.IsFalse(f.State.Bodies.Any(b => b.SourceId == source));
            Assert.AreEqual(-1, f.State.Resources.Single(r => r.SourceId == source && r.Lap == 0).RecoveryAtTick);
            f.Ack();
        }

        [TestCase(false), TestCase(true)]
        public void RehashedCheckpointCannotRestoreRetiredPuzzleLettersOrRecoveryDeadline(bool deadline)
        {
            var f = Create();
            string source = Source(f);
            var oldBody = f.State.Bodies.First(b => b.SourceId == source);
            var oldTile = f.State.Economy.Tiles.Single(t => oldBody.Id == "tile:" + t.Key);
            string id = f.CraftBed();
            SolveBed(f, id);
            var s = f.State;
            var e = s.Economy;
            var changedEconomy = deadline ? e : (EconomySnapshot)Activator.CreateInstance(typeof(EconomySnapshot),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] {
                    e.NextItemId, e.NextTileId, e.Minted + 1, e.Spent, e.Players, e.Items,
                    e.Tiles.Concat(new[] { oldTile }) }, null);
            var changed = new MatchSnapshot(s.Binding, s.Epoch, s.Sequence, s.NextWorldEntityId, s.Progress,
                changedEconomy, s.Commands, s.Actors, deadline ? s.Bodies : s.Bodies.Concat(new[] { oldBody }),
                s.Resources.Select(r => deadline && r.SourceId == source && r.Lap == 0 ?
                    new MatchResourceState(r.SourceId, r.Team, r.Lap, r.Generation, s.Progress.Tick + 100) : r),
                s.Mechanisms, s.RandomStreams, s.NarrativeFlags, s.ThrownFlights);
            Assert.IsFalse(f.Source.Import(MatchSnapshotCodec.Export(changed)).Success);
        }
    }
}
