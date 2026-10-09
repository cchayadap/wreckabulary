using System;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary.Art
{
    [CreateAssetMenu(menuName = "Wreckabulary/Seasonal Collection")]
    public sealed class SeasonalCollection : ScriptableObject
    {
        public const string WinterPath = "Collections/Winter";
        public string themeId = "winter";
        public string finish = "Winter";
        public string[] recipes = { "SHIELD", "SODA" };
        public string emoteName = "Winter shuffle";
        public AnimationClip emote;
        public GameObject decor;
        public Material[] materials = Array.Empty<Material>();
        static SeasonalCollection winter;
        public static SeasonalCollection Winter => winter ? winter : winter = Resources.Load<SeasonalCollection>(WinterPath);

        public Material FindMaterial(string materialName)
        {
            foreach (var material in materials)
                if (material && material.name == materialName) return material;
            return null;
        }

        // Unity collections extend the native catalogue without changing the shared web data.
        public void Apply(ItemCatalogue catalogue)
        {
            if (catalogue == null || string.IsNullOrEmpty(finish)) return;
            foreach (string word in recipes)
                if (catalogue.TryGet(word, out var item) && item.Enabled && !item.HasSkin(finish))
                    item.Skins.Add(finish);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => winter = null;
    }
}
