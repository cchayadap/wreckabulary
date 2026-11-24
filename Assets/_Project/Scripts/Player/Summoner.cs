using System;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    [RequireComponent(typeof(LetterInventory))]
    public class Summoner : MonoBehaviour
    {
        [SerializeField] WordDatabase database;
        [SerializeField] float moveScaleWhileSpelling = 0.35f;
        [SerializeField] int maxHints = 3;
        LetterInventory inventory;
        PlayerController controller;
        WordEntry crafting;
        bool craftFree;
        float craftStarted, craftReady;
        public bool IsSpelling { get; private set; }
        public bool IsCrafting => crafting != null;
        internal bool IsDisabling { get; private set; }
        public string CraftWord => crafting?.word;
        public float CraftProgress => IsCrafting ? Mathf.Clamp01((Time.time - craftStarted) / Mathf.Max(0.001f, craftReady - craftStarted)) : 0f;
        public List<WordEntry> Ready { get; private set; } = new();
        public List<(WordEntry entry, string missing)> Hints { get; private set; } = new();
        public int Selected { get; private set; }
        public WordEntry SelectedWord => Selected >= 0 && Selected < Ready.Count ? Ready[Selected] : null;
        public event Action<string> Summoned;
        public IReadOnlyList<WordEntry> WordsOverride { get; set; }
        /// <summary>
        /// Signal-only words: crafting one fires Summoned without requiring an items.json recipe.
        /// Used by Boss Fight so CALC / ULA / TOR can crack shields. Null in every other mode.
        /// </summary>
        // @trace MECH-002, MECH-003
        public IReadOnlyCollection<string> SignalWords { get; set; }
        bool IsSignal(WordEntry entry)
        {
            if (SignalWords == null || entry == null) return false;
            string id = entry.word.ToUpperInvariant();
            foreach (var word in SignalWords)
                if (word == id) return true;
            return false;
        }
        /// <summary>True for words that craft as mode signals (e.g. boss shield words) with no items.json recipe.</summary>
        // @trace MECH-003
        public bool IsSignalWord(string word)
        {
            if (SignalWords == null || string.IsNullOrEmpty(word)) return false;
            string id = word.ToUpperInvariant();
            foreach (var w in SignalWords) if (w == id) return true;
            return false;
        }

        /// <summary>
        /// Single craft gate used by the typed composer and the word wheel. Signal words skip
        /// the items.json / gear-slot checks but still respect health, dodge and channel state.
        /// </summary>
        // @trace MECH-003
        public bool CanCraft(WordEntry entry)
        {
            entry = ResolveRecipe(entry);
            if (entry == null || IsCrafting || !controller.CanAct || controller.IsDodging || controller.Combat.IsChanneling) return false;
            return IsSignal(entry) || SummonEffects.CanApply(controller, entry);
        }

        public IReadOnlyCollection<string> ChecklistPlacementWords { get; set; }
        public bool Typed { get; set; }
        public string WantedWord => HouseModeDirectors.WantedWord(controller);
        string objectivesGoal;
        IReadOnlyCollection<string> objectivesFrom, objectives;

        public IReadOnlyCollection<string> Objectives
        {
            get
            {
                string goal = WantedWord;
                var placement = ChecklistPlacementWords;
                if (goal != objectivesGoal || !ReferenceEquals(placement, objectivesFrom))
                {
                    objectivesGoal = goal;
                    objectivesFrom = placement;
                    objectives = SpellChoice.WithGoal(placement, goal);
                }
                return objectives;
            }
        }
        public IReadOnlyList<WordEntry> Words => WordsOverride ?? (database ? database : GameAssets.I.words).Words;

        public SpellNeeds Needs
        {
            get
            {
                bool armed = false, guarded = false;
                var combat = controller ? controller.Combat : null;
                if (combat)
                {
                    Carry(combat.Weapon, ref armed, ref guarded);
                    Carry(combat.StoredGear, ref armed, ref guarded);
                }
                var health = controller ? controller.Health : null;
                return new SpellNeeds(armed, guarded, health && health.IsAlive && health.Fraction <= SpellNeeds.HurtAt);
            }
        }

        static void Carry(HeldWeapon gear, ref bool armed, ref bool guarded)
        {
            if (!gear) return;
            var role = gear.Definition != null ? SpellChoice.RoleOf(gear.Definition) : gear.Shield != null ? SpellRole.Shield : SpellRole.Weapon;
            armed |= SpellChoice.Fights(role);
            guarded |= role == SpellRole.Shield;
        }

        public List<WordEntry> Options() => Spellable(Needs, Objectives);

        List<WordEntry> Spellable(SpellNeeds needs, IReadOnlyCollection<string> wanted) => inventory.SpellsFree
            ? SpellChoice.Order(Words, w => w.word, w => WordSolver.RoleOf(w, wanted), needs)
            : WordSolver.Spellable(Words, inventory.Letters, needs, wanted);

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
        void OnEnable() => IsDisabling = false;
        void OnDisable()
        {
            IsDisabling = true;
            CancelCraft();
            Close();
        }

        void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (!controller.CanAct || controller.IsDodging) { CancelCraft(); Close(); return; }
            var command = controller.Commands;
            if (IsCrafting)
            {
                if (command.grab || command.spellDown) { CancelCraft(); return; }
                if (Time.time >= craftReady)
                {
                    var entry = crafting;
                    bool free = craftFree;
                    crafting = null;
                    craftFree = false;
                    inventory.ReservedCount = 0;
                    controller.MoveScale = 1f;
                    // @trace MECH-003
                    if (IsSignal(entry)) Summoned?.Invoke(entry.word);
                    else if (SummonEffects.Apply(controller, entry)) Summoned?.Invoke(entry.word);
                    else Refund(entry.word, free);
                }
                return;
            }
            if (command.spellDown) Open();
            if (!IsSpelling || Typed) return;
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
            var needs = Needs;
            var wanted = Objectives;
            Ready = Spellable(needs, wanted);
            var hints = inventory.SpellsFree ? new List<(WordEntry entry, string missing)>()
                : WordSolver.Hints(Words, inventory.Letters, inventory.Capacity, needs, wanted);
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

        public WordEntry Recipe(string word) => ResolveRecipe(new WordEntry { word = word });

        public bool StartsRecipe(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return false;
            foreach (var word in Words) if (word.word.StartsWith(prefix, StringComparison.Ordinal)) return true;
            return false;
        }

        public bool BeginCraft(WordEntry entry)
        {
            entry = ResolveRecipe(entry);
            // @trace MECH-003
            if (IsCrafting || entry == null || (!IsSignal(entry) && !SummonEffects.CanApply(controller, entry))) return false;
            bool free = inventory.SpellsFree;
            if (!inventory.TrySpend(entry.word)) return false;
            Close();
            crafting = entry;
            craftFree = free;
            inventory.ReservedCount = free ? 0 : entry.word.Length;
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
            bool free = craftFree;
            crafting = null;
            craftFree = false;
            inventory.ReservedCount = 0;
            controller.MoveScale = 1f;
            Refund(word, free);
        }
        void Refund(string word, bool free)
        {
            if (free) return;
            foreach (char letter in word) inventory.TryAdd(letter);
        }

        public bool Summon(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            foreach (var entry in Words) if (entry.word == word.ToUpperInvariant()) return Summon(entry);
            return false;
        }
        public bool Summon(WordEntry entry)
        {
            entry = ResolveRecipe(entry);
            // @trace MECH-003, MECH-005
            if (IsCrafting || entry == null || (!IsSignal(entry) && !SummonEffects.CanApply(controller, entry))) return false;
            bool free = inventory.SpellsFree;
            if (!inventory.TrySpend(entry.word)) return false;
            if (!IsSignal(entry) && !SummonEffects.Apply(controller, entry)) { Refund(entry.word, free); return false; }
            Summoned?.Invoke(entry.word);
            return true;
        }
    }
}
