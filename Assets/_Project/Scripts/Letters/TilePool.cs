using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    public class TilePool : MonoBehaviour
    {
        public static TilePool Instance { get; private set; }

        [SerializeField] LetterTile tilePrefab;
        [SerializeField] int prewarm = 64;
        [Tooltip("When more tiles than this are loose, the oldest one is recycled.")]
        [SerializeField] int maxActive = 160;

        readonly Stack<LetterTile> free = new();
        readonly List<LetterTile> active = new();

        public IReadOnlyList<LetterTile> Active => active;

        public static TilePool Ensure()
        {
            if (!Instance) new GameObject("TilePool").AddComponent<TilePool>();
            return Instance;
        }

        void Awake()
        {
            Instance = this;
            if (!tilePrefab) tilePrefab = GameAssets.I.tilePrefab;
            for (int i = 0; i < prewarm; i++)
            {
                var t = Instantiate(tilePrefab, transform);
                t.gameObject.SetActive(false);
                free.Push(t);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public LetterTile Get(char letter)
        {
            if (free.Count == 0 && active.Count >= maxActive) Release(active[0]);
            var t = free.Count > 0 ? free.Pop() : Instantiate(tilePrefab, transform);
            t.gameObject.SetActive(true);
            t.SetLetter(letter);
            active.Add(t);
            return t;
        }

        public void Release(LetterTile t)
        {
            if (!active.Remove(t)) return;
            t.gameObject.SetActive(false);
            free.Push(t);
        }

        public void ReleaseAll()
        {
            for (int i = active.Count - 1; i >= 0; i--) Release(active[i]);
        }

        public void Burst(string letters, Vector3 origin, float force = 4f)
        {
            foreach (char c in letters)
            {
                var dir = (Random.insideUnitSphere + Vector3.up * 1.2f).normalized;
                Get(c).Launch(origin + dir * 0.3f, dir * force);
            }
        }

        public void BurstFrom(IReadOnlyList<Transform> blocks, string word, Vector3 centre, float force = 4f)
        {
            for (int i = 0; i < word.Length; i++)
            {
                var from = i < blocks.Count && blocks[i] ? blocks[i].position : centre;
                var dir = ((from - centre).normalized * 0.8f + Random.insideUnitSphere * 0.4f + Vector3.up * 1.2f).normalized;
                Get(word[i]).Launch(from, dir * force);
            }
        }
    }
}
