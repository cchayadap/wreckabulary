using System;

namespace Wreckabulary.Rules
{
    public enum HelperBody { Stays, Walks, Hops }

    public enum HelperPick { Aimed, Nearest, Provoker, Stepper }

    public enum HelperBite { Enemies, Anyone }

    public enum HelperAttack { None, Touch, Swing, Shot, Burst }

    public sealed class HelperSpec
    {
        public const string Key = "helper";
        public const int BurstTargets = 4;

        static readonly string[] Keys =
        {
            "body", "pick", "bites", "attack", "damage", "range", "windup", "cooldown", "knockback",
            "speed", "sight", "leash", "radius", "seconds", "strikes", "health", "slow", "slowSeconds", "shotSpeed",
        };

        public string Id = "";
        public HelperBody Body;
        public HelperPick Pick;
        public HelperBite Bites;
        public HelperAttack Attack;
        public float Damage, Range, Windup, Cooldown, Knockback;
        public float Speed, Sight, Leash, Radius;
        public float Seconds;
        public int Strikes;
        public float Health;
        public float Slow, SlowSeconds;
        public float ShotSpeed;

        public bool Moves => Body != HelperBody.Stays;
        public bool Hurts => Attack != HelperAttack.None && Damage > 0f;
        public bool Slows => Slow > 0f && SlowSeconds > 0f;
        public bool HomeIsMaker => Moves && Pick == HelperPick.Provoker;
        public bool IsTrap => Pick == HelperPick.Stepper;
        public float Reach => Moves ? Sight : Radius;
        public float Cycle => Windup + Cooldown;
        public float DamagePerSecond => Hurts && Cycle > 0f ? Damage / Cycle : 0f;
        public float TotalDamage => Hurts ? Damage * (Attack == HelperAttack.Burst ? BurstTargets : Strikes) : 0f;
        public float Strength => Math.Min(TotalDamage, DamagePerSecond * Seconds);

        public static HelperSpec Read(string id, JsonNode n)
        {
            foreach (string key in n.Keys)
                if (Array.IndexOf(Keys, key) < 0) throw n.Error($"'{key}' is not a helper field");
            return new HelperSpec
            {
                Id = id ?? "",
                Body = ItemCatalogue.ParseEnum<HelperBody>(n["body"]),
                Pick = ItemCatalogue.ParseEnum<HelperPick>(n["pick"]),
                Bites = ItemCatalogue.ParseEnum<HelperBite>(n["bites"]),
                Attack = ItemCatalogue.ParseEnum<HelperAttack>(n["attack"]),
                Damage = n["damage"].Float(0f),
                Range = n["range"].Float(0f),
                Windup = n["windup"].Float(0f),
                Cooldown = n["cooldown"].Float(0f),
                Knockback = n["knockback"].Float(0f),
                Speed = n["speed"].Float(0f),
                Sight = n["sight"].Float(0f),
                Leash = n["leash"].Float(0f),
                Radius = n["radius"].Float(0f),
                Seconds = n["seconds"].Float(),
                Strikes = n["strikes"].Int(1),
                Health = n["health"].Float(),
                Slow = n["slow"].Float(0f),
                SlowSeconds = n["slowSeconds"].Float(0f),
                ShotSpeed = n["shotSpeed"].Float(0f),
            };
        }

        public string CardLine()
        {
            bool anyone = Bites == HelperBite.Anyone;
            if (!Hurts) return Moves ? "Wanders about and gets in the way" : $"Stands here for {Seconds:0} s";
            if (IsTrap) return anyone ? "Snaps at anyone who steps on it" : "Snaps at foes who step on it";
            return Pick switch
            {
                HelperPick.Aimed when Moves => anyone ? "Chases the one you aim at. Even you!" : "Chases the one you aim at",
                HelperPick.Aimed => anyone ? "Shoots at the one you aim at. Even you!" : "Shoots at the one you aim at",
                HelperPick.Provoker when Moves => "Follows you and bites whoever hurts you",
                HelperPick.Provoker => "Leave it be. Hit it and it hits back",
                _ when Moves => anyone ? "Chases anyone near. Even you!" : "Chases the nearest foe",
                _ => anyone ? "Bites anyone near, even you" : "Guards this spot from foes",
            };
        }

        public string Details()
        {
            if (!Hurts) return $"{Seconds:0} s · {Health:0} health";
            string hits = Attack == HelperAttack.Burst ? $"{Damage:0} to everyone near, once" : $"{Damage:0} x {Strikes}";
            string slow = Slows ? $" · slows to {Slow * 100f:0}% for {SlowSeconds:0.#} s" : "";
            return $"{hits} · {Seconds:0} s · {Health:0} health{slow}";
        }
    }
}
