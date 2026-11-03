using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Tests;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundHostTickTests
    {
        const int Players = 8, Ticks = 900, WarmUp = 60;
        const string ArenaGolden = "d4ac0e5efc20eb654e3223da2ff5f215f6f65b3015f746222dfda721f287ba6b";
        const string FlagshipGolden = "6b558d8f47e90367b24124ff4de000e1611ab5f8780b9c469a48a2cc4d7caf09";

        sealed class Host
        {
            public ModeDefinition Mode;
            public ResolvedCourse Course;
            public ItemCatalogue Items;
            public GameRules Rules;
            public MatchBinding Binding;
            public AuthoritativeMatch Match;
        }

        [Test]
        public void ScriptedArenaFreeForAllKeepsItsCanonicalTickHashes() => Check("arena", ArenaGolden, Arena());

        [Test]
        public void ScriptedFlagshipSquadRaceKeepsItsCanonicalTickHashes() => Check("flagship", FlagshipGolden, Flagship());

        [Test]
        public void OnlyTheAuthoritysLatestCaptureIsItsOwn()
        {
            var host = Arena();
            var frame = host.Match.Capture(1);
            Assert.IsTrue(host.Match.IsOwnCapture(frame));
            Assert.IsFalse(host.Match.IsOwnCapture(null));
            var copy = MatchSnapshotCodec.Import(MatchSnapshotCodec.Export(frame), host.Binding, host.Mode, host.Course,
                host.Items, host.Rules);
            Assert.IsTrue(copy.Success, copy.Error);
            Assert.IsFalse(host.Match.IsOwnCapture(copy.Snapshot), "An imported copy is validated like any received snapshot.");
            Assert.IsTrue(host.Match.Advance(frame.Progress.WallClockMs + 17).Advanced);
            Assert.IsFalse(host.Match.IsOwnCapture(frame), "A frame from before the latest tick is no longer the authority's own.");
            var next = host.Match.Capture(1);
            Assert.IsTrue(host.Match.IsOwnCapture(next));
            Assert.AreEqual("", MatchStateValidation.Validate(next, host.Binding, host.Mode, host.Course, host.Items, host.Rules));
        }

        static Host Arena()
        {
            var mode = ModeDefinition.For("FreeForAll");
            var geometry = CourseTemplates.ProofCatalogue();
            var resolved = CourseResolver.Resolve(CourseTemplates.CombatArena(), geometry,
                new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(mode)));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            var seats = Enumerable.Range(0, Players).Select(i => new WordboundSeat("p" + i, i)).ToArray();
            return Create(mode, resolved.Manifest, geometry, seats);
        }

        static Host Flagship()
        {
            var items = TestData.Catalogue();
            var mode = ModeDefinition.For("TeamRace", RaceFormat.Squad);
            var content = WordboundFlagshipContent.Create(items);
            var resolved = content.Resolve("0123456789abcdef", MatchStateValidation.Requirements(mode));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            var seats = Enumerable.Range(0, Players).Select(i => new WordboundSeat("p" + i, i / 4, i % 4)).ToArray();
            return Create(mode, resolved.Manifest, content.Geometry, seats);
        }

        static Host Create(ModeDefinition mode, ResolvedCourse course, CourseCatalogue geometry, WordboundSeat[] seats)
        {
            var host = new Host { Mode = mode, Course = course, Items = TestData.Catalogue() };
            host.Rules = mode.CreateGameplayRules(TestData.RulesFor("Dibs"));
            host.Binding = new MatchBinding("host-tick", MatchSnapshotCodec.ProtocolVersion, mode.Hash,
                course.SourceHash, course.Hash, new string('a', 64), new string('b', 64), seats);
            host.Match = new AuthoritativeMatch(host.Binding, mode, course, geometry, host.Items, host.Rules,
                new WordboundAuthorityTests.World());
            return host;
        }

        static void Check(string name, string golden, Host host)
        {
            var match = host.Match;
            var sequences = new long[Players];
            var start = match.Capture(1).Bodies.Where(b => b.Kind == WorldBodyKind.Player)
                .ToDictionary(b => b.Id, b => b.Position, StringComparer.Ordinal);
            string chain = "";
            var advance = new Stopwatch(); var capture = new Stopwatch(); var validate = new Stopwatch();
            int accepted = 0;
            for (int t = 0; t < Ticks; t++)
            {
                if (t == WarmUp) { advance.Reset(); capture.Reset(); validate.Reset(); }
                if (match.Tick % 60 == 0)
                {
                    var stamp = match.PublishSnapshot(out _);
                    Assert.IsTrue(match.Acknowledge(stamp));
                }
                var state = match.Capture(1);
                int actor = t % Players;
                AuthenticatedCommand command = null;
                WorldVector? reach = null;
                if (t % 3 == 0)
                {
                    var body = state.Bodies.Single(b => b.Id == "player:" + actor);
                    var tile = state.Bodies.Where(b => b.Kind == WorldBodyKind.Tile && b.Active)
                        .OrderBy(b => Distance(b.Position, body.Position)).ThenBy(b => b.Id, StringComparer.Ordinal).FirstOrDefault();
                    bool collect = t % 6 == 0 && tile != null;
                    if (collect) reach = tile.Position;
                    command = new AuthenticatedCommand("p" + actor, new PlayerCommand(++sequences[actor], match.Epoch,
                        collect ? WordboundAction.CollectLetter : WordboundAction.Attack, collect ? tile.Id : "", 0, -1, ""));
                }
                var poses = state.Bodies.Select(b =>
                {
                    if (b.Kind != WorldBodyKind.Player || !b.Active) return b;
                    int index = int.Parse(b.Id.Substring(7), CultureInfo.InvariantCulture);
                    if (index == actor && reach.HasValue) return b.At(reach.Value);
                    var origin = start[b.Id];
                    float offset = ((t + 7 * index) % 40 - 20) * .02f;
                    return b.At(new WorldVector(origin.X + offset, origin.Y, origin.Z - offset));
                }).ToArray();
                MatchTickResult result;
                advance.Start();
                result = match.Advance(state.Progress.WallClockMs + 17, command == null ? null : new[] { command }, poses);
                advance.Stop();
                Assert.IsTrue(result.Advanced);
                capture.Start();
                var frame = match.Capture(1);
                capture.Stop();
                validate.Start();
                Assert.IsTrue(MatchStateValidation.ReconcileAndApply(frame, host.Binding, host.Mode, host.Course, host.Items,
                    host.Rules, host.Rules, () => true, () => { }));
                validate.Stop();
                string hash = "";
                if (t % 15 == 14 || t == Ticks - 1) MatchSnapshotCodec.Export(frame, out hash);
                accepted += result.Receipts.Count(r => r.Effect.Code == "Ok");
                chain = WordboundIdentity.Sha256(chain + "|" + hash + "|" +
                    string.Join(",", result.Receipts.Select(r => r.Effect.Code + ":" + r.Effect.EntityId)));
            }
            int timed = Ticks - WarmUp;
            TestContext.Progress.WriteLine(FormattableString.Invariant(
                $"HOST_TICK_COST {name} bodies={match.Capture(1).Bodies.Count} ok={accepted} recovered={match.Capture(1).Resources.Count(r => r.Generation >= 3)} advance={advance.Elapsed.TotalMilliseconds / timed:F3}ms capture={capture.Elapsed.TotalMilliseconds / timed:F3}ms validate={validate.Elapsed.TotalMilliseconds / timed:F3}ms chain={chain}"));
            Assert.AreEqual(golden, chain);
        }

        static float Distance(WorldVector a, WorldVector b)
        {
            float x = a.X - b.X, y = a.Y - b.Y, z = a.Z - b.Z;
            return x * x + y * y + z * z;
        }
    }
}
