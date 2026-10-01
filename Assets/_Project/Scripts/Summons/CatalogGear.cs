using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>One physical representation for an enabled catalogue recipe; data selects its behavior.</summary>
    public static class CatalogGear
    {
        public static HeldWeapon Create(ItemDefinition item)
        {
            var root = new GameObject(item.Id);
            root.transform.SetParent(World.Transient, false);
            if (!ModelVisual.Spawn(item.Model, root.transform))
            {
                var visual = LetterBuilt.Spawn(item.Id, Vector3.one * 0.2f, 3, new Color(0.8f, 0.6f, 0.4f), root.transform, false);
                visual.name = "Recipe visual";
            }
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(Mathf.Max(0.1f, item.Size[0]), Mathf.Max(0.1f, item.Size[1]), Mathf.Max(0.1f, item.Size[2]));
            box.center = Vector3.up * box.size.y * 0.5f;
            var body = root.AddComponent<Rigidbody>();
            body.mass = item.IsTwoHanded ? 4f : 1f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var gear = root.AddComponent<HeldWeapon>();
            gear.Configure(item);
            var materials = MaterialLibrary.Load();
            if (materials) materials.ApplySkin(root, Skin.Standard);
            return gear;
        }

        public static void ApplyUse(PlayerController owner, ItemDefinition item)
        {
            var use = item.Use;
            if (use == null) return;
            if (use.Effect == UseEffect.Heal) { owner.Health.Heal(use.Amount); return; }
            if (use.Effect == UseEffect.Speed) { owner.Boost(use.Amount, use.Seconds); return; }
            if (use.Effect != UseEffect.Bubble) return;
            int token = owner.Health.GiveOwnedBubble(use.Amount, use.Seconds);
            var visual = new GameObject(item.Id + " protection");
            visual.transform.SetParent(owner.visual ? owner.visual : owner.transform, false);
            visual.transform.localPosition = Vector3.up * 0.8f;
            // A wire bubble keeps the body visible instead of hiding it under an opaque sphere.
            var line = visual.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 32;
            line.startWidth = line.endWidth = 0.025f;
            line.sharedMaterial = GameAssets.I.Tinted(new Color(0.4f, 0.8f, 1f));
            for (int i = 0; i < 32; i++)
            {
                float a = i * Mathf.PI * 2f / 32f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.7f);
            }
            var life = SummonedThing.Attach(visual, item.Id, owner, use.Seconds);
            life.ReturnsLetters = false;
            life.KeepAlive = () => owner && owner.Health.OwnsBubble(token) && owner.Health.Bubble > 0f;
            life.Ended = () => { if (owner) owner.Health.ClearOwnedBubble(token); };
        }
    }
}
