using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Wreckabulary.Rules
{
    public readonly struct PaletteColour
    {
        public float R { get; }
        public float G { get; }
        public float B { get; }

        public PaletteColour(float r, float g, float b) { R = r; G = g; B = b; }

        public static PaletteColour FromHex(string hex)
        {
            string digits = hex != null && hex.StartsWith("#", StringComparison.Ordinal) ? hex.Substring(1) : hex;
            if (digits == null || digits.Length != 6 || !uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
                throw new FormatException($"'{hex}' is not a #RRGGBB colour.");
            return new PaletteColour((rgb >> 16 & 0xFF) / 255f, (rgb >> 8 & 0xFF) / 255f, (rgb & 0xFF) / 255f);
        }

        public static PaletteColour FromLinear(float r, float g, float b) => new PaletteColour(Encode(r), Encode(g), Encode(b));

        public float Value => Math.Max(R, Math.Max(G, B));

        public float Saturation
        {
            get
            {
                float max = Value, min = Math.Min(R, Math.Min(G, B));
                return max <= 0f ? 0f : (max - min) / max;
            }
        }

        public float Hue
        {
            get
            {
                float max = Value, min = Math.Min(R, Math.Min(G, B)), span = max - min;
                if (span <= 0f) return 0f;
                float hue = max == R ? (G - B) / span % 6f : max == G ? (B - R) / span + 2f : (R - G) / span + 4f;
                hue *= 60f;
                return hue < 0f ? hue + 360f : hue;
            }
        }

        public static float HueDistance(PaletteColour a, PaletteColour b)
        {
            float d = Math.Abs(a.Hue - b.Hue);
            return Math.Min(d, 360f - d);
        }

        public string Hex => "#" + Byte(R).ToString("X2", CultureInfo.InvariantCulture) + Byte(G).ToString("X2", CultureInfo.InvariantCulture) + Byte(B).ToString("X2", CultureInfo.InvariantCulture);

        static int Byte(float c) => (int)Math.Round(Math.Max(0f, Math.Min(1f, c)) * 255f);

        static float Encode(float c) => c <= 0.0031308f ? c * 12.92f : 1.055f * (float)Math.Pow(c, 1.0 / 2.4) - 0.055f;
    }

    public sealed class PaletteFamily
    {
        public string Id { get; }
        public PaletteColour Colour { get; }
        public float Roughness { get; }
        public string CandyFrom { get; }
        public string ArcadeFrom { get; }

        public PaletteFamily(string id, PaletteColour colour, float roughness, string candyFrom, string arcadeFrom)
        {
            Id = id; Colour = colour; Roughness = roughness; CandyFrom = candyFrom; ArcadeFrom = arcadeFrom;
        }
    }

    public sealed class PalettePart
    {
        public string Family { get; }
        public float Share { get; }
        public string Target { get; }
        public string Final => Target ?? Family;

        public PalettePart(string family, float share, string target) { Family = family; Share = share; Target = target; }
    }

    public sealed class ItemPalette
    {
        public const string FamilyPrefix = "toy_";
        public const string ResourcePath = "Art/item_palette";
        public const string SourceFile = "Assets/_Project/Resources/Art/item_palette.json";

        readonly Dictionary<string, PaletteFamily> families;
        readonly Dictionary<string, IReadOnlyList<PalettePart>> items;

        public IReadOnlyDictionary<string, PaletteFamily> Families => families;
        public IReadOnlyDictionary<string, IReadOnlyList<PalettePart>> Items => items;
        public IReadOnlyDictionary<string, string> NeutralByRole { get; }
        public IReadOnlyList<IReadOnlyList<string>> Similar { get; }

        ItemPalette(Dictionary<string, PaletteFamily> families, Dictionary<string, IReadOnlyList<PalettePart>> items,
            Dictionary<string, string> neutralByRole, List<IReadOnlyList<string>> similar)
        {
            this.families = families; this.items = items; NeutralByRole = neutralByRole; Similar = similar;
        }

        public static ItemPalette FromJson(string json, string source = "item_palette.json")
        {
            var root = Json.Parse(json, source);
            var families = new Dictionary<string, PaletteFamily>(StringComparer.Ordinal);
            foreach (string id in root["families"].Keys)
            {
                var node = root["families"][id];
                if (!id.StartsWith(FamilyPrefix, StringComparison.Ordinal)) throw node.Error($"item families start with {FamilyPrefix}");
                families.Add(id, new PaletteFamily(id, PaletteColour.FromHex(node["colour"].String()), node["roughness"].Float(),
                    node["candy"].String(), node["arcade"].String()));
            }
            var items = new Dictionary<string, IReadOnlyList<PalettePart>>(StringComparer.Ordinal);
            foreach (string id in root["items"].Keys)
            {
                var parts = new List<PalettePart>();
                foreach (string family in root["items"][id].Keys)
                {
                    var node = root["items"][id][family];
                    string target = node["to"].String(null);
                    parts.Add(new PalettePart(family, node["share"].Float(), target));
                }
                items.Add(id, parts);
            }
            var neutral = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string id in root["neutralByRole"].Keys) neutral.Add(id, root["neutralByRole"][id].String());
            var similar = root["similar"].Items.Select(group => (IReadOnlyList<string>)group.Strings()).ToList();
            return new ItemPalette(families, items, neutral, similar);
        }

        public bool TryMap(string item, string family, out string target)
        {
            target = null;
            if (item == null || family == null || !items.TryGetValue(item, out var parts)) return false;
            foreach (var part in parts)
                if (part.Target != null && string.Equals(part.Family, family, StringComparison.Ordinal))
                {
                    target = part.Target;
                    return true;
                }
            return false;
        }

        public string SourceOf(string item, string family)
        {
            if (item == null || family == null || !items.TryGetValue(item, out var parts)) return family;
            foreach (var part in parts)
                if (string.Equals(part.Target, family, StringComparison.Ordinal)) return part.Family;
            return family;
        }

        public IReadOnlyDictionary<string, float> FinalShares(string item)
        {
            var shares = new Dictionary<string, float>(StringComparer.Ordinal);
            if (!items.TryGetValue(item, out var parts)) return shares;
            foreach (var part in parts)
                shares[part.Final] = (shares.TryGetValue(part.Final, out float s) ? s : 0f) + part.Share;
            return shares;
        }

        public string MainFamily(string item)
        {
            var shares = FinalShares(item);
            return shares.Count == 0 ? null : shares.OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key, StringComparer.Ordinal).First().Key;
        }
    }
}
