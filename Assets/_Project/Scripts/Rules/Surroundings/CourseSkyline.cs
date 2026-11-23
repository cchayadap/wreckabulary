using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GameStudioKit.Determinism;

namespace Wreckabulary.Rules.Surroundings
{
    public static class CourseSkyline
    {
        public const int HighRenderers = 8, HighTriangles = 120000, LowRenderers = 3, LowTriangles = 24000;
        public const float FarClip = 1800f, ViewMargin = 100f, EyeLift = 10f;
        public const string WallSlot = SurroundingsSlots.Plaster, GlassSlot = "bk_glass_dark", MetalSlot = "bk_metal", LitSlot = "bk_glass_lit";
        public const float WallSmoothness = .06f, GlassSmoothness = .3f, MetalSmoothness = .3f;
        public const float GlowRed = .55f, GlowGreen = .45f, GlowBlue = .3f;

        public static readonly IReadOnlyList<string> Slots = new[] { WallSlot, GlassSlot, MetalSlot, LitSlot };

        public static float ViewReach => FarClip - ViewMargin;

        public static float SmoothnessOf(string slot) => slot == GlassSlot || slot == LitSlot ? GlassSmoothness : slot == MetalSlot ? MetalSmoothness : WallSmoothness;

        public static IReadOnlyList<SkylineMeshPart> Parts(CourseSkylineResult skyline) =>
            CourseSkylineGeometry.Parts(skyline ?? throw new ArgumentNullException(nameof(skyline)));

        public static double FarthestView(CourseFootprint footprint, IEnumerable<SkylineBox> boxes)
        {
            double eye = footprint.Top + EyeLift, farthest = 0;
            var corners = new[] { (footprint.MinX, footprint.MinZ), (footprint.MaxX, footprint.MinZ), (footprint.MaxX, footprint.MaxZ), (footprint.MinX, footprint.MaxZ) };
            foreach (var b in boxes)
                foreach (var c in b.Corners())
                    foreach (var (x, z) in corners)
                    {
                        double flat = Math.Sqrt((c.x - x) * (c.x - x) + (c.z - z) * (c.z - z)), rise = Math.Max(Math.Abs(b.Y - eye), Math.Abs(b.TopY - eye));
                        farthest = Math.Max(farthest, Math.Sqrt(flat * flat + rise * rise));
                    }
            return farthest;
        }
    }

    sealed class CourseSkylineLot
    {
        public long Key;
        public Footprint Lot;
        public Rect Bounds;
        public Flat At;
        public double Yaw, Width, Depth, Height, Distance, Heading, Weight, Core;
        public int Cluster;
        public bool Downtown, Dropped, Crane;
        public string Type, Landmark;
        public CourseFacadeSpec Facade;
        public uint Wall, Pane, Pane2, Accent, Base, Roof, Awning;
        public double Roll, CraneRoll;
    }

    sealed class CourseSkylinePlanner
    {
        const double Slack = 1e-6, RingLift = .3, CoreFloor = .75, DowntownSkew = .75, NeedleTurn = 80, NeedleReach = 150, ClockStep = 25;
        const double TopAllowance = 70, CraneHeight = 70, ReachStep = .3;
        const int ReachSteps = 4;

        static readonly string[] CraneTypes = { CourseSkylineTypes.Block, CourseSkylineTypes.Curtain, CourseSkylineTypes.Deco, CourseSkylineTypes.Slab };

        readonly CourseSkylineSpec spec;
        readonly ulong seed;
        readonly Rect box;
        readonly Flat centre;
        readonly CourseFootprint footprint;
        readonly float groundY, clear;
        readonly List<double> headings = new List<double>();
        readonly List<CourseSkylineLot> lots = new List<CourseSkylineLot>();
        readonly List<string> thinned = new List<string>();

        public CourseSkylinePlanner(CourseSkylineSpec spec, ulong seed, CourseFootprint footprint, float groundY, float clear)
        {
            this.spec = spec;
            this.seed = seed;
            this.footprint = footprint;
            this.groundY = groundY;
            this.clear = clear;
            box = footprint.Rect;
            centre = box.Centre;
        }

        IEnumerable<CourseSkylineLot> Live => lots.Where(l => !l.Dropped);

        Pcg32 Random(string purpose, long index = 0) => Pcg32.ForStream(seed, "skyline:" + purpose, index);

        static float Q(double value, double step) => (float)(Math.Round(value / step, MidpointRounding.AwayFromZero) * step);

        static double Lerp(double a, double b, double t) => a + (b - a) * t;

        static double Lerp((float min, float max) range, double t) => range.min + (range.max - range.min) * t;

        static double Clamp01(double t) => t < 0 ? 0 : t > 1 ? 1 : t;

        static double Apart(double a, double b)
        {
            double d = Math.Abs(Shapes.Wrap360(a) - Shapes.Wrap360(b));
            return d > 180 ? 360 - d : d;
        }

        public void Plan(Func<Footprint, Rect, bool> blocked)
        {
            PickHeadings();
            double reach = Math.Max(spec.Downtown.Band.To, spec.Ring.To) + spec.Cell;
            double half = Math.Max(box.MaxX - box.MinX, box.MaxZ - box.MinZ) * .5 + reach;
            int cells = (int)Math.Ceiling(half / spec.Cell);
            for (int i = -cells; i <= cells; i++)
                for (int j = -cells; j <= cells; j++)
                    Consider(i, j, blocked);
            PlaceLandmarks();
            foreach (var lot in Live.ToList())
                if (FarthestView(lot) > CourseSkyline.ViewReach) lot.Dropped = true;
            Paint();
        }

        void Paint()
        {
            var painted = new Dictionary<long, CourseSkylineLot>();
            foreach (var lot in Live.OrderBy(l => l.Key))
            {
                var rng = Random("paint", lot.Key);
                var near = Neighbours(lot.Key, painted).ToList();
                var facade = lot.Facade;
                lot.Wall = Choose(facade.Walls, rng.NextDouble(), near.Select(n => n.Wall));
                lot.Pane = Choose(facade.Panes, rng.NextDouble(), near.Select(n => n.Pane));
                lot.Pane2 = Choose(facade.Panes, rng.NextDouble(), new[] { lot.Pane });
                var accents = lot.Landmark != null && spec.Bold.Count > 0 ? spec.Bold : facade.Accents.Count > 0 ? facade.Accents : spec.Accents;
                lot.Accent = Choose(accents, rng.NextDouble(), near.Select(n => n.Accent).Append(lot.Wall));
                lot.Base = Choose(spec.Bases, rng.NextDouble(), near.Select(n => n.Base).Append(lot.Wall));
                lot.Roof = Choose(spec.Roofs, rng.NextDouble(), near.Select(n => n.Roof));
                lot.Awning = Choose(spec.Awnings, rng.NextDouble(), near.Select(n => n.Awning).Append(lot.Base));
                painted[lot.Key] = lot;
            }
        }

        static IEnumerable<CourseSkylineLot> Neighbours(long key, Dictionary<long, CourseSkylineLot> painted)
        {
            for (long di = -1; di <= 1; di++)
                for (long dj = -1; dj <= 1; dj++)
                    if ((di != 0 || dj != 0) && painted.TryGetValue(key + di * 100000L + dj, out var lot)) yield return lot;
        }

        static uint Choose(IReadOnlyList<uint> list, double pick, IEnumerable<uint> avoid)
        {
            var taken = new HashSet<uint>(avoid);
            int start = Math.Min(list.Count - 1, (int)(pick * list.Count));
            for (int k = 0; k < list.Count; k++)
            {
                uint colour = list[(start + k) % list.Count];
                if (!taken.Contains(colour)) return colour;
            }
            return list[start];
        }

        void PickHeadings()
        {
            var rng = Random("downtown");
            var clusters = spec.Downtown.Clusters;
            int count = rng.Range(clusters.min, clusters.max + 1);
            double first = rng.NextDouble() * 360;
            double turn = rng.Range(spec.Downtown.Second, 180f) * (rng.NextFloat() < .5f ? 1 : -1);
            headings.Add(Q(first, .01));
            if (count > 1) headings.Add(Q(Shapes.Wrap360(first + turn), .01));
        }

        (double weight, int cluster) WeightAt(double heading)
        {
            double best = 0;
            int cluster = 0;
            for (int k = 0; k < headings.Count; k++)
            {
                double d = Apart(heading, headings[k]) / spec.Downtown.Spread;
                double w = Math.Exp(-d * d);
                if (w > best) { best = w; cluster = k; }
            }
            return (best, cluster);
        }

        void Consider(int i, int j, Func<Footprint, Rect, bool> blocked)
        {
            long key = (i + 10000L) * 100000L + (j + 10000L);
            var rng = Random("lot", key);
            double roll = rng.NextDouble(), tallness = rng.NextDouble(), wide = rng.NextDouble(), deep = rng.NextDouble();
            double jx = rng.NextDouble(), jz = rng.NextDouble(), turn = rng.NextDouble(), look = rng.NextDouble();
            double lowRoll = rng.NextDouble(), outlier = rng.NextDouble();
            double typePick = rng.NextDouble(), craneRoll = rng.NextDouble();
            var cell = centre + new Flat(i * spec.Cell, j * spec.Cell);
            double distance = box.Distance(cell);
            var (weight, cluster) = WeightAt((cell - centre).Yaw);
            var down = spec.Downtown.Band;
            var ring = spec.Ring;
            bool inDown = distance >= down.From && distance <= down.To && weight >= spec.Downtown.Core;
            bool inRing = distance >= ring.From && distance <= ring.To;
            if (!inDown && !inRing) return;
            double chance = inDown ? Lerp(ring.Chance, down.Chance, weight) : ring.Chance;
            if (roll >= chance) return;
            var type = PickType(inDown ? weight : 0, typePick);
            double height = HeightFor(type, inDown, weight, distance, tallness, outlier);
            var (width, depth) = SizeFor(type.Id, inDown, wide, deep);
            double yaw = 90 * Math.Min(3, (int)(turn * 4));
            double lotW = width + 2 * spec.Plaza, lotD = depth + 2 * spec.Plaza;
            bool square = Math.Abs(yaw % 180) < 1;
            double spanX = square ? lotW : lotD, spanZ = square ? lotD : lotW;
            double freeX = Math.Min(spec.Jitter, Math.Max(0, spec.Cell - spec.Gap - spanX) * .5), freeZ = Math.Min(spec.Jitter, Math.Max(0, spec.Cell - spec.Gap - spanZ) * .5);
            var at = cell + new Flat((jx * 2 - 1) * freeX, (jz * 2 - 1) * freeZ);
            at = new Flat(Q(at.X, .001), Q(at.Z, .001));
            var shape = new Footprint(at.X, at.Z, Q(lotW, .001), Q(lotD, .001), yaw);
            var bounds = shape.Bounds();
            double gap = shape.Distance(box);
            if (gap < clear - Slack || blocked(shape, bounds)) return;
            var facade = spec.Facade(type.Facades, look);
            lots.Add(new CourseSkylineLot
            {
                Key = key, Lot = shape, Bounds = bounds, At = at, Yaw = yaw, Width = Q(width, .001), Depth = Q(depth, .001), Height = Q(height, .01),
                Distance = gap, Heading = (at - centre).Yaw, Weight = weight, Core = weight * CoreAt(distance), Cluster = cluster, Downtown = inDown,
                Type = type.Id, Facade = facade, Roll = lowRoll, CraneRoll = craneRoll,
            });
        }

        CourseTypeSpec PickType(double downtown, double pick)
        {
            double total = spec.Types.Sum(t => t.WeightAt(downtown));
            double at = pick * total;
            foreach (var type in spec.Types)
            {
                double w = type.WeightAt(downtown);
                if (w <= 0) continue;
                at -= w;
                if (at < 0) return type;
            }
            return spec.Types.Last(t => t.WeightAt(downtown) > 0);
        }

        double CoreAt(double distance)
        {
            var band = spec.Downtown.Band;
            double middle = (band.From + band.To) * .5, half = Math.Max(1, (band.To - band.From) * .5);
            return Clamp01(1 - Math.Abs(distance - middle) / half);
        }

        double HeightFor(CourseTypeSpec type, bool inDown, double weight, double distance, double tallness, double outlier)
        {
            if (type.Height.HasValue) return Lerp(type.Height.Value, tallness);
            var ring = spec.Ring;
            if (!inDown && outlier < ring.TallChance) return Lerp(ring.Tall, tallness);
            var band = spec.Downtown.Band;
            double rise = ring.RowRise * Clamp01((distance - ring.From) / Math.Max(1, ring.To - ring.From));
            double lift = inDown ? weight * (CoreFloor + (1 - CoreFloor) * CoreAt(distance)) : weight * RingLift;
            double low = Lerp(ring.Height.min + rise, band.Height.min, lift), high = Lerp(ring.Height.max + rise, band.Height.max, lift);
            return Lerp(low, high, inDown ? Math.Pow(tallness, DowntownSkew) : tallness);
        }

        (double width, double depth) SizeFor(string type, bool inDown, double wide, double deep)
        {
            double limit = spec.LotLimit;
            var widths = inDown ? spec.Downtown.Band.Width : spec.Ring.Width;
            switch (type)
            {
                case CourseSkylineTypes.Loft: case CourseSkylineTypes.LPlan: case CourseSkylineTypes.UPlan:
                    return (limit * Lerp(.82, 1, wide), limit * Lerp(.82, 1, deep));
                case CourseSkylineTypes.Slab: return (limit * Lerp(.85, 1, wide), Lerp(8, 10.5, deep));
                case CourseSkylineTypes.Twin: return (limit * Lerp(.88, 1, wide), limit * Lerp(.4, .5, deep));
                case CourseSkylineTypes.Round: case CourseSkylineTypes.Deco:
                    double side = Math.Min(Lerp(widths, wide), limit);
                    return (side, side);
                default: return (Math.Min(Lerp(widths, wide), limit), Math.Min(Lerp(widths, deep), limit));
            }
        }

        void PlaceLandmarks()
        {
            var band = spec.Downtown.Band;
            for (int k = 0; k < headings.Count; k++)
            {
                var best = Live.Where(l => l.Downtown && l.Cluster == k && l.Landmark == null).OrderByDescending(l => l.Core).ThenBy(l => l.Key).FirstOrDefault();
                if (best != null) Make(best, CourseSkylineTypes.Supertall, spec.Supertall.Height, spec.SupertallFacades, Random("landmark:supertall", k));
            }
            var rng = Random("landmark:needle");
            if (rng.NextDouble() < spec.NeedleChance)
            {
                double heading = (headings.Count > 0 ? headings[0] : 0) + 180 + (rng.NextDouble() - .5) * NeedleTurn;
                var needle = Nearest(heading, NeedleReach, Math.Min(spec.LotLimit, 20));
                if (needle != null) Make(needle, CourseSkylineTypes.Needle, spec.Needle.Height, spec.NeedleFacades, rng);
            }
            var clockRng = Random("landmark:clock");
            var clock = Nearest(clockRng.NextDouble() * 360, spec.Ring.From + ClockStep, 0);
            if (clock != null) Make(clock, CourseSkylineTypes.Clock, spec.Clock.Height, spec.ClockFacades, clockRng);
            foreach (var lot in Live)
                lot.Crane = lot.Downtown && lot.Landmark == null && lot.Height >= CraneHeight && lot.CraneRoll < spec.CraneChance && CraneTypes.Contains(lot.Type);
        }

        CourseSkylineLot Nearest(double heading, double distance, double least)
        {
            var dir = Flat.Heading(heading);
            double reach = Math.Abs(dir.X) * (box.MaxX - box.MinX) * .5 + Math.Abs(dir.Z) * (box.MaxZ - box.MinZ) * .5 + distance;
            var target = centre + dir * reach;
            return Live.Where(l => l.Landmark == null && Math.Min(l.Lot.HalfWidth, l.Lot.HalfDepth) * 2 - 2 * spec.Plaza >= least)
                .OrderBy(l => (l.At - target).Length).ThenBy(l => l.Key).FirstOrDefault();
        }

        void Make(CourseSkylineLot lot, string type, (float min, float max) heights, IReadOnlyList<string> facades, Pcg32 rng)
        {
            double side = Math.Min(lot.Lot.HalfWidth, lot.Lot.HalfDepth) * 2 - 2 * spec.Plaza;
            lot.Type = type;
            lot.Landmark = type;
            lot.Width = lot.Depth = Q(side, .001);
            lot.Height = Q(Lerp(heights, rng.NextDouble()), .01);
            lot.Facade = facades.Count > 0 ? spec.Facade(facades, rng.NextDouble()) : lot.Facade;
        }

        double FarthestView(CourseSkylineLot lot)
        {
            double eye = footprint.Top + CourseSkyline.EyeLift, rise = Math.Max(Math.Abs(groundY - eye), Math.Abs(groundY + lot.Height + TopAllowance - eye)), farthest = 0;
            foreach (var c in lot.Lot.Corners())
                foreach (var corner in box.Corners())
                {
                    double flat = (c - corner).Length;
                    farthest = Math.Max(farthest, Math.Sqrt(flat * flat + rise * rise));
                }
            return farthest;
        }

        public HashSet<int> Thin(IReadOnlyList<string> pieces, IReadOnlyList<string> sources, IReadOnlyList<Footprint> cards)
        {
            var dropped = new HashSet<int>();
            var downtown = Live.Where(l => l.Downtown).ToList();
            for (int c = 0; c < cards.Count; c++)
            {
                if (!spec.ThinCards.Contains(pieces[c])) continue;
                if (!downtown.Any(l => l.Lot.Overlaps(cards[c], spec.CardGap))) continue;
                dropped.Add(c);
                thinned.Add(sources[c]);
            }
            for (int c = 0; c < cards.Count; c++)
            {
                if (dropped.Contains(c)) continue;
                foreach (var lot in Live.ToList())
                    if (lot.Lot.Overlaps(cards[c], spec.CardGap)) lot.Dropped = true;
            }
            return dropped;
        }

        public bool Covers(Footprint shape, Rect bounds)
        {
            foreach (var lot in lots)
                if (!lot.Dropped && lot.Bounds.Overlaps(bounds) && lot.Lot.Overlaps(shape)) return true;
            return false;
        }

        public CourseSkylineResult Finish(CourseSurroundingsTier tier, List<string> problems)
        {
            var kept = Trim(Live.ToList(), true, out int trimmed, out float reach);
            if (tier == CourseSurroundingsTier.Low)
            {
                var picked = kept.Where(k => k.lot.Landmark != null || k.lot.Roll < (k.lot.Downtown ? spec.KeepDowntown : spec.KeepRing)).Select(k => k.lot).ToList();
                kept = Trim(picked, false, out trimmed, out reach);
            }
            var towers = kept.Select(k => k.tower).ToList();
            for (int n = 0; n < towers.Count; n++) towers[n].Ordinal = n;
            var result = CourseSkylineResult.None(tier);
            result.Headings = headings.Select(h => (float)h).ToList();
            result.Towers = towers;
            result.Groups = CourseSkylineGeometry.Groups(towers);
            result.ThinnedCards = thinned.ToList();
            result.Trimmed = trimmed;
            result.DetailReach = reach;
            Check(result, problems);
            result.Canonical = Canonical(result);
            result.Hash = Hash64.ToHex(Hash64.Of(result.Canonical));
            return result;
        }

        List<(CourseSkylineLot lot, CourseTower tower)> Trim(List<CourseSkylineLot> chosen, bool full, out int trimmed, out float reach)
        {
            int rendererLimit = full ? CourseSkyline.HighRenderers : CourseSkyline.LowRenderers;
            int triangleLimit = full ? CourseSkyline.HighTriangles : CourseSkyline.LowTriangles;
            bool Fits(List<(CourseSkylineLot lot, CourseTower tower)> set) =>
                set.Sum(k => k.tower.Triangles) <= triangleLimit && CourseSkylineGeometry.Groups(set.Select(k => k.tower)).Sum(g => g.Renderers) <= rendererLimit;
            trimmed = 0;
            reach = 0;
            List<(CourseSkylineLot lot, CourseTower tower)> kept = null;
            for (int step = 0; step < (full ? ReachSteps : 1); step++)
            {
                reach = full ? Q(spec.DetailReach * Math.Max(0, 1 - step * ReachStep), .01) : 0;
                if (step == ReachSteps - 1) reach = 0;
                float detail = reach;
                kept = chosen.Select(l => (lot: l, tower: Tower(l, full, detail))).ToList();
                if (Fits(kept)) return kept;
            }
            int triangles = kept.Sum(k => k.tower.Triangles);
            var order = kept.OrderBy(k => k.lot.Landmark != null ? 1 : 0).ThenBy(k => k.lot.Downtown ? 1 : 0).ThenByDescending(k => k.lot.Distance)
                .ThenBy(k => k.lot.Height).ThenByDescending(k => k.lot.Key).ToList();
            foreach (var entry in order)
            {
                if (triangles <= triangleLimit && Fits(kept)) break;
                kept.Remove(entry);
                triangles -= entry.tower.Triangles;
                trimmed++;
            }
            return kept;
        }

        CourseTower Tower(CourseSkylineLot lot, bool full, float reach)
        {
            bool detailed = full && (lot.Landmark != null || lot.Distance <= reach);
            return new CourseTower
            {
                X = (float)lot.At.X, Z = (float)lot.At.Z, Yaw = (float)lot.Yaw, Width = (float)lot.Width, Depth = (float)lot.Depth,
                LotWidth = (float)lot.Lot.HalfWidth * 2, LotDepth = (float)lot.Lot.HalfDepth * 2, Ground = Q(groundY, .001), Height = (float)lot.Height,
                Distance = Q(lot.Distance, .01), Heading = Q(lot.Heading, .01), Weight = Q(lot.Weight, .001), Downtown = lot.Downtown, Detailed = detailed,
                Crane = lot.Crane, Type = lot.Type, Landmark = lot.Landmark, Facade = lot.Facade.Id, Wall = lot.Wall, Pane = lot.Pane, Accent = lot.Accent, Base = lot.Base,
                Source = lot.Landmark ?? (lot.Downtown ? "downtown " + lot.Cluster : "ring"),
                Boxes = new CourseTowerShape(spec, lot, groundY, full, detailed).Build(Random("tower", lot.Key)),
            };
        }

        void Check(CourseSkylineResult result, List<string> problems)
        {
            void Problem(string line) { if (!problems.Contains(line)) problems.Add(line); }
            if (result.Renderers > result.RendererLimit) Problem($"skyline budget: {result.Tier}: {result.Renderers} renderers, at most {result.RendererLimit}");
            if (result.Triangles > result.TriangleLimit) Problem($"skyline budget: {result.Tier}: {result.Triangles} triangles, at most {result.TriangleLimit}");
            foreach (var tower in result.Towers)
                foreach (var b in tower.Boxes)
                {
                    var shape = new Footprint(b.X, b.Z, b.Width * b.Reach, b.Depth * b.Reach, b.Yaw);
                    if (shape.Distance(box) < clear - Slack) Problem($"skyline clear: the {b.Part} of the {tower.Source} {tower.Type} at ({tower.X}, {tower.Z}) is inside the {clear} m margin");
                    if (b.Y < groundY - Slack) Problem($"skyline ground: the {b.Part} of the {tower.Source} {tower.Type} at ({tower.X}, {tower.Z}) sinks below the street");
                }
            double farthest = CourseSkyline.FarthestView(footprint, result.Boxes);
            if (farthest > CourseSkyline.FarClip) Problem($"skyline far clip: a tower point is {farthest:0} m from a course corner, beyond the {CourseSkyline.FarClip} m camera far clip");
        }

        static string Canonical(CourseSkylineResult r)
        {
            var text = new StringBuilder();
            text.Append("skyline ").Append(r.Tier).Append(' ').Append(string.Join(",", r.Headings.Select(h => F(h)))).Append(" reach ").Append(F(r.DetailReach)).Append('\n');
            foreach (string card in r.ThinnedCards) text.Append("thin ").Append(card).Append('\n');
            foreach (var t in r.Towers)
            {
                text.Append("tower ").Append(t.Type).Append(' ').Append(t.Landmark ?? "-").Append(t.Crane ? " crane " : " - ").Append(t.Detailed ? "rich " : "plain ")
                    .Append(string.Join(" ", new[] { t.X, t.Z, t.Yaw, t.Width, t.Depth, t.Height, t.Top }.Select(F))).Append(' ')
                    .Append(t.Facade).Append(' ').Append(Hex(t.Wall)).Append(' ').Append(Hex(t.Pane)).Append(' ').Append(Hex(t.Accent)).Append(' ').Append(Hex(t.Base)).Append(' ').Append(t.Source).Append(' ')
                    .Append(t.Boxes.Count).Append(' ').Append(t.Triangles).Append('\n');
            }
            foreach (var g in r.Groups) text.Append("group ").Append(g.Slot).Append(' ').Append(g.Boxes).Append(' ').Append(g.Triangles).Append(' ').Append(g.Renderers).Append('\n');
            return text.ToString();
        }

        static string F(float x) => (x == 0 ? 0.0 : x).ToString("0.###", CultureInfo.InvariantCulture);

        static string Hex(uint rgb) => rgb.ToString("x6", CultureInfo.InvariantCulture);
    }

    static class CourseSkylineGeometry
    {
        public static List<SkylineGroup> Groups(IEnumerable<CourseTower> towers)
        {
            var list = towers.ToList();
            var groups = new List<SkylineGroup>();
            foreach (string slot in CourseSkyline.Slots)
            {
                var group = new SkylineGroup { Slot = slot };
                int part = 0;
                foreach (var b in list.SelectMany(t => t.Boxes).Where(b => b.Slot == slot))
                {
                    if (part > 0 && part + b.Vertices > SurroundingsLayout.MergeVertexLimit) { group.Renderers++; part = 0; }
                    part += b.Vertices;
                    group.Boxes++;
                    group.Triangles += b.Triangles;
                    group.Vertices += b.Vertices;
                }
                if (group.Boxes == 0) continue;
                group.Renderers++;
                groups.Add(group);
            }
            return groups;
        }

        sealed class Builder
        {
            public readonly List<float> Positions = new List<float>(), Normals = new List<float>();
            public readonly List<uint> Colours = new List<uint>();
            public readonly List<int> Indices = new List<int>();
            public int Vertices => Colours.Count;
        }

        readonly struct V
        {
            public readonly double X, Y, Z;

            public V(double x, double y, double z)
            {
                X = x; Y = y; Z = z;
            }

            public static V operator +(V a, V b) => new V(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
            public static V operator -(V a, V b) => new V(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
            public static V operator *(V a, double s) => new V(a.X * s, a.Y * s, a.Z * s);

            public double Dot(V o) => X * o.X + Y * o.Y + Z * o.Z;
            public V Cross(V o) => new V(Y * o.Z - Z * o.Y, Z * o.X - X * o.Z, X * o.Y - Y * o.X);
            public V Unit => this * (1 / Math.Sqrt(Math.Max(1e-24, Dot(this))));
        }

        public static List<SkylineMeshPart> Parts(CourseSkylineResult skyline)
        {
            var parts = new List<SkylineMeshPart>();
            foreach (string slot in CourseSkyline.Slots)
            {
                var builder = new Builder();
                int index = 0;
                foreach (var b in skyline.Boxes.Where(b => b.Slot == slot))
                {
                    if (builder.Vertices > 0 && builder.Vertices + b.Vertices > SurroundingsLayout.MergeVertexLimit)
                    {
                        parts.Add(Part(slot, index++, builder));
                        builder = new Builder();
                    }
                    Append(builder, b);
                }
                if (builder.Vertices > 0) parts.Add(Part(slot, index, builder));
            }
            return parts;
        }

        static SkylineMeshPart Part(string slot, int index, Builder builder) => new SkylineMeshPart
        {
            Slot = slot, Index = index, Positions = builder.Positions.ToArray(), Normals = builder.Normals.ToArray(),
            Colours = builder.Colours.ToArray(), Indices = builder.Indices.ToArray(),
        };

        static V[] Ring(SkylineBox b, double y, double scale)
        {
            double yaw = b.Yaw * Math.PI / 180, ax = Math.Sin(yaw), az = Math.Cos(yaw), rx = az, rz = -ax;
            double reach = 1 / Math.Cos(Math.PI / b.Sides), hw = b.Width * .5 * scale * reach, hd = b.Depth * .5 * scale * reach;
            var ring = new V[b.Sides];
            for (int k = 0; k < b.Sides; k++)
            {
                double a = 2 * Math.PI * k / b.Sides + Math.PI / b.Sides, u = Math.Cos(a) * hw, v = Math.Sin(a) * hd;
                ring[k] = new V(b.X + rx * u + ax * v, y, b.Z + rz * u + az * v);
            }
            return ring;
        }

        static void Append(Builder builder, SkylineBox b)
        {
            var axis = new V(b.X, b.Y + b.Height * .5, b.Z);
            var bottom = Ring(b, b.Y, 1);
            var top = b.Apex ? null : Ring(b, b.TopY, b.Taper);
            var apex = new V(b.X, b.TopY, b.Z);
            for (int k = 0; k < b.Sides; k++)
            {
                var b0 = bottom[k];
                var b1 = bottom[(k + 1) % b.Sides];
                var face = b.Apex ? new[] { b0, b1, apex } : new[] { b0, b1, top[(k + 1) % b.Sides], top[k] };
                Emit(builder, b.Tint, face, (b0 + b1) * .5 - axis);
            }
            if (b.Capped) Emit(builder, b.Tint, top, new V(0, 1, 0));
        }

        static void Emit(Builder builder, uint tint, V[] face, V outward)
        {
            var normal = (face[1] - face[0]).Cross(face[2] - face[0]);
            if (normal.Dot(outward) < 0)
            {
                Array.Reverse(face);
                normal = normal * -1;
            }
            normal = normal.Unit;
            int start = builder.Vertices;
            foreach (var p in face)
            {
                builder.Positions.Add((float)p.X);
                builder.Positions.Add((float)p.Y);
                builder.Positions.Add((float)p.Z);
                builder.Normals.Add((float)normal.X);
                builder.Normals.Add((float)normal.Y);
                builder.Normals.Add((float)normal.Z);
                builder.Colours.Add(tint);
            }
            for (int i = 1; i + 1 < face.Length; i++)
            {
                builder.Indices.Add(start);
                builder.Indices.Add(start + i);
                builder.Indices.Add(start + i + 1);
            }
        }
    }
}
