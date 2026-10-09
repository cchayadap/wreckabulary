using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    [CreateAssetMenu(menuName = "Wreckabulary/Game Data")]
    public sealed class GameData : ScriptableObject
    {
        public const string ResourcePath = "GameData";

        [Tooltip("rules.json: limits and timings, with overrides per mode.")]
        public TextAsset rules;
        [Tooltip("items.json: the recipe catalogue.")]
        public TextAsset items;
        [Tooltip("house_*.json: the map's rooms, doors, spawns, furniture and clear-out orders.")]
        public TextAsset house;
        [Tooltip("Additional house maps. The filename house_<id>.json supplies the map id.")]
        public TextAsset[] maps = Array.Empty<TextAsset>();
        [Tooltip("wardrobe.json: the avatar's pieces and colourways.")]
        public TextAsset wardrobe;

        public GameConfig Parse()
        {
            var missing = new List<string>();
            if (!rules) missing.Add(nameof(rules));
            if (!items) missing.Add(nameof(items));
            if (!house) missing.Add(nameof(house));
            if (!wardrobe) missing.Add(nameof(wardrobe));
            if (missing.Count > 0)
                throw new InvalidOperationException($"{name} has no {string.Join(", ", missing)} file. Run Wreckabulary > Data > Set Up Game Data.");
            var additional = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var map in maps ?? Array.Empty<TextAsset>())
            {
                if (!map) throw new InvalidOperationException("GameData has an empty map reference.");
                string id = map.name.StartsWith("house_", StringComparison.Ordinal) ? map.name.Substring(6) : map.name;
                if (additional.ContainsKey(id)) throw new InvalidOperationException($"Duplicate map id '{id}'.");
                additional.Add(id, map.text);
            }
            return GameConfig.FromJson(rules.text, items.text, house.text, wardrobe.text, rules.name, items.name, house.name, wardrobe.name, additional);
        }
    }

    public sealed class GameConfig
    {
        public RuleBook Rules { get; }
        public ItemCatalogue Items { get; }
        public HouseLayout House { get; }
        public WardrobeCatalogue Wardrobe { get; }
        public IReadOnlyDictionary<string, HouseLayout> Houses { get; }

        static GameConfig current;

        public static GameConfig Current => current ??= Load();

        public static void Use(GameConfig config) => current = config;

        public GameConfig(RuleBook rules, ItemCatalogue items, HouseLayout house, WardrobeCatalogue wardrobe,
            IDictionary<string, HouseLayout> maps = null)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Items = items ?? throw new ArgumentNullException(nameof(items));
            House = house ?? throw new ArgumentNullException(nameof(house));
            Wardrobe = wardrobe ?? throw new ArgumentNullException(nameof(wardrobe));
            var all = new Dictionary<string, HouseLayout>(StringComparer.Ordinal) { ["pinwheel"] = house };
            foreach (var entry in maps ?? new Dictionary<string, HouseLayout>())
            {
                if (all.ContainsKey(entry.Key)) throw new ArgumentException($"Duplicate map id '{entry.Key}'.", nameof(maps));
                all.Add(entry.Key, entry.Value ?? throw new ArgumentException($"Map '{entry.Key}' is null.", nameof(maps)));
            }
            Houses = all;
        }

        public HouseLayout HouseFor(string id) => string.IsNullOrEmpty(id) ? House :
            Houses.TryGetValue(id, out var house) ? house : throw new KeyNotFoundException($"Unknown map '{id}'.");

        public static GameConfig Load()
        {
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            if (!data) throw new InvalidOperationException($"Resources/{GameData.ResourcePath} is missing. Run Wreckabulary > Data > Set Up Game Data.");
            var config = data.Parse();
            Art.SeasonalCollection.Winter?.Apply(config.Items);
            return config;
        }

        public static GameConfig FromJson(string rules, string items, string house, string wardrobe,
            string rulesName = "rules.json", string itemsName = "items.json", string houseName = "house.json", string wardrobeName = "wardrobe.json",
            IDictionary<string, string> maps = null)
        {
            var config = new GameConfig(
                RuleBook.FromJson(rules, rulesName),
                ItemCatalogue.FromJson(items, itemsName),
                HouseLayout.FromJson(house, houseName),
                WardrobeCatalogue.FromJson(wardrobe, wardrobeName),
                maps?.ToDictionary(m => m.Key, m => HouseLayout.FromJson(m.Value, $"house_{m.Key}.json")));
            var problems = config.Validate();
            if (problems.Count > 0)
                throw new InvalidOperationException($"The game data has {problems.Count} problem(s):\n" + string.Join("\n", problems));
            return config;
        }

        public GameRules RulesFor(string mode) => Rules.Modes.Contains(mode) ? Rules.For(mode) : Rules.Defaults;

        public List<string> Validate()
        {
            var problems = new List<string>();
            problems.AddRange(Rules.Validate().Select(p => "rules: " + p));
            foreach (var rules in AllRules())
                foreach (string p in Items.Validate(rules.MaxLetters))
                    if (!problems.Contains("items: " + p)) problems.Add("items: " + p);
            foreach (var entry in Houses)
                problems.AddRange(entry.Value.Validate(Items).Select(p => $"house_{entry.Key}: " + p));
            problems.AddRange(Wardrobe.Validate().Select(p => "wardrobe: " + p));

            foreach (var rules in AllRules())
            {
                string mode = rules.Mode;
                foreach (var entry in Houses)
                    if (rules.ClearOutEnabled && !entry.Value.ClearOutOrders.ContainsKey(mode))
                        problems.Add($"rules: {mode} turns the clear-out on, but {entry.Value.Name} has no clear-out order for it");
                if (rules.StarterLetters.Length > rules.MaxLetters)
                    problems.Add($"rules: {mode} starts players with {rules.StarterLetters.Length} letters but a bag holds {rules.MaxLetters}");
                if (rules.StarterLetters.Length > 0 && !LetterBag.IsWord(rules.StarterLetters))
                    problems.Add($"rules: {mode} starter letters must be A-Z");
            }
            foreach (var entry in Houses)
            {
                foreach (string mode in entry.Value.ClearOutOrders.Keys)
                    if (!Rules.Modes.Contains(mode))
                        problems.Add($"house_{entry.Key}: clear-out order '{mode}' is for a mode rules.json doesn't have");
                foreach (var f in entry.Value.Furniture)
                    if (Items.TryGet(f.Word, out var item) && string.IsNullOrEmpty(item.Model))
                        problems.Add($"house_{entry.Key}: {f.Word} stands in {f.Room}, but its item has no model");
            }
            return problems;
        }

        IEnumerable<GameRules> AllRules() => new[] { Rules.Defaults }.Concat(Rules.Modes.Select(Rules.For));
    }
}
