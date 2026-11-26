using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public partial class GameHud
    {
        const float ArrowUpTurn = 90f, ArrowDownTurn = -90f, ArrowGlyphInset = -8f;

        readonly char[] trayBefore = new char[MostTrayTiles];
        int trayBagRows = BagRows.BaseRows, trayTop, trayPlacedTop, trayHeldBefore, trayRound = int.MinValue;
        LetterInventory trayFor;
        RectTransform trayWindow, trayScroll, trayThumb;
        CanvasGroup trayUp, trayDown;

        public int TrayBagRows => trayBagRows;
        public int TrayTop => trayTop;
        public bool TrayScrolls => trayScroll && trayScroll.gameObject.activeSelf;

        bool TrayFrozen => dragging && dragging.tray;

        void BuildTrayScroll()
        {
            trayWindow = Rect("Letter window", tray, new Vector2(0f, 1f), Vector2.zero, new Vector2(BagRows.LettersWide, TrayWindow.ColumnTall));
            trayWindow.pivot = new Vector2(0f, 1f);
            trayScroll = Rect("Letter scroll", tray, new Vector2(0f, 1f), new Vector2(-TrayWindow.ColumnGap, 0f), new Vector2(TrayWindow.ColumnWide, TrayWindow.ColumnTall));
            trayScroll.pivot = Vector2.one;
            trayUp = TrayArrow("Up", new Vector2(.5f, 1f), ArrowUpTurn, -1);
            trayDown = TrayArrow("Down", new Vector2(.5f, 0f), ArrowDownTurn, 1);
            var track = Panel("Track", trayScroll, new Vector2(.5f, 1f), new Vector2(0f, -(TrayWindow.ArrowTall + TrayWindow.ArrowGap)),
                new Vector2(TrayWindow.TrackWide, TrayWindow.TrackTall), new Vector2(.5f, 1f), HealthTrack);
            trayThumb = Panel("Thumb", track, new Vector2(.5f, 1f), Vector2.zero, new Vector2(TrayWindow.TrackWide, TrayWindow.TrackTall), new Vector2(.5f, 1f), GlassEdge);
            trayScroll.gameObject.SetActive(false);
        }

        CanvasGroup TrayArrow(string label, Vector2 anchor, float turn, int step)
        {
            var rt = Panel(label, trayScroll, anchor, Vector2.zero, new Vector2(TrayWindow.ColumnWide, TrayWindow.ArrowTall), anchor, Glass);
            var face = rt.GetComponent<Image>(); face.raycastTarget = true;
            var edge = CreateImage("Edge", rt, Vector2.zero, Vector2.zero, Vector2.zero, GlassIdle);
            Stretch(edge.rectTransform); edge.sprite = ringSprite; edge.type = Image.Type.Sliced;
            var glyph = Cover("Icon", rt, ArrowGlyphInset).gameObject.AddComponent<Image>();
            glyph.sprite = LobbyIcons.Get(LobbyIcons.Chevron); glyph.color = OnGame; glyph.preserveAspect = true; glyph.raycastTarget = false;
            glyph.rectTransform.localRotation = Quaternion.Euler(0f, 0f, turn);
            AddHudShadow(glyph);
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None; button.targetGraphic = face;
            var nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav;
            button.onClick.AddListener(() => ScrollTray(step));
            return rt.gameObject.AddComponent<CanvasGroup>();
        }

        public bool ScrollTray(int step)
        {
            if (TrayFrozen || !TrayWindow.Scrolls(trayBagRows)) return false;
            int next = TrayWindow.Scroll(trayTop, step, trayBagRows);
            if (next == trayTop) return false;
            trayTop = next;
            if (LocalPlayer && tray.gameObject.activeSelf) ShowTrayWindow(LocalPlayer.Inventory);
            return true;
        }

        void WheelTray()
        {
            var mouse = Mouse.current;
            if (mouse == null || !trayWindow || !tray.gameObject.activeInHierarchy || BagOpen || CursorPolicy.Locked || !TrayWindow.Scrolls(trayBagRows)) return;
            int step = TrayWindow.WheelStep(mouse.scroll.ReadValue().y);
            if (step == 0 || !RectTransformUtility.RectangleContainsScreenPoint(trayWindow, mouse.position.ReadValue(), null)) return;
            ScrollTray(step);
        }

        void FollowTray(LetterInventory inv)
        {
            int round = rounds ? rounds.Round : 0;
            if (inv != trayFor || round != trayRound)
            {
                trayFor = inv; trayRound = round; trayTop = 0;
                RememberTray();
                return;
            }
            if (TrayFrozen) return;
            int held = bagOrder.Count;
            if (held != trayHeldBefore)
            {
                int landed = held > trayHeldBefore ? TrayWindow.Landed(trayBefore, trayHeldBefore, bagOrder) : -1;
                trayTop = TrayWindow.Follow(trayTop, trayBagRows, trayHeldBefore, held, held + inv.ReservedCount, landed);
            }
            trayTop = TrayWindow.Clamp(trayTop, trayBagRows);
            RememberTray();
        }

        void RememberTray()
        {
            int kept = Mathf.Min(bagOrder.Count, trayBefore.Length);
            for (int i = 0; i < kept; i++) trayBefore[i] = bagOrder[i];
            trayHeldBefore = bagOrder.Count;
        }

        void ShowTrayWindow(LetterInventory inv)
        {
            int first = TrayWindow.Clamp(trayTop, trayBagRows) * TilesPerRow;
            FillCells(trayCells, inv, first, first + trayRows * TilesPerRow);
            PlaceTrayCells();
            ShowTrayScroll();
        }

        void PlaceTrayCells()
        {
            if (trayPlacedTop == trayTop) return;
            trayPlacedTop = trayTop;
            for (int i = 0; i < trayCells.Length; i++)
                ((RectTransform)trayCells[i].face.transform.parent).anchoredPosition = new Vector2(BagRows.CellX(i), TrayWindow.CellTop(i, trayTop));
        }

        void ShowTrayScroll()
        {
            bool scrolls = TrayWindow.Scrolls(trayBagRows);
            if (trayScroll.gameObject.activeSelf != scrolls) trayScroll.gameObject.SetActive(scrolls);
            if (!scrolls) return;
            trayUp.alpha = TrayWindow.CanUp(trayTop, trayBagRows) ? 1f : TrayWindow.ArrowDimmed;
            trayDown.alpha = TrayWindow.CanDown(trayTop, trayBagRows) ? 1f : TrayWindow.ArrowDimmed;
            var thumb = TrayWindow.Thumb(trayTop, trayBagRows);
            var at = new Vector2(0f, -thumb.Offset);
            if (trayThumb.anchoredPosition != at) trayThumb.anchoredPosition = at;
            var size = new Vector2(TrayWindow.TrackWide, thumb.Length);
            if (trayThumb.sizeDelta != size) trayThumb.sizeDelta = size;
        }

        void ShowWear(HandView view, HeldWeapon gear, bool carrying)
        {
            if (!view.wearTrack) return;
            int most = gear && gear.Definition != null ? gear.Definition.Durability : 0;
            bool shows = SlotWear.Shows(gear, carrying, most);
            if (view.wearTrack.activeSelf != shows) view.wearTrack.SetActive(shows);
            if (!shows) return;
            float left = SlotWear.Fraction(gear.DurabilityLeft, most);
            view.wear.fillAmount = left;
            view.wear.color = SlotWear.Low(left) ? LowHp : view.wearColour;
        }

        void AddSlotWear(HandView view, RectTransform slot, float slotWide)
        {
            var track = Panel("Wear", slot, new Vector2(.5f, 0f), new Vector2(0f, SlotWear.SlotBottom),
                new Vector2(SlotWear.SlotWide(slotWide), SlotWear.SlotTall), new Vector2(.5f, 0f), HealthTrack);
            view.wear = Fill("Left", track, Mint); view.wearColour = Mint; view.wearTrack = track.gameObject;
            view.wearTrack.SetActive(false);
        }
    }
}
