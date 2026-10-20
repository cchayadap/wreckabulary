using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(Rigidbody), typeof(NetworkRigidbody))]
    public sealed class WordboundNetworkBody : NetworkBehaviour
    {
        [Networked] public NetworkString<_64> BodyId { get; private set; }
        [Networked] public NetworkString<_64> ActorId { get; private set; }
        [Networked] public NetworkString<_64> SourceId { get; private set; }
        [Networked] public int BodyKind { get; private set; }
        [Networked] public int OwnerTeam { get; private set; }
        [Networked] public int RecoveryGeneration { get; private set; }
        [Networked] public NetworkBool Protected { get; private set; }
        [Networked] public NetworkBool BodyActive { get; private set; }
        [Networked] public NetworkBool CanonicalKinematic { get; private set; }
        [Networked] public int ItemMode { get; private set; }

        Rigidbody body;
        NetworkRigidbody networkBody;
        Collider[] colliders;
        Renderer[] renderers;
        WordboundPhysicsWorld world;
        WorldBodyState canonical;
        bool sampledSinceRestore;
        bool initialized;
        bool visuallyActive;

        public Rigidbody Rigidbody => body;
        public IReadOnlyList<Collider> OwnedColliders => colliders;
        public WorldBodyState Canonical => canonical;

        public override void Spawned()
        {
            CacheComponents();
            if (canonical == null) ApplyVisibility(BodyActive);
        }

        void CacheComponents()
        {
            if (initialized) return;
            body = GetComponent<Rigidbody>();
            networkBody = GetComponent<NetworkRigidbody>();
            if (GetComponent<NetworkTransform>() != null)
                throw new InvalidOperationException("A Wordbound body uses NetworkRigidbody without NetworkTransform.");
            colliders = GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0) throw new InvalidOperationException("A canonical physics body needs an owned collider.");
            foreach (var collider in colliders)
                if (collider.attachedRigidbody != body)
                    throw new InvalidOperationException("A body cannot own nested independent rigidbodies.");
            renderers = GetComponentsInChildren<Renderer>(true);
            networkBody.SyncParent = false;
            networkBody.SyncScale = false;
            initialized = true;
        }

        public void BindHost(WordboundPhysicsWorld owner, WorldBodyState state, PlayerRef inputAuthority)
        {
            if (owner == null || !owner.IsHost || Object == null || !Object.HasStateAuthority)
                throw new InvalidOperationException("Only the running state authority binds a canonical body.");
            if (world != null || canonical != null) throw new InvalidOperationException("Physics body is already bound.");
            CacheComponents();
            string actor = WordboundPhysicsPolicy.ValidateAdmission(state, owner.Binding,
                inputAuthority != PlayerRef.None, owner.AdmittedActor(inputAuthority));
            if (Object.InputAuthority != inputAuthority)
                throw new ArgumentException("Input authority does not match the admitted canonical seat.");
            world = owner;
            BodyId = state.Id; BodyKind = (int)state.Kind; ActorId = actor;
            ItemMode = -1;
            if (state.Kind == WorldBodyKind.Player) body.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            ApplyCanonical(state);
        }

        public void ApplyCanonical(WorldBodyState state)
        {
            if (world == null || !world.IsHost || Object == null || !Object.HasStateAuthority)
                throw new InvalidOperationException("Only a bound host body accepts canonical state.");
            if (canonical != null) WordboundPhysicsPolicy.ValidateUpdate(canonical, state);
            else WordboundPhysicsPolicy.ValidatePose(state);
            if (WordboundPhysicsPolicy.Actor(state, world.Binding) != ActorId.ToString())
                throw new ArgumentException("A body cannot change its authenticated actor.");
            canonical = state;
            SourceId = state.SourceId; OwnerTeam = state.OwnerTeam; Protected = state.Protected;
            RecoveryGeneration = state.RecoveryGeneration; BodyActive = state.Active; CanonicalKinematic = state.Kinematic;
            sampledSinceRestore = false;
            RestoreEngine(world.ShouldFreeze);
        }

        internal void PrepareSimulation(bool freeze)
        {
            if (canonical == null) return;
            if (freeze) RestoreEngine(true);
            else
            {
                bool kinematic = canonical.Kinematic || !canonical.Active;
                if (body.isKinematic != kinematic)
                {
                    body.isKinematic = kinematic;
                    if (!kinematic) RestoreVelocity();
                }
            }
        }

        internal void AfterSimulation() { if (canonical != null && canonical.Active) sampledSinceRestore = true; }
        internal void SetItemMode(ItemState? state) { ItemMode = state.HasValue ? (int)state.Value : -1; }

        public WorldBodyState CaptureHost()
        {
            if (world == null || !world.IsHost || canonical == null)
                throw new InvalidOperationException("Only a registered host body can be captured.");
            if (!sampledSinceRestore || world.ShouldFreeze) return canonical;
            var rotation = body.rotation;
            return WordboundPhysicsPolicy.Capture(canonical, true, Value(body.position),
                new WorldVector(rotation.x, rotation.y, rotation.z), rotation.w, Value(body.linearVelocity),
                Value(body.angularVelocity), body.IsSleeping());
        }

        void RestoreEngine(bool freeze)
        {
            bool kinematic = canonical.Kinematic || !canonical.Active || freeze;
            if (body.isKinematic != kinematic) body.isKinematic = kinematic;
            var position = Vector(canonical.Position);
            var rotation = new Quaternion(canonical.RotationXyz.X, canonical.RotationXyz.Y, canonical.RotationXyz.Z, canonical.RotationW);
            bool poseChanged = !body.position.Equals(position) || !body.rotation.Equals(rotation);
            if (poseChanged)
            {
                if (networkBody.PhysicsBody != null) networkBody.Teleport(position, rotation);
                body.position = position;
                body.rotation = rotation;
                transform.SetPositionAndRotation(body.position, body.rotation);
            }
            if (!body.isKinematic) RestoreVelocity();
            if (visuallyActive != canonical.Active || body.detectCollisions != canonical.Active) ApplyVisibility(canonical.Active);
        }

        void RestoreVelocity()
        {
            var velocity = Vector(canonical.Velocity);
            var angular = Vector(canonical.AngularVelocity);
            if (!body.linearVelocity.Equals(velocity)) body.linearVelocity = velocity;
            if (!body.angularVelocity.Equals(angular)) body.angularVelocity = angular;
            if (canonical.Sleeping && !body.IsSleeping()) body.Sleep();
            else if (!canonical.Sleeping && body.IsSleeping()) body.WakeUp();
        }

        void ApplyVisibility(bool active)
        {
            visuallyActive = active;
            foreach (var collider in colliders) collider.enabled = active;
            foreach (var renderer in renderers) renderer.enabled = active;
            body.detectCollisions = active;
        }

        public override void FixedUpdateNetwork()
        {
            if (world == null || canonical == null || !Object.HasStateAuthority) return;
            PrepareSimulation(world.ShouldFreeze);
            if (canonical.Kind != WorldBodyKind.Player || world.ShouldFreeze || !canonical.Active || canonical.Kinematic) return;
            string actor = ActorId.ToString();
            if (world.AdmittedActor(Object.InputAuthority) != actor || !world.CanMove(actor, out float moveScale)) return;
            if (!world.TryMovementEnvironment(actor, out var environment)) return;
            WorldVector direction = default;
            bool hasInput = GetInput(out WordboundMovementInput input) &&
                WordboundPhysicsPolicy.TryMovement(input, world.Epoch, out direction);
            Vector3 current = body.linearVelocity;
            Vector3 normal = Vector3.up;
            bool grounded = current.y <= .1f && world.TryGround(this, out normal);
            Vector3 desired = Vector(direction) * (6.5f * moveScale * environment.SpeedMultiplier);
            if (grounded && desired.sqrMagnitude > .0001f) desired = Vector3.ProjectOnPlane(desired, normal).normalized * desired.magnitude;
            float acceleration = direction.DistanceSquared(default) > .0001f ? world.Rules.GroundAccel : world.Rules.GroundFriction;
            acceleration *= environment.AccelerationMultiplier;
            Vector3 next = Vector3.MoveTowards(current, new Vector3(desired.x, grounded ? desired.y : current.y, desired.z),
                acceleration * Runner.DeltaTime);
            if (hasInput && input.Jump && grounded) next.y = Mathf.Sqrt(2 * 9.81f * world.Rules.JumpHeight);
            body.linearVelocity = next;
            if (direction.DistanceSquared(default) > .0001f)
                body.MoveRotation(Quaternion.LookRotation(new Vector3(direction.X, 0, direction.Z), Vector3.up));
        }

        public override void Render()
        {
            if (!initialized) return;
            if (visuallyActive != BodyActive) ApplyVisibility(BodyActive);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            world?.Unregister(this);
            world = null; canonical = null;
        }

        internal static Vector3 Vector(WorldVector value) => new Vector3(value.X, value.Y, value.Z);
        internal static WorldVector Value(Vector3 value) => new WorldVector(value.x, value.y, value.z);
    }
}
