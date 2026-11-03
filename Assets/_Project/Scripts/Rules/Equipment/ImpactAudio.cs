using System;
using System.Collections.Generic;
using System.Text;

namespace Wreckabulary.Rules
{
    public enum SurfaceMaterial { Wood, Glass, Metal, Ceramic, Stone, Soft, Body }

    public enum StrikePart { Body, Strap, Buckle }

    public enum ImpactResult { Miss, Glance, Hit, Damaged, Broken }

    public enum ImpactLayer { Swing, Contact, Damage, Break }

    public readonly struct ImpactEvent
    {
        public readonly string Id;
        public readonly string Weapon;
        public readonly StrikePart Part;
        public readonly SurfaceMaterial Surface;
        public readonly ImpactResult Result;
        public readonly float Strength;
        public readonly string Target;
        public readonly int Stage;

        public ImpactEvent(string id, string weapon, StrikePart part, SurfaceMaterial surface, ImpactResult result, float strength,
            string target = null, int stage = 0)
        {
            Id = id ?? "";
            Weapon = weapon ?? "";
            Part = part;
            Surface = surface;
            Result = result;
            Strength = ImpactCues.Clamp01(strength);
            Target = string.IsNullOrEmpty(target) ? Id : target;
            Stage = stage;
        }

        public override string ToString() => $"{Id} {Weapon}/{Part}/{Surface} {Result} {Strength:0.00}";
    }

    public readonly struct ImpactCue
    {
        public readonly ImpactLayer Layer;
        public readonly string Key;
        public readonly float Volume;
        public readonly int Priority;
        public readonly string Once;

        public ImpactCue(ImpactLayer layer, string key, float volume, string once)
        {
            Layer = layer;
            Key = key;
            Volume = volume;
            Priority = ImpactCues.PriorityOf(layer);
            Once = once;
        }

        public override string ToString() => $"{Layer} {Key} {Volume:0.00}";
    }

    public static class ImpactCues
    {
        public const string Any = "Any";
        public const string DefaultWeapon = "Default";
        public const float GlanceScale = 0.45f;

        public static string SwingKey(string weapon) => "swing/" + Weapon(weapon);
        public static string ContactKey(string weapon, StrikePart part, SurfaceMaterial surface) => $"contact/{Weapon(weapon)}/{part}/{surface}";
        public static string ContactKey(string weapon, string part, SurfaceMaterial surface) => $"contact/{Weapon(weapon)}/{part}/{surface}";
        public static string DamageKey(SurfaceMaterial surface) => "damage/" + surface;
        public static string BreakKey(SurfaceMaterial surface) => "break/" + surface;

        static string Weapon(string weapon) => string.IsNullOrEmpty(weapon) ? DefaultWeapon : weapon;

        public static int PriorityOf(ImpactLayer layer) => layer switch
        {
            ImpactLayer.Break => 3,
            ImpactLayer.Damage => 2,
            ImpactLayer.Contact => 1,
            _ => 0,
        };

        public static IEnumerable<string> ContactFallbacks(string weapon, StrikePart part, SurfaceMaterial surface)
        {
            yield return ContactKey(weapon, part, surface);
            yield return ContactKey(weapon, Any, surface);
            yield return ContactKey(DefaultWeapon, part, surface);
            yield return ContactKey(DefaultWeapon, Any, surface);
        }

        public static string Resolve(IEnumerable<string> candidates, Func<string, bool> available)
        {
            if (available == null) return null;
            foreach (var key in candidates) if (available(key)) return key;
            return null;
        }

        public static List<ImpactCue> Plan(in ImpactEvent e, Func<string, bool> available = null)
        {
            var cues = new List<ImpactCue>(3);
            if (e.Result == ImpactResult.Miss) return cues;
            float strength = e.Result == ImpactResult.Glance ? e.Strength * GlanceScale : e.Strength;
            string contact = available == null ? ContactKey(e.Weapon, e.Part, e.Surface) : Resolve(ContactFallbacks(e.Weapon, e.Part, e.Surface), available);
            if (contact != null) cues.Add(new ImpactCue(ImpactLayer.Contact, contact, ContactVolume(strength), "contact|" + e.Id));
            if (e.Result == ImpactResult.Damaged)
                Add(cues, ImpactLayer.Damage, DamageKey(e.Surface), LayerVolume(strength), $"damage|{e.Target}|{e.Stage}", available);
            if (e.Result == ImpactResult.Broken) cues.AddRange(BreakOnly(e.Target, e.Surface, strength, available));
            return cues;
        }

        public static List<ImpactCue> BreakOnly(string target, SurfaceMaterial surface, float strength, Func<string, bool> available = null)
        {
            var cues = new List<ImpactCue>(1);
            Add(cues, ImpactLayer.Break, BreakKey(surface), LayerVolume(Math.Max(Clamp01(strength), 0.6f)), "break|" + target, available);
            return cues;
        }

        static void Add(List<ImpactCue> cues, ImpactLayer layer, string key, float volume, string once, Func<string, bool> available)
        {
            if (available == null || available(key)) cues.Add(new ImpactCue(layer, key, volume, once));
        }

        public const float CrackedAt = 0.7f;
        public const float BadlyCrackedAt = 0.35f;

        public static int Stage(float remaining) => remaining <= 0f ? 3 : remaining <= BadlyCrackedAt ? 2 : remaining <= CrackedAt ? 1 : 0;

        public static ImpactResult ResultFor(float before, float after, bool broken, bool glancing = false)
        {
            if (broken) return ImpactResult.Broken;
            if (Stage(after) > Stage(before)) return ImpactResult.Damaged;
            return glancing ? ImpactResult.Glance : ImpactResult.Hit;
        }

        public static float ContactVolume(float strength) => Lerp(0.3f, 1f, Clamp01(strength));
        public static float LayerVolume(float strength) => Lerp(0.55f, 1f, Clamp01(strength));

        public static float Strength(MeleeStats stats, float dealtDamage, float referenceDamage = 28f, float referenceBreak = 4.5f)
        {
            if (stats == null) return Clamp01(dealtDamage / Math.Max(1f, referenceDamage));
            float damage = Clamp01(Math.Max(dealtDamage, stats.Damage) / Math.Max(1f, referenceDamage));
            float power = Clamp01(stats.BreakPower / Math.Max(0.1f, referenceBreak));
            return Clamp01(0.55f * damage + 0.45f * power);
        }

        public static float Audibility(float distance, float maxDistance, bool occluded, float occlusionGain)
        {
            if (!(maxDistance > 0f) || float.IsNaN(distance)) return 0f;
            float d = Clamp01(distance / maxDistance);
            float gain = 1f - d * d;
            return occluded ? gain * Clamp01(occlusionGain) : gain;
        }

        internal static float Clamp01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }

    public static class SurfaceMaterials
    {
        static readonly Dictionary<string, SurfaceMaterial> Words = Build();

        static Dictionary<string, SurfaceMaterial> Build()
        {
            var map = new Dictionary<string, SurfaceMaterial>(StringComparer.Ordinal);
            void Add(SurfaceMaterial m, params string[] words) { foreach (var w in words) map[w] = m; }
            Add(SurfaceMaterial.Wood, "TABLE", "CHAIR", "STOOL", "DESK", "SHELF", "CABINET", "CRATE", "BARREL", "CHEST", "BAT", "BROOM", "SPEAR",
                "DOOR", "DRESSER", "BOOKCASE", "WARDROBE", "BENCH", "CLOCK", "DRAWER", "PALLET", "LOG", "PLANK", "ARROW", "RACK", "SPEAKER");
            Add(SurfaceMaterial.Glass, "WINDOW", "MIRROR", "GLASS", "BOTTLE", "JAR", "TV", "SCREEN", "WATER", "AQUARIUM", "TANK", "LIGHTBULB", "BULB");
            Add(SurfaceMaterial.Metal, "FRIDGE", "STOVE", "OVEN", "PAN", "POT", "BIN", "LOCKER", "SAFE", "HAMMER", "BLADE", "SHIELD", "BOMB", "FAN",
                "SODA", "CAN", "KETTLE", "RADIATOR", "WASHER", "DRYER", "MICROWAVE", "TOASTER", "SINK", "TRASH", "BUCKET", "PIPE");
            Add(SurfaceMaterial.Ceramic, "VASE", "MUG", "CUP", "BOWL", "PLATE", "TEAPOT", "TOILET", "BATH", "BATHTUB", "TILE", "PLANT", "LAMP", "POTTERY");
            Add(SurfaceMaterial.Stone, "ROCK", "BRICK", "STATUE", "FIREPLACE", "STONE", "BOULDER", "COUNTER", "PILLAR", "GNOME", "CONCRETE");
            Add(SurfaceMaterial.Soft, "SOFA", "BED", "PILLOW", "CUSHION", "RUG", "MAT", "CURTAIN", "FOAM", "BALL", "BOX", "BOOK", "CAKE", "PIE",
                "APPLE", "SOAP", "TEDDY", "BEAR", "BEANBAG", "MATTRESS", "BLANKET", "TOWEL", "PARCEL", "CARDBOARD", "COUCH");
            return map;
        }

        public static bool TryForWord(string name, out SurfaceMaterial material)
        {
            material = SurfaceMaterial.Wood;
            if (string.IsNullOrEmpty(name)) return false;
            var token = new StringBuilder();
            for (int i = 0; i <= name.Length; i++)
            {
                char c = i < name.Length ? char.ToUpperInvariant(name[i]) : ' ';
                if (c >= 'A' && c <= 'Z') { token.Append(c); continue; }
                if (token.Length > 0 && Words.TryGetValue(token.ToString(), out material)) return true;
                token.Clear();
            }
            material = SurfaceMaterial.Wood;
            return false;
        }

        public static SurfaceMaterial ForWord(string name) => TryForWord(name, out var m) ? m : SurfaceMaterial.Wood;
    }

    public static class ImpactVariation
    {
        public static uint Hash(string text)
        {
            uint h = 2166136261u;
            if (text != null) foreach (char c in text) { h ^= c; h *= 16777619u; }
            return h;
        }

        public static int Pick(string eventId, string key, int count, int previous)
        {
            if (count <= 0) return -1;
            if (count == 1) return 0;
            int pick = (int)(Hash(eventId + "|" + key) % (uint)count);
            return pick == previous ? (pick + 1 + (int)(Hash(key) % (uint)(count - 1))) % count : pick;
        }

        public static float Pitch(string eventId, string key, float min, float max)
        {
            if (!(max > min)) return min;
            float t = (Hash(key + "|" + eventId) & 0xFFFF) / 65535f;
            return min + (max - min) * t;
        }
    }

    public sealed class ImpactEventLedger
    {
        readonly int capacity;
        readonly HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        readonly Queue<string> order = new Queue<string>();

        public ImpactEventLedger(int capacity = 512)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
        }

        public int Count => seen.Count;

        public bool Admit(string id)
        {
            if (string.IsNullOrEmpty(id) || seen.Contains(id)) return false;
            if (order.Count >= capacity) seen.Remove(order.Dequeue());
            seen.Add(id);
            order.Enqueue(id);
            return true;
        }

        public bool Contains(string id) => id != null && seen.Contains(id);

        public void Clear()
        {
            seen.Clear();
            order.Clear();
        }
    }

    public sealed class ImpactContactCooldown
    {
        readonly Dictionary<string, double> last = new Dictionary<string, double>(StringComparer.Ordinal);
        readonly double seconds;
        readonly int capacity;

        public ImpactContactCooldown(double seconds, int capacity = 256)
        {
            if (seconds < 0 || double.IsNaN(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            this.seconds = seconds;
            this.capacity = Math.Max(1, capacity);
        }

        public bool Admit(string source, string target, string key, double now)
        {
            string pair = source + ">" + target + ">" + key;
            if (last.TryGetValue(pair, out double at) && now - at < seconds) return false;
            if (last.Count >= capacity) Prune(now);
            last[pair] = now;
            return true;
        }

        void Prune(double now)
        {
            var stale = new List<string>();
            foreach (var kv in last) if (now - kv.Value >= seconds) stale.Add(kv.Key);
            foreach (var k in stale) last.Remove(k);
            if (last.Count < capacity) return;
            string oldest = null;
            double when = double.PositiveInfinity;
            foreach (var kv in last) if (kv.Value < when) { when = kv.Value; oldest = kv.Key; }
            if (oldest != null) last.Remove(oldest);
        }
    }

    public sealed class ImpactVoiceBudget
    {
        public readonly struct Voice
        {
            public readonly int Slot;
            public readonly string Key;
            public readonly int Priority;
            public readonly float Gain;
            public readonly double Ends;

            public Voice(int slot, string key, int priority, float gain, double ends)
            {
                Slot = slot;
                Key = key;
                Priority = priority;
                Gain = gain;
                Ends = ends;
            }
        }

        readonly Voice?[] slots;
        readonly int perKey;

        public ImpactVoiceBudget(int voices, int perKey)
        {
            if (voices <= 0) throw new ArgumentOutOfRangeException(nameof(voices));
            slots = new Voice?[voices];
            this.perKey = Math.Max(1, perKey);
        }

        public int Capacity => slots.Length;

        public int Active(double now)
        {
            int n = 0;
            for (int i = 0; i < slots.Length; i++) if (slots[i] is Voice v && v.Ends > now) n++;
            return n;
        }

        public int ActiveFor(string key, double now)
        {
            int n = 0;
            for (int i = 0; i < slots.Length; i++) if (slots[i] is Voice v && v.Ends > now && v.Key == key) n++;
            return n;
        }

        public int Request(string key, int priority, float gain, double now, double seconds)
        {
            int victim = -1;
            int sameKey = 0, sameKeyWeakest = -1;
            for (int i = 0; i < slots.Length; i++)
            {
                if (!(slots[i] is Voice v) || v.Ends <= now)
                {
                    if (victim < 0) victim = i;
                    continue;
                }
                if (v.Key == key)
                {
                    sameKey++;
                    if (sameKeyWeakest < 0 || Weaker(v, (Voice)slots[sameKeyWeakest])) sameKeyWeakest = i;
                }
            }
            if (sameKey >= perKey)
            {
                var weakest = (Voice)slots[sameKeyWeakest];
                if (!Outranks(priority, gain, weakest)) return -1;
                victim = sameKeyWeakest;
            }
            else if (victim < 0)
            {
                for (int i = 0; i < slots.Length; i++)
                    if (victim < 0 || Weaker((Voice)slots[i], (Voice)slots[victim])) victim = i;
                if (!Outranks(priority, gain, (Voice)slots[victim])) return -1;
            }
            slots[victim] = new Voice(victim, key, priority, gain, now + Math.Max(0.01, seconds));
            return victim;
        }

        public void Release(int slot)
        {
            if (slot >= 0 && slot < slots.Length) slots[slot] = null;
        }

        static bool Weaker(Voice a, Voice b) => a.Priority != b.Priority ? a.Priority < b.Priority : a.Gain < b.Gain;
        static bool Outranks(int priority, float gain, Voice v) => priority != v.Priority ? priority > v.Priority : gain > v.Gain;
    }
}
