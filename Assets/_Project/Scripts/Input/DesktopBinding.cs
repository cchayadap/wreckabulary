using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace Wreckabulary
{
    /// <summary>
    /// One player on keyboard and mouse, with the brief's desktop layout (§8): WASD move, mouse aim,
    /// left click attack, right click block, Space jump, Shift dodge, E grab (hold to revive),
    /// Q spell (mouse wheel or W/S to choose), hold R to drop, Enter start.
    /// The keys live in an action map so they can be rebound, and prompts read them back
    /// through <see cref="ControlHints"/>.
    /// </summary>
    public class DesktopBinding : InputBinding
    {
        static DesktopBinding shared;

        /// <summary>There is one mouse, so there is one desktop player, and a rebind applies everywhere.</summary>
        public static DesktopBinding Shared => shared ??= new DesktopBinding();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetShared()
        {
            shared?.Map.Dispose();
            shared = null;
        }

        public readonly InputActionMap Map = new("Desktop");
        public readonly InputAction Move, Attack, Block, Jump, Dodge, Interact, Spell, Drop, Deploy, Swap, Up, Down, Start, Point;
        HoldToFire dropHold;
        readonly List<RaycastResult> uiHits = new();
        PointerEventData uiPointer;
        EventSystem uiEvents;

        DesktopBinding()
        {
            Move = Map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Attack = Button("Attack", "<Mouse>/leftButton");
            Block = Button("Block", "<Mouse>/rightButton");
            Jump = Button("Jump", "<Keyboard>/space");
            Dodge = Button("Dodge", "<Keyboard>/leftShift");
            Interact = Button("Interact", "<Keyboard>/e");
            Spell = Button("Spell", "<Keyboard>/q");
            Drop = Button("Drop", "<Keyboard>/r");
            Deploy = Button("Place", "<Keyboard>/f");
            Swap = Button("Swap", "<Keyboard>/tab");
            Up = Button("Up", "<Keyboard>/w", "<Mouse>/scroll/up");
            Down = Button("Down", "<Keyboard>/s", "<Mouse>/scroll/down");
            Start = Button("Start", "<Keyboard>/enter");
            Point = Map.AddAction("Point", InputActionType.PassThrough, "<Mouse>/position", expectedControlLayout: "Vector2");
            Map.Enable();
        }

        InputAction Button(string actionName, params string[] paths)
        {
            var a = Map.AddAction(actionName, InputActionType.Button);
            foreach (var path in paths) a.AddBinding(path);
            return a;
        }

        public override string Id => "keyboard-mouse";

        public override void Read(ref PlayerCommands c)
        {
            c.move = Vector2.ClampMagnitude(Move.ReadValue<Vector2>(), 1f);
            c.attack = Attack.WasPressedThisFrame();
            c.blockHeld = Block.IsPressed();
            c.jump = Jump.WasPressedThisFrame();
            c.dodge = Dodge.WasPressedThisFrame();
            c.grab = Interact.WasPressedThisFrame();
            c.grabHeld = Interact.IsPressed();
            c.drop = dropHold.Update(Drop.IsPressed(), Time.unscaledTime);
            c.deploy = Deploy.WasPressedThisFrame();
            c.swap = Swap.WasPressedThisFrame();
            c.spellHeld = Spell.IsPressed();
            c.spellDown = Spell.WasPressedThisFrame();
            c.spellUp = Spell.WasReleasedThisFrame();
            c.up = Up.WasPressedThisFrame();
            c.down = Down.WasPressedThisFrame();
            c.start = Start.WasPressedThisFrame();

            // Clicking a menu or an on-screen skill must never also punch into the world.
            bool overUi = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
            // Hover state can be a frame behind a pointer moved and pressed in the same update.
            if (!overUi && (c.attack || c.blockHeld)) overUi = HitsUiNow();
            if (overUi) c.attack = c.blockHeld = false;

            // Aim only while the pointer is over the game, so alt-tabbing away doesn't spin the player.
            if (Mouse.current == null || !Application.isFocused || overUi ||
                (TouchBinding.Shared.IsOverlayFor(Id) && TouchBinding.Shared.IsAiming)) return;
            c.pointer = Point.ReadValue<Vector2>();
            c.aimAtPointer = c.pointer.x >= 0f && c.pointer.y >= 0f && c.pointer.x <= Screen.width && c.pointer.y <= Screen.height;
        }

        bool HitsUiNow()
        {
            var events = EventSystem.current;
            if (!events) return false;
            if (uiEvents != events)
            {
                uiEvents = events;
                uiPointer = new PointerEventData(events);
            }
            uiPointer.position = Point.ReadValue<Vector2>();
            uiHits.Clear();
            events.RaycastAll(uiPointer, uiHits);
            return uiHits.Count > 0;
        }

        /// <summary>Space, E or a left click.</summary>
        public override bool JoinPressed() =>
            Jump.WasPressedThisFrame() || Interact.WasPressedThisFrame() ||
            (Attack.WasPressedThisFrame() && !HitsUiNow());

        public override bool StartPressed() => Start.WasPressedThisFrame() ||
            (TouchBinding.Shared.IsOverlayFor(Id) && TouchBinding.Shared.StartPressed());
    }
}
