using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public sealed class EconomySnapshot
    {
        internal readonly PlayerInventory[] PlayerStates;
        internal readonly ItemInstance[] ItemStates;
        internal readonly KeyValuePair<int, char>[] TileStates;
        public int NextItemId { get; }
        public int NextTileId { get; }
        public int Minted { get; }
        public int Spent { get; }
        public IReadOnlyList<PlayerInventory> Players => Array.AsReadOnly(PlayerStates.Select(Copy).ToArray());
        public IReadOnlyList<ItemInstance> Items => Array.AsReadOnly(ItemStates.Select(Copy).ToArray());
        public IReadOnlyList<KeyValuePair<int, char>> Tiles => Array.AsReadOnly((KeyValuePair<int, char>[])TileStates.Clone());

        internal EconomySnapshot(int nextItemId, int nextTileId, int minted, int spent,
            IEnumerable<PlayerInventory> players, IEnumerable<ItemInstance> items, IEnumerable<KeyValuePair<int, char>> tiles)
        {
            NextItemId = nextItemId; NextTileId = nextTileId; Minted = minted; Spent = spent;
            PlayerStates = players.OrderBy(p => p.PlayerId).Select(Copy).ToArray();
            ItemStates = items.OrderBy(i => i.Id).Select(Copy).ToArray();
            TileStates = tiles.OrderBy(t => t.Key).ToArray();
        }

        internal static PlayerInventory Copy(PlayerInventory source)
        {
            var result = new PlayerInventory
            {
                PlayerId = source.PlayerId, Team = source.Team, Slots = (int[])source.Slots.Clone(),
                CarriedFurniture = source.CarriedFurniture, CanAct = source.CanAct,
                Craft = source.Craft == null ? null : new CraftJob
                {
                    ItemId = source.Craft.ItemId, StartedAt = source.Craft.StartedAt,
                    ReadyAt = source.Craft.ReadyAt, Slot = source.Craft.Slot
                }
            };
            result.Letters.Add(source.Letters);
            result.Reserved.Add(source.Reserved);
            return result;
        }

        internal static ItemInstance Copy(ItemInstance source) => new ItemInstance
        {
            Id = source.Id, Word = source.Word, Origin = source.Origin, State = source.State,
            HolderId = source.HolderId, CrafterId = source.CrafterId, DeployerId = source.DeployerId,
            DeploymentTick = source.DeploymentTick,
            ThrowerId = source.ThrowerId, ThrowerSkin = source.ThrowerSkin, ActivatorId = source.ActivatorId,
            Durability = source.Durability, MaxDurability = source.MaxDurability, Spent = source.Spent,
            Revision = source.Revision
        };
    }

    public sealed partial class Economy
    {
        public EconomySnapshot Capture() => new EconomySnapshot(nextItemId, nextTileId, Minted, Spent,
            players.Values, items.Values, tiles);

        public void Restore(EconomySnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var replacement = ValidateReplacement(snapshot);
            players.Clear(); items.Clear(); tiles.Clear();
            foreach (var p in replacement.players) players.Add(p.Key, p.Value);
            foreach (var item in replacement.items) items.Add(item.Key, item.Value);
            foreach (var tile in replacement.tiles) tiles.Add(tile.Key, tile.Value);
            nextItemId = replacement.nextItemId; nextTileId = replacement.nextTileId;
            Minted = replacement.Minted; Spent = replacement.Spent;
        }

        Economy ValidateReplacement(EconomySnapshot snapshot)
        {
            if (snapshot.NextItemId < 1 || snapshot.NextItemId >= int.MaxValue ||
                snapshot.NextTileId < 1 || snapshot.NextTileId >= int.MaxValue ||
                snapshot.Minted < 0 || snapshot.Spent < 0 || snapshot.Spent > snapshot.Minted ||
                snapshot.ItemStates.Length > 65536 || snapshot.TileStates.Length > 65536 ||
                snapshot.PlayerStates.Length != players.Count ||
                snapshot.PlayerStates.Select(p => p.PlayerId).Distinct().Count() != players.Count)
                throw InvalidSnapshot("counts or allocation frontiers");

            var replacement = new Economy(Catalogue, Rules)
            {
                nextItemId = snapshot.NextItemId, nextTileId = snapshot.NextTileId,
                Minted = snapshot.Minted, Spent = snapshot.Spent
            };
            foreach (var source in snapshot.PlayerStates)
            {
                if (!players.TryGetValue(source.PlayerId, out var existing) || source.Team != existing.Team ||
                    source.Slots.Length != Rules.MaxCarried || source.Slots.Any(id => id < -1 || id == 0) ||
                    source.CarriedFurniture < -1 || source.CarriedFurniture == 0 ||
                    source.LetterCount > Rules.MaxLetters || source.LetterCount < 0)
                    throw InvalidSnapshot("roster or inventory");
                var p = EconomySnapshot.Copy(source);
                if (p.Craft == null)
                {
                    if (!p.Reserved.IsEmpty) throw InvalidSnapshot("orphan reserved letters");
                }
                else if (!Catalogue.TryGet(p.Craft.ItemId, out var recipe) || !recipe.Enabled ||
                    !Finite(p.Craft.StartedAt) || !Finite(p.Craft.ReadyAt) ||
                    p.Craft.StartedAt < 0 || p.Craft.ReadyAt < p.Craft.StartedAt ||
                    p.Craft.Slot < 0 || p.Craft.Slot >= p.Slots.Length || p.Slots[p.Craft.Slot] != -1 ||
                    p.CarriedFurniture != -1 || !p.Reserved.Equals(recipe.Letters))
                    throw InvalidSnapshot("pending craft reservation");
                replacement.players.Add(p.PlayerId, p);
            }
            foreach (var source in snapshot.ItemStates)
            {
                if (source.Id < 1 || source.Id >= snapshot.NextItemId || replacement.items.ContainsKey(source.Id) ||
                    !LetterBag.IsWord(source.Word) || source.Word.Length > 64 ||
                    !Enum.IsDefined(typeof(ItemOrigin), source.Origin) || !Enum.IsDefined(typeof(ItemState), source.State) ||
                    source.Revision < 0 || source.DeploymentTick < -1 || !Finite(source.Durability) || !Finite(source.MaxDurability) ||
                    source.MaxDurability <= 0 || source.Durability < 0 || source.Durability > source.MaxDurability ||
                    (source.Origin == ItemOrigin.Crafted && !Catalogue.TryGet(source.Word, out _)) ||
                    !ValidPlayerReference(source.HolderId) || !ValidPlayerReference(source.CrafterId) ||
                    !ValidPlayerReference(source.DeployerId) || !ValidPlayerReference(source.ThrowerId) ||
                    !ValidPlayerReference(source.ActivatorId) || (source.ThrowerSkin != null && source.ThrowerSkin.Length > 64))
                    throw InvalidSnapshot("item or despawn tombstone");
                replacement.items.Add(source.Id, EconomySnapshot.Copy(source));
            }
            foreach (var tile in snapshot.TileStates)
            {
                if (tile.Key < 1 || tile.Key >= snapshot.NextTileId || !LetterBag.IsLetter(tile.Value) ||
                    replacement.tiles.ContainsKey(tile.Key)) throw InvalidSnapshot("letter entity");
                replacement.tiles.Add(tile.Key, tile.Value);
            }
            if (!replacement.Audit().Balanced || replacement.CheckOwnership().Count != 0)
                throw InvalidSnapshot("letter conservation or item ownership");
            foreach (var p in replacement.players.Values)
            {
                if (p.Slots.Any(id => id >= 0 && replacement.items[id].State != ItemState.Held) ||
                    (p.CarriedFurniture >= 0 && (replacement.items[p.CarriedFurniture].State != ItemState.Carried ||
                        replacement.items[p.CarriedFurniture].Origin != ItemOrigin.Map)))
                    throw InvalidSnapshot("held item category");
            }
            return replacement;
        }

        bool ValidPlayerReference(int id) => id == -1 || players.ContainsKey(id);
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static ArgumentException InvalidSnapshot(string field) => new ArgumentException("Invalid economy snapshot: " + field + ".");
    }
}
