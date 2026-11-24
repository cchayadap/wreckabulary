using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public enum SpellRole { Objective, Weapon, Shot, Shield, Heal, Boost, Gadget }

    public readonly struct SpellNeeds : IEquatable<SpellNeeds>
    {
        public const float HurtAt = .5f;
        public static readonly SpellNeeds Unarmed = default;

        public readonly bool Armed, Guarded, Hurt;

        public SpellNeeds(bool armed, bool guarded, bool hurt)
        {
            Armed = armed;
            Guarded = guarded;
            Hurt = hurt;
        }

        public int Bits => (Armed ? 1 : 0) | (Guarded ? 2 : 0) | (Hurt ? 4 : 0);

        public bool Equals(SpellNeeds other) => Bits == other.Bits;
        public override bool Equals(object obj) => obj is SpellNeeds other && Equals(other);
        public override int GetHashCode() => Bits;
        public override string ToString() => $"armed {Armed}, guarded {Guarded}, hurt {Hurt}";
    }

    public static class SpellChoice
    {
        public static SpellRole RoleOf(ItemDefinition item)
        {
            if (item == null) return SpellRole.Gadget;
            if (item.Use != null) return item.Use.Effect is UseEffect.Heal or UseEffect.Bubble ? SpellRole.Heal : SpellRole.Boost;
            if (item.Shield != null) return SpellRole.Shield;
            if (item.Ranged != null) return SpellRole.Weapon;
            if (item.Thrown != null) return item.Consumable || !item.Thrown.Recoverable ? SpellRole.Shot : SpellRole.Weapon;
            if (item.Deploy != null) return SpellRole.Gadget;
            return item.Melee != null ? SpellRole.Weapon : SpellRole.Gadget;
        }

        public static bool Fights(SpellRole role) => role is SpellRole.Weapon or SpellRole.Shot;

        public static int Rank(SpellRole role, SpellNeeds needs) => role switch
        {
            SpellRole.Objective => 0,
            SpellRole.Heal when needs.Hurt => 1,
            SpellRole.Weapon when !needs.Armed => 2,
            SpellRole.Shield when needs.Armed && !needs.Guarded => 2,
            SpellRole.Shot when !needs.Armed => 3,
            SpellRole.Gadget => 5,
            _ => 4,
        };

        public static bool BotWants(SpellRole role, SpellNeeds needs) =>
            needs.Armed ? role == SpellRole.Shield && !needs.Guarded : Fights(role);

        public static bool BotWants(SpellRole role, SpellNeeds needs, string word, string goal) =>
            string.IsNullOrEmpty(goal) ? BotWants(role, needs) : word == goal;

        public static IReadOnlyCollection<string> WithGoal(IReadOnlyCollection<string> objectives, string goal)
        {
            if (string.IsNullOrEmpty(goal) || (objectives != null && objectives.Contains(goal))) return objectives;
            var all = objectives != null ? new HashSet<string>(objectives, StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
            all.Add(goal);
            return all;
        }

        public static List<T> Order<T>(IEnumerable<T> options, Func<T, string> word, Func<T, SpellRole> role, SpellNeeds needs) =>
            options.OrderBy(o => Rank(role(o), needs)).ThenByDescending(o => RecipeDifficulty.Score(word(o))).ToList();
    }
}
