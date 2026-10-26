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

        static readonly Vector2 ModeSize = new Vector2(348, 260), HouseSize = new Vector2(346, 244);
        static readonly Color ArtShadow = new Color(LobbyKit.Navy.r, LobbyKit.Navy.g, LobbyKit.Navy.b, .4f);
        static readonly Color Tree = LobbyKit.Hex(0x2f9e3a);

        static readonly (string id, string label)[] Queues =
        {
            ("Queue " + LobbyMenu.Practice, "Local"),
            ("Queue " + LobbyMenu.Matchmaking, "Online"),
            ("Queue " + LobbyMenu.Workshop, "Create"),
        };

        string DisplayQueue => Menu.Queue == LobbyMenu.Practice && Menu.Mode == LobbyMenu.TutorialMode
            ? LobbyMenu.Workshop : Menu.Queue;

        protected override void Build()
        {
            var body = Panel(Vector2.zero, Vector2.one, "Play", QueueLine(DisplayQueue));
            if ((DisplayQueue == LobbyMenu.Practice && WordboundSolo) ||
                (DisplayQueue == LobbyMenu.Workshop && WordboundMaps))
            {
                var fit = body.parent.gameObject.AddComponent<LobbyCompactPanel>();
                fit.MaximumSize = new Vector2(1100, 600);
                fit.Fit();
            }
            var queues = LobbyKit.Segmented(body.parent, "Queues", Queues, "Queue " + DisplayQueue, PickQueue, 52, 20);
            queues.Place(Vector2.one, Vector2.one, new Vector2(-688, -78), new Vector2(-88, -26));

            if (Menu.Queue == LobbyMenu.Matchmaking)
            {
                Footer(body);
                First = queues.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == "Queue " + LobbyMenu.Practice);
                return;
            }

            var column = LobbyKit.Column(body, "Choices", 8).Place(Vector2.zero, Vector2.one, new Vector2(0, 104), Vector2.zero);
            if (DisplayQueue == LobbyMenu.Practice)
            {
                LobbyKit.Segmented(column, "Local families", new[] {
                    ("Family house", "House games"), ("Family Wordbound", "Wordbound solo")
                }, WordboundSolo ? "Family Wordbound" : "Family house", id => {
                    WordboundSolo = id == "Family Wordbound";
                    Refresh(); Reselect(id);
                }, 44, 18);
                if (WordboundSolo)
                {
                    SoloChoices(column, body);
                    return;
                }
            }
            else if (DisplayQueue == LobbyMenu.Workshop)
            {
                LobbyKit.Segmented(column, "Create families", new[] {
                    ("Create house", "Home & tutorial"), ("Create Wordbound", "Wordbound maps")
                }, WordboundMaps ? "Create Wordbound" : "Create house", id => {
                    WordboundMaps = id == "Create Wordbound";
                    Refresh(); Reselect(id);
                }, 44, 18);
                if (WordboundMaps) { WorkshopChoices(column, body); return; }
            }
            else LobbyKit.SectionLabel(column, "Mode");
            var modes = ModesFor(DisplayQueue).ToList();
            var posters = Centred(LobbyKit.FitRow(column, "Modes", ModeSize, 16, modes.Count));
            Selectable chosen = null;
            foreach (var (mode, title, tag) in modes)
            {
                var poster = ModePoster(posters, mode, title, tag, Menu.Mode == mode);
                if (Menu.Mode == mode) chosen = poster;
            }
            LobbyKit.Rect(column, "Gap").Size(-1, 8);
            if (Menu.Mode == LobbyMenu.TutorialMode)
            {
                LobbyKit.SectionLabel(column, "Room");
                TutorialRoom(column);
            }
            else
            {
                LobbyKit.SectionLabel(column, "House");
                if (Menu.Mode == "Escape")
                {
                    // The Escape randomises the house, so this is the only choice it offers.
                    var randomRow = Centred(LobbyKit.FitRow(column, "Houses", HouseSize, 18, 1));
                    RandomHouseCard(randomRow);
                }
                else
                {
                    var houses = Centred(LobbyKit.FitRow(column, "Houses", HouseSize, 18, GameConfig.Current.Houses.Count));
                    int index = 0;
                    foreach (var house in GameConfig.Current.Houses)
                        HouseCard(houses, house.Key, house.Value, index++);
                }
            }
            Footer(body);

            First = chosen;
            if (Aim != null)
            {
                var aimed = Root.GetComponentsInChildren<Selectable>().FirstOrDefault(s => s.name == Aim && s.IsInteractable());
                if (aimed) First = aimed;
                Aim = null;
            }
        }

        void SoloChoices(RectTransform column, RectTransform body)
        {
            var row = Centred(LobbyKit.FitRow(column, "Solo modes", ModeSize, 16, 2));
            foreach (string mode in new[] { "TimeRun", "Freestyle" })
            {
                bool timed = mode == "TimeRun";
                var card = Poster(row, "Solo " + mode, timed ? LobbyKit.Sun : LobbyKit.Lime,
                    WordboundSoloEntry.SelectedMode == mode,
                    () => { WordboundSoloEntry.Select(mode); Refresh(); Reselect("Solo " + mode); }, ModeSize);
                var art = LobbyKit.ItemImage(card.Body(), timed ? "CLOCK" : "BOX", 136);
                if (art)
                {
                    art.rectTransform.Pin(new Vector2(.5f, 1), new Vector2(0, -14), new Vector2(136, 136));
                    LobbyKit.Drop(art.rectTransform, 6, ArtShadow);
                    Wiggle(card, art.rectTransform);
                }
                var words = Words(card.Body(), 16);
                var title = LobbyKit.Display(words, timed ? "Time Run" : "Freestyle", 34, LobbyKit.Cream,
                    TextAlignmentOptions.BottomLeft, LobbyKit.Ink.Stroke);
                title.Size(-1, 42);
                TagLine(words, timed ? "Against the clock" : "Explore & practise");
                if (WordboundSoloEntry.SelectedMode == mode) First = card;
            }
            var bar = LobbyKit.Rect(body, "Solo summary").Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 88));
            LobbyKit.Face(bar, LobbyKit.ChipFill, 18, LobbyKit.Line, 2);
            string blocked = Menu.PartySize != 1 ? "Solo only. Leave extra local seats to start." :
                !Session.CanLoad(Session.WordboundScene) ? "Wordbound is unavailable in this build." : null;
            var heading = LobbyKit.Display(bar, "WORDBOUND  ·  SOLO", 28, LobbyKit.Cream, TextAlignmentOptions.BottomLeft);
            heading.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(20, 44), new Vector2(-300, -8));
            var detail = LobbyKit.Text(bar, blocked ?? (WordboundSoloEntry.SelectedMode == "TimeRun"
                ? "Solve the delivery circuit. 1 player."
                : "Explore, craft and try the puzzles. 1 player."), 18,
                blocked == null ? LobbyKit.Muted : LobbyKit.Sun, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            detail.name = "Solo detail";
            detail.enableAutoSizing = true; detail.fontSizeMin = 14; detail.fontSizeMax = 18;
            detail.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(20, 10), new Vector2(-300, -48));
            var go = LobbyKit.Primary(bar, "Solo GO", "GO", () => Menu.OpenWordboundSolo(WordboundSoloEntry.SelectedMode), 48, 30);
            ((RectTransform)go.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-268, -34), new Vector2(-12, 34));
            go.interactable = blocked == null;
        }

        void WorkshopChoices(RectTransform column, RectTransform body)
        {
            var row = Centred(LobbyKit.FitRow(column, "Wordbound workshop", ModeSize, 16, 1));
            var card = Poster(row, "Wordbound maps", LobbyKit.Lime, true, Menu.OpenWordboundWorkshop, ModeSize);
            var art = LobbyKit.ItemImage(card.Body(), "TABLE", 136);
            if (art)
            {
                art.rectTransform.Pin(new Vector2(.5f, 1), new Vector2(0, -14), new Vector2(136, 136));
                LobbyKit.Drop(art.rectTransform, 6, ArtShadow);
            }
            var words = Words(card.Body(), 16);
            var title = LobbyKit.Display(words, "Wordbound maps", 30, LobbyKit.Cream, TextAlignmentOptions.BottomLeft, LobbyKit.Ink.Stroke);
            title.enableAutoSizing = true; title.fontSizeMin = 24; title.fontSizeMax = 30;
            title.Size(-1, 42);
            TagLine(words, "Build & playtest");
            bool available = Session.CanLoad(Session.WordboundScene);
            card.interactable = available;
            var bar = LobbyKit.Rect(body, "Workshop summary").Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 88));
            LobbyKit.Face(bar, LobbyKit.ChipFill, 18, LobbyKit.Line, 2);
            var detail = LobbyKit.Text(bar, available ? "Local editor. One-player playtests." : "Wordbound is unavailable in this build.",
                18, available ? LobbyKit.Muted : LobbyKit.Sun, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            detail.name = "Workshop detail";
            detail.enableAutoSizing = true; detail.fontSizeMin = 14; detail.fontSizeMax = 18;
            detail.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(20, 10), new Vector2(-300, -10));
            var open = LobbyKit.Primary(bar, "Workshop OPEN", "OPEN", Menu.OpenWordboundWorkshop, 48, 30);
            ((RectTransform)open.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-268, -34), new Vector2(-12, 34));
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
            ? "House modes are local. Choose Local or Online."
            : queue == LobbyMenu.Workshop ? "Learn or build." : "Play on this PC.";

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

        static RectTransform Centred(RectTransform row)
        {
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(.5f, 1);
            row.anchoredPosition = Vector2.zero;
            return row;
        }

        Button ModePoster(Transform parent, string mode, string title, string tag, bool on)
        {
            var card = Poster(parent, "Mode " + mode, LobbyKit.ModeColour(mode), on,
                () => { Menu.Choose(queue: DisplayQueue, mode: mode); Refresh(); Reselect("Mode " + mode); }, ModeSize);
            var body = card.Body();
            var art = LobbyKit.ItemImage(body, LobbyKit.ModeArt(mode), 136);
            if (art)
            {
                art.rectTransform.Pin(new Vector2(.5f, 1), new Vector2(0, -14), new Vector2(136, 136));
                LobbyKit.Drop(art.rectTransform, 6, ArtShadow);
                Wiggle(card, art.rectTransform);
            }
            var words = Words(body, 16);
            var head = LobbyKit.Display(words, title, 34, LobbyKit.Cream, TextAlignmentOptions.BottomLeft, LobbyKit.Ink.Stroke);
            head.enableAutoSizing = true; head.fontSizeMin = 24; head.fontSizeMax = 34;
            head.Size(-1, 42);
            TagLine(words, tag);
            return card;
        }

        void RandomHouseCard(Transform parent)
        {
            var card = Poster(parent, "Map random", LobbyKit.Sun, Menu.Map == Session.LobbyRandomMap,
                () => { Menu.Choose(map: Session.LobbyRandomMap); Refresh(); Reselect("Map random"); }, HouseSize);
            var body = card.Body();
            var icon = LobbyKit.Icon(body, LobbyIcons.Home, LobbyKit.Navy);
            icon.rectTransform.Pin(new Vector2(.5f, 1), new Vector2(0, -20), new Vector2(110, 110));
            var words = Words(body, 14);
            var title = LobbyKit.Display(words, "Random house", 30, LobbyKit.Cream, TextAlignmentOptions.BottomLeft, LobbyKit.Ink.Stroke);
            title.enableAutoSizing = true; title.fontSizeMin = 22; title.fontSizeMax = 30;
            title.Size(-1, 36);
            TagLine(words, "New each run");
        }

        void HouseCard(Transform parent, string id, HouseLayout layout, int index)
        {
            bool open = Menu.Mode != LobbyMenu.WorkshopMode || HomeDesigner.Supports(id);
            var card = Poster(parent, "Map " + id, index % 2 == 0 ? LobbyKit.Hot : LobbyKit.Lime, Menu.Map == id,
                () => { Menu.Choose(map: id); Refresh(); Reselect("Map " + id); }, HouseSize);
            if (!open)
            {
                card.interactable = false;
                card.gameObject.AddComponent<LobbyHint>().Text = "The workshop builds homes in " + string.Join(" and ",
                    GameConfig.Current.Houses.Where(h => HomeDesigner.Supports(h.Key)).Select(h => h.Value.Name));
            }
            var body = card.Body();
            var art = LobbyKit.Rect(body, "Art").Place(new Vector2(0, 1), Vector2.one, new Vector2(18, -150), new Vector2(-18, -14));
            var plan = LobbyKit.Rect(art, "Plan").Fill();
            plan.localRotation = Quaternion.Euler(0, 0, 4f);
            FloorPlan(plan, layout, Color.white, new Vector2(HouseSize.x - 60, 128));
            Wiggle(card, art);
            var words = Words(body, 14);
            var name = LobbyKit.Display(words, layout.Name, 30, LobbyKit.Cream, TextAlignmentOptions.BottomLeft, LobbyKit.Ink.Stroke);
            name.enableAutoSizing = true; name.fontSizeMin = 22; name.fontSizeMax = 30;
            name.Size(-1, 36);
            TagLine(words, Facts(layout));
        }

        static void TutorialRoom(Transform parent)
        {
            var note = LobbyKit.Rect(parent, "Tutorial room").Size(-1, HouseSize.y);
            LobbyKit.Face(note, LobbyKit.ChipFill, 20, LobbyKit.Line, 2);
            var art = LobbyKit.ItemImage(note, LobbyKit.ModeArt(LobbyMenu.TutorialMode), 160, 8f);
            if (art) art.rectTransform.Pin(new Vector2(0, .5f), new Vector2(48, 0), new Vector2(160, 160));
            var title = LobbyKit.Display(note, "PLAY & LEARN", 36, LobbyKit.Sun, TextAlignmentOptions.BottomLeft, LobbyKit.Ink.Drop);
            title.characterSpacing = 2;
            title.rectTransform.Place(new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(250, 8), new Vector2(-48, 64));
            var text = Wrapped(note, "Your training room is ready.", 20, LobbyKit.Muted);
            text.rectTransform.Place(new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(250, -76), new Vector2(-48, -4));
        }

        void Footer(RectTransform body)
        {
            var bar = LobbyKit.Rect(body, "Summary").Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 88));
            LobbyKit.Face(bar, LobbyKit.ChipFill, 18, LobbyKit.Line, 2);
            string blocked = Menu.Blocked;
            var badge = LobbyKit.Rect(bar, "Badge").Pin(new Vector2(0, .5f), new Vector2(14, 0), new Vector2(64, 64));
            LobbyKit.Face(badge, LobbyKit.ModeColour(Menu.Mode), 14, LobbyKit.Navy, 3);
            LobbyKit.ItemImage(badge, LobbyKit.ModeArt(Menu.Mode), 52, 8f);
            string where = Menu.Mode == LobbyMenu.TutorialMode ? "The tutorial room" : MapName(Menu.Map);
            var title = LobbyKit.Display(bar, LobbyKit.Upper(LobbyMenu.ModeName(Menu.Mode) + "  ·  " + where), 28, LobbyKit.Cream, TextAlignmentOptions.BottomLeft);
            title.characterSpacing = 1;
            title.enableAutoSizing = true; title.fontSizeMin = 20; title.fontSizeMax = 28;
            title.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(94, 44), new Vector2(-300, -8));
            string detailText = blocked ?? Objective(Menu.Mode) + "  \u00b7  " + Participants(Menu.Mode, Menu.PartySize);
            var detail = LobbyKit.Text(bar, detailText, 18, blocked == null ? LobbyKit.Muted : LobbyKit.Sun, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            detail.name = "Mode detail";
            detail.gameObject.SetActive(!string.IsNullOrEmpty(detailText));
            detail.enableAutoSizing = true; detail.fontSizeMin = 14; detail.fontSizeMax = 18;
            detail.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(94, 10), new Vector2(-300, -48));
            var go = LobbyKit.Primary(bar, "GO", "GO", Menu.Go, 48, 30);
            ((RectTransform)go.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-268, -34), new Vector2(-12, 34));
            go.interactable = blocked == null;
        }

        static RectTransform Words(RectTransform body, int pad)
        {
            var words = LobbyKit.Column(body, "Words", 6);
            words.anchorMin = Vector2.zero; words.anchorMax = new Vector2(1, 0); words.pivot = new Vector2(.5f, 0);
            words.offsetMin = new Vector2(pad, pad); words.offsetMax = new Vector2(-pad, pad);
            words.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.LowerLeft;
            words.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return words;
        }

        static void TagLine(Transform parent, string text)
        {
            var line = LobbyKit.Row(parent, "Tag", 0);
            line.Size(-1, 30);
            var pill = LobbyKit.Row(line, "Pill", 0);
            pill.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(11, 11, 0, 0);
            pill.Paint(LobbyKit.Navy, 8).raycastTarget = false;
            var label = LobbyKit.Text(pill, LobbyKit.Upper(text), 16, Color.white, TextAlignmentOptions.Center);
            label.characterSpacing = 1;
            label.overflowMode = TextOverflowModes.Overflow;
        }

        static void Wiggle(Button card, RectTransform art)
        {
            card.GetComponent<LobbyPress>().Hot = hot =>
            {
                art.localRotation = Quaternion.Euler(0, 0, hot ? 8f : 0f);
                art.localScale = Vector3.one * (hot ? 1.1f : 1f);
            };
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
                    LobbyKit.Frame(rect, LobbyKit.Navy, 3, 3);
                    if (garden)
                    {
                        float crown = Mathf.Min(rect.sizeDelta.x, rect.sizeDelta.y) * .42f;
                        var tree = LobbyKit.Rect(rect, "Tree").Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(crown, crown));
                        LobbyKit.Face(tree, Tree, Mathf.RoundToInt(crown / 2f), LobbyKit.Navy, 3);
                    }
                }
                if (storeys > 1)
                    foreach (var s in layout.Stairs)
                    {
                        if (layout.StoreyOf(layout.Room(s.Lower)) != storey && layout.StoreyOf(layout.Room(s.Upper)) != storey) continue;
                        var flight = LobbyKit.Rect(box, "Stairs").Pin(new Vector2(.5f, .5f), At((s.MinX + s.MaxX) * .5f, (s.MinZ + s.MaxZ) * .5f),
                            new Vector2(s.MaxX - s.MinX, s.MaxZ - s.MinZ) * scale);
                        flight.Paint(new Color(LobbyKit.Navy.r, LobbyKit.Navy.g, LobbyKit.Navy.b, .55f)).raycastTarget = false;
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
                tag.Paint(LobbyKit.Navy, 7).raycastTarget = false;
                var name = LobbyKit.Display(tag, layout.StoreyLabel(storey).Replace(" FLOOR", ""), 14, Color.white);
                name.characterSpacing = 1;
                name.enableAutoSizing = true; name.fontSizeMin = 12; name.fontSizeMax = 14;
                name.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, 0));
            }
        }
    }

}
