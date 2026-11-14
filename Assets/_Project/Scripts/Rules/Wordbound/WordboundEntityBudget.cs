using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class WordboundEntityBudget
    {
        public const int Version = 3;
        public const int MaximumBodies = 512;
        public const int MaximumCanonicalEntities = 8192;
        public const int RetryTicks = ModeDefinition.TickRate;
        const int ExactCheckpointPuzzles = 4;
        public static string Hash { get; } = WordboundIdentity.Sha256(
            "wordbound.entity-budget|3|512|8192|retry60|equal-pools|fixed-roots|" +
            "critical-prefix4|physical-auxiliary|actor-bursts|holder-reserves|shared-ceil|stable-loose-retirement|" +
            "available-enabled-recipes|single-practice-station|retained-practice-evidence");

        readonly Dictionary<string, ResolvedCourseSupply> supplies;
        readonly Dictionary<string, string[]> consumers;
        readonly HashSet<string>[] lapPuzzles;
        readonly bool race;
        readonly bool practice;
        public int FixedBodies { get; }
        public int PoolCount { get; }
        public int PoolBodies { get; }
        public int CriticalRecoveryBodies { get; }
        public int PhysicalAuxiliaryBodies { get; }
        public int ActorAndBurstBodies { get; }
        public int OptionalBodies { get; }
        public int MaximumRetainedItems { get; }

        internal WordboundEntityBudget(ModeDefinition mode, ResolvedCourse course)
        {
            if (mode == null || course == null) throw new ArgumentNullException("Budget requires a mode and resolved course.");
            race = mode.IsRace;
            practice = WordboundPracticePolicy.Supports(mode);
            PoolCount = race ? mode.TeamCount : 1;
            FixedBodies = FixedCount(mode, course);
            PoolBodies = (MaximumBodies - FixedBodies) / PoolCount;
            supplies = course.Supplies.ToDictionary(s => s.Id, StringComparer.Ordinal);
            consumers = supplies.Values.ToDictionary(s => s.Id,
                s => course.Puzzles.Where(p => p.RecoverySupplyIds.Contains(s.SourceId)).Select(p => p.Id).ToArray(),
                StringComparer.Ordinal);
            lapPuzzles = course.Laps.Select(l => new HashSet<string>(l.Checkpoints.SelectMany(c => c.PuzzleIds),
                StringComparer.Ordinal)).ToArray();
            CriticalRecoveryBodies = race ? CriticalPeak(course) : practice ? course.PhysicalSites.Select(site =>
                supplies.Values.Where(s => consumers[s.Id].Contains(site.PuzzleId)).Sum(s => s.Letters.Length))
                .DefaultIfEmpty(0).Max() : 0;
            PhysicalAuxiliaryBodies = race || practice ? course.PhysicalSites.Count(s =>
                (practice || lapPuzzles[s.Lap - 1].Contains(s.PuzzleId)) &&
                course.Puzzles.Single(p => p.Id == s.PuzzleId).PresetId == "RedirectWind") : 0;
        }

        public WordboundEntityBudget(ModeDefinition mode, ResolvedCourse course, CourseCatalogue geometry,
            ItemCatalogue items, GameRules rules) : this(mode, course)
        {
            if (geometry == null || items == null || rules == null)
                throw new ArgumentNullException("Budget requires exact geometry, item and rule content.");
            int capacity = race ? mode.TeamCapacity : mode.MaximumPlayers;
            var enabled = items.Enabled.Where(i => mode.Recipes.Contains(i.Id)).ToArray();
            int handWord = enabled.Select(i => i.Letters.Count).DefaultIfEmpty(0).Max();
            int deployedWord = enabled.Where(i => i.CanDeploy).Select(i => i.Letters.Count).DefaultIfEmpty(0).Max();
            int propBurst = race || practice ? course.PhysicalSites.Where(s => s.PropSourceZoneId != "")
                .SelectMany(s => course.Placements.Where(p => p.ZoneId == s.PropSourceZoneId))
                .Select(p => geometry.Assets[p.AssetId].Letters.Length).DefaultIfEmpty(0).Max() : 0;
            int rewardBurst = race ? course.Laps.SelectMany(l => l.Checkpoints).Select(c =>
                c.PuzzleIds.Sum(id => course.Puzzles.Single(p => p.Id == id).RewardLetters.Length))
                .DefaultIfEmpty(0).Max() : practice ? course.PhysicalSites
                    .Select(s => course.Puzzles.Single(p => p.Id == s.PuzzleId).RewardLetters.Length).DefaultIfEmpty(0).Max() : 0;
            ActorAndBurstBodies = checked(capacity * (rules.MaxLetters +
                rules.MaxCarried * (handWord + 1) + rules.MaxDeployed * (deployedWord + 1)) +
                propBurst + rewardBurst);
            OptionalBodies = PoolBodies - CriticalRecoveryBodies - PhysicalAuxiliaryBodies - ActorAndBurstBodies;
            MaximumRetainedItems = checked(course.Placements.Count(p => p.Purpose != "Resources") +
                (race ? course.Laps.Sum(l => l.Checkpoints.Sum(c => c.PuzzleIds.Count)) * mode.TeamCount :
                    practice ? course.PhysicalSites.Count : 0) +
                MaximumBodies);
            int resources = checked((course.Supplies.Count + course.Placements.Count(p => p.Purpose == "Resources")) *
                (race ? 3 : 1));
            int mechanisms = race ? checked(course.Puzzles.Count * mode.TeamCount * 3) : practice ? course.PhysicalSites.Count : 0;
            int optionalBatch = supplies.Values.Where(s => !race && !practice || consumers[s.Id].Length == 0).Select(s => s.Letters.Length)
                .Concat(course.Placements.Where(p => p.Purpose == "Resources").Select(p => p.Letters.Length))
                .DefaultIfEmpty(0).Max();
            if (FixedBodies >= MaximumBodies || OptionalBodies < Math.Max(capacity, optionalBatch) ||
                MaximumRetainedItems > MaximumCanonicalEntities || resources > MaximumCanonicalEntities ||
                mechanisms > MaximumCanonicalEntities)
                throw new ArgumentException("Course and rules exceed the 512-body budget or 8192-state budget; " +
                    "reduce authored roots, concurrent recovery, inventory limits or physical fixtures.");
        }

        int CriticalPeak(ResolvedCourse course)
        {
            int maximum = 0;
            foreach (var lap in course.Laps)
            {
                var complete = new HashSet<string>(StringComparer.Ordinal);
                foreach (var checkpoint in lap.Checkpoints)
                {
                    bool exact = checkpoint.PuzzleIds.Count <= ExactCheckpointPuzzles;
                    int variants = exact ? 1 << checkpoint.PuzzleIds.Count : 1;
                    for (int mask = 0; mask < variants; mask++)
                    {
                        var possible = new HashSet<string>(complete, StringComparer.Ordinal);
                        for (int p = 0; p < checkpoint.PuzzleIds.Count; p++)
                            if (!exact || (mask & (1 << p)) != 0) possible.Add(checkpoint.PuzzleIds[p]);
                        var consumed = exact ? possible : complete;
                        foreach (var team in supplies.Values.Select(s => s.TeamIndex).Distinct())
                        {
                            int count = supplies.Values.Where(s => s.TeamIndex == team &&
                                s.AfterPuzzleIds.All(possible.Contains) && consumers[s.Id].Any(id =>
                                    lapPuzzles[lap.Number - 1].Contains(id) && !consumed.Contains(id)))
                                .Sum(s => s.Letters.Length);
                            maximum = Math.Max(maximum, count);
                        }
                    }
                    complete.UnionWith(checkpoint.PuzzleIds);
                }
            }
            return maximum;
        }

        internal bool CriticalSource(string sourceId, TeamProgress team)
        {
            return race && team != null && !team.Finished && supplies.TryGetValue(sourceId, out var source) &&
                source.TeamIndex == team.Team && source.AfterPuzzleIds.All(team.HasPuzzle) &&
                consumers[sourceId].Any(id => lapPuzzles[team.Lap].Contains(id) && !team.HasPuzzle(id));
        }

        internal int Pool(int ownerTeam) => race ? ownerTeam : 0;

        internal static HashSet<string> AuxiliaryBodies(ResolvedCourse course, ModeDefinition mode,
            IEnumerable<WorldBodyState> sourceBodies, Func<int, ItemInstance> item,
            IEnumerable<MatchMechanismState> sourceMechanisms, IEnumerable<TeamProgress> teams, MatchPracticeState practice = null)
        {
            var mechanisms = sourceMechanisms.ToArray();
            var bodies = sourceBodies.ToArray();
            var result = new HashSet<string>(mechanisms.Where(m => m.Physical.AuxiliaryBodyId != "")
                .Select(m => m.Physical.AuxiliaryBodyId), StringComparer.Ordinal);
            if (WordboundPracticePolicy.Supports(mode))
            {
                var site = WordboundPracticePolicy.Selected(course, practice);
                if (site != null && site.WindReceiverRecipeId != "" && !mechanisms.Any(m =>
                    m.PuzzleId == site.PuzzleId && m.Lap == site.Lap - 1 && m.Solved))
                {
                    var candidate = bodies.Where(b => b.Kind == WorldBodyKind.Item && b.OwnerTeam == -1 &&
                        !result.Contains(b.Id) && ReceiverCandidate(site, b, item(int.Parse(b.Id.Substring(5)))))
                        .OrderBy(b => int.Parse(b.Id.Substring(5))).FirstOrDefault();
                    if (candidate != null) result.Add(candidate.Id);
                }
                return result;
            }
            if (!mode.IsRace) return result;
            foreach (var team in teams)
            {
                if (team.Finished || team.NextCheckpoint >= course.Laps[team.Lap].Checkpoints.Count) continue;
                foreach (var site in course.PhysicalSites.Where(s => s.Lap == team.Lap + 1 &&
                    s.CheckpointId == course.Laps[team.Lap].Checkpoints[team.NextCheckpoint].Id &&
                    s.WindReceiverRecipeId != "" && !team.HasPuzzle(s.PuzzleId)))
                {
                    var state = mechanisms.Single(m => m.PuzzleId == site.PuzzleId && m.Team == team.Team && m.Lap == team.Lap);
                    if (state.Physical.AuxiliaryBodyId != "") continue;
                    var candidate = bodies.Where(b => b.Kind == WorldBodyKind.Item && b.OwnerTeam == team.Team &&
                        !result.Contains(b.Id) && ReceiverCandidate(site, b, item(int.Parse(b.Id.Substring(5)))))
                        .OrderBy(b => int.Parse(b.Id.Substring(5))).FirstOrDefault();
                    if (candidate != null) result.Add(candidate.Id);
                }
            }
            return result;
        }

        internal static bool ReceiverCandidate(ResolvedCoursePhysicalSite site, WorldBodyState body, ItemInstance item) =>
            item != null && item.Origin == ItemOrigin.Crafted && item.Word == site.WindReceiverRecipeId &&
            item.State == ItemState.World && body.Active &&
            CourseSensorBoundary.ContainsFootPoint(site.ReceiverStartBounds, body.Position);

        internal static int FixedCount(ModeDefinition mode, ResolvedCourse course) =>
            checked(mode.MaximumPlayers + (WordboundPracticePolicy.HasKeepsake(mode) ? mode.TeamCount : 0) +
                course.Placements.Count(p => p.Purpose != "Resources") +
                course.Connections.Count(c => c.GatePuzzleId != "") +
                (mode.IsRace || WordboundPracticePolicy.Supports(mode) ? course.PhysicalSites.Sum(s => s.Fixtures.Count) * mode.TeamCount : 0));

        internal static bool FixedBody(WorldBodyState body, ItemInstance item) =>
            body.Kind != WorldBodyKind.Item && body.Kind != WorldBodyKind.Tile ||
            body.Kind == WorldBodyKind.Item && item?.Origin == ItemOrigin.Map;

        internal static int Usage(IEnumerable<WorldBodyState> dynamicBodies, int team, int poolCount,
            Func<WorldBodyState, int> owner = null)
        {
            int own = 0, shared = 0;
            foreach (var body in dynamicBodies)
            {
                int actualOwner = owner == null ? body.OwnerTeam : owner(body);
                if (poolCount == 1 || actualOwner == team) own++;
                else if (actualOwner < 0) shared++;
            }
            return own + (shared + poolCount - 1) / poolCount;
        }
    }
}
