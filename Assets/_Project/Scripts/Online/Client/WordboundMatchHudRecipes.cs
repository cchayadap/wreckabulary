using System;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.UI;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundMatchHud
    {
        string selectedRecipe, recipeContext;
        int recipeColumns = 1, recipeStyleGeneration = -1;
        bool recipeScrollActive;
        GUIStyle recipeName;
        Rect recipeViewportRect, recipeDetailRect, recipeReturnRect, recipeCraftRect, recipePreviewImageRect;

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

        void PrepareRecipeMenu()
        {
            NormalizeRecipeSelection();
            if (search.Length != 0 || inventory == null || inventory.Craft != null) return;
            var suggestion = RecipeSuggestion();
            if (suggestion.Recipe == null || suggestion.Context == recipeContext) return;
            if (!recipeCards.Any(card => card.Item.Id == suggestion.Recipe && RecipeVisible(card))) return;
            SelectRecipe(suggestion.Recipe);
            recipeContext = suggestion.Context;
            revealMenuSelection = true;
        }

        (string Context, string Recipe) RecipeSuggestion()
        {
            if (course == null || frame == null || mode == null || course.Play.ModeId != mode.Id ||
                frame.Binding.ModeHash != mode.Hash || frame.Binding.ManifestHash != course.Hash ||
                frame.Binding.CourseHash != course.SourceHash || frame.Progress.Migrating || frame.Progress.Result != null)
                return default;
            var player = frame.Progress.Players.SingleOrDefault(p => p.Id == actorId);
            if (player == null || !player.Connected || player.Returned || player.Eliminated ||
                !frame.Binding.Seats.Any(s => s.PlayerId == actorId && s.Team == player.Team)) return default;
            ResolvedCoursePhysicalSite site = null;
            if (mode.Id == "Freestyle")
            {
                site = WordboundPracticePolicy.Selected(course, frame.Practice);
                if (site == null || WordboundPracticeGuidePlan.Completed(frame, site))
                    site = WordboundPracticeGuidePlan.Nearest(course, frame, actorId, 20);
                if (site == null || WordboundPracticeGuidePlan.Completed(frame, site)) return default;
            }
            else if (mode.IsRace)
            {
                var progress = frame.Progress.Teams.SingleOrDefault(t => t.Team == player.Team);
                if (progress == null || progress.Finished || progress.Lap < 0 || progress.Lap >= course.Laps.Count ||
                    progress.NextCheckpoint < 0 || progress.NextCheckpoint > course.Laps[progress.Lap].Checkpoints.Count)
                    return default;
                var lap = course.Laps[progress.Lap];
                foreach (var checkpoint in lap.Checkpoints.Skip(progress.NextCheckpoint))
                {
                    foreach (string id in checkpoint.PuzzleIds.Where(id => !progress.CompletedPuzzles.Contains(id)))
                    {
                        site = course.PhysicalSites.FirstOrDefault(s => s.Lap == lap.Number && s.PuzzleId == id && s.CheckpointId == checkpoint.Id);
                        if (site != null) break;
                    }
                    if (site != null) break;
                }
            }
            if (site == null) return default;
            var puzzle = course.Puzzles.SingleOrDefault(p => p.Id == site.PuzzleId);
            if (puzzle == null || !mode.Recipes.Contains(puzzle.RecipeId)) return default;
            string recipe = puzzle.RecipeId;
            if (puzzle.PresetId == "CollectLetters" && mode.Recipes.Contains(site.ToolRecipeId) &&
                !frame.Economy.Items.Any(held => held.Word == site.ToolRecipeId && held.State == ItemState.Held &&
                    held.HolderId == seat && held.Durability > 0 && !held.Spent))
            {
                var sources = course.Placements.Where(p => p.ZoneId == site.PropSourceZoneId &&
                    (p.TeamIndex < 0 || p.TeamIndex == player.Team)).Select(p => p.Id).ToArray();
                if (frame.Bodies.Any(b => b.Kind == WorldBodyKind.Item && b.Active && sources.Contains(b.SourceId)))
                    recipe = site.ToolRecipeId;
            }
            return (course.Hash + ":" + player.Team + ":" + site.Hash + ":" + recipe, recipe);
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

        void RecipeWorkspace(Rect body)
        {
            float gap = ui.Gap * 2 + 8;
            float listWidth = Mathf.Floor((body.width - gap) * .46f);
            var list = new Rect(body.x, body.y, listWidth, body.height);
            var preview = new Rect(list.xMax + gap, body.y, body.width - listWidth - gap, body.height);
            var craft = new Rect(preview.x, preview.yMax - ui.RowHeight - 4, preview.width, ui.RowHeight + 4);
            var inspection = new Rect(preview.x, preview.y, preview.width, Mathf.Max(1, craft.y - preview.y - ui.Gap));

            GUILayout.BeginArea(list);
            recipeScroll = GUILayout.BeginScrollView(recipeScroll);
            recipeScrollActive = true;
            EquipmentMenu();
            GUI.enabled = true;
            GUILayout.Space(ui.Gap);
            GUILayout.Label("Letters  " + (inventory.Letters.Count == 0 ? "—" : inventory.Letters.ToString()), ui.Wrapped);
            GUILayout.Label("Find a recipe", ui.SingleLine);
            GUILayout.BeginHorizontal();
            string nextSearch = GUILayout.TextField(pendingSearch ?? search, 32, GUILayout.MinHeight(ui.RowHeight));
            if (nextSearch != search) pendingSearch = nextSearch;
            if (search.Length > 0 && MenuButton("clear-search", "Clear", GUILayout.Width(Mathf.Max(56,
                ui.SingleLine.CalcSize(new GUIContent("Clear")).x + 16)), GUILayout.MinHeight(ui.RowHeight)))
                QueueMenuAction(() => search = "");
            GUILayout.EndHorizontal();
            GUILayout.Space(ui.Gap);
            RecipeGrid(list.width, body.height);
            recipeScrollActive = false;
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            GUILayout.BeginArea(inspection);
            RecipePreview(inspection.width - 4, inspection.height);
            GUILayout.EndArea();
            GUILayout.BeginArea(craft);
            RecipeSelectionDetail();
            GUILayout.EndArea();
            if (Event.current.type == EventType.Repaint)
            {
                recipeViewportRect = list;
                recipeViewportHeight = Mathf.Max(30, list.height);
                recipeDetailRect = preview;
                recipeCraftRect = craft;
            }
        }

        void RecipeGrid(float width, float height)
        {
            if (recipeStyleGeneration != ui.Generation)
            {
                recipeName = new GUIStyle(ui.SingleLine) { alignment = TextAnchor.MiddleLeft };
                recipeStyleGeneration = ui.Generation;
            }
            recipeColumns = 1;
            float tileWidth = Mathf.Max(1, width - 24);
            float tileHeight = Mathf.Max(52, ui.RowHeight + 12);
            int visible = 0;
            foreach (var card in recipeCards)
            {
                if (!RecipeVisible(card)) continue;
                RecipeTile(card, tileWidth, tileHeight);
                visible++;
            }
            if (visible == 0) GUILayout.Label("No matching recipes.", ui.Wrapped);
        }

        void RecipeTile(RecipeCard card, float width, float height)
        {
            string id = card.Item.Id;
            if (MenuButton("recipe:" + id, "", GUILayout.Width(width), GUILayout.Height(height)))
                QueueMenuAction(() => SelectRecipe(id));
            if (Event.current.type != EventType.Repaint) return;
            Rect tile = GUILayoutUtility.GetLastRect();
            float iconSize = Mathf.Min(44, tile.height - 12);
            var picture = new Rect(tile.x + 12, tile.y + (tile.height - iconSize) * .5f, iconSize, iconSize);
            var texture = OriginalItemArt.Find(id);
            if (texture) GUI.DrawTexture(FitRecipeIcon(picture, texture.width, texture.height), texture, ScaleMode.StretchToFill, true);
            GUI.Label(new Rect(picture.xMax + 12, tile.y, Mathf.Max(1, tile.xMax - picture.xMax - 16), tile.height), id, recipeName);
        }

        void RecipePreview(float width, float height)
        {
            var card = SelectedRecipe();
            if (card.Item == null)
            {
                GUILayout.Label("Choose a recipe", ui.Heading);
                return;
            }
            string status = inventory?.Craft != null ? "Crafting…" : !card.Craftable ? "Missing " + card.Missing : "Letters ready";
            string blurb = LoadoutPage.Blurb(card.Item);
            float titleHeight = ui.Heading.CalcHeight(new GUIContent(card.Item.Id), width);
            float statusHeight = ui.Wrapped.CalcHeight(new GUIContent(status), width);
            float blurbHeight = ui.Wrapped.CalcHeight(new GUIContent(blurb), width);
            bool showBlurb = height >= titleHeight + statusHeight + blurbHeight + ui.Gap * 4 + 100;
            float reserved = titleHeight + statusHeight + ui.Gap * 2 + 20 + (showBlurb ? blurbHeight + ui.Gap : 0);
            GUILayout.Label(card.Item.Id, ui.Heading, GUILayout.Height(titleHeight));
            float imageHeight = Mathf.Clamp(height - reserved, 32, 236);
            Rect picture = GUILayoutUtility.GetRect(Mathf.Max(1, width), imageHeight, GUILayout.ExpandWidth(true));
            var texture = OriginalItemArt.Find(card.Item.Id);
            if (Event.current.type == EventType.Repaint && texture)
            {
                recipePreviewImageRect = FitRecipeIcon(picture, texture.width, texture.height);
                GUI.DrawTexture(recipePreviewImageRect, texture, ScaleMode.StretchToFill, true);
            }
            GUILayout.Space(ui.Gap);
            if (showBlurb)
            {
                GUILayout.Label(blurb, ui.Wrapped, GUILayout.Height(blurbHeight));
                GUILayout.Space(ui.Gap);
            }
            GUILayout.Label(status, ui.Wrapped, GUILayout.Height(statusHeight));
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
            bool before = GUI.enabled;
            GUI.enabled = before && CanCraftRecipe(card);
            if (MenuButton("craft-selected", "Craft", GUILayout.MinHeight(ui.RowHeight), GUILayout.ExpandWidth(true)))
                QueueMenuAction(CraftSelectedRecipe);
            GUI.enabled = before;
        }
    }
}
