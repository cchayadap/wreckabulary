using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>
    /// Moving day: when the room runs low on letters, labelled boxes drop in. Smash one and it
    /// bursts into the letters on its label. During the collapse, boxes rain down and hurt.
    /// </summary>
    public class DeliverySpawner : MonoBehaviour
    {
        [SerializeField] string[] words =
        {
            "BOX", "WAX", "PIZZA", "QUILT", "SOCKS", "SPOONS", "KETTLE", "TOWELS", "CANDLE", "DISHES",
            "BRUSH", "GAMES", "SHOES", "JUICE", "JAM", "FORKS", "COMICS", "ZIPPER", "GLOVES", "WIGS",
        };
        [SerializeField] Vector2 areaX = new(-6.5f, 6.5f);
        [SerializeField] Vector2 areaZ = new(-4.2f, 3.2f);
        [SerializeField] float dropHeight = 6f;
        const float StairClearance = .85f;
        [Tooltip("Boxes only arrive while fewer letters than this are in play.")]
        [SerializeField] int minLettersInPlay = 26;
        [SerializeField] float interval = 4f;
        [SerializeField] float collapseInterval = 1.1f;

        public bool Running { get; set; } = true;
        public bool Collapsing { get; private set; }

        float next;

        public void ResetDrops()
        {
            Collapsing = false;
            next = Time.time + interval;
        }

        public void StartCollapse() => Collapsing = true;

        void Update()
        {
            if (!Running || Time.time < next) return;
            next = Time.time + (Collapsing ? collapseInterval : interval);
            if (!Collapsing && LettersInPlay() >= minLettersInPlay) return;
            Drop(Collapsing);
        }

        public HouseLayout Layout { get; set; }
        public System.Func<string, bool> RoomOpen { get; set; }

        public Smashable Drop(bool hazard)
        {
            var word = words[Random.Range(0, words.Length)];
            var box = CreateBox(word, DropPoint());
            if (hazard) ThrowTracker.Attach(box.gameObject, null, 4f, 3.5f);
            return box;
        }

        Vector3 DropPoint()
        {
            var floors = Layout?.StoreyFloors();
            if (floors == null || floors.Count < 2)
                return new Vector3(Random.Range(areaX.x, areaX.y), dropHeight, Random.Range(areaZ.x, areaZ.y));
            var rooms = Layout.Rooms.Where(r => r.MaxX - r.MinX > 2.5f && r.MaxZ - r.MinZ > 2.5f && (RoomOpen == null || RoomOpen(r.Name))).ToList();
            if (rooms.Count == 0) rooms = Layout.Rooms.Where(r => r.MaxX - r.MinX > 2.5f && r.MaxZ - r.MinZ > 2.5f).ToList();
            if (rooms.Count == 0) rooms = Layout.Rooms.ToList();
            float total = rooms.Sum(r => (r.MaxX - r.MinX) * (r.MaxZ - r.MinZ));
            bool NearStairs(float px, float pz) => Layout.Stairs.Any(s =>
                px > s.MinX - StairClearance && px < s.MaxX + StairClearance && pz > s.MinZ - StairClearance && pz < s.MaxZ + StairClearance);
            RoomBox room = rooms[0];
            float x = 0f, z = 0f;
            bool clear = false;
            for (int attempt = 0; attempt < 12 && !clear; attempt++)
            {
                float pick = Random.Range(0f, total);
                room = rooms.FirstOrDefault(r => (pick -= (r.MaxX - r.MinX) * (r.MaxZ - r.MinZ)) <= 0f) ?? rooms[rooms.Count - 1];
                float inset = Mathf.Min(1f, (room.MaxX - room.MinX) * .4f), insetZ = Mathf.Min(1f, (room.MaxZ - room.MinZ) * .4f);
                x = Random.Range(room.MinX + inset, room.MaxX - inset);
                z = Random.Range(room.MinZ + insetZ, room.MaxZ - insetZ);
                clear = !NearStairs(x, z);
            }
            if (!clear)
            {
                var spots = new List<(RoomBox room, float x, float z)>();
                foreach (var r in rooms)
                {
                    float inset = Mathf.Min(1f, (r.MaxX - r.MinX) * .4f), insetZ = Mathf.Min(1f, (r.MaxZ - r.MinZ) * .4f);
                    for (float gx = r.MinX + inset; gx <= r.MaxX - inset; gx += .5f)
                        for (float gz = r.MinZ + insetZ; gz <= r.MaxZ - insetZ; gz += .5f)
                            if (!NearStairs(gx, gz)) spots.Add((r, gx, gz));
                }
                if (spots.Count > 0) (room, x, z) = spots[Random.Range(0, spots.Count)];
            }
            int storey = Layout.StoreyOf(room);
            float ceiling = storey + 1 < floors.Count ? floors[storey + 1] - .24f : float.PositiveInfinity;
            return new Vector3(x, Mathf.Min(room.FloorY + dropHeight, ceiling - 1f), z);
        }

        public static Smashable CreateBox(string word, Vector3 position)
        {
            var go = new GameObject($"Box ({word})");
            go.transform.SetParent(World.Transient, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f));

            var size = new Vector3(0.95f, 0.7f, 0.95f) * (word.Length > 5 ? 1.15f : 1f);
            var block = LetterBlocks.Create(word, size, GameAssets.I.cardboard, go.transform, true);
            block.transform.localPosition = Vector3.up * size.y * 0.5f;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var smash = go.AddComponent<Smashable>();
            smash.Init(word, 8f);
            return smash;
        }

        static int LettersInPlay()
        {
            int n = TilePool.Instance ? TilePool.Instance.Active.Count : 0;
            foreach (var s in FindObjectsByType<Smashable>()) n += s.Word.Length;
            foreach (var p in World.Players) n += p.Inventory.Count;
            return n;
        }
    }
}
