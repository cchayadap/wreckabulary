using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>Choose an allowed recipe, reserve its letters, then channel the mode's craft time.</summary>
    [RequireComponent(typeof(LetterInventory))]
    public class Summoner : MonoBehaviour
    {
        [SerializeField] WordDatabase database;
        [SerializeField] float moveScaleWhileSpelling = 0.35f;
        [SerializeField] int maxHints = 3;
        LetterInventory inventory;
        PlayerController controller;
        WordEntry crafting;
        float craftStarted, craftReady;
        public bool IsSpelling { get; private set; }
        public bool IsCrafting => crafting != null;
        public string CraftWord => crafting?.word;
        public float CraftProgress => IsCrafting ? Mathf.Clamp01((Time.time - craftStarted) / Mathf.Max(0.001f, craftReady - craftStarted)) : 0f;
        public List<WordEntry> Ready { get; private set; } = new();
        public List<(WordEntry entry, string missing)> Hints { get; private set; } = new();
        public int Selected { get; private set; }
        public WordEntry SelectedWord => Selected >= 0 && Selected < Ready.Count ? Ready[Selected] : null;
        public event Action<string> Summoned;
        public IReadOnlyList<WordEntry> WordsOverride { get; set; }
        /// <summary>Explicit objective IDs placed as furniture. Null keeps legacy Furniture-only override fixtures.</summary>
        public IReadOnlyCollection<string> ChecklistPlacementWords { get; set; }
        IReadOnlyList<WordEntry> Words => WordsOverride ?? (database ? database : GameAssets.I.words).Words;

        void Awake()
        {
            inventory = GetComponent<LetterInventory>();
            controller = GetComponent<PlayerController>();
            inventory.Changed += InventoryChanged;
        }

        void Start()
        {
            controller.Health.Damaged += (_, _, result) => { if (result.HitStun > 0f || result.BecameDowned || result.BecameEliminated) CancelCraft(); };
            controller.Health.KnockedOut += _ => CancelCraft();
        }

        void InventoryChanged() { if (IsSpelling) Refresh(); }
        void OnDisable() { CancelCraft(); Close(); }

        void Update()
        {
            if (!controller.CanAct || controller.IsDodging) { CancelCraft(); Close(); return; }
            var command = controller.Commands;
            if (IsCrafting)
            {
                if (command.grab || command.spellDown) { CancelCraft(); return; }
                if (Time.time >= craftReady)
                {
                    var entry = crafting;
                    crafting = null;
                    inventory.ReservedCount = 0;
                    controller.MoveScale = 1f;
                    if (SummonEffects.Apply(controller, entry)) Summoned?.Invoke(entry.word);
                    else Refund(entry.word);
                }
                return;
            }
            if (command.spellDown) Open();
            if (!IsSpelling) return;
            if (command.up) Step(-1);
            if (command.down) Step(1);
            if (command.grab) { Close(); return; }
            if (command.spellUp || !command.spellHeld) CraftSelected();
        }

        public void Open()
        {
            if (IsCrafting || !controller.CanAct || controller.IsDodging || controller.Combat.IsChanneling) return;
            IsSpelling = true;
            Selected = 0;
            Refresh();
            controller.MoveScale = moveScaleWhileSpelling;
        }

        public void Close()
        {
            if (!IsSpelling) return;
            IsSpelling = false;
            if (!IsCrafting) controller.MoveScale = 1f;
        }

        void Refresh()
        {
            var keep = SelectedWord;
            Ready = WordSolver.Spellable(Words, inventory.Letters);
            var hints = WordSolver.Hints(Words, inventory.Letters, inventory.Capacity);
            Hints = hints.GetRange(0, Mathf.Min(maxHints, hints.Count));
            Selected = keep != null && Ready.Contains(keep) ? Ready.IndexOf(keep) : 0;
        }

        public void Step(int delta)
        {
            if (Ready.Count == 0) return;
            Selected = ((Selected + delta) % Ready.Count + Ready.Count) % Ready.Count;
        }
        public void Select(int index) { if (index >= 0 && index < Ready.Count) Selected = index; }
        public bool CraftSelected()
        {
            var entry = SelectedWord;
            Close();
            return BeginCraft(entry);
        }

        internal WordEntry ResolveRecipe(WordEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.word)) return null;
            string id = entry.word.ToUpperInvariant();
            foreach (var word in Words) if (word.word == id) return word;
            return null;
        }

        public bool BeginCraft(WordEntry entry)
        {
            entry = ResolveRecipe(entry);
            if (IsCrafting || entry == null || !SummonEffects.CanApply(controller, entry) || !inventory.TrySpend(entry.word)) return false;
            Close();
            crafting = entry;
            inventory.ReservedCount = entry.word.Length;
            craftStarted = Time.time;
            var rules = controller.Health.Rules;
            craftReady = craftStarted + rules.CraftBaseSeconds + rules.CraftPerLetterSeconds * entry.word.Length;
            controller.MoveScale = rules.CraftMoveSpeed;
            return true;
        }

        public void CancelCraft()
        {
            if (!IsCrafting) return;
            var word = crafting.word;
            crafting = null;
            inventory.ReservedCount = 0;
            controller.MoveScale = 1f;
            Refund(word);
        }
        void Refund(string word) { foreach (char letter in word) inventory.TryAdd(letter); }

        public bool Summon(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            foreach (var entry in Words) if (entry.word == word.ToUpperInvariant()) return Summon(entry);
            return false;
        }
        public bool Summon(WordEntry entry)
        {
            entry = ResolveRecipe(entry);
            if (IsCrafting || entry == null || !SummonEffects.CanApply(controller, entry) || !inventory.TrySpend(entry.word)) return false;
            if (!SummonEffects.Apply(controller, entry)) { Refund(entry.word); return false; }
            Summoned?.Invoke(entry.word);
            return true;
        }
    }
}
