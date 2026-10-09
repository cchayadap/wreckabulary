using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules
{
    public static class Skin
    {
        public const string Standard = "Classic";
    }

    public enum ItemState
    {
        World,
        Held,
        Carried,
        Thrown,
        Deployed,
        Armed,
        Destroyed,
    }

    public enum ItemOrigin { Map, Crafted }

    public sealed class ItemInstance
    {
        public int Id { get; internal set; }
        public string Word { get; internal set; }
        public ItemOrigin Origin { get; internal set; }
        public ItemState State { get; internal set; }
        public int HolderId { get; internal set; } = -1;
        public int CrafterId { get; internal set; } = -1;
        public int DeployerId { get; internal set; } = -1;
        public int ThrowerId { get; internal set; } = -1;
        public string ThrowerSkin { get; internal set; }
        public int ActivatorId { get; internal set; } = -1;
        public float Durability { get; internal set; }
        public float MaxDurability { get; internal set; }
        public bool Spent { get; internal set; }
        public int Revision { get; internal set; }

        public LetterBag Letters => LetterBag.FromWord(Word);

        public bool IsGone => State == ItemState.Destroyed;

        public int LockedLetterCount => IsGone || Spent ? 0 : Word.Length;

        public override string ToString() => $"#{Id} {Word} {State}";
    }

    public readonly struct ItemVisual
    {
        public readonly string Skin;
        public readonly bool Miniature;
        public readonly string CollisionProfile;

        public ItemVisual(string skin, bool miniature, string collisionProfile)
        {
            Skin = skin;
            Miniature = miniature;
            CollisionProfile = collisionProfile;
        }

        public override string ToString() => $"{Skin}{(Miniature ? " miniature" : " full")} [{CollisionProfile}]";
    }

    public static class Appearance
    {
        public static ItemVisual Resolve(ItemInstance item, ItemDefinition definition, Func<int, string, string> chosenSkin)
        {
            switch (item.State)
            {
                case ItemState.Held:
                    return new ItemVisual(Pick(definition, chosenSkin?.Invoke(item.HolderId, item.Word)), true, item.Word + "_held");
                case ItemState.Thrown when item.Origin == ItemOrigin.Crafted:
                    return new ItemVisual(Pick(definition, item.ThrowerSkin), true, item.Word + "_held");
                default:
                    return new ItemVisual(Rules.Skin.Standard, false, item.Word + "_world");
            }
        }

        public static string Pick(ItemDefinition definition, string chosen)
        {
            if (definition != null && chosen != null && definition.HasSkin(chosen)) return chosen;
            return Rules.Skin.Standard;
        }
    }

    public enum Refusal
    {
        None,
        NoSuchPlayer,
        NoSuchItem,
        NoSuchTile,
        UnknownRecipe,
        RecipeDisabled,
        StaleRevision,
        WrongState,
        NotHolder,
        HandsFull,
        EmptySlot,
        BagFull,
        MissingLetters,
        AlreadyCrafting,
        NotCrafting,
        TooEarly,
        DeployLimit,
        NotDeployable,
        NotConsumable,
        NotAlive,
        NotTeammate,
        OriginalFurniture,
    }

    public readonly struct Outcome
    {
        public readonly Refusal Refusal;
        public readonly ItemInstance Item;

        Outcome(Refusal refusal, ItemInstance item)
        {
            Refusal = refusal;
            Item = item;
        }

        public bool Ok => Refusal == Refusal.None;

        public static Outcome Done(ItemInstance item = null) => new Outcome(Refusal.None, item);
        public static Outcome Refused(Refusal why) => new Outcome(why, null);

        public override string ToString() => Ok ? $"ok {Item}" : $"refused: {Refusal}";
    }

    public readonly struct TileSpawn
    {
        public readonly int TileId;
        public readonly char Letter;

        public TileSpawn(int tileId, char letter)
        {
            TileId = tileId;
            Letter = letter;
        }
    }

    public sealed class TileBatch
    {
        public readonly List<TileSpawn> Tiles = new List<TileSpawn>();
        public int Count => Tiles.Count;
    }
}
