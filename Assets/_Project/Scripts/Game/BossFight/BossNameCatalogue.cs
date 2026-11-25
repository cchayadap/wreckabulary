using System;
using System.Collections.Generic;
using Wreckabulary.Rules;

namespace Wreckabulary.BossFight
{
    public sealed class BossNameCatalogue
    {
        HashSet<string> signals;

        public string Name => BossFightTuning.Name;

        public int LayerCount => BossFightTuning.Words.Count;

        public string WordFor(int layer) => BossFightTuning.WordFor(layer);

        public IReadOnlyCollection<string> SignalWords()
        {
            if (signals != null) return signals;
            signals = new HashSet<string>(StringComparer.Ordinal);
            foreach (string word in BossFightTuning.Words) signals.Add(word);
            return signals;
        }

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
                if (list.Exists(w => w.word == word)) continue;
                list.Add(new WordEntry { word = word, category = WordCategory.Chaos, notes = "Boss shield " + (i + 1) });
            }
            return list;
        }
    }
}
