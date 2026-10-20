using System;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed partial class AuthoritativeMatch
    {
        MatchPracticeState practice = MatchPracticeState.Empty;
        bool IsPractice => WordboundPracticePolicy.Supports(Mode);
        ResolvedCoursePhysicalSite PracticeSite => WordboundPracticePolicy.Selected(course, practice);

        void InitializePractice()
        {
            if (!IsPractice) return;
            AddBody(new WorldBodyState("keepsake:0", WorldBodyKind.Keepsake, Spawn(0),
                ownerTeam: 0, protectedResource: true));
            foreach (var site in course.PhysicalSites)
                mechanisms.Add(Key(site.PuzzleId, 0, site.Lap - 1),
                    new MatchMechanismState(site.PuzzleId, 0, site.Lap - 1, physical: InitialPhysical(site.PuzzleId, site.Lap - 1)));
        }

        bool PracticeActive(string puzzle, int lap) => IsPractice && PracticeSite is ResolvedCoursePhysicalSite site &&
            site.PuzzleId == puzzle && site.Lap == lap + 1 && !mechanisms[Key(puzzle, 0, lap)].Solved;

        CommandEffect SelectPractice(WordboundSeat seat, PlayerCommand command, WorldBodyState origin)
        {
            if (!IsPractice) return Effect("Unavailable");
            if (command.Revision != 0 || command.Slot != -1 || command.Value != "") return Effect("InvalidPractice");
            var site = course.PhysicalSites.SingleOrDefault(s => s.Hash == command.Target);
            if (site == null) return Effect("UnknownPractice");
            var at = Position(site.Position);
            if (origin.Position.DistanceSquared(at) > 9 || !world.HasLineOfSight(seat.PlayerId, origin.Position, at))
                return Effect("OutOfReach");
            if (mechanisms[Key(site.PuzzleId, 0, site.Lap - 1)].Solved) return Effect("AlreadySolved");
            if (practice.SiteHash == site.Hash) return Effect("Ok", site.PuzzleId);
            foreach (var prior in mechanisms.Values.Where(m => !m.Solved).ToArray())
                mechanisms[Key(prior.PuzzleId, prior.Team, prior.Lap)] = new MatchMechanismState(
                    prior.PuzzleId, prior.Team, prior.Lap, physical: prior.Physical.Reset());
            practice = new MatchPracticeState(site.Hash, Tick + 1);
            return Effect("Ok", site.PuzzleId);
        }

        WorldVector PracticeRecoveryPosition()
        {
            var site = PracticeSite;
            var anchor = site == null ? null : course.RecoveryAnchors
                .Where(a => a.ModuleId == course.Puzzles.Single(p => p.Id == site.PuzzleId).ModuleId)
                .OrderBy(a => a.Id, StringComparer.Ordinal).FirstOrDefault();
            return anchor == null ? Spawn(0) : Position(anchor.Position);
        }

        bool PracticeRecoverySource(string sourceId) =>
            IsPractice && resourceLifecycle.PracticeSource(sourceId, practice, mechanisms.Values);
    }
}
