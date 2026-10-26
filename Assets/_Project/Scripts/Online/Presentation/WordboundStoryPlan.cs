using System;
using System.Collections.Generic;
using System.Globalization;
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
        public string Cue { get; }
        public CoursePoint Position { get; }
        internal WordboundStoryNotice(string id, int lap, int checkpoint, string chapter,
            string place, string clue, CoursePoint position)
            : this(id, lap, checkpoint, chapter, place, clue, position, clue) { }
        internal WordboundStoryNotice(string id, int lap, int checkpoint, string chapter,
            string place, string clue, CoursePoint position, string cue)
        { Id = id; Lap = lap; Checkpoint = checkpoint; Chapter = chapter; Place = place; Clue = clue; Position = position; Cue = cue; }
    }

    public sealed class WordboundStoryEpilogue
    {
        public string Id { get; }
        public string Title { get; }
        public string Cue { get; }
        public string ResultKey { get; }
        internal WordboundStoryEpilogue(string id, string title, string cue, string resultKey)
        { Id = id; Title = title; Cue = cue; ResultKey = resultKey; }
    }

    public sealed class WordboundStoryPlan
    {
        readonly ResolvedCourse course;
        readonly IReadOnlyList<WordboundStoryNotice> notices;
        readonly bool supportsEpilogues;
        readonly Dictionary<string, (string Title, string Cue)> epilogues = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        public IReadOnlyList<WordboundStoryNotice> Notices => notices;

        public WordboundStoryPlan(ResolvedCourse resolved, ItemCatalogue items)
            : this(resolved, items, null) { }

        public WordboundStoryPlan(ResolvedCourse resolved, ItemCatalogue items, WordboundStoryProfile profile,
            Func<string, string> localize = null)
        {
            course = resolved ?? throw new ArgumentNullException(nameof(resolved));
            if (items == null) throw new ArgumentNullException(nameof(items));
            var document = course.CopyDocument();
            var selected = new List<WordboundStoryNotice>();
            if (document.Id == WordboundFlagshipContent.CourseId && course.Laps.Count > 0)
            {
                var story = WordboundFlagshipContent.Create(items);
                if (profile) profile.Validate(story);
                supportsEpilogues = course.CatalogueHash == story.Geometry.Hash &&
                    course.SourceHash == CourseJson.Sha256(CourseJson.ExportDocument(story.CopyDocument()));
                foreach (string id in WordboundStoryProfile.EpilogueIds)
                {
                    var fallback = WordboundStoryProfile.DefaultEpilogue(id);
                    epilogues.Add(id, (profile ? profile.EpilogueTitle(id, localize) : fallback.Title.Resolve(localize),
                        profile ? profile.EpilogueCue(id, localize) : fallback.Cue.Resolve(localize)));
                }
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
                        string id = lap.Number + ":" + checkpoint.Id;
                        selected.Add(new WordboundStoryNotice(id, lap.Number, index,
                            profile ? profile.Chapter(lap.NarrativeId, localize) : WordboundStoryProfile.DefaultChapter(lap.NarrativeId),
                            destination?.Name ?? room?.Name ?? "Parcel notice",
                            profile ? profile.Clue(id, beat.Clue, localize) : beat.Clue, anchor.Position,
                            profile ? profile.Cue(id, localize) : WordboundStoryProfile.DefaultCue(id)));
                    }
            }
            notices = selected.AsReadOnly();
        }

        public WordboundStoryEpilogue Epilogue(MatchSnapshot frame, string actor, ModeDefinition mode)
        {
            if (frame == null || mode == null || !supportsEpilogues || !mode.IsRace ||
                frame.Binding.ModeHash != mode.Hash || frame.Binding.CourseHash != course.SourceHash ||
                frame.Binding.ManifestHash != course.Hash || course.Play.ModeId != mode.Id ||
                course.Play.TeamCount != mode.TeamCount || course.Play.TeamCapacity != mode.TeamCapacity) return null;
            var progress = frame.Progress;
            var result = progress.Result;
            if (progress.Migrating || result == null || !result.Confirmed || result.Tick != progress.Tick) return null;
            var player = progress.Players.SingleOrDefault(p => p.Id == actor);
            var seat = frame.Binding.Seats.SingleOrDefault(s => s.PlayerId == actor);
            if (player == null || seat == null || player.Team != seat.Team || player.Slot != seat.Slot ||
                player.Returned || !player.Connected) return null;
            var team = progress.Teams.SingleOrDefault(t => t.Team == player.Team);
            if (team == null || progress.Teams.Any(t => t.Finished != (t.Lap == mode.Laps) || t.Finished && t.NextCheckpoint != 0)) return null;
            var finished = progress.Teams.Where(t => t.Finished).Select(t => t.Team).OrderBy(t => t).ToArray();
            bool expired = progress.ElapsedTicks >= (long)mode.TimeLimitSeconds * ModeDefinition.TickRate;
            int[] expectedWinners;
            if (finished.Length != 0) expectedWinners = finished;
            else if (!expired) return null;
            else if (mode.Id == "TimeRun") expectedWinners = Array.Empty<int>();
            else
            {
                int best = progress.Teams.Max(t => t.OrderedObjectives);
                long earliest = progress.Teams.Where(t => t.OrderedObjectives == best).Min(t => t.ProgressTick);
                expectedWinners = progress.Teams.Where(t => t.OrderedObjectives == best && t.ProgressTick == earliest)
                    .Select(t => t.Team).OrderBy(t => t).ToArray();
            }
            string outcome = expectedWinners.Length == 0 ? "TimeExpired" : expectedWinners.Length == 1 ? "Win" : "Draw";
            if (result.Outcome != outcome || !result.WinningTeams.SequenceEqual(expectedWinners)) return null;
            bool ownWinner = result.WinningTeams.Contains(team.Team);
            string id;
            if (team.Finished && ownWinner)
            {
                if (!Journal(frame, actor).Any(n => n.Id == "3:L3_Rooftop_GardenGate")) return null;
                id = mode.Id == "TimeRun" ? "TimeRunSuccess" : "RaceDelivery";
            }
            else if (mode.Id == "TimeRun") id = "TimeRunExpired";
            else if (!ownWinner) id = "RaceDefeat";
            else id = result.Outcome == "Draw" ? "RaceProgressDraw" : "RaceProgressWin";
            var copy = epilogues[id];
            string key = string.Join("|", frame.Binding.Hash, team.Team.ToString(CultureInfo.InvariantCulture),
                result.Tick.ToString(CultureInfo.InvariantCulture), result.Outcome,
                string.Join(",", expectedWinners.Select(t => t.ToString(CultureInfo.InvariantCulture))));
            return new WordboundStoryEpilogue(id, copy.Title, copy.Cue, key);
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

        public WordboundStoryNotice ContextualCue(ProgressSnapshot progress, IEnumerable<string> narrativeFlags, string actor)
        {
            if (progress == null || progress.Migrating || progress.Result != null) return null;
            var player = progress.Players.SingleOrDefault(p => p.Id == actor)
                ?? throw new ArgumentException("Story reader is not in the roster.");
            if (player.Returned || !player.Connected || player.Eliminated) return null;
            var team = progress.Teams.Single(t => t.Team == player.Team);
            if (team.Finished) return null;
            return Journal(progress, narrativeFlags, actor).LastOrDefault(n => n.Lap == team.Lap + 1);
        }

        public WordboundStoryNotice ContextualCue(MatchSnapshot frame, string actor)
        {
            if (frame == null) return null;
            if (frame.Binding.ManifestHash != course.Hash || frame.Binding.CourseHash != course.SourceHash)
                throw new ArgumentException("Story progress must belong to the exact course.");
            return ContextualCue(frame.Progress, frame.NarrativeFlags, actor);
        }
    }
}
