using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundPuzzleRuntime
    {
        readonly Dictionary<string, string> launches = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly HashSet<string> windPushes = new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string, long> fixturePhases = new Dictionary<string, long>(StringComparer.Ordinal);

        static string PhaseKey(WordboundPhysicalSite site, int team) => site.Lap + "/" + site.PuzzleId + "/" + team;

        void MoveAuthoredFixtures(MatchSnapshot frame)
        {
            foreach (var site in proof.Plan.Sites.Where(s => s.Authored != null && s.Authored.Fixtures.Count > 0))
                foreach (var team in frame.Progress.Teams)
                {
                    var previous = frame.Mechanisms.Single(m => m.Team == team.Team && m.Lap == site.Lap && m.PuzzleId == site.PuzzleId);
                    long units = previous.Physical.MotionUnits;
                    if (!previous.Solved && !team.Finished)
                    {
                        float speed = 1;
                        foreach (var fixture in site.Authored.Fixtures)
                        {
                            var body = controller.Bodies[CourseFixtureMotion.BodyId(fixture.Id, team.Team)];
                            foreach (var field in Fields(frame).Where(f => f.Definition.Deploy.Effect == DeployEffect.SlowField))
                                if (ClockAffects(field.Definition, field.Body, body))
                                    speed = Math.Min(speed, field.Definition.Deploy.Strength);
                        }
                        units = checked(units + Math.Max(1, (int)Math.Round(speed * CourseFixtureMotion.UnitsPerTick)));
                    }
                    fixturePhases[PhaseKey(site, team.Team)] = units;
                    foreach (var fixture in site.Authored.Fixtures)
                    {
                        var body = controller.Bodies[CourseFixtureMotion.BodyId(fixture.Id, team.Team)];
                        if (!body.Rigidbody.isKinematic || body.Canonical.SourceId != fixture.Id ||
                            body.Canonical.OwnerTeam != team.Team || !body.Canonical.Protected)
                            throw new InvalidOperationException("Authored moving machinery lost its canonical ownership or physics mode.");
                        body.Rigidbody.MovePosition(Vector(CourseFixtureMotion.Position(fixture, units)));
                    }
                }
        }

        bool ClockAffects(ItemDefinition definition, WordboundNetworkBody clock, WordboundNetworkBody fixture)
        {
            if (!WordboundPhysicsPolicy.ShouldCollide(proof.Mode, clock.Canonical, fixture.Canonical)) return false;
            var pose = Pose(fixture);
            var query = new WorldBodyState(pose.Id, pose.Kind, pose.Position, active: pose.Active, ownerTeam: pose.OwnerTeam);
            return WordboundFieldPolicy.Contains(definition.Deploy, Pose(clock), query) &&
                ClearLine(fixture.Canonical, clock.Rigidbody.position + Vector3.up * .5f,
                    fixture.Rigidbody.worldCenterOfMass, clock, fixture);
        }

        void ObserveAuthored(MatchSnapshot frame, TeamProgress team, List<HostMechanismObservation> output)
        {
            foreach (var site in proof.Plan.Sites)
            {
                var previous = frame.Mechanisms.Single(m => m.Team == team.Team && m.Lap == site.Lap && m.PuzzleId == site.PuzzleId);
                if (previous.Solved) continue;
                long units = site.Authored.Fixtures.Count == 0 ? 0 :
                    fixturePhases.TryGetValue(PhaseKey(site, team.Team), out var phase) ? phase :
                    throw new InvalidOperationException("Moving machinery must complete its host physics step before observation.");
                var idle = new MatchPhysicalEvidence(siteHash: site.Hash, motionUnits: units);
                void Reset() => output.Add(new HostMechanismObservation(site.PuzzleId, team.Team, site.Lap, false, physical: idle));
                if (WordboundPracticePolicy.Supports(proof.Mode) ? !WordboundPracticePolicy.Active(frame, site.Authored) :
                    site.Lap != team.Lap || !WordboundPuzzlePolicy.ActiveObjective(proof.Course, team, site.PuzzleId))
                {
                    if (site.Authored.Fixtures.Count > 0) Reset();
                    continue;
                }
                var puzzle = proof.Course.Puzzles.Single(p => p.Id == site.PuzzleId);
                var recipe = controller.Bodies.Values.OrderBy(b => b.Canonical.Id, StringComparer.Ordinal)
                    .FirstOrDefault(b => Qualifies(frame, team.Team, site.Socket, puzzle, b));
                if (recipe == null) { Reset(); continue; }
                bool continuous = previous.ActivatedTick >= 0 && previous.ItemEntityId == recipe.Canonical.Id;
                var evidence = continuous ? previous.Physical.WithMotion(units) : idle;
                string actor = evidence.ActorId;
                if (actor != "" && !CanAct(actor)) { Reset(); continue; }
                if (actor == "")
                    actor = frame.Progress.Players.Where(p => p.Team == team.Team && CanAct(p.Id))
                        .OrderBy(p => p.Id, StringComparer.Ordinal).Select(p => p.Id)
                        .FirstOrDefault(id => ActorNear(id, recipe.Rigidbody.position)) ?? "";
                if (actor == "") { Reset(); continue; }
                string auxiliary = evidence.AuxiliaryBodyId;
                bool qualifies = FamilyReady(frame, team, site, puzzle, recipe, actor, evidence, ref auxiliary, out bool completed);
                if (!qualifies) { Reset(); continue; }
                long nextTick = checked(frame.Progress.Tick + 1);
                MatchPhysicalStage stage = evidence.Stage;
                long stageTick = evidence.StageTick;
                if (stage == MatchPhysicalStage.Waiting) { stage = MatchPhysicalStage.Armed; stageTick = nextTick; }
                else if (stage == MatchPhysicalStage.Armed && completed &&
                    (puzzle.PresetId == "BuildRoute" || puzzle.PresetId == "RedirectWind" || puzzle.PresetId == "TimedMechanism" ||
                        previous.ProgressTicks + 1 >= site.Socket.HoldTicks))
                { stage = MatchPhysicalStage.Complete; stageTick = nextTick; }
                var next = new MatchPhysicalEvidence(stage, actor, auxiliary, stageTick, site.Hash, units);
                var timing = WordboundPuzzlePolicy.Observe(previous, frame.Progress.Tick, recipe.Canonical.Id);
                output.Add(new HostMechanismObservation(site.PuzzleId, team.Team, site.Lap, true,
                    timing.PhaseTick, timing.ProgressTicks, recipe.Canonical.Id, next));
            }
        }

        bool FamilyReady(MatchSnapshot frame, TeamProgress team, WordboundPhysicalSite site, ResolvedCoursePuzzle puzzle,
            WordboundNetworkBody recipe, string actor, MatchPhysicalEvidence evidence, ref string auxiliary, out bool complete)
        {
            complete = false;
            var player = controller.Bodies["player:" + WordboundGameplayPlan.Seat(frame.Binding, actor)];
            switch (puzzle.PresetId)
            {
                case "PlaceObject":
                    complete = true;
                    return true;
                case "CollectLetters":
                    if (auxiliary == "")
                        auxiliary = frame.Bodies.Where(b => b.Kind == WorldBodyKind.Item &&
                            (b.OwnerTeam == team.Team || b.OwnerTeam == -1) &&
                            proof.Course.Placements.Any(p => p.Id == b.SourceId && p.ZoneId == site.PropSourceZoneId) &&
                            frame.Economy.Items.Any(i => "item:" + i.Id == b.Id && i.State == ItemState.Destroyed))
                            .OrderBy(b => b.Id, StringComparer.Ordinal).Select(b => b.Id).FirstOrDefault() ?? "";
                    complete = auxiliary != "";
                    return complete;
                case "BalanceWeight":
                    complete = site.FloorPadBounds.All(pad => CoversSwitch(recipe, pad));
                    return complete;
                case "BuildRoute":
                    if (evidence.Stage == MatchPhysicalStage.Waiting &&
                        !launches.ContainsKey(recipe.Canonical.Id + "/" + actor)) return false;
                    complete = evidence.Stage == MatchPhysicalStage.Complete || Inside(site.ArrivalBounds, player.Rigidbody.position) &&
                        ClearLine(player.Canonical, player.Rigidbody.position + Vector3.up * .2f,
                            Vector(WordboundPlatformPath.Metres(site.Authored.Target)) + Vector3.up * .2f, player, recipe);
                    return true;
                case "RedirectWind":
                    if (auxiliary == "")
                        auxiliary = controller.Bodies.Values.OrderBy(b => b.Canonical.Id, StringComparer.Ordinal)
                            .Where(b => WindReceiver(frame, team.Team, site, b) &&
                                Inside(site.ReceiverStartBounds, Vector(frame.Bodies.Single(p => p.Id == b.Canonical.Id).Position)) &&
                                windPushes.Contains(recipe.Canonical.Id + "/" + b.Canonical.Id))
                            .Select(b => b.Canonical.Id).FirstOrDefault() ?? "";
                    if (auxiliary == "" || !controller.Bodies.TryGetValue(auxiliary, out var receiver) ||
                        !WindReceiver(frame, team.Team, site, receiver)) return false;
                    complete = evidence.Stage == MatchPhysicalStage.Complete ||
                        Inside(site.ArrivalBounds, receiver.Rigidbody.position) &&
                        windPushes.Contains(recipe.Canonical.Id + "/" + auxiliary);
                    return true;
                case "TimedMechanism":
                    if (team.KeepsakeCarrier != actor) return false;
                    auxiliary = "keepsake:" + team.Team;
                    bool slowed = fixturePhases[PhaseKey(site, team.Team)] - frame.Mechanisms.Single(m =>
                        m.Team == team.Team && m.PuzzleId == site.PuzzleId && m.Lap == site.Lap).Physical.MotionUnits <
                        CourseFixtureMotion.UnitsPerTick;
                    if (!slowed || evidence.Stage == MatchPhysicalStage.Waiting && !ActorNear(actor, recipe.Rigidbody.position) ||
                        evidence.Stage == MatchPhysicalStage.Armed && frame.Progress.Tick + 1 - evidence.StageTick > site.ActiveWindowTicks)
                        return false;
                    complete = evidence.Stage == MatchPhysicalStage.Complete || Inside(site.ArrivalBounds, player.Rigidbody.position) &&
                        Supported(player.Canonical, player.Rigidbody.position + Vector3.up * .1f, player);
                    return true;
                default: return false;
            }
        }

        bool ActorNear(string actor, Vector3 at) =>
            (controller.Bodies["player:" + WordboundGameplayPlan.Seat(controller.Frame.Binding, actor)].Rigidbody.position - at).sqrMagnitude <= 9;

        bool WindReceiver(MatchSnapshot frame, int team, WordboundPhysicalSite site, WordboundNetworkBody body)
        {
            if (body.Canonical.Kind != WorldBodyKind.Item || !body.Canonical.Active || body.Rigidbody.isKinematic ||
                body.Canonical.OwnerTeam != team && body.Canonical.OwnerTeam != -1) return false;
            var item = frame.Economy.Items.SingleOrDefault(i => "item:" + i.Id == body.Canonical.Id);
            return item != null && item.Word == site.WindReceiverRecipeId &&
                (item.State == ItemState.World || item.State == ItemState.Thrown) &&
                (item.Origin == ItemOrigin.Crafted || proof.Course.Placements.Any(p =>
                    p.Id == body.Canonical.SourceId && p.ZoneId == site.WindReceiverZoneId));
        }

        bool CoversSwitch(WordboundNetworkBody body, CourseBox pad)
        {
            var min = Vector(WordboundPlatformPath.Metres(pad.Min));
            var max = Vector(WordboundPlatformPath.Metres(pad.Max));
            int count = scene.OverlapBox((min + max) / 2, (max - min) / 2, overlaps, Quaternion.identity, ~0,
                QueryTriggerInteraction.Ignore);
            if (count >= overlaps.Length) return false;
            for (int i = 0; i < count; i++)
                if (owned.TryGetValue(overlaps[i], out var candidate) && candidate == body)
                {
                    var collider = overlaps[i];
                    foreach (float x in new[] { min.x, max.x })
                        foreach (float z in new[] { min.z, max.z })
                        {
                            Vector3 point = new Vector3(x, max.y, z);
                            if ((collider.ClosestPoint(point) - point).sqrMagnitude > .0001f) return false;
                        }
                    return true;
                }
            return false;
        }

        static bool Inside(CourseBox box, Vector3 point) => point.x >= box.Min.X / 1000f && point.x <= box.Max.X / 1000f &&
            point.y >= box.Min.Y / 1000f && point.y <= box.Max.Y / 1000f && point.z >= box.Min.Z / 1000f && point.z <= box.Max.Z / 1000f;
    }
}
