using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary.Audio
{
    public static class ImpactAudioBridge
    {
        static int scopes;
        static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            scopes = 0;
            hooked = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            if (hooked) return;
            hooked = true;
            PlayerCombat.Contacted -= OnContact;
            HeldWeapon.MeleeStarted -= OnSwing;
            Smashable.AnyBroken -= OnBroken;
            PlayerCombat.Contacted += OnContact;
            HeldWeapon.MeleeStarted += OnSwing;
            Smashable.AnyBroken += OnBroken;
        }

        public static bool Active
        {
            get
            {
                if (scopes <= 0 && !HouseModes) return false;
                return ImpactAudioPlayer.Instance;
            }
        }

        static bool HouseModes
        {
            get
            {
                var library = Resources.Load<ImpactAudioLibrary>(ImpactAudioLibrary.ResourcePath);
                return library && library.useInHouseModes;
            }
        }

        internal static void Enter() { scopes++; Hook(); }
        internal static void Exit() => scopes = Mathf.Max(0, scopes - 1);

        public static bool VoicesSwing(HeldWeapon weapon)
        {
            if (!weapon || weapon.Definition?.Melee == null || !Active) return false;
            var library = ImpactAudioPlayer.Instance.Library;
            return library.Has(ImpactCues.SwingKey(weapon.word)) || library.Has(ImpactCues.SwingKey(ImpactCues.DefaultWeapon));
        }

        public static bool VoicesBreak(Smashable smashable)
        {
            return smashable && Active && ImpactAudioPlayer.Instance.Library.Has(ImpactCues.BreakKey(SurfaceAudioTag.Resolve(smashable)));
        }

        public static bool VoicesHit(in HitInfo hit)
        {
            if (hit.Source != HitSource.Melee && hit.Source != HitSource.Unarmed) return false;
            if (!Active) return false;
            var library = ImpactAudioPlayer.Instance.Library;
            return ImpactCues.Resolve(ImpactCues.ContactFallbacks(hit.ItemId, StrikePart.Body, SurfaceMaterial.Body), library.Has) != null;
        }

        static void OnSwing(HeldWeapon weapon, PlayerController owner)
        {
            if (!weapon || !owner || !Active) return;
            ImpactAudioPlayer.Swing(weapon.word, $"{owner.GetEntityId()}:{weapon.GetEntityId()}:{weapon.SwingStartedAt:0.0000}",
                weapon.transform.position);
        }

        static void OnContact(MeleeContact contact)
        {
            if (!contact.Target || !Active) return;
            var surface = SurfaceFor(contact.Target);
            float strength = ImpactCues.Strength(contact.Stats, contact.Stats?.Damage ?? 0f);
            var e = new ImpactEvent(contact.Id, contact.ItemId, contact.Part, surface, contact.Result, strength, contact.TargetId, contact.Stage);
            ImpactAudioPlayer.Impact(e, contact.Point, contact.Attacker ? "p" + contact.Attacker.GetEntityId() : null, contact.TargetId);
        }

        static void OnBroken(Smashable smashable)
        {
            if (!smashable || !Active) return;
            var body = smashable.GetComponent<Rigidbody>();
            string target = body ? "rb" + body.GetEntityId() : "rb" + smashable.GetEntityId();
            var centre = body ? body.worldCenterOfMass : smashable.transform.position;
            ImpactAudioPlayer.Break(target, SurfaceAudioTag.Resolve(smashable), 0.8f, centre);
        }

        public static SurfaceMaterial SurfaceFor(Rigidbody target)
        {
            if (target.TryGetComponent(out PlayerHealth health))
            {
                var shield = health.RaisedShield;
                if (shield != null)
                {
                    var combat = target.GetComponent<PlayerCombat>();
                    return combat && combat.Weapon && combat.Weapon.word == "PLATE" ? SurfaceMaterial.Ceramic : SurfaceMaterial.Metal;
                }
                return SurfaceMaterial.Body;
            }
            return SurfaceAudioTag.Resolve(target);
        }
    }
}
