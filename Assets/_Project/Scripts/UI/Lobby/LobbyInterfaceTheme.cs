using System;
using UnityEngine;

namespace Wreckabulary
{
    public enum LobbyColourRole { Panel, Card, Raised, Text, Muted, Border, Selection, OnSelection, Focus, Primary, OnPrimary, Disabled, DisabledText, Error, Positive }

    [Serializable]
    public sealed class LobbyInterfacePalette
    {
        static readonly LobbyColourRole[] roles = (LobbyColourRole[])Enum.GetValues(typeof(LobbyColourRole));
        [ColorUsage(false)] public Color Panel = Hex(0x171d27);
        [ColorUsage(false)] public Color Card = Hex(0x232c38);
        [ColorUsage(false)] public Color Raised = Hex(0x303d4d);
        [ColorUsage(false)] public Color Text = Hex(0xf6f8fc);
        [ColorUsage(false)] public Color Muted = Hex(0xb2bdca);
        [ColorUsage(false)] public Color Border = Hex(0x46566c);
        [ColorUsage(false)] public Color Selection = Hex(0x53c9e8);
        [ColorUsage(false)] public Color OnSelection = Hex(0x171d27);
        [ColorUsage(false)] public Color Focus = Hex(0xb8edff);
        [ColorUsage(false)] public Color Primary = Hex(0xf2b84b);
        [ColorUsage(false)] public Color OnPrimary = Hex(0x171d27);
        [ColorUsage(false)] public Color Disabled = Hex(0x343c49);
        [ColorUsage(false)] public Color DisabledText = Hex(0x91a0b2);
        [ColorUsage(false)] public Color Error = Hex(0xff756b);
        [ColorUsage(false)] public Color Positive = Hex(0x6fd69b);

        public Color Get(LobbyColourRole role) => role switch
        {
            LobbyColourRole.Panel => Panel, LobbyColourRole.Card => Card, LobbyColourRole.Raised => Raised,
            LobbyColourRole.Text => Text, LobbyColourRole.Muted => Muted, LobbyColourRole.Border => Border,
            LobbyColourRole.Selection => Selection, LobbyColourRole.OnSelection => OnSelection, LobbyColourRole.Focus => Focus,
            LobbyColourRole.Primary => Primary, LobbyColourRole.OnPrimary => OnPrimary, LobbyColourRole.Disabled => Disabled,
            LobbyColourRole.DisabledText => DisabledText, LobbyColourRole.Error => Error, LobbyColourRole.Positive => Positive,
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };

        public bool TryValidate(out string reason)
        {
            foreach (LobbyColourRole role in roles)
            {
                Color c = Get(role);
                if (!Finite(c.r) || !Finite(c.g) || !Finite(c.b) || c.a != 1f)
                { reason = role + " must be an opaque finite colour between 0 and 1."; return false; }
            }
            reason = null; return true;
        }

        internal int Fingerprint()
        {
            unchecked
            {
                int hash = 17;
                foreach (LobbyColourRole role in roles) hash = hash * 31 + Get(role).GetHashCode();
                return hash;
            }
        }
        static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x) && x >= 0 && x <= 1;
        static Color Hex(uint rgb) => new Color((rgb >> 16 & 255) / 255f, (rgb >> 8 & 255) / 255f, (rgb & 255) / 255f, 1);
    }

    [CreateAssetMenu(menuName = "Wreckabulary/Interface/Shared theme", fileName = "LobbyInterfaceTheme")]
    public sealed class LobbyInterfaceTheme : ScriptableObject
    {
        public const string ResourcePath = "UI/LobbyInterfaceTheme";
        public LobbyInterfacePalette Colours = new LobbyInterfacePalette();
        [Header("Online interface fonts")]
        [Tooltip("Roboto Regular TTF. Lobby text uses the saved TMP typography profile.")]
        public Font BodyFont;
        [Tooltip("Roboto Medium TTF. Lobby text uses the saved TMP typography profile.")]
        public Font HeadingFont;
        static readonly LobbyInterfacePalette fallback = new LobbyInterfacePalette();
        static LobbyInterfaceTheme current;
        static LobbyInterfacePalette activePalette = fallback;

        public static LobbyInterfacePalette Palette => activePalette;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void LoadSaved()
        {
            current = Resources.Load<LobbyInterfaceTheme>(ResourcePath);
            activePalette = current && current.TryValidate(out _) ? current.Colours : fallback;
        }

        public bool TryValidate(out string reason)
        {
            if (Colours == null) { reason = "A shared palette is required."; return false; }
            return Colours.TryValidate(out reason);
        }

        public void ApplyOnline(WordboundUiTheme target)
        {
            if (!target) throw new ArgumentNullException(nameof(target));
            if (!TryValidate(out string reason)) throw new ArgumentException(reason);
            target.Panel = Colours.Panel; target.Text = Colours.Text; target.Muted = Colours.Muted;
            target.Button = Colours.Card; target.Hover = Colours.Raised;
            target.Primary = Colours.Primary; target.PrimaryText = Colours.OnPrimary; target.Focus = Colours.Focus;
            target.Disabled = Colours.Disabled; target.DisabledText = Colours.DisabledText; target.Error = Colours.Error;
            if (BodyFont) target.BodyFont = BodyFont;
            if (HeadingFont) target.HeadingFont = HeadingFont;
        }

        internal int Fingerprint()
        {
            unchecked { return ((Colours?.Fingerprint() ?? 0) * 31 + (BodyFont ? BodyFont.GetHashCode() : 0)) * 31 +
                (HeadingFont ? HeadingFont.GetHashCode() : 0); }
        }

        void OnValidate()
        {
            if (ReferenceEquals(current, this)) activePalette = TryValidate(out _) ? Colours : fallback;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntime() { current = null; activePalette = fallback; }
    }
}
