using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary.Art
{
    /// <summary>
    /// Every material the imported art uses, built from <c>Data/Generated/materials.json</c> by
    /// Wreckabulary → Art → Build Material Library. Item materials come in skins named
    /// <c>&lt;family&gt;_Classic</c>, <c>_Candy</c> and <c>_Arcade</c>; <see cref="ForSkin"/> swaps
    /// between them for the held-item look (brief §4), falling back to the standard material.
    /// </summary>
    [CreateAssetMenu(menuName = "Wreckabulary/Material Library", fileName = "MaterialLibrary")]
    public sealed class MaterialLibrary : ScriptableObject
    {
        public const string StandardSkin = "Classic";
        public const string ResourcePath = "MaterialLibrary";

        [SerializeField] List<Material> materials = new List<Material>();

        Dictionary<string, Material> byName;

        public IReadOnlyList<Material> Materials => materials;

        static MaterialLibrary loaded;

        /// <summary>The library in <c>Resources/MaterialLibrary.asset</c>, or null if it hasn't been built.</summary>
        public static MaterialLibrary Load()
        {
            if (loaded == null) loaded = Resources.Load<MaterialLibrary>(ResourcePath);
            return loaded;
        }

        public void Set(IEnumerable<Material> all)
        {
            materials = new List<Material>(all);
            byName = null;
        }

        public Material Find(string materialName)
        {
            if (string.IsNullOrEmpty(materialName)) return null;
            if (byName == null)
            {
                byName = new Dictionary<string, Material>();
                foreach (var m in materials)
                    if (m != null) byName[m.name] = m;
            }
            return byName.TryGetValue(materialName, out var found) ? found : null;
        }

        /// <summary>
        /// The same material in another skin: "wood_light_Classic" in Candy is
        /// "wood_light_Candy". Materials without skins (the avatar's, the house's) and skins
        /// that don't exist give back the material unchanged.
        /// </summary>
        public Material ForSkin(Material material, string skin)
        {
            if (material == null) return null;
            string family = FamilyOf(material.name);
            if (family == null || string.IsNullOrEmpty(skin)) return material;
            return Find(family + "_" + skin) ?? Find(family + "_" + StandardSkin) ?? material;
        }

        /// <summary>Puts every renderer under <paramref name="root"/> into <paramref name="skin"/>.</summary>
        public void ApplySkin(GameObject root, string skin)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < shared.Length; i++)
                {
                    var swapped = ForSkin(shared[i], skin);
                    if (swapped != shared[i])
                    {
                        shared[i] = swapped;
                        changed = true;
                    }
                }
                if (changed) r.sharedMaterials = shared;
            }
        }

        /// <summary>"wood_light_Candy" → "wood_light"; null for a material that has no skins.</summary>
        public static string FamilyOf(string materialName)
        {
            if (materialName == null) return null;
            foreach (string skin in new[] { "_Classic", "_Candy", "_Arcade" })
                if (materialName.EndsWith(skin)) return materialName.Substring(0, materialName.Length - skin.Length);
            return null;
        }
    }
}
