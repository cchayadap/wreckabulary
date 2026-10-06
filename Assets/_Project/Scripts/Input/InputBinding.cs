using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Wreckabulary
{
    public struct PlayerCommands
    {
        public Vector2 move;
        public bool grab, grabHeld;
        public bool attack, attackHeld;
        public bool aimHeld;
        public bool jump, dodge;
        public bool blockHeld;
        public bool drop;
        public bool swap;
        public int slot;
        public bool spellHeld, spellDown, spellUp;
        public bool up, down;
        public bool start;
        public Vector2 look;
        public bool aimAtPointer;
        public Vector2 pointer;
        public Vector2 lookDelta;
    }

    public abstract class InputBinding
    {
        public const float DropHoldSeconds = 0.25f;

        public abstract string Id { get; }
        public virtual bool CanLook => false;
        public virtual bool ReadsMouse => false;
        public abstract void Read(ref PlayerCommands c);
        public abstract bool JoinPressed();
        public abstract bool StartPressed();
    }

    public struct HoldToFire
    {
        bool holding, fired;
        float since;

        public bool Update(bool pressed, float now, float seconds = InputBinding.DropHoldSeconds)
        {
            if (!pressed)
            {
                holding = false;
                return false;
            }
            if (!holding)
            {
                holding = true;
                fired = false;
                since = now;
            }
            if (fired || now - since < seconds) return false;
            fired = true;
            return true;
        }
    }

    /// <summary>
    /// Two players can share a keyboard (the couch layout; one player alone uses <see cref="DesktopBinding"/>).
    /// Left: WASD move, Space grab, J attack (also throws and places), K spell (W/S choose), U jump, Left Shift dodge, L block, hold R to drop.
    /// Right: arrows move, . or Numpad1 grab, / or Numpad2 attack (also throws and places), Right Shift or Numpad3 spell,
    /// comma or Numpad0 jump, Right Ctrl or Numpad5 dodge, ; or Numpad4 block, hold ' or Numpad6 to drop.
    /// </summary>
    public class KeyboardBinding : InputBinding
    {
        public enum Side { Left, Right }

        readonly Side side;
        HoldToFire dropHold;

        public KeyboardBinding(Side side) => this.side = side;

        public override string Id => $"keyboard-{side}";

        static float Axis(KeyControl positive, KeyControl negative) =>
            (positive.isPressed ? 1f : 0f) - (negative.isPressed ? 1f : 0f);

        public override void Read(ref PlayerCommands c)
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (side == Side.Left)
            {
                c.move = new Vector2(Axis(kb.dKey, kb.aKey), Axis(kb.wKey, kb.sKey));
                c.grab = kb.spaceKey.wasPressedThisFrame;
                c.grabHeld = kb.spaceKey.isPressed;
                c.attack = kb.jKey.wasPressedThisFrame;
                c.jump = kb.uKey.wasPressedThisFrame;
                c.dodge = kb.leftShiftKey.wasPressedThisFrame;
                c.blockHeld = kb.lKey.isPressed;
                c.drop = dropHold.Update(kb.rKey.isPressed, Time.unscaledTime);
                c.swap = kb.tabKey.wasPressedThisFrame;
                c.spellHeld = kb.kKey.isPressed;
                c.spellDown = kb.kKey.wasPressedThisFrame;
                c.spellUp = kb.kKey.wasReleasedThisFrame;
                c.up = kb.wKey.wasPressedThisFrame;
                c.down = kb.sKey.wasPressedThisFrame;
                c.start = kb.enterKey.wasPressedThisFrame;
            }
            else
            {
                c.move = new Vector2(Axis(kb.rightArrowKey, kb.leftArrowKey), Axis(kb.upArrowKey, kb.downArrowKey));
                c.grab = kb.periodKey.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame;
                c.grabHeld = kb.periodKey.isPressed || kb.numpad1Key.isPressed;
                c.attack = kb.slashKey.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame;
                c.jump = kb.commaKey.wasPressedThisFrame || kb.numpad0Key.wasPressedThisFrame;
                c.dodge = kb.rightCtrlKey.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame;
                c.blockHeld = kb.semicolonKey.isPressed || kb.numpad4Key.isPressed;
                c.drop = dropHold.Update(kb.quoteKey.isPressed || kb.numpad6Key.isPressed, Time.unscaledTime);
                c.swap = kb.numpad8Key.wasPressedThisFrame;
                c.spellHeld = kb.rightShiftKey.isPressed || kb.numpad3Key.isPressed;
                c.spellDown = kb.rightShiftKey.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame;
                c.spellUp = !c.spellHeld && (kb.rightShiftKey.wasReleasedThisFrame || kb.numpad3Key.wasReleasedThisFrame);
                c.up = kb.upArrowKey.wasPressedThisFrame;
                c.down = kb.downArrowKey.wasPressedThisFrame;
                c.start = kb.numpadEnterKey.wasPressedThisFrame;
            }
            if (c.move.sqrMagnitude > 1f) c.move.Normalize();
        }

        public override bool JoinPressed()
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            return side == Side.Left
                ? kb.jKey.wasPressedThisFrame
                : kb.periodKey.wasPressedThisFrame || kb.slashKey.wasPressedThisFrame ||
                  kb.numpad1Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame;
        }

        public override bool StartPressed()
        {
            var kb = Keyboard.current;
            return (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) ||
                (TouchBinding.Shared.IsOverlayFor(Id) && TouchBinding.Shared.StartPressed());
        }
    }

    public class GamepadBinding : InputBinding
    {
        public readonly Gamepad Pad;
        float lastStickY;
        HoldToFire dropHold;

        public GamepadBinding(Gamepad pad) => Pad = pad;

        public const float LookYawSpeed = 3f, LookPitchSpeed = 2f;

        public override string Id => $"gamepad-{Pad.deviceId}";
        public override bool CanLook => true;

        public override void Read(ref PlayerCommands c)
        {
            if (Pad == null || !Pad.added) return;

            var stick = Pad.leftStick.ReadValue();
            if (stick.magnitude < 0.2f) stick = Vector2.zero;
            c.move = Vector2.ClampMagnitude(stick + Pad.dpad.ReadValue(), 1f);
            var look = Pad.rightStick.ReadValue();
            c.look = look.magnitude < 0.3f ? Vector2.zero : look;
            c.lookDelta = new Vector2(c.look.x * LookYawSpeed, -c.look.y * LookPitchSpeed) * Time.deltaTime;

            c.grab = Pad.rightTrigger.wasPressedThisFrame;
            c.grabHeld = Pad.rightTrigger.isPressed;
            c.attack = Pad.buttonWest.wasPressedThisFrame;
            c.jump = Pad.buttonSouth.wasPressedThisFrame;
            c.dodge = Pad.buttonEast.wasPressedThisFrame;
            c.blockHeld = Pad.leftTrigger.isPressed;
            c.drop = dropHold.Update(Pad.leftShoulder.isPressed, Time.unscaledTime);
            c.swap = Pad.rightStickButton.wasPressedThisFrame;
            c.spellHeld = Pad.buttonNorth.isPressed;
            c.spellDown = Pad.buttonNorth.wasPressedThisFrame;
            c.spellUp = Pad.buttonNorth.wasReleasedThisFrame;

            // A flick of the stick counts as one step through the word wheel.
            float y = stick.y;
            c.up = Pad.dpad.up.wasPressedThisFrame || (y > 0.6f && lastStickY <= 0.6f);
            c.down = Pad.dpad.down.wasPressedThisFrame || (y < -0.6f && lastStickY >= -0.6f);
            lastStickY = y;
            c.start = Pad.startButton.wasPressedThisFrame;
        }

        public override bool JoinPressed() =>
            Pad.added && (Pad.buttonSouth.wasPressedThisFrame || Pad.buttonWest.wasPressedThisFrame || Pad.startButton.wasPressedThisFrame);

        public override bool StartPressed() => (Pad.added && Pad.startButton.wasPressedThisFrame) ||
            (TouchBinding.Shared.IsOverlayFor(Id) && TouchBinding.Shared.StartPressed());
    }

    public class ScriptedBinding : InputBinding
    {
        static int count;
        readonly string id = $"scripted-{count++}";
        public PlayerCommands Next;

        public override string Id => id;

        public override void Read(ref PlayerCommands c)
        {
            c = Next;
            Next.grab = Next.attack = Next.jump = Next.dodge = Next.drop = false;
            Next.swap = false;
            Next.slot = 0;
            Next.spellDown = Next.spellUp = Next.up = Next.down = Next.start = false;
            Next.lookDelta = Vector2.zero;
        }

        public override bool JoinPressed() => false;
        public override bool StartPressed() => false;
    }
}
