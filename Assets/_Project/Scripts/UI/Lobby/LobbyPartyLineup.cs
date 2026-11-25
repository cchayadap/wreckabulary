using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    [DefaultExecutionOrder(ExecutionOrder)]
    public sealed class LobbyPartyLineup : MonoBehaviour
    {
        public const int ExecutionOrder = 50;
        public const string RootName = "Party line-up", LayerName = "Plates", PlatePrefix = "Party plate ", OpenName = "Add player";
        public const string ChipName = "Plate chip", NameName = "Plate name", TagName = "Plate tag", FrameName = "Edge";
        public const string KeysName = "Join keys", KeyPrefix = "Join key ";
        public const string PartyPanel = "Safe area/" + LobbySocialRail.HostName + "/" + LobbySocialRail.PanelName;
        public const float NameText = 22f, NameTextMinimum = 15f, TagText = 15f, OpenText = 18f, ChipText = 18f, ChipTextMinimum = 15f;
        public const float KeyText = 15f, KeyHeight = 24f;
        public const float Margin = 12f;
        const float PlateHeight = 52f, TaggedHeight = 72f, Pad = 10f, Chip = 36f, Gap = 10f;
        const float NameWidthMinimum = 64f, NameWidthMaximum = 210f, TextSlack = 4f;
        const float TagHeight = 22f, TagInset = 8f, FootDrop = 18f, ScreenMargin = 12f, PlateShadow = 3f, GrainStrength = .14f;
        const float OpenAlpha = .88f, PlusInset = 8f, KeyPad = 8f, KeyMinimum = 28f, KeyGap = 6f;
        const float SyncEvery = .25f, Moved = .05f;
        const int PlateRadius = 14, ChipRadius = 10, FrameWidth = 3, ChipEdge = 2, OpenEdge = 2, KeyRadius = 6, KeyEdge = 1;
        static readonly Vector2 Centre = new Vector2(.5f, .5f), TopCentre = new Vector2(.5f, 1f), LeftMiddle = new Vector2(0f, .5f);

        sealed class View
        {
            public PartyPlate Plate;
            public RectTransform Rect;
            public Vector2 Size;
        }

        sealed class Spare
        {
            public Button Button;
            public RectTransform Rect;
            public Vector2 Size;
        }

        readonly List<View> views = new List<View>();
        readonly List<Spare> spares = new List<Spare>();
        readonly List<PlateBox> placed = new List<PlateBox>();
        readonly Vector3[] corners = new Vector3[4];
        IReadOnlyList<PartyPlate> lineup = new PartyPlate[0];
        LobbyMenu menu;
        RectTransform layer, dock;
        string signature;
        float nextSync;

        public IReadOnlyList<PartyPlate> Plates => lineup;
        public RectTransform Layer => layer;
        public Button Open => spares.Count > 0 ? spares[0].Button : null;
        public bool Showing => layer && layer.gameObject.activeSelf;

        public IReadOnlyList<Button> Opens
        {
            get
            {
                var buttons = new List<Button>();
                foreach (var spare in spares) buttons.Add(spare.Button);
                return buttons;
            }
        }

        public RectTransform PlateAt(int place)
        {
            foreach (var view in views)
                if (view.Plate.Place == place) return view.Rect;
            return null;
        }

        public static LobbyPartyLineup Show(LobbyMenu menu, RectTransform page, RectTransform dock)
        {
            var pages = page ? page.parent : null;
            var safe = pages ? pages.parent : null;
            if (!menu || !safe) return null;
            var existing = safe.Find(RootName);
            var lineup = existing ? existing.GetComponent<LobbyPartyLineup>() : Create(safe, pages);
            if (!lineup) return null;
            lineup.menu = menu;
            lineup.dock = dock;
            lineup.signature = null;
            lineup.Sync();
            lineup.Arrange();
            return lineup;
        }

        static LobbyPartyLineup Create(Transform safe, Transform pages)
        {
            var host = LobbyKit.Rect(safe, RootName).Fill();
            host.SetSiblingIndex(pages.GetSiblingIndex() + 1);
            var lineup = host.gameObject.AddComponent<LobbyPartyLineup>();
            lineup.layer = LobbyKit.Rect(host, LayerName).Fill();
            return lineup;
        }

        void Update()
        {
            if (menu && menu.Current == LobbyMenu.Home && Time.unscaledTime >= nextSync) Sync();
        }

        void LateUpdate() => Arrange();

        void Sync()
        {
            nextSync = Time.unscaledTime + SyncEvery;
            var party = WordboundLobbyParty.Current;
            var friends = party != null && party.Active ? party.Members : null;
            lineup = PartyPlates.Lineup(menu.Career != null ? menu.Career.Name : null, menu.PartySize - 1, friends);
            var stage = menu.Stage;
            if (stage && stage.Camera) stage.SetPartyFriends(PartyPlates.Friends(lineup));
            string next = PartyPlates.Signature(lineup);
            if (next == signature) return;
            if (!layer.gameObject.activeInHierarchy) { signature = null; return; }
            signature = next;
            Rebuild();
        }

        void Rebuild()
        {
            int selected = spares.FindIndex(spare => spare.Button && Selected(spare.Button.gameObject));
            foreach (var view in views) Discard(view.Rect.gameObject);
            views.Clear();
            foreach (var spare in spares)
                if (spare.Button) Discard(spare.Button.gameObject);
            spares.Clear();
            foreach (var plate in lineup)
            {
                if (plate.Stands) views.Add(Build(plate));
                else spares.Add(BuildOpen());
            }
            if (selected < 0) return;
            if (spares.Count > 0 && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(spares[Mathf.Min(selected, spares.Count - 1)].Button.gameObject);
            else SelectHome();
        }

        View Build(PartyPlate plate)
        {
            Color seat = SeatColour(plate.Place), paper = LobbyKit.Panel;
            var rect = LobbyKit.Rect(layer, PlatePrefix + (plate.Place + 1));
            rect.anchorMin = rect.anchorMax = Centre; rect.pivot = TopCentre;
            LobbyKit.Face(rect, paper, PlateRadius, seat, FrameWidth, PlateShadow);
            LobbyParcel.Grain(rect, GrainStrength);
            var chip = LobbyKit.Rect(rect, ChipName).Pin(LeftMiddle, new Vector2(Pad, 0f), new Vector2(Chip, Chip));
            LobbyKit.Face(chip, seat, ChipRadius, LobbyKit.Navy, ChipEdge);
            var initial = LobbyKit.Display(chip, plate.Initial, ChipText, LobbyKit.ArtInk);
            initial.enableAutoSizing = true; initial.fontSizeMin = ChipTextMinimum; initial.fontSizeMax = ChipText;
            initial.rectTransform.Fill();
            var ink = PartyPlates.Ink(new Srgb(seat.r, seat.g, seat.b), new Srgb(paper.r, paper.g, paper.b));
            var name = LobbyKit.Display(rect, plate.Name, NameText, new Color(ink.R, ink.G, ink.B), TextAlignmentOptions.MidlineLeft);
            name.name = NameName;
            float width = name.GetPreferredValues(plate.Name).x;
            name.enableAutoSizing = true; name.fontSizeMin = NameTextMinimum; name.fontSizeMax = NameText;
            bool tagged = plate.Tag.Length > 0;
            float left = Pad + Chip + Gap;
            if (tagged)
            {
                var tag = LobbyKit.Caps(rect, plate.Tag, TagText, TextAlignmentOptions.MidlineLeft);
                tag.name = TagName;
                width = Mathf.Max(width, tag.GetPreferredValues(tag.text).x);
                tag.rectTransform.Place(Vector2.zero, Vector2.right, new Vector2(left, TagInset), new Vector2(-Pad, TagInset + TagHeight));
            }
            width = Mathf.Clamp(Mathf.Ceil(width) + TextSlack, NameWidthMinimum, NameWidthMaximum);
            name.rectTransform.Place(Vector2.zero, Vector2.one,
                new Vector2(left, tagged ? TagInset + TagHeight : 0f), new Vector2(-Pad, tagged ? -TagInset : 0f));
            var size = new Vector2(left + width + Pad, tagged ? TaggedHeight : PlateHeight);
            rect.sizeDelta = size;
            return new View { Plate = plate, Rect = rect, Size = size };
        }

        Spare BuildOpen()
        {
            var fill = LobbyKit.Card;
            fill.a = OpenAlpha;
            var button = LobbyKit.Button(layer, OpenName, fill, OpenParty, PlateRadius, LobbyKit.Line, OpenEdge);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = Centre; rect.pivot = TopCentre;
            var body = button.Body();
            var chip = LobbyKit.Rect(body, ChipName).Pin(LeftMiddle, new Vector2(Pad, 0f), new Vector2(Chip, Chip));
            LobbyKit.Face(chip, LobbyKit.Raised, ChipRadius, LobbyKit.Line, ChipEdge);
            var plus = LobbyKit.Icon(chip, LobbyIcons.Plus, LobbyKit.Cyan);
            plus.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(PlusInset, PlusInset), new Vector2(-PlusInset, -PlusInset));
            float left = Pad + Chip + Gap;
            var label = LobbyKit.Display(body, PartyPlates.OpenLabel, NameText, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
            label.name = NameName;
            float width = label.GetPreferredValues(PartyPlates.OpenLabel).x;
            label.enableAutoSizing = true; label.fontSizeMin = NameTextMinimum; label.fontSizeMax = NameText;
            width = Mathf.Max(width, JoinKeys(body, left));
            width = Mathf.Clamp(Mathf.Ceil(width) + TextSlack, NameWidthMinimum, NameWidthMaximum);
            label.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(left, TagInset + KeyHeight), new Vector2(-Pad, -TagInset));
            button.GetComponent<LobbyPress>().Hot = hot => label.color = hot ? LobbyKit.Cyan : LobbyKit.Muted;
            button.gameObject.AddComponent<LobbyHint>().Text = PartyPlates.OpenHint;
            var size = new Vector2(left + width + Pad, TaggedHeight);
            rect.sizeDelta = size;
            return new Spare { Button = button, Rect = rect, Size = size };
        }

        static float JoinKeys(RectTransform body, float left)
        {
            var row = LobbyKit.Rect(body, KeysName).Place(Vector2.zero, Vector2.right, new Vector2(left, TagInset), new Vector2(-Pad, TagInset + KeyHeight));
            float x = 0f;
            var keys = PartyPlates.JoinKeys(!Application.isMobilePlatform);
            for (int i = 0; i < keys.Count; i++)
            {
                if (i > 0) x += Word(row, PartyPlates.JoinKeyJoiner, x) + KeyGap;
                x += Key(row, keys[i], x) + KeyGap;
            }
            return Mathf.Max(0f, x - KeyGap);
        }

        static float Key(RectTransform row, string key, float x)
        {
            var cap = LobbyKit.Rect(row, KeyPrefix + key);
            LobbyKit.Face(cap, LobbyKit.Raised, KeyRadius, LobbyKit.Line, KeyEdge);
            var text = LobbyKit.Display(cap, key, KeyText, LobbyKit.Navy);
            float width = Mathf.Max(KeyMinimum, Mathf.Ceil(text.GetPreferredValues(key).x) + 2f * KeyPad);
            text.rectTransform.Fill();
            cap.Place(Vector2.zero, Vector2.up, new Vector2(x, 0f), new Vector2(x + width, 0f));
            return width;
        }

        static float Word(RectTransform row, string word, float x)
        {
            var text = LobbyKit.Text(row, word, KeyText, LobbyKit.Muted, TextAlignmentOptions.Center);
            float width = Mathf.Ceil(text.GetPreferredValues(word).x) + TextSlack;
            text.rectTransform.Place(Vector2.zero, Vector2.up, new Vector2(x, 0f), new Vector2(x + width, 0f));
            return width;
        }

        void OpenParty()
        {
            if (!menu || menu.Starting) return;
            var panel = menu.transform.Find(PartyPanel);
            if (panel && panel.gameObject.activeSelf) return;
            menu.ToggleParty();
        }

        void Arrange()
        {
            if (!layer) return;
            var stage = menu ? menu.Stage : null;
            bool show = stage && menu.Current == LobbyMenu.Home && stage.PartyShown && stage.Camera;
            if (layer.gameObject.activeSelf != show) layer.gameObject.SetActive(show);
            if (!show) return;
            if (signature == null) Sync();
            var area = layer.rect;
            var bounds = new PlateBox(area.xMin + ScreenMargin, area.yMin + ScreenMargin, area.xMax - ScreenMargin - menu.RailEdge, area.yMax - ScreenMargin);
            PlateBox? blocked = null;
            if (dock && dock.gameObject.activeInHierarchy) blocked = LocalBox(dock);
            placed.Clear();
            foreach (var view in views)
            {
                bool standing = stage.PartyFoot(view.Plate.Place, out var foot);
                standing = Project(stage.Camera, foot, out var under) && standing;
                if (view.Rect.gameObject.activeSelf != standing) view.Rect.gameObject.SetActive(standing);
                if (!standing) continue;
                var box = PartyPlates.Settle(PlateBox.Under(under.x, under.y - FootDrop, view.Size.x, view.Size.y), bounds, blocked, Margin);
                Put(view.Rect, box);
                placed.Add(box);
            }
            ArrangeOpen(stage, bounds, blocked);
        }

        void ArrangeOpen(LobbyStage stage, PlateBox bounds, PlateBox? blocked)
        {
            var offsets = PartyPlates.OpenOffsets(stage.PresentedPartySize);
            for (int i = 0; i < spares.Count; i++)
            {
                var spare = spares[i];
                bool offer = !menu.Starting && i < offsets.Length;
                if (offer && Project(stage.Camera, stage.PartyPoint(offsets[i]), out var under))
                {
                    var box = PartyPlates.Settle(PlateBox.Under(under.x, under.y - FootDrop, spare.Size.x, spare.Size.y), bounds, blocked, Margin);
                    offer = Clear(box.Grown(Gap));
                    if (offer)
                    {
                        Put(spare.Rect, box);
                        placed.Add(box);
                    }
                }
                else offer = false;
                Offer(spare, offer);
            }
        }

        bool Clear(PlateBox reach)
        {
            foreach (var other in placed)
                if (reach.Overlaps(other)) return false;
            return true;
        }

        void Offer(Spare spare, bool offer)
        {
            if (spare.Button.gameObject.activeSelf == offer) return;
            bool selected = !offer && Selected(spare.Button.gameObject);
            spare.Button.gameObject.SetActive(offer);
            if (selected) SelectHome();
        }

        bool Project(Camera camera, Vector3 world, out Vector2 local)
        {
            var screen = camera.WorldToScreenPoint(world);
            local = default;
            return screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, null, out local);
        }

        PlateBox LocalBox(RectTransform other)
        {
            other.GetWorldCorners(corners);
            Vector2 low = layer.InverseTransformPoint(corners[0]), high = layer.InverseTransformPoint(corners[2]);
            return new PlateBox(low.x, low.y, high.x, high.y);
        }

        void Put(RectTransform rect, PlateBox box)
        {
            var middle = layer.rect.center;
            var at = new Vector2(box.CentreX - middle.x, box.Top - middle.y);
            if ((rect.anchoredPosition - at).sqrMagnitude > Moved * Moved) rect.anchoredPosition = at;
        }

        void SelectHome()
        {
            var first = menu ? menu.Page<HomePage>()?.First : null;
            if (first && EventSystem.current) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        static Color SeatColour(int place) => GameAssets.I ? GameAssets.I.PlayerColor(place) : LobbyKit.Lime;

        static bool Selected(GameObject item) => EventSystem.current && EventSystem.current.currentSelectedGameObject == item;

        static void Discard(GameObject item)
        {
            item.SetActive(false);
            Destroy(item);
        }
    }
}
