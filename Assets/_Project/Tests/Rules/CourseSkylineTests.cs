using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Surroundings;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class CourseSkylineTests
    {
        const string Folder = "Assets/_Project/Data/Surroundings";
        const string StyleFile = "Courses/course_city.json";
        const string DepotId = "wordbound-parcel-service-v2";
        const double Tolerance = 1e-3, EyeHeight = 1.7;
        const float LowestBody = 14f, TallestBody = 200f, TopAboveBody = 45f, Downtown = 120f;
        const float SupertallLow = 220f, SupertallHigh = 260f, NeedleLow = 180f, NeedleHigh = 215f, ClockLow = 44f, ClockHigh = 58f;

        static readonly string[] Shapes = { "arena", "depot", "flagship", "offset", "ribbon" };
        static readonly CourseSurroundingsTier[] Tiers = { CourseSurroundingsTier.High, CourseSurroundingsTier.Low };
        static readonly string[] Details = { "plaza", "shop", "awning", "sign", "pane", "pier", "fin", "mullion", "parapet", "hut", "stand", "tank", "ac", "antenna", "chimney", "hand", "skeleton", "crane" };
        static readonly Dictionary<(string, string, CourseSurroundingsTier), CourseSurroundingsResult> built = new Dictionary<(string, string, CourseSurroundingsTier), CourseSurroundingsResult>();
        static SurroundingsKit kit;
        static CourseSurroundingsStyle style;

        static string Read(string file) => File.ReadAllText(Path.Combine(TestData.Root, Folder, file));

        static SurroundingsKit Kit()
        {
            if (kit != null) return kit;
            var loaded = new SurroundingsKit();
            foreach (string file in Directory.GetFiles(Path.Combine(TestData.Root, Folder), "kit_*.json").OrderBy(f => f, StringComparer.Ordinal))
                loaded.Add(File.ReadAllText(file), Path.GetFileName(file));
            return kit = loaded;
        }

        static CourseSurroundingsStyle Style() => style ??= CourseSurroundingsStyle.FromJson(Read(StyleFile), StyleFile);

        static string WithoutSkyline(string json)
        {
            int key = json.IndexOf("\"skyline\"", StringComparison.Ordinal);
            Assert.Greater(key, 0, "the style has a skyline block");
            return json.Substring(0, json.LastIndexOf(',', key)) + "\n}\n";
        }

        static CourseFootprint Footprint(string shape)
        {
            switch (shape)
            {
                case "arena": return new CourseFootprint(-10, -10, 10, 10, 0, 4);
                case "depot": return new CourseFootprint(-36, -20, 36, 20, 0, 7.2f);
                case "flagship": return new CourseFootprint(-48, -48, 48, 24, 0, 30);
                case "offset": return new CourseFootprint(130.5f, -260.25f, 171.25f, -228f, 12.5f, 18f);
                default: return new CourseFootprint(-70, -6, 70, 6, 0, 3);
            }
        }

        static CourseSurroundingsResult Build(string shape, CourseSurroundingsTier tier, string id = DepotId)
        {
            if (built.TryGetValue((shape, id, tier), out var cached)) return cached;
            try { return built[(shape, id, tier)] = CourseSurroundings.Build(Style(), Kit(), id, Footprint(shape), tier); }
            catch (SurroundingsException e) { Assert.Fail(e.Message); return null; }
        }

        static IEnumerable<TestCaseData> Cases() =>
            from shape in Shapes from tier in Tiers select new TestCaseData(shape, tier).SetName($"{shape} {tier}");

        static (double x, double z)[] Corners(double x, double z, double width, double depth, double yawDegrees)
        {
            double yaw = yawDegrees * Math.PI / 180, hw = width * .5, hd = depth * .5;
            double ax = Math.Sin(yaw), az = Math.Cos(yaw), rx = az, rz = -ax;
            return new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }
                .Select(c => (x + rx * hw * c.Item1 + ax * hd * c.Item2, z + rz * hw * c.Item1 + az * hd * c.Item2)).ToArray();
        }

        static (double x, double z)[] Corners(SurroundingsPlacement p)
        {
            var piece = Kit().Piece(p.Piece);
            return Corners(p.X, p.Z, piece.Width * p.Scale, piece.Depth * p.Scale, p.Yaw);
        }

        static (double x, double z)[] Lot(CourseTower t) => Corners(t.X, t.Z, t.LotWidth, t.LotDepth, t.Yaw);

        static bool InLot(CourseTower t, (double x, double z) point)
        {
            double yaw = t.Yaw * Math.PI / 180, dx = point.x - t.X, dz = point.z - t.Z;
            double along = dx * Math.Sin(yaw) + dz * Math.Cos(yaw), across = dx * Math.Cos(yaw) - dz * Math.Sin(yaw);
            return Math.Abs(across) <= t.LotWidth * .5 + Tolerance && Math.Abs(along) <= t.LotDepth * .5 + Tolerance;
        }

        static bool Overlap((double x, double z)[] a, (double x, double z)[] b, double pad = 0)
        {
            foreach (var polygon in new[] { a, b })
                for (int i = 0; i < polygon.Length; i++)
                {
                    var p = polygon[i];
                    var q = polygon[(i + 1) % polygon.Length];
                    double nx = -(q.z - p.z), nz = q.x - p.x, length = Math.Sqrt(nx * nx + nz * nz);
                    if (length < 1e-12) continue;
                    nx /= length;
                    nz /= length;
                    double aMin = a.Min(v => v.x * nx + v.z * nz), aMax = a.Max(v => v.x * nx + v.z * nz);
                    double bMin = b.Min(v => v.x * nx + v.z * nz), bMax = b.Max(v => v.x * nx + v.z * nz);
                    if (aMax + pad <= bMin + Tolerance || bMax + pad <= aMin + Tolerance) return false;
                }
            return true;
        }

        static (double x, double z)[] Grown(CourseFootprint f, double by) =>
            new[] { (f.MinX - by, (double)f.MinZ - by), (f.MaxX + by, f.MinZ - by), (f.MaxX + by, f.MaxZ + by), (f.MinX - by, f.MaxZ + by) };

        static double Coverage(CourseSurroundingsResult result, double elevation)
        {
            var f = result.Footprint;
            double cx = f.CentreX, cz = f.CentreZ, eye = f.Floor + EyeHeight;
            var filled = new bool[360];
            foreach (var t in result.Skyline.Towers)
            {
                double distance = Math.Sqrt((t.X - cx) * (t.X - cx) + (t.Z - cz) * (t.Z - cz));
                if (Math.Atan2(t.Top - eye, distance) * 180 / Math.PI < elevation) continue;
                double heading = Math.Atan2(t.X - cx, t.Z - cz) * 180 / Math.PI;
                var turns = Lot(t).Select(c => ((Math.Atan2(c.x - cx, c.z - cz) * 180 / Math.PI - heading + 540) % 360) - 180).ToList();
                for (int a = (int)Math.Ceiling(heading + turns.Min()); a <= heading + turns.Max(); a++) filled[((a % 360) + 360) % 360] = true;
            }
            return filled.Count(x => x) / 360.0;
        }

        [Test]
        public void TheStyleBringsASkylineThatOnlyUsesKitSlots()
        {
            Assert.IsTrue(Style().HasSkyline);
            var slots = new HashSet<string>(Kit().Pieces.SelectMany(p => p.Slots));
            foreach (string slot in CourseSkyline.Slots.Where(s => s != CourseSkyline.LitSlot)) Assert.IsTrue(slots.Contains(slot), $"{slot} has no backdrop material in the kit");
            Assert.AreEqual("bk_glass_lit", CourseSkyline.LitSlot, "lit panes use the baked warm glass slot every course kit carries");
            Assert.IsFalse(CourseSurroundingsStyle.FromJson(WithoutSkyline(Read(StyleFile)), "bare.json").HasSkyline);
        }

        [Test]
        public void TheSameCourseAndBoundsAlwaysGiveTheSameSkyline()
        {
            foreach (var tier in Tiers)
            {
                var first = CourseSurroundings.Build(Style(), Kit(), DepotId, Footprint("depot"), tier);
                var second = CourseSurroundings.Build(Style(), Kit(), DepotId, Footprint("depot"), tier);
                Assert.IsFalse(first.Skyline.Empty, tier.ToString());
                Assert.AreEqual(first.Skyline.Hash, second.Skyline.Hash, tier.ToString());
                Assert.AreEqual(first.Skyline.Canonical, second.Skyline.Canonical, tier.ToString());
                StringAssert.Contains("skyline " + first.Skyline.Hash, first.Canonical, "the town hash covers the skyline");
            }
        }

        [Test]
        public void ADifferentCourseOrSizeGivesADifferentSkyline()
        {
            var depot = Build("depot", CourseSurroundingsTier.High).Skyline;
            var other = Build("depot", CourseSurroundingsTier.High, "wordbound-lost-address").Skyline;
            Assert.AreNotEqual(depot.Hash, other.Hash);
            CollectionAssert.AreNotEqual(depot.Headings, other.Headings, "the downtown turns with the course id");
            CollectionAssert.AreNotEqual(depot.Towers.Select(t => t.Type).ToList(), other.Towers.Select(t => t.Type).ToList(), "the architecture changes with the course id");
            Assert.AreNotEqual(depot.Hash, Build("flagship", CourseSurroundingsTier.High).Skyline.Hash);
        }

        [Test]
        public void TheDowntownFacesDifferentWaysOnDifferentCourses()
        {
            var headings = new List<float>();
            var clusters = new HashSet<int>();
            for (int n = 0; n < 16; n++)
            {
                var skyline = CourseSurroundings.Build(Style(), Kit(), "course-" + n, Footprint("arena"), CourseSurroundingsTier.Low).Skyline;
                Assert.That(skyline.Headings.Count, Is.InRange(1, 2));
                clusters.Add(skyline.Headings.Count);
                headings.Add(skyline.Headings[0]);
            }
            Assert.GreaterOrEqual(headings.Select(h => (int)(h / 45f)).Distinct().Count(), 5, string.Join(", ", headings));
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, clusters, "some courses get one downtown and some get two");
        }

        [TestCaseSource(nameof(Cases))]
        public void NothingStandsInsideTheClearMarginOrOnTheTown(string shape, CourseSurroundingsTier tier)
        {
            var result = Build(shape, tier);
            var skyline = result.Skyline;
            var margin = Grown(result.Footprint, result.Clear);
            Assert.IsNotEmpty(skyline.Towers);
            foreach (var t in skyline.Towers)
            {
                Assert.AreEqual(result.GroundY, t.Ground, 1e-3);
                foreach (var b in t.Boxes)
                {
                    Assert.IsFalse(Overlap(b.Corners(), margin), $"the {b.Part} of the {t.Type} at ({t.X}, {t.Z}) is inside the course bounds plus {result.Clear} m");
                    Assert.GreaterOrEqual(b.Y, result.GroundY - 1e-3, $"the {b.Part} of the {t.Type} at ({t.X}, {t.Z}) sinks below the street");
                    Assert.IsTrue(b.Corners().All(c => InLot(t, c)), $"the {b.Part} of the {t.Type} at ({t.X}, {t.Z}) leaves its lot");
                }
                var lot = Lot(t);
                foreach (var p in result.Placements)
                    Assert.IsFalse(Overlap(lot, Corners(p)), $"the {t.Type} at ({t.X}, {t.Z}) overlaps {p.Piece} ({p.Source})");
            }
            for (int a = 0; a < skyline.Towers.Count; a++)
                for (int b = a + 1; b < skyline.Towers.Count; b++)
                    Assert.IsFalse(Overlap(Lot(skyline.Towers[a]), Lot(skyline.Towers[b])), $"towers {a} and {b} share ground");
        }

        [TestCaseSource(nameof(Shapes))]
        public void HeightsRiseToTallDowntownsWithOneSupertallEach(string shape)
        {
            var skyline = Build(shape, CourseSurroundingsTier.High).Skyline;
            var towers = skyline.Towers;
            foreach (var t in towers.Where(t => t.Landmark == null))
            {
                Assert.That(t.Height, Is.InRange(LowestBody - Tolerance, TallestBody + Tolerance), $"the {t.Type} at ({t.X}, {t.Z})");
                Assert.That(t.Top - t.Ground, Is.InRange(t.Height - Tolerance, t.Height + TopAboveBody), $"the {t.Type} at ({t.X}, {t.Z})");
            }
            foreach (var t in towers.Where(t => t.Landmark != null))
                Assert.AreEqual(t.Height, t.Top - t.Ground, .6, $"the {t.Landmark} reaches its stated height");
            var supertalls = towers.Where(t => t.Type == CourseSkylineTypes.Supertall).ToList();
            Assert.AreEqual(skyline.Headings.Count, supertalls.Count, "one supertall per downtown");
            Assert.IsTrue(supertalls.All(t => t.Downtown && t.Height >= SupertallLow - Tolerance && t.Height <= SupertallHigh + Tolerance));
            Assert.AreEqual(supertalls.Max(t => t.Top), towers.Max(t => t.Top), 1e-3, "a supertall is the tallest point on the skyline");
            Assert.IsTrue(towers.Where(t => t.Type == CourseSkylineTypes.Needle).All(t => t.Height >= NeedleLow - Tolerance && t.Height <= NeedleHigh + Tolerance));
            Assert.IsTrue(towers.Where(t => t.Type == CourseSkylineTypes.Clock).All(t => t.Height >= ClockLow - Tolerance && t.Height <= ClockHigh + Tolerance));
            var downtown = towers.Where(t => t.Downtown && t.Landmark == null).ToList();
            var ring = towers.Where(t => !t.Downtown && t.Landmark == null).ToList();
            Assert.Greater(downtown.Average(t => t.Height), ring.Average(t => t.Height) + 30, "the downtown is clearly taller than the ring");
            Assert.GreaterOrEqual(downtown.Count(t => t.Height >= Downtown), 8, "the downtown reaches 120 to 200 m");
        }

        [TestCaseSource(nameof(Shapes))]
        public void TheCityHasManyArchitectures(string shape)
        {
            var skyline = Build(shape, CourseSurroundingsTier.High).Skyline;
            var plain = skyline.Towers.Where(t => t.Landmark == null).Select(t => t.Type).Distinct().ToList();
            Assert.GreaterOrEqual(plain.Count, 7, string.Join(", ", plain));
            Assert.AreEqual(1, skyline.Count(CourseSkylineTypes.Clock), "one clock tower");
            Assert.LessOrEqual(skyline.Count(CourseSkylineTypes.Needle), 1, "at most one needle tower");
            Assert.GreaterOrEqual(skyline.Towers.Select(t => t.Wall).Distinct().Count(), 12, "walls vary through the palette");
            Assert.IsTrue(skyline.Boxes.Any(b => b.Sides > 4), "round and octagonal towers are prisms");
            Assert.IsTrue(skyline.Boxes.Any(b => b.Apex), "pyramids and spires come to a point");
            Assert.IsTrue(skyline.Boxes.Any(b => b.Part == "mullion"), "curtain walls have vertical mullions");
            Assert.IsTrue(skyline.Boxes.Any(b => b.Part == "tank"), "lofts carry water tanks");
            Assert.IsTrue(skyline.Boxes.Any(b => b.Part == "face"), "the clock tower has faces");
        }

        static (double hue, double saturation, double value) Hsv(uint rgb)
        {
            double r = (rgb >> 16 & 0xFF) / 255.0, g = (rgb >> 8 & 0xFF) / 255.0, b = (rgb & 0xFF) / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), delta = max - min;
            double hue = delta < 1e-9 ? 0 : max == r ? 60 * (((g - b) / delta + 6) % 6) : max == g ? 60 * ((b - r) / delta + 2) : 60 * ((r - g) / delta + 4);
            return (hue, max <= 0 ? 0 : delta / max, max);
        }

        static double Median(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            return sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];
        }

        [TestCaseSource(nameof(Shapes))]
        public void TheCityIsPaintedInRichVariedColours(string shape)
        {
            var skyline = Build(shape, CourseSurroundingsTier.High).Skyline;
            var towers = skyline.Towers;
            Assert.IsFalse(skyline.Boxes.Any(b => b.Tint == SurroundingsSlots.NoTint), "every box carries its own colour, since the skyline draws on a white base");
            double walls = Median(towers.Select(t => Hsv(t.Wall).saturation)), panes = Median(towers.Select(t => Hsv(t.Pane).saturation));
            var hues = towers.SelectMany(t => new[] { t.Wall, t.Pane, t.Accent }).Select(Hsv).Where(c => c.saturation >= .3 && c.value >= .25).Select(c => (int)(c.hue / 30)).Distinct().Count();
            string summary = $"wall saturation {walls:0.00}, pane saturation {panes:0.00}, {hues} hue families";
            TestContext.WriteLine(summary);
            Assert.GreaterOrEqual(walls, .35, summary);
            Assert.GreaterOrEqual(panes, .35, summary);
            Assert.GreaterOrEqual(hues, 6, summary);
            Assert.GreaterOrEqual(towers.Where(t => t.Downtown && t.Facade == "glass").Select(t => t.Pane).Distinct().Count(), 4, "downtown glass towers come in several glass tints");
            Assert.IsTrue(towers.All(t => t.Accent != t.Wall), "every building has a trim colour that differs from its walls");
            Assert.IsTrue(towers.All(t => t.Base != t.Wall), "every plinth contrasts with its walls");
            for (int a = 0; a < towers.Count; a++)
                for (int b = a + 1; b < towers.Count; b++)
                {
                    double dx = towers[a].X - towers[b].X, dz = towers[a].Z - towers[b].Z;
                    if (dx * dx + dz * dz < 36 * 36) Assert.AreNotEqual(towers[a].Wall, towers[b].Wall, $"the neighbours at ({towers[a].X}, {towers[a].Z}) and ({towers[b].X}, {towers[b].Z}) share a wall colour");
                }
        }

        [TestCaseSource(nameof(Shapes))]
        public void FacadesHaveAccentsShopfrontsAndColouredRoofs(string shape)
        {
            var towers = Build(shape, CourseSurroundingsTier.High).Skyline.Towers;
            var detailed = towers.Where(t => t.Detailed && t.Landmark == null).ToList();
            Assert.IsNotEmpty(detailed);
            Assert.IsTrue(towers.All(t => t.Boxes.Where(b => b.Part == "plinth").All(b => b.Tint == t.Base)), "plinths use the building's base colour");
            Assert.Greater(towers.Count(t => t.Boxes.Any(b => b.Part == "band" && b.Tint == t.Accent) && t.Boxes.Any(b => b.Part == "band" && b.Tint == t.Wall)), towers.Count / 2, "most buildings have accent bands between their wall bands");
            Assert.Greater(detailed.Count(t => t.Boxes.Any(b => b.Part == "awning")), detailed.Count / 2, "most near buildings have shopfront awnings");
            Assert.IsTrue(detailed.Any(t => t.Boxes.Any(b => b.Part == "sign")), "some shopfronts carry signs");
            Assert.Greater(detailed.Count(t => t.Boxes.Any(b => b.Part == "pane")), detailed.Count / 2, "most near buildings have lit or tinted panes in their window rows");
            Assert.GreaterOrEqual(towers.SelectMany(t => t.Boxes).Where(b => b.Part == "roof").Select(b => b.Tint).Distinct().Count(), 4, "roofs come in copper green, red tile, slate and more");
            Assert.IsTrue(towers.Where(t => t.Boxes.Any(b => b.Part == "mullion")).Any(t => t.Boxes.Where(b => b.Part == "mullion").Select(b => b.Tint).Distinct().Count() == 2), "curtain wall mullions alternate two colours");
            var shops = towers.SelectMany(t => t.Boxes).Where(b => b.Part == "shop").ToList();
            Assert.IsTrue(shops.All(b => b.Slot == CourseSkyline.LitSlot && Hsv(b.Tint).value >= .9 && Hsv(b.Tint).hue >= 30 && Hsv(b.Tint).hue <= 60), "shopfronts glow warm");
            Assert.IsTrue(towers.SelectMany(t => t.Boxes).Any(b => b.Part == "pane" && b.Slot == CourseSkyline.LitSlot), "some panes are lit");
            Assert.IsTrue(towers.SelectMany(t => t.Boxes).Any(b => b.Part == "pane" && b.Slot == CourseSkyline.GlassSlot), "some panes are tinted glass");
        }

        [Test]
        public void TheSkylineDrawsItsOwnColoursOnAWhiteBase()
        {
            foreach (string slot in CourseSkyline.Slots) Assert.That(CourseSkyline.SmoothnessOf(slot), Is.InRange(0f, .35f), slot);
            Assert.Less(CourseSkyline.WallSmoothness, CourseSkyline.GlassSmoothness);
            Assert.That(new[] { CourseSkyline.GlowRed, CourseSkyline.GlowGreen, CourseSkyline.GlowBlue }, Is.All.InRange(.1f, .7f), "lit panes glow gently in daylight");
            Assert.That(CourseSkyline.GlowRed, Is.GreaterThan(CourseSkyline.GlowBlue), "the glow is warm");
        }

        [Test]
        public void SomeDowntownTopsAreStillBeingBuilt()
        {
            var towers = Shapes.SelectMany(s => Build(s, CourseSurroundingsTier.High).Skyline.Towers).ToList();
            var cranes = towers.Where(t => t.Crane).ToList();
            Assert.IsNotEmpty(cranes);
            Assert.IsTrue(cranes.All(t => t.Downtown && t.Landmark == null));
            Assert.IsTrue(cranes.Where(t => t.Detailed).All(t => t.Boxes.Any(b => b.Part == "crane") && t.Boxes.Any(b => b.Part == "skeleton")));
            Assert.IsTrue(towers.Where(t => !t.Crane).All(t => t.Boxes.All(b => b.Part != "crane")));
        }

        [TestCaseSource(nameof(Shapes))]
        public void ThePackedCityFillsTheHorizon(string shape)
        {
            double high = Coverage(Build(shape, CourseSurroundingsTier.High), 8), tall = Coverage(Build(shape, CourseSurroundingsTier.High), 12);
            double low = Coverage(Build(shape, CourseSurroundingsTier.Low), 4);
            string summary = $"high {high:0.00} at 8 deg, {tall:0.00} at 12 deg; low {low:0.00} at 4 deg";
            TestContext.WriteLine(summary);
            Assert.GreaterOrEqual(high, .97, summary);
            Assert.GreaterOrEqual(tall, .8, summary);
            Assert.GreaterOrEqual(low, .7, summary);
        }

        [TestCaseSource(nameof(Cases))]
        public void EachTierFitsItsSkylineBudget(string shape, CourseSurroundingsTier tier)
        {
            var result = Build(shape, tier);
            var skyline = result.Skyline;
            Assert.IsTrue(result.Valid, string.Join("\n", result.Problems));
            bool high = tier == CourseSurroundingsTier.High;
            Assert.AreEqual(high ? CourseSkyline.HighRenderers : CourseSkyline.LowRenderers, skyline.RendererLimit);
            Assert.AreEqual(high ? CourseSkyline.HighTriangles : CourseSkyline.LowTriangles, skyline.TriangleLimit);
            Assert.LessOrEqual(skyline.Renderers, skyline.RendererLimit);
            Assert.LessOrEqual(skyline.Triangles, skyline.TriangleLimit);
            Assert.AreEqual(skyline.Boxes.Sum(b => b.Triangles), skyline.Triangles);
            Assert.LessOrEqual(result.Renderers, result.RendererLimit, "the town keeps its own budget");
            Assert.LessOrEqual(result.Triangles, result.TriangleLimit, "the town keeps its own budget");
        }

        [TestCaseSource(nameof(Cases))]
        public void NothingReachesPastTheCameraFarClip(string shape, CourseSurroundingsTier tier)
        {
            var result = Build(shape, tier);
            Assert.LessOrEqual(CourseSkyline.FarthestView(result.Footprint, result.Skyline.Boxes), CourseSkyline.ViewReach);
        }

        [Test]
        public void AVeryLargeCourseKeepsItsTowersInsideTheFarClip()
        {
            var large = new CourseFootprint(-700, -500, 700, 500, 0, 12);
            var result = CourseSurroundings.Draft(Style(), Kit(), "large", large, CourseSurroundingsTier.High);
            Assert.IsTrue(result.Valid, string.Join("\n", result.Problems));
            Assert.LessOrEqual(CourseSkyline.FarthestView(large, result.Skyline.Boxes), CourseSkyline.ViewReach);
        }

        [TestCaseSource(nameof(Shapes))]
        public void TheLowSkylineIsALighterCopyOfTheHighSkyline(string shape)
        {
            var high = Build(shape, CourseSurroundingsTier.High).Skyline;
            var low = Build(shape, CourseSurroundingsTier.Low).Skyline;
            Assert.Less(low.Towers.Count, high.Towers.Count);
            Assert.LessOrEqual(low.Triangles, high.Triangles * .3, "low is clearly lighter");
            Assert.LessOrEqual(low.Renderers, high.Renderers);
            var spots = high.Towers.ToDictionary(t => (t.X, t.Z), t => t);
            foreach (var t in low.Towers)
            {
                Assert.IsTrue(spots.TryGetValue((t.X, t.Z), out var twin), $"the low {t.Type} at ({t.X}, {t.Z}) is not in the high skyline");
                Assert.AreEqual(twin.Type, t.Type);
                Assert.AreEqual(twin.Yaw, t.Yaw);
                Assert.AreEqual(twin.Height, t.Height);
                Assert.LessOrEqual(t.Boxes.Count(b => b.Part == "band"), twin.Boxes.Count(b => b.Part == "band"), "low has no more window bands");
                Assert.LessOrEqual(t.Triangles, twin.Triangles);
            }
            foreach (var landmark in high.Towers.Where(t => t.Landmark != null))
                Assert.IsTrue(low.Towers.Any(t => t.X == landmark.X && t.Z == landmark.Z), $"low keeps the {landmark.Landmark}");
            Assert.IsFalse(low.Boxes.Any(b => Details.Contains(b.Part)), "low has no plazas, shopfronts, awnings, signs, tinted panes, ribs, parapets, roof details or cranes");
            Assert.IsFalse(low.Groups.Any(g => g.Slot == CourseSkyline.MetalSlot || g.Slot == CourseSkyline.LitSlot), "low draws walls and glass only");
            Assert.Less(low.Boxes.Count(b => b.Part == "band"), high.Boxes.Count(b => b.Part == "band"), "low has fewer window bands");
        }

        [Test]
        public void TheGeometryMatchesTheBoxesAndFacesOutward()
        {
            var skyline = Build("flagship", CourseSurroundingsTier.High).Skyline;
            var parts = CourseSkyline.Parts(skyline);
            Assert.AreEqual(skyline.Renderers, parts.Count);
            Assert.AreEqual(skyline.Vertices, parts.Sum(p => p.Vertices));
            Assert.AreEqual(skyline.Triangles, parts.Sum(p => p.Triangles));
            CollectionAssert.AreEquivalent(CourseSkyline.Slots, parts.Select(p => p.Slot).Distinct());
            foreach (string slot in CourseSkyline.Slots)
            {
                var boxes = skyline.Boxes.Where(b => b.Slot == slot).ToList();
                int box = 0;
                foreach (var part in parts.Where(p => p.Slot == slot).OrderBy(p => p.Index))
                {
                    Assert.LessOrEqual(part.Vertices, SurroundingsLayout.MergeVertexLimit);
                    Assert.AreEqual(part.Vertices, part.Colours.Length);
                    Assert.AreEqual(part.Positions.Length, part.Normals.Length);
                    Assert.IsTrue(part.Indices.All(i => i >= 0 && i < part.Vertices));
                    var owner = new int[part.Vertices];
                    for (int v = 0, first = box; v < part.Vertices; box++)
                    {
                        Assert.Less(box, boxes.Count, $"{part.Label} has more vertices than its boxes");
                        for (int k = 0; k < boxes[box].Vertices; k++, v++)
                        {
                            owner[v] = box;
                            Assert.AreEqual(boxes[box].Tint, part.Colours[v]);
                        }
                    }
                    for (int i = 0; i < part.Indices.Length; i += 3)
                    {
                        Vec Corner(int k) => new Vec(part.Positions[part.Indices[i + k] * 3], part.Positions[part.Indices[i + k] * 3 + 1], part.Positions[part.Indices[i + k] * 3 + 2]);
                        var normal = new Vec(part.Normals[part.Indices[i] * 3], part.Normals[part.Indices[i] * 3 + 1], part.Normals[part.Indices[i] * 3 + 2]);
                        var b = boxes[owner[part.Indices[i]]];
                        Assert.IsTrue(owner[part.Indices[i + 1]] == owner[part.Indices[i]] && owner[part.Indices[i + 2]] == owner[part.Indices[i]], "a triangle stays inside one box");
                        Assert.Greater((Corner(1) - Corner(0)).Cross(Corner(2) - Corner(0)).Dot(normal), 0, $"triangle {i / 3} of {part.Label} is wound against its normal");
                        var middle = (Corner(0) + Corner(1) + Corner(2)) * (1.0 / 3);
                        Assert.Greater((middle - new Vec(b.X, b.Y + b.Height * .5, b.Z)).Dot(normal), 0, $"a face of the {b.Part} at ({b.X}, {b.Z}) points inwards");
                    }
                }
                Assert.AreEqual(boxes.Count, box, $"every {slot} box became faces");
            }
        }

        [Test]
        public void TheRooflineCardsGiveWayDowntownAndTheHillsStay()
        {
            var bare = CourseSurroundingsStyle.FromJson(WithoutSkyline(Read(StyleFile)), "bare.json");
            int thinned = 0;
            foreach (string shape in Shapes)
            {
                var result = Build(shape, CourseSurroundingsTier.High);
                var plain = CourseSurroundings.Build(bare, Kit(), DepotId, Footprint(shape), CourseSurroundingsTier.High);
                Assert.IsTrue(plain.Skyline.Empty);
                Assert.IsFalse(plain.Canonical.Contains("skyline "), "a style without a skyline builds the town as before");
                Assert.AreEqual(plain.Count("card_hills_a"), result.Count("card_hills_a"), "the hills ring stays whole");
                Assert.AreEqual(plain.Count("card_roofline_a") - result.Skyline.ThinnedCards.Count, result.Count("card_roofline_a"));
                thinned += result.Skyline.ThinnedCards.Count;
                foreach (var card in result.Placements.Where(p => p.Card))
                    foreach (var t in result.Skyline.Towers)
                        Assert.IsFalse(Overlap(Lot(t), Corners(card)), $"{card.Piece} ({card.Source}) cuts through the {t.Type} at ({t.X}, {t.Z})");
            }
            Assert.Greater(thinned, 0, "some roofline cards give way to downtown towers");
        }

        [Test]
        public void TheCityStandsOnAGreyGroundBelowTheTownFades()
        {
            var result = Build("depot", CourseSurroundingsTier.High);
            var city = result.Ground.Single(g => g.Source == "city ground");
            var grass = result.Ground.Single(g => g.Source == "ground");
            var fades = result.Ground.Where(g => g.Source.StartsWith("ground fade", StringComparison.Ordinal)).ToList();
            Assert.Greater(city.Y, grass.Y);
            Assert.IsTrue(fades.All(f => f.Y > city.Y && f.Radius < city.Radius));
            double farthest = result.Skyline.Towers.Max(t => Lot(t).Max(c => Math.Sqrt((c.x - city.CentreX) * (c.x - city.CentreX) + (c.z - city.CentreZ) * (c.z - city.CentreZ))));
            Assert.Greater(city.Radius, farthest, "the grey ground reaches past the last tower");
        }

        [Test]
        public void TheDepotIsAPackedCity()
        {
            var skyline = Build("depot", CourseSurroundingsTier.High).Skyline;
            string summary = $"{skyline.Towers.Count} towers, {skyline.Towers.Count(t => t.Downtown)} downtown, {skyline.Renderers} renderers, {skyline.Triangles} tris, " +
                $"types {string.Join("/", skyline.Towers.GroupBy(t => t.Type).OrderBy(g => g.Key).Select(g => g.Key + " " + g.Count()))}";
            TestContext.WriteLine(summary);
            Assert.GreaterOrEqual(skyline.Towers.Count, 200, summary);
            Assert.GreaterOrEqual(skyline.Towers.Count(t => t.Downtown), 40, summary);
            Assert.That(skyline.Renderers, Is.InRange(3, CourseSkyline.HighRenderers), summary);
        }

        [Test]
        public void AHugeCourseKeepsTheSkylineInBudget()
        {
            var huge = new CourseFootprint(-300, -300, 300, 300, 0, 10);
            foreach (var tier in Tiers)
            {
                var result = CourseSurroundings.Draft(Style(), Kit(), "huge", huge, tier);
                Assert.IsTrue(result.Valid, string.Join("\n", result.Problems));
                Assert.IsNotEmpty(result.Skyline.Towers);
                Assert.LessOrEqual(result.Skyline.Triangles, result.Skyline.TriangleLimit);
                Assert.LessOrEqual(result.Skyline.Renderers, result.Skyline.RendererLimit);
                Assert.IsTrue(result.Skyline.Towers.Count(t => t.Landmark != null) >= 2, "landmarks survive trimming");
            }
        }

        [Test]
        public void ABrokenSkylineStyleIsRefused()
        {
            string json = Read(StyleFile);
            Assert.Throws<FormatException>(() => CourseSurroundingsStyle.FromJson(json.Replace("\"cell\": 30", "\"cell\": 20"), "cell.json"));
            Assert.Throws<FormatException>(() => CourseSurroundingsStyle.FromJson(json.Replace("\"clusters\": [1, 2]", "\"clusters\": [1, 3]"), "clusters.json"));
            Assert.Throws<FormatException>(() => CourseSurroundingsStyle.FromJson(json.Replace("\"glass\": [0.72, 0.82]", "\"glass\": [0.72, 0.95]"), "glass.json"));
            Assert.Throws<FormatException>(() => CourseSurroundingsStyle.FromJson(json.Replace("\"height\": [70, 200]", "\"height\": [70, 360]"), "height.json"));
            Assert.Throws<FormatException>(() => CourseSurroundingsStyle.FromJson(json.Replace("\"twin\": {", "\"tower\": {"), "type.json"));
            Assert.Throws<FormatException>(() => CourseSurroundingsStyle.FromJson(json.Replace("\"facades\": [\"glass\"] }", "\"facades\": [\"steel\"] }"), "facade.json"));
        }

        readonly struct Vec
        {
            readonly double x, y, z;

            public Vec(double x, double y, double z)
            {
                this.x = x; this.y = y; this.z = z;
            }

            public static Vec operator +(Vec a, Vec b) => new Vec(a.x + b.x, a.y + b.y, a.z + b.z);
            public static Vec operator -(Vec a, Vec b) => new Vec(a.x - b.x, a.y - b.y, a.z - b.z);
            public static Vec operator *(Vec a, double s) => new Vec(a.x * s, a.y * s, a.z * s);

            public double Dot(Vec o) => x * o.x + y * o.y + z * o.z;
            public Vec Cross(Vec o) => new Vec(y * o.z - z * o.y, z * o.x - x * o.z, x * o.y - y * o.x);
        }
    }
}
