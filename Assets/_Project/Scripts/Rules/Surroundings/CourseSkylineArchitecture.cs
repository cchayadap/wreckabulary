using System;
using System.Collections.Generic;
using System.Linq;
using GameStudioKit.Determinism;

namespace Wreckabulary.Rules.Surroundings
{
    sealed class CourseTowerShape
    {
        const int Draws = 64;
        const float PlazaHeight = .2f, PlinthOut = .4f, RoofSlab = .5f, ParapetOut = .12f, ParapetHeight = 1.1f, MinSegment = 6f, RoofMargin = 1.2f;
        const float LedgeOut = .18f, LedgeHeight = .45f, CorniceOut = .3f, CorniceHeight = .8f, PierOut = .14f, DecoPier = .55f, DecoPierOut = .28f;
        const float ShopBottom = .35f, ShopWidth = .78f, ShopHeight = .9f, ShopMargin = .7f, DoorWidth = 2.4f, DoorHeight = 3f, DoorDepth = .2f;
        const float AwningDepth = .55f, AwningHeight = .45f, AwningRise = .62f, BalconyDepth = .9f, BalconyHeight = 1.05f, BalconyShare = .85f;
        const float HutWidth = 3.2f, HutDepth = 3.8f, HutHeight = 2.8f, AcWidth = 2f, AcDepth = 1.3f, AcHeight = 1.1f, DuctWidth = .9f, DuctHeight = .8f, AntennaSize = .4f;
        const float StandSize = 2.6f, StandHeight = 2f, TankSize = 3f, TankHeight = 3.4f, TankCap = 1.5f, ChimneySize = 1.4f;
        const float BoardHeight = 4f, BoardDepth = .35f, BoardLift = 2.6f, BoardPost = .35f;
        const float SpireBase = 1.8f, TwinShare = .38f, TwinCap = 170f, BridgeLift = .62f, CrownLine = .6f;
        const float ClockStage = 7f, ClockRoof = 6.5f, ClockSpire = 5f, ClockFace = 5.2f, FaceDepth = .24f, FaceProud = .12f, HandDepth = .14f;
        const float NeedleShaft = 7f, NeedleUpper = 3.2f, PodLimit = 20f, PodFlare = 3.2f, PodSkirt = 6f, PodGlass = 5f, PodBand = 1.2f, PodRoof = 3f, MastBands = 3;
        const float CraneRise = 22f, CraneMast = 1.6f, CraneBeam = 1.2f, CraneLine = 12f, SkeletonColumn = .8f, SkeletonSlab = .3f;
        const float LedgeShade = 1.05f, PierShade = 1.03f, ParapetShade = .92f, DoorShade = .55f, BalconyShade = 1.06f;
        const float ShedBand = .3f, ShedTop = .4f, ShedSpacing = 3.6f, ShedWidth = .7f, ShedHeight = .55f, ShedMargin = 1.5f, ShedDoor = 4f, ShedDoorHeight = 4.5f;
        const float RidgeLift = 1.12f, RidgeTaper = .6f, RidgeHeight = 2.4f, StackSize = 3f, StackTaper = .7f, StackRise = 16f, StackEdge = 2.5f, VentSize = .9f, VentHeight = 1.2f, VentShift = 1.6f;
        const int SkeletonStoreys = 3, MostBalconyRows = 16, NearVents = 2;

        readonly CourseSkylineSpec spec;
        readonly CourseDistrictSpec district;
        readonly CourseSkylineLot lot;
        readonly float ground;
        readonly bool full, rich;
        readonly List<SkylineBox> boxes = new List<SkylineBox>();
        readonly float[] draw = new float[Draws];
        Flat across, along;
        float storey, lotWidth, lotDepth, groundFloor;
        int ledgeEvery;
        uint wall, trim, plinth, ledge, cap, plant;
        SkylineWindows body, shop;

        public CourseTowerShape(CourseSkylineSpec spec, CourseDistrictSpec district, CourseSkylineLot lot, float ground, bool full, bool rich)
        {
            this.spec = spec;
            this.district = district;
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

        bool Masonry => lot.Facade.Kind != CourseSkylineKinds.Glass;

        public List<SkylineBox> Build(Pcg32 rng)
        {
            for (int k = 0; k < Draws; k++) draw[k] = rng.NextFloat();
            along = Flat.Heading(lot.Yaw);
            across = along.Right;
            lotWidth = (float)lot.Lot.HalfWidth * 2;
            lotDepth = (float)lot.Lot.HalfDepth * 2;
            storey = Lerp(spec.Storey, R(1));
            groundFloor = Q(Math.Min(Lerp(spec.Plinth, R(0)), H * .25f));
            ledgeEvery = 4 + (int)(R(47) * 3);
            wall = lot.Wall;
            trim = CourseSkyline.Shade(wall, CourseSkyline.TrimShade);
            plinth = CourseSkyline.Shade(wall, CourseSkyline.PlinthShade);
            ledge = CourseSkyline.Shade(wall, LedgeShade);
            cap = lot.Landmark != null ? lot.Accent : Pick(spec.Caps, 7);
            plant = Pick(spec.Plant, 20);
            var f = lot.Facade;
            body = new SkylineWindows
            {
                Spacing = Q(Lerp(f.Spacing, R(2))), Storey = Q(storey), Width = Q(Lerp(f.Width, R(3))), Height = Q(Lerp(f.Height, R(4))),
                Margin = f.Margin, Bottom = .25f, Top = Q(storey * .45f), Glass = lot.Pane, Lit = Q(f.Lit * (.6f + .8f * R(5))), Seed = Q(R(6)),
            };
            float shopTop = Q(Math.Max(.6f, groundFloor * .2f));
            shop = new SkylineWindows
            {
                Spacing = Q(4.2f + R(37)), Storey = Q(Math.Max(1f, groundFloor - ShopBottom - shopTop) - .01f), Width = ShopWidth, Height = ShopHeight,
                Margin = ShopMargin, Bottom = ShopBottom, Top = Q(shopTop), Glass = Pick(spec.ShopGlass, 38), Lit = spec.ShopLit, Seed = Q(R(39)),
            };
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
                case CourseSkylineTypes.Shed: Shed(); break;
                default: Block(); break;
            }
            return boxes;
        }

        void Add(string part, float lx, float lz, float y, float width, float depth, float height, uint tint, bool top, int sides = 4, float taper = 1f, SkylineWindows windows = null)
        {
            if (width <= 0 || depth <= 0 || height <= 0) return;
            var at = lot.At + across * lx + along * lz;
            boxes.Add(new SkylineBox
            {
                Part = part, Slot = CourseSkyline.SkylineSlot, X = Q(at.X), Y = Q(y), Z = Q(at.Z), Width = Q(width), Depth = Q(depth), Height = Q(height),
                Yaw = (float)lot.Yaw, Tint = tint, Top = top, Sides = sides, Taper = taper, Windows = windows,
                Floor = Q(ground), Rise = Q(Math.Max(1f, H)), Haze = Q(lot.Haze),
            });
        }

        void Plaza()
        {
            if (rich) Add("plaza", 0, 0, ground, lotWidth, lotDepth, PlazaHeight, spec.Pavement, true);
        }

        void GroundFloor(float ox, float oz, float w, float d, bool front = true)
        {
            Add("plinth", ox, oz, ground, w + 2 * PlinthOut, d + 2 * PlinthOut, groundFloor, plinth, true, 4, 1, shop);
            if (!rich || !front) return;
            float face = d * .5f + PlinthOut, room = Math.Min(lotDepth * .5f, d * .5f + spec.Plaza) - .02f - face;
            foreach (int side in new[] { -1, 1 })
            {
                float doorX = (w * .5f - DoorWidth) * (R(40 + (side + 1) / 2) * 2 - 1) * .6f;
                Add("door", ox + doorX, oz + side * (face + DoorDepth * .5f), ground, DoorWidth, DoorDepth, Math.Min(DoorHeight, groundFloor - .4f), CourseSkyline.Shade(plinth, DoorShade), true);
                if (room < AwningDepth * .6f || R(42 + (side + 1) / 2) < .45f) continue;
                float span = Math.Min(w * .45f, 3f + 3f * R(44 + (side + 1) / 2)), awningX = (w * .5f - span * .5f - .6f) * (R(41) * 2 - 1);
                float depth = Math.Min(AwningDepth, room);
                Add("awning", ox + awningX, oz + side * (face + depth * .5f), ground + groundFloor * AwningRise, span, depth, AwningHeight, lot.Awning, true);
            }
        }

        int Rows(float height) => body.Rows(height);

        void Segment(float ox, float oz, float y, float w, float d, float height, int sides = 4, SkylineWindows windows = null)
        {
            windows ??= body;
            Add("body", ox, oz, y, w, d, height, wall, false, sides, 1, windows);
            if (!full) return;
            int rows = windows.Rows(height);
            int every = rich ? ledgeEvery : Math.Max(ledgeEvery, rows / 2 + 1);
            for (int k = every; k < rows; k += every)
                Add("ledge", ox, oz, y + windows.Bottom + k * windows.Storey - LedgeHeight * .5f, w + 2 * LedgeOut, d + 2 * LedgeOut, LedgeHeight, ledge, false, sides);
            Add("cornice", ox, oz, y + height - CorniceHeight, w + 2 * CorniceOut, d + 2 * CorniceOut, CorniceHeight, trim, false, sides);
            if (rich && sides == 4 && lot.Facade.Piers && windows.Margin >= .6f)
            {
                float pier = windows.Margin * .8f;
                foreach (int sx in new[] { -1, 1 })
                    foreach (int sz in new[] { -1, 1 })
                        Add("pier", ox + sx * (w * .5f + PierOut - pier * .5f), oz + sz * (d * .5f + PierOut - pier * .5f), y, pier, pier, height - CorniceHeight, CourseSkyline.Shade(wall, PierShade), false);
            }
        }

        void Roof(float ox, float oz, float y, float w, float d, int sides = 4)
        {
            Add("roof", ox, oz, y, w, d, RoofSlab, lot.Roof, true, sides);
            if (rich) Add("parapet", ox, oz, y, w + 2 * ParapetOut, d + 2 * ParapetOut, ParapetHeight, CourseSkyline.Shade(wall, ParapetShade), false, sides);
        }

        List<float> Shares(int count)
        {
            var shares = new List<float>();
            if (count == 1) shares.Add(1);
            else if (count == 2) { float first = .6f + .2f * R(9); shares.Add(first); shares.Add(1 - first); }
            else
            {
                float left = 1, first = .42f + .12f * R(9);
                for (int s = 0; s < count - 1; s++)
                {
                    float share = s == 0 ? first : left * (.5f + .15f * R(10 + Math.Min(s, 3)));
                    shares.Add(share);
                    left -= share;
                }
                shares.Add(left);
            }
            return shares;
        }

        int SegmentCount(float rest, int most)
        {
            int count = H < 30 ? 1 : H < 55 ? 1 + (int)(R(8) * 2) : 1 + (int)(R(8) * most);
            return Math.Max(1, Math.Min(count, (int)((rest - RoofSlab) / (3 * storey + RoofSlab))));
        }

        int Fit(int count, float rest)
        {
            while (count > 1 && Shares(count).Min() * (rest - count * RoofSlab) < 2 * storey + body.Bottom + body.Top) count--;
            return count;
        }

        (float y, float ox, float oz, float w, float d) Stack(float w, float d, float y, float rest, int count, float shrink, bool centred, int sides, Action<float, float, float, float, float, float> floors = null)
        {
            floors ??= (ox, oz, sy, sw, sd, h) => Segment(ox, oz, sy, sw, sd, h, sides);
            count = Fit(count, rest);
            var shares = Shares(count);
            float free = rest - count * RoofSlab, cx = 0, cz = 0;
            for (int s = 0; s < count; s++)
            {
                if (s > 0)
                {
                    int r = 48 + Math.Min(s - 1, 3) * 4;
                    float nw = Math.Max(MinSegment, w * (shrink + .14f * R(r))), nd = sides == 4 ? Math.Max(MinSegment, d * (shrink + .14f * R(r + 1))) : nw;
                    if (!centred)
                    {
                        cx += (w - nw) * .5f * (R(r + 2) * 2 - 1) * .8f;
                        cz += (d - nd) * .5f * (R(r + 3) * 2 - 1) * .8f;
                    }
                    w = nw;
                    d = nd;
                }
                float height = shares[s] * free;
                floors(cx, cz, y, w, d, height);
                Roof(cx, cz, y + height, w, d, sides);
                y += height + RoofSlab;
            }
            return (y, cx, cz, w, d);
        }

        void Block()
        {
            float w = (float)lot.Width, d = (float)lot.Depth;
            Plaza();
            GroundFloor(0, 0, w, d);
            var top = Stack(w, d, ground + groundFloor, H - groundFloor, SegmentCount(H - groundFloor, 3), .68f, false, 4);
            Crown(top.y, top.ox, top.oz, top.w, top.d);
        }

        void Crown(float y, float ox, float oz, float w, float d)
        {
            if (lot.Crane && rich) { Construction(y, ox, oz, w, d); return; }
            float style = R(14);
            if (Masonry && style < .22f) { Add("pyramid", ox, oz, y, w, d, Math.Min(w, d) * .4f, cap, false, 4, 0); return; }
            if (lot.Downtown && H >= 60 && style < .6f)
            {
                Add("crown", ox, oz, y, w * .62f, d * .62f, 3.5f, trim, true);
                Add("crown", ox, oz, y + 3.5f, w * .36f, d * .36f, 4f, CourseSkyline.Shade(wall, ParapetShade), true);
                Antenna(ox, oz, y + 7.5f);
                return;
            }
            Details(ox, oz, y, w, d);
            Billboard(ox, oz, y, w, d);
            if (lot.Downtown && H >= 50) Antenna(ox, oz, y);
        }

        void Antenna(float ox, float oz, float y)
        {
            if (rich && R(15) < .5f) Add("antenna", ox, oz, y, AntennaSize, AntennaSize, 8 + 10 * R(16), spec.Metal, true);
        }

        void Billboard(float ox, float oz, float y, float w, float d)
        {
            if (!rich || !lot.Downtown || lot.Landmark != null || H < 30 || H > 140 || R(44) >= .16f) return;
            float span = Math.Min(w * .7f, 12f), side = R(45) < .5f ? -1 : 1, z = oz + side * (d * .5f - RoofMargin - BoardDepth);
            foreach (int sx in new[] { -1, 1 }) Add("board", ox + sx * span * .35f, z, y, BoardPost, BoardPost, BoardLift, spec.Metal, true);
            Add("board", ox, z, y + BoardLift, span, BoardDepth, BoardHeight, lot.Sign, true);
        }

        List<(float x, float z)> Quadrants(float ox, float oz, float w, float d)
        {
            int first = (int)(R(19) * 4) % 4;
            return Enumerable.Range(0, 4).Select(k => (first + k) % 4).Select(q => (ox + (q % 2 == 0 ? -w * .25f : w * .25f), oz + (q < 2 ? -d * .25f : d * .25f))).ToList();
        }

        void Details(float ox, float oz, float y, float w, float d)
        {
            if (!rich) return;
            float roomX = w * .5f - RoofMargin, roomZ = d * .5f - RoofMargin;
            if (roomX < AcWidth || roomZ < AcWidth) return;
            var quads = Quadrants(ox, oz, w, d);
            if (roomX >= HutWidth && roomZ >= HutDepth) Add("hut", quads[0].x, quads[0].z, y, HutWidth, HutDepth, HutHeight, CourseSkyline.Shade(wall, ParapetShade), true);
            if (R(17) < (lot.Facade.Kind == CourseSkylineKinds.Brick ? .7f : .3f) && roomX >= TankSize && roomZ >= TankSize) Tank(quads[3].x, quads[3].z, y);
            int units = 1 + (int)(R(18) * 3);
            for (int k = 0; k < units; k++)
            {
                var spot = quads[1 + k % 2];
                if (k == 2 && d * .25f + AcDepth * 1.9f > d * .5f - ParapetOut) continue;
                Add("plant", spot.x, spot.z + (k == 2 ? AcDepth * 1.4f : 0), y, AcWidth, AcDepth, AcHeight, plant, true);
            }
            if (R(21) < .6f) Add("duct", ox, quads[2].z, y, Math.Min(w * .5f, 8f), DuctWidth, DuctHeight, plant, true);
            Vents(quads, y);
        }

        void Vents(List<(float x, float z)> quads, float y)
        {
            if (!rich || lot.Distance > spec.Streets.Reach) return;
            for (int k = 0; k < NearVents; k++)
                Add("vent", quads[k + 1].x + (k == 0 ? VentShift : -VentShift), quads[k + 1].z - VentShift, y, VentSize, VentSize, VentHeight, CourseSkyline.Shade(plant, .9f), true);
        }

        void Shed()
        {
            float w = (float)lot.Width, d = (float)lot.Depth, h = Math.Max(CourseSkylineSpec.MinHeight - RoofSlab, H - RoofSlab), band = h * ShedBand;
            var clerestory = new SkylineWindows
            {
                Spacing = Q(ShedSpacing + R(2)), Storey = Q(band - .01f), Width = ShedWidth, Height = ShedHeight, Margin = ShedMargin,
                Bottom = Q(h - band - ShedTop), Top = ShedTop, Glass = lot.Pane, Lit = 0, Seed = Q(R(6)),
            };
            Plaza();
            Add("body", 0, 0, ground, w, d, h, wall, false, 4, 1, clerestory);
            if (full) Add("cornice", 0, 0, ground + h - CorniceHeight * .6f, w + 2 * ParapetOut, d + 2 * ParapetOut, CorniceHeight * .6f, trim, false);
            Add("roof", 0, 0, ground + h, w, d, RoofSlab, lot.Roof, true);
            int ridges = 3 + (int)(R(25) * 2);
            float pitch = d / ridges;
            for (int k = 0; k < ridges; k++)
                Add("ridge", 0, -d * .5f + pitch * (k + .5f), ground + h + RoofSlab, w * .97f, pitch * .9f, Math.Min(RidgeHeight, pitch * .35f), CourseSkyline.Shade(lot.Roof, RidgeLift), true, 4, RidgeTaper);
            if (rich)
            {
                int doors = 2 + (int)(R(40) * 2);
                for (int k = 0; k < doors; k++)
                    Add("door", -w * .5f + w * (k + .5f) / doors, -(d * .5f + DoorDepth * .5f), ground, ShedDoor, DoorDepth, Math.Min(ShedDoorHeight, h * .6f), CourseSkyline.Shade(wall, DoorShade), true);
                Vents(Quadrants(0, 0, w, d), ground + h + RoofSlab);
            }
            if (R(26) < district.Chimneys)
            {
                float sx = R(27) < .5f ? -1 : 1, sz = R(29) < .5f ? -1 : 1;
                Add("stack", sx * (w * .5f - StackEdge), sz * (d * .5f - StackEdge), ground, StackSize, StackSize, h + StackRise + StackRise * R(28), spec.Stack, false, 8, StackTaper);
            }
        }

        void Tank(float x, float z, float y)
        {
            if (!full) return;
            Add("stand", x, z, y, StandSize, StandSize, StandHeight, spec.Metal, true);
            Add("tank", x, z, y + StandHeight, TankSize, TankSize, TankHeight, spec.Tank, false, 8);
            Add("tank", x, z, y + StandHeight + TankHeight, TankSize, TankSize, TankCap, lot.Roof, false, 8, 0);
        }

        void Loft()
        {
            float w = (float)lot.Width, d = (float)lot.Depth;
            Plaza();
            GroundFloor(0, 0, w, d);
            float rest = H - groundFloor - RoofSlab, y = ground + groundFloor;
            Segment(0, 0, y, w, d, rest);
            Roof(0, 0, y + rest, w, d);
            var quads = Quadrants(0, 0, w, d);
            float roof = y + rest + RoofSlab;
            Tank(quads[0].x, quads[0].z, roof);
            if (R(22) < .5f) Tank(quads[3].x, quads[3].z, roof);
            if (rich && R(23) < .6f) Add("chimney", quads[1].x, quads[1].z, roof, ChimneySize, ChimneySize, 5 + 4 * R(24), plinth, true);
            if (rich) Add("hut", quads[2].x, quads[2].z, roof, HutWidth, HutDepth, HutHeight, CourseSkyline.Shade(wall, ParapetShade), true);
        }

        void Wings(bool u)
        {
            float w = (float)lot.Width, d = (float)lot.Depth;
            float back = d * (.38f + .14f * R(25)), side = w * (.32f + .14f * R(26)), flip = R(27) < .5f ? -1 : 1;
            var wings = new List<(float x, float z, float w, float d)> { (0, (d - back) * .5f, w, back) };
            float sideX = (w - side) * .5f, sideZ = -back * .5f, sideD = d - back;
            if (u) { wings.Add((-sideX, sideZ, side, sideD)); wings.Add((sideX, sideZ, side, sideD)); }
            else wings.Add((flip * sideX, sideZ, side, sideD));
            Plaza();
            float rest = H - groundFloor - RoofSlab, y = ground + groundFloor;
            foreach (var wing in wings)
            {
                GroundFloor(wing.x, wing.z, wing.w, wing.d, false);
                Segment(wing.x, wing.z, y, wing.w, wing.d, rest);
                Roof(wing.x, wing.z, y + rest, wing.w, wing.d);
            }
            Details(wings[0].x, wings[0].z, y + rest + RoofSlab, wings[0].w, wings[0].d);
            if (lot.Facade.Kind == CourseSkylineKinds.Brick) Tank(wings[1].x, wings[1].z, y + rest + RoofSlab);
        }

        void Slab()
        {
            float w = (float)lot.Width, d = (float)lot.Depth;
            Plaza();
            GroundFloor(0, 0, w, d);
            float rest = H - groundFloor;
            int count = H > 70 && R(46) < .5f ? 2 : 1;
            bool balconies = rich && Masonry && H <= 75 && R(43) < .65f;
            var top = Stack(w, d, ground + groundFloor, rest, count, .7f, false, 4, (ox, oz, y, sw, sd, h) =>
            {
                Segment(ox, oz, y, sw, sd, h);
                if (balconies) Balconies(ox, oz, y, sw, sd, h);
            });
            if (lot.Crane && rich) { Construction(top.y, top.ox, top.oz, top.w, top.d); return; }
            Add("penthouse", top.ox, top.oz, top.y, top.w * .5f, top.d * .6f, 4f, CourseSkyline.Shade(wall, ParapetShade), true);
            Antenna(top.ox, top.oz, top.y + 4f);
        }

        void Balconies(float ox, float oz, float y, float w, float d, float height)
        {
            int columns = body.Columns(w), rows = Math.Min(MostBalconyRows, body.Rows(height));
            if (columns < 2 || rows < 2) return;
            float span = columns * body.Spacing, width = body.Spacing * BalconyShare, room = Math.Min(BalconyDepth, Math.Min(lotDepth * .5f, d * .5f + spec.Plaza) - .02f - d * .5f);
            if (room <= .2f) return;
            uint tint = CourseSkyline.Shade(wall, BalconyShade);
            for (int row = 1; row < rows; row++)
                for (int c = row % 2; c < columns; c += 2)
                    foreach (int side in new[] { -1, 1 })
                        Add("balcony", ox - span * .5f + (c + .5f) * body.Spacing, oz + side * (d * .5f + room * .5f), y + body.Bottom + row * body.Storey - .1f, width, room, BalconyHeight, tint, true);
        }

        void Deco(float height, bool landmark)
        {
            float w = Math.Min((float)lot.Width, (float)lot.Depth);
            float spire = landmark ? 30 + 12 * R(36) : 12 + 16 * R(36), crown = 11f;
            float rest = (landmark ? height - spire - crown : height) - groundFloor;
            Plaza();
            GroundFloor(0, 0, w, w);
            int count = Math.Max(1, Math.Min(landmark ? 5 : 3 + (int)(R(8) * 3), (int)((rest - RoofSlab) / (3 * storey + RoofSlab))));
            var top = Stack(w, w, ground + groundFloor, rest, count, .74f, true, 4, (ox, oz, y, sw, sd, h) =>
            {
                Segment(ox, oz, y, sw, sd, h);
                DecoPiers(ox, oz, y, sw, sd, h);
            });
            if (lot.Crane && rich && !landmark) { Construction(top.y, top.ox, top.oz, top.w, top.d); return; }
            Steps(top.y, top.w, spire);
        }

        void DecoPiers(float ox, float oz, float y, float w, float d, float height)
        {
            if (!rich) return;
            foreach (bool onZ in new[] { true, false })
            {
                float face = onZ ? w : d, other = onZ ? d : w;
                int columns = body.Columns(face);
                if (columns < 3) continue;
                float span = columns * body.Spacing;
                for (int k = 2; k < columns; k += 2)
                    foreach (int side in new[] { -1, 1 })
                    {
                        float t = -span * .5f + k * body.Spacing, offset = side * (other * .5f + DecoPierOut * .5f);
                        if (onZ) Add("pier", ox + t, oz + offset, y, DecoPier, DecoPierOut, height - CorniceHeight, ledge, false);
                        else Add("pier", ox + offset, oz + t, y, DecoPierOut, DecoPier, height - CorniceHeight, ledge, false);
                    }
            }
        }

        void Steps(float y, float w, float spire)
        {
            Add("crown", 0, 0, y, w * .7f, w * .7f, 4f, trim, true);
            Add("crown", 0, 0, y + 4f - CrownLine, w * .7f + .1f, w * .7f + .1f, CrownLine, lot.Accent, false);
            Add("crown", 0, 0, y + 4f, w * .48f, w * .48f, 4f, wall, true);
            Add("crown", 0, 0, y + 8f, w * .3f, w * .3f, 3f, trim, true);
            Add("spire", 0, 0, y + 11f, SpireBase, SpireBase, spire, cap, false, 4, 0);
        }

        void Round()
        {
            int sides = R(28) < .5f ? 8 : 12;
            float s = Math.Min((float)lot.Width, (float)lot.Depth);
            var windows = Narrow(body, .35f);
            Plaza();
            GroundFloor(0, 0, s, s);
            var top = Stack(s, s, ground + groundFloor, H - groundFloor, SegmentCount(H - groundFloor, 3), .8f, true, sides, (ox, oz, y, sw, sd, h) => Segment(ox, oz, y, sw, sd, h, sides, windows));
            if (R(29) < .5f)
            {
                Add("crown", 0, 0, top.y, top.w, top.w, 6f, trim, true, sides, .55f);
                Antenna(0, 0, top.y + 6f);
                return;
            }
            Add("dome", 0, 0, top.y, top.w * .9f, top.w * .9f, 5f, cap, false, sides, .6f);
            Add("dome", 0, 0, top.y + 5f, top.w * .54f, top.w * .54f, 4f, cap, false, sides, 0);
        }

        static SkylineWindows Narrow(SkylineWindows windows, float margin) => new SkylineWindows
        {
            Spacing = windows.Spacing, Storey = windows.Storey, Width = windows.Width, Height = windows.Height, Margin = Math.Min(windows.Margin, margin),
            Bottom = windows.Bottom, Top = windows.Top, Glass = windows.Glass, Lit = windows.Lit, Seed = windows.Seed,
        };

        void Curtain()
        {
            float w = (float)lot.Width, d = (float)lot.Depth;
            Plaza();
            GroundFloor(0, 0, w, d);
            var top = Stack(w, d, ground + groundFloor, H - groundFloor, Math.Min(2, SegmentCount(H - groundFloor, 2)), .74f, false, 4);
            if (lot.Crane && rich) { Construction(top.y, top.ox, top.oz, top.w, top.d); return; }
            if (R(30) < .5f)
            {
                bool point = R(31) < .5f;
                Add("pyramid", top.ox, top.oz, top.y, top.w, top.d, Math.Min(top.w, top.d) * (point ? .7f : .4f), CourseSkyline.Shade(lot.Pane, .9f), !point, 4, point ? 0 : .3f);
                return;
            }
            Add("crown", top.ox, top.oz, top.y, top.w * .8f, top.d * .8f, 5f, trim, true, 4, .7f);
            Details(top.ox, top.oz, top.y, top.w * .8f, top.d * .8f);
            Billboard(top.ox, top.oz, top.y, top.w, top.d);
            Antenna(top.ox, top.oz, top.y + 5f);
        }

        void Twin()
        {
            float w = (float)lot.Width, d = (float)lot.Depth, t = Math.Min(w * TwinShare, d);
            float height = Math.Min(H, TwinCap), rest = height - groundFloor - RoofSlab, shift = (w - t) * .5f, y = ground + groundFloor;
            Plaza();
            GroundFloor(0, 0, w, t);
            float shortfall = H - height + 1f;
            bool pointed = R(32) < .5f && t * 1.2f >= shortfall;
            float spire = Math.Max(10 + 8 * R(33), shortfall - 4f);
            foreach (float x in new[] { -shift, shift })
            {
                Segment(x, 0, y, t, t, rest);
                Roof(x, 0, y + rest, t, t);
                if (pointed) Add("pyramid", x, 0, y + rest + RoofSlab, t, t, t * 1.2f, cap, false, 4, 0);
                else
                {
                    Add("crown", x, 0, y + rest + RoofSlab, t * .6f, t * .6f, 4f, trim, true);
                    Add("spire", x, 0, y + rest + RoofSlab + 4f, SpireBase * .8f, SpireBase * .8f, spire, cap, false, 4, 0);
                }
            }
            float span = w - 2 * t;
            if (span <= 1) return;
            float lift = ground + height * BridgeLift;
            Add("bridge", 0, 0, lift - RoofSlab, span + .4f, t * .5f, RoofSlab, trim, true);
            Add("bridge", 0, 0, lift, span + .4f, t * .45f, storey * 2, wall, false, 4, 1, Narrow(body, .2f));
            Add("bridge", 0, 0, lift + storey * 2, span + .4f, t * .5f, RoofSlab, trim, true);
        }

        void Supertall()
        {
            int design = Math.Min(2, (int)(R(34) * 3));
            if (design == 2) { Deco(H, true); return; }
            float s = Math.Min((float)lot.Width, (float)lot.Depth);
            float spire = 30 + 15 * R(35), crown = 8f, rest = H - groundFloor - spire - crown;
            Plaza();
            GroundFloor(0, 0, s, s);
            int sides = design == 1 ? 8 : 4;
            var windows = design == 1 ? Narrow(body, .35f) : body;
            var top = design == 1
                ? Stack(s, s, ground + groundFloor, rest, 3, .8f, true, 8, (ox, oz, y, sw, sd, h) => Segment(ox, oz, y, sw, sd, h, 8, windows))
                : Stack(s, s, ground + groundFloor, rest, 5, .82f, true, 4);
            Add("crown", 0, 0, top.y, top.w, top.w, crown, trim, true, sides, .55f);
            Add("crown", 0, 0, top.y, top.w + .1f, top.w + .1f, CrownLine, lot.Accent, false, sides);
            Add("spire", 0, 0, top.y + crown, SpireBase * 1.4f, SpireBase * 1.4f, spire, cap, false, sides, 0);
        }

        void Clock()
        {
            float s = Math.Max(8f, Math.Min(11f, Math.Min((float)lot.Width, (float)lot.Depth) * .55f)), stage = s + 1.2f;
            float shaft = H - groundFloor - ClockStage - ClockRoof - ClockSpire, y = ground + groundFloor;
            Plaza();
            GroundFloor(0, 0, s + 3f, s + 3f);
            Segment(0, 0, y, s, s, shaft, 4, Narrow(body, .9f));
            float ys = y + shaft;
            Add("stage", 0, 0, ys, stage, stage, ClockStage, trim, true);
            float yc = ys + (ClockStage - ClockFace) * .5f, mid = yc + ClockFace * .5f, face = stage * .5f + FaceProud - FaceDepth * .5f, hand = face + FaceDepth * .5f + HandDepth * .5f;
            foreach (int side in new[] { -1, 1 })
            {
                Add("face", 0, side * face, yc, ClockFace, FaceDepth, ClockFace, spec.Face, false);
                Add("face", side * face, 0, yc, FaceDepth, ClockFace, ClockFace, spec.Face, false);
                if (!rich) continue;
                Add("hand", 0, side * hand, mid, .35f, HandDepth, 2.2f, spec.Hands, false);
                Add("hand", .8f, side * hand, mid - .15f, 1.6f, HandDepth, .3f, spec.Hands, false);
                Add("hand", side * hand, 0, mid, HandDepth, .35f, 2.2f, spec.Hands, false);
                Add("hand", side * hand, .8f, mid - .15f, HandDepth, 1.6f, .3f, spec.Hands, false);
            }
            Add("pyramid", 0, 0, ys + ClockStage, stage + .4f, stage + .4f, ClockRoof, cap, false, 4, 0);
            Add("spire", 0, 0, ys + ClockStage + ClockRoof - 1f, .8f, .8f, ClockSpire + 1f, spec.Metal, false, 4, 0);
        }

        void Needle()
        {
            float s = Math.Min((float)lot.Width, (float)lot.Depth), pod = Math.Min(PodLimit, Math.Min(lotWidth, lotDepth) - 2 * spec.Plaza - .4f);
            float shaftTop = ground + H * .72f, upper = H * .1f;
            Plaza();
            GroundFloor(0, 0, s, s);
            Add("shaft", 0, 0, ground + groundFloor, NeedleShaft, NeedleShaft, shaftTop - ground - groundFloor, wall, false, 8, .6f);
            Add("pod", 0, 0, shaftTop, pod / PodFlare, pod / PodFlare, PodSkirt, wall, false, 12, PodFlare);
            Add("pod", 0, 0, shaftTop + PodSkirt, pod, pod, PodGlass, trim, false, 12, 1, new SkylineWindows
            {
                Spacing = 2.2f, Storey = PodGlass - .6f, Width = .82f, Height = .9f, Margin = .1f, Bottom = .3f, Top = .3f, Glass = lot.Pane, Lit = .08f, Seed = Q(R(6)),
            });
            Add("pod", 0, 0, shaftTop + PodSkirt + PodGlass, pod, pod, PodBand, trim, false, 12);
            float roof = shaftTop + PodSkirt + PodGlass + PodBand;
            Add("pod", 0, 0, roof, pod, pod, PodRoof, wall, true, 12, .45f);
            Add("shaft", 0, 0, roof + PodRoof, NeedleUpper, NeedleUpper, upper, wall, false, 8, .7f);
            float mast = roof + PodRoof + upper, length = ground + H - mast, band = length * .35f / MastBands;
            Add("mast", 0, 0, mast, 1.2f, 1.2f, length * .65f, spec.Metal, false, 4, 1);
            for (int k = 0; k < MastBands; k++)
                Add("mast", 0, 0, mast + length * .65f + k * band, 1.2f * (1 - .2f * k), 1.2f * (1 - .2f * k), band, k % 2 == 0 ? spec.Mast : spec.Face, k == MastBands - 1 ? false : true, 4, k == MastBands - 1 ? 0 : 1);
        }

        void Construction(float y, float ox, float oz, float w, float d)
        {
            float frame = SkeletonStoreys * storey;
            uint concrete = CourseSkyline.Shade(spec.Pavement, .85f);
            foreach (int sx in new[] { -1, 1 })
                foreach (int sz in new[] { -1, 1 })
                    Add("skeleton", ox + sx * (w * .5f - SkeletonColumn * .5f), oz + sz * (d * .5f - SkeletonColumn * .5f), y, SkeletonColumn, SkeletonColumn, frame, concrete, false);
            for (int k = 1; k <= SkeletonStoreys; k++) Add("skeleton", ox, oz, y + k * storey - SkeletonSlab, w, d, SkeletonSlab, concrete, true);
            float span = lotWidth - 1.2f, mastX = -span * .5f + span * .3f, jib = y + frame + CraneRise;
            Add("crane", mastX, oz, y, CraneMast, CraneMast, frame + CraneRise, spec.Crane, true);
            Add("crane", mastX, oz + CraneMast, jib - 3.5f, 2.2f, 2f, 2.2f, spec.Crane, true);
            Add("crane", 0, oz, jib, span, CraneBeam, CraneBeam, spec.Crane, true);
            Add("crane", -span * .5f + 1.4f, oz, jib - 2f, 2.4f, 1.8f, 2f, concrete, true);
            Add("crane", span * .5f - 1.5f, oz, jib - CraneLine, .15f, .15f, CraneLine, spec.Metal, false);
        }
    }
}
