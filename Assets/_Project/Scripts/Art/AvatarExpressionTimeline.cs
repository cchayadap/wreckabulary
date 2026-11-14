using UnityEngine;

namespace Wreckabulary.Art
{
    public struct AvatarExpressionSample
    {
        public long Epoch, Tick;
        public float Health;
        public int RecoveryGeneration;
        public bool Available, Downed, Hurt, Focus, Falling, GroundedKnown, Grounded, Moving, Equipped, Celebrate;
    }

    public sealed class AvatarExpressionTimeline
    {
        AvatarExpressionSample sample;
        bool hasSample;
        long epoch = -1, tick = -1;
        float hurtLeft, recoverLeft, celebrateLeft;
        public bool Available => hasSample && sample.Available;
        public AvatarExpression State => sample.Downed ? AvatarExpression.Downed : sample.Hurt || hurtLeft > 0 ? AvatarExpression.Hurt :
            sample.Falling ? AvatarExpression.Falling : sample.Focus ? AvatarExpression.Focus : recoverLeft > 0 ? AvatarExpression.Recover :
            celebrateLeft > 0 ? AvatarExpression.Celebrate : sample.Moving ? AvatarExpression.Moving :
            sample.Equipped ? AvatarExpression.Equipped : AvatarExpression.Idle;

        public bool Observe(AvatarExpressionSample next, AvatarExpressionProfile profile = null)
        {
            if (next.Epoch < epoch || next.Epoch == epoch && next.Tick < tick) return false;
            if (next.Epoch != epoch) Reset();
            epoch = next.Epoch; tick = next.Tick;
            if (!next.Available || float.IsNaN(next.Health) || float.IsInfinity(next.Health))
            { ClearPresentation(); return true; }
            if (hasSample && next.Health < sample.Health)
                hurtLeft = AvatarExpressionProfile.Bounded(profile ? profile.HurtSeconds : .32f, .1f, 1, .32f);
            bool recovered = hasSample && (sample.Downed && !next.Downed || next.RecoveryGeneration != sample.RecoveryGeneration ||
                sample.Falling && next.GroundedKnown && next.Grounded && !next.Falling);
            if (recovered) recoverLeft = AvatarExpressionProfile.Bounded(profile ? profile.RecoverSeconds : .55f, .1f, 1.5f, .55f);
            if (next.Celebrate && (!hasSample || !sample.Celebrate))
                celebrateLeft = AvatarExpressionProfile.Bounded(profile ? profile.CelebrateSeconds : 1.5f, .2f, 3, 1.5f);
            sample = next; hasSample = true; return true;
        }

        public void Advance(float seconds)
        {
            float dt = AvatarExpressionProfile.Bounded(seconds, 0, 1, 0);
            hurtLeft = Mathf.Max(0, hurtLeft - dt); recoverLeft = Mathf.Max(0, recoverLeft - dt);
            celebrateLeft = Mathf.Max(0, celebrateLeft - dt);
        }

        void ClearPresentation() { sample = default; hasSample = false; hurtLeft = recoverLeft = celebrateLeft = 0; }
        public void Reset() { ClearPresentation(); epoch = tick = -1; }
    }
}
