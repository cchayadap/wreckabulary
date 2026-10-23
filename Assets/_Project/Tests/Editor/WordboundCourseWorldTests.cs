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
        public void DefaultSurfaceUvsUseOriginalMetreScaleAcrossCutoutsAndTransformedParents()
        {
            var catalogue = CourseTemplates.ProofCatalogue();
            var course = Resolve(CourseTemplates.RaceCircuit(), catalogue, "TimeRun");
            parent.transform.SetPositionAndRotation(new Vector3(20, 3, -10), Quaternion.Euler(0, 90, 0));
            world = WordboundCourseWorld.Build(course, catalogue, parent.transform);
            var topCorners = new List<(Collider surface, Vector3 point, Vector2 uv)>();
            var floorWidths = new HashSet<float>();
            int wallVertices = 0;
            foreach (var surface in world.Surfaces)
            {
                var kind = Node(surface).Kind;
                if (kind != WordboundCourseNodeKind.Floor && kind != WordboundCourseNodeKind.Wall) continue;
                var mesh = surface.GetComponent<MeshFilter>().sharedMesh;
                var vertices = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv;
                if (kind == WordboundCourseNodeKind.Floor) floorWidths.Add(surface.transform.localScale.x);
                for (int i = 0; i < vertices.Length; i++)
                {
                    var point = world.ToCourse(surface.transform.TransformPoint(vertices[i]));
                    if (kind == WordboundCourseNodeKind.Floor && normals[i] == Vector3.up)
                    {
                        Assert.That(uv[i].x, Is.EqualTo(point.x / 4f).Within(.0001f));
                        Assert.That(uv[i].y, Is.EqualTo(point.z / 1.2f).Within(.0001f));
                        topCorners.Add((surface, point, uv[i]));
                    }
                    else if (kind == WordboundCourseNodeKind.Wall && Mathf.Abs(normals[i].y) < .01f)
                    {
                        float across = Mathf.Abs(normals[i].z) > .99f ? point.x : point.z;
                        Assert.That(uv[i].x, Is.EqualTo(across / 3f).Within(.0001f));
                        Assert.That(uv[i].y, Is.EqualTo(point.y / 3f).Within(.0001f));
                        wallVertices++;
                    }
                }
            }
            Assert.Greater(floorWidths.Count, 1, "Actual cutouts must exercise unequal surface sizes.");
            Assert.Greater(wallVertices, 0);
            Assert.Greater(topCorners.Max(c => c.uv.x) - topCorners.Min(c => c.uv.x), 1f,
                "A whole course must not stretch one stock cube UV repeat.");
            int sharedCorners = 0;
            for (int a = 0; a < topCorners.Count; a++)
                for (int b = a + 1; b < topCorners.Count; b++)
                    if (topCorners[a].surface != topCorners[b].surface &&
                        Vector3.Distance(topCorners[a].point, topCorners[b].point) < .0001f)
                    {
                        Assert.That(Vector2.Distance(topCorners[a].uv, topCorners[b].uv), Is.LessThan(.0001f),
                            "A floor cutout must not restart the pattern on adjacent pieces.");
                        sharedCorners++;
                    }
            Assert.Greater(sharedCorners, 0, "The real split floor must exercise a shared seam.");
        }

        [TestCase("Stairs"), TestCase("Ramp")]
        public void DefaultConnectorUvsFollowActualSlopedLengthWithoutChangingEndpoints(string kind)
        {
            var catalogue = CourseTemplates.ProofCatalogue();
            var document = CourseTemplates.RaceCircuit();
            var authored = document.Connections.Single(c => c.Id == "UpA");
            authored.Kind = kind;
            authored.From = new CoursePoint(5000, 0, -3000);
            authored.To = new CoursePoint(-5000, 6000, -3000);
            parent.transform.SetPositionAndRotation(new Vector3(-7, 2, 15), Quaternion.Euler(0, 90, 0));
            world = WordboundCourseWorld.Build(Resolve(document, catalogue, "TimeRun"), catalogue, parent.transform);
            var connection = world.Course.Connections.Single(c => c.Id == "UpA");
            var surface = world.Surfaces.Single(c => Node(c).Kind == WordboundCourseNodeKind.Connection &&
                Node(c).StableId == connection.Id);
            var mesh = surface.GetComponent<MeshFilter>().sharedMesh;
            var normals = mesh.normals; var uv = mesh.uv;
            var top = Enumerable.Range(0, mesh.vertexCount).Where(i => normals[i] == Vector3.up).ToArray();
            Assert.AreEqual(4, top.Length);
            var from = world.ToWorld(connection.From); var to = world.ToWorld(connection.To);
            Assert.That(top.Max(i => uv[i].x) - top.Min(i => uv[i].x),
                Is.EqualTo(Vector3.Distance(from, to) / 2f).Within(.0001f), "Wood grain follows the full slope.");
            Assert.That(top.Max(i => uv[i].y) - top.Min(i => uv[i].y),
                Is.EqualTo(connection.WidthMm / 2000f).Within(.0001f));
            Assert.That(Vector3.Distance(surface.transform.TransformPoint(new Vector3(0, .5f, -.5f)), from),
                Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(surface.transform.TransformPoint(new Vector3(0, .5f, .5f)), to),
                Is.LessThan(.0001f));
            Assert.AreEqual(new Bounds(Vector3.zero, Vector3.one), mesh.bounds);
            Assert.AreEqual(Vector3.zero, ((BoxCollider)surface).center);
            Assert.AreEqual(Vector3.one, ((BoxCollider)surface).size);
        }

        [Test]
        public void DefaultUvMeshesPreserveCustomSurfacesPhysicsAndBorrowedMaterials()
        {
            var catalogue = CourseTemplates.ProofCatalogue();
            var course = Resolve(CourseTemplates.RaceCircuit(), catalogue, "TimeRun");
            string manifest = CourseJson.ExportManifest(course);
            parent.transform.SetPositionAndRotation(new Vector3(13, 2, -8), Quaternion.Euler(0, 90, 0));
            int callbacks = 0;
            options.ConfigureCollision = (_, __) => callbacks++;
            world = WordboundCourseWorld.Build(course, catalogue, parent.transform, options);
            var before = world.Surfaces.ToDictionary(c => c.name, c =>
            {
                var box = (BoxCollider)c;
                Assert.AreSame(material, c.GetComponent<MeshRenderer>().sharedMaterial);
                return (position: c.transform.position, rotation: c.transform.rotation, scale: c.transform.localScale,
                    centre: box.center, size: box.size, mesh: c.GetComponent<MeshFilter>().sharedMesh);
            });
            Assert.AreEqual(before.Count, callbacks);
            world.Dispose();
            Assert.IsTrue(material != null);
            callbacks = 0;
            var defaults = new WordboundCourseWorldOptions { ConfigureCollision = (_, __) => callbacks++ };
            world = WordboundCourseWorld.Build(course, catalogue, parent.transform, defaults);
            Assert.AreEqual(before.Count, callbacks);
            Assert.AreEqual(manifest, CourseJson.ExportManifest(world.Course));
            var shared = world.Surfaces.ToDictionary(c => c.name, c =>
            {
                var previous = before[c.name]; var box = (BoxCollider)c;
                var mesh = c.GetComponent<MeshFilter>().sharedMesh;
                Assert.AreEqual(previous.position, c.transform.position);
                Assert.AreEqual(previous.rotation, c.transform.rotation);
                Assert.AreEqual(previous.scale, c.transform.localScale);
                Assert.AreEqual(previous.centre, box.center);
                Assert.AreEqual(previous.size, box.size);
                Assert.AreEqual(previous.mesh.bounds, mesh.bounds);
                Assert.IsNull(c.GetComponent<MeshCollider>());
                Assert.AreNotSame(previous.mesh, mesh, "Only default rendering changes from the stock cube.");
                return (mesh, uv: mesh.uv, material: c.GetComponent<MeshRenderer>().sharedMaterial);
            });
            world.Dispose();
            foreach (var entry in shared.Values)
            {
                Assert.IsTrue(entry.mesh == null, "World disposal releases its generated UV mesh.");
                Assert.IsTrue(entry.material != null);
            }
            world = WordboundCourseWorld.Build(course, catalogue, parent.transform, defaults);
            foreach (var surface in world.Surfaces)
            {
                var mesh = surface.GetComponent<MeshFilter>().sharedMesh;
                Assert.AreNotSame(shared[surface.name].mesh, mesh);
                CollectionAssert.AreEqual(shared[surface.name].uv, mesh.uv);
                Assert.AreSame(shared[surface.name].material, surface.GetComponent<MeshRenderer>().sharedMaterial);
            }
            world.Dispose();
            world = WordboundCourseWorld.Build(course, catalogue, parent.transform, options);
            foreach (var surface in world.Surfaces)
            {
                Assert.AreSame(before[surface.name].mesh, surface.GetComponent<MeshFilter>().sharedMesh);
                Assert.AreSame(material, surface.GetComponent<MeshRenderer>().sharedMaterial);
            }
        }

        [Test]
        public void UncachedSurfaceFactoryMatchesOriginalBoxWithoutOwningBorrowedCache()
        {
            var size = new Vector3(3, .24f, 7); var tile = new Vector2(2, 2);
            var origin = new Vector3(9, 3, -7);
            var borrowed = Art.Surfaces.Box(size, tile, origin, true);
            int count = Art.Surfaces.MeshCount;
            var owned = Art.Surfaces.CreateBox(size, tile, origin, true);
            try
            {
                Assert.AreNotSame(borrowed, owned);
                Assert.AreEqual(count, Art.Surfaces.MeshCount);
                Assert.AreEqual(borrowed.bounds, owned.bounds);
                CollectionAssert.AreEqual(borrowed.vertices, owned.vertices);
                CollectionAssert.AreEqual(borrowed.normals, owned.normals);
                CollectionAssert.AreEqual(borrowed.tangents, owned.tangents);
                CollectionAssert.AreEqual(borrowed.uv, owned.uv);
                CollectionAssert.AreEqual(borrowed.triangles, owned.triangles);
            }
            finally { UnityEngine.Object.DestroyImmediate(owned); }
            Assert.IsTrue(borrowed != null);
            Assert.AreSame(borrowed, Art.Surfaces.Box(size, tile, origin, true));
        }

        [Test]
        public void ChangingCoursesReleaseUvMeshesOnDisposeParentDestructionAndConstructionFailure()
        {
            var proof = CourseTemplates.ProofCatalogue();
            var borrowed = Art.Surfaces.Box(Vector3.one, Vector2.one);
            int cachedMeshes = Art.Surfaces.MeshCount;
            for (int revision = 0; revision < 3; revision++)
            {
                var catalogue = new CourseCatalogue(new[] {
                    new CourseModuleDefinition("Room", 20000 + revision * 1000, 20000, 4000)
                }, proof.Assets.Values, proof.PuzzlePresets, proof.Themes, proof.NarrativeEvents);
                var document = CourseTemplates.CombatArena();
                document.Modules[0].Position = new CoursePoint(revision * 500, 0, revision * 250);
                world = WordboundCourseWorld.Build(Resolve(document, catalogue, "FreeForAll"), catalogue, parent.transform);
                var meshes = world.Surfaces.Select(c => c.GetComponent<MeshFilter>().sharedMesh).ToArray();
                var materials = world.Surfaces.Select(c => c.GetComponent<MeshRenderer>().sharedMaterial).ToArray();
                Assert.Greater(meshes.Length, 0);
                Assert.AreEqual(cachedMeshes, Art.Surfaces.MeshCount, "Course changes must not grow the original mesh cache.");
                if (revision == 1)
                {
                    UnityEngine.Object.DestroyImmediate(parent);
                    parent = new GameObject("CourseFixtureAfterParentDestruction");
                }
                else world.Dispose();
                Assert.IsTrue(meshes.All(m => m == null), "Every owned native mesh must be destroyed.");
                Assert.IsTrue(materials.All(m => m != null), "Shared original materials are borrowed.");
                Assert.IsTrue(borrowed != null);
            }
            var failedMeshes = new List<Mesh>();
            var fail = new WordboundCourseWorldOptions { ConfigureCollision = (collider, _) =>
            {
                failedMeshes.Add(collider.GetComponent<MeshFilter>().sharedMesh);
                if (failedMeshes.Count == 3) throw new InvalidOperationException("fixture failure after owned meshes");
            }};
            var course = Resolve(CourseTemplates.CombatArena(), proof, "FreeForAll");
            Assert.Throws<InvalidOperationException>(() => WordboundCourseWorld.Build(course, proof, parent.transform, fail));
            Assert.AreEqual(3, failedMeshes.Count);
            Assert.IsTrue(failedMeshes.All(m => m == null));
            Assert.AreEqual(0, parent.transform.childCount);
            Assert.AreEqual(cachedMeshes, Art.Surfaces.MeshCount);
            Assert.IsTrue(borrowed != null);
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
