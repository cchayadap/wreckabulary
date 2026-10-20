using System;
using System.IO;
using System.Text;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class WorkshopStorage
    {
        readonly string root;
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        const int RecoveryBytes = CourseLimits.DocumentBytes * 3;
        public WorkshopStorage(string folder)
        {
            root = Path.GetFullPath(folder ?? throw new ArgumentNullException(nameof(folder)));
            Directory.CreateDirectory(root);
        }
        string PathFor(string id, string extension)
        {
            if (!CourseValidator.ValidId(id)) throw new ArgumentException("Use a valid course ID.");
            return Path.Combine(root, id + extension);
        }
        public void Save(WorkshopSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            AtomicWrite(PathFor(snapshot.Course().Id, ".autosave.json"), snapshot.RecoveryJson, RecoveryBytes);
        }
        public bool HasRecovery(string id)
        {
            string path = PathFor(id, ".autosave.json");
            return File.Exists(path) || File.Exists(path + ".bak");
        }
        public WorkshopSnapshot Recover(string id, out bool usedBackup)
        {
            string primary = PathFor(id, ".autosave.json");
            usedBackup = false;
            try { return WorkshopSnapshot.Recover(ReadBounded(primary, RecoveryBytes)); }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is FormatException)
            {
                string backup = primary + ".bak";
                if (!File.Exists(backup)) throw;
                var recovered = WorkshopSnapshot.Recover(ReadBounded(backup, RecoveryBytes));
                usedBackup = true; return recovered;
            }
        }
        public string ExportCourse(WorkshopSnapshot snapshot) => snapshot?.CourseJsonText ?? throw new ArgumentNullException(nameof(snapshot));
        public WorkshopSnapshot ImportCourse(string json)
        {
            var imported = CourseJson.ImportDocument(json);
            if (!imported.Success) throw new FormatException(string.Join("\n", imported.Issues));
            return new WorkshopSnapshot(imported.Document);
        }
        public string SaveShare(WorkshopSnapshot snapshot)
        {
            string target = PathFor(snapshot.Course().Id, ".course.json");
            AtomicWrite(target, ExportCourse(snapshot), CourseLimits.DocumentBytes);
            return target;
        }
        public WorkshopSnapshot LoadShare(string id) => ImportCourse(ReadBounded(PathFor(id, ".course.json"), CourseLimits.DocumentBytes));

        static string ReadBounded(string path, int maximum)
        {
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (input.Length > maximum) throw new FormatException("Workshop file exceeds the supported size.");
                using (var reader = new StreamReader(input, Utf8, false)) return reader.ReadToEnd();
            }
        }
        static void AtomicWrite(string path, string json, int maximum)
        {
            byte[] bytes = Utf8.GetBytes(json);
            if (bytes.Length > maximum) throw new ArgumentException("Workshop file exceeds the supported size.");
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
