using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    public class LetterInventory : MonoBehaviour
    {
        [SerializeField] float pickupRadius = 0.9f;

        readonly List<char> letters = new();
        static readonly Collider[] Hits = new Collider[32];
        PlayerController controller;

        public IReadOnlyList<char> Letters => letters;
        public int Capacity => controller && controller.Health ? controller.Health.Rules.MaxLetters : Match.Rules.MaxLetters;
        public int Count => letters.Count;
        public int ReservedCount { get; set; }
        public int TotalCount => Count + ReservedCount;
        public bool IsEmpty => letters.Count == 0;
        public bool IsFull => TotalCount >= Capacity;

        /// <summary>Off for the tutorial dummy, so it doesn't hoover up loose letters.</summary>
        public bool Collects { get; set; } = true;

        public event Action Changed;

        void Awake() => controller = GetComponent<PlayerController>();

        void FixedUpdate()
        {
            if (!Collects || IsFull || (controller && (controller.IsKnockedOut || controller.IsHeld))) return;

            int n = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.5f, pickupRadius, Hits,
                                                  World.TileMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n && !IsFull; i++)
            {
                var rb = Hits[i].attachedRigidbody;
                if (rb && rb.gameObject.activeSelf && rb.TryGetComponent(out LetterTile tile) &&
                    tile.CanBeCollectedBy(this) && TryAdd(tile.Letter))
                    tile.Collect();
            }
        }

        public bool TryAdd(char c)
        {
            c = char.ToUpperInvariant(c);
            if (IsFull || c < 'A' || c > 'Z') return false;
            letters.Add(c);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Replaces the held letters (round start, tests).</summary>
        public void Set(string newLetters)
        {
            letters.Clear();
            foreach (char c in newLetters)
                if (TotalCount < Capacity && char.ToUpperInvariant(c) >= 'A' && char.ToUpperInvariant(c) <= 'Z') letters.Add(char.ToUpperInvariant(c));
            Changed?.Invoke();
        }

        /// <summary>Removes the letters of a word. Returns false if they are not all held.</summary>
        public bool TrySpend(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            if (!WordSolver.CanSpell(WordSolver.Count(letters), word)) return false;
            foreach (char c in word) letters.Remove(char.ToUpperInvariant(c));
            Changed?.Invoke();
            return true;
        }

        /// <summary>Knocks random letters loose into the world.</summary>
        public void DropRandom(int count, Vector3 from, Vector3 hitDirection)
        {
            var pool = TilePool.Instance;
            for (int i = 0; i < count && letters.Count > 0; i++)
            {
                int idx = UnityEngine.Random.Range(0, letters.Count);
                char c = letters[idx];
                letters.RemoveAt(idx);
                var dir = (hitDirection.normalized + Vector3.up * 1.2f + UnityEngine.Random.insideUnitSphere * 0.6f).normalized;
                if (pool) pool.Get(c).Launch(from + Vector3.up * 1.3f, dir * 5f, this);
            }
            Changed?.Invoke();
        }
    }
}
