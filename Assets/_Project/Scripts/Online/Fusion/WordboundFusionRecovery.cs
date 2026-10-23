using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundFusionSession
    {
        sealed class WordboundRunnerStartException : Exception
        {
            public ShutdownReason Reason { get; }
            public WordboundRunnerStartException(ShutdownReason reason) : base("Fusion startup failed: " + reason) { Reason = reason; }
        }
        readonly WordboundRecoveryWindow recovery = new WordboundRecoveryWindow();
        readonly HashSet<NetworkRunner> migrationSources = new HashSet<NetworkRunner>();
        CancellationTokenSource recoveryCancellation, recoveryAttempt;
        Task recoveryTask;
        NetworkRunner recoverySource, retiringRunner;
        HostMigrationToken pendingMigration;
        string localPlayerId;
        bool gameplayObserved, recoveryRetry;
        public bool Recovering => recovery.Active;
        public long RecoveryDeadlineMilliseconds => recovery.Deadline;
        public event Action<string> RecoveryStatusChanged;

        bool OwnsRecoveryCallback(NetworkRunner runner) => runner != null && !lifetime.IsCancellationRequested &&
            !recovery.Cancelled && Failure == "" &&
            (IsCurrent(activeRun) && runner == activeRun.Runner || recovery.Active && runner == recoverySource);
        static bool TransientShutdown(ShutdownReason reason) => reason == ShutdownReason.ConnectionTimeout ||
            reason == ShutdownReason.PhotonCloudTimeout || reason == ShutdownReason.OperationTimeout ||
            reason == ShutdownReason.GameNotFound;
        static bool TransientRecoveryError(Exception error) => error is IOException || error is HttpRequestException ||
            error is TimeoutException || error is OperationCanceledException ||
            error is OnlineServiceException service && (service.Status == 429 || service.Status >= 500) ||
            error is WordboundRunnerStartException start && TransientShutdown(start.Reason);

        void RecoverTransport(NetworkRunner runner, HostMigrationToken token)
        {
            if (!OwnsRecoveryCallback(runner) || (!gameplayObserved && token == null)) return;
            if (token != null)
            {
                if (!migrationSources.Add(runner)) return;
                pendingMigration = token;
            }
            bool beginning = recovery.Begin(clock());
            if (beginning)
            {
                recoverySource = runner;
                recoveryCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                recoveryCancellation.CancelAfter(TimeSpan.FromMilliseconds(WordboundRecoveryWindow.MaximumMilliseconds));
            }
            else if (runner != recoverySource) recoverySource = runner;
            FreezeRecovery();
            if (!beginning) return;
            RecoveryStatusChanged?.Invoke("Connection interrupted. Reconnecting to your match…");
            recoveryTask = RecoverLoop(recoveryCancellation);
        }
        void FreezeRecovery()
        {
            InvalidateRun();
            recoveryRetry = true;
            recoveryAttempt?.Cancel();
            migrating = true; restored = false; localAdmitted = false; localReady = false;
            GameplayReady = false; gate?.Freeze(); authority.PauseForMigration();
            authority.SetTransportReady(false, Runner != null && Runner.IsServer);
            beginReceived = false; committedMark = null; committedPacket = null;
            receivedMark = null; receivedChunks = null;
            pending.Clear(); rates.Clear(); sentMarks.Clear(); leases.Clear();
        }
        async Task RetireRecoveryRunner(NetworkRunner runner, CancellationToken ct)
        {
            if (runner == null) return;
            retiringRunner = runner;
            try
            {
                await runner.Shutdown(false, ShutdownReason.HostMigration);
                if (ReferenceEquals(recoverySource, runner)) recoverySource = null;
                if (ReferenceEquals(Runner, runner)) Runner = null;
                UnityEngine.Object.Destroy(runner.gameObject);
            }
            finally { retiringRunner = null; }
            ct.ThrowIfCancellationRequested();
        }
        async Task RecoverLoop(CancellationTokenSource owner)
        {
            var ct = owner.Token;
            try
            {
                await Task.Delay(500, ct);
                while (recovery.Active && !ct.IsCancellationRequested)
                {
                    recovery.RequireTime(clock());
                    await RetireRecoveryRunner(Runner, ct);
                    recovery.RequireTime(clock());
                    int backoff = recovery.NextAttempt(clock());
                    HostMigrationToken migration = null;
                    try
                    {
                        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        recoveryAttempt = attempt;
                        recoveryRetry = false;
                        var seats = await connections.Renew(gate.Match, Array.Empty<OnlineConnectionLease>(), attempt.Token);
                        attempt.Token.ThrowIfCancellationRequested();
                        if (connections.LocalPlayerId != localPlayerId) throw new InvalidOperationException("The original match identity changed during recovery.");
                        var seat = seats.Single(value => value.PlayerId == localPlayerId);
                        if (seat.Returned) throw new InvalidOperationException("The original match seat was returned.");
                        recovery.Narrow(seat.ReconnectDeadline, clock());
                        owner.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(0, recovery.Deadline - clock())));
                        migration = pendingMigration;
                        pendingMigration = null;
                        await StartRunner(migration, attempt.Token, true);
                        while (!GameplayReady && !recoveryRetry && IsCurrent(activeRun) && Failure == "")
                        {
                            recovery.RequireTime(clock());
                            await Task.Delay(50, ct);
                        }
                        if (GameplayReady && !recoveryRetry && IsCurrent(activeRun))
                        {
                            recovery.Complete(); migrationSources.Clear(); recoverySource = null;
                            owner.CancelAfter(Timeout.Infinite);
                            RecoveryStatusChanged?.Invoke("");
                            return;
                        }
                        if (Failure != "") return;
                    }
                    catch (Exception error) when (TransientRecoveryError(error) && !ct.IsCancellationRequested &&
                        (migration == null || migration.GameMode != GameMode.Host)) { }
                    finally { recoveryAttempt = null; }
                    FreezeRecovery();
                    await Task.Delay(backoff, ct);
                }
                ct.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested || recovery.Cancelled) { }
            catch (Exception error)
            {
                recovery.Cancel();
                Fail(error is OperationCanceledException ? new TimeoutException("The original reconnect window ended.") : error);
            }
            finally
            {
                if (ReferenceEquals(recoveryCancellation, owner))
                {
                    recoveryAttempt = null;
                    recoveryCancellation = null;
                }
                owner.Dispose();
            }
        }
        void CancelRecovery()
        {
            recovery.Cancel();
            recoveryCancellation?.Cancel();
            recoveryAttempt?.Cancel();
        }
    }
}
