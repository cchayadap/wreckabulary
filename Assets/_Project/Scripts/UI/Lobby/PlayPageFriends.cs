using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary
{
    public sealed partial class PlayPage
    {
        public const string RoomModeCard = "Room mode ", TeamSizeChoice = "Room team size", LayoutChoice = "Room layout";
        public const string LayoutRow = "Room layout row", LayoutPicture = "Room layout picture", LayoutCourse = "Room layout course";
        public const string CreateRoom = "Create room GO", RoomModes = "Room modes", RoomModesFamily = "Room mode tiles";
        public const string BackToPlaylists = "Back to playlists", CustomMap = "Host a custom map", FriendsSummary = "Friends summary";
        public const string CustomMatchNote = "Custom match note", AmongYourselves = "Play among yourselves. Only people with the code can join.";
        const float LayoutHeight = 56f, LayoutThumb = 96f, VersionWidth = 72f, VersionHeight = 52f, LayoutGap = 16f, CourseText = 22f;
        const int ThumbRadius = 8;
        public bool Friends;

        void ShowFriends()
        {
            Friends = true;
            Refresh();
            Reselect(CreateRoom);
        }

        void ShowPlaylists()
        {
            Friends = false;
            Refresh();
            Reselect(CustomMatch);
        }

        void FriendsChoices(RectTransform column, RectTransform body)
        {
            var choice = new FriendRoomChoice(PlayerPrefs.GetString(FriendRooms.StorageKey, ""));
            bool available = Session.CanLoad(Session.WordboundScene);
            var selected = choice.Selected;
            var sizes = FriendRooms.TeamSizes.Select(size => (TeamSizeChoice + " " + size, FriendRooms.TeamLabel(size))).ToArray();
            var header = TeamSizeRowFor(column, TeamSizeChoice, sizes, TeamSizeChoice + " " + choice.TeamSize,
                id => SaveRoomChoice(next => next.ChooseTeamSize(id.Substring(TeamSizeChoice.Length + 1)), id),
                selected.Teams ? new string[0] : sizes.Select(size => size.Item1).ToArray(), selected.Teams ? null : NoTeams(selected));
            CodeBoxFor(header, available);
            var top = LobbyKit.Row(column, "Host a room row", 16);
            top.Size(-1, 28);
            LobbyKit.SectionLabel(top, "Host a room");
            var note = LobbyKit.Text(top, AmongYourselves, 17, LobbyKit.Muted, TextAlignmentOptions.BottomRight);
            note.name = CustomMatchNote;
            note.Size(-1, 28, 1);
            var tiles = ModeTiles(column, RoomModes, RoomModesFamily);
            foreach (var room in FriendRooms.All) RoomModeCardFor(tiles, choice, room);
            SizeTiles(tiles.GetComponentInParent<LobbyPlayModeFamilies>());
            LobbyKit.SectionLabel(column, "Map");
            LayoutRowFor(column, choice);
            var custom = LobbyKit.TextAction(column, CustomMap, "Host a custom map from the Map editor", 17, Menu.HostCustomMap,
                icon: LobbyIcons.Layers, ink: LobbyKit.Muted);
            custom.Size(-1, 44);
            custom.interactable = available;
            HowToPlay(column, selected.Mode, false);

            var bar = SummaryFrame(body, FriendsSummary);
            SummaryLine(bar);
            var heading = LobbyKit.Text(bar, choice.Summary(), 26, LobbyKit.Navy, TextAlignmentOptions.BottomLeft);
            heading.name = "Friends room";
            heading.enableAutoSizing = true; heading.fontSizeMin = 18; heading.fontSizeMax = 26;
            heading.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 44), new Vector2(-560, -8));
            GuideButton(heading, selected.Mode);
            string region = QuickPlayRegions.Name(QuickPlayRegions.Nearest(QuickPlayRegions.LocalOffsetMinutes()));
            var detail = LobbyKit.Text(bar, available
                    ? FriendRooms.Players(selected.Mode, choice.Format) + MapCardText.Separator + "Online" + MapCardText.Separator + region
                    : "Online play is unavailable in this build.",
                18, available ? LobbyKit.Muted : LobbyKit.Hot, TextAlignmentOptions.TopLeft);
            detail.name = "Friends detail";
            detail.enableAutoSizing = true; detail.fontSizeMin = 14; detail.fontSizeMax = 18;
            detail.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 10), new Vector2(-560, -48));
            var back = LobbyKit.Pill(bar, BackToPlaylists, "Playlists", 18, ShowPlaylists);
            ((RectTransform)back.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-544, -22), new Vector2(-332, 22));
            var go = LobbyKit.Primary(bar, CreateRoom, "Create room",
                () => Menu.HostFriendsRoom(selected.Mode, choice.Format, choice.Layout), 36, 26);
            ((RectTransform)go.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-316, -30), new Vector2(-8, 30));
            go.interactable = available;
            if (Aim != null)
            {
                var aimed = Root.GetComponentsInChildren<Selectable>().FirstOrDefault(s => s.name == Aim && s.IsInteractable());
                First = aimed ? aimed : go;
                Aim = null;
            }
            else First = available ? go : back;
        }

        public static string NoTeams(FriendRoomMode room) => room.Label + " has no teams";

        public static string VersionLabel(int layout) => (layout + 1).ToString(CultureInfo.InvariantCulture);

        void RoomModeCardFor(Transform cards, FriendRoomChoice choice, FriendRoomMode room)
        {
            string name = RoomModeCard + room.Id;
            var card = ModeTile(cards, name, room.Label, LobbyPlayModeLabels.Emblem(room.Mode), false, choice.Mode == room.Id,
                () => SaveRoomChoice(next => next.Choose(room.Id), name));
            card.gameObject.AddComponent<LobbyHint>().Text = room.Detail;
        }

        void LayoutRowFor(RectTransform column, FriendRoomChoice choice)
        {
            string mode = choice.Selected.Mode;
            var row = LobbyKit.Row(column, LayoutRow, LayoutGap);
            row.Size(-1, LayoutHeight);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            var thumb = LobbyKit.Rect(row, LayoutPicture);
            thumb.Size(LayoutThumb, LayoutThumb / LobbyPlayCardGrid.PictureAspect);
            var sprite = CoursePicture(mode);
            thumb.Paint(sprite ? Color.white : Color.Lerp(LobbyKit.Raised, LobbyKit.Selection, .14f), ThumbRadius).raycastTarget = false;
            if (sprite)
            {
                thumb.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                var image = LobbyKit.Rect(thumb, "Map preview").Fill().gameObject.AddComponent<Image>();
                image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
            }
            else
            {
                var art = LobbyKit.ItemImage(thumb, LobbyPlayModeLabels.Emblem(mode), LayoutThumb / LobbyPlayCardGrid.PictureAspect);
                if (art) art.rectTransform.Pin(new Vector2(.5f, .5f), Vector2.zero, Vector2.one * (LayoutThumb / LobbyPlayCardGrid.PictureAspect));
            }
            var course = LobbyKit.Text(row, OnlineMaps.Name(OnlineMaps.CourseFor(mode)) ?? OnlineMaps.PrivateMap, CourseText, LobbyKit.Navy,
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            course.name = LayoutCourse;
            course.Size(Mathf.Ceil(course.GetPreferredValues(course.text).x) + TagSlack, LayoutHeight);
            var versions = Enumerable.Range(0, FriendRooms.Layouts).Select(index => (LayoutChoice + " " + (index + 1), VersionLabel(index))).ToArray();
            LobbyKit.Segmented(row, LayoutChoice, versions, LayoutChoice + " " + (choice.Layout + 1),
                id => SaveRoomChoice(next => next.ChooseLayout(int.Parse(id.Substring(LayoutChoice.Length + 1), CultureInfo.InvariantCulture) - 1), id), VersionHeight, 20)
                .Size(VersionWidth * FriendRooms.Layouts + 8, VersionHeight);
        }

        void SaveRoomChoice(System.Func<FriendRoomChoice, bool> change, string aim)
        {
            var choice = new FriendRoomChoice(PlayerPrefs.GetString(FriendRooms.StorageKey, ""));
            if (!change(choice)) return;
            PlayerPrefs.SetString(FriendRooms.StorageKey, choice.Save());
            PlayerPrefs.Save();
            Refresh();
            Reselect(aim);
        }

        TMP_InputField CodeInput(RectTransform row, string name, string placeholder, string draft,
            System.Action<string> keep, System.Action submit)
        {
            var holder = LobbyKit.Rect(row, name);
            holder.gameObject.SetActive(false);
            holder.Size(-1, 48, 1);
            holder.Paint(LobbyKit.Raised, 6);
            LobbyKit.Frame(holder, LobbyKit.Line, 6, 1);
            var area = LobbyKit.Rect(holder, "Text area").Place(Vector2.zero, Vector2.one, new Vector2(14, 2), new Vector2(-14, -2));
            area.gameObject.AddComponent<RectMask2D>();
            var hint = LobbyKit.Text(area, placeholder, 20, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
            hint.enableAutoSizing = true; hint.fontSizeMin = SmallestLabel; hint.fontSizeMax = 20;
            hint.rectTransform.Fill();
            var text = LobbyKit.Text(area, "", 20, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft);
            text.overflowMode = TextOverflowModes.Overflow;
            text.rectTransform.Fill();
            var field = holder.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = hint;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = FriendRooms.InviteLength;
            field.customCaretColor = true;
            field.caretColor = LobbyKit.Navy;
            field.caretWidth = 2;
            field.selectionColor = new Color(LobbyKit.Selection.r, LobbyKit.Selection.g, LobbyKit.Selection.b, .5f);
            field.text = draft;
            field.onValueChanged.AddListener(value => keep(value));
            field.onSubmit.AddListener(_ => submit());
            holder.gameObject.SetActive(true);
            return field;
        }
    }
}
