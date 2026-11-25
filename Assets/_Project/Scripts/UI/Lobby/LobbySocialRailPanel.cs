using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed partial class LobbySocialRail
    {
        public const string PanelName = "Social panel", ShadeName = "Social shade", PanelColumn = "Panel column", HeadName = "Panel head";
        public const string HeadingName = "Party heading", CloseName = "Close social", PartyBlock = "Party block";
        public const string OnlineName = "Rail online", MembersName = "Rail members", MemberPrefix = "Member ", StatusName = "Rail party status";
        public const string SitOutName = "Rail sit out", InviteButton = "Rail invite", PasteButton = "Rail paste code";
        public const string CopyButton = "Rail copy code", LeaveButton = "Rail leave", TabsName = "Social tabs", TabPrefix = "Tab ";
        public const string ListArea = "Social list", RecentPrefix = "Recent match ", ClearButton = "Clear recent", EmptyName = "Recent empty";
        public const string ServerLineName = "Server line", ServerLineText = "Server line text";
        public const string InviteWord = "Invite friends", PasteWord = "Paste code", ClearWord = "Clear list", YouWord = "You";
        const float HeadHeight = 40f, PillHeight = 44f, PillText = 18f, BodyText = 15f, TitleText = 18f, TitleMinimum = 15f;
        const float TitleHeight = 26f, MemberHeight = 40f, MemberSquare = 30f, SeatSquare = 26f, RowPad = 8f, RowGap = 4f, ListMinimum = 72f;
        const float ServerLineHeight = 48f, PopRise = 12f, Inner = SocialRail.PanelWidth - 2f * PanelPad;
        const int PanelPad = 18, RowRadius = 12, SquareRadius = 6, SquareEdge = 2;

        RectTransform panel, shade, panelColumn, partyHost, online, members, tabs, list;
        TextMeshProUGUI heading, status, sitOut, serverLine;
        Image serverLineDot;
        Button close, inviteButton, pasteButton, copyButton, leaveButton;
        GameObject soloRow, partyRow;
        string section = SocialRail.Party, tab = SocialRail.Recent, panelSignature, listSignature, tabsSignature;
        Selectable opener;

        public RectTransform Panel => panel;
        public RectTransform PartyHost => partyHost;
        public bool Expanded => panel && panel.gameObject.activeSelf;
        public string Section => section;
        public string Tab => tab;

        void BuildShade()
        {
            shade = LobbyKit.Rect(host, ShadeName).Fill();
            shade.Paint(Color.clear);
            shade.gameObject.AddComponent<LobbyShade>().Clicked = () => Close(false);
            shade.gameObject.SetActive(false);
        }

        void BuildPanel()
        {
            panel = LobbyKit.Rect(host, PanelName);
            LobbyKit.PanelFace(panel, RailRadius, 1, 0).raycastTarget = true;
            panelColumn = LobbyKit.Column(panel, PanelColumn, 10, PanelPad).Fill();
            panelColumn.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
            var head = LobbyKit.Row(panelColumn, HeadName, 8).Size(-1, HeadHeight);
            head.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            heading = LobbyKit.Heading(head, SocialRail.Heading(1, false));
            heading.name = HeadingName;
            heading.Size(-1, 34, 1);
            close = LobbyKit.IconButton(head, LobbyIcons.Close, "Close", () => Close(true));
            close.name = CloseName;
            close.Size(HeadHeight, HeadHeight);
            partyHost = LobbyKit.Column(panelColumn, PartyBlock, 10, 0);
            BuildOnline();
            tabs = LobbyKit.Row(panelColumn, TabsName, 6).Size(-1, PillHeight);
            tabs.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            var area = LobbyKit.Rect(panelColumn, ListArea);
            var room = area.gameObject.AddComponent<LayoutElement>();
            room.flexibleHeight = 1; room.minHeight = ListMinimum;
            list = LobbyKit.Scroll(area, ListArea + " content", RowGap * 2f);
            var line = LobbyKit.Row(panelColumn, ServerLineName, 8).Size(-1, ServerLineHeight);
            line.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            serverLineDot = LobbyKit.Disc(line, ServerDotName, DotSize, LobbyKit.Line);
            serverLineDot.Size(DotSize, DotSize);
            serverLine = LobbyKit.Text(line, "", BodyText, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
            serverLine.name = ServerLineText;
            serverLine.textWrappingMode = TextWrappingModes.Normal;
            serverLine.Size(-1, ServerLineHeight, 1);
            panel.gameObject.SetActive(false);
        }

        void BuildOnline()
        {
            online = LobbyKit.Column(panelColumn, OnlineName, 8, 0);
            sitOut = Words(online, SocialRail.SitOut, SitOutName);
            members = LobbyKit.Column(online, MembersName, 6, 0);
            status = Words(online, "", StatusName);
            var solo = LobbyKit.Column(online, "Rail solo", 6, 0);
            soloRow = solo.gameObject;
            inviteButton = Pill(solo, InviteButton, InviteWord, InviteFriends);
            pasteButton = Pill(solo, PasteButton, PasteWord, PasteCode);
            var group = LobbyKit.Row(online, "Rail party row", 8).Size(-1, PillHeight);
            group.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            partyRow = group.gameObject;
            copyButton = Pill(group, CopyButton, LobbyPartyStrip.CopyCode, LobbyPartyStrip.CopyNow);
            copyButton.Size(-1, PillHeight, 1);
            leaveButton = Pill(group, LeaveButton, LobbyPartyStrip.LeaveWord, LobbyPartyStrip.LeaveOrRetry);
            leaveButton.Size(-1, PillHeight, 1);
            online.gameObject.SetActive(OnlineBuild);
        }

        static Button Pill(Transform parent, string name, string word, System.Action click)
        {
            var button = LobbyKit.Pill(parent, name, word, PillText, click);
            button.Size(-1, PillHeight);
            return button;
        }

        static TextMeshProUGUI Words(Transform parent, string text, string name)
        {
            var words = LobbyKit.Text(parent, text, BodyText, LobbyKit.Muted, TextAlignmentOptions.TopLeft);
            words.name = name;
            words.textWrappingMode = TextWrappingModes.Normal;
            Fit(words);
            return words;
        }

        static void Fit(TextMeshProUGUI words)
        {
            float height = string.IsNullOrEmpty(words.text) ? 0f : Mathf.Ceil(words.GetPreferredValues(words.text, Inner, 0f).y) + 4f;
            words.Size(-1, height);
            if (words.gameObject.activeSelf != height > 0f) words.gameObject.SetActive(height > 0f);
        }

        void InviteFriends()
        {
            if (menu.Starting) return;
            Close(false);
            menu.InviteFriends();
        }

        void PasteCode()
        {
            if (menu.Starting) return;
            PlayPage.JoinByCode(menu, PlayPage.CodeFrom(""));
        }

        public void Toggle(string from)
        {
            if (Expanded && section == SectionOf(from) && opener == Opener(from)) Close(true);
            else Show(from);
        }

        public void ToggleParty()
        {
            if (Expanded) Close(true);
            else Show(SocialRail.Party);
        }

        static string SectionOf(string from) => from == SocialRail.Recent || from == SocialRail.Friends || from == SocialRail.Nearby ||
            from == SocialRail.Requests ? from : SocialRail.Party;

        public void Show(string from)
        {
            if (!panel || menu.Starting) return;
            var events = EventSystem.current;
            var selected = events ? events.currentSelectedGameObject : null;
            if (selected && !Owns(selected)) returnTo = selected;
            section = SectionOf(from);
            if (section != SocialRail.Party) tab = section;
            opener = Opener(from);
            bool opening = !Expanded;
            shade.gameObject.SetActive(true);
            panel.gameObject.SetActive(true);
            if (opening) LobbyPop.On(panelColumn).Play(new Vector2(PopRise, 0f));
            RefreshPanel(true);
            var target = from == SocialRail.Invite && inviteButton.isActiveAndEnabled && inviteButton.interactable ? inviteButton
                : section != SocialRail.Party ? FirstIn(list) ?? TabButton(tab) : null;
            if (events) events.SetSelectedGameObject((target ? target : PanelFirst ?? close).gameObject);
        }

        public void Close(bool focusOpener)
        {
            if (!panel) return;
            bool wasInside = Focused;
            shade.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
            Wire();
            if (!EventSystem.current) return;
            if (focusOpener && opener && opener.isActiveAndEnabled) EventSystem.current.SetSelectedGameObject(opener.gameObject);
            else if (wasInside)
            {
                var back = ReturnSelectable;
                if (back) EventSystem.current.SetSelectedGameObject(back.gameObject);
            }
        }

        void RefreshPanel(bool force)
        {
            var lobbyParty = WordboundLobbyParty.Current;
            Set(heading, LobbyKit.Upper(SocialRail.Heading(Count, Online)));
            string state = OnlineState(lobbyParty) + "|" + menu.PartySize + "|" + check.State + "|" + check.Milliseconds;
            string listKey = tab + "|" + PlayerPrefs.GetString(RecentPlayers.StorageKey, "");
            if (!force && state == panelSignature && listKey == listSignature) return;
            bool listChanged = force || listKey != listSignature;
            panelSignature = state;
            listSignature = listKey;
            ShowOnline(lobbyParty);
            ShowServerLine(lobbyParty);
            BuildTabs();
            if (listChanged) BuildList();
            Wire();
        }

        string OnlineState(IWordboundLobbyParty lobbyParty)
        {
            if (!Online || lobbyParty == null) return "solo";
            return string.Join("|", lobbyParty.Members) + "|" + lobbyParty.Status + "|" + lobbyParty.Problem + "|" + lobbyParty.Copied + "|" +
                lobbyParty.CanCopy + "|" + lobbyParty.CanLeave + "|" + lobbyParty.CanRetry;
        }

        void ShowOnline(IWordboundLobbyParty lobbyParty)
        {
            if (!OnlineBuild) return;
            if (soloRow.activeSelf == Online) soloRow.SetActive(!Online);
            if (partyRow.activeSelf != Online) partyRow.SetActive(Online);
            LobbyKit.Clear(members);
            sitOut.text = Online && menu.PartySize > 1 ? SocialRail.SitOut : "";
            Fit(sitOut);
            if (!Online)
            {
                status.text = "";
                Fit(status);
                inviteButton.interactable = pasteButton.interactable = !menu.Starting;
                return;
            }
            for (int i = 0; i < Plates.Count; i++) Member(Plates[i], i + 1);
            status.text = lobbyParty.Problem ?? lobbyParty.Status ?? "";
            status.color = lobbyParty.Problem != null ? LobbyKit.Hot : LobbyKit.Muted;
            Fit(status);
            copyButton.interactable = lobbyParty.CanCopy;
            Set(copyButton.GetComponentInChildren<TMP_Text>(true), LobbyKit.Upper(lobbyParty.Copied ? LobbyPartyStrip.Copied : LobbyPartyStrip.CopyCode));
            bool retry = lobbyParty.CanRetry;
            leaveButton.interactable = retry || lobbyParty.CanLeave;
            Set(leaveButton.GetComponentInChildren<TMP_Text>(true), LobbyKit.Upper(retry ? LobbyPartyStrip.RetryWord : LobbyPartyStrip.LeaveWord));
        }

        void Member(PartyPlate plate, int number)
        {
            var row = LobbyKit.Row(members, MemberPrefix + number, 10, 0).Size(-1, MemberHeight);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            Square(row, plate.Initial, plate.Place, MemberSquare);
            var name = LobbyKit.Display(row, plate.Name, TitleText, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft);
            name.enableAutoSizing = true; name.fontSizeMin = TitleMinimum; name.fontSizeMax = TitleText;
            name.Size(-1, MemberHeight, 1);
            if (plate.Tag.Length > 0) LobbyKit.Caps(row, plate.Tag, BodyText, TextAlignmentOptions.MidlineRight).Size(-1, MemberHeight);
        }

        static RectTransform Square(Transform parent, string initial, int seat, float size)
        {
            var square = LobbyKit.Rect(parent, "Seat square");
            LobbyKit.Face(square, SeatColour(seat), SquareRadius, LobbyKit.Navy, SquareEdge);
            square.Size(size, size);
            var letter = LobbyKit.Display(square, initial, BodyText, LobbyKit.ArtInk);
            letter.enableAutoSizing = true; letter.fontSizeMin = BodyText; letter.fontSizeMax = BodyText;
            letter.rectTransform.Fill();
            return square;
        }

        void ShowServerLine(IWordboundLobbyParty lobbyParty)
        {
            Set(serverLine, ServerLine(lobbyParty));
            var shown = ServiceText.Dot(check.State);
            serverLineDot.enabled = shown != ServiceDot.None;
            serverLineDot.color = DotColour(shown);
        }

        void BuildTabs()
        {
            var names = SocialRail.Tabs(Services);
            string key = string.Join(",", names) + "|" + tab;
            if (key == tabsSignature) return;
            tabsSignature = key;
            LobbyKit.Clear(tabs);
            foreach (string name in names)
            {
                var button = LobbyKit.TextAction(tabs, TabPrefix + name, name, PillText, () => ShowTab(name), name == tab);
                button.Size(-1, PillHeight, 1);
            }
        }

        Button TabButton(string name)
        {
            var found = tabs ? tabs.Find(TabPrefix + name) : null;
            return found && found.gameObject.activeSelf ? found.GetComponent<Button>() : null;
        }

        void ShowTab(string name)
        {
            if (tab == name) return;
            tab = name;
            section = name;
            RefreshPanel(true);
            var target = TabButton(name);
            if (target && EventSystem.current) EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        void BuildList()
        {
            LobbyKit.Clear(list);
            if (tab == SocialRail.Recent) BuildRecent();
        }

        void BuildRecent()
        {
            var recent = LobbyRecentPlayers.Load();
            long now = LobbyRecentPlayers.Now;
            if (recent.Matches.Count == 0)
            {
                var empty = Words(list, RecentPlayers.Empty, EmptyName);
                empty.color = LobbyKit.Muted;
                return;
            }
            for (int i = 0; i < recent.Matches.Count; i++) RecentRow(recent.Matches[i], i + 1, now);
            var clear = LobbyKit.TextAction(list, ClearButton, ClearWord, BodyText, ClearRecent, ink: LobbyKit.Muted);
            clear.Size(-1, PillHeight);
            clear.gameObject.AddComponent<LobbyHint>().Text = "Forget the matches kept on this device";
        }

        void RecentRow(RecentMatch match, int number, long now)
        {
            string names = HouseFirst(match) + RecentPlayers.Names(match, YouWord) + MapCardText.Separator + RecentPlayers.When(match.EndedAt, now);
            float textWidth = Inner - 2f * RowPad;
            var button = LobbyKit.Button(list, RecentPrefix + number, LobbyKit.Card, null, RowRadius, LobbyKit.Line, 1);
            var body = LobbyKit.Column(button.Body(), "Content", RowGap, (int)RowPad).Fill();
            var title = LobbyKit.Display(body, LobbyMenu.ModeName(match.Mode), TitleText, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft);
            title.enableAutoSizing = true; title.fontSizeMin = TitleMinimum; title.fontSizeMax = TitleText;
            title.Size(-1, TitleHeight);
            var seats = LobbyKit.Row(body, "Seats", 3).Size(-1, SeatSquare);
            seats.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            foreach (var player in match.Players) Square(seats, player.Initial, player.Seat, SeatSquare);
            var line = LobbyKit.Text(body, names, BodyText, LobbyKit.Muted, TextAlignmentOptions.TopLeft);
            line.textWrappingMode = TextWrappingModes.Normal;
            float lineHeight = Mathf.Ceil(line.GetPreferredValues(names, textWidth, 0f).y) + 2f;
            line.Size(-1, lineHeight);
            button.Size(-1, 2f * RowPad + TitleHeight + RowGap + SeatSquare + RowGap + lineHeight);
            button.gameObject.AddComponent<LobbyHint>().Text = names;
        }

        static string HouseFirst(RecentMatch match) =>
            GameConfig.Current.Houses.TryGetValue(match.Map ?? "", out var house) ? house.Name + MapCardText.Separator : "";

        void ClearRecent()
        {
            LobbyRecentPlayers.Clear();
            RefreshPanel(true);
            var target = TabButton(tab);
            if (target && EventSystem.current) EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        Selectable PanelFirst => PanelOrder().FirstOrDefault();

        static Selectable FirstIn(Transform root) =>
            root ? root.GetComponentsInChildren<Selectable>().FirstOrDefault(selectable => selectable.IsInteractable()) : null;

        IReadOnlyList<Selectable> PanelOrder()
        {
            if (!panel) return new Selectable[0];
            var order = panel.GetComponentsInChildren<Selectable>().Where(selectable => selectable.IsInteractable() && selectable != close).ToList();
            order.Add(close);
            return order;
        }

        void WirePanel()
        {
            var order = PanelOrder();
            var right = opener && opener.isActiveAndEnabled ? opener : party;
            for (int i = 0; i < order.Count; i++)
            {
                var before = i > 0 ? order[i - 1] : null;
                var after = i < order.Count - 1 ? order[i + 1] : null;
                bool sameRowBefore = before && before.transform.parent == order[i].transform.parent && RowOf(order[i]);
                bool sameRowAfter = after && after.transform.parent == order[i].transform.parent && RowOf(order[i]);
                order[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = before,
                    selectOnDown = after,
                    selectOnLeft = sameRowBefore ? before : null,
                    selectOnRight = sameRowAfter ? after : right,
                };
            }
        }

        static bool RowOf(Selectable selectable) => selectable.transform.parent && selectable.transform.parent.GetComponent<HorizontalLayoutGroup>();

    }
}
