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
        string menuConnectionStatus = "", menuResultStatus = "", menuActionStatus = "";
        IReadOnlyList<WordboundStoryNotice> menuJournal = Array.Empty<WordboundStoryNotice>();
        WordboundStoryEpilogue menuEpilogue;
        float recipeViewportHeight = 180;
        Vector2 menuStatusScroll;
        GUIStyle quietMenuButton, activeMenuButton;
        int menuStyleGeneration = -1;
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
                NormalizeRecipeSelection();
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
            NormalizeRecipeSelection();
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
                if (journalOpen || helpOpen)
                {
                    string previous = helpOpen ? "help" : "journal";
                    journalOpen = false; helpOpen = false; recipeScroll = Vector2.zero; menuFocus.Reset(previous);
                }
                else if (controller.Fault == "") Resume();
                return;
            }
            if (commands.Step != 0)
            {
                if (helpOpen || journalOpen || inventory == null ||
                    !menuFocus.MoveGrid(commands.Direction.x, commands.Direction.y, recipeColumns)) menuFocus.Move(commands.Step);
                revealMenuSelection = true;
            }
            if (commands.Confirm) menuFocus.Confirm();
            if (Mathf.Abs(commands.Scroll) > .25f)
                recipeScroll.y = Mathf.Max(0, recipeScroll.y - commands.Scroll * 420 * Time.unscaledDeltaTime);
        }

        void Resume()
        {
            view.SetMenuOpen(false);
            crafting = false; helpOpen = false;
            ResetMenuInput();
        }

        bool MenuButton(string id, string label, params GUILayoutOption[] options)
        {
            EnsureMenuStyles();
            if (Event.current.type == EventType.Layout && GUI.enabled) menuFocus.Register(id);
            bool selected = view.UsesGamepad && menuFocus.Selected == id;
            bool chosenRecipe = id == "recipe:" + selectedRecipe;
            bool tab = id == "equipment" && !helpOpen && !journalOpen || id == "help" && helpOpen || id == "journal" && journalOpen;
            var style = id == "craft-selected" ? ui.Primary : selected || chosenRecipe || tab ? activeMenuButton : quietMenuButton;
            GUI.SetNextControlName("wordbound:" + id);
            var content = new GUIContent((selected && label.Length != 0 ? "› " : "") + label);
            var layout = options.Length == 0 ? new[] { GUILayout.MinHeight(ui.RowHeight) } : options;
            bool clicked = id == "craft-selected" ? ui.Button(content, style, layout) : GUILayout.Button(content, style, layout);
            if (Event.current.type == EventType.Repaint && (selected || chosenRecipe || tab))
            {
                Rect row = GUILayoutUtility.GetLastRect();
                Color before = GUI.color;
                GUI.color = new Color(ui.Ink.r, ui.Ink.g, ui.Ink.b, .75f);
                GUI.DrawTexture(id.StartsWith("recipe:", StringComparison.Ordinal)
                    ? new Rect(row.x, row.y + 6, 3, Mathf.Max(1, row.height - 12))
                    : new Rect(row.x + 8, row.yMax - 2, Mathf.Max(1, row.width - 16), 2), Texture2D.whiteTexture);
                GUI.color = before;
            }
            if (clicked) menuFocus.CancelConfirmation();
            if ((selected || !view.UsesGamepad && chosenRecipe && revealMenuSelection) && Event.current.type == EventType.Repaint)
            {
                if (selected) GUI.FocusControl("wordbound:" + id);
                if (revealMenuSelection && recipeScrollActive)
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

        void EnsureMenuStyles()
        {
            if (menuStyleGeneration == ui.Generation) return;
            quietMenuButton = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter };
            foreach (var state in new[] { quietMenuButton.normal, quietMenuButton.hover, quietMenuButton.active,
                quietMenuButton.focused, quietMenuButton.onNormal, quietMenuButton.onHover,
                quietMenuButton.onActive, quietMenuButton.onFocused })
            {
                state.background = null;
                state.textColor = ui.Ink;
            }
            activeMenuButton = new GUIStyle(quietMenuButton) { fontStyle = FontStyle.Bold };
            menuStyleGeneration = ui.Generation;
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
                    persistentNotice.Length != 0 ? persistentNotice : !controller.IsPresentationReady ? "Waiting for the host…" : "";
                menuActionStatus = TransientNotice(Time.unscaledTimeAsDouble);
                menuResultStatus = frame?.Progress.Result != null ? ResultText(frame.Progress.Result) : "";
                menuJournal = story ? story.Journal : Array.Empty<WordboundStoryNotice>();
                menuEpilogue = story ? story.Epilogue(mode) : null;
            }
            Rect area = ui.MenuRect(width, height);
            float inside = Mathf.Max(1, area.width - ui.Padding * 2);
            float line = Mathf.Max(ui.RowHeight, ui.Heading.CalcHeight(new GUIContent("Equipment"), inside - 120)) + 4;
            Rect header = new Rect(ui.Padding, ui.Padding, inside, line);
            Rect tabs = new Rect(ui.Padding, header.yMax + ui.Gap, inside, ui.RowHeight + 4);
            float bodyTop = tabs.yMax + ui.Gap * 2;
            Rect footer = new Rect(ui.Padding, area.height - ui.Padding - ui.RowHeight - 4, inside, ui.RowHeight + 4);
            string status = string.Join("\n", new[] { menuConnectionStatus, menuResultStatus, menuActionStatus }.Where(value => value.Length > 0));
            GUI.BeginGroup(area);
            GUI.Box(new Rect(0, 0, area.width, area.height), GUIContent.none, ui.Panel);
            GUILayout.BeginArea(header);
            GUILayout.BeginHorizontal();
            GUILayout.Label(helpOpen ? "Help" : journalOpen ? "Journal" : "Equipment", ui.Heading, GUILayout.MinHeight(ui.RowHeight));
            GUILayout.FlexibleSpace();
            GUI.enabled = controller.Fault == "";
            if (MenuButton("resume", "Resume", GUILayout.Width(Mathf.Max(100, ui.SingleLine.CalcSize(new GUIContent("› Resume")).x + 24)),
                GUILayout.MinHeight(ui.RowHeight))) QueueMenuAction(Resume);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUILayout.BeginArea(tabs);
            GUILayout.BeginHorizontal();
            if (MenuButton("equipment", "Equipment")) QueueMenuAction(() => { journalOpen = false; helpOpen = false; recipeScroll = Vector2.zero; });
            if ((menuJournal.Count > 0 || menuEpilogue != null) && MenuButton("journal", "Journal"))
                QueueMenuAction(() => { journalOpen = true; helpOpen = false; recipeScroll = Vector2.zero; });
            if (MenuButton("help", "Help")) QueueMenuAction(() => { helpOpen = true; journalOpen = false; recipeScroll = Vector2.zero; });
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            if (status.Length != 0)
            {
                float statusHeight = Mathf.Min(ui.Wrapped.CalcHeight(new GUIContent(status), Mathf.Max(1, inside - 24)) + 6,
                    Mathf.Max(ui.LabelHeight + 6, (footer.y - bodyTop) * .25f));
                GUILayout.BeginArea(new Rect(ui.Padding, bodyTop, inside, statusHeight));
                menuStatusScroll = GUILayout.BeginScrollView(menuStatusScroll);
                GUILayout.Label(status, ui.Wrapped);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                bodyTop += statusHeight + ui.Gap;
            }
            var body = new Rect(ui.Padding, bodyTop, inside, Mathf.Max(30, footer.y - bodyTop - ui.Gap * 2));
            GUI.enabled = helpOpen || journalOpen || frame != null && !controller.IsSimulationPaused;
            if (helpOpen)
            {
                GUILayout.BeginArea(body); HelpMenu(); GUILayout.EndArea();
            }
            else if (journalOpen && story)
            {
                GUILayout.BeginArea(body);
                recipeScroll = GUILayout.BeginScrollView(recipeScroll);
                if (menuEpilogue != null)
                {
                    GUILayout.Label(menuEpilogue.Title, ui.Heading);
                    GUILayout.Label(menuEpilogue.Cue, ui.Wrapped);
                    GUILayout.Space(12);
                }
                foreach (var clue in menuJournal)
                {
                    GUILayout.Label("Lap " + clue.Lap + " · " + clue.Chapter);
                    GUILayout.Label(clue.Place + ": " + clue.Clue, ui.Wrapped);
                    GUILayout.Space(8);
                }
                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }
            else if (inventory != null) RecipeWorkspace(body);
            GUI.enabled = true;
            GUILayout.BeginArea(footer);
            if (MenuButton("return", returnLabel, GUILayout.MinHeight(ui.RowHeight))) QueueMenuAction(() => ReturnRequested?.Invoke());
            if (Event.current.type == EventType.Repaint) recipeReturnRect = footer;
            GUILayout.EndArea();
            GUI.EndGroup();
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
            if (inventory.CarriedFurniture >= 0 && MenuButton("drop-furniture", "Put down furniture"))
                QueueMenuAction(() => Send(WordboundAction.Drop));
            if (actor.EquippedSlot >= 0)
            {
                GUILayout.BeginHorizontal();
                int selectedSlot = actor.EquippedSlot;
                if (MenuButton("place", "Place")) QueueMenuAction(() => Send(WordboundAction.Deploy, slot: selectedSlot));
                if (MenuButton("drop", "Drop")) QueueMenuAction(() => Send(WordboundAction.Drop, slot: selectedSlot));
                GUILayout.EndHorizontal();
            }
            if ((mode.IsRace || mode.Id == "Freestyle") && frame.Progress.Teams.Single(t => t.Team == team).KeepsakeCarrier == actorId &&
                MenuButton("drop-keepsake", "Put down keepsake")) QueueMenuAction(() => Send(WordboundAction.DropKeepsake));
        }
    }
}
