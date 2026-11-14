#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Wreckabulary
{
    public sealed class LobbyTypographyReloadState : ScriptableObject
    {
        [Serializable]
        public struct FontPair
        {
            public TMP_FontAsset Source, Owned;
            public bool Extended;
        }

        [Serializable]
        public struct MaterialPair
        {
            public TMP_FontAsset Font;
            public Material Source, Owned;
        }

        public bool Session;
        public List<FontPair> Fonts = new();
        public List<MaterialPair> Presets = new();
        public List<Material> Materials = new();
    }
}
#endif
