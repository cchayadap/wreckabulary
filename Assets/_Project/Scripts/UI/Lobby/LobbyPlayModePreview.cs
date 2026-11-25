using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class LobbyPlayModePreview : MonoBehaviour
    {
        public const string Name = "Play preview", PhotoName = "Preview photo", ArtName = "Preview art";
        public const string ScrimName = "Preview scrim", ModeLabelName = "Preview mode", StepLabelName = "Preview step";
        public const float Aspect = 16f / 9f, StepSeconds = 2.6f, ScrimHeight = 62, ArtShare = .6f;
        const int Radius = 14;

        Image photo;
        RectTransform art, scrim;
        TextMeshProUGUI modeLabel, stepLabel;
        string selected, shown;
        int step, artStep = -1;
        string artMode;
        float nextStep;

        public string Selected => selected;
        public string Shown => shown;
        public int Step => step;
        public bool PictureShown => photo && photo.enabled && photo.sprite;

        public static LobbyPlayModePreview Create(Transform parent, string selectedMode)
        {
            LobbyPlayModePreview preview = null;
            var button = LobbyKit.Button(parent, Name, LobbyKit.Raised, () => { if (preview) preview.OpenGuide(); }, Radius, LobbyKit.Line, 1);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.gameObject.AddComponent<LobbyHint>().Text = PlayPage.GuideHint;
            preview = button.gameObject.AddComponent<LobbyPlayModePreview>();
            preview.Build(button.Body());
            preview.selected = selectedMode;
            preview.Show(selectedMode);
            return preview;
        }

        void Build(RectTransform body)
        {
            body.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            photo = LobbyKit.Rect(body, PhotoName).Fill().gameObject.AddComponent<Image>();
            photo.preserveAspect = false; photo.raycastTarget = false;
            art = LobbyKit.Rect(body, ArtName).Place(Vector2.zero, Vector2.one, new Vector2(0, ScrimHeight), Vector2.zero);
            scrim = LobbyKit.Rect(body, ScrimName).Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, ScrimHeight));
            scrim.Paint(new Color(LobbyKit.ScrimNavy.r, LobbyKit.ScrimNavy.g, LobbyKit.ScrimNavy.b, .74f)).raycastTarget = false;
            modeLabel = LobbyKit.Text(scrim, "", 20, Color.white, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            modeLabel.name = ModeLabelName;
            modeLabel.enableAutoSizing = true; modeLabel.fontSizeMin = 14; modeLabel.fontSizeMax = 20;
            modeLabel.textWrappingMode = TextWrappingModes.NoWrap; modeLabel.overflowMode = TextOverflowModes.Ellipsis;
            modeLabel.raycastTarget = false;
            modeLabel.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(12, 28), new Vector2(-12, -4));
            stepLabel = LobbyKit.Text(scrim, "", 15, new Color(1, 1, 1, .86f), TextAlignmentOptions.TopLeft);
            stepLabel.name = StepLabelName;
            stepLabel.enableAutoSizing = true; stepLabel.fontSizeMin = 12; stepLabel.fontSizeMax = 15;
            stepLabel.textWrappingMode = TextWrappingModes.NoWrap; stepLabel.overflowMode = TextOverflowModes.Ellipsis;
            stepLabel.raycastTarget = false;
            stepLabel.rectTransform.Place(Vector2.zero, new Vector2(1, 0), new Vector2(12, 4), new Vector2(-12, 28));
        }

        public void Show(string mode)
        {
            if (mode == shown && mode != null) return;
            shown = mode;
            step = 0;
            nextStep = Time.unscaledTime + StepSeconds;
            Apply();
        }

        public void ShowSelected() => Show(selected);

        public void OpenGuide()
        {
            if (!string.IsNullOrEmpty(shown) && ModeGuides.Has(shown)) ModeGuides.Show(shown);
        }

        void Update()
        {
            var guide = ModeGuideBook.For(shown);
            if (guide == null || guide.Steps.Count < 2 || Time.unscaledTime < nextStep) return;
            step = (step + 1) % guide.Steps.Count;
            nextStep = Time.unscaledTime + StepSeconds;
            Apply();
        }

        void Apply()
        {
            var guide = ModeGuideBook.For(shown);
            modeLabel.text = string.IsNullOrEmpty(shown) ? "" : LobbyPlayModeLabels.Name(shown);
            if (guide == null || guide.Steps.Count == 0)
            {
                photo.enabled = false;
                SetArt(null);
                stepLabel.text = "";
                return;
            }
            var current = guide.Steps[Mathf.Clamp(step, 0, guide.Steps.Count - 1)];
            var sprite = ModeGuides.Picture(current.Image);
            photo.sprite = sprite;
            photo.enabled = sprite;
            SetArt(sprite ? null : current.Art);
            stepLabel.text = (step + 1) + LobbyPlayDetail.StepSeparator + current.Title;
        }

        void SetArt(string item)
        {
            if (artMode == shown && artStep == step && art.childCount > 0 == (item != null)) return;
            for (int i = art.childCount - 1; i >= 0; i--) Destroy(art.GetChild(i).gameObject);
            artMode = shown; artStep = step;
            if (item == null) return;
            var image = LobbyKit.ItemImage(art, item, 160);
            if (!image) return;
            float margin = (1f - ArtShare) * .5f;
            image.rectTransform.Place(new Vector2(0, margin), new Vector2(1, 1f - margin));
            var square = image.gameObject.AddComponent<AspectRatioFitter>();
            square.aspectMode = AspectRatioFitter.AspectMode.FitInParent; square.aspectRatio = 1;
        }
    }

    public sealed class LobbyPlayModeHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public LobbyPlayModePreview Preview;
        public string Mode;

        public void OnPointerEnter(PointerEventData data) { if (Preview) Preview.Show(Mode); }
        public void OnPointerExit(PointerEventData data) { if (Preview) Preview.ShowSelected(); }
        public void OnSelect(BaseEventData data) { if (Preview) Preview.Show(Mode); }
        public void OnDeselect(BaseEventData data) { if (Preview) Preview.ShowSelected(); }
    }
}
