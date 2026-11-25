using System;
using System.Collections.Generic;
using System.Text;

namespace Wreckabulary.Rules
{
    public enum ControlDevice { MouseKeyboard, KeysLeft, KeysRight, Gamepad, Touch }

    public enum PlayerControl { Move, Look, Aim, Jump, Dodge, Attack, Grab, Spell, Book, Hands, Block, Drop, Pause, Start }

    public static class ControlWords
    {
        public const string JoinMore = "More players: press Start on a controller, or . on the keyboard's right half";

        public static string Key(ControlDevice device, PlayerControl control) => device switch
        {
            ControlDevice.KeysLeft => LeftKey(control),
            ControlDevice.KeysRight => RightKey(control),
            ControlDevice.Gamepad => PadKey(control),
            ControlDevice.Touch => TouchKey(control),
            _ => "",
        };

        static string LeftKey(PlayerControl control) => control switch
        {
            PlayerControl.Move => "WASD",
            PlayerControl.Jump => "U",
            PlayerControl.Dodge => "L-SHIFT",
            PlayerControl.Attack => "J",
            PlayerControl.Grab => "SPACE",
            PlayerControl.Spell => "K",
            PlayerControl.Hands => "TAB",
            PlayerControl.Block => "L",
            PlayerControl.Drop => "R",
            PlayerControl.Pause => "ESC",
            PlayerControl.Start => "ENTER",
            _ => "",
        };

        static string RightKey(PlayerControl control) => control switch
        {
            PlayerControl.Move => "ARROWS",
            PlayerControl.Jump => ",",
            PlayerControl.Dodge => "R-CTRL",
            PlayerControl.Attack => "/",
            PlayerControl.Grab => ".",
            PlayerControl.Spell => "R-SHIFT",
            PlayerControl.Hands => "NUM 8",
            PlayerControl.Block => ";",
            PlayerControl.Drop => "'",
            PlayerControl.Pause => "ESC",
            PlayerControl.Start => "NUM ENTER",
            _ => "",
        };

        static string PadKey(PlayerControl control) => control switch
        {
            PlayerControl.Move => "LEFT STICK",
            PlayerControl.Look => "RIGHT STICK",
            PlayerControl.Jump => "A",
            PlayerControl.Dodge => "B",
            PlayerControl.Attack => "X",
            PlayerControl.Grab => "RT",
            PlayerControl.Spell => "Y",
            PlayerControl.Book => "RB",
            PlayerControl.Hands => "D-PAD ‹ ›",
            PlayerControl.Block => "LT",
            PlayerControl.Drop => "LB",
            PlayerControl.Pause => "START",
            PlayerControl.Start => "START",
            _ => "",
        };

        static string TouchKey(PlayerControl control) => control switch
        {
            PlayerControl.Move => "MOVE",
            PlayerControl.Look => "AIM",
            PlayerControl.Jump => "JUMP",
            PlayerControl.Dodge => "DODGE",
            PlayerControl.Attack => "SMASH",
            PlayerControl.Grab => "INTERACT",
            PlayerControl.Spell => "SPELL",
            PlayerControl.Book => "BOOK",
            PlayerControl.Hands => "HAND",
            PlayerControl.Block => "BLOCK",
            PlayerControl.Drop => "HOLD DROP",
            PlayerControl.Start => "PLAY",
            _ => "",
        };

        public static bool HoldsToSpell(ControlDevice device) => device is ControlDevice.Gamepad or ControlDevice.KeysLeft or ControlDevice.KeysRight;

        public static string Verb(ControlDevice device) => device == ControlDevice.Touch ? "Tap" : "Press";

        public static string MoveHow(ControlDevice device, string keys) => device switch
        {
            ControlDevice.Touch => "Drag the MOVE stick",
            ControlDevice.Gamepad => "Move with the left stick",
            ControlDevice.KeysRight => "Move with the arrow keys",
            _ => $"Move with {keys}",
        };

        public static string JumpDodgeHow(ControlDevice device, string jump, string dodge) =>
            $"{Verb(device)} {jump} to jump, then {dodge} to dodge";

        public static string SmashHow(ControlDevice device, string attack) =>
            $"{Verb(device)} {attack} to punch it. Furniture drops the letters of its name.";

        public static string BookHow(ControlDevice device, string book) =>
            string.IsNullOrEmpty(book) ? "" : device == ControlDevice.Touch
                ? "Tap the book to see every recipe and what it does"
                : $"Hold {book} to see every recipe and what it does";

        public static string SpellHow(ControlDevice device, string spell, string word) => device switch
        {
            ControlDevice.Touch => $"Tap {spell}, choose {word}, then tap {spell} again",
            ControlDevice.Gamepad => $"Hold {spell}, choose {word} with the stick, then let go",
            ControlDevice.KeysLeft => $"Hold {spell}, choose {word} with W and S, then let go",
            ControlDevice.KeysRight => $"Hold {spell}, choose {word} with the arrow keys, then let go",
            _ => $"Press {spell}, type {word}, then press {spell} again",
        };

        public static string SwingHow(ControlDevice device, string attack, string word) =>
            $"{Verb(device)} {attack} to swing your {word}";

        public static string ThrowHow(ControlDevice device, string grab, string attack) => device == ControlDevice.MouseKeyboard
            ? $"Press {grab} to grab it, then hold {attack} to aim and let go to throw"
            : $"{Verb(device)} {grab} to grab it, then {attack} to throw it";

        public static string HandsHow(ControlDevice device, string hands) => device == ControlDevice.Touch
            ? "Tap a hand slot to switch between Hand 1 and Hand 2"
            : $"Press {hands} to switch between Hand 1 and Hand 2";

        public static string JoinFirst(string key, string verb) => $"Press {key} to {verb}, or Start on a controller";

        public static string PerDevice(IReadOnlyList<(int seat, ControlDevice device)> seats, Func<ControlDevice, string> line)
        {
            if (seats == null || seats.Count == 0) return line(ControlDevice.MouseKeyboard) ?? "";
            var devices = new List<(int seat, ControlDevice device)>();
            foreach (var seat in seats)
                if (!devices.Exists(d => d.device == seat.device)) devices.Add(seat);
            if (devices.Count == 1) return line(devices[0].device) ?? "";
            var text = new StringBuilder();
            foreach (var (seat, device) in devices)
            {
                string said = line(device);
                if (string.IsNullOrEmpty(said)) continue;
                if (text.Length > 0) text.Append('\n');
                text.Append('P').Append(seat).Append(": ").Append(said);
            }
            return text.ToString();
        }
    }
}
