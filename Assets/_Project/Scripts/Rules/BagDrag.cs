using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules
{
    public enum BagThing { None, Letter, Item }

    public enum BagSpot { None, Letter, Item, Drop }

    public enum BagMoveKind { Back, Reorder, Swap, Drop }

    public readonly struct BagMove
    {
        public readonly BagMoveKind Kind;
        public readonly int To;

        public BagMove(BagMoveKind kind, int to) { Kind = kind; To = to; }

        public static BagMove Back => new(BagMoveKind.Back, -1);
    }

    public static class BagDrag
    {
        public const float StartPixels = 12f;
        public const float ColumnMin = 30f, ColumnMax = 96f, ColumnGap = 8f, ColumnEdge = 4f;
        public const float IconShare = .6f, IconMin = 36f, IconMax = 64f, IconInset = 4f;
        public const string DropWords = "drag to a bin to drop";

        public static bool Started(float dx, float dy) => dx * dx + dy * dy >= StartPixels * StartPixels;

        public static float ColumnWidth(float spare) => Math.Min(ColumnMax, Math.Max(ColumnMin, spare));

        public static float ColumnReserve(float width) => ColumnEdge + width + ColumnGap;

        public static float SideColumnWidth(float areaWidth, float middle) => ColumnWidth((areaWidth - middle) * .5f - ColumnReserve(0f));

        public static float MapTarget(float areaWidth, float leftTaken, float gap, float floor, float bookComfort, float bookMin, float bookMax)
        {
            float taken = leftTaken + floor + gap;
            float reserve = ColumnReserve(ColumnWidth(areaWidth - taken - bookComfort - ColumnReserve(0f)));
            float book = Math.Min(bookMax, Math.Max(bookMin, areaWidth - taken - reserve));
            return Math.Max(floor, areaWidth - leftTaken - gap - reserve - book);
        }

        public static float LetterScale(float footprint, float margin, float gridWidth) => (footprint - margin) / gridWidth;

        public static float MapSide(float height, float rest, float gap, float floor, float most, float column, float margin, float gridWidth, float gridHeight)
        {
            float free = height - rest - gap, slope = gridHeight / gridWidth;
            float wide = (free + slope * margin) / (1f + slope);
            float side = wide >= column ? wide : free - gridHeight * LetterScale(column, margin, gridWidth);
            return Math.Min(most, Math.Max(floor, side));
        }

        public static float IconSize(float columnWidth) =>
            Math.Max(0f, Math.Min(Math.Min(IconMax, Math.Max(IconMin, columnWidth * IconShare)), columnWidth - 2f * IconInset));

        public static bool CanDrag(BagThing thing, int index, int letters, bool hasItem) => thing switch
        {
            BagThing.Letter => index >= 0 && index < letters,
            BagThing.Item => index >= 0 && hasItem,
            _ => false,
        };

        public static BagMove Resolve(BagThing thing, int from, BagSpot spot, int to, int letters)
        {
            if (thing == BagThing.None || from < 0) return BagMove.Back;
            if (spot == BagSpot.Drop) return new BagMove(BagMoveKind.Drop, from);
            if (thing == BagThing.Letter && spot == BagSpot.Letter && to >= 0 && letters > 0)
            {
                int target = Math.Min(to, letters - 1);
                return target == from ? BagMove.Back : new BagMove(BagMoveKind.Reorder, target);
            }
            if (thing == BagThing.Item && spot == BagSpot.Item && to >= 0 && to != from) return new BagMove(BagMoveKind.Swap, to);
            return BagMove.Back;
        }

        public static (BagSpot spot, int index) TraySpot(int letter, int item, bool inBlock)
        {
            if (letter >= 0) return (BagSpot.Letter, letter);
            if (item >= 0) return (BagSpot.Item, item);
            return inBlock ? (BagSpot.None, -1) : (BagSpot.Drop, -1);
        }

        public static bool TrayAccepts(bool bagOpen, bool playing, bool cardShown, bool spellBoxOpen, bool cursorLocked, bool pointerSeat) =>
            !bagOpen && playing && !cardShown && !spellBoxOpen && !cursorLocked && pointerSeat;

        public static bool ShowsBin(BagSpot spot, bool canDrop) => spot == BagSpot.Drop && canDrop;

        public static bool Arrange(List<char> order, IReadOnlyList<char> letters, bool arranged, List<char> spare)
        {
            spare.Clear();
            for (int i = 0; i < letters.Count; i++) spare.Add(letters[i]);
            if (!arranged || spare.Count == 0)
            {
                order.Clear();
                order.AddRange(spare);
                order.Sort();
                return false;
            }
            int kept = 0;
            for (int i = 0; i < order.Count; i++)
            {
                int at = spare.IndexOf(order[i]);
                if (at < 0) continue;
                spare.RemoveAt(at);
                order[kept++] = order[i];
            }
            order.RemoveRange(kept, order.Count - kept);
            order.AddRange(spare);
            return true;
        }

        public static bool Move(List<char> order, int from, int to)
        {
            if (from < 0 || from >= order.Count || to < 0 || to == from) return false;
            char letter = order[from];
            order.RemoveAt(from);
            order.Insert(Math.Min(to, order.Count), letter);
            return true;
        }

        public static char Take(List<char> order, int index)
        {
            if (index < 0 || index >= order.Count) return '\0';
            char letter = order[index];
            order.RemoveAt(index);
            return letter;
        }

        public static string Hint(string thing, string key, bool touch)
        {
            string how = touch || string.IsNullOrEmpty(key) ? DropWords : $"hold {key} or {DropWords}";
            return string.IsNullOrEmpty(thing) ? Capital(how) : $"{thing} · {how}";
        }

        static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }
}
