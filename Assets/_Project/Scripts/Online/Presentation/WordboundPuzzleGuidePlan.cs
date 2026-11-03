using System;
using System.Collections.Generic;
using System.Linq;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public enum WordboundGuideKind { Socket, Source, Arrival, Receiver, Pad, ArmPath, Checkpoint, Finish }

    public sealed class WordboundGuideMarker
    {
        public string Id { get; }
        public string Label { get; }
        public WordboundGuideKind Kind { get; }
        public CoursePoint Position { get; }
        public CoursePoint End { get; }
        public CourseBox Bounds { get; }
        public int RadiusMm { get; }
        public int Yaw { get; }
        public bool Ready { get; }
        public string SourceId { get; }

        internal WordboundGuideMarker(string id, string label, WordboundGuideKind kind, CoursePoint at,
            CoursePoint end = default, CourseBox bounds = default, int radiusMm = 0, int yaw = 0, bool ready = true,
            string sourceId = "")
        { Id = id; Label = label; Kind = kind; Position = at; End = end; Bounds = bounds; RadiusMm = radiusMm; Yaw = yaw; Ready = ready; SourceId = sourceId; }
    }

    public static class WordboundPuzzleGuidePlan
    {
        public const string PickUpKeepsake = "PICK UP KEEPSAKE";

        public static IReadOnlyList<WordboundGuideMarker> Build(ResolvedCourse course, MatchSnapshot frame, string localActor)
        {
            if (course == null) throw new ArgumentNullException(nameof(course));
            if (frame == null) return Array.Empty<WordboundGuideMarker>();
            if (frame.Binding.ManifestHash != course.Hash || frame.Binding.CourseHash != course.SourceHash)
                throw new ArgumentException("Guide frame must belong to the exact resolved course.");
            if (!frame.Binding.Seats.Any(s => s.PlayerId == localActor))
                throw new ArgumentException("Guide player is not in the bound roster.");
            if (course.Play.ModeId == "Freestyle")
                return WordboundPracticeGuidePlan.Build(course, frame, localActor);
            return Array.AsReadOnly(Build(course, frame.Progress, localActor).Where(marker =>
                marker.Kind != WordboundGuideKind.Source ||
                frame.Bodies.Any(b => b.Kind == WorldBodyKind.Item && b.SourceId == marker.SourceId && b.Active)).ToArray());
        }

        public static IReadOnlyList<WordboundGuideMarker> Build(ResolvedCourse course, ProgressSnapshot progress, string localActor)
        {
            if (course == null) throw new ArgumentNullException(nameof(course));
            if (progress == null || progress.Migrating || progress.Result != null || course.Laps.Count == 0)
                return Array.Empty<WordboundGuideMarker>();
            var actor = progress.Players.SingleOrDefault(p => p.Id == localActor);
            if (actor == null) throw new ArgumentException("Guide player is not in the progress roster.");
            if (actor.Returned || !actor.Connected || actor.Eliminated) return Array.Empty<WordboundGuideMarker>();
            var team = progress.Teams.Single(t => t.Team == actor.Team);
            if (team.Finished || team.Lap < 0 || team.Lap >= course.Laps.Count) return Array.Empty<WordboundGuideMarker>();
            var lap = course.Laps[team.Lap];
            var markers = new List<WordboundGuideMarker>();
            if (team.NextCheckpoint < 0 || team.NextCheckpoint > lap.Checkpoints.Count)
                throw new ArgumentException("Guide checkpoint is outside the selected lap.");
            bool carried = team.KeepsakeCarrier != null;
            if (team.NextCheckpoint == lap.Checkpoints.Count)
            {
                var last = lap.Checkpoints[lap.Checkpoints.Count - 1];
                var finish = course.RecoveryAnchors.Single(r => r.Id == last.RecoveryAnchorId);
                markers.Add(new WordboundGuideMarker("finish:" + lap.Number, carried ? "LAP FINISH\nBring keepsake" : PickUpKeepsake,
                    WordboundGuideKind.Finish, finish.Position, radiusMm: finish.RadiusMm, ready: carried));
                return markers.AsReadOnly();
            }
            var checkpoint = lap.Checkpoints[team.NextCheckpoint];
            var completed = new HashSet<string>(team.CompletedPuzzles, StringComparer.Ordinal);
            bool solved = checkpoint.PuzzleIds.All(completed.Contains);
            var anchor = course.RecoveryAnchors.Single(r => r.Id == checkpoint.RecoveryAnchorId);
            markers.Add(new WordboundGuideMarker("checkpoint:" + checkpoint.Id,
                !solved ? "CHECKPOINT\nSolve puzzle first" : carried ? "CHECKPOINT\nBring keepsake" : PickUpKeepsake,
                WordboundGuideKind.Checkpoint, anchor.Position, radiusMm: anchor.RadiusMm, ready: solved && carried));
            foreach (string id in checkpoint.PuzzleIds.Where(id => !completed.Contains(id)))
            {
                var puzzle = course.Puzzles.Single(p => p.Id == id);
                if (!puzzle.PrerequisiteIds.All(completed.Contains)) continue;
                var site = course.PhysicalSites.SingleOrDefault(s => s.Lap == lap.Number &&
                    s.PuzzleId == id && s.CheckpointId == checkpoint.Id);
                if (site == null) continue;
                AddSite(markers, course, site, puzzle, actor.Team);
            }
            return markers.AsReadOnly();
        }

        static void AddSite(List<WordboundGuideMarker> markers, ResolvedCourse course,
            ResolvedCoursePhysicalSite site, ResolvedCoursePuzzle puzzle, int team)
        {
            string id = site.Lap + ":" + site.PuzzleId + ":";
            markers.Add(new WordboundGuideMarker(id + "socket", SocketLabel(puzzle), WordboundGuideKind.Socket,
                site.Position, radiusMm: site.SocketRadiusMm, yaw: site.Yaw));
            if (puzzle.PresetId == "CollectLetters")
                foreach (var source in course.Placements.Where(p => p.ZoneId == site.PropSourceZoneId &&
                    (p.TeamIndex < 0 || p.TeamIndex == team)))
                    markers.Add(new WordboundGuideMarker(id + "source:" + source.Id, "SMASH · " + source.Letters,
                        WordboundGuideKind.Source, source.Position, radiusMm: 700, sourceId: source.Id));
            if (puzzle.PresetId == "RedirectWind")
                AddBox(markers, id + "receiver", "LOOSE " + site.WindReceiverRecipeId,
                    WordboundGuideKind.Receiver, site.ReceiverStartBounds);
            if (puzzle.PresetId == "RedirectWind" || puzzle.PresetId == "BuildRoute" || puzzle.PresetId == "TimedMechanism")
                AddBox(markers, id + "arrival", puzzle.PresetId == "RedirectWind" ? "WIND TARGET" :
                    puzzle.PresetId == "BuildRoute" ? "REACH THIS MARKER" : "CROSS WITH KEEPSAKE",
                    WordboundGuideKind.Arrival, site.ArrivalBounds);
            for (int i = 0; i < site.FloorPadBounds.Count; i++)
                AddBox(markers, id + "pad:" + i, "WEIGHT PAD " + (i + 1), WordboundGuideKind.Pad, site.FloorPadBounds[i]);
            foreach (var arm in site.Fixtures)
            {
                var half = new CoursePoint((arm.SizeMm.X + 1) / 2, 0, (arm.SizeMm.Z + 1) / 2);
                var box = new CourseBox(new CoursePoint(Math.Min(arm.From.X, arm.To.X) - half.X,
                        Math.Min(arm.From.Y, arm.To.Y), Math.Min(arm.From.Z, arm.To.Z) - half.Z),
                    new CoursePoint(Math.Max(arm.From.X, arm.To.X) + half.X,
                        Math.Max(arm.From.Y, arm.To.Y) + arm.SizeMm.Y, Math.Max(arm.From.Z, arm.To.Z) + half.Z));
                markers.Add(new WordboundGuideMarker(id + "arm:" + arm.Id, "MOVING ARM · CLOCK slows it",
                    WordboundGuideKind.ArmPath, arm.From, arm.To, box));
            }
        }

        static void AddBox(List<WordboundGuideMarker> markers, string id, string label, WordboundGuideKind kind, CourseBox box)
        {
            markers.Add(new WordboundGuideMarker(id, label, kind,
                new CoursePoint((box.Min.X + box.Max.X) / 2, box.Min.Y, (box.Min.Z + box.Max.Z) / 2), bounds: box));
        }

        static string SocketLabel(ResolvedCoursePuzzle puzzle) => puzzle.PresetId switch
        {
            "CollectLetters" => "SPELL " + puzzle.RecipeId,
            "BuildRoute" => "PLACE " + puzzle.RecipeId + " · launch",
            "RedirectWind" => "PLACE " + puzzle.RecipeId + " · face arrow",
            "BalanceWeight" => "PLACE " + puzzle.RecipeId + " · cover both pads",
            "TimedMechanism" => "PLACE " + puzzle.RecipeId + " · slow arm",
            _ => "PLACE " + puzzle.RecipeId
        };
    }
}
