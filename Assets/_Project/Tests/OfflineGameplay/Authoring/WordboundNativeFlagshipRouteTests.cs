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
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;
using Object = UnityEngine.Object;

namespace Wreckabulary.OfflineGameplayTests
{
    public sealed class WordboundNativeFlagshipRouteTests
    {
        const string Actor = WordboundLocalMatchClient.LocalActor;
        const string Seed = "0123456789abcdef";
        GameObject root;
        WordboundLocalMatchClient client;
        WordboundGameplayController controller;
        WordboundCourseWorld world;
        IWordboundPuzzleContent content;
        WordboundGameplayOptions options;
        Func<WordboundMovementInput> originalMovement, movement;
        Action<MatchSnapshot> observer;
        readonly Dictionary<string, byte[]> savedBytes = new Dictionary<string, byte[]>();
        readonly Dictionary<Object, string> savedMemory = new Dictionary<Object, string>();
        readonly HashSet<string> collected = new HashSet<string>();
        readonly HashSet<string> crafted = new HashSet<string>();
        readonly List<string> visited = new List<string>();
        readonly List<string> advanced = new List<string>();
        string currentModule, sourceJson, observationFailure = "";
        string meleeTarget = "";
        MeleeStats meleeStats;
        double deadline;
        bool enteredPlay;
        int movementReads;
        int playerRecovery = -1, teamRecovery = -1, bodyRecovery = -1, keepsakeRecovery = -1;
        MatchSnapshot Frame => controller.Frame;
        TeamProgress Team => Frame.Progress.Teams.Single();
        PlayerInventory Inventory => Frame.Economy.Players[0];
        WorldBodyState Player => Frame.Bodies.Single(b => b.Kind == WorldBodyKind.Player && b.SourceId == Actor);
        WorldBodyState Body(string id) => Frame.Bodies.Single(b => b.Id == id);
        ItemInstance Item(string id) => Frame.Economy.Items.Single(i => "item:" + i.Id == id);
        MatchMechanismState Mechanism(ResolvedCoursePhysicalSite site) => Frame.Mechanisms.Single(m =>
            m.Team == 0 && m.Lap == site.Lap - 1 && m.PuzzleId == site.PuzzleId);

        [UnityTest, Category("LocalFlagshipRoute"), Timeout(600000)]
        public IEnumerator OriginalStartFirstThreeRoomsAndTwoPuzzlesUseMotorCraftsAndNativeSensors()
        {
            if (Environment.GetEnvironmentVariable("WORDBOUND_LOCAL_FLAGSHIP_ROUTE") != "1")
                Assert.Ignore("Set WORDBOUND_LOCAL_FLAGSHIP_ROUTE=1 for the unchanged full flagship's first physical route leg.");
            Assert.IsFalse(Application.isPlaying);
            enteredPlay = true;
            yield return new EnterPlayMode();
            Assert.IsTrue(Application.isPlaying);
            yield return Start();
            var firstCheckpoint = content.Course.Laps[0].Checkpoints[0];
            var firstAnchor = content.Course.RecoveryAnchors.Single(a => a.Id == firstCheckpoint.RecoveryAnchorId);
            var firstCenter = Metres(firstAnchor.Position);
            Assert.Less(firstAnchor.RadiusMm / 1000f, 2f);
            yield return MoveWithinRoom(new WorldVector(firstCenter.X - 2.2f, firstCenter.Y, firstCenter.Z), .08f,
                "Approach the normal keepsake outside the first checkpoint");
            Assert.Greater(Player.Position.DistanceSquared(firstCenter), Mathf.Pow(firstAnchor.RadiusMm / 1000f + .2f, 2));
            Assert.Less(Player.Position.DistanceSquared(Body("keepsake:0").Position), 3f * 3f);
            Assert.AreEqual(0, Team.NextCheckpoint, "The stronger carrier-first proof acquires before entering the first trigger.");
            yield return Command(WordboundAction.TakeKeepsake, "keepsake:0");
            Assert.AreEqual(Actor, Team.KeepsakeCarrier);
            Assert.AreEqual(0, Team.NextCheckpoint);
            var lap = content.Course.Laps[0];
            for (int order = 0; order < 3; order++)
            {
                var checkpoint = lap.Checkpoints[order];
                yield return FollowRouteStep(0, order);
                foreach (string puzzleId in checkpoint.PuzzleIds)
                {
                    Assert.AreEqual(order, Team.NextCheckpoint, "Do not skip an unsolved ordered checkpoint.");
                    yield return Solve(content.Course.PhysicalSites.Single(s => s.Lap == 1 && s.PuzzleId == puzzleId));
                }
                var anchor = content.Course.RecoveryAnchors.Single(a => a.Id == checkpoint.RecoveryAnchorId);
                yield return MoveWithinRoom(Metres(anchor.Position), .2f, "Carry through " + checkpoint.Id);
                int expected = order + 1;
                yield return Until(() => Team.NextCheckpoint == expected, "Observe native checkpoint " + checkpoint.Id, 10);
                Assert.AreEqual(0, Team.Lap); Assert.AreEqual(expected, Team.OrderedObjectives);
            }
            CollectionAssert.AreEqual(lap.Checkpoints.Take(3).Select(c => c.ModuleId), visited);
            CollectionAssert.AreEqual(lap.Checkpoints.Take(3).Select(c => c.Id), advanced);
            Assert.AreEqual(2, Frame.Mechanisms.Count(m => m.Solved));
            Assert.AreEqual(3, crafted.Count, "HAMMER plus two TABLE objects must be genuinely crafted.");
            Assert.AreEqual(16, collected.Count, "Six tool letters, five smashed TABLE letters and five socket recovery letters.");
            Assert.AreEqual(Actor, Team.KeepsakeCarrier);
            Assert.IsFalse(Team.Finished); Assert.IsNull(Frame.Progress.Result);
            Assert.Greater(movementReads, 0); Assert.IsEmpty(observationFailure);
            RequireSourcesPreserved();
            Debug.Log("WORDBOUND_FLAGSHIP_FIRST_LEG_COMPLETE: exact full flagship; ordinary spawn; three ordered rooms; " +
                "two canonical door crossings; 16 actual letters; HAMMER melee and 3 crafts; 2 physical sockets; " +
                "3 supported checkpoints; zero fixture pose/state injection. Remaining 31 rooms/70 sites and all full laps are NOT proved.");
        }

        IEnumerator Start()
        {
            deadline = Time.realtimeSinceStartupAsDouble + 480;
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            var launch = Resources.Load<WordboundLaunchAssets>(WordboundLaunchAssets.ResourcePath);
            Assert.IsNotNull(data); Assert.IsNotNull(data.items); Assert.IsNotNull(data.rules); Assert.IsNotNull(launch);
            foreach (var asset in new Object[] { data, data.items, data.rules, launch, launch.VisualProfile, launch.CameraProfile })
                Remember(asset);
            content = WordboundLauncher.CreateSoloContent(data.items.text, data.rules.text, "TimeRun", Seed, WordboundSoloEntry.LostAddressMap);
            var document = content.Course.CopyDocument();
            sourceJson = CourseJson.ExportDocument(document);
            Assert.AreEqual(WordboundFlagshipContent.CourseId, document.Id);
            Assert.AreEqual(34, content.Course.Geometry.Count); Assert.AreEqual(72, content.Course.PhysicalSites.Count);
            Assert.AreEqual(3, content.Course.Laps.Count);
            CollectionAssert.AreEqual(new[] { 0, 8000, 16000, 24000 }, document.Layers.OrderBy(l => l.HeightMm).Select(l => l.HeightMm));
            var measures = WordboundFlagshipRoutes.Measure(content.Course);
            foreach (var measure in measures)
                Debug.Log("WORDBOUND_FLAGSHIP_ROUTE_MEASURE: lap=" + measure.Lap + "; metres=" + measure.AuthoredMetres +
                    "; shortestOrderedMetres=" + measure.ShortestOrderedCheckpointMetres + "; rooms=" + measure.RequiredRoomVisits +
                    "; heightTransitions=" + measure.VerticalConnections);
            root = new GameObject("Owned full flagship first-leg proof");
            client = root.AddComponent<WordboundLocalMatchClient>();
            yield return Completed(client.StartLocal(content, launch.BodyPrefabs(), launch.GameplayScene(),
                cameraProfile: launch.CameraProfile, visualProfile: launch.VisualProfile,
                uiTheme: launch.UiTheme, storyProfile: launch.StoryProfile), "Start normal local flagship", 60);
            controller = client.Gameplay;
            yield return Until(() => controller && controller.Frame != null && controller.CourseWorld &&
                controller.IsPresentationReady && !controller.IsSimulationPaused, "Wait for full native world", 40);
            world = controller.CourseWorld;
            Assert.AreEqual(content.Course.Hash, world.ManifestHash);
            Assert.AreEqual(content.Course.Hash, Frame.Binding.ManifestHash);
            Assert.AreEqual(content.CatalogueHash, Frame.Binding.CatalogueHash);
            Assert.AreNotEqual(Physics.defaultPhysicsScene, controller.PhysicsWorld.PhysicsScene);
            Assert.IsTrue(world.Surfaces.All(c => c && c.enabled && !c.isTrigger));
            Assert.IsTrue(world.Surfaces.Any(c => c.GetComponent<WordboundCourseNode>()?.Kind == WordboundCourseNodeKind.Wall));
            Assert.AreEqual(0, Team.Lap); Assert.AreEqual(0, Team.NextCheckpoint); Assert.AreEqual(0, Team.OrderedObjectives);
            Assert.IsFalse(Frame.Mechanisms.Any(m => m.Solved)); Assert.IsNull(Frame.Progress.Result);
            playerRecovery = Frame.Progress.Players.Single(p => p.Id == Actor).RecoveryGeneration;
            teamRecovery = Team.RecoveryGeneration; bodyRecovery = Player.RecoveryGeneration;
            keepsakeRecovery = Body("keepsake:0").RecoveryGeneration;
            var spawn = content.Course.Spawns.First(s => s.Kind == "Start");
            Assert.Less(Player.Position.DistanceSquared(Metres(spawn.Position)), .5f * .5f, "Start at the production spawn, without fixture repositioning.");
            currentModule = spawn.ModuleId;
            Assert.AreEqual(content.Course.Laps[0].StartModuleId, currentModule);
            var field = typeof(WordboundGameplayController).GetField("options", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field); options = (WordboundGameplayOptions)field.GetValue(controller);
            originalMovement = options.ReadMovement; Assert.IsNotNull(originalMovement);
            movement = () => default; options.ReadMovement = ReadMovement;
            int priorOrder = 0;
            observer = frame => {
                var team = frame.Progress.Teams.Single();
                if (team.RecoveryGeneration != teamRecovery ||
                    frame.Progress.Players.Single(p => p.Id == Actor).RecoveryGeneration != playerRecovery ||
                    frame.Bodies.Single(b => b.Kind == WorldBodyKind.Player && b.SourceId == Actor).RecoveryGeneration != bodyRecovery ||
                    frame.Bodies.Single(b => b.Id == "keepsake:0").RecoveryGeneration != keepsakeRecovery)
                    observationFailure = "Recovery teleport or keepsake auto-return occurred during the route proof.";
                if (team.Lap != 0 || team.OrderedObjectives != team.NextCheckpoint || team.NextCheckpoint < priorOrder || team.NextCheckpoint > priorOrder + 1)
                    observationFailure = "Unexpected, skipped or regressed first-leg progression.";
                if (team.NextCheckpoint == priorOrder + 1)
                {
                    var checkpoint = content.Course.Laps[0].Checkpoints[priorOrder];
                    var anchor = content.Course.RecoveryAnchors.Single(a => a.Id == checkpoint.RecoveryAnchorId);
                    var actor = frame.Bodies.Single(b => b.Kind == WorldBodyKind.Player && b.SourceId == Actor);
                    var body = controller.Bodies[actor.Id];
                    var native = body.Rigidbody.position;
                    var center = Metres(anchor.Position);
                    float radius = anchor.RadiusMm / 1000f;
                    bool supported = CanonicalSupport(native);
                    string diagnostic = "checkpoint=" + checkpoint.Id + "; tick=" + frame.Progress.Tick +
                        "; canonical=" + Coordinates(actor.Position) + "; native=" + Coordinates(new WorldVector(native.x, native.y, native.z)) +
                        "; anchor=" + Coordinates(center) + "; radius=" + radius + "; supported=" + supported +
                        "; carrier=" + (team.KeepsakeCarrier ?? "none");
                    Debug.Log("WORDBOUND_FLAGSHIP_CHECKPOINT: " + diagnostic);
                    if (team.KeepsakeCarrier != Actor || actor.Position.DistanceSquared(center) > radius * radius ||
                        (native - new Vector3(center.X, center.Y, center.Z)).sqrMagnitude > radius * radius || !supported ||
                        !controller.PhysicsWorld.CanMove(Actor, out _) || controller.PhysicsWorld.AdmittedActor(body.Object.InputAuthority) != Actor)
                        observationFailure = "Carrier-first proof requires the admitted, supported native actor inside the authored checkpoint radius; " + diagnostic;
                    advanced.Add(checkpoint.Id);
                }
                priorOrder = team.NextCheckpoint;
            };
            controller.FrameChanged += observer;
            Debug.Log("WORDBOUND_FLAGSHIP_START: source=" + content.Course.SourceHash + "; manifest=" + content.Course.Hash +
                "; geometry=" + content.Geometry.Hash + "; catalogue=" + content.CatalogueHash + "; spawn=" + spawn.Id + "; player=" + Coordinates(Player.Position) +
                "; initialRecovery=" + playerRecovery + "/" + teamRecovery + "/" + bodyRecovery + "/" + keepsakeRecovery);
            Debug.Log("WORDBOUND_FLAGSHIP_FULL_PATH: " + string.Join(" | ", content.Course.Laps.Select(l =>
                "lap=" + l.Number + "; rooms=" + string.Join(",", l.Checkpoints.Select(c => c.ModuleId)) +
                "; connections=" + string.Join(",", l.ConnectionIds))));
        }

        IEnumerator FollowRouteStep(int lapIndex, int order)
        {
            var route = content.Course.Laps[lapIndex];
            var checkpoint = route.Checkpoints[order];
            if (order == 0) Assert.AreEqual(route.StartModuleId, currentModule);
            else
            {
                var edge = content.Course.Connections.Single(e => e.Id == route.ConnectionIds[order - 1]);
                bool forward = edge.FromModuleId == currentModule;
                Assert.IsTrue(forward || edge.Bidirectional && edge.ToModuleId == currentModule);
                string next = forward ? edge.ToModuleId : edge.FromModuleId;
                Assert.AreEqual(checkpoint.ModuleId, next, "Follow the exact ordered route, not a shortcut.");
                yield return MoveWithinRoom(Metres(content.Course.Geometry.Single(m => m.Id == currentModule).Position), .1f, "Reach departure aisle");
                yield return Walk(() => Metres(forward ? edge.From : edge.To), .1f, "Enter canonical connection " + edge.Id);
                yield return Walk(() => Metres(forward ? edge.To : edge.From), .1f, "Cross canonical connection " + edge.Id);
                currentModule = next;
                yield return Walk(() => Metres(content.Course.Geometry.Single(m => m.Id == currentModule).Position), .15f, "Reach arrival aisle");
            }
            Assert.AreEqual(checkpoint.ModuleId, currentModule);
            visited.Add(currentModule);
            Debug.Log("WORDBOUND_FLAGSHIP_ROOM: " + currentModule + "; checkpoint=" + Team.NextCheckpoint + "; player=" + Coordinates(Player.Position));
        }

        IEnumerator Solve(ResolvedCoursePhysicalSite site)
        {
            var puzzle = content.Course.Puzzles.Single(p => p.Id == site.PuzzleId);
            Assert.AreEqual(currentModule, puzzle.ModuleId);
            Assert.IsTrue(puzzle.PresetId == "CollectLetters" || puzzle.PresetId == "PlaceObject", "This discriminating first-leg gate proves only the first two families.");
            if (puzzle.PresetId == "CollectLetters")
            {
                yield return CollectSupply(site.ToolRecoverySupplyId);
                string tool = null; yield return Craft(site.ToolRecipeId, id => tool = id);
                yield return Command(WordboundAction.Equip, slot: Array.IndexOf(Inventory.Slots, Item(tool).Id));
                var placement = content.Course.Placements.Single(p => p.ZoneId == site.PropSourceZoneId);
                string marked = Frame.Bodies.Single(b => b.Kind == WorldBodyKind.Item && b.SourceId == placement.Id).Id;
                Assert.AreEqual(ItemOrigin.Map, Item(marked).Origin);
                var hammer = content.Items.Get(site.ToolRecipeId);
                string mapWord = content.Geometry.Assets[placement.AssetId].Letters;
                var economyRules = content.Rules;
                float expectedToughness = economyRules.FurnitureToughnessBase + economyRules.FurnitureToughnessPerLetter * mapWord.Length;
                Assert.AreEqual(mapWord, Item(marked).Word);
                Assert.Greater(expectedToughness, 0);
                Assert.AreEqual(expectedToughness, Item(marked).Durability, .001f, "Map furniture must use the bound economy toughness rule.");
                int expectedStrikes = (int)Math.Ceiling(expectedToughness / hammer.Melee.BreakPower);
                int strikes = expectedStrikes + 2, damageObservations = 0;
                Debug.Log("WORDBOUND_FLAGSHIP_MAP_TOUGHNESS: word=" + mapWord + "; base=" + economyRules.FurnitureToughnessBase +
                    "; perLetter=" + economyRules.FurnitureToughnessPerLetter + "; toughness=" + expectedToughness +
                    "; breakPower=" + hammer.Melee.BreakPower + "; expectedDamageObservations=" + expectedStrikes);
                meleeTarget = marked; meleeStats = hammer.Melee;
                var initial = MeleeStance(marked);
                AssertMeleeDestination(marked, initial, hammer.Melee);
                Debug.Log("WORDBOUND_FLAGSHIP_MELEE_APPROACH: " + MeleeDiagnostic());
                yield return MoveWithinRoom(initial, .04f, "Approach the aisle-facing furniture edge");
                for (int attempt = 0; attempt < strikes && Item(marked).State != ItemState.Destroyed; attempt++)
                {
                    yield return Until(() => Frame.Progress.Tick >= Frame.Actors.Single(a => a.PlayerId == Actor).NextAttackTick, "Respect HAMMER cooldown", 5);
                    AssertMeleeDestination(marked, MeleeStance(marked), hammer.Melee);
                    yield return Walk(() => MeleeStance(marked), .04f, "Reach the non-overlapping actual collider edge");
                    var target = Body(marked).Position;
                    yield return Face(Mathf.Atan2(target.X - Player.Position.X, target.Z - Player.Position.Z) * Mathf.Rad2Deg);
                    AssertMeleeContact(marked, hammer.Melee);
                    float previous = Item(marked).Durability;
                    Debug.Log("WORDBOUND_FLAGSHIP_MELEE_STRIKE: number=" + (attempt + 1) + "; durability=" + previous + "; " + MeleeDiagnostic());
                    yield return Command(WordboundAction.Attack);
                    yield return Until(() => Item(marked).Durability < previous, "Observe real melee damage", 5);
                    Assert.AreEqual(Math.Max(0, previous - hammer.Melee.BreakPower), Item(marked).Durability, .001f);
                    damageObservations++;
                    Debug.Log("WORDBOUND_FLAGSHIP_MELEE_DAMAGE: number=" + damageObservations + "; before=" + previous + "; after=" + Item(marked).Durability);
                }
                Assert.AreEqual(ItemState.Destroyed, Item(marked).State);
                Assert.AreEqual(expectedStrikes, damageObservations);
                meleeTarget = ""; meleeStats = null;
                var fragments = Frame.Bodies.Where(b => b.Kind == WorldBodyKind.Tile && b.SourceId == placement.Id).Select(b => b.Id).ToArray();
                CollectionAssert.AreEquivalent(puzzle.RequiredLetters.ToCharArray(), fragments.Select(id => Frame.Economy.Tiles.Single(t => "tile:" + t.Key == id).Value));
                foreach (string id in fragments) yield return Collect(id);
            }
            else yield return CollectSupply(site.RecoverySupplyId);
            string recipe = null; yield return Craft(puzzle.RecipeId, id => recipe = id);
            var socket = Metres(site.Position);
            yield return MoveWithinRoom(new WorldVector(socket.X, socket.Y, socket.Z - 1.5f), .04f, "Reach socket approach");
            yield return Face(site.Yaw);
            yield return Command(WordboundAction.Deploy, slot: Array.IndexOf(Inventory.Slots, Item(recipe).Id));
            movement = () => default;
            Assert.AreEqual(ItemState.Deployed, Item(recipe).State);
            var native = controller.Bodies[recipe];
            Assert.IsTrue(native.Rigidbody.isKinematic);
            Assert.IsTrue(native.GetComponent<BoxCollider>().enabled);
            Assert.Less(HorizontalDistance(Body(recipe).Position, socket), site.SocketRadiusMm / 1000f);
            yield return Until(() => Mechanism(site).Physical.Stage == MatchPhysicalStage.Complete, "Observe actual socket support and hold", 15);
            Assert.AreEqual(recipe, Mechanism(site).ItemEntityId); Assert.AreEqual(site.Hash, Mechanism(site).Physical.SiteHash);
            Assert.AreEqual(Actor, Mechanism(site).Physical.ActorId);
            Assert.GreaterOrEqual(Mechanism(site).ProgressTicks, site.HoldTicks);
            yield return Command(WordboundAction.UsePuzzle, site.PuzzleId, value: recipe);
            Assert.IsTrue(Mechanism(site).Solved); Assert.IsTrue(Item(recipe).IsGone);
            Assert.IsFalse(controller.Bodies.ContainsKey(recipe));
            Debug.Log("WORDBOUND_FLAGSHIP_SOCKET: " + puzzle.PresetId + "; " + site.PuzzleId + "; site=" + site.Hash + "; realItem=" + recipe);
        }

        IEnumerator CollectSupply(string sourceId)
        {
            int team = content.Course.Play.TeamCount > 0 ? 0 : -1;
            var supply = content.Course.Supplies.Single(s => s.SourceId == sourceId && s.TeamIndex == team);
            Assert.IsTrue(supply.Protected && supply.Renewable);
            yield return Until(() => Frame.Bodies.Count(b => b.Kind == WorldBodyKind.Tile && b.SourceId == supply.Id) == supply.Letters.Length, "Expose current recovery letters", 15);
            yield return MoveWithinRoom(Metres(supply.Position), .8f, "Reach recovery letters through actual room");
            foreach (var tile in Frame.Bodies.Where(b => b.Kind == WorldBodyKind.Tile && b.SourceId == supply.Id).ToArray())
            {
                Assert.IsTrue(tile.Protected); Assert.AreEqual(team, tile.OwnerTeam);
                yield return Collect(tile.Id);
            }
        }
        IEnumerator Collect(string id)
        {
            Assert.IsTrue(collected.Add(id));
            yield return Walk(() => Body(id).Position, .85f, "Reach actual letter " + id);
            yield return Command(WordboundAction.CollectLetter, id);
            Assert.IsFalse(Frame.Bodies.Any(b => b.Id == id));
        }
        IEnumerator Craft(string word, Action<string> receive)
        {
            Assert.IsTrue(Inventory.Letters.Contains(content.Items.Get(word).Letters));
            int before = Frame.Economy.NextItemId;
            yield return Command(WordboundAction.BeginCraft, value: word);
            yield return Until(() => Frame.Economy.Items.Any(i => i.Id >= before && i.Word == word && i.Origin == ItemOrigin.Crafted && i.HolderId == 0 && i.State == ItemState.Held), "Complete unchanged " + word + " craft", 20);
            var item = Frame.Economy.Items.Single(i => i.Id >= before && i.Word == word && i.Origin == ItemOrigin.Crafted && i.HolderId == 0 && i.State == ItemState.Held);
            string id = "item:" + item.Id; Assert.IsTrue(crafted.Add(id)); Assert.IsTrue(Inventory.Reserved.IsEmpty);
            receive(id);
        }
        IEnumerator MoveWithinRoom(WorldVector target, float radius, string label)
        {
            var module = content.Course.Geometry.Single(m => m.Id == currentModule);
            var center = Metres(module.Position);
            Assert.That(target.X, Is.InRange(module.Bounds.Min.X / 1000f, module.Bounds.Max.X / 1000f));
            Assert.That(target.Z, Is.InRange(module.Bounds.Min.Z / 1000f, module.Bounds.Max.Z / 1000f));
            yield return Walk(() => new WorldVector(Player.Position.X, center.Y, center.Z), .12f, "Enter current room center aisle");
            yield return Walk(() => new WorldVector(target.X, center.Y, center.Z), .12f, "Follow current room center aisle");
            yield return Walk(() => target, radius, label);
        }
        IEnumerator Walk(Func<WorldVector> target, float radius, string label)
        {
            movement = () => {
                var at = controller.Bodies[Player.Id].Rigidbody.position; var goal = target();
                var delta = new Vector2(goal.X - at.x, goal.Z - at.z);
                if (delta.magnitude <= radius) return default;
                var input = delta.normalized * Mathf.Clamp((delta.magnitude - radius * .5f) * .1f, .06f, .8f);
                return new WordboundMovementInput { MoveX = input.x, MoveZ = input.y };
            };
            try { yield return Until(() => HorizontalDistance(Player.Position, target()) <= radius && Mathf.Abs(Player.Position.Y - target().Y) < .4f, label, 60); }
            finally { movement = () => default; }
            yield return Until(() => SpeedSquared() < .01f, "Settle through unchanged motor", 10);
            var hits = new RaycastHit[32]; var foot = controller.Bodies[Player.Id].Rigidbody.position;
            int count = controller.PhysicsWorld.PhysicsScene.Raycast(foot + Vector3.up * .08f, Vector3.down, hits, .22f, ~0, QueryTriggerInteraction.Ignore);
            Assert.Less(count, hits.Length);
            Assert.IsTrue(hits.Take(count).Any(h => world.Surfaces.Contains(h.collider) && h.normal.y > .8f), label + " requires actual canonical floor/ramp support.");
        }
        IEnumerator Face(float yaw)
        {
            movement = () => new WordboundMovementInput { HasFacing = true, FacingYaw = yaw };
            yield return Until(() => Quaternion.Angle(controller.Bodies[Player.Id].Rigidbody.rotation, Quaternion.Euler(0, yaw, 0)) < .5f && SpeedSquared() < .01f, "Face using admitted motor input", 10);
        }
        WordboundMovementInput ReadMovement() { movementReads++; return movement?.Invoke() ?? default; }
        float SpeedSquared() => Player.Velocity.X * Player.Velocity.X + Player.Velocity.Z * Player.Velocity.Z;
        static float HorizontalDistance(WorldVector a, WorldVector b) => new Vector2(a.X - b.X, a.Z - b.Z).magnitude;
        static WorldVector Metres(CoursePoint p) => new WorldVector(p.X / 1000f, p.Y / 1000f, p.Z / 1000f);
        static string Coordinates(WorldVector p) => FormattableString.Invariant($"({p.X:F4},{p.Y:F4},{p.Z:F4})");
        static WorldVector Value(Vector3 p) => new WorldVector(p.x, p.y, p.z);
        WorldVector MeleeStance(string id)
        {
            var box = controller.Bodies[id].GetComponent<BoxCollider>();
            var capsule = controller.Bodies[Player.Id].GetComponent<CapsuleCollider>();
            var bounds = box.bounds;
            float radius = capsule.radius * Mathf.Max(Mathf.Abs(capsule.transform.lossyScale.x), Mathf.Abs(capsule.transform.lossyScale.z));
            float floor = content.Course.Geometry.Single(m => m.Id == currentModule).Position.Y / 1000f;
            return new WorldVector(controller.Bodies[id].Rigidbody.position.x, floor, bounds.max.z + radius + .12f);
        }
        void AssertMeleeDestination(string id, WorldVector destination, MeleeStats stats)
        {
            var room = content.Course.Geometry.Single(m => m.Id == currentModule);
            var box = controller.Bodies[id].GetComponent<BoxCollider>();
            Assert.IsNotNull(box); Assert.IsTrue(box.enabled && !box.isTrigger);
            Assert.AreSame(controller.Bodies[id].Rigidbody, box.attachedRigidbody);
            Assert.That(destination.X, Is.InRange(room.Bounds.Min.X / 1000f + .4f, room.Bounds.Max.X / 1000f - .4f), MeleeDiagnostic());
            Assert.That(destination.Z, Is.InRange(room.Bounds.Min.Z / 1000f + .4f, room.Bounds.Max.Z / 1000f - .4f), MeleeDiagnostic());
            Assert.Less(destination.DistanceSquared(Body(id).Position), Mathf.Pow(stats.Reach - .05f, 2), MeleeDiagnostic());
        }
        void AssertMeleeContact(string id, MeleeStats stats)
        {
            var actor = controller.Bodies[Player.Id];
            var target = controller.Bodies[id];
            var capsule = actor.GetComponent<CapsuleCollider>();
            var box = target.GetComponent<BoxCollider>();
            string diagnostic = MeleeDiagnostic();
            Assert.LessOrEqual(Player.Position.DistanceSquared(Body(id).Position), stats.Reach * stats.Reach, diagnostic);
            var delta = target.Rigidbody.position - actor.Rigidbody.position; delta.y = 0;
            Assert.LessOrEqual(Vector3.Angle(actor.Rigidbody.rotation * Vector3.forward, delta), stats.ArcDegrees / 2, diagnostic);
            Assert.IsFalse(Physics.ComputePenetration(capsule, capsule.transform.position, capsule.transform.rotation,
                box, box.transform.position, box.transform.rotation, out _, out _), diagnostic);
            Assert.IsTrue(CanonicalSupport(actor.Rigidbody.position), diagnostic);
        }
        string MeleeDiagnostic()
        {
            if (meleeTarget.Length == 0 || controller == null || !controller.Bodies.TryGetValue(meleeTarget, out var target)) return "";
            var actor = controller.Bodies[Player.Id]; var box = target.GetComponent<BoxCollider>();
            var bounds = box.bounds;
            return "target=" + meleeTarget + "; actorCanonical=" + Coordinates(Player.Position) +
                "; actorNative=" + Coordinates(Value(actor.Rigidbody.position)) + "; targetCanonical=" + Coordinates(Body(meleeTarget).Position) +
                "; targetNative=" + Coordinates(Value(target.Rigidbody.position)) + "; targetVelocity=" + Coordinates(Value(target.Rigidbody.linearVelocity)) +
                "; boundsMin=" + Coordinates(Value(bounds.min)) + "; boundsMax=" + Coordinates(Value(bounds.max)) +
                "; boxSize=" + Coordinates(Value(box.size)) + "; boxCenter=" + Coordinates(Value(box.center)) +
                "; stance=" + Coordinates(MeleeStance(meleeTarget)) + "; distance=" + Math.Sqrt(Player.Position.DistanceSquared(Body(meleeTarget).Position)) +
                "; reach=" + meleeStats.Reach + "; arc=" + meleeStats.ArcDegrees;
        }
        bool CanonicalSupport(Vector3 foot)
        {
            var hits = new RaycastHit[32];
            int count = controller.PhysicsWorld.PhysicsScene.Raycast(foot + Vector3.up * .1f, Vector3.down, hits, .2f, ~0, QueryTriggerInteraction.Ignore);
            return count < hits.Length && hits.Take(count).Any(h => world.Surfaces.Contains(h.collider) && h.normal.y >= .819f);
        }
        IEnumerator Command(WordboundAction action, string target = "", int revision = 0, int slot = -1, string value = "")
        {
            var command = controller.Submit(action, target, revision, slot, value);
            yield return Until(() => Frame.Commands.Frontiers.TryGetValue(Actor, out long frontier) && frontier >= command.Sequence, "Commit " + action, 10);
            var receipt = Frame.Commands.Receipts.Single(r => r.ActorId == Actor && r.Sequence == command.Sequence);
            Assert.AreEqual(command.PayloadHash, receipt.PayloadHash); Assert.AreEqual("Ok", receipt.Effect.Code, action.ToString());
        }
        IEnumerator Until(Func<bool> ready, string label, double seconds)
        {
            double limit = Time.realtimeSinceStartupAsDouble + seconds;
            Debug.Log("WORDBOUND_FLAGSHIP_STAGE: " + label);
            while (!ready() && Time.realtimeSinceStartupAsDouble < limit) { Healthy(); yield return null; }
            Healthy(); Assert.IsTrue(ready(), label + "; tick=" + (controller?.Frame?.Progress.Tick ?? -1) + "; player=" +
                (controller?.Frame == null ? "absent" : Coordinates(Player.Position)) + "; " + MeleeDiagnostic());
        }
        void Healthy()
        {
            Assert.Less(Time.realtimeSinceStartupAsDouble, deadline, "Bounded first-leg test timed out.");
            if (controller) Assert.AreEqual("", controller.Fault);
            Assert.IsEmpty(observationFailure);
            if (controller && controller.Frame != null)
            {
                Assert.AreEqual(0, Frame.Progress.Players.Single(p => p.Id == Actor).Knockouts, "Recovery cannot stand in for traversing the route.");
                if (playerRecovery >= 0)
                {
                    Assert.AreEqual(playerRecovery, Frame.Progress.Players.Single(p => p.Id == Actor).RecoveryGeneration);
                    Assert.AreEqual(teamRecovery, Team.RecoveryGeneration);
                    Assert.AreEqual(bodyRecovery, Player.RecoveryGeneration);
                    Assert.AreEqual(keepsakeRecovery, Body("keepsake:0").RecoveryGeneration);
                }
                Assert.IsNull(Frame.Progress.Result, "This first-leg proof must not inject a terminal result.");
            }
        }
        static IEnumerator Completed(Task task, string label, double seconds)
        {
            double limit = Time.realtimeSinceStartupAsDouble + seconds;
            while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < limit) yield return null;
            Assert.IsTrue(task.IsCompleted, label + " timed out"); Assert.IsFalse(task.IsCanceled);
            Assert.IsFalse(task.IsFaulted, label + ": " + task.Exception?.GetBaseException());
        }
        void Remember(Object asset)
        {
            if (!asset) return;
            Assert.IsFalse(EditorUtility.IsDirty(asset), "Save or revert source edits before native verification.");
            savedMemory[asset] = EditorJsonUtility.ToJson(asset);
            string path = AssetDatabase.GetAssetPath(asset);
            if (File.Exists(path)) savedBytes[path] = File.ReadAllBytes(path);
            if (File.Exists(path + ".meta")) savedBytes[path + ".meta"] = File.ReadAllBytes(path + ".meta");
        }
        void RequireSourcesPreserved()
        {
            Assert.AreEqual(sourceJson, CourseJson.ExportDocument(content.Course.CopyDocument()));
            foreach (var pair in savedBytes) CollectionAssert.AreEqual(pair.Value, File.ReadAllBytes(pair.Key), pair.Key);
            foreach (var pair in savedMemory) Assert.AreEqual(pair.Value, EditorJsonUtility.ToJson(pair.Key), pair.Key.name);
        }
        static void DestroyOwned(Object value)
        {
            if (!value) return;
            Assert.IsFalse(EditorUtility.IsPersistent(value));
            if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var failures = new List<string>(); movement = () => default;
            if (options != null)
            {
                if (options.ReadMovement == (Func<WordboundMovementInput>)ReadMovement) options.ReadMovement = originalMovement;
                else failures.Add("input ownership changed");
            }
            if (controller && observer != null) controller.FrameChanged -= observer;
            Task stop = null;
            try { if (client) stop = client.ReturnToWorkshop(); } catch (Exception e) { failures.Add("shutdown: " + e.GetType().Name); }
            if (stop != null)
            {
                double limit = Time.realtimeSinceStartupAsDouble + 30;
                while (!stop.IsCompleted && Time.realtimeSinceStartupAsDouble < limit) yield return null;
                if (!stop.IsCompleted || stop.IsCanceled) failures.Add("shutdown incomplete");
                if (stop.IsFaulted) failures.Add("shutdown: " + stop.Exception.GetBaseException().GetType().Name);
                if (!stop.IsCompleted) _ = stop.ContinueWith(done => { _ = done.Exception; }, System.Threading.CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            if (content != null) try { RequireSourcesPreserved(); } catch (Exception e) { failures.Add("preservation: " + e.Message); }
            DestroyOwned(root);
            if (Application.isPlaying) yield return null;
            if (root || world) failures.Add("owned world retained");
            if (Application.isPlaying && enteredPlay) yield return new ExitPlayMode();
            root = null; client = null; controller = null; world = null; options = null; observer = null; enteredPlay = false;
            Debug.Log("WORDBOUND_FLAGSHIP_FIRST_LEG_CLEANUP: failures=" + failures.Count);
            Assert.IsEmpty(failures, string.Join("; ", failures));
        }
    }
}
