using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class WorkshopShareImport
    {
        public WorkshopSnapshot Snapshot { get; }
        public WorkshopModeChoice Mode { get; }
        public bool HasPlaySettings => Mode != null;
        internal WorkshopShareImport(WorkshopSnapshot snapshot, WorkshopModeChoice mode)
        { Snapshot = snapshot; Mode = mode; }

        public bool Apply(WorkshopHistory history)
        {
            if (history == null) throw new ArgumentNullException(nameof(history));
            var source = Snapshot.Edit();
            return history.Execute("Import map", target =>
            {
                target.Course = source.Course; target.Seed = source.Seed; target.SeedLocked = source.SeedLocked;
                target.GridMm = source.GridMm; target.ActiveLayer = source.ActiveLayer; target.Selection = source.Selection;
                target.Groups = source.Groups; target.HiddenLayers = source.HiddenLayers; target.LockedLayers = source.LockedLayers;
            });
        }
    }

    public static class WorkshopShare
    {
        public const string Format = "wreckabulary.workshop-share";
        public const int Schema = 1;
        public const int MaximumBytes = CourseLimits.DocumentBytes * 2 + 4096;

        public static string Export(WorkshopSnapshot snapshot, CourseCatalogue catalogue, CoursePlayRequirements play)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (catalogue == null) throw new ArgumentNullException(nameof(catalogue));
            var document = snapshot.Course();
            var choice = WorkshopModes.Choices(document).SingleOrDefault(item => Same(item.Requirements, play))
                ?? throw new ArgumentException("Choose an enabled mode and team format before sharing a playable map.");
            ValidatePlayable(snapshot, catalogue, choice);
            string json = CourseJson.Encode(new Dictionary<string, object>
            {
                ["format"] = Format, ["schema"] = Schema, ["course"] = snapshot.CourseJsonText,
                ["seed"] = snapshot.Seed, ["mode"] = choice.ModeId, ["raceFormat"] = choice.Format.ToString(),
                ["catalogueHash"] = catalogue.Hash, ["modeHash"] = ModeDefinition.For(choice.ModeId, choice.Format).Hash
            });
            CourseJson.Preflight(json, MaximumBytes, "course");
            return json;
        }

        public static WorkshopShareImport Import(string json, CourseCatalogue catalogue)
        {
            CourseJson.Preflight(json, MaximumBytes, "course");
            var node = Json.Parse(json, "shared map");
            if (node["format"].String() == CourseDocument.FormatId)
                return new WorkshopShareImport(ReadCourse(json), null);
            CourseJson.Keys(node, new[] { "format", "schema", "course", "seed", "mode", "raceFormat", "catalogueHash", "modeHash" });
            if (node["format"].String() != Format || node["schema"].Int() != Schema)
                throw new FormatException("This map bundle version is not supported.");
            if (catalogue == null) throw new ArgumentNullException(nameof(catalogue));
            string seed = node["seed"].String();
            if (!CourseRandom.ValidSeed(seed)) throw new FormatException("Use sixteen lowercase hexadecimal seed digits.");
            string formatName = node["raceFormat"].String();
            RaceFormat format = formatName switch {
                "Solo" => RaceFormat.Solo, "Duo" => RaceFormat.Duo, "Squad" => RaceFormat.Squad,
                _ => throw new FormatException("Choose Solo, Duo or Squad.")
            };
            var draft = ReadCourse(node["course"].String()).Edit();
            draft.Seed = seed;
            WorkshopModeChoice choice;
            try { choice = WorkshopModes.Select(draft.Course, node["mode"].String(), format); }
            catch (ArgumentException error) { throw new FormatException(error.Message, error); }
            if (node["catalogueHash"].String() != catalogue.Hash ||
                node["modeHash"].String() != ModeDefinition.For(choice.ModeId, choice.Format).Hash)
                throw new FormatException("This map uses different content or mode rules. Open it with the matching game version.");
            var snapshot = new WorkshopSnapshot(draft);
            ValidatePlayable(snapshot, catalogue, choice);
            return new WorkshopShareImport(snapshot, choice);
        }

        static WorkshopSnapshot ReadCourse(string json)
        {
            var imported = CourseJson.ImportDocument(json);
            if (!imported.Success) throw new FormatException(string.Join("\n", imported.Issues));
            return new WorkshopSnapshot(imported.Document);
        }

        static void ValidatePlayable(WorkshopSnapshot snapshot, CourseCatalogue catalogue, WorkshopModeChoice choice)
        {
            var result = CourseResolver.Resolve(snapshot.Course(), catalogue, new CourseResolveRequest(snapshot.Seed, choice.Requirements));
            if (!result.Success) throw new FormatException(result.Issues.FirstOrDefault()?.Message ?? "This map cannot resolve for its seed and mode.");
            string issue = WorkshopPhysicalSites.PlaytestIssue(result.Manifest);
            if (issue.Length != 0) throw new FormatException(issue);
        }

        static bool Same(CoursePlayRequirements a, CoursePlayRequirements b) => b != null && a.ModeId == b.ModeId &&
            a.Capability == b.Capability && a.TeamCount == b.TeamCount && a.TeamCapacity == b.TeamCapacity &&
            a.MaxOccupants == b.MaxOccupants && a.Ownership == b.Ownership;
    }
}
