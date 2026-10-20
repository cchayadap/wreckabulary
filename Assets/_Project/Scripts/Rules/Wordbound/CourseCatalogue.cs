using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class CourseModuleDefinition
    {
        public string Id { get; }
        public int WidthMm { get; }
        public int DepthMm { get; }
        public int HeightMm { get; }
        public CourseModuleDefinition(string id, int widthMm, int depthMm, int heightMm)
        {
            if (!CourseValidator.ValidId(id) || widthMm < 1000 || depthMm < 1000 || heightMm < 1000 ||
                widthMm > 100000 || depthMm > 100000 || heightMm > 50000)
                throw new ArgumentException("Module definitions need a valid ID and bounded positive dimensions.");
            Id = id; WidthMm = widthMm; DepthMm = depthMm; HeightMm = heightMm;
        }
    }
    public sealed class CourseAssetDefinition
    {
        public string Id { get; }
        public string Letters { get; }
        public int WidthMm { get; }
        public int DepthMm { get; }
        public int HeightMm { get; }
        public bool CanCraft { get; }
        public CourseAssetDefinition(string id, string letters, int widthMm, int depthMm, int heightMm, bool canCraft = true)
        {
            if (!CourseValidator.ValidId(id) || !CourseValidator.ValidLetters(letters, false) ||
                widthMm < 1 || depthMm < 1 || heightMm < 1 || widthMm > 10000 || depthMm > 10000 || heightMm > 10000)
                throw new ArgumentException("Asset definitions need a valid ID, A-Z letters and bounded dimensions.");
            Id = id; Letters = letters; WidthMm = widthMm; DepthMm = depthMm; HeightMm = heightMm; CanCraft = canCraft;
        }
    }
    public sealed class CourseCatalogue
    {
        public IReadOnlyDictionary<string, CourseModuleDefinition> Modules { get; }
        public IReadOnlyDictionary<string, CourseAssetDefinition> Assets { get; }
        public IReadOnlyList<string> PuzzlePresets { get; }
        public IReadOnlyList<string> Themes { get; }
        public IReadOnlyList<string> NarrativeEvents { get; }
        public string Hash { get; }

        public CourseCatalogue(IEnumerable<CourseModuleDefinition> modules, IEnumerable<CourseAssetDefinition> assets,
            IEnumerable<string> puzzlePresets, IEnumerable<string> themes, IEnumerable<string> narrativeEvents)
        {
            Modules = new ReadOnlyDictionary<string, CourseModuleDefinition>(modules.ToDictionary(x => x.Id, StringComparer.Ordinal));
            Assets = new ReadOnlyDictionary<string, CourseAssetDefinition>(assets.ToDictionary(x => x.Id, StringComparer.Ordinal));
            PuzzlePresets = FreezeIds(puzzlePresets);
            Themes = FreezeIds(themes);
            NarrativeEvents = FreezeIds(narrativeEvents);
            Hash = CourseJson.Sha256(CourseJson.Encode(new Dictionary<string, object>
            {
                ["modules"] = Modules.Values.OrderBy(x => x.Id, StringComparer.Ordinal).Select(x => (object)new object[] { x.Id, x.WidthMm, x.DepthMm, x.HeightMm }).ToList(),
                ["assets"] = Assets.Values.OrderBy(x => x.Id, StringComparer.Ordinal).Select(x => (object)new object[] { x.Id, x.Letters, x.WidthMm, x.DepthMm, x.HeightMm, x.CanCraft }).ToList(),
                ["presets"] = PuzzlePresets, ["themes"] = Themes, ["narrative"] = NarrativeEvents
            }));
        }
        static IReadOnlyList<string> FreezeIds(IEnumerable<string> ids)
        {
            var list = ids.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            if (list.Any(x => !CourseValidator.ValidId(x)) || list.Distinct(StringComparer.Ordinal).Count() != list.Length)
                throw new ArgumentException("Catalogue IDs must be valid and unique.");
            return Array.AsReadOnly(list);
        }
        public CourseBox Bounds(CourseModule module)
        {
            var definition = Modules[module.DefinitionId];
            int width = module.Yaw % 180 == 0 ? definition.WidthMm : definition.DepthMm;
            int depth = module.Yaw % 180 == 0 ? definition.DepthMm : definition.WidthMm;
            return new CourseBox(new CoursePoint(module.Position.X - width / 2, module.Position.Y, module.Position.Z - depth / 2),
                new CoursePoint(module.Position.X + width / 2, module.Position.Y + definition.HeightMm, module.Position.Z + depth / 2));
        }
    }
}
