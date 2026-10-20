using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Tests;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundArenaResourceTests
    {
        static IEnumerable<string> Recipes() => TestData.Catalogue().Enabled
            .Where(item => ModeDefinition.For("FreeForAll").Recipes.Contains(item.Id))
            .Select(item => item.Id);

        sealed class World : IWordboundWorldQuery, IWordboundReleaseQuery
        {
            public bool HasLineOfSight(string actorId, WorldVector from, WorldVector to) => true;
            public bool CanDeploy(string actorId, string recipeId, WorldVector position) => true;
            public bool PuzzleSatisfied(string actorId, ResolvedCoursePuzzle puzzle, WorldBodyState placedItem) => false;
            public bool TryRelease(WorldBodyState actor, WorldBodyState heldItem, bool throwing, out WorldVector position)
            {
                position = new WorldVector(actor.Position.X, actor.Position.Y + .1f, actor.Position.Z + 1.5f);
                return true;
            }
        }

        internal sealed class Arena
        {
            public readonly ModeDefinition Mode = ModeDefinition.For("FreeForAll");
            public readonly ItemCatalogue Items = TestData.Catalogue();
            public readonly CourseCatalogue Geometry = CourseTemplates.ProofCatalogue();
            public readonly GameRules Rules;
            public readonly ResolvedCourse Course;
            public readonly MatchBinding Binding;
            public readonly AuthoritativeMatch Match;
            readonly HashSet<string> cacheSources;
            readonly long[] sequences;
            public MatchSnapshot State => Match.Capture(1);

            public Arena(int players = 2, string seed = "0123456789abcdef")
            {
                Rules = Mode.CreateGameplayRules(TestData.RulesFor("Dibs"));
                var result = CourseResolver.Resolve(CourseTemplates.CombatArena(), Geometry,
                    new CourseResolveRequest(seed, MatchStateValidation.Requirements(Mode)));
                Assert.IsTrue(result.Success, string.Join("\n", result.Issues));
                Course = result.Manifest;
                cacheSources = new HashSet<string>(Course.Supplies.Where(s => InCache(s.SourceId, 0)).Select(s => s.Id));
                var seats = Enumerable.Range(0, players).Select(i => new WordboundSeat("p" + i, i)).ToArray();
                Binding = new MatchBinding("arena-resources", MatchSnapshotCodec.ProtocolVersion, Mode.Hash,
                    Course.SourceHash, Course.Hash, WordboundIdentity.Sha256(string.Join("|", seats.Select(s => s.PlayerId))),
                    WordboundIdentity.Sha256(TestData.Read("items.json") + TestData.Read("rules.json")), seats);
                Match = new AuthoritativeMatch(Binding, Mode, Course, Geometry, Items, Rules, new World());
                sequences = new long[players];
            }

            public MatchTickResult Step(WordboundAction? action = null, string target = "", string value = "",
                int actor = 0, WorldVector? at = null, int slot = -1, int revision = 0,
                IEnumerable<HostDamage> damage = null, IEnumerable<ProgressEvent> observations = null)
            {
                if (Match.Tick % 60 == 0) Acknowledge();
                var commands = action.HasValue ? new[] { new AuthenticatedCommand("p" + actor,
                    new PlayerCommand(++sequences[actor], Match.Epoch, action.Value, target, revision, slot, value)) } : null;
                var poses = at.HasValue ? new[] { State.Bodies.Single(b => b.Id == "player:" + actor).At(at.Value) } : null;
                var result = Match.Advance(State.Progress.WallClockMs + 17, commands, poses, observations, damage);
                Assert.IsTrue(result.Advanced);
                Assert.LessOrEqual(State.Bodies.Count, WordboundEntityBudget.MaximumBodies);
                return result;
            }

            public void Advance(int ticks)
            {
                for (int i = 0; i < ticks; i++) Step();
            }

            public void Acknowledge()
            {
                var stamp = Match.PublishSnapshot(out string json);
                Assert.IsTrue(Match.Acknowledge(stamp));
                var restored = MatchSnapshotCodec.Import(json, Binding, Mode, Course, Items, Rules);
                Assert.IsTrue(restored.Success, restored.Error);
            }

            public WorldBodyState Letter(string source, char letter)
            {
                var state = State;
                var letters = state.Economy.Tiles.ToDictionary(t => t.Key, t => t.Value);
                return state.Bodies.First(b => b.Kind == WorldBodyKind.Tile && b.SourceId == source &&
                    letters[Number(b.Id)] == letter);
            }

            public WorldBodyState CacheLetter(char letter)
            {
                var state = State;
                var letters = state.Economy.Tiles.ToDictionary(t => t.Key, t => t.Value);
                return state.Bodies.First(b => b.Kind == WorldBodyKind.Tile && IsCache(b.SourceId) &&
                    letters[Number(b.Id)] == letter);
            }

            public bool IsCache(string resolvedSourceId) => cacheSources.Contains(resolvedSourceId);

            public void Collect(WorldBodyState tile, int actor = 0) =>
                Assert.AreEqual("Ok", Step(WordboundAction.CollectLetter, tile.Id, actor: actor, at: tile.Position)
                    .Receipts.Single().Effect.Code);

            public void Craft(string recipe, int actor = 0)
            {
                Assert.AreEqual("Ok", Step(WordboundAction.BeginCraft, value: recipe, actor: actor).Receipts.Single().Effect.Code);
                Advance((int)Math.Ceiling(Rules.CraftSeconds(Items.Get(recipe)) * ModeDefinition.TickRate) + 2);
                var state = State;
                var item = state.Economy.Items.Single(i => i.Origin == ItemOrigin.Crafted && i.HolderId == actor && i.Word == recipe);
                Assert.IsFalse(item.IsGone);
                Assert.IsTrue(state.Economy.Players.Single(p => p.PlayerId == actor).Letters.IsEmpty);
                Acknowledge();
            }
        }

        static int Number(string bodyId) => int.Parse(bodyId.Substring(bodyId.IndexOf(':') + 1), CultureInfo.InvariantCulture);
        static bool InCache(string sourceId, int cache) => sourceId.StartsWith("ArenaKit" + cache + "Tile", StringComparison.Ordinal);

        [Test]
        public void EveryCacheCoversTheRealEnabledRecipeMultisetsAndHasEqualSpawnAccess()
        {
            var source = CourseTemplates.CombatArena();
            var items = TestData.Catalogue();
            var recipes = Recipes().Select(items.Get).ToArray();
            Assert.AreEqual(13, recipes.Length);
            Assert.AreEqual(168, source.Supplies.Count);
            Assert.AreEqual(168, source.Supplies.Sum(s => s.Letters.Length));
            foreach (var supply in source.Supplies)
            {
                Assert.IsTrue(supply.Renewable);
                Assert.IsFalse(supply.Protected);
                Assert.IsEmpty(supply.AfterPuzzleIds);
                Assert.AreEqual(1, supply.Letters.Length, "Separate positions keep letters individually selectable.");
                Assert.IsTrue(supply.Position.X > -10000 && supply.Position.X < 10000 &&
                    supply.Position.Z > -10000 && supply.Position.Z < 10000);
            }
            for (int cache = 0; cache < 8; cache++)
            {
                var supplies = source.Supplies.Where(s => InCache(s.Id, cache)).ToArray();
                Assert.AreEqual(21, supplies.Length);
                var letters = LetterBag.FromWord(string.Concat(supplies.Select(s => s.Letters)));
                foreach (var recipe in recipes)
                    Assert.IsTrue(letters.Contains(recipe.Letters), "Cache " + cache + " cannot supply " + recipe.Id);
                foreach (var supply in supplies)
                    Assert.GreaterOrEqual(source.Supplies.Where(s => s.Id != supply.Id).Min(s => {
                        long x = s.Position.X - supply.Position.X, z = s.Position.Z - supply.Position.Z;
                        return x * x + z * z;
                    }), 250000L);
            }
            foreach (var spawn in source.Spawns)
            {
                long distance = source.Supplies.Min(s => {
                    long x = s.Position.X - spawn.Position.X, z = s.Position.Z - spawn.Position.Z;
                    return x * x + z * z;
                });
                Assert.AreEqual(2250000L, distance, spawn.Id + " must have the same nearest-cache distance");
            }
        }

        [TestCaseSource(nameof(Recipes))]
        public void EachEnabledRecipeCanBeCollectedAndCraftedThroughAuthority(string recipe)
        {
            var f = new Arena();
            foreach (char letter in f.Items.Get(recipe).Letters.ToString())
                f.Collect(f.CacheLetter(letter));
            f.Craft(recipe);
        }

        [TestCase(2, "0123456789abcdef")]
        [TestCase(8, "0000000000000000")]
        [TestCase(8, "fedcba9876543210")]
        public void NormalArenaStartupMintsEveryCacheWithinExistingBodyReservations(int players, string seed)
        {
            var f = new Arena(players, seed);
            var state = f.State;
            foreach (var source in f.Course.Supplies)
            {
                Assert.AreEqual(-1, source.TeamIndex);
                Assert.AreEqual(source.Letters.Length, state.Bodies.Count(b => b.Kind == WorldBodyKind.Tile && b.SourceId == source.Id));
            }
            int expected = players + f.Course.Placements.Count(p => p.Purpose != "Resources") +
                f.Course.Placements.Where(p => p.Purpose == "Resources").Sum(p => p.Letters.Length) +
                f.Course.Supplies.Sum(s => s.Letters.Length);
            Assert.AreEqual(expected, state.Bodies.Count);
            Assert.LessOrEqual(expected, 194);
            Assert.GreaterOrEqual(f.Match.EntityBudget.OptionalBodies, 183);
            Assert.AreEqual(512, WordboundEntityBudget.MaximumBodies);
            Assert.IsTrue(state.Resources.All(r => r.RecoveryAtTick == -1), "No initial cache should be throttled.");
            f.Acknowledge();
        }

        [Test]
        public void RenewableCacheReplacesOnlyCollectedLettersAndPreservesCraftedEquipment()
        {
            var f = new Arena(8);
            foreach (char letter in "BAT") f.Collect(f.CacheLetter(letter));
            var pending = f.State.Resources.Where(r => f.IsCache(r.SourceId) && r.RecoveryAtTick >= 0).ToArray();
            Assert.AreEqual(3, pending.Length);
            long firstDeadline = pending.Min(r => r.RecoveryAtTick), lastDeadline = pending.Max(r => r.RecoveryAtTick);
            Assert.Greater(firstDeadline, f.Match.Tick);
            f.Craft("BAT");
            f.Advance((int)(firstDeadline - f.Match.Tick - 1));
            Assert.AreEqual(18, f.State.Bodies.Count(b => f.IsCache(b.SourceId) && b.Kind == WorldBodyKind.Tile));
            f.Step();
            Assert.AreEqual(19, f.State.Bodies.Count(b => f.IsCache(b.SourceId) && b.Kind == WorldBodyKind.Tile));
            f.Advance((int)(lastDeadline - f.Match.Tick));
            var state = f.State;
            var tiles = state.Bodies.Where(b => f.IsCache(b.SourceId) && b.Kind == WorldBodyKind.Tile).ToArray();
            var letters = state.Economy.Tiles.ToDictionary(t => t.Key, t => t.Value);
            Assert.AreEqual(21, tiles.Length);
            Assert.AreEqual(LetterBag.FromWord(string.Concat(f.Course.Supplies.Where(s => f.IsCache(s.Id)).Select(s => s.Letters))).ToString(),
                LetterBag.FromWord(new string(tiles.Select(b => letters[Number(b.Id)]).ToArray())).ToString());
            Assert.AreEqual(1, state.Economy.Items.Count(i => i.Origin == ItemOrigin.Crafted && i.Word == "BAT"));
            Assert.IsTrue(state.Economy.Players.Single(p => p.PlayerId == 0).Letters.IsEmpty);
            f.Advance(601);
            Assert.AreEqual(21, f.State.Bodies.Count(b => f.IsCache(b.SourceId) && b.Kind == WorldBodyKind.Tile));
            Assert.AreEqual(f.State.Bodies.Count, f.State.Bodies.Select(b => b.Id).Distinct().Count());
            f.Acknowledge();
        }

        [Test]
        public void UnarmedSmashLettersCombineWithTheCacheToCraftAWeapon()
        {
            var f = new Arena();
            var prop = f.State.Bodies.First(b => b.Kind == WorldBodyKind.Item);
            var original = f.State.Economy.Items.Single(i => "item:" + i.Id == prop.Id);
            int attempts = (int)Math.Ceiling(original.Durability / f.Rules.Unarmed.BreakPower) + 1;
            for (int attempt = 0; attempt < attempts && f.State.Bodies.Any(b => b.Id == prop.Id && b.Active); attempt++)
            {
                var at = new WorldVector(prop.Position.X, prop.Position.Y, prop.Position.Z - .5f);
                Assert.AreEqual("Ok", f.Step(WordboundAction.Attack, at: at).Receipts.Single().Effect.Code);
                f.Advance(WordboundCombatPolicy.CycleTicks(f.Rules.Unarmed) + 1);
            }
            Assert.IsFalse(f.State.Bodies.Any(b => b.Id == prop.Id && b.Active));
            f.Collect(f.Letter(prop.SourceId, 'T'));
            f.Collect(f.CacheLetter('B'));
            f.Collect(f.CacheLetter('A'));
            f.Craft("BAT");
        }
    }
}
