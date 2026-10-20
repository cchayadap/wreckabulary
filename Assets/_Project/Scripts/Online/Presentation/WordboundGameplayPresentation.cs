using System;
using System.Collections.Generic;
using System.Globalization;
using Fusion;
using UnityEngine;
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
            public bool Carrying, TwoHanded;
            public void Dispose() { Motion?.Dispose(); World?.Dispose(); Held?.Dispose(); }
        }

        readonly List<View> views = new List<View>();
        readonly Dictionary<string, string> appearances = new Dictionary<string, string>(StringComparer.Ordinal);
        WordboundVisualResolver resolver;
        WordboundGameplayController controller;
        MatchBinding binding;
        public string Fault { get; private set; } = "";
        public event Action<string> Failed;
        public int VisualCount => views.Count;

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
            appearances.Clear();
            foreach (var actor in frame.Actors) appearances.Add(actor.PlayerId, actor.Appearance);
            foreach (var view in views) ApplyAppearance(view);
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
            view.World = replacement; view.Motion = motion; view.Held = null; view.HeldSkin = null;
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
                ApplyAppearance(view);
            }
            view.World.Show(body.Body.BodyActive);
        }

        void ApplyAppearance(View view)
        {
            if (view.Key != "player" || view.World == null) return;
            string appearance = appearances.TryGetValue(view.Actor ?? "", out var serialized) ? serialized : "";
            if (view.Appearance == appearance) return;
            view.World.Dress(appearance);
            view.Appearance = appearance;
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
                    if (carrier == null) continue;
                    carrier.Carrying = true;
                    carrier.TwoHanded |= view.World.TwoHanded;
                }
                foreach (var view in views)
                    view.Motion?.Step(deltaTime, view.Body.Body.BodyActive, view.Carrying, view.TwoHanded);
                foreach (var view in views) ShowHeld(view);
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

        void ShowHeld(View view)
        {
            if (view.World == null || view.Body.Body.BodyActive || view.Body.CarrierSeat < 0)
            { view.Held?.Show(false); return; }
            var carrier = Carrier(view);
            if (carrier == null) { view.Held?.Show(false); return; }
            string skin = resolver.SkinFor(view.Key, carrier.World.Outfit);
            if (view.Held == null || !view.Held.Root || view.HeldSkin != skin)
            {
                var replacement = resolver.Create(view.Key, view.Size, carrier.Parent, skin);
                view.Held?.Dispose();
                view.Held = replacement; view.HeldSkin = skin;
            }
            var hand = view.Key == "keepsake" && carrier.World.LeftHand ? carrier.World.LeftHand : carrier.World.RightHand;
            view.Held.PlaceAtHand(carrier.Parent, hand, view.Body.Body.ItemMode == (int)ItemState.Held);
            view.Held.Show(true);
        }

        void GameplayFailed(string reason) => SetFault(new InvalidOperationException(reason));
        void SetFault(Exception error)
        {
            if (Fault != "") return;
            Fault = error.GetType().Name + ": " + error.Message;
            foreach (var view in views) { view.World?.Show(false); view.Held?.Show(false); }
            Failed?.Invoke(Fault);
        }
        void OnDisable()
        {
            foreach (var view in views) { view.World?.Show(false); view.Held?.Show(false); }
        }
        public void Clear()
        {
            foreach (var view in views) view.Dispose();
            views.Clear(); appearances.Clear(); binding = null; Fault = "";
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
