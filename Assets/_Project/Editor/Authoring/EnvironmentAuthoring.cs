using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    /// <summary>Adds saved room presentation once without regenerating authored structure or furniture.</summary>
    public static class EnvironmentAuthoring
    {
        public const string PrintPath = "Assets/_Project/Art/Generated/Environment/townhouse-print-v1.png";
        static readonly string[] Maps = { "pinwheel", "courtyard", "flat", "terrace", "walkup" };
        static readonly Color Cream = Surfaces.Hex(0xF1E6D2), Oak = Surfaces.Hex(0xB78A5C);
        static readonly Dictionary<string, Material> detailMaterials = new();

        [MenuItem("Wreckabulary/Authoring/Dress Current Worlds", priority = 20)]
        public static void UpgradeWorlds()
        {
            Guard();
            foreach (string map in Maps)
            {
                string path = "Assets/_Project/Resources/" + AuthoredHouse.ResourcePath(map) + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var world = root.GetComponent<AuthoredHouse>();
                    if (!world || !world.IsCurrent) throw new InvalidOperationException("Migrate the authored world first: " + path);
                    bool changed = RepairImportedModelAnchors(root);
                    changed |= ApplyMissing(world);
                    if (!changed) continue;
                    Persist(root, map);
                    if (!PrefabUtility.SaveAsPrefabAsset(root, path)) throw new InvalidOperationException("Could not save dressing: " + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("ENVIRONMENT_WORLDS_READY: five editable worlds dressed without rebuilding gameplay geometry.");
        }

        [MenuItem("Wreckabulary/Authoring/Dress Hub and Tutorial", priority = 21)]
        public static void UpgradeScenes()
        {
            Guard();
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previousPath = SceneManager.GetActiveScene().path;
            try
            {
                foreach (string name in new[] { "Hub", "Tutorial" })
                {
                    var scene = EditorSceneManager.OpenScene(SceneWorkspace.SceneFolder + name + ".unity", OpenSceneMode.Single);
                    var room = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Room");
                    if (!room) continue;
                    bool changed = RepairImportedModelAnchors(room);
                    changed |= ApplyMissingScene(room, name);
                    if (!changed) continue;
                    Persist(room, name.ToLowerInvariant());
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save dressing: " + scene.path);
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (!string.IsNullOrEmpty(previousPath)) EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Single);
            }
            Debug.Log("ENVIRONMENT_SCENES_READY: Hub and Tutorial presentation saved; original placements retained.");
        }

        public static bool ApplyMissing(AuthoredHouse world)
        {
            Guard();
            if (!world || !world.GeometryRoot) throw new ArgumentException("An authored geometry root is required.", nameof(world));
            var layout = GameConfig.Current.HouseFor(world.MapId);
            var existing = world.GeometryRoot.GetComponentsInChildren<RoomDressing>(true).Select(d => d.RoomId).ToHashSet();
            bool changed = false;
            foreach (var room in layout.Rooms)
            {
                if (room.Name == "Garden" || existing.Contains(room.Name)) continue;
                int storey = layout.StoreyOf(room);
                var parent = world.GeometryRoot.Find(RoomBuilder.StoreyName(storey));
                if (!parent) throw new InvalidOperationException("Missing authored storey: " + room.Name);
                var floors = layout.StoreyFloors();
                float ceiling = storey + 1 < floors.Count ? floors[storey + 1] - .28f : room.FloorY + 3f;
                var holes = layout.Stairs.Where(s => s.Lower == room.Name).Select(StairOpening).ToList();
                var area = Rect.MinMaxRect(room.MinX + .1f, room.MinZ + .1f, room.MaxX - .1f, room.MaxZ - .1f);
                var blocked = FurnitureBounds(world.FurnitureRoot, world.GeometryRoot, room.FloorY);
                foreach (var spawn in layout.Spawns.Where(s => s.Room == room.Name))
                    blocked.Add(new Bounds(new Vector3(spawn.X, room.FloorY + .7f, spawn.Z), new Vector3(2.4f, 1.4f, 2.4f)));
                var faces = Faces(world.GeometryRoot, room);
                BuildRoom(parent, world.GeometryRoot, room, storey, ceiling, Slabs(area, holes), faces, blocked,
                    roof: storey + 1 == floors.Count);
                changed = true;
            }
            return AddMissingDoorLintels(world) || changed;
        }

        /// <summary>Fill only the presentation gap above each 2.4 m doorway; gameplay colliders stay unchanged.</summary>
        public static bool AddMissingDoorLintels(AuthoredHouse world)
        {
            Guard();
            bool changed = false;
            var layout = GameConfig.Current.HouseFor(world.MapId);
            var floors = layout.StoreyFloors();
            foreach (var dressing in world.GeometryRoot.GetComponentsInChildren<RoomDressing>(true))
            {
                if (dressing.Edition != 1 || dressing.HasDoorLintels) continue;
                var room = layout.Room(dressing.RoomId);
                if (room == null || room.Name == "Garden") continue;
                int storey = layout.StoreyOf(room);
                float ceiling = storey + 1 < floors.Count ? floors[storey + 1] - .28f : room.FloorY + 3f;
                float bottom = room.FloorY + TallWall.Interior;
                foreach (var door in layout.Doors.Where(d => d.A == room.Name || d.B == room.Name))
                {
                    if (ceiling - bottom < .02f) continue;
                    bool alongZ = Mathf.Min(Mathf.Abs(door.X - room.MinX), Mathf.Abs(door.X - room.MaxX)) < .35f;
                    Vector3 inward = alongZ
                        ? (Mathf.Abs(door.X - room.MinX) < Mathf.Abs(door.X - room.MaxX) ? Vector3.right : Vector3.left)
                        : (Mathf.Abs(door.Z - room.MinZ) < Mathf.Abs(door.Z - room.MaxZ) ? Vector3.forward : Vector3.back);
                    var at = new Vector3(door.X, (bottom + ceiling) * .5f, door.Z) + inward * .125f;
                    var group = Group(dressing.transform, "Door header - " + door.A + " to " + door.B);
                    group.gameObject.AddComponent<HousePresentation>();
                    Block(group, world.GeometryRoot, "Door lintel", at, new Vector3(door.Width + .02f, ceiling - bottom, .035f),
                        Quaternion.LookRotation(inward), Surfaces.Kind.Plaster, Cream);
                    Block(group, world.GeometryRoot, "Door cornice", new Vector3(at.x, ceiling - .055f, at.z),
                        new Vector3(door.Width + .02f, .11f, .11f), Quaternion.LookRotation(inward), Surfaces.Kind.Wood, Cream);
                    Mark(group, CutawayKind.UpperWall, dressing);
                }
                dressing.MarkDoorLintels();
                EditorUtility.SetDirty(dressing);
                changed = true;
            }
            return changed;
        }

        /// <summary>Retain the saved intended pose on an anchor, restoring only the imported child's source transform.</summary>
        public static bool RepairImportedModelAnchors(GameObject root)
        {
            Guard();
            bool changed = false;
            var library = ModelLibrary.Load();
            foreach (var dressing in root.GetComponentsInChildren<RoomDressing>(true))
            {
                if (dressing.Edition != 1 || dressing.HasImportedModelAnchors) continue;
                foreach (var entry in new[] { (key: "Environment/Window_Frame", parent: "Window vignette"),
                             (key: "Environment/Curtain", parent: "Window vignette"),
                             (key: "Environment/Wall_Sconce", parent: "Wall sconce") })
                {
                    var source = library ? library.Find(entry.key) : null;
                    if (!source) throw new InvalidOperationException("Missing original import for dressing repair: " + entry.key);
                    var imports = dressing.GetComponentsInChildren<Transform>(true)
                        .Where(t => t.parent && t.parent.name == entry.parent && t.name == source.name).ToArray();
                    foreach (var model in imports)
                    {
                        var anchor = Group(model.parent, "Imported model anchor - " + source.name);
                        anchor.SetLocalPositionAndRotation(model.localPosition, model.localRotation);
                        anchor.localScale = model.localScale;
                        model.SetParent(anchor, false);
                        model.SetLocalPositionAndRotation(source.transform.localPosition, source.transform.localRotation);
                        model.localScale = source.transform.localScale;
                        EditorUtility.SetDirty(model);
                    }
                }
                dressing.MarkImportedModelAnchors();
                EditorUtility.SetDirty(dressing);
                changed = true;
            }
            return changed;
        }

        public static bool ApplyMissingScene(GameObject roomRoot, string sceneName)
        {
            Guard();
            if (roomRoot.GetComponentInChildren<RoomDressing>(true)) return false;
            var floor = roomRoot.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "Floor");
            if (!floor) return false;
            var bounds = ModelVisual.BoundsIn(roomRoot.transform, floor.gameObject);
            var room = new RoomBox { Name = sceneName, MinX = bounds.min.x, MaxX = bounds.max.x,
                MinZ = bounds.min.z, MaxZ = bounds.max.z, FloorY = bounds.max.y };
            var faces = Faces(roomRoot.transform, room);
            float ceiling = Mathf.Max(3.3f, faces.Count > 0 ? faces.Max(f => f.Top) + .12f : 3.3f);
            // The original open-front stage stays open; only its rear lounge/training bay is covered.
            var covered = Rect.MinMaxRect(room.MinX + .12f, Mathf.Lerp(room.MinZ, room.MaxZ, .58f), room.MaxX - .12f, room.MaxZ - .12f);
            var blocked = new List<Bounds>();
            foreach (var root in roomRoot.scene.GetRootGameObjects())
            {
                foreach (var item in root.GetComponentsInChildren<LetterBuilt>(true))
                    blocked.Add(ModelVisual.BoundsIn(roomRoot.transform, item.gameObject));
                foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    var at = roomRoot.transform.InverseTransformPoint(label.transform.position);
                    if (at.y > .8f) blocked.Add(new Bounds(at, new Vector3(8f, 1.5f, .35f)));
                }
            }
            foreach (var existing in roomRoot.GetComponentsInChildren<Renderer>(true).Where(r => r.name is "Window" or "Window Frame"))
                blocked.Add(ModelVisual.BoundsIn(roomRoot.transform, existing.gameObject));
            BuildRoom(roomRoot.transform, roomRoot.transform, room, 0, ceiling, new List<Rect> { covered }, faces, blocked, true);
            return true;
        }

        static void Persist(GameObject root, string stem)
        {
            foreach (var dressing in root.GetComponentsInChildren<RoomDressing>(true))
            {
                var marker = dressing.GetComponent<HousePresentation>();
                if (!marker || !marker.IsAuthored)
                {
                    if (!marker) dressing.gameObject.AddComponent<HousePresentation>();
                    WorldAuthoring.PersistPresentation(dressing.gameObject, "dressing1_shared");
                }
                foreach (var addition in dressing.GetComponentsInChildren<HousePresentation>(true))
                    if (!addition.IsAuthored) WorldAuthoring.PersistPresentation(addition.gameObject, "dressing1_shared");
            }
        }

        sealed class Face
        {
            public Vector3 Center, Inward;
            public float Width, Thickness, Top;
            public Quaternion Rotation => Quaternion.LookRotation(Inward);
            public Vector3 Right => Rotation * Vector3.right;
        }

        static List<Face> Faces(Transform space, RoomBox room)
        {
            var faces = new List<Face>();
            foreach (var renderer in space.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!renderer.enabled || !renderer.name.StartsWith("Wall", StringComparison.Ordinal) || !renderer.GetComponent<BoxCollider>()) continue;
                if (renderer.GetComponentInParent<RoomDressing>()) continue;
                var b = ModelVisual.BoundsIn(space, renderer.gameObject);
                if (Mathf.Abs(b.min.y - room.FloorY) > .25f || b.size.y < 1f) continue;
                bool alongX = b.size.x > b.size.z;
                float edge = alongX ? b.center.z : b.center.x;
                float minimum = alongX ? room.MinZ : room.MinX, maximum = alongX ? room.MaxZ : room.MaxX;
                if (Mathf.Min(Mathf.Abs(edge - minimum), Mathf.Abs(edge - maximum)) > .35f) continue;
                float start = Mathf.Max(alongX ? b.min.x : b.min.z, alongX ? room.MinX : room.MinZ);
                float end = Mathf.Min(alongX ? b.max.x : b.max.z, alongX ? room.MaxX : room.MaxZ);
                if (end - start < .35f) continue;
                bool lowSide = Mathf.Abs(edge - minimum) < Mathf.Abs(edge - maximum);
                faces.Add(new Face
                {
                    Center = alongX ? new Vector3((start + end) * .5f, room.FloorY, edge) : new Vector3(edge, room.FloorY, (start + end) * .5f),
                    Inward = alongX ? (lowSide ? Vector3.forward : Vector3.back) : (lowSide ? Vector3.right : Vector3.left),
                    Width = end - start, Thickness = alongX ? b.size.z : b.size.x, Top = b.max.y
                });
            }
            return faces;
        }

        static void BuildRoom(Transform parent, Transform space, RoomBox room, int storey, float ceiling,
            List<Rect> slabs, List<Face> faces, List<Bounds> blocked, bool roof)
        {
            var root = Group(parent, "Room dressing - " + room.Name);
            var data = root.gameObject.AddComponent<RoomDressing>();
            data.Configure(room.Name, storey);
            Color accent = Accent(room.Name);
            foreach (var area in slabs)
            {
                var panel = Block(root, space, "Ceiling panel", new Vector3(area.center.x, ceiling + .05f, area.center.y),
                    new Vector3(area.width, .1f, area.height), Quaternion.identity, Surfaces.Kind.Plaster, Cream);
                Mark(panel.transform, CutawayKind.Ceiling, data);
                if (roof)
                {
                    var cap = Block(root, space, "Roof cap", new Vector3(area.center.x, ceiling + .145f, area.center.y),
                        new Vector3(area.width + .12f, .09f, area.height + .12f), Quaternion.identity, Surfaces.Kind.Plaster, Surfaces.Hex(0xB9775F));
                    Mark(cap.transform, CutawayKind.Roof, data);
                }
            }
            int n = 0;
            foreach (var face in faces)
            {
                var wall = Group(root, "Wall finish " + ++n);
                float inset = face.Thickness * .5f + .025f;
                Vector3 at = face.Center + face.Inward * inset;
                Block(wall, space, "Painted wall panel", at + Vector3.up * .59f, new Vector3(face.Width - .08f, .98f, .028f), face.Rotation, Surfaces.Kind.Plaster, accent);
                Block(wall, space, "Picture rail", at + Vector3.up * 1.115f, new Vector3(face.Width - .06f, .055f, .06f), face.Rotation, Surfaces.Kind.Wood, Oak);
                float height = ceiling - face.Top;
                if (height > .02f)
                    Block(wall, space, "Upper wall infill", new Vector3(at.x, face.Top + height * .5f, at.z),
                        new Vector3(face.Width - .015f, height, .035f), face.Rotation, Surfaces.Kind.Plaster, Cream);
                Block(wall, space, "Ceiling cornice", new Vector3(at.x, ceiling - .055f, at.z),
                    new Vector3(face.Width - .02f, .11f, .11f), face.Rotation, Surfaces.Kind.Wood, Cream);
                Mark(wall, CutawayKind.UpperWall, data);
            }
            var candidates = faces.Where(f => f.Width > 2.2f).OrderByDescending(f => f.Width).ToList();
            if (candidates.Count == 0) return;
            if (Location(candidates, blocked, 1.8f, room.FloorY + 1.8f, out var windowFace, out float windowOffset))
            {
                Window(root, space, windowFace, room, data, windowOffset);
                blocked.Add(new Bounds(windowFace.Center + windowFace.Right * windowOffset + Vector3.up * 1.8f, new Vector3(1.9f, 1.6f, 1.9f)));
            }
            if (Location(candidates, blocked, 1.1f, room.FloorY + 2.02f, out var printFace, out float printOffset))
            {
                Picture(root, space, printFace, room, data, printOffset);
                blocked.Add(new Bounds(printFace.Center + printFace.Right * printOffset + Vector3.up * 2.02f, Vector3.one * 1.15f));
            }
            int lamps = room.MaxX - room.MinX > 10f || room.MaxZ - room.MinZ > 10f ? 2 : 1;
            for (int i = 0; i < lamps; i++)
                if (Location(candidates, blocked, .35f, room.FloorY + 2.42f, out var lampFace, out float lampOffset))
                {
                    Sconce(root, space, lampFace, room, data, lampOffset);
                    blocked.Add(new Bounds(lampFace.Center + lampFace.Right * lampOffset + Vector3.up * 2.42f, Vector3.one * .65f));
                }
        }

        static bool Location(List<Face> faces, List<Bounds> blocked, float width, float y, out Face selected, out float offset)
        {
            foreach (var face in faces)
                foreach (float ratio in new[] { 0f, -.26f, .26f, -.38f, .38f })
                {
                    float shift = face.Width * ratio;
                    if (Mathf.Abs(shift) + width * .5f > face.Width * .5f - .2f) continue;
                    if (!Clear(face.Center + face.Right * shift + face.Inward * .22f, width, y, blocked)) continue;
                    selected = face;
                    offset = shift;
                    return true;
                }
            selected = null;
            offset = 0f;
            return false;
        }

        static bool Clear(Vector3 center, float width, float y, List<Bounds> blocked)
        {
            var box = new Bounds(new Vector3(center.x, y, center.z), new Vector3(width, 1.15f, width));
            return !blocked.Any(b => { b.Expand(.35f); return b.Intersects(box); });
        }

        static void Window(Transform parent, Transform space, Face face, RoomBox room, RoomDressing data, float offset)
        {
            var group = Group(parent, "Window vignette");
            Vector3 at = face.Center + face.Right * offset + face.Inward * (face.Thickness * .5f + .09f);
            float floor = room.FloorY;
            Block(group, space, "Window recess", new Vector3(at.x, floor + 1.83f, at.z), new Vector3(1.52f, 1.45f, .055f), face.Rotation, Surfaces.Kind.Wood, Oak);
            var glass = Block(group, space, "Window glass", new Vector3(at.x, floor + 1.83f, at.z) + face.Inward * .035f,
                new Vector3(1.29f, 1.22f, .035f), face.Rotation, Surfaces.Kind.Plaster, Surfaces.Hex(0x8ABFD0));
            glass.sharedMaterial = DetailMaterial("Dressing window daylight", () =>
            {
                var glow = new Material(glass.sharedMaterial);
                glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor", new Color(.14f, .25f, .3f));
                return glow;
            });
            Model(group, space, "Environment/Window_Frame", new Vector3(at.x, floor + 1.23f, at.z) + face.Inward * .07f, face.Rotation, 1.13f);
            if (room.Name is not ("Bathroom" or "Garage" or "Hall" or "Lobby") && !room.Name.StartsWith("Landing", StringComparison.Ordinal))
                Model(group, space, "Environment/Curtain", new Vector3(at.x, floor + 1.13f, at.z) + face.Inward * .14f, face.Rotation, 1.22f);
            Block(group, space, "Window sill", new Vector3(at.x, floor + 1.13f, at.z) + face.Inward * .08f,
                new Vector3(1.7f, .10f, .22f), face.Rotation, Surfaces.Kind.Wood, Cream);
            Mark(group, CutawayKind.UpperWall, data);
        }

        static void Picture(Transform parent, Transform space, Face face, RoomBox room, RoomDressing data, float offset)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PrintPath);
            if (!texture) throw new InvalidOperationException("The authored townhouse print is missing: " + PrintPath);
            var group = Group(parent, "Framed townhouse print");
            Vector3 at = face.Center + face.Right * offset + face.Inward * (face.Thickness * .5f + .09f) + Vector3.up * 2.02f;
            Block(group, space, "Oak picture frame", at, new Vector3(.98f, .98f, .055f), face.Rotation, Surfaces.Kind.Wood, Oak);
            var art = GameObject.CreatePrimitive(PrimitiveType.Quad);
            art.name = "Print surface";
            art.transform.SetParent(group, false);
            Place(art.transform, space, at + face.Inward * .033f, face.Rotation * Quaternion.Euler(0f, 180f, 0f));
            art.transform.localScale = new Vector3(.87f, .87f, 1f);
            Object.DestroyImmediate(art.GetComponent<Collider>());
            art.GetComponent<Renderer>().sharedMaterial = DetailMaterial("Framed townhouse illustration", () =>
            {
                var material = new Material(GameAssets.I.tintBase) { color = Color.white };
                material.SetTexture("_BaseMap", texture);
                material.SetFloat("_Smoothness", .12f);
                return material;
            });
            Mark(group, CutawayKind.UpperWall, data);
        }

        static void Sconce(Transform parent, Transform space, Face face, RoomBox room, RoomDressing data, float offset)
        {
            var group = Group(parent, "Wall sconce");
            Vector3 at = face.Center + face.Right * offset + face.Inward * (face.Thickness * .5f + .05f) + Vector3.up * 2.12f;
            Model(group, space, "Environment/Wall_Sconce", at, face.Rotation, 1.15f);
            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = "Bulb glow";
            bulb.transform.SetParent(group, false);
            Place(bulb.transform, space, at + Vector3.up * .31f + face.Inward * .19f, Quaternion.identity);
            bulb.transform.localScale = Vector3.one * .11f;
            Object.DestroyImmediate(bulb.GetComponent<Collider>());
            bulb.GetComponent<Renderer>().sharedMaterial = DetailMaterial("Warm practical bulb", () =>
            {
                var material = new Material(GameAssets.I.tintBase) { color = Surfaces.Hex(0xFFE5AE) };
                material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", Surfaces.Hex(0xFFD890) * 2.1f);
                return material;
            });
            bulb.AddComponent<Light>();
            bulb.AddComponent<PracticalLight>().Configure(Surfaces.Hex(0xFFE2B0), 1.4f, 5f);
            Mark(group, CutawayKind.UpperWall, data);
        }

        static void Model(Transform parent, Transform space, string key, Vector3 at, Quaternion rotation, float scale)
        {
            var source = ModelLibrary.Load().Find(key);
            if (!source) throw new InvalidOperationException("Missing dressing model: " + key);
            var anchor = Group(parent, "Imported model anchor - " + source.name);
            Place(anchor, space, at, rotation);
            anchor.localScale = Vector3.one * scale;
            var model = ModelVisual.Spawn(key, anchor);
            if (!model) throw new InvalidOperationException("Missing dressing model: " + key);
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        }

        static MeshRenderer Block(Transform parent, Transform space, string name, Vector3 at, Vector3 size, Quaternion rotation, Surfaces.Kind kind, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Place(go.transform, space, at, rotation);
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = Surfaces.Box(size, Surfaces.Tile(kind));
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Surfaces.Get(kind, color);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return renderer;
        }

        static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        static Material DetailMaterial(string name, Func<Material> create)
        {
            if (detailMaterials.TryGetValue(name, out var material) && material) return material;
            material = create();
            material.name = name;
            material.hideFlags = HideFlags.DontSave;
            detailMaterials[name] = material;
            return material;
        }

        static void Place(Transform item, Transform space, Vector3 position, Quaternion rotation) =>
            item.SetPositionAndRotation(space.TransformPoint(position), space.rotation * rotation);

        static void Mark(Transform group, CutawayKind kind, RoomDressing room) =>
            group.gameObject.AddComponent<CutawaySurface>().Configure(kind, room.OwnerStorey, room.RoomId, group.GetComponentsInChildren<Renderer>(true));

        static List<Bounds> FurnitureBounds(Transform root, Transform space, float floor)
        {
            var bounds = new List<Bounds>();
            if (!root) return bounds;
            foreach (var item in root.GetComponentsInChildren<Smashable>(true))
            {
                var b = ModelVisual.BoundsIn(space, item.gameObject);
                if (b.min.y > floor - .5f && b.min.y < floor + 1f) bounds.Add(b);
            }
            return bounds;
        }

        static List<Rect> Slabs(Rect area, IEnumerable<Rect> holes)
        {
            var pieces = new List<Rect> { area };
            foreach (var hole in holes)
            {
                var next = new List<Rect>();
                foreach (var piece in pieces)
                {
                    if (!piece.Overlaps(hole)) { next.Add(piece); continue; }
                    float x0 = Mathf.Max(piece.xMin, hole.xMin), x1 = Mathf.Min(piece.xMax, hole.xMax);
                    float z0 = Mathf.Max(piece.yMin, hole.yMin), z1 = Mathf.Min(piece.yMax, hole.yMax);
                    next.Add(Rect.MinMaxRect(piece.xMin, piece.yMin, piece.xMax, z0));
                    next.Add(Rect.MinMaxRect(piece.xMin, z1, piece.xMax, piece.yMax));
                    next.Add(Rect.MinMaxRect(piece.xMin, z0, x0, z1));
                    next.Add(Rect.MinMaxRect(x1, z0, piece.xMax, z1));
                }
                pieces = next.Where(p => p.width > .05f && p.height > .05f).ToList();
            }
            return pieces;
        }

        static Rect StairOpening(Stairway stair)
        {
            bool alongX = Mathf.Abs(stair.ToX - stair.FromX) > Mathf.Abs(stair.ToZ - stair.FromZ);
            float xMargin = alongX ? Stairway.StepOff : .1f, zMargin = alongX ? .1f : Stairway.StepOff;
            return Rect.MinMaxRect(stair.MinX - xMargin, stair.MinZ - zMargin, stair.MaxX + xMargin, stair.MaxZ + zMargin);
        }

        static Color Accent(string room) => Surfaces.Hex(room.TrimEnd('1', '2', '3') switch
        {
            "Kitchen" => 0x8DAE9E, "Bathroom" => 0x84BCC0, "Study" or "BlueFlat" => 0x839FB6,
            "Bedroom" => 0xC598A4, "KidsRoom" or "Playroom" or "YellowFlat" => 0xDABA72,
            "RedFlat" or "Cafe" or "LivingRoom" => 0xC98E75, "GreenFlat" => 0x93AC83,
            "Garage" => 0xA6AA9E, "Hub" => 0x8BAC9E, _ => 0xB9B69C
        });

        static void Guard()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Dress authored environments outside Play mode.");
        }
    }
}
