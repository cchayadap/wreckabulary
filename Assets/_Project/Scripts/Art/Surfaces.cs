using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Wreckabulary.Art
{
    public static class Surfaces
    {
        public enum Kind { Planks, Plaster, Wood, Lawn, Checker, Tile, Rug }

        sealed class Spec
        {
            public int Size;
            public float Smoothness, Bump;
            public Vector2 Tile;
        }

        static readonly Dictionary<Kind, Spec> specs = new()
        {
            [Kind.Planks] = new Spec { Size = 1024, Smoothness = .21f, Bump = .6f, Tile = new Vector2(4f, 1.2f) },
            [Kind.Plaster] = new Spec { Size = 512, Smoothness = .1f, Bump = .45f, Tile = new Vector2(3f, 3f) },
            [Kind.Wood] = new Spec { Size = 512, Smoothness = .24f, Bump = .5f, Tile = new Vector2(2f, 2f) },
            [Kind.Lawn] = new Spec { Size = 1024, Smoothness = .02f, Bump = .7f, Tile = new Vector2(4f, 4f) },
            [Kind.Checker] = new Spec { Size = 512, Smoothness = .69f, Bump = .35f, Tile = new Vector2(1.6f, 1.6f) },
            [Kind.Tile] = new Spec { Size = 512, Smoothness = .69f, Bump = .35f, Tile = new Vector2(1.2f, 1.2f) },
            [Kind.Rug] = new Spec { Size = 512, Smoothness = .04f, Bump = .5f, Tile = Vector2.one },
        };

        static readonly Dictionary<string, (Texture2D albedo, Texture2D normal)> textures = new();
        static readonly Dictionary<string, Material> materials = new();
        static readonly Dictionary<string, Mesh> meshes = new();

        public static Vector2 Tile(Kind kind) => specs[kind].Tile;

        public static int TextureCount => textures.Count;

        public static Color Hex(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        public static Material Get(Kind kind, Color tint, float aspect = 1f)
        {
            aspect = kind == Kind.Rug ? Mathf.Clamp(Mathf.Round(aspect * 4f) / 4f, .5f, 3f) : 1f;
            string key = $"{kind}:{aspect:0.00}:{ColorUtility.ToHtmlStringRGB(tint)}";
            if (materials.TryGetValue(key, out var cached) && cached) return cached;
            var (albedo, normal) = Textures(kind, aspect);
            var spec = specs[kind];
            var assets = GameAssets.I;
            var material = assets && assets.tintBase ? new Material(assets.tintBase) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = "Surface " + key;
            material.hideFlags = HideFlags.DontUnloadUnusedAsset;
            material.color = tint;
            material.mainTexture = albedo;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", albedo);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", spec.Smoothness);
            if (material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", spec.Bump);
                material.EnableKeyword("_NORMALMAP");
            }
            materials[key] = material;
            return material;
        }

        public static Mesh Box(Vector3 size, Vector2 tile, Vector3? at = null, bool grainAlongLength = false)
        {
            var origin = at ?? size * .5f;
            string key = $"{size.x:0.###},{size.y:0.###},{size.z:0.###}|{tile.x:0.###},{tile.y:0.###}|{(at.HasValue ? $"{origin.x:0.###},{origin.y:0.###},{origin.z:0.###}" : "-")}|{grainAlongLength}";
            if (meshes.TryGetValue(key, out var cached) && cached) return cached;
            var vertices = new List<Vector3>(24);
            var normals = new List<Vector3>(24);
            var uvs = new List<Vector2>(24);
            var triangles = new List<int>(36);
            foreach (var (normal, up) in new[] { (Vector3.right, Vector3.up), (Vector3.left, Vector3.up), (Vector3.forward, Vector3.up),
                (Vector3.back, Vector3.up), (Vector3.up, Vector3.forward), (Vector3.down, Vector3.forward) })
            {
                var right = Vector3.Cross(normal, up);
                int first = vertices.Count;
                foreach (var (r, u) in new[] { (-.5f, -.5f), (-.5f, .5f), (.5f, .5f), (.5f, -.5f) })
                {
                    var p = normal * .5f + right * r + up * u;
                    vertices.Add(p);
                    normals.Add(normal);
                    var w = origin + Vector3.Scale(p, size);
                    Vector2 uv;
                    if (normal.y != 0f) uv = grainAlongLength && size.z > size.x ? new Vector2(w.z, w.x) : new Vector2(w.x, w.z);
                    else if (normal.z != 0f) uv = new Vector2(w.x, w.y);
                    else uv = new Vector2(w.z, w.y);
                    uvs.Add(new Vector2(uv.x / tile.x, uv.y / tile.y));
                }
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
            var mesh = new Mesh { name = "Surface box " + key, hideFlags = HideFlags.DontUnloadUnusedAsset };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            meshes[key] = mesh;
            return mesh;
        }

        static (Texture2D albedo, Texture2D normal) Textures(Kind kind, float aspect)
        {
            string key = $"{kind}:{aspect:0.00}";
            if (textures.TryGetValue(key, out var pair) && pair.albedo && pair.normal) return pair;
            int size = specs[kind].Size;
            var colour = new Color32[size * size];
            var heights = new float[size * size];
            Pattern(kind, aspect, size, colour, heights);
            var wrap = kind == Kind.Rug ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            var albedo = new Texture2D(size, size, TextureFormat.RGBA32, true, false) { name = "Surface " + key, wrapMode = wrap };
            var normal = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { name = "Surface normal " + key, wrapMode = wrap };
            var bumps = new Color32[size * size];
            bool repeats = kind != Kind.Rug;
            Parallel.For(0, size, y =>
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (Height(heights, size, x - 1, y, repeats) - Height(heights, size, x + 1, y, repeats)) * 5f;
                    float dy = (Height(heights, size, x, y - 1, repeats) - Height(heights, size, x, y + 1, repeats)) * 5f;
                    float length = Mathf.Sqrt(dx * dx + dy * dy + 1f);
                    bumps[y * size + x] = new Color32((byte)Mathf.RoundToInt((dx / length + 1f) * 127.5f), (byte)Mathf.RoundToInt((dy / length + 1f) * 127.5f),
                        (byte)Mathf.RoundToInt((1f / length + 1f) * 127.5f), 255);
                }
            });
            foreach (var (texture, pixels) in new[] { (albedo, colour), (normal, bumps) })
            {
                texture.SetPixels32(pixels);
                texture.filterMode = FilterMode.Trilinear;
                texture.anisoLevel = 8;
                texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
                texture.Apply(true, true);
            }
            textures[key] = (albedo, normal);
            return (albedo, normal);
        }

        static float Height(float[] heights, int size, int x, int y, bool repeats) => repeats
            ? heights[(y + size) % size * size + (x + size) % size]
            : heights[Mathf.Clamp(y, 0, size - 1) * size + Mathf.Clamp(x, 0, size - 1)];

        static float Smooth(float a, float b, float v)
        {
            float t = Mathf.Clamp01((v - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        static uint Hash(string text)
        {
            uint h = 2166136261;
            foreach (char c in text) { h ^= c; h = unchecked(h * 16777619); }
            return h;
        }

        sealed class Random32
        {
            uint state;
            public Random32(uint seed) => state = seed;
            public float Next()
            {
                unchecked
                {
                    state += 0x6d2b79f5;
                    uint value = (state ^ (state >> 15)) * (state | 1);
                    value ^= value + (value ^ (value >> 7)) * (value | 61);
                    return (value ^ (value >> 14)) / 4294967296f;
                }
            }
        }

        sealed class Noise
        {
            readonly float[] grid;
            readonly int resolution;
            public Noise(int resolution, Random32 random)
            {
                this.resolution = resolution;
                grid = new float[resolution * resolution];
                for (int i = 0; i < grid.Length; i++) grid[i] = random.Next();
            }
            public float At(float u, float v)
            {
                float x = (u - Mathf.Floor(u)) * resolution, y = (v - Mathf.Floor(v)) * resolution;
                int ix = (int)x % resolution, iy = (int)y % resolution, jx = (ix + 1) % resolution, jy = (iy + 1) % resolution;
                float sx = x - (int)x, sy = y - (int)y;
                sx = sx * sx * (3f - 2f * sx); sy = sy * sy * (3f - 2f * sy);
                float top = grid[iy * resolution + ix] * (1f - sx) + grid[iy * resolution + jx] * sx;
                float bottom = grid[jy * resolution + ix] * (1f - sx) + grid[jy * resolution + jx] * sx;
                return top * (1f - sy) + bottom * sy;
            }
        }

        static Color32 Shade(Color32 face, float shade) => new(
            (byte)Mathf.Clamp(Mathf.RoundToInt(face.r * shade), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(face.g * shade), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(face.b * shade), 0, 255), 255);

        static void Pattern(Kind kind, float aspect, int size, Color32[] colour, float[] heights)
        {
            string web = kind switch { Kind.Checker or Kind.Tile => "ceramic", _ => kind.ToString().ToLowerInvariant() };
            var random = new Random32(Hash("wreckabulary:surface:v1:" + web));
            var broad = new Noise(8, random);
            var grain = new Noise(32, random);
            var fine = new Noise(128, random);
            var micro = new Noise(Mathf.Max(128, size / 2), random);
            var boards = new float[12];
            for (int i = 0; i < boards.Length; i++) boards[i] = (random.Next() - .5f) * .035f;
            Color32 white = new(255, 255, 255, 255);
            Color32 light = new(0xF1, 0xE7, 0xCE, 255), dark = new(0xBC, 0xC9, 0xBA, 255), kitchenGrout = new(0xC7, 0xC2, 0xB1, 255);
            Color32 bath = new(0xE9, 0xEE, 0xEA, 255), bathGrout = new(0xC3, 0xCF, 0xCF, 255);
            const float twoPi = Mathf.PI * 2f;
            Parallel.For(0, size, y =>
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    float large = broad.At(u, v) - .5f, middle = grain.At(u, v) - .5f, small = fine.At(u, v) - .5f, tiny = micro.At(u, v) - .5f;
                    float shade, height;
                    var face = white;
                    switch (kind)
                    {
                        case Kind.Wood:
                        case Kind.Planks:
                        {
                            float drift = Mathf.Sin(u * twoPi * 2f) * .13f;
                            float streak = Mathf.Sin((v * 46f + drift + (large + .5f) * .55f) * twoPi);
                            float pores = Mathf.Pow(Mathf.Max(0f, streak), 6f);
                            shade = .96f + large * .042f + streak * .014f - pores * .025f + tiny * .012f;
                            height = .5f + streak * .028f + small * .015f;
                            if (kind == Kind.Planks)
                            {
                                int row = Mathf.Min(3, Mathf.FloorToInt(v * 4f));
                                float stagger = row % 2 * .5f;
                                float across = v * 4f % 1f, along = (u * 2f + stagger) % 1f;
                                float seam = 1f - Mathf.Min(Smooth(.01f, .035f, Mathf.Min(across, 1f - across)), Smooth(.008f, .023f, Mathf.Min(along, 1f - along)));
                                shade += boards[row * 3 + Mathf.Min(2, Mathf.FloorToInt(u * 2f + stagger))] - seam * .075f;
                                height -= seam * .09f;
                            }
                            break;
                        }
                        case Kind.Rug:
                        {
                            float warp = Mathf.Cos(u * twoPi * 128f * aspect), weft = Mathf.Cos(v * twoPi * 128f), woven = warp * weft;
                            shade = .973f + woven * .012f + middle * .012f + tiny * .008f;
                            height = .5f + woven * .052f + small * .016f;
                            float edge = Mathf.Min(Mathf.Min(u * aspect, (1f - u) * aspect), Mathf.Min(v, 1f - v));
                            float band = 1f - Smooth(.039f, .058f, edge);
                            float piping = Mathf.Exp(-Mathf.Pow((edge - .055f) / .005f, 2f));
                            float phase = Mathf.Min(v, 1f - v) <= Mathf.Min(u, 1f - u) * aspect ? u * aspect : v;
                            float stitch = Mathf.Exp(-Mathf.Pow((edge - .025f) / .0035f, 2f)) * Mathf.Pow(Mathf.Max(0f, Mathf.Cos(phase * twoPi * 50f)), 3f);
                            shade += -band * .065f + stitch * .045f - piping * .024f;
                            height += piping * .05f + stitch * .035f;
                            shade -= Mathf.Pow(Mathf.Max(0f, Mathf.Cos(v * twoPi * 6f)), 12f) * .008f;
                            break;
                        }
                        case Kind.Lawn:
                        {
                            float blades = Mathf.Sin((u * 74f + v * 13f + middle * .7f) * twoPi) * Mathf.Cos(v * twoPi * 57f);
                            float tufts = Mathf.Sin((u * 151f - v * 29f + small * 1.3f) * twoPi) * Mathf.Cos(v * twoPi * 113f);
                            shade = .92f + large * .075f + middle * .035f + blades * .015f + tufts * .01f + tiny * .014f;
                            height = .5f + blades * .05f + tufts * .025f + small * .027f;
                            break;
                        }
                        case Kind.Checker:
                        case Kind.Tile:
                        {
                            int squares = kind == Kind.Checker ? 2 : 4;
                            float su = u * squares, sv = v * squares;
                            float gu = Mathf.Min(su % 1f, 1f - su % 1f), gv = Mathf.Min(sv % 1f, 1f - sv % 1f);
                            float grout = 1f - Smooth(.006f * squares, .016f * squares, Mathf.Min(gu, gv));
                            bool odd = (Mathf.FloorToInt(su) + Mathf.FloorToInt(sv)) % 2 == 1;
                            var square = kind == Kind.Checker ? odd ? dark : light : bath;
                            var line = kind == Kind.Checker ? kitchenGrout : bathGrout;
                            face = Color32.Lerp(square, line, grout);
                            shade = 1f + large * .012f + tiny * .008f;
                            height = .5f + small * .012f - grout * .08f;
                            break;
                        }
                        default:
                            shade = .984f + large * .016f + small * .006f + tiny * .005f;
                            height = .5f + large * .033f + small * .029f;
                            break;
                    }
                    colour[y * size + x] = Shade(face, shade);
                    heights[y * size + x] = height;
                }
            });
        }
    }
}
