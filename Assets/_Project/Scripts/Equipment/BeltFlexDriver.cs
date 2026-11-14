using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    [DefaultExecutionOrder(200)]
    public sealed class BeltFlexDriver : MonoBehaviour
    {
        public const int Segments = 6;

        FlexibleWeaponProfile profile;
        FlexibleWeaponDefinition definition;
        HeldWeapon weapon;
        Transform[] bones;
        Transform tip;
        Quaternion[] rest;
        Vector3[] axes;
        Quaternion tipRest;
        Vector3 tipAxis;
        Renderer skinned;
        Transform visualRoot;
        Quaternion visualRest;
        Vector3 grip;
        float hang, lift;
        readonly float[] angles = new float[Segments];
        float tipAngle;
        PlayerController owner;
        string skin = Skin.Standard;

        public float[] Angles => angles;
        public float TipAngle => tipAngle;
        public SwingPhase Phase { get; private set; }
        public bool Ready => bones != null;
        public Transform Root => bones != null ? bones[0].parent : null;
        public Transform Tip => tip;
        public string CurrentSkin => skin;

        public void Configure(FlexibleWeaponProfile source, FlexibleWeaponDefinition belt, Transform visual)
        {
            profile = source;
            definition = belt;
            weapon = GetComponent<HeldWeapon>();
            visualRoot = visual;
            visualRest = visual.localRotation;
            grip = belt != null && belt.Grip != null && belt.Grip.Length == 3 ? new Vector3(belt.Grip[0], belt.Grip[1], belt.Grip[2]) : Vector3.zero;
            hang = source ? (source.invertBend ? 1f : -1f) * source.heldDroop : 0f;
            skinned = visual.GetComponentInChildren<SkinnedMeshRenderer>(true);
            bones = new Transform[Segments];
            rest = new Quaternion[Segments];
            axes = new Vector3[Segments];
            for (int i = 0; i < Segments; i++)
            {
                bones[i] = Find(visual, $"Belt_0{i + 1}");
                if (!bones[i]) { bones = null; return; }
                rest[i] = bones[i].localRotation;
                axes[i] = AxisFor(bones[i], visual);
            }
            tip = Find(visual, "Belt_Tip");
            if (tip)
            {
                tipRest = tip.localRotation;
                tipAxis = AxisFor(tip, visual);
            }
        }

        void Hang(bool held, bool swinging)
        {
            float target = held && !swinging ? 1f : 0f;
            lift = Mathf.Lerp(lift, target, 1f - Mathf.Exp(-(target > lift ? 8f : 18f) * Time.deltaTime));
            var local = visualRest;
            var parent = visualRoot.parent;
            if (lift > 0.001f && parent)
            {
                var rest = parent.rotation * visualRest;
                var hanging = Quaternion.FromToRotation(rest * Vector3.up, Vector3.down) * rest;
                local = Quaternion.Inverse(parent.rotation) * Quaternion.Slerp(rest, hanging, lift);
            }
            visualRoot.localRotation = local;
            visualRoot.localPosition = grip - local * Quaternion.Inverse(visualRest) * grip;
        }

        static Vector3 AxisFor(Transform bone, Transform visual) => (Quaternion.Inverse(bone.rotation) * visual.right).normalized;

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        public void SetSkin(string next)
        {
            skin = string.IsNullOrEmpty(next) ? Skin.Standard : next;
            var leather = profile ? profile.LeatherFor(skin) : null;
            if (!skinned || !leather) return;
            var shared = skinned.sharedMaterials;
            if (shared.Length > 0 && shared[0] != leather)
            {
                shared[0] = leather;
                skinned.sharedMaterials = shared;
            }
        }

        public float[] Evaluate(double since, bool swinging, bool held, double time, float speed, float[] into)
        {
            var swing = definition.Swing;
            float total;
            if (swinging)
            {
                Phase = FlexibleSwing.PhaseAt(definition.Melee, since);
                float release = Mathf.Clamp01((float)(since / Mathf.Max(1e-3f, definition.Melee.Windup * 0.5f)));
                total = swing.TipBend(definition.Melee, since) + hang * (1f - release * release * (3f - 2f * release));
            }
            else { Phase = SwingPhase.Ready; total = held ? hang : 0f; }
            for (int i = 0; i < Segments; i++)
            {
                float a = total * swing.Weight(i) + (held && !swinging ? swing.IdleBend(time, speed, i) : 0f);
                into[i] = Mathf.Clamp(a, -profile.jointLimit, profile.jointLimit);
            }
            tipAngle = swinging ? Mathf.Clamp(total * 0.12f, -12f, 12f) : 0f;
            return into;
        }

        void LateUpdate()
        {
            if (!Ready || definition == null) return;
            if (weapon && weapon.IsHeld)
            {
                var holder = weapon.GetComponentInParent<PlayerController>();
                if (holder != owner)
                {
                    owner = holder;
                    var appearance = owner ? owner.GetComponent<PlayerAppearance>() : null;
                    SetSkin(appearance ? appearance.SkinFor(weapon.word) : skin);
                }
            }
            else owner = null;
            bool held = weapon && weapon.IsHeld;
            bool swinging = weapon && weapon.SwingStartedAt >= 0f;
            float speed = owner && owner.Body ? World.Flat(owner.Body.linearVelocity).magnitude / 4f : 0f;
            Hang(held, swinging);
            Evaluate(swinging ? Time.time - weapon.SwingStartedAt : -1.0, swinging, held, Time.time, speed, angles);
            float sign = profile.invertBend ? 1f : -1f;
            for (int i = 0; i < Segments; i++)
                bones[i].localRotation = rest[i] * Quaternion.AngleAxis(sign * angles[i], axes[i]);
            if (tip) tip.localRotation = tipRest * Quaternion.AngleAxis(sign * tipAngle, tipAxis);
        }
    }
}
