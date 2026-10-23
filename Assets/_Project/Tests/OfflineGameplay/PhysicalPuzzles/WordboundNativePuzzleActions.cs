using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Assert = NUnit.Framework.Assert;

namespace Wreckabulary.OfflineGameplayTests
{
    public sealed partial class WordboundNativePuzzleFlow
    {
        const float ClockWaypointTolerance = .03f;
        RouteObservation activeRoute;
        int beforeTickCallbacks, inputPolls;
        long readyInputRunnerTick = -1, readyInputCanonicalTick = -1;
        string inputReadinessError = "";

        sealed class RouteObservation
        {
            public ResolvedCoursePhysicalSite Site;
            public Vector3[] Targets;
            public Vector3[] ClosestPositions;
            public float[] Closest;
            public long[] ClosestTick, CanonicalInsideTick, CoroutineInsideTick, SegmentSkipTick, WaypointAdvanceTick;
            public long Epoch, StartedTick, LastTick, LastMotion, MaximumTickGap;
            public double StartedAt, Deadline;
            public Vector3 LastPosition;
            public int Waypoint, Samples, CoroutineSamples, MaximumSamplesPerFrame, SamplesThisFrame, LastFrame = -1;
            public int RawArrivalSamples, SharedArrivalSamples, SlowedSamples, CarrierMissingSamples, NonAdvancingCallbacks, OmittedTransitions;
            public MatchPhysicalStage LastStage;
            public readonly List<string> Transitions = new List<string>();
            public string Failure = "", FirstArrivalSupport = "not observed";
            public string SteeringFailure = "", SteeringStopped = "";
            public long LastInjectedRunnerTick = -1, LastSteeredCanonicalTick = -1, MaximumSteeringCanonicalGap;
            public int Injections, PostPhysicsInputMatches, PostPhysicsInputMismatches, Polls, LastPollFrame = -1, PollsThisFrame, MaximumPollsPerFrame;
            public WordboundMovementInput InjectedInput;
            public string ClockFixtureId = "", ClockObservationFailure = "";
            public int ClockDecisions, ClockTickObservations, OmittedClockTransitions;
            public long FirstClockTick = -1, LastClockTick = -1;
            public readonly long[] ClockKindCounts = new long[Enum.GetValues(typeof(WordboundClockDecisionKind)).Length];
            public readonly WordboundClockDecision?[] FirstClockKinds = new WordboundClockDecision?[Enum.GetValues(typeof(WordboundClockDecisionKind)).Length];
            public readonly List<WordboundClockDecision> ClockTransitions = new List<WordboundClockDecision>();
            public WordboundClockDecision? LastClockDecision;
        }

        void ObserveRouteClock(RouteObservation route)
        {
            if (content.Course.Puzzles.Single(p => p.Id == route.Site.PuzzleId).PresetId != "TimedMechanism") return;
            Assert.IsNull(runtime.NativeClockDecision, "The fixture must own its CLOCK observation subscription.");
            Assert.IsEmpty(runtime.NativeClockDiagnosticError);
            route.ClockFixtureId = CourseFixtureMotion.BodyId(route.Site.Fixtures.Single().Id, 0);
            runtime.NativeClockFixtureId = route.ClockFixtureId;
            runtime.NativeClockDecision = RecordClockDecision;
        }

        void RecordClockDecision(WordboundClockDecision decision)
        {
            var route = activeRoute;
            if (route == null || route.ClockFixtureId == "") return;
            if (decision.Epoch != route.Epoch) route.ClockObservationFailure = "ClockEpochChanged";
            if (decision.CompletedTick < route.LastClockTick) route.ClockObservationFailure = "ClockTickRegressed";
            if (route.FirstClockTick < 0) route.FirstClockTick = decision.CompletedTick;
            if (decision.CompletedTick != route.LastClockTick) route.ClockTickObservations++;
            route.LastClockTick = decision.CompletedTick;
            route.ClockDecisions++;
            int kind = (int)decision.Kind;
            route.ClockKindCounts[kind]++;
            if (!route.FirstClockKinds[kind].HasValue) route.FirstClockKinds[kind] = decision;
            bool changed = !route.LastClockDecision.HasValue ||
                route.LastClockDecision.Value.Kind != decision.Kind ||
                route.LastClockDecision.Value.SourceInstance != decision.SourceInstance ||
                route.LastClockDecision.Value.FixtureInstance != decision.FixtureInstance ||
                route.LastClockDecision.Value.Line.BlockerInstance != decision.Line.BlockerInstance;
            if (changed)
            {
                if (route.ClockTransitions.Count == 24)
                { route.ClockTransitions.RemoveAt(0); route.OmittedClockTransitions++; }
                route.ClockTransitions.Add(decision);
            }
            route.LastClockDecision = decision;
        }

        void DetachRouteClock()
        {
            if (runtime == null) return;
            runtime.NativeClockDecision = null;
            runtime.NativeClockFixtureId = null;
        }

        static string ClockDecisionDetails(WordboundClockDecision decision)
        {
            var line = decision.Line;
            return "prePhysicsCompletedTick=" + decision.CompletedTick + ",epoch=" + decision.Epoch + ",kind=" + decision.Kind +
                ",sourceInstance=" + decision.SourceInstance + ",fixtureInstance=" + decision.FixtureInstance +
                ",collision=" + decision.CollisionEvaluated + "/" + decision.CollisionAllowed +
                ",containment=" + decision.ContainmentEvaluated + "/" + decision.ContainmentAllowed +
                ",sourcePosition=" + Vector(decision.SourcePosition).ToString("F6") + ",fixturePosition=" + Vector(decision.FixturePosition).ToString("F6") +
                ",radius=" + decision.Radius.ToString("F6") + ",arcDegrees=" + decision.ArcDegrees.ToString("F6") +
                ",lineEvaluated=" + line.Evaluated + ",lineFrom=" + line.From.ToString("F6") + ",lineTo=" + line.To.ToString("F6") +
                ",lineHits=" + line.HitCount + "/" + line.Capacity + ",lineBufferFull=" + line.BufferFull +
                ",hasBlocker=" + line.HasBlocker + ",blockerInstance=" + line.BlockerInstance + ",blockerLayer=" + line.BlockerLayer +
                ",blockerDistance=" + line.BlockerDistance.ToString("F6") + ",blockerCourseSurface=" + line.BlockerCourseSurface +
                ",blockerRegisteredBody=" + line.BlockerRegisteredBody + ",blockerKind=" + line.BlockerKind;
        }

        string ClockDetails(RouteObservation route)
        {
            if (route.ClockFixtureId == "") return "; clockDecisionObservation=not requested";
            return "; clockFixture=" + route.ClockFixtureId + "; clockDecisions=" + route.ClockDecisions +
                "; clockTickObservations=" + route.ClockTickObservations + "; firstClockPrePhysicsTick=" + route.FirstClockTick +
                "; lastClockPrePhysicsTick=" + route.LastClockTick + "; clockRuntimeObserverError=" + runtime.NativeClockDiagnosticError +
                "; clockFixtureObserverError=" + route.ClockObservationFailure + "; clockKindCounts=" +
                string.Join(",", route.ClockKindCounts.Select((count, index) => ((WordboundClockDecisionKind)index) + ":" + count)) +
                "; clockFirstKinds=" + string.Join(" | ", route.FirstClockKinds.Where(value => value.HasValue).Select(value => ClockDecisionDetails(value.Value))) +
                "; clockTransitionTail=" + string.Join(" | ", route.ClockTransitions.Select(ClockDecisionDetails)) +
                "; clockOmittedTransitions=" + route.OmittedClockTransitions + "; clockLastDecision=" +
                (route.LastClockDecision.HasValue ? ClockDecisionDetails(route.LastClockDecision.Value) : "none");
        }

        WordboundMovementInput ReadScriptedMovement()
        {
            inputPolls++;
            var route = activeRoute;
            if (route != null)
            {
                route.Polls++;
                route.PollsThisFrame = route.LastPollFrame == Time.frameCount ? route.PollsThisFrame + 1 : 1;
                route.LastPollFrame = Time.frameCount;
                route.MaximumPollsPerFrame = Math.Max(route.MaximumPollsPerFrame, route.PollsThisFrame);
            }
            return movement;
        }

        void SteerRouteTick(NetworkRunner runner)
        {
            beforeTickCallbacks++;
            var route = activeRoute;
            NetworkInput? raw = null;
            try
            {
                if (controller == null || controller.Frame == null || session == null || runner != session.Runner) return;
                if (route == null && readyInputRunnerTick >= 0) return;
                var frame = controller.Frame;
                if (runner.GameMode != GameMode.Single || !runner.IsForward)
                    throw new InvalidOperationException("Scripted traversal requires a forward local-only tick.");
                raw = runner.GetRawInputForPlayer(runner.LocalPlayer);
                if (!raw.HasValue || !raw.Value.IsValid || !raw.Value.TryGet(out WordboundMovementInput existing))
                {
                    if (route != null) throw new InvalidOperationException("The local tick has no typed movement input.");
                    return;
                }
                if (!WordboundPhysicsPolicy.TryMovement(existing, frame.Epoch, out _))
                    throw new InvalidOperationException("The polled local input failed epoch/movement validation.");
                if (readyInputRunnerTick < 0)
                { readyInputRunnerTick = runner.Tick.Raw; readyInputCanonicalTick = frame.Progress.Tick; }
                if (route == null) return;
                if (route.Epoch != frame.Epoch) throw new InvalidOperationException("The traversal epoch changed.");
                if (runner.Tick.Raw <= route.LastInjectedRunnerTick) throw new InvalidOperationException("The scripted input tick did not advance.");
                if (route.LastSteeredCanonicalTick >= 0)
                    route.MaximumSteeringCanonicalGap = Math.Max(route.MaximumSteeringCanonicalGap, frame.Progress.Tick - route.LastSteeredCanonicalTick);
                route.LastSteeredCanonicalTick = frame.Progress.Tick;
                WordboundMovementInput desired = new WordboundMovementInput { Epoch = frame.Epoch };
                if (route.SteeringFailure == "" && Time.realtimeSinceStartupAsDouble < route.Deadline && !lifetime.IsCancellationRequested &&
                    Mechanism(route.Site).Physical.Stage != MatchPhysicalStage.Complete)
                {
                    Vector2 at = Horizontal(Vector(Player.Position));
                    while (route.Waypoint < route.Targets.Length - 1 &&
                        Vector2.Distance(at, Horizontal(route.Targets[route.Waypoint])) <= ClockWaypointTolerance)
                    {
                        route.WaypointAdvanceTick[route.Waypoint] = frame.Progress.Tick;
                        route.Waypoint++;
                    }
                    Vector2 delta = Horizontal(route.Targets[route.Waypoint]) - at;
                    float tolerance = route.Waypoint < route.Targets.Length - 1 ? ClockWaypointTolerance : .08f;
                    Vector2 direction = delta.magnitude <= tolerance ? Vector2.zero : delta.normalized * Mathf.Clamp(delta.magnitude * .65f, .12f, .8f);
                    desired.MoveX = direction.x; desired.MoveZ = direction.y;
                }
                else if (route.SteeringStopped == "")
                    route.SteeringStopped = route.SteeringFailure != "" ? "failure" : lifetime.IsCancellationRequested ? "cancelled" :
                        Time.realtimeSinceStartupAsDouble >= route.Deadline ? "deadline" : "complete";
                if (!WordboundPhysicsPolicy.TryMovement(desired, frame.Epoch, out _) || !raw.Value.TrySet(desired))
                    throw new InvalidOperationException("The current local input could not accept validated steering.");
                movement = desired;
                route.InjectedInput = desired; route.LastInjectedRunnerTick = runner.Tick.Raw; route.Injections++;
            }
            catch (Exception error)
            {
                movement = default;
                if (route != null) route.SteeringFailure = error.GetType().Name;
                else inputReadinessError = error.GetType().Name;
                try
                {
                    if (raw.HasValue && raw.Value.IsValid)
                        raw.Value.TrySet(new WordboundMovementInput { Epoch = controller?.Frame?.Epoch ?? 0 });
                }
                catch (Exception neutralError)
                {
                    if (route != null) route.SteeringFailure += "/neutral:" + neutralError.GetType().Name;
                    else inputReadinessError += "/neutral:" + neutralError.GetType().Name;
                }
            }
        }

        void RecordRouteInputAfterPhysics()
        {
            var route = activeRoute;
            if (route == null) return;
            try
            {
                bool matches = session.Runner.Tick.Raw == route.LastInjectedRunnerTick &&
                    session.Runner.TryGetInputForPlayer(session.Runner.LocalPlayer, out WordboundMovementInput actual) &&
                    actual.Epoch == route.InjectedInput.Epoch && actual.MoveX == route.InjectedInput.MoveX && actual.MoveZ == route.InjectedInput.MoveZ &&
                    actual.Jump == route.InjectedInput.Jump && actual.HasFacing == route.InjectedInput.HasFacing && actual.FacingYaw == route.InjectedInput.FacingYaw;
                if (matches) route.PostPhysicsInputMatches++;
                else { route.PostPhysicsInputMismatches++; route.SteeringFailure = "PostPhysicsInputMismatch"; }
            }
            catch (Exception error) { route.SteeringFailure = error.GetType().Name; }
        }

        void RecordRouteFrame(MatchSnapshot frame)
        {
            var route = activeRoute;
            if (route == null || route.Failure != "") return;
            try
            {
                if (frame.Epoch != route.Epoch) { route.Failure = "EpochChanged"; return; }
                if (frame.Progress.Tick <= route.LastTick) { route.NonAdvancingCallbacks++; return; }
                var actor = frame.Bodies.Single(b => b.Kind == WorldBodyKind.Player && b.SourceId == Actor);
                var state = frame.Mechanisms.Single(m => m.Team == 0 && m.Lap == route.Site.Lap - 1 && m.PuzzleId == route.Site.PuzzleId);
                Vector3 at = Vector(actor.Position);
                long tick = frame.Progress.Tick, gap = tick - route.LastTick;
                route.MaximumTickGap = Math.Max(route.MaximumTickGap, gap);
                route.Samples++;
                route.SamplesThisFrame = route.LastFrame == Time.frameCount ? route.SamplesThisFrame + 1 : 1;
                route.MaximumSamplesPerFrame = Math.Max(route.MaximumSamplesPerFrame, route.SamplesThisFrame);
                route.LastFrame = Time.frameCount;
                int index = Math.Min(route.Waypoint, route.Targets.Length - 1);
                Vector2 target = Horizontal(route.Targets[index]), prior = Horizontal(route.LastPosition), next = Horizontal(at);
                float tolerance = index < route.Targets.Length - 1 ? ClockWaypointTolerance : .08f;
                float distance = Vector2.Distance(next, target);
                if (distance < route.Closest[index])
                { route.Closest[index] = distance; route.ClosestTick[index] = tick; route.ClosestPositions[index] = at; }
                if (distance <= tolerance && route.CanonicalInsideTick[index] < 0) route.CanonicalInsideTick[index] = tick;
                Vector2 segment = next - prior;
                float along = segment.sqrMagnitude <= .00000001f ? 0 : Mathf.Clamp01(Vector2.Dot(target - prior, segment) / segment.sqrMagnitude);
                if (gap == 1 && distance > tolerance && Vector2.Distance(prior, target) > tolerance &&
                    Vector2.Distance(prior + segment * along, target) <= tolerance && route.SegmentSkipTick[index] < 0)
                    route.SegmentSkipTick[index] = tick;
                bool raw = Inside(route.Site.ArrivalBounds, at);
                bool shared = CourseSensorBoundary.ContainsFootPoint(route.Site.ArrivalBounds, actor.Position);
                if (raw) route.RawArrivalSamples++;
                if (shared)
                {
                    route.SharedArrivalSamples++;
                    if (route.FirstArrivalSupport == "not observed")
                        route.FirstArrivalSupport = "tick=" + tick + "; " + RouteSupport(at);
                }
                long motionDelta = state.Physical.MotionUnits - route.LastMotion;
                if (gap == 1 && motionDelta > 0 && motionDelta < CourseFixtureMotion.UnitsPerTick) route.SlowedSamples++;
                if (frame.Progress.Teams.Single().KeepsakeCarrier != Actor) route.CarrierMissingSamples++;
                if (state.Physical.Stage != route.LastStage)
                {
                    if (route.Transitions.Count < 24)
                        route.Transitions.Add(tick + ":" + route.LastStage + "->" + state.Physical.Stage +
                            "@" + at.ToString("F6") + ",stageTick=" + state.Physical.StageTick + ",motionDelta=" + motionDelta + ",raw=" + raw + ",shared=" + shared);
                    else route.OmittedTransitions++;
                }
                route.LastStage = state.Physical.Stage; route.LastMotion = state.Physical.MotionUnits;
                route.LastTick = tick; route.LastPosition = at;
            }
            catch (Exception error) { route.Failure = error.GetType().Name; }
        }

        static Vector2 Horizontal(Vector3 point) => new Vector2(point.x, point.z);

        string RouteSupport(Vector3 at)
        {
            var hits = new RaycastHit[32];
            int count = controller.PhysicsWorld.PhysicsScene.Raycast(at + Vector3.up * .1f, Vector3.down,
                hits, .2f, ~0, QueryTriggerInteraction.Ignore);
            return "rawSupportRayCount=" + count + "; supportBufferFull=" + (count >= hits.Length) +
                "; rawSupportHits=" + string.Join(" | ", hits.Take(Math.Min(count, hits.Length)).Select(hit =>
                hit.collider.name + ":distance=" + hit.distance.ToString("F6") + ":normalY=" + hit.normal.y.ToString("F6") +
                ":courseSurface=" + controller.CourseWorld.Surfaces.Contains(hit.collider) +
                ":body=" + (hit.collider.GetComponentInParent<WordboundNetworkBody>()?.Canonical?.Id ?? "none")));
        }

        string RouteDetails(RouteObservation route)
        {
            try
            {
                var state = Mechanism(route.Site);
                var actor = controller.Bodies[Player.Id];
                string itemId = state.ItemEntityId != "" ? state.ItemEntityId : lastCrafted;
                var item = Frame.Economy.Items.SingleOrDefault(i => "item:" + i.Id == itemId);
                controller.Bodies.TryGetValue(itemId, out var recipe);
                var fixture = route.Site.Fixtures.FirstOrDefault();
                string arm = fixture == null ? "none" : controller.Bodies[CourseFixtureMotion.BodyId(fixture.Id, 0)].Rigidbody.position.ToString("F6");
                string points = string.Join(" | ", route.Targets.Select((point, i) => i + ":" + point.ToString("F6") +
                    ",closest=" + route.Closest[i].ToString("F6") + "@" + route.ClosestTick[i] + ":" + route.ClosestPositions[i].ToString("F6") +
                    ",canonicalInside=" + route.CanonicalInsideTick[i] + ",coroutineInside=" + route.CoroutineInsideTick[i] +
                    ",waypointAdvanced=" + route.WaypointAdvanceTick[i] + ",segmentSkip=" + route.SegmentSkipTick[i]));
                bool canMove = controller.PhysicsWorld.CanMove(Actor, out float moveScale);
                return RunDetails() + "; routeStartedTick=" + route.StartedTick + "; routeSeconds=" +
                    (Time.realtimeSinceStartupAsDouble - route.StartedAt).ToString("F3") + "; waypoint=" + route.Waypoint +
                    "; canonicalSamples=" + route.Samples + "; coroutineSamples=" + route.CoroutineSamples +
                    "; scriptedInput=before-tick-local-buffer; injections=" + route.Injections + "; inputPolls=" + route.Polls +
                    "; maximumInputPollsPerUnityFrame=" + route.MaximumPollsPerFrame + "; postPhysicsInputMatches=" + route.PostPhysicsInputMatches +
                    "; postPhysicsInputMismatches=" + route.PostPhysicsInputMismatches + "; lastInjectedRunnerTick=" + route.LastInjectedRunnerTick +
                    "; lastSteeredCanonicalTick=" + route.LastSteeredCanonicalTick + "; maximumSteeringCanonicalGap=" + route.MaximumSteeringCanonicalGap +
                    "; steeringFailure=" + route.SteeringFailure + "; steeringStopped=" + route.SteeringStopped +
                    "; maximumCanonicalTickGap=" + route.MaximumTickGap + "; maximumCanonicalSamplesPerUnityFrame=" + route.MaximumSamplesPerFrame +
                    "; nonAdvancingCallbacks=" + route.NonAdvancingCallbacks + "; omittedTransitions=" + route.OmittedTransitions +
                    "; observerError=" + route.Failure + "; rawArrivalSamples=" + route.RawArrivalSamples +
                    "; sharedArrivalSamples=" + route.SharedArrivalSamples + "; slowedSamples=" + route.SlowedSamples +
                    "; missingCarrierSamples=" + route.CarrierMissingSamples + "; carrier=" + Team.KeepsakeCarrier +
                    "; actorCanonical=" + Vector(Player.Position).ToString("F6") + "; actorNative=" + actor.Rigidbody.position.ToString("F6") +
                    "; actorVelocity=" + actor.Rigidbody.linearVelocity.ToString("F6") + "; canMove=" + canMove + "; moveScale=" + moveScale +
                    "; stage=" + state.Physical.Stage + "; stageTick=" + state.Physical.StageTick + "; activeWindowTicks=" + route.Site.ActiveWindowTicks +
                    "; progressTicks=" + state.ProgressTicks + "; recipe=" + itemId + "; recipeState=" + (item == null ? "missing" : item.State.ToString()) +
                    "; deploymentTick=" + (item?.DeploymentTick ?? -1) + "; lifetimeSeconds=" +
                    content.Items.Get(content.Course.Puzzles.Single(p => p.Id == route.Site.PuzzleId).RecipeId).Deploy.LifetimeSeconds +
                    "; recipeNative=" + (recipe ? recipe.Rigidbody.position.ToString("F6") : "missing") +
                    "; recipeActive=" + (recipe && recipe.Canonical.Active) + "; recipeKinematic=" + (recipe && recipe.Rigidbody.isKinematic) +
                    "; armNative=" + arm + "; targets=" + points +
                    "; transitions=" + string.Join(" | ", route.Transitions) + "; firstArrivalSupport=" + route.FirstArrivalSupport +
                    "; finalSupport=" + RouteSupport(actor.Rigidbody.position) + ClockDetails(route);
            }
            catch (Exception error) { return "routeDetailsUnavailable=" + error.GetType().Name; }
        }

        IEnumerator ReachObjective(ResolvedCoursePhysicalSite site)
        {
            var route = content.Course.Laps[0];
            int target = route.Checkpoints.ToList().FindIndex(c => c.Id == site.CheckpointId);
            Assert.GreaterOrEqual(target, Team.NextCheckpoint);
            while (Team.NextCheckpoint < target)
            {
                var checkpoint = route.Checkpoints[Team.NextCheckpoint];
                Assert.IsTrue(checkpoint.PuzzleIds.All(Team.CompletedPuzzles.Contains),
                    "Fixture room positioning cannot skip an unsolved objective.");
                var anchor = content.Course.RecoveryAnchors.Single(r => r.Id == checkpoint.RecoveryAnchorId);
                int before = Team.NextCheckpoint;
                yield return SetPlayer(Metres(anchor.Position) + Vector3.up * .03f, 0);
                yield return Until(() => Team.NextCheckpoint > before, "Observe real checkpoint support/volume", 8);
            }
            Assert.AreEqual(site.CheckpointId, route.Checkpoints[Team.NextCheckpoint].Id);
            Assert.AreEqual(MatchPhysicalStage.Waiting, Mechanism(site).Physical.Stage);
        }

        IEnumerator CompleteSite(ResolvedCoursePhysicalSite site)
        {
            var puzzle = content.Course.Puzzles.Single(p => p.Id == site.PuzzleId);
            string windReceiver = "";
            if (puzzle.PresetId == "CollectLetters")
            {
                yield return CollectSupply(site.ToolRecoverySupplyId);
                yield return Craft(site.ToolRecipeId);
                int toolSlot = Array.IndexOf(Inventory.Slots, Item(lastCrafted).Id);
                yield return Command(WordboundAction.Equip, slot: toolSlot);
                string marked = PlacementBody(site.PropSourceZoneId);
                Assert.AreNotEqual(ItemState.Destroyed, Item(marked).State);
                Assert.AreEqual(MatchPhysicalStage.Waiting, Mechanism(site).Physical.Stage);
                yield return SmashMarkedProp(marked);
                Assert.AreEqual(ItemState.Destroyed, Item(marked).State);
                var fragments = Frame.Bodies.Where(b => b.Kind == WorldBodyKind.Tile &&
                    b.SourceId == Body(marked).SourceId).Select(b => b.Id).ToArray();
                CollectionAssert.AreEquivalent(puzzle.RequiredLetters.ToCharArray(), fragments.Select(id =>
                    Frame.Economy.Tiles.Single(t => "tile:" + t.Key == id).Value));
                foreach (string fragment in fragments) yield return Collect(fragment);
            }
            else yield return CollectSupply(site.RecoverySupplyId);
            yield return Craft(puzzle.RecipeId);
            string recipe = lastCrafted;
            if (puzzle.PresetId == "RedirectWind")
            {
                windReceiver = PlacementBody(site.WindReceiverZoneId);
                yield return SetBody(windReceiver, Metres(site.Position) + Vector3.right * 3 + Vector3.up * .03f, site.Yaw);
            }
            if (puzzle.PresetId == "PlaceObject" || puzzle.PresetId == "BalanceWeight")
            {
                yield return Deploy(recipe, site, Vector3.right);
                yield return Command(WordboundAction.UsePuzzle, site.PuzzleId, value: recipe, expected: "MechanismIncomplete");
                Assert.AreEqual(MatchPhysicalStage.Waiting, Mechanism(site).Physical.Stage);
                yield return Command(WordboundAction.PickUp, recipe, Item(recipe).Revision);
            }
            yield return Deploy(recipe, site, Vector3.zero);
            if (puzzle.PresetId == "RedirectWind")
            {
                yield return Command(WordboundAction.UsePuzzle, site.PuzzleId, value: recipe, expected: "MechanismIncomplete");
                var placement = content.Course.Placements.Single(p => p.ZoneId == site.WindReceiverZoneId && p.TeamIndex == 0);
                var start = Metres(placement.Position) + Vector3.back * .15f + Vector3.up * .02f;
                yield return SetBody(windReceiver, start, placement.Yaw);
                yield return Until(() => Mechanism(site).Physical.Stage == MatchPhysicalStage.Complete,
                    "FAN physically transports the authored receiver into its arrival volume", 12,
                    () => WindDetails(site, recipe, windReceiver, start));
                Assert.AreEqual(windReceiver, Mechanism(site).Physical.AuxiliaryBodyId);
                Assert.Greater(Body(windReceiver).Position.DistanceSquared(new WorldVector(start.x, start.y, start.z)), .16f);
                Assert.IsTrue(observedStages.Contains(site.PuzzleId + "/" + MatchPhysicalStage.Armed));
            }
            else if (puzzle.PresetId == "TimedMechanism")
            {
                Assert.AreEqual(Actor, Team.KeepsakeCarrier);
                yield return Command(WordboundAction.UsePuzzle, site.PuzzleId, value: recipe, expected: "MechanismIncomplete");
                yield return Until(() => slowedTicks.TryGetValue(site.PuzzleId, out int count) && count >= 3,
                    "CLOCK changes the real sorting arm's canonical motion rate", 5);
                var fixture = site.Fixtures.Single();
                var arm = controller.Bodies[CourseFixtureMotion.BodyId(fixture.Id, 0)];
                Assert.IsTrue(arm.Rigidbody.isKinematic);
                Assert.IsTrue(arm.GetComponent<BoxCollider>().enabled);
                Assert.Less((arm.Rigidbody.position - Vector(CourseFixtureMotion.Position(fixture,
                    Mechanism(site).Physical.MotionUnits))).sqrMagnitude, .000001f);
                yield return MoveIntoArrival(site, "Cross slowed native sorting arm with the keepsake", 8,
                    ClockApproach(site, recipe));
                Assert.AreEqual("keepsake:0", Mechanism(site).Physical.AuxiliaryBodyId);
            }
            else if (puzzle.PresetId == "BuildRoute")
            {
                yield return Command(WordboundAction.UsePuzzle, site.PuzzleId, value: recipe, expected: "MechanismIncomplete");
                var pad = controller.Bodies[recipe];
                float height = pad.GetComponent<BoxCollider>().size.y;
                float launchStrength = content.Items.Get(puzzle.RecipeId).Deploy.Strength;
                var launch = new LaunchObservation { Site = site, ItemId = recipe, Epoch = Frame.Epoch,
                    AfterTick = Frame.Progress.Tick, MinimumVelocity = launchStrength * .6f };
                Assert.IsNull(pendingLaunch);
                pendingLaunch = launch;
                try
                {
                    yield return SetPlayer(pad.Rigidbody.position + Vector3.up * (height + .02f), site.Yaw);
                    yield return Until(() => launch.ObservedAt > launch.AfterTick,
                        "Capture actual upward BED velocity and bound physical evidence in the same host frame", 5);
                }
                finally { if (ReferenceEquals(pendingLaunch, launch)) pendingLaunch = null; }
                yield return MoveIntoArrival(site, "Move the launched actor into the raised arrival volume", 8);
                Assert.IsTrue(observedStages.Contains(site.PuzzleId + "/" + MatchPhysicalStage.Armed));
            }
            else
                yield return Until(() => Mechanism(site).Physical.Stage == MatchPhysicalStage.Complete,
                    puzzle.PresetId + " accumulates actual host-observed support/coverage", 8);
            var completed = Mechanism(site);
            Assert.AreEqual(MatchPhysicalStage.Complete, completed.Physical.Stage);
            Assert.AreEqual(site.Hash, completed.Physical.SiteHash);
            Assert.AreEqual(Actor, completed.Physical.ActorId);
            Assert.AreEqual(recipe, completed.ItemEntityId);
            Assert.IsTrue(observedStages.Contains(site.PuzzleId + "/" + MatchPhysicalStage.Complete));
            if (puzzle.PresetId == "BalanceWeight")
            {
                Assert.AreEqual(2, site.FloorPadBounds.Count);
                Assert.GreaterOrEqual(completed.ProgressTicks, site.HoldTicks);
                var collider = controller.Bodies[recipe].GetComponent<BoxCollider>();
                foreach (var pad in site.FloorPadBounds)
                    foreach (float x in new[] { pad.Min.X / 1000f, pad.Max.X / 1000f })
                        foreach (float z in new[] { pad.Min.Z / 1000f, pad.Max.Z / 1000f })
                        {
                            var point = new Vector3(x, pad.Max.Y / 1000f, z);
                            Assert.LessOrEqual((collider.ClosestPoint(point) - point).sqrMagnitude, .0001f);
                        }
            }
            if (puzzle.PresetId == "BuildRoute")
                yield return SetPlayer(Metres(site.Position) + Vector3.back * 1.5f + Vector3.up * .02f, site.Yaw);
            yield return Until(() => controller.PhysicsWorld.HasLineOfSight(Actor, Player.Position, Body(recipe).Position),
                "Reach the completed physical item through unobstructed native geometry", 5);
            yield return Command(WordboundAction.UsePuzzle, site.PuzzleId, value: recipe);
            Assert.IsTrue(Mechanism(site).Solved);
            Assert.IsTrue(Team.CompletedPuzzles.Contains(site.PuzzleId));
            Assert.AreEqual(ItemState.Destroyed, Item(recipe).State);
            Debug.Log("WORDBOUND_PHYSICAL_FAMILY: " + puzzle.PresetId + " " + site.PuzzleId +
                " completed at tick " + Frame.Progress.Tick + " with site hash " + site.Hash);
        }

        string WindDetails(ResolvedCoursePhysicalSite site, string recipe, string receiver, Vector3 start)
        {
            try { return ReadWindDetails(site, recipe, receiver, start); }
            catch (Exception error) { return "windDetailsUnavailable=" + error.GetType().Name; }
        }

        string ReadWindDetails(ResolvedCoursePhysicalSite site, string recipe, string receiver, Vector3 start)
        {
            var fan = controller.Bodies[recipe];
            var target = controller.Bodies[receiver];
            var state = Mechanism(site);
            var definition = content.Items.Get(Item(recipe).Word);
            var origin = fan.Rigidbody.position + Vector3.up * .5f;
            var delta = target.Rigidbody.worldCenterOfMass - origin;
            var hits = new RaycastHit[32];
            int count = delta.sqrMagnitude < .000001f ? 0 : controller.PhysicsWorld.PhysicsScene.Raycast(
                origin, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            return "windStage=" + state.Physical.Stage + "; activated=" + state.ActivatedTick +
                "; progressTicks=" + state.ProgressTicks + "; auxiliary=" + state.Physical.AuxiliaryBodyId +
                "; observedStages=" + string.Join(",", observedStages.Where(s => s.StartsWith(site.PuzzleId + "/", StringComparison.Ordinal))) +
                "; receiverStart=" + start.ToString("F3") + "; receiverCanonical=" + Vector(Body(receiver).Position).ToString("F3") +
                "; receiverNative=" + target.Rigidbody.position.ToString("F3") +
                "; receiverVelocity=" + target.Rigidbody.linearVelocity.ToString("F3") +
                "; receiverState=" + Item(receiver).State + "; receiverKinematic=" + target.Rigidbody.isKinematic +
                "; fanNative=" + fan.Rigidbody.position.ToString("F3") +
                "; fanForward=" + (fan.Rigidbody.rotation * Vector3.forward).ToString("F3") +
                "; fanState=" + Item(recipe).State + "; fanKinematic=" + fan.Rigidbody.isKinematic +
                "; fieldContainsCanonical=" + WordboundFieldPolicy.Contains(definition.Deploy, Body(recipe), Body(receiver)) +
                "; insideStart=" + Inside(site.ReceiverStartBounds, target.Rigidbody.position) +
                "; insideArrival=" + Inside(site.ArrivalBounds, target.Rigidbody.position) +
                "; arrivalMin=" + Metres(site.ArrivalBounds.Min).ToString("F3") +
                "; arrivalMax=" + Metres(site.ArrivalBounds.Max).ToString("F3") +
                "; rayHits=" + string.Join(",", hits.Take(count).Select(hit => hit.collider.name + "@" + hit.distance.ToString("F3")));
        }

        IEnumerator SmashMarkedProp(string bodyId)
        {
            var definition = content.Items.Get("HAMMER");
            int strikes = (int)Math.Ceiling(Item(bodyId).Durability / definition.Melee.BreakPower) + 2;
            for (int attempt = 0; attempt < strikes && Item(bodyId).State != ItemState.Destroyed; attempt++)
            {
                yield return Until(() => Frame.Progress.Tick >= Frame.Actors.Single(a => a.PlayerId == Actor).NextAttackTick,
                    "Respect the real HAMMER cooldown", 4);
                var at = Vector(Body(bodyId).Position);
                yield return SetPlayer(at + Vector3.back * 1.05f, 0);
                float previous = Item(bodyId).Durability;
                yield return Command(WordboundAction.Attack);
                yield return Until(() => Item(bodyId).Durability < previous, "Strike the marked native prop", 4);
                Assert.AreEqual(Math.Max(0, previous - definition.Melee.BreakPower), Item(bodyId).Durability, .001f);
            }
            Assert.AreEqual(ItemState.Destroyed, Item(bodyId).State, "Only actual melee may destroy the marked prop.");
        }

        IEnumerator CollectSupply(string sourceId)
        {
            var source = content.Course.Supplies.Single(s => s.SourceId == sourceId && s.TeamIndex == 0);
            yield return Until(() => Frame.Bodies.Count(b => b.Kind == WorldBodyKind.Tile && b.SourceId == source.Id) ==
                source.Letters.Length, "Expose the current authored recovery batch", 12);
            var ids = Frame.Bodies.Where(b => b.Kind == WorldBodyKind.Tile && b.SourceId == source.Id)
                .Select(b => b.Id).ToArray();
            foreach (string id in ids) yield return Collect(id);
        }

        IEnumerator Collect(string id)
        {
            var at = Vector(Body(id).Position);
            yield return SetPlayer(at + Vector3.back * .8f, 0);
            yield return Command(WordboundAction.CollectLetter, id);
            Assert.IsFalse(Frame.Bodies.Any(b => b.Id == id));
        }

        IEnumerator Craft(string recipeId)
        {
            var required = content.Items.Get(recipeId).Letters;
            Assert.IsTrue(Inventory.Letters.Contains(required));
            int prior = Frame.Economy.NextItemId;
            Debug.Log("WORDBOUND_PHYSICAL_CRAFT: begin " + CraftDetails(recipeId, prior));
            yield return Command(WordboundAction.BeginCraft, value: recipeId);
            Debug.Log("WORDBOUND_PHYSICAL_CRAFT: accepted " + CraftDetails(recipeId, prior));
            yield return Until(() => Frame.Economy.Items.Any(i => i.Id >= prior && i.Word == recipeId &&
                i.Origin == ItemOrigin.Crafted && i.HolderId == 0 && i.State == ItemState.Held),
                "Complete the unchanged " + recipeId + " craft timer", 20, () => CraftDetails(recipeId, prior));
            lastCrafted = "item:" + Frame.Economy.Items.Single(i => i.Id >= prior && i.Word == recipeId &&
                i.HolderId == 0 && i.State == ItemState.Held).Id;
            Assert.IsTrue(Inventory.Reserved.IsEmpty);
            Debug.Log("WORDBOUND_PHYSICAL_CRAFT: completed " + CraftDetails(recipeId, prior));
        }

        string CraftDetails(string recipeId, int firstItemId)
        {
            var frame = Frame;
            var inventory = frame.Economy.Players[0];
            var job = inventory.Craft;
            string timer = job == null ? "none" : job.ItemId + "@slot" + job.Slot +
                ",started=" + job.StartedAt.ToString("F6") + ",ready=" + job.ReadyAt.ToString("F6") +
                ",remainingTicks=" + Math.Max(0, Math.Ceiling(job.ReadyAt * ModeDefinition.TickRate) - frame.Progress.Tick);
            return recipeId + "; " + RunDetails() + "; secondsPerRecipe=" +
                content.Rules.CraftSeconds(content.Items.Get(recipeId)).ToString("F6") +
                "; firstItemId=" + firstItemId + "; nextItemId=" + frame.Economy.NextItemId +
                "; letters=" + inventory.Letters + "; reserved=" + inventory.Reserved +
                "; slots=" + string.Join(",", inventory.Slots) + "; carriedFurniture=" + inventory.CarriedFurniture +
                "; canAct=" + inventory.CanAct + "; craft=" + timer + "; matchingItems=" +
                string.Join(" | ", frame.Economy.Items.Where(i => i.Word == recipeId).Select(i =>
                    i.Id + ":" + i.Origin + ":" + i.State + ":holder=" + i.HolderId + ":crafter=" + i.CrafterId));
        }

        IEnumerator Deploy(string id, ResolvedCoursePhysicalSite site, Vector3 offset)
        {
            Vector3 forward = Quaternion.Euler(0, site.Yaw, 0) * Vector3.forward;
            yield return SetPlayer(Metres(site.Position) + offset - forward * 1.5f + Vector3.up * .02f, site.Yaw);
            yield return Ticks(3);
            int slot = Array.IndexOf(Inventory.Slots, Item(id).Id);
            Assert.GreaterOrEqual(slot, 0);
            yield return Command(WordboundAction.Deploy, slot: slot);
            Assert.AreEqual(ItemState.Deployed, Item(id).State);
            Assert.IsTrue(controller.Bodies[id].Rigidbody.isKinematic);
        }

        Vector3[] ClockApproach(ResolvedCoursePhysicalSite site, string recipe)
        {
            var clock = controller.Bodies[recipe];
            var box = clock.GetComponent<BoxCollider>();
            var capsule = controller.Bodies[Player.Id].GetComponent<CapsuleCollider>();
            Assert.IsNotNull(box); Assert.IsNotNull(capsule);
            var origin = clock.Rigidbody.position;
            var rotation = clock.Rigidbody.rotation;
            var inverse = Quaternion.Inverse(rotation);
            var start = inverse * (Vector(Player.Position) - origin);
            float side = box.size.x / 2 + capsule.radius + .12f;
            float forward = box.size.z / 2 + capsule.radius + .08f;
            var arm = site.Fixtures.Single();
            float beforeArm = Mathf.Min((inverse * (Metres(arm.From) - origin)).z,
                (inverse * (Metres(arm.To) - origin)).z) - arm.SizeMm.Z / 2000f - capsule.radius;
            Assert.Less(forward + ClockWaypointTolerance, beforeArm,
                "The CLOCK detour must rejoin before the real sorting arm, including player clearance.");
            var points = new[] {
                origin + rotation * new Vector3(side, start.y, start.z),
                origin + rotation * new Vector3(side, start.y, forward),
                origin + rotation * new Vector3(0, start.y, forward)
            };
            string moduleId = content.Course.Puzzles.Single(p => p.Id == site.PuzzleId).ModuleId;
            var room = content.Course.Geometry.Single(m => m.Id == moduleId).Bounds;
            foreach (var point in points)
            {
                Assert.GreaterOrEqual(point.x - capsule.radius, room.Min.X / 1000f);
                Assert.LessOrEqual(point.x + capsule.radius, room.Max.X / 1000f);
                Assert.GreaterOrEqual(point.z - capsule.radius, room.Min.Z / 1000f);
                Assert.LessOrEqual(point.z + capsule.radius, room.Max.Z / 1000f);
            }
            return points;
        }

        IEnumerator MoveIntoArrival(ResolvedCoursePhysicalSite site, string operation, double seconds,
            Vector3[] approach = null)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            bool entered = observedActorArrivals.Contains(site.Hash);
            var targets = (approach ?? Array.Empty<Vector3>()).Concat(new[] { Metres(site.Target) }).ToArray();
            var observation = new RouteObservation { Site = site, Targets = targets, Epoch = Frame.Epoch, StartedTick = Frame.Progress.Tick,
                LastTick = Frame.Progress.Tick, StartedAt = Time.realtimeSinceStartupAsDouble, Deadline = deadline, LastPosition = Vector(Player.Position),
                LastMotion = Mechanism(site).Physical.MotionUnits, LastStage = Mechanism(site).Physical.Stage,
                Closest = Enumerable.Repeat(float.PositiveInfinity, targets.Length).ToArray(),
                ClosestPositions = new Vector3[targets.Length],
                ClosestTick = Enumerable.Repeat(-1L, targets.Length).ToArray(), CanonicalInsideTick = Enumerable.Repeat(-1L, targets.Length).ToArray(),
                CoroutineInsideTick = Enumerable.Repeat(-1L, targets.Length).ToArray(), SegmentSkipTick = Enumerable.Repeat(-1L, targets.Length).ToArray(),
                WaypointAdvanceTick = Enumerable.Repeat(-1L, targets.Length).ToArray() };
            activeRoute = observation;
            try
            {
                ObserveRouteClock(observation);
                while (Mechanism(site).Physical.Stage != MatchPhysicalStage.Complete &&
                    Time.realtimeSinceStartupAsDouble < deadline && !lifetime.IsCancellationRequested)
                {
                    Healthy();
                    observation.CoroutineSamples++;
                    int waypoint = observation.Waypoint;
                    bool approaching = waypoint < targets.Length - 1;
                    Vector3 at = Vector(Player.Position), target = targets[waypoint];
                    entered |= observedActorArrivals.Contains(site.Hash) || Inside(site.ArrivalBounds, at);
                    Vector2 delta = new Vector2(target.x - at.x, target.z - at.z);
                    if (delta.magnitude <= (approaching ? ClockWaypointTolerance : .08f) && observation.CoroutineInsideTick[waypoint] < 0)
                        observation.CoroutineInsideTick[waypoint] = Frame.Progress.Tick;
                    yield return null;
                }
                movement = default;
                entered |= observedActorArrivals.Contains(site.Hash) || Inside(site.ArrivalBounds, Vector(Player.Position));
                Debug.Log("WORDBOUND_PHYSICAL_ROUTE: " + RouteDetails(observation));
                if (observation.ClockFixtureId != "")
                {
                    Assert.IsEmpty(runtime.NativeClockDiagnosticError, "The real CLOCK decision observer must not fail silently.");
                    Assert.IsEmpty(observation.ClockObservationFailure);
                    Assert.Greater(observation.ClockDecisions, 0, "CLOCK traversal must observe actual pre-physics field decisions.");
                }
                Assert.IsEmpty(observation.SteeringFailure, "Tick-local scripted input must remain valid.");
                Assert.Greater(observation.Injections, 0, "Traversal must use actual local input ticks.");
                Assert.AreEqual(observation.Injections, observation.PostPhysicsInputMatches,
                    "Every injected tick must retain the same normal motor input through its native physics step.");
                Assert.AreEqual(0, observation.PostPhysicsInputMismatches);
                Assert.AreEqual(MatchPhysicalStage.Complete, Mechanism(site).Physical.Stage, operation);
                Assert.IsTrue(entered, operation + " must include an observed actor position inside the authored volume.");
            }
            finally
            {
                movement = default;
                DetachRouteClock();
                if (ReferenceEquals(activeRoute, observation)) activeRoute = null;
            }
        }

        IEnumerator SetBody(string id, Vector3 at, float yaw)
        {
            var observation = BeginSetup(id, at);
            setup.Enqueue(() => {
                if (!ReferenceEquals(pendingSetup, observation)) return;
                var body = controller.Bodies[id].Rigidbody;
                Assert.IsFalse(body.isKinematic);
                body.position = at; body.rotation = Quaternion.Euler(0, yaw, 0);
                body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
                body.WakeUp();
                observation.Epoch = Frame.Epoch;
                observation.AppliedAt = Frame.Progress.Tick;
            });
            try { yield return AwaitSetup(observation); }
            finally { if (ReferenceEquals(pendingSetup, observation)) pendingSetup = null; }
        }

        IEnumerator SaveAndCheck(ResolvedCoursePhysicalSite site)
        {
            long completed = Frame.Progress.Tick;
            yield return Wait(session.PublishCheckpoint(), "Publish actual physical completion checkpoint", 8);
            Assert.GreaterOrEqual(session.Committed.Tick, completed);
            var imported = MatchSnapshotCodec.Import(session.CommittedJson, Frame.Binding, content.Mode,
                content.Course, content.Items, content.Rules);
            Assert.IsTrue(imported.Success, imported.Error);
            Assert.AreEqual(imported.Hash, session.Committed.StateHash);
            var mechanism = imported.Snapshot.Mechanisms.Single(m => m.PuzzleId == site.PuzzleId && m.Team == 0 && m.Lap == 0);
            Assert.IsTrue(mechanism.Solved);
            Assert.AreEqual(MatchPhysicalStage.Complete, mechanism.Physical.Stage);
            Assert.AreEqual(site.Hash, mechanism.Physical.SiteHash);
            Assert.LessOrEqual(imported.Snapshot.Bodies.Count, WordboundEntityBudget.MaximumBodies);
        }

        string PlacementBody(string zone)
        {
            var placement = content.Course.Placements.Single(p => p.ZoneId == zone && p.TeamIndex == 0);
            return Frame.Bodies.Single(b => b.Kind == WorldBodyKind.Item && b.SourceId == placement.Id).Id;
        }
    }
}
