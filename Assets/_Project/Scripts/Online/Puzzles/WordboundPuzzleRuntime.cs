using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;

namespace Wreckabulary.Online
{
    public sealed class WordboundPuzzleRuntime
    {
        readonly WordboundGameplayController controller;
        readonly WordboundPuzzleProof proof;
        readonly ItemCatalogue items;
        readonly RaycastHit[] hits = new RaycastHit[128];
        readonly Collider[] overlaps = new Collider[128];
        readonly Dictionary<Collider, WordboundNetworkBody> owned = new Dictionary<Collider, WordboundNetworkBody>();
        readonly HashSet<Collider> structure = new HashSet<Collider>();
        PhysicsScene scene;

        public WordboundPuzzleRuntime(WordboundGameplayController controller, WordboundPuzzleProof proof)
        {
            this.controller = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
            this.proof = proof ?? throw new ArgumentNullException(nameof(proof));
            items = proof.Items;
        }

        public void BeforeHostPhysics(MatchSnapshot frame, IReadOnlyDictionary<string, WordboundNetworkBody> bodies)
        {
            RequireHost(frame);
            if (!ReferenceEquals(bodies, controller.Bodies)) throw new ArgumentException("Use the complete registered host body map.");
            if (controller.IsSimulationPaused) return;
            RefreshColliders();
            foreach (var path in proof.Plan.Platforms)
            {
                if (!bodies.TryGetValue(path.BodyId, out var body) || body.Canonical.Kind != WorldBodyKind.Platform ||
                    !body.Canonical.Kinematic || !body.Rigidbody.isKinematic)
                    throw new InvalidOperationException("A canonical kinematic proof platform is missing.");
                body.Rigidbody.MovePosition(Vector(path.Position(checked(frame.Progress.Tick + 1))));
            }
            foreach (var item in frame.Economy.Items.Where(i => i.Origin == ItemOrigin.Crafted && i.State == ItemState.Deployed))
            {
                var definition = items.Get(item.Word);
                if (definition.Deploy == null || definition.Deploy.Effect != DeployEffect.JumpPad ||
                    definition.Deploy.LifetimeSeconds != 0 || !bodies.TryGetValue("item:" + item.Id, out var pad)) continue;
                foreach (var player in bodies.Values.Where(b => b.Canonical.Kind == WorldBodyKind.Player))
                    ApplyJumpPad(player, pad, definition);
            }
        }

        public WordboundHostObservations ObserveHost(MatchSnapshot frame, IReadOnlyList<WorldBodyState> poses)
        {
            RequireHost(frame);
            if (poses == null || poses.Count != controller.Bodies.Count ||
                !new HashSet<string>(poses.Select(p => p.Id)).SetEquals(controller.Bodies.Keys))
                throw new ArgumentException("Physical observations require the exact completed host pose set.");
            RefreshColliders();
            var mechanisms = new List<HostMechanismObservation>();
            var progress = new List<ProgressEvent>();
            foreach (var team in frame.Progress.Teams.Where(t => !t.Finished))
            {
                foreach (var socket in proof.Plan.Sockets)
                {
                    if (!WordboundPuzzlePolicy.ActiveObjective(proof.Course, team, socket.PuzzleId)) continue;
                    var previous = frame.Mechanisms.Single(m => m.PuzzleId == socket.PuzzleId && m.Team == team.Team && m.Lap == team.Lap);
                    if (previous.Solved) continue;
                    var puzzle = proof.Course.Puzzles.Single(p => p.Id == socket.PuzzleId);
                    string item = controller.Bodies.OrderBy(p => p.Key, StringComparer.Ordinal)
                        .Where(p => Qualifies(frame, team.Team, socket, puzzle, p.Value)).Select(p => p.Key).FirstOrDefault() ?? "";
                    mechanisms.Add(WordboundPuzzlePolicy.Observe(previous, frame.Progress.Tick, item));
                }
                ObserveProgress(frame, team, progress);
            }
            return new WordboundHostObservations { Mechanisms = mechanisms, Progress = progress };
        }

        public bool Evaluate(string actorId, ResolvedCoursePuzzle puzzle, WorldBodyState item)
        {
            var frame = controller.Frame;
            RequireHost(frame);
            if (puzzle == null || item == null || !ReferenceEquals(proof.Course.Puzzles.SingleOrDefault(p => p.Id == puzzle.Id), puzzle))
                return false;
            var socket = proof.Plan.Sockets.SingleOrDefault(s => s.PuzzleId == puzzle.Id);
            var player = frame.Progress.Players.SingleOrDefault(p => p.Id == actorId);
            if (socket == null || player == null || !CanAct(actorId) || !controller.Bodies.TryGetValue(item.Id, out var registered)) return false;
            var team = frame.Progress.Teams.Single(t => t.Team == player.Team);
            if (!WordboundPuzzlePolicy.ActiveObjective(proof.Course, team, puzzle.Id) ||
                !Qualifies(frame, player.Team, socket, puzzle, registered)) return false;
            var state = frame.Mechanisms.Single(m => m.PuzzleId == puzzle.Id && m.Team == player.Team && m.Lap == team.Lap);
            return WordboundPuzzlePolicy.Ready(socket, state, frame.Progress.Tick, item.Id);
        }

        void RequireHost(MatchSnapshot frame)
        {
            if (controller.PhysicsWorld == null || !controller.PhysicsWorld.IsHost || controller.CourseWorld == null ||
                frame == null || !ReferenceEquals(frame, controller.Frame))
                throw new InvalidOperationException("Physical puzzles run only against the current host runner frame.");
            proof.Validate(frame.Binding);
            scene = controller.PhysicsWorld.PhysicsScene;
            if (!scene.IsValid() || scene == UnityEngine.Physics.defaultPhysicsScene ||
                controller.CourseWorld.ManifestHash != proof.Course.Hash)
                throw new InvalidOperationException("Physical puzzle geometry does not belong to the verified isolated runner.");
        }

        void RefreshColliders()
        {
            owned.Clear(); structure.Clear();
            foreach (var collider in controller.CourseWorld.Surfaces)
            {
                if (collider == null || collider.gameObject.scene.GetPhysicsScene() != scene)
                    throw new InvalidOperationException("Puzzle support geometry left its runner.");
                structure.Add(collider);
            }
            foreach (var body in controller.Bodies.Values)
                foreach (var collider in body.OwnedColliders)
                {
                    if (collider == null || collider.gameObject.scene.GetPhysicsScene() != scene || !owned.TryAdd(collider, body))
                        throw new InvalidOperationException("Puzzle body collider ownership is invalid.");
                }
        }

        bool Qualifies(MatchSnapshot frame, int team, WordboundPlacementSocket socket,
            ResolvedCoursePuzzle puzzle, WordboundNetworkBody body)
        {
            if (puzzle.PresetId != "PlaceObject" || !WordboundPuzzlePolicy.LiveRecipe(frame, body.Canonical, puzzle.RecipeId, team) ||
                !body.Rigidbody.isKinematic || !WordboundPuzzlePolicy.InSocket(socket, Pose(body))) return false;
            var box = body.GetComponent<BoxCollider>();
            if (box == null || !box.enabled || box.isTrigger || box.attachedRigidbody != body.Rigidbody) return false;
            Vector3 half = box.size / 2;
            var rotation = body.Rigidbody.rotation;
            var foot = body.Rigidbody.position;
            int count = scene.OverlapBox(foot + rotation * box.center + Vector3.up * .025f,
                new Vector3(Mathf.Max(.005f, half.x - .015f), Mathf.Max(.005f, half.y - .015f), Mathf.Max(.005f, half.z - .015f)),
                overlaps, rotation, ~0, QueryTriggerInteraction.Ignore);
            if (count >= overlaps.Length) return false;
            for (int i = 0; i < count; i++)
                if (Blocks(body.Canonical, overlaps[i], body)) return false;
            foreach (float x in new[] { -half.x + .02f, half.x - .02f })
                foreach (float z in new[] { -half.z + .02f, half.z - .02f })
                    if (!Supported(body.Canonical, foot + rotation * new Vector3(x, .08f, z), body)) return false;
            return true;
        }

        void ApplyJumpPad(WordboundNetworkBody player, WordboundNetworkBody pad, ItemDefinition definition)
        {
            if (!player.Canonical.Active || !pad.Canonical.Active || !CanAct(player.Canonical.SourceId) ||
                !WordboundPhysicsPolicy.ShouldCollide(proof.Mode, player.Canonical, pad.Canonical)) return;
            Vector3 local = Quaternion.Inverse(pad.Rigidbody.rotation) * (player.Rigidbody.position - pad.Rigidbody.position);
            var box = pad.GetComponent<BoxCollider>();
            if (box == null || !box.enabled || box.isTrigger || player.Rigidbody.linearVelocity.y > .5f ||
                Mathf.Abs(local.x) > box.size.x / 2 + .3f || Mathf.Abs(local.z) > box.size.z / 2 + .3f ||
                local.y < -.1f || local.y > box.size.y + .15f) return;
            Vector3 from = pad.Rigidbody.position + Vector3.up * (box.size.y + .05f);
            if (!ClearLine(player.Canonical, from, player.Rigidbody.position + Vector3.up * .9f, player, pad)) return;
            var velocity = player.Rigidbody.linearVelocity;
            velocity.y = definition.Deploy.Strength;
            player.Rigidbody.linearVelocity = velocity;
        }

        void ObserveProgress(MatchSnapshot frame, TeamProgress team, List<ProgressEvent> output)
        {
            var lap = proof.Course.Laps[team.Lap];
            bool finish = team.NextCheckpoint >= lap.Checkpoints.Count;
            var checkpoint = finish ? lap.Checkpoints[lap.Checkpoints.Count - 1] : lap.Checkpoints[team.NextCheckpoint];
            if (!finish && checkpoint.PuzzleIds.Any(id => !team.CompletedPuzzles.Contains(id))) return;
            var anchor = proof.Course.RecoveryAnchors.Single(r => r.Id == checkpoint.RecoveryAnchorId);
            foreach (var player in frame.Progress.Players.Where(p => p.Team == team.Team))
            {
                if (!CanAct(player.Id)) continue;
                var body = controller.Bodies["player:" + WordboundGameplayPlan.Seat(frame.Binding, player.Id)];
                if ((body.Rigidbody.position - Vector(WordboundPlatformPath.Metres(anchor.Position))).sqrMagnitude >
                    anchor.RadiusMm * anchor.RadiusMm / 1000000f ||
                    !Supported(body.Canonical, body.Rigidbody.position + Vector3.up * .1f, body)) continue;
                output.Add(new ProgressEvent(finish ? ProgressEventKind.FinishReached : ProgressEventKind.CheckpointReached,
                    player.Id, finish ? lap.FinishModuleId : checkpoint.Id, team.Lap));
            }
        }

        bool CanAct(string actor)
        {
            if (!controller.PhysicsWorld.CanMove(actor, out _)) return false;
            var body = controller.Bodies["player:" + WordboundGameplayPlan.Seat(controller.Frame.Binding, actor)];
            return controller.PhysicsWorld.AdmittedActor(body.Object.InputAuthority) == actor;
        }
        bool Supported(WorldBodyState actor, Vector3 origin, WordboundNetworkBody ignored)
        {
            int count = scene.Raycast(origin, Vector3.down, hits, .2f, ~0, QueryTriggerInteraction.Ignore);
            if (count >= hits.Length) return false;
            for (int i = 0; i < count; i++)
                if (hits[i].normal.y >= .819f && Blocks(actor, hits[i].collider, ignored) &&
                    (!owned.TryGetValue(hits[i].collider, out var body) || body.Canonical.Kind != WorldBodyKind.Player)) return true;
            return false;
        }
        bool ClearLine(WorldBodyState actor, Vector3 from, Vector3 to, params WordboundNetworkBody[] ignored)
        {
            var delta = to - from;
            if (delta.sqrMagnitude < .000001f) return true;
            int count = scene.Raycast(from, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count >= hits.Length) return false;
            for (int i = 0; i < count; i++)
                if (Blocks(actor, hits[i].collider, ignored)) return false;
            return true;
        }
        bool Blocks(WorldBodyState actor, Collider collider, params WordboundNetworkBody[] ignored) =>
            collider != null && (structure.Contains(collider) || owned.TryGetValue(collider, out var body) &&
                !ignored.Contains(body) && WordboundPhysicsPolicy.ShouldCollide(proof.Mode, actor, body.Canonical));

        static Vector3 Vector(WorldVector p) => new Vector3(p.X, p.Y, p.Z);
        static WorldBodyState Pose(WordboundNetworkBody body)
        {
            var c = body.Canonical; var p = body.Rigidbody.position; var q = body.Rigidbody.rotation;
            return new WorldBodyState(c.Id, c.Kind, new WorldVector(p.x, p.y, p.z),
                new WorldVector(q.x, q.y, q.z), q.w, active: c.Active, kinematic: c.Kinematic, ownerTeam: c.OwnerTeam,
                protectedResource: c.Protected, sourceId: c.SourceId, recoveryGeneration: c.RecoveryGeneration);
        }
    }
}
