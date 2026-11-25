using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

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
            var exact = Find(family + "_" + skin);
            if (exact) return exact;
            if (Finishes.Find(skin) != null) ReportMissing(family + "_" + skin);
            return Find(family + "_" + StandardSkin) ?? material;
        }

        public bool Resolves(Material material, string skin)
        {
            string family = material ? FamilyOf(material.name) : null;
            return family != null && Finishes.Find(skin) != null && Find(family + "_" + skin);
        }

        static readonly HashSet<string> reported = new HashSet<string>();

        public static IReadOnlyCollection<string> MissingFinishes => reported;

        static void ReportMissing(string materialName)
        {
            if (reported.Add(materialName))
                Debug.LogWarning($"[Wreckabulary] The finish material {materialName} is missing, so this part keeps its Classic look.");
        }

        public void ApplySkin(GameObject root, string skin)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                var look = r.GetComponentInParent<ItemLook>(true);
                bool changed = false;
                for (int i = 0; i < shared.Length; i++)
                {
                    var swapped = look ? look.Resolve(this, shared[i], skin) : ForSkin(shared[i], skin);
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

        public static string FamilyOf(string materialName) => Finishes.FamilyOf(materialName);
    }
}
