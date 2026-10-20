using System;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WordboundNetworkBody))]
    [DefaultExecutionOrder(-100)]
    public sealed class WordboundGameplayBody : NetworkBehaviour
    {
        [Networked] public Vector3 ShapeSize { get; private set; }
        [Networked] public NetworkString<_64> VisualKey { get; private set; }
        [Networked] public int CarrierSeat { get; private set; }
        WordboundNetworkBody body;
        Vector3 appliedSize;
        string appliedKey;
        int appliedCarrier = -2;
        public WordboundNetworkBody Body => body != null ? body : body = GetComponent<WordboundNetworkBody>();
        public Transform VisualRoot => GetComponent<NetworkRigidbody>().InterpolationTarget;
        public event Action<WordboundGameplayBody> PresentationChanged;

        public void Configure(WordboundBodyPresentation presentation)
        {
            if (Object == null || !Object.HasStateAuthority) throw new InvalidOperationException("Only the host configures body presentation.");
            ShapeSize = new Vector3(presentation.Size.X, presentation.Size.Y, presentation.Size.Z);
            VisualKey = presentation.VisualKey; CarrierSeat = presentation.CarrierSeat;
            Apply();
        }

        public override void Spawned() { if (ShapeSize.x > 0) Apply(); }
        public override void FixedUpdateNetwork() { if (ShapeSize.x > 0) Apply(); }
        public override void Render() { if (ShapeSize.x > 0) Apply(); }

        void Apply()
        {
            var size = ShapeSize;
            var key = VisualKey.ToString();
            _ = new WordboundBodyPresentation(new WorldVector(size.x, size.y, size.z), key, CarrierSeat);
            if (!size.Equals(appliedSize))
            {
                var capsule = GetComponent<CapsuleCollider>();
                var box = GetComponent<BoxCollider>();
                if (capsule != null)
                {
                    if (key != "player" || Math.Abs(size.x - .6f) > .00001f ||
                        Math.Abs(size.y - 1.8f) > .00001f || Math.Abs(size.z - .6f) > .00001f)
                        throw new InvalidOperationException("Player capsule differs from its canonical shape.");
                    capsule.direction = 1; capsule.radius = .3f; capsule.height = 1.8f; capsule.center = Vector3.up * .9f;
                }
                else if (box != null) { box.size = size; box.center = Vector3.up * size.y / 2; }
                else throw new InvalidOperationException("Gameplay prefab needs its approved root capsule or box.");
                appliedSize = size;
            }
            if (key != appliedKey || appliedCarrier != CarrierSeat)
            {
                appliedKey = key; appliedCarrier = CarrierSeat;
                PresentationChanged?.Invoke(this);
            }
            var visual = VisualRoot;
            if (visual != null)
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true)) renderer.enabled = Body.BodyActive;
        }
    }
}
