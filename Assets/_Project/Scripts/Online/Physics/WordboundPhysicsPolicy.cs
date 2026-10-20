using System;
using System.Globalization;
using Fusion;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public struct WordboundMovementInput : INetworkInput
    {
        public long Epoch;
        public float MoveX;
        public float MoveZ;
        public NetworkBool Jump;
    }

    public static class WordboundPhysicsPolicy
    {
        public static bool TryMovement(WordboundMovementInput input, long epoch, out WorldVector direction)
        {
            direction = default;
            if (input.Epoch != epoch || !Finite(input.MoveX) || !Finite(input.MoveZ) ||
                Math.Abs(input.MoveX) > 1 || Math.Abs(input.MoveZ) > 1) return false;
            double length = Math.Sqrt((double)input.MoveX * input.MoveX + (double)input.MoveZ * input.MoveZ);
            float divisor = length > 1 ? (float)length : 1;
            direction = new WorldVector(input.MoveX / divisor, 0, input.MoveZ / divisor);
            return true;
        }

        public static string Actor(WorldBodyState state, MatchBinding binding)
        {
            if (state == null || binding == null) throw new ArgumentNullException(nameof(state));
            ValidatePose(state);
            if (state.Kind != WorldBodyKind.Player) return "";
            if (!state.Id.StartsWith("player:", StringComparison.Ordinal) ||
                !int.TryParse(state.Id.Substring(7), NumberStyles.None, CultureInfo.InvariantCulture, out int index) ||
                index < 0 || index >= binding.Seats.Count || state.Id != "player:" + index.ToString(CultureInfo.InvariantCulture))
                throw new ArgumentException("Player body does not identify a canonical seat.");
            var seat = binding.Seats[index];
            if (state.SourceId != seat.PlayerId || state.OwnerTeam != seat.Team || state.Protected)
                throw new ArgumentException("Player body identity disagrees with its signed seat.");
            return seat.PlayerId;
        }

        public static void ValidatePose(WorldBodyState state)
        {
            if (state == null || !Bounded(state.Position, 10000) || !Bounded(state.Velocity, 1000) ||
                !Bounded(state.AngularVelocity, 10000))
                throw new ArgumentException("Body pose or velocity is outside supported bounds.");
        }

        public static string ValidateAdmission(WorldBodyState state, MatchBinding binding, bool hasInputAuthority, string admittedActor)
        {
            string actor = Actor(state, binding);
            if (actor == "" && hasInputAuthority ||
                actor != "" && (hasInputAuthority ? admittedActor != actor : state.Active))
                throw new ArgumentException("Input authority does not match the admitted canonical seat.");
            return actor;
        }

        public static void ValidateUpdate(WorldBodyState current, WorldBodyState next)
        {
            ValidatePose(next);
            if (current == null || current.Id != next.Id || current.Kind != next.Kind)
                throw new ArgumentException("A registered physics body cannot change its canonical identity or kind.");
        }

        public static WorldBodyState Capture(WorldBodyState canonical, bool simulated, WorldVector position,
            WorldVector rotationXyz, float rotationW, WorldVector velocity, WorldVector angularVelocity, bool sleeping)
        {
            if (canonical == null) throw new ArgumentNullException(nameof(canonical));
            if (!simulated) return canonical;
            var result = new WorldBodyState(canonical.Id, canonical.Kind, position, rotationXyz, rotationW, velocity,
                angularVelocity, sleeping, canonical.Kinematic, canonical.Active, canonical.OwnerTeam,
                canonical.Protected, canonical.SourceId, canonical.RecoveryGeneration);
            ValidatePose(result);
            return result;
        }

        public static void ValidateItemMode(WorldBodyState body, ItemState state)
        {
            if (body == null || body.Kind != WorldBodyKind.Item) throw new ArgumentException("Item mode requires an item body.");
            bool valid = state switch
            {
                ItemState.Held => !body.Active && body.Kinematic,
                ItemState.Carried => !body.Active && body.Kinematic,
                ItemState.Deployed => body.Active && body.Kinematic,
                ItemState.Thrown => body.Active && !body.Kinematic,
                ItemState.World => body.Active && !body.Kinematic,
                ItemState.Armed => body.Active,
                ItemState.Destroyed => !body.Active,
                _ => false
            };
            if (!valid) throw new ArgumentException("Canonical activity/kinematic flags disagree with the item's economy mode.");
        }

        public static bool ShouldCollide(ModeDefinition mode, WorldBodyState first, WorldBodyState second)
        {
            if (mode == null || first == null || second == null || !first.Active || !second.Active) return false;
            if (first.Kind == WorldBodyKind.Player && second.Kind == WorldBodyKind.Player)
                return mode.PlayersCollide(first.OwnerTeam, second.OwnerTeam);
            if (first.OwnerTeam >= 0 && second.OwnerTeam >= 0 && first.OwnerTeam != second.OwnerTeam)
                return mode.Ownership != WordboundOwnership.TeamIsolated && !first.Protected && !second.Protected;
            return true;
        }

        static bool Bounded(WorldVector value, float bound) => Finite(value.X) && Finite(value.Y) && Finite(value.Z) &&
            Math.Abs(value.X) <= bound && Math.Abs(value.Y) <= bound && Math.Abs(value.Z) <= bound;
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
