using System;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary.Art
{
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class ItemLook : MonoBehaviour
    {
        const string ItemPrefix = "Items/";

        [SerializeField] string item;

        static ItemPalette palette;
        static bool loaded;

        public string Item => item;

        public static ItemPalette Palette
        {
            get
            {
                if (loaded) return palette;
                loaded = true;
                var text = Resources.Load<TextAsset>(ItemPalette.ResourcePath);
                palette = text ? ItemPalette.FromJson(text.text, ItemPalette.SourceFile) : null;
                return palette;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Forget()
        {
            palette = null;
            loaded = false;
        }

        public static string ItemOf(string modelKey) =>
            modelKey != null && modelKey.StartsWith(ItemPrefix, StringComparison.Ordinal) ? modelKey.Substring(ItemPrefix.Length) : null;

        public static bool Covers(string itemId) => itemId != null && Palette != null && Palette.Items.ContainsKey(itemId);

        public static ItemLook Attach(GameObject model, string itemId, string skin = MaterialLibrary.StandardSkin)
        {
            if (!model || !Covers(itemId)) return null;
            var look = model.GetComponent<ItemLook>();
            if (!look) look = model.AddComponent<ItemLook>();
            look.hideFlags = HideFlags.HideInInspector;
            look.item = itemId;
            var library = MaterialLibrary.Load();
            if (library) library.ApplySkin(model, skin);
            return look;
        }

        public Material Resolve(MaterialLibrary library, Material current, string skin)
        {
            string family = current ? MaterialLibrary.FamilyOf(current.name) : null;
            var look = Palette;
            if (family == null || look == null) return library.ForSkin(current, skin);
            string source = look.SourceOf(item, family);
            if (skin == MaterialLibrary.StandardSkin && look.TryMap(item, source, out string target))
            {
                var vivid = library.Find(target + "_" + MaterialLibrary.StandardSkin);
                if (vivid) return vivid;
            }
            var original = library.Find(source + "_" + MaterialLibrary.StandardSkin);
            return library.ForSkin(original ? original : current, skin);
        }
    }
}
