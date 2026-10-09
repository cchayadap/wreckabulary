using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules
{
    public sealed class GameRules
    {
        public string Mode = "Default";

        public float MaxHealth = 100f;
        public float SpawnProtectionSeconds = 2f;

        public int MaxLetters = 10;
        public int MaxCarried = 2;
        public int MaxDeployed = 2;
        public string StarterLetters = "";
        public int LettersDroppedPerHit;
        public float FurnitureToughnessBase = 1f;
        public float FurnitureToughnessPerLetter = 0.6f;

        public float CraftBaseSeconds = 0.6f;
        public float CraftPerLetterSeconds = 0.12f;
        public float CraftMoveSpeed = 0.4f;

        public float HitStunMax = 0.35f;
        public float StaggerImmunitySeconds = 1f;
        public float DodgeSeconds = 0.35f;
        public float DodgeInvulnerableSeconds = 0.15f;
        public float DodgeDistance = 4f;
        public float DodgeCooldown = 1.5f;
        public float JumpHeight = 1.1f;
        public float GroundAccel = 36f;
        public float GroundFriction = 28f;
        public MeleeStats Unarmed = new MeleeStats
        {
            Damage = 8f, Reach = 1.1f, ArcDegrees = 90f, Windup = 0.12f, Active = 0.08f,
            Recovery = 0.25f, Knockback = 3f, BreakPower = 1f, HitStun = 0.15f,
        };

        public int TeamSize = 1;
        public bool FriendlyFire = true;
        public bool DownedEnabled;
        public float[] BleedOutSeconds = { 20f, 12f, 6f };
        public float ReviveSeconds = 3f;
        public float ReviveHealth = 30f;
        public float ReviveRange = 1.8f;

        public int RoundsToWin = 3;
        public float RespawnSeconds = -1f;
        public float ReconnectGraceSeconds = 60f;
        public float RoundTimeLimitSeconds = 150f;

        public bool ClearOutEnabled;
        public float ClearOutFirstAt = 45f;
        public float ClearOutInterval = 25f;
        public float ClearOutWarnSeconds = 10f;
        public float ClearOutFillSeconds = 6f;
        public float ClearOutDamagePerSecond = 8f;
        public float ClearOutDamageGrowth = 2f;

        public float CraftSeconds(ItemDefinition item) => CraftBaseSeconds + CraftPerLetterSeconds * item.Letters.Count;

        public float BleedOutFor(int timesDowned)
        {
            if (BleedOutSeconds == null || BleedOutSeconds.Length == 0) return 0f;
            int i = Math.Min(Math.Max(timesDowned - 1, 0), BleedOutSeconds.Length - 1);
            return BleedOutSeconds[i];
        }

        public GameRules Clone()
        {
            var copy = (GameRules)MemberwiseClone();
            copy.BleedOutSeconds = (float[])BleedOutSeconds.Clone();
            var u = Unarmed;
            copy.Unarmed = new MeleeStats
            {
                Damage = u.Damage, Reach = u.Reach, ArcDegrees = u.ArcDegrees, Windup = u.Windup, Active = u.Active,
                Recovery = u.Recovery, Knockback = u.Knockback, BreakPower = u.BreakPower, HitStun = u.HitStun,
            };
            return copy;
        }

        public List<string> Validate()
        {
            var problems = new List<string>();
            void Need(bool ok, string message) { if (!ok) problems.Add(Mode + ": " + message); }
            Need(MaxHealth > 0, "maxHealth must be positive");
            Need(MaxLetters > 0, "maxLetters must be positive");
            Need(MaxCarried >= 1, "maxCarried must be at least 1");
            Need(MaxDeployed >= 0, "maxDeployed can't be negative");
            Need(LettersDroppedPerHit >= 0, "lettersDroppedPerHit can't be negative");
            Need(StarterLetters.Length <= MaxLetters, "starterLetters don't fit the bag");
            Need(StarterLetters.Length == 0 || LetterBag.IsWord(StarterLetters), "starterLetters must be A-Z");
            Need(TeamSize >= 1, "teamSize must be at least 1");
            Need(RoundsToWin >= 1, "roundsToWin must be at least 1");
            Need(HitStunMax <= StaggerImmunitySeconds, "hit stun must not outlast stagger immunity, or stuns can chain");
            Need(!DownedEnabled || TeamSize > 1, "downed-and-revive needs teams");
            Need(ReviveHealth > 0 && ReviveHealth <= MaxHealth, "reviveHealth must be within max health");
            Need(DodgeInvulnerableSeconds <= DodgeSeconds, "dodge invulnerability can't outlast the dodge");
            Need(DodgeSeconds > 0 && DodgeDistance >= 0, "a dodge needs a duration and a distance");
            Need(JumpHeight > 0, "jumpHeight must be positive");
            Need(GroundAccel > 0 && GroundFriction > 0, "groundAccel and groundFriction must be positive");
            return problems;
        }
    }

    public sealed class RuleBook
    {
        readonly Dictionary<string, GameRules> modes = new Dictionary<string, GameRules>(StringComparer.Ordinal);
        public GameRules Defaults { get; private set; } = new GameRules();

        public IEnumerable<string> Modes => modes.Keys;

        public GameRules For(string mode)
        {
            if (modes.TryGetValue(mode, out var rules)) return rules;
            throw new KeyNotFoundException($"rules.json has no mode '{mode}'.");
        }

        public static RuleBook FromJson(string json, string source = "rules.json")
        {
            var root = Json.Parse(json, source);
            var book = new RuleBook();
            book.Defaults = Apply(new GameRules(), root["defaults"]);
            book.Defaults.Mode = "defaults";
            foreach (string mode in root["modes"].Keys)
            {
                var rules = Apply(book.Defaults.Clone(), root["modes"][mode]);
                rules.Mode = mode;
                book.modes[mode] = rules;
            }
            return book;
        }

        public List<string> Validate()
        {
            var problems = Defaults.Validate();
            foreach (var r in modes.Values) problems.AddRange(r.Validate());
            return problems;
        }

        static readonly HashSet<string> Known = new HashSet<string>(StringComparer.Ordinal)
        {
            "maxHealth", "spawnProtection", "maxLetters", "maxCarried", "maxDeployed", "starterLetters",
            "lettersDroppedPerHit", "furnitureToughness", "craftBase", "craftPerLetter", "craftMoveSpeed", "hitStunMax",
            "staggerImmunity", "dodgeSeconds", "dodgeInvulnerable", "dodgeDistance", "dodgeCooldown", "jumpHeight",
            "groundAccel", "groundFriction", "unarmed",
            "teamSize", "friendlyFire", "downed", "bleedOut", "reviveSeconds", "reviveHealth", "reviveRange",
            "roundsToWin", "respawn", "reconnectGrace", "roundTimeLimit", "clearOut", "notes",
        };

        static readonly string[] ClearOutKeys = { "enabled", "firstAt", "interval", "warn", "fill", "damagePerSecond", "damageGrowth" };

        static GameRules Apply(GameRules r, JsonNode n)
        {
            if (n.IsNull) return r;
            foreach (string key in n.Keys)
                if (!Known.Contains(key)) throw n.Error($"'{key}' is not a rules setting");
            r.MaxHealth = n["maxHealth"].Float(r.MaxHealth);
            r.SpawnProtectionSeconds = n["spawnProtection"].Float(r.SpawnProtectionSeconds);
            r.MaxLetters = n["maxLetters"].Int(r.MaxLetters);
            r.MaxCarried = n["maxCarried"].Int(r.MaxCarried);
            r.MaxDeployed = n["maxDeployed"].Int(r.MaxDeployed);
            r.StarterLetters = n["starterLetters"].String(r.StarterLetters);
            r.LettersDroppedPerHit = n["lettersDroppedPerHit"].Int(r.LettersDroppedPerHit);
            if (n.Has("furnitureToughness"))
            {
                var ft = n["furnitureToughness"].Floats(2);
                r.FurnitureToughnessBase = ft[0];
                r.FurnitureToughnessPerLetter = ft[1];
            }
            r.CraftBaseSeconds = n["craftBase"].Float(r.CraftBaseSeconds);
            r.CraftPerLetterSeconds = n["craftPerLetter"].Float(r.CraftPerLetterSeconds);
            r.CraftMoveSpeed = n["craftMoveSpeed"].Float(r.CraftMoveSpeed);
            r.HitStunMax = n["hitStunMax"].Float(r.HitStunMax);
            r.StaggerImmunitySeconds = n["staggerImmunity"].Float(r.StaggerImmunitySeconds);
            r.DodgeSeconds = n["dodgeSeconds"].Float(r.DodgeSeconds);
            r.DodgeInvulnerableSeconds = n["dodgeInvulnerable"].Float(r.DodgeInvulnerableSeconds);
            r.DodgeDistance = n["dodgeDistance"].Float(r.DodgeDistance);
            r.DodgeCooldown = n["dodgeCooldown"].Float(r.DodgeCooldown);
            r.JumpHeight = n["jumpHeight"].Float(r.JumpHeight);
            r.GroundAccel = n["groundAccel"].Float(r.GroundAccel);
            r.GroundFriction = n["groundFriction"].Float(r.GroundFriction);
            if (n.Has("unarmed")) r.Unarmed = ItemCatalogue.ReadMelee(n["unarmed"]);
            r.TeamSize = n["teamSize"].Int(r.TeamSize);
            r.FriendlyFire = n["friendlyFire"].Bool(r.FriendlyFire);
            r.DownedEnabled = n["downed"].Bool(r.DownedEnabled);
            if (n.Has("bleedOut"))
            {
                var items = n["bleedOut"].Items;
                r.BleedOutSeconds = new float[items.Count];
                for (int i = 0; i < items.Count; i++) r.BleedOutSeconds[i] = items[i].Float();
            }
            r.ReviveSeconds = n["reviveSeconds"].Float(r.ReviveSeconds);
            r.ReviveHealth = n["reviveHealth"].Float(r.ReviveHealth);
            r.ReviveRange = n["reviveRange"].Float(r.ReviveRange);
            r.RoundsToWin = n["roundsToWin"].Int(r.RoundsToWin);
            r.RespawnSeconds = n["respawn"].Float(r.RespawnSeconds);
            r.ReconnectGraceSeconds = n["reconnectGrace"].Float(r.ReconnectGraceSeconds);
            r.RoundTimeLimitSeconds = n["roundTimeLimit"].Float(r.RoundTimeLimitSeconds);
            if (n.Has("clearOut"))
            {
                var c = n["clearOut"];
                foreach (string key in c.Keys)
                    if (Array.IndexOf(ClearOutKeys, key) < 0) throw c.Error($"'{key}' is not a clear-out setting");
                r.ClearOutEnabled = c["enabled"].Bool(r.ClearOutEnabled);
                r.ClearOutFirstAt = c["firstAt"].Float(r.ClearOutFirstAt);
                r.ClearOutInterval = c["interval"].Float(r.ClearOutInterval);
                r.ClearOutWarnSeconds = c["warn"].Float(r.ClearOutWarnSeconds);
                r.ClearOutFillSeconds = c["fill"].Float(r.ClearOutFillSeconds);
                r.ClearOutDamagePerSecond = c["damagePerSecond"].Float(r.ClearOutDamagePerSecond);
                r.ClearOutDamageGrowth = c["damageGrowth"].Float(r.ClearOutDamageGrowth);
            }
            return r;
        }
    }
}
