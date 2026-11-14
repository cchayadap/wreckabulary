using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class WorkspaceAssetBrowser
    {
        public sealed class Entry
        {
            public string Name, Category, Detail;
            public Object Asset;
        }

        static readonly string[] Categories = { "Weapons", "Furniture", "Tools", "Characters", "Accessories", "Materials", "Fonts", "Animation", "Audio", "UI" };
        static List<Entry> entries;
        static int category, page;
        static string search = "", error;
        static Object selected;
        const int PageSize = 24;

        static WorkspaceAssetBrowser() => EditorApplication.projectChanged += Invalidate;
        static void Invalidate() { entries = null; page = 0; }

        public static void Draw()
        {
            EditorGUILayout.LabelField("Asset library", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            category = GUILayout.SelectionGrid(category, Categories, 3, GUILayout.Height(92));
            search = EditorGUILayout.TextField("Find", search);
            if (EditorGUI.EndChangeCheck()) page = 0;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh")) Invalidate();
                if (GUILayout.Button("Project folders")) Select(AssetDatabase.LoadMainAssetAtPath("Assets/_Project"));
            }
            if (entries == null)
            {
                try { entries = ReadEntries().ToList(); error = null; }
                catch (Exception exception) { entries = new List<Entry>(); error = exception.Message; }
            }
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Warning);
            string current = Categories[category];
            if (current == "Accessories")
                EditorGUILayout.HelpBox("These pieces share the character rig. Select a mesh to inspect it; wardrobe choices are in the character data.", MessageType.Info);
            if (current == "Materials")
                EditorGUILayout.HelpBox("Imported materials can be rebuilt by the art pipeline. Use an editable copy for your own changes, then assign it to a prefab or visual profile.", MessageType.Info);
            if (current == "Fonts")
                EditorGUILayout.HelpBox("Saved lobby fonts and presets are in UI/Fonts and UI/Materials after typography setup. The Typography profile assigns them to the lobby.", MessageType.Info);
            var matches = entries.Where(entry => entry.Asset && entry.Category == current &&
                (string.IsNullOrWhiteSpace(search) || (entry.Name + " " + entry.Detail).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
            int pages = Mathf.Max(1, Mathf.CeilToInt(matches.Length / (float)PageSize));
            page = Mathf.Clamp(page, 0, pages - 1);
            foreach (var entry in matches.Skip(page * PageSize).Take(PageSize))
                using (new EditorGUILayout.HorizontalScope())
                {
                    var content = new GUIContent(entry.Name, AssetPreview.GetMiniThumbnail(entry.Asset), entry.Detail);
                    if (GUILayout.Button(content, selected == entry.Asset ? EditorStyles.miniButtonMid : EditorStyles.miniButton, GUILayout.Height(26)))
                    { selected = entry.Asset; Select(entry.Asset); }
                    if (entry.Asset is Material || entry.Asset is GameObject && (entry.Category is "Weapons" or "Furniture" or "Tools"))
                        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                            if (GUILayout.Button("Copy", GUILayout.Width(48), GUILayout.Height(26))) MakeCopy(entry);
                }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(page == 0)) if (GUILayout.Button("Previous")) page--;
                GUILayout.Label($"{matches.Length} assets · {page + 1}/{pages}", EditorStyles.centeredGreyMiniLabel);
                using (new EditorGUI.DisabledScope(page + 1 >= pages)) if (GUILayout.Button("Next")) page++;
            }
            if (selected)
            {
                EditorGUILayout.Space();
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("Selected", selected, typeof(Object), false);
                EditorGUILayout.SelectableLabel(AssetDatabase.GetAssetPath(selected), EditorStyles.wordWrappedMiniLabel, GUILayout.Height(38));
            }
        }

        public static IReadOnlyList<Entry> ReadEntries()
        {
            var result = new List<Entry>();
            var library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(ImportedArtSettings.ModelLibraryAssetPath);
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Config/items.json");
            var catalogue = source ? ItemCatalogue.FromJson(source.text) : null;
            if (library)
                foreach (var model in library.Entries)
                {
                    if (!model.model) continue;
                    var item = catalogue?.All.FirstOrDefault(value => value.Model == model.key);
                    string group = item == null ? model.key.StartsWith("Avatar/", StringComparison.Ordinal) ? "Characters" : "Furniture"
                        : item.Category == ItemCategory.Combat ? "Weapons" : item.Category == ItemCategory.Furniture ? "Furniture" : "Tools";
                    if (model.key.StartsWith("Items/", StringComparison.Ordinal) || model.key.StartsWith("Environment/", StringComparison.Ordinal) || group == "Characters")
                        Add(result, model.model.name, group, model.model, model.key);
                    foreach (var clip in model.clips ?? Array.Empty<AnimationClip>())
                        if (clip) Add(result, clip.name, "Animation", clip, model.key);
                }
            var avatar = library ? library.Find("Avatar/Avatar") : null;
            var wardrobeSource = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Config/wardrobe.json");
            if (avatar && wardrobeSource)
            {
                var wardrobe = WardrobeCatalogue.FromJson(wardrobeSource.text);
                foreach (var piece in wardrobe.Pieces)
                {
                    var renderer = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(value => value.name == piece.Mesh);
                    if (renderer && renderer.sharedMesh) Add(result, piece.Id, "Accessories", renderer.sharedMesh, piece.Slot + " · " + piece.Mesh);
                }
                Add(result, "Wardrobe choices", "Accessories", wardrobeSource, "Shared rig pieces and colour choices");
            }
            AddAssets(result, "t:Material", "Materials", "Assets/_Project/Materials", "Assets/_Project/Art", "Assets/_Project/UI", "Assets/_Project/Resources/Wordbound/Render");
            AddAssets(result, "t:Font", "Fonts", "Assets/_Project/Resources/Fonts");
            AddAssets(result, "t:TMP_FontAsset", "Fonts", "Assets/_Project/UI/Fonts");
            AddAssets(result, "t:AnimationClip", "Animation", "Assets/_Project/Art/Seasonal");
            AddAssets(result, "t:AudioClip", "Audio", "Assets/_Project/Audio", "Assets/_Project/Resources/Audio");
            AddAssets(result, "t:Texture2D", "UI", "Assets/_Project/Resources/UI");
            AddAssets(result, "t:GameObject", "Weapons", "Assets/_Project/Prefabs/Equipment/Weapons");
            AddAssets(result, "t:GameObject", "Furniture", "Assets/_Project/Prefabs/Furniture");
            AddAssets(result, "t:GameObject", "Tools", "Assets/_Project/Prefabs/Equipment/Tools");
            return result.GroupBy(entry => (entry.Category, entry.Asset)).Select(group => group.First())
                .OrderBy(entry => entry.Category, StringComparer.Ordinal).ThenBy(entry => entry.Name, StringComparer.Ordinal).ToArray();
        }

        static void Add(List<Entry> target, string name, string group, Object asset, string detail) =>
            target.Add(new Entry { Name = name, Category = group, Asset = asset, Detail = detail });

        static void AddAssets(List<Entry> target, string filter, string category, params string[] roots)
        {
            var folders = roots.Where(AssetDatabase.IsValidFolder).ToArray();
            if (folders.Length == 0) return;
            foreach (string guid in AssetDatabase.FindAssets(filter, folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var type = filter.Substring(2);
                var assets = type == nameof(GameObject) ? new[] { AssetDatabase.LoadMainAssetAtPath(path) } : AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var asset in assets)
                    if (asset && asset.GetType().Name == type)
                        Add(target, asset.name, category, asset, path);
            }
        }

        static void Select(Object asset)
        {
            if (!asset) return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        static void MakeCopy(Entry entry)
        {
            string folder = entry.Asset is Material ? "Assets/_Project/Materials/Authored" : entry.Category == "Furniture"
                ? "Assets/_Project/Prefabs/Furniture" : "Assets/_Project/Prefabs/Equipment/" + entry.Category;
            string extension = entry.Asset is Material ? "mat" : "prefab";
            try
            {
                WorkspaceAssetCopies.EnsureFolder(folder);
                string path = EditorUtility.SaveFilePanelInProject("Save editable copy", entry.Name, extension,
                    "The source stays unchanged. Assign your copy to the relevant prefab or Wordbound visual profile.", folder);
                if (string.IsNullOrEmpty(path)) return;
                Select(WorkspaceAssetCopies.Create(entry.Asset, path));
                Invalidate();
            }
            catch (Exception exception) { EditorUtility.DisplayDialog("Copy not created", exception.Message, "OK"); }
        }
    }
}
