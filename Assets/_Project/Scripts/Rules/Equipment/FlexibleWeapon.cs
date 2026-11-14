using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Wreckabulary.Rules
{
    public enum SwingPhase { Ready, Windup, Active, Recovery }

    public readonly struct StrikeZone
    {
        public readonly StrikePart Part;
        public readonly float From;
        public readonly float To;

        public StrikeZone(StrikePart part, float from, float to)
        {
            Part = part;
            From = from;
            To = to;
        }
    }

    public sealed class FlexibleSwing
    {
        public int Segments = 6;
        public float MaxLagDegrees = 48f;
        public float LeadDegrees = 8f;
        public float ContactToleranceDegrees = 14f;
        public float RecoveryDamping = 7f;
        public float RecoveryHertz = 2.4f;
        public float IdleSwayDegrees = 3f;
        public float IdleSwayHertz = 1.1f;

        public static SwingPhase PhaseAt(MeleeStats m, double since)
        {
            if (m == null || since < 0 || double.IsNaN(since)) return SwingPhase.Ready;
            if (since < m.Windup) return SwingPhase.Windup;
            if (since < m.Windup + m.Active) return SwingPhase.Active;
            if (since < m.Cycle) return SwingPhase.Recovery;
            return SwingPhase.Ready;
        }

        public float TipBend(MeleeStats m, double since)
        {
            switch (PhaseAt(m, since))
            {
                case SwingPhase.Windup:
                    return MaxLagDegrees * Smooth((float)(since / Math.Max(1e-4, m.Windup)));
                case SwingPhase.Active:
                    float t = (float)((since - m.Windup) / Math.Max(1e-4, m.Active));
                    return MaxLagDegrees + (-LeadDegrees - MaxLagDegrees) * Smooth(Math.Min(1f, t * 1.6f));
                case SwingPhase.Recovery:
                    double r = since - m.Windup - m.Active;
                    return (float)(-LeadDegrees * Math.Exp(-RecoveryDamping * r) * Math.Cos(2 * Math.PI * RecoveryHertz * r));
                default:
                    return 0f;
            }
        }

        public float SegmentBend(MeleeStats m, double since, int segment) => TipBend(m, since) * Weight(segment);

        public float IdleBend(double time, float moveSpeed, int segment)
        {
            float amplitude = IdleSwayDegrees * (1f + Math.Min(2f, Math.Max(0f, moveSpeed)) * 0.6f);
            return (float)(amplitude * Math.Sin(2 * Math.PI * IdleSwayHertz * time - segment * 0.45)) * Weight(segment);
        }

        public float Weight(int segment)
        {
            if (Segments <= 0 || segment < 0 || segment >= Segments) return 0f;
            float total = Segments * (Segments + 1) * 0.5f;
            return (segment + 1) / total;
        }

        public float ContactDeviation(MeleeStats m)
        {
            if (m == null) return float.PositiveInfinity;
            float worst = 0f;
            for (int i = 1; i <= 8; i++)
            {
                double at = m.Windup + m.Active * (0.35 + 0.5 * i / 8.0);
                worst = Math.Max(worst, Math.Abs(TipBend(m, at)));
            }
            return worst;
        }

        static float Smooth(float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return t * t * (3f - 2f * t);
        }
    }

    public sealed class FlexibleWeaponDefinition
    {
        public const int Version = 1;
        public string Id = "BELT";
        public MeleeStats Melee;
        public MeleeStats BuckleMelee;
        public int Durability;
        public float Length;
        public float BuckleFrom;
        public float[] Grip = new float[3];
        public float[] Size = new float[3];
        public float HeldScale = 1f;
        public FlexibleSwing Swing = new FlexibleSwing();
        public readonly List<StrikeZone> Zones = new List<StrikeZone>();

        public static FlexibleWeaponDefinition Belt()
        {
            var belt = new FlexibleWeaponDefinition
            {
                Id = "BELT",
                Durability = 22,
                Length = 0.52f,
                BuckleFrom = 0.75f,
                Grip = new[] { 0f, 0.16f, 0f },
                Size = new[] { 0.112f, 1.2f, 0.072f },
                HeldScale = 0.5f,
                Melee = new MeleeStats
                {
                    Damage = 10f,
                    Reach = 1.85f,
                    ArcDegrees = 120f,
                    Windup = 0.22f,
                    Active = 0.1f,
                    Recovery = 0.34f,
                    Knockback = 4f,
                    BreakPower = 0.6f,
                    HitStun = 0.22f,
                },
            };
            belt.BuckleMelee = belt.Tip(14f, 5.5f, 1.5f, 0.3f);
            belt.RebuildZones();
            return belt;
        }

        public MeleeStats Tip(float damage, float knockback, float breakPower, float hitStun) => new MeleeStats
        {
            Damage = damage, Reach = Melee.Reach, ArcDegrees = Melee.ArcDegrees, Windup = Melee.Windup, Active = Melee.Active,
            Recovery = Melee.Recovery, Knockback = knockback, BreakPower = breakPower, HitStun = hitStun,
        };

        public float InHandLength => Size == null || Grip == null || Size.Length < 2 || Grip.Length < 2 ? 0f : (Size[1] - Grip[1]) * HeldScale;

        public MeleeStats StatsFor(StrikePart part) => part == StrikePart.Buckle && BuckleMelee != null ? BuckleMelee : Melee;

        public void RebuildZones()
        {
            Zones.Clear();
            Zones.Add(new StrikeZone(StrikePart.Strap, 0f, BuckleFrom));
            Zones.Add(new StrikeZone(StrikePart.Buckle, BuckleFrom, 1f));
        }

        public StrikePart PartAt(float distance)
        {
            float strapStart = Math.Max(0f, Melee.Reach - Length);
            float along = Length <= 0f ? 1f : (distance - strapStart) / Length;
            along = along < 0f ? 0f : along > 1f ? 1f : along;
            foreach (var zone in Zones) if (along >= zone.From && along <= zone.To) return zone.Part;
            return StrikePart.Strap;
        }

        public ItemDefinition ToItem()
        {
            var item = new ItemDefinition
            {
                Id = Id,
                Category = ItemCategory.Combat,
                Tier = ItemTier.Expanded,
                Enabled = false,
                Family = HandlingFamily.MeleeSwing,
                Hands = 1,
                Model = "Equipment/BELT",
                HeldScale = HeldScale,
                Grip = (float[])Grip.Clone(),
                Size = (float[])Size.Clone(),
                Durability = Durability,
                Melee = Melee,
                Notes = "Practice prototype; not admitted to crafting, Shop or public catalogues.",
            };
            item.Letters = LetterBag.FromWord(Id);
            return item;
        }

        public string Identity()
        {
            var s = new StringBuilder("prototype/").Append(Id).Append("/v").Append(Version);
            foreach (var m in new[] { Melee, BuckleMelee ?? Melee })
                foreach (float v in new[] { m.Damage, m.Reach, m.ArcDegrees, m.Windup, m.Active, m.Recovery, m.Knockback, m.BreakPower, m.HitStun })
                    s.Append('|').Append(v.ToString("0.####", CultureInfo.InvariantCulture));
            foreach (float v in new[] { Length, BuckleFrom, Durability })
                s.Append('|').Append(v.ToString("0.####", CultureInfo.InvariantCulture));
            return s.Append('#').Append(ImpactVariation.Hash(s.ToString()).ToString("x8")).ToString();
        }

        public List<string> Validate()
        {
            var problems = new List<string>();
            void Require(bool ok, string message) { if (!ok) problems.Add(Id + ": " + message); }
            Require(LetterBag.IsWord(Id), "id must be A-Z letters");
            Require(Melee != null, "needs melee stats");
            if (Melee == null) return problems;
            ValidateStats(Melee, "strap", Require);
            if (BuckleMelee != null)
            {
                ValidateStats(BuckleMelee, "buckle", Require);
                Require(BuckleMelee.Reach == Melee.Reach && BuckleMelee.ArcDegrees == Melee.ArcDegrees && BuckleMelee.Cycle == Melee.Cycle,
                    "buckle and strap share one swing: same reach, arc and timing");
            }
            Require(Durability > 0, "needs durability");
            Require(Length > 0.3f && Length <= Melee.Reach, "strap length must be positive and within reach");
            Require(BuckleFrom > 0.5f && BuckleFrom < 1f, "buckle zone must be the outer end of the strap");
            Require(Math.Abs(InHandLength - Length) <= 0.02f, $"strap length {Length} must match the model beyond the grip ({InHandLength:0.###} m)");
            Require(Swing != null && Swing.Segments >= 3 && Swing.Segments <= 10, "strap rig needs 3-10 segments");
            if (Swing != null) Require(Swing.ContactDeviation(Melee) <= Swing.ContactToleranceDegrees, "visual strap bend must match the contact arc during the active window");
            return problems;
        }

        static void ValidateStats(MeleeStats m, string part, Action<bool, string> require)
        {
            void Require(bool ok, string message) => require(ok, part + " " + message);
            Require(m.Damage >= MeleeFairness.MinDamage && m.Damage <= MeleeFairness.MaxDamage, $"damage {m.Damage} is outside [{MeleeFairness.MinDamage}, {MeleeFairness.MaxDamage}]");
            Require(m.Reach >= 1f && m.Reach <= MeleeFairness.MaxSwingReach, $"reach {m.Reach} is outside [1, {MeleeFairness.MaxSwingReach}]");
            Require(m.ArcDegrees > 0f && m.ArcDegrees <= MeleeFairness.MaxSwingArc, $"arc {m.ArcDegrees} is outside (0, {MeleeFairness.MaxSwingArc}]");
            Require(m.Windup >= 0.12f && m.Active > 0f && m.Active <= 0.2f && m.Recovery >= 0.2f, "timing needs a readable wind-up, a short active window and a recovery");
            Require(m.HitStun >= 0f && m.HitStun <= 0.45f && m.Knockback >= 0f && m.Knockback <= 10f && m.BreakPower >= 0f && m.BreakPower <= 4.5f, "impact values exceed the existing melee range");
            Require(MeleeFairness.DamagePerSecond(m) <= MeleeFairness.MaxDamagePerSecond, $"damage per second {MeleeFairness.DamagePerSecond(m):0.0} exceeds {MeleeFairness.MaxDamagePerSecond}");
            Require(MeleeFairness.Threat(m) <= MeleeFairness.MaxThreat, $"reach x arc x damage threat {MeleeFairness.Threat(m):0} exceeds {MeleeFairness.MaxThreat}");
        }
    }

    public static class MeleeFairness
    {
        public const float MinDamage = 6f;
        public const float MaxDamage = 28f;
        public const float MaxSwingReach = 2.2f;
        public const float MaxSwingArc = 145f;
        public const float MaxDamagePerSecond = 32f;
        public const float MaxThreat = 4000f;

        public static float DamagePerSecond(MeleeStats m) => m == null || m.Cycle <= 0f ? float.PositiveInfinity : m.Damage / m.Cycle;

        public static float Threat(MeleeStats m) => m == null ? float.PositiveInfinity : m.Reach * m.ArcDegrees * m.Damage;

        public static ImpactResult Classify(float angleDegrees, float arcDegrees, float distance, float reach)
        {
            if (!(reach > 0f) || distance > reach || Math.Abs(angleDegrees) > arcDegrees * 0.5f) return ImpactResult.Miss;
            return Math.Abs(angleDegrees) > arcDegrees * 0.4f ? ImpactResult.Glance : ImpactResult.Hit;
        }
    }
}
