using System;
using System.Linq;
using System.Text;
using UnityEngine;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.OnlineGameplayTests
{
    public sealed partial class WordboundNativeGameplayTests
    {
        sealed class ContactObservation
        {
            public Peer Host, Victim;
            public Func<WorldVector> Target;
            public string Operation;
            public float Radius;
            public int Ordinal, PeriodicSamples;
            public double Started, NextSample;
            public float SampledMinimum = float.PositiveInfinity;
        }

        ContactObservation contactObservation;
        int contactWalkOrdinal;

        void BeginContactObservation(Peer host, Peer victim, Func<WorldVector> target, float radius, string operation)
        {
            contactObservation = new ContactObservation {
                Host = host, Victim = victim, Target = target, Radius = radius,
                Operation = operation, Ordinal = ++contactWalkOrdinal
            };
        }

        void SampleContactObservation(string operation, double now, double maximumGap)
        {
            var sample = contactObservation;
            if (sample == null || sample.Operation != operation || sample.PeriodicSamples >= 7 || now < sample.NextSample) return;
            sample.PeriodicSamples++;
            sample.NextSample = now + 5;
            EmitContactObservation(sample, "sample", maximumGap);
        }

        void ObserveContactOperation(string operation, string phase, double started, double maximumGap)
        {
            var sample = contactObservation;
            if (sample == null || sample.Operation != operation) return;
            if (phase == "begin")
            {
                sample.Started = started;
                sample.NextSample = started + 5;
            }
            try { EmitContactObservation(sample, phase, maximumGap); }
            finally { if (phase == "end") contactObservation = null; }
        }

        void EmitContactObservation(ContactObservation sample, string phase, double maximumGap)
        {
            try
            {
                var host = sample.Host;
                var controller = host.Controller;
                var runner = host.Runner;
                var frame = controller.Frame;
                var goal = sample.Target();
                var actor = frame.Actors.Single(value => value.PlayerId == sample.Victim.Actor);
                var progress = frame.Progress.Players.Single(value => value.Id == sample.Victim.Actor);
                var canonical = frame.Bodies.Single(value => value.Kind == WorldBodyKind.Player && value.SourceId == sample.Victim.Actor);
                var mark = host.Session.Committed;
                bool sameEpoch = mark != null && mark.Epoch == frame.Epoch;
                bool canMove = controller.PhysicsWorld.CanMove(sample.Victim.Actor, out float moveScale);
                var text = new StringBuilder(FormattableString.Invariant(
                    $"WORDBOUND_FFA_CONTACT: walk={sample.Ordinal}; phase={phase}; elapsed={Time.realtimeSinceStartupAsDouble - sample.Started:F3}; periodicSamples={sample.PeriodicSamples}; maxFrameGap={maximumGap:F3}; lifetimeCancelled={lifetime.IsCancellationRequested}; "));
                text.Append(FormattableString.Invariant(
                    $"canonicalTick={frame.Progress.Tick}; epoch={frame.Epoch}; sdkTick={runner.Tick.Raw}; hostIsServer={runner.IsServer}; target={ContactVector(goal)}; radius={sample.Radius:F6}; "));
                text.Append(FormattableString.Invariant(
                    $"canonicalPosition={ContactVector(canonical.Position)}; canonicalVelocity={ContactVector(canonical.Velocity)}; canonicalDistance={HorizontalDistance(canonical.Position, goal):F6}; canonicalActive={canonical.Active}; canonicalKinematic={canonical.Kinematic}; canonicalProtected={canonical.Protected}; "));
                text.Append(FormattableString.Invariant(
                    $"hostCanMove={canMove}; moveScale={moveScale:F6}; health={actor.Health:F6}; stunnedUntilTick={actor.StunnedUntilTick}; lastInputSequence={actor.LastInputSequence}; connected={progress.Connected}; returned={progress.Returned}; eliminated={progress.Eliminated}; protectedUntilTick={progress.ProtectedUntil}; protectedNow={progress.ProtectedUntil > frame.Progress.Tick}; respawnAtTick={progress.RespawnAt}; recoveryGeneration={progress.RecoveryGeneration}; simulationPaused={controller.IsSimulationPaused}; "));
                text.Append("committedSameEpoch=").Append(sameEpoch).Append("; canonicalCommittedGapTicks=")
                    .Append(sameEpoch ? (frame.Progress.Tick - mark.Tick).ToString(System.Globalization.CultureInfo.InvariantCulture) : "unavailable").Append("; ");
                try
                {
                    var body = controller.Bodies["player:" + WordboundGameplayPlan.Seat(frame.Binding, sample.Victim.Actor)];
                    bool currentHostBody = runner.IsServer && body != null && body.Runner == runner && body.Object != null && body.Object.IsValid;
                    text.Append("nativeCurrentHostBody=").Append(currentHostBody).Append("; ");
                    if (currentHostBody)
                    {
                        var rigidbody = body.Rigidbody;
                        var position = rigidbody.position;
                        var velocity = rigidbody.linearVelocity;
                        var nativePosition = new WorldVector(position.x, position.y, position.z);
                        float distance = HorizontalDistance(nativePosition, goal);
                        sample.SampledMinimum = Mathf.Min(sample.SampledMinimum, distance);
                        text.Append(FormattableString.Invariant(
                            $"hostNativePosition={position.x:F6}/{position.y:F6}/{position.z:F6}; hostNativeVelocity={velocity.x:F6}/{velocity.y:F6}/{velocity.z:F6}; hostNativeDistance={distance:F6}; sampledMinimumDistance={sample.SampledMinimum:F6}; canonicalNativeDistance={HorizontalDistance(canonical.Position, nativePosition):F6}; nativeKinematic={rigidbody.isKinematic}; nativeSleeping={rigidbody.IsSleeping()}; nativeActive={body.gameObject.activeInHierarchy}; "));
                    }
                    else text.Append("nativeFactsUnavailable=not-current-host-body; ");
                }
                catch (Exception error) { text.Append("nativeFactsUnavailable=").Append(error.GetType().Name).Append("; "); }
                text.Append(host.Checkpoints?.Facts(controller, host.Session) ?? "checkpointFactsUnavailable=missing-probe");
                Debug.Log(text.ToString());
            }
            catch (Exception error)
            {
                Debug.Log($"WORDBOUND_FFA_CONTACT_UNAVAILABLE: walk={sample.Ordinal}; phase={phase}; reason={error.GetType().Name}");
            }
        }

        static string ContactVector(WorldVector value) => FormattableString.Invariant($"{value.X:F6}/{value.Y:F6}/{value.Z:F6}");
    }
}
