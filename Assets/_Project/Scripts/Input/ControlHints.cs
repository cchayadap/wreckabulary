using System;
using UnityEngine.InputSystem;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public static class ControlHints
    {
        static DesktopBinding Desktop => DesktopBinding.Shared;

        public const string PadSpell = "Y", PadBag = "RB", PadHands = "D-PAD ‹ ›", PadLookAround = "L3", PadPushToTalk = "D-PAD UP";

        public static string PadHand(int slot) => slot == 0 ? "‹" : "›";

        static string Key(InputAction action) => KeyBindings.Display(action);

        public static string KeyOf(InputAction action) => Key(action);

        public static string MoveKeys
        {
            get
            {
                var move = Desktop.Move;
                var parts = new System.Collections.Generic.List<string>();
                foreach (var part in new[] { "Up", "Left", "Down", "Right" })
                    for (int i = 0; i < move.bindings.Count; i++)
                        if (move.bindings[i].isPartOfComposite && move.bindings[i].name == part) parts.Add(KeyBindings.Display(move, i));
                return parts.TrueForAll(p => p.Length == 1) ? string.Concat(parts) : string.Join("/", parts);
            }
        }

        public static ControlDevice Device(InputBinding binding)
        {
            if (binding is TouchBinding || TouchOverlays(binding)) return ControlDevice.Touch;
            return binding switch
            {
                GamepadBinding => ControlDevice.Gamepad,
                KeyboardBinding keys => keys.Id.EndsWith("Left", StringComparison.Ordinal) ? ControlDevice.KeysLeft : ControlDevice.KeysRight,
                _ => Application.isMobilePlatform ? ControlDevice.Touch : ControlDevice.MouseKeyboard,
            };
        }

        static bool TouchOverlays(InputBinding binding) =>
            binding != null && TouchBinding.Shared.Enabled && TouchBinding.Shared.IsOverlayFor(binding.Id);

        public static string For(InputBinding binding, PlayerControl control) => For(Device(binding), control);

        public static string For(ControlDevice device, PlayerControl control) =>
            device == ControlDevice.MouseKeyboard ? DesktopKey(control) : ControlWords.Key(device, control);

        public static bool HasBook(InputBinding binding) => binding != null && For(binding, PlayerControl.Book).Length > 0;

        public static string LookAroundKey => Key(Desktop.LookAround).Replace("LEFT ", "L-").Replace("RIGHT ", "R-").Replace("CONTROL", "CTRL");

        public static string LookAround(InputBinding binding) => Device(binding) switch
        {
            ControlDevice.MouseKeyboard => LookAroundKey,
            ControlDevice.Gamepad => PadLookAround,
            _ => "",
        };

        public static string PushToTalkKey => Key(Desktop.PushToTalk).Replace("LEFT ", "L-").Replace("RIGHT ", "R-").Replace("CONTROL", "CTRL");

        public static string PushToTalk(InputBinding binding) => Device(binding) switch
        {
            ControlDevice.MouseKeyboard => PushToTalkKey,
            ControlDevice.Gamepad => PadPushToTalk,
            _ => "",
        };

        static string DesktopKey(PlayerControl control) => control switch
        {
            PlayerControl.Move => MoveKeys,
            PlayerControl.Look => "MOUSE",
            PlayerControl.Aim => Key(Desktop.Attack),
            PlayerControl.Jump => Key(Desktop.Jump),
            PlayerControl.Dodge => Key(Desktop.Dodge),
            PlayerControl.Attack or PlayerControl.Block => Key(Desktop.Attack),
            PlayerControl.Grab => Key(Desktop.Interact),
            PlayerControl.Spell => Key(Desktop.SpellBox),
            PlayerControl.Book => Key(Desktop.Bag),
            PlayerControl.Hands => $"{Key(Desktop.Hand1)} / {Key(Desktop.Hand2)}",
            PlayerControl.Drop => Key(Desktop.Drop),
            PlayerControl.Pause => Key(Desktop.Pause),
            PlayerControl.Start => Key(Desktop.Start),
            _ => "",
        };

        public static string Join(string verb) => Application.isMobilePlatform ? $"Tap PLAY to {verb}" : ControlWords.JoinFirst(Key(Desktop.Jump), verb);

        public static string JoinMore => ControlWords.JoinMore;

        public static string Players => Application.isMobilePlatform ? "Use the left stick to move and the right stick to aim" : JoinMore;
    }
}
