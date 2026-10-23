using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        const int CapacityBodies = 366;

        [Test]
        public void AuthoredCapacityPreflightUsesTheFullBudgetAndPinnedPassiveHistoryMetadata()
        {
            CapacityHistory.Preflight();
            CreateCapacityCourse(Path.GetFullPath(Path.Combine(Application.dataPath, "..")));
        }

        [UnityTest, Category("ExternalPhotonWorkshopGameplay"), Timeout(600000)]
        public IEnumerator TwoAuthenticatedPeersReclaimAndReuseTheMaximumAuthoredBudget()
        {
            if (Environment.GetEnvironmentVariable("WORDBOUND_EXTERNAL_WORKSHOP_GAMEPLAY") != "1")
                Assert.Ignore("Set WORDBOUND_EXTERNAL_WORKSHOP_GAMEPLAY=1 for the signed private capacity and reclamation case.");
            CapacityHistory.Preflight();
            return Run(2, false, authoredWorkshop: true, authoredContentFactory: CreateCapacityCourse,
                afterStarted: ReclaimCapacityBodies);
        }

        static WordboundClientContent CreateCapacityCourse(string projectRoot)
        {
            var original = CreateCompactTimeRun(projectRoot);
            var document = original.Course.CopyDocument();
            document.Id = "native-capacity-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            document.Title = "Maximum admitted budget and actual body reclamation";
            document.Supplies.Add(new CourseSupply {
                Id = "CapacityReserve", ModuleId = "Room", Position = new CoursePoint(6000, 0, 6000), Letters = new string('A', 123)
            });
            document.Puzzles.Single(puzzle => puzzle.Id == FullPuzzle).RecoverySupplyIds.Add("CapacityReserve");
            document.Supplies.Add(new CourseSupply {
                Id = "CapacityOptional", ModuleId = "Room", Position = new CoursePoint(6000, 0, -6000), Letters = "BALL"
            });
            var centres = new[] { new CoursePoint(24000, 0, 0), new CoursePoint(-24000, 0, 0), new CoursePoint(0, 0, 24000) };
            var doors = new[] { new CoursePoint(12000, 0, 0), new CoursePoint(-12000, 0, 0), new CoursePoint(0, 0, 12000) };
            for (int room = 0; room < centres.Length; room++)
            {
                string id = "CapacityRoom" + room;
                document.Modules.Add(new CourseModule {
                    Id = id, DefinitionId = "FlagshipRoom", LayerId = "Ground", Position = centres[room]
                });
                document.Connections.Add(new CourseConnection {
                    Id = "CapacityGate" + room, FromModuleId = "Room", ToModuleId = id,
                    Kind = "Door", From = doors[room], To = doors[room], GatePuzzleId = FullPuzzle
                });
                for (int prop = 0; prop < 76; prop++)
                {
                    var point = new CoursePoint(centres[room].X - 9000 + prop % 19 * 1000, 0,
                        centres[room].Z + 5000 + prop / 19 * 1000);
                    document.Zones.Add(new CourseZone {
                        Id = "CapacityProp" + room + "_" + prop, ModuleId = id, Purpose = "Props", Placement = "Fixed",
                        FixedPosition = point, FixedYaw = 0, MinCount = 1, MaxCount = 1, SpacingMm = 100,
                        Bounds = new CourseBox(new CoursePoint(point.X - 300, 0, point.Z - 300),
                            new CoursePoint(point.X + 300, 1000, point.Z + 300)),
                        LayerIds = new List<string> { "Ground" }, Yaws = new List<int> { 0 },
                        Pool = new List<CourseWeightedAsset> { new CourseWeightedAsset { AssetId = "BALL" } }
                    });
                }
            }
            var mode = ModeDefinition.For("TimeRun");
            var resolved = CourseResolver.Resolve(document, original.Geometry,
                new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(mode)));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            string folder = Path.Combine(projectRoot, "Assets/_Project/Data/Config");
            var content = WordboundClientContent.FromAuthored(CourseJson.ExportDocument(document), CourseJson.ExportManifest(resolved.Manifest),
                original.Geometry, File.ReadAllText(Path.Combine(folder, "items.json")), File.ReadAllText(Path.Combine(folder, "rules.json")),
                mode.Id, mode.Format.ToString(), resolved.Manifest.Seed);
            var budget = new WordboundEntityBudget(mode, content.Course, content.Geometry, content.Items, content.Rules);
            Assert.AreEqual(236, budget.FixedBodies); Assert.AreEqual(1, budget.PoolCount);
            Assert.AreEqual(276, budget.PoolBodies); Assert.AreEqual(128, budget.CriticalRecoveryBodies);
            Assert.AreEqual(0, budget.PhysicalAuxiliaryBodies); Assert.AreEqual(144, budget.ActorAndBurstBodies);
            Assert.AreEqual(4, budget.OptionalBodies);
            Assert.AreEqual(512, budget.FixedBodies + budget.CriticalRecoveryBodies + budget.ActorAndBurstBodies + budget.OptionalBodies);
            var binding = new MatchBinding("capacity-preflight", "wordbound-v1", mode.Hash, content.Course.SourceHash,
                content.Course.Hash, WordboundIdentity.Sha256("capacity-roster"), content.PrivateBinding.CatalogHash,
                new[] { new WordboundSeat("capacity-a", 0), new WordboundSeat("capacity-b", 0, 1) });
            var authority = new AuthoritativeMatch(binding, mode, content.Course, content.Geometry, content.Items, content.Rules,
                new CapacityInitialQuery());
            var snapshot = authority.Capture(1);
            Assert.AreEqual("", MatchStateValidation.Validate(snapshot, binding, mode, content.Course, content.Items, content.Rules));
            AssertCapacityBodyCounts(snapshot);
            AssertCapacitySourceLetters(snapshot, content.Course, 0);
            return content;
        }

        sealed class CapacityInitialQuery : IWordboundWorldQuery
        {
            public bool HasLineOfSight(string actor, WorldVector from, WorldVector to) => throw new InvalidOperationException("Initial-state preflight cannot perform gameplay queries.");
            public bool CanDeploy(string actor, string recipe, WorldVector position) => throw new InvalidOperationException("Initial-state preflight cannot deploy equipment.");
            public bool PuzzleSatisfied(string actor, ResolvedCoursePuzzle puzzle, WorldBodyState item) => throw new InvalidOperationException("Initial-state preflight cannot solve a puzzle.");
        }

        static void AssertCapacityBodyCounts(MatchSnapshot frame)
        {
            Assert.AreEqual(CapacityBodies, frame.Bodies.Count);
            Assert.AreEqual(2, frame.Bodies.Count(body => body.Kind == WorldBodyKind.Player));
            Assert.AreEqual(1, frame.Bodies.Count(body => body.Kind == WorldBodyKind.Keepsake));
            Assert.AreEqual(3, frame.Bodies.Count(body => body.Kind == WorldBodyKind.Platform));
            Assert.AreEqual(228, frame.Bodies.Count(body => body.Kind == WorldBodyKind.Item));
            Assert.AreEqual(132, frame.Bodies.Count(body => body.Kind == WorldBodyKind.Tile));
        }

        static void AssertCapacitySourceLetters(MatchSnapshot frame, ResolvedCourse course, int lap)
        {
            var tiles = frame.Economy.Tiles.ToDictionary(tile => tile.Key, tile => tile.Value);
            foreach (var source in course.Supplies)
            {
                var resource = frame.Resources.Single(value => value.SourceId == source.Id && value.Team == 0 && value.Lap == lap);
                var bodies = frame.Bodies.Where(body => body.Kind == WorldBodyKind.Tile && body.SourceId == source.Id).ToArray();
                Assert.IsTrue(bodies.All(body => body.OwnerTeam == 0 && body.RecoveryGeneration == resource.Generation));
                var letters = new LetterBag();
                foreach (var body in bodies) letters.Add(tiles[int.Parse(body.Id.Substring("tile:".Length))]);
                Assert.IsTrue(letters.Equals(LetterBag.FromWord(source.Letters)), "Each populated source must retain its exact authored letter multiset.");
            }
        }

        IEnumerator ReclaimCapacityBodies(Peer host)
        {
            var remote = peers.Single(peer => peer != host);
            Assert.AreEqual("TimeRun", live.Mode.Id); Assert.IsFalse(live.PublicQueue);
            Assert.AreEqual("Workshop", live.Verified.Match.Content.Kind);
            Assert.AreEqual(2, live.Verified.Roster.Count);
            Assert.IsTrue(live.Verified.Roster.All(seat => seat.TeamId == 0));
            AssertCapacityBodyCounts(host.Controller.Frame);
            var startingFrame = host.Controller.Frame;
            string startingBytes = MatchSnapshotCodec.Export(startingFrame);
            var roots = host.Controller.Frame.Bodies.Where(body => body.Kind != WorldBodyKind.Tile)
                .Select(body => body.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var critical = new HashSet<string>(live.Course.Supplies.Where(source => source.SourceId == "TableLetters" ||
                source.SourceId == "CapacityReserve").Select(source => source.Id), StringComparer.Ordinal);
            string optional = live.Course.Supplies.Single(source => source.SourceId == "CapacityOptional").Id;
            string document = CourseJson.ExportDocument(live.Course.CopyDocument());
            var histories = new CapacityHistory();
            var retired = new HashSet<string>(StringComparer.Ordinal);
            var allocated = new List<int>();
            yield return FullWalk(host, remote, () => Pose(host, "keepsake:0").Position, .8f, "Reach the capacity course keepsake");
            yield return Command(host, remote, WordboundAction.TakeKeepsake, "keepsake:0");
            try
            {
                for (int lap = 0; lap < 2; lap++)
                {
                    yield return FullWalk(host, remote, () => new WorldVector(-4, 0, -3), .25f, "Keep the carrier clear of capacity collection and sockets");
                    Assert.AreEqual(lap, FullTeam(host).Lap);
                    yield return CommittedState(host, frame => frame.Progress.Teams.Single().Lap == lap && frame.Bodies.Count == CapacityBodies,
                        "Acknowledge the populated capacity course on both peers");
                    yield return Until(() => peers.All(peer => Ready(peer) && ExactBodies(peer)), "Observe exact ready capacity bodies", 25);
                    AssertCapacityBodyCounts(host.Controller.Frame);
                    AssertCapacitySourceLetters(host.Controller.Frame, live.Course, lap);
                    Assert.IsFalse(host.Controller.Frame.Bodies.Any(body => retired.Contains(body.Id)));
                    long populatedTick = remote.Runner.Tick.Raw;
                    yield return Until(() => remote.Runner.Tick.Raw >= populatedTick + 120,
                        "Observe ordinary network progression without forcing history writes", 25);
                    foreach (var peer in peers) histories.Sample(peer, "populated-" + lap);
                    string tableSource = live.Course.Supplies.Single(source => source.SourceId == "TableLetters").Id;
                    string[] letters = host.Controller.Frame.Bodies.Where(body => body.Kind == WorldBodyKind.Tile && body.SourceId == tableSource)
                        .Select(body => body.Id).ToArray();
                    Assert.AreEqual(5, letters.Length);
                    foreach (string id in letters)
                    {
                        yield return FullWalk(host, host, () => Pose(host, id).Position, .85f, "Reach a capacity TABLE letter through admitted input");
                        yield return Command(host, host, WordboundAction.CollectLetter, id);
                    }
                    Assert.IsTrue(Inventory(host, host).Letters.Equals(LetterBag.FromWord("TABLE")));
                    int seat = WordboundGameplayPlan.Seat(host.Controller.Frame.Binding, host.Actor);
                    int firstItem = host.Controller.Frame.Economy.NextItemId;
                    yield return Command(host, host, WordboundAction.BeginCraft, value: "TABLE");
                    yield return Until(() => host.Controller.Frame.Economy.Items.Any(item => item.Id >= firstItem && item.Word == "TABLE" &&
                        item.State == ItemState.Held && item.HolderId == seat), "Complete the unchanged capacity TABLE craft", 20);
                    var table = host.Controller.Frame.Economy.Items.Single(item => item.Id >= firstItem && item.Word == "TABLE" &&
                        item.State == ItemState.Held && item.HolderId == seat);
                    string tableId = "item:" + table.Id;
                    yield return FullWalk(host, host, () => FullSocketApproach, .08f, "Reach the capacity TABLE socket");
                    host.InteractionInput = new WordboundMovementInput { HasFacing = true, FacingYaw = 0 };
                    yield return Until(() => FullFacesSocket(host, host), "Face the capacity socket through admitted input", 10);
                    yield return Command(host, host, WordboundAction.Deploy, slot: Array.IndexOf(Inventory(host, host).Slots, table.Id));
                    host.InteractionInput = default;
                    yield return Until(() => FullMechanism(host, lap).Physical.Stage == MatchPhysicalStage.Complete,
                        "Complete actual capacity TABLE socket contact", 15);
                    Assert.AreEqual(tableId, FullMechanism(host, lap).ItemEntityId);
                    Assert.GreaterOrEqual(FullMechanism(host, lap).ProgressTicks, 30);
                    Assert.AreEqual(live.Course.PhysicalSites.Single(site => site.Lap == lap + 1).Hash,
                        FullMechanism(host, lap).Physical.SiteHash);
                    yield return CommittedState(host, frame => frame.Bodies.Count(body => body.Kind == WorldBodyKind.Tile &&
                        critical.Contains(body.SourceId)) >= 123 && frame.Bodies.Any(body => body.Id == tableId),
                        "Acknowledge populated source bodies before retirement");
                    var high = histories.Sample(remote, "before-retire-" + lap);
                    MatchSnapshot previous = host.Controller.Frame, beforeSolve = null, solved = null;
                    void ObserveSolve(MatchSnapshot frame)
                    {
                        if (solved != null) return;
                        if (frame.Mechanisms.Single(value => value.PuzzleId == FullPuzzle && value.Team == 0 && value.Lap == lap).Solved)
                        { beforeSolve = previous; solved = frame; }
                        else previous = frame;
                    }
                    host.Controller.FrameChanged += ObserveSolve;
                    try { yield return Command(host, host, WordboundAction.UsePuzzle, FullPuzzle, value: tableId); }
                    finally { host.Controller.FrameChanged -= ObserveSolve; }
                    Assert.NotNull(beforeSolve); Assert.NotNull(solved);
                    Assert.AreEqual(beforeSolve.Progress.Tick + 1, solved.Progress.Tick);
                    string beforeBytes = MatchSnapshotCodec.Export(beforeSolve);
                    string[] loose = beforeSolve.Bodies.Where(body => body.Kind == WorldBodyKind.Tile && critical.Contains(body.SourceId)).Select(body => body.Id).ToArray();
                    string[] optionalIds = beforeSolve.Bodies.Where(body => body.Kind == WorldBodyKind.Tile && body.SourceId == optional).Select(body => body.Id).ToArray();
                    Assert.GreaterOrEqual(loose.Length, 123); Assert.LessOrEqual(loose.Length, 128); Assert.AreEqual(4, optionalIds.Length);
                    int generation = beforeSolve.Resources.Where(resource => critical.Contains(resource.SourceId) && resource.Lap == lap).Max(resource => resource.Generation);
                    Assert.AreEqual(beforeSolve.Economy.Minted, solved.Economy.Minted, "Solving this zero-reward objective must not mint letters.");
                    Assert.AreEqual(loose.Length + 5, solved.Economy.Spent - beforeSolve.Economy.Spent,
                        "Retirement must spend every observed loose source letter and the consumed TABLE exactly once.");
                    Assert.IsFalse(solved.Economy.Tiles.Any(tile => loose.Contains("tile:" + tile.Key)));
                    Assert.IsTrue(solved.Economy.Items.Single(item => item.Id == table.Id).IsGone);
                    Assert.IsTrue(FullMechanism(host, lap).Solved);
                    yield return CommittedState(host, frame => frame.Progress.Teams.Single().Lap == lap &&
                        !frame.Bodies.Any(body => critical.Contains(body.SourceId) || body.Id == tableId),
                        "Acknowledge actual source and consumed-equipment retirement");
                    yield return Until(() => peers.All(peer => Ready(peer) && ExactBodies(peer)), "Observe the exact low-water native body set", 25);
                    foreach (var peer in peers)
                    {
                        CollectionAssert.IsSubsetOf(optionalIds, peer.Controller.Frame.Bodies.Select(body => body.Id).ToArray());
                        Assert.IsFalse(loose.Any(peer.Controller.Bodies.ContainsKey));
                        Assert.IsFalse(peer.Controller.Bodies.ContainsKey(tableId));
                    }
                    double nextMemory = 0;
                    yield return Until(() => {
                        if (Time.realtimeSinceStartupAsDouble < nextMemory) return false;
                        nextMemory = Time.realtimeSinceStartupAsDouble + .25;
                        var current = histories.Usage(remote);
                        return current.Used < high.Used && (current.Free > high.Free || current.FreeBlocks > high.FreeBlocks);
                    }, "Observe retired object segments become reusable", 25);
                    var low = histories.Sample(remote, "retired-" + lap);
                    Assert.Less(low.Used, high.Used, "Retiring actual bodies must release live body-bucket segments.");
                    Assert.IsTrue(low.Free > high.Free || low.FreeBlocks > high.FreeBlocks,
                        "Retirement must expose reusable segments or return pages to the allocator.");
                    retired.UnionWith(loose); retired.UnionWith(optionalIds); retired.Add(tableId);
                    Assert.AreEqual(beforeBytes, MatchSnapshotCodec.Export(beforeSolve), "Later native retirement must not mutate the retained source snapshot.");
                    Debug.Log("WORDBOUND_CAPACITY_RETIRE: lap=" + lap + "; observedLoose=" + loose.Length + "; generation=" + generation +
                        "; refillObserved=" + (loose.Length > 123) + "; usedDrop=" + (high.Used - low.Used) + "; privateAuthored=true");
                    yield return FullWalk(host, host, () => FullCheckpoint, .25f, "Cross the capacity socket checkpoint");
                    yield return Until(() => FullTeam(host).NextCheckpoint == 1, "Observe the capacity first checkpoint", 10);
                    yield return FullWalk(host, host, () => FullFinish, .25f, "Cross the capacity delivery checkpoint without the carrier");
                    yield return Until(() => FullTeam(host).NextCheckpoint == 2, "Observe the capacity delivery checkpoint", 10);
                    yield return FullWalk(host, host, () => FullHostParking, .25f, "Clear the capacity finish for the carrier");
                    int completed = lap + 1;
                    yield return FullWalk(host, remote, () => FullFinish, .4f, "Deliver the capacity keepsake through native movement",
                        () => FullTeam(host).Lap == completed);
                    yield return CommittedState(host, frame => frame.Progress.Teams.Single().Lap == completed && frame.Bodies.Count == CapacityBodies,
                        "Acknowledge the newly minted next-lap capacity bodies");
                    yield return Until(() => peers.All(peer => Ready(peer) && ExactBodies(peer)), "Observe recreated native capacity bodies", 25);
                    AssertCapacityBodyCounts(host.Controller.Frame);
                    foreach (var peer in peers)
                    {
                        AssertCapacitySourceLetters(peer.Controller.Frame, live.Course, completed);
                        Assert.IsFalse(peer.Controller.Frame.Bodies.Any(body => retired.Contains(body.Id)));
                        CollectionAssert.AreEquivalent(roots, peer.Controller.Frame.Bodies.Where(body => body.Kind != WorldBodyKind.Tile).Select(body => body.Id).ToArray());
                    }
                    nextMemory = 0;
                    yield return Until(() => {
                        if (Time.realtimeSinceStartupAsDouble < nextMemory) return false;
                        nextMemory = Time.realtimeSinceStartupAsDouble + .25;
                        var current = histories.Usage(remote);
                        return current.Used > low.Used && (current.Free < low.Free || current.FreeBlocks < low.FreeBlocks);
                    }, "Observe new body identities consume reusable allocator segments", 25);
                    var recreated = histories.Sample(remote, "recreated-" + lap);
                    Assert.Greater(recreated.Used, low.Used, "New admitted body identities must allocate live segments again.");
                    Assert.IsTrue(recreated.Free < low.Free || recreated.FreeBlocks < low.FreeBlocks,
                        "Recreated bodies must consume available freed segments or pages.");
                    allocated.Add(recreated.Allocated);
                    Assert.IsNull(host.Controller.Frame.Progress.Result);
                }
                Assert.AreEqual(document, CourseJson.ExportDocument(live.Course.CopyDocument()));
                Assert.AreEqual(startingBytes, MatchSnapshotCodec.Export(startingFrame));
                Assert.AreEqual(2, FullTeam(host).Lap);
                Debug.Log("WORDBOUND_CAPACITY_OBSERVED: budget=512; initialBodies=366; authenticatedScriptedPeers=2; " +
                    "actualRetirementCycles=2; retiredEntityIdsReused=false; live512Claim=false; " +
                    "recreatedAllocatedSegments=" + string.Join(",", allocated) + "; plateauClaim=false; cleanupPending=true");
            }
            finally
            {
                foreach (var peer in peers) { peer.ScriptedMovement = null; peer.InteractionInput = default; }
            }
        }

        sealed class CapacityHistory
        {
            const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            static readonly FieldInfo Meta = typeof(Fusion.NetworkObject).GetField("Meta", Fields);
            static readonly FieldInfo Snapshots = Meta?.FieldType.GetField("_snapshots", Fields);
            static readonly FieldInfo Count = Snapshots?.FieldType.GetField("_count", Fields);
            const int BodyBucket = 25;

            internal static void Preflight()
            {
                Assert.NotNull(Meta); Assert.NotNull(Snapshots); Assert.NotNull(Count);
                Assert.AreEqual("Fusion.NetworkObjectMeta", Meta.FieldType.FullName);
                Assert.AreEqual("Fusion.NetworkObjectHeaderSnapshotList", Snapshots.FieldType.FullName);
                Assert.AreEqual(typeof(int), Count.FieldType);
                var sizing = typeof(Fusion.Allocator).GetNestedType("AllocatorBucketSize", BindingFlags.NonPublic);
                Assert.NotNull(sizing);
                var field = sizing.GetField("Sizes", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.NotNull(field);
                var sizes = field.GetValue(null) as int[];
                Assert.NotNull(sizes); Assert.AreEqual(57, sizes.Length);
                Assert.AreEqual(128, sizes[BodyBucket - 1]); Assert.AreEqual(160, sizes[BodyBucket]);
            }

            internal readonly struct Sampled
            {
                internal readonly int Used, Free, Allocated, FreeBlocks;
                internal Sampled(int used, int free, int allocated, int freeBlocks)
                { Used = used; Free = free; Allocated = allocated; FreeBlocks = freeBlocks; }
            }

            internal Sampled Usage(Peer peer)
            {
                Assert.AreEqual(Fusion.PageSizes._32Kb, peer.Runner.Config.Heap.PageShift);
                Assert.AreEqual(2048, peer.Runner.Config.Heap.PageCount);
                Assert.IsTrue(peer.Runner.TryGetFusionStatistics(out var statistics), "Allocator evidence must be available.");
                var snapshot = statistics.MemorySnapshot.ObjectAllocatorMemorySnapshot;
                Assert.AreEqual(2048, snapshot.TotalBlocks);
                Assert.NotNull(snapshot.BucketUsedSegmentsCount); Assert.NotNull(snapshot.BucketFreeSegmentsCount);
                Assert.NotNull(snapshot.BucketAllocatedSegmentsCount);
                Assert.AreEqual(57, snapshot.BucketUsedSegmentsCount.Length);
                Assert.AreEqual(57, snapshot.BucketFreeSegmentsCount.Length);
                Assert.AreEqual(57, snapshot.BucketAllocatedSegmentsCount.Length);
                var result = new Sampled(snapshot.BucketUsedSegmentsCount[BodyBucket], snapshot.BucketFreeSegmentsCount[BodyBucket],
                    snapshot.BucketAllocatedSegmentsCount[BodyBucket], snapshot.TotalFreeBlocks);
                Assert.Greater(result.Used, 0); Assert.GreaterOrEqual(result.Free, 0);
                return result;
            }

            internal Sampled Sample(Peer peer, string phase)
            {
                var result = Usage(peer);
                var counts = new List<int>();
                foreach (var body in peer.Controller.Bodies.Values)
                {
                    Assert.AreEqual(315, Fusion.NetworkObject.GetWordCount(body.Object));
                    object meta = Meta.GetValue(body.Object);
                    Assert.NotNull(meta);
                    int count = (int)Count.GetValue(Snapshots.GetValue(meta));
                    Assert.GreaterOrEqual(count, 0); Assert.LessOrEqual(count, ModeDefinition.TickRate);
                    counts.Add(count);
                }
                Assert.Greater(counts.Count, 0);
                if (!peer.Runner.IsServer) Assert.Greater(counts.Max(), 0, "At least one actual received history must be observed.");
                Debug.Log(FormattableString.Invariant($"WORDBOUND_CAPACITY_MEMORY: phase={phase}; host={peer.Runner.IsServer}; monotonic={Time.realtimeSinceStartupAsDouble:F6}; runnerTick={peer.Runner.Tick.Raw}; canonicalTick={peer.Controller.Frame.Progress.Tick}; bodies={counts.Count}; networkObjects={peer.Runner.GetAllNetworkObjects().Count}; historyMin={counts.Min()}; historyMax={counts.Max()}; historyTotal={counts.Sum()}; historyAt60={counts.Count(value => value == 60)}; bodyBucket={BodyBucket}; bodyBucketUsed={result.Used}; bodyBucketFree={result.Free}; bodyBucketAllocated={result.Allocated}; freeBlocks={result.FreeBlocks}; blocks=2048; passiveMetadata=true; forcedSnapshots=false"));
                return result;
            }
        }
    }
}
