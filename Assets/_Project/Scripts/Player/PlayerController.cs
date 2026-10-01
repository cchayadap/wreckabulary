using TMPro;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>
    /// Physics-driven movement for a blobby roommate. The body stays upright; a separate visual
    /// leans, squashes and bobs so it feels floppy (the roadmap's fallback for full active ragdoll).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(-50)] // read input before combat and spelling run
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] float moveSpeed = 6.5f;
        [SerializeField] float acceleration = 50f;
        [SerializeField] float turnSpeed = 900f;
        [SerializeField] float extraGravity = 14f;
        [Tooltip("Speed while downed, as a share of normal speed.")]
        [SerializeField] float crawlSpeed = 0.25f;
        [Tooltip("After a knock, footing stays loose at least this long so the push carries.")]
        [SerializeField] float minSlide = 0.25f;

        [Header("Jump and dodge (height, distance and timing are in rules.json)")]
        [Tooltip("A jump or dodge pressed this long before it's possible still happens, e.g. just before landing.")]
        [SerializeField] float pressBuffer = 0.12f;
        [Tooltip("A jump still works this long after walking off an edge.")]
        [SerializeField] float coyoteTime = 0.1f;
        [Tooltip("Mouse aim points at the ground plane this high above the feet.")]
        [SerializeField] float aimHeight = 0.8f;

        [Header("Rig")]
        public Transform visual;
        public Transform handL, handR, holdPoint, overheadPoint;
        public Renderer[] bodyRenderers;
        public Renderer[] sweaterRenderers;
        public TextMeshPro initialLabel;

        public int Index { get; private set; }
        /// <summary>Players on the same team can't hurt each other when friendly fire is off. Each player is their own team unless a mode pairs them.</summary>
        public int Team { get; set; }
        public Color Color { get; private set; } = Color.white;
        public string Name { get; set; } = "P1";
        public char Initial { get; private set; } = 'W';
        public InputBinding Binding { get; set; }
        public PlayerCommands Commands;

        public Rigidbody Body { get; private set; }
        public PlayerHealth Health { get; private set; }
        public LetterInventory Inventory { get; private set; }
        public PlayerCombat Combat { get; private set; }
        public Summoner Summoner { get; private set; }

        public Vector3 Facing { get; private set; } = Vector3.forward;
        /// <summary>Down or wrecked: can't act, can't be targeted.</summary>
        public bool IsKnockedOut => Health && !Health.IsAlive;
        public bool IsDowned => Health && Health.IsDowned;
        public bool IsEliminated => Health && Health.IsEliminated;
        public bool IsHeld { get; private set; }
        public bool Frozen { get; set; }
        public bool Grounded { get; private set; }
        public float MoveScale { get; set; } = 1f;
        public bool IsStaggered => Time.time < staggerUntil;
        public bool CanAct => !Frozen && !IsKnockedOut && !IsHeld && !IsStaggered;
        /// <summary>Mid-dash: attacks, grabs and blocks wait until it ends.</summary>
        public bool IsDodging => Time.time < dodgeUntil;
        public Vector3 OverheadPosition => transform.position + Vector3.up * 2.1f;

        public event System.Action<PlayerController> Jumped, Dodged;

        float staggerUntil, slideUntil, boostUntil, boost = 1f, slipperyUntil, floatyUntil, autoWalkUntil;
        float jumpWantedUntil, dodgeWantedUntil, dodgeUntil, dodgeSpeed;
        float lastJumpAt = float.NegativeInfinity, lastGroundedAt = float.NegativeInfinity;
        Vector3 dodgeDirection;
        bool tumbling;
        Vector3 autoWalk;
        Collider[] colliders;
        Transform homeParent;

        // Visual wobble state
        Vector3 lean, leanVel;
        float squash, squashVel, walkCycle, punchT;
        bool punchRight;
        Vector3 handLRest, handRRest;
        bool wasGrounded;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Health = GetComponent<PlayerHealth>();
            Inventory = GetComponent<LetterInventory>();
            Combat = GetComponent<PlayerCombat>();
            Summoner = GetComponent<Summoner>();
            colliders = GetComponents<Collider>();
            if (handL) handLRest = handL.localPosition;
            if (handR) handRRest = handR.localPosition;
        }

        void OnEnable() => World.Players.Add(this);
        void OnDisable() => World.Players.Remove(this);

        public void Setup(int index, InputBinding binding) =>
            Setup(index, binding, GameAssets.I.PlayerColor(index), GameAssets.I.PlayerInitial(index), $"P{index + 1}");

        public void Setup(int index, InputBinding binding, Color color, char initial, string displayName)
        {
            Index = index;
            Team = index;
            Binding = binding;
            Name = displayName;
            Initial = initial;
            name = $"Player {displayName}";
            Color = color;
            var sweater = Color.Lerp(Color, new Color(0.2f, 0.12f, 0.1f), 0.35f);
            foreach (var r in bodyRenderers) r.material.color = Color;
            foreach (var r in sweaterRenderers) r.material.color = sweater;
            if (initialLabel)
            {
                initialLabel.text = initial.ToString();
                initialLabel.color = new Color(0.97f, 0.92f, 0.82f);
            }
            if (Health) Health.Init();
            // Existing prefabs gain the supplied avatar and saved wardrobe at runtime as well.
            var appearance = GetComponent<PlayerAppearance>() ?? gameObject.AddComponent<PlayerAppearance>();
            appearance.Initialize(this);
            var feedback = GetComponent<PlayerFeedback>() ?? gameObject.AddComponent<PlayerFeedback>();
            feedback.Initialize(this);
        }

        void Update()
        {
            if (Binding != null)
            {
                Commands = default;
                Binding.Read(ref Commands);
                if (Binding is not TouchBinding && TouchBinding.Shared.IsOverlayFor(Binding.Id))
                    TouchBinding.Shared.Merge(ref Commands);
            }
            // Physics steps on its own clock, so hold on to a press until a step can act on it.
            if (Commands.jump) jumpWantedUntil = Time.time + pressBuffer;
            if (Commands.dodge) dodgeWantedUntil = Time.time + pressBuffer;
            AnimateRig();
        }

        void FixedUpdate()
        {
            Grounded = Physics.CheckSphere(transform.position + Vector3.up * 0.3f, 0.36f, World.GroundMask, QueryTriggerInteraction.Ignore);
            if (Grounded) lastGroundedAt = Time.time;
            if (IsEliminated || IsHeld) return;

            if (Time.time < dodgeWantedUntil) TryDodge();
            if (Time.time < jumpWantedUntil) TryJump();

            if (!Grounded)
            {
                // Wings give a floaty glide, otherwise fall a bit faster than default gravity for snappy hops.
                float g = Time.time < floatyUntil ? -6f : extraGravity;
                Body.AddForce(Vector3.down * g, ForceMode.Acceleration);
            }

            var v = Body.linearVelocity;
            if (IsDodging)
            {
                Body.linearVelocity = new Vector3(dodgeDirection.x * dodgeSpeed, v.y, dodgeDirection.z * dodgeSpeed);
                return;
            }
            if (dodgeUntil > 0f)
            {
                // Come out of the dash at running speed rather than sailing on.
                dodgeUntil = 0f;
                var kept = Vector3.ClampMagnitude(new Vector3(v.x, 0f, v.z), moveSpeed);
                v = new Vector3(kept.x, v.y, kept.z);
            }

            bool rooted = Frozen || IsReviving;
            var input = Time.time < autoWalkUntil ? autoWalk
                      : rooted ? Vector3.zero
                      : new Vector3(Commands.move.x, 0f, Commands.move.y);
            var shield = Health ? Health.RaisedShield : null;
            float speed = moveSpeed * MoveScale * (Time.time < boostUntil ? boost : 1f) * (IsDowned ? crawlSpeed : 1f)
                        * (shield != null ? shield.MoveSpeedMultiplier : 1f);
            float accel = acceleration
                        * (IsStaggered || Time.time < slideUntil ? 0.1f : 1f)
                        * (Time.time < slipperyUntil ? 0.1f : 1f)
                        * (Grounded ? 1f : 0.35f);

            var h = Vector3.MoveTowards(new Vector3(v.x, 0f, v.z), input * speed, accel * Time.fixedDeltaTime);
            Body.linearVelocity = new Vector3(h.x, v.y, h.z);

            // Face where the mouse or the right stick aims, otherwise the way you're walking.
            var aim = AimDirection();
            var turnTo = aim.sqrMagnitude > 0f ? aim : input;
            if (turnTo.sqrMagnitude > 0.01f && !IsStaggered)
                Facing = Vector3.RotateTowards(Facing, turnTo.normalized, turnSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
        }

        bool IsReviving => Combat && Combat.IsReviving;

        /// <summary>Flat direction the right stick or the mouse points; zero when not aiming.</summary>
        Vector3 AimDirection()
        {
            if (!CanAct || IsReviving || Time.time < autoWalkUntil) return Vector3.zero;
            var look = Commands.look;
            if (look.sqrMagnitude > 0.01f) return new Vector3(look.x, 0f, look.y);
            if (!Commands.aimAtPointer) return Vector3.zero;

            var cam = Camera.main;
            if (!cam) return Vector3.zero;
            var ray = cam.ScreenPointToRay(Commands.pointer);
            var plane = new Plane(Vector3.up, transform.position + Vector3.up * aimHeight);
            if (!plane.Raycast(ray, out float distance)) return Vector3.zero;
            var to = World.Flat(ray.GetPoint(distance) - transform.position);
            return to.sqrMagnitude > 0.04f ? to : Vector3.zero;
        }

        /// <summary>Jump height for the default gravity plus the extra fall gravity, from rules.json.</summary>
        float JumpSpeed => Mathf.Sqrt(2f * (-Physics.gravity.y + extraGravity) * (Health ? Health.Rules.JumpHeight : 1.1f));

        void TryJump()
        {
            // Coyote time covers stepping off a ledge; the gap stops the step after take-off counting as ground.
            if (!CanAct || IsDodging || IsReviving || Time.time - lastGroundedAt > coyoteTime || Time.time - lastJumpAt < 0.25f) return;
            var v = Body.linearVelocity;
            Body.linearVelocity = new Vector3(v.x, JumpSpeed, v.z);
            lastJumpAt = Time.time;
            lastGroundedAt = float.NegativeInfinity;
            jumpWantedUntil = 0f;
            Grounded = false;
            squashVel = 6f;
            Jumped?.Invoke(this);
        }

        /// <summary>A low dash the way you're moving (or facing), invulnerable for its first moments (rules.json).</summary>
        void TryDodge()
        {
            if (!CanAct || IsDodging || IsReviving || !Health || !Health.Dodge()) return;
            var rules = Health.Rules;
            var input = new Vector3(Commands.move.x, 0f, Commands.move.y);
            dodgeDirection = input.sqrMagnitude > 0.01f ? input.normalized : Facing;
            dodgeSpeed = rules.DodgeDistance / rules.DodgeSeconds;
            dodgeUntil = Time.time + rules.DodgeSeconds;
            dodgeWantedUntil = 0f;
            squashVel = -5f;
            Dodged?.Invoke(this);
        }

        // ---- Things other systems do to the player ----

        /// <summary>
        /// Adds a velocity kick. Footing stays loose for a moment so the push carries, and for
        /// <paramref name="stagger"/> seconds the player can't act (hit-stun).
        /// </summary>
        public void Knock(Vector3 velocityChange, float stagger)
        {
            dodgeUntil = 0f; // a hit that lands ends the dash, so the push isn't overwritten
            Body.linearVelocity += velocityChange;
            staggerUntil = Mathf.Max(staggerUntil, Time.time + stagger);
            slideUntil = Mathf.Max(slideUntil, Time.time + Mathf.Max(stagger, minSlide));
            squashVel = -7f;
        }

        public void Stun(float seconds) => staggerUntil = Mathf.Max(staggerUntil, Time.time + seconds);

        public void Launch(Vector3 velocity)
        {
            dodgeUntil = 0f;
            Body.linearVelocity = velocity;
            Grounded = false;
            squashVel = 6f;
        }

        public void Boost(float multiplier, float seconds) { boost = multiplier; boostUntil = Time.time + seconds; }
        public void MakeSlippery(float seconds) => slipperyUntil = Time.time + seconds;
        public void MakeFloaty(float seconds) => floatyUntil = Time.time + seconds;

        /// <summary>Walks on its own for a moment, e.g. through the front door on arrival.</summary>
        public void AutoWalk(Vector3 direction, float seconds)
        {
            autoWalk = World.Flat(direction).normalized;
            autoWalkUntil = Time.time + seconds;
        }

        public void FaceTowards(Vector3 dir)
        {
            dir = World.Flat(dir);
            if (dir.sqrMagnitude > 0.001f) Facing = dir.normalized;
        }

        /// <summary>
        /// Matches the body to the health state: wrecked players tumble over, downed players stay
        /// upright and crawl (the rig leans them over), everyone else stands.
        /// </summary>
        public void ApplyLifeState(LifeState state, Vector3 push = default)
        {
            if (state == LifeState.Eliminated)
            {
                if (tumbling) return;
                tumbling = true;
                Body.constraints = RigidbodyConstraints.None;
                var axis = Vector3.Cross(Vector3.up, push.sqrMagnitude > 0.01f ? push.normalized : Random.onUnitSphere);
                Body.AddTorque(axis * 10f + Random.insideUnitSphere * 3f, ForceMode.VelocityChange);
                Body.linearVelocity += push * 0.5f + Vector3.up * 3f;
                return;
            }
            tumbling = false;
            Body.constraints = RigidbodyConstraints.FreezeRotation;
            if (IsHeld) return;
            Body.angularVelocity = Vector3.zero;
            transform.rotation = Quaternion.identity;
            Body.rotation = Quaternion.identity;
        }

        /// <summary>Picked up (or put down) by another player.</summary>
        public void SetHeld(bool held, Transform at = null)
        {
            if (held == IsHeld) return;
            IsHeld = held;
            Body.isKinematic = held;
            foreach (var c in colliders) c.enabled = !held;
            if (held)
            {
                homeParent = transform.parent;
                transform.SetParent(at, false);
                transform.localPosition = Vector3.down * 0.3f;
                transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            else
            {
                transform.SetParent(homeParent, true);
                if (!IsEliminated)
                {
                    transform.rotation = Quaternion.identity;
                    Body.rotation = Quaternion.identity;
                }
            }
        }

        /// <summary>Puts the player back on their feet for a new round or after a lobby knockout.</summary>
        public void Respawn(Vector3 position)
        {
            SetHeld(false);
            tumbling = false;
            ApplyLifeState(Health ? Health.State : LifeState.Alive);
            transform.position = position;
            Body.position = position;
            Body.linearVelocity = Vector3.zero;
            staggerUntil = slideUntil = boostUntil = slipperyUntil = floatyUntil = autoWalkUntil = 0f;
            jumpWantedUntil = dodgeWantedUntil = dodgeUntil = 0f;
            lastJumpAt = float.NegativeInfinity;
            MoveScale = 1f;
            FaceTowards(-position);
        }

        public void PlayPunch()
        {
            GameFeedback.Play(GameCue.Swing);
            punchT = 1f;
            punchRight = !punchRight;
        }

        // ---- Visual wobble ----

        void AnimateRig()
        {
            if (!visual) return;
            float dt = Time.deltaTime;
            var v = Body.linearVelocity;
            var hv = new Vector3(v.x, 0f, v.z);

            if (Grounded && !wasGrounded) squashVel = -4f;
            wasGrounded = Grounded;

            if (!IsEliminated && !IsHeld)
            {
                var leanTarget = Vector3.ClampMagnitude(hv * 2.4f, 16f);
                lean = Vector3.SmoothDamp(lean, leanTarget, ref leanVel, 0.12f);
                var tilt = lean.sqrMagnitude > 0.0001f
                    ? Quaternion.AngleAxis(lean.magnitude, Vector3.Cross(Vector3.up, lean.normalized))
                    : Quaternion.identity;
                // Downed players lie forward and crawl. A dodge ducks low into the dash.
                var down = IsDowned ? Quaternion.Euler(70f, 0f, 0f) : Quaternion.identity;
                var dash = IsDodging ? Quaternion.AngleAxis(20f, Vector3.Cross(Vector3.up, dodgeDirection)) : Quaternion.identity;
                visual.rotation = dash * tilt * Quaternion.LookRotation(Facing) * down;
            }

            squashVel += (-squash * 180f - squashVel * 10f) * dt;
            squash += squashVel * dt;
            visual.localScale = new Vector3(1f - squash * 0.5f, 1f + squash, 1f - squash * 0.5f);

            float speed01 = Mathf.Clamp01(hv.magnitude / moveSpeed);
            walkCycle += hv.magnitude * dt * 2.2f;
            visual.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(walkCycle)) * 0.07f * speed01);

            if (!handL || !handR) return;
            punchT = Mathf.MoveTowards(punchT, 0f, dt * 5f);
            float jab = Mathf.Sin(punchT * Mathf.PI) * 0.55f;
            bool holding = Combat && Combat.IsHolding;
            var restL = holding ? new Vector3(-0.28f, 0.95f, 0.45f) : handLRest;
            var restR = holding ? new Vector3(0.28f, 0.95f, 0.45f) : handRRest;
            // A raised PLATE sits square in front of the chest.
            if (Combat && Combat.IsBlocking) restR = new Vector3(0.06f, 0.72f, 0.5f);
            if (IsReviving)
            {
                // Both hands pump on the teammate on the floor.
                float pump = Mathf.Abs(Mathf.Sin(Time.time * 9f)) * 0.12f;
                restL = new Vector3(-0.14f, 0.5f - pump, 0.55f);
                restR = new Vector3(0.14f, 0.5f - pump, 0.55f);
            }
            float swing = Mathf.Sin(walkCycle) * 0.14f * speed01;
            handL.localPosition = restL + Vector3.forward * (swing + (!punchRight ? jab : 0f)) + (!punchRight ? Vector3.up * jab * 0.4f : Vector3.zero);
            handR.localPosition = restR + Vector3.forward * (-swing + (punchRight ? jab : 0f)) + (punchRight ? Vector3.up * jab * 0.4f : Vector3.zero);
        }
    }
}
