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
        public int Broken, Crafted, Damage;
        public MatchRecord Reward;
        public bool Best;

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
        static readonly Color ComposerFill = Hex(0xfff0dc, .95f), Well = Hex(0x173b3c, .07f), ReadyEdge = Hex(0x5bb48e), ReadyRing = Hex(0x9ff8d3, .25f);
        static readonly Color ReadyInk = Hex(0x2f7a5c), Bad = Hex(0xb3533b), TileLine = Hex(0xdbc38f);
        static readonly Color MissingFill = Hex(0xf3d3c6), MissingEdge = Hex(0xe3a994), MissingLip = Hex(0xc98f7c), Go = Hex(0xdf7955);
        static readonly Color StepNumber = Hex(0xbe7754), StepInk = Hex(0x576f66), FinePrint = Hex(0x546763);

        RectTransform toast;
        CanvasGroup toastFade;
        TextMeshProUGUI toastLabel;
        float toastAt = -10f;
        string lastTitle = "", lastSub = "";
        RectTransform chip;
        Image chipDisc;
        TextMeshProUGUI chipDigit, chipLine;
        int chipLeft = -1;
        float chipPulseAt = -10f, goUntil;
        GameObject modeActions;
        TextMeshProUGUI modePlayLabel;
        Action modeAction;
        bool modeWanted;
        GameObject pauseCard, helpCard;
        TextMeshProUGUI pauseBlurb;
        Button resumeButton, helpDone;
        GameObject resultRoot;
        RectTransform resultShade;
        Image resultSpark;
        TextMeshProUGUI resultEyebrow, resultHeading, resultFlavour, resultDetail, resultNextLabel;
        readonly TextMeshProUGUI[] resultStats = new TextMeshProUGUI[3];
        GameObject resultReward, resultRewardGap, resultBest;
        TextMeshProUGUI resultCoins, resultScore;
        Button resultNext;
        Action resultNextAction, resultHomeAction;
        Coroutine resultSoon;
        Sprite sparkSprite;
        RectTransform composer, composerTiles, caret;
        Image composerEdge, composerRing;
        readonly ComposerTile[] tiles = new ComposerTile[TrayTiles];
        GameObject composerPrev, composerNext, bookLink;
        string composerText = "", composerError;
        bool composerOpen;
        float caretAt, shakeAt = -10f, eraseNext;
        int openedFrame = -1;
        bool composerReleaseWait;
        Keyboard typingKeyboard;
        readonly System.Collections.Generic.List<(RectTransform card, float width)> cardWidths = new();
        GridLayoutGroup helpKeys;
        float fittedRoom = -1f;

        sealed class ComposerTile
        {
            public RectTransform root;
            public Image lip, face, edge;
            public TextMeshProUGUI letter;
        }

        bool TypedMode => LocalPlayer && LocalPlayer.Binding is DesktopBinding && !TouchControlsShown;
        public bool ComposerOpen => craftRoot && craftRoot.activeSelf;
        public string ComposerText => composerText;
        public string ComposerStatus => craftStatus ? craftStatus.text : "";
        public string ToastText => toast && toast.gameObject.activeSelf ? toastLabel.text : "";
        public bool CountdownShown => chip && chip.gameObject.activeSelf;
        public string CountdownText => CountdownShown ? chipDigit.text : "";
        public bool ResultShown => resultRoot && resultRoot.activeSelf;
        public bool ResultPending => resultSoon != null;
        public bool HelpShown => helpCard && helpCard.activeSelf;

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
            var text = Copy(body, "Label", caption, primary ? LobbyFonts.Black : LobbyFonts.Body, 15f, primary ? Cream : Ink, primary ? 6f : 0f);
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
            BuildModeActions();
            BuildResult();
            BuildToast();
            typingKeyboard = Keyboard.current;
            if (typingKeyboard != null) typingKeyboard.onTextInput += OnTextTyped;
        }

        void BuildToast()
        {
            toast = Put(Rect("Toast", safe, Vector2.zero, Vector2.zero, Vector2.zero), new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -238f), new Vector2(300f, 50f));
            toastFade = toast.gameObject.AddComponent<CanvasGroup>();
            toastFade.blocksRaycasts = toastFade.interactable = false;
            SoftShadow(toast, ToastDrop, 37.5f, -10f);
            Paint(Cover("Face", toast), LobbyIcons.RoundedSprite(15), ToastFill);
            Paint(Cover("Edge", toast), LobbyIcons.FrameSprite(15, 1), ToastEdge);
            toastLabel = Copy(toast, "Text", "", LobbyFonts.Body, 15f, ToastInk);
            Stretch(toastLabel.rectTransform);
            toastLabel.rectTransform.offsetMin = new Vector2(25f, 15f); toastLabel.rectTransform.offsetMax = new Vector2(-25f, -15f);
            toast.gameObject.SetActive(false);
        }

        void BuildChip()
        {
            chip = Put(Rect("Round chip", safe, Vector2.zero, Vector2.zero, Vector2.zero), new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -25f), new Vector2(300f, 56f));
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

        void BuildModeActions()
        {
            var row = Put(Rect("Mode actions", safe, Vector2.zero, Vector2.zero, Vector2.zero), new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -300f), new Vector2(476f, 68f));
            modeActions = row.gameObject;
            var play = CardButton(row, "Play", "START WITH AI →", () => modeAction?.Invoke(), true, 60f);
            Put((RectTransform)play.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(260f, 60f));
            modePlayLabel = LabelOf(play);
            var home = CardButton(row, "Home", "Back to the house party", GoHome, false, 60f);
            Put((RectTransform)home.transform, Vector2.one, Vector2.one, Vector2.zero, new Vector2(200f, 60f));
            bagHides.Add(row.gameObject.AddComponent<CanvasGroup>());
            modeActions.SetActive(false);
        }

        public void Toast(string text)
        {
            if (!toast || string.IsNullOrEmpty(text)) return;
            toastLabel.text = text;
            float max = Mathf.Min(700f, safe.rect.width * .8f);
            var size = toastLabel.GetPreferredValues(text, max - 50f, 0f);
            toast.sizeDelta = new Vector2(Mathf.Min(max, size.x + 50f), size.y + 30f);
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
        }

        void BuildPause()
        {
            var shade = Rect("Pause", safe, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(shade);
            var dim = shade.gameObject.AddComponent<Image>(); dim.color = ModalShade; dim.raycastTarget = true;
            pauseRoot = shade.gameObject; pauseShade = shade;

            var card = Card("Pause card", shade, 575f, 44f);
            pauseCard = card.gameObject;
            Copy(card, "Eyebrow", "TAKE A BREATHER", LobbyFonts.Black, 12.5f, Eyebrow, 21f);
            Gap(card, 12.5f);
            Copy(card, "Heading", "The mess can wait.", LobbyFonts.Black, 40f, Ink, -3f);
            Gap(card, 12.5f);
            pauseBlurb = Copy(card, "Blurb", "", LobbyFonts.Body, 15f, BodyInk);
            Gap(card, 22.5f);
            resumeButton = CardButton(card, "Resume", "KEEP PLAYING →", TogglePause, true, 60f);
            Gap(card, 12.5f);
            CardButton(card, "Pause controls", "Controls & recipes", () => ShowHelp(true), false, 46f);
            Gap(card, 12.5f);
            CardButton(card, "Pause home", "Back to the house party", GoHome, false, 46f);
            bool touchable = Touchscreen.current != null || Application.isMobilePlatform;
            var gap = Gap(card, 12.5f); gap.SetActive(touchable);
            var touch = CardButton(card, "Touch switch", "Touch controls: off", () => { touchChosen = true; ShowTouchControls(!TouchControlsShown); }, false, 46f);
            touchToggle = LabelOf(touch);
            touch.gameObject.SetActive(touchable);

            BuildHelp(shade);
            pauseRoot.SetActive(false);
        }

        void BuildHelp(RectTransform shade)
        {
            var card = Card("Pause help", shade, 812f, 40f);
            helpCard = card.gameObject;
            var close = RoundButton(card, "Close help", LobbyIcons.Close, () => ShowHelp(false), 40f);
            Put((RectTransform)close.transform, Vector2.one, Vector2.one, new Vector2(-22f, -22f), Vector2.one * 40f);
            Copy(card, "Eyebrow", "A HOUSE FULL OF POSSIBILITIES", LobbyFonts.Black, 12.5f, Eyebrow, 21f);
            Gap(card, 10f);
            Copy(card, "Heading", "Everything starts with a word.", LobbyFonts.Black, 36f, Ink, -3f);
            Gap(card, 22f);

            var rules = GameConfig.Current.Rules.Defaults;
            int recipes = GameConfig.Current.Items.Enabled.Count();
            bool phone = Application.isMobilePlatform;
            var keys = DesktopBinding.Shared;
            string spell = phone ? "SPELL" : ControlHints.KeyOf(keys.Spell);
            string swing = phone ? "SMASH" : ControlHints.KeyOf(keys.Attack);
            var steps = Rect("Steps", card, Vector2.zero, Vector2.zero, Vector2.zero);
            var row = steps.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 25f; row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = true; row.childForceExpandHeight = false;
            void Step(string number, string title, string text)
            {
                var column = Rect("Step " + number, steps, Vector2.zero, Vector2.zero, Vector2.zero);
                var stack = column.gameObject.AddComponent<VerticalLayoutGroup>();
                stack.spacing = 4f; stack.childControlWidth = stack.childControlHeight = true;
                stack.childForceExpandWidth = true; stack.childForceExpandHeight = false;
                var size = column.gameObject.AddComponent<LayoutElement>(); size.preferredWidth = 1f; size.flexibleWidth = 1f;
                Copy(column, "Number", number, DisplayFont, 35f, StepNumber).alignment = TextAlignmentOptions.Left;
                Copy(column, "Title", title, LobbyFonts.Black, 20f, Ink).alignment = TextAlignmentOptions.Left;
                Copy(column, "Text", text, LobbyFonts.Body, 14f, StepInk).alignment = TextAlignmentOptions.TopLeft;
            }
            Step("01", "Break it.", "Smash the house's own furniture. A TABLE breaks into T, A, B, L and E.");
            Step("02", "Spell it.", $"Walk over letters; your bag holds {rules.MaxLetters}. Press {spell} to craft any of the {recipes} recipes.");
            Step("03", "Bring it.", $"Press {swing} to swing gear, throw it or set down a tool. Then wreck the place.");

            if (!phone)
            {
                Gap(card, 22f);
                var table = Rect("Keys", card, Vector2.zero, Vector2.zero, Vector2.zero);
                helpKeys = table.gameObject.AddComponent<GridLayoutGroup>();
                helpKeys.cellSize = new Vector2(356f, 32f); helpKeys.spacing = new Vector2(20f, 8f);
                helpKeys.constraint = GridLayoutGroup.Constraint.FixedColumnCount; helpKeys.constraintCount = 2;
                var entries = new[]
                {
                    ("WASD", "move"), ("MOUSE", "aim"),
                    (ControlHints.KeyOf(keys.Attack), "smash, throw, place, block"), (ControlHints.KeyOf(keys.Aim), "hold to aim"),
                    (ControlHints.KeyOf(keys.Dodge), "dodge"), (ControlHints.KeyOf(keys.Jump), "jump"),
                    (ControlHints.KeyOf(keys.Spell), "spell"), (ControlHints.KeyOf(keys.Hand1) + " " + ControlHints.KeyOf(keys.Hand2), "switch hand"),
                    (ControlHints.KeyOf(keys.Bag), "hold for bag and map"), (ControlHints.KeyOf(keys.Interact), "pick up, hold to revive"),
                    (ControlHints.KeyOf(keys.Drop), "hold to drop gear"), (ControlHints.KeyOf(keys.Pause), "pause"),
                };
                foreach (var (key, what) in entries)
                {
                    var cell = Rect(what, table, Vector2.zero, Vector2.zero, Vector2.zero);
                    var cap = Put(Rect("Key", cell, Vector2.zero, Vector2.zero, Vector2.zero), new Vector2(0f, .5f), new Vector2(0f, .5f), Vector2.zero, new Vector2(40f, 30f));
                    Paint(Cover("Lip", cap, 0f, -2.5f), LobbyIcons.RoundedSprite(8), Hex(0xdcc9a6));
                    Paint(Cover("Face", cap), LobbyIcons.RoundedSprite(8), Hex(0xfffaf0));
                    Paint(Cover("Edge", cap), LobbyIcons.FrameSprite(8, 1), Hair);
                    var name = Copy(cap, "Name", key, LobbyFonts.Black, 13f, Ink);
                    Stretch(name.rectTransform); name.textWrappingMode = TextWrappingModes.NoWrap;
                    cap.sizeDelta = new Vector2(Mathf.Max(40f, name.GetPreferredValues(key).x + 20f), 30f);
                    var does = Copy(cell, "Does", what, LobbyFonts.Body, 14f, StepInk);
                    Stretch(does.rectTransform); does.rectTransform.offsetMin = new Vector2(cap.sizeDelta.x + 12f, 0f);
                    does.alignment = TextAlignmentOptions.MidlineLeft; does.textWrappingMode = TextWrappingModes.NoWrap;
                }
            }
            Gap(card, 18f);
            Copy(card, "Fine print", $"Health is {Mathf.RoundToInt(rules.MaxHealth)} HP. Your looks never change your stats.", LobbyFonts.Body, 12.5f, FinePrint);
            Gap(card, 22f);
            helpDone = CardButton(card, "Help done", "GOT IT. LET’S PLAY. →", () => SetPaused(false), true, 60f);
            helpCard.SetActive(false);
        }

        void ShowHelp(bool on)
        {
            if (!helpCard) return;
            helpCard.SetActive(on);
            pauseCard.SetActive(!on);
            FocusOn(on ? helpDone : resumeButton);
        }

        string ModeBlurb()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == Session.TutorialScene)
                return "A friendly room to try smashing, spelling and every skill.";
            return Match.Mode switch
            {
                "Duos" => "Watch each other’s backs. Hold interact to revive your buddy.",
                "MovingOut" => "Find every keepsake, then get the whole crew to the van before the house is packed.",
                "MovingDay" => "Spell the checklist furniture and place it in its marked room.",
                _ => $"Smash, spell, survive. First to {NumberWord(Match.Rules.RoundsToWin)} rounds wins.",
            };
        }

        static string NumberWord(int n) => n switch
        {
            1 => "one", 2 => "two", 3 => "three", 4 => "four", 5 => "five", 6 => "six", 7 => "seven", 8 => "eight", 9 => "nine",
            _ => n.ToString(),
        };

        void OnPauseShown()
        {
            if (pauseBlurb) pauseBlurb.text = ModeBlurb();
            ShowHelp(false);
        }

        void BuildResult()
        {
            resultShade = Rect("Result", safe, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(resultShade);
            var dim = resultShade.gameObject.AddComponent<Image>(); dim.color = ModalShade; dim.raycastTarget = true;
            resultRoot = resultShade.gameObject;
            var card = Card("Result card", resultShade, 575f, 44f);
            resultSpark = Picture(card, "Spark", SparkSprite(), Spark, 62.5f);
            Gap(card, 10f);
            resultEyebrow = Copy(card, "Eyebrow", "", LobbyFonts.Black, 12.5f, Eyebrow, 21f);
            Gap(card, 10f);
            resultHeading = Copy(card, "Heading", "", LobbyFonts.Black, 40f, Ink, -3f);
            Gap(card, 8f);
            resultFlavour = Copy(card, "Flavour", "", LobbyFonts.Body, 15f, BodyInk);
            resultDetail = Copy(card, "Detail", "", LobbyFonts.Black, 15f, Ink);
            Gap(card, 22.5f);

            var stats = Rect("Stats", card, Vector2.zero, Vector2.zero, Vector2.zero);
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
            string[] names = { "OBJECTS WRECKED", "WORDS CRAFTED", "DAMAGE DEALT" };
            for (int i = 0; i < 3; i++)
            {
                var cell = Rect(names[i], stats, Vector2.zero, Vector2.zero, Vector2.zero);
                cell.anchorMin = new Vector2(i / 3f, 0f); cell.anchorMax = new Vector2((i + 1) / 3f, 1f); cell.offsetMin = cell.offsetMax = Vector2.zero;
                var number = resultStats[i] = Copy(cell, "Number", "0", DisplayFont, 31f, Ink);
                number.rectTransform.anchorMin = new Vector2(0f, .42f); number.rectTransform.anchorMax = Vector2.one;
                number.rectTransform.offsetMin = Vector2.zero; number.rectTransform.offsetMax = new Vector2(0f, -8f);
                number.textWrappingMode = TextWrappingModes.NoWrap;
                var label = Copy(cell, "Label", names[i], LobbyFonts.Black, 11f, BodyInk, 12f);
                label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = new Vector2(1f, .42f);
                label.rectTransform.offsetMin = new Vector2(4f, 10f); label.rectTransform.offsetMax = new Vector2(-4f, 0f);
                label.alignment = TextAlignmentOptions.Top; label.textWrappingMode = TextWrappingModes.NoWrap;
                label.enableAutoSizing = true; label.fontSizeMin = 8f; label.fontSizeMax = 11f;
            }

            resultRewardGap = Gap(card, 16f);
            var reward = Rect("Reward", card, Vector2.zero, Vector2.zero, Vector2.zero);
            resultReward = reward.gameObject;
            var rewardRow = reward.gameObject.AddComponent<HorizontalLayoutGroup>();
            rewardRow.childAlignment = TextAnchor.MiddleCenter; rewardRow.spacing = 16f;
            rewardRow.childControlWidth = rewardRow.childControlHeight = true;
            rewardRow.childForceExpandWidth = rewardRow.childForceExpandHeight = false;
            var rewardSize = reward.gameObject.AddComponent<LayoutElement>(); rewardSize.preferredHeight = rewardSize.minHeight = 30f;
            var coin = Picture(reward, "Coin", LobbyIcons.Get(LobbyIcons.Coin), CoinGold, 24f);
            coin.GetComponent<LayoutElement>().preferredWidth = 24f;
            resultCoins = Copy(reward, "Coins", "", LobbyFonts.Black, 17f, Ink);
            resultScore = Copy(reward, "Score", "", LobbyFonts.Body, 15f, BodyInk);
            resultBest = Copy(reward, "Best", "New best!", LobbyFonts.Black, 15f, NewBest).gameObject;
            foreach (var text in reward.GetComponentsInChildren<TextMeshProUGUI>(true)) text.textWrappingMode = TextWrappingModes.NoWrap;

            Gap(card, 22.5f);
            resultNext = CardButton(card, "Next", "NEXT ROUND →", ConfirmResult, true, 60f);
            resultNextLabel = LabelOf(resultNext);
            Gap(card, 12.5f);
            CardButton(card, "Result home", "Back to the house party", () =>
            {
                var home = resultHomeAction;
                HideResult();
                if (home != null) home(); else GoHome();
            }, false, 46f);
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
            resultEyebrow.text = result.Final ? "HOUSE PARTY COMPLETE" : $"ROUND {result.Round} COMPLETE";
            resultHeading.text = result.Heading ?? "";
            resultFlavour.text = result.Won ? "A little imagination goes a long way." : "Every good mess teaches you a new trick.";
            resultDetail.text = result.Detail ?? "";
            resultDetail.gameObject.SetActive(!string.IsNullOrEmpty(result.Detail));
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
            }
            resultNextLabel.text = result.Final ? "PLAY AGAIN →" : "NEXT ROUND →";
            resultRoot.SetActive(true);
            resultRoot.transform.SetAsLastSibling();
            RefreshBagPanel();
            FocusOn(resultNext);
        }

        public void ShowResultSoon(HudResult result, Action next, float delay = .9f)
        {
            if (resultSoon != null) StopCoroutine(resultSoon);
            resultSoon = StartCoroutine(ResultAfter(result, next, delay));
        }

        IEnumerator ResultAfter(HudResult result, Action next, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            resultSoon = null;
            ShowResult(result, next);
        }

        public void HideResult()
        {
            if (resultSoon != null) { StopCoroutine(resultSoon); resultSoon = null; }
            resultNextAction = resultHomeAction = null;
            if (resultRoot && resultRoot.activeSelf) { resultRoot.SetActive(false); ClearFocus(); }
        }

        public void ConfirmResult()
        {
            if (!ResultShown) return;
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
            composer = Put(Rect("Craft drawer", safe, Vector2.zero, Vector2.zero, Vector2.zero), new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 262f), new Vector2(650f, 189f));
            craftRoot = composer.gameObject;
            var hit = composer.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            SoftShadow(composer, CardDrop, 75f, -19f);
            Paint(Cover("Face", composer), LobbyIcons.RoundedSprite(25), ComposerFill);
            composerEdge = Paint(Cover("Edge", composer), LobbyIcons.FrameSprite(25, 2), Hair);
            composerRing = Paint(Cover("Ring", composer, 5f), LobbyIcons.FrameSprite(30, 5), ReadyRing);
            composerRing.enabled = false;

            var eyebrow = Copy(composer, "Eyebrow", "SPELL A WORD", LobbyFonts.Black, 12.5f, Faded, 21f);
            Put(eyebrow.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -16f), new Vector2(300f, 40f));
            eyebrow.alignment = TextAlignmentOptions.MidlineLeft; eyebrow.textWrappingMode = TextWrappingModes.NoWrap;
            var book = MakeButton("Recipe book", composer, Vector2.one, new Vector2(-70f, -16f), new Vector2(170f, 40f),
                "<u>Recipe book</u>", () => SetBagPinned(true), Color.clear);
            var bookText = book.GetComponentInChildren<TextMeshProUGUI>();
            bookText.fontSize = 13f; bookText.color = Faded; bookText.alignment = TextAlignmentOptions.Right;
            bookLink = book.gameObject;
            var cancel = RoundButton(composer, "Cancel", LobbyIcons.Close, CancelCraft, 40f);
            Put((RectTransform)cancel.transform, Vector2.one, Vector2.one, new Vector2(-20f, -16f), Vector2.one * 40f);

            var well = Put(Rect("Field", composer, Vector2.zero, Vector2.zero, Vector2.zero), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -64f), new Vector2(490f, 75f));
            Paint(well, LobbyIcons.RoundedSprite(16), Well);
            composerTiles = Rect("Tiles", well, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(composerTiles);
            for (int i = 0; i < tiles.Length; i++)
            {
                var root = Put(Rect($"Tile {i + 1}", composerTiles, Vector2.zero, Vector2.zero, Vector2.zero), new Vector2(0f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(47.5f, 55f));
                var tile = tiles[i] = new ComposerTile { root = root };
                tile.lip = Paint(Cover("Lip", root, 0f, -3.75f), LobbyIcons.RoundedSprite(9), TileEdge);
                tile.face = Paint(Cover("Face", root), LobbyIcons.RoundedSprite(9), Tile);
                tile.edge = Paint(Cover("Edge", root), LobbyIcons.FrameSprite(9, 1), TileLine);
                tile.letter = Copy(root, "Letter", "", DisplayFont, 32f, Ink);
                Stretch(tile.letter.rectTransform); tile.letter.textWrappingMode = TextWrappingModes.NoWrap;
                root.gameObject.SetActive(false);
            }
            caret = Put(CreateImage("Caret", composerTiles, Vector2.zero, Vector2.zero, Vector2.zero, Ink).rectTransform, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(12.5f, 0f), new Vector2(4f, 43f));
            composerPrev = Chevron(well, "Previous", false, () => { if (LocalPlayer) LocalPlayer.Summoner.Step(-1); });
            composerNext = Chevron(well, "Next", true, () => { if (LocalPlayer) LocalPlayer.Summoner.Step(1); });

            buildButton = CardButton(composer, "Build", "SPELL", SpellNow, true, 75f);
            Put((RectTransform)buildButton.transform, Vector2.one, Vector2.one, new Vector2(-20f, -64f), new Vector2(110f, 75f));
            buildLabel = LabelOf(buildButton);
            buildLabel.textWrappingMode = TextWrappingModes.Normal; buildLabel.lineSpacing = -12f;

            craftStatus = Copy(composer, "Craft status", "", LobbyFonts.Body, 13f, Faded);
            Put(craftStatus.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -150f), new Vector2(610f, 24f));
            craftStatus.alignment = TextAlignmentOptions.MidlineLeft;
            craftStatus.enableAutoSizing = true; craftStatus.fontSizeMin = 10f; craftStatus.fontSizeMax = 13f;
            craftRoot.SetActive(false);
        }

        GameObject Chevron(RectTransform well, string label, bool forward, Action action)
        {
            var rt = Put(Rect(label, well, Vector2.zero, Vector2.zero, Vector2.zero), new Vector2(forward ? 1f : 0f, .5f), new Vector2(forward ? 1f : 0f, .5f), new Vector2(forward ? -6f : 6f, 0f), new Vector2(36f, 56f));
            var face = Paint(rt, LobbyIcons.RoundedSprite(12), Quiet); face.raycastTarget = true;
            var glyph = Cover("Icon", rt, -6f).gameObject.AddComponent<Image>();
            glyph.sprite = LobbyIcons.Get(LobbyIcons.Chevron); glyph.color = Ink; glyph.preserveAspect = true; glyph.raycastTarget = false;
            if (!forward) glyph.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None; button.targetGraphic = face;
            button.onClick.AddListener(() => action());
            var press = rt.gameObject.AddComponent<LobbyPress>();
            press.Body = (RectTransform)glyph.transform; press.Lift = 0f; press.Sink = 1f;
            press.Hot = on => face.color = on ? QuietHover : Quiet;
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
            if (craftRoot.activeSelf != open) craftRoot.SetActive(open);
            DesktopBinding.Typing = (open && typed) || composerReleaseWait;
            if (summoner && !summoner.IsSpelling && !summoner.IsCrafting && TouchBinding.Shared.CraftOpen && !TouchBinding.Shared.CraftPressPending)
                TouchBinding.Shared.SetCraftOpen(false);
        }

        void OnTextTyped(char typed)
        {
            if (!composerOpen || !TypedMode || Paused || BagOpen || Time.frameCount == openedFrame) return;
            typed = char.ToUpperInvariant(typed);
            if (typed < 'A' || typed > 'Z' || composerText.Length >= TrayTiles) return;
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
            if (!composerOpen || !TypedMode || Paused || BagOpen) return;
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

        void CloseComposerOnKeys()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (composerReleaseWait)
            {
                bool held = (keyboard != null && keyboard.tabKey.isPressed) || (mouse != null && mouse.rightButton.isPressed);
                if (!held) composerReleaseWait = false;
                return;
            }
            if (!composerOpen || !TypedMode || Paused || BagOpen) return;
            bool tab = keyboard != null && keyboard.tabKey.wasPressedThisFrame;
            bool right = mouse != null && mouse.rightButton.wasPressedThisFrame;
            if (!tab && !right) return;
            composerReleaseWait = true;
            DesktopBinding.Typing = true;
            CancelCraft();
        }

        void SpellNow()
        {
            if (TypedMode) SubmitComposer();
            else ConfirmCraft();
        }

        WordEntry EnabledRecipe(string word)
        {
            if (!LocalPlayer || string.IsNullOrEmpty(word)) return null;
            var recipe = LocalPlayer.Summoner.Recipe(word);
            return recipe != null && GameConfig.Current.Items.TryGet(recipe.word, out var item) && item.Enabled ? recipe : null;
        }

        string MissingLetters(string word)
        {
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
            bool furniture = player.Combat.IsHolding && !player.Combat.Weapon;
            string error = !player.CanAct || player.IsDodging ? "Wait until you can act again."
                : recipe == null ? "That recipe is not available."
                : furniture && !SummonEffects.CanApply(player, recipe) ? "Set down the furniture first."
                : player.Combat.IsChanneling ? "Finish your current action first."
                : summoner.IsCrafting ? "Finish your current word first."
                : !SummonEffects.CanApply(player, recipe) ? "Both hands are full. Drop some gear first."
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

        string TypedStatus(out int state)
        {
            state = 2;
            if (composerError != null) return composerError;
            string word = composerText;
            state = 0;
            if (word.Length == 0) return "Type a word you can make from your letters. Tab or right-click closes.";
            var player = LocalPlayer;
            var recipe = EnabledRecipe(word);
            state = 2;
            if (recipe != null)
            {
                string missing = MissingLetters(word);
                if (missing.Length > 0) return $"{word} needs {Spread(missing)}. Smash more furniture.";
                if (player.Summoner.IsCrafting) return "Already spelling something. Hang on.";
                if (!SummonEffects.CanApply(player, recipe))
                    return player.Combat.IsHolding && !player.Combat.Weapon ? "Set down the furniture first." : "Both hands are full. Drop something first.";
                state = 1;
                return $"{word} is ready. Press Enter!";
            }
            if (player.Summoner.StartsRecipe(word)) { state = 0; return "Keep going…"; }
            return "That isn't a recipe. Check the book with " + ControlHints.KeyOf(DesktopBinding.Shared.Bag) + ".";
        }

        void RefreshComposer()
        {
            if (!composerOpen || !LocalPlayer) return;
            var summoner = LocalPlayer.Summoner;
            bool typed = TypedMode;
            string word, line;
            int state;
            if (typed)
            {
                word = composerText;
                line = TypedStatus(out state);
            }
            else if (summoner.SelectedWord != null)
            {
                word = summoner.SelectedWord.word; state = 1;
                bool touch = TouchControlsShown || LocalPlayer.Binding is TouchBinding;
                line = touch ? $"{word} is ready. Tap SPELL." : $"{word} is ready. Let go to spell it.";
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
            craftStatus.color = state == 1 ? ReadyInk : state == 2 ? Bad : Faded;
            composerEdge.color = state == 1 ? ReadyEdge : Hair;
            composerRing.enabled = state == 1;
            bool picking = !typed && summoner.Ready.Count > 1;
            if (composerPrev.activeSelf != picking) { composerPrev.SetActive(picking); composerNext.SetActive(picking); }
            LayTiles(word, typed, picking);
            buildButton.interactable = typed || summoner.SelectedWord != null;
            string caption = typed ? "SPELL\n<size=62%><alpha=#99>ENTER</size>" : "SPELL";
            if (buildLabel.text != caption) buildLabel.text = caption;
            float shake = (Time.unscaledTime - shakeAt) / .24f;
            float x = shake < 1f ? Mathf.Sin(shake * Mathf.PI * 6f) * 8.75f * (1f - shake) : 0f;
            composer.anchoredPosition = new Vector2(x, composer.anchoredPosition.y);
        }

        void LayTiles(string word, bool typed, bool picking)
        {
            const float width = 47.5f, height = 55f, gap = 7.5f, field = 490f;
            int count = Mathf.Min(word.Length, tiles.Length);
            spare.Clear(); spare.AddRange(LocalPlayer.Inventory.Letters);
            float room = field - 25f - (typed ? 14f : 0f) - (picking ? 84f : 0f);
            float run = count * width + Mathf.Max(0, count - 1) * gap;
            float scale = run > room ? room / run : 1f;
            float x0 = typed ? 12.5f : (field - run * scale) * .5f;
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                bool shown = i < count;
                if (tile.root.gameObject.activeSelf != shown) tile.root.gameObject.SetActive(shown);
                if (!shown) continue;
                char letter = word[i];
                int at = spare.IndexOf(letter);
                bool got = at >= 0;
                if (got) spare.RemoveAt(at);
                tile.root.anchoredPosition = new Vector2(x0 + (i * (width + gap) + width * .5f) * scale, 0f);
                tile.root.sizeDelta = new Vector2(width, height) * scale;
                tile.root.localRotation = Quaternion.Euler(0f, 0f, i % 3 == 2 ? 3f : i % 3 == 0 ? -2f : 0f);
                tile.face.color = got ? Tile : MissingFill;
                tile.edge.color = got ? TileLine : MissingEdge;
                tile.lip.color = got ? TileEdge : MissingLip;
                tile.letter.color = got ? Ink : Bad;
                tile.letter.fontSize = 32f * scale;
                string text = letter.ToString();
                if (tile.letter.text != text) tile.letter.text = text;
            }
            bool blink = typed && (Time.unscaledTime - caretAt) % 1f < .5f;
            if (caret.gameObject.activeSelf != blink) caret.gameObject.SetActive(blink);
            caret.anchoredPosition = new Vector2(x0 + (count > 0 ? run * scale + 6f : 0f), 0f);
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
                    toast.anchoredPosition = new Vector2(0f, -238f + 12.5f * (1f - Mathf.Clamp01(shown)));
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
            if (modeActions)
            {
                bool shown = modeWanted && !Paused && !ResultShown;
                if (modeActions.activeSelf != shown) modeActions.SetActive(shown);
            }
            ReadComposerKeys();
        }

        void FitCards()
        {
            if (!safe) return;
            float room = safe.rect.width - 32f;
            if (room <= 0f || Mathf.Approximately(room, fittedRoom)) return;
            fittedRoom = room;
            foreach (var (card, width) in cardWidths)
                if (card) card.sizeDelta = new Vector2(Mathf.Min(width, room), card.sizeDelta.y);
            if (helpKeys)
            {
                float inner = Mathf.Min(812f, room) - 80f;
                helpKeys.cellSize = new Vector2((inner - helpKeys.spacing.x) * .5f, helpKeys.cellSize.y);
            }
        }

        void UnhookKeyboard()
        {
            if (typingKeyboard != null) typingKeyboard.onTextInput -= OnTextTyped;
            typingKeyboard = null;
        }
    }
}
