using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public sealed class WordboundCommandTests
    {
        static readonly string Identity = new string('a', 64);
        static AuthenticatedCommand Request(string actor, long sequence = 1, long epoch = 0, string target = "tile") =>
            new AuthenticatedCommand(actor, new PlayerCommand(sequence, epoch, WordboundAction.CollectLetter, target));

        sealed class PickupTarget : ITransactionalCommandTarget
        {
            public readonly List<string> Winners = new List<string>();
            public bool Fail;
            public object CaptureTransactionState() => Winners.ToArray();
            public void RestoreTransactionState(object state) { Winners.Clear(); Winners.AddRange((string[])state); }
            public CommandEffect ApplyValidatedCommand(AuthenticatedCommand command)
            {
                if (command.Command.Target == "throws")
                {
                    Winners.Add("partial");
                    if (Fail) throw new InvalidOperationException("simulated transaction failure");
                }
                if (Winners.Count != 0) return new CommandEffect("AlreadyTaken");
                Winners.Add(command.ActorId);
                return new CommandEffect("Collected", command.Command.Target);
            }
        }

        [Test]
        public void ContestedPickupUsesStableActorOrderInsteadOfArrivalOrder()
        {
            foreach (bool reverse in new[] { false, true })
            {
                var journal = new CommandJournal(Identity, new[] { "a", "b" });
                var target = new PickupTarget();
                var requests = new[] { Request("b"), Request("a") };
                var receipts = journal.ExecuteBatch(1, reverse ? requests.Reverse() : requests, target);
                CollectionAssert.AreEqual(new[] { "a" }, target.Winners);
                Assert.AreEqual("Collected", receipts[0].Effect.Code);
                Assert.AreEqual("AlreadyTaken", receipts[1].Effect.Code);
            }
        }

        [Test]
        public void ReplayedCommandsReturnSameReceiptAndChangedPayloadIsRejected()
        {
            var journal = new CommandJournal(Identity, new[] { "a" });
            var target = new PickupTarget();
            var first = journal.ExecuteBatch(1, new[] { Request("a") }, target).Single();
            var replay = journal.ExecuteBatch(2, new[] { Request("a") }, target).Single();
            Assert.AreSame(first, replay);
            var changed = journal.ExecuteBatch(3, new[] { Request("a", target: "other") }, target).Single();
            Assert.AreEqual("PayloadConflict", changed.Effect.Code);
            Assert.AreEqual(1, target.Winners.Count);
            Assert.AreEqual(1, journal.Capture().Frontiers["a"]);
        }

        [Test]
        public void EpochChangePreservesReceiptsAndRejectsStaleConnections()
        {
            var journal = new CommandJournal(Identity, new[] { "a" });
            var target = new PickupTarget();
            var receipt = journal.ExecuteBatch(1, new[] { Request("a") }, target).Single();
            var resumed = new CommandJournal(Identity, new[] { "a" });
            resumed.Restore(journal.Capture());
            resumed.AdvanceEpoch(1);
            Assert.AreEqual("StaleEpoch", resumed.ExecuteBatch(2, new[] { Request("a") }, target)[0].Effect.Code);
            Assert.AreSame(receipt, resumed.ExecuteBatch(2, new[] { Request("a", epoch: 1) }, target)[0]);
            Assert.AreEqual(1, target.Winners.Count);
        }

        [Test]
        public void ConflictingBatchCannotChoosePayloadByArrivalAndOldEpochCannotBlockCurrent()
        {
            var journal = new CommandJournal(Identity, new[] { "a" }, 1);
            var target = new PickupTarget();
            var conflicts = journal.ExecuteBatch(1, new[] {
                Request("a", epoch: 1), Request("a", epoch: 1, target: "other") }, target);
            Assert.IsTrue(conflicts.All(r => r.Effect.Code == "PayloadConflict"));
            Assert.AreEqual(0, journal.Capture().Frontiers["a"]);
            var results = journal.ExecuteBatch(2, new[] {
                Request("a", epoch: 0, target: "other"), Request("a", epoch: 1) }, target);
            Assert.IsTrue(results.Any(r => r.Effect.Code == "Collected"));
            Assert.AreEqual(1, journal.Capture().Frontiers["a"]);
        }

        [Test]
        public void WholeBatchRollsBackStateAndReceiptsWhenAnApplicationFails()
        {
            var journal = new CommandJournal(Identity, new[] { "a", "b" });
            var target = new PickupTarget { Fail = true };
            Assert.Throws<InvalidOperationException>(() => journal.ExecuteBatch(1,
                new[] { Request("a"), Request("b", target: "throws") }, target));
            Assert.AreEqual(0, target.Winners.Count);
            Assert.AreEqual(0, journal.Capture().Frontiers["a"]);
            Assert.IsEmpty(journal.Capture().Receipts);
            Assert.AreEqual("Collected", journal.ExecuteBatch(1, new[] { Request("a") }, target)[0].Effect.Code);
        }

        [Test]
        public void RetiredSequencesNeverApplyAgainAndUnknownActorsAndGapsDoNotAdvance()
        {
            var journal = new CommandJournal(Identity, new[] { "a" });
            var target = new PickupTarget();
            Assert.AreEqual("UnknownActor", journal.ExecuteBatch(1, new[] { Request("stranger") }, target)[0].Effect.Code);
            Assert.AreEqual("SequenceGap", journal.ExecuteBatch(1, new[] { Request("a", 2) }, target)[0].Effect.Code);
            for (int i = 1; i <= 150; i++) journal.ExecuteBatch(i, new[] { Request("a", i) }, target);
            var snapshot = journal.Capture();
            Assert.AreEqual(128, snapshot.Receipts.Count);
            var recovered = new CommandJournal(Identity, new[] { "a" });
            recovered.Restore(snapshot);
            Assert.AreEqual("RetiredSequence", recovered.ExecuteBatch(151, new[] { Request("a") }, target)[0].Effect.Code);
            Assert.AreEqual(1, target.Winners.Count);
            Assert.AreEqual(150, recovered.Capture().Frontiers["a"]);
        }

        [Test]
        public void ForeignJournalRestoreRejectsWithoutChangingLiveFrontier()
        {
            var journal = new CommandJournal(Identity, new[] { "a" });
            var target = new PickupTarget();
            journal.ExecuteBatch(1, new[] { Request("a") }, target);
            var other = new CommandJournal(new string('b', 64), new[] { "a" });
            Assert.Throws<ArgumentException>(() => journal.Restore(other.Capture()));
            Assert.AreEqual(1, journal.Capture().Frontiers["a"]);
        }

        [Test]
        public void AcknowledgementMustMatchEveryPublishedIdentityField()
        {
            var tracker = new SnapshotCommitTracker("match", Identity);
            var stamp = tracker.Publish(60, new string('b', 64));
            Assert.IsFalse(tracker.Acknowledge(new SnapshotStamp("other", Identity, 0, 1, 60, stamp.StateHash)));
            Assert.IsFalse(tracker.Acknowledge(new SnapshotStamp("match", new string('c', 64), 0, 1, 60, stamp.StateHash)));
            Assert.IsFalse(tracker.Acknowledge(new SnapshotStamp("match", Identity, 1, 1, 60, stamp.StateHash)));
            Assert.IsFalse(tracker.Acknowledge(new SnapshotStamp("match", Identity, 0, 2, 60, stamp.StateHash)));
            Assert.IsFalse(tracker.Acknowledge(new SnapshotStamp("match", Identity, 0, 1, 61, stamp.StateHash)));
            Assert.IsFalse(tracker.Acknowledge(new SnapshotStamp("match", Identity, 0, 1, 60, new string('c', 64))));
            Assert.IsFalse(tracker.Confirms(60));
            Assert.IsTrue(tracker.Acknowledge(stamp));
            Assert.IsTrue(tracker.Confirms(60));
            Assert.IsFalse(tracker.Acknowledge(stamp));
            Assert.IsFalse(tracker.Confirms(61));
        }

        [Test]
        public void TwoSecondWindowPausesUntilDurableAckAndDoesNotResetDuringMigration()
        {
            var tracker = new SnapshotCommitTracker("match", Identity);
            var stamp = tracker.Publish(120, new string('b', 64));
            Assert.IsTrue(tracker.CanAdvance(120));
            Assert.IsFalse(tracker.CanAdvance(121));
            Assert.Throws<InvalidOperationException>(() => tracker.Publish(121, stamp.StateHash));
            tracker.Acknowledge(stamp);
            Assert.IsTrue(tracker.CanAdvance(240));
            Assert.IsFalse(tracker.CanAdvance(241));
            var resumed = new SnapshotCommitTracker("match", Identity, 1, tracker.AcknowledgedTick, tracker.AcknowledgedSequence);
            Assert.AreEqual(120, resumed.AcknowledgedTick);
            Assert.IsFalse(resumed.CanAdvance(241));
            Assert.IsFalse(resumed.Acknowledge(stamp));
        }

        [Test]
        public void NewerAckRetiresOlderPendingSnapshotsWithoutMovingBackwards()
        {
            var tracker = new SnapshotCommitTracker("match", Identity);
            var first = tracker.Publish(10, new string('b', 64));
            var second = tracker.Publish(20, new string('c', 64));
            Assert.IsTrue(tracker.Acknowledge(second));
            Assert.AreEqual(0, tracker.PendingCount);
            Assert.IsFalse(tracker.Acknowledge(first));
            Assert.AreEqual(20, tracker.AcknowledgedTick);
        }
    }
}
