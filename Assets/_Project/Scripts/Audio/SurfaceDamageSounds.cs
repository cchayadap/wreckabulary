using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary.Audio
{
    [CreateAssetMenu(menuName = "Wreckabulary/Audio/Surface Damage Sounds", fileName = "SurfaceDamageSounds")]
    public sealed class SurfaceDamageSounds : ScriptableObject
    {
        public SurfaceMaterial surface = SurfaceMaterial.Wood;
        [Tooltip("Played when a hit is confirmed to crack, dent or splinter the object without breaking it.")]
        public ImpactSound damaged = new ImpactSound();
        [Tooltip("Played only when the object is confirmed broken: shatter, splinter or debris.")]
        public ImpactSound broken = new ImpactSound { volume = 1f, cooldown = 0.2f };
    }
}
