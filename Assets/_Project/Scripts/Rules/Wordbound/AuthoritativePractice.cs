using System;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed partial class AuthoritativeMatch
    {
        MatchPracticeState practice = MatchPracticeState.Empty;
        bool initialPracticePlacementUnresolved;
        bool IsPractice => WordboundPracticePolicy.Supports(Mode);
        ResolvedCoursePhysicalSite PracticeSite => WordboundPracticePolicy.Selected(course, practice);

        void InitializePractice()
        {
            if (!IsPractice) return;
            AddBody(new WorldBodyState("keepsake:0", WorldBodyKind.Keepsake, Spawn(0),
                ownerTeam: 0, protectedResource: true));
            foreach (var site in course.PhysicalSites)
                mechanisms.Add(Key(site.PuzzleId, 0, site.Lap - 1),
                    new MatchMechanismState(site.PuzzleId, 0, site.Lap - 1, physical: InitialPhysical(site.PuzzleId, site.Lap - 1)));
        }

        void PlaceInitialPracticeKeepsake()
        {
            if (!IsPractice) return;
            var start = course.Spawns.First(s => s.Kind == "Start");
            var module = course.Geometry.Single(m => m.Id == start.ModuleId);
            var anchor = course.RecoveryAnchors.Where(a => a.ModuleId == module.Id && a.AfterPuzzleIds.Count == 0)
                .OrderBy(a => a.Id, StringComparer.Ordinal).FirstOrDefault(a => InitialPracticeSpace(a.Position, module));
            initialPracticePlacementUnresolved = anchor == null;
            var body = bodies["keepsake:0"];
            bodies[body.Id] = body.At(anchor == null ? body.Position : Position(anchor.Position),
                active: anchor != null, kinematic: anchor == null);
        }

        void RequireInitialPracticePlacement()
        {
            if (initialPracticePlacementUnresolved)
                throw new InvalidOperationException("Add a clear recovery anchor on the starting room floor before starting Freestyle.");
        }

        bool InitialPracticeSpace(CoursePoint at, ResolvedCourseModule module)
        {
            const int half = 225, height = 450, clearance = 20, wallHalf = 100;
            var room = module.Bounds;
            if (at.Y != room.Min.Y || (long)at.Y + height + clearance > room.Max.Y ||
                (long)at.X - half - clearance < (long)room.Min.X + wallHalf ||
                (long)at.X + half + clearance > (long)room.Max.X - wallHalf ||
                (long)at.Z - half - clearance < (long)room.Min.Z + wallHalf ||
                (long)at.Z + half + clearance > (long)room.Max.Z - wallHalf) return false;
            var volume = new CourseBox(new CoursePoint(at.X - half - clearance, at.Y - clearance, at.Z - half - clearance),
                new CoursePoint(at.X + half + clearance, at.Y + height + clearance, at.Z + half + clearance));
            foreach (var spawn in course.Spawns.Where(s => s.Kind == "Start" || s.Kind == "Respawn"))
                if (volume.Intersects(InitialPracticeBox(spawn.Position, 600, 1800, 600))) return false;
            foreach (var placement in course.Placements)
            {
                var asset = courseCatalogue.Assets[placement.AssetId];
                int width = placement.Yaw % 180 == 0 ? asset.WidthMm : asset.DepthMm;
                int depth = placement.Yaw % 180 == 0 ? asset.DepthMm : asset.WidthMm;
                var occupied = placement.Purpose == "Resources" ? InitialPracticeBox(placement.Position, 300, 120, 300) :
                    InitialPracticeBox(placement.Position, width, asset.HeightMm, depth);
                if (volume.Intersects(occupied)) return false;
            }
            foreach (var supply in course.Supplies)
                if (volume.Intersects(InitialPracticeBox(supply.Position, 300, 120, 300))) return false;
            foreach (var site in course.PhysicalSites)
                foreach (var fixture in site.Fixtures)
                {
                    var from = InitialPracticeBox(fixture.From, fixture.SizeMm.X, fixture.SizeMm.Y, fixture.SizeMm.Z);
                    var to = InitialPracticeBox(fixture.To, fixture.SizeMm.X, fixture.SizeMm.Y, fixture.SizeMm.Z);
                    var sweep = new CourseBox(new CoursePoint(Math.Min(from.Min.X, to.Min.X), Math.Min(from.Min.Y, to.Min.Y), Math.Min(from.Min.Z, to.Min.Z)),
                        new CoursePoint(Math.Max(from.Max.X, to.Max.X), Math.Max(from.Max.Y, to.Max.Y), Math.Max(from.Max.Z, to.Max.Z)));
                    if (volume.Intersects(sweep)) return false;
                }
            foreach (var connection in course.Connections)
            {
                if (connection.GatePuzzleId != "" && volume.Intersects(
                    InitialPracticeBox(connection.From, connection.WidthMm, 240, connection.WidthMm))) return false;
                if (connection.Kind == "Walk" || connection.Kind == "Door") continue;
                var a = connection.From; var b = connection.To;
                int radius = (connection.WidthMm + 1) / 2;
                int solidMargin = radius + 240;
                var solid = new CourseBox(new CoursePoint(Math.Min(a.X, b.X) - solidMargin,
                    Math.Min(a.Y, b.Y) - solidMargin, Math.Min(a.Z, b.Z) - solidMargin),
                    new CoursePoint(Math.Max(a.X, b.X) + solidMargin, Math.Max(a.Y, b.Y) + solidMargin, Math.Max(a.Z, b.Z) + solidMargin));
                if (volume.Intersects(solid)) return false;
                if (connection.Kind != "Stairs" && connection.Kind != "Ramp") continue;
                bool alongX = a.X != b.X;
                var envelope = new CourseBox(new CoursePoint(
                    alongX ? Math.Min(a.X, b.X) : a.X - radius, Math.Min(a.Y, b.Y) - 240,
                    alongX ? a.Z - radius : Math.Min(a.Z, b.Z)),
                    new CoursePoint(alongX ? Math.Max(a.X, b.X) : a.X + radius, Math.Max(a.Y, b.Y),
                        alongX ? a.Z + radius : Math.Max(a.Z, b.Z)));
                bool footprint = volume.Min.X < envelope.Max.X && volume.Max.X > envelope.Min.X &&
                    volume.Min.Z < envelope.Max.Z && volume.Max.Z > envelope.Min.Z;
                bool cutoutFloor = at.Y > Math.Min(a.Y, b.Y) && (long)at.Y <= (long)Math.Max(a.Y, b.Y) + 2640;
                if (footprint && cutoutFloor) return false;
            }
            return true;
        }

        static CourseBox InitialPracticeBox(CoursePoint at, int width, int height, int depth) => new CourseBox(
            new CoursePoint(at.X - (width + 1) / 2, at.Y, at.Z - (depth + 1) / 2),
            new CoursePoint(at.X + (width + 1) / 2, at.Y + height, at.Z + (depth + 1) / 2));

        bool PracticeActive(string puzzle, int lap) => IsPractice && PracticeSite is ResolvedCoursePhysicalSite site &&
            site.PuzzleId == puzzle && site.Lap == lap + 1 && !mechanisms[Key(puzzle, 0, lap)].Solved;

        CommandEffect SelectPractice(WordboundSeat seat, PlayerCommand command, WorldBodyState origin)
        {
            if (!IsPractice) return Effect("Unavailable");
            if (command.Revision != 0 || command.Slot != -1 || command.Value != "") return Effect("InvalidPractice");
            var site = course.PhysicalSites.SingleOrDefault(s => s.Hash == command.Target);
            if (site == null) return Effect("UnknownPractice");
            var at = Position(site.Position);
            if (origin.Position.DistanceSquared(at) > 9 || !world.HasLineOfSight(seat.PlayerId, origin.Position, at))
                return Effect("OutOfReach");
            if (mechanisms[Key(site.PuzzleId, 0, site.Lap - 1)].Solved) return Effect("AlreadySolved");
            if (practice.SiteHash == site.Hash) return Effect("Ok", site.PuzzleId);
            foreach (var prior in mechanisms.Values.Where(m => !m.Solved).ToArray())
                mechanisms[Key(prior.PuzzleId, prior.Team, prior.Lap)] = new MatchMechanismState(
                    prior.PuzzleId, prior.Team, prior.Lap, physical: prior.Physical.Reset());
            practice = new MatchPracticeState(site.Hash, Tick + 1);
            return Effect("Ok", site.PuzzleId);
        }

        WorldVector PracticeRecoveryPosition()
        {
            var site = PracticeSite;
            var anchor = site == null ? null : course.RecoveryAnchors
                .Where(a => a.ModuleId == course.Puzzles.Single(p => p.Id == site.PuzzleId).ModuleId)
                .OrderBy(a => a.Id, StringComparer.Ordinal).FirstOrDefault();
            return anchor == null ? Spawn(0) : Position(anchor.Position);
        }

        bool PracticeRecoverySource(string sourceId) =>
            IsPractice && resourceLifecycle.PracticeSource(sourceId, practice, mechanisms.Values);
    }
}
