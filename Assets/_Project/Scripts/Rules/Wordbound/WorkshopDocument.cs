using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class WorkshopDraft
    {
        public CourseDocument Course;
        public string Seed = "0000000000000001", ActiveLayer = "";
        public bool SeedLocked = true;
        public int GridMm = 500;
        public List<string> Selection = new List<string>();
        public List<string> HiddenLayers = new List<string>();
        public List<string> LockedLayers = new List<string>();
        public Dictionary<string, List<string>> Groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    }

    public sealed class WorkshopSnapshot
    {
        public const string Format = "wreckabulary.workshop";
        readonly string courseJson, recoveryJson;
        readonly string[] selection, hidden, locked;
        readonly Dictionary<string, string[]> groups;
        public string CourseJsonText => courseJson;
        public string RecoveryJson => recoveryJson;
        public string Seed { get; }
        public bool SeedLocked { get; }
        public string ActiveLayer { get; }
        public int GridMm { get; }
        public IReadOnlyList<string> Selection => Array.AsReadOnly(selection);
        public IReadOnlyList<string> HiddenLayers => Array.AsReadOnly(hidden);
        public IReadOnlyList<string> LockedLayers => Array.AsReadOnly(locked);
        public IEnumerable<string> GroupIds => groups.Keys.OrderBy(id => id, StringComparer.Ordinal);

        public WorkshopSnapshot(CourseDocument course) : this(new WorkshopDraft
        {
            Course = course, ActiveLayer = course?.Layers?.FirstOrDefault()?.Id ?? ""
        }) { }

        public WorkshopSnapshot(WorkshopDraft draft)
        {
            if (draft == null || draft.Course == null) throw new ArgumentNullException(nameof(draft));
            courseJson = CourseJson.ExportDocument(draft.Course);
            var imported = CourseJson.ImportDocument(courseJson);
            if (!imported.Success) throw new ArgumentException(string.Join("\n", imported.Issues));
            if (!CourseRandom.ValidSeed(draft.Seed)) throw new ArgumentException("Use sixteen lowercase hexadecimal seed digits.");
            if (draft.GridMm < 100 || draft.GridMm > 5000) throw new ArgumentException("Grid is 100–5000 mm.");
            var modules = new HashSet<string>(draft.Course.Modules.Select(module => module.Id), StringComparer.Ordinal);
            var layers = new HashSet<string>(draft.Course.Layers.Select(layer => layer.Id), StringComparer.Ordinal);
            Seed = draft.Seed; SeedLocked = draft.SeedLocked; GridMm = draft.GridMm;
            ActiveLayer = layers.Contains(draft.ActiveLayer ?? "") ? draft.ActiveLayer : layers.OrderBy(id => id, StringComparer.Ordinal).FirstOrDefault() ?? "";
            selection = ValidMembers(draft.Selection, modules);
            hidden = ValidMembers(draft.HiddenLayers, layers);
            locked = ValidMembers(draft.LockedLayers, layers);
            groups = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var grouped = new HashSet<string>(StringComparer.Ordinal);
            if (draft.Groups == null || draft.Groups.Count > CourseLimits.Modules) throw new ArgumentException("Too many groups.");
            foreach (var group in draft.Groups.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (!CourseValidator.ValidId(group.Key)) throw new ArgumentException("Invalid group ID.");
                var members = ValidMembers(group.Value, modules);
                if (members.Length < 2) continue;
                if (members.Any(member => !grouped.Add(member))) throw new ArgumentException("A module can belong to only one group.");
                groups.Add(group.Key, members);
            }
            recoveryJson = CourseJson.Encode(new Dictionary<string, object>
            {
                ["format"] = Format, ["schema"] = 1, ["course"] = courseJson, ["seed"] = Seed,
                ["seedLocked"] = SeedLocked, ["gridMm"] = GridMm, ["activeLayer"] = ActiveLayer,
                ["selection"] = selection, ["hiddenLayers"] = hidden, ["lockedLayers"] = locked,
                ["groups"] = groups.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => (object)new Dictionary<string, object> { ["id"] = pair.Key, ["members"] = pair.Value }).ToList()
            });
        }

        static string[] ValidMembers(IEnumerable<string> source, HashSet<string> valid)
        {
            if (source == null) throw new ArgumentException("Editor lists cannot be null.");
            var values = source.Take(CourseLimits.Modules + 1).ToArray();
            if (values.Length > CourseLimits.Modules || values.Any(id => !CourseValidator.ValidId(id)))
                throw new ArgumentException("Editor list exceeds its ID limit.");
            return values.Where(valid.Contains).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        }

        public IReadOnlyList<string> GroupMembers(string id) =>
            groups.TryGetValue(id ?? "", out var members) ? Array.AsReadOnly(members) : Array.AsReadOnly(Array.Empty<string>());
        public CourseDocument Course() => CourseJson.ImportDocument(courseJson).Document;
        public WorkshopDraft Edit() => new WorkshopDraft
        {
            Course = Course(), Seed = Seed, SeedLocked = SeedLocked, ActiveLayer = ActiveLayer, GridMm = GridMm,
            Selection = selection.ToList(), HiddenLayers = hidden.ToList(), LockedLayers = locked.ToList(),
            Groups = groups.ToDictionary(pair => pair.Key, pair => pair.Value.ToList(), StringComparer.Ordinal)
        };

        public static WorkshopSnapshot Recover(string json)
        {
            CourseJson.Preflight(json, CourseLimits.DocumentBytes * 3);
            var node = Json.Parse(json, "workshop recovery");
            CourseJson.Keys(node, new[] { "format", "schema", "course", "seed", "seedLocked", "gridMm", "activeLayer", "selection", "hiddenLayers", "lockedLayers", "groups" });
            if (node["format"].String() != Format || node["schema"].Int() != 1) throw new FormatException("Not a workshop recovery file.");
            var course = CourseJson.ImportDocument(node["course"].String());
            if (!course.Success) throw new FormatException(string.Join("\n", course.Issues));
            var draft = new WorkshopDraft
            {
                Course = course.Document, Seed = node["seed"].String(), SeedLocked = node["seedLocked"].Bool(),
                GridMm = node["gridMm"].Int(), ActiveLayer = node["activeLayer"].String(),
                Selection = node["selection"].Strings(), HiddenLayers = node["hiddenLayers"].Strings(), LockedLayers = node["lockedLayers"].Strings()
            };
            if (node["groups"].Items.Count > CourseLimits.Modules) throw new FormatException("Too many groups.");
            foreach (var group in node["groups"].Items)
            {
                CourseJson.Keys(group, new[] { "id", "members" });
                draft.Groups.Add(group["id"].String(), group["members"].Strings());
            }
            return new WorkshopSnapshot(draft);
        }
    }

    public sealed class WorkshopHistory
    {
        readonly List<WorkshopSnapshot> undo = new List<WorkshopSnapshot>();
        readonly List<WorkshopSnapshot> redo = new List<WorkshopSnapshot>();
        const int HistoryLimit = 64, HistoryCharacters = 8 * 1024 * 1024;
        public WorkshopSnapshot Current { get; private set; }
        public int Revision { get; private set; }
        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;
        public string LastAction { get; private set; } = "Open";
        public WorkshopHistory(WorkshopSnapshot document) { Current = document ?? throw new ArgumentNullException(nameof(document)); }

        public bool Execute(string label, Action<WorkshopDraft> transaction)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            var draft = Current.Edit();
            transaction(draft);
            var next = new WorkshopSnapshot(draft);
            if (next.RecoveryJson == Current.RecoveryJson) return false;
            undo.Add(Current); redo.Clear(); Current = next;
            while (undo.Count > HistoryLimit || undo.Sum(snapshot => (long)snapshot.RecoveryJson.Length) > HistoryCharacters)
                undo.RemoveAt(0);
            LastAction = label ?? "Edit"; Revision++; return true;
        }

        public void Select(IEnumerable<string> ids, bool additive = false, bool expandGroups = true)
        {
            var draft = Current.Edit();
            var selected = new HashSet<string>(additive ? draft.Selection : Enumerable.Empty<string>(), StringComparer.Ordinal);
            foreach (string id in ids ?? throw new ArgumentNullException(nameof(ids))) selected.Add(id);
            if (expandGroups)
                foreach (var members in draft.Groups.Values)
                    if (members.Any(selected.Contains)) selected.UnionWith(members);
            draft.Selection = selected.ToList();
            var next = new WorkshopSnapshot(draft);
            if (next.RecoveryJson == Current.RecoveryJson) return;
            Current = next; Revision++;
        }

        public bool Undo()
        {
            if (!CanUndo) return false;
            redo.Add(Current); Current = undo[undo.Count - 1]; undo.RemoveAt(undo.Count - 1);
            LastAction = "Undo"; Revision++; return true;
        }
        public bool Redo()
        {
            if (!CanRedo) return false;
            undo.Add(Current); Current = redo[redo.Count - 1]; redo.RemoveAt(redo.Count - 1);
            LastAction = "Redo"; Revision++; return true;
        }
        public CourseResolution Preview(CourseCatalogue catalogue, CoursePlayRequirements play) =>
            CourseResolver.Resolve(Current.Course(), catalogue, new CourseResolveRequest(Current.Seed, play));
        public bool Reroll(string newSeed)
        {
            if (Current.SeedLocked) return false;
            return Execute("Reroll", draft => draft.Seed = newSeed);
        }
    }
}
