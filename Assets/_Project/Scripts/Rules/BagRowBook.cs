using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules
{
    public sealed class BagRowSpec
    {
        public string Id = "";
        public int Rows = 1;

        public static BagRowSpec Read(string id, JsonNode n)
        {
            if (!n.IsObject) throw n.Error("a bag block is an object");
            foreach (string key in n.Keys)
                if (key != BagRowBook.RowsKey) throw n.Error($"'{key}' is not a bag field");
            if (!n.Has(BagRowBook.RowsKey)) throw n.Error($"a bag block needs '{BagRowBook.RowsKey}'");
            return new BagRowSpec { Id = id ?? "", Rows = n[BagRowBook.RowsKey].Int() };
        }
    }

    public sealed class BagRowBook
    {
        public const string Key = "bag", RowsKey = "rows";

        readonly Dictionary<string, BagRowSpec> specs = new Dictionary<string, BagRowSpec>(StringComparer.Ordinal);
        readonly List<BagRowSpec> ordered = new List<BagRowSpec>();

        public IReadOnlyList<BagRowSpec> All => ordered;
        public int Count => ordered.Count;

        public static BagRowBook FromJson(string json, string source = "items_house.json")
        {
            var book = new BagRowBook();
            if (string.IsNullOrEmpty(json)) return book;
            var root = Json.Parse(json, source);
            if (!root.Has("items")) return book;
            foreach (var n in root["items"].Items)
                if (n.Has(Key)) book.Add(BagRowSpec.Read(n["id"].String(), n[Key]));
            return book;
        }

        public void Add(BagRowSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (specs.ContainsKey(spec.Id)) throw new ArgumentException($"Bag item '{spec.Id}' is listed twice.");
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

        public bool TryGet(string id, out BagRowSpec spec) => specs.TryGetValue(id ?? "", out spec);

        public bool Holds(string id) => id != null && specs.ContainsKey(id);

        public bool Holds(ItemDefinition item) => item != null && Holds(item.Id);

        public static bool Offered(string modeId) => HouseModes.Has(modeId);

        public string CardLine(ItemDefinition item) => Holds(item) ? BagRows.CardLine : null;

        public List<string> Validate(ItemCatalogue items, HelperBook helpers = null)
        {
            var problems = new List<string>();
            foreach (var spec in ordered)
            {
                string p = spec.Id + ": ";
                if (spec.Rows < 1 || spec.Rows > BagRows.MostBonusRows) problems.Add(p + $"a bag item adds 1 to {BagRows.MostBonusRows} rows, not {spec.Rows}.");
                if (helpers != null && helpers.TryGet(spec.Id, out _)) problems.Add(p + "a bag item can't also be a helper.");
                if (items == null || !items.TryGet(spec.Id, out var item) || !item.Enabled) { problems.Add(p + "a bag block names no enabled recipe."); continue; }
                if (item.Family != HandlingFamily.Helper) problems.Add(p + $"a bag item uses the stat-less Helper family, not {item.Family}.");
                if (!item.Consumable) problems.Add(p + "a bag item is used up when spelled, so it must be consumable.");
                if (item.Hands != 1) problems.Add(p + "a bag item is listed as one hand.");
                if (item.Melee != null || item.Thrown != null || item.Shield != null || item.Deploy != null || item.Use != null || item.Ranged != null)
                    problems.Add(p + "a bag item acts only through its bag block.");
                if (item.Letters.Count > BagRows.BaseCells) problems.Add(p + $"needs {item.Letters.Count} letters but must fit a {BagRows.BaseCells}-slot bag.");
            }
            return problems;
        }
    }
}
