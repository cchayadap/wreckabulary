using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Wreckabulary.EditorTools
{
    public static class PowerRecipeAuthoring
    {
        public const string SupplyName = "WATER - Recipe supply";

        [MenuItem("Wreckabulary/Authoring/Add Power Recipe Supplies")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play mode before authoring supplies.");
            GameDataSetup.Run();
            GeneratedItemArtImporter.Rebuild();
            foreach (string map in new[] { "pinwheel", "courtyard", "walkup" })
            {
                string path = "Assets/_Project/Resources/" + AuthoredHouse.ResourcePath(map) + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var world = root.GetComponent<AuthoredHouse>();
                    if (!AddMissingSupply(world)) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                    if (!success) throw new InvalidOperationException("Failed to save recipe supply in " + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("POWER_RECIPES_READY: 24 enabled recipes, six generated illustrations and WATER supplies in all houses.");
        }

        /// <summary>Add only the new supply; repeat runs preserve existing artist positions and all other world content.</summary>
        public static bool AddMissingSupply(AuthoredHouse world)
        {
            if (!world || !world.FurnitureRoot) throw new ArgumentException("A saved authored house is required.");
            if (world.FurnitureRoot.Find(SupplyName)) return false;
            var layout = GameConfig.Current.HouseFor(world.MapId);
            var placement = layout.Furniture.Last(entry => entry.Word == "WATER");
            var position = new Vector3(placement.X, layout.Room(placement.Room).FloorY + placement.Y, placement.Z);
            var prop = FurnitureCatalog.Spawn(placement.Word, position, placement.Yaw, world.FurnitureRoot);
            prop.name = SupplyName;
            WorldAuthoring.PersistAssets(prop.gameObject, world.MapId + "_power_supply");
            return true;
        }
    }
}
