using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wreckabulary
{
    public partial class GameHud
    {
        RectTransform pauseSettings, pauseSettingsContent;
        CanvasGroup pauseSettingsGroup;
        ControlsPanel pauseControls;

        public bool PauseSettingsShown => pauseSettingsGroup && pauseSettingsGroup.interactable;

        void ShowPauseSettings(string tab)
        {
            if (!Paused) return;
            if (pauseControls) pauseControls.Close();
            if (!pauseSettings)
            {
                pauseSettings = Rect("Pause settings", pauseShade, new Vector2(.5f, .5f), Vector2.zero, new Vector2(1100f, 820f));
                LobbyKit.PanelFace(pauseSettings, 28, 3, 6).raycastTarget = true;
                pauseSettingsGroup = pauseSettings.gameObject.AddComponent<CanvasGroup>();
                pauseSettingsContent = Rect("Content", pauseSettings, Vector2.zero, Vector2.zero, Vector2.zero);
                Stretch(pauseSettingsContent);
            }
            LobbyKit.Clear(pauseSettingsContent);
            pauseControls = null;
            pauseCard.SetActive(false);
            helpCard.SetActive(false);
            pauseSettingsGroup.alpha = 1f;
            pauseSettingsGroup.interactable = pauseSettingsGroup.blocksRaycasts = true;
            pauseSettings.SetAsLastSibling();
            FitPauseSettings();

            var heading = LobbyKit.Display(pauseSettingsContent, "SETTINGS", 34, LobbyKit.Sun, TextAlignmentOptions.MidlineLeft);
            heading.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(30, -64), new Vector2(-180, -12));
            var back = LobbyKit.Pill(pauseSettingsContent, "Settings back", "BACK", 20, HandlePauseBack);
            ((RectTransform)back.transform).Place(Vector2.one, Vector2.one, new Vector2(-164, -60), new Vector2(-28, -16));
            var tabs = LobbyKit.Row(pauseSettingsContent, "Tabs", 12);
            tabs.Place(new Vector2(0, 1), Vector2.one, new Vector2(28, -124), new Vector2(-28, -72));
            foreach (var id in new[] { "controls", "audio", "graphics" })
            {
                string selected = id;
                var button = LobbyKit.Chip(tabs, id.ToUpperInvariant(), tab == id, () => ShowPauseSettings(selected));
                button.name = "Pause tab " + id;
                button.Size(210, 48);
            }
            var body = LobbyKit.Rect(pauseSettingsContent, "Settings body").Place(Vector2.zero, Vector2.one, new Vector2(28, 24), new Vector2(-28, -142));
            Selectable first = back;
            if (tab == "controls")
            {
                pauseControls = ControlsPanel.Mount(body);
                first = pauseControls.FirstSelectable;
            }
            else
            {
                var list = LobbyKit.Scroll(body, "Settings scroll", 14);
                if (tab == "audio") BuildPauseAudio(list);
                else BuildPauseGraphics(list);
                var choices = list.GetComponentsInChildren<Selectable>();
                if (choices.Length > 0) first = choices[0];
            }
            if (EventSystem.current && first) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        void FitPauseSettings()
        {
            if (!pauseSettings || !safe) return;
            pauseSettings.sizeDelta = new Vector2(Mathf.Min(1100f, safe.rect.width - 48f), Mathf.Min(820f, safe.rect.height - 48f));
        }

        void HandlePauseBack()
        {
            if (pauseControls && pauseControls.CancelRebind()) return;
            if (PauseSettingsShown)
            {
                ClosePauseSettings();
                ShowHelp(false);
            }
            else if (HelpShown) ShowHelp(false);
            else SetPaused(false);
        }

        void ClosePauseSettings()
        {
            // A CanvasGroup hide does not disable children or release their gameplay-input lease.
            if (pauseControls) pauseControls.Close();
            if (!pauseSettingsGroup) return;
            pauseSettingsGroup.alpha = 0f;
            pauseSettingsGroup.interactable = pauseSettingsGroup.blocksRaycasts = false;
        }

        void BuildPauseAudio(Transform list)
        {
            PauseToggle(list, "Sound", () => !GameFeedback.Muted, enabled => GameFeedback.Muted = !enabled);
            PauseStepper(list, "Sound pack", () => GameSoundPacks.Label(GameSoundPacks.Selected), step =>
                GameSoundPacks.Select(Next(GameSoundPacks.Ids, GameSoundPacks.Selected, step)));
            PauseAction(list, "Preview sound pack", "PREVIEW SOUND", () => GameSoundPacks.Preview());
            PauseStepper(list, "Volume", () => Mathf.RoundToInt(AudioListener.volume * 100f) + "%", step =>
            {
                AudioListener.volume = Mathf.Clamp01(Mathf.Round(AudioListener.volume * 10f + step) / 10f);
                PlayerPrefs.SetFloat(LobbyMenu.VolumeKey, AudioListener.volume);
                PlayerPrefs.Save();
            });
            PauseAction(list, "Reset audio", "RESET AUDIO", () =>
            {
                GameFeedback.Muted = false;
                GameSoundPacks.Select("default");
                AudioListener.volume = 1f;
                PlayerPrefs.DeleteKey(LobbyMenu.VolumeKey);
                PlayerPrefs.Save();
                ShowPauseSettings("audio");
            });
        }

        void BuildPauseGraphics(Transform list)
        {
            PauseStepper(list, "Quality preset", () => GraphicsOptions.Preset, step =>
                GraphicsOptions.UsePreset(Next(GraphicsOptions.Presets, GraphicsOptions.Preset, step)));
            PauseStepper(list, "Render scale", () => Mathf.RoundToInt(GraphicsOptions.RenderScale * 100f) + "%", step =>
                GraphicsOptions.SetRenderScale(GraphicsOptions.RenderScale + step * GraphicsOptions.ScaleStep));
            PauseStepper(list, "Anti-aliasing", () => GraphicsOptions.Smoothing, step =>
                GraphicsOptions.SetSmoothing(Next(GraphicsOptions.Smoothings, GraphicsOptions.Smoothing, step)));
            PauseStepper(list, "Shadows", () => GraphicsOptions.Shadows, step =>
                GraphicsOptions.SetShadows(Next(GraphicsOptions.ShadowLevels, GraphicsOptions.Shadows, step)));
            PauseStepper(list, "Frame cap", () => GraphicsOptions.FrameCap == 0 ? "UNLIMITED" : GraphicsOptions.FrameCap.ToString(), step =>
                GraphicsOptions.SetFrameCap(Next(GraphicsOptions.FrameCaps, GraphicsOptions.FrameCap, step)));
            PauseToggle(list, "V-sync", () => QualitySettings.vSyncCount > 0, enabled =>
            {
                QualitySettings.vSyncCount = enabled ? 1 : 0;
                PlayerPrefs.SetInt(LobbyMenu.VsyncKey, QualitySettings.vSyncCount);
                PlayerPrefs.Save();
            });
            PauseAction(list, "Reset graphics", "RESET GRAPHICS", () =>
            {
                GraphicsOptions.ResetToDefaults();
                QualitySettings.vSyncCount = 0;
                PlayerPrefs.DeleteKey(LobbyMenu.VsyncKey);
                PlayerPrefs.Save();
                ShowPauseSettings("graphics");
            });
        }

        static T Next<T>(T[] values, T current, int step) => values[Mathf.Clamp(Array.IndexOf(values, current) + step, 0, values.Length - 1)];

        static RectTransform PauseSetting(Transform list, string name)
        {
            var row = LobbyKit.Row(list, name, 12, 14);
            row.Size(-1, 76);
            row.Paint(LobbyKit.Card, 12).raycastTarget = false;
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            LobbyKit.Text(row, name, 24, LobbyKit.Cream, TextAlignmentOptions.MidlineLeft).Size(-1, 46, 1);
            return row;
        }

        void PauseStepper(Transform list, string name, Func<string> value, Action<int> change)
        {
            var row = PauseSetting(list, name);
            TextMeshProUGUI number = null;
            void Step(int amount)
            {
                change(amount);
                number.text = value();
                // Presets also change the scale, edge smoothing and shadow values shown in other rows.
                if (list.parent) RefreshPauseGraphicsLabels(list);
            }
            PauseAction(row, name + " less", "−", () => Step(-1), 52);
            number = LobbyKit.Text(row, value(), 24, LobbyKit.Sun).Size(220, 46);
            number.name = "Value";
            PauseAction(row, name + " more", "+", () => Step(1), 52);
        }

        void PauseToggle(Transform list, string name, Func<bool> value, Action<bool> change)
        {
            var row = PauseSetting(list, name);
            Button button = null;
            button = PauseAction(row, name + " toggle", value() ? "ON" : "OFF", () =>
            {
                change(!value());
                button.GetComponentInChildren<TMP_Text>().text = value() ? "ON" : "OFF";
            }, 336);
        }

        static Button PauseAction(Transform parent, string name, string text, Action action, float width = -1)
        {
            var button = LobbyKit.Pill(parent, name, text, 22, action);
            button.Size(width, 48);
            return button;
        }

        static void RefreshPauseGraphicsLabels(Transform list)
        {
            foreach (var (row, value) in new[]
            {
                ("Quality preset", GraphicsOptions.Preset), ("Render scale", Mathf.RoundToInt(GraphicsOptions.RenderScale * 100f) + "%"),
                ("Anti-aliasing", GraphicsOptions.Smoothing), ("Shadows", GraphicsOptions.Shadows),
                ("Frame cap", GraphicsOptions.FrameCap == 0 ? "UNLIMITED" : GraphicsOptions.FrameCap.ToString()),
            })
            {
                var label = list.Find(row + "/Value");
                if (label) label.GetComponent<TMP_Text>().text = value;
            }
        }
    }
}
