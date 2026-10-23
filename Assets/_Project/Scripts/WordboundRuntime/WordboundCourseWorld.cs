using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.WordboundRuntime
{
    [DisallowMultipleComponent]
    public sealed class WordboundCourseWorld : MonoBehaviour, IDisposable
    {
        public const float FloorThickness = .24f;
        public const float WallThickness = .2f;
        public const float ConnectorHeadroom = 2.4f;
        const int MaxSurfaces = 32768;
        readonly Dictionary<string, Transform> anchors = new Dictionary<string, Transform>(StringComparer.Ordinal);
        readonly List<Collider> surfaces = new List<Collider>();
        readonly List<Mesh> surfaceMeshes = new List<Mesh>();
        WordboundCourseWorldOptions options;
        bool disposed;

        public ResolvedCourse Course { get; private set; }
        public string ManifestHash => Course.Hash;
        public IReadOnlyList<Collider> Surfaces => surfaces.AsReadOnly();

        public static WordboundCourseWorld Build(ResolvedCourse course, CourseCatalogue catalogue,
            Transform parent = null, WordboundCourseWorldOptions options = null)
        {
            if (course == null || catalogue == null) throw new ArgumentNullException(nameof(course));
            if (course.CatalogueHash != catalogue.Hash) throw new ArgumentException("The course catalogue binding differs.");
            if (parent != null && (parent.lossyScale - Vector3.one).sqrMagnitude > .000001f)
                throw new ArgumentException("The course parent must preserve metre scale.");
            if (course.Connections.Any(c => c.Kind == "Traversal"))
                throw new NotSupportedException("Traversal connectors require their physics preset adapter.");
            if (course.Connections.Any(c => (c.Kind == "Stairs" || c.Kind == "Ramp") && c.To.Y <= c.From.Y))
                throw new ArgumentException("Stairs and ramps require ascending endpoints under the course contract.");
            var root = new GameObject("WordboundCourse");
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            var world = root.AddComponent<WordboundCourseWorld>();
            world.Course = course;
            world.options = options ?? new WordboundCourseWorldOptions();
            try
            {
                world.Construct(catalogue);
                root.SetActive(true);
                return world;
            }
            catch
            {
                world.Dispose();
                throw;
            }
        }

        public bool TryGetAnchor(WordboundCourseNodeKind kind, string id, out Transform anchor) =>
            anchors.TryGetValue(Key(kind, id), out anchor);

        public Vector3 ToWorld(CoursePoint point) => transform.TransformPoint(Metres(point));
        public Vector3 ToCourse(Vector3 worldPosition) => transform.InverseTransformPoint(worldPosition);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            gameObject.SetActive(false);
            anchors.Clear();
            surfaces.Clear();
            ReleaseSurfaceMeshes();
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        void OnDestroy() => ReleaseSurfaceMeshes();

        void ReleaseSurfaceMeshes()
        {
            foreach (var mesh in surfaceMeshes)
                if (mesh)
                {
                    if (Application.isPlaying) Destroy(mesh);
                    else DestroyImmediate(mesh);
                }
            surfaceMeshes.Clear();
        }

        void Construct(CourseCatalogue catalogue)
        {
            var document = Course.CopyDocument();
            foreach (var layer in document.Layers)
                Anchor(WordboundCourseNodeKind.Layer, layer.Id, "", new Vector3(0, layer.HeightMm / 1000f, 0), 0, transform);
            foreach (var module in Course.Geometry)
            {
                var definition = catalogue.Modules[module.DefinitionId];
                var layer = anchors[Key(WordboundCourseNodeKind.Layer, module.LayerId)];
                var node = Anchor(WordboundCourseNodeKind.Module, module.Id, module.Id, Metres(module.Position), module.Yaw, layer);
                BuildFloor(module, node);
                BuildWalls(module, definition.HeightMm / 1000f, node);
            }
            foreach (var connection in Course.Connections) BuildConnection(connection);
            foreach (var spawn in Course.Spawns)
                Anchor(WordboundCourseNodeKind.Spawn, spawn.Id, spawn.ModuleId, Metres(spawn.Position), spawn.Yaw);
            foreach (var recovery in Course.RecoveryAnchors)
                Anchor(WordboundCourseNodeKind.Recovery, recovery.Id, recovery.ModuleId, Metres(recovery.Position));
            foreach (var puzzle in Course.Puzzles)
                Anchor(WordboundCourseNodeKind.Puzzle, puzzle.Id, puzzle.ModuleId,
                    Metres(Course.Geometry.Single(m => m.Id == puzzle.ModuleId).Position));
            foreach (var checkpoint in document.Checkpoints)
                Anchor(WordboundCourseNodeKind.Checkpoint, checkpoint.Id, checkpoint.ModuleId,
                    Metres(Course.RecoveryAnchors.Single(a => a.Id == checkpoint.RecoveryAnchorId).Position));
            foreach (var placement in Course.Placements)
                Anchor(WordboundCourseNodeKind.Placement, placement.Id, placement.ModuleId, Metres(placement.Position),
                    placement.Yaw, team: placement.TeamIndex, protectedResource: placement.Purpose == "Resources");
            foreach (var supply in Course.Supplies)
                Anchor(WordboundCourseNodeKind.Supply, supply.Id, supply.ModuleId, Metres(supply.Position),
                    team: supply.TeamIndex, protectedResource: supply.Protected);
        }

        void BuildFloor(ResolvedCourseModule module, Transform parent)
        {
            Vector3 min = Metres(module.Bounds.Min), max = Metres(module.Bounds.Max);
            var pieces = new List<Rect> { Rect.MinMaxRect(min.x, min.z, max.x, max.z) };
            foreach (var c in Course.Connections)
            {
                if (c.Kind != "Stairs" && c.Kind != "Ramp") continue;
                Vector3 a = Metres(c.From), b = Metres(c.To);
                if (min.y <= Mathf.Min(a.y, b.y) || min.y > Mathf.Max(a.y, b.y) + ConnectorHeadroom + FloorThickness) continue;
                float firstClearance = Mathf.Clamp01((min.y - FloorThickness - ConnectorHeadroom - a.y) / (b.y - a.y));
                Vector3 openingStart = Vector3.Lerp(a, b, firstClearance);
                float halfWidth = c.WidthMm / 2000f;
                Rect hole = Mathf.Abs(b.x - a.x) > .001f ?
                    Rect.MinMaxRect(Mathf.Min(openingStart.x, b.x), a.z - halfWidth, Mathf.Max(openingStart.x, b.x), a.z + halfWidth) :
                    Rect.MinMaxRect(a.x - halfWidth, Mathf.Min(openingStart.z, b.z), a.x + halfWidth, Mathf.Max(openingStart.z, b.z));
                pieces = Subtract(pieces, hole);
            }
            foreach (var piece in pieces)
                Box(WordboundCourseNodeKind.Floor, module.Id, module.Id, "",
                    new Vector3(piece.center.x, min.y - FloorThickness / 2, piece.center.y),
                    new Vector3(piece.width, FloorThickness, piece.height), Quaternion.identity, parent, FloorMaterial(),
                    options.FloorMaterial == null ? Art.Surfaces.Kind.Planks : (Art.Surfaces.Kind?)null);
        }

        void BuildWalls(ResolvedCourseModule module, float height, Transform parent)
        {
            Vector3 min = Metres(module.Bounds.Min), max = Metres(module.Bounds.Max);
            for (int side = 0; side < 4; side++)
            {
                bool fixedX = side < 2;
                float fixedValue = side == 0 ? min.x : side == 1 ? max.x : side == 2 ? min.z : max.z;
                float start = fixedX ? min.z : min.x, end = fixedX ? max.z : max.x;
                var pieces = new List<Rect> { Rect.MinMaxRect(start, min.y, end, min.y + height) };
                foreach (var c in Course.Connections)
                {
                    Vector3 a = Metres(c.From), b = Metres(c.To);
                    float from = fixedX ? a.x : a.z, to = fixedX ? b.x : b.z;
                    float delta = to - from;
                    float t;
                    if (Mathf.Abs(delta) < .0001f)
                    {
                        if ((c.Kind != "Walk" && c.Kind != "Door") || Mathf.Abs(from - fixedValue) > .0001f ||
                            c.FromModuleId != module.Id && c.ToModuleId != module.Id) continue;
                        t = 0;
                    }
                    else
                    {
                        t = (fixedValue - from) / delta;
                        if (t < 0 || t > 1) continue;
                    }
                    var point = Vector3.Lerp(a, b, t);
                    float across = fixedX ? point.z : point.x;
                    float halfWidth = c.WidthMm / 2000f;
                    float slopeAllowance = Mathf.Abs(delta) < .0001f ? 0 : Mathf.Abs((b.y - a.y) / delta) * WallThickness / 2;
                    pieces = Subtract(pieces, Rect.MinMaxRect(across - halfWidth, point.y - FloorThickness - slopeAllowance,
                        across + halfWidth, point.y + ConnectorHeadroom + slopeAllowance));
                }
                foreach (var piece in pieces)
                {
                    Vector3 centre = fixedX ? new Vector3(fixedValue, piece.center.y, piece.center.x) :
                        new Vector3(piece.center.x, piece.center.y, fixedValue);
                    Vector3 size = fixedX ? new Vector3(WallThickness, piece.height, piece.width) :
                        new Vector3(piece.width, piece.height, WallThickness);
                    Box(WordboundCourseNodeKind.Wall, module.Id, module.Id, "", centre, size,
                        Quaternion.identity, parent, WallMaterial(),
                        options.WallMaterial == null ? Art.Surfaces.Kind.Plaster : (Art.Surfaces.Kind?)null);
                }
            }
        }

        void BuildConnection(ResolvedCourseConnection connection)
        {
            Vector3 a = Metres(connection.From), b = Metres(connection.To);
            var node = Anchor(WordboundCourseNodeKind.Connection, connection.Id, connection.FromModuleId, (a + b) / 2,
                gate: connection.GatePuzzleId);
            Anchor(WordboundCourseNodeKind.ConnectionFrom, connection.Id, connection.FromModuleId, a,
                parent: node, gate: connection.GatePuzzleId);
            Anchor(WordboundCourseNodeKind.ConnectionTo, connection.Id, connection.ToModuleId, b,
                parent: node, gate: connection.GatePuzzleId);
            if (connection.Kind == "Walk" || connection.Kind == "Door") return;
            Vector3 direction = (b - a).normalized;
            var rotation = Quaternion.LookRotation(direction, Vector3.up);
            Vector3 normal = rotation * Vector3.up;
            Box(WordboundCourseNodeKind.Connection, connection.Id, connection.FromModuleId, connection.GatePuzzleId,
                (a + b) / 2 - normal * FloorThickness / 2,
                new Vector3(connection.WidthMm / 1000f, FloorThickness, Vector3.Distance(a, b)),
                rotation, node, ConnectorMaterial(),
                options.ConnectorMaterial == null ? Art.Surfaces.Kind.Wood : (Art.Surfaces.Kind?)null);
        }

        Transform Anchor(WordboundCourseNodeKind kind, string id, string module, Vector3 position,
            float yaw = 0, Transform parent = null, string gate = "", int team = -1, bool protectedResource = true)
        {
            var go = new GameObject(kind + ":" + id);
            go.transform.SetParent(parent != null ? parent : transform, false);
            SetCoursePose(go.transform, position, Quaternion.Euler(0, yaw, 0));
            Tag(go, kind, id, module, gate, team, protectedResource);
            anchors.Add(Key(kind, id), go.transform);
            return go.transform;
        }

        void Box(WordboundCourseNodeKind kind, string id, string module, string gate,
            Vector3 position, Vector3 size, Quaternion rotation, Transform parent, Material material,
            Art.Surfaces.Kind? surface = null)
        {
            if (surfaces.Count >= MaxSurfaces) throw new InvalidOperationException("Course surface budget exceeded.");
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = kind + ":" + id + ":" + surfaces.Count.ToString(CultureInfo.InvariantCulture);
            go.transform.SetParent(parent, false);
            SetCoursePose(go.transform, position, rotation);
            go.transform.localScale = size;
            if (surface.HasValue)
            {
                var mesh = Art.Surfaces.CreateBox(size, Art.Surfaces.Tile(surface.Value),
                    surface.Value == Art.Surfaces.Kind.Wood ? (Vector3?)null : position,
                    surface.Value == Art.Surfaces.Kind.Wood);
                surfaceMeshes.Add(mesh);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
            }
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            var collider = go.GetComponent<BoxCollider>();
            var tag = Tag(go, kind, id, module, gate);
            surfaces.Add(collider);
            options.ConfigureCollision?.Invoke(collider, tag);
        }

        WordboundCourseNode Tag(GameObject go, WordboundCourseNodeKind kind, string id, string module,
            string gate, int team = -1, bool protectedResource = true)
        {
            var tag = go.AddComponent<WordboundCourseNode>();
            tag.Bind(kind, id, module, gate, Course.Play.Ownership, team, protectedResource);
            return tag;
        }

        void SetCoursePose(Transform child, Vector3 position, Quaternion rotation)
        {
            child.position = transform.TransformPoint(position);
            child.rotation = transform.rotation * rotation;
        }

        Material FloorMaterial() => options.FloorMaterial != null ? options.FloorMaterial :
            Art.Surfaces.Get(Art.Surfaces.Kind.Planks, new Color(.65f, .48f, .3f));
        Material WallMaterial() => options.WallMaterial != null ? options.WallMaterial :
            Art.Surfaces.Get(Art.Surfaces.Kind.Plaster, new Color(.85f, .8f, .67f));
        Material ConnectorMaterial() => options.ConnectorMaterial != null ? options.ConnectorMaterial :
            Art.Surfaces.Get(Art.Surfaces.Kind.Wood, new Color(.46f, .62f, .57f));

        static string Key(WordboundCourseNodeKind kind, string id) => kind + ":" + id;
        static Vector3 Metres(CoursePoint p) => new Vector3(p.X / 1000f, p.Y / 1000f, p.Z / 1000f);
        static List<Rect> Subtract(List<Rect> input, Rect hole)
        {
            var result = new List<Rect>();
            foreach (var r in input)
            {
                float left = Mathf.Max(r.xMin, hole.xMin), right = Mathf.Min(r.xMax, hole.xMax);
                float bottom = Mathf.Max(r.yMin, hole.yMin), top = Mathf.Min(r.yMax, hole.yMax);
                if (right <= left || top <= bottom) { result.Add(r); continue; }
                Add(result, r.xMin, r.yMin, left, r.yMax);
                Add(result, right, r.yMin, r.xMax, r.yMax);
                Add(result, left, r.yMin, right, bottom);
                Add(result, left, top, right, r.yMax);
            }
            return result;
        }
        static void Add(List<Rect> list, float x1, float y1, float x2, float y2)
        {
            if (list.Count >= MaxSurfaces) throw new InvalidOperationException("Course surface budget exceeded.");
            if (x2 - x1 > .00001f && y2 - y1 > .00001f) list.Add(Rect.MinMaxRect(x1, y1, x2, y2));
        }
    }
}
