using System.Collections;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Art;

namespace Wreckabulary
{
    [RequireComponent(typeof(Rigidbody))]
    public class HeldWeapon : MonoBehaviour, IDamageable
    {
        public const float HealthPerDamage = 0.9f;
        public string word;
        public int uses = 5;
        public float cooldown = 0.35f, reach = 1.2f, radius = 0.8f, knockback = 9f, damage = 25f;
        public bool ranged;
        public float projectileSpeed = 18f, blastRadius;

        ItemDefinition definition;
        MeleeStats legacyStats;
        float durabilityLeft = -1f;
        bool broken, spent, usingItem, inFlight;
        float thrownAt;
        PlayerCombat holder;
        RigidbodyInterpolation releasedInterpolation;
        bool interpolationSuspended;
        public ItemDefinition Definition => definition;
        public bool IsSpent => spent;
        public bool IsUsing => usingItem;
        public ShieldStats Shield => definition?.Shield;
        public float DurabilityLeft => durabilityLeft < 0f ? (definition != null ? Mathf.Max(1, definition.Durability) : 1f) : durabilityLeft;
        public MeleeStats Stats => definition?.Melee ?? (legacyStats ??= new MeleeStats
        {
            Damage = Mathf.Round(damage * HealthPerDamage), Reach = reach + radius - 0.4f,
            ArcDegrees = 100f, Recovery = cooldown, Knockback = knockback / Hits.KnockbackSpeed,
            BreakPower = damage / Smashable.HealthPerBreakPower, HitStun = 0.25f,
        });
        public float Cooldown => definition?.Melee?.Cycle ?? definition?.Use?.ChannelSeconds ?? cooldown;
        public Quaternion HoldRotation => definition != null ? Quaternion.identity : Quaternion.Euler(ranged ? 90f : 65f, 0f, 0f);

        public void Configure(ItemDefinition item)
        {
            definition = item;
            word = item.Id;
            durabilityLeft = Mathf.Max(1, item.Durability);
        }

        void OnDisable() { StopAllCoroutines(); usingItem = false; }

        public void CancelUse()
        {
            StopAllCoroutines();
            usingItem = false;
            SetPoseRotation(HoldRotation);
        }

        void Update()
        {
            if (inFlight && Time.time - thrownAt > 0.3f && GetComponent<Rigidbody>().linearVelocity.sqrMagnitude < 0.1f)
                OnReleased();
        }

        public void OnHeld(PlayerCombat user)
        {
            var body = GetComponent<Rigidbody>();
            if (!interpolationSuspended)
            {
                releasedInterpolation = body.interpolation;
                interpolationSuspended = true;
            }
            body.interpolation = RigidbodyInterpolation.None;
            CancelUse();
            inFlight = false;
            holder = user;
            if (TryGetComponent(out DeployedGear deployed)) deployed.Deactivate();
            if (TryGetComponent(out ThrownGear thrown)) thrown.StopTracking();
            if (definition != null)
            {
                transform.localScale = Vector3.one * definition.HeldScale;
                var library = MaterialLibrary.Load();
                if (library) library.ApplySkin(gameObject, user.GetComponent<PlayerAppearance>()?.SkinFor(word) ?? Skin.Standard);
            }
        }

        public void OnReleased()
        {
            CancelUse();
            inFlight = false;
            holder = null;
            if (definition != null) transform.localScale = Vector3.one;
            var library = MaterialLibrary.Load();
            if (library) library.ApplySkin(gameObject, Skin.Standard);
            RestoreInterpolation();
        }

        public void OnThrown(PlayerController thrower)
        {
            CancelUse();
            holder = null;
            inFlight = true;
            thrownAt = Time.time;
            if (definition != null) transform.localScale = Vector3.one * definition.HeldScale;
            var library = MaterialLibrary.Load();
            if (library) library.ApplySkin(gameObject, thrower.GetComponent<PlayerAppearance>()?.SkinFor(word) ?? Skin.Standard);
            RestoreInterpolation();
        }

        void RestoreInterpolation()
        {
            if (!interpolationSuspended) return;
            GetComponent<Rigidbody>().interpolation = releasedInterpolation;
            interpolationSuspended = false;
        }

        public void Use(PlayerCombat user)
        {
            if (broken || usingItem || !user) return;
            var owner = user.GetComponent<PlayerController>();
            if (definition != null)
            {
                if (definition.Use != null) { StartCoroutine(ConsumeAfterChannel(user, owner)); return; }
                if (definition.Thrown != null) { user.Throw(); return; }
                if (definition.Deploy != null) { user.DeployHeld(); return; }
                if (definition.Melee != null) { StartCoroutine(MeleeAfterWindup(user, owner)); return; }
                return;
            }
            if (ranged)
                Projectile.Fire(word, transform.position + owner.Facing * 0.4f, owner.Facing * projectileSpeed, owner,
                    Mathf.Round(damage * HealthPerDamage), knockback / Hits.KnockbackSpeed,
                    damage / Smashable.HealthPerBreakPower, blastRadius);
            else { user.Strike(Stats, word); StartCoroutine(Swing()); }
            if (--uses <= 0) Break(user);
        }

        IEnumerator MeleeAfterWindup(PlayerCombat user, PlayerController owner)
        {
            usingItem = true;
            owner.GetComponent<PlayerAppearance>()?.Play(definition.Family == HandlingFamily.MeleeThrust ? "Thrust_OneHand" : "Swing_OneHand", Stats.Cycle);
            yield return new WaitForSeconds(Stats.Windup);
            if (owner && owner.CanAct && !owner.IsDodging && user.Weapon == this && !user.IsBlocking)
            {
                StartCoroutine(Swing());
                float ends = Time.time + Stats.Active;
                bool firstFrame = true;
                do
                {
                    while (Time.timeScale <= 0f) yield return null;
                    if (!owner.CanAct || owner.IsDodging || user.Weapon != this) break;
                    user.Strike(Stats, word, firstFrame);
                    if (firstFrame)
                    {
                        GameFeedback.Burst("Speed_Trail", owner.transform.position + Vector3.up * .75f + owner.Facing * .6f,
                            definition.Family == HandlingFamily.MeleeThrust ? .42f : .7f, GameFeedback.SkillColor(word), .2f);
                        firstFrame = false;
                        Wear(1f, user);
                        if (broken) yield break;
                    }
                    yield return null;
                } while (Time.time < ends);
                yield return new WaitForSeconds(Stats.Recovery);
            }
            usingItem = false;
        }

        IEnumerator ConsumeAfterChannel(PlayerCombat user, PlayerController owner)
        {
            usingItem = true;
            owner.GetComponent<PlayerAppearance>()?.PlayItemUse(definition, definition.Use.ChannelSeconds);
            float ready = Time.time + definition.Use.ChannelSeconds;
            while (Time.time < ready)
            {
                while (Time.timeScale <= 0f) yield return null;
                if (!owner || !owner.CanAct || owner.IsDodging || user.Weapon != this) { usingItem = false; yield break; }
                yield return null;
            }
            if (owner && owner.CanAct && user.Weapon == this)
            {
                user.ForgetHeld();
                MarkSpent();
                CatalogGear.ApplyUse(owner, definition);
                Destroy(gameObject);
            }
            usingItem = false;
        }

        public bool ApplyDamage(in HitInfo hit)
        {
            if (broken || hit.BreakPower <= 0f || spent) return false;
            Wear(hit.BreakPower, holder);
            return true;
        }

        public bool Wear(float amount, PlayerCombat user)
        {
            if (broken || amount <= 0f) return false;
            durabilityLeft = DurabilityLeft - amount;
            if (durabilityLeft > 0f) return false;
            Break(user);
            return true;
        }

        public void MarkSpent() => spent = true;

        public void Break(PlayerCombat user = null)
        {
            if (broken) return;
            broken = true;
            CancelUse();
            if (user && user.Weapon == this) user.ForgetHeld();
            transform.SetParent(World.Transient, true);
            if (!spent && TilePool.Instance) TilePool.Instance.Burst(word, transform.position + Vector3.up * 0.4f, 3f);
            Destroy(gameObject);
        }

        public void ShowRaised(bool raised) => SetPoseRotation(raised && word != "SHIELD" ? Quaternion.Euler(90f, 0f, 0f) : HoldRotation);

        /// <summary>Rotate around the authored grip, never around the model's floor pivot.</summary>
        public void SetPoseRotation(Quaternion localRotation)
        {
            transform.localRotation = localRotation;
            if (definition == null || !holder || holder.Weapon != this) return;
            var grip = definition.Grip;
            transform.localPosition = -(localRotation * new Vector3(grip[0], grip[1], grip[2])) * definition.HeldScale;
        }

        IEnumerator Swing()
        {
            var rest = HoldRotation;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.18f)
            {
                SetPoseRotation(rest * Quaternion.Euler(Mathf.Sin(t * Mathf.PI) * 70f, 0f, 0f));
                yield return null;
            }
            SetPoseRotation(rest);
        }
    }
}
