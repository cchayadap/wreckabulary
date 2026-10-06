using UnityEditor;

namespace Wreckabulary.EditorTools
{
    public static class ProjectSetup
    {
        [MenuItem("Wreckabulary/Set Up Art and Data", priority = 0)]
        static void RunFromMenu() => Run();

        public static void Run()
        {
            ArtSetup.Run();
            GameDataSetup.Run();
            TactileMaterialBuildSupport.Generate();
        }
    }
}
