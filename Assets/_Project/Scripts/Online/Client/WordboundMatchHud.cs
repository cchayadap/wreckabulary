using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;

namespace Wreckabulary.Online
{
    [DisallowMultipleComponent]
    public sealed partial class WordboundMatchHud : MonoBehaviour
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
        readonly RaycastHit[] sightHits = new RaycastHit[64];
        readonly Dictionary<string, WorldBodyState> canonicalBodies = new Dictionary<string, WorldBodyState>(StringComparer.Ordinal);
        readonly HashSet<Collider> staticSurfaces = new HashSet<Collider>();
        WordboundCourseWorld sightWorld;
        readonly HudPanel statusPanel = new HudPanel(), actionPanel = new HudPanel();
        bool panelsReady;
        public event Action ReturnRequested;
        public WordboundUiTheme Theme;
        public WordboundStoryProfile StoryProfile;
        readonly WordboundUiStyles ui = new WordboundUiStyles();

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
            story.Bind(controller, course, items, actorId, view.Camera, StoryProfile);
            controller.FrameChanged += ApplyFrame;
            controller.Faulted += ConnectionFailed;
            view.CommandsRead += ReadCommands;
            view.MenuCommandsRead += ReadMenuCommands;
            view.MenuRequested += MenuRequested;
            initialized = true;
            ApplyFrame(controller.Frame);
        }

        void ApplyFrame(MatchSnapshot next)
        {
            frame = next;
            canonicalBodies.Clear();
            if (next == null) { inventory = null; actor = null; return; }
            foreach (var body in next.Bodies) canonicalBodies.Add(body.Id, body);
            inventory = next.Economy.Players.Single(p => p.PlayerId == seat);
            RefreshRecipes();
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
            if (input.spellDown)
            {
                ReleaseDefense(); crafting = true; journalOpen = false;
                menuFocus.Reset(FirstCraftableRecipe()); revealMenuSelection = true; view.SetMenuOpen(true); return;
            }
            if (input.slot > 0) { Send(WordboundAction.Equip, slot: input.slot - 1); return; }
            if (input.swap)
            {
                if (inventory != null && actor != null && frame != null)
                {
                    int next = -1;
                    for (int slot = actor.EquippedSlot + 1; slot < inventory.Slots.Length; slot++)
                    {
                        var item = HeldItem(slot);
                        if (item == null) continue;
                        next = slot; break;
                    }
                    Send(WordboundAction.Equip, slot: next);
                }
                return;
            }
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
            if (!initialized) return;
            ExecuteMenuAction(Application.isFocused);
            if (!defenseHeld) return;
            var shield = SelectedShield();
            if (!view || view.MenuOpen || !Application.isFocused || controller.IsSimulationPaused ||
                shield == null || defenseItem != "item:" + shield.Id) ReleaseDefense();
        }

        void MenuRequested() { ReleaseDefense(); crafting = false; ResetMenuInput(); }
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
            if (view.Player == null || view.Player.Object == null || !view.Player.Object.IsValid ||
                view.Player.Rigidbody == null || frame == null ||
                !canonicalBodies.TryGetValue(view.Player.BodyId.ToString(), out var playerState) ||
                playerState.Kind != WorldBodyKind.Player || playerState.SourceId != actorId || !playerState.Active)
                return null;
            WordboundNetworkBody best = null;
            float score = float.PositiveInfinity;
            foreach (var body in controller.PresentedBodies)
            {
                if (!body || body.Object == null || !body.Object.IsValid || !body.BodyActive || body.Rigidbody == null ||
                    body.Runner != view.Player.Runner || !canonicalBodies.TryGetValue(body.BodyId.ToString(), out var state) ||
                    !state.Active || body.BodyKind != (int)state.Kind) continue;
                var kind = state.Kind;
                if (kind != WorldBodyKind.Item && kind != WorldBodyKind.Tile && kind != WorldBodyKind.Keepsake ||
                    !mode.CanInteract(team, state.OwnerTeam, state.Protected)) continue;
                Vector3 offset = body.Rigidbody.position - view.Player.Rigidbody.position;
                float squared = offset.sqrMagnitude;
                if (squared > 9f) continue;
                var screen = view.Camera.WorldToViewportPoint(body.Rigidbody.position + Vector3.up * .35f);
                if (screen.z <= 0 || screen.x < 0 || screen.x > 1 || screen.y < 0 || screen.y > 1) continue;
                float candidate = squared + Mathf.Abs(screen.x - .5f) * 3;
                if (candidate >= score || !VisibleFromPlayer(playerState, body)) continue;
                best = body; score = candidate;
            }
            return best;
        }

        bool VisibleFromPlayer(WorldBodyState playerState, WordboundNetworkBody target)
        {
            Vector3 from = view.Player.Rigidbody.position, to = target.Rigidbody.position;
            if (controller.PhysicsWorld != null)
                return controller.PhysicsWorld.HasLineOfSight(actorId,
                    new WorldVector(from.x, from.y, from.z), new WorldVector(to.x, to.y, to.z));
            var world = controller.CourseWorld;
            if (!world) return false;
            var scene = world.gameObject.scene.GetPhysicsScene();
            if (!scene.IsValid() || scene == Physics.defaultPhysicsScene ||
                view.Player.gameObject.scene.GetPhysicsScene() != scene || target.gameObject.scene.GetPhysicsScene() != scene)
                return false;
            if (sightWorld != world)
            {
                staticSurfaces.Clear();
                foreach (var surface in world.Surfaces) staticSurfaces.Add(surface);
                sightWorld = world;
            }
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < .000001f) return true;
            int count = scene.Raycast(from + Vector3.up * .75f, delta.normalized, sightHits,
                delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count >= sightHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var collider = sightHits[i].collider;
                if (!collider) continue;
                if (staticSurfaces.Contains(collider)) return false;
                var other = collider.attachedRigidbody ? collider.attachedRigidbody.GetComponent<WordboundNetworkBody>() : null;
                if (!other || other.Object == null || !other.Object.IsValid || other.Runner != view.Player.Runner ||
                    !canonicalBodies.TryGetValue(other.BodyId.ToString(), out var state) ||
                    other.BodyKind != (int)state.Kind || !WordboundPhysicsPolicy.ShouldCollide(mode, playerState, state)) continue;
                if (other == view.Player || (other.Rigidbody.position - to).sqrMagnitude < .0001f) continue;
                return false;
            }
            return true;
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
                if (!panelsReady || Event.current.type == EventType.Layout) PreparePanels();
                if (view.MenuOpen) Menu(width, height, Application.isFocused);
                else
                {
                    statusPanel.Draw(18, 18, Mathf.Min(380, width - 36), WrappedLabel, GUI.skin.box);
                    actionPanel.Draw(18, height - 18, width - 36, WrappedLabel, GUI.skin.box, bottom: true);
                }
            }
            finally { GUI.matrix = saved; GUI.enabled = enabled; }
        }

        void PreparePanels()
        {
            statusPanel.Begin(); actionPanel.Begin();
            statusPanel.Add(mode.Name);
            var shield = SelectedShield();
            if (frame == null) statusPanel.Add("Preparing the world…");
            else
            {
                var progress = frame.Progress.Teams.Single(t => t.Team == team);
                if (mode.IsRace)
                {
                    int lap = Math.Min(progress.Lap, course.Laps.Count - 1);
                    int checkpoints = course.Laps[lap].Checkpoints.Count;
                    statusPanel.Add($"Lap {Math.Min(progress.Lap + 1, mode.Laps)} / {mode.Laps} · Checkpoint {Math.Min(progress.NextCheckpoint + 1, checkpoints)} / {checkpoints}");
                    statusPanel.Add(ObjectiveText(progress));
                }
                else if (mode.Objective == WordboundObjective.Knockouts)
                    statusPanel.Add($"Knockouts: {frame.Progress.Players.Single(p => p.Id == actorId).Knockouts} / {mode.KnockoutTarget}");
                else if (mode.Id == "Freestyle")
                {
                    var selected = WordboundPracticePolicy.Selected(course, frame.Practice);
                    statusPanel.Add(selected == null ? "Explore freely · select a nearby puzzle to practise." :
                        WordboundPracticeGuidePlan.Status(frame, selected));
                }
                if (mode.TimeLimitSeconds > 0)
                {
                    long seconds = Math.Max(0, mode.TimeLimitSeconds - frame.Progress.ElapsedTicks / ModeDefinition.TickRate);
                    statusPanel.Add($"{seconds / 60:00}:{seconds % 60:00} remaining");
                }
                statusPanel.Add($"Health {Mathf.CeilToInt(actor.Health)} · Stamina {Mathf.CeilToInt(actor.Stamina)}");
                if (shield != null)
                    statusPanel.Add(actor.Defense == null ? "Hold " + Hint(WordboundControl.Block, "block") + " to raise " + shield.Word :
                        WordboundDefensePolicy.Raised(actor.Defense, items.Get(shield.Word).Shield, frame.Progress.Tick)
                            ? "Blocking · face incoming attacks" : "Raising shield…");
                if (controller.Fault != "") statusPanel.Add("Connection interrupted");
                else if (controller.IsSimulationPaused) statusPanel.Add("Waiting for the host…");
                if (frame.Progress.Result != null) statusPanel.Add(ResultText(frame.Progress.Result));
            }
            string letters = inventory?.Letters.ToString();
            actionPanel.Add("Letters: " + (string.IsNullOrEmpty(letters) ? "—" : letters));
            actionPanel.Add(Hint(WordboundControl.Interact, "Interact") + " Pick up · " +
                Hint(WordboundControl.Spell, "Spell") + " Craft · " +
                (shield == null ? Hint(WordboundControl.Attack, "Attack") + " Smash" :
                    "Hold " + Hint(WordboundControl.Block, "Block") + " Block") +
                " · " + Hint(WordboundControl.Menu, "Menu") + " Menu");
            bool carried = inventory != null && inventory.CarriedFurniture >= 0;
            bool equipped = inventory != null && actor != null && actor.EquippedSlot >= 0 &&
                actor.EquippedSlot < inventory.Slots.Length && inventory.Slots[actor.EquippedSlot] >= 0;
            string gear = "";
            if (equipped && !carried)
                gear = Hint(WordboundControl.Aim, "Aim") + " + " + Hint(WordboundControl.Attack, "Attack") + " Throw · ";
            if (equipped || carried) gear += "Hold " + Hint(WordboundControl.Drop, "Drop") + " Drop · ";
            if (inventory != null && inventory.Slots.Any(id => id >= 0))
                gear += Hint(WordboundControl.CycleGear, "Cycle") + " Cycle gear";
            if (gear.Length > 0) actionPanel.Add(gear.TrimEnd(' ', '·'));
            if (notice != "") actionPanel.Add(notice);
            if (!view.MenuOpen && Target() != null)
                actionPanel.Add(Hint(WordboundControl.Interact, "Interact") + " Use nearby object");
            else if (!view.MenuOpen && WordboundPracticeGuidePlan.Nearest(course, frame, actorId) != null)
                actionPanel.Add(Hint(WordboundControl.Interact, "Interact") + " Select practice station · " +
                    Hint(WordboundControl.Menu, "Menu") + " Station details");
            panelsReady = true;
        }

        string Hint(WordboundControl control, string fallback)
        {
            string hint = view.ControlHint(control);
            return string.IsNullOrEmpty(hint) ? fallback : "[" + hint + "]";
        }

        sealed class HudPanel
        {
            readonly List<GUIContent> rows = new List<GUIContent>();
            readonly List<Rect> rects = new List<Rect>();
            int count;
            float measuredWidth = -1, measuredHeight;
            bool dirty = true;
            GUIStyle measuredLabel, measuredBox;
            public void Begin() { count = 0; }
            public void Add(string text)
            {
                if (count == rows.Count) { rows.Add(new GUIContent()); rects.Add(default); dirty = true; }
                if (rows[count].text != text) { rows[count].text = text; dirty = true; }
                count++;
            }
            int measuredCount = -1;
            public Rect Draw(float x, float y, float width, GUIStyle label, GUIStyle box, bool bottom = false)
            {
                if (dirty || measuredCount != count || measuredWidth != width || measuredLabel != label || measuredBox != box)
                {
                    float offset = box.padding.top;
                    float inner = Mathf.Max(1, width - box.padding.horizontal);
                    for (int i = 0; i < count; i++)
                    {
                        float rowHeight = label.CalcHeight(rows[i], inner);
                        rects[i] = new Rect(box.padding.left, offset, inner, rowHeight);
                        offset += rowHeight + (i + 1 < count ? 2 : 0);
                    }
                    measuredHeight = offset + box.padding.bottom;
                    measuredWidth = width; measuredCount = count; measuredLabel = label; measuredBox = box; dirty = false;
                }
                var area = new Rect(x, bottom ? y - measuredHeight : y, width, measuredHeight);
                GUI.Box(area, GUIContent.none, box);
                for (int i = 0; i < count; i++)
                {
                    var row = rects[i]; row.position += area.position;
                    GUI.Label(row, rows[i], label);
                }
                return area;
            }
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

        void OnDisable() { ReleaseDefense(); ResetMenuInput(); }

        void OnDestroy()
        {
            ui.Dispose();
            if (controller) { controller.FrameChanged -= ApplyFrame; controller.Faulted -= ConnectionFailed; }
            if (view)
            {
                view.CommandsRead -= ReadCommands; view.MenuRequested -= MenuRequested;
                view.MenuCommandsRead -= ReadMenuCommands;
            }
        }
    }
}
