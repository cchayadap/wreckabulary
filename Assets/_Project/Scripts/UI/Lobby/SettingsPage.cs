using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Voice;
using Wreckabulary.Voice;

namespace Wreckabulary
{
    public sealed class SettingsPage : LobbyPage
    {
        static readonly (string id, string title)[] Tabs =
            { ("profile", "Profile"), ("audio", "Audio"), ("gameplay", "Gameplay"), ("graphics", "Graphics"), ("controls", "Controls"), ("help", "Help") };
        static readonly (string id, string label)[] DisplayModes =
            { ("borderless", "Borderless"), ("fullscreen", "Full screen"), ("windowed", "Windowed") };
        public static readonly (string id, string label)[] ShakeChoices =
            { (ShakeLevel.Off.ToString(), "Off"), (ShakeLevel.Low.ToString(), "Low"), (ShakeLevel.Full.ToString(), "Full") };
        public static readonly string[] GameplayRows =
            { "Screen shake", "Damage numbers", "Effect words", "Furniture names", "Control tips", "Points per hit", "Combat feedback", "Reduce theme motion" };
        public static readonly string[] VoiceRows = new[]
        {
            "Voice chat", "Voice status", "Voice echo hint", "Microphone", "Push-to-talk key", "Microphone device", "Test microphone", "Hear yourself",
            VoiceChat.QaTools ? VoiceChat.SimulateRow : null, "Voice volume", "Hear teammates",
        }.Where(row => row != null).ToArray();
        static readonly (string id, string label)[] VoiceMics =
            { (VoiceSettings.MicId(MicMode.Off), "Off"), (VoiceSettings.MicId(MicMode.PushToTalk), "Push to talk"), (VoiceSettings.MicId(MicMode.OpenMic), "Open mic") };
        const float KeepFor = 10f;
        public const string HelpPlay = "Got it", HelpActions = "Help actions";
        public const float PlayWidth = 212f, PlayHeight = 60f;
        const float PlayWord = 42f, PlayArrow = 26f, HelpViewWidth = 220f, ActionHeight = 46f, HelpGap = 12f;

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
        LobbySettingsPageView authoredFrame;

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
            authoredFrame = null;
            var saved = Resources.Load<LobbySettingsLayout>(LobbySettingsLayout.ResourcePath);
            RectTransform body;
            if (saved)
            {
                saved.Validate();
                authoredFrame = LobbyRuntimeTypography.Instantiate(saved.Page, Root, false);
                authoredFrame.Bind(Menu.Close, revertTo.HasValue);
                authoredFrame.FitHeight(LobbySettingsPageView.PanelHeight);
                body = authoredFrame.Body;
            }
            else body = Panel(new Vector2(.12f, 0), new Vector2(.88f, 1), "Settings");
            var side = authoredFrame ? authoredFrame.Tabs : LobbyKit.Column(body, "Tabs", 8);
            if (!authoredFrame) side.Place(Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(230, 0));
            foreach (var (id, title) in Tabs)
            {
                string name = "Settings tab " + id;
                var chip = LobbyKit.Chip(side, title, tab == id, () => ShowTab(id), size: 21);
                chip.onClick.AddListener(() => Reselect(name));
                chip.name = name;
                chip.Size(-1, 50);
                if (tab == id) First = chip;
            }
            var page = authoredFrame ? authoredFrame.Content : LobbyKit.Rect(body, "Tab " + tab).Place(Vector2.zero, Vector2.one, new Vector2(254, 0), Vector2.zero);
            var holder = LobbyKit.Rect(page, "Tab content").Fill();
            if (revertTo.HasValue && !authoredFrame) holder.offsetMin = new Vector2(0, 92);
            if (tab == "controls")
            {
                controlsPanel = ControlsPanel.Mount(TouchLayouts.Available ? TouchLayoutRow(holder) : holder);
                scroll = controlsPanel.GetComponentInChildren<ScrollRect>();
                First = controlsPanel.FirstSelectable;
            }
            else
            {
                var list = LobbyKit.Scroll(holder, "Scroll", 6);
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
            track.Paint(LobbyKit.Track, 8);
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
            NameField(Setting(list, "Player name", "Shown to other players."));
            Pill(Setting(list, "Appearance"), "Open loadout", "Customize", 20,
                () => Menu.Open(LobbyMenu.Loadout)).Size(220, 46);
        }

        void Gameplay(Transform list)
        {
            Choose(Setting(list, "Screen shake", "How much the camera shakes on big hits."), "Screen shake", ShakeChoices,
                ScreenShake.Level.ToString(), id => ScreenShake.Level = (ShakeLevel)Enum.Parse(typeof(ShakeLevel), id), 330);
            Toggle(Setting(list, "Damage numbers", "A number pops up on every hit."), "Damage numbers", GameplayPrefs.DamageNumbers, on => GameplayPrefs.DamageNumbers = on);
            Toggle(Setting(list, "Effect words", "Words like CRUSH! and DAZED on special hits."), "Effect words", GameplayPrefs.EffectWords, on => GameplayPrefs.EffectWords = on);
            Toggle(Setting(list, "Furniture names", "Word labels over the furniture you can smash."), "Furniture names", GameplayPrefs.FurnitureNames, on => GameplayPrefs.FurnitureNames = on);
            Toggle(Setting(list, "Control tips", "One tip at a time until you've used each control. Turn on again to replay them."),
                "Control tips", GameplayPrefs.ControlTips, on => GameplayPrefs.ControlTips = on);
            Toggle(Setting(list, "Points per hit"), "Points per hit", CombatFeedbackOptions.ShowPoints, on => CombatFeedbackOptions.ShowPoints = on);
            Pill(Setting(list, "Combat feedback"), "Reset feedback", "Reset feedback", 20,
                () => { CombatFeedbackOptions.ResetToDefaults(); RefreshSetting("Reset feedback"); }).Size(220, 46);
            Toggle(Setting(list, "Reduce theme motion"), "Reduce theme motion", PlayerPrefs.GetInt("wv.theme.effects", 1) == 0, on =>
            {
                PlayerPrefs.SetInt("wv.theme.effects", on ? 0 : 1);
                PlayerPrefs.Save();
                if (Menu.Stage) Menu.Stage.SetThemeEffects(!on);
            });
        }

        RectTransform TouchLayoutRow(RectTransform holder)
        {
            var controls = Setting(holder, "Touch layout", "Move, resize and fade the on-screen buttons.");
            ((RectTransform)controls.parent).Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -92), Vector2.zero);
            var book = TouchLayouts.Book;
            LobbyKit.Text(controls, book.Name(book.Active), 20, LobbyKit.Muted, TextAlignmentOptions.MidlineRight).Size(170, 40);
            Pill(controls, "Edit touch layout", "Edit layout", 20, () => TouchLayoutEditor.Show(() =>
            {
                if (Root && Root.gameObject.activeInHierarchy) RefreshSetting("Edit touch layout");
            })).Size(200, 46);
            return LobbyKit.Rect(holder, "Below touch layout").Place(Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -104));
        }

        void Audio(Transform list)
        {
            Toggle(Setting(list, "Sound"), "Sound", !GameFeedback.Muted, on => GameFeedback.Muted = !on);
            Choose(Setting(list, "Sound pack"), "Sound pack", GameSoundPacks.Ids.Select(id => (id, GameSoundPacks.Label(id))).ToList(),
                GameSoundPacks.Selected, id => GameSoundPacks.Select(id), 340);
            Pill(Setting(list, "Listen"), "Preview sound pack", "Preview", 20, () => GameSoundPacks.Preview()).Size(220, 46);
            Stepper(Setting(list, "Volume"), "Volume", Mathf.RoundToInt(AudioListener.volume * 100) + "%", step =>
            {
                AudioListener.volume = Mathf.Clamp01(Mathf.Round(AudioListener.volume * 10f + step) / 10f);
                PlayerPrefs.SetFloat(LobbyMenu.VolumeKey, AudioListener.volume);
                PlayerPrefs.Save();
            });
            Pill(Setting(list, "Defaults"), "Reset audio", "Reset audio", 20, () =>
            {
                GameFeedback.Muted = false;
                GameSoundPacks.Select("default");
                AudioListener.volume = 1f;
                PlayerPrefs.DeleteKey(LobbyMenu.VolumeKey);
                PlayerPrefs.Save();
                RefreshSetting("Reset audio");
            }).Size(220, 46);
            VoiceChatGroup(list);
        }

        void VoiceChatGroup(Transform list)
        {
            var heading = LobbyKit.Text(list, "Voice chat", 22, LobbyKit.Cyan, TextAlignmentOptions.BottomLeft);
            heading.name = "Voice chat";
            heading.Size(-1, 44);
            Wrapped(list, VoiceChat.Status, 17, LobbyKit.Muted, 30).name = "Voice status";
            Wrapped(list, VoiceChat.EchoHint, 17, LobbyKit.Muted, 30).name = "Voice echo hint";
            Choose(Setting(list, "Microphone", "Off until you turn it on."), "Microphone", VoiceMics,
                VoiceSettings.MicId(VoiceChat.Mic), id => VoiceChat.Mic = VoiceSettings.ParseMic(id), 480);
            var key = Setting(list, "Push-to-talk key", "Hold it to talk. Controller: hold D-pad up.");
            var shown = LobbyKit.Text(key, VoiceChat.PushToTalkKey, 22, LobbyKit.Navy, TextAlignmentOptions.MidlineRight);
            shown.name = "Push-to-talk key name";
            shown.enableAutoSizing = true; shown.fontSizeMin = 14; shown.fontSizeMax = 22;
            shown.Size(120, 40);
            Pill(key, "Change push-to-talk key", "Change", 20, () => ShowTab("controls")).Size(160, 46);
            var devices = new[] { "" }.Concat(VoiceChat.Devices).Distinct().ToList();
            int device = Mathf.Max(0, devices.IndexOf(VoiceChat.Device));
            Stepper(Setting(list, "Microphone device"), "Mic device", VoiceSettings.ShortDevice(devices[device]),
                step => VoiceChat.Device = devices[(device + step + devices.Count) % devices.Count]);
            var test = VoiceTest;
            var controls = Setting(list, "Test microphone", test.Status);
            var words = ((RectTransform)controls.parent).Find("Words");
            var status = words.GetChild(words.childCount - 1).GetComponent<TMP_Text>();
            status.name = "Mic test status";
            var meter = LobbyKit.Rect(controls, "Mic level").Size(160, 14);
            meter.Paint(LobbyKit.Track, 7);
            var fill = LobbyKit.Rect(meter, "Fill").Fill();
            fill.anchorMax = new Vector2(0, 1);
            fill.Paint(LobbyKit.Cyan, 7);
            var button = Pill(controls, "Test microphone", VoiceTestView.StartLabel, 20, () => test.Toggle()).Size(140, 46);
            VoiceTestView.Bind(controls.gameObject, test, fill, status, button.GetComponentInChildren<TMP_Text>());
            Toggle(Setting(list, "Hear yourself", "Plays your mic back to you."), "Hear yourself", test.HearYourself, on => test.HearYourself = on);
            if (VoiceChat.QaTools)
                Toggle(Setting(list, VoiceChat.SimulateRow, VoiceChat.SimulateHelp), VoiceChat.SimulateRow, test.SimulateBadConnection,
                    on => test.SimulateBadConnection = on);
            Stepper(Setting(list, "Voice volume", "Separate from game volume."), "Voice volume", VoiceChat.Volume + "%", VoiceChat.StepVolume);
            Toggle(Setting(list, "Hear teammates", VoiceChat.SpeakersNote + "."), "Hear teammates", VoiceChat.HearTeammates, on => VoiceChat.HearTeammates = on);
        }

        LocalVoiceTest VoiceTest => Root.TryGetComponent(out LocalVoiceTest test) ? test : Root.gameObject.AddComponent<LocalVoiceTest>();

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
            Choose(Setting(list, "Quality", preset == GraphicsOptions.Custom ? "Your saved custom settings." : "Detail, shadows and edge smoothing."),
                "Preset", GraphicsOptions.Presets.Select(p => (p, p)).ToList(), preset, GraphicsOptions.UsePreset, 520);
            Stepper(Setting(list, "Render scale", "Lower for speed; higher for detail."), "Render scale",
                Mathf.RoundToInt(GraphicsOptions.RenderScale * 100) + "%",
                step => GraphicsOptions.SetRenderScale(GraphicsOptions.RenderScale + step * GraphicsOptions.ScaleStep));
            Choose(Setting(list, "Edge smoothing"), "AA",
                GraphicsOptions.Smoothings.Select(s => (s, s.StartsWith("MSAA") ? "MSAA " + s.Substring(4) + "x" : s)).ToList(),
                GraphicsOptions.Smoothing, GraphicsOptions.SetSmoothing, 620);
            Choose(Setting(list, "Shadows"), "Shadows",
                GraphicsOptions.ShadowLevels.Select(s => (s, s)).ToList(), GraphicsOptions.Shadows, GraphicsOptions.SetShadows, 560);
            Choose(Setting(list, "Frame limit", "V-sync follows your display's refresh rate."), "Cap",
                GraphicsOptions.FrameCaps.Select(c => (c.ToString(CultureInfo.InvariantCulture), c == 0 ? "None" : c.ToString(CultureInfo.InvariantCulture))).ToList(),
                GraphicsOptions.FrameCap.ToString(CultureInfo.InvariantCulture), id => GraphicsOptions.SetFrameCap(int.Parse(id, CultureInfo.InvariantCulture)), 520);
            Toggle(Setting(list, "V-sync", "Reduces screen tearing."), "V-sync", QualitySettings.vSyncCount > 0, on =>
            {
                QualitySettings.vSyncCount = on ? 1 : 0;
                PlayerPrefs.SetInt(LobbyMenu.VsyncKey, on ? 1 : 0);
                PlayerPrefs.Save();
            });
            var reset = Pill(Setting(list, "Defaults", "High, no frame cap, v-sync off."), "Reset graphics", "Reset", 20, () =>
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
            var kicker = LobbyKit.Text(column, "House games", 18, LobbyKit.Cyan, TextAlignmentOptions.BottomLeft);
            kicker.name = "Help scope";
            kicker.color = LobbyKit.Cyan;
            kicker.Size(-1, 32);
            Wrapped(column, "Course controls: Controls tab → Courses.", 17, LobbyKit.Muted, 42);
            LobbyKit.Text(column, "Smash. Spell. Play.", 28, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft).Size(-1, 42);
            var steps = LobbyKit.Column(column, "Steps", 8);
            steps.Size(-1, 352);
            var rules = GameConfig.Current.Rules.Defaults;
            int recipes = GameConfig.Current.Items.Enabled.Count();
            bool phone = Application.isMobilePlatform;
            var keys = DesktopBinding.Shared;
            string spell = phone ? "CRAFT" : ControlHints.KeyOf(keys.SpellBox);
            string swing = phone ? "SMASH" : ControlHints.KeyOf(keys.Attack);
            Step(steps, "01", "Break furniture", "HAMMER", "A TABLE becomes T, A, B, L and E. Walk over the letters to collect them.");
            Step(steps, "02", "Spell equipment", "BOOK", $"Press {spell} to choose from {recipes} recipes. Your bag holds {rules.MaxLetters} letters.");
            Step(steps, "03", "Use what you make", "BAT", phone
                ? $"Press {swing} to use your gear. Aim to throw it, or place a useful tool."
                : $"Press {swing} to use your gear. Hold {swing} to aim a throw, or place a useful tool.");
            Wrapped(column, "Cosmetics never change your stats.", 17, LobbyKit.Muted, 26);
            var actions = LobbyKit.Row(column, HelpActions, HelpGap);
            actions.Size(-1, PlayHeight);
            actions.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            var controls = LobbyKit.TextAction(actions, "Open help controls", "View controls", 20, () => ShowTab("controls"));
            LobbyChoiceShape.Action(controls);
            controls.Size(HelpViewWidth, ActionHeight);
            LobbyKit.Rect(actions, "Space").Size(-1, 1, 1);
            LobbyKit.Primary(actions, HelpPlay, "Play", () => Menu.Open(LobbyMenu.Play), PlayWord, PlayArrow).Size(PlayWidth, PlayHeight);
        }

        static void Step(Transform row, string number, string title, string art, string text)
        {
            var card = LobbyKit.Rect(row, "Step " + number);
            card.Size(-1, 112);
            var image = LobbyKit.ItemImage(card, art, 60);
            if (image) image.rectTransform.Pin(new Vector2(0, .5f), new Vector2(8, 0), new Vector2(60, 60));
            LobbyKit.Text(card, title, 23, LobbyKit.Navy, TextAlignmentOptions.TopLeft).rectTransform
                .Place(new Vector2(0, 1), Vector2.one, new Vector2(88, -48), new Vector2(-8, -8));
            var body = Wrapped(card, text, 17, LobbyKit.Muted);
            body.enableAutoSizing = true; body.fontSizeMin = 16; body.fontSizeMax = 17;
            body.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(88, 8), new Vector2(-8, -48));
            LobbyKit.Rect(card, "Divider").Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 1))
                .Paint(LobbyKit.Line).raycastTarget = false;
        }

        static RectTransform Setting(Transform list, string label, string help = null)
        {
            var row = LobbyKit.Rect(list, label);
            row.Size(-1, help == null ? 64 : 92);
            row.Paint(LobbyKit.Card, 10).raycastTarget = false;
            var words = LobbyKit.Column(row, "Words", 0);
            words.Place(Vector2.zero, new Vector2(.36f, 1), new Vector2(8, 6), new Vector2(-12, -6));
            words.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            var text = LobbyKit.Text(words, label, 22, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft);
            text.Size(-1, 40);
            if (help != null)
            {
                var hint = LobbyKit.Text(words, help, 15, LobbyKit.Muted, TextAlignmentOptions.TopLeft);
                hint.textWrappingMode = TextWrappingModes.Normal;
                hint.enableAutoSizing = true; hint.fontSizeMin = 13; hint.fontSizeMax = 15;
                hint.Size(-1, 40);
            }
            var controls = LobbyKit.Row(row, "Controls", 8);
            controls.Place(new Vector2(.36f, 0), Vector2.one, new Vector2(0, 8), new Vector2(-8, -8));
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
            }, ActionHeight, 17);
            LobbyChoiceShape.Choices(track, prefix + " " + picked);
            track.Size(Mathf.Min(width, 480), ActionHeight);
            track.GetComponent<LayoutElement>().minWidth = Mathf.Min(width, 48f * choices.Count);
        }

        void Toggle(RectTransform controls, string name, bool value, Action<bool> set)
        {
            foreach (bool option in new[] { true, false })
            {
                string id = name + (option ? " on" : " off");
                var chip = LobbyKit.Chip(controls, option ? "On" : "Off", value == option, () => { set(option); RefreshSetting(id); }, size: 20);
                chip.name = id;
                chip.Size(96, ActionHeight);
                LobbyChoiceShape.Choice(chip, value == option);
            }
        }

        static Button Pill(Transform parent, string name, string text, float size, Action click)
        {
            var button = LobbyKit.Pill(parent, name, text, size, click);
            LobbyChoiceShape.Action(button);
            return button;
        }

        void Stepper(RectTransform controls, string name, string value, Action<int> step)
        {
            Round(controls, name + " less", true, () => { step(-1); RefreshSetting(name + " less"); });
            LobbyKit.Text(controls, value, 23, LobbyKit.Navy, TextAlignmentOptions.Center).Size(180, 40);
            Round(controls, name + " more", false, () => { step(1); RefreshSetting(name + " more"); });
        }

        static void Round(RectTransform controls, string name, bool back, Action click)
        {
            var button = LobbyKit.Button(controls, name, LobbyKit.Raised, click, 22, LobbyKit.Line, 1, 0);
            button.Size(44, 44);
            var glyph = LobbyKit.Icon(button.Body(), LobbyIcons.Chevron, LobbyKit.Navy);
            glyph.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(11, 11), new Vector2(-11, -11));
            if (back) glyph.rectTransform.localRotation = Quaternion.Euler(0, 0, 180f);
            var face = button.FaceOf();
            button.GetComponent<LobbyPress>().Hot = hot => face.color = hot ? LobbyKit.Picked : LobbyKit.Raised;
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
            var bar = LobbyKit.Row(authoredFrame ? authoredFrame.Confirmation : page, "Keep display", 12);
            if (authoredFrame) bar.Fill();
            else bar.Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 80));
            var layout = bar.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(22, 14, 0, 0);
            layout.childForceExpandHeight = false;
            LobbyKit.Face(bar, LobbyKit.Card, 8);
            var words = LobbyKit.Text(bar, "Keep this display?", 20, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft);
            words.Size(-1, 40, 1);
            var keep = LobbyKit.Button(bar, "Keep display", LobbyKit.PrimaryFill, Keep, 6);
            LobbyKit.Text(keep.Body(), "Keep", 22, LobbyKit.PrimaryInk, TextAlignmentOptions.Center).rectTransform.Fill();
            keep.Size(150, 52);
            Pill(bar, "Revert display", "Revert", 22, Revert).Size(150, 52);
            if (timer) timer.Show(words, seconds => $"Keep this display? It goes back in {seconds} s.");
        }

        void NameField(RectTransform controls)
        {
            var holder = LobbyKit.Rect(controls, "Name field");
            holder.gameObject.SetActive(false);
            holder.Size(320, 46);
            holder.Paint(LobbyKit.Raised, 6);
            LobbyKit.Frame(holder, LobbyKit.Line, 6, 1);
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
            field.selectionColor = new Color(LobbyKit.Selection.r, LobbyKit.Selection.g, LobbyKit.Selection.b, .5f);
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
