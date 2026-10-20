using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    internal sealed class WordboundResourceLifecycle
    {
        readonly bool race;
        readonly Dictionary<string, string[]> consumers;
        readonly HashSet<string>[] selected;
        internal WordboundResourceLifecycle(ResolvedCourse course, ModeDefinition mode)
        {
            race = mode.IsRace;
            consumers = course.Supplies.ToDictionary(s => s.Id,
                s => course.Puzzles.Where(p => p.RecoverySupplyIds.Contains(s.SourceId)).Select(p => p.Id).ToArray(),
                StringComparer.Ordinal);
            selected = course.Laps.Select(l => new HashSet<string>(l.Checkpoints.SelectMany(c => c.PuzzleIds),
                StringComparer.Ordinal)).ToArray();
        }

        internal bool Demanded(TeamProgress team, MatchResourceState resource)
        {
            if (!race) return true;
            if (team.Finished || resource.Lap != team.Lap) return false;
            return !consumers.TryGetValue(resource.SourceId, out var puzzles) || puzzles.Length == 0 ||
                puzzles.Any(id => selected[team.Lap].Contains(id) && !team.HasPuzzle(id));
        }
    }

    public sealed partial class AuthoritativeMatch
    {
        void RetireInactiveResources()
        {
            var teams = progress.Capture().Teams.ToDictionary(t => t.Team);
            foreach (var resource in resources.Values.ToArray())
            {
                if (resourceLifecycle.Demanded(Mode.IsRace ? teams[resource.Team] : null, resource)) continue;
                foreach (var body in bodies.Values.Where(b => b.Kind == WorldBodyKind.Tile &&
                    b.SourceId == resource.SourceId && b.OwnerTeam == resource.Team &&
                    b.RecoveryGeneration == resource.Generation).ToArray())
                {
                    economy.RetireWordboundTile(Number(body.Id));
                    bodies.Remove(body.Id);
                }
                if (resource.RecoveryAtTick >= 0)
                    resources[Key(resource.SourceId, resource.Team, resource.Lap)] =
                        new MatchResourceState(resource.SourceId, resource.Team, resource.Lap, resource.Generation);
            }
        }

        void CompactDestroyedEntities()
        {
            var bodyReferences = new HashSet<string>(StringComparer.Ordinal);
            foreach (var actor in actors.Values)
                if (actor.Attack != null) bodyReferences.UnionWith(actor.Attack.HitBodyIds);
            foreach (var flight in thrownFlights.Values)
            {
                bodyReferences.Add(flight.ItemBodyId);
                bodyReferences.UnionWith(flight.HitBodyIds);
            }
            foreach (var mechanism in mechanisms.Values)
                if (mechanism.Physical.AuxiliaryBodyId != "") bodyReferences.Add(mechanism.Physical.AuxiliaryBodyId);
            var itemReferences = new HashSet<string>(mechanisms.Values.Where(m => m.ItemEntityId != "")
                .Select(m => m.ItemEntityId), StringComparer.Ordinal);
            foreach (var item in economy.Items.Where(i => i.IsGone && i.Origin == ItemOrigin.Crafted).ToArray())
            {
                string id = "item:" + item.Id;
                if (bodyReferences.Contains(id)) continue;
                bodies.Remove(id);
                if (!itemReferences.Contains(id)) economy.ForgetWordboundDestroyedItem(item.Id);
            }
        }
    }
}

namespace Wreckabulary.Rules
{
    public sealed partial class Economy
    {
        internal void RetireWordboundTile(int tileId)
        {
            if (!tiles.ContainsKey(tileId)) throw new InvalidOperationException("Retired letter has no economy identity.");
            int nextSpent = checked(Spent + 1);
            tiles.Remove(tileId);
            Spent = nextSpent;
        }

        internal void ForgetWordboundDestroyedItem(int itemId)
        {
            if (!items.TryGetValue(itemId, out var item) || !item.IsGone || item.Origin != ItemOrigin.Crafted ||
                players.Values.Any(p => p.Slots.Contains(itemId) || p.CarriedFurniture == itemId))
                throw new InvalidOperationException("Only an unheld destroyed crafted item can be forgotten.");
            items.Remove(itemId);
        }
    }
}
