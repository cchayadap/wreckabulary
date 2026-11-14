using UnityEngine;

namespace Wreckabulary.EditorTools
{
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
