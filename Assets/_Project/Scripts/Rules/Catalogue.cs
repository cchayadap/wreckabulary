using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public enum ItemCategory { Combat, Furniture, Utilities, Consumables, Legacy }

    public enum ItemTier { Core, Expanded, Legacy }

    public enum HandlingFamily
    {
        None,
        MeleeSwing,
        MeleeThrust,
        Thrown,
        Shield,
        DeployCover,
        DeployPad,
        DeploySpeed,
        DeployZone,
        Buff,
        Heal,
        Ranged,
        Utility,
    }

    public sealed class MeleeStats
    {
        public float Damage;
        public float Reach;
        public float ArcDegrees;
        public float Windup;
        public float Active;
        public float Recovery;
        public float Knockback;
        public float BreakPower;
        public float HitStun;

        public float Cycle => Windup + Active + Recovery;
    }

    public sealed class ThrownStats
    {
        public float Damage;
        public float Speed;
        public bool Lob;
        public bool Recoverable;
        public float FuseSeconds;
        public float Radius;
        public float EdgeDamage;
        public float Knockback;
        public float BreakPower;
    }

    public sealed class ShieldStats
    {
        public float FrontArcDegrees;
        public float DamageReduction;
        public float MoveSpeedMultiplier;
        public float RaiseSeconds;
    }

    public enum DeployEffect { Cover, JumpPad, SpeedStrip, SlipZone, WindField, SlowField }

    public sealed class DeployStats
    {
        public DeployEffect Effect;
        public float FootprintX;
        public float FootprintZ;
        public float PlaceSeconds;
        public float Strength;
        public float LifetimeSeconds;
        public float Radius;
        public float ArcDegrees = 360f;
    }

    public enum UseEffect { None, Bubble, Heal, Speed }

    public sealed class UseStats
    {
        public UseEffect Effect;
        public float Amount;
        public float Seconds;
        public float ChannelSeconds;
    }

    public sealed class ItemDefinition
    {
        public string Id;
        public LetterBag Letters;
        public ItemCategory Category;
        public ItemTier Tier;
        public bool Enabled;
        public bool Consumable;
        public HandlingFamily Family;
        public int Hands = 1;
        public string Model;
        public float HeldScale = 1f;
        public float[] Grip = new float[3];
        public float[] Size = new float[3];
        public List<string> Skins = new List<string> { Skin.Standard };
        public int Durability;
        public MeleeStats Melee;
        public ThrownStats Thrown;
        public ShieldStats Shield;
        public DeployStats Deploy;
        public UseStats Use;
        public List<string> LegacyWords = new List<string>();
        public string Notes = "";

        public bool CanDeploy => Deploy != null;
        public bool IsTwoHanded => Hands == 2;
        public bool HasSkin(string skin) => skin != null && Skins.Contains(skin);

        public override string ToString() => Id;
    }

    public readonly struct RecipeCard
    {
        public readonly ItemDefinition Item;
        public readonly LetterBag Missing;
        public readonly bool Craftable;

        public RecipeCard(ItemDefinition item, LetterBag missing)
        {
            Item = item;
            Missing = missing;
            Craftable = missing.IsEmpty;
        }

        public int Have(char letter) => Item.Letters[letter] - Missing[letter];
    }

    public sealed class ItemCatalogue
    {
        readonly Dictionary<string, ItemDefinition> byId = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        readonly List<ItemDefinition> ordered = new List<ItemDefinition>();

        public IReadOnlyList<ItemDefinition> All => ordered;

        public IEnumerable<ItemDefinition> Enabled => ordered.Where(i => i.Enabled);

        public void Add(ItemDefinition item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (!LetterBag.IsWord(item.Id)) throw new ArgumentException($"Item id '{item.Id}' must be A-Z letters only.");
            if (byId.ContainsKey(item.Id)) throw new ArgumentException($"Item '{item.Id}' is listed twice.");
            item.Letters = LetterBag.FromWord(item.Id);
            byId.Add(item.Id, item);
            ordered.Add(item);
        }

        public bool TryGet(string id, out ItemDefinition item) => byId.TryGetValue(id ?? "", out item);

        public ItemDefinition Get(string id)
        {
            if (TryGet(id, out var item)) return item;
            throw new KeyNotFoundException($"No item '{id}' in the catalogue.");
        }

        public List<RecipeCard> Cards(LetterBag have)
        {
            return Enabled
                .Select(i => new RecipeCard(i, have.Missing(i.Letters)))
                .OrderBy(c => c.Missing.Count)
                .ThenBy(c => c.Item.Tier)
                .ThenBy(c => c.Item.Id, StringComparer.Ordinal)
                .ToList();
        }

        public List<string> Validate(int maxLetters)
        {
            var problems = new List<string>();
            foreach (var item in ordered)
            {
                string p = item.Id + ": ";
                if (item.Letters.Count > maxLetters)
                    problems.Add(p + $"needs {item.Letters.Count} letters but a bag holds {maxLetters}");
                if (!item.HasSkin(Skin.Standard))
                    problems.Add(p + "has no Classic skin to fall back to");
                if (!item.Enabled) continue;
                if (string.IsNullOrEmpty(item.Model)) problems.Add(p + "is enabled but has no model");
                if (item.Family == HandlingFamily.None) problems.Add(p + "is enabled but has no handling family");
                if (!Finite(item.HeldScale) || item.HeldScale <= 0f || item.HeldScale > 1f) problems.Add(p + $"held scale {item.HeldScale} is outside (0, 1]");
                if (item.Hands != 1 && item.Hands != 2) problems.Add(p + "must use 1 or 2 hands");
                if (!item.Consumable && item.Durability <= 0) problems.Add(p + "is reusable but has no durability");
                switch (item.Family)
                {
                    case HandlingFamily.MeleeSwing:
                    case HandlingFamily.MeleeThrust:
                        if (item.Melee == null) problems.Add(p + "is a melee item without melee stats");
                        else if (item.Melee.Damage <= 0 || item.Melee.Reach <= 0) problems.Add(p + "melee damage and reach must be positive");
                        break;
                    case HandlingFamily.Thrown:
                        if (item.Thrown == null) problems.Add(p + "is thrown without thrown stats");
                        break;
                    case HandlingFamily.Shield:
                        if (item.Shield == null) problems.Add(p + "is a shield without shield stats");
                        break;
                    case HandlingFamily.DeployCover:
                    case HandlingFamily.DeployPad:
                    case HandlingFamily.DeploySpeed:
                    case HandlingFamily.DeployZone:
                        if (item.Deploy == null) problems.Add(p + "is deployable without deploy stats");
                        break;
                    case HandlingFamily.Buff:
                    case HandlingFamily.Heal:
                        if (item.Use == null) problems.Add(p + "is usable without use stats");
                        break;
                }
                if (item.Consumable && item.Family == HandlingFamily.MeleeSwing)
                    problems.Add(p + "a consumable can't be a melee weapon");
                if (item.Family is HandlingFamily.Ranged or HandlingFamily.Utility)
                    problems.Add(p + "handling family has no enabled runtime implementation");
                ValidateStats(item, problems);
            }
            return problems;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Positive(float value) => Finite(value) && value > 0f;
        static bool NonNegative(float value) => Finite(value) && value >= 0f;
        static bool Arc(float value) => Positive(value) && value <= 360f;

        static void ValidateStats(ItemDefinition item, List<string> problems)
        {
            void Require(bool valid, string message) { if (!valid) problems.Add(item.Id + ": " + message); }
            Require(item.Size != null && item.Size.Length == 3 && item.Size.All(Positive), "model size must have three positive dimensions");
            Require(item.Grip != null && item.Grip.Length == 3 && item.Grip.All(Finite), "grip must have three finite coordinates");
            if (item.Melee != null)
            {
                var m = item.Melee;
                Require(Positive(m.Damage) && Positive(m.Reach) && Arc(m.ArcDegrees), "melee damage, reach and arc must be valid");
                Require(NonNegative(m.Windup) && Positive(m.Active) && NonNegative(m.Recovery), "melee timing must include a positive active window");
                Require(NonNegative(m.Knockback) && NonNegative(m.BreakPower) && NonNegative(m.HitStun), "melee impact values must be non-negative");
            }
            if (item.Thrown != null)
            {
                var t = item.Thrown;
                Require(Positive(t.Damage) && Positive(t.Speed), "thrown damage and speed must be positive");
                Require(NonNegative(t.FuseSeconds) && NonNegative(t.Radius) && NonNegative(t.Knockback) && NonNegative(t.BreakPower), "thrown timing and impact values must be non-negative");
                Require(t.FuseSeconds <= 0f || (Positive(t.Radius) && NonNegative(t.EdgeDamage) && t.EdgeDamage <= t.Damage), "fused throws need a radius and valid edge damage");
                Require(!item.Consumable || !t.Recoverable, "a consumed throw cannot also be recoverable");
            }
            if (item.Shield != null)
            {
                var s = item.Shield;
                Require(Arc(s.FrontArcDegrees), "shield arc must be in (0, 360]");
                Require(Positive(s.DamageReduction) && s.DamageReduction <= 1f, "shield reduction must be in (0, 1]");
                Require(Positive(s.MoveSpeedMultiplier) && s.MoveSpeedMultiplier <= 1f && NonNegative(s.RaiseSeconds), "shield movement and raise timing must be valid");
            }
            if (item.Deploy != null)
            {
                var d = item.Deploy;
                Require(Positive(d.FootprintX) && Positive(d.FootprintZ) && NonNegative(d.PlaceSeconds), "deploy footprint and placement timing must be valid");
                Require(Arc(d.ArcDegrees) && NonNegative(d.LifetimeSeconds) && NonNegative(d.Radius), "deploy arc, lifetime and radius must be valid");
                if (d.Effect == DeployEffect.JumpPad || d.Effect == DeployEffect.WindField)
                    Require(Positive(d.Strength), "launch or wind strength must be positive");
                if (d.Effect == DeployEffect.SpeedStrip) Require(Finite(d.Strength) && d.Strength > 1f, "speed strip must increase movement speed");
                if (d.Effect == DeployEffect.SlowField) Require(Positive(d.Strength) && d.Strength < 1f, "slow field strength must be a multiplier in (0, 1)");
                if (d.Effect is DeployEffect.SlipZone or DeployEffect.WindField or DeployEffect.SlowField)
                    Require(Positive(d.Radius) && Positive(d.LifetimeSeconds), "area effects need a positive radius and lifetime");
            }
            if (item.Use != null)
            {
                var u = item.Use;
                Require(item.Consumable, "use effects must consume their item");
                Require(u.Effect != UseEffect.None && Positive(u.Amount) && NonNegative(u.ChannelSeconds), "use effects need an effect, positive amount and valid channel");
                Require(NonNegative(u.Seconds), "use duration must be non-negative");
                if (u.Effect is UseEffect.Bubble or UseEffect.Speed) Require(Positive(u.Seconds), "temporary effects need a positive duration");
                if (u.Effect == UseEffect.Speed) Require(u.Amount > 1f, "speed use must increase movement speed");
            }
        }

        public static ItemCatalogue FromJson(string json, string source = "items.json")
        {
            var root = Json.Parse(json, source);
            var catalogue = new ItemCatalogue();
            foreach (var n in root["items"].Items)
            {
                var item = new ItemDefinition
                {
                    Id = n["id"].String(),
                    Category = ParseEnum<ItemCategory>(n["category"]),
                    Tier = ParseEnum<ItemTier>(n["tier"]),
                    Enabled = n["enabled"].Bool(false),
                    Consumable = n["consumable"].Bool(false),
                    Family = n.Has("family") ? ParseEnum<HandlingFamily>(n["family"]) : HandlingFamily.None,
                    Hands = n["hands"].Int(1),
                    Model = n["model"].String(null),
                    HeldScale = n["heldScale"].Float(1f),
                    Durability = n["durability"].Int(0),
                    Notes = n["notes"].String(""),
                };
                if (n.Has("grip")) item.Grip = n["grip"].Floats(3);
                if (n.Has("size")) item.Size = n["size"].Floats(3);
                if (n.Has("skins")) item.Skins = n["skins"].Strings();
                if (n.Has("legacyWords")) item.LegacyWords = n["legacyWords"].Strings();
                if (n.Has("melee")) item.Melee = ReadMelee(n["melee"]);
                if (n.Has("thrown"))
                {
                    var t = n["thrown"];
                    item.Thrown = new ThrownStats
                    {
                        Damage = t["damage"].Float(),
                        Speed = t["speed"].Float(),
                        Lob = t["lob"].Bool(false),
                        Recoverable = t["recoverable"].Bool(false),
                        FuseSeconds = t["fuse"].Float(0f),
                        Radius = t["radius"].Float(0f),
                        EdgeDamage = t["edgeDamage"].Float(0f),
                        Knockback = t["knockback"].Float(0f),
                        BreakPower = t["breakPower"].Float(1f),
                    };
                }
                if (n.Has("shield"))
                {
                    var s = n["shield"];
                    item.Shield = new ShieldStats
                    {
                        FrontArcDegrees = s["frontArc"].Float(),
                        DamageReduction = s["reduction"].Float(),
                        MoveSpeedMultiplier = s["moveSpeed"].Float(1f),
                        RaiseSeconds = s["raise"].Float(0.1f),
                    };
                }
                if (n.Has("deploy"))
                {
                    var d = n["deploy"];
                    var footprint = d["footprint"].Floats(2);
                    item.Deploy = new DeployStats
                    {
                        Effect = ParseEnum<DeployEffect>(d["effect"]),
                        FootprintX = footprint[0],
                        FootprintZ = footprint[1],
                        PlaceSeconds = d["place"].Float(0.5f),
                        Strength = d["strength"].Float(0f),
                        LifetimeSeconds = d["lifetime"].Float(0f),
                        Radius = d["radius"].Float(0f),
                        ArcDegrees = d["arc"].Float(360f),
                    };
                }
                if (n.Has("use"))
                {
                    var u = n["use"];
                    item.Use = new UseStats
                    {
                        Effect = ParseEnum<UseEffect>(u["effect"]),
                        Amount = u["amount"].Float(0f),
                        Seconds = u["seconds"].Float(0f),
                        ChannelSeconds = u["channel"].Float(0.4f),
                    };
                }
                catalogue.Add(item);
            }
            return catalogue;
        }

        internal static MeleeStats ReadMelee(JsonNode m) => new MeleeStats
        {
            Damage = m["damage"].Float(),
            Reach = m["reach"].Float(),
            ArcDegrees = m["arc"].Float(),
            Windup = m["windup"].Float(),
            Active = m["active"].Float(),
            Recovery = m["recovery"].Float(),
            Knockback = m["knockback"].Float(0f),
            BreakPower = m["breakPower"].Float(1f),
            HitStun = m["hitStun"].Float(0.2f),
        };

        internal static T ParseEnum<T>(JsonNode node) where T : struct
        {
            string s = node.String();
            var names = Enum.GetNames(typeof(T));
            if (Array.IndexOf(names, s) >= 0) return (T)Enum.Parse(typeof(T), s);
            throw node.Error($"'{s}' is not one of {string.Join(", ", names)}");
        }
    }
}
