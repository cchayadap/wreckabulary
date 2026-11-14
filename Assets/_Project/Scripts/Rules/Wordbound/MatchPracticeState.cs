using System;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class MatchPracticeState
    {
        public static MatchPracticeState Empty { get; } = new MatchPracticeState();
        public string SiteHash { get; }
        public long SelectedTick { get; }
        public MatchPracticeState(string siteHash = "", long selectedTick = -1)
        {
            if (siteHash == null || siteHash != "" && !WordboundIdentity.IsHash(siteHash) ||
                (siteHash == "" ? selectedTick != -1 : selectedTick < 1 || selectedTick == long.MaxValue))
                throw new ArgumentException("Practice selection needs an exact authored site and causal tick.");
            SiteHash = siteHash;
            SelectedTick = selectedTick;
        }
    }

    public static class WordboundPracticePolicy
    {
        public const int Version = 1;
        public static string Hash { get; } = WordboundIdentity.Sha256(
            "wordbound.practice|1|single-shared-authored-site|host-reach-los|once-per-session|" +
            "selected-recovery-only|real-keepsake|no-race-progress|no-result");
        public static bool Supports(ModeDefinition mode) => mode?.Id == "Freestyle";
        public static bool HasKeepsake(ModeDefinition mode) => mode.IsRace || Supports(mode);
        public static ResolvedCoursePhysicalSite Selected(ResolvedCourse course, MatchPracticeState state) =>
            state == null || state.SiteHash == "" ? null : course.PhysicalSites.SingleOrDefault(s => s.Hash == state.SiteHash);
        public static bool Active(MatchSnapshot frame, ResolvedCoursePhysicalSite site) =>
            site != null && frame.Practice.SiteHash == site.Hash &&
            !frame.Mechanisms.Any(m => m.Team == 0 && m.Lap == site.Lap - 1 && m.PuzzleId == site.PuzzleId && m.Solved);
    }
}
