using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary.EditorTools
{
    public static class ShopPreviewImporter
    {
        const string Folder = "Assets/_Project/Resources/UI/Shop/Looks";

        [MenuItem("Wreckabulary/Art/Rebuild shop portraits")]
        public static void Rebuild()
        {
            var library = ModelLibrary.Load();
            if (!library || !library.Find("Avatar/Avatar"))
                throw new InvalidOperationException("The original avatar must be imported before shop portraits.");
            var idle = library.FindClip("Avatar/Avatar", "Idle");
            if (!idle) throw new InvalidOperationException("The original idle clip is required.");
            EnsureFolder(Folder);
            foreach (var look in CosmeticBundles.All)
            {
                var preview = new PreviewRenderUtility();
                Texture2D image = null;
                try
                {
                    var holder = new GameObject("Shop portrait " + look.Id);
                    preview.AddSingleGO(holder);
                    var model = ModelVisual.Spawn("Avatar/Avatar", holder.transform);
                    if (!model) throw new InvalidOperationException("Could not instantiate the original avatar.");
                    idle.SampleAnimation(model, 0f);
                    var outfit = look.Apply(GameConfig.Current.Wardrobe.Default, GameConfig.Current.Wardrobe);
                    if (outfit == null) throw new InvalidOperationException("Invalid shop outfit: " + look.Id);
                    PlayerAppearance.Dress(model.GetComponentsInChildren<SkinnedMeshRenderer>(true), outfit);
                    holder.transform.rotation = Quaternion.Euler(0f, 160f, 0f);
                    var renderers = model.GetComponentsInChildren<Renderer>()
                        .Where(renderer => renderer.enabled && !renderer.forceRenderingOff).ToArray();
                    if (renderers.Length == 0) throw new InvalidOperationException("No visible outfit geometry: " + look.Id);
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                    var camera = preview.camera;
                    camera.orthographic = true;
                    camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / .8f) * 1.12f;
                    camera.nearClipPlane = .01f;
                    camera.farClipPlane = 30f;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(.97f, .94f, .85f, 0f);
                    camera.allowHDR = false;
                    camera.transform.position = bounds.center + Vector3.back * 5f;
                    camera.transform.LookAt(bounds.center);
                    preview.lights[0].intensity = 1.15f;
                    preview.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
                    preview.lights[1].intensity = .65f;
                    preview.lights[1].transform.rotation = Quaternion.Euler(340f, 218f, 0f);
                    preview.ambientColor = new Color(.45f, .45f, .45f);
                    preview.BeginStaticPreview(new Rect(0, 0, 256, 320));
                    preview.Render(true);
                    image = preview.EndStaticPreview();
                    if (!image || image.width != 256 || image.height != 320)
                        throw new InvalidOperationException("Invalid portrait render: " + look.Id);
                    string path = Folder + "/" + look.Id + ".png";
                    File.WriteAllBytes(path, image.EncodeToPNG());
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Default;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.maxTextureSize = 512;
                    importer.SaveAndReimport();
                }
                finally
                {
                    if (image) UnityEngine.Object.DestroyImmediate(image);
                    preview.Cleanup();
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("SHOP_PORTRAITS: count=" + CosmeticBundles.All.Count + "; source=original-avatar; width=256; height=320");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
