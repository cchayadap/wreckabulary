// Game Studio Kit (MIT). Locomotion gait probe for one body × clip. Run a window of at least one full cycle.
// Metrics (definitions are the kit's; thresholds from probes.config.json, distances × k):
//  - stance_foot_slide_cm_per_step: per step (the time one foot is the stance foot), the sum over frames of the smaller
//    of the two feet's planar world displacements (the stance foot should not move in the world); worst full step.
//  - foot_heading_vs_facing_deg: during each step the stance foot travels backwards relative to the body; the angle
//    between that travel (reversed) and the body's facing (plus expectedHeadingDeg for strafe or backward clips),
//    averaged over steps weighted by travel.
//  - foot_crossing_frames_per_cycle: frames where the left foot is on or right of the right foot in body space, per
//    full cycle (two steps).
//  - stance_height_error_cm: median signed height of the stance foot's sole above the ground (positive floats,
//    negative sinks); sole = foot bone minus soleHeight (Humanoid default: the avatar's foot bottom height).
// A window with fewer than two complete steps (one full cycle) measured no gait: it reports
// stance_foot_slide_cm_per_step as NaN (FAIL) and nothing else, never 0-valued PASSes for a body stuck in idle.
using System.Collections.Generic;
using UnityEngine;

namespace GameStudioKit.Qa.Probes
{
    [AddComponentMenu("Game Studio Kit/Probes/Gait Probe")]
    public sealed class GaitProbe : EmbodimentProbe
    {
        [Tooltip("Expected travel direction relative to facing (0 forward, 180 backward, ±90 strafe).")]
        public float expectedHeadingDeg;
        [Tooltip("Foot bone height above the sole in metres; negative = auto (Humanoid foot bottom height, else 0).")]
        public float soleHeight = -1f;

        /// <summary>Mean root distance per full cycle (two steps) over complete steps, in metres.</summary>
        public float MeasuredStrideM { get; private set; }
        public int CompleteSteps { get; private set; }

        /// <summary>Complete steps a window needs before any gait metric is reported (one full cycle).</summary>
        public const int MinCompleteSteps = 2;

        Vector3 _lastL, _lastR;
        bool _hasLast;
        int _stance = -1;
        float _stepSlide;
        Vector3 _stepStartLocal;
        Vector3 _stepStartRoot;
        bool _stepStarted;
        bool _firstStepPartial;
        readonly List<float> _slidePerStep = new List<float>();
        readonly List<float> _strideSamples = new List<float>();
        readonly List<float> _heightErrors = new List<float>();
        float _headingWeighted;
        float _headingWeight;
        int _crossFrames;
        float _sole;

        protected override void OnBegin()
        {
            _hasLast = false;
            _stance = -1;
            _stepSlide = 0f;
            _stepStarted = false;
            _firstStepPartial = true;
            _slidePerStep.Clear();
            _strideSamples.Clear();
            _heightErrors.Clear();
            _headingWeighted = 0f;
            _headingWeight = 0f;
            _crossFrames = 0;
            CompleteSteps = 0;
            MeasuredStrideM = 0f;
            _sole = soleHeight >= 0f ? soleHeight
                : animator != null && animator.isHuman ? Mathf.Min(animator.leftFeetBottomHeight, animator.rightFeetBottomHeight) : 0f;
        }

        protected override void Sample(float dt)
        {
            var b = Bones;
            if (!b.HasFeet || b.Root == null) return;
            Vector3 l = b.LeftFoot.position;
            Vector3 r = b.RightFoot.position;
            Vector3 root = b.Root.position;
            float gl = Ground(l, root.y);
            float gr = Ground(r, root.y);
            float hl = l.y - _sole - gl;
            float hr = r.y - _sole - gr;

            if (_hasLast)
            {
                float dl = BodyBones.Planar(l, _lastL);
                float dr = BodyBones.Planar(r, _lastR);
                float speedL = dt > 0 ? dl / dt : 0f;
                float speedR = dt > 0 ? dr / dt : 0f;
                // Stance = lower foot, nudged by speed; switch only on a clear margin (hysteresis).
                float scoreL = hl + 0.02f * speedL;
                float scoreR = hr + 0.02f * speedR;
                int wanted = scoreL <= scoreR ? 0 : 1;
                if (_stance < 0) BeginStep(wanted, l, r, root);
                else if (wanted != _stance && Mathf.Abs(scoreL - scoreR) > 0.005f) EndStep(wanted, l, r, root);
                _stepSlide += Mathf.Min(dl, dr);
                _heightErrors.Add(_stance == 0 ? hl : hr);
            }

            // Crossing in body space: +x is the body's right.
            float xl = b.Root.InverseTransformPoint(l).x;
            float xr = b.Root.InverseTransformPoint(r).x;
            if (xl >= xr) _crossFrames++;

            _lastL = l;
            _lastR = r;
            _hasLast = true;
        }

        void BeginStep(int stance, Vector3 l, Vector3 r, Vector3 root)
        {
            _stance = stance;
            _stepSlide = 0f;
            _stepStartLocal = Bones.Root.InverseTransformPoint(stance == 0 ? l : r);
            _stepStartRoot = root;
            _stepStarted = true;
        }

        void EndStep(int newStance, Vector3 l, Vector3 r, Vector3 root)
        {
            if (_stepStarted && !_firstStepPartial)
            {
                CompleteSteps++;
                _slidePerStep.Add(_stepSlide);
                _strideSamples.Add(2f * BodyBones.Planar(root, _stepStartRoot));
                Vector3 endLocal = Bones.Root.InverseTransformPoint(_stance == 0 ? l : r);
                Vector3 travel = _stepStartLocal - endLocal; // reversed: the direction the body moved over the foot
                travel.y = 0f;
                float mag = travel.magnitude;
                if (mag > 0.01f)
                {
                    Vector3 expected = Quaternion.AngleAxis(expectedHeadingDeg, Vector3.up) * Vector3.forward;
                    _headingWeighted += Vector3.Angle(travel, expected) * mag;
                    _headingWeight += mag;
                }
            }
            _firstStepPartial = false;
            BeginStep(newStance, l, r, root);
        }

        protected override void OnEnd(ProbeConfig config, List<ProbeResult> results)
        {
            if (Bones == null || !Bones.HasFeet)
            {
                Notes.Add("GaitProbe: the body has no feet the probe can find (set the foot bone names for a Generic rig)");
                results.Add(Eval(config, "stance_foot_slide_cm_per_step", double.NaN));
                return;
            }
            if (CompleteSteps < MinCompleteSteps)
            {
                Notes.Add("GaitProbe: no complete gait cycle was measured (" + CompleteSteps + " complete step(s) in " + SampleCount +
                          " frames; need " + MinCompleteSteps + "): is the clip playing, and is the window at least one cycle long?");
                results.Add(Eval(config, "stance_foot_slide_cm_per_step", double.NaN));
                return;
            }
            float worstSlide = 0f;
            foreach (var s in _slidePerStep) worstSlide = Mathf.Max(worstSlide, s);
            results.Add(Eval(config, "stance_foot_slide_cm_per_step", worstSlide * 100f));

            if (_headingWeight > 0f) results.Add(Eval(config, "foot_heading_vs_facing_deg", _headingWeighted / _headingWeight));

            float cycles = CompleteSteps / 2f;
            results.Add(Eval(config, "foot_crossing_frames_per_cycle", Mathf.Ceil(_crossFrames / cycles)));
            results.Add(Eval(config, "stance_height_error_cm", Median(_heightErrors) * 100f));

            if (_strideSamples.Count > 0)
            {
                float sum = 0f;
                foreach (var s in _strideSamples) sum += s;
                MeasuredStrideM = sum / _strideSamples.Count;
            }
        }
    }
}
