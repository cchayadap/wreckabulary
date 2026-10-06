using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public sealed class WardrobePiece
    {
        public string Id;
        public string Slot;
        public string Mesh;
        public string TintMaterial;
        public string ColourFrom;
        public List<string> Requires = new List<string>();
        public List<string> Excludes = new List<string>();
    }

    public sealed class Colourway
    {
        public string Id;
        public string Name;
        public float R, G, B;
    }

    public sealed class Outfit
    {
        public readonly Dictionary<string, string> Pieces = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly Dictionary<string, string> Colours = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly Dictionary<string, string> ItemSkins = new Dictionary<string, string>(StringComparer.Ordinal);

        public string PieceIn(string slot) => Pieces.TryGetValue(slot, out string p) ? p : null;
        public string ColourOf(string slot) => Colours.TryGetValue(slot, out string c) ? c : null;
        public string SkinFor(string itemId) => ItemSkins.TryGetValue(itemId, out string s) ? s : null;

        public Outfit Clone()
        {
            var o = new Outfit();
            foreach (var kv in Pieces) o.Pieces[kv.Key] = kv.Value;
            foreach (var kv in Colours) o.Colours[kv.Key] = kv.Value;
            foreach (var kv in ItemSkins) o.ItemSkins[kv.Key] = kv.Value;
            return o;
        }

        public string Serialize()
        {
            var slots = Pieces.Keys.Union(Colours.Keys).OrderBy(k => k, StringComparer.Ordinal)
                .Select(s => $"{s}={PieceIn(s) ?? ""}{(ColourOf(s) != null ? ":" + ColourOf(s) : "")}");
            var skins = ItemSkins.OrderBy(k => k.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value}");
            return string.Join(";", slots) + "|" + string.Join(";", skins);
        }

        public static Outfit Deserialize(string text)
        {
            var o = new Outfit();
            if (string.IsNullOrEmpty(text)) return o;
            var halves = text.Split('|');
            foreach (string part in halves[0].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string slot = part.Substring(0, eq);
                string[] pc = part.Substring(eq + 1).Split(':');
                if (pc[0].Length > 0) o.Pieces[slot] = pc[0];
                if (pc.Length > 1 && pc[1].Length > 0) o.Colours[slot] = pc[1];
            }
            if (halves.Length > 1)
                foreach (string part in halves[1].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int eq = part.IndexOf('=');
                    if (eq > 0) o.ItemSkins[part.Substring(0, eq)] = part.Substring(eq + 1);
                }
            return o;
        }
    }

    public sealed class WardrobeCatalogue
    {
        public readonly List<string> Slots = new List<string>();
        public readonly HashSet<string> RequiredSlots = new HashSet<string>(StringComparer.Ordinal);
        public readonly List<WardrobePiece> Pieces = new List<WardrobePiece>();
        public readonly Dictionary<string, List<Colourway>> Palettes = new Dictionary<string, List<Colourway>>(StringComparer.Ordinal);
        public Outfit Default = new Outfit();

        public WardrobePiece Piece(string id) => Pieces.FirstOrDefault(p => p.Id == id);

        public IEnumerable<WardrobePiece> PiecesFor(string slot) => Pieces.Where(p => p.Slot == slot);

        public Colourway Colour(string slot, string id) =>
            Palettes.TryGetValue(slot, out var list) ? list.FirstOrDefault(c => c.Id == id) : null;

        public List<string> Problems(Outfit outfit)
        {
            var problems = new List<string>();
            foreach (string slot in RequiredSlots)
                if (outfit.PieceIn(slot) == null) problems.Add($"{slot} can't be empty");
            var worn = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in outfit.Pieces)
            {
                var piece = Piece(kv.Value);
                if (!Slots.Contains(kv.Key)) problems.Add($"there is no {kv.Key} slot");
                else if (piece == null) problems.Add($"there is no piece called {kv.Value}");
                else if (piece.Slot != kv.Key) problems.Add($"{piece.Id} goes in {piece.Slot}, not {kv.Key}");
                else worn.Add(piece.Id);
            }
            foreach (string id in worn)
            {
                var piece = Piece(id);
                foreach (string need in piece.Requires)
                    if (!worn.Contains(need)) problems.Add($"{id} needs {need}");
                foreach (string clash in piece.Excludes)
                    if (worn.Contains(clash)) problems.Add($"{id} can't be worn with {clash}");
            }
            foreach (var kv in outfit.Colours)
                if (Colour(kv.Key, kv.Value) == null) problems.Add($"{kv.Value} is not a {kv.Key} colour");
            return problems;
        }

        public bool Fits(Outfit outfit) => Problems(outfit).Count == 0;

        public Outfit Wear(Outfit current, string slot, string pieceId)
        {
            var next = current.Clone();
            if (pieceId == null) next.Pieces.Remove(slot);
            else next.Pieces[slot] = pieceId;
            bool removed = true;
            while (removed)
            {
                removed = false;
                foreach (var kv in next.Pieces.ToList())
                {
                    if (kv.Key == slot || RequiredSlots.Contains(kv.Key)) continue;
                    var piece = Piece(kv.Value);
                    if (piece == null) continue;
                    bool unmet = piece.Requires.Any(r => !next.Pieces.ContainsValue(r));
                    bool clash = piece.Excludes.Any(next.Pieces.ContainsValue);
                    if (unmet || clash)
                    {
                        next.Pieces.Remove(kv.Key);
                        removed = true;
                    }
                }
            }
            return Fits(next) ? next : null;
        }

        public List<string> Choices(Outfit current, string slot)
        {
            var choices = new List<string>();
            if (!RequiredSlots.Contains(slot) && Wear(current, slot, null) != null) choices.Add(null);
            foreach (var p in PiecesFor(slot))
                if (Wear(current, slot, p.Id) != null) choices.Add(p.Id);
            return choices;
        }

        public Colourway ColourFor(Outfit outfit, string slot)
        {
            var piece = Piece(outfit.PieceIn(slot) ?? "");
            string from = piece?.ColourFrom ?? slot;
            string id = outfit.ColourOf(from) ?? Default.ColourOf(from);
            return id == null ? null : Colour(from, id);
        }

        public Outfit Sanitize(Outfit outfit)
        {
            if (outfit != null && Fits(outfit)) return outfit;
            var fixedUp = Default.Clone();
            if (outfit == null) return fixedUp;
            foreach (var kv in outfit.ItemSkins) fixedUp.ItemSkins[kv.Key] = kv.Value;
            foreach (var kv in outfit.Colours)
                if (Colour(kv.Key, kv.Value) != null) fixedUp.Colours[kv.Key] = kv.Value;
            return fixedUp;
        }

        public List<string> Validate()
        {
            var problems = new List<string>();
            if (Pieces.Select(p => p.Id).Distinct().Count() != Pieces.Count) problems.Add("a piece id is used twice");
            foreach (var p in Pieces)
            {
                if (!Slots.Contains(p.Slot)) problems.Add($"{p.Id} is in unknown slot {p.Slot}");
                if (string.IsNullOrEmpty(p.Mesh)) problems.Add($"{p.Id} has no mesh");
                foreach (string r in p.Requires.Concat(p.Excludes))
                    if (Piece(r) == null) problems.Add($"{p.Id} refers to unknown piece {r}");
                if (p.ColourFrom != null && !Slots.Contains(p.ColourFrom)) problems.Add($"{p.Id} takes its colour from unknown slot {p.ColourFrom}");
                string tintSlot = p.ColourFrom ?? p.Slot;
                if (p.TintMaterial != null && !Palettes.ContainsKey(tintSlot)) problems.Add($"{p.Id} is tinted but {tintSlot} has no palette");
            }
            foreach (string s in RequiredSlots)
                if (!PiecesFor(s).Any()) problems.Add($"required slot {s} has no pieces");
            foreach (var kv in Palettes)
            {
                if (kv.Value.Select(c => c.Id).Distinct().Count() != kv.Value.Count) problems.Add($"{kv.Key} lists a colour twice");
                foreach (var c in kv.Value)
                {
                    if (string.IsNullOrEmpty(c.Name)) problems.Add($"{kv.Key} colour {c.Id} has no name to show");
                    if (c.R < 0 || c.R > 1 || c.G < 0 || c.G > 1 || c.B < 0 || c.B > 1) problems.Add($"{kv.Key} colour {c.Id} is outside 0-1");
                }
            }
            foreach (string p in Problems(Default)) problems.Add("default outfit: " + p);
            return problems;
        }

        public static WardrobeCatalogue FromJson(string json, string source = "wardrobe.json")
        {
            var root = Json.Parse(json, source);
            var w = new WardrobeCatalogue();
            w.Slots.AddRange(root["slots"].Strings());
            foreach (string s in root["requiredSlots"].Strings()) w.RequiredSlots.Add(s);
            foreach (var n in root["pieces"].Items)
            {
                w.Pieces.Add(new WardrobePiece
                {
                    Id = n["id"].String(),
                    Slot = n["slot"].String(),
                    Mesh = n["mesh"].String(),
                    TintMaterial = n["tint"].String(null),
                    ColourFrom = n["colourFrom"].String(null),
                    Requires = n["requires"].Strings(),
                    Excludes = n["excludes"].Strings(),
                });
            }
            foreach (string slot in root["palettes"].Keys)
            {
                var list = new List<Colourway>();
                foreach (var c in root["palettes"][slot].Items)
                {
                    var rgb = c["rgb"].Floats(3);
                    list.Add(new Colourway { Id = c["id"].String(), Name = c["name"].String(), R = rgb[0], G = rgb[1], B = rgb[2] });
                }
                w.Palettes[slot] = list;
            }
            var d = root["default"];
            foreach (string slot in d["pieces"].Keys) w.Default.Pieces[slot] = d["pieces"][slot].String();
            foreach (string slot in d["colours"].Keys) w.Default.Colours[slot] = d["colours"][slot].String();
            return w;
        }
    }
}
