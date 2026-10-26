using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class LobbyShopLayoutSetup
    {
        public const string ProfilePath = "Assets/_Project/Resources/UI/LobbyShopLayout.asset";
        public const string PrefabFolder = "Assets/_Project/Prefabs/UI/Shop";
        public const string GraphicFolder = "Assets/_Project/UI/Shop/Graphics";

        [MenuItem("Wreckabulary/Authoring/Create or Verify Saved Shop Layout")]
        public static void CreateOrVerify()
        {
            var typography = AssetDatabase.LoadAssetAtPath<LobbyTypographyProfile>(LobbyTypographySetup.ProfilePath);
            var profile = CreateAt(ProfilePath, PrefabFolder, GraphicFolder, typography);
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
        }

        public static LobbyShopLayout CreateAt(string profilePath, string prefabFolder, string graphicFolder, LobbyTypographyProfile typography)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Create Shop layouts outside Play Mode.");
            CheckPath(profilePath, ".asset");
            CheckFolder(prefabFolder); CheckFolder(graphicFolder);
            var existing = AssetDatabase.LoadMainAssetAtPath(profilePath);
            if (existing)
            {
                if (existing is not LobbyShopLayout saved) throw new IOException("The Shop profile destination is occupied.");
                saved.Validate(); ValidatePersistent(saved.Page); ValidatePersistent(saved.Offer);
                return saved;
            }
            RequireVacant(profilePath);
            if (!typography || !EditorUtility.IsPersistent(typography) || !typography.TryValidate(out _))
                throw new InvalidOperationException("Create and validate saved Lobby typography before the Shop layout.");
            string pagePath = prefabFolder + "/ShopPage.prefab", offerPath = prefabFolder + "/ShopOfferCard.prefab";
            var page = Existing<LobbyShopPageView>(pagePath);
            var offer = Existing<LobbyShopOfferView>(offerPath);
            WorkspaceAssetCopies.EnsureFolder(prefabFolder);
            WorkspaceAssetCopies.EnsureFolder(graphicFolder);
            WorkspaceAssetCopies.EnsureFolder(Path.GetDirectoryName(profilePath).Replace('\\', '/'));
            var graphics = new Dictionary<Sprite, Sprite>();
            if (!page) page = SaveTemplate(pagePath, () => BuildPage(typography), graphicFolder, graphics);
            if (!offer) offer = SaveTemplate(offerPath, () => BuildOffer(typography), graphicFolder, graphics);
            var profile = ScriptableObject.CreateInstance<LobbyShopLayout>();
            try
            {
                profile.Page = page; profile.Offer = offer;
                profile.Validate(); ValidatePersistent(page); ValidatePersistent(offer);
                RequireVacant(profilePath);
                AssetDatabase.CreateAsset(profile, profilePath);
                AssetDatabase.SaveAssetIfDirty(profile);
                return profile;
            }
            catch { if (!AssetDatabase.Contains(profile)) Object.DestroyImmediate(profile); throw; }
        }

        static T Existing<T>(string path) where T : Component
        {
            CheckPath(path, ".prefab");
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (!existing) { RequireVacant(path); return null; }
            if (existing is not GameObject root || !root.TryGetComponent<T>(out var view))
                throw new IOException("Existing Shop prefab needs its typed view: " + path);
            if (view is LobbyShopPageView page) page.Validate();
            if (view is LobbyShopOfferView offer) offer.Validate();
            ValidatePersistent(view);
            return view;
        }

        static T SaveTemplate<T>(string path, Func<T> build, string folder, Dictionary<Sprite, Sprite> graphics) where T : Component
        {
            var previous = SceneManager.GetActiveScene();
            const string template = "Assets/_Project/Scenes/WordboundGameplay.unity";
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(template)) throw new IOException("The saved scene template is unavailable.");
            string temporary = Path.GetDirectoryName(path).Replace('\\', '/') + "/ShopLayoutEditing-" + Guid.NewGuid().ToString("N") + ".unity";
            RequireVacant(temporary);
            bool copied = false;
            Scene scene = default;
            try
            {
                copied = AssetDatabase.CopyAsset(template, temporary);
                if (!copied) throw new IOException("Could not create the isolated saved Shop authoring scene.");
                scene = EditorSceneManager.OpenScene(temporary, OpenSceneMode.Additive);
                if (!scene.IsValid() || !scene.isLoaded || scene.path != temporary || !SceneManager.SetActiveScene(scene))
                    throw new InvalidOperationException("Could not isolate Shop template creation.");
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                var view = build();
                foreach (var image in view.GetComponentsInChildren<Image>(true))
                    if (image.sprite) image.sprite = PersistSprite(image.sprite, folder, graphics);
                if (view is LobbyShopOfferView offer)
                {
                    offer.Coin = PersistSprite(offer.Coin, folder, graphics);
                    offer.Check = PersistSprite(offer.Check, folder, graphics);
                    offer.Validate();
                }
                if (view is LobbyShopPageView page) page.Validate();
                RequireVacant(path);
                var prefab = PrefabUtility.SaveAsPrefabAsset(view.gameObject, path);
                if (!prefab) throw new IOException("Shop prefab was not saved: " + path);
                var saved = prefab.GetComponent<T>();
                ValidatePersistent(saved);
                return saved;
            }
            finally
            {
                if (!scene.IsValid()) scene = SceneManager.GetSceneByPath(temporary);
                try
                {
                    if (scene.IsValid() && scene.isLoaded && !EditorSceneManager.CloseScene(scene, true))
                        throw new IOException("The owned Shop authoring scene could not close: " + temporary);
                }
                finally
                {
                    if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                    if (copied && (!scene.IsValid() || !scene.isLoaded) && !AssetDatabase.DeleteAsset(temporary))
                        throw new IOException("The owned temporary Shop scene remains for recovery: " + temporary);
                }
            }
        }

        static Sprite PersistSprite(Sprite source, string folder, Dictionary<Sprite, Sprite> cache)
        {
            if (!source) throw new InvalidOperationException("A generated Shop sprite is missing.");
            if (EditorUtility.IsPersistent(source)) return source;
            if (cache.TryGetValue(source, out var cached)) return cached;
            if (!source.texture || source.texture.width > 512 || source.texture.height > 512 || source.packed)
                throw new InvalidOperationException("Generated Shop graphics must be unpacked sprites at most 512 pixels per side.");
            Texture2D copy = null;
            Sprite result = null;
            try
            {
                copy = ReadableCopy(source.texture);
                string key = GraphicIdentity(source, copy);
                string path = folder + "/" + key + ".asset";
                var existing = AssetDatabase.LoadMainAssetAtPath(path);
                if (existing)
                {
                    var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().SingleOrDefault();
                    if (existing is not Texture2D texture || !sprite || sprite.texture != texture ||
                        !texture.isReadable || GraphicIdentity(sprite, texture) != key)
                        throw new IOException("Existing Shop graphic does not match its source identity: " + path);
                    cache[source] = sprite;
                    return sprite;
                }
                RequireVacant(path);
                AssetDatabase.CreateAsset(copy, path);
                result = Sprite.Create(copy, source.rect, new Vector2(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height), source.pixelsPerUnit, 0,
                    SpriteMeshType.FullRect, source.border);
                result.name = source.name;
                AssetDatabase.AddObjectToAsset(result, copy);
                EditorUtility.SetDirty(copy);
                AssetDatabase.SaveAssetIfDirty(copy);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var loaded = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().SingleOrDefault();
                if (!loaded || !EditorUtility.IsPersistent(loaded.texture) || !loaded.texture.isReadable ||
                    GraphicIdentity(loaded, loaded.texture) != key)
                    throw new IOException("Shop graphic failed pixel/metadata persistence: " + path);
                cache[source] = loaded;
                return loaded;
            }
            finally
            {
                if (result && !AssetDatabase.Contains(result)) Object.DestroyImmediate(result);
                if (copy && !AssetDatabase.Contains(copy)) Object.DestroyImmediate(copy);
            }
        }

        static Texture2D ReadableCopy(Texture2D source)
        {
            if (source.format != TextureFormat.RGBA32 || source.activeMipmapLimit != 0)
                throw new InvalidOperationException("Generated Shop graphics require RGBA32 with no active mipmap limit.");
            if (!source.isReadable && (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null ||
                (SystemInfo.copyTextureSupport & CopyTextureSupport.TextureToRT) == 0))
                throw new InvalidOperationException("Saving unreadable Shop graphics requires a graphics-enabled editor with TextureToRT copy support.");
            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, source.mipmapCount, !source.isDataSRGB)
            {
                name = source.name, hideFlags = HideFlags.None,
                filterMode = source.filterMode, wrapModeU = source.wrapModeU, wrapModeV = source.wrapModeV,
                wrapModeW = source.wrapModeW, anisoLevel = source.anisoLevel, mipMapBias = source.mipMapBias
            };
            try
            {
                if (copy.graphicsFormat != source.graphicsFormat || copy.isDataSRGB != source.isDataSRGB)
                    throw new InvalidOperationException("Shop graphics must retain their exact GPU format and stored color encoding.");
                for (int mip = 0; mip < source.mipmapCount; mip++)
                    copy.SetPixels32(source.isReadable ? source.GetPixels32(mip) : ReadGpuMip(source, mip), mip);
                copy.Apply(false, false);
                return copy;
            }
            catch { Object.DestroyImmediate(copy); throw; }
        }

        static Color32[] ReadGpuMip(Texture2D source, int mip)
        {
            int width = Mathf.Max(1, source.width >> mip), height = Mathf.Max(1, source.height >> mip);
            var previous = RenderTexture.active;
            RenderTexture temporary = null;
            Texture2D readback = null;
            try
            {
                var descriptor = new RenderTextureDescriptor(width, height)
                {
                    graphicsFormat = source.graphicsFormat, depthBufferBits = 0, msaaSamples = 1,
                    useMipMap = false, autoGenerateMips = false
                };
                temporary = RenderTexture.GetTemporary(descriptor);
                PrepareReadbackTarget(temporary, source, mip);
                readback = new Texture2D(width, height, TextureFormat.RGBA32, false, !source.isDataSRGB);
                if (readback.graphicsFormat != temporary.graphicsFormat)
                    throw new InvalidOperationException($"Shop readback format changed the source encoding. " +
                        $"Expected={source.graphicsFormat}, target={temporary.graphicsFormat}, readback={readback.graphicsFormat}, " +
                        $"size={width}x{height}, mip={mip}, IsCreated={temporary.IsCreated()}, backend={SystemInfo.graphicsDeviceType}.");
                Graphics.CopyTexture(source, 0, mip, temporary, 0, 0);
                RenderTexture.active = temporary;
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                readback.Apply(false, false);
                return readback.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                if (temporary) RenderTexture.ReleaseTemporary(temporary);
                if (readback) Object.DestroyImmediate(readback);
            }
        }

        static void PrepareReadbackTarget(RenderTexture target, Texture2D source, int mip)
        {
            int width = Mathf.Max(1, source.width >> mip), height = Mathf.Max(1, source.height >> mip);
            if (!target)
                throw new InvalidOperationException($"Shop readback target is missing. Expected={source.graphicsFormat} {width}x{height}, " +
                    $"mip={mip}, IsCreated=false, backend={SystemInfo.graphicsDeviceType}.");
            bool wasCreated = target.IsCreated();
            bool createSucceeded = wasCreated || target.Create();
            if (!createSucceeded || !target.IsCreated() || target.graphicsFormat != source.graphicsFormat ||
                target.width != width || target.height != height || target.antiAliasing != 1 ||
                target.dimension != TextureDimension.Tex2D || target.volumeDepth != 1)
                throw new InvalidOperationException($"The editor cannot create an exact-format Shop graphic readback target. " +
                    $"Expected={source.graphicsFormat} {width}x{height}, actual={target.graphicsFormat} {target.width}x{target.height}, " +
                    $"mip={mip}, samples={target.antiAliasing}, dimension={target.dimension}, volumeDepth={target.volumeDepth}, " +
                    $"wasCreated={wasCreated}, CreateSucceeded={createSucceeded}, IsCreated={target.IsCreated()}, " +
                    $"backend={SystemInfo.graphicsDeviceType}.");
        }

        static string GraphicIdentity(Sprite sprite, Texture2D texture)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write("ShopGraphicRGBA-v1");
                writer.Write(sprite.name);
                writer.Write(texture.width); writer.Write(texture.height); writer.Write(texture.mipmapCount);
                writer.Write(texture.isDataSRGB); writer.Write((int)texture.graphicsFormat);
                writer.Write((int)texture.filterMode); writer.Write((int)texture.wrapModeU);
                writer.Write((int)texture.wrapModeV); writer.Write((int)texture.wrapModeW);
                writer.Write(texture.anisoLevel); writer.Write(texture.mipMapBias);
                writer.Write(sprite.rect.x); writer.Write(sprite.rect.y); writer.Write(sprite.rect.width); writer.Write(sprite.rect.height);
                writer.Write(sprite.border.x); writer.Write(sprite.border.y); writer.Write(sprite.border.z); writer.Write(sprite.border.w);
                writer.Write(sprite.pivot.x); writer.Write(sprite.pivot.y); writer.Write(sprite.pixelsPerUnit);
                for (int mip = 0; mip < texture.mipmapCount; mip++)
                    foreach (var pixel in texture.GetPixels32(mip))
                    {
                        writer.Write(pixel.r); writer.Write(pixel.g); writer.Write(pixel.b); writer.Write(pixel.a);
                    }
            }
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
        }

        public static void ValidatePersistent(Component root)
        {
            if (!root || !EditorUtility.IsPersistent(root)) throw new InvalidOperationException("Shop views must be saved prefab assets.");
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                if (image.type == Image.Type.Sliced && !image.sprite || image.sprite &&
                    (!EditorUtility.IsPersistent(image.sprite) || !EditorUtility.IsPersistent(image.sprite.texture)))
                    throw new InvalidOperationException("Shop images need saved sprite and texture dependencies.");
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (!text.font || !text.fontSharedMaterial || !EditorUtility.IsPersistent(text.font) ||
                    !EditorUtility.IsPersistent(text.fontSharedMaterial) || !text.font.atlasTextures.All(EditorUtility.IsPersistent))
                    throw new InvalidOperationException("Shop text needs saved font, atlas and material dependencies.");
            if (root is LobbyShopOfferView offer && (!EditorUtility.IsPersistent(offer.Coin) || !EditorUtility.IsPersistent(offer.Check)))
                throw new InvalidOperationException("Shop offer state icons must be persistent.");
        }

        static LobbyShopPageView BuildPage(LobbyTypographyProfile typography)
        {
            var root = new GameObject("Shop page layout", typeof(RectTransform));
            ((RectTransform)root.transform).Fill();
            var view = root.AddComponent<LobbyShopPageView>();
            var panel = LobbyKit.Rect(root.transform, "Panel").Place(new Vector2(1, 0), Vector2.one,
                new Vector2(-LobbyPage.SideWidth, 0), Vector2.zero);
            view.Panel = panel;
            LobbyKit.PanelFace(panel, 28, 5, 8).raycastTarget = true;
            var title = Text(panel, "Title", "SHOP", typography.Display, 43, LobbyKit.Sun, TextAlignmentOptions.BottomLeft);
            title.characterSpacing = 2;
            title.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(34, -84), new Vector2(-88, -20));
            view.Close = LobbyKit.IconButton(panel, LobbyIcons.Close, "Close page", null);
            view.CloseGlyph = view.Close.GetComponentsInChildren<Image>().Single(image => image.name == "Icon " + LobbyIcons.Close);
            MotionContainer(view.Close);
            ((RectTransform)view.Close.transform).Pin(Vector2.one, new Vector2(-20, -20), new Vector2(48, 48));
            var body = LobbyKit.Rect(panel, "Body").Place(Vector2.zero, Vector2.one, new Vector2(32, 28), new Vector2(-32, -100));
            var tabs = LobbyKit.Row(body, "Shop categories", 8);
            tabs.Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -48), Vector2.zero);
            string[] ids = { ShopPage.Looks, ShopPage.Gear, ShopPage.Colours, ShopPage.Rooms, ShopPage.Extras };
            string[] labels = { "Looks", "Gear", "Colours", "Rooms", "Extras" };
            view.Categories = ids.Select((id, i) => new LobbyShopPageView.CategoryBinding
            {
                Id = id,
                Action = Action(tabs, "Shop category " + id, labels[i], typography, 18)
            }).ToArray();
            foreach (var binding in view.Categories) binding.Action.Size(-1, 48, 1);
            view.Footnote = Text(body, "Footnote", "Select to preview", typography.Body, 16, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
            view.Footnote.enableAutoSizing = true; view.Footnote.fontSizeMin = 13; view.Footnote.fontSizeMax = 16;
            view.Footnote.rectTransform.Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 36));
            var offers = LobbyKit.Rect(body, "Offers").Place(Vector2.zero, Vector2.one, new Vector2(0, 46), new Vector2(0, -62));
            view.Content = LobbyKit.Scroll(offers, "Scroll", 12);
            view.Scroll = view.Content.GetComponentInParent<ScrollRect>();
            view.Content.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(4, 4, 8, 12);
            view.Offers = LobbyKit.Grid(view.Content, "Offers grid", new Vector2(248, 270), 16).GetComponent<GridLayoutGroup>();
            return view;
        }

        static LobbyShopOfferView BuildOffer(LobbyTypographyProfile typography)
        {
            var button = LobbyKit.Button(null, "Shop offer card", Color.white, null, 16, LobbyKit.Line, 2, 3);
            var face = button.Body();
            MotionContainer(button);
            ((RectTransform)button.transform).sizeDelta = new Vector2(248, 270);
            var view = button.gameObject.AddComponent<LobbyShopOfferView>();
            view.Preview = button; view.Face = face;
            view.Outline = LobbyKit.Ring(face, LobbyKit.Cyan, 16, 3);
            view.Outline.enabled = false;
            view.Art = LobbyKit.Rect(face, "Art").Place(new Vector2(0, 1), Vector2.one, new Vector2(10, -166), new Vector2(-10, -10));
            view.Art.Paint(Color.Lerp(LobbyThemes.All[0].Secondary, Color.white, .7f), 12).raycastTarget = false;
            view.Title = Text(face, "Title", "Offer title", typography.Display, 23, LobbyKit.Navy, TextAlignmentOptions.Center);
            view.Title.enableAutoSizing = true; view.Title.fontSizeMin = 18; view.Title.fontSizeMax = 23;
            view.Title.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(10, -204), new Vector2(-10, -172));
            view.Hint = button.gameObject.AddComponent<LobbyHint>();
            view.Action = Action(face, "Offer action", "EQUIP", typography, 21);
            ((RectTransform)view.Action.transform).Place(Vector2.zero, new Vector2(1, 0), new Vector2(16, 10), new Vector2(-16, 54));
            view.Coin = LobbyIcons.Get(LobbyIcons.Coin);
            view.Check = LobbyIcons.Get(LobbyIcons.Check);
            return view;
        }

        static LobbyShopActionView Action(Transform parent, string name, string label, LobbyTypographyProfile typography, float size)
        {
            var button = LobbyKit.Button(parent, name, Color.clear, null, 8);
            var body = button.Body();
            MotionContainer(button);
            var content = LobbyKit.Row(body, "Content", 10).Fill();
            var row = content.GetComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter; row.childForceExpandHeight = false;
            row.padding = new RectOffset(8, 8, 4, 6);
            var view = button.gameObject.AddComponent<LobbyShopActionView>();
            view.Button = button; view.Press = button.GetComponent<LobbyPress>();
            view.Icon = LobbyKit.Icon(content, LobbyIcons.Coin, LobbyKit.Navy).Size(size, size);
            view.Icon.gameObject.SetActive(false);
            view.Label = Text(content, "Text", label, typography.Display, size, LobbyKit.Navy, TextAlignmentOptions.Center);
            view.Label.characterSpacing = .6f;
            view.Label.enableAutoSizing = true;
            view.Label.fontSizeMin = Mathf.Min(size, Mathf.Max(14f, size * .78f)); view.Label.fontSizeMax = size;
            view.Label.Size(-1, size + 10).GetComponent<LayoutElement>().minWidth = 0;
            view.Underline = LobbyKit.Rect(body, "Underline").Place(Vector2.zero, new Vector2(1, 0),
                new Vector2(10, 1), new Vector2(-10, 4)).Paint(LobbyKit.Cyan, 1);
            view.Underline.raycastTarget = false; view.Underline.enabled = false;
            return view;
        }

        static void MotionContainer(Button button)
        {
            var press = button.GetComponent<LobbyPress>();
            var body = press.Body;
            var motion = LobbyKit.Rect(button.transform, "Motion (runtime animation)").Fill();
            body.SetParent(motion, false);
            press.Body = motion;
        }

        static TextMeshProUGUI Text(Transform parent, string name, string value, TMP_FontAsset font, float size, Color colour, TextAlignmentOptions alignment)
        {
            var text = LobbyKit.Rect(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.fontSharedMaterial = font.material;
            text.text = value; text.fontSize = size; text.color = colour; text.alignment = alignment;
            text.raycastTarget = false; text.extraPadding = true;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        static void CheckFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/_Project/", StringComparison.Ordinal) || path.Contains('\\') ||
                path.Split('/').Any(part => string.IsNullOrEmpty(part) || part == "." || part == ".." || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
                throw new ArgumentException("Shop authoring paths must stay inside Assets/_Project.");
        }

        static void CheckPath(string path, string extension)
        {
            CheckFolder(path);
            if (!string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Unexpected Shop asset extension.");
        }

        static void RequireVacant(string path)
        {
            if (File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta") || AssetDatabase.LoadMainAssetAtPath(path))
                throw new IOException("Existing Shop authoring content is preserved; inspect this destination: " + path);
        }
    }
}
