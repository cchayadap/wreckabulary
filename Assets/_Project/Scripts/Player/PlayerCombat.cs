using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>Punching, grabbing, carrying and throwing, using summoned weapons, blocking with a PLATE and reviving teammates.</summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerCombat : MonoBehaviour
    {
        // The punch's numbers are the mode's "unarmed" block in rules.json.

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
        /// <summary>When block went down with a shield in hand; the shield is up once its raise time has passed.</summary>
        float raiseStartedAt = -1f;
        PlayerController reviving;
        static readonly Collider[] Overlaps = new Collider[48];
        static readonly RaycastHit[] Obstructions = new RaycastHit[32];
        readonly HashSet<Rigidbody> struck = new();

        /// <summary>Hits closer than this skip the arc check: they're inside the attacker.</summary>
        const float PointBlank = 0.25f;

        /// <summary>Something (or someone) was thrown.</summary>
        public event System.Action<Rigidbody> Thrown;

        public bool IsHolding => held;
        public Rigidbody Held => held;
        public HeldWeapon Weapon => held ? weapon : null;
        public HeldWeapon StoredGear => storedGear;
        public bool IsDeploying => deploying;
        public bool IsChanneling => deploying || punching || (Weapon && Weapon.IsUsing);
        public bool HasFreeGearSlot => (Weapon ? 1 : 0) + (storedGear ? 1 : 0)
            + (controller.Summoner && controller.Summoner.IsCrafting ? 1 : 0) < controller.Health.Rules.MaxCarried;
        /// <summary>A PLATE is up: hits from the front are blocked.</summary>
        public bool IsBlocking => controller.Health.RaisedShield != null;
        public bool IsReviving => reviving;
        /// <summary>The downed teammate being revived, or null.</summary>
        public PlayerController Reviving => reviving;

        void Awake() => controller = GetComponent<PlayerController>();

        void Start()
        {
            var health = controller.Health;
            controller.Dodged += _ => CancelChannels();
            health.Damaged += (_, _, r) =>
            {
                if (r.Blocked) WearShield(r.BlockedDamage);
                if (r.HitStun > 0f) CancelChannels();
                // A staggering hit knocks a carried thing out of the hands; weapons are gripped tighter.
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
            if (held == null && (heldPlayer || weapon)) ClearHeld();
            if (heldPlayer && Time.time - heldSince > struggleTime) Drop();

            var c = controller.Commands;
            bool free = controller.CanAct && !controller.IsDodging && !deploying && !(controller.Summoner && (controller.Summoner.IsSpelling || controller.Summoner.IsCrafting));
            UpdateRevive(free && c.grabHeld);
            UpdateBlock(free && c.blockHeld && !reviving);
            if (!free || reviving) return;

            if (c.drop) Drop();
            if (c.swap) SwitchGear();
            if (c.deploy) DeployHeld();
            if (c.grab) GrabOrThrow();
            // No swinging from behind a raised (or rising) shield.
            if (c.attack && raiseStartedAt < 0f) Attack();
        }

        // ---- Blocking ----

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

        /// <summary>Blocked damage wears the PLATE down. Worn through, it falls apart and the block drops with it (see <see cref="ClearHeld"/>).</summary>
        void WearShield(float blocked)
        {
            if (Weapon && Weapon.Shield != null) Weapon.Wear(blocked, this);
        }

        // ---- Reviving ----

        /// <summary>The nearest downed teammate within the rules' revive range, or null.</summary>
        public PlayerController DownedTeammateNearby()
        {
            float range = controller.Health.Rules.ReviveRange;
            float bestSq = range * range;
            PlayerController best = null;
            foreach (var p in World.Players)
            {
                if (p == controller || !p.IsDowned || p.IsHeld || !Teams.AreTeammates(p.Team, controller.Team)) continue;
                float d = World.Flat(p.transform.position - transform.position).sqrMagnitude;
                if (d > bestSq || !HasClearInteractionPath(p.Body, p.Body.worldCenterOfMass)) continue;
                bestSq = d;
                best = p;
            }
            return best;
        }

        /// <summary>Starts reviving the nearest downed teammate. Grab has to stay held until it finishes.</summary>
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
            // A little slack, so the teammate's crawl doesn't break it off at the edge of reach.
            float range = controller.Health.Rules.ReviveRange + 0.3f;
            var to = World.Flat(reviving.transform.position - transform.position);
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

        // ---- Attacking ----

        public void Attack()
        {
            if (!controller.CanAct || controller.IsDodging || IsChanneling || IsReviving || raiseStartedAt >= 0f
                || (controller.Summoner && (controller.Summoner.IsCrafting || controller.Summoner.IsSpelling)) || Time.time < nextAttack) return;
            if (Weapon && Weapon.Shield == null)
            {
                nextAttack = Time.time + Weapon.Cooldown;
                controller.PlayPunch();
                Weapon.Use(this);
                return;
            }
            // Carried things are thrown; with a PLATE in one hand, the other still punches.
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
                if (!controller.CanAct || controller.IsDodging || IsReviving || IsBlocking || (held && !Weapon)) break;
                Strike(stats, null, firstFrame);
                firstFrame = false;
                yield return null;
            } while (Time.time < ends);
            yield return new WaitForSeconds(stats.Recovery);
            punching = false;
        }

        /// <summary>
        /// Hits everything within reach of the chest and inside the swing's arc: players take the
        /// stats' damage, furniture takes its break power. Returns how many players were hit.
        /// </summary>
        public int Strike(MeleeStats stats, string itemId, bool beginSwing = true)
        {
            var chest = transform.position + Vector3.up * 0.8f;
            var facing = controller.Facing;
            int mask = World.TileLayer >= 0 ? ~(1 << World.TileLayer) : ~0;
            int n = Physics.OverlapSphereNonAlloc(chest, stats.Reach, Overlaps, mask, QueryTriggerInteraction.Ignore);
            var hits = Overlaps;
            // Preserve hit correctness in dense prop clusters; ordinary swings reuse the fixed buffer.
            if (n == hits.Length) { hits = Physics.OverlapSphere(chest, stats.Reach, mask, QueryTriggerInteraction.Ignore); n = hits.Length; }
            if (beginSwing) struck.Clear();
            int playersHit = 0;

            for (int i = 0; i < n; i++)
            {
                var col = hits[i];
                var rb = col.attachedRigidbody;
                if (!rb || rb == controller.Body || rb == held || struck.Contains(rb)) continue;

                // Aim at the nearest part of the thing, so a sofa counts when its arm is in front.
                var to = World.Flat(ClosestPoint(col, chest) - chest);
                if (to.sqrMagnitude > PointBlank * PointBlank && !Geometry.InFrontArc(facing.x, facing.z, to.x, to.z, stats.ArcDegrees))
                    continue;
                if (!HasClearInteractionPath(rb, ClosestPoint(col, chest))) continue;
                struck.Add(rb);

                var dir = World.Flat(rb.position - transform.position);
                var hit = Hits.Melee(controller, dir.sqrMagnitude > 0.01f ? dir : facing, stats, itemId);
                if (rb.TryGetComponent(out PlayerHealth victim))
                {
                    if (victim.ApplyDamage(hit)) playersHit++;
                    continue;
                }
                if (rb.TryGetComponent(out IDamageable target)) target.ApplyDamage(hit);
                if (rb && !rb.isKinematic)
                    rb.AddForce((facing + Vector3.up * 0.4f) * stats.Knockback * Hits.KnockbackSpeed * 0.5f, ForceMode.VelocityChange);
            }
            return playersHit;
        }

        static Vector3 ClosestPoint(Collider col, Vector3 point)
        {
            // ClosestPoint only works on primitives and convex meshes.
            if (col is MeshCollider { convex: false }) return col.bounds.ClosestPoint(point);
            return col.ClosestPoint(point);
        }

        /// <summary>Room geometry and placed cover interrupt a reach; loose objects and visual colliders don't.</summary>
        bool HasClearInteractionPath(Rigidbody target, Vector3 destination)
        {
            // The grab query is ahead of the player and can already be beyond a thin wall.
            // Every visibility ray therefore starts at the player's chest, never at that query centre.
            var origin = transform.position + Vector3.up * .8f;
            var delta = destination - origin;
            float distance = delta.magnitude;
            if (distance < .001f) return true;
            int mask = World.GroundMask & Physics.DefaultRaycastLayers;
            int count = Physics.RaycastNonAlloc(origin, delta / distance, Obstructions, distance, mask, QueryTriggerInteraction.Ignore);
            var hits = Obstructions;
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

        // ---- Grabbing ----

        /// <summary>Throws what's held. Empty-handed, a downed teammate in reach comes first, then the nearest thing to pick up.</summary>
        void GrabOrThrow()
        {
            if (held) Throw();
            else if (!TryRevive()) TryGrab();
        }

        public bool TryGrab()
        {
            if (!controller.CanAct || controller.IsDodging || IsChanneling || (controller.Summoner && controller.Summoner.IsCrafting)) return false;
            var centre = transform.position + Vector3.up * 0.6f + controller.Facing * grabReach;
            int mask = World.TileLayer >= 0 ? ~(1 << World.TileLayer) : ~0;
            int n = Physics.OverlapSphereNonAlloc(centre, grabRadius, Overlaps, mask, QueryTriggerInteraction.Ignore);
            var hits = Overlaps;
            if (n == hits.Length) { hits = Physics.OverlapSphere(centre, grabRadius, mask, QueryTriggerInteraction.Ignore); n = hits.Length; }
            Rigidbody best = null;
            float bestSq = float.MaxValue;

            for (int i = 0; i < n; i++)
            {
                var rb = hits[i].attachedRigidbody;
                if (!rb || rb == controller.Body || (rb.isKinematic && !rb.GetComponent<HeldWeapon>()) || rb.GetComponent<LetterTile>()) continue;
                if (rb.TryGetComponent(out HeldWeapon gear) && (gear.IsSpent || !HasFreeGearSlot)) continue;
                bool isPlayer = rb.GetComponent<PlayerController>();
                if (!isPlayer && rb.mass > maxCarryMass) continue;
                if (!HasClearInteractionPath(rb, ClosestPoint(hits[i], transform.position + Vector3.up * .8f))) continue;
                float d = (rb.worldCenterOfMass - centre).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = rb; }
            }
            if (!best) return false;
            if (best.TryGetComponent(out HeldWeapon pickedGear)) return TryEquip(pickedGear);
            Pick(best);
            return true;
        }

        /// <summary>Equips gear in a free slot, keeping the other crafted item intact.</summary>
        public void Equip(HeldWeapon w) => TryEquip(w);

        public bool TryEquip(HeldWeapon w)
        {
            if (!w || w.IsSpent || !HasFreeGearSlot || (controller.Summoner && controller.Summoner.IsCrafting)) return false;
            if (Weapon)
            {
                storedGear = Weapon;
                ClearHeld();
                storedGear.gameObject.SetActive(false);
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
            return true;
        }

        void Pick(Rigidbody rb)
        {
            held = rb;
            heldSince = Time.time;
            heldHomeParent = rb.transform.parent;
            weapon = rb.GetComponent<HeldWeapon>();

            if (rb.TryGetComponent(out PlayerController other))
            {
                heldPlayer = other;
                other.SetHeld(true, controller.overheadPoint);
                return;
            }

            rb.isKinematic = true;
            SetCollidersEnabled(rb, false);
            if (weapon)
            {
                rb.transform.SetParent(controller.handR, false);
                weapon.OnHeld(this);
                var grip = weapon.Definition?.Grip;
                rb.transform.localPosition = grip == null ? Vector3.zero : -new Vector3(grip[0], grip[1], grip[2]) * weapon.Definition.HeldScale;
                rb.transform.localRotation = weapon.HoldRotation;
                return;
            }

            var point = rb.mass > 2.5f ? controller.overheadPoint : controller.holdPoint;
            rb.transform.rotation = Quaternion.LookRotation(controller.Facing);
            var offset = rb.transform.position - BoundsCentre(rb);
            rb.transform.position = point.position + offset;
            rb.transform.SetParent(point, true);
        }

        /// <summary>Throws what's held. Thrown players take a knock as they go.</summary>
        public void Throw()
        {
            if (!held || !controller.CanAct || controller.IsDodging || deploying || IsReviving || (controller.Summoner && controller.Summoner.IsCrafting)) return;
            nextAttack = Time.time + controller.Health.Rules.Unarmed.Cycle;
            controller.PlayPunch();
            var gear = Weapon;
            var thrownStats = gear ? gear.Definition?.Thrown : null;
            float speed = thrownStats != null ? thrownStats.Speed : throwSpeed * Mathf.Lerp(1f, 0.65f, Mathf.Clamp01(held.mass / maxCarryMass));
            var velocity = controller.Facing * speed + Vector3.up * (thrownStats != null && !thrownStats.Lob ? 0.3f : 4f) + World.Flat(controller.Body.linearVelocity) * 0.5f;

            if (heldPlayer)
            {
                var victim = heldPlayer;
                Release(velocity);
                victim.Health.ApplyDamage(Hits.Of(controller, controller.Facing, HitSource.Thrown, thrownPlayerDamage, 0f, hitStun: 0.35f));
                victim.Body.linearVelocity = velocity;
                ThrowTracker.Attach(victim.gameObject, controller, 1.2f);
                Thrown?.Invoke(victim.Body);
                return;
            }
            var rb = held;
            Release(velocity, gear != null);
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
            if (DeployedGear.CountFor(controller) >= controller.Health.Rules.MaxDeployed) return false;
            StartCoroutine(PlaceAfterChannel(gear));
            return true;
        }

        IEnumerator PlaceAfterChannel(HeldWeapon gear)
        {
            deploying = true;
            controller.GetComponent<PlayerAppearance>()?.Play("Place", gear.Definition.Deploy?.PlaceSeconds ?? 0.3f);
            controller.MoveScale = controller.Health.Rules.CraftMoveSpeed;
            float ready = Time.time + (gear.Definition.Deploy?.PlaceSeconds ?? 0f);
            while (Time.time < ready)
            {
                if (!controller.CanAct || controller.IsDodging || Weapon != gear) { deploying = false; controller.MoveScale = 1f; yield break; }
                yield return null;
            }
            deploying = false;
            controller.MoveScale = 1f;
            if (!controller.CanAct || controller.IsDodging || Weapon != gear || DeployedGear.CountFor(controller) >= controller.Health.Rules.MaxDeployed) yield break;
            ApplyDeployment(gear);
        }

        void ApplyDeployment(HeldWeapon gear)
        {
            var item = gear.Definition;
            var body = held;
            Release(Vector3.zero);
            body.position = transform.position + controller.Facing * 1.6f;
            body.rotation = Quaternion.LookRotation(controller.Facing);
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

        /// <summary>Lets go of whatever is held without throwing it.</summary>
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
            SetCollidersEnabled(body, true);
            body.linearVelocity = Vector3.zero;
        }

        /// <summary>Round reset: summoned weapons vanish, everything else is let go.</summary>
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
            nextAttack = 0f;
            ClearHeld();
        }

        /// <summary>Called by a weapon when it runs out of uses.</summary>
        public void ForgetHeld() => ClearHeld();

        void Release(Vector3 velocity, bool thrown = false)
        {
            var rb = held;
            var p = heldPlayer;
            ClearHeld();

            if (p)
            {
                p.SetHeld(false);
                p.transform.position = transform.position + controller.Facing * 0.9f + Vector3.up * 1.2f;
                p.Body.linearVelocity = velocity;
                return;
            }
            // A stored slot remains parented to a hand. Released crafted gear always belongs to the room.
            rb.transform.SetParent(rb.GetComponent<HeldWeapon>() ? World.Transient : heldHomeParent ? heldHomeParent : null, true);
            if (rb.TryGetComponent(out HeldWeapon released))
            {
                if (thrown) released.OnThrown(controller);
                else released.OnReleased();
                rb.transform.position = transform.position + controller.Facing * 0.8f + Vector3.up * 1f;
            }
            rb.isKinematic = false;
            SetCollidersEnabled(rb, true);
            rb.linearVelocity = velocity;
            rb.angularVelocity = Random.insideUnitSphere * 3f;
        }

        void ClearHeld()
        {
            held = null;
            heldPlayer = null;
            weapon = null;
            // Whatever was held, it isn't a raised shield any more.
            raiseStartedAt = -1f;
            if (controller && controller.Health) controller.Health.RaisedShield = null;
        }

        static void SetCollidersEnabled(Rigidbody rb, bool on)
        {
            // Imported visual colliders remain disabled; only the authored gameplay collider participates.
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
