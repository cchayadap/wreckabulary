using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed partial class AuthoritativeMatch
    {
        readonly Dictionary<string, MatchThrownFlight> thrownFlights = new Dictionary<string, MatchThrownFlight>(StringComparer.Ordinal);
        readonly HashSet<ProgressEvent> thrownKnockouts = new HashSet<ProgressEvent>();

        void ApplyThrownContacts(IEnumerable<HostThrownContact> contacts, long tick, long wallClockMs)
        {
            foreach (var contact in contacts.OrderBy(c => c.ItemBodyId, StringComparer.Ordinal)
                .ThenBy(c => c.TargetBodyId, StringComparer.Ordinal).ThenByDescending(c => c.RelativeSpeed))
            {
                if (contact.Tick != tick) throw new ArgumentException("Thrown contact does not belong to this completed host step.");
                if (!thrownFlights.TryGetValue(contact.ItemBodyId, out var flight) || flight.LaunchRevision != contact.LaunchRevision ||
                    tick <= flight.StartedTick || tick >= flight.StartedTick + WordboundThrownPolicy.LifetimeTicks ||
                    flight.HitBodyIds.Contains(contact.TargetBodyId) || contact.RelativeSpeed < 1 ||
                    !bodies.TryGetValue(contact.TargetBodyId, out var target) || !target.Active ||
                    target.Kind != WorldBodyKind.Player && target.Kind != WorldBodyKind.Item ||
                    target.Id == "player:" + indices[flight.ThrowerId] ||
                    !FlightOwnerAvailable(progress.Player(flight.ThrowerId), wallClockMs)) continue;
                var item = economy.Item(Number(flight.ItemBodyId));
                var projectile = bodies[flight.ItemBodyId];
                if (item == null || item.State != ItemState.Thrown || !projectile.Active || projectile.Kinematic) continue;
                var stats = WordboundThrownPolicy.Stats(catalogue.Get(item.Word));
                if (!ContactNear(projectile, contact.ContactPoint) || !ContactNear(target, contact.ContactPoint)) continue;
                float amount = target.Kind == WorldBodyKind.Item ? stats.BreakPower : stats.Damage;
                if (amount <= 0 || !ApplyDamage(new HostDamage(flight.ThrowerId, target.Id, amount,
                    WordboundCombatPolicy.Ticks(Math.Min(.2f, rules.HitStunMax))), out var impact, true, projectile.Position)) continue;
                thrownFlights[flight.ItemBodyId] = new MatchThrownFlight(flight.ItemBodyId, flight.LaunchRevision,
                    flight.StartedTick, flight.ThrowerId, flight.HitBodyIds.Concat(new[] { target.Id }));
                var current = bodies[target.Id];
                if (!current.Active || current.Kinematic || stats.Knockback <= 0) continue;
                float x = target.Position.X - projectile.Position.X, z = target.Position.Z - projectile.Position.Z;
                float length = (float)Math.Sqrt(x * x + z * z);
                var forward = Forward(projectile, 1);
                float speed = stats.Knockback * 2 * impact.KnockbackScale;
                var velocity = Add(current.Velocity, new WorldVector((length > .0001f ? x / length : forward.X) * speed,
                    speed * impact.VerticalKnockbackScale, (length > .0001f ? z / length : forward.Z) * speed));
                bodies[target.Id] = current.At(current.Position, new WorldVector(
                    Math.Max(-1000, Math.Min(1000, velocity.X)), Math.Max(-1000, Math.Min(1000, velocity.Y)),
                    Math.Max(-1000, Math.Min(1000, velocity.Z))));
            }
        }

        bool ContactNear(WorldBodyState body, WorldVector point)
        {
            double radius = 2.5;
            if (body.Kind == WorldBodyKind.Item)
            {
                var size = catalogue.Get(economy.Item(Number(body.Id)).Word).Size;
                if (size == null || size.Length != 3 || size.Any(v => !WorldVector.Finite(v) || v <= 0 || v > 10)) return false;
                radius = Math.Sqrt(size.Sum(v => (double)v * v)) + 1;
            }
            return body.Position.DistanceSquared(point) <= radius * radius;
        }

        static bool FlightOwnerAvailable(PlayerProgress player, long wallClockMs) =>
            !player.Returned && (player.Connected || player.ReconnectDeadlineMs > wallClockMs);

        void SettleThrows(long tick, long? wallClockMs = null)
        {
            foreach (var item in economy.Items.Where(i => i.State == ItemState.Thrown).ToArray())
            {
                string id = "item:" + item.Id;
                if (bodies[id].Sleeping || thrownFlights.TryGetValue(id, out var flight) &&
                    (tick >= flight.StartedTick + WordboundThrownPolicy.LifetimeTicks ||
                     !FlightOwnerAvailable(progress.Player(flight.ThrowerId), wallClockMs ?? progress.WallClockMs)))
                    economy.Settle(item.Id);
            }
            foreach (var flight in thrownFlights.Values.ToArray())
            {
                var item = economy.Item(Number(flight.ItemBodyId));
                if (item == null || item.State != ItemState.Thrown || !bodies[flight.ItemBodyId].Active)
                    thrownFlights.Remove(flight.ItemBodyId);
            }
        }
    }

    internal static class MatchThrownValidation
    {
        internal static void Validate(MatchSnapshot snapshot, ModeDefinition mode, ItemCatalogue catalogue)
        {
            void Need(bool ok, string error) { if (!ok) throw new ArgumentException(error); }
            var flights = snapshot.ThrownFlights;
            Need(flights.Count <= 8192 && flights.Select(f => f.ItemBodyId).Distinct(StringComparer.Ordinal).Count() == flights.Count,
                "Thrown flight identities are duplicated or exceed bounds.");
            var bodies = snapshot.Bodies.ToDictionary(b => b.Id, StringComparer.Ordinal);
            var expected = snapshot.Economy.Items.Where(i => i.State == ItemState.Thrown &&
                WordboundThrownPolicy.Supported(catalogue.Get(i.Word))).Select(i => "item:" + i.Id);
            Need(new HashSet<string>(expected).SetEquals(flights.Select(f => f.ItemBodyId)),
                "Every supported thrown item requires exactly one canonical flight.");
            foreach (var flight in flights)
            {
                var item = snapshot.Economy.Items.Single(i => "item:" + i.Id == flight.ItemBodyId);
                WordboundThrownPolicy.Stats(catalogue.Get(item.Word));
                int index = snapshot.Binding.Seats.ToList().FindIndex(s => s.PlayerId == flight.ThrowerId);
                Need(index >= 0 && item.ThrowerId == index && item.Revision >= flight.LaunchRevision &&
                    flight.StartedTick <= snapshot.Progress.Tick &&
                    (flight.HitBodyIds.Count == 0 || flight.StartedTick < snapshot.Progress.Tick) &&
                    snapshot.Progress.Tick < checked(flight.StartedTick + WordboundThrownPolicy.LifetimeTicks) &&
                    snapshot.Progress.Players.Any(p => p.Id == flight.ThrowerId && !p.Returned &&
                        (p.Connected || p.ReconnectDeadlineMs > snapshot.Progress.WallClockMs)) &&
                    bodies.TryGetValue(flight.ItemBodyId, out var body) && body.Active && !body.Kinematic && !body.Sleeping,
                    "Thrown flight owner, revision, lifetime or physical state is invalid.");
                int team = snapshot.Binding.Seats[index].Team;
                foreach (string id in flight.HitBodyIds)
                    Need(id != flight.ItemBodyId && id != "player:" + index && bodies.TryGetValue(id, out var hit) &&
                        (hit.Kind == WorldBodyKind.Player && mode.CanDamage(team, hit.OwnerTeam) ||
                         hit.Kind == WorldBodyKind.Item && mode.CanInteract(team, hit.OwnerTeam, hit.Protected)),
                        "Thrown hit ledger contains an impossible target.");
            }
        }
    }
}
