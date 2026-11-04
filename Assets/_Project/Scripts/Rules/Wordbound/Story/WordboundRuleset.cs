using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public enum WordboundStoryId { Classic, DeadLetter, ForgettingHouse, Unspeller }
    public enum WordboundShift { Rush, Storm, LightsOut, Free }

    public sealed class WordboundRuleset
    {
        public const int Version = 1;
        public const string ClassicId = "Classic";
        public const string SteadyToken = "Steady";
        public const int MaximumGrudge = 3;

        static readonly string[] storyTokens = { "Classic", "DeadLetter", "House", "Unspeller" };
        static readonly string[] shiftTokens = { "Rush", "Storm", "LightsOut", "Free" };

        public static WordboundRuleset Classic { get; } =
            new WordboundRuleset(WordboundStoryId.Classic, WordboundShift.Rush, false, 0);
        public static IReadOnlyList<WordboundRuleset> Standard { get; } = Array.AsReadOnly(StandardRulesets().ToArray());

        public string Id { get; }
        public WordboundStoryId Story { get; }
        public WordboundShift Shift { get; }
        public bool Director { get; }
        public int Grudge { get; }
        public WordboundDeckProfile Deck { get; }
        public string Hash { get; }
        public bool IsClassic => Story == WordboundStoryId.Classic;
        public bool LocalOnly => Grudge > 0;

        WordboundRuleset(WordboundStoryId story, WordboundShift shift, bool steady, int grudge)
        {
            Story = story; Shift = shift; Grudge = grudge;
            Director = story != WordboundStoryId.Classic && shift != WordboundShift.Free && !steady;
            Deck = story == WordboundStoryId.Classic ? WordboundDeckProfile.None : WordboundDeckProfile.For(shift, Director);
            Id = story == WordboundStoryId.Classic ? ClassicId : string.Join(".", new[]
                {
                    storyTokens[(int)story], shiftTokens[(int)shift], steady ? SteadyToken : null,
                    grudge > 0 ? "G" + grudge.ToString(CultureInfo.InvariantCulture) : null
                }.Where(part => part != null));
            Hash = WordboundIdentity.Sha256(string.Join("|", new object[] { "wordbound.ruleset", Version, Id,
                storyTokens[(int)story], shiftTokens[(int)shift], Director, Grudge, Deck.Canonical,
                WordboundRaceDirector.Canonical }
                .Select(value => Convert.ToString(value, CultureInfo.InvariantCulture))));
        }

        public bool AppliesTo(string modeId)
        {
            if (IsClassic)
                return modeId == "TimeRun" || modeId == "TeamRace" || modeId == "BrawlRace" ||
                    modeId == "FreeForAll" || modeId == "Freestyle";
            if (Shift == WordboundShift.Free) return modeId == "Freestyle";
            return modeId == "TimeRun" || modeId == "TeamRace" || modeId == "BrawlRace";
        }

        public bool DirectorFor(string modeId) => Director && (modeId == "TeamRace" || modeId == "BrawlRace");

        public static WordboundRuleset Parse(string id) =>
            TryParse(id, out var ruleset) ? ruleset : throw new ArgumentException("Unknown Wordbound ruleset.", nameof(id));

        public static bool TryParse(string id, out WordboundRuleset ruleset)
        {
            ruleset = null;
            if (id == ClassicId) { ruleset = Classic; return true; }
            if (string.IsNullOrEmpty(id) || !WordboundIdentity.IsId(id)) return false;
            var parts = id.Split('.');
            if (parts.Length < 2 || parts.Length > 4) return false;
            int story = Array.IndexOf(storyTokens, parts[0]);
            int shift = Array.IndexOf(shiftTokens, parts[1]);
            if (story <= 0 || shift < 0) return false;
            int next = 2;
            bool steady = next < parts.Length && parts[next] == SteadyToken;
            if (steady) next++;
            int grudge = next < parts.Length ? GrudgeLevel(parts[next]) : 0;
            if (grudge > 0) next++;
            if (next != parts.Length) return false;
            if (steady && (WordboundShift)shift == WordboundShift.Free) return false;
            if (grudge > 0 && (WordboundStoryId)story != WordboundStoryId.Unspeller) return false;
            var parsed = new WordboundRuleset((WordboundStoryId)story, (WordboundShift)shift, steady, grudge);
            if (parsed.Id != id) return false;
            ruleset = parsed;
            return true;
        }

        static int GrudgeLevel(string token) =>
            token.Length == 2 && token[0] == 'G' && token[1] >= '1' && token[1] <= (char)('0' + MaximumGrudge) ? token[1] - '0' : 0;

        static IEnumerable<WordboundRuleset> StandardRulesets()
        {
            yield return Classic;
            foreach (var story in new[] { WordboundStoryId.DeadLetter, WordboundStoryId.ForgettingHouse, WordboundStoryId.Unspeller })
            {
                foreach (var shift in new[] { WordboundShift.Rush, WordboundShift.Storm, WordboundShift.LightsOut })
                {
                    yield return new WordboundRuleset(story, shift, false, 0);
                    yield return new WordboundRuleset(story, shift, true, 0);
                }
                yield return new WordboundRuleset(story, WordboundShift.Free, false, 0);
            }
        }
    }
}
