// Game Studio Kit (MIT). Keeps locomotion clips' foot speed matched to the body's ground speed, so feet do not skate.
// Mode BodyScale uses the baseline playback scale for the body size; mode StrideMatched divides the measured ground
// speed by the clip's scaled stride speed (take the stride from GaitProbe.MeasuredStrideM or AnimSheetCapture).
// The value goes to an Animator float parameter used as a state speed multiplier, or to Animator.speed.
using GameStudioKit.Common;
using UnityEngine;

namespace GameStudioKit.Locomotion
{
    [AddComponentMenu("Game Studio Kit/Loco Scaler")]
    [DefaultExecutionOrder(-50)]
    public sealed class LocoScaler : MonoBehaviour
    {
        public enum Mode
        {
            BodyScale,
            StrideMatched,
        }

        public Animator animator;
        public Mode mode = Mode.BodyScale;

        [Tooltip("Animator float used as the locomotion states' speed multiplier. Empty = set Animator.speed instead.")]
        public string playbackParameter = "LocoScale";

        [Tooltip("Body height in metres; 0 = Humanoid animator.humanScale × 1.8 m (Generic rigs must set it).")]
        public float bodyHeightM;

        [Header("Body scale mode")]
        public float maxScale = 1.25f;
        public float minHumanScale = 0.3f;

        [Header("Stride matched mode")]
        [Tooltip("Ground covered by one full clip cycle at playback 1 on a 1.8 m body.")]
        public float strideAtReferenceM = 1.4f;
        [Tooltip("Clip cycle duration in seconds at playback 1.")]
        public float cycleSeconds = 1f;
        public float minPlayback = 0.5f;
        public float maxPlayback = 2f;
        [Tooltip("Below this ground speed (m/s) the playback stays at 1 (idle, turning in place).")]
        public float minGroundSpeed = 0.05f;

        [Tooltip("Optional external speed source; when < 0 the speed is measured from this transform.")]
        public float externalSpeed = -1f;

        public float CurrentPlayback { get; private set; } = 1f;
        public float MeasuredSpeed { get; private set; }

        int _paramHash;
        bool _hasParam;
        Vector3 _lastPos;
        bool _hasLast;

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            ResolveParameter();
        }

        void OnEnable() => _hasLast = false;

        void ResolveParameter()
        {
            _hasParam = false;
            if (animator == null || string.IsNullOrEmpty(playbackParameter)) return;
            _paramHash = Animator.StringToHash(playbackParameter);
            foreach (var p in animator.parameters)
            {
                if (p.nameHash == _paramHash && p.type == AnimatorControllerParameterType.Float)
                {
                    _hasParam = true;
                    return;
                }
            }
            KitLog.Warn("LocoScaler on " + name + ": animator has no float parameter '" + playbackParameter + "'; using Animator.speed");
        }

        /// <summary>k = body height / 1.8 m.</summary>
        public float BodyK
        {
            get
            {
                if (bodyHeightM > 0) return LocoMath.BodyScale(bodyHeightM);
                if (animator != null && animator.isHuman) return Mathf.Max(0.01f, animator.humanScale);
                return 1f;
            }
        }

        void Update()
        {
            if (animator == null) return;
            float dt = Time.deltaTime;
            Vector3 pos = transform.position;
            if (externalSpeed >= 0f) MeasuredSpeed = externalSpeed;
            else if (_hasLast && dt > 0f)
            {
                Vector3 d = pos - _lastPos;
                d.y = 0f;
                MeasuredSpeed = d.magnitude / dt;
            }
            _lastPos = pos;
            _hasLast = true;

            CurrentPlayback = mode == Mode.BodyScale
                ? LocoMath.BaselineScale(BodyK, maxScale, minHumanScale)
                : MeasuredSpeed < minGroundSpeed ? 1f : LocoMath.StridePlayback(MeasuredSpeed, strideAtReferenceM, cycleSeconds, BodyK, minPlayback, maxPlayback);

            if (_hasParam) animator.SetFloat(_paramHash, CurrentPlayback);
            else animator.speed = CurrentPlayback;
        }
    }
}
