// Game Studio Kit (MIT). After every player build, writes <build folder>/packed_assets.json from the BuildReport
// (CONTRACTS §3.6, §7.6): every serialized file with the source assets packed into it, and every file in the build
// folder with its role. build_join.mjs joins it to Docs/licenses.csv, so the licence scope is what really ships.
// The build kind comes from what was compiled, not from the folder name: a player built with GSK_QA contains QaRunner
// and is "release-qa" wherever it was written (a folder a human picked in the Build Profiles window included).
using System;
using System.Collections.Generic;
using System.IO;
using GameStudioKit.Common;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GameStudioKit.Editor.Ship
{
    public sealed class BuildReportExport : IPostprocessBuildWithReport
    {
        public const string FileName = "packed_assets.json";
        public const string QaDefine = "GSK_QA";

        public int callbackOrder => 1000;

        public void OnPostprocessBuild(BuildReport report)
        {
            try
            {
                string path = Export(report);
                KitLog.Info("BuildReportExport: wrote " + path);
            }
            catch (Exception e)
            {
                KitLog.Error("BuildReportExport: could not write " + FileName + ": " + e.Message);
            }
        }

        /// <summary>Writes packed_assets.json for <paramref name="report"/> and returns its path.</summary>
        public static string Export(BuildReport report)
        {
            var summary = report.summary;
            string buildDir = PackedAssetsDocument.BuildFolder(summary.outputPath);
            if (buildDir == null) throw new InvalidOperationException("the build report has no output path");
            bool development = (summary.options & BuildOptions.Development) != 0;
            PackedAssetsDocument.KindFromFolder(Path.GetFileName(buildDir), development, out string folderKind, out string buildId);

            // What was compiled: the built player itself when its assemblies can be read (extra defines passed to
            // BuildPipeline.BuildPlayer are invisible afterwards), else the active profile's and player settings' defines.
            string kindSource = "folder";
            bool? gskQa = null;
            var defines = CompiledDefines(summary.platformGroup);
            bool? fromDefines = defines == null ? (bool?)null : defines.Contains(QaDefine);
            bool? fromBinary = PlayerContainsQaRunner(buildDir);
            if (fromBinary.HasValue)
            {
                gskQa = fromBinary;
                kindSource = "binary";
                if (fromDefines.HasValue && fromDefines != fromBinary)
                    KitLog.Warn("BuildReportExport: the defines say GSK_QA is " + (fromDefines.Value ? "on" : "off") + " but the player " +
                                (fromBinary.Value ? "contains" : "does not contain") + " QaRunner; the player wins");
            }
            else if (fromDefines.HasValue)
            {
                gskQa = fromDefines;
                kindSource = "defines";
            }
            string kind = PackedAssetsDocument.ResolveKind(folderKind, development, gskQa);
            if (kind != folderKind)
            {
                KitLog.Warn("BuildReportExport: " + Path.GetFileName(buildDir) + " is named like a " + folderKind + " build, but the player is " + kind +
                            " (" + kindSource + "); packed_assets.json says " + kind + ". Gate builds use Builds/qa-<id>/ for GSK_QA players.");
            }

            string projectRoot = Path.GetFullPath(Path.GetDirectoryName(Application.dataPath) ?? ".");
            var doc = new PackedAssetsDocument
            {
                BuildId = buildId,
                Kind = kind,
                KindSource = kindSource,
                GskQa = gskQa,
                Utc = KitIo.UtcNowIso(),
                UnityVersion = Application.unityVersion,
                Profile = ActiveProfilePath(),
                Development = development,
                OutputDir = KitIo.RelativePosix(projectRoot, buildDir) ?? KitIo.ToPosix(buildDir),
            };

            foreach (var packed in report.packedAssets)
            {
                var row = new PackedFileRow { ShortPath = packed.shortPath };
                foreach (var info in packed.contents)
                {
                    row.Contents.Add(new PackedAssetInfoRow
                    {
                        SourceAssetPath = info.sourceAssetPath,
                        Guid = info.sourceAssetGUID.ToString(),
                        Type = info.type != null ? info.type.Name : "",
                        PackedSize = info.packedSize,
                    });
                }
                doc.Packed.Add(row);
            }

            foreach (var file in report.GetFiles())
            {
                string rel = PackedAssetsDocument.RelativeToBuild(buildDir, file.path);
                if (rel == null) continue;
                doc.Files.Add(new BuildFileRow { Path = rel, Role = file.role, Size = file.size });
            }

            string outPath = Path.Combine(buildDir, FileName);
            doc.WriteTo(outPath);
            return outPath;
        }

        /// <summary>
        /// The scripting defines the player was compiled with: the active build profile's defines plus the player
        /// settings' defines for the build's target group. Null when neither can be read.
        /// </summary>
        static HashSet<string> CompiledDefines(BuildTargetGroup group)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            bool any = false;
            try
            {
                var profile = BuildProfile.GetActiveBuildProfile();
                if (profile != null && profile.scriptingDefines != null)
                {
                    any = true;
                    foreach (var d in profile.scriptingDefines) if (!string.IsNullOrWhiteSpace(d)) set.Add(d.Trim());
                }
            }
            catch (Exception e)
            {
                KitLog.Warn("BuildReportExport: cannot read the build profile's defines: " + e.Message);
            }
            try
            {
                if (group != BuildTargetGroup.Unknown)
                {
                    string symbols = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(group));
                    any = true;
                    foreach (var d in (symbols ?? "").Split(';')) if (!string.IsNullOrWhiteSpace(d)) set.Add(d.Trim());
                }
            }
            catch (Exception e)
            {
                KitLog.Warn("BuildReportExport: cannot read the player settings' defines: " + e.Message);
            }
            return any ? set : null;
        }

        /// <summary>
        /// Whether the built player contains the QA runner: the names QaRunner and QaRunOptions (both only compiled under
        /// GSK_QA) in the Mono GameStudioKit.Runtime.dll or the IL2CPP global-metadata.dat. Null when neither file is
        /// found (another platform layout).
        /// </summary>
        public static bool? PlayerContainsQaRunner(string buildDir)
        {
            if (string.IsNullOrEmpty(buildDir) || !Directory.Exists(buildDir)) return null;
            bool? found = null;
            try
            {
                foreach (var dataDir in Directory.GetDirectories(buildDir, "*_Data"))
                {
                    foreach (var candidate in new[]
                             {
                                 Path.Combine(dataDir, "Managed", "GameStudioKit.Runtime.dll"),
                                 Path.Combine(dataDir, "il2cpp_data", "Metadata", "global-metadata.dat"),
                             })
                    {
                        if (!File.Exists(candidate)) continue;
                        if (PackedAssetsDocument.ContainsQaRunner(File.ReadAllBytes(candidate))) return true;
                        found = false;
                    }
                }
            }
            catch (Exception e)
            {
                KitLog.Warn("BuildReportExport: cannot scan the player for QaRunner: " + e.Message);
                return null;
            }
            return found;
        }

        static string ActiveProfilePath()
        {
            try
            {
                var profile = BuildProfile.GetActiveBuildProfile();
                return profile != null ? AssetDatabase.GetAssetPath(profile) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
