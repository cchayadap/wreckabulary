using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>Builds the selected shared house layout, including open doorways, spawns and original furniture.</summary>
    [DefaultExecutionOrder(-150)]
    public class RoomBuilder : MonoBehaviour
    {
        [SerializeField] Transform furnitureRoot;
        Transform geometry;
        readonly List<Smashable> originals = new();
        public HouseLayout Layout { get; private set; }
        public IReadOnlyList<Smashable> Originals => originals;

        void Awake()
        {
            Layout = GameConfig.Current.HouseFor(Session.MapId);
            var oldRoom = GameObject.Find("Room");
            if (oldRoom) oldRoom.SetActive(false);
            if (furnitureRoot) furnitureRoot.gameObject.SetActive(false);
            geometry = new GameObject(Layout.Name).transform;
            geometry.SetParent(transform, false);
            BuildGeometry();
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

        void BuildGeometry()
        {
            Color[] floors = { new(.76f, .62f, .45f), new(.66f, .73f, .73f), new(.83f, .74f, .59f), new(.64f, .63f, .75f), new(.73f, .55f, .47f) };
            var edges = new Dictionary<string, Edge>();
            for (int i = 0; i < Layout.Rooms.Count; i++)
            {
                var r = Layout.Rooms[i];
                bool garden = r.Name == "Garden";
                Block(r.Name + " floor", new Vector3((r.MinX + r.MaxX) * .5f, r.FloorY - .12f, (r.MinZ + r.MaxZ) * .5f),
                    new Vector3(r.MaxX - r.MinX, .24f, r.MaxZ - r.MinZ), garden ? new Color(.41f, .56f, .35f) : floors[i % floors.Length]);
                AddEdge(edges, true, r.MinX, r.MinZ, r.MaxZ);
                AddEdge(edges, true, r.MaxX, r.MinZ, r.MaxZ);
                AddEdge(edges, false, r.MinZ, r.MinX, r.MaxX);
                AddEdge(edges, false, r.MaxZ, r.MinX, r.MaxX);
                // A single imported rug per indoor room gives material detail without tiling hundreds of meshes.
                if (!garden) Decor("Environment/Arena_Rug", new Vector3((r.MinX + r.MaxX) * .5f, r.FloorY + .006f, (r.MinZ + r.MaxZ) * .5f), .8f);
            }
            foreach (var edge in edges.Values) BuildEdge(edge);
            // The pinwheel balcony is a reachable elevated route; the courtyard is intentionally open and flat.
            if (Session.MapId == "pinwheel")
            {
                Block("Playroom balcony", new Vector3(3.25f, 1.55f, 2.5f), new Vector3(1.5f, .3f, 3f), new Color(.7f, .52f, .36f));
                var ramp = Block("Balcony ramp", new Vector3(3.25f, .76f, .02f), new Vector3(1.5f, .15f, 2.7f), new Color(.65f, .47f, .33f));
                ramp.transform.rotation = Quaternion.Euler(-40f, 0f, 0f);
                Decor("Environment/Railing_2m", new Vector3(2.5f, 1.7f, 2.5f), 1f, 90f);
            }
            else
            {
                Block("Courtyard path east-west", new Vector3(0f, .008f, 0f), new Vector3(12f, .018f, 2.5f), new Color(.75f, .72f, .59f), false);
                Block("Courtyard path north-south", new Vector3(0f, .009f, 0f), new Vector3(2.5f, .018f, 12f), new Color(.75f, .72f, .59f), false);
                Decor("Items/PLANT", new Vector3(-4.5f, 0f, 4.5f), 1.4f);
                Decor("Items/PLANT", new Vector3(4.5f, 0f, -4.5f), 1.4f);
            }
        }

        sealed class Edge
        {
            public bool Vertical;
            public float Fixed;
            public readonly List<Vector2> Intervals = new();
        }

        static void AddEdge(Dictionary<string, Edge> all, bool vertical, float fixedAt, float min, float max)
        {
            string key = (vertical ? "x" : "z") + fixedAt.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!all.TryGetValue(key, out var edge)) all[key] = edge = new Edge { Vertical = vertical, Fixed = fixedAt };
            edge.Intervals.Add(new Vector2(min, max));
        }

        void BuildEdge(Edge edge)
        {
            var cuts = edge.Intervals.SelectMany(i => new[] { i.x, i.y }).ToList();
            var openings = Layout.Doors.Where(d => Mathf.Abs((edge.Vertical ? d.X : d.Z) - edge.Fixed) < .01f)
                .Select(d => new Vector2((edge.Vertical ? d.Z : d.X) - d.Width * .5f, (edge.Vertical ? d.Z : d.X) + d.Width * .5f)).ToArray();
            cuts.AddRange(openings.SelectMany(i => new[] { i.x, i.y }));
            cuts = cuts.Distinct().OrderBy(v => v).ToList();
            for (int i = 0; i < cuts.Count - 1; i++)
            {
                float mid = (cuts[i] + cuts[i + 1]) * .5f;
                if (!edge.Intervals.Any(range => mid >= range.x && mid <= range.y) || openings.Any(range => mid > range.x && mid < range.y)) continue;
                float length = cuts[i + 1] - cuts[i];
                var wall = Block("Wall", edge.Vertical ? new Vector3(edge.Fixed, .55f, mid) : new Vector3(mid, .55f, edge.Fixed),
                    edge.Vertical ? new Vector3(.2f, 1.1f, length) : new Vector3(length, 1.1f, .2f), new Color(.66f, .7f, .65f));
                // Cutaway rendering keeps players visible; the full-height collider enforces the doorway route.
                var collider = wall.GetComponent<BoxCollider>();
                collider.size = new Vector3(1f, 3f, 1f);
                collider.center = new Vector3(0f, 1f, 0f);
            }
        }

        GameObject Block(string name, Vector3 at, Vector3 scale, Color colour, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(geometry, false);
            go.transform.position = at;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = GameAssets.I.Tinted(colour);
            if (!solid) Destroy(go.GetComponent<Collider>());
            return go;
        }

        void Decor(string key, Vector3 at, float scale, float yaw = 0f)
        {
            var library = ModelLibrary.Load();
            if (!library || !library.Find(key)) return;
            var root = new GameObject(key).transform;
            root.SetParent(geometry, false);
            root.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            root.localScale = Vector3.one * scale;
            library.Spawn(key, root);
        }
    }
}
