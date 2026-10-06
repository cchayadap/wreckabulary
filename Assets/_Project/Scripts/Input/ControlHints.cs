using UnityEngine.InputSystem;
using UnityEngine;

namespace Wreckabulary
{
    public static class ControlHints
    {
        static DesktopBinding Desktop => DesktopBinding.Shared;

        static string Key(InputAction action) => action.GetBindingDisplayString().ToUpperInvariant();

        public static string KeyOf(InputAction action) => Key(action);

        public static string MoveKeys
        {
            get
            {
                var move = Desktop.Move;
                var parts = new System.Collections.Generic.List<string>();
                foreach (var part in new[] { "Up", "Left", "Down", "Right" })
                    for (int i = 0; i < move.bindings.Count; i++)
                        if (move.bindings[i].isPartOfComposite && move.bindings[i].name == part) parts.Add(move.GetBindingDisplayString(i).ToUpperInvariant());
                return parts.TrueForAll(p => p.Length == 1) ? string.Concat(parts) : string.Join("/", parts);
            }
        }

        public static string Join(string verb) => Application.isMobilePlatform ? $"Tap PLAY to {verb}" : $"Press {Key(Desktop.Jump)}, J, . or A to {verb}";

        public static string Players => Application.isMobilePlatform ? "Use the left stick to move and the right stick to aim" : $"Up to 4 roommates: {Key(Desktop.Jump)} plays with the mouse, J and . share one keyboard, plus gamepads";

        public static string Attack => Application.isMobilePlatform ? "SMASH button" : $"{Key(Desktop.Attack)}, J, / or X";
        public static string Grab => Application.isMobilePlatform ? "GRAB button" : $"{Key(Desktop.Interact)}, Space, . or RT";
        public static string Spell => Application.isMobilePlatform ? "CRAFT, choose a word, then BUILD" : $"{Key(Desktop.Spell)}, K, Right Shift or Y";
        public static string Jump => Application.isMobilePlatform ? "JUMP button" : $"{Key(Desktop.Jump)}, U, comma or A";
        public static string Dodge => Application.isMobilePlatform ? "DASH button" : $"{Key(Desktop.Dodge)}, Right Ctrl or B";
        public static string Block => Application.isMobilePlatform ? "Hold BLOCK with a shield" : $"Hold {Key(Desktop.Attack)} with a shield, L, ; or LT";
        public static string Aim => Application.isMobilePlatform ? "Drag to look" : $"Hold {Key(Desktop.Aim)}";
        public static string Drop => Application.isMobilePlatform ? "Hold DROP button" :
            Key(Desktop.Drop) == "R" ? "Hold R, ' or Numpad6, or LB" : $"Hold {Key(Desktop.Drop)}, R (left keys), ' or Numpad6, or LB";
        public static string Swap => Application.isMobilePlatform ? "Tap a hand slot" : $"{Key(Desktop.Hand1)} / {Key(Desktop.Hand2)}, Tab (left keys), Numpad8 or right-stick press";
        public static string Move => Application.isMobilePlatform ? "Left stick to move, right stick to aim" : $"{MoveKeys}, arrow keys or the left stick";
    }
}
