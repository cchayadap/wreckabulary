using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Wreckabulary.EditorTools
{
    public static class GameDataSetup
    {
        public const string ConfigFolder = "Assets/_Project/Data/Config/";
        public const string AssetPath = "Assets/_Project/Resources/GameData.asset";
        public const string RulesFile = "rules.json";
        public const string ItemsFile = "items.json";
        public const string HouseFile = "house_pinwheel.json";
        public const string WardrobeFile = "wardrobe.json";

        [MenuItem("Wreckabulary/Data/Set Up Game Data")]
        static void RunFromMenu() => Run();

        public static void Run()
        {
            MaterialLibraryBuilder.EnsureFolder(Path.GetDirectoryName(AssetPath).Replace('\\', '/'));
            var data = AssetDatabase.LoadAssetAtPath<GameData>(AssetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<GameData>();
                AssetDatabase.CreateAsset(data, AssetPath);
            }
            data.rules = Text(RulesFile);
            data.items = Text(ItemsFile);
            data.house = Text(HouseFile);
            data.wardrobe = Text(WardrobeFile);
            data.maps = Directory.GetFiles(ConfigFolder, "house_*.json")
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .Where(path => Path.GetFileName(path) != HouseFile)
                .Select(path => Text(Path.GetFileName(path))).ToArray();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            var config = data.Parse();
            GameConfig.Use(null);
            Debug.Log($"GAME_DATA_RESULT {{\"items\": {config.Items.All.Count}, \"enabled\": {config.Items.Enabled.Count()}, " +
                      $"\"modes\": {config.Rules.Modes.Count()}, \"rooms\": {config.House.Rooms.Count}, " +
                      $"\"furniture\": {config.House.Furniture.Count}, \"wardrobePieces\": {config.Wardrobe.Pieces.Count}}}");
        }

        static TextAsset Text(string file)
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigFolder + file);
            if (text == null) throw new FileNotFoundException($"{ConfigFolder}{file} is missing or isn't imported as text.");
            return text;
        }
    }
}
