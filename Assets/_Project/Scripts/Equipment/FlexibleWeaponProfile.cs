using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    [CreateAssetMenu(menuName = "Wreckabulary/Equipment/Flexible Weapon Profile", fileName = "BeltPrototype")]
    public sealed class FlexibleWeaponProfile : ScriptableObject
    {
        [Header("Model")]
        [Tooltip("Visual prefab: the skinned BELT model only (no colliders or scripts).")]
        public GameObject visual;
        [Tooltip("Leather per skin, in order Classic, Candy, Arcade. Slot 2 keeps the shared gold.")]
        public Material[] leather = new Material[3];

        [Header("Strap hit (inner part)")]
        [Range(6f, 28f)] public float strapDamage = 10f;
        [Range(0f, 10f)] public float strapKnockback = 4f;
        [Range(0f, 4.5f)] public float strapBreakPower = 0.6f;
        [Range(0f, 0.45f)] public float strapHitStun = 0.22f;

        [Header("Buckle hit (outer end)")]
        [Range(6f, 28f)] public float buckleDamage = 14f;
        [Range(0f, 10f)] public float buckleKnockback = 5.5f;
        [Range(0f, 4.5f)] public float buckleBreakPower = 1.5f;
        [Range(0f, 0.45f)] public float buckleHitStun = 0.3f;
        [Tooltip("Share of the strap, from the hand, before the buckle zone begins.")]
        [Range(0.55f, 0.95f)] public float buckleFrom = 0.75f;

        [Header("Swing")]
        [Range(1f, 2.2f)] public float reach = 1.85f;
        [Range(30f, 145f)] public float arc = 120f;
        [Range(0.12f, 0.6f)] public float windup = 0.22f;
        [Range(0.04f, 0.2f)] public float active = 0.1f;
        [Range(0.2f, 0.8f)] public float recovery = 0.34f;
        [Min(1)] public int durability = 22;

        [Header("Strap bend (presentation)")]
        [Tooltip("Total bend at the tip while the strap trails the wind-up.")]
        [Range(0f, 90f)] public float maxLag = 48f;
        [Tooltip("Whip-through past straight at contact.")]
        [Range(0f, 20f)] public float lead = 8f;
        [Range(1f, 20f)] public float recoveryDamping = 7f;
        [Range(0.5f, 6f)] public float recoveryHertz = 2.4f;
        [Range(0f, 10f)] public float idleSway = 3f;
        [Tooltip("Natural curve of the strap while it hangs from the hand at rest.")]
        [Range(0f, 40f)] public float heldDroop = 8f;
        [Tooltip("Largest bend one strap joint may take.")]
        [Range(5f, 35f)] public float jointLimit = 35f;
        [Tooltip("Flip if the strap bends the wrong way relative to the swing.")]
        public bool invertBend;

        public FlexibleWeaponDefinition Definition()
        {
            var d = FlexibleWeaponDefinition.Belt();
            d.Melee = new MeleeStats
            {
                Damage = strapDamage, Reach = reach, ArcDegrees = arc, Windup = windup, Active = active, Recovery = recovery,
                Knockback = strapKnockback, BreakPower = strapBreakPower, HitStun = strapHitStun,
            };
            d.BuckleMelee = d.Tip(buckleDamage, buckleKnockback, buckleBreakPower, buckleHitStun);
            d.BuckleFrom = buckleFrom;
            d.Durability = durability;
            d.Swing.MaxLagDegrees = maxLag;
            d.Swing.LeadDegrees = lead;
            d.Swing.RecoveryDamping = recoveryDamping;
            d.Swing.RecoveryHertz = recoveryHertz;
            d.Swing.IdleSwayDegrees = idleSway;
            d.RebuildZones();
            return d;
        }

        public List<string> Validate()
        {
            var problems = Definition().Validate();
            if (!visual) problems.Add("Assign the BELT visual prefab.");
            if (leather == null || leather.Length != 3) problems.Add("List three leather skins: Classic, Candy, Arcade.");
            else for (int i = 0; i < 3; i++) if (!leather[i]) problems.Add($"Leather skin {i + 1} is empty.");
            return problems;
        }

        public Material LeatherFor(string skin)
        {
            int i = skin == "Candy" ? 1 : skin == "Arcade" ? 2 : 0;
            return leather != null && leather.Length > i && leather[i] ? leather[i] : null;
        }
    }
}
