using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary
{
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
            if (item.Shield != null && item.Shield.FrontArcDegrees >= 359f) root.AddComponent<HeldShieldAura>();
            var materials = MaterialLibrary.Load();
            if (materials) materials.ApplySkin(root, Skin.Standard);
            return gear;
        }

        public static void ApplyUse(PlayerController owner, ItemDefinition item)
        {
            var use = item.Use;
            if (use == null) return;
            if (use.Effect == UseEffect.Heal)
            {
                float restored = owner.Health.Heal(use.Amount);
                if (restored > 0f)
                {
                    Popup.Show("+" + Mathf.CeilToInt(restored), owner.OverheadPosition, GameFeedback.SkillColor(item.Id), 2.5f);
                    GameFeedback.Play(GameCue.Heal);
                    GameFeedback.Burst("Foam_Cloud", owner.transform.position + Vector3.up, .65f, GameFeedback.SkillColor(item.Id), .5f);
                }
                return;
            }
            if (use.Effect == UseEffect.Speed)
            {
                owner.Boost(use.Amount, use.Seconds);
                GameFeedback.Play(GameCue.Boost);
                GameFeedback.Burst("Speed_Trail", owner.transform.position + Vector3.up * .4f, 1f, GameFeedback.SkillColor(item.Id), .6f);
                return;
            }
            if (use.Effect != UseEffect.Bubble) return;
            int token = owner.Health.GiveOwnedBubble(use.Amount, use.Seconds);
            var visual = new GameObject(item.Id + " protection");
            visual.transform.SetParent(owner.visual ? owner.visual : owner.transform, false);
            visual.transform.localPosition = Vector3.up * 0.8f;
            var color = GameFeedback.SkillColor(item.Id);
            var material = Resources.Load<Material>("VFX/FoamBubble");
            var mesh = Resources.Load<Mesh>("VFX/BubbleSphere");
            if (material && mesh) Bubble(visual.transform, "Foam film", mesh, material, 1.5f);
            var bubbles = new Transform[7];
            var positions = new Vector3[bubbles.Length];
            for (int i = 0; i < bubbles.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / bubbles.Length;
                positions[i] = new Vector3(Mathf.Cos(angle) * .66f, Mathf.Sin(angle * 2f) * .44f, Mathf.Sin(angle) * .66f);
                if (!material || !mesh) continue;
                bubbles[i] = Bubble(visual.transform, "Foam pearl", mesh, material, .14f + .06f * (i % 3));
                bubbles[i].localPosition = positions[i];
            }
            GameFeedback.Play(GameCue.Protect);
            GameFeedback.Burst("Foam_Cloud", owner.transform.position + Vector3.up * .9f, .85f, color, .55f);
            var life = SummonedThing.Attach(visual, item.Id, owner, use.Seconds);
            life.ReturnsLetters = false;
            life.Tick = () =>
            {
                for (int i = 0; i < bubbles.Length; i++)
                    if (bubbles[i]) bubbles[i].localPosition = positions[i] + Vector3.up * (Mathf.Sin(Time.time * 2.2f + i) * .04f);
            };
            life.KeepAlive = () => owner && owner.Health.OwnsBubble(token) && owner.Health.Bubble > 0f;
            life.Ended = () => { if (owner) owner.Health.ClearOwnedBubble(token); };
        }

        static Transform Bubble(Transform parent, string name, Mesh mesh, Material material, float diameter)
        {
            var bubble = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            bubble.transform.SetParent(parent, false);
            bubble.transform.localScale = Vector3.one * diameter;
            bubble.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = bubble.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return bubble.transform;
        }
    }
}
