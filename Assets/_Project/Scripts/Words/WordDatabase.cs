using System;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>Furniture words are Moving Day checklist items: spelling one builds the piece of furniture.</summary>
    public enum WordCategory { Weapon, Defence, Movement, Chaos, Furniture }

    [Serializable]
    public class WordEntry
    {
        public string word;
        public WordCategory category;
        [Tooltip("Hidden words are not hinted in the word wheel until you can spell them.")]
        public bool hidden;
        public string notes;
        public int Score => LetterScores.ScoreOf(word);
    }

    [CreateAssetMenu(menuName = "Wreckabulary/Word Database")]
    public class WordDatabase : ScriptableObject
    {
        [SerializeField] TextAsset csv;

        [NonSerialized] List<WordEntry> words;
        [NonSerialized] ItemCatalogue catalogue;
        [NonSerialized] bool explicitFixture;

        public IReadOnlyList<WordEntry> Words
        {
            get
            {
                if (explicitFixture) return words;
                var current = GameConfig.Current.Items;
                if (words == null || catalogue != current)
                {
                    catalogue = current;
                    words = CatalogueWords(current);
                }
                return words;
            }
        }

        static List<WordEntry> CatalogueWords(ItemCatalogue catalogue)
        {
            var result = new List<WordEntry>();
            foreach (var item in catalogue.Enabled)
            {
                var category = item.Category == ItemCategory.Furniture ? WordCategory.Furniture
                    : item.Shield != null || item.Use != null ? WordCategory.Defence
                    : item.Deploy != null && (item.Deploy.Effect == DeployEffect.JumpPad || item.Deploy.Effect == DeployEffect.SpeedStrip) ? WordCategory.Movement
                    : item.Consumable ? WordCategory.Chaos : WordCategory.Weapon;
                result.Add(new WordEntry { word = item.Id, category = category, notes = item.Notes });
            }
            return result;
        }

        public WordEntry Find(string word)
        {
            word = word.ToUpperInvariant();
            foreach (var w in Words)
                if (w.word == word) return w;
            return null;
        }

        public static WordDatabase FromCsv(string text)
        {
            var db = CreateInstance<WordDatabase>();
            db.words = Parse(text);
            db.explicitFixture = true;
            return db;
        }

        /// <summary>Parses "word,category,hidden,notes" lines. The first line is a header.</summary>
        public static List<WordEntry> Parse(string text)
        {
            var list = new List<WordEntry>();
            var lines = text.Split('\n');
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0) continue;
                var parts = line.Split(new[] { ',' }, 4);
                if (parts.Length < 2) continue;
                if (!Enum.TryParse(parts[1].Trim(), true, out WordCategory category))
                {
                    Debug.LogWarning($"word_list.csv line {i + 1}: unknown category '{parts[1]}'");
                    continue;
                }
                list.Add(new WordEntry
                {
                    word = parts[0].Trim().ToUpperInvariant(),
                    category = category,
                    hidden = parts.Length > 2 && parts[2].Trim().Equals("true", StringComparison.OrdinalIgnoreCase),
                    notes = parts.Length > 3 ? parts[3].Trim() : ""
                });
            }
            return list;
        }
    }
}
