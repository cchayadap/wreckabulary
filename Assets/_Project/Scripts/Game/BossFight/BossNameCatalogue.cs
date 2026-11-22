using System;
using System.Collections.Generic;
using Wreckabulary.Rules;

namespace Wreckabulary.BossFight
{
    /// <summary>
    /// Boss identity comes from rules.json (bossName / bossWords), so no new ScriptableObject
    /// or CSV has to be registered with the data setup window.
    /// </summary>
    // @trace MECH-002
    public sealed class BossNameCatalogue
    {
        readonly GameRules rules;

        public BossNameCatalogue(GameRules rules)
        {
            this.rules = rules ?? Match.Rules;
        }

        public string Name => string.IsNullOrEmpty(rules.BossName) ? "CALCULATOR" : rules.BossName;

        public int LayerCount => rules.BossWords != null && rules.BossWords.Length > 0
            ? rules.BossWords.Length : 3;

        public string WordFor(int layer)
        {
            if (rules.BossWords != null && layer >= 0 && layer < rules.BossWords.Length)
                return rules.BossWords[layer].ToUpperInvariant();
            string[] fallback = { "CALC", "ULA", "TOR" };
            return layer >= 0 && layer < fallback.Length ? fallback[layer] : fallback[0];
        }

        /// <summary>Words that crack shields: craftable without an items.json recipe.</summary>
        public HashSet<string> SignalWords()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < LayerCount; i++) set.Add(WordFor(i));
            return set;
        }

        /// <summary>
        /// Everything the player can spell in this mode: the full normal recipe list plus the
        /// shield words. Never replace the catalogue here — doing so removes BAT, weapons and
        /// every other recipe from both the word wheel and typed spelling.
        /// </summary>
        // @trace MECH-002, MECH-003
        public List<WordEntry> SpellWords()
        {
            var list = new List<WordEntry>();
            var db = GameAssets.I ? GameAssets.I.words : null;
            if (db)
                foreach (var entry in db.Words)
                    if (entry != null && !string.IsNullOrEmpty(entry.word)) list.Add(entry);

            for (int i = 0; i < LayerCount; i++)
            {
                string word = WordFor(i);
                var existing = list.Find(w => w.word == word);
                if (existing != null) existing.hidden = false;
                else list.Add(new WordEntry
                {
                    word = word, category = WordCategory.Chaos, hidden = false,
                    notes = "Boss shield " + (i + 1),
                });
            }
            return list;
        }
    }
}
