using UnityEditor;

namespace Wreckabulary.EditorTools
{
    /// <summary>
    /// Runs every generator that turns pipeline output and JSON into Unity assets, in order.
    /// Menu: Wreckabulary → Set Up Art and Data.
    /// Batch: Unity -batchmode -quit -projectPath &lt;abs&gt; -executeMethod Wreckabulary.EditorTools.ProjectSetup.Run
    /// </summary>
    public static class ProjectSetup
    {
        [MenuItem("Wreckabulary/Set Up Art and Data", priority = 0)]
        static void RunFromMenu() => Run();

        public static void Run()
        {
            ArtSetup.Run();
            GameDataSetup.Run();
        }
    }
}
