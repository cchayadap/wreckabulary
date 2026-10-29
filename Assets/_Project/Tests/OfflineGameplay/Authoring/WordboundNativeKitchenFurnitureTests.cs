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
    public sealed class WordboundNativeKitchenFurnitureTests
    {
        const string CoursePath = "Assets/_Project/Data/Wordbound/Authoring/FlagshipKitchenV1/Course.asset";
        const string Actor = WordboundLocalMatchClient.LocalActor;
        const string Room = "Kitchen_ServiceLanding";
        WordboundCourseAsset source;
        GameObject root;
        WordboundLocalMatchClient client;
        WordboundGameplayController controller;
        WordboundCourseWorld world;
        IWordboundPuzzleContent content;
        WordboundGameplayOptions options;
        Func<WordboundMovementInput> previousMovement, movement;
        readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();
        readonly Dictionary<Object, string> memory = new Dictionary<Object, string>();
        string diagnosticJson, observationFailure = "";
        Action<MatchSnapshot> observer;
        double deadline;
        bool enteredPlay;
        int movementReads, recovery;
        MatchSnapshot Frame => controller.Frame;
        PlayerInventory Inventory => Frame.Economy.Players[0];
        WorldBodyState Player => Frame.Bodies.Single(b => b.Kind == WorldBodyKind.Player && b.SourceId == Actor);
        WorldBodyState Body(string id) => Frame.Bodies.Single(b => b.Id == id);
        ItemInstance Item(string id) => Frame.Economy.Items.Single(i => "item:" + i.Id == id);

        [UnityTest, Category("LocalKitchenFurniture"), Timeout(600000)]
        public IEnumerator FridgeUsesNativeCarryDropThrowSnapshotAndOwnLetterBreak()
        {
            RequireEntry(); yield return new EnterPlayMode();
            yield return Exercise("FRIDGE", "KitchenPilot_Fridge");
        }
        [UnityTest, Category("LocalKitchenFurniture"), Timeout(600000)]
        public IEnumerator CounterUsesNativeCarryDropThrowSnapshotAndOwnLetterBreak()
        {
            RequireEntry(); yield return new EnterPlayMode();
            yield return Exercise("COUNTER", "KitchenPilot_CounterA");
        }
        [UnityTest, Category("LocalKitchenFurniture"), Timeout(600000)]
        public IEnumerator StoveUsesNativeCarryDropThrowSnapshotAndOwnLetterBreak()
        {
            RequireEntry(); yield return new EnterPlayMode();
            yield return Exercise("STOVE", "KitchenPilot_Stove");
        }
        void RequireEntry()
        {
            if (Environment.GetEnvironmentVariable("WORDBOUND_LOCAL_KITCHEN") != "1")
                Assert.Ignore("Set WORDBOUND_LOCAL_KITCHEN=1 for isolated real Kitchen furniture physics. This does not prove the full route.");
            Assert.IsFalse(Application.isPlaying); enteredPlay = true;
        }

        IEnumerator Exercise(string word, string zone)
        {
            yield return Start();
            var definition = content.Items.Get(word);
            Assert.IsFalse(definition.Enabled); Assert.IsTrue(definition.IsTwoHanded);
            Assert.IsFalse(content.Geometry.Assets[word].CanCraft);
            Assert.IsFalse(content.Mode.Recipes.Contains(word));
            var placement = content.Course.Placements.Single(p => p.ZoneId == zone);
            string id = Frame.Bodies.Single(b => b.Kind == WorldBodyKind.Item && b.SourceId == placement.Id).Id;
            int itemRecovery = Body(id).RecoveryGeneration;
            Assert.AreEqual(word, Item(id).Word); Assert.AreEqual(ItemOrigin.Map, Item(id).Origin);
            Assert.AreEqual(ItemState.World, Item(id).State);
            Assert.Less(Body(id).Position.DistanceSquared(Metres(placement.Position)), .05f * .05f);
            RequireShapeAndArtwork(id, definition);
            Snapshot(id, ItemState.World, "initial");
            yield return Command(WordboundAction.BeginCraft, value: word, expected: "RecipeDisabled");
            yield return CraftHammer();
            yield return Approach(id);
            yield return PickUp(id);
            yield return Until(() => HeldArt(word) != null, "Show the original carried model", 10);
            RequireHeldGrip(word, definition);
            Snapshot(id, ItemState.Carried, "carried");

            var room = content.Course.Geometry.Single(m => m.Id == Room);
            var centre = Metres(room.Position);
            yield return Walk(() => new WorldVector(Player.Position.X, centre.Y, centre.Z), .1f, "Carry into the clear centre aisle");
            yield return Walk(() => centre, .08f, "Carry along the centre aisle");
            var wall = new WorldVector(centre.X, centre.Y, room.Bounds.Max.Z / 1000f - .6f);
            yield return Walk(() => wall, .04f, "Reach the closed diagnostic wall through the motor");
            yield return Face(0);
            int heldRevision = Item(id).Revision;
            yield return Command(WordboundAction.Drop, expected: "BlockedRelease");
            yield return Command(WordboundAction.Throw, expected: "BlockedRelease");
            Assert.AreEqual(heldRevision, Item(id).Revision);
            Assert.AreEqual(Item(id).Id, Inventory.CarriedFurniture);
            Snapshot(id, ItemState.Carried, "blocked-wall-release");

            yield return Walk(() => centre, .08f, "Return to a clear release location");
            yield return Face(0);
            yield return Command(WordboundAction.Drop);
            Assert.AreEqual(-1, Inventory.CarriedFurniture); Assert.AreEqual(ItemState.World, Item(id).State);
            yield return Settle(id);
            RequireShapeAndArtwork(id, definition); RequireNoOverlap(id);
            Snapshot(id, ItemState.World, "dropped");
            yield return Approach(id); yield return PickUp(id);
            yield return Walk(() => centre, .08f, "Return carried furniture to the clear throwing lane");
            yield return Face(0);
            var beforeThrow = Player.Position;
            yield return Command(WordboundAction.Throw);
            Assert.AreEqual(-1, Inventory.CarriedFurniture);
            Assert.AreEqual(ItemState.Thrown, Item(id).State);
            Assert.IsTrue(Body(id).Active); Assert.IsFalse(Body(id).Kinematic);
            Assert.Greater(Body(id).Velocity.DistanceSquared(default), 1f);
            Assert.AreEqual(0, Frame.ThrownFlights.Count(f => f.ItemBodyId == id), "Furniture is not the BALL damage-flight policy.");
            Snapshot(id, ItemState.Thrown, "thrown");
            yield return Until(() => Body(id).Position.DistanceSquared(beforeThrow) > 2f, "Observe actual thrown displacement", 10);
            yield return Until(() => Item(id).State == ItemState.World, "Settle the actual thrown body", 30);
            yield return Settle(id);
            Assert.AreEqual(itemRecovery, Body(id).RecoveryGeneration, "A recovery teleport cannot stand in for a successful throw.");
            RequireShapeAndArtwork(id, definition);
            Snapshot(id, ItemState.World, "settled-throw");
            yield return Approach(id); yield return PickUp(id);
            yield return Walk(() => centre, .08f, "Carry the settled prop back through the motor");
            yield return Face(0); yield return Command(WordboundAction.Drop); yield return Settle(id);
            yield return BreakWithHammer(id, placement.Id, word);
            Snapshot(id, ItemState.Destroyed, "broken-own-word");
            Assert.Greater(movementReads, 0); Assert.IsEmpty(observationFailure);
            RequirePreserved();
            Debug.Log("[Kitchen pilot] NATIVE_FURNITURE_COMPLETE: " + word +
                "; real spawn/model/collider, motor carry, blocked wall releases, clear drop/throw, canonical snapshot roundtrips, HAMMER damage and exact own letters. " +
                "Isolated copied Kitchen encounter only; no full-course route, public networking, camera or visual acceptance claim.");
        }

        IEnumerator Start()
        {
            Assert.IsTrue(Application.isPlaying);
            deadline = Time.realtimeSinceStartupAsDouble + 480;
            files.Clear(); memory.Clear(); movementReads = 0; observationFailure = "";
            source = AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(CoursePath);
            Assert.IsNotNull(source, "Create the reviewed saved Kitchen course before this gate.");
            var launch = Resources.Load<WordboundLaunchAssets>(WordboundLaunchAssets.ResourcePath);
            Assert.IsNotNull(launch); Assert.IsNotNull(source.Items); Assert.IsNotNull(source.RuleBook);
            foreach (var asset in new Object[] { source, source.Items, source.RuleBook, source.CameraProfile,
                source.VisualProfile, launch, launch.UiTheme, launch.StoryProfile, ModelLibrary.Load() }) Remember(asset);
            var full = source.ReadDocument();
            Assert.AreEqual("wordbound-lost-address-kitchen-v1", full.Id);
            Assert.AreEqual(34, full.Modules.Count); Assert.AreEqual(72, full.PhysicalSites.Count); Assert.AreEqual(3, full.Laps.Count);
            var geometry = source.Catalogue();
            var module = full.Modules.Single(m => m.Id == Room);
            var zones = full.Zones.Where(z => z.Id.StartsWith("KitchenPilot_", StringComparison.Ordinal)).ToList();
            Assert.AreEqual(4, zones.Count); Assert.IsTrue(zones.All(z => z.ModuleId == Room && z.Purpose == "Props" && z.Placement == "Fixed"));
            Assert.AreEqual(25, zones.Sum(z => geometry.Assets[z.Pool.Single().AssetId].Letters.Length));
            var diagnostic = new CourseDocument { Id = "kitchen-native-encounter", Title = "Isolated Kitchen physics diagnostic",
                ThemeId = full.ThemeId, SupportedModes = new List<string> { "Freestyle" },
                Modules = new List<CourseModule> { module }, Layers = full.Layers.Where(l => l.Id == module.LayerId).ToList(), Zones = zones };
            for (int i = 0; i < 8; i++) diagnostic.Spawns.Add(new CourseSpawn { Id = "Start" + i, ModuleId = Room,
                Position = new CoursePoint(module.Position.X - 7000 + i * 2000, module.Position.Y, module.Position.Z - 6000) });
            diagnostic.RecoveryAnchors.Add(new CourseRecovery { Id = "KitchenDiagnosticSafe", ModuleId = Room,
                Position = new CoursePoint(module.Position.X, module.Position.Y, module.Position.Z - 8500), RadiusMm = 500 });
            diagnostic.Supplies.Add(new CourseSupply { Id = "KitchenDiagnosticHammer", ModuleId = Room, Letters = "HAMMER",
                Position = new CoursePoint(module.Position.X, module.Position.Y, module.Position.Z - 2000) });
            var mode = ModeDefinition.For("Freestyle", RaceFormat.Solo);
            var resolved = CourseResolver.Resolve(diagnostic, geometry, new CourseResolveRequest(source.Seed, MatchStateValidation.Requirements(mode)));
            Assert.IsTrue(resolved.Success, string.Join("; ", resolved.Validation.Issues));
            content = new WordboundAuthoredPuzzleContent(mode, geometry, resolved.Manifest, source.Items.text, source.RuleBook.text);
            diagnosticJson = CourseJson.ExportDocument(content.Course.CopyDocument());
            foreach (string word in new[] { "FRIDGE", "COUNTER", "STOVE" })
            {
                var model = ModelLibrary.Load().Find(content.Items.Get(word).Model); Assert.IsNotNull(model);
                Remember(model);
                foreach (string path in AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(model), true))
                {
                    RememberFile(path); RememberFile(path + ".meta");
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                        if (asset is Material || asset is Texture || asset is Mesh) Remember(asset);
                }
            }
            root = new GameObject("Owned isolated Kitchen furniture diagnostic");
            client = root.AddComponent<WordboundLocalMatchClient>();
            yield return Completed(client.StartLocal(content, launch.BodyPrefabs(), launch.GameplayScene(),
                cameraProfile: source.CameraProfile, visualProfile: source.VisualProfile, uiTheme: launch.UiTheme,
                storyProfile: launch.StoryProfile), "Start normal local authority with diagnostic authored course", 60);
            controller = client.Gameplay;
            yield return Until(() => controller && controller.Frame != null && controller.CourseWorld &&
                controller.IsPresentationReady && !controller.IsSimulationPaused, "Native world ready", 40);
            world = controller.CourseWorld;
            Assert.AreEqual(content.Course.Hash, world.ManifestHash); Assert.AreEqual(content.CatalogueHash, Frame.Binding.CatalogueHash);
            Assert.AreNotEqual(Physics.defaultPhysicsScene, controller.PhysicsWorld.PhysicsScene);
            Assert.IsTrue(world.Surfaces.All(c => c && c.enabled && !c.isTrigger));
            Assert.IsTrue(world.Surfaces.Any(c => c.GetComponent<WordboundCourseNode>()?.Kind == WordboundCourseNodeKind.Wall));
            Assert.IsTrue(world.Surfaces.Any(c => c.GetComponent<WordboundCourseNode>()?.Kind == WordboundCourseNodeKind.Floor));
            Assert.AreEqual(4, Frame.Bodies.Count(b => b.Kind == WorldBodyKind.Item && b.Active));
            Assert.Less(Player.Position.DistanceSquared(Metres(content.Course.Spawns.First(s => s.Kind == "Start").Position)), .5f * .5f);
            recovery = Player.RecoveryGeneration;
            options = (WordboundGameplayOptions)typeof(WordboundGameplayController).GetField("options", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);
            previousMovement = options.ReadMovement; Assert.IsNotNull(previousMovement);
            movement = () => default; options.ReadMovement = ReadMovement;
            long priorTick = Frame.Progress.Tick;
            observer = frame => {
                if (frame.Progress.Tick < priorTick || frame.Bodies.Single(b => b.Kind == WorldBodyKind.Player && b.SourceId == Actor).RecoveryGeneration != recovery)
                    observationFailure = "Ticks regressed or actor recovered during the diagnostic.";
                priorTick = frame.Progress.Tick;
            };
            controller.FrameChanged += observer;
        }

        IEnumerator CraftHammer()
        {
            var supply = content.Course.Supplies.Single(s => s.SourceId == "KitchenDiagnosticHammer");
            yield return Until(() => Frame.Bodies.Count(b => b.Kind == WorldBodyKind.Tile && b.SourceId == supply.Id) == 6, "Mint actual tool supply", 10);
            foreach (string id in Frame.Bodies.Where(b => b.Kind == WorldBodyKind.Tile && b.SourceId == supply.Id).Select(b => b.Id).ToArray())
            {
                yield return Walk(() => Body(id).Position, .8f, "Reach actual HAMMER letter");
                yield return Command(WordboundAction.CollectLetter, id);
            }
            Assert.IsTrue(Inventory.Letters.Contains(content.Items.Get("HAMMER").Letters));
            yield return Command(WordboundAction.BeginCraft, value: "HAMMER");
            yield return Until(() => Frame.Economy.Items.Any(i => i.Word == "HAMMER" && i.Origin == ItemOrigin.Crafted && i.State == ItemState.Held && i.HolderId == 0), "Complete real HAMMER craft", 20);
        }
        IEnumerator PickUp(string id)
        {
            yield return Command(WordboundAction.PickUp, id, Item(id).Revision);
            Assert.AreEqual(ItemState.Carried, Item(id).State); Assert.AreEqual(Item(id).Id, Inventory.CarriedFurniture);
            Assert.IsFalse(Body(id).Active); Assert.IsTrue(Body(id).Kinematic);
            var native = controller.Bodies[id];
            Assert.IsTrue(native.Rigidbody.isKinematic); Assert.IsFalse(native.Rigidbody.detectCollisions);
            Assert.IsTrue(native.OwnedColliders.All(c => !c.enabled));
        }
        IEnumerator Approach(string id)
        {
            var centre = Metres(content.Course.Geometry.Single(m => m.Id == Room).Position);
            yield return Walk(() => new WorldVector(Player.Position.X, centre.Y, centre.Z), .1f, "Enter unobstructed room aisle");
            yield return Walk(() => new WorldVector(Body(id).Position.X, centre.Y, centre.Z), .1f, "Align with target from the aisle");
            yield return Walk(() => Stance(id), .04f, "Approach actual collider from its free south edge");
        }
        WorldVector Stance(string id)
        {
            var box = controller.Bodies[id].GetComponent<BoxCollider>();
            float radius = controller.Bodies[Player.Id].GetComponent<CapsuleCollider>().radius;
            float floor = content.Course.Geometry.Single(m => m.Id == Room).Position.Y / 1000f;
            return new WorldVector(box.bounds.center.x, floor, box.bounds.min.z - radius - .12f);
        }
        IEnumerator BreakWithHammer(string id, string placement, string word)
        {
            var hammer = Frame.Economy.Items.Single(i => i.Word == "HAMMER" && i.Origin == ItemOrigin.Crafted && i.HolderId == 0);
            yield return Command(WordboundAction.Equip, slot: Array.IndexOf(Inventory.Slots, hammer.Id));
            var stats = content.Items.Get("HAMMER").Melee;
            float toughness = content.Rules.FurnitureToughnessBase + content.Rules.FurnitureToughnessPerLetter * word.Length;
            Assert.AreEqual(toughness, Item(id).Durability, .001f);
            int expected = (int)Math.Ceiling(toughness / stats.BreakPower), observed = 0;
            while (Item(id).State != ItemState.Destroyed && observed < expected + 1)
            {
                yield return Until(() => Frame.Progress.Tick >= Frame.Actors.Single(a => a.PlayerId == Actor).NextAttackTick, "Respect hammer cooldown", 5);
                yield return Approach(id);
                yield return Face(Mathf.Atan2(Body(id).Position.X - Player.Position.X, Body(id).Position.Z - Player.Position.Z) * Mathf.Rad2Deg);
                RequireNoOverlap(id);
                Assert.LessOrEqual(Player.Position.DistanceSquared(Body(id).Position), stats.Reach * stats.Reach);
                var actor = controller.Bodies[Player.Id]; var target = controller.Bodies[id];
                var delta = target.Rigidbody.position - actor.Rigidbody.position; delta.y = 0;
                Assert.LessOrEqual(Vector3.Angle(actor.Rigidbody.rotation * Vector3.forward, delta), stats.ArcDegrees / 2);
                float before = Item(id).Durability;
                yield return Command(WordboundAction.Attack);
                yield return Until(() => Item(id).Durability < before, "Observe actual HAMMER damage", 5);
                Assert.AreEqual(Math.Max(0, before - stats.BreakPower), Item(id).Durability, .001f); observed++;
            }
            Assert.AreEqual(ItemState.Destroyed, Item(id).State); Assert.AreEqual(expected, observed);
            var tiles = Frame.Bodies.Where(b => b.Kind == WorldBodyKind.Tile && b.SourceId == placement).ToArray();
            Assert.AreEqual(word.Length, tiles.Length);
            CollectionAssert.AreEquivalent(word.ToCharArray(), tiles.Select(b => Frame.Economy.Tiles.Single(t => "tile:" + t.Key == b.Id).Value));
            Assert.AreEqual(placement, Body(id).SourceId);
            Assert.IsFalse(Body(id).Active, "Destroyed map furniture retains an inactive source tombstone.");
            yield return Until(() => controller.Bodies.TryGetValue(id, out var native) && !native.BodyActive &&
                !native.Rigidbody.detectCollisions && native.OwnedColliders.All(c => !c.enabled) &&
                native.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled),
                "Hide and disable the retained map-furniture body", 5);
            Debug.Log("[Kitchen pilot] HAMMER_BREAK: word=" + word + "; mapToughness=" + toughness + "; breakPower=" + stats.BreakPower + "; hits=" + observed + "; letters=" + tiles.Length);
        }

        void RequireShapeAndArtwork(string id, ItemDefinition definition)
        {
            var native = controller.Bodies[id]; var box = native.GetComponent<BoxCollider>();
            var geometry = content.Geometry.Assets[definition.Id];
            var expected = new Vector3(geometry.WidthMm, geometry.HeightMm, geometry.DepthMm) / 1000f;
            Assert.Less(Vector3.Distance(expected, box.size), .00001f);
            Assert.Less(Vector3.Distance(Vector3.up * expected.y / 2, box.center), .00001f);
            Assert.AreSame(native.Rigidbody, box.attachedRigidbody); Assert.IsTrue(box.enabled && !box.isTrigger);
            Assert.IsTrue(native.OwnedColliders.Contains(box)); Assert.Less(Vector3.Distance(Vector3.one, native.transform.lossyScale), .00001f);
            var parent = native.GetComponent<WordboundGameplayBody>().VisualRoot;
            var art = parent.Find("Wordbound artwork " + definition.Id); Assert.IsNotNull(art);
            Assert.IsTrue(art.gameObject.activeInHierarchy);
            Assert.AreEqual(0, art.GetComponentsInChildren<Collider>(true).Length);
            Assert.AreEqual(0, art.GetComponentsInChildren<Rigidbody>(true).Length);
            var original = ModelLibrary.Load().Find(definition.Model); Assert.IsNotNull(original);
            CollectionAssert.AreEqual(Meshes(original), Meshes(art.gameObject));
            Assert.IsTrue(art.GetComponentsInChildren<Renderer>(true).All(r => r.sharedMaterials.All(m => m)));
            var measurement = new GameObject("Owned original appliance bounds");
            measurement.SetActive(false);
            measurement.transform.SetParent(root.transform, false);
            Bounds measured;
            try
            {
                var instance = Object.Instantiate(original, measurement.transform, false);
                measured = ModelVisual.BoundsIn(measurement.transform, instance);
            }
            finally { Object.DestroyImmediate(measurement); }
            Assert.Less(Vector3.Distance(measured.size, new Vector3(definition.Size[0], definition.Size[1], definition.Size[2])), .005f,
                "Item dimensions must match the actual original imported model, not only a generated report.");
            Assert.Less(Vector3.Distance(measured.size, expected), .005f, "Canonical collider must retain original appliance scale within millimetre rounding.");
            var fitted = ModelVisual.BoundsIn(art, art.gameObject);
            Assert.Less(Vector3.Distance(fitted.size, expected), .003f);
            Assert.Less(Vector3.Distance(fitted.center, Vector3.up * expected.y / 2), .003f);
            Debug.Log("[Kitchen pilot] NATIVE_MODEL_BOUNDS: " + definition.Id + "; original=" + measured + "; fitted=" + fitted + "; collider=" + expected);
        }
        Transform HeldArt(string word)
        {
            var parent = controller.Bodies[Player.Id].GetComponent<WordboundGameplayBody>().VisualRoot;
            return parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Wordbound artwork " + word && t.gameObject.activeInHierarchy);
        }
        void RequireHeldGrip(string word, ItemDefinition definition)
        {
            var held = HeldArt(word); Assert.IsNotNull(held);
            var parent = controller.Bodies[Player.Id].GetComponent<WordboundGameplayBody>().VisualRoot;
            var hand = ModelVisual.FindNamed(parent.gameObject, "grip_R"); Assert.IsNotNull(hand);
            var authored = ModelVisual.FindNamed(held.gameObject, "Grip_R");
            var fit = held.Find("Shape fit"); Assert.IsNotNull(fit);
            var grip = authored ? authored.position : held.TransformPoint(fit.localPosition + Vector3.Scale(fit.localScale,
                new Vector3(definition.Grip[0], definition.Grip[1], definition.Grip[2])));
            Assert.Less(Vector3.Distance(hand.position, grip), .02f);
            Assert.Less(Vector3.Distance(Vector3.one, held.localScale), .00001f, "Map furniture must not use the crafted miniature scale.");
            Assert.AreEqual(0, held.GetComponentsInChildren<Collider>(true).Length);
            CollectionAssert.AreEqual(Meshes(ModelLibrary.Load().Find(definition.Model)), Meshes(held.gameObject));
        }
        static Mesh[] Meshes(GameObject value) => value.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh)
            .Concat(value.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.sharedMesh)).ToArray();
        void RequireNoOverlap(string id)
        {
            var actor = controller.Bodies[Player.Id].GetComponent<CapsuleCollider>();
            var target = controller.Bodies[id].GetComponent<BoxCollider>();
            Assert.IsFalse(Physics.ComputePenetration(actor, actor.transform.position, actor.transform.rotation,
                target, target.transform.position, target.transform.rotation, out _, out _), "Normal release/stance must not overlap the actor.");
            Assert.IsTrue(Supported(controller.Bodies[Player.Id].Rigidbody.position));
        }
        void Snapshot(string id, ItemState state, string phase)
        {
            var frame = Frame; string json = MatchSnapshotCodec.Export(frame, out string hash);
            var imported = MatchSnapshotCodec.Import(json, frame.Binding, content.Mode, content.Course, content.Items, content.Rules);
            Assert.IsTrue(imported.Success, phase + ": " + imported.Error); Assert.AreEqual(hash, imported.Hash);
            var original = frame.Economy.Items.Single(i => "item:" + i.Id == id);
            var copy = imported.Snapshot.Economy.Items.Single(i => i.Id == original.Id);
            Assert.AreEqual(state, copy.State); Assert.AreEqual(original.Word, copy.Word); Assert.AreEqual(original.Origin, copy.Origin);
            Assert.AreEqual(original.HolderId, copy.HolderId); Assert.AreEqual(original.Revision, copy.Revision);
            Assert.AreEqual(json, MatchSnapshotCodec.Export(imported.Snapshot));
            Debug.Log("[Kitchen pilot] SNAPSHOT: word=" + copy.Word + "; phase=" + phase + "; state=" + state + "; hash=" + hash);
        }
        IEnumerator Settle(string id)
        {
            yield return Until(() => Body(id).Sleeping && Body(id).Velocity.DistanceSquared(default) < .01f,
                "Settle native furniture on its actual floor", 30);
            var box = controller.Bodies[id].GetComponent<BoxCollider>();
            float floor = content.Course.Geometry.Single(m => m.Id == Room).Position.Y / 1000f;
            Assert.Less(Mathf.Abs(box.bounds.min.y - floor), .06f, "The settled native collider must contact the canonical floor.");
        }
        IEnumerator Walk(Func<WorldVector> target, float radius, string label)
        {
            movement = () => {
                var at = controller.Bodies[Player.Id].Rigidbody.position; var to = target();
                var delta = new Vector2(to.X - at.x, to.Z - at.z);
                if (delta.magnitude <= radius) return default;
                var input = delta.normalized * Mathf.Clamp((delta.magnitude - radius * .5f) * .1f, .06f, .8f);
                return new WordboundMovementInput { MoveX = input.x, MoveZ = input.y };
            };
            try { yield return Until(() => Horizontal(Player.Position, target()) <= radius && Mathf.Abs(Player.Position.Y - target().Y) < .4f, label, 60); }
            finally { movement = () => default; }
            yield return Until(() => Player.Velocity.X * Player.Velocity.X + Player.Velocity.Z * Player.Velocity.Z < .01f, "Settle through motor", 10);
            Assert.IsTrue(Supported(controller.Bodies[Player.Id].Rigidbody.position), label + " needs canonical floor support.");
        }
        bool Supported(Vector3 foot)
        {
            var hits = new RaycastHit[32];
            int count = controller.PhysicsWorld.PhysicsScene.Raycast(foot + Vector3.up * .1f, Vector3.down, hits, .25f, ~0, QueryTriggerInteraction.Ignore);
            return count < hits.Length && hits.Take(count).Any(h => world.Surfaces.Contains(h.collider) && h.normal.y > .8f);
        }
        IEnumerator Face(float yaw)
        {
            movement = () => new WordboundMovementInput { HasFacing = true, FacingYaw = yaw };
            yield return Until(() => Quaternion.Angle(controller.Bodies[Player.Id].Rigidbody.rotation, Quaternion.Euler(0, yaw, 0)) < .5f, "Face through normal motor input", 10);
        }
        WordboundMovementInput ReadMovement() { movementReads++; return movement?.Invoke() ?? default; }
        static WorldVector Metres(CoursePoint value) => new WorldVector(value.X / 1000f, value.Y / 1000f, value.Z / 1000f);
        static float Horizontal(WorldVector a, WorldVector b) => new Vector2(a.X - b.X, a.Z - b.Z).magnitude;
        IEnumerator Command(WordboundAction action, string target = "", int revision = 0, int slot = -1, string value = "", string expected = "Ok")
        {
            var command = controller.Submit(action, target, revision, slot, value);
            yield return Until(() => Frame.Commands.Frontiers.TryGetValue(Actor, out long frontier) && frontier >= command.Sequence, "Commit " + action, 10);
            var receipt = Frame.Commands.Receipts.Single(r => r.ActorId == Actor && r.Sequence == command.Sequence);
            Assert.AreEqual(command.PayloadHash, receipt.PayloadHash); Assert.AreEqual(expected, receipt.Effect.Code, action.ToString());
        }
        IEnumerator Until(Func<bool> ready, string label, double seconds)
        {
            double until = Math.Min(deadline, Time.realtimeSinceStartupAsDouble + seconds);
            while (!ready() && Time.realtimeSinceStartupAsDouble < until) { Healthy(); yield return null; }
            Healthy(); Assert.IsTrue(ready(), label + " timed out.");
        }
        void Healthy()
        {
            if (controller) { Assert.AreEqual("", controller.Fault); if (world) Assert.IsFalse(controller.IsSimulationPaused); }
            Assert.IsEmpty(observationFailure);
            Assert.Less(Time.realtimeSinceStartupAsDouble, deadline, "Bounded isolated diagnostic deadline.");
        }
        static IEnumerator Completed(Task task, string label, double seconds)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < until) yield return null;
            Assert.IsTrue(task.IsCompleted, label + " timed out."); Assert.IsFalse(task.IsCanceled);
            Assert.IsFalse(task.IsFaulted, label + ": " + task.Exception?.GetBaseException());
        }
        void Remember(Object asset)
        {
            if (!asset || memory.ContainsKey(asset)) return;
            Assert.IsFalse(EditorUtility.IsDirty(asset), "Save pending source edits before native verification: " + asset.name);
            memory[asset] = EditorJsonUtility.ToJson(asset);
            string path = AssetDatabase.GetAssetPath(asset); RememberFile(path); RememberFile(path + ".meta");
        }
        void RememberFile(string path) { if (File.Exists(path) && !files.ContainsKey(path)) files[path] = File.ReadAllBytes(path); }
        void RequirePreserved()
        {
            foreach (var file in files) CollectionAssert.AreEqual(file.Value, File.ReadAllBytes(file.Key), file.Key);
            foreach (var asset in memory) Assert.AreEqual(asset.Value, EditorJsonUtility.ToJson(asset.Key), asset.Key.name);
            if (content != null) Assert.AreEqual(diagnosticJson, CourseJson.ExportDocument(content.Course.CopyDocument()));
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var failures = new List<string>(); movement = () => default;
            if (options != null)
            {
                if (options.ReadMovement == (Func<WordboundMovementInput>)ReadMovement) options.ReadMovement = previousMovement;
                else failures.Add("movement delegate ownership changed");
            }
            if (controller && observer != null) controller.FrameChanged -= observer;
            Task stop = null;
            try { if (client) stop = client.ReturnToWorkshop(); } catch (Exception e) { failures.Add("shutdown: " + e.Message); }
            if (stop != null)
            {
                double until = Time.realtimeSinceStartupAsDouble + 30;
                while (!stop.IsCompleted && Time.realtimeSinceStartupAsDouble < until) yield return null;
                if (!stop.IsCompleted || stop.IsCanceled) failures.Add("shutdown incomplete");
                if (stop.IsFaulted) failures.Add("shutdown: " + stop.Exception.GetBaseException().Message);
                if (!stop.IsCompleted) _ = stop.ContinueWith(done => { _ = done.Exception; }, System.Threading.CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            try { RequirePreserved(); } catch (Exception e) { failures.Add("source preservation: " + e.Message); }
            if (root)
            {
                Assert.IsFalse(EditorUtility.IsPersistent(root));
                if (Application.isPlaying) Object.Destroy(root); else Object.DestroyImmediate(root);
            }
            if (Application.isPlaying) yield return null;
            if (root || world) failures.Add("owned native world retained");
            if (Application.isPlaying && enteredPlay) yield return new ExitPlayMode();
            root = null; client = null; controller = null; world = null; options = null; observer = null; content = null; source = null; enteredPlay = false;
            Debug.Log("[Kitchen pilot] NATIVE_FURNITURE_CLEANUP: failures=" + failures.Count);
            Assert.IsEmpty(failures, string.Join("; ", failures));
        }
    }
}
