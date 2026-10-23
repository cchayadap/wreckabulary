using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundMatchHud
    {
        readonly WordboundMenuFocus menuFocus = new WordboundMenuFocus();
        List<RecipeCard> recipeCards = new List<RecipeCard>();
        LetterBag recipeLetters;
        GUIStyle wrappedLabel;
        bool revealMenuSelection, menuHasFocus;
        Action pendingMenuAction;
        string pendingSearch;
        string menuConnectionStatus = "", menuResultStatus = "";
        float recipeViewportHeight = 180;
        GUIStyle WrappedLabel => wrappedLabel ??= new GUIStyle(GUI.skin.label) { wordWrap = true };

        void QueueMenuAction(Action action)
        {
            if (pendingMenuAction == null) pendingMenuAction = action;
        }

        void ExecuteMenuAction(bool focused)
        {
            var action = pendingMenuAction;
            string nextSearch = pendingSearch;
            pendingMenuAction = null;
            pendingSearch = null;
            if (focused && view && view.MenuOpen)
            {
                if (nextSearch != null) search = nextSearch;
                action?.Invoke();
            }
        }

        void ResetMenuInput()
        {
            pendingMenuAction = null;
            pendingSearch = null;
            menuFocus.Reset();
        }

        void RefreshRecipes()
        {
            if (inventory == null || inventory.Letters.Equals(recipeLetters)) return;
            recipeLetters = new LetterBag(inventory.Letters);
            recipeCards = items.Cards(recipeLetters);
            recipeCards.RemoveAll(card => !mode.Recipes.Contains(card.Item.Id));
        }

        string FirstCraftableRecipe()
        {
            foreach (var card in recipeCards)
                if (card.Craftable && inventory?.Craft == null) return "recipe:" + card.Item.Id;
            return "resume";
        }

        ItemInstance HeldItem(int slot)
        {
            if (frame == null || inventory == null || slot < 0 || slot >= inventory.Slots.Length) return null;
            foreach (var item in frame.Economy.Items)
                if (item.Id == inventory.Slots[slot] && item.State == ItemState.Held && item.HolderId == seat) return item;
            return null;
        }

        void ReadMenuCommands(WordboundMenuCommands commands)
        {
            if (!initialized || !view.MenuOpen) return;
            if (commands.Back)
            {
                ResetMenuInput();
                if (journalOpen) { journalOpen = false; recipeScroll = Vector2.zero; menuFocus.Reset("journal"); }
                else if (controller.Fault == "") Resume();
                return;
            }
            if (commands.Step != 0)
            {
                menuFocus.Move(commands.Step);
                revealMenuSelection = true;
            }
            if (commands.Confirm) menuFocus.Confirm();
            if (Mathf.Abs(commands.Scroll) > .25f)
                recipeScroll.y = Mathf.Max(0, recipeScroll.y - commands.Scroll * 420 * Time.unscaledDeltaTime);
        }

        void Resume()
        {
            view.SetMenuOpen(false);
            crafting = false;
            ResetMenuInput();
        }

        bool MenuButton(string id, string label, params GUILayoutOption[] options)
        {
            if (Event.current.type == EventType.Layout && GUI.enabled) menuFocus.Register(id);
            bool selected = view.UsesGamepad && menuFocus.Selected == id;
            var style = selected ? ui.Selected : id == "resume" ? ui.Primary : GUI.skin.button;
            GUI.SetNextControlName("wordbound:" + id);
            bool clicked = options.Length == 0
                ? ui.Button(new GUIContent((selected ? "› " : "") + label), style, GUILayout.MinHeight(ui.RowHeight))
                : ui.Button(new GUIContent((selected ? "› " : "") + label), style, options);
            if (clicked) menuFocus.CancelConfirmation();
            if (selected && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl("wordbound:" + id);
                if (revealMenuSelection && id.StartsWith("recipe:", StringComparison.Ordinal))
                {
                    var row = GUILayoutUtility.GetLastRect();
                    if (row.yMin < recipeScroll.y) recipeScroll.y = row.yMin;
                    else if (row.yMax > recipeScroll.y + recipeViewportHeight)
                        recipeScroll.y = Mathf.Max(0, row.yMax - recipeViewportHeight);
                    revealMenuSelection = false;
                }
            }
            return clicked || menuHasFocus && view.UsesGamepad && GUI.enabled &&
                Event.current.type == EventType.Repaint && menuFocus.Consume(id);
        }

        void Menu(float width, float height, bool focused)
        {
            using var themeScope = ui.Apply(Theme);
            menuHasFocus = focused;
            if (!view.UsesGamepad) menuFocus.CancelConfirmation();
            if (Event.current.type == EventType.Layout)
            {
                menuFocus.BeginLayout();
                menuConnectionStatus = controller.Fault != "" ? "Connection interrupted · " + returnLabel + " to continue." :
                    controller.IsSimulationPaused ? "Waiting for the host…" : "";
                menuResultStatus = frame?.Progress.Result != null ? ResultText(frame.Progress.Result) : "";
            }
            GUILayout.BeginArea(ui.MenuRect(width, height), ui.Panel);
            GUILayout.Label(journalOpen ? "Collected clues" : crafting ? "Spell equipment" : "Wordbound", ui.Heading);
            if (menuConnectionStatus.Length != 0) GUILayout.Label(menuConnectionStatus, ui.Wrapped);
            if (menuResultStatus.Length != 0) GUILayout.Label(menuResultStatus, ui.Wrapped);
            GUILayout.Label(view.UsesGamepad
                ? "D-pad / left stick: select · A: confirm · B: back · Right stick: scroll"
                : "Choose equipment, craft a word, or return to your game.", ui.Wrapped);
            GUI.enabled = controller.Fault == "";
            if (MenuButton("resume", "Resume", GUILayout.MinHeight(ui.RowHeight))) QueueMenuAction(Resume);
            GUI.enabled = true;
            if (story && story.Journal.Count > 0)
            {
                GUILayout.BeginHorizontal();
                if (MenuButton("equipment", "Equipment")) QueueMenuAction(() => { journalOpen = false; recipeScroll = Vector2.zero; });
                if (MenuButton("journal", "Story clues · " + story.Journal.Count))
                    QueueMenuAction(() => { journalOpen = true; recipeScroll = Vector2.zero; });
                GUILayout.EndHorizontal();
            }
            GUI.enabled = frame != null && !controller.IsSimulationPaused;
            if (journalOpen && story)
            {
                recipeScroll = GUILayout.BeginScrollView(recipeScroll);
                foreach (var clue in story.Journal)
                {
                    GUILayout.Label("Lap " + clue.Lap + " · " + clue.Chapter);
                    GUILayout.Label(clue.Place + ": " + clue.Clue, ui.Wrapped);
                    GUILayout.Space(8);
                }
                GUILayout.EndScrollView();
            }
            else if (inventory != null)
            {
                EquipmentMenu();
                GUILayout.Label("Recipes · letters: " + inventory.Letters);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Search", ui.SingleLine,
                    GUILayout.Width(Mathf.Ceil(ui.SingleLine.CalcSize(new GUIContent("Search")).x)), GUILayout.MinHeight(ui.RowHeight));
                string nextSearch = GUILayout.TextField(pendingSearch ?? search, 32, GUILayout.MinHeight(ui.RowHeight));
                if (nextSearch != search) pendingSearch = nextSearch;
                if (search.Length > 0 && MenuButton("clear-search", "Clear", GUILayout.Width(64), GUILayout.MinHeight(ui.RowHeight))) QueueMenuAction(() => search = "");
                GUILayout.EndHorizontal();
                recipeScroll = GUILayout.BeginScrollView(recipeScroll);
                int visible = 0;
                foreach (var card in recipeCards)
                {
                    if (card.Item.Id.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    visible++;
                    bool ready = card.Craftable && inventory.Craft == null;
                    GUI.enabled = !controller.IsSimulationPaused && ready;
                    if (MenuButton("recipe:" + card.Item.Id,
                        card.Item.Id + (ready ? "  ·  Craft" : card.Craftable ? "  ·  Crafting…" : "  ·  Need " + card.Missing),
                        GUILayout.MinHeight(ui.RowHeight)))
                    {
                        QueueMenuAction(() => { if (Send(WordboundAction.BeginCraft, value: card.Item.Id)) Resume(); });
                    }
                }
                GUI.enabled = true;
                if (visible == 0) GUILayout.Label("No recipes match. Clear the search to see all equipment.", ui.Wrapped);
                GUILayout.EndScrollView();
                if (Event.current.type == EventType.Repaint)
                    recipeViewportHeight = Mathf.Max(30, GUILayoutUtility.GetLastRect().height);
            }
            GUI.enabled = true;
            if (MenuButton("return", returnLabel, GUILayout.MinHeight(ui.RowHeight))) QueueMenuAction(() => ReturnRequested?.Invoke());
            GUILayout.EndArea();
            if (Event.current.type == EventType.Layout) menuFocus.EndLayout();
        }

        void EquipmentMenu()
        {
            var nearby = WordboundPracticeGuidePlan.Nearest(course, frame, actorId);
            if (nearby != null)
            {
                GUILayout.Label(WordboundPracticeGuidePlan.Status(frame, nearby), ui.Wrapped);
                if (!WordboundPracticeGuidePlan.Completed(frame, nearby) && frame.Practice.SiteHash != nearby.Hash &&
                    MenuButton("practice", "Practise " + course.Puzzles.Single(p => p.Id == nearby.PuzzleId).RecipeId + " station"))
                    QueueMenuAction(() => { if (Send(WordboundAction.PracticePuzzle, nearby.Hash)) Resume(); });
            }
            if (inventory.Craft != null)
            {
                GUILayout.Label("Crafting " + inventory.Craft.ItemId);
                if (MenuButton("cancel-craft", "Cancel craft")) QueueMenuAction(() => Send(WordboundAction.CancelCraft));
            }
            GUILayout.BeginHorizontal();
            if (MenuButton("fists", actor.EquippedSlot < 0 ? "• Fists" : "Fists")) QueueMenuAction(() => Send(WordboundAction.Equip, slot: -1));
            bool usable = GUI.enabled;
            for (int i = 0; i < inventory.Slots.Length; i++)
            {
                var item = HeldItem(i);
                GUI.enabled = usable && item != null;
                if (MenuButton("slot:" + i, (actor.EquippedSlot == i ? "• " : "") + (i + 1) + ": " + (item?.Word ?? "Empty")))
                {
                    int slot = i;
                    QueueMenuAction(() => Send(WordboundAction.Equip, slot: slot));
                }
            }
            GUI.enabled = usable;
            GUILayout.EndHorizontal();
            if (inventory.CarriedFurniture >= 0 && MenuButton("drop-furniture", "Put down carried furniture"))
                QueueMenuAction(() => Send(WordboundAction.Drop));
            if (actor.EquippedSlot >= 0)
            {
                GUILayout.BeginHorizontal();
                int selectedSlot = actor.EquippedSlot;
                if (MenuButton("place", "Place selected item")) QueueMenuAction(() => Send(WordboundAction.Deploy, slot: selectedSlot));
                if (MenuButton("drop", "Drop selected item")) QueueMenuAction(() => Send(WordboundAction.Drop, slot: selectedSlot));
                GUILayout.EndHorizontal();
            }
            if ((mode.IsRace || mode.Id == "Freestyle") && frame.Progress.Teams.Single(t => t.Team == team).KeepsakeCarrier == actorId &&
                MenuButton("drop-keepsake", "Put down keepsake")) QueueMenuAction(() => Send(WordboundAction.DropKeepsake));
        }
    }
}
