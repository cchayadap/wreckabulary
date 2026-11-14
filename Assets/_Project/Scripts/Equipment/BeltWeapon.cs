using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class BeltWeapon : MonoBehaviour, IMeleeContactShaper
    {
        public FlexibleWeaponDefinition Definition { get; private set; }
        public FlexibleWeaponProfile Profile { get; private set; }
        public StrikePart LastPart { get; private set; }

        public void Configure(FlexibleWeaponProfile profile, FlexibleWeaponDefinition definition)
        {
            Profile = profile;
            Definition = definition;
        }

        public MeleeStats Shape(MeleeStats stats, float distance, out StrikePart part)
        {
            part = Definition != null ? Definition.PartAt(distance) : StrikePart.Strap;
            LastPart = part;
            return Definition != null ? Definition.StatsFor(part) : stats;
        }
    }
}
