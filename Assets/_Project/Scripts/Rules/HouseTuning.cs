using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public enum RecipeTier { Common, Uncommon, Rare, Epic }

    public enum TraitKind { Bleed, Heavy, Daze, Guard, Swift, Crush }

    public enum TraitDelivery { EveryHit, Chance, BuildUp, OverTime, WhileHeld }

    public sealed class ItemTrait
    {
        public TraitKind Kind;
        public float Amount;
        public float Seconds;
        public float Chance = 1f;
        public int Every = 1;
        public int Stacks = 1;

        public bool Held => Kind is TraitKind.Guard or TraitKind.Swift;

        public TraitDelivery Delivery => Held ? TraitDelivery.WhileHeld
            : Every > 1 ? TraitDelivery.BuildUp
            : Chance < 1f ? TraitDelivery.Chance
            : Kind == TraitKind.Bleed ? TraitDelivery.OverTime
            : TraitDelivery.EveryHit;

        public string Describe()
        {
            string effect = Kind switch
            {
                TraitKind.Bleed => Stacks > 1 ? $"bleeds {Amount:0.#}/s for {Seconds:0.#} s, cuts deepen x{Stacks}" : $"bleeds {Amount:0.#}/s for {Seconds:0.#} s",
                TraitKind.Heavy => $"knocks back x{Amount:0.##}",
                TraitKind.Daze => $"slows to {Amount * 100f:0}% for {Seconds:0.#} s",
                TraitKind.Guard => $"-{Amount * 100f:0}% damage",
                TraitKind.Swift => $"+{(Amount - 1f) * 100f:0}% speed",
                TraitKind.Crush => $"crushes x{Amount:0.##}",
                _ => Kind.ToString().ToLowerInvariant(),
            };
            return Delivery switch
            {
                TraitDelivery.WhileHeld => "while held: " + effect,
                TraitDelivery.BuildUp => $"every {Ordinal(Every)} hit {effect}",
                TraitDelivery.Chance => $"{Chance * 100f:0}% chance: {effect}",
                _ => effect,
            };
        }

        static string Ordinal(int n) => n + (n % 100 is >= 11 and <= 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" });
    }

    public static class RecipeDifficulty
    {
        static readonly int[] Values = { 1, 3, 3, 2, 1, 4, 2, 4, 1, 8, 5, 1, 3, 1, 1, 3, 10, 1, 1, 1, 1, 4, 4, 8, 4, 10 };

        public static int LetterValue(char c)
        {
            int i = char.ToUpperInvariant(c) - 'A';
            return i >= 0 && i < 26 ? Values[i] : 0;
        }

        public static int Score(string word) => word.Sum(LetterValue) + Math.Max(0, word.Length - 5);

        public static RecipeTier TierOf(string word)
        {
            int score = Score(word);
            return score >= 13 ? RecipeTier.Epic : score >= 10 ? RecipeTier.Rare : score >= 7 ? RecipeTier.Uncommon : RecipeTier.Common;
        }

        public static int TraitBudget(RecipeTier tier) => (int)tier;

        public static float MaxMeleeDps(RecipeTier tier) => tier switch { RecipeTier.Common => 21f, RecipeTier.Uncommon => 26f, RecipeTier.Rare => 29f, _ => 32f };
        public static float MaxThrownDamage(RecipeTier tier) => tier switch { RecipeTier.Common => 14f, RecipeTier.Uncommon => 22f, RecipeTier.Rare => 45f, _ => 50f };
    }

    public sealed class HouseTuning
    {
        public const float UnarmedFloor = 1f;

        public float? UnarmedDamage;
        public float? LettersPerHundredSquareMetres;
        public int MinLetters, MaxLetters;
        readonly Dictionary<string, JsonNode> timers = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        static readonly string[] TimerKeys = { "roundTimeLimit", "clearOut" };
        static readonly string[] ClearOutTimerKeys = { "firstAt", "interval", "warn", "fill" };
        public IEnumerable<string> TimedModes => timers.Keys;
        readonly Dictionary<string, JsonNode> patches = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        readonly Dictionary<string, List<ItemTrait>> traits = new Dictionary<string, List<ItemTrait>>(StringComparer.Ordinal);
        static readonly IReadOnlyList<ItemTrait> None = Array.Empty<ItemTrait>();

        public IReadOnlyList<ItemTrait> TraitsOf(string id) => id != null && traits.TryGetValue(id, out var list) ? list : None;
        public ItemTrait Trait(string id, TraitKind kind)
        {
            if (id == null || !traits.TryGetValue(id, out var list)) return null;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Kind == kind) return list[i];
            return null;
        }
        public string Describe(string id) => string.Join(" · ", TraitsOf(id).Select(t => t.Describe()));

        public int LettersInPlay(HouseLayout house, int fallback)
        {
            if (house == null || !LettersPerHundredSquareMetres.HasValue) return fallback;
            int wanted = (int)Math.Round(house.FloorArea * LettersPerHundredSquareMetres.Value / 100f);
            return Math.Max(MinLetters, Math.Min(MaxLetters, wanted));
        }

        public static HouseTuning FromJson(string json, string source = "items_house.json")
        {
            var tuning = new HouseTuning();
            var root = Json.Parse(json, source);
            if (!root.Has("tuning")) return tuning;
            var t = root["tuning"];
            if (t.Has("unarmed")) tuning.UnarmedDamage = t["unarmed"]["damage"].Float();
            if (t.Has("modes"))
                foreach (string mode in t["modes"].Keys)
                    tuning.timers[mode] = Timers(t["modes"][mode]);
            if (t.Has("deliveries"))
            {
                var d = t["deliveries"];
                tuning.LettersPerHundredSquareMetres = d["lettersPerHundredSquareMetres"].Float();
                tuning.MinLetters = d["minLetters"].Int();
                tuning.MaxLetters = d["maxLetters"].Int();
                if (tuning.LettersPerHundredSquareMetres <= 0f || tuning.MinLetters < 1 || tuning.MaxLetters < tuning.MinLetters)
                    throw d.Error("deliveries need a positive density and 1 <= minLetters <= maxLetters");
            }
            if (!t.Has("items")) return tuning;
            foreach (string id in t["items"].Keys)
            {
                var entry = t["items"][id];
                if (entry.Has("melee") || entry.Has("thrown")) tuning.patches[id] = entry;
                if (!entry.Has("traits")) continue;
                tuning.traits[id] = entry["traits"].Items.Select(n => new ItemTrait
                {
                    Kind = ItemCatalogue.ParseEnum<TraitKind>(n["kind"]),
                    Amount = n["amount"].Float(),
                    Seconds = n["seconds"].Float(0f),
                    Chance = n["chance"].Float(1f),
                    Every = n["every"].Int(1),
                    Stacks = n["stacks"].Int(1),
                }).ToList();
            }
            return tuning;
        }

        static JsonNode Timers(JsonNode mode)
        {
            foreach (string key in mode.Keys)
                if (Array.IndexOf(TimerKeys, key) < 0) throw mode.Error($"'{key}' is not a House timer; only roundTimeLimit and clearOut timings follow the house size");
            if (mode.Has("roundTimeLimit") && mode["roundTimeLimit"].Float() <= 0f) throw mode.Error("roundTimeLimit must be positive");
            if (mode.Has("clearOut"))
                foreach (string key in mode["clearOut"].Keys)
                {
                    if (Array.IndexOf(ClearOutTimerKeys, key) < 0) throw mode["clearOut"].Error($"'{key}' is not a clear-out timing");
                    if (mode["clearOut"][key].Float() <= 0f) throw mode["clearOut"].Error($"{key} must be positive");
                }
            return mode;
        }

        public void Apply(ItemCatalogue items, RuleBook rules)
        {
            if (UnarmedDamage.HasValue)
                foreach (var r in new[] { rules.Defaults }.Concat(rules.Modes.Select(rules.For))) r.Unarmed.Damage = UnarmedDamage.Value;
            foreach (var timer in timers)
            {
                if (!rules.Modes.Contains(timer.Key)) throw new ArgumentException($"Tuning times unknown mode '{timer.Key}'.");
                rules.Override(timer.Key, timer.Value);
            }
            foreach (var patch in patches)
            {
                if (!items.TryGet(patch.Key, out var item)) throw new ArgumentException($"Tuning names unknown item '{patch.Key}'.");
                if (patch.Value.Has("melee"))
                {
                    if (item.Melee == null) throw new ArgumentException($"Tuning gives '{patch.Key}' melee stats it does not have.");
                    item.Melee.Damage = patch.Value["melee"]["damage"].Float(item.Melee.Damage);
                    item.Melee.Knockback = patch.Value["melee"]["knockback"].Float(item.Melee.Knockback);
                }
                if (patch.Value.Has("thrown"))
                {
                    if (item.Thrown == null) throw new ArgumentException($"Tuning gives '{patch.Key}' thrown stats it does not have.");
                    item.Thrown.Damage = patch.Value["thrown"]["damage"].Float(item.Thrown.Damage);
                    item.Thrown.Knockback = patch.Value["thrown"]["knockback"].Float(item.Thrown.Knockback);
                }
            }
        }

        public List<string> Validate(ItemCatalogue items, GameRules defaults)
        {
            var problems = new List<string>();
            if (defaults.Unarmed.Damage < UnarmedFloor) problems.Add("Bare hands must still hurt a little.");
            foreach (string id in traits.Keys.Concat(patches.Keys).Distinct())
                if (!items.TryGet(id, out var named) || !named.Enabled) problems.Add($"Tuning names '{id}', which is not an enabled recipe.");
            foreach (var item in items.Enabled)
            {
                var tier = RecipeDifficulty.TierOf(item.Id);
                var list = TraitsOf(item.Id);
                if (list.Count > RecipeDifficulty.TraitBudget(tier)) problems.Add($"{item.Id} is {tier} and may carry {RecipeDifficulty.TraitBudget(tier)} trait(s), not {list.Count}.");
                if (list.Select(t => t.Kind).Distinct().Count() != list.Count) problems.Add($"{item.Id} lists a trait twice.");
                foreach (var t in list)
                {
                    string bad = t.Kind switch
                    {
                        TraitKind.Bleed when t.Amount <= 0f || t.Amount > 3f || t.Seconds <= 0f || t.Seconds > 4f => "bleed needs 0-3 damage a second for up to 4 s",
                        TraitKind.Bleed when t.Stacks < 1 || t.Stacks > 3 || t.Amount * t.Stacks > 4.5f => "a bleed deepens at most 3 times and to at most 4.5 damage a second",
                        TraitKind.Heavy when t.Amount <= 1f || t.Amount > 1.5f => "heavy multiplies knockback by more than 1 and at most 1.5",
                        TraitKind.Daze when t.Amount < .5f || t.Amount >= 1f || t.Seconds <= 0f || t.Seconds > 2.5f => "daze slows to 50-99% for up to 2.5 s",
                        TraitKind.Guard when t.Amount <= 0f || t.Amount > .2f => "guard cuts at most 20% of damage",
                        TraitKind.Swift when t.Amount <= 1f || t.Amount > 1.15f => "swift adds at most 15% speed",
                        TraitKind.Crush when t.Amount <= 1f || t.Amount > 2f || t.Every < 2 => "crush multiplies a build-up blow (every 2nd hit or rarer) by at most 2",
                        _ when t.Chance <= 0f || t.Chance > 1f => "chance must be above 0 and at most 1",
                        _ when t.Every < 1 || t.Every > 5 => "a build-up lands every 1st to 5th hit",
                        _ when t.Kind != TraitKind.Bleed && t.Stacks != 1 => "only a bleed can stack",
                        _ when t.Held && (t.Chance < 1f || t.Every > 1) => "a held trait is constant; it has no chance or build-up",
                        _ => null,
                    };
                    if (bad != null) problems.Add($"{item.Id}: {bad}.");
                    if (!t.Held && item.Melee == null && item.Thrown == null)
                        problems.Add($"{item.Id}: {t.Kind} needs a hit to land; it has no melee or thrown stats.");
                    if (t.Kind == TraitKind.Crush && item.Melee == null)
                        problems.Add($"{item.Id}: crush is a melee build-up; it has no melee stats.");
                }
                var crush = list.FirstOrDefault(t => t.Kind == TraitKind.Crush);
                float average = crush == null ? 1f : 1f + (crush.Amount - 1f) / crush.Every;
                if (item.Melee != null && item.Family is HandlingFamily.MeleeSwing or HandlingFamily.MeleeThrust
                    && item.Melee.Damage * average / item.Melee.Cycle > RecipeDifficulty.MaxMeleeDps(tier) + 1e-3f)
                    problems.Add($"{item.Id} is {tier}: {item.Melee.Damage * average / item.Melee.Cycle:0.0} melee damage a second is over its {RecipeDifficulty.MaxMeleeDps(tier)}.");
                if (item.Thrown != null && item.Thrown.Damage > RecipeDifficulty.MaxThrownDamage(tier) + 1e-3f)
                    problems.Add($"{item.Id} is {tier}: {item.Thrown.Damage} thrown damage is over its {RecipeDifficulty.MaxThrownDamage(tier)}.");
            }
            return problems;
        }
    }
}
