using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace Wreckabulary
{
    public class DesktopBinding : InputBinding
    {
        static DesktopBinding shared;
        public static bool Typing;

        public static DesktopBinding Shared => shared ??= new DesktopBinding();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetShared()
        {
            shared?.Map.Dispose();
            shared = null;
            Typing = false;
        }

        public readonly InputActionMap Map = new("Desktop");
        public readonly InputAction Move, Attack, Aim, Jump, Dodge, Interact, Spell, Drop, Hand1, Hand2, Bag, Pause, Up, Down, Start, Point, Look;
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
            Aim = Button("Aim", "<Mouse>/rightButton");
            Jump = Button("Jump", "<Keyboard>/space");
            Dodge = Button("Dodge", "<Keyboard>/leftShift");
            Interact = Button("Interact", "<Keyboard>/e");
            Spell = Button("Spell", "<Keyboard>/q");
            Drop = Button("Drop", "<Keyboard>/r");
            Hand1 = Button("Hand 1", "<Keyboard>/1");
            Hand2 = Button("Hand 2", "<Keyboard>/2");
            Bag = Button("Bag", "<Keyboard>/tab");
            Pause = Button("Pause", "<Keyboard>/escape");
            Up = Button("Up", "<Keyboard>/w", "<Mouse>/scroll/up");
            Down = Button("Down", "<Keyboard>/s", "<Mouse>/scroll/down");
            Start = Button("Start", "<Keyboard>/enter");
            Point = Map.AddAction("Point", InputActionType.PassThrough, "<Mouse>/position", expectedControlLayout: "Vector2");
            Look = Map.AddAction("Look", InputActionType.PassThrough, "<Mouse>/delta", expectedControlLayout: "Vector2");
            KeyBindings.Load(Map);
            Map.Enable();
        }

        InputAction Button(string actionName, params string[] paths)
        {
            var a = Map.AddAction(actionName, InputActionType.Button);
            foreach (var path in paths) a.AddBinding(path);
            return a;
        }

        public override string Id => "keyboard-mouse";
        public override bool CanLook => true;
        public override bool ReadsMouse => true;

        public override void Read(ref PlayerCommands c)
        {
            if (Typing || KeyBindings.Listening) return;
            c.move = Vector2.ClampMagnitude(Move.ReadValue<Vector2>(), 1f);
            c.attack = Attack.WasPressedThisFrame();
            c.attackHeld = Attack.IsPressed();
            c.aimHeld = Aim.IsPressed();
            c.jump = Jump.WasPressedThisFrame();
            c.dodge = Dodge.WasPressedThisFrame();
            c.grab = Interact.WasPressedThisFrame();
            c.grabHeld = Interact.IsPressed();
            c.drop = dropHold.Update(Drop.IsPressed(), Time.unscaledTime);
            c.slot = Hand1.WasPressedThisFrame() ? 1 : Hand2.WasPressedThisFrame() ? 2 : 0;
            c.spellHeld = Spell.IsPressed();
            c.spellDown = Spell.WasPressedThisFrame();
            c.spellUp = Spell.WasReleasedThisFrame();
            c.up = Up.WasPressedThisFrame();
            c.down = Down.WasPressedThisFrame();
            c.start = Start.WasPressedThisFrame();

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                var delta = Look.ReadValue<Vector2>();
                c.lookDelta = KeyBindings.Look(delta);
                return;
            }

            bool overUi = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
            if (!overUi && (c.attack || c.attackHeld || c.aimHeld)) overUi = HitsUiNow();
            if (overUi) c.attack = c.attackHeld = c.aimHeld = false;

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

        public override bool JoinPressed() =>
            Jump.WasPressedThisFrame() || Interact.WasPressedThisFrame() ||
            (Attack.WasPressedThisFrame() && !HitsUiNow());

        public override bool StartPressed() => Start.WasPressedThisFrame() ||
            (TouchBinding.Shared.IsOverlayFor(Id) && TouchBinding.Shared.StartPressed());
    }
}
