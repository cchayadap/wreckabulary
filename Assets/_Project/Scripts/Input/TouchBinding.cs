using UnityEngine;

namespace Wreckabulary
{
    public enum TouchAction { Attack, Dodge, Jump, Craft, Block, Grab, Deploy, Drop, Swap, Start, Up, Down }

    public sealed class TouchBinding : InputBinding
    {
        static TouchBinding shared;
        public static TouchBinding Shared => shared ??= new TouchBinding();
        readonly bool[] held = new bool[12];
        readonly bool[] pressed = new bool[12];
        HoldToFire dropHold;
        bool craftOpen, craftDown;
        int startFrame = -1;
        Vector2 move, look;

        public override string Id => "touch-local";
        public bool Enabled { get; set; }
        public bool OverlayDesktop { get; set; }
        public string OverlayBindingId { get; set; }
        public bool IsAiming => Enabled && look.sqrMagnitude > 0.01f;
        public bool CraftOpen => craftOpen;
        public bool CraftPressPending => craftDown;
        public bool IsOverlayFor(string bindingId) => Enabled && !string.IsNullOrEmpty(bindingId) && OverlayBindingId == bindingId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetShared() => shared = null;

        public void SetMove(Vector2 value) => move = Vector2.ClampMagnitude(value, 1f);
        public void SetLook(Vector2 value) => look = Vector2.ClampMagnitude(value, 1f);

        public void SetHeld(TouchAction action, bool value)
        {
            int i = (int)action;
            if (value && !held[i]) Pulse(action);
            held[i] = value;
        }

        public void Pulse(TouchAction action)
        {
            pressed[(int)action] = true;
            if (action == TouchAction.Start) startFrame = Time.frameCount;
        }

        public void SetCraftOpen(bool open)
        {
            if (open && !craftOpen) craftDown = true;
            craftOpen = open;
        }

        public override void Read(ref PlayerCommands c)
        {
            if (!Enabled) return;
            c.move = move;
            c.look = look;
            c.attack = pressed[(int)TouchAction.Attack];
            c.dodge = pressed[(int)TouchAction.Dodge];
            c.jump = pressed[(int)TouchAction.Jump];
            c.grab = pressed[(int)TouchAction.Grab];
            c.grabHeld = held[(int)TouchAction.Grab];
            c.blockHeld = held[(int)TouchAction.Block];
            c.swap = pressed[(int)TouchAction.Swap];
            c.drop = dropHold.Update(held[(int)TouchAction.Drop], Time.unscaledTime);
            c.spellDown = craftDown;
            c.spellHeld = craftOpen;
            c.up = pressed[(int)TouchAction.Up];
            c.down = pressed[(int)TouchAction.Down];
            c.start = pressed[(int)TouchAction.Start];
            if (c.start) startFrame = Time.frameCount;
            System.Array.Clear(pressed, 0, pressed.Length);
            craftDown = false;
        }

        public void Merge(ref PlayerCommands c)
        {
            var touch = default(PlayerCommands);
            Read(ref touch);
            if (touch.move.sqrMagnitude > 0f) c.move = touch.move;
            if (touch.look.sqrMagnitude > 0f)
            {
                c.look = touch.look;
                c.aimAtPointer = false;
            }
            c.attack |= touch.attack;
            c.dodge |= touch.dodge;
            c.jump |= touch.jump;
            c.grab |= touch.grab;
            c.grabHeld |= touch.grabHeld;
            c.blockHeld |= touch.blockHeld;
            c.drop |= touch.drop;
            c.swap |= touch.swap;
            c.spellDown |= touch.spellDown;
            c.spellHeld |= touch.spellHeld;
            c.up |= touch.up;
            c.down |= touch.down;
            c.start |= touch.start;
        }

        public void ReleaseAll()
        {
            System.Array.Clear(held, 0, held.Length);
            System.Array.Clear(pressed, 0, pressed.Length);
            move = look = Vector2.zero;
            craftOpen = craftDown = false;
            dropHold = default;
            startFrame = -1;
        }

        public override bool JoinPressed() => false;
        public override bool StartPressed() => Enabled && startFrame == Time.frameCount;
    }
}
