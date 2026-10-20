using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public static class MatchStateValidation
    {
        public static string Validate(MatchSnapshot snapshot, MatchBinding expectedBinding, ModeDefinition mode,
            ResolvedCourse course, ItemCatalogue catalogue, GameRules rules)
        {
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
                var progress = new MatchProgress(mode, expectedBinding.Seats, Route(course));
                progress.Restore(snapshot.Progress);
                var economy = new Economy(catalogue, rules);
                for (int i = 0; i < expectedBinding.Seats.Count; i++) economy.AddPlayer(i, expectedBinding.Seats[i].Team);
                economy.Restore(snapshot.Economy);
                var commands = new CommandJournal(expectedBinding.Hash, expectedBinding.Seats.Select(s => s.PlayerId));
                commands.Restore(snapshot.Commands);
                Need(snapshot.Commands.Epoch == snapshot.Epoch && snapshot.Commands.Receipts.All(r => r.Tick <= snapshot.Progress.Tick),
                    "Command journal epoch or receipt tick disagrees with the snapshot.");
                Need(snapshot.Economy.ItemStates.Length <= 8192 && snapshot.Economy.TileStates.Length <= 8192 &&
                    snapshot.Bodies.Count <= 8192 && snapshot.Resources.Count <= 8192 && snapshot.Mechanisms.Count <= 8192 &&
                    snapshot.RandomStreams.Count <= 256 && snapshot.NarrativeFlags.Count <= 1024,
                    "Snapshot section exceeds the wire bounds.");
                ValidateActors(snapshot, expectedBinding, rules, catalogue, mode);
                ValidateDeployments(snapshot, catalogue);
                ValidateResources(snapshot, course, mode);
                ValidateBodies(snapshot, expectedBinding, course, mode);
                ValidateMechanisms(snapshot, course, mode);
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
                    Need(item.Origin == ItemOrigin.Crafted && item.DeploymentTick >= 1 && item.DeploymentTick <= s.Progress.Tick &&
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

        static void ValidateResources(MatchSnapshot s, ResolvedCourse course, ModeDefinition mode)
        {
            var sources = Sources(course);
            Need(s.Resources.Count == sources.Count * (mode.IsRace ? 3 : 1) &&
                s.Resources.Select(r => ResourceKey(r.SourceId, r.Team, r.Lap)).Distinct(StringComparer.Ordinal).Count() == s.Resources.Count,
                "Snapshot must include every static source, team and lap exactly once.");
            foreach (var r in s.Resources)
            {
                Need(sources.TryGetValue(r.SourceId, out var source) && source.Item1 == r.Team &&
                    (mode.IsRace ? r.Lap >= 0 && r.Lap < 3 : r.Lap == 0) &&
                    r.Generation >= 0 && (!mode.IsRace || r.Generation % 3 == r.Lap) && r.RecoveryAtTick >= -1 &&
                    (r.RecoveryAtTick == -1 || r.RecoveryAtTick > s.Progress.Tick && r.RecoveryAtTick <= s.Progress.Tick + 3600),
                    "Recovery source, team, lap or deadline is invalid.");
                if (mode.IsRace && r.Team >= 0 && r.Lap != Math.Min(s.Progress.Teams.Single(t => t.Team == r.Team).Lap, 2))
                    Need(r.RecoveryAtTick == -1, "Inactive lap resources cannot retain a pending recovery deadline.");
            }
            foreach (var pair in sources)
            {
                int lap = mode.IsRace && pair.Value.Item1 >= 0 ? Math.Min(s.Progress.Teams.Single(t => t.Team == pair.Value.Item1).Lap, 2) : 0;
                Need(s.Resources.Any(r => r.SourceId == pair.Key && r.Team == pair.Value.Item1 && r.Lap == lap),
                    "Snapshot omits a current recovery source.");
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

        static void ValidateBodies(MatchSnapshot s, MatchBinding binding, ResolvedCourse course, ModeDefinition mode)
        {
            Need(s.Bodies.Select(b => b.Id).Distinct(StringComparer.Ordinal).Count() == s.Bodies.Count, "Duplicate world body identity.");
            var bodies = s.Bodies.ToDictionary(b => b.Id, StringComparer.Ordinal);
            var items = s.Economy.ItemStates.ToDictionary(i => i.Id);
            var tiles = s.Economy.TileStates.ToDictionary(t => t.Key, t => t.Value);
            var sources = Sources(course);
            var placements = course.Placements.ToDictionary(p => p.Id, StringComparer.Ordinal);
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
                if (mode.IsRace)
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
                        ValidateSource(body, sources, placements, s, mode);
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
                        ValidateSource(body, sources, placements, s, mode);
                        if (sources.TryGetValue(body.SourceId, out var source))
                            Need(source.Item3.IndexOf(letter) >= 0, "Letter does not belong to its recovery source.");
                        break;
                    case WorldBodyKind.Keepsake:
                        int team = Suffix(body.Id, "keepsake:");
                        Need(mode.IsRace && teams.Contains(team) && body.OwnerTeam == team && body.Protected &&
                            body.SourceId == "", "Unbound keepsake body.");
                        break;
                    case WorldBodyKind.Platform:
                        Need(body.Id.StartsWith("platform:", StringComparison.Ordinal) &&
                            course.Connections.Any(c => body.Id == "platform:" + c.Id) &&
                            body.Kinematic && body.OwnerTeam == -1 && !body.Protected &&
                            (body.SourceId == "" || body.SourceId == body.Id.Substring(9)), "Unbound platform body.");
                        break;
                    default: throw new ArgumentException("Unknown world body kind.");
                }
            }
        }

        static void ValidateSource(WorldBodyState body, Dictionary<string, Tuple<int, bool, string>> sources,
            Dictionary<string, ResolvedCoursePlacement> placements, MatchSnapshot s, ModeDefinition mode)
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
                        (!mode.IsRace || r.Lap <= Math.Min(s.Progress.Teams.Single(t => t.Team == r.Team).Lap, 2))),
                    "Protected world object disagrees with its recovery source.");
                return;
            }
            Need(placements.TryGetValue(body.SourceId, out var placement) && placement.Purpose != "Resources" &&
                body.OwnerTeam == placement.TeamIndex && !body.Protected, "World source does not match an approved placement.");
        }

        static void ValidateMechanisms(MatchSnapshot s, ResolvedCourse course, ModeDefinition mode)
        {
            Need(s.Mechanisms.Select(m => ResourceKey(m.PuzzleId, m.Team, m.Lap)).Distinct(StringComparer.Ordinal).Count() == s.Mechanisms.Count,
                "Duplicate mechanism state.");
            if (!mode.IsRace) { Need(s.Mechanisms.Count == 0, "This mode has no race mechanisms."); return; }
            var puzzles = new HashSet<string>(course.Puzzles.Select(p => p.Id), StringComparer.Ordinal);
            var teams = s.Progress.Teams.ToDictionary(t => t.Team);
            Need(s.Mechanisms.Count == puzzles.Count * teams.Count * 3, "Snapshot must include every static team/lap mechanism.");
            foreach (var m in s.Mechanisms)
            {
                Need(puzzles.Contains(m.PuzzleId) && teams.TryGetValue(m.Team, out var team) && m.Lap >= 0 && m.Lap < 3,
                    "Mechanism has an unknown puzzle, team or lap.");
                team = teams[m.Team];
                bool expectedSolved = m.Lap < team.Lap ? course.Laps[m.Lap].Checkpoints.Any(c => c.PuzzleIds.Contains(m.PuzzleId)) :
                    m.Lap == team.Lap && team.CompletedPuzzles.Contains(m.PuzzleId);
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
                if (!m.Solved && m.ActivatedTick >= 0)
                {
                    var puzzle = course.Puzzles.Single(p => p.Id == m.PuzzleId);
                    Need(!team.Finished && m.Lap == team.Lap &&
                        team.NextCheckpoint < course.Laps[team.Lap].Checkpoints.Count &&
                        course.Laps[team.Lap].Checkpoints[team.NextCheckpoint].PuzzleIds.Contains(m.PuzzleId) &&
                        puzzle.PrerequisiteIds.All(team.CompletedPuzzles.Contains),
                        "Active mechanism is outside the reached objective or its prerequisites.");
                    Need(m.ItemEntityId != "" || puzzle.RecipeId == "", "Active mechanism requires its deployed recipe item.");
                    if (m.ItemEntityId != "")
                    {
                        var item = s.Economy.ItemStates.Single(i => i.Id == Suffix(m.ItemEntityId, "item:"));
                        var body = s.Bodies.SingleOrDefault(b => b.Id == m.ItemEntityId);
                        var bounds = course.Geometry.Single(g => g.Id == puzzle.ModuleId).Bounds;
                        Need(body != null && body.Kind == WorldBodyKind.Item && body.Active && body.OwnerTeam == m.Team &&
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
