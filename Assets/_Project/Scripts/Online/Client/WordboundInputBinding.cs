using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Wreckabulary.Online
{
    public enum WordboundControl { Interact, Spell, Attack, Aim, Block, Drop, CycleGear, Menu }

    public readonly struct WordboundMenuCommands
    {
        public readonly int Step;
        public readonly bool Confirm, Back;
        public readonly float Scroll;
        public readonly Vector2Int Direction;
        public WordboundMenuCommands(int step, bool confirm, bool back, float scroll = 0, Vector2Int direction = default)
        { Step = step; Confirm = confirm; Back = back; Scroll = scroll; Direction = direction; }
    }

    public sealed class WordboundInputBinding : InputBinding, IDisposable
    {
        readonly DesktopBinding desktop;
        public InputBinding BorrowedSource { get; }
        bool fixedPadUnavailable;
        readonly Dictionary<Gamepad, Vector4> sticks = new Dictionary<Gamepad, Vector4>();
        readonly List<Gamepad> removed = new List<Gamepad>();
        Gamepad selected;
        GamepadBinding gamepad;
        bool disposed, requireRelease, menuRequiresRelease;
        HoldToFire dropHold;
        int menuDirection, menuAxis;
        float nextMenuStep;
        readonly string[] hintPaths = new string[8], hints = new string[8];
        int hintVersion = -1;
        Keyboard hintKeyboard;
        string hintLayout;
        public int Revision { get; private set; }
        public Gamepad ActiveGamepad => selected;
        public override string Id => gamepad?.Id ?? desktop?.Id ?? "wordbound-input";
        public override bool CanLook => true;
        public override bool ReadsMouse => !disposed && selected == null && desktop != null;

        public WordboundInputBinding() => desktop = new DesktopBinding();

        public WordboundInputBinding(DesktopBinding source)
        {
            desktop = source ?? throw new ArgumentNullException(nameof(source));
            BorrowedSource = source;
        }

        public WordboundInputBinding(GamepadBinding source)
        {
            gamepad = source ?? throw new ArgumentNullException(nameof(source));
            selected = source.Pad ?? throw new ArgumentException("A borrowed gamepad binding needs its original device.", nameof(source));
            BorrowedSource = source;
        }

        public string ControlHint(WordboundControl control)
        {
            if (disposed) return "";
            if (selected != null) return PadHint(selected, control);
            var action = DesktopAction(desktop, control);
            if (action == null) return "";
            var keyboard = Keyboard.current;
            string layout = keyboard?.keyboardLayout;
            if (hintVersion != KeyBindings.Version || hintKeyboard != keyboard || hintLayout != layout)
            {
                Array.Clear(hints, 0, hints.Length);
                hintVersion = KeyBindings.Version; hintKeyboard = keyboard; hintLayout = layout;
            }
            int index = (int)control;
            string path = action.bindings[0].effectivePath;
            if (hints[index] == null || hintPaths[index] != path)
            {
                hintPaths[index] = path;
                hints[index] = Display(action);
            }
            return hints[index];
        }

        internal static string ControlHint(InputBinding source, WordboundControl control) => source switch
        {
            WordboundInputBinding automatic => automatic.ControlHint(control),
            _ => ""
        };

        static InputAction DesktopAction(DesktopBinding keys, WordboundControl control) => control switch
        {
            WordboundControl.Interact => keys.Interact,
            WordboundControl.Spell => keys.Spell,
            WordboundControl.Attack or WordboundControl.Block => keys.Attack,
            WordboundControl.Aim => keys.Aim,
            WordboundControl.Drop => keys.Drop,
            WordboundControl.CycleGear => keys.Bag,
            WordboundControl.Menu => keys.Pause,
            _ => null
        };

        static string Display(InputAction action)
        {
            if (action == null) return "";
            string value = KeyBindings.Display(action);
            return string.IsNullOrEmpty(value) ? "Unbound" : value;
        }

        static string PadHint(Gamepad pad, WordboundControl control)
        {
            if (pad == null || !pad.added || !pad.enabled) return "";
            var button = control switch
            {
                WordboundControl.Interact => pad.rightTrigger,
                WordboundControl.Spell => pad.buttonNorth,
                WordboundControl.Attack => pad.buttonWest,
                WordboundControl.Aim => pad.rightShoulder,
                WordboundControl.Block => pad.leftTrigger,
                WordboundControl.Drop => pad.leftShoulder,
                WordboundControl.CycleGear => pad.rightStickButton,
                WordboundControl.Menu => pad.startButton,
                _ => null
            };
            if (button == null) return "";
            string name = button.shortDisplayName;
            if (string.IsNullOrEmpty(name)) name = button.displayName;
            if (!string.IsNullOrEmpty(name) && KeyBindings.IsLatin(name)) return name;
            return control switch
            {
                WordboundControl.Interact => "RT", WordboundControl.Spell => "Y",
                WordboundControl.Attack => "X", WordboundControl.Aim => "RB",
                WordboundControl.Block => "LT", WordboundControl.Drop => "LB",
                WordboundControl.CycleGear => "RS", WordboundControl.Menu => "Start", _ => ""
            };
        }

        public bool RefreshDevice(bool allowSelection = true)
        {
            if (disposed) return false;
            if (BorrowedSource != null)
            {
                bool unavailable = selected != null && (!selected.added || !selected.enabled);
                bool lostFixed = unavailable && !fixedPadUnavailable;
                if (unavailable != fixedPadUnavailable) SuspendGameplay();
                fixedPadUnavailable = unavailable;
                return lostFixed;
            }
            bool lost = selected != null && (!selected.added || !selected.enabled);
            if (lost) Select(null);
            removed.Clear();
            foreach (var pad in sticks.Keys) if (!pad.added) removed.Add(pad);
            foreach (var pad in removed) sticks.Remove(pad);
            if (!allowSelection || DesktopBinding.Typing || KeyBindings.GameplayBlocked) return lost;
            Gamepad candidate = null;
            foreach (var pad in Gamepad.all)
            {
                if (!pad.enabled) continue;
                var move = pad.leftStick.ReadValue();
                var look = pad.rightStick.ReadValue();
                var sample = new Vector4(move.x, move.y, look.x, look.y);
                sticks.TryGetValue(pad, out var previous);
                sticks[pad] = sample;
                bool stickIntent = (move.sqrMagnitude > .0625f || look.sqrMagnitude > .09f) &&
                    (sample - previous).sqrMagnitude > .01f;
                bool buttonIntent = false;
                foreach (var control in pad.allControls)
                    if (control is ButtonControl button && button.wasPressedThisFrame) { buttonIntent = true; break; }
                if (stickIntent || buttonIntent) candidate = pad;
            }
            var mouse = Mouse.current;
            bool desktopIntent = Keyboard.current?.anyKey.wasPressedThisFrame == true || mouse != null &&
                (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
                 mouse.middleButton.wasPressedThisFrame || mouse.delta.ReadValue().sqrMagnitude > 4 ||
                 mouse.scroll.ReadValue().sqrMagnitude > 0);
            if (desktopIntent) Select(null);
            else if (!lost && candidate != null) Select(candidate);
            return lost;
        }

        void Select(Gamepad pad)
        {
            if (selected == pad) return;
            selected = pad;
            gamepad = pad == null ? null : new GamepadBinding(pad);
            SuspendGameplay();
            menuDirection = 0; menuAxis = 0;
            Revision++;
        }

        public void SuspendGameplay()
        {
            requireRelease = true;
            menuRequiresRelease = true;
            dropHold = default;
        }

        public override void Read(ref PlayerCommands commands)
        {
            commands = default;
            if (disposed) return;
            RefreshDevice();
            if (fixedPadUnavailable || DesktopBinding.Typing || KeyBindings.GameplayBlocked || Time.timeScale <= 0)
            {
                SuspendGameplay();
                return;
            }
            if (selected == null)
            {
                desktop.Read(ref commands);
                commands.swap = desktop.Bag.WasPressedThisFrame();
            }
            else
            {
                gamepad.Read(ref commands);
                commands.attackHeld = selected.buttonWest.isPressed;
                commands.aimHeld = selected.rightShoulder.isPressed;
            }
            if (requireRelease)
            {
                requireRelease = GameplayActionHeld();
                if (requireRelease)
                {
                    commands = new PlayerCommands { move = commands.move, look = commands.look,
                        lookDelta = commands.lookDelta, aimAtPointer = commands.aimAtPointer, pointer = commands.pointer };
                    return;
                }
            }
            commands.drop = dropHold.Update(selected == null ? desktop.Drop.IsPressed() : selected.leftShoulder.isPressed,
                Time.unscaledTime);
        }

        bool GameplayActionHeld() => selected == null
            ? desktop.Attack.IsPressed() || desktop.Aim.IsPressed() || desktop.Jump.IsPressed() ||
                desktop.Interact.IsPressed() || desktop.Spell.IsPressed() || desktop.Drop.IsPressed() ||
                desktop.Hand1.IsPressed() || desktop.Hand2.IsPressed() || desktop.Bag.IsPressed()
            : selected.buttonWest.isPressed || selected.buttonSouth.isPressed || selected.buttonNorth.isPressed ||
                selected.rightTrigger.isPressed || selected.leftTrigger.isPressed || selected.leftShoulder.isPressed ||
                selected.rightShoulder.isPressed || selected.rightStickButton.isPressed;

        public WordboundMenuCommands ReadMenu(float now)
        {
            if (disposed || selected == null || !selected.added || !selected.enabled ||
                DesktopBinding.Typing || KeyBindings.GameplayBlocked)
            { menuDirection = 0; menuAxis = 0; return default; }
            Vector2 navigation = selected.dpad.ReadValue();
            if (navigation.sqrMagnitude < .25f) navigation = selected.leftStick.ReadValue();
            if (menuRequiresRelease)
            {
                menuRequiresRelease = navigation.sqrMagnitude > .25f ||
                    selected.buttonSouth.isPressed || selected.buttonEast.isPressed;
                if (menuRequiresRelease) { menuDirection = 0; menuAxis = 0; return default; }
            }
            int direction = navigation.y > .55f ? -1 : navigation.y < -.55f ? 1 :
                navigation.x < -.55f ? -1 : navigation.x > .55f ? 1 : 0;
            int axisKind = direction == 0 ? 0 : Mathf.Abs(navigation.y) > .55f ? 2 : 1;
            bool repeatedDirection = direction == menuDirection && axisKind == menuAxis;
            int step = 0;
            if (direction != 0 && (!repeatedDirection || now >= nextMenuStep))
            {
                step = direction;
                nextMenuStep = now + (repeatedDirection ? .12f : .35f);
            }
            menuDirection = direction; menuAxis = axisKind;
            Vector2Int axis = step == 0 ? Vector2Int.zero : Mathf.Abs(navigation.y) > .55f ?
                new Vector2Int(0, step) : new Vector2Int(step, 0);
            return new WordboundMenuCommands(step, selected.buttonSouth.wasPressedThisFrame,
                selected.buttonEast.wasPressedThisFrame, selected.rightStick.ReadValue().y, axis);
        }

        public override bool JoinPressed()
        {
            RefreshDevice();
            return !disposed && !DesktopBinding.Typing && !KeyBindings.GameplayBlocked &&
                (selected == null ? desktop.JoinPressed() : selected.added && selected.enabled && gamepad.JoinPressed());
        }

        public override bool StartPressed()
        {
            return !disposed && !DesktopBinding.Typing && !KeyBindings.GameplayBlocked &&
                (selected == null ? desktop.Pause.WasPressedThisFrame() : selected.added && selected.enabled && selected.startButton.wasPressedThisFrame);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            selected = null; gamepad = null; sticks.Clear();
            removed.Clear();
            if (BorrowedSource == null) desktop?.Dispose();
        }
    }
}
