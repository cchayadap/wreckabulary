using System;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public class LetterInventory : MonoBehaviour
    {
        [SerializeField] float pickupRadius = 0.9f;

        readonly List<char> letters = new();
        static readonly Collider[] Hits = new Collider[32];
        PlayerController controller;

        public IReadOnlyList<char> Letters => letters;
        public int BaseCapacity => controller && controller.Health ? controller.Health.Rules.MaxLetters : Match.Rules.MaxLetters;
        public int BonusRows { get; private set; }
        public int Capacity => BagRows.Capacity(BaseCapacity, BonusRows);
        public int ShownCells => BagRows.ShownCells(Capacity, TotalCount);
        public int ShownRows => BagRows.RowsFor(ShownCells);
        public bool CanAddRow => BagRows.CanAddRow(BaseCapacity, BonusRows);
        public int Count => letters.Count;
        public int ReservedCount { get; set; }
        public int TotalCount => Count + ReservedCount;
        public bool IsEmpty => letters.Count == 0;
        public bool IsFull => !BagRows.CanPickUp(TotalCount, Capacity);

        public bool Collects { get; set; } = true;

        public static bool FreeSpelling { get; set; }
        public bool SpellsFree => FreeSpelling && controller && controller.Binding is not BotBinding;

        public event Action Changed;
        public static event Action<LetterInventory> RowsChanged;
        static readonly List<LetterInventory> Live = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ForgetFreeSpelling()
        {
            FreeSpelling = false;
            Live.Clear();
            RowsChanged = null;
        }

        void Awake() => controller = GetComponent<PlayerController>();

        void OnEnable() { if (!Live.Contains(this)) Live.Add(this); }

        void OnDestroy() => Live.Remove(this);

        public bool AddBonusRow()
        {
            if (!CanAddRow) return false;
            BonusRows = BagRows.AddRow(BaseCapacity, BonusRows);
            Changed?.Invoke();
            RowsChanged?.Invoke(this);
            return true;
        }

        public void ClearBonusRows()
        {
            if (BonusRows == 0) return;
            BonusRows = 0;
            Changed?.Invoke();
            RowsChanged?.Invoke(this);
        }

        public static void ClearAllBonusRows()
        {
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                if (Live[i]) Live[i].ClearBonusRows();
                else Live.RemoveAt(i);
            }
        }

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

        public void Set(string newLetters)
        {
            letters.Clear();
            foreach (char c in newLetters)
                if (TotalCount < Capacity && char.ToUpperInvariant(c) >= 'A' && char.ToUpperInvariant(c) <= 'Z') letters.Add(char.ToUpperInvariant(c));
            Changed?.Invoke();
        }

        public bool TrySpend(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            if (SpellsFree) return true;
            if (!WordSolver.CanSpell(WordSolver.Count(letters), word)) return false;
            foreach (char c in word) letters.Remove(char.ToUpperInvariant(c));
            Changed?.Invoke();
            return true;
        }

        public bool DropLetter(char c, Vector3 from, Vector3 direction)
        {
            c = char.ToUpperInvariant(c);
            int at = letters.IndexOf(c);
            if (at < 0) return false;
            letters.RemoveAt(at);
            var pool = TilePool.Instance;
            if (pool) pool.Get(c).Launch(from + Vector3.up * 1.2f, direction * 4f, this);
            Changed?.Invoke();
            return true;
        }

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
