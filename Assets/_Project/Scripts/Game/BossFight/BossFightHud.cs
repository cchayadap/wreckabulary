using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.BossFight
{
    /// <summary>Runtime canvas for boss name slots, shield pips, HP and result. No scene/prefab edits needed.</summary>
    // @trace ART-006, UI-001
    public sealed class BossFightHud : MonoBehaviour
    {
        TextMeshProUGUI heading, words, shield, timer, clue, result, hpText, banner;
        Image hpFill;
        GameObject hpRow, resultPanel, bossBar;
        CanvasGroup canvas;
        float bannerUntil;

        static readonly Color Navy = new(.17f, .24f, .31f, .94f);
        static readonly Color Gold = new(.96f, .65f, .14f);
        static readonly Color Cream = new(.98f, .95f, .90f);

        public void Build(Transform parent, GameRules rules, BossNameCatalogue names)
        {
            var root = new GameObject("Boss Fight HUD", typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            var c = root.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 330;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvas = root.GetComponent<CanvasGroup>();
            canvas.blocksRaycasts = false;
            canvas.interactable = false;

            bossBar = Panel(root.transform, "Boss bar", new Vector2(.5f, 1f), new Vector2(.5f, 1f),
                new Vector2(0f, -46f), new Vector2(1060f, 190f), Navy);
            heading = Text(bossBar.transform, "Boss", new Vector2(0f, 62f), new Vector2(1000f, 48f), 30f, Gold, TextAlignmentOptions.Center);
            shield = Text(bossBar.transform, "Shield", new Vector2(0f, 26f), new Vector2(1000f, 36f), 24f, Cream, TextAlignmentOptions.Center);
            words = Text(bossBar.transform, "Words", new Vector2(0f, -22f), new Vector2(1000f, 46f), 34f, Cream, TextAlignmentOptions.Center);
            clue = Text(bossBar.transform, "Clue", new Vector2(0f, -68f), new Vector2(1000f, 32f), 22f, Gold, TextAlignmentOptions.Center);

            timer = Text(root.transform, "Timer", new Vector2(-70f, -40f), new Vector2(220f, 60f), 34f, Cream, TextAlignmentOptions.TopRight);
            ((RectTransform)timer.transform).anchorMin = ((RectTransform)timer.transform).anchorMax = new Vector2(1f, 1f);

            hpRow = Panel(root.transform, "HP row", new Vector2(.5f, 1f), new Vector2(.5f, 1f),
                new Vector2(0f, -252f), new Vector2(720f, 46f), new Color(.1f, .1f, .12f, .9f));
            hpText = Text(hpRow.transform, "HP text", Vector2.zero, new Vector2(680f, 38f), 22f, Cream, TextAlignmentOptions.Center);
            hpFill = new GameObject("HP fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            hpFill.transform.SetParent(hpRow.transform, false);
            var hr = (RectTransform)hpFill.transform;
            hr.anchorMin = new Vector2(0f, 0f);
            hr.anchorMax = new Vector2(0f, 1f);
            hr.pivot = new Vector2(0f, .5f);
            hr.anchoredPosition = new Vector2(2f, -16f);
            hr.sizeDelta = new Vector2(0f, 4f);
            hpFill.color = new Color(1f, .28f, .34f);
            hpFill.transform.SetAsLastSibling();
            hpRow.SetActive(false);

            resultPanel = Panel(root.transform, "Result", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(900f, 320f), Navy);
            result = Text(resultPanel.transform, "Result text", Vector2.zero, new Vector2(840f, 280f), 38f, Cream, TextAlignmentOptions.Center);
            resultPanel.SetActive(false);

            banner = Text(root.transform, "Phase banner", Vector2.zero, new Vector2(1200f, 170f),
                64f, Gold, TextAlignmentOptions.Center);
            banner.gameObject.SetActive(false);

            heading.text = names.Name;
            words.text = "Spell to break the shield";
        }

        GameObject Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 at, Vector2 size, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = anchorMin;
            r.anchorMax = anchorMax;
            r.pivot = new Vector2(.5f, .5f);
            r.anchoredPosition = at;
            r.sizeDelta = size;
            go.GetComponent<Image>().color = colour;
            return go;
        }

        TextMeshProUGUI Text(Transform parent, string name, Vector2 at, Vector2 size,
            float fontSize, Color colour, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
            r.anchoredPosition = at;
            r.sizeDelta = size;
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = GameAssets.I ? GameAssets.I.font : null;
            t.fontSize = fontSize;
            t.color = colour;
            t.alignment = align;
            t.raycastTarget = false;
            t.text = "";
            return t;
        }

        /// <summary>Boss name, shield pips and words stay secret during the 20 s prepare phase.</summary>
        // @trace AMD-006
        public void SetRevealed(bool revealed)
        {
            if (bossBar) bossBar.SetActive(revealed);
            if (hpRow && !revealed) hpRow.SetActive(false);
        }

        /// <summary>Big centre-screen callout for layer cracks and the shield break.</summary>
        // @trace AMD-009
        public void Flash(string text, Color colour, float seconds)
        {
            if (!banner) return;
            banner.text = text;
            banner.color = colour;
            banner.rectTransform.localScale = Vector3.one * .7f;
            banner.gameObject.SetActive(true);
            bannerUntil = Time.unscaledTime + seconds;
        }

        void Update()
        {
            if (!banner || !banner.gameObject.activeSelf) return;
            float t = Mathf.Clamp01((bannerUntil - Time.unscaledTime) / .35f);
            banner.rectTransform.localScale = Vector3.one * Mathf.Lerp(.7f, 1.12f, t);
            if (Time.unscaledTime >= bannerUntil) banner.gameObject.SetActive(false);
        }

        /// <summary>Called by the director at a low frequency; all strings are built by the caller.</summary>
        public void Refresh(string shieldText, string wordText, string clueText, string timeText,
            float health01, bool showHealth, string resultText)
        {
            shield.text = shieldText;
            words.text = wordText;
            clue.text = clueText;
            timer.text = timeText;
            hpRow.SetActive(showHealth);
            if (showHealth)
            {
                hpFill.rectTransform.sizeDelta = new Vector2(716f * Mathf.Clamp01(health01), 4f);
                hpText.text = "CALCULATOR  " + Mathf.CeilToInt(health01 * 100f) + "%";
            }
            resultPanel.SetActive(!string.IsNullOrEmpty(resultText));
            if (!string.IsNullOrEmpty(resultText)) result.text = resultText;
        }
    }
}
