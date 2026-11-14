using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundStoryEpilogueTests
    {
        ItemCatalogue items;
        WordboundFlagshipContent content;
        ResolvedCourse course;
        ModeDefinition mode;
        WordboundSeat[] seats;
        MatchBinding binding;
        MatchProgress progress;
        WordboundStoryPlan plan;

        [OneTimeSetUp]
        public void Content()
        {
            items = WordboundPuzzleGuidePlanTests.Items();
            content = WordboundFlagshipContent.Create(items);
        }

        void Start(string modeId = "TimeRun", CourseDocument document = null)
        {
            mode = ModeDefinition.For(modeId);
            var resolved = CourseResolver.Resolve(document ?? content.CopyDocument(), content.Geometry,
                new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(mode)));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            course = resolved.Manifest;
            seats = mode.HasRaceFormat ? new[] { new WordboundSeat("a", 0), new WordboundSeat("b", 1) }
                : new[] { new WordboundSeat("a", 0) };
            binding = Binding();
            progress = new MatchProgress(mode, seats, MatchStateValidation.Route(course));
            plan = new WordboundStoryPlan(course, items);
        }

        MatchBinding Binding(string source = null, string manifest = null, string modeHash = null)
            => new MatchBinding("epilogue", MatchSnapshotCodec.ProtocolVersion, modeHash ?? mode.Hash,
                source ?? course.SourceHash, manifest ?? course.Hash, new string('a', 64), new string('b', 64), seats);

        MatchSnapshot Frame(string[] flags = null, MatchBinding identity = null)
        {
            identity ??= binding;
            var state = progress.Capture();
            flags ??= mode.IsRace ? state.Teams.SelectMany(t => Enumerable.Range(0, Math.Min(t.Lap, 2) + 1)
                .Select(lap => MatchStateValidation.NarrativeFlag(t.Team, lap))).ToArray() : Array.Empty<string>();
            return new MatchSnapshot(identity, 0, 1, 1, state, new Economy(items, new GameRules()).Capture(),
                new CommandJournal(identity.Hash, seats.Select(s => s.PlayerId)).Capture(), Array.Empty<MatchActorState>(),
                Array.Empty<WorldBodyState>(), Array.Empty<MatchResourceState>(), Array.Empty<MatchMechanismState>(),
                Array.Empty<MatchRandomState>(), flags);
        }

        void Confirm() => typeof(MatchProgress).GetMethod("ConfirmResult", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(progress, new object[] { progress.Tick });

        void ReadyToDeliver(string actor = "a")
        {
            int team = progress.Player(actor).Team;
            while (progress.Team(team).Lap < mode.Laps)
            {
                int lap = progress.Team(team).Lap;
                while (progress.Team(team).NextCheckpoint < course.Laps[lap].Checkpoints.Count)
                    WordboundPuzzleGuidePlanTests.StepCheckpoint(progress, course, actor);
                if (progress.Team(team).KeepsakeCarrier == null) Assert.IsTrue(progress.TakeKeepsake(actor));
                if (lap == mode.Laps - 1) return;
                Finish(actor);
            }
        }

        void Finish(params string[] actors)
            => progress.Advance(progress.Tick + 1, progress.WallClockMs + 17, actors.Select(actor => {
                int lap = progress.Team(progress.Player(actor).Team).Lap;
                return new ProgressEvent(ProgressEventKind.FinishReached, actor, course.Laps[lap].FinishModuleId, lap);
            }).ToArray());

        void WaitUntil(long elapsed)
        {
            while (progress.ElapsedTicks < elapsed)
            {
                Assert.IsNull(progress.Result);
                progress.Advance(progress.Tick + 1, progress.WallClockMs + 17);
            }
        }

        long Limit => (long)mode.TimeLimitSeconds * ModeDefinition.TickRate;

        [Test]
        public void ProvisionalThenConfirmedAtTheSameTickRevealsOnlyEarnedDelivery()
        {
            Start();
            Assert.IsNull(plan.Epilogue(Frame(), "a", mode));
            ReadyToDeliver();
            Assert.AreEqual(72, content.PuzzleSites.Count);
            Assert.IsNull(plan.Epilogue(Frame(), "a", mode));
            Finish("a");
            var provisional = Frame();
            Assert.AreEqual("Win", provisional.Progress.Result.Outcome);
            Assert.IsNull(plan.Epilogue(provisional, "a", mode));
            Confirm();
            var confirmed = Frame();
            Assert.AreEqual(provisional.Progress.Tick, confirmed.Progress.Tick);
            var ending = plan.Epilogue(confirmed, "a", mode);
            Assert.AreEqual("TimeRunSuccess", ending.Id);
            Assert.AreEqual("The keepsake is home.", ending.Cue);
            Assert.AreEqual(ending.ResultKey, plan.Epilogue(confirmed, "a", mode).ResultKey);
            Assert.IsNull(plan.Epilogue(provisional, "a", mode));
            string before = MatchSnapshotCodec.Export(confirmed);
            plan.Epilogue(confirmed, "a", mode);
            Assert.AreEqual(before, MatchSnapshotCodec.Export(confirmed));
        }

        [TestCase("TimeRun"), TestCase("TeamRace"), TestCase("BrawlRace")]
        public void FinishOnTheExactLimitStillMeansDelivery(string modeId)
        {
            Start(modeId);
            ReadyToDeliver();
            WaitUntil(Limit - 1);
            Finish("a");
            Confirm();
            Assert.AreEqual(Limit, progress.ElapsedTicks);
            Assert.AreEqual(modeId == "TimeRun" ? "TimeRunSuccess" : "RaceDelivery", plan.Epilogue(Frame(), "a", mode).Id);
        }

        [Test]
        public void TimeRunExpiryHasNoWinnerOrDeliveryClaim()
        {
            Start();
            WaitUntil(Limit);
            Confirm();
            Assert.AreEqual("TimeExpired", progress.Result.Outcome);
            Assert.IsEmpty(progress.Result.WinningTeams);
            Assert.AreEqual("TimeRunExpired", plan.Epilogue(Frame(), "a", mode).Id);
            Assert.IsEmpty(plan.Journal(Frame(), "a"));
        }

        [TestCase("TeamRace"), TestCase("BrawlRace")]
        public void SameTickFinishDrawGivesEachTeamItsOwnDelivery(string modeId)
        {
            Start(modeId);
            ReadyToDeliver("a"); ReadyToDeliver("b");
            Finish("a", "b"); Confirm();
            Assert.AreEqual("Draw", progress.Result.Outcome);
            var frame = Frame();
            Assert.AreEqual("RaceDelivery", plan.Epilogue(frame, "a", mode).Id);
            Assert.AreEqual("RaceDelivery", plan.Epilogue(frame, "b", mode).Id);
            Assert.AreNotEqual(plan.Epilogue(frame, "a", mode).ResultKey, plan.Epilogue(frame, "b", mode).ResultKey);
        }

        [TestCase(false), TestCase(true)]
        public void RaceTimeoutSeparatesProgressDrawWinAndDefeat(bool advanceOwnTeam)
        {
            Start("TeamRace");
            if (advanceOwnTeam) WordboundPuzzleGuidePlanTests.StepCheckpoint(progress, course);
            WaitUntil(Limit); Confirm();
            Assert.IsTrue(progress.Capture().Teams.All(t => !t.Finished));
            Assert.AreEqual(advanceOwnTeam ? "RaceProgressWin" : "RaceProgressDraw", plan.Epilogue(Frame(), "a", mode).Id);
            Assert.AreEqual(advanceOwnTeam ? "RaceDefeat" : "RaceProgressDraw", plan.Epilogue(Frame(), "b", mode).Id);
        }

        [Test]
        public void DefeatRecapUsesOnlyTheLosingTeamsEarnedClues()
        {
            Start("TeamRace");
            WordboundPuzzleGuidePlanTests.StepCheckpoint(progress, course, "b");
            ReadyToDeliver(); Finish("a"); Confirm();
            Assert.AreEqual("RaceDefeat", plan.Epilogue(Frame(), "b", mode).Id);
            Assert.AreEqual(1, plan.Journal(Frame(), "b").Count);
            Assert.AreEqual(1, plan.Journal(Frame(), "b")[0].Lap);
            var rivalsOnly = Frame(Enumerable.Range(0, 3).Select(l => MatchStateValidation.NarrativeFlag(0, l)).ToArray());
            Assert.IsEmpty(plan.Journal(rivalsOnly, "b"));
            Assert.IsNull(plan.Epilogue(Frame(new[] { MatchStateValidation.NarrativeFlag(1, 2) }), "a", mode));
        }

        [Test]
        public void MissingFinalFlagFutureFlagsAndReachedRepairNeverInventHomecoming()
        {
            Start();
            ReadyToDeliver();
            Assert.IsTrue(plan.Journal(Frame(), "a").Any(n => n.Id == "3:L3_Packing_RepairBay"));
            Assert.IsNull(plan.Epilogue(Frame(), "a", mode));
            Finish("a"); Confirm();
            Assert.IsNull(plan.Epilogue(Frame(new[] { MatchStateValidation.NarrativeFlag(0, 0),
                MatchStateValidation.NarrativeFlag(0, 1) }), "a", mode));
            Assert.AreEqual("TimeRunSuccess", plan.Epilogue(Frame(), "a", mode).Id);
            Start("TeamRace");
            WordboundPuzzleGuidePlanTests.StepCheckpoint(progress, course);
            ReadyToDeliver("b"); Finish("b"); Confirm();
            var future = Frame(Enumerable.Range(0, 3).Select(l => MatchStateValidation.NarrativeFlag(0, l)).ToArray());
            Assert.AreEqual("RaceDefeat", plan.Epilogue(future, "a", mode).Id);
            Assert.IsTrue(plan.Journal(future, "a").All(n => n.Lap == 1));
        }

        [Test]
        public void UnknownReaderMismatchedBindingModeAndCustomCourseFailClosed()
        {
            Start(); ReadyToDeliver(); Finish("a"); Confirm();
            Assert.IsNull(plan.Epilogue(null, "a", mode));
            Assert.IsNull(plan.Epilogue(Frame(), "outsider", mode));
            Assert.IsNull(plan.Epilogue(Frame(), "a", null));
            Assert.IsNull(plan.Epilogue(Frame(), "a", ModeDefinition.For("TeamRace")));
            Assert.IsNull(plan.Epilogue(Frame(identity: Binding(source: new string('c', 64))), "a", mode));
            Assert.IsNull(plan.Epilogue(Frame(identity: Binding(manifest: new string('c', 64))), "a", mode));
            Assert.IsNull(plan.Epilogue(Frame(identity: Binding(modeHash: new string('c', 64))), "a", mode));
            var custom = content.CopyDocument(); custom.Title = "A different delivery";
            Start(document: custom); ReadyToDeliver(); Finish("a"); Confirm();
            Assert.IsNotEmpty(plan.Notices);
            Assert.IsNull(plan.Epilogue(Frame(), "a", mode));
        }

        [Test]
        public void MigrationDisconnectAndReturnCannotInventAFailureAndReconnectKeepsTheKey()
        {
            Start(); ReadyToDeliver(); Finish("a"); Confirm();
            var expected = plan.Epilogue(Frame(), "a", mode);
            progress.BeginMigration();
            Assert.IsNull(plan.Epilogue(Frame(), "a", mode));
            progress.CompleteMigration(progress.WallClockMs + 1);
            Assert.IsTrue(progress.Disconnect("a", progress.WallClockMs + 1));
            Assert.IsNull(plan.Epilogue(Frame(), "a", mode));
            Assert.IsTrue(progress.Reconnect("a", progress.WallClockMs + 1));
            Assert.AreEqual(expected.ResultKey, plan.Epilogue(Frame(), "a", mode).ResultKey);
            progress.Return("a", progress.WallClockMs + 1);
            Assert.IsNull(plan.Epilogue(Frame(), "a", mode));
        }

        [TestCase("FreeForAll"), TestCase("Freestyle")]
        public void NonStoryModesNeverAcquireAnEndingFromAConfirmedRaceFrame(string otherMode)
        {
            Start(); ReadyToDeliver(); Finish("a"); Confirm();
            Assert.IsNull(plan.Epilogue(Frame(), "a", ModeDefinition.For(otherMode)));
        }

        [Test]
        public void FreestyleReturnAndConfirmedFreeForAllTimeoutHaveNoStoryEnding()
        {
            Start("Freestyle");
            Assert.IsNull(plan.Epilogue(Frame(), "a", mode));
            progress.Return("a", 1);
            Assert.IsNull(plan.Epilogue(Frame(), "a", mode));
            mode = ModeDefinition.For("FreeForAll");
            var resolved = CourseResolver.Resolve(CourseTemplates.CombatArena(), CourseTemplates.ProofCatalogue(),
                new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(mode)));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            course = resolved.Manifest;
            seats = new[] { new WordboundSeat("a", 0), new WordboundSeat("b", 1) };
            binding = Binding();
            progress = new MatchProgress(mode, seats, MatchStateValidation.Route(course));
            plan = new WordboundStoryPlan(course, items);
            WaitUntil(Limit); Confirm();
            Assert.AreEqual("Draw", progress.Result.Outcome);
            Assert.IsNull(plan.Epilogue(Frame(), "a", mode));
        }

        [TestCase("wrong-winner"), TestCase("duplicate-winner"), TestCase("wrong-outcome"), TestCase("wrong-tick")]
        public void ContradictoryConfirmedResultsFallBackToPlainResults(string invalid)
        {
            Start("TeamRace"); ReadyToDeliver(); Finish("a");
            string outcome = invalid == "wrong-outcome" ? "Draw" : "Win";
            int[] winners = invalid == "wrong-winner" ? new[] { 1 } : invalid == "duplicate-winner" ? new[] { 0, 0 } : new[] { 0 };
            var result = (MatchResult)Activator.CreateInstance(typeof(MatchResult), BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { outcome, winners, progress.Tick + (invalid == "wrong-tick" ? 1 : 0) }, null);
            typeof(MatchProgress).GetProperty("Result").SetValue(progress, result);
            typeof(MatchResult).GetProperty("Confirmed").SetValue(result, true);
            Assert.IsNull(plan.Epilogue(Frame(), "a", mode));
        }

        [Test]
        public void CodecAndCommittedEpochRecoveryRetainConfirmedIdentityWithoutChangingBytes()
        {
            Start();
            var rules = mode.CreateGameplayRules(new GameRules());
            var authority = new AuthoritativeMatch(binding, mode, course, content.Geometry, items, rules, new EmptyWorld());
            var initial = authority.Capture(1);
            string initialJson = MatchSnapshotCodec.Export(initial);
            Assert.IsTrue(initial.Economy.Players.All(player => player.CanAct));
            WaitUntil(Limit);
            MatchSnapshot Timeout(EconomySnapshot economy) => new MatchSnapshot(binding, 0, 1, initial.NextWorldEntityId, progress.Capture(), economy,
                initial.Commands, initial.Actors, initial.Bodies, initial.Resources, initial.Mechanisms,
                initial.RandomStreams, initial.NarrativeFlags, initial.ThrownFlights, initial.Practice);
            var contradictory = MatchSnapshotCodec.Import(MatchSnapshotCodec.Export(Timeout(initial.Economy)), binding, mode, course, items, rules);
            Assert.IsFalse(contradictory.Success);
            Assert.IsNull(contradictory.Snapshot);
            StringAssert.Contains("inventory action permission", contradictory.Error.ToLowerInvariant());
            var endedEconomy = new Economy(items, rules);
            foreach (var player in initial.Economy.Players) endedEconomy.AddPlayer(player.PlayerId, player.Team);
            endedEconomy.Restore(initial.Economy);
            foreach (var player in endedEconomy.Players) player.CanAct = false;
            var timeout = Timeout(endedEconomy.Capture());
            Assert.AreEqual(initialJson, MatchSnapshotCodec.Export(initial));
            Assert.IsFalse(timeout.Progress.Result.Confirmed);
            string provisionalJson = MatchSnapshotCodec.Export(timeout, out string hash);
            var imported = MatchSnapshotCodec.Import(provisionalJson, binding, mode, course, items, rules);
            Assert.IsTrue(imported.Success, imported.Error);
            Assert.IsNull(plan.Epilogue(imported.Snapshot, "a", mode));
            authority.RestoreCommitted(imported.Snapshot, new SnapshotStamp(binding.MatchId, binding.Hash, 0, 1,
                timeout.Progress.Tick, hash), 1, timeout.Progress.WallClockMs + 1);
            var first = authority.Capture(2);
            Assert.AreEqual(timeout.Progress.Tick, first.Progress.Tick);
            Assert.IsTrue(first.Progress.Result.Confirmed);
            Assert.IsTrue(first.Economy.Players.All(player => !player.CanAct));
            var ending = plan.Epilogue(first, "a", mode);
            Assert.AreEqual("TimeRunExpired", ending.Id);
            string confirmedJson = MatchSnapshotCodec.Export(first, out string confirmedHash);
            var restored = MatchSnapshotCodec.Import(confirmedJson, binding, mode, course, items, rules);
            Assert.IsTrue(restored.Success, restored.Error);
            Assert.AreEqual(ending.ResultKey, new WordboundStoryPlan(course, items).Epilogue(restored.Snapshot, "a", mode).ResultKey);
            Assert.AreEqual(confirmedJson, MatchSnapshotCodec.Export(restored.Snapshot));
            authority.RestoreCommitted(restored.Snapshot, new SnapshotStamp(binding.MatchId, binding.Hash, 1, 2,
                first.Progress.Tick, confirmedHash), 2, first.Progress.WallClockMs + 1);
            var migrated = authority.Capture(3);
            Assert.IsTrue(migrated.Economy.Players.All(player => !player.CanAct));
            Assert.AreEqual(ending.Id, plan.Epilogue(migrated, "a", mode).Id);
            Assert.AreEqual(ending.ResultKey, plan.Epilogue(migrated, "a", mode).ResultKey);
            Assert.AreEqual(ending.Title, plan.Epilogue(migrated, "a", mode).Title);
            Assert.AreEqual(ending.Cue, plan.Epilogue(migrated, "a", mode).Cue);
        }

        [Test]
        public void AuthoredEpilogueCopyChangesOnlyPresentation()
        {
            Start(); ReadyToDeliver(); Finish("a"); Confirm();
            var profile = ScriptableObject.CreateInstance<WordboundStoryProfile>();
            try
            {
                profile.AddMissingEpilogues();
                profile.Epilogues.Single(e => e.Id == "TimeRunSuccess").Cue.Key = "test.delivery";
                var localized = new WordboundStoryPlan(course, items, profile, key => key == "test.delivery" ? "Home together." : null);
                var frame = Frame();
                string before = MatchSnapshotCodec.Export(frame);
                Assert.AreEqual("Home together.", localized.Epilogue(frame, "a", mode).Cue);
                Assert.AreEqual(plan.Epilogue(frame, "a", mode).ResultKey, localized.Epilogue(frame, "a", mode).ResultKey);
                Assert.AreEqual(before, MatchSnapshotCodec.Export(frame));
            }
            finally { UnityEngine.Object.DestroyImmediate(profile); }
        }

        sealed class EmptyWorld : IWordboundWorldQuery
        {
            public bool HasLineOfSight(string actorId, WorldVector from, WorldVector to) => false;
            public bool CanDeploy(string actorId, string recipeId, WorldVector position) => false;
            public bool PuzzleSatisfied(string actorId, ResolvedCoursePuzzle puzzle, WorldBodyState placedItem) => false;
        }
    }
}
