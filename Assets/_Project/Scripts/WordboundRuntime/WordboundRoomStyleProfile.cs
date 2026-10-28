using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wreckabulary.WordboundRuntime
{
    [Serializable]
    public sealed class WordboundRoomPalette
    {
        public string LayerId = "Packing";
        public Color Floor = new Color(.64f, .43f, .28f);
        public Color Wall = new Color(.23f, .42f, .40f);
        public Color Ceiling = new Color(.90f, .86f, .75f);
        public Color Frame = new Color(.88f, .69f, .38f);
        public Color Inset = new Color(.37f, .65f, .73f);
        public Color Accent = new Color(.93f, .62f, .22f);
        public Material FloorMaterial, WallMaterial, CeilingMaterial, FrameMaterial, InsetMaterial, AccentMaterial;
    }

    [CreateAssetMenu(menuName = "Wreckabulary/Wordbound/Room architecture", fileName = "RoomArchitecture")]
    public sealed class WordboundRoomStyleProfile : ScriptableObject
    {
        public const string ResourcePath = "Wordbound/RoomArchitecture";
        [Tooltip("Visual architecture only. Does not add or change collision, map rules, furniture or online manifests.")]
        public bool Enabled = true;
        [Range(0, 3)] public int MaximumInsetsPerWallPiece = 0;
        [Range(0, 512)] public int MaximumDecoratedWallPieces = 256;
        [Range(2f, 6f)] public float InsetWidth = 4.6f;
        [Range(1.5f, 4.5f)] public float InsetHeight = 3.2f;
        [Range(.1f, .8f)] public float FrameWidth = .20f;
        [Tooltip("Optional wall illustration, used only when insets are enabled. Its source file is never changed.")]
        public Texture2D WallArtwork;
        public List<WordboundRoomPalette> Palettes = Defaults();

        public static List<WordboundRoomPalette> Defaults() => new List<WordboundRoomPalette>
        {
            new WordboundRoomPalette(),
            new WordboundRoomPalette { LayerId = "Kitchen", Floor = Hex(0xc89f75), Wall = Hex(0xb95f49),
                Ceiling = Hex(0xf0e3cc), Frame = Hex(0xf5d9a7), Inset = Hex(0x83c3bb), Accent = Hex(0x34675c) },
            new WordboundRoomPalette { LayerId = "Attic", Floor = Hex(0x87634e), Wall = Hex(0x505679),
                Ceiling = Hex(0xd2bda2), Frame = Hex(0xc99a55), Inset = Hex(0xa8c4d4), Accent = Hex(0xe1b55e) },
            new WordboundRoomPalette { LayerId = "Rooftop", Floor = Hex(0xb7aa89), Wall = Hex(0x66856b),
                Ceiling = Hex(0xe6e5ce), Frame = Hex(0xe9ca83), Inset = Hex(0x85bfd4), Accent = Hex(0xc8794e) }
        };

        public WordboundRoomPalette ForLayer(string id) => Palettes.FirstOrDefault(p => p.LayerId == id) ?? Palettes[0];

        public void Validate()
        {
            if (Palettes == null || Palettes.Count == 0 || Palettes.Count > 16 ||
                Palettes.Any(p => p == null || string.IsNullOrWhiteSpace(p.LayerId)) ||
                Palettes.Select(p => p.LayerId).Distinct(StringComparer.Ordinal).Count() != Palettes.Count)
                throw new ArgumentException("Room architecture needs 1–16 distinct layer palettes.");
            if (MaximumInsetsPerWallPiece < 0 || MaximumInsetsPerWallPiece > 3 ||
                MaximumDecoratedWallPieces < 0 || MaximumDecoratedWallPieces > 512 ||
                !Within(InsetWidth, 2, 6) || !Within(InsetHeight, 1.5f, 4.5f) || !Within(FrameWidth, .1f, .8f))
                throw new ArgumentException("Room architecture dimensions are outside their supported bounds.");
            foreach (var p in Palettes)
                foreach (var c in new[] { p.Floor, p.Wall, p.Ceiling, p.Frame, p.Inset, p.Accent })
                    if (!Within(c.r, 0, 1) || !Within(c.g, 0, 1) || !Within(c.b, 0, 1) || !Within(c.a, 1, 1))
                        throw new ArgumentException("Room palette colours must be finite opaque colours.");
        }

        static bool Within(float value, float min, float max) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
        static Color Hex(uint value) => new Color32((byte)(value >> 16), (byte)(value >> 8), (byte)value, 255);
    }
}
