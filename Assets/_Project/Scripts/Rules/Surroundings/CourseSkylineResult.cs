using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Surroundings
{
    public sealed class SkylineWindows
    {
        public float Spacing { get; internal set; }
        public float Storey { get; internal set; }
        public float Width { get; internal set; }
        public float Height { get; internal set; }
        public float Margin { get; internal set; }
        public float Bottom { get; internal set; }
        public float Top { get; internal set; }
        public uint Glass { get; internal set; } = SurroundingsSlots.NoTint;
        public float Lit { get; internal set; }
        public float Seed { get; internal set; }

        public int Columns(double face) => Spacing <= 0 ? 0 : Math.Max(0, (int)Math.Floor((face - 2 * Margin) / Spacing + 1e-6));

        public int Rows(double height) => Storey <= 0 ? 0 : Math.Max(0, (int)Math.Floor((height - Bottom - Top) / Storey + 1e-6));
    }

    public sealed class SkylineBox
    {
        public string Slot { get; internal set; }
        public string Part { get; internal set; }
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Z { get; internal set; }
        public float Width { get; internal set; }
        public float Depth { get; internal set; }
        public float Height { get; internal set; }
        public float Yaw { get; internal set; }
        public uint Tint { get; internal set; } = SurroundingsSlots.NoTint;
        public bool Top { get; internal set; }
        public int Sides { get; internal set; } = 4;
        public float Taper { get; internal set; } = 1f;
        public SkylineWindows Windows { get; internal set; }
        public float Floor { get; internal set; }
        public float Rise { get; internal set; } = 1f;
        public float Haze { get; internal set; }
        public bool Decal { get; internal set; }

        public float TopY => Y + Height;
        public bool Apex => !Decal && Taper <= 0f;
        public bool Capped => Decal || (Top && !Apex);
        public bool Straight => !Decal && !Apex && Math.Abs(Taper - 1f) < 1e-4f;
        public int SideVertices => Apex ? 3 : 4;
        public int Triangles => (Decal ? 0 : Sides * (Apex ? 1 : 2)) + (Capped ? Sides - 2 : 0);
        public int Vertices => (Decal ? 0 : Sides * SideVertices) + (Capped ? Sides : 0);
        public float Reach => Math.Max(1f, Taper);

        public (double x, double z)[] Corners()
        {
            double yaw = Yaw * Math.PI / 180, hw = Width * .5 * Reach, hd = Depth * .5 * Reach;
            double ax = Math.Sin(yaw), az = Math.Cos(yaw), rx = az, rz = -ax;
            return new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }
                .Select(c => (X + rx * hw * c.Item1 + ax * hd * c.Item2, Z + rz * hw * c.Item1 + az * hd * c.Item2)).ToArray();
        }

        public double FaceWidth()
        {
            if (Sides == 4) return Width;
            double reach = 1 / Math.Cos(Math.PI / Sides), a0 = Math.PI / Sides, a1 = 3 * Math.PI / Sides;
            double dx = (Math.Cos(a1) - Math.Cos(a0)) * Width * .5 * reach, dz = (Math.Sin(a1) - Math.Sin(a0)) * Depth * .5 * reach;
            return Math.Sqrt(dx * dx + dz * dz);
        }
    }

    public sealed class CourseTower
    {
        public int Ordinal { get; internal set; }
        public float X { get; internal set; }
        public float Z { get; internal set; }
        public float Yaw { get; internal set; }
        public float Width { get; internal set; }
        public float Depth { get; internal set; }
        public float LotWidth { get; internal set; }
        public float LotDepth { get; internal set; }
        public float Ground { get; internal set; }
        public float Height { get; internal set; }
        public float Distance { get; internal set; }
        public float Heading { get; internal set; }
        public float Weight { get; internal set; }
        public bool Downtown { get; internal set; }
        public bool Detailed { get; internal set; }
        public bool Crane { get; internal set; }
        public string Type { get; internal set; }
        public string Landmark { get; internal set; }
        public string Facade { get; internal set; }
        public string Kind { get; internal set; }
        public bool Merged { get; internal set; }
        public uint Wall { get; internal set; }
        public uint Pane { get; internal set; }
        public uint Accent { get; internal set; }
        public uint Base { get; internal set; }
        public float Haze { get; internal set; }
        public string Source { get; internal set; }
        public IReadOnlyList<SkylineBox> Boxes { get; internal set; } = Array.Empty<SkylineBox>();

        public float Top => Boxes.Count == 0 ? Ground + Height : Boxes.Max(b => b.TopY);
        public int Triangles => Boxes.Sum(b => b.Triangles);
    }

    public sealed class SkylineGroup
    {
        public string Slot { get; internal set; }
        public int Boxes { get; internal set; }
        public int Triangles { get; internal set; }
        public int Vertices { get; internal set; }
        public int Renderers { get; internal set; }
    }

    public sealed class SkylineMeshPart
    {
        public string Slot { get; internal set; }
        public int Index { get; internal set; }
        public bool Street { get; internal set; }
        public float[] Positions { get; internal set; }
        public float[] Normals { get; internal set; }
        public uint[] Colours { get; internal set; }
        public float[] Facade { get; internal set; }
        public float[] Grid { get; internal set; }
        public float[] Glass { get; internal set; }
        public float[] Extra { get; internal set; }
        public int[] Indices { get; internal set; }

        public int Vertices => Positions.Length / 3;
        public int Triangles => Indices.Length / 3;
        public string Label => (Street ? CourseSkyline.StreetPrefix : "") + Slot + (Index == 0 ? "" : "_" + (Index + 1));
    }

    public sealed class CourseSkylineResult
    {
        public CourseSurroundingsTier Tier { get; internal set; }
        public IReadOnlyList<float> Headings { get; internal set; } = Array.Empty<float>();
        public IReadOnlyList<CourseTower> Towers { get; internal set; } = Array.Empty<CourseTower>();
        public IReadOnlyList<SkylineGroup> Groups { get; internal set; } = Array.Empty<SkylineGroup>();
        public IReadOnlyList<string> ThinnedCards { get; internal set; } = Array.Empty<string>();
        public int Trimmed { get; internal set; }
        public float DetailReach { get; internal set; }
        public uint HazeTint { get; internal set; } = SurroundingsSlots.NoTint;
        public string District { get; internal set; } = "";
        public IReadOnlyList<SkylineBox> Streets { get; internal set; } = Array.Empty<SkylineBox>();
        public IReadOnlyList<SkylineGroup> StreetGroups { get; internal set; } = Array.Empty<SkylineGroup>();
        public float StreetReach { get; internal set; }
        public int RendererLimit { get; internal set; }
        public int TriangleLimit { get; internal set; }
        public int StreetRendererLimit { get; internal set; }
        public int StreetTriangleLimit { get; internal set; }
        public string Canonical { get; internal set; } = "";
        public string Hash { get; internal set; } = "";

        public IEnumerable<SkylineBox> Boxes => Towers.SelectMany(t => t.Boxes);
        public int Renderers => Groups.Sum(g => g.Renderers);
        public int Triangles => Groups.Sum(g => g.Triangles);
        public int Vertices => Groups.Sum(g => g.Vertices);
        public int StreetRenderers => StreetGroups.Sum(g => g.Renderers);
        public int StreetTriangles => StreetGroups.Sum(g => g.Triangles);
        public int StreetVertices => StreetGroups.Sum(g => g.Vertices);
        public bool Empty => Towers.Count == 0;

        public int CountStreet(string part) => Streets.Count(b => b.Part == part);

        public int Count(string type) => Towers.Count(t => t.Type == type);

        public static CourseSkylineResult None(CourseSurroundingsTier tier) => new CourseSkylineResult
        {
            Tier = tier,
            RendererLimit = tier == CourseSurroundingsTier.High ? CourseSkyline.HighRenderers : CourseSkyline.LowRenderers,
            TriangleLimit = tier == CourseSurroundingsTier.High ? CourseSkyline.HighTriangles : CourseSkyline.LowTriangles,
            StreetRendererLimit = tier == CourseSurroundingsTier.High ? CourseSkyline.HighStreetRenderers : CourseSkyline.LowStreetRenderers,
            StreetTriangleLimit = tier == CourseSurroundingsTier.High ? CourseSkyline.HighStreetTriangles : CourseSkyline.LowStreetTriangles,
        };
    }
}
