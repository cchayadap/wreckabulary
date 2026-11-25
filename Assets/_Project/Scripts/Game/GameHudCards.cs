using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class HudResult
    {
        public int Round;
        public bool Final, Won;
        public string Heading = "", Detail = "";
        public string Eyebrow = "", Flavour = "", NextLabel = "", HomeLabel = "";
        public bool Stats = true;
        public string Winner = "";
        public bool Draw;
        public string[] Lines;
        public int Broken, Crafted, Damage;
        public MatchRecord Reward;
        public bool Best;
        public MatchEndBoard Board;
        public RoundSummary Standings;

        public static HudResult Of(int round, bool final, bool won, string heading, MatchRecord reward)
        {
            return new HudResult
            {
                Round = round, Final = final, Won = won, Heading = heading ?? "", Reward = reward,
                Best = reward != null && MatchTally.LastWasBest,
                Broken = reward != null ? reward.Broken : MatchTally.RoundBroken,
                Crafted = reward != null ? reward.Crafted : MatchTally.RoundCrafted,
                Damage = reward != null ? reward.Damage : MatchTally.RoundDamage,
            };
        }
    }

    public partial class GameHud
    {
        static readonly Color Primary = Hex(0x173b3c), PrimaryLip = Hex(0x102b2c), PrimaryHover = Hex(0x245754);
        static readonly Color Quiet = Hex(0xfff0d5, .9f), QuietHover = Hex(0xfff7e8), Hair = Hex(0x173b3c, .14f);
        static readonly Color ModalShade = Hex(0x103532, .62f), FocusRing = Hex(0xf08a4b);
        static readonly Color CardFace = Hex(0xfff0d9), CardLip = Hex(0xbba682), CardDrop = Hex(0x102d2c, .33f);
        static readonly Color Spark = Hex(0xcd7750), Eyebrow = Hex(0xb86647), BodyInk = Hex(0x697a6c), NewBest = Hex(0xff5a1f), CoinGold = Hex(0xe0a43a);
        static readonly Color ToastFill = Hex(0x173b3c, .94f), ToastEdge = Hex(0xcadac6, .33f), ToastInk = Hex(0xfff1d8), ToastDrop = Hex(0x173b3c, .3f);
        static readonly Color ReadyEdge = Hex(0x5bb48e);
        static readonly Color ReadyInk = Hex(0x2f7a5c), Bad = Hex(0xb3533b), TileLine = Hex(0xdbc38f);
        static readonly Color MissingFill = Hex(0xf3d3c6), MissingEdge = Hex(0xe3a994), MissingLip = Hex(0xc98f7c), Go = Hex(0xdf7955);
        static readonly Color StepNumber = Hex(0xbe7754), StepInk = Hex(0x576f66), FinePrint = Hex(0x546763);

        const float ToastFont = 20f, ToastMinFont = 15f, ToastPadX = 25f, ToastPadY = 15f, ToastMaxWidth = 760f, BannerSeconds = 3.2f;
        const float CraftStatusFont = 20f, CraftStatusMinFont = 15f, CraftStatusHeight = 30f;
        const float ComposerPad = 20f, ComposerRowGap = 10f, ComposerWellHeight = 75f, ComposerLineMax = 610f, ComposerEdgeRoom = 32f;
        const float ComposerTileWidth = 47.5f, ComposerTileHeight = 55f, ComposerTileGap = 7.5f, ComposerArrowRoom = 42f, ComposerLetterFont = 32f;
        const float ComposerTapWidth = 104f, ComposerCloseSize = 40f, ComposerSpawnY = 262f, ComposerHintFont = 15f, ComposerCaretWidth = 4f, ComposerLineOutline = .26f;
        const int ComposerTileRadius = 9, ComposerSlotStroke = 2;
        const float StatLabelFont = 15f, StatLabelSpacing = 4f;
        const float RewardChipHeight = 64f, RewardChipPad = 22f, RewardSpacing = 14f, RewardCoin = 40f, RewardLip = 5f, RewardCoinsFont = 36f, RewardLineFont = 20f;
        const int RewardRadius = 32;
        const float PreviousArrowTurn = 180f;

        RectTransform toast;
        CanvasGroup toastFade;
        TextMeshProUGUI toastLabel;
        string toastAfterBanner;
        float toastAt = -10f;
        string lastTitle = "", lastSub = "";
        RectTransform chip;
        Image chipDisc;
        TextMeshProUGUI chipDigit, chipLine;
        int chipLeft = -1;
        float chipPulseAt = -10f, goUntil;
        GameObject modeActions;
        CanvasGroup modeActionsGroup;
        TextMeshProUGUI modePlayLabel;
        Action modeAction;
        bool modeWanted;
        GameObject pauseCard, helpCard;
        TextMeshProUGUI pauseBlurb;
        Button resumeButton, helpDone;
        GameObject resultRoot;
        RectTransform resultShade;
        Image resultSpark;
        TextMeshProUGUI resultEyebrow, resultHeading, resultFlavour, resultDetail, resultPlayers, resultNextLabel, resultHomeLabel;
        readonly TextMeshProUGUI[] resultStats = new TextMeshProUGUI[3];
        GameObject resultReward, resultRewardGap, resultBest, resultStatsRow, resultStatsGap, resultPlayersGap;
        TextMeshProUGUI resultCoins, resultScore, resultBestText;
        RectTransform rewardChip;
        Button resultNext, resultHome, pauseHome, howToPlay;
        ModeGuidePanel guide;
        Action resultNextAction, resultHomeAction;
        bool resultFinal;
        Coroutine resultSoon;
        RectTransform banner;
        CanvasGroup bannerFade;
        TextMeshProUGUI bannerTitle, bannerLine;
        float bannerAt = -10f;
        GameObject leaveRoot;
        Button leaveKeep;
        Button leaveReturnFocus;
        Action leaveAction;
        Sprite sparkSprite;
        RectTransform composer, composerTiles, composerWell, caret;
        readonly ComposerTile[] tiles = new ComposerTile[TrayTiles];
        GameObject composerPrev, composerNext, composerChrome, composerClose;
        int composerLayout = -1, composerHintVersion = -1;
        string fitLine;
        int fitSlots = -1;
        bool fitPicking, fitTap, fitTyped;
        float fitRoom;
        TextMeshProUGUI composerHint;
        string composerText = "", composerError;
        bool composerOpen;
        float caretAt, shakeAt = -10f, eraseNext;
        int openedFrame = -1;
        bool composerReleaseWait;
        Keyboard typingKeyboard;
        readonly System.Collections.Generic.List<(RectTransform card, float width)> cardWidths = new();
        GridLayoutGroup helpKeys;
        readonly System.Collections.Generic.List<(RectTransform cap, TextMeshProUGUI key, TextMeshProUGUI does)> helpRows = new();
        TextMeshProUGUI helpSpellStep, helpSwingStep;
        float fittedRoom = -1f;

        sealed class ComposerTile
        {
            public RectTransform root;
            public Image lip, face, edge, outline;
            public TextMeshProUGUI letter;
        }

        bool TypedMode => LocalPlayer && LocalPlayer.Binding is DesktopBinding && !TouchControlsShown;
        bool TapSpelling => TouchControlsShown || (LocalPlayer && LocalPlayer.Binding is TouchBinding);
        public bool ComposerOpen => craftRoot && craftRoot.activeSelf;
        public string ComposerText => composerText;
        public string ComposerStatus => craftStatus ? craftStatus.text : "";
        public bool ComposerLettersOnly => composerChrome && !composerChrome.activeSelf;
        public string ComposerHint => composerHint && composerHint.gameObject.activeInHierarchy ? composerHint.text : "";
        public string ToastText => toast && toast.gameObject.activeSelf ? toastLabel.text : "";
        public bool CountdownShown => chip && chip.gameObject.activeSelf;
        public string CountdownText => CountdownShown ? chipDigit.text : "";
        public bool ResultShown => resultRoot && resultRoot.activeSelf;
        public bool ResultPending => resultSoon != null;
        public bool HelpShown => (helpCard && helpCard.activeSelf) || GuideShown;
        public bool GuideShown => guide && guide.Showing;
        public bool BannerShown => banner && banner.gameObject.activeSelf;
        public string BannerText => BannerShown ? bannerTitle.text + " · " + bannerLine.text : "";
        public bool LeaveAsked => leaveRoot && leaveRoot.activeSelf;

        TMP_FontAsset GameFont => GameAssets.I ? GameAssets.I.font : TMP_Settings.defaultFontAsset;
        TMP_FontAsset DisplayFont => LobbyFonts.Display ? LobbyFonts.Display : GameFont;

        Image Paint(RectTransform rt, Sprite sprite, Color colour, float multiplier = 1f)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = multiplier;
            image.color = colour; image.raycastTarget = false;
            return image;
        }

        static RectTransform Cover(string label, Transform parent, float grow = 0f, float dy = 0f)
        {
            var rt = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-grow, -grow + dy); rt.offsetMax = new Vector2(grow, grow + dy);
            rt.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return rt;
        }

        static RectTransform Put(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot; rt.anchoredPosition = position; rt.sizeDelta = size;
            return rt;
        }

        Image SoftShadow(Transform parent, Color colour, float blur, float dy) =>
            Paint(Cover("Shadow", parent, blur * .6f, dy), softSprite, colour, 28f / blur);

        void Raise(RectTransform rt, int radius, Color face, Color lip, float lipDepth, Color drop, float blur, float dropY, Color? edge = null)
        {
            SoftShadow(rt, drop, blur, dropY);
            if (lipDepth > 0f) Paint(Cover("Lip", rt, 0f, -lipDepth), LobbyIcons.RoundedSprite(radius), lip);
            Paint(Cover("Face", rt), LobbyIcons.RoundedSprite(radius), face);
            if (edge.HasValue) Paint(Cover("Edge", rt), LobbyIcons.FrameSprite(radius, 1), edge.Value);
        }

        TextMeshProUGUI Copy(Transform parent, string label, string text, TMP_FontAsset font, float size, Color colour, float spacing = 0f)
        {
            var rt = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            var copy = rt.gameObject.AddComponent<TextMeshProUGUI>();
            copy.font = font ? font : GameFont; copy.fontSize = size; copy.fontStyle = FontStyles.Normal;
            copy.color = colour; copy.characterSpacing = spacing; copy.alignment = TextAlignmentOptions.Center;
            copy.textWrappingMode = TextWrappingModes.Normal; copy.raycastTarget = false; copy.text = text;
            return copy;
        }

        static GameObject Gap(Transform parent, float height)
        {
            var rt = new GameObject("Gap", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            var layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = layout.minHeight = height;
            return rt.gameObject;
        }

        Image Picture(Transform parent, string label, Sprite sprite, Color colour, float size)
        {
            var rt = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false); rt.sizeDelta = Vector2.one * size;
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.color = colour; image.preserveAspect = true; image.raycastTarget = false;
            var layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = layout.minHeight = size; layout.preferredWidth = size;
            return image;
        }

        RectTransform Card(string label, Transform parent, float width, float padding)
        {
            var rt = Rect(label, parent, new Vector2(.5f, .5f), Vector2.zero, new Vector2(width, 200f));
            Raise(rt, 31, CardFace, CardLip, 15f, CardDrop, 100f, -25f);
            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            int pad = Mathf.RoundToInt(padding);
            layout.padding = new RectOffset(pad, pad, pad, pad);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            rt.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            cardWidths.Add((rt, width));
            return rt;
        }

        Button CardButton(Transform parent, string label, string caption, Action action, bool primary, float height)
        {
            var rt = Rect(label, parent, new Vector2(.5f, .5f), Vector2.zero, new Vector2(300f, height));
            var hit = rt.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None; button.targetGraphic = hit;
            button.onClick.AddListener(() => action?.Invoke());
            var layout = rt.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight = layout.minHeight = height;
            if (primary) Paint(Cover("Lip", rt, 0f, -7.5f), LobbyIcons.RoundedSprite(15), PrimaryLip);
            var body = Cover("Body", rt);
            var face = Paint(Cover("Face", body), LobbyIcons.RoundedSprite(15), primary ? Primary : Quiet);
            if (!primary) Paint(Cover("Edge", body), LobbyIcons.FrameSprite(15, 1), Hair);
            var text = Copy(body, "Label", caption, primary ? LobbyFonts.Black : LobbyFonts.Body, 22f, primary ? Cream : Ink, primary ? 1f : 0f);
            Stretch(text.rectTransform); text.rectTransform.offsetMin = new Vector2(12f, 0f); text.rectTransform.offsetMax = new Vector2(-12f, 0f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            var ring = Paint(Cover("Ring", rt, 4f), LobbyIcons.FrameSprite(19, 3), FocusRing); ring.enabled = false;
            var press = rt.gameObject.AddComponent<LobbyPress>();
            press.Body = body; press.Lift = 2.5f; press.Sink = 1.25f; press.Ring = ring;
            Color rest = primary ? Primary : Quiet, hot = primary ? PrimaryHover : QuietHover;
            press.Hot = on => face.color = on ? hot : rest;
            return button;
        }

        static TextMeshProUGUI LabelOf(Button button) => button.transform.Find("Body/Label").GetComponent<TextMeshProUGUI>();

        Button RoundButton(Transform parent, string label, string icon, Action action, float size)
        {
            var rt = Rect(label, parent, Vector2.one, Vector2.zero, Vector2.one * size);
            var face = Paint(rt, LobbyIcons.RoundedSprite(Mathf.RoundToInt(size * .5f)), Quiet); face.raycastTarget = true;
            Paint(Cover("Edge", rt), LobbyIcons.FrameSprite(Mathf.RoundToInt(size * .5f), 1), Hair);
            var glyph = Cover("Icon", rt, -size * .26f).gameObject.AddComponent<Image>();
            glyph.sprite = LobbyIcons.Get(icon); glyph.color = Ink; glyph.preserveAspect = true; glyph.raycastTarget = false;
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None; button.targetGraphic = face;
            button.onClick.AddListener(() => action?.Invoke());
            var press = rt.gameObject.AddComponent<LobbyPress>();
            press.Body = (RectTransform)glyph.transform; press.Lift = 0f; press.Sink = 1f;
            press.Hot = on => face.color = on ? QuietHover : Quiet;
            rt.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return button;
        }

        static void FocusOn(Button button)
        {
            var events = EventSystem.current;
            if (events && button && button.isActiveAndEnabled) events.SetSelectedGameObject(button.gameObject);
        }

        static void ClearFocus()
        {
            var events = EventSystem.current;
            if (events) events.SetSelectedGameObject(null);
        }

        void BuildCards()
        {
            BuildChip();
            BuildBanner();
            BuildModeActions();
            BuildResult();
            BuildToast();
            BuildLeave();
            typingKeyboard = Keyboard.current;
            if (typingKeyboard != null) typingKeyboard.onTextInput += OnTextTyped;
        }

        void BuildToast()
        {
            toast = Put(Rect("Toast", safe, Vector2.zero, Vector2.zero, Vector2.zero), new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, ToastY - topInset), new Vector2(300f, 50f));
            toastFade = toast.gameObject.AddComponent<CanvasGroup>();
            toastFade.blocksRaycasts = toastFade.interactable = false;
            SoftShadow(toast, ToastDrop, 37.5f, -10f);
            Paint(Cover("Face", toast), LobbyIcons.RoundedSprite(15), ToastFill);
            Paint(Cover("Edge", toast), LobbyIcons.FrameSprite(15, 1), ToastEdge);
            toastLabel = Copy(toast, "Text", "", LobbyFonts.Body, ToastFont, ToastInk);
            Stretch(toastLabel.rectTransform);
            toastLabel.rectTransform.offsetMin = new Vector2(ToastPadX, ToastPadY); toastLabel.rectTransform.offsetMax = new Vector2(-ToastPadX, -ToastPadY);
            toastLabel.enableAutoSizing = true; toastLabel.fontSizeMin = ToastMinFont; toastLabel.fontSizeMax = ToastFont;
            toast.gameObject.SetActive(false);
        }

        void BuildChip()
        {
            chip = Put(Rect("Round chip", safe, Vector2.zero, Vector2.zero, Vector2.zero), new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, ChipY - topInset), new Vector2(300f, 56f));
            SoftShadow(chip, CardDrop, 30f, -8f);
            Paint(Cover("Lip", chip, 0f, -5f), LobbyIcons.RoundedSprite(28), PrimaryLip);
            Paint(Cover("Face", chip), LobbyIcons.RoundedSprite(28), Primary);
            var disc = Put(Rect("Count", chip, Vector2.zero, Vector2.zero, Vector2.zero), new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(6f, 0f), new Vector2(44f, 44f));
            chipDisc = Paint(disc, LobbyIcons.RoundedSprite(22), Cream);
            chipDigit = Copy(disc, "Digit", "", LobbyFonts.Display, 28f, Ink);
            Stretch(chipDigit.rectTransform);
            chipDigit.textWrappingMode = TextWrappingModes.NoWrap;
            chipDigit.enableAutoSizing = true; chipDigit.fontSizeMin = 12f; chipDigit.fontSizeMax = 28f;
            chipLine = Copy(chip, "Line", "", LobbyFonts.Black, 17f, Cream, 8f);
            Stretch(chipLine.rectTransform);
            chipLine.rectTransform.offsetMin = new Vector2(64f, 0f); chipLine.rectTransform.offsetMax = new Vector2(-24f, 0f);
            chipLine.alignment = TextAlignmentOptions.MidlineLeft; chipLine.textWrappingMode = TextWrappingModes.NoWrap;
            bagHides.Add(chip.gameObject.AddComponent<CanvasGroup>());
            chip.gameObject.SetActive(false);
        }

        void BuildBanner()
        {
            banner = Put(Rect("Round banner", safe, Vector2.zero, Vector2.zero, Vector2.zero), new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, BannerY - topInset), new Vector2(720f, 116f));
            bannerFade = banner.gameObject.AddComponent<CanvasGroup>();
            bannerFade.blocksRaycasts = bannerFade.interactable = false;
            SoftShadow(banner, ToastDrop, 37.5f, -10f);
            Paint(Cover("Face", banner), LobbyIcons.RoundedSprite(22), ToastFill);
            Paint(Cover("Edge", banner), LobbyIcons.FrameSprite(22, 1), ToastEdge);
            bannerTitle = Copy(banner, "Title", "", DisplayFont, 44f, ToastInk);
            Put(bannerTitle.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -12f), new Vector2(680f, 54f));
            bannerTitle.textWrappingMode = TextWrappingModes.NoWrap;
            bannerLine = Copy(banner, "Line", "", LobbyFonts.Body, 24f, ToastInk);
            Put(bannerLine.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 14f), new Vector2(680f, 34f));
            bannerLine.textWrappingMode = TextWrappingModes.NoWrap;
            banner.gameObject.SetActive(false);
        }

        public void ShowBanner(string title, string line)
        {
            if (!banner) return;
            bannerTitle.text = title ?? ""; bannerLine.text = line ?? "";
            float wide = Mathf.Max(bannerTitle.GetPreferredValues(bannerTitle.text).x, bannerLine.GetPreferredValues(bannerLine.text).x);
            float width = Mathf.Clamp(wide + 80f, 420f, Mathf.Max(420f, safe.rect.width * .9f));
            banner.sizeDelta = new Vector2(width, 116f);
            bannerTitle.rectTransform.sizeDelta = new Vector2(width - 40f, 54f);
            bannerLine.rectTransform.sizeDelta = new Vector2(width - 40f, 34f);
            bannerAt = Time.unscaledTime; bannerFade.alpha = 0f;
            banner.gameObject.SetActive(true);
            banner.SetAsLastSibling();
        }

        void HideBanner()
        {
            banner.gameObject.SetActive(false);
            string waiting = toastAfterBanner;
            toastAfterBanner = null;
            if (!string.IsNullOrEmpty(waiting) && !ResultShown) Toast(waiting);
        }

        public void ToastAfterBanner(string text)
        {
            if (BannerShown) toastAfterBanner = text;
            else Toast(text);
        }

        void BuildLeave()
        {
            var shade = Rect("Leave match", UiCanvas.transform, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(shade);
            var dim = shade.gameObject.AddComponent<Image>(); dim.color = ModalShade; dim.raycastTarget = true;
            leaveRoot = shade.gameObject;
            var card = Card("Leave card", shade, 520f, 44f);
            Copy(card, "Eyebrow", "LEAVE MATCH", LobbyFonts.Black, 18f, Eyebrow, 1f);
            Gap(card, 12.5f);
            Copy(card, "Heading", "Leave this match?", LobbyFonts.Black, 36f, Ink, -3f);
            Gap(card, 12.5f);
            Copy(card, "Blurb", "Everyone goes back to the lobby.", LobbyFonts.Body, 20f, BodyInk);
            Gap(card, 22.5f);
            leaveKeep = CardButton(card, "Keep playing", "KEEP PLAYING →", CloseLeave, true, 60f);
            Gap(card, 12.5f);
            CardButton(card, "Leave", "Leave match", ConfirmLeave, false, 50f);
            leaveRoot.SetActive(false);
        }

        void AskLeave(Action leave, Button back)
        {
            if (!leaveRoot) { leave?.Invoke(); return; }
            leaveAction = leave; leaveReturnFocus = back;
            leaveRoot.SetActive(true);
            leaveRoot.transform.SetAsLastSibling();
            FocusOn(leaveKeep);
        }

        void CloseLeave()
        {
            if (!LeaveAsked) return;
            leaveRoot.SetActive(false);
            leaveAction = null;
            FocusOn(leaveReturnFocus);
        }

        void ConfirmLeave()
        {
            var leave = leaveAction;
            leaveAction = null;
            if (leaveRoot) leaveRoot.SetActive(false);
            leave?.Invoke();
        }

        void LeaveFromResult()
        {
            var home = resultHomeAction;
            HideResult();
            if (home != null) home(); else GoHome();
        }

        void BuildModeActions()
        {
            var row = Put(Rect("Mode actions", safe, Vector2.zero, Vector2.zero, Vector2.zero), new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, ModeActionsY - topInset), new Vector2(476f, 68f));
            modeActions = row.gameObject;
            var play = CardButton(row, "Play", "START WITH AI →", () => { if (AcceptsGameplayInput) modeAction?.Invoke(); }, true, 60f);
            Put((RectTransform)play.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(260f, 60f));
            modePlayLabel = LabelOf(play);
            var home = CardButton(row, "Home", "Leave match", GoHome, false, 60f);
            Put((RectTransform)home.transform, Vector2.one, Vector2.one, Vector2.zero, new Vector2(200f, 60f));
            modeActionsGroup = row.gameObject.AddComponent<CanvasGroup>();
            RefreshModeActionVisibility();
        }

        public void Toast(string text)
        {
            if (!toast || string.IsNullOrEmpty(text)) return;
            if (BannerShown) return;
            toastLabel.text = text;
            float max = Mathf.Min(ToastMaxWidth, safe.rect.width * .8f);
            var size = toastLabel.GetPreferredValues(text, max - 2f * ToastPadX, 0f);
            toast.sizeDelta = new Vector2(Mathf.Min(max, size.x + 2f * ToastPadX), size.y + 2f * ToastPadY);
            toastAt = Time.unscaledTime;
            toast.gameObject.SetActive(true);
            toast.SetAsLastSibling();
        }

        public void ShowCountdown(int left, string line)
        {
            if (!chip) return;
            goUntil = 0f;
            if (!chip.gameObject.activeSelf) chip.gameObject.SetActive(true);
            if (left != chipLeft)
            {
                chipLeft = left; chipPulseAt = Time.unscaledTime;
                chipDigit.text = left.ToString(); chipDisc.color = Cream; chipDigit.color = Ink;
            }
            SetChipLine(line);
        }

        public void ShowGo(string line = null)
        {
            if (!chip) return;
            chip.gameObject.SetActive(true);
            chipLeft = -1; chipPulseAt = Time.unscaledTime; goUntil = Time.unscaledTime + .8f;
            chipDigit.text = "GO"; chipDisc.color = Go; chipDigit.color = Cream;
            if (line != null) SetChipLine(line);
        }

        public void HideCountdown()
        {
            chipLeft = -1; goUntil = 0f;
            if (chip && chip.gameObject.activeSelf) chip.gameObject.SetActive(false);
        }

        void SetChipLine(string line)
        {
            line ??= "";
            if (chipLine.text == line) return;
            chipLine.text = line;
            float width = line.Length > 0 ? chipLine.GetPreferredValues(line).x : 0f;
            chip.sizeDelta = new Vector2(Mathf.Clamp(width + 90f, 160f, 900f), 56f);
        }

        public void ShowModeActions(bool on, string caption = null, Action play = null)
        {
            if (!modeActions) return;
            if (caption != null && modePlayLabel.text != caption) modePlayLabel.text = caption;
            if (play != null) modeAction = play;
            modeWanted = on;
            RefreshModeActionVisibility();
        }

        void RefreshModeActionVisibility()
        {
            if (!modeActionsGroup) return;
            bool shown = modeWanted && !BagOpen && !ResultShown && StateController.CurrentState == UIState.GameplayHUD;
            modeActionsGroup.alpha = shown ? 1f : 0f;
            modeActionsGroup.interactable = modeActionsGroup.blocksRaycasts = shown && AcceptsGameplayInput;
        }

        void BuildPause()
        {
            var shade = Rect("Pause", UiCanvas.transform, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(shade);
            var dim = shade.gameObject.AddComponent<Image>(); dim.color = ModalShade; dim.raycastTarget = true;
            pauseRoot = shade.gameObject; pauseShade = shade;

            var card = Card("Pause card", shade, 575f, 44f);
            pauseCard = card.gameObject;
            Copy(card, "Eyebrow", "TAKE A BREATHER", LobbyFonts.Black, 18f, Eyebrow, 1f);
            Gap(card, 12.5f);
            Copy(card, "Heading", "The mess can wait.", LobbyFonts.Black, 40f, Ink, -3f);
            Gap(card, 12.5f);
            pauseBlurb = Copy(card, "Blurb", "", LobbyFonts.Body, 20f, BodyInk);
            Gap(card, 22.5f);
            resumeButton = CardButton(card, "Resume", "KEEP PLAYING →", TogglePause, true, 60f);
            Gap(card, 12.5f);
            CardButton(card, "Pause controls", "Controls", () => ShowPauseSettings("controls"), false, 50f);
            Gap(card, 10f);
            CardButton(card, "Pause settings", "Settings", () => ShowPauseSettings("audio"), false, 50f);
            Gap(card, 10f);
            howToPlay = CardButton(card, "Pause how to play", "How to play", () => ShowHelp(true), false, 50f);
            Gap(card, 12.5f);
            pauseHome = CardButton(card, "Pause home", "Leave match", () => AskLeave(GoHome, pauseHome), false, 46f);
            bool touchable = Touchscreen.current != null || Application.isMobilePlatform;
            var gap = Gap(card, 12.5f); gap.SetActive(touchable);
            var touch = CardButton(card, "Touch switch", "Touch controls: off", () => { touchChosen = true; ShowTouchControls(!TouchControlsShown); }, false, 46f);
            touchToggle = LabelOf(touch);
            touch.gameObject.SetActive(touchable);
            Gap(card, 10f).SetActive(touchable);
            CardButton(card, "Pause touch layout", "Touch layout", TouchLayoutEditor.Open, false, 46f).gameObject.SetActive(touchable);

            BuildHelp(shade);
            StateController.Register(UIState.PauseMenu, pauseRoot.AddComponent<CanvasGroup>(), resumeButton.gameObject);
        }

        void BuildHelp(RectTransform shade)
        {
            var card = Card("Pause help", shade, 812f, 40f);
            helpCard = card.gameObject;
            var close = RoundButton(card, "Close help", LobbyIcons.Close, () => ShowHelp(false), 40f);
            Put((RectTransform)close.transform, Vector2.one, Vector2.one, new Vector2(-22f, -22f), Vector2.one * 40f);
            Copy(card, "Eyebrow", "A HOUSE FULL OF POSSIBILITIES", LobbyFonts.Black, 18f, Eyebrow, 1f);
            Gap(card, 10f);
            Copy(card, "Heading", "Everything starts with a word.", LobbyFonts.Black, 36f, Ink, -3f);
            Gap(card, 22f);

            var rules = GameConfig.Current.Rules.Defaults;
            bool phone = Application.isMobilePlatform;
            var keys = DesktopBinding.Shared;
            var steps = Rect("Steps", card, Vector2.zero, Vector2.zero, Vector2.zero);
            var row = steps.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 25f; row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = true; row.childForceExpandHeight = false;
            TextMeshProUGUI Step(string number, string title, string text)
            {
                var column = Rect("Step " + number, steps, Vector2.zero, Vector2.zero, Vector2.zero);
                var stack = column.gameObject.AddComponent<VerticalLayoutGroup>();
                stack.spacing = 4f; stack.childControlWidth = stack.childControlHeight = true;
                stack.childForceExpandWidth = true; stack.childForceExpandHeight = false;
                var size = column.gameObject.AddComponent<LayoutElement>(); size.preferredWidth = 1f; size.flexibleWidth = 1f;
                Copy(column, "Number", number, DisplayFont, 35f, StepNumber).alignment = TextAlignmentOptions.Left;
                Copy(column, "Title", title, LobbyFonts.Black, 20f, Ink).alignment = TextAlignmentOptions.Left;
                var body = Copy(column, "Text", text, LobbyFonts.Body, 18f, StepInk);
                body.alignment = TextAlignmentOptions.TopLeft;
                return body;
            }
            Step("01", "Break it.", "Smash the house's own furniture. A TABLE breaks into T, A, B, L and E.");
            helpSpellStep = Step("02", "Spell it.", SpellStepText(phone ? (InputBinding)TouchBinding.Shared : keys));
            helpSwingStep = Step("03", "Bring it.", SwingStepText(phone ? "SMASH" : SwingKey(keys)));

            if (!phone)
            {
                Gap(card, 22f);
                var table = Rect("Keys", card, Vector2.zero, Vector2.zero, Vector2.zero);
                helpKeys = table.gameObject.AddComponent<GridLayoutGroup>();
                helpKeys.cellSize = new Vector2(356f, 36f); helpKeys.spacing = new Vector2(20f, 8f);
                helpKeys.constraint = GridLayoutGroup.Constraint.FixedColumnCount; helpKeys.constraintCount = 2;
                helpRows.Clear();
                foreach (var (key, what) in HelpEntries(false))
                {
                    var cell = Rect(what, table, Vector2.zero, Vector2.zero, Vector2.zero);
                    var cap = Put(Rect("Key", cell, Vector2.zero, Vector2.zero, Vector2.zero), new Vector2(0f, .5f), new Vector2(0f, .5f), Vector2.zero, new Vector2(40f, 30f));
                    Paint(Cover("Lip", cap, 0f, -2.5f), LobbyIcons.RoundedSprite(8), Hex(0xdcc9a6));
                    Paint(Cover("Face", cap), LobbyIcons.RoundedSprite(8), Hex(0xfffaf0));
                    Paint(Cover("Edge", cap), LobbyIcons.FrameSprite(8, 1), Hair);
                    var name = Copy(cap, "Name", key, LobbyFonts.Black, 18f, Ink);
                    Stretch(name.rectTransform); name.textWrappingMode = TextWrappingModes.NoWrap;
                    var does = Copy(cell, "Does", what, LobbyFonts.Body, 18f, StepInk);
                    Stretch(does.rectTransform);
                    does.alignment = TextAlignmentOptions.MidlineLeft; does.textWrappingMode = TextWrappingModes.NoWrap;
                    helpRows.Add((cap, name, does));
                    ShowHelpRow(helpRows[^1], key, what);
                }
            }
            Gap(card, 18f);
            Copy(card, "Fine print", $"Health is {Mathf.RoundToInt(rules.MaxHealth)} HP. Your looks never change your stats.", LobbyFonts.Body, 18f, FinePrint);
            Gap(card, 22f);
            helpDone = CardButton(card, "Help done", "GOT IT. LET’S PLAY. →", () => SetPaused(false), true, 60f);
            helpCard.SetActive(false);
        }

        public static string SpellStepText(InputBinding binding)
        {
            string bag = $"Walk over letters (your bag holds {GameConfig.Current.Rules.Defaults.MaxLetters}). ";
            var keys = DesktopBinding.Shared;
            return binding switch
            {
                TouchBinding => bag + "Tap SPELL, pick a word, tap SPELL.",
                GamepadBinding => bag + $"Hold {ControlHints.PadSpell}, pick a word with the stick, let go.",
                KeyboardBinding half => bag + $"Hold {SpellKeyLabel(half)}, pick a word with {(half.Id.EndsWith("Left", StringComparison.Ordinal) ? "W and S" : "the arrow keys")}, let go.",
                _ => bag + $"Press {ControlHints.KeyOf(keys.SpellBox)}, type a word, press {ControlHints.KeyOf(keys.SpellBox)} again. Or hold {ControlHints.KeyOf(keys.Bag)} and click a lit card.",
            };
        }

        public static string SwingStepText(string key) => $"Press {key} to swing, throw or set down an item. Harder words carry effects.";

        static string SwingKey(InputBinding binding) => binding switch
        {
            GamepadBinding => "X",
            TouchBinding => "SMASH",
            KeyboardBinding half => half.Id.EndsWith("Left", StringComparison.Ordinal) ? "J" : "/",
            _ => ControlHints.KeyOf(DesktopBinding.Shared.Attack),
        };

        public static (string key, string what)[] HelpEntries(bool gamepad)
        {
            if (gamepad)
                return new[]
                {
                    ("L-STICK", "move"), ("R-STICK", "aim"),
                    ("X", "smash, throw, place, block"), ("LT", "hold to block"),
                    ("B", "dodge"), ("A", "jump"),
                    (ControlHints.PadSpell, "hold to spell"), (ControlHints.PadHands, "switch hand"),
                    (ControlHints.PadBag, "hold for bag and map"), ("RT", "pick up, hold to revive"),
                    ("LB", "hold to drop an item"), ("START", "pause"),
                };
            var keys = DesktopBinding.Shared;
            return new[]
            {
                ("WASD", "move"), ("MOUSE", "aim"),
                (ControlHints.KeyOf(keys.Attack), "smash, throw, place, block"), (ControlHints.KeyOf(keys.Attack), "hold to aim, let go to throw"),
                (ControlHints.KeyOf(keys.Dodge), "dodge"), (ControlHints.KeyOf(keys.Jump), "jump"),
                (ControlHints.KeyOf(keys.SpellBox), "spell, again to finish"), (ControlHints.KeyOf(keys.Hand1) + " " + ControlHints.KeyOf(keys.Hand2), "switch hand"),
                (ControlHints.KeyOf(keys.Bag), "hold for bag and map"), (ControlHints.KeyOf(keys.Interact), "pick up, hold to revive"),
                (ControlHints.KeyOf(keys.Drop), "hold to drop an item"), (ControlHints.KeyOf(keys.Pause), "pause"),
            };
        }

        static void ShowHelpRow((RectTransform cap, TextMeshProUGUI key, TextMeshProUGUI does) row, string key, string what)
        {
            row.key.text = key; row.does.text = what;
            row.cap.sizeDelta = new Vector2(Mathf.Max(40f, row.key.GetPreferredValues(key).x + 20f), 30f);
            row.does.rectTransform.offsetMin = new Vector2(row.cap.sizeDelta.x + 12f, 0f);
        }

        void FillHelp()
        {
            if (Application.isMobilePlatform) return;
            bool gamepad = LocalPlayer && LocalPlayer.Binding is GamepadBinding;
            var entries = HelpEntries(gamepad);
            for (int i = 0; i < helpRows.Count && i < entries.Length; i++) ShowHelpRow(helpRows[i], entries[i].key, entries[i].what);
            var binding = TouchControlsShown ? TouchBinding.Shared
                : LocalPlayer && LocalPlayer.Binding is GamepadBinding or KeyboardBinding ? LocalPlayer.Binding : DesktopBinding.Shared;
            if (helpSpellStep) helpSpellStep.text = SpellStepText(binding);
            if (helpSwingStep) helpSwingStep.text = SwingStepText(SwingKey(binding));
        }

        void ShowHelp(bool on)
        {
            if (!helpCard) return;
            if (on && OpenGuide()) return;
            bool fromGuide = !on && CloseGuide();
            if (on) FillHelp();
            helpCard.SetActive(on);
            pauseCard.SetActive(!on);
            FocusOn(on ? helpDone : fromGuide ? howToPlay : resumeButton);
        }

        string GuideMode => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == Session.TutorialScene ? HouseModes.Tutorial : Match.Mode;

        InputBinding GuideViewer => TouchControlsShown ? TouchBinding.Shared : LocalPlayer ? LocalPlayer.Binding : null;

        bool OpenGuide()
        {
            if (!ModeGuides.Has(GuideMode)) return false;
            helpCard.SetActive(false);
            pauseCard.SetActive(false);
            guide = ModeGuides.Show(GuideMode, GuideViewer, GuideClosed);
            if (!guide) { pauseCard.SetActive(true); return false; }
            guide.KeepWhile = () => this && Paused;
            return true;
        }

        bool CloseGuide()
        {
            if (!guide) return false;
            var open = guide;
            guide = null;
            open.Close();
            return true;
        }

        void GuideClosed()
        {
            guide = null;
            if (!this || !Paused || PauseSettingsShown || LeaveAsked || (helpCard && helpCard.activeSelf)) return;
            pauseCard.SetActive(true);
            FocusOn(howToPlay);
        }

        string ModeBlurb()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == Session.TutorialScene)
                return "A friendly room to try smashing, spelling and every skill.";
            return HouseGoals.Blurb(Match.Mode, Match.Rules);
        }

        void OnPauseShown()
        {
            ClosePauseSettings();
            if (pauseBlurb) pauseBlurb.text = ModeBlurb();
            ShowHelp(false);
        }

        void BuildResult()
        {
            resultShade = Rect("Result", safe, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(resultShade);
            var dim = resultShade.gameObject.AddComponent<Image>(); dim.color = ModalShade; dim.raycastTarget = true;
            resultRoot = resultShade.gameObject;
            var card = resultCard = Card("Result card", resultShade, ResultCardWidth, ResultCardPad);
            resultSpark = Picture(card, "Spark", SparkSprite(), Spark, 62.5f);
            resultSparkGap = Gap(card, 10f);
            resultEyebrow = Copy(card, "Eyebrow", "", LobbyFonts.Black, 12.5f, Eyebrow, 21f);
            resultEyebrowGap = Gap(card, 10f);
            resultHeading = Copy(card, "Heading", "", LobbyFonts.Black, ResultHeadingFont, Ink, -3f);
            resultHeadingGap = Gap(card, 8f);
            resultFlavour = Copy(card, "Flavour", "", LobbyFonts.Body, 15f, BodyInk);
            resultDetail = Copy(card, "Detail", "", LobbyFonts.Black, 15f, Ink);
            resultPlayersGap = Gap(card, 14f);
            resultPlayers = Copy(card, "Players", "", LobbyFonts.Body, 18f, Ink);
            resultPlayers.lineSpacing = 6f;
            resultStatsGap = Gap(card, 22.5f);

            var stats = Rect("Stats", card, Vector2.zero, Vector2.zero, Vector2.zero);
            resultStatsRow = stats.gameObject;
            var statsSize = stats.gameObject.AddComponent<LayoutElement>(); statsSize.preferredHeight = statsSize.minHeight = 86f;
            Image Line(string label, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
            {
                var line = CreateImage(label, stats, Vector2.zero, Vector2.zero, Vector2.zero, Hair);
                line.rectTransform.anchorMin = min; line.rectTransform.anchorMax = max;
                line.rectTransform.offsetMin = offsetMin; line.rectTransform.offsetMax = offsetMax;
                return line;
            }
            Line("Top line", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -1f), Vector2.zero);
            Line("Bottom line", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 1f));
            Line("Divider 1", new Vector2(1f / 3f, 0f), new Vector2(1f / 3f, 1f), new Vector2(0f, 16f), new Vector2(1f, -16f));
            Line("Divider 2", new Vector2(2f / 3f, 0f), new Vector2(2f / 3f, 1f), new Vector2(0f, 16f), new Vector2(1f, -16f));
            string[] names = { "OBJECTS WRECKED", "WORDS SPELLED", "DAMAGE DEALT" };
            for (int i = 0; i < 3; i++)
            {
                var cell = Rect(names[i], stats, Vector2.zero, Vector2.zero, Vector2.zero);
                cell.anchorMin = new Vector2(i / 3f, 0f); cell.anchorMax = new Vector2((i + 1) / 3f, 1f); cell.offsetMin = cell.offsetMax = Vector2.zero;
                var number = resultStats[i] = Copy(cell, "Number", "0", DisplayFont, 31f, Ink);
                number.rectTransform.anchorMin = new Vector2(0f, .42f); number.rectTransform.anchorMax = Vector2.one;
                number.rectTransform.offsetMin = Vector2.zero; number.rectTransform.offsetMax = new Vector2(0f, -8f);
                number.textWrappingMode = TextWrappingModes.NoWrap;
                var label = Copy(cell, "Label", names[i], LobbyFonts.Black, StatLabelFont, BodyInk, StatLabelSpacing);
                label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = new Vector2(1f, .42f);
                label.rectTransform.offsetMin = new Vector2(4f, 8f); label.rectTransform.offsetMax = new Vector2(-4f, 0f);
                label.alignment = TextAlignmentOptions.Top; label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
                label.enableAutoSizing = true; label.fontSizeMin = StatLabelFont; label.fontSizeMax = StatLabelFont;
            }

            resultRewardGap = Gap(card, 16f);
            var reward = Rect("Reward", card, Vector2.zero, Vector2.zero, Vector2.zero);
            resultReward = reward.gameObject;
            rewardChip = Put(Rect("Chip", reward, Vector2.zero, Vector2.zero, Vector2.zero), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(ResultCardWidth * .5f, RewardChipHeight));
            rewardChip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            SoftShadow(rewardChip, CardDrop, 30f, -8f);
            Paint(Cover("Lip", rewardChip, 0f, -RewardLip), LobbyIcons.RoundedSprite(RewardRadius), PrimaryLip);
            Paint(Cover("Face", rewardChip), LobbyIcons.RoundedSprite(RewardRadius), Primary);
            var rewardRow = reward.gameObject.AddComponent<HorizontalLayoutGroup>();
            rewardRow.childAlignment = TextAnchor.MiddleCenter; rewardRow.spacing = RewardSpacing;
            rewardRow.childControlWidth = rewardRow.childControlHeight = true;
            rewardRow.childForceExpandWidth = rewardRow.childForceExpandHeight = false;
            var rewardSize = reward.gameObject.AddComponent<LayoutElement>(); rewardSize.preferredHeight = rewardSize.minHeight = RewardChipHeight;
            var coin = Picture(reward, "Coin", LobbyIcons.Get(LobbyIcons.Coin), CoinGold, RewardCoin);
            coin.GetComponent<LayoutElement>().preferredWidth = RewardCoin;
            resultCoins = Copy(reward, "Coins", "", DisplayFont, RewardCoinsFont, Cream);
            resultScore = Copy(reward, "Score", "", LobbyFonts.Body, RewardLineFont, ToastInk);
            resultBestText = Copy(reward, "Best", "New best!", LobbyFonts.Black, RewardLineFont, Gold);
            resultBest = resultBestText.gameObject;
            foreach (var text in reward.GetComponentsInChildren<TextMeshProUGUI>(true)) text.textWrappingMode = TextWrappingModes.NoWrap;

            Gap(card, 22.5f);
            resultNext = CardButton(card, "Next", "NEXT ROUND →", ConfirmResult, true, 60f);
            resultNextLabel = LabelOf(resultNext);
            Gap(card, 12.5f);
            resultHome = CardButton(card, "Result home", "Leave match", () =>
            {
                if (resultFinal) LeaveFromResult();
                else AskLeave(LeaveFromResult, resultHome);
            }, false, 46f);
            resultHomeLabel = LabelOf(resultHome);
            resultRoot.SetActive(false);
        }

        public void ShowResult(HudResult result, Action next, Action home = null)
        {
            if (!resultRoot || result == null) return;
            if (resultSoon != null) { StopCoroutine(resultSoon); resultSoon = null; }
            if (Paused) SetPaused(false);
            CloseComposer();
            if (bagPinned) SetBagPinned(false);
            resultNextAction = next; resultHomeAction = home;
            resultSpark.sprite = result.Won ? SparkSprite() : LobbyIcons.Get(LobbyIcons.Arrow);
            resultFinal = result.Final;
            resultEyebrow.text = Or(result.Eyebrow, result.Final ? "HOUSE PARTY COMPLETE" : $"ROUND {result.Round} COMPLETE");
            resultHeading.text = result.Heading ?? "";
            resultFlavour.text = Or(result.Flavour, result.Won ? "A little imagination goes a long way." : "Every good mess teaches you a new trick.");
            resultDetail.text = result.Detail ?? "";
            resultDetail.gameObject.SetActive(!string.IsNullOrEmpty(result.Detail));
            bool lines = result.Lines != null && result.Lines.Length > 0;
            resultPlayers.text = lines ? string.Join('\n', result.Lines) : "";
            resultPlayers.gameObject.SetActive(lines); resultPlayersGap.SetActive(lines);
            resultStatsRow.SetActive(result.Stats); resultStatsGap.SetActive(result.Stats);
            resultStats[0].text = result.Broken.ToString();
            resultStats[1].text = result.Crafted.ToString();
            resultStats[2].text = result.Damage.ToString();
            bool paid = result.Reward != null;
            resultReward.SetActive(paid); resultRewardGap.SetActive(paid);
            if (paid)
            {
                resultCoins.text = "+" + result.Reward.Coins;
                resultScore.text = "Score " + result.Reward.Score;
                resultBest.SetActive(result.Best);
                FitRewardChip();
            }
            resultNextLabel.text = Or(result.NextLabel, result.Final ? "PLAY AGAIN →" : "NEXT ROUND →");
            resultHomeLabel.text = Or(result.HomeLabel, "Leave match");
            resultRoot.SetActive(true);
            resultRoot.transform.SetAsLastSibling();
            RefreshBagPanel();
            FocusOn(resultNext);
            ShowMatchEnd(result.Final ? result.Board : null);
            ShowRoundRow(result.Final ? null : result.Standings);
        }

        void FitRewardChip()
        {
            float width = 2f * RewardChipPad + RewardCoin + RewardSpacing + resultCoins.GetPreferredValues(resultCoins.text).x;
            width += RewardSpacing + resultScore.GetPreferredValues(resultScore.text).x;
            if (resultBest.activeSelf) width += RewardSpacing + resultBestText.GetPreferredValues(resultBestText.text).x;
            rewardChip.sizeDelta = new Vector2(width, RewardChipHeight);
        }

        static string Or(string chosen, string fallback) => string.IsNullOrEmpty(chosen) ? fallback : chosen;

        public void ShowResultSoon(HudResult result, Action next, float delay = .9f)
        {
            if (resultSoon != null) StopCoroutine(resultSoon);
            resultSoon = StartCoroutine(ResultAfter(result, next, delay));
        }

        IEnumerator ResultAfter(HudResult result, Action next, float delay)
        {
            float elapsed = 0f;
            while (elapsed < delay || Paused)
            {
                if (Time.timeScale > 0f) elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            resultSoon = null;
            ShowResult(result, next);
        }

        public void HideResult()
        {
            if (resultSoon != null) { StopCoroutine(resultSoon); resultSoon = null; }
            resultNextAction = resultHomeAction = null;
            if (LeaveAsked && leaveReturnFocus == resultHome) { leaveRoot.SetActive(false); leaveAction = null; }
            if (resultRoot && resultRoot.activeSelf) { resultRoot.SetActive(false); ClearFocus(); }
        }

        public void ConfirmResult()
        {
            if (!ResultShown || LeaveAsked) return;
            var next = resultNextAction;
            HideResult();
            next?.Invoke();
        }

        Sprite SparkSprite()
        {
            if (sparkSprite) return sparkSprite;
            const int size = 128, samples = 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "HUD spark", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < samples; sy++)
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float px = Mathf.Abs((x + (sx + .5f) / samples) / size * 2f - 1f) / .96f;
                            float py = Mathf.Abs((y + (sy + .5f) / samples) / size * 2f - 1f) / .96f;
                            if (Mathf.Pow(px, .72f) + Mathf.Pow(py, .72f) <= 1f) inside++;
                        }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(inside * 255 / (samples * samples)));
                }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            sparkSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
            ownedSprites.Add(sparkSprite); ownedTextures.Add(texture);
            return sparkSprite;
        }

        void BuildComposer()
        {
            composer = Put(Rect("Craft drawer", safe, Vector2.zero, Vector2.zero, Vector2.zero), new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, ComposerSpawnY), new Vector2(ComposerLineMax + ComposerPad * 2f, ComposerPad * 2f + ComposerWellHeight + ComposerRowGap + CraftStatusHeight));
            craftRoot = composer.gameObject;
            var hit = composer.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;

            composerChrome = Rect("Chrome", composer, Vector2.zero, Vector2.zero, Vector2.zero).gameObject;
            Stretch((RectTransform)composerChrome.transform);
            var chrome = (RectTransform)composerChrome.transform;

            var well = composerWell = Put(Rect("Field", composer, Vector2.zero, Vector2.zero, Vector2.zero), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ComposerPad, -ComposerPad), new Vector2(TileRun(1), ComposerWellHeight));
            composerTiles = Rect("Tiles", well, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(composerTiles);
            for (int i = 0; i < tiles.Length; i++)
            {
                var root = Put(Rect($"Tile {i + 1}", composerTiles, Vector2.zero, Vector2.zero, Vector2.zero), new Vector2(0f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(ComposerTileWidth, ComposerTileHeight));
                var tile = tiles[i] = new ComposerTile { root = root };
                tile.lip = Paint(Cover("Lip", root, 0f, -3.75f), LobbyIcons.RoundedSprite(ComposerTileRadius), TileEdge);
                tile.face = Paint(Cover("Face", root), LobbyIcons.RoundedSprite(ComposerTileRadius), Tile);
                tile.edge = Paint(Cover("Edge", root), LobbyIcons.FrameSprite(ComposerTileRadius, 1), TileLine);
                tile.outline = Paint(Cover("Outline", root), LobbyIcons.FrameSprite(ComposerTileRadius, ComposerSlotStroke), OnGame);
                AddHudShadow(tile.outline);
                tile.outline.enabled = false;
                tile.letter = Copy(root, "Letter", "", DisplayFont, ComposerLetterFont, Ink);
                Stretch(tile.letter.rectTransform); tile.letter.textWrappingMode = TextWrappingModes.NoWrap;
                root.gameObject.SetActive(false);
            }
            var caretMark = CreateImage("Caret", composerTiles, Vector2.zero, Vector2.zero, Vector2.zero, OnGame);
            AddHudShadow(caretMark);
            caret = Put(caretMark.rectTransform, new Vector2(0f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(ComposerCaretWidth, ComposerTileHeight - ComposerTileGap * 2f));
            composerPrev = Chevron(well, "Previous", false, () => { if (LocalPlayer) LocalPlayer.Summoner.Step(-1); });
            composerNext = Chevron(well, "Next", true, () => { if (LocalPlayer) LocalPlayer.Summoner.Step(1); });

            buildButton = CardButton(chrome, "Build", "SPELL", SpellNow, true, ComposerWellHeight);
            Put((RectTransform)buildButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ComposerPad, -ComposerPad), new Vector2(ComposerTapWidth, ComposerWellHeight));
            buildLabel = LabelOf(buildButton);
            buildLabel.textWrappingMode = TextWrappingModes.Normal; buildLabel.lineSpacing = -12f;
            buildButton.gameObject.SetActive(false);
            var cancel = RoundButton(chrome, "Cancel", LobbyIcons.Close, CancelCraft, ComposerCloseSize);
            Put((RectTransform)cancel.transform, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.one * ComposerCloseSize);
            composerClose = cancel.gameObject;
            composerClose.SetActive(false);

            craftStatus = ComposerLine(chrome, "Craft status", CraftStatusFont);
            craftStatus.enableAutoSizing = true; craftStatus.fontSizeMin = CraftStatusMinFont; craftStatus.fontSizeMax = CraftStatusFont;

            composerHint = ComposerLine(composer, "Typed hint", ComposerHintFont);
            composerHint.gameObject.SetActive(false);
            craftRoot.SetActive(false);
        }

        TextMeshProUGUI ComposerLine(Transform parent, string label, float size)
        {
            var line = Copy(parent, label, "", LobbyFonts.Body, size, OnGame);
            Put(line.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ComposerPad, -(ComposerPad + ComposerWellHeight + ComposerRowGap)), new Vector2(ComposerLineMax, CraftStatusHeight));
            line.alignment = TextAlignmentOptions.Midline;
            line.textWrappingMode = TextWrappingModes.NoWrap; line.overflowMode = TextOverflowModes.Overflow;
            OnGameText(line); AddHudShadow(line);
            line.outlineWidth = ComposerLineOutline; line.outlineColor = Ink; line.fontStyle = FontStyles.Bold;
            return line;
        }

        GameObject Chevron(RectTransform well, string label, bool forward, Action action)
        {
            var rt = Put(Rect(label, well, Vector2.zero, Vector2.zero, Vector2.zero), new Vector2(forward ? 1f : 0f, .5f), new Vector2(forward ? 1f : 0f, .5f), new Vector2(forward ? -6f : 6f, 0f), new Vector2(36f, 56f));
            var face = Paint(rt, LobbyIcons.FrameSprite(12, ComposerSlotStroke), OnGame); face.raycastTarget = true;
            AddHudShadow(face);
            var glyph = Cover("Icon", rt, -6f).gameObject.AddComponent<Image>();
            glyph.sprite = LobbyIcons.Get(LobbyIcons.Chevron); glyph.color = OnGame; glyph.preserveAspect = true; glyph.raycastTarget = false;
            AddHudShadow(glyph);
            if (!forward) glyph.rectTransform.localRotation = Quaternion.Euler(0f, 0f, PreviousArrowTurn);
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None; button.targetGraphic = face;
            button.onClick.AddListener(() => action());
            var press = rt.gameObject.AddComponent<LobbyPress>();
            press.Body = (RectTransform)glyph.transform; press.Lift = 0f; press.Sink = 1f;
            press.Lean = forward ? 0f : PreviousArrowTurn;
            press.Hot = on => face.color = on ? Mint : OnGame;
            rt.gameObject.SetActive(false);
            return rt.gameObject;
        }

        void OpenComposer()
        {
            if (!LocalPlayer || Paused || ResultShown) return;
            if (!TypedMode) { ToggleCraft(); return; }
            if (LocalPlayer.Summoner.IsSpelling) CloseComposer();
            else { LocalPlayer.Summoner.Open(); SyncComposer(); }
        }

        void CloseComposer()
        {
            if (LocalPlayer) LocalPlayer.Summoner.Close();
            TouchBinding.Shared.SetCraftOpen(false);
            SyncComposer();
        }

        void SyncComposer()
        {
            if (!craftRoot) return;
            bool typed = TypedMode;
            var summoner = LocalPlayer ? LocalPlayer.Summoner : null;
            if (summoner) summoner.Typed = typed;
            bool open = summoner && summoner.IsSpelling && !ResultShown;
            if (open && !composerOpen)
            {
                composerText = ""; composerError = null; caretAt = Time.unscaledTime; openedFrame = Time.frameCount;
                if (typed) ClearFocus();
            }
            composerOpen = open;
            LayComposer(typed);
            if (craftRoot.activeSelf != open) craftRoot.SetActive(open);
            DesktopBinding.Typing = (open && typed) || composerReleaseWait;
            if (summoner && !summoner.IsSpelling && !summoner.IsCrafting && TouchBinding.Shared.CraftOpen && !TouchBinding.Shared.CraftPressPending)
                TouchBinding.Shared.SetCraftOpen(false);
        }

        void LayComposer(bool typed)
        {
            int layout = typed ? 1 : 0;
            if (composerLayout == layout) return;
            composerLayout = layout;
            composerChrome.SetActive(!typed);
            composerHint.gameObject.SetActive(typed);
            fitLine = null;
        }

        static float TileRun(int count) => count * ComposerTileWidth + Mathf.Max(0, count - 1) * ComposerTileGap;

        void FitComposer(int slots, bool typed, bool picking)
        {
            bool tap = !typed && TapSpelling;
            var said = typed ? composerHint : craftStatus;
            string line = said.text;
            float room = safe ? safe.rect.width - ComposerEdgeRoom : ComposerLineMax + ComposerPad * 2f;
            if (slots == fitSlots && line == fitLine && typed == fitTyped && picking == fitPicking && tap == fitTap && Mathf.Approximately(room, fitRoom)) return;
            fitSlots = slots; fitLine = line; fitTyped = typed; fitPicking = picking; fitTap = tap; fitRoom = room;
            bool letters = slots > 0;
            bool spell = tap && letters;
            float inner = room - ComposerPad * 2f;
            float tapRoom = spell ? ComposerRowGap + ComposerTapWidth : 0f;
            float field = letters ? TileRun(slots) + (picking ? ComposerArrowRoom * 2f : 0f) : 0f;
            float row = letters ? field + tapRoom : 0f;
            float lineWidth = Mathf.Min(said.GetPreferredValues(line).x, ComposerLineMax, inner);
            float width = Mathf.Max(row, lineWidth) + ComposerPad * 2f;
            float height = ComposerPad * 2f + CraftStatusHeight + (letters ? ComposerWellHeight + ComposerRowGap : 0f);
            composer.sizeDelta = new Vector2(width, height);
            float left = (width - row) * .5f;
            if (composerWell.gameObject.activeSelf != letters) composerWell.gameObject.SetActive(letters);
            composerWell.anchoredPosition = new Vector2(left, -ComposerPad);
            composerWell.sizeDelta = new Vector2(field, ComposerWellHeight);
            var build = (RectTransform)buildButton.transform;
            if (build.gameObject.activeSelf != spell) build.gameObject.SetActive(spell);
            build.anchoredPosition = new Vector2(left + field + ComposerRowGap, -ComposerPad);
            if (composerClose.activeSelf != tap) composerClose.SetActive(tap);
            said.rectTransform.anchoredPosition = new Vector2(ComposerPad, -(ComposerPad + (letters ? ComposerWellHeight + ComposerRowGap : 0f)));
            said.rectTransform.sizeDelta = new Vector2(width - ComposerPad * 2f, CraftStatusHeight);
        }

        void RefreshComposerHint()
        {
            if (composerHintVersion == KeyBindings.Version) return;
            composerHintVersion = KeyBindings.Version;
            composerHint.text = $"<b>ENTER</b> spell     <b>{ControlHints.KeyOf(DesktopBinding.Shared.Pause)}</b> back";
        }

        void OnTextTyped(char typed)
        {
            if (!AcceptsGameplayInput || !composerOpen || !TypedMode || BagOpen || Time.frameCount == openedFrame) return;
            typed = char.ToUpperInvariant(typed);
            if (typed < 'A' || typed > 'Z' || composerText.Length >= TrayTiles || typed == SpellBoxLetter()) return;
            composerText += typed;
            composerError = null; caretAt = Time.unscaledTime;
        }

        public void TypeWord(string word)
        {
            sb.Clear();
            foreach (char letter in (word ?? "").ToUpperInvariant())
                if (letter >= 'A' && letter <= 'Z' && sb.Length < TrayTiles) sb.Append(letter);
            composerText = sb.ToString();
            composerError = null; caretAt = Time.unscaledTime;
        }

        void ReadComposerKeys()
        {
            if (!AcceptsGameplayInput || !composerOpen || !TypedMode || BagOpen) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            var erase = keyboard.backspaceKey;
            if (erase.wasPressedThisFrame || (erase.isPressed && Time.unscaledTime >= eraseNext))
            {
                eraseNext = Time.unscaledTime + (erase.wasPressedThisFrame ? .45f : .05f);
                if (composerText.Length > 0) composerText = composerText.Substring(0, composerText.Length - 1);
                composerError = null; caretAt = Time.unscaledTime;
            }
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) SubmitComposer();
        }

        static char SpellBoxLetter()
        {
            var spell = DesktopBinding.Shared.SpellBox;
            string path = spell.bindings.Count > 0 ? spell.bindings[0].effectivePath : null;
            const string Prefix = "<Keyboard>/";
            if (path == null || path.Length != Prefix.Length + 1 || !path.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return '\0';
            char letter = char.ToUpperInvariant(path[Prefix.Length]);
            return letter >= 'A' && letter <= 'Z' ? letter : '\0';
        }

        void CloseComposerOnKeys()
        {
            var spell = DesktopBinding.Shared.SpellBox;
            if (composerReleaseWait)
            {
                if (!spell.IsPressed()) composerReleaseWait = false;
                return;
            }
            if (!composerOpen || !TypedMode || Paused || BagOpen || Time.frameCount == openedFrame || !spell.WasPressedThisFrame()) return;
            composerReleaseWait = true;
            DesktopBinding.Typing = true;
            if (ComposerReady()) SubmitComposer();
            else CancelCraft();
        }

        void SpellNow()
        {
            if (!AcceptsGameplayInput) return;
            if (TypedMode) SubmitComposer();
            else ConfirmCraft();
        }

        WordEntry EnabledRecipe(string word)
        {
            if (!LocalPlayer || string.IsNullOrEmpty(word)) return null;
            var recipe = LocalPlayer.Summoner.Recipe(word);
            if (recipe != null && LocalPlayer.Summoner.IsSignalWord(recipe.word)) return recipe;
            return recipe != null && GameConfig.Current.Items.TryGet(recipe.word, out var item) && item.Enabled ? recipe : null;
        }

        string MissingLetters(string word)
        {
            if (LocalPlayer.Inventory.SpellsFree) return "";
            spare.Clear(); spare.AddRange(LocalPlayer.Inventory.Letters);
            sb.Clear();
            foreach (char letter in word)
            {
                int at = spare.IndexOf(letter);
                if (at >= 0) spare.RemoveAt(at); else sb.Append(letter);
            }
            return sb.ToString();
        }

        static string Spread(string letters) => string.Join(" ", letters.ToCharArray());

        public void SubmitComposer()
        {
            if (!LocalPlayer || !composerOpen) return;
            var player = LocalPlayer;
            var summoner = player.Summoner;
            string word = composerText;
            var recipe = EnabledRecipe(word);
            bool signal = recipe != null && summoner.IsSignalWord(recipe.word);
            bool fits = recipe != null && (signal || SummonEffects.CanApply(player, recipe));
            bool furniture = !signal && player.Combat.IsHolding && !player.Combat.Weapon;
            string error = !player.CanAct || player.IsDodging ? "Wait until you can act again."
                : recipe == null ? "That recipe is not available."
                : furniture && !fits ? "Set down the furniture first."
                : player.Combat.IsChanneling ? "Finish your current action first."
                : summoner.IsCrafting ? "Finish your current word first."
                : !fits ? "Both hands are full. Drop some gear first."
                : MissingLetters(word).Length > 0 ? "You still need some letters."
                : null;
            if (error == null && summoner.BeginCraft(recipe))
            {
                Toast($"Spelling {recipe.word}…");
                SyncComposer();
                return;
            }
            composerError = error ?? "You still need some letters.";
            shakeAt = Time.unscaledTime;
        }

        bool ComposerReady()
        {
            if (!LocalPlayer || !composerOpen) return false;
            WordStatus(out int state);
            return state == 1;
        }

        string TypedStatus(out int state)
        {
            state = 2;
            if (composerError != null) return composerError;
            return WordStatus(out state);
        }

        string WordStatus(out int state)
        {
            string word = composerText;
            string spellKey = ControlHints.KeyOf(DesktopBinding.Shared.SpellBox);
            state = 0;
            if (word.Length == 0) return LocalPlayer.Inventory.SpellsFree ? Warmup.TypeAnything + " " + DesktopScheme.ClosesLine(spellKey) : DesktopScheme.EmptyLine(spellKey);
            var player = LocalPlayer;
            var recipe = EnabledRecipe(word);
            state = 2;
            if (recipe != null)
            {
                string missing = MissingLetters(word);
                if (missing.Length > 0) return $"{word} needs {Spread(missing)}. Smash more furniture.";
                if (player.Summoner.IsCrafting) return "Already spelling something. Hang on.";
                if (!player.Summoner.IsSignalWord(recipe.word) && !SummonEffects.CanApply(player, recipe))
                    return player.Combat.IsHolding && !player.Combat.Weapon ? "Set down the furniture first." : "Both hands are full. Drop something first.";
                state = 1;
                return DesktopScheme.ReadyLine(word, spellKey);
            }
            if (player.Summoner.StartsRecipe(word)) { state = 0; return "Keep going…"; }
            return DesktopScheme.UnknownLine(ControlHints.KeyOf(DesktopBinding.Shared.Bag));
        }

        void RefreshComposer()
        {
            if (!composerOpen || !LocalPlayer) return;
            var summoner = LocalPlayer.Summoner;
            bool typed = TypedMode;
            LayComposer(typed);
            string word, line;
            int state;
            if (typed)
            {
                word = composerText;
                line = TypedStatus(out state);
                RefreshComposerHint();
            }
            else if (summoner.SelectedWord != null)
            {
                word = summoner.SelectedWord.word; state = 1;
                line = TapSpelling ? $"{word} is ready. Tap SPELL." : $"{word} is ready. Let go to spell it.";
            }
            else if (summoner.Hints.Count > 0)
            {
                var (entry, missing) = summoner.Hints[0];
                word = entry.word; state = 2;
                line = $"{word} needs {Spread(missing)}. Smash more furniture.";
            }
            else
            {
                word = ""; state = 0;
                line = "Nothing to spell yet. Smash furniture for letters.";
            }
            if (craftStatus.text != line) craftStatus.text = line;
            craftStatus.color = state == 1 ? Mint : state == 2 ? LowHp : OnGame;
            bool picking = !typed && summoner.Ready.Count > 1;
            if (composerPrev.activeSelf != picking) { composerPrev.SetActive(picking); composerNext.SetActive(picking); }
            int count = Mathf.Min(word.Length, tiles.Length);
            int slots = typed && count < tiles.Length ? count + 1 : count;
            FitComposer(slots, typed, picking);
            LayTiles(word, slots, typed, state == 1);
            buildButton.interactable = typed || summoner.SelectedWord != null;
            string caption = typed ? "SPELL\n<size=62%><alpha=#99>ENTER</size>" : "SPELL";
            if (buildLabel.text != caption) buildLabel.text = caption;
            float shake = (Time.unscaledTime - shakeAt) / .24f;
            float x = shake < 1f ? Mathf.Sin(shake * Mathf.PI * 6f) * 8.75f * (1f - shake) * ScreenShake.Scale : 0f;
            composer.anchoredPosition = new Vector2(x, composer.anchoredPosition.y);
        }

        void LayTiles(string word, int slots, bool typed, bool ready)
        {
            const float width = ComposerTileWidth, height = ComposerTileHeight, gap = ComposerTileGap;
            int count = Mathf.Min(word.Length, slots);
            bool free = LocalPlayer.Inventory.SpellsFree;
            spare.Clear(); spare.AddRange(LocalPlayer.Inventory.Letters);
            float x0 = (composerWell.sizeDelta.x - TileRun(slots)) * .5f;
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                bool shown = i < slots;
                if (tile.root.gameObject.activeSelf != shown) tile.root.gameObject.SetActive(shown);
                if (!shown) continue;
                tile.root.anchoredPosition = new Vector2(x0 + i * (width + gap) + width * .5f, 0f);
                tile.root.sizeDelta = new Vector2(width, height);
                tile.root.localRotation = Quaternion.Euler(0f, 0f, i % 3 == 2 ? 3f : i % 3 == 0 ? -2f : 0f);
                bool filled = i < count;
                tile.lip.enabled = tile.edge.enabled = filled;
                tile.outline.enabled = !filled;
                if (!filled)
                {
                    tile.face.color = Color.clear;
                    if (tile.letter.text.Length > 0) tile.letter.text = "";
                    continue;
                }
                char letter = word[i];
                int at = spare.IndexOf(letter);
                bool got = free || at >= 0;
                if (at >= 0) spare.RemoveAt(at);
                tile.face.color = got ? Tile : MissingFill;
                tile.edge.color = ready ? ReadyEdge : got ? TileLine : MissingEdge;
                tile.lip.color = got ? TileEdge : MissingLip;
                tile.letter.color = got ? Ink : Bad;
                string text = One(letter);
                if (tile.letter.text != text) tile.letter.text = text;
            }
            bool blink = typed && count < slots && (Time.unscaledTime - caretAt) % 1f < .5f;
            if (caret.gameObject.activeSelf != blink) caret.gameObject.SetActive(blink);
            if (blink) caret.anchoredPosition = tiles[count].root.anchoredPosition;
        }

        void UpdateCards()
        {
            float now = Time.unscaledTime;
            if (toast && toast.gameObject.activeSelf)
            {
                float age = now - toastAt;
                if (age >= 2.9f) toast.gameObject.SetActive(false);
                else
                {
                    float shown = age < .2f ? age / .2f : age < 2.7f ? 1f : 1f - (age - 2.7f) / .2f;
                    toastFade.alpha = Mathf.Clamp01(shown) * (bagHidden ? 0f : 1f);
                    toast.anchoredPosition = new Vector2(0f, ToastY - topInset + 12.5f * (1f - Mathf.Clamp01(shown)));
                }
            }
            if (chip && chip.gameObject.activeSelf)
            {
                if (goUntil > 0f && now >= goUntil) HideCountdown();
                else
                {
                    float pulse = Mathf.Clamp01((now - chipPulseAt) / .25f);
                    chipDigit.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.18f, 1f, pulse);
                }
            }
            if (banner && banner.gameObject.activeSelf)
            {
                float age = now - bannerAt;
                if (age >= BannerSeconds) HideBanner();
                else bannerFade.alpha = Mathf.Clamp01(Mathf.Min(age / .2f, (BannerSeconds - age) / .3f)) * (bagHidden ? 0f : 1f);
            }
            RefreshModeActionVisibility();
            PulseCards();
            ReadComposerKeys();
            DrawMatchEndFaces();
        }

        void FitCards()
        {
            if (!safe) return;
            FitPauseSettings();
            float room = safe.rect.width - 32f;
            if (room <= 0f || Mathf.Approximately(room, fittedRoom)) return;
            fittedRoom = room;
            foreach (var (card, width) in cardWidths)
                if (card) card.sizeDelta = new Vector2(Mathf.Min(width, room), card.sizeDelta.y);
            FitMatchEnd();
            if (helpKeys)
            {
                float inner = Mathf.Min(812f, room) - 80f;
                helpKeys.cellSize = new Vector2((inner - helpKeys.spacing.x) * .5f, helpKeys.cellSize.y);
            }
        }

        public const string GoalPlateName = "Goal plate";
        const float GoalWidth = 252f, GoalPad = 12f, GoalHeadHeight = 28f, GoalRowHeight = 44f, GoalPicture = 38f, GoalTick = 22f, GoalGap = 8f;
        const float GoalHeadFont = 17f, GoalWordFont = 20f, GoalNoteFont = 15f, GoalLineFont = 18f, GoalDoneAlpha = .55f;
        const int GoalRadius = 14, GoalBandRadius = 10;

        sealed class GoalRow
        {
            public RectTransform root;
            public Image band, icon, tick;
            public RawImage picture;
            public CanvasGroup fade;
            public TextMeshProUGUI word, note, line;
        }

        RectTransform goalPlate;
        TextMeshProUGUI goalHead;
        readonly System.Collections.Generic.List<GoalRow> goalRows = new();
        int goalCount;

        public bool GoalPlateShown => goalPlate && goalPlate.gameObject.activeInHierarchy && goalCount > 0;
        public RectTransform GoalPlate => goalPlate;
        public string GoalHeading => GoalPlateShown ? goalHead.text : "";
        public int GoalCount => GoalPlateShown ? goalCount : 0;
        public string GoalWord(int index) => GoalPlateShown && index >= 0 && index < goalCount ? goalRows[index].word.text : "";
        public bool GoalTicked(int index) => GoalPlateShown && index >= 0 && index < goalCount && goalRows[index].tick.enabled;
        public bool GoalPictured(int index) => GoalPlateShown && index >= 0 && index < goalCount && goalRows[index].picture.enabled;

        public void SetGoals(string heading, System.Collections.Generic.IReadOnlyList<HudGoal> goals)
        {
            goalCount = goals?.Count ?? 0;
            var holder = objective ? objective.transform.parent : null;
            if (!holder) return;
            if (!goalPlate) BuildGoalPlate(holder);
            bool shown = goalCount > 0;
            goalPlate.gameObject.SetActive(shown);
            objective.enabled = !shown;
            if (!shown) return;
            bool headed = !string.IsNullOrEmpty(heading);
            goalHead.gameObject.SetActive(headed);
            goalHead.text = heading ?? "";
            float y = GoalPad + (headed ? GoalHeadHeight : 0f);
            while (goalRows.Count < goalCount) goalRows.Add(MakeGoalRow(goalRows.Count + 1));
            for (int i = 0; i < goalRows.Count; i++)
            {
                bool used = i < goalCount;
                goalRows[i].root.gameObject.SetActive(used);
                if (!used) continue;
                FillGoalRow(goalRows[i], goals[i]);
                goalRows[i].root.anchoredPosition = new Vector2(GoalPad, -y);
                y += GoalRowHeight;
            }
            goalPlate.sizeDelta = new Vector2(GoalWidth, y + GoalPad);
        }

        void BuildGoalPlate(Transform holder)
        {
            goalPlate = Put(Rect(GoalPlateName, holder, Vector2.zero, Vector2.zero, Vector2.zero), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(GoalWidth, GoalRowHeight));
            Paint(goalPlate, LobbyIcons.RoundedSprite(GoalRadius), KeyPill);
            goalHead = GoalText(goalPlate, "Heading", LobbyFonts.Black, GoalHeadFont, Gold, TextAlignmentOptions.MidlineLeft);
            Put(goalHead.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(GoalPad, -GoalPad), new Vector2(GoalWidth - 2f * GoalPad, GoalHeadHeight));
            goalHead.characterSpacing = 2f;
        }

        TextMeshProUGUI GoalText(Transform parent, string label, TMP_FontAsset font, float size, Color colour, TextAlignmentOptions alignment)
        {
            var text = Copy(parent, label, "", font, size, colour);
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true; text.fontSizeMin = Mathf.Min(MatchEndMinFont, size); text.fontSizeMax = size;
            return text;
        }

        GoalRow MakeGoalRow(int number)
        {
            float inner = GoalWidth - 2f * GoalPad, left = GoalPicture + GoalGap, right = GoalTick + GoalGap * .5f;
            var row = new GoalRow { root = Put(Rect("Goal " + number, goalPlate, Vector2.zero, Vector2.zero, Vector2.zero), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(inner, GoalRowHeight)) };
            row.band = Paint(Cover("Band", row.root, 2f), LobbyIcons.RoundedSprite(GoalBandRadius), GoldGlow);
            var body = Rect("Body", row.root, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(body);
            row.fade = body.gameObject.AddComponent<CanvasGroup>();
            row.fade.blocksRaycasts = row.fade.interactable = false;
            var art = Put(Rect("Picture", body, Vector2.zero, Vector2.zero, Vector2.zero), new Vector2(0f, .5f), new Vector2(0f, .5f), Vector2.zero, Vector2.one * GoalPicture);
            row.picture = art.gameObject.AddComponent<RawImage>(); row.picture.raycastTarget = false;
            row.icon = Picture(body, "Icon", null, OnGame, GoalPicture * .7f);
            Put(row.icon.rectTransform, new Vector2(0f, .5f), new Vector2(.5f, .5f), new Vector2(GoalPicture * .5f, 0f), Vector2.one * GoalPicture * .7f);
            row.icon.GetComponent<LayoutElement>().ignoreLayout = true;
            row.word = GoalText(body, "Word", LobbyFonts.Black, GoalWordFont, OnGame, TextAlignmentOptions.BottomLeft);
            Put(row.word.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(left, 0f), new Vector2(inner - left - right, GoalRowHeight * .55f));
            row.note = GoalText(body, "Note", LobbyFonts.Body, GoalNoteFont, TileRim, TextAlignmentOptions.TopLeft);
            Put(row.note.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(left, 0f), new Vector2(inner - left - right, GoalRowHeight * .45f));
            row.line = GoalText(body, "Line", LobbyFonts.Body, GoalLineFont, OnGame, TextAlignmentOptions.MidlineLeft);
            row.line.textWrappingMode = TextWrappingModes.Normal;
            Stretch(row.line.rectTransform);
            row.tick = Picture(row.root, "Tick", LobbyIcons.Get(LobbyIcons.Check), Mint, GoalTick);
            Put(row.tick.rectTransform, new Vector2(1f, .5f), new Vector2(1f, .5f), Vector2.zero, Vector2.one * GoalTick);
            row.tick.GetComponent<LayoutElement>().ignoreLayout = true;
            return row;
        }

        static void FillGoalRow(GoalRow row, HudGoal goal)
        {
            bool texty = goal.Word.Length == 0;
            bool pictured = !texty && Wreckabulary.UI.ItemArt.Apply(row.picture, goal.Word);
            if (texty) row.picture.enabled = false;
            bool iconed = !pictured && goal.Icon.Length > 0;
            row.icon.enabled = iconed;
            if (iconed) row.icon.sprite = LobbyIcons.Get(goal.Icon);
            bool lined = texty && goal.Icon.Length == 0;
            row.line.gameObject.SetActive(lined);
            row.word.gameObject.SetActive(!lined);
            row.note.gameObject.SetActive(!lined);
            row.line.text = lined ? goal.Note : "";
            row.word.text = lined ? "" : goal.Word;
            row.note.text = lined ? "" : goal.Note;
            row.word.color = goal.Done ? Mint : goal.Current ? Gold : OnGame;
            row.tick.enabled = goal.Done;
            row.band.enabled = goal.Current && !goal.Done;
            row.fade.alpha = goal.Done ? GoalDoneAlpha : 1f;
        }

        void UnhookKeyboard()
        {
            if (typingKeyboard != null) typingKeyboard.onTextInput -= OnTextTyped;
            typingKeyboard = null;
        }
    }
}
