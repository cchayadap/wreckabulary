// Game Studio Kit (MIT). Builds/<dir>/packed_assets.json (licenses.schema.json#/$defs/packedAssets, CONTRACTS §7.6):
// what actually ships, for the licence join (build_join.mjs). Engine-free so the node tests can serialise it under
// plain .NET and validate it against the schema.
using System;
using System.Collections.Generic;
using System.IO;
using GameStudioKit.Common;

namespace GameStudioKit.Editor.Ship
{
    public sealed class PackedAssetInfoRow
    {
        public string SourceAssetPath;
        public string Guid;
        public string Type;
        public ulong PackedSize;
    }

    public sealed class PackedFileRow
    {
        public string ShortPath;
        public readonly List<PackedAssetInfoRow> Contents = new List<PackedAssetInfoRow>();
    }

    public sealed class BuildFileRow
    {
        public string Path;
        public string Role;
        public ulong Size;
    }

    public sealed class PackedAssetsDocument : IKitJson
    {
        public string BuildId;

        /// <summary>release | release-qa | dev.</summary>
        public string Kind = "release";

        /// <summary>How Kind was decided: "defines" (the compiled scripting defines), "binary" (QaRunner found in the
        /// built player) or "folder" (the Builds/ folder prefix only). Additive field.</summary>
        public string KindSource;

        /// <summary>True when the player was compiled with GSK_QA (it contains QaRunner); null when unknown. Additive field.</summary>
        public bool? GskQa;
        public string Utc;
        public string UnityVersion;
        public string Profile;
        public bool Development;

        /// <summary>U-relative POSIX build folder.</summary>
        public string OutputDir;
        public readonly List<PackedFileRow> Packed = new List<PackedFileRow>();
        public readonly List<BuildFileRow> Files = new List<BuildFileRow>();

        /// <summary>
        /// Kind and id from the gate's folder names (CONTRACTS §7.8): Builds/&lt;id&gt; (release), Builds/qa-&lt;id&gt;
        /// (release-qa), Builds/dev-&lt;id&gt; (development). A development build is always "dev". Same rule as the
        /// player's PerfProbe.BuildId (KitIo.ParseBuildFolder).
        /// </summary>
        public static void KindFromFolder(string folderName, bool development, out string kind, out string buildId) =>
            KitIo.ParseBuildFolder(folderName, development, out kind, out buildId);

        /// <summary>
        /// The build kind from what was really compiled: development → dev; GSK_QA compiled in → release-qa, whatever the
        /// folder is called; otherwise release. <paramref name="gskQa"/> null (unknown) keeps the folder's kind.
        /// </summary>
        public static string ResolveKind(string folderKind, bool development, bool? gskQa)
        {
            if (development) return "dev";
            if (gskQa == true) return "release-qa";
            if (gskQa == false) return "release";
            return string.IsNullOrEmpty(folderKind) ? "release" : folderKind;
        }

        /// <summary>Type names compiled only under GSK_QA; a player assembly or IL2CPP metadata holding both has QaRunner.</summary>
        static readonly string[] QaRunnerMarkers = { "QaRunner", "QaRunOptions" };

        /// <summary>True when <paramref name="assemblyOrMetadata"/> (GameStudioKit.Runtime.dll or global-metadata.dat)
        /// contains every QaRunner marker as ASCII.</summary>
        public static bool ContainsQaRunner(byte[] assemblyOrMetadata)
        {
            if (assemblyOrMetadata == null) return false;
            foreach (var m in QaRunnerMarkers) if (!ContainsAscii(assemblyOrMetadata, m)) return false;
            return true;
        }

        public static bool ContainsAscii(byte[] data, string text)
        {
            byte[] needle = System.Text.Encoding.ASCII.GetBytes(text ?? "");
            if (data == null || needle.Length == 0 || data.Length < needle.Length) return false;
            for (int i = 0; i <= data.Length - needle.Length; i++)
            {
                if (data[i] != needle[0]) continue;
                int j = 1;
                while (j < needle.Length && data[i + j] == needle[j]) j++;
                if (j == needle.Length) return true;
            }
            return false;
        }

        /// <summary>POSIX path of <paramref name="file"/> inside <paramref name="buildDir"/>, or null when outside.</summary>
        public static string RelativeToBuild(string buildDir, string file) => KitIo.RelativePosix(buildDir, file);

        public void WriteJson(KitJsonWriter w)
        {
            w.BeginObject();
            w.Prop("schema", 1);
            w.Prop("build_id", BuildId ?? "unknown");
            w.Prop("kind", Kind);
            if (!string.IsNullOrEmpty(KindSource)) w.Prop("kind_source", KindSource);
            if (GskQa.HasValue) w.Prop("gsk_qa", GskQa.Value);
            w.Prop("utc", Utc ?? KitIo.UtcNowIso());
            if (!string.IsNullOrEmpty(UnityVersion)) w.Prop("unity_version", UnityVersion);
            if (!string.IsNullOrEmpty(Profile)) w.Prop("profile", Profile);
            w.Prop("development", Development);
            if (!string.IsNullOrEmpty(OutputDir)) w.Prop("output_dir", OutputDir);
            w.Name("packed").BeginArray();
            foreach (var p in Packed)
            {
                w.BeginObject();
                w.Prop("short_path", p.ShortPath ?? "");
                w.Name("contents").BeginArray();
                foreach (var c in p.Contents)
                {
                    w.BeginObject();
                    w.Prop("source_asset_path", c.SourceAssetPath ?? "");
                    w.Prop("guid", c.Guid ?? "");
                    w.Prop("type", c.Type ?? "");
                    w.Name("packed_size").Value(c.PackedSize);
                    w.EndObject();
                }
                w.EndArray();
                w.EndObject();
            }
            w.EndArray();
            w.Name("files").BeginArray();
            foreach (var f in Files)
            {
                w.BeginObject();
                w.Prop("path", f.Path ?? "");
                w.Prop("role", f.Role ?? "");
                w.Name("size").Value(f.Size);
                w.EndObject();
            }
            w.EndArray();
            w.EndObject();
        }

        public string ToJson() => KitJson.Serialize(this, true) + "\n";

        public void WriteTo(string path) => KitIo.WriteAllTextAtomic(path, ToJson());

        /// <summary>The folder holding the player (the output path itself when it is a folder).</summary>
        public static string BuildFolder(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath)) return null;
            if (Directory.Exists(outputPath) && !File.Exists(outputPath)) return Path.GetFullPath(outputPath);
            return Path.GetDirectoryName(Path.GetFullPath(outputPath));
        }
    }
}
