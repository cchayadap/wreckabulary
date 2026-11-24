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
    public abstract class LobbyPage
    {
        public const float SideWidth = 860f;
        public const string Tag = "Name tag", TagLine = "Tag line", CardName = "Card name";

        public abstract string Id { get; }
        public virtual LobbyStage.Focus Focus => LobbyStage.Focus.Centre;
        public virtual bool ShowFeed => false;
        public virtual bool Modal => false;
        public virtual float PanelWidth => SideWidth;
        public RectTransform Root { get; private set; }
        public Selectable First { get; protected set; }
        protected LobbyMenu Menu { get; private set; }

        public void Create(LobbyMenu menu, RectTransform host)
        {
            Menu = menu;
            Root = LobbyKit.Rect(host, Id + " page").Fill();
            Root.gameObject.SetActive(false);
        }

        public void Pop()
        {
            if (Id == LobbyMenu.Home) return;
            LobbyPop.On(Root).Play(Focus == LobbyStage.Focus.Left ? new Vector2(24, 0) : new Vector2(0, -16));
        }

        public virtual void Opened() { }
        public virtual void Closed() { }

        public void Refresh()
        {
            Closed();
            LobbyKit.Clear(Root);
            First = null;
            if (Modal)
            {
                var shade = LobbyKit.Rect(Root, "Shade").Fill();
                shade.Paint(LobbyKit.Shade);
                shade.gameObject.AddComponent<LobbyShade>().Clicked = Menu.Close;
            }
            Build();
            LobbyParcel.DressPage(Root);
        }

        protected abstract void Build();

        protected void Reselect(string name)
        {
            if (!EventSystem.current) return;
            var target = Root.GetComponentsInChildren<Selectable>().FirstOrDefault(s => s.name == name);
            if (target) EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        protected RectTransform Panel(Vector2 min, Vector2 max, string title, string subtitle = null)
        {
            var panel = LobbyKit.Rect(Root, "Panel").Place(min, max);
            LobbyKit.PanelFace(panel, 28, 5, 8).raycastTarget = true;
            var head = LobbyKit.Display(panel, LobbyKit.Upper(title), 43, LobbyKit.Sun, TextAlignmentOptions.BottomLeft, LobbyKit.Ink.Drop);
            head.name = "Title";
            head.characterSpacing = 2;
            head.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(34, -84), new Vector2(-88, -20));
            var close = LobbyKit.IconButton(panel, LobbyIcons.Close, "Close page", Menu.Close);
            ((RectTransform)close.transform).Pin(Vector2.one, new Vector2(-20, -20), new Vector2(48, 48));
            float top = 100;
            if (!string.IsNullOrEmpty(subtitle))
            {
                var sub = LobbyKit.Text(panel, subtitle, 17, LobbyKit.Muted, TextAlignmentOptions.TopLeft);
                sub.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(34, -120), new Vector2(-88, -90));
                top = 130;
            }
            return LobbyKit.Rect(panel, "Body").Place(Vector2.zero, Vector2.one, new Vector2(32, 28), new Vector2(-32, -top));
        }

        protected RectTransform Side(string title, string subtitle = null)
        {
            var body = Panel(new Vector2(1, 0), Vector2.one, title, subtitle);
            ((RectTransform)body.parent).offsetMin = new Vector2(-PanelWidth, 0);
            return body;
        }

        protected static TextMeshProUGUI Wrapped(Transform parent, string text, float size, Color colour, float height = -1)
        {
            var label = LobbyKit.Text(parent, text, size, colour, TextAlignmentOptions.TopLeft);
            label.textWrappingMode = TextWrappingModes.Normal;
            if (height > 0) label.Size(-1, height);
            return label;
        }

        protected static RectTransform Grow(RectTransform rect)
        {
            if (!rect.TryGetComponent(out LayoutElement element)) element = rect.gameObject.AddComponent<LayoutElement>();
            element.flexibleHeight = 1;
            return rect;
        }

        protected static Button Poster(Transform parent, string name, Color colour, bool on, Action click, Vector2 size)
        {
            var button = LobbyKit.Button(parent, name, LobbyKit.Card, click, 12, LobbyKit.Line, 1);
            var press = button.GetComponent<LobbyPress>();
            press.Lift = 0f; press.Tilt = 0f;
            var body = button.Body();
            if (on) Chosen(body, 12);
            return button;
        }

        protected static void Chosen(RectTransform body, int radius)
        {
            LobbyKit.Ring(body, LobbyKit.Selection, radius, 2);
            var check = LobbyKit.Rect(body, "Check").Pin(Vector2.one, new Vector2(12, 12), new Vector2(38, 38));
            LobbyKit.Face(check, LobbyKit.Selection, 19, LobbyKit.Line, 1);
            LobbyKit.Icon(check, LobbyIcons.Check, LobbyKit.OnSelection).rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -8));
        }

        protected static string SkinOf(Outfit outfit)
        {
            var styles = GameConfig.Current.Items.Enabled.Where(item => item.Skins.Count > 1).Select(item => outfit.SkinFor(item.Id) ?? Skin.Standard)
                .Distinct().Take(2).ToArray();
            return styles.Length > 1 ? null : styles.FirstOrDefault() ?? Skin.Standard;
        }

        public static Outfit WithFinish(Outfit outfit, string skin)
        {
            var next = outfit.Clone();
            foreach (var item in GameConfig.Current.Items.Enabled)
                if (item.HasSkin(skin)) next.ItemSkins[item.Id] = skin;
            return next;
        }

        public static IEnumerable<Finish> CustomizeFinishes => Rules.Finishes.All.Where(finish => finish.InCustomize);

        public static string FinishName(string skin) => Rules.Finishes.Find(skin)?.Name ?? skin;

        public static (Color from, Color to) FinishPaint(string skin) =>
            Rules.Finishes.Find(skin) is Finish finish ? (LobbyKit.Hex(finish.Swatch), LobbyKit.Hex(finish.Accent)) : (LobbyKit.WoodHi, LobbyKit.WoodLo);

        public static string ShowcaseItem(string skin)
        {
            var items = GameConfig.Current.Items;
            bool Shows(ItemDefinition item) => skin == Skin.Standard || item.HasSkin(skin);
            if (items.TryGet("BAT", out var bat) && bat.Enabled && Shows(bat)) return bat.Id;
            return items.Enabled.FirstOrDefault(Shows)?.Id ?? "BAT";
        }

        protected static RawImage FinishedItem(Transform parent, string id, string skin, float size, float degrees = 0)
        {
            if (!Wreckabulary.UI.ItemArt.TryGet(id, skin, out var texture, out var uv)) return null;
            var rect = LobbyKit.Rect(parent, "Art " + id).Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(size, size));
            rect.localRotation = Quaternion.Euler(0, 0, degrees);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture; image.uvRect = uv; image.raycastTarget = false;
            return image;
        }

        protected static RawImage Emblem(Transform parent, string path, Vector2 anchor, Vector2 position, float size)
        {
            var picture = string.IsNullOrEmpty(path) ? null : Resources.Load<Texture2D>(path);
            if (!picture) return null;
            var image = LobbyKit.Rect(parent, "Emblem").Pin(anchor, position, new Vector2(size, size)).gameObject.AddComponent<RawImage>();
            image.texture = picture; image.raycastTarget = false;
            return image;
        }

        protected static RectTransform NameTag(RectTransform art, string line, float height = 28)
        {
            var tag = LobbyKit.Rect(art, Tag).Place(Vector2.zero, new Vector2(1, 0), new Vector2(6, 6), new Vector2(-6, 6 + height));
            var fill = LobbyKit.Panel; fill.a = .88f;
            tag.Paint(fill, 9).raycastTarget = false;
            var text = LobbyKit.Text(tag, line, 15, LobbyKit.Cream, TextAlignmentOptions.Center);
            text.name = TagLine;
            text.textWrappingMode = height > 30 ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.enableAutoSizing = true; text.fontSizeMin = 11; text.fontSizeMax = 15;
            text.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 2), new Vector2(-8, -2));
            return tag;
        }

        protected static RectTransform FinishDisc(Transform parent, string skin, float size)
        {
            var (from, to) = FinishPaint(skin);
            var disc = LobbyKit.Rect(parent, "Finish").Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(size, size));
            LobbyKit.Face(disc, from, Mathf.RoundToInt(size / 2f), LobbyKit.Navy, 3, 0, to);
            FinishedItem(disc, ShowcaseItem(skin), skin, size * .95f, 20f);
            Emblem(disc, Rules.Finishes.Find(skin)?.Emblem, new Vector2(1, 0), new Vector2(size * .12f, -size * .12f), size * .42f);
            return disc;
        }

        protected static RectTransform Blob(Transform parent, Color colour, float size)
        {
            var blob = LobbyKit.Rect(parent, "Swatch").Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(size, size));
            LobbyKit.Face(blob, Color.Lerp(colour, Color.white, .2f), Mathf.RoundToInt(size / 2f), LobbyKit.Navy, 3, 0,
                Color.Lerp(colour, Color.black, .18f));
            var shine = LobbyKit.Rect(blob, "Shine").Pin(new Vector2(.32f, .72f), Vector2.zero, new Vector2(size * .22f, size * .16f));
            shine.Paint(new Color(1f, 1f, 1f, .7f), Mathf.Max(1, Mathf.RoundToInt(size * .08f))).raycastTarget = false;
            return blob;
        }

        protected static void Burst(RectTransform rect, int width, int height, int radius)
        {
            rect.Paint(LobbyKit.Card, radius).raycastTarget = false;
        }

        protected static string Capital(string text) =>
            string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

        protected static string MapName(string id) =>
            id == Session.LobbyRandomMap ? "Random house"
            : id != null && GameConfig.Current.Houses.TryGetValue(id, out var house) ? house.Name : id ?? "";

        protected static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

        protected static string When(long endedAt) => endedAt <= 0 || endedAt > Career.LatestTime ? "-" :
            DateTimeOffset.FromUnixTimeSeconds(endedAt).ToLocalTime().ToString("d MMM HH:mm", CultureInfo.InvariantCulture);
    }

    public sealed class HomePage : LobbyPage
    {
        public override string Id => LobbyMenu.Home;
        public override bool ShowFeed => true;
        public const float DockWidth = 300f;

        protected override void Build()
        {
            var layout = Resources.Load<LobbyHomeLayout>(LobbyHomeLayout.ResourcePath);
            LobbyHomePageView view;
            if (layout)
            {
                layout.Validate();
                view = LobbyRuntimeTypography.Instantiate(layout.Page, Root, false);
            }
            else view = LobbyHomeDefaultLayout.Create(Root);
            string last = Menu.LastMatch;
            First = view.Bind(!PlayerPrefs.HasKey(LobbyMenu.TurnHintKey), Menu.Starting, Menu.PlayLast, last,
                () => Menu.Open(LobbyMenu.Play), LastMode(last));
            view.ShowCareer(PartyPlates.CareerLine(Menu.Career.Matches, Menu.Career.SecondsPlayed));
            LobbyPartyLineup.Show(Menu, Root, view.Dock);
        }

        string LastMode(string last) => last != null && last == PlayPage.CourseTitle ? WordboundSoloEntry.SelectedMode : Menu.Mode;
    }

    public sealed class CareerScreen : LobbyPage
    {
        public override string Id => LobbyMenu.CareerPage;
        public override LobbyStage.Focus Focus => LobbyStage.Focus.Left;

        protected override void Build()
        {
            var career = Menu.Career;
            var body = Side("Career", "Matches on this screen count. Online play will add ranked results.");
            var column = LobbyKit.Column(body, "Column", 16).Fill();

            var level = LobbyKit.Row(column, "Level", 22);
            level.Size(-1, 112);
            level.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            LobbyKit.LetterTile(level, career.Level.ToString(CultureInfo.InvariantCulture), 104).name = "Level tile";
            var info = LobbyKit.Rect(level, "Info");
            info.Size(-1, 112, 1);
            var name = LobbyKit.Display(info, career.Name, 36, LobbyKit.Cream, TextAlignmentOptions.BottomLeft);
            name.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -46), Vector2.zero);
            int next = Career.XpToNext(career.Level);
            var bar = LobbyKit.Rect(info, "XP bar").Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -84), new Vector2(0, -54));
            bar.Paint(LobbyKit.Track, 10).raycastTarget = false;
            var fill = LobbyKit.Rect(bar, "Fill").Place(Vector2.zero, new Vector2(Mathf.Clamp01(career.XpIntoLevel / (float)next), 1)).Paint(Color.white, 10);
            fill.raycastTarget = false;
            LobbyKit.Gradient(fill, LobbyKit.Selection, LobbyKit.Selection);
            LobbyKit.Frame(bar, LobbyKit.Line, 10, 1);
            var xpBacking = LobbyKit.Rect(bar, "XP label").Place(new Vector2(1, 0), Vector2.one, new Vector2(-180, 2), new Vector2(-2, -2));
            xpBacking.Paint(LobbyKit.Card, 8).raycastTarget = false;
            var xp = LobbyKit.Display(xpBacking, Number(career.XpIntoLevel) + " / " + Number(next) + " XP", 18, LobbyKit.Cream,
                TextAlignmentOptions.MidlineRight, LobbyKit.Ink.Stroke);
            xp.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-14, 0));
            var line = LobbyKit.Text(info, $"Level {career.Level}  ·  {Number(Mathf.Max(0, next - career.XpIntoLevel))} XP to level {career.Level + 1}",
                18, LobbyKit.Muted, TextAlignmentOptions.TopLeft);
            line.name = "XP line";
            line.overflowMode = TextOverflowModes.Overflow;
            line.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -114), new Vector2(0, -90));

            var stats = LobbyKit.Row(column, "Stats", 14);
            stats.Size(-1, 110);
            Stat(stats, "Matches", Number(career.Matches), LobbyIcons.Play);
            Stat(stats, "Wins", Number(career.Wins), LobbyIcons.Trophy);
            Stat(stats, "Win rate", career.Matches == 0 ? "-" : Mathf.RoundToInt(100f * career.Wins / career.Matches) + "%", LobbyIcons.Badge);
            Stat(stats, "Coins", Number(career.Coins), null);

            LobbyKit.SectionLabel(column, "Recent matches");
            if (career.History.Count == 0)
            {
                Empty(column);
                return;
            }
            var holder = Grow(LobbyKit.Rect(column, "History"));
            var list = LobbyKit.Scroll(holder, "Scroll", 10);
            foreach (var match in career.History) Match(list, match);
        }

        static void Stat(Transform parent, string title, string value, string icon)
        {
            var tile = LobbyKit.Rect(parent, title);
            LobbyKit.Face(tile, LobbyKit.Card, 16, LobbyKit.Line, 1, 0);
            tile.Size(-1, -1, 1);
            var mark = LobbyKit.Rect(tile, "Mark").Pin(new Vector2(0, .5f), new Vector2(16, 0), new Vector2(52, 52));
            if (icon == null) LobbyKit.Coin(mark, 52).Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(52, 52));
            else
            {
                LobbyKit.Face(mark, LobbyKit.SunHi, 26, LobbyKit.Navy, 2, 0, LobbyKit.Sun2);
                LobbyKit.Icon(mark, icon, LobbyKit.ArtInk).rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(13, 13), new Vector2(-13, -13));
            }
            var number = LobbyKit.Display(tile, value, 34, LobbyKit.Navy, TextAlignmentOptions.BottomLeft);
            number.enableAutoSizing = true; number.fontSizeMin = 22; number.fontSizeMax = 34;
            number.rectTransform.Place(new Vector2(0, .5f), Vector2.one, new Vector2(80, -6), new Vector2(-10, -8));
            var caption = LobbyKit.Caps(tile, title, 13, TextAlignmentOptions.TopLeft);
            caption.color = LobbyKit.CardSub;
            caption.rectTransform.Place(Vector2.zero, new Vector2(1, .5f), new Vector2(80, 8), new Vector2(-8, -4));
        }

        static void Match(Transform list, MatchRecord match)
        {
            var card = LobbyKit.Row(list, "Match", 14);
            card.Size(-1, 72);
            var layout = card.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 18, 0, 0);
            layout.childForceExpandHeight = false;
            LobbyKit.Face(card, LobbyKit.Card, 14, LobbyKit.Line, 2);
            var colour = LobbyKit.ModeColour(match.Mode);
            var badge = LobbyKit.Rect(card, "Mode");
            badge.Size(52, 52);
            LobbyKit.Face(badge, Color.Lerp(colour, Color.white, .25f), 12, LobbyKit.Navy, 2, 0, Color.Lerp(colour, Color.black, .2f));
            LobbyKit.ItemImage(badge, LobbyKit.ModeArt(match.Mode), 44, 6f);
            var words = LobbyKit.Rect(card, "Words");
            words.Size(-1, 56, 1);
            var title = LobbyKit.Display(words, LobbyMenu.ModeName(match.Mode), 24, LobbyKit.Cream, TextAlignmentOptions.BottomLeft);
            title.rectTransform.Place(new Vector2(0, .45f), Vector2.one, Vector2.zero, Vector2.zero);
            var detail = LobbyKit.Text(words, MapName(match.Map) + "  ·  " + When(match.EndedAt), 15, LobbyKit.Muted, TextAlignmentOptions.TopLeft);
            detail.rectTransform.Place(Vector2.zero, new Vector2(1, .45f), Vector2.zero, new Vector2(0, -2));
            var result = LobbyKit.Rect(card, "Result");
            result.Size(84, 32);
            if (match.Won) LobbyKit.Face(result, LobbyKit.LimeHi, 16, LobbyKit.Navy, 2, 0, LobbyKit.LimeLo);
            else LobbyKit.Face(result, LobbyKit.Card, 16, LobbyKit.Line, 2);
            LobbyKit.Display(result, match.Won ? "WON" : "LOST", 18, match.Won ? LobbyKit.ArtInk : LobbyKit.Muted).rectTransform.Fill();
            LobbyKit.Display(card, Number(match.Score), 28, LobbyKit.Sun, TextAlignmentOptions.MidlineRight).Size(110, 40);
            LobbyKit.Text(card, $"+{Number(match.Coins)} coins  ·  +{Number(match.Xp)} XP", 15, LobbyKit.Muted,
                TextAlignmentOptions.MidlineRight).Size(200, 30);
        }

        void Empty(Transform column)
        {
            var card = LobbyKit.Rect(column, "No matches");
            card.Size(-1, 220);
            LobbyKit.Face(card, LobbyKit.Card, 18, LobbyKit.Line, 2);
            var art = LobbyKit.ItemImage(card, "BOX", 112, 8f);
            if (art) art.rectTransform.Pin(new Vector2(0, .5f), new Vector2(30, 0), new Vector2(112, 112));
            var title = LobbyKit.Display(card, "No matches yet", 34, LobbyKit.Sun, TextAlignmentOptions.BottomLeft, LobbyKit.Ink.Stroke);
            title.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(172, -78), new Vector2(-24, -26));
            var line = Wrapped(card, "Finish a match to see your results here.", 19, LobbyKit.Muted);
            line.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(172, -136), new Vector2(-24, -84));
            var go = LobbyKit.Button(card, "Play practice", LobbyKit.SunHi, () =>
            {
                Menu.Choose(queue: LobbyMenu.Practice);
                Menu.Open(LobbyMenu.Play);
            }, 12, LobbyKit.Navy, 3, 4, LobbyKit.Sun2);
            var label = LobbyKit.Display(go.Body(), "PLAY PRACTICE", 22, LobbyKit.ArtInk);
            label.characterSpacing = 2;
            label.rectTransform.Fill();
            ((RectTransform)go.transform).Pin(Vector2.zero, new Vector2(172, 26), new Vector2(250, 54));
            First = go;
        }
    }

    public sealed class TrophyPage : LobbyPage
    {
        static readonly (Color from, Color to, Color hi, Color lo)[] Podium =
        {
            (LobbyKit.Hex(0xffe14d), LobbyKit.Hex(0xfff7c2), LobbyKit.Hex(0xfff4a8), LobbyKit.Hex(0xe09a00)),
            (LobbyKit.Hex(0xd9e2f2), LobbyKit.Hex(0xf7f9fd), LobbyKit.Hex(0xffffff), LobbyKit.Hex(0x8f9cb0)),
            (LobbyKit.Hex(0xffbf8a), LobbyKit.Hex(0xffeedf), LobbyKit.Hex(0xffd5ad), LobbyKit.Hex(0xb4601f)),
        };
        const int Places = 5, ChipsPerRow = 5;
        string board;

        public override string Id => LobbyMenu.Trophy;
        public override LobbyStage.Focus Focus => LobbyStage.Focus.Left;
        public override float PanelWidth => 700f;

        public override void Opened() => board = null;

        protected override void Build()
        {
            var career = Menu.Career;
            if (!LobbyMenu.Modes.Contains(board)) board = LobbyMenu.Modes.Contains(Menu.Mode) ? Menu.Mode : LobbyMenu.Modes[0];
            var body = Side("Leaderboards", "Best single-match score in each mode.");
            var column = LobbyKit.Column(body, "Column", 10, 6).Fill();
            for (int first = 0; first < LobbyMenu.Modes.Length; first += ChipsPerRow)
            {
                var chips = LobbyKit.Row(column, "Boards", 10);
                chips.Size(-1, 92);
                chips.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
                var modes = LobbyMenu.Modes.Skip(first).Take(ChipsPerRow).ToArray();
                foreach (string mode in modes) Chip(chips, mode);
                for (int empty = modes.Length; empty < ChipsPerRow; empty++) LobbyKit.Rect(chips, "Empty").Size(-1, -1, 1);
            }
            LobbyKit.Rect(column, "Gap").Size(-1, 4);

            var rows = Rows(career, board);
            Note(column, "Your best scores.");
            if (rows.Count == 0) Note(column, "No scores yet. Be the first!");
            for (int i = 0; i < rows.Count; i++) Entry(column, i + 1, rows[i].score, career.Name, rows[i].detail, i == 0);
            Grow(LobbyKit.Rect(column, "Space"));

            var bar = LobbyKit.Row(column, "Your best", 12);
            bar.Size(-1, 68);
            var layout = bar.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(20, 22, 0, 0);
            layout.childForceExpandHeight = false;
            LobbyKit.Face(bar, LobbyKit.Raised, 14, LobbyKit.Line, 2);
            LobbyKit.Icon(bar, LobbyIcons.Trophy, LobbyKit.SunHi).Size(30, 30);
            LobbyKit.Text(bar, "Your best", 20, Color.white, TextAlignmentOptions.MidlineLeft).Size(-1, 30, 1);
            LobbyKit.Display(bar, career.Bests.TryGetValue(board, out int best) ? Number(best) : "-", 32, LobbyKit.SunHi,
                TextAlignmentOptions.MidlineRight).Size(200, 40);
        }

        static List<(int score, string detail)> Rows(Career career, string mode)
        {
            var rows = career.History.Where(m => m.Mode == mode).OrderByDescending(m => m.Score)
                .Select(m => (score: m.Score, detail: MapName(m.Map) + "  ·  " + When(m.EndedAt))).ToList();
            if (career.Bests.TryGetValue(mode, out int best) && (rows.Count == 0 || best > rows[0].score)) rows.Insert(0, (best, "All-time best"));
            return rows.Take(Places).ToList();
        }

        void Chip(Transform row, string mode)
        {
            bool on = mode == board;
            string name = "Board " + mode;
            var button = on ? LobbyKit.Button(row, name, LobbyKit.Raised, () => Pick(mode), 12, LobbyKit.Selection, 2, 0)
                : LobbyKit.Button(row, name, LobbyKit.Card, () => Pick(mode), 12, LobbyKit.Line, 2);
            button.Size(-1, -1, 1);
            var press = button.GetComponent<LobbyPress>();
            press.Lift = 2f;
            if (on) press.Lean = 2f;
            var body = button.Body();
            var art = LobbyKit.ItemImage(body, LobbyKit.ModeArt(mode), 50);
            if (art) art.rectTransform.Pin(new Vector2(.5f, 1), new Vector2(0, -6), new Vector2(50, 50));
            var label = LobbyKit.Display(body, LobbyMenu.ModeName(mode), 17, LobbyKit.Cream, TextAlignmentOptions.Center,
                on ? LobbyKit.Ink.Stroke : LobbyKit.Ink.Plain);
            label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = 17;
            label.rectTransform.Place(Vector2.zero, new Vector2(1, 0), new Vector2(6, 8), new Vector2(-6, 34));
            if (on) First = button;
        }

        void Pick(string mode)
        {
            board = mode;
            Refresh();
            Reselect("Board " + mode);
        }

        static void Entry(Transform column, int rank, int score, string name, string detail, bool you)
        {
            var row = LobbyKit.Row(column, "Rank " + rank, 14);
            row.Size(-1, 64);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 18, 0, 0);
            layout.childForceExpandHeight = false;
            var tile = LobbyKit.Rect(row, "Rank");
            tile.Size(48, 48);
            bool podium = rank <= Podium.Length;
            if (podium)
            {
                var (from, to, hi, lo) = Podium[rank - 1];
                LobbyKit.Face(row, from, 14, LobbyKit.Navy, 3, 3, to).GetComponent<LobbyGradient>().Set(from, to, true);
                LobbyKit.Face(tile, hi, 12, LobbyKit.Navy, 2, 0, lo);
            }
            else
            {
                LobbyKit.Face(row, LobbyKit.Card, 14, LobbyKit.Line, 1);
                LobbyKit.Face(tile, LobbyKit.WoodHi, 12, LobbyKit.Cocoa, 2, 3, LobbyKit.WoodLo, dropColour: LobbyKit.Cocoa);
            }
            if (you) LobbyKit.Frame(row, LobbyKit.Hot, 14, 3, "You", 4);
            LobbyKit.Display(tile, rank.ToString(CultureInfo.InvariantCulture), 26, podium ? LobbyKit.ArtInk : LobbyKit.WoodInk).rectTransform.Fill();
            var who = LobbyKit.Rect(row, "Who");
            who.Size(-1, 56, 1);
            var title = LobbyKit.Display(who, name, 24, podium ? LobbyKit.ArtInk : LobbyKit.Navy, TextAlignmentOptions.BottomLeft);
            title.rectTransform.Place(new Vector2(0, .45f), Vector2.one, Vector2.zero, Vector2.zero);
            var sub = LobbyKit.Text(who, detail, 15, podium ? LobbyKit.ArtInk : LobbyKit.CardSub, TextAlignmentOptions.TopLeft);
            sub.rectTransform.Place(Vector2.zero, new Vector2(1, .45f), Vector2.zero, new Vector2(0, -2));
            LobbyKit.Display(row, Number(score), 30, podium ? LobbyKit.ArtInk : LobbyKit.Navy, TextAlignmentOptions.MidlineRight).Size(160, 40);
        }

        static void Note(Transform column, string text)
        {
            var note = LobbyKit.Rect(column, "Note");
            note.Size(-1, 48);
            note.Paint(LobbyKit.Card, 10).raycastTarget = false;
            LobbyKit.Text(note, text, 17, LobbyKit.Cream, TextAlignmentOptions.MidlineLeft).rectTransform
                .Place(Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-12, 0));
        }
    }

    public sealed class LatinNameValidator : TMP_InputValidator
    {
        public static LatinNameValidator Create() => CreateInstance<LatinNameValidator>();

        public override char Validate(ref string text, ref int position, char character)
        {
            if (Career.CleanName(character.ToString()).Length == 0) return '\0';
            text = text.Insert(position, character.ToString());
            position++;
            return character;
        }
    }
}

