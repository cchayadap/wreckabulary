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
        ItemCatalogue items;
        ModeDefinition mode;
        ResolvedCourse course;
        string actorId, search = "", notice = "";
        int seat, team;
        bool initialized, crafting;
        Vector2 recipeScroll;
        long receiptSequence, receiptEpoch = -1;
        MatchSnapshot frame;
        PlayerInventory inventory;
        MatchActorState actor;
        public event Action ReturnRequested;

        public void Initialize(WordboundGameplayController gameplay, WordboundPlayerView playerView,
            ItemCatalogue catalogue, ModeDefinition definition, ResolvedCourse resolved, string localActor)
        {
            if (initialized) throw new InvalidOperationException("Match HUD is already initialized.");
            controller = gameplay != null ? gameplay : throw new ArgumentNullException(nameof(gameplay));
            view = playerView != null ? playerView : throw new ArgumentNullException(nameof(playerView));
            items = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
            mode = definition ?? throw new ArgumentNullException(nameof(definition));
            course = resolved ?? throw new ArgumentNullException(nameof(resolved));
            actorId = localActor;
            var binding = controller.Authority.Match.Binding;
            seat = Enumerable.Range(0, binding.Seats.Count).Single(i => binding.Seats[i].PlayerId == actorId);
            team = binding.Seats[seat].Team;
            controller.FrameChanged += ApplyFrame;
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
            if (input.spellDown) { crafting = true; view.SetMenuOpen(true); return; }
            if (input.slot > 0) Send(WordboundAction.Equip, slot: input.slot - 1);
            if (input.grab) Interact();
            if (input.drop) Send(WordboundAction.Drop, slot: inventory?.CarriedFurniture >= 0 ? -1 : actor?.EquippedSlot ?? -1);
            if (input.attack)
            {
                if (input.aimHeld && actor != null && actor.EquippedSlot >= 0)
                    Send(WordboundAction.Throw, slot: actor.EquippedSlot);
                else Send(WordboundAction.Attack);
            }
        }

        void MenuRequested() => crafting = false;
        void Send(WordboundAction action, string target = "", int revision = 0, int slot = -1, string value = "")
        {
            try
            {
                controller.Submit(action, target, revision, slot, value);
                notice = "";
            }
            catch (InvalidOperationException) { notice = "Waiting for the connection. Try again shortly."; }
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
            if (target == null) { notice = "Move closer to a letter, object or keepsake."; return; }
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
                    if (puzzle != null) Send(WordboundAction.UsePuzzle, puzzle.Id, value: id);
                    else Send(WordboundAction.PickUp, id, item.Revision);
                    break;
            }
        }

        void OnGUI()
        {
            if (!initialized) return;
            Matrix4x4 saved = GUI.matrix;
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), .6f, 1.8f);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            float width = Screen.width / scale, height = Screen.height / scale;
            try
            {
                GUILayout.BeginArea(new Rect(18, 18, 360, 150), GUI.skin.box);
                GUILayout.Label(mode.Name);
                if (frame == null) GUILayout.Label("Preparing the world…");
                else
                {
                    var progress = frame.Progress.Teams.Single(t => t.Team == team);
                    if (mode.IsRace) GUILayout.Label($"Lap {progress.Lap + 1} / {mode.Laps} · Checkpoint {progress.NextCheckpoint}");
                    else if (mode.Objective == WordboundObjective.Knockouts)
                        GUILayout.Label($"Knockouts: {frame.Progress.Players.Single(p => p.Id == actorId).Knockouts} / {mode.KnockoutTarget}");
                    if (mode.TimeLimitSeconds > 0)
                    {
                        long seconds = Math.Max(0, mode.TimeLimitSeconds - frame.Progress.ElapsedTicks / ModeDefinition.TickRate);
                        GUILayout.Label($"{seconds / 60:00}:{seconds % 60:00} remaining");
                    }
                    GUILayout.Label($"Health {Mathf.CeilToInt(actor.Health)} · Stamina {Mathf.CeilToInt(actor.Stamina)}");
                    if (controller.IsSimulationPaused) GUILayout.Label("Waiting for the host…");
                    if (frame.Progress.Result != null)
                        GUILayout.Label(frame.Progress.Result.Confirmed ? frame.Progress.Result.Outcome : "Confirming result…");
                }
                GUILayout.EndArea();
                GUILayout.BeginArea(new Rect(18, height - 100, width - 36, 82), GUI.skin.box);
                GUILayout.Label("Letters: " + (inventory?.Letters.ToString() ?? ""));
                GUILayout.Label(notice != "" ? notice : "Interact to collect or pick up · Spell to craft · Aim + attack to throw");
                if (!view.MenuOpen && Target() != null) GUILayout.Label("Interact: use nearby object");
                GUILayout.EndArea();
                if (view.MenuOpen) Menu(width, height);
            }
            finally { GUI.matrix = saved; }
        }

        void Menu(float width, float height)
        {
            GUILayout.BeginArea(new Rect((width - 580) / 2, 48, 580, height - 170), GUI.skin.box);
            GUILayout.Label(crafting ? "Spell equipment" : "Wordbound");
            if (GUILayout.Button("Resume", GUILayout.Height(34))) { view.SetMenuOpen(false); crafting = false; }
            GUI.enabled = frame != null && !controller.IsSimulationPaused;
            if (inventory != null)
            {
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
                if (mode.IsRace && frame.Progress.Teams.Single(t => t.Team == team).KeepsakeCarrier == actorId &&
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
            if (GUILayout.Button("Return to party", GUILayout.Height(32))) ReturnRequested?.Invoke();
            GUILayout.EndArea();
        }

        static string RefusalText(string code) => code switch
        {
            "OutOfReach" => "Move closer and keep the object in sight.",
            "MissingLetters" => "Collect the missing letters first.",
            "HandsFull" => "Free a slot or put down the furniture.",
            "BlockedPlacement" => "That spot is blocked. Move to a clear space.",
            "EmptySlot" => "Choose an occupied equipment slot.",
            "NotAlive" => "Wait until you recover.",
            "StaleRevision" => "The object changed. Try again.",
            "TooEarly" => "Wait a moment before trying again.",
            "OutOfOrder" => "Complete the current puzzle first.",
            "Unavailable" => "That action is unavailable here.",
            _ => "That action could not be completed."
        };

        void OnDestroy()
        {
            if (controller) controller.FrameChanged -= ApplyFrame;
            if (view) { view.CommandsRead -= ReadCommands; view.MenuRequested -= MenuRequested; }
        }
    }
}
