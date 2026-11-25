using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Wreckabulary
{
    public sealed class ControlsPanel : MonoBehaviour
    {
        public const string TitleName = "Control legend title", HouseTitle = "House controls", CourseTitle = "Course controls";
        public const float TitleText = 22f;
        const float RowHeight = 68f, DetailRowHeight = 88f, RowSpacing = 6f, LegendHeight = 44f, NoteHeight = 108f;
        const int CardRadius = 10;
        const string HouseNote = "Right stick: look / aim. Controller mappings stay fixed.\nHold Bag for your letters, the recipe book and the map.\n" +
            "Touch: move, drag to look, tap actions or hand slots.\nKeyboard changes apply to the mouse-player seat.";
        const string CourseNote = "Right stick: look. Controller mappings stay fixed.\nIn courses, Bag swaps your hands.\n" +
            "Keyboard changes apply to the mouse-player seat.";

        sealed class BindingRow
        {
            public string Name;
            public InputAction Action;
            public int Binding;
            public Button Button;
            public TextMeshProUGUI Label, ActionLabel, Legend;
            public RectTransform Root;
        }

        readonly List<BindingRow> rows = new();
        readonly List<Button> buttons = new();
        IDisposable inputLease;
        BindingRow listening;
        TextMeshProUGUI sensitivity, invert, status, legendNote, legendTitle;
        Button cancel, first, houseLegend, wordboundLegend;
        bool showWordbound;
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
            var title = legendTitle = LobbyKit.Text(root, HouseTitle, TitleText, LobbyKit.Cyan, TextAlignmentOptions.MidlineLeft);
            title.name = TitleName;
            title.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(4, -38), new Vector2(-4, 0));
            var hint = LobbyKit.Text(root, "Select a key to change it. Existing keys swap.", 17, LobbyKit.Muted);
            hint.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(4, -68), new Vector2(-4, -38));
            hint.enableAutoSizing = true; hint.fontSizeMin = 16; hint.fontSizeMax = 18;
            var area = LobbyKit.Rect(root, "Controls list").Place(Vector2.zero, Vector2.one, new Vector2(0, 88), new Vector2(0, -76));
            var list = LobbyKit.Scroll(area, "Controls scroll", RowSpacing);

            var legends = LobbyKit.Segmented(list, "Control legends", new[] {
                ("House controls", "House games"), ("Wordbound controls", "Courses")
            }, "House controls", name => SelectLegend(name == "Wordbound controls"), LegendHeight, 18);
            LobbyChoiceShape.Track(legends);
            houseLegend = legends.Find("House controls").GetComponent<Button>();
            wordboundLegend = legends.Find("Wordbound controls").GetComponent<Button>();
            buttons.Add(houseLegend); buttons.Add(wordboundLegend);
            LobbyChoiceShape.Choice(houseLegend, () => !showWordbound);
            LobbyChoiceShape.Choice(wordboundLegend, () => showWordbound);

            var look = Row(list, "Mouse sensitivity", "Look speed");
            first = ActionButton(look, "Sensitivity less", "−", () => KeyBindings.SetSensitivity(KeyBindings.Sensitivity - KeyBindings.SensitivityStep), 46);
            sensitivity = LobbyKit.Text(look, "", 22, LobbyKit.Navy, TextAlignmentOptions.Center).Size(90, 40);
            ActionButton(look, "Sensitivity more", "+", () => KeyBindings.SetSensitivity(KeyBindings.Sensitivity + KeyBindings.SensitivityStep), 46);
            var inverted = Row(list, "Invert mouse Y", "Mouse up looks down");
            var flip = ActionButton(inverted, "Invert mouse Y", "", () => KeyBindings.SetInvertY(!KeyBindings.InvertY), 150);
            invert = flip.GetComponentInChildren<TextMeshProUGUI>();

            var headings = LobbyKit.Rect(list, "Binding columns").Size(-1, 28);
            var keyboardHeading = LobbyKit.Text(headings, "Keyboard / mouse", 15, LobbyKit.Muted, TextAlignmentOptions.Center);
            keyboardHeading.rectTransform.Place(new Vector2(1, 0), Vector2.one, new Vector2(-308, 0), new Vector2(-160, 0));
            var controllerHeading = LobbyKit.Text(headings, "Controller", 15, LobbyKit.Muted, TextAlignmentOptions.MidlineRight);
            controllerHeading.rectTransform.Place(new Vector2(1, 0), Vector2.one, new Vector2(-150, 0), new Vector2(-8, 0));
            foreach (var (name, action, binding) in KeyBindings.Rows(DesktopBinding.Shared))
            {
                var controls = Row(list, name);
                var rowRoot = (RectTransform)controls.parent;
                var row = new BindingRow { Name = name, Action = action, Binding = binding, Root = rowRoot,
                    ActionLabel = rowRoot.GetComponentInChildren<TextMeshProUGUI>() };
                row.ActionLabel.text = name;
                row.Button = ActionButton(controls, "Rebind " + name, "", () => Begin(row), 148);
                row.Label = row.Button.GetComponentInChildren<TextMeshProUGUI>();
                rows.Add(row);
                var legend = row.Legend = LobbyKit.Text(controls, "", 16, LobbyKit.Muted, TextAlignmentOptions.MidlineRight);
                legend.name = "Controller " + action.name;
                legend.enableAutoSizing = false;
                legend.textWrappingMode = TextWrappingModes.Normal;
                legend.Size(142, 46);
            }
            legendNote = LobbyKit.Text(list, "", 16, LobbyKit.Muted);
            legendNote.name = "Control legend note";
            legendNote.textWrappingMode = TextWrappingModes.Normal;
            legendNote.Size(-1, NoteHeight);

            var footer = LobbyKit.Rect(root, "Controls footer").Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 80));
            status = LobbyKit.Text(footer, "", 17, LobbyKit.Muted);
            status.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(4, -29), new Vector2(-4, 0));
            status.enableAutoSizing = true; status.fontSizeMin = 16; status.fontSizeMax = 17;
            cancel = LobbyKit.TextAction(footer, "Cancel rebind", "Cancel", 20, () => CancelRebind());
            LobbyChoiceShape.Action(cancel);
            ((RectTransform)cancel.transform).Place(Vector2.zero, Vector2.zero, new Vector2(0, 2), new Vector2(156, 46));
            var reset = LobbyKit.TextAction(footer, "Reset controls", "Reset defaults", 20, KeyBindings.ResetToDefaults);
            LobbyChoiceShape.Action(reset);
            buttons.Add(reset);
            ((RectTransform)reset.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-220, 2), new Vector2(0, 46));
        }

        static RectTransform Row(Transform list, string label, string detail = null)
        {
            var row = LobbyKit.Rect(list, label);
            row.Size(-1, detail == null ? RowHeight : DetailRowHeight);
            row.Paint(LobbyKit.Card, CardRadius).raycastTarget = false;
            var text = LobbyKit.Text(row, label, 20, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft);
            text.rectTransform.Place(Vector2.zero, new Vector2(.42f, 1), new Vector2(8, detail == null ? 5 : 40), new Vector2(-12, -5));
            text.enableAutoSizing = true; text.fontSizeMin = 18; text.fontSizeMax = 20;
            text.textWrappingMode = TextWrappingModes.Normal;
            if (detail != null)
            {
                var note = LobbyKit.Text(row, detail, 16, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
                note.rectTransform.Place(Vector2.zero, new Vector2(.42f, 0), new Vector2(8, 7), new Vector2(-12, 40));
                note.textWrappingMode = TextWrappingModes.Normal;
            }
            var controls = LobbyKit.Row(row, "Values", 10);
            controls.Place(new Vector2(.42f, 0), Vector2.one, new Vector2(0, 8), new Vector2(-8, -8));
            var layout = controls.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childForceExpandHeight = false;
            return controls;
        }

        Button ActionButton(Transform parent, string name, string label, Action click, float width, bool lockWhileListening = true)
        {
            var button = LobbyKit.Button(parent, name, LobbyKit.Raised, click, 6, LobbyKit.Line, 1);
            button.Size(width, 44);
            var text = LobbyKit.Text(button.Body(), label, 19, LobbyKit.Navy, TextAlignmentOptions.Center);
            text.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-8, 0));
            text.enableAutoSizing = true; text.fontSizeMin = 16; text.fontSizeMax = 19;
            if (lockWhileListening) buttons.Add(button);
            return button;
        }

        void SelectLegend(bool wordbound)
        {
            if (IsListening) return;
            showWordbound = wordbound;
            Refresh();
            EventSystem.current?.SetSelectedGameObject((wordbound ? wordboundLegend : houseLegend).gameObject);
        }


        static string PadHint(string action, bool wordbound) => action switch
        {
            "Move" => "Left stick", "Attack" => "X / Square\nLT / L2 blocks",
            "Aim" => wordbound ? "RB / R1" : "—", "Look around" => "Hold L3 + right stick", "Jump" => "A / Cross",
            "Dodge" => wordbound ? "—" : "B / Circle", "Interact" => "RT / R2",
            "Spell" => wordbound ? "Y / Triangle" : "Hold Y / Triangle", "Spell box" => "Hold Y / Triangle", "Drop" => "Hold LB / L1", "Push to talk" => "Hold D-pad up",
            "Hand 1" => wordbound ? "D-pad left" : "D-pad left\nR3 swaps hands",
            "Hand 2" => wordbound ? "D-pad right" : "D-pad right\nR3 swaps hands",
            "Bag" => wordbound ? "R3 cycles gear" : "Hold RB / R1", "Pause" => wordbound ? "Start / Options" : "View / Share", _ => "—"
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
            legendTitle.text = showWordbound ? CourseTitle : HouseTitle;
            sensitivity.text = Mathf.RoundToInt(KeyBindings.Sensitivity * 100f) + "%";
            invert.text = KeyBindings.InvertY ? "On" : "Off";
            foreach (var row in rows)
            {
                row.Root.gameObject.SetActive(Rules.DesktopScheme.ShowsIn(row.Action.name, showWordbound));
                row.Legend.text = PadHint(row.Action.name, showWordbound);
                row.Label.text = waiting && row == listening ? "Press a key…" : KeyBindings.KeyName(row.Action, row.Binding);
            }
            houseLegend.GetComponent<LobbyPress>().Hot(houseLegend.GetComponent<LobbyPress>().IsHot);
            wordboundLegend.GetComponent<LobbyPress>().Hot(wordboundLegend.GetComponent<LobbyPress>().IsHot);
            legendNote.text = showWordbound ? CourseNote : HouseNote;
            foreach (var button in buttons) if (button) button.interactable = !waiting;
            cancel.gameObject.SetActive(waiting);
            status.text = waiting ? "Press a key · Esc / B cancels" : KeyBindings.LoadWarning ?? "Saved automatically";
        }
    }

    public static class LobbyChoiceShape
    {
        public const string EdgeName = "Choice edge";
        public const int Radius = 8, ChoiceEdge = 2, ActionEdge = 1;
        public const float Gap = 6f, SmallestText = 15f;

        public static void Track(RectTransform track)
        {
            var row = track.GetComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(0, 0, 0, 0);
            row.spacing = Gap;
        }

        public static void Choices(RectTransform track, string picked)
        {
            Track(track);
            foreach (Transform child in track)
                if (child.TryGetComponent(out Button button)) Choice(button, button.name == picked);
        }

        public static void Choice(Button button, bool picked) => Choice(button, () => picked);

        public static void Choice(Button button, Func<bool> picked)
        {
            var (edge, label) = Dress(button, ChoiceEdge);
            void Show(bool hot)
            {
                bool on = picked();
                edge.color = on ? LobbyKit.Selection : LobbyKit.Line;
                label.color = on || hot ? LobbyKit.Cyan : LobbyKit.Navy;
            }
            button.GetComponent<LobbyPress>().Hot = Show;
            Show(false);
        }

        public static void Action(Button button)
        {
            var (_, label) = Dress(button, ActionEdge);
            button.GetComponent<LobbyPress>().Hot = hot => label.color = hot ? LobbyKit.Cyan : LobbyKit.Navy;
            label.color = LobbyKit.Navy;
        }

        static (Image edge, TMP_Text label) Dress(Button button, int width)
        {
            var body = button.Body();
            if (body.TryGetComponent(out Image face)) face.color = LobbyKit.Raised;
            var underline = body.Find("Underline");
            if (underline) underline.gameObject.SetActive(false);
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label.enableAutoSizing) label.fontSizeMin = Mathf.Min(label.fontSizeMax, Mathf.Max(label.fontSizeMin, SmallestText));
            return (LobbyKit.Frame(body, LobbyKit.Line, Radius, width, EdgeName), label);
        }
    }
}
