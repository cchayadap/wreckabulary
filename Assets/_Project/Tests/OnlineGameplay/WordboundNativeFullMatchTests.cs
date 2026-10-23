using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.OnlineGameplayTests
{
    public sealed partial class WordboundNativeGameplayTests
    {
        const string FullPuzzle = "DeliverySocket";
        static readonly WorldVector FullSocketApproach = new WorldVector(0, 0, .5f);
        static readonly WorldVector FullCheckpoint = new WorldVector(-2, 0, -1);
        static readonly WorldVector FullFinish = new WorldVector(2, 0, -2);
        static readonly WorldVector FullHostParking = new WorldVector(4, 0, -2);

        [UnityTest, Category("ExternalPhotonFullMatch"), Timeout(600000)]
        public IEnumerator TwoAuthenticatedPeersCompleteCompactAuthoredTimeRunAndConfirmDurableResult()
        {
            if (Environment.GetEnvironmentVariable("WORDBOUND_EXTERNAL_FULL_MATCH") != "1")
                Assert.Ignore("Set WORDBOUND_EXTERNAL_FULL_MATCH=1 for compact private authored three-lap pipeline coverage.");
            return Run(2, false, authoredWorkshop: true, authoredContentFactory: CreateCompactTimeRun,
                afterStarted: CompleteCompactTimeRun);
        }

        static WordboundClientContent CreateCompactTimeRun(string projectRoot)
        {
            string folder = Path.Combine(projectRoot, "Assets/_Project/Data/Config");
            string items = File.ReadAllText(Path.Combine(folder, "items.json"));
            string rules = File.ReadAllText(Path.Combine(folder, "rules.json"));
            var geometry = WordboundFlagshipContent.Create(ItemCatalogue.FromJson(items)).Geometry;
            var document = new CourseDocument {
                Schema = 2, Id = "native-full-timerun-" + Guid.NewGuid().ToString("N").Substring(0, 12),
                Title = "Compact native Time Run pipeline proof", SupportedModes = new List<string> { "TimeRun" }
            };
            document.Layers.Add(new CourseLayer { Id = "Ground" });
            document.Modules.Add(new CourseModule { Id = "Room", DefinitionId = "FlagshipRoom", LayerId = "Ground" });
            for (int seat = 0; seat < 4; seat++)
                document.Spawns.Add(new CourseSpawn {
                    Id = "Start" + seat, ModuleId = "Room", Position = new CoursePoint(-3000 + seat * 2000, 0, -4000)
                });
            document.RecoveryAnchors.Add(new CourseRecovery {
                Id = "SocketCheckpoint", ModuleId = "Room", Position = new CoursePoint(-2000, 0, -1000), RadiusMm = 800
            });
            document.RecoveryAnchors.Add(new CourseRecovery {
                Id = "DeliveryCheckpoint", ModuleId = "Room", Position = new CoursePoint(2000, 0, -2000), RadiusMm = 800
            });
            document.Supplies.Add(new CourseSupply {
                Id = "TableLetters", ModuleId = "Room", Position = new CoursePoint(-3000, 0, 1000), Letters = "TABLE"
            });
            document.Puzzles.Add(new CoursePuzzle {
                Id = FullPuzzle, ModuleId = "Room", PresetId = "PlaceObject", RecipeId = "TABLE", RequiredLetters = "TABLE",
                RecoverySupplyIds = new List<string> { "TableLetters" }, ConsumeLetters = true
            });
            document.Checkpoints.Add(new CourseCheckpoint {
                Id = "SocketDone", ModuleId = "Room", RecoveryAnchorId = "SocketCheckpoint", PuzzleIds = new List<string> { FullPuzzle }
            });
            document.Checkpoints.Add(new CourseCheckpoint {
                Id = "Delivery", ModuleId = "Room", RecoveryAnchorId = "DeliveryCheckpoint"
            });
            document.RouteSections.Add(new CourseRouteSection {
                Id = "Circuit", EntryModuleId = "Room", ExitModuleId = "Room",
                Alternatives = new List<CourseAlternative> { new CourseAlternative {
                    Id = "GroundRoute", CheckpointIds = new List<string> { "SocketDone", "Delivery" }
                } }
            });
            string[] stories = { "FindAddress", "WrongDelivery", "RestoreHome" };
            for (int lap = 1; lap <= 3; lap++)
            {
                document.Laps.Add(new CourseLap {
                    Number = lap, StartModuleId = "Room", FinishModuleId = "Room", NarrativeId = stories[lap - 1],
                    SectionIds = new List<string> { "Circuit" }
                });
                document.PhysicalSites.Add(new CoursePhysicalSite {
                    Lap = lap, PuzzleId = FullPuzzle, CheckpointId = "SocketDone", RecoverySupplyId = "TableLetters",
                    Position = new CoursePoint(0, 0, 2000), Target = new CoursePoint(0, 0, 2000),
                    SocketRadiusMm = 500, YawTolerance = 15, HoldTicks = 30
                });
            }
            var mode = ModeDefinition.For("TimeRun");
            var resolved = CourseResolver.Resolve(document, geometry,
                new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(mode)));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            return WordboundClientContent.FromAuthored(CourseJson.ExportDocument(document), CourseJson.ExportManifest(resolved.Manifest),
                geometry, items, rules, mode.Id, mode.Format.ToString(), resolved.Manifest.Seed);
        }

        IEnumerator CompleteCompactTimeRun(Peer host)
        {
            var carrierPeer = peers.Single(peer => peer != host);
            Assert.AreEqual("TimeRun", live.Mode.Id);
            Assert.AreEqual(3, live.Mode.Laps);
            Assert.AreEqual(1500, live.Mode.TimeLimitSeconds);
            Assert.IsFalse(live.PublicQueue);
            Assert.AreEqual("Workshop", live.Verified.Match.Content.Kind);
            Assert.AreEqual(1, live.Course.Geometry.Count);
            Assert.AreEqual(3, live.Course.PhysicalSites.Count);
            Assert.AreEqual(2, live.Verified.Roster.Count);
            Assert.IsTrue(live.Verified.Roster.All(seat => seat.TeamId == 0));
            Assert.AreEqual(1, live.Verified.Roster.Select(seat => seat.PartyId).Distinct().Count());
            string document = CourseJson.ExportDocument(live.Course.CopyDocument());
            var observedLaps = new Dictionary<int, long>();
            var observedObjectives = new HashSet<int>();
            var solvedItems = new HashSet<string>(StringComparer.Ordinal);
            long lastTick = host.Controller.Frame.Progress.Tick;
            int lastLap = 0, lastObjectives = 0;
            string observationError = "";
            void Observe(MatchSnapshot frame)
            {
                var team = frame.Progress.Teams.Single();
                if (frame.Progress.Tick < lastTick || team.Lap < lastLap || team.Lap > lastLap + 1 ||
                    team.OrderedObjectives < lastObjectives || team.OrderedObjectives > lastObjectives + 1)
                    observationError = "Canonical race progression skipped or regressed.";
                observedObjectives.Add(team.OrderedObjectives);
                if (team.Lap > lastLap)
                {
                    var player = frame.Bodies.Single(body => body.Kind == WorldBodyKind.Player && body.SourceId == carrierPeer.Actor);
                    if (team.KeepsakeCarrier != carrierPeer.Actor || player.Position.DistanceSquared(FullFinish) > .8f * .8f)
                        observationError = "Lap transition lacked the actual keepsake carrier at the authored finish.";
                    observedLaps[team.Lap] = frame.Progress.Tick;
                }
                lastTick = frame.Progress.Tick; lastLap = team.Lap; lastObjectives = team.OrderedObjectives;
            }
            Observe(host.Controller.Frame);
            host.Controller.FrameChanged += Observe;
            try
            {
                Assert.AreEqual(0, FullTeam(host).Lap);
                Assert.AreEqual(0, FullTeam(host).NextCheckpoint);
                Assert.IsNull(host.Controller.Frame.Progress.Result);
                yield return FullWalk(host, carrierPeer, () => Pose(host, "keepsake:0").Position, .8f, "Reach the native keepsake");
                yield return Command(host, carrierPeer, WordboundAction.TakeKeepsake, "keepsake:0");
                Assert.AreEqual(carrierPeer.Actor, FullTeam(host).KeepsakeCarrier);
                yield return FullWalk(host, carrierPeer, () => new WorldVector(-4, 0, -3), .25f, "Carry the keepsake clear of the workbench");

                for (int lap = 0; lap < 3; lap++)
                {
                    yield return FullWalk(host, carrierPeer, () => new WorldVector(-4, 0, -3), .25f,
                        "Carry the keepsake away from the preceding lap's finish");
                    var crafter = lap == 1 ? carrierPeer : host;
                    int seat = WordboundGameplayPlan.Seat(host.Controller.Frame.Binding, crafter.Actor);
                    Assert.AreEqual(lap, FullTeam(host).Lap);
                    Assert.AreEqual(0, FullTeam(host).NextCheckpoint);
                    Assert.IsNull(host.Controller.Frame.Progress.Result);
                    var source = live.Course.Supplies.Single(supply => supply.SourceId == "TableLetters" && supply.TeamIndex == 0);
                    yield return Until(() => host.Controller.Frame.Bodies.Count(body => body.Kind == WorldBodyKind.Tile &&
                        body.SourceId == source.Id) == 5, "Expose the current lap's real TABLE letters", 15);
                    var letters = host.Controller.Frame.Bodies.Where(body => body.Kind == WorldBodyKind.Tile && body.SourceId == source.Id)
                        .Select(body => body.Id).ToArray();
                    foreach (string id in letters)
                    {
                        yield return FullWalk(host, crafter, () => Pose(host, id).Position, .85f, "Reach each native letter through admitted input");
                        yield return Command(host, crafter, WordboundAction.CollectLetter, id);
                    }
                    Assert.IsTrue(Inventory(host, crafter).Letters.Equals(LetterBag.FromWord("TABLE")));
                    int firstItem = host.Controller.Frame.Economy.NextItemId;
                    yield return Command(host, crafter, WordboundAction.BeginCraft, value: "TABLE");
                    yield return Until(() => host.Controller.Frame.Economy.Items.Any(item => item.Id >= firstItem && item.Word == "TABLE" &&
                        item.Origin == ItemOrigin.Crafted && item.HolderId == seat && item.State == ItemState.Held),
                        "Complete the unchanged TABLE crafting timer", 20);
                    var table = host.Controller.Frame.Economy.Items.Single(item => item.Id >= firstItem && item.Word == "TABLE" &&
                        item.Origin == ItemOrigin.Crafted && item.HolderId == seat && item.State == ItemState.Held);
                    string tableId = "item:" + table.Id;
                    Assert.IsTrue(solvedItems.Add(tableId), "Every lap must craft a fresh consumed objective item.");
                    yield return FullWalk(host, crafter, () => FullSocketApproach, .08f, "Approach the authored socket without repositioning");
                    crafter.InteractionInput = new WordboundMovementInput { HasFacing = true, FacingYaw = 0 };
                    yield return Until(() => FullFacesSocket(host, crafter), "Turn toward the socket through admitted facing input", 10);
                    int slot = Array.IndexOf(Inventory(host, crafter).Slots, table.Id);
                    Assert.GreaterOrEqual(slot, 0);
                    yield return Command(host, crafter, WordboundAction.Deploy, slot: slot);
                    crafter.InteractionInput = default;
                    Assert.AreEqual(ItemState.Deployed, Item(host, tableId).State);
                    Assert.IsTrue(host.Controller.Bodies[tableId].Rigidbody.isKinematic);
                    int currentLap = lap;
                    yield return Until(() => FullMechanism(host, currentLap).Physical.Stage == MatchPhysicalStage.Complete,
                        "Hold the real native TABLE collider inside the socket", 15);
                    var mechanism = FullMechanism(host, lap);
                    Assert.AreEqual(tableId, mechanism.ItemEntityId);
                    Assert.GreaterOrEqual(mechanism.ProgressTicks, 30);
                    Assert.AreEqual(live.Course.PhysicalSites.Single(site => site.Lap == lap + 1).Hash, mechanism.Physical.SiteHash);
                    yield return Command(host, crafter, WordboundAction.UsePuzzle, FullPuzzle, value: tableId);
                    Assert.IsTrue(FullMechanism(host, lap).Solved);
                    Assert.IsFalse(Pose(host, tableId).Active);
                    yield return FullWalk(host, crafter, () => FullCheckpoint, .25f, "Cross the supported ordered checkpoint");
                    yield return Until(() => FullTeam(host).NextCheckpoint == 1, "Observe the native first checkpoint", 10);

                    yield return FullWalk(host, host, () => FullFinish, .25f, "Reach the finish without the keepsake carrier");
                    yield return Until(() => FullTeam(host).NextCheckpoint == 2, "Observe the native delivery checkpoint", 10);
                    long waitFrom = host.Controller.Frame.Progress.Tick;
                    yield return Until(() => host.Controller.Frame.Progress.Tick >= waitFrom + 60,
                        "Keep the non-carrier at the finish for sixty ordinary ticks", 10);
                    Assert.AreEqual(lap, FullTeam(host).Lap, "A teammate without the keepsake must not complete the lap.");
                    Assert.IsNull(host.Controller.Frame.Progress.Result);
                    yield return FullWalk(host, host, () => FullHostParking, .25f, "Leave the finish clear for the keepsake carrier");
                    yield return FullWalk(host, carrierPeer, () => FullFinish, .4f,
                        "Deliver the keepsake through native movement and support", () => FullTeam(host).Lap == currentLap + 1);
                    yield return Until(() => FullTeam(host).Lap == currentLap + 1, "Observe the natural keepsake lap transition", 10);
                    yield return CommittedState(host, frame => frame.Progress.Teams.Single().Lap == currentLap + 1,
                        "Commit and acknowledge the completed lap on both authenticated peers");
                    Assert.IsEmpty(observationError);
                    Debug.Log("WORDBOUND_COMPACT_LAP_OBSERVED: lap=" + (lap + 1) + "; tick=" + observedLaps[lap + 1] +
                        "; orderedObjectives=" + FullTeam(host).OrderedObjectives + "; nativeSocket=true; nativeCarrierFinish=true; cleanupPending=true");
                }

                yield return Until(() => peers.All(peer => peer.Controller.Frame.Progress.Result?.Confirmed == true),
                    "Confirm the natural terminal result on host and replica", 25);
                var result = host.Controller.Frame.Progress.Result;
                CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, observedLaps.Keys);
                CollectionAssert.AreEquivalent(Enumerable.Range(0, 10), observedObjectives);
                Assert.AreEqual(9, FullTeam(host).OrderedObjectives);
                Assert.AreEqual(3, solvedItems.Count);
                foreach (var peer in peers) AssertFullResult(peer.Controller.Frame, result.Tick);
                yield return VerifyFullDurableResult(host, carrierPeer, result.Tick);
                Assert.IsEmpty(observationError);
                Assert.AreEqual(document, CourseJson.ExportDocument(live.Course.CopyDocument()));
                Assert.Greater(host.PhysicalSteps, 0); Assert.Greater(host.PhysicalObservations, 0);
                Assert.Greater(host.MovingInputReads, 0); Assert.Greater(carrierPeer.MovingInputReads, 0);
                Assert.AreEqual(0, carrierPeer.Advanced);
                Debug.Log("WORDBOUND_COMPACT_FULL_MATCH_OBSERVED: TimeRun; private authored map; two authenticated scripted clients; " +
                    "three fresh crafted/consumed TABLE objectives; three native carrier deliveries; confirmed Win on both peers; " +
                    "exact durable checkpoint downloaded and imported; no traversal teleports or direct progress injection; " +
                    "not public flagship or human play acceptance; cleanup pending.");
            }
            finally
            {
                host.Controller.FrameChanged -= Observe;
                foreach (var peer in peers) { peer.ScriptedMovement = null; peer.InteractionInput = default; }
            }
        }

        IEnumerator FullWalk(Peer host, Peer actor, Func<WorldVector> target, float radius, string operation,
            Func<bool> delivered = null)
        {
            int samples = 0;
            float minimumInput = float.PositiveInfinity, maximumInput = 0, lastInput = 0;
            actor.ScriptedMovement = () => {
                samples++; lastInput = 0;
                if (delivered?.Invoke() == true) return default;
                var body = host.Controller.Bodies["player:" + WordboundGameplayPlan.Seat(host.Controller.Frame.Binding, actor.Actor)];
                var at = body.Rigidbody.position; var goal = target();
                var delta = new Vector2(goal.X - at.x, goal.Z - at.z);
                if (delta.magnitude <= radius) return default;
                var value = delta.normalized * Mathf.Clamp((delta.magnitude - radius * .5f) * .07f, .06f, .22f);
                lastInput = value.magnitude;
                minimumInput = Mathf.Min(minimumInput, lastInput); maximumInput = Mathf.Max(maximumInput, lastInput);
                return new WordboundMovementInput { MoveX = value.x, MoveZ = value.y };
            };
            try
            {
                yield return Until(() => delivered?.Invoke() == true || HorizontalDistance(PlayerPose(host, actor).Position, target()) <= radius,
                    operation, 35);
            }
            finally
            {
                actor.ScriptedMovement = null; actor.InteractionInput = default;
                try
                {
                    var body = host.Controller.Bodies["player:" + WordboundGameplayPlan.Seat(host.Controller.Frame.Binding, actor.Actor)].Rigidbody;
                    var position = body.position; var velocity = body.linearVelocity; var goal = target();
                    float distance = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(goal.X, goal.Z));
                    bool canMove = host.Controller.PhysicsWorld.CanMove(actor.Actor, out float moveScale);
                    Debug.Log(FormattableString.Invariant($"WORDBOUND_COMPACT_WALK: operation={operation}; phase=steering-end; tick={host.Controller.Frame.Progress.Tick}; ") +
                        FormattableString.Invariant($"hostPosition={position.x:F6}/{position.y:F6}/{position.z:F6}; hostVelocity={velocity.x:F6}/{velocity.y:F6}/{velocity.z:F6}; ") +
                        FormattableString.Invariant($"target={goal.X:F3}/{goal.Y:F3}/{goal.Z:F3}; distance={distance:F6}; radius={radius:F3}; inside={distance <= radius}; ") +
                        FormattableString.Invariant($"delivered={delivered?.Invoke() == true}; samples={samples}; nonzeroObserved={maximumInput > 0}; minNonzeroInput={(float.IsPositiveInfinity(minimumInput) ? 0 : minimumInput):F6}; ") +
                        FormattableString.Invariant($"maxInput={maximumInput:F6}; lastInput={lastInput:F6}; canMove={canMove}; moveScale={moveScale:F6}; kinematic={body.isKinematic}; ") +
                        FormattableString.Invariant($"paused={host.Controller.IsSimulationPaused}; lifetimeCancelled={lifetime.IsCancellationRequested}; poseSource=host-native; inputs=requested-not-delivery-proof"));
                }
                catch (Exception) { Debug.Log("WORDBOUND_COMPACT_WALK_UNAVAILABLE: observation-failed"); }
            }
            if (host.Controller.Frame.Progress.Result == null)
                yield return Until(() => {
                    var velocity = PlayerPose(host, actor).Velocity;
                    return velocity.X * velocity.X + velocity.Z * velocity.Z < .01f;
                }, "Stop through normal player motor and friction", 10);
            Assert.IsTrue(delivered?.Invoke() == true ||
                HorizontalDistance(PlayerPose(host, actor).Position, target()) <= radius + .2f,
                "Ordinary input must settle near the requested point or trigger the actual carrier delivery sensor.");
            Assert.AreEqual(0, host.Controller.Frame.Progress.Players.Single(player => player.Id == actor.Actor).Knockouts);
        }

        static float HorizontalDistance(WorldVector a, WorldVector b) => new Vector2(a.X - b.X, a.Z - b.Z).magnitude;
        static TeamProgress FullTeam(Peer host) => host.Controller.Frame.Progress.Teams.Single();
        static MatchMechanismState FullMechanism(Peer host, int lap) => host.Controller.Frame.Mechanisms.Single(
            mechanism => mechanism.Team == 0 && mechanism.Lap == lap && mechanism.PuzzleId == FullPuzzle);
        static bool FullFacesSocket(Peer host, Peer actor)
        {
            var body = host.Controller.Bodies["player:" + WordboundGameplayPlan.Seat(host.Controller.Frame.Binding, actor.Actor)];
            return Vector3.Dot(body.Rigidbody.rotation * Vector3.forward, Vector3.forward) > .999f &&
                new Vector2(body.Rigidbody.linearVelocity.x, body.Rigidbody.linearVelocity.z).sqrMagnitude < .01f;
        }
        static void AssertFullResult(MatchSnapshot frame, long resultTick)
        {
            Assert.NotNull(frame.Progress.Result);
            Assert.AreEqual("Win", frame.Progress.Result.Outcome);
            CollectionAssert.AreEqual(new[] { 0 }, frame.Progress.Result.WinningTeams);
            Assert.AreEqual(resultTick, frame.Progress.Result.Tick);
            Assert.IsTrue(frame.Progress.Result.Confirmed);
            Assert.AreEqual(3, frame.Progress.Teams.Single().Lap);
            Assert.IsTrue(frame.Progress.Teams.Single().Finished);
            Assert.AreEqual(9, frame.Progress.Teams.Single().OrderedObjectives);
            Assert.AreEqual(3, frame.Mechanisms.Count(mechanism => mechanism.Solved));
        }
        IEnumerator VerifyFullDurableResult(Peer host, Peer client, long tick)
        {
            int index = live.Clients.FindIndex(value => value.Credentials.PlayerId == client.Actor);
            var service = new OnlineCheckpointService(live.Clients[index], live.Expected, host.Controller.Authority.IdentityHash);
            var head = service.Head(lifetime.Token);
            yield return Wait(head, "Read the real durable terminal checkpoint head", 20);
            Assert.NotNull(head.Result.Head);
            var manifest = head.Result.Head;
            Assert.GreaterOrEqual(manifest.Mark.Tick, tick);
            var download = service.Download(manifest, lifetime.Token);
            yield return Wait(download, "Independently download the durable terminal checkpoint", 20);
            var imported = MatchSnapshotCodec.Import(download.Result, host.Controller.Frame.Binding, live.Mode, live.Course, live.Items, live.Rules);
            Assert.IsTrue(imported.Success, imported.Error);
            Assert.AreEqual(manifest.Mark.StateHash, imported.Hash);
            AssertFullResult(imported.Snapshot, tick);
            yield return Until(() => client.Received.Values.Any(mark => mark.Same(manifest.Mark)) &&
                host.Acknowledged.TryGetValue(Key(client.Actor, manifest.Mark.Sequence), out var ack) && ack.Same(manifest.Mark),
                "Acknowledge the exact downloaded terminal checkpoint on the replica", 25);
        }
    }
}
