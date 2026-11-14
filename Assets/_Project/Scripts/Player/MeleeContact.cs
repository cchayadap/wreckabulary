using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public interface IMeleeContactShaper
    {
        MeleeStats Shape(MeleeStats stats, float distance, out StrikePart part);
    }

    public readonly struct MeleeContact
    {
        public readonly PlayerController Attacker;
        public readonly string ItemId;
        public readonly int Swing;
        public readonly Rigidbody Target;
        public readonly StrikePart Part;
        public readonly Vector3 Point;
        public readonly ImpactResult Result;
        public readonly MeleeStats Stats;
        public readonly int Stage;

        public MeleeContact(PlayerController attacker, string itemId, int swing, Rigidbody target, StrikePart part, Vector3 point,
            ImpactResult result, MeleeStats stats, int stage)
        {
            Attacker = attacker;
            ItemId = itemId;
            Swing = swing;
            Target = target;
            Part = part;
            Point = point;
            Result = result;
            Stats = stats;
            Stage = stage;
        }

        public string TargetId => Target ? "rb" + Target.GetEntityId() : "none";
        public string Id => $"{(Attacker ? Attacker.GetEntityId().ToString() : "0")}:{Swing}:{TargetId}";
    }
}
