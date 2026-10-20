using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public interface IWordboundWorldQuery
    {
        bool HasLineOfSight(string actorId, WorldVector from, WorldVector to);
        bool CanDeploy(string actorId, string recipeId, WorldVector position);
        bool PuzzleSatisfied(string actorId, ResolvedCoursePuzzle puzzle, WorldBodyState placedItem);
    }

    public interface IWordboundReleaseQuery
    {
        bool TryRelease(WorldBodyState actor, WorldBodyState heldItem, bool throwing, out WorldVector position);
    }

    public sealed class HostDamage
    {
        public string ActorId { get; }
        public string BodyId { get; }
        public float Amount { get; }
        public int StunTicks { get; }
        public HostDamage(string actorId, string bodyId, float amount, int stunTicks = 0)
        {
            if (actorId == null || actorId != "" && !WordboundIdentity.IsId(actorId) || !WordboundIdentity.IsId(bodyId) ||
                !WorldVector.Finite(amount) || amount <= 0 || amount > 1000 || stunTicks < 0 ||
                stunTicks > ModeDefinition.TickRate) throw new ArgumentException("Invalid trusted damage observation.");
            ActorId = actorId; BodyId = bodyId; Amount = amount; StunTicks = stunTicks;
        }
    }

    public sealed class MatchTickResult
    {
        public bool Advanced { get; }
        public IReadOnlyList<CommandReceipt> Receipts { get; }
        internal MatchTickResult(bool advanced, IEnumerable<CommandReceipt> receipts)
        { Advanced = advanced; Receipts = Array.AsReadOnly(receipts.ToArray()); }
    }

    public sealed class HostMechanismObservation
    {
        public string PuzzleId { get; }
        public int Team { get; }
        public int Lap { get; }
        public bool Active { get; }
        public long PhaseTick { get; }
        public int ProgressTicks { get; }
        public string ItemEntityId { get; }
        public HostMechanismObservation(string puzzleId, int team, int lap, bool active,
            long phaseTick = 0, int progressTicks = 0, string itemEntityId = "")
        {
            if (!WordboundIdentity.IsId(puzzleId) || team < 0 || team > 7 || lap < 0 || lap > 2 ||
                phaseTick < 0 || progressTicks < 0 || itemEntityId == null ||
                itemEntityId != "" && !WordboundIdentity.IsId(itemEntityId))
                throw new ArgumentException("Invalid host mechanism observation.");
            PuzzleId = puzzleId; Team = team; Lap = lap; Active = active; PhaseTick = phaseTick;
            ProgressTicks = progressTicks; ItemEntityId = itemEntityId;
        }
    }

    public sealed class AuthoritativeMatch : ITransactionalCommandTarget
    {
        const float InteractionRange = 3f;
        const int RecoveryTicks = 10 * ModeDefinition.TickRate;
        readonly ResolvedCourse course;
        readonly CourseCatalogue courseCatalogue;
        readonly ItemCatalogue catalogue;
        readonly GameRules rules;
        readonly IWordboundWorldQuery world;
        readonly Dictionary<string, int> indices;
        readonly Dictionary<string, WorldBodyState> bodies = new Dictionary<string, WorldBodyState>(StringComparer.Ordinal);
        readonly Dictionary<string, MatchActorState> actors = new Dictionary<string, MatchActorState>(StringComparer.Ordinal);
        readonly Dictionary<string, MatchResourceState> resources = new Dictionary<string, MatchResourceState>(StringComparer.Ordinal);
        readonly Dictionary<string, MatchMechanismState> mechanisms = new Dictionary<string, MatchMechanismState>(StringComparer.Ordinal);
        readonly Dictionary<string, MatchRandomState> random = new Dictionary<string, MatchRandomState>(StringComparer.Ordinal);
        readonly HashSet<string> narrative = new HashSet<string>(StringComparer.Ordinal);
        readonly List<ProgressEvent> pending = new List<ProgressEvent>();
        readonly Economy economy;
        readonly MatchProgress progress;
        readonly CommandJournal journal;
        SnapshotCommitTracker commits;
        long nextWorldEntityId = 1;
        bool inTick;

        public MatchBinding Binding { get; }
        public ModeDefinition Mode { get; }
        public long Tick => progress.Tick;
        public long Epoch => journal.Epoch;
        public bool IsPaused => progress.Migrating || progress.Result != null || !commits.CanAdvance(Tick + 1);

        public AuthoritativeMatch(MatchBinding binding, ModeDefinition mode, ResolvedCourse resolvedCourse,
            CourseCatalogue geometryCatalogue, ItemCatalogue itemCatalogue, GameRules gameRules,
            IWordboundWorldQuery hostWorld, long wallClockMs = 0)
        {
            Binding = binding ?? throw new ArgumentNullException(nameof(binding));
            Mode = mode ?? throw new ArgumentNullException(nameof(mode));
            course = resolvedCourse ?? throw new ArgumentNullException(nameof(resolvedCourse));
            courseCatalogue = geometryCatalogue ?? throw new ArgumentNullException(nameof(geometryCatalogue));
            catalogue = itemCatalogue ?? throw new ArgumentNullException(nameof(itemCatalogue));
            rules = gameRules?.Clone() ?? throw new ArgumentNullException(nameof(gameRules));
            world = hostWorld ?? throw new ArgumentNullException(nameof(hostWorld));
            if (rules.Mode != Mode.Id || rules.StarterLetters != "" || rules.MaxCarried > 8 || rules.Validate().Count != 0 ||
                !CourseValidator.ValidateResolved(course, courseCatalogue, MatchStateValidation.Requirements(Mode)).Ok)
                throw new ArgumentException("Authoritative content or gameplay rules are invalid.");
            WordboundCombatPolicy.Validate(rules.Unarmed);
            WordboundCombatPolicy.Ticks(rules.StaggerImmunitySeconds);
            foreach (string recipeId in Mode.Recipes)
                if (catalogue.TryGet(recipeId, out var combatItem) && combatItem.Melee != null)
                    WordboundCombatPolicy.Validate(combatItem.Melee);
            foreach (var puzzle in course.Puzzles)
                if (puzzle.RecipeId != "" && (!Mode.Recipes.Contains(puzzle.RecipeId) ||
                    !catalogue.TryGet(puzzle.RecipeId, out var recipe) || !recipe.Enabled ||
                    !recipe.Letters.Equals(LetterBag.FromWord(puzzle.RequiredLetters))))
                    throw new ArgumentException("A puzzle recipe is unavailable or has a different letter cost.");
            indices = Binding.Seats.Select((seat, index) => (seat, index)).ToDictionary(x => x.seat.PlayerId, x => x.index, StringComparer.Ordinal);
            economy = new Economy(catalogue, rules);
            progress = new MatchProgress(Mode, Binding.Seats, MatchStateValidation.Route(course), wallClockMs);
            journal = new CommandJournal(Binding.Hash, indices.Keys);
            commits = new SnapshotCommitTracker(Binding.MatchId, Binding.Hash);
            for (int i = 0; i < Binding.Seats.Count; i++)
            {
                var seat = Binding.Seats[i];
                economy.AddPlayer(i, seat.Team);
                actors.Add(seat.PlayerId, new MatchActorState(seat.PlayerId, rules.MaxHealth, rules.MaxStamina));
                AddBody(new WorldBodyState("player:" + i, WorldBodyKind.Player, Spawn(i), ownerTeam: seat.Team, sourceId: seat.PlayerId));
            }
            foreach (int team in Binding.Seats.Select(s => s.Team).Distinct())
                if (Mode.IsRace)
                {
                    AddBody(new WorldBodyState("keepsake:" + team, WorldBodyKind.Keepsake,
                        Landmark(team, progress.Team(team).SafeCheckpoint), ownerTeam: team, protectedResource: true));
                    for (int lap = 0; lap < 3; lap++)
                        foreach (var puzzle in course.Puzzles)
                            mechanisms.Add(Key(puzzle.Id, team, lap), new MatchMechanismState(puzzle.Id, team, lap));
                }
            foreach (var placement in course.Placements)
            {
                if (placement.Purpose == "Resources")
                    AddResource(placement.Id, placement.TeamIndex);
                else
                {
                    var item = economy.PlaceFurniture(courseCatalogue.Assets[placement.AssetId].Letters);
                    float y = placement.Yaw == 180 ? 1 : placement.Yaw == 0 ? 0 : .70710677f;
                    float w = placement.Yaw == 180 ? 0 : placement.Yaw == 0 ? 1 : placement.Yaw == 90 ? .70710677f : -.70710677f;
                    AddBody(new WorldBodyState("item:" + item.Id, WorldBodyKind.Item, Position(placement.Position),
                        rotationXyz: new WorldVector(0, y, 0), rotationW: w,
                        ownerTeam: placement.TeamIndex, sourceId: placement.Id));
                }
            }
            foreach (var supply in course.Supplies) AddResource(supply.Id, supply.TeamIndex);
            foreach (var resource in resources.Values.ToArray())
                if (resource.Lap == 0 && SourceAvailable(resource)) MintSource(resource);
            foreach (var connection in course.Connections.Where(c => c.GatePuzzleId != ""))
                AddBody(new WorldBodyState("platform:" + connection.Id, WorldBodyKind.Platform,
                    Position(connection.From), kinematic: true, sourceId: connection.Id));
            Synchronize(null);
            EnsureValid(Capture(1));
        }

        public MatchTickResult Advance(long wallClockMs, IEnumerable<AuthenticatedCommand> commands = null,
            IEnumerable<WorldBodyState> hostBodyStates = null, IEnumerable<ProgressEvent> hostObservations = null,
            IEnumerable<HostDamage> hostDamage = null, IEnumerable<HostMechanismObservation> hostMechanisms = null)
        {
            if (inTick) throw new InvalidOperationException("A tick cannot be reentered.");
            if (IsPaused) return new MatchTickResult(false, Array.Empty<CommandReceipt>());
            var requests = Bounded(commands, CommandJournal.MaximumBatch);
            var poses = Bounded(hostBodyStates, 8192);
            var observations = Bounded(hostObservations, 4096);
            var damages = Bounded(hostDamage, 256);
            var timings = Bounded(hostMechanisms, 1024);
            if (poses.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != poses.Length ||
                timings.Select(m => Key(m.PuzzleId, m.Team, m.Lap)).Distinct(StringComparer.Ordinal).Count() != timings.Length ||
                wallClockMs < progress.WallClockMs || wallClockMs > long.MaxValue - 120000)
                throw new ArgumentException("Invalid host frame.");
            var before = Capture(Math.Max(1, commits.PublishedSequence));
            inTick = true;
            try
            {
                pending.Clear();
                foreach (var pose in poses) ApplyPose(pose);
                SetPermissions(Tick + 1);
                ExpireDeployments(Tick + 1);
                foreach (var timing in timings) ApplyMechanism(timing);
                foreach (var seat in Binding.Seats)
                {
                    int index = indices[seat.PlayerId];
                    var crafted = economy.CompleteCraft(index, (Tick + 1) / (double)ModeDefinition.TickRate);
                    if (crafted.Ok) AddCraftedBody(crafted.Item, seat);
                }
                var receipts = journal.ExecuteBatch(Tick + 1, requests, this);
                AdvanceCombat(Tick + 1);
                foreach (var damage in damages.OrderBy(d => d.ActorId, StringComparer.Ordinal).ThenBy(d => d.BodyId, StringComparer.Ordinal))
                    ApplyDamage(damage);
                foreach (var observation in observations) ObserveProgress(observation);
                foreach (var body in bodies.Values.Where(b => b.Kind == WorldBodyKind.Player && b.Active && BelowCourse(b.Position)).ToArray())
                    pending.Add(new ProgressEvent(ProgressEventKind.Fell, body.SourceId));
                progress.Advance(Tick + 1, wallClockMs, pending);
                Synchronize(before);
                EnsureCapacity();
                pending.Clear();
                return new MatchTickResult(true, receipts);
            }
            catch { RestoreState(before); pending.Clear(); throw; }
            finally { inTick = false; }
        }

        public MatchSnapshot Capture(long sequence)
        {
            if (inTick) throw new InvalidOperationException("Capture only a completed tick.");
            return CaptureState(sequence);
        }

        MatchSnapshot CaptureState(long sequence) => new MatchSnapshot(Binding, Epoch, sequence, nextWorldEntityId,
            progress.Capture(), economy.Capture(), journal.Capture(), actors.Values, bodies.Values,
            resources.Values, mechanisms.Values, random.Values, narrative);

        public SnapshotStamp PublishSnapshot(out string json)
        {
            if (inTick) throw new InvalidOperationException("Publish only a completed tick.");
            var snapshot = Capture(checked(commits.PublishedSequence + 1));
            EnsureValid(snapshot);
            json = MatchSnapshotCodec.Export(snapshot);
            return commits.Publish(Tick, MatchSnapshotCodec.ComputeHash(snapshot));
        }

        public bool Acknowledge(SnapshotStamp durableCommit)
        {
            if (inTick) return false;
            if (!commits.Acknowledge(durableCommit)) return false;
            progress.ConfirmResult(commits.AcknowledgedTick);
            return true;
        }

        public void PauseForMigration()
        {
            if (inTick) throw new InvalidOperationException("Pause only between ticks.");
            progress.BeginMigration();
            SetPermissions(Tick);
        }

        public void RestoreCommitted(MatchSnapshot snapshot, SnapshotStamp durableCommit, long nextEpoch, long wallClockMs)
        {
            if (inTick) throw new InvalidOperationException("Restore only between ticks.");
            EnsureValid(snapshot);
            if (durableCommit == null || durableCommit.MatchId != Binding.MatchId || durableCommit.IdentityHash != Binding.Hash ||
                durableCommit.Epoch != snapshot.Epoch || durableCommit.Sequence != snapshot.Sequence ||
                durableCommit.Tick != snapshot.Progress.Tick || durableCommit.StateHash != MatchSnapshotCodec.ComputeHash(snapshot) ||
                nextEpoch != snapshot.Epoch + 1 || nextEpoch <= Epoch || nextEpoch == long.MaxValue ||
                wallClockMs < snapshot.Progress.WallClockMs || wallClockMs > long.MaxValue - 120000)
                throw new ArgumentException("Recovery requires the exact durable snapshot and the next epoch.");
            RestoreState(snapshot);
            journal.AdvanceEpoch(nextEpoch);
            commits = new SnapshotCommitTracker(Binding.MatchId, Binding.Hash, nextEpoch, Tick, snapshot.Sequence);
            progress.CompleteMigration(wallClockMs);
            progress.ConfirmResult(Tick);
            SetPermissions(Tick);
        }

        public bool Disconnect(string playerId, long wallClockMs)
        {
            if (inTick) throw new InvalidOperationException("Disconnect only between ticks.");
            if (!progress.Disconnect(playerId, wallClockMs)) return false;
            economy.CancelCraft(indices[playerId]);
            SetPermissions(Tick);
            UpdateKeepsakes();
            return true;
        }

        public bool Reconnect(string playerId, long wallClockMs)
        {
            if (inTick) throw new InvalidOperationException("Reconnect only between ticks.");
            bool reconnected = progress.Reconnect(playerId, wallClockMs);
            SetPermissions(Tick);
            return reconnected;
        }

        public bool Return(string playerId, long wallClockMs)
        {
            if (inTick) throw new InvalidOperationException("Return only between ticks.");
            if (!indices.TryGetValue(playerId ?? "", out int index) || progress.Player(playerId).Returned) return false;
            var before = Capture(Math.Max(1, commits.PublishedSequence));
            try
            {
                if (!progress.Return(playerId, wallClockMs)) return false;
                var seat = Binding.Seats[index];
                var dropped = new List<ItemInstance>();
                var tiles = economy.Eliminate(index, dropped);
                WorldVector safe = RecoveryPosition(seat);
                AddTiles(tiles, safe, Mode.IsRace ? seat.Team : -1);
                foreach (var item in dropped) Release(Outcome.Done(item), safe, default, false);
                actors[playerId] = Actor(actors[playerId], stun: 0);
                UpdateKeepsakes();
                SetPermissions(Tick);
                EnsureCapacity();
                return true;
            }
            catch { RestoreState(before); throw; }
        }

        object ITransactionalCommandTarget.CaptureTransactionState() =>
            Tuple.Create(CaptureState(Math.Max(1, commits.PublishedSequence)), pending.ToArray());
        void ITransactionalCommandTarget.RestoreTransactionState(object state)
        {
            var transaction = (Tuple<MatchSnapshot, ProgressEvent[]>)state;
            RestoreState(transaction.Item1); pending.Clear(); pending.AddRange(transaction.Item2);
        }
        CommandEffect ITransactionalCommandTarget.ApplyValidatedCommand(AuthenticatedCommand request)
        {
            if (!inTick) throw new InvalidOperationException("Commands execute only inside an authoritative tick.");
            var command = request.Command;
            int index = indices[request.ActorId];
            var seat = Binding.Seats[index];
            var actor = actors[seat.PlayerId];
            actors[seat.PlayerId] = Actor(actor, lastInput: command.Sequence);
            if (!economy.Player(index).CanAct) return Effect("NotAlive");
            var origin = bodies["player:" + index];
            Outcome outcome;
            switch (command.Action)
            {
                case WordboundAction.Equip:
                    if (command.Target != "" || command.Value != "" || command.Revision != 0) return Effect("InvalidPayload");
                    if (economy.Player(index).CarriedFurniture >= 0) return Effect("HandsFull");
                    if (command.Slot >= 0 && Held(index, command.Slot) == null) return Effect("EmptySlot");
                    actors[seat.PlayerId] = Actor(actors[seat.PlayerId], equippedSlot: command.Slot, clearAttack: true);
                    return Effect("Ok", command.Slot < 0 ? "" : "item:" + Held(index, command.Slot).Id);
                case WordboundAction.Attack:
                    if (command.Target != "" || command.Value != "" || command.Revision != 0 || command.Slot != -1)
                        return Effect("InvalidPayload");
                    return BeginAttack(seat, index);
                case WordboundAction.BeginCraft:
                    if (!Mode.Recipes.Contains(command.Value)) return Effect("RecipeDisabled");
                    outcome = economy.BeginCraft(index, command.Value, (Tick + 1) / (double)ModeDefinition.TickRate);
                    return Effect(outcome);
                case WordboundAction.CancelCraft: return Effect(economy.CancelCraft(index));
                case WordboundAction.CollectLetter:
                    if (!Reachable(seat, origin, command.Target, WorldBodyKind.Tile, out var tile)) return Effect("OutOfReach");
                    outcome = economy.CollectTile(index, Number(tile.Id));
                    if (outcome.Ok) { bodies.Remove(tile.Id); ScheduleSource(tile); }
                    return Effect(outcome);
                case WordboundAction.PickUp:
                    if (!Reachable(seat, origin, command.Target, WorldBodyKind.Item, out var itemBody)) return Effect("OutOfReach");
                    outcome = economy.PickUp(index, Number(itemBody.Id), command.Revision);
                    if (outcome.Ok)
                    {
                        outcome.Item.DeploymentTick = -1;
                        bodies[itemBody.Id] = itemBody.At(origin.Position, active: false, kinematic: true);
                    }
                    return Effect(outcome);
                case WordboundAction.Drop:
                    if (!ReleasePosition(index, command.Slot, origin, false, out var dropAt, out var dropRefusal))
                        return Effect(dropRefusal);
                    outcome = command.Slot < 0 ? economy.DropCarried(index) : economy.Drop(index, command.Slot);
                    Release(outcome, dropAt, default, false, origin);
                    return Effect(outcome);
                case WordboundAction.Throw:
                    if (!ReleasePosition(index, command.Slot, origin, true, out var throwAt, out var throwRefusal))
                        return Effect(throwRefusal);
                    outcome = economy.Throw(index, command.Slot, "");
                    Release(outcome, throwAt, Forward(origin, 8), false, origin);
                    return Effect(outcome);
                case WordboundAction.Deploy:
                    var held = Held(index, command.Slot);
                    if (held == null) return Effect("EmptySlot");
                    var placement = Add(origin.Position, Forward(origin, 1.5f));
                    if (!world.CanDeploy(seat.PlayerId, held.Word, placement)) return Effect("BlockedPlacement");
                    if (catalogue.Get(held.Word).Deploy != null)
                        MatchStateValidation.DeploymentExpiryTick(catalogue.Get(held.Word), Tick + 1);
                    outcome = economy.Deploy(index, command.Slot);
                    if (outcome.Ok) outcome.Item.DeploymentTick = Tick + 1;
                    Release(outcome, placement, default, true, origin);
                    return Effect(outcome);
                case WordboundAction.GiveLetter:
                case WordboundAction.Transfer:
                    if (!indices.TryGetValue(command.Target, out int other) ||
                        Binding.Seats[other].Team != seat.Team || !economy.Player(other).CanAct ||
                        !WithinReach(seat.PlayerId, origin.Position, bodies["player:" + other].Position)) return Effect("OutOfReach");
                    if (command.Action == WordboundAction.GiveLetter)
                        return command.Value.Length == 1 && LetterBag.IsLetter(command.Value[0]) ?
                            Effect(economy.GiveLetter(index, other, command.Value[0])) : Effect("InvalidLetter");
                    return Effect(economy.Transfer(index, other, command.Slot));
                case WordboundAction.TakeKeepsake:
                    string keepsakeId = "keepsake:" + seat.Team;
                    if (!Mode.IsRace || !Reachable(seat, origin, keepsakeId, WorldBodyKind.Keepsake, out _)) return Effect("OutOfReach");
                    bool taken = progress.TakeKeepsake(seat.PlayerId); UpdateKeepsakes();
                    return Effect(taken ? "Ok" : "Unavailable", taken ? keepsakeId : "");
                case WordboundAction.DropKeepsake:
                    bool dropped = progress.DropKeepsake(seat.PlayerId);
                    if (dropped) bodies["keepsake:" + seat.Team] = bodies["keepsake:" + seat.Team].At(origin.Position, active: true, kinematic: false);
                    return Effect(dropped ? "Ok" : "Unavailable");
                case WordboundAction.UsePuzzle: return UsePuzzle(seat, command, origin);
                default: return Effect("Unsupported");
            }
        }

        CommandEffect UsePuzzle(WordboundSeat seat, PlayerCommand command, WorldBodyState origin)
        {
            if (!Mode.IsRace) return Effect("Unavailable");
            var team = progress.Team(seat.Team);
            if (team.Finished || team.NextCheckpoint >= course.Laps[team.Lap].Checkpoints.Count ||
                !course.Laps[team.Lap].Checkpoints[team.NextCheckpoint].PuzzleIds.Contains(command.Target))
                return Effect("OutOfOrder");
            var puzzle = course.Puzzles.Single(p => p.Id == command.Target);
            string key = Key(puzzle.Id, seat.Team, team.Lap);
            if (mechanisms[key].Solved || pending.Any(e => e.Kind == ProgressEventKind.PuzzleSatisfied &&
                e.Target == puzzle.Id && progress.Player(e.PlayerId).Team == seat.Team)) return Effect("AlreadySolved");
            if (!InModule(origin.Position, puzzle.ModuleId) ||
                puzzle.PrerequisiteIds.Any(id => !team.CompletedPuzzles.Contains(id) &&
                    !pending.Any(e => e.Kind == ProgressEventKind.PuzzleSatisfied && e.Target == id &&
                        progress.Player(e.PlayerId).Team == seat.Team))) return Effect("OutOfOrder");
            int index = indices[seat.PlayerId];
            WorldBodyState itemBody = null;
            ItemInstance item = null;
            if (puzzle.RecipeId != "")
            {
                if (!Reachable(seat, origin, command.Value, WorldBodyKind.Item, out itemBody)) return Effect("OutOfReach");
                item = economy.Item(Number(itemBody.Id));
                if (item == null || item.IsGone || item.Word != puzzle.RecipeId || item.Origin != ItemOrigin.Crafted ||
                    item.State != ItemState.Deployed || itemBody.OwnerTeam != seat.Team || !InModule(itemBody.Position, puzzle.ModuleId))
                    return Effect("WrongPuzzleItem");
            }
            else if (!economy.Player(index).Letters.Contains(LetterBag.FromWord(puzzle.RequiredLetters)))
                return Effect("MissingLetters");
            if (!world.PuzzleSatisfied(seat.PlayerId, puzzle, itemBody)) return Effect("MechanismIncomplete");
            if (puzzle.ConsumeLetters)
            {
                if (item != null) { economy.CommitWordboundPuzzleItem(item.Id); bodies[itemBody.Id] = itemBody.At(itemBody.Position, active: false, kinematic: true); }
                else economy.CommitWordboundPuzzleLetters(index, puzzle.RequiredLetters);
            }
            var mechanism = mechanisms[key];
            bool timed = mechanism.ActivatedTick >= 0;
            mechanisms[key] = new MatchMechanismState(puzzle.Id, seat.Team, team.Lap, true,
                timed ? mechanism.ActivatedTick : Tick + 1, timed ? mechanism.PhaseTick : Tick + 1,
                timed ? mechanism.ProgressTicks : 1, itemBody?.Id ?? "");
            pending.Add(new ProgressEvent(ProgressEventKind.PuzzleSatisfied, seat.PlayerId, puzzle.Id, team.Lap));
            if (puzzle.RewardLetters.Length > 0) AddTiles(economy.MintTiles(puzzle.RewardLetters), origin.Position, seat.Team);
            return Effect("Ok", puzzle.Id);
        }

        CommandEffect BeginAttack(WordboundSeat seat, int index)
        {
            NormalizeEquipment(seat.PlayerId, Tick + 1);
            var actor = actors[seat.PlayerId];
            var inventory = economy.Player(index);
            if (inventory.CarriedFurniture >= 0) return Effect("HandsFull");
            if (inventory.Craft != null) return Effect("AlreadyCrafting");
            if (actor.NextAttackTick > Tick + 1) return Effect("Cooldown");
            var item = actor.EquippedSlot < 0 ? null : Held(index, actor.EquippedSlot);
            var stats = item == null ? rules.Unarmed : catalogue.Get(item.Word).Melee;
            if (stats == null) return Effect("NoMelee");
            int cycle = WordboundCombatPolicy.CycleTicks(stats);
            var attack = new MatchAttackState(Tick + 1, item == null ? "" : "item:" + item.Id);
            actors[seat.PlayerId] = Actor(actor, nextAttack: checked(Tick + 1 + cycle), attack: attack);
            return Effect("Ok", attack.WeaponEntityId);
        }

        void AdvanceCombat(long tick)
        {
            foreach (var seat in Binding.Seats)
            {
                NormalizeEquipment(seat.PlayerId, tick);
                var actor = actors[seat.PlayerId];
                var attack = actor.Attack;
                if (attack == null) continue;
                var stats = attack.WeaponEntityId == "" ? rules.Unarmed : catalogue.Get(economy.Item(Number(attack.WeaponEntityId)).Word).Melee;
                long starts = checked(attack.StartedTick + WordboundCombatPolicy.WindupTicks(stats));
                if (tick >= checked(starts + WordboundCombatPolicy.ActiveTicks(stats)))
                {
                    actors[seat.PlayerId] = Actor(actor, clearAttack: true);
                    continue;
                }
                if (tick < starts) continue;
                var origin = bodies["player:" + indices[seat.PlayerId]];
                var hitIds = new HashSet<string>(attack.HitBodyIds, StringComparer.Ordinal);
                foreach (var body in bodies.Values.OrderBy(b => b.Id, StringComparer.Ordinal).ToArray())
                {
                    if (!body.Active || body.Id == origin.Id || hitIds.Contains(body.Id) ||
                        body.Kind != WorldBodyKind.Player && body.Kind != WorldBodyKind.Item ||
                        !WordboundCombatPolicy.InReach(origin, body, stats)) continue;
                    float amount = body.Kind == WorldBodyKind.Item ? stats.BreakPower : stats.Damage;
                    if (amount <= 0) continue;
                    int stun = Math.Min(ModeDefinition.TickRate,
                        WordboundCombatPolicy.Ticks(Math.Min(stats.HitStun, rules.HitStunMax)));
                    if (!ApplyDamage(new HostDamage(seat.PlayerId, body.Id, amount, stun))) continue;
                    hitIds.Add(body.Id);
                    var current = bodies[body.Id];
                    if (current.Active && !current.Kinematic && stats.Knockback > 0)
                    {
                        float x = body.Position.X - origin.Position.X, z = body.Position.Z - origin.Position.Z;
                        float length = (float)Math.Sqrt(x * x + z * z);
                        var facing = Forward(origin, 1);
                        x = length > .0001f ? x / length : facing.X;
                        z = length > .0001f ? z / length : facing.Z;
                        float speed = stats.Knockback * 2;
                        var velocity = Add(current.Velocity, new WorldVector(x * speed, speed * .35f, z * speed));
                        bodies[body.Id] = current.At(current.Position,
                            new WorldVector(Math.Max(-1000, Math.Min(1000, velocity.X)),
                                Math.Max(-1000, Math.Min(1000, velocity.Y)), Math.Max(-1000, Math.Min(1000, velocity.Z))));
                    }
                }
                actors[seat.PlayerId] = Actor(actors[seat.PlayerId],
                    attack: new MatchAttackState(attack.StartedTick, attack.WeaponEntityId, hitIds));
                if (tick == starts && attack.WeaponEntityId != "")
                {
                    var weapon = economy.Item(Number(attack.WeaponEntityId));
                    var tiles = economy.Damage(weapon.Id, 1);
                    if (weapon.IsGone)
                    {
                        var body = bodies[attack.WeaponEntityId];
                        bodies[body.Id] = body.At(origin.Position, active: false, kinematic: true);
                        AddTiles(tiles, origin.Position, body.OwnerTeam, body.SourceId, body.Protected, body.RecoveryGeneration);
                        ScheduleSource(body);
                        NormalizeEquipment(seat.PlayerId, tick);
                    }
                }
            }
        }

        void NormalizeEquipment(string playerId, long tick)
        {
            var actor = actors[playerId];
            int index = indices[playerId];
            var inventory = economy.Player(index);
            int slot = actor.EquippedSlot;
            var item = slot < 0 ? null : Held(index, slot);
            if (slot >= 0 && (item == null || inventory.CarriedFurniture >= 0)) slot = -1;
            string weaponId = slot < 0 ? "" : "item:" + item.Id;
            var player = progress.Player(playerId);
            bool cancel = !inventory.CanAct || inventory.Craft != null || inventory.CarriedFurniture >= 0 ||
                actor.Health <= 0 || actor.StunnedUntilTick > tick || player.Returned ||
                actor.Attack != null && actor.Attack.WeaponEntityId != weaponId;
            actors[playerId] = Actor(actor, equippedSlot: slot, clearAttack: cancel);
        }

        bool ApplyDamage(HostDamage damage)
        {
            if (!bodies.TryGetValue(damage.BodyId, out var body) || !body.Active) return false;
            bool hazard = damage.ActorId == "";
            int attacker = -1;
            if (hazard ? !Mode.HazardDamage :
                !indices.TryGetValue(damage.ActorId, out attacker) || !economy.Player(attacker).CanAct) return false;
            if (!hazard && (!world.HasLineOfSight(damage.ActorId, bodies["player:" + attacker].Position, body.Position) ||
                bodies["player:" + attacker].Position.DistanceSquared(body.Position) > 1600)) return false;
            if (body.Kind == WorldBodyKind.Item)
            {
                if (!hazard && !Mode.CanInteract(Binding.Seats[attacker].Team, body.OwnerTeam, body.Protected)) return false;
                var item = economy.Item(Number(body.Id));
                if (item == null || item.IsGone) return false;
                var tiles = economy.Damage(item.Id, damage.Amount);
                if (item.IsGone)
                {
                    bodies[body.Id] = body.At(body.Position, active: false);
                    AddTiles(tiles, body.Position, body.OwnerTeam, body.SourceId, body.Protected, body.RecoveryGeneration);
                    ScheduleSource(body);
                }
                return true;
            }
            else if (body.Kind == WorldBodyKind.Player)
            {
                var victim = progress.Player(body.SourceId);
                if (victim.Eliminated || !victim.Connected || victim.ProtectedUntil > Tick + 1 ||
                    !hazard && !Mode.CanDamage(Binding.Seats[attacker].Team, victim.Team)) return false;
                var actor = actors[victim.Id];
                if (actor.DodgeUntilTick > Tick + 1 || pending.Any(e =>
                    (e.Kind == ProgressEventKind.Fell && e.PlayerId == victim.Id) ||
                    (e.Kind == ProgressEventKind.Knockout && e.Target == victim.Id))) return false;
                float health = Math.Max(0, actor.Health - damage.Amount);
                bool stagger = damage.StunTicks > 0 && actor.StaggerImmuneUntilTick <= Tick + 1;
                int stun = Math.Min(damage.StunTicks, WordboundCombatPolicy.Ticks(rules.HitStunMax));
                actors[victim.Id] = Actor(actor, health: health,
                    stun: stagger ? Tick + 1 + stun : actor.StunnedUntilTick,
                    immune: stagger ? Tick + 1 + WordboundCombatPolicy.Ticks(rules.StaggerImmunitySeconds) : actor.StaggerImmuneUntilTick,
                    clearAttack: stagger || health == 0);
                if (stagger) economy.CancelCraft(indices[victim.Id]);
                if (health == 0) pending.Add(hazard ?
                    new ProgressEvent(ProgressEventKind.Fell, victim.Id) :
                    new ProgressEvent(ProgressEventKind.Knockout, damage.ActorId, victim.Id));
                return true;
            }
            return false;
        }

        void ObserveProgress(ProgressEvent observation)
        {
            if (!indices.TryGetValue(observation.PlayerId ?? "", out int index)) return;
            if (observation.Kind == ProgressEventKind.Fell) { pending.Add(observation); return; }
            if (!Mode.IsRace) return;
            var team = progress.Team(Binding.Seats[index].Team);
            if (team.Finished || observation.Lap != team.Lap) return;
            var lap = course.Laps[team.Lap];
            string module = observation.Kind == ProgressEventKind.FinishReached && observation.Target == lap.FinishModuleId ?
                lap.FinishModuleId : observation.Kind == ProgressEventKind.CheckpointReached ?
                    lap.Checkpoints.FirstOrDefault(c => c.Id == observation.Target)?.ModuleId : null;
            if (module != null && InModule(bodies["player:" + index].Position, module)) pending.Add(observation);
        }

        void ApplyMechanism(HostMechanismObservation observation)
        {
            string key = Key(observation.PuzzleId, observation.Team, observation.Lap);
            if (!Mode.IsRace || !mechanisms.TryGetValue(key, out var previous))
                throw new ArgumentException("Mechanism observation is not bound to this course and team.");
            if (previous.Solved) return;
            var team = progress.Team(observation.Team);
            if (team.Finished || team.Lap != observation.Lap || team.NextCheckpoint >= course.Laps[team.Lap].Checkpoints.Count ||
                !course.Laps[team.Lap].Checkpoints[team.NextCheckpoint].PuzzleIds.Contains(observation.PuzzleId))
                throw new ArgumentException("Mechanism observation is outside the active objective.");
            if (!observation.Active)
            {
                if (observation.PhaseTick != 0 || observation.ProgressTicks != 0 || observation.ItemEntityId != "")
                    throw new ArgumentException("Inactive mechanisms must reset their phase and progress.");
                mechanisms[key] = new MatchMechanismState(observation.PuzzleId, observation.Team, observation.Lap);
                return;
            }
            long nextTick = Tick + 1;
            if (observation.PhaseTick < 1 || observation.PhaseTick > nextTick ||
                observation.ProgressTicks > nextTick - observation.PhaseTick + 1)
                throw new ArgumentException("Mechanism timing is not causal.");
            bool newPhase = previous.ActivatedTick < 0 || previous.PhaseTick != observation.PhaseTick ||
                previous.ItemEntityId != observation.ItemEntityId;
            if (newPhase ? observation.PhaseTick != nextTick || observation.ProgressTicks > 1 :
                observation.ProgressTicks < previous.ProgressTicks)
                throw new ArgumentException("Mechanism timing regressed or changed its item without a new phase.");
            var puzzle = course.Puzzles.Single(p => p.Id == observation.PuzzleId);
            if (puzzle.PrerequisiteIds.Any(id => !team.CompletedPuzzles.Contains(id)))
                throw new ArgumentException("Mechanism cannot start before its puzzle prerequisites.");
            if (observation.ItemEntityId != "")
            {
                if (!bodies.TryGetValue(observation.ItemEntityId, out var body) || body.Kind != WorldBodyKind.Item ||
                    !body.Active || body.OwnerTeam != observation.Team || !InModule(body.Position, puzzle.ModuleId))
                    throw new ArgumentException("Mechanism item is missing, inaccessible or owned by another team.");
                var item = economy.Item(Number(body.Id));
                if (item == null || item.State != ItemState.Deployed || item.Origin != ItemOrigin.Crafted ||
                    item.Word != puzzle.RecipeId) throw new ArgumentException("Mechanism item does not match the puzzle recipe.");
            }
            else if (puzzle.RecipeId != "") throw new ArgumentException("This mechanism requires a deployed recipe item.");
            mechanisms[key] = new MatchMechanismState(observation.PuzzleId, observation.Team, observation.Lap, false,
                newPhase ? nextTick : previous.ActivatedTick, observation.PhaseTick,
                observation.ProgressTicks, observation.ItemEntityId);
        }

        void Synchronize(MatchSnapshot before)
        {
            foreach (var item in economy.Items.Where(i => i.State != ItemState.Deployed)) item.DeploymentTick = -1;
            foreach (var state in mechanisms.Values.Where(m => !m.Solved && m.ItemEntityId != "").ToArray())
            {
                var puzzle = course.Puzzles.Single(p => p.Id == state.PuzzleId);
                var item = economy.Item(Number(state.ItemEntityId));
                if (!bodies.TryGetValue(state.ItemEntityId, out var itemBody) || !itemBody.Active ||
                    item == null || item.State != ItemState.Deployed || !InModule(itemBody.Position, puzzle.ModuleId))
                    mechanisms[Key(state.PuzzleId, state.Team, state.Lap)] = new MatchMechanismState(state.PuzzleId, state.Team, state.Lap);
            }
            foreach (var seat in Binding.Seats)
            {
                int index = indices[seat.PlayerId];
                var current = progress.Player(seat.PlayerId);
                var previous = before?.Progress.Players.Single(p => p.Id == seat.PlayerId);
                var body = bodies["player:" + index];
                if (current.Eliminated && previous != null && !previous.Eliminated)
                {
                    var dropped = new List<ItemInstance>();
                    var loose = economy.Eliminate(index, dropped);
                    WorldVector safe = RecoveryPosition(seat);
                    AddTiles(loose, safe, Mode.IsRace ? seat.Team : -1);
                    foreach (var item in dropped) Release(Outcome.Done(item), safe, default, false);
                    actors[seat.PlayerId] = Actor(actors[seat.PlayerId], health: 0, stun: 0);
                }
                if (previous != null && current.RecoveryGeneration > previous.RecoveryGeneration)
                {
                    body = body.At(RecoveryPosition(seat), active: current.Connected);
                    actors[seat.PlayerId] = Actor(actors[seat.PlayerId], health: rules.MaxHealth, stamina: rules.MaxStamina, stun: 0);
                }
                bodies[body.Id] = body.At(body.Position, body.Velocity, current.Connected && !current.Eliminated);
                var actor = actors[seat.PlayerId];
                actors[seat.PlayerId] = Actor(actor, stamina: Math.Min(rules.MaxStamina, actor.Stamina + rules.StaminaRegen / ModeDefinition.TickRate));
            }
            UpdateKeepsakes();
            foreach (var team in progress.Capture().Teams)
                if (Mode.IsRace)
                {
                    narrative.Add(MatchStateValidation.NarrativeFlag(team.Team, Math.Min(team.Lap, 2)));
                    foreach (var puzzle in course.Puzzles)
                        for (int lap = 0; lap < 3; lap++)
                        {
                            string key = Key(puzzle.Id, team.Team, lap);
                            var state = mechanisms[key];
                            bool solved = lap < team.Lap ? course.Laps[lap].Checkpoints.Any(c => c.PuzzleIds.Contains(puzzle.Id)) :
                                lap == team.Lap && team.CompletedPuzzles.Contains(puzzle.Id);
                            if (solved != state.Solved)
                                mechanisms[key] = new MatchMechanismState(puzzle.Id, team.Team, lap, solved,
                                    solved ? Tick : -1, solved ? Tick : 0, solved ? 1 : 0, state.ItemEntityId);
                        }
                }
            foreach (var state in resources.Values.ToArray())
            {
                int currentLap = CurrentLap(state.Team);
                if (state.Lap != currentLap)
                {
                    if (state.RecoveryAtTick >= 0)
                        resources[Key(state.SourceId, state.Team, state.Lap)] =
                            new MatchResourceState(state.SourceId, state.Team, state.Lap, state.Generation);
                    continue;
                }
                if (!SourceAvailable(state)) continue;
                bool newLap = before != null && Mode.IsRace &&
                    Math.Min(before.Progress.Teams.Single(t => t.Team == state.Team).Lap, 2) < state.Lap;
                if (newLap) MintSource(state);
                else if (state.RecoveryAtTick >= 0 && state.RecoveryAtTick <= Tick)
                {
                    var next = new MatchResourceState(state.SourceId, state.Team, state.Lap, checked(state.Generation + 3));
                    resources[Key(state.SourceId, state.Team, state.Lap)] = next;
                    RecoverSource(state, next);
                }
                else if (!HasSourceGeneration(state) && state.RecoveryAtTick == -1 &&
                    SourceAfterPuzzles(state).Any() && !WasSourceAvailable(before, state)) MintSource(state);
            }
            foreach (var body in bodies.Values.Where(b => b.Active && b.Kind != WorldBodyKind.Player &&
                b.Kind != WorldBodyKind.Platform && BelowCourse(b.Position)).ToArray())
            {
                WorldVector safe = body.SourceId != "" && SourcePosition(body.SourceId, out var sourcePosition) ?
                    sourcePosition : Position(course.RecoveryAnchors[0].Position);
                bodies[body.Id] = body.At(safe);
            }
            SetPermissions(Tick);
        }

        void UpdateKeepsakes()
        {
            if (!Mode.IsRace) return;
            foreach (int teamId in Binding.Seats.Select(s => s.Team).Distinct())
            {
                var team = progress.Team(teamId);
                var body = bodies["keepsake:" + teamId];
                bool carried = team.KeepsakeCarrier != null;
                WorldVector at = carried ? bodies["player:" + indices[team.KeepsakeCarrier]].Position :
                    team.RecoveryGeneration != body.RecoveryGeneration ? Landmark(teamId, team.SafeCheckpoint) : body.Position;
                bodies[body.Id] = new WorldBodyState(body.Id, body.Kind, at, active: !carried,
                    kinematic: carried, ownerTeam: teamId, protectedResource: true, recoveryGeneration: team.RecoveryGeneration);
            }
        }

        void AddResource(string source, int team)
        {
            int laps = Mode.IsRace ? 3 : 1;
            for (int lap = 0; lap < laps; lap++) resources.Add(Key(source, team, lap), new MatchResourceState(source, team, lap, lap));
        }
        void MintSource(MatchResourceState state)
        {
            if (HasSourceGeneration(state)) return;
            var source = MatchStateValidation.Sources(course)[state.SourceId];
            SourcePosition(state.SourceId, out var at);
            AddTiles(economy.MintTiles(source.Item3), at, state.Team, state.SourceId, source.Item2, state.Generation);
        }
        void RecoverSource(MatchResourceState previous, MatchResourceState next)
        {
            var source = MatchStateValidation.Sources(course)[next.SourceId];
            var remaining = LetterBag.FromWord(source.Item3);
            SourcePosition(next.SourceId, out var at);
            var tiles = economy.Capture().TileStates.ToDictionary(t => t.Key, t => t.Value);
            foreach (var body in bodies.Values.Where(b => b.SourceId == previous.SourceId &&
                b.OwnerTeam == previous.Team && b.RecoveryGeneration == previous.Generation).ToArray())
            {
                bodies[body.Id] = new WorldBodyState(body.Id, body.Kind, at, active: body.Active,
                    ownerTeam: body.OwnerTeam, protectedResource: body.Protected, sourceId: body.SourceId,
                    recoveryGeneration: next.Generation);
                if (body.Kind == WorldBodyKind.Tile && tiles.TryGetValue(Number(body.Id), out char letter))
                    remaining.TryRemove(letter);
            }
            if (!remaining.IsEmpty) AddTiles(economy.MintTiles(remaining.ToString()), at, next.Team,
                next.SourceId, source.Item2, next.Generation);
        }
        bool HasSourceGeneration(MatchResourceState state) => bodies.Values.Any(b => b.Active && b.SourceId == state.SourceId &&
            b.OwnerTeam == state.Team && b.RecoveryGeneration == state.Generation);
        void ScheduleSource(WorldBodyState body)
        {
            var supply = course.Supplies.FirstOrDefault(s => s.Id == body.SourceId);
            if (supply == null || !supply.Renewable) return;
            var state = resources.Values.FirstOrDefault(s => s.SourceId == body.SourceId && s.Team == body.OwnerTeam &&
                s.Generation == body.RecoveryGeneration);
            if (state != null && state.Lap == CurrentLap(state.Team) && state.RecoveryAtTick == -1)
                resources[Key(state.SourceId, state.Team, state.Lap)] = new MatchResourceState(state.SourceId, state.Team, state.Lap,
                    state.Generation, Tick + 1 + RecoveryTicks);
        }
        IEnumerable<string> SourceAfterPuzzles(MatchResourceState state) =>
            course.Supplies.FirstOrDefault(s => s.Id == state.SourceId)?.AfterPuzzleIds ?? Array.Empty<string>();
        bool SourceAvailable(MatchResourceState state) => SourceAfterPuzzles(state).All(id =>
            Mode.IsRace && progress.Team(state.Team).CompletedPuzzles.Contains(id));
        bool WasSourceAvailable(MatchSnapshot before, MatchResourceState state) => before != null && SourceAfterPuzzles(state).All(id =>
            Mode.IsRace && before.Progress.Teams.Single(t => t.Team == state.Team).CompletedPuzzles.Contains(id));
        int CurrentLap(int team) => Mode.IsRace && team >= 0 ? Math.Min(progress.Team(team).Lap, 2) : 0;
        bool SourcePosition(string id, out WorldVector position)
        {
            var supply = course.Supplies.FirstOrDefault(s => s.Id == id);
            var placement = course.Placements.FirstOrDefault(p => p.Id == id);
            position = supply != null ? Position(supply.Position) : placement != null ? Position(placement.Position) : default;
            return supply != null || placement != null;
        }

        void AddTiles(TileBatch batch, WorldVector at, int team, string source = "", bool protectedResource = false, int generation = 0)
        {
            foreach (var tile in batch.Tiles)
                AddBody(new WorldBodyState("tile:" + tile.TileId, WorldBodyKind.Tile, at, ownerTeam: team,
                    protectedResource: protectedResource, sourceId: source, recoveryGeneration: generation));
        }
        void AddCraftedBody(ItemInstance item, WordboundSeat seat) =>
            AddBody(new WorldBodyState("item:" + item.Id, WorldBodyKind.Item, bodies["player:" + indices[seat.PlayerId]].Position,
                active: false, kinematic: true, ownerTeam: Mode.IsRace ? seat.Team : -1));
        void AddBody(WorldBodyState body) { bodies.Add(body.Id, body); nextWorldEntityId = checked(nextWorldEntityId + 1); }
        void Release(Outcome outcome, WorldVector at, WorldVector velocity, bool kinematic, WorldBodyState orientation = null)
        {
            if (outcome.Ok && outcome.Item != null)
            {
                if (outcome.Item.State != ItemState.Deployed) outcome.Item.DeploymentTick = -1;
                string id = "item:" + outcome.Item.Id;
                var body = bodies[id];
                bodies[id] = orientation == null ? body.At(at, velocity, true, kinematic) :
                    new WorldBodyState(body.Id, body.Kind, at, orientation.RotationXyz, orientation.RotationW,
                        velocity, active: true, kinematic: kinematic, ownerTeam: body.OwnerTeam, protectedResource: body.Protected,
                        sourceId: body.SourceId, recoveryGeneration: body.RecoveryGeneration);
            }
        }
        void ExpireDeployments(long nextTick)
        {
            foreach (var item in economy.Items.Where(i => i.State == ItemState.Deployed).OrderBy(i => i.Id).ToArray())
            {
                long deadline = MatchStateValidation.DeploymentExpiryTick(catalogue.Get(item.Word), item.DeploymentTick);
                if (deadline < 0 || deadline > nextTick) continue;
                var body = bodies["item:" + item.Id];
                var tiles = economy.Break(item.Id);
                item.DeploymentTick = -1;
                bodies[body.Id] = body.At(body.Position, active: false, kinematic: true);
                AddTiles(tiles, body.Position, body.OwnerTeam, body.SourceId, body.Protected, body.RecoveryGeneration);
                ScheduleSource(body);
            }
        }
        ItemInstance Held(int index, int slot)
        {
            var slots = economy.Player(index).Slots;
            return slot >= 0 && slot < slots.Length ? economy.Item(slots[slot]) : null;
        }
        bool ReleasePosition(int index, int slot, WorldBodyState actor, bool throwing, out WorldVector at, out string refusal)
        {
            at = default; refusal = "EmptySlot";
            var item = slot < 0 ? economy.Item(economy.Player(index).CarriedFurniture) : Held(index, slot);
            if (item == null || item.HolderId != index || item.State != ItemState.Held && item.State != ItemState.Carried) return false;
            refusal = "BlockedRelease";
            return world is IWordboundReleaseQuery release &&
                release.TryRelease(actor, bodies["item:" + item.Id], throwing, out at) &&
                Bounded(at, 10000) && actor.Position.DistanceSquared(at) <= 144;
        }
        bool Reachable(WordboundSeat seat, WorldBodyState origin, string id, WorldBodyKind kind, out WorldBodyState body) =>
            bodies.TryGetValue(id, out body) && body.Kind == kind && body.Active &&
            Mode.CanInteract(seat.Team, body.OwnerTeam, body.Protected) && WithinReach(seat.PlayerId, origin.Position, body.Position);
        bool WithinReach(string actor, WorldVector from, WorldVector to) =>
            from.DistanceSquared(to) <= InteractionRange * InteractionRange && world.HasLineOfSight(actor, from, to);
        bool InModule(WorldVector position, string moduleId)
        {
            var bounds = course.Geometry.Single(m => m.Id == moduleId).Bounds;
            return position.X >= bounds.Min.X / 1000f && position.X <= bounds.Max.X / 1000f &&
                position.Y >= bounds.Min.Y / 1000f - .5f && position.Y <= bounds.Max.Y / 1000f + .5f &&
                position.Z >= bounds.Min.Z / 1000f && position.Z <= bounds.Max.Z / 1000f;
        }
        bool BelowCourse(WorldVector position) => position.Y < course.Geometry.Min(m => m.Position.Y) / 1000f - 20;
        WorldVector Spawn(int index)
        {
            var spawns = course.Spawns.Where(s => s.Kind == "Start").ToArray();
            return Position(spawns[index % spawns.Length].Position);
        }
        WorldVector RecoveryPosition(WordboundSeat seat) => Mode.IsRace ?
            Landmark(seat.Team, progress.Team(seat.Team).SafeCheckpoint) : Spawn(indices[seat.PlayerId]);
        WorldVector Landmark(int team, string id)
        {
            var checkpoint = course.Laps.SelectMany(l => l.Checkpoints).FirstOrDefault(c => c.Id == id);
            var anchor = checkpoint == null ? course.RecoveryAnchors.FirstOrDefault(a => a.ModuleId == id) :
                course.RecoveryAnchors.Single(a => a.Id == checkpoint.RecoveryAnchorId);
            if (anchor != null) return Position(anchor.Position);
            return Position(course.Geometry.Single(m => m.Id == id).Position);
        }
        void ApplyPose(WorldBodyState pose)
        {
            if (!bodies.TryGetValue(pose.Id, out var previous) || pose.Kind != previous.Kind ||
                !Bounded(pose.Position, 10000) || !Bounded(pose.Velocity, 1000) || !Bounded(pose.AngularVelocity, 10000))
                throw new ArgumentException("Unbound or out-of-range host pose.");
            if (!previous.Active) return;
            bodies[pose.Id] = new WorldBodyState(previous.Id, previous.Kind, pose.Position, pose.RotationXyz, pose.RotationW,
                pose.Velocity, pose.AngularVelocity, pose.Sleeping, previous.Kinematic, previous.Active, previous.OwnerTeam,
                previous.Protected, previous.SourceId, previous.RecoveryGeneration);
            if (previous.Kind == WorldBodyKind.Item && pose.Sleeping)
            {
                var item = economy.Item(Number(previous.Id));
                if (item != null && item.State == ItemState.Thrown) economy.Settle(item.Id);
            }
        }
        void SetPermissions(long tick)
        {
            foreach (var seat in Binding.Seats)
            {
                var player = progress.Player(seat.PlayerId);
                economy.Player(indices[seat.PlayerId]).CanAct = player.Connected && !player.Eliminated && !player.Returned &&
                    !progress.Migrating && progress.Result == null && actors[seat.PlayerId].StunnedUntilTick <= tick;
                var body = bodies["player:" + indices[seat.PlayerId]];
                bodies[body.Id] = body.At(body.Position, body.Velocity, player.Connected && !player.Eliminated);
                NormalizeEquipment(seat.PlayerId, tick);
            }
        }
        void RestoreState(MatchSnapshot snapshot)
        {
            economy.Restore(snapshot.Economy); progress.Restore(snapshot.Progress); journal.Restore(snapshot.Commands);
            Replace(actors, snapshot.Actors, a => a.PlayerId);
            Replace(bodies, snapshot.Bodies, b => b.Id);
            Replace(resources, snapshot.Resources, r => Key(r.SourceId, r.Team, r.Lap));
            Replace(mechanisms, snapshot.Mechanisms, m => Key(m.PuzzleId, m.Team, m.Lap));
            Replace(random, snapshot.RandomStreams, r => r.StreamId);
            narrative.Clear(); narrative.UnionWith(snapshot.NarrativeFlags);
            nextWorldEntityId = snapshot.NextWorldEntityId;
        }
        void EnsureValid(MatchSnapshot snapshot)
        {
            string error = MatchStateValidation.Validate(snapshot, Binding, Mode, course, catalogue, rules);
            if (error != "") throw new ArgumentException(error);
            foreach (var body in snapshot.Bodies.Where(b => b.SourceId != ""))
            {
                var placement = course.Placements.FirstOrDefault(p => p.Id == body.SourceId);
                if (placement == null) continue;
                string letters = courseCatalogue.Assets[placement.AssetId].Letters;
                if (body.Kind == WorldBodyKind.Item && snapshot.Economy.ItemStates.Single(i => i.Id == Number(body.Id)).Word != letters ||
                    body.Kind == WorldBodyKind.Tile && letters.IndexOf(snapshot.Economy.TileStates.Single(t => t.Key == Number(body.Id)).Value) < 0)
                    throw new ArgumentException("World prop letters differ from the pinned asset catalogue.");
            }
        }
        void EnsureCapacity()
        {
            var state = economy.Capture();
            if (bodies.Count > 8192 || state.ItemStates.Length > 8192 || state.TileStates.Length > 8192)
                throw new InvalidOperationException("Match entity capacity exceeded.");
        }
        static void Replace<T>(Dictionary<string, T> destination, IEnumerable<T> values, Func<T, string> key)
        { destination.Clear(); foreach (var value in values) destination.Add(key(value), value); }
        static T[] Bounded<T>(IEnumerable<T> values, int maximum) where T : class
        {
            var array = values?.Take(maximum + 1).ToArray() ?? Array.Empty<T>();
            if (array.Length > maximum || array.Any(v => v == null)) throw new ArgumentException("Host frame exceeds bounds.");
            return array;
        }
        static MatchActorState Actor(MatchActorState previous, float? health = null, float? stamina = null,
            long? stun = null, long? lastInput = null, int? equippedSlot = null, MatchAttackState attack = null,
            bool clearAttack = false, long? nextAttack = null, long? immune = null) =>
            new MatchActorState(previous.PlayerId, health ?? previous.Health, stamina ?? previous.Stamina,
                nextAttack ?? previous.NextAttackTick, stun ?? previous.StunnedUntilTick, previous.DodgeUntilTick,
                lastInput ?? previous.LastInputSequence, previous.Appearance, equippedSlot ?? previous.EquippedSlot,
                clearAttack ? null : attack ?? previous.Attack, immune ?? previous.StaggerImmuneUntilTick);
        static string Key(string id, int team, int lap) => MatchStateValidation.ResourceKey(id, team, lap);
        static int Number(string id) => int.Parse(id.Substring(id.IndexOf(':') + 1), CultureInfo.InvariantCulture);
        static CommandEffect Effect(string code, string entity = "") => new CommandEffect(code, entity);
        static CommandEffect Effect(Outcome outcome) => Effect(outcome.Ok ? "Ok" : outcome.Refusal.ToString(), outcome.Item == null ? "" : "item:" + outcome.Item.Id);
        static WorldVector Position(CoursePoint point) => new WorldVector(point.X / 1000f, point.Y / 1000f, point.Z / 1000f);
        static WorldVector Add(WorldVector a, WorldVector b) => new WorldVector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        static WorldVector Forward(WorldBodyState body, float magnitude)
        {
            var q = body.RotationXyz;
            return new WorldVector(2 * (q.X * q.Z + body.RotationW * q.Y) * magnitude,
                2 * (q.Y * q.Z - body.RotationW * q.X) * magnitude, (1 - 2 * (q.X * q.X + q.Y * q.Y)) * magnitude);
        }
        static bool Bounded(WorldVector value, float bound) =>
            Math.Abs(value.X) <= bound && Math.Abs(value.Y) <= bound && Math.Abs(value.Z) <= bound;
    }
}

namespace Wreckabulary.Rules
{
    public sealed partial class Economy
    {
        internal void CommitWordboundPuzzleLetters(int playerId, string letters)
        {
            var cost = LetterBag.FromWord(letters);
            if (!Player(playerId).Letters.TryRemove(cost)) throw new InvalidOperationException("Puzzle letters are unavailable.");
            Spent = checked(Spent + letters.Length);
        }

        internal void CommitWordboundPuzzleItem(int itemId)
        {
            var item = Item(itemId);
            if (item == null || item.IsGone || item.State != ItemState.Deployed)
                throw new InvalidOperationException("Puzzle item must be deployed.");
            Spend(item);
            Remove(item);
        }
    }
}
