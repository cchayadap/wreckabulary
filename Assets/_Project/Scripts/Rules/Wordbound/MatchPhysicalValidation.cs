using System;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public static class MatchStateValidationPhysical
    {
        public static void Validate(MatchSnapshot snapshot, ResolvedCourse course, MatchMechanismState mechanism)
        {
            void Need(bool value, string error) { if (!value) throw new ArgumentException(error); }
            var site = course.PhysicalSites.SingleOrDefault(s => s.Lap == mechanism.Lap + 1 && s.PuzzleId == mechanism.PuzzleId);
            var evidence = mechanism.Physical;
            if (site == null)
            {
                Need(evidence.SiteHash == "" && evidence.Stage == MatchPhysicalStage.Waiting && evidence.MotionUnits == 0,
                    "Mechanism has unbound physical evidence.");
                return;
            }
            Need(evidence.SiteHash == site.Hash && evidence.MotionUnits / CourseFixtureMotion.UnitsPerTick +
                (evidence.MotionUnits % CourseFixtureMotion.UnitsPerTick == 0 ? 0 : 1) <= snapshot.Progress.Tick &&
                (site.Fixtures.Count != 0 || evidence.MotionUnits == 0), "Physical profile or motion phase is invalid.");
            Need(mechanism.ActivatedTick >= 0 || evidence.Stage == MatchPhysicalStage.Waiting,
                "Inactive physical mechanism has active evidence.");
            if (mechanism.Solved) Need(evidence.Stage == MatchPhysicalStage.Complete, "A physical puzzle was solved without completed host evidence.");
            if (evidence.Stage == MatchPhysicalStage.Waiting) return;
            Need(evidence.StageTick >= mechanism.ActivatedTick && evidence.StageTick <= snapshot.Progress.Tick &&
                snapshot.Binding.Seats.Any(s => s.PlayerId == evidence.ActorId && s.Team == mechanism.Team),
                "Physical evidence has an invalid actor or milestone tick.");
            var puzzle = course.Puzzles.Single(p => p.Id == mechanism.PuzzleId);
            if (puzzle.PresetId == "TimedMechanism")
                Need(evidence.AuxiliaryBodyId == "keepsake:" + mechanism.Team, "Timed evidence has a foreign keepsake.");
            else if (puzzle.PresetId == "CollectLetters" || puzzle.PresetId == "RedirectWind")
            {
                var body = snapshot.Bodies.SingleOrDefault(b => b.Id == evidence.AuxiliaryBodyId);
                Need(body != null && body.Kind == WorldBodyKind.Item && (body.OwnerTeam == mechanism.Team || body.OwnerTeam == -1),
                    "Physical source/receiver identity is missing or belongs to another team.");
                var item = snapshot.Economy.Items.SingleOrDefault(i => "item:" + i.Id == evidence.AuxiliaryBodyId);
                Need(item != null, "Physical source/receiver economy identity is missing.");
                if (puzzle.PresetId == "CollectLetters")
                    Need(item.State == ItemState.Destroyed && course.Placements.Any(p =>
                        p.Id == body.SourceId && p.ZoneId == site.PropSourceZoneId && p.Purpose == "Props"),
                        "Smash evidence does not identify the authored destroyed prop.");
                else
                    Need(item.Word == site.WindReceiverRecipeId && (item.Origin == ItemOrigin.Crafted ||
                        course.Placements.Any(p => p.Id == body.SourceId && p.ZoneId == site.WindReceiverZoneId)),
                        "Wind evidence does not identify the authored or replacement receiver.");
            }
            else Need(evidence.AuxiliaryBodyId == "", "Unexpected auxiliary body in physical evidence.");
        }
    }
}
