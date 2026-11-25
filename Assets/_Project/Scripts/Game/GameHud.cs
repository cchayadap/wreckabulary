using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public partial class GameHud : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI title, subtitle, timer, scoreboard, instruction, checklist;
        [SerializeField] GameObject checklistPanel;
        public Canvas UiCanvas { get; private set; }
        public bool TouchControlsShown => touchRoot && touchRoot.activeSelf;
        public PlayerController LocalPlayer { get; private set; }
        public bool BagOpen => bagPanel && bagPanel.activeSelf;
        public UIStateController StateController { get; private set; }
        public bool Paused => StateController && StateController.CurrentState == UIState.PauseMenu;
        public bool AcceptsGameplayInput => StateController && StateController.CurrentState == UIState.GameplayHUD && !StateController.IsTransitioning;
        public static GameHud Active { get; private set; }
        public bool NeedsPointer => !AcceptsGameplayInput || pointerFreed || BagOpen || (craftRoot && craftRoot.activeSelf) || (typewriter && typewriter.User) || (modeActions && modeWanted) || ResultShown || LeaveAsked;
        public bool FullCardShown => ResultShown || ShowcaseShown;
        public static bool? PauseOnFocusLossOverride;
        static bool PauseOnFocusLoss => PauseOnFocusLossOverride ?? !Application.isBatchMode;

        static readonly Color Ink = Hex(0x173b3c), Cream = Hex(0xfff0d9), Paper = Hex(0xfff0dc, .92f);
        static readonly Color Teal = Hex(0x193f3d, .93f), Tile = Hex(0xf4dca7), TileEdge = Hex(0xbba06b);
        static readonly Color Slot = Hex(0xf7e5c8), SlotActive = Hex(0xdeedcf), Track = Hex(0x153c3c, .13f);
        static readonly Color Health = Hex(0x419681), Low = Hex(0xd16849), Faded = Hex(0x62736a);
        static readonly Color EmptyCell = Hex(0xead9b7, .35f), Reserved = Hex(0xc8e6d8), ReservedInk = Hex(0x2f6b5c);
        static readonly Color MapBg = Hex(0x193c3b, .85f), MapEdge = Hex(0xf1dfbd, .85f), RoomFill = Hex(0xaaad82, .38f);
        static readonly Color RoomEdge = Hex(0xe4d6b2, .53f), RoomEdgeHere = Hex(0xc9ffe9);
        static readonly Color RoomHere = Hex(0x9ff8d3, .35f), RoomWarn = Hex(0xe79553), RoomClosed = Hex(0xd85c45, .65f);
        static readonly Color Mint = Hex(0xb7dfc8), Coral = Hex(0xe7785e), Dark = Hex(0x1a2a30, .94f);
        static readonly Color OnGame = Hex(0xfff7e8), LowHp = Hex(0xff8a6b), Shade = Hex(0x0b1f1f, .8f);
        static readonly Color Glass = Hex(0x1d2b2b, .28f), GlassActive = Hex(0x1d2b2b, .40f), GlassEdge = Hex(0xfff7e8, .85f), GlassIdle = Hex(0xffffff, .17f);
        static readonly Color BagInk = Hex(0xfff4e2), BagCell = Hex(0xffffff, .08f), BagCellEdge = Hex(0xffffff, .25f);
        static readonly Color BagCard = Hex(0xffffff, .09f), BagCardActive = Hex(0xffffff, .15f), BagCardEdge = Hex(0xffffff, .24f);
        static readonly Color Gold = Hex(0xffe7b8), GoldGlow = Hex(0xffd46a, .4f), TileGot = Hex(0xe8c48b), TileGotInk = Hex(0x3b2614);
        static readonly Color TileMissingInk = Hex(0xffffff, .65f), TileRim = Hex(0xfff4e2, .55f);
        static readonly Color KeyPill = Hex(0x1e2a2e, .8f), CellRim = Hex(0x1e2a2e, .55f);
        static readonly string[] HandNumbers = { "1", "2" };
        static readonly string[] LetterTexts = Enumerable.Range('A', 26).Select(c => ((char)c).ToString()).ToArray();
        static readonly ProfilerMarker RefreshMarker = new("Hud.Refresh");
        const float BagTop = 105f, BagSide = 35f, BagBottom = 35f, BagColumn = 375f, BookColumn = 840f, BagGap = 24f;
        const float Chip = 52f, ChipGap = 10f;
        const float VitalsX = 39f, VitalsY = 38f, TrayY = 52f, AsideVitalsY = 276f;
        const float VitalsBarX = 46f, VitalsCrossX = 17f, VitalsCross = 33f, EffectsWidth = 320f, EffectsHeight = 26f, EffectsSize = 18f, EffectsGap = 6f;
        const float SideGap = 12f, SideRowHeight = 54f, SideRowGap = 6f, RoomHeight = 64f, RoomGap = 16f;
        const float ObjectiveWidth = 252f, ObjectiveMinHeight = 48f;
        public const float BookTitleTop = 4f, BookTitleHeight = 40f, BookKeyGap = 4f, BookKeyHeight = 30f, BookCardsGap = 6f, BookTextLeft = 48f;
        public const float BookKeyTop = BookTitleTop + BookTitleHeight + BookKeyGap, BookCardsTop = BookKeyTop + BookKeyHeight + BookCardsGap;
        public const float BookCloseSize = 40f, BookCloseInset = 4f, BookCloseGap = 8f;
        const float BookGlyphCentre = 21f, BookKeyStar = 18f, BookKeyFont = 20f, BookKeyMinFont = 15f;
        public const float BookCardHeight = 220f, BookPicture = 112f, BookPictureTop = 16f, BookTileY = 26f, BookFactY = 64f;
        const float BookBarY = 4f, BookBarCaption = 52f, BookBarTrack = 30f, BookFactFont = 16f, BookBarFont = 15f;
        const int ChipsPerRow = 6;
        public const string FreeSpelling = "Free spelling", FreeBookLine = "Warm-up: every recipe is free";

        readonly List<Sprite> ownedSprites = new();
        readonly List<Texture2D> ownedTextures = new();
        readonly List<TouchStick> sticks = new();
        readonly Dictionary<TouchAction, (TouchActionButton button, TextMeshProUGUI label)> skillButtons = new();
        readonly StringBuilder sb = new();
        const int TrayTiles = 10, TilesPerRow = 5;
        readonly LetterCell[] trayCells = new LetterCell[TrayTiles], bagCells = new LetterCell[TrayTiles];
        readonly HandView[] hands = new HandView[2], bagHands = new HandView[2];
        IReadOnlyList<PlayerController> players;
        Func<PlayerController, int> wins;
        int roundsToWin;
        bool showWins;
        string scoreUnit;
        PlayerJoinManager joins;
        Typewriter typewriter;
        RoomBuilder rooms;
        ClearOutController clearOut;
        RoundManager rounds;
        RectTransform safe, side, status, vitals, tray, hint;
        GameObject touchRoot, craftRoot, desktopHints, bagPanel, pauseRoot, crosshair;
        GameObject typewriterControls;
        TextMeshProUGUI typewriterChoice;
        TextMeshProUGUI matchTag, matchTitle, matchDetail, roomPill, objective, statusText;
        TextMeshProUGUI effects, interactPrompt;
        VitalsView soloVitals;
        readonly List<Image> crosshairTicks = new();
        GameObject drawMeter;
        RectTransform drawFill;
        TextMeshProUGUI aliveText, hintText, bagPanelCount, touchToggle, spellKey, bagKey;
        TextMeshProUGUI craftStatus, buildLabel, spellHint;
        long spellHintKey = long.MinValue;
        SpellNeeds spellHintNeeds;
        IReadOnlyCollection<string> spellHintObjectives;
        Image craftProgress;
        Button buildButton;
        RectTransform playRect;
        MapView miniMap, bigMap;
        Texture2D iconAtlas;
        Sprite roundSprite, panelSprite, ringSprite;
        Rect lastSafe;
        int lastWidth, lastHeight;
        float nextRefresh, resumeScale = 1f;
        bool bagPinned, trayAside;
        int sideKey = -1;
        readonly List<CanvasGroup> bagHides = new();
        readonly List<(GameObject root, RawImage icon, GameObject badge, TextMeshProUGUI seconds)> effectChips = new();
        readonly List<BookCard> bookCards = new();
        readonly List<char> spare = new();
        readonly Dictionary<string, Texture2D> glyphs = new(StringComparer.Ordinal);
        RectTransform bagColumn, bagLetters, bagHandsRow, bagEffectsRow, bagMapFrame, bagMapShadow, bagMapArea, bagBook, bagBackdrop, pauseShade;
        RectTransform spellCap, bagCap, roomBacking;
        TextMeshProUGUI bookLine;
        string bookWord;
        bool bookFree;
        GridLayoutGroup recipeGrid;
        CanvasGroup bagFade;
        Sprite hairRing, circleRing, softSprite, lineBox;
        float bagOpenedAt;
        bool bagHidden;
        bool touchChosen;
        string tipWanted = "";
        float tipSince;
        ControlTips.Tip? tipShown, tipTextFor;
        InputBinding tipTextBinding;
        int tipTextVersion = -1;
        string matchShown, layoutShown, objectiveShown, roomShown = "\u0000";
        int roundShown, targetShown, aliveShown = int.MinValue, effectsShown = int.MinValue, keyVersion = -1;
        InputBinding keyBinding;
        string spellLabel = "", spellHintLabel = "", bagLabel = "", interactLabel = "";
        bool hubScene;
        Sprite tileRing;
        HouseLayout practice;
        PlayerController followed;
        bool pointerFreed;
        string checklistText = "", checklistSource;
        float objectiveHeight = ObjectiveMinHeight;
        readonly Dictionary<PlayerController, bool> priorFrozen = new();
        bool simulationPaused;

        sealed class LetterCell
        {
            public Image face, edge, ring, outline, focus;
            public TextMeshProUGUI letter;
        }
        sealed class HandView
        {
            public Image face, wear;
            public Image edge, focus;
            public RawImage icon, empty;
            public TextMeshProUGUI label, key;
            public GameObject wearTrack;
        }
        sealed class BookCard
        {
            public string word;
            public Button button;
            public Image face, edge, glow;
            public Image[] tiles, rims;
            public TextMeshProUGUI[] letters;
        }

        static Color Hex(int rgb, float a = 1f) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

        void Awake()
        {
            StateController = GetComponent<UIStateController>();
            if (!StateController) StateController = gameObject.AddComponent<UIStateController>();
            StateController.StateChanged += OnUiStateChanged;
            StateController.TransitionCompleted += OnUiTransitionCompleted;
        }

        void Start()
        {
            UiCanvas = GetComponentInParent<Canvas>();
            if (!UiCanvas) UiCanvas = gameObject.AddComponent<Canvas>();
            UiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (!UiCanvas.TryGetComponent<GraphicRaycaster>(out _)) UiCanvas.gameObject.AddComponent<GraphicRaycaster>();
            if (!UiCanvas.TryGetComponent<CanvasScaler>(out var scaler)) scaler = UiCanvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            if (!EventSystem.current)
            {
                var events = new GameObject("UI Events", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            joins = FindAnyObjectByType<PlayerJoinManager>();
            typewriter = FindAnyObjectByType<Typewriter>();
            rooms = FindAnyObjectByType<RoomBuilder>();
            clearOut = FindAnyObjectByType<ClearOutController>();
            practice = rooms || clearOut ? null : PracticeLayout();
            rounds = FindAnyObjectByType<RoundManager>();
            NoteScene(default, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += NoteScene;
            iconAtlas = Resources.Load<Texture2D>("UI/ActionIcons");
            roundSprite = MakeShape(true);
            panelSprite = MakeShape(false);
            ringSprite = MakeShape(false, 3f);
            hairRing = MakeShape(false, 1.6f);
            circleRing = MakeShape(true, 2f);
            softSprite = MakeSoft();
            lineBox = MakeLineBox();
            safe = Rect("Safe HUD", UiCanvas.transform, Vector2.zero, Vector2.zero, Vector2.zero);
            safe.anchorMax = Vector2.one;
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            StateController.Register(UIState.GameplayHUD, safe.gameObject.AddComponent<CanvasGroup>());
            StyleLegacyText();
            BuildTop(); BuildSide(); BuildStatus(); BuildVitals(); BuildEffects(); BuildTray(); BuildHint();
            BuildComposer(); BuildTouchControls(); BuildNavigation(); BuildBagPanel(); BuildPause(); BuildCrosshair(); BuildCards();
            BuildScoreboard();
            foreach (var part in new[] { side, tray, vitals, effects.rectTransform, hint, status, bar, (RectTransform)craftRoot.transform, (RectTransform)touchRoot.transform, (RectTransform)typewriterControls.transform })
                bagHides.Add(part.gameObject.AddComponent<CanvasGroup>());
            ShowTouchControls(Application.isMobilePlatform);
            ApplySafeArea();
            if (joins) joins.Joined += OnPlayerJoined;
            OnUiStateChanged(StateController.CurrentState);
        }

        void NoteScene(UnityEngine.SceneManagement.Scene _, UnityEngine.SceneManagement.Scene next) => hubScene = next.name == Session.HubScene;

        void OnPlayerJoined(PlayerController player)
        {
            if (StateController.CurrentState != UIState.GameplayHUD) FreezePlayer(player);
        }

        void FreezePlayer(PlayerController player)
        {
            if (!player) return;
            if (!priorFrozen.ContainsKey(player)) priorFrozen[player] = player.Frozen;
            player.Frozen = true;
        }

        void OnUiStateChanged(UIState state)
        {
            if (state == UIState.GameplayHUD)
            {
                ClosePauseSettings();
                RestoreSimulation();
                ClearFocus();
            }
            else
            {
                if (state != UIState.PauseMenu) ClosePauseSettings();
                if (crosshair) crosshair.SetActive(false);
                foreach (var player in World.Players) FreezePlayer(player);
                TouchBinding.Shared.ReleaseAll();
                DesktopBinding.Typing = false;
                if (state == UIState.PauseMenu)
                {
                    ControlTips.Learn(ControlTips.Tip.Menu);
                    if (!simulationPaused) resumeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                    simulationPaused = true;
                    Time.timeScale = 0f;
                    bagPinned = false;
                    if (pauseRoot) pauseRoot.transform.SetAsLastSibling();
                    OnPauseShown();
                }
                else if (simulationPaused)
                {
                    Time.timeScale = resumeScale;
                    simulationPaused = false;
                }
            }
            if (safe) RefreshBagPanel();
            RefreshModeActionVisibility();
        }

        void OnUiTransitionCompleted(UIState state)
        {
            RefreshModeActionVisibility();
            if (state == UIState.PauseMenu) FocusOn(HelpShown ? helpDone : resumeButton);
        }

        void RestoreSimulation()
        {
            if (simulationPaused) Time.timeScale = resumeScale;
            simulationPaused = false;
            foreach (var entry in priorFrozen) if (entry.Key) entry.Key.Frozen = entry.Value;
            priorFrozen.Clear();
        }

        void StyleLegacyText()
        {
            if (scoreboard) scoreboard.gameObject.SetActive(false);
            if (title) title.gameObject.SetActive(false);
            if (subtitle) subtitle.gameObject.SetActive(false);
            if (checklist) checklist.gameObject.SetActive(false);
            if (checklistPanel) checklistPanel.SetActive(false);
        }

        void BuildTop()
        {
            var brand = brandRect = Panel("Brand", safe, new Vector2(0f, 1f), new Vector2(BrandX, -30f), new Vector2(58f, 58f), new Vector2(0f, 1f), Hex(0xf2d295));
            brand.localRotation = Quaternion.Euler(0f, 0f, 5f);
            brand.GetComponent<Image>().raycastTarget = true;
            brand.gameObject.AddComponent<Button>().onClick.AddListener(TogglePause);
            Text("W", brand, new Vector2(.5f, .5f), new Vector2(-3f, 0f), new Vector2(50f, 50f), 34f, Ink).text = "W<size=55%><color=#B86647>!</color></size>";
            var label = labelRect = Rect("Match label", safe, new Vector2(0f, 1f), new Vector2(LabelX, -LabelY), new Vector2(LabelWidth, LabelHeight));
            label.pivot = new Vector2(0f, 1f);
            matchTag = Text("Tag", label, new Vector2(0f, 1f), Vector2.zero, new Vector2(460f, 22f), 17f, Gold, TextAlignmentOptions.Left);
            matchTag.characterSpacing = 1f; matchTag.rectTransform.pivot = new Vector2(0f, 1f);
            matchTitle = Text("Title", label, new Vector2(0f, 1f), new Vector2(0f, -22f), new Vector2(460f, 40f), 34f, OnGame, TextAlignmentOptions.Left);
            matchTitle.rectTransform.pivot = new Vector2(0f, 1f);
            matchDetail = Text("Detail", label, new Vector2(0f, 1f), new Vector2(0f, -66f), new Vector2(460f, 28f), 20f, OnGame, TextAlignmentOptions.Left);
            matchDetail.characterSpacing = 0f; matchDetail.rectTransform.pivot = new Vector2(0f, 1f);
            matchDetail.overflowMode = TextOverflowModes.Ellipsis;
            foreach (var text in new[] { matchTag, matchTitle, matchDetail }) { OnGameText(text); AddHudShadow(text); }
            BuildBar();
        }

        void BuildSide()
        {
            side = Rect("Side column", safe, Vector2.one, new Vector2(-38f, -30f), new Vector2(252f, 560f));
            side.pivot = Vector2.one;
            var mapFrame = minimapRect = Panel("Minimap", side, new Vector2(0f, 1f), Vector2.zero, new Vector2(252f, 252f), new Vector2(0f, 1f), MapEdge);
            miniMap = MakeMap(mapFrame, false, 7f);
            BuildLookEye(side);
            var alive = Rect("Alive", side, new Vector2(0f, 1f), new Vector2(0f, -264f), new Vector2(110f, 54f));
            alive.pivot = new Vector2(0f, 1f);
            AddHudShadow(Panel("Head", alive, new Vector2(0f, .5f), new Vector2(15f, 8f), new Vector2(12f, 12f), new Vector2(.5f, .5f), Cream, true).GetComponent<Image>());
            AddHudShadow(Panel("Shoulders", alive, new Vector2(0f, .5f), new Vector2(15f, -8f), new Vector2(22f, 14f), new Vector2(.5f, .5f), Cream, true).GetComponent<Image>());
            AddHudShadow(Panel("Head behind", alive, new Vector2(0f, .5f), new Vector2(29f, 10f), new Vector2(10f, 10f), new Vector2(.5f, .5f), Cream, true).GetComponent<Image>());
            aliveText = Text("Count", alive, new Vector2(1f, .5f), Vector2.zero, new Vector2(72f, 42f), 28f, OnGame, TextAlignmentOptions.Right);
            aliveText.rectTransform.pivot = new Vector2(1f, .5f);
            OnGameText(aliveText); AddHudShadow(aliveText);
            var timerPill = Rect("Timer", side, new Vector2(0f, 1f), new Vector2(122f, -264f), new Vector2(130f, 54f));
            timerPill.pivot = new Vector2(0f, 1f);
            if (timer)
            {
                timer.rectTransform.SetParent(timerPill, false);
                timer.rectTransform.anchorMin = Vector2.zero; timer.rectTransform.anchorMax = Vector2.one;
                timer.rectTransform.pivot = new Vector2(.5f, .5f);
                timer.rectTransform.offsetMin = timer.rectTransform.offsetMax = Vector2.zero;
                timer.font = LobbyFonts.Body ? LobbyFonts.Body : GameFont;
                timer.fontSharedMaterial = timer.font.material;
                _ = timer.fontMaterial;
                timer.fontStyle = FontStyles.Normal; timer.fontSize = 32f; timer.color = OnGame; timer.alignment = TextAlignmentOptions.Right;
                timer.characterSpacing = 0f; timer.raycastTarget = false; timer.textWrappingMode = TextWrappingModes.NoWrap;
                OnGameText(timer); AddHudShadow(timer);
            }
            timerPill.gameObject.SetActive(false);
            var room = Rect("Room", side, new Vector2(0f, 1f), new Vector2(0f, -324f), new Vector2(252f, 64f));
            room.pivot = new Vector2(0f, 1f);
            roomBacking = Panel("Backing", room, new Vector2(1f, .5f), Vector2.zero, new Vector2(120f, 42f), new Vector2(1f, .5f), KeyPill);
            roomPill = Text("Room name", room, new Vector2(1f, .5f), new Vector2(-16f, 0f), new Vector2(220f, 42f), 23f, OnGame, TextAlignmentOptions.Right);
            roomPill.rectTransform.pivot = new Vector2(1f, .5f);
            roomPill.overflowMode = TextOverflowModes.Ellipsis;
            OnGameText(roomPill); AddHudShadow(roomPill);
            var goal = Rect("Objective", side, new Vector2(0f, 1f), new Vector2(0f, -404f), new Vector2(252f, 120f));
            goal.pivot = new Vector2(0f, 1f);
            objective = Text("Objective text", goal, new Vector2(0f, 1f), Vector2.zero, new Vector2(252f, 120f), 20f, OnGame, TextAlignmentOptions.TopLeft);
            objective.rectTransform.pivot = new Vector2(0f, 1f); objective.textWrappingMode = TextWrappingModes.Normal;
            objective.lineSpacing = 4f;
            OnGameText(objective); AddHudShadow(objective);
        }

        void BuildStatus()
        {
            status = Rect("Status", safe, new Vector2(.5f, 1f), new Vector2(0f, StatusY), new Vector2(525f, 50f));
            status.pivot = new Vector2(.5f, 1f);
            statusText = Text("Status text", status, new Vector2(.5f, .5f), Vector2.zero, new Vector2(475f, 54f), 22f, OnGame);
            statusText.textWrappingMode = TextWrappingModes.Normal;
            OnGameText(statusText); AddHudShadow(statusText);
            if (instruction) instruction.gameObject.SetActive(false);
            status.gameObject.SetActive(false);
        }

        void BuildVitals()
        {
            vitals = Rect("Vitals", safe, Vector2.zero, new Vector2(VitalsX, VitalsY), new Vector2(VitalsBarX + PlayerBar.SoloWidth, PlayerBar.SoloTall));
            vitals.pivot = Vector2.zero;
            var cross = Rect("HP cross", vitals, new Vector2(0f, 1f), new Vector2(VitalsCrossX, -PlayerBar.SoloHeight * .5f), new Vector2(VitalsCross, VitalsCross));
            foreach (var (bar, size) in new[] { ("Across", new Vector2(VitalsCross, VitalsCross / 3f)), ("Down", new Vector2(VitalsCross / 3f, VitalsCross)) })
            {
                var piece = CreateImage(bar, cross, new Vector2(.5f, .5f), Vector2.zero, size, OnGame);
                var shadow = piece.gameObject.AddComponent<Shadow>(); shadow.effectColor = Shade; shadow.effectDistance = new Vector2(0f, -2f);
            }
            soloVitals = MakeVitals(vitals, new Vector2(0f, 1f), PlayerBar.SoloNumberSize);
            LayVitals(soloVitals, VitalsBarX, 0f, PlayerBar.SoloWidth, PlayerBar.SoloHeight,
                -(PlayerBar.SoloHeight + PlayerBar.SoloStaminaGap), PlayerBar.SoloWidth, PlayerBar.SoloStaminaHeight, PlayerBar.SoloPad);
        }

        void BuildEffects()
        {
            effects = Text("Effects", safe, Vector2.zero, Vector2.zero, new Vector2(EffectsWidth, EffectsHeight), EffectsSize, Breath, TextAlignmentOptions.BottomLeft);
            effects.rectTransform.pivot = Vector2.zero;
            OnGameText(effects);
            effects.text = "";
            effects.gameObject.SetActive(false);
        }

        void BuildTray()
        {
            tray = Rect("Letter bag", safe, new Vector2(.5f, 0f), new Vector2(0f, 52f), new Vector2(500f, 176f));
            tray.pivot = new Vector2(.5f, 0f);
            for (int i = 0; i < 2; i++)
            {
                int slot = i;
                hands[i] = MakeHand($"Gear slot {i + 1}", tray, new Vector2(324f, 94f - i * 94f), new Vector2(176f, 82f), i, () => SelectHand(slot));
            }
            TextMeshProUGUI Shortcut(string label, float x, Action action, out RectTransform cap)
            {
                var button = MakeButton(label, tray, Vector2.zero, new Vector2(x, 0f), new Vector2(120f, 40f), "", action, Color.clear);
                cap = Panel("Key cap", button.transform, new Vector2(0f, .5f), new Vector2(36f, 0f), new Vector2(40f, 32f), new Vector2(0f, .5f), KeyPill);
                cap.SetSiblingIndex(0);
                var key = button.GetComponentInChildren<TextMeshProUGUI>();
                key.rectTransform.anchorMin = key.rectTransform.anchorMax = key.rectTransform.pivot = new Vector2(0f, .5f);
                key.rectTransform.anchoredPosition = new Vector2(36f, 0f);
                key.rectTransform.sizeDelta = new Vector2(40f, 32f);
                key.fontSize = 20f; key.color = OnGame;
                key.enableAutoSizing = true; key.fontSizeMin = 14f; key.fontSizeMax = 20f;
                return key;
            }
            spellKey = Shortcut("Spell key", 20f, OpenComposer, out spellCap);
            SpellGlyph(spellKey.transform.parent);
            bagKey = Shortcut("Bag link", 150f, () => SetBagPinned(!bagPinned), out bagCap);
            AddHudShadow(GlyphImage("Glyph", bagKey.transform.parent, new Vector2(0f, .5f), new Vector2(17f, 0f), 25f, "book", OnGame));
            spellHint = Text("Spell hint", tray, new Vector2(0f, 1f), new Vector2(0f, 8f), new Vector2(310f, 30f), 23f, OnGame, TextAlignmentOptions.BottomLeft);
            spellHint.rectTransform.pivot = Vector2.zero;
            spellHint.overflowMode = TextOverflowModes.Ellipsis;
            OnGameText(spellHint); AddHudShadow(spellHint);
            spellHint.text = "";
            spellHint.gameObject.SetActive(false);
            for (int i = 0; i < TrayTiles; i++)
            {
                var cell = trayCells[i] = MakeCell($"Letter {i + 1}", tray, new Vector2((i % TilesPerRow) * 62f, -(i / TilesPerRow) * 70f), new Vector2(54f, 58f), 34f);
                cell.outline = CreateImage("Outline", cell.face.transform, Vector2.zero, Vector2.zero, Vector2.zero, CellRim);
                Stretch(cell.outline.rectTransform); cell.outline.sprite = hairRing; cell.outline.type = Image.Type.Sliced;
            }
            craftProgress = Fill("Building", Panel("Progress track", tray, Vector2.zero, new Vector2(0f, -12f), new Vector2(302f, 6f), Vector2.zero, Track), Health);
        }

        void BuildHint()
        {
            hint = Rect("Desktop controls", safe, new Vector2(.5f, 1f), new Vector2(0f, StatusY), new Vector2(720f, 40f));
            hint.pivot = new Vector2(.5f, 1f);
            hintText = Text("Keys", hint, new Vector2(.5f, .5f), Vector2.zero, new Vector2(720f, 40f), 22f, OnGame);
            OnGameText(hintText); AddHudShadow(hintText);
            hintText.text = ""; hintText.alpha = 0f;
            desktopHints = hint.gameObject;
            desktopHints.SetActive(false);
        }

        InputBinding TipBinding => LocalPlayer && LocalPlayer.Binding is GamepadBinding or KeyboardBinding ? LocalPlayer.Binding : DesktopBinding.Shared;

        void LearnTips(bool thirdPerson)
        {
            if (!LocalPlayer || TouchControlsShown || !AcceptsGameplayInput) return;
            if (LocalPlayer.Binding is DesktopBinding or TouchBinding)
            {
                var keys = DesktopBinding.Shared;
                if (keys.Move.ReadValue<Vector2>().sqrMagnitude > .25f) ControlTips.Learn(ControlTips.Tip.Move);
                if (thirdPerson && keys.Look.ReadValue<Vector2>().sqrMagnitude > 25f) ControlTips.Learn(ControlTips.Tip.Look);
                if (keys.Attack.WasPressedThisFrame()) ControlTips.Learn(ControlTips.Tip.Smash);
                if (LocalPlayer.Combat && LocalPlayer.Combat.IsAiming) ControlTips.Learn(ControlTips.Tip.Aim);
                if (keys.SpellBox.WasPressedThisFrame()) ControlTips.Learn(ControlTips.Tip.Spell);
                if (keys.Interact.WasPressedThisFrame()) ControlTips.Learn(ControlTips.Tip.Interact);
                if (BagOpen) ControlTips.Learn(ControlTips.Tip.Bag);
                return;
            }
            ControlTips.LearnFrom(LocalPlayer.Commands, thirdPerson, BagOpen);
        }

        void RefreshTips(bool thirdPerson)
        {
            bool quiet = !LocalPlayer || TouchControlsShown || NeedsPointer || BannerShown
                || status.gameObject.activeSelf || composer && composer.gameObject.activeSelf;
            var binding = TipBinding;
            var next = quiet ? null : ControlTips.Next(thirdPerson, binding);
            if (next != tipTextFor || binding != tipTextBinding || KeyBindings.Version != tipTextVersion)
            {
                tipTextFor = next; tipTextBinding = binding; tipTextVersion = KeyBindings.Version;
                tipWanted = next.HasValue ? ControlTips.Text(next.Value, binding) : "";
            }
            tipShown = next;
            ShowTipLine();
        }

        void FadeTip()
        {
            float step = Time.unscaledDeltaTime;
            if (tipWanted != hintText.text)
            {
                hintText.alpha = Mathf.MoveTowards(hintText.alpha, 0f, step / .25f);
                if (hintText.alpha <= 0f) { hintText.text = tipWanted; tipSince = Time.unscaledTime; }
            }
            else if (tipWanted.Length > 0 && Time.unscaledTime - tipSince > .4f) hintText.alpha = Mathf.MoveTowards(hintText.alpha, 1f, step / .35f);
            if (tipShown.HasValue && hintText.alpha > 0f && hintText.text == tipWanted) ControlTips.Showing(tipShown.Value, step);
            ShowTipLine();
        }

        void ShowTipLine()
        {
            bool show = !TouchControlsShown && (tipWanted.Length > 0 || hintText.alpha > 0f);
            if (desktopHints.activeSelf != show) desktopHints.SetActive(show);
        }

        static void OnGameText(TMP_Text t)
        {
            t.outlineWidth = .14f;
            t.outlineColor = new Color32(0x0b, 0x1f, 0x1f, 0xb0);
        }

        static void AddHudShadow(Graphic graphic)
        {
            var shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = Hex(0x0b1f1f, .55f);
            shadow.effectDistance = new Vector2(0f, -2f);
        }

        void BuildCrosshair()
        {
            var root = Rect("Crosshair", UiCanvas.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(22f, 22f));
            root.SetAsFirstSibling();
            void Tick(string label, Vector2 at, Vector2 size)
            {
                var tick = CreateImage(label, root, Vector2.zero, at, size, Hex(0xfff8e8));
                tick.rectTransform.pivot = Vector2.zero;
                crosshairTicks.Add(tick);
                var outline = tick.gameObject.AddComponent<Outline>();
                outline.effectColor = Hex(0x10292b, .8f);
                outline.effectDistance = new Vector2(1f, -1f);
                var shadow = tick.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, .56f);
                shadow.effectDistance = new Vector2(0f, -1.5f);
            }
            Tick("Top", new Vector2(10f, 16f), new Vector2(2f, 6f));
            Tick("Bottom", new Vector2(10f, 0f), new Vector2(2f, 6f));
            Tick("Left", new Vector2(0f, 10f), new Vector2(6f, 2f));
            Tick("Right", new Vector2(16f, 10f), new Vector2(6f, 2f));
            interactPrompt = Text("Interaction key", root, new Vector2(.5f, 0f), new Vector2(0f, -22f), new Vector2(260f, 30f), 22f, Hex(0x9ff8d3), TextAlignmentOptions.Center);
            OnGameText(interactPrompt);
            var meter = CreateImage("Draw meter", root, new Vector2(.5f, 1f), new Vector2(0f, 12f), new Vector2(46f, 6f), Hex(0x10292b, .6f)).rectTransform;
            meter.pivot = new Vector2(.5f, 0f);
            drawFill = CreateImage("Drawn", meter, Vector2.zero, Vector2.zero, Vector2.zero, Hex(0x9ff8d3)).rectTransform;
            drawFill.anchorMin = Vector2.zero; drawFill.anchorMax = new Vector2(0f, 1f); drawFill.pivot = Vector2.zero;
            drawFill.offsetMin = new Vector2(1f, 1f); drawFill.offsetMax = new Vector2(-1f, -1f);
            drawMeter = meter.gameObject;
            drawMeter.SetActive(false);
            crosshair = root.gameObject;
            crosshair.SetActive(false);
        }

        void BuildTouchControls()
        {
            var rt = Rect("Touch controls", safe, Vector2.zero, Vector2.zero, Vector2.zero);
            rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; touchRoot = rt.gameObject;
            AddStick(rt, false, new Vector2(135f, 128f), Vector2.zero, 190f);
            AddStick(rt, true, new Vector2(-128f, 142f), new Vector2(1f, 0f), 152f);
            AddSkill(rt, TouchAction.Attack, "SMASH", new Vector2(-280f, 167f), 112f, Coral);
            AddSkill(rt, TouchAction.Dodge, "DODGE", new Vector2(-250f, 297f), 89f, Mint);
            AddSkill(rt, TouchAction.Jump, "JUMP", new Vector2(-123f, 294f), 89f, Cream);
            AddSkill(rt, TouchAction.Block, "BLOCK", new Vector2(-384f, 270f), 89f, Cream);
            AddSkill(rt, TouchAction.Grab, "INTERACT", new Vector2(-384f, 161f), 89f, Cream);
            AddSkill(rt, TouchAction.Craft, "SPELL", new Vector2(-503f, 270f), 89f, Mint);
            AddSkill(rt, TouchAction.Drop, "HOLD DROP", new Vector2(-384f, 51f), 76f, Cream);
            playRect = (RectTransform)MakeButton("Play or Ready", rt, new Vector2(.5f, 1f), new Vector2(0f, PlayY), new Vector2(200f, 50f), "PLAY", JoinOrReady).transform;
            TouchLayoutView.Attach(rt, this);
        }

        void AddStick(Transform parent, bool aim, Vector2 position, Vector2 anchor, float diameter)
        {
            var rt = Panel(aim ? "Aim stick" : "Move stick", parent, anchor, position, new Vector2(diameter, diameter), new Vector2(.5f, .5f), Hex(0x153f3c, .45f), true);
            rt.GetComponent<Image>().raycastTarget = true;
            var ring = Panel("Ring", rt, new Vector2(.5f, .5f), Vector2.zero, new Vector2(diameter - 12f, diameter - 12f), new Vector2(.5f, .5f), Hex(0xffe7bc, .2f), true);
            var knob = Panel("Thumb", rt, new Vector2(.5f, .5f), Vector2.zero, new Vector2(diameter * .36f, diameter * .36f), new Vector2(.5f, .5f), Hex(0xffe6b5, .8f), true);
            Text("Hint", ring, new Vector2(.5f, .5f), Vector2.zero, new Vector2(diameter, 30f), 19f, Cream).text = aim ? "AIM" : "MOVE";
            var stick = rt.gameObject.AddComponent<TouchStick>(); stick.knob = knob; stick.aims = aim; stick.radius = diameter * .28f;
            stick.Initialise(Camera.main); sticks.Add(stick);
        }

        void BuildNavigation()
        {
            var panel = Panel("Typewriter touch menu", safe, new Vector2(.5f, 0f), new Vector2(0f, 262f), new Vector2(580f, 166f), new Vector2(.5f, 0f), Paper);
            typewriterControls = panel.gameObject;
            typewriterChoice = Text("Mode choice", panel, new Vector2(.5f, 1f), new Vector2(0f, -35f), new Vector2(540f, 54f), 25f, Ink);
            typewriterChoice.textWrappingMode = TextWrappingModes.Normal;
            MakeButton("Previous mode", panel, Vector2.zero, new Vector2(18f, 22f), new Vector2(132f, 52f), "PREV", () => TouchBinding.Shared.Pulse(TouchAction.Up));
            MakeButton("Next mode", panel, Vector2.zero, new Vector2(162f, 22f), new Vector2(132f, 52f), "NEXT", () => TouchBinding.Shared.Pulse(TouchAction.Down));
            MakeButton("Choose mode", panel, Vector2.zero, new Vector2(306f, 22f), new Vector2(132f, 52f), "SELECT", () => TouchBinding.Shared.Pulse(TouchAction.Grab));
            MakeButton("Leave typewriter", panel, Vector2.zero, new Vector2(450f, 22f), new Vector2(112f, 52f), "BACK", () => { if (typewriter) typewriter.Close(); });
            typewriterControls.SetActive(false);
        }

        void BuildBagPanel()
        {
            var rt = Rect("Bag panel", safe, Vector2.zero, Vector2.zero, Vector2.zero);
            rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            bagPanel = rt.gameObject;
            bagFade = bagPanel.AddComponent<CanvasGroup>();
            var scrim = CreateImage("Backdrop", rt, Vector2.zero, Vector2.zero, Vector2.zero, Color.white);
            bagBackdrop = scrim.rectTransform; bagBackdrop.anchorMax = Vector2.one; bagBackdrop.offsetMin = bagBackdrop.offsetMax = Vector2.zero;
            scrim.sprite = MakeScrim(); scrim.raycastTarget = true;

            bagColumn = Rect("Bag", rt, new Vector2(0f, .5f), new Vector2(BagSide, (BagBottom - BagTop) * .5f), new Vector2(BagColumn, 400f));
            bagColumn.pivot = new Vector2(0f, .5f);
            RectTransform Row(string label) { var row = Rect(label, bagColumn, new Vector2(0f, 1f), Vector2.zero, new Vector2(BagColumn, 0f)); row.pivot = new Vector2(0f, 1f); return row; }
            bagLetters = Row("Letters");
            GlyphImage("Bag glyph", bagLetters, new Vector2(0f, 1f), new Vector2(0f, -4f), 28f, "bag", BagInk);
            for (int i = 0; i < TrayTiles; i++)
            {
                var cell = bagCells[i] = MakeCell($"Big letter {i + 1}", bagLetters, new Vector2(40f + (i % TilesPerRow) * 62.5f, -(i / TilesPerRow) * 67.5f), new Vector2(55f, 60f), 33f);
                cell.face.pixelsPerUnitMultiplier = cell.edge.pixelsPerUnitMultiplier = 11f / 6.25f;
                cell.ring = CreateImage("Ring", cell.face.transform, Vector2.zero, Vector2.zero, Vector2.zero, BagCellEdge);
                Stretch(cell.ring.rectTransform); cell.ring.sprite = hairRing; cell.ring.type = Image.Type.Sliced;
                cell.ring.pixelsPerUnitMultiplier = cell.face.pixelsPerUnitMultiplier;
            }
            bagPanelCount = Text("Count", bagLetters, Vector2.one, new Vector2(0f, -133.5f), new Vector2(120f, 24f), 20f, BagInk, TextAlignmentOptions.Right);
            bagPanelCount.rectTransform.pivot = Vector2.one;
            bagHandsRow = Row("Hands");
            for (int i = 0; i < 2; i++)
            {
                int slot = i;
                bagHands[i] = MakeBagHand($"Bag hand {i + 1}", bagHandsRow, new Vector2(i * 193.5f, 0f), i, () => { if (!BagDragging) SelectHand(slot); });
            }
            bagEffectsRow = Row("Effects");
            foreach (var effect in new[] { "Effect 1", "Effect 2", "Effect 3", "Effect 4" })
            {
                var chip = MakeChip(effect, bagEffectsRow);
                var icon = GlyphImage("Glyph", chip, new Vector2(.5f, .5f), Vector2.zero, 28f, "bubble", BagInk);
                var badge = Panel("Seconds", chip, new Vector2(1f, 0f), new Vector2(6f, -6f), new Vector2(32f, 26f), new Vector2(1f, 0f), Gold);
                badge.GetComponent<Image>().pixelsPerUnitMultiplier = 11f / 10f;
                var seconds = Text("Count", badge, new Vector2(.5f, .5f), Vector2.zero, new Vector2(32f, 26f), 18f, Hex(0x17393a));
                effectChips.Add((chip.gameObject, icon, badge.gameObject, seconds));
            }

            var house = Rect("House", rt, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(house);
            var area = bagMapArea = Rect("Map area", house, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(area);
            var shadow = CreateImage("Map shadow", area, new Vector2(.5f, .5f), new Vector2(0f, -30f), Vector2.one * 100f, Hex(0x000000, .53f));
            shadow.sprite = softSprite; shadow.type = Image.Type.Sliced; shadow.pixelsPerUnitMultiplier = 28f / 75f;
            bagMapShadow = shadow.rectTransform;
            bagMapFrame = Panel("Big map", area, new Vector2(.5f, .5f), Vector2.zero, Vector2.one * 100f, new Vector2(.5f, .5f), MapEdge);
            bagMapFrame.GetComponent<Image>().pixelsPerUnitMultiplier = 11f / 22f;
            bigMap = MakeMap(bagMapFrame, true, 9f);

            var book = bagBook = Rect("Recipe book", rt, Vector2.one, Vector2.zero, Vector2.zero);
            book.anchorMin = new Vector2(1f, 0f); book.pivot = Vector2.one;
            book.offsetMin = new Vector2(-BagSide - BookColumn, BagBottom); book.offsetMax = new Vector2(-BagSide, -BagTop);
            GlyphImage("Book glyph", book, new Vector2(0f, 1f), new Vector2(4f, -2f), 34f, "book", BagInk);
            bookLine = Text("Book line", book, new Vector2(0f, 1f), Vector2.zero, Vector2.zero, 23f, BagInk, TextAlignmentOptions.MidlineLeft);
            bookLine.rectTransform.anchorMax = Vector2.one; bookLine.rectTransform.pivot = new Vector2(0f, 1f);
            bookLine.rectTransform.offsetMin = new Vector2(BookTextLeft, -BookTitleTop - BookTitleHeight);
            bookLine.rectTransform.offsetMax = new Vector2(-BookCloseInset - BookCloseSize - BookCloseGap, -BookTitleTop);
            bookLine.overflowMode = TextOverflowModes.Ellipsis; bookLine.text = "";
            BuildBookKey(book);
            var view = Rect("View", book, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(view); view.offsetMax = new Vector2(0f, -BookCardsTop);
            view.gameObject.AddComponent<RectMask2D>();
            view.gameObject.AddComponent<Image>().color = Color.clear;
            var cardsRoot = Rect("Cards", view, new Vector2(.5f, 1f), Vector2.zero, Vector2.zero);
            cardsRoot.anchorMin = new Vector2(0f, 1f); cardsRoot.anchorMax = Vector2.one; cardsRoot.pivot = new Vector2(.5f, 1f); cardsRoot.offsetMin = cardsRoot.offsetMax = Vector2.zero;
            var grid = recipeGrid = cardsRoot.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(199f, BookCardHeight); grid.spacing = new Vector2(12f, 12f); grid.padding = new RectOffset(4, 4, 4, 4);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 4;
            cardsRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view; scroll.content = cardsRoot; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 60f;
            foreach (var item in GameConfig.Current.Items.All)
                if (item.Enabled) bookCards.Add(MakeBookCard(item.Id, cardsRoot));

            var close = Panel("Close bag", rt, Vector2.one, new Vector2(-BagSide - BookCloseInset, -BagTop - BookTitleTop),
                Vector2.one * BookCloseSize, Vector2.one, Hex(0xffffff, .12f), true);
            close.GetComponent<Image>().raycastTarget = true;
            close.gameObject.AddComponent<Button>().onClick.AddListener(() => SetBagPinned(false));
            var edge = CreateImage("Edge", close, Vector2.zero, Vector2.zero, Vector2.zero, Hex(0xffffff, .25f));
            Stretch(edge.rectTransform); edge.sprite = circleRing;
            Text("Label", close, new Vector2(.5f, .5f), new Vector2(0f, 2f), new Vector2(40f, 40f), 30f, BagInk).text = "×";
            bagClose = close;
            BuildBagDrag(rt);
            bagPanel.SetActive(false);
        }

        void BuildBookKey(RectTransform book)
        {
            var key = Rect("Legend", book, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            key.anchorMax = Vector2.one; key.pivot = new Vector2(0f, 1f);
            key.offsetMin = new Vector2(0f, -BookKeyTop - BookKeyHeight); key.offsetMax = new Vector2(0f, -BookKeyTop);
            var text = Text("Text", key, Vector2.zero, Vector2.zero, Vector2.zero, BookKeyFont, Hex(0xfff4e2, .8f), TextAlignmentOptions.MidlineLeft);
            Stretch(text.rectTransform); text.rectTransform.offsetMin = new Vector2(BookTextLeft, 0f);
            text.enableAutoSizing = true; text.fontSizeMin = BookKeyMinFont; text.fontSizeMax = BookKeyFont;
            text.text = RecipeStars.Legend;
            var star = CreateImage("Star", key, new Vector2(0f, .5f), new Vector2(BookGlyphCentre, 0f), Vector2.one * BookKeyStar, TierColour(RecipeTier.Epic));
            star.sprite = SparkSprite(); star.preserveAspect = true;
        }

        static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

        RectTransform MakeChip(string label, RectTransform row)
        {
            var chip = Panel(label, row, new Vector2(0f, 1f), Vector2.zero, Vector2.one * Chip, new Vector2(0f, 1f), BagCard);
            Edge(chip, BagCardEdge, 15f);
            return chip;
        }

        Image Edge(RectTransform shape, Color colour, float radius)
        {
            var face = shape.GetComponent<Image>(); face.pixelsPerUnitMultiplier = 11f / radius;
            var edge = CreateImage("Edge", shape, Vector2.zero, Vector2.zero, Vector2.zero, colour);
            Stretch(edge.rectTransform); edge.sprite = hairRing; edge.type = Image.Type.Sliced; edge.pixelsPerUnitMultiplier = face.pixelsPerUnitMultiplier;
            return edge;
        }

        HandView MakeBagHand(string label, RectTransform parent, Vector2 position, int slot, Action onTap)
        {
            var size = new Vector2(181f, 145f);
            var rt = Panel(label, parent, new Vector2(0f, 1f), position, size, new Vector2(0f, 1f), BagCard);
            var view = new HandView { face = rt.GetComponent<Image>() };
            view.face.raycastTarget = true;
            rt.gameObject.AddComponent<Button>().onClick.AddListener(() => onTap());
            view.edge = Edge(rt, BagCardEdge, 17.5f);
            view.edge.sprite = MakeShape(false, 2.6f);
            var key = view.key = Text("Key", rt, new Vector2(0f, 1f), new Vector2(10f, -8f), new Vector2(26f, 26f), 20f, BagInk, TextAlignmentOptions.Left);
            key.text = (slot + 1).ToString(); key.rectTransform.pivot = new Vector2(0f, 1f);
            view.icon = Rect("Icon", rt, new Vector2(.5f, .5f), new Vector2(0f, 10f), new Vector2(78f, 78f)).gameObject.AddComponent<RawImage>();
            view.icon.raycastTarget = false;
            view.empty = GlyphImage("Open hand", rt, new Vector2(.5f, .5f), Vector2.zero, 42f, "hand", Hex(0xfff4e2, .45f));
            view.label = Text("Word", rt, new Vector2(.5f, 0f), new Vector2(0f, 20f), new Vector2(size.x - 16f, 26f), 20f, BagInk);
            view.label.rectTransform.pivot = new Vector2(.5f, 0f);
            var track = Panel("Wear", rt, new Vector2(.5f, 0f), new Vector2(0f, 10f), new Vector2(size.x - 25f, 6f), new Vector2(.5f, 0f), Hex(0xffffff, .16f));
            view.wear = Fill("Left", track, Health); view.wearTrack = track.gameObject;
            return view;
        }

        BookCard MakeBookCard(string word, RectTransform parent)
        {
            var root = new GameObject(word, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            var card = new BookCard { word = word, tiles = new Image[word.Length], letters = new TextMeshProUGUI[word.Length] };
            card.glow = CreateImage("Glow", root, Vector2.zero, Vector2.zero, Vector2.zero, GoldGlow);
            Stretch(card.glow.rectTransform); card.glow.rectTransform.offsetMin = -Vector2.one * 19f; card.glow.rectTransform.offsetMax = Vector2.one * 19f;
            card.glow.sprite = softSprite; card.glow.type = Image.Type.Sliced; card.glow.pixelsPerUnitMultiplier = 28f / 17.5f;
            var face = Panel("Face", root, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(.5f, .5f), BagCard);
            Stretch(face);
            card.face = face.GetComponent<Image>(); card.face.raycastTarget = true;
            card.edge = Edge(face, BagCardEdge, 15f);
            card.button = root.gameObject.AddComponent<Button>();
            card.button.targetGraphic = card.face;
            var colours = card.button.colors; colours.disabledColor = Color.white; card.button.colors = colours;
            card.button.onClick.AddListener(() => SpellFromBook(word));
            root.gameObject.AddComponent<BookCardFocus>().Changed = on => ShowBookLine(on ? word : null);
            var icon = Rect("Picture", root, new Vector2(.5f, 1f), new Vector2(0f, -BookPictureTop), new Vector2(BookPicture, BookPicture)).gameObject.AddComponent<RawImage>();
            ((RectTransform)icon.transform).pivot = new Vector2(.5f, 1f); icon.raycastTarget = false;
            Wreckabulary.UI.ItemArt.Apply(icon, word);
            var tier = RecipeDifficulty.TierOf(word);
            int stars = GameConfig.Current.Tuning != null ? RecipeStars.Count(tier) : 0;
            for (int s = 0; s < stars; s++)
            {
                var star = CreateImage("Star", root, Vector2.one, new Vector2(-16f - s * 19f, -16f), new Vector2(18f, 18f), TierColour(tier));
                star.sprite = SparkSprite(); star.preserveAspect = true;
            }
            if (!tileRing) tileRing = MakeShape(false, 4.4f);
            const float tile = BookTile, step = BookTileStep;
            float x0 = -(word.Length * step - 4f) * .5f;
            card.rims = new Image[word.Length];
            for (int i = 0; i < word.Length; i++)
            {
                var t = Panel($"Tile {i + 1}", root, new Vector2(.5f, 0f), new Vector2(x0 + i * step, BookTileY), new Vector2(tile, 32f), Vector2.zero, Color.clear);
                card.tiles[i] = t.GetComponent<Image>(); card.tiles[i].pixelsPerUnitMultiplier = 11f / 3.75f;
                var rim = card.rims[i] = CreateImage("Rim", t, Vector2.zero, Vector2.zero, Vector2.zero, TileRim);
                Stretch(rim.rectTransform); rim.sprite = tileRing; rim.type = Image.Type.Sliced; rim.pixelsPerUnitMultiplier = card.tiles[i].pixelsPerUnitMultiplier;
                card.letters[i] = Text("Letter", t, new Vector2(.5f, .5f), Vector2.zero, new Vector2(tile, 32f), 23f, TileMissingInk);
                card.letters[i].text = word[i].ToString();
            }
            AddCardFacts(root, word);
            return card;
        }

        void AddCardFacts(RectTransform root, string word)
        {
            var config = GameConfig.Current;
            if (!config.Items.TryGet(word, out var item)) return;
            var chips = RecipeFacts.Chips(item, config.Tuning);
            var sized = new List<(RecipeChip chip, float width)>();
            float total = 0f;
            foreach (var chip in chips)
            {
                float measured = FactProbe().GetPreferredValues(chip.Label, 999f, 22f).x;
                float width = Mathf.Ceil(measured > 1f ? measured : chip.Label.Length * 8.5f) + 16f;
                sized.Add((chip, width)); total += width;
            }
            total += Mathf.Max(0, sized.Count - 1) * 6f;
            float x = -total * .5f;
            foreach (var (chip, width) in sized)
            {
                bool role = chip.Kind == FactKind.Role;
                var pill = Panel("Fact " + chip.Label, root, new Vector2(.5f, 0f), new Vector2(x, BookFactY), new Vector2(width, 22f), Vector2.zero, role ? Hex(0xffffff, .12f) : FactColour(chip.Kind));
                pill.GetComponent<Image>().pixelsPerUnitMultiplier = 11f / 11f;
                Text("Label", pill, new Vector2(.5f, .5f), new Vector2(0f, 1f), new Vector2(width, 22f), BookFactFont, role ? BagInk : Hex(0x17393a)).text = chip.Label;
                x += width + 6f;
            }
            var bars = RecipeFacts.Bars(item, config.Items.All, config.Tuning);
            for (int i = 0; i < bars.Count; i++)
            {
                float left = i == 0 ? -88f : 4f;
                var caption = Text("Bar " + bars[i].Label, root, new Vector2(.5f, 0f), new Vector2(left, BookBarY), new Vector2(BookBarCaption, 18f), BookBarFont, Hex(0xfff4e2, .75f), TextAlignmentOptions.MidlineLeft);
                caption.rectTransform.pivot = Vector2.zero; caption.text = bars[i].Label;
                var track = Panel("Track", root, new Vector2(.5f, 0f), new Vector2(left + BookBarCaption, BookBarY + 6f), new Vector2(BookBarTrack, 6f), Vector2.zero, Hex(0xffffff, .16f));
                track.GetComponent<Image>().pixelsPerUnitMultiplier = 11f / 2.5f;
                var fill = Panel("Fill", track, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, BagInk);
                fill.anchorMax = new Vector2(Mathf.Max(.06f, bars[i].Fill), 1f); fill.offsetMin = fill.offsetMax = Vector2.zero;
                fill.GetComponent<Image>().pixelsPerUnitMultiplier = 11f / 2.5f;
            }
        }

        TextMeshProUGUI factProbe;
        TextMeshProUGUI FactProbe()
        {
            if (factProbe) return factProbe;
            factProbe = Text("Fact probe", safe, Vector2.zero, new Vector2(-9999f, -9999f), new Vector2(10f, 10f), BookFactFont, Color.clear);
            factProbe.gameObject.SetActive(false);
            return factProbe;
        }

        static Color FactColour(FactKind kind) => kind switch
        {
            FactKind.Heavy => Hex(0x8cc8ff), FactKind.Daze => Hex(0xcdb4ff), FactKind.Bleed => Hex(0xff9a8a),
            FactKind.Crush => Hex(0xffc568), FactKind.Guard or FactKind.Swift => Hex(0x9ff8d3), _ => Hex(0xe2dccd),
        };

        static Color TierColour(RecipeTier tier) => tier switch
        {
            RecipeTier.Uncommon => Hex(0x9ff8d3), RecipeTier.Rare => Hex(0x8cc8ff), RecipeTier.Epic => Hex(0xffc568), _ => Hex(0xe2dccd),
        };

        void SpellFromBook(string word)
        {
            if (!LocalPlayer || Paused) return;
            SetBagPinned(false);
            if (TypedMode)
            {
                var summoner = LocalPlayer.Summoner;
                if (!summoner.IsSpelling) { summoner.Open(); if (!summoner.IsSpelling) return; SyncComposer(); }
                TypeWord(word);
                return;
            }
            LocalPlayer.Summoner.BeginCraft(new WordEntry { word = word });
        }

        RawImage GlyphImage(string label, Transform parent, Vector2 anchor, Vector2 position, float size, string glyph, Color colour)
        {
            var image = Rect(label, parent, anchor, position, Vector2.one * size).gameObject.AddComponent<RawImage>();
            if (anchor == new Vector2(0f, 1f)) image.rectTransform.pivot = anchor;
            image.texture = Glyph(glyph); image.color = colour; image.raycastTarget = false;
            return image;
        }

        void SpellGlyph(Transform parent)
        {
            var tile = Panel("Glyph", parent, new Vector2(0f, .5f), new Vector2(17f, 0f), new Vector2(24f, 26f), new Vector2(.5f, .5f), Tile);
            tile.localRotation = Quaternion.Euler(0f, 0f, -6f);
            var face = tile.GetComponent<Image>(); face.pixelsPerUnitMultiplier = 11f / 4f;
            AddHudShadow(face);
            var letter = Text("Letter", tile, new Vector2(.5f, .5f), Vector2.zero, new Vector2(24f, 26f), 19f, Ink);
            letter.text = "A"; letter.fontStyle = FontStyles.Bold;
        }

        Texture2D Glyph(string name)
        {
            if (!glyphs.TryGetValue(name, out var texture)) glyphs[name] = texture = Resources.Load<Texture2D>("UI/Glyphs/" + name);
            return texture;
        }

        void AddSkill(Transform parent, TouchAction action, string label, Vector2 position, float diameter, Color accent)
        {
            var rt = Panel(label, parent, new Vector2(1f, 0f), position, new Vector2(diameter, diameter), new Vector2(.5f, .5f), accent, true);
            rt.GetComponent<Image>().raycastTarget = true;
            var inset = Panel("Face", rt, new Vector2(.5f, .5f), Vector2.zero, new Vector2(diameter - 7f, diameter - 7f), new Vector2(.5f, .5f), Hex(0xfff0dc, .95f), true);
            var icon = CreateImage("Icon", inset, new Vector2(.5f, .5f), new Vector2(0f, 5f), new Vector2(diameter * .77f, diameter * .77f), Color.white);
            icon.sprite = Icon(action); icon.preserveAspect = true; if (!icon.sprite) icon.enabled = false;
            var text = Text("Label", rt, new Vector2(.5f, 0f), new Vector2(0f, -4f), new Vector2(diameter + 34f, 26f), 14f, Cream);
            text.rectTransform.pivot = new Vector2(.5f, 1f); text.text = label; text.outlineWidth = .2f; text.outlineColor = Ink;
            var button = rt.gameObject.AddComponent<TouchActionButton>(); button.action = action;
            skillButtons[action] = (button, text);
            if (action == TouchAction.Craft) { button.sendsInput = false; button.pressed = ToggleCraft; }
        }

        public void ShowTouchControls(bool on)
        {
            if (!touchRoot) return;
            touchRoot.SetActive(on); TouchBinding.Shared.ReleaseAll(); TouchBinding.Shared.Enabled = on;
            TouchBinding.Shared.OverlayDesktop = on && LocalPlayer && LocalPlayer.Binding is DesktopBinding;
            TouchBinding.Shared.OverlayBindingId = on && LocalPlayer && LocalPlayer.Binding is not TouchBinding ? LocalPlayer.Binding?.Id : null;
            if (desktopHints && on) desktopHints.SetActive(false);
            if (touchToggle) touchToggle.text = on ? "Touch controls: on" : "Touch controls: off";
            if (LocalPlayer) LocalPlayer.Summoner.Close();
            ApplySafeArea();
        }
        void JoinOrReady()
        {
            if (!joins) return;
            if (!LocalPlayer) { TouchBinding.Shared.Enabled = true; LocalPlayer = joins.Join(TouchBinding.Shared); TouchBinding.Shared.OverlayDesktop = false; TouchBinding.Shared.OverlayBindingId = null; }
            else TouchBinding.Shared.Pulse(TouchAction.Start);
        }
        void ToggleCraft()
        {
            if (!LocalPlayer || !LocalPlayer.CanAct) return;
            if (LocalPlayer.Summoner.IsCrafting || LocalPlayer.Summoner.IsSpelling) CancelCraft();
            else TouchBinding.Shared.SetCraftOpen(true);
        }
        void ConfirmCraft() { if (LocalPlayer) LocalPlayer.Summoner.CraftSelected(); TouchBinding.Shared.SetCraftOpen(false); }
        void CancelCraft()
        {
            if (LocalPlayer) { LocalPlayer.Summoner.Close(); LocalPlayer.Summoner.CancelCraft(); }
            TouchBinding.Shared.SetCraftOpen(false);
        }
        void SelectHand(int slot) { if (LocalPlayer && AcceptsGameplayInput) LocalPlayer.Combat.SelectSlot(slot); }
        void SetBagPinned(bool on) { bagPinned = on; if (!on) ShowBookLine(null); RefreshBagPanel(); }

        public void ToggleBag() => SetBagPinned(!bagPinned);

        void ShowBookLine(string word)
        {
            if (!bookLine) return;
            bookWord = word;
            bool free = word == null && bookFree;
            string effect = word == null ? null : GameConfig.Current.Tuning?.Describe(word);
            bookLine.text = word == null ? (free ? FreeBookLine : "") : $"{word} · {(string.IsNullOrEmpty(effect) ? "no extra effect" : effect)}";
        }

        sealed class BookCardFocus : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
        {
            public Action<bool> Changed;
            public void OnPointerEnter(PointerEventData _) => Changed?.Invoke(true);
            public void OnPointerExit(PointerEventData _) => Changed?.Invoke(false);
            public void OnSelect(BaseEventData _) => Changed?.Invoke(true);
            public void OnDeselect(BaseEventData _) => Changed?.Invoke(false);
        }
        void GoHome()
        {
            if (Paused) SetPaused(false);
            TouchBinding.Shared.ReleaseAll();
            Session.GoHome();
        }
        public void TogglePause()
        {
            if (KeyBindings.Busy) return;
            if (LeaveAsked) CloseLeave();
            else if (Paused) HandlePauseBack();
            else if (!PadSelectShowsScoreboard()) SetPaused(true);
        }
        void SetPaused(bool on)
        {
            if (!pauseRoot || on == Paused || StateController.CurrentState == UIState.MainMenu) return;
            if (on && ResultShown) return;
            StateController.TransitionToState(on ? UIState.PauseMenu : UIState.GameplayHUD);
        }

        void Update()
        {
            if (safe) UpdateScoreboard();
            if (!safe || (!AcceptsGameplayInput && !Paused)) return;
            if (!KeyBindings.Busy && (DesktopBinding.Shared.Pause.WasPressedThisFrame()
                || ((Paused || LeaveAsked) && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                || (LeaveAsked && PadBackPressed())))
            {
                if (LeaveAsked) CloseLeave();
                else if (Paused) HandlePauseBack();
                else if (bagPinned) SetBagPinned(false);
                else if (craftRoot.activeSelf) CancelCraft();
                else if (ResultShown) { }
                else if (hubScene) pointerFreed = CursorPolicy.Locked;
                else if (!(typewriter && typewriter.User)) SetPaused(true);
            }
            else if (pointerFreed && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
                && !(EventSystem.current && EventSystem.current.IsPointerOverGameObject()))
                pointerFreed = false;
            if (Paused && Time.timeScale != 0f) { resumeScale = Time.timeScale; Time.timeScale = 0f; }
            CloseComposerOnKeys();
            if (BagOpen != WantsBag()) RefreshBagPanel();
            DropFocusedInBag();
            var screen = Touchscreen.current;
            if (!touchChosen && !TouchControlsShown && screen != null && screen.primaryTouch.press.wasPressedThisFrame) ShowTouchControls(true);
            UpdateCards();
        }

        static bool PadBackPressed()
        {
            foreach (var pad in Gamepad.all) if (pad.buttonEast.wasPressedThisFrame) return true;
            return false;
        }

        bool WantsBag() => LocalPlayer && !Paused && !ResultShown && !composerReleaseWait && (bagPinned || (LocalPlayer.Binding is DesktopBinding && !DesktopBinding.Typing && DesktopBinding.Shared.Bag.IsPressed()) ||
            (LocalPlayer.Binding is GamepadBinding pad && pad.BagHeld));

        void OnEnable() => Active = this;

        void LateUpdate()
        {
            if (!safe) return;
            if (Screen.width != lastWidth || Screen.height != lastHeight || Screen.safeArea != lastSafe) ApplySafeArea();
            if (StateController.CurrentState != UIState.GameplayHUD) { FitCards(); return; }
            var rig = CameraRig.Instance;
            bool aiming = rig && rig.isActiveAndEnabled && rig.IsThirdPerson && !NeedsPointer;
            bool crossed = aiming && !rig.Looking;
            if (crosshair && crosshair.activeSelf != crossed) crosshair.SetActive(crossed);
            RefreshDrawMeter();
            bool thirdPerson = rig && rig.isActiveAndEnabled && rig.IsThirdPerson;
            if (thirdPerson != trayAside) { trayAside = thirdPerson; ApplySafeArea(); }
            LearnTips(thirdPerson); FadeTip();
            if (BagOpen && bagFade.alpha < 1f) bagFade.alpha = Mathf.Clamp01((Time.unscaledTime - bagOpenedAt) / .12f);
            SyncComposer(); RefreshComposer();
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .08f;
            using (RefreshMarker.Auto())
            {
                FindLocalPlayer(); RefreshKeys(); RefreshTips(thirdPerson); RefreshInteraction(aiming); RefreshMatch(); RefreshCards(); RefreshSide(); RefreshVitals(); RefreshTray(); RefreshSkills(); RefreshNavigation();
                FitCards();
                if (BagOpen) RefreshBagContents();
                if (ScoreboardShown) RefreshScoreboard();
                if (playRect) playRect.gameObject.SetActive(!LocalPlayer);
            }
        }

        void RefreshKeys()
        {
            var binding = LocalPlayer ? LocalPlayer.Binding : null;
            if (keyVersion == KeyBindings.Version && keyBinding == binding) return;
            keyVersion = KeyBindings.Version; keyBinding = binding;
            var keys = DesktopBinding.Shared;
            spellLabel = binding is GamepadBinding or KeyboardBinding ? SpellKeyLabel(binding) : ControlHints.KeyOf(keys.SpellBox);
            spellHintLabel = SpellKeyLabel(binding);
            bagLabel = BagKeyLabel(binding);
            interactLabel = binding switch { GamepadBinding => "RT", TouchBinding => "GRAB", _ => ControlHints.KeyOf(keys.Interact) };
            FitCap(spellKey, spellCap, spellLabel);
            FitCap(bagKey, bagCap, bagLabel);
            spellHintKey = long.MinValue;
        }

        static void FitCap(TextMeshProUGUI key, RectTransform cap, string label)
        {
            if (!key || !cap) return;
            key.text = label;
            float width = Mathf.Clamp(key.GetPreferredValues(label, 999f, 32f).x + 16f, 32f, 78f);
            key.rectTransform.sizeDelta = cap.sizeDelta = new Vector2(width, 32f);
        }
        void RefreshDrawMeter()
        {
            if (!drawMeter) return;
            var bow = LocalPlayer && LocalPlayer.Combat ? LocalPlayer.Combat.Weapon : null;
            bool drawing = bow && bow.IsDrawing;
            if (drawMeter.activeSelf != drawing) drawMeter.SetActive(drawing);
            if (!drawing) return;
            float power = bow.DrawPower;
            drawFill.anchorMax = new Vector2(power, 1f);
            drawFill.GetComponent<Image>().color = power >= 1f ? Hex(0xffd166) : Hex(0x9ff8d3);
        }

        void RefreshInteraction(bool aiming)
        {
            bool available = aiming && LocalPlayer && LocalPlayer.CanAct && !LocalPlayer.Combat.IsChanneling &&
                (LocalPlayer.Combat.GrabTarget() || LocalPlayer.Combat.DownedTeammateNearby());
            var color = available ? Hex(0x9ff8d3) : Hex(0xfff8e8);
            foreach (var tick in crosshairTicks) tick.color = color;
            if (interactPrompt) interactPrompt.text = available ? interactLabel + StashVerb() : "";
        }

        string StashVerb()
        {
            var target = LocalPlayer.Combat.GrabTarget();
            if (!target || !target.TryGetComponent(out LetterStash stash) || !stash.CanOpen(LocalPlayer) || !stash.WantsExchange(LocalPlayer)) return "";
            return LocalPlayer.Inventory.IsEmpty ? $" · TAKE {stash.Count}" : " · BANK";
        }

        public void Follow(PlayerController player)
        {
            followed = player;
            LocalPlayer = null;
            nextRefresh = 0f;
        }

        void FindLocalPlayer()
        {
            if (LocalPlayer && LocalPlayer.isActiveAndEnabled) return;
            LocalPlayer = followed && followed.isActiveAndEnabled ? followed : null;
            if (!LocalPlayer) foreach (var p in World.Players) if (p && (p.Binding is TouchBinding || p.Binding is DesktopBinding)) { LocalPlayer = p; break; }
            if (!LocalPlayer) foreach (var p in World.Players) if (p && p.Binding is not (null or ScriptedBinding or BotBinding)) { LocalPlayer = p; break; }
            TouchBinding.Shared.OverlayDesktop = TouchControlsShown && LocalPlayer && LocalPlayer.Binding is DesktopBinding;
            TouchBinding.Shared.OverlayBindingId = TouchControlsShown && LocalPlayer && LocalPlayer.Binding is not TouchBinding ? LocalPlayer.Binding?.Id : null;
        }
        IReadOnlyList<PlayerController> Roster => players ?? joins?.Players;
        void RefreshMatch()
        {
            string mode = hubScene ? "Hub" : Match.Mode;
            int target = roundsToWin > 0 ? roundsToWin : Match.Rules.RoundsToWin;
            int round = HouseGoals.Rounds(mode) && rounds && rounds.Round > 0 ? rounds.Round : 0;
            string layoutName = Layout?.Name ?? "";
            if (mode == matchShown && round == roundShown && target == targetShown && layoutName == layoutShown) return;
            matchShown = mode; roundShown = round; targetShown = target; layoutShown = layoutName;
            (string tag, string name) = MatchHeading(mode);
            matchTag.text = round > 0 ? HouseGoals.RoundTag(round, target) : tag;
            matchTitle.text = name;
            string goal = HouseGoals.Line(mode);
            matchDetail.text = goal.Length > 0 ? goal : layoutName.ToUpperInvariant();
            MeasureLabel();
        }
        public static (string tag, string name) MatchHeading(string mode) => HouseModes.Find(mode) is { } house ? (house.Tag, house.Name)
            : mode == "Hub" ? ("WELCOME HOME", "The house") : ("HOUSE PARTY", "Wreckabulary");
        static bool SeatHuman(PlayerController p) => p.Binding != null && p.Binding is not BotBinding;
        public static string SeatScore(int score, int target, string unit) => target > 0 ? $"{score}/{target}{unit}" : $"{score}{unit}";
        HouseLayout Layout => rooms && rooms.Layout != null ? rooms.Layout : clearOut ? clearOut.Layout : practice;
        void RefreshSide()
        {
            UpdateMap(miniMap);
            var layout = Layout;
            string room = LocalPlayer && layout != null ? RoomOf(layout, LocalPlayer) : null;
            string roomKey = room ?? (layout != null ? "" : null);
            if (roomKey != roomShown)
            {
                roomShown = roomKey;
                roomPill.text = room != null ? Spaced(room) : layout != null ? "House" : "";
                float width = Mathf.Min(252f, roomPill.GetPreferredValues(roomPill.text, 999f, 42f).x + 32f);
                roomBacking.sizeDelta = new Vector2(width, 42f);
                roomPill.rectTransform.sizeDelta = new Vector2(width - 32f, 42f);
            }
            roomPill.transform.parent.gameObject.SetActive(roomPill.text.Length > 0 && layout.Rooms.Count > 1);
            var current = Roster;
            int up = 0, all = 0;
            if (current != null) foreach (var p in current) { if (!p) continue; all++; if (!p.IsDowned && !p.IsEliminated) up++; }
            int alive = all > 0 ? up * 64 + all : -1;
            if (alive != aliveShown) { aliveShown = alive; aliveText.text = all > 0 ? $"{up}/{all}" : "-"; }
            aliveText.transform.parent.gameObject.SetActive(all > 1 && !BarSeated);
            if (!ReferenceEquals(checklistText, objectiveShown))
            {
                objectiveShown = checklistText;
                objective.text = checklistText;
                objective.transform.parent.gameObject.SetActive(objective.text.Length > 0);
                objectiveHeight = Mathf.Max(ObjectiveMinHeight, objective.GetPreferredValues(objective.text, ObjectiveWidth, 0f).y);
                objective.rectTransform.sizeDelta = new Vector2(ObjectiveWidth, objectiveHeight);
            }
            SizeObjective();
            LaySideColumn();
        }

        void SizeObjective()
        {
            var goal = (RectTransform)objective.transform.parent;
            var size = GoalPlateShown ? GoalPlate.sizeDelta : new Vector2(ObjectiveWidth, objectiveHeight);
            if (goal.sizeDelta != size) goal.sizeDelta = size;
        }

        void LaySideColumn()
        {
            var alive = (RectTransform)aliveText.transform.parent;
            var clock = timer ? (RectTransform)timer.transform.parent : null;
            var room = (RectTransform)roomPill.transform.parent;
            var goal = (RectTransform)objective.transform.parent;
            bool map = minimapRect.gameObject.activeSelf, row = alive.gameObject.activeSelf || (clock && clock.gameObject.activeSelf);
            int key = (map ? 1 : 0) | (row ? 2 : 0) | (room.gameObject.activeSelf ? 4 : 0);
            if (key == sideKey) return;
            sideKey = key;
            if (eyeRect && eyeRect.parent is RectTransform eye) eye.anchoredPosition = new Vector2(-EyeGap, map ? -MinimapSide : -EyeSize);
            float y = map ? MinimapSide + SideGap : 0f;
            alive.anchoredPosition = new Vector2(alive.anchoredPosition.x, -y);
            if (clock) clock.anchoredPosition = new Vector2(clock.anchoredPosition.x, -y);
            if (row) y += SideRowHeight + SideRowGap;
            room.anchoredPosition = new Vector2(room.anchoredPosition.x, -y);
            if (room.gameObject.activeSelf) y += RoomHeight + RoomGap;
            goal.anchoredPosition = new Vector2(goal.anchoredPosition.x, -y);
        }
        void RefreshVitals()
        {
            bool shown = LocalPlayer && !FullCardShown;
            bool solo = shown && !HealthOnBar;
            if (vitals.gameObject.activeSelf != solo) vitals.gameObject.SetActive(solo);
            if (solo) ShowVitals(soloVitals, LocalPlayer.Health, true);
            RefreshEffects(shown, solo);
        }

        void RefreshEffects(bool shown, bool solo)
        {
            var health = shown ? LocalPlayer.Health : null;
            int shield = health ? Mathf.CeilToInt(health.Bubble) : 0;
            bool bleeding = health && health.BleedingLeft > 0f, slowed = shown && LocalPlayer.SlowLeft > 0f;
            int key = shown ? shield * 4 + (bleeding ? 2 : 0) + (slowed ? 1 : 0) : -1;
            if (key != effectsShown) { effectsShown = key; effects.text = EffectsLine(shield, bleeding, slowed); }
            bool on = effects.text.Length > 0;
            if (effects.gameObject.activeSelf != on) effects.gameObject.SetActive(on);
            var at = vitals.anchoredPosition + new Vector2(VitalsBarX, solo ? PlayerBar.SoloTall + EffectsGap : 0f);
            if (effects.rectTransform.anchoredPosition != at) effects.rectTransform.anchoredPosition = at;
        }

        string EffectsLine(int shield, bool bleeding, bool slowed)
        {
            sb.Clear();
            if (shield > 0) sb.Append('+').Append(shield).Append(" SHIELD");
            if (bleeding) sb.Append(sb.Length > 0 ? "  " : "").Append("<color=#FF6B5E>BLEEDING</color>");
            if (slowed) sb.Append(sb.Length > 0 ? "  " : "").Append("<color=#FFC568>SLOWED</color>");
            return sb.ToString();
        }
        void ShowHand(HandView view, int slot, bool detail)
        {
            if (view == null || !view.face || !view.edge || !view.label || !view.empty || !view.icon) return;
            var combat = LocalPlayer.Combat;
            var gear = combat.GearIn(slot);
            bool active = slot == combat.ActiveSlot;
            bool carrying = active && combat.IsHolding && !combat.Weapon;
            string word = gear ? gear.word : carrying ? "Carrying" : null;
            if (detail)
            {
                view.face.color = active ? BagCardActive : BagCard; view.edge.color = active ? Gold : BagCardEdge;
                view.label.text = gear ? Spaced(gear.word) : ""; view.label.color = BagInk;
                view.empty.enabled = !gear;
            }
            else
            {
                view.face.color = active ? GlassActive : Glass; view.edge.color = active ? GlassEdge : GlassIdle;
                view.label.text = word ?? ""; view.label.color = OnGame;
                view.empty.enabled = word == null;
            }
            if (view.key) view.key.text = HandKeyLabel(LocalPlayer.Binding, slot);
            Wreckabulary.UI.ItemArt.Apply(view.icon, gear ? gear.word : null);
            view.icon.enabled = view.icon.texture;
            if (view.wearTrack)
            {
                int most = gear && gear.Definition != null ? Mathf.Max(1, gear.Definition.Durability) : 0;
                view.wearTrack.SetActive(detail && gear && most > 0);
                if (most > 0) view.wear.fillAmount = Mathf.Clamp01(gear.DurabilityLeft / most);
            }
        }
        void RefreshTray()
        {
            bool shown = LocalPlayer && !FullCardShown;
            tray.gameObject.SetActive(shown);
            if (!shown) return;
            for (int i = 0; i < 2; i++) ShowHand(hands[i], i, false);
            var inv = LocalPlayer.Inventory;
            FillCells(trayCells, inv);
            var summon = LocalPlayer.Summoner;
            craftProgress.transform.parent.gameObject.SetActive(summon.IsCrafting);
            craftProgress.fillAmount = summon.CraftProgress;
            RefreshSpellHint(inv, summon);
        }
        void RefreshSpellHint(LetterInventory inv, Summoner summon)
        {
            bool quiet = summon.IsCrafting || summon.IsSpelling || (craftRoot && craftRoot.activeSelf);
            string spell = spellHintLabel;
            var needs = summon.Needs;
            var objectives = summon.Objectives;
            bool free = inv.SpellsFree;
            long key = quiet ? long.MinValue + 1 : free ? long.MinValue + 2 : LetterSignature(inv.Letters, inv.Capacity);
            if (key != spellHintKey || !needs.Equals(spellHintNeeds) || !ReferenceEquals(objectives, spellHintObjectives))
            {
                spellHintKey = key; spellHintNeeds = needs; spellHintObjectives = objectives;
                string line = quiet ? "" : free ? FreeSpellHint(spell) : SpellHintText(summon.Words, inv.Letters, inv.Capacity, spell, needs, objectives);
                spellHint.text = line;
                spellHint.color = free || line.Contains(" ready") ? Hex(0x9ff8d3) : OnGame;
            }
            bool shown = spellHint.text.Length > 0;
            if (spellHint.gameObject.activeSelf != shown) spellHint.gameObject.SetActive(shown);
        }
        static long LetterSignature(IReadOnlyList<char> letters, int capacity)
        {
            unchecked
            {
                long sum = 0, squares = 0;
                for (int i = 0; i < letters.Count; i++) { long v = letters[i] * 2654435761L; sum += v; squares += v * v; }
                return sum * 31 + squares + capacity;
            }
        }

        public static string SpellHintText(IEnumerable<WordEntry> words, IReadOnlyList<char> letters, int capacity, string key) =>
            SpellHintText(words, letters, capacity, key, SpellNeeds.Unarmed, null);

        public static string SpellHintText(IEnumerable<WordEntry> words, IReadOnlyList<char> letters, int capacity, string key,
            SpellNeeds needs, IReadOnlyCollection<string> objectives)
        {
            if (words == null || letters == null || letters.Count == 0) return "";
            var usable = words.Where(w => w != null && !string.IsNullOrEmpty(w.word)).ToList();
            var ready = WordSolver.Spellable(usable, letters, needs, objectives);
            if (ready.Count > 0) return ready[0].word + " ready" + (string.IsNullOrEmpty(key) ? "" : " · " + key);
            var near = WordSolver.Hints(usable, letters, capacity, needs, objectives);
            return near.Count > 0 ? near[0].entry.word + " needs " + near[0].missing : "";
        }
        public static string FreeSpellHint(string key) => string.IsNullOrEmpty(key) ? FreeSpelling : FreeSpelling + " · " + key;

        public static string SpellKeyLabel(InputBinding binding) => binding switch
        {
            GamepadBinding => "Y",
            TouchBinding => "SPELL",
            KeyboardBinding keys => keys.Id.EndsWith("Left", StringComparison.Ordinal) ? "K" : "R-SHIFT",
            _ => ControlHints.KeyOf(DesktopBinding.Shared.SpellBox),
        };
        public static string BagKeyLabel(InputBinding binding) =>
            binding is GamepadBinding ? ControlHints.PadBag : ControlHints.KeyOf(DesktopBinding.Shared.Bag);
        public static string HandKeyLabel(InputBinding binding, int slot) =>
            binding is GamepadBinding ? ControlHints.PadHand(slot) : slot >= 0 && slot < HandNumbers.Length ? HandNumbers[slot] : (slot + 1).ToString();
        void FillCells(LetterCell[] cells, LetterInventory inv)
        {
            ArrangeLetters(inv);
            var crafting = LocalPlayer.Summoner.IsCrafting ? LocalPlayer.Summoner.CraftWord ?? "" : "";
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                cell.face.transform.parent.gameObject.SetActive(i < inv.Capacity);
                bool held = i < bagOrder.Count, reserved = !held && i < bagOrder.Count + inv.ReservedCount;
                int r = i - bagOrder.Count;
                cell.face.color = held ? Tile : reserved ? Reserved : cell.ring ? BagCell : EmptyCell;
                cell.edge.enabled = held;
                if (cell.ring) cell.ring.enabled = !held && !reserved;
                if (cell.outline) cell.outline.enabled = !held && !reserved;
                cell.letter.color = reserved ? ReservedInk : Ink;
                cell.letter.text = held ? One(bagOrder[i]) : reserved && r < crafting.Length ? One(crafting[r]) : "";
            }
        }

        static string One(char letter) => letter >= 'A' && letter <= 'Z' ? LetterTexts[letter - 'A'] : letter.ToString();
        void RefreshBagPanel()
        {
            if (!bagPanel) return;
            bool open = WantsBag();
            if (open != bagPanel.activeSelf)
            {
                bagPanel.SetActive(open);
                if (open)
                {
                    bagOpenedAt = Time.unscaledTime; bagFade.alpha = 0f;
                    TouchBinding.Shared.ReleaseAll();
                    foreach (var stick in sticks) if (stick) stick.Release();
                }
                OnBagShown(open);
            }
            HideForBag(open);
            RefreshModeActionVisibility();
            if (!open) return;
            bagPanel.transform.SetAsLastSibling();
            RefreshBagContents();
        }

        void HideForBag(bool open)
        {
            if (bagHidden == open) return;
            bagHidden = open;
            foreach (var group in bagHides) { if (!group) continue; group.alpha = open ? 0f : 1f; group.blocksRaycasts = group.interactable = !open; }
        }
        void RefreshBagContents()
        {
            if (!LocalPlayer) return;
            var inv = LocalPlayer.Inventory;
            bagPanelCount.text = $"{inv.TotalCount}/{inv.Capacity}";
            FillCells(bagCells, inv);
            for (int i = 0; i < 2; i++) ShowHand(bagHands[i], i, true);
            int effects = 0;
            void Effect(Texture picture, Color tint, float seconds, Rect? uv = null)
            {
                var (root, icon, badge, count) = effectChips[effects++];
                root.SetActive(true); icon.texture = picture; icon.color = tint;
                icon.uvRect = uv ?? new Rect(0f, 0f, 1f, 1f);
                badge.SetActive(seconds > 0f); count.text = Mathf.CeilToInt(seconds).ToString();
            }
            var health = LocalPlayer.Health;
            if (health.Bubble > 0f) Effect(Glyph("bubble"), Hex(0x9fe3ff), health.BubbleLeft);
            if (LocalPlayer.BoostLeft > 0f) Effect(Glyph("speed"), Hex(0xb9ffb0), LocalPlayer.BoostLeft);
            if (LocalPlayer.SlowLeft > 0f)
            {
                bool clock = Wreckabulary.UI.ItemArt.TryGet("CLOCK", out var slowIcon, out var slowUv);
                Effect(clock ? slowIcon : Glyph("timer"), clock ? Color.white : Hex(0xcdb4ff), LocalPlayer.SlowLeft, slowUv);
            }
            var combat = LocalPlayer.Combat;
            if (combat.IsHolding && !combat.Weapon)
            {
                var held = combat.Held.name.Replace("(Clone)", "").Trim().ToUpperInvariant();
                bool pictured = Wreckabulary.UI.ItemArt.TryGet(held, out var picture, out var heldUv);
                Effect(pictured ? picture : Glyph("carry"), pictured ? Color.white : BagInk, 0f, heldUv);
            }
            for (int i = effects; i < effectChips.Count; i++) effectChips[i].root.SetActive(false);
            RefreshBook(inv);
            LayoutBag(effects);
            ShowDragTargets();
            UpdateMap(bigMap);
        }

        void RefreshBook(LetterInventory inv)
        {
            bool free = LocalPlayer.CanAct && !LocalPlayer.Summoner.IsCrafting;
            bool gift = inv.SpellsFree;
            if (gift != bookFree)
            {
                bookFree = gift;
                if (bookWord == null) ShowBookLine(null);
            }
            foreach (var card in bookCards)
            {
                spare.Clear(); spare.AddRange(inv.Letters);
                bool all = true;
                for (int i = 0; i < card.word.Length; i++)
                {
                    int at = gift ? -1 : spare.IndexOf(card.word[i]);
                    bool got = gift || at >= 0;
                    if (at >= 0) spare.RemoveAt(at); else if (!got) all = false;
                    card.tiles[i].color = got ? TileGot : Color.clear;
                    card.rims[i].enabled = !got;
                    card.letters[i].color = got ? TileGotInk : TileMissingInk;
                }
                card.face.color = all ? Hex(0xffe7b8, .17f) : BagCard;
                card.edge.color = all ? Gold : BagCardEdge;
                card.glow.enabled = all;
                card.button.interactable = all && free;
            }
        }

        void LayoutBag(int effects)
        {
            float y = 0f;
            static float Rows(int n) => n == 0 ? 0f : Mathf.Ceil(n / (float)ChipsPerRow) * (Chip + ChipGap) - ChipGap;
            void Place(RectTransform row, float height)
            {
                row.gameObject.SetActive(height > 0f);
                if (height <= 0f) return;
                row.anchoredPosition = new Vector2(0f, -y); row.sizeDelta = new Vector2(BagColumn, height);
                y += height + BagGap;
            }
            void Flow(int count, Func<int, GameObject> chip)
            {
                for (int i = 0; i < count; i++)
                    ((RectTransform)chip(i).transform).anchoredPosition = new Vector2(i % ChipsPerRow * (Chip + ChipGap), -(i / ChipsPerRow) * (Chip + ChipGap));
            }
            Place(bagLetters, 155f); Place(bagHandsRow, 145f); Place(bagEffectsRow, Rows(effects));
            Flow(effects, i => effectChips[i].root);
            bagColumn.sizeDelta = new Vector2(BagColumn, Mathf.Max(0f, y - BagGap));
            var area = safe.rect;
            float mapTarget = BagDrag.MapTarget(area.width, BagSide + BagColumn + BagGap, BagGap, BagMapFloor, bookComfort, 188f, BookColumn);
            float mapSize = BagDrag.MapSide(area.height - BagTop - BagBottom, bagColumn.sizeDelta.y, BagGap, BagMapFloor, mapTarget);
            float footprint = Mathf.Max(mapSize, BagColumn);
            float reserve = PlaceDropColumns(BagDrag.SideColumnWidth(area.width, footprint + 2f * BagGap + bookComfort));
            float binEdge = BagDrag.ColumnEdge + dropColumnWidth, between = area.width - 2f * binEdge - BagDrag.ColumnGap;
            float bookWidth = Mathf.Min(BookColumn, Mathf.Max(188f, between - footprint - 2f * BagGap));
            bagBook.offsetMin = new Vector2(-reserve - bookWidth, BagBottom);
            bagBook.offsetMax = new Vector2(-reserve, -BagTop);
            if (bagClose) bagClose.anchoredPosition = new Vector2(-reserve - BookCloseInset, -BagTop - BookTitleTop);
            int columns = Mathf.Clamp(BookColumnsFor(bookWidth), 1, 4);
            recipeGrid.constraintCount = columns;
            recipeGrid.cellSize = new Vector2((bookWidth - 8f - (columns - 1) * 12f) / columns, BookCardHeight);
            float left = binEdge + Mathf.Max(BagGap, (between - bookWidth - footprint) * .5f);
            bagMapArea.anchorMin = bagMapArea.anchorMax = bagMapArea.pivot = new Vector2(0f, 1f);
            bagMapArea.anchoredPosition = new Vector2(left + (footprint - mapSize) * .5f, -BagTop);
            bagMapArea.sizeDelta = new Vector2(mapSize, mapSize);
            bagColumn.anchorMin = bagColumn.anchorMax = bagColumn.pivot = new Vector2(0f, 1f);
            bagColumn.anchoredPosition = new Vector2(left + (footprint - BagColumn) * .5f, -BagTop - mapSize - BagGap);
            bagMapFrame.sizeDelta = Vector2.one * mapSize;
            bagMapShadow.sizeDelta = Vector2.one * (mapSize + 2f * 31f * 75f / 28f);
        }
        void RefreshSkills()
        {
            if (!LocalPlayer) return;
            var combat = LocalPlayer.Combat;
            var weapon = combat.Weapon;
            bool canAct = LocalPlayer.CanAct && !LocalPlayer.IsDodging;
            bool free = canAct && !LocalPlayer.Summoner.IsSpelling && !LocalPlayer.Summoner.IsCrafting && !combat.IsChanneling;
            foreach (var pair in skillButtons)
            {
                bool available = pair.Key switch
                {
                    TouchAction.Block => free && weapon && weapon.Shield != null,
                    TouchAction.Drop => free && combat.IsHolding,
                    TouchAction.Grab => free && !(combat.IsHolding && !weapon),
                    TouchAction.Dodge => canAct && LocalPlayer.Health.CanDodge,
                    TouchAction.Jump => canAct,
                    TouchAction.Craft => canAct && (LocalPlayer.Summoner.IsCrafting || LocalPlayer.Summoner.IsSpelling || !combat.IsChanneling),
                    _ => free,
                };
                pair.Value.button.SetAvailable(available);
            }
            skillButtons[TouchAction.Grab].label.text = combat.IsReviving ? "REVIVING" : combat.DownedTeammateNearby() ? "HOLD REVIVE" : "INTERACT";
            skillButtons[TouchAction.Craft].label.text = LocalPlayer.Summoner.IsCrafting || LocalPlayer.Summoner.IsSpelling ? "CANCEL" : "SPELL";
            var job = weapon && weapon.Shield == null ? weapon.Definition : null;
            skillButtons[TouchAction.Attack].label.text = combat.IsHolding && !weapon ? "THROW"
                : job == null ? "SMASH"
                : job.Use != null ? "USE" : job.Thrown != null ? "THROW" : job.Deploy != null ? "PLACE"
                : job.Family == HandlingFamily.Ranged ? "FIRE" : "SMASH";
        }
        void RefreshNavigation()
        {
            bool usingMenu = typewriter && LocalPlayer && typewriter.User == LocalPlayer;
            typewriterControls.SetActive(usingMenu);
            if (!usingMenu) return;
            var mode = typewriter.Modes[typewriter.Selected];
            typewriterChoice.text = mode.label + "\n<size=65%>" + mode.blurb + "</size>";
        }

        void ApplySafeArea()
        {
            if (!safe) return;
            lastWidth = Screen.width; lastHeight = Screen.height; lastSafe = Screen.safeArea;
            if (lastWidth <= 0 || lastHeight <= 0) return;
            safe.anchorMin = new Vector2(lastSafe.xMin / lastWidth, lastSafe.yMin / lastHeight); safe.anchorMax = new Vector2(lastSafe.xMax / lastWidth, lastSafe.yMax / lastHeight);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            float safeWidth = Mathf.Max(1f, lastSafe.width), safeHeight = Mathf.Max(1f, lastSafe.height);
            var coverMin = new Vector2(-lastSafe.xMin / safeWidth, -lastSafe.yMin / safeHeight);
            var coverMax = new Vector2(1f + (lastWidth - lastSafe.xMax) / safeWidth, 1f + (lastHeight - lastSafe.yMax) / safeHeight);
            foreach (var cover in new[] { bagBackdrop, resultShade }) if (cover) { cover.anchorMin = coverMin; cover.anchorMax = coverMax; }
            bool portrait = lastWidth < lastHeight, touch = TouchControlsShown, aside = trayAside && !portrait && !touch;
            if (vitals) vitals.anchoredPosition = new Vector2(VitalsX, touch ? (portrait ? 420f : 250f) : aside ? AsideVitalsY : VitalsY);
            if (tray)
            {
                tray.anchorMin = tray.anchorMax = tray.pivot = portrait ? new Vector2(0f, 1f) : aside ? Vector2.zero : new Vector2(.5f, 0f);
                tray.anchoredPosition = portrait ? new Vector2(38f, -134f) : aside ? new Vector2(VitalsX, TrayY) : new Vector2(0f, touch ? 24f : TrayY);
                float available = safeWidth / Mathf.Max(.01f, UiCanvas.scaleFactor) - 76f;
                tray.localScale = Vector3.one * Mathf.Min(1f, Mathf.Max(.1f, available / tray.sizeDelta.x));
            }
            if (playRect)
            {
                playRect.anchorMin = playRect.anchorMax = portrait ? Vector2.zero : new Vector2(.5f, 1f);
                playRect.pivot = playRect.anchorMin;
            }
            PlaceTop();
            ResizeStatus();
        }
        void ResizeStatus()
        {
            if (!status || !UiCanvas) return;
            float logicalWidth = lastSafe.width / Mathf.Max(.01f, UiCanvas.scaleFactor);
            float width = Mathf.Min(525f, logicalWidth - 600f);
            if (width < 360f) width = Mathf.Min(525f, logicalWidth - 64f);
            var size = statusText.GetPreferredValues(statusText.text, width - 50f, 0f);
            status.sizeDelta = new Vector2(Mathf.Min(width, size.x + 50f), size.y + 25f);
            statusText.rectTransform.sizeDelta = new Vector2(Mathf.Min(width, size.x + 50f) - 46f, size.y + 4f);
        }
        void OnApplicationFocus(bool focused)
        {
            if (focused) return; TouchBinding.Shared.ReleaseAll();
            foreach (var stick in sticks) if (stick) stick.Release(); if (LocalPlayer) LocalPlayer.Summoner.Close();
            if (PauseOnFocusLoss && safe && LocalPlayer && !hubScene)
                SetPaused(true);
        }
        void OnDisable()
        {
            ClosePauseSettings();
            RestoreSimulation();
            TouchBinding.Shared.ReleaseAll();
            DesktopBinding.Typing = false;
            if (LocalPlayer) LocalPlayer.Summoner.Typed = false;
            if (bagHidden) HideForBag(false);
            ShowScoreboard(false);
            if (Active == this) Active = null;
        }
        void OnDestroy()
        {
            ClosePauseSettings();
            if (StateController)
            {
                StateController.StateChanged -= OnUiStateChanged;
                StateController.TransitionCompleted -= OnUiTransitionCompleted;
            }
            if (joins) joins.Joined -= OnPlayerJoined;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= NoteScene;
            RestoreSimulation();
            UnhookKeyboard();
            foreach (var sprite in ownedSprites) if (sprite) Destroy(sprite);
            foreach (var texture in ownedTextures) if (texture) Destroy(texture);
        }

        public void SetTitle(string text, string sub = "")
        {
            text ??= ""; sub ??= "";
            if (text == lastTitle && sub == lastSub) return;
            lastTitle = text; lastSub = sub;
            string line = text.Length == 0 ? sub : sub.Length == 0 ? text : text + " · " + sub;
            if (line.Length > 0) Toast(line);
        }
        public void SetInstruction(string main, string hint = "")
        {
            string text = string.IsNullOrEmpty(hint) ? main : $"<b>{main}</b>\n<size=82%><color=#FFF4E0CC>{hint}</color></size>";
            if (instruction) instruction.text = text;
            if (!statusText || statusText.text == text) return;
            statusText.text = text;
            status.gameObject.SetActive(!string.IsNullOrEmpty(main) || !string.IsNullOrEmpty(hint));
            ResizeStatus();
        }
        public void SetChecklist(string text)
        {
            if (checklistSource != null && text == checklistSource && (!checklist || checklist.text == text)) return;
            checklistSource = text;
            if (checklist) checklist.text = text;
            checklistText = (text ?? "").Replace("<b>", "").Replace("</b>", "").Replace("<size=75%>", "<size=90%>");
        }
        public void SetTimer(string text)
        {
            timerText = text;
            if (timer) timer.text = text;
            if (bar) PlaceClock();
            else if (timer) timer.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
        public void SetScoreboard(IReadOnlyList<PlayerController> currentPlayers, Func<PlayerController, int> currentWins, int targetWins, bool winsVisible)
            => SetScoreboard(currentPlayers, currentWins, targetWins, winsVisible, null);

        public void SetScoreboard(IReadOnlyList<PlayerController> currentPlayers, Func<PlayerController, int> currentScore, int target, bool scoreVisible, string unit)
        { players = currentPlayers; wins = currentScore; roundsToWin = target; showWins = scoreVisible; scoreUnit = unit; }

        static readonly Regex Words = new("(?<=[a-z])(?=[A-Z])");
        public static string Spaced(string name) => Words.Replace(name ?? "", " ");

        static string RoomOf(HouseLayout layout, PlayerController p) => layout.RoomAt(p.transform.position.x, p.transform.position.y, p.transform.position.z);

        public static string Initial(HouseLayout layout, string name) =>
            name.Substring(0, layout.Rooms.Count(r => r.Name[0] == name[0]) > 1 ? Mathf.Min(2, name.Length) : 1);

        LetterCell MakeCell(string label, RectTransform parent, Vector2 position, Vector2 size, float fontSize)
        {
            var holder = Rect(label, parent, new Vector2(0f, 1f), position, size); holder.pivot = new Vector2(0f, 1f);
            var edge = Panel("Edge", holder, new Vector2(.5f, .5f), new Vector2(0f, -3f), size, new Vector2(.5f, .5f), TileEdge).GetComponent<Image>();
            var face = Panel("Face", holder, new Vector2(.5f, .5f), Vector2.zero, size, new Vector2(.5f, .5f), Tile).GetComponent<Image>();
            var letter = Text("Letter", face.transform, new Vector2(.5f, .5f), Vector2.zero, size, fontSize, Ink);
            return new LetterCell { face = face, edge = edge, letter = letter };
        }
        HandView MakeHand(string label, RectTransform parent, Vector2 position, Vector2 size, int slot, Action onTap)
        {
            var rt = Panel(label, parent, Vector2.zero, position, size, Vector2.zero, Glass);
            var view = new HandView { face = rt.GetComponent<Image>() };
            view.face.raycastTarget = true;
            rt.gameObject.AddComponent<Button>().onClick.AddListener(() => onTap());
            view.edge = CreateImage("Edge", rt, Vector2.zero, Vector2.zero, Vector2.zero, GlassIdle);
            Stretch(view.edge.rectTransform); view.edge.sprite = ringSprite; view.edge.type = Image.Type.Sliced;
            var key = view.key = Text("Key", rt, new Vector2(0f, 1f), new Vector2(8f, -7f), new Vector2(18f, 26f), 20f, OnGame, TextAlignmentOptions.Left);
            key.text = (slot + 1).ToString(); key.rectTransform.pivot = new Vector2(0f, 1f);
            view.icon = Rect("Icon", rt, new Vector2(0f, .5f), new Vector2(52f, -3f), new Vector2(52f, 52f)).gameObject.AddComponent<RawImage>();
            view.icon.raycastTarget = false;
            view.empty = GlyphImage("Open hand", rt, new Vector2(.5f, .5f), new Vector2(8f, 0f), 36f, "hand", Hex(0xfff7e8, .72f));
            view.label = Text("Word", rt, new Vector2(1f, .5f), new Vector2(-10f, -3f), new Vector2(size.x - 96f, 32f), 18f, OnGame, TextAlignmentOptions.Right);
            view.label.rectTransform.pivot = new Vector2(1f, .5f);
            OnGameText(key); OnGameText(view.label);
            return view;
        }
        Sprite Icon(TouchAction action)
        {
            if (!iconAtlas || (int)action > 8) return null;
            var bounds = new Rect[] { new(34,19,391,376), new(453,100,369,286), new(925,28,280,373), new(39,445,365,336), new(499,434,313,353), new(874,439,333,346), new(35,833,381,375), new(487,813,281,405), new(836,870,410,314) };
            var r = bounds[(int)action]; float sx = iconAtlas.width / 1254f, sy = iconAtlas.height / 1254f;
            var sprite = Sprite.Create(iconAtlas, new Rect(r.x * sx, iconAtlas.height - (r.y + r.height) * sy, r.width * sx, r.height * sy), new Vector2(.5f, .5f), 100f);
            ownedSprites.Add(sprite); return sprite;
        }
        Sprite MakeShape(bool circle, float ring = 0f)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = circle ? "HUD circle" : "HUD panel", filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - 31.5f), dy = Mathf.Abs(y - 31.5f);
                float distance = circle ? Mathf.Sqrt(dx * dx + dy * dy) - 30f : new Vector2(Mathf.Max(dx - 20f, 0f), Mathf.Max(dy - 20f, 0f)).magnitude - 11f;
                float alpha = Mathf.Clamp01(.5f - distance);
                if (ring > 0f) alpha *= Mathf.Clamp01(.5f + distance + ring);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            if (ring > 0f) texture.name = "HUD ring";
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, circle ? Vector4.zero : new Vector4(14f, 14f, 14f, 14f));
            ownedSprites.Add(sprite); ownedTextures.Add(texture); return sprite;
        }
        Sprite MakeLineBox()
        {
            const int size = 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = LineBoxName, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                pixels[y * size + x] = new Color(1f, 1f, 1f, x == 0 || y == 0 || x == size - 1 || y == size - 1 ? 1f : 0f);
            texture.SetPixels(pixels); texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, Vector4.one);
            ownedSprites.Add(sprite); ownedTextures.Add(texture); return sprite;
        }

        Sprite MakeSoft()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "HUD soft", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x - 31.5f) - 3.5f, 0f), dy = Mathf.Max(Mathf.Abs(y - 31.5f) - 3.5f, 0f);
                float t = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 28f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, 1f - t * t * (3f - 2f * t));
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, Vector4.one * 31f);
            ownedSprites.Add(sprite); ownedTextures.Add(texture); return sprite;
        }

        Sprite MakeScrim()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Bag scrim", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            Color middle = Hex(0x10292b, .55f), corner = Hex(0x0b1d1f, .85f);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = (x - 63.5f) / 63.5f, dy = (y - 63.5f) / 63.5f;
                pixels[y * size + x] = Color.Lerp(middle, corner, Mathf.Clamp01(Mathf.Sqrt((dx * dx + dy * dy) * .5f)));
            }
            texture.SetPixels(pixels); texture.Apply(false);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
            ownedSprites.Add(sprite); ownedTextures.Add(texture); return sprite;
        }

        RectTransform Rect(string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rt = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor; rt.anchoredPosition = position; rt.sizeDelta = size; return rt;
        }
        RectTransform Panel(string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot, Color color, bool circle = false)
        {
            var rt = Rect(label, parent, anchor, position, size); rt.pivot = pivot;
            var image = rt.gameObject.AddComponent<Image>(); image.sprite = circle ? roundSprite : panelSprite;
            image.type = circle ? Image.Type.Simple : Image.Type.Sliced; image.color = color; image.raycastTarget = false; return rt;
        }
        Image CreateImage(string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var image = Rect(label, parent, anchor, position, size).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        Image Fill(string label, Transform parent, Color color)
        {
            var image = CreateImage(label, parent, Vector2.zero, Vector2.zero, Vector2.zero, color); image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero; image.sprite = panelSprite;
            image.type = Image.Type.Filled; image.fillMethod = Image.FillMethod.Horizontal; image.fillOrigin = 0; return image;
        }
        TextMeshProUGUI Text(string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var text = Rect(label, parent, anchor, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = LobbyFonts.Body ? LobbyFonts.Body : GameFont; text.fontSize = fontSize; text.fontStyle = FontStyles.Normal;
            text.color = color; text.alignment = alignment; text.textWrappingMode = TextWrappingModes.NoWrap; text.raycastTarget = false; return text;
        }
        Button MakeButton(string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, string caption, Action action, Color? colour = null)
        {
            var rt = Panel(label, parent, anchor, position, size, anchor, colour ?? Teal); rt.GetComponent<Image>().raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>(); button.onClick.AddListener(() => action?.Invoke());
            Text("Label", rt, new Vector2(.5f, .5f), Vector2.zero, size - new Vector2(8f, 4f), 20f, colour.HasValue ? Ink : Cream).text = caption; return button;
        }
    }
}
