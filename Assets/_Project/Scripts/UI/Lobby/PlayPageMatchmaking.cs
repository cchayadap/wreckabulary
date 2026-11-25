using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary
{
    public sealed partial class PlayPage
    {
        public const string PlaylistCard = "Playlist ", FindMatch = "Find match GO", CustomMatch = "Custom match";
        public const string MatchmakingSummary = "Matchmaking summary", KeepOnePlaylist = "Keep at least one playlist ticked.";
        public const string PartyStrip = "Party strip", PartyLabel = "Party label", InviteFriendsButton = "Invite friends";
        public const string CodeBox = "Code box", CodeField = "Code field", JoinCode = "Join code";
        public const string PartySolo = "Party solo", PartyGroup = "Party group", PartyHeading = "Party heading", PartyMembers = "Party members";
        public const string PartyPlatesRow = "Party plates";
        public const string CopyPartyCode = "Copy party code", LeaveParty = "Leave party";
        public const string TeamSizeWord = "Team size", TeamSizeSwitch = "Team size", TeamSizeRow = "Team size row";
        public const string TeamSizeTitle = "Team size label", TeamSizeNote = "Team size note";
        public const string PlaylistBand = "Playlist band", PlaylistSection = "Playlists ", PlaylistDetail = "Playlist detail";
        public const string PlayersBadge = "Players badge", PlayersCount = "Players count";
        const float DisabledSizeAlpha = .6f, SkippedAlpha = .55f, CodeMinimum = 280f, JoinWidth = 130f;
        const float BadgeInset = 8f, BadgeWidth = 66f, BadgeHeight = 30f, BadgeIcon = 18f, BadgeText = 18f, BadgeGap = 4f;
        const int BadgeRadius = 15, BadgePadding = 6;
        public bool Online;
        string codeDraft = "";
        string detailCard;

        void MatchmakingChoices(RectTransform column, RectTransform body)
        {
            var choices = new QuickPlayChoices(PlayerPrefs.GetString(QuickPlayChoices.StorageKey, ""));
            bool available = Session.CanLoad(Session.WordboundScene);
            int partySize = OnlinePartySize();
            string size = choices.SizeFor(partySize);
            var top = TeamSizeRowFor(column, TeamSizeSwitch,
                QuickPlayCards.TeamSizes.Select(each => (TeamSizeSwitch + " " + each, QuickPlayCards.TeamLabel(each))).ToArray(), TeamSizeSwitch + " " + size,
                id => ChooseTeamSize(id.Substring(TeamSizeSwitch.Length + 1), id),
                QuickPlayCards.TeamSizes.Where(each => !QuickPlayCards.CanPlay(each, partySize)).Select(each => TeamSizeSwitch + " " + each).ToArray(),
                QuickPlayCards.CanPlay(QuickPlayCards.Duo, partySize) || partySize > QuickPlaylists.MaximumParty ? null : PartyPlaysSquad(partySize));
            var code = CodeBoxFor(top, available);
            var strip = PartyStripFor(column, available);
            var band = LobbyKit.Rect(column, PlaylistBand);
            band.Size(-1, LobbyPlayColumnFlow.Head + LobbyPlayColumnFlow.MaximumHeight);
            var flow = band.gameObject.AddComponent<LobbyPlayColumnFlow>();
            var about = LobbyPlayDetail.Create(band, PlaylistDetail);
            flow.Side = (RectTransform)about.transform;
            var covered = new List<RectTransform> { flow.Side };
            foreach (string group in QuickPlaylists.Groups)
            {
                var section = LobbyKit.Rect(band, PlaylistSection + group);
                covered.Add(section);
                LobbyKit.SectionLabel(section, group).name = LobbyPlayColumnFlow.Caption;
                foreach (var card in QuickPlayCards.All.Where(card => card.Group == group)) PlaylistCardFor(section, choices, card, size, partySize, about);
            }
            ShowDetail(about, choices);
            var moment = LobbyMatchBand.Create(band, covered);
            flow.Overlay = (RectTransform)moment.transform;
            flow.Fit();
            var bar = SummaryFrame(body, MatchmakingSummary);
            SummaryLine(bar);
            string summary = PlayPageText.SearchLine(choices, partySize);
            var heading = LobbyKit.Text(bar, summary, 26, LobbyKit.Navy, TextAlignmentOptions.BottomLeft);
            heading.name = "Matchmaking playlists";
            heading.enableAutoSizing = true; heading.fontSizeMin = 18; heading.fontSizeMax = 26;
            heading.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 44), new Vector2(-560, -8));
            string region = QuickPlayRegions.Name(QuickPlayRegions.Nearest(QuickPlayRegions.LocalOffsetMinutes()));
            string onlineDetail = available ? "Online" + MapCardText.Separator + region : "Online play is unavailable in this build.";
            var detail = LobbyKit.Text(bar, onlineDetail, 18, available ? LobbyKit.Muted : LobbyKit.Hot, TextAlignmentOptions.TopLeft);
            detail.name = "Matchmaking detail";
            detail.enableAutoSizing = true; detail.fontSizeMin = 14; detail.fontSizeMax = 18;
            detail.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 10), new Vector2(-560, -48));
            var friends = LobbyKit.Pill(bar, CustomMatch, "Custom match", 18, ShowFriends);
            ((RectTransform)friends.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-544, -22), new Vector2(-332, 22));
            friends.interactable = available;
            var go = LobbyKit.Primary(bar, FindMatch, "Find match", Menu.PlayOnline, 42, 26);
            ((RectTransform)go.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-316, -30), new Vector2(-8, 30));
            go.interactable = available;
            bool fits = PlayPageText.Playable(choices, partySize).Count > 0;
            strip.BindParty(code.gameObject, moment, fits ? choices.PartyLine(partySize) : null, PlayPageText.PartyLine(partySize, region));
            strip.BindFooter(heading, detail, go, friends, summary, onlineDetail, available);
            strip.WatchPartySize(partySize, KeepSelectionAndRefresh);
            if (Aim != null)
            {
                var aimed = Root.GetComponentsInChildren<Selectable>().FirstOrDefault(s => s.name == Aim && s.IsInteractable());
                First = aimed ? aimed : go;
                Aim = null;
            }
            else if (available) First = go;
        }

        public static string PartyPlaysSquad(int partySize) =>
            "A party of " + partySize + " plays " + QuickPlayCards.TeamLabel(QuickPlayCards.Squad);

        static int OnlinePartySize()
        {
            var party = WordboundLobbyParty.Current;
            return party != null && party.Active ? Mathf.Max(1, party.Members.Count) : 1;
        }

        void KeepSelectionAndRefresh()
        {
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            string name = selected && selected.transform.IsChildOf(Root) ? selected.name : null;
            Refresh();
            if (name == null) return;
            var target = Root.GetComponentsInChildren<Selectable>().FirstOrDefault(s => s.name == name && s.IsInteractable()) ??
                Root.GetComponentsInChildren<Selectable>().FirstOrDefault(s => s.name.StartsWith(TeamSizeSwitch + " ") && s.IsInteractable());
            if (target) EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        RectTransform TeamSizeRowFor(RectTransform column, string name, (string id, string label)[] sizes, string picked, Action<string> pick,
            string[] disabled, string note)
        {
            var row = LobbyKit.Row(column, name + " row", 16);
            row.Size(-1, 52);
            var title = LobbyKit.Text(row, TeamSizeWord, 20, LobbyKit.Cyan, TextAlignmentOptions.MidlineLeft);
            title.name = TeamSizeTitle;
            title.Size(130, 48);
            var track = LobbyKit.Segmented(row, name, sizes, picked, pick, 48, 20);
            track.Size(110 * sizes.Length + 8, 48);
            foreach (var button in track.GetComponentsInChildren<Button>().Where(button => disabled.Contains(button.name)))
            {
                button.interactable = false;
                button.gameObject.AddComponent<CanvasGroup>().alpha = DisabledSizeAlpha;
            }
            if (note == null) return row;
            var line = LobbyKit.Text(row, note, 17, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
            line.name = TeamSizeNote;
            line.enableAutoSizing = true; line.fontSizeMin = SmallestLabel; line.fontSizeMax = 17;
            line.Size(-1, 48, 1);
            return row;
        }

        RectTransform CodeBoxFor(RectTransform row, bool available)
        {
            var box = LobbyKit.Row(row, CodeBox, 12);
            box.Size(-1, 48, 1);
            box.GetComponent<LayoutElement>().minWidth = CodeMinimum + JoinWidth;
            TMP_InputField field = null;
            field = CodeInput(box, CodeField, PlayPageText.CodeHint, codeDraft, value => codeDraft = value, () => JoinWithCode(field));
            field.interactable = available;
            var join = LobbyKit.Pill(box, JoinCode, "Join", 18, () => JoinWithCode(field));
            join.Size(JoinWidth, 48);
            join.interactable = available;
            return box;
        }

        void ChooseTeamSize(string size, string id)
        {
            var choices = new QuickPlayChoices(PlayerPrefs.GetString(QuickPlayChoices.StorageKey, ""));
            if (!QuickPlayCards.CanPlay(size, OnlinePartySize()) || !choices.ChooseTeamSize(size)) return;
            PlayerPrefs.SetString(QuickPlayChoices.StorageKey, choices.Save());
            PlayerPrefs.Save();
            Refresh();
            Reselect(id);
        }

        LobbyPartyStrip PartyStripFor(RectTransform column, bool available)
        {
            var strip = LobbyKit.Rect(column, PartyStrip);
            strip.gameObject.SetActive(false);
            strip.Size(-1, 52);
            var solo = LobbyKit.Row(strip, PartySolo, 12, 2);
            solo.Fill();
            var label = LobbyKit.Text(solo, "Party" + MapCardText.Separator + "You", 20, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft);
            label.name = PartyLabel;
            label.Size(150, 48);
            var invite = LobbyKit.Pill(solo, InviteFriendsButton, "Invite friends", 18, Menu.InviteFriends);
            invite.Size(220, 48);
            invite.interactable = available;
            var group = LobbyKit.Row(strip, PartyGroup, 12, 2);
            group.Fill();
            var heading = LobbyKit.Text(group, "", 20, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft);
            heading.name = PartyHeading;
            heading.Size(150, 48);
            var plates = LobbyPartyChips.Create(group, PartyPlatesRow, false);
            plates.Size(-1, LobbyPartyChips.Height, 1);
            var members = LobbyKit.Text(group, "", 18, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
            members.name = PartyMembers;
            members.enableAutoSizing = true; members.fontSizeMin = SmallestLabel; members.fontSizeMax = 18;
            members.textWrappingMode = TextWrappingModes.Normal;
            members.Size(-1, 48, 1);
            var copy = LobbyKit.Pill(group, CopyPartyCode, LobbyPartyStrip.CopyCode, 18, LobbyPartyStrip.CopyNow);
            copy.Size(190, 48);
            var leave = LobbyKit.Pill(group, LeaveParty, LobbyPartyStrip.LeaveWord, 18, LobbyPartyStrip.LeaveOrRetry);
            leave.Size(130, 48);
            group.gameObject.SetActive(false);
            var live = strip.gameObject.AddComponent<LobbyPartyStrip>();
            live.BindStrip(solo.gameObject, group.gameObject, invite, heading, members, plates, copy, leave);
            strip.gameObject.SetActive(true);
            return live;
        }

        void JoinWithCode(TMP_InputField field)
        {
            string typed = (field.text ?? "").Trim();
            string code = CodeFrom(typed);
            if (code != typed) field.text = code;
            JoinByCode(Menu, code);
        }

        public static string CodeFrom(string typed)
        {
            string code = (typed ?? "").Trim();
            if (code.Length > 0) return code;
            string copied = GUIUtility.systemCopyBuffer;
            return FriendRooms.LooksLikePartyInvite(copied) || FriendRooms.LooksLikeInvite(copied) ? copied.Trim() : code;
        }

        public static void JoinByCode(LobbyMenu menu, string code)
        {
            if (FriendRooms.LooksLikePartyInvite(code)) menu.JoinFriendsParty(code);
            else if (FriendRooms.LooksLikeInvite(code)) menu.JoinFriendsRoom(code);
            else menu.Post(PlayPageText.NotACode);
        }

        void PlaylistCardFor(Transform section, QuickPlayChoices choices, QuickPlayCard card, string size, int partySize, LobbyPlayDetail about)
        {
            string name = PlaylistCard + card.Id;
            var list = card.PlaylistFor(size);
            string reason = PlayPageText.PartyReason(list, partySize);
            var button = MapCard(section, name, choices.Cards.IsTicked(card.Id), () => ToggleCard(card.Id, name));
            var picture = ModePicture(button, card.Mode);
            var detail = RowWords(button, picture, LobbyPlayModeLabels.Name(card.Mode), reason.Length > 0 ? PlayPageText.SkipLine(reason) : card.Detail(size));
            PeopleBadge(picture, list.Players);
            if (reason.Length > 0)
            {
                detail.color = LobbyKit.Hot;
                button.gameObject.AddComponent<CanvasGroup>().alpha = SkippedAlpha;
            }
            about.Add(OnlineEntry(card.Id, card.Mode));
            var source = button.gameObject.AddComponent<LobbyPlayDetailSource>();
            source.Detail = about; source.Id = card.Id;
        }

        static void PeopleBadge(RectTransform picture, int players)
        {
            var badge = LobbyKit.Rect(picture, PlayersBadge).Pin(Vector2.zero, new Vector2(BadgeInset, BadgeInset), new Vector2(BadgeWidth, BadgeHeight));
            LobbyKit.Face(badge, LobbyKit.Card, BadgeRadius, LobbyKit.Line, 1, 0);
            var row = LobbyKit.Row(badge, "Content", BadgeGap);
            row.Fill();
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter; layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(BadgePadding, BadgePadding, 0, 0);
            LobbyKit.Icon(row, LobbyIcons.Party, LobbyKit.Navy).Size(BadgeIcon, BadgeIcon);
            var count = LobbyKit.Display(row, players.ToString(CultureInfo.InvariantCulture), BadgeText, LobbyKit.Navy);
            count.name = PlayersCount;
            count.Size(-1, BadgeText + 6);
        }

        static RectTransform ModePicture(Selectable card, string mode) =>
            CardPicture(card, null, LobbyIcons.Layers, LobbyPlayModeLabels.Emblem(mode));

        LobbyPlayDetailEntry OnlineEntry(string id, string mode) => new LobbyPlayDetailEntry
        {
            Id = id, Guide = mode, Title = LobbyPlayModeLabels.Name(mode),
            Place = OnlineMaps.Name(OnlineMaps.CourseFor(mode)) ?? "",
            Who = ModeAccess.For(mode).Line,
            Picture = CoursePicture(mode), Emblem = LobbyPlayModeLabels.Emblem(mode),
            Steps = ModeAccess.Steps(mode)
        };

        Sprite CoursePicture(string mode) => LobbyMapSnapshots.ForMode(mode) ??
            (LobbyMapSnapshots.CourseKey(mode) == WordboundSoloEntry.LostAddressMap ? CoursePreview(WordboundSoloEntry.LostAddressMap) : null);

        void ShowDetail(LobbyPlayDetail about, QuickPlayChoices choices)
        {
            string aimed = Aim != null && Aim.StartsWith(PlaylistCard, StringComparison.Ordinal) ? Aim.Substring(PlaylistCard.Length) : null;
            about.Show(about.Has(aimed) ? aimed : about.Has(detailCard) ? detailCard :
                QuickPlayCards.Ids.FirstOrDefault(id => choices.Cards.IsTicked(id)) ?? QuickPlayCards.Ids[0]);
            detailCard = about.Shown;
            about.Changed = id => detailCard = id;
        }

        void ToggleCard(string id, string name)
        {
            var choices = new QuickPlayChoices(PlayerPrefs.GetString(QuickPlayChoices.StorageKey, ""));
            if (!choices.Cards.Toggle(id))
            {
                Menu.Post(KeepOnePlaylist);
                return;
            }
            PlayerPrefs.SetString(QuickPlayChoices.StorageKey, choices.Save());
            PlayerPrefs.Save();
            Refresh();
            Reselect(name);
        }
    }
}
