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
    public sealed class PlayPage : LobbyPage
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

        static readonly Vector2 HouseSize = new Vector2(280, 300);
        const float ModeHeight = 52f;
        static readonly Color Tree = LobbyKit.Hex(0x2f9e3a);

        static readonly (string id, string label)[] Queues =
        {
            ("Queue " + LobbyMenu.Practice, "Practice (Offline)"),
            ("Queue " + LobbyMenu.Matchmaking, "Matchmaking"),
            ("Queue " + LobbyMenu.Workshop, "Create"),
        };

        string DisplayQueue => Menu.Queue == LobbyMenu.Practice && Menu.Mode == LobbyMenu.TutorialMode
            ? LobbyMenu.Workshop : Menu.Queue;

        protected override void Build()
        {
            authoredFrame = null; framePanel = null;
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

            if (Menu.Queue == LobbyMenu.Matchmaking)
            {
                Footer(body);
                First = queues.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == "Queue " + LobbyMenu.Practice);
                return;
            }

            var column = authoredFrame ? authoredFrame.Choices : LobbyKit.Column(body, "Choices", 8).Place(Vector2.zero, Vector2.one, new Vector2(0, 104), Vector2.zero);
            column = ScrollableChoices(column);
            if (DisplayQueue == LobbyMenu.Practice)
            {
                PracticeModes(column);
                if (WordboundSolo)
                {
                    SoloChoices(column, body);
                    return;
                }
            }
            else if (DisplayQueue == LobbyMenu.Workshop)
            {
                CreateModes(column);
                if (WordboundMaps) { WorkshopChoices(column, body); return; }
            }
            if (Menu.Mode == LobbyMenu.TutorialMode)
            {
                LobbyKit.Text(column, "Room", 18, LobbyKit.Muted).Size(-1, 32);
                TutorialRoom(column);
            }
            else
            {
                LobbyKit.SectionLabel(column, "Map");
                if (Menu.Mode == "Escape")
                {
                    // The Escape randomises the house, so this is the only choice it offers.
                    var randomRow = MapCards(column, "Houses");
                    RandomHouseCard(randomRow);
                }
                else
                {
                    var houses = MapCards(column, "Houses");
                    int index = 0;
                    foreach (var house in GameConfig.Current.Houses)
                        HouseCard(houses, house.Key, house.Value, index++);
                }
            }
            Footer(body);

            if (Aim != null)
            {
                var aimed = Root.GetComponentsInChildren<Selectable>().FirstOrDefault(s => s.name == Aim && s.IsInteractable());
                if (aimed) First = aimed;
                Aim = null;
            }
        }

        void SoloChoices(RectTransform column, RectTransform body)
        {
            LobbyKit.SectionLabel(column, "Map");
            var maps = MapCards(column, "Solo maps");
            var mapGrid = maps.GetComponent<LobbyPlayCardGrid>();
            mapGrid.PreviewAspectRatio = 16f / 9f;
            SoloMapCard(maps, WordboundSoloEntry.LostAddressMap, "Lost Address", "Original course", "BOX", LobbyKit.Sun);
            SoloMapCard(maps, WordboundSoloEntry.ParcelHallMap, "Parcel Hall", "Furnished hall", "TABLE", LobbyKit.Lime);
            SoloMapCard(maps, WordboundSoloEntry.ParcelServiceMap, "Parcel Service", "Three-room office", null, LobbyKit.Cyan);
            var bar = SummaryFrame(body, "Solo summary");
            SummaryLine(bar);
            string blocked = Menu.PartySize != 1 ? "Solo only. Leave extra local seats to start." :
                !Session.CanLoad(Session.WordboundScene) ? "Wordbound is unavailable in this build." : null;
            var heading = LobbyKit.Text(bar, LobbyPlayModeLabels.Name(WordboundSoloEntry.SelectedMode), 26, LobbyKit.Navy);
            heading.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 44), new Vector2(-250, -8));
            var detail = LobbyKit.Text(bar, blocked ?? (WordboundSoloEntry.SelectedMode == "TimeRun"
                ? "Solve the delivery circuit. 1 player."
                : "Explore, craft and try the puzzles. 1 player."), 18,
                blocked == null ? LobbyKit.Muted : LobbyKit.Hot, TextAlignmentOptions.TopLeft);
            detail.name = "Solo detail";
            detail.enableAutoSizing = true; detail.fontSizeMin = 14; detail.fontSizeMax = 18;
            detail.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 10), new Vector2(-250, -48));
            var go = LobbyKit.Primary(bar, "Solo GO", "Play", () => Menu.OpenWordboundSolo(WordboundSoloEntry.SelectedMode), 42, 26);
            ((RectTransform)go.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-220, -30), new Vector2(-8, 30));
            go.interactable = blocked == null;
        }

        void WorkshopChoices(RectTransform column, RectTransform body)
        {
            LobbyKit.SectionLabel(column, "Map");
            var row = MapCards(column, "Wordbound workshop");
            var card = MapCard(row, "Wordbound maps", LobbyKit.Lime, true, Menu.OpenWordboundWorkshop);
            var art = LobbyKit.ItemImage(card.Body(), "TABLE", 184);
            if (art) art.rectTransform.Pin(new Vector2(.5f, 1), new Vector2(0, -18), new Vector2(184, 184));
            MapWords(card.Body(), "Wordbound maps", "Build & playtest");
            bool available = Session.CanLoad(Session.WordboundScene);
            card.interactable = available;
            var bar = SummaryFrame(body, "Workshop summary");
            SummaryLine(bar);
            var detail = LobbyKit.Text(bar, available ? "Local editor. One-player playtests." : "Wordbound is unavailable in this build.",
                18, available ? LobbyKit.Muted : LobbyKit.Hot, TextAlignmentOptions.MidlineLeft);
            detail.name = "Workshop detail";
            detail.enableAutoSizing = true; detail.fontSizeMin = 14; detail.fontSizeMax = 18;
            detail.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 10), new Vector2(-250, -10));
            var open = LobbyKit.Primary(bar, "Workshop OPEN", "Open", Menu.OpenWordboundWorkshop, 32, 24);
            ((RectTransform)open.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-220, -30), new Vector2(-8, 30));
            open.interactable = available;
            First = open;
        }

        void PickQueue(string id)
        {
            string queue = id.Substring("Queue ".Length);
            if (queue == LobbyMenu.Matchmaking)
            {
                Menu.OpenWordbound();
                return;
            }
            string mode = queue == LobbyMenu.Practice && !LobbyMenu.Modes.Contains(Menu.Mode)
                ? LobbyMenu.Modes[0] : null;
            Menu.Choose(queue: queue, mode: mode);
            Refresh();
            Reselect(id);
        }

        static string QueueLine(string queue) => queue == LobbyMenu.Matchmaking
            ? "Human players online."
            : queue == LobbyMenu.Workshop ? "Create & learn." : "Play on this PC.";

        void PracticeModes(RectTransform column)
        {
            var row = ModeRow(column, "Modes", 7);
            foreach (var (mode, title, tag) in ModesFor(LobbyMenu.Practice))
            {
                string id = "Mode " + mode;
                var button = CompactMode(row, id, LobbyPlayModeLabels.Name(mode), !WordboundSolo && Menu.Mode == mode,
                    () => { WordboundSolo = false; Menu.Choose(queue: LobbyMenu.Practice, mode: mode); Refresh(); Reselect(id); });
                if (!WordboundSolo && Menu.Mode == mode) First = button;
            }
            foreach (string mode in new[] { "TimeRun", "Freestyle" })
            {
                string id = "Solo " + mode;
                var button = CompactMode(row, id, LobbyPlayModeLabels.Name(mode), WordboundSolo && WordboundSoloEntry.SelectedMode == mode,
                    () => { WordboundSolo = true; WordboundSoloEntry.Select(mode); Refresh(); Reselect(id); });
                if (WordboundSolo && WordboundSoloEntry.SelectedMode == mode) First = button;
            }
            row.GetComponent<LobbyPlayModeDock>()?.Fit();
        }

        void CreateModes(RectTransform column)
        {
            var row = ModeRow(column, "Create modes", 3);
            var choices = new[] { (id: "Create house", title: "Home builder"),
                (id: "Create Wordbound", title: "Map editor"), (id: "Mode " + LobbyMenu.TutorialMode, title: "Tutorial") };
            foreach (var choice in choices)
            {
                bool tutorial = choice.id == "Mode " + LobbyMenu.TutorialMode;
                bool selected = tutorial ? !WordboundMaps && Menu.Mode == LobbyMenu.TutorialMode :
                    choice.id == "Create Wordbound" ? WordboundMaps : !WordboundMaps && Menu.Mode != LobbyMenu.TutorialMode;
                var button = CompactMode(row, choice.id, choice.title, selected, () => {
                    WordboundMaps = choice.id == "Create Wordbound";
                    if (tutorial) Menu.Choose(queue: LobbyMenu.Workshop, mode: LobbyMenu.TutorialMode);
                    else if (!WordboundMaps) Menu.Choose(queue: LobbyMenu.Workshop, mode: LobbyMenu.WorkshopMode);
                    Refresh(); Reselect(choice.id);
                });
                if (selected) First = button;
            }
            row.GetComponent<LobbyPlayModeDock>()?.Fit();
        }

        RectTransform ModeRow(Transform parent, string name, int maximum)
        {
            var row = Cards(authoredFrame ? authoredFrame.Body : parent, name,
                playLayout ? playLayout.ModeMinimumWidth : 104, playLayout ? playLayout.ModeHeight : ModeHeight, maximum);
            if (authoredFrame)
            {
                row.GetComponent<LayoutElement>().ignoreLayout = true;
                var dock = row.gameObject.AddComponent<LobbyPlayModeDock>();
                dock.Choices = authoredFrame.Choices;
                dock.MaximumWidth = playLayout ? playLayout.ModeMaximumWidth : 1160;
            }
            return row;
        }

        static Button CompactMode(Transform parent, string id, string label, bool selected, Action action) =>
            LobbyKit.TextAction(parent, id, label, 20, action, selected);

        RectTransform MapCards(Transform parent, string name)
        {
            var row = Cards(parent, name, playLayout ? playLayout.MapMinimumWidth : 264,
                playLayout ? playLayout.MapMinimumHeight : 340, playLayout ? playLayout.MapColumns : 3);
            row.GetComponent<LobbyPlayCardGrid>().PreviewAspectRatio = 16f / 9f;
            return row;
        }

        static IEnumerable<(string mode, string title, string tag)> ModesFor(string queue)
        {
            if (queue == LobbyMenu.Workshop)
            {
                yield return (LobbyMenu.TutorialMode, LobbyMenu.ModeName(LobbyMenu.TutorialMode), "Learn");
                yield return (LobbyMenu.WorkshopMode, LobbyMenu.ModeName(LobbyMenu.WorkshopMode), "Build");
                yield break;
            }
            yield return ("Dibs", LobbyMenu.ModeName("Dibs"), "Free for all");
            yield return ("Duos", LobbyMenu.ModeName("Duos"), "2 v 2");
            yield return ("MovingOut", LobbyMenu.ModeName("MovingOut"), "Co-op");
            yield return ("MovingDay", LobbyMenu.ModeName("MovingDay"), "Co-op");
            yield return ("Escape", LobbyMenu.ModeName("Escape"), "Co-op");
        }

        static string Objective(string mode) => mode switch
        {
            "Dibs" => "Smash, spell, survive. Win three rounds.",
            "Duos" => "Win three rounds. Revive your partner.",
            "MovingOut" => "Bring every keepsake to the van.",
            "MovingDay" => "Furnish the marked rooms in time.",
            "Escape" => "Unlock the doors. Get everyone out.",
            LobbyMenu.TutorialMode => "Try smashing, spelling and crafting.",
            LobbyMenu.WorkshopMode => "Build and tour your home.",
            _ => "Choose a mode.",
        };

        static string Participants(string mode, int humans)
        {
            if (mode == LobbyMenu.WorkshopMode) return "Solo";
            string seats = LobbyMenu.Seats(mode, humans);
            return string.IsNullOrEmpty(seats) ? humans == 1 ? "1 player" : humans + " players" : Capital(seats);
        }

        static RectTransform Cards(Transform parent, string name, float minimumWidth, float height, int maximumColumns)
        {
            var rect = LobbyKit.Grid(parent, name, new Vector2(minimumWidth, height), 12).Size(-1, height);
            var flow = rect.gameObject.AddComponent<LobbyPlayCardGrid>();
            flow.MinimumWidth = minimumWidth; flow.CardHeight = height; flow.MaximumColumns = maximumColumns;
            return rect;
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

        static string ShortMode(string mode, string fallback) => mode switch
        {
            "Dibs" => "Dibs!", "Duos" => "Duos", "MovingOut" => "Moving Out", "MovingDay" => "Moving Day",
            "Escape" => "Escape", LobbyMenu.TutorialMode => "Tutorial", LobbyMenu.WorkshopMode => "Home builder", _ => fallback,
        };

        static Button MapCard(Transform parent, string name, Color colour, bool selected, Action click)
        {
            var card = LobbyKit.Button(parent, name, LobbyKit.Card, click, 14,
                selected ? LobbyKit.Selection : LobbyKit.Line, selected ? 3 : 1, 0);
            var press = card.GetComponent<LobbyPress>();
            press.Lift = 0; press.Tilt = 0; press.Sink = 1;
            if (selected)
            {
                var mark = LobbyKit.Rect(card.Body(), "Selected").Pin(Vector2.one, new Vector2(-8, -8), new Vector2(26, 26));
                mark.Paint(LobbyKit.Selection, 13).raycastTarget = false;
                LobbyKit.Icon(mark, LobbyIcons.Check, LobbyKit.OnSelection).rectTransform.Fill().offsetMin = new Vector2(5, 5);
                mark.GetChild(0).GetComponent<RectTransform>().offsetMax = new Vector2(-5, -5);
            }
            return card;
        }

        void SoloMapCard(Transform parent, string id, string title, string detail, string artId, Color colour)
        {
            string name = "Solo map " + id;
            var card = MapCard(parent, name, colour, WordboundSoloEntry.SelectedMap == id, () => {
                WordboundSoloEntry.SelectMap(id); Refresh(); Reselect(name);
            });
            var preview = !playLayout ? null : id switch
            {
                WordboundSoloEntry.LostAddressMap => playLayout.LostAddressPreview,
                WordboundSoloEntry.ParcelHallMap => playLayout.ParcelHallPreview,
                WordboundSoloEntry.ParcelServiceMap => playLayout.ParcelServicePreview,
                _ => null,
            };
            if (preview)
            {
                var art = LobbyKit.Rect(card.Body(), "Map preview").Place(Vector2.zero, Vector2.one,
                    new Vector2(18, 88), new Vector2(-18, -18)).gameObject.AddComponent<Image>();
                art.sprite = preview; art.preserveAspect = true; art.color = Color.white; art.raycastTarget = false;
                art.transform.SetAsFirstSibling();
            }
            else if (artId == null)
                LobbyKit.Icon(card.Body(), LobbyIcons.Layers, LobbyKit.Cyan).rectTransform
                    .Pin(new Vector2(.5f, 1), new Vector2(0, -28), new Vector2(136, 136));
            else
            {
                var art = LobbyKit.ItemImage(card.Body(), artId, 184);
                if (art) art.rectTransform.Pin(new Vector2(.5f, 1), new Vector2(0, -18), new Vector2(184, 184));
            }
            MapWords(card.Body(), title, detail);
        }

        static void MapWords(RectTransform body, string title, string detail)
        {
            var name = LobbyKit.Text(body, title, 26, LobbyKit.Navy, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            name.name = "Map title";
            name.enableAutoSizing = true; name.fontSizeMin = 20; name.fontSizeMax = 26;
            name.rectTransform.Place(Vector2.zero, new Vector2(1, 0), new Vector2(18, 40), new Vector2(-18, 80));
            var text = LobbyKit.Text(body, detail, 18, LobbyKit.Navy);
            text.rectTransform.Place(Vector2.zero, new Vector2(1, 0), new Vector2(18, 12), new Vector2(-18, 40));
        }

        void RandomHouseCard(Transform parent)
        {
            var card = MapCard(parent, "Map random", LobbyKit.Sun, Menu.Map == Session.LobbyRandomMap,
                () => { Menu.Choose(map: Session.LobbyRandomMap); Refresh(); Reselect("Map random"); });
            LobbyKit.Icon(card.Body(), LobbyIcons.Home, LobbyKit.Cyan).rectTransform
                .Pin(new Vector2(.5f, 1), new Vector2(0, -28), new Vector2(136, 136));
            MapWords(card.Body(), "Random house", "New each run");
        }

        void HouseCard(Transform parent, string id, HouseLayout layout, int index)
        {
            bool open = Menu.Mode != LobbyMenu.WorkshopMode || HomeDesigner.Supports(id);
            var card = MapCard(parent, "Map " + id, index % 2 == 0 ? LobbyKit.Hot : LobbyKit.Lime, Menu.Map == id,
                () => { Menu.Choose(map: id); Refresh(); Reselect("Map " + id); });
            if (!open)
            {
                card.interactable = false;
                card.gameObject.AddComponent<LobbyHint>().Text = "The workshop builds homes in " + string.Join(" and ",
                    GameConfig.Current.Houses.Where(h => HomeDesigner.Supports(h.Key)).Select(h => h.Value.Name));
            }
            var plan = LobbyKit.Rect(card.Body(), "Plan").Place(Vector2.zero, Vector2.one,
                new Vector2(18, 88), new Vector2(-18, -18));
            var drawing = LobbyKit.Rect(plan, "Fitted plan").Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(500, 281.25f));
            drawing.gameObject.AddComponent<LobbyPlayPreviewFit>();
            FloorPlan(drawing, layout, Color.white, new Vector2(500, 281.25f));
            MapWords(card.Body(), layout.Name, Facts(layout));
        }

        static void TutorialRoom(Transform parent)
        {
            var note = LobbyKit.Rect(parent, "Tutorial room").Size(-1, HouseSize.y);
            LobbyKit.Face(note, LobbyKit.Card, 14, LobbyKit.Line, 1, 0);
            var art = LobbyKit.ItemImage(note, LobbyKit.ModeArt(LobbyMenu.TutorialMode), 172);
            if (art) art.rectTransform.Pin(new Vector2(0, .5f), new Vector2(24, 0), new Vector2(172, 172));
            var title = LobbyKit.Text(note, "Play & learn", 26, LobbyKit.Navy);
            title.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(224, 150), new Vector2(-24, -70));
            var text = Wrapped(note, "Your training room is ready.", 19, LobbyKit.Muted);
            text.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(224, 90), new Vector2(-24, -154));
        }

        RectTransform SummaryFrame(RectTransform body, string name) => authoredFrame
            ? LobbyKit.Rect(authoredFrame.Summary, name).Fill()
            : LobbyKit.Rect(body, name).Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 88));

        static void SummaryLine(RectTransform bar)
        {
            LobbyKit.Rect(bar, "Summary divider").Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -1), Vector2.zero)
                .Paint(LobbyKit.Line).raycastTarget = false;
        }

        void Footer(RectTransform body)
        {
            var bar = SummaryFrame(body, "Summary");
            SummaryLine(bar);
            string blocked = Menu.Blocked;
            string where = Menu.Mode == LobbyMenu.TutorialMode ? "The tutorial room" : MapName(Menu.Map);
            var title = LobbyKit.Text(bar, ShortMode(Menu.Mode, LobbyMenu.ModeName(Menu.Mode)) + " · " + where, 26, LobbyKit.Navy, TextAlignmentOptions.BottomLeft);
            title.enableAutoSizing = true; title.fontSizeMin = 20; title.fontSizeMax = 26;
            title.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 44), new Vector2(-250, -8));
            string detailText = blocked ?? Objective(Menu.Mode) + "  ·  " + Participants(Menu.Mode, Menu.PartySize);
            var detail = LobbyKit.Text(bar, detailText, 18, blocked == null ? LobbyKit.Muted : LobbyKit.Hot, TextAlignmentOptions.TopLeft);
            detail.name = "Mode detail";
            detail.gameObject.SetActive(!string.IsNullOrEmpty(detailText));
            detail.enableAutoSizing = true; detail.fontSizeMin = 14; detail.fontSizeMax = 18;
            detail.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 10), new Vector2(-250, -48));
            var go = LobbyKit.Primary(bar, "GO", "Play", Menu.Go, 42, 26);
            ((RectTransform)go.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-220, -30), new Vector2(-8, 30));
            go.interactable = blocked == null;
        }
        static string Facts(HouseLayout layout)
        {
            int floors = layout.StoreyFloors().Count;
            int rooms = layout.Rooms.Count(r => !IsPassage(r.Name));
            return floors + (floors == 1 ? " floor · " : " floors · ") + rooms + (rooms == 1 ? " room" : " rooms");
        }

        static bool IsPassage(string room) => room.Contains("Hall") || room.Contains("Landing");

        public static void FloorPlan(RectTransform box, HouseLayout layout, Color colour, Vector2 size)
        {
            if (layout.Rooms.Count == 0) return;
            const float Gap = 12f, LabelHeight = 24f;
            float minX = layout.Rooms.Min(r => r.MinX), maxX = layout.Rooms.Max(r => r.MaxX);
            float minZ = layout.Rooms.Min(r => r.MinZ), maxZ = layout.Rooms.Max(r => r.MaxZ);
            int storeys = layout.StoreyFloors().Count;
            float label = storeys > 1 ? LabelHeight : 0f;
            float column = (size.x - Gap * (storeys - 1)) / storeys;
            float scale = Mathf.Min(column / Mathf.Max(1f, maxX - minX), (size.y - label) / Mathf.Max(1f, maxZ - minZ));
            var middle = new Vector2((minX + maxX) * .5f, (minZ + maxZ) * .5f);
            var planSize = new Vector2(maxX - minX, maxZ - minZ) * scale;
            for (int storey = 0; storey < storeys; storey++)
            {
                var offset = new Vector2((storey - (storeys - 1) * .5f) * (planSize.x + Gap), -label * .5f);
                Vector2 At(float x, float z) => offset + (new Vector2(x, z) - middle) * scale;
                foreach (var room in layout.Rooms)
                {
                    if (layout.StoreyOf(room) != storey) continue;
                    var rect = LobbyKit.Rect(box, room.Name).Pin(new Vector2(.5f, .5f), At((room.MinX + room.MaxX) * .5f, (room.MinZ + room.MaxZ) * .5f),
                        new Vector2(room.MaxX - room.MinX, room.MaxZ - room.MinZ) * scale + new Vector2(2, 2));
                    bool garden = room.Name.Contains("Garden");
                    var fill = garden ? LobbyKit.Lime : IsPassage(room.Name) ? LobbyKit.Sun : colour;
                    rect.Paint(fill, 3).raycastTarget = false;
                    LobbyKit.Frame(rect, LobbyKit.ArtInk, 3, 3);
                    if (garden)
                    {
                        float crown = Mathf.Min(rect.sizeDelta.x, rect.sizeDelta.y) * .42f;
                        var tree = LobbyKit.Rect(rect, "Tree").Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(crown, crown));
                        LobbyKit.Face(tree, Tree, Mathf.RoundToInt(crown / 2f), LobbyKit.ArtInk, 3);
                    }
                }
                if (storeys > 1)
                    foreach (var s in layout.Stairs)
                    {
                        if (layout.StoreyOf(layout.Room(s.Lower)) != storey && layout.StoreyOf(layout.Room(s.Upper)) != storey) continue;
                        var flight = LobbyKit.Rect(box, "Stairs").Pin(new Vector2(.5f, .5f), At((s.MinX + s.MaxX) * .5f, (s.MinZ + s.MaxZ) * .5f),
                            new Vector2(s.MaxX - s.MinX, s.MaxZ - s.MinZ) * scale);
                        flight.Paint(new Color(LobbyKit.ArtInk.r, LobbyKit.ArtInk.g, LobbyKit.ArtInk.b, .55f)).raycastTarget = false;
                    }
                foreach (var spawn in layout.Spawns)
                {
                    var room = layout.Room(spawn.Room);
                    if (room == null || layout.StoreyOf(room) != storey) continue;
                    var dot = LobbyKit.Rect(box, "Start").Pin(new Vector2(.5f, .5f), At(spawn.X, spawn.Z), new Vector2(11, 11));
                    LobbyKit.Face(dot, LobbyKit.Wood, 6, LobbyKit.Cocoa, 2);
                }
                if (storeys == 1) continue;
                var tag = LobbyKit.Rect(box, "Storey").Pin(new Vector2(.5f, .5f), offset + new Vector2(0f, (planSize.y + label) * .5f),
                    new Vector2(Mathf.Min(planSize.x + Gap, 120f), label - 4f));
                tag.Paint(LobbyKit.ArtInk, 7).raycastTarget = false;
                var name = LobbyKit.Display(tag, layout.StoreyLabel(storey).Replace(" FLOOR", ""), 14, Color.white);
                name.characterSpacing = 1;
                name.enableAutoSizing = true; name.fontSizeMin = 12; name.fontSizeMax = 14;
                name.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, 0));
            }
        }
    }

}
