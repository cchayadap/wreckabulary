using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public sealed class LetterDropTuning
    {
        public const string Key = "drops";
        public const float DefaultShare = .6f;
        public const int DefaultFewest = 2;
        public static readonly IReadOnlyList<string> DefaultWholeIn = new[] { HouseModes.Tutorial };
        static readonly string[] Keys = { "note", "share", "fewest", "wholeIn" };
        const float ShareSlack = 1e-4f;

        public float Share { get; private set; } = DefaultShare;
        public int Fewest { get; private set; } = DefaultFewest;
        public IReadOnlyList<string> WholeIn { get; private set; } = DefaultWholeIn;

        public static LetterDropTuning Default => new LetterDropTuning();

        public static LetterDropTuning Of(float share, int fewest, params string[] wholeIn)
        {
            var tuning = new LetterDropTuning { Share = share, Fewest = fewest, WholeIn = wholeIn ?? Array.Empty<string>() };
            var problems = tuning.Validate();
            if (problems.Count > 0) throw new ArgumentException(string.Join(" ", problems));
            return tuning;
        }

        public static LetterDropTuning FromJson(JsonNode node)
        {
            foreach (string key in node.Keys)
                if (Array.IndexOf(Keys, key) < 0) throw node.Error($"'{key}' is not a drop setting; use share, fewest and wholeIn");
            var tuning = new LetterDropTuning
            {
                Share = node["share"].Float(DefaultShare),
                Fewest = node["fewest"].Int(DefaultFewest),
                WholeIn = node.Has("wholeIn") ? node["wholeIn"].Strings() : DefaultWholeIn,
            };
            var problems = tuning.Validate();
            if (problems.Count > 0) throw node.Error(string.Join(" ", problems));
            return tuning;
        }

        public List<string> Validate()
        {
            var problems = new List<string>();
            if (!(Share > 0f) || Share > 1f) problems.Add("drops: share must be above 0 and at most 1.");
            if (Fewest < 1) problems.Add("drops: fewest must be at least 1.");
            foreach (string mode in WholeIn)
                if (!HouseModes.Has(mode)) problems.Add($"drops: wholeIn names '{mode}', which is not a House mode.");
            if (!WholeIn.Contains(HouseModes.Tutorial)) problems.Add("drops: the Tutorial's scripted steps need every letter, so wholeIn must keep Tutorial.");
            return problems;
        }

        public int FewestFor(int length)
        {
            if (length <= Fewest) return Math.Max(0, length);
            int share = (int)Math.Ceiling(length * Share - ShareSlack);
            return Math.Min(length, Math.Max(Fewest, share));
        }

        public bool Partial(string mode) => HouseModes.Has(mode) && !WholeIn.Contains(mode);
    }

    public static class LetterDrops
    {
        public static int[] All(int length)
        {
            var all = new int[Math.Max(0, length)];
            for (int i = 0; i < all.Length; i++) all[i] = i;
            return all;
        }

        public static int[] Pick(string word, LetterDropTuning tuning, Random random, IReadOnlyDictionary<char, int> needed = null)
        {
            if (string.IsNullOrEmpty(word)) return Array.Empty<int>();
            tuning ??= LetterDropTuning.Default;
            if (random == null) throw new ArgumentNullException(nameof(random));
            int length = word.Length;
            int fewest = tuning.FewestFor(length);
            int count = fewest >= length ? length : random.Next(fewest, length + 1);
            var order = All(length);
            for (int i = length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            var taken = new bool[length];
            int chosen = 0;
            if (needed != null && needed.Count > 0)
            {
                var left = new Dictionary<char, int>();
                foreach (var pair in needed) if (pair.Value > 0) left[char.ToUpperInvariant(pair.Key)] = pair.Value;
                foreach (int i in order)
                {
                    char c = char.ToUpperInvariant(word[i]);
                    if (!left.TryGetValue(c, out int n) || n <= 0) continue;
                    left[c] = n - 1;
                    taken[i] = true;
                    chosen++;
                }
            }
            foreach (int i in order)
            {
                if (chosen >= count) break;
                if (taken[i]) continue;
                taken[i] = true;
                chosen++;
            }
            var picked = new int[chosen];
            for (int i = 0, k = 0; i < length; i++) if (taken[i]) picked[k++] = i;
            return picked;
        }

        public static string Letters(string word, IReadOnlyList<int> indices)
        {
            if (string.IsNullOrEmpty(word) || indices == null) return "";
            var chars = new char[indices.Count];
            for (int i = 0; i < indices.Count; i++) chars[i] = word[indices[i]];
            return new string(chars);
        }

        public static void Require(Dictionary<char, int> needed, string letters)
        {
            if (needed == null || string.IsNullOrEmpty(letters)) return;
            var own = new Dictionary<char, int>();
            foreach (char raw in letters)
            {
                char c = char.ToUpperInvariant(raw);
                own[c] = own.TryGetValue(c, out int n) ? n + 1 : 1;
            }
            foreach (var pair in own)
                needed[pair.Key] = Math.Max(pair.Value, needed.TryGetValue(pair.Key, out int n) ? n : 0);
        }

        public static void RequireFor(Dictionary<char, int> needed, IEnumerable<string> goals, IEnumerable<char> held)
        {
            if (needed == null) return;
            foreach (var pair in StillNeeded(goals, held))
                needed[pair.Key] = Math.Max(pair.Value, needed.TryGetValue(pair.Key, out int n) ? n : 0);
        }

        public static Dictionary<char, int> StillNeeded(IEnumerable<string> goals, IEnumerable<char> have)
        {
            var need = new Dictionary<char, int>();
            if (goals == null) return need;
            foreach (string goal in goals.Where(g => !string.IsNullOrEmpty(g)).Select(g => g.ToUpperInvariant()).Distinct())
                foreach (char c in goal) need[c] = need.TryGetValue(c, out int n) ? n + 1 : 1;
            if (have != null)
                foreach (char raw in have)
                {
                    char c = char.ToUpperInvariant(raw);
                    if (need.TryGetValue(c, out int n) && n > 0) need[c] = n - 1;
                }
            foreach (char c in need.Where(p => p.Value <= 0).Select(p => p.Key).ToList()) need.Remove(c);
            return need;
        }
    }
}
