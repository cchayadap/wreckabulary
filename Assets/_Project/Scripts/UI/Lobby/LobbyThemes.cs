using System;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class LobbyTheme
    {
        public string Id { get; }
        public string Name { get; }
        public int Price { get; }
        public Color Accent { get; }
        public Color Secondary { get; }
        public Color Ink { get; }
        public Color Surface { get; }
        Texture2D background;
        public Texture2D Background => background ? background : background = Resources.Load<Texture2D>("UI/Themes/" + Id + "-v1");

        internal LobbyTheme(string id, string name, int price, uint accent, uint secondary, uint ink, uint surface)
        {
            Id = id; Name = name; Price = price;
            Accent = Colour(accent); Secondary = Colour(secondary); Ink = Colour(ink); Surface = Colour(surface);
        }

        static Color Colour(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }

    /// <summary>Preview is temporary; equipping a free or owned theme saves the selection.</summary>
    public static class LobbyThemes
    {
        public const string PreferenceKey = "wv.theme";
        public static IReadOnlyList<LobbyTheme> All { get; } = Array.AsReadOnly(new[]
        {
            new LobbyTheme("sunroom", "Sunroom Social", 0, 0xD59D4D, 0x63B6A0, 0x243F3B, 0xFFF4DF),
            new LobbyTheme("candy", "Candy Carnival", 300, 0xD96385, 0x74B7AC, 0x573E48, 0xFFF0F2),
            new LobbyTheme("lantern", "Lantern Festival", 350, 0xD76543, 0x347E7B, 0x553329, 0xFFF0D7),
            new LobbyTheme("winter", "Winter House Party", 0, 0xB53642, 0x3D8970, 0x263E36, 0xFFF4E2)
        });

        static LobbyTheme current;
        public static LobbyTheme Current => current ??= Saved();
        public static event Action Changed;

        static LobbyTheme Find(string id)
        {
            for (int i = 0; i < All.Count; i++)
                if (string.Equals(All[i].Id, id, StringComparison.Ordinal)) return All[i];
            return null;
        }

        static LobbyTheme Saved() => Find(PlayerPrefs.GetString(PreferenceKey, "sunroom")) ?? All[0];

        public static bool Preview(string id)
        {
            var theme = Find(id);
            if (theme == null) return false;
            Select(theme); return true;
        }

        public static bool Equip(string id, Career career)
        {
            var theme = Find(id);
            if (theme == null || (theme.Price > 0 && (career == null || !career.Owns("theme", id)))) return false;
            PlayerPrefs.SetString(PreferenceKey, theme.Id);
            PlayerPrefs.Save();
            current = theme;
            Changed?.Invoke();
            return true;
        }

        public static void Restore() => Select(Saved());

        static void Select(LobbyTheme theme)
        {
            if (Current == theme) return;
            current = theme; Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntime() { current = null; Changed = null; }
    }
}
