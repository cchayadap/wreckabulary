using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class WordboundNativeClockLineTests
    {
        const string PrefabPath = "Assets/_Project/Prefabs/Wordbound/Network/WordboundPlatformBody.prefab";
        readonly List<Scene> scenes = new List<Scene>();
        readonly RaycastHit[] hits = new RaycastHit[32];

        [UnityTest, Category("NativePhysics")]
        public IEnumerator ActualPlatformColliderTargetClearsFloorAndRetainsWallOcclusion()
        {
            yield return new EnterPlayMode();
            Assert.IsTrue(Application.isPlaying);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab);
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Config/items.json");
            Assert.IsNotNull(json);
            var content = WordboundFlagshipContent.Create(ItemCatalogue.FromJson(json.text));
            var resolved = content.Resolve("0123456789abcdef", MatchStateValidation.Requirements(ModeDefinition.For("TimeRun")));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            var site = resolved.Manifest.PhysicalSites.First(s => s.Lap == 1 &&
                resolved.Manifest.Puzzles.Single(p => p.Id == s.PuzzleId).PresetId == "TimedMechanism");
            var authored = site.Fixtures.Single();
            var dimensions = new Vector3(authored.SizeMm.X, authored.SizeMm.Y, authored.SizeMm.Z) / 1000f;
            Assert.AreEqual(new Vector3(.6f, 1.2f, .2f), dimensions);
            var scene = SceneManager.CreateScene("WB_ClockLine_" + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            scenes.Add(scene);
            var physics = scene.GetPhysicsScene();
            Assert.IsTrue(physics.IsValid()); Assert.AreNotEqual(Physics.defaultPhysicsScene, physics);
            var root = new GameObject("Owned CLOCK line calibration");
            SceneManager.MoveGameObjectToScene(root, scene);
            float floorY = site.Position.Y / 1000f;
            var source = new Vector3(site.Position.X / 1000f, floorY + .5f, site.Position.Z / 1000f);
            var floor = Box(root.transform, "Owned floor", new Vector3(source.x, floorY - .12f, source.z), new Vector3(10, .24f, 10));
            var arm = new GameObject("Owned actual platform physics");
            arm.transform.SetParent(root.transform, false);
            var body = arm.AddComponent<Rigidbody>();
            var collider = arm.AddComponent<BoxCollider>();
            EditorUtility.CopySerialized(prefab.GetComponent<Rigidbody>(), body);
            EditorUtility.CopySerialized(prefab.GetComponent<BoxCollider>(), collider);
            Assert.AreSame(arm, body.gameObject); Assert.AreSame(arm, collider.gameObject);
            Assert.AreSame(body, collider.attachedRigidbody);
            Assert.IsTrue(body.isKinematic); Assert.IsFalse(body.useGravity);
            Assert.AreEqual(RigidbodyInterpolation.None, body.interpolation);
            Assert.AreEqual(100, body.mass);
            Assert.IsEmpty(arm.GetComponents<MonoBehaviour>());
            collider.size = dimensions; collider.center = Vector3.up * dimensions.y / 2;
            var visual = new GameObject("Independent presentation offset");
            visual.transform.SetParent(arm.transform, false);
            visual.transform.localPosition = new Vector3(50, 50, 50);
            var start = new Vector3(authored.From.X, authored.From.Y, authored.From.Z) / 1000f;
            var end = new Vector3(authored.To.X, authored.To.Y, authored.To.Z) / 1000f;
            int oldFloorHits = 0, checks = 0;
            foreach (var scale in new[] { Vector3.one, new Vector3(1.4f, 1.3f, .75f) })
                foreach (float yaw in new[] { 0f, 37f })
                {
                    arm.transform.localScale = scale;
                    body.position = start; body.rotation = Quaternion.Euler(0, yaw, 0);
                    Physics.SyncTransforms();
                    physics.Simulate(1f / 60);
                    Assert.AreEqual(floorY, floor.bounds.max.y, .0001f);
                    Vector3 previous = WordboundClockLineTarget.FromCollider(body);
                    foreach (float t in new[] { .2f, .59f, .9f })
                    {
                        body.MovePosition(Vector3.Lerp(start, end, t));
                        body.MoveRotation(Quaternion.Euler(0, yaw + t * 5, 0));
                        physics.Simulate(1f / 60);
                        var target = WordboundClockLineTarget.FromCollider(body);
                        var expected = body.position + body.rotation * Vector3.Scale(collider.center, scale);
                        Assert.Less(Vector3.Distance(expected, target), .0001f, "Use the current native collider pose after kinematic movement.");
                        Assert.Greater(Vector3.Distance(previous, target), .01f);
                        previous = target;
                        Assert.Greater(target.y, floor.bounds.max.y + .5f);
                        Assert.Less(Vector3.Distance(target, collider.ClosestPoint(target)), .0001f);
                        var clear = Segment(physics, source, target, collider);
                        Assert.IsFalse(clear.Contains(floor), "The floor remains solid without intercepting the collider-center segment.");
                        Assert.IsEmpty(clear);
                        body.centerOfMass = Vector3.zero;
                        var oldTarget = body.worldCenterOfMass;
                        Assert.AreEqual(floorY, oldTarget.y, .0001f, "Reproduce the mass-center height recorded in the failing native route.");
                        if (Segment(physics, source, oldTarget, collider).Contains(floor)) oldFloorHits++;
                        Assert.Less(Vector3.Distance(target, WordboundClockLineTarget.FromCollider(body)), .0001f,
                            "Mass-center changes must not move the geometric target.");
                        var wall = Box(root.transform, "Owned solid wall", (source + target) / 2, new Vector3(.35f, 1.5f, .1f));
                        wall.transform.rotation = Quaternion.LookRotation(target - source);
                        Physics.SyncTransforms();
                        Assert.Contains(wall, Segment(physics, source, WordboundClockLineTarget.FromCollider(body), collider));
                        Object.DestroyImmediate(wall.gameObject);
                        Physics.SyncTransforms();
                        Assert.IsEmpty(Segment(physics, source, WordboundClockLineTarget.FromCollider(body), collider));
                        checks++;
                    }
                }
            Assert.Greater(oldFloorHits, 0, "The recorded floor-height endpoint must reproduce a native floor interception.");
            Assert.AreEqual(12, checks);
            TestContext.WriteLine("WORDBOUND_CLOCK_LINE: colliderCenterChecks=" + checks + "; oldFloorEndpointHits=" + oldFloorHits +
                "; actualPrefabPhysicsCopied=true; nativeKinematicMovement=true; wallBlocked=true; networkSession=false");
            collider.enabled = false;
            Assert.Throws<InvalidOperationException>(() => WordboundClockLineTarget.FromCollider(body));
            collider.enabled = true; collider.isTrigger = true;
            Assert.Throws<InvalidOperationException>(() => WordboundClockLineTarget.FromCollider(body));
            collider.isTrigger = false; arm.SetActive(false);
            Assert.Throws<InvalidOperationException>(() => WordboundClockLineTarget.FromCollider(body));
        }

        Collider[] Segment(PhysicsScene scene, Vector3 from, Vector3 to, Collider ignored)
        {
            var delta = to - from;
            int count = scene.Raycast(from, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            Assert.Less(count, hits.Length, "A full query buffer is not a clear line.");
            return hits.Take(count).Where(hit => hit.collider != ignored).Select(hit => hit.collider).ToArray();
        }

        static BoxCollider Box(Transform root, string name, Vector3 position, Vector3 size)
        {
            var node = new GameObject(name);
            node.transform.SetParent(root, false); node.transform.position = position;
            var collider = node.AddComponent<BoxCollider>(); collider.size = size;
            return collider;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var scene in scenes)
                if (scene.IsValid() && scene.isLoaded)
                {
                    foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                    var unload = SceneManager.UnloadSceneAsync(scene);
                    while (unload != null && !unload.isDone) yield return null;
                }
            scenes.Clear();
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
