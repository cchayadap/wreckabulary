using System;
using UnityEditor;
using Wreckabulary.Online.Editor;

namespace Wreckabulary.EditorTools
{
    public static class WordboundDeliverySetup
    {
        public static void PrepareFromEnvironment()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Prepare delivery assets outside Play Mode.");
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WORDBOUND_PUBLIC_RELEASE")) ||
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WORDBOUND_PUBLIC_SEEDS")))
                throw new InvalidOperationException("Provide the explicit public release and seed list before preparing delivery assets.");
            WordboundEquipmentImporter.BuildDetailed();
            WordboundAuthoringSetup.CreateDefaults();
            WordboundPublicCourseSetup.ExportAndAssignFromEnvironment();
            AssetDatabase.SaveAssets();
        }
    }
}
