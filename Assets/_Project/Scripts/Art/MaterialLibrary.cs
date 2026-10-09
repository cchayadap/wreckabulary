using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary.Art
{
    [CreateAssetMenu(menuName = "Wreckabulary/Material Library", fileName = "MaterialLibrary")]
    public sealed class MaterialLibrary : ScriptableObject
    {
        public const string StandardSkin = "Classic";
        public const string ResourcePath = "MaterialLibrary";

        [SerializeField] List<Material> materials = new List<Material>();

        Dictionary<string, Material> byName;

        public IReadOnlyList<Material> Materials => materials;

        static MaterialLibrary loaded;

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
            return byName.TryGetValue(materialName, out var found) ? found : SeasonalCollection.Winter?.FindMaterial(materialName);
        }

        public Material ForSkin(Material material, string skin)
        {
            if (material == null) return null;
            string family = FamilyOf(material.name);
            if (family == null || string.IsNullOrEmpty(skin)) return material;
            return Find(family + "_" + skin) ?? Find(family + "_" + StandardSkin) ?? material;
        }

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
            TactileMaterials.Refresh(root);
        }

        public static string FamilyOf(string materialName)
        {
            if (materialName == null) return null;
            foreach (string skin in new[] { "_Classic", "_Candy", "_Arcade", "_Winter" })
                if (materialName.EndsWith(skin)) return materialName.Substring(0, materialName.Length - skin.Length);
            return null;
        }
    }
}
