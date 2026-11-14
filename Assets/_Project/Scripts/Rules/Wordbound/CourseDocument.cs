using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules.Wordbound
{
    public enum CourseCapability { Circuit, Arena, Explore }
    public enum CourseOwnershipPolicy { Shared, TeamIsolated, TeamProtected, Individual }

    public readonly struct CoursePoint : IEquatable<CoursePoint>
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }
        public CoursePoint(int x, int y, int z) { X = x; Y = y; Z = z; }
        public bool Equals(CoursePoint other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object other) => other is CoursePoint point && Equals(point);
        public override int GetHashCode() => unchecked((X * 397 ^ Y) * 397 ^ Z);
    }

    public readonly struct CourseBox
    {
        public CoursePoint Min { get; }
        public CoursePoint Max { get; }
        public CourseBox(CoursePoint min, CoursePoint max) { Min = min; Max = max; }
        public bool Contains(CoursePoint p, int margin = 0) =>
            p.X >= (long)Min.X + margin && p.X <= (long)Max.X - margin &&
            p.Y >= Min.Y && p.Y <= Max.Y &&
            p.Z >= (long)Min.Z + margin && p.Z <= (long)Max.Z - margin;
        public bool Intersects(CourseBox other) =>
            Min.X < other.Max.X && Max.X > other.Min.X &&
            Min.Y < other.Max.Y && Max.Y > other.Min.Y &&
            Min.Z < other.Max.Z && Max.Z > other.Min.Z;
    }

    public sealed class CourseDocument
    {
        public const string FormatId = "wreckabulary.course";
        public int Schema = 1;
        public string Id = "";
        public string Title = "";
        public string ThemeId = "Cozy";
        public List<string> SupportedModes = new List<string>();
        public List<CourseLayer> Layers = new List<CourseLayer>();
        public List<CourseModule> Modules = new List<CourseModule>();
        public List<CourseConnection> Connections = new List<CourseConnection>();
        public List<CoursePuzzle> Puzzles = new List<CoursePuzzle>();
        public List<CourseSupply> Supplies = new List<CourseSupply>();
        public List<CourseSpawn> Spawns = new List<CourseSpawn>();
        public List<CourseRecovery> RecoveryAnchors = new List<CourseRecovery>();
        public List<CourseCheckpoint> Checkpoints = new List<CourseCheckpoint>();
        public List<CourseRouteSection> RouteSections = new List<CourseRouteSection>();
        public List<CourseLap> Laps = new List<CourseLap>();
        public List<CourseZone> Zones = new List<CourseZone>();
        public List<CourseReservedVolume> ReservedVolumes = new List<CourseReservedVolume>();
        public List<CoursePhysicalSite> PhysicalSites = new List<CoursePhysicalSite>();
        public CourseDocument Clone() => CourseJson.ImportDocument(CourseJson.ExportDocument(this)).Document;
    }

    public sealed class CourseLayer { public string Id = ""; public int HeightMm; }
    public sealed class CourseModule
    {
        public string Id = "", DefinitionId = "", LayerId = "";
        public CoursePoint Position;
        public int Yaw;
    }
    public sealed class CourseConnection
    {
        public string Id = "", FromModuleId = "", ToModuleId = "", Kind = "Walk", GatePuzzleId = "";
        public CoursePoint From, To;
        public int WidthMm = 1500;
        public bool Bidirectional = true;
    }
    public sealed class CoursePuzzle
    {
        public string Id = "", ModuleId = "", PresetId = "", RecipeId = "";
        public string RequiredLetters = "", RewardLetters = "";
        public bool ConsumeLetters = true;
        public int MinimumPlayers = 1;
        public List<string> PrerequisiteIds = new List<string>();
        public List<string> RecoverySupplyIds = new List<string>();
    }
    public sealed class CourseSupply
    {
        public string Id = "", ModuleId = "", Letters = "";
        public CoursePoint Position;
        public bool Renewable = true;
        public bool Protected = true;
        public List<string> AfterPuzzleIds = new List<string>();
    }
    public sealed class CourseSpawn
    {
        public string Id = "", ModuleId = "", Kind = "Start";
        public CoursePoint Position;
        public int Yaw;
    }
    public sealed class CourseRecovery
    {
        public string Id = "", ModuleId = "";
        public CoursePoint Position;
        public int RadiusMm = 1000;
        public List<string> AfterPuzzleIds = new List<string>();
    }
    public sealed class CourseCheckpoint
    {
        public string Id = "", ModuleId = "", RecoveryAnchorId = "", EntryConnectionId = "";
        public List<string> PuzzleIds = new List<string>();
    }
    public sealed class CourseAlternative
    {
        public string Id = "";
        public int Weight = 1;
        public List<string> ConnectionIds = new List<string>();
        public List<string> CheckpointIds = new List<string>();
    }
    public sealed class CourseRouteSection
    {
        public string Id = "", EntryModuleId = "", ExitModuleId = "";
        public List<CourseAlternative> Alternatives = new List<CourseAlternative>();
    }
    public sealed class CourseLap
    {
        public int Number;
        public string StartModuleId = "", FinishModuleId = "", NarrativeId = "";
        public List<string> SectionIds = new List<string>();
    }
    public sealed class CourseWeightedAsset { public string AssetId = ""; public int Weight = 1; }
    public sealed class CourseZone
    {
        public string Id = "", ModuleId = "", Purpose = "Props", Placement = "Random";
        public CoursePoint FixedPosition;
        public int FixedYaw;
        public CourseBox Bounds;
        public int MinCount, MaxCount, ChancePermille = 1000, SpacingMm = 500, GridMm = 500;
        public List<int> Yaws = new List<int> { 0, 90, 180, 270 };
        public List<string> LayerIds = new List<string>();
        public List<CourseWeightedAsset> Pool = new List<CourseWeightedAsset>();
    }
    public sealed class CourseReservedVolume
    {
        public string Id = "", Reason = "Path";
        public CourseBox Bounds;
    }

    public sealed class CoursePlayRequirements
    {
        public string ModeId { get; }
        public CourseCapability Capability { get; }
        public int TeamCount { get; }
        public int TeamCapacity { get; }
        public int MaxOccupants { get; }
        public CourseOwnershipPolicy Ownership { get; }
        public CoursePlayRequirements(string modeId, CourseCapability capability, int teamCount,
            int teamCapacity, int maxOccupants, CourseOwnershipPolicy ownership)
        {
            ModeId = modeId; Capability = capability; TeamCount = teamCount;
            TeamCapacity = teamCapacity; MaxOccupants = maxOccupants; Ownership = ownership;
        }
    }
    public sealed class CourseResolveRequest
    {
        public string Seed { get; }
        public CoursePlayRequirements Play { get; }
        public int MaxAttempts { get; }
        public CourseResolveRequest(string seed, CoursePlayRequirements play, int maxAttempts = 16)
        { Seed = seed; Play = play; MaxAttempts = maxAttempts; }
    }

    public static class CourseLimits
    {
        public const int DocumentBytes = 2097152, ManifestBytes = 8388608, JsonDepth = 24;
        public const int Layers = 16, Modules = 256, Connections = 1024, Puzzles = 1024;
        public const int Supplies = 1024, Zones = 256, Checkpoints = 256, Sections = 128;
        public const int Alternatives = 16, Entities = 8192, ListEntries = 4096, Attempts = 16;
        public const int PlacementSamples = 64, HorizontalMm = 2000000, VerticalMm = 256000;
    }
}
