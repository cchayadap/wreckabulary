using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class WordboundNativeWindPhysicsTests
    {
        const int Steps = 300;
        const string PrefabPath = "Assets/_Project/Prefabs/Wordbound/Network/WordboundItemBody.prefab";
        readonly List<Scene> scenes = new List<Scene>();
        readonly List<PhysicsMaterial> materials = new List<PhysicsMaterial>();

        sealed class Result
        {
            public string Name;
            public int WindTicks, ArrivalTick = -1;
            public float MaximumTravel, RequiredTravel;
            public Vector3 Start, End, Velocity, ArrivalPosition;
            public int LastWindTick, FieldExitTick = -1, NonFloorProximityTicks;
            public bool ArrivalReceivedWind, WindAfterExit;
            public float LastSecondMaximumSpeed, LastSecondMaximumDrift;
            public Vector3 ExitPosition, ExitVelocity;
        }

        [UnityTest, Category("NativePhysics")]
        public IEnumerator ActualStoolCrossesAuthoredWindArrivalWithDefaultContacts()
        {
            yield return new EnterPlayMode();
            Assert.IsTrue(Application.isPlaying, "The isolated native solver requires Play Mode.");
            Load(out var content, out var course, out var site, out var placement, out var items, out var prefab);
            var results = new[]
            {
                Run("no-wind", false, false, content, course, site, placement, items, prefab),
                Run("default-contact-wind", true, false, content, course, site, placement, items, prefab),
                Run("zero-friction-wind", true, true, content, course, site, placement, items, prefab)
            };
            WriteResults(results);
            Assert.Less(results[0].MaximumTravel, .05f, "The no-wind floor control must not travel toward the target.");
            Assert.AreEqual(-1, results[0].ArrivalTick);
            Assert.Greater(results[1].WindTicks, 0, "Default contacts must actually receive wind.");
            Assert.Greater(results[2].WindTicks, 0, "Zero-friction control must actually receive wind.");
            Assert.GreaterOrEqual(results[2].ArrivalTick, 1, "Zero friction must reach the actual arrival volume within 300 solver steps.");
            Assert.GreaterOrEqual(results[2].MaximumTravel + .00001f, results[2].RequiredTravel);
            Assert.GreaterOrEqual(results[1].ArrivalTick, 1, "Default contacts must let the actual FAN move the STOOL into its authored arrival volume.");
            Assert.GreaterOrEqual(results[1].MaximumTravel + .00001f, results[1].RequiredTravel);
        }

        [UnityTest, Category("NativePhysics")]
        public IEnumerator CandidateItemContactReachesWindArrivalAndStopsWithoutObstacles()
        {
            yield return new EnterPlayMode();
            Assert.IsTrue(Application.isPlaying, "The isolated native solver requires Play Mode.");
            Load(out var content, out var course, out var site, out var placement, out var items, out var prefab);
            var results = new[]
            {
                Run("candidate-no-wind", false, false, content, course, site, placement, items, prefab, true),
                Run("candidate-wind", true, false, content, course, site, placement, items, prefab, true),
                Run("candidate-inertia-stop", false, false, content, course, site, placement, items, prefab, true, 3)
            };
            WriteResults(results);
            Assert.Less(results[0].MaximumTravel, .05f);
            Assert.Less(Vector3.Distance(results[0].Start, results[0].End), .05f, "The stationary control must not drift.");
            Assert.AreEqual(-1, results[0].ArrivalTick);
            Assert.AreEqual(0, results[0].WindTicks);
            Assert.GreaterOrEqual(results[1].ArrivalTick, 1, "The candidate must enter the actual 3D arrival volume.");
            Assert.IsTrue(results[1].ArrivalReceivedWind, "Arrival must occur during an actual wind push, as the mechanism requires.");
            Assert.GreaterOrEqual(results[1].MaximumTravel + .00001f, results[1].RequiredTravel);
            Assert.Greater(results[1].FieldExitTick, results[1].ArrivalTick, "Continue beyond arrival until the receiver leaves the field.");
            Assert.IsFalse(results[1].WindAfterExit, "Stopping evidence must not include a later wind push.");
            Assert.Greater(results[2].MaximumTravel, .1f, "The inertia control must move before contact friction stops it.");
            Assert.AreEqual(0, results[2].WindTicks);
            foreach (var result in results)
            {
                Assert.AreEqual(0, result.NonFloorProximityTicks, result.Name + " must not stop against a wall or another obstacle.");
                Assert.Less(result.Velocity.magnitude, .05f, result.Name + " must finish at rest without a scripted velocity reset.");
                Assert.Less(result.LastSecondMaximumSpeed, .05f, result.Name + " must remain at rest throughout the last second.");
                Assert.Less(result.LastSecondMaximumDrift, .02f, result.Name + " must not keep coasting during the last second.");
            }
        }

        static void Load(out WordboundFlagshipContent content, out ResolvedCourse course,
            out ResolvedCoursePhysicalSite site, out ResolvedCoursePlacement placement, out ItemCatalogue items, out GameObject prefab)
        {
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Config/items.json");
            Assert.IsNotNull(source, "Use the installed gameplay catalogue.");
            items = ItemCatalogue.FromJson(source.text);
            content = WordboundFlagshipContent.Create(items);
            var resolved = content.Resolve("0123456789abcdef", MatchStateValidation.Requirements(ModeDefinition.For("TimeRun")));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            course = resolved.Manifest;
            var manifest = course;
            site = manifest.PhysicalSites.First(s => s.Lap == 1 &&
                manifest.Puzzles.Single(p => p.Id == s.PuzzleId).PresetId == "RedirectWind");
            var selectedSite = site;
            placement = manifest.Placements.Single(p => p.ZoneId == selectedSite.WindReceiverZoneId && p.TeamIndex == 0);
            Assert.AreEqual("STOOL", site.WindReceiverRecipeId);
            Assert.AreEqual(0, site.Yaw, "This calibration preserves the observed first +Z wind site.");
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "Native item prefab must already be generated.");
        }

        Result Run(string name, bool wind, bool frictionless, WordboundFlagshipContent content, ResolvedCourse course,
            ResolvedCoursePhysicalSite site, ResolvedCoursePlacement placement, ItemCatalogue items, GameObject prefab,
            bool candidate = false, float initialSpeed = 0)
        {
            var scene = SceneManager.CreateScene("WB_Wind_" + name + "_" + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            scenes.Add(scene);
            var physics = scene.GetPhysicsScene();
            Assert.IsTrue(physics.IsValid());
            Assert.AreNotEqual(Physics.defaultPhysicsScene, physics);
            var root = new GameObject("Owned wind calibration");
            SceneManager.MoveGameObjectToScene(root, scene);
            var world = WordboundCourseWorld.Build(course, content.Geometry, root.transform);
            var start = Metres(placement.Position) + Vector3.back * .15f + Vector3.up * .02f;
            var arrival = new Bounds();
            arrival.SetMinMax(Metres(site.ArrivalBounds.Min), Metres(site.ArrivalBounds.Max));
            var forward = Quaternion.Euler(0, site.Yaw, 0) * Vector3.forward;
            float required = Vector3.Dot(arrival.ClosestPoint(start) - start, forward);
            Assert.AreEqual(.800f, required, .0001f, "Use the authored initial start, not the stalled final pose.");
            Assert.IsFalse(arrival.Contains(start));
            Physics.SyncTransforms();
            Assert.IsTrue(physics.Raycast(start + Vector3.up * 2, Vector3.down, out var hit, 4, ~0, QueryTriggerInteraction.Ignore));
            var floor = hit.collider;
            var floorNode = floor.GetComponent<WordboundCourseNode>();
            Assert.IsNotNull(floorNode);
            Assert.AreEqual(WordboundCourseNodeKind.Floor, floorNode.Kind);
            Assert.IsTrue(world.Surfaces.Contains(floor));
            Assert.AreEqual(site.Position.Y / 1000f, floor.bounds.max.y, .0001f);
            Assert.IsNull(floor.sharedMaterial, "Baseline uses the current generated default contact material.");

            var receiver = new GameObject("Owned STOOL physics");
            receiver.transform.SetParent(root.transform, false);
            var body = receiver.AddComponent<Rigidbody>();
            var collider = receiver.AddComponent<BoxCollider>();
            var prefabBody = prefab.GetComponent<Rigidbody>();
            var prefabCollider = prefab.GetComponent<BoxCollider>();
            Assert.IsNotNull(prefabBody); Assert.IsNotNull(prefabCollider);
            EditorUtility.CopySerialized(prefabBody, body);
            EditorUtility.CopySerialized(prefabCollider, collider);
            Assert.AreSame(receiver, body.gameObject); Assert.AreSame(receiver, collider.gameObject);
            Assert.AreSame(body, collider.attachedRigidbody);
            Assert.AreEqual(scene, receiver.scene);
            Assert.AreEqual(1, receiver.GetComponents<Rigidbody>().Length);
            Assert.AreEqual(1, receiver.GetComponents<Collider>().Length);
            Assert.IsEmpty(receiver.GetComponents<MonoBehaviour>(), "The calibration must not create a Fusion lifecycle.");
            var stool = items.Get(site.WindReceiverRecipeId);
            collider.size = new Vector3(stool.Size[0], stool.Size[1], stool.Size[2]);
            collider.center = Vector3.up * collider.size.y / 2;
            Assert.AreEqual(new Vector3(.52f, .466f, .52f), collider.size);
            Assert.AreEqual(new Vector3(0, .233f, 0), collider.center);
            Assert.AreEqual(prefabBody.mass, body.mass);
            Assert.AreEqual(prefabBody.linearDamping, body.linearDamping);
            Assert.AreEqual(prefabBody.angularDamping, body.angularDamping);
            Assert.AreEqual(prefabBody.constraints, body.constraints);
            Assert.AreEqual(prefabBody.useGravity, body.useGravity); Assert.IsTrue(body.useGravity);
            Assert.AreSame(prefabCollider.sharedMaterial, collider.sharedMaterial); Assert.IsNull(collider.sharedMaterial);
            Assert.AreEqual(prefabCollider.contactOffset, collider.contactOffset);
            Assert.IsFalse(collider.isTrigger); Assert.IsTrue(collider.enabled);
            WordboundBodyMotion.Apply(body, false, false);
            body.position = start; body.rotation = Quaternion.Euler(0, placement.Yaw, 0);
            body.linearVelocity = forward * initialSpeed; body.angularVelocity = Vector3.zero;
            if (frictionless)
            {
                var material = new PhysicsMaterial("Owned zero friction control")
                { staticFriction = 0, dynamicFriction = 0, bounciness = 0,
                    frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum };
                materials.Add(material);
                collider.sharedMaterial = material;
                foreach (var surface in world.Surfaces) surface.sharedMaterial = material;
            }
            if (candidate)
            {
                Assert.IsFalse(frictionless);
                var material = new PhysicsMaterial("Owned Wordbound item contact candidate")
                { staticFriction = .30f, dynamicFriction = .25f, bounciness = 0,
                    frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum };
                materials.Add(material);
                collider.sharedMaterial = material;
                Assert.AreSame(material, collider.sharedMaterial);
                Assert.IsTrue(world.Surfaces.All(surface => surface.sharedMaterial == null), "The candidate changes only the receiver contact material.");
            }
            Physics.SyncTransforms();
            Assert.AreEqual(start, body.position);
            Assert.AreEqual(start.y, collider.bounds.min.y, .0001f);
            var rotation = Quaternion.Euler(0, site.Yaw, 0);
            var fan = new WorldBodyState("item:1", WorldBodyKind.Item, Value(Metres(site.Position)),
                new WorldVector(rotation.x, rotation.y, rotation.z), rotation.w, kinematic: true);
            var stats = items.Get("FAN").Deploy;
            Assert.IsTrue(WordboundFieldPolicy.Contains(stats, fan, Pose(body)));
            Console.WriteLine(FormattableString.Invariant($"WB_WIND_SETUP: scenario={name}; unity={Application.unityVersion}; manifest={course.Hash}; site={site.Hash}; fan={V(Metres(site.Position))}; arrivalMin={V(arrival.min)}; arrivalMax={V(arrival.max)}; floorTop={floor.bounds.max.y:R}; floorThickness={floor.bounds.size.y:R}; mass={body.mass:R}; gravity={V(Physics.gravity)}; linearDamping={body.linearDamping:R}; angularDamping={body.angularDamping:R}; constraints={body.constraints}; solverIterations={body.solverIterations}; solverVelocityIterations={body.solverVelocityIterations}; contactOffset={collider.contactOffset:R}; frictionless={frictionless}; strength={stats.Strength:R}; radius={stats.Radius:R}; arc={stats.ArcDegrees:R}; simulationMode={Physics.simulationMode}; fixedDeltaTime={Time.fixedDeltaTime:R}"));
            Console.WriteLine(FormattableString.Invariant($"WB_WIND_CONTACT: scenario={name}; receiverOnlyCandidate={candidate}; staticFriction={collider.sharedMaterial?.staticFriction.ToString("R", CultureInfo.InvariantCulture) ?? "default"}; dynamicFriction={collider.sharedMaterial?.dynamicFriction.ToString("R", CultureInfo.InvariantCulture) ?? "default"}; combine={collider.sharedMaterial?.frictionCombine.ToString() ?? "default"}; initialSpeed={initialSpeed:R}"));
            var result = new Result { Name = name, Start = start, RequiredTravel = required };
            var nearby = new Collider[32];
            var lastSecondStart = Vector3.zero;
            for (int tick = 1; tick <= Steps; tick++)
            {
                WordboundBodyMotion.Apply(body, false, false);
                var before = body.linearVelocity;
                bool pushed = wind && WordboundFieldPolicy.Contains(stats, fan, Pose(body));
                if (pushed)
                {
                    var velocity = WordboundFieldPolicy.WindVelocity(stats, fan, Value(before));
                    body.linearVelocity = new Vector3(velocity.X, velocity.Y, velocity.Z);
                    result.WindTicks++;
                    result.LastWindTick = tick;
                    if (result.FieldExitTick >= 0) result.WindAfterExit = true;
                }
                var impulse = body.linearVelocity;
                physics.Simulate(1f / ModeDefinition.TickRate);
                float travel = Vector3.Dot(body.position - start, forward);
                result.MaximumTravel = Mathf.Max(result.MaximumTravel, travel);
                if (result.ArrivalTick < 0 && arrival.Contains(body.position))
                { result.ArrivalTick = tick; result.ArrivalPosition = body.position; result.ArrivalReceivedWind = pushed; }
                if (wind && result.FieldExitTick < 0 && !WordboundFieldPolicy.Contains(stats, fan, Pose(body)))
                { result.FieldExitTick = tick; result.ExitPosition = body.position; result.ExitVelocity = body.linearVelocity; }
                if (tick == Steps - ModeDefinition.TickRate) lastSecondStart = body.position;
                if (tick > Steps - ModeDefinition.TickRate)
                {
                    result.LastSecondMaximumSpeed = Mathf.Max(result.LastSecondMaximumSpeed, body.linearVelocity.magnitude);
                    result.LastSecondMaximumDrift = Mathf.Max(result.LastSecondMaximumDrift, Vector3.Distance(lastSecondStart, body.position));
                }
                if (candidate)
                {
                    int count = physics.OverlapBox(body.position + body.rotation * collider.center,
                        collider.size / 2 + Vector3.one * collider.contactOffset, nearby, body.rotation, ~0, QueryTriggerInteraction.Ignore);
                    Assert.Less(count, nearby.Length, "Contact-proximity buffer must not truncate evidence.");
                    for (int index = 0; index < count; index++)
                        if (nearby[index] != collider && nearby[index].GetComponent<WordboundCourseNode>()?.Kind != WordboundCourseNodeKind.Floor)
                        { result.NonFloorProximityTicks++; break; }
                }
                if (tick == 1 || tick % 60 == 0)
                    Console.WriteLine(FormattableString.Invariant($"WB_WIND_SAMPLE: scenario={name}; tick={tick}; pose={V(body.position)}; beforeWind={V(before)}; beforeSolver={V(impulse)}; afterSolver={V(body.linearVelocity)}; signedTravel={travel:R}; sleeping={body.IsSleeping()}"));
            }
            result.End = body.position; result.Velocity = body.linearVelocity;
            return result;
        }

        static void WriteResults(IEnumerable<Result> results)
        {
            foreach (var result in results)
                Console.WriteLine(FormattableString.Invariant($"WB_WIND_RESULT: scenario={result.Name}; steps={Steps}; hz={ModeDefinition.TickRate}; start={V(result.Start)}; end={V(result.End)}; velocity={V(result.Velocity)}; maximumSignedTravel={result.MaximumTravel:R}; requiredTravel={result.RequiredTravel:R}; windTicks={result.WindTicks}; firstArrivalTick={result.ArrivalTick}; arrivalPosition={V(result.ArrivalPosition)}; arrivalReceivedWind={result.ArrivalReceivedWind}; lastWindTick={result.LastWindTick}; fieldExitTick={result.FieldExitTick}; exitPosition={V(result.ExitPosition)}; exitVelocity={V(result.ExitVelocity)}; windAfterExit={result.WindAfterExit}; lastSecondMaxSpeed={result.LastSecondMaximumSpeed:R}; lastSecondMaxDrift={result.LastSecondMaximumDrift:R}; nonFloorProximityTicks={result.NonFloorProximityTicks}; scope=isolated-native-solver-not-Fusion"));
        }

        [UnityTearDown]
        public IEnumerator CleanupOwnedScenes()
        {
            foreach (var scene in scenes)
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            scenes.Clear();
            foreach (var material in materials) if (material != null) Object.DestroyImmediate(material);
            materials.Clear();
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        static Vector3 Metres(CoursePoint point) => new Vector3(point.X, point.Y, point.Z) / 1000f;
        static WorldVector Value(Vector3 value) => new WorldVector(value.x, value.y, value.z);
        static WorldBodyState Pose(Rigidbody body) => new WorldBodyState("item:2", WorldBodyKind.Item, Value(body.position),
            new WorldVector(body.rotation.x, body.rotation.y, body.rotation.z), body.rotation.w, velocity: Value(body.linearVelocity));
        static string V(Vector3 value) => string.Format(CultureInfo.InvariantCulture, "({0:R},{1:R},{2:R})", value.x, value.y, value.z);
    }
}
