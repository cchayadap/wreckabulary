// Game Studio Kit (MIT). Idle foot lock for Humanoid rigs, applied through Unity's foot IK goals in OnAnimatorIK.
// The Animator layer set in ikLayer needs "IK Pass" enabled. Generic rigs use IdleFootLockGeneric.
// Behaviour and knobs: FootLockCore. Prove it with IdleDriftProbe on every body (thresholds scale with body height).
using System.Diagnostics;
using GameStudioKit.Common;
using UnityEngine;

namespace GameStudioKit.Locomotion
{
    [AddComponentMenu("Game Studio Kit/Idle Foot Lock (Humanoid)")]
    [RequireComponent(typeof(Animator))]
    public sealed class IdleFootLock : MonoBehaviour, IFootLockMetrics
    {
        public FootLockSettings settings = new FootLockSettings();

        [Tooltip("Animator layer whose IK pass applies the lock (the layer needs IK Pass on).")]
        public int ikLayer;

        [Tooltip("Body height in metres for scaling distances; 0 = animator.humanScale × 1.8 m.")]
        public float bodyHeightM;

        [Tooltip("Transform whose motion decides idle vs moving; default this transform.")]
        public Transform motionRoot;

        [Tooltip("Optional speed from the movement controller (m/s); negative = measure the motion root.")]
        public float externalSpeed = -1f;

        Animator _animator;
        FootLockCore _core;
        readonly Stopwatch _watch = new Stopwatch();

        public FootLockCore Core => _core;

        void Awake()
        {
            _animator = GetComponent<Animator>();
            _core = new FootLockCore(settings);
            if (motionRoot == null) motionRoot = transform;
            if (!_animator.isHuman)
            {
                KitLog.Warn("IdleFootLock on " + name + " needs a Humanoid avatar; use IdleFootLockGeneric for Generic rigs");
                enabled = false;
            }
        }

        /// <summary>Tell the lock the body is about to move (releases at once instead of waiting for motion).</summary>
        public void SetMoveIntent(bool moving) => _core?.SetMoveIntent(moving);

        void OnAnimatorIK(int layerIndex)
        {
            if (!enabled || layerIndex != ikLayer || _core == null) return;
            _watch.Restart();
            float dt = Time.deltaTime;
            _core.Settings = settings;
            _core.K = bodyHeightM > 0f ? LocoMath.BodyScale(bodyHeightM) : Mathf.Max(0.01f, _animator.humanScale);
            _core.UpdateMotion(motionRoot.position, dt, externalSpeed);

            Vector3 lp = _animator.GetIKPosition(AvatarIKGoal.LeftFoot);
            Vector3 rp = _animator.GetIKPosition(AvatarIKGoal.RightFoot);
            Quaternion lr = _animator.GetIKRotation(AvatarIKGoal.LeftFoot);
            Quaternion rr = _animator.GetIKRotation(AvatarIKGoal.RightFoot);
            float lowest = Mathf.Min(lp.y, rp.y);

            Apply(AvatarIKGoal.LeftFoot, 0, lp, lr, lowest, dt);
            Apply(AvatarIKGoal.RightFoot, 1, rp, rr, lowest, dt);
            _watch.Stop();
            _core.AddCpuSample(_watch.Elapsed.TotalMilliseconds);
        }

        void Apply(AvatarIKGoal goal, int foot, Vector3 animPos, Quaternion animRot, float lowest, float dt)
        {
            _core.Solve(foot, animPos, animRot, lowest, dt, out var pos, out var rot, out float w);
            _animator.SetIKPositionWeight(goal, w);
            _animator.SetIKRotationWeight(goal, settings.lockRotation ? w : 0f);
            if (w > 0f)
            {
                _animator.SetIKPosition(goal, pos);
                _animator.SetIKRotation(goal, rot);
            }
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
