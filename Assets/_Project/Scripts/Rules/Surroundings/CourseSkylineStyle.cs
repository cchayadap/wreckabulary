using System;
using System.Collections.Generic;
using System.Linq;
using static Wreckabulary.Rules.Surroundings.CourseSurroundingsStyle;

namespace Wreckabulary.Rules.Surroundings
{
    public static class CourseSkylineTypes
    {
        public const string Block = "block", Loft = "loft", LPlan = "lplan", UPlan = "uplan", Slab = "slab", Deco = "deco", Round = "round", Curtain = "curtain", Twin = "twin";
        public const string Supertall = "supertall", Needle = "needle", Clock = "clock";

        public static readonly IReadOnlyList<string> Plain = new[] { Block, Loft, LPlan, UPlan, Slab, Deco, Round, Curtain, Twin };
        public static readonly IReadOnlyList<string> Landmarks = new[] { Supertall, Needle, Clock };
    }

    public static class CourseSkylineKinds
    {
        public const string Neutral = "neutral", Brick = "brick", Glass = "glass";

        public static readonly IReadOnlyList<string> All = new[] { Neutral, Brick, Glass };
    }

    sealed class CourseSkylineSpec
    {
        public const float MinStorey = 2.5f, MinHeight = 8f, MaxHeight = 300f;

        public float Cell, Jitter, Gap, Plaza, CardGap, Inset, DetailReach;
        public (float min, float max) Storey, Plinth;
        public CourseDowntownSpec Downtown;
        public CourseSkylineBand Ring;
        public readonly Dictionary<string, CourseFacadeSpec> Facades = new Dictionary<string, CourseFacadeSpec>(StringComparer.Ordinal);
        public readonly List<CourseTypeSpec> Types = new List<CourseTypeSpec>();
        public CourseLandmarkHeights Supertall, Needle, Clock;
        public float NeedleChance, CraneChance;
        public List<string> SupertallFacades, ClockFacades, NeedleFacades;
        public List<uint> Plant, Caps, Roofs, Awnings, Signs, Crowns, Bold, ShopGlass;
        public uint Pavement, Tank, Crane, Face, Hands, Metal, Mast, HazeTint;
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
            return spec;
        }

        public CourseFacadeSpec Facade(IReadOnlyList<string> ids, double pick, double downtown = 0)
        {
            var list = (ids.Count > 0 ? ids : Facades.Keys.ToList()).Select(id => Facades[id]).ToList();
            double total = list.Sum(f => f.WeightAt(downtown));
            if (total <= 0) return list[Math.Min(list.Count - 1, (int)(pick * list.Count))];
            double at = pick * total;
            foreach (var facade in list)
            {
                double w = facade.WeightAt(downtown);
                if (w <= 0) continue;
                at -= w;
                if (at < 0) return facade;
            }
            return list.Last(f => f.WeightAt(downtown) > 0);
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
}
