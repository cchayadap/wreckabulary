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
        const string DepotId = "wordbound-parcel-service-v2", LostId = "wordbound-lost-address", HallId = "wordbound-parcel-hall-arrival-v2";
        const double Tolerance = 1e-3, EyeHeight = 1.7, Cell = 36, Gap = 11;
        const float LowestBody = 8f, TallestBody = 260f, TopAboveBody = 45f, Downtown = 120f, Proportion = 12f, SupertallSpan = 13f;
        const float SupertallLow = 285f, SupertallHigh = 345f, NeedleLow = 200f, NeedleHigh = 240f, ClockLow = 44f, ClockHigh = 58f;

        static readonly string[] Shapes = { "arena", "depot", "flagship", "offset", "ribbon" };
        static readonly CourseSurroundingsTier[] Tiers = { CourseSurroundingsTier.High, CourseSurroundingsTier.Low };
        static readonly string[] Districts = { DepotId, LostId, HallId };
        static readonly string[] Details = { "plaza", "door", "awning", "balcony", "pier", "ledge", "cornice", "parapet", "hut", "plant", "duct", "stand", "tank", "antenna", "chimney", "hand", "skeleton", "crane", "board", "vent" };
        static readonly string[] Furniture = { "tree", "trunk", "lamp", "car", "van", "stop", "bench", "wall", "hedge", "fence", "gantry", "fountain" };
        static readonly string[] Trims = { "body", "ledge", "cornice", "pier", "parapet", "plinth", "balcony", "hut", "chimney", "penthouse", "stage", "bridge", "shaft", "pod" };
        static readonly string[] Accents = { "awning", "board", "crown", "spire", "pyramid", "dome", "crane", "mast", "tank", "stand", "stack" };
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

        static (double hue, double saturation, double value) Hsv(uint rgb)
        {
            double r = (rgb >> 16 & 0xFF) / 255.0, g = (rgb >> 8 & 0xFF) / 255.0, b = (rgb & 0xFF) / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), delta = max - min;
            double hue = delta < 1e-9 ? 0 : max == r ? 60 * (((g - b) / delta + 6) % 6) : max == g ? 60 * ((b - r) / delta + 2) : 60 * ((r - g) / delta + 4);
            return (hue, max <= 0 ? 0 : delta / max, max);
        }

        static bool SameHue(uint colour, uint wall)
        {
            double[] c = { colour >> 16 & 0xFF, colour >> 8 & 0xFF, colour & 0xFF }, w = { wall >> 16 & 0xFF, wall >> 8 & 0xFF, wall & 0xFF };
            double k = c.Sum() / Math.Max(1, w.Sum());
            return Enumerable.Range(0, 3).All(i => Math.Abs(c[i] - w[i] * k) <= 2.5);
        }

        static double Share(IEnumerable<CourseTower> towers, string kind)
        {
            var list = towers.ToList();
            return list.Count == 0 ? 0 : list.Count(t => t.Kind == kind) / (double)list.Count;
        }

        [Test]
        public void TheStyleBringsASkylineDrawnWithItsOwnMaterial()
        {
            Assert.IsTrue(Style().HasSkyline);
            CollectionAssert.AreEqual(new[] { CourseSkyline.SkylineSlot }, CourseSkyline.Slots);
            Assert.IsTrue(Kit().Pieces.Any(p => p.Slots.Contains(CourseSkyline.FallbackSlot)), "the plain fallback uses a slot the kit has");
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
            Assert.That(supertalls.Count, Is.InRange(skyline.Headings.Count, 3 * skyline.Headings.Count), "one to three supertalls per downtown");
            Assert.IsTrue(supertalls.All(t => t.Downtown && t.Height >= Math.Min(SupertallLow, SupertallSpan * t.Width) - Tolerance && t.Height <= SupertallHigh + Tolerance));
            Assert.IsTrue(supertalls.All(t => t.Height <= SupertallSpan * t.Width + Tolerance), "supertalls keep real proportions");
            Assert.AreEqual(supertalls.Max(t => t.Top), towers.Max(t => t.Top), 1e-3, "a supertall is the tallest point on the skyline");
            Assert.IsTrue(towers.Where(t => t.Landmark == null).All(t => t.Height <= Proportion * Math.Min(t.Width, t.Depth) + .05), "plain towers stay at most twelve times as tall as they are wide");
            Assert.IsTrue(towers.Where(t => t.Type == CourseSkylineTypes.Needle).All(t => t.Height >= NeedleLow - Tolerance && t.Height <= NeedleHigh + Tolerance));
            Assert.IsTrue(towers.Where(t => t.Type == CourseSkylineTypes.Clock).All(t => t.Height >= ClockLow - Tolerance && t.Height <= ClockHigh + Tolerance));
            var downtown = towers.Where(t => t.Downtown && t.Landmark == null).ToList();
            var ring = towers.Where(t => !t.Downtown && t.Landmark == null).ToList();
            Assert.Greater(downtown.Average(t => t.Height), ring.Average(t => t.Height) + 30, "the downtown is clearly taller than the ring");
            Assert.GreaterOrEqual(downtown.Count(t => t.Height >= Downtown), 6, "the downtown reaches well above 120 m");
            Assert.GreaterOrEqual(downtown.Max(t => t.Height), 170, "the tallest downtown towers reach about 180 to 250 m");
        }

        [TestCaseSource(nameof(Shapes))]
        public void TheCityKeepsItsArchitectures(string shape)
        {
            var skyline = Build(shape, CourseSurroundingsTier.High).Skyline;
            var plain = skyline.Towers.Where(t => t.Landmark == null).Select(t => t.Type).Distinct().ToList();
            Assert.GreaterOrEqual(plain.Count, 7, string.Join(", ", plain));
            Assert.IsTrue(plain.Contains(CourseSkylineTypes.Shed), "the industrial depot district has warehouse sheds");
            Assert.IsTrue(skyline.Boxes.Any(b => b.Part == "ridge") && skyline.Boxes.Any(b => b.Part == "stack"), "sheds carry roof ridges and some chimney stacks");
            Assert.AreEqual(1, skyline.Count(CourseSkylineTypes.Clock), "one clock tower");
            Assert.LessOrEqual(skyline.Count(CourseSkylineTypes.Needle), 1, "at most one needle tower");
            Assert.IsTrue(skyline.Boxes.Any(b => b.Sides > 4 && b.Windows != null), "round and octagonal towers are prisms with windows");
            Assert.IsTrue(skyline.Boxes.Any(b => b.Apex), "pyramids and spires come to a point");
            Assert.IsTrue(skyline.Boxes.Any(b => b.Part == "tank"), "lofts carry water tanks");
            Assert.IsTrue(skyline.Boxes.Any(b => b.Part == "face"), "the clock tower has faces");
            Assert.IsFalse(skyline.Boxes.Any(b => b.Part == "band" || b.Part == "glass"), "no horizontal stripe bands or glass cores");
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

        [Test]
        public void ThePaletteIsMostlyNeutralWithSomeBrickAndGlass()
        {
            var towers = Districts.SelectMany(id => new[] { "flagship", "depot" }.SelectMany(s => Build(s, CourseSurroundingsTier.High, id).Skyline.Towers)).ToList();
            double neutral = Share(towers, CourseSkylineKinds.Neutral), brick = Share(towers, CourseSkylineKinds.Brick), glass = Share(towers, CourseSkylineKinds.Glass);
            string summary = $"neutral {neutral:0.00}, brick {brick:0.00}, glass {glass:0.00}";
            TestContext.WriteLine(summary);
            Assert.That(neutral, Is.InRange(.5, .74), summary);
            Assert.That(brick, Is.InRange(.12, .34), summary);
            Assert.That(glass, Is.InRange(.08, .26), summary);
            var downtown = towers.Where(t => t.Downtown).ToList();
            var ring = towers.Where(t => !t.Downtown).ToList();
            Assert.Greater(Share(downtown, CourseSkylineKinds.Glass), Share(ring, CourseSkylineKinds.Glass) + .1, "the downtown is mostly glass and stone");
            Assert.Greater(Share(ring, CourseSkylineKinds.Brick), Share(downtown, CourseSkylineKinds.Brick) + .1, "the ring is mostly stone and brick");
            Assert.GreaterOrEqual(Share(downtown, CourseSkylineKinds.Glass) + Share(downtown, CourseSkylineKinds.Neutral), .8, summary);
        }

        [TestCaseSource(nameof(Shapes))]
        public void EachBuildingHasOneWallMaterialWithTrimsDerivedFromIt(string shape)
        {
            var skyline = Build(shape, CourseSurroundingsTier.High).Skyline;
            foreach (var t in skyline.Towers)
            {
                Assert.AreEqual(CourseSkyline.Shade(t.Wall, CourseSkyline.PlinthShade), t.Base, "the plinth is the wall a little darker");
                foreach (var b in t.Boxes.Where(b => Trims.Contains(b.Part)))
                    Assert.IsTrue(SameHue(b.Tint, t.Wall), $"the {b.Part} of the {t.Type} at ({t.X}, {t.Z}) is {b.Tint:x6}, not a shade of its wall {t.Wall:x6}");
                Assert.IsTrue(t.Boxes.Where(b => b.Part == "body").All(b => b.Tint == t.Wall), $"the {t.Type} at ({t.X}, {t.Z}) keeps one wall colour");
                Assert.IsTrue(t.Boxes.Where(b => b.Part == "roof").All(b => Hsv(b.Tint).saturation < .2 && Hsv(b.Tint).value < .45), "roofs are dark grey or slate");
                foreach (var b in t.Boxes.Where(b => Hsv(b.Tint).saturation > .42))
                    Assert.IsTrue(Accents.Contains(b.Part) || (t.Kind == CourseSkylineKinds.Brick && SameHue(b.Tint, t.Wall)),
                        $"the {b.Part} of the {t.Type} at ({t.X}, {t.Z}) is saturated ({b.Tint:x6}); saturated colour belongs on small things and brick");
            }
            var glass = skyline.Towers.Select(t => Hsv(t.Pane)).ToList();
            Assert.IsTrue(glass.All(c => c.hue >= 185 && c.hue <= 215 && c.saturation <= .45 && c.value <= .65), "glass comes from one blue-grey to teal family");
            Assert.Less(skyline.Towers.Where(t => t.Kind != CourseSkylineKinds.Glass).Average(t => Hsv(t.Pane).value),
                skyline.Towers.Where(t => t.Kind == CourseSkylineKinds.Glass).Average(t => Hsv(t.Pane).value), "older masonry buildings have darker glass");
            var towers = skyline.Towers;
            for (int a = 0; a < towers.Count; a++)
                for (int b = a + 1; b < towers.Count; b++)
                {
                    double dx = towers[a].X - towers[b].X, dz = towers[a].Z - towers[b].Z;
                    if (dx * dx + dz * dz < 36 * 36) Assert.AreNotEqual(towers[a].Wall, towers[b].Wall, $"the neighbours at ({towers[a].X}, {towers[a].Z}) and ({towers[b].X}, {towers[b].Z}) share a wall colour");
                }
        }

        [TestCaseSource(nameof(Cases))]
        public void FacadesShowGridsOfRealWindows(string shape, CourseSurroundingsTier tier)
        {
            var skyline = Build(shape, tier).Skyline;
            foreach (var t in skyline.Towers)
                foreach (var b in t.Boxes.Where(b => b.Part == "body"))
                {
                    var w = b.Windows;
                    Assert.IsNotNull(w, $"the body of the {t.Type} at ({t.X}, {t.Z}) has windows");
                    Assert.AreEqual(t.Pane, w.Glass);
                    Assert.That(w.Width, Is.InRange(.2f, .95f));
                    Assert.That(w.Height, Is.InRange(.2f, .95f));
                    Assert.That(w.Lit, Is.InRange(0f, .05f), "only a few lit panes in daylight");
                    if (t.Kind == CourseSkylineKinds.Glass) Assert.IsTrue(w.Spacing < 2f && w.Width >= .8f, "curtain walls have narrow panes and thin mullions");
                    else Assert.IsTrue(w.Spacing >= 2.2f && w.Width <= .75f, "masonry has punched windows with piers between them");
                    Assert.GreaterOrEqual(w.Rows(b.Height), 1, $"the body of the {t.Type} at ({t.X}, {t.Z}) has at least one row");
                }
            var parts = CourseSkyline.Parts(skyline);
            int windowed = 0, faces = 0;
            foreach (var part in parts)
                for (int v = 0; v < part.Vertices; v += 4)
                {
                    faces++;
                    if (part.Grid[v * 4] > 0) windowed++;
                }
            Assert.Greater(windowed, faces / 10, "a good share of faces carry window grids");
        }

        [TestCaseSource(nameof(Shapes))]
        public void GroundFloorsAreTallerWithShopfrontsAndStreetDetail(string shape)
        {
            var result = Build(shape, CourseSurroundingsTier.High);
            var skyline = result.Skyline;
            var towers = skyline.Towers;
            foreach (var t in towers)
                foreach (var plinth in t.Boxes.Where(b => b.Part == "plinth"))
                {
                    var body = t.Boxes.FirstOrDefault(b => b.Part == "body");
                    Assert.IsNotNull(plinth.Windows, "the ground floor has shopfront windows");
                    Assert.AreEqual(1, plinth.Windows.Rows(plinth.Height), "one tall row of shop windows");
                    Assert.Greater(plinth.Windows.Spacing, 4f, "shop windows are wide");
                    if (body != null && t.Height >= 30) Assert.Greater(plinth.Height, body.Windows.Storey, $"the ground floor of the {t.Type} at ({t.X}, {t.Z}) is taller than its storeys");
                }
            var detailed = towers.Where(t => t.Detailed && t.Landmark == null && t.Type != CourseSkylineTypes.Shed).ToList();
            Assert.Greater(detailed.Count(t => t.Boxes.Any(b => b.Part == "door")), detailed.Count / 2, "most near buildings have entrances");
            Assert.Greater(detailed.Count(t => t.Boxes.Any(b => b.Part == "awning")), detailed.Count / 4, "many shopfronts have awnings");
            Assert.Greater(detailed.Count(t => t.Boxes.Any(b => b.Part == "cornice")), detailed.Count * 3 / 4, "cornices top most buildings");
            Assert.Greater(detailed.Count(t => t.Boxes.Any(b => b.Part == "ledge")), detailed.Count / 2, "ledges every few floors");
            Assert.Greater(detailed.Count(t => t.Boxes.Any(b => b.Part == "parapet")), detailed.Count / 2, "parapets on the roofs");
            Assert.Greater(detailed.Count(t => t.Boxes.Any(b => b.Part == "plant" || b.Part == "hut" || b.Part == "tank")), detailed.Count / 2, "rooftop plant");
            Assert.IsTrue(detailed.Any(t => t.Type == CourseSkylineTypes.Slab && t.Boxes.Any(b => b.Part == "balcony")), "some residential slabs have balconies");
            Assert.IsTrue(detailed.Any(t => t.Boxes.Any(b => b.Part == "pier")), "masonry buildings have corner piers");
            Assert.IsTrue(towers.Where(t => t.Detailed && t.Distance <= result.Skyline.StreetReach && t.Boxes.Any(b => b.Part == "roof")).Any(t => t.Boxes.Any(b => b.Part == "vent")), "near rooftops carry extra vents");
        }

        [Test]
        public void TheDowntownCarriesAFewRooftopBillboards()
        {
            var towers = Shapes.SelectMany(s => Build(s, CourseSurroundingsTier.High).Skyline.Towers).ToList();
            var boards = towers.Where(t => t.Boxes.Any(b => b.Part == "board")).ToList();
            Assert.IsNotEmpty(boards);
            Assert.IsTrue(boards.All(t => t.Downtown));
            Assert.Less(boards.Count, towers.Count(t => t.Downtown) / 4, "only a few");
        }

        [TestCaseSource(nameof(Shapes))]
        public void FarRowsRecedeIntoTheHaze(string shape)
        {
            var skyline = Build(shape, CourseSurroundingsTier.High).Skyline;
            var towers = skyline.Towers.OrderBy(t => t.Distance).ToList();
            Assert.AreEqual(0.0, towers.First().Haze, .05, "the nearest row is clear");
            Assert.Greater(towers.Last().Haze, .1, "the farthest row is hazy");
            for (int i = 1; i < towers.Count; i++) Assert.GreaterOrEqual(towers[i].Haze, towers[i - 1].Haze - 1e-3, "haze grows with distance");
            var far = towers.Last();
            var body = far.Boxes.First(b => b.Part == "body");
            uint tinted = CourseSkyline.VertexColour(body, body.TopY, skyline.HazeTint), plain = CourseSkyline.Shade(body.Tint, CourseSkyline.Gradient(body, body.TopY));
            double Distance(uint a, uint b) => Math.Abs((int)(a >> 16 & 0xFF) - (int)(b >> 16 & 0xFF)) + Math.Abs((int)(a >> 8 & 0xFF) - (int)(b >> 8 & 0xFF)) + Math.Abs((int)(a & 0xFF) - (int)(b & 0xFF));
            Assert.Less(Distance(tinted, skyline.HazeTint), Distance(plain, skyline.HazeTint), "a far wall moves towards the sky colour");
            foreach (var b in skyline.Boxes.Where(b => b.Part == "body").Take(50))
            {
                uint low = CourseSkyline.VertexColour(b, b.Y, skyline.HazeTint), high = CourseSkyline.VertexColour(b, b.TopY, skyline.HazeTint);
                Assert.LessOrEqual((low >> 16 & 0xFF) + (low >> 8 & 0xFF) + (low & 0xFF), (high >> 16 & 0xFF) + (high >> 8 & 0xFF) + (high & 0xFF), "walls are a little darker towards the street");
            }
        }

        [TestCaseSource(nameof(Shapes))]
        public void ThePackedCityFillsTheHorizon(string shape)
        {
            double high = Coverage(Build(shape, CourseSurroundingsTier.High), 8), tall = Coverage(Build(shape, CourseSurroundingsTier.High), 12);
            double low = Coverage(Build(shape, CourseSurroundingsTier.Low), 4);
            string summary = $"high {high:0.00} at 8 deg, {tall:0.00} at 12 deg; low {low:0.00} at 4 deg";
            TestContext.WriteLine(summary);
            Assert.GreaterOrEqual(high, .95, summary);
            Assert.GreaterOrEqual(tall, .75, summary);
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
            Assert.AreEqual(high ? CourseSkyline.HighStreetRenderers : CourseSkyline.LowStreetRenderers, skyline.StreetRendererLimit);
            Assert.AreEqual(high ? CourseSkyline.HighStreetTriangles : CourseSkyline.LowStreetTriangles, skyline.StreetTriangleLimit);
            Assert.LessOrEqual(skyline.StreetRenderers, skyline.StreetRendererLimit);
            Assert.LessOrEqual(skyline.StreetTriangles, skyline.StreetTriangleLimit);
            Assert.AreEqual(skyline.Streets.Sum(b => b.Triangles), skyline.StreetTriangles);
            Assert.LessOrEqual(result.Renderers, result.RendererLimit, "the town keeps its own budget");
            Assert.LessOrEqual(result.Triangles, result.TriangleLimit, "the town keeps its own budget");
        }

        [TestCaseSource(nameof(Cases))]
        public void NothingReachesPastTheCameraFarClip(string shape, CourseSurroundingsTier tier)
        {
            var result = Build(shape, tier);
            Assert.LessOrEqual(CourseSkyline.FarthestView(result.Footprint, result.Skyline.Boxes), CourseSkyline.ViewReach);
            Assert.LessOrEqual(CourseSkyline.FarthestView(result.Footprint, result.Skyline.Streets), CourseSkyline.ViewReach);
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
                Assert.AreEqual(twin.Wall, t.Wall);
                Assert.LessOrEqual(t.Triangles, twin.Triangles);
            }
            foreach (var landmark in high.Towers.Where(t => t.Landmark != null))
                Assert.IsTrue(low.Towers.Any(t => t.X == landmark.X && t.Z == landmark.Z), $"low keeps the {landmark.Landmark}");
            Assert.IsFalse(low.Boxes.Any(b => Details.Contains(b.Part)), "low has no plazas, entrances, awnings, balconies, piers, ledges, roof plant or cranes");
            Assert.IsTrue(low.Boxes.Where(b => b.Part == "body").All(b => b.Windows != null), "low keeps its window grids, which cost no triangles");
            Assert.Less(low.StreetTriangles, high.StreetTriangles, "low streets are lighter");
            Assert.LessOrEqual(low.StreetReach, high.StreetReach);
            Assert.Greater(low.CountStreet("road"), 0, "low keeps its roads");
            Assert.Greater(low.CountStreet("pavement"), 0, "low keeps its pavements");
            Assert.Greater(low.CountStreet("tree"), 0, "low keeps some street trees");
            foreach (string part in new[] { "lamp", "car", "van", "stop", "bench", "fence", "gantry" })
                Assert.AreEqual(0, low.CountStreet(part), $"low drops every {part}");
            Assert.IsTrue(low.Streets.Where(b => b.Part == "pavement").All(b => b.Decal), "low pavements are flat");
        }

        [Test]
        public void TheGeometryMatchesTheBoxesFacesOutwardAndCarriesTheWindows()
        {
            var skyline = Build("flagship", CourseSurroundingsTier.High).Skyline;
            var parts = CourseSkyline.Parts(skyline);
            Assert.AreEqual(skyline.Renderers, parts.Count);
            Assert.AreEqual(skyline.Vertices, parts.Sum(p => p.Vertices));
            Assert.AreEqual(skyline.Triangles, parts.Sum(p => p.Triangles));
            var boxes = skyline.Boxes.ToList();
            int box = 0;
            foreach (var part in parts.OrderBy(p => p.Index))
            {
                Assert.LessOrEqual(part.Vertices, SurroundingsLayout.MergeVertexLimit);
                Assert.AreEqual(part.Vertices, part.Colours.Length);
                Assert.AreEqual(part.Vertices * 4, part.Facade.Length);
                Assert.AreEqual(part.Vertices * 4, part.Grid.Length);
                Assert.AreEqual(part.Vertices * 4, part.Glass.Length);
                Assert.AreEqual(part.Vertices * 2, part.Extra.Length);
                Assert.IsTrue(part.Indices.All(i => i >= 0 && i < part.Vertices));
                var owner = new int[part.Vertices];
                for (int v = 0; v < part.Vertices; box++)
                {
                    Assert.Less(box, boxes.Count, $"{part.Label} has more vertices than its boxes");
                    var b = boxes[box];
                    for (int k = 0; k < b.Vertices; k++, v++)
                    {
                        owner[v] = box;
                        Assert.AreEqual(CourseSkyline.VertexColour(b, part.Positions[v * 3 + 1], skyline.HazeTint), part.Colours[v]);
                        Assert.AreEqual(b.Haze, part.Extra[v * 2 + 1], 1e-6);
                        if (part.Grid[v * 4] <= 0) continue;
                        Assert.IsNotNull(b.Windows, $"only boxes with windows carry a grid ({b.Part})");
                        Assert.IsTrue(b.Straight, "windows sit on straight faces only");
                        Assert.AreEqual(b.Windows.Spacing, part.Grid[v * 4], 1e-5);
                        Assert.AreEqual(b.Windows.Storey, part.Grid[v * 4 + 1], 1e-5);
                        Assert.AreEqual(b.Windows.Lit, part.Glass[v * 4 + 3], 1e-5);
                        uint glass = CourseSkyline.GlassColour(b, skyline.HazeTint);
                        Assert.AreEqual((glass >> 16 & 0xFF) / 255f, part.Glass[v * 4], 1e-5);
                        Assert.GreaterOrEqual(part.Facade[v * 4 + 2], 1f, "at least one column");
                        Assert.GreaterOrEqual(part.Facade[v * 4 + 3], 1f, "at least one row");
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
                for (int v = 0; v + 3 < part.Vertices; v += 4)
                {
                    if (part.Grid[v * 4] <= 0 || boxes[owner[v]].Vertices % 4 != 0) continue;
                    double width = Math.Sqrt(Math.Pow(part.Positions[(v + 1) * 3] - part.Positions[v * 3], 2) + Math.Pow(part.Positions[(v + 1) * 3 + 2] - part.Positions[v * 3 + 2], 2));
                    Assert.AreEqual(width, Math.Abs(part.Facade[(v + 1) * 4] - part.Facade[v * 4]), 1e-2, "facade metres follow the face width");
                    double span = part.Facade[(v + 1) * 4] - part.Facade[v * 4];
                    double cells = part.Facade[v * 4 + 2] * part.Grid[v * 4];
                    Assert.AreEqual(0, Math.Min(part.Facade[v * 4], part.Facade[(v + 1) * 4]) + (Math.Abs(span) - cells) * .5, 1e-2, "the window columns are centred on the face");
                }
            }
            Assert.AreEqual(boxes.Count, box, "every box became faces");
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
                $"kinds {string.Join("/", skyline.Towers.GroupBy(t => t.Kind).OrderBy(g => g.Key).Select(g => g.Key + " " + g.Count()))}";
            TestContext.WriteLine(summary);
            Assert.GreaterOrEqual(skyline.Towers.Count, 140, summary);
            Assert.GreaterOrEqual(skyline.Towers.Count(t => t.Downtown), 25, summary);
            Assert.That(skyline.Renderers, Is.InRange(1, CourseSkyline.HighRenderers), summary);
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
            Assert.Throws<FormatException>(() => CourseSurroundingsStyle.FromJson(json.Replace("\"cell\": 36", "\"cell\": 20"), "cell.json"));
            Assert.Throws<FormatException>(() => CourseSurroundingsStyle.FromJson(json.Replace("\"clusters\": [1, 2]", "\"clusters\": [1, 3]"), "clusters.json"));
            Assert.Throws<FormatException>(() => CourseSurroundingsStyle.FromJson(json.Replace("\"width\": [0.86, 0.9]", "\"width\": [0.86, 0.99]"), "windows.json"));
            Assert.Throws<FormatException>(() => CourseSurroundingsStyle.FromJson(json.Replace("\"height\": [150, 250]", "\"height\": [150, 460]"), "height.json"));
            Assert.Throws<FormatException>(() => CourseSurroundingsStyle.FromJson(json.Replace("\"count\": 3, \"apart\"", "\"count\": 9, \"apart\""), "supertalls.json"));
            Assert.Throws<FormatException>(() => CourseSurroundingsStyle.FromJson(json.Replace("\"road\": 7, \"walk\": 2", "\"road\": 9, \"walk\": 2"), "road.json"));
            Assert.Throws<FormatException>(() => CourseSurroundingsStyle.FromJson(json.Replace("\"shed\": { \"ring\": 3.2", "\"tower\": { \"ring\": 3.2"), "district.json"));
            Assert.Throws<FormatException>(() => CourseSurroundingsStyle.FromJson(json.Replace("\"parks\": 0.15, \"yards\": 0.55", "\"parks\": 0.65, \"yards\": 0.55"), "open.json"));
            Assert.Throws<FormatException>(() => CourseSurroundingsStyle.FromJson(json.Replace("\"twin\": {", "\"tower\": {"), "type.json"));
            Assert.Throws<FormatException>(() => CourseSurroundingsStyle.FromJson(json.Replace("\"facades\": [\"glass\"] }", "\"facades\": [\"steel\"] }"), "facade.json"));
            Assert.Throws<FormatException>(() => CourseSurroundingsStyle.FromJson(json.Replace("\"kind\": \"brick\", \"ring\": 2.9", "\"kind\": \"marble\", \"ring\": 2.9"), "kind.json"));
            Assert.Throws<FormatException>(() => CourseSurroundingsStyle.FromJson(json.Replace("\"to\": 520", "\"to\": 20"), "haze.json"));
        }

        [Test]
        public void EachCourseGetsItsOwnDistrictCharacter()
        {
            Assert.AreEqual("industrial", Build("flagship", CourseSurroundingsTier.High, DepotId).Skyline.District);
            Assert.AreEqual("oldtown", Build("flagship", CourseSurroundingsTier.High, LostId).Skyline.District);
            Assert.AreEqual("civic", Build("flagship", CourseSurroundingsTier.High, HallId).Skyline.District);
            var picked = Enumerable.Range(0, 12).Select(n => CourseSurroundings.Build(Style(), Kit(), "course-" + n, Footprint("arena"), CourseSurroundingsTier.Low).Skyline.District).ToList();
            CollectionAssert.IsSubsetOf(picked.Distinct(), new[] { "industrial", "oldtown", "civic" });
            Assert.GreaterOrEqual(picked.Distinct().Count(), 2, "other courses pick a district from their id");
            Assert.AreEqual(picked[3], CourseSurroundings.Build(Style(), Kit(), "course-3", Footprint("arena"), CourseSurroundingsTier.Low).Skyline.District, "the pick is deterministic");
            var industrial = new[] { "flagship", "depot" }.Select(s => Build(s, CourseSurroundingsTier.High, DepotId).Skyline).ToList();
            var oldtown = new[] { "flagship", "depot" }.Select(s => Build(s, CourseSurroundingsTier.High, LostId).Skyline).ToList();
            var civic = new[] { "flagship", "depot" }.Select(s => Build(s, CourseSurroundingsTier.High, HallId).Skyline).ToList();
            double Kind(List<CourseSkylineResult> set, string kind) => Share(set.SelectMany(k => k.Towers), kind);
            double Type(List<CourseSkylineResult> set, params string[] types) => set.SelectMany(k => k.Towers).Count(t => types.Contains(t.Type)) / (double)set.Sum(k => k.Towers.Count);
            string summary = $"brick {Kind(industrial, CourseSkylineKinds.Brick):0.00}/{Kind(oldtown, CourseSkylineKinds.Brick):0.00}/{Kind(civic, CourseSkylineKinds.Brick):0.00}, " +
                $"glass {Kind(industrial, CourseSkylineKinds.Glass):0.00}/{Kind(oldtown, CourseSkylineKinds.Glass):0.00}/{Kind(civic, CourseSkylineKinds.Glass):0.00}";
            TestContext.WriteLine(summary);
            Assert.Greater(Type(industrial, CourseSkylineTypes.Shed), .1, "the industrial district has warehouses");
            Assert.AreEqual(0, Type(oldtown, CourseSkylineTypes.Shed) + Type(civic, CourseSkylineTypes.Shed), 1e-9, "warehouses belong to the industrial district");
            Assert.IsTrue(industrial.Any(k => k.CountStreet("container") > 0) && industrial.Any(k => k.CountStreet("gantry") > 0), "the industrial district has container yards");
            Assert.IsTrue(oldtown.Concat(civic).All(k => k.CountStreet("container") == 0), "only the industrial district has yards");
            Assert.Greater(industrial.Sum(k => k.Towers.Count(t => t.Crane)), 0, "the industrial district is still building");
            Assert.Greater(Kind(oldtown, CourseSkylineKinds.Brick), Kind(civic, CourseSkylineKinds.Brick) + .1, summary);
            Assert.Greater(Kind(oldtown, CourseSkylineKinds.Brick), Kind(industrial, CourseSkylineKinds.Brick) + .05, "the old town is the brick district: " + summary);
            Assert.Greater(Type(oldtown, CourseSkylineTypes.Loft, CourseSkylineTypes.Deco, CourseSkylineTypes.LPlan, CourseSkylineTypes.UPlan), Type(civic, CourseSkylineTypes.Loft, CourseSkylineTypes.Deco, CourseSkylineTypes.LPlan, CourseSkylineTypes.UPlan), "the old town has more lofts, deco towers and courtyard blocks");
            Assert.Greater(Kind(civic, CourseSkylineKinds.Glass), Kind(oldtown, CourseSkylineKinds.Glass) + .05, summary);
            Assert.Greater(Kind(civic, CourseSkylineKinds.Glass), Kind(industrial, CourseSkylineKinds.Glass) + .05, summary);
            Assert.Greater(civic.Sum(k => k.CountStreet("grass")), industrial.Sum(k => k.CountStreet("grass")), "the civic district has the most parks");
        }

        [TestCaseSource(nameof(Shapes))]
        public void SupertallsStandOnMergedLotsWithRoomAround(string shape)
        {
            var skyline = Build(shape, CourseSurroundingsTier.High).Skyline;
            var supertalls = skyline.Towers.Where(t => t.Type == CourseSkylineTypes.Supertall).ToList();
            Assert.IsTrue(supertalls.Any(t => t.Merged), "at least one supertall takes a double block");
            foreach (var t in supertalls.Where(t => t.Merged))
            {
                Assert.AreEqual(2 * Cell - Gap, t.LotWidth, 1e-2, "a merged lot spans two blocks and the street between them");
                Assert.AreEqual(t.LotWidth, t.LotDepth, 1e-2);
                Assert.Less(t.Width, t.LotWidth - 4, "the supertall stands in its own plaza");
            }
            for (int a = 0; a < supertalls.Count; a++)
                for (int b = a + 1; b < supertalls.Count; b++)
                    Assert.Greater(Math.Sqrt(Math.Pow(supertalls[a].X - supertalls[b].X, 2) + Math.Pow(supertalls[a].Z - supertalls[b].Z, 2)), 60, "supertalls keep their distance");
        }

        [TestCaseSource(nameof(Shapes))]
        public void StreetsRunBetweenTheBlocksWithPavementsCrossingsAndLife(string shape)
        {
            var result = Build(shape, CourseSurroundingsTier.High);
            var skyline = result.Skyline;
            var margin = Grown(result.Footprint, result.Clear);
            string counts = string.Join(" ", skyline.Streets.GroupBy(b => b.Part).OrderBy(g => g.Key).Select(g => g.Key + "=" + g.Count()));
            TestContext.WriteLine($"{counts}; {skyline.StreetRenderers} renderers, {skyline.StreetTriangles} tris, reach {skyline.StreetReach}");
            foreach (string part in new[] { "road", "pavement", "line", "crossing", "tree", "trunk", "lamp", "car" })
                Assert.Greater(skyline.CountStreet(part), 0, $"the streets have {part}s: {counts}");
            Assert.Greater(skyline.CountStreet("van") + skyline.CountStreet("stop"), 0, counts);
            Assert.Greater(skyline.StreetReach, 100, "near streets carry their furniture well into the middle ground");
            foreach (var b in skyline.Streets)
            {
                Assert.IsFalse(Overlap(b.Corners(), margin), $"the {b.Part} at ({b.X}, {b.Z}) is inside the clear margin");
                Assert.GreaterOrEqual(b.Y, result.GroundY - 1e-3, $"the {b.Part} at ({b.X}, {b.Z}) sinks below the street");
                Assert.IsNull(b.Windows, "street detail has no windows");
                Assert.AreEqual(0f, b.Rise, "street detail keeps its own colour without the tower gradient");
                if (b.Decal) Assert.AreEqual(b.Sides - 2, b.Triangles, "a decal is one flat face");
            }
            Assert.IsTrue(skyline.Streets.Where(b => b.Part == "road" || b.Part == "line" || b.Part == "crossing").All(b => b.Decal), "roads and markings are flat decals");
            var lots = skyline.Towers.Select(Lot).ToList();
            foreach (var b in skyline.Streets.Where(b => b.Part == "road" || b.Part == "pavement" || Furniture.Contains(b.Part)))
            {
                var shrunk = Corners(b.X, b.Z, Math.Max(.01, b.Width - .2), Math.Max(.01, b.Depth - .2), b.Yaw);
                foreach (var lot in lots)
                    Assert.IsFalse(Overlap(shrunk, lot), $"the {b.Part} at ({b.X}, {b.Z}) runs into a building lot");
                foreach (var p in result.Placements.Where(p => !p.Card))
                    Assert.IsFalse(Overlap(shrunk, Corners(p)), $"the {b.Part} at ({b.X}, {b.Z}) runs into {p.Piece} ({p.Source})");
            }
            var heights = skyline.Streets.Where(b => b.Part == "tree" && b.Top).Select(b => b.TopY - result.GroundY).ToList();
            Assert.AreEqual(skyline.CountStreet("trunk"), heights.Count, "every trunk carries one crown top");
            Assert.IsTrue(heights.All(h => h > 4 && h < 9), "street trees stand 4 to 9 m tall");
            Assert.IsTrue(skyline.Streets.Where(b => b.Part == "car").All(b => b.Width < 2 && b.Depth < 5), "cars are car sized");
        }

        [Test]
        public void ParksAndYardsFillSomeOpenBlocks()
        {
            var lost = Shapes.Select(s => Build(s, CourseSurroundingsTier.High, LostId).Skyline).ToList();
            Assert.Greater(lost.Sum(k => k.CountStreet("grass")), 0, "the old town has small parks");
            Assert.Greater(lost.Sum(k => k.CountStreet("path")), 0);
            Assert.Greater(lost.Sum(k => k.CountStreet("bench")), 0);
            Assert.Greater(lost.Sum(k => k.CountStreet("wall") + k.CountStreet("hedge")), 0, "parks have low walls or hedges");
            var depot = Shapes.Select(s => Build(s, CourseSurroundingsTier.High).Skyline).ToList();
            Assert.Greater(depot.Sum(k => k.CountStreet("yard")), 0);
            Assert.Greater(depot.Sum(k => k.CountStreet("fence")), 0, "yards are fenced");
            foreach (var k in depot.Concat(lost))
                foreach (var b in k.Streets.Where(b => b.Part == "container"))
                    Assert.LessOrEqual(b.TopY - k.Towers[0].Ground, 3 * 2.6 + 1e-3, "containers stack at most three high");
        }

        [Test]
        public void TheStreetGeometryMatchesItsBoxes()
        {
            var skyline = Build("flagship", CourseSurroundingsTier.High).Skyline;
            var parts = CourseSkyline.StreetParts(skyline);
            Assert.AreEqual(skyline.StreetRenderers, parts.Count);
            Assert.IsTrue(parts.All(p => p.Street && p.Label.StartsWith(CourseSkyline.StreetPrefix, StringComparison.Ordinal)));
            Assert.AreEqual(skyline.StreetVertices, parts.Sum(p => p.Vertices));
            Assert.AreEqual(skyline.StreetTriangles, parts.Sum(p => p.Triangles));
            var boxes = skyline.Streets.ToList();
            int box = 0;
            foreach (var part in parts.OrderBy(p => p.Index))
            {
                Assert.LessOrEqual(part.Vertices, SurroundingsLayout.MergeVertexLimit);
                Assert.IsTrue(part.Grid.All(g => g == 0f), "streets carry no windows");
                for (int v = 0; v < part.Vertices; box++)
                {
                    var b = boxes[box];
                    for (int k = 0; k < b.Vertices; k++, v++)
                    {
                        Assert.AreEqual(CourseSkyline.VertexColour(b, part.Positions[v * 3 + 1], skyline.HazeTint), part.Colours[v]);
                        if (b.Decal) Assert.AreEqual(1f, part.Normals[v * 3 + 1], 1e-5, "a decal faces the sky");
                    }
                }
                for (int i = 0; i < part.Indices.Length; i += 3)
                {
                    int a = part.Indices[i], b = part.Indices[i + 1], c = part.Indices[i + 2];
                    double ux = part.Positions[b * 3] - part.Positions[a * 3], uy = part.Positions[b * 3 + 1] - part.Positions[a * 3 + 1], uz = part.Positions[b * 3 + 2] - part.Positions[a * 3 + 2];
                    double wx = part.Positions[c * 3] - part.Positions[a * 3], wy = part.Positions[c * 3 + 1] - part.Positions[a * 3 + 1], wz = part.Positions[c * 3 + 2] - part.Positions[a * 3 + 2];
                    double nx = uy * wz - uz * wy, ny = uz * wx - ux * wz, nz = ux * wy - uy * wx;
                    Assert.Greater(nx * part.Normals[a * 3] + ny * part.Normals[a * 3 + 1] + nz * part.Normals[a * 3 + 2], 0, $"triangle {i / 3} of {part.Label} is wound against its normal");
                }
            }
            Assert.AreEqual(boxes.Count, box, "every street box became faces");
            Assert.AreEqual(1.0, CourseSkyline.Gradient(boxes[0], boxes[0].Y), 1e-9, "street detail is not shaded like a tower");
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
