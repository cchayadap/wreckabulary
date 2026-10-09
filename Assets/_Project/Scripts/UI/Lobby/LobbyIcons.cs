using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    public static class LobbyIcons
    {
        public const string Home = "home", Settings = "settings", Power = "power", Trophy = "trophy",
            Cart = "cart", Coin = "coin", Close = "close", Turn = "turn", Play = "play", Locker = "locker",
            Badge = "badge", Lock = "lock", Check = "check", Arrow = "arrow", CoinW = "coin-w", Chevron = "chevron",
            Party = "party", Plus = "plus", Glasses = "glasses", Satchel = "satchel";
        public static readonly string[] All =
            { Home, Settings, Power, Trophy, Cart, Coin, Close, Turn, Play, Locker, Badge, Lock, Check, Arrow, CoinW, Chevron, Party, Plus,
              Glasses, Satchel };
        const int Size = 128;
        const float Half = 2.3f / 24f;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static readonly Dictionary<(int, int), Sprite> slices = new Dictionary<(int, int), Sprite>();
        static readonly Dictionary<(int, int, int), Texture2D> bursts = new Dictionary<(int, int, int), Texture2D>();
        static readonly Dictionary<(int, int, int), Texture2D> rays = new Dictionary<(int, int, int), Texture2D>();

        public static Sprite Get(string name)
        {
            if (cache.TryGetValue(name, out var sprite) && sprite) return sprite;
            Func<Vector2, float> shape = Shape(name);
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            { name = "Lobby icon " + name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[Size * Size];
            float pixel = 2f / Size;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2((x + .5f) * pixel - 1f, (y + .5f) * pixel - 1f);
                    float cover = Mathf.Clamp01(.5f - shape(p) / pixel);
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(cover * 255));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), Size);
            sprite.name = texture.name;
            cache[name] = sprite;
            return sprite;
        }

        public static Sprite Rounded => RoundedSprite(10);

        public static Sprite RoundedSprite(int radius) => Slice(radius, 0);

        public static Sprite FrameSprite(int radius, int width) => Slice(radius, width);

        public static Texture2D Sunburst(int width, int height, int radius)
        {
            var key = (width, height, radius);
            if (bursts.TryGetValue(key, out var cached) && cached) return cached;
            const int Scale = 2;
            const float Ray = 9f * Mathf.Deg2Rad;
            int w = width * Scale, h = height * Scale;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, true)
            { name = $"Lobby sunburst {width}x{height}", filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[w * h];
            var middle = new Vector2(w / 2f, h / 2f);
            float far = middle.magnitude;
            Color inner = LobbyKit.Hex(0x6ee9ff), outer = LobbyKit.Hex(0x2a5cff);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var at = new Vector2(x + .5f, y + .5f);
                    var p = at - middle;
                    float r = p.magnitude;
                    var colour = Color.Lerp(inner, outer, r / far);
                    float a = Mathf.Repeat(Mathf.Atan2(p.y, p.x), 2f * Ray);
                    float inside = a < Ray ? Mathf.Min(a, Ray - a) : -Mathf.Min(a - Ray, 2f * Ray - a);
                    colour = Color.Lerp(colour, Color.white, .267f * Mathf.Clamp01(.5f + inside * r));
                    colour.a = Mathf.Clamp01(.5f - Box(at, middle, middle, radius * Scale));
                    pixels[y * w + x] = colour;
                }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            bursts[key] = texture;
            return texture;
        }

        public static Texture2D Rays(int width, int height, int radius)
        {
            var key = (width, height, radius);
            if (rays.TryGetValue(key, out var cached) && cached) return cached;
            const int Scale = 2;
            const float Ray = 10f * Mathf.Deg2Rad;
            int w = width * Scale, h = height * Scale;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, true)
            { name = $"Lobby rays {width}x{height}", filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[w * h];
            var middle = new Vector2(w / 2f, h / 2f);
            var origin = new Vector2(w / 2f, h * .74f);
            const float strength = 0x22 / 255f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var at = new Vector2(x + .5f, y + .5f);
                    var p = at - origin;
                    float r = p.magnitude;
                    float a = Mathf.Repeat(Mathf.Atan2(p.x, p.y), 2f * Ray);
                    float inside = a < Ray ? Mathf.Min(a, Ray - a) : -Mathf.Min(a - Ray, 2f * Ray - a);
                    float ray = Mathf.Clamp01(.5f + inside * r);
                    float shape = Mathf.Clamp01(.5f - Box(at, middle, middle, radius * Scale));
                    pixels[y * w + x] = new Color(1f, 1f, 1f, strength * ray * shape);
                }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            rays[key] = texture;
            return texture;
        }

        static Sprite Slice(int radius, int width)
        {
            if (slices.TryGetValue((radius, width), out var sprite) && sprite) return sprite;
            int size = 2 * (radius + 2) + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = width > 0 ? $"Lobby frame {radius}/{width}" : $"Lobby rounded {radius}",
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[size * size];
            var middle = new Vector2(size / 2f, size / 2f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Box(new Vector2(x + .5f, y + .5f), middle, middle, radius);
                    float cover = Mathf.Clamp01(.5f - d);
                    if (width > 0) cover *= Mathf.Clamp01(.5f + d + width);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(cover * 255));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            float border = radius + 2;
            sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.name = texture.name;
            slices[(radius, width)] = sprite;
            return sprite;
        }

        static Func<Vector2, float> Shape(string name)
        {
            switch (name)
            {
                case Home:
                    return p => Lines(p, G(3.5f, 11.5f), G(12, 4), G(20.5f, 11.5f))
                        .Min(Lines(p, G(5.5f, 10), G(5.5f, 20), G(18.5f, 20), G(18.5f, 10)))
                        .Min(Lines(p, G(10, 20), G(10, 14.5f), G(14, 14.5f), G(14, 20))) - Half;
                case Settings:
                    return p =>
                    {
                        float gear = Circle(p, Vector2.zero, .5f);
                        for (int i = 0; i < 8; i++)
                            gear = Union(gear, Box(Rotate(p, -i * Mathf.PI / 4f), new Vector2(0, .64f), new Vector2(.13f, .16f), .05f));
                        return Union(Mathf.Abs(gear), Mathf.Abs(Circle(p, Vector2.zero, .19f))) - Half * .85f;
                    };
                case Power:
                    return p => Arc(p, new Vector2(0, -.06f), .6f, 125f, 55f).Min(Segment(p, new Vector2(0, .1f), new Vector2(0, .8f))) - Half;
                case Trophy:
                    return p => Lines(p, G(7.5f, 8.5f), G(7.5f, 4), G(16.5f, 4), G(16.5f, 8.5f))
                        .Min(Arc(p, G(12, 8.5f), 4.5f / 12f, 180f, 360f))
                        .Min(Lines(p, G(7.5f, 6), G(4.5f, 6))).Min(Arc(p, G(7.383f, 6.829f), .25f, 164f, 278f))
                        .Min(Lines(p, G(16.5f, 6), G(19.5f, 6))).Min(Arc(p, G(16.617f, 6.829f), .25f, -98f, 16f))
                        .Min(Lines(p, G(12, 13), G(12, 16.5f)))
                        .Min(Lines(p, G(8, 20), G(16, 20), G(15, 16.5f), G(9, 16.5f), G(8, 20))) - Half;
                case Cart:
                    return p => Union(Lines(p, G(2, 3.5f), G(4.2f, 3.5f), G(6.8f, 15.5f), G(18.4f, 15.5f), G(20.4f, 7.6f), G(5.3f, 7.6f)) - Half,
                        Union(Circle(p, G(8.6f, 20), .14f), Circle(p, G(17, 20), .14f)));
                case Coin:
                    return p => Cut(Circle(p, Vector2.zero, .9f), Mathf.Abs(Circle(p, Vector2.zero, .64f)) - .07f);
                case Close:
                    return p => Lines(p, G(6.5f, 6.5f), G(17.5f, 17.5f)).Min(Lines(p, G(17.5f, 6.5f), G(6.5f, 17.5f))) - Half;
                case Turn:
                    return p =>
                    {
                        var squashed = new Vector2(p.x, p.y * 2.6f);
                        float arc = (Mathf.Abs(Circle(squashed, Vector2.zero, .8f)) - .16f) / 2.6f;
                        arc = Cut(arc, Box(p, new Vector2(0, .3f), new Vector2(.42f, .3f), 0));
                        float left = Polygon(p, new Vector2(-.62f, .02f), new Vector2(-.22f, .2f), new Vector2(-.28f, -.2f));
                        float right = Polygon(p, new Vector2(.62f, .02f), new Vector2(.22f, .2f), new Vector2(.28f, -.2f));
                        return Union(arc, Union(left, right));
                    };
                case Play:
                    return p => Polygon(p, G(8, 5.5f), G(19, 12), G(8, 18.5f)) - .04f;
                case Locker:
                    return p => Arc(p, G(12, 6.5f), 2f / 12f, -72.4f, 180f)
                        .Min(Lines(p, G(12.6f, 8.4f), G(12, 9.3f), G(12, 10)))
                        .Min(Lines(p, G(12, 10), G(3.8f, 16.6f), G(4.3f, 18), G(19.7f, 18), G(20.2f, 16.6f), G(12, 10))) - Half;
                case Badge:
                    return p => Mathf.Abs(Box(p, Vector2.zero, new Vector2(.625f, .625f), 3.5f / 12f))
                        .Min(Lines(p, G(8.5f, 9), G(15.5f, 9))).Min(Lines(p, G(12, 9), G(12, 16))) - Half;
                case Lock:
                    return p => Union(Lines(p, G(7.5f, 11), G(7.5f, 8.5f)).Min(Arc(p, G(12, 8.5f), 4.5f / 12f, 0f, 180f))
                            .Min(Lines(p, G(16.5f, 8.5f), G(16.5f, 11))) - Half,
                        Box(p, G(12, 15.75f), new Vector2(7f / 12f, 4.75f / 12f), 2.5f / 12f) - Half);
                case Check:
                    return p => Lines(p, G(5, 12.5f), G(9.5f, 17), G(19, 7.5f)) - Half;
                case Arrow:
                    return p => Lines(p, G(5, 12), G(18, 12)).Min(Lines(p, G(13, 6.5f), G(18.5f, 12), G(13, 17.5f))) - Half;
                case CoinW:
                    return p => Lines(p, G(7.6f, 9.2f), G(9.2f, 15), G(10.8f, 10.9f), G(12, 10.9f), G(13.6f, 15), G(15.2f, 9.2f)) - 1.8f / 24f;
                case Chevron:
                    return p => Lines(p, G(9, 5.5f), G(15.5f, 12), G(9, 18.5f)) - Half;
                case Party:
                    return p => Union(Union(Circle(p, G(9, 8), 3.2f / 12f), Mathf.Max(Circle(p, G(9, 19.5f), 6.2f / 12f), G(9, 19.5f).y - p.y)),
                        Mathf.Min(Mathf.Abs(Circle(p, G(17, 9), 2.5f / 12f)), Arc(p, G(16.5f, 19f), 5f / 12f, -6f, 90f)) - Half);
                case Plus:
                    return p => Lines(p, G(12, 6), G(12, 18)).Min(Lines(p, G(6, 12), G(18, 12))) - Half;
                case Glasses:
                    return p => Mathf.Abs(Circle(p, G(7, 13), 3.5f / 12f)).Min(Mathf.Abs(Circle(p, G(17, 13), 3.5f / 12f)))
                        .Min(Lines(p, G(10.5f, 13), G(13.5f, 13))).Min(Lines(p, G(3.5f, 12), G(2.5f, 9))).Min(Lines(p, G(20.5f, 12), G(21.5f, 9))) - Half;
                case Satchel:
                    return p => Union(Mathf.Abs(Box(p, G(12, 14.75f), new Vector2(7.5f / 12f, 4.75f / 12f), 0f))
                            .Min(Lines(p, G(8, 10), G(8, 8))).Min(Arc(p, G(12, 8), 4f / 12f, 0f, 180f)).Min(Lines(p, G(16, 8), G(16, 10)))
                            .Min(Lines(p, G(4.5f, 13.5f), G(19.5f, 13.5f))) - Half,
                        Box(p, G(12, 13.75f), new Vector2(1.5f / 12f, 1.25f / 12f), .6f / 12f));
                default:
                    throw new ArgumentException("No lobby icon called " + name, nameof(name));
            }
        }

        static Vector2 G(float x, float y) => new Vector2((x - 12f) / 12f, (12f - y) / 12f);

        static float Min(this float a, float b) => Mathf.Min(a, b);
        static float Union(float a, float b) => Mathf.Min(a, b);
        static float Cut(float a, float b) => Mathf.Max(a, -b);
        static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        static float Lines(Vector2 p, params Vector2[] points)
        {
            float d = float.MaxValue;
            for (int i = 0; i + 1 < points.Length; i++) d = Mathf.Min(d, Segment(p, points[i], points[i + 1]));
            return d;
        }

        static float Arc(Vector2 p, Vector2 c, float r, float from, float to)
        {
            var q = p - c;
            float span = Mathf.Repeat(to - from, 360f);
            if (span == 0f) span = 360f;
            if (Mathf.Repeat(Mathf.Atan2(q.y, q.x) * Mathf.Rad2Deg - from, 360f) <= span) return Mathf.Abs(q.magnitude - r);
            Vector2 a = c + r * new Vector2(Mathf.Cos(from * Mathf.Deg2Rad), Mathf.Sin(from * Mathf.Deg2Rad));
            Vector2 b = c + r * new Vector2(Mathf.Cos(to * Mathf.Deg2Rad), Mathf.Sin(to * Mathf.Deg2Rad));
            return Mathf.Min((p - a).magnitude, (p - b).magnitude);
        }

        static Vector2 Rotate(Vector2 p, float a)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(c * p.x - s * p.y, s * p.x + c * p.y);
        }

        static float Box(Vector2 p, Vector2 c, Vector2 half, float round)
        {
            var d = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + Vector2.one * round;
            return new Vector2(Mathf.Max(d.x, 0), Mathf.Max(d.y, 0)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0) - round;
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        static float Polygon(Vector2 p, params Vector2[] v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                var e = v[j] - v[i];
                var w = p - v[i];
                var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }
    }
}
