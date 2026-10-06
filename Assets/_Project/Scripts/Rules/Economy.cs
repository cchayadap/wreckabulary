using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public sealed class CraftJob
    {
        public string ItemId;
        public double StartedAt;
        public double ReadyAt;
        public int Slot;
    }

    public sealed class PlayerInventory
    {
        public int PlayerId { get; internal set; }
        public int Team { get; internal set; }
        public LetterBag Letters { get; } = new LetterBag();
        public LetterBag Reserved { get; } = new LetterBag();
        public int[] Slots { get; internal set; }
        public int CarriedFurniture { get; internal set; } = -1;
        public CraftJob Craft { get; internal set; }
        public bool CanAct { get; set; } = true;

        public int LetterCount => Letters.Count + Reserved.Count;
        public bool IsCrafting => Craft != null;

        internal int FreeSlot()
        {
            for (int i = 0; i < Slots.Length; i++)
                if (Slots[i] < 0 && (Craft == null || Craft.Slot != i)) return i;
            return -1;
        }

        internal int SlotOf(int itemId) => Array.IndexOf(Slots, itemId);
    }

    public readonly struct LetterAudit
    {
        public readonly int Tiles, Bags, Reserved, InItems, Minted, Spent;

        public LetterAudit(int tiles, int bags, int reserved, int inItems, int minted, int spent)
        {
            Tiles = tiles; Bags = bags; Reserved = reserved; InItems = inItems; Minted = minted; Spent = spent;
        }

        public int Total => Tiles + Bags + Reserved + InItems;
        public bool Balanced => Total == Minted - Spent;

        public override string ToString() =>
            $"tiles {Tiles} + bags {Bags} + reserved {Reserved} + items {InItems} = {Total}; minted {Minted} - spent {Spent} = {Minted - Spent}";
    }

    public sealed class Economy
    {
        public ItemCatalogue Catalogue { get; }
        public GameRules Rules { get; }

        readonly Dictionary<int, PlayerInventory> players = new Dictionary<int, PlayerInventory>();
        readonly Dictionary<int, ItemInstance> items = new Dictionary<int, ItemInstance>();
        readonly Dictionary<int, char> tiles = new Dictionary<int, char>();
        int nextItemId = 1;
        int nextTileId = 1;

        public int Minted { get; private set; }
        public int Spent { get; private set; }

        public event Action<ItemInstance> ItemChanged;

        public Economy(ItemCatalogue catalogue, GameRules rules)
        {
            Catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public IEnumerable<PlayerInventory> Players => players.Values;
        public IEnumerable<ItemInstance> Items => items.Values;
        public IReadOnlyDictionary<int, char> Tiles => tiles;

        public PlayerInventory Player(int playerId) => players.TryGetValue(playerId, out var p) ? p : null;
        public ItemInstance Item(int itemId) => items.TryGetValue(itemId, out var i) ? i : null;

        public PlayerInventory AddPlayer(int playerId, int team)
        {
            if (players.ContainsKey(playerId)) throw new ArgumentException($"Player {playerId} is already in the match.");
            var p = new PlayerInventory { PlayerId = playerId, Team = team, Slots = new int[Rules.MaxCarried] };
            for (int i = 0; i < p.Slots.Length; i++) p.Slots[i] = -1;
            if (!string.IsNullOrEmpty(Rules.StarterLetters))
            {
                p.Letters.Add(LetterBag.FromWord(Rules.StarterLetters));
                Minted += Rules.StarterLetters.Length;
            }
            players.Add(playerId, p);
            return p;
        }

        public ItemInstance PlaceFurniture(string word)
        {
            if (!LetterBag.IsWord(word)) throw new ArgumentException($"Furniture word '{word}' must be A-Z.");
            float toughness = Rules.FurnitureToughnessBase + Rules.FurnitureToughnessPerLetter * word.Length;
            var item = NewItem(word, ItemOrigin.Map, ItemState.World, toughness);
            Minted += word.Length;
            Changed(item);
            return item;
        }

        public TileBatch MintTiles(string letters)
        {
            if (!LetterBag.IsWord(letters)) throw new ArgumentException($"'{letters}' must be A-Z.");
            var batch = new TileBatch();
            foreach (char c in letters) AddTile(batch, c);
            Minted += letters.Length;
            return batch;
        }

        public Outcome CollectTile(int playerId, int tileId)
        {
            var p = Player(playerId);
            if (p == null) return Outcome.Refused(Refusal.NoSuchPlayer);
            if (!p.CanAct) return Outcome.Refused(Refusal.NotAlive);
            if (!tiles.TryGetValue(tileId, out char letter)) return Outcome.Refused(Refusal.NoSuchTile);
            if (p.LetterCount >= Rules.MaxLetters) return Outcome.Refused(Refusal.BagFull);
            tiles.Remove(tileId);
            p.Letters.Add(letter);
            return Outcome.Done();
        }

        public TileBatch KnockLoose(int playerId, int count)
        {
            var batch = new TileBatch();
            var p = Player(playerId);
            if (p == null) return batch;
            for (int n = 0; n < count && !p.Letters.IsEmpty; n++)
            {
                char pick = p.Letters.Letters().GroupBy(c => c).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key;
                p.Letters.TryRemove(pick);
                AddTile(batch, pick);
            }
            return batch;
        }

        public Outcome GiveLetter(int fromId, int toId, char letter)
        {
            var from = Player(fromId);
            var to = Player(toId);
            if (from == null || to == null) return Outcome.Refused(Refusal.NoSuchPlayer);
            if (fromId == toId || !Teams.AreTeammates(from.Team, to.Team)) return Outcome.Refused(Refusal.NotTeammate);
            if (!from.CanAct) return Outcome.Refused(Refusal.NotAlive);
            if (from.Letters[letter] == 0) return Outcome.Refused(Refusal.MissingLetters);
            if (to.LetterCount >= Rules.MaxLetters) return Outcome.Refused(Refusal.BagFull);
            from.Letters.TryRemove(letter);
            to.Letters.Add(letter);
            return Outcome.Done();
        }

        public TileBatch TossLetter(int playerId, char letter)
        {
            var batch = new TileBatch();
            var p = Player(playerId);
            if (p != null && p.CanAct && p.Letters.TryRemove(letter)) AddTile(batch, letter);
            return batch;
        }

        public Outcome BeginCraft(int playerId, string itemId, double now)
        {
            var p = Player(playerId);
            if (p == null) return Outcome.Refused(Refusal.NoSuchPlayer);
            if (!p.CanAct) return Outcome.Refused(Refusal.NotAlive);
            if (!Catalogue.TryGet(itemId, out var def)) return Outcome.Refused(Refusal.UnknownRecipe);
            if (!def.Enabled) return Outcome.Refused(Refusal.RecipeDisabled);
            if (p.IsCrafting) return Outcome.Refused(Refusal.AlreadyCrafting);
            if (p.CarriedFurniture >= 0) return Outcome.Refused(Refusal.HandsFull);
            int slot = p.FreeSlot();
            if (slot < 0) return Outcome.Refused(Refusal.HandsFull);
            if (!p.Letters.TryRemove(def.Letters)) return Outcome.Refused(Refusal.MissingLetters);
            p.Reserved.Add(def.Letters);
            p.Craft = new CraftJob { ItemId = def.Id, StartedAt = now, ReadyAt = now + Rules.CraftSeconds(def), Slot = slot };
            return Outcome.Done();
        }

        public Outcome CancelCraft(int playerId)
        {
            var p = Player(playerId);
            if (p == null) return Outcome.Refused(Refusal.NoSuchPlayer);
            if (!p.IsCrafting) return Outcome.Refused(Refusal.NotCrafting);
            p.Letters.Add(p.Reserved.TakeAll());
            p.Craft = null;
            return Outcome.Done();
        }

        public Outcome CompleteCraft(int playerId, double now)
        {
            var p = Player(playerId);
            if (p == null) return Outcome.Refused(Refusal.NoSuchPlayer);
            if (!p.IsCrafting) return Outcome.Refused(Refusal.NotCrafting);
            if (!p.CanAct) return Outcome.Refused(Refusal.NotAlive);
            if (now < p.Craft.ReadyAt) return Outcome.Refused(Refusal.TooEarly);
            var def = Catalogue.Get(p.Craft.ItemId);
            int slot = p.Craft.Slot;
            if (!p.Reserved.Equals(def.Letters))
                throw new InvalidOperationException($"Player {playerId}'s reservation {p.Reserved} doesn't match {def.Id}.");
            p.Reserved.TakeAll();
            p.Craft = null;
            var item = NewItem(def.Id, ItemOrigin.Crafted, ItemState.Held, def.Durability > 0 ? def.Durability : 1f);
            item.CrafterId = playerId;
            item.HolderId = playerId;
            p.Slots[slot] = item.Id;
            Changed(item);
            return Outcome.Done(item);
        }

        public Outcome PickUp(int playerId, int itemId, int seenRevision)
        {
            var p = Player(playerId);
            if (p == null) return Outcome.Refused(Refusal.NoSuchPlayer);
            if (!p.CanAct) return Outcome.Refused(Refusal.NotAlive);
            var item = Item(itemId);
            if (item == null || item.IsGone) return Outcome.Refused(Refusal.NoSuchItem);
            if (item.Revision != seenRevision) return Outcome.Refused(Refusal.StaleRevision);
            if (item.State != ItemState.World && item.State != ItemState.Deployed) return Outcome.Refused(Refusal.WrongState);

            if (item.Origin == ItemOrigin.Map)
            {
                if (p.CarriedFurniture >= 0) return Outcome.Refused(Refusal.HandsFull);
                if (p.IsCrafting) return Outcome.Refused(Refusal.AlreadyCrafting);
                item.State = ItemState.Carried;
                item.HolderId = playerId;
                p.CarriedFurniture = item.Id;
                Changed(item);
                return Outcome.Done(item);
            }

            int slot = p.FreeSlot();
            if (slot < 0) return Outcome.Refused(Refusal.HandsFull);
            item.State = ItemState.Held;
            item.HolderId = playerId;
            item.DeployerId = -1;
            p.Slots[slot] = item.Id;
            Changed(item);
            return Outcome.Done(item);
        }

        public Outcome Transfer(int fromId, int toId, int slot)
        {
            var from = Player(fromId);
            var to = Player(toId);
            if (from == null || to == null) return Outcome.Refused(Refusal.NoSuchPlayer);
            if (fromId == toId || !Teams.AreTeammates(from.Team, to.Team)) return Outcome.Refused(Refusal.NotTeammate);
            if (!from.CanAct || !to.CanAct) return Outcome.Refused(Refusal.NotAlive);
            var item = SlotItem(from, slot, out var refusal);
            if (item == null) return Outcome.Refused(refusal);
            int toSlot = to.FreeSlot();
            if (toSlot < 0) return Outcome.Refused(Refusal.HandsFull);
            from.Slots[slot] = -1;
            to.Slots[toSlot] = item.Id;
            item.HolderId = toId;
            Changed(item);
            return Outcome.Done(item);
        }

        public Outcome Drop(int playerId, int slot)
        {
            var p = Player(playerId);
            if (p == null) return Outcome.Refused(Refusal.NoSuchPlayer);
            var item = SlotItem(p, slot, out var refusal);
            if (item == null) return Outcome.Refused(refusal);
            p.Slots[slot] = -1;
            item.State = ItemState.World;
            item.HolderId = -1;
            Changed(item);
            return Outcome.Done(item);
        }

        public Outcome DropCarried(int playerId)
        {
            var p = Player(playerId);
            if (p == null) return Outcome.Refused(Refusal.NoSuchPlayer);
            var item = Item(p.CarriedFurniture);
            if (item == null) return Outcome.Refused(Refusal.EmptySlot);
            p.CarriedFurniture = -1;
            item.State = ItemState.World;
            item.HolderId = -1;
            Changed(item);
            return Outcome.Done(item);
        }

        public Outcome Throw(int playerId, int slot, string throwerSkin)
        {
            var p = Player(playerId);
            if (p == null) return Outcome.Refused(Refusal.NoSuchPlayer);
            if (!p.CanAct) return Outcome.Refused(Refusal.NotAlive);
            ItemInstance item;
            if (slot < 0)
            {
                item = Item(p.CarriedFurniture);
                if (item == null) return Outcome.Refused(Refusal.EmptySlot);
                p.CarriedFurniture = -1;
            }
            else
            {
                item = SlotItem(p, slot, out var refusal);
                if (item == null) return Outcome.Refused(refusal);
                p.Slots[slot] = -1;
                if (Catalogue.TryGet(item.Word, out var def) && IsFused(def))
                {
                    Spend(item);
                    item.ActivatorId = playerId;
                }
            }
            item.State = ItemState.Thrown;
            item.HolderId = -1;
            item.ThrowerId = playerId;
            item.ThrowerSkin = item.Origin == ItemOrigin.Crafted ? throwerSkin : null;
            Changed(item);
            return Outcome.Done(item);
        }

        public Outcome Settle(int itemId)
        {
            var item = Item(itemId);
            if (item == null || item.IsGone) return Outcome.Refused(Refusal.NoSuchItem);
            if (item.State != ItemState.Thrown) return Outcome.Refused(Refusal.WrongState);
            item.State = item.Spent ? ItemState.Armed : ItemState.World;
            item.ThrowerId = -1;
            item.ThrowerSkin = null;
            Changed(item);
            return Outcome.Done(item);
        }

        public Outcome Deploy(int playerId, int slot)
        {
            var p = Player(playerId);
            if (p == null) return Outcome.Refused(Refusal.NoSuchPlayer);
            if (!p.CanAct) return Outcome.Refused(Refusal.NotAlive);
            var item = SlotItem(p, slot, out var refusal);
            if (item == null) return Outcome.Refused(refusal);
            var def = Catalogue.Get(item.Word);
            if (!def.CanDeploy && !IsFused(def)) return Outcome.Refused(Refusal.NotDeployable);
            if (DeployedCount(playerId) >= Rules.MaxDeployed) return Outcome.Refused(Refusal.DeployLimit);
            p.Slots[slot] = -1;
            item.HolderId = -1;
            item.DeployerId = playerId;
            if (def.Consumable)
            {
                Spend(item);
                item.ActivatorId = playerId;
                item.State = ItemState.Armed;
            }
            else
            {
                item.State = ItemState.Deployed;
            }
            Changed(item);
            return Outcome.Done(item);
        }

        public Outcome Consume(int playerId, int slot)
        {
            var p = Player(playerId);
            if (p == null) return Outcome.Refused(Refusal.NoSuchPlayer);
            if (!p.CanAct) return Outcome.Refused(Refusal.NotAlive);
            var item = SlotItem(p, slot, out var refusal);
            if (item == null) return Outcome.Refused(refusal);
            var def = Catalogue.Get(item.Word);
            if (!def.Consumable || def.Use == null) return Outcome.Refused(Refusal.NotConsumable);
            p.Slots[slot] = -1;
            Spend(item);
            Remove(item);
            return Outcome.Done(item);
        }

        public Outcome Expire(int itemId)
        {
            var item = Item(itemId);
            if (item == null || item.IsGone) return Outcome.Refused(Refusal.NoSuchItem);
            if (item.State != ItemState.Armed) return Outcome.Refused(Refusal.WrongState);
            Remove(item);
            return Outcome.Done(item);
        }

        public TileBatch Damage(int itemId, float amount)
        {
            var item = Item(itemId);
            if (item == null || item.IsGone || amount <= 0f) return new TileBatch();
            item.Durability = Math.Max(0f, item.Durability - amount);
            if (item.Durability > 0f)
            {
                Changed(item);
                return new TileBatch();
            }
            return Break(itemId);
        }

        public TileBatch Break(int itemId)
        {
            var batch = new TileBatch();
            var item = Item(itemId);
            if (item == null || item.IsGone) return batch;
            if (item.HolderId >= 0)
            {
                var holder = Player(item.HolderId);
                if (holder != null)
                {
                    int slot = holder.SlotOf(itemId);
                    if (slot >= 0) holder.Slots[slot] = -1;
                    if (holder.CarriedFurniture == itemId) holder.CarriedFurniture = -1;
                }
            }
            if (!item.Spent)
                foreach (char c in item.Word) AddTile(batch, c);
            Remove(item);
            return batch;
        }

        public TileBatch Eliminate(int playerId, List<ItemInstance> droppedItems = null)
        {
            var batch = new TileBatch();
            var p = Player(playerId);
            if (p == null) return batch;
            if (p.IsCrafting) CancelCraft(playerId);
            foreach (char c in p.Letters.TakeAll().Letters()) AddTile(batch, c);
            for (int s = 0; s < p.Slots.Length; s++)
            {
                if (p.Slots[s] < 0) continue;
                var r = Drop(playerId, s);
                if (r.Ok) droppedItems?.Add(r.Item);
            }
            if (p.CarriedFurniture >= 0)
            {
                var r = DropCarried(playerId);
                if (r.Ok) droppedItems?.Add(r.Item);
            }
            p.CanAct = false;
            return batch;
        }

        public int DeployedCount(int playerId) =>
            items.Values.Count(i => i.DeployerId == playerId && (i.State == ItemState.Deployed || i.State == ItemState.Armed));

        public LetterAudit Audit()
        {
            int bags = 0, reserved = 0, inItems = 0;
            foreach (var p in players.Values)
            {
                bags += p.Letters.Count;
                reserved += p.Reserved.Count;
                if (p.IsCrafting && !p.Reserved.Equals(Catalogue.Get(p.Craft.ItemId).Letters))
                    throw new InvalidOperationException($"Player {p.PlayerId}'s reservation doesn't match the recipe.");
            }
            foreach (var i in items.Values) inItems += i.LockedLetterCount;
            return new LetterAudit(tiles.Count, bags, reserved, inItems, Minted, Spent);
        }

        public List<string> CheckOwnership()
        {
            var problems = new List<string>();
            var seen = new HashSet<int>();
            foreach (var p in players.Values)
            {
                foreach (int id in p.Slots.Where(s => s >= 0).Concat(p.CarriedFurniture >= 0 ? new[] { p.CarriedFurniture } : new int[0]))
                {
                    if (!seen.Add(id)) problems.Add($"item {id} is held twice");
                    var item = Item(id);
                    if (item == null || item.IsGone) problems.Add($"player {p.PlayerId} holds missing item {id}");
                    else if (item.HolderId != p.PlayerId) problems.Add($"{item} is in player {p.PlayerId}'s hands but lists holder {item.HolderId}");
                }
            }
            foreach (var item in items.Values)
            {
                bool inHands = item.State == ItemState.Held || item.State == ItemState.Carried;
                if (inHands && !seen.Contains(item.Id)) problems.Add($"{item} says it is held but no player has it");
                if (!inHands && item.HolderId >= 0) problems.Add($"{item} has holder {item.HolderId} while {item.State}");
            }
            return problems;
        }

        static bool IsFused(ItemDefinition def) => def.Consumable && def.Thrown != null && def.Thrown.FuseSeconds > 0f;

        ItemInstance SlotItem(PlayerInventory p, int slot, out Refusal refusal)
        {
            refusal = Refusal.None;
            if (slot < 0 || slot >= p.Slots.Length || p.Slots[slot] < 0)
            {
                refusal = Refusal.EmptySlot;
                return null;
            }
            var item = Item(p.Slots[slot]);
            if (item == null || item.IsGone || item.State != ItemState.Held || item.HolderId != p.PlayerId)
            {
                refusal = Refusal.NotHolder;
                return null;
            }
            return item;
        }

        ItemInstance NewItem(string word, ItemOrigin origin, ItemState state, float durability)
        {
            var item = new ItemInstance
            {
                Id = nextItemId++, Word = word, Origin = origin, State = state,
                Durability = durability, MaxDurability = durability,
            };
            items.Add(item.Id, item);
            return item;
        }

        void Spend(ItemInstance item)
        {
            if (item.Spent) return;
            item.Spent = true;
            Spent += item.Word.Length;
        }

        void Remove(ItemInstance item)
        {
            item.State = ItemState.Destroyed;
            item.HolderId = -1;
            item.DeployerId = -1;
            item.ThrowerId = -1;
            item.ThrowerSkin = null;
            Changed(item);
        }

        void AddTile(TileBatch batch, char letter)
        {
            int id = nextTileId++;
            tiles.Add(id, letter);
            batch.Tiles.Add(new TileSpawn(id, letter));
        }

        void Changed(ItemInstance item)
        {
            item.Revision++;
            ItemChanged?.Invoke(item);
        }
    }
}
