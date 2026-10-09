using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class ShopPage : LobbyPage
    {
        static readonly Vector2 Card = new Vector2(248, 316);
        string note, spotlight, recipe = "BAT";
        bool recipePicker;
        TMP_Text footnote;

        public override string Id => LobbyMenu.Shop;
        public override LobbyStage.Focus Focus => LobbyStage.Focus.Left;
        public string Spotlit => spotlight;
        public string SelectedRecipe => recipe;

        public override void Opened() { note = null; spotlight = null; }

        public override void Closed()
        {
            LobbyThemes.Restore();
            if (Menu && Menu.Stage)
            {
                Menu.Stage.ClearItemPreview();
                Menu.Stage.Dress(Menu.Outfit);
            }
        }

        public void Spotlight(string offerId)
        {
            spotlight = offerId;
            Refresh();
            if (First && EventSystem.current) EventSystem.current.SetSelectedGameObject(First.gameObject);
        }

        protected override void Build()
        {
            var body = Side("Style studio", "Permanent collections. Your looks, your choice.");
            footnote = LobbyKit.Text(body, note ?? "Preview freely. Earn coins in matches. Looks never change stats.",
                16, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
            footnote.enableAutoSizing = true; footnote.fontSizeMin = 13; footnote.fontSizeMax = 16;
            footnote.rectTransform.Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 36));
            var view = LobbyKit.Rect(body, "Offers").Place(Vector2.zero, Vector2.one, new Vector2(0, 46), Vector2.zero);
            var list = LobbyKit.Scroll(view, "Scroll", 12);
            list.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(4, 4, 8, 12);

            LobbyKit.SectionLabel(list, "Set the scene");
            var themes = LobbyKit.Grid(list, "Themes", Card, 16);
            foreach (var theme in LobbyThemes.All) ThemeCard(themes, theme);
            WinterEmote(list);
            LobbyKit.SectionLabel(list, "Collected looks");
            var looks = LobbyKit.Grid(list, "Looks", Card, 16);
            foreach (var bundle in CosmeticBundles.All) LookCard(looks, bundle);
            LobbyKit.SectionLabel(list, "Your accessories · always free");
            var accessories = LobbyKit.Grid(list, "Accessories", new Vector2(248, 170), 16);
            foreach (string piece in new[] { "Cap", "Hood", "Glasses", "Satchel", "TBadge" }) Accessory(accessories, piece);
            LobbyKit.SectionLabel(list, "Make each recipe yours");
            RecipeChooser(list);
            var finishes = LobbyKit.Grid(list, "Finishes", Card, 16);
            foreach (string skin in GameConfig.Current.Items.Get(recipe).Skins)
                FinishCard(finishes, Career.Shop.FirstOrDefault(o => o.Kind == "skin" && o.Value == skin)
                    ?? new ShopOffer("skin", skin, skin, 0));
            LobbyKit.SectionLabel(list, "Top colours");
            var colours = LobbyKit.Grid(list, "Colours", Card, 16);
            foreach (var offer in Career.Shop.Where(o => o.Kind == "colour")) ColourCard(colours, offer);
        }

        void WinterEmote(Transform parent)
        {
            var collection = Art.SeasonalCollection.Winter;
            if (!collection || !collection.emote) return;
            LobbyKit.SectionLabel(parent, "Winter House Party · yours to keep");
            var row = LobbyKit.Row(parent, "Winter emote", 8);
            row.Size(-1, 58);
            LobbyKit.TextAction(row, "Play winter shuffle", "WINTER SHUFFLE", 24, PlayWinterEmote,
                icon: LobbyIcons.Chevron).Size(-1, 52, 1);
            LobbyKit.TextAction(row, "Stop winter shuffle", "STOP", 19, () => Menu.Stage.StopEmote()).Size(100, 52);
        }

        public void PlayWinterEmote()
        {
            var collection = Art.SeasonalCollection.Winter;
            if (!collection || !collection.emote) return;
            Menu.Stage.ClearItemPreview();
            Menu.Stage.PlayEmote(collection.emote);
        }

        RectTransform ArtCard(Transform parent, ShopOffer offer, string title, string description, bool worn, out RectTransform art)
        {
            var card = LobbyKit.Button(parent, "Offer " + offer.Id, Color.white, () => Preview(offer), 16, LobbyKit.Line, 2, 3);
            var face = card.Body();
            if (worn || offer.Id == spotlight) LobbyKit.Ring(face, worn ? LobbyKit.Cyan : LobbyKit.Sun, 16, 3);
            art = LobbyKit.Rect(face, "Art").Place(new Vector2(0, 1), Vector2.one, new Vector2(10, -138), new Vector2(-10, -10));
            art.Paint(Color.Lerp(LobbyThemes.Current.Secondary, Color.white, .7f), 12).raycastTarget = false;
            var name = LobbyKit.Display(face, title, 23, LobbyKit.Navy);
            name.enableAutoSizing = true; name.fontSizeMin = 18; name.fontSizeMax = 23;
            name.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(10, -176), new Vector2(-10, -146));
            var detail = LobbyKit.Text(face, description, 16, LobbyKit.Muted, TextAlignmentOptions.Center);
            detail.textWrappingMode = TextWrappingModes.Normal;
            detail.enableAutoSizing = true; detail.fontSizeMin = 15; detail.fontSizeMax = 16;
            detail.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(10, -246), new Vector2(-10, -178));
            ActionFor(face, offer, worn);
            return face;
        }

        void ActionFor(RectTransform face, ShopOffer offer, bool worn)
        {
            bool owned = Menu.Career.Owns(offer.Kind, offer.Value);
            int price = Menu.Career.PriceOf(offer);
            var action = LobbyKit.TextAction(face, (worn ? "Wearing " : owned ? "Wear " : "Buy ") + offer.Id,
                worn ? "EQUIPPED" : owned ? "EQUIP" : price + " COINS", 21,
                worn ? null : owned ? () => Wear(offer) : () => Buy(offer), worn, worn ? LobbyIcons.Check : owned ? null : LobbyIcons.Coin);
            action.interactable = !worn;
            ((RectTransform)action.transform).Place(Vector2.zero, new Vector2(1, 0), new Vector2(16, 10), new Vector2(-16, 54));
            if (!worn && (offer.Id == spotlight || !First)) First = action;
        }

        void ThemeCard(Transform parent, LobbyTheme theme)
        {
            var offer = Career.Shop.FirstOrDefault(o => o.Id == "theme:" + theme.Id) ?? new ShopOffer("theme", theme.Id, theme.Name, 0);
            bool worn = PlayerPrefs.GetString("wv.theme", "sunroom") == theme.Id;
            ArtCard(parent, offer, theme.Name, "Lobby art + matching colours", worn, out var art);
            if (theme.Background)
            {
                var image = LobbyKit.Rect(art, "Theme picture").Fill().gameObject.AddComponent<RawImage>();
                image.texture = theme.Background; image.raycastTarget = false;
                float crop = Mathf.Min(1f, (Card.x - 20f) / 128f / ((float)theme.Background.width / theme.Background.height));
                image.uvRect = new Rect((1f - crop) * .5f, 0, crop, 1);
            }
        }

        void LookCard(Transform parent, CosmeticBundle bundle)
        {
            var offer = Career.Shop.FirstOrDefault(o => o.Id == "look:" + bundle.Id) ?? new ShopOffer("look", bundle.Id, bundle.Name, 0);
            var proposed = bundle.Apply(Menu.Outfit, GameConfig.Current.Wardrobe);
            bool worn = proposed != null && proposed.Serialize() == Menu.Outfit.Serialize();
            ArtCard(parent, offer, bundle.Name, bundle.Collection + "\n" + bundle.Description, worn, out var art);
            var colour = GameConfig.Current.Wardrobe.Colour("Top", bundle.TopColour);
            Blob(art, new Color(colour.R, colour.G, colour.B), 94);
            var icon = LobbyKit.Icon(art, LobbyIcons.Satchel, LobbyKit.Navy);
            icon.rectTransform.Pin(new Vector2(.75f, .35f), Vector2.zero, new Vector2(48, 48));
            var tag = LobbyKit.Text(art, bundle.Price == 0 ? "YOURS" : "COMPLETE LOOK", 12, LobbyKit.Navy, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            tag.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -8));
        }

        void FinishCard(Transform parent, ShopOffer offer)
        {
            bool worn = (Menu.Outfit.SkinFor(recipe) ?? "Classic") == offer.Value;
            ArtCard(parent, offer, offer.Value + " " + recipe, "Finish for this recipe only", worn, out var art);
            FinishDisc(art, offer.Value, 106);
            var item = LobbyKit.ItemImage(art, recipe, 84, -8);
            if (item) item.rectTransform.Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(84, 84));
        }

        void ColourCard(Transform parent, ShopOffer offer)
        {
            ArtCard(parent, offer, offer.Name, (Menu.Outfit.PieceIn("Top") ?? "Top") + " colour",
                Menu.Outfit.ColourOf("Top") == offer.Value, out var art);
            var colour = GameConfig.Current.Wardrobe.Colour("Top", offer.Value);
            Blob(art, new Color(colour.R, colour.G, colour.B), 100);
        }

        void Accessory(Transform parent, string id)
        {
            var piece = GameConfig.Current.Wardrobe.Piece(id);
            bool worn = Menu.Outfit.PieceIn(piece.Slot) == id;
            var face = LobbyKit.Rect(parent, "Accessory " + id);
            face.Paint(Color.white, 14).raycastTarget = false;
            var icon = LobbyKit.Icon(face, id == "Glasses" ? LobbyIcons.Glasses : id == "Satchel" ? LobbyIcons.Satchel : id == "TBadge" ? LobbyIcons.Badge : LobbyIcons.Locker, LobbyKit.Cyan);
            icon.rectTransform.Pin(new Vector2(.5f, 1), new Vector2(0, -14), new Vector2(54, 54));
            LobbyKit.Text(face, id == "TBadge" ? "Letter badge" : id, 21, LobbyKit.Navy, TextAlignmentOptions.Center).rectTransform
                .Place(new Vector2(0, 1), Vector2.one, new Vector2(8, -106), new Vector2(-8, -74));
            var action = LobbyKit.TextAction(face, "Accessory toggle " + id, worn ? "REMOVE" : "EQUIP", 19, () => EquipAccessory(id), worn);
            ((RectTransform)action.transform).Place(Vector2.zero, new Vector2(1, 0), new Vector2(12, 10), new Vector2(-12, 54));
        }

        void RecipeChooser(Transform parent)
        {
            var row = LobbyKit.Row(parent, "Recipe selector", 8);
            row.Size(-1, 54);
            LobbyKit.TextAction(row, "Previous recipe", "‹", 30, () => StepRecipe(-1)).Size(60, 48);
            var choose = LobbyKit.TextAction(row, "Choose recipe", recipe, 24, () => { recipePicker = !recipePicker; Refresh(); Reselect("Choose recipe"); }, icon: LobbyIcons.Chevron).Size(-1, 48, 1);
            choose.Body().Find("Content/Icon " + LobbyIcons.Chevron).localRotation = Quaternion.Euler(0, 0, -90);
            LobbyKit.TextAction(row, "Next recipe", "›", 30, () => StepRecipe(1)).Size(60, 48);
            if (!recipePicker) return;
            var grid = LobbyKit.Grid(parent, "Recipes", new Vector2(180, 44), 8);
            foreach (var item in GameConfig.Current.Items.Enabled)
            {
                string word = item.Id;
                LobbyKit.TextAction(grid, "Recipe " + word, word, 18, () => SelectRecipe(word), word == recipe);
            }
        }

        public bool SelectRecipe(string word)
        {
            if (!GameConfig.Current.Items.TryGet(word, out var item) || !item.Enabled) return false;
            recipe = item.Id; recipePicker = false;
            Refresh();
            Menu.Stage.PreviewItem(recipe, Menu.Outfit.SkinFor(recipe) ?? "Classic");
            Reselect("Choose recipe");
            return true;
        }

        void StepRecipe(int direction)
        {
            var items = GameConfig.Current.Items.Enabled.ToArray();
            int index = Array.FindIndex(items, item => item.Id == recipe);
            SelectRecipe(items[(index + direction + items.Length) % items.Length].Id);
        }

        public void Preview(ShopOffer offer)
        {
            if (offer == null) return;
            if (offer.Kind == "theme") LobbyThemes.Preview(offer.Value);
            else if (offer.Kind == "skin") Menu.Stage.PreviewItem(recipe, offer.Value);
            else
            {
                var next = offer.Kind == "look" ? CosmeticBundles.Find(offer.Value)?.Apply(Menu.Outfit, GameConfig.Current.Wardrobe) : Menu.Outfit.Clone();
                if (offer.Kind == "colour") next.Colours["Top"] = offer.Value;
                if (next == null || !GameConfig.Current.Wardrobe.Fits(next)) return;
                Menu.Stage.ClearItemPreview(); Menu.Stage.Dress(next);
            }
            if (footnote) footnote.text = "Preview: " + offer.Name + " · " +
                (Menu.Career.Owns(offer.Kind, offer.Value) ? "Equip to save" : "Unlock to equip");
        }

        public void Buy(ShopOffer offer)
        {
            if (offer == null) return;
            string error = Menu.Career.Buy(offer.Id);
            note = error ?? "Bought " + offer.Name + ".";
            if (error == null) { Menu.SaveCareer(); Menu.Post(note); }
            Refresh();
            Reselect((error == null ? "Wear " : "Buy ") + offer.Id);
        }

        public void Wear(ShopOffer offer)
        {
            if (offer == null || !Menu.Career.Owns(offer.Kind, offer.Value)) return;
            if (offer.Kind == "theme")
            {
                if (!LobbyThemes.Equip(offer.Value, Menu.Career)) return;
            }
            else
            {
                Outfit next;
                if (offer.Kind == "skin") next = CosmeticBundles.WithItemSkin(Menu.Outfit, recipe, offer.Value, Menu.Career, GameConfig.Current.Items);
                else if (offer.Kind == "look") next = CosmeticBundles.Find(offer.Value)?.Apply(Menu.Outfit, GameConfig.Current.Wardrobe);
                else if (offer.Kind == "colour") { next = Menu.Outfit.Clone(); next.Colours["Top"] = offer.Value; }
                else return;
                if (next == null || !GameConfig.Current.Wardrobe.Fits(next)) return;
                Menu.Wear(next);
            }
            note = "Equipped " + offer.Name + ".";
            Refresh();
            if (offer.Kind == "skin") Menu.Stage.PreviewItem(recipe, offer.Value);
            Reselect("Offer " + offer.Id);
        }

        public void EquipAccessory(string id)
        {
            var wardrobe = GameConfig.Current.Wardrobe;
            var piece = wardrobe.Piece(id);
            if (piece == null || !Menu.Career.Owns("piece", id) || wardrobe.RequiredSlots.Contains(piece.Slot)) return;
            bool remove = Menu.Outfit.PieceIn(piece.Slot) == id;
            var next = Menu.Outfit.Clone();
            if (!remove)
                foreach (string requirement in piece.Requires)
                {
                    var required = wardrobe.Piece(requirement);
                    next = wardrobe.Wear(next, required.Slot, requirement) ?? next;
                }
            next = wardrobe.Wear(next, piece.Slot, remove ? null : id);
            if (next == null) return;
            Menu.Wear(next); Refresh(); Reselect("Accessory toggle " + id);
        }
    }
}
