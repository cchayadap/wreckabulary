using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class HomeStorage
    {
        public const string Backup = ".bak";
        public const string CurrentFile = "-v2.json", LegacyFile = ".json";
        public static string DefaultFolder;
        readonly HomeDesigner designer;
        readonly string directory;
        public HomeStorage(HomeDesigner designer, string directory = null)
        {
            this.designer = designer ?? throw new ArgumentNullException(nameof(designer));
            this.directory = directory ?? DefaultFolder ?? Path.Combine(Application.persistentDataPath, "homes");
        }

        public static HomeStorage ForGame() => new HomeStorage(new HomeDesigner(GameConfig.Current.Houses, GameConfig.Current.Items));

        public bool TrySave(HomeLayout layout, out string error) => Write(layout, false, out _, out error);
        public bool TryExportFile(HomeLayout layout, out string path, out string error) => Write(layout, true, out path, out error);
        public bool TryLoad(string map, out HomeLayout layout, out string error) => TryLoad(map, out layout, out error, out _);
        public bool TryLoad(string map, out HomeLayout layout, out string error, out bool restored)
        {
            layout = null; error = null; restored = false;
            try
            {
                string path = PathFor(map, false);
                bool primary = File.Exists(path), backup = File.Exists(path + Backup);
                if (!primary && !backup) return TryLoadLegacy(map, out layout, out error);
                if (primary && Read(path, map, out layout, out error)) return true;
                string primaryError = error;
                if (backup && Read(path + Backup, map, out layout, out _)) { error = null; restored = true; return true; }
                layout = null; error = primaryError ?? "Saved home could not be read.";
                return false;
            }
            catch (Exception ex) { layout = null; error = "Could not load home: " + ex.Message; return false; }
        }

        bool TryLoadLegacy(string map, out HomeLayout layout, out string error)
        {
            layout = null; error = null;
            string path = Path.Combine(directory, map + LegacyFile);
            foreach (string file in new[] { path, path + Backup })
            {
                if (!File.Exists(file)) continue;
                if (new FileInfo(file).Length > HomeDesigner.MaxJsonLength * 4L) { error ??= "Saved home exceeds the supported file size."; continue; }
                var migrated = designer.ImportLegacy(File.ReadAllText(file, Encoding.UTF8));
                if (migrated.Ok && migrated.Layout.Map == map) { layout = migrated.Layout; error = null; return true; }
                error ??= migrated.Ok ? "Saved home belongs to a different map." : string.Join("\n", migrated.Errors);
            }
            return false;
        }

        public List<HomeLayout> Saved()
        {
            var homes = new List<HomeLayout>();
            foreach (string map in new[] { "pinwheel", "courtyard" })
                if (TryLoad(map, out var layout, out _)) homes.Add(layout);
            return homes;
        }

        bool Read(string path, string map, out HomeLayout layout, out string error)
        {
            layout = null; error = null;
            try
            {
                if (new FileInfo(path).Length > HomeDesigner.MaxJsonLength * 4L) { error = "Saved home exceeds the supported file size."; return false; }
                var result = designer.Import(File.ReadAllText(path, Encoding.UTF8));
                if (!result.Ok) { error = string.Join("\n", result.Errors); return false; }
                if (result.Layout.Map != map) { error = "Saved home belongs to a different map."; return false; }
                layout = result.Layout; return true;
            }
            catch (IOException ex) { error = "Could not load home: " + ex.Message; return false; }
            catch (UnauthorizedAccessException ex) { error = "Could not load home: " + ex.Message; return false; }
        }

        bool Write(HomeLayout layout, bool export, out string path, out string error)
        {
            path = null; error = null; string temp = null;
            try
            {
                var result = designer.Export(layout);
                if (!result.Ok) { error = string.Join("\n", result.Errors); return false; }
                path = PathFor(layout.Map, export);
                Directory.CreateDirectory(directory);
                temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                var bytes = new UTF8Encoding(false).GetBytes(result.Json);
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (!File.Exists(path)) File.Move(temp, path);
                else if (!export && Read(path, layout.Map, out _, out _)) File.Replace(temp, path, path + Backup);
                else File.Replace(temp, path, null);
                temp = null; return true;
            }
            catch (Exception ex) { error = "Could not save home: " + ex.Message; path = null; return false; }
            finally { if (temp != null) { try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } } }
        }

        string PathFor(string map, bool export)
        {
            if (map != "pinwheel" && map != "courtyard") throw new ArgumentException("Unsupported home map.", nameof(map));
            return Path.Combine(directory, map + (export ? "-export.json" : CurrentFile));
        }
    }

    public sealed class HomeHistory
    {
        readonly HomeDesigner designer;
        readonly List<HomeLayout> snapshots = new();
        int cursor;
        public HomeLayout Current => snapshots[cursor].Clone();
        public bool CanUndo => cursor > 0;
        public bool CanRedo => cursor + 1 < snapshots.Count;
        public HomeHistory(HomeDesigner designer, HomeLayout initial)
        {
            this.designer = designer;
            var result = designer.Validate(initial);
            if (!result.Ok) throw new ArgumentException(string.Join("\n", result.Errors), nameof(initial));
            snapshots.Add(result.Layout);
        }
        public bool TryApply(HomeLayout layout, out string error)
        {
            var result = designer.Validate(layout);
            error = result.Ok ? null : string.Join("\n", result.Errors);
            if (!result.Ok) return false;
            if (result.Layout.Map != snapshots[cursor].Map) { error = "An edit cannot change the map of this draft."; return false; }
            if (result.Layout.ToJson() == snapshots[cursor].ToJson()) return true;
            snapshots.RemoveRange(cursor + 1, snapshots.Count - cursor - 1);
            snapshots.Add(result.Layout); cursor++;
            if (snapshots.Count > 101) { snapshots.RemoveAt(0); cursor--; }
            return true;
        }
        public bool Undo() { if (!CanUndo) return false; cursor--; return true; }
        public bool Redo() { if (!CanRedo) return false; cursor++; return true; }
    }
}
