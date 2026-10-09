using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Wreckabulary
{
    /// <summary>Shared controls editor. Its input lease blocks gameplay without disabling UI navigation.</summary>
    public sealed class ControlsPanel : MonoBehaviour
    {
        sealed class BindingRow
        {
            public string Name;
            public InputAction Action;
            public int Binding;
            public Button Button;
            public TextMeshProUGUI Label;
        }

        readonly List<BindingRow> rows = new();
        readonly List<Button> buttons = new();
        IDisposable inputLease;
        BindingRow listening;
        TextMeshProUGUI sensitivity, invert, status;
        Button cancel, first;
        Canvas canvas;
        bool cancelRequested;

        public Selectable FirstSelectable => first;
        public bool IsListening => listening != null && KeyBindings.Listening;
        public string StatusText => status ? status.text : "";

        public static ControlsPanel Mount(RectTransform host)
        {
            var root = LobbyKit.Rect(host, "Controls panel").Fill();
            root.gameObject.SetActive(false);
            var panel = root.gameObject.AddComponent<ControlsPanel>();
            panel.Build(root);
            root.gameObject.SetActive(true);
            return panel;
        }

        void Build(RectTransform root)
        {
            canvas = root.GetComponentInParent<Canvas>();
            var title = LobbyKit.Display(root, "MAKE IT YOURS", 28, LobbyKit.Sun, TextAlignmentOptions.MidlineLeft);
            title.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(4, -38), new Vector2(-4, 0));
            var hint = LobbyKit.Text(root, "Choose a key. Press its replacement. Shared keys swap.", 18, LobbyKit.Muted);
            hint.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(4, -68), new Vector2(-4, -38));
            hint.enableAutoSizing = true; hint.fontSizeMin = 16; hint.fontSizeMax = 18;
            var area = LobbyKit.Rect(root, "Controls list").Place(Vector2.zero, Vector2.one, new Vector2(0, 88), new Vector2(0, -76));
            var list = LobbyKit.Scroll(area, "Controls scroll", 8);

            var look = Row(list, "Mouse sensitivity", "Turn speed · saved automatically");
            first = ActionButton(look, "Sensitivity less", "−", () => KeyBindings.SetSensitivity(KeyBindings.Sensitivity - KeyBindings.SensitivityStep), 46);
            sensitivity = LobbyKit.Display(look, "", 22, LobbyKit.Sun).Size(90, 40);
            ActionButton(look, "Sensitivity more", "+", () => KeyBindings.SetSensitivity(KeyBindings.Sensitivity + KeyBindings.SensitivityStep), 46);
            var inverted = Row(list, "Invert mouse Y", "Mouse up looks down when enabled");
            var flip = ActionButton(inverted, "Invert mouse Y", "", () => KeyBindings.SetInvertY(!KeyBindings.InvertY), 150);
            invert = flip.GetComponentInChildren<TextMeshProUGUI>();

            var headings = LobbyKit.Text(list, "KEYBOARD + MOUSE             CONTROLLER", 16, LobbyKit.Cyan, TextAlignmentOptions.MidlineRight);
            headings.Size(-1, 28);
            foreach (var (name, action, binding) in KeyBindings.Rows(DesktopBinding.Shared))
            {
                var controls = Row(list, name);
                var row = new BindingRow { Name = name, Action = action, Binding = binding };
                row.Button = ActionButton(controls, "Rebind " + name, "", () => Begin(row), 158);
                row.Label = row.Button.GetComponentInChildren<TextMeshProUGUI>();
                rows.Add(row);
                var legend = LobbyKit.Text(controls, PadHint(action.name), 16, LobbyKit.Cyan, TextAlignmentOptions.MidlineRight);
                legend.enableAutoSizing = false;
                legend.textWrappingMode = TextWrappingModes.Normal;
                legend.Size(130, 46);
            }
            var touch = LobbyKit.Text(list, "Touch: move with the left stick, drag to look, tap action buttons and hand slots.\nController bindings stay fixed. Keyboard changes apply to the mouse-player seat.", 16, LobbyKit.Muted);
            touch.textWrappingMode = TextWrappingModes.Normal;
            touch.Size(-1, 84);

            var footer = LobbyKit.Rect(root, "Controls footer").Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 80));
            status = LobbyKit.Text(footer, "", 17, LobbyKit.Muted);
            status.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(4, -29), new Vector2(-4, 0));
            status.enableAutoSizing = true; status.fontSizeMin = 16; status.fontSizeMax = 17;
            cancel = LobbyKit.TextAction(footer, "Cancel rebind", "CANCEL", 20, () => CancelRebind());
            ((RectTransform)cancel.transform).Place(Vector2.zero, Vector2.zero, new Vector2(0, 2), new Vector2(156, 46));
            var reset = LobbyKit.TextAction(footer, "Reset controls", "RESET DEFAULTS", 20, KeyBindings.ResetToDefaults);
            buttons.Add(reset);
            ((RectTransform)reset.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-220, 2), new Vector2(0, 46));
        }

        static RectTransform Row(Transform list, string label, string detail = null)
        {
            var row = LobbyKit.Rect(list, label);
            row.Size(-1, detail == null ? 68 : 96);
            row.Paint(LobbyKit.Card, 10).raycastTarget = false;
            var text = LobbyKit.Text(row, label, 20, LobbyKit.Cream, TextAlignmentOptions.MidlineLeft);
            text.rectTransform.Place(Vector2.zero, new Vector2(.47f, 1), new Vector2(14, detail == null ? 5 : 48), new Vector2(-8, -5));
            text.enableAutoSizing = true; text.fontSizeMin = 18; text.fontSizeMax = 20;
            text.textWrappingMode = TextWrappingModes.Normal;
            if (detail != null)
            {
                var note = LobbyKit.Text(row, detail, 16, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
                note.rectTransform.Place(Vector2.zero, new Vector2(.47f, 0), new Vector2(14, 7), new Vector2(-8, 47));
                note.textWrappingMode = TextWrappingModes.Normal;
            }
            var controls = LobbyKit.Row(row, "Values", 10);
            controls.Place(new Vector2(.47f, 0), Vector2.one, new Vector2(0, 8), new Vector2(-14, -8));
            var layout = controls.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childForceExpandHeight = false;
            return controls;
        }

        Button ActionButton(Transform parent, string name, string label, Action click, float width, bool lockWhileListening = true)
        {
            var button = LobbyKit.Button(parent, name, Color.white, click, 8, LobbyKit.Navy, 2, 3);
            button.Size(width, 44);
            var text = LobbyKit.Display(button.Body(), label, 19, LobbyKit.Navy);
            text.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-8, 0));
            text.enableAutoSizing = true; text.fontSizeMin = 16; text.fontSizeMax = 19;
            if (lockWhileListening) buttons.Add(button);
            return button;
        }

        static string PadHint(string action) => action switch
        {
            "Move" => "Left stick / D-pad", "Attack" => "X / Square\nLT / L2 blocks", "Aim" => "Right stick", "Jump" => "A / Cross",
            "Dodge" => "B / Circle", "Interact" => "RT / R2", "Spell" => "Hold Y / Triangle", "Drop" => "Hold LB / L1",
            "Hand 1" or "Hand 2" => "R3 swaps hands", "Pause" => "View / Share", _ => "—"
        };

        void Begin(BindingRow row)
        {
            CancelRebind();
            listening = row;
            KeyBindings.Listen(row.Action, row.Binding, () =>
            {
                listening = null;
                if (!this || !isActiveAndEnabled) return;
                Refresh();
                EventSystem.current?.SetSelectedGameObject(row.Button.gameObject);
            }, AcceptControl);
            Refresh();
            EventSystem.current?.SetSelectedGameObject(cancel.gameObject);
        }

        bool AcceptControl(InputControl control)
        {
            if (control.device is not Mouse mouse) return true;
            var camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)cancel.transform, mouse.position.ReadValue(), camera)) return true;
            // Matching input is suppressed before UI receives it, so queue the cancel click for Update.
            cancelRequested = true;
            return false;
        }

        public bool CancelRebind()
        {
            if (!IsListening) { listening = null; return false; }
            KeyBindings.Stop();
            return true;
        }

        public void Close()
        {
            CancelRebind();
            gameObject.SetActive(false);
        }

        void OnEnable()
        {
            inputLease ??= KeyBindings.BlockGameplay();
            TouchBinding.Shared.ReleaseAll();
            KeyBindings.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            KeyBindings.Changed -= Refresh;
            CancelRebind();
            inputLease?.Dispose();
            inputLease = null;
            TouchBinding.Shared.ReleaseAll();
        }

        void Update()
        {
            if (cancelRequested) { cancelRequested = false; CancelRebind(); }
            if (!IsListening) return;
            foreach (var pad in Gamepad.all)
                if (pad.buttonEast.wasPressedThisFrame)
                { CancelRebind(); KeyBindings.QuietInput(pad.buttonEast); break; }
        }

        void Refresh()
        {
            if (!status) return;
            bool waiting = IsListening;
            sensitivity.text = Mathf.RoundToInt(KeyBindings.Sensitivity * 100f) + "%";
            invert.text = KeyBindings.InvertY ? "ON" : "OFF";
            foreach (var row in rows) row.Label.text = waiting && row == listening ? "PRESS A KEY…" : KeyBindings.KeyName(row.Action, row.Binding);
            foreach (var button in buttons) if (button) button.interactable = !waiting;
            cancel.gameObject.SetActive(waiting);
            status.text = waiting ? "Waiting for a key · ESC / controller B cancels" : KeyBindings.LoadWarning ?? "Saved automatically · reset restores keys and mouse settings";
        }
    }
}
