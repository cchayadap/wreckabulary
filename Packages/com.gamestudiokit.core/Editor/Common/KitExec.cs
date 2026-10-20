// Game Studio Kit (MIT). Helpers for exec entry points (CONTRACTS §7.9): parse KitCli.Args, resolve paths, and hand
// KitCli a plain JSON tree (dictionaries, lists, strings, numbers, booleans) so every result keeps the exact field
// names and shapes of the kit's schemas whatever serializer KitCli uses.
using System;
using System.Collections.Generic;
using System.IO;
using GameStudioKit.Common;
using GameStudioKit.Qa;
using UnityEngine;

namespace GameStudioKit.Editor
{
    public static class KitExec
    {
        /// <summary>KitCli.Args parsed as an object (empty when missing or not an object).</summary>
        public static Dictionary<string, object> Args()
        {
            string json = KitCli.Args;
            if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, object>();
            var parsed = KitJson.Parse(json);
            return KitJson.AsObject(parsed) ?? throw new ArgumentException("exec args must be a JSON object");
        }

        /// <summary>Writes <paramref name="value"/> as the exec result, converted to a plain JSON tree first.</summary>
        public static void Result(object value) => KitCli.WriteResult(ToPlain(value));

        /// <summary>Round-trips through KitJson: IKitJson documents become dictionaries with their schema field names.</summary>
        public static object ToPlain(object value) => KitJson.Parse(KitJson.Serialize(value, false));

        /// <summary>The Unity project root U (the folder that holds Assets/).</summary>
        public static string ProjectRoot => Path.GetFullPath(Path.GetDirectoryName(Application.dataPath) ?? ".");

        /// <summary>The workspace root W (the folder with Tools/kit/kit.json at or above U), or U when none is found.</summary>
        public static string WorkspaceRoot
        {
            get
            {
                string kitJson = KitIo.FindUpwards(ProjectRoot, Path.Combine("Tools", "kit", "kit.json"), 4);
                return kitJson == null ? ProjectRoot : Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(kitJson)));
            }
        }

        /// <summary>
        /// Absolute path for an argument: rooted paths as given; Unity-side prefixes (Assets/, Packages/, Logs/, Builds/,
        /// Temp/, Library/) relative to U; anything else relative to W (the CONTRACTS §1.1 default).
        /// </summary>
        public static string ResolvePath(string p)
        {
            if (string.IsNullOrEmpty(p)) return null;
            if (Path.IsPathRooted(p)) return Path.GetFullPath(p);
            string posix = KitIo.ToPosix(p);
            foreach (var prefix in new[] { "Assets/", "Packages/", "Logs/", "Builds/", "Temp/", "Library/" })
                if (posix.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return Path.GetFullPath(Path.Combine(ProjectRoot, p));
            return Path.GetFullPath(Path.Combine(WorkspaceRoot, p));
        }

        /// <summary>W-relative POSIX path, or the absolute POSIX path outside W.</summary>
        public static string ToWorkspaceRel(string abs) =>
            KitIo.RelativePosix(WorkspaceRoot, abs) ?? KitIo.ToPosix(Path.GetFullPath(abs));

        /// <summary>The project's probe thresholds: args "probes_config", else W/Docs/qa/probes.config.json, else defaults.</summary>
        public static ProbeConfig LoadProbeConfig(Dictionary<string, object> args)
        {
            string path = ResolvePath(KitJson.GetString(args, "probes_config"));
            if (path == null) path = Path.Combine(WorkspaceRoot, "Docs", "qa", "probes.config.json");
            var config = ProbeConfig.LoadOrDefaults(path);
            foreach (var w in config.Warnings) KitLog.Warn("probes.config: " + w);
            return config;
        }

        public static string GetString(Dictionary<string, object> args, string key, string fallback = null) =>
            KitJson.GetString(args, key, fallback);
    }
}
