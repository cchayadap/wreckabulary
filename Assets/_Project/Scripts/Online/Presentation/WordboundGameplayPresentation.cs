using System;
using System.Collections.Generic;
using System.Globalization;
using Fusion;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class WordboundGameplayPresentation : MonoBehaviour
    {
        sealed class View : IDisposable
        {
            public WordboundGameplayBody Body;
            public NetworkRunner Runner;
            public Transform Parent;
            public string Key, Actor, Appearance, HeldSkin;
            public NetworkString<_64> NetworkKey;
            public int Seat = -1;
            public Vector3 Size;
            public WordboundVisualInstance World, Held;
            public WordboundAvatarMotion Motion;
            public AvatarExpressionDriver Expression;
            public bool Carrying, TwoHanded;
            public float DefenseBlend;
            public void Dispose() { Expression?.Dispose(); Motion?.Dispose(); World?.Dispose(); Held?.Dispose(); }
        }

        readonly List<View> views = new List<View>();
        readonly Dictionary<string, string> appearances = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly Dictionary<string, string> equipped = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly Dictionary<string, MatchDefenseState> defenses = new Dictionary<string, MatchDefenseState>(StringComparer.Ordinal);
        readonly Dictionary<string, MatchActorState> actors = new Dictionary<string, MatchActorState>(StringComparer.Ordinal);
        readonly Dictionary<string, PlayerInventory> expressionInventories = new Dictionary<string, PlayerInventory>(StringComparer.Ordinal);
        readonly Dictionary<string, ItemDefinition> meleeItems = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        readonly HashSet<string> meleeAvailable = new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<int, ItemInstance> inventoryItems = new Dictionary<int, ItemInstance>();
        readonly HashSet<string> presentPlayers = new HashSet<string>(StringComparer.Ordinal);
        readonly WordboundDefensePose defaultDefensePose = new WordboundDefensePose();
        [SerializeField] AvatarExpressionProfile expressionProfile;
        WordboundVisualResolver resolver;
        WordboundGameplayController controller;
        MatchBinding binding;
        long frameTick, frameEpoch = -1;
        bool frameUnavailable, frameHasResult;
        public string Fault { get; private set; } = "";
        public event Action<string> Failed;
        public int VisualCount => views.Count;
        public WordboundVisualResolver ArtworkResolver => resolver;

        public void Initialize(WordboundVisualResolver artwork)
        {
            if (resolver != null) throw new InvalidOperationException("Presentation is already initialized.");
            resolver = artwork ?? throw new ArgumentNullException(nameof(artwork));
        }

        public void Bind(WordboundGameplayController gameplay)
        {
            if (!gameplay || resolver == null || controller != null)
                throw new InvalidOperationException("Initialize presentation before binding one gameplay controller.");
            controller = gameplay;
            controller.FrameChanged += ApplyFrame;
            controller.Faulted += GameplayFailed;
            if (controller.Frame != null) ApplyFrame(controller.Frame);
        }

        public void ApplyFrame(MatchSnapshot frame)
        {
            if (frame == null) return;
            if (binding != null && binding.Hash != frame.Binding.Hash)
                throw new InvalidOperationException("Presentation cannot mix different signed matches.");
            binding = frame.Binding;
            if (frameEpoch != frame.Epoch)
                foreach (var view in views) view.DefenseBlend = 0;
            frameEpoch = frame.Epoch;
            frameTick = frame.Progress.Tick;
            frameUnavailable = frame.Progress.Migrating || frame.Progress.Result != null;
            frameHasResult = frame.Progress.Result != null;
            appearances.Clear();
            equipped.Clear();
            defenses.Clear();
            actors.Clear(); expressionInventories.Clear(); meleeItems.Clear(); meleeAvailable.Clear(); inventoryItems.Clear(); presentPlayers.Clear();
            var inventories = frame.Economy.Players;
            bool needsMelee = false;
            foreach (var actor in frame.Actors) needsMelee |= actor.Attack != null;
            foreach (var view in views) needsMelee |= view.Motion != null && view.Motion.HasMelee;
            if (needsMelee)
                foreach (var item in frame.Economy.Items) inventoryItems.Add(item.Id, item);
            foreach (var player in frame.Progress.Players)
                if (player.Connected && !player.Eliminated && !player.Returned) presentPlayers.Add(player.Id);
            foreach (var actor in frame.Actors)
            {
                appearances.Add(actor.PlayerId, actor.Appearance);
                actors.Add(actor.PlayerId, actor);
                if (actor.Defense != null) defenses.Add(actor.PlayerId, actor.Defense);
                int seat = -1;
                for (int i = 0; i < frame.Binding.Seats.Count; i++)
                    if (frame.Binding.Seats[i].PlayerId == actor.PlayerId) { seat = i; break; }
                string selected = "";
                foreach (var inventory in inventories)
                {
                    if (inventory.PlayerId != seat) continue;
                    expressionInventories.Add(actor.PlayerId, inventory);
                    if (inventory.CarriedFurniture < 0 && actor.EquippedSlot >= 0 && actor.EquippedSlot < inventory.Slots.Length &&
                        inventory.Slots[actor.EquippedSlot] > 0) selected = "item:" + inventory.Slots[actor.EquippedSlot];
                    if (inventory.CanAct && inventory.Craft == null && inventory.CarriedFurniture < 0 &&
                        presentPlayers.Contains(actor.PlayerId)) meleeAvailable.Add(actor.PlayerId);
                    break;
                }
                equipped.Add(actor.PlayerId, selected);
                if (selected.StartsWith("item:", StringComparison.Ordinal) &&
                    int.TryParse(selected.Substring(5), NumberStyles.None, CultureInfo.InvariantCulture, out int id) &&
                    inventoryItems.TryGetValue(id, out var held) && held.State == ItemState.Held &&
                    held.HolderId == seat && !held.IsGone && resolver.TryGetItem(held.Word, out var definition))
                    meleeItems.Add(actor.PlayerId, definition);
            }
            foreach (var view in views) { ApplyAppearance(view); ApplyMelee(view); ObserveExpression(view, frame); }
        }

        public void ConfigureVisual(WordboundGameplayBody body)
        {
            if (resolver == null || !body || body.Object == null || !body.Object.IsValid)
                throw new InvalidOperationException("Artwork requires an initialized resolver and a spawned gameplay body.");
            if (Fault != "") throw new InvalidOperationException(Fault);
            try
            {
                View view = null;
                foreach (var candidate in views) if (candidate.Body == body) { view = candidate; break; }
                if (view == null)
                {
                    view = new View { Body = body, Runner = body.Runner };
                    views.Add(view);
                }
                Refresh(view);
            }
            catch (Exception error) { SetFault(error); throw; }
        }

        void Refresh(View view)
        {
            var body = view.Body;
            var parent = body.VisualRoot;
            if (!parent) return;
            var size = body.ShapeSize;
            if (view.World != null && view.Parent == parent && view.NetworkKey.Equals(body.VisualKey) && view.Size == size) return;
            if (parent == body.transform || !parent.IsChildOf(body.transform) ||
                parent.GetComponentsInChildren<Collider>(true).Length != 0 ||
                parent.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
                (parent.lossyScale - Vector3.one).sqrMagnitude > .000001f)
                throw new InvalidOperationException("VisualRoot must be a unit-scale, collider-free child of its canonical body.");
            string key = body.VisualKey.ToString();
            if (string.IsNullOrEmpty(key) || body.ShapeSize.x <= 0) return;
            var replacement = resolver.Create(key, size, parent);
            WordboundAvatarMotion motion = null;
            try { if (key == "player") motion = new WordboundAvatarMotion(replacement); }
            catch { replacement.Dispose(); throw; }
            view.Dispose();
            view.World = replacement; view.Motion = motion; view.Held = null; view.HeldSkin = null; view.Expression = null;
            view.Key = key; view.NetworkKey = body.VisualKey; view.Size = size;
            view.Parent = parent; view.Appearance = null; view.Seat = -1;
            view.Actor = body.Body.ActorId.ToString();
            if (key == "player")
            {
                string id = body.Body.BodyId.ToString();
                if (!id.StartsWith("player:", StringComparison.Ordinal) ||
                    !int.TryParse(id.Substring(7), NumberStyles.None, CultureInfo.InvariantCulture, out int seat) ||
                    seat < 0 || seat > 7)
                    throw new InvalidOperationException("Avatar body requires a canonical roster seat.");
                view.Seat = seat;
                view.Expression = new AvatarExpressionDriver(replacement.Model, expressionProfile, seat);
                ApplyAppearance(view);
            }
            view.World.Show(body.Body.BodyActive);
            ApplyMelee(view);
        }

        void ApplyMelee(View view)
        {
            if (view.Motion == null) return;
            string id = view.Actor ?? "";
            actors.TryGetValue(id, out var actor);
            equipped.TryGetValue(id, out var item);
            meleeItems.TryGetValue(id, out var definition);
            view.Motion.ObserveMelee(frameEpoch, frameTick, actor, item, definition,
                !frameUnavailable && meleeAvailable.Contains(id));
        }

        void ApplyAppearance(View view)
        {
            if (view.Key != "player" || view.World == null) return;
            string appearance = appearances.TryGetValue(view.Actor ?? "", out var serialized) ? serialized : "";
            if (view.Appearance == appearance) return;
            view.World.Dress(appearance);
            view.Appearance = appearance;
        }

        void ObserveExpression(View view, MatchSnapshot frame)
        {
            if (view.Expression == null) return;
            actors.TryGetValue(view.Actor ?? "", out var actor);
            PlayerProgress player = null;
            WorldBodyState body = null;
            for (int i = 0; i < frame.Progress.Players.Count; i++)
                if (frame.Progress.Players[i].Id == view.Actor) { player = frame.Progress.Players[i]; break; }
            for (int i = 0; i < frame.Bodies.Count; i++)
                if (frame.Bodies[i].Kind == WorldBodyKind.Player && frame.Bodies[i].SourceId == view.Actor)
                { body = frame.Bodies[i]; break; }
            bool victory = false;
            var result = frame.Progress.Result;
            if (result != null && result.Confirmed && player != null)
                for (int i = 0; i < result.WinningTeams.Count; i++) victory |= result.WinningTeams[i] == player.Team;
            var velocity = body?.Velocity ?? default;
            bool playing = result == null;
            equipped.TryGetValue(view.Actor ?? "", out var item);
            expressionInventories.TryGetValue(view.Actor ?? "", out var inventory);
            view.Expression.Observe(new AvatarExpressionSample {
                Epoch = frame.Epoch, Tick = frame.Progress.Tick,
                Available = actor != null && player != null && body != null && player.Connected && !player.Returned && !frame.Progress.Migrating,
                Health = actor?.Health ?? 0, Downed = actor == null || actor.Health <= 0 || player == null || player.Eliminated,
                Hurt = playing && actor != null && actor.StunnedUntilTick > frame.Progress.Tick,
                Focus = playing && actor != null && (actor.Attack != null || actor.Defense != null || actor.DodgeUntilTick > frame.Progress.Tick ||
                    inventory != null && inventory.CanAct && inventory.Craft != null),
                Falling = playing && body != null && body.Active && velocity.Y < -2.5f,
                Moving = playing && velocity.X * velocity.X + velocity.Z * velocity.Z > .04f,
                RecoveryGeneration = player?.RecoveryGeneration ?? 0,
                Equipped = !string.IsNullOrEmpty(item) || inventory != null && inventory.CarriedFurniture >= 0, Celebrate = victory
            });
        }

        void LateUpdate() => Present(Time.deltaTime);

        public void Present(float deltaTime)
        {
            if (resolver == null || Fault != "" || !isActiveAndEnabled) return;
            try
            {
                for (int i = views.Count - 1; i >= 0; i--)
                {
                    var view = views[i];
                    if (!view.Body || view.Body.Object == null || !view.Body.Object.IsValid ||
                        view.Body.Runner != view.Runner)
                    { view.Dispose(); views.RemoveAt(i); continue; }
                    Refresh(view);
                    view.Carrying = false; view.TwoHanded = false;
                    view.World?.Show(view.Body.Body.BodyActive);
                }
                foreach (var view in views)
                {
                    if (view.World == null || view.Body.Body.BodyActive || view.Body.CarrierSeat < 0) continue;
                    var carrier = Carrier(view);
                    if (carrier == null || !VisibleInHand(view, carrier)) continue;
                    carrier.Carrying = true;
                    carrier.TwoHanded |= view.World.TwoHanded;
                }
                foreach (var view in views)
                    view.Motion?.Step(deltaTime, view.Body.Body.BodyActive, view.Carrying, view.TwoHanded,
                        !frameUnavailable && (controller == null || !controller.IsSimulationPaused));
                foreach (var view in views)
                    view.Expression?.Step(deltaTime, frameHasResult || !frameUnavailable && (controller == null || !controller.IsSimulationPaused));
                foreach (var view in views) ShowHeld(view, deltaTime);
            }
            catch (Exception error) { SetFault(error); }
        }

        View Carrier(View source)
        {
            foreach (var player in views)
                if (player.Seat == source.Body.CarrierSeat && player.Runner == source.Runner &&
                    player.World != null && player.Body.Body.BodyActive && player.World.RightHand)
                    return player;
            return null;
        }

        void ShowHeld(View view, float deltaTime)
        {
            if (view.World == null || view.Body.Body.BodyActive || view.Body.CarrierSeat < 0)
            { view.DefenseBlend = 0; view.Held?.Show(false); return; }
            var carrier = Carrier(view);
            if (carrier == null || !VisibleInHand(view, carrier))
            { view.DefenseBlend = 0; view.Held?.Show(false); return; }
            string skin = resolver.SkinFor(view.Key, carrier.World.Outfit);
            if (view.Held == null || !view.Held.Root || view.HeldSkin != skin)
            {
                var replacement = resolver.Create(view.Key, view.Size, carrier.Parent, skin);
                view.Held?.Dispose();
                view.Held = replacement; view.HeldSkin = skin;
            }
            var hand = view.Key == "keepsake" && carrier.World.LeftHand ? carrier.World.LeftHand : carrier.World.RightHand;
            view.Held.PlaceAtHand(carrier.Parent, hand, view.Body.Body.ItemMode == (int)ItemState.Held);
            ApplyDefensePose(view, carrier, hand, deltaTime);
            view.Held.Show(true);
        }

        void ApplyDefensePose(View item, View carrier, Transform hand, float deltaTime)
        {
            var pose = resolver.Profile?.DefensePose ?? defaultDefensePose;
            string itemId = item.Body.Body.BodyId.ToString();
            bool active = !frameUnavailable && (controller == null || !controller.IsSimulationPaused) &&
                (controller == null || controller.Bodies.TryGetValue(itemId, out var current) && current == item.Body.Body) &&
                item.Body.Body.ItemMode == (int)ItemState.Held &&
                defenses.TryGetValue(carrier.Actor, out var defense) &&
                WordboundDefensePose.IsActive(defense, itemId, frameTick);
            if (!active || !pose.TryGet(item.Key, out var offset, out var euler))
            { item.DefenseBlend = 0; return; }
            float delta = float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) ? 0 : Mathf.Clamp(deltaTime, 0, .1f);
            item.DefenseBlend = pose.BlendSeconds <= 0 ? 1 :
                Mathf.MoveTowards(item.DefenseBlend, 1, delta / pose.BlendSeconds);
            WordboundDefensePose.PlaceAroundGrip(item.Held.Root.transform, item.Held.Grip, hand,
                carrier.Parent, offset, euler, item.DefenseBlend);
        }

        bool VisibleInHand(View item, View carrier) => item.Body.Body.ItemMode != (int)ItemState.Held ||
            equipped.TryGetValue(carrier.Actor, out var selected) && selected == item.Body.Body.BodyId.ToString();

        void GameplayFailed(string reason) => SetFault(new InvalidOperationException(reason));
        void SetFault(Exception error)
        {
            if (Fault != "") return;
            Fault = error.GetType().Name + ": " + error.Message;
            foreach (var view in views) { view.Expression?.Reset(); view.World?.Show(false); view.Held?.Show(false); }
            Failed?.Invoke(Fault);
        }
        void OnDisable()
        {
            foreach (var view in views) { view.Expression?.Reset(); view.Motion?.StopMelee(); view.World?.Show(false); view.Held?.Show(false); }
        }
        public void Clear()
        {
            foreach (var view in views) view.Dispose();
            views.Clear(); appearances.Clear(); equipped.Clear(); defenses.Clear();
            actors.Clear(); expressionInventories.Clear(); meleeItems.Clear(); meleeAvailable.Clear(); inventoryItems.Clear(); presentPlayers.Clear();
            frameEpoch = -1; frameTick = 0; frameUnavailable = true; frameHasResult = false; binding = null; Fault = "";
        }
        void OnDestroy()
        {
            if (controller)
            {
                controller.FrameChanged -= ApplyFrame;
                controller.Faulted -= GameplayFailed;
            }
            Clear();
        }
    }
}
