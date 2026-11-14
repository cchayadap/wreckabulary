using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using Wreckabulary.Online;

namespace Wreckabulary.EditorTools
{
    public static class WordboundPublicCourseSetup
    {
        public const string CataloguePath = "Assets/_Project/Resources/Wordbound/public-catalogue.json";
        const string LaunchAssetsPath = "Assets/_Project/Resources/Wordbound/LaunchAssets.asset";

        public static void ExportAndAssignFromEnvironment()
        {
            string release = Environment.GetEnvironmentVariable("WORDBOUND_PUBLIC_RELEASE");
            string approvedSeeds = Environment.GetEnvironmentVariable("WORDBOUND_PUBLIC_SEEDS");
            if (string.IsNullOrWhiteSpace(release) || string.IsNullOrWhiteSpace(approvedSeeds))
                throw new InvalidOperationException("Provide an explicit approved public release and comma-separated seed list.");
            ExportAndAssign(release, approvedSeeds.Split(',').Select(seed => seed.Trim()).ToArray());
        }

        public static void ExportAndAssign(string mapPoolVersion, string[] approvedSeeds)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Export the public catalogue outside Play Mode.");
            var assets = AssetDatabase.LoadAssetAtPath<WordboundLaunchAssets>(LaunchAssetsPath);
            if (!assets) throw new InvalidOperationException("Create the native Wordbound launch assets first.");
            var catalogue = WordboundPublicCourseCatalog.ResolveRelease(
                File.ReadAllText("Assets/_Project/Data/Config/items.json"),
                File.ReadAllText("Assets/_Project/Data/Config/rules.json"), mapPoolVersion, approvedSeeds);
            string json = catalogue.Json;
            if (File.Exists(CataloguePath))
            {
                string previous = File.ReadAllText(CataloguePath);
                var existing = WordboundPublicCourseCatalog.Parse(previous);
                if (existing.MapPoolVersion == mapPoolVersion && existing.Json != json)
                    throw new InvalidOperationException("A published release is immutable. Use a new map-pool version for changed content or seeds.");
            }
            File.WriteAllText(CataloguePath, json, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(CataloguePath, ImportAssetOptions.ForceSynchronousImport);
            var imported = AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(CataloguePath);
            if (!imported || imported.text != json) throw new InvalidOperationException("The imported public catalogue differs from its exported bytes.");
            assets.ApprovedPublicCourses = imported;
            assets.MapPoolVersion = catalogue.MapPoolVersion;
            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssetIfDirty(assets);
            UnityEngine.Debug.Log("[Wreckabulary] Exported " + catalogue.Courses.Count +
                " exact public course bindings and assigned launch data. Worker activation is a separate deferred step.");
        }
    }
}
