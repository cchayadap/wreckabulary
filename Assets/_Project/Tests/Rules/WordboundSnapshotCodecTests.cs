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
            f.Binding = new MatchBinding("match", MatchSnapshotCodec.ProtocolVersion, f.Mode.Hash, f.Course.SourceHash, f.Course.Hash,
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
            if (WordboundPracticePolicy.HasKeepsake(f.Mode))
                foreach (int team in seats.Select(s => s.Team).Distinct())
                {
                    f.Bodies.Add(new WorldBodyState("keepsake:" + team, WorldBodyKind.Keepsake, default,
                        ownerTeam: team, protectedResource: true));
                    if (f.Mode.IsRace)
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
            string json = MatchSnapshotCodec.Export(snapshot, out string hash);
            Assert.AreEqual(MatchSnapshotCodec.ComputeHash(snapshot), hash);
            Assert.AreEqual(json, MatchSnapshotCodec.Export(snapshot));
            Assert.AreEqual(json, Rehash(json));
            var imported = f.Import(json);
            Assert.IsTrue(imported.Success, imported.Error);
            Assert.AreEqual(json, MatchSnapshotCodec.Export(imported.Snapshot));
            Assert.AreEqual(MatchSnapshotCodec.ComputeHash(snapshot), imported.Hash);
            Assert.AreEqual(hash, imported.Hash);
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
            string expected = MatchSnapshotCodec.Export(snapshot, out string hash);
            f.Bodies.Reverse(); f.Resources.Reverse(); f.Mechanisms.Reverse();
            var previous = CultureInfo.CurrentCulture;
            try
            {
                foreach (string culture in new[] { "en-US", "fr-FR", "ar-SA" })
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                    string actual = MatchSnapshotCodec.Export(f.Snapshot(), out string actualHash);
                    Assert.AreEqual(expected, actual);
                    Assert.AreEqual(hash, actualHash);
                    Assert.AreEqual(MatchSnapshotCodec.ComputeHash(f.Snapshot()), actualHash);
                }
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [TestCase("en-US"), TestCase("fr-FR"), TestCase("ar-SA")]
        public void FrozenSectionOrderRetainsLiteralKeysAcrossConstructionAndCaptureCultures(string cultureName)
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                var f = Create();
                f.Resources.Clear(); f.Mechanisms.Clear();
                foreach (string id in new[] { "a", "a-", "a.", "a:", "a_", "a0", "aA" })
                    for (int team = 0; team < 8; team++)
                        for (int lap = 0; lap < 3; lap++)
                            for (int duplicate = 0; duplicate < 2; duplicate++)
                            {
                                f.Resources.Add(new MatchResourceState(id, team, lap, duplicate));
                                f.Mechanisms.Add(new MatchMechanismState(id, team, lap, phaseTick: duplicate));
                            }
                f.Resources.Reverse(); f.Mechanisms.Reverse();
                var retained = f.Snapshot();
                string json = MatchSnapshotCodec.Export(retained), hash = MatchSnapshotCodec.ComputeHash(retained);
                var culture = (CultureInfo)CultureInfo.GetCultureInfo(cultureName).Clone();
                culture.NumberFormat.NativeDigits = new[] { "٠", "١", "٢", "٣", "٤", "٥", "٦", "٧", "٨", "٩" };
                culture.NumberFormat.PositiveSign = "positive"; culture.NumberFormat.NegativeSign = "negative";
                culture.NumberFormat.NumberDecimalSeparator = "!"; culture.NumberFormat.NumberGroupSeparator = "?";
                CultureInfo.CurrentCulture = culture;
                AssertLiteralSectionOrder(f, f.Snapshot());
                Assert.AreEqual(json, MatchSnapshotCodec.Export(f.Snapshot()));
                f.Resources[0] = new MatchResourceState("a-", 7, 2, 12);
                f.Mechanisms[0] = new MatchMechanismState("a-", 7, 2, phaseTick: 12);
                var replaced = f.Snapshot();
                AssertLiteralSectionOrder(f, replaced);
                Assert.AreNotEqual(hash, MatchSnapshotCodec.ComputeHash(replaced));
                Assert.AreEqual(json, MatchSnapshotCodec.Export(retained));
                Assert.AreEqual(hash, MatchSnapshotCodec.ComputeHash(retained));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [TestCase(false), TestCase(true)]
        public void SharedResourceOrderRetainsLiveNegativeSignAndStableEqualKeys(bool reverse)
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
                CultureInfo.CurrentCulture = culture;
                var f = Create(); f.Resources.Clear();
                foreach (string id in new[] { "a", "a-", "a." })
                    foreach (int team in new[] { -1, 0, 1, -1 })
                        for (int lap = 0; lap < 3; lap++)
                            f.Resources.Add(new MatchResourceState(id, team, lap, f.Resources.Count));
                if (reverse) f.Resources.Reverse();
                var retained = f.Snapshot();
                string json = MatchSnapshotCodec.Export(retained), hash = MatchSnapshotCodec.ComputeHash(retained);
                foreach (string sign in new[] { "~", "", "minus", "-" })
                {
                    culture.NumberFormat.NegativeSign = sign;
                    AssertLiteralSectionOrder(f, f.Snapshot());
                    Assert.AreEqual(json, MatchSnapshotCodec.Export(retained));
                    Assert.AreEqual(hash, MatchSnapshotCodec.ComputeHash(retained));
                }
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        static void AssertLiteralSectionOrder(Fixture f, MatchSnapshot snapshot)
        {
            var resources = f.Resources.OrderBy(r => r.SourceId + "/" + r.Team + "/" + r.Lap, StringComparer.Ordinal).ToArray();
            var mechanisms = f.Mechanisms.OrderBy(m => m.PuzzleId + "/" + m.Team + "/" + m.Lap, StringComparer.Ordinal).ToArray();
            Assert.AreEqual(resources.Length, snapshot.Resources.Count);
            Assert.AreEqual(mechanisms.Length, snapshot.Mechanisms.Count);
            for (int i = 0; i < resources.Length; i++) Assert.AreSame(resources[i], snapshot.Resources[i]);
            for (int i = 0; i < mechanisms.Length; i++) Assert.AreSame(mechanisms[i], snapshot.Mechanisms[i]);
        }

        [TestCase("null"), TestCase("null-text"), TestCase("nan"), TestCase("infinity")]
        [TestCase("unicode"), TestCase("string-limit"), TestCase("token-limit"), TestCase("utf8-limit")]
        public void CombinedExportRetainsIndependentHashRejections(string invalid)
        {
            var f = Create();
            TestData.GiveLetters(f.Economy, 0, "BAT");
            Assert.IsTrue(f.Economy.BeginCraft(0, "BAT", 0).Ok);
            var job = f.Economy.Player(0).Craft;
            switch (invalid)
            {
                case "null-text": job.ItemId = null; break;
                case "nan": job.ReadyAt = double.NaN; break;
                case "infinity": job.StartedAt = double.PositiveInfinity; break;
                case "unicode": job.ItemId = "\ud800"; break;
                case "string-limit": job.ItemId = new string('A', 24579); break;
            }
            var snapshot = invalid == "null" ? null : f.Snapshot();
            if (invalid == "token-limit" || invalid == "utf8-limit")
            {
                var actors = invalid == "utf8-limit"
                    ? Enumerable.Repeat(new MatchActorState("a", 100, 100, appearance: new string('界', 2048)), 1400)
                    : snapshot.Actors;
                var bodies = invalid == "token-limit" ? Enumerable.Repeat(snapshot.Bodies[0], 8192) : snapshot.Bodies;
                snapshot = new MatchSnapshot(snapshot.Binding, snapshot.Epoch, snapshot.Sequence, snapshot.NextWorldEntityId,
                    snapshot.Progress, snapshot.Economy, snapshot.Commands, actors, bodies, snapshot.Resources,
                    snapshot.Mechanisms, snapshot.RandomStreams, snapshot.NarrativeFlags, snapshot.ThrownFlights, snapshot.Practice);
            }
            var independent = Assert.Catch(() => MatchSnapshotCodec.ComputeHash(snapshot));
            var combined = Assert.Catch(() => MatchSnapshotCodec.Export(snapshot, out _));
            var legacyEntry = Assert.Catch(() => MatchSnapshotCodec.Export(snapshot));
            Assert.AreEqual(independent.GetType(), combined.GetType());
            Assert.AreEqual(independent.Message, combined.Message);
            Assert.AreEqual(combined.GetType(), legacyEntry.GetType());
            Assert.AreEqual(combined.Message, legacyEntry.Message);
            if (invalid == "token-limit") Assert.AreEqual("JSON token limit exceeded.", combined.Message);
            if (invalid == "utf8-limit") Assert.AreEqual("JSON exceeds the UTF-8 byte limit.", combined.Message);
        }

        [Test]
        public void PublicationRevalidatesMutableRecipeWithoutConsumingSequenceAndKeepsRetainedBytes()
        {
            var f = WordboundAuthorityTests.Create();
            string source = f.Source.Course.Supplies.Single(s => s.SourceId == "BedSupply" && s.TeamIndex == 0).Id;
            foreach (var tile in f.State.Bodies.Where(b => b.SourceId == source).ToArray())
                Assert.AreEqual("Ok", f.Collect("a", tile.Id).Effect.Code);
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.BeginCraft, value: "BED")).Receipts.Single().Effect.Code);
            var first = f.Match.PublishSnapshot(out string firstJson);
            var retained = f.Source.Import(firstJson);
            Assert.IsTrue(retained.Success, retained.Error);
            Assert.AreEqual(1, first.Sequence);
            Assert.AreEqual(retained.Hash, first.StateHash);
            Assert.AreEqual(retained.Snapshot.Progress.Tick, first.Tick);
            Assert.IsTrue(f.Match.Acknowledge(first));
            var recipe = f.Source.Catalogue.Get("BED");
            bool enabled = recipe.Enabled;
            try
            {
                recipe.Enabled = false;
                string expected = MatchStateValidation.Validate(f.State, f.Source.Binding, f.Source.Mode,
                    f.Source.Course, f.Source.Catalogue, f.Source.Rules);
                Assert.IsNotEmpty(expected);
                for (int attempt = 0; attempt < 2; attempt++)
                    Assert.AreEqual(expected, Assert.Throws<ArgumentException>(() => f.Match.PublishSnapshot(out _)).Message);
            }
            finally { recipe.Enabled = enabled; }
            var second = f.Match.PublishSnapshot(out string secondJson);
            Assert.AreEqual(first.Sequence + 1, second.Sequence);
            Assert.AreEqual(first.Tick, second.Tick);
            var secondImport = f.Source.Import(secondJson);
            Assert.IsTrue(secondImport.Success, secondImport.Error);
            Assert.AreEqual(secondImport.Hash, second.StateHash);
            Assert.IsTrue(f.Match.Acknowledge(second));
            Assert.IsFalse(f.Match.Acknowledge(first));
            Assert.IsTrue(f.Step().Advanced);
            var third = f.Match.PublishSnapshot(out string thirdJson);
            var thirdImport = f.Source.Import(thirdJson);
            Assert.IsTrue(thirdImport.Success, thirdImport.Error);
            Assert.AreEqual(second.Sequence + 1, third.Sequence);
            Assert.AreEqual(second.Tick + 1, third.Tick);
            Assert.AreEqual(thirdImport.Hash, third.StateHash);
            Assert.AreNotEqual(second.StateHash, third.StateHash);
            Assert.IsTrue(f.Match.Acknowledge(third));
            Assert.AreEqual(firstJson, MatchSnapshotCodec.Export(retained.Snapshot, out string retainedHash));
            Assert.AreEqual(first.StateHash, retainedHash);
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
