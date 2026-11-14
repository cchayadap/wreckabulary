// Game Studio Kit (MIT). Idle foot lock for any rig (Generic, procedural, Humanoid without IK pass): after the Animator
// writes the pose, LateUpdate pins the feet with the kit's analytic two-bone IK. No package dependency.
// Behaviour and knobs: FootLockCore. Prove it with IdleDriftProbe on every body.
using System;
using System.Diagnostics;
using GameStudioKit.Common;
using UnityEngine;

namespace GameStudioKit.Locomotion
{
    [AddComponentMenu("Game Studio Kit/Idle Foot Lock (Generic)")]
    [DefaultExecutionOrder(100)]
    public sealed class IdleFootLockGeneric : MonoBehaviour, IFootLockMetrics
    {
        [Serializable]
        public sealed class Leg
        {
            public Transform upper;
            public Transform lower;
            public Transform foot;
            [Tooltip("Optional knee pole target; without it the leg keeps its current bend plane.")]
            public Transform hint;

            public bool IsValid => upper != null && lower != null && foot != null;
        }

        public Leg left = new Leg();
        public Leg right = new Leg();
        public FootLockSettings settings = new FootLockSettings();

        [Tooltip("Body height in metres; distances scale by height / 1.8 m.")]
        public float bodyHeightM = 1.8f;

        [Tooltip("Transform whose motion decides idle vs moving; default this transform.")]
        public Transform motionRoot;

        [Tooltip("Optional speed from the movement controller (m/s); negative = measure the motion root.")]
        public float externalSpeed = -1f;

        FootLockCore _core;
        readonly Stopwatch _watch = new Stopwatch();

        public FootLockCore Core => _core;

        void Awake()
        {
            _core = new FootLockCore(settings);
            if (motionRoot == null) motionRoot = transform;
            if (!left.IsValid || !right.IsValid)
            {
                KitLog.Warn("IdleFootLockGeneric on " + name + ": assign upper, lower and foot for both legs");
                enabled = false;
            }
        }

        public void SetMoveIntent(bool moving) => _core?.SetMoveIntent(moving);

        void LateUpdate()
        {
            if (_core == null) return;
            _watch.Restart();
            float dt = Time.deltaTime;
            _core.Settings = settings;
            _core.K = LocoMath.BodyScale(bodyHeightM);
            _core.UpdateMotion(motionRoot.position, dt, externalSpeed);

            Vector3 lp = left.foot.position;
            Vector3 rp = right.foot.position;
            Quaternion lr = left.foot.rotation;
            Quaternion rr = right.foot.rotation;
            float lowest = Mathf.Min(lp.y, rp.y);
            Apply(left, 0, lp, lr, lowest, dt);
            Apply(right, 1, rp, rr, lowest, dt);
            _watch.Stop();
            _core.AddCpuSample(_watch.Elapsed.TotalMilliseconds);
        }

        void Apply(Leg leg, int index, Vector3 animPos, Quaternion animRot, float lowest, float dt)
        {
            _core.Solve(index, animPos, animRot, lowest, dt, out var pos, out var rot, out float w);
            if (w <= 0f) return;
            Vector3 goal = Vector3.Lerp(animPos, pos, w);
            TwoBoneIk.Solve(leg.upper, leg.lower, leg.foot, goal, leg.hint != null ? leg.hint.position : (Vector3?)null, 1f);
            if (settings.lockRotation) leg.foot.rotation = Quaternion.Slerp(animRot, rot, w);
        }

        public bool IsLockActive => enabled && _core != null && _core.IsLockActive;
        public float LockedFraction => _core?.LockedFraction ?? 0f;
        public float MaxReleaseLatencySeconds => _core?.MaxReleaseLatencySeconds ?? 0f;
        public int ReleaseCount => _core?.ReleaseCount ?? 0;
        public float MaxReleaseStepCm => _core?.MaxReleaseStepCm ?? 0f;
        public float MaxReplantJumpCm => _core?.MaxReplantJumpCm ?? 0f;
        public float AverageCpuMs => _core?.AverageCpuMs ?? 0f;
        public void ResetMetrics() => _core?.ResetMetrics();
    }
}
