using System;
using System.Collections.Generic;
using System.Linq;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public static class WordboundPracticeGuidePlan
    {
        public static ResolvedCoursePhysicalSite Nearest(ResolvedCourse course, MatchSnapshot frame, string actorId,
            float maximumDistance = 3)
        {
            if (course == null || frame == null || course.Play.ModeId != "Freestyle" ||
                float.IsNaN(maximumDistance) || float.IsInfinity(maximumDistance) || maximumDistance < 0 || maximumDistance > 30 ||
                frame.Binding.ManifestHash != course.Hash || frame.Binding.CourseHash != course.SourceHash)
                return null;
            var actor = frame.Progress.Players.SingleOrDefault(p => p.Id == actorId);
            var body = frame.Bodies.SingleOrDefault(b => b.Kind == WorldBodyKind.Player && b.SourceId == actorId);
            if (actor == null || actor.Returned || !actor.Connected || actor.Eliminated || body == null ||
                !body.Active || frame.Progress.Migrating || frame.Progress.Result != null) return null;
            return course.PhysicalSites.Where(s => Distance(s.Position, body.Position) <= maximumDistance * maximumDistance)
                .OrderBy(s => Completed(frame, s)).ThenBy(s => Distance(s.Position, body.Position))
                .ThenBy(s => s.Hash, StringComparer.Ordinal).FirstOrDefault();
        }

        public static bool Completed(MatchSnapshot frame, ResolvedCoursePhysicalSite site) =>
            frame != null && site != null && frame.Mechanisms.Any(m => m.Team == 0 && m.Lap == site.Lap - 1 &&
                m.PuzzleId == site.PuzzleId && m.Solved);

        public static string Status(MatchSnapshot frame, ResolvedCoursePhysicalSite site) =>
            Completed(frame, site) ? "Completed this session · start a fresh session to repeat" :
            frame?.Practice.SiteHash == site?.Hash ? "Practice active · use the real recipe and marked sensors" :
            "Practice this station";

        public static IReadOnlyList<WordboundGuideMarker> Build(ResolvedCourse course, MatchSnapshot frame, string actorId)
        {
            if (course == null) throw new ArgumentNullException(nameof(course));
            if (frame == null || course.Play.ModeId != "Freestyle") return Array.Empty<WordboundGuideMarker>();
            if (frame.Binding.ManifestHash != course.Hash || frame.Binding.CourseHash != course.SourceHash)
                throw new ArgumentException("Practice guide needs the exact canonical course.");
            var actor = frame.Progress.Players.SingleOrDefault(p => p.Id == actorId);
            if (actor == null) throw new ArgumentException("Practice guide player is not in this roster.");
            if (actor.Returned || !actor.Connected || actor.Eliminated || frame.Progress.Migrating || frame.Progress.Result != null)
                return Array.Empty<WordboundGuideMarker>();
            var markers = new List<WordboundGuideMarker>();
            var selected = WordboundPracticePolicy.Selected(course, frame.Practice);
            var nearby = Nearest(course, frame, actorId, 20);
            if (nearby != null && nearby.Hash != selected?.Hash)
                markers.Add(new WordboundGuideMarker("practice:" + nearby.Hash,
                    Status(frame, nearby), WordboundGuideKind.Socket, nearby.Position, radiusMm: 700,
                    ready: !Completed(frame, nearby)));
            if (selected == null) return markers.AsReadOnly();
            bool complete = Completed(frame, selected);
            var puzzle = course.Puzzles.Single(p => p.Id == selected.PuzzleId);
            string prefix = "practice:" + selected.Hash + ":";
            markers.Add(new WordboundGuideMarker(prefix + (complete ? "complete" : "socket"),
                complete ? Status(frame, selected) : "PRACTICE · PLACE " + puzzle.RecipeId,
                WordboundGuideKind.Socket, selected.Position, radiusMm: selected.SocketRadiusMm, yaw: selected.Yaw));
            if (complete) return markers.AsReadOnly();
            foreach (var supply in course.Supplies.Where(s => puzzle.RecoverySupplyIds.Contains(s.SourceId)))
                markers.Add(new WordboundGuideMarker(prefix + supply.Id, "RECOVERY LETTERS · " + supply.Letters,
                    WordboundGuideKind.Receiver, supply.Position, radiusMm: 500));
            foreach (var source in course.Placements.Where(p => p.ZoneId == selected.PropSourceZoneId &&
                frame.Bodies.Any(b => b.SourceId == p.Id && b.Kind == WorldBodyKind.Item && b.Active)))
                markers.Add(new WordboundGuideMarker(prefix + source.Id, "SMASH · " + source.Letters,
                    WordboundGuideKind.Source, source.Position, radiusMm: 700, sourceId: source.Id));
            void Box(string id, string label, WordboundGuideKind kind, CourseBox box) =>
                markers.Add(new WordboundGuideMarker(prefix + id, label, kind,
                    new CoursePoint((box.Min.X + box.Max.X) / 2, box.Min.Y, (box.Min.Z + box.Max.Z) / 2), bounds: box));
            if (puzzle.PresetId == "RedirectWind")
                Box("receiver", "LOOSE " + selected.WindReceiverRecipeId, WordboundGuideKind.Receiver, selected.ReceiverStartBounds);
            if (puzzle.PresetId == "BuildRoute" || puzzle.PresetId == "RedirectWind" || puzzle.PresetId == "TimedMechanism")
                Box("arrival", puzzle.PresetId == "BuildRoute" ? "REACH THIS MARKER" :
                    puzzle.PresetId == "RedirectWind" ? "WIND TARGET" : "CROSS WITH KEEPSAKE",
                    WordboundGuideKind.Arrival, selected.ArrivalBounds);
            for (int i = 0; i < selected.FloorPadBounds.Count; i++)
                Box("pad:" + i, "WEIGHT PAD " + (i + 1), WordboundGuideKind.Pad, selected.FloorPadBounds[i]);
            foreach (var arm in selected.Fixtures)
                markers.Add(new WordboundGuideMarker(prefix + arm.Id, "MOVING ARM · CLOCK slows it",
                    WordboundGuideKind.ArmPath, arm.From, arm.To,
                    new CourseBox(new CoursePoint(Math.Min(arm.From.X, arm.To.X) - arm.SizeMm.X / 2,
                        Math.Min(arm.From.Y, arm.To.Y), Math.Min(arm.From.Z, arm.To.Z) - arm.SizeMm.Z / 2),
                        new CoursePoint(Math.Max(arm.From.X, arm.To.X) + arm.SizeMm.X / 2,
                            Math.Max(arm.From.Y, arm.To.Y) + arm.SizeMm.Y, Math.Max(arm.From.Z, arm.To.Z) + arm.SizeMm.Z / 2))));
            if (puzzle.PresetId == "TimedMechanism" && frame.Progress.Teams[0].KeepsakeCarrier == null)
            {
                var keepsake = frame.Bodies.Single(b => b.Id == "keepsake:0");
                markers.Add(new WordboundGuideMarker(prefix + "keepsake", "TAKE SHARED KEEPSAKE",
                    WordboundGuideKind.Receiver, new CoursePoint((int)(keepsake.Position.X * 1000),
                        (int)(keepsake.Position.Y * 1000), (int)(keepsake.Position.Z * 1000)), radiusMm: 500));
            }
            return markers.AsReadOnly();
        }

        static float Distance(CoursePoint point, WorldVector position) =>
            new WorldVector(point.X / 1000f, point.Y / 1000f, point.Z / 1000f).DistanceSquared(position);
    }
}
