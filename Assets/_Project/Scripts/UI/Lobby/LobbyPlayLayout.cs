using System;
using UnityEngine;

namespace Wreckabulary
{
    [CreateAssetMenu(menuName = "Wreckabulary/Lobby/Play layout", fileName = "LobbyPlayLayout")]
    public sealed class LobbyPlayLayout : ScriptableObject
    {
        public const string ResourcePath = "UI/LobbyPlayLayout";
        public LobbyPlayPageView Page;
        [Tooltip("Responsive maximum for Wordbound solo and map-creator pages. The full house page keeps its authored anchors.")]
        public Vector2 CompactMaximum = new Vector2(1100, 600);

        public void Validate()
        {
            if (!Page) throw new InvalidOperationException("Assign the saved Play frame prefab.");
            if (!LobbyShopLayout.Finite(CompactMaximum) || CompactMaximum.x < 900 || CompactMaximum.x > 1920 ||
                CompactMaximum.y < 580 || CompactMaximum.y > 1080)
                throw new InvalidOperationException("Compact Play size must be finite, 900–1920 wide and 580–1080 high.");
            Page.Validate();
        }
    }
}
