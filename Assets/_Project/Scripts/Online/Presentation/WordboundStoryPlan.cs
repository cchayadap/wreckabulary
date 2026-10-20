using System;
using System.Collections.Generic;
using System.Linq;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed class WordboundStoryNotice
    {
        public string Id { get; }
        public int Lap { get; }
        public int Checkpoint { get; }
        public string Chapter { get; }
        public string Place { get; }
        public string Clue { get; }
        public CoursePoint Position { get; }
        internal WordboundStoryNotice(string id, int lap, int checkpoint, string chapter,
            string place, string clue, CoursePoint position)
        { Id = id; Lap = lap; Checkpoint = checkpoint; Chapter = chapter; Place = place; Clue = clue; Position = position; }
    }

    public sealed class WordboundStoryPlan
    {
        readonly ResolvedCourse course;
        readonly IReadOnlyList<WordboundStoryNotice> notices;
        public IReadOnlyList<WordboundStoryNotice> Notices => notices;

        public WordboundStoryPlan(ResolvedCourse resolved, ItemCatalogue items)
        {
            course = resolved ?? throw new ArgumentNullException(nameof(resolved));
            if (items == null) throw new ArgumentNullException(nameof(items));
            var document = course.CopyDocument();
            var selected = new List<WordboundStoryNotice>();
            if (document.Id == WordboundFlagshipContent.CourseId && course.Laps.Count > 0)
            {
                var story = WordboundFlagshipContent.Create(items);
                foreach (var lap in course.Laps)
                    for (int index = 0; index < lap.Checkpoints.Count; index++)
                    {
                        var checkpoint = lap.Checkpoints[index];
                        var beat = story.Story.SingleOrDefault(b => b.Lap == lap.Number &&
                            b.CheckpointId == checkpoint.Id && b.EventId == lap.NarrativeId);
                        if (beat == null) continue;
                        var anchor = course.RecoveryAnchors.Single(a => a.Id == checkpoint.RecoveryAnchorId);
                        var room = story.Rooms.SingleOrDefault(r => r.Id == checkpoint.ModuleId);
                        var destination = story.Destinations.SingleOrDefault(d => d.ModuleIds.Contains(checkpoint.ModuleId));
                        selected.Add(new WordboundStoryNotice(lap.Number + ":" + checkpoint.Id, lap.Number,
                            index, Chapter(lap.NarrativeId), destination?.Name ?? room?.Name ?? "Parcel notice",
                            beat.Clue, anchor.Position));
                    }
            }
            notices = selected.AsReadOnly();
        }

        public IReadOnlyList<WordboundStoryNotice> Journal(MatchSnapshot frame, string actor)
        {
            if (frame == null) return Array.Empty<WordboundStoryNotice>();
            if (frame.Binding.ManifestHash != course.Hash || frame.Binding.CourseHash != course.SourceHash)
                throw new ArgumentException("Story progress must belong to the exact course.");
            return Journal(frame.Progress, frame.NarrativeFlags, actor);
        }

        public IReadOnlyList<WordboundStoryNotice> Journal(ProgressSnapshot progress,
            IEnumerable<string> narrativeFlags, string actor)
        {
            if (progress == null) return Array.Empty<WordboundStoryNotice>();
            var player = progress.Players.SingleOrDefault(p => p.Id == actor)
                ?? throw new ArgumentException("Story reader is not in the roster.");
            var team = progress.Teams.Single(t => t.Team == player.Team);
            var flags = new HashSet<string>(narrativeFlags ?? Array.Empty<string>(), StringComparer.Ordinal);
            return Array.AsReadOnly(notices.Where(n => flags.Contains(MatchStateValidation.NarrativeFlag(team.Team, n.Lap - 1)) &&
                (n.Lap <= team.Lap || n.Lap == team.Lap + 1 && n.Checkpoint < team.NextCheckpoint)).ToArray());
        }

        static string Chapter(string id) => id switch
        {
            "FindAddress" => "Find the address",
            "WrongDelivery" => "Follow the wrong delivery",
            "RestoreHome" => "Put home back together",
            _ => "The lost address"
        };
    }
}
