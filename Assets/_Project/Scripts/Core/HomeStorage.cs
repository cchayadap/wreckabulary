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
        readonly HomeDesigner designer;
        readonly string directory;
        public HomeStorage(HomeDesigner designer, string directory = null)
        { this.designer = designer ?? throw new ArgumentNullException(nameof(designer)); this.directory = directory ?? Path.Combine(Application.persistentDataPath, "homes"); }

        public bool TrySave(HomeLayout layout, out string error) => Write(layout, false, out _, out error);
        public bool TryExportFile(HomeLayout layout, out string path, out string error) => Write(layout, true, out path, out error);
        public bool TryLoad(string map, out HomeLayout layout, out string error)
        {
            layout = null; error = null;
            try
            {
                string path = PathFor(map, false);
                if (!File.Exists(path)) return false;
                if (new FileInfo(path).Length > HomeDesigner.MaxJsonLength * 4L) { error = "Saved home exceeds the supported file size."; return false; }
                var result = designer.Import(File.ReadAllText(path, Encoding.UTF8));
                if (!result.Ok) { error = string.Join("\n", result.Errors); return false; }
                if (result.Layout.Map != map) { error = "Saved home belongs to a different map."; return false; }
                layout = result.Layout; return true;
            }
            catch (Exception ex) { error = "Could not load home: " + ex.Message; return false; }
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
                File.WriteAllText(temp, result.Json, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
                temp = null; return true;
            }
            catch (Exception ex) { error = "Could not save home: " + ex.Message; path = null; return false; }
            finally { if (temp != null) { try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } } }
        }

        string PathFor(string map, bool export)
        {
            if (map != "pinwheel" && map != "courtyard") throw new ArgumentException("Unsupported home map.", nameof(map));
            return Path.Combine(directory, map + (export ? "-export.json" : ".json"));
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
