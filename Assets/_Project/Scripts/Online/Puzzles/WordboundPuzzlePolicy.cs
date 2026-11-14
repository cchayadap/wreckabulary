using System;
using System.Linq;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public static class WordboundPuzzlePolicy
    {
        public static bool InSocket(WordboundPlacementSocket socket, WorldBodyState body)
        {
            if (socket == null || body == null || body.Kind != WorldBodyKind.Item || !body.Active || !body.Kinematic ||
                socket.Position.DistanceSquared(body.Position) > socket.RadiusMm * socket.RadiusMm / 1000000f) return false;
            double x = body.RotationXyz.X, y = body.RotationXyz.Y, z = body.RotationXyz.Z, w = body.RotationW;
            double upY = 1 - 2 * (x * x + z * z);
            double forwardX = 2 * (x * z + w * y), forwardZ = 1 - 2 * (x * x + y * y);
            double radians = socket.Yaw * Math.PI / 180;
            return upY >= Math.Cos(5 * Math.PI / 180) &&
                forwardX * Math.Sin(radians) + forwardZ * Math.Cos(radians) >= Math.Cos(socket.YawToleranceDegrees * Math.PI / 180);
        }

        public static HostMechanismObservation Observe(MatchMechanismState previous, long completedTick,
            string satisfyingItem)
        {
            if (previous == null || previous.Solved || completedTick < 0 || completedTick == long.MaxValue ||
                satisfyingItem == null || satisfyingItem != "" && !WordboundIdentity.IsId(satisfyingItem))
                throw new ArgumentException("A mechanism observation needs its current unsolved canonical state.");
            if (satisfyingItem == "") return new HostMechanismObservation(previous.PuzzleId, previous.Team, previous.Lap, false);
            long nextTick = completedTick + 1;
            bool continuous = previous.ActivatedTick >= 0 && previous.ItemEntityId == satisfyingItem &&
                previous.PhaseTick >= 1 && previous.PhaseTick <= completedTick &&
                previous.ProgressTicks == completedTick - previous.PhaseTick + 1;
            return new HostMechanismObservation(previous.PuzzleId, previous.Team, previous.Lap, true,
                continuous ? previous.PhaseTick : nextTick,
                continuous ? checked(previous.ProgressTicks + 1) : 1, satisfyingItem);
        }

        public static bool Ready(WordboundPlacementSocket socket, MatchMechanismState state, long completedTick, string itemId) =>
            socket != null && state != null && !state.Solved && state.PuzzleId == socket.PuzzleId &&
            state.ActivatedTick >= 0 && state.ItemEntityId == itemId && state.ProgressTicks >= socket.HoldTicks &&
            state.PhaseTick >= 1 && state.PhaseTick <= completedTick &&
            state.ProgressTicks == completedTick - state.PhaseTick + 1;

        public static bool ActiveObjective(ResolvedCourse course, TeamProgress team, string puzzleId) =>
            course != null && team != null && !team.Finished && team.Lap >= 0 && team.Lap < course.Laps.Count &&
            team.NextCheckpoint < course.Laps[team.Lap].Checkpoints.Count &&
            course.Laps[team.Lap].Checkpoints[team.NextCheckpoint].PuzzleIds.Contains(puzzleId) &&
            course.Puzzles.Single(p => p.Id == puzzleId).PrerequisiteIds.All(id => team.CompletedPuzzles.Contains(id));

        public static bool LiveRecipe(MatchSnapshot frame, WorldBodyState body, string recipe, int team)
        {
            if (frame == null || body == null || body.Kind != WorldBodyKind.Item || !body.Active || !body.Kinematic ||
                body.OwnerTeam != team || !body.Id.StartsWith("item:", StringComparison.Ordinal) ||
                !int.TryParse(body.Id.Substring(5), out int id)) return false;
            var item = frame.Economy.Items.SingleOrDefault(i => i.Id == id);
            return item != null && item.Word == recipe && item.Origin == ItemOrigin.Crafted && item.State == ItemState.Deployed;
        }
    }
}
