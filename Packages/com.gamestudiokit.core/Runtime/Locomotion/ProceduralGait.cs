// Game Studio Kit (MIT). Procedural stepping for bodies without locomotion clips (creatures, props with legs, the
// KNOTWELL-style fully procedural route). Planted feet stay fixed in the world; when a foot falls too far behind its
// home spot (rest position under the body, led by the velocity) it steps there along an arc. Legs in the same group
// step together and groups alternate (biped: two groups; quadruped: diagonal pairs). The body bobs and leans through
// springs. Written clean-room for the kit (ARCHITECTURE §3.8 procedural.md).
using System;
using UnityEngine;

namespace GameStudioKit.Locomotion
{
    [AddComponentMenu("Game Studio Kit/Procedural Gait")]
    [DefaultExecutionOrder(90)]
    public sealed class ProceduralGait : MonoBehaviour
    {
        [Serializable]
        public sealed class Leg
        {
            public Transform upper;
            public Transform lower;
            public Transform foot;
            [Tooltip("Optional knee pole target.")]
            public Transform hint;
            [Tooltip("Legs with the same group step together; groups take turns.")]
            public int group;

            [NonSerialized] public Vector3 RestLocal;
            [NonSerialized] public Vector3 Planted;
            [NonSerialized] public Vector3 StepFrom;
            [NonSerialized] public Vector3 StepTo;
            [NonSerialized] public float StepT = -1f;
            [NonSerialized] public Vector3 Current;

            public bool IsValid => upper != null && lower != null && foot != null;
            public bool IsStepping => StepT >= 0f;
        }

        public Leg[] legs = new Leg[0];

        [Tooltip("Transform that moves the body; default this transform.")]
        public Transform motionRoot;
        public LayerMask groundMask = ~0;

        [Tooltip("Body height in metres; distances scale by height / 1.8 m.")]
        public float bodyHeightM = 1.8f;

        [Header("Stepping (distances at the 1.8 m reference)")]
        public float stepDistance = 0.3f;
        public float stepHeight = 0.12f;
        public float stepDuration = 0.28f;
        [Tooltip("Fraction of the step duration's travel added ahead of the home spot.")]
        public float velocityLead = 0.5f;
        [Tooltip("A foot further than this × step distance from home steps even out of turn.")]
        public float forceStepFactor = 2.5f;

        [Header("Body")]
        [Tooltip("Optional pelvis or body bone that bobs and leans.")]
        public Transform body;
        public float bobAmount = 0.03f;
        public float leanDegreesPerMetrePerSecond = 4f;
        public FloatSpring bobSpring = new FloatSpring(4f, 0.6f);
        public FloatSpring leanSpring = new FloatSpring(3f, 0.8f);

        /// <summary>Raised with the leg index whenever a foot lands (hook footstep audio here).</summary>
        public event Action<int> FootPlanted;

        public Vector3 Velocity { get; private set; }

        Vector3 _lastRoot;
        bool _hasRoot;
        Vector3 _bodyRestLocalPos;
        Quaternion _bodyRestLocalRot;
        int _lastGroup = -1;
        readonly RaycastHit[] _hits = new RaycastHit[8];

        float K => LocoMath.BodyScale(bodyHeightM);

        void Awake()
        {
            if (motionRoot == null) motionRoot = transform;
            if (body != null)
            {
                _bodyRestLocalPos = body.localPosition;
                _bodyRestLocalRot = body.localRotation;
            }
            foreach (var leg in legs)
            {
                if (leg == null || !leg.IsValid) continue;
                leg.RestLocal = motionRoot.InverseTransformPoint(leg.foot.position);
                leg.Planted = leg.foot.position;
                leg.Current = leg.Planted;
            }
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 root = motionRoot.position;
            if (_hasRoot) Velocity = (root - _lastRoot) / dt;
            _lastRoot = root;
            _hasRoot = true;
            float k = K;

            UpdateBody(dt, k);

            // Pick at most one group to start stepping this frame, when no other group is mid-step.
            int steppingGroup = -1;
            foreach (var leg in legs) if (leg != null && leg.IsStepping) { steppingGroup = leg.group; break; }
            int bestGroup = -1;
            float bestError = 0f;
            foreach (var leg in legs)
            {
                if (leg == null || !leg.IsValid || leg.IsStepping) continue;
                float err = PlanarDistance(leg.Planted, Home(leg, k));
                bool force = err > stepDistance * k * forceStepFactor;
                if (force) BeginStep(leg, k);
                else if (steppingGroup < 0 && err > stepDistance * k)
                {
                    // Prefer the group that did not step last, then the largest error.
                    float score = err + (leg.group == _lastGroup ? 0f : stepDistance * k);
                    if (score > bestError) { bestError = score; bestGroup = leg.group; }
                }
            }
            if (bestGroup >= 0)
            {
                foreach (var leg in legs)
                    if (leg != null && leg.IsValid && !leg.IsStepping && leg.group == bestGroup) BeginStep(leg, k);
                _lastGroup = bestGroup;
            }

            for (int i = 0; i < legs.Length; i++)
            {
                var leg = legs[i];
                if (leg == null || !leg.IsValid) continue;
                if (leg.IsStepping)
                {
                    leg.StepT += dt / Mathf.Max(0.01f, stepDuration);
                    float t = Mathf.Clamp01(leg.StepT);
                    float s = LocoMath.SmoothStep01(t);
                    leg.Current = Vector3.Lerp(leg.StepFrom, leg.StepTo, s) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * stepHeight * k);
                    if (leg.StepT >= 1f)
                    {
                        leg.StepT = -1f;
                        leg.Planted = leg.StepTo;
                        leg.Current = leg.Planted;
                        FootPlanted?.Invoke(i);
                    }
                }
                else leg.Current = leg.Planted;

                TwoBoneIk.Solve(leg.upper, leg.lower, leg.foot, leg.Current, leg.hint != null ? leg.hint.position : (Vector3?)null, 1f);
            }
        }

        void BeginStep(Leg leg, float k)
        {
            leg.StepFrom = leg.Planted;
            leg.StepTo = Home(leg, k);
            leg.StepT = 0f;
        }

        /// <summary>Rest position under the body, led by the velocity and dropped onto the ground.</summary>
        Vector3 Home(Leg leg, float k)
        {
            Vector3 p = motionRoot.TransformPoint(leg.RestLocal);
            Vector3 lead = Velocity;
            lead.y = 0f;
            p += lead * (stepDuration * velocityLead);
            var origin = p + Vector3.up * (0.6f * k);
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, 2f * k, groundMask, QueryTriggerInteraction.Ignore);
            float bestDist = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].collider.transform.IsChildOf(motionRoot)) continue;
                if (_hits[i].distance < bestDist)
                {
                    bestDist = _hits[i].distance;
                    p.y = _hits[i].point.y + (motionRoot.TransformPoint(leg.RestLocal).y - motionRoot.position.y);
                }
            }
            return p;
        }

        void UpdateBody(float dt, float k)
        {
            if (body == null) return;
            // Bob: the body dips while legs are mid-step (weight on fewer legs), springs back when all are planted.
            int stepping = 0, valid = 0;
            foreach (var leg in legs)
            {
                if (leg == null || !leg.IsValid) continue;
                valid++;
                if (leg.IsStepping) stepping++;
            }
            float bobTarget = valid > 0 ? -bobAmount * k * stepping / valid : 0f;
            float bob = bobSpring.Step(bobTarget, dt);

            Vector3 localVel = motionRoot.InverseTransformDirection(Velocity);
            float leanTarget = Mathf.Clamp(localVel.z * leanDegreesPerMetrePerSecond, -20f, 20f);
            float lean = leanSpring.Step(leanTarget, dt);

            body.localPosition = _bodyRestLocalPos + Vector3.up * bob;
            body.localRotation = _bodyRestLocalRot * Quaternion.AngleAxis(lean, Vector3.right);
        }

        static float PlanarDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
