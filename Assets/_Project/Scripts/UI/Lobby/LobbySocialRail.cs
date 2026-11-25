using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary
{
    public sealed partial class LobbySocialRail : MonoBehaviour
    {
        public const string HostName = "Social", RailName = "Social rail", ColumnName = "Rail column", SlotsName = "Rail party";
        public const string ServerDotName = "Server dot", ServerTextName = "Server text", CountName = "Party count";
        public const string DividerName = "Rail divider", SpaceName = "Rail space", InitialName = "Portrait initial", ButtonLabelName = "Rail label";
        public static readonly SocialServices Services = SocialServices.None;
        const float ServerText = 18f, ServerTextMinimum = 15f, ServerWidth = 188f, ServerHeight = 52f;
        const float CountText = 18f, CountTextMinimum = 15f, CountIcon = 20f, DotSize = 12f;
        const float LabelText = 15f, LabelSlack = 4f, LabelBottom = 2f, GlyphSide = 16f, GlyphTop = 6f, GlyphGap = 2f;
        const float InitialMaximum = 24f, InitialMinimum = 15f, PlusInset = 14f, RefreshEvery = .25f;
        const int RailRadius = 16, CellRadius = 12, PortraitRadius = 8, PortraitEdge = 3, SlotEdge = 2, DividerLine = 2;

        LobbyMenu menu;
        RectTransform host, rail, column, slots;
        CanvasGroup group;
        Button server, party, invite;
        Image dot;
        TextMeshProUGUI serverText, count;
        LobbyHint serverHint;
        readonly List<Button> portraits = new List<Button>();
        readonly List<Button> buttons = new List<Button>();
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        ServiceCheck check;
        RailLayout layout;
        bool laidOut;
        string slotSignature, railSignature;
        float nextRefresh;
        GameObject returnTo;

        public RectTransform Rail => rail;
        public RailLayout Layout => layout;
        public ServiceCheck Service => check;
        public bool Online { get; private set; }
        public int Count { get; private set; } = 1;
        public IReadOnlyList<PartyPlate> Plates { get; private set; } = new PartyPlate[0];
        public float Edge => SocialRail.Margin + (laidOut ? layout.Width : SocialRail.Width) + SocialRail.Gap;
        public float FrameExtra => laidOut && layout.Docked ? layout.Inset : 0f;

        public bool Focused
        {
            get
            {
                var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
                return selected && Owns(selected);
            }
        }

        public static LobbySocialRail Build(RectTransform safe, LobbyMenu menu, RectTransform topRow)
        {
            var host = LobbyKit.Rect(safe, HostName).Fill();
            var social = host.gameObject.AddComponent<LobbySocialRail>();
            social.menu = menu;
            social.host = host;
            social.group = host.gameObject.AddComponent<CanvasGroup>();
            social.check = new ServiceCheck(Session.CanLoad(Session.WordboundScene), () => Time.realtimeSinceStartupAsDouble);
            social.BuildShade();
            social.BuildPanel();
            social.BuildServer(topRow);
            social.BuildRail();
            return social;
        }

        void OnDestroy()
        {
            lifetime.Cancel();
            lifetime.Dispose();
        }

        void BuildRail()
        {
            rail = LobbyKit.Rect(host, RailName);
            LobbyKit.PanelFace(rail, RailRadius, 1, 0).raycastTarget = true;
            column = LobbyKit.Column(rail, ColumnName, SocialRail.Spacing, (int)SocialRail.Pad).Fill();
            column.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
            BuildCount();
            slots = LobbyKit.Column(column, SlotsName, SocialRail.Spacing, 0);
            var divider = LobbyKit.Rect(column, DividerName).Size(-1, DividerLine);
            divider.Paint(LobbyKit.Line, 1).raycastTarget = false;
            LobbyKit.Rect(column, SpaceName).gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            foreach (string name in SocialRail.Buttons(Services)) buttons.Add(RailButton(name));
        }

        void BuildServer(RectTransform topRow)
        {
            server = LobbyKit.Button(topRow, SocialRail.Server, Color.clear, () => Toggle(SocialRail.Server), LobbyKit.TabRadius);
            var content = LobbyKit.Row(server.Body(), "Content", 8).Fill();
            var row = content.GetComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childForceExpandHeight = false;
            row.padding = new RectOffset(14, 14, 0, 0);
            dot = LobbyKit.Disc(content, ServerDotName, DotSize, LobbyKit.Line);
            dot.Size(DotSize, DotSize);
            serverText = LobbyKit.Text(content, ServiceText.NotChecked, ServerText, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft);
            serverText.name = ServerTextName;
            serverText.enableAutoSizing = true; serverText.fontSizeMin = ServerTextMinimum; serverText.fontSizeMax = ServerText;
            serverText.Size(-1, ServerText + 12f);
            serverHint = server.gameObject.AddComponent<LobbyHint>();
            serverHint.Text = ServiceText.NotChecked;
            server.Size(ServerWidth, ServerHeight);
        }

        void BuildCount()
        {
            party = LobbyKit.Button(column, SocialRail.Party, Color.clear, () => Toggle(SocialRail.Party), CellRadius);
            party.Size(-1, SocialRail.CountHeight);
            var content = LobbyKit.Row(party.Body(), "Content", 4).Fill();
            var row = content.GetComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childForceExpandHeight = false;
            LobbyKit.Icon(content, LobbyIcons.Party, LobbyKit.Navy).Size(CountIcon, CountIcon);
            count = LobbyKit.Display(content, SocialRail.CountText(1), CountText, LobbyKit.Navy);
            count.name = CountName;
            count.enableAutoSizing = true; count.fontSizeMin = CountTextMinimum; count.fontSizeMax = CountText;
            count.Size(-1, CountText + 8f);
            party.gameObject.AddComponent<LobbyHint>().Text = "Party";
        }

        Button RailButton(string name)
        {
            string icon = name == SocialRail.Recent ? LobbyIcons.Undo : name == SocialRail.AddFriend ? LobbyIcons.Plus : LobbyIcons.Eye;
            string hint = name == SocialRail.Recent ? "Recent players" : name;
            var button = LobbyKit.Button(column, name, LobbyKit.Card, () => Toggle(name), PortraitRadius, LobbyKit.Line, SlotEdge);
            button.Size(-1, SocialRail.Width - 2f * SocialRail.Pad);
            var glyph = LobbyKit.Icon(button.Body(), icon, LobbyKit.Navy);
            var label = LobbyKit.Text(button.Body(), name, LabelText, LobbyKit.Navy, TextAlignmentOptions.Center);
            label.name = ButtonLabelName;
            label.overflowMode = TextOverflowModes.Overflow;
            float labelHeight = LabelHeight(label);
            label.rectTransform.Place(Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, LabelBottom), new Vector2(0f, LabelBottom + labelHeight));
            glyph.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(GlyphSide, LabelBottom + labelHeight + GlyphGap), new Vector2(-GlyphSide, -GlyphTop));
            button.GetComponent<LobbyPress>().Hot = hot => glyph.color = label.color = hot ? LobbyKit.Cyan : LobbyKit.Navy;
            button.gameObject.AddComponent<LobbyHint>().Text = hint;
            return button;
        }

        public static float LabelHeight(TMP_Text label) => Mathf.Ceil(label.GetPreferredValues(label.text).y) + LabelSlack;

        public bool Arrange(float canvasWidth, float canvasHeight, float barHeight)
        {
            bool touch = Application.isMobilePlatform || Touchscreen.current != null;
            var next = SocialRail.Layout(canvasWidth, canvasHeight, barHeight, Screen.height, touch);
            if (laidOut && Same(next, layout)) return false;
            bool resized = !laidOut || next.Width != layout.Width;
            layout = next;
            laidOut = true;
            float right = layout.Right, left = right + layout.Width;
            rail.Place(new Vector2(1f, 0f), Vector2.one, new Vector2(-left, layout.Bottom), new Vector2(-right, -layout.Top));
            float panelRight = left + SocialRail.PanelGap;
            panel.Place(new Vector2(1f, 0f), Vector2.one, new Vector2(-(panelRight + layout.PanelWidth), layout.Bottom), new Vector2(-panelRight, -layout.Top));
            if (resized)
            {
                foreach (var button in buttons) button.Size(-1, layout.Portrait);
                slotSignature = null;
                Refresh(true);
            }
            return true;
        }

        static bool Same(RailLayout a, RailLayout b) =>
            a.Width == b.Width && a.Docked == b.Docked && Mathf.Approximately(a.Top, b.Top) && Mathf.Approximately(a.Bottom, b.Bottom);

        public void Tick()
        {
            bool locked = menu.Starting;
            if (group.interactable == locked) { group.interactable = !locked; group.blocksRaycasts = !locked; }
            if (locked && Expanded) Close(false);
            var call = LobbyServiceCheck.Call;
            if (call != null && check.Due) _ = check.Run(call, lifetime.Token);
            Refresh(false);
        }

        public void Refresh(bool force)
        {
            if (!rail || (!force && Time.unscaledTime < nextRefresh)) return;
            nextRefresh = Time.unscaledTime + RefreshEvery;
            var lobbyParty = WordboundLobbyParty.Current;
            Online = lobbyParty != null && lobbyParty.Active;
            var members = Online ? lobbyParty.Members : null;
            Count = SocialRail.PartyCount(menu.PartySize, Online, members?.Count ?? 0);
            Plates = SocialRail.Portraits(menu.Career != null ? menu.Career.Name : null, menu.PartySize - 1, Online, members);
            string plates = PartyPlates.Signature(Plates) + "|" + SocialRail.InviteSlot(Count) + "|" + (laidOut ? layout.Width : 0f);
            if (force || plates != slotSignature)
            {
                slotSignature = plates;
                BuildSlots();
            }
            string next = Count + "|" + Online + "|" + check.State + "|" + check.Milliseconds + "|" + (lobbyParty?.Searching ?? false);
            if (force || next != railSignature)
            {
                railSignature = next;
                ShowState(lobbyParty);
            }
            if (Expanded) RefreshPanel(force);
        }

        void ShowState(IWordboundLobbyParty lobbyParty)
        {
            Set(count, SocialRail.CountText(Count));
            string pill = ServiceText.Pill(check.State, check.Milliseconds, Region);
            Set(serverText, pill);
            serverHint.Text = ServerLine(lobbyParty);
            var shown = ServiceText.Dot(check.State);
            dot.enabled = shown != ServiceDot.None;
            dot.color = DotColour(shown);
        }

        static string Region => QuickPlayRegions.Nearest(QuickPlayRegions.LocalOffsetMinutes());

        string ServerLine(IWordboundLobbyParty lobbyParty) =>
            ServiceText.Line(check.State, check.Milliseconds, Region, Online, Online && lobbyParty.Searching);

        static Color DotColour(ServiceDot shown) =>
            shown == ServiceDot.Good ? LobbyKit.Lime : shown == ServiceDot.Bad ? LobbyKit.Hot : LobbyKit.Line;

        void BuildSlots()
        {
            var events = EventSystem.current;
            var selected = events ? events.currentSelectedGameObject : null;
            string kept = selected && selected.transform.IsChildOf(slots) ? selected.name : null;
            portraits.Clear();
            invite = null;
            LobbyKit.Clear(slots);
            for (int i = 0; i < Plates.Count; i++) portraits.Add(Portrait(Plates[i], i + 1));
            if (SocialRail.InviteSlot(Count)) invite = InviteSlot();
            Wire();
            if (kept == null || !events) return;
            var again = portraits.Concat(invite ? new[] { invite } : new Button[0]).FirstOrDefault(button => button.name == kept);
            events.SetSelectedGameObject((again ? again : party).gameObject);
        }

        Button Portrait(PartyPlate plate, int number)
        {
            float size = laidOut ? layout.Portrait : SocialRail.Width - 2f * SocialRail.Pad;
            var button = LobbyKit.Button(slots, SocialRail.PortraitPrefix + number, SeatColour(plate.Place), () => Toggle(SocialRail.Party),
                PortraitRadius, LobbyKit.Navy, PortraitEdge);
            button.Size(-1, size);
            var initial = LobbyKit.Display(button.Body(), plate.Initial, InitialMaximum, LobbyKit.ArtInk);
            initial.name = InitialName;
            initial.enableAutoSizing = true; initial.fontSizeMin = InitialMinimum; initial.fontSizeMax = InitialMaximum;
            initial.rectTransform.Fill();
            button.gameObject.AddComponent<LobbyHint>().Text = plate.Tag.Length > 0 ? plate.Name + MapCardText.Separator + plate.Tag : plate.Name;
            return button;
        }

        Button InviteSlot()
        {
            float size = laidOut ? layout.Portrait : SocialRail.Width - 2f * SocialRail.Pad;
            var button = LobbyKit.Button(slots, SocialRail.Invite, LobbyKit.Raised, () => Toggle(SocialRail.Invite), PortraitRadius, LobbyKit.Line, SlotEdge);
            button.Size(-1, size);
            var plus = LobbyKit.Icon(button.Body(), LobbyIcons.Plus, LobbyKit.Cyan);
            plus.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(PlusInset, PlusInset), new Vector2(-PlusInset, -PlusInset));
            button.gameObject.AddComponent<LobbyHint>().Text = OnlineBuild ? InviteHint : JoinHint;
            return button;
        }

        public const string InviteHint = "Invite friends, or press Start on a controller to join here";
        public const string JoinHint = "Press Start on a controller to join here";

        static bool OnlineBuild => Session.CanLoad(Session.WordboundScene);

        static Color SeatColour(int place) => GameAssets.I ? GameAssets.I.PlayerColor(place) : LobbyKit.Lime;

        IReadOnlyList<Selectable> RailOrder()
        {
            var order = new List<Selectable> { server, party };
            order.AddRange(portraits);
            if (invite) order.Add(invite);
            order.AddRange(buttons);
            return order;
        }

        public IReadOnlyList<string> FocusNames => RailOrder().Select(selectable => selectable.name).ToArray();

        void Wire()
        {
            var order = RailOrder();
            var side = Expanded ? PanelFirst : ReturnSelectable;
            for (int i = 0; i < order.Count; i++)
                order[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = i > 0 ? order[i - 1] : null,
                    selectOnDown = i < order.Count - 1 ? order[i + 1] : null,
                    selectOnLeft = order[i] == server ? Beside(server) : side,
                };
            if (Expanded) WirePanel();
        }

        static Selectable Beside(Selectable item)
        {
            var parent = item.transform.parent;
            Selectable before = null;
            for (int i = 0; parent && i < item.transform.GetSiblingIndex(); i++)
                if (parent.GetChild(i).TryGetComponent(out Selectable each) && each.isActiveAndEnabled) before = each;
            return before;
        }

        Selectable ReturnSelectable => returnTo && returnTo.activeInHierarchy && !Owns(returnTo) && returnTo.TryGetComponent(out Selectable back)
            ? back : menu.PageFirst;

        bool Owns(GameObject item) => item && (item.transform.IsChildOf(rail) || (panel && item.transform.IsChildOf(panel)) ||
            (server && item.transform.IsChildOf(server.transform)));

        public void Jump()
        {
            var events = EventSystem.current;
            if (!events || menu.Starting) return;
            if (Focused) { Return(); return; }
            returnTo = events.currentSelectedGameObject;
            Wire();
            events.SetSelectedGameObject(party.gameObject);
        }

        public void Return()
        {
            if (Expanded) Close(false);
            var target = ReturnSelectable;
            returnTo = null;
            Wire();
            if (EventSystem.current && target && target.isActiveAndEnabled) EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        public bool Back()
        {
            if (Expanded) { Close(true); return true; }
            if (!Focused) return false;
            Return();
            return true;
        }

        Selectable Opener(string from) => from switch
        {
            SocialRail.Server => server,
            SocialRail.Invite => invite ? invite : party,
            _ => buttons.FirstOrDefault(button => button.name == from) ?? (Selectable)party,
        };

        static void Set(TMP_Text text, string value)
        {
            if (text && text.text != value) text.text = value;
        }
    }
}
