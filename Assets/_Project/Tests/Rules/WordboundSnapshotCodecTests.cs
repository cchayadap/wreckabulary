using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Tests;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundSnapshotCodecTests
    {
        internal sealed class Fixture
        {
            internal ModeDefinition Mode;
            internal ResolvedCourse Course;
            internal ItemCatalogue Catalogue;
            internal GameRules Rules;
            internal MatchBinding Binding;
            internal MatchProgress Progress;
            internal Economy Economy;
            internal CommandJournal Journal;
            internal List<WorldBodyState> Bodies = new List<WorldBodyState>();
            internal List<MatchResourceState> Resources = new List<MatchResourceState>();
            internal List<MatchMechanismState> Mechanisms = new List<MatchMechanismState>();
            internal MatchSnapshot Snapshot() => new MatchSnapshot(Binding, 0, 9007199254740993L, 9007199254740995L,
                Progress.Capture(), Economy.Capture(), Journal.Capture(),
                Binding.Seats.Select(s => new MatchActorState(s.PlayerId, 100, 100, lastInputSequence: 9007199254740997L, appearance: "coat:navy 🧸")),
                Bodies, Resources, Mechanisms, new[] { new MatchRandomState("physics", ulong.MaxValue, 18446744073709551613UL) }, Array.Empty<string>());
            internal MatchSnapshotImport Import(string json) => MatchSnapshotCodec.Import(json, Binding, Mode, Course, Catalogue, Rules);
        }

        internal static Fixture Create(string mode = "Freestyle")
        {
            var f = new Fixture { Mode = ModeDefinition.For(mode), Catalogue = TestData.Catalogue() };
            f.Rules = f.Mode.CreateGameplayRules(TestData.RulesFor("Dibs"));
            var document = mode == "Freestyle" ? CourseTemplates.FreestyleWorld() :
                mode == "FreeForAll" ? CourseTemplates.CombatArena() : CourseTemplates.RaceCircuit();
            var result = CourseResolver.Resolve(document, CourseTemplates.ProofCatalogue(),
                new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(f.Mode)));
            Assert.IsTrue(result.Success, string.Join("\n", result.Issues));
            f.Course = result.Manifest;
            var seats = new[] { new WordboundSeat("a", 0), new WordboundSeat("b", f.Mode.TeamCount > 1 ? 1 : 0, f.Mode.TeamCount > 1 ? 0 : 1) };
            f.Binding = new MatchBinding("match", "wordbound-v1", f.Mode.Hash, f.Course.SourceHash, f.Course.Hash,
                new string('a', 64), new string('b', 64), seats);
            f.Progress = new MatchProgress(f.Mode, seats, MatchStateValidation.Route(f.Course));
            f.Economy = new Economy(f.Catalogue, f.Rules);
            f.Journal = new CommandJournal(f.Binding.Hash, seats.Select(s => s.PlayerId));
            for (int i = 0; i < seats.Length; i++)
            {
                f.Economy.AddPlayer(i, seats[i].Team);
                f.Bodies.Add(new WorldBodyState("player:" + i, WorldBodyKind.Player,
                    new WorldVector(BitConverter.Int32BitsToSingle(unchecked((int)0x80000000)), 1.234567f, float.Epsilon),
                    velocity: new WorldVector(0.123f, 0, 0), ownerTeam: seats[i].Team, sourceId: seats[i].PlayerId));
            }
            foreach (var supply in f.Course.Supplies)
                for (int lap = 0; lap < (f.Mode.IsRace ? 3 : 1); lap++)
                    f.Resources.Add(new MatchResourceState(supply.Id, supply.TeamIndex, lap, lap));
            foreach (var placement in f.Course.Placements.Where(p => p.Purpose == "Resources"))
                for (int lap = 0; lap < (f.Mode.IsRace ? 3 : 1); lap++)
                    f.Resources.Add(new MatchResourceState(placement.Id, placement.TeamIndex, lap, lap));
            if (f.Mode.IsRace)
                foreach (int team in seats.Select(s => s.Team).Distinct())
                {
                    f.Bodies.Add(new WorldBodyState("keepsake:" + team, WorldBodyKind.Keepsake, default,
                        ownerTeam: team, protectedResource: true));
                    for (int lap = 0; lap < 3; lap++)
                        foreach (var puzzle in f.Course.Puzzles) f.Mechanisms.Add(new MatchMechanismState(puzzle.Id, team, lap));
                }
            return f;
        }

        [TestCase("TimeRun"), TestCase("TeamRace"), TestCase("BrawlRace"), TestCase("FreeForAll"), TestCase("Freestyle")]
        public void FullSnapshotRoundTripPreservesBindingCountersAndFloatBits(string mode)
        {
            var f = Create(mode);
            var snapshot = f.Snapshot();
            string json = MatchSnapshotCodec.Export(snapshot);
            var imported = f.Import(json);
            Assert.IsTrue(imported.Success, imported.Error);
            Assert.AreEqual(json, MatchSnapshotCodec.Export(imported.Snapshot));
            Assert.AreEqual(MatchSnapshotCodec.ComputeHash(snapshot), imported.Hash);
            Assert.AreEqual(snapshot.Sequence, imported.Snapshot.Sequence);
            Assert.AreEqual(snapshot.NextWorldEntityId, imported.Snapshot.NextWorldEntityId);
            Assert.AreEqual(ulong.MaxValue, imported.Snapshot.RandomStreams[0].State);
            Assert.AreEqual(BitConverter.SingleToInt32Bits(snapshot.Bodies[0].Position.X),
                BitConverter.SingleToInt32Bits(imported.Snapshot.Bodies[0].Position.X));
            Assert.AreEqual(BitConverter.SingleToInt32Bits(snapshot.Bodies[0].Position.Z),
                BitConverter.SingleToInt32Bits(imported.Snapshot.Bodies[0].Position.Z));
        }

        [Test]
        public void MidCraftAndRetainedReceiptSurviveRecoveryWithoutDuplicateEffects()
        {
            var f = Create();
            f.Progress.Advance(1, 9007199254740993L);
            TestData.GiveLetters(f.Economy, 0, "BAT");
            Assert.IsTrue(f.Economy.BeginCraft(0, "BAT", 0.0000123456789).Ok);
            var target = new CountingTarget();
            var request = new AuthenticatedCommand("a", new PlayerCommand(1, 0, WordboundAction.BeginCraft, value: "BAT"));
            f.Journal.ExecuteBatch(1, new[] { request }, target);
            var imported = f.Import(MatchSnapshotCodec.Export(f.Snapshot()));
            Assert.IsTrue(imported.Success, imported.Error);
            var snapshot = imported.Snapshot;
            Assert.AreEqual(f.Progress.WallClockMs, snapshot.Progress.WallClockMs);
            Assert.AreEqual(BitConverter.DoubleToInt64Bits(f.Economy.Player(0).Craft.StartedAt),
                BitConverter.DoubleToInt64Bits(snapshot.Economy.Players[0].Craft.StartedAt));
            var recovered = new Economy(f.Catalogue, f.Rules);
            recovered.AddPlayer(0, 0); recovered.AddPlayer(1, 0); recovered.Restore(snapshot.Economy);
            Assert.IsTrue(recovered.CompleteCraft(0, 10).Ok);
            Assert.IsFalse(recovered.CompleteCraft(0, 10).Ok);
            Assert.AreEqual(1, recovered.Items.Count());
            var journal = new CommandJournal(f.Binding.Hash, new[] { "a", "b" });
            journal.Restore(snapshot.Commands);
            var replay = journal.ExecuteBatch(2, new[] { request }, target);
            Assert.AreEqual("Applied", replay[0].Effect.Code);
            Assert.AreEqual(1, target.Count);
        }

        [Test]
        public void CultureAndInputCollectionOrderDoNotChangeCanonicalState()
        {
            var f = Create();
            var snapshot = f.Snapshot();
            string expected = MatchSnapshotCodec.Export(snapshot);
            f.Bodies.Reverse(); f.Resources.Reverse(); f.Mechanisms.Reverse();
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Assert.AreEqual(expected, MatchSnapshotCodec.Export(f.Snapshot()));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [TestCase("numeric-counter"), TestCase("leading-zero"), TestCase("counter-overflow"), TestCase("negative-counter")]
        [TestCase("nan"), TestCase("infinity"), TestCase("uppercase-bits"), TestCase("short-bits")]
        [TestCase("enum-number"), TestCase("duplicate-key"), TestCase("unknown-key"), TestCase("future-schema"), TestCase("bad-hash")]
        public void WireBoundaryRejectsMalformedOrAmbiguousEncoding(string mutation)
        {
            var f = Create();
            string json = MatchSnapshotCodec.Export(f.Snapshot());
            switch (mutation)
            {
                case "numeric-counter": json = json.Replace("\"sequence\":\"9007199254740993\"", "\"sequence\":9007199254740993"); break;
                case "leading-zero": json = json.Replace("\"sequence\":\"9007199254740993\"", "\"sequence\":\"09007199254740993\""); break;
                case "counter-overflow": json = json.Replace("\"sequence\":\"9007199254740993\"", "\"sequence\":\"9223372036854775808\""); break;
                case "negative-counter": json = json.Replace("\"sequence\":\"9007199254740993\"", "\"sequence\":\"-1\""); break;
                case "nan": json = json.Replace("\"health\":\"42c80000\"", "\"health\":\"7fc00000\""); break;
                case "infinity": json = json.Replace("\"health\":\"42c80000\"", "\"health\":\"7f800000\""); break;
                case "uppercase-bits": json = json.Replace("\"health\":\"42c80000\"", "\"health\":\"42C80000\""); break;
                case "short-bits": json = json.Replace("\"health\":\"42c80000\"", "\"health\":\"0000000\""); break;
                case "enum-number": json = json.Replace("\"kind\":\"Player\"", "\"kind\":\"0\""); break;
                case "duplicate-key": json = "{\"schema\":1," + json.Substring(1); break;
                case "unknown-key": json = "{\"script\":\"run\"," + json.Substring(1); break;
                case "future-schema": json = json.Replace("\"schema\":" + MatchSnapshot.Schema, "\"schema\":" + (MatchSnapshot.Schema + 1)); break;
                case "bad-hash": json = Regex.Replace(json, "\"hash\":\"[a-f0-9]{64}\"", "\"hash\":\"" + new string('0', 64) + "\""); break;
            }
            var imported = f.Import(json);
            Assert.IsFalse(imported.Success, mutation); Assert.IsNull(imported.Snapshot); Assert.IsNotEmpty(imported.Error);
        }

        [Test]
        public void OversizeDepthAndInvalidUnicodeAreRejected()
        {
            var f = Create();
            Assert.IsFalse(f.Import(new string(' ', MatchSnapshotCodec.MaximumBytes + 1)).Success);
            Assert.IsFalse(f.Import(new string('[', 25) + "0" + new string(']', 25)).Success);
            Assert.IsFalse(f.Import("\"\ud800\"").Success);
        }

        internal static string Rehash(string json)
        {
            string canonical = Regex.Replace(json, "\"hash\":\"[a-f0-9]{64}\",", "");
            return Regex.Replace(json, "\"hash\":\"[a-f0-9]{64}\"", "\"hash\":\"" + CourseJson.Sha256(canonical) + "\"");
        }
        sealed class CountingTarget : ITransactionalCommandTarget
        {
            internal int Count;
            public object CaptureTransactionState() => Count;
            public void RestoreTransactionState(object state) => Count = (int)state;
            public CommandEffect ApplyValidatedCommand(AuthenticatedCommand request) { Count++; return new CommandEffect("Applied"); }
        }
    }
}
