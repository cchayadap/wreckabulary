using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public partial class GameHud
    {
        const int MaxCards = PlayerBar.MostSeats;
        const float BrandX = 38f, LabelX = 112f, LabelY = 28f, LabelWidth = 460f, LabelHeight = 100f;
        const float ChipY = -25f, BannerY = -100f, StatusY = -140f, PortraitStatusY = -360f, PlayY = -185f, ToastY = -238f, ModeActionsY = -300f;
        const float LeftColumnTop = 150f;
        const float ClockHeight = 30f, ClockSize = 28f, ClockBig = 64f, ScoreTop = 30f, ScoreRowHeight = 56f, ScoreSize = 46f, ColonWidth = 28f;
        const float CaptionTop = 88f, CaptionHeight = 20f, CaptionSize = 15f, CentreMinText = 15f, LeadRow = 26f, LeadScoreSize = 22f;
        const float TargetSide = 56f, TargetGap = 6f, ProgressTop = 48f, ProgressHeight = 34f, ProgressSize = 24f, LeadSize = 18f;
        const float ScoresSize = 44f, ScoresGap = 10f, ScoresCapWidth = 52f, ScoresCapHeight = 22f, ScoresGlyphSize = 26f;
        const float ScoresReach = ScoresGap + ScoresSize + (ScoresCapWidth - ScoresSize) * .5f;
        const float CrossShare = .62f, StatusLineSpacing = -18f;
        static readonly IReadOnlyList<int> NoTeams = Array.Empty<int>();
        static readonly List<ColourSeat> colourSeats = new();
        static readonly List<PlayerController> colourOwners = new();
        static readonly List<int> colourFrames = new();
        static IReadOnlyList<PlayerController> colourSource;
        static int colourFrame = -1;
        static bool colourAccented;
        static readonly Color CardBacking = Hex(0x16302f), WreckedFrame = Hex(0x8b9592), WreckedTint = Hex(0x6d7472);
        static readonly Color WreckedShade = Hex(0x0b1515, .55f), DownShade = Hex(0x0b1515, .28f), PulseFrame = Hex(0xff8a6b);
        static readonly Color HealthTrack = Hex(0x0b1f1f, .78f), ScoreInk = Hex(0xffe7b8), Plate = Hex(0x121c24, .72f), CentreFill = Hex(0x121c24);
        static readonly Color Amber = Hex(0xffc568), Breath = Hex(0x9ff8d3);

        sealed class VitalsView
        {
            public RectTransform track, staminaTrack;
            public Image fill, staminaFill;
            public TextMeshProUGUI number, numberInk;
            public float width, staminaWidth, shownFill = float.NaN, shownBreath = float.NaN;
            public int shownReading = int.MinValue, shownBand = -1, shownWinded = -1;
        }

        sealed class PlayerCard
        {
            public RectTransform root;
            public VitalsView vitals;
            public Image backing, shade, frame, lead, cross;
            public RawImage portrait;
            public TextMeshProUGUI letter, status, name, score;
            public PlayerController seat;
            public SeatPortrait face;
            public string shownName, shownUnit;
            public int shownWins = int.MinValue, shownTarget = int.MinValue, shownStatus = int.MinValue, shownLook = -1, shownSeats = -1;
            public bool pulsing, shownLead;
            public Texture shownPicture;
            public Color vivid, nameColour;
            public float shownX = float.NaN;
        }

        readonly List<PlayerCard> cards = new();
        readonly List<PlayerController> barSeats = new();
        readonly List<int> barTeams = new();
        readonly List<BarBlock> barBlocks = new();
        readonly List<BarBlock> barBlocksShifted = new();
        readonly List<int> barScores = new();
        readonly int[] barOrder = new int[MaxCards];
        readonly float[] barCentres = new float[MaxCards];
        readonly Vector3[] barCorners = new Vector3[4];
        RectTransform bar, plate, brandRect, labelRect, minimapRect, eyeRect, centre, scoresButton;
        TextMeshProUGUI barClock, leftScore, rightScore, scoreColon, centreCaption, leadScore, scoresKey;
        RawImage targetPicture;
        Image leftLead, rightLead, scoresFace;
        GameObject scoresCap;
        float barWidth = -1f, barScale = 1f, barTop = PlayerBar.Top, topInset, labelWidth = LabelWidth;
        int barSize = -1, barSplit, barLeader = ScoreLine.NoLead;
        float barCentreWidth = -1f, barCentreX, barTall = -1f;
        bool barSeated, laidLead;
        readonly List<Image> outlines = new();
        readonly int[] groupFirst = new int[MaxCards], groupLast = new int[MaxCards];
        int groupCount, barJoins = -1;
        Image centreFill;
        ScoreCentre barCentre, laidCentre;
        Func<int, int> sideScore;
        string sideCaption = "", targetWord = "", timerText = "";
        string shownClock, shownCaption, shownTarget;
        int shownLeft = int.MinValue, shownRight = int.MinValue, shownTotal = int.MinValue, shownLeadSide = int.MinValue, shownBest = int.MinValue, shownBestOf = int.MinValue;
        bool shownSides, shownProgress;
        Color shownLeftColour, shownRightColour, shownBestColour;
        InputBinding scoresKeyFor;
        bool scoresKeyTouch, scoresKeySet;

        public float TopInset => topInset;
        public bool BarShown => bar && bar.gameObject.activeSelf;
        public float BarBottom => BarShown ? barTop + (BarHeight(barSize) + PlayerBar.PlatePad) * barScale : 0f;
        public string CentreLead => ClockCentred && laidLead ? leadScore.text : "";
        public bool BarSeated => BarShown && barSeated;
        public bool HealthOnBar => BarSeated && LocalPlayer && barSeats.Contains(LocalPlayer);
        public float LeftColumnStart => Mathf.Max(LeftColumnTop, BarShown && OverlapsLeftColumn() ? BarBottom + PlayerBar.Clearance : 0f);
        public ScoreCentre Centre => BarShown ? barCentre : ScoreCentre.None;
        public bool ClockCentred => Centre != ScoreCentre.None;
        public string CentreScore => ClockCentred && shownSides ? leftScore.text + " " + ScoreLine.Separator + " " + rightScore.text : "";
        public string CentreCaption => ClockCentred ? centreCaption.text : "";
        public string CentreClock => ClockCentred ? barClock.text : "";
        public string TargetWord => Centre == ScoreCentre.Target ? targetWord : "";
        public int LeadingSide => Centre == ScoreCentre.Target && shownSides ? shownLeadSide : ScoreLine.NoLead;
        public PlayerController LeadingSeat => Centre == ScoreCentre.Target && barLeader >= 0 && barLeader < barSeats.Count ? barSeats[barLeader] : null;

        public void SetSideScores(Func<int, int> scoreOfTeam, string caption = "")
        { sideScore = scoreOfTeam; sideCaption = caption ?? ""; }

        public void SetTarget(string word) => targetWord = word ?? "";

        public static Color SeatColour(PlayerController player) => SeatColour(player, World.Players);

        public static Color SeatColour(PlayerController player, IReadOnlyList<PlayerController> everyone)
        {
            if (!player) return Color.white;
            int slot = ColourSlot(player, everyone);
            return PlayerHud.Vivid(slot >= 0 ? colourOwners[colourFrames[slot]].Color : player.Color);
        }

        public static bool SeatAccented(IReadOnlyList<PlayerController> everyone)
        {
            if (everyone == null) return false;
            Gather(everyone);
            return colourAccented;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSeatColours()
        {
            colourSeats.Clear(); colourOwners.Clear(); colourFrames.Clear();
            colourSource = null; colourFrame = -1; colourAccented = false;
        }

        static int ColourSlot(PlayerController player, IReadOnlyList<PlayerController> everyone)
        {
            if (everyone == null) return -1;
            Gather(everyone);
            return colourOwners.IndexOf(player);
        }

        static void Gather(IReadOnlyList<PlayerController> everyone)
        {
            if (colourFrame == Time.frameCount && ReferenceEquals(colourSource, everyone)) return;
            colourFrame = Time.frameCount; colourSource = everyone;
            colourSeats.Clear(); colourOwners.Clear();
            for (int i = 0; i < everyone.Count; i++)
            {
                var p = everyone[i];
                if (!p) continue;
                colourOwners.Add(p);
                colourSeats.Add(new ColourSeat(p.Index, p.Team, Hue(p.Color)));
            }
            TeamColours.Frames(colourSeats, colourFrames);
            colourAccented = TeamColours.Accented(colourSeats);
        }

        static int Hue(Color colour)
        {
            Color32 vivid = PlayerHud.Vivid(colour);
            return (vivid.r << 16) | (vivid.g << 8) | vivid.b;
        }

        float BarHeight(int cards) => cards > 0 ? PlayerBar.CardHeight(cards, Scored) : ScoreLine.ClockRow;

        public static string SeatLine(int score, int target, string unit) => target > 0 ? ScoreLine.Progress(score, target) : SeatScore(score, 0, unit);

        void BuildBar()
        {
            bar = Rect("Player bar", safe, new Vector2(.5f, 1f), new Vector2(0f, -PlayerBar.Top), new Vector2(0f, PlayerBar.Height));
            bar.pivot = new Vector2(.5f, 1f);
            var backing = CardImage("Backing", bar, Plate);
            Square(backing);
            plate = backing.rectTransform;
            for (int i = 0; i < MaxCards; i++) outlines.Add(MakeOutline($"Side {i + 1}", bar));
            for (int i = 0; i < MaxCards; i++) cards.Add(MakeSeatCard(i));
            BuildCentre();
            BuildScoresButton();
            bar.gameObject.SetActive(false);
        }

        void BuildCentre()
        {
            centre = Rect("Centre", bar, new Vector2(.5f, 1f), Vector2.zero, new Vector2(ScoreLine.CentreWidth, PlayerBar.Height));
            centre.pivot = new Vector2(.5f, 1f);
            centreFill = CreateImage("Fill", centre, Vector2.zero, Vector2.zero, Vector2.zero, CentreFill);
            Stretch(centreFill.rectTransform);
            centreFill.rectTransform.offsetMin = new Vector2(0f, -PlayerBar.Outline);
            centreFill.rectTransform.offsetMax = new Vector2(0f, PlayerBar.Outline);
            Square(centreFill);
            barClock = CentreText("Clock", ClockSize, OnGame);
            Place(barClock.rectTransform, 0f, ScoreLine.CentreWidth, ClockHeight);
            leftScore = CentreText("Left score", ScoreSize, OnGame);
            scoreColon = CentreText("Colon", ScoreSize, OnGame);
            scoreColon.text = ScoreLine.Separator;
            Place(scoreColon.rectTransform, -ScoreTop, ColonWidth, ScoreRowHeight);
            rightScore = CentreText("Right score", ScoreSize, OnGame);
            leftScore.fontStyle = rightScore.fontStyle = scoreColon.fontStyle = FontStyles.Bold;
            targetPicture = Rect("Target", centre, new Vector2(.5f, 1f), new Vector2(0f, -ScoreTop), new Vector2(TargetSide, TargetSide)).gameObject.AddComponent<RawImage>();
            targetPicture.rectTransform.pivot = new Vector2(.5f, 1f);
            targetPicture.raycastTarget = false;
            targetPicture.enabled = false;
            leftLead = LeadMark("Left lead", centre);
            rightLead = LeadMark("Right lead", centre);
            centreCaption = CentreText("Caption", CaptionSize, ScoreInk);
            centreCaption.characterSpacing = 1f;
            Place(centreCaption.rectTransform, -CaptionTop, ScoreLine.CentreWidth, CaptionHeight);
            leadScore = CentreText("Lead score", LeadScoreSize, ScoreInk);
            leadScore.fontStyle = FontStyles.Bold;
            leadScore.enabled = false;
            leftScore.enabled = rightScore.enabled = false;
            laidCentre = ScoreCentre.None;
            centre.gameObject.SetActive(false);
        }

        TextMeshProUGUI CentreText(string label, float size, Color colour)
        {
            var text = CardText(label, centre, size, colour);
            text.enableAutoSizing = true; text.fontSizeMin = CentreMinText; text.fontSizeMax = size;
            OnGameText(text); AddHudShadow(text);
            return text;
        }

        Image LeadMark(string label, RectTransform parent)
        {
            var mark = CardImage(label, parent, Gold);
            mark.type = Image.Type.Simple;
            mark.sprite = LobbyIcons.Get(LobbyIcons.Trophy);
            mark.preserveAspect = true;
            mark.rectTransform.sizeDelta = new Vector2(LeadSize, LeadSize);
            AddHudShadow(mark);
            mark.enabled = false;
            return mark;
        }

        void BuildScoresButton()
        {
            scoresButton = Rect("Scores", bar, new Vector2(.5f, 1f), Vector2.zero, new Vector2(ScoresSize, ScoresSize));
            scoresButton.pivot = new Vector2(0f, 1f);
            var face = Panel("Face", scoresButton, new Vector2(.5f, .5f), Vector2.zero, new Vector2(ScoresSize, ScoresSize), new Vector2(.5f, .5f), Glass, true);
            scoresFace = face.GetComponent<Image>();
            scoresFace.raycastTarget = true;
            var button = scoresButton.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = scoresFace;
            button.onClick.AddListener(ToggleScoreboard);
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            var icon = CreateImage("Icon", face, new Vector2(.5f, .5f), Vector2.zero, new Vector2(ScoresSize * .58f, ScoresSize * .58f), OnGame);
            icon.sprite = LobbyIcons.Get(LobbyIcons.Trophy); icon.preserveAspect = true;
            AddHudShadow(icon);
            var cap = Panel("Key cap", scoresButton, new Vector2(.5f, 0f), new Vector2(0f, -4f), new Vector2(ScoresCapWidth, ScoresCapHeight), new Vector2(.5f, 1f), KeyPill);
            scoresCap = cap.gameObject;
            scoresKey = Text("Key", cap, new Vector2(.5f, .5f), Vector2.zero, new Vector2(ScoresCapWidth - 4f, ScoresCapHeight - 2f), CaptionSize, OnGame);
            if (LobbyFonts.Black) scoresKey.font = LobbyFonts.Black;
            scoresKey.text = "";
            scoresCap.SetActive(false);
        }

        PlayerCard MakeSeatCard(int index)
        {
            var root = Rect($"Card {index + 1}", bar, new Vector2(.5f, 1f), Vector2.zero, new Vector2(PlayerBar.RoomyCard, PlayerBar.Height));
            root.pivot = new Vector2(.5f, 1f);
            var card = new PlayerCard { root = root };
            card.backing = CardImage("Backing", root, CardBacking);
            card.backing.sprite = null;
            card.portrait = Rect("Portrait", root, new Vector2(.5f, 1f), Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>();
            card.portrait.rectTransform.pivot = new Vector2(.5f, 1f);
            card.portrait.raycastTarget = false;
            card.letter = CardText("Letter", root, 38f, Ink);
            card.letter.fontStyle = FontStyles.Bold;
            card.letter.alignment = TextAlignmentOptions.Center;
            card.shade = CardImage("Shade", root, WreckedShade);
            card.shade.sprite = null;
            card.frame = CardImage("Frame", root, Mint);
            SquareFrame(card.frame, PlayerBar.Frame);
            card.cross = CardImage("Cross", root, OnGame);
            card.cross.type = Image.Type.Simple;
            card.cross.sprite = LobbyIcons.Get(LobbyIcons.Close);
            card.cross.preserveAspect = true;
            AddHudShadow(card.cross);
            card.lead = LeadMark("Lead", root);
            card.status = CardText("Status", root, PlayerBar.StatusSize, LowHp);
            card.status.fontStyle = FontStyles.Bold;
            card.status.enableAutoSizing = true; card.status.fontSizeMin = PlayerBar.StatusMin; card.status.fontSizeMax = PlayerBar.StatusSize;
            card.status.lineSpacing = StatusLineSpacing;
            OnGameText(card.status);
            card.vitals = MakeVitals(root, new Vector2(.5f, 1f), PlayerBar.NumberSize);
            card.name = CardText("Name", root, PlayerBar.NameSize, Cream);
            card.name.fontStyle = FontStyles.Bold;
            FitLine(card.name, PlayerBar.NameSize);
            OnGameText(card.name); AddHudShadow(card.name);
            card.name.gameObject.SetActive(false);
            card.score = CardText("Score", root, PlayerBar.ScoreSize, ScoreInk);
            FitLine(card.score, PlayerBar.ScoreSize);
            OnGameText(card.score); AddHudShadow(card.score);
            card.shade.enabled = card.letter.enabled = card.portrait.enabled = card.cross.enabled = false;
            SizeSeatCard(card, PlayerBar.RoomySeats, true);
            root.gameObject.SetActive(false);
            return card;
        }

        static void FitLine(TextMeshProUGUI text, float size)
        {
            text.enableAutoSizing = true; text.fontSizeMin = PlayerBar.TextMin; text.fontSizeMax = size;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        Image MakeOutline(string label, RectTransform parent)
        {
            var outline = CardImage(label, parent, GlassEdge);
            SquareFrame(outline, PlayerBar.Outline);
            outline.enabled = false;
            return outline;
        }

        void SquareFrame(Image image, float width)
        {
            image.sprite = lineBox;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f / width;
        }

        static void Square(Image image)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
        }

        public const string LineBoxName = "HUD line box";

        public static bool Squared(Image image) =>
            image && (!image.sprite || (image.sprite.texture && image.sprite.texture.name == LineBoxName));

        Image PaintSquare(RectTransform rt, Color colour)
        {
            var image = Paint(rt, null, colour);
            Square(image);
            return image;
        }

        Image PaintLine(RectTransform rt, Color colour, float width) => Paint(rt, lineBox, colour, 1f / width);

        VitalsView MakeVitals(RectTransform parent, Vector2 anchor, float numberSize)
        {
            var view = new VitalsView { track = BarTrack("Health track", parent, anchor) };
            view.number = BarNumber(view.track, OnGame, numberSize);
            view.fill = BarFill("Health", view.track, OnGame);
            view.fill.gameObject.AddComponent<RectMask2D>();
            view.numberInk = BarNumber(view.fill.rectTransform, Ink, numberSize);
            view.staminaTrack = BarTrack("Stamina track", parent, anchor);
            view.staminaFill = BarFill("Stamina", view.staminaTrack, Breath);
            return view;
        }

        RectTransform BarTrack(string label, RectTransform parent, Vector2 anchor)
        {
            var image = CreateImage(label, parent, anchor, Vector2.zero, Vector2.zero, HealthTrack);
            image.rectTransform.pivot = anchor;
            Square(image);
            return image.rectTransform;
        }

        Image BarFill(string label, RectTransform track, Color colour)
        {
            var image = CreateImage(label, track, Vector2.zero, Vector2.zero, Vector2.zero, colour);
            image.rectTransform.anchorMax = new Vector2(0f, 1f);
            image.rectTransform.pivot = new Vector2(0f, .5f);
            Square(image);
            return image;
        }

        TextMeshProUGUI BarNumber(RectTransform parent, Color colour, float size)
        {
            var text = Text("Number", parent, new Vector2(0f, .5f), Vector2.zero, Vector2.zero, size, colour, TextAlignmentOptions.MidlineLeft);
            text.rectTransform.pivot = new Vector2(0f, .5f);
            if (LobbyFonts.Black) text.font = LobbyFonts.Black;
            text.enableAutoSizing = true; text.fontSizeMin = PlayerBar.NumberMin; text.fontSizeMax = size;
            text.overflowMode = TextOverflowModes.Overflow;
            text.text = "";
            return text;
        }

        static void LayVitals(VitalsView view, float x, float y, float width, float height, float staminaY, float staminaWidth, float staminaHeight, float pad)
        {
            view.track.anchoredPosition = new Vector2(x, y);
            view.track.sizeDelta = new Vector2(width, height);
            var box = new Vector2(width - 2f * pad, height);
            view.number.rectTransform.anchoredPosition = view.numberInk.rectTransform.anchoredPosition = new Vector2(pad, 0f);
            view.number.rectTransform.sizeDelta = view.numberInk.rectTransform.sizeDelta = box;
            view.staminaTrack.anchoredPosition = new Vector2(x + (staminaWidth - width) * view.track.pivot.x, staminaY);
            view.staminaTrack.sizeDelta = new Vector2(staminaWidth, staminaHeight);
            view.width = width;
            view.staminaWidth = staminaWidth;
            view.shownFill = view.shownBreath = float.NaN;
        }

        static void ShowVitals(VitalsView view, PlayerHealth health, bool countdown)
        {
            bool alive = health && health.IsAlive;
            float share = health ? SeatVitals.Share(health.Current, health.Max) : 0f;
            Widen(view.fill.rectTransform, view.width * share, ref view.shownFill);
            int band = (int)SeatVitals.Band(share);
            if (band != view.shownBand) { view.shownBand = band; view.fill.color = BandColour((HealthBand)band); }
            int reading = health ? SeatVitals.Reading(health.Current, alive, health.IsDowned, health.BleedOutLeft, countdown) : SeatVitals.Hidden;
            if (reading != view.shownReading) { view.shownReading = reading; view.number.text = view.numberInk.text = SeatVitals.Label(reading); }
            bool breath = health && SeatVitals.ShowsStamina(alive);
            if (view.staminaTrack.gameObject.activeSelf != breath) view.staminaTrack.gameObject.SetActive(breath);
            if (!breath) return;
            Widen(view.staminaFill.rectTransform, view.staminaWidth * SeatVitals.Share(health.Stamina, health.Model.MaxStamina), ref view.shownBreath);
            int winded = SeatVitals.Winded(health.Stamina, health.Rules.DodgeStamina) ? 1 : 0;
            if (winded != view.shownWinded) { view.shownWinded = winded; view.staminaFill.color = winded == 1 ? Amber : Breath; }
        }

        static void Widen(RectTransform rt, float width, ref float shown)
        {
            if (width == shown) return;
            shown = width;
            rt.sizeDelta = new Vector2(width, 0f);
        }

        static Color BandColour(HealthBand band) => band switch
        {
            HealthBand.Critical => Low, HealthBand.Hurt => Amber, _ => OnGame,
        };

        Image CardImage(string label, RectTransform parent, Color colour)
        {
            var image = CreateImage(label, parent, new Vector2(.5f, 1f), Vector2.zero, Vector2.zero, colour);
            image.rectTransform.pivot = new Vector2(.5f, 1f);
            image.type = Image.Type.Sliced;
            return image;
        }

        TextMeshProUGUI CardText(string label, RectTransform parent, float size, Color colour)
        {
            var text = Text(label, parent, new Vector2(.5f, 1f), Vector2.zero, Vector2.zero, size, colour);
            text.rectTransform.pivot = new Vector2(.5f, 1f);
            return text;
        }

        static void Place(RectTransform rt, float y, float width, float height)
        {
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(width, height);
        }

        static void SizeSeatCard(PlayerCard card, int seats, bool scored)
        {
            int shape = seats * 2 + (scored ? 1 : 0);
            if (card.shownSeats == shape) return;
            card.shownSeats = shape;
            float width = PlayerBar.CardWidth(seats), side = PlayerBar.Portrait(seats), rim = PlayerBar.Frame;
            card.root.sizeDelta = new Vector2(width, PlayerBar.CardHeight(seats, scored));
            Place(card.backing.rectTransform, 0f, side, side);
            Place(card.portrait.rectTransform, -rim, side - 2f * rim, side - 2f * rim);
            Place(card.letter.rectTransform, 0f, side, side);
            card.letter.fontSize = side * .56f;
            Place(card.shade.rectTransform, -rim, side - 2f * rim, side - 2f * rim);
            Place(card.frame.rectTransform, 0f, side, side);
            float cross = side * CrossShare;
            Place(card.cross.rectTransform, -(side - cross) * .5f, cross, cross);
            card.lead.rectTransform.anchoredPosition = new Vector2(side * .5f - LeadSize * .5f, LeadSize * .25f);
            Place(card.status.rectTransform, -rim, width - 4f, side - 2f * rim);
            LayVitals(card.vitals, 0f, -PlayerBar.HealthY(seats), PlayerBar.HealthWidth(seats), PlayerBar.HealthHeight,
                -PlayerBar.StaminaY(seats), PlayerBar.StaminaWidth(seats), PlayerBar.StaminaHeight, PlayerBar.NumberPad);
            Place(card.name.rectTransform, -PlayerBar.ScoreY(seats), width, PlayerBar.NameHeight);
            Place(card.score.rectTransform, -PlayerBar.ScoreY(seats), width, PlayerBar.ScoreHeight);
        }

        void RefreshCards()
        {
            var current = Roster;
            barSeats.Clear(); barTeams.Clear();
            int seats = 0, humans = 0;
            if (current != null)
                for (int i = 0; i < current.Count; i++)
                {
                    var p = current[i];
                    if (!p) continue;
                    seats++;
                    if (SeatHuman(p)) humans++;
                    if (barSeats.Count < MaxCards) { barSeats.Add(p); barTeams.Add(p.Team); }
                }
            barSeated = PlayerBar.Visible(seats);
            barCentre = ScoreLine.Centre(barSeated ? barTeams : NoTeams, Scored, targetWord.Length > 0, !string.IsNullOrEmpty(timerText));
            bool shown = barSeated || barCentre == ScoreCentre.Clock;
            if (!shown) barCentre = ScoreCentre.None;
            if (bar.gameObject.activeSelf != shown) bar.gameObject.SetActive(shown);
            if (shown) LayOutBar();
            SetTopInset(!shown ? 0f : PlayerBar.ShiftFor(barTop, barScale, barSeated && Scored ? PlayerBar.Height : BarHeight(barSeated ? barSize : 0)));
            PlaceClock();
            if (!shown) return;
            bool drew = false;
            for (int slot = 0; slot < cards.Count; slot++)
            {
                var card = cards[slot];
                bool used = barSeated && slot < barSeats.Count;
                if (card.root.gameObject.activeSelf != used) card.root.gameObject.SetActive(used);
                if (used) ShowSeat(card, barSeats[barOrder[slot]], humans, ref drew);
            }
            ColourOutlines();
            ReadScores();
            RefreshCentre();
            MarkLeader();
            RefreshScoresKey();
        }

        bool Scored => showWins && wins != null;

        void ReadScores()
        {
            barScores.Clear();
            bool scored = Scored;
            for (int i = 0; i < barSeats.Count; i++) barScores.Add(scored ? wins(barSeats[i]) : 0);
        }

        int SideScore(int seat) => sideScore != null ? sideScore(barSeats[seat].Team) : ScoreLine.Best(barTeams, barScores, seat);

        void PlaceClock()
        {
            if (timer)
            {
                var pill = timer.transform.parent.gameObject;
                bool beside = !string.IsNullOrEmpty(timerText) && !bar;
                if (pill.activeSelf != beside) pill.SetActive(beside);
            }
            if (!barClock || ReferenceEquals(timerText, shownClock)) return;
            shownClock = timerText;
            barClock.text = timerText ?? "";
        }

        bool CentreLeads => Scored && barSeated && (barCentre == ScoreCentre.Clock || barCentre == ScoreCentre.Target) && !ScoreLine.TwoSides(barTeams);

        void PlaceBarClock()
        {
            bool alone = laidCentre == ScoreCentre.Clock;
            float tall = BarHeight(barSize) - (laidLead ? LeadRow : 0f);
            Place(barClock.rectTransform, 0f, alone ? ScoreLine.ClockWidth : ScoreLine.CentreWidth, alone ? tall : ClockHeight);
            barClock.fontSizeMax = alone ? ClockBig : ClockSize;
            barClock.fontStyle = alone ? FontStyles.Bold : FontStyles.Normal;
        }

        void LayCentre()
        {
            laidCentre = barCentre;
            laidLead = CentreLeads;
            PlaceBarClock();
            float tall = BarHeight(barSize);
            leadScore.enabled = laidLead;
            Place(leadScore.rectTransform, -(tall - LeadRow), barCentre == ScoreCentre.Clock ? ScoreLine.ClockWidth : ScoreLine.CentreWidth, LeadRow);
            shownBest = shownBestOf = int.MinValue;
            float captionTop = laidLead ? tall - LeadRow - CaptionHeight : CaptionTop;
            Place(centreCaption.rectTransform, -captionTop, ScoreLine.CentreWidth, CaptionHeight);
            float picture = Mathf.Clamp(captionTop - ScoreTop, 0f, TargetSide);
            Place(targetPicture.rectTransform, -ScoreTop, picture, picture);
            if (barCentre == ScoreCentre.Clock)
            {
                leftScore.enabled = rightScore.enabled = scoreColon.enabled = targetPicture.enabled = false;
                leftLead.enabled = rightLead.enabled = false;
                shownSides = false;
                shownTarget = null;
                shownLeft = shownRight = shownTotal = shownLeadSide = int.MinValue;
                return;
            }
            bool target = barCentre == ScoreCentre.Target;
            float box = target ? (ScoreLine.CentreWidth - TargetSide) * .5f - TargetGap : (ScoreLine.CentreWidth - ColonWidth) * .5f;
            float reach = target ? (TargetSide + box) * .5f + TargetGap : (ColonWidth + box) * .5f;
            float top = target ? ProgressTop : ScoreTop, height = target ? ProgressHeight : ScoreRowHeight, size = target ? ProgressSize : ScoreSize;
            LaySide(leftScore, -reach, top, box, height, size, target ? TextAlignmentOptions.Center : TextAlignmentOptions.Right);
            LaySide(rightScore, reach, top, box, height, size, target ? TextAlignmentOptions.Center : TextAlignmentOptions.Left);
            leftLead.rectTransform.anchoredPosition = new Vector2(-reach, -ScoreTop);
            rightLead.rectTransform.anchoredPosition = new Vector2(reach, -ScoreTop);
            leftLead.enabled = rightLead.enabled = false;
            scoreColon.enabled = !target;
            if (!target) targetPicture.enabled = false;
            shownTarget = null;
            shownLeft = shownRight = shownTotal = shownLeadSide = int.MinValue;
        }

        static void LaySide(TextMeshProUGUI text, float x, float top, float width, float height, float size, TextAlignmentOptions alignment)
        {
            text.rectTransform.anchoredPosition = new Vector2(x, -top);
            text.rectTransform.sizeDelta = new Vector2(width, height);
            text.fontSizeMax = size;
            text.alignment = alignment;
        }

        void RefreshCentre()
        {
            bool on = barCentre != ScoreCentre.None;
            if (centre.gameObject.activeSelf != on) centre.gameObject.SetActive(on);
            if (!on) return;
            if (barCentre != laidCentre || CentreLeads != laidLead) LayCentre();
            if (laidLead) ShowBest();
            bool target = barCentre == ScoreCentre.Target;
            bool sides = barCentre != ScoreCentre.Clock && Scored && ScoreLine.TwoSides(barTeams);
            if (sides != shownSides)
            {
                shownSides = sides;
                leftScore.enabled = rightScore.enabled = sides;
                leftLead.enabled = rightLead.enabled = false;
                shownLeft = shownRight = shownTotal = shownLeadSide = int.MinValue;
            }
            if (sides) ShowSides(target);
            string caption = target ? targetWord : barCentre == ScoreCentre.Score ? sideCaption : "";
            if (!ReferenceEquals(caption, shownCaption)) { shownCaption = caption; centreCaption.text = caption; }
            if (!target || ReferenceEquals(targetWord, shownTarget)) return;
            shownTarget = targetWord;
            Wreckabulary.UI.ItemArt.Apply(targetPicture, targetWord);
        }

        void ShowBest()
        {
            int best = 0;
            for (int i = 0; i < barScores.Count; i++) best = Mathf.Max(best, barScores[i]);
            int leader = ScoreLine.Leader(barScores);
            var colour = leader >= 0 && leader < barSeats.Count ? SeatColour(barSeats[leader]) : ScoreInk;
            if (best != shownBest || roundsToWin != shownBestOf)
            {
                shownBest = best; shownBestOf = roundsToWin;
                leadScore.text = SeatLine(best, roundsToWin, scoreUnit);
            }
            if (colour != shownBestColour) leadScore.color = shownBestColour = colour;
        }

        void ShowSides(bool progress)
        {
            int first = barOrder[0], second = barOrder[barSplit];
            int left = SideScore(first), right = SideScore(second), total = progress ? roundsToWin : 0;
            bool restyled = total != shownTotal || progress != shownProgress;
            if (restyled || left != shownLeft) leftScore.text = ScoreLine.Value(left, total, progress);
            if (restyled || right != shownRight) rightScore.text = ScoreLine.Value(right, total, progress);
            shownLeft = left; shownRight = right; shownTotal = total; shownProgress = progress;
            Color leftColour = SeatColour(barSeats[first]), rightColour = SeatColour(barSeats[second]);
            if (leftColour != shownLeftColour) leftScore.color = shownLeftColour = leftColour;
            if (rightColour != shownRightColour) rightScore.color = shownRightColour = rightColour;
            int lead = progress ? ScoreLine.Lead(left, right) : ScoreLine.NoLead;
            if (lead == shownLeadSide) return;
            shownLeadSide = lead;
            leftLead.enabled = lead == ScoreLine.Left;
            rightLead.enabled = lead == ScoreLine.Right;
        }

        void MarkLeader()
        {
            bool target = barCentre == ScoreCentre.Target && Scored, marks = target && !shownSides;
            barLeader = !target ? ScoreLine.NoLead
                : marks ? ScoreLine.Leader(barScores)
                : shownLeadSide == ScoreLine.Left ? barOrder[0] : shownLeadSide == ScoreLine.Right ? barOrder[barSplit] : ScoreLine.NoLead;
            for (int slot = 0; slot < barSeats.Count; slot++)
            {
                var card = cards[slot];
                bool leads = marks && barOrder[slot] == barLeader;
                if (leads == card.shownLead) continue;
                card.shownLead = leads;
                card.lead.enabled = leads;
            }
        }

        void RefreshScoresKey()
        {
            bool offered = !hubScene && barSeated;
            if (scoresButton.gameObject.activeSelf != offered) scoresButton.gameObject.SetActive(offered);
            scoresFace.color = ScoreboardShown ? GlassActive : Glass;
            var binding = LocalPlayer ? LocalPlayer.Binding : null;
            bool touch = TouchControlsShown;
            if (scoresKeySet && ReferenceEquals(binding, scoresKeyFor) && touch == scoresKeyTouch) return;
            scoresKeySet = true; scoresKeyFor = binding; scoresKeyTouch = touch;
            string key = touch ? "" : ScoreboardKeyLabel(binding);
            scoresKey.text = key;
            scoresKey.fontSize = key.Length == 1 ? ScoresGlyphSize : CaptionSize;
            scoresCap.SetActive(key.Length > 0);
        }

        void LayOutBar()
        {
            int count = barSeated ? barSeats.Count : 0;
            float centreWidth = ScoreLine.Width(barCentre);
            float width = PlayerBar.Strip(barSeated ? barTeams : NoTeams, barOrder, barCentres, centreWidth, out barSplit, out float centreX);
            for (int slot = 0; slot < count; slot++)
            {
                var card = cards[slot];
                SizeSeatCard(card, count, Scored);
                if (card.shownX == barCentres[slot]) continue;
                card.shownX = barCentres[slot];
                card.root.anchoredPosition = new Vector2(card.shownX, 0f);
            }
            float reach = count > 0 ? ScoresReach : 0f;
            float scale = PlayerBar.Scale(width + 2f * reach, safe.rect.width);
            float height = BarHeight(count);
            CollectBlocks();
            float top = ClearBarTop(width, height, scale, reach);
            int joins = JoinMask(count);
            if (width == barWidth && scale == barScale && top == barTop && count == barSize && centreWidth == barCentreWidth && joins == barJoins && centreX == barCentreX && height == barTall) return;
            barWidth = width; barScale = scale; barTop = top; barSize = count; barCentreWidth = centreWidth; barJoins = joins; barCentreX = centreX; barTall = height;
            LayOutlines(count);
            bar.sizeDelta = new Vector2(width, height);
            plate.anchoredPosition = new Vector2(0f, PlayerBar.PlatePad);
            plate.sizeDelta = new Vector2(PlayerBar.PlateWidth(width), height + 2f * PlayerBar.PlatePad);
            bar.localScale = new Vector3(scale, scale, 1f);
            bar.anchoredPosition = new Vector2(0f, -top);
            centre.sizeDelta = new Vector2(Mathf.Max(centreWidth, ScoreLine.ClockWidth), height);
            centre.anchoredPosition = new Vector2(centreX, 0f);
            scoresButton.anchoredPosition = new Vector2(width * .5f + PlayerBar.PlatePad + ScoresGap, PlayerBar.PlatePad);
            if (barCentre != ScoreCentre.None) LayCentre();
        }

        int JoinMask(int count)
        {
            int mask = barSplit << MaxCards;
            for (int slot = 1; slot < count; slot++)
                if (PlayerBar.Joined(barTeams, barOrder, slot, barSplit)) mask |= 1 << slot;
            return mask;
        }

        void LayOutlines(int count)
        {
            groupCount = 0;
            float half = PlayerBar.CardWidth(count) * .5f, tall = PlayerBar.BoxHeight(count, Scored);
            for (int slot = 0; slot < count; slot++)
            {
                if (slot > 0 && PlayerBar.Joined(barTeams, barOrder, slot, barSplit)) { groupLast[groupCount - 1] = slot; continue; }
                groupFirst[groupCount] = groupLast[groupCount] = slot;
                groupCount++;
            }
            for (int group = 0; group < outlines.Count; group++)
            {
                var outline = outlines[group];
                bool used = group < groupCount;
                if (outline.enabled != used) outline.enabled = used;
                if (!used) continue;
                float left = barCentres[groupFirst[group]] - half - PlayerBar.Outline, right = barCentres[groupLast[group]] + half + PlayerBar.Outline;
                outline.rectTransform.anchoredPosition = new Vector2((left + right) * .5f, PlayerBar.Outline);
                outline.rectTransform.sizeDelta = new Vector2(right - left, tall);
            }
        }

        void ColourOutlines()
        {
            for (int group = 0; group < groupCount; group++)
            {
                var colour = GroupColour(groupFirst[group], groupLast[group]);
                if (outlines[group].color != colour) outlines[group].color = colour;
            }
        }

        Color GroupColour(int first, int last)
        {
            bool wrecked = true;
            for (int slot = first; slot <= last; slot++) wrecked &= barSeats[barOrder[slot]].IsEliminated;
            return wrecked ? WreckedFrame : SeatColour(barSeats[barOrder[first]]);
        }

        public void CollectHudRects(List<Rect> into)
        {
            if (into == null || !safe || StateController.CurrentState != UIState.GameplayHUD) return;
            ScreenRect(into, brandRect);
            if (labelRect && labelRect.gameObject.activeInHierarchy && labelWidth > 0f && ScreenRect(into, labelRect))
            {
                var label = into[into.Count - 1];
                float share = labelRect.rect.width > 0f ? Mathf.Clamp01(labelWidth / labelRect.rect.width) : 1f;
                into[into.Count - 1] = new Rect(label.xMin, label.yMin, label.width * share, label.height);
            }
            if (BarShown)
            {
                ScreenRect(into, plate);
                ScreenRect(into, scoresButton);
                ScreenRect(into, scoresCap ? (RectTransform)scoresCap.transform : null);
            }
            ScreenRect(into, minimapRect);
            ScreenRect(into, eyeRect);
            ScreenRect(into, eyeRect ? eyeRect.Find("Key cap") as RectTransform : null);
            ScreenRect(into, aliveText ? (RectTransform)aliveText.transform.parent : null);
            ScreenRect(into, timer ? (RectTransform)timer.transform.parent : null);
            ScreenRect(into, roomBacking);
            ScreenRect(into, objective ? (RectTransform)objective.transform.parent : null);
            ScreenRect(into, GoalPlateShown ? GoalPlate : null);
            ScreenRect(into, safe.Find(ShoppingListHud.RootName) as RectTransform);
            ScreenRect(into, tray);
            ScreenRect(into, trayScroll);
            ScreenRect(into, spellHint ? spellHint.rectTransform : null);
            ScreenRect(into, vitals);
            ScreenRect(into, effects ? effects.rectTransform : null);
            ScreenRect(into, status);
            ScreenRect(into, chip);
            ScreenRect(into, banner);
            ScreenRect(into, toast);
            ScreenRect(into, safe.Find(WarmupGuideStrip.RootName) as RectTransform);
        }

        bool ScreenRect(List<Rect> into, RectTransform rt)
        {
            if (!rt || !rt.gameObject.activeInHierarchy) return false;
            rt.GetWorldCorners(barCorners);
            var eye = UiCanvas && UiCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? UiCanvas.worldCamera : null;
            float left = float.MaxValue, right = float.MinValue, low = float.MaxValue, high = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                var point = RectTransformUtility.WorldToScreenPoint(eye, barCorners[i]);
                left = Mathf.Min(left, point.x); right = Mathf.Max(right, point.x);
                low = Mathf.Min(low, point.y); high = Mathf.Max(high, point.y);
            }
            into.Add(new Rect(left, low, right - left, high - low));
            return true;
        }

        float ClearBarTop(float width, float height, float scale, float reach)
        {
            float shift = reach * .5f * scale;
            barBlocksShifted.Clear();
            for (int i = 0; i < barBlocks.Count; i++)
            {
                var block = barBlocks[i];
                barBlocksShifted.Add(new BarBlock(block.Left - shift, block.Right - shift, block.Top, block.Bottom));
            }
            return PlayerBar.ClearTop(width * scale * .5f + shift, height * scale, barBlocksShifted, PlayerBar.PlatePad * scale);
        }

        void ShowSeat(PlayerCard card, PlayerController p, int humans, ref bool drew)
        {
            if (card.seat != p)
            {
                card.seat = p;
                card.face = SeatPortrait.For(p);
                card.letter.text = PlayerBar.Letter(p.Initial);
                card.shownName = null; card.shownWins = card.shownStatus = card.shownLook = int.MinValue;
                card.shownPicture = null;
            }
            bool you = p == LocalPlayer;
            string name = SeatTags.Name(p.Name, you, humans);
            if (name != card.shownName) { card.shownName = name; card.name.text = name; }
            int won = showWins && wins != null ? wins(p) : -1;
            if (won != card.shownWins || roundsToWin != card.shownTarget || !ReferenceEquals(scoreUnit, card.shownUnit))
            {
                card.shownWins = won; card.shownTarget = roundsToWin; card.shownUnit = scoreUnit;
                bool scored = won >= 0;
                card.score.text = scored ? SeatLine(won, roundsToWin, scoreUnit) : "";
                if (card.score.gameObject.activeSelf != scored) card.score.gameObject.SetActive(scored);
            }
            ShowVitals(card.vitals, p.Health, false);
            int status = p.IsEliminated ? -1 : p.IsDowned ? Mathf.CeilToInt(p.Health.BleedOutLeft) : -2;
            if (status != card.shownStatus)
            {
                card.shownStatus = status;
                card.status.text = PlayerBar.Status(status == -1, status >= 0, status);
            }
            Texture picture = null;
            if (card.face)
            {
                bool render = !drew && card.face.Wants;
                picture = card.face.Refresh(render);
                drew |= render;
            }
            int look = status == -1 ? 2 : status >= 0 ? 1 : 0;
            var vivid = SeatColour(p);
            var nameColour = SeatAccented(World.Players) ? PlayerHud.Vivid(p.Color) : Cream;
            if (nameColour != card.nameColour) card.name.color = card.nameColour = nameColour;
            if (look == card.shownLook && picture == card.shownPicture && vivid == card.vivid) return;
            card.shownLook = look; card.shownPicture = picture; card.vivid = vivid;
            card.cross.enabled = look == 2;
            bool drawn = picture;
            card.portrait.texture = picture;
            card.portrait.enabled = drawn;
            card.portrait.color = look == 2 ? WreckedTint : Color.white;
            card.letter.enabled = !drawn;
            card.letter.color = look == 2 ? Hex(0x2a3333) : Ink;
            card.backing.color = drawn ? CardBacking : look == 2 ? WreckedFrame : vivid;
            card.shade.enabled = look != 0;
            card.shade.color = look == 2 ? WreckedShade : DownShade;
            card.frame.color = look == 2 ? WreckedFrame : vivid;
            card.pulsing = look == 1;
        }

        void PulseCards()
        {
            if (!BarShown) return;
            float wave = .5f + .5f * Mathf.Sin(Time.unscaledTime * 7f);
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card.pulsing && card.root.gameObject.activeSelf) card.frame.color = Color.Lerp(card.vivid, PulseFrame, wave);
            }
        }

        void MeasureLabel()
        {
            float widest = 0f;
            foreach (var text in new[] { matchTag, matchTitle, matchDetail })
                if (text && text.text.Length > 0) widest = Mathf.Max(widest, text.GetPreferredValues(text.text).x);
            labelWidth = Mathf.Min(LabelWidth, widest);
        }

        void CollectBlocks()
        {
            barBlocks.Clear();
            Block(brandRect);
            if (labelRect && labelRect.gameObject.activeInHierarchy && labelWidth > 0f)
            {
                Block(labelRect);
                var label = barBlocks[barBlocks.Count - 1];
                barBlocks[barBlocks.Count - 1] = new BarBlock(label.Left, label.Left + labelWidth, label.Top, label.Bottom);
            }
            Block(minimapRect);
            Block(eyeRect);
            if (!TouchControlsShown || !touchRoot.TryGetComponent(out TouchLayoutView touch)) return;
            for (var part = TouchPart.Move; part <= TouchPart.Eye; part++)
            {
                var rt = touch.Part(part);
                if (rt) Block(rt);
            }
        }

        void Block(RectTransform rt)
        {
            if (!rt || !rt.gameObject.activeInHierarchy) return;
            rt.GetWorldCorners(barCorners);
            var area = safe.rect;
            float left = float.MaxValue, right = float.MinValue, low = float.MaxValue, high = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                var local = safe.InverseTransformPoint(barCorners[i]);
                left = Mathf.Min(left, local.x); right = Mathf.Max(right, local.x);
                low = Mathf.Min(low, local.y); high = Mathf.Max(high, local.y);
            }
            float middle = area.center.x;
            barBlocks.Add(new BarBlock(left - middle, right - middle, area.yMax - high, area.yMax - low));
        }

        bool OverlapsLeftColumn()
        {
            float half = (barWidth * .5f + PlayerBar.PlatePad) * barScale, columnRight = BrandX + 280f - safe.rect.width * .5f;
            return -half < columnRight;
        }

        void SetTopInset(float inset)
        {
            if (Mathf.Approximately(inset, topInset)) return;
            topInset = inset;
            PlaceTop();
        }

        void PlaceTop()
        {
            bool portrait = lastWidth < lastHeight;
            if (chip) chip.anchoredPosition = new Vector2(0f, ChipY - topInset);
            if (banner) banner.anchoredPosition = new Vector2(0f, BannerY - topInset);
            if (status) status.anchoredPosition = new Vector2(0f, (portrait ? PortraitStatusY : StatusY) - topInset);
            if (hint) hint.anchoredPosition = new Vector2(0f, (portrait ? PortraitStatusY : StatusY) - topInset);
            if (playRect) playRect.anchoredPosition = portrait ? new Vector2(30f, 340f) : new Vector2(0f, PlayY - topInset);
            if (modeActions) ((RectTransform)modeActions.transform).anchoredPosition = new Vector2(0f, ModeActionsY - topInset);
        }
    }
}
