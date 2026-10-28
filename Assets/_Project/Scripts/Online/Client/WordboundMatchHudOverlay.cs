using System;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundMatchHud
    {
        GUIStyle overlayLabel, overlayBox;
        int overlayStyleGeneration = -1;

        void DrawOverlay(float width, float height)
        {
            using var themeScope = ui.Apply(Theme);
            if (overlayStyleGeneration != ui.Generation)
            {
                overlayLabel = new GUIStyle(ui.Wrapped) { padding = new RectOffset(0, 0, 1, 1) };
                overlayLabel.normal.textColor = ui.Ink;
                overlayBox = new GUIStyle(ui.Panel) { padding = new RectOffset(10, 10, 6, 6) };
                overlayStyleGeneration = ui.Generation;
            }
            Color background = GUI.backgroundColor;
            try
            {
                GUI.backgroundColor = new Color(1f, 1f, 1f, .78f);
                float available = width - 36;
                statusPanel.DrawCompact(18, 18, Mathf.Min(380, available * .48f), overlayLabel, overlayBox, false, 0);
                inventoryPanel.DrawCompact(18, height - 18, available * .23f, overlayLabel, overlayBox, true, 0);
                actionPanel.DrawCompact(width * .5f, height - 18, available * .5f, overlayLabel, overlayBox, true, .5f);
                menuHintPanel.DrawCompact(width - 18, height - 18, available * .23f, overlayLabel, overlayBox, true, 1);
            }
            finally { GUI.backgroundColor = background; }
        }

        void PreparePanels()
        {
            statusPanel.Begin(); actionPanel.Begin(); inventoryPanel.Begin(); menuHintPanel.Begin();
            string heading = mode.Name;
            if (frame != null && mode.TimeLimitSeconds > 0)
            {
                long seconds = Math.Max(0, mode.TimeLimitSeconds - frame.Progress.ElapsedTicks / ModeDefinition.TickRate);
                heading += $" · {seconds / 60:00}:{seconds % 60:00}";
            }
            statusPanel.Add(heading);
            if (frame != null)
            {
                var progress = frame.Progress.Teams.Single(t => t.Team == team);
                if (mode.IsRace)
                {
                    int lap = Math.Min(progress.Lap, course.Laps.Count - 1);
                    int checkpoints = course.Laps[lap].Checkpoints.Count;
                    statusPanel.Add($"Lap {Math.Min(progress.Lap + 1, mode.Laps)}/{mode.Laps} · Checkpoint {Math.Min(progress.NextCheckpoint + 1, checkpoints)}/{checkpoints}");
                }
                else if (mode.Objective == WordboundObjective.Knockouts)
                    statusPanel.Add($"Knockouts {frame.Progress.Players.Single(p => p.Id == actorId).Knockouts}/{mode.KnockoutTarget}");
                if (actor != null) statusPanel.Add($"Health {Mathf.CeilToInt(actor.Health)} · Stamina {Mathf.CeilToInt(actor.Stamina)}");
            }
            string letters = CompactLetters(inventory?.Letters.ToString());
            if (letters.Length != 0) inventoryPanel.Add("Letters " + letters);
            var held = actor == null ? null : HeldItem(actor.EquippedSlot);
            if (held != null) inventoryPanel.Add(held.Word);
            string context = ContextPrompt();
            if (context.Length != 0) actionPanel.Add(context);
            menuHintPanel.Add(Hint(WordboundControl.Spell, "Craft") + " · " + Hint(WordboundControl.Menu, "Menu"));
            panelsReady = true;
        }

        string ContextPrompt()
        {
            if (controller.Fault != "") return "Connection interrupted · " + returnLabel;
            if (persistentNotice.Length != 0) return persistentNotice;
            if (frame == null) return "Preparing the world…";
            if (frame.Progress.Result != null) return ResultText(frame.Progress.Result);
            if (controller.IsSimulationPaused) return "Waiting for the host…";
            if (actor != null && actor.Health <= 0) return "Recovering…";
            string transient = TransientNotice(Time.unscaledTimeAsDouble);
            if (transient.Length != 0) return transient;
            if (inventory?.Craft != null) return "Crafting " + inventory.Craft.ItemId + "…";
            if (actor == null || inventory == null || actor.Health <= 0 || !inventory.CanAct) return "Recovering…";
            var target = Target();
            if (target != null)
            {
                var kind = (WorldBodyKind)target.BodyKind;
                var item = kind == WorldBodyKind.Item ? frame.Economy.Items.FirstOrDefault(i => target.BodyId.ToString() == "item:" + i.Id) : null;
                string action = InteractionCaption(kind, item?.Word, item != null && InteractionPuzzle(item) != null);
                if (action.Length != 0) return Hint(WordboundControl.Interact, action);
            }
            var nearby = WordboundPracticeGuidePlan.Nearest(course, frame, actorId);
            if (nearby != null && frame.Practice.SiteHash != nearby.Hash && !WordboundPracticeGuidePlan.Completed(frame, nearby))
                return Hint(WordboundControl.Interact, "Practise " + course.Puzzles.Single(p => p.Id == nearby.PuzzleId).RecipeId);
            if (inventory.CarriedFurniture >= 0) return "Hold " + Hint(WordboundControl.Drop, "Drop");
            var shield = SelectedShield();
            if (shield != null)
                return actor.Defense == null ? "Hold " + Hint(WordboundControl.Block, "Block") :
                    WordboundDefensePolicy.Raised(actor.Defense, items.Get(shield.Word).Shield, frame.Progress.Tick) ? "Blocking" : "Raising shield…";
            return showQuickTips ? Hint(WordboundControl.Attack, "Smash furniture") : "";
        }

        void SetNotice(string message)
        {
            notice = message ?? "";
            noticeUntil = Time.unscaledTimeAsDouble + 4;
        }

        string TransientNotice(double now) => now < noticeUntil ? notice : "";

        static string InteractionCaption(WorldBodyKind kind, string word, bool puzzle) => kind switch
        {
            WorldBodyKind.Tile => "Collect letter",
            WorldBodyKind.Keepsake => "Take keepsake",
            WorldBodyKind.Item when !string.IsNullOrEmpty(word) => (puzzle ? "Use " : "Pick up ") + word,
            _ => ""
        };

        static string CompactLetters(string letters) => string.IsNullOrEmpty(letters) ? "" :
            letters.Length <= 18 ? letters : letters.Substring(0, 18) + "… +" + (letters.Length - 18);

        ResolvedCoursePuzzle InteractionPuzzle(ItemInstance item)
        {
            var progress = frame.Progress.Teams.Single(t => t.Team == team);
            var puzzle = mode.IsRace && item.State == ItemState.Deployed && !progress.Finished
                ? course.Puzzles.FirstOrDefault(p => p.RecipeId == item.Word &&
                    course.Laps[progress.Lap].Checkpoints.Skip(progress.NextCheckpoint).Take(1)
                        .Any(c => c.PuzzleIds.Contains(p.Id)) && !progress.CompletedPuzzles.Contains(p.Id))
                : null;
            if (mode.Id == "Freestyle" && item.State == ItemState.Deployed)
            {
                var selected = WordboundPracticePolicy.Selected(course, frame.Practice);
                if (selected != null && !WordboundPracticeGuidePlan.Completed(frame, selected))
                    puzzle = course.Puzzles.FirstOrDefault(p => p.Id == selected.PuzzleId && p.RecipeId == item.Word);
            }
            return puzzle;
        }

        void HelpMenu()
        {
            recipeScroll = GUILayout.BeginScrollView(recipeScroll);
            if (frame != null && mode != null && course != null)
            {
                if (mode.IsRace) GUILayout.Label(ObjectiveText(frame.Progress.Teams.Single(t => t.Team == team)), ui.Wrapped);
                else if (mode.Id == "Freestyle")
                {
                    var nearby = WordboundPracticeGuidePlan.Nearest(course, frame, actorId);
                    GUILayout.Label(nearby == null ? "Explore and choose a practice station." : WordboundPracticeGuidePlan.Status(frame, nearby), ui.Wrapped);
                }
                GUILayout.Space(8);
            }
            GUILayout.Label("Smash furniture. Collect letters. Craft equipment.", ui.Wrapped);
            if (MenuButton("quick-tips", showQuickTips ? "Hide quick tips" : "Show quick tips"))
                QueueMenuAction(() => showQuickTips = !showQuickTips);
            GUILayout.Space(8);
            foreach (string row in HelpControls()) GUILayout.Label(row, ui.Wrapped);
            GUILayout.EndScrollView();
        }

        string ThrowHint()
        {
            string aim = view.ControlHint(WordboundControl.Aim), attack = view.ControlHint(WordboundControl.Attack);
            return string.IsNullOrEmpty(aim) || string.IsNullOrEmpty(attack) ? "Throw" : "[" + aim + "] + [" + attack + "] Throw";
        }

        string[] HelpControls() => new[] {
            Hint(WordboundControl.Interact, "Pick up / use"),
            Hint(WordboundControl.Attack, "Smash"),
            Hint(WordboundControl.Spell, "Craft from letters"),
            ThrowHint(),
            "Hold " + Hint(WordboundControl.Drop, "Drop"),
            Hint(WordboundControl.CycleGear, "Change equipment"),
            "Hold " + Hint(WordboundControl.Block, "Block with a shield"),
            Hint(WordboundControl.Menu, "Resume / menu")
        };
    }
}
