using System;
using System.Collections.Generic;
using System.Linq;
using static Wreckabulary.Rules.Surroundings.CourseSurroundingsStyle;

namespace Wreckabulary.Rules.Surroundings
{
    public static class CourseSkylineTypes
    {
        public const string Block = "block", Loft = "loft", LPlan = "lplan", UPlan = "uplan", Slab = "slab", Deco = "deco", Round = "round", Curtain = "curtain", Twin = "twin", Shed = "shed";
        public const string Supertall = "supertall", Needle = "needle", Clock = "clock";

        public static readonly IReadOnlyList<string> Plain = new[] { Block, Loft, LPlan, UPlan, Slab, Deco, Round, Curtain, Twin, Shed };
        public static readonly IReadOnlyList<string> Landmarks = new[] { Supertall, Needle, Clock };
    }

    public static class CourseSkylineKinds
    {
        public const string Neutral = "neutral", Brick = "brick", Glass = "glass";

        public static readonly IReadOnlyList<string> All = new[] { Neutral, Brick, Glass };
    }

    sealed class CourseSkylineSpec
    {
        public const float MinStorey = 2.5f, MinHeight = 8f, MaxHeight = 400f;
        public const int MostSupertalls = 4;

        public float Cell, Jitter, Gap, Plaza, CardGap, Inset, DetailReach;
        public (float min, float max) Storey, Plinth;
        public CourseDowntownSpec Downtown;
        public CourseSkylineBand Ring;
        public readonly Dictionary<string, CourseFacadeSpec> Facades = new Dictionary<string, CourseFacadeSpec>(StringComparer.Ordinal);
        public readonly List<CourseTypeSpec> Types = new List<CourseTypeSpec>();
        public CourseLandmarkHeights Supertall, Needle, Clock;
        public float NeedleChance, CraneChance, SupertallApart, Proportion;
        public int SupertallCount;
        public CourseCityStreetSpec Streets;
        public readonly List<CourseDistrictSpec> Districts = new List<CourseDistrictSpec>();
        public List<string> SupertallFacades, ClockFacades, NeedleFacades;
        public List<uint> Plant, Caps, Roofs, Awnings, Signs, Crowns, Bold, ShopGlass;
        public uint Pavement, Tank, Crane, Face, Hands, Metal, Mast, HazeTint, Stack;
        public float HazeFrom, HazeTo, HazeMost, ShopLit;
        public List<string> ThinCards;
        public float KeepDowntown, KeepRing;
        public int StoreysPerBand, FarStoreysPerBand;

        public float LotLimit => Cell - Gap - 2 * Plaza;

        public static CourseSkylineSpec Read(JsonNode n)
        {
            var low = At(n, "low");
            var marks = At(n, "landmarks");
            var haze = At(n, "haze");
            var shop = At(n, "shop");
            var spec = new CourseSkylineSpec
            {
                Cell = AtLeast(At(n, "cell"), 30f, 12f), Jitter = AtLeast(At(n, "jitter"), 3f, 0f), Gap = AtLeast(At(n, "gap"), 4f, 2f),
                Plaza = AtLeast(At(n, "plaza"), 1f, .8f), CardGap = AtLeast(At(n, "cardGap"), 4f, 0f), Inset = AtLeast(At(n, "inset"), .3f, .05f),
                DetailReach = AtLeast(At(n, "detailReach"), 190f, 0f),
                Storey = Span(At(n, "storey"), 3.5f, MinStorey), Plinth = Span(At(n, "plinth"), 4.5f, MinStorey),
                Downtown = CourseDowntownSpec.Read(At(n, "downtown")),
                Ring = CourseSkylineBand.Read(At(n, "ring"), 80f, 300f, .85f, (22f, 46f), (16f, 24f)),
                Supertall = CourseLandmarkHeights.Read(At(marks, "supertall"), (220f, 260f)),
                Needle = CourseLandmarkHeights.Read(At(marks, "needle"), (180f, 215f)),
                Clock = CourseLandmarkHeights.Read(At(marks, "clock"), (44f, 58f)),
                NeedleChance = Fraction(At(At(marks, "needle"), "chance"), .75f),
                CraneChance = Fraction(At(At(marks, "crane"), "chance"), .1f),
                SupertallCount = At(At(marks, "supertall"), "count").Int(1),
                SupertallApart = AtLeast(At(At(marks, "supertall"), "apart"), 60f, 0f),
                Proportion = AtLeast(At(n, "proportion"), 10f, 3f),
                SupertallFacades = Names(At(At(marks, "supertall"), "facades"), false),
                ClockFacades = Names(At(At(marks, "clock"), "facades"), false),
                NeedleFacades = Names(At(At(marks, "needle"), "facades"), false),
                Plant = Tints(At(n, "plant")),
                Caps = Tints(At(n, "caps")),
                Roofs = Tints(At(n, "roofs")),
                Awnings = Tints(At(n, "awnings")),
                Signs = Tints(At(n, "signs")),
                Crowns = Tints(At(n, "crowns")),
                Bold = Tints(At(marks, "bold")),
                ShopGlass = Tints(At(shop, "glass")),
                ShopLit = Fraction(At(shop, "lit"), .35f),
                Metal = Tint(At(n, "metal"), 0x5A5E63),
                Mast = Tint(At(At(marks, "needle"), "mast"), 0xB8433A),
                Pavement = Tint(At(n, "pavement"), 0xCFC9BD),
                Tank = Tint(At(n, "tank"), 0x6E5A48),
                Stack = Tint(At(n, "stack"), 0x8A5A4A),
                Crane = Tint(At(At(marks, "crane"), "tint"), 0xD9A23A),
                Face = Tint(At(At(marks, "clock"), "face"), 0xF1EDE2),
                Hands = Tint(At(At(marks, "clock"), "hands"), 0x2E2E2E),
                HazeTint = Tint(At(haze, "tint"), 0xC8D4DF),
                HazeFrom = AtLeast(At(haze, "from"), 60f, 0f), HazeTo = At(haze, "to").Float(520f), HazeMost = Fraction(At(haze, "most"), .35f),
                ThinCards = Names(At(n, "thinCards"), false),
                KeepDowntown = Fraction(At(low, "keepDowntown"), .5f), KeepRing = Fraction(At(low, "keepRing"), .3f),
                StoreysPerBand = Math.Max(1, At(low, "storeysPerBand").Int(3)),
                FarStoreysPerBand = Math.Max(1, At(n, "farStoreysPerBand").Int(2)),
            };
            if (spec.HazeTo <= spec.HazeFrom) throw At(haze, "to").Error("must be beyond from");
            if (spec.SupertallCount < 1 || spec.SupertallCount > MostSupertalls) throw At(At(marks, "supertall"), "count").Error($"must be between 1 and {MostSupertalls}");
            if (spec.Plant.Count == 0) spec.Plant.Add(0x9A9C9E);
            if (spec.Roofs.Count == 0) spec.Roofs.Add(0x4E5257);
            if (spec.Caps.Count == 0) spec.Caps.AddRange(spec.Roofs);
            if (spec.Awnings.Count == 0) spec.Awnings.AddRange(spec.Plant);
            if (spec.Signs.Count == 0) spec.Signs.AddRange(spec.Awnings);
            if (spec.Crowns.Count == 0) spec.Crowns.AddRange(spec.Caps);
            if (spec.Bold.Count == 0) spec.Bold.AddRange(spec.Caps);
            if (spec.ShopGlass.Count == 0) spec.ShopGlass.Add(0x7F7563);
            var facades = At(n, "facades");
            if (facades.IsNull) throw n.Error("needs facades");
            foreach (string key in facades.Keys.OrderBy(k => k, StringComparer.Ordinal)) spec.Facades[key] = CourseFacadeSpec.Read(key, facades[key]);
            if (spec.Facades.Count == 0) throw facades.Error("needs at least one facade");
            var types = At(n, "types");
            if (types.IsNull) throw n.Error("needs types");
            foreach (string key in types.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                if (!CourseSkylineTypes.Plain.Contains(key)) throw types.Error($"has an unknown type \"{key}\"; the types are {string.Join(", ", CourseSkylineTypes.Plain)}");
                spec.Types.Add(CourseTypeSpec.Read(key, types[key], spec.Facades));
            }
            if (spec.Types.Sum(t => t.Ring) <= 0 || spec.Types.Sum(t => t.Downtown) <= 0) throw types.Error("needs a ring and a downtown weight above 0");
            foreach (string id in spec.SupertallFacades.Concat(spec.ClockFacades).Concat(spec.NeedleFacades))
                if (!spec.Facades.ContainsKey(id)) throw marks.Error($"names the facade \"{id}\", which is not in facades");
            float widest = Math.Max(spec.Downtown.Band.Width.max, spec.Ring.Width.max);
            if (widest > spec.LotLimit) throw At(n, "cell").Error($"leaves {spec.LotLimit:0.##} m for a building, less than the widest footprint {widest} m");
            spec.Streets = CourseCityStreetSpec.Read(At(n, "streets"), spec.Gap);
            var districts = At(n, "districts");
            if (!districts.IsNull)
                foreach (string key in districts.Keys.OrderBy(k => k, StringComparer.Ordinal))
                    spec.Districts.Add(CourseDistrictSpec.Read(key, districts[key], spec.Types));
            if (spec.Districts.Count == 0) spec.Districts.Add(CourseDistrictSpec.Plain());
            return spec;
        }

        public CourseFacadeSpec Facade(IReadOnlyList<string> ids, double pick, double downtown = 0, CourseDistrictSpec district = null)
        {
            var list = (ids.Count > 0 ? ids : Facades.Keys.ToList()).Select(id => Facades[id]).ToList();
            double Weight(CourseFacadeSpec f) => f.WeightAt(downtown) * (district?.KindScale(f.Kind) ?? 1);
            double total = list.Sum(Weight);
            if (total <= 0) return list[Math.Min(list.Count - 1, (int)(pick * list.Count))];
            double at = pick * total;
            foreach (var facade in list)
            {
                double w = Weight(facade);
                if (w <= 0) continue;
                at -= w;
                if (at < 0) return facade;
            }
            return list.Last(f => Weight(f) > 0);
        }

        public CourseDistrictSpec District(string courseId, double pick)
        {
            foreach (var district in Districts)
                if (district.Match.Any(prefix => (courseId ?? "").StartsWith(prefix, StringComparison.Ordinal))) return district;
            return Districts[Math.Min(Districts.Count - 1, (int)(pick * Districts.Count))];
        }

        static List<uint> Tints(JsonNode node) => node.IsNull ? new List<uint>() : node.Items.Select(SurroundingsPlan.Tint).ToList();

        static uint Tint(JsonNode node, uint fallback) => SurroundingsPlan.OptionalTint(node) ?? fallback;
    }

    sealed class CourseSkylineBand
    {
        public float From, To, Chance, TallChance, RowRise;
        public (float min, float max) Height, Width, Tall;

        public static CourseSkylineBand Read(JsonNode n, float from, float to, float chance, (float, float) height, (float, float) width)
        {
            var band = new CourseSkylineBand
            {
                From = AtLeast(At(n, "from"), from, 1f), To = At(n, "to").Float(to), Chance = Fraction(At(n, "chance"), chance),
                Height = Has(n, "height") ? Span(At(n, "height"), 0f, CourseSkylineSpec.MinHeight) : height,
                Width = Has(n, "width") ? Span(At(n, "width"), 0f, 6f) : width,
                TallChance = Fraction(At(n, "tallChance"), 0f), RowRise = AtLeast(At(n, "rowRise"), 0f, 0f),
            };
            band.Tall = Has(n, "tall") ? Span(At(n, "tall"), 0f, CourseSkylineSpec.MinHeight) : band.Height;
            if (band.To < band.From) throw At(n, "to").Error("must be at least from");
            if (Math.Max(band.Height.max + band.RowRise, band.Tall.max) > CourseSkylineSpec.MaxHeight) throw At(n, "height").Error($"must stay at or below {CourseSkylineSpec.MaxHeight} m");
            return band;
        }
    }

    sealed class CourseDowntownSpec
    {
        public CourseSkylineBand Band;
        public (int min, int max) Clusters;
        public float Spread, Second, Core;

        public static CourseDowntownSpec Read(JsonNode n)
        {
            var clusters = Span(At(n, "clusters"), 1f, 1f);
            var spec = new CourseDowntownSpec
            {
                Band = CourseSkylineBand.Read(n, 100f, 300f, 1f, (70f, 200f), (16f, 24f)),
                Clusters = ((int)clusters.min, (int)clusters.max),
                Spread = AtLeast(At(n, "spread"), 36f, 5f), Second = AtLeast(At(n, "second"), 110f, 30f),
                Core = Fraction(At(n, "core"), .3f),
            };
            if (spec.Clusters.max > 2) throw At(n, "clusters").Error("allows at most two downtown clusters");
            if (spec.Second > 180f) throw At(n, "second").Error("must be at most 180 degrees");
            return spec;
        }
    }

    sealed class CourseLandmarkHeights
    {
        public (float min, float max) Height;

        public static CourseLandmarkHeights Read(JsonNode n, (float, float) fallback)
        {
            var spec = new CourseLandmarkHeights { Height = Has(n, "height") ? Span(At(n, "height"), 0f, CourseSkylineSpec.MinHeight) : fallback };
            if (spec.Height.max > CourseSkylineSpec.MaxHeight) throw At(n, "height").Error($"must stay at or below {CourseSkylineSpec.MaxHeight} m");
            return spec;
        }
    }

    sealed class CourseTypeSpec
    {
        public string Id;
        public float Ring, Downtown;
        public (float min, float max)? Height;
        public List<string> Facades;

        public static CourseTypeSpec Read(string id, JsonNode n, IReadOnlyDictionary<string, CourseFacadeSpec> facades)
        {
            var spec = new CourseTypeSpec
            {
                Id = id, Ring = AtLeast(At(n, "ring"), 0f, 0f), Downtown = AtLeast(At(n, "downtown"), 0f, 0f),
                Height = Has(n, "height") ? Span(At(n, "height"), 0f, CourseSkylineSpec.MinHeight) : ((float, float)?)null,
                Facades = Names(At(n, "facades"), true),
            };
            foreach (string facade in spec.Facades)
                if (!facades.ContainsKey(facade)) throw At(n, "facades").Error($"names \"{facade}\", which is not in facades");
            if (spec.Height.HasValue && spec.Height.Value.max > CourseSkylineSpec.MaxHeight) throw At(n, "height").Error($"must stay at or below {CourseSkylineSpec.MaxHeight} m");
            return spec;
        }

        public float WeightAt(double downtown) => (float)(Ring + (Downtown - Ring) * downtown);
    }

    sealed class CourseFacadeSpec
    {
        const float MinSpacing = 1.2f, LeastShare = .2f, MostShare = .95f;

        public string Id, Kind;
        public float Ring, Downtown, Margin, Lit;
        public (float min, float max) Spacing, Width, Height;
        public List<uint> Walls, Glass;
        public bool Piers;

        public static CourseFacadeSpec Read(string id, JsonNode n)
        {
            var windows = At(n, "windows");
            var spec = new CourseFacadeSpec
            {
                Id = id, Kind = At(n, "kind").String(CourseSkylineKinds.Neutral),
                Ring = AtLeast(At(n, "ring"), 1f, 0f), Downtown = AtLeast(At(n, "downtown"), 1f, 0f),
                Walls = At(n, "walls").Items.Select(SurroundingsPlan.Tint).ToList(),
                Glass = At(n, "glass").Items.Select(SurroundingsPlan.Tint).ToList(),
                Piers = At(n, "piers").Bool(false),
                Spacing = Span(At(windows, "spacing"), 3f, MinSpacing),
                Width = SurroundingsPlan.Range(At(windows, "width"), .5f), Height = SurroundingsPlan.Range(At(windows, "height"), .55f),
                Margin = AtLeast(At(windows, "margin"), 1f, 0f), Lit = Fraction(At(windows, "lit"), .05f),
            };
            if (!CourseSkylineKinds.All.Contains(spec.Kind)) throw At(n, "kind").Error($"must be one of {string.Join(", ", CourseSkylineKinds.All)}");
            foreach (var (range, key) in new[] { (spec.Width, "width"), (spec.Height, "height") })
                if (!(range.min >= LeastShare && range.max <= MostShare)) throw At(windows, key).Error($"must stay between {LeastShare} and {MostShare} so frames and spandrels show");
            if (spec.Walls.Count == 0) throw At(n, "walls").Error("must list at least one colour");
            if (spec.Glass.Count == 0) throw At(n, "glass").Error("must list at least one glass colour");
            return spec;
        }

        public float WeightAt(double downtown) => (float)(Ring + (Downtown - Ring) * downtown);
    }

    sealed class CourseDistrictSpec
    {
        public string Id;
        public List<string> Match;
        public readonly Dictionary<string, (float ring, float downtown)> Types = new Dictionary<string, (float, float)>(StringComparer.Ordinal);
        public readonly Dictionary<string, float> Kinds = new Dictionary<string, float>(StringComparer.Ordinal);
        public float RingHeight = 1, DowntownHeight = 1, Parks, Yards, Chimneys, Cranes = 1;

        public static CourseDistrictSpec Plain() => new CourseDistrictSpec { Id = "plain", Match = new List<string>() };

        public static CourseDistrictSpec Read(string id, JsonNode n, IReadOnlyList<CourseTypeSpec> types)
        {
            var spec = new CourseDistrictSpec
            {
                Id = id, Match = Names(At(n, "match"), false),
                RingHeight = AtLeast(At(n, "ringHeight"), 1f, .3f), DowntownHeight = AtLeast(At(n, "downtownHeight"), 1f, .3f),
                Parks = Fraction(At(n, "parks"), 0f), Yards = Fraction(At(n, "yards"), 0f), Chimneys = Fraction(At(n, "chimneys"), 0f),
                Cranes = AtLeast(At(n, "cranes"), 1f, 0f),
            };
            if (spec.Parks + spec.Yards > 1f) throw At(n, "yards").Error("parks and yards together must stay at or below 1");
            var kinds = At(n, "kinds");
            if (!kinds.IsNull)
                foreach (string kind in kinds.Keys.OrderBy(k => k, StringComparer.Ordinal))
                {
                    if (!CourseSkylineKinds.All.Contains(kind)) throw kinds.Error($"has an unknown kind \"{kind}\"");
                    spec.Kinds[kind] = AtLeast(kinds[kind], 1f, 0f);
                }
            var weights = At(n, "types");
            if (!weights.IsNull)
                foreach (string type in weights.Keys.OrderBy(k => k, StringComparer.Ordinal))
                {
                    if (!types.Any(t => t.Id == type)) throw weights.Error($"names the type \"{type}\", which is not in types");
                    spec.Types[type] = (AtLeast(At(weights[type], "ring"), 0f, 0f), AtLeast(At(weights[type], "downtown"), 0f, 0f));
                }
            if (types.Sum(t => spec.WeightOf(t, 0)) <= 0 || types.Sum(t => spec.WeightOf(t, 1)) <= 0) throw n.Error("leaves no type with a ring and a downtown weight above 0");
            return spec;
        }

        public float KindScale(string kind) => Kinds.TryGetValue(kind, out float scale) ? scale : 1f;

        public float WeightOf(CourseTypeSpec type, double downtown)
        {
            if (!Types.TryGetValue(type.Id, out var w)) return type.WeightAt(downtown);
            return (float)(w.ring + (w.downtown - w.ring) * downtown);
        }
    }

    sealed class CourseCityStreetSpec
    {
        public float Road, Walk, Reach, LowReach, TreeEvery, LampEvery, CarChance, StopChance, LowTrees, Kerb;
        public uint RoadTint, PavementTint, Line, Crossing, Grass, Path, Trunk, Lamp, Shelter, Fence, Wall, Yard, Gantry;
        public List<uint> Crowns, Cars, Containers;

        public static CourseCityStreetSpec Read(JsonNode n, float gap)
        {
            var spec = new CourseCityStreetSpec
            {
                Road = AtLeast(At(n, "road"), 7f, 4f), Walk = AtLeast(At(n, "walk"), 2f, .8f), Kerb = AtLeast(At(n, "kerb"), .16f, .05f),
                Reach = AtLeast(At(n, "reach"), 160f, 0f), LowReach = AtLeast(At(n, "lowReach"), 110f, 0f),
                TreeEvery = AtLeast(At(n, "treeEvery"), 12f, 4f), LampEvery = AtLeast(At(n, "lampEvery"), 24f, 6f),
                CarChance = Fraction(At(n, "carChance"), .5f), StopChance = Fraction(At(n, "stopChance"), .12f), LowTrees = Fraction(At(n, "lowTrees"), .5f),
                RoadTint = Tint(At(n, "roadTint"), 0x5B5F64), PavementTint = Tint(At(n, "pavementTint"), 0xBDB7AC), Line = Tint(At(n, "line"), 0xE8E4D8),
                Crossing = Tint(At(n, "crossing"), 0xECE8DE), Grass = Tint(At(n, "grass"), 0x7FA05A), Path = Tint(At(n, "path"), 0xCFC4AE),
                Trunk = Tint(At(n, "trunk"), 0x6B5440), Lamp = Tint(At(n, "lamp"), 0x3E4246), Shelter = Tint(At(n, "shelter"), 0x4E5A62),
                Fence = Tint(At(n, "fence"), 0x3A4A3E), Wall = Tint(At(n, "wall"), 0xB0A898), Yard = Tint(At(n, "yard"), 0x9A968E), Gantry = Tint(At(n, "gantry"), 0xD9A23A),
                Crowns = Tints(At(n, "crowns"), 0x5E8A4A), Cars = Tints(At(n, "cars"), 0x8A8F94), Containers = Tints(At(n, "containers"), 0x7A7F84),
            };
            if (spec.Road + 2 * spec.Walk > gap + 1e-4f) throw At(n, "road").Error($"and two pavements need {spec.Road + 2 * spec.Walk} m, more than the {gap} m gap between lots");
            if (spec.LowReach > spec.Reach) throw At(n, "lowReach").Error("must be at most reach");
            return spec;
        }

        static uint Tint(JsonNode node, uint fallback) => SurroundingsPlan.OptionalTint(node) ?? fallback;

        static List<uint> Tints(JsonNode node, uint fallback)
        {
            var list = node.IsNull ? new List<uint>() : node.Items.Select(SurroundingsPlan.Tint).ToList();
            if (list.Count == 0) list.Add(fallback);
            return list;
        }
    }
}
