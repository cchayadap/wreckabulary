using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Art;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;
using Object = UnityEngine.Object;

namespace Wreckabulary.OfflineGameplayTests
{
    public sealed class WordboundNativeParcelServiceTests
    {
        const string Actor = WordboundLocalMatchClient.LocalActor;
        const string CoursePath = "Assets/_Project/Resources/Wordbound/LocalCourses/ParcelServiceV2.asset";
        GameObject root;
        WordboundLocalMatchClient client;
        WordboundGameplayController controller;
        WordboundCourseWorld nativeWorld;
        WordboundCourseAsset source, owned;
        WordboundGameplayOptions options;
        Func<WordboundMovementInput> previousMovement, movement;
        IWordboundPuzzleContent content;
        CourseDocument layout;
        byte[] sourceBytes, metaBytes;
        string sourceMemory, observationFailure = "";
        Action<MatchSnapshot> observer;
        double deadline;
        bool enteredPlay;
        int movementReads;
        readonly HashSet<string> crafted = new HashSet<string>();
        readonly HashSet<string> collected = new HashSet<string>();
        readonly HashSet<int> laps = new HashSet<int>();
        MatchSnapshot Frame => controller.Frame;
        TeamProgress Team => Frame.Progress.Teams.Single();
        PlayerInventory Inventory => Frame.Economy.Players[0];
        WorldBodyState Player => Frame.Bodies.Single(b => b.Kind == WorldBodyKind.Player && b.SourceId == Actor);
        WorldBodyState Body(string id) => Frame.Bodies.Single(b => b.Id == id);
        ItemInstance Item(string id) => Frame.Economy.Items.Single(i => "item:" + i.Id == id);
        MatchMechanismState Mechanism(ResolvedCoursePhysicalSite site) => Frame.Mechanisms.Single(m =>
            m.Team == 0 && m.Lap == site.Lap - 1 && m.PuzzleId == site.PuzzleId);

        [UnityTest, Category("LocalParcelService"), Timeout(1800000)]
        public IEnumerator TimeRunCompletesAllThreeLapsThroughRealCraftsSocketsAndCarrierDeliveries()
        {
            RequireNativeEntry();
            yield return new EnterPlayMode();
            Assert.IsTrue(Application.isPlaying, "Root UnityTest must enter Play Mode before creating a local client.");
            yield return Start("TimeRun");
            yield return RouteTo(Body("keepsake:0").Position, .7f, "Reach the actual keepsake");
            yield return Command(WordboundAction.TakeKeepsake, "keepsake:0");
            Assert.AreEqual(Actor, Team.KeepsakeCarrier);
            for (int lap = 0; lap < 3; lap++)
            {
                Assert.AreEqual(lap, Team.Lap);
                var route = content.Course.Laps[lap];
                for (int order = 0; order < route.Checkpoints.Count; order++)
                {
                    var checkpoint = route.Checkpoints[order];
                    foreach (var puzzle in checkpoint.PuzzleIds)
                    {
                        Assert.AreEqual(order, Team.NextCheckpoint);
                        var site = content.Course.PhysicalSites.Single(s => s.Lap == lap + 1 && s.PuzzleId == puzzle);
                        yield return Solve(site);
                    }
                    var at = Metres(content.Course.RecoveryAnchors.Single(a => a.Id == checkpoint.RecoveryAnchorId).Position);
                    int expectedOrder = order + 1, expectedLap = lap;
                    yield return RouteTo(at, .3f, "Carry through " + checkpoint.Id,
                        () => Team.Lap > expectedLap || Team.NextCheckpoint >= expectedOrder);
                    yield return Until(() => Team.Lap > expectedLap || Team.NextCheckpoint >= expectedOrder,
                        "Observe actual supported checkpoint " + checkpoint.Id, 10);
                }
                int nextLap = lap + 1;
                yield return Until(() => Team.Lap == nextLap, "Deliver this lap's keepsake at the real finish", 10);
                Debug.Log("WORDBOUND_PARCEL_LAP: lap=" + nextLap + "; tick=" + Frame.Progress.Tick +
                    "; manifest=" + content.Course.Hash + "; solved=" + Frame.Mechanisms.Count(m => m.Solved));
            }
            yield return Until(() => Frame.Progress.Result?.Confirmed == true, "Confirm native terminal result", 20);
            Assert.AreEqual("Win", Frame.Progress.Result.Outcome);
            CollectionAssert.AreEqual(new[] { 0 }, Frame.Progress.Result.WinningTeams);
            Assert.AreEqual(3, Team.Lap); Assert.IsTrue(Team.Finished);
            Assert.AreEqual(content.Course.Laps.Sum(l => l.Checkpoints.Count + 1), Team.OrderedObjectives);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, laps);
            var solved = content.Course.Laps.SelectMany(l => l.Checkpoints.SelectMany(c => c.PuzzleIds)).ToArray();
            int letters = solved.Sum(id => content.Course.Puzzles.Single(p => p.Id == id).RecipeId.Length);
            Assert.AreEqual(solved.Length, Frame.Mechanisms.Count(m => m.Solved));
            Assert.AreEqual(solved.Length, crafted.Count); Assert.AreEqual(letters, collected.Count);
            RequireSourcePreserved();
            RequireGameplayEvidence();
            Debug.Log("WORDBOUND_PARCEL_RACE_COMPLETE: 3 native laps; " + solved.Length + " unchanged crafts/deployments/socket holds/consumptions; " +
                letters + " real letters; supported door and stair routes; keepsake deliveries; confirmed Win. Scripted admitted input, not physical-device/UI or online acceptance.");
        }

        [UnityTest, Category("LocalParcelService"), Timeout(1800000)]
        public IEnumerator FreestyleCompletesEveryAuthoredSiteAndRejectsMapOriginTableAtSocket()
        {
            RequireNativeEntry();
            yield return new EnterPlayMode();
            Assert.IsTrue(Application.isPlaying, "Root UnityTest must enter Play Mode before creating a local client.");
            yield return Start("Freestyle");
            bool checkedMapTable = false;
            string[] order = { "SortingBench", "DockBed", "ReturnClock", "LockerStep", "OfficeSofa", "DispatchStand" };
            CollectionAssert.AreEquivalent(content.Course.PhysicalSites.Select(s => s.PuzzleId).Distinct(), order);
            foreach (string puzzle in order)
            {
                var stations = content.Course.PhysicalSites.Where(s => s.PuzzleId == puzzle).ToArray();
                foreach (var ignored in stations)
                {
                    var at = Metres(stations[0].Position);
                    yield return RouteTo(new WorldVector(at.X, at.Y, at.Z - 1.5f), .08f, "Approach practice station");
                    var site = WordboundPracticeGuidePlan.Nearest(content.Course, Frame, Actor);
                    Assert.IsNotNull(site);
                    Assert.AreEqual(puzzle, site.PuzzleId);
                    Assert.IsFalse(WordboundPracticeGuidePlan.Completed(Frame, site),
                        "The normal HUD selector must offer each unfinished co-located lap station.");
                    yield return Command(WordboundAction.PracticePuzzle, site.Hash);
                    Assert.AreEqual(site.Hash, Frame.Practice.SiteHash);
                    if (!checkedMapTable) { yield return RejectMapTable(site); checkedMapTable = true; }
                    yield return Solve(site);
                    Assert.IsNull(Frame.Progress.Result);
                    Assert.AreEqual(0, Team.Lap); Assert.AreEqual(0, Team.OrderedObjectives);
                }
            }
            int sites = content.Course.PhysicalSites.Count;
            int letters = content.Course.PhysicalSites.Sum(s => content.Course.Puzzles.Single(p => p.Id == s.PuzzleId).RecipeId.Length);
            Assert.AreEqual(sites, Frame.Mechanisms.Count(m => m.Solved));
            Assert.AreEqual(sites, crafted.Count); Assert.AreEqual(letters, collected.Count);
            Assert.IsFalse(Team.Finished); Assert.IsNull(Frame.Progress.Result);
            RequireSourcePreserved();
            RequireGameplayEvidence();
            Debug.Log("WORDBOUND_PARCEL_PRACTICE_COMPLETE: " + sites + " actual physical sites; map-origin TABLE rejected; " +
                letters + " real recovery letters and " + sites + " crafted/deployed/consumed objects; no race result. Uses production nearest unfinished station selection and public commands; physical-device/menu click acceptance remains separate.");
        }

        void RequireNativeEntry()
        {
            if (Environment.GetEnvironmentVariable("WORDBOUND_LOCAL_PARCEL") != "1")
                Assert.Ignore("Set WORDBOUND_LOCAL_PARCEL=1 for actual Parcel Depot local completion and physical socket tests.");
            Assert.IsFalse(Application.isPlaying);
            enteredPlay = true;
        }

        IEnumerator Start(string mode)
        {
            Assert.IsTrue(Application.isPlaying, "Native startup cannot execute from Edit Mode.");
            crafted.Clear(); collected.Clear(); laps.Clear(); observationFailure = ""; movementReads = 0;
            deadline = Time.realtimeSinceStartupAsDouble + 1680;
            source = AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(CoursePath);
            Assert.IsNotNull(source, "Run Tools/Wordbound/Courses/Create or select Parcel Depot first.");
            Assert.IsFalse(EditorUtility.IsDirty(source), "Save or revert owner edits before native verification.");
            sourceBytes = File.ReadAllBytes(CoursePath); metaBytes = File.ReadAllBytes(CoursePath + ".meta");
            sourceMemory = EditorJsonUtility.ToJson(source);
            owned = Object.Instantiate(source);
            owned.ModeId = mode; owned.Format = RaceFormat.Solo;
            content = owned.CreateContent(owned.Resolve());
            layout = content.Course.CopyDocument();
            Assert.AreEqual(WordboundParcelServiceContent.CourseId, layout.Id);
            Assert.AreEqual(WordboundParcelServiceContent.PhysicalSiteCount, content.Course.PhysicalSites.Count);
            var launch = Resources.Load<WordboundLaunchAssets>(WordboundLaunchAssets.ResourcePath);
            Assert.IsNotNull(launch);
            root = new GameObject("Owned Parcel Depot native completion");
            client = root.AddComponent<WordboundLocalMatchClient>();
            yield return Completed(client.StartLocal(content, launch.BodyPrefabs(), launch.GameplayScene(),
                cameraProfile: owned.CameraProfile, visualProfile: owned.VisualProfile,
                uiTheme: launch.UiTheme, storyProfile: launch.StoryProfile), "Start production local client", 60);
            controller = client.Gameplay;
            yield return Until(() => controller && controller.Frame != null && controller.CourseWorld &&
                controller.IsPresentationReady && !controller.IsSimulationPaused, "Native world ready", 40);
            Assert.AreEqual(content.Course.Hash, controller.CourseWorld.ManifestHash);
            nativeWorld = controller.CourseWorld;
            Assert.AreEqual(content.Course.Hash, Frame.Binding.ManifestHash);
            Assert.AreEqual(content.CatalogueHash, Frame.Binding.CatalogueHash);
            Assert.AreEqual(WordboundParcelServiceContent.FurnitureCount, Frame.Bodies.Count(b => b.Kind == WorldBodyKind.Item && b.Active));
            Assert.AreNotEqual(Physics.defaultPhysicsScene, controller.PhysicsWorld.PhysicsScene);
            Assert.IsTrue(controller.CourseWorld.Surfaces.All(c => c && c.enabled && !c.isTrigger));
            Assert.IsTrue(controller.CourseWorld.Surfaces.Any(c => c.GetComponent<WordboundCourseNode>()?.Kind == WordboundCourseNodeKind.Wall));
            Assert.IsTrue(controller.CourseWorld.Surfaces.Any(c => c.GetComponent<WordboundCourseNode>()?.Kind == WordboundCourseNodeKind.Floor));
            var field = typeof(WordboundGameplayController).GetField("options", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field);
            options = (WordboundGameplayOptions)field.GetValue(controller);
            previousMovement = options.ReadMovement;
            Assert.IsNotNull(previousMovement);
            movement = () => default; options.ReadMovement = ReadMovement;
            long previousTick = Frame.Progress.Tick;
            int priorLap = Team.Lap, priorObjectives = Team.OrderedObjectives;
            observer = frame => {
                var team = frame.Progress.Teams.Single();
                if (frame.Progress.Tick < previousTick || team.Lap < priorLap || team.Lap > priorLap + 1 ||
                    team.OrderedObjectives < priorObjectives || team.OrderedObjectives > priorObjectives + 2)
                    observationFailure = "Canonical race progression skipped or regressed.";
                if (content.Mode.IsRace && team.OrderedObjectives !=
                    content.Course.Laps.Take(team.Lap).Sum(l => l.Checkpoints.Count + 1) + team.NextCheckpoint)
                    observationFailure = "Ordered progression differs from the actual lap checkpoints plus finish.";
                if (team.Lap > priorLap)
                {
                    var checkpoint = content.Course.Laps[priorLap].Checkpoints.Last();
                    var finish = Metres(content.Course.RecoveryAnchors.Single(a => a.Id == checkpoint.RecoveryAnchorId).Position);
                    var player = frame.Bodies.Single(b => b.Kind == WorldBodyKind.Player && b.SourceId == Actor);
                    if (team.KeepsakeCarrier != Actor || player.Position.DistanceSquared(finish) > .8f * .8f)
                        observationFailure = "Finish lacked actual keepsake carrier at authored finish.";
                    laps.Add(team.Lap);
                }
                previousTick = frame.Progress.Tick; priorLap = team.Lap; priorObjectives = team.OrderedObjectives;
            };
            controller.FrameChanged += observer;
            Assert.IsNull(Frame.Progress.Result);
        }

        IEnumerator Solve(ResolvedCoursePhysicalSite site)
        {
            var puzzle = content.Course.Puzzles.Single(p => p.Id == site.PuzzleId);
            int expectedTeam = content.Course.Play.TeamCount > 0 ? 0 : -1;
            var sourceSupply = content.Course.Supplies.Single(s => s.SourceId == site.RecoverySupplyId && s.TeamIndex == expectedTeam);
            Assert.IsTrue(sourceSupply.Protected && sourceSupply.Renewable);
            Assert.IsTrue(content.Items.Get(puzzle.RecipeId).CanDeploy, "Every actual socket recipe must deploy.");
            Assert.IsTrue(Inventory.Letters.IsEmpty);
            yield return Until(() => Frame.Bodies.Count(b => b.Kind == WorldBodyKind.Tile && b.SourceId == sourceSupply.Id) == puzzle.RecipeId.Length,
                "Expose selected recovery letters for " + site.PuzzleId, 15);
            yield return RouteTo(Metres(sourceSupply.Position), .8f, "Reach real recovery supply");
            var batch = Frame.Bodies.Where(b => b.Kind == WorldBodyKind.Tile && b.SourceId == sourceSupply.Id).ToArray();
            foreach (var tile in batch)
            {
                Assert.IsTrue(tile.Protected); Assert.AreEqual(expectedTeam, tile.OwnerTeam); Assert.IsTrue(collected.Add(tile.Id));
                yield return Walk(() => Body(tile.Id).Position, .85f, "Collect actual letter");
                yield return Command(WordboundAction.CollectLetter, tile.Id);
                Assert.IsFalse(Frame.Bodies.Any(b => b.Id == tile.Id));
            }
            Assert.IsTrue(Inventory.Letters.Equals(LetterBag.FromWord(puzzle.RecipeId)));
            int firstId = Frame.Economy.NextItemId;
            yield return Command(WordboundAction.BeginCraft, value: puzzle.RecipeId);
            yield return Until(() => Frame.Economy.Items.Any(i => i.Id >= firstId && i.Word == puzzle.RecipeId &&
                i.Origin == ItemOrigin.Crafted && i.HolderId == 0 && i.State == ItemState.Held), "Finish original craft timer", 20);
            var item = Frame.Economy.Items.Single(i => i.Id >= firstId && i.Word == puzzle.RecipeId &&
                i.Origin == ItemOrigin.Crafted && i.HolderId == 0 && i.State == ItemState.Held);
            string id = "item:" + item.Id;
            Assert.IsTrue(crafted.Add(id)); Assert.IsTrue(Inventory.Letters.IsEmpty && Inventory.Reserved.IsEmpty);
            var at = Metres(site.Position);
            yield return RouteTo(new WorldVector(at.X, at.Y, at.Z - 1.5f), .06f, "Reach real socket deployment approach");
            yield return FaceNorth();
            int slot = Array.IndexOf(Inventory.Slots, item.Id);
            Assert.GreaterOrEqual(slot, 0);
            yield return Command(WordboundAction.Deploy, slot: slot);
            movement = () => default;
            Assert.AreEqual(ItemState.Deployed, Item(id).State);
            var native = controller.Bodies[id];
            Assert.IsTrue(native.Rigidbody.isKinematic);
            var collider = native.GetComponent<BoxCollider>();
            Assert.IsNotNull(collider); Assert.IsTrue(collider.enabled && !collider.isTrigger);
            Assert.Less(HorizontalDistance(Body(id).Position, at), site.SocketRadiusMm / 1000f);
            yield return Until(() => Mechanism(site).Physical.Stage == MatchPhysicalStage.Complete,
                "Hold real native " + puzzle.RecipeId + " at " + site.PuzzleId, 15);
            Assert.AreEqual(id, Mechanism(site).ItemEntityId);
            Assert.AreEqual(site.Hash, Mechanism(site).Physical.SiteHash);
            Assert.GreaterOrEqual(Mechanism(site).ProgressTicks, site.HoldTicks);
            RequireOriginalMesh(id, puzzle.RecipeId);
            yield return Command(WordboundAction.UsePuzzle, site.PuzzleId, value: id);
            Assert.IsTrue(Mechanism(site).Solved); Assert.IsTrue(Item(id).IsGone);
            Assert.IsFalse(Frame.Bodies.Any(b => b.Id == id));
            Assert.IsFalse(controller.Bodies.ContainsKey(id));
            Debug.Log("WORDBOUND_PARCEL_SOCKET: mode=" + content.Mode.Id + "; lap=" + site.Lap + "; puzzle=" +
                site.PuzzleId + "; recipe=" + puzzle.RecipeId + "; item=" + id + "; site=" + site.Hash + "; physical=true; consumed=true");
        }

        IEnumerator RejectMapTable(ResolvedCoursePhysicalSite site)
        {
            var placement = content.Course.Placements.Single(p => p.ZoneId == "SortingBenchProp");
            var table = Frame.Bodies.Single(b => b.Kind == WorldBodyKind.Item && b.SourceId == placement.Id);
            Assert.AreEqual(ItemOrigin.Map, Item(table.Id).Origin);
            var original = Metres(placement.Position);
            yield return RouteTo(new WorldVector(original.X, original.Y, original.Z - 1.15f), .1f, "Reach actual map table");
            yield return Command(WordboundAction.PickUp, table.Id, Item(table.Id).Revision);
            Assert.AreEqual(ItemState.Carried, Item(table.Id).State);
            var size = controller.Bodies[table.Id].GetComponent<BoxCollider>().size;
            float releaseOffset = controller.Bodies[Player.Id].GetComponent<CapsuleCollider>().radius + size.z / 2 + .15f;
            var at = Metres(site.Position);
            yield return RouteTo(new WorldVector(at.X, at.Y, at.Z - releaseOffset), .04f, "Carry map table to the physical socket");
            yield return FaceNorth();
            LogMapRelease(table.Id);
            yield return Command(WordboundAction.Drop);
            movement = () => default;
            yield return Until(() => Body(table.Id).Velocity.DistanceSquared(default) < .01f, "Settle map table on actual floor", 10);
            Assert.Less(HorizontalDistance(Body(table.Id).Position, at), site.SocketRadiusMm / 1000f);
            long observedUntil = Frame.Progress.Tick + site.HoldTicks + 5;
            yield return Until(() => Frame.Progress.Tick >= observedUntil, "Observe rejection across full socket hold", 10);
            Assert.AreEqual(MatchPhysicalStage.Waiting, Mechanism(site).Physical.Stage);
            Assert.IsFalse(Mechanism(site).Solved);
            yield return Command(WordboundAction.UsePuzzle, site.PuzzleId, value: table.Id, expected: "WrongPuzzleItem");
            Assert.IsFalse(Item(table.Id).IsGone);
            yield return Command(WordboundAction.PickUp, table.Id, Item(table.Id).Revision);
            yield return RouteTo(new WorldVector(original.X, original.Y, original.Z - releaseOffset), .04f, "Return map furniture to sorting bench");
            yield return FaceNorth(); yield return Command(WordboundAction.Drop); movement = () => default;
            Assert.AreEqual(ItemOrigin.Map, Item(table.Id).Origin);
        }

        void LogMapRelease(string itemId)
        {
            var actor = controller.Bodies[Player.Id]; var item = controller.Bodies[itemId];
            var capsule = actor.GetComponent<CapsuleCollider>(); var box = item.GetComponent<BoxCollider>();
            var world = controller.PhysicsWorld; var scene = world.PhysicsScene;
            var rotation = actor.Rigidbody.rotation;
            var half = box.size / 2 + Vector3.one * .015f;
            var start = actor.Rigidbody.position + Vector3.up * .08f + rotation * box.center;
            var candidates = WordboundReleasePolicy.Candidates(Player, new WorldVector(box.size.x, box.size.y, box.size.z), capsule.radius, false);
            string V(Vector3 v) => v.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
            string Describe(Collider c)
            {
                var owner = controller.Bodies.Values.FirstOrDefault(b => b.OwnedColliders.Contains(c));
                var node = c.GetComponent<WordboundCourseNode>();
                bool blocks = owner != null ? WordboundPhysicsPolicy.ShouldCollide(content.Mode,
                    Body(itemId).At(Body(itemId).Position, active: true, kinematic: false), owner.Canonical) :
                    controller.CourseWorld.Surfaces.Contains(c);
                return c.name + "/" + (owner == null ? "static:" + node?.Kind : owner.Canonical.Id) +
                    "/blocks=" + blocks + "/active=" + (owner?.Canonical.Active.ToString() ?? "n/a") +
                    "/center=" + V(c.bounds.center) + "/size=" + V(c.bounds.size);
            }
            string Overlaps(Vector3 center)
            {
                var found = new Collider[128];
                int count = scene.OverlapBox(center, half, found, rotation, ~0, QueryTriggerInteraction.Ignore);
                return count + ":" + string.Join(" | ", found.Take(count).Select(Describe));
            }
            bool canMove = world.CanMove(Actor, out float moveScale);
            bool possible = world.TryRelease(Player, Body(itemId), false, out var released);
            Debug.Log("WORDBOUND_PARCEL_RELEASE: query=" + possible + "; candidate=" + released +
                "; canMove=" + canMove + "; scale=" + moveScale + "; actorNative=" + V(actor.Rigidbody.position) +
                "; actorCanonical=" + Player.Position + "; actorDelta=" +
                (actor.Rigidbody.position - new Vector3(Player.Position.X, Player.Position.Y, Player.Position.Z)).sqrMagnitude +
                "; rotation=" + rotation.ToString("F6") + "; actorScale=" + V(actor.transform.lossyScale) +
                "; itemScale=" + V(item.transform.lossyScale) + "; itemState=" + Item(itemId).State +
                "; holder=" + Item(itemId).HolderId + "; active=" + Body(itemId).Active + "; kinematic=" + Body(itemId).Kinematic +
                "; nativeKinematic=" + item.Rigidbody.isKinematic + "; box=" + V(box.size) + "; boxCenter=" + V(box.center) +
                "; boxTrigger=" + box.isTrigger + "; boxAttached=" + (box.attachedRigidbody == item.Rigidbody) +
                "; capsuleRadius=" + capsule.radius + "; capsuleHeight=" + capsule.height + "; capsuleCenter=" + V(capsule.center) +
                "; capsuleDirection=" + capsule.direction + "; capsuleTrigger=" + capsule.isTrigger +
                "; capsuleAttached=" + (capsule.attachedRigidbody == actor.Rigidbody) + "; start=" + V(start) + "; startHits=" + Overlaps(start));
            for (int i = 0; i < candidates.Count; i++)
            {
                var at = candidates[i]; var end = new Vector3(at.X, at.Y, at.Z) + rotation * box.center;
                var delta = end - start; var hits = new RaycastHit[128];
                int count = scene.BoxCast(start, half, delta.normalized, hits, rotation, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
                Debug.Log("WORDBOUND_PARCEL_RELEASE_PATH: index=" + i + "; foot=" + at + "; endHits=" + Overlaps(end) +
                    "; sweepHits=" + count + ":" + string.Join(" | ", hits.Take(count).Select(h => Describe(h.collider) + "/distance=" + h.distance)));
            }
        }

        IEnumerator RouteTo(WorldVector target, float radius, string label, Func<bool> reached = null)
        {
            if (reached?.Invoke() == true) yield break;
            foreach (var (point, stop) in Waypoints(Player.Position, target))
                yield return Walk(() => point, stop ? .12f : .3f, "Follow actual door and stair route to " + label, reached, stop);
            yield return Walk(() => target, radius, label, reached);
        }

        List<(WorldVector point, bool stop)> Waypoints(WorldVector from, WorldVector to)
        {
            var route = new List<(CoursePoint point, bool stop)>();
            var module = ModuleAt(from); var goal = ModuleAt(to);
            var junction = Junction(module, Millimetres(from, module));
            foreach (var (connection, next) in Hops(module, goal))
            {
                var exit = Doorway(connection, module); var enter = Doorway(connection, next);
                Cross(module, junction, exit.junction, route);
                route.Add((exit.entry, true)); route.Add((enter.entry, true));
                module = next; junction = enter.junction;
            }
            Cross(module, junction, Junction(goal, Millimetres(to, goal)), route);
            return route.Where((step, i) => i == 0 || !step.point.Equals(route[i - 1].point))
                .Select(step => (Metres(step.point), step.stop)).ToList();
        }

        void Cross(CourseModule module, CoursePoint from, CoursePoint to, List<(CoursePoint point, bool stop)> route)
        {
            route.Add((from, false));
            if (!Aisles(module).Any(box => box.Contains(from) && box.Contains(to))) route.Add((module.Position, false));
            route.Add((to, false));
        }

        List<(CourseConnection connection, CourseModule next)> Hops(CourseModule from, CourseModule to)
        {
            var previous = new Dictionary<string, CourseConnection>(StringComparer.Ordinal) { [from.Id] = null };
            var queue = new Queue<string>(new[] { from.Id });
            while (queue.Count > 0 && !previous.ContainsKey(to.Id))
            {
                string at = queue.Dequeue();
                foreach (var connection in layout.Connections)
                {
                    string next = connection.FromModuleId == at ? connection.ToModuleId : connection.ToModuleId == at ? connection.FromModuleId : null;
                    if (next == null || previous.ContainsKey(next)) continue;
                    previous.Add(next, connection); queue.Enqueue(next);
                }
            }
            Assert.IsTrue(previous.ContainsKey(to.Id), "No authored door or stair route reaches " + to.Id);
            var hops = new List<(CourseConnection connection, CourseModule next)>();
            for (string at = to.Id; at != from.Id;)
            {
                var connection = previous[at];
                hops.Insert(0, (connection, layout.Modules.Single(m => m.Id == at)));
                at = connection.FromModuleId == at ? connection.ToModuleId : connection.FromModuleId;
            }
            return hops;
        }

        (CoursePoint entry, CoursePoint junction) Doorway(CourseConnection connection, CourseModule module)
        {
            var room = content.Geometry.Bounds(module);
            bool stairs = connection.Kind == "Stairs", from = module.Id == connection.FromModuleId;
            var at = from ? connection.From : connection.To; var other = from ? connection.To : connection.From;
            bool alongX = stairs ? other.Z != at.Z : at.X == room.Min.X || at.X == room.Max.X;
            int sign = stairs ? -Math.Sign(alongX ? other.Z - at.Z : other.X - at.X) :
                alongX ? (at.X == room.Min.X ? 1 : -1) : (at.Z == room.Min.Z ? 1 : -1);
            var entry = alongX == stairs ? new CoursePoint(at.X, at.Y, at.Z + sign * 1500) : new CoursePoint(at.X + sign * 1500, at.Y, at.Z);
            return (entry, alongX ? new CoursePoint(module.Position.X, at.Y, entry.Z) : new CoursePoint(entry.X, at.Y, module.Position.Z));
        }

        CourseModule ModuleAt(WorldVector at)
        {
            var module = layout.Modules.Where(m => {
                var room = content.Geometry.Bounds(m);
                return at.X * 1000 >= room.Min.X && at.X * 1000 <= room.Max.X && at.Z * 1000 >= room.Min.Z &&
                    at.Z * 1000 <= room.Max.Z && m.Position.Y <= at.Y * 1000 + 1000;
            }).OrderByDescending(m => m.Position.Y).ThenBy(m => m.Id, StringComparer.Ordinal).FirstOrDefault();
            Assert.IsNotNull(module, "No authored module contains " + at);
            return module;
        }

        CourseBox[] Aisles(CourseModule module) => layout.ReservedVolumes
            .Where(v => v.Id == "Route_" + module.Id || v.Id == "Cross_" + module.Id).Select(v => v.Bounds).ToArray();
        CoursePoint Junction(CourseModule module, CoursePoint point) => Aisles(module).Any(box => box.Contains(point)) ? point :
            new CoursePoint(point.X, module.Position.Y, module.Position.Z);
        static CoursePoint Millimetres(WorldVector at, CourseModule module) =>
            new CoursePoint((int)Math.Round(at.X * 1000), module.Position.Y, (int)Math.Round(at.Z * 1000));

        IEnumerator Walk(Func<WorldVector> target, float radius, string label, Func<bool> reached = null, bool settle = true)
        {
            movement = () => {
                if (reached?.Invoke() == true) return default;
                var at = controller.Bodies[Player.Id].Rigidbody.position; var goal = target();
                var delta = new Vector2(goal.X - at.x, goal.Z - at.z);
                if (delta.magnitude <= radius) return default;
                var input = delta.normalized * (settle ? Mathf.Clamp((delta.magnitude - radius * .5f) * .1f, .06f, .32f) : .32f);
                return new WordboundMovementInput { MoveX = input.x, MoveZ = input.y };
            };
            try { yield return Until(() => reached?.Invoke() == true || HorizontalDistance(Player.Position, target()) <= radius, label, 45); }
            finally { movement = () => default; }
            Assert.AreEqual(0, Frame.Progress.Players.Single(p => p.Id == Actor).Knockouts);
            if (!settle) yield break;
            if (Frame.Progress.Result == null) yield return Until(() => SpeedSquared() < .01f, "Settle through unchanged motor", 10);
            var hits = new RaycastHit[16];
            var foot = controller.Bodies[Player.Id].Rigidbody.position;
            int count = controller.PhysicsWorld.PhysicsScene.Raycast(foot + Vector3.up * .08f, Vector3.down,
                hits, .22f, ~0, QueryTriggerInteraction.Ignore);
            Assert.Less(count, hits.Length);
            Assert.IsTrue(hits.Take(count).Any(h => controller.CourseWorld.Surfaces.Contains(h.collider) && h.normal.y > .8f),
                label + " must end supported on an actual canonical world surface.");
        }
        IEnumerator FaceNorth()
        {
            movement = () => new WordboundMovementInput { HasFacing = true, FacingYaw = 0 };
            yield return Until(() => Vector3.Dot(controller.Bodies[Player.Id].Rigidbody.rotation * Vector3.forward,
                Vector3.forward) > .999f && SpeedSquared() < .01f, "Face socket using admitted input", 10);
        }
        WordboundMovementInput ReadMovement() { movementReads++; return movement?.Invoke() ?? default; }
        float SpeedSquared() => Player.Velocity.X * Player.Velocity.X + Player.Velocity.Z * Player.Velocity.Z;
        static float HorizontalDistance(WorldVector a, WorldVector b) => new Vector2(a.X - b.X, a.Z - b.Z).magnitude;
        static WorldVector Metres(CoursePoint at) => new WorldVector(at.X / 1000f, at.Y / 1000f, at.Z / 1000f);
        IEnumerator Command(WordboundAction action, string target = "", int revision = 0, int slot = -1, string value = "", string expected = "Ok")
        {
            var command = controller.Submit(action, target, revision, slot, value);
            yield return Until(() => Frame.Commands.Frontiers.TryGetValue(Actor, out long frontier) && frontier >= command.Sequence,
                "Commit real " + action, 10);
            var receipt = Frame.Commands.Receipts.Single(r => r.ActorId == Actor && r.Sequence == command.Sequence);
            Assert.AreEqual(command.PayloadHash, receipt.PayloadHash);
            Assert.AreEqual(expected, receipt.Effect.Code, action + " must use unchanged authority rules.");
        }
        IEnumerator Until(Func<bool> ready, string label, double seconds)
        {
            double limit = Time.realtimeSinceStartupAsDouble + seconds;
            Debug.Log("WORDBOUND_PARCEL_STAGE: " + label);
            while (!ready() && Time.realtimeSinceStartupAsDouble < limit) { Healthy(); yield return null; }
            Healthy();
            Assert.IsTrue(ready(), label + "; tick=" + (controller?.Frame?.Progress.Tick ?? -1) + "; position=" +
                (controller?.Frame == null ? "unavailable" : Player.Position.ToString()));
        }
        void Healthy()
        {
            Assert.Less(Time.realtimeSinceStartupAsDouble, deadline, "Fixture exceeded its bounded runtime.");
            if (controller) Assert.AreEqual("", controller.Fault);
            Assert.IsEmpty(observationFailure);
        }
        static IEnumerator Completed(Task task, string label, double seconds)
        {
            double limit = Time.realtimeSinceStartupAsDouble + seconds;
            while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < limit) yield return null;
            Assert.IsTrue(task.IsCompleted, label + " exceeded deadline.");
            Assert.IsFalse(task.IsCanceled, label + " cancelled.");
            Assert.IsFalse(task.IsFaulted, label + ": " + task.Exception?.GetBaseException());
        }
        void RequireOriginalMesh(string id, string recipe)
        {
            var model = ModelLibrary.Load()?.Find("Items/" + recipe); Assert.IsNotNull(model);
            var visual = controller.Bodies[id].GetComponent<WordboundGameplayBody>().VisualRoot;
            var art = visual ? visual.Find("Wordbound artwork " + recipe) : null;
            Assert.IsNotNull(art); Assert.IsTrue(art.gameObject.activeInHierarchy);
            CollectionAssert.AreEqual(Meshes(model), Meshes(art.gameObject));
        }
        static Mesh[] Meshes(GameObject value) => value.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh)
            .Concat(value.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.sharedMesh)).ToArray();
        void RequireSourcePreserved()
        {
            Assert.AreEqual(sourceMemory, EditorJsonUtility.ToJson(source));
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(CoursePath));
            CollectionAssert.AreEqual(metaBytes, File.ReadAllBytes(CoursePath + ".meta"));
        }
        void RequireGameplayEvidence()
        {
            Assert.Greater(movementReads, 0); Assert.IsEmpty(observationFailure);
        }
        static void DestroyOwned(Object value)
        {
            if (!value) return;
            Assert.IsFalse(EditorUtility.IsPersistent(value), "Fixture cleanup must never destroy a saved asset.");
            if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var failures = new List<string>();
            movement = () => default;
            if (options != null)
            {
                if (options.ReadMovement == (Func<WordboundMovementInput>)ReadMovement) options.ReadMovement = previousMovement;
                else failures.Add("input delegate ownership changed");
            }
            if (controller && observer != null) controller.FrameChanged -= observer;
            Task stop = null;
            try { if (client) stop = client.ReturnToWorkshop(); }
            catch (Exception e) { failures.Add("shutdown start: " + e.GetType().Name); }
            if (stop != null)
            {
                double until = Time.realtimeSinceStartupAsDouble + 30;
                while (!stop.IsCompleted && Time.realtimeSinceStartupAsDouble < until) yield return null;
                if (!stop.IsCompleted || stop.IsCanceled) failures.Add("shutdown incomplete");
                if (stop.IsFaulted) failures.Add("shutdown: " + stop.Exception.GetBaseException().GetType().Name);
                if (!stop.IsCompleted) _ = stop.ContinueWith(done => { _ = done.Exception; },
                    System.Threading.CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
            if (sourceBytes != null) try { RequireSourcePreserved(); } catch (Exception e) { failures.Add("source preservation: " + e.Message); }
            DestroyOwned(root);
            DestroyOwned(owned);
            if (Application.isPlaying) yield return null;
            if (root || owned) failures.Add("owned Unity objects retained");
            if (nativeWorld) failures.Add("owned canonical world retained after shutdown");
            if (Application.isPlaying && enteredPlay) yield return new ExitPlayMode();
            root = null; owned = null; client = null; controller = null; options = null; observer = null; enteredPlay = false;
            Debug.Log("WORDBOUND_PARCEL_CLEANUP: failures=" + failures.Count);
            Assert.IsEmpty(failures, string.Join("; ", failures));
        }
    }
}
