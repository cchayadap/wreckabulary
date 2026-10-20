using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public enum WorkshopSitePart { Socket, Arrival, ReceiverStart, FirstPad, SecondPad, FixtureStart, FixtureEnd }

    public sealed class WorkshopSiteDrag
    {
        readonly WorkshopSnapshot original;
        readonly int revision, lap, fixtureIndex;
        readonly string puzzleId;
        readonly WorkshopSitePart part;
        CoursePoint delta;
        public CoursePhysicalSite Preview { get; private set; }

        public WorkshopSiteDrag(WorkshopHistory history, int lap, string puzzleId, WorkshopSitePart part, int fixtureIndex = 0)
        {
            original = history?.Current ?? throw new ArgumentNullException(nameof(history));
            revision = history.Revision; this.lap = lap; this.puzzleId = puzzleId; this.part = part; this.fixtureIndex = fixtureIndex;
            Move(0, 0);
        }
        public void Move(int x, int z)
        {
            delta = new CoursePoint(WorkshopCommands.Snap(x, original.GridMm), 0, WorkshopCommands.Snap(z, original.GridMm));
            var draft = original.Edit();
            WorkshopPhysicalSites.MovePart(draft, lap, puzzleId, part, delta, fixtureIndex, false);
            Preview = WorkshopPhysicalSites.Copy(draft.Course.PhysicalSites.Single(s => s.Lap == lap && s.PuzzleId == puzzleId));
        }
        public bool Commit(WorkshopHistory history)
        {
            if (history.Revision != revision) throw new InvalidOperationException("The draft changed. Start the drag again.");
            return history.Execute("Move puzzle " + part, draft =>
                WorkshopPhysicalSites.MovePart(draft, lap, puzzleId, part, delta, fixtureIndex, false));
        }
    }

    public static class WorkshopPhysicalSites
    {
        public static string PlaytestIssue(ResolvedCourse course)
        {
            foreach (var lap in course.Laps)
                foreach (var checkpoint in lap.Checkpoints)
                    foreach (string puzzleId in checkpoint.PuzzleIds)
                    {
                        var site = course.PhysicalSites.SingleOrDefault(s => s.Lap == lap.Number && s.PuzzleId == puzzleId);
                        if (site == null) return $"Add physical controls for {puzzleId} on lap {lap.Number} before playtesting.";
                        if (site.CheckpointId != checkpoint.Id)
                            return $"Link {puzzleId} physical controls to selected checkpoint {checkpoint.Id} on lap {lap.Number}.";
                    }
            return "";
        }

        public static CoursePhysicalSite Create(WorkshopDraft draft, CourseCatalogue catalogue, int lapNumber, string puzzleId)
        {
            RequireEditable(draft, puzzleId);
            if (draft.Course.PhysicalSites.Any(s => s.Lap == lapNumber && s.PuzzleId == puzzleId))
                throw new InvalidOperationException("This puzzle already has physical controls on that lap.");
            var course = draft.Course;
            var puzzle = course.Puzzles.Single(p => p.Id == puzzleId);
            var module = course.Modules.Single(m => m.Id == puzzle.ModuleId);
            var lap = course.Laps.SingleOrDefault(l => l.Number == lapNumber)
                ?? throw new InvalidOperationException("Add this lap to the course before authoring its physical controls.");
            var lapCheckpoints = lap.SectionIds.SelectMany(id => course.RouteSections.Where(s => s.Id == id)
                .SelectMany(s => s.Alternatives).SelectMany(a => a.CheckpointIds)).ToHashSet(StringComparer.Ordinal);
            var checkpoint = course.Checkpoints.FirstOrDefault(c => c.ModuleId == module.Id &&
                c.PuzzleIds.Contains(puzzleId) && lapCheckpoints.Contains(c.Id))
                ?? course.Checkpoints.FirstOrDefault(c => c.ModuleId == module.Id && lapCheckpoints.Contains(c.Id))
                ?? throw new InvalidOperationException("This module needs an ordered checkpoint on the selected lap.");
            var supply = course.Supplies.FirstOrDefault(s => puzzle.RecoverySupplyIds.Contains(s.Id) &&
                s.ModuleId == module.Id && s.Renewable && s.Protected && s.Letters == puzzle.RequiredLetters)
                ?? throw new InvalidOperationException("Add protected renewable recipe letters to this puzzle first.");
            if (!checkpoint.PuzzleIds.Contains(puzzleId)) checkpoint.PuzzleIds.Add(puzzleId);
            var at = module.Position;
            var site = new CoursePhysicalSite { Lap = lapNumber, PuzzleId = puzzle.Id, CheckpointId = checkpoint.Id,
                RecoverySupplyId = supply.Id, Position = at, Target = at };
            CoursePoint Offset(int x, int y, int z) => Add(at, new CoursePoint(x, y, z));
            CourseBox Sensor(CoursePoint centre, int x = 500, int height = 900, int z = 500) =>
                new CourseBox(Add(centre, new CoursePoint(-x / 2, 0, -z / 2)),
                    Add(centre, new CoursePoint(x / 2, height, z / 2)));
            string ReceiverSupply(string recipe)
            {
                if (!catalogue.Assets.TryGetValue(recipe, out var asset) || !asset.CanCraft)
                    throw new ArgumentException("The physical controls need approved " + recipe + " equipment.");
                string id = WorkshopCommands.Unique("recovery_" + puzzle.Id, course.Supplies.Select(s => s.Id));
                course.Supplies.Add(new CourseSupply { Id = id, ModuleId = module.Id, Letters = asset.Letters,
                    Position = Offset(-2000, 0, -2000), AfterPuzzleIds = puzzle.PrerequisiteIds.ToList() });
                puzzle.RecoverySupplyIds.Add(id); return id;
            }
            string Prop(string recipe, CoursePoint position)
            {
                if (!catalogue.Assets.ContainsKey(recipe)) throw new ArgumentException("The physical controls need approved " + recipe + " equipment.");
                string id = WorkshopCommands.Unique("source_" + puzzle.Id, course.Zones.Select(z => z.Id));
                course.Zones.Add(new CourseZone { Id = id, ModuleId = module.Id, Purpose = "Props", Placement = "Fixed",
                    FixedPosition = position, Bounds = catalogue.Bounds(module), MinCount = 1, MaxCount = 1,
                    LayerIds = new List<string> { module.LayerId }, Yaws = new List<int> { 0 },
                    Pool = new List<CourseWeightedAsset> { new CourseWeightedAsset { AssetId = recipe } } });
                return id;
            }
            switch (puzzle.PresetId)
            {
                case "CollectLetters":
                    site.PropSourceZoneId = Prop(puzzle.RecipeId, Offset(2000, 0, 0));
                    if (catalogue.Assets.ContainsKey("HAMMER"))
                    { site.ToolRecipeId = "HAMMER"; site.ToolRecoverySupplyId = ReceiverSupply("HAMMER"); }
                    break;
                case "PlaceObject": break;
                case "BuildRoute":
                    if (!catalogue.Assets.TryGetValue(puzzle.RecipeId, out var pad))
                        throw new ArgumentException("Choose an approved jump-pad recipe.");
                    site.Target = Offset(0, 1000, pad.DepthMm / 2 + 800); site.ArrivalBounds = Sensor(site.Target);
                    break;
                case "RedirectWind":
                    site.WindReceiverRecipeId = "STOOL"; site.ReceiverRecoverySupplyId = ReceiverSupply("STOOL");
                    site.WindReceiverZoneId = Prop("STOOL", Offset(0, 0, 900));
                    site.ReceiverStartBounds = Sensor(Offset(0, 0, 900), 700, 700, 700);
                    site.Target = Offset(0, 0, 1800); site.ArrivalBounds = Sensor(site.Target);
                    break;
                case "BalanceWeight":
                    if (!catalogue.Assets.TryGetValue(puzzle.RecipeId, out var cover))
                        throw new ArgumentException("Choose an approved cover recipe.");
                    site.HoldTicks = 60;
                    site.FloorPadBounds.Add(Sensor(Offset(-cover.WidthMm / 4, 0, 0), 200, 80, 200));
                    site.FloorPadBounds.Add(Sensor(Offset(cover.WidthMm / 4, 0, 0), 200, 80, 200));
                    break;
                case "TimedMechanism":
                    site.Target = Offset(0, 0, 1500); site.ArrivalBounds = Sensor(site.Target); site.ActiveWindowTicks = 180;
                    site.Fixtures.Add(new CourseMovingFixture {
                        Id = WorkshopCommands.Unique("arm_" + puzzle.Id, course.PhysicalSites.SelectMany(s => s.Fixtures).Select(f => f.Id)),
                        From = Offset(-900, 0, 1000), To = Offset(900, 0, 1000), SizeMm = new CoursePoint(600, 1200, 200),
                        PeriodTicks = 240, PhaseTicks = 0 });
                    break;
                default: throw new ArgumentException("Choose one of the six approved physical puzzle families.");
            }
            Set(draft, site);
            return Copy(site);
        }

        public static CoursePhysicalSite Copy(CoursePhysicalSite source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new CoursePhysicalSite
            {
                Lap = source.Lap, PuzzleId = source.PuzzleId, CheckpointId = source.CheckpointId,
                RecoverySupplyId = source.RecoverySupplyId, ToolRecoverySupplyId = source.ToolRecoverySupplyId,
                ToolRecipeId = source.ToolRecipeId, PropSourceZoneId = source.PropSourceZoneId,
                WindReceiverZoneId = source.WindReceiverZoneId, WindReceiverRecipeId = source.WindReceiverRecipeId,
                ReceiverRecoverySupplyId = source.ReceiverRecoverySupplyId, Position = source.Position, Target = source.Target,
                ArrivalBounds = source.ArrivalBounds, ReceiverStartBounds = source.ReceiverStartBounds,
                FloorPadBounds = source.FloorPadBounds.ToList(), Yaw = source.Yaw,
                SocketRadiusMm = source.SocketRadiusMm, YawTolerance = source.YawTolerance,
                HoldTicks = source.HoldTicks, ActiveWindowTicks = source.ActiveWindowTicks,
                Fixtures = source.Fixtures.Select(f => new CourseMovingFixture
                { Id = f.Id, From = f.From, To = f.To, SizeMm = f.SizeMm, PeriodTicks = f.PeriodTicks, PhaseTicks = f.PhaseTicks }).ToList()
            };
        }

        public static void Set(WorkshopDraft draft, CoursePhysicalSite value)
        {
            RequireEditable(draft, value.PuzzleId);
            var copy = Copy(value);
            draft.Course.PhysicalSites.RemoveAll(s => s.Lap == value.Lap && s.PuzzleId == value.PuzzleId);
            draft.Course.PhysicalSites.Add(copy); draft.Course.Schema = 2;
        }

        public static void Delete(WorkshopDraft draft, int lap, string puzzleId)
        {
            RequireEditable(draft, puzzleId);
            draft.Course.PhysicalSites.RemoveAll(s => s.Lap == lap && s.PuzzleId == puzzleId);
        }

        public static void MovePart(WorkshopDraft draft, int lap, string puzzleId, WorkshopSitePart part,
            CoursePoint delta, int fixtureIndex = 0, bool snap = true)
        {
            RequireEditable(draft, puzzleId);
            var site = draft.Course.PhysicalSites.Single(s => s.Lap == lap && s.PuzzleId == puzzleId);
            if (snap) delta = new CoursePoint(WorkshopCommands.Snap(delta.X, draft.GridMm),
                WorkshopCommands.Snap(delta.Y, draft.GridMm), WorkshopCommands.Snap(delta.Z, draft.GridMm));
            CoursePoint Move(CoursePoint p) => Add(p, delta);
            switch (part)
            {
                case WorkshopSitePart.Socket:
                    if (delta.Y != 0) throw new ArgumentException("The socket stays on its module floor.");
                    site.Position = Move(site.Position); break;
                case WorkshopSitePart.Arrival:
                    RequireVolume(site.ArrivalBounds); site.ArrivalBounds = TransformBox(site.ArrivalBounds, Move);
                    site.Target = Move(site.Target); break;
                case WorkshopSitePart.ReceiverStart:
                    RequireVolume(site.ReceiverStartBounds);
                    site.ReceiverStartBounds = TransformBox(site.ReceiverStartBounds, Move);
                    var receiver = draft.Course.Zones.SingleOrDefault(z => z.Id == site.WindReceiverZoneId);
                    if (receiver != null)
                    {
                        receiver.Bounds = TransformBox(receiver.Bounds, Move);
                        if (receiver.Placement == "Fixed") receiver.FixedPosition = Move(receiver.FixedPosition);
                    }
                    break;
                case WorkshopSitePart.FirstPad:
                case WorkshopSitePart.SecondPad:
                    int pad = part == WorkshopSitePart.FirstPad ? 0 : 1;
                    if (pad >= site.FloorPadBounds.Count) throw new ArgumentException("This site has no selected switch pad.");
                    site.FloorPadBounds[pad] = TransformBox(site.FloorPadBounds[pad], Move); break;
                case WorkshopSitePart.FixtureStart:
                case WorkshopSitePart.FixtureEnd:
                    if (fixtureIndex < 0 || fixtureIndex >= site.Fixtures.Count) throw new ArgumentException("Choose a moving fixture.");
                    if (part == WorkshopSitePart.FixtureStart) site.Fixtures[fixtureIndex].From = Move(site.Fixtures[fixtureIndex].From);
                    else site.Fixtures[fixtureIndex].To = Move(site.Fixtures[fixtureIndex].To);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(part));
            }
        }

        internal static void TransformOwned(CourseDocument course, ISet<string> moduleIds,
            Func<CoursePoint, CoursePoint> point, int yaw)
        {
            var puzzles = new HashSet<string>(course.Puzzles.Where(p => moduleIds.Contains(p.ModuleId)).Select(p => p.Id), StringComparer.Ordinal);
            foreach (var site in course.PhysicalSites.Where(s => puzzles.Contains(s.PuzzleId)))
            {
                site.Position = point(site.Position); site.Target = point(site.Target); site.Yaw = (site.Yaw + yaw) % 360;
                if (!Empty(site.ArrivalBounds)) site.ArrivalBounds = TransformBox(site.ArrivalBounds, point);
                if (!Empty(site.ReceiverStartBounds)) site.ReceiverStartBounds = TransformBox(site.ReceiverStartBounds, point);
                site.FloorPadBounds = site.FloorPadBounds.Select(b => TransformBox(b, point)).ToList();
                foreach (var fixture in site.Fixtures)
                {
                    fixture.From = point(fixture.From); fixture.To = point(fixture.To);
                    if (yaw % 180 != 0) fixture.SizeMm = new CoursePoint(fixture.SizeMm.Z, fixture.SizeMm.Y, fixture.SizeMm.X);
                }
            }
        }

        internal static void RemoveOwned(CourseDocument course, ISet<string> moduleIds)
        {
            var puzzles = new HashSet<string>(course.Puzzles.Where(p => moduleIds.Contains(p.ModuleId)).Select(p => p.Id), StringComparer.Ordinal);
            course.PhysicalSites.RemoveAll(s => puzzles.Contains(s.PuzzleId));
        }

        internal static void DuplicateOwned(CourseDocument source, CourseDocument target,
            IDictionary<string, string> puzzles, IDictionary<string, string> supplies,
            IDictionary<string, string> zones, IDictionary<string, string> checkpoints)
        {
            string Remap(string id, IDictionary<string, string> map) => map.TryGetValue(id, out var next) ? next : id;
            foreach (var profile in source.PhysicalSites.Where(s => puzzles.ContainsKey(s.PuzzleId)))
            {
                var site = Copy(profile);
                site.PuzzleId = puzzles[site.PuzzleId]; site.CheckpointId = Remap(site.CheckpointId, checkpoints);
                site.RecoverySupplyId = Remap(site.RecoverySupplyId, supplies);
                site.ToolRecoverySupplyId = Remap(site.ToolRecoverySupplyId, supplies);
                site.PropSourceZoneId = Remap(site.PropSourceZoneId, zones);
                site.WindReceiverZoneId = Remap(site.WindReceiverZoneId, zones);
                site.ReceiverRecoverySupplyId = Remap(site.ReceiverRecoverySupplyId, supplies);
                var taken = target.PhysicalSites.SelectMany(s => s.Fixtures).Select(f => f.Id).ToList();
                foreach (var fixture in site.Fixtures)
                { fixture.Id = WorkshopCommands.Unique(fixture.Id, taken); taken.Add(fixture.Id); }
                target.PhysicalSites.Add(site);
            }
            if (target.PhysicalSites.Count != 0) target.Schema = 2;
        }

        public static CoursePoint Centre(CourseBox box) => new CoursePoint(
            checked((int)(((long)box.Min.X + box.Max.X) / 2)), checked((int)(((long)box.Min.Y + box.Max.Y) / 2)),
            checked((int)(((long)box.Min.Z + box.Max.Z) / 2)));
        public static bool Empty(CourseBox box) => box.Min.Equals(default(CoursePoint)) && box.Max.Equals(default(CoursePoint));
        static void RequireVolume(CourseBox box)
        { if (Empty(box)) throw new ArgumentException("This site has no selected sensor volume."); }
        static void RequireEditable(WorkshopDraft draft, string puzzleId)
        {
            if (draft?.Course == null) throw new ArgumentNullException(nameof(draft));
            var puzzle = draft.Course.Puzzles.Single(p => p.Id == puzzleId);
            var module = draft.Course.Modules.Single(m => m.Id == puzzle.ModuleId);
            if (draft.LockedLayers.Contains(module.LayerId)) throw new InvalidOperationException("Unlock the layer first.");
        }
        static CoursePoint Add(CoursePoint a, CoursePoint b) =>
            new CoursePoint(checked(a.X + b.X), checked(a.Y + b.Y), checked(a.Z + b.Z));
        static CourseBox TransformBox(CourseBox box, Func<CoursePoint, CoursePoint> point)
        {
            var a = point(box.Min); var b = point(box.Max);
            return new CourseBox(new CoursePoint(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z)),
                new CoursePoint(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z)));
        }
    }
}
