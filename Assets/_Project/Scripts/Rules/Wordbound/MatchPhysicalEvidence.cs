using System;
using System.Globalization;

namespace Wreckabulary.Rules.Wordbound
{
    public enum MatchPhysicalStage { Waiting, Armed, Complete }

    public sealed class MatchPhysicalEvidence
    {
        public static MatchPhysicalEvidence Empty { get; } = new MatchPhysicalEvidence();
        public MatchPhysicalStage Stage { get; }
        public string ActorId { get; }
        public string AuxiliaryBodyId { get; }
        public long StageTick { get; }
        public string SiteHash { get; }
        public long MotionUnits { get; }
        public MatchPhysicalEvidence(MatchPhysicalStage stage = MatchPhysicalStage.Waiting, string actorId = "",
            string auxiliaryBodyId = "", long stageTick = 0, string siteHash = "", long motionUnits = 0)
        {
            if (!Enum.IsDefined(typeof(MatchPhysicalStage), stage) || actorId == null || auxiliaryBodyId == null ||
                siteHash == null || actorId != "" && !WordboundIdentity.IsId(actorId) ||
                auxiliaryBodyId != "" && !WordboundIdentity.IsId(auxiliaryBodyId) ||
                siteHash != "" && !WordboundIdentity.IsHash(siteHash) || stageTick < 0 || motionUnits < 0 ||
                stage == MatchPhysicalStage.Waiting && (actorId != "" || auxiliaryBodyId != "" || stageTick != 0) ||
                stage != MatchPhysicalStage.Waiting && (actorId == "" || stageTick == 0 || siteHash == "") ||
                siteHash == "" && motionUnits != 0)
                throw new ArgumentException("Invalid physical mechanism evidence.");
            Stage = stage; ActorId = actorId; AuxiliaryBodyId = auxiliaryBodyId; StageTick = stageTick;
            SiteHash = siteHash; MotionUnits = motionUnits;
        }
        public MatchPhysicalEvidence Reset() => new MatchPhysicalEvidence(siteHash: SiteHash, motionUnits: MotionUnits);
        public MatchPhysicalEvidence WithMotion(long units) =>
            new MatchPhysicalEvidence(Stage, ActorId, AuxiliaryBodyId, StageTick, SiteHash, units);
    }

    public static class CourseFixtureMotion
    {
        public const int UnitsPerTick = 1000;
        public static string BodyId(string fixtureId, int team) => "fixture:" + fixtureId + ":t" + team.ToString(CultureInfo.InvariantCulture);
        public static WorldVector Position(ResolvedCourseMovingFixture fixture, long units)
        {
            if (fixture == null || units < 0) throw new ArgumentException("Invalid canonical fixture motion.");
            long period = (long)fixture.PeriodTicks * UnitsPerTick;
            long phase = (units % period + (long)fixture.PhaseTicks * UnitsPerTick) % period;
            double t = phase * 2d / period;
            if (t > 1) t = 2 - t;
            return new WorldVector((float)((fixture.From.X + (fixture.To.X - fixture.From.X) * t) / 1000d),
                (float)((fixture.From.Y + (fixture.To.Y - fixture.From.Y) * t) / 1000d),
                (float)((fixture.From.Z + (fixture.To.Z - fixture.From.Z) * t) / 1000d));
        }
    }
}
