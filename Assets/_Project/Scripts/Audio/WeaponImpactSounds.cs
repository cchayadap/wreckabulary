using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary.Audio
{
    [CreateAssetMenu(menuName = "Wreckabulary/Audio/Weapon Impact Sounds", fileName = "WeaponImpactSounds")]
    public sealed class WeaponImpactSounds : ScriptableObject
    {
        [Tooltip("Item word such as BELT or BAT. Default answers for every weapon without its own sounds.")]
        public string weapon = ImpactCues.DefaultWeapon;
        [Tooltip("Played when the swing starts, hit or miss.")]
        public ImpactSound swing = new ImpactSound { volume = 0.7f, cooldown = 0.1f };
        [Tooltip("Played only for a confirmed contact. Any covers every part of this weapon.")]
        public List<ContactSound> contacts = new List<ContactSound>();
    }
}
