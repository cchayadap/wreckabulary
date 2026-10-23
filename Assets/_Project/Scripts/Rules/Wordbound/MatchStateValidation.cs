using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Wreckabulary.Rules.Wordbound
{
    public static class MatchStateValidation
    {
        static readonly ConditionalWeakTable<ResolvedCourse, CourseCache> CourseCaches =
            new ConditionalWeakTable<ResolvedCourse, CourseCache>();
        static readonly ConditionalWeakTable<ResolvedCourse, CourseCache>.CreateValueCallback CreateCourseCache =
            course => new CourseCache(course);

        sealed class CourseCache
        {
            readonly ConditionalWeakTable<ModeDefinition, StaticContent> modes =
                new ConditionalWeakTable<ModeDefinition, StaticContent>();
            readonly ConditionalWeakTable<ModeDefinition, StaticContent>.CreateValueCallback create;
            public CourseCache(ResolvedCourse course) { create = mode => new StaticContent(course, mode); }
            public StaticContent For(ModeDefinition mode) => modes.GetValue(mode, create);
        }

        sealed class StaticContent
        {
            public readonly RaceLap[] Route;
            public readonly Dictionary<string, Tuple<int, bool, string>> Sources;
            public readonly Dictionary<string, ResolvedCoursePlacement> Placements;
            public readonly WordboundEntityBudget Budget;
            public readonly WordboundResourceLifecycle Lifecycle;
            public readonly Dictionary<(int Lap, string PuzzleId), ResolvedCoursePhysicalSite> Sites;
            public readonly FixtureContent[] Fixtures;
            public readonly Dictionary<string, FixtureContent> FixturesById;
            public readonly Dictionary<string, ResolvedCoursePuzzle> Puzzles;
            public readonly Dictionary<string, ResolvedCourseModule> Geometry;
            public readonly HashSet<string> ConnectionPlatforms;

            public StaticContent(ResolvedCourse course, ModeDefinition mode)
            {
                Route = MatchStateValidation.Route(course);
                Sources = MatchStateValidation.Sources(course);
                Placements = course.Placements.ToDictionary(p => p.Id, StringComparer.Ordinal);
                Budget = new WordboundEntityBudget(mode, course);
                Lifecycle = new WordboundResourceLifecycle(course, mode);
                Sites = course.PhysicalSites.ToDictionary(site => (site.Lap, site.PuzzleId));
                Fixtures = course.PhysicalSites.SelectMany(site => site.Fixtures.Select(fixture =>
                    new FixtureContent(site, fixture))).ToArray();
                FixturesById = Fixtures.ToDictionary(value => value.Fixture.Id, StringComparer.Ordinal);
                Puzzles = course.Puzzles.ToDictionary(puzzle => puzzle.Id, StringComparer.Ordinal);
                Geometry = course.Geometry.ToDictionary(module => module.Id, StringComparer.Ordinal);
                ConnectionPlatforms = new HashSet<string>(course.Connections.Select(connection => "platform:" + connection.Id), StringComparer.Ordinal);
            }

            public ResolvedCoursePhysicalSite Site(int lap, string puzzleId) =>
                Sites.TryGetValue((lap, puzzleId), out var site) ? site : null;
        }

        sealed class FixtureContent
        {
            public readonly ResolvedCoursePhysicalSite Site;
            public readonly ResolvedCourseMovingFixture Fixture;
            public FixtureContent(ResolvedCoursePhysicalSite site, ResolvedCourseMovingFixture fixture)
            { Site = site; Fixture = fixture; }
        }

        public static string Validate(MatchSnapshot snapshot, MatchBinding expectedBinding, ModeDefinition mode,
            ResolvedCourse course, ItemCatalogue catalogue, GameRules rules)
        {
#if UNITY_EDITOR || GSK_QA
            using var phase = WordboundPhaseCapture.Measure(WordboundPhase.FullValidation);
#endif
            try
            {
                Need(snapshot != null && expectedBinding != null && mode != null && course != null && catalogue != null && rules != null,
                    "Snapshot and all trusted match dependencies are required.");
                Need(snapshot.Binding.Hash == expectedBinding.Hash && expectedBinding.ModeHash == mode.Hash &&
                    expectedBinding.CourseHash == course.SourceHash && expectedBinding.ManifestHash == course.Hash,
                    "Snapshot binding differs from the expected match or content.");
                Need(mode.ValidateRoster(expectedBinding.Seats).Count == 0, "Snapshot roster violates the mode.");
                var expectedPlay = Requirements(mode);
                Need(course.Play.ModeId == expectedPlay.ModeId && course.Play.Capability == expectedPlay.Capability &&
                    course.Play.TeamCount == expectedPlay.TeamCount && course.Play.TeamCapacity == expectedPlay.TeamCapacity &&
                    course.Play.MaxOccupants == expectedPlay.MaxOccupants && course.Play.Ownership == expectedPlay.Ownership,
                    "Resolved course uses a different mode or format.");
                Need(snapshot.Epoch >= 0 && snapshot.Epoch < long.MaxValue && snapshot.Sequence > 0 && snapshot.Sequence < long.MaxValue &&
                    snapshot.NextWorldEntityId > 0 && snapshot.NextWorldEntityId < long.MaxValue,
                    "Snapshot allocation, sequence or epoch exceeds bounds.");
                var content = CourseCaches.GetValue(course, CreateCourseCache).For(mode);
#if UNITY_EDITOR || GSK_QA
                using (WordboundPhaseCapture.Measure(WordboundPhase.ProgressRestore))
#endif
                {
                    var progress = new MatchProgress(mode, expectedBinding.Seats, content.Route);
                    progress.Restore(snapshot.Progress);
                }
                ValidateEconomy(snapshot, expectedBinding, catalogue, rules);
                var commands = new CommandJournal(expectedBinding.Hash, expectedBinding.Seats.Select(s => s.PlayerId));
                commands.Restore(snapshot.Commands);
                Need(snapshot.Commands.Epoch == snapshot.Epoch && snapshot.Commands.Receipts.All(r => r.Tick <= snapshot.Progress.Tick),
                    "Command journal epoch or receipt tick disagrees with the snapshot.");
                Need(snapshot.Economy.ItemStates.Length <= WordboundEntityBudget.MaximumCanonicalEntities &&
                    snapshot.Economy.TileStates.Length <= WordboundEntityBudget.MaximumCanonicalEntities &&
                    snapshot.Bodies.Count <= WordboundEntityBudget.MaximumBodies &&
                    snapshot.Resources.Count <= WordboundEntityBudget.MaximumCanonicalEntities &&
                    snapshot.Mechanisms.Count <= WordboundEntityBudget.MaximumCanonicalEntities &&
                    snapshot.RandomStreams.Count <= 256 && snapshot.NarrativeFlags.Count <= 1024,
                    "Snapshot section exceeds the wire bounds.");
                ValidateActors(snapshot, expectedBinding, rules, catalogue, mode);
                ValidatePractice(snapshot, course, mode);
                ValidateDeployments(snapshot, catalogue);
#if UNITY_EDITOR || GSK_QA
                using (WordboundPhaseCapture.Measure(WordboundPhase.ResourcesValidation))
#endif
                    ValidateResources(snapshot, course, mode, content);
#if UNITY_EDITOR || GSK_QA
                using (WordboundPhaseCapture.Measure(WordboundPhase.BodiesValidation))
#endif
                    ValidateBodies(snapshot, expectedBinding, course, mode, content);
                MatchThrownValidation.Validate(snapshot, mode, catalogue);
#if UNITY_EDITOR || GSK_QA
                using (WordboundPhaseCapture.Measure(WordboundPhase.MechanismsValidation))
#endif
                    ValidateMechanisms(snapshot, course, mode, content);
#if UNITY_EDITOR || GSK_QA
                using (WordboundPhaseCapture.Measure(WordboundPhase.BodyBudgetValidation))
#endif
                    ValidateBodyBudget(snapshot, course, mode, rules, content);
                Need(snapshot.RandomStreams.Select(r => r.StreamId).Distinct(StringComparer.Ordinal).Count() == snapshot.RandomStreams.Count &&
                    snapshot.RandomStreams.All(r => WordboundIdentity.IsId(r.StreamId) && (r.Increment & 1) == 1),
                    "Invalid or duplicate random stream state.");
                var stories = new HashSet<string>(StringComparer.Ordinal);
                if (mode.IsRace)
                    foreach (var team in snapshot.Progress.Teams)
                        for (int lap = 0; lap <= Math.Min(team.Lap, 2); lap++) stories.Add(NarrativeFlag(team.Team, lap));
                Need(snapshot.NarrativeFlags.Distinct(StringComparer.Ordinal).Count() == snapshot.NarrativeFlags.Count &&
                    snapshot.NarrativeFlags.All(stories.Contains), "Narrative flags must belong to reached team laps.");
                return "";
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is OverflowException ||
                e is KeyNotFoundException || e is NullReferenceException)
            { return e.Message; }
        }

        public static bool ReconcileAndApply(MatchSnapshot snapshot, MatchBinding expectedBinding, ModeDefinition mode,
            ResolvedCourse course, ItemCatalogue catalogue, GameRules initialRules, GameRules applyRules,
            Func<bool> reconcile, Action apply)
        {
            if (reconcile == null) throw new ArgumentNullException(nameof(reconcile));
            if (apply == null) throw new ArgumentNullException(nameof(apply));
            string error = Validate(snapshot, expectedBinding, mode, course, catalogue, initialRules);
            if (error != "") throw new ArgumentException(error);
            if (!reconcile()) return false;
            error = ValidateMutableDependencies(snapshot, expectedBinding, mode, course, catalogue, applyRules);
            if (error != "") throw new ArgumentException(error);
            apply();
            return true;
        }

        static string ValidateMutableDependencies(MatchSnapshot snapshot, MatchBinding binding, ModeDefinition mode,
            ResolvedCourse course, ItemCatalogue catalogue, GameRules rules)
        {
#if UNITY_EDITOR || GSK_QA
            using var phase = WordboundPhaseCapture.Measure(WordboundPhase.MutableValidation);
#endif
            try
            {
                Need(rules != null, "Snapshot and all trusted match dependencies are required.");
                ValidateEconomy(snapshot, binding, catalogue, rules);
                ValidateActors(snapshot, binding, rules, catalogue, mode);
                ValidateDeployments(snapshot, catalogue);
                MatchThrownValidation.Validate(snapshot, mode, catalogue);
                ValidateBodyBudget(snapshot, course, mode, rules, CourseCaches.GetValue(course, CreateCourseCache).For(mode));
                return "";
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is OverflowException ||
                e is KeyNotFoundException || e is NullReferenceException)
            { return e.Message; }
        }

        static void ValidateEconomy(MatchSnapshot snapshot, MatchBinding binding, ItemCatalogue catalogue, GameRules rules)
        {
#if UNITY_EDITOR || GSK_QA
            using var phase = WordboundPhaseCapture.Measure(WordboundPhase.EconomyValidation);
#endif
            var economy = new Economy(catalogue, rules);
            for (int i = 0; i < binding.Seats.Count; i++) economy.AddPlayer(i, binding.Seats[i].Team);
            economy.Restore(snapshot.Economy);
        }

        public static CoursePlayRequirements Requirements(ModeDefinition mode) => new CoursePlayRequirements(mode.Id,
            mode.IsRace ? CourseCapability.Circuit : mode.Id == "FreeForAll" ? CourseCapability.Arena : CourseCapability.Explore,
            mode.IsRace ? mode.TeamCount : 0, mode.IsRace ? mode.TeamCapacity : mode.Id == "FreeForAll" ? 1 : 0, mode.MaximumPlayers,
            mode.Ownership == WordboundOwnership.TeamIsolated ? CourseOwnershipPolicy.TeamIsolated :
            mode.Ownership == WordboundOwnership.ProtectedProgress ? CourseOwnershipPolicy.TeamProtected :
            mode.Id == "FreeForAll" ? CourseOwnershipPolicy.Individual : CourseOwnershipPolicy.Shared);

        public static RaceLap[] Route(ResolvedCourse course) => course.Laps.Select(l => new RaceLap(l.StartModuleId, l.FinishModuleId,
            l.NarrativeId, l.Checkpoints.Select(c => new RaceCheckpoint(c.Id, c.PuzzleIds.ToArray())).ToArray())).ToArray();

        public static string NarrativeFlag(int team, int lap) => "story:" + team.ToString(CultureInfo.InvariantCulture) + ":" + lap.ToString(CultureInfo.InvariantCulture);
        internal static void Need(bool condition, string message) { if (!condition) throw new ArgumentException(message); }

        static void ValidateBodyBudget(MatchSnapshot snapshot, ResolvedCourse course, ModeDefinition mode, GameRules rules,
            StaticContent content)
        {
            var budget = content.Budget;
            int pools = budget.PoolCount, capacity = budget.PoolBodies;
            var items = snapshot.Economy.ItemStates.ToDictionary(i => "item:" + i.Id, StringComparer.Ordinal);
            var mapBodies = snapshot.Bodies.Where(b => b.Kind == WorldBodyKind.Item &&
                items[b.Id].Origin == ItemOrigin.Map).ToArray();
            Need(mapBodies.All(b => course.Placements.Any(p => p.Id == b.SourceId && p.Purpose != "Resources")) &&
                mapBodies.Select(b => b.SourceId).Distinct(StringComparer.Ordinal).Count() == mapBodies.Length,
                "Authored map bodies must retain one unique placement identity.");
            foreach (var player in snapshot.Economy.PlayerStates)
                Need(snapshot.Economy.ItemStates.Count(i => i.DeployerId == player.PlayerId &&
                    (i.State == ItemState.Deployed || i.State == ItemState.Armed)) <= rules.MaxDeployed,
                    "Deployed equipment exceeds its reserved player capacity.");
            var dynamicBodies = snapshot.Bodies.Where(b =>
                !WordboundEntityBudget.FixedBody(b, items.TryGetValue(b.Id, out var item) ? item : null)).ToArray();
            int Owner(WorldBodyState body)
            {
                if (!mode.IsRace || !items.TryGetValue(body.Id, out var item)) return body.OwnerTeam;
                int player = item.State == ItemState.Held ? item.HolderId :
                    item.State == ItemState.Deployed || item.State == ItemState.Armed ? item.DeployerId : -1;
                return player >= 0 ? snapshot.Binding.Seats[player].Team : body.OwnerTeam;
            }
            var teams = snapshot.Progress.Teams.ToDictionary(t => t.Team);
            var lifecycle = content.Lifecycle;
            var auxiliary = WordboundEntityBudget.AuxiliaryBodies(course, mode, snapshot.Bodies,
                id => items.TryGetValue("item:" + id, out var item) ? item : null, snapshot.Mechanisms, snapshot.Progress.Teams, snapshot.Practice);
            var core = dynamicBodies.Where(b => !auxiliary.Contains(b.Id) &&
                !(b.Kind == WorldBodyKind.Tile && (lifecycle.PracticeSource(b.SourceId, snapshot.Practice, snapshot.Mechanisms) ||
                    mode.IsRace && b.OwnerTeam >= 0 && budget.CriticalSource(b.SourceId, teams[b.OwnerTeam])))).ToArray();
            var references = new HashSet<string>(snapshot.Actors.Where(a => a.Attack != null).SelectMany(a => a.Attack.HitBodyIds),
                StringComparer.Ordinal);
            foreach (var flight in snapshot.ThrownFlights)
            { references.Add(flight.ItemBodyId); references.UnionWith(flight.HitBodyIds); }
            references.UnionWith(snapshot.Mechanisms.Where(m => !m.Solved && m.ItemEntityId != "").Select(m => m.ItemEntityId));
            var pinned = core.Where(b => references.Contains(b.Id) || b.Kind == WorldBodyKind.Item &&
                items[b.Id].State != ItemState.World && items[b.Id].State != ItemState.Destroyed).ToArray();
            for (int team = 0; team < pools; team++)
            {
                Need(capacity >= 0 && WordboundEntityBudget.Usage(dynamicBodies, team, pools, Owner) <= capacity,
                    "Snapshot bodies exceed the reserved team capacity.");
                Need(WordboundEntityBudget.Usage(core, team, pools, Owner) <= capacity -
                    budget.CriticalRecoveryBodies - budget.PhysicalAuxiliaryBodies,
                    "Snapshot core bodies consume reserved recovery or physical auxiliary capacity.");
                int pending = snapshot.Economy.PlayerStates.Count(p => p.Craft != null && (!mode.IsRace || p.Team == team));
                Need(WordboundEntityBudget.Usage(pinned, team, pools, Owner) + pending <= capacity -
                    budget.CriticalRecoveryBodies - budget.PhysicalAuxiliaryBodies,
                    "Pending craft completions exceed unoccupied reserved core capacity.");
            }
        }

        public static long DeploymentExpiryTick(ItemDefinition definition, long deploymentTick)
        {
            Need(definition?.Deploy != null && deploymentTick >= 1 &&
                WorldVector.Finite(definition.Deploy.LifetimeSeconds) &&
                definition.Deploy.LifetimeSeconds >= 0 && definition.Deploy.LifetimeSeconds <= 86400,
                "Deployment requires a causal tick and a lifetime of at most one day.");
            if (definition.Deploy.LifetimeSeconds == 0) return -1;
            return checked(deploymentTick + (long)Math.Ceiling((double)definition.Deploy.LifetimeSeconds * ModeDefinition.TickRate));
        }

        static void ValidateDeployments(MatchSnapshot s, ItemCatalogue catalogue)
        {
            foreach (var item in s.Economy.ItemStates)
                if (item.State == ItemState.Deployed)
                {
                    Need(item.Origin == ItemOrigin.Crafted && item.DeployerId >= 0 &&
                        item.DeploymentTick >= 1 && item.DeploymentTick <= s.Progress.Tick &&
                        catalogue.TryGet(item.Word, out _), "Deployed item has no causal authoritative activation tick.");
                    long expiry = DeploymentExpiryTick(catalogue.Get(item.Word), item.DeploymentTick);
                    Need(expiry == -1 || expiry > s.Progress.Tick, "A live deployed item has already expired.");
                }
                else Need(item.DeploymentTick == -1, "Only deployed items may retain an activation tick.");
        }

        static void ValidateActors(MatchSnapshot s, MatchBinding binding, GameRules rules, ItemCatalogue catalogue, ModeDefinition mode)
        {
            Need(s.Actors.Count == binding.Seats.Count && s.Actors.Select(a => a.PlayerId).Distinct(StringComparer.Ordinal).Count() == s.Actors.Count &&
                s.Actors.All(a => binding.Seats.Any(seat => seat.PlayerId == a.PlayerId)), "Actor identities do not match the roster.");
            long tick = s.Progress.Tick;
            var bodyIds = s.Bodies.ToDictionary(b => b.Id, StringComparer.Ordinal);
            foreach (var actor in s.Actors)
            {
                Need(WorldVector.Finite(actor.Health) && actor.Health >= 0 && actor.Health <= rules.MaxHealth &&
                    WorldVector.Finite(actor.Stamina) && actor.Stamina >= 0 && actor.Stamina <= rules.MaxStamina &&
                    actor.NextAttackTick <= tick + 3600 && actor.StunnedUntilTick <= tick + ModeDefinition.MaximumHitStunSeconds * ModeDefinition.TickRate &&
                    actor.DodgeUntilTick <= tick + 3600 && actor.LastInputSequence < long.MaxValue &&
                    actor.StaggerImmuneUntilTick <= tick + WordboundCombatPolicy.Ticks(rules.StaggerImmunitySeconds),
                    "Actor health, stamina or cooldown exceeds its domain.");
                var progress = s.Progress.Players.Single(p => p.Id == actor.PlayerId);
                int index = binding.Seats.ToList().FindIndex(p => p.PlayerId == actor.PlayerId);
                var inventory = s.Economy.PlayerStates.Single(p => p.PlayerId == index);
                Need(actor.EquippedSlot >= -1 && actor.EquippedSlot < inventory.Slots.Length &&
                    (actor.EquippedSlot == -1 || inventory.CarriedFurniture < 0 &&
                        inventory.Slots[actor.EquippedSlot] > 0),
                    "Equipped slot must identify one held item and cannot coexist with carried furniture.");
                string defenseError = WordboundDefensePolicy.SnapshotError(s, actor, catalogue, mode);
                Need(defenseError == "", defenseError);
                if (actor.Attack != null)
                {
                    var attack = actor.Attack;
                    string selected = actor.EquippedSlot < 0 ? "" : "item:" + inventory.Slots[actor.EquippedSlot];
                    Need(inventory.CanAct && inventory.Craft == null && inventory.CarriedFurniture < 0 && actor.Health > 0 &&
                        progress.Connected && !progress.Eliminated && !progress.Returned && !s.Progress.Migrating &&
                        s.Progress.Result == null && actor.StunnedUntilTick <= tick && attack.WeaponEntityId == selected,
                        "Active attack is not available to this actor or equipment.");
                    var item = selected == "" ? null : s.Economy.ItemStates.SingleOrDefault(i => "item:" + i.Id == selected);
                    Need(selected == "" || item != null && item.State == ItemState.Held && item.HolderId == index,
                        "Active attack weapon is not held by its actor.");
                    var stats = item == null ? rules.Unarmed : catalogue.Get(item.Word).Melee;
                    int windup = WordboundCombatPolicy.WindupTicks(stats);
                    int active = WordboundCombatPolicy.ActiveTicks(stats);
                    Need(attack.StartedTick >= 1 && attack.StartedTick <= tick &&
                        tick < checked(attack.StartedTick + windup + active) &&
                        actor.NextAttackTick == checked(attack.StartedTick + WordboundCombatPolicy.CycleTicks(stats)) &&
                        (tick >= attack.StartedTick + windup || attack.HitBodyIds.Count == 0),
                        "Active attack timing, hit window or cooldown is not causal.");
                    foreach (string hitId in attack.HitBodyIds)
                    {
                        Need(bodyIds.TryGetValue(hitId, out var body) && hitId != "player:" + index &&
                            (body.Kind == WorldBodyKind.Player && mode.CanDamage(inventory.Team, body.OwnerTeam) ||
                             body.Kind == WorldBodyKind.Item && mode.CanInteract(inventory.Team, body.OwnerTeam, body.Protected)),
                            "Attack hit ledger references an invalid or inaccessible target.");
                    }
                }
                Need(!inventory.CanAct || (progress.Connected && !progress.Eliminated && !s.Progress.Migrating &&
                    s.Progress.Result == null && actor.StunnedUntilTick <= tick), "Inventory action permission disagrees with authoritative player state.");
                if (progress.Returned)
                    Need(!inventory.CanAct && inventory.Craft == null && inventory.LetterCount == 0 &&
                        inventory.CarriedFurniture == -1 && inventory.Slots.All(slot => slot == -1),
                        "A returned player cannot retain an inventory or pending craft.");
                Need(actor.Appearance.Length <= 2048, "Actor appearance exceeds bounds.");
                CourseJson.ValidateText(actor.Appearance);
            }
        }

        static void ValidateResources(MatchSnapshot s, ResolvedCourse course, ModeDefinition mode, StaticContent content)
        {
            var sources = content.Sources;
            var lifecycle = content.Lifecycle;
            Need(s.Resources.Count == sources.Count * (mode.IsRace ? 3 : 1) &&
                s.Resources.Select(r => ResourceKey(r.SourceId, r.Team, r.Lap)).Distinct(StringComparer.Ordinal).Count() == s.Resources.Count,
                "Snapshot must include every static source, team and lap exactly once.");
            var teams = s.Progress.Teams.ToDictionary(t => t.Team);
            TeamProgress Team(int id) => teams.TryGetValue(id, out var team) ? team : s.Progress.Teams.Single(t => t.Team == id);
            foreach (var r in s.Resources)
            {
                Need(sources.TryGetValue(r.SourceId, out var source) && source.Item1 == r.Team &&
                    (mode.IsRace ? r.Lap >= 0 && r.Lap < 3 : r.Lap == 0) &&
                    r.Generation >= 0 && (!mode.IsRace || r.Generation % 3 == r.Lap) && r.RecoveryAtTick >= -1 &&
                    (r.RecoveryAtTick == -1 || r.RecoveryAtTick > s.Progress.Tick && r.RecoveryAtTick <= s.Progress.Tick + 3600),
                    "Recovery source, team, lap or deadline is invalid.");
                if (mode.IsRace && r.Team >= 0 && r.Lap != Math.Min(Team(r.Team).Lap, 2))
                    Need(r.RecoveryAtTick == -1, "Inactive lap resources cannot retain a pending recovery deadline.");
                if (!lifecycle.Demanded(mode.IsRace ? Team(r.Team) : null, r, s.Practice, s.Mechanisms))
                    Need(r.RecoveryAtTick == -1, "Retired puzzle resources cannot retain a pending recovery deadline.");
            }
            var tiles = s.Economy.TileStates.ToDictionary(t => t.Key, t => t.Value);
            var resourceKeys = new HashSet<(string SourceId, int Team, int Lap)>(s.Resources.Select(r => (r.SourceId, r.Team, r.Lap)));
            var looseTiles = s.Bodies.Where(b => b.Kind == WorldBodyKind.Tile).ToLookup(b => b.SourceId, StringComparer.Ordinal);
            foreach (var pair in sources)
            {
                int lap = mode.IsRace && pair.Value.Item1 >= 0 ? Math.Min(Team(pair.Value.Item1).Lap, 2) : 0;
                Need(resourceKeys.Contains((pair.Key, pair.Value.Item1, lap)),
                    "Snapshot omits a current recovery source.");
                if (!looseTiles.Contains(pair.Key) && (pair.Value.Item3 == null || pair.Value.Item3.All(LetterBag.IsLetter))) continue;
                var available = LetterBag.FromWord(pair.Value.Item3);
                foreach (var body in looseTiles[pair.Key])
                    Need(tiles.TryGetValue(Suffix(body.Id, "tile:"), out char letter) && available.TryRemove(letter),
                        "Recovery source contains more loose letters than its authored batch.");
            }
        }

        internal static Dictionary<string, Tuple<int, bool, string>> Sources(ResolvedCourse course)
        {
            var sources = new Dictionary<string, Tuple<int, bool, string>>(StringComparer.Ordinal);
            foreach (var supply in course.Supplies) sources.Add(supply.Id, Tuple.Create(supply.TeamIndex, supply.Protected, supply.Letters));
            foreach (var p in course.Placements.Where(p => p.Purpose == "Resources")) sources.Add(p.Id, Tuple.Create(p.TeamIndex, true, p.Letters));
            return sources;
        }
        internal static string ResourceKey(string id, int team, int lap) => id + "/" + team.ToString(CultureInfo.InvariantCulture) + "/" + lap.ToString(CultureInfo.InvariantCulture);

        static void ValidateBodies(MatchSnapshot s, MatchBinding binding, ResolvedCourse course, ModeDefinition mode,
            StaticContent content)
        {
            Need(s.Bodies.Select(b => b.Id).Distinct(StringComparer.Ordinal).Count() == s.Bodies.Count, "Duplicate world body identity.");
            var bodies = s.Bodies.ToDictionary(b => b.Id, StringComparer.Ordinal);
            var items = s.Economy.ItemStates.ToDictionary(i => i.Id);
            var tiles = s.Economy.TileStates.ToDictionary(t => t.Key, t => t.Value);
            var sources = content.Sources;
            var lifecycle = content.Lifecycle;
            var placements = content.Placements;
            var teams = new HashSet<int>(binding.Seats.Select(seat => seat.Team));
            for (int i = 0; i < binding.Seats.Count; i++)
                Need(bodies.TryGetValue("player:" + i, out var body) && body.Kind == WorldBodyKind.Player &&
                    body.OwnerTeam == binding.Seats[i].Team && body.SourceId == binding.Seats[i].PlayerId && !body.Protected,
                    "Player world identity or ownership is missing.");
            foreach (var item in items.Values.Where(i => !i.IsGone))
                Need(bodies.TryGetValue("item:" + item.Id, out var body) && body.Kind == WorldBodyKind.Item, "Live inventory item has no world body.");
            foreach (int tile in tiles.Keys)
                Need(bodies.TryGetValue("tile:" + tile, out var body) && body.Kind == WorldBodyKind.Tile && body.Active,
                    "Live letter tile has no active world body.");
            foreach (var team in s.Progress.Teams)
                if (WordboundPracticePolicy.HasKeepsake(mode))
                    Need(bodies.TryGetValue("keepsake:" + team.Team, out var body) && body.Kind == WorldBodyKind.Keepsake &&
                        body.OwnerTeam == team.Team && body.Protected && body.RecoveryGeneration == team.RecoveryGeneration,
                        "Team keepsake body is missing or has the wrong recovery generation.");
            foreach (var body in s.Bodies)
            {
                Need(Bounded(body.Position, 10000) && Bounded(body.Velocity, 1000) && Bounded(body.AngularVelocity, 10000) &&
                    (body.OwnerTeam == -1 || teams.Contains(body.OwnerTeam)), "World body exceeds physical bounds or names an absent team.");
                switch (body.Kind)
                {
                    case WorldBodyKind.Player:
                        int player = Suffix(body.Id, "player:");
                        Need(player >= 0 && player < binding.Seats.Count && body.SourceId == binding.Seats[player].PlayerId &&
                            body.OwnerTeam == binding.Seats[player].Team && !body.Protected, "Unbound player body.");
                        Need(!s.Progress.Players.Single(p => p.Id == body.SourceId).Returned || !body.Active,
                            "A returned player cannot have an active world body.");
                        break;
                    case WorldBodyKind.Item:
                        int itemId = Suffix(body.Id, "item:");
                        Need(items.TryGetValue(itemId, out var item) && (!item.IsGone || !body.Active), "World item has no matching economy state or active tombstone.");
                        ValidateSource(body, sources, placements, s, mode, lifecycle);
                        if (body.SourceId == "" && item.Origin == ItemOrigin.Crafted)
                        {
                            Need(item.CrafterId >= 0 && item.CrafterId < binding.Seats.Count, "Crafted item needs a valid creator.");
                            int owner = mode.IsRace ? binding.Seats[item.CrafterId].Team : -1;
                            Need(body.OwnerTeam == owner && !body.Protected, "Crafted item ownership disagrees with its creator.");
                        }
                        break;
                    case WorldBodyKind.Tile:
                        int tileId = Suffix(body.Id, "tile:");
                        Need(tiles.TryGetValue(tileId, out char letter), "World letter has no matching economy entity.");
                        ValidateSource(body, sources, placements, s, mode, lifecycle);
                        if (sources.TryGetValue(body.SourceId, out var source))
                            Need(source.Item3.IndexOf(letter) >= 0, "Letter does not belong to its recovery source.");
                        break;
                    case WorldBodyKind.Keepsake:
                        int team = Suffix(body.Id, "keepsake:");
                        Need(WordboundPracticePolicy.HasKeepsake(mode) && teams.Contains(team) && body.OwnerTeam == team && body.Protected &&
                            body.SourceId == "", "Unbound keepsake body.");
                        break;
                    case WorldBodyKind.Platform:
                        if (content.FixturesById.TryGetValue(body.SourceId, out var physicalFixture))
                        {
                            var fixture = physicalFixture.Fixture;
                            Need((mode.IsRace || WordboundPracticePolicy.Supports(mode)) && teams.Contains(body.OwnerTeam) && body.Active && body.Kinematic && body.Protected &&
                                body.Id == CourseFixtureMotion.BodyId(fixture.Id, body.OwnerTeam) && body.RotationW == 1 &&
                                body.RotationXyz.X == 0 && body.RotationXyz.Y == 0 && body.RotationXyz.Z == 0,
                                "Unbound physical fixture body.");
                            var site = physicalFixture.Site;
                            var mechanism = s.Mechanisms.SingleOrDefault(m => m.PuzzleId == site.PuzzleId && m.Lap == site.Lap - 1 && m.Team == body.OwnerTeam);
                            Need(mechanism != null && body.Position.DistanceSquared(CourseFixtureMotion.Position(fixture, mechanism.Physical.MotionUnits)) < .000001f,
                                "Fixture pose differs from its canonical physical phase.");
                        }
                        else Need(body.Id.StartsWith("platform:", StringComparison.Ordinal) &&
                            content.ConnectionPlatforms.Contains(body.Id) &&
                            body.Kinematic && body.OwnerTeam == -1 && !body.Protected &&
                            (body.SourceId == "" || body.SourceId == body.Id.Substring(9)), "Unbound platform body.");
                        break;
                    default: throw new ArgumentException("Unknown world body kind.");
                }
            }
            if (mode.IsRace || WordboundPracticePolicy.Supports(mode))
                foreach (int team in teams)
                    foreach (var fixture in content.Fixtures)
                        Need(bodies.ContainsKey(CourseFixtureMotion.BodyId(fixture.Fixture.Id, team)), "An authored physical fixture is missing.");
        }

        static void ValidateSource(WorldBodyState body, Dictionary<string, Tuple<int, bool, string>> sources,
            Dictionary<string, ResolvedCoursePlacement> placements, MatchSnapshot s, ModeDefinition mode, WordboundResourceLifecycle lifecycle)
        {
            if (body.SourceId == "")
            {
                Need(!body.Protected && (mode.IsRace || body.OwnerTeam == -1) &&
                    (mode.Ownership != WordboundOwnership.TeamIsolated || body.OwnerTeam >= 0),
                    "Unsourced loose object has invalid protection or ownership.");
                return;
            }
            if (sources.TryGetValue(body.SourceId, out var source))
            {
                Need(body.OwnerTeam == source.Item1 && body.Protected == source.Item2 &&
                    s.Resources.Any(r => r.SourceId == body.SourceId && r.Team == body.OwnerTeam && r.Generation == body.RecoveryGeneration &&
                        lifecycle.Demanded(mode.IsRace ? s.Progress.Teams.Single(t => t.Team == r.Team) : null, r, s.Practice, s.Mechanisms)),
                    "Protected world object disagrees with its recovery source.");
                return;
            }
            Need(placements.TryGetValue(body.SourceId, out var placement) && placement.Purpose != "Resources" &&
                body.OwnerTeam == placement.TeamIndex && !body.Protected, "World source does not match an approved placement.");
        }

        static void ValidatePractice(MatchSnapshot s, ResolvedCourse course, ModeDefinition mode)
        {
            Need(s.Practice != null && (s.Practice.SiteHash == "" ? s.Practice.SelectedTick == -1 :
                WordboundPracticePolicy.Supports(mode) && s.Practice.SelectedTick >= 1 &&
                s.Practice.SelectedTick <= s.Progress.Tick && WordboundPracticePolicy.Selected(course, s.Practice) != null),
                "Practice selection differs from this mode, course or completed tick.");
            Need(WordboundPracticePolicy.Supports(mode) || s.Practice.SiteHash == "", "Only Freestyle can select practice.");
            if (WordboundPracticePolicy.Supports(mode))
                Need(s.Practice.SiteHash != "" || s.Mechanisms.All(m => !m.Solved && m.ActivatedTick == -1),
                    "Practice evidence cannot exist before a station was selected.");
        }

        static void ValidateMechanisms(MatchSnapshot s, ResolvedCourse course, ModeDefinition mode, StaticContent content)
        {
            Need(s.Mechanisms.Select(m => ResourceKey(m.PuzzleId, m.Team, m.Lap)).Distinct(StringComparer.Ordinal).Count() == s.Mechanisms.Count,
                "Duplicate mechanism state.");
            bool practiceMode = WordboundPracticePolicy.Supports(mode);
            if (!mode.IsRace && !practiceMode) { Need(s.Mechanisms.Count == 0, "This mode has no puzzle mechanisms."); return; }
            var puzzles = content.Puzzles;
            var teams = s.Progress.Teams.ToDictionary(t => t.Team);
            Need(s.Mechanisms.Count == (practiceMode ? course.PhysicalSites.Count : puzzles.Count * teams.Count * 3),
                "Snapshot must include every static team/lap mechanism.");
            foreach (var m in s.Mechanisms)
            {
                Need(puzzles.ContainsKey(m.PuzzleId) && teams.TryGetValue(m.Team, out var team) && m.Lap >= 0 && m.Lap < 3,
                    "Mechanism has an unknown puzzle, team or lap.");
                team = teams[m.Team];
                var site = content.Site(m.Lap + 1, m.PuzzleId);
                var practiceSite = practiceMode ? site : null;
                Need(!practiceMode || practiceSite != null && m.Team == 0, "Practice mechanism is not an authored station.");
                MatchStateValidationPhysical.Validate(s, course, m, site, puzzles[m.PuzzleId]);
                bool expectedSolved = practiceMode ? m.Solved : m.Lap < team.Lap ? course.Laps[m.Lap].Checkpoints.Any(c => c.PuzzleIds.Contains(m.PuzzleId)) :
                    m.Lap == team.Lap && team.HasPuzzle(m.PuzzleId);
                Need(m.Solved == expectedSolved && m.ActivatedTick >= -1 && m.ActivatedTick <= s.Progress.Tick &&
                    m.PhaseTick >= 0 && m.PhaseTick <= s.Progress.Tick && m.ProgressTicks >= 0 &&
                    m.ProgressTicks <= s.Progress.Tick && (!m.Solved || m.ActivatedTick >= 0) &&
                    (m.ActivatedTick < 0 ? m.PhaseTick == 0 && m.ProgressTicks == 0 && m.ItemEntityId == "" :
                        m.ActivatedTick >= 1 && m.PhaseTick >= m.ActivatedTick &&
                        m.ProgressTicks <= s.Progress.Tick - m.PhaseTick + 1),
                    "Mechanism completion or timing disagrees with ordered progress.");
                if (m.ItemEntityId != "")
                {
                    int item = Suffix(m.ItemEntityId, "item:");
                    Need(s.Economy.ItemStates.Any(i => i.Id == item), "Mechanism item reference is missing.");
                }
                if (practiceMode && m.Solved)
                {
                    var puzzle = puzzles[m.PuzzleId];
                    var spent = s.Economy.ItemStates.SingleOrDefault(i => "item:" + i.Id == m.ItemEntityId);
                    Need(spent != null && spent.Origin == ItemOrigin.Crafted && spent.Word == puzzle.RecipeId &&
                        (!puzzle.ConsumeLetters || spent.IsGone && spent.Spent), "Practice completion lacks its actual recipe cost.");
                    if (puzzle.ConsumeLetters)
                        Need(s.Mechanisms.Count(other => other.Solved && other.ItemEntityId == m.ItemEntityId) == 1,
                            "One consumed recipe cannot complete multiple practice stations.");
                }
                if (!m.Solved && m.ActivatedTick >= 0)
                {
                    var puzzle = puzzles[m.PuzzleId];
                    Need(practiceMode ? WordboundPracticePolicy.Active(s, practiceSite) && m.ActivatedTick >= s.Practice.SelectedTick :
                        !team.Finished && m.Lap == team.Lap &&
                        team.NextCheckpoint < course.Laps[team.Lap].Checkpoints.Count &&
                        course.Laps[team.Lap].Checkpoints[team.NextCheckpoint].PuzzleIds.Contains(m.PuzzleId) &&
                        puzzle.PrerequisiteIds.All(team.HasPuzzle),
                        "Active mechanism is outside the reached objective or its prerequisites.");
                    Need(m.ItemEntityId != "" || puzzle.RecipeId == "", "Active mechanism requires its deployed recipe item.");
                    if (m.ItemEntityId != "")
                    {
                        var item = s.Economy.ItemStates.Single(i => i.Id == Suffix(m.ItemEntityId, "item:"));
                        var body = s.Bodies.SingleOrDefault(b => b.Id == m.ItemEntityId);
                        var bounds = content.Geometry[puzzle.ModuleId].Bounds;
                        Need(body != null && body.Kind == WorldBodyKind.Item && body.Active && body.OwnerTeam == (practiceMode ? -1 : m.Team) &&
                            item.State == ItemState.Deployed && item.Origin == ItemOrigin.Crafted && item.Word == puzzle.RecipeId &&
                            body.Position.X >= bounds.Min.X / 1000f && body.Position.X <= bounds.Max.X / 1000f &&
                            body.Position.Y >= bounds.Min.Y / 1000f - .5f && body.Position.Y <= bounds.Max.Y / 1000f + .5f &&
                            body.Position.Z >= bounds.Min.Z / 1000f && body.Position.Z <= bounds.Max.Z / 1000f,
                            "Active mechanism item is missing, inaccessible or incompatible with its recipe.");
                    }
                }
            }
        }

        static bool Bounded(WorldVector v, float bound) => WorldVector.Finite(v.X) && WorldVector.Finite(v.Y) && WorldVector.Finite(v.Z) &&
            Math.Abs(v.X) <= bound && Math.Abs(v.Y) <= bound && Math.Abs(v.Z) <= bound;
        static int Suffix(string id, string prefix)
        {
            if (!id.StartsWith(prefix, StringComparison.Ordinal)) throw new ArgumentException("World ID kind prefix is invalid.");
            string suffix = id.Substring(prefix.Length);
            if (!int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < 0 ||
                value.ToString(CultureInfo.InvariantCulture) != suffix) throw new ArgumentException("World ID suffix is invalid.");
            return value;
        }
    }
}
