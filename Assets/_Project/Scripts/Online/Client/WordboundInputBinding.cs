using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Wreckabulary.Online
{
    public readonly struct WordboundMenuCommands
    {
        public readonly int Step;
        public readonly bool Confirm, Back;
        public readonly float Scroll;
        public WordboundMenuCommands(int step, bool confirm, bool back, float scroll = 0)
        { Step = step; Confirm = confirm; Back = back; Scroll = scroll; }
    }

    public sealed class WordboundInputBinding : InputBinding, IDisposable
    {
        readonly DesktopBinding desktop = new DesktopBinding();
        readonly Dictionary<Gamepad, Vector4> sticks = new Dictionary<Gamepad, Vector4>();
        readonly List<Gamepad> removed = new List<Gamepad>();
        Gamepad selected;
        GamepadBinding gamepad;
        bool disposed, requireRelease, menuRequiresRelease;
        HoldToFire dropHold;
        int menuDirection;
        float nextMenuStep;
        public int Revision { get; private set; }
        public Gamepad ActiveGamepad => selected;
        public override string Id => selected == null ? desktop.Id : gamepad.Id;
        public override bool CanLook => true;
        public override bool ReadsMouse => selected == null;

        public bool RefreshDevice(bool allowSelection = true)
        {
            if (disposed) return false;
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
            menuDirection = 0;
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
            if (DesktopBinding.Typing || KeyBindings.GameplayBlocked || Time.timeScale <= 0)
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
            { menuDirection = 0; return default; }
            Vector2 navigation = selected.dpad.ReadValue();
            if (navigation.sqrMagnitude < .25f) navigation = selected.leftStick.ReadValue();
            if (menuRequiresRelease)
            {
                menuRequiresRelease = navigation.sqrMagnitude > .25f ||
                    selected.buttonSouth.isPressed || selected.buttonEast.isPressed;
                if (menuRequiresRelease) { menuDirection = 0; return default; }
            }
            int direction = navigation.y > .55f ? -1 : navigation.y < -.55f ? 1 :
                navigation.x < -.55f ? -1 : navigation.x > .55f ? 1 : 0;
            int step = 0;
            if (direction != 0 && (direction != menuDirection || now >= nextMenuStep))
            {
                step = direction;
                nextMenuStep = now + (direction == menuDirection ? .12f : .35f);
            }
            menuDirection = direction;
            return new WordboundMenuCommands(step, selected.buttonSouth.wasPressedThisFrame,
                selected.buttonEast.wasPressedThisFrame, selected.rightStick.ReadValue().y);
        }

        public override bool JoinPressed()
        {
            RefreshDevice();
            return !disposed && !DesktopBinding.Typing && !KeyBindings.GameplayBlocked &&
                (selected == null ? desktop.JoinPressed() : gamepad.JoinPressed());
        }

        public override bool StartPressed()
        {
            return !disposed && !DesktopBinding.Typing && !KeyBindings.GameplayBlocked &&
                (selected == null ? desktop.Pause.WasPressedThisFrame() : selected.startButton.wasPressedThisFrame);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            selected = null; gamepad = null; sticks.Clear();
            removed.Clear();
            desktop.Dispose();
        }
    }
}
