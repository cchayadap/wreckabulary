using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Wreckabulary
{
    public sealed class WordboundPadFocusGate
    {
        Gamepad device;
        bool requireNeutral = true;

        public void Suspend() => requireNeutral = true;

        public bool CanRead(Gamepad pad, bool focused, bool typing)
        {
            if (pad != device) { device = pad; requireNeutral = true; }
            if (!focused || typing || pad == null || !pad.added || !pad.enabled)
            { requireNeutral = true; return false; }
            if (!requireNeutral) return true;
            if (pad.leftStick.ReadValue().sqrMagnitude > .04f || pad.rightStick.ReadValue().sqrMagnitude > .04f ||
                pad.dpad.ReadValue().sqrMagnitude > .04f) return false;
            foreach (var control in pad.allControls)
                if (control is ButtonControl button && button.isPressed) return false;
            requireNeutral = false;
            return false;
        }
    }
}
