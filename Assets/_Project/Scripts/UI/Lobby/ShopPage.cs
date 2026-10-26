using System;
using System.Collections.Generic;
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
        public const string Looks = "looks", Gear = "gear", Colours = "colours", Rooms = "rooms", Extras = "extras";
        static readonly (string id, string label)[] Categories =
            { (Looks, "Looks"), (Gear, "Gear"), (Colours, "Colours"), (Rooms, "Rooms"), (Extras, "Extras") };
        static readonly Vector2 Card = new Vector2(248, 270);
        string note, spotlight, recipe = "BAT";
        bool recipePicker;
        TMP_Text footnote;
        readonly Dictionary<string, (Image outline, bool worn)> outlines = new Dictionary<string, (Image, bool)>(StringComparer.Ordinal);
        readonly Dictionary<string, LobbyShopOfferView> authoredOffers = new Dictionary<string, LobbyShopOfferView>(StringComparer.Ordinal);
        LobbyShopLayout layout;
        LobbyShopPageView authoredPage;

        public override string Id => LobbyMenu.Shop;
        public override LobbyStage.Focus Focus => LobbyStage.Focus.Left;
        public override float PanelWidth => authoredPage && authoredPage.Panel ? authoredPage.Panel.rect.width : base.PanelWidth;
        public string Spotlit => spotlight;
        public string SelectedRecipe => recipe;
        public string Category { get; private set; } = Looks;

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
            Category = CategoryFor(offerId);
            Refresh();
            if (First && EventSystem.current) EventSystem.current.SetSelectedGameObject(First.gameObject);
        }

        protected override void Build()
        {
            outlines.Clear();
            authoredOffers.Clear();
            authoredPage = null;
            layout = Resources.Load<LobbyShopLayout>(LobbyShopLayout.ResourcePath);
            RectTransform list;
            if (layout)
            {
                layout.Validate();
                authoredPage = LobbyRuntimeTypography.Instantiate(layout.Page, Root, false);
                First = authoredPage.Bind(Category, Menu.Close, id => ShowCategory(id), note);
                footnote = authoredPage.Footnote;
                list = authoredPage.Content;
                authoredPage.Offers.gameObject.SetActive(Category != Extras);
            }
            else list = BuildLegacyShell();

            BuildOffers(list);
        }

        RectTransform BuildLegacyShell()
        {
            var body = Side("Shop");
            var tabs = LobbyKit.Row(body, "Shop categories", 8);
            tabs.Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -48), Vector2.zero);
            foreach (var choice in Categories)
            {
                string id = choice.id;
                var tab = LobbyKit.TextAction(tabs, "Shop category " + id, choice.label, 18,
                    () => ShowCategory(id), Category == id).Size(-1, 48, 1);
                if (Category == id) First = tab;
            }
            footnote = LobbyKit.Text(body, note ?? "Select to preview",
                16, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
            footnote.enableAutoSizing = true; footnote.fontSizeMin = 13; footnote.fontSizeMax = 16;
            footnote.rectTransform.Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 36));
            var view = LobbyKit.Rect(body, "Offers").Place(Vector2.zero, Vector2.one, new Vector2(0, 46), new Vector2(0, -62));
            var list = LobbyKit.Scroll(view, "Scroll", 12);
            list.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(4, 4, 8, 12);
            return list;
        }

        void BuildOffers(RectTransform list)
        {
            if (Category == Looks)
            {
                var looks = OfferGrid(list, "Looks");
                foreach (var bundle in CosmeticBundles.All) LookCard(looks, bundle);
            }
            else if (Category == Gear)
            {
                RecipeChooser(list);
                var finishes = OfferGrid(list, "Finishes");
                foreach (string skin in GameConfig.Current.Items.Get(recipe).Skins)
                    FinishCard(finishes, Career.Shop.FirstOrDefault(o => o.Kind == "skin" && o.Value == skin)
                        ?? new ShopOffer("skin", skin, skin, 0));
            }
            else if (Category == Colours)
            {
                var colours = OfferGrid(list, "Colours");
                foreach (var offer in Career.Shop.Where(o => o.Kind == "colour")) ColourCard(colours, offer);
            }
            else if (Category == Rooms)
            {
                var themes = OfferGrid(list, "Themes");
                foreach (var theme in LobbyThemes.All) ThemeCard(themes, theme);
            }
            else
            {
                var accessories = LobbyKit.Grid(list, "Accessories", new Vector2(248, 170), 16);
                foreach (string piece in new[] { "Cap", "Hood", "Glasses", "Satchel", "TBadge" }) Accessory(accessories, piece);
                WinterEmote(list);
            }
        }

        RectTransform OfferGrid(Transform parent, string name)
        {
            if (!authoredPage) return LobbyKit.Grid(parent, name, Card, 16);
            var grid = (RectTransform)authoredPage.Offers.transform;
            grid.name = name;
            grid.SetAsLastSibling();
            return grid;
        }

        static string CategoryFor(string offerId)
        {
            if (offerId == null) return Looks;
            if (offerId.StartsWith("skin:", StringComparison.Ordinal)) return Gear;
            if (offerId.StartsWith("colour:", StringComparison.Ordinal)) return Colours;
            if (offerId.StartsWith("theme:", StringComparison.Ordinal)) return Rooms;
            return Looks;
        }

        public bool ShowCategory(string id)
        {
            if (!Categories.Any(choice => choice.id == id)) return false;
            Category = id;
            spotlight = note = null;
            recipePicker = false;
            Refresh();
            Reselect("Shop category " + id);
            return true;
        }

        void WinterEmote(Transform parent)
        {
            var collection = Art.SeasonalCollection.Winter;
            if (!collection || !collection.emote) return;
            LobbyKit.SectionLabel(parent, "Move");
            var row = LobbyKit.Row(parent, "Winter emote", 8);
            row.Size(-1, 58);
            LobbyKit.TextAction(row, "Play winter shuffle", "SHUFFLE", 24, PlayWinterEmote,
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
            if (authoredPage)
            {
                bool owned = Menu.Career.Owns(offer.Kind, offer.Value);
                var view = LobbyRuntimeTypography.Instantiate(layout.Offer, parent, false);
                view.Bind(offer.Id, title, description, owned, worn, Menu.Career.PriceOf(offer), offer.Id == spotlight,
                    () => Preview(offer), owned ? () => Wear(offer) : () => Buy(offer));
                authoredOffers[offer.Id] = view;
                outlines[offer.Id] = (view.Outline, worn);
                if (!worn && (offer.Id == spotlight || !First)) First = view.Action.Button;
                art = view.Art;
                return view.Face;
            }
            var card = LobbyKit.Button(parent, "Offer " + offer.Id, Color.white, () => Preview(offer), 16, LobbyKit.Line, 2, 3);
            var face = card.Body();
            var outline = LobbyKit.Ring(face, offer.Id == spotlight ? LobbyKit.Sun : LobbyKit.Cyan, 16, 3);
            outline.enabled = worn || offer.Id == spotlight;
            outlines[offer.Id] = (outline, worn);
            art = LobbyKit.Rect(face, "Art").Place(new Vector2(0, 1), Vector2.one, new Vector2(10, -166), new Vector2(-10, -10));
            art.Paint(Color.Lerp(LobbyThemes.Current.Secondary, Color.white, .7f), 12).raycastTarget = false;
            var name = LobbyKit.Display(face, title, 23, LobbyKit.Navy);
            name.enableAutoSizing = true; name.fontSizeMin = 18; name.fontSizeMax = 23;
            name.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(10, -204), new Vector2(-10, -172));
            card.gameObject.AddComponent<LobbyHint>().Text = description.Replace("\n", " · ");
            ActionFor(face, offer, worn);
            return face;
        }

        void ActionFor(RectTransform face, ShopOffer offer, bool worn)
        {
            bool owned = Menu.Career.Owns(offer.Kind, offer.Value);
            int price = Menu.Career.PriceOf(offer);
            var action = LobbyKit.TextAction(face, (worn ? "Wearing " : owned ? "Wear " : "Buy ") + offer.Id,
                worn ? "EQUIPPED" : owned ? "EQUIP" : price == 0 ? "FREE" : price.ToString(), 21,
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
                if (authoredPage) image.gameObject.AddComponent<LobbyShopPictureCrop>();
                else
                {
                    float crop = Mathf.Min(1f, (Card.x - 20f) / 156f / ((float)theme.Background.width / theme.Background.height));
                    image.uvRect = new Rect((1f - crop) * .5f, 0, crop, 1);
                }
            }
        }

        void LookCard(Transform parent, CosmeticBundle bundle)
        {
            var offer = Career.Shop.FirstOrDefault(o => o.Id == "look:" + bundle.Id) ?? new ShopOffer("look", bundle.Id, bundle.Name, 0);
            var proposed = bundle.Apply(Menu.Outfit, GameConfig.Current.Wardrobe);
            bool worn = proposed != null && proposed.Serialize() == Menu.Outfit.Serialize();
            ArtCard(parent, offer, bundle.Name, bundle.Collection + "\n" + bundle.Description, worn, out var art);
            var portrait = Resources.Load<Texture2D>("UI/Shop/Looks/" + bundle.Id);
            if (portrait)
            {
                var image = LobbyKit.Rect(art, "Outfit portrait").Fill().gameObject.AddComponent<RawImage>();
                image.texture = portrait;
                image.raycastTarget = false;
                var fit = image.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = (float)portrait.width / portrait.height;
                return;
            }
            var colour = GameConfig.Current.Wardrobe.Colour("Top", bundle.TopColour);
            Blob(art, new Color(colour.R, colour.G, colour.B), 94);
            var icon = LobbyKit.Icon(art, LobbyIcons.Satchel, LobbyKit.Navy);
            icon.rectTransform.Pin(new Vector2(.75f, .35f), Vector2.zero, new Vector2(48, 48));
        }

        void FinishCard(Transform parent, ShopOffer offer)
        {
            bool worn = (Menu.Outfit.SkinFor(recipe) ?? "Classic") == offer.Value;
            ArtCard(parent, offer, offer.Value, "Finish for " + recipe, worn, out var art);
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
            Category = Gear;
            spotlight = note = null;
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
            foreach (var entry in outlines)
            {
                if (!entry.Value.outline) continue;
                if (authoredOffers.TryGetValue(entry.Key, out var authored) && authored)
                { authored.SetPreview(entry.Key == offer.Id); continue; }
                entry.Value.outline.enabled = entry.Value.worn || entry.Key == offer.Id;
                entry.Value.outline.color = entry.Key == offer.Id ? LobbyKit.Sun : LobbyKit.Cyan;
            }
            if (footnote) footnote.text = offer.Name + " · Preview";
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
