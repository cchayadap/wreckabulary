using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Wreckabulary.Rules
{
    public readonly struct SheetRow
    {
        public readonly string Label;
        public readonly string Value;

        public SheetRow(string label, string value)
        {
            Label = label;
            Value = value;
        }

        public override string ToString() => Label + ": " + Value;
    }

    public sealed class RecipeSheetText
    {
        public string Name;
        public RecipeTier Tier;
        public int Stars;
        public string TierName;
        public string Kind;
        public List<SheetRow> Rows = new List<SheetRow>();
        public List<string> Effects = new List<string>();
    }

    public static class RecipeSheet
    {
        public const float Width = 400f, Pad = 18f, LabelWidth = 140f, RowFont = 17f, MinFont = 15f;
        public const float NameFont = 30f, KindFont = 18f, EffectFont = 17f, Picture = 76f, HeadGap = 12f;
        public const float GlyphEm = .62f;
        public const int EffectLines = 2;
        public const string NoEffect = "No extra effect.";
        public const string Hand = "Hold", Uses = "Uses", Lasts = "Lasts", Toughness = "Toughness", SetUp = "Set up", Size = "Size";

        public static float ValueWidth => Width - 2f * Pad - LabelWidth;
        public static float TextWidth => Width - 2f * Pad;
        public static float KindWidth => Width - 2f * Pad - Picture - HeadGap;

        public static RecipeSheetText Build(ItemDefinition item, HouseTuning tuning)
        {
            if (item == null) return null;
            var tier = RecipeDifficulty.TierOf(item.Id);
            var (kind, rows) = Family(item);
            var sheet = new RecipeSheetText
            {
                Name = item.Id,
                Tier = tier,
                Stars = tuning != null ? RecipeStars.Count(tier) : 0,
                TierName = tier.ToString(),
                Kind = kind,
            };
            rows(item, sheet.Rows);
            if (item.Consumable) sheet.Rows.Add(new SheetRow(Uses, "Once"));
            sheet.Rows.Add(new SheetRow(Hand, item.IsTwoHanded ? "Two hands" : "One hand"));
            sheet.Effects.AddRange(Effects(item, tuning));
            return sheet;
        }

        public static string Kind(ItemDefinition item) => Family(item).kind;

        static (string kind, Action<ItemDefinition, List<SheetRow>> rows) Family(ItemDefinition item) => item.Family switch
        {
            HandlingFamily.MeleeSwing => ("Swing weapon", Swung),
            HandlingFamily.MeleeThrust => ("Reach weapon", Swung),
            HandlingFamily.Thrown => (item.Thrown != null && item.Thrown.FuseSeconds > 0f ? "Thrown blast" : "Thrown weapon", Thrown),
            HandlingFamily.Ranged => ("Ranged weapon", Ranged),
            HandlingFamily.Shield => ("Shield", Shield),
            HandlingFamily.DeployCover => ("Cover to place", Cover),
            HandlingFamily.DeployPad => ("Jump pad to place", Bouncy),
            HandlingFamily.DeploySpeed => ("Speed strip to place", Strip),
            HandlingFamily.DeployZone => (ZoneKind(item.Deploy), Zone),
            HandlingFamily.Buff => (item.Use != null && item.Use.Effect == UseEffect.Speed ? "Speed boost" : "Bubble shield", Buff),
            HandlingFamily.Heal => ("Heal", Heal),
            HandlingFamily.Utility => ("Team stash to place", Stash),
            _ => ("Gear", Any),
        };

        static string ZoneKind(DeployStats deploy) => deploy?.Effect switch
        {
            DeployEffect.SlipZone => "Slippery patch",
            DeployEffect.WindField => "Wind to place",
            DeployEffect.SlowField => "Slow field to place",
            _ => "Zone to place",
        };

        public static List<string> Effects(ItemDefinition item, HouseTuning tuning)
        {
            var effects = new List<string>();
            if (item != null && tuning != null)
                foreach (var trait in tuning.TraitsOf(item.Id)) effects.Add(Sentence(trait.Describe()));
            if (effects.Count == 0) effects.Add(NoEffect);
            return effects;
        }

        public static string Sentence(string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase)) return "";
            string text = phrase.Trim();
            text = char.ToUpperInvariant(text[0]) + text.Substring(1);
            return text.EndsWith(".", StringComparison.Ordinal) ? text : text + ".";
        }

        static void Swung(ItemDefinition item, List<SheetRow> rows)
        {
            var m = item.Melee;
            if (m == null) { Any(item, rows); return; }
            rows.Add(new SheetRow("Damage", N(m.Damage) + " a hit"));
            rows.Add(new SheetRow(item.Family == HandlingFamily.MeleeThrust ? "Jab time" : "Swing time", S(m.Cycle)));
            rows.Add(new SheetRow("Reach", N(m.Reach) + " m"));
            if (!item.Consumable) rows.Add(new SheetRow(Lasts, Count(item.Durability, "swing")));
        }

        static void Thrown(ItemDefinition item, List<SheetRow> rows)
        {
            var t = item.Thrown;
            if (t == null) { Any(item, rows); return; }
            if (t.FuseSeconds > 0f)
            {
                rows.Add(new SheetRow("Damage", $"{N(t.Damage)} centre, {N(t.EdgeDamage)} edge"));
                rows.Add(new SheetRow("Blast", $"{N(t.Radius)} m after {N(t.FuseSeconds)} s"));
            }
            else rows.Add(new SheetRow("Damage", N(t.Damage) + " a hit"));
            rows.Add(new SheetRow("Throw", $"{N(t.Speed)} m/s, {(t.Lob ? "lobbed" : "straight")}"));
            if (t.Recoverable) rows.Add(new SheetRow("After a throw", "Pick it up again"));
            if (!item.Consumable) rows.Add(new SheetRow(Toughness, N(item.Durability)));
        }

        static void Ranged(ItemDefinition item, List<SheetRow> rows)
        {
            var r = item.Ranged;
            if (r == null) { Any(item, rows); return; }
            rows.Add(new SheetRow("Damage", $"{N(r.Damage)} to {N(r.FullDamage)}"));
            rows.Add(new SheetRow("Shot speed", $"{N(r.Speed)} to {N(r.FullSpeed)} m/s"));
            rows.Add(new SheetRow("Full draw", S(r.DrawSeconds)));
            if (!item.Consumable) rows.Add(new SheetRow(Lasts, Count(item.Durability, "shot")));
        }

        static void Shield(ItemDefinition item, List<SheetRow> rows)
        {
            var s = item.Shield;
            if (s == null) { Any(item, rows); return; }
            rows.Add(new SheetRow("Blocks", $"{P(s.DamageReduction)} of damage"));
            rows.Add(new SheetRow("Front arc", s.FrontArcDegrees >= 359f ? "All round" : $"{N(s.FrontArcDegrees)} degrees"));
            rows.Add(new SheetRow("Move while up", $"{P(s.MoveSpeedMultiplier)} speed"));
            rows.Add(new SheetRow("Raise time", S(s.RaiseSeconds)));
            if (!item.Consumable) rows.Add(new SheetRow(Lasts, $"{N(item.Durability)} damage blocked"));
        }

        static void Cover(ItemDefinition item, List<SheetRow> rows)
        {
            Placed(item, rows);
            if (item.Melee != null) rows.Add(new SheetRow("Swing damage", N(item.Melee.Damage) + " a hit"));
        }

        static void Bouncy(ItemDefinition item, List<SheetRow> rows)
        {
            if (item.Deploy != null) rows.Add(new SheetRow("Bounce", N(item.Deploy.Strength) + " m/s up"));
            Placed(item, rows);
        }

        static void Strip(ItemDefinition item, List<SheetRow> rows)
        {
            if (item.Deploy != null) rows.Add(new SheetRow("Boost", $"x{N(item.Deploy.Strength)} speed"));
            Placed(item, rows);
        }

        static void Zone(ItemDefinition item, List<SheetRow> rows)
        {
            var d = item.Deploy;
            if (d == null) { Any(item, rows); return; }
            switch (d.Effect)
            {
                case DeployEffect.SlowField:
                    rows.Add(new SheetRow("Slows to", $"{P(d.Strength)} speed"));
                    rows.Add(new SheetRow(Size, N(d.Radius) + " m radius"));
                    break;
                case DeployEffect.WindField:
                    rows.Add(new SheetRow("Push", $"up to {N(d.Strength)} m/s"));
                    rows.Add(new SheetRow("Cone", $"{N(d.ArcDegrees)} degrees, {N(d.Radius)} m"));
                    break;
                default:
                    rows.Add(new SheetRow(Size, N(d.Radius) + " m radius"));
                    break;
            }
            if (d.LifetimeSeconds > 0f) rows.Add(new SheetRow(Lasts, S(d.LifetimeSeconds)));
            rows.Add(new SheetRow(SetUp, S(d.PlaceSeconds)));
            if (!item.Consumable) rows.Add(new SheetRow(Toughness, N(item.Durability)));
        }

        static void Buff(ItemDefinition item, List<SheetRow> rows)
        {
            var u = item.Use;
            if (u == null) { Any(item, rows); return; }
            if (u.Effect == UseEffect.Speed) rows.Add(new SheetRow("Speed", $"+{N((u.Amount - 1f) * 100f, "0")}%"));
            else rows.Add(new SheetRow("Bubble", N(u.Amount) + " HP shield"));
            if (u.Seconds > 0f) rows.Add(new SheetRow(Lasts, S(u.Seconds)));
            rows.Add(new SheetRow("Use time", S(u.ChannelSeconds)));
        }

        static void Heal(ItemDefinition item, List<SheetRow> rows)
        {
            var u = item.Use;
            if (u == null) { Any(item, rows); return; }
            rows.Add(new SheetRow("Heals", N(u.Amount) + " HP"));
            if (u.Seconds > 0f) rows.Add(new SheetRow(Lasts, S(u.Seconds)));
            rows.Add(new SheetRow("Use time", S(u.ChannelSeconds)));
        }

        static void Stash(ItemDefinition item, List<SheetRow> rows) => Placed(item, rows);

        static void Placed(ItemDefinition item, List<SheetRow> rows)
        {
            var d = item.Deploy;
            if (d == null) return;
            rows.Add(new SheetRow(Size, $"{N(d.FootprintX)} x {N(d.FootprintZ)} m"));
            rows.Add(new SheetRow(SetUp, S(d.PlaceSeconds)));
            if (!item.Consumable) rows.Add(new SheetRow(Toughness, N(item.Durability)));
        }

        static void Any(ItemDefinition item, List<SheetRow> rows)
        {
            if (item.Melee != null) rows.Add(new SheetRow("Damage", N(item.Melee.Damage) + " a hit"));
            else if (item.Thrown != null) rows.Add(new SheetRow("Damage", N(item.Thrown.Damage) + " a hit"));
            else if (item.Ranged != null) rows.Add(new SheetRow("Damage", $"{N(item.Ranged.Damage)} to {N(item.Ranged.FullDamage)}"));
            if (!item.Consumable && item.Durability > 0) rows.Add(new SheetRow(Toughness, N(item.Durability)));
        }

        public static bool[] Held(string word, IEnumerable<char> letters, bool free)
        {
            var held = new bool[word?.Length ?? 0];
            if (held.Length == 0) return held;
            var spare = letters != null ? letters.ToList() : new List<char>();
            for (int i = 0; i < held.Length; i++)
            {
                if (free) { held[i] = true; continue; }
                int at = spare.IndexOf(word[i]);
                held[i] = at >= 0;
                if (at >= 0) spare.RemoveAt(at);
            }
            return held;
        }

        public static int Missing(bool[] held) => held == null ? 0 : held.Count(h => !h);

        public static string LettersLine(int missing, bool free) =>
            free ? "Free to spell" : missing <= 0 ? "Ready to spell" : missing == 1 ? "1 letter missing" : $"{missing} letters missing";

        public static int MaxChars(float width, float fontSize) => fontSize > 0f ? (int)Math.Floor(width / (fontSize * GlyphEm)) : 0;

        public static string N(float value, string format = "0.##") => value.ToString(format, CultureInfo.InvariantCulture);

        static string S(float seconds) => N(seconds) + " s";

        static string P(float share) => N(share * 100f, "0") + "%";

        static string Count(int n, string unit) => n == 1 ? $"1 {unit}" : $"{n} {unit}s";

        static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
