using System;
using UnityEngine;

namespace Wreckabulary
{
    [CreateAssetMenu(menuName = "Wreckabulary/Lobby/Shop layout", fileName = "LobbyShopLayout")]
    public sealed class LobbyShopLayout : ScriptableObject
    {
        public const string ResourcePath = "UI/LobbyShopLayout";
        public LobbyShopPageView Page;
        public LobbyShopOfferView Offer;

        public void Validate()
        {
            if (!Page || !Offer) throw new InvalidOperationException("Assign the saved Shop page and offer prefabs.");
            Page.Validate();
            Offer.Validate();
        }

        public static void Require(Component owner, Component reference, string role)
        {
            if (!reference || !reference.transform.IsChildOf(owner.transform))
                throw new InvalidOperationException(owner.name + " needs its own " + role + " reference.");
        }

        public static void ValidateTree(Component root)
        {
            var nodes = root.GetComponentsInChildren<Transform>(true);
            if (nodes.Length > 256) throw new InvalidOperationException("Shop templates support at most 256 objects each.");
            foreach (var node in nodes)
            {
                var scale = node.localScale;
                if (!Finite(node.localPosition) || !Finite(scale) || scale.x <= 0 || scale.y <= 0 || scale.z <= 0)
                    throw new InvalidOperationException("Shop layout transforms must be finite with positive scale.");
                if (node is RectTransform rect && (!Finite(rect.anchorMin) || !Finite(rect.anchorMax) ||
                    !Finite(rect.offsetMin) || !Finite(rect.offsetMax)))
                    throw new InvalidOperationException("Shop layout anchors and offsets must be finite.");
            }
        }

        public static bool Finite(Vector3 value) => Good(value.x) && Good(value.y) && Good(value.z);
        static bool Good(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
