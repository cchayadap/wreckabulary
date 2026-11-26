using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules
{
    public sealed class HelperBook
    {
        public static readonly string[] NoHelperModes = { "Hub", HouseModes.ShoppingList, HouseModes.RiddleRush };
        public static readonly string[] CrewModes = { HouseModes.MovingOut, HouseModes.MovingDay, HouseModes.Escape, HouseModes.BossFight };
        public static readonly string[] NoAnyoneModes = { HouseModes.Duos, HouseModes.MovingOut, HouseModes.MovingDay, HouseModes.Escape, HouseModes.BossFight };
        public static readonly string[] AlwaysLiveModes = { HouseModes.Tutorial };

        readonly Dictionary<string, HelperSpec> specs = new Dictionary<string, HelperSpec>(StringComparer.Ordinal);
        readonly List<HelperSpec> ordered = new List<HelperSpec>();

        public IReadOnlyList<HelperSpec> All => ordered;
        public int Count => ordered.Count;

        public static HelperBook FromJson(string json, string source = "items_house.json")
        {
            var book = new HelperBook();
            if (string.IsNullOrEmpty(json)) return book;
            var root = Json.Parse(json, source);
            if (!root.Has("items")) return book;
            foreach (var n in root["items"].Items)
                if (n.Has(HelperSpec.Key)) book.Add(HelperSpec.Read(n["id"].String(), n[HelperSpec.Key]));
            return book;
        }

        public void Add(HelperSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (specs.ContainsKey(spec.Id)) throw new ArgumentException($"Helper '{spec.Id}' is listed twice.");
            specs.Add(spec.Id, spec);
            ordered.Add(spec);
        }

        public bool Remove(string id)
        {
            if (id == null || !specs.TryGetValue(id, out var spec)) return false;
            specs.Remove(id);
            ordered.Remove(spec);
            return true;
        }

        public bool TryGet(string id, out HelperSpec spec) => specs.TryGetValue(id ?? "", out spec);

        public List<string> Validate(ItemCatalogue items, BagRowBook bags = null)
        {
            var problems = new List<string>();
            foreach (var spec in ordered)
            {
                if (!items.TryGet(spec.Id, out var item) || !item.Enabled) { problems.Add($"{spec.Id}: a helper block names no enabled recipe."); continue; }
                if (item.Family != HandlingFamily.Helper) problems.Add($"{spec.Id}: a helper block needs the Helper family, not {item.Family}.");
                problems.AddRange(HelperCaps.Check(spec));
            }
            var walls = new List<ItemDefinition>();
            foreach (var item in items.All)
            {
                if (!item.Enabled) continue;
                if (IsWall(item))
                {
                    walls.Add(item);
                    problems.AddRange(HelperCaps.CheckWall(item));
                }
                if (item.Family != HandlingFamily.Helper || (bags != null && bags.Holds(item) && !specs.ContainsKey(item.Id))) continue;
                string p = item.Id + ": ";
                if (!specs.ContainsKey(item.Id)) problems.Add(p + "is a Helper recipe without a helper block.");
                if (!item.Consumable) problems.Add(p + "a helper is used up when released, so it must be consumable.");
                if (item.Hands != 1) problems.Add(p + "a helper is held in one hand.");
                if (item.Melee != null || item.Thrown != null || item.Shield != null || item.Deploy != null || item.Use != null || item.Ranged != null)
                    problems.Add(p + "a helper acts only through its helper block.");
            }
            problems.AddRange(HelperCaps.Inversions(ordered));
            problems.AddRange(HelperCaps.WallInversions(walls));
            return problems;
        }

        public static bool IsWall(ItemDefinition item) =>
            item != null && item.Family == HandlingFamily.DeployCover && item.Deploy != null && item.Deploy.Effect == DeployEffect.Cover && item.Deploy.LifetimeSeconds > 0f;

        public static bool AllowedIn(string modeId, HelperSpec spec)
        {
            if (spec == null) return true;
            if (Contains(NoHelperModes, modeId)) return false;
            if (spec.Bites == HelperBite.Anyone && Contains(NoAnyoneModes, modeId)) return false;
            if (Contains(CrewModes, modeId)) return !spec.Hurts || spec.IsTrap;
            return true;
        }

        public static bool LiveNow(string modeId, bool roundPlaying) => roundPlaying || Contains(AlwaysLiveModes, modeId);

        public static string WallLine(ItemDefinition item) => IsWall(item) ? $"Blocks the way for {item.Deploy.LifetimeSeconds:0} s" : null;

        public string CardLine(ItemDefinition item)
        {
            if (item == null) return null;
            if (item.Family == HandlingFamily.Helper) return TryGet(item.Id, out var spec) ? spec.CardLine() : null;
            return WallLine(item);
        }

        static bool Contains(string[] list, string id) => id != null && Array.IndexOf(list, id) >= 0;
    }
}
