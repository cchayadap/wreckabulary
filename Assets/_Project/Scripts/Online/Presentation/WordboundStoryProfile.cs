using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    [Serializable]
    public sealed class WordboundStoryText
    {
        public string Key = "";
        [TextArea(1, 4)] public string Fallback = "";

        public string Resolve(Func<string, string> localize = null)
        {
            string translated = string.IsNullOrWhiteSpace(Key) ? null : localize?.Invoke(Key);
            return string.IsNullOrWhiteSpace(translated) || translated == Key ? Fallback : translated;
        }

        public void Validate(int maximum, string field)
        {
            if (Key == null || Key.Length > 128 || Key.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)))
                throw new ArgumentException(field + " needs a localization key without whitespace, or an empty key.");
            if (string.IsNullOrWhiteSpace(Fallback) || Fallback.Length > maximum ||
                Fallback.Any(c => char.IsControl(c) && c != '\n' && c != '\r' && c != '\t'))
                throw new ArgumentException(field + " needs fallback text of at most " + maximum + " characters.");
        }
    }

    [Serializable]
    public sealed class WordboundStoryChapter
    {
        public string Id = "";
        public WordboundStoryText Title = new WordboundStoryText();
    }

    [Serializable]
    public sealed class WordboundStoryCopy
    {
        [Tooltip("Existing lap:checkpoint identity. This cannot create or move a clue.")]
        public string Id = "";
        public WordboundStoryText Cue = new WordboundStoryText();
        public WordboundStoryText Clue = new WordboundStoryText();
    }

    [CreateAssetMenu(menuName = "Wreckabulary/Wordbound/Story profile")]
    public sealed class WordboundStoryProfile : ScriptableObject
    {
        public WordboundStoryText Title = new WordboundStoryText { Key = "wordbound.story.title", Fallback = "The Lost Address" };
        public List<WordboundStoryChapter> Chapters = new List<WordboundStoryChapter>();
        public List<WordboundStoryCopy> Notices = new List<WordboundStoryCopy>();

        public void Validate(WordboundFlagshipContent content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (Title == null) throw new ArgumentException("Assign a story title.");
            Title.Validate(96, "Story title");
            if (Chapters == null || Chapters.Count > 3 || Notices == null || Notices.Count > content.Story.Count)
                throw new ArgumentException("Story copy must refer only to the existing chapters and clues.");
            var chapterIds = new HashSet<string>(content.Story.Select(b => b.EventId), StringComparer.Ordinal);
            var noticeIds = new HashSet<string>(content.Story.Select(b => b.Lap + ":" + b.CheckpointId), StringComparer.Ordinal);
            var distinct = new HashSet<string>(StringComparer.Ordinal);
            foreach (var chapter in Chapters)
            {
                if (chapter == null || !chapterIds.Contains(chapter.Id) || !distinct.Add(chapter.Id) || chapter.Title == null)
                    throw new ArgumentException("Chapter copy needs a distinct existing chapter ID and title.");
                chapter.Title.Validate(96, chapter.Id);
            }
            distinct.Clear();
            foreach (var notice in Notices)
            {
                if (notice == null || !noticeIds.Contains(notice.Id) || !distinct.Add(notice.Id) ||
                    notice.Cue == null || notice.Clue == null)
                    throw new ArgumentException("Notice copy needs a distinct existing clue ID, cue and journal text.");
                notice.Cue.Validate(96, notice.Id + " cue");
                notice.Clue.Validate(1024, notice.Id + " journal");
            }
        }

        public void PopulateDefaults(WordboundFlagshipContent content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (Chapters == null || Notices == null || Chapters.Count != 0 || Notices.Count != 0)
                throw new InvalidOperationException("Default population requires an empty profile; authored copy is preserved.");
            Chapters = content.Story.Select(b => b.EventId).Distinct().Select(id => new WordboundStoryChapter {
                Id = id, Title = Text("chapter." + id, DefaultChapter(id))
            }).ToList();
            Notices = content.Story.Select(b => {
                string id = b.Lap + ":" + b.CheckpointId;
                return new WordboundStoryCopy { Id = id, Cue = Text("notice." + id + ".cue", DefaultCue(id)),
                    Clue = Text("notice." + id + ".clue", b.Clue) };
            }).ToList();
            Validate(content);
        }

        public string Chapter(string id, Func<string, string> localize = null)
            => Chapters.FirstOrDefault(c => c.Id == id)?.Title.Resolve(localize) ?? DefaultChapter(id);

        public string Cue(string id, Func<string, string> localize = null)
            => Notices.FirstOrDefault(n => n.Id == id)?.Cue.Resolve(localize) ?? DefaultCue(id);

        public string Clue(string id, string fallback, Func<string, string> localize = null)
            => Notices.FirstOrDefault(n => n.Id == id)?.Clue.Resolve(localize) ?? fallback;

        static WordboundStoryText Text(string key, string fallback)
            => new WordboundStoryText { Key = "wordbound.story." + key, Fallback = fallback };

        public static string DefaultChapter(string id) => id switch
        {
            "FindAddress" => "Find the address",
            "WrongDelivery" => "Follow the wrong delivery",
            "RestoreHome" => "Put home back together",
            _ => "The lost address"
        };

        public static string DefaultCue(string id) => id switch
        {
            "1:L1_Packing_ParcelGate" => "An address nobody wrote.",
            "1:L1_Kitchen_ServiceLanding" => "These labels belong upstairs.",
            "1:L1_Attic_TrunkLanding" => "These rooms once fit together.",
            "1:L1_Rooftop_GardenGate" => "One address is missing.",
            "2:L2_Rooftop_RooftopSorter" => "Wrong instructions lead downstairs.",
            "2:L2_Attic_ChimneyLanding" => "The sorting pointer is broken.",
            "2:L2_Kitchen_Dumbwaiter" => "Kitchen parcels. Attic labels.",
            "2:L2_Packing_LiftLanding" => "The Movers obey the labels.",
            "2:L2_Packing_RepairBay" => "The mistake began at the sorter.",
            "3:L3_Packing_ParcelGate" => "Send the right labels home.",
            "3:L3_Packing_RepairBay" => "The pointer is repaired.",
            "3:L3_Kitchen_ServiceLanding" => "Return labels to their drawers.",
            "3:L3_Attic_TrunkLanding" => "Put the diagram in order.",
            "3:L3_Rooftop_GardenGate" => "Deliver the keepsake home.",
            _ => "A clue for the journal."
        };
    }
}
