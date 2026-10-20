using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;

namespace Wreckabulary.Tests
{
    public sealed class WordboundCourseWorldTests
    {
        GameObject parent;
        Material material;
        WordboundCourseWorld world;
        WordboundCourseWorldOptions options;

        [SetUp]
        public void SetUp()
        {
            parent = new GameObject("CourseFixture");
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            options = new WordboundCourseWorldOptions
            { FloorMaterial = material, WallMaterial = material, ConnectorMaterial = material };
        }

        [TearDown]
        public void TearDown()
        {
            if (parent != null) UnityEngine.Object.DestroyImmediate(parent);
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
        }

        [TestCase("TimeRun"), TestCase("FreeForAll"), TestCase("Freestyle")]
        public void ProofWorldUsesManifestBoundsAndAnchorsWithoutSpawningGameplayBodies(string mode)
        {
            var catalogue = CourseTemplates.ProofCatalogue();
            var document = mode == "TimeRun" ? CourseTemplates.RaceCircuit() :
                mode == "FreeForAll" ? CourseTemplates.CombatArena() : CourseTemplates.FreestyleWorld();
            var resolved = Resolve(document, catalogue, mode);
            int callbackCount = 0;
            options.ConfigureCollision = (collider, node) =>
            {
                callbackCount++;
                Assert.IsTrue(node.Protected);
                Assert.AreEqual(-1, node.OwnerTeam);
                Assert.AreEqual(resolved.Play.Ownership, node.Ownership);
                collider.gameObject.layer = 2;
            };
            world = WordboundCourseWorld.Build(resolved, catalogue, parent.transform, options);
            Physics.SyncTransforms();
            Assert.AreEqual(resolved.Hash, world.ManifestHash);
            Assert.AreEqual(callbackCount, world.Surfaces.Count);
            Assert.IsTrue(world.Surfaces.All(c => c.gameObject.layer == 2));
            Assert.IsEmpty(world.GetComponentsInChildren<Rigidbody>());
            foreach (var module in resolved.Geometry)
            {
                var anchor = Anchor(WordboundCourseNodeKind.Module, module.Id);
                Assert.That(Vector3.Distance(anchor.position, world.ToWorld(module.Position)), Is.LessThan(.0001f));
                var floor = world.Surfaces.Where(c => Node(c).Kind == WordboundCourseNodeKind.Floor && Node(c).ModuleId == module.Id).ToArray();
                Assert.IsNotEmpty(floor);
                Assert.That(floor.Min(c => c.bounds.min.x), Is.EqualTo(module.Bounds.Min.X / 1000f).Within(.001f));
                Assert.That(floor.Max(c => c.bounds.max.x), Is.EqualTo(module.Bounds.Max.X / 1000f).Within(.001f));
                Assert.That(floor.Min(c => c.bounds.min.z), Is.EqualTo(module.Bounds.Min.Z / 1000f).Within(.001f));
                Assert.That(floor.Max(c => c.bounds.max.z), Is.EqualTo(module.Bounds.Max.Z / 1000f).Within(.001f));
                Assert.That(floor.Max(c => c.bounds.max.y), Is.EqualTo(module.Position.Y / 1000f).Within(.001f));
            }
            foreach (var spawn in resolved.Spawns)
                Assert.That(Vector3.Distance(Anchor(WordboundCourseNodeKind.Spawn, spawn.Id).position,
                    world.ToWorld(spawn.Position)), Is.LessThan(.0001f));
            foreach (var supply in resolved.Supplies)
            {
                var anchor = Anchor(WordboundCourseNodeKind.Supply, supply.Id);
                Assert.AreEqual(supply.TeamIndex, anchor.GetComponent<WordboundCourseNode>().OwnerTeam);
                Assert.AreEqual(supply.Protected, anchor.GetComponent<WordboundCourseNode>().Protected);
                Assert.IsNull(anchor.GetComponent<Collider>());
            }
            foreach (var puzzle in resolved.Puzzles) Assert.IsNotNull(Anchor(WordboundCourseNodeKind.Puzzle, puzzle.Id));
            foreach (var checkpoint in document.Checkpoints) Assert.IsNotNull(Anchor(WordboundCourseNodeKind.Checkpoint, checkpoint.Id));
        }

        [Test]
        public void AllThreeLapBranchesHaveContinuousSupportedSurfacesAndCapsuleHeadroom()
        {
            var catalogue = CourseTemplates.ProofCatalogue();
            var resolved = Resolve(CourseTemplates.RaceCircuit(), catalogue, "TimeRun");
            world = WordboundCourseWorld.Build(resolved, catalogue, parent.transform, options);
            Physics.SyncTransforms();
            foreach (var connection in resolved.Connections)
            {
                Assert.AreEqual(connection.GatePuzzleId,
                    Anchor(WordboundCourseNodeKind.Connection, connection.Id).GetComponent<WordboundCourseNode>().GatePuzzleId);
                AssertTraversable(connection);
            }
            Assert.IsFalse(SolidAt(new Vector3(2, 6, -3)), "Upper slab must not seal UpA's headroom.");
            Assert.IsFalse(SolidAt(new Vector3(2, 6, 3)), "Upper slab must not seal UpB's headroom.");
            Assert.IsTrue(SolidAt(new Vector3(0, 5.9f, -3)), "Floor can remain where the stair is safely below it.");
            Assert.IsTrue(SolidAt(new Vector3(9.1f, 5.9f, 0)), "Return must not remove the Upper floor below safe headroom.");
            Assert.IsFalse(SolidAt(new Vector3(12, 6, 0)), "Return still needs its upper landing opening.");
            Assert.IsFalse(SolidAt(new Vector3(10, 3.8f, 0)), "Return must cross the lower room wall.");
            Assert.IsTrue(SolidAt(new Vector3(10, 1, 7)), "Unrelated wall panels remain present.");
            Assert.IsTrue(SolidAt(new Vector3(0, 5.9f, 8)), "Safe upper landing remains supported.");
        }

        [Test]
        public void BridgeAndRampUseExactEndpointSurfaces()
        {
            var catalogue = CourseTemplates.ProofCatalogue();
            var document = CourseTemplates.FreestyleWorld();
            document.Zones.Clear();
            document.Modules.Single(m => m.Id == "Garden").Position = new CoursePoint(30000, 0, 0);
            var connection = document.Connections[0];
            connection.Kind = "Bridge";
            connection.From = new CoursePoint(5000, 0, 0);
            connection.To = new CoursePoint(25000, 0, 0);
            world = WordboundCourseWorld.Build(Resolve(document, catalogue, "Freestyle"), catalogue, parent.transform, options);
            Physics.SyncTransforms();
            AssertTraversable(world.Course.Connections.Single());
            Assert.IsTrue(SolidAt(new Vector3(15, -.1f, 0)), "The gap needs the bridge surface.");
            world.Dispose();
            document = CourseTemplates.RaceCircuit();
            document.Connections.Single(c => c.Id == "UpA").Kind = "Ramp";
            world = WordboundCourseWorld.Build(Resolve(document, catalogue, "TimeRun"), catalogue, parent.transform, options);
            Physics.SyncTransforms();
            AssertTraversable(world.Course.Connections.Single(c => c.Id == "UpA"));
        }

        [TestCase("Stairs"), TestCase("Ramp")]
        public void AscendingConnectorsCanRunInTheOppositeHorizontalDirection(string kind)
        {
            var catalogue = CourseTemplates.ProofCatalogue();
            var document = CourseTemplates.RaceCircuit();
            var connection = document.Connections.Single(c => c.Id == "UpA");
            connection.Kind = kind;
            connection.From = new CoursePoint(5000, 0, -3000);
            connection.To = new CoursePoint(-5000, 6000, -3000);
            world = WordboundCourseWorld.Build(Resolve(document, catalogue, "TimeRun"), catalogue, parent.transform, options);
            Physics.SyncTransforms();
            AssertTraversable(world.Course.Connections.Single(c => c.Id == "UpA"));
            Assert.IsFalse(SolidAt(new Vector3(-2, 6, -3)), "Headroom opening follows the reversed horizontal ascent.");
            Assert.IsTrue(SolidAt(new Vector3(2, 5.9f, -3)), "The low end retains safe overhead floor.");
        }

        [Test]
        public void FourLayersAndRotatedRectangularModulesRetainAuthoredDimensions()
        {
            var proof = CourseTemplates.ProofCatalogue();
            var catalogue = new CourseCatalogue(proof.Modules.Values.Concat(new[]
                { new CourseModuleDefinition("Rectangle", 20000, 12000, 4000) }), proof.Assets.Values,
                proof.PuzzlePresets, proof.Themes, proof.NarrativeEvents);
            var document = CourseTemplates.FreestyleWorld();
            for (int layer = 1; layer <= 3; layer++)
            {
                string id = "Level" + layer;
                document.Layers.Add(new CourseLayer { Id = id, HeightMm = layer * 6000 });
                document.Modules.Add(new CourseModule
                {
                    Id = id, LayerId = id, DefinitionId = layer == 1 ? "Rectangle" : "Room",
                    Position = new CoursePoint((layer + 1) * 20000, layer * 6000, 0), Yaw = 90
                });
                document.Connections.Add(new CourseConnection
                {
                    Id = "Climb" + layer, FromModuleId = layer == 1 ? "Garden" : "Level" + (layer - 1), ToModuleId = id,
                    Kind = "Stairs", From = new CoursePoint(layer * 20000 + 5000, (layer - 1) * 6000, 0),
                    To = new CoursePoint((layer + 1) * 20000 - 5000, layer * 6000, 0)
                });
            }
            var resolved = Resolve(document, catalogue, "Freestyle");
            world = WordboundCourseWorld.Build(resolved, catalogue, parent.transform, options);
            Physics.SyncTransforms();
            Assert.AreEqual(4, world.GetComponentsInChildren<WordboundCourseNode>().Count(n => n.Kind == WordboundCourseNodeKind.Layer));
            Assert.That(Anchor(WordboundCourseNodeKind.Module, "Level3").position.y, Is.EqualTo(18).Within(.001f));
            Assert.That(Quaternion.Angle(Anchor(WordboundCourseNodeKind.Module, "Level1").rotation,
                Quaternion.Euler(0, 90, 0)), Is.LessThan(.001f));
            var floor = world.Surfaces.Where(c => Node(c).Kind == WordboundCourseNodeKind.Floor && Node(c).ModuleId == "Level1").ToArray();
            Assert.That(floor.Max(c => c.bounds.max.x) - floor.Min(c => c.bounds.min.x), Is.EqualTo(12).Within(.001f));
            Assert.That(floor.Max(c => c.bounds.max.z) - floor.Min(c => c.bounds.min.z), Is.EqualTo(20).Within(.001f));
            foreach (var c in resolved.Connections) AssertTraversable(c);
        }

        [Test]
        public void DisposeAndFailedConstructionRemoveOnlyOwnedObjectsAndKeepBorrowedMaterials()
        {
            var catalogue = CourseTemplates.ProofCatalogue();
            var course = Resolve(CourseTemplates.FreestyleWorld(), catalogue, "Freestyle");
            var sibling = new GameObject("ExistingSibling");
            sibling.transform.SetParent(parent.transform);
            world = WordboundCourseWorld.Build(course, catalogue, parent.transform, options);
            world.Dispose();
            Assert.AreEqual(1, parent.transform.childCount);
            Assert.IsTrue(material != null);
            options.ConfigureCollision = (_, __) => throw new InvalidOperationException("fixture failure");
            Assert.Throws<InvalidOperationException>(() => WordboundCourseWorld.Build(course, catalogue, parent.transform, options));
            Assert.AreEqual(1, parent.transform.childCount);
            Assert.IsTrue(sibling != null);
        }

        [Test]
        public void RebuildPreservesStableAnchorsAndTransformsWithoutChangingManifest()
        {
            var catalogue = CourseTemplates.ProofCatalogue();
            var course = Resolve(CourseTemplates.RaceCircuit(), catalogue, "TimeRun");
            string before = CourseJson.ExportManifest(course);
            parent.transform.SetPositionAndRotation(new Vector3(20, 3, -10), Quaternion.Euler(0, 90, 0));
            world = WordboundCourseWorld.Build(course, catalogue, parent.transform, options);
            Vector3 position = Anchor(WordboundCourseNodeKind.Spawn, "Start0").position;
            int count = world.Surfaces.Count;
            world.Dispose();
            world = WordboundCourseWorld.Build(course, catalogue, parent.transform, options);
            Assert.AreEqual(position, Anchor(WordboundCourseNodeKind.Spawn, "Start0").position);
            Assert.AreEqual(count, world.Surfaces.Count);
            Assert.AreEqual(before, CourseJson.ExportManifest(course));
            Assert.That(Vector3.Distance(world.ToCourse(position), new Vector3(-7, 0, -8)), Is.LessThan(.001f));
        }

        [Test]
        public void UnsupportedTraversalAndWrongCatalogueFailBeforeCreatingGeometry()
        {
            var catalogue = CourseTemplates.ProofCatalogue();
            var document = CourseTemplates.RaceCircuit();
            document.Connections.Single(c => c.Id == "UpA").Kind = "Traversal";
            var course = Resolve(document, catalogue, "TimeRun");
            Assert.Throws<NotSupportedException>(() => WordboundCourseWorld.Build(course, catalogue, parent.transform, options));
            Assert.AreEqual(0, parent.transform.childCount);
            var changedCatalogue = new CourseCatalogue(catalogue.Modules.Values, catalogue.Assets.Values,
                catalogue.PuzzlePresets, catalogue.Themes.Concat(new[] { "OtherTheme" }), catalogue.NarrativeEvents);
            course = Resolve(CourseTemplates.RaceCircuit(), catalogue, "TimeRun");
            Assert.Throws<ArgumentException>(() => WordboundCourseWorld.Build(course, changedCatalogue, parent.transform, options));
            Assert.AreEqual(0, parent.transform.childCount);
            parent.transform.localScale = new Vector3(2, 1, 1);
            Assert.Throws<ArgumentException>(() => WordboundCourseWorld.Build(course, catalogue, parent.transform, options));
            Assert.AreEqual(0, parent.transform.childCount);
        }

        Transform Anchor(WordboundCourseNodeKind kind, string id)
        {
            Assert.IsTrue(world.TryGetAnchor(kind, id, out var value), kind + ":" + id);
            return value;
        }

        static ResolvedCourse Resolve(CourseDocument document, CourseCatalogue catalogue, string mode)
        {
            var result = CourseResolver.Resolve(document, catalogue,
                new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(ModeDefinition.For(mode))));
            Assert.IsTrue(result.Success, string.Join("; ", result.Issues.Select(i => i.Code + ": " + i.Message)));
            return result.Manifest;
        }

        static WordboundCourseNode Node(Collider collider) => collider.GetComponent<WordboundCourseNode>();
        bool SolidAt(Vector3 point) => world.Surfaces.Any(c => (c.ClosestPoint(point) - point).sqrMagnitude < .0000001f);

        void AssertTraversable(ResolvedCourseConnection connection)
        {
            Vector3 a = world.ToWorld(connection.From), b = world.ToWorld(connection.To);
            if (connection.Kind == "Walk" || connection.Kind == "Door")
            {
                var from = world.Course.Geometry.Single(m => m.Id == connection.FromModuleId);
                var to = world.Course.Geometry.Single(m => m.Id == connection.ToModuleId);
                Vector3 direction = (world.ToWorld(to.Position) - world.ToWorld(from.Position)).normalized;
                a -= direction; b += direction;
            }
            var probeObject = new GameObject("ClearanceProbe");
            try
            {
                var probe = probeObject.AddComponent<CapsuleCollider>();
                probe.radius = .3f;
                probe.height = 1.8f;
                probe.enabled = false;
                for (int i = 1; i < 20; i++)
                {
                    Vector3 foot = Vector3.Lerp(a, b, i / 20f);
                    var ray = new Ray(foot + Vector3.up * .05f, Vector3.down);
                    Assert.IsTrue(world.Surfaces.Any(c => c.Raycast(ray, out var hit, .1f) &&
                        Mathf.Abs(hit.point.y - foot.y) < .01f), connection.Id + " lacks floor at " + foot);
                    Vector3 centre = foot + Vector3.up * 1.02f;
                    foreach (var collider in world.Surfaces)
                    {
                        bool blocked = Physics.ComputePenetration(probe, centre, Quaternion.identity, collider,
                            collider.transform.position, collider.transform.rotation, out _, out float depth);
                        Assert.IsFalse(blocked && depth > .005f, connection.Id + " blocked by " + collider.name + " at " + foot);
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(probeObject); }
        }
    }
}
