// Game Studio Kit (MIT). Idle foot lock state machine shared by IdleFootLock (Humanoid, OnAnimatorIK) and
// IdleFootLockGeneric (any rig, analytic two-bone IK). Written clean-room from the kit's behaviour specification
// (ARCHITECTURE §3.8 foot-lock.md; thresholds in §3.12):
//  - while the body is idle, each planted foot is pinned where it stood, so idle clips cannot make feet slide;
//  - a pinned foot that the animation moves further than the replant distance is carried smoothly to its new spot;
//  - feet the clip lifts (weight shifts) are not pinned;
//  - motion or a move intent releases the pins quickly (target: latency ≤ 0.15 s, release pop ≤ 1.5 cm × k);
//  - every distance scales with k = body height / 1.8 m.
// The self-test criteria are measured by IdleDriftProbe through IFootLockMetrics.
using System;
using UnityEngine;

namespace GameStudioKit.Locomotion
{
    /// <summary>Numbers the idle-drift probe reads from a foot lock.</summary>
    public interface IFootLockMetrics
    {
        bool IsLockActive { get; }

        /// <summary>Fraction of idle time with every eligible foot fully pinned (target ≥ 0.95).</summary>
        float LockedFraction { get; }

        /// <summary>Worst time from motion onset (or move intent) until the pins are fully released.</summary>
        float MaxReleaseLatencySeconds { get; }
        int ReleaseCount { get; }

        /// <summary>Largest single-frame change of the lock's foot offset while releasing, in cm.</summary>
        float MaxReleaseStepCm { get; }

        /// <summary>Largest single-frame change of the lock's foot offset while idle (replants), in cm.</summary>
        float MaxReplantJumpCm { get; }

        /// <summary>Average CPU time per frame spent in the lock, in ms.</summary>
        float AverageCpuMs { get; }

        void ResetMetrics();
    }

    [Serializable]
    public sealed class FootLockSettings
    {
        [Tooltip("Planar speed (m/s) below which the body counts as idle.")]
        public float idleSpeed = 0.1f;
        [Tooltip("Planar speed (m/s) above which the pins release (raw speed, no smoothing).")]
        public float moveSpeed = 0.15f;
        [Tooltip("Seconds the body must stay idle before pins engage.")]
        public float engageDelay = 0.12f;
        [Tooltip("Seconds to blend pins in.")]
        public float blendInTime = 0.2f;
        [Tooltip("Seconds to blend pins out on release.")]
        public float releaseTime = 0.09f;
        [Tooltip("Metres (at the 1.8 m reference height) the animated foot may move away before the pin is carried over.")]
        public float replantDistance = 0.08f;
        [Tooltip("Seconds a replant takes.")]
        public float replantTime = 0.15f;
        [Tooltip("Metres (at 1.8 m) a foot may sit above the lowest foot and still be pinned; higher feet are lifted.")]
        public float liftTolerance = 0.03f;
        [Tooltip("Time constant (s) of the speed smoothing used to decide when the body is idle.")]
        public float speedSmoothing = 0.08f;
        public bool lockRotation = true;
    }

    public enum FootLockState
    {
        Free,
        Engaging,
        Locked,
        Releasing,
    }

    public sealed class FootLockCore
    {
        struct Foot
        {
            public bool Planted;
            public Vector3 LockPos;
            public Quaternion LockRot;
            public Vector3 ReplantFrom;
            public float ReplantT;
            public float Weight;
            public Vector3 LastOffset;
            public bool HasLastOffset;
        }

        public FootLockSettings Settings;

        /// <summary>Body scale k (height / 1.8 m) applied to every distance setting.</summary>
        public float K = 1f;

        public FootLockState State { get; private set; } = FootLockState.Free;

        /// <summary>Global pin weight (0..1).</summary>
        public float GlobalWeight { get; private set; }
        public float SmoothedSpeed { get; private set; }
        public float RawSpeed { get; private set; }

        readonly Foot[] _feet = new Foot[2];
        Vector3 _lastRoot;
        bool _hasRoot;
        float _idleTimer;
        bool _moveIntent;
        bool _needsCapture;
        float _time;
        float _onsetTime = -1f;

        // Metrics.
        float _idleSeconds;
        float _lockedSeconds;
        float _maxLatency;
        int _releases;
        float _maxReleaseStep;
        float _maxReplantJump;
        double _cpuMs;
        int _cpuFrames;

        public FootLockCore(FootLockSettings settings)
        {
            Settings = settings ?? new FootLockSettings();
            for (int i = 0; i < 2; i++) _feet[i].ReplantT = -1f;
        }

        /// <summary>Asks for an immediate release (a controller knows the body will move before it does).</summary>
        public void SetMoveIntent(bool moving) => _moveIntent = moving;

        public bool IsFootPinned(int foot) => _feet[foot].Planted && _feet[foot].Weight * GlobalWeight > 0.99f;

        /// <summary>Advances the state machine from the motion root position. Call once per frame before Solve.</summary>
        public void UpdateMotion(Vector3 rootPosition, float dt, float externalSpeed = -1f)
        {
            if (dt <= 0f) return;
            _time += dt;
            if (externalSpeed >= 0f) RawSpeed = externalSpeed;
            else if (_hasRoot)
            {
                float dx = rootPosition.x - _lastRoot.x;
                float dz = rootPosition.z - _lastRoot.z;
                RawSpeed = Mathf.Sqrt(dx * dx + dz * dz) / dt;
            }
            _lastRoot = rootPosition;
            _hasRoot = true;
            float a = Settings.speedSmoothing <= 0f ? 1f : 1f - Mathf.Exp(-dt / Settings.speedSmoothing);
            SmoothedSpeed += (RawSpeed - SmoothedSpeed) * a;

            bool wantsRelease = _moveIntent || RawSpeed > Settings.moveSpeed;
            bool idle = !_moveIntent && SmoothedSpeed < Settings.idleSpeed;
            if (idle) _idleSeconds += dt;

            // Motion onset: the first frame the pinned body starts to move (or is told it will). Release latency is
            // measured from here; a twitch that settles again before a release clears it.
            if (State == FootLockState.Locked || State == FootLockState.Engaging)
            {
                if (RawSpeed > Settings.idleSpeed || _moveIntent)
                {
                    if (_onsetTime < 0) _onsetTime = _time;
                }
                else _onsetTime = -1f;
            }

            switch (State)
            {
                case FootLockState.Free:
                    GlobalWeight = 0f;
                    _idleTimer = idle ? _idleTimer + dt : 0f;
                    if (_idleTimer >= Settings.engageDelay)
                    {
                        State = FootLockState.Engaging;
                        _needsCapture = true;
                        _onsetTime = -1f;
                    }
                    break;
                case FootLockState.Engaging:
                    if (wantsRelease) { BeginRelease(); break; }
                    GlobalWeight = Mathf.Min(1f, GlobalWeight + dt / Mathf.Max(1e-3f, Settings.blendInTime));
                    if (GlobalWeight >= 1f) State = FootLockState.Locked;
                    break;
                case FootLockState.Locked:
                    if (wantsRelease) BeginRelease();
                    break;
                case FootLockState.Releasing:
                    GlobalWeight = Mathf.Max(0f, GlobalWeight - dt / Mathf.Max(1e-3f, Settings.releaseTime));
                    if (GlobalWeight <= 0f)
                    {
                        float start = _onsetTime >= 0 ? _onsetTime : _time;
                        _maxLatency = Mathf.Max(_maxLatency, _time - start);
                        _releases++;
                        _onsetTime = -1f;
                        State = FootLockState.Free;
                        _idleTimer = 0f;
                        for (int i = 0; i < 2; i++)
                        {
                            _feet[i].Planted = false;
                            _feet[i].Weight = 0f;
                            _feet[i].ReplantT = -1f;
                        }
                    }
                    break;
            }

            if (idle && (State == FootLockState.Locked || State == FootLockState.Engaging) && AllEligiblePinned()) _lockedSeconds += dt;
        }

        void BeginRelease()
        {
            if (_onsetTime < 0) _onsetTime = _time;
            State = FootLockState.Releasing;
        }

        bool AllEligiblePinned()
        {
            bool any = false;
            for (int i = 0; i < 2; i++)
            {
                if (!_feet[i].Planted) continue;
                any = true;
                if (_feet[i].Weight * GlobalWeight < 0.99f) return false;
            }
            return any;
        }

        /// <summary>
        /// Pins foot 0 (left) or 1 (right). Inputs are the animated foot goal this frame and the lowest animated foot
        /// height. Outputs the pin target and its weight: final foot = lerp(animated, target, weight).
        /// </summary>
        public void Solve(int foot, Vector3 animatedPos, Quaternion animatedRot, float lowestFootY, float dt,
            out Vector3 targetPos, out Quaternion targetRot, out float weight)
        {
            ref Foot f = ref _feet[foot];
            if (_needsCapture && State == FootLockState.Engaging)
            {
                f.LockPos = animatedPos;
                f.LockRot = animatedRot;
                f.Planted = animatedPos.y - lowestFootY <= Settings.liftTolerance * K;
                f.ReplantT = -1f;
                f.Weight = 0f;
                f.HasLastOffset = false;
                if (foot == 1) _needsCapture = false;
            }

            if (State == FootLockState.Free)
            {
                f.Planted = false;
                f.HasLastOffset = false;
                targetPos = animatedPos;
                targetRot = animatedRot;
                weight = 0f;
                return;
            }

            bool lifted = animatedPos.y - lowestFootY > Settings.liftTolerance * K;
            if (State != FootLockState.Releasing)
            {
                if (lifted && f.Planted) f.Planted = false;
                else if (!lifted && !f.Planted)
                {
                    f.Planted = true;
                    f.LockPos = animatedPos;
                    f.LockRot = animatedRot;
                    f.ReplantT = -1f;
                }
            }

            // Replant when the animation has moved the foot too far from its pin.
            if (f.Planted && State != FootLockState.Releasing)
            {
                if (f.ReplantT < 0f)
                {
                    float dx = animatedPos.x - f.LockPos.x;
                    float dz = animatedPos.z - f.LockPos.z;
                    if (dx * dx + dz * dz > Sq(Settings.replantDistance * K))
                    {
                        f.ReplantFrom = f.LockPos;
                        f.ReplantT = 0f;
                    }
                }
                if (f.ReplantT >= 0f)
                {
                    f.ReplantT += dt / Mathf.Max(1e-3f, Settings.replantTime);
                    float s = LocoMath.SmoothStep01(f.ReplantT);
                    f.LockPos = Vector3.Lerp(f.ReplantFrom, animatedPos, s);
                    if (f.ReplantT >= 1f)
                    {
                        f.LockPos = animatedPos;
                        f.LockRot = animatedRot;
                        f.ReplantT = -1f;
                    }
                }
            }

            float footTarget = f.Planted ? 1f : 0f;
            float rate = footTarget > f.Weight ? Settings.blendInTime : Settings.releaseTime;
            f.Weight = Mathf.MoveTowards(f.Weight, footTarget, dt / Mathf.Max(1e-3f, rate));

            weight = f.Weight * GlobalWeight;
            targetPos = f.LockPos;
            targetRot = Settings.lockRotation ? f.LockRot : animatedRot;

            // Pop metrics: frame-to-frame change of the displacement the lock adds to the animated foot.
            Vector3 offset = (f.LockPos - animatedPos) * weight;
            if (f.HasLastOffset)
            {
                float stepCm = (offset - f.LastOffset).magnitude * 100f;
                if (State == FootLockState.Releasing) _maxReleaseStep = Mathf.Max(_maxReleaseStep, stepCm);
                else _maxReplantJump = Mathf.Max(_maxReplantJump, stepCm);
            }
            f.LastOffset = offset;
            f.HasLastOffset = true;
        }

        public void AddCpuSample(double ms)
        {
            _cpuMs += ms;
            _cpuFrames++;
        }

        public bool IsLockActive => State != FootLockState.Free;
        public float LockedFraction => _idleSeconds > 0f ? Mathf.Clamp01(_lockedSeconds / _idleSeconds) : 0f;
        public float MaxReleaseLatencySeconds => _maxLatency;
        public int ReleaseCount => _releases;
        public float MaxReleaseStepCm => _maxReleaseStep;
        public float MaxReplantJumpCm => _maxReplantJump;
        public float AverageCpuMs => _cpuFrames > 0 ? (float)(_cpuMs / _cpuFrames) : 0f;

        public void ResetMetrics()
        {
            _idleSeconds = 0f;
            _lockedSeconds = 0f;
            _maxLatency = 0f;
            _releases = 0;
            _maxReleaseStep = 0f;
            _maxReplantJump = 0f;
            _cpuMs = 0;
            _cpuFrames = 0;
        }

        static float Sq(float v) => v * v;
    }
}
