using System;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed partial class AuthoritativeMatch
    {
        MatchPhysicalEvidence InitialPhysical(string puzzle, int lap)
        {
            var site = course.PhysicalSites.SingleOrDefault(s => s.Lap == lap + 1 && s.PuzzleId == puzzle);
            return site == null ? MatchPhysicalEvidence.Empty : new MatchPhysicalEvidence(siteHash: site.Hash);
        }

        bool DestroyedDuringTick(string id, MatchSnapshot before)
        {
            if (id == "" || !bodies.TryGetValue(id, out var body) || body.Kind != WorldBodyKind.Item) return false;
            var now = economy.Item(Number(id));
            return now != null && now.IsGone && before.Economy.Items.Any(item => item.Id == now.Id && !item.IsGone);
        }

        void ValidatePhysicalObservation(MatchMechanismState previous, HostMechanismObservation observation)
        {
            var site = course.PhysicalSites.SingleOrDefault(s => s.Lap == observation.Lap + 1 && s.PuzzleId == observation.PuzzleId);
            var next = observation.Physical;
            var prior = previous.Physical;
            if (site == null)
            {
                if (next.SiteHash != "" || next.Stage != MatchPhysicalStage.Waiting || next.MotionUnits != 0)
                    throw new ArgumentException("Unbound physical observation.");
                return;
            }
            if (next.SiteHash != site.Hash || next.MotionUnits < prior.MotionUnits ||
                next.MotionUnits - prior.MotionUnits > CourseFixtureMotion.UnitsPerTick ||
                site.Fixtures.Count == 0 && next.MotionUnits != 0)
                throw new ArgumentException("Physical profile or motion phase is not causal.");
            if (next.Stage == MatchPhysicalStage.Waiting) return;
            if (!observation.Active || !indices.TryGetValue(next.ActorId, out int index) ||
                Binding.Seats[index].Team != observation.Team || !progress.Player(next.ActorId).Connected ||
                progress.Player(next.ActorId).Eliminated || progress.Player(next.ActorId).Returned ||
                next.StageTick > Tick + 1)
                throw new ArgumentException("Physical evidence requires a live actor from this team.");
            bool restarted = prior.Stage == MatchPhysicalStage.Waiting || previous.ItemEntityId != observation.ItemEntityId;
            if (restarted ? next.Stage != MatchPhysicalStage.Armed || next.StageTick != Tick + 1 :
                next.Stage < prior.Stage || (int)next.Stage > (int)prior.Stage + 1 ||
                next.ActorId != prior.ActorId || next.AuxiliaryBodyId != prior.AuxiliaryBodyId ||
                (next.Stage == prior.Stage ? next.StageTick != prior.StageTick : next.StageTick != Tick + 1))
                throw new ArgumentException("Physical evidence skipped a stage, changed its actor/body or replayed an old milestone.");
            ValidatePhysicalAuxiliary(site, next, observation.Team);
        }

        void ValidatePhysicalAuxiliary(ResolvedCoursePhysicalSite site, MatchPhysicalEvidence evidence, int team)
        {
            var puzzle = course.Puzzles.Single(p => p.Id == site.PuzzleId);
            if (puzzle.PresetId == "TimedMechanism")
            {
                if (evidence.AuxiliaryBodyId != "keepsake:" + team ||
                    progress.Team(team).KeepsakeCarrier != evidence.ActorId)
                    throw new ArgumentException("Timed traversal requires this actor carrying the team's keepsake.");
            }
            else if (puzzle.PresetId == "CollectLetters" || puzzle.PresetId == "RedirectWind")
            {
                if (!bodies.TryGetValue(evidence.AuxiliaryBodyId, out var body) || body.Kind != WorldBodyKind.Item ||
                    body.OwnerTeam != team && body.OwnerTeam != -1)
                    throw new ArgumentException("Physical evidence has no compatible source/receiver body.");
                var item = economy.Item(Number(body.Id));
                if (puzzle.PresetId == "CollectLetters")
                {
                    if (item.State != ItemState.Destroyed || !course.Placements.Any(p => p.Id == body.SourceId &&
                        p.ZoneId == site.PropSourceZoneId && p.Purpose == "Props"))
                        throw new ArgumentException("Smash evidence requires the authored prop's destruction.");
                }
                else if (!body.Active || item.Word != site.WindReceiverRecipeId ||
                    item.State != ItemState.World && item.State != ItemState.Thrown ||
                    item.Origin != ItemOrigin.Crafted && !course.Placements.Any(p => p.Id == body.SourceId && p.ZoneId == site.WindReceiverZoneId))
                    throw new ArgumentException("Wind evidence requires a free authored or replacement receiver.");
            }
            else if (evidence.AuxiliaryBodyId != "") throw new ArgumentException("Unexpected physical auxiliary body.");
        }

        void ValidateFixturePoses()
        {
            if (!Mode.IsRace) return;
            foreach (int team in Binding.Seats.Select(s => s.Team).Distinct())
                foreach (var site in course.PhysicalSites)
                    foreach (var fixture in site.Fixtures)
                    {
                        var body = bodies[CourseFixtureMotion.BodyId(fixture.Id, team)];
                        var state = mechanisms[Key(site.PuzzleId, team, site.Lap - 1)];
                        if (body.Position.DistanceSquared(CourseFixtureMotion.Position(fixture, state.Physical.MotionUnits)) >= .000001f ||
                            body.RotationW != 1 || body.RotationXyz.X != 0 || body.RotationXyz.Y != 0 || body.RotationXyz.Z != 0)
                            throw new ArgumentException("Host machinery pose and canonical motion phase must advance atomically.");
                    }
        }
    }
}
