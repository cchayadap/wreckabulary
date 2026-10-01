using UnityEngine.InputSystem;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// Control names for prompts. The desktop keys are read from <see cref="DesktopBinding"/>, so a rebind
    /// shows up here; then come the couch keyboard halves (left, right) and the gamepad.
    /// </summary>
    public static class ControlHints
    {
        static DesktopBinding Desktop => DesktopBinding.Shared;

        static string Key(InputAction action) => action.GetBindingDisplayString().ToUpperInvariant();

        /// <summary>"Press SPACE, J, . or A to join".</summary>
        public static string Join(string verb) => Application.isMobilePlatform ? $"Tap PLAY to {verb}" : $"Press {Key(Desktop.Jump)}, J, . or A to {verb}";

        public static string Players => Application.isMobilePlatform ? "Use the left stick to move and the right stick to aim" : $"Up to 4 roommates: {Key(Desktop.Jump)} plays with the mouse, J and . share one keyboard, plus gamepads";

        public static string Attack => Application.isMobilePlatform ? "SMASH button" : $"{Key(Desktop.Attack)}, J, / or X";
        public static string Grab => Application.isMobilePlatform ? "GRAB button" : $"{Key(Desktop.Interact)}, Space, . or RT";
        public static string Spell => Application.isMobilePlatform ? "CRAFT, choose a word, then BUILD" : $"{Key(Desktop.Spell)}, K, Right Shift or Y";
        public static string Jump => Application.isMobilePlatform ? "JUMP button" : $"{Key(Desktop.Jump)}, U, comma or A";
        // The left keyboard half dodges on Left Shift too.
        public static string Dodge => Application.isMobilePlatform ? "DASH button" : $"{Key(Desktop.Dodge)}, Right Ctrl or B";
        public static string Block => Application.isMobilePlatform ? "Hold BLOCK with a shield" : $"{Key(Desktop.Block)}, L, ; or LT";
        public static string Place => Application.isMobilePlatform ? "PLACE button" : $"{Key(Desktop.Deploy)}, I, Numpad7 or RB";
        public static string Swap => Application.isMobilePlatform ? "SWAP button" : $"{Key(Desktop.Swap)}, Numpad8 or right-stick press";
        public static string Move => Application.isMobilePlatform ? "Left stick to move, right stick to aim" : "WASD, arrow keys or the left stick";
    }
}
