using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    [RequireComponent(typeof(PlayerController))]
    [DefaultExecutionOrder(100)]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Grab and throw")]
        [SerializeField] float grabReach = 0.8f;
        [SerializeField] float grabRadius = 0.9f;
        [SerializeField] float maxCarryMass = 6f;
        [SerializeField] float throwSpeed = 13f;
        [Tooltip("A carried player wriggles free after this long.")]
        [SerializeField] float struggleTime = 1.6f;
        [Tooltip("Damage a thrown player takes on release.")]
        [SerializeField] float thrownPlayerDamage = 6f;

        PlayerController controller;
        float nextAttack;
        Rigidbody held;
        PlayerController heldPlayer;
        HeldWeapon weapon;
        HeldWeapon storedGear;
        bool deploying, punching;
        Transform heldHomeParent;
        float heldSince;
        float raiseStartedAt = -1f;
        ClickHold attackHold;
        bool aiming;
        PlayerController reviving;
        readonly Collider[] overlaps = new Collider[48];
        readonly RaycastHit[] obstructions = new RaycastHit[32];
        readonly HashSet<Rigidbody> struck = new();
        sealed class CarryState
        {
            public Transform parent;
            public Collider[] colliders;
            public bool[] enabled;
            public RigidbodyInterpolation interpolation;
        }
        readonly Dictionary<Rigidbody, CarryState> carriedStates = new();
        Vector3 carryLocal, carryCentre, holdGrip;
        Quaternion holdTurn = Quaternion.identity, holdBase = Quaternion.identity, holdWritten = Quaternion.identity;
        float carryRadius, carryHalfWidth, bodyRadius = .42f;
        Vector3 carryAxis = Vector3.right;
        Rigidbody lastTarget;
        public float GrabDistance => grabReach + grabRadius;

        const float PointBlank = 0.25f;
        const float ChestHeight = .8f;
        const float GrabArc = 110f;
        const float HeavyCarryMass = 2.5f;
        const float MinCarryRadius = .08f, MaxCarryRadius = 1.1f;
        const float PlayerFitShrink = .9f, PlayerSpotGap = .1f, PlayerSweepRadius = .3f;
        const int DepenetratePasses = 3;
        public const float DeployReach = 1.6f;
        static int CarryMask => World.GroundMask & Physics.DefaultRaycastLayers;
        static int SpaceMask => World.TileLayer >= 0 ? Physics.DefaultRaycastLayers & ~(1 << World.TileLayer) : Physics.DefaultRaycastLayers;

        public event System.Action<Rigidbody> Thrown;

        public static event System.Action<MeleeContact> Contacted;
        int swingSerial;
        IMeleeContactShaper shaper;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Contacted = null;

        public bool IsHolding => held;
        public Rigidbody Held => held;
        public HeldWeapon Weapon => held ? weapon : null;
        public HeldWeapon StoredGear => storedGear;
        public bool IsDeploying => deploying;
        public bool IsChanneling => deploying || punching || (Weapon && Weapon.IsUsing);
        public bool HasFreeGearSlot => (Weapon ? 1 : 0) + (storedGear ? 1 : 0)
            + (controller.Summoner && controller.Summoner.IsCrafting ? 1 : 0) < controller.Health.Rules.MaxCarried;
        public bool IsBlocking => controller.Health.RaisedShield != null;
        public bool AttackHeld => controller.Commands.attackHeld;
        public bool IsAiming => aiming;
        public bool IsReviving => reviving;
        public PlayerController Reviving => reviving;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            if (TryGetComponent(out CapsuleCollider capsule)) bodyRadius = capsule.radius * Mathf.Abs(transform.lossyScale.x);
            if (!GetComponent<CarryTargetMarker>()) gameObject.AddComponent<CarryTargetMarker>().Initialize(controller);
        }

        void Start()
        {
            var health = controller.Health;
            controller.Dodged += _ => CancelChannels();
            health.Damaged += (_, _, r) =>
            {
                if (r.Blocked) WearShield(r.BlockedDamage);
                if (r.HitStun > 0f) CancelChannels();
                if (r.HitStun > 0f && held && !weapon) Drop();
            };
            health.KnockedOut += _ =>
            {
                CancelChannels();
                StopReviving();
                DropAllGear();
            };
        }

        void CancelChannels()
        {
            StopAllCoroutines();
            deploying = punching = false;
            StopReviving();
            controller.MoveScale = 1f;
            if (Weapon) Weapon.CancelUse();
        }

        void OnDisable() { if (controller) { CancelChannels(); StopReviving(); } }

        void Update()
        {
            if (Time.timeScale <= 0f)
            {
                if (attackHold.Down) attackHold.Cancel(true);
                aiming = false;
                return;
            }
            if (held == null && (heldPlayer || weapon)) ClearHeld();
            if (heldPlayer && !heldPlayer.IsHeld) ClearHeld();
            if (heldPlayer && Time.time - heldSince > struggleTime) Drop();

            var c = controller.Commands;
            bool free = controller.CanAct && !controller.IsDodging && !deploying && !(controller.Summoner && (controller.Summoner.IsSpelling || controller.Summoner.IsCrafting));
            UpdateRevive(free && c.grabHeld);
            var kind = KindInHand();
            var step = ReadAttackHold(c, kind, free && !reviving);
            bool shieldInHand = Weapon && Weapon.Shield != null;
            bool attackRaises = c.attackHeld && shieldInHand && (controller.Binding == null || controller.Binding.HoldingAttackRaisesShield);
            UpdateBlock(free && (c.blockHeld || attackRaises) && !reviving);
            if (!free || reviving) return;

            if (c.drop) Drop();
            if (c.swap) SwitchGear();
            if (c.slot > 0) SelectSlot(c.slot - 1);
            if (c.grab) GrabOrRevive();
            if (AttackFires(c, kind, step) && raiseStartedAt < 0f) Attack();
        }

        bool AimsWithHold => controller.Binding != null && controller.Binding.AimsWithAttackHold;

        public AttackKind KindInHand()
        {
            if (held && !Weapon) return AttackKind.Throw;
            var gear = Weapon;
            if (!gear) return AttackKind.Swing;
            if (gear.Shield != null) return AttackKind.Shield;
            var item = gear.Definition;
            if (item == null || item.Use != null) return AttackKind.Swing;
            if (item.Ranged != null) return AttackKind.Draw;
            return item.Thrown != null ? AttackKind.Throw : AttackKind.Swing;
        }

        PressStep ReadAttackHold(in PlayerCommands c, AttackKind kind, bool allowed)
        {
            if (!AimsWithHold)
            {
                attackHold = default;
                aiming = false;
                return PressStep.None;
            }
            if (!allowed)
            {
                if (attackHold.Down) attackHold.Cancel(c.attackHeld);
                aiming = false;
                return PressStep.None;
            }
            var step = attackHold.Update(c.attackHeld, Time.time);
            aiming = attackHold.Aiming && DesktopScheme.AimsWhileHeld(kind) && (kind != AttackKind.Draw || Weapon.IsDrawing);
            return step;
        }

        bool AttackFires(in PlayerCommands c, AttackKind kind, PressStep step) =>
            AimsWithHold && DesktopScheme.FiresOnRelease(kind) ? DesktopScheme.Fires(kind, step) : c.attack;

        void UpdateBlock(bool wanted)
        {
            var shield = wanted && Weapon ? Weapon.Shield : null;
            if (shield == null)
            {
                if (IsBlocking && Weapon) Weapon.ShowRaised(false);
                raiseStartedAt = -1f;
                controller.Health.RaisedShield = null;
                return;
            }
            if (raiseStartedAt < 0f) raiseStartedAt = Time.time;
            bool up = Time.time - raiseStartedAt >= shield.RaiseSeconds;
            if (up != IsBlocking) Weapon.ShowRaised(up);
            controller.Health.RaisedShield = up ? shield : null;
        }

        void WearShield(float blocked)
        {
            if (Weapon && Weapon.Shield != null) Weapon.Wear(blocked, this);
        }

        public PlayerController DownedTeammateNearby()
        {
            Rigidbody aimed = null;
            bool focused = InteractionAim.TryRay(controller, out var ray);
            if (focused && InteractionAim.Trace(controller, ray, out var hit)) aimed = hit.rigidbody;
            float range = controller.Health.Rules.ReviveRange;
            float bestSq = range * range;
            PlayerController best = null;
            foreach (var p in World.Players)
            {
                if (p == controller || !p.IsDowned || p.IsHeld || !Teams.AreTeammates(p.Team, controller.Team)) continue;
                if (focused && aimed != p.Body) continue;
                float d = (p.transform.position - transform.position).sqrMagnitude;
                if (d > bestSq || !HasClearInteractionPath(p.Body, p.Body.worldCenterOfMass)) continue;
                bestSq = d;
                best = p;
            }
            return best;
        }

        public bool TryRevive()
        {
            if (!controller.CanAct || controller.IsDodging || IsChanneling || (controller.Summoner && (controller.Summoner.IsSpelling || controller.Summoner.IsCrafting))) return false;
            var target = DownedTeammateNearby();
            if (!target || !target.Health.BeginRevive(controller)) return false;
            reviving = target;
            controller.FaceTowards(target.transform.position - transform.position);
            return true;
        }

        void UpdateRevive(bool keepGoing)
        {
            if (!reviving)
            {
                reviving = null;
                return;
            }
            float range = controller.Health.Rules.ReviveRange + 0.3f;
            var to = reviving.transform.position - transform.position;
            if (!keepGoing || !reviving.IsDowned || reviving.IsHeld || to.sqrMagnitude > range * range
                || !HasClearInteractionPath(reviving.Body, reviving.Body.worldCenterOfMass))
            {
                StopReviving();
                return;
            }
            controller.FaceTowards(to);
            if (reviving.Health.TryFinishRevive(controller)) reviving = null;
        }

        void StopReviving()
        {
            if (reviving) reviving.Health.CancelRevive(controller);
            reviving = null;
        }

        public void Attack()
        {
            if (!controller.CanAct || controller.IsDodging || IsChanneling || IsReviving || raiseStartedAt >= 0f
                || (controller.Summoner && (controller.Summoner.IsCrafting || controller.Summoner.IsSpelling)) || Time.time < nextAttack) return;
            if (Weapon && Weapon.Shield == null)
            {
                nextAttack = Time.time + Weapon.Cooldown;
                var job = Weapon.Definition;
                if (job == null || (job.Use == null && job.Thrown == null && job.Deploy == null && job.Ranged == null))
                    controller.PlayPunch(!Audio.ImpactAudioBridge.VoicesSwing(Weapon));
                Weapon.Use(this);
                return;
            }
            if (held && !Weapon) { Throw(); return; }

            var fist = controller.Health.Rules.Unarmed;
            nextAttack = Time.time + fist.Cycle;
            controller.PlayPunch();
            controller.GetComponent<PlayerAppearance>()?.Play("Swing_OneHand", fist.Cycle);
            StartCoroutine(PunchAfterWindup(fist));
        }

        IEnumerator PunchAfterWindup(MeleeStats stats)
        {
            punching = true;
            yield return new WaitForSeconds(stats.Windup);
            float ends = Time.time + stats.Active;
            bool firstFrame = true;
            do
            {
                while (Time.timeScale <= 0f) yield return null;
                if (!controller.CanAct || controller.IsDodging || IsReviving || IsBlocking || (held && !Weapon)) break;
                Strike(stats, null, firstFrame);
                firstFrame = false;
                yield return null;
            } while (Time.time < ends);
            yield return new WaitForSeconds(stats.Recovery);
            punching = false;
        }

        public int Strike(MeleeStats stats, string itemId, bool beginSwing = true)
        {
            var chest = transform.position + Vector3.up * 0.8f;
            var facing = controller.Facing;
            int mask = World.TileLayer >= 0 ? ~(1 << World.TileLayer) : ~0;
            int n = Physics.OverlapSphereNonAlloc(chest, stats.Reach, overlaps, mask, QueryTriggerInteraction.Ignore);
            var hits = overlaps;
            if (n == hits.Length) { hits = Physics.OverlapSphere(chest, stats.Reach, mask, QueryTriggerInteraction.Ignore); n = hits.Length; }
            if (beginSwing)
            {
                struck.Clear();
                swingSerial++;
                shaper = itemId != null && Weapon ? Weapon.GetComponent<IMeleeContactShaper>() : null;
            }
            int playersHit = 0;

            for (int i = 0; i < n; i++)
            {
                var col = hits[i];
                var rb = col.attachedRigidbody;
                if (!rb || rb == controller.Body || rb == held || struck.Contains(rb)) continue;

                var point = ClosestPoint(col, chest);
                var to = World.Flat(point - chest);
                if (to.sqrMagnitude > PointBlank * PointBlank && !Geometry.InFrontArc(facing.x, facing.z, to.x, to.z, stats.ArcDegrees))
                    continue;
                if (!HasClearInteractionPath(rb, point)) continue;
                struck.Add(rb);

                var part = StrikePart.Body;
                var used = shaper != null ? shaper.Shape(stats, to.magnitude, out part) : stats;
                bool glancing = to.sqrMagnitude > PointBlank * PointBlank &&
                    MeleeFairness.Classify(Vector3.Angle(facing, to), used.ArcDegrees, to.magnitude, used.Reach) == ImpactResult.Glance;
                var dir = World.Flat(rb.position - transform.position);
                var hit = Hits.Melee(controller, dir.sqrMagnitude > 0.01f ? dir : facing, used, itemId);
                if (rb.TryGetComponent(out PlayerHealth victim))
                {
                    if (victim.ApplyDamage(hit)) playersHit++;
                    Contacted?.Invoke(new MeleeContact(controller, itemId, swingSerial, rb, part, point,
                        glancing ? ImpactResult.Glance : ImpactResult.Hit, used, 0));
                    continue;
                }
                var smashable = rb.GetComponent<Smashable>();
                float before = smashable ? smashable.Remaining : 1f;
                if (rb.TryGetComponent(out IDamageable target)) target.ApplyDamage(hit);
                float after = smashable ? smashable.Remaining : 1f;
                bool broken = smashable && smashable.IsBroken;
                if (rb && !rb.isKinematic)
                    rb.AddForce((facing + Vector3.up * 0.4f) * used.Knockback * Hits.KnockbackSpeed * 0.5f, ForceMode.VelocityChange);
                Contacted?.Invoke(new MeleeContact(controller, itemId, swingSerial, rb, part, point,
                    ImpactCues.ResultFor(before, after, broken, glancing), used, ImpactCues.Stage(after)));
            }
            return playersHit;
        }

        static Vector3 ClosestPoint(Collider col, Vector3 point)
        {
            if (col is MeshCollider { convex: false }) return col.bounds.ClosestPoint(point);
            return col.ClosestPoint(point);
        }

        bool HasClearInteractionPath(Rigidbody target, Vector3 destination)
        {
            var origin = transform.position + Vector3.up * .8f;
            var delta = destination - origin;
            float distance = delta.magnitude;
            if (distance < .001f) return true;
            int mask = World.GroundMask & Physics.DefaultRaycastLayers;
            int count = Physics.RaycastNonAlloc(origin, delta / distance, obstructions, distance, mask, QueryTriggerInteraction.Ignore);
            var hits = obstructions;
            if (count == hits.Length)
            {
                hits = Physics.RaycastAll(origin, delta / distance, distance, mask, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }
            for (int i = 0; i < count; i++)
            {
                var collider = hits[i].collider;
                if (!collider || collider.transform.IsChildOf(transform) || collider.transform.IsChildOf(target.transform)) continue;
                var body = collider.attachedRigidbody;
                if (body)
                {
                    if (body == controller.Body || body == target || body == held || body.GetComponent<PlayerController>() || body.GetComponent<LetterTile>()) continue;
                    var cover = body.GetComponent<HeldWeapon>();
                    if (!body.isKinematic || !cover || cover.Definition?.Deploy?.Effect != DeployEffect.Cover) continue;
                }
                return false;
            }
            return true;
        }

        void GrabOrRevive()
        {
            if (held && !Weapon) return;
            if (!TryRevive()) TryGrab();
        }

        public bool TryGrab()
        {
            if (!controller.CanAct || controller.IsDodging || IsChanneling || (controller.Summoner && controller.Summoner.IsCrafting)) return false;
            if (held && !Weapon) return false;
            var best = GrabTarget();
            if (!best) return false;
            if (best.TryGetComponent(out LetterStash stash) && stash.CanOpen(controller))
                return stash.TryExchange(controller) || (stash.IsEmpty && best.TryGetComponent(out HeldWeapon emptyChest) && HasFreeGearSlot && TryEquip(emptyChest));
            if (best.TryGetComponent(out HeldWeapon pickedGear)) return TryEquip(pickedGear);
            if (Weapon)
            {
                if (storedGear) return false;
                storedGear = Weapon;
                ClearHeld();
                storedGear.gameObject.SetActive(false);
                ActiveSlot = 1 - ActiveSlot;
            }
            Pick(best);
            return true;
        }

        bool Grabbable(Rigidbody rb)
        {
            if (!rb || rb == controller.Body || rb == held || rb.GetComponent<LetterTile>()) return false;
            if (rb.TryGetComponent(out HelperAgent _)) return false;
            if (rb.TryGetComponent(out PlayerController player)) return !player.IsHeld && !player.IsEliminated;
            if (rb.TryGetComponent(out LetterStash stash) && stash.CanOpen(controller))
                return stash.WantsExchange(controller) || (stash.IsEmpty && HasFreeGearSlot && rb.TryGetComponent(out HeldWeapon chest) && !chest.IsHeld && !chest.IsSpent);
            if (rb.TryGetComponent(out HeldWeapon gear)) return !gear.IsSpent && !gear.IsHeld && HasFreeGearSlot;
            return !rb.isKinematic && rb.mass <= maxCarryMass;
        }

        public Rigidbody GrabTarget()
        {
            if ((held && !Weapon) || (Weapon && storedGear)) return null;
            var chest = transform.position + Vector3.up * .8f;
            if (InteractionAim.TryRay(controller, out var ray))
            {
                if (!InteractionAim.Trace(controller, ray, out var hit) || !Grabbable(hit.rigidbody)) return lastTarget = null;
                return lastTarget = Vector3.Distance(chest, hit.point) <= GrabDistance && HasClearInteractionPath(hit.rigidbody, hit.point)
                    ? hit.rigidbody : null;
            }
            var centre = transform.position + Vector3.up * 0.6f + controller.Facing * grabReach;
            int mask = World.TileLayer >= 0 ? ~(1 << World.TileLayer) : ~0;
            int n = Physics.OverlapSphereNonAlloc(centre, grabRadius, overlaps, mask, QueryTriggerInteraction.Ignore);
            var hits = overlaps;
            if (n == hits.Length) { hits = Physics.OverlapSphere(centre, grabRadius, mask, QueryTriggerInteraction.Ignore); n = hits.Length; }
            Rigidbody best = null;
            float bestScore = float.MaxValue;
            var facing = controller.Facing;

            for (int i = 0; i < n; i++)
            {
                var rb = hits[i].attachedRigidbody;
                if (!Grabbable(rb)) continue;
                var point = ClosestPoint(hits[i], chest);
                var to = point - chest;
                float d = to.sqrMagnitude;
                var flat = World.Flat(to);
                bool pointBlank = flat.sqrMagnitude <= PointBlank * PointBlank;
                if (d > GrabDistance * GrabDistance || (!pointBlank && !Geometry.InFrontArc(facing.x, facing.z, to.x, to.z, GrabArc))) continue;
                float score = CarryRules.AimScore(Mathf.Sqrt(d), pointBlank ? 0f : Vector3.Angle(facing, flat), rb == lastTarget);
                if (score >= bestScore || !HasClearInteractionPath(rb, point)) continue;
                bestScore = score;
                best = rb;
            }
            return lastTarget = best;
        }

        public void Equip(HeldWeapon w) => TryEquip(w);

        public bool TryEquip(HeldWeapon w)
        {
            if (!w || w.IsSpent || w.IsHeld || !HasFreeGearSlot || (controller.Summoner && controller.Summoner.IsCrafting)) return false;
            if (Weapon)
            {
                storedGear = Weapon;
                ClearHeld();
                storedGear.gameObject.SetActive(false);
                ActiveSlot = 1 - ActiveSlot;
            }
            else if (held) Drop();
            Pick(w.GetComponent<Rigidbody>());
            return true;
        }

        public bool SwitchGear()
        {
            if (!controller.CanAct || controller.IsDodging || deploying || (controller.Summoner && controller.Summoner.IsCrafting) || !storedGear || (held && !Weapon)) return false;
            var next = storedGear;
            var previous = Weapon;
            storedGear = previous;
            ClearHeld();
            if (previous) previous.gameObject.SetActive(false);
            next.gameObject.SetActive(true);
            Pick(next.GetComponent<Rigidbody>());
            ActiveSlot = 1 - ActiveSlot;
            return true;
        }

        public int ActiveSlot { get; private set; }

        public bool SwapSlots()
        {
            if (deploying || (controller.Summoner && controller.Summoner.IsCrafting)) return false;
            ActiveSlot = 1 - ActiveSlot;
            return true;
        }

        public HeldWeapon GearIn(int slot) => slot == ActiveSlot ? Weapon : storedGear;

        public bool SelectSlot(int slot)
        {
            if (slot is < 0 or > 1 || slot == ActiveSlot) return false;
            if (storedGear) return SwitchGear();
            if (!controller.CanAct || controller.IsDodging || deploying || (controller.Summoner && controller.Summoner.IsCrafting)) return false;
            if (held && !Weapon) return false;
            if (Weapon)
            {
                storedGear = Weapon;
                ClearHeld();
                storedGear.gameObject.SetActive(false);
            }
            ActiveSlot = slot;
            return true;
        }

        void Pick(Rigidbody rb)
        {
            if (rb.TryGetComponent(out ReleasedBodyCollision grace)) grace.Restore();
            CarryFlight.End(rb);
            held = rb;
            heldSince = Time.time;
            heldHomeParent = rb.transform.parent;
            weapon = rb.GetComponent<HeldWeapon>();

            if (rb.TryGetComponent(out PlayerController other))
            {
                heldPlayer = other;
                other.SetHeld(true, controller.overheadPoint);
                CacheCarryShape();
                return;
            }

            if (!carriedStates.ContainsKey(rb))
            {
                var colliders = rb.GetComponent<HeldWeapon>() ? rb.GetComponents<Collider>() : rb.GetComponentsInChildren<Collider>(true);
                var state = new CarryState { parent = heldHomeParent, colliders = colliders, enabled = new bool[colliders.Length], interpolation = rb.interpolation };
                for (int i = 0; i < colliders.Length; i++) state.enabled[i] = colliders[i].enabled;
                carriedStates.Add(rb, state);
            }
            rb.isKinematic = true;
            if (!weapon) rb.interpolation = RigidbodyInterpolation.None;
            SetCollidersEnabled(rb, false);
            if (weapon)
            {
                rb.transform.SetParent(controller.handR, false);
                weapon.OnHeld(this);
                HeldItemPose.SeatModel(weapon);
                var item = weapon.Definition;
                holdTurn = HeldItemPose.Turn(item);
                holdGrip = HeldItemPose.Grip(item);
                holdBase = weapon.HoldRotation;
                rb.transform.localRotation = holdWritten = holdBase * holdTurn;
                rb.transform.localPosition = item == null ? Vector3.zero : -(rb.transform.localRotation * holdGrip) * item.HeldScale;
                CacheCarryShape();
                return;
            }

            var point = rb.mass > HeavyCarryMass ? controller.overheadPoint : controller.holdPoint;
            rb.transform.rotation = Quaternion.LookRotation(controller.Facing);
            var offset = rb.transform.position - BoundsCentre(rb);
            rb.transform.position = point.position + offset;
            rb.transform.SetParent(point, true);
            CacheCarryShape();
        }

        void CacheCarryShape()
        {
            carryLocal = held.transform.localPosition;
            if (heldPlayer && held.TryGetComponent(out CapsuleCollider capsule))
            {
                float scale = Mathf.Abs(held.transform.lossyScale.y);
                carryCentre = capsule.center;
                carryRadius = Mathf.Clamp(capsule.radius * scale, MinCarryRadius, MaxCarryRadius);
                carryHalfWidth = Mathf.Max(capsule.height * .5f * scale, carryRadius);
                carryAxis = capsule.direction == 0 ? Vector3.right : capsule.direction == 2 ? Vector3.forward : Vector3.up;
                return;
            }
            var renderers = held.GetComponentsInChildren<Renderer>();
            var bounds = new Bounds(held.position, Vector3.one * .2f);
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            }
            carryCentre = held.transform.InverseTransformPoint(bounds.center);
            carryRadius = Mathf.Clamp(Mathf.Min(bounds.extents.x, bounds.extents.z), MinCarryRadius, MaxCarryRadius);
            var side = Vector3.Cross(Vector3.up, controller.Facing).normalized;
            carryHalfWidth = Mathf.Abs(side.x) * bounds.extents.x + Mathf.Abs(side.z) * bounds.extents.z;
            carryAxis = held.transform.InverseTransformDirection(side);
        }

        void LateUpdate() => ConstrainCarry();

        void ConstrainCarry()
        {
            if (heldPlayer && !heldPlayer.IsHeld) ClearHeld();
            if (!held || !held.transform.parent) return;
            if (heldPlayer)
            {
                held.transform.SetLocalPositionAndRotation(PlayerController.HeldOffset, PlayerController.HeldTurn);
                SweepCarry();
                FitCarrySideways();
                return;
            }
            var local = carryLocal;
            if (weapon && weapon.Definition != null)
            {
                if (holdTurn != Quaternion.identity)
                {
                    if (held.transform.localRotation != holdWritten) holdBase = held.transform.localRotation;
                    held.transform.localRotation = holdWritten = holdBase * holdTurn;
                }
                local = -(held.transform.localRotation * holdGrip) * weapon.Definition.HeldScale;
            }
            held.transform.localPosition = local;
            SweepCarry();
            FitCarrySideways();
        }

        void FitCarrySideways()
        {
            float radius = Mathf.Min(carryRadius, CarryRules.SideProbeRadius);
            if (carryHalfWidth <= radius + CarryRules.FitSkin) return;
            var axis = World.Flat(held.transform.TransformDirection(carryAxis));
            if (axis.sqrMagnitude < .01f) return;
            axis.Normalize();
            var centre = held.transform.TransformPoint(carryCentre);
            float shift = CarryRules.SideShift(carryHalfWidth, SideRoom(centre, axis, radius), SideRoom(centre, -axis, radius));
            if (Mathf.Abs(shift) > .001f) held.transform.position += axis * shift;
        }

        float SideRoom(Vector3 centre, Vector3 direction, float radius)
        {
            float room = carryHalfWidth;
            int count = Physics.SphereCastNonAlloc(centre, radius, direction, obstructions, carryHalfWidth - radius,
                CarryMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var collider = obstructions[i].collider;
                if (InteractionAim.Ignored(controller, collider) || (heldPlayer && collider.transform.IsChildOf(heldPlayer.transform))) continue;
                room = Mathf.Min(room, obstructions[i].distance + radius - CarryRules.FitSkin);
            }
            return Mathf.Max(0f, room);
        }

        void SweepCarry()
        {
            var centre = held.transform.TransformPoint(carryCentre);
            var origin = transform.position + Vector3.up * .8f;
            var delta = centre - origin;
            float distance = delta.magnitude;
            if (distance < .001f) return;
            float sweep = heldPlayer ? Mathf.Min(carryRadius, PlayerSweepRadius) : carryRadius;
            int count = Physics.SphereCastNonAlloc(origin, sweep, delta / distance, obstructions, distance,
                World.GroundMask & Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var hits = obstructions;
            if (count == hits.Length)
            {
                hits = Physics.SphereCastAll(origin, sweep, delta / distance, distance,
                    World.GroundMask & Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }
            float allowed = distance;
            for (int i = 0; i < count; i++)
                if (!InteractionAim.Ignored(controller, hits[i].collider)) allowed = Mathf.Min(allowed, Mathf.Max(0f, hits[i].distance - .03f));
            count = Physics.RaycastNonAlloc(origin, delta / distance, obstructions, distance,
                World.GroundMask & Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            hits = obstructions;
            if (count == hits.Length)
            {
                hits = Physics.RaycastAll(origin, delta / distance, distance,
                    World.GroundMask & Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }
            for (int i = 0; i < count; i++)
                if (!InteractionAim.Ignored(controller, hits[i].collider)) allowed = Mathf.Min(allowed, hits[i].distance - sweep - .03f);
            if (allowed < distance) held.transform.position -= delta / distance * (distance - allowed);
        }

        public void Throw()
        {
            if (!held || !controller.CanAct || controller.IsDodging || deploying || IsReviving || (controller.Summoner && controller.Summoner.IsCrafting)) return;
            nextAttack = Time.time + controller.Health.Rules.Unarmed.Cycle;
            controller.PlayPunch();
            var gear = Weapon;
            ConstrainCarry();
            if (!held) return;
            var thrownStats = gear ? gear.Definition?.Thrown : null;
            float speed = thrownStats != null ? thrownStats.Speed : throwSpeed * Mathf.Lerp(1f, 0.65f, Mathf.Clamp01(held.mass / maxCarryMass));
            var velocity = controller.Facing * speed + Vector3.up * (thrownStats != null && !thrownStats.Lob ? 0.3f : 4f) + World.Flat(controller.Body.linearVelocity) * 0.5f;
            if (InteractionAim.TryRay(controller, out var aimRay))
            {
                var target = InteractionAim.Trace(controller, aimRay, out var hit) ? hit.point : aimRay.GetPoint(12f);
                velocity = InteractionAim.LaunchVelocity(target - held.transform.TransformPoint(held.centerOfMass), speed, held.useGravity);
            }
            velocity = CappedThrow(velocity);

            if (heldPlayer)
            {
                var victim = heldPlayer;
                Release(velocity, false, true);
                victim.Health.ApplyDamage(Hits.Of(controller, controller.Facing, HitSource.Thrown, thrownPlayerDamage, 0f, hitStun: 0.35f));
                victim.Body.linearVelocity = velocity;
                ThrowTracker.Attach(victim.gameObject, controller, 1.2f);
                Thrown?.Invoke(victim.Body);
                return;
            }
            var rb = held;
            Release(velocity, gear != null, true);
            if (gear && thrownStats != null) ThrownGear.Attach(gear, controller);
            else ThrowTracker.Attach(rb.gameObject, controller, 1.5f);
            Thrown?.Invoke(rb);
        }

        public bool DeployHeld()
        {
            var gear = Weapon;
            var item = gear ? gear.Definition : null;
            if (!controller.CanAct || controller.IsDodging || IsReviving || IsChanneling || (controller.Summoner && (controller.Summoner.IsCrafting || controller.Summoner.IsSpelling))
                || item == null || (item.Deploy == null && !(item.Thrown != null && item.Thrown.FuseSeconds > 0f))) return false;
            int limit = controller.Health.Rules.MaxDeployed;
            if (DeployedGear.CountFor(controller) >= limit)
            {
                Popup.Show($"{limit} ALREADY PLACED", controller.OverheadPosition + Vector3.up * 0.4f, Color.white, 2.5f);
                return false;
            }
            if (HelperAgent.RefusesCover(controller, item)) return false;
            StartCoroutine(PlaceAfterChannel(gear));
            return true;
        }

        public bool ReleaseHelper()
        {
            var gear = Weapon;
            var item = gear ? gear.Definition : null;
            if (!controller.CanAct || controller.IsDodging || IsReviving || IsChanneling || (controller.Summoner && (controller.Summoner.IsCrafting || controller.Summoner.IsSpelling))
                || !HelperAgent.SpecFor(item, out var spec) || HelperAgent.RefusesRelease(controller, item, spec)) return false;
            StartCoroutine(ReleaseAfterChannel(gear, spec));
            return true;
        }

        IEnumerator ReleaseAfterChannel(HeldWeapon gear, HelperSpec spec)
        {
            deploying = true;
            controller.GetComponent<PlayerAppearance>()?.Play("Place", HelperAgent.PlaceSeconds);
            controller.MoveScale = controller.Health.Rules.CraftMoveSpeed;
            float ready = Time.time + HelperAgent.PlaceSeconds;
            while (Time.time < ready)
            {
                while (Time.timeScale <= 0f) yield return null;
                if (!controller.CanAct || controller.IsDodging || Weapon != gear) { deploying = false; controller.MoveScale = 1f; yield break; }
                yield return null;
            }
            deploying = false;
            controller.MoveScale = 1f;
            if (!controller.CanAct || controller.IsDodging || Weapon != gear || HelperAgent.RefusesRelease(controller, gear.Definition, spec)) yield break;
            int locked = HelperAgent.AimedLockFor(controller, spec);
            var body = PlaceInFront();
            HelperAgent.Launch(body.gameObject, gear.Definition, spec, controller, locked);
        }

        IEnumerator PlaceAfterChannel(HeldWeapon gear)
        {
            deploying = true;
            controller.GetComponent<PlayerAppearance>()?.Play("Place", gear.Definition.Deploy?.PlaceSeconds ?? 0.3f);
            controller.MoveScale = controller.Health.Rules.CraftMoveSpeed;
            float ready = Time.time + (gear.Definition.Deploy?.PlaceSeconds ?? 0f);
            while (Time.time < ready)
            {
                while (Time.timeScale <= 0f) yield return null;
                if (!controller.CanAct || controller.IsDodging || Weapon != gear) { deploying = false; controller.MoveScale = 1f; yield break; }
                yield return null;
            }
            deploying = false;
            controller.MoveScale = 1f;
            if (!controller.CanAct || controller.IsDodging || Weapon != gear || DeployedGear.CountFor(controller) >= controller.Health.Rules.MaxDeployed) yield break;
            if (HelperAgent.RefusesCover(controller, gear.Definition)) yield break;
            ApplyDeployment(gear);
        }

        void ApplyDeployment(HeldWeapon gear)
        {
            var item = gear.Definition;
            var body = PlaceInFront();
            if (item.Thrown != null)
            {
                DeployedGear.Register(gear.gameObject, controller);
                ThrownGear.Attach(gear, controller);
                return;
            }
            if (item.Consumable)
            {
                gear.MarkSpent();
                SlipZone.Create(body.position, item, controller);
                Destroy(gear.gameObject);
                return;
            }
            body.isKinematic = true;
            DeployedGear.Attach(gear, controller);
        }

        Rigidbody PlaceInFront()
        {
            var body = held;
            Release(Vector3.zero);
            var handSpot = body.position;
            var placedAt = transform.position + controller.Facing * DeployReach;
            var placedFacing = Quaternion.LookRotation(controller.Facing);
            body.transform.SetPositionAndRotation(placedAt, placedFacing);
            body.position = placedAt;
            body.rotation = placedFacing;
            SettleReleasedBody(body, false, false, handSpot);
            return body;
        }

        public void Drop()
        {
            if (held) Release(World.Flat(controller.Body.linearVelocity));
        }

        void DropAllGear()
        {
            Drop();
            if (!storedGear) return;
            var gear = storedGear;
            storedGear = null;
            gear.gameObject.SetActive(true);
            gear.transform.SetParent(World.Transient, true);
            gear.OnReleased();
            gear.transform.position = transform.position - controller.Facing * 0.8f + Vector3.up;
            var body = gear.GetComponent<Rigidbody>();
            body.isKinematic = false;
            RestoreCarry(body);
            body.position = gear.transform.position;
            SettleReleasedBody(body, false, false, body.position);
            body.linearVelocity = Vector3.zero;
        }

        public void ResetForRound()
        {
            StopAllCoroutines();
            deploying = punching = false;
            controller.MoveScale = 1f;
            StopReviving();
            if (held && weapon) Destroy(held.gameObject);
            else Drop();
            if (storedGear) Destroy(storedGear.gameObject);
            storedGear = null;
            carriedStates.Clear();
            ActiveSlot = 0;
            nextAttack = 0f;
            ClearHeld();
        }

        public void ForgetHeld()
        {
            if (held) carriedStates.Remove(held);
            ClearHeld();
        }

        static Vector3 CappedThrow(Vector3 velocity)
        {
            float x = velocity.x, y = velocity.y, z = velocity.z;
            CarryRules.ClampSpeed(ref x, ref y, ref z, CarryRules.MaxThrowSpeed);
            return new Vector3(x, y, z);
        }

        void Release(Vector3 velocity, bool thrown = false, bool launched = false)
        {
            ConstrainCarry();
            if (!held) return;
            var rb = held;
            var p = heldPlayer;
            var origin = rb.transform.position;
            var rotation = rb.transform.rotation;
            var carriedCentre = rb.transform.TransformPoint(carryCentre);
            bool overhead = rb.transform.parent == controller.overheadPoint;
            ClearHeld();

            if (p)
            {
                p.SetHeld(false);
                PlaceReleasedPlayer(p, carriedCentre, launched);
                p.Body.linearVelocity = velocity;
                if (launched) CarryFlight.Attach(p.Body, CarryRules.FlightSeconds);
                ReleasedBodyCollision.Attach(p.Body, controller);
                return;
            }
            var home = carriedStates.TryGetValue(rb, out var state) ? state.parent : heldHomeParent;
            rb.transform.SetParent(rb.GetComponent<HeldWeapon>() ? World.Transient : home ? home : null, true);
            if (rb.TryGetComponent(out HeldWeapon released))
            {
                if (thrown) released.OnThrown(controller);
                else released.OnReleased();
                rb.transform.SetPositionAndRotation(origin, rotation);
            }
            rb.position = rb.transform.position;
            rb.rotation = rb.transform.rotation;
            rb.isKinematic = false;
            RestoreCarry(rb);
            if (!thrown) SettleReleasedBody(rb, overhead && !launched && !released);
            rb.linearVelocity = velocity;
            rb.angularVelocity = Random.insideUnitSphere * (launched ? CarryRules.ThrowSpin : CarryRules.DropSpin);
            if (launched) CarryFlight.Attach(rb, CarryRules.FlightSeconds);
            ReleasedBodyCollision.Attach(rb, controller);
        }

        Vector3 Chest => transform.position + Vector3.up * ChestHeight;

        void PlaceReleasedPlayer(PlayerController p, Vector3 carriedCentre, bool launched)
        {
            if (!p.TryGetComponent(out CapsuleCollider capsule)) return;
            Physics.SyncTransforms();
            var lift = Vector3.up * (capsule.center.y * Mathf.Abs(p.transform.lossyScale.y));
            var ground = transform.position + controller.Facing * (bodyRadius + carryRadius + PlayerSpotGap) + Vector3.up * CarryRules.SetDownLift;
            if (launched && TryPlayerSpots(p, capsule, carriedCentre - lift, lift)) return;
            if (TryPlayerSpots(p, capsule, ground, lift)) return;
            MovePlayer(p, transform.position);
        }

        bool TryPlayerSpots(PlayerController p, CapsuleCollider capsule, Vector3 first, Vector3 lift)
        {
            var facing = controller.Facing;
            float step = Mathf.Max(CarryRules.SpotStep, carryRadius * 2f);
            for (int i = 0; i < CarryRules.SpotCount; i++)
            {
                CarryRules.Spot(i, facing.x, facing.z, step, out float x, out float z);
                var feet = first + new Vector3(x, 0f, z);
                if (!PlayerFits(p, capsule, feet) || !Reachable(Chest, feet + lift, p.transform)) continue;
                MovePlayer(p, feet);
                return true;
            }
            return false;
        }

        bool PlayerFits(PlayerController p, CapsuleCollider capsule, Vector3 feet)
        {
            float scale = Mathf.Abs(p.transform.lossyScale.y);
            float radius = capsule.radius * scale;
            float half = Mathf.Max(0f, capsule.height * .5f * scale - radius);
            var centre = feet + Vector3.up * (capsule.center.y * scale);
            int count = Physics.OverlapCapsuleNonAlloc(centre - Vector3.up * half, centre + Vector3.up * half, radius * PlayerFitShrink,
                overlaps, SpaceMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var other = overlaps[i];
                if (!other || other.transform.IsChildOf(p.transform)) continue;
                var body = other.attachedRigidbody;
                if (body && (body.TryGetComponent(out LetterTile _) || (!body.isKinematic && body.mass <= maxCarryMass && !body.TryGetComponent(out PlayerController _)))) continue;
                return false;
            }
            return true;
        }

        static void MovePlayer(PlayerController p, Vector3 feet)
        {
            p.transform.position = feet;
            p.Body.position = feet;
            Physics.SyncTransforms();
        }

        bool Reachable(Vector3 from, Vector3 to, Transform ignore)
        {
            var delta = to - from;
            float distance = delta.magnitude;
            if (distance < .001f) return true;
            int count = Physics.RaycastNonAlloc(from, delta / distance, obstructions, distance, CarryMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var collider = obstructions[i].collider;
                if (InteractionAim.Ignored(controller, collider) || (ignore && collider.transform.IsChildOf(ignore))) continue;
                var body = collider.attachedRigidbody;
                if (body && !body.isKinematic) continue;
                return false;
            }
            return true;
        }

        void SettleReleasedBody(Rigidbody body, bool setDown) =>
            SettleReleasedBody(body, setDown, !setDown, body.position);

        void SettleReleasedBody(Rigidbody body, bool setDown, bool trustFirst, Vector3 fallback)
        {
            Physics.SyncTransforms();
            var shapes = body.GetComponentsInChildren<Collider>();
            if (setDown) MoveBody(body, body.position + SetDownShift(ShapeBounds(shapes, body)));
            var first = body.position;
            var bounds = ShapeBounds(shapes, body);
            float step = Mathf.Max(CarryRules.SpotStep, Mathf.Max(bounds.extents.x, bounds.extents.z));
            var facing = controller.Facing;
            for (int i = 0; i < CarryRules.SpotCount; i++)
            {
                if (i > 0)
                {
                    CarryRules.Spot(i, facing.x, facing.z, step, out float x, out float z);
                    MoveBody(body, first + new Vector3(x, 0f, z));
                }
                Depenetrate(body, shapes);
                if (Overlapping(body, shapes, false)) continue;
                if ((i == 0 && trustFirst) || Reachable(Chest, ShapeBounds(shapes, body).center, body.transform)) return;
            }
            MoveBody(body, fallback);
            Depenetrate(body, shapes);
        }

        Vector3 SetDownShift(Bounds bounds)
        {
            var facing = controller.Facing;
            float along = Mathf.Abs(facing.x) * bounds.extents.x + Mathf.Abs(facing.z) * bounds.extents.z;
            var target = transform.position + facing * (bodyRadius + along + CarryRules.SetDownGap);
            return new Vector3(target.x - bounds.center.x, transform.position.y + CarryRules.SetDownLift - bounds.min.y, target.z - bounds.center.z);
        }

        static Bounds ShapeBounds(Collider[] shapes, Rigidbody body)
        {
            var bounds = new Bounds(body.position, Vector3.zero);
            bool any = false;
            foreach (var shape in shapes)
            {
                if (!shape || !shape.enabled || shape.isTrigger || shape.attachedRigidbody != body) continue;
                if (any) bounds.Encapsulate(shape.bounds);
                else { bounds = shape.bounds; any = true; }
            }
            return bounds;
        }

        static void MoveBody(Rigidbody body, Vector3 position)
        {
            body.transform.position = position;
            body.position = position;
            Physics.SyncTransforms();
        }

        void Depenetrate(Rigidbody body, Collider[] shapes)
        {
            for (int pass = 0; pass < DepenetratePasses; pass++)
            {
                if (!Overlapping(body, shapes, true)) break;
                Physics.SyncTransforms();
            }
        }

        bool Overlapping(Rigidbody body, Collider[] shapes, bool push)
        {
            bool found = false;
            foreach (var shape in shapes)
            {
                if (!shape || !shape.enabled || shape.isTrigger || shape.attachedRigidbody != body) continue;
                var bounds = shape.bounds;
                int count = Physics.OverlapSphereNonAlloc(bounds.center, bounds.extents.magnitude + .05f, overlaps, SpaceMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var obstacle = overlaps[i];
                    if (!obstacle || obstacle.attachedRigidbody == body || InteractionAim.Ignored(controller, obstacle)) continue;
                    var other = obstacle.attachedRigidbody;
                    if (other && other.TryGetComponent(out LetterTile _)) continue;
                    if (!Physics.ComputePenetration(shape, shape.transform.position, shape.transform.rotation,
                        obstacle, obstacle.transform.position, obstacle.transform.rotation, out var direction, out float distance)) continue;
                    if (distance <= (push ? 0f : CarryRules.EmbedTolerance)) continue;
                    found = true;
                    if (!push) return true;
                    body.transform.position += direction * (distance + .015f);
                    body.position = body.transform.position;
                }
            }
            return found;
        }

        void RestoreCarry(Rigidbody body)
        {
            if (!carriedStates.TryGetValue(body, out var state)) { SetCollidersEnabled(body, true); return; }
            for (int i = 0; i < state.colliders.Length; i++) if (state.colliders[i]) state.colliders[i].enabled = state.enabled[i];
            body.interpolation = state.interpolation;
            carriedStates.Remove(body);
        }

        void ClearHeld()
        {
            held = null;
            heldPlayer = null;
            weapon = null;
            raiseStartedAt = -1f;
            if (controller && controller.Health) controller.Health.RaisedShield = null;
        }

        static void SetCollidersEnabled(Rigidbody rb, bool on)
        {
            var colliders = rb.GetComponent<HeldWeapon>() ? rb.GetComponents<Collider>() : rb.GetComponentsInChildren<Collider>(true);
            foreach (var c in colliders) c.enabled = on;
        }

        static Vector3 BoundsCentre(Rigidbody rb)
        {
            var renderers = rb.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return rb.worldCenterOfMass;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b.center;
        }
    }
}
