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
        public float MaxStamina = 100f;
        public float DodgeStamina = 30f;
        public float StaminaRegen = 20f;
        public float StaminaRegenDelay = 1f;
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

        public bool EscapeNeedsKey = true;

        public int EscapeKeyAfterRooms = 2;

        public bool EscapeRandomExit = true;

        public int EscapeKeys = 1;

<<<<<<< HEAD
        public int KnockoutsToWin { get => Setting("knockoutsToWin", 0); set => SetSetting("knockoutsToWin", value); }
        public int TeamLives { get => Setting("teamLives", 0); set => SetSetting("teamLives", value); }
        public float HoldBonusSeconds { get => Setting("holdBonus", 0f); set => SetSetting("holdBonus", value); }
        Dictionary<string, float> settings;

        public float Setting(string key, float fallback) => settings != null && key != null && settings.TryGetValue(key, out float value) ? value : fallback;

        public int Setting(string key, int fallback) => (int)Math.Round(Setting(key, (float)fallback));

        public void SetSetting(string key, float value) => (settings ??= new Dictionary<string, float>(StringComparer.Ordinal))[key] = value;
=======
        // @trace NUM-001, NUM-002, NUM-003, NUM-005
        /// <summary>Boss Fight: the label shown on the boss bar and the three shield words.</summary>
        public string BossName = "";
        public string[] BossWords = System.Array.Empty<string>();
        public float BossMaxHealth = 240f;
        public float BossVulnerability = 1.5f;
        public float PrepSeconds = 45f;
        public float AppearSeconds = 12f;
        public float LayerSeconds = 35f;
        public float HitSeconds = 75f;
        public float AoeWarnSeconds = 1.2f;
        public float AoeActiveSeconds = 0.45f;
        public float[] AoeRadiusByLayer = { 1.6f, 1.8f, 2f, 2.2f };
        public float[] AoeDamageByLayer = { 10f, 12f, 14f, 14f };
        public float[] AoeCooldownByLayer = { 4.5f, 4f, 3.2f, 2.8f };
        public int MaxAoe = 3;
        public int LettersLostOnHit = 2;
        public float SlickDodgeWindow = 0.3f;

        static float LayerValue(float[] values, int layer, float fallback) =>
            values != null && layer >= 0 && layer < values.Length ? values[layer] : fallback;

        /// <summary>Layer 0..2 are the shield layers; layer 3 is the vulnerable hit phase.</summary>
        public float AoeRadiusFor(int layer) => LayerValue(AoeRadiusByLayer, layer, 1.6f);
        public float AoeDamageFor(int layer) => LayerValue(AoeDamageByLayer, layer, 10f);
        public float AoeCooldownFor(int layer) => LayerValue(AoeCooldownByLayer, layer, 4.5f);
>>>>>>> fd3f0126 (first commit)

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
<<<<<<< HEAD
            if (settings != null) copy.settings = new Dictionary<string, float>(settings, StringComparer.Ordinal);
=======
            copy.BossWords = (string[])BossWords.Clone();
            copy.AoeRadiusByLayer = (float[])AoeRadiusByLayer.Clone();
            copy.AoeDamageByLayer = (float[])AoeDamageByLayer.Clone();
            copy.AoeCooldownByLayer = (float[])AoeCooldownByLayer.Clone();
>>>>>>> fd3f0126 (first commit)
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
            Need(MaxStamina > 0 && DodgeStamina >= 0 && DodgeStamina <= MaxStamina, "dodge stamina must fit the stamina pool");
            Need(StaminaRegen >= 0 && StaminaRegenDelay >= 0, "stamina recovery cannot be negative");
            Need(JumpHeight > 0, "jumpHeight must be positive");
            Need(GroundAccel > 0 && GroundFriction > 0, "groundAccel and groundFriction must be positive");
<<<<<<< HEAD
            Need(KnockoutsToWin >= 0 && TeamLives >= 0 && HoldBonusSeconds >= 0, "knockoutsToWin, teamLives and holdBonus can't be negative");
=======
            // @trace NUM-005
            Need(BossWords == null || BossWords.Length == 0 || BossWords.Length == 3, "bossWords needs exactly three shield words");
            Need(BossMaxHealth > 0f, "bossMaxHealth must be positive");
            Need(BossVulnerability > 0f, "bossVulnerability must be positive");
            Need(PrepSeconds >= 0f && AppearSeconds >= 0f, "prep and appear phases cannot be negative");
            Need(LayerSeconds > 0f && HitSeconds > 0f, "layer and hit phases need a positive duration");
            Need(AoeWarnSeconds > SlickDodgeWindow && SlickDodgeWindow >= 0f, "slickDodgeWindow must fit inside aoeWarnSeconds");
            Need(AoeActiveSeconds > 0f, "aoeActiveSeconds must be positive");
            Need(MaxAoe >= 1, "maxAoe must be at least 1");
            Need(LettersLostOnHit >= 0, "lettersLostOnHit cannot be negative");
>>>>>>> fd3f0126 (first commit)
            return problems;
        }
    }

    public sealed class RuleBook
    {
        readonly Dictionary<string, GameRules> modes = new Dictionary<string, GameRules>(StringComparer.Ordinal);
        public GameRules Defaults { get; private set; } = new GameRules();

        readonly List<string> layered = new List<string>();

        public IEnumerable<string> Modes => modes.Keys;

        public IReadOnlyList<string> LayeredModes => layered;

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

        public void Override(string mode, JsonNode settings) => Apply(For(mode), settings);

        public void Layer(string json, string source = "modes_house.json")
        {
            var root = Json.Parse(json, source);
            foreach (string key in root.Keys)
                if (key != "notes" && key != "modes") throw root.Error($"'{key}' is not a House modes setting");
            var list = root["modes"];
            foreach (string mode in list.Keys)
            {
                var n = list[mode];
                if (modes.ContainsKey(mode)) throw n.Error($"'{mode}' is a rules.json mode; House modes may only add new ones");
                string from = n["base"].String("");
                if (from.Length > 0 && !modes.ContainsKey(from)) throw n.Error($"base '{from}' is not a known mode");
                var rules = Apply((from.Length > 0 ? modes[from] : Defaults).Clone(), n, layered: true);
                rules.Mode = mode;
                modes[mode] = rules;
                layered.Add(mode);
            }
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
            "exitNeedsKey", "keyAfterRooms", "randomExit", "exitKeys",
            "bossName", "bossWords", "bossMaxHealth", "bossVulnerability", "prepSeconds", "appearSeconds",
            "layerSeconds", "hitSeconds", "aoeWarnSeconds", "aoeActiveSeconds", "aoeRadiusByLayer",
            "aoeDamageByLayer", "aoeCooldownByLayer", "maxAoe", "lettersLostOnHit", "slickDodgeWindow",
        };

        static readonly HashSet<string> HouseOnly = new HashSet<string>(StringComparer.Ordinal) { "base", "knockoutsToWin", "teamLives", "holdBonus", "settings" };

        static readonly string[] ClearOutKeys = { "enabled", "firstAt", "interval", "warn", "fill", "damagePerSecond", "damageGrowth" };

        static GameRules Apply(GameRules r, JsonNode n, bool layered = false)
        {
            if (n.IsNull) return r;
            foreach (string key in n.Keys)
                if (!Known.Contains(key) && !(layered && HouseOnly.Contains(key))) throw n.Error(layered || !HouseOnly.Contains(key) ? $"'{key}' is not a rules setting" : $"'{key}' belongs in modes_house.json");
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
            r.MaxStamina = n["maxStamina"].Float(r.MaxStamina);
            r.DodgeStamina = n["dodgeStamina"].Float(r.DodgeStamina);
            r.StaminaRegen = n["staminaRegen"].Float(r.StaminaRegen);
            r.StaminaRegenDelay = n["staminaRegenDelay"].Float(r.StaminaRegenDelay);
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
            r.EscapeNeedsKey = n["exitNeedsKey"].Bool(r.EscapeNeedsKey);
            r.EscapeKeyAfterRooms = n["keyAfterRooms"].Int(r.EscapeKeyAfterRooms);
            r.EscapeRandomExit = n["randomExit"].Bool(r.EscapeRandomExit);
            r.EscapeKeys = n["exitKeys"].Int(r.EscapeKeys);
<<<<<<< HEAD
            r.KnockoutsToWin = n["knockoutsToWin"].Int(r.KnockoutsToWin);
            r.TeamLives = n["teamLives"].Int(r.TeamLives);
            r.HoldBonusSeconds = n["holdBonus"].Float(r.HoldBonusSeconds);
            if (n.Has("settings"))
            {
                var extra = n["settings"];
                foreach (string key in extra.Keys) r.SetSetting(key, extra[key].Float());
            }
=======
            // @trace NUM-005
            r.BossName = n["bossName"].String(r.BossName);
            var words = n["bossWords"].Strings();
            if (words != null)
            {
                r.BossWords = new string[words.Count];
                for (int i = 0; i < words.Count; i++) r.BossWords[i] = words[i].ToUpperInvariant();
            }
            r.BossMaxHealth = n["bossMaxHealth"].Float(r.BossMaxHealth);
            r.BossVulnerability = n["bossVulnerability"].Float(r.BossVulnerability);
            r.PrepSeconds = n["prepSeconds"].Float(r.PrepSeconds);
            r.AppearSeconds = n["appearSeconds"].Float(r.AppearSeconds);
            r.LayerSeconds = n["layerSeconds"].Float(r.LayerSeconds);
            r.HitSeconds = n["hitSeconds"].Float(r.HitSeconds);
            r.AoeWarnSeconds = n["aoeWarnSeconds"].Float(r.AoeWarnSeconds);
            r.AoeActiveSeconds = n["aoeActiveSeconds"].Float(r.AoeActiveSeconds);
            if (n.Has("aoeRadiusByLayer")) r.AoeRadiusByLayer = n["aoeRadiusByLayer"].Floats(4);
            if (n.Has("aoeDamageByLayer")) r.AoeDamageByLayer = n["aoeDamageByLayer"].Floats(4);
            if (n.Has("aoeCooldownByLayer")) r.AoeCooldownByLayer = n["aoeCooldownByLayer"].Floats(4);
            r.MaxAoe = n["maxAoe"].Int(r.MaxAoe);
            r.LettersLostOnHit = n["lettersLostOnHit"].Int(r.LettersLostOnHit);
            r.SlickDodgeWindow = n["slickDodgeWindow"].Float(r.SlickDodgeWindow);
>>>>>>> fd3f0126 (first commit)
            return r;
        }
    }
}
