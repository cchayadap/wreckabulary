using UnityEngine;

namespace Wreckabulary
{
    /// <summary>Allocation-free camera facing for world-space canvases and TextMeshPro labels.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class WorldSpaceBillboard : MonoBehaviour
    {
        [SerializeField] Camera targetCamera;
        [SerializeField] Transform followTarget;
        [Tooltip("World-space offset from Follow Target. Leave the target empty to animate position externally.")]
        [SerializeField] Vector3 structuralOffset = Vector3.zero;
        [SerializeField] bool lockVerticalAxis;

        Transform cachedTransform, cameraTransform;
        Canvas worldCanvas;

        public Camera TargetCamera => targetCamera;
        public Transform FollowTarget { get => followTarget; set => followTarget = value; }
        public Vector3 StructuralOffset { get => structuralOffset; set => structuralOffset = value; }
        public bool LockVerticalAxis { get => lockVerticalAxis; set => lockVerticalAxis = value; }

        void Awake()
        {
            cachedTransform = transform;
            TryGetComponent(out worldCanvas);
            SetCamera(targetCamera ? targetCamera : Camera.main);
        }

        /// <summary>Inject a replacement or per-player camera without searching during LateUpdate.</summary>
        public void SetCamera(Camera camera)
        {
            targetCamera = camera;
            cameraTransform = camera ? camera.transform : null;
            if (worldCanvas && worldCanvas.renderMode == RenderMode.WorldSpace) worldCanvas.worldCamera = camera;
        }

        void LateUpdate()
        {
            if (followTarget) cachedTransform.position = followTarget.position + structuralOffset;
            if (!cameraTransform) return;
            if (!lockVerticalAxis)
            {
                cachedTransform.rotation = cameraTransform.rotation;
                return;
            }
            var forward = cameraTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f) cachedTransform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
