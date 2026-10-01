using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules
{
    public static class Skin
    {
        /// <summary>The standard look. Every item has it, and every other skin falls back to it.</summary>
        public const string Standard = "Classic";
    }

    /// <summary>Where an item instance is now. See <see cref="Appearance"/> for how each state looks.</summary>
    public enum ItemState
    {
        /// <summary>Lying in the room: original furniture, or dropped or settled gear.</summary>
        World,
        /// <summary>In one of a player's equipment slots (crafted gear only).</summary>
        Held,
        /// <summary>Original furniture lifted in both hands, full size (the team's grab-and-throw).</summary>
        Carried,
        /// <summary>In flight after a throw, until it settles.</summary>
        Thrown,
        /// <summary>Placed as a tool: cover, jump pad, speed strip.</summary>
        Deployed,
        /// <summary>A consumable that has been activated and is running: a lit BOMB, a SOAP puddle.</summary>
        Armed,
        /// <summary>Gone: broken into letters, or used up.</summary>
        Destroyed,
    }

    /// <summary>Original furnishings are broken for letters; crafted gear can be picked up (brief §5).</summary>
    public enum ItemOrigin { Map, Crafted }

    /// <summary>
    /// One physical object in the match. The same instance survives pickup, transfer, throw,
    /// drop and deploy: its id, recipe, durability and owner never change on the way (brief §4).
    /// Only the server changes it, through <see cref="Economy"/>.
    /// </summary>
    public sealed class ItemInstance
    {
        public int Id { get; internal set; }
        /// <summary>The word this object is made of. For crafted gear it is also the catalogue id.</summary>
        public string Word { get; internal set; }
        public ItemOrigin Origin { get; internal set; }
        public ItemState State { get; internal set; }
        /// <summary>The player holding or carrying it, or -1.</summary>
        public int HolderId { get; internal set; } = -1;
        /// <summary>Who crafted it (-1 for original furniture). Kept for results and ownership tests.</summary>
        public int CrafterId { get; internal set; } = -1;
        /// <summary>The player whose deploy limit this counts against while Deployed or Armed, or -1.</summary>
        public int DeployerId { get; internal set; } = -1;
        /// <summary>The thrower, while Thrown.</summary>
        public int ThrowerId { get; internal set; } = -1;
        /// <summary>The thrower's skin, captured at the moment of the throw.</summary>
        public string ThrowerSkin { get; internal set; }
        /// <summary>Who lit or placed a consumable (the BOMB's thrower gets the knockout credit), or -1. Kept after it expires.</summary>
        public int ActivatorId { get; internal set; } = -1;
        public float Durability { get; internal set; }
        public float MaxDurability { get; internal set; }
        /// <summary>True once a consumable has been used: its letters are spent, never refunded.</summary>
        public bool Spent { get; internal set; }
        /// <summary>Goes up on every change. A request made against an old revision is refused.</summary>
        public int Revision { get; internal set; }

        public LetterBag Letters => LetterBag.FromWord(Word);

        public bool IsGone => State == ItemState.Destroyed;

        /// <summary>Letters this object would give back if it broke now (none once spent).</summary>
        public int LockedLetterCount => IsGone || Spent ? 0 : Word.Length;

        public override string ToString() => $"#{Id} {Word} {State}";
    }

    /// <summary>How an item looks right now. Only visual; collision comes from <see cref="CollisionProfile"/>.</summary>
    public readonly struct ItemVisual
    {
        public readonly string Skin;
        /// <summary>True for the miniature hand-held size (world model × held scale, grip in the hand).</summary>
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

    /// <summary>
    /// The appearance lifecycle from the brief (§4):
    /// held → the holder's skin, miniature; transferred → the new holder's skin;
    /// thrown → the thrower's skin until it settles; dropped, deployed, world → standard, full size.
    /// A skin the item doesn't have, or no choice at all, falls back to standard.
    /// </summary>
    public static class Appearance
    {
        /// <param name="chosenSkin">Returns a player's chosen skin for an item word, or null.</param>
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

        /// <summary>The skin to show: the chosen one if this item has it, otherwise standard.</summary>
        public static string Pick(ItemDefinition definition, string chosen)
        {
            if (definition != null && chosen != null && definition.HasSkin(chosen)) return chosen;
            return Rules.Skin.Standard;
        }
    }

    /// <summary>
    /// Why the server refused a request. Every refusal leaves the match unchanged, so a refused
    /// pickup, craft or deploy can never lose or duplicate anything.
    /// </summary>
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

    /// <summary>Letters that must appear in the room as loose tiles (a break, a drop or an elimination).</summary>
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
