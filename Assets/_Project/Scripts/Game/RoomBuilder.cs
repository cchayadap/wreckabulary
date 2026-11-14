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
        [SerializeField] AuthoredHouse authoredHouse;
        Transform geometry;
        GameObject sign;
        readonly List<Smashable> originals = new();
        List<HomeFurniture> design;
        public HouseLayout Layout { get; private set; }
        public IReadOnlyList<Smashable> Originals => originals;
        public AuthoredHouse AuthoredWorld => authoredHouse;
        public string HomeName { get; private set; }

        void Awake()
        {
            Layout = GameConfig.Current.HouseFor(Session.MapId);
            UseHome(Session.Home);
            var previousFurniture = furnitureRoot;
            if (FindAuthoredWorld(Session.MapId))
            {
                if (previousFurniture && previousFurniture != authoredHouse.FurnitureRoot) previousFurniture.gameObject.SetActive(false);
                authoredHouse.gameObject.SetActive(true);
                if (design != null) FurnishFromHome(authoredHouse);
                authoredHouse.PrepareForPlay();
                geometry = authoredHouse.GeometryRoot;
                furnitureRoot = authoredHouse.FurnitureRoot;
                CollectOriginals();
            }
            else
            {
                if (furnitureRoot) furnitureRoot.gameObject.SetActive(false);
                geometry = CreateGeometry(Layout, Session.MapId, transform);
                SetTallWalls(geometry, true);
                BuildFurniture();
            }
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (root.name == "Room" && !root.GetComponentInChildren<AuthoredHouse>(true)) root.SetActive(false);
                if (root.name != "Sign") continue;
                if (Session.MapId is "pinwheel" or "courtyard") sign = root;
                else root.SetActive(false);
            }
            ApplyFog(false);
            var cutaway = GetComponent<StoreyCutaway>();
            if (!cutaway) cutaway = gameObject.AddComponent<StoreyCutaway>();
            cutaway.Configure(Layout, geometry);
            var joins = GetComponent<PlayerJoinManager>();
            if (joins) joins.ConfigureLayout(Layout);
            var camera = FindAnyObjectByType<CameraRig>();
            if (camera) camera.FrameLayout(Layout);
        }

        void UseHome(HomeLayout home)
        {
            design = null; HomeName = null;
            if (home == null || home.Map != Session.MapId) return;
            var designer = new HomeDesigner(GameConfig.Current.Houses, GameConfig.Current.Items);
            if (!designer.TryFurnish(home, out var furniture, out string error))
            {
                Debug.LogWarning("[Wreckabulary] The saved home could not be used, so the house keeps its own furniture. " + error);
                return;
            }
            design = furniture; HomeName = home.Name;
        }

        void FurnishFromHome(AuthoredHouse world)
        {
            var previous = world.FurnitureRoot;
            var root = new GameObject("Original furniture").transform;
            root.SetParent(world.transform, false);
            SpawnHome(root, null);
            if (previous) { previous.gameObject.SetActive(false); Destroy(previous.gameObject); }
            world.Configure(world.MapId, world.GeometryRoot, root);
        }

        void SpawnHome(Transform root, List<Smashable> spawned)
        {
            foreach (var item in design)
            {
                var room = Layout.Room(item.Room);
                var prop = FurnitureCatalog.Spawn(item.Word, new Vector3(item.X, room != null ? room.FloorY : 0f, item.Z), item.Yaw, root);
                Dress(prop, item.Skin);
                spawned?.Add(prop);
            }
        }

        static void Dress(Smashable prop, string skin)
        {
            if (!prop || string.IsNullOrEmpty(skin) || skin == MaterialLibrary.StandardSkin) return;
            var visual = prop.transform.Find("ImportedVisual");
            var materials = MaterialLibrary.Load();
            if (!visual || !materials) return;
            TactileMaterials.Release(visual.gameObject);
            materials.ApplySkin(visual.gameObject, skin);
            foreach (Transform model in visual) TactileMaterials.Apply(model.gameObject);
        }

        bool FindAuthoredWorld(string mapId)
        {
            bool Eligible(AuthoredHouse world) => world && world.IsCurrent && world.Matches(mapId);
            if (authoredHouse && !Eligible(authoredHouse)) authoredHouse.gameObject.SetActive(false);
            AuthoredHouse selected = Eligible(authoredHouse) ? authoredHouse : null;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<AuthoredHouse>(true))
                {
                    if (!selected && Eligible(candidate)) selected = candidate;
                    if (candidate != selected) candidate.gameObject.SetActive(false);
                }
            if (!selected)
            {
                string path = AuthoredHouse.ResourcePath(mapId);
                var prefab = string.IsNullOrEmpty(path) ? null : Resources.Load<GameObject>(path);
                if (prefab && Eligible(prefab.GetComponent<AuthoredHouse>())) selected = Instantiate(prefab, transform).GetComponent<AuthoredHouse>();
            }
            authoredHouse = selected;
            return authoredHouse;
        }

        public void SetAuthoredWorld(AuthoredHouse world)
        {
            authoredHouse = world;
            if (!world) return;
            geometry = world.GeometryRoot;
            furnitureRoot = world.FurnitureRoot;
        }

        void CollectOriginals()
        {
            originals.Clear();
            if (furnitureRoot) originals.AddRange(furnitureRoot.GetComponentsInChildren<Smashable>());
        }

        public AuthoredHouse BuildForAuthoring(HouseLayout layout, string mapId)
        {
            Layout = layout;
            design = null; HomeName = null;
            geometry = CreateGeometry(layout, mapId, transform);
            geometry.name = "Geometry";
            SetTallWalls(geometry, true);
            geometry.gameObject.AddComponent<HousePresentation>();
            BuildFurniture();
            var world = gameObject.AddComponent<AuthoredHouse>();
            world.Configure(mapId, geometry, furnitureRoot);
            return world;
        }

        public void ResetRoom(bool furnish = true)
        {
            SummonedThing.ClearAll();
            World.ClearTransient();
            if (TilePool.Instance) TilePool.Instance.ReleaseAll();
            if (authoredHouse)
            {
                furnitureRoot = authoredHouse.ResetFurniture(furnish);
                CollectOriginals();
                return;
            }
            if (furnitureRoot) { furnitureRoot.gameObject.SetActive(false); Destroy(furnitureRoot.gameObject); }
            originals.Clear();
            furnitureRoot = new GameObject("Original furniture").transform;
            furnitureRoot.SetParent(transform, false);
            if (furnish) BuildFurnitureIntoRoot();
        }

        void BuildFurniture()
        {
            if (furnitureRoot)
            {
                if (Application.isPlaying) Destroy(furnitureRoot.gameObject);
                else DestroyImmediate(furnitureRoot.gameObject);
            }
            furnitureRoot = new GameObject("Original furniture").transform;
            furnitureRoot.SetParent(transform, false);
            BuildFurnitureIntoRoot();
        }

        void BuildFurnitureIntoRoot()
        {
            if (design != null) { SpawnHome(furnitureRoot, originals); return; }
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
            if (EnvironmentLighting.Active) { EnvironmentLighting.Active.ApplyFog(thirdPerson); return; }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color32(0x17, 0x3a, 0x3d, 0xff);
            RenderSettings.fogStartDistance = thirdPerson ? 48f : 60f;
            RenderSettings.fogEndDistance = thirdPerson ? 100f : 130f;
        }

        public static string StoreyName(int storey) => "Storey " + storey;

        public static int SurroundingPlants(float width, float depth) => Mathf.Max(16, Mathf.RoundToInt((width + depth) * 2f / 7f));

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
                    var holes = Layout.Stairs.Where(s => s.Upper == r.Name).Select(s => Rect.MinMaxRect(s.MinX, s.MinZ, s.MaxX, s.MaxZ));
                    var (floor, floorColour) = FloorOf(r);
                    foreach (var piece in Slab(r, holes))
                        Surface(r.Name + " floor", storeys[storey], new Vector3(piece.center.x, r.FloorY - .12f, piece.center.y),
                            new Vector3(piece.width, .24f, piece.height), floor, floorColour);
                    AddEdge(edges, storey, r.FloorY, true, r.MinX, r.MinZ, r.MaxZ);
                    AddEdge(edges, storey, r.FloorY, true, r.MaxX, r.MinZ, r.MaxZ);
                    AddEdge(edges, storey, r.FloorY, false, r.MinZ, r.MinX, r.MaxX);
                    AddEdge(edges, storey, r.FloorY, false, r.MaxZ, r.MinX, r.MaxX);
                    if (includeExtras && !r.Outdoor && !Bare(r.Name)) BuildRug(r, storey);
                }
                foreach (var edge in edges.Values) BuildEdge(edge);
                foreach (var s in Layout.Stairs) BuildStairs(s);
                for (int s = 1; s < storeys.Length; s++)
                    foreach (var r in storeys[s].GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
                if (!includeExtras) return;
                BuildSurroundings();
                if (mapId == "pinwheel" && Layout.Room("Playroom") is { } playroom) BuildBalcony(playroom, storeys[Layout.StoreyOf(playroom)]);
                foreach (var r in Layout.Rooms)
                {
                    var group = storeys[Layout.StoreyOf(r)];
                    switch (RoomKind(r.Name))
                    {
                        case "FountainSquare": BuildFountainSquare(r, group); break;
                        case "PoolLawn": BuildPaddlingPool(r, group); break;
                        case "Orchard": BuildOrchard(r, group); break;
                        case "Roof": BuildWashingLines(r, group); break;
                        case "RoofGarden": BuildPlanters(r, group); break;
                    }
                }
            }

            void BuildBalcony(RoomBox room, Transform group)
            {
                float x = room.MaxX - .75f, y = room.FloorY;
                Surface("Playroom balcony", group, new Vector3(x, y + 1.55f, room.MaxZ - 1.5f), new Vector3(1.5f, .3f, 3f), Surfaces.Kind.Wood, new Color(.7f, .52f, .36f));
                var ramp = Surface("Balcony ramp", group, new Vector3(x, y + .76f, room.MaxZ - 3.98f), new Vector3(1.5f, .15f, 2.7f), Surfaces.Kind.Wood, new Color(.65f, .47f, .33f), turned: true);
                ramp.transform.rotation = Quaternion.Euler(-40f, 0f, 0f);
                Decor("Environment/Railing_2m", group, new Vector3(room.MaxX - 1.5f, y + 1.7f, room.MaxZ - 1.5f), 1f, 90f);
            }

            void BuildFountainSquare(RoomBox room, Transform group)
            {
                var centre = Centre(room);
                float width = room.MaxX - room.MinX, depth = room.MaxZ - room.MinZ;
                var path = new Color(.75f, .72f, .59f);
                Surface("Courtyard path east-west", group, centre + Vector3.up * .008f, new Vector3(width, .018f, 2.5f), Surfaces.Kind.Plaster, path, false);
                Surface("Courtyard path north-south", group, centre + Vector3.up * .009f, new Vector3(2.5f, .018f, depth), Surfaces.Kind.Plaster, path, false);
                var stone = Surfaces.Hex(0xC2B8A3);
                const float Basin = 1.6f;
                foreach (float side in new[] { -1f, 1f })
                {
                    Surface("Fountain basin", group, centre + new Vector3(0f, .3f, side * Basin), new Vector3(Basin * 2f + .3f, .6f, .3f), Surfaces.Kind.Plaster, stone);
                    Surface("Fountain basin", group, centre + new Vector3(side * Basin, .3f, 0f), new Vector3(.3f, .6f, Basin * 2f - .3f), Surfaces.Kind.Plaster, stone);
                }
                Surface("Fountain water", group, centre + Vector3.up * .42f, new Vector3(Basin * 2f - .3f, .06f, Basin * 2f - .3f), Surfaces.Kind.Tile, Surfaces.Hex(0x7DB4C6), false);
                Surface("Fountain column", group, centre + Vector3.up * .9f, new Vector3(.5f, 1.8f, .5f), Surfaces.Kind.Plaster, stone);
                Surface("Fountain bowl", group, centre + Vector3.up * 1.6f, new Vector3(1.3f, .14f, 1.3f), Surfaces.Kind.Plaster, stone, false);
                Decor("Items/PLANT", group, centre + new Vector3(-width * .375f, 0f, depth * .375f), 1.4f);
                Decor("Items/PLANT", group, centre + new Vector3(width * .375f, 0f, -depth * .375f), 1.4f);
            }

            void BuildPaddlingPool(RoomBox room, Transform group)
            {
                var centre = Centre(room);
                var rim = Surfaces.Hex(0x4F8FC9);
                const float Width = 4.4f, Depth = 2.6f;
                Surface("Paddling pool water", group, centre + Vector3.up * .03f, new Vector3(Width, .06f, Depth), Surfaces.Kind.Tile, Surfaces.Hex(0x9FD3E3), false);
                foreach (float side in new[] { -1f, 1f })
                {
                    Surface("Paddling pool rim", group, centre + new Vector3(0f, .12f, side * Depth * .5f), new Vector3(Width + .2f, .24f, .2f), Surfaces.Kind.Plaster, rim, false);
                    Surface("Paddling pool rim", group, centre + new Vector3(side * Width * .5f, .12f, 0f), new Vector3(.2f, .24f, Depth), Surfaces.Kind.Plaster, rim, false);
                }
            }

            void BuildOrchard(RoomBox room, Transform group)
            {
                for (float x = room.MinX + 1.1f; x <= room.MaxX - 1.1f; x += 4f) Tree(group, new Vector3(x, room.FloorY, room.MaxZ - 1.1f), x * 37f);
                for (float z = room.MaxZ - 5.1f; z >= room.MinZ + 1.1f; z -= 4f) Tree(group, new Vector3(room.MinX + 1.1f, room.FloorY, z), z * 41f);
            }

            void Tree(Transform group, Vector3 at, float yaw)
            {
                var tree = Decor("Items/PLANT", group, at, 2.4f, yaw);
                if (!tree) return;
                foreach (var collider in tree.GetComponentsInChildren<Collider>(true))
                    if (Application.isPlaying) Object.Destroy(collider);
                    else Object.DestroyImmediate(collider);
            }

            void BuildWashingLines(RoomBox room, Transform group)
            {
                var centre = Centre(room);
                float width = room.MaxX - room.MinX;
                var post = new Color(.45f, .32f, .22f);
                var laundry = new[] { Surfaces.Hex(0xE8EEF2), Surfaces.Hex(0xE7B8A8), Surfaces.Hex(0xA9C7E0), Surfaces.Hex(0xF0D58A) };
                int piece = 0;
                foreach (float side in new[] { -1f, 1f })
                    for (int line = -1; line <= 1; line++)
                    {
                        float near = centre.x + side * width * .15f, far = centre.x + side * width * .38f, z = centre.z + line * 3f;
                        foreach (float x in new[] { near, far })
                            Surface("Washing line post", group, new Vector3(x, room.FloorY + 1.05f, z), new Vector3(.08f, 2.1f, .08f), Surfaces.Kind.Wood, post, false);
                        Surface("Washing line", group, new Vector3((near + far) * .5f, room.FloorY + 2f, z), new Vector3(Mathf.Abs(far - near), .02f, .02f), Surfaces.Kind.Plaster, laundry[0], false);
                        for (float x = Mathf.Min(near, far) + .9f; x < Mathf.Max(near, far) - .5f; x += 1.3f)
                            Surface("Laundry", group, new Vector3(x, room.FloorY + 1.62f, z), new Vector3(.55f, .72f, .03f), Surfaces.Kind.Plaster, laundry[piece++ % laundry.Length], false);
                    }
            }

            void BuildPlanters(RoomBox room, Transform group)
            {
                foreach (float x in new[] { room.MinX + 1.2f, room.MaxX - 1.2f })
                    foreach (float z in new[] { room.MinZ + 1.2f, room.MaxZ - 1.2f })
                    {
                        Surface("Planter", group, new Vector3(x, room.FloorY + .2f, z), new Vector3(1.6f, .4f, 1.6f), Surfaces.Kind.Wood, Surfaces.Hex(0x9C7A5B), false);
                        Tree(group, new Vector3(x, room.FloorY + .4f, z), x * 29f + z);
                    }
            }

            static Vector3 Centre(RoomBox room) => new((room.MinX + room.MaxX) * .5f, room.FloorY, (room.MinZ + room.MaxZ) * .5f);

            List<float> storeyFloors;
            Transform[] storeys;

            static readonly Color Plaster = Surfaces.Hex(0xEDDFC4), Trim = Surfaces.Hex(0xB58760);

            static string RoomKind(string room) => room.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');

            static bool Bare(string room) => RoomKind(room) is "Garage" or "Workshop" or "Storeroom" or "Shed";

            static (Surfaces.Kind kind, Color colour) FloorOf(RoomBox room) => RoomKind(room.Name) switch
            {
                "Garden" or "Orchard" or "PoolLawn" or "Yard" or "RoofGarden" => (Surfaces.Kind.Lawn, Surfaces.Hex(0x86A17A)),
                "Kitchen" or "Diner" or "CafeKitchen" => (Surfaces.Kind.Checker, Color.white),
                "Bathroom" => (Surfaces.Kind.Tile, Color.white),
                "Garage" or "Workshop" or "Storeroom" => (Surfaces.Kind.Plaster, Surfaces.Hex(0xA7A39A)),
                "Study" or "Library" => (Surfaces.Kind.Planks, Surfaces.Hex(0xB68E6B)),
                "Shed" or "Attic" => (Surfaces.Kind.Planks, Surfaces.Hex(0xA1805F)),
                "Walkway" => (Surfaces.Kind.Tile, Surfaces.Hex(0xE3D5BC)),
                _ when room.Outdoor => (Surfaces.Kind.Plaster, Surfaces.Hex(0xC9C0AC)),
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
                float halfX = (maxX - minX) * .5f, halfZ = (maxZ - minZ) * .5f, lawnSize = Mathf.Max(240f, Mathf.Max(halfX, halfZ) * 8f);
                var lawn = Surface("Lawn", ground, new Vector3(cx, -.85f, cz), new Vector3(lawnSize, 1f, lawnSize), Surfaces.Kind.Lawn, Surfaces.Hex(0x789775), false);
                foreach (var piece in new[] { plinth, lawn }) piece.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                int plants = SurroundingPlants(maxX - minX, maxZ - minZ);
                for (int n = 0; n < plants; n++)
                {
                    float angle = (n + .5f) / plants * Mathf.PI * 2f, cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                    float wall = Mathf.Min(halfX / Mathf.Max(Mathf.Abs(cos), .001f), halfZ / Mathf.Max(Mathf.Abs(sin), .001f));
                    float radius = wall + 4f + n % 3 * 1.5f;
                    Decor("Items/PLANT", ground, new Vector3(cx + cos * radius, -.35f, cz + sin * radius), 1.8f + n % 3 * .35f, n * 53f);
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
                    bool before = edge.Vertical ? Outdoors(edge.Storey, edge.Fixed - .1f, mid) : Outdoors(edge.Storey, mid, edge.Fixed - .1f);
                    bool after = edge.Vertical ? Outdoors(edge.Storey, edge.Fixed + .1f, mid) : Outdoors(edge.Storey, mid, edge.Fixed + .1f);
                    tall.Outside = before || after;
                    tall.Fence = before && after;
                    tall.Apply(false);
                }
            }

            bool Outdoors(int storey, float x, float z) => !Layout.Rooms.Any(r => !r.Outdoor && Layout.StoreyOf(r) == storey
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
                if (!solid)
                {
                    var collider = go.GetComponent<Collider>();
                    collider.enabled = false;
                    if (Application.isPlaying) Object.Destroy(collider);
                    else Object.DestroyImmediate(collider);
                }
                return go;
            }

            Transform Decor(string key, Transform group, Vector3 at, float scale, float yaw = 0f)
            {
                var library = ModelLibrary.Load();
                if (!library || !library.Find(key)) return null;
                var root = new GameObject(key).transform;
                root.SetParent(group, false);
                root.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
                root.localScale = Vector3.one * scale;
                library.Spawn(key, root);
                return root;
            }
        }
    }
}
