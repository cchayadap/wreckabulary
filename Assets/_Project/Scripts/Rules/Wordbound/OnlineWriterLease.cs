using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class OnlineWriterLease
    {
        public sealed class Observation
        {
            public readonly bool InFlight, Completed;
            public readonly long DueAt, Attempts, StartedAt, CompletedAt, ErrorAt;
            public readonly Exception Error;
            internal Observation(bool inFlight, bool completed, long dueAt, long attempts, long startedAt,
                long completedAt, long errorAt, Exception error)
            {
                InFlight = inFlight; Completed = completed; DueAt = dueAt; Attempts = attempts;
                StartedAt = startedAt; CompletedAt = completedAt; ErrorAt = errorAt; Error = error;
            }
        }

        readonly object sync = new object();
        readonly Func<long> clock;
        readonly CancellationToken cancellation;
        OnlineCheckpointWriter current;
        bool retired, inFlight, completed;
        long dueAt, attempts, startedAt, completedAt, errorAt;
        Exception error;

        public OnlineWriterLease(OnlineCheckpointWriter writer, Func<long> clock, CancellationToken cancellation)
        {
            current = writer ?? throw new ArgumentNullException(nameof(writer));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.cancellation = cancellation;
            RequireLive();
            dueAt = checked(clock() + 5000);
        }

        public OnlineCheckpointWriter Current { get { lock (sync) return current; } }
        public bool IsLiveFor(string playerId)
        {
            lock (sync) return !retired && !cancellation.IsCancellationRequested &&
                current.PlayerId == playerId && clock() < current.ExpiresAtMilliseconds;
        }
        public bool RenewalDue
        {
            get
            {
                lock (sync)
                {
                    long now = clock();
                    return !retired && !cancellation.IsCancellationRequested && !inFlight &&
                        now < current.ExpiresAtMilliseconds && now >= dueAt;
                }
            }
        }
        public Observation Observe()
        {
            lock (sync) return new Observation(inFlight, completed, dueAt, attempts, startedAt, completedAt, errorAt, error);
        }
        public void Retire() { lock (sync) retired = true; }

        void RequireLive()
        {
            if (retired || cancellation.IsCancellationRequested) throw new OperationCanceledException(cancellation);
            if (clock() >= current.ExpiresAtMilliseconds) throw new TimeoutException("Canonical writer lease expired.");
        }

        static bool SameFence(OnlineCheckpointWriter left, OnlineCheckpointWriter right) =>
            right != null && left.PlayerId == right.PlayerId && left.ClaimId == right.ClaimId &&
            left.Term == right.Term && left.Epoch == right.Epoch && left.BaseHeadId == right.BaseHeadId;

        void Merge(OnlineCheckpointWriter request, OnlineCheckpointWriter response)
        {
            RequireLive();
            if (!SameFence(current, request) || !SameFence(request, response) ||
                response.ExpiresAtMilliseconds < request.ExpiresAtMilliseconds)
                throw new FormatException("Writer renewal changed its authority fence.");
            current = new OnlineCheckpointWriter(response, Math.Max(current.ExpiresAtMilliseconds, response.ExpiresAtMilliseconds),
                current.AdmissionOpen || response.AdmissionOpen);
        }

        public void OpenAdmission(OnlineCheckpointWriter request, OnlineCheckpointWriter response)
        {
            lock (sync)
            {
                if (response == null || !response.AdmissionOpen) throw new FormatException("Coordinator did not open writer admission.");
                Merge(request, response);
            }
        }

        public async Task<bool> Renew(Func<OnlineCheckpointWriter, CancellationToken, Task<OnlineCheckpointState>> renew)
        {
            if (renew == null) throw new ArgumentNullException(nameof(renew));
            OnlineCheckpointWriter request;
            lock (sync)
            {
                if (inFlight) return false;
                long now = clock();
                if (retired || cancellation.IsCancellationRequested || now >= current.ExpiresAtMilliseconds)
                {
                    error = retired || cancellation.IsCancellationRequested ? (Exception)new OperationCanceledException(cancellation) :
                        new TimeoutException("Canonical writer lease expired.");
                    errorAt = now; completedAt = now; completed = true;
                    return false;
                }
                dueAt = checked(now + 5000); inFlight = true; startedAt = now; attempts++;
                request = current;
            }
            try
            {
                var response = await renew(request, cancellation).ConfigureAwait(false);
                lock (sync) Merge(request, response?.Writer);
                return true;
            }
            catch (Exception failed)
            {
                lock (sync) { error = failed; errorAt = clock(); }
                return false;
            }
            finally
            {
                lock (sync) { completedAt = clock(); completed = true; inFlight = false; }
            }
        }
    }
}
