using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public sealed class CosmeticBundle
    {
        public string Id { get; }
        public string Name { get; }
        public string Collection { get; }
        public string Description { get; }
        public int Price { get; }
        public IReadOnlyList<string> Unlocks { get; }
        public string TopColour { get; }
        readonly string top, headwear, bottoms, gloves, boots, capColour, faceColour, bagColour, gear, skin;
        readonly bool glasses;

        internal CosmeticBundle(string id, string name, string collection, string description, int price, string[] unlocks,
            string top, string headwear, string topColour, string bottoms, string gloves, string boots,
            string capColour, string faceColour, string bagColour, bool glasses, string gear = null, string skin = null)
        {
            Id = id; Name = name; Collection = collection; Description = description; Price = price;
            Unlocks = Array.AsReadOnly(unlocks);
            this.top = top; this.headwear = headwear; TopColour = topColour; this.bottoms = bottoms;
            this.gloves = gloves; this.boots = boots; this.capColour = capColour; this.faceColour = faceColour;
            this.bagColour = bagColour; this.glasses = glasses; this.gear = gear; this.skin = skin;
        }

        public bool OwnedBy(Career career) => career != null && Unlocks.All(id => career.Owns(IdKind(id), IdValue(id)));

        public int RemainingPrice(Career career) => Unlocks.Where(id => !career.Owns(IdKind(id), IdValue(id)))
            .Sum(id => Career.Shop.First(offer => offer.Id == id).Price);

        internal static string IdKind(string id) => id.Substring(0, id.IndexOf(':'));
        internal static string IdValue(string id) => id.Substring(id.IndexOf(':') + 1);

        /// <summary>Apply a complete native outfit while retaining unrelated recipe finishes.</summary>
        public Outfit Apply(Outfit current, WardrobeCatalogue wardrobe)
        {
            var next = (current ?? wardrobe.Default).Clone();
            next.Pieces["Top"] = top; next.Pieces["Bottoms"] = "Joggers";
            next.Pieces["Gloves"] = "Mittens"; next.Pieces["Footwear"] = "Boots";
            next.Pieces["Headwear"] = headwear; next.Pieces["Back"] = "Satchel"; next.Pieces["Badge"] = "TBadge";
            if (glasses) next.Pieces["Face"] = "Glasses"; else next.Pieces.Remove("Face");
            next.Colours["Top"] = TopColour; next.Colours["Bottoms"] = bottoms; next.Colours["Gloves"] = gloves;
            next.Colours["Footwear"] = boots; next.Colours["Headwear"] = capColour;
            next.Colours["Face"] = faceColour; next.Colours["Back"] = bagColour;
            if (gear != null) next.ItemSkins[gear] = skin;
            return wardrobe.Fits(next) ? next : null;
        }
    }

    public static class CosmeticBundles
    {
        public static IReadOnlyList<CosmeticBundle> All { get; } = Array.AsReadOnly(new[]
        {
            new CosmeticBundle("sunroom", "Sunny-side Club", "Sunroom Social", "Cap, glasses and a golden crewneck", 0, Array.Empty<string>(),
                "Crewneck", "Cap", "sunflower", "cream", "putty", "mustard", "sunflower", "gold", "mustard", true),
            new CosmeticBundle("courier", "Mint Messenger", "Sunroom Social", "Mint hoodie, satchel and letter badge", 0, Array.Empty<string>(),
                "Hoodie", "Hood", "mint", "olive", "charcoal", "forest", "mint", "classic", "olive", false),
            new CosmeticBundle("candy", "Sugar Rush", "Candy Carnival", "Pink hoodie, cherry glasses + Candy BAT", 400, new[] { "colour:bubblegum", "skin:Candy" },
                "Hoodie", "Cap", "bubblegum", "plum", "putty", "cherry", "oat", "cherry", "canvas", true, "BAT", "Candy"),
            new CosmeticBundle("arcade", "Midnight Arcade", "Arcade Social", "Grape crewneck, shades + Arcade SHIELD", 550, new[] { "colour:grape", "skin:Arcade" },
                "Crewneck", "Cap", "grape", "charcoal", "ink", "navy", "charcoal", "sky", "navy", true, "SHIELD", "Arcade"),
            new CosmeticBundle("lantern", "Lantern Walker", "Lantern Festival", "Warm hoodie, gold glasses and satchel", 0, Array.Empty<string>(),
                "Hoodie", "Hood", "tangerine", "cocoa", "sunflower", "tan", "tomato", "gold", "brick", true),
            new CosmeticBundle("winter", "Holly Housemate", "Winter House Party", "Red hoodie, forest boots and cream mittens", 0, Array.Empty<string>(),
                "Hoodie", "Hood", "tomato", "olive", "putty", "forest", "tomato", "gold", "olive", false)
        });

        public static CosmeticBundle Find(string id) => All.FirstOrDefault(bundle => bundle.Id == id);

        public static Outfit WithItemSkin(Outfit current, string recipe, string skin, Career career, ItemCatalogue items)
        {
            if (current == null || career == null || !career.Owns("skin", skin) ||
                !items.TryGet(recipe, out var item) || !item.Enabled || !item.HasSkin(skin)) return null;
            var next = current.Clone();
            next.ItemSkins[item.Id] = skin;
            return next;
        }
    }
}
