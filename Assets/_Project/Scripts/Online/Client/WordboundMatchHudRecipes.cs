using System;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.UI;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundMatchHud
    {
        string selectedRecipe;
        int recipeColumns = 1, recipeStyleGeneration = -1;
        bool recipeScrollActive;
        GUIStyle recipeName;
        Rect recipeViewportRect, recipeDetailRect, recipeReturnRect;

        bool RecipeVisible(RecipeCard card) => card.Item != null &&
            card.Item.Id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        RecipeCard SelectedRecipe()
        {
            foreach (var card in recipeCards)
                if (card.Item.Id == selectedRecipe && RecipeVisible(card)) return card;
            return default;
        }

        void NormalizeRecipeSelection()
        {
            if (SelectedRecipe().Item != null) return;
            selectedRecipe = null;
            foreach (var card in recipeCards)
                if (RecipeVisible(card)) { selectedRecipe = card.Item.Id; break; }
        }

        void SelectRecipe(string id)
        {
            foreach (var card in recipeCards)
                if (card.Item.Id == id && RecipeVisible(card)) { selectedRecipe = id; return; }
        }

        bool CanCraftRecipe(RecipeCard card) => card.Item != null && RecipeVisible(card) &&
            controller && controller.Fault == "" && controller.IsPresentationReady && !controller.IsSimulationPaused &&
            frame != null && frame.Progress.Result == null && actor != null && actor.Health > 0 &&
            inventory != null && inventory.CanAct && inventory.Craft == null && card.Craftable &&
            inventory.Letters.Contains(card.Item.Letters);

        void CraftSelectedRecipe()
        {
            var card = SelectedRecipe();
            if (CanCraftRecipe(card) && Send(WordboundAction.BeginCraft, value: card.Item.Id)) Resume();
        }

        string RecipeDetail(RecipeCard card)
        {
            if (card.Item == null) return "Choose a recipe";
            if (!card.Craftable) return card.Item.Id + " · Need " + card.Missing;
            if (inventory?.Craft != null) return card.Item.Id + " · Crafting…";
            return card.Item.Id;
        }

        void RecipeGrid(float width, float height)
        {
            if (recipeStyleGeneration != ui.Generation)
            {
                recipeName = new GUIStyle(ui.SingleLine) { alignment = TextAnchor.MiddleCenter };
                recipeStyleGeneration = ui.Generation;
            }
            float available = Mathf.Max(100, width - ui.Padding * 2 - 24);
            float minimum = 112;
            foreach (var card in recipeCards)
                minimum = Mathf.Max(minimum, recipeName.CalcSize(new GUIContent(card.Item.Id)).x + 24);
            recipeColumns = Mathf.Clamp(Mathf.FloorToInt((available + ui.Gap) / (minimum + ui.Gap + 4)), 1, 4);
            float tileWidth = Mathf.Max(1, (available - (recipeColumns - 1) * (ui.Gap + 4) - 4) / recipeColumns);
            float tileHeight = (height < 600 ? 52 : 78) + ui.LabelHeight + 18;
            int visible = 0;
            foreach (var card in recipeCards)
            {
                if (!RecipeVisible(card)) continue;
                if (visible % recipeColumns == 0) GUILayout.BeginHorizontal();
                else GUILayout.Space(ui.Gap);
                RecipeTile(card, tileWidth, tileHeight);
                visible++;
                if (visible % recipeColumns == 0) { GUILayout.FlexibleSpace(); GUILayout.EndHorizontal(); }
            }
            if (visible % recipeColumns != 0) { GUILayout.FlexibleSpace(); GUILayout.EndHorizontal(); }
            if (visible == 0) GUILayout.Label("No matching recipes.", ui.Wrapped);
        }

        void RecipeTile(RecipeCard card, float width, float height)
        {
            string id = card.Item.Id;
            if (MenuButton("recipe:" + id, "", GUILayout.Width(width), GUILayout.Height(height)))
                QueueMenuAction(() => SelectRecipe(id));
            if (Event.current.type != EventType.Repaint) return;
            Rect tile = GUILayoutUtility.GetLastRect();
            var picture = new Rect(tile.x + 8, tile.y + 6, tile.width - 16, tile.height - ui.LabelHeight - 18);
            var texture = OriginalItemArt.Find(id);
            Color before = GUI.color;
            try
            {
                if (!card.Craftable) GUI.color = new Color(before.r, before.g, before.b, before.a * .6f);
                if (texture) GUI.DrawTexture(FitRecipeIcon(picture, texture.width, texture.height), texture, ScaleMode.StretchToFill, true);
            }
            finally { GUI.color = before; }
            GUI.Label(new Rect(tile.x + 4, tile.yMax - ui.LabelHeight - 8, tile.width - 8, ui.LabelHeight),
                selectedRecipe == id ? "• " + id : id, recipeName);
        }

        static Rect FitRecipeIcon(Rect area, float width, float height)
        {
            float scale = Mathf.Min(area.width / Mathf.Max(1, width), area.height / Mathf.Max(1, height));
            var size = new Vector2(width * scale, height * scale);
            return new Rect(area.center - size * .5f, size);
        }

        void RecipeSelectionDetail()
        {
            var card = SelectedRecipe();
            GUILayout.BeginHorizontal();
            GUILayout.Label(RecipeDetail(card), ui.Wrapped, GUILayout.MinHeight(ui.RowHeight));
            bool before = GUI.enabled;
            GUI.enabled = before && CanCraftRecipe(card);
            if (MenuButton("craft-selected", "Craft", GUILayout.Width(Mathf.Max(92, ui.SingleLine.CalcSize(new GUIContent("Craft")).x + 28)),
                GUILayout.MinHeight(ui.RowHeight))) QueueMenuAction(CraftSelectedRecipe);
            GUI.enabled = before;
            GUILayout.EndHorizontal();
            if (Event.current.type == EventType.Repaint) recipeDetailRect = GUILayoutUtility.GetLastRect();
        }
    }
}
