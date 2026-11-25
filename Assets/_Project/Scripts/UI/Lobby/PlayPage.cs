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
    public sealed partial class PlayPage : LobbyPage
    {
        public override string Id => LobbyMenu.Play;
        public string Aim;
        public bool WordboundSolo;
        public bool WordboundMaps;
        LobbyPlayPageView authoredFrame;
        LobbyPlayLayout playLayout;
        RectTransform framePanel;
        public override LobbyStage.Focus Focus => LobbyStage.Focus.Centre;
        public override float PanelWidth => framePanel && framePanel.rect.width > 0 ? framePanel.rect.width : base.PanelWidth;

        public const string SavedHomeCard = "Saved home ", SelectedMark = "Selected";
        public const string ModeBoard = "Modes", ModeSections = "Sections";
        public const string KeepOneMap = "Keep at least one map ticked.";
        public const string GoalLine = "Mode goal", AccessLine = "Mode access", PlayOnline = "Play online";
        public const string NameScrim = "Map name scrim";
        public const string GuideIcon = "Guide icon", GuideHint = "How to play";
        public const string GuideStrip = "How to play strip", GuideStripTitle = "How to play title", GuideStep = "How to play step ";
        public const string GuideStepTitle = "Step title", GuideStepLine = "Step line", LastMatchLine = "Last match";
        public const string MapEditorTitle = "Map editor", MapEditorNote = "One-player playtests.";
        public const string ModeEmblem = "Mode emblem", SeatTag = "Seat tag", StartTag = "Start here tag";
        public const string MapsRow = "Maps row", AnyHouse = "Any house", Houses = "Houses", AnyHouseHint = "Each match picks one of the houses";
        public const string TutorialRoom = "Tutorial room", TutorialRow = "Tutorial maps";
        public const string CreateRow = "Create tools", CreateHouse = "Create house", CreateEditor = "Create Wordbound";
        public const string ToolTitle = "Tool title", ToolLine = "Tool line";
        public const string MapEditorLine = "Build course maps and test them on your own";
        public const string MapEditorMissing = "The map editor is unavailable in this build.";
        const string MapEditorArt = "TABLE", TutorialLine = "Learn the game in two minutes";
        public const string ModeUnderline = "Underline", ModeName = "Mode name";
        const float ModeArt = 28f, TagSize = 15f, TagHeight = 24f, TagSlack = 4f, ClosedAlpha = .5f;
        const float TilePad = 8f, TileArtTop = 8f, TileArtGap = 4f, TileTagRoom = 28f, TileName = 18f, TileFit = 16f, NameSlack = 6f, UnderlineInset = 14f, UnderlineHeight = 3f;
        const int TileRadius = 14;
        const float ChipHeight = 44f, ChipIcon = 20f, ChipGap = 8f, ChipText = 18f, MapsRowGap = 16f;
        const int ChipRadius = 22, ChipPadLeft = 14, ChipPadRight = 18;
        const float ToolTitleSize = 28f, ToolLineSize = 18f, ToolInset = 16f, ToolArt = 240f;
        const int PictureRadius = 10, GuideRadius = 13, StepRadius = 14;
        const float GuideInset = 9f;
        const float StripHead = 32f, StripGap = 8f, StepSpacing = 12f, StepPadding = 8f, StepArt = 56f, StepTitle = 22f, StepLineGap = 0f, StepMinimumWidth = 420f;
        const float StripLowest = 120f, StripTallest = 168f;
        const float TitleInset = 12f;
        const float SmallestLabel = 15f;
        const int WidestMapRow = 5, HouseRow = 6;

        static readonly (string id, string label)[] Queues =
        {
            ("Queue " + LobbyMenu.Practice, "Same screen"),
            ("Queue " + LobbyMenu.Matchmaking, "Online"),
            ("Queue " + LobbyMenu.Workshop, "Create"),
        };

        string DisplayQueue => Online ? LobbyMenu.Matchmaking : Menu.Queue;

        protected override void Build()
        {
            authoredFrame = null; framePanel = null;
            if (Menu.PartySize > 1) WordboundSolo = false;
            var saved = Resources.Load<LobbyPlayLayout>(LobbyPlayLayout.ResourcePath);
            playLayout = saved;
            RectTransform body;
            if (saved)
            {
                saved.Validate();
                authoredFrame = LobbyRuntimeTypography.Instantiate(saved.Page, Root, false);
                authoredFrame.BindOptions(Menu.Close, QueueLine(DisplayQueue));
                body = authoredFrame.Body;
            }
            else body = Panel(Vector2.zero, Vector2.one, "Play", QueueLine(DisplayQueue));
            framePanel = authoredFrame ? authoredFrame.Panel : (RectTransform)body.parent;
            var queues = LobbyKit.Segmented(authoredFrame ? authoredFrame.Queues : body.parent, "Queues", Queues, "Queue " + DisplayQueue, PickQueue, 52, 20);
            if (authoredFrame) queues.Fill();
            else queues.Place(Vector2.one, Vector2.one, new Vector2(-688, -78), new Vector2(-88, -26));

            if (Menu.Queue == LobbyMenu.Matchmaking && !Online)
            {
                Footer(body);
                First = queues.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == "Queue " + LobbyMenu.Practice);
                return;
            }

            var column = authoredFrame ? authoredFrame.Choices : LobbyKit.Column(body, "Choices", 8).Place(Vector2.zero, Vector2.one, new Vector2(0, 104), Vector2.zero);
            column = ScrollableChoices(column);
            if (Online)
            {
                if (Friends) FriendsChoices(column, body);
                else MatchmakingChoices(column, body);
                return;
            }
            if (DisplayQueue == LobbyMenu.Workshop)
            {
                CreateTools(column, body);
                return;
            }
            bool oneStep = !WordboundSolo && (Menu.Mode == LobbyMenu.TutorialMode || HouseModes.FixedHouse(Menu.Mode) != null);
            if (Aim != null && Aim.StartsWith(SavedHomeCard)) housesStep = true;
            bool placeStep = housesStep && !oneStep;
            bool houseStep = placeStep && !WordboundSolo;
            if (!placeStep) PracticeModes(column);
            if (WordboundSolo)
            {
                if (placeStep) HouseStepHeader(column, CourseStepTitle);
                SoloChoices(column, body, placeStep);
                return;
            }
            if (houseStep)
            {
                HouseStepHeader(column, HouseStepTitle);
                MapsHeader(column);
                var houses = MapCards(column, Houses);
                FillRow(houses);
                foreach (var house in GameConfig.Current.Houses)
                    HouseCard(houses, house.Key, house.Value);
                if (HomeDesigner.Playable(Menu.Mode))
                    foreach (var home in HomeStorage.ForGame().Saved()) HomeCard(houses, home);
            }
            Footer(body, houseStep || oneStep);

            if (Aim != null)
            {
                var aimed = Root.GetComponentsInChildren<Selectable>().FirstOrDefault(s => s.name == Aim && s.IsInteractable());
                if (aimed) First = aimed;
                Aim = null;
            }
        }

        void SoloChoices(RectTransform column, RectTransform body, bool mapStep)
        {
            if (mapStep)
            {
                var maps = MapCards(column, "Solo maps");
                FillRow(maps);
                foreach (string map in SoloMaps) SoloMapCard(maps, map, SoloMapName(map), LobbyMapSnapshots.Trait(map));
            }
            var bar = SummaryFrame(body, "Solo summary");
            SummaryLine(bar);
            string blocked = Menu.PartySize != 1 ? "One player only. Remove the extra seats to start." :
                !Session.CanLoad(Session.WordboundScene) ? "Courses are unavailable in this build." : null;
            var heading = LobbyKit.Text(bar, CourseTitle, 26, LobbyKit.Navy, TextAlignmentOptions.BottomLeft);
            heading.enableAutoSizing = true; heading.fontSizeMin = 20; heading.fontSizeMax = 26;
            heading.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 44), new Vector2(-250, -8));
            var detail = LobbyKit.Text(bar, blocked ?? "1 player", 18,
                blocked == null ? LobbyKit.Muted : LobbyKit.Hot, TextAlignmentOptions.TopLeft);
            detail.name = "Solo detail";
            detail.enableAutoSizing = true; detail.fontSizeMin = 14; detail.fontSizeMax = 18;
            detail.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 10), new Vector2(-250, -48));
            Action action = mapStep ? () => Menu.OpenWordboundSolo(WordboundSoloEntry.SelectedMode) : ShowPlaces;
            var go = LobbyKit.Primary(bar, "Solo GO", mapStep ? "Play" : NextWord, action, 42, 26);
            ((RectTransform)go.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-220, -30), new Vector2(-8, 30));
            go.interactable = blocked == null;
        }

        void CreateTools(RectTransform column, RectTransform body)
        {
            var row = LobbyKit.Rect(column, CreateRow);
            var fit = row.gameObject.AddComponent<LobbyPlayToolRow>();
            bool editor = Session.CanLoad(Session.WordboundScene);
            var house = ToolCard(row, CreateHouse, LobbyMenu.ModeName(LobbyMenu.WorkshopMode), LobbyPlayModeLabels.Goal(LobbyMenu.WorkshopMode),
                LobbyMapSnapshots.For(Menu.Map), LobbyKit.ModeArt(LobbyMenu.WorkshopMode), OpenHomeBuilder, true);
            var maps = ToolCard(row, CreateEditor, MapEditorTitle, editor ? MapEditorLine : MapEditorMissing, null, MapEditorArt, OpenMapEditor, editor);
            fit.Fit();
            if (WordboundMaps) WorkshopFooter(body, editor);
            else Footer(body);
            First = WordboundMaps && editor ? maps : house;
        }

        Button ToolCard(RectTransform row, string name, string title, string line, Sprite picture, string art, Action open, bool available)
        {
            var card = MapCard(row, name, false, open);
            card.interactable = available;
            var area = LobbyKit.Rect(card.Body(), "Map picture").Place(Vector2.zero, Vector2.one,
                new Vector2(LobbyPlayToolRow.Inset, LobbyPlayToolRow.Caption), new Vector2(-LobbyPlayToolRow.Inset, -LobbyPlayToolRow.Inset));
            area.SetAsFirstSibling();
            var shape = LobbyKit.Rect(area, picture ? "Picture frame" : "Map fallback").Fill();
            var aspect = shape.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent; aspect.aspectRatio = LobbyPlayCardGrid.PictureAspect;
            shape.Paint(picture ? Color.white : Color.Lerp(LobbyKit.Raised, LobbyKit.Selection, .14f), PictureRadius).raycastTarget = false;
            if (picture)
            {
                shape.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                var image = LobbyKit.Rect(shape, "Map preview").Fill().gameObject.AddComponent<Image>();
                image.sprite = picture; image.preserveAspect = true; image.color = Color.white; image.raycastTarget = false;
            }
            else
            {
                var item = LobbyKit.ItemImage(shape, art, ToolArt);
                if (item)
                {
                    item.rectTransform.Place(new Vector2(0, .1f), new Vector2(1, .9f));
                    var square = item.gameObject.AddComponent<AspectRatioFitter>();
                    square.aspectMode = AspectRatioFitter.AspectMode.FitInParent; square.aspectRatio = 1;
                }
            }
            var heading = LobbyKit.Text(card.Body(), title, ToolTitleSize, LobbyKit.Navy, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            heading.name = ToolTitle;
            heading.enableAutoSizing = true; heading.fontSizeMin = SmallestLabel; heading.fontSizeMax = ToolTitleSize;
            heading.rectTransform.Place(Vector2.zero, new Vector2(1, 0), new Vector2(ToolInset, LobbyPlayToolRow.Caption * .5f), new Vector2(-ToolInset, LobbyPlayToolRow.Caption - LobbyPlayToolRow.Inset));
            var words = LobbyKit.Text(card.Body(), line, ToolLineSize, available ? LobbyKit.Muted : LobbyKit.Hot, TextAlignmentOptions.TopLeft);
            words.name = ToolLine;
            words.enableAutoSizing = true; words.fontSizeMin = SmallestLabel; words.fontSizeMax = ToolLineSize;
            words.rectTransform.Place(Vector2.zero, new Vector2(1, 0), new Vector2(ToolInset, LobbyPlayToolRow.Inset), new Vector2(-ToolInset, LobbyPlayToolRow.Caption * .5f - 2f));
            return card;
        }

        void OpenHomeBuilder()
        {
            if (Menu.Starting) return;
            WordboundMaps = false;
            Menu.Choose(queue: LobbyMenu.Workshop, mode: LobbyMenu.WorkshopMode);
            Menu.NoteChoice();
            Menu.Go();
        }

        void OpenMapEditor()
        {
            if (Menu.Starting || !Session.CanLoad(Session.WordboundScene)) return;
            WordboundMaps = true;
            Menu.NoteChoice();
            Menu.OpenWordboundWorkshop();
        }

        void WorkshopFooter(RectTransform body, bool available)
        {
            var bar = SummaryFrame(body, "Workshop summary");
            SummaryLine(bar);
            var heading = LobbyKit.Text(bar, MapEditorTitle, 26, LobbyKit.Navy, TextAlignmentOptions.BottomLeft);
            heading.name = "Workshop title";
            heading.enableAutoSizing = true; heading.fontSizeMin = 20; heading.fontSizeMax = 26;
            heading.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 44), new Vector2(-250, -8));
            var detail = LobbyKit.Text(bar, available ? MapEditorNote : MapEditorMissing,
                18, available ? LobbyKit.Muted : LobbyKit.Hot, TextAlignmentOptions.TopLeft);
            detail.name = "Workshop detail";
            detail.enableAutoSizing = true; detail.fontSizeMin = SmallestLabel; detail.fontSizeMax = 18;
            detail.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 10), new Vector2(-250, -48));
            var open = LobbyKit.Primary(bar, "Workshop OPEN", PlayPageText.OpenWord, OpenMapEditor, 32, 24);
            ((RectTransform)open.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-220, -30), new Vector2(-8, 30));
            open.interactable = available;
        }

        public static string CourseTitle =>
            LobbyPlayModeLabels.Name(WordboundSoloEntry.SelectedMode) + MapCardText.Separator + SoloMapName(WordboundSoloEntry.SelectedMap);

        public static readonly string[] SoloMaps = { WordboundSoloEntry.ParcelServiceMap, WordboundSoloEntry.LostAddressMap, WordboundSoloEntry.ParcelHallMap };

        public static string SoloMapName(string id) => id switch
        {
            WordboundSoloEntry.LostAddressMap => "The Lost Address",
            WordboundSoloEntry.ParcelHallMap => "Parcel Hall",
            _ => "Parcel Depot",
        };

        void PickQueue(string id)
        {
            string queue = id.Substring("Queue ".Length);
            Online = queue == LobbyMenu.Matchmaking;
            Friends = false;
            if (!Online) Menu.Choose(queue: queue);
            Menu.NoteChoice();
            Refresh();
            Reselect(id);
        }

        static string QueueLine(string queue) => queue == LobbyMenu.Matchmaking
            ? "Play with people online."
            : queue == LobbyMenu.Workshop ? "Build homes and maps." : "1 to 4 players on this screen. Bots fill empty seats.";

        string ShownMode => WordboundSolo ? WordboundSoloEntry.SelectedMode : Menu.Mode;

        public static string ModeButton(string mode) => (HouseModes.Has(mode) ? "Mode " : "Solo ") + mode;

        public static string SoloOnly(string mode) => LobbyPlayModeLabels.Name(mode) + " is for 1 player. Remove extra seats to play.";

        public static string TooMany(string mode) => LobbyPlayModeLabels.Seats(mode) == 1 ? SoloOnly(mode)
            : LobbyPlayModeLabels.Name(mode) + " takes up to " + LobbyPlayModeLabels.Seats(mode) + " players. Remove extra seats to play.";

        public static IReadOnlyList<HouseModeFamily> FamilyOrder => PlayPageText.Families(TutorialDirector.Done);

        public static bool SeatOpen(string mode, int partySize) => partySize <= LobbyPlayModeLabels.Seats(mode);

        public static string Refusal(LobbyMenu menu)
        {
            if (!menu || menu.Queue != LobbyMenu.Practice || !HouseModes.Has(menu.Mode)) return null;
            return SeatOpen(menu.Mode, menu.PartySize) ? null : TooMany(menu.Mode);
        }

        public const string ModeWords = "Mode words", ModeEmblemName = ModeEmblem, ModeTiles = "Mode tiles", ModeDetail = "Mode panel", ModeTitle = "Mode title";
        const float StepHeight = 300, TileShare = .44f, TileGap = 10, DetailPad = 12, TileArt = 72, GuideWidth = 180, GuideHeight = 48;

        void PracticeModes(RectTransform column)
        {
            string chosen = ShownMode;
            var open = BrowsedFamily ?? OpenFamily(chosen);
            bool looking = !HouseModes.ModesIn(open).Contains(chosen) && HouseModes.ModesIn(open).Count > 0;
            string shown = looking ? HouseModes.ModesIn(open)[0] : chosen;
            var tint = CategoryTint(open);
            ModeCategories(column, open);
            var step = LobbyKit.Rect(column, ModeBoard);
            step.Size(-1, StepHeight);

            var tiles = LobbyKit.Rect(step, ModeTiles).Place(Vector2.zero, new Vector2(TileShare, 1));
            var detail = LobbyKit.Rect(step, ModeDetail).Place(new Vector2(TileShare, 0), Vector2.one, new Vector2(TileGap * 2, 0), Vector2.zero);
            LobbyKit.Face(detail, LobbyKit.Card, 16, LobbyKit.Line, 1);

            var preview = LobbyPlayModePreview.Create(detail, shown);
            var frame = (RectTransform)preview.transform;
            float pictureHeight = StepHeight - DetailPad * 2, pictureWidth = pictureHeight * LobbyPlayModePreview.Aspect;
            frame.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(DetailPad, -DetailPad - pictureHeight), new Vector2(DetailPad + pictureWidth, -DetailPad));
            var text = LobbyKit.Rect(detail, ModeWords).Fill();
            text.offsetMin = new Vector2(DetailPad * 2 + pictureWidth, 0);
            const float words = 0;
            var fit = step.gameObject.AddComponent<LobbyPlayStepFit>();
            fit.Preview = frame; fit.Words = text; fit.Tiles = tiles; fit.Pad = DetailPad;

            var modes = HouseModes.ModesIn(open);
            int columns = modes.Count <= 2 ? 1 : 2, rows = Mathf.Max(1, Mathf.CeilToInt(modes.Count / (float)columns));
            var closed = new List<(Button button, string mode)>();
            for (int i = 0; i < modes.Count; i++)
            {
                string mode = modes[i];
                int c = i % columns, r = i / columns;
                var button = ModeTile(tiles, mode, tint);
                ((RectTransform)button.transform).Place(new Vector2(c / (float)columns, 1f - (r + 1f) / rows), new Vector2((c + 1f) / columns, 1f - r / (float)rows),
                    new Vector2(c == 0 ? 0 : TileGap / 2, r == rows - 1 ? 0 : TileGap / 2), new Vector2(c == columns - 1 ? 0 : -TileGap / 2, r == 0 ? 0 : -TileGap / 2));
                var hover = button.gameObject.AddComponent<LobbyPlayModeHover>();
                hover.Preview = preview; hover.Mode = mode;
                if (!SeatOpen(mode, Menu.PartySize)) closed.Add((button, mode));
            }

            var title = LobbyKit.Text(text, LobbyPlayModeLabels.Name(shown), 34, tint.ink, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            title.name = ModeTitle;
            title.enableAutoSizing = true; title.fontSizeMin = 20; title.fontSizeMax = 34;
            title.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(words, -DetailPad - 46), new Vector2(-DetailPad, -DetailPad));
            var line = Goal(text, looking ? FullCategory(open) : LobbyPlayModeLabels.Goal(shown));
            if (line)
            {
                line.alignment = TextAlignmentOptions.TopLeft;
                line.textWrappingMode = TextWrappingModes.Normal;
                line.fontSizeMin = SmallestLabel; line.fontSizeMax = 21;
                line.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(words, 122), new Vector2(-DetailPad, -DetailPad - 54));
            }
            var access = Access(text, shown);
            if (access)
            {
                access.alignment = TextAlignmentOptions.MidlineLeft;
                access.textWrappingMode = TextWrappingModes.Normal;
                access.enableAutoSizing = true; access.fontSizeMin = SmallestLabel; access.fontSizeMax = Mathf.Max(SmallestLabel, access.fontSize);
                access.rectTransform.Place(Vector2.zero, new Vector2(1, 0), new Vector2(words, 66), new Vector2(-DetailPad, 118));
            }
            HowToPlayButton(text, shown, words);
            foreach (var (button, mode) in closed) ExplainClosed(button, line, TooMany(mode));
        }

        const int HouseStepRow3 = 3;
        public const string HouseStepRow = "House step", BackToModes = "Back to modes", ChosenMode = "Chosen mode", NextWord = "Next";
        public const string HouseStepTitle = "Now pick a house", CourseStepTitle = "Now pick a map";
        bool housesStep;

        public HouseModeFamily? BrowsedFamily { get; set; }

        public override void Opened() { housesStep = false; BrowsedFamily = null; base.Opened(); }

        void ShowPlaces()
        {
            housesStep = true;
            Refresh();
            var cards = Root.GetComponentsInChildren<Button>().Where(card => card.name.StartsWith("Map ") || card.name.StartsWith("Solo map ") || card.name.StartsWith(SavedHomeCard)).ToArray();
            var chosen = cards.FirstOrDefault(card => card.GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == SelectedMark));
            Reselect(chosen ? chosen.name : cards.Length > 0 ? cards[0].name : AnyHouse);
        }

        void HouseStepHeader(RectTransform column, string heading)
        {
            string mode = ShownMode;
            var tint = CategoryTint(OpenFamily(mode));
            var row = LobbyKit.Row(column, HouseStepRow, 12);
            row.Size(-1, 64);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            var back = LobbyKit.Button(row, BackToModes, LobbyKit.Raised, () => { housesStep = false; Refresh(); Reselect(ModeButton(mode)); }, 12, LobbyKit.Line, 2);
            back.GetComponent<RectTransform>().Size(150, 52);
            var backLabel = LobbyKit.Text(back.Body(), "<  Modes", 19, LobbyKit.Navy, TextAlignmentOptions.Center, FontStyles.Bold);
            backLabel.raycastTarget = false; backLabel.rectTransform.Fill();

            var chip = LobbyKit.Rect(row, ChosenMode);
            chip.Size(340, 64);
            LobbyKit.Face(chip, tint.fill, 14, tint.edge, 3);
            var art = LobbyKit.ItemImage(chip, LobbyPlayModeLabels.Emblem(mode), 46);
            if (art) art.rectTransform.Pin(new Vector2(0, .5f), new Vector2(14, 0), new Vector2(46, 46));
            var name = LobbyKit.Text(chip, LobbyPlayModeLabels.Name(mode), 22, tint.ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            name.enableAutoSizing = true; name.fontSizeMin = SmallestLabel; name.fontSizeMax = 22;
            name.raycastTarget = false;
            name.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(70, 0), new Vector2(-12, 0));

            var title = LobbyKit.Text(row, heading, 26, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            title.rectTransform.Size(360, 64);
        }

        Button ModeTile(Transform parent, string mode, (Color fill, Color edge, Color ink) tint)
        {
            string id = ModeButton(mode);
            bool open = SeatOpen(mode, Menu.PartySize);
            bool selected = open && ShownMode == mode;
            Action pick = open ? () => { PickMode(mode); Refresh(); Reselect(id); } : () => Menu.Post(TooMany(mode));
            var button = LobbyKit.Button(parent, id, tint.fill, pick, 14, selected ? tint.edge : tint.fill, selected ? 3 : 1);
            var body = button.Body();
            var art = LobbyKit.ItemImage(body, LobbyPlayModeLabels.Emblem(mode), TileArt);
            if (art)
            {
                art.name = ModeEmblem;
                art.rectTransform.Pin(new Vector2(1, 1), new Vector2(-16, -12), new Vector2(TileArt, TileArt));
            }
            var name = LobbyKit.Text(body, LobbyPlayModeLabels.Name(mode), 26, tint.ink, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            name.enableAutoSizing = true; name.fontSizeMin = SmallestLabel; name.fontSizeMax = 26;
            name.textWrappingMode = TextWrappingModes.Normal;
            name.raycastTarget = false;
            name.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(16, 12), new Vector2(-16, -10));
            if (ModeAccess.For(mode).MaxSeats == 1) TileTag(body, SeatTag, ModeAccess.OnePlayer, tint.ink, !open);
            if (open && mode == LobbyMenu.TutorialMode && !TutorialDirector.Done) TileTag(body, StartTag, PlayPageText.StartHere, tint.edge, true);
            var underline = LobbyKit.Rect(body, ModeUnderline).Place(Vector2.zero, new Vector2(1, 0), new Vector2(14, 4), new Vector2(-14, 8)).Paint(tint.edge, 2);
            underline.raycastTarget = false; underline.enabled = selected;
            if (!open) button.gameObject.AddComponent<CanvasGroup>().alpha = ClosedAlpha;
            if (selected) First = button;
            return button;
        }

        static void TileTag(Transform body, string name, string text, Color ink, bool shown)
        {
            var room = LobbyKit.Rect(body, name).Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -10 - TagHeight), new Vector2(-14, -10));
            var tag = LobbyKit.Caps(room, text, TagSize, TextAlignmentOptions.MidlineLeft);
            tag.color = ink; tag.raycastTarget = false;
            tag.rectTransform.Fill();
            tag.gameObject.SetActive(shown);
        }

        static void HowToPlayButton(RectTransform detail, string mode, float words)
        {
            if (string.IsNullOrEmpty(mode) || !ModeGuides.Has(mode)) return;
            var button = LobbyKit.Button(detail, GuideIcon, LobbyKit.Raised, () => ModeGuides.Show(mode), 12, LobbyKit.Line, 2);
            if (detail.Find(PlayOnline)) ((RectTransform)button.transform).Place(Vector2.zero, new Vector2(.5f, 0), new Vector2(words, DetailPad), new Vector2(-DetailPad / 2, DetailPad + GuideHeight));
            else ((RectTransform)button.transform).Place(Vector2.zero, Vector2.zero, new Vector2(words, DetailPad), new Vector2(words + GuideWidth, DetailPad + GuideHeight));
            var label = LobbyKit.Text(button.Body(), "?  " + GuideHint, 17, LobbyKit.Navy, TextAlignmentOptions.Center, FontStyles.Bold);
            label.enableAutoSizing = true; label.fontSizeMin = SmallestLabel; label.fontSizeMax = 17;
            label.raycastTarget = false;
            label.rectTransform.Fill();
            button.gameObject.AddComponent<LobbyHint>().Text = GuideHint;
        }

        public const string CategoryRow = "Mode categories", CategoryCard = "Category ", CategoryName = "Category name", CategoryLine = "Category line";
        const float CategoryHeight = 78, CategoryArt = 52, CategoryInset = 12;

        static HouseModeFamily OpenFamily(string shown)
        {
            var family = HouseModes.FamilyOf(shown);
            return FamilyOrder.Contains(family) ? family : FamilyOrder[0];
        }

        static string CategoryBlurb(HouseModeFamily family) => family switch
        {
            HouseModeFamily.Classic => "Battle each other",
            HouseModeFamily.Coop => "Work together",
            HouseModeFamily.Arcade => "Puzzles and challenges",
            _ => "Learn the controls",
        };

        public static string FullCategory(HouseModeFamily family) =>
            "Every " + HouseModes.FamilyName(family) + " mode is for 1 player. Remove extra seats to play one.";

        static (Color fill, Color edge, Color ink) CategoryTint(HouseModeFamily family) => family switch
        {
            HouseModeFamily.Classic => (new Color32(0xFA, 0xEC, 0xE7, 0xFF), new Color32(0xD8, 0x5A, 0x30, 0xFF), new Color32(0x71, 0x2B, 0x13, 0xFF)),
            HouseModeFamily.Coop => (new Color32(0xEA, 0xF3, 0xDE, 0xFF), new Color32(0x63, 0x99, 0x22, 0xFF), new Color32(0x27, 0x50, 0x0A, 0xFF)),
            HouseModeFamily.Arcade => (new Color32(0xEE, 0xED, 0xFE, 0xFF), new Color32(0x7F, 0x77, 0xDD, 0xFF), new Color32(0x3C, 0x34, 0x89, 0xFF)),
            _ => (new Color32(0xE1, 0xF5, 0xEE, 0xFF), new Color32(0x1D, 0x9E, 0x75, 0xFF), new Color32(0x08, 0x50, 0x41, 0xFF)),
        };

        void ModeCategories(RectTransform column, HouseModeFamily open)
        {
            var row = LobbyKit.Row(column, CategoryRow, 12);
            row.Size(-1, CategoryHeight);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            foreach (var family in FamilyOrder)
            {
                var modes = HouseModes.ModesIn(family);
                var tint = CategoryTint(family);
                bool lit = family == open;
                var button = LobbyKit.Button(row, CategoryCard + HouseModes.FamilyName(family), lit ? tint.fill : LobbyKit.Raised,
                    () => OpenCategory(family), 14, lit ? tint.edge : LobbyKit.Line, lit ? 3 : 1);
                var body = button.Body();
                float words = CategoryInset;
                var art = modes.Count > 0 ? LobbyKit.ItemImage(body, LobbyPlayModeLabels.Emblem(modes[0]), CategoryArt) : null;
                if (art)
                {
                    art.rectTransform.Place(new Vector2(0, .5f), new Vector2(0, .5f),
                        new Vector2(CategoryInset, -CategoryArt / 2), new Vector2(CategoryInset + CategoryArt, CategoryArt / 2));
                    words += CategoryArt + CategoryInset;
                }
                var name = LobbyKit.Text(body, HouseModes.FamilyName(family), 22, tint.ink, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
                name.name = CategoryName;
                name.enableAutoSizing = true; name.fontSizeMin = SmallestLabel; name.fontSizeMax = 22;
                name.raycastTarget = false;
                name.rectTransform.Place(new Vector2(0, .5f), Vector2.one, new Vector2(words, 0), new Vector2(-CategoryInset, -6));
                var line = LobbyKit.Text(body, CategoryBlurb(family), 15, LobbyKit.Muted, TextAlignmentOptions.TopLeft);
                line.name = CategoryLine;
                line.enableAutoSizing = true; line.fontSizeMin = 12; line.fontSizeMax = 15;
                line.textWrappingMode = TextWrappingModes.Normal;
                line.raycastTarget = false;
                line.rectTransform.Place(Vector2.zero, new Vector2(1, .5f), new Vector2(words, 4), new Vector2(-CategoryInset, -2));
            }
        }

        void OpenCategory(HouseModeFamily family)
        {
            var modes = HouseModes.ModesIn(family);
            string card = CategoryCard + HouseModes.FamilyName(family);
            if (modes.Contains(ShownMode))
            {
                if (BrowsedFamily != null) { BrowsedFamily = null; Refresh(); Reselect(card); }
                return;
            }
            string mode = modes.FirstOrDefault(m => SeatOpen(m, Menu.PartySize));
            if (mode == null)
            {
                if (modes.Count == 0) return;
                BrowsedFamily = family; Refresh(); Reselect(card);
                return;
            }
            PickMode(mode);
            Refresh();
            Reselect(CategoryCard + HouseModes.FamilyName(family));
        }

        TextMeshProUGUI Access(Transform parent, string mode)
        {
            var access = ModeAccess.For(mode);
            if (access.Line.Length == 0) return null;
            var line = LobbyKit.Text(parent, access.Line, 16, LobbyKit.Muted, TextAlignmentOptions.Center);
            line.name = AccessLine;
            line.enableAutoSizing = true; line.fontSizeMin = SmallestLabel; line.fontSizeMax = 16;
            if (access.SameScreen && access.OnlineCard != null) PlayOnlineButton(line, PlaylistCard + access.OnlineCard, ModeGuides.Has(mode));
            return line;
        }

        void PlayOnlineButton(TextMeshProUGUI line, string card, bool besideGuide)
        {
            var words = (RectTransform)line.transform.parent;
            var button = LobbyKit.Button(words, PlayOnline, LobbyKit.Raised, () => OpenOnline(card), 12, LobbyKit.Line, 2);
            var label = LobbyKit.Text(button.Body(), PlayOnline, 17, LobbyKit.Navy, TextAlignmentOptions.Center, FontStyles.Bold);
            label.raycastTarget = false; label.rectTransform.Fill();
            label.enableAutoSizing = true; label.fontSizeMin = SmallestLabel; label.fontSizeMax = 17;
            if (besideGuide) ((RectTransform)button.transform).Place(new Vector2(.5f, 0), new Vector2(1, 0), new Vector2(DetailPad / 2, DetailPad), new Vector2(-DetailPad, DetailPad + GuideHeight));
            else ((RectTransform)button.transform).Place(Vector2.zero, Vector2.zero, new Vector2(0, DetailPad), new Vector2(GuideWidth, DetailPad + GuideHeight));
        }

        void OpenOnline(string card)
        {
            Online = true; Friends = false;
            Aim = card;
            Menu.NoteChoice();
            Refresh();
            Reselect(card);
        }

        static Transform TileBoard(RectTransform column, string name, string family)
        {
            var board = LobbyKit.Rect(column, name);
            var tiles = LobbyKit.Rect(board, ModeSections).gameObject.AddComponent<LobbyPlayModeFamilies>();
            var fit = board.gameObject.AddComponent<LobbyPlayModeBoard>();
            fit.Sections = tiles;
            return LobbyKit.Rect(tiles.transform, family);
        }

        public const string TagRow = "Tags";

        static Button ModeTile(Transform parent, string id, string label, string emblem, bool named, bool selected, Action action)
        {
            var tile = LobbyKit.Button(parent, id, selected ? LobbyKit.Picked : LobbyKit.Card, action, TileRadius,
                selected ? LobbyKit.Selection : LobbyKit.Line, selected ? 3 : 1, 0);
            var press = tile.GetComponent<LobbyPress>();
            press.Tilt = 0;
            var body = tile.Body();
            var name = LobbyKit.Text(body, label, TileName, LobbyKit.Navy, TextAlignmentOptions.Top, FontStyles.Bold);
            name.name = ModeName;
            name.textWrappingMode = TextWrappingModes.Normal;
            name.enableAutoSizing = true; name.fontSizeMin = SmallestLabel; name.fontSizeMax = TileName;
            name.color = selected ? LobbyKit.Selection : LobbyKit.Navy;
            name.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(TilePad, TileTagRoom), new Vector2(-TilePad, -(TileArtTop + ModeArt + TileArtGap)));
            var art = LobbyKit.ItemImage(body, emblem, ModeArt);
            if (art)
            {
                if (named) art.name = ModeEmblem;
                art.rectTransform.Place(new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(-ModeArt * .5f, -TileArtTop - ModeArt), new Vector2(ModeArt * .5f, -TileArtTop));
            }
            var tags = LobbyKit.Row(body, TagRow, TagSlack);
            tags.Place(Vector2.zero, new Vector2(1, 0), new Vector2(TilePad, TilePad * .5f), new Vector2(-TilePad, TilePad * .5f + TagHeight));
            var row = tags.GetComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter; row.childForceExpandHeight = false; row.childForceExpandWidth = false;
            var underline = LobbyKit.Rect(body, ModeUnderline).Place(Vector2.zero, new Vector2(1, 0), new Vector2(UnderlineInset, 1), new Vector2(-UnderlineInset, 1 + UnderlineHeight))
                .Paint(LobbyKit.Selection, 1);
            underline.raycastTarget = false;
            underline.enabled = selected;
            press.Hot = hot => underline.enabled = selected || hot;
            return tile;
        }

        static void SizeTiles(LobbyPlayModeFamilies families)
        {
            if (!families) return;
            float need = LobbyPlayModeFamilies.MinimumTile;
            foreach (var tile in families.GetComponentsInChildren<Button>(true)) need = Mathf.Max(need, TileNeed(tile));
            families.MinimumWidth = Mathf.Ceil(need);
        }

        public static float TileNeed(Selectable tile)
        {
            var body = tile.Body();
            float need = 0f;
            if (body.Find(ModeName) is Transform label && label.TryGetComponent(out TMP_Text name)) need = TwoLines(name);
            if (body.Find(TagRow) is Transform row)
                foreach (var tag in row.GetComponentsInChildren<TMP_Text>(true)) need = Mathf.Max(need, tag.GetPreferredValues(tag.text).x + TagSlack * 2);
            return need + TilePad * 2;
        }

        static float TwoLines(TMP_Text text)
        {
            float size = text.fontSize; bool automatic = text.enableAutoSizing;
            text.enableAutoSizing = false; text.fontSize = TileFit;
            var words = text.text.Split(' ');
            float best = text.GetPreferredValues(text.text).x;
            for (int split = 1; split < words.Length; split++)
                best = Mathf.Min(best, Mathf.Max(text.GetPreferredValues(string.Join(" ", words, 0, split)).x,
                    text.GetPreferredValues(string.Join(" ", words, split, words.Length - split)).x));
            text.fontSize = size; text.enableAutoSizing = automatic;
            return best + NameSlack;
        }

        static void ExplainClosed(Button button, TextMeshProUGUI goal, string reason)
        {
            if (!button.TryGetComponent(out LobbyHint hint)) hint = button.gameObject.AddComponent<LobbyHint>();
            hint.Text = reason;
            if (!goal) return;
            var swap = button.gameObject.AddComponent<LobbyPlayGoalSwap>();
            swap.Goal = goal; swap.Original = goal.text; swap.Reason = reason;
        }

        void PickMode(string mode)
        {
            housesStep = false;
            BrowsedFamily = null;
            WordboundSolo = !HouseModes.Has(mode);
            if (WordboundSolo) WordboundSoloEntry.Select(mode);
            else Menu.Choose(queue: LobbyMenu.Practice, mode: mode);
            Menu.NoteChoice(true);
        }

        static TextMeshProUGUI Goal(Transform parent, string goal)
        {
            if (string.IsNullOrEmpty(goal)) return null;
            var line = LobbyKit.Text(parent, goal, 19, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
            line.name = GoalLine;
            line.enableAutoSizing = true; line.fontSizeMin = SmallestLabel; line.fontSizeMax = 19;
            return line;
        }

        static void GuideButton(TextMeshProUGUI line, string mode)
        {
            if (!line || string.IsNullOrEmpty(mode) || !ModeGuides.Has(mode)) return;
            var button = LobbyKit.Button(line.transform, GuideIcon, LobbyKit.Card, () => ModeGuides.Show(mode), GuideRadius, LobbyKit.Line, 2);
            var rect = ((RectTransform)button.transform).Pin(new Vector2(.5f, .5f), Vector2.zero, Vector2.one * LobbyPlayGoalInfo.Size);
            button.Body().Place(Vector2.zero, Vector2.one, Vector2.one * GuideInset, -Vector2.one * GuideInset);
            LobbyKit.Text(button.Body(), "i", 17, LobbyKit.Navy, TextAlignmentOptions.Center, FontStyles.Bold).rectTransform.Fill();
            button.gameObject.AddComponent<LobbyHint>().Text = GuideHint;
            var info = line.gameObject.AddComponent<LobbyPlayGoalInfo>();
            info.Line = line; info.Icon = rect;
        }

        static void FillRow(RectTransform cards)
        {
            var grid = cards.GetComponent<LobbyPlayCardGrid>();
            grid.MaximumColumns = HouseStepRow3;
            grid.FillPage = true;
        }

        static int MapBasis => Mathf.Max(1, GameConfig.Current?.Houses?.Count ?? WidestMapRow);

        RectTransform MapCards(Transform parent, string name)
        {
            float width = LobbyPlayCardGrid.PreferredWidth, height = LobbyPlayCardGrid.HeightFor(width);
            var rect = LobbyKit.Grid(parent, name, new Vector2(width, height), 12).Size(-1, height);
            var grid = rect.gameObject.AddComponent<LobbyPlayCardGrid>();
            grid.MinimumWidth = playLayout ? playLayout.MapMinimumWidth : 264;
            grid.MaximumColumns = playLayout ? playLayout.MapColumns : WidestMapRow;
            grid.Basis = MapBasis;
            return rect;
        }

        void MapsHeader(RectTransform column)
        {
            var row = LobbyKit.Row(column, MapsRow, MapsRowGap);
            row.Size(-1, ChipHeight);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            var label = LobbyKit.SectionLabel(row, "Houses");
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.Size(Mathf.Ceil(label.GetPreferredValues(label.text).x) + TagSlack, ChipHeight);
            AnyHouseChip(row, Menu.SavedHome == null && Menu.Maps.All);
        }

        void AnyHouseChip(Transform row, bool lit)
        {
            var chip = LobbyKit.Button(row, AnyHouse, lit ? LobbyKit.Picked : LobbyKit.Card, () => PickAnyHouse(AnyHouse), ChipRadius,
                lit ? LobbyKit.Selection : LobbyKit.Line, lit ? 2 : 1);
            var content = LobbyKit.Row(chip.Body(), "Content", ChipGap);
            content.Fill();
            var layout = content.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter; layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(ChipPadLeft, ChipPadRight, 0, 0);
            LobbyKit.Icon(content, LobbyIcons.Check, lit ? LobbyKit.Selection : LobbyKit.Line).Size(ChipIcon, ChipIcon);
            var label = LobbyKit.Text(content, PracticeMaps.Every, ChipText, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft);
            label.Size(-1, ChipText + 10);
            float width = ChipPadLeft + ChipIcon + ChipGap + Mathf.Ceil(label.GetPreferredValues(label.text).x) + ChipPadRight;
            chip.Size(width, ChipHeight);
            chip.gameObject.AddComponent<LobbyHint>().Text = AnyHouseHint;
        }

        void PickAnyHouse(string aim)
        {
            Menu.Maps.TickAll();
            Session.LobbyMaps = Menu.Maps.Save();
            Menu.Choose(map: Menu.Map);
            Menu.NoteChoice(true);
            Refresh(); Reselect(aim);
        }

        void PickHouse(string id, string name)
        {
            if (Menu.SavedHome == null && Menu.Maps.All) Menu.PlayOnly(id);
            else if (!Menu.ToggleMap(id)) { PickAnyHouse(name); return; }
            Menu.NoteChoice(true);
            Refresh(); Reselect(name);
        }

        static string Participants(string mode, int humans)
        {
            if (mode == LobbyMenu.WorkshopMode) return "Solo";
            string seats = LobbyMenu.Seats(mode, humans);
            return string.IsNullOrEmpty(seats) ? humans == 1 ? "1 player" : humans + " players" : Capital(seats);
        }

        static RectTransform ScrollableChoices(RectTransform region)
        {
            var content = LobbyKit.Scroll(region, "House choices scroll", 8);
            var scroll = content.GetComponentInParent<ScrollRect>();
            var view = (RectTransform)scroll.transform;
            view.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            view.offsetMax = new Vector2(-18, 0);
            var track = LobbyKit.Rect(region, "Scroll position").Place(new Vector2(1, 0), Vector2.one,
                new Vector2(-8, 0), Vector2.zero);
            track.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            track.Paint(LobbyKit.Line, 4);
            var handle = LobbyKit.Rect(track, "Handle").Fill();
            var ink = handle.Paint(LobbyKit.Cyan, 4);
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handle; bar.targetGraphic = ink;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        static Button MapCard(Transform parent, string name, bool selected, Action click)
        {
            var card = LobbyKit.Button(parent, name, LobbyKit.Card, click, 14,
                selected ? LobbyKit.Selection : LobbyKit.Line, selected ? 3 : 1, 0);
            var press = card.GetComponent<LobbyPress>();
            press.Lift = 0; press.Tilt = 0; press.Sink = 1;
            var ink = selected ? LobbyParcel.Stamp : LobbyKit.Line;
            LobbyParcel.Dashes(card.Body(), new Color(ink.r, ink.g, ink.b, selected ? .9f : .7f), 5f);
            if (selected)
            {
                var mark = LobbyKit.Rect(card.Body(), SelectedMark).Pin(Vector2.one, new Vector2(-16, -16), new Vector2(34, 38));
                mark.localRotation = Quaternion.Euler(0f, 0f, -6f);
                mark.Paint(LobbyKit.Hex(0xFFF6E8), 2).raycastTarget = false;
                LobbyParcel.Dashes(mark, LobbyParcel.Stamp, 2f);
                var tick = LobbyKit.Icon(mark, LobbyIcons.Check, LobbyParcel.Stamp).rectTransform.Fill();
                tick.offsetMin = new Vector2(7, 9); tick.offsetMax = new Vector2(-7, -9);
            }
            return card;
        }

        static RectTransform PictureShape(Selectable card, string name, Color fill)
        {
            var area = LobbyKit.Rect(card.Body(), "Map picture").Place(Vector2.zero, Vector2.one,
                new Vector2(LobbyPlayCardGrid.Inset, LobbyPlayCardGrid.CaptionHeight), new Vector2(-LobbyPlayCardGrid.Inset, -LobbyPlayCardGrid.Inset));
            area.SetAsFirstSibling();
            var shape = LobbyKit.Rect(area, name).Fill();
            var fitter = shape.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = LobbyPlayCardGrid.PictureAspect;
            shape.Paint(fill, PictureRadius).raycastTarget = false;
            return shape;
        }

        static RectTransform CardPicture(Selectable card, Sprite picture, string icon, string art = null)
        {
            var shape = PictureShape(card, picture ? "Picture frame" : "Map fallback", picture ? Color.white : Color.Lerp(LobbyKit.Raised, LobbyKit.Selection, .14f));
            if (picture)
            {
                shape.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                var image = LobbyKit.Rect(shape, "Map preview").Fill().gameObject.AddComponent<Image>();
                image.sprite = picture; image.preserveAspect = true; image.color = Color.white; image.raycastTarget = false;
                return shape;
            }
            var item = art == null ? null : LobbyKit.ItemImage(shape, art, 160);
            if (item)
            {
                item.rectTransform.Place(new Vector2(0, .12f), new Vector2(1, .88f));
                var square = item.gameObject.AddComponent<AspectRatioFitter>();
                square.aspectMode = AspectRatioFitter.AspectMode.FitInParent; square.aspectRatio = 1;
                return shape;
            }
            LobbyKit.Icon(shape, icon, new Color(LobbyKit.Selection.r, LobbyKit.Selection.g, LobbyKit.Selection.b, .9f)).rectTransform
                .Place(new Vector2(0, .3f), new Vector2(1, .7f));
            return shape;
        }

        Sprite CoursePreview(string id) => !playLayout ? null : id switch
        {
            WordboundSoloEntry.LostAddressMap => playLayout.LostAddressPreview,
            WordboundSoloEntry.ParcelHallMap => playLayout.ParcelHallPreview,
            WordboundSoloEntry.ParcelServiceMap => playLayout.ParcelServicePreview,
            _ => null,
        };

        void SoloMapCard(Transform parent, string id, string title, string detail)
        {
            string name = "Solo map " + id;
            var card = MapCard(parent, name, WordboundSoloEntry.SelectedMap == id, () => {
                WordboundSoloEntry.SelectMap(id); Menu.NoteChoice(true); Refresh(); Reselect(name);
            });
            MapWords(card, CardPicture(card, LobbyMapSnapshots.For(id) ?? CoursePreview(id), LobbyIcons.Layers), title, detail);
        }

        static void MapWords(Selectable card, RectTransform picture, string title, string detail)
        {
            var scrim = LobbyKit.Rect(picture, NameScrim).Place(Vector2.zero, new Vector2(1, .55f));
            var shade = scrim.Paint(Color.white, PictureRadius);
            shade.raycastTarget = false;
            var dark = LobbyKit.ScrimNavy;
            LobbyKit.Gradient(shade, new Color(dark.r, dark.g, dark.b, 0f), new Color(dark.r, dark.g, dark.b, .8f));
            var name = LobbyKit.Text(picture, title, 24, Color.white, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            name.name = "Map title";
            name.enableAutoSizing = true; name.fontSizeMin = 14; name.fontSizeMax = 24;
            name.rectTransform.Place(Vector2.zero, new Vector2(1, .5f), new Vector2(TitleInset, 8), new Vector2(-TitleInset, 0));
            float inset = LobbyPlayCardGrid.Inset + 6;
            var text = LobbyKit.Text(card.Body(), detail, 17, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
            text.name = "Map detail";
            text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = 17;
            text.rectTransform.Place(Vector2.zero, new Vector2(1, 0), new Vector2(inset, 4), new Vector2(-inset, LobbyPlayCardGrid.CaptionHeight - 2));
        }

        static TextMeshProUGUI RowWords(Selectable card, RectTransform picture, string title, string detail)
        {
            var name = LobbyKit.Text(card.Body(), title, 24, LobbyKit.Navy, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            name.name = "Map title";
            name.textWrappingMode = TextWrappingModes.Normal;
            name.enableAutoSizing = true; name.fontSizeMin = SmallestLabel; name.fontSizeMax = 24;
            var text = LobbyKit.Text(card.Body(), detail, 17, LobbyKit.Muted, TextAlignmentOptions.TopLeft);
            text.name = "Map detail";
            text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = true; text.fontSizeMin = SmallestLabel; text.fontSizeMax = 17;
            var row = card.gameObject.AddComponent<LobbyPlayRowCard>();
            row.Picture = (RectTransform)picture.parent; row.Title = name.rectTransform; row.Detail = text.rectTransform;
            row.Fit();
            return text;
        }

        void HouseCard(Transform parent, string id, HouseLayout layout)
        {
            string name = "Map " + id;
            bool ticked = Menu.SavedHome == null && !Menu.Maps.All && Menu.Maps.IsTicked(id);
            var card = MapCard(parent, name, ticked, () => PickHouse(id, name));
            MapWords(card, CardPicture(card, LobbyMapSnapshots.For(id), LobbyIcons.Home), layout.Name, LobbyMapSnapshots.HouseCaption(id, layout));
            card.gameObject.AddComponent<LobbyHint>().Text = layout.Name + MapCardText.Separator + MapCardText.HouseLine(layout);
        }

        void HomeCard(Transform parent, HomeLayout home)
        {
            string name = SavedHomeCard + home.Map;
            var house = GameConfig.Current.HouseFor(home.Map);
            var card = MapCard(parent, name, Menu.SavedHome == home.Map,
                () => { if (Menu.ChooseHome(home.Map)) { Menu.NoteChoice(true); Refresh(); Reselect(name); } });
            MapWords(card, CardPicture(card, LobbyMapSnapshots.For(home.Map), LobbyIcons.Home), home.Name, "Your home" + MapCardText.Separator + house.Name);
        }

        void TutorialCard(RectTransform column)
        {
            var row = MapCards(column, TutorialRow);
            row.GetComponent<LobbyPlayCardGrid>().MaximumColumns = HouseRow;
            var card = MapCard(row, TutorialRoom, true, null);
            card.interactable = false;
            MapWords(card, CardPicture(card, null, LobbyIcons.Home, LobbyKit.ModeArt(LobbyMenu.TutorialMode)), TutorialRoom, TutorialLine);
        }

        void HowToPlay(RectTransform column, string mode, bool sameScreen = true)
        {
            var guide = ModeGuideBook.For(mode);
            if (guide == null || guide.Steps.Count == 0) return;
            int count = guide.Steps.Count;
            var strip = LobbyKit.Rect(column, GuideStrip);
            var content = LobbyKit.Rect(strip, "Content").Fill();
            var head = LobbyKit.SectionLabel(content, LobbyPlayDetail.GuideWord);
            head.name = GuideStripTitle;
            head.rectTransform.Place(new Vector2(0, 1), new Vector2(.5f, 1), new Vector2(0, -StripHead), Vector2.zero);
            string last = sameScreen ? ModeAccess.LastMatch(Menu.Career.History, mode) : "";
            if (last.Length > 0)
            {
                var result = LobbyKit.Text(content, last, 17, LobbyKit.Muted, TextAlignmentOptions.BottomRight);
                result.name = LastMatchLine;
                result.rectTransform.Place(new Vector2(.5f, 1), Vector2.one, new Vector2(0, -StripHead), Vector2.zero);
            }
            var device = ModeGuidePanel.Pressed(null) ?? ModeGuides.Fallback();
            for (int i = 0; i < count; i++) GuideStepCard(content, guide.Steps[i], i, count, device);
            var spare = strip.gameObject.AddComponent<LobbyPlaySpare>();
            spare.MinimumHeight = StripLowest; spare.MaximumHeight = StripTallest;
            spare.Watch(content, count * StepMinimumWidth + (count - 1) * StepSpacing);
        }

        static void GuideStepCard(Transform content, ModeGuideStep step, int index, int count, ControlDevice device)
        {
            float share = 1f / count;
            var card = LobbyKit.Rect(content, GuideStep + (index + 1)).Place(new Vector2(index * share, 0), new Vector2((index + 1) * share, 1),
                new Vector2(index * StepSpacing * share, 0), new Vector2(-(count - 1 - index) * StepSpacing * share, -StripHead - StripGap));
            LobbyKit.Face(card, LobbyKit.Card, StepRadius, LobbyKit.Line, 1, 0).raycastTarget = false;
            var art = LobbyKit.Rect(card, "Step art").Place(Vector2.zero, new Vector2(0, 1),
                new Vector2(StepPadding, StepPadding), new Vector2(StepPadding + StepArt, -StepPadding));
            LobbyKit.ItemImage(art, step.Art, StepArt);
            float words = StepPadding * 2 + StepArt;
            var title = LobbyKit.Text(card, (index + 1) + LobbyPlayDetail.StepSeparator + step.Title, 20, LobbyKit.Navy,
                TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            title.name = GuideStepTitle;
            title.enableAutoSizing = true; title.fontSizeMin = SmallestLabel; title.fontSizeMax = 20;
            title.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(words, -StepPadding - StepTitle), new Vector2(-StepPadding, -StepPadding));
            var line = LobbyKit.Text(card, ModeGuides.Line(step, device), 17, LobbyKit.Muted, TextAlignmentOptions.TopLeft);
            line.name = GuideStepLine;
            line.textWrappingMode = TextWrappingModes.Normal;
            line.enableAutoSizing = true; line.fontSizeMin = SmallestLabel; line.fontSizeMax = 17;
            line.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(words, StepPadding),
                new Vector2(-StepPadding, -StepPadding - StepTitle - StepLineGap));
        }


        RectTransform SummaryFrame(RectTransform body, string name) => authoredFrame
            ? LobbyKit.Rect(authoredFrame.Summary, name).Fill()
            : LobbyKit.Rect(body, name).Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 88));

        static void SummaryLine(RectTransform bar)
        {
            LobbyKit.Rect(bar, "Summary divider").Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -1), Vector2.zero)
                .Paint(LobbyKit.Line).raycastTarget = false;
        }

        void Footer(RectTransform body, bool final = true)
        {
            var bar = SummaryFrame(body, "Summary");
            SummaryLine(bar);
            string blocked = Menu.Blocked ?? Refusal(Menu);
            var title = LobbyKit.Text(bar, Menu.MatchTitle, 26, LobbyKit.Navy, TextAlignmentOptions.BottomLeft);
            title.enableAutoSizing = true; title.fontSizeMin = 20; title.fontSizeMax = 26;
            title.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 44), new Vector2(-250, -8));
            string detailText = blocked ?? Participants(Menu.Mode, Menu.PartySize);
            var detail = LobbyKit.Text(bar, detailText, 18, blocked == null ? LobbyKit.Muted : LobbyKit.Hot, TextAlignmentOptions.TopLeft);
            detail.name = "Mode detail";
            detail.gameObject.SetActive(!string.IsNullOrEmpty(detailText));
            detail.enableAutoSizing = true; detail.fontSizeMin = 14; detail.fontSizeMax = 18;
            detail.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 10), new Vector2(-250, -48));
            string word = Menu.Queue == LobbyMenu.Workshop ? PlayPageText.OpenWord : final ? "Play" : NextWord;
            Action action = final ? Menu.Go : ShowPlaces;
            var go = LobbyKit.Primary(bar, "GO", word, action, 42, 26);
            ((RectTransform)go.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-220, -30), new Vector2(-8, 30));
            go.interactable = blocked == null;
        }
    }

}
