namespace Wreckabulary.Rules
{
    public enum HelperPhase { Arming, Idle, Chase, WindUp, Strike, Cooldown, Done }

    public enum HelperOrder { Hold, Approach, FaceOnly, Release, ReturnHome, Vanish, SideStep }

    public enum HelperEnd { None, Life, Strikes, Smashed, MakerOut, RoundOver, Stuck, Lost }

    public struct HelperState
    {
        public HelperPhase Phase;
        public float PhaseLeft, Life, Health, LostSight, Stuck;
        public int Target, StrikesLeft, LockedId;
        public int Provoker, StuckCount;
        public float ProvokeLeft;
        public HelperEnd Ended;
    }

    public readonly struct HelperSense
    {
        public readonly float TargetDistance, FromHome;
        public readonly bool TargetInSight, MakerOut, RoundOver, Blocked, Lost;

        public HelperSense(float targetDistance, float fromHome, bool targetInSight, bool makerOut, bool roundOver, bool blocked = false, bool lost = false)
        {
            TargetDistance = targetDistance; FromHome = fromHome; TargetInSight = targetInSight; MakerOut = makerOut; RoundOver = roundOver;
            Blocked = blocked; Lost = lost;
        }
    }

    public static class HelperBrain
    {
        public const float ArmSeconds = .6f, LoseSightSeconds = 1.5f, StuckSeconds = 1.2f;
        public const float AtHome = .5f;
        public const int SideStepsBeforeHome = 1, StucksBeforeEnd = 3;

        public static HelperState Start(HelperSpec spec, int lockedId) => new HelperState
        {
            Phase = HelperPhase.Arming, PhaseLeft = ArmSeconds, Life = spec.Seconds, Health = spec.Health,
            StrikesLeft = spec.Strikes, Target = -1, LockedId = lockedId, Provoker = -1,
        };

        public static void Provoke(ref HelperState s, HelperSpec spec, int makerId, int makerTeam, int attackerId, int attackerTeam)
        {
            if (!HelperTargeting.Provokes(spec, makerId, makerTeam, attackerId, attackerTeam)) return;
            s.Provoker = attackerId;
            s.ProvokeLeft = HelperTargeting.ProvokeSeconds;
        }

        public static bool Hurt(ref HelperState s, float damage)
        {
            if (s.Phase == HelperPhase.Done || !(damage > 0f)) return false;
            s.Health -= damage;
            return true;
        }

        public static HelperOrder Step(ref HelperState s, HelperSpec spec, in HelperSense sense, int target, float dt)
        {
            if (s.Phase == HelperPhase.Done) return HelperOrder.Vanish;
            s.Life -= dt;
            HelperEnd end = sense.RoundOver ? HelperEnd.RoundOver : sense.MakerOut ? HelperEnd.MakerOut : sense.Lost ? HelperEnd.Lost
                : s.Health <= 0f ? HelperEnd.Smashed : s.StrikesLeft <= 0 ? HelperEnd.Strikes : s.Life <= 0f ? HelperEnd.Life : HelperEnd.None;
            if (end != HelperEnd.None) return Stop(ref s, end);
            if (s.ProvokeLeft > 0f)
            {
                s.ProvokeLeft -= dt;
                if (s.ProvokeLeft <= 0f) { s.ProvokeLeft = 0f; s.Provoker = -1; }
            }
            s.PhaseLeft -= dt;
            if (s.Phase == HelperPhase.Arming) { if (s.PhaseLeft > 0f) return HelperOrder.Hold; s.Phase = HelperPhase.Idle; }
            if (s.Phase == HelperPhase.WindUp)
            {
                if (s.PhaseLeft > 0f) return HelperOrder.FaceOnly;
                s.StrikesLeft--;
                s.Phase = HelperPhase.Strike;
                s.PhaseLeft = spec.Cooldown;
                if (spec.Attack == HelperAttack.Burst) { s.Phase = HelperPhase.Done; s.Ended = HelperEnd.Strikes; }
                return HelperOrder.Release;
            }
            if (s.Phase == HelperPhase.Strike) s.Phase = HelperPhase.Cooldown;
            if (s.Phase == HelperPhase.Cooldown && s.PhaseLeft > 0f) return HelperOrder.Hold;
            s.Target = target;
            if (target < 0 || !spec.Hurts)
            {
                s.Phase = HelperPhase.Idle;
                s.LostSight = 0f;
                return spec.Moves && sense.FromHome > AtHome ? Move(ref s, sense, HelperOrder.ReturnHome, dt) : HelperOrder.Hold;
            }
            s.LostSight = sense.TargetInSight ? 0f : s.LostSight + dt;
            if (s.LostSight > LoseSightSeconds)
            {
                s.Target = -1; s.LockedId = -1; s.LostSight = 0f; s.Phase = HelperPhase.Idle;
                return HelperOrder.Hold;
            }
            if (sense.TargetDistance <= spec.Range && sense.TargetInSight)
            {
                s.Phase = HelperPhase.WindUp;
                s.PhaseLeft = spec.Windup;
                s.Stuck = 0f;
                return HelperOrder.FaceOnly;
            }
            if (!spec.Moves) { s.Phase = HelperPhase.Idle; return HelperOrder.FaceOnly; }
            if (sense.FromHome >= spec.Leash) { s.Phase = HelperPhase.Idle; s.Stuck = 0f; return HelperOrder.FaceOnly; }
            s.Phase = HelperPhase.Chase;
            return Move(ref s, sense, HelperOrder.Approach, dt);
        }

        static HelperOrder Move(ref HelperState s, in HelperSense sense, HelperOrder order, float dt)
        {
            if (!sense.Blocked) { s.Stuck = 0f; return order; }
            s.Stuck += dt;
            if (s.Stuck < StuckSeconds) return order;
            s.Stuck = 0f;
            s.StuckCount++;
            if (s.StuckCount >= StucksBeforeEnd) return Stop(ref s, HelperEnd.Stuck);
            if (s.StuckCount <= SideStepsBeforeHome) return HelperOrder.SideStep;
            s.Phase = HelperPhase.Idle;
            return HelperOrder.ReturnHome;
        }

        public static HelperOrder Stop(ref HelperState s, HelperEnd end)
        {
            if (s.Ended == HelperEnd.None) s.Ended = end;
            s.Phase = HelperPhase.Done;
            return HelperOrder.Vanish;
        }
    }
}
