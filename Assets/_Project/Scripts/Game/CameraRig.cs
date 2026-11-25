using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Rules;
using System.Linq;

namespace Wreckabulary
{
    [DefaultExecutionOrder(-60)]
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }

        [SerializeField] float follow = 0.12f;
        [SerializeField] float soloDistance = .5f;
        [SerializeField] float soloFieldOfView = 40f;

        const float ThirdPersonShake = .3f;
        const float FadeHold = .15f;
        static readonly Vector3 Overhead = new(0f, PartyCamera.Rise, -PartyCamera.Back);

        Vector3 basePosition, smooth;
        Quaternion baseRotation;
        bool baseOrthographic;
        float baseFieldOfView, baseNearClip;
        float shake;
        bool framesLayout;
        PartyArea area;
        PartyView party;
        float boom = 1f;
        readonly List<PartySeat> seats = new(4);
        Camera lens;
        CameraCutaway visibility;
        StoreyCutaway house;

        readonly ShoulderView view = new();
        PlayerController target, followed;
        Vector3 lastFeet;
        readonly RaycastHit[] blockers = new RaycastHit[16];
        readonly Dictionary<Renderer, float> faded = new();
        readonly List<Renderer> occluding = new();
        readonly List<Renderer> unfade = new();
        readonly Dictionary<Renderer, Renderer[]> cutawayParts = new();
        readonly List<Renderer> bodyRenderers = new();
        int leases;
        bool held, orbited;
        float orbitYaw, orbitPitch;
        RoundManager rounds;
        Vector3 pushAt;
        float pushSeconds, pushElapsed;

        public PlayerController Target => target;
        public Camera ViewCamera => lens;
        public PlayerController FollowedPlayer => target;
        public bool IsFollowing(PlayerController player) => target && target == player;
        public static bool TakesLook => Instance && Instance.isActiveAndEnabled;
        public bool Looking => leases > 0 || held;
        public bool Dragging => leases > 0;
        public float OrbitYaw => orbitYaw;
        public float OrbitPitch => orbitPitch;
        bool Orbiting => orbitYaw != 0f || orbitPitch != 0f;
        Vector3 SteadyForward => target ? ShoulderView.Forward(target.LookYaw, 0f) : baseRotation * Vector3.forward;
        public Vector3 PlanarForward => World.Flat(Orbiting ? SteadyForward : transform.forward).normalized;
        public Vector2 ScreenDirectionToWorld(Vector2 input)
        {
            var forward = PlanarForward;
            var world = forward * input.y + Vector3.Cross(Vector3.up, forward) * input.x;
            return new Vector2(world.x, world.z);
        }
        public bool IsThirdPerson => target;
        public bool Pushing => pushSeconds > 0f;
        public float PushAmount => CameraPush.Weight(pushElapsed, pushSeconds);
        public Vector3 PushedToward => pushAt;
        public float ViewDistance => view.CurrentDistance;
        public IReadOnlyCollection<Renderer> Faded => faded.Keys;

        public static bool IsHuman(PlayerController p) =>
            p && p.Binding != null && p.Binding is not BotBinding && p.Binding is not ScriptedBinding;

        public void Follow(PlayerController player) => followed = player;

        public void BeginLook() => leases++;

        public void EndLook() => leases = Mathf.Max(0, leases - 1);

        public void Push(Vector3 toward, float seconds)
        {
            if (!(seconds > 0f)) return;
            pushAt = toward;
            pushSeconds = seconds;
            pushElapsed = 0f;
        }

        void WatchRounds()
        {
            var current = RoundManager.Instance;
            if (ReferenceEquals(current, rounds)) return;
            if (rounds is not null) rounds.RoundWon -= OnRoundWon;
            rounds = current;
            if (rounds is not null) rounds.RoundWon += OnRoundWon;
        }

        void OnRoundWon(PlayerController winner)
        {
            if (winner && rounds && rounds.FinishBeatPlaying) Push(winner.transform.position, RoundManager.BeatSeconds);
        }

        void AdvancePush(float dt)
        {
            if (pushSeconds <= 0f) return;
            pushElapsed += dt;
            if (pushElapsed >= CameraPush.Length(pushSeconds)) pushSeconds = pushElapsed = 0f;
        }

        Vector3 PushOverview(Vector3 at)
        {
            float weight = PushAmount;
            if (weight <= 0f || !framesLayout || !lens) return at;
            var forward = transform.forward;
            var gap = pushAt - new Vector3(party.X, party.Y, party.Z);
            gap -= forward * Vector3.Dot(gap, forward);
            at += gap.normalized * CameraPush.Reach(gap.magnitude, weight);
            if (lens.orthographic) lens.orthographicSize = CameraPush.Narrow(lens.orthographicSize, weight);
            else lens.fieldOfView = CameraPush.Narrow(lens.fieldOfView, weight);
            return at;
        }

        void PushShoulder()
        {
            float weight = PushAmount;
            if (weight > 0f && lens) lens.fieldOfView = CameraPush.Narrow(lens.fieldOfView, weight);
        }

        public void Orbit(float yawDegrees, float pitchDegrees)
        {
            if (!float.IsFinite(yawDegrees) || !float.IsFinite(pitchDegrees)) return;
            orbitYaw = FreeLook.Wrap(orbitYaw + yawDegrees);
            orbitPitch = ClampPitch(orbitPitch + pitchDegrees);
        }

        public void OrbitDrag(Vector2 screenPixels) =>
            Orbit(DragDegrees(screenPixels.x), DragDegrees(KeyBindings.InvertY ? screenPixels.y : -screenPixels.y));

        public static float DragDegrees(float pixels) => FreeLook.DragDegrees(pixels, Screen.height);

        float ClampPitch(float pitch) => target
            ? FreeLook.ClampPitch(pitch, target.LookPitch * Mathf.Rad2Deg, ShoulderView.MinPitch * Mathf.Rad2Deg, ShoulderView.MaxPitch * Mathf.Rad2Deg)
            : FreeLook.OverheadPitch(pitch);

        void ResetOrbit()
        {
            orbitYaw = orbitPitch = 0f;
            orbited = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void OnEnable() => Instance = this;

        void OnDestroy()
        {
            RestoreFaded();
            if (rounds is not null) rounds.RoundWon -= OnRoundWon;
            rounds = null;
            if (Instance == this) Instance = null;
        }

        public void FrameLayout(HouseLayout layout)
        {
            var camera = lens = GetComponent<Camera>();
            area = new PartyArea(layout.Rooms.Min(r => r.MinX), layout.Rooms.Max(r => r.MaxX),
                layout.Rooms.Min(r => r.MinZ), layout.Rooms.Max(r => r.MaxZ));
            var centre = new Vector3(area.CentreX, 0f, area.CentreZ);
            camera.orthographic = true;
            camera.orthographicSize = PartyCamera.HouseSize(area.Width, area.Depth, camera.aspect);
            party = PartyView.At(centre.x, 0f, centre.z, camera.orthographicSize);
            boom = 1f;
            basePosition = smooth = centre + Overhead;
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
                if (EnvironmentLighting.Active) EnvironmentLighting.Active.ApplyCamera(lens);
                else
                {
                    lens.clearFlags = CameraClearFlags.SolidColor;
                    lens.backgroundColor = new Color32(0x17, 0x3a, 0x3d, 0xff);
                }
            }
            if (!TryGetComponent(out visibility)) visibility = gameObject.AddComponent<CameraCutaway>();
            visibility.SetOccluders(occluding);
            house = StoreyCutaway.Instance;
            GraphicsOptions.ApplyTo(lens);
        }

        void OnDisable()
        {
            if (target) target.ShooterView = false;
            target = null;
            leases = 0;
            held = false;
            pushSeconds = pushElapsed = 0f;
            ResetOrbit();
            RestoreFaded();
            if (visibility) visibility.ClearView(this);
            CursorPolicy.Apply(false);
        }

        public static void Shake(float amount)
        {
            if (Instance) Instance.shake = Mathf.Max(Instance.shake, amount * ScreenShake.Scale);
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
            ResetOrbit();
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
                    party = PartyView.At(lastFeet.x, party.Y, lastFeet.z, party.Size);
                }
            }
            RoomBuilder.ApplyFog(target);
            if (!house) house = StoreyCutaway.Instance;
            if (house) house.Refresh();
        }

        void LateUpdate()
        {
            if (Time.timeScale <= 0f)
            {
                CursorPolicy.Apply(false);
                return;
            }
            WatchRounds();
            AdvancePush(Time.unscaledDeltaTime);
            var next = ChooseTarget();
            if (next != target) SetTarget(next);
            GatherLook(Time.unscaledDeltaTime);
            if (!house) house = StoreyCutaway.Instance;
            visibility.SetView(this, house, target);
            visibility.SetOccluders(occluding);
            view.Environment = house;
            view.Cutaway = visibility;
            if (target) FollowTarget();
            else Overview();
            visibility.RefreshVisibility();
        }

        void GatherLook(float dt)
        {
            held = false;
            if (target) Gather(target);
            else foreach (var player in World.Players) if (IsHuman(player)) Gather(player);
            if (!Looking)
            {
                orbitYaw = FreeLook.Settle(orbitYaw, dt);
                orbitPitch = FreeLook.Settle(orbitPitch, dt);
            }
            orbitPitch = ClampPitch(orbitPitch);
        }

        void Gather(PlayerController player)
        {
            if (!player.Commands.lookAround) return;
            held = true;
            Orbit(player.Commands.orbit.x * Mathf.Rad2Deg, player.Commands.orbit.y * Mathf.Rad2Deg);
        }

        void FollowTarget()
        {
            if (!target.IsEliminated) lastFeet = target.transform.position;
            float dt = Time.unscaledDeltaTime;
            if (lens) view.Place(lens, lastFeet, target.LookYaw + orbitYaw * Mathf.Deg2Rad, target.LookPitch + orbitPitch * Mathf.Deg2Rad,
                dt, !target.IsEliminated && (target.Commands.aimHeld || (target.Combat && target.Combat.IsAiming)));
            PushShoulder();
            if (shake > 0f)
            {
                var t = transform;
                t.position += (t.right * (Random.value - .5f) + t.up * (Random.value - .5f)) * (shake * ThirdPersonShake);
            }
            shake = Mathf.MoveTowards(shake, 0f, dt * 1.2f);
            FadeBlockers();
            var hud = GameHud.Active;
            bool mouse = target.Binding != null && target.Binding.ReadsMouse && !KeyBindings.Busy;
            CursorPolicy.Apply(CursorPolicy.WantsLock(mouse, hud && hud.NeedsPointer, Application.isFocused));
        }

        void FadeBlockers()
        {
            float now = Time.unscaledTime;
            if (target) FindOccluders(lastFeet, now);
            else foreach (var player in World.Players)
                if (player && !player.IsEliminated && player.Binding is not BotBinding) FindOccluders(player.transform.position, now);
            unfade.Clear();
            foreach (var pair in faded) if (pair.Value < now) unfade.Add(pair.Key);
            foreach (var renderer in unfade) faded.Remove(renderer);
            occluding.Clear();
            foreach (var pair in faded) occluding.Add(pair.Key);
        }

        void FindOccluders(Vector3 feet, float now)
        {
            var origin = feet + Vector3.up * .4f;
            var delta = transform.position - origin;
            if (delta.sqrMagnitude < .001f) return;
            int count = Physics.SphereCastNonAlloc(origin, .3f, delta.normalized, blockers,
                delta.magnitude, World.GroundMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var collider = blockers[i].collider;
                if (!collider || collider.GetComponentInParent<PlayerController>()) continue;
                var body = blockers[i].rigidbody;
                if (body)
                {
                    bodyRenderers.Clear();
                    body.GetComponentsInChildren(bodyRenderers);
                    foreach (var renderer in bodyRenderers) faded[renderer] = now + FadeHold;
                    continue;
                }
                if (!collider.GetComponent<TallWall>() && !ShoulderView.IsLegacyCutaway(collider)) continue;
                if (!collider.TryGetComponent<Renderer>(out var wall)) continue;
                var visibleBounds = wall.bounds;
                visibleBounds.Expand(.6f);
                if (!visibleBounds.IntersectRay(new Ray(origin, delta.normalized), out float visibleDistance) || visibleDistance > delta.magnitude) continue;
                if (!cutawayParts.TryGetValue(wall, out var parts))
                {
                    var group = new List<Renderer> { wall };
                    var tall = collider.GetComponent<TallWall>();
                    if (tall && tall.Trim && tall.Trim.TryGetComponent<Renderer>(out var cap)) group.Add(cap);
                    var area = wall.bounds;
                    area.Expand(.18f);
                    if (wall.transform.parent)
                        foreach (Transform child in wall.transform.parent)
                            if (child.TryGetComponent<Renderer>(out var trim) && trim != wall && trim.bounds.size.y < .15f && area.Intersects(trim.bounds) && !group.Contains(trim))
                                group.Add(trim);
                    parts = group.ToArray();
                    cutawayParts.Add(wall, parts);
                }
                foreach (var part in parts) if (part) faded[part] = now + FadeHold;
            }
        }

        void RestoreFaded()
        {
            faded.Clear();
            occluding.Clear();
        }

        void Overview()
        {
            CursorPolicy.Apply(false);
            float dt = Time.unscaledDeltaTime;
            if (framesLayout) smooth = FramePlayers(dt);
            else smooth = Vector3.Lerp(smooth, Drift(), 1f - Mathf.Exp(-3f * dt));
            var at = smooth;
            if (Orbiting && TryPivot(out var pivot)) at = OrbitAround(pivot);
            else if (orbited)
            {
                transform.rotation = baseRotation;
                orbited = false;
            }
            at = PushOverview(at);
            transform.position = at + Random.insideUnitSphere * shake;
            shake = Mathf.MoveTowards(shake, 0f, dt * 1.5f);
            FadeBlockers();
        }

        bool TryPivot(out Vector3 pivot)
        {
            pivot = new Vector3(party.X, party.Y, party.Z);
            if (framesLayout) return true;
            var forward = baseRotation * Vector3.forward;
            if (forward.y > -.1f) return false;
            pivot = smooth + forward * (-smooth.y / forward.y);
            return true;
        }

        Vector3 OrbitAround(Vector3 pivot)
        {
            var offset = smooth - pivot;
            var (x, y, z) = FreeLook.Orbit(offset.x, offset.y, offset.z, orbitYaw, orbitPitch);
            var at = pivot + new Vector3(x, y, z);
            transform.rotation = Quaternion.LookRotation(pivot - at);
            orbited = true;
            if (framesLayout && lens && lens.orthographic) lens.orthographicSize = FreeLook.WidenedSize(party.Size, lens.aspect, orbitYaw);
            return at;
        }

        Vector3 FramePlayers(float dt)
        {
            seats.Clear();
            foreach (var p in World.Players)
            {
                if (!p) continue;
                var at = p.transform.position;
                seats.Add(new PartySeat(at.x, at.y, at.z, p.Binding != null && p.Binding is not BotBinding, p.IsEliminated));
            }
            var cutaway = StoreyCutaway.Instance;
            var shot = PartyCamera.Frame(seats, area, lens ? lens.aspect : 16f / 9f, cutaway ? cutaway.FocusY : 0f, ref party, dt);
            boom = Mathf.Lerp(boom, shot.Solo ? soloDistance : 1f, 1f - Mathf.Exp(-3f * dt));
            if (lens)
            {
                lens.orthographic = !shot.Solo;
                if (shot.Solo) lens.fieldOfView = soloFieldOfView;
                lens.orthographicSize = party.Size;
            }
            return new Vector3(party.X, party.Y, party.Z) + Overhead * boom;
        }

        Vector3 Drift()
        {
            var centre = Vector3.zero;
            int n = 0;
            foreach (var p in World.Players)
            {
                if (!p || p.IsEliminated) continue;
                centre += p.transform.position;
                n++;
            }
            if (n == 0) return basePosition;
            centre /= n;
            return basePosition + new Vector3(centre.x * follow, 0f, centre.z * follow * 0.6f);
        }
    }
}
