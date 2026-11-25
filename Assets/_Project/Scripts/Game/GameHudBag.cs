using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public partial class GameHud
    {
        public const float BookTile = 24f, BookTileStep = 28f, BookTilePad = 1f;
        public const float BagMapFloor = 325f, BagInventoryGap = BagGap, BagInventoryCellWidth = BagColumn;
        public const float BagLetterMargin = 40f, BagLetterWide = 55f, BagLetterTall = 60f, BagLetterStepX = 62.5f, BagLetterStepY = 67.5f;
        public const float BagLetterGridWide = (TilesPerRow - 1) * BagLetterStepX + BagLetterWide;
        public const float BagLetterGridTall = (TrayTiles / TilesPerRow - 1) * BagLetterStepY + BagLetterTall;
        const float BagLetterCountGap = 6f, BagLetterFoot = 27.5f, BagHandTall = 145f;
        const float BookCardGap = 12f, BookPadding = 8f, BookColumnStep = 200f;
        const int BookComfortColumns = 3;
        const float DropGrow = .04f, DropPulse = .03f, DropPulseSeconds = .3f, DropEaseSpeed = .5f, DragGhostAlpha = .9f, DragSourceAlpha = .35f, FocusRingOut = 3f;
        const float GhostItem = 78f;
        const int TrashTexture = 192, TrashSamples = 4, DropSides = 2, RightSide = 1;
        static readonly Color DropFace = Hex(0xffffff, .15f), DropFaceLit = Hex(0xffe7b8, .17f), DropRim = Hex(0xfff4e2, .55f);

        readonly List<char> bagOrder = new(), bagSpare = new();
        bool bagArranged;
        LetterInventory bagOrderFor;
        readonly RectTransform[] dropColumns = new RectTransform[DropSides];
        readonly Image[] dropFaces = new Image[DropSides], dropEdges = new Image[DropSides], dropIcons = new Image[DropSides];
        RectTransform bagClose, dragGhost;
        LetterCell ghostCell;
        RawImage ghostIcon;
        BagDragSource dragging, hovered;
        bool dragShown;
        char dragLetter;
        BagSpot dragSpot;
        int dragTo = -1;
        float dropColumnWidth = BagDrag.ColumnMax, bookComfort;
        Sprite trashSprite;

        public bool BagDragging => dragging && dragShown;
        public IReadOnlyList<char> BagLetterOrder => bagOrder;
        public int LongestRecipe { get; private set; }

        int BookColumnsFor(float bookWidth) => Mathf.Min(Mathf.FloorToInt((bookWidth - BookPadding + BookCardGap) / BookColumnStep),
            Mathf.FloorToInt((bookWidth - BookPadding + BookCardGap) / (BookCardFloor(LongestRecipe) + BookCardGap)));

        public static float BookCardFloor(int letters) => letters * BookTileStep - (BookTileStep - BookTile) + 2f * BookTilePad;

        sealed class BagDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
            IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
        {
            public GameHud hud;
            public BagThing thing;
            public int index;
            public CanvasGroup group;

            public void OnBeginDrag(PointerEventData data) { if (hud) hud.BeginBagDrag(this, data); }
            public void OnDrag(PointerEventData data) { if (hud) hud.MoveBagDrag(this, data); }
            public void OnEndDrag(PointerEventData data) { if (hud) hud.EndBagDrag(this, data); }
            public void OnPointerEnter(PointerEventData _) { if (hud) hud.HoverBag(this, true); }
            public void OnPointerExit(PointerEventData _) { if (hud) hud.HoverBag(this, false); }
            public void OnSelect(BaseEventData _) { if (hud) hud.SelectBag(this, true); }
            public void OnDeselect(BaseEventData _) { if (hud) hud.SelectBag(this, false); }
        }

        sealed class DropColumnPulse : MonoBehaviour
        {
            public GameHud hud;
            float pulseAt = float.NegativeInfinity, grow;
            bool was;

            void Update()
            {
                bool on = hud && hud.BagDragging;
                if (on && !was) pulseAt = Time.unscaledTime;
                was = on;
                grow = Mathf.MoveTowards(grow, on ? DropGrow : 0f, Time.unscaledDeltaTime * DropEaseSpeed);
                float pulse = on ? DropPulse * Mathf.Clamp01(1f - (Time.unscaledTime - pulseAt) / DropPulseSeconds) : 0f;
                transform.localScale = Vector3.one * (1f + grow + pulse);
            }
        }

        void BuildBagDrag(RectTransform panel)
        {
            foreach (var card in bookCards) LongestRecipe = Mathf.Max(LongestRecipe, card.word.Length);
            bookComfort = Mathf.Max(BookComfortColumns * BookColumnStep - BookCardGap + BookPadding,
                BookComfortColumns * BookCardFloor(LongestRecipe) + (BookComfortColumns - 1) * BookCardGap + BookPadding);
            for (int i = 0; i < bagCells.Length; i++)
            {
                var cell = bagCells[i];
                var holder = (RectTransform)cell.face.transform.parent;
                cell.face.raycastTarget = true;
                var select = holder.gameObject.AddComponent<Selectable>();
                select.transition = Selectable.Transition.None;
                select.targetGraphic = cell.face;
                cell.focus = BagFocusRing(holder, hairRing, cell.face.pixelsPerUnitMultiplier);
                AddBagSource(holder, BagThing.Letter, i);
            }
            for (int i = 0; i < bagHands.Length; i++)
            {
                var view = bagHands[i];
                var hand = (RectTransform)view.face.transform;
                view.focus = BagFocusRing(hand, view.edge.sprite, view.face.pixelsPerUnitMultiplier);
                AddBagSource(hand, BagThing.Item, i);
            }
            BuildDropColumns(panel);
            BuildDragGhost(panel);
        }

        void PlaceBagLetters(float scale)
        {
            for (int i = 0; i < bagCells.Length; i++)
            {
                var holder = (RectTransform)bagCells[i].face.transform.parent;
                holder.anchoredPosition = new Vector2(BagLetterMargin + i % TilesPerRow * BagLetterStepX * scale, -(i / TilesPerRow) * BagLetterStepY * scale);
                holder.localScale = Vector3.one * scale;
            }
            bagPanelCount.rectTransform.anchoredPosition = new Vector2(0f, -(BagLetterGridTall * scale + BagLetterCountGap));
            if (ghostCell != null) ghostCell.face.transform.parent.localScale = Vector3.one * scale;
        }

        void AddBagSource(RectTransform holder, BagThing thing, int index)
        {
            var group = holder.gameObject.AddComponent<CanvasGroup>();
            var source = holder.gameObject.AddComponent<BagDragSource>();
            source.hud = this; source.thing = thing; source.index = index; source.group = group;
        }

        Image BagFocusRing(RectTransform holder, Sprite sprite, float pixels)
        {
            var ring = CreateImage("Focus", holder, Vector2.zero, Vector2.zero, Vector2.zero, Gold);
            Stretch(ring.rectTransform);
            ring.rectTransform.offsetMin = -Vector2.one * FocusRingOut; ring.rectTransform.offsetMax = Vector2.one * FocusRingOut;
            ring.sprite = sprite; ring.type = Image.Type.Sliced; ring.pixelsPerUnitMultiplier = pixels;
            ring.enabled = false;
            return ring;
        }

        void BuildDropColumns(RectTransform panel)
        {
            for (int side = 0; side < DropSides; side++)
            {
                float x = side == RightSide ? 1f : 0f;
                var column = dropColumns[side] = Panel(side == RightSide ? "Drop column right" : "Drop column left", panel,
                    new Vector2(x, .5f), Vector2.zero, Vector2.zero, new Vector2(x, .5f), DropFace);
                column.anchorMin = new Vector2(x, 0f); column.anchorMax = new Vector2(x, 1f);
                dropFaces[side] = column.GetComponent<Image>();
                dropEdges[side] = Edge(column, DropRim, 15f);
                dropEdges[side].sprite = ringSprite;
                var icon = dropIcons[side] = CreateImage("Trash", column, new Vector2(.5f, .5f), Vector2.zero, Vector2.one * BagDrag.IconMax, BagInk);
                icon.sprite = TrashSprite(); icon.preserveAspect = true;
                column.gameObject.AddComponent<DropColumnPulse>().hud = this;
            }
            PlaceDropColumns(BagDrag.ColumnMax);
        }

        void BuildDragGhost(RectTransform panel)
        {
            dragGhost = Rect("Drag ghost", panel, new Vector2(.5f, .5f), Vector2.zero, Vector2.one * GhostItem);
            var fade = dragGhost.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = DragGhostAlpha; fade.blocksRaycasts = false; fade.interactable = false;
            ghostCell = MakeCell("Letter", dragGhost, Vector2.zero, new Vector2(BagLetterWide, BagLetterTall), 33f);
            var holder = (RectTransform)ghostCell.face.transform.parent;
            holder.anchorMin = holder.anchorMax = holder.pivot = new Vector2(.5f, .5f);
            holder.anchoredPosition = Vector2.zero;
            ghostIcon = Rect("Item", dragGhost, new Vector2(.5f, .5f), Vector2.zero, Vector2.one * GhostItem).gameObject.AddComponent<RawImage>();
            ghostIcon.raycastTarget = false;
            dragGhost.gameObject.SetActive(false);
        }

        float PlaceDropColumns(float width)
        {
            dropColumnWidth = BagDrag.ColumnWidth(width);
            for (int side = 0; side < DropSides; side++)
            {
                var column = dropColumns[side];
                if (!column) continue;
                if (side == RightSide)
                {
                    column.offsetMin = new Vector2(-BagDrag.ColumnEdge - dropColumnWidth, BagBottom);
                    column.offsetMax = new Vector2(-BagDrag.ColumnEdge, -BagTop);
                }
                else
                {
                    column.offsetMin = new Vector2(BagDrag.ColumnEdge, BagBottom);
                    column.offsetMax = new Vector2(BagDrag.ColumnEdge + dropColumnWidth, -BagTop);
                }
                dropIcons[side].rectTransform.sizeDelta = Vector2.one * BagDrag.IconSize(dropColumnWidth);
            }
            return BagDrag.ColumnReserve(dropColumnWidth);
        }

        Sprite TrashSprite()
        {
            if (trashSprite) return trashSprite;
            var texture = new Texture2D(TrashTexture, TrashTexture, TextureFormat.RGBA32, true)
            { name = "HUD trash", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[TrashTexture * TrashTexture];
            for (int y = 0; y < TrashTexture; y++)
                for (int x = 0; x < TrashTexture; x++)
                    pixels[y * TrashTexture + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(TrashGlyph.Coverage(x, y, TrashTexture, TrashSamples) * 255f));
            texture.SetPixels32(pixels); texture.Apply(true, true);
            trashSprite = Sprite.Create(texture, new Rect(0, 0, TrashTexture, TrashTexture), new Vector2(.5f, .5f), 100f);
            ownedSprites.Add(trashSprite); ownedTextures.Add(texture);
            return trashSprite;
        }

        void BeginBagDrag(BagDragSource source, PointerEventData data)
        {
            StopBagDrag();
            if (!BagOpen || !LocalPlayer || !source) return;
            bool hasItem = source.thing == BagThing.Item && LocalPlayer.Combat.GearIn(source.index);
            if (!BagDrag.CanDrag(source.thing, source.index, bagOrder.Count, hasItem)) return;
            dragging = source;
            dragLetter = source.thing == BagThing.Letter ? bagOrder[source.index] : '\0';
            MoveBagDrag(source, data);
        }

        void MoveBagDrag(BagDragSource source, PointerEventData data)
        {
            if (!dragging || dragging != source) return;
            if (!BagOpen) { StopBagDrag(); return; }
            if (!dragShown)
            {
                var moved = data.position - data.pressPosition;
                if (!BagDrag.Started(moved.x, moved.y)) return;
                ShowDragGhost(source);
            }
            dragGhost.position = data.position;
            (dragSpot, dragTo) = BagSpotAt(data.position);
            ShowDragTargets();
        }

        void EndBagDrag(BagDragSource source, PointerEventData data)
        {
            if (!dragging || dragging != source) return;
            bool shown = dragShown;
            var (spot, to) = BagSpotAt(data.position);
            StopBagDrag();
            if (!shown || !BagOpen || !LocalPlayer) return;
            ApplyBagMove(source, BagDrag.Resolve(source.thing, source.index, spot, to, bagOrder.Count));
            RefreshBagContents();
        }

        void ShowDragGhost(BagDragSource source)
        {
            dragShown = true;
            bool letter = source.thing == BagThing.Letter;
            ghostCell.face.transform.parent.gameObject.SetActive(letter);
            ghostIcon.gameObject.SetActive(!letter);
            if (letter) ghostCell.letter.text = One(dragLetter);
            else
            {
                var gear = LocalPlayer.Combat.GearIn(source.index);
                Wreckabulary.UI.ItemArt.Apply(ghostIcon, gear ? gear.word : null);
            }
            if (source.group) source.group.alpha = DragSourceAlpha;
            dragGhost.gameObject.SetActive(true);
            dragGhost.SetAsLastSibling();
        }

        void StopBagDrag()
        {
            if (dragging && dragging.group) dragging.group.alpha = 1f;
            dragging = null;
            dragShown = false;
            dragSpot = BagSpot.None;
            dragTo = -1;
            if (dragGhost) dragGhost.gameObject.SetActive(false);
            ShowDragTargets();
        }

        (BagSpot spot, int index) BagSpotAt(Vector2 screen)
        {
            for (int side = 0; side < DropSides; side++)
            {
                var column = dropColumns[side];
                if (column && column.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(column, screen, null))
                    return (BagSpot.Drop, side);
            }
            for (int i = 0; i < bagCells.Length; i++)
            {
                var holder = (RectTransform)bagCells[i].face.transform.parent;
                if (holder.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(holder, screen, null)) return (BagSpot.Letter, i);
            }
            for (int i = 0; i < bagHands.Length; i++)
            {
                var hand = (RectTransform)bagHands[i].face.transform;
                if (hand.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(hand, screen, null)) return (BagSpot.Item, i);
            }
            return (BagSpot.None, -1);
        }

        void ApplyBagMove(BagDragSource source, BagMove move)
        {
            switch (move.Kind)
            {
                case BagMoveKind.Drop:
                    if (source.thing == BagThing.Letter) { if (SameLetter(source.index)) DropBagLetter(source.index); }
                    else DropBagGear(source.index);
                    break;
                case BagMoveKind.Reorder:
                    if (SameLetter(source.index) && BagDrag.Move(bagOrder, source.index, move.To)) bagArranged = true;
                    break;
                case BagMoveKind.Swap:
                    LocalPlayer.Combat.SwapSlots();
                    break;
            }
        }

        void ArrangeLetters(LetterInventory inv)
        {
            if (inv != bagOrderFor) { bagOrderFor = inv; bagArranged = false; }
            bagArranged = BagDrag.Arrange(bagOrder, inv.Letters, bagArranged, bagSpare);
        }

        bool SameLetter(int index) => index >= 0 && index < bagOrder.Count && bagOrder[index] == dragLetter;

        bool CanBagDrop()
        {
            var player = LocalPlayer;
            return player && Time.timeScale > 0f && player.CanAct && !player.IsDodging && !player.Combat.IsDeploying && !player.Combat.IsReviving
                && !player.Summoner.IsSpelling && !player.Summoner.IsCrafting;
        }

        bool DropBagLetter(int index)
        {
            if (!CanBagDrop() || index < 0 || index >= bagOrder.Count) return false;
            var player = LocalPlayer;
            char letter = BagDrag.Take(bagOrder, index);
            if (player.Inventory.DropLetter(letter, player.transform.position, player.Facing)) return true;
            bagOrder.Insert(index, letter);
            return false;
        }

        bool DropBagGear(int slot)
        {
            if (!CanBagDrop()) return false;
            var combat = LocalPlayer.Combat;
            if (!combat.GearIn(slot)) return false;
            if (slot != combat.ActiveSlot && !combat.SelectSlot(slot)) return false;
            combat.Drop();
            return true;
        }

        void DropFocusedInBag()
        {
            if (!BagOpen || !LocalPlayer || !LocalPlayer.Commands.drop || BagDragging) return;
            var focus = FocusedBagSource();
            if (!focus) return;
            if (focus.thing == BagThing.Item && focus.index == LocalPlayer.Combat.ActiveSlot) return;
            LocalPlayer.Commands.drop = false;
            if (focus.thing == BagThing.Letter) DropBagLetter(focus.index);
            else DropBagGear(focus.index);
            RefreshBagContents();
            ShowBagHint(focus);
        }

        BagDragSource FocusedBagSource()
        {
            if (hovered && hovered.isActiveAndEnabled) return hovered;
            if (!LocalPlayer || LocalPlayer.Binding is not GamepadBinding) return null;
            var events = EventSystem.current;
            var selected = events ? events.currentSelectedGameObject : null;
            return selected && selected.TryGetComponent(out BagDragSource source) && source.hud == this && source.isActiveAndEnabled ? source : null;
        }

        void HoverBag(BagDragSource source, bool on)
        {
            if (on) hovered = source;
            else if (hovered == source) hovered = null;
            if (!BagDragging) ShowBagHint(FocusedBagSource());
            ShowDragTargets();
        }

        void SelectBag(BagDragSource source, bool on)
        {
            var focus = FocusedBagSource();
            if (!on && focus == source && hovered != source) focus = null;
            if (!BagDragging) ShowBagHint(focus);
            ShowDragTargets();
        }

        void ShowBagHint(BagDragSource source)
        {
            if (!bookLine) return;
            string thing = BagThingName(source);
            if (thing == null) { ShowBookLine(null); return; }
            bookWord = null;
            var binding = LocalPlayer ? LocalPlayer.Binding : null;
            bookLine.text = BagDrag.Hint(thing, ControlHints.For(binding, PlayerControl.Drop), ControlHints.Device(binding) == ControlDevice.Touch);
        }

        string BagThingName(BagDragSource source)
        {
            if (!source || !LocalPlayer) return null;
            if (source.thing == BagThing.Letter) return source.index < bagOrder.Count ? One(bagOrder[source.index]) : null;
            var gear = LocalPlayer.Combat.GearIn(source.index);
            return gear ? Spaced(gear.word) : null;
        }

        void ShowDragTargets()
        {
            for (int side = 0; side < DropSides; side++)
            {
                if (!dropFaces[side]) continue;
                bool over = dragShown && dragSpot == BagSpot.Drop && dragTo == side;
                dropFaces[side].color = over ? DropFaceLit : DropFace;
                dropEdges[side].color = over ? Gold : DropRim;
                dropIcons[side].color = over ? Gold : BagInk;
            }
            var focus = dragShown ? null : FocusedBagSource();
            for (int i = 0; i < bagCells.Length; i++)
                if (bagCells[i].focus) bagCells[i].focus.enabled = Targeted(BagThing.Letter, BagSpot.Letter, i, focus);
            for (int i = 0; i < bagHands.Length; i++)
                if (bagHands[i].focus) bagHands[i].focus.enabled = Targeted(BagThing.Item, BagSpot.Item, i, focus);
        }

        bool Targeted(BagThing thing, BagSpot spot, int index, BagDragSource focus) => dragShown
            ? dragging && dragging.thing == thing && dragSpot == spot && dragTo == index && dragTo != dragging.index
            : focus && focus.thing == thing && focus.index == index;

        void OnBagShown(bool open)
        {
            if (open)
            {
                if (LocalPlayer && LocalPlayer.Binding is GamepadBinding)
                {
                    var hand = bagHands[Mathf.Clamp(LocalPlayer.Combat.ActiveSlot, 0, bagHands.Length - 1)];
                    if (hand.face) FocusOn(hand.face.GetComponent<Button>());
                }
                return;
            }
            StopBagDrag();
            hovered = null;
            var events = EventSystem.current;
            if (events && events.currentSelectedGameObject && events.currentSelectedGameObject.TryGetComponent(out BagDragSource source) && source.hud == this)
                ClearFocus();
            ShowDragTargets();
        }
    }
}
