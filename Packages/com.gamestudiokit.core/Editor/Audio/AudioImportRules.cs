// Game Studio Kit (MIT). Folder-based audio import rules (ARCHITECTURE §3.10 unity-audio.md). A clip's category comes
// from a folder segment of its path (Music, Ambience, SFX, UI, VO…); each category has load type, codec, quality, mono
// and preload settings suited to it. Engine-free so the kit's tests check the classification under plain .NET.
using System;
using System.Collections.Generic;

namespace GameStudioKit.Editor.Audio
{
    public sealed class AudioRule
    {
        public string Category;

        /// <summary>streaming | compressed_in_memory | decompress_on_load.</summary>
        public string LoadType;

        /// <summary>vorbis | adpcm | pcm.</summary>
        public string Format;
        public float Quality = 0.7f;
        public bool ForceMono;
        public bool Preload = true;
        public bool LoadInBackground;
    }

    public static class AudioImportRules
    {
        static readonly Dictionary<string, string> SegmentToCategory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "music", "music" }, { "soundtrack", "music" }, { "bgm", "music" },
            { "ambience", "ambience" }, { "ambient", "ambience" }, { "amb", "ambience" },
            { "sfx", "sfx" }, { "sounds", "sfx" }, { "footsteps", "sfx" },
            { "ui", "ui" },
            { "vo", "vo" }, { "voice", "vo" }, { "dialogue", "vo" }, { "dialog", "vo" },
        };

        /// <summary>The category of the deepest matching folder segment, or null when no segment matches.</summary>
        public static string CategoryFor(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            var parts = assetPath.Replace('\\', '/').Split('/');
            for (int i = parts.Length - 2; i >= 0; i--)
                if (SegmentToCategory.TryGetValue(parts[i], out string cat)) return cat;
            return null;
        }

        /// <summary>The rule for a category (music and ambience stream; short SFX and UI decompress on load; VO mono).</summary>
        public static AudioRule RuleFor(string category)
        {
            switch (category)
            {
                case "music":
                    return new AudioRule { Category = category, LoadType = "streaming", Format = "vorbis", Quality = 0.7f, Preload = false, LoadInBackground = true };
                case "ambience":
                    return new AudioRule { Category = category, LoadType = "streaming", Format = "vorbis", Quality = 0.6f, Preload = false, LoadInBackground = true };
                case "sfx":
                    return new AudioRule { Category = category, LoadType = "decompress_on_load", Format = "adpcm", Quality = 1f, Preload = true };
                case "ui":
                    return new AudioRule { Category = category, LoadType = "decompress_on_load", Format = "adpcm", Quality = 1f, ForceMono = true, Preload = true };
                case "vo":
                    return new AudioRule { Category = category, LoadType = "compressed_in_memory", Format = "vorbis", Quality = 0.6f, ForceMono = true, Preload = false, LoadInBackground = true };
                default:
                    return null;
            }
        }
    }
}
