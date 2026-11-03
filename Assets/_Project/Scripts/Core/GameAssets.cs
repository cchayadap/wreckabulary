using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Wreckabulary
{
    [CreateAssetMenu(menuName = "Wreckabulary/Game Assets")]
    public class GameAssets : ScriptableObject
    {
        static GameAssets instance;

        public static GameAssets I
        {
            get
            {
                if (!instance) instance = Resources.Load<GameAssets>("GameAssets");
                return instance;
            }
            set => instance = value;
        }

        [Header("Letters")]
        public TMP_FontAsset font;
        public Mesh blockMesh;
        public Material tileCommon, tileRare, tileLegendary;
        public Color ink = new(0.23f, 0.15f, 0.09f);

        [Header("Objects")]
        [Tooltip("White URP Lit material that tinted copies are made from.")]
        public Material tintBase;
        public Material cardboard;
        [Tooltip("Tinted materials saved as assets by the prototype builder.")]
        public List<Material> tints = new();

        [Header("Prefabs and data")]
        public LetterTile tilePrefab;
        public PlayerController playerPrefab;
        public WordDatabase words;

        [Header("Players")]
        public Color[] playerColors =
        {
            new(0.91f, 0.34f, 0.29f),
            new(0.25f, 0.56f, 0.88f),
            new(0.36f, 0.73f, 0.39f),
            new(0.95f, 0.70f, 0.24f),
        };
        [Tooltip("Sweater initials per player slot. Together they spell WORD.")]
        public string playerInitials = "WORD";

        public static Func<Color, Material> EditorTintFactory;

        readonly Dictionary<Color, Material> runtimeTints = new();

        public Color PlayerColor(int index) => playerColors[index % playerColors.Length];
        public char PlayerInitial(int index) => playerInitials[index % playerInitials.Length];

        public Material Tinted(Color c)
        {
            foreach (var m in tints)
                if (m && Same(m.color, c)) return m;
            if (runtimeTints.TryGetValue(c, out var cached) && cached) return cached;

            if (!Application.isPlaying && EditorTintFactory != null)
            {
                var saved = EditorTintFactory(c);
                ApplyTint(saved, c);
                tints.Add(saved);
                return saved;
            }
            var mat = new Material(tintBase) { color = c };
            ApplyTint(mat, c);
            runtimeTints[c] = mat;
            return mat;
        }

        static void ApplyTint(Material material, Color colour)
        {
            if (!material) return;
            colour.a = 1f;
            material.color = colour;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
        }

        public Material TileMaterial(LetterRarity rarity) => rarity switch
        {
            LetterRarity.Legendary => tileLegendary,
            LetterRarity.Rare => tileRare,
            _ => tileCommon
        };

        static bool Same(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.003f && Mathf.Abs(a.g - b.g) < 0.003f && Mathf.Abs(a.b - b.b) < 0.003f;
    }
}
