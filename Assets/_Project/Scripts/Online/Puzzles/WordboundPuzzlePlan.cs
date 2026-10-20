using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed class WordboundPlacementSocket
    {
        public string PuzzleId { get; }
        public WorldVector Position { get; }
        public int Yaw { get; }
        public int RadiusMm { get; }
        public int YawToleranceDegrees { get; }
        public int HoldTicks { get; }
        internal WordboundPlacementSocket(string puzzle, WorldVector position, int yaw, int radius, int tolerance, int hold)
        { PuzzleId = puzzle; Position = position; Yaw = yaw; RadiusMm = radius; YawToleranceDegrees = tolerance; HoldTicks = hold; }
    }

    public sealed class WordboundPlatformPath
    {
        public string BodyId { get; }
        public WorldVector From { get; }
        public WorldVector To { get; }
        public int TravelTicks { get; }
        public int DwellTicks { get; }
        internal WordboundPlatformPath(ResolvedCourseConnection connection, int travelTicks, int dwellTicks)
        {
            BodyId = "platform:" + connection.Id;
            From = Metres(connection.From); To = Metres(connection.To);
            TravelTicks = travelTicks; DwellTicks = dwellTicks;
        }
        public WorldVector Position(long tick)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            long phase = tick % (2L * (TravelTicks + DwellTicks));
            double t = phase < DwellTicks ? 0 : phase < DwellTicks + TravelTicks ?
                (phase - DwellTicks) / (double)TravelTicks : phase < 2L * DwellTicks + TravelTicks ? 1 :
                1 - (phase - 2L * DwellTicks - TravelTicks) / (double)TravelTicks;
            return new WorldVector((float)(From.X + (To.X - From.X) * t),
                (float)(From.Y + (To.Y - From.Y) * t), (float)(From.Z + (To.Z - From.Z) * t));
        }
        internal static WorldVector Metres(CoursePoint p) => new WorldVector(p.X / 1000f, p.Y / 1000f, p.Z / 1000f);
    }

    public sealed class WordboundPuzzlePlan
    {
        public const int Version = 2;
        public string SourceHash { get; }
        public string ManifestHash { get; }
        public string Hash { get; }
        public IReadOnlyList<WordboundPlacementSocket> Sockets { get; }
        public IReadOnlyList<WordboundPlatformPath> Platforms { get; }
        internal WordboundPuzzlePlan(ResolvedCourse course)
        {
            SourceHash = course.SourceHash; ManifestHash = course.Hash;
            Sockets = Array.AsReadOnly(new[]
            {
                new WordboundPlacementSocket("BedBridge", new WorldVector(0, 0, 0), 0, 250, 15, 30),
                new WordboundPlacementSocket("FanDoor", new WorldVector(0, 6, 0), 0, 250, 15, 30)
            });
            Platforms = Array.AsReadOnly(course.Connections.Where(c => c.Id == "UpA" || c.Id == "UpB")
                .OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => new WordboundPlatformPath(c, 360, 60)).ToArray());
            Hash = WordboundIdentity.Sha256(string.Join("|", new[] { "wordbound.physical-proof", Version.ToString(CultureInfo.InvariantCulture),
                SourceHash, ManifestHash, "BedBridge:0,0,0:0:250:15:30", "FanDoor:0,6000,0:0:250:15:30",
                "UpA:360:60", "UpB:360:60", "checkpoint:authored-recovery-radius", "jump-pad:catalogue-strength",
                "fields:next-tick-expiry:height1.25:runner-los:minimum-slow:slippery0.1:wind-catalogue-cap" }));
        }
        public void Validate(MatchBinding binding)
        {
            if (binding == null || binding.CourseHash != SourceHash || binding.ManifestHash != ManifestHash)
                throw new ArgumentException("The physical puzzle plan differs from the exact signed course.");
        }
    }

    public sealed class WordboundPuzzleProof
    {
        readonly string itemJson;
        public ModeDefinition Mode { get; }
        public CourseCatalogue Geometry { get; }
        public ResolvedCourse Course { get; }
        public WordboundPuzzlePlan Plan { get; }
        public string ItemSourceHash { get; }
        public string CatalogueHash { get; }
        public ItemCatalogue Items => ItemCatalogue.FromJson(itemJson);

        WordboundPuzzleProof(string json, ModeDefinition mode, string seed)
        {
            if (json == null || json.Length > CourseLimits.DocumentBytes) throw new ArgumentException("Item catalogue exceeds the proof bound.");
            itemJson = json; Mode = mode;
            var items = Items;
            if (items.Validate(64).Count != 0) throw new ArgumentException("The actual item catalogue is invalid.");
            foreach (string id in new[] { "BED", "STOOL" })
            {
                var item = items.Get(id);
                if (!item.Enabled || item.Deploy == null || item.Deploy.Effect != DeployEffect.JumpPad ||
                    item.Deploy.LifetimeSeconds != 0) throw new NotSupportedException("The proof requires permanent catalogue BED/STOOL jump pads.");
            }
            foreach (string id in new[] { "FAN", "CLOCK" })
            {
                var item = items.Get(id);
                if (!item.Enabled || item.Deploy == null || item.Deploy.Effect !=
                    (id == "FAN" ? DeployEffect.WindField : DeployEffect.SlowField))
                    throw new NotSupportedException("The proof requires catalogue FAN wind and CLOCK slow fields.");
                WordboundFieldPolicy.Validate(item.Deploy);
            }
            var baseGeometry = CourseTemplates.ProofCatalogue();
            Geometry = new CourseCatalogue(baseGeometry.Modules.Values,
                new[] { "BED", "STOOL", "PLATE" }.Select(id =>
                {
                    var item = items.Get(id);
                    return new CourseAssetDefinition(id, id, Mm(item.Size[0]), Mm(item.Size[2]), Mm(item.Size[1]));
                }), baseGeometry.PuzzlePresets, baseGeometry.Themes, baseGeometry.NarrativeEvents);
            var document = CourseTemplates.RaceCircuit();
            document.Id = "physical-proof-v2"; document.Title = "Parcel Lift Workshop";
            document.Puzzles.Single(p => p.Id == "BedBridge").PresetId = "PlaceObject";
            var upper = document.Puzzles.Single(p => p.Id == "FanDoor");
            upper.PresetId = "PlaceObject"; upper.RecipeId = "STOOL"; upper.RequiredLetters = "STOOL";
            document.Supplies.Single(s => s.Id == "FanSupply").Letters = "STOOL";
            document.Supplies.Add(new CourseSupply { Id = "FanPractice", ModuleId = "Start",
                Position = new CoursePoint(5000, 0, -6500), Letters = "FAN" });
            document.Supplies.Add(new CourseSupply { Id = "ClockPractice", ModuleId = "Start",
                Position = new CoursePoint(7500, 0, -6500), Letters = "CLOCK" });
            var resolved = CourseResolver.Resolve(document, Geometry, new CourseResolveRequest(seed, MatchStateValidation.Requirements(mode)));
            if (!resolved.Success) throw new ArgumentException("Physical proof course did not resolve: " + string.Join("; ", resolved.Issues));
            Course = resolved.Manifest;
            Plan = new WordboundPuzzlePlan(Course);
            ItemSourceHash = WordboundIdentity.Sha256(json);
            CatalogueHash = WordboundIdentity.Sha256(string.Join("|", "wordbound.proof-content", "2",
                Geometry.Hash, ItemSourceHash, Plan.Hash));
        }
        public static WordboundPuzzleProof Create(string itemCatalogueJson, string modeId = "TimeRun",
            RaceFormat format = RaceFormat.Solo, string seed = "0123456789abcdef")
        {
            var mode = ModeDefinition.For(modeId, format);
            if (!mode.IsRace) throw new NotSupportedException("This built-in physical proof is a three-lap race course.");
            return new WordboundPuzzleProof(itemCatalogueJson, mode, seed);
        }
        public void Validate(MatchBinding binding)
        {
            Plan.Validate(binding);
            if (binding.ModeHash != Mode.Hash || binding.CatalogueHash != CatalogueHash)
                throw new ArgumentException("The physical proof content bundle differs from signed admission.");
        }
        static int Mm(float value) => checked((int)Math.Ceiling(value * 1000d));
    }
}
