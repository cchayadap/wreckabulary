using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public enum FactKind { Role, Bleed, Heavy, Daze, Guard, Swift, Crush }

    public readonly struct RecipeChip
    {
        public readonly string Label;
        public readonly FactKind Kind;

        public RecipeChip(string label, FactKind kind)
        {
            Label = label;
            Kind = kind;
        }

        public override string ToString() => Label;
    }

    public readonly struct RecipeBar
    {
        public readonly string Label;
        public readonly float Fill;

        public RecipeBar(string label, float fill)
        {
            Label = label;
            Fill = Math.Max(0f, Math.Min(1f, fill));
        }
    }

    public static class RecipeFacts
    {
        public const int MaxChips = 2;

        public static List<RecipeChip> Chips(ItemDefinition item, HouseTuning tuning)
        {
            var chips = new List<RecipeChip>();
            if (item == null) return chips;
            if (tuning != null)
                foreach (var trait in tuning.TraitsOf(item.Id))
                    if (chips.Count < MaxChips) chips.Add(new RecipeChip(TraitLabel(trait), (FactKind)Enum.Parse(typeof(FactKind), trait.Kind.ToString())));
            if (chips.Count == 0)
            {
                string role = RoleLabel(item);
                if (role != null) chips.Add(new RecipeChip(role, FactKind.Role));
            }
            return chips;
        }

        public static string TraitLabel(ItemTrait trait) => trait.Delivery switch
        {
            TraitDelivery.BuildUp => $"{trait.Kind} {Ordinal(trait.Every)}",
            TraitDelivery.Chance => $"{trait.Kind} {trait.Chance * 100f:0}%",
            _ => trait.Kind.ToString(),
        };

        public static string RoleLabel(ItemDefinition item)
        {
            if (item.Use != null)
                return item.Use.Effect switch
                {
                    UseEffect.Heal => $"+{item.Use.Amount:0} HP",
                    UseEffect.Bubble => "Bubble",
                    UseEffect.Speed => "Speed boost",
                    _ => null,
                };
            if (item.Ranged != null) return "Hold to draw";
            if (item.Shield != null) return item.Shield.FrontArcDegrees >= 359f ? "Block all round" : "Block front";
            if (item.Deploy != null)
                return item.Deploy.Effect switch
                {
                    DeployEffect.Cover => "Cover",
                    DeployEffect.JumpPad => "Jump pad",
                    DeployEffect.SpeedStrip => "Speed strip",
                    DeployEffect.SlipZone => "Slippery",
                    DeployEffect.WindField => "Wind",
                    DeployEffect.SlowField => "Slow field",
                    DeployEffect.Stash => "Team stash",
                    _ => null,
                };
            if (item.Thrown != null) return item.Thrown.FuseSeconds > 0f ? "Blast" : item.Consumable ? "One throw" : "Throw";
            return null;
        }

        public static List<RecipeBar> Bars(ItemDefinition item, IEnumerable<ItemDefinition> catalogue, HouseTuning tuning)
        {
            var bars = new List<RecipeBar>();
            if (item == null) return bars;
            var all = (catalogue ?? Enumerable.Empty<ItemDefinition>()).Where(i => i.Enabled).ToList();
            if (item.Melee != null && item.Use == null && item.Thrown == null && item.Deploy == null && item.Shield == null)
            {
                bars.Add(new RecipeBar("DMG", Share(Dps(item, tuning), all.Where(Swung).Select(i => Dps(i, tuning)))));
                bars.Add(new RecipeBar("REACH", Share(item.Melee.Reach, all.Where(Swung).Select(i => i.Melee.Reach))));
            }
            else if (item.Ranged != null || (item.Thrown != null && item.Use == null))
            {
                var shots = all.Where(i => i.Ranged != null || (i.Thrown != null && i.Use == null)).ToList();
                bars.Add(new RecipeBar("DMG", Share(ShotDamage(item), shots.Select(ShotDamage))));
                bars.Add(new RecipeBar("SPEED", Share(ShotSpeed(item), shots.Select(ShotSpeed))));
            }
            return bars;
        }

        static bool Swung(ItemDefinition i) => i.Melee != null && i.Use == null && i.Thrown == null && i.Deploy == null && i.Shield == null;
        static float Dps(ItemDefinition i, HouseTuning tuning) => tuning != null ? tuning.MeleeDps(i) : i.Melee.Damage / Math.Max(0.01f, i.Melee.Cycle);
        static float ShotDamage(ItemDefinition i) => i.Ranged != null ? i.Ranged.FullDamage : i.Thrown.Damage;
        static float ShotSpeed(ItemDefinition i) => i.Ranged != null ? i.Ranged.FullSpeed : i.Thrown.Speed;

        static float Share(float value, IEnumerable<float> rivals)
        {
            float best = rivals.DefaultIfEmpty(value).Max();
            return best > 0f ? value / best : 0f;
        }

        static string Ordinal(int n) => n + (n % 100 is >= 11 and <= 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" });
    }
}
