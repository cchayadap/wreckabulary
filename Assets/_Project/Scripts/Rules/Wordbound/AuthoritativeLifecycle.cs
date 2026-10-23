using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    internal sealed class WordboundResourceLifecycle
    {
        readonly bool race;
        readonly bool practiceMode;
        readonly ResolvedCourse course;
        readonly Dictionary<string, string[]> consumers;
        readonly HashSet<string>[] selected;
        internal WordboundResourceLifecycle(ResolvedCourse course, ModeDefinition mode)
        {
            race = mode.IsRace;
            practiceMode = WordboundPracticePolicy.Supports(mode);
            this.course = course;
            consumers = course.Supplies.ToDictionary(s => s.Id,
                s => course.Puzzles.Where(p => p.RecoverySupplyIds.Contains(s.SourceId)).Select(p => p.Id).ToArray(),
                StringComparer.Ordinal);
            selected = course.Laps.Select(l => new HashSet<string>(l.Checkpoints.SelectMany(c => c.PuzzleIds),
                StringComparer.Ordinal)).ToArray();
        }

        internal bool PracticeSource(string sourceId, MatchPracticeState practice, IEnumerable<MatchMechanismState> mechanisms)
        {
            var site = WordboundPracticePolicy.Selected(course, practice);
            return practiceMode && site != null && consumers.TryGetValue(sourceId, out var ids) && ids.Contains(site.PuzzleId) &&
                mechanisms.Any(m => m.Team == 0 && m.Lap == site.Lap - 1 && m.PuzzleId == site.PuzzleId && !m.Solved);
        }

        internal bool Demanded(TeamProgress team, MatchResourceState resource, MatchPracticeState practice = null,
            IEnumerable<MatchMechanismState> mechanisms = null)
        {
            if (practiceMode && consumers.TryGetValue(resource.SourceId, out var ids) && ids.Length != 0)
                return PracticeSource(resource.SourceId, practice, mechanisms ?? Array.Empty<MatchMechanismState>());
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
            Dictionary<(string Source, int Team, int Generation), List<WorldBodyState>> tiles = null;
            foreach (var resource in resources.Values.ToArray())
            {
                if (resourceLifecycle.Demanded(Mode.IsRace ? teams[resource.Team] : null, resource, practice, mechanisms.Values)) continue;
                if (tiles == null)
                {
                    tiles = new Dictionary<(string Source, int Team, int Generation), List<WorldBodyState>>();
                    foreach (var body in bodies.Values)
                    {
                        if (body.Kind != WorldBodyKind.Tile) continue;
                        var identity = (body.SourceId, body.OwnerTeam, body.RecoveryGeneration);
                        if (!tiles.TryGetValue(identity, out var group))
                            tiles.Add(identity, group = new List<WorldBodyState>());
                        group.Add(body);
                    }
                }
                var key = (resource.SourceId, resource.Team, resource.Generation);
                if (tiles.TryGetValue(key, out var retired))
                {
                    tiles.Remove(key);
                    foreach (var body in retired)
                    {
                        economy.RetireWordboundTile(Number(body.Id));
                        bodies.Remove(body.Id);
                    }
                }
                if (resource.RecoveryAtTick >= 0)
                    resources[Key(resource.SourceId, resource.Team, resource.Lap)] =
                        new MatchResourceState(resource.SourceId, resource.Team, resource.Lap, resource.Generation);
            }
        }

        void CompactDestroyedEntities()
        {
            var bodyReferences = ReferencedBodies();
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

        HashSet<string> ReferencedBodies()
        {
            var references = new HashSet<string>(StringComparer.Ordinal);
            foreach (var actor in actors.Values)
                if (actor.Attack != null) references.UnionWith(actor.Attack.HitBodyIds);
            foreach (var flight in thrownFlights.Values)
            {
                references.Add(flight.ItemBodyId);
                references.UnionWith(flight.HitBodyIds);
            }
            foreach (var mechanism in mechanisms.Values)
            {
                if (mechanism.Physical.AuxiliaryBodyId != "") references.Add(mechanism.Physical.AuxiliaryBodyId);
                if (!mechanism.Solved && mechanism.ItemEntityId != "") references.Add(mechanism.ItemEntityId);
            }
            return references;
        }

        bool FixedBody(WorldBodyState body) => WordboundEntityBudget.FixedBody(body,
            body.Kind == WorldBodyKind.Item ? economy.Item(Number(body.Id)) : null);

        bool CriticalBody(WorldBodyState body) => body.Kind == WorldBodyKind.Tile &&
            (PracticeRecoverySource(body.SourceId) || Mode.IsRace &&
                body.OwnerTeam >= 0 && entityBudget.CriticalSource(body.SourceId, progress.Team(body.OwnerTeam)));

        int BudgetOwner(WorldBodyState body)
        {
            if (!Mode.IsRace || body.Kind != WorldBodyKind.Item) return body.OwnerTeam;
            var item = economy.Item(Number(body.Id));
            int player = item.State == ItemState.Held ? item.HolderId :
                item.State == ItemState.Deployed || item.State == ItemState.Armed ? item.DeployerId : -1;
            return player >= 0 ? Binding.Seats[player].Team : body.OwnerTeam;
        }

        HashSet<string> AuxiliaryBodies() => WordboundEntityBudget.AuxiliaryBodies(course, Mode,
            bodies.Values, economy.Item, mechanisms.Values, progress.Capture().Teams, practice);

        bool ReceiverCandidate(ResolvedCoursePhysicalSite site, WorldBodyState body, ItemInstance item) =>
            WordboundEntityBudget.ReceiverCandidate(site, body, item);

        bool OptionalBody(WorldBodyState body, HashSet<string> auxiliaries)
        {
            if (FixedBody(body) || CriticalBody(body) || auxiliaries.Contains(body.Id)) return false;
            if (body.Kind == WorldBodyKind.Tile) return true;
            var item = economy.Item(Number(body.Id));
            return item.State != ItemState.Held && item.State != ItemState.Carried &&
                item.State != ItemState.Deployed && item.State != ItemState.Armed;
        }

        int ReservedPoolUsage(int team)
        {
            var auxiliaries = AuxiliaryBodies();
            return WordboundEntityBudget.Usage(bodies.Values.Where(b => !FixedBody(b) && !CriticalBody(b) &&
                !auxiliaries.Contains(b.Id)), team, entityBudget.PoolCount, BudgetOwner);
        }

        bool AdmitCraftedBody(int player)
        {
            int pool = Mode.IsRace ? Binding.Seats[player].Team : 0;
            var recyclable = new HashSet<string>(RecyclableBodies(-1).Select(b => b.Id), StringComparer.Ordinal);
            var auxiliaries = AuxiliaryBodies();
            int pinned = WordboundEntityBudget.Usage(bodies.Values.Where(b => !FixedBody(b) &&
                !CriticalBody(b) && !auxiliaries.Contains(b.Id) && !recyclable.Contains(b.Id)), pool, entityBudget.PoolCount, BudgetOwner);
            int pendingCrafts = Binding.Seats.Select((s, i) => (s, i)).Count(p =>
                (!Mode.IsRace || p.s.Team == pool) && economy.Player(p.i).Craft != null);
            return pinned + pendingCrafts + 1 <= entityBudget.PoolBodies -
                entityBudget.CriticalRecoveryBodies - entityBudget.PhysicalAuxiliaryBodies;
        }

        bool ReserveOptional(int owner, int count, bool recycle)
        {
            int[] pools = owner < 0 ? Enumerable.Range(0, entityBudget.PoolCount).ToArray() :
                new[] { entityBudget.Pool(owner) };
            int added = owner < 0 ? (count + entityBudget.PoolCount - 1) / entityBudget.PoolCount : count;
            var auxiliaries = AuxiliaryBodies();
            var optional = bodies.Values.Where(b => OptionalBody(b, auxiliaries)).ToArray();
            var excluded = new HashSet<string>(StringComparer.Ordinal);
            bool Fits() => pools.All(p => WordboundEntityBudget.Usage(optional.Where(b => !excluded.Contains(b.Id)),
                p, entityBudget.PoolCount) + added <= entityBudget.OptionalBodies);
            if (Fits()) return true;
            if (!recycle) return false;
            var candidates = RecyclableBodies(owner).ToArray();
            var selected = new List<WorldBodyState>();
            foreach (var body in candidates)
            {
                selected.Add(body);
                excluded.Add(body.Id);
                if (!Fits()) continue;
                foreach (var candidate in selected) RetireOptional(candidate);
                return true;
            }
            return false;
        }

        IEnumerable<WorldBodyState> RecyclableBodies(int owner)
        {
            var references = ReferencedBodies();
            var auxiliaries = AuxiliaryBodies();
            return bodies.Values.Where(b => (owner < 0 || b.OwnerTeam < 0 || b.OwnerTeam == owner) &&
                OptionalBody(b, auxiliaries) && !references.Contains(b.Id) &&
                (b.Kind == WorldBodyKind.Tile || economy.Item(Number(b.Id)).State == ItemState.World))
                .OrderBy(b => b.Kind == WorldBodyKind.Tile ? 0 : 1).ThenBy(b => Number(b.Id));
        }

        void RetireOptional(WorldBodyState body)
        {
            if (body.Kind == WorldBodyKind.Tile)
            {
                economy.RetireWordboundTile(Number(body.Id));
                ScheduleSource(body);
            }
            else economy.RetireWordboundLooseItem(Number(body.Id));
            bodies.Remove(body.Id);
        }

        bool AdmitRelease(int index, int slot, WorldVector at, bool throwing)
        {
            var item = slot < 0 ? economy.Item(economy.Player(index).CarriedFurniture) : Held(index, slot);
            if (item == null || item.Origin == ItemOrigin.Map) return true;
            var body = bodies["item:" + item.Id];
            if (!throwing && IsPractice)
            {
                var site = PracticeSite;
                if (site != null && PracticeActive(site.PuzzleId, site.Lap - 1) && site.WindReceiverRecipeId == item.Word &&
                    mechanisms[Key(site.PuzzleId, 0, site.Lap - 1)].Physical.AuxiliaryBodyId == "")
                {
                    var copy = EconomySnapshot.Copy(item);
                    copy.State = ItemState.World;
                    var auxiliaries = AuxiliaryBodies();
                    if (!bodies.Values.Any(b => auxiliaries.Contains(b.Id) && b.Kind == WorldBodyKind.Item &&
                        ReceiverCandidate(site, b, economy.Item(Number(b.Id)))) &&
                        ReceiverCandidate(site, body.At(at, active: true), copy)) return true;
                }
            }
            if (!throwing && Mode.IsRace)
            {
                var team = progress.Team(body.OwnerTeam);
                if (!team.Finished && team.NextCheckpoint < course.Laps[team.Lap].Checkpoints.Count)
                {
                    var copy = EconomySnapshot.Copy(item);
                    copy.State = ItemState.World;
                    var auxiliaries = AuxiliaryBodies();
                    foreach (var site in course.PhysicalSites.Where(s => s.Lap == team.Lap + 1 &&
                        s.CheckpointId == course.Laps[team.Lap].Checkpoints[team.NextCheckpoint].Id &&
                        s.WindReceiverRecipeId == item.Word && !team.HasPuzzle(s.PuzzleId)))
                        if (mechanisms[Key(site.PuzzleId, team.Team, team.Lap)].Physical.AuxiliaryBodyId == "" &&
                            !bodies.Values.Any(b => auxiliaries.Contains(b.Id) &&
                                b.OwnerTeam == team.Team && b.Kind == WorldBodyKind.Item &&
                                ReceiverCandidate(site, b, economy.Item(Number(b.Id)))) &&
                            ReceiverCandidate(site, body.At(at, active: true), copy)) return true;
                }
            }
            return ReserveOptional(body.OwnerTeam, 1, true);
        }

        void EnforceBodyBudget()
        {
            CompactDestroyedEntities();
            for (int team = 0; team < entityBudget.PoolCount; team++)
            {
                int capacity = entityBudget.PoolBodies - entityBudget.CriticalRecoveryBodies - entityBudget.PhysicalAuxiliaryBodies;
                if (ReservedPoolUsage(team) <= capacity) continue;
                foreach (var body in RecyclableBodies(Mode.IsRace ? team : -1).ToArray())
                {
                    RetireOptional(body);
                    if (ReservedPoolUsage(team) <= capacity) break;
                }
                if (ReservedPoolUsage(team) > capacity)
                    throw new InvalidOperationException("Pinned entities exceed their reserved team body budget.");
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

        internal void RetireWordboundLooseItem(int itemId)
        {
            if (!items.TryGetValue(itemId, out var item) || item.Origin != ItemOrigin.Crafted ||
                item.State != ItemState.World || item.HolderId >= 0)
                throw new InvalidOperationException("Only unheld loose crafted equipment can be recycled.");
            int nextSpent = checked(Spent + item.LockedLetterCount);
            items.Remove(itemId);
            Spent = nextSpent;
        }
    }
}
