using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class SettingsPage : LobbyPage
    {
        static readonly (string id, string title)[] Tabs =
            { ("profile", "Profile"), ("audio", "Audio"), ("gameplay", "Gameplay"), ("graphics", "Graphics"), ("controls", "Controls"), ("help", "Help") };
        static readonly (string id, string label)[] DisplayModes =
            { ("borderless", "Borderless"), ("fullscreen", "Full screen"), ("windowed", "Windowed") };
        const float KeepFor = 10f;

        public override string Id => LobbyMenu.Settings;
        public override bool Modal => true;

        string tab = "profile";
        string builtTab;
        ScrollRect scroll;
        readonly Dictionary<string, float> scrollPositions = new();
        (FullScreenMode mode, int width, int height)? asked;
        (FullScreenMode mode, int width, int height)? revertTo;
        LobbyCountdown timer;
        ControlsPanel controlsPanel;

        public override void Opened()
        {
            if (controlsPanel && tab == "controls") controlsPanel.gameObject.SetActive(true);
        }

        public override void Closed()
        {
            if (scroll && builtTab != null) scrollPositions[builtTab] = scroll.verticalNormalizedPosition;
            if (controlsPanel) controlsPanel.Close();
        }

        public void ShowTab(string id)
        {
            id = id switch { "general" => "profile", "how" => "help", _ => id };
            if (!Tabs.Any(entry => entry.id == id)) throw new ArgumentException("Unknown settings tab.", nameof(id));
            tab = id;
            Refresh();
            Reselect("Settings tab " + tab);
        }

        protected override void Build()
        {
            builtTab = tab;
            scroll = null;
            controlsPanel = null;
            var body = Panel(new Vector2(.12f, 0), new Vector2(.88f, 1), "Settings");
            var side = LobbyKit.Column(body, "Tabs", 8);
            side.Place(Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(230, 0));
            foreach (var (id, title) in Tabs)
            {
                string name = "Settings tab " + id;
                var chip = LobbyKit.Chip(side, LobbyKit.Upper(title), tab == id, () => ShowTab(id));
                chip.onClick.AddListener(() => Reselect(name));
                chip.name = name;
                chip.Size(-1, 50);
                if (tab == id) First = chip;
            }
            var page = LobbyKit.Rect(body, "Tab " + tab).Place(Vector2.zero, Vector2.one, new Vector2(254, 0), Vector2.zero);
            var holder = LobbyKit.Rect(page, "Tab content").Fill();
            if (revertTo.HasValue) holder.offsetMin = new Vector2(0, 92);
            if (tab == "controls")
            {
                controlsPanel = ControlsPanel.Mount(holder);
                scroll = controlsPanel.GetComponentInChildren<ScrollRect>();
                First = controlsPanel.FirstSelectable;
            }
            else
            {
                var list = LobbyKit.Scroll(holder, "Scroll", 8);
                scroll = list.GetComponentInParent<ScrollRect>();
                switch (tab)
                {
                    case "audio": Audio(list); break;
                    case "gameplay": Gameplay(list); break;
                    case "graphics": Graphics(list); break;
                    case "help": HowToPlay(list); break;
                    default: Profile(list); break;
                }
            }
            AddScrollbar(scroll);
            if (revertTo.HasValue) KeepBar(page);
            Canvas.ForceUpdateCanvases();
            if (scroll && scrollPositions.TryGetValue(tab, out float position)) scroll.verticalNormalizedPosition = position;
        }

        static void AddScrollbar(ScrollRect list)
        {
            if (!list) return;
            var view = (RectTransform)list.transform;
            view.offsetMax = new Vector2(-24, view.offsetMax.y);
            var track = LobbyKit.Rect(view.parent, "Scroll position").Place(new Vector2(1, 0), Vector2.one,
                new Vector2(-16, 0), Vector2.zero);
            track.Paint(LobbyKit.Hex(0x136b68, .12f), 8);
            var area = LobbyKit.Rect(track, "Sliding area").Fill();
            area.offsetMin = new Vector2(2, 2); area.offsetMax = new Vector2(-2, -2);
            var handle = LobbyKit.Rect(area, "Handle").Fill();
            var ink = handle.Paint(LobbyKit.Cyan, 6);
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handle; bar.targetGraphic = ink;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            list.verticalScrollbar = bar;
            list.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        void Profile(Transform list)
        {
            NameField(Setting(list, "Name", "Shown to other players."));
            LobbyKit.Pill(Setting(list, "Appearance"), "Open loadout", "Customize", 20,
                () => Menu.Open(LobbyMenu.Loadout)).Size(220, 46);
        }

        void Gameplay(Transform list)
        {
            Toggle(Setting(list, "Damage numbers"), "Damage numbers", CombatFeedbackOptions.ShowDamage, on => CombatFeedbackOptions.ShowDamage = on);
            Toggle(Setting(list, "Points per hit"), "Points per hit", CombatFeedbackOptions.ShowPoints, on => CombatFeedbackOptions.ShowPoints = on);
            LobbyKit.Pill(Setting(list, "Combat feedback"), "Reset feedback", "Reset feedback", 20,
                () => { CombatFeedbackOptions.ResetToDefaults(); RefreshSetting("Reset feedback"); }).Size(220, 46);
            Toggle(Setting(list, "Reduce theme motion"), "Reduce theme motion", PlayerPrefs.GetInt("wv.theme.effects", 1) == 0, on =>
            {
                PlayerPrefs.SetInt("wv.theme.effects", on ? 0 : 1);
                PlayerPrefs.Save();
                if (Menu.Stage) Menu.Stage.SetThemeEffects(!on);
            });
        }

        void Audio(Transform list)
        {
            Toggle(Setting(list, "Sound"), "Sound", !GameFeedback.Muted, on => GameFeedback.Muted = !on);
            Choose(Setting(list, "Sound pack"), "Sound pack", GameSoundPacks.Ids.Select(id => (id, GameSoundPacks.Label(id))).ToList(),
                GameSoundPacks.Selected, id => GameSoundPacks.Select(id), 340);
            LobbyKit.Pill(Setting(list, "Listen"), "Preview sound pack", "Preview", 20, () => GameSoundPacks.Preview()).Size(220, 46);
            Stepper(Setting(list, "Volume"), "Volume", Mathf.RoundToInt(AudioListener.volume * 100) + "%", step =>
            {
                AudioListener.volume = Mathf.Clamp01(Mathf.Round(AudioListener.volume * 10f + step) / 10f);
                PlayerPrefs.SetFloat(LobbyMenu.VolumeKey, AudioListener.volume);
                PlayerPrefs.Save();
            });
            LobbyKit.Pill(Setting(list, "Defaults"), "Reset audio", "Reset audio", 20, () =>
            {
                GameFeedback.Muted = false;
                GameSoundPacks.Select("default");
                AudioListener.volume = 1f;
                PlayerPrefs.DeleteKey(LobbyMenu.VolumeKey);
                PlayerPrefs.Save();
                RefreshSetting("Reset audio");
            }).Size(220, 46);
        }

        void Graphics(Transform list)
        {
            if (!Application.isMobilePlatform)
            {
                var display = asked ?? Current;
                asked = null;
                Choose(Setting(list, "Display mode", "Borderless switches apps fastest."), "Display", DisplayModes,
                    DisplayId(display.mode), id => Show(DisplayMode(id), display.width, display.height), 520);
                var sizes = Sizes(display);
                int at = Mathf.Max(0, sizes.FindIndex(s => s.width == display.width && s.height == display.height));
                Stepper(Setting(list, "Resolution", "The window's size, or the screen's."), "Resolution", display.width + " x " + display.height, step =>
                {
                    var size = sizes[Mathf.Clamp(at + step, 0, sizes.Count - 1)];
                    Show(display.mode, size.width, size.height);
                });
            }
            string preset = GraphicsOptions.Preset;
            Choose(Setting(list, "Quality preset", preset == GraphicsOptions.Custom ? "Custom: your own mix below." : "Scale, edge smoothing and shadows."),
                "Preset", GraphicsOptions.Presets.Select(p => (p, p)).ToList(), preset, GraphicsOptions.UsePreset, 520);
            Stepper(Setting(list, "Render scale", "Over 100% is sharper, under is faster."), "Render scale",
                Mathf.RoundToInt(GraphicsOptions.RenderScale * 100) + "%",
                step => GraphicsOptions.SetRenderScale(GraphicsOptions.RenderScale + step * GraphicsOptions.ScaleStep));
            Choose(Setting(list, "Anti-aliasing", "Smooths edges. MSAA is the sharpest."), "AA",
                GraphicsOptions.Smoothings.Select(s => (s, s.StartsWith("MSAA") ? "MSAA " + s.Substring(4) + "x" : s)).ToList(),
                GraphicsOptions.Smoothing, GraphicsOptions.SetSmoothing, 620);
            Choose(Setting(list, "Shadows", "Sharper, and reaching farther, as they go up."), "Shadows",
                GraphicsOptions.ShadowLevels.Select(s => (s, s)).ToList(), GraphicsOptions.Shadows, GraphicsOptions.SetShadows, 560);
            Choose(Setting(list, "Frame cap", "With v-sync on, your screen sets the pace."), "Cap",
                GraphicsOptions.FrameCaps.Select(c => (c.ToString(CultureInfo.InvariantCulture), c == 0 ? "None" : c.ToString(CultureInfo.InvariantCulture))).ToList(),
                GraphicsOptions.FrameCap.ToString(CultureInfo.InvariantCulture), id => GraphicsOptions.SetFrameCap(int.Parse(id, CultureInfo.InvariantCulture)), 520);
            Toggle(Setting(list, "V-sync", "Stops tearing, for a little delay."), "V-sync", QualitySettings.vSyncCount > 0, on =>
            {
                QualitySettings.vSyncCount = on ? 1 : 0;
                PlayerPrefs.SetInt(LobbyMenu.VsyncKey, on ? 1 : 0);
                PlayerPrefs.Save();
            });
            var reset = LobbyKit.Pill(Setting(list, "Defaults", "High, no frame cap, v-sync off."), "Reset graphics", "Reset", 20, () =>
            {
                GraphicsOptions.ResetToDefaults();
                QualitySettings.vSyncCount = 0;
                PlayerPrefs.DeleteKey(LobbyMenu.VsyncKey);
                PlayerPrefs.Save();
                RefreshSetting("Reset graphics");
            });
            reset.Size(180, 46);
        }

        void HowToPlay(RectTransform column)
        {
            column.GetComponent<VerticalLayoutGroup>().spacing = 14;
            var kicker = LobbyKit.Caps(column, "A house full of possibilities", 14, TextAlignmentOptions.BottomLeft);
            kicker.color = LobbyKit.Cyan;
            kicker.Size(-1, 20);
            LobbyKit.Display(column, "Everything starts with a word.", 36, LobbyKit.Sun, TextAlignmentOptions.MidlineLeft, LobbyKit.Ink.Stroke).Size(-1, 46);
            var steps = LobbyKit.Row(column, "Steps", 14);
            steps.Size(-1, 200);
            steps.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            var rules = GameConfig.Current.Rules.Defaults;
            int recipes = GameConfig.Current.Items.Enabled.Count();
            bool phone = Application.isMobilePlatform;
            var keys = DesktopBinding.Shared;
            string spell = phone ? "CRAFT" : ControlHints.KeyOf(keys.Spell);
            string swing = phone ? "SMASH" : ControlHints.KeyOf(keys.Attack);
            Step(steps, "01", "Break it.", "HAMMER", "Smash the house's own furniture. A TABLE breaks into T, A, B, L and E.");
            Step(steps, "02", "Spell it.", "BOOK", $"Walk over letters; your bag holds {rules.MaxLetters}. Press {spell} to craft any of the {recipes} recipes.");
            Step(steps, "03", "Bring it.", "BAT", $"Press {swing} to swing gear, throw it or set down a tool. Then wreck the place.");
            if (!phone) KeyBox(column, keys);
            Wrapped(column, $"Health is {Mathf.RoundToInt(rules.MaxHealth)} HP. Your looks never change your stats.", 17, LobbyKit.Muted, 26);
            var go = LobbyKit.Button(column, "Got it", LobbyKit.SunHi, () => Menu.Open(LobbyMenu.Play), 14, LobbyKit.Navy, 4, 6, LobbyKit.Sun2);
            go.Size(-1, 64);
            var label = LobbyKit.Display(go.Body(), "GOT IT. LET'S PLAY", 26, LobbyKit.Navy);
            label.characterSpacing = 3;
            label.rectTransform.Fill();
        }

        static void Step(Transform row, string number, string title, string art, string text)
        {
            var card = LobbyKit.Rect(row, "Step " + number);
            card.Size(-1, -1, 1);
            LobbyKit.Face(card, LobbyKit.Card, 18, LobbyKit.Line, 2);
            var image = LobbyKit.ItemImage(card, art, 68, -10f);
            if (image) image.rectTransform.Pin(Vector2.one, new Vector2(-14, -10), new Vector2(68, 68));
            LobbyKit.Display(card, number, 44, LobbyKit.Sun, TextAlignmentOptions.TopLeft, LobbyKit.Ink.Stroke).rectTransform
                .Place(new Vector2(0, 1), Vector2.one, new Vector2(20, -66), new Vector2(-16, -12));
            LobbyKit.Display(card, title, 28, LobbyKit.Cream, TextAlignmentOptions.TopLeft).rectTransform
                .Place(new Vector2(0, 1), Vector2.one, new Vector2(20, -106), new Vector2(-16, -68));
            var body = Wrapped(card, text, 17, LobbyKit.Muted);
            body.enableAutoSizing = true; body.fontSizeMin = 13; body.fontSizeMax = 17;
            body.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(20, 12), new Vector2(-16, -110));
        }

        static void KeyBox(Transform column, DesktopBinding keys)
        {
            var box = LobbyKit.Row(column, "Keys", 24, 16);
            box.Size(-1, 242);
            var layout = box.GetComponent<HorizontalLayoutGroup>();
            layout.childForceExpandWidth = true;
            layout.childAlignment = TextAnchor.UpperLeft;
            LobbyKit.Face(box, LobbyKit.Track, 16, LobbyKit.Line, 2);
            var entries = new[]
            {
                (new[] { "WASD" }, "move"), (new[] { "MOUSE" }, "aim"),
                (new[] { ControlHints.KeyOf(keys.Attack) }, "smash, throw, place, block"), (new[] { ControlHints.KeyOf(keys.Aim) }, "hold to aim"),
                (new[] { ControlHints.KeyOf(keys.Dodge) }, "dodge"), (new[] { ControlHints.KeyOf(keys.Jump) }, "jump"),
                (new[] { ControlHints.KeyOf(keys.Spell) }, "spell"), (new[] { ControlHints.KeyOf(keys.Hand1), ControlHints.KeyOf(keys.Hand2) }, "switch hand"),
                (new[] { ControlHints.KeyOf(keys.Bag) }, "hold for bag and map"), (new[] { ControlHints.KeyOf(keys.Interact) }, "pick up, hold to revive"),
                (new[] { ControlHints.KeyOf(keys.Drop) }, "hold to drop gear"), (new[] { ControlHints.KeyOf(keys.Pause) }, "pause"),
            };
            for (int side = 0; side < 2; side++)
            {
                var list = LobbyKit.Column(box, side == 0 ? "Left keys" : "Right keys", 6);
                list.Size(-1, -1, 1);
                for (int i = side; i < entries.Length; i += 2)
                {
                    var (caps, what) = entries[i];
                    var line = LobbyKit.Row(list, what, 8);
                    line.Size(-1, 30);
                    line.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
                    foreach (string cap in caps) LobbyKit.Kbd(line, cap);
                    LobbyKit.Text(line, what, 17, LobbyKit.Cream, TextAlignmentOptions.MidlineLeft).Size(-1, 28, 1);
                }
            }
        }

        static RectTransform Setting(Transform list, string label, string help = null)
        {
            var row = LobbyKit.Rect(list, label);
            row.Size(-1, help == null ? 60 : 80);
            row.Paint(LobbyKit.Card, 14).raycastTarget = false;
            var words = LobbyKit.Column(row, "Words", 0);
            words.Place(Vector2.zero, new Vector2(.42f, 1), new Vector2(20, 4), new Vector2(-8, -4));
            words.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            var text = LobbyKit.Display(words, label, 24, LobbyKit.Cream, TextAlignmentOptions.MidlineLeft);
            text.characterSpacing = 1;
            text.Size(-1, 30);
            if (help != null) LobbyKit.Text(words, help, 15, LobbyKit.Muted, TextAlignmentOptions.TopLeft).textWrappingMode = TextWrappingModes.Normal;
            var controls = LobbyKit.Row(row, "Controls", 8);
            controls.Place(new Vector2(.42f, 0), Vector2.one, new Vector2(0, 8), new Vector2(-12, -8));
            var layout = controls.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childForceExpandHeight = false;
            return controls;
        }

        void RefreshSetting(string name)
        {
            Refresh();
            Reselect(name);
        }

        void Choose(RectTransform controls, string prefix, IReadOnlyList<(string id, string label)> choices, string picked, Action<string> pick, float width)
        {
            var named = choices.Select(c => (prefix + " " + c.id, c.label)).ToList();
            var track = LobbyKit.Segmented(controls, prefix, named, prefix + " " + picked, name =>
            {
                pick(name.Substring(prefix.Length + 1));
                RefreshSetting(name);
            });
            track.Size(width, 48);
            track.GetComponent<LayoutElement>().minWidth = Mathf.Min(width, 64f * choices.Count);
        }

        void Toggle(RectTransform controls, string name, bool value, Action<bool> set)
        {
            foreach (bool option in new[] { true, false })
            {
                string id = name + (option ? " on" : " off");
                var chip = LobbyKit.Chip(controls, option ? "ON" : "OFF", value == option, () => { set(option); RefreshSetting(id); });
                chip.name = id;
                chip.Size(96, 46);
            }
        }

        void Stepper(RectTransform controls, string name, string value, Action<int> step)
        {
            Round(controls, name + " less", true, () => { step(-1); RefreshSetting(name + " less"); });
            LobbyKit.Display(controls, value, 24, LobbyKit.Sun, TextAlignmentOptions.Center).Size(200, 40);
            Round(controls, name + " more", false, () => { step(1); RefreshSetting(name + " more"); });
        }

        static void Round(RectTransform controls, string name, bool back, Action click)
        {
            var button = LobbyKit.Button(controls, name, Color.white, click, 22, LobbyKit.Navy, 3, 3);
            button.Size(44, 44);
            var glyph = LobbyKit.Icon(button.Body(), LobbyIcons.Chevron, LobbyKit.Navy);
            glyph.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(11, 11), new Vector2(-11, -11));
            if (back) glyph.rectTransform.localRotation = Quaternion.Euler(0, 0, 180f);
            var face = button.FaceOf();
            button.GetComponent<LobbyPress>().Hot = hot => face.color = hot ? LobbyKit.Sun : Color.white;
        }

        static (FullScreenMode mode, int width, int height) Current => (Screen.fullScreenMode, Screen.width, Screen.height);

        static string DisplayId(FullScreenMode mode) => mode switch
        {
            FullScreenMode.ExclusiveFullScreen => "fullscreen",
            FullScreenMode.Windowed or FullScreenMode.MaximizedWindow => "windowed",
            _ => "borderless",
        };

        static FullScreenMode DisplayMode(string id) => id switch
        {
            "fullscreen" => FullScreenMode.ExclusiveFullScreen,
            "windowed" => FullScreenMode.Windowed,
            _ => FullScreenMode.FullScreenWindow,
        };

        static List<(int width, int height)> Sizes((FullScreenMode mode, int width, int height) now) =>
            Screen.resolutions.Select(r => (width: r.width, height: r.height)).Append((now.width, now.height)).Distinct()
                .OrderBy(s => s.width * s.height).ThenBy(s => s.width).ToList();

        void Show(FullScreenMode mode, int width, int height)
        {
            var now = Current;
            if (!revertTo.HasValue && now.mode == mode && now.width == width && now.height == height) return;
            revertTo ??= now;
            asked = (mode, width, height);
            Screen.SetResolution(width, height, mode);
            if (!timer) timer = Menu.gameObject.AddComponent<LobbyCountdown>();
            timer.Ends = Time.unscaledTime + KeepFor;
            timer.Done = Revert;
        }

        void Keep()
        {
            revertTo = null;
            Stop();
            Refresh();
            Reselect("Settings tab " + tab);
        }

        void Revert()
        {
            if (revertTo is { } back)
            {
                asked = back;
                Screen.SetResolution(back.width, back.height, back.mode);
            }
            revertTo = null;
            Stop();
            if (Root && Root.gameObject.activeSelf)
            {
                Refresh();
                Reselect("Settings tab " + tab);
            }
            else asked = null;
        }

        void Stop()
        {
            if (timer) UnityEngine.Object.Destroy(timer);
            timer = null;
        }

        void KeepBar(RectTransform page)
        {
            var bar = LobbyKit.Row(page, "Keep display", 12);
            bar.Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 80));
            var layout = bar.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(22, 14, 0, 0);
            layout.childForceExpandHeight = false;
            LobbyKit.Face(bar, LobbyKit.Panel, 16, LobbyKit.Sun, 3);
            var words = LobbyKit.Text(bar, "Keep this display?", 20, LobbyKit.Cream, TextAlignmentOptions.MidlineLeft);
            words.Size(-1, 40, 1);
            var keep = LobbyKit.Button(bar, "Keep display", LobbyKit.SunHi, Keep, 12, LobbyKit.Navy, 3, 4, LobbyKit.Sun2);
            LobbyKit.Display(keep.Body(), "KEEP", 22, LobbyKit.Navy).rectTransform.Fill();
            keep.Size(150, 52);
            LobbyKit.Pill(bar, "Revert display", "Revert", 22, Revert).Size(150, 52);
            if (timer) timer.Show(words, seconds => $"Keep this display? It goes back in {seconds} s.");
        }

        void NameField(RectTransform controls)
        {
            var holder = LobbyKit.Rect(controls, "Name field");
            holder.gameObject.SetActive(false);
            holder.Size(360, 46);
            holder.Paint(Color.white, 10);
            LobbyKit.Frame(holder, LobbyKit.Navy, 10, 3);
            var area = LobbyKit.Rect(holder, "Text area").Place(Vector2.zero, Vector2.one, new Vector2(14, 2), new Vector2(-14, -2));
            area.gameObject.AddComponent<RectMask2D>();
            var text = LobbyKit.Text(area, "", 22, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft);
            text.overflowMode = TextOverflowModes.Overflow;
            text.rectTransform.Fill();
            var field = holder.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.characterLimit = Career.NameLength;
            field.customCaretColor = true;
            field.caretColor = LobbyKit.Navy;
            field.caretWidth = 2;
            field.selectionColor = new Color(LobbyKit.Sun.r, LobbyKit.Sun.g, LobbyKit.Sun.b, .5f);
            field.text = Menu.Career.Name;
            field.inputValidator = LatinNameValidator.Create();
            field.onEndEdit.AddListener(value =>
            {
                string trimmed = Career.CleanName(value);
                if (trimmed == null || trimmed.Length < 2 || trimmed == Menu.Career.Name) { field.text = Menu.Career.Name; return; }
                Menu.Career.Name = trimmed;
                Menu.SaveCareer();
                Menu.Post("You're now " + trimmed + ".");
            });
            holder.gameObject.SetActive(true);
        }
    }

}
