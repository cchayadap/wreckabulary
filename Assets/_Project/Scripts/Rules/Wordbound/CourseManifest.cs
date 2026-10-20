using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    internal sealed class CoursePlayData
    {
        public CoursePlayData() { }
        public string ModeId = "", Capability = "", Ownership = "";
        public int TeamCount, TeamCapacity, MaxOccupants;
        internal CoursePlayRequirements ToRequirements()
        {
            if (!Enum.TryParse(Capability, out CourseCapability capability) || capability.ToString() != Capability ||
                !Enum.TryParse(Ownership, out CourseOwnershipPolicy ownership) || ownership.ToString() != Ownership)
                throw new FormatException("Unknown course play policy.");
            return new CoursePlayRequirements(ModeId, capability, TeamCount, TeamCapacity, MaxOccupants, ownership);
        }
        internal static CoursePlayData From(CoursePlayRequirements p) => new CoursePlayData
        {
            ModeId = p.ModeId, Capability = p.Capability.ToString(), Ownership = p.Ownership.ToString(),
            TeamCount = p.TeamCount, TeamCapacity = p.TeamCapacity, MaxOccupants = p.MaxOccupants
        };
    }
    internal sealed class CourseLapSelection
    {
        public CourseLapSelection() { }
        public int Number;
        public string StartModuleId = "", FinishModuleId = "", NarrativeId = "";
        public List<string> AlternativeIds = new List<string>();
        public List<string> ConnectionIds = new List<string>();
        public List<string> CheckpointIds = new List<string>();
        public List<int> CheckpointSteps = new List<int>();
    }
    internal sealed class CoursePlacementData
    {
        public CoursePlacementData() { }
        public string Id = "", ZoneId = "", AssetId = "", ModuleId = "", Purpose = "", Letters = "";
        public CoursePoint Position;
        public int Yaw, TeamIndex = -1;
    }
    internal sealed class CourseSupplyData
    {
        public CourseSupplyData() { }
        public string Id = "", SourceId = "", ModuleId = "", Letters = "";
        public CoursePoint Position;
        public int TeamIndex = -1;
        public bool Renewable, Protected;
        public List<string> AfterPuzzleIds = new List<string>();
    }
    internal sealed class CourseManifestData
    {
        public CourseManifestData() { }
        public int Schema = 1, GeneratorVersion = 1, CanonicalVersion = 1, Attempt;
        public string Format = "wreckabulary.resolved-course", RngVersion = CourseRandom.Version;
        public string Seed = "", SourceHash = "", CatalogueHash = "", Hash = "";
        public CoursePlayData Play = new CoursePlayData();
        public CourseDocument Document = new CourseDocument();
        public List<CourseLapSelection> Laps = new List<CourseLapSelection>();
        public List<CoursePlacementData> Placements = new List<CoursePlacementData>();
        public List<CourseSupplyData> Supplies = new List<CourseSupplyData>();
    }
    public sealed class CourseResolution
    {
        public ResolvedCourse Manifest { get; internal set; }
        public int AttemptsUsed { get; internal set; }
        public CourseValidation Validation { get; } = new CourseValidation();
        public bool Success => Manifest != null && Validation.Ok;
        public IReadOnlyList<CourseIssue> Issues => Validation.Issues;
    }
    public sealed class CourseManifestResult
    {
        public ResolvedCourse Manifest { get; internal set; }
        public CourseValidation Validation { get; } = new CourseValidation();
        public bool Success => Manifest != null && Validation.Ok;
        public IReadOnlyList<CourseIssue> Issues => Validation.Issues;
    }
    public sealed class ResolvedCourseModule
    {
        public string Id { get; }
        public string DefinitionId { get; }
        public string LayerId { get; }
        public CoursePoint Position { get; }
        public int Yaw { get; }
        public CourseBox Bounds { get; }
        internal ResolvedCourseModule(CourseModule m, CourseCatalogue catalogue)
        { Id = m.Id; DefinitionId = m.DefinitionId; LayerId = m.LayerId; Position = m.Position; Yaw = m.Yaw; Bounds = catalogue.Bounds(m); }
    }
    public sealed class ResolvedCourseConnection
    {
        public string Id { get; }
        public string FromModuleId { get; }
        public string ToModuleId { get; }
        public string Kind { get; }
        public string GatePuzzleId { get; }
        public CoursePoint From { get; }
        public CoursePoint To { get; }
        public int WidthMm { get; }
        public bool Bidirectional { get; }
        internal ResolvedCourseConnection(CourseConnection c)
        { Id = c.Id; FromModuleId = c.FromModuleId; ToModuleId = c.ToModuleId; Kind = c.Kind; GatePuzzleId = c.GatePuzzleId; From = c.From; To = c.To; WidthMm = c.WidthMm; Bidirectional = c.Bidirectional; }
    }
    public sealed class ResolvedCoursePuzzle
    {
        public string Id { get; }
        public string ModuleId { get; }
        public string PresetId { get; }
        public string RecipeId { get; }
        public string RequiredLetters { get; }
        public string RewardLetters { get; }
        public bool ConsumeLetters { get; }
        public IReadOnlyList<string> PrerequisiteIds { get; }
        public IReadOnlyList<string> RecoverySupplyIds { get; }
        internal ResolvedCoursePuzzle(CoursePuzzle p)
        {
            Id = p.Id; ModuleId = p.ModuleId; PresetId = p.PresetId; RecipeId = p.RecipeId;
            RequiredLetters = p.RequiredLetters; RewardLetters = p.RewardLetters; ConsumeLetters = p.ConsumeLetters;
            PrerequisiteIds = Array.AsReadOnly(p.PrerequisiteIds.ToArray()); RecoverySupplyIds = Array.AsReadOnly(p.RecoverySupplyIds.ToArray());
        }
    }
    public sealed class ResolvedCourseRecovery
    {
        public string Id { get; }
        public string ModuleId { get; }
        public CoursePoint Position { get; }
        public int RadiusMm { get; }
        public IReadOnlyList<string> AfterPuzzleIds { get; }
        internal ResolvedCourseRecovery(CourseRecovery r)
        { Id = r.Id; ModuleId = r.ModuleId; Position = r.Position; RadiusMm = r.RadiusMm; AfterPuzzleIds = Array.AsReadOnly(r.AfterPuzzleIds.ToArray()); }
    }
    public sealed class ResolvedCourseSpawn
    {
        public string Id { get; }
        public string ModuleId { get; }
        public string Kind { get; }
        public CoursePoint Position { get; }
        public int Yaw { get; }
        internal ResolvedCourseSpawn(CourseSpawn s) { Id = s.Id; ModuleId = s.ModuleId; Kind = s.Kind; Position = s.Position; Yaw = s.Yaw; }
    }
    public sealed class ResolvedCourseCheckpoint
    {
        public string Id { get; }
        public string ModuleId { get; }
        public string RecoveryAnchorId { get; }
        public string EntryConnectionId { get; }
        public int RouteStep { get; }
        public IReadOnlyList<string> PuzzleIds { get; }
        internal ResolvedCourseCheckpoint(CourseCheckpoint c, int step)
        {
            Id = c.Id; ModuleId = c.ModuleId; RecoveryAnchorId = c.RecoveryAnchorId;
            EntryConnectionId = c.EntryConnectionId; RouteStep = step; PuzzleIds = Array.AsReadOnly(c.PuzzleIds.ToArray());
        }
    }
    public sealed class ResolvedCourseLap
    {
        public int Number { get; }
        public string StartModuleId { get; }
        public string FinishModuleId { get; }
        public string NarrativeId { get; }
        public IReadOnlyList<string> AlternativeIds { get; }
        public IReadOnlyList<string> ConnectionIds { get; }
        public IReadOnlyList<ResolvedCourseCheckpoint> Checkpoints { get; }
        internal ResolvedCourseLap(CourseLapSelection lap, CourseDocument d)
        {
            Number = lap.Number; StartModuleId = lap.StartModuleId; FinishModuleId = lap.FinishModuleId; NarrativeId = lap.NarrativeId;
            AlternativeIds = Array.AsReadOnly(lap.AlternativeIds.ToArray()); ConnectionIds = Array.AsReadOnly(lap.ConnectionIds.ToArray());
            Checkpoints = Array.AsReadOnly(lap.CheckpointIds.Select((id, i) => new ResolvedCourseCheckpoint(d.Checkpoints.Single(c => c.Id == id), lap.CheckpointSteps[i])).ToArray());
        }
    }
    public sealed class ResolvedCoursePlacement
    {
        public string Id { get; }
        public string ZoneId { get; }
        public string AssetId { get; }
        public string ModuleId { get; }
        public string Purpose { get; }
        public string Letters { get; }
        public CoursePoint Position { get; }
        public int Yaw { get; }
        public int TeamIndex { get; }
        internal ResolvedCoursePlacement(CoursePlacementData p)
        { Id = p.Id; ZoneId = p.ZoneId; AssetId = p.AssetId; ModuleId = p.ModuleId; Purpose = p.Purpose; Letters = p.Letters; Position = p.Position; Yaw = p.Yaw; TeamIndex = p.TeamIndex; }
    }
    public sealed class ResolvedCourseSupply
    {
        public string Id { get; }
        public string SourceId { get; }
        public string ModuleId { get; }
        public string Letters { get; }
        public CoursePoint Position { get; }
        public int TeamIndex { get; }
        public bool Renewable { get; }
        public bool Protected { get; }
        public IReadOnlyList<string> AfterPuzzleIds { get; }
        internal ResolvedCourseSupply(CourseSupplyData s)
        { Id = s.Id; SourceId = s.SourceId; ModuleId = s.ModuleId; Letters = s.Letters; Position = s.Position; TeamIndex = s.TeamIndex; Renewable = s.Renewable; Protected = s.Protected; AfterPuzzleIds = Array.AsReadOnly(s.AfterPuzzleIds.ToArray()); }
    }
    public sealed class ResolvedCourse
    {
        internal CourseManifestData Data { get; }
        public string Hash => Data.Hash;
        public string SourceHash => Data.SourceHash;
        public string CatalogueHash => Data.CatalogueHash;
        public string Seed => Data.Seed;
        public int Attempt => Data.Attempt;
        public int GeneratorVersion => Data.GeneratorVersion;
        public int Schema => Data.Schema;
        public string RngVersion => Data.RngVersion;
        public CoursePlayRequirements Play { get; }
        public IReadOnlyList<ResolvedCourseModule> Geometry { get; }
        public IReadOnlyList<ResolvedCourseConnection> Connections { get; }
        public IReadOnlyList<ResolvedCoursePuzzle> Puzzles { get; }
        public IReadOnlyList<ResolvedCourseLap> Laps { get; }
        public IReadOnlyList<ResolvedCoursePlacement> Placements { get; }
        public IReadOnlyList<ResolvedCourseSupply> Supplies { get; }
        public IReadOnlyList<ResolvedCourseRecovery> RecoveryAnchors { get; }
        public IReadOnlyList<ResolvedCourseSpawn> Spawns { get; }
        public CourseDocument CopyDocument() => Data.Document.Clone();
        internal ResolvedCourse(CourseManifestData data, CourseCatalogue catalogue)
        {
            Data = data; Play = data.Play.ToRequirements();
            Geometry = Array.AsReadOnly(data.Document.Modules.OrderBy(x => x.Id, StringComparer.Ordinal).Select(x => new ResolvedCourseModule(x, catalogue)).ToArray());
            Connections = Array.AsReadOnly(data.Document.Connections.OrderBy(x => x.Id, StringComparer.Ordinal).Select(x => new ResolvedCourseConnection(x)).ToArray());
            Puzzles = Array.AsReadOnly(data.Document.Puzzles.OrderBy(x => x.Id, StringComparer.Ordinal).Select(x => new ResolvedCoursePuzzle(x)).ToArray());
            Laps = Array.AsReadOnly(data.Laps.OrderBy(x => x.Number).Select(x => new ResolvedCourseLap(x, data.Document)).ToArray());
            Placements = Array.AsReadOnly(data.Placements.OrderBy(x => x.Id, StringComparer.Ordinal).Select(x => new ResolvedCoursePlacement(x)).ToArray());
            Supplies = Array.AsReadOnly(data.Supplies.OrderBy(x => x.Id, StringComparer.Ordinal).Select(x => new ResolvedCourseSupply(x)).ToArray());
            RecoveryAnchors = Array.AsReadOnly(data.Document.RecoveryAnchors.OrderBy(x => x.Id, StringComparer.Ordinal).Select(x => new ResolvedCourseRecovery(x)).ToArray());
            Spawns = Array.AsReadOnly(data.Document.Spawns.OrderBy(x => x.Id, StringComparer.Ordinal).Select(x => new ResolvedCourseSpawn(x)).ToArray());
        }
    }
}
