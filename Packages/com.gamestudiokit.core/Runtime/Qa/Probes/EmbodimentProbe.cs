// Game Studio Kit (MIT). Base class of the per-body embodiment probes. A probe samples the final pose every frame
// (LateUpdate at execution order 32000, after animation, IK and foot locks) between BeginWindow and EndWindow, then
// turns its measurements into probe results with the project's thresholds, scaled by the body's k.
using System.Collections.Generic;
using UnityEngine;

namespace GameStudioKit.Qa.Probes
{
    [DefaultExecutionOrder(32000)]
    public abstract class EmbodimentProbe : MonoBehaviour
    {
        [Tooltip("probes.config.json bodies[].id (sets k = body height / reference height).")]
        public string bodyId;
        [Tooltip("Clip or state being measured (reported with each result).")]
        public string clip;
        public Animator animator;

        [Header("Generic rigs: bone names (Humanoid rigs map through the avatar)")]
        public string hipsBone;
        public string headBone;
        public string leftFootBone;
        public string rightFootBone;
        public string leftHandBone;
        public string rightHandBone;

        public LayerMask groundMask = ~0;

        public bool IsSampling { get; private set; }
        public int SampleCount { get; protected set; }
        public BodyBones Bones { get; private set; }

        /// <summary>Why a window measured nothing (or less than it needs), for the caller to record with
        /// QaContext.Note; probe results cannot carry text. Cleared by BeginWindow.</summary>
        public readonly List<string> Notes = new List<string>();

        protected readonly RaycastHit[] Hits = new RaycastHit[16];

        protected virtual void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        /// <summary>Resolves bones and starts a measurement window.</summary>
        public void BeginWindow()
        {
            Bones = ResolveBones();
            SampleCount = 0;
            Notes.Clear();
            OnBegin();
            IsSampling = true;
        }

        /// <summary>Ends the window and returns probe results evaluated against <paramref name="config"/>.</summary>
        public List<ProbeResult> EndWindow(ProbeConfig config)
        {
            IsSampling = false;
            var results = new List<ProbeResult>();
            OnEnd(config ?? ProbeConfig.Defaults(), results);
            return results;
        }

        protected BodyBones ResolveBones()
        {
            bool explicitNames = !string.IsNullOrEmpty(leftFootBone) || !string.IsNullOrEmpty(hipsBone);
            if (explicitNames)
                return BodyBones.FromNames(animator != null ? animator.transform : transform, hipsBone, headBone, leftFootBone, rightFootBone, leftHandBone, rightHandBone);
            return animator != null ? BodyBones.FromAnimator(animator) : BodyBones.Guess(transform);
        }

        void LateUpdate()
        {
            if (!IsSampling || Bones == null) return;
            Sample(Time.deltaTime);
            SampleCount++;
        }

        protected abstract void OnBegin();
        protected abstract void Sample(float dt);
        protected abstract void OnEnd(ProbeConfig config, List<ProbeResult> results);

        protected string ProbeName => GetType().Name;

        protected ProbeResult Eval(ProbeConfig config, string metric, double value) =>
            config.Evaluate(ProbeName, metric, value, string.IsNullOrEmpty(bodyId) ? null : bodyId, string.IsNullOrEmpty(clip) ? null : clip);

        protected float Ground(Vector3 at, float fallback) =>
            BodyBones.GroundHeight(at, Bones?.Root, groundMask, fallback, Hits);

        /// <summary>Angle between <paramref name="v"/> and world up, in degrees (0 = upright).</summary>
        protected static float TiltFromUp(Vector3 v) => v.sqrMagnitude < 1e-10f ? 0f : Vector3.Angle(v, Vector3.up);

        protected static float Median(List<float> values)
        {
            if (values.Count == 0) return 0f;
            var copy = values.ToArray();
            System.Array.Sort(copy);
            int mid = copy.Length / 2;
            return copy.Length % 2 == 1 ? copy[mid] : 0.5f * (copy[mid - 1] + copy[mid]);
        }
    }
}
