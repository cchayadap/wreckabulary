using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class FinishArt
    {
        public const string IconFolder = "Assets/_Project/Resources/UI/Items/Finishes";
        public const string EmblemFolder = "Assets/_Project/Resources/UI/Finishes";
        public const int IconSize = 256, CellSize = 160, CouchSize = 20, CouchScale = 5, RowsPerPage = 12;
        const string ReviewRoot = "BuildNotes/asset-review";
        static readonly Vector3 IconView = new Vector3(-2.3f, 2.3f, 3.6f), KeyLight = new Vector3(3f, 4f, 4f), FillLight = new Vector3(-3f, 3f, 1f);
        static readonly Vector3 PartyView = new Vector3(0f, 30f, -22f);
        static readonly Color CellFill = new Color(.42f, .45f, .48f), FloorFill = new Color(.6f, .42f, .28f);
        static readonly Color32 Empty = new Color32(24, 26, 30, 255);

        public static string IconPath(string word, string finish) => IconFolder + "/" + word + "_" + finish + ".png";
        public static string EmblemPath(string finish) => EmblemFolder + "/" + finish + ".png";

        [MenuItem("Wreckabulary/Art/Finishes/Build materials, emblems and icons")]
        public static void Run()
        {
            Materials();
            Emblems();
            Icons();
            Debug.Log("[Wreckabulary] FINISH_ART_READY: materials, emblems and icons for " + string.Join(", ", Finishes.Ids));
        }

        [MenuItem("Wreckabulary/Art/Finishes/Build materials")]
        public static void Materials()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var library = MaterialLibraryBuilder.Build();
            var winter = SeasonalCollection.Winter;
            if (!winter) throw new InvalidOperationException("Resources/Collections/Winter is missing.");
            var missing = MaterialLibraryBuilder.ReadSeasonal().Select(entry => entry.spec.Name).Where(name => !winter.FindMaterial(name)).ToArray();
            if (missing.Length > 0) throw new InvalidOperationException("The Winter collection does not list " + string.Join(", ", missing) + "; run Build Winter Collection, then this again.");
            AssetDatabase.SaveAssets();
            Debug.Log($"[Wreckabulary] FINISH_MATERIALS: {library.Materials.Count} library materials, {winter.materials.Length} Winter materials");
        }

        [MenuItem("Wreckabulary/Art/Finishes/Import emblems")]
        public static void Emblems()
        {
            foreach (var finish in Finishes.All)
            {
                string path = EmblemPath(finish.Id);
                if (!File.Exists(path)) throw new FileNotFoundException(path + " is missing; run Tools/AssetPipeline/build_finish_emblems.py.");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                ConfigureIcon(path);
            }
        }

        [MenuItem("Wreckabulary/Art/Finishes/Render finish icons")]
        public static void Icons()
        {
            RequireRenderer();
            MaterialLibraryBuilder.EnsureFolder(IconFolder);
            var written = new List<string>();
            using (var studio = new Studio())
                foreach (var item in SkinnedItems())
                    foreach (var finish in Finishes.ForItem(item.Id, item.Skins).Where(f => f != Finishes.Classic))
                    {
                        bool house = HouseItemsSetup.Words.Contains(item.Id) || HouseItemsV2Setup.Words.Contains(item.Id);
                        var pixels = studio.Render(item.Model, finish.Id, IconSize, IconView, Color.clear, true, house ? Fit.Bounds : Fit.Vertices);
                        string path = IconPath(item.Id, finish.Id);
                        File.WriteAllBytes(path, Encode(pixels, IconSize, IconSize, true));
                        written.Add(path);
                    }
            foreach (string path in written)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                ConfigureIcon(path);
            }
            Debug.Log($"[Wreckabulary] FINISH_ICONS: {written.Count} icons in {IconFolder}");
        }

        [MenuItem("Wreckabulary/Art/Finishes/Capture comparison sheets")]
        public static void Comparison()
        {
            RequireRenderer();
            string relative = ReviewRoot + "/" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-finishes";
            string folder = Full(relative);
            if (Directory.Exists(folder)) throw new IOException("Preserve existing sheets: " + folder);
            Directory.CreateDirectory(folder);
            var items = SkinnedItems().ToList();
            var columns = Finishes.All.ToList();
            var rows = new List<string>();
            var pages = new List<string>();
            int couchCell = CouchSize * CouchScale;
            using (var studio = new Studio())
                for (int page = 0; page * RowsPerPage < items.Count; page++)
                {
                    var slice = items.Skip(page * RowsPerPage).Take(RowsPerPage).ToList();
                    var close = new Color32[columns.Count * CellSize * slice.Count * CellSize];
                    var couch = new Color32[columns.Count * couchCell * slice.Count * couchCell];
                    for (int r = 0; r < slice.Count; r++)
                    {
                        rows.Add(slice[r].Id);
                        var offered = Finishes.ForItem(slice[r].Id, slice[r].Skins).ToList();
                        for (int c = 0; c < columns.Count; c++)
                        {
                            bool listed = offered.Contains(columns[c]);
                            var near = listed ? studio.Render(slice[r].Model, columns[c].Id, CellSize, IconView, CellFill, false) : Fill(CellSize, Empty);
                            var far = listed ? Upscale(studio.Render(slice[r].Model, columns[c].Id, CouchSize, PartyView, FloorFill, false), CouchSize, CouchScale) : Fill(couchCell, Empty);
                            Blit(near, CellSize, close, columns.Count * CellSize, c * CellSize, (slice.Count - 1 - r) * CellSize);
                            Blit(far, couchCell, couch, columns.Count * couchCell, c * couchCell, (slice.Count - 1 - r) * couchCell);
                        }
                    }
                    string closeName = $"comparison-{page + 1}.png", couchName = $"couch-{page + 1}.png";
                    File.WriteAllBytes(Path.Combine(folder, closeName), Encode(close, columns.Count * CellSize, slice.Count * CellSize, false));
                    File.WriteAllBytes(Path.Combine(folder, couchName), Encode(couch, columns.Count * couchCell, slice.Count * couchCell, false));
                    pages.Add(closeName); pages.Add(couchName);
                }
            var index = new SheetIndex
            {
                utc = DateTime.UtcNow.ToString("O"), folder = relative, columns = columns.Select(f => f.Id).ToArray(), rows = rows.ToArray(), pages = pages.ToArray(),
                rowsPerPage = RowsPerPage, cell = CellSize, couchCell = CouchSize, couchScale = CouchScale,
                scope = "Rows are items (alphabetical), columns are finishes. comparison-N: three-quarter icon view at 160 px on grey. couch-N: party-camera view rendered at 20 px " +
                    "(about a held item at couch distance) and scaled 5x with nearest filtering, on a wood-floor colour. Dark cells: the item does not list that finish. No art approval is implied."
            };
            File.WriteAllText(Path.Combine(folder, "index.json"), JsonUtility.ToJson(index, true));
            Debug.Log($"[Wreckabulary] FINISH_SHEETS: {rows.Count} items x {columns.Count} finishes in {relative}");
        }

        [Serializable] sealed class SheetIndex
        {
            public string utc, folder, scope; public string[] columns, rows, pages; public int rowsPerPage, cell, couchCell, couchScale;
        }

        public static IEnumerable<ItemDefinition> SkinnedItems()
        {
            var models = ModelLibrary.Load();
            return GameConfig.Current.Items.All
                .Where(item => !string.IsNullOrEmpty(item.Model) && item.Id != "BELT" && models && models.Find(item.Model))
                .OrderBy(item => item.Id, StringComparer.Ordinal);
        }

        enum Fit { Bounds, Vertices }

        sealed class Studio : IDisposable
        {
            readonly PreviewRenderUtility preview = new PreviewRenderUtility();
            readonly bool asyncShaders = EditorSettings.asyncShaderCompilation, ssao = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");
            readonly RenderTexture active = RenderTexture.active;

            public Studio()
            {
                EditorSettings.asyncShaderCompilation = false;
                Shader.DisableKeyword("_SCREEN_SPACE_OCCLUSION");
            }

            public Color32[] Render(string key, string finish, int size, Vector3 view, Color background, bool straightAlpha, Fit fit = Fit.Bounds)
            {
                GameObject anchor = null; bool begun = false;
                try
                {
                    anchor = EditorUtility.CreateGameObjectWithHideFlags("Finish art " + key, HideFlags.HideAndDontSave);
                    SceneManager.MoveGameObjectToScene(anchor, preview.camera.gameObject.scene);
                    var model = ModelVisual.Spawn(key, anchor.transform, finish);
                    if (!model) throw new InvalidOperationException("No model for " + key);
                    var renderers = model.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
                    if (renderers.Length == 0) throw new InvalidOperationException(key + " has no visible renderers.");
                    var bounds = renderers[0].bounds; foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
                    var camera = preview.camera;
                    float distance = bounds.size.magnitude * 2f + 1f;
                    camera.orthographic = true; camera.aspect = 1f; camera.allowHDR = false; camera.allowMSAA = false;
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = background;
                    camera.nearClipPlane = .01f; camera.farClipPlane = distance * 2f;
                    camera.transform.position = bounds.center + view.normalized * distance;
                    camera.transform.LookAt(bounds.center);
                    var points = fit == Fit.Vertices ? Vertices(model).Select(camera.transform.InverseTransformPoint).ToList() : new List<Vector3>();
                    camera.orthographicSize = points.Count > 0
                        ? .59f * Mathf.Max(points.Max(p => p.x) - points.Min(p => p.x), points.Max(p => p.y) - points.Min(p => p.y))
                        : 1.08f * Enumerable.Range(0, 8)
                            .Select(i => camera.transform.InverseTransformPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))))
                            .Max(point => Mathf.Max(Mathf.Abs(point.x), Mathf.Abs(point.y)));
                    preview.ambientColor = new Color(.45f, .48f, .51f);
                    preview.lights[0].color = new Color(1f, .86f, .72f); preview.lights[0].intensity = 1.1f; preview.lights[0].shadows = LightShadows.None;
                    preview.lights[0].transform.rotation = Quaternion.LookRotation(-KeyLight);
                    preview.lights[1].color = new Color(.72f, .84f, 1f); preview.lights[1].intensity = .6f; preview.lights[1].shadows = LightShadows.None;
                    preview.lights[1].transform.rotation = Quaternion.LookRotation(-FillLight);
                    preview.BeginStaticPreview(new Rect(0, 0, size, size)); begun = true;
                    preview.Render(true); preview.Render(true);
                    var pixels = Read(camera.targetTexture, size);
                    begun = false; Object.DestroyImmediate(preview.EndStaticPreview());
                    if (straightAlpha) Straighten(pixels);
                    return pixels;
                }
                finally
                {
                    if (begun) Object.DestroyImmediate(preview.EndStaticPreview());
                    if (anchor) { TactileMaterials.Release(anchor); Object.DestroyImmediate(anchor); }
                }
            }

            public void Dispose()
            {
                preview.Cleanup();
                RenderTexture.active = active;
                EditorSettings.asyncShaderCompilation = asyncShaders;
                if (ssao) Shader.EnableKeyword("_SCREEN_SPACE_OCCLUSION");
                TactileMaterials.CollectUnused();
            }
        }

        static IEnumerable<Vector3> Vertices(GameObject model)
        {
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh && filter.GetComponent<Renderer>() is Renderer renderer && renderer.enabled)
                    foreach (var vertex in filter.sharedMesh.vertices) yield return filter.transform.TransformPoint(vertex);
        }

        static Color32[] Read(RenderTexture source, int size)
        {
            var active = RenderTexture.active; bool srgbWrite = GL.sRGBWrite;
            var target = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { hideFlags = HideFlags.HideAndDontSave };
            Texture2D image = null;
            try
            {
                GL.sRGBWrite = true;
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                image = new Texture2D(size, size, TextureFormat.RGBA32, false, false) { hideFlags = HideFlags.HideAndDontSave };
                image.ReadPixels(new Rect(0, 0, size, size), 0, 0, false);
                image.Apply(false);
                return image.GetPixels32();
            }
            finally
            {
                RenderTexture.active = active; GL.sRGBWrite = srgbWrite;
                target.Release(); Object.DestroyImmediate(target);
                if (image) Object.DestroyImmediate(image);
            }
        }

        static void Straighten(Color32[] pixels)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                if (p.a == 255) continue;
                pixels[i] = p.a == 0 ? default : new Color32(Straight(p.r, p.a), Straight(p.g, p.a), Straight(p.b, p.a), p.a);
            }
        }

        static byte Straight(byte value, byte alpha) =>
            (byte)Mathf.RoundToInt(Mathf.Clamp01(Mathf.LinearToGammaSpace(Mathf.GammaToLinearSpace(value / 255f) * 255f / alpha)) * 255f);

        static Color32[] Fill(int size, Color32 colour) => Enumerable.Repeat(colour, size * size).ToArray();

        static Color32[] Upscale(Color32[] source, int size, int scale)
        {
            int big = size * scale;
            var result = new Color32[big * big];
            for (int y = 0; y < big; y++)
                for (int x = 0; x < big; x++)
                    result[y * big + x] = source[y / scale * size + x / scale];
            return result;
        }

        static void Blit(Color32[] cell, int size, Color32[] sheet, int width, int x, int y)
        {
            for (int row = 0; row < size; row++) Array.Copy(cell, row * size, sheet, (y + row) * width + x, size);
        }

        static byte[] Encode(Color32[] pixels, int width, int height, bool alpha)
        {
            var texture = new Texture2D(width, height, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false, false) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false);
                return texture.EncodeToPNG();
            }
            finally { Object.DestroyImmediate(texture); }
        }

        public static void ConfigureIcon(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (IsIcon(importer)) return;
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.spriteImportMode = SpriteImportMode.None;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 1;
            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        public static bool IsIcon(TextureImporter importer) => HouseItemsSetup.HasIconSettings(importer);

        static void RequireRenderer()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Render finish art in Edit Mode.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !GraphicsSettings.currentRenderPipeline || QualitySettings.activeColorSpace != ColorSpace.Linear)
                throw new InvalidOperationException("Finish art needs a graphics device, URP and linear colour; do not run with -nographics.");
        }

        static string Full(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
    }
}
