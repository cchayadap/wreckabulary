using System;
using UnityEngine;

namespace Wreckabulary
{
    [CreateAssetMenu(menuName = "Wreckabulary/Lobby/Play layout", fileName = "LobbyPlayLayout")]
    public sealed class LobbyPlayLayout : ScriptableObject
    {
        public const string ResourcePath = "UI/LobbyPlayLayout";
        public LobbyPlayPageView Page;
        [Header("Mode and map choices")]
        [Range(96, 220)] public float ModeMinimumWidth = 104;
        [Range(720, 1440)] public float ModeMaximumWidth = 1160;
        [Range(44, 80)] public float ModeHeight = 52;
        [Range(220, 420)] public float MapMinimumWidth = 264;
        [Range(300, 520)] public float MapMinimumHeight = 340;
        [Range(2, 5)] public int MapColumns = 3;
        [Header("Solo map previews")]
        [Tooltip("Actual Lost Address course preview. Leave empty to use the original object artwork.")]
        public Sprite LostAddressPreview;
        [Tooltip("Actual Parcel Hall course preview. Leave empty to use the original object artwork.")]
        public Sprite ParcelHallPreview;
        [Tooltip("Actual Parcel Depot course preview. Leave empty to show a neutral map symbol.")]
        public Sprite ParcelServicePreview;
        [Tooltip("Actual installed Combat Arena capture for online FFA. Empty keeps the truthful course name without substituted artwork.")]
        public Sprite CombatArenaPreview;
        [Range(320, 720), Tooltip("Preferred minimum Solo card height. Actual previews fit their full width; short windows limit the card to the scroll viewport.")]
        public float SoloMapMinimumHeight = 480;
        [Tooltip("Responsive maximum for Wordbound solo and map-creator pages. The full house page keeps its authored anchors.")]
        public Vector2 CompactMaximum = new Vector2(1100, 600);

        public void Validate()
        {
            if (!Page) throw new InvalidOperationException("Assign the saved Play frame prefab.");
            if (!Within(ModeMinimumWidth, 96, 220) || !Within(ModeMaximumWidth, 720, 1440) || !Within(ModeHeight, 44, 80) ||
                !Within(MapMinimumWidth, 220, 420) || !Within(MapMinimumHeight, 300, 520) || MapColumns < 2 || MapColumns > 5)
                throw new InvalidOperationException("Mode/map dimensions must be finite and within the supported designer ranges.");
            if (float.IsNaN(SoloMapMinimumHeight) || float.IsInfinity(SoloMapMinimumHeight) || SoloMapMinimumHeight < 320 || SoloMapMinimumHeight > 720)
                throw new InvalidOperationException("Solo map minimum height must be finite and between 320 and 720.");
            if (!LobbyShopLayout.Finite(CompactMaximum) || CompactMaximum.x < 900 || CompactMaximum.x > 1920 ||
                CompactMaximum.y < 580 || CompactMaximum.y > 1080)
                throw new InvalidOperationException("Compact Play size must be finite, 900–1920 wide and 580–1080 high.");
            Page.Validate();
        }
        static bool Within(float value, float minimum, float maximum) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;
    }
}
