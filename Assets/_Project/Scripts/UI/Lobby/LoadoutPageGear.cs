using System;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed partial class LoadoutPage
    {
        public const string StylesRow = "Styles", FinishTiles = "Finish tiles", FinishPrefix = "Finish ", FinishState = "Gear finish state";
        public const string RecipePrefix = "Recipe ", MixedFinishes = "Mixed recipe finishes · choose below to match all.", AllRecipes = "All recipes: ";
        const float FinishTileHeight = 112f, FinishDiscSize = 44f, FinishNameHeight = 24f, FinishPriceHeight = 22f, FinishStateHeight = 30f;
        const float FinishText = 18f, FinishPriceText = 16f, FinishTop = 8f, FinishNameGap = 4f, FinishPriceBottom = 6f;
        public const float DetailCard = 72f;
        const float DetailCardGap = 12f, DetailArt = 56f, DetailPad = 14f, DetailEdge = 6f, DetailLetter = 32f, DetailWord = 268f;
        const float DetailLetterGap = 4f, BlurbText = 20f, FinishActionTop = 56f, FinishRowGap = 8f, FinishBlockBottom = 14f;
        const float RecipeShrinkFloor = .85f;
        const int DetailRadius = 18;
        float recipeShrink;

        ItemDefinition ShownRecipe()
        {
            var items = GameConfig.Current.Items.Enabled.ToList();
            return items.FirstOrDefault(i => i.Id == Pinned) ?? items.FirstOrDefault();
        }

        void Recipes(RectTransform body)
        {
            var shown = ShownRecipe();
            if (authoredPage)
            {
                recipeFinish = authoredPage.Finish.Label;
                detail = authoredPage.RecipeDetail;
                ShortenDetail(detail, (RectTransform)authoredPage.RecipeScroll.transform);
                FinishRow(authoredPage.RecipeScroll.content);
                FitRecipeRows(authoredPage.RecipeScroll, authoredPage.RecipeGrid);
                foreach (var item in GameConfig.Current.Items.Enabled) RecipeCard(authoredPage.RecipeGrid.transform, item, item == shown);
                Detail(shown);
                if (tryOn != null)
                {
                    authoredPage.Finish.gameObject.SetActive(false);
                    BuildGetBar(body, 0f);
                }
                return;
            }
            float foot = 0f;
            if (tryOn != null)
            {
                BuildGetBar(body, 0f);
                foot = BarHeight + BarGap;
            }
            var finish = LobbyKit.TextAction(body, "Recipe finish", "Finish", 18, CustomizeRecipe, icon: LobbyIcons.Chevron);
            ((RectTransform)finish.transform).Place(Vector2.zero, new Vector2(1, 0), new Vector2(0, foot), new Vector2(0, foot + 44));
            recipeFinish = finish.GetComponentInChildren<TMP_Text>();
            detail = LobbyKit.Rect(body, "Recipe detail").Place(Vector2.zero, new Vector2(1, 0), new Vector2(0, foot + FinishActionTop),
                new Vector2(0, foot + FinishActionTop + DetailCard));
            var view = LobbyKit.Rect(body, "Recipes").Place(Vector2.zero, Vector2.one, new Vector2(0, foot + FinishActionTop + DetailCard + DetailCardGap), Vector2.zero);
            var list = LobbyKit.Scroll(view, ScrollName, 0);
            list.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(4, 4, 8, 12);
            FinishRow(list);
            var grid = LobbyKit.Grid(list, "Grid", new Vector2(122, 118), 12);
            foreach (var item in GameConfig.Current.Items.Enabled) RecipeCard(grid, item, item == shown);
            Detail(shown);
        }

        static void ShortenDetail(RectTransform card, RectTransform scroll)
        {
            float spare = card.rect.height - DetailCard;
            if (spare <= 0f) return;
            card.offsetMax = new Vector2(card.offsetMax.x, card.offsetMax.y - spare);
            scroll.offsetMin = new Vector2(scroll.offsetMin.x, scroll.offsetMin.y - spare);
        }

        void FinishRow(RectTransform content)
        {
            var styles = LobbyKit.Column(content, StylesRow, FinishRowGap);
            styles.SetAsFirstSibling();
            styles.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(0, 0, 0, Mathf.RoundToInt(FinishBlockBottom));
            string wornSkin = SkinOf(Menu.Outfit);
            var state = LobbyKit.Text(styles, wornSkin == null ? MixedFinishes : AllRecipes + FinishName(wornSkin), CaptionText, LobbyKit.Muted,
                TextAlignmentOptions.MidlineLeft);
            state.name = FinishState;
            state.enableAutoSizing = true; state.fontSizeMin = MinText; state.fontSizeMax = CaptionText;
            state.Size(-1, FinishStateHeight);
            var row = LobbyKit.Row(styles, FinishTiles, 12);
            row.Size(-1, FinishTileHeight);
            var spread = row.GetComponent<HorizontalLayoutGroup>();
            spread.childForceExpandWidth = true;
            spread.padding = new RectOffset(0, Mathf.RoundToInt(SpareBesideRecipes(content)), 0, 0);
            string triedSkin = tryOn == null ? null : SkinOf(tryOn);
            foreach (var finish in CustomizeFinishes)
                FinishTile(row, finish, wornSkin == finish.Id, tryOn != null && triedSkin == finish.Id && wornSkin != finish.Id);
        }

        float SpareBesideRecipes(RectTransform content)
        {
            if (!authoredPage) return 0f;
            var grid = authoredPage.RecipeGrid;
            float cell = grid.cellSize.x, gap = grid.spacing.x, width = content.rect.width - grid.padding.horizontal;
            if (content.TryGetComponent(out LayoutGroup frame)) width -= frame.padding.horizontal;
            if (width <= cell) return 0f;
            int columns = Mathf.FloorToInt((width + gap) / (cell + gap));
            float spare = width - (columns * cell + (columns - 1) * gap);
            return spare > 0f && spare < cell ? spare : 0f;
        }

        public static float FittedRecipeCell(float view, float above, float cell, float gap)
        {
            float room = view - above + gap, pitch = cell + gap;
            if (room <= 0f || pitch <= 0f) return cell;
            int whole = Mathf.FloorToInt(room / pitch);
            if (whole * pitch >= room - .5f) return cell;
            float fitted = Mathf.Floor(room / (whole + 1) - gap);
            return fitted >= cell * RecipeShrinkFloor ? fitted : cell;
        }

        void FitRecipeRows(ScrollRect scroll, GridLayoutGroup grid)
        {
            var view = scroll.viewport ? scroll.viewport : (RectTransform)scroll.transform;
            if (view.rect.height <= 0f) return;
            float above = FinishStateHeight + FinishRowGap + FinishTileHeight + FinishBlockBottom + grid.padding.top;
            if (scroll.content.TryGetComponent(out VerticalLayoutGroup column)) above += column.padding.top + column.spacing;
            float cell = FittedRecipeCell(view.rect.height, above, grid.cellSize.y, grid.spacing.y);
            recipeShrink = grid.cellSize.y - cell;
            grid.cellSize = new Vector2(grid.cellSize.x, cell);
        }

        void FinishTile(Transform parent, Finish finish, bool worn, bool tried)
        {
            string skin = finish.Id;
            bool owned = Menu.Career.Owns(SkinKind, skin);
            var offer = owned ? null : Dressing.OfferFor(SkinKind + ":" + skin);
            var card = LobbyKit.Button(parent, FinishPrefix + skin, LobbyKit.Card, () => PickFinish(finish, owned), TileRadius, LobbyKit.Line, 1);
            card.Size(-1, -1, 1);
            var press = card.GetComponent<LobbyPress>();
            press.Lift = 3f; press.Tilt = 0f;
            var body = card.Body();
            FinishDisc(body, skin, FinishDiscSize).Pin(new Vector2(.5f, 1), new Vector2(0, -FinishTop), new Vector2(FinishDiscSize, FinishDiscSize));
            var name = LobbyKit.Text(body, finish.Name, FinishText, LobbyKit.Navy, TextAlignmentOptions.Center);
            name.name = CardName;
            name.enableAutoSizing = true; name.fontSizeMin = MinText; name.fontSizeMax = FinishText;
            float nameTop = FinishTop + FinishDiscSize + FinishNameGap;
            name.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(6, -(nameTop + FinishNameHeight)), new Vector2(-6, -nameTop));
            if (worn) WornMark(body);
            else if (tried) TriedMark(body);
            string price = "";
            if (!owned)
            {
                CornerDisc(body, LockMark, LobbyIcons.Lock, LobbyKit.Panel, LobbyKit.Navy, TopRight);
                if (offer != null)
                {
                    var tag = LobbyKit.Row(body, PricePill, 4);
                    tag.Place(Vector2.zero, new Vector2(1, 0), new Vector2(6, FinishPriceBottom), new Vector2(-6, FinishPriceBottom + FinishPriceHeight));
                    var layout = tag.GetComponent<HorizontalLayoutGroup>();
                    layout.childAlignment = TextAnchor.MiddleCenter;
                    layout.childForceExpandHeight = false;
                    LobbyKit.Coin(tag, 18);
                    string amount = Menu.Career.PriceOf(offer).ToString("N0", CultureInfo.InvariantCulture);
                    LobbyKit.Display(tag, amount, FinishPriceText, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft).Size(-1, FinishPriceHeight);
                    price = Separator + amount + " coins";
                }
            }
            if (IsNew(SkinKind, skin)) CornerDisc(body, NewMark, LobbyIcons.New, LobbyKit.PrimaryFill, LobbyKit.PrimaryInk, TopLeft);
            string tagline = string.IsNullOrEmpty(finish.Tagline) ? "" : Separator + finish.Tagline;
            card.gameObject.AddComponent<LobbyHint>().Text = finish.Name + tagline + price;
            var face = card.FaceOf();
            press.Hot = hot =>
            {
                face.color = hot ? LobbyKit.Raised : LobbyKit.Card;
                if (hot) { previewing = FinishPrefix + skin; HoldFinish(skin); }
                else if (previewing == FinishPrefix + skin) { previewing = null; HoldShown(); }
            };
        }

        void PickFinish(Finish finish, bool owned)
        {
            Disarm();
            string name = FinishPrefix + finish.Id;
            if (!owned)
            {
                StartTryOn(WithFinish(Shown, finish.Id), name);
                return;
            }
            Menu.Wear(WithFinish(Menu.Outfit, finish.Id));
            if (tryOn != null) tryOn = WithFinish(tryOn, finish.Id);
            Rebuild();
            Reselect(name);
        }

        void HoldShown()
        {
            var item = ShownRecipe();
            if (item == null || !Stage) return;
            if (!Stage.HoldItem(item.Id, Appearance.Pick(item, Shown.SkinFor(item.Id)))) Stage.ClearHeldItem();
        }

        void HoldRecipe(ItemDefinition item)
        {
            if (item != null && Stage) Stage.HoldItem(item.Id, Appearance.Pick(item, Shown.SkinFor(item.Id)));
        }

        void HoldFinish(string skin)
        {
            if (!Stage) return;
            var item = ShownRecipe();
            if (item != null && item.HasSkin(skin)) Stage.HoldItem(item.Id, skin);
            else Stage.HoldItem(ShowcaseItem(skin), skin);
        }

        void PreviewRecipe(ItemDefinition item, bool hot)
        {
            if (hot)
            {
                previewing = item.Id;
                Detail(item);
                HoldRecipe(item);
            }
            else if (previewing == item.Id)
            {
                previewing = null;
                Detail(ShownRecipe());
                HoldShown();
            }
        }

        void RecipeCard(Transform parent, ItemDefinition item, bool on)
        {
            if (authoredPage)
            {
                var savedTile = LobbyRuntimeTypography.Instantiate(layout.Recipe, parent, false);
                savedTile.Bind(item.Id, on, () => PinRecipe(item.Id), hot => PreviewRecipe(item, hot));
                if (recipeShrink > 0f) savedTile.Icon.rectTransform.sizeDelta -= Vector2.one * recipeShrink;
                if (on) First = savedTile.Button;
                return;
            }
            var card = LobbyKit.Button(parent, RecipePrefix + item.Id, LobbyKit.Card, () => PinRecipe(item.Id), 14, LobbyKit.Line, 1, 0);
            var press = card.GetComponent<LobbyPress>();
            press.Lift = 4f; press.Tilt = 2f;
            var body = card.Body();
            if (on)
            {
                LobbyKit.Ring(body, LobbyKit.Selection, 14, 2);
                First = card;
            }
            var icon = LobbyKit.ItemImage(body, item.Id, 64);
            if (icon) icon.rectTransform.Pin(new Vector2(.5f, 1), new Vector2(0, -10), new Vector2(64, 64));
            string word = item.Id;
            float tile = Mathf.Min(24f, (112f - 2f * (word.Length - 1)) / word.Length);
            var letters = LobbyKit.Row(body, "Letters", 2);
            letters.Place(Vector2.zero, new Vector2(1, 0), new Vector2(4, 12), new Vector2(-4, 12 + tile + 4));
            var row = letters.GetComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childForceExpandHeight = false;
            foreach (char letter in word) LobbyKit.LetterTile(letters, letter, tile);
            press.Hot = hot => PreviewRecipe(item, hot);
        }

        public void PinRecipe(string id)
        {
            Pinned = id;
            Rebuild();
            Reselect(RecipePrefix + id);
        }

        void CustomizeRecipe()
        {
            string id = shownRecipe;
            if (id == null) return;
            Menu.Open(LobbyMenu.Shop);
            Menu.Page<ShopPage>().SelectRecipe(id);
        }

        void Detail(ItemDefinition item)
        {
            if (!detail || item == null) return;
            shownRecipe = item.Id;
            string skin = Appearance.Pick(item, Shown.SkinFor(item.Id));
            if (recipeFinish) recipeFinish.text = "Finish · " + FinishName(skin);
            LobbyKit.Clear(detail);
            var strip = LobbyKit.Rect(detail, "Strip").Fill();
            LobbyKit.Face(strip, LobbyKit.Track, DetailRadius);
            var art = LobbyKit.Rect(strip, "Art").Pin(new Vector2(0, .5f), new Vector2(DetailPad, 0), new Vector2(DetailArt, DetailArt));
            FinishedItem(art, item.Id, skin, DetailArt);
            var words = LobbyKit.Row(strip, "Words", DetailPad);
            words.Place(Vector2.zero, Vector2.one, new Vector2(DetailPad * 2f + DetailArt, DetailEdge), new Vector2(-DetailPad, -DetailEdge));
            var line = words.GetComponent<HorizontalLayoutGroup>();
            line.childAlignment = TextAnchor.MiddleLeft;
            line.childForceExpandHeight = false;
            string word = item.Id;
            float size = Mathf.Min(DetailLetter, (DetailWord - DetailLetterGap * (word.Length - 1)) / word.Length);
            var letters = LobbyKit.Row(words, "Word", DetailLetterGap);
            letters.Size(size * word.Length + DetailLetterGap * (word.Length - 1), size + DetailEdge);
            letters.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            for (int i = 0; i < word.Length; i++) LobbyKit.LetterTile(letters, word[i], size, (i - (word.Length - 1) / 2f) * 2f);
            var blurb = LobbyKit.Display(words, Blurb(item), BlurbText, LobbyKit.Cream, TextAlignmentOptions.MidlineLeft);
            blurb.name = "Blurb";
            blurb.textWrappingMode = TextWrappingModes.Normal;
            blurb.enableAutoSizing = true; blurb.fontSizeMin = MinText; blurb.fontSizeMax = BlurbText;
            blurb.Size(-1, DetailCard - DetailEdge * 2f, 1);
            blurb.GetComponent<LayoutElement>().minWidth = 0;
        }

        public static string Blurb(ItemDefinition item)
        {
            if (item.Use?.Effect == UseEffect.Heal)
                return $"Restore {item.Use.Amount:0} HP · {item.Use.ChannelSeconds:0.##}s to use";
            if (item.Use?.Effect == UseEffect.Speed)
                return $"{item.Use.Amount:0.##}× speed · {item.Use.Seconds:0.#} seconds";
            if (item.Shield?.FrontArcDegrees >= 360f) return "All-around block · hold to raise";
            if (item.Deploy?.Effect == DeployEffect.WindField) return "Forward gust · pushes everyone";
            if (item.Deploy?.Effect == DeployEffect.SlowField) return "Slowing field · affects everyone";
            if (item.Thrown?.FuseSeconds > 0f) return "Delayed blast · clear the room";
            if (item.Consumable && item.Thrown != null) return "One throw · one splat";
            return Blurb(item.Family);
        }

        public static string Blurb(HandlingFamily family) => family switch
        {
            HandlingFamily.MeleeSwing => "A satisfying swing",
            HandlingFamily.MeleeThrust => "A little extra reach",
            HandlingFamily.Thrown => "Catch. Throw. Repeat.",
            HandlingFamily.Buff => "A protective bubble",
            HandlingFamily.Shield => "Frontal block · hold to raise",
            HandlingFamily.DeployPad => "A bouncy jump pad",
            HandlingFamily.DeploySpeed => "A speedy little shortcut",
            HandlingFamily.DeployZone => "A slippery surprise",
            HandlingFamily.DeployCover => "Make your own cover",
            HandlingFamily.Heal => "Patch yourself up",
            HandlingFamily.Ranged => "Hit them from afar",
            _ => "Handy around the house",
        };
    }
}
