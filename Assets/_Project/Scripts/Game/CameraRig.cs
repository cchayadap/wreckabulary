using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Rules;
using System.Linq;

namespace Wreckabulary
{
    [DefaultExecutionOrder(-60)]
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }

        [SerializeField] float follow = 0.12f;
        [SerializeField] float soloDistance = .5f;
        [SerializeField] float soloFieldOfView = 40f;

        const float ThirdPersonShake = .3f;
        const float FadeHold = .15f;

        Vector3 basePosition, smooth;
        Quaternion baseRotation;
        bool baseOrthographic;
        float baseFieldOfView, baseNearClip;
        float shake;
        bool framesLayout;
        Vector3 layoutCentre;
        float wholeHouseSize;
        float layoutWidth, layoutDepth;
        Camera lens;

        readonly ShoulderView view = new();
        PlayerController target, followed;
        Vector3 lastFeet;
        readonly RaycastHit[] blockers = new RaycastHit[16];
        readonly Dictionary<Renderer, (ShadowCastingMode mode, float until)> faded = new();
        readonly List<Renderer> unfade = new();

        public PlayerController Target => target;
        public bool IsThirdPerson => target;
        public float ViewDistance => view.CurrentDistance;
        public IReadOnlyCollection<Renderer> Faded => faded.Keys;

        public static bool IsHuman(PlayerController p) =>
            p && p.Binding != null && p.Binding is not BotBinding && p.Binding is not ScriptedBinding;

        public void Follow(PlayerController player) => followed = player;

        public void FrameLayout(HouseLayout layout)
        {
            var camera = lens = GetComponent<Camera>();
            float minX = layout.Rooms.Min(r => r.MinX), maxX = layout.Rooms.Max(r => r.MaxX);
            float minZ = layout.Rooms.Min(r => r.MinZ), maxZ = layout.Rooms.Max(r => r.MaxZ);
            var centre = new Vector3((minX + maxX) * .5f, 0f, (minZ + maxZ) * .5f);
            layoutWidth = maxX - minX;
            layoutDepth = maxZ - minZ;
            camera.orthographic = true;
            float aspect = Mathf.Max(.5f, camera.aspect);
            camera.orthographicSize = HouseSize(aspect);
            layoutCentre = centre;
            wholeHouseSize = camera.orthographicSize;
            basePosition = smooth = centre + new Vector3(0f, 30f, -22f);
            if (!target)
            {
                transform.position = basePosition;
                transform.LookAt(centre);
            }
            baseRotation = Quaternion.LookRotation(centre - basePosition);
            baseOrthographic = true;
            framesLayout = true;
        }

        void Awake()
        {
            Instance = this;
            basePosition = smooth = transform.position;
            baseRotation = transform.rotation;
            lens = GetComponent<Camera>();
            if (lens)
            {
                baseOrthographic = lens.orthographic;
                baseFieldOfView = lens.fieldOfView;
                baseNearClip = lens.nearClipPlane;
                lens.clearFlags = CameraClearFlags.SolidColor;
                lens.backgroundColor = new Color32(0x17, 0x3a, 0x3d, 0xff);
            }
            GraphicsOptions.ApplyTo(lens);
        }

        void OnDisable()
        {
            if (target) target.ShooterView = false;
            target = null;
            RestoreFaded();
            CursorPolicy.Apply(false);
        }

        float HouseSize(float aspect) => Mathf.Max(layoutWidth / aspect, layoutDepth * .85f) * .5f + .8f;

        public static void Shake(float amount)
        {
            if (Instance) Instance.shake = Mathf.Max(Instance.shake, amount);
        }

        PlayerController ChooseTarget()
        {
            if (followed && followed.isActiveAndEnabled) return followed;
            PlayerController only = null;
            foreach (var p in World.Players)
            {
                if (!IsHuman(p)) continue;
                if (only) return null;
                only = p;
            }
            return only && only.Binding.CanLook ? only : null;
        }

        void SetTarget(PlayerController next)
        {
            bool wasThirdPerson = target;
            if (target) target.ShooterView = false;
            target = next;
            if (target)
            {
                target.ShooterView = true;
                target.ResetLook();
                lastFeet = target.transform.position;
                view.Snap();
            }
            else
            {
                RestoreFaded();
                CursorPolicy.Apply(false);
                if (wasThirdPerson && lens)
                {
                    smooth = transform.position;
                    transform.rotation = baseRotation;
                    lens.orthographic = baseOrthographic;
                    lens.fieldOfView = baseFieldOfView;
                    lens.nearClipPlane = baseNearClip;
                }
            }
            var rooms = FindAnyObjectByType<RoomBuilder>();
            if (rooms) { rooms.SetTallWalls(target); RoomBuilder.ApplyFog(target); }
            if (StoreyCutaway.Instance) StoreyCutaway.Instance.Refresh();
        }

        void LateUpdate()
        {
            var next = ChooseTarget();
            if (next != target) SetTarget(next);
            if (target) FollowTarget();
            else Overview();
        }

        void FollowTarget()
        {
            if (!target.IsEliminated) lastFeet = target.transform.position;
            float dt = Time.unscaledDeltaTime;
            if (lens) view.Place(lens, lastFeet, target.LookYaw, target.LookPitch, dt, !target.IsEliminated && target.Commands.aimHeld);
            if (shake > 0f)
            {
                var t = transform;
                t.position += (t.right * (Random.value - .5f) + t.up * (Random.value - .5f)) * (shake * ThirdPersonShake);
            }
            shake = Mathf.MoveTowards(shake, 0f, dt * 1.2f);
            FadeBlockers();
            var hud = GameHud.Active;
            bool mouse = target.Binding != null && target.Binding.ReadsMouse;
            CursorPolicy.Apply(CursorPolicy.WantsLock(mouse, hud && hud.NeedsPointer, Application.isFocused));
        }

        void FadeBlockers()
        {
            float now = Time.unscaledTime;
            var from = transform.position;
            var to = lastFeet + Vector3.up * .9f - from;
            float length = to.magnitude;
            if (length > .01f)
            {
                int n = Physics.RaycastNonAlloc(from, to / length, blockers, length, World.GroundMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    var body = blockers[i].rigidbody;
                    if (!body || body.GetComponentInParent<PlayerController>()) continue;
                    foreach (var r in body.GetComponentsInChildren<Renderer>())
                    {
                        if (faded.TryGetValue(r, out var was)) faded[r] = (was.mode, now + FadeHold);
                        else
                        {
                            faded[r] = (r.shadowCastingMode, now + FadeHold);
                            r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                        }
                    }
                }
            }
            unfade.Clear();
            foreach (var pair in faded)
                if (pair.Value.until < now) unfade.Add(pair.Key);
            foreach (var r in unfade)
            {
                if (r) r.shadowCastingMode = faded[r].mode;
                faded.Remove(r);
            }
        }

        void RestoreFaded()
        {
            foreach (var pair in faded)
                if (pair.Key) pair.Key.shadowCastingMode = pair.Value.mode;
            faded.Clear();
        }

        void Overview()
        {
            CursorPolicy.Apply(false);
            var centre = Vector3.zero;
            int n = 0;
            int localCount = 0;
            PlayerController localPlayer = null;
            Vector3 low = Vector3.positiveInfinity, high = Vector3.negativeInfinity;
            foreach (var p in World.Players)
            {
                if (!p || p.IsEliminated) continue;
                centre += p.transform.position;
                n++;
                if (p.Binding != null && p.Binding is not BotBinding)
                {
                    localCount++; localPlayer = p;
                    low = Vector3.Min(low, p.transform.position); high = Vector3.Max(high, p.transform.position);
                }
            }
            var goal = basePosition;
            if (framesLayout)
            {
                bool followLocal = localCount == 1;
                float aspect = lens ? Mathf.Max(.5f, lens.aspect) : 16f / 9f;
                wholeHouseSize = HouseSize(aspect);
                var focus = followLocal ? localPlayer.transform.position : layoutCentre;
                float size = wholeHouseSize;
                if (localCount > 1)
                {
                    var spread = high - low;
                    size = Mathf.Clamp(Mathf.Max((spread.x + 9f) / aspect, (spread.z + 7f) * .85f) * .5f + 1f, 7.5f, wholeHouseSize);
                    var middle = (low + high) * .5f;
                    focus = Vector3.Lerp(middle, layoutCentre, Mathf.InverseLerp(wholeHouseSize * .7f, wholeHouseSize, size));
                }
                var cutaway = StoreyCutaway.Instance;
                focus.y = cutaway ? cutaway.FocusY : 0f;
                goal = focus + new Vector3(0f, 30f, -22f) * (followLocal ? soloDistance : 1f);
                if (lens)
                {
                    lens.orthographic = !followLocal;
                    if (followLocal) lens.fieldOfView = soloFieldOfView;
                    lens.orthographicSize = Mathf.Lerp(lens.orthographicSize, size,
                        1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
                }
            }
            else if (n > 0)
            {
                centre /= n;
                goal += new Vector3(centre.x * follow, 0f, centre.z * follow * 0.6f);
            }
            smooth = Vector3.Lerp(smooth, goal, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
            transform.position = smooth + Random.insideUnitSphere * shake;
            shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 1.5f);
        }
    }
}
