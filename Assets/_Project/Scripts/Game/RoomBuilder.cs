using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    [DefaultExecutionOrder(-150)]
    public class RoomBuilder : MonoBehaviour
    {
        [SerializeField] Transform furnitureRoot;
        Transform geometry;
        GameObject sign;
        readonly List<Smashable> originals = new();
        public HouseLayout Layout { get; private set; }
        public IReadOnlyList<Smashable> Originals => originals;

        void Awake()
        {
            Layout = GameConfig.Current.HouseFor(Session.MapId);
            var oldRoom = GameObject.Find("Room");
            if (oldRoom) oldRoom.SetActive(false);
            var found = GameObject.Find("Sign");
            if (Session.MapId is "pinwheel" or "courtyard") sign = found;
            else if (found) found.SetActive(false);
            if (furnitureRoot) furnitureRoot.gameObject.SetActive(false);
            geometry = CreateGeometry(Layout, Session.MapId, transform);
            ApplyFog(false);
            if (Layout.StoreyFloors().Count > 1) gameObject.AddComponent<StoreyCutaway>().Configure(Layout, geometry);
            var joins = GetComponent<PlayerJoinManager>();
            if (joins) joins.ConfigureLayout(Layout);
            var camera = FindAnyObjectByType<CameraRig>();
            if (camera) camera.FrameLayout(Layout);
            BuildFurniture();
        }

        public void ResetRoom(bool furnish = true)
        {
            SummonedThing.ClearAll();
            World.ClearTransient();
            if (TilePool.Instance) TilePool.Instance.ReleaseAll();
            if (furnitureRoot) { furnitureRoot.gameObject.SetActive(false); Destroy(furnitureRoot.gameObject); }
            originals.Clear();
            furnitureRoot = new GameObject("Original furniture").transform;
            furnitureRoot.SetParent(transform, false);
            if (furnish) BuildFurnitureIntoRoot();
        }

        void BuildFurniture()
        {
            if (furnitureRoot) Destroy(furnitureRoot.gameObject);
            furnitureRoot = new GameObject("Original furniture").transform;
            furnitureRoot.SetParent(transform, false);
            BuildFurnitureIntoRoot();
        }

        void BuildFurnitureIntoRoot()
        {
            foreach (var f in Layout.Furniture)
            {
                var prop = FurnitureCatalog.Spawn(f.Word, new Vector3(f.X, Layout.Room(f.Room).FloorY + f.Y, f.Z), f.Yaw, furnitureRoot);
                originals.Add(prop);
            }
        }

        public void SetTallWalls(bool tall)
        {
            if (sign) sign.SetActive(!tall);
            SetTallWalls(geometry, tall);
        }

        public static void SetTallWalls(Transform geometry, bool tall)
        {
            if (!geometry) return;
            foreach (var wall in geometry.GetComponentsInChildren<TallWall>(true)) wall.Apply(tall);
        }

        public static void ApplyFog(bool thirdPerson)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color32(0x17, 0x3a, 0x3d, 0xff);
            RenderSettings.fogStartDistance = thirdPerson ? 48f : 60f;
            RenderSettings.fogEndDistance = thirdPerson ? 100f : 130f;
        }

        public static string StoreyName(int storey) => "Storey " + storey;

        public static Transform CreateGeometry(HouseLayout layout, string mapId, Transform parent, bool includeExtras = true)
        {
            var root = new GameObject(layout.Name + " geometry").transform;
            root.SetParent(parent, false);
            new GeometryFactory(layout, mapId, root, includeExtras).Build();
            return root;
        }

        sealed class GeometryFactory
        {
            readonly HouseLayout Layout;
            readonly string mapId;
            readonly Transform geometry;
            readonly bool includeExtras;
            public GeometryFactory(HouseLayout layout, string id, Transform root, bool extras)
            { Layout = layout; mapId = id; geometry = root; includeExtras = extras; }

            public void Build()
            {
                storeyFloors = Layout.StoreyFloors();
                storeys = new Transform[storeyFloors.Count];
                for (int s = 0; s < storeys.Length; s++)
                {
                    storeys[s] = new GameObject(StoreyName(s)).transform;
                    storeys[s].SetParent(geometry, false);
                }
                var edges = new Dictionary<string, Edge>();
                for (int i = 0; i < Layout.Rooms.Count; i++)
                {
                    var r = Layout.Rooms[i];
                    int storey = Layout.StoreyOf(r);
                    bool garden = r.Name == "Garden";
                    var holes = Layout.Stairs.Where(s => s.Upper == r.Name).Select(s => Rect.MinMaxRect(s.MinX, s.MinZ, s.MaxX, s.MaxZ));
                    var (floor, floorColour) = FloorOf(r.Name);
                    foreach (var piece in Slab(r, holes))
                        Surface(r.Name + " floor", storeys[storey], new Vector3(piece.center.x, r.FloorY - .12f, piece.center.y),
                            new Vector3(piece.width, .24f, piece.height), floor, floorColour);
                    AddEdge(edges, storey, r.FloorY, true, r.MinX, r.MinZ, r.MaxZ);
                    AddEdge(edges, storey, r.FloorY, true, r.MaxX, r.MinZ, r.MaxZ);
                    AddEdge(edges, storey, r.FloorY, false, r.MinZ, r.MinX, r.MaxX);
                    AddEdge(edges, storey, r.FloorY, false, r.MaxZ, r.MinX, r.MaxX);
                    if (includeExtras && !garden && RoomKind(r.Name) != "Garage") BuildRug(r, storey);
                }
                foreach (var edge in edges.Values) BuildEdge(edge);
                foreach (var s in Layout.Stairs) BuildStairs(s);
                for (int s = 1; s < storeys.Length; s++)
                    foreach (var r in storeys[s].GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
                if (!includeExtras) return;
                BuildSurroundings();
                var ground = storeys[0];
                if (mapId == "pinwheel")
                {
                    Surface("Playroom balcony", ground, new Vector3(3.25f, 1.55f, 2.5f), new Vector3(1.5f, .3f, 3f), Surfaces.Kind.Wood, new Color(.7f, .52f, .36f));
                    var ramp = Surface("Balcony ramp", ground, new Vector3(3.25f, .76f, .02f), new Vector3(1.5f, .15f, 2.7f), Surfaces.Kind.Wood, new Color(.65f, .47f, .33f), turned: true);
                    ramp.transform.rotation = Quaternion.Euler(-40f, 0f, 0f);
                    Decor("Environment/Railing_2m", ground, new Vector3(2.5f, 1.7f, 2.5f), 1f, 90f);
                }
                else if (mapId == "courtyard")
                {
                    Surface("Courtyard path east-west", ground, new Vector3(0f, .008f, 0f), new Vector3(12f, .018f, 2.5f), Surfaces.Kind.Plaster, new Color(.75f, .72f, .59f), false);
                    Surface("Courtyard path north-south", ground, new Vector3(0f, .009f, 0f), new Vector3(2.5f, .018f, 12f), Surfaces.Kind.Plaster, new Color(.75f, .72f, .59f), false);
                    Decor("Items/PLANT", ground, new Vector3(-4.5f, 0f, 4.5f), 1.4f);
                    Decor("Items/PLANT", ground, new Vector3(4.5f, 0f, -4.5f), 1.4f);
                }
            }

            List<float> storeyFloors;
            Transform[] storeys;

            static readonly Color Plaster = Surfaces.Hex(0xEDDFC4), Trim = Surfaces.Hex(0xB58760);

            static string RoomKind(string room) => room.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');

            static (Surfaces.Kind kind, Color colour) FloorOf(string room) => RoomKind(room) switch
            {
                "Garden" => (Surfaces.Kind.Lawn, Surfaces.Hex(0x86A17A)),
                "Kitchen" => (Surfaces.Kind.Checker, Color.white),
                "Bathroom" => (Surfaces.Kind.Tile, Color.white),
                "Garage" => (Surfaces.Kind.Plaster, Surfaces.Hex(0xA7A39A)),
                "Study" => (Surfaces.Kind.Planks, Surfaces.Hex(0xB68E6B)),
                _ => (Surfaces.Kind.Planks, Surfaces.Hex(0xCBA37B)),
            };

            static Color RugOf(string room) => Surfaces.Hex(RoomKind(room) switch
            {
                "Garden" => 0x92B78B, "Playroom" => 0xE1B970, "Bedroom" => 0xC8B3BD, "Kitchen" => 0xB8C9BE, "Study" => 0xA8B7C8,
                "LivingRoom" => 0xD4B397, "Hall" => 0xC9B79C, "Bathroom" => 0xB7D3D6, "Landing" => 0xBFB3A0, "KidsRoom" => 0xE6C48A,
                "Cafe" => 0xCDA98A, "BlueFlat" => 0xA8B7C8, "RedFlat" => 0xD4A396, "GreenFlat" => 0xA9C4A0, "YellowFlat" => 0xE1C987,
                _ => 0xE4C695,
            });

            void BuildRug(RoomBox r, int storey)
            {
                var flights = Layout.Stairs.Where(s => s.Lower == r.Name || s.Upper == r.Name)
                    .Select(s => Rect.MinMaxRect(s.MinX - .4f, s.MinZ - .4f, s.MaxX + .4f, s.MaxZ + .4f));
                var free = Slab(Rect.MinMaxRect(r.MinX, r.MinZ, r.MaxX, r.MaxZ), flights).OrderByDescending(p => p.width * p.height).FirstOrDefault();
                float width = Mathf.Min(free.width * .62f, free.width - 1f), depth = Mathf.Min(free.height * .55f, free.height - 1f);
                if (width < 1f || depth < 1f) return;
                var size = new Vector3(width, .008f, depth);
                var rug = Piece("Rug", storeys[storey], new Vector3(free.center.x, r.FloorY + .004f, free.center.y), size,
                    Surfaces.Get(Surfaces.Kind.Rug, RugOf(r.Name), width / depth), Surfaces.Box(size, new Vector2(width, depth)), false);
                rug.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            void BuildSurroundings()
            {
                float minX = Layout.Rooms.Min(r => r.MinX), maxX = Layout.Rooms.Max(r => r.MaxX);
                float minZ = Layout.Rooms.Min(r => r.MinZ), maxZ = Layout.Rooms.Max(r => r.MaxZ);
                float cx = (minX + maxX) * .5f, cz = (minZ + maxZ) * .5f;
                var ground = storeys[0];
                var plinth = Surface("Plinth", ground, new Vector3(cx, -.45f, cz), new Vector3(maxX - minX + 2f, .6f, maxZ - minZ + 2f),
                    Surfaces.Kind.Plaster, Surfaces.Hex(0xC4AA86), false);
                var lawn = Surface("Lawn", ground, new Vector3(cx, -.85f, cz), new Vector3(240f, 1f, 240f), Surfaces.Kind.Lawn, Surfaces.Hex(0x789775), false);
                foreach (var piece in new[] { plinth, lawn }) piece.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                float extent = Mathf.Max(maxX - minX, maxZ - minZ) * .5f;
                for (int n = 0; n < 16; n++)
                {
                    float angle = (n + .5f) / 16f * Mathf.PI * 2f, radius = extent + 4f + n % 3 * 1.5f;
                    Decor("Items/PLANT", ground, new Vector3(cx + Mathf.Cos(angle) * radius, -.35f, cz + Mathf.Sin(angle) * radius), 1.8f + n % 3 * .35f, n * 53f);
                }
            }

            static List<Rect> Slab(RoomBox r, IEnumerable<Rect> holes) => Slab(Rect.MinMaxRect(r.MinX, r.MinZ, r.MaxX, r.MaxZ), holes);

            static List<Rect> Slab(Rect area, IEnumerable<Rect> holes)
            {
                var pieces = new List<Rect> { area };
                foreach (var hole in holes)
                {
                    var next = new List<Rect>();
                    foreach (var p in pieces)
                    {
                        if (!p.Overlaps(hole)) { next.Add(p); continue; }
                        float z0 = Mathf.Max(p.yMin, hole.yMin), z1 = Mathf.Min(p.yMax, hole.yMax);
                        next.Add(Rect.MinMaxRect(p.xMin, p.yMin, p.xMax, hole.yMin));
                        next.Add(Rect.MinMaxRect(p.xMin, hole.yMax, p.xMax, p.yMax));
                        next.Add(Rect.MinMaxRect(p.xMin, z0, hole.xMin, z1));
                        next.Add(Rect.MinMaxRect(hole.xMax, z0, p.xMax, z1));
                    }
                    pieces = next.Where(p => p.width > .01f && p.height > .01f).ToList();
                }
                return pieces;
            }

            sealed class Edge
            {
                public bool Vertical;
                public float Fixed, FloorY;
                public int Storey;
                public readonly List<Vector2> Intervals = new();
            }

            static void AddEdge(Dictionary<string, Edge> all, int storey, float floorY, bool vertical, float fixedAt, float min, float max)
            {
                string key = storey + (vertical ? "x" : "z") + fixedAt.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (!all.TryGetValue(key, out var edge)) all[key] = edge = new Edge { Vertical = vertical, Fixed = fixedAt, Storey = storey, FloorY = floorY };
                edge.Intervals.Add(new Vector2(min, max));
            }

            void BuildEdge(Edge edge)
            {
                var cuts = edge.Intervals.SelectMany(i => new[] { i.x, i.y }).ToList();
                var openings = Layout.Doors.Where(d => Layout.StoreyOf(Layout.Room(d.A)) == edge.Storey && Mathf.Abs((edge.Vertical ? d.X : d.Z) - edge.Fixed) < .01f)
                    .Select(d => new Vector2((edge.Vertical ? d.Z : d.X) - d.Width * .5f, (edge.Vertical ? d.Z : d.X) + d.Width * .5f)).ToArray();
                cuts.AddRange(openings.SelectMany(i => new[] { i.x, i.y }));
                cuts = cuts.Distinct().OrderBy(v => v).ToList();
                float height = edge.Storey + 1 < storeyFloors.Count ? Mathf.Min(3.3f, storeyFloors[edge.Storey + 1] - .24f - edge.FloorY) : 3.3f;
                for (int i = 0; i < cuts.Count - 1; i++)
                {
                    float mid = (cuts[i] + cuts[i + 1]) * .5f;
                    if (!edge.Intervals.Any(range => mid >= range.x && mid <= range.y) || openings.Any(range => mid > range.x && mid < range.y)) continue;
                    float length = cuts[i + 1] - cuts[i];
                    var at = edge.Vertical ? new Vector3(edge.Fixed, edge.FloorY + .55f, mid) : new Vector3(mid, edge.FloorY + .55f, edge.Fixed);
                    var wall = Surface("Wall", storeys[edge.Storey], at, edge.Vertical ? new Vector3(.2f, 1.1f, length) : new Vector3(length, 1.1f, .2f),
                        Surfaces.Kind.Plaster, Plaster);
                    var tall = wall.AddComponent<TallWall>();
                    tall.Tile = Surfaces.Tile(Surfaces.Kind.Plaster);
                    tall.Trim = Surface("Wall trim", storeys[edge.Storey], at, edge.Vertical ? new Vector3(.24f, .085f, length) : new Vector3(length, .085f, .24f),
                        Surfaces.Kind.Wood, Trim, false).transform;
                    tall.FloorY = edge.FloorY;
                    tall.Height = height;
                    tall.Outside = edge.Vertical
                        ? Outdoors(edge.Storey, edge.Fixed - .1f, mid) || Outdoors(edge.Storey, edge.Fixed + .1f, mid)
                        : Outdoors(edge.Storey, mid, edge.Fixed - .1f) || Outdoors(edge.Storey, mid, edge.Fixed + .1f);
                    tall.Apply(false);
                }
            }

            bool Outdoors(int storey, float x, float z) => !Layout.Rooms.Any(r => r.Name != "Garden" && Layout.StoreyOf(r) == storey
                && x > r.MinX && x < r.MaxX && z > r.MinZ && z < r.MaxZ);

            void BuildStairs(Stairway s)
            {
                RoomBox lower = Layout.Room(s.Lower), upper = Layout.Room(s.Upper);
                Transform below = storeys[Layout.StoreyOf(lower)], above = storeys[Layout.StoreyOf(upper)];
                s.Along(0f, out float fx, out float fz);
                s.Along(s.Run, out float tx, out float tz);
                var foot = new Vector3(fx, lower.FloorY, fz);
                var top = new Vector3(tx, upper.FloorY, tz);
                var slope = (top - foot).normalized;
                var flat = new Vector3(slope.x, 0f, slope.z).normalized;
                var across = Vector3.Cross(Vector3.up, flat);
                var normal = Vector3.Cross(slope, across);
                float length = Vector3.Distance(foot, top), rise = top.y - foot.y;
                var tilt = Quaternion.LookRotation(slope, normal);
                var wood = new Color(.65f, .47f, .33f);

                var ramp = Block("Stairs ramp", below, (foot + top) * .5f - normal * .075f, new Vector3(s.Width, .15f, length), wood);
                ramp.transform.rotation = tilt;
                ramp.GetComponent<Renderer>().enabled = false;

                int count = Mathf.Max(1, Mathf.RoundToInt(rise / .2f));
                float riser = rise / count, tread = s.Run / count;
                for (int i = 1; i < count; i++)
                {
                    float h = i * riser;
                    var step = Surface("Step", below, foot + flat * ((i + .5f) * tread) + Vector3.up * (h * .5f), new Vector3(s.Width, h, tread),
                        Surfaces.Kind.Wood, i % 2 == 0 ? wood : new Color(.7f, .52f, .36f), turned: true);
                    step.transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
                    var collider = step.GetComponent<BoxCollider>();
                    collider.size = new Vector3(1f, (h - .03f) / h, 1f);
                    collider.center = new Vector3(0f, -.015f / h, 0f);
                }

                float start = rise / s.Run;
                var railColour = new Color(.45f, .32f, .22f);
                foreach (float side in new[] { -1f, 1f })
                {
                    var rail = Surface("Stair rail", below, foot + slope * ((start + length) * .5f) + across * (side * (s.Width * .5f + .05f)) + normal * .5f,
                        new Vector3(.1f, 1f, length - start), Surfaces.Kind.Wood, railColour, turned: true);
                    rail.transform.rotation = tilt;
                }

                float y = upper.FloorY + .5f;
                var middle = (new Vector3(fx, 0f, fz) + new Vector3(tx, 0f, tz)) * .5f;
                foreach (float side in new[] { -1f, 1f })
                {
                    var at = middle + across * (side * (s.Width * .5f + .05f));
                    Surface("Stairwell railing", above, new Vector3(at.x, y, at.z), Scaled(flat, s.Run, .1f), Surfaces.Kind.Wood, railColour);
                }
                var end = new Vector3(fx, 0f, fz) - flat * .05f;
                Surface("Stairwell railing", above, new Vector3(end.x, y, end.z), Scaled(across, s.Width + .2f, .1f), Surfaces.Kind.Wood, railColour);
            }

            static Vector3 Scaled(Vector3 along, float length, float thickness) =>
                Mathf.Abs(along.x) > Mathf.Abs(along.z) ? new Vector3(length, 1f, thickness) : new Vector3(thickness, 1f, length);

            GameObject Block(string name, Transform group, Vector3 at, Vector3 scale, Color colour, bool solid = true) =>
                Piece(name, group, at, scale, GameAssets.I.Tinted(colour), null, solid);

            GameObject Surface(string name, Transform group, Vector3 at, Vector3 scale, Surfaces.Kind kind, Color colour, bool solid = true, bool turned = false) =>
                Piece(name, group, at, scale, Surfaces.Get(kind, colour), Surfaces.Box(scale, Surfaces.Tile(kind), turned ? null : at, kind == Surfaces.Kind.Wood), solid);

            GameObject Piece(string name, Transform group, Vector3 at, Vector3 scale, Material material, Mesh mesh, bool solid)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(group, false);
                go.transform.position = at;
                go.transform.localScale = scale;
                if (mesh) go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<Renderer>().sharedMaterial = material;
                if (!solid) Object.Destroy(go.GetComponent<Collider>());
                return go;
            }

            void Decor(string key, Transform group, Vector3 at, float scale, float yaw = 0f)
            {
                var library = ModelLibrary.Load();
                if (!library || !library.Find(key)) return;
                var root = new GameObject(key).transform;
                root.SetParent(group, false);
                root.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
                root.localScale = Vector3.one * scale;
                library.Spawn(key, root);
            }
        }
    }
}
