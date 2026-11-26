using System;

namespace Wreckabulary.Rules
{
    public readonly struct HudBox
    {
        public readonly float X, Y, Width, Height;

        public HudBox(float x, float y, float width, float height) { X = x; Y = y; Width = width; Height = height; }

        public float Right => X + Width;
        public float Top => Y + Height;

        public bool Overlaps(HudBox other) => X < other.Right && other.X < Right && Y < other.Top && other.Y < Top;

        public bool Inside(float width, float height) => X >= 0f && Y >= 0f && Right <= width && Top <= height;

        public override string ToString() => $"({X:0.#}, {Y:0.#}, {Width:0.#} x {Height:0.#})";
    }

    public readonly struct TrayFrame
    {
        public readonly HudBox Tray, Letters, Hands, SpellHint, Vitals;

        public TrayFrame(HudBox tray, HudBox letters, HudBox hands, HudBox spellHint, HudBox vitals)
        {
            Tray = tray; Letters = letters; Hands = hands; SpellHint = spellHint; Vitals = vitals;
        }
    }

    public readonly struct BagFit
    {
        public readonly float Map, Scale;

        public BagFit(float map, float scale) { Map = map; Scale = scale; }
    }

    public static class BagRows
    {
        public const int SlotsPerRow = 5, BaseRows = 2, MostRows = 5, MostBonusRows = 1;
        public const int BaseCells = BaseRows * SlotsPerRow, MostCells = MostRows * SlotsPerRow;

        public const float TrayWidth = 500f, TrayBaseHeight = 176f, TrayBottom = 52f, TouchTrayBottom = 24f;
        public const float TrayStepX = 62f, TrayStepY = 70f, TrayTileWide = 54f, TrayTileTall = 58f;
        public const float HandX = 324f, HandWide = 176f, HandTall = 82f, HandStep = 94f;
        public const float SpellHintGap = 8f, SpellHintTall = 30f, SpellHintWide = 310f;
        public const float VitalsX = 39f, VitalsY = 38f, AsideVitalsY = 276f, TouchVitalsY = 250f, VitalsWide = 286f, VitalsTall = 46f, EffectsAbove = 32f;
        public const float SideInsetX = 38f, SideInsetY = 30f, MapSize = 252f;
        public const float ReferenceWidth = 1920f, ReferenceHeight = 1080f, TrayGutter = 76f;
        public const float FitSlack = .5f, MinLetterScale = .5f;
        public const float RowPopSeconds = .35f, RowPopGrow = .18f;

        public const string Kind = "Bag upgrade", Chip = "+5 slots";
        public const string CardLine = "Adds a row of letter slots for this round. Takes no hand.";
        public const string AddsLabel = "Adds", AddsValue = "5 letter slots", LastsValue = "This round", NoHand = "No hand";
        public const string GainedPopup = "+5 SLOTS", FullPopup = "BAG IS ALREADY BIG";
        public const string FullLine = "Your bag already has its extra row.";

        public static int Capacity(int baseLetters, int bonusRows) => Math.Max(0, baseLetters) + SlotsPerRow * Math.Max(0, Math.Min(MostBonusRows, bonusRows));

        public static bool CanAddRow(int baseLetters, int bonusRows) =>
            bonusRows >= 0 && bonusRows < MostBonusRows && Capacity(baseLetters, bonusRows + 1) <= MostCells;

        public static int AddRow(int baseLetters, int bonusRows) => CanAddRow(baseLetters, bonusRows) ? bonusRows + 1 : bonusRows;

        public static bool CanPickUp(int held, int capacity) => held < capacity;

        public static int ShownCells(int capacity, int held) => Math.Min(MostCells, Math.Max(Math.Max(0, capacity), Math.Max(0, held)));

        public static int RowsFor(int cells) => Math.Max(BaseRows, Math.Min(MostRows, (Math.Max(0, cells) + SlotsPerRow - 1) / SlotsPerRow));

        public static int RowOf(int cell) => cell / SlotsPerRow;

        public static float TrayHeight(int rows) => Math.Max(TrayBaseHeight, TrayTileTall + TrayStepY * (Clamp(rows) - 1));

        public static float TrayLift(int rows) => TrayHeight(rows) - TrayBaseHeight;

        public static float HandY(int slot, int rows) => TrayHeight(rows) - HandTall - HandStep * slot;

        public static float CellX(int cell) => cell % SlotsPerRow * TrayStepX;

        public static float CellTop(int cell) => -RowOf(cell) * TrayStepY;

        public static float VitalsAt(bool aside, bool touch, int rows) => touch ? TouchVitalsY : aside ? AsideVitalsY + TrayLift(rows) : VitalsY;

        public static float GridTall(int rows, float step, float tall) => (Clamp(rows) - 1) * step + tall;

        public static float LettersWide => (SlotsPerRow - 1) * TrayStepX + TrayTileWide;

        public static float CanvasScale(float width, float height) => (float)Math.Sqrt(width / ReferenceWidth * (height / ReferenceHeight));

        public static TrayFrame Frame(int rows, float width, bool aside, bool touch)
        {
            float tall = TrayHeight(rows);
            float scale = Math.Min(1f, Math.Max(.1f, (width - TrayGutter) / TrayWidth));
            bool side = aside && !touch;
            float wide = TrayWidth * scale, high = tall * scale;
            float x = side ? VitalsX : (width - wide) * .5f;
            float y = touch ? TouchTrayBottom : TrayBottom;
            var tray = new HudBox(x, y, wide, high);
            float top = y + high;
            float lettersTall = GridTall(rows, TrayStepY, TrayTileTall) * scale;
            var letters = new HudBox(x, top - lettersTall, LettersWide * scale, lettersTall);
            var hands = new HudBox(x + HandX * scale, y + HandY(1, rows) * scale, HandWide * scale, (HandStep + HandTall) * scale);
            var hint = new HudBox(x, top + SpellHintGap * scale, SpellHintWide * scale, SpellHintTall * scale);
            var vitals = new HudBox(VitalsX, VitalsAt(side, touch, rows), VitalsWide, VitalsTall + EffectsAbove);
            return new TrayFrame(tray, letters, hands, hint, vitals);
        }

        public static HudBox Minimap(float width, float height) => new HudBox(width - SideInsetX - MapSize, height - SideInsetY - MapSize, MapSize, MapSize);

        public static BagFit Fit(float height, float rest, float gap, float floor, float most, float column, float margin, float gridWidth, float gridHeight)
        {
            float map = BagDrag.MapSide(height, rest, gap, floor, most, column, margin, gridWidth, gridHeight);
            float scale = BagDrag.LetterScale(Math.Max(map, column), margin, gridWidth);
            float free = height - rest - gap;
            float room = free - map;
            if (gridHeight * scale <= room + FitSlack) return new BagFit(map, scale);
            scale = Math.Max(MinLetterScale, room / gridHeight);
            if (gridHeight * scale > room + FitSlack) map = Math.Max(0f, free - gridHeight * scale);
            return new BagFit(map, scale);
        }

        public static float RowPop(float since)
        {
            if (since < 0f || since >= RowPopSeconds) return 1f;
            return 1f + RowPopGrow * (float)Math.Sin(Math.PI * since / RowPopSeconds);
        }

        static int Clamp(int rows) => Math.Max(BaseRows, Math.Min(MostRows, rows));
    }
}
