using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class CoursePhysicalSite
    {
        public int Lap = 1;
        public string PuzzleId = "", CheckpointId = "", RecoverySupplyId = "";
        public string ToolRecoverySupplyId = "", ToolRecipeId = "", PropSourceZoneId = "";
        public string WindReceiverZoneId = "", WindReceiverRecipeId = "", ReceiverRecoverySupplyId = "";
        public CoursePoint Position, Target;
        public CourseBox ArrivalBounds, ReceiverStartBounds;
        public List<CourseBox> FloorPadBounds = new List<CourseBox>();
        public List<CourseMovingFixture> Fixtures = new List<CourseMovingFixture>();
        public int Yaw, SocketRadiusMm = 250, YawTolerance = 15, HoldTicks = 30, ActiveWindowTicks;
    }

    public sealed class CourseMovingFixture
    {
        public string Id = "";
        public CoursePoint From, To, SizeMm;
        public int PeriodTicks = 240, PhaseTicks;
    }

    public sealed class ResolvedCourseMovingFixture
    {
        public string Id { get; }
        public CoursePoint From { get; }
        public CoursePoint To { get; }
        public CoursePoint SizeMm { get; }
        public int PeriodTicks { get; }
        public int PhaseTicks { get; }
        internal ResolvedCourseMovingFixture(CourseMovingFixture value)
        {
            Id = value.Id; From = value.From; To = value.To; SizeMm = value.SizeMm;
            PeriodTicks = value.PeriodTicks; PhaseTicks = value.PhaseTicks;
        }
    }

    public sealed class ResolvedCoursePhysicalSite
    {
        public int Lap { get; }
        public string PuzzleId { get; }
        public string CheckpointId { get; }
        public string RecoverySupplyId { get; }
        public string ToolRecoverySupplyId { get; }
        public string ToolRecipeId { get; }
        public string PropSourceZoneId { get; }
        public string WindReceiverZoneId { get; }
        public string WindReceiverRecipeId { get; }
        public string ReceiverRecoverySupplyId { get; }
        public CoursePoint Position { get; }
        public CoursePoint Target { get; }
        public CourseBox ArrivalBounds { get; }
        public CourseBox ReceiverStartBounds { get; }
        public IReadOnlyList<CourseBox> FloorPadBounds { get; }
        public IReadOnlyList<ResolvedCourseMovingFixture> Fixtures { get; }
        public int Yaw { get; }
        public int SocketRadiusMm { get; }
        public int YawTolerance { get; }
        public int HoldTicks { get; }
        public int ActiveWindowTicks { get; }
        public string Hash { get; }
        internal ResolvedCoursePhysicalSite(CoursePhysicalSite value)
        {
            Lap = value.Lap; PuzzleId = value.PuzzleId; CheckpointId = value.CheckpointId;
            RecoverySupplyId = value.RecoverySupplyId; ToolRecoverySupplyId = value.ToolRecoverySupplyId;
            ToolRecipeId = value.ToolRecipeId; PropSourceZoneId = value.PropSourceZoneId;
            WindReceiverZoneId = value.WindReceiverZoneId; WindReceiverRecipeId = value.WindReceiverRecipeId;
            ReceiverRecoverySupplyId = value.ReceiverRecoverySupplyId;
            Position = value.Position; Target = value.Target; ArrivalBounds = value.ArrivalBounds;
            ReceiverStartBounds = value.ReceiverStartBounds;
            FloorPadBounds = Array.AsReadOnly(value.FloorPadBounds.ToArray());
            Fixtures = Array.AsReadOnly(value.Fixtures.OrderBy(f => f.Id, StringComparer.Ordinal)
                .Select(f => new ResolvedCourseMovingFixture(f)).ToArray());
            Yaw = value.Yaw; SocketRadiusMm = value.SocketRadiusMm; YawTolerance = value.YawTolerance;
            HoldTicks = value.HoldTicks; ActiveWindowTicks = value.ActiveWindowTicks;
            Hash = CourseJson.Sha256(CourseJson.Encode(CourseJson.Value(value)));
        }
    }

    public static partial class CourseValidator
    {
        static readonly HashSet<string> SiteRecipes = new HashSet<string>(ModeDefinition.For("TimeRun").Recipes, StringComparer.Ordinal);
        internal static bool EmptyBox(CourseBox box) => box.Min.Equals(default(CoursePoint)) && box.Max.Equals(default(CoursePoint));
        public static bool SiteRecipeAllowed(string recipe) => recipe != null && SiteRecipes.Contains(recipe);
        static bool OptionalSiteRecipe(string recipe) => recipe == "" || SiteRecipeAllowed(recipe);

        static void ValidatePhysicalSiteShapes(CourseDocument d, CourseValidation v)
        {
            if (d.PhysicalSites.Count > 768 ||
                d.PhysicalSites.Select(s => s.Lap + "/" + s.PuzzleId).Distinct(StringComparer.Ordinal).Count() != d.PhysicalSites.Count)
                v.Add("physical.count", "$.physicalSites", "", "At most 768 distinct lap/puzzle profiles are supported.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var site in d.PhysicalSites)
            {
                if (site.Lap < 1 || site.Lap > 3 || !ValidId(site.PuzzleId) || !ValidId(site.CheckpointId) ||
                    !ValidId(site.RecoverySupplyId) || !Yaw(site.Yaw) || site.SocketRadiusMm < 50 || site.SocketRadiusMm > 1000 ||
                    site.YawTolerance < 0 || site.YawTolerance > 45 || site.HoldTicks < 1 || site.HoldTicks > 600 ||
                    site.ActiveWindowTicks < 0 || site.ActiveWindowTicks > 600 || site.FloorPadBounds.Count > 2 || site.Fixtures.Count > 4 ||
                    new[] { site.ToolRecipeId, site.ToolRecoverySupplyId, site.PropSourceZoneId, site.WindReceiverZoneId,
                        site.WindReceiverRecipeId, site.ReceiverRecoverySupplyId }.Any(id => id != "" && !ValidId(id)))
                    v.Add("physical.shape", "$.physicalSites", site.PuzzleId, "Invalid physical profile identity, timing or sensor count.");
                foreach (var fixture in site.Fixtures)
                    if (!ValidId(fixture.Id) || !ids.Add(fixture.Id) || fixture.From.Equals(fixture.To) ||
                        fixture.SizeMm.X < 100 || fixture.SizeMm.X > 10000 || fixture.SizeMm.Y < 100 || fixture.SizeMm.Y > 3000 ||
                        fixture.SizeMm.Z < 100 || fixture.SizeMm.Z > 10000 || fixture.PeriodTicks < 60 || fixture.PeriodTicks > 3600 ||
                        fixture.PhaseTicks < 0 || fixture.PhaseTicks >= fixture.PeriodTicks)
                        v.Add("physical.fixture", "$.physicalSites", site.PuzzleId, "Invalid bounded moving fixture.");
            }
        }

        static void ValidatePhysicalSites(CourseDocument d, CourseCatalogue catalogue, CourseValidation v)
        {
            if (d.PhysicalSites.Count > 768 ||
                d.PhysicalSites.Select(s => s.Lap + "/" + s.PuzzleId).Distinct(StringComparer.Ordinal).Count() != d.PhysicalSites.Count)
            { v.Add("physical.count", "$.physicalSites", "", "At most 768 distinct lap/puzzle profiles are supported."); return; }
            var fixtureIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var site in d.PhysicalSites)
            {
                void Error(string message) => v.Add("physical.invalid", "$.physicalSites", site.PuzzleId, message);
                if (!OptionalSiteRecipe(site.ToolRecipeId) || !OptionalSiteRecipe(site.WindReceiverRecipeId))
                    v.Add("physical.recipe", "$.physicalSites", site.PuzzleId, "Choose craftable Wordbound recipes for this site's tool and receiver.");
                var puzzle = d.Puzzles.SingleOrDefault(p => p.Id == site.PuzzleId);
                var checkpoint = d.Checkpoints.SingleOrDefault(c => c.Id == site.CheckpointId);
                var lap = d.Laps.SingleOrDefault(l => l.Number == site.Lap);
                if (puzzle == null || checkpoint == null || lap == null || checkpoint.ModuleId != puzzle.ModuleId ||
                    !checkpoint.PuzzleIds.Contains(site.PuzzleId) || !lap.SectionIds.SelectMany(id =>
                        d.RouteSections.Where(s => s.Id == id).SelectMany(s => s.Alternatives)).Any(a => a.CheckpointIds.Contains(site.CheckpointId)))
                { Error("The site must refer to a puzzle checkpoint on its authored lap."); continue; }
                var module = d.Modules.SingleOrDefault(m => m.Id == puzzle.ModuleId);
                if (module == null) { Error("Unknown module."); continue; }
                bool Supply(string id, string letters = "") => ValidId(id) && puzzle.RecoverySupplyIds.Contains(id) &&
                    d.Supplies.Any(s => s.Id == id && s.ModuleId == puzzle.ModuleId && s.Renewable && s.Protected &&
                        (letters == "" || s.Letters == letters));
                bool Prop(string id) => ValidId(id) && d.Zones.Any(z => z.Id == id && z.ModuleId == puzzle.ModuleId &&
                    z.Purpose == "Props" && z.MinCount >= 1 && z.ChancePermille == 1000);
                bool Sensor(CourseBox box) => ValidBox(box) && box.Min.Y < box.Max.Y &&
                    (catalogue == null || catalogue.Bounds(module).Contains(box.Min) && catalogue.Bounds(module).Contains(box.Max));
                if (!Supply(site.RecoverySupplyId) || !ValidPoint(site.Position) || site.Position.Y != module.Position.Y ||
                    !ValidPoint(site.Target) || !Yaw(site.Yaw) || site.SocketRadiusMm < 50 || site.SocketRadiusMm > 1000 ||
                    site.YawTolerance < 0 || site.YawTolerance > 45 || site.HoldTicks < 1 || site.HoldTicks > 600 ||
                    site.ActiveWindowTicks < 0 || site.ActiveWindowTicks > 600 || site.FloorPadBounds.Count > 2 || site.Fixtures.Count > 4 ||
                    catalogue != null && !catalogue.Bounds(module).Contains(site.Position, site.SocketRadiusMm))
                    Error("The socket, recovery supply or bounded timing is invalid.");
                if (new[] { site.ToolRecoverySupplyId, site.ToolRecipeId, site.PropSourceZoneId,
                        site.WindReceiverZoneId, site.WindReceiverRecipeId, site.ReceiverRecoverySupplyId }
                    .Any(id => id != "" && !ValidId(id))) Error("Invalid optional stable ID.");
                if (site.ToolRecoverySupplyId != "" || site.ToolRecipeId != "")
                    if (!ValidLetters(site.ToolRecipeId, false) || !Supply(site.ToolRecoverySupplyId, site.ToolRecipeId))
                        Error("Tool letters require a protected renewable supply for the specified recipe.");
                switch (puzzle.PresetId)
                {
                    case "CollectLetters":
                        if (!Prop(site.PropSourceZoneId)) Error("Smash requires an authored breakable prop source.");
                        break;
                    case "PlaceObject": break;
                    case "BuildRoute":
                        if (!Sensor(site.ArrivalBounds)) Error("Traversal requires a positive arrival volume in the module.");
                        break;
                    case "RedirectWind":
                        if (!Sensor(site.ArrivalBounds) || !Sensor(site.ReceiverStartBounds) || !Prop(site.WindReceiverZoneId) ||
                            !ValidLetters(site.WindReceiverRecipeId, false) || !Supply(site.ReceiverRecoverySupplyId, site.WindReceiverRecipeId))
                            Error("Wind requires an authored receiver, start/arrival volumes and replacement letters.");
                        break;
                    case "BalanceWeight":
                        if (site.FloorPadBounds.Count != 2 || site.FloorPadBounds.Any(b => !Sensor(b)) ||
                            site.FloorPadBounds[0].Intersects(site.FloorPadBounds[1]))
                            Error("Balance requires two distinct positive switch volumes.");
                        break;
                    case "TimedMechanism":
                        if (!Sensor(site.ArrivalBounds) || site.ActiveWindowTicks < 1 || site.Fixtures.Count == 0)
                            Error("Timed transport requires an arrival, crossing window and actual moving fixtures.");
                        break;
                    default: Error("Unsupported physical puzzle family."); break;
                }
                if (puzzle.PresetId != "CollectLetters" && (site.PropSourceZoneId != "" || site.ToolRecipeId != "" || site.ToolRecoverySupplyId != "") ||
                    puzzle.PresetId != "RedirectWind" && (site.WindReceiverZoneId != "" || site.WindReceiverRecipeId != "" || site.ReceiverRecoverySupplyId != "") ||
                    puzzle.PresetId != "BalanceWeight" && site.FloorPadBounds.Count != 0 ||
                    puzzle.PresetId != "TimedMechanism" && (site.ActiveWindowTicks != 0 || site.Fixtures.Count != 0))
                    Error("Profile contains unrelated family fields.");
                foreach (var fixture in site.Fixtures)
                {
                    bool Fits(CoursePoint at)
                    {
                        if (catalogue == null) return true;
                        var bounds = catalogue.Bounds(module);
                        return bounds.Contains(new CoursePoint(at.X - fixture.SizeMm.X / 2, at.Y, at.Z - fixture.SizeMm.Z / 2)) &&
                            bounds.Contains(new CoursePoint(at.X + (fixture.SizeMm.X + 1) / 2, at.Y + fixture.SizeMm.Y, at.Z + (fixture.SizeMm.Z + 1) / 2));
                    }
                    if (!ValidId(fixture.Id) || !fixtureIds.Add(fixture.Id) || fixture.From.Equals(fixture.To) ||
                        !ValidPoint(fixture.From) || !ValidPoint(fixture.To) || fixture.SizeMm.X < 100 || fixture.SizeMm.X > 10000 ||
                        fixture.SizeMm.Y < 100 || fixture.SizeMm.Y > 3000 || fixture.SizeMm.Z < 100 || fixture.SizeMm.Z > 10000 ||
                        fixture.PeriodTicks < 60 || fixture.PeriodTicks > 3600 || fixture.PhaseTicks < 0 ||
                        fixture.PhaseTicks >= fixture.PeriodTicks || !Fits(fixture.From) || !Fits(fixture.To))
                        Error("Moving fixtures need unique IDs, bounded dimensions and a contained periodic path.");
                }
            }
        }
    }
}
