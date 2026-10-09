using UnityEngine;

namespace Wreckabulary.EditorTools
{
    /// <summary>Explicit, additive stage upgrade. Opening the workspace never runs this migration.</summary>
    public static class EnvironmentStageAuthoring
    {
        public static void Run()
        {
            SkillEffectAuthoring.EnsureFoamAssets();
            EnvironmentAuthoring.UpgradeWorlds();
            EnvironmentAuthoring.UpgradeScenes();
            EnvironmentLightingAuthoring.UpgradeScenes();
            Debug.Log("ENVIRONMENT_STAGE_READY: daylight, room dressing and foam art saved.");
        }
    }
}
