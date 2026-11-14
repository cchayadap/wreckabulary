using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class SnapshotStamp
    {
        public string MatchId { get; }
        public string IdentityHash { get; }
        public long Epoch { get; }
        public long Sequence { get; }
        public long Tick { get; }
        public string StateHash { get; }
        public SnapshotStamp(string matchId, string identityHash, long epoch, long sequence, long tick, string stateHash)
        {
            if (!WordboundIdentity.IsId(matchId) || !WordboundIdentity.IsHash(identityHash) ||
                !WordboundIdentity.IsHash(stateHash) || epoch < 0 || sequence < 1 || tick < 0)
                throw new ArgumentException("Snapshot stamp is invalid.");
            MatchId = matchId; IdentityHash = identityHash; Epoch = epoch;
            Sequence = sequence; Tick = tick; StateHash = stateHash;
        }
    }

    public sealed class SnapshotCommitTracker
    {
        readonly string matchId;
        readonly string identityHash;
        readonly SortedDictionary<long, SnapshotStamp> pending = new SortedDictionary<long, SnapshotStamp>();
        public long Epoch { get; }
        public long AcknowledgedTick { get; private set; }
        public long AcknowledgedSequence { get; private set; }
        public long PublishedSequence { get; private set; }
        public long PublishedTick { get; private set; }
        public int PendingCount => pending.Count;

        public SnapshotCommitTracker(string matchId, string identityHash, long epoch = 0,
            long restoredCommittedTick = 0, long restoredSequence = 0)
        {
            if (!WordboundIdentity.IsId(matchId) || !WordboundIdentity.IsHash(identityHash) ||
                epoch < 0 || restoredCommittedTick < 0 || restoredSequence < 0)
                throw new ArgumentException("Invalid snapshot commitment identity.");
            this.matchId = matchId; this.identityHash = identityHash; Epoch = epoch;
            AcknowledgedTick = PublishedTick = restoredCommittedTick;
            AcknowledgedSequence = PublishedSequence = restoredSequence;
        }

        public bool CanAdvance(long nextTick) => nextTick >= AcknowledgedTick &&
            nextTick - AcknowledgedTick <= ModeDefinition.ProvisionalWindowSeconds * ModeDefinition.TickRate;

        public SnapshotStamp Publish(long tick, string completeStateHash)
        {
            if (tick < PublishedTick || !CanAdvance(tick) || pending.Count >= 256 || PublishedSequence == long.MaxValue)
                throw new InvalidOperationException("Snapshot publication exceeds its monotonic or provisional bounds.");
            var stamp = new SnapshotStamp(matchId, identityHash, Epoch, PublishedSequence + 1, tick, completeStateHash);
            pending.Add(stamp.Sequence, stamp);
            PublishedSequence = stamp.Sequence; PublishedTick = tick;
            return stamp;
        }

        public bool Acknowledge(SnapshotStamp durableAcknowledgement)
        {
            var ack = durableAcknowledgement;
            if (ack == null || ack.MatchId != matchId || ack.IdentityHash != identityHash || ack.Epoch != Epoch ||
                ack.Sequence <= AcknowledgedSequence || !pending.TryGetValue(ack.Sequence, out var expected) ||
                expected.StateHash != ack.StateHash || expected.Tick != ack.Tick) return false;
            AcknowledgedTick = ack.Tick; AcknowledgedSequence = ack.Sequence;
            foreach (long sequence in pending.Keys.Where(sequence => sequence <= ack.Sequence).ToArray()) pending.Remove(sequence);
            return true;
        }

        public bool Confirms(long resultTick) => resultTick >= 0 && AcknowledgedSequence > 0 && resultTick <= AcknowledgedTick;
    }
}
