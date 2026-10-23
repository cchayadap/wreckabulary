using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wreckabulary.Online
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class WordboundPlayerView : MonoBehaviour
    {
        public WordboundCameraProfile Profile;
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
        float followDistance = ShoulderView.Distance, minimumDistance = ShoulderView.MinDistance;
        float pivotHeight = ShoulderView.PivotHeight, lift = ShoulderView.Lift;
        float probeRadius = ShoulderView.ProbeRadius, skin = ShoulderView.Skin, outEase = ShoulderView.OutEase;
        float minimumPitch = ShoulderView.MinPitch, maximumPitch = ShoulderView.MaxPitch;
        float sensitivity = ShoulderView.MouseSensitivity, stickYaw = 2.6f, stickPitch = 1.8f;
        WordboundCameraProfile activeProfile;
        string profileError;
        int bindingRevision;
        public bool MenuOpen { get; private set; } = true;
        public Camera Camera => view;
        public WordboundNetworkBody Player => player;
        public bool UsesGamepad => binding is WordboundInputBinding automatic &&
            automatic.ActiveGamepad != null && automatic.ActiveGamepad.added && automatic.ActiveGamepad.enabled;
        public string ControlHint(WordboundControl control) => WordboundInputBinding.ControlHint(binding, control);
        public event Action<PlayerCommands> CommandsRead;
        public event Action<WordboundMenuCommands> MenuCommandsRead;
        public event Action MenuRequested;

        public void Initialize(WordboundGameplayController gameplay, Camera camera = null, InputBinding controls = null,
            WordboundCameraProfile cameraProfile = null)
        {
            if (initialized) throw new InvalidOperationException("Player view is already initialized.");
            controller = gameplay != null ? gameplay : throw new ArgumentNullException(nameof(gameplay));
            if (controls == null)
            {
                var automatic = new WordboundInputBinding();
                controls = automatic;
                ownedBinding = automatic;
            }
            binding = controls;
            view = camera;
            if (view == null)
            {
                ownedCamera = new GameObject("Wordbound player camera");
                view = ownedCamera.AddComponent<Camera>();
                view.clearFlags = CameraClearFlags.Skybox;
                GraphicsOptions.ApplyTo(view);
            }
            if (cameraProfile) Profile = cameraProfile;
            Profile?.Validate();
            ApplyCameraProfile();
            previousLock = Cursor.lockState;
            previousCursor = Cursor.visible;
            capturedCursor = true;
            initialized = true;
        }

        void ApplyCameraProfile()
        {
            try { if (Profile) Profile.Validate(); }
            catch (ArgumentException error)
            {
                if (profileError != error.Message) Debug.LogWarning("Wordbound camera profile: " + error.Message, this);
                profileError = error.Message;
                return;
            }
            profileError = null;
            view.fieldOfView = Profile ? Profile.FieldOfView : ShoulderView.FieldOfView;
            view.nearClipPlane = Profile ? Profile.NearClip : ShoulderView.NearClip;
            view.farClipPlane = Profile ? Profile.FarClip : 1800f;
            followDistance = Profile ? Profile.Distance : ShoulderView.Distance;
            minimumDistance = Profile ? Profile.MinDistance : ShoulderView.MinDistance;
            pivotHeight = Profile ? Profile.PivotHeight : ShoulderView.PivotHeight;
            lift = Profile ? Profile.Lift : ShoulderView.Lift;
            probeRadius = Profile ? Profile.ProbeRadius : ShoulderView.ProbeRadius;
            skin = Profile ? Profile.Skin : ShoulderView.Skin;
            outEase = Profile ? Profile.OutEase : ShoulderView.OutEase;
            minimumPitch = Profile ? Profile.MinPitch : ShoulderView.MinPitch;
            maximumPitch = Profile ? Profile.MaxPitch : ShoulderView.MaxPitch;
            sensitivity = Profile ? Profile.MouseSensitivity : ShoulderView.MouseSensitivity;
            stickYaw = Profile ? Profile.StickYawSpeed : 2.6f;
            stickPitch = Profile ? Profile.StickPitchSpeed : 1.8f;
            if (activeProfile != Profile)
            {
                pitch = Profile ? Profile.DefaultPitch : ShoulderView.DefaultPitch;
                activeProfile = Profile;
                placed = false;
            }
            pitch = Mathf.Clamp(pitch, minimumPitch, maximumPitch);
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
            (binding as WordboundInputBinding)?.SuspendGameplay();
            if (open) CommandsRead?.Invoke(default);
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
            ApplyCameraProfile();
            ProcessInput(Application.isFocused);
        }

        void ProcessInput(bool focused)
        {
            if (binding is WordboundInputBinding automatic)
            {
                bool lost = automatic.RefreshDevice(focused);
                if (bindingRevision != automatic.Revision)
                {
                    bindingRevision = automatic.Revision;
                    movement = Vector2.zero;
                    jumpPending = false;
                    CommandsRead?.Invoke(default);
                    if (MenuOpen && !lost) MenuRequested?.Invoke();
                    ApplyCursor();
                }
                if (lost) { SetMenuOpen(true); MenuRequested?.Invoke(); }
            }
            if (!focused)
            {
                movement = Vector2.zero;
                jumpPending = false;
                (binding as WordboundInputBinding)?.SuspendGameplay();
                return;
            }
            bool pause = binding is DesktopBinding desktop ? desktop.Pause.WasPressedThisFrame() : binding.StartPressed();
            if (pause)
            {
                SetMenuOpen(!MenuOpen);
                MenuRequested?.Invoke();
                return;
            }
            if (MenuOpen)
            {
                movement = Vector2.zero;
                jumpPending = false;
                if (binding is WordboundInputBinding menuBinding)
                    MenuCommandsRead?.Invoke(menuBinding.ReadMenu(Time.unscaledTime));
                return;
            }
            if (player == null || controller.IsSimulationPaused)
            {
                movement = Vector2.zero;
                jumpPending = false;
                (binding as WordboundInputBinding)?.SuspendGameplay();
                return;
            }
            ApplyCursor();
            var commands = default(PlayerCommands);
            binding.Read(ref commands);
            if (binding.ReadsMouse)
            {
                yaw += commands.lookDelta.x * sensitivity;
                pitch -= commands.lookDelta.y * sensitivity;
            }
            else
            {
                yaw += commands.look.x * stickYaw * Time.unscaledDeltaTime;
                pitch -= commands.look.y * stickPitch * Time.unscaledDeltaTime;
            }
            pitch = Mathf.Clamp(pitch, minimumPitch, maximumPitch);
            yaw = Mathf.Repeat(yaw + Mathf.PI, 2 * Mathf.PI) - Mathf.PI;
            movement = ShoulderView.CameraRelative(Vector2.ClampMagnitude(commands.move, 1), yaw);
            jumpPending |= commands.jump;
            CommandsRead?.Invoke(commands);
        }

        public WordboundMovementInput ReadMovement()
        {
            if (!initialized || MenuOpen || !Application.isFocused || player == null || controller.IsSimulationPaused ||
                DesktopBinding.Typing || KeyBindings.GameplayBlocked)
            {
                jumpPending = false;
                return default;
            }
            var input = new WordboundMovementInput {
                MoveX = movement.x, MoveZ = movement.y, Jump = jumpPending,
                HasFacing = true, FacingYaw = Mathf.Repeat(yaw * Mathf.Rad2Deg + 180f, 360f) - 180f
            };
            jumpPending = false;
            return input;
        }

        void LateUpdate()
        {
            if (!initialized || view == null || player == null || player.Object == null || !player.Object.IsValid) return;
            var visual = player.GetComponent<WordboundGameplayBody>()?.VisualRoot;
            Vector3 feet = visual != null ? visual.position : player.transform.position;
            Vector3 pivot = feet + Vector3.up * (pivotHeight + lift);
            Vector3 forward = ShoulderView.Forward(yaw, pitch);
            float allowed = followDistance;
            var scene = player.gameObject.scene.GetPhysicsScene();
            if (!scene.IsValid() || scene == Physics.defaultPhysicsScene) return;
            int count = scene.SphereCast(pivot, probeRadius, -forward, obstructions,
                followDistance, ~0, QueryTriggerInteraction.Ignore);
            if (count == obstructions.Length) allowed = minimumDistance;
            else
                for (int i = 0; i < count; i++)
                {
                    var collider = obstructions[i].collider;
                    if (collider == null || collider.attachedRigidbody == player.Rigidbody) continue;
                    allowed = Mathf.Min(allowed, obstructions[i].distance - skin);
                }
            allowed = Mathf.Clamp(allowed, minimumDistance, followDistance);
            distance = !placed || allowed < distance ? allowed :
                Mathf.Lerp(distance, allowed, 1 - Mathf.Exp(-outEase * Time.unscaledDeltaTime));
            view.transform.SetPositionAndRotation(pivot - forward * distance, Quaternion.LookRotation(forward, Vector3.up));
            placed = true;
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && initialized) { SetMenuOpen(true); MenuRequested?.Invoke(); }
        }

        void OnDisable()
        {
            movement = Vector2.zero;
            jumpPending = false;
            (binding as WordboundInputBinding)?.SuspendGameplay();
            CommandsRead?.Invoke(default);
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
