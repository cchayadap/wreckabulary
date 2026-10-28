using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Profiling;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;

namespace Wreckabulary.Online
{
    public sealed class WordboundPhysicsWorld : IWordboundWorldQuery, IWordboundReleaseQuery, IDisposable
    {
        static readonly ProfilerMarker ApplyMarker = new ProfilerMarker(ProfilerCategory.Scripts, "WB.Match.PhysicsApply");
        readonly NetworkRunner runner;
        readonly RunnerSimulatePhysics simulator;
        readonly PhysicsScene scene;
        readonly ModeDefinition mode;
        readonly ResolvedCourse course;
        readonly ItemCatalogue items;
        readonly Func<PlayerRef, string> admittedActor;
        readonly Func<bool> canAdvance;
        readonly Func<string, ResolvedCoursePuzzle, WorldBodyState, bool> puzzleEvaluator;
        readonly Func<string, WordboundMovementEnvironment> movementEnvironment;
        readonly Dictionary<string, WordboundNetworkBody> bodies = new Dictionary<string, WordboundNetworkBody>(StringComparer.Ordinal);
        readonly Dictionary<Collider, WordboundNetworkBody> owned = new Dictionary<Collider, WordboundNetworkBody>();
        readonly HashSet<Collider> structure = new HashSet<Collider>();
        readonly RaycastHit[] hits = new RaycastHit[128];
        readonly Collider[] overlaps = new Collider[128];
        readonly WordboundThrownContactBuffer thrownContacts = new WordboundThrownContactBuffer();
        Exception contactFailure;
        MatchSnapshot frame;
        long appliedRevision;
        long simulationRevision;
        bool simulating;
        bool failed;
        bool disposed;
        bool collisionPairsDirty;
        bool reconciling;
        bool pendingComposition;
        bool registeringBatch;
        Dictionary<string, CollisionRegistration> collisionBatch;

        sealed class CollisionRegistration
        {
            public readonly WordboundNetworkBody Body;
            public readonly WorldBodyState State;
            readonly Collider[] colliders;
            public CollisionRegistration(WordboundNetworkBody body)
            { Body = body; State = body.Canonical; colliders = body.OwnedColliders.ToArray(); }
            public bool Matches(WordboundNetworkBody body)
            {
                if (!ReferenceEquals(Body, body) || !ReferenceEquals(State, body.Canonical) ||
                    colliders.Length != body.OwnedColliders.Count) return false;
                for (int i = 0; i < colliders.Length; i++)
                    if (!ReferenceEquals(colliders[i], body.OwnedColliders[i])) return false;
                return true;
            }
        }

#if UNITY_EDITOR || GSK_QA
        public long CollisionPairFullPasses { get; private set; }
        public long CollisionPairIncrementalPasses { get; private set; }
        public long CollisionPairDecisions { get; private set; }
#endif

        public MatchBinding Binding { get; }
        public GameRules Rules { get; }
        public long Epoch => frame?.Epoch ?? -1;
        public bool IsHost => !disposed && runner != null && runner.IsRunning && runner.IsServer;
        public bool ShouldFreeze => !IsHost || frame == null || failed || pendingComposition || !canAdvance() ||
            frame.Progress.Migrating || frame.Progress.Result != null || HostStepCompleted == null;
        public PhysicsScene PhysicsScene => scene;
        public IReadOnlyList<HostThrownContact> CompletedThrownContacts =>
            thrownContacts.Read(Epoch, checked(frame.Progress.Tick + 1));
        public event Action<IReadOnlyList<WorldBodyState>> HostStepCompleted;
        public event Action<Exception> MovementFaulted;

        public WordboundPhysicsWorld(NetworkRunner runner, MatchBinding binding, ModeDefinition mode,
            ResolvedCourse course, ItemCatalogue itemCatalogue, GameRules rules,
            Func<PlayerRef, string> admittedActor, Func<bool> canAdvance,
            Func<string, ResolvedCoursePuzzle, WorldBodyState, bool> puzzleEvaluator = null,
            Func<string, WordboundMovementEnvironment> movementEnvironment = null)
        {
            this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
            Binding = binding ?? throw new ArgumentNullException(nameof(binding));
            this.mode = mode ?? throw new ArgumentNullException(nameof(mode));
            this.course = course ?? throw new ArgumentNullException(nameof(course));
            items = itemCatalogue ?? throw new ArgumentNullException(nameof(itemCatalogue));
            Rules = rules?.Clone() ?? throw new ArgumentNullException(nameof(rules));
            this.admittedActor = admittedActor ?? throw new ArgumentNullException(nameof(admittedActor));
            this.canAdvance = canAdvance ?? throw new ArgumentNullException(nameof(canAdvance));
            this.puzzleEvaluator = puzzleEvaluator;
            this.movementEnvironment = movementEnvironment;
            if (binding.ModeHash != mode.Hash || binding.ManifestHash != course.Hash || binding.CourseHash != course.SourceHash)
                throw new ArgumentException("Physics world content differs from the canonical match.");
            if (!runner.IsRunning || !runner.IsServer || runner.SceneManager == null ||
                !runner.SceneManager.TryGetPhysicsScene3D(out scene) || !scene.IsValid() ||
                scene == UnityEngine.Physics.defaultPhysicsScene)
                throw new InvalidOperationException("Host physics requires the runner's isolated 3D physics scene.");
            simulator = runner.GetComponent<RunnerSimulatePhysics>();
            if (simulator == null) throw new InvalidOperationException("Install RunnerSimulatePhysics before binding the physics world.");
            simulator.OnBeforeSimulate += BeforeSimulate;
            simulator.OnAfterSimulate += AfterSimulate;
        }

        public string AdmittedActor(PlayerRef player) => IsHost && player != PlayerRef.None ? admittedActor(player) : null;

        public void RegisterStatic(WordboundCourseWorld world)
        {
            if (world == null || world.ManifestHash != course.Hash) throw new ArgumentException("Static world uses a different manifest.");
            foreach (var collider in world.Surfaces)
                if (collider == null || collider.gameObject.scene.GetPhysicsScene() != scene)
                    throw new ArgumentException("Every static collider must belong to this runner's physics scene.");
            foreach (var collider in world.Surfaces) structure.Add(collider);
        }

        public void Register(WordboundNetworkBody body, WorldBodyState canonical, PlayerRef inputAuthority)
        {
            if (!IsHost || canonical == null || body == null || body.Runner != runner ||
                bodies.Count >= WordboundEntityBudget.MaximumBodies || bodies.ContainsKey(canonical.Id) ||
                body.gameObject.scene.GetPhysicsScene() != scene)
                throw new ArgumentException("Body is duplicated or belongs to a different runner.");
            body.BindHost(this, canonical, inputAuthority);
            bodies.Add(canonical.Id, body);
            if (reconciling) { pendingComposition = true; registeringBatch = true; }
            collisionPairsDirty = true;
            foreach (var collider in body.OwnedColliders) owned.Add(collider, body);
            if (reconciling && collisionBatch == null && bodies.Count == 1)
                collisionBatch = new Dictionary<string, CollisionRegistration>(StringComparer.Ordinal);
            if (!reconciling || !RefreshAddedCollisionPairs(body)) RefreshCollisionPairs(true);
        }

        public void Unregister(WordboundNetworkBody body)
        {
            if (body == null || body.Canonical == null) return;
            bool removed = bodies.Remove(body.Canonical.Id);
            if (removed && reconciling) pendingComposition = true;
            foreach (var collider in body.OwnedColliders) owned.Remove(collider);
            collisionPairsDirty = true;
        }

        public void ApplySnapshot(MatchSnapshot snapshot)
        {
            if (reconciling) throw new InvalidOperationException("Physics restoration cannot reenter reconciliation.");
            if (!IsHost) throw new InvalidOperationException("Only the active host restores physics.");
            string error = MatchStateValidation.Validate(snapshot, Binding, mode, course, items, Rules);
            if (error != "") throw new ArgumentException(error);
            ApplyValidatedSnapshot(snapshot);
        }

        public bool ReconcileAndApplySnapshot(MatchSnapshot snapshot, GameRules initialRules, Func<bool> reconcile)
        {
            if (reconciling) throw new InvalidOperationException("Physics restoration cannot reenter reconciliation.");
            if (!IsHost) throw new InvalidOperationException("Only the active host restores physics.");
            if (reconcile == null) throw new ArgumentNullException(nameof(reconcile));
            long revision = appliedRevision;
            reconciling = true;
            Exception failure = null;
            try
            {
                return MatchStateValidation.ReconcileAndApply(snapshot, Binding, mode, course, items, initialRules, Rules,
                    () => reconcile() && IsHost && !failed && appliedRevision == revision,
                    () => ApplyValidatedSnapshot(snapshot));
            }
            catch (Exception error) { failure = error; throw; }
            finally
            {
                try
                {
                    if (pendingComposition && collisionPairsDirty && IsHost) RefreshCollisionPairs();
                }
                catch when (failure != null) { failed = true; }
                finally { collisionBatch = null; registeringBatch = false; reconciling = false; }
            }
        }

        void ApplyValidatedSnapshot(MatchSnapshot snapshot)
        {
            using var scope = ApplyMarker.Auto();
            if (frame != null && (snapshot.Epoch < frame.Epoch ||
                snapshot.Epoch == frame.Epoch && snapshot.Progress.Tick < frame.Progress.Tick))
                throw new ArgumentException("Physics restoration cannot rewind within the same authority epoch.");
            var states = snapshot.Bodies.ToDictionary(b => b.Id, StringComparer.Ordinal);
            if (pendingComposition && states.Count != bodies.Count)
                throw new ArgumentException("Complete the pending body registry before applying a snapshot.");
            var itemStates = snapshot.Economy.Items.ToDictionary(i => "item:" + i.Id);
            foreach (var state in states.Values)
                if (state.Kind == WorldBodyKind.Item) WordboundPhysicsPolicy.ValidateItemMode(state, itemStates[state.Id].State);
            foreach (var pair in bodies)
            {
                if (pair.Value == null || !states.TryGetValue(pair.Key, out var next))
                    throw new ArgumentException("Reconcile despawned bodies before applying this snapshot.");
                WordboundPhysicsPolicy.ValidateUpdate(pair.Value.Canonical, next);
                if (WordboundPhysicsPolicy.Actor(next, Binding) != pair.Value.ActorId.ToString())
                    throw new ArgumentException("Snapshot changes a registered body's authenticated seat.");
            }
            frame = snapshot;
            appliedRevision++;
            foreach (var pair in bodies)
            {
                var current = pair.Value.Canonical;
                var next = states[pair.Key];
                collisionPairsDirty |= current.Active != next.Active || current.OwnerTeam != next.OwnerTeam || current.Protected != next.Protected;
                pair.Value.ApplyCanonical(next);
                pair.Value.SetItemMode(itemStates.TryGetValue(pair.Key, out var item) ? item.State : (ItemState?)null);
            }
            RefreshCollisionPairs();
            pendingComposition = false;
        }

        public IReadOnlyList<WorldBodyState> CaptureHost()
        {
            if (!IsHost) throw new InvalidOperationException("Only the host captures physics.");
            return bodies.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value.CaptureHost()).ToArray();
        }

        public bool CanMove(string actor, out float scale)
        {
            scale = 1;
            if (ShouldFreeze) return false;
            var player = frame.Progress.Players.SingleOrDefault(p => p.Id == actor);
            var stats = frame.Actors.SingleOrDefault(a => a.PlayerId == actor);
            int index = SeatIndex(actor);
            if (player == null || stats == null || index < 0 || !player.Connected || player.Returned || player.Eliminated ||
                stats.Health <= 0 || stats.StunnedUntilTick > frame.Progress.Tick) return false;
            var inventory = frame.Economy.Players.Single(p => p.PlayerId == index);
            if (!inventory.CanAct) return false;
            scale = inventory.Craft == null ? 1 : Rules.CraftMoveSpeed;
            if (stats.Defense != null)
            {
                var shield = frame.Economy.Items.Single(i => "item:" + i.Id == stats.Defense.ItemEntityId);
                scale *= WordboundDefensePolicy.MoveScale(stats.Defense, items.Get(shield.Word).Shield, frame.Progress.Tick);
            }
            return true;
        }

        internal bool TryGround(WordboundNetworkBody actor, out Vector3 normal)
        {
            normal = Vector3.up;
            Vector3 origin = actor.Rigidbody.position + Vector3.up * .4f;
            int count = scene.SphereCast(origin, .28f, Vector3.down, hits, .18f, ~0, QueryTriggerInteraction.Ignore);
            if (count >= hits.Length) return false;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (!Blocks(actor.Canonical, hit.collider) || owned.TryGetValue(hit.collider, out var owner) && owner == actor ||
                    hit.normal.y < .819f || hit.distance >= nearest) continue;
                nearest = hit.distance; normal = hit.normal;
            }
            return nearest < float.PositiveInfinity;
        }

        internal bool TryMovementEnvironment(string actor, out WordboundMovementEnvironment environment)
        {
            environment = WordboundMovementEnvironment.Neutral;
            if (ShouldFreeze) return false;
            try
            {
                environment = movementEnvironment?.Invoke(actor) ?? WordboundMovementEnvironment.Neutral;
                environment.Validate();
                return true;
            }
            catch (Exception error)
            {
                failed = true;
                foreach (var body in bodies.Values) body.PrepareSimulation(true);
                MovementFaulted?.Invoke(error);
                return false;
            }
        }

        public bool HasLineOfSight(string actorId, WorldVector from, WorldVector to)
        {
            if (!ActorBody(actorId, out var actor)) return false;
            var origin = WordboundNetworkBody.Vector(from) + Vector3.up * .75f;
            var target = WordboundNetworkBody.Vector(to) + Vector3.up * .75f;
            Vector3 delta = target - origin;
            if (delta.sqrMagnitude < .000001f) return true;
            int count = scene.Raycast(origin, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count >= hits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var collider = hits[i].collider;
                if (!Blocks(actor.Canonical, collider)) continue;
                if (owned.TryGetValue(collider, out var body) &&
                    (body == actor || (body.Rigidbody.position - WordboundNetworkBody.Vector(to)).sqrMagnitude < .0001f)) continue;
                return false;
            }
            return true;
        }

        public bool CanDeploy(string actorId, string recipeId, WorldVector position)
        {
            if (!ActorBody(actorId, out var actor) || !items.TryGet(recipeId, out var definition) ||
                !definition.Enabled || !definition.CanDeploy || !mode.Recipes.Contains(recipeId)) return false;
            if (definition.Size == null || definition.Size.Length != 3 || definition.Size.Any(n => n <= 0 || n > 10 || float.IsNaN(n) || float.IsInfinity(n)))
                return false;
            if (!ValidFootprint(definition.Deploy.FootprintX) || !ValidFootprint(definition.Deploy.FootprintZ)) return false;
            Vector3 half = new Vector3(Mathf.Max(definition.Size[0], definition.Deploy.FootprintX), definition.Size[1],
                Mathf.Max(definition.Size[2], definition.Deploy.FootprintZ)) / 2;
            Vector3 foot = WordboundNetworkBody.Vector(position);
            Quaternion orientation = actor.Rigidbody.rotation;
            if (!HasLineOfSight(actorId, WordboundNetworkBody.Value(actor.Rigidbody.position), position)) return false;
            int count = scene.OverlapBox(foot + orientation * new Vector3(0, half.y + .03f, 0),
                new Vector3(Mathf.Max(.01f, half.x - .02f), half.y, Mathf.Max(.01f, half.z - .02f)),
                overlaps, orientation, ~0, QueryTriggerInteraction.Ignore);
            if (count >= overlaps.Length) return false;
            for (int i = 0; i < count; i++)
                if (Blocks(actor.Canonical, overlaps[i])) return false;
            foreach (float x in new[] { -half.x, half.x })
                foreach (float z in new[] { -half.z, half.z })
                    if (!Supported(actor.Canonical, foot + orientation * new Vector3(x, .08f, z))) return false;
            return true;
        }

        public bool TryRelease(WorldBodyState actorState, WorldBodyState itemState, bool throwing, out WorldVector position)
        {
            position = default;
            if (actorState == null || itemState == null || !ActorBody(actorState.SourceId, out var actor) ||
                !CanMove(actorState.SourceId, out _) || !bodies.TryGetValue(itemState.Id, out var item) ||
                item.Canonical.Kind != WorldBodyKind.Item || item.Canonical.Active || !item.Canonical.Kinematic ||
                (actor.Rigidbody.position - WordboundNetworkBody.Vector(actorState.Position)).sqrMagnitude > .0001f)
                return false;
            int index = SeatIndex(actorState.SourceId);
            var inventoryItem = frame.Economy.Items.SingleOrDefault(i => "item:" + i.Id == itemState.Id);
            if (inventoryItem == null || inventoryItem.HolderId != index ||
                inventoryItem.State != ItemState.Held && inventoryItem.State != ItemState.Carried) return false;
            var box = item.GetComponent<BoxCollider>();
            var capsule = actor.GetComponent<CapsuleCollider>();
            if (box == null || capsule == null || box.isTrigger || capsule.isTrigger || capsule.direction != 1 ||
                !owned.TryGetValue(box, out var registeredItem) || registeredItem != item ||
                !item.OwnedColliders.Contains(box) || !ReleaseBodyMatches(box, item.Rigidbody) ||
                capsule.attachedRigidbody != actor.Rigidbody ||
                (box.center - new Vector3(0, box.size.y / 2, 0)).sqrMagnitude > .000001f ||
                (capsule.center - new Vector3(0, capsule.height / 2, 0)).sqrMagnitude > .000001f ||
                (item.transform.lossyScale - Vector3.one).sqrMagnitude > .000001f ||
                (actor.transform.lossyScale - Vector3.one).sqrMagnitude > .000001f) return false;
            var size = WordboundNetworkBody.Value(box.size);
            var candidates = WordboundReleasePolicy.Candidates(actorState, size, capsule.radius, throwing);
            var released = itemState.At(itemState.Position, active: true, kinematic: false);
            Quaternion orientation = actor.Rigidbody.rotation;
            Vector3 half = box.size / 2 + Vector3.one * .015f;
            Vector3 start = actor.Rigidbody.position + Vector3.up * (throwing ? .75f : .08f) + orientation * box.center;
            if (!ReleaseClear(start, half, orientation, released, actor, item, true)) return false;
            foreach (var candidate in candidates)
            {
                Vector3 root = WordboundNetworkBody.Vector(candidate);
                Vector3 end = root + orientation * box.center;
                if (!ReleaseClear(end, half, orientation, released, actor, item, false)) continue;
                Vector3 delta = end - start;
                int count = scene.BoxCast(start, half, delta.normalized, hits, orientation, delta.magnitude, ~0,
                    QueryTriggerInteraction.Ignore);
                if (count >= hits.Length || Enumerable.Range(0, count).Any(i => ReleaseBlocks(hits[i].collider, released, actor, item, true))) continue;
                position = candidate;
                return true;
            }
            return false;
        }

        static bool ReleaseBodyMatches(BoxCollider collider, Rigidbody body)
        {
            if (!collider || !body || collider.gameObject != body.gameObject || collider.GetComponent<Rigidbody>() != body)
                return false;
            var attached = collider.attachedRigidbody;
            return attached == body || attached == null && !collider.enabled && body.isKinematic && !body.detectCollisions;
        }

        bool ReleaseClear(Vector3 center, Vector3 half, Quaternion orientation,
            WorldBodyState released, WordboundNetworkBody actor, WordboundNetworkBody item, bool ignoreActor)
        {
            int count = scene.OverlapBox(center, half, overlaps, orientation, ~0, QueryTriggerInteraction.Ignore);
            return count < overlaps.Length && !Enumerable.Range(0, count).Any(i => ReleaseBlocks(overlaps[i], released, actor, item, ignoreActor));
        }
        bool ReleaseBlocks(Collider collider, WorldBodyState released, WordboundNetworkBody actor, WordboundNetworkBody item, bool ignoreActor) =>
            collider != null && (!owned.TryGetValue(collider, out var owner) || owner != item && (!ignoreActor || owner != actor)) &&
            Blocks(released, collider);

        public bool PuzzleSatisfied(string actorId, ResolvedCoursePuzzle puzzle, WorldBodyState placedItem)
        {
            if (puzzleEvaluator == null || !ActorBody(actorId, out var actor) ||
                !course.Puzzles.Any(p => p.Id == puzzle?.Id && p.ModuleId == puzzle.ModuleId)) return false;
            if (placedItem != null && (!bodies.TryGetValue(placedItem.Id, out var registered) ||
                !registered.Canonical.Active || !mode.CanInteract(actor.Canonical.OwnerTeam, placedItem.OwnerTeam, placedItem.Protected)))
                return false;
            return puzzleEvaluator(actorId, puzzle, placedItem);
        }

        bool Supported(WorldBodyState actor, Vector3 origin)
        {
            int count = scene.Raycast(origin, Vector3.down, hits, .2f, ~0, QueryTriggerInteraction.Ignore);
            return count < hits.Length && Enumerable.Range(0, count).Any(i =>
                Blocks(actor, hits[i].collider) && hits[i].normal.y >= .819f &&
                (!owned.TryGetValue(hits[i].collider, out var body) || body.Canonical.Kind != WorldBodyKind.Player));
        }

        bool ActorBody(string actor, out WordboundNetworkBody body)
        {
            body = null;
            int index = SeatIndex(actor);
            return IsHost && frame != null && index >= 0 && bodies.TryGetValue("player:" + index, out body) &&
                body.Canonical.Active && AdmittedActor(body.Object.InputAuthority) == actor;
        }
        int SeatIndex(string actor)
        {
            for (int i = 0; i < Binding.Seats.Count; i++) if (Binding.Seats[i].PlayerId == actor) return i;
            return -1;
        }
        bool Blocks(WorldBodyState actor, Collider collider) => collider != null &&
            (structure.Contains(collider) || owned.TryGetValue(collider, out var other) &&
                WordboundPhysicsPolicy.ShouldCollide(mode, actor, other.Canonical));
        static bool ValidFootprint(float value) => value > 0 && value <= 10 && !float.IsNaN(value) && !float.IsInfinity(value);

        void RefreshCollisionPairs(bool captureRegistration = false)
        {
            if (!collisionPairsDirty) return;
#if UNITY_EDITOR || GSK_QA
            CollisionPairFullPasses++;
#endif
            var all = bodies.Values.ToArray();
            for (int i = 0; i < all.Length; i++)
                for (int j = i + 1; j < all.Length; j++)
                    ApplyCollisionPair(all[i], all[j]);
            collisionPairsDirty = false;
            if (reconciling && registeringBatch && captureRegistration)
                collisionBatch = bodies.ToDictionary(pair => pair.Key, pair => new CollisionRegistration(pair.Value), StringComparer.Ordinal);
        }

        bool RefreshAddedCollisionPairs(WordboundNetworkBody added)
        {
            if (collisionBatch == null || collisionBatch.Count != bodies.Count - 1) return false;
            foreach (var pair in collisionBatch)
                if (!bodies.TryGetValue(pair.Key, out var body) || !pair.Value.Matches(body)) return false;
            foreach (var body in bodies.Values)
                if (!ReferenceEquals(body, added)) ApplyCollisionPair(body, added);
            collisionBatch.Add(added.Canonical.Id, new CollisionRegistration(added));
#if UNITY_EDITOR || GSK_QA
            CollisionPairIncrementalPasses++;
#endif
            return true;
        }

        void ApplyCollisionPair(WordboundNetworkBody first, WordboundNetworkBody second)
        {
#if UNITY_EDITOR || GSK_QA
            CollisionPairDecisions++;
#endif
            bool ignore = !WordboundPhysicsPolicy.ShouldCollide(mode, first.Canonical, second.Canonical);
            foreach (var a in first.OwnedColliders)
                foreach (var b in second.OwnedColliders) UnityEngine.Physics.IgnoreCollision(a, b, ignore);
        }

        void BeforeSimulate(NetworkRunner current)
        {
            if (disposed || current != runner) return;
            bool freeze = ShouldFreeze;
            simulating = !freeze;
            simulationRevision = appliedRevision;
            if (simulating)
            {
                contactFailure = null;
                thrownContacts.Begin(Epoch, checked(frame.Progress.Tick + 1));
            }
            foreach (var body in bodies.Values) body.PrepareSimulation(freeze);
        }

        internal void RecordThrownContact(WordboundNetworkBody source, Collision collision)
        {
            if (!simulating || !IsHost || source == null || collision == null || collision.contactCount == 0 ||
                source.Canonical == null || !bodies.TryGetValue(source.Canonical.Id, out var registered) || registered != source ||
                !owned.TryGetValue(collision.collider, out var target) || target == source ||
                !WordboundPhysicsPolicy.ShouldCollide(mode, source.Canonical, target.Canonical)) return;
            var flight = frame.ThrownFlights.SingleOrDefault(f => f.ItemBodyId == source.Canonical.Id);
            if (flight == null) return;
            try
            {
                var contact = collision.GetContact(0);
                if (!owned.TryGetValue(contact.thisCollider, out var mine) || mine != source ||
                    !owned.TryGetValue(contact.otherCollider, out var other) || other != target) return;
                thrownContacts.Add(Epoch, new HostThrownContact(flight.ItemBodyId, flight.LaunchRevision, target.Canonical.Id,
                    WordboundNetworkBody.Value(contact.point), collision.relativeVelocity.magnitude, checked(frame.Progress.Tick + 1)));
            }
            catch (Exception error) { contactFailure = error; }
        }

        void AfterSimulate(NetworkRunner current)
        {
            if (disposed || current != runner || !simulating) return;
            simulating = false;
            try
            {
                if (contactFailure != null) throw new InvalidOperationException("Host thrown contact collection failed.", contactFailure);
                thrownContacts.Complete(Epoch, checked(frame.Progress.Tick + 1));
                foreach (var body in bodies.Values) body.AfterSimulation();
                HostStepCompleted?.Invoke(CaptureHost());
                if (appliedRevision == simulationRevision)
                    throw new InvalidOperationException("The host step must apply its resulting canonical snapshot before another physics step.");
            }
            catch
            {
                failed = true;
                foreach (var body in bodies.Values) body.PrepareSimulation(true);
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            foreach (var body in bodies.Values) if (body != null) body.PrepareSimulation(true);
            disposed = true;
            if (simulator != null)
            {
                simulator.OnBeforeSimulate -= BeforeSimulate;
                simulator.OnAfterSimulate -= AfterSimulate;
            }
            bodies.Clear(); owned.Clear(); structure.Clear(); collisionBatch = null; registeringBatch = false;
        }
    }
}
