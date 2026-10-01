using UnityEngine;

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

        public Smashable Drop(bool hazard)
        {
            var word = words[Random.Range(0, words.Length)];
            var pos = new Vector3(Random.Range(areaX.x, areaX.y), dropHeight, Random.Range(areaZ.x, areaZ.y));
            var box = CreateBox(word, pos);
            if (hazard) ThrowTracker.Attach(box.gameObject, null, 4f, 3.5f);
            return box;
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
