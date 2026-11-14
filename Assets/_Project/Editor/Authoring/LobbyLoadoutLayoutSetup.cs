using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class LobbyLoadoutLayoutSetup
    {
        public const string ProfilePath = "Assets/_Project/Resources/UI/LobbyLoadoutLayout.asset";
        public const string PrefabFolder = "Assets/_Project/Prefabs/UI/Loadout";

        [MenuItem("Wreckabulary/Authoring/Create or Verify Saved Loadout Layout")]
        public static void CreateOrVerify()
        {
            var seed = AssetDatabase.LoadAssetAtPath<LobbyShopLayout>(LobbyShopLayoutSetup.ProfilePath);
            var profile = CreateAt(ProfilePath, PrefabFolder, seed);
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
        }

        public static LobbyLoadoutLayout CreateAt(string profilePath, string folder, LobbyShopLayout seed)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Create Loadout layouts outside Play Mode.");
            CheckPath(profilePath, ".asset"); CheckFolder(folder);
            var current = AssetDatabase.LoadMainAssetAtPath(profilePath);
            if (current)
            {
                if (current is not LobbyLoadoutLayout existing) throw new IOException("The Loadout profile destination is occupied.");
                Validate(existing); return existing;
            }
            RequireVacant(profilePath);
            if (!seed || !EditorUtility.IsPersistent(seed)) throw new InvalidOperationException("Create the saved Shop layout before seeding Loadout.");
            seed.Validate();
            LobbyShopLayoutSetup.ValidatePersistent(seed.Page); LobbyShopLayoutSetup.ValidatePersistent(seed.Offer);
            string pagePath = folder + "/LoadoutPage.prefab", tilePath = folder + "/RecipeTile.prefab";
            var page = Existing<LobbyLoadoutPageView>(pagePath);
            var tile = Existing<LobbyRecipeTileView>(tilePath);
            WorkspaceAssetCopies.EnsureFolder(folder);
            WorkspaceAssetCopies.EnsureFolder(Path.GetDirectoryName(profilePath).Replace('\\', '/'));
            if (!page) page = CopyTemplate(seed.Page, pagePath, ConvertPage);
            if (!tile) tile = CopyTemplate(seed.Offer, tilePath, ConvertTile);
            var profile = ScriptableObject.CreateInstance<LobbyLoadoutLayout>();
            try
            {
                profile.Page = page; profile.Recipe = tile;
                Validate(profile); RequireVacant(profilePath);
                AssetDatabase.CreateAsset(profile, profilePath);
                AssetDatabase.SaveAssetIfDirty(profile);
                return profile;
            }
            catch { if (!AssetDatabase.Contains(profile)) Object.DestroyImmediate(profile); throw; }
        }

        static void Validate(LobbyLoadoutLayout profile)
        {
            profile.Validate();
            LobbyShopLayoutSetup.ValidatePersistent(profile.Page);
            LobbyShopLayoutSetup.ValidatePersistent(profile.Recipe);
        }

        static T Existing<T>(string path) where T : Component
        {
            CheckPath(path, ".prefab");
            var root = AssetDatabase.LoadMainAssetAtPath(path);
            if (!root) { RequireVacant(path); return null; }
            if (root is not GameObject prefab || !prefab.TryGetComponent<T>(out var view))
                throw new IOException("Existing Loadout destination needs its typed view: " + path);
            if (view is LobbyLoadoutPageView page) page.Validate();
            if (view is LobbyRecipeTileView tile) tile.Validate();
            LobbyShopLayoutSetup.ValidatePersistent(view);
            return view;
        }

        static TTarget CopyTemplate<TSource, TTarget>(TSource source, string path, Func<TSource, TTarget> convert)
            where TSource : Component where TTarget : Component
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (!PrefabUtility.IsPartOfPrefabAsset(source) || string.IsNullOrEmpty(sourcePath))
                throw new InvalidOperationException("The Loadout seed must be a saved Shop prefab.");
            var root = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                if (PrefabUtility.IsPartOfPrefabInstance(root))
                    PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var view = convert(root.GetComponent<TSource>());
                if (view is LobbyLoadoutPageView page) page.Validate();
                if (view is LobbyRecipeTileView tile) tile.Validate();
                RequireVacant(path);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (!prefab) throw new IOException("Loadout prefab was not saved: " + path);
                var saved = prefab.GetComponent<TTarget>();
                LobbyShopLayoutSetup.ValidatePersistent(saved);
                return saved;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static LobbyLoadoutPageView ConvertPage(LobbyShopPageView source)
        {
            if (!source) throw new InvalidOperationException("The copied Shop page is missing its bindings.");
            source.Validate();
            var body = source.Panel.Find("Body") as RectTransform;
            var title = source.Panel.GetComponentsInChildren<TextMeshProUGUI>(true).SingleOrDefault(text => text.name == "Title");
            if (!body || !title || !source.Scroll.transform.IsChildOf(body))
                throw new InvalidOperationException("Loadout seeding requires the Shop template's Body and Title objects; existing Shop assets are preserved.");
            var locker = source.Categories.Single(binding => binding.Id == ShopPage.Looks).Action;
            var recipes = source.Categories.Single(binding => binding.Id == ShopPage.Gear).Action;
            var tabs = locker.transform.parent as RectTransform;
            if (!tabs || recipes.transform.parent != tabs)
                throw new InvalidOperationException("Loadout seeding requires the two seed actions in the same tab row.");
            var view = source.gameObject.AddComponent<LobbyLoadoutPageView>();
            source.gameObject.name = "Loadout page layout";
            view.Panel = source.Panel;
            view.Panel.offsetMin = new Vector2(-LoadoutPage.Width, view.Panel.offsetMin.y);
            title.text = "LOADOUT";
            view.Close = source.Close; view.CloseGlyph = source.CloseGlyph;
            view.CloseIdleInk = source.CloseIdleInk; view.CloseActiveInk = source.CloseActiveInk;
            view.LockerTab = locker; view.RecipesTab = recipes;
            foreach (var binding in source.Categories)
                if (binding.Action != locker && binding.Action != recipes) Object.DestroyImmediate(binding.Action.gameObject);
            tabs.SetParent(view.Panel, false);
            tabs.name = "Tabs";
            tabs.Place(Vector2.one, Vector2.one, new Vector2(-324, -74), new Vector2(-84, -26));
            tabs.GetComponent<HorizontalLayoutGroup>().spacing = 4;
            locker.gameObject.name = "LOCKER"; locker.Label.text = "LOCKER";
            recipes.gameObject.name = "RECIPES"; recipes.Label.text = "RECIPES";
            foreach (var action in new[] { locker, recipes }) action.Size(-1, 48, 1);
            view.LockerBody = LobbyKit.Rect(body, "Locker body").Fill();
            view.RecipesBody = LobbyKit.Rect(body, "Recipes body").Fill();
            var scrollHost = source.Scroll.transform.parent as RectTransform;
            if (!scrollHost || scrollHost == body) throw new InvalidOperationException("The seed scroll host is not isolated.");
            scrollHost.SetParent(view.RecipesBody, false);
            scrollHost.name = "Recipes";
            scrollHost.Place(Vector2.zero, Vector2.one, new Vector2(0, 268), Vector2.zero);
            view.RecipeScroll = source.Scroll; view.RecipeGrid = source.Offers;
            view.RecipeGrid.name = "Grid"; view.RecipeGrid.cellSize = new Vector2(122, 118); view.RecipeGrid.spacing = new Vector2(12, 12);
            source.Content.GetComponent<VerticalLayoutGroup>().spacing = 0;
            view.RecipeDetail = LobbyKit.Rect(view.RecipesBody, "Recipe detail").Place(Vector2.zero, new Vector2(1, 0), new Vector2(0, 56), new Vector2(0, 252));
            view.Finish = Object.Instantiate(locker, view.RecipesBody, false);
            view.Finish.gameObject.name = "Recipe finish"; view.Finish.Label.text = "Finish";
            ((RectTransform)view.Finish.transform).Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 44));
            Object.DestroyImmediate(source.Footnote.gameObject);
            Object.DestroyImmediate(source);
            view.RecipesBody.gameObject.SetActive(false);
            return view;
        }

        static LobbyRecipeTileView ConvertTile(LobbyShopOfferView source)
        {
            if (!source) throw new InvalidOperationException("The copied Shop card is missing its bindings.");
            source.Validate();
            var view = source.gameObject.AddComponent<LobbyRecipeTileView>();
            source.gameObject.name = "Recipe tile";
            ((RectTransform)source.transform).sizeDelta = new Vector2(122, 118);
            view.Button = source.Preview; view.Press = source.Preview.GetComponent<LobbyPress>();
            view.Press.Lift = 4; view.Press.Tilt = 2;
            view.Selection = source.Outline; view.Selection.color = view.SelectedInk; view.Selection.enabled = false;
            var art = source.Art;
            foreach (var image in art.GetComponents<Image>()) Object.DestroyImmediate(image);
            art.Pin(new Vector2(.5f, 1), new Vector2(0, -10), new Vector2(64, 64));
            art.name = "Recipe art";
            view.Icon = art.gameObject.AddComponent<RawImage>(); view.Icon.raycastTarget = false;
            view.Letters = LobbyKit.Row(source.Face, "Letters (runtime contents)", 2);
            view.Letters.Place(Vector2.zero, new Vector2(1, 0), new Vector2(4, 12), new Vector2(-4, 40));
            var row = view.Letters.GetComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter; row.childForceExpandHeight = false;
            Object.DestroyImmediate(source.Title.gameObject);
            Object.DestroyImmediate(source.Action.gameObject);
            Object.DestroyImmediate(source.Hint);
            Object.DestroyImmediate(source);
            return view;
        }

        static void CheckFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/_Project/", StringComparison.Ordinal) || path.Contains('\\') ||
                path.Split('/').Any(part => string.IsNullOrEmpty(part) || part == "." || part == ".." || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
                throw new ArgumentException("Loadout authoring paths must stay inside Assets/_Project.");
        }

        static void CheckPath(string path, string extension)
        {
            CheckFolder(path);
            if (!string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Unexpected Loadout asset extension.");
        }

        static void RequireVacant(string path)
        {
            if (File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta") || AssetDatabase.LoadMainAssetAtPath(path))
                throw new IOException("Existing Loadout authoring content is preserved; inspect this destination: " + path);
        }
    }
}
