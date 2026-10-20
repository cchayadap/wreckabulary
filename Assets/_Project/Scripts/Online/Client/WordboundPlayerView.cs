using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wreckabulary.Online
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class WordboundPlayerView : MonoBehaviour
    {
        readonly RaycastHit[] obstructions = new RaycastHit[64];
        WordboundGameplayController controller;
        WordboundNetworkBody player;
        InputBinding binding;
        IDisposable ownedBinding;
        Camera view;
        GameObject ownedCamera;
        CursorLockMode previousLock;
        bool previousCursor, initialized, capturedCursor, jumpPending, placed;
        Vector2 movement;
        float yaw, pitch = ShoulderView.DefaultPitch, distance = ShoulderView.Distance;
        public bool MenuOpen { get; private set; } = true;
        public Camera Camera => view;
        public WordboundNetworkBody Player => player;
        public event Action<PlayerCommands> CommandsRead;
        public event Action MenuRequested;

        public void Initialize(WordboundGameplayController gameplay, Camera camera = null, InputBinding controls = null)
        {
            if (initialized) throw new InvalidOperationException("Player view is already initialized.");
            controller = gameplay != null ? gameplay : throw new ArgumentNullException(nameof(gameplay));
            if (controls == null)
            {
                var desktop = new DesktopBinding();
                controls = desktop;
                ownedBinding = desktop;
            }
            binding = controls;
            view = camera;
            if (view == null)
            {
                ownedCamera = new GameObject("Wordbound player camera");
                view = ownedCamera.AddComponent<Camera>();
                view.clearFlags = CameraClearFlags.Skybox;
            }
            view.fieldOfView = ShoulderView.FieldOfView;
            view.nearClipPlane = ShoulderView.NearClip;
            view.farClipPlane = 1800f;
            previousLock = Cursor.lockState;
            previousCursor = Cursor.visible;
            capturedCursor = true;
            initialized = true;
        }

        public void LocalPlayerChanged(WordboundNetworkBody body)
        {
            player = body;
            placed = false;
            movement = Vector2.zero;
            jumpPending = false;
            if (body != null) yaw = body.transform.eulerAngles.y * Mathf.Deg2Rad;
        }

        public void SetMenuOpen(bool open)
        {
            MenuOpen = open;
            movement = Vector2.zero;
            jumpPending = false;
            ApplyCursor();
        }

        void ApplyCursor()
        {
            if (!initialized) return;
            bool lockMouse = binding.ReadsMouse && !MenuOpen && Application.isFocused && player != null;
            Cursor.lockState = lockMouse ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockMouse;
        }

        void Update()
        {
            if (!initialized) return;
            bool pause = binding is DesktopBinding desktop ? desktop.Pause.WasPressedThisFrame() : binding.StartPressed();
            if (pause && Application.isFocused)
            {
                SetMenuOpen(!MenuOpen);
                MenuRequested?.Invoke();
            }
            if (MenuOpen || !Application.isFocused || player == null || controller.IsSimulationPaused)
            {
                movement = Vector2.zero;
                jumpPending = false;
                return;
            }
            ApplyCursor();
            var commands = default(PlayerCommands);
            binding.Read(ref commands);
            if (binding.ReadsMouse)
            {
                yaw += commands.lookDelta.x * ShoulderView.MouseSensitivity;
                pitch -= commands.lookDelta.y * ShoulderView.MouseSensitivity;
            }
            else
            {
                yaw += commands.look.x * 2.6f * Time.unscaledDeltaTime;
                pitch -= commands.look.y * 1.8f * Time.unscaledDeltaTime;
            }
            pitch = Mathf.Clamp(pitch, ShoulderView.MinPitch, ShoulderView.MaxPitch);
            yaw = Mathf.Repeat(yaw + Mathf.PI, 2 * Mathf.PI) - Mathf.PI;
            movement = ShoulderView.CameraRelative(Vector2.ClampMagnitude(commands.move, 1), yaw);
            jumpPending |= commands.jump;
            CommandsRead?.Invoke(commands);
        }

        public WordboundMovementInput ReadMovement()
        {
            if (!initialized || MenuOpen || !Application.isFocused || player == null || controller.IsSimulationPaused)
            {
                jumpPending = false;
                return default;
            }
            var input = new WordboundMovementInput { MoveX = movement.x, MoveZ = movement.y, Jump = jumpPending };
            jumpPending = false;
            return input;
        }

        void LateUpdate()
        {
            if (!initialized || view == null || player == null || player.Object == null || !player.Object.IsValid) return;
            var visual = player.GetComponent<WordboundGameplayBody>()?.VisualRoot;
            Vector3 feet = visual != null ? visual.position : player.transform.position;
            Vector3 pivot = feet + Vector3.up * (ShoulderView.PivotHeight + ShoulderView.Lift);
            Vector3 forward = ShoulderView.Forward(yaw, pitch);
            float allowed = ShoulderView.Distance;
            var scene = player.gameObject.scene.GetPhysicsScene();
            if (!scene.IsValid() || scene == Physics.defaultPhysicsScene) return;
            int count = scene.SphereCast(pivot, ShoulderView.ProbeRadius, -forward, obstructions,
                ShoulderView.Distance, ~0, QueryTriggerInteraction.Ignore);
            if (count == obstructions.Length) allowed = ShoulderView.MinDistance;
            else
                for (int i = 0; i < count; i++)
                {
                    var collider = obstructions[i].collider;
                    if (collider == null || collider.attachedRigidbody == player.Rigidbody) continue;
                    allowed = Mathf.Min(allowed, obstructions[i].distance - ShoulderView.Skin);
                }
            allowed = Mathf.Clamp(allowed, ShoulderView.MinDistance, ShoulderView.Distance);
            distance = !placed || allowed < distance ? allowed :
                Mathf.Lerp(distance, allowed, 1 - Mathf.Exp(-ShoulderView.OutEase * Time.unscaledDeltaTime));
            view.transform.SetPositionAndRotation(pivot - forward * distance, Quaternion.LookRotation(forward, Vector3.up));
            placed = true;
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && initialized) SetMenuOpen(true);
        }

        void OnDisable()
        {
            movement = Vector2.zero;
            jumpPending = false;
            if (capturedCursor)
            {
                Cursor.lockState = previousLock;
                Cursor.visible = previousCursor;
            }
        }

        void OnDestroy()
        {
            ownedBinding?.Dispose();
            ownedBinding = null;
            if (ownedCamera != null) Destroy(ownedCamera);
        }
    }
}
