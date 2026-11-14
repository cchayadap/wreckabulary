using System;

namespace Wreckabulary.Online
{
    public sealed class WordboundRecoveryWindow
    {
        public const long MaximumMilliseconds = 120000;
        public bool Active { get; private set; }
        public bool Cancelled { get; private set; }
        public long Deadline { get; private set; }
        public int Attempts { get; private set; }
        long latestMigrationGeneration = -1;

        public bool Begin(long now)
        {
            if (now < 0 || now > long.MaxValue - MaximumMilliseconds) throw new ArgumentOutOfRangeException(nameof(now));
            if (Cancelled) return false;
            if (Active) return false;
            Active = true; Deadline = now + MaximumMilliseconds; Attempts = 0;
            return true;
        }
        public void Narrow(long originalDeadline, long now)
        {
            if (!Active || originalDeadline < 0) throw new InvalidOperationException("Recovery has no original seat deadline.");
            Deadline = Math.Min(Deadline, originalDeadline);
            RequireTime(now);
        }
        public int NextAttempt(long now)
        {
            RequireTime(now);
            Attempts = checked(Attempts + 1);
            return Math.Min(2000, 250 << Math.Min(Attempts - 1, 3));
        }
        public void RequireTime(long now)
        {
            if (!Active || Cancelled || now < 0 || now >= Deadline)
                throw new TimeoutException("The original reconnect window ended.");
        }
        public bool AcceptMigration(long generation)
        {
            if (!Active || Cancelled || generation < 0 || generation < latestMigrationGeneration) return false;
            latestMigrationGeneration = generation;
            return true;
        }
        public void Complete() { Active = false; Deadline = 0; Attempts = 0; latestMigrationGeneration = -1; }
        public void Cancel() { Cancelled = true; Active = false; }
    }
}
