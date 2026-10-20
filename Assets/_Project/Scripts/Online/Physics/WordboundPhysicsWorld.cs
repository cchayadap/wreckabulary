using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;

namespace Wreckabulary.Online
{
    public sealed class WordboundPhysicsWorld : IWordboundWorldQuery, IDisposable
    {
        readonly NetworkRunner runner;
        readonly RunnerSimulatePhysics simulator;
        readonly PhysicsScene scene;
        readonly ModeDefinition mode;
        readonly ResolvedCourse course;
        readonly ItemCatalogue items;
        readonly Func<PlayerRef, string> admittedActor;
        readonly Func<bool> canAdvance;
        readonly Func<string, ResolvedCoursePuzzle, WorldBodyState, bool> puzzleEvaluator;
        readonly Dictionary<string, WordboundNetworkBody> bodies = new Dictionary<string, WordboundNetworkBody>(StringComparer.Ordinal);
        readonly Dictionary<Collider, WordboundNetworkBody> owned = new Dictionary<Collider, WordboundNetworkBody>();
        readonly HashSet<Collider> structure = new HashSet<Collider>();
        readonly RaycastHit[] hits = new RaycastHit[128];
        readonly Collider[] overlaps = new Collider[128];
        MatchSnapshot frame;
        long appliedRevision;
        long simulationRevision;
        bool simulating;
        bool failed;
        bool disposed;
        bool collisionPairsDirty;

        public MatchBinding Binding { get; }
        public GameRules Rules { get; }
        public long Epoch => frame?.Epoch ?? -1;
        public bool IsHost => !disposed && runner != null && runner.IsRunning && runner.IsServer;
        public bool ShouldFreeze => !IsHost || frame == null || failed || !canAdvance() ||
            frame.Progress.Migrating || frame.Progress.Result != null || HostStepCompleted == null;
        public PhysicsScene PhysicsScene => scene;
        public event Action<IReadOnlyList<WorldBodyState>> HostStepCompleted;

        public WordboundPhysicsWorld(NetworkRunner runner, MatchBinding binding, ModeDefinition mode,
            ResolvedCourse course, ItemCatalogue itemCatalogue, GameRules rules,
            Func<PlayerRef, string> admittedActor, Func<bool> canAdvance,
            Func<string, ResolvedCoursePuzzle, WorldBodyState, bool> puzzleEvaluator = null)
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
            if (!IsHost || canonical == null || body == null || body.Runner != runner || bodies.Count >= 512 || bodies.ContainsKey(canonical.Id) ||
                body.gameObject.scene.GetPhysicsScene() != scene)
                throw new ArgumentException("Body is duplicated or belongs to a different runner.");
            body.BindHost(this, canonical, inputAuthority);
            bodies.Add(canonical.Id, body);
            foreach (var collider in body.OwnedColliders) owned.Add(collider, body);
            collisionPairsDirty = true;
            RefreshCollisionPairs();
        }

        public void Unregister(WordboundNetworkBody body)
        {
            if (body == null || body.Canonical == null) return;
            bodies.Remove(body.Canonical.Id);
            foreach (var collider in body.OwnedColliders) owned.Remove(collider);
            collisionPairsDirty = true;
        }

        public void ApplySnapshot(MatchSnapshot snapshot)
        {
            if (!IsHost) throw new InvalidOperationException("Only the active host restores physics.");
            string error = MatchStateValidation.Validate(snapshot, Binding, mode, course, items, Rules);
            if (error != "") throw new ArgumentException(error);
            if (frame != null && (snapshot.Epoch < frame.Epoch ||
                snapshot.Epoch == frame.Epoch && snapshot.Progress.Tick < frame.Progress.Tick))
                throw new ArgumentException("Physics restoration cannot rewind within the same authority epoch.");
            var states = snapshot.Bodies.ToDictionary(b => b.Id, StringComparer.Ordinal);
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
            if (!HasLineOfSight(actorId, WordboundNetworkBody.Value(actor.Rigidbody.position), position)) return false;
            int count = scene.OverlapBox(foot + Vector3.up * (half.y + .03f),
                new Vector3(Mathf.Max(.01f, half.x - .02f), half.y, Mathf.Max(.01f, half.z - .02f)),
                overlaps, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            if (count >= overlaps.Length) return false;
            for (int i = 0; i < count; i++)
                if (Blocks(actor.Canonical, overlaps[i]) &&
                    (!owned.TryGetValue(overlaps[i], out var owner) || owner != actor)) return false;
            foreach (float x in new[] { -half.x, half.x })
                foreach (float z in new[] { -half.z, half.z })
                    if (!Supported(actor.Canonical, foot + new Vector3(x, .08f, z))) return false;
            return true;
        }

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

        void RefreshCollisionPairs()
        {
            if (!collisionPairsDirty) return;
            collisionPairsDirty = false;
            var all = bodies.Values.ToArray();
            for (int i = 0; i < all.Length; i++)
                for (int j = i + 1; j < all.Length; j++)
                {
                    bool ignore = !WordboundPhysicsPolicy.ShouldCollide(mode, all[i].Canonical, all[j].Canonical);
                    foreach (var a in all[i].OwnedColliders)
                        foreach (var b in all[j].OwnedColliders) UnityEngine.Physics.IgnoreCollision(a, b, ignore);
                }
        }

        void BeforeSimulate(NetworkRunner current)
        {
            if (disposed || current != runner) return;
            bool freeze = ShouldFreeze;
            simulating = !freeze;
            simulationRevision = appliedRevision;
            foreach (var body in bodies.Values) body.PrepareSimulation(freeze);
        }

        void AfterSimulate(NetworkRunner current)
        {
            if (disposed || current != runner || !simulating) return;
            simulating = false;
            try
            {
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
            bodies.Clear(); owned.Clear(); structure.Clear();
        }
    }
}
