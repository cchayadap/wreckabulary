using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class CourseIssue
    {
        public string Code { get; }
        public string Path { get; }
        public string EntityId { get; }
        public string Message { get; }
        public CourseIssue(string code, string path, string entityId, string message)
        { Code = code; Path = path; EntityId = entityId; Message = message; }
        public override string ToString() => Code + " " + Path + ": " + Message;
    }
    public sealed class CourseValidation
    {
        readonly List<CourseIssue> issues = new List<CourseIssue>();
        public bool Ok => issues.Count == 0;
        public IReadOnlyList<CourseIssue> Issues => issues.AsReadOnly();
        internal void Add(string code, string path, string id, string message)
        { if (issues.Count < 256) issues.Add(new CourseIssue(code, path, id, message)); }
        internal void AddRange(IEnumerable<CourseIssue> errors) { foreach (var error in errors) if (issues.Count < 256) issues.Add(error); }
    }
    public sealed class CourseImportResult
    {
        public CourseDocument Document { get; internal set; }
        public CourseValidation Validation { get; } = new CourseValidation();
        public bool Success => Document != null && Validation.Ok;
        public IReadOnlyList<CourseIssue> Issues => Validation.Issues;
    }

    public static partial class CourseValidator
    {
        static readonly Regex IdPattern = new Regex(@"\A[A-Za-z0-9_-]{1,48}\z");
        internal static readonly string[] ModeIds = { "TimeRun", "TeamRace", "BrawlRace", "FreeForAll", "Freestyle" };
        public static bool ValidId(string id) => id != null && IdPattern.IsMatch(id);
        public static bool ValidLetters(string letters, bool allowEmpty = true) => letters != null && letters.Length <= 128 &&
            (allowEmpty || letters.Length > 0) && letters.All(c => c >= 'A' && c <= 'Z');
        internal static bool ValidPoint(CoursePoint p) => Math.Abs((long)p.X) <= CourseLimits.HorizontalMm &&
            Math.Abs((long)p.Z) <= CourseLimits.HorizontalMm && Math.Abs((long)p.Y) <= CourseLimits.VerticalMm;
        internal static bool ValidBox(CourseBox b) => ValidPoint(b.Min) && ValidPoint(b.Max) &&
            b.Min.X < b.Max.X && b.Min.Z < b.Max.Z && b.Min.Y <= b.Max.Y;
        internal static bool Yaw(int value) => value == 0 || value == 90 || value == 180 || value == 270;

        public static CourseValidation Validate(CourseDocument document, CourseCatalogue catalogue, CoursePlayRequirements play)
        {
            var v = ValidateStructure(document);
            ValidatePlay(play, v);
            if (catalogue == null) v.Add("catalogue", "$", "", "An approved catalogue is required.");
            if (!v.Ok) return v;
            if (!document.SupportedModes.Contains(play.ModeId)) v.Add("mode.unsupported", "$.supportedModes", document.Id, "This course does not support the selected mode.");
            if (!catalogue.Themes.Contains(document.ThemeId)) v.Add("theme.unknown", "$.themeId", document.Id, "Choose an approved theme.");
            ValidateGeometry(document, catalogue, play, v);
            if (v.Ok) ValidateProgression(document, catalogue, play, v);
            return v;
        }

        public static CourseValidation ValidateStructure(CourseDocument d)
        {
            var v = new CourseValidation();
            if (d == null) { v.Add("document.null", "$", "", "Provide a course document."); return v; }
            ValidateFields(d, "$", v, 0);
            if (!v.Ok) return v;
            if (d.Schema != 1) v.Add("schema.unsupported", "$.schema", d.Id, "Only course schema 1 is supported.");
            if (!ValidId(d.Id)) v.Add("id.invalid", "$.id", "", "Use 1–48 ASCII letters, digits, underscores or hyphens.");
            if (d.Title == null || d.Title.Length < 1 || d.Title.Length > 80 || d.Title != d.Title.Trim() || d.Title.Any(char.IsControl))
                v.Add("title.invalid", "$.title", d.Id, "Use a trimmed title of 1–80 characters.");
            if (d.SupportedModes == null || d.SupportedModes.Count == 0 || d.SupportedModes.Count > 5 ||
                d.SupportedModes.Any(m => !ModeIds.Contains(m)) || d.SupportedModes.Distinct().Count() != d.SupportedModes.Count)
                v.Add("modes.invalid", "$.supportedModes", d.Id, "Choose distinct supported mode IDs.");
            Check(d.Layers, CourseLimits.Layers, x => x.Id, "layers", v);
            Check(d.Modules, CourseLimits.Modules, x => x.Id, "modules", v);
            Check(d.Connections, CourseLimits.Connections, x => x.Id, "connections", v);
            Check(d.Puzzles, CourseLimits.Puzzles, x => x.Id, "puzzles", v);
            Check(d.Supplies, CourseLimits.Supplies, x => x.Id, "supplies", v);
            Check(d.Spawns, 64, x => x.Id, "spawns", v);
            Check(d.RecoveryAnchors, CourseLimits.Checkpoints, x => x.Id, "recoveryAnchors", v);
            Check(d.Checkpoints, CourseLimits.Checkpoints, x => x.Id, "checkpoints", v);
            Check(d.RouteSections, CourseLimits.Sections, x => x.Id, "routeSections", v);
            Check(d.Zones, CourseLimits.Zones, x => x.Id, "zones", v);
            Check(d.ReservedVolumes, 1024, x => x.Id, "reservedVolumes", v);
            if (d.Laps == null || d.Laps.Count > 3 || d.Laps.Any(x => x == null) ||
                d.Laps.Select(x => x.Number).Distinct().Count() != d.Laps.Count)
                v.Add("laps.invalid", "$.laps", d.Id, "Provide at most three distinct lap definitions.");
            if (!v.Ok) return v;
            foreach (var puzzle in d.Puzzles)
            {
                IdList(puzzle.PrerequisiteIds, "puzzles", puzzle.Id, v);
                IdList(puzzle.RecoverySupplyIds, "puzzles", puzzle.Id, v);
                if (!ValidLetters(puzzle.RequiredLetters) || !ValidLetters(puzzle.RewardLetters))
                    v.Add("letters.invalid", "$.puzzles", puzzle.Id, "Use at most 128 A-Z letters.");
                if (puzzle.MinimumPlayers != 1) v.Add("solo.required", "$.puzzles", puzzle.Id, "Every puzzle needs a one-player solution.");
            }
            foreach (var supply in d.Supplies)
            {
                IdList(supply.AfterPuzzleIds, "supplies", supply.Id, v);
                if (!ValidLetters(supply.Letters, false)) v.Add("letters.invalid", "$.supplies", supply.Id, "Supply 1–128 A-Z letters.");
            }
            foreach (var recovery in d.RecoveryAnchors) IdList(recovery.AfterPuzzleIds, "recoveryAnchors", recovery.Id, v);
            foreach (var checkpoint in d.Checkpoints) IdList(checkpoint.PuzzleIds, "checkpoints", checkpoint.Id, v);
            foreach (var lap in d.Laps) IdList(lap.SectionIds, "laps", lap.Number.ToString(), v);
            var alternativeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var section in d.RouteSections)
            {
                Check(section.Alternatives, CourseLimits.Alternatives, x => x.Id, "alternatives", v);
                if (section.Alternatives == null) continue;
                if (section.Alternatives.Count == 0) v.Add("route.empty", "$.routeSections", section.Id, "A section needs an alternative.");
                foreach (var alternative in section.Alternatives.Where(x => x != null))
                {
                    if (!alternativeIds.Add(alternative.Id)) v.Add("id.duplicate", "$.routeSections", alternative.Id, "Alternative IDs must be unique across sections.");
                    IdList(alternative.ConnectionIds, "alternatives", alternative.Id, v);
                    IdList(alternative.CheckpointIds, "alternatives", alternative.Id, v);
                    if (alternative.Weight < 1 || alternative.Weight > 10000) v.Add("weight.invalid", "$.routeSections", alternative.Id, "Weight must be 1–10000.");
                }
            }
            foreach (var zone in d.Zones)
            {
                IdList(zone.LayerIds, "zones", zone.Id, v);
                Check(zone.Pool, 128, x => x.AssetId, "pool", v);
                if (zone.Pool == null) continue;
                if (zone.Pool.Count == 0 || zone.Pool.Any(x => x != null && (x.Weight < 1 || x.Weight > 10000)))
                    v.Add("pool.invalid", "$.zones", zone.Id, "Use a nonempty weighted asset pool.");
                if (zone.Yaws == null || zone.Yaws.Count == 0 || zone.Yaws.Count > 4 || zone.Yaws.Any(x => !Yaw(x)) ||
                    zone.Yaws.Distinct().Count() != zone.Yaws.Count) v.Add("yaw.invalid", "$.zones", zone.Id, "Choose distinct quarter-turn rotations.");
                if (zone.MinCount < 0 || zone.MaxCount < zone.MinCount || zone.MaxCount > 256 ||
                    zone.ChancePermille < 0 || zone.ChancePermille > 1000 || (zone.MinCount > 0 && zone.ChancePermille != 1000) ||
                    zone.SpacingMm < 0 || zone.SpacingMm > 10000 || zone.GridMm < 100 || zone.GridMm > 5000)
                    v.Add("zone.range", "$.zones", zone.Id, "Invalid count, probability, spacing or grid range. A required minimum cannot be probabilistic.");
                if (!new[] { "Props", "Resources", "Decoration" }.Contains(zone.Purpose))
                    v.Add("zone.purpose", "$.zones", zone.Id, "Choose Props, Resources or Decoration.");
                if (zone.Placement != "Random" && zone.Placement != "Fixed")
                    v.Add("zone.placement", "$.zones", zone.Id, "Choose Random or Fixed.");
                if (zone.Placement == "Fixed" && (zone.MinCount != 1 || zone.MaxCount != 1 || zone.ChancePermille != 1000 ||
                    zone.Pool.Count != 1 || zone.Yaws == null || zone.Yaws.Count != 1 || zone.Yaws[0] != zone.FixedYaw ||
                    !Yaw(zone.FixedYaw) || !zone.Bounds.Contains(zone.FixedPosition)))
                    v.Add("zone.fixed", "$.zones", zone.Id, "A fixed placement needs one asset, count one, certainty and one matching rotation inside its bounds.");
            }
            return v;
        }
        static void ValidateFields(object value, string path, CourseValidation v, int depth)
        {
            if (depth > CourseLimits.JsonDepth) { v.Add("data.depth", path, "", "Data nesting limit exceeded."); return; }
            if (value == null) { v.Add("data.null", path, "", "Null values are not allowed."); return; }
            if (value is string text)
            {
                if (text.Length > 4096) v.Add("data.string", path, "", "String exceeds 4096 characters.");
                try { CourseJson.ValidateText(text); } catch (FormatException e) { v.Add("data.unicode", path, "", e.Message); }
                return;
            }
            if (value is int || value is bool) return;
            if (value is CoursePoint point) { if (!ValidPoint(point)) v.Add("data.point", path, "", "Coordinate exceeds bounds."); return; }
            if (value is CourseBox box) { if (!ValidBox(box)) v.Add("data.box", path, "", "Invalid volume bounds."); return; }
            if (value is System.Collections.IList list)
            {
                if (list.Count > CourseLimits.Entities) { v.Add("data.count", path, "", "Array exceeds bounds."); return; }
                for (int i = 0; i < list.Count; i++) ValidateFields(list[i], path + "[" + i + "]", v, depth + 1);
                return;
            }
            foreach (var field in value.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                ValidateFields(field.GetValue(value), path + "." + field.Name, v, depth + 1);
        }
        static void Check<T>(List<T> list, int maximum, Func<T, string> id, string path, CourseValidation v) where T : class
        {
            if (list == null || list.Count > maximum) { v.Add("count.invalid", "$." + path, "", "Expected an array of at most " + maximum + " entries."); return; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in list)
            {
                if (item == null) { v.Add("item.null", "$." + path, "", "Null entries are not allowed."); continue; }
                string value = id(item);
                if (!ValidId(value)) v.Add("id.invalid", "$." + path, value, "Invalid stable ID.");
                else if (!ids.Add(value)) v.Add("id.duplicate", "$." + path, value, "IDs must be unique within their collection.");
            }
        }
        static void IdList(List<string> list, string path, string id, CourseValidation v)
        {
            if (list == null || list.Count > CourseLimits.ListEntries || list.Any(x => !ValidId(x)) ||
                list.Distinct(StringComparer.Ordinal).Count() != list.Count)
                v.Add("references.invalid", "$." + path, id, "Use a bounded array of distinct valid IDs.");
        }
        internal static void ValidatePlay(CoursePlayRequirements p, CourseValidation v)
        {
            bool valid = p != null && ModeIds.Contains(p.ModeId) && Enum.IsDefined(typeof(CourseCapability), p.Capability) &&
                Enum.IsDefined(typeof(CourseOwnershipPolicy), p.Ownership) && p.MaxOccupants >= 1 && p.MaxOccupants <= 8;
            if (valid)
                switch (p.ModeId)
                {
                    case "TimeRun": valid = p.Capability == CourseCapability.Circuit && p.TeamCount == 1 && p.TeamCapacity == 4 &&
                        p.MaxOccupants == 4 && p.Ownership == CourseOwnershipPolicy.TeamIsolated; break;
                    case "TeamRace":
                    case "BrawlRace": valid = p.Capability == CourseCapability.Circuit && p.TeamCount == 2 &&
                        new[] { 1, 2, 4 }.Contains(p.TeamCapacity) && p.MaxOccupants == p.TeamCount * p.TeamCapacity &&
                        p.Ownership == (p.ModeId == "TeamRace" ? CourseOwnershipPolicy.TeamIsolated : CourseOwnershipPolicy.TeamProtected); break;
                    case "FreeForAll": valid = p.Capability == CourseCapability.Arena && p.TeamCount == 0 && p.TeamCapacity == 1 &&
                        p.MaxOccupants >= 2 && p.Ownership == CourseOwnershipPolicy.Individual; break;
                    case "Freestyle": valid = p.Capability == CourseCapability.Explore && p.TeamCount == 0 &&
                        p.TeamCapacity == 0 && p.Ownership == CourseOwnershipPolicy.Shared; break;
                }
            if (!valid) v.Add("play.invalid", "$.play", "", "Mode, capability, format, capacity and ownership do not agree.");
        }
    }
}
