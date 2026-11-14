using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    [Serializable] public sealed class WordboundModuleDefinition
    { public string Id = "Room"; public Vector3Int SizeMm = new Vector3Int(20000, 4000, 20000); }
    [Serializable] public sealed class WordboundAssetDefinition
    { public string Id = "", Letters = ""; public Vector3Int SizeMm = new Vector3Int(1000, 1000, 1000); public bool CanCraft = true; }

    [CreateAssetMenu(menuName = "Wreckabulary/Wordbound/Editable course")]
    public sealed class WordboundCourseAsset : ScriptableObject
    {
        [SerializeField, HideInInspector] string documentJson = "";
        public TextAsset Items;
        public TextAsset RuleBook;
        public string Seed = "0000000000000001";
        public string ModeId = "Freestyle";
        public RaceFormat Format = RaceFormat.Solo;
        public WordboundCameraProfile CameraProfile;
        public WordboundVisualProfile VisualProfile;
        public List<WordboundModuleDefinition> Modules = new List<WordboundModuleDefinition>();
        public List<WordboundAssetDefinition> Assets = new List<WordboundAssetDefinition>();
        public List<string> PuzzlePresets = new List<string>();
        public List<string> Themes = new List<string>();
        public List<string> NarrativeEvents = new List<string>();
        public bool HasDocument => !string.IsNullOrEmpty(documentJson);
        public string DocumentJson => documentJson;
        public string SourceHash => CourseJson.Sha256(documentJson);
        public string InputHash => WordboundIdentity.Sha256(string.Join("|", "wordbound.inspector-input-v1", SourceHash,
            Seed, ModeDefinition.For(ModeId, Format).Hash, Catalogue().Hash,
            Items ? WordboundIdentity.Sha256(Items.text) : "", RuleBook ? WordboundIdentity.Sha256(RuleBook.text) : ""));

        public CourseDocument ReadDocument()
        {
            var loaded = CourseJson.ImportDocument(documentJson);
            if (!loaded.Success) throw new ArgumentException(string.Join("\n", loaded.Issues));
            return loaded.Document;
        }
        public void SaveDocument(CourseDocument document) => ImportDocument(CourseJson.ExportDocument(document));
        public void ImportDocument(string json)
        {
            var loaded = CourseJson.ImportDocument(json);
            if (!loaded.Success) throw new ArgumentException(string.Join("\n", loaded.Issues));
            documentJson = CourseJson.ExportDocument(loaded.Document);
        }
        public CourseCatalogue Catalogue()
        {
            if (Modules == null || Assets == null || Modules.Count > 256 || Assets.Count > 8192 ||
                Modules.Any(m => m == null) || Assets.Any(a => a == null))
                throw new ArgumentException("The geometry catalogue exceeds its supported bounds.");
            return new CourseCatalogue(Modules.Select(m => new CourseModuleDefinition(m.Id, m.SizeMm.x, m.SizeMm.z, m.SizeMm.y)),
                Assets.Select(a => new CourseAssetDefinition(a.Id, a.Letters, a.SizeMm.x, a.SizeMm.z, a.SizeMm.y, a.CanCraft)),
                PuzzlePresets, Themes, NarrativeEvents);
        }
        public void Initialize(CourseDocument document, CourseCatalogue catalogue)
        {
            string json = CourseJson.ExportDocument(document);
            if (catalogue == null) throw new ArgumentNullException(nameof(catalogue));
            Modules = catalogue.Modules.Values.Select(m => new WordboundModuleDefinition {
                Id = m.Id, SizeMm = new Vector3Int(m.WidthMm, m.HeightMm, m.DepthMm) }).ToList();
            Assets = catalogue.Assets.Values.Select(a => new WordboundAssetDefinition {
                Id = a.Id, Letters = a.Letters, SizeMm = new Vector3Int(a.WidthMm, a.HeightMm, a.DepthMm), CanCraft = a.CanCraft }).ToList();
            PuzzlePresets = catalogue.PuzzlePresets.ToList(); Themes = catalogue.Themes.ToList();
            NarrativeEvents = catalogue.NarrativeEvents.ToList(); documentJson = json;
        }
        public ResolvedCourse Resolve()
        {
            var mode = ModeDefinition.For(ModeId, Format);
            var result = CourseResolver.Resolve(ReadDocument(), Catalogue(),
                new CourseResolveRequest(Seed, MatchStateValidation.Requirements(mode)));
            if (!result.Success) throw new ArgumentException(string.Join("\n", result.Validation.Issues));
            return result.Manifest;
        }
        public IWordboundPuzzleContent CreateContent(ResolvedCourse exactPreview)
        {
            if (exactPreview == null || exactPreview.SourceHash != SourceHash || exactPreview.Seed != Seed)
                throw new ArgumentException("The course or seed changed. Preview again before playing.");
            if (!Items || !RuleBook) throw new ArgumentException("Assign the item catalogue and rule book TextAssets.");
            CameraProfile?.Validate(); VisualProfile?.Validate();
            var geometry = Catalogue();
            var imported = CourseJson.ImportManifest(CourseJson.ExportManifest(exactPreview), geometry);
            if (!imported.Success || imported.Manifest.Hash != exactPreview.Hash)
                throw new ArgumentException("The exact preview does not match the current geometry catalogue.");
            return new WordboundAuthoredPuzzleContent(ModeDefinition.For(ModeId, Format), geometry,
                exactPreview, Items.text, RuleBook.text);
        }
    }
}
