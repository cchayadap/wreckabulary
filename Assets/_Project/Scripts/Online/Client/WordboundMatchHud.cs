using System;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    [DisallowMultipleComponent]
    public sealed class WordboundMatchHud : MonoBehaviour
    {
        WordboundGameplayController controller;
        WordboundPlayerView view;
        WordboundStoryView story;
        ItemCatalogue items;
        ModeDefinition mode;
        ResolvedCourse course;
        string actorId, search = "", notice = "";
        string returnLabel = "Return to party";
        int seat, team;
        bool initialized, crafting, journalOpen, defenseHeld;
        string defenseItem = "";
        double nextDefenseInput;
        Vector2 recipeScroll;
        long receiptSequence, receiptEpoch = -1;
        MatchSnapshot frame;
        PlayerInventory inventory;
        MatchActorState actor;
        public event Action ReturnRequested;

        public void Initialize(WordboundGameplayController gameplay, WordboundPlayerView playerView,
            ItemCatalogue catalogue, ModeDefinition definition, ResolvedCourse resolved, string localActor,
            string returnButtonLabel = "Return to party")
        {
            if (initialized) throw new InvalidOperationException("Match HUD is already initialized.");
            controller = gameplay != null ? gameplay : throw new ArgumentNullException(nameof(gameplay));
            view = playerView != null ? playerView : throw new ArgumentNullException(nameof(playerView));
            items = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
            mode = definition ?? throw new ArgumentNullException(nameof(definition));
            course = resolved ?? throw new ArgumentNullException(nameof(resolved));
            actorId = localActor;
            if (string.IsNullOrWhiteSpace(returnButtonLabel)) throw new ArgumentException("A return label is required.");
            returnLabel = returnButtonLabel;
            var binding = controller.Authority.Match.Binding;
            seat = Enumerable.Range(0, binding.Seats.Count).Single(i => binding.Seats[i].PlayerId == actorId);
            team = binding.Seats[seat].Team;
            story = gameObject.AddComponent<WordboundStoryView>();
            story.Bind(controller, course, items, actorId, view.Camera);
            controller.FrameChanged += ApplyFrame;
            controller.Faulted += ConnectionFailed;
            view.CommandsRead += ReadCommands;
            view.MenuRequested += MenuRequested;
            initialized = true;
            ApplyFrame(controller.Frame);
        }

        void ApplyFrame(MatchSnapshot next)
        {
            frame = next;
            if (next == null) return;
            inventory = next.Economy.Players.Single(p => p.PlayerId == seat);
            actor = next.Actors.Single(p => p.PlayerId == actorId);
            if (receiptEpoch != next.Epoch) { receiptEpoch = next.Epoch; receiptSequence = 0; }
            foreach (var receipt in next.Commands.Receipts.Where(r => r.ActorId == actorId && r.Sequence > receiptSequence)
                .OrderBy(r => r.Sequence))
            {
                receiptSequence = receipt.Sequence;
                if (receipt.Effect.Code != "Ok") notice = RefusalText(receipt.Effect.Code);
            }
        }

        void ReadCommands(PlayerCommands input)
        {
            if (input.spellDown) { ReleaseDefense(); crafting = true; journalOpen = false; view.SetMenuOpen(true); return; }
            if (input.slot > 0) { Send(WordboundAction.Equip, slot: input.slot - 1); return; }
            bool shieldHeld = SelectedShield() != null && (input.blockHeld || input.attackHeld) &&
                !input.aimHeld && !input.grab && !input.drop;
            ReadDefense(shieldHeld);
            if (input.grab) Interact();
            if (input.drop) Send(WordboundAction.Drop, slot: inventory?.CarriedFurniture >= 0 ? -1 : actor?.EquippedSlot ?? -1);
            if (input.attack && !shieldHeld)
            {
                if (input.aimHeld && actor != null && actor.EquippedSlot >= 0)
                    Send(WordboundAction.Throw, slot: actor.EquippedSlot);
                else Send(WordboundAction.Attack);
            }
        }

        ItemInstance SelectedShield()
        {
            if (actor == null || inventory == null || frame == null || actor.EquippedSlot < 0 ||
                actor.EquippedSlot >= inventory.Slots.Length || inventory.Craft != null ||
                inventory.CarriedFurniture >= 0 || !inventory.CanAct || actor.Health <= 0) return null;
            int id = inventory.Slots[actor.EquippedSlot];
            var item = frame.Economy.Items.FirstOrDefault(i => i.Id == id && i.State == ItemState.Held && i.HolderId == seat);
            return item != null && items.Get(item.Word).Shield != null ? item : null;
        }

        void ReadDefense(bool held)
        {
            var item = held ? SelectedShield() : null;
            if (item == null) { ReleaseDefense(); return; }
            string id = "item:" + item.Id;
            if (defenseHeld && defenseItem != id) ReleaseDefense();
            if (Time.unscaledTimeAsDouble < nextDefenseInput) return;
            nextDefenseInput = Time.unscaledTimeAsDouble + WordboundDefensePolicy.RefreshTicks / (double)ModeDefinition.TickRate;
            if (Send(WordboundAction.Block, value: "Hold")) { defenseHeld = true; defenseItem = id; }
        }

        void ReleaseDefense()
        {
            if (!defenseHeld) return;
            defenseHeld = false;
            defenseItem = "";
            nextDefenseInput = 0;
            if (controller && controller.Frame != null) Send(WordboundAction.Block, value: "Release");
        }

        void Update()
        {
            if (!initialized || !defenseHeld) return;
            var shield = SelectedShield();
            if (!view || view.MenuOpen || !Application.isFocused || controller.IsSimulationPaused ||
                shield == null || defenseItem != "item:" + shield.Id) ReleaseDefense();
        }

        void MenuRequested() { ReleaseDefense(); crafting = false; }
        void ConnectionFailed(string reason)
            => ShowFailure("The session stopped. Choose " + returnLabel + " to continue.");
        public void ShowFailure(string message)
        {
            ReleaseDefense();
            notice = message;
            crafting = false;
            view.SetMenuOpen(true);
        }
        bool Send(WordboundAction action, string target = "", int revision = 0, int slot = -1, string value = "")
        {
            if (action != WordboundAction.Block) ReleaseDefense();
            try
            {
                controller.Submit(action, target, revision, slot, value);
                if (action != WordboundAction.Block) notice = "";
                return true;
            }
            catch (InvalidOperationException) { notice = "Waiting for the connection. Try again shortly."; return false; }
        }

        WordboundNetworkBody Target()
        {
            if (view.Player == null || frame == null) return null;
            WordboundNetworkBody best = null;
            float score = float.PositiveInfinity;
            foreach (var body in controller.Bodies.Values)
            {
                if (!body || body.Object == null || !body.Object.IsValid || !body.BodyActive) continue;
                var kind = (WorldBodyKind)body.BodyKind;
                if (kind != WorldBodyKind.Item && kind != WorldBodyKind.Tile && kind != WorldBodyKind.Keepsake ||
                    !mode.CanInteract(team, body.OwnerTeam, body.Protected)) continue;
                Vector3 offset = body.transform.position - view.Player.transform.position;
                float squared = offset.sqrMagnitude;
                if (squared > 9f) continue;
                var screen = view.Camera.WorldToViewportPoint(body.transform.position + Vector3.up * .35f);
                if (screen.z <= 0 || screen.x < 0 || screen.x > 1 || screen.y < 0 || screen.y > 1) continue;
                float candidate = squared + Mathf.Abs(screen.x - .5f) * 3;
                if (candidate >= score) continue;
                best = body; score = candidate;
            }
            return best;
        }

        void Interact()
        {
            var target = Target();
            if (target == null)
            {
                var nearby = WordboundPracticeGuidePlan.Nearest(course, frame, actorId);
                if (nearby != null && frame.Practice.SiteHash != nearby.Hash && !WordboundPracticeGuidePlan.Completed(frame, nearby))
                    Send(WordboundAction.PracticePuzzle, nearby.Hash);
                else notice = nearby != null ? WordboundPracticeGuidePlan.Status(frame, nearby) : "Move closer to a letter, object or keepsake.";
                return;
            }
            string id = target.BodyId.ToString();
            switch ((WorldBodyKind)target.BodyKind)
            {
                case WorldBodyKind.Tile: Send(WordboundAction.CollectLetter, id); break;
                case WorldBodyKind.Keepsake: Send(WordboundAction.TakeKeepsake); break;
                case WorldBodyKind.Item:
                    var item = frame.Economy.Items.FirstOrDefault(i => id == "item:" + i.Id);
                    if (item == null) return;
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
                    if (puzzle != null) Send(WordboundAction.UsePuzzle, puzzle.Id, value: id);
                    else Send(WordboundAction.PickUp, id, item.Revision);
                    break;
            }
        }

        void OnGUI()
        {
            if (!initialized) return;
            Matrix4x4 saved = GUI.matrix;
            bool enabled = GUI.enabled;
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), .6f, 1.8f);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            float width = Screen.width / scale, height = Screen.height / scale;
            try
            {
                GUILayout.BeginArea(new Rect(18, 18, 380, 220), GUI.skin.box);
                GUILayout.Label(mode.Name);
                if (frame == null) GUILayout.Label("Preparing the world…");
                else
                {
                    var progress = frame.Progress.Teams.Single(t => t.Team == team);
                    if (mode.IsRace)
                    {
                        int lap = Math.Min(progress.Lap, course.Laps.Count - 1);
                        int checkpoints = course.Laps[lap].Checkpoints.Count;
                        GUILayout.Label($"Lap {Math.Min(progress.Lap + 1, mode.Laps)} / {mode.Laps} · Checkpoint {Math.Min(progress.NextCheckpoint + 1, checkpoints)} / {checkpoints}");
                        GUILayout.Label(ObjectiveText(progress),
                            new GUIStyle(GUI.skin.label) { wordWrap = true });
                    }
                    else if (mode.Objective == WordboundObjective.Knockouts)
                        GUILayout.Label($"Knockouts: {frame.Progress.Players.Single(p => p.Id == actorId).Knockouts} / {mode.KnockoutTarget}");
                    else if (mode.Id == "Freestyle")
                    {
                        var selected = WordboundPracticePolicy.Selected(course, frame.Practice);
                        GUILayout.Label(selected == null ? "Explore freely · select a nearby puzzle to practise." :
                            WordboundPracticeGuidePlan.Status(frame, selected), new GUIStyle(GUI.skin.label) { wordWrap = true });
                    }
                    if (mode.TimeLimitSeconds > 0)
                    {
                        long seconds = Math.Max(0, mode.TimeLimitSeconds - frame.Progress.ElapsedTicks / ModeDefinition.TickRate);
                        GUILayout.Label($"{seconds / 60:00}:{seconds % 60:00} remaining");
                    }
                    GUILayout.Label($"Health {Mathf.CeilToInt(actor.Health)} · Stamina {Mathf.CeilToInt(actor.Stamina)}");
                    var shield = SelectedShield();
                    if (shield != null)
                        GUILayout.Label(actor.Defense == null ? "Hold attack or Block to raise " + shield.Word :
                            WordboundDefensePolicy.Raised(actor.Defense, items.Get(shield.Word).Shield, frame.Progress.Tick)
                                ? "Blocking · face incoming attacks" : "Raising shield…");
                    if (controller.Fault != "") GUILayout.Label("Connection interrupted");
                    else if (controller.IsSimulationPaused) GUILayout.Label("Waiting for the host…");
                    if (frame.Progress.Result != null)
                        GUILayout.Label(ResultText(frame.Progress.Result));
                }
                GUILayout.EndArea();
                GUILayout.BeginArea(new Rect(18, height - 100, width - 36, 82), GUI.skin.box);
                GUILayout.Label("Letters: " + (inventory?.Letters.ToString() ?? ""));
                GUILayout.Label(notice != "" ? notice : "Interact to collect or pick up · Spell to craft · Aim + attack to throw");
                if (!view.MenuOpen && Target() != null) GUILayout.Label("Interact: use nearby object");
                else if (!view.MenuOpen && WordboundPracticeGuidePlan.Nearest(course, frame, actorId) != null)
                    GUILayout.Label("Interact: select nearby practice station · Menu: station details");
                GUILayout.EndArea();
                if (view.MenuOpen) Menu(width, height);
            }
            finally { GUI.matrix = saved; GUI.enabled = enabled; }
        }

        string ObjectiveText(TeamProgress progress)
        {
            int lap = progress.Lap, checkpoint = progress.NextCheckpoint;
            if (progress.Finished || lap < 0 || lap >= course.Laps.Count) return "Course complete.";
            var route = course.Laps[lap].Checkpoints;
            if (checkpoint < 0 || checkpoint >= route.Count) return "Bring the keepsake to the lap finish.";
            var completed = progress.CompletedPuzzles;
            var puzzle = course.Puzzles.FirstOrDefault(p => route[checkpoint].PuzzleIds.Contains(p.Id) && !completed.Contains(p.Id));
            if (puzzle == null) return "Bring the keepsake to the next checkpoint.";
            return puzzle.PresetId switch
            {
                "CollectLetters" => "Smash the marked furniture, collect its letters, and spell " + puzzle.RecipeId + ".",
                "PlaceObject" => "Place " + puzzle.RecipeId + " on the marked socket.",
                "RedirectWind" => "Place " + puzzle.RecipeId + " to push the loose stool into its target.",
                "TimedMechanism" => "Use " + puzzle.RecipeId + " to slow the arm. Carry the keepsake through.",
                "BuildRoute" => "Use " + puzzle.RecipeId + " to reach the raised marker.",
                "BalanceWeight" => "Cover both floor pads with " + puzzle.RecipeId + ".",
                _ => "Spell " + puzzle.RecipeId + " and use it at the marked puzzle."
            };
        }

        string ResultText(MatchResult result)
        {
            if (!result.Confirmed) return "Confirming result…";
            if (result.Outcome == "Draw") return "Draw";
            if (result.Outcome == "TimeExpired") return "Time's up";
            return result.WinningTeams.Contains(team) ? "You won!" : "Match complete";
        }

        void Menu(float width, float height)
        {
            GUILayout.BeginArea(new Rect((width - 580) / 2, 48, 580, height - 170), GUI.skin.box);
            GUILayout.Label(journalOpen ? "Collected clues" : crafting ? "Spell equipment" : "Wordbound");
            GUI.enabled = controller.Fault == "";
            if (GUILayout.Button("Resume", GUILayout.Height(34))) { view.SetMenuOpen(false); crafting = false; }
            GUI.enabled = true;
            if (story && story.Journal.Count > 0)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Equipment")) journalOpen = false;
                if (GUILayout.Button("Story clues · " + story.Journal.Count)) journalOpen = true;
                GUILayout.EndHorizontal();
            }
            GUI.enabled = frame != null && !controller.IsSimulationPaused;
            if (journalOpen && story)
            {
                recipeScroll = GUILayout.BeginScrollView(recipeScroll);
                var label = new GUIStyle(GUI.skin.label) { wordWrap = true };
                foreach (var clue in story.Journal)
                {
                    GUILayout.Label("Lap " + clue.Lap + " · " + clue.Chapter);
                    GUILayout.Label(clue.Place + ": " + clue.Clue, label);
                    GUILayout.Space(8);
                }
                GUILayout.EndScrollView();
            }
            else if (inventory != null)
            {
                var nearby = WordboundPracticeGuidePlan.Nearest(course, frame, actorId);
                if (nearby != null)
                {
                    GUILayout.Label(WordboundPracticeGuidePlan.Status(frame, nearby), new GUIStyle(GUI.skin.label) { wordWrap = true });
                    if (!WordboundPracticeGuidePlan.Completed(frame, nearby) && frame.Practice.SiteHash != nearby.Hash &&
                        GUILayout.Button("Practise " + course.Puzzles.Single(p => p.Id == nearby.PuzzleId).RecipeId + " station"))
                    { Send(WordboundAction.PracticePuzzle, nearby.Hash); view.SetMenuOpen(false); }
                }
                if (inventory.Craft != null)
                {
                    GUILayout.Label("Crafting " + inventory.Craft.ItemId);
                    if (GUILayout.Button("Cancel craft")) Send(WordboundAction.CancelCraft);
                }
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(actor.EquippedSlot < 0 ? "• Fists" : "Fists")) Send(WordboundAction.Equip, slot: -1);
                for (int i = 0; i < inventory.Slots.Length; i++)
                {
                    int itemId = inventory.Slots[i];
                    string label = frame.Economy.Items.FirstOrDefault(item => item.Id == itemId)?.Word ?? "Empty";
                    if (GUILayout.Button((actor.EquippedSlot == i ? "• " : "") + (i + 1) + ": " + label))
                        Send(WordboundAction.Equip, slot: i);
                }
                GUILayout.EndHorizontal();
                if (inventory.CarriedFurniture >= 0 && GUILayout.Button("Put down carried furniture")) Send(WordboundAction.Drop);
                if (actor.EquippedSlot >= 0)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Place selected item")) Send(WordboundAction.Deploy, slot: actor.EquippedSlot);
                    if (GUILayout.Button("Drop selected item")) Send(WordboundAction.Drop, slot: actor.EquippedSlot);
                    GUILayout.EndHorizontal();
                }
                if ((mode.IsRace || mode.Id == "Freestyle") && frame.Progress.Teams.Single(t => t.Team == team).KeepsakeCarrier == actorId &&
                    GUILayout.Button("Put down keepsake")) Send(WordboundAction.DropKeepsake);
                search = GUILayout.TextField(search, 32);
                recipeScroll = GUILayout.BeginScrollView(recipeScroll);
                foreach (var card in items.Cards(inventory.Letters).Where(c => mode.Recipes.Contains(c.Item.Id) &&
                    c.Item.Id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    bool ready = card.Missing.IsEmpty && inventory.Craft == null;
                    GUI.enabled = !controller.IsSimulationPaused && ready;
                    if (GUILayout.Button(card.Item.Id + (ready ? "  ·  Craft" : "  ·  Need " + card.Missing)))
                    { Send(WordboundAction.BeginCraft, value: card.Item.Id); view.SetMenuOpen(false); crafting = false; }
                }
                GUILayout.EndScrollView();
            }
            GUI.enabled = true;
            if (GUILayout.Button(returnLabel, GUILayout.Height(32))) ReturnRequested?.Invoke();
            GUILayout.EndArea();
        }

        static string RefusalText(string code) => code switch
        {
            "OutOfReach" => "Move closer and keep the object in sight.",
            "MissingLetters" => "Collect the missing letters first.",
            "HandsFull" => "Free a slot or put down the furniture.",
            "Capacity" => "Object limit reached. Try again after unused objects clear.",
            "BlockedPlacement" => "That spot is blocked. Move to a clear space.",
            "BlockedRelease" => "Make room in front of you before putting the item down.",
            "EmptySlot" => "Choose an occupied equipment slot.",
            "NotAlive" => "Wait until you recover.",
            "StaleRevision" => "The object changed. Try again.",
            "TooEarly" => "Wait a moment before trying again.",
            "OutOfOrder" => "Complete the current puzzle first.",
            "Unavailable" => "That action is unavailable here.",
            "AlreadySolved" => "Completed this session. Start a fresh session to repeat it.",
            "NoShield" => "Select a shield, finish your current action, and try again.",
            "Blocking" => "Lower your shield before attacking.",
            _ => "That action could not be completed."
        };

        void OnDisable() => ReleaseDefense();

        void OnDestroy()
        {
            if (controller) { controller.FrameChanged -= ApplyFrame; controller.Faulted -= ConnectionFailed; }
            if (view) { view.CommandsRead -= ReadCommands; view.MenuRequested -= MenuRequested; }
        }
    }
}
