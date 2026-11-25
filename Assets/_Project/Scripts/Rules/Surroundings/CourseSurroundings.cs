using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GameStudioKit.Determinism;

namespace Wreckabulary.Rules.Surroundings
{
    public static class CourseSurroundings
    {
        public const int HighRenderers = SurroundingsBudget.HighRenderers, HighTriangles = SurroundingsBudget.HighTriangles;
        public const int LowRenderers = SurroundingsBudget.LowRenderers, LowTriangles = SurroundingsBudget.LowTriangles;
        public const int DiscSegments = 64;

        public static CourseSurroundingsResult Build(CourseSurroundingsStyle style, SurroundingsKit kit, string courseId, CourseFootprint footprint, CourseSurroundingsTier tier)
        {
            var result = Draft(style, kit, courseId, footprint, tier);
            if (!result.Valid) throw new SurroundingsException("course " + result.CourseId, result.Problems);
            return result;
        }

        public static CourseSurroundingsResult Draft(CourseSurroundingsStyle style, SurroundingsKit kit, string courseId, CourseFootprint footprint, CourseSurroundingsTier tier)
        {
            if (style == null) throw new ArgumentNullException(nameof(style));
            if (kit == null) throw new ArgumentNullException(nameof(kit));
            if (footprint == null) throw new ArgumentNullException(nameof(footprint));
            return new CourseLayoutBuilder(style, kit, courseId ?? "", footprint).Run(tier);
        }

        public static int DiscTriangles => DiscSegments;
        public static int DiscVertices => DiscSegments + 2;
    }

    enum CourseKind { Road, Crossing, RingPavement, Pavement, Lamp, Car, House, BackRow, Tree, FarTree, Scatter, Landmark, Card }

    sealed class CourseItem
    {
        public SurroundingsPlacement Placement;
        public SurroundingsPiece Piece;
        public Footprint Shape;
        public Rect Bounds;
        public CourseKind Kind;
        public double Distance;
        public float Roll;
        public int Ordinal;
    }

    sealed class CourseAsk
    {
        public string Piece;
        public Flat At;
        public double Yaw, Scale = 1;
        public float? Y;
        public CourseKind Kind;
        public string Source;
        public uint Plaster = SurroundingsSlots.NoTint, Roof = SurroundingsSlots.NoTint, Paint = SurroundingsSlots.NoTint;
        public bool Fill, OverFlats;
    }

    sealed class CourseSegment
    {
        public string Name;
        public Flat From, Dir;
        public double Length;
        public bool Ring;
        public Flat Mid => From + Dir * (Length * .5);
    }

    sealed class CourseLayoutBuilder
    {
        const float FadeStep = .01f, ApronLift = .004f, CrossingLift = .004f;
        const double LotGap = 1, Slack = 1e-6, GardenNear = 1.5, GardenFar = 4.5, ScatterSpread = 4;
        const int LandmarkTries = 6, MinCards = 8;
        const double LandmarkStep = 12;

        readonly CourseSurroundingsStyle style;
        readonly SurroundingsKit kit;
        readonly string courseId;
        readonly CourseFootprint footprint;
        readonly ulong seed;
        readonly Rect box;
        readonly Flat centre;
        readonly float groundY;
        readonly List<CourseItem> items = new List<CourseItem>();
        readonly List<string> problems = new List<string>();
        readonly List<CourseSegment> segments = new List<CourseSegment>();
        readonly Dictionary<string, (List<uint> order, int next)> palettes = new Dictionary<string, (List<uint>, int)>(StringComparer.Ordinal);
        SurroundingsPiece road, pavement;
        double roadHalf, walk;
        Rect apron;
        CourseSkylinePlanner skyline;
        List<CourseAsk> cards;
        HashSet<int> thinnedCards = new HashSet<int>();

        public CourseLayoutBuilder(CourseSurroundingsStyle style, SurroundingsKit kit, string courseId, CourseFootprint footprint)
        {
            this.style = style;
            this.kit = kit;
            this.courseId = courseId;
            this.footprint = footprint;
            seed = Hash64.Of("course:" + courseId + ":" + footprint.Key);
            box = footprint.Rect;
            centre = box.Centre;
            groundY = footprint.Floor - style.GroundDrop;
        }

        public CourseSurroundingsResult Run(CourseSurroundingsTier tier)
        {
            foreach (string id in style.Pieces)
                if (!kit.Has(id)) Problem("kit", $"the style names \"{id}\", which the kit does not have");
            if (problems.Count == 0)
            {
                road = kit.Piece(style.Streets.Road);
                pavement = kit.Piece(style.Streets.Pavement);
                LayStreets();
                foreach (var s in segments.Where(s => s.Ring)) Frontage(s, OuterSide(s));
                foreach (var s in segments.Where(s => !s.Ring)) { Frontage(s, s.Dir.Left); Frontage(s, s.Dir.Right); }
                foreach (var s in segments.Where(s => s.Ring)) BackRow(s, OuterSide(s));
                foreach (var s in segments) StreetLife(s);
                PlaceGardens();
                PlaceLandmark();
                PlanSkyline();
                PlaceScatter();
                PlaceCards();
            }
            return Finish(tier);
        }

        Pcg32 Random(string purpose, long index = 0) => Pcg32.ForStream(seed, purpose, index);

        void Problem(string rule, string detail)
        {
            string line = $"{rule}: {detail}";
            if (!problems.Contains(line)) problems.Add(line);
        }

        uint NextTint(string palette)
        {
            if (!palettes.TryGetValue(palette, out var state))
            {
                var order = style.PaletteOf(palette).ToList();
                var rng = Random("palette:" + palette);
                rng.Shuffle(order);
                state = (order, 0);
            }
            if (state.order.Count == 0) return SurroundingsSlots.NoTint;
            uint tint = state.order[state.next % state.order.Count];
            palettes[palette] = (state.order, state.next + 1);
            return tint;
        }

        static float Q(double value, double step) => (float)(Math.Round(value / step, MidpointRounding.AwayFromZero) * step);

        static double Long(SurroundingsPiece p) => Math.Max(p.Width, p.Depth);
        static double Short(SurroundingsPiece p) => Math.Min(p.Width, p.Depth);
        static double YawAlong(SurroundingsPiece piece, Flat dir) => piece.Width >= piece.Depth ? dir.Left.Yaw : dir.Yaw;

        static Footprint ShapeOf(SurroundingsPiece piece, Flat at, double yaw, double scale) =>
            new Footprint(at.X, at.Z, piece.Width * scale, piece.Depth * scale, yaw);

        SurroundingsRing RingOf(double distance) =>
            distance < style.NearReach ? SurroundingsRing.Near : distance < style.MidReach ? SurroundingsRing.Mid : SurroundingsRing.Far;

        SurroundingsPiece Detailed(SurroundingsPiece piece, double distance) =>
            distance >= style.NearReach && piece.Far != null && kit.Has(piece.Far) ? kit.Piece(piece.Far) : piece;

        double Distance(Footprint shape) => shape.Distance(box);

        CourseItem Place(CourseAsk ask)
        {
            var piece = kit.Piece(ask.Piece);
            double yaw = Shapes.Wrap360(ask.Yaw);
            var at = new Flat(Q(ask.At.X, .001), Q(ask.At.Z, .001));
            float scale = Q(ask.Scale, .001);
            var shape = ShapeOf(piece, at, Q(yaw, .01), scale);
            double distance = Distance(shape);
            var detailed = Detailed(piece, distance);
            if (detailed != piece)
            {
                piece = detailed;
                shape = ShapeOf(piece, at, Q(yaw, .01), scale);
                distance = Distance(shape);
            }
            var item = new CourseItem
            {
                Piece = piece, Shape = shape, Bounds = shape.Bounds(), Kind = ask.Kind, Distance = distance, Ordinal = items.Count,
                Placement = new SurroundingsPlacement
                {
                    Piece = piece.Id, X = (float)at.X, Z = (float)at.Z, Yaw = Q(yaw, .01), Scale = scale, Y = Q(ask.Y ?? groundY, .001),
                    Ring = RingOf(distance), Card = piece.IsCard, PlasterTint = ask.Plaster, RoofTint = ask.Roof, PaintTint = ask.Paint, Source = ask.Source,
                },
            };
            if (distance < style.Clear - Slack)
            {
                if (!ask.Fill) Problem("clear", $"{piece.Id} ({ask.Source}) is {distance:0.###} m from the course, inside the {style.Clear} m margin");
                return null;
            }
            if (ask.Fill && Blocked(item, ask)) return null;
            item.Roll = Random("low", item.Ordinal).NextFloat();
            items.Add(item);
            return item;
        }

        bool Blocked(CourseItem item, CourseAsk ask)
        {
            if (item.Kind == CourseKind.Card) return false;
            if (skyline != null && skyline.Covers(item.Shape, item.Bounds)) return true;
            foreach (var other in items)
            {
                if (other.Kind == CourseKind.Card || !other.Bounds.Overlaps(item.Bounds)) continue;
                if (other.Piece.IsFlat && (item.Piece.IsFlat || ask.OverFlats)) continue;
                if (Shelters(item.Kind, other.Kind)) continue;
                if (item.Shape.Overlaps(other.Shape)) return true;
            }
            return false;
        }

        static bool Shelters(CourseKind a, CourseKind b) =>
            a == CourseKind.Tree && b == CourseKind.Car || a == CourseKind.Car && b == CourseKind.Tree;

        static IEnumerable<double> Tiles(double from, double to, double length)
        {
            double span = to - from;
            if (span < length * .95 || length <= 0) yield break;
            int n = (int)Math.Ceiling(span / length - 1e-6);
            for (int k = 0; k < n - 1; k++) yield return from + (k + .5) * length;
            yield return to - length * .5;
        }

        double Half(double span, double tile)
        {
            double raw = span * .5 + style.Clear + walk + roadHalf;
            double inner = Math.Ceiling((2 * raw - 2 * roadHalf) / tile - 1e-9) * tile;
            return inner * .5 + roadHalf;
        }

        void LayStreets()
        {
            roadHalf = Short(road) * .5;
            walk = Short(pavement);
            double tile = Long(road);
            double reach = Math.Ceiling(style.Streets.Reach / tile - 1e-9) * tile;
            double halfX = Half(footprint.Width, tile), halfZ = Half(footprint.Depth, tile);
            double west = centre.X - halfX, east = centre.X + halfX, south = centre.Z - halfZ, north = centre.Z + halfZ;
            apron = new Rect(west + roadHalf, south + roadHalf, east - roadHalf, north - roadHalf);
            var alongX = new Flat(1, 0);
            var alongZ = new Flat(0, 1);
            foreach (var (z, name) in new[] { (south, "south"), (north, "north") })
            {
                Segment(name + " west", new Flat(west - roadHalf - reach, z), alongX, reach, false);
                Segment(name, new Flat(west + roadHalf, z), alongX, east - west - 2 * roadHalf, true);
                Segment(name + " east", new Flat(east + roadHalf, z), alongX, reach, false);
            }
            foreach (var (x, name) in new[] { (west, "west"), (east, "east") })
            {
                Segment(name + " south", new Flat(x, south - roadHalf - reach), alongZ, reach, false);
                Segment(name, new Flat(x, south + roadHalf), alongZ, north - south - 2 * roadHalf, true);
                Segment(name + " north", new Flat(x, north + roadHalf), alongZ, reach, false);
            }
            foreach (var s in segments) Pave(s, tile);
            foreach (var x in new[] { west, east })
                foreach (var z in new[] { south, north })
                    Place(new CourseAsk { Piece = road.Id, At = new Flat(x, z), Yaw = YawAlong(road, alongX), Y = groundY + CrossingLift, Kind = CourseKind.Crossing, Source = "crossing" });
        }

        void Segment(string name, Flat from, Flat dir, double length, bool ring)
        {
            if (length <= Slack) return;
            segments.Add(new CourseSegment { Name = name, From = from, Dir = dir, Length = length, Ring = ring });
        }

        void Pave(CourseSegment s, double tile)
        {
            foreach (double t in Tiles(0, s.Length, tile))
                Place(new CourseAsk { Piece = road.Id, At = s.From + s.Dir * t, Yaw = YawAlong(road, s.Dir), Kind = CourseKind.Road, Source = "street " + s.Name });
            foreach (var (lateral, left) in new[] { (s.Dir.Left, true), (s.Dir.Right, false) })
            {
                var origin = s.From + lateral * (roadHalf + walk * .5);
                double yaw = YawAlong(pavement, s.Dir) + (left ? 180 : 0);
                foreach (double t in Tiles(0, s.Length, Long(pavement)))
                    Place(new CourseAsk
                    {
                        Piece = pavement.Id, At = origin + s.Dir * t, Yaw = yaw, Kind = s.Ring ? CourseKind.RingPavement : CourseKind.Pavement,
                        Source = $"street {s.Name} pavement {(left ? "left" : "right")}",
                    });
            }
        }

        Flat OuterSide(CourseSegment s) => (s.Mid - centre).Dot(s.Dir.Left) > 0 ? s.Dir.Left : s.Dir.Right;

        string SideName(CourseSegment s, Flat normal) => normal.Dot(s.Dir.Left) > 0 ? "left" : "right";

        void Frontage(CourseSegment s, Flat normal)
        {
            var houses = style.Houses;
            string side = SideName(s, normal);
            var rng = Random($"frontage:{s.Name}:{side}");
            var edge = s.From + normal * (roadHalf + walk);
            var front = normal * -1;
            double t = walk + LotGap, end = s.Length - walk - LotGap;
            while (true)
            {
                var piece = kit.Piece(houses.Pieces[rng.Range(0, houses.Pieces.Count)]);
                double setback = rng.Range(houses.Setback.min, houses.Setback.max), gap = rng.Range(houses.Gap.min, houses.Gap.max);
                if (t + piece.Width > end + Slack) break;
                var at = edge + s.Dir * (t + piece.Width * .5) + normal * (setback + piece.Depth * .5);
                Place(new CourseAsk
                {
                    Piece = piece.Id, At = at, Yaw = front.Yaw, Kind = CourseKind.House, Fill = true, Source = $"street {s.Name} houses {side}",
                    Plaster = NextTint("plaster"), Roof = NextTint("roof"),
                });
                t += piece.Width + gap;
            }
        }

        SurroundingsPiece StandIn(string id)
        {
            var piece = kit.Piece(id);
            return piece.Far != null && kit.Has(piece.Far) ? kit.Piece(piece.Far) : piece;
        }

        void BackRow(CourseSegment s, Flat normal)
        {
            var houses = style.Houses;
            string side = SideName(s, normal);
            var rng = Random($"backrow:{s.Name}:{side}");
            double deepest = houses.Pieces.Max(id => kit.Piece(id).Depth);
            var edge = s.From + normal * (roadHalf + walk + houses.Setback.max + deepest + houses.Garden);
            double t = walk + LotGap, end = s.Length - walk - LotGap;
            while (true)
            {
                var piece = StandIn(houses.Pieces[rng.Range(0, houses.Pieces.Count)]);
                double scale = rng.Range(houses.BackScale.min, houses.BackScale.max), gap = rng.Range(houses.BackGap.min, houses.BackGap.max);
                double width = piece.Width * scale;
                if (t + width > end + Slack) break;
                var at = edge + s.Dir * (t + width * .5) + normal * (piece.Depth * scale * .5);
                Place(new CourseAsk
                {
                    Piece = piece.Id, At = at, Yaw = normal.Yaw, Scale = scale, Kind = CourseKind.BackRow, Fill = true, Source = $"street {s.Name} back row",
                    Plaster = NextTint("plaster"), Roof = NextTint("roof"),
                });
                t += width + gap;
            }
        }

        void StreetLife(CourseSegment s)
        {
            var streets = style.Streets;
            var outer = OuterSide(s);
            if (streets.Lamp != null)
                foreach (var lateral in s.Ring ? new[] { s.Dir.Left, s.Dir.Right } : new[] { s.Dir.Right })
                    for (double t = streets.LampEvery * .5; t <= s.Length - LotGap; t += streets.LampEvery)
                    {
                        var at = s.From + s.Dir * t + lateral * (roadHalf + streets.LampInset);
                        if (Distance(ShapeOf(kit.Piece(streets.Lamp), at, 0, 1)) >= style.NearReach) continue;
                        Place(new CourseAsk { Piece = streets.Lamp, At = at, Yaw = (lateral * -1).Yaw, Kind = CourseKind.Lamp, Fill = true, OverFlats = true, Source = $"street {s.Name} lamps" });
                    }
            if (!s.Ring) return;
            if (style.Houses.Trees.Count > 0)
            {
                var rng = Random($"street trees:{s.Name}");
                for (double t = streets.LampEvery; t <= s.Length - streets.LampEvery * .5; t += streets.LampEvery)
                {
                    var piece = style.Houses.Trees[rng.Range(0, style.Houses.Trees.Count)];
                    float scale = rng.Range(.85f, 1.1f);
                    Place(new CourseAsk
                    {
                        Piece = piece, At = s.From + s.Dir * t + outer * (roadHalf + walk * .5), Yaw = rng.Range(0f, 360f), Scale = scale,
                        Kind = CourseKind.Tree, Fill = true, OverFlats = true, Source = $"street {s.Name} trees",
                    });
                }
            }
            if (streets.Cars.Count == 0) return;
            int k = 0;
            for (double t = 3; t <= s.Length - 3; t += streets.ParkEvery, k++)
            {
                var rng = Random($"parking:{s.Name}", k);
                bool parked = rng.NextFloat() < streets.ParkChance;
                var car = kit.Piece(streets.Cars[rng.Range(0, streets.Cars.Count)]);
                if (!parked) continue;
                bool left = outer.Dot(s.Dir.Left) > 0;
                Place(new CourseAsk
                {
                    Piece = car.Id, At = s.From + s.Dir * t + outer * (roadHalf - streets.Kerb - Short(car) * .5), Yaw = s.Dir.Yaw + (left ? 180 : 0),
                    Kind = CourseKind.Car, Fill = true, OverFlats = true, Paint = NextTint("paint"), Source = $"street {s.Name} parking",
                });
            }
        }

        void PlaceGardens()
        {
            var trees = style.Houses.Trees;
            if (trees.Count == 0) return;
            foreach (var house in items.Where(i => i.Kind == CourseKind.House).ToList())
            {
                var rng = Random("garden", house.Ordinal);
                if (rng.NextFloat() >= style.Houses.TreeChance) continue;
                var back = Flat.Heading(house.Placement.Yaw) * -1;
                var along = back.Right;
                int count = rng.Range(1, 3);
                for (int n = 0; n < count; n++)
                {
                    var piece = trees[rng.Range(0, trees.Count)];
                    var at = house.Shape.Centre + back * (house.Shape.HalfDepth + rng.Range((float)GardenNear, (float)GardenFar))
                        + along * rng.Range((float)-house.Shape.HalfWidth, (float)house.Shape.HalfWidth);
                    Place(new CourseAsk { Piece = piece, At = at, Yaw = rng.Range(0f, 360f), Scale = rng.Range(.9f, 1.15f), Kind = CourseKind.Tree, Fill = true, Source = "garden trees" });
                }
            }
        }

        void PlaceLandmark()
        {
            var mark = style.Landmark;
            if (mark == null) return;
            for (int attempt = 0; attempt < LandmarkTries; attempt++)
            {
                var at = new Flat(centre.X, box.MaxZ + mark.Reach + attempt * LandmarkStep);
                if (Place(new CourseAsk { Piece = mark.Piece, At = at, Yaw = mark.Yaw, Kind = CourseKind.Landmark, Fill = true, Source = "landmark" }) != null) return;
            }
        }

        void PlaceScatter()
        {
            var s = style.Scatter;
            double half = Math.Max(footprint.Width, footprint.Depth) * .5 + s.To;
            int cells = (int)Math.Ceiling(half / s.Cell);
            for (int i = -cells; i <= cells; i++)
                for (int j = -cells; j <= cells; j++)
                {
                    var rng = Random("far", (i + 10000L) * 100000L + (j + 10000L));
                    var at = centre + new Flat(i * s.Cell + (rng.NextFloat() - .5) * s.Jitter, j * s.Cell + (rng.NextFloat() - .5) * s.Jitter);
                    double roll = rng.NextFloat();
                    if (roll < s.HouseChance) ScatterHouse(s, rng, at);
                    else if (roll < s.HouseChance + s.TreeChance && s.Trees.Count > 0) ScatterTrees(s, rng, at);
                }
        }

        bool InBand(CourseScatterSpec s, SurroundingsPiece piece, Flat at, double yaw, double scale)
        {
            double d = Distance(ShapeOf(piece, at, yaw, scale));
            return d >= s.From && d <= s.To;
        }

        void ScatterHouse(CourseScatterSpec s, Pcg32 rng, Flat at)
        {
            var piece = StandIn(style.Houses.Pieces[rng.Range(0, style.Houses.Pieces.Count)]);
            double yaw = Math.Round((centre - at).Yaw / 90) * 90;
            double scale = rng.Range(s.Scale.min, s.Scale.max);
            if (!InBand(s, piece, at, yaw, scale)) return;
            Place(new CourseAsk
            {
                Piece = piece.Id, At = at, Yaw = yaw, Scale = scale, Kind = CourseKind.Scatter, Fill = true, Source = "far houses",
                Plaster = NextTint("plaster"), Roof = NextTint("roof"),
            });
        }

        void ScatterTrees(CourseScatterSpec s, Pcg32 rng, Flat at)
        {
            int count = rng.Range(1, 4);
            for (int t = 0; t < count; t++)
            {
                var piece = kit.Piece(s.Trees[rng.Range(0, s.Trees.Count)]);
                var spot = at + new Flat(rng.Range((float)-ScatterSpread, (float)ScatterSpread), rng.Range((float)-ScatterSpread, (float)ScatterSpread));
                float scale = rng.Range(.85f, 1.2f);
                if (!InBand(s, piece, spot, 0, scale)) continue;
                Place(new CourseAsk { Piece = piece.Id, At = spot, Yaw = rng.Range(0f, 360f), Scale = scale, Kind = CourseKind.FarTree, Fill = true, Source = "far trees" });
            }
        }

        List<CourseAsk> CardAsks()
        {
            var asks = new List<CourseAsk>();
            for (int i = 0; i < style.Cards.Count; i++)
            {
                var ring = style.Cards[i];
                double radius = footprint.HalfDiagonal + ring.Reach;
                int count = Math.Max(MinCards, (int)Math.Round(2 * Math.PI * radius / ring.Spacing));
                double step = 360.0 / count;
                double phase = Random($"cards:{i}").NextFloat() * step;
                for (int k = 0; k < count; k++)
                {
                    var rng = Random($"cards:{i}", k + 1);
                    double r = radius + (rng.NextFloat() - .5) * 2 * ring.Jitter;
                    var at = centre + Flat.Heading(phase + k * step) * r;
                    asks.Add(new CourseAsk { Piece = ring.Piece, At = at, Yaw = (centre - at).Yaw, Scale = rng.Range(ring.Scale.min, ring.Scale.max), Kind = CourseKind.Card, Source = $"cards {i}" });
                }
            }
            return asks;
        }

        Footprint CardShape(CourseAsk ask) =>
            ShapeOf(kit.Piece(ask.Piece), new Flat(Q(ask.At.X, .001), Q(ask.At.Z, .001)), Q(Shapes.Wrap360(ask.Yaw), .01), Q(ask.Scale, .001));

        void PlanSkyline()
        {
            if (style.Skyline == null) return;
            cards = CardAsks();
            skyline = new CourseSkylinePlanner(style.Skyline, seed, footprint, groundY, style.Clear, courseId);
            skyline.Plan(TownBlocks);
            var sources = cards.Select((c, k) => $"{c.Source} #{k}").ToList();
            thinnedCards = skyline.Thin(cards.Select(c => c.Piece).ToList(), sources, cards.Select(CardShape).ToList());
        }

        bool TownBlocks(Footprint shape, Rect bounds)
        {
            foreach (var other in items)
                if (other.Kind != CourseKind.Card && other.Bounds.Overlaps(bounds) && shape.Overlaps(other.Shape)) return true;
            return false;
        }

        void PlaceCards()
        {
            var asks = cards ?? CardAsks();
            for (int k = 0; k < asks.Count; k++)
                if (!thinnedCards.Contains(k)) Place(asks[k]);
        }

        List<SurroundingsGround> Ground()
        {
            var g = style.Ground;
            var zones = new List<SurroundingsGround>();
            int fades = g.Fades.Count;
            double halfDiagonal = footprint.HalfDiagonal;
            zones.Add(Disc(halfDiagonal + g.Reach, g.Slot, g.Tint, groundY - FadeStep * fades, "ground"));
            if (g.CityReach > 0) zones.Add(Disc(halfDiagonal + g.CityReach, g.CitySlot, g.CityTint, groundY - FadeStep * (fades - .5f), "city ground"));
            for (int k = 0; k < fades; k++)
                zones.Add(Disc(halfDiagonal + g.Fades[k].reach, g.Slot, g.Fades[k].tint, groundY - FadeStep * (fades - 1 - k), $"ground fade {k}"));
            if (segments.Count > 0)
                zones.Add(new SurroundingsGround
                {
                    Shape = SurroundingsGroundShape.Rect, Slot = g.ApronSlot, Tint = g.ApronTint, Y = Q(groundY + ApronLift, .001), Source = "apron",
                    CentreX = Q(apron.Centre.X, .001), CentreZ = Q(apron.Centre.Z, .001),
                    MinX = Q(apron.MinX, .001), MinZ = Q(apron.MinZ, .001), MaxX = Q(apron.MaxX, .001), MaxZ = Q(apron.MaxZ, .001),
                });
            return zones.OrderBy(z => z.Y).ToList();
        }

        SurroundingsGround Disc(double radius, string slot, uint tint, float y, string source) => new SurroundingsGround
        {
            Shape = SurroundingsGroundShape.Disc, Slot = slot, Tint = tint, Y = Q(y, .001), Source = source,
            CentreX = Q(centre.X, .001), CentreZ = Q(centre.Z, .001), Radius = Q(radius, .001),
            MinX = Q(centre.X - radius, .001), MinZ = Q(centre.Z - radius, .001), MaxX = Q(centre.X + radius, .001), MaxZ = Q(centre.Z + radius, .001),
        };

        static int Priority(CourseKind kind)
        {
            switch (kind)
            {
                case CourseKind.Scatter: case CourseKind.FarTree: return 1;
                case CourseKind.BackRow: return 2;
                case CourseKind.Car: return 3;
                case CourseKind.Tree: case CourseKind.Lamp: return 4;
                case CourseKind.Pavement: return 5;
                case CourseKind.Landmark: return 6;
                case CourseKind.House: return 7;
                case CourseKind.RingPavement: return 8;
                case CourseKind.Card: return 9;
                default: return Essential;
            }
        }

        const int Essential = 10;

        List<CourseItem> ForTier(CourseSurroundingsTier tier)
        {
            if (tier == CourseSurroundingsTier.High) return items.ToList();
            var low = style.Low;
            var kept = new List<CourseItem>();
            foreach (var item in items)
                switch (item.Kind)
                {
                    case CourseKind.Pavement: case CourseKind.Lamp: case CourseKind.Car: case CourseKind.Landmark: break;
                    case CourseKind.House: kept.Add(Swapped(item)); break;
                    case CourseKind.BackRow: if (item.Roll < low.KeepBackRow) kept.Add(item); break;
                    case CourseKind.Tree: if (item.Roll < low.KeepTrees) kept.Add(Swapped(item)); break;
                    case CourseKind.FarTree: if (item.Roll < low.KeepTrees) kept.Add(item); break;
                    case CourseKind.Scatter: if (item.Roll < low.KeepScatter) kept.Add(item); break;
                    default: kept.Add(item); break;
                }
            return kept;
        }

        CourseItem Swapped(CourseItem item)
        {
            var piece = item.Piece;
            if (piece.Far == null || !kit.Has(piece.Far)) return item;
            var far = kit.Piece(piece.Far);
            var p = item.Placement;
            var shape = new Footprint(item.Shape.Centre.X, item.Shape.Centre.Z, far.Width * p.Scale, far.Depth * p.Scale, item.Shape.Yaw);
            return new CourseItem
            {
                Piece = far, Shape = shape, Bounds = shape.Bounds(), Kind = item.Kind, Distance = Distance(shape), Roll = item.Roll, Ordinal = item.Ordinal,
                Placement = new SurroundingsPlacement
                {
                    Piece = far.Id, X = p.X, Y = p.Y, Z = p.Z, Yaw = p.Yaw, Scale = p.Scale, Ring = p.Ring, Card = far.IsCard,
                    PlasterTint = p.PlasterTint, RoofTint = p.RoofTint, PaintTint = p.PaintTint, Source = p.Source,
                },
            };
        }

        CourseSurroundingsResult Finish(CourseSurroundingsTier tier)
        {
            bool high = tier == CourseSurroundingsTier.High;
            var skipped = high ? new List<string>() : style.Low.SkipSlots.ToList();
            int rendererLimit = high ? CourseSurroundings.HighRenderers : CourseSurroundings.LowRenderers;
            int triangleLimit = high ? CourseSurroundings.HighTriangles : CourseSurroundings.LowTriangles;
            var ground = Ground();
            var kept = ForTier(tier);
            foreach (var item in kept)
                if (item.Distance < style.Clear - Slack) Problem("clear", $"{item.Piece.Id} ({item.Placement.Source}) sits {item.Distance:0.###} m from the course");
            int triangles = Groups(kept, ground, tier, skipped).Sum(g => g.Triangles);
            int trimmed = 0;
            if (triangles > triangleLimit)
                foreach (var item in kept.OrderBy(i => Priority(i.Kind)).ThenByDescending(i => i.Distance).ThenByDescending(i => i.Ordinal).ToList())
                {
                    if (triangles <= triangleLimit || Priority(item.Kind) >= Essential) break;
                    kept.Remove(item);
                    triangles -= item.Piece.Slots.Where(slot => !skipped.Contains(slot)).Sum(slot => item.Piece.TrisIn(slot));
                    trimmed++;
                }
            var groups = Groups(kept, ground, tier, skipped);
            var towers = skyline != null ? skyline.Finish(tier, problems) : CourseSkylineResult.None(tier);
            var result = new CourseSurroundingsResult
            {
                CourseId = courseId, StyleId = style.Id, Name = style.Name, Tier = tier, Footprint = footprint, Clear = style.Clear, GroundY = Q(groundY, .001),
                Placements = kept.Select(i => i.Placement).ToList(), Distances = kept.Select(i => (float)i.Distance).ToList(),
                Ground = ground, Groups = groups, SkippedSlots = skipped, Trimmed = trimmed, RendererLimit = rendererLimit, TriangleLimit = triangleLimit,
                Skyline = towers,
            };
            if (result.Renderers > rendererLimit) Problem("budget", $"{tier}: {result.Renderers} renderers, at most {rendererLimit}");
            if (result.Triangles > triangleLimit) Problem("budget", $"{tier}: {result.Triangles} triangles, at most {triangleLimit}");
            result.Problems = problems.ToList();
            result.Canonical = Canonical(result);
            result.Hash = Hash64.ToHex(Hash64.Of(result.Canonical));
            return result;
        }

        List<SurroundingsGroup> Groups(List<CourseItem> kept, List<SurroundingsGround> ground, CourseSurroundingsTier tier, List<string> skipped)
        {
            var groups = new Dictionary<(SurroundingsRing, string), SurroundingsGroup>();
            SurroundingsGroup Group(SurroundingsRing band, string slot)
            {
                if (!groups.TryGetValue((band, slot), out var g)) groups[(band, slot)] = g = new SurroundingsGroup { Ring = band, Slot = slot };
                return g;
            }
            foreach (var item in kept)
            {
                var band = tier == CourseSurroundingsTier.High && item.Placement.Ring == SurroundingsRing.Near ? SurroundingsRing.Near : SurroundingsRing.Far;
                foreach (string slot in item.Piece.Slots)
                {
                    if (skipped.Contains(slot)) continue;
                    var g = Group(band, slot);
                    g.Pieces++;
                    g.Triangles += item.Piece.TrisIn(slot);
                    g.Vertices += item.Piece.VertsIn(slot);
                }
            }
            foreach (var zone in ground)
            {
                var g = Group(SurroundingsRing.Far, zone.Slot);
                bool disc = zone.Shape == SurroundingsGroundShape.Disc;
                g.Pieces++;
                g.Triangles += disc ? CourseSurroundings.DiscTriangles : 2;
                g.Vertices += disc ? CourseSurroundings.DiscVertices : 4;
            }
            return groups.Values.OrderBy(g => g.Ring).ThenBy(g => g.Slot, StringComparer.Ordinal).ToList();
        }

        static string Canonical(CourseSurroundingsResult r)
        {
            var text = new StringBuilder();
            text.Append("course ").Append(r.CourseId).Append('\n');
            text.Append("style ").Append(r.StyleId).Append('\n');
            text.Append("tier ").Append(r.Tier).Append('\n');
            text.Append("footprint ").Append(r.Footprint.Key).Append('\n');
            foreach (var g in r.Ground)
                text.Append("ground ").Append(g.Shape).Append(' ').Append(g.Slot).Append(' ')
                    .Append(Join(g.Y, g.CentreX, g.CentreZ, g.Radius, g.MinX, g.MinZ, g.MaxX, g.MaxZ)).Append(' ').Append(Hex(g.Tint)).Append('\n');
            foreach (var p in r.Placements)
                text.Append("place ").Append(p.Ring).Append(' ').Append(p.Piece).Append(' ').Append(Join(p.X, p.Y, p.Z, p.Yaw, p.Scale)).Append(' ')
                    .Append(Hex(p.PlasterTint)).Append(' ').Append(Hex(p.RoofTint)).Append(' ').Append(Hex(p.PaintTint)).Append(' ').Append(p.Source).Append('\n');
            if (!r.Skyline.Empty) text.Append("skyline ").Append(r.Skyline.Hash).Append('\n');
            return text.ToString();
        }

        static string Join(params float[] values) => string.Join(" ", values.Select(x => (x == 0 ? 0.0 : x).ToString("0.###", CultureInfo.InvariantCulture)));

        static string Hex(uint rgb) => rgb.ToString("x6", CultureInfo.InvariantCulture);
    }
}
