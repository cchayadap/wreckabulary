using System;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class MatchDefenseState
    {
        public string ItemEntityId { get; }
        public long StartedTick { get; }
        public long UntilTick { get; }
        public MatchDefenseState(string itemEntityId, long startedTick, long untilTick)
        {
            if (!WordboundIdentity.IsId(itemEntityId) || !itemEntityId.StartsWith("item:", StringComparison.Ordinal) ||
                startedTick < 1 || untilTick <= startedTick || untilTick >= long.MaxValue)
                throw new ArgumentException("Invalid held-defense state.");
            ItemEntityId = itemEntityId; StartedTick = startedTick; UntilTick = untilTick;
        }
    }

    public readonly struct WordboundDefenseHit
    {
        public bool Blocked { get; }
        public float Damage { get; }
        public float BlockedDamage { get; }
        public float KnockbackScale => Blocked ? .3f : 1;
        public float VerticalKnockbackScale => Blocked ? 0 : .35f;
        internal WordboundDefenseHit(float damage, float blocked)
        { Damage = damage; BlockedDamage = blocked; Blocked = blocked > 0; }
    }

    public static class WordboundDefensePolicy
    {
        public const int HoldTicks = 30;
        public const int RefreshTicks = 10;
        public static string Hash { get; } = WordboundIdentity.Sha256(
            "wordbound.defense|1|hold30|refresh10|catalogue-raise-arc-reduction-move-wear|" +
            "blocked-impulse0.3-no-up-no-stun|projectile-side|selected-held-only|admitted-facing180");

        public static int RaiseTicks(ShieldStats stats)
        {
            if (stats == null || !WorldVector.Finite(stats.FrontArcDegrees) || stats.FrontArcDegrees <= 0 ||
                stats.FrontArcDegrees > 360 || !WorldVector.Finite(stats.DamageReduction) ||
                stats.DamageReduction <= 0 || stats.DamageReduction > 1 ||
                !WorldVector.Finite(stats.MoveSpeedMultiplier) || stats.MoveSpeedMultiplier <= 0 ||
                stats.MoveSpeedMultiplier > 1)
                throw new ArgumentException("Shield statistics exceed their supported domain.");
            return WordboundCombatPolicy.Ticks(stats.RaiseSeconds);
        }

        public static MatchDefenseState Renew(MatchDefenseState previous, string itemId, long tick)
        {
            if (tick < 1 || previous != null && previous.StartedTick > tick)
                throw new ArgumentException("Defense input must use a causal authoritative tick.");
            long start = previous != null && previous.ItemEntityId == itemId && previous.UntilTick > tick
                ? previous.StartedTick : tick;
            return new MatchDefenseState(itemId, start, checked(tick + HoldTicks));
        }

        public static bool Raised(MatchDefenseState defense, ShieldStats stats, long tick)
        {
            if (defense == null) return false;
            int raise = RaiseTicks(stats);
            return defense.StartedTick <= tick && tick < defense.UntilTick &&
                tick - defense.StartedTick >= raise;
        }

        public static float MoveScale(MatchDefenseState defense, ShieldStats stats, long tick) =>
            Raised(defense, stats, tick) ? stats.MoveSpeedMultiplier : 1;

        public static WordboundDefenseHit Resolve(MatchDefenseState defense, ShieldStats stats,
            WorldBodyState victim, WorldVector? incomingFrom, float damage, long tick)
        {
            if (!WorldVector.Finite(damage) || damage < 0 || damage > 1000)
                throw new ArgumentException("Damage exceeds the defense domain.");
            if (defense == null || incomingFrom == null || !Raised(defense, stats, tick))
                return new WordboundDefenseHit(damage, 0);
            if (victim == null || victim.Kind != WorldBodyKind.Player)
                throw new ArgumentException("Defense requires a canonical player body.");
            var q = victim.RotationXyz;
            float x = 2 * (q.X * q.Z + victim.RotationW * q.Y);
            float z = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            var from = incomingFrom.Value;
            if (!Geometry.InFrontArc(x, z, from.X - victim.Position.X, from.Z - victim.Position.Z, stats.FrontArcDegrees))
                return new WordboundDefenseHit(damage, 0);
            float remaining = damage * (1 - stats.DamageReduction);
            return new WordboundDefenseHit(remaining, damage - remaining);
        }

        public static string SnapshotError(MatchSnapshot snapshot, MatchActorState actor,
            ItemCatalogue catalogue, ModeDefinition mode)
        {
            var defense = actor.Defense;
            if (defense == null) return "";
            long tick = snapshot.Progress.Tick;
            int index = snapshot.Binding.Seats.ToList().FindIndex(s => s.PlayerId == actor.PlayerId);
            var player = snapshot.Progress.Players.Single(p => p.Id == actor.PlayerId);
            var inventory = snapshot.Economy.Players.Single(p => p.PlayerId == index);
            string selected = WordboundCombatPolicy.EquippedItemId(snapshot, actor.PlayerId);
            var item = snapshot.Economy.Items.SingleOrDefault(i => "item:" + i.Id == defense.ItemEntityId);
            if (defense.StartedTick > tick || defense.UntilTick <= tick || defense.UntilTick - tick > HoldTicks ||
                !player.Connected || player.Returned || player.Eliminated || snapshot.Progress.Migrating ||
                snapshot.Progress.Result != null || !inventory.CanAct || inventory.Craft != null ||
                inventory.CarriedFurniture >= 0 || actor.Health <= 0 || actor.StunnedUntilTick > tick ||
                actor.DodgeUntilTick > tick || actor.Attack != null || selected != defense.ItemEntityId ||
                item == null || item.State != ItemState.Held || item.HolderId != index ||
                mode.IsRace && snapshot.Progress.Teams.Single(t => t.Team == player.Team).Finished)
                return "Held defense disagrees with actor availability, timing or selected equipment.";
            RaiseTicks(catalogue.Get(item.Word).Shield);
            return "";
        }
    }
}
