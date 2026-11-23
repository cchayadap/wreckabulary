using System;
using System.Collections.Generic;
using System.Linq;
using GameStudioKit.Determinism;

namespace Wreckabulary.Rules.Surroundings
{
    sealed class CourseTowerShape
    {
        const int Draws = 64;
        const float PlazaHeight = .2f, PlinthOut = .6f, ShopLift = .6f, ShopShare = .5f, ShopProud = .1f, RoofSlab = .5f;
        const float ParapetOut = .15f, ParapetHeight = 1.1f, RibProud = .12f, PierWidth = .8f, PierSpacing = 5.5f, CornerPier = 1f;
        const float FinWidth = .3f, MullionWidth = .2f, MullionSpacing = 2.6f, MinSegment = 6f, RoofMargin = 1.2f;
        const float HutWidth = 3.2f, HutDepth = 3.8f, HutHeight = 2.8f, AcWidth = 2f, AcDepth = 1.3f, AcHeight = 1.1f, AntennaSize = .4f;
        const float StandSize = 2.6f, StandHeight = 2f, TankSize = 3f, TankHeight = 3.4f, TankCap = 1.5f, ChimneySize = 1.4f;
        const float CurtainBandShare = .12f, SpireBase = 1.8f, TwinShare = .38f, TwinCap = 170f, BridgeLift = .62f;
        const float ClockStage = 7f, ClockRoof = 6.5f, ClockSpire = 5f, ClockFace = 5.2f, FaceDepth = .24f, FaceProud = .12f, HandDepth = .14f;
        const float NeedleShaft = 7f, NeedleUpper = 3.2f, PodLimit = 20f, PodFlare = 3.2f, PodSkirt = 6f, PodGlass = 5f, PodBand = 1.2f, PodRoof = 3f;
        const float CraneRise = 22f, CraneMast = 1.6f, CraneBeam = 1.2f, CraneLine = 12f, SkeletonColumn = .8f, SkeletonSlab = .3f;
        const float AwningHeight = .7f, AwningShare = .78f, SignDepth = .22f, SignLeast = .9f, PaneProud = .3f, PaneLeast = 2.4f, PaneMost = 6f;
        const int MaxPiers = 4, MaxFins = 7, MaxMullions = 8, SkeletonStoreys = 3, MostPanes = 3;

        readonly CourseSkylineSpec spec;
        readonly CourseSkylineLot lot;
        readonly float ground;
        readonly bool full, rich;
        readonly List<SkylineBox> boxes = new List<SkylineBox>();
        readonly float[] draw = new float[Draws];
        Pcg32 extra;
        Flat across, along;
        float storey, glass, lotWidth, lotDepth;
        int accentEvery, segment;
        uint trim, cap, shop;

        public CourseTowerShape(CourseSkylineSpec spec, CourseSkylineLot lot, float ground, bool full, bool rich)
        {
            this.spec = spec;
            this.lot = lot;
            this.ground = ground;
            this.full = full;
            this.rich = full && rich;
        }

        float R(int k) => draw[k];

        static float Lerp((float min, float max) range, float t) => range.min + (range.max - range.min) * t;

        T Pick<T>(IReadOnlyList<T> list, int k) => list[Math.Min(list.Count - 1, (int)(R(k) * list.Count))];

        static float Q(double value) => (float)(Math.Round(value / .001, MidpointRounding.AwayFromZero) * .001);

        float H => (float)lot.Height;

        public List<SkylineBox> Build(Pcg32 rng)
        {
            for (int k = 0; k < Draws; k++) draw[k] = rng.NextFloat();
            extra = rng;
            along = Flat.Heading(lot.Yaw);
            across = along.Right;
            lotWidth = (float)lot.Lot.HalfWidth * 2;
            lotDepth = (float)lot.Lot.HalfDepth * 2;
            storey = Lerp(spec.Storey, R(1));
            glass = Lerp(lot.Facade.Glass, R(2));
            trim = Pick(spec.Trim, 3);
            cap = lot.Landmark != null ? lot.Accent : Pick(spec.Caps, 7);
            shop = Pick(spec.Lit, 37);
            accentEvery = 3 + (int)(R(38) * 4);
            switch (lot.Type)
            {
                case CourseSkylineTypes.Loft: Loft(); break;
                case CourseSkylineTypes.LPlan: Wings(false); break;
                case CourseSkylineTypes.UPlan: Wings(true); break;
                case CourseSkylineTypes.Slab: Slab(); break;
                case CourseSkylineTypes.Deco: Deco(H, false); break;
                case CourseSkylineTypes.Round: Round(); break;
                case CourseSkylineTypes.Curtain: Curtain(); break;
                case CourseSkylineTypes.Twin: Twin(); break;
                case CourseSkylineTypes.Supertall: Supertall(); break;
                case CourseSkylineTypes.Needle: Needle(); break;
                case CourseSkylineTypes.Clock: Clock(); break;
                default: Block(); break;
            }
            return boxes;
        }

        void Add(string part, string slot, float lx, float lz, float y, float width, float depth, float height, uint tint, bool top, int sides = 4, float taper = 1f)
        {
            if (width <= 0 || depth <= 0 || height <= 0) return;
            if (!full && slot == CourseSkyline.MetalSlot) { slot = CourseSkyline.WallSlot; tint = trim; }
            var at = lot.At + across * lx + along * lz;
            boxes.Add(new SkylineBox
            {
                Part = part, Slot = slot, X = Q(at.X), Y = Q(y), Z = Q(at.Z), Width = Q(width), Depth = Q(depth), Height = Q(height),
                Yaw = (float)lot.Yaw, Tint = tint, Top = top, Sides = sides, Taper = taper,
            });
        }

        float PlinthHeight() => Math.Min(Lerp(spec.Plinth, R(0)), H * .25f);

        void Plaza()
        {
            if (rich) Add("plaza", CourseSkyline.WallSlot, 0, 0, ground, lotWidth, lotDepth, PlazaHeight, spec.Pavement, true);
        }

        void Plinth(float ox, float oz, float w, float d, float height, bool front = true)
        {
            Add("plinth", CourseSkyline.WallSlot, ox, oz, ground, w + 2 * PlinthOut, d + 2 * PlinthOut, height, lot.Base, true);
            if (!rich) return;
            float shopHeight = (height - ShopLift) * ShopShare, shopTop = ground + ShopLift + shopHeight;
            Add("shop", CourseSkyline.LitSlot, ox, oz, ground + ShopLift, w + 2 * (PlinthOut + ShopProud), d + 2 * (PlinthOut + ShopProud), shopHeight, shop, false);
            if (!front) return;
            float face = d * .5f + PlinthOut + ShopProud, reach = Math.Min(lotDepth * .5f, d * .5f + spec.Plaza) - .02f - face;
            if (reach <= .05f) return;
            float span = (w + 2 * PlinthOut) * AwningShare;
            foreach (int side in new[] { -1, 1 }) Add("awning", CourseSkyline.WallSlot, ox, oz + side * (face + reach * .5f), shopTop, span, reach, AwningHeight, lot.Awning, true);
            float room = ground + height - (shopTop + AwningHeight) - .15f;
            if (room < SignLeast || R(39) < .35f) return;
            float signWidth = Math.Min(w * .4f, 3f + 3f * R(40)), signSide = R(41) < .5f ? -1 : 1, signX = (w * .5f - signWidth * .5f - 1f) * (R(42) * 2 - 1);
            Add("sign", CourseSkyline.WallSlot, ox + signX, oz + signSide * (d * .5f + PlinthOut + SignDepth * .5f), shopTop + AwningHeight + .1f, signWidth, SignDepth, Math.Min(1.6f, room), Pick(spec.Awnings, 43), true);
        }

        int BandEvery(int storeys) => rich ? storeys : full ? spec.FarStoreysPerBand * storeys : spec.StoreysPerBand * storeys;

        void Floors(float ox, float oz, float y, float w, float d, float height, int sides = 4, int every = 1, float share = -1f, bool ribs = true)
        {
            uint pane = segment % 2 == 1 ? lot.Pane2 : lot.Pane;
            Add("glass", CourseSkyline.GlassSlot, ox, oz, y, w - 2 * spec.Inset, d - 2 * spec.Inset, height, pane, false, sides);
            int storeys = Math.Max(2, (int)Math.Round(height / storey));
            int per = BandEvery(every);
            int bands = Math.Max(1, (storeys + per - 1) / per);
            float step = height / bands, band = step * (share > 0 ? share : 1 - glass);
            int accent = Math.Max(2, (int)Math.Round(accentEvery / (float)per));
            for (int k = 0; k < bands; k++)
                Add("band", CourseSkyline.WallSlot, ox, oz, y + k * step, w, d, band, k % accent == accent - 1 || (k == 0 && bands > 2) ? lot.Accent : lot.Wall, false, sides);
            if (rich && sides == 4 && bands >= 3) Panes(ox, oz, y, w, d, step, band, bands, pane);
            if (!ribs || !rich || sides != 4) return;
            if (lot.Facade.Piers) Ribs("pier", ox, oz, y, w, d, height, PierWidth, PierSpacing, MaxPiers, true, lot.Wall, lot.Wall);
            if (lot.Facade.Fins > 0) Ribs("fin", ox, oz, y, w, d, height, FinWidth, lot.Facade.Fins, MaxFins, false, lot.Wall, lot.Accent);
        }

        void Panes(float ox, float oz, float y, float w, float d, float step, float band, int bands, uint pane)
        {
            int count = 1 + (int)(extra.NextFloat() * MostPanes);
            float depth = spec.Inset * PaneProud * 2 + .02f, inner = spec.Inset - depth * .5f + .01f;
            for (int n = 0; n < count; n++)
            {
                int face = (int)(extra.NextFloat() * 4) % 4, row = 1 + (int)(extra.NextFloat() * (bands - 1)) % (bands - 1);
                float t = extra.NextFloat(), size = PaneLeast + (PaneMost - PaneLeast) * extra.NextFloat();
                uint other = pane == lot.Pane ? lot.Pane2 : lot.Pane;
                bool lit = extra.NextFloat() < .65f || other == pane;
                uint tint = lit ? Pick(spec.Lit, 44 + n) : other;
                string slot = lit ? CourseSkyline.LitSlot : CourseSkyline.GlassSlot;
                bool onZ = face < 2;
                float span = (onZ ? w : d) - 2 * spec.Inset - 1f, width = Math.Min(size, span);
                if (width < PaneLeast) continue;
                float along = (span - width) * (t - .5f), offset = (face % 2 == 0 ? -1 : 1) * ((onZ ? d : w) * .5f - inner);
                float py = y + (row - 1) * step + band, height = step - band;
                if (height <= .2f) continue;
                if (onZ) Add("pane", slot, ox + along, oz + offset, py, width, depth, height, tint, false);
                else Add("pane", slot, ox + offset, oz + along, py, depth, width, height, tint, false);
            }
        }

        void Ribs(string part, float ox, float oz, float y, float w, float d, float height, float width, float spacing, int most, bool corners, uint tint, uint second)
        {
            float rib = spec.Inset + RibProud * 2;
            foreach (int side in new[] { -1, 1 })
            {
                float faceZ = side * (d * .5f + RibProud - rib * .5f), faceX = side * (w * .5f + RibProud - rib * .5f);
                int n = 0;
                foreach (float t in Spots(w, spacing, most)) Add(part, CourseSkyline.WallSlot, ox + t, oz + faceZ, y, width, rib, height, n++ % 2 == 0 ? tint : second, false);
                n = 0;
                foreach (float t in Spots(d, spacing, most)) Add(part, CourseSkyline.WallSlot, ox + faceX, oz + t, y, rib, width, height, n++ % 2 == 0 ? tint : second, false);
                if (!corners) continue;
                foreach (int other in new[] { -1, 1 })
                    Add(part, CourseSkyline.WallSlot, ox + side * (w * .5f + RibProud - CornerPier * .5f), oz + other * (d * .5f + RibProud - CornerPier * .5f), y, CornerPier, CornerPier, height, tint, false);
            }
        }

        static IEnumerable<float> Spots(float face, float spacing, int most)
        {
            int count = Math.Max(1, Math.Min(most, (int)(face / spacing) - 1));
            for (int i = 0; i < count; i++) yield return -face * .5f + face * (i + 1) / (count + 1);
        }

        void Roof(float ox, float oz, float y, float w, float d, int sides = 4)
        {
            Add("roof", CourseSkyline.WallSlot, ox, oz, y, w, d, RoofSlab, lot.Roof, true, sides);
            if (rich) Add("parapet", CourseSkyline.WallSlot, ox, oz, y, w + 2 * ParapetOut, d + 2 * ParapetOut, ParapetHeight, lot.Accent, false, sides);
        }

        List<float> Shares(int count)
        {
            var shares = new List<float>();
            if (count == 1) shares.Add(1);
            else if (count == 2) { float first = .6f + .2f * R(5); shares.Add(first); shares.Add(1 - first); }
            else
            {
                float left = 1, first = .42f + .12f * R(5);
                for (int s = 0; s < count - 1; s++)
                {
                    float share = s == 0 ? first : left * (.5f + .15f * R(5 + s));
                    shares.Add(share);
                    left -= share;
                }
                shares.Add(left);
            }
            return shares;
        }

        int SegmentCount(float body, int most)
        {
            int count = H < 30 ? 1 : H < 55 ? 1 + (int)(R(4) * 2) : 1 + (int)(R(4) * most);
            return Math.Max(1, Math.Min(count, (int)((body - RoofSlab) / (2 * storey + RoofSlab))));
        }

        (float y, float ox, float oz, float w, float d) Stack(float w, float d, float y, float body, int count, float shrink, bool centred, int sides, Action<float, float, float, float, float, float> floors)
        {
            var shares = Shares(count);
            float free = body - count * RoofSlab, ox = 0, oz = 0;
            for (int s = 0; s < count; s++)
            {
                if (s > 0)
                {
                    float nw = Math.Max(MinSegment, w * (shrink + .14f * R(8 + s * 4))), nd = sides == 4 ? Math.Max(MinSegment, d * (shrink + .14f * R(9 + s * 4))) : nw;
                    if (!centred)
                    {
                        ox += (w - nw) * .5f * (R(10 + s * 4) * 2 - 1) * .8f;
                        oz += (d - nd) * .5f * (R(11 + s * 4) * 2 - 1) * .8f;
                    }
                    w = nw;
                    d = nd;
                }
                float height = shares[s] * free;
                segment = s;
                floors(ox, oz, y, w, d, height);
                Roof(ox, oz, y + height, w, d, sides);
                y += height + RoofSlab;
            }
            segment = 0;
            return (y, ox, oz, w, d);
        }

        void Block()
        {
            float w = (float)lot.Width, d = (float)lot.Depth, plinth = PlinthHeight();
            Plaza();
            Plinth(0, 0, w, d, plinth);
            var top = Stack(w, d, ground + plinth, H - plinth, SegmentCount(H - plinth, 3), .68f, false, 4, (ox, oz, y, sw, sd, h) => Floors(ox, oz, y, sw, sd, h));
            Crown(top.y, top.ox, top.oz, top.w, top.d);
        }

        void Crown(float y, float ox, float oz, float w, float d)
        {
            if (lot.Crane && rich) { Construction(y, ox, oz, w, d); return; }
            float style = R(24);
            bool masonry = lot.Facade.Piers || lot.Facade.Glass.max < .6f;
            if (masonry && style < .22f) { Add("pyramid", CourseSkyline.WallSlot, ox, oz, y, w, d, Math.Min(w, d) * .45f, cap, false, 4, 0); return; }
            if (lot.Downtown && H >= 60 && style < .6f)
            {
                Add("crown", CourseSkyline.WallSlot, ox, oz, y, w * .62f, d * .62f, 3.5f, lot.Accent, true);
                Add("crown", CourseSkyline.WallSlot, ox, oz, y + 3.5f, w * .36f, d * .36f, 4f, cap, true);
                Antenna(ox, oz, y + 7.5f);
                return;
            }
            Details(ox, oz, y, w, d);
            if (lot.Downtown && H >= 50) Antenna(ox, oz, y);
        }

        void Antenna(float ox, float oz, float y)
        {
            if (rich && R(25) < .5f) Add("antenna", CourseSkyline.MetalSlot, ox, oz, y, AntennaSize, AntennaSize, 8 + 10 * R(26), spec.Metal, true);
        }

        List<(float x, float z)> Quadrants(float ox, float oz, float w, float d)
        {
            int first = (int)(R(27) * 4) % 4;
            return Enumerable.Range(0, 4).Select(k => (first + k) % 4).Select(q => (ox + (q % 2 == 0 ? -w * .25f : w * .25f), oz + (q < 2 ? -d * .25f : d * .25f))).ToList();
        }

        void Details(float ox, float oz, float y, float w, float d)
        {
            if (!rich) return;
            float roomX = w * .5f - RoofMargin, roomZ = d * .5f - RoofMargin;
            if (roomX < AcWidth || roomZ < AcWidth) return;
            var quads = Quadrants(ox, oz, w, d);
            if (roomX >= HutWidth && roomZ >= HutDepth) Add("hut", CourseSkyline.WallSlot, quads[0].x, quads[0].z, y, HutWidth, HutDepth, HutHeight, trim, true);
            float tankChance = lot.Facade.Piers ? .7f : lot.Facade.Glass.max >= .7f ? .1f : .4f;
            if (R(28) < tankChance && roomX >= TankSize && roomZ >= TankSize) Tank(quads[3].x, quads[3].z, y);
            int units = 1 + (int)(R(29) * 3);
            for (int k = 0; k < units; k++)
            {
                var spot = quads[1 + k % 2];
                if (k == 2 && d * .25f + AcDepth * 1.9f > d * .5f - ParapetOut) continue;
                Add("ac", CourseSkyline.MetalSlot, spot.x, spot.z + (k == 2 ? AcDepth * 1.4f : 0), y, AcWidth, AcDepth, AcHeight, spec.Metal, true);
            }
        }

        void Tank(float x, float z, float y)
        {
            if (!full) return;
            Add("stand", CourseSkyline.MetalSlot, x, z, y, StandSize, StandSize, StandHeight, spec.Metal, true);
            Add("tank", CourseSkyline.WallSlot, x, z, y + StandHeight, TankSize, TankSize, TankHeight, spec.Tank, false, 8);
            Add("tank", CourseSkyline.WallSlot, x, z, y + StandHeight + TankHeight, TankSize, TankSize, TankCap, lot.Roof, false, 8, 0);
        }

        void Loft()
        {
            float w = (float)lot.Width, d = (float)lot.Depth, plinth = Math.Min(PlinthHeight(), 4f);
            Plaza();
            Plinth(0, 0, w, d, plinth);
            float body = H - plinth - RoofSlab, y = ground + plinth;
            Floors(0, 0, y, w, d, body);
            Roof(0, 0, y + body, w, d);
            var quads = Quadrants(0, 0, w, d);
            float roof = y + body + RoofSlab;
            Tank(quads[0].x, quads[0].z, roof);
            if (R(28) < .5f) Tank(quads[3].x, quads[3].z, roof);
            if (rich && R(35) < .6f) Add("chimney", CourseSkyline.WallSlot, quads[1].x, quads[1].z, roof, ChimneySize, ChimneySize, 5 + 4 * R(36), lot.Wall, true);
            if (rich) Add("hut", CourseSkyline.WallSlot, quads[2].x, quads[2].z, roof, HutWidth, HutDepth, HutHeight, trim, true);
        }

        void Wings(bool u)
        {
            float w = (float)lot.Width, d = (float)lot.Depth, plinth = Math.Min(PlinthHeight(), 4.5f);
            float back = d * (.38f + .14f * R(32)), side = w * (.32f + .14f * R(33)), flip = R(34) < .5f ? -1 : 1;
            var wings = new List<(float x, float z, float w, float d)> { (0, (d - back) * .5f, w, back) };
            float sideX = (w - side) * .5f, sideZ = -back * .5f, sideD = d - back;
            if (u) { wings.Add((-sideX, sideZ, side, sideD)); wings.Add((sideX, sideZ, side, sideD)); }
            else wings.Add((flip * sideX, sideZ, side, sideD));
            Plaza();
            float body = H - plinth - RoofSlab, y = ground + plinth;
            foreach (var wing in wings)
            {
                Plinth(wing.x, wing.z, wing.w, wing.d, plinth, false);
                Floors(wing.x, wing.z, y, wing.w, wing.d, body);
                Roof(wing.x, wing.z, y + body, wing.w, wing.d);
            }
            Details(wings[0].x, wings[0].z, y + body + RoofSlab, wings[0].w, wings[0].d);
            if (lot.Facade.Piers) Tank(wings[1].x, wings[1].z, y + body + RoofSlab);
        }

        void Slab()
        {
            float w = (float)lot.Width, d = (float)lot.Depth, plinth = PlinthHeight();
            Plaza();
            Plinth(0, 0, w, d, plinth);
            float body = H - plinth;
            int count = H > 70 && R(4) < .5f ? 2 : 1;
            var top = Stack(w, d, ground + plinth, body, count, .7f, false, 4, (ox, oz, y, sw, sd, h) => Floors(ox, oz, y, sw, sd, h));
            if (lot.Crane && rich) { Construction(top.y, top.ox, top.oz, top.w, top.d); return; }
            Add("penthouse", CourseSkyline.WallSlot, top.ox, top.oz, top.y, top.w * .5f, top.d * .6f, 4f, trim, true);
            Antenna(top.ox, top.oz, top.y + 4f);
        }

        void Deco(float height, bool landmark)
        {
            float w = Math.Min((float)lot.Width, (float)lot.Depth), plinth = PlinthHeight();
            float spire = landmark ? 30 + 12 * R(36) : 12 + 16 * R(36), crown = 11f;
            float body = (landmark ? height - spire - crown : height) - plinth;
            Plaza();
            Plinth(0, 0, w, w, plinth);
            int count = Math.Max(1, Math.Min(landmark ? 5 : 3 + (int)(R(4) * 3), (int)((body - RoofSlab) / (2 * storey + RoofSlab))));
            var top = Stack(w, w, ground + plinth, body, count, .74f, true, 4, (ox, oz, y, sw, sd, h) => Floors(ox, oz, y, sw, sd, h));
            if (lot.Crane && rich && !landmark) { Construction(top.y, top.ox, top.oz, top.w, top.d); return; }
            Steps(top.y, top.w, spire);
        }

        void Steps(float y, float w, float spire)
        {
            Add("crown", CourseSkyline.WallSlot, 0, 0, y, w * .7f, w * .7f, 4f, lot.Accent, true);
            Add("crown", CourseSkyline.WallSlot, 0, 0, y + 4f, w * .48f, w * .48f, 4f, lot.Wall, true);
            Add("crown", CourseSkyline.WallSlot, 0, 0, y + 8f, w * .3f, w * .3f, 3f, cap, true);
            Add("spire", CourseSkyline.WallSlot, 0, 0, y + 11f, SpireBase, SpireBase, spire, cap, false, 4, 0);
        }

        void Round()
        {
            int sides = R(31) < .5f ? 8 : 12;
            float s = Math.Min((float)lot.Width, (float)lot.Depth), plinth = PlinthHeight();
            Plaza();
            Plinth(0, 0, s, s, plinth);
            var top = Stack(s, s, ground + plinth, H - plinth, SegmentCount(H - plinth, 3), .8f, true, sides, (ox, oz, y, sw, sd, h) => Floors(ox, oz, y, sw, sd, h, sides));
            if (R(24) < .5f)
            {
                Add("crown", CourseSkyline.WallSlot, 0, 0, top.y, top.w, top.w, 6f, lot.Accent, true, sides, .55f);
                Antenna(0, 0, top.y + 6f);
                return;
            }
            Add("dome", CourseSkyline.GlassSlot, 0, 0, top.y, top.w * .9f, top.w * .9f, 5f, lot.Pane2, false, sides, .6f);
            Add("dome", CourseSkyline.GlassSlot, 0, 0, top.y + 5f, top.w * .54f, top.w * .54f, 4f, cap, false, sides, 0);
        }

        void CurtainFloors(float ox, float oz, float y, float w, float d, float height)
        {
            Floors(ox, oz, y, w, d, height, 4, 3, CurtainBandShare, false);
            if (rich) Ribs("mullion", ox, oz, y, w, d, height, MullionWidth, MullionSpacing, MaxMullions, false, lot.Wall, lot.Accent);
        }

        void Curtain()
        {
            float w = (float)lot.Width, d = (float)lot.Depth, plinth = PlinthHeight();
            Plaza();
            Plinth(0, 0, w, d, plinth);
            var top = Stack(w, d, ground + plinth, H - plinth, Math.Min(2, SegmentCount(H - plinth, 2)), .74f, false, 4, CurtainFloors);
            if (lot.Crane && rich) { Construction(top.y, top.ox, top.oz, top.w, top.d); return; }
            if (R(24) < .5f)
            {
                bool point = R(30) < .5f;
                Add("pyramid", CourseSkyline.GlassSlot, top.ox, top.oz, top.y, top.w, top.d, Math.Min(top.w, top.d) * (point ? .7f : .4f), lot.Pane2, !point, 4, point ? 0 : .3f);
                return;
            }
            Add("crown", CourseSkyline.WallSlot, top.ox, top.oz, top.y, top.w * .8f, top.d * .8f, 5f, lot.Accent, true, 4, .7f);
            Antenna(top.ox, top.oz, top.y + 5f);
        }

        void Twin()
        {
            float w = (float)lot.Width, d = (float)lot.Depth, t = Math.Min(w * TwinShare, d), plinth = PlinthHeight();
            float height = Math.Min(H, TwinCap), body = height - plinth - RoofSlab, shift = (w - t) * .5f, y = ground + plinth;
            Plaza();
            Plinth(0, 0, w, t, plinth);
            bool pointed = R(24) < .5f;
            foreach (float x in new[] { -shift, shift })
            {
                segment = x > 0 ? 1 : 0;
                Floors(x, 0, y, t, t, body);
                Roof(x, 0, y + body, t, t);
                if (pointed) Add("pyramid", CourseSkyline.WallSlot, x, 0, y + body + RoofSlab, t, t, t * 1.2f, cap, false, 4, 0);
                else
                {
                    Add("crown", CourseSkyline.WallSlot, x, 0, y + body + RoofSlab, t * .6f, t * .6f, 4f, lot.Accent, true);
                    Add("spire", CourseSkyline.WallSlot, x, 0, y + body + RoofSlab + 4f, SpireBase * .8f, SpireBase * .8f, 10 + 8 * R(36), cap, false, 4, 0);
                }
            }
            segment = 0;
            float span = w - 2 * t;
            if (span <= 1) return;
            float lift = ground + height * BridgeLift;
            Add("bridge", CourseSkyline.WallSlot, 0, 0, lift - RoofSlab, span + .4f, t * .5f, RoofSlab, lot.Accent, true);
            Add("bridge", CourseSkyline.GlassSlot, 0, 0, lift, span + .4f, t * .45f, storey * 2, lot.Pane2, false);
            Add("bridge", CourseSkyline.WallSlot, 0, 0, lift + storey * 2, span + .4f, t * .5f, RoofSlab, lot.Accent, true);
        }

        void Supertall()
        {
            int design = Math.Min(2, (int)(R(31) * 3));
            if (design == 2) { Deco(H, true); return; }
            float s = Math.Min((float)lot.Width, (float)lot.Depth), plinth = Math.Max(PlinthHeight(), 2 * storey);
            float spire = 30 + 15 * R(36), crown = 8f, body = H - plinth - spire - crown;
            Plaza();
            Plinth(0, 0, s, s, plinth);
            int sides = design == 1 ? 8 : 4;
            var top = design == 1
                ? Stack(s, s, ground + plinth, body, 3, .8f, true, 8, (ox, oz, y, sw, sd, h) => Floors(ox, oz, y, sw, sd, h, 8))
                : Stack(s, s, ground + plinth, body, 5, .82f, true, 4, CurtainFloors);
            Add("crown", CourseSkyline.WallSlot, 0, 0, top.y, top.w, top.w, crown, lot.Accent, true, sides, .55f);
            Add("spire", CourseSkyline.WallSlot, 0, 0, top.y + crown, SpireBase * 1.4f, SpireBase * 1.4f, spire, cap, false, sides, 0);
        }

        void Clock()
        {
            float s = Math.Max(8f, Math.Min(11f, Math.Min((float)lot.Width, (float)lot.Depth) * .55f)), plinth = 5f, stage = s + 1.2f;
            float shaft = H - plinth - ClockStage - ClockRoof - ClockSpire, y = ground + plinth;
            Plaza();
            Plinth(0, 0, s + 4f, s + 4f, plinth);
            Floors(0, 0, y, s, s, shaft);
            float ys = y + shaft;
            Add("stage", CourseSkyline.WallSlot, 0, 0, ys, stage, stage, ClockStage, lot.Accent, true);
            float yc = ys + (ClockStage - ClockFace) * .5f, mid = yc + ClockFace * .5f, face = stage * .5f + FaceProud - FaceDepth * .5f, hand = face + FaceDepth * .5f + HandDepth * .5f;
            foreach (int side in new[] { -1, 1 })
            {
                Add("face", CourseSkyline.WallSlot, 0, side * face, yc, ClockFace, FaceDepth, ClockFace, spec.Face, false);
                Add("face", CourseSkyline.WallSlot, side * face, 0, yc, FaceDepth, ClockFace, ClockFace, spec.Face, false);
                if (!rich) continue;
                Add("hand", CourseSkyline.WallSlot, 0, side * hand, mid, .35f, HandDepth, 2.2f, spec.Hands, false);
                Add("hand", CourseSkyline.WallSlot, .8f, side * hand, mid - .15f, 1.6f, HandDepth, .3f, spec.Hands, false);
                Add("hand", CourseSkyline.WallSlot, side * hand, 0, mid, HandDepth, .35f, 2.2f, spec.Hands, false);
                Add("hand", CourseSkyline.WallSlot, side * hand, .8f, mid - .15f, HandDepth, 1.6f, .3f, spec.Hands, false);
            }
            Add("pyramid", CourseSkyline.WallSlot, 0, 0, ys + ClockStage, stage + .4f, stage + .4f, ClockRoof, cap, false, 4, 0);
            Add("spire", CourseSkyline.WallSlot, 0, 0, ys + ClockStage + ClockRoof - 1f, .8f, .8f, ClockSpire + 1f, cap, false, 4, 0);
        }

        void Needle()
        {
            float s = Math.Min((float)lot.Width, (float)lot.Depth), plinth = 6f, pod = Math.Min(PodLimit, Math.Min(lotWidth, lotDepth) - 2 * spec.Plaza - .4f);
            float shaftTop = ground + H * .72f, upper = H * .1f;
            Plaza();
            Plinth(0, 0, s, s, plinth);
            Add("shaft", CourseSkyline.WallSlot, 0, 0, ground + plinth, NeedleShaft, NeedleShaft, shaftTop - ground - plinth, lot.Wall, false, 8, .6f);
            Add("pod", CourseSkyline.WallSlot, 0, 0, shaftTop, pod / PodFlare, pod / PodFlare, PodSkirt, lot.Accent, false, 12, PodFlare);
            Add("pod", CourseSkyline.GlassSlot, 0, 0, shaftTop + PodSkirt, pod, pod, PodGlass, lot.Pane, false, 12);
            Add("pod", CourseSkyline.WallSlot, 0, 0, shaftTop + PodSkirt + PodGlass, pod, pod, PodBand, lot.Accent, false, 12);
            float roof = shaftTop + PodSkirt + PodGlass + PodBand;
            Add("pod", CourseSkyline.WallSlot, 0, 0, roof, pod, pod, PodRoof, lot.Accent, true, 12, .45f);
            Add("shaft", CourseSkyline.WallSlot, 0, 0, roof + PodRoof, NeedleUpper, NeedleUpper, upper, lot.Wall, false, 8, .7f);
            float mast = roof + PodRoof + upper;
            Add("mast", CourseSkyline.MetalSlot, 0, 0, mast, 1.2f, 1.2f, ground + H - mast, spec.Metal, false, 4, 0);
        }

        void Construction(float y, float ox, float oz, float w, float d)
        {
            float frame = SkeletonStoreys * storey;
            foreach (int sx in new[] { -1, 1 })
                foreach (int sz in new[] { -1, 1 })
                    Add("skeleton", CourseSkyline.WallSlot, ox + sx * (w * .5f - SkeletonColumn * .5f), oz + sz * (d * .5f - SkeletonColumn * .5f), y, SkeletonColumn, SkeletonColumn, frame, trim, false);
            for (int k = 1; k <= SkeletonStoreys; k++) Add("skeleton", CourseSkyline.WallSlot, ox, oz, y + k * storey - SkeletonSlab, w, d, SkeletonSlab, spec.Roof, true);
            float span = lotWidth - 1.2f, mastX = -span * .5f + span * .3f, jib = y + frame + CraneRise;
            Add("crane", CourseSkyline.WallSlot, mastX, oz, y, CraneMast, CraneMast, frame + CraneRise, spec.Crane, true);
            Add("crane", CourseSkyline.WallSlot, mastX, oz + CraneMast, jib - 3.5f, 2.2f, 2f, 2.2f, spec.Crane, true);
            Add("crane", CourseSkyline.WallSlot, 0, oz, jib, span, CraneBeam, CraneBeam, spec.Crane, true);
            Add("crane", CourseSkyline.WallSlot, -span * .5f + 1.4f, oz, jib - 2f, 2.4f, 1.8f, 2f, trim, true);
            Add("crane", CourseSkyline.MetalSlot, span * .5f - 1.5f, oz, jib - CraneLine, .15f, .15f, CraneLine, spec.Metal, false);
        }
    }
}
