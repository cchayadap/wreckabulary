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
        public List<uint> Trim, Caps, Accents, Bases, Roofs, Awnings, Lit, Bold;
        public uint Roof, Pavement, Tank, Crane, Face, Hands, Metal;
        public List<string> ThinCards;
        public float KeepDowntown, KeepRing;
        public int StoreysPerBand, FarStoreysPerBand;

        public float LotLimit => Cell - Gap - 2 * Plaza;

        public static CourseSkylineSpec Read(JsonNode n)
        {
            var low = At(n, "low");
            var marks = At(n, "landmarks");
            var spec = new CourseSkylineSpec
            {
                Cell = AtLeast(At(n, "cell"), 30f, 12f), Jitter = AtLeast(At(n, "jitter"), 3f, 0f), Gap = AtLeast(At(n, "gap"), 4f, 2f),
                Plaza = AtLeast(At(n, "plaza"), 1f, .8f), CardGap = AtLeast(At(n, "cardGap"), 4f, 0f), Inset = AtLeast(At(n, "inset"), .3f, .05f),
                DetailReach = AtLeast(At(n, "detailReach"), 190f, 0f),
                Storey = Span(At(n, "storey"), 3.5f, MinStorey), Plinth = Span(At(n, "plinth"), 4f, MinStorey),
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
                Trim = Tints(At(n, "trim")),
                Caps = Tints(At(n, "caps")),
                Accents = Tints(At(n, "accents")),
                Bases = Tints(At(n, "bases")),
                Roofs = Tints(At(n, "roofs")),
                Awnings = Tints(At(n, "awnings")),
                Lit = Tints(At(n, "lit")),
                Bold = Tints(At(marks, "bold")),
                Metal = SurroundingsPlan.OptionalTint(At(n, "metal")) ?? SurroundingsSlots.NoTint,
                Roof = SurroundingsPlan.OptionalTint(At(n, "roof")) ?? SurroundingsSlots.NoTint,
                Pavement = SurroundingsPlan.OptionalTint(At(n, "pavement")) ?? SurroundingsSlots.NoTint,
                Tank = SurroundingsPlan.OptionalTint(At(n, "tank")) ?? SurroundingsSlots.NoTint,
                Crane = SurroundingsPlan.OptionalTint(At(At(marks, "crane"), "tint")) ?? SurroundingsSlots.NoTint,
                Face = SurroundingsPlan.OptionalTint(At(At(marks, "clock"), "face")) ?? SurroundingsSlots.NoTint,
                Hands = SurroundingsPlan.OptionalTint(At(At(marks, "clock"), "hands")) ?? SurroundingsSlots.NoTint,
                ThinCards = Names(At(n, "thinCards"), false),
                KeepDowntown = Fraction(At(low, "keepDowntown"), .5f), KeepRing = Fraction(At(low, "keepRing"), .3f),
                StoreysPerBand = Math.Max(1, At(low, "storeysPerBand").Int(3)),
                FarStoreysPerBand = Math.Max(1, At(n, "farStoreysPerBand").Int(2)),
            };
            if (spec.Trim.Count == 0) spec.Trim.Add(SurroundingsSlots.NoTint);
            if (spec.Caps.Count == 0) spec.Caps.Add(spec.Roof);
            if (spec.Accents.Count == 0) spec.Accents.AddRange(spec.Trim);
            if (spec.Bases.Count == 0) spec.Bases.AddRange(spec.Trim);
            if (spec.Roofs.Count == 0) spec.Roofs.Add(spec.Roof);
            if (spec.Awnings.Count == 0) spec.Awnings.AddRange(spec.Accents);
            if (spec.Lit.Count == 0) spec.Lit.Add(SurroundingsSlots.NoTint);
            if (spec.Metal == SurroundingsSlots.NoTint) spec.Metal = spec.Trim[0];
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

        public CourseFacadeSpec Facade(IReadOnlyList<string> ids, double pick)
        {
            var list = ids.Count > 0 ? ids : Facades.Keys.ToList();
            return Facades[list[Math.Min(list.Count - 1, (int)(pick * list.Count))]];
        }

        static List<uint> Tints(JsonNode node) => node.IsNull ? new List<uint>() : node.Items.Select(SurroundingsPlan.Tint).ToList();
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
        const float MinFinSpacing = 1.5f;

        public string Id;
        public (float min, float max) Glass;
        public List<uint> Walls, Panes, Accents;
        public bool Piers;
        public float Fins;

        public static CourseFacadeSpec Read(string id, JsonNode n)
        {
            var spec = new CourseFacadeSpec
            {
                Id = id, Glass = Span(At(n, "glass"), .5f, .1f),
                Walls = At(n, "walls").Items.Select(SurroundingsPlan.Tint).ToList(),
                Panes = At(n, "panes").Items.Select(SurroundingsPlan.Tint).ToList(),
                Accents = Has(n, "accents") ? At(n, "accents").Items.Select(SurroundingsPlan.Tint).ToList() : new List<uint>(),
                Piers = At(n, "piers").Bool(false), Fins = At(n, "fins").Float(0f),
            };
            if (spec.Fins != 0 && spec.Fins < MinFinSpacing) throw At(n, "fins").Error($"must be 0 or a spacing of at least {MinFinSpacing} m");
            if (spec.Glass.max > .9f) throw At(n, "glass").Error("must stay at or below 0.9 so wall bands show");
            if (spec.Walls.Count == 0) throw At(n, "walls").Error("must list at least one colour");
            if (spec.Panes.Count == 0) spec.Panes.Add(SurroundingsSlots.NoTint);
            return spec;
        }
    }
}
