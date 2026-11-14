using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class WorkshopMapEntry
    {
        public string Folder { get; }
        public string Source { get; }
        public string CourseId { get; }
        public string Title { get; }
        public DateTime SavedUtc { get; }
        public string Problem { get; }
        public bool Readable => Problem.Length == 0;
        internal WorkshopMapEntry(string folder, string source, string id, string title, DateTime saved, string problem)
        { Folder = folder; Source = source; CourseId = id; Title = title; SavedUtc = saved; Problem = problem ?? ""; }
    }

    public sealed class WorkshopFileEntry
    {
        public string Path { get; }
        public string Name { get; }
        public bool Playable { get; }
        public DateTime SavedUtc { get; }
        internal WorkshopFileEntry(string path, bool playable, DateTime saved)
        {
            Path = path; Playable = playable; SavedUtc = saved;
            string file = System.IO.Path.GetFileName(path);
            Name = file.Substring(0, file.Length - (playable ? PlayableExtension : CourseExtension).Length);
        }
        internal const string PlayableExtension = ".wordbound.json", CourseExtension = ".course.json";
    }

    public sealed class WorkshopStorage
    {
        public const string AutosaveExtension = ".autosave.json", SettingsExtension = ".settings.json";
        const string LastMapFile = "last-map.json", SettingsFormat = "wreckabulary.workshop-settings", LastFormat = "wreckabulary.workshop-last";
        const string BeforeImport = " (before import)";
        readonly string root;
        readonly Dictionary<string, string> owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string lastMap;
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        const int RecoveryBytes = CourseLimits.DocumentBytes * 3, SideBytes = 4096;
        static readonly string[] ReservedNames = { "con", "prn", "aux", "nul" };

        public WorkshopStorage(string folder)
        {
            root = Path.GetFullPath(folder ?? throw new ArgumentNullException(nameof(folder)));
            Directory.CreateDirectory(root);
        }
        public string Folder => root;

        public static bool ReservedName(string name)
        {
            string value = (name ?? "").ToLowerInvariant();
            return ReservedNames.Contains(value) || value.Length == 4 && (value.StartsWith("com", StringComparison.Ordinal) ||
                value.StartsWith("lpt", StringComparison.Ordinal)) && value[3] >= '1' && value[3] <= '9';
        }
        public static string FileStem(string id)
        {
            if (!CourseValidator.ValidId(id)) throw new ArgumentException("Use a valid course ID.");
            string stem = id.ToLowerInvariant();
            return ReservedName(stem) ? stem + "~" : stem;
        }
        string PathFor(string id, string extension) => Path.Combine(root, FileStem(id) + extension);
        string ExistingPath(string id, string extension)
        {
            string path = PathFor(id, extension), legacy = Path.Combine(root, id + extension);
            return File.Exists(path) || File.Exists(path + ".bak") || !(File.Exists(legacy) || File.Exists(legacy + ".bak")) ? path : legacy;
        }

        public void Save(WorkshopSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            string id = snapshot.Course().Id, path = PathFor(id, AutosaveExtension);
            Claim(id, path);
            AtomicWrite(path, snapshot.RecoveryJson, RecoveryBytes);
        }
        public bool HasRecovery(string id)
        {
            string path = ExistingPath(id, AutosaveExtension);
            return File.Exists(path) || File.Exists(path + ".bak");
        }
        public WorkshopSnapshot Recover(string id, out bool usedBackup)
        {
            string primary = ExistingPath(id, AutosaveExtension);
            usedBackup = false;
            try { return ReadRecovery(primary, id); }
            catch (Exception error) when (Unreadable(error))
            {
                string backup = primary + ".bak";
                if (!File.Exists(backup)) throw;
                var recovered = ReadRecovery(backup, id);
                usedBackup = true; return recovered;
            }
        }
        static bool Unreadable(Exception error) => error is IOException || error is ArgumentException || error is FormatException;
        static WorkshopSnapshot ReadRecovery(string path, string id)
        {
            var recovered = WorkshopSnapshot.Recover(ReadBounded(path, RecoveryBytes));
            if (id != null && recovered.Course().Id != id) throw new FormatException("The saved draft belongs to a different course ID.");
            return recovered;
        }
        public string ArchiveRecovery(string id)
        {
            string primary = ExistingPath(id, AutosaveExtension);
            string archive = Path.Combine(root, "recovery-archives", FileStem(id) + "-" + Stamp() + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(archive);
            foreach (string path in new[] { primary, primary + ".bak" })
                if (File.Exists(path)) File.Copy(path, Path.Combine(archive, Path.GetFileName(path)), false);
            return archive;
        }
        public string SetAside(string id)
        {
            string primary = ExistingPath(id, AutosaveExtension), kept = "";
            string suffix = ".unreadable-" + Stamp() + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            foreach (string path in new[] { primary, primary + ".bak" })
            {
                if (!File.Exists(path)) continue;
                File.Move(path, path + suffix);
                if (kept.Length == 0) kept = path + suffix;
            }
            owners.Remove(primary);
            return kept;
        }
        static string Stamp() => DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);

        public string ExportCourse(WorkshopSnapshot snapshot) => snapshot?.CourseJsonText ?? throw new ArgumentNullException(nameof(snapshot));
        public WorkshopSnapshot ImportCourse(string json)
        {
            var imported = CourseJson.ImportDocument(json);
            if (!imported.Success) throw new FormatException(string.Join("\n", imported.Issues));
            return new WorkshopSnapshot(imported.Document);
        }
        public string SaveShare(WorkshopSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            string id = snapshot.Course().Id, target = PathFor(id, WorkshopFileEntry.CourseExtension);
            Claim(id, target, PathFor(id, AutosaveExtension));
            AtomicWrite(target, ExportCourse(snapshot), CourseLimits.DocumentBytes);
            return target;
        }
        public WorkshopSnapshot LoadShare(string id) => ImportCourse(ReadBounded(ExistingPath(id, WorkshopFileEntry.CourseExtension), CourseLimits.DocumentBytes));

        public string SavePlayableShare(WorkshopSnapshot snapshot, CourseCatalogue catalogue, CoursePlayRequirements play)
        {
            string json = WorkshopShare.Export(snapshot, catalogue, play);
            string id = snapshot.Course().Id, target = PathFor(id, WorkshopFileEntry.PlayableExtension);
            Claim(id, target, PathFor(id, AutosaveExtension));
            AtomicWrite(target, json, WorkshopShare.MaximumBytes);
            return target;
        }
        public WorkshopShareImport LoadPlayableShare(string id, CourseCatalogue catalogue) =>
            WorkshopShare.Import(ReadBounded(ExistingPath(id, WorkshopFileEntry.PlayableExtension), WorkshopShare.MaximumBytes), catalogue);

        public IReadOnlyList<WorkshopFileEntry> Files() => Directory.GetFiles(root)
            .Where(path => path.EndsWith(WorkshopFileEntry.PlayableExtension, StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(WorkshopFileEntry.CourseExtension, StringComparison.OrdinalIgnoreCase))
            .Select(path => new WorkshopFileEntry(path, path.EndsWith(WorkshopFileEntry.PlayableExtension, StringComparison.OrdinalIgnoreCase),
                File.GetLastWriteTimeUtc(path)))
            .OrderByDescending(entry => entry.SavedUtc).ThenBy(entry => entry.Name, StringComparer.Ordinal).ToList().AsReadOnly();

        public WorkshopShareImport OpenFile(string path, CourseCatalogue catalogue)
        {
            string full = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
            if (!string.Equals(Path.GetDirectoryName(full)?.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Open a map file from this workshop folder.");
            if (full.EndsWith(WorkshopFileEntry.PlayableExtension, StringComparison.OrdinalIgnoreCase))
                return WorkshopShare.Import(ReadBounded(full, WorkshopShare.MaximumBytes), catalogue);
            if (full.EndsWith(WorkshopFileEntry.CourseExtension, StringComparison.OrdinalIgnoreCase))
                return WorkshopShare.Import(ImportCourse(ReadBounded(full, CourseLimits.DocumentBytes)).CourseJsonText, catalogue);
            throw new ArgumentException("Choose a .wordbound.json or .course.json file.");
        }

        public void SaveSettings(string id, string modeId, RaceFormat format)
        {
            if (!CourseValidator.ModeIds.Contains(modeId)) throw new ArgumentException("Unknown Wordbound mode.");
            AtomicWrite(PathFor(id, SettingsExtension), CourseJson.Encode(new Dictionary<string, object> {
                ["format"] = SettingsFormat, ["schema"] = 1, ["mode"] = modeId, ["raceFormat"] = format.ToString()
            }), SideBytes, false);
        }
        public bool TryLoadSettings(string id, out string modeId, out RaceFormat format)
        {
            modeId = ""; format = RaceFormat.Solo;
            try
            {
                string path = ExistingPath(id, SettingsExtension);
                if (!File.Exists(path)) return false;
                var node = Json.Parse(ReadBounded(path, SideBytes), "workshop settings");
                if (node["format"].String() != SettingsFormat || node["schema"].Int() != 1) return false;
                string mode = node["mode"].String();
                if (!CourseValidator.ModeIds.Contains(mode) || !Enum.TryParse(node["raceFormat"].String(), false, out RaceFormat parsed) ||
                    !Enum.IsDefined(typeof(RaceFormat), parsed)) return false;
                modeId = mode; format = parsed; return true;
            }
            catch (Exception error) when (Unreadable(error) || error is UnauthorizedAccessException) { return false; }
        }

        public string LastMap()
        {
            try
            {
                string path = Path.Combine(root, LastMapFile);
                if (!File.Exists(path)) return null;
                var node = Json.Parse(ReadBounded(path, SideBytes), "workshop last map");
                if (node["format"].String() != LastFormat || node["schema"].Int() != 1) return null;
                string id = node["courseId"].String();
                return CourseValidator.ValidId(id) ? lastMap = id : null;
            }
            catch (Exception error) when (Unreadable(error) || error is UnauthorizedAccessException) { return null; }
        }
        public void RememberLastMap(string id)
        {
            if (!CourseValidator.ValidId(id)) throw new ArgumentException("Use a valid course ID.");
            if (lastMap == id) return;
            AtomicWrite(Path.Combine(root, LastMapFile), CourseJson.Encode(new Dictionary<string, object> {
                ["format"] = LastFormat, ["schema"] = 1, ["courseId"] = id }), SideBytes, false);
            lastMap = id;
        }

        public static string NewMapId(string name, IEnumerable<string> taken)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in taken ?? Enumerable.Empty<string>())
                if (!string.IsNullOrEmpty(id)) used.Add(CourseValidator.ValidId(id) ? FileStem(id) : id.ToLowerInvariant());
            string slug = Slug(name);
            for (int number = 1; number <= 9999; number++)
            {
                string suffix = number == 1 ? "" : "-" + number.ToString(CultureInfo.InvariantCulture);
                string candidate = slug.Substring(0, Math.Min(slug.Length, 48 - suffix.Length)).TrimEnd('-') + suffix;
                if (!ReservedName(candidate) && !used.Contains(FileStem(candidate))) return candidate;
            }
            throw new InvalidOperationException("Choose a different map name.");
        }
        public string FreeMapId(string name, params string[] alsoTaken) => NewMapId(name, TakenNames().Concat(alsoTaken ?? Array.Empty<string>()));
        IEnumerable<string> TakenNames()
        {
            foreach (string path in Directory.GetFiles(root))
            {
                string file = Path.GetFileName(path);
                int dot = file.IndexOf('.');
                if (dot > 0) yield return file.Substring(0, dot);
            }
        }
        static string Slug(string name)
        {
            var text = new StringBuilder();
            foreach (char c in (name ?? "").ToLowerInvariant())
            {
                if (c >= 'a' && c <= 'z' || c >= '0' && c <= '9') text.Append(c);
                else if (text.Length > 0 && text[text.Length - 1] != '-') text.Append('-');
                if (text.Length >= 40) break;
            }
            string slug = text.ToString().Trim('-');
            return slug.Length == 0 ? "map" : slug;
        }

        public WorkshopSnapshot SaveAs(WorkshopSnapshot snapshot, string title)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            string name = (title ?? "").Trim();
            if (name.Length == 0) name = snapshot.Course().Title;
            if (name.Length > 80) name = name.Substring(0, 80).TrimEnd();
            var draft = snapshot.Edit();
            draft.Course.Id = FreeMapId(name, snapshot.Course().Id); draft.Course.Title = name;
            var copy = new WorkshopSnapshot(draft);
            Save(copy);
            return copy;
        }
        public WorkshopSnapshot PrepareImport(WorkshopSnapshot current, WorkshopSnapshot imported, out string keptId)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (imported == null) throw new ArgumentNullException(nameof(imported));
            string currentId = current.Course().Id, importedId = imported.Course().Id;
            Save(current);
            keptId = currentId;
            if (importedId == currentId)
            {
                if (imported.CourseJsonText == current.CourseJsonText && imported.Seed == current.Seed) return imported;
                string title = current.Course().Title;
                keptId = SaveAs(current, (title.Length + BeforeImport.Length > 80 ? title.Substring(0, 80 - BeforeImport.Length).TrimEnd() : title) + BeforeImport).Course().Id;
                return imported;
            }
            if (FileStem(importedId) != FileStem(currentId) && !HasRecovery(importedId)) return imported;
            var renamed = imported.Edit();
            renamed.Course.Id = FreeMapId(renamed.Course.Title, currentId, importedId);
            return new WorkshopSnapshot(renamed);
        }

        public IReadOnlyList<WorkshopMapEntry> List(string source = "") => ListFolder(root, source).ToList().AsReadOnly();
        public static IReadOnlyList<WorkshopMapEntry> Library(string baseFolder)
        {
            string full = Path.GetFullPath(baseFolder ?? throw new ArgumentNullException(nameof(baseFolder)));
            var entries = new List<WorkshopMapEntry>();
            if (!Directory.Exists(full)) return entries.AsReadOnly();
            entries.AddRange(ListFolder(full, ""));
            foreach (string group in new[] { "templates", "local-courses" })
            {
                string parent = Path.Combine(full, group);
                if (!Directory.Exists(parent)) continue;
                foreach (string folder in Directory.GetDirectories(parent).OrderBy(path => path, StringComparer.Ordinal))
                    entries.AddRange(ListFolder(folder, group + "/" + Path.GetFileName(folder)));
            }
            return entries.OrderByDescending(entry => entry.SavedUtc).ThenBy(entry => entry.Title, StringComparer.Ordinal).ToList().AsReadOnly();
        }
        static IEnumerable<WorkshopMapEntry> ListFolder(string folder, string source)
        {
            if (!Directory.Exists(folder)) yield break;
            foreach (string path in Directory.GetFiles(folder).OrderBy(path => path, StringComparer.Ordinal))
            {
                if (!path.EndsWith(AutosaveExtension, StringComparison.OrdinalIgnoreCase)) continue;
                string stem = Path.GetFileName(path);
                stem = stem.Substring(0, stem.Length - AutosaveExtension.Length);
                WorkshopSnapshot snapshot = null; string problem = "";
                foreach (string candidate in new[] { path, path + ".bak" })
                {
                    if (!File.Exists(candidate)) continue;
                    try { snapshot = ReadRecovery(candidate, null); problem = ""; break; }
                    catch (Exception error) when (Unreadable(error)) { if (problem.Length == 0) problem = error.Message; }
                }
                DateTime saved;
                try { saved = File.GetLastWriteTimeUtc(path); }
                catch (Exception error) when (Unreadable(error) || error is UnauthorizedAccessException) { saved = DateTime.MinValue; }
                if (snapshot == null) yield return new WorkshopMapEntry(folder, source, stem.TrimEnd('~'), "", saved, problem.Length == 0 ? "This save cannot be read." : problem);
                else
                {
                    var course = snapshot.Course();
                    yield return new WorkshopMapEntry(folder, source, course.Id, course.Title, saved, "");
                }
            }
        }

        void Claim(string id, params string[] paths)
        {
            foreach (string path in paths)
            {
                if (owners.TryGetValue(path, out string owner) && owner == id) continue;
                string existing = SavedCourseId(path);
                if (existing != null && existing != id)
                    throw new IOException("\"" + existing + "\" already uses this file name. Course IDs that differ only by capital letters cannot share a folder.");
            }
            foreach (string path in paths) owners[path] = id;
        }
        static string SavedCourseId(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var node = Json.Parse(ReadBounded(path, RecoveryBytes), "workshop file");
                if (node.Has("course")) node = Json.Parse(node["course"].String(), "workshop course");
                return node.Has("id") ? node["id"].String() : null;
            }
            catch (Exception error) when (Unreadable(error) || error is UnauthorizedAccessException) { return null; }
        }
        static string ReadBounded(string path, int maximum)
        {
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (input.Length > maximum) throw new FormatException("Workshop file exceeds the supported size.");
                using (var reader = new StreamReader(input, Utf8, false)) return reader.ReadToEnd();
            }
        }
        static void AtomicWrite(string path, string json, int maximum, bool backup = true)
        {
            byte[] bytes = Utf8.GetBytes(json);
            if (bytes.Length > maximum) throw new ArgumentException("Workshop file exceeds the supported size.");
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
                if (File.Exists(path)) File.Replace(temporary, path, backup ? path + ".bak" : null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
