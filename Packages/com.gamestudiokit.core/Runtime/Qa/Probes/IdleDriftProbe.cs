// Game Studio Kit (MIT). Idle quality probe for one body × idle clip, plus the foot-lock self-test numbers.
// Only frames where the body's planar speed is below idleSpeed count as idle. Metrics (distances × k):
//  - idle_foot_drift_lock_on_cm / idle_foot_drift_lock_off_cm: the sum over idle frames of the smaller of the two feet's
//    planar world displacements (isolates the stance foot); "lock_on" when an active IFootLockMetrics is on the body.
//  - idle_pop_offset_jump_cm: the lock's worst single-frame offset change while idle; without a lock, the worst
//    single-frame planar jump of either foot while idle.
//  - idle_spine_lean_deg: worst angle of hips→head from vertical, minus referenceLeanDeg (the rest pose's own lean).
//  - idle_head_tilt_deg: worst angle of the head's up axis from world up (calibrate the axis with CalibrateRest).
//  - locked_fraction, foot_lock_cpu_ms_per_character, lock_release_latency_s, walk_start_foot_pop_cm: read from the
//    lock (release numbers only after at least one release inside the window, e.g. a scripted walk start).
using System.Collections.Generic;
using GameStudioKit.Locomotion;
using UnityEngine;

namespace GameStudioKit.Qa.Probes
{
    [AddComponentMenu("Game Studio Kit/Probes/Idle Drift Probe")]
    public sealed class IdleDriftProbe : EmbodimentProbe
    {
        [Tooltip("Planar speed (m/s) below which a frame counts as idle.")]
        public float idleSpeed = 0.1f;
        [Tooltip("Lean of hips→head in the rest pose (degrees), subtracted from the measured lean.")]
        public float referenceLeanDeg;

        IFootLockMetrics _lock;
        Vector3 _lastL, _lastR, _lastRoot;
        bool _hasLast;
        float _drift;
        float _maxJump;
        float _maxLean;
        float _maxTilt;
        int _idleFrames;
        Vector3 _headUpLocal = Vector3.up;
        bool _calibrated;

        /// <summary>
        /// Records the head's up axis from the current pose, which must be upright (a rest or T-pose, or the first idle
        /// frame of a clip known to be upright). Without calibration the head bone's local +Y is used.
        /// </summary>
        public void CalibrateRest()
        {
            var b = Bones ?? ResolveBones();
            if (b?.Head == null) return;
            _headUpLocal = b.Head.InverseTransformDirection(Vector3.up);
            _calibrated = true;
            if (b.HasSpine) referenceLeanDeg = TiltFromUp(b.Head.position - b.Hips.position);
        }

        protected override void OnBegin()
        {
            _lock = FindLock();
            _lock?.ResetMetrics();
            _hasLast = false;
            _drift = 0f;
            _maxJump = 0f;
            _maxLean = 0f;
            _maxTilt = 0f;
            _idleFrames = 0;
        }

        IFootLockMetrics FindLock()
        {
            var root = animator != null ? animator.gameObject : gameObject;
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>())
                if (mb is IFootLockMetrics m && mb.enabled) return m;
            return null;
        }

        protected override void Sample(float dt)
        {
            var b = Bones;
            if (!b.HasFeet || b.Root == null) return;
            Vector3 l = b.LeftFoot.position;
            Vector3 r = b.RightFoot.position;
            Vector3 root = b.Root.position;
            if (_hasLast && dt > 0f)
            {
                float speed = BodyBones.Planar(root, _lastRoot) / dt;
                if (speed < idleSpeed)
                {
                    _idleFrames++;
                    float dl = BodyBones.Planar(l, _lastL);
                    float dr = BodyBones.Planar(r, _lastR);
                    _drift += Mathf.Min(dl, dr);
                    _maxJump = Mathf.Max(_maxJump, Mathf.Max(dl, dr));
                    if (b.HasSpine) _maxLean = Mathf.Max(_maxLean, TiltFromUp(b.Head.position - b.Hips.position) - referenceLeanDeg);
                    if (b.Head != null)
                    {
                        Vector3 up = _calibrated ? b.Head.TransformDirection(_headUpLocal) : b.Head.up;
                        _maxTilt = Mathf.Max(_maxTilt, TiltFromUp(up));
                    }
                }
            }
            _lastL = l;
            _lastR = r;
            _lastRoot = root;
            _hasLast = true;
        }

        protected override void OnEnd(ProbeConfig config, List<ProbeResult> results)
        {
            if (Bones == null || !Bones.HasFeet || _idleFrames == 0)
            {
                results.Add(Eval(config, _lock != null ? "idle_foot_drift_lock_on_cm" : "idle_foot_drift_lock_off_cm", double.NaN));
                return;
            }
            bool lockOn = _lock != null;
            results.Add(Eval(config, lockOn ? "idle_foot_drift_lock_on_cm" : "idle_foot_drift_lock_off_cm", _drift * 100f));
            results.Add(Eval(config, "idle_pop_offset_jump_cm", lockOn ? _lock.MaxReplantJumpCm : _maxJump * 100f));
            if (Bones.HasSpine) results.Add(Eval(config, "idle_spine_lean_deg", Mathf.Max(0f, _maxLean)));
            if (Bones.Head != null) results.Add(Eval(config, "idle_head_tilt_deg", _maxTilt));
            if (lockOn)
            {
                results.Add(Eval(config, "locked_fraction", _lock.LockedFraction));
                results.Add(Eval(config, "foot_lock_cpu_ms_per_character", _lock.AverageCpuMs));
                if (_lock.ReleaseCount > 0)
                {
                    results.Add(Eval(config, "lock_release_latency_s", _lock.MaxReleaseLatencySeconds));
                    results.Add(Eval(config, "walk_start_foot_pop_cm", _lock.MaxReleaseStepCm));
                }
            }
        }
    }
}
