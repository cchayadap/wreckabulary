using System;
using System.Collections.Generic;
using System.Linq;
using GameStudioKit.Determinism;

namespace Wreckabulary.Rules.Surroundings
{
    sealed class CourseStreetBuilder
    {
        const double Slack = 1e-6, Inset = .05, ReachStep = .1, FarMargin = 40;
        const int ReachSteps = 11;
        const float Film = .01f, PavementLift = .03f, RoadLift = .07f, MarkLift = .1f, GrassLift = .05f;
        const float DashLength = 3f, DashWidth = .15f, DashEvery = 8f, ZebraDepth = 2.6f, ZebraStripe = .6f, ZebraEdge = .7f, ZebraGap = .5f;
        const float TreeEnd = 3f, TreeJitter = 1f, TreeKerb = .55f, TrunkSize = .3f, TrunkHeight = 2.4f, CrownLow = 2.4f, CrownHigh = 3f, CrownRise = 3f, CrownTaper = .4f, CrownSink = .4f, CrownFoot = .55f, CrownBelly = .4f, CrownUnder = .85f;
        const float LampKerb = .3f, LampPole = .16f, LampHeight = 5.6f, LampArm = .9f, LampHead = .3f, LampThick = .2f;
        const float CarKerb = 1.15f, CarWidth = 1.8f, CarLength = 4.3f, CarHeight = .9f, CarLift = .12f, CabinWidth = 1.6f, CabinLength = 2.3f, CabinHeight = .62f, CabinTaper = .78f, CarGap = 1.5f, CarEnd = 4f;
        const float VanShare = .25f, VanWidth = 2f, VanLength = 4f, VanHeight = 2f, CabLength = 1f, CabHeight = 1.5f, VanTint = .97f;
        const float StopDepth = 3.2f, StopHeight = 2.3f, StopRoof = 1.5f, StopPanel = .1f, StopSign = .55f, StopPole = .1f, StopPoleHeight = 2.8f, StopClear = 2.2f;
        const float GrassHeight = .3f, PathWidth = 2.2f, BenchLength = 1.6f, BenchDepth = .5f, BenchHeight = .45f, WallThick = .35f, WallHeight = .6f, HedgeThick = .8f, HedgeHeight = 1f, EntryGap = 2.6f;
        const float FountainSize = 3.6f, FountainHeight = .5f, WaterSize = 2.8f, FenceThick = .08f, FenceHeight = 2f;
        const float BoxLength = 6.1f, BoxWidth = 2.44f, BoxHeight = 2.6f, BoxGap = .6f, YardEdge = 2f, LegSize = .5f, GantryHeight = 10f, BeamWidth = .7f, BeamHeight = .9f;
        const uint Water = 0x6E9AB0;

        readonly CourseSkylineSpec spec;
        readonly CourseCityStreetSpec street;
        readonly CourseDistrictSpec district;
        readonly ulong seed;
        readonly CourseFootprint footprint;
        readonly float groundY, clear;
        readonly Rect box;
        readonly Flat centre;
        readonly List<(Footprint shape, Rect bounds)> lots;
        readonly List<CourseOpenCell> opens;
        readonly List<Footprint> cards;
        readonly Func<Footprint, Rect, bool> blocked;
        readonly HashSet<(int, int)> city;
        List<SkylineBox> boxes;
        bool full;
        double reach;

        sealed class Run
        {
            public int I, J;
            public bool Vertical;
            public Flat At;
            public double Distance;
            public long Key;
        }

        public CourseStreetBuilder(CourseSkylineSpec spec, CourseDistrictSpec district, ulong seed, CourseFootprint footprint, float groundY, float clear,
            List<Footprint> lots, List<CourseOpenCell> opens, List<Footprint> cards, Func<Footprint, Rect, bool> blocked)
        {
            this.spec = spec;
            street = spec.Streets;
            this.district = district;
            this.seed = seed;
            this.footprint = footprint;
            this.groundY = groundY;
            this.clear = clear;
            box = footprint.Rect;
            centre = box.Centre;
            this.lots = lots.Select(l => (l, l.Bounds())).ToList();
            this.cards = cards;
            this.blocked = blocked;
            this.opens = opens.Where(o => !blocked(o.Shape, o.Shape.Bounds()) && Within(o.Shape)).ToList();
            city = CityCells(spec, centre, lots, this.opens);
        }

        public static HashSet<(int, int)> CityCells(CourseSkylineSpec spec, Flat centre, IEnumerable<Footprint> lots, IEnumerable<CourseOpenCell> opens)
        {
            var city = new HashSet<(int, int)>();
            foreach (var lot in lots)
            {
                double u = (lot.Centre.X - centre.X) / spec.Cell, v = (lot.Centre.Z - centre.Z) / spec.Cell;
                if (lot.HalfWidth * 2 > spec.Cell)
                    for (int i = (int)Math.Floor(u); i <= (int)Math.Ceiling(u); i++)
                        for (int j = (int)Math.Floor(v); j <= (int)Math.Ceiling(v); j++) city.Add((i, j));
                else city.Add(((int)Math.Round(u), (int)Math.Round(v)));
            }
            foreach (var open in opens) city.Add((open.I, open.J));
            return city;
        }

        float Ground => groundY;

        float Gap => spec.Gap;

        float Length => spec.Cell - spec.Gap;

        static float Q(double value) => (float)(Math.Round(value / .001, MidpointRounding.AwayFromZero) * .001);

        Pcg32 Random(string purpose, long index) => Pcg32.ForStream(seed, "street:" + purpose, index);

        public List<SkylineBox> Build(bool high, out float used)
        {
            var runs = Runs();
            var crossings = Crossings(runs);
            int rendererLimit = high ? CourseSkyline.HighStreetRenderers : CourseSkyline.LowStreetRenderers;
            int triangleLimit = high ? CourseSkyline.HighStreetTriangles : CourseSkyline.LowStreetTriangles;
            full = high;
            for (int step = 0; step < ReachSteps; step++)
            {
                reach = (high ? street.Reach : street.LowReach) * Math.Max(0, 1 - step * ReachStep);
                if (step == ReachSteps - 1) reach = 0;
                boxes = new List<SkylineBox>();
                foreach (var run in runs) Lay(run);
                foreach (var at in crossings) Crossing(at);
                foreach (var open in opens.Where(o => o.Distance <= reach))
                    if (open.Kind == CourseOpenCell.Park) Park(open);
                    else Yard(open);
                if (boxes.Sum(b => b.Triangles) <= triangleLimit && CourseSkylineGeometry.Groups(boxes).Sum(g => g.Renderers) <= rendererLimit) break;
            }
            used = Q(reach);
            return boxes;
        }

        int Cells => (int)Math.Ceiling((Math.Max(box.MaxX - box.MinX, box.MaxZ - box.MinZ) * .5 + Math.Max(spec.Downtown.Band.To, spec.Ring.To) + spec.Cell) / spec.Cell);

        bool Within(Footprint shape) => shape.Corners().Max(c => box.Corners().Max(k => (c - k).Length)) <= CourseSkyline.ViewReach - FarMargin;

        bool Free(Footprint shape)
        {
            var bounds = shape.Bounds();
            if (shape.Distance(box) < clear - Slack || blocked(shape, bounds)) return false;
            if (!Within(shape)) return false;
            foreach (var (lot, lotBounds) in lots)
                if (lotBounds.Overlaps(bounds) && lot.Overlaps(shape)) return false;
            foreach (var card in cards)
                if (card.Overlaps(shape, spec.CardGap)) return false;
            return true;
        }

        List<Run> Runs()
        {
            var runs = new List<Run>();
            int cells = Cells;
            for (int i = -cells; i <= cells; i++)
                for (int j = -cells; j <= cells; j++)
                    foreach (bool vertical in new[] { true, false })
                    {
                        var at = vertical ? centre + new Flat((i + .5) * spec.Cell, j * spec.Cell) : centre + new Flat(i * spec.Cell, (j + .5) * spec.Cell);
                        bool beside = vertical ? city.Contains((i, j)) || city.Contains((i + 1, j)) : city.Contains((i, j)) || city.Contains((i, j + 1));
                        if (!beside) continue;
                        var shape = vertical ? new Footprint(at.X, at.Z, Gap - 2 * Inset, Length - 2 * Inset, 0) : new Footprint(at.X, at.Z, Length - 2 * Inset, Gap - 2 * Inset, 0);
                        if (!Free(shape)) continue;
                        runs.Add(new Run { I = i, J = j, Vertical = vertical, At = new Flat(Q(at.X), Q(at.Z)), Distance = shape.Distance(box), Key = ((i + 10000L) * 100000L + (j + 10000L)) * 2 + (vertical ? 0 : 1) });
                    }
            return runs;
        }

        List<Flat> Crossings(List<Run> runs)
        {
            var have = new HashSet<(int, int, bool)>(runs.Select(r => (r.I, r.J, r.Vertical)));
            var points = new List<Flat>();
            int cells = Cells;
            for (int i = -cells; i <= cells; i++)
                for (int j = -cells; j <= cells; j++)
                {
                    if (!have.Contains((i, j, true)) && !have.Contains((i, j + 1, true)) && !have.Contains((i, j, false)) && !have.Contains((i + 1, j, false))) continue;
                    var at = centre + new Flat((i + .5) * spec.Cell, (j + .5) * spec.Cell);
                    if (!Free(new Footprint(at.X, at.Z, Gap - 2 * Inset, Gap - 2 * Inset, 0))) continue;
                    points.Add(new Flat(Q(at.X), Q(at.Z)));
                }
            return points;
        }

        void Add(string part, Flat at, float y, float width, float depth, float height, double yaw, uint tint, bool top, int sides = 4, float taper = 1f, bool decal = false)
        {
            if (width <= 0 || depth <= 0 || height <= 0) return;
            boxes.Add(new SkylineBox
            {
                Part = part, Slot = CourseSkyline.SkylineSlot, X = Q(at.X), Y = Q(y), Z = Q(at.Z), Width = Q(width), Depth = Q(depth), Height = Q(height),
                Yaw = (float)yaw, Tint = tint, Top = top || decal, Sides = sides, Taper = taper, Decal = decal, Floor = Q(groundY), Rise = 0,
                Haze = Q(CourseSkyline.HazeAt(box.Distance(at), spec.HazeMost, spec.HazeFrom, spec.HazeTo)),
            });
        }

        void Decal(string part, Flat at, float lift, float width, float depth, double yaw, uint tint) => Add(part, at, Ground + lift, width, depth, Film, yaw, tint, true, 4, 1, true);

        void Lay(Run run)
        {
            double yaw = run.Vertical ? 0 : 90;
            var along = run.Vertical ? new Flat(0, 1) : new Flat(1, 0);
            var across = along.Right;
            Flat P(double u, double v) => run.At + across * u + along * v;
            float road = street.Road, walk = street.Walk, length = Length;
            bool near = run.Distance <= reach;
            Decal("road", run.At, RoadLift, road, length, yaw, street.RoadTint);
            bool kerbs = near && full;
            if (kerbs)
                foreach (int side in new[] { -1, 1 }) Add("pavement", P(side * (road + walk) * .5, 0), Ground, walk, length, street.Kerb, yaw, street.PavementTint, true);
            else Decal("pavement", run.At, PavementLift, road + 2 * walk, length, yaw, street.PavementTint);
            if (!near) return;
            float top = kerbs ? Ground + street.Kerb : Ground + PavementLift + Film;
            for (float v = -length * .5f + ZebraDepth + ZebraGap + 2; v + DashLength <= length * .5f - ZebraDepth - ZebraGap - 2; v += DashEvery)
                Decal("line", P(0, v + DashLength * .5f), MarkLift, DashWidth, DashLength, yaw, street.Line);
            foreach (int end in new[] { -1, 1 })
            {
                float v = end * (length * .5f - ZebraGap - ZebraDepth * .5f);
                int stripes = Math.Max(2, (int)((road - 2 * ZebraEdge) / (ZebraStripe * 2)) + 1);
                float pitch = (road - 2 * ZebraEdge - ZebraStripe) / (stripes - 1);
                for (int k = 0; k < stripes; k++)
                    Decal("crossing", P(-road * .5f + ZebraEdge + ZebraStripe * .5f + k * pitch, v), MarkLift, ZebraStripe, ZebraDepth, yaw, street.Crossing);
            }
            var rng = Random("run", run.Key);
            int stopSide = full && rng.NextDouble() < street.StopChance ? (rng.NextDouble() < .5 ? -1 : 1) : 0;
            foreach (int side in new[] { -1, 1 })
            {
                float shift = (float)(rng.NextDouble() * TreeJitter);
                for (float v = -length * .5f + TreeEnd + shift; v <= length * .5f - TreeEnd; v += street.TreeEvery)
                {
                    double roll = rng.NextDouble(), pick = rng.NextDouble(), size = rng.NextDouble();
                    if (side == stopSide && Math.Abs(v) < StopDepth * .5f + StopClear) continue;
                    if (!full && roll >= street.LowTrees) continue;
                    Tree(P(side * (road * .5f + TreeKerb), v), top, pick, size);
                }
            }
            if (!full) return;
            int lamps = Math.Max(1, (int)(length / street.LampEvery));
            for (int k = 0; k < lamps; k++)
            {
                int side = k % 2 == 0 ? -1 : 1;
                float v = -length * .5f + (k + .5f) * length / lamps + street.TreeEvery * .5f;
                if (v > length * .5f - TreeEnd) v -= street.TreeEvery;
                if (side == stopSide && Math.Abs(v) < StopDepth * .5f + StopClear) continue;
                Lamp(P(side * (road * .5f + LampKerb), v), P(side * (road * .5f + LampKerb - LampArm * .5f), v), top, yaw);
            }
            foreach (int side in new[] { -1, 1 })
            {
                if (rng.NextDouble() >= street.CarChance) continue;
                int cars = rng.NextDouble() < .4 ? 2 : 1;
                float room = length - 2 * CarEnd - cars * CarLength - (cars - 1) * CarGap;
                if (room < 0) continue;
                float v = -length * .5f + CarEnd + (float)(rng.NextDouble() * room);
                for (int k = 0; k < cars; k++)
                {
                    Vehicle(P(side * (road * .5f - CarKerb), v + CarLength * .5f), along, yaw, rng.NextDouble(), rng.NextDouble());
                    v += CarLength + CarGap;
                }
            }
            if (stopSide != 0) Stop(run, stopSide, top, yaw);
        }

        void Tree(Flat at, float y, double pick, double size)
        {
            uint crown = street.Crowns[Math.Min(street.Crowns.Count - 1, (int)(pick * street.Crowns.Count))];
            float width = CrownLow + (CrownHigh - CrownLow) * (float)size;
            float rise = CrownRise + width * .25f, low = TrunkHeight - CrownSink;
            Add("trunk", at, y, TrunkSize, TrunkSize, TrunkHeight, 0, street.Trunk, false);
            Add("tree", at, y + low, width * CrownFoot, width * CrownFoot, rise * CrownBelly, 0, CourseSkyline.Shade(crown, CrownUnder), false, 6, 1 / CrownFoot);
            Add("tree", at, y + low + rise * CrownBelly, width, width, rise * (1 - CrownBelly), 0, crown, true, 6, CrownTaper);
        }

        void Lamp(Flat pole, Flat head, float y, double yaw)
        {
            Add("lamp", pole, y, LampPole, LampPole, LampHeight, 0, street.Lamp, false);
            Add("lamp", head, y + LampHeight - LampThick, LampArm, LampHead, LampThick, yaw, street.Lamp, true);
        }

        void Vehicle(Flat at, Flat along, double yaw, double pick, double kind)
        {
            uint tint = street.Cars[Math.Min(street.Cars.Count - 1, (int)(pick * street.Cars.Count))];
            if (kind < VanShare)
            {
                Add("van", at - along * (CabLength * .5f), Ground + CarLift, VanWidth, VanLength, VanHeight, yaw, CourseSkyline.Shade(tint, VanTint), true);
                Add("van", at + along * (VanLength * .5f), Ground + CarLift, VanWidth, CabLength, CabHeight, yaw, CourseSkyline.Shade(tint, VanTint), true);
                return;
            }
            Add("car", at, Ground + CarLift, CarWidth, CarLength, CarHeight, yaw, tint, true);
            Add("car", at - along * .2f, Ground + CarLift + CarHeight, CabinWidth, CabinLength, CabinHeight, yaw, CourseSkyline.Shade(tint, .9), true, 4, CabinTaper);
        }

        void Stop(Run run, int side, float y, double yaw)
        {
            var along = run.Vertical ? new Flat(0, 1) : new Flat(1, 0);
            var across = along.Right;
            float road = street.Road, walk = street.Walk;
            Flat P(double u, double v) => run.At + across * u + along * v;
            Add("stop", P(side * (road * .5f + walk - StopPanel), 0), y, StopPanel, StopDepth, StopHeight, yaw, street.Shelter, true);
            Add("stop", P(side * (road * .5f + walk - StopRoof * .5f), 0), y + StopHeight, StopRoof, StopDepth + .2f, .14f, yaw, street.Shelter, true);
            Add("stop", P(side * (road * .5f + LampKerb), StopDepth * .5f + .8f), y, StopPole, StopPole, StopPoleHeight, 0, street.Lamp, false);
            Add("stop", P(side * (road * .5f + LampKerb), StopDepth * .5f + .8f), y + StopPoleHeight - StopSign, StopPanel, StopSign, StopSign, yaw, street.Line, true);
        }

        void Crossing(Flat at)
        {
            bool near = box.Distance(at) <= reach;
            float road = street.Road, walk = street.Walk;
            Decal("road", at, RoadLift, road, Gap, 0, street.RoadTint);
            Decal("road", at, RoadLift, Gap, road, 0, street.RoadTint);
            if (!(near && full))
            {
                Decal("pavement", at, PavementLift, Gap, Gap, 0, street.PavementTint);
                return;
            }
            float corner = (road + walk) * .5f;
            foreach (int sx in new[] { -1, 1 })
                foreach (int sz in new[] { -1, 1 })
                    Add("pavement", at + new Flat(sx * corner, sz * corner), Ground, walk, walk, street.Kerb, 0, street.PavementTint, true);
        }

        void Park(CourseOpenCell open)
        {
            var rng = Random("park", open.Key);
            var at = open.Shape.Centre;
            float side = Length;
            float top = full ? Ground + GrassHeight : Ground + GrassLift + Film;
            if (full) Add("grass", at, Ground, side, side, GrassHeight, 0, street.Grass, true);
            else Decal("grass", at, GrassLift, side, side, 0, street.Grass);
            Add("path", at, top, PathWidth, side - .4f, Film, 0, street.Path, true, 4, 1, true);
            Add("path", at, top, side - .4f, PathWidth, Film, 0, street.Path, true, 4, 1, true);
            int trees = 4 + (int)(rng.NextDouble() * 4);
            for (int k = 0; k < trees; k++)
            {
                double qx = k % 2 == 0 ? -1 : 1, qz = (k / 2) % 2 == 0 ? -1 : 1, jx = rng.NextDouble(), jz = rng.NextDouble(), pick = rng.NextDouble(), size = rng.NextDouble();
                if (!full && k % 2 == 1) continue;
                double spread = side * (k < 4 ? .27 : .4);
                Tree(at + new Flat(qx * spread + (jx - .5) * 2, qz * spread + (jz - .5) * 2), top, pick, size);
            }
            if (!full) return;
            bool hedge = rng.NextDouble() < .5;
            float thick = hedge ? HedgeThick : WallThick, height = hedge ? HedgeHeight : WallHeight, piece = (side - EntryGap) * .5f;
            uint edge = hedge ? CourseSkyline.Shade(street.Crowns[0], .85) : street.Wall;
            foreach (int s in new[] { -1, 1 })
                foreach (int t in new[] { -1, 1 })
                {
                    float offset = (EntryGap + piece) * .5f * t, rim = (side - thick) * .5f * s;
                    Add(hedge ? "hedge" : "wall", at + new Flat(offset, rim), top, piece, thick, height, 0, edge, true);
                    Add(hedge ? "hedge" : "wall", at + new Flat(rim, offset), top, thick, piece, height, 0, edge, true);
                }
            foreach (int s in new[] { -1, 1 })
                Add("bench", at + new Flat(s * (PathWidth * .5f + BenchDepth), side * .2f * s), top, BenchDepth, BenchLength, BenchHeight, 0, street.Trunk, true);
            if (rng.NextDouble() < .5)
            {
                Add("fountain", at, top, FountainSize, FountainSize, FountainHeight, 0, street.Wall, true, 8);
                Add("fountain", at, top + FountainHeight, WaterSize, WaterSize, Film, 0, Water, true, 8, 1, true);
            }
        }

        void Yard(CourseOpenCell open)
        {
            var rng = Random("yard", open.Key);
            var at = open.Shape.Centre;
            float side = Length, inner = side - 2 * YardEdge;
            Decal("yard", at, PavementLift, side, side, 0, street.Yard);
            if (full)
                foreach (int s in new[] { -1, 1 })
                {
                    Add("fence", at + new Flat(0, s * (side - FenceThick) * .5f), Ground, side, FenceThick, FenceHeight, 0, street.Fence, true);
                    Add("fence", at + new Flat(s * (side - FenceThick) * .5f, 0), Ground, FenceThick, side, FenceHeight, 0, street.Fence, true);
                }
            int columns = Math.Max(1, (int)((inner + BoxGap) / (BoxLength + BoxGap))), rows = Math.Max(1, (int)((inner * .6f + BoxGap) / (BoxWidth + BoxGap)));
            float spanX = columns * BoxLength + (columns - 1) * BoxGap, spanZ = rows * BoxWidth + (rows - 1) * BoxGap;
            for (int c = 0; c < columns; c++)
                for (int r = 0; r < rows; r++)
                {
                    int high = 1 + (int)(rng.NextDouble() * (full ? 3 : 2));
                    var spot = at + new Flat(-spanX * .5f + BoxLength * .5f + c * (BoxLength + BoxGap), -spanZ * .5f + BoxWidth * .5f + r * (BoxWidth + BoxGap) - inner * .15f);
                    for (int k = 0; k < high; k++)
                    {
                        uint tint = street.Containers[Math.Min(street.Containers.Count - 1, (int)(rng.NextDouble() * street.Containers.Count))];
                        Add("container", spot, Ground + k * BoxHeight, BoxLength, BoxWidth, BoxHeight, 0, tint, true);
                    }
                }
            if (!full) return;
            float legX = spanX * .5f + 1.2f, legZ = spanZ * .5f + 1.2f, shiftZ = -inner * .15f;
            foreach (int sx in new[] { -1, 1 })
                foreach (int sz in new[] { -1, 1 })
                    Add("gantry", at + new Flat(sx * legX, shiftZ + sz * legZ), Ground, LegSize, LegSize, GantryHeight, 0, street.Gantry, false);
            foreach (int sz in new[] { -1, 1 })
                Add("gantry", at + new Flat(0, shiftZ + sz * legZ), Ground + GantryHeight, 2 * legX + LegSize, BeamWidth, BeamHeight, 0, street.Gantry, true);
            Add("gantry", at + new Flat((float)(rng.NextDouble() - .5) * spanX, shiftZ), Ground + GantryHeight - .4f, 2.4f, 2 * legZ, 1.2f, 0, CourseSkyline.Shade(street.Gantry, .8), true);
        }
    }
}
