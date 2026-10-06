using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class LobbyMenu : MonoBehaviour
    {
        public const string OutfitKey = "wv.outfit.0";
        public const string Home = "home", Loadout = "loadout", Play = "play", CareerPage = "career",
            Shop = "shop", Trophy = "trophy", Settings = "settings";
        public const string Practice = "practice", Matchmaking = "matchmaking", Workshop = "workshop";
        public const string TutorialMode = "Tutorial", WorkshopMode = "Workshop", RoomMode = "Room";
        public const string VolumeKey = "wv.volume", VsyncKey = "wv.vsync";
        public static readonly string[] Modes = { "Dibs", "Duos", "MovingOut", "MovingDay" };
        public static float StartDelay = 3f;
        public const int PartyMax = 4;
        const float BarHeight = 76f, Gutter = 26f, PartyWidth = 380f, FeedWidth = 420f;
        const float ReferenceHeight = 1080f;
        public const float MessageLife = 8f, MessageFade = .4f;
        public const int MessagesShown = 3;
        public const string TurnHintKey = "lobby.turnHint";
        public static readonly string[] PageOrder = { Home, Loadout, Play, CareerPage, Shop, Trophy, Settings };

        public static LobbyMenu Instance { get; private set; }

        public Outfit Outfit { get; private set; }
        public Career Career { get; private set; }
        public string Map { get; private set; }
        public string Queue { get; private set; } = Practice;
        public string Mode { get; private set; } = "Dibs";
        public LobbyStage Stage { get; private set; }
        public string Current { get; private set; }
        public bool Starting => startAt > 0f;
        public IReadOnlyList<string> Messages => messages;
        public IReadOnlyList<InputBinding> Party => party;
        public int PartySize => 1 + party.Count;

        readonly List<string> messages = new List<string>();
        readonly List<InputBinding> party = new List<InputBinding>();
        readonly KeyboardBinding keyboardRight = new KeyboardBinding(KeyboardBinding.Side.Right);
        readonly Dictionary<string, LobbyPage> pages = new Dictionary<string, LobbyPage>();
        readonly Dictionary<string, LobbyTab> tabs = new Dictionary<string, LobbyTab>();
        readonly Dictionary<string, Selectable> openers = new Dictionary<string, Selectable>();
        readonly List<PlayerController> frozen = new List<PlayerController>();
        readonly List<(CanvasGroup card, float posted)> notices = new List<(CanvasGroup, float)>();
        Canvas canvas;
        RectTransform safe, pageHost, feed, rail, partyList, status, quitDialog;
        TextMeshProUGUI coins, initial, partyHeading, partyCount, statusTitle, statusDetail, statusCount;
        Button power, cancelButton;
        LobbyTab partyTab;
        GameObject bottomScrim;
        GameHud hud;
        Gamepad goPad;
        float startAt;
        bool hidden, typedLastFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= OnScene;
            SceneManager.sceneLoaded += OnScene;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OpenOnPlay() => OnScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);

        static void OnScene(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == Session.HubScene && !FindAnyObjectByType<LobbyMenu>())
                new GameObject("Lobby").AddComponent<LobbyMenu>();
        }

        void Awake() => Instance = this;

        void Start()
        {
            var wardrobe = GameConfig.Current.Wardrobe;
            Outfit = wardrobe.Default.Clone();
            string saved = PlayerPrefs.GetString(OutfitKey, "");
            if (!string.IsNullOrEmpty(saved))
            {
                var candidate = Outfit.Deserialize(saved);
                if (wardrobe.Problems(candidate).Count == 0) Outfit = candidate;
            }
            Career = MatchTally.LoadCareer();
            if (Career.Keep(Outfit.ItemSkins.Values.FirstOrDefault() ?? "Classic", Outfit.ColourOf("Top") ?? ""))
                MatchTally.SaveCareer(Career);
            Map = GameConfig.Current.Houses.ContainsKey(Session.MapId) ? Session.MapId : GameConfig.Current.Houses.Keys.First();
            Choose(Session.LobbyQueue, Session.LobbyMode);
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
            if (PlayerPrefs.HasKey(VsyncKey)) QualitySettings.vSyncCount = PlayerPrefs.GetInt(VsyncKey) > 0 ? 1 : 0;
            foreach (var binding in Session.Bindings.Skip(1))
                if (party.Count < PartyMax - 1 && Couch(binding) && !InParty(binding.Id)) party.Add(binding);
            SuspendHub();
            Stage = new GameObject("Lobby stage").AddComponent<LobbyStage>();
            Stage.Show(Map, Outfit);
            BuildCanvas();
            BuildBar();
            BuildParty();
            BuildFeed();
            BuildStatus();
            foreach (var page in new LobbyPage[] { new HomePage(), new LoadoutPage(), new PlayPage(), new CareerScreen(),
                new ShopPage(), new TrophyPage(), new SettingsPage() })
            {
                page.Create(this, pageHost);
                pages[page.Id] = page;
            }
            Open(Home);
            Post($"Welcome back, {Career.Name}.");
            if (MatchTally.LastResult != null)
            {
                var last = MatchTally.LastResult;
                Post($"{ModeName(last.Mode)} {(last.Won ? "won" : "finished")}: {last.Score} points, +{last.Coins} coins, +{last.Xp} XP" +
                    (MatchTally.LastWasBest ? ". New best!" : "."));
                MatchTally.LastResult = null;
            }
            RefreshBar();
        }

        void OnEnable()
        {
            if (!Stage) return;
            Stage.TakeCamera();
            if (Current != null && pages.TryGetValue(Current, out var page) && page.First && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(page.First.gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (Stage) Destroy(Stage.gameObject);
            ResumeHub();
        }

        void SuspendHub()
        {
            var joins = FindAnyObjectByType<PlayerJoinManager>();
            if (joins)
            {
                joins.AllowJoining = false;
                foreach (var player in joins.Players)
                    if (player && (player.Binding is DesktopBinding || player.Binding is TouchBinding ||
                        player.Binding is KeyboardBinding || player.Binding is GamepadBinding) && !player.Frozen)
                    { player.Frozen = true; frozen.Add(player); }
            }
            hud = FindAnyObjectByType<GameHud>();
            if (hud) foreach (var c in hud.GetComponentsInChildren<Canvas>(true)) c.enabled = false;
        }

        void ResumeHub()
        {
            foreach (var player in frozen) if (player) player.Frozen = false;
            frozen.Clear();
            if (hud) foreach (var c in hud.GetComponentsInChildren<Canvas>(true)) c.enabled = true;
        }

        void BuildCanvas()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, ReferenceHeight);
            scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();
            if (!FindAnyObjectByType<EventSystem>())
            {
                var system = new GameObject("Menu Event System");
                system.AddComponent<EventSystem>();
                system.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            LobbyKit.Scrim(transform, "Top scrim", LobbyKit.Edge.Top, 220, .9f, 64);
            LobbyKit.Scrim(transform, "Right scrim", LobbyKit.Edge.Right, 520, .55f);
            bottomScrim = LobbyKit.Scrim(transform, "Bottom scrim", LobbyKit.Edge.Bottom, 300, .6f).gameObject;
            safe = LobbyKit.Rect(transform, "Safe area").Fill();
            var drag = LobbyKit.Rect(safe, "Turn area").Fill();
            drag.Paint(new Color(0, 0, 0, 0));
            var turner = drag.gameObject.AddComponent<LobbyDrag>();
            turner.Turned = degrees => Stage.Turn(degrees);
            turner.Started = Turning;
            pageHost = LobbyKit.Rect(safe, "Pages").Place(Vector2.zero, Vector2.one,
                new Vector2(Gutter, Gutter), new Vector2(-Gutter, -(BarHeight + 10)));
        }

        void BuildBar()
        {
            var bar = LobbyKit.Rect(safe, "Top bar").Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -BarHeight), Vector2.zero);
            var left = LobbyKit.Row(bar, "System", 12, 0);
            left.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(Gutter, 8), new Vector2(600, -8));
            left.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            left.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            var system = LobbyKit.Segment(left, "System pill");
            tabs[Home] = LobbyKit.IconTab(system, LobbyIcons.Home, "Home", () => Open(Home));
            tabs[Settings] = LobbyKit.IconTab(system, LobbyIcons.Settings, "Settings", () => Toggle(Settings));
            power = LobbyKit.IconTab(system, LobbyIcons.Power, "Quit", AskQuit, LobbyKit.Hot).Button;
            var places = LobbyKit.Segment(left, "Places pill");
            tabs[Trophy] = LobbyKit.IconTab(places, LobbyIcons.Trophy, "Leaderboard", () => Toggle(Trophy));
            tabs[Shop] = LobbyKit.IconTab(places, LobbyIcons.Cart, "Shop", () => Toggle(Shop));

            var centre = LobbyKit.Row(bar, "Pages", 14, 0);
            centre.Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(628, BarHeight));
            var row = centre.GetComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childForceExpandHeight = false;
            foreach (var (id, title, icon) in new[] { (Loadout, "LOADOUT", LobbyIcons.Locker), (Play, "PLAY", LobbyIcons.Play), (CareerPage, "CAREER", LobbyIcons.Badge) })
                tabs[id] = LobbyKit.Tab(centre, title, icon, () => Toggle(id));
            foreach (var kv in tabs) openers[kv.Key] = kv.Value.Button;

            var wallet = LobbyKit.Row(bar, "Wallet", 12, 0);
            wallet.Place(new Vector2(1, 0), new Vector2(1, 1), new Vector2(-600, 8), new Vector2(-Gutter, -8));
            wallet.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;
            wallet.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            var chip = LobbyKit.Row(wallet, "Coin chip", 10);
            var inside = chip.GetComponent<HorizontalLayoutGroup>();
            inside.padding = new RectOffset(12, 18, 10, 10);
            inside.childForceExpandHeight = false;
            LobbyKit.Face(chip, LobbyKit.ChipFill, 12, LobbyKit.ChipEdge, 2);
            chip.Size(-1, 52);
            LobbyKit.Coin(chip, 32);
            coins = LobbyKit.Display(chip, "0", 26, LobbyKit.Sun, TextAlignmentOptions.MidlineLeft, LobbyKit.Ink.Stroke);
            coins.Size(-1, 34);
            partyTab = LobbyKit.Tab(wallet, "Party", LobbyIcons.Party, ToggleParty, 132, 52, 26, 0);
            partyTab.GetComponentInChildren<HorizontalLayoutGroup>().padding = new RectOffset(16, 18, 0, 0);
            partyCount = partyTab.GetComponentsInChildren<TextMeshProUGUI>(true).First();
        }

        void BuildParty()
        {
            rail = LobbyKit.Column(safe, "Party and friends", 10, 18);
            rail.Pin(Vector2.one, new Vector2(-Gutter, -(BarHeight + 6)), new Vector2(PartyWidth, 0));
            rail.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            LobbyKit.PanelFace(rail, 18, 4, 6).raycastTarget = true;
            var column = rail;
            column.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
            partyHeading = LobbyKit.Heading(column, "Party");
            var you = LobbyKit.Row(column, "You", 12, 10).Size(-1, 74);
            you.Paint(LobbyKit.Card, 14);
            var badge = LobbyKit.Rect(you, "Badge");
            LobbyKit.Face(badge, LobbyKit.Hot, 12, LobbyKit.Navy, 3, 3);
            badge.Size(54, 54);
            initial = LobbyKit.Display(badge, LobbyKit.Upper(Career.Name.Substring(0, 1)), 30, LobbyKit.Cream, TextAlignmentOptions.Center, LobbyKit.Ink.Stroke);
            initial.rectTransform.Fill();
            var who = LobbyKit.Column(you, "Who", 0, 0);
            who.Size(170, -1, 1);
            LobbyKit.Display(who, Career.Name, 26, LobbyKit.Cream, TextAlignmentOptions.MidlineLeft).Size(-1, 32).name = "Name";
            LobbyKit.Text(who, "Level " + Career.Level + "  ·  in the lobby", 17, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft).Size(-1, 24).name = "Level";
            partyList = LobbyKit.Column(column, "Couch", 8, 0);
            var friends = LobbyKit.Text(column, "Friends show up here once online or LAN play is built. Bots fill the empty seats.",
                16, LobbyKit.Muted, TextAlignmentOptions.TopLeft);
            friends.textWrappingMode = TextWrappingModes.Normal;
            friends.Size(-1, 44);
            RefreshParty();
            rail.gameObject.SetActive(false);
        }

        public void ToggleParty() => ShowParty(!rail.gameObject.activeSelf);

        void ShowParty(bool on)
        {
            if (!rail) return;
            if (on && Current != Home) Open(Home);
            rail.gameObject.SetActive(on);
            if (partyTab) partyTab.On = on;
            if (on) LobbyPop.On(rail).Play(new Vector2(0, 12));
        }

        void Turning()
        {
            if (PlayerPrefs.HasKey(TurnHintKey)) return;
            PlayerPrefs.SetString(TurnHintKey, "seen");
            PlayerPrefs.Save();
            var hint = pageHost.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r => r.name == "Turn hint");
            if (hint) LobbyPop.On(hint).Vanish();
        }

        void RefreshParty()
        {
            if (!partyList) return;
            partyHeading.text = $"PARTY  {PartySize} / {PartyMax}";
            if (partyCount) partyCount.text = $"{PartySize}/{PartyMax}";
            LobbyKit.Clear(partyList);
            for (int i = 0; i < party.Count; i++)
            {
                var binding = party[i];
                int seat = i + 2;
                var row = LobbyKit.Row(partyList, "Seat " + seat, 12, 9).Size(-1, 68);
                row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
                row.Paint(LobbyKit.Card, 14);
                var badge = LobbyKit.Rect(row, "Seat badge");
                LobbyKit.Face(badge, GameAssets.I ? GameAssets.I.PlayerColor(seat - 1) : LobbyKit.Lime, 12, LobbyKit.Navy, 3, 3);
                badge.Size(46, 46);
                LobbyKit.Display(badge, "P" + seat, 22, LobbyKit.Cream, TextAlignmentOptions.Center, LobbyKit.Ink.Stroke).rectTransform.Fill();
                var who = LobbyKit.Column(row, "Who", 0, 0);
                who.Size(-1, -1, 1);
                LobbyKit.Display(who, "Player " + seat, 22, LobbyKit.Cream, TextAlignmentOptions.MidlineLeft).Size(-1, 26);
                LobbyKit.Text(who, binding is GamepadBinding ? "Controller" : "Right keyboard", 15,
                    LobbyKit.Muted, TextAlignmentOptions.MidlineLeft).Size(-1, 22);
                var leave = LobbyKit.Button(row, "Leave " + seat, Color.white, () => Leave(binding), 17, LobbyKit.Navy, 2, 2);
                leave.Size(34, 34);
                var glyph = LobbyKit.Icon(leave.Body(), LobbyIcons.Close, LobbyKit.Navy);
                glyph.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(9, 9), new Vector2(-9, -9));
                var face = leave.FaceOf();
                leave.GetComponent<LobbyPress>().Hot = hot => face.color = hot ? LobbyKit.Sun : Color.white;
                leave.gameObject.AddComponent<LobbyHint>().Text = "Leave the party";
            }
            if (party.Count >= PartyMax - 1) return;
            var join = LobbyKit.Row(partyList, "Join", 12, 9).Size(-1, 86);
            join.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            join.Paint(LobbyKit.Mist(.06f), 14).raycastTarget = false;
            LobbyKit.Frame(join, LobbyKit.Line, 14, 2);
            var slot = LobbyKit.Rect(join, "Free seat");
            LobbyKit.Face(slot, LobbyKit.Mist(.1f), 12, LobbyKit.Line, 2);
            slot.Size(46, 46);
            LobbyKit.Icon(slot, LobbyIcons.Plus, LobbyKit.Cyan).rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -10));
            var how = LobbyKit.Text(join, "Start on a controller, or . on the keyboard's right half, joins. Select leaves.", 15,
                LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
            how.textWrappingMode = TextWrappingModes.Normal;
            how.Size(-1, 68, 1);
        }

        bool Couch(InputBinding binding) =>
            binding is GamepadBinding g ? g.Pad != null && g.Pad.added : binding is KeyboardBinding k && k.Id == keyboardRight.Id;

        bool InParty(string id) => party.Exists(b => b.Id == id);

        void PollParty()
        {
            if (Starting || quitDialog || Typing) return;
            for (int i = party.Count - 1; i >= 0; i--)
                if (party[i] is GamepadBinding g && (!g.Pad.added || g.Pad.selectButton.wasPressedThisFrame)) Leave(party[i]);
            if (party.Count >= PartyMax - 1) return;
            if (!InParty(keyboardRight.Id) && keyboardRight.JoinPressed()) Join(new KeyboardBinding(KeyboardBinding.Side.Right));
            foreach (var pad in Gamepad.all)
            {
                var binding = new GamepadBinding(pad);
                if (pad.startButton.wasPressedThisFrame && !InParty(binding.Id) && party.Count < PartyMax - 1) Join(binding);
            }
        }

        static bool Typing
        {
            get
            {
                var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
                return selected && selected.TryGetComponent(out TMP_InputField field) && field.isFocused;
            }
        }

        public void Join(InputBinding binding)
        {
            if (binding == null || party.Count >= PartyMax - 1 || InParty(binding.Id)) return;
            party.Add(binding);
            Post($"Player {party.Count + 1} joined the party.");
            PartyChanged();
        }

        public void Leave(InputBinding binding)
        {
            int index = party.FindIndex(b => b.Id == binding.Id);
            if (index < 0) return;
            party.RemoveAt(index);
            Post($"Player {index + 2} left the party.");
            PartyChanged();
        }

        void PartyChanged()
        {
            RefreshParty();
            if (Current != Home && Current != Play) return;
            var page = pages[Current];
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            bool onPage = selected && selected.transform.IsChildOf(page.Root);
            string name = onPage ? selected.name : null;
            page.Refresh();
            if (!onPage || !EventSystem.current) return;
            var again = page.Root.GetComponentsInChildren<Selectable>().FirstOrDefault(s => s.name == name);
            var target = again ? again : page.First;
            if (target) EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        void BuildFeed()
        {
            feed = LobbyKit.Column(safe, "Messages", 8, 0);
            feed.Pin(new Vector2(0, 1), new Vector2(Gutter, -(BarHeight + 10)), new Vector2(FeedWidth, 0));
            feed.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        void BuildStatus()
        {
            status = LobbyKit.Rect(safe, "Starting").Pin(new Vector2(.5f, 1), new Vector2(0, -(BarHeight + 12)), new Vector2(560, 88));
            LobbyKit.PanelFace(status, 18, 4, 6).raycastTarget = true;
            LobbyKit.Rect(status, "Accent").Place(Vector2.zero, new Vector2(0, 1), new Vector2(14, 14), new Vector2(24, -14)).Paint(LobbyKit.Sun, 5).raycastTarget = false;
            statusTitle = LobbyKit.Display(status, "", 30, LobbyKit.Sun, TextAlignmentOptions.MidlineLeft, LobbyKit.Ink.Drop);
            statusTitle.enableAutoSizing = true; statusTitle.fontSizeMin = 20; statusTitle.fontSizeMax = 30;
            statusTitle.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(40, 42), new Vector2(-206, -8));
            statusDetail = LobbyKit.Text(status, "", 17, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
            statusDetail.overflowMode = TextOverflowModes.Ellipsis;
            statusDetail.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(40, 10), new Vector2(-206, -48));
            statusCount = LobbyKit.Display(status, "", 44, LobbyKit.Cream, TextAlignmentOptions.Center, LobbyKit.Ink.Stroke);
            statusCount.rectTransform.Place(new Vector2(1, 0), Vector2.one, new Vector2(-200, 0), new Vector2(-136, 0));
            cancelButton = LobbyKit.Pill(status, "CANCEL", "Cancel", 20, CancelStart);
            ((RectTransform)cancelButton.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-130, -24), new Vector2(-16, 24));
            status.gameObject.SetActive(false);
        }

        public void Open(string id)
        {
            if (!pages.TryGetValue(id, out var page)) throw new ArgumentException("No lobby page " + id, nameof(id));
            bool moved = id != Current;
            foreach (var other in pages.Values) other.Root.gameObject.SetActive(other == page);
            Current = id;
            if (id != Home) ShowParty(false);
            if (bottomScrim) bottomScrim.SetActive(id == Home);
            Stage.Dress(Outfit);
            if (moved) page.Opened();
            page.Refresh();
            if (moved) page.Pop();
            Stage.Frame(page.Focus, (Gutter + page.PanelWidth) / ReferenceHeight);
            feed.gameObject.SetActive(page.ShowFeed);
            foreach (var kv in tabs) kv.Value.On = kv.Key == id;
            var first = page.First;
            if (EventSystem.current && first) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        public void Toggle(string id)
        {
            if (id == Current && id != Home) Close();
            else Open(id);
        }

        public void OpenShop(string offerId)
        {
            Open(Shop);
            if (pages[Shop] is ShopPage shop) shop.Spotlight(offerId);
        }

        public void OpenPlay(string card)
        {
            if (pages[Play] is PlayPage play) play.Aim = card;
            Open(Play);
        }

        public T Page<T>() where T : LobbyPage => pages.Values.OfType<T>().FirstOrDefault();

        public void Close()
        {
            if (Current == null || Current == Home) return;
            string was = Current;
            Open(Home);
            if (EventSystem.current && openers.TryGetValue(was, out var opener) && opener && opener.isActiveAndEnabled)
                EventSystem.current.SetSelectedGameObject(opener.gameObject);
        }

        public void Post(string message)
        {
            messages.Insert(0, message);
            if (messages.Count > 6) messages.RemoveAt(messages.Count - 1);
            if (!feed) return;
            var card = LobbyKit.Rect(feed, "Message");
            card.SetAsFirstSibling();
            card.Paint(LobbyKit.Panel, 10).raycastTarget = false;
            LobbyKit.Frame(card, LobbyKit.ChipEdge, 10, 2);
            LobbyKit.Rect(card, "Accent").Place(Vector2.zero, new Vector2(0, 1), new Vector2(9, 10), new Vector2(14, -10))
                .Paint(LobbyKit.Sun, 2).raycastTarget = false;
            var text = LobbyKit.Text(card, message, 17, LobbyKit.Cream, TextAlignmentOptions.MidlineLeft);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(26, 6), new Vector2(-14, -6));
            card.Size(-1, Mathf.Max(44f, Mathf.Ceil(text.GetPreferredValues(message, FeedWidth - 40f, 0f).y) + 18f));
            var fade = card.gameObject.AddComponent<CanvasGroup>();
            fade.blocksRaycasts = false;
            notices.Insert(0, (fade, Time.unscaledTime));
            while (notices.Count > MessagesShown)
            {
                var old = notices[notices.Count - 1].card;
                notices.RemoveAt(notices.Count - 1);
                if (old) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
            }
        }

        void AgeNotices()
        {
            for (int i = notices.Count - 1; i >= 0; i--)
            {
                var (card, posted) = notices[i];
                float left = MessageLife - (Time.unscaledTime - posted);
                if (card && left > 0f) { card.alpha = Mathf.Clamp01(left / MessageFade); continue; }
                if (card) { card.gameObject.SetActive(false); Destroy(card.gameObject); }
                notices.RemoveAt(i);
            }
        }

        public void RefreshBar()
        {
            if (coins) coins.text = Career.Coins.ToString("N0", CultureInfo.InvariantCulture);
            if (initial) initial.text = LobbyKit.Upper(Career.Name.Substring(0, 1));
            var level = rail ? rail.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t.name == "Level") : null;
            if (level) level.text = "Level " + Career.Level + "  ·  " + (Starting ? "starting a match" : "in the lobby");
            var name = rail ? rail.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t.name == "Name") : null;
            if (name) name.text = Career.Name;
        }

        public void Wear(Outfit next)
        {
            var problems = GameConfig.Current.Wardrobe.Problems(next);
            if (problems.Count > 0) throw new InvalidOperationException(string.Join("\n", problems));
            Outfit = next;
            PlayerPrefs.SetString(OutfitKey, Outfit.Serialize());
            PlayerPrefs.Save();
            Stage.Dress(Outfit);
            var joins = FindAnyObjectByType<PlayerJoinManager>();
            var player = joins ? joins.Players.FirstOrDefault(p => p.Binding is not BotBinding) : null;
            if (player) player.GetComponent<PlayerAppearance>()?.ApplyOutfit(Outfit);
        }

        public void SaveCareer()
        {
            MatchTally.SaveCareer(Career);
            RefreshBar();
        }

        public void Choose(string queue = null, string mode = null, string map = null)
        {
            if (Starting) CancelStart();
            if (queue != null) Queue = queue;
            if (mode != null) Mode = mode;
            if (Queue == Workshop && Mode != TutorialMode && Mode != WorkshopMode) Mode = TutorialMode;
            if (Queue != Workshop && !Modes.Contains(Mode) && !(Queue == Matchmaking && Mode == RoomMode) && !(Queue == Practice && Mode == TutorialMode))
                Mode = Modes[0];
            if (Mode == WorkshopMode && !HomeDesigner.Supports(map ?? Map)) map = HomeDesigner.Supports(Map) ? null : "pinwheel";
            Session.LobbyQueue = Queue;
            Session.LobbyMode = Mode;
            if (map != null && map != Map)
            {
                Session.SelectMap(map);
                Map = map;
                Stage.SetMap(map);
                Post("Map: " + GameConfig.Current.HouseFor(map).Name + ".");
            }
        }

        public string Blocked => Queue == Matchmaking
            ? "Matchmaking needs online or LAN play, which isn't built yet. Practice plays every mode with bots."
            : null;

        public void Go()
        {
            if (Starting) return;
            if (Blocked != null) { Post(Blocked); return; }
            goPad = Gamepad.all.FirstOrDefault(p => p.buttonSouth.wasPressedThisFrame || p.startButton.wasPressedThisFrame);
            if (goPad != null)
            {
                int seat = party.FindIndex(b => b is GamepadBinding g && g.Pad == goPad);
                if (seat >= 0) { party.RemoveAt(seat); RefreshParty(); }
            }
            startAt = Time.unscaledTime + StartDelay;
            status.gameObject.SetActive(true);
            statusTitle.text = Queue == Workshop ? "OPENING" : "STARTING PRACTICE";
            statusDetail.text = Describe();
            Open(Home);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(cancelButton.gameObject);
            Post("Starting " + Describe() + ".");
            RefreshBar();
        }

        public void CancelStart()
        {
            if (!Starting) return;
            startAt = 0f;
            goPad = null;
            status.gameObject.SetActive(false);
            Post("Cancelled.");
            if (Current != null) Open(Current);
            RefreshBar();
        }

        public string Describe()
        {
            if (Mode == TutorialMode) return "Play & learn · the tutorial room";
            if (Mode == WorkshopMode) return ModeName(WorkshopMode) + " · " + GameConfig.Current.HouseFor(Map).Name;
            if (Queue == Matchmaking) return ModeName(Mode) + " · " + GameConfig.Current.HouseFor(Map).Name + " · online";
            return ModeName(Mode) + " · " + GameConfig.Current.HouseFor(Map).Name + " · " + Seats(Mode, PartySize);
        }

        public static string ModeName(string mode) => mode switch
        {
            "Dibs" => "Dibs!",
            "Duos" => "Double trouble",
            "MovingOut" => "The great escape",
            "MovingDay" => "Moving day",
            "Tutorial" => "Play & learn",
            "Workshop" => "Creative workshop",
            "Room" => "Private room",
            _ => mode,
        };

        public static string Seats(string mode, int humans = 1)
        {
            humans = Mathf.Clamp(humans, 1, PartyMax);
            int bots = PartyMax - humans;
            return mode switch
            {
                "Dibs" => humans == 1 ? "you + 3 bots" : bots == 0 ? "4 players" : $"{humans} players + {bots} bot{(bots > 1 ? "s" : "")}",
                "Duos" => humans switch
                {
                    1 => "you and a bot vs 2 bots",
                    2 => "2 players, each with a bot",
                    3 => "3 players and a bot",
                    _ => "2 v 2",
                },
                _ => humans == 1 ? "solo" : $"{humans} players",
            };
        }

        void Update()
        {
            var area = Screen.safeArea;
            if (Screen.width > 0 && Screen.height > 0)
            {
                safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
                safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            }
            bool workshopOpen = CreativeWorkshop.Instance;
            if (workshopOpen != hidden)
            {
                hidden = workshopOpen;
                canvas.enabled = !hidden;
            }
            if (hidden) return;
            AgeNotices();
            PollParty();
            if (Starting)
            {
                float left = startAt - Time.unscaledTime;
                statusCount.text = Mathf.CeilToInt(Mathf.Max(0f, left)).ToString();
                if (left <= 0f) Launch();
            }
            int step = AnyPad(p => p.rightShoulder) ? 1 : AnyPad(p => p.leftShoulder) ? -1 : 0;
            if (step != 0 && !quitDialog && !Starting && !Typing)
            {
                int at = Array.IndexOf(PageOrder, Current);
                Open(PageOrder[(at + step + PageOrder.Length) % PageOrder.Length]);
            }
            var keyboard = Keyboard.current;
            if (!KeyBindings.Busy && ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || AnyPad(p => p.buttonEast)))
            {
                if (quitDialog) CloseQuit();
                else if (rail && rail.gameObject.activeSelf)
                {
                    ShowParty(false);
                    if (EventSystem.current && partyTab) EventSystem.current.SetSelectedGameObject(partyTab.gameObject);
                }
                else if (Starting) CancelStart();
                else if (Typing || typedLastFrame) { }
                else Close();
            }
        }

        void LateUpdate() => typedLastFrame = Typing;

        static bool AnyPad(Func<Gamepad, ButtonControl> button)
        {
            foreach (var pad in Gamepad.all)
                if (button(pad).wasPressedThisFrame) return true;
            return false;
        }

        void Launch()
        {
            startAt = 0f;
            status.gameObject.SetActive(false);
            Open(Current ?? Home);
            var pad = goPad;
            goPad = null;
            if (Mode == WorkshopMode)
            {
                Stage.Release();
                if (!Session.OpenWorkshop(Map, out string error)) { Stage.TakeCamera(); Post(error); }
                RefreshBar();
                return;
            }
            Seat(pad);
            if (Mode == TutorialMode) Session.LoadMode(TutorialMode);
            else Session.LoadMode(Mode, Map);
        }

        void Seat(Gamepad pad)
        {
            InputBinding you;
            if (pad != null && pad.added) you = new GamepadBinding(pad);
            else
            {
                bool touch = Application.isMobilePlatform || Touchscreen.current != null;
                if (touch) TouchBinding.Shared.Enabled = true;
                you = touch ? TouchBinding.Shared : DesktopBinding.Shared;
            }
            Session.Bindings.Clear();
            Session.Remember(you);
            foreach (var binding in party) Session.Remember(binding);
        }

        void AskQuit()
        {
            if (quitDialog) return;
            var shade = quitDialog = LobbyKit.Rect(safe, "Quit dialog").Fill();
            shade.Paint(LobbyKit.Shade);
            shade.gameObject.AddComponent<LobbyShade>().Clicked = CloseQuit;
            var box = LobbyKit.Rect(shade, "Box").Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(640, 300));
            LobbyKit.PanelFace(box, 28, 5, 8).raycastTarget = true;
            var title = LobbyKit.Display(box, "LEAVE THE HOUSE PARTY?", 40, LobbyKit.Sun, TextAlignmentOptions.Center, LobbyKit.Ink.Drop);
            title.characterSpacing = 2;
            title.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(64, -98), new Vector2(-64, -34));
            var saved = LobbyKit.Text(box, "Your looks, coins and career are saved.", 20, LobbyKit.Muted, TextAlignmentOptions.Center);
            saved.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(30, -138), new Vector2(-30, -104));
            var close = LobbyKit.IconButton(box, LobbyIcons.Close, "Stay", CloseQuit);
            close.name = "Close quit";
            ((RectTransform)close.transform).Pin(Vector2.one, new Vector2(-18, -18), new Vector2(44, 44));
            var quit = LobbyKit.Danger(box, "QUIT GAME", 26, Application.Quit);
            ((RectTransform)quit.transform).Pin(new Vector2(.5f, 0), new Vector2(-140, 36), new Vector2(256, 72));
            var stay = LobbyKit.Button(box, "STAY", LobbyKit.SunHi, CloseQuit, 14, LobbyKit.Navy, 4, 6, LobbyKit.Sun2);
            var word = LobbyKit.Display(stay.Body(), "STAY", 30, LobbyKit.Navy);
            word.characterSpacing = 3;
            word.rectTransform.Fill();
            ((RectTransform)stay.transform).Pin(new Vector2(.5f, 0), new Vector2(140, 36), new Vector2(256, 72));
            quit.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = stay };
            stay.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = quit, selectOnUp = close };
            close.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = stay };
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(stay.gameObject);
        }

        void CloseQuit()
        {
            if (quitDialog) { quitDialog.gameObject.SetActive(false); Destroy(quitDialog.gameObject); }
            quitDialog = null;
            if (EventSystem.current && power) EventSystem.current.SetSelectedGameObject(power.gameObject);
        }
    }
}
