#if UNITY_EDITOR || GSK_QA
using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundPhaseCaptureTests
    {
        [Test]
        public void DisabledScopesDoNotAcquireACaptureOrContributeToTheNextWindow()
        {
            using (WordboundPhaseCapture.Measure(WordboundPhase.Synchronize)) { }
            using (WordboundPhaseCapture.Measure((WordboundPhase)999)) { }
            using var capture = WordboundPhaseCapture.TryStart();
            Assert.IsNotNull(capture);
            capture.Dispose();
            Assert.AreEqual(0, capture.Count(WordboundPhase.Synchronize));
            Assert.AreEqual(WordboundPhaseInvalid.None, capture.Invalid);
        }

        [Test]
        public void NestedScopesReportInclusiveDurationsAndCloseOnExceptions()
        {
            using var capture = WordboundPhaseCapture.TryStart();
            var failure = new InvalidOperationException("measured exception");
            var caught = Assert.Throws<InvalidOperationException>(() => {
                using var outer = WordboundPhaseCapture.Measure(WordboundPhase.FullValidation);
                using var inner = WordboundPhaseCapture.Measure(WordboundPhase.EconomyValidation);
                Thread.SpinWait(512);
                throw failure;
            });
            capture.Dispose();
            Assert.AreSame(failure, caught);
            Assert.AreEqual(0, capture.InFlight);
            Assert.AreEqual(WordboundPhaseInvalid.None, capture.Invalid);
            Assert.AreEqual(1, capture.Count(WordboundPhase.FullValidation));
            Assert.AreEqual(1, capture.Count(WordboundPhase.EconomyValidation));
            Assert.Greater(capture.TotalTicks(WordboundPhase.EconomyValidation), 0);
            Assert.GreaterOrEqual(capture.TotalTicks(WordboundPhase.FullValidation), capture.TotalTicks(WordboundPhase.EconomyValidation));
        }

        [Test]
        public void OverlappingCaptureCannotReplaceTheCurrentOwner()
        {
            using var capture = WordboundPhaseCapture.TryStart();
            Assert.IsNull(WordboundPhaseCapture.TryStart());
            using (WordboundPhaseCapture.Measure(WordboundPhase.Prepare)) { }
            capture.Dispose();
            Assert.AreEqual(1, capture.Count(WordboundPhase.Prepare));
            Assert.AreEqual(WordboundPhaseInvalid.None, capture.Invalid);
        }

        [Test]
        public void AnotherThreadHasAnIndependentOwnerAndCannotPolluteThisWindow()
        {
            using var capture = WordboundPhaseCapture.TryStart();
            WordboundPhaseCapture other = null;
            Exception failure = null;
            var thread = new Thread(() => {
                try {
                    using (WordboundPhaseCapture.Measure(WordboundPhase.Prepare)) { }
                    using (other = WordboundPhaseCapture.TryStart())
                        using (WordboundPhaseCapture.Measure(WordboundPhase.FrameChanged)) { }
                }
                catch (Exception error) { failure = error; }
            });
            thread.Start(); thread.Join();
            capture.Dispose();
            Assert.IsNull(failure); Assert.IsNotNull(other);
            Assert.AreNotEqual(capture.Generation, other.Generation);
            Assert.AreEqual(0, capture.Count(WordboundPhase.FrameChanged));
            Assert.AreEqual(0, other.Count(WordboundPhase.Prepare));
            Assert.AreEqual(1, other.Count(WordboundPhase.FrameChanged));
            Assert.AreEqual(WordboundPhaseInvalid.None, capture.Invalid);
            Assert.AreEqual(WordboundPhaseInvalid.None, other.Invalid);
        }

        [Test]
        public void CrossThreadDisposalIsInvalidAndLeavesOwnerCleanupPossible()
        {
            using var capture = WordboundPhaseCapture.TryStart();
            var scope = WordboundPhaseCapture.Measure(WordboundPhase.Prepare);
            var thread = new Thread(() => { scope.Dispose(); capture.Dispose(); });
            thread.Start(); thread.Join();
            Assert.IsFalse(capture.Stopped);
            Assert.AreEqual(1, capture.InFlight);
            scope.Dispose(); capture.Dispose();
            Assert.AreEqual(WordboundPhaseInvalid.WrongThread, capture.Invalid);
            Assert.AreEqual(0, capture.InFlight);
        }

        [Test]
        public void StoppedUnfinishedScopeCannotEndOrAddToANewGeneration()
        {
            using var old = WordboundPhaseCapture.TryStart();
            var scope = WordboundPhaseCapture.Measure(WordboundPhase.Prepare);
            old.Dispose();
            using var current = WordboundPhaseCapture.TryStart();
            Assert.IsNotNull(current);
            scope.Dispose(); old.Dispose();
            using (WordboundPhaseCapture.Measure(WordboundPhase.Prepare)) { }
            current.Dispose();
            Assert.AreEqual(WordboundPhaseInvalid.Unfinished | WordboundPhaseInvalid.LateScope, old.Invalid);
            Assert.AreEqual(1, old.InFlight);
            Assert.AreEqual(0, old.Count(WordboundPhase.Prepare));
            Assert.AreEqual(1, current.Count(WordboundPhase.Prepare));
            Assert.AreEqual(WordboundPhaseInvalid.None, current.Invalid);
        }

        [TestCase(false), TestCase(true)]
        public void DuplicateOrOutOfOrderDisposalInvalidatesEvenWhenOtherScopesRemain(bool outOfOrder)
        {
            using var capture = WordboundPhaseCapture.TryStart();
            var outer = WordboundPhaseCapture.Measure(WordboundPhase.FullValidation);
            var inner = WordboundPhaseCapture.Measure(WordboundPhase.EconomyValidation);
            if (outOfOrder) outer.Dispose();
            inner.Dispose();
            if (!outOfOrder) inner.Dispose();
            outer.Dispose(); capture.Dispose();
            Assert.AreEqual(WordboundPhaseInvalid.Unbalanced, capture.Invalid);
            Assert.AreEqual(0, capture.InFlight);
            Assert.AreEqual(1, capture.Count(WordboundPhase.FullValidation));
            Assert.AreEqual(1, capture.Count(WordboundPhase.EconomyValidation));
        }

        [Test]
        public void ExcessNestingIsBoundedAndDoesNotThrowThroughGameplay()
        {
            using var capture = WordboundPhaseCapture.TryStart();
            var scopes = new WordboundPhaseCapture.Scope[257];
            for (int i = 0; i < scopes.Length; i++) scopes[i] = WordboundPhaseCapture.Measure(WordboundPhase.Prepare);
            for (int i = scopes.Length - 1; i >= 0; i--) scopes[i].Dispose();
            capture.Dispose();
            Assert.AreEqual(WordboundPhaseInvalid.Overflow, capture.Invalid);
            Assert.AreEqual(0, capture.InFlight);
            Assert.AreEqual(256, capture.Count(WordboundPhase.Prepare));
        }

        [Test]
        public void InvalidPhaseAndEmptyTimingAreNeverPresentedAsMeasuredZeroCost()
        {
            using var capture = WordboundPhaseCapture.TryStart();
            using (WordboundPhaseCapture.Measure((WordboundPhase)(-1))) { }
            capture.Dispose();
            Assert.AreEqual(WordboundPhaseInvalid.InvalidPhase, capture.Invalid);
            string report = string.Join("\n", capture.Report("test", "invalid", new WordboundPhaseBoundary(null), new WordboundPhaseBoundary(null)));
            Assert.IsTrue(report.Contains("status=invalid"));
            Assert.IsTrue(report.Contains("elapsedMs=unavailable"));
            using var empty = WordboundPhaseCapture.TryStart();
            empty.Dispose();
            Assert.IsTrue(empty.Report("test", "empty", default, default).Skip(1).All(line =>
                line.Contains("status=unavailable") && line.Contains("elapsedMs=unavailable")));
        }

        [TestCase("TimeRun"), TestCase("TeamRace"), TestCase("BrawlRace"), TestCase("FreeForAll"), TestCase("Freestyle")]
        public void RealAdvanceAndAtomicValidationHaveIdenticalResultsWithCaptureEnabled(string mode)
        {
            var control = WordboundAuthorityTests.Create(mode);
            var measured = WordboundAuthorityTests.Create(mode);
            Assert.IsTrue(control.Match.Advance(17).Advanced);
            using var capture = WordboundPhaseCapture.TryStart();
            Assert.IsTrue(measured.Match.Advance(17).Advanced);
            var frame = measured.State; var source = measured.Source;
            Assert.IsTrue(MatchStateValidation.ReconcileAndApply(frame, source.Binding, source.Mode, source.Course,
                source.Catalogue, source.Rules, source.Rules.Clone(), () => true, () => { }));
            capture.Dispose();
            Assert.AreEqual(MatchSnapshotCodec.Export(control.State), MatchSnapshotCodec.Export(frame));
            Assert.AreEqual(WordboundPhaseInvalid.None, capture.Invalid);
            Assert.AreEqual(0, capture.InFlight);
            foreach (var phase in new[] { WordboundPhase.RollbackCapture, WordboundPhase.Synchronize,
                WordboundPhase.RetireResources, WordboundPhase.FullValidation, WordboundPhase.MutableValidation })
                Assert.AreEqual(1, capture.Count(phase), phase.ToString());
            Assert.AreEqual(2, capture.Count(WordboundPhase.EconomyValidation));
        }

        [Test]
        public void FailedHostQueryStillRollsBackAndRetriesWithTheSameCommand()
        {
            var f = WordboundAuthorityTests.Create();
            string item = f.CraftBed();
            f.Step(f.Command("a", WordboundAction.Deploy, slot: 0));
            string before = MatchSnapshotCodec.Export(f.State);
            var failure = new InvalidOperationException("query failure");
            f.World.DuringQuery = () => {
                Assert.Throws<InvalidOperationException>(() => f.Match.Advance(10000));
                throw failure;
            };
            var command = f.Command("a", WordboundAction.UsePuzzle, "BedBridge", value: item);
            using var capture = WordboundPhaseCapture.TryStart();
            Assert.AreSame(failure, Assert.Throws<InvalidOperationException>(() => f.Step(command)));
            Assert.AreEqual(before, MatchSnapshotCodec.Export(f.State));
            Assert.AreEqual(0, capture.InFlight);
            f.World.DuringQuery = null;
            Assert.AreEqual("Ok", f.Step(command).Receipts.Single().Effect.Code);
            capture.Dispose();
            Assert.AreEqual(2, capture.Count(WordboundPhase.RollbackCapture));
            Assert.AreEqual(1, capture.Count(WordboundPhase.Synchronize));
            Assert.AreEqual(WordboundPhaseInvalid.None, capture.Invalid);
        }

        [TestCase("abort"), TestCase("prepare-error"), TestCase("apply-error")]
        public void AtomicAbortAndCallbackExceptionsKeepTheirBoundariesAndIdentity(string ending)
        {
            var f = WordboundSnapshotCodecTests.Create();
            var frame = f.Snapshot(); string before = MatchSnapshotCodec.Export(frame);
            var failure = new OperationCanceledException("callback failure");
            using var capture = WordboundPhaseCapture.TryStart();
            Func<bool> run = () => MatchStateValidation.ReconcileAndApply(frame, f.Binding, f.Mode, f.Course,
                f.Catalogue, f.Rules, f.Rules.Clone(), () => {
                    if (ending == "prepare-error") throw failure;
                    return ending != "abort";
                }, () => { if (ending == "apply-error") throw failure; });
            if (ending == "abort") Assert.IsFalse(run());
            else Assert.AreSame(failure, Assert.Throws<OperationCanceledException>(() => run()));
            capture.Dispose();
            Assert.AreEqual(before, MatchSnapshotCodec.Export(frame));
            Assert.AreEqual(0, capture.InFlight);
            Assert.AreEqual(WordboundPhaseInvalid.None, capture.Invalid);
            Assert.AreEqual(1, capture.Count(WordboundPhase.FullValidation));
            Assert.AreEqual(ending == "apply-error" ? 1 : 0, capture.Count(WordboundPhase.MutableValidation));
        }
    }
}
#endif
