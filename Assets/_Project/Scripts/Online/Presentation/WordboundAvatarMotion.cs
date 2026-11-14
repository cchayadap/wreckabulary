using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online
{
    public sealed class WordboundAvatarMotion : IDisposable
    {
        PlayableGraph graph;
        AnimationMixerPlayable locomotion, holding, melee;
        AnimationLayerMixerPlayable layers;
        AnimationClipPlayable walk, run, swing, thrust;
        AvatarMask upperBody;
        readonly Transform root;
        Vector3 previousPosition;
        bool sampled;
        float speed, holdWeight;
        long observedEpoch = -1, observedTick = -1, attackStart, attackEnd, retiredStart;
        string attackWeapon;
        int windupTicks, activeTicks, cycleTicks;
        bool thrusting;
        double confirmedAge, interpolation, presentedAge;
        internal bool HasMelee => attackStart != 0;

        public WordboundAvatarMotion(WordboundVisualInstance visual)
        {
            if (visual == null || !visual.Model) throw new ArgumentNullException(nameof(visual));
            root = visual.Root.transform.parent;
            var animator = visual.Model.GetComponentInChildren<Animator>(true);
            var library = ModelLibrary.Load();
            if (!animator || !library || !visual.RightHand || !visual.LeftHand)
                throw new InvalidOperationException("The authored avatar requires its animator and both grip sockets.");
            try
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph = PlayableGraph.Create("Wordbound cosmetic avatar");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                locomotion = AnimationMixerPlayable.Create(graph, 3);
                holding = AnimationMixerPlayable.Create(graph, 2);
                melee = AnimationMixerPlayable.Create(graph, 2);
                layers = AnimationLayerMixerPlayable.Create(graph, 3);
                Connect(library, "Idle", locomotion, 0);
                walk = Connect(library, "Walk_InPlace", locomotion, 1);
                run = Connect(library, "Run_InPlace", locomotion, 2);
                Connect(library, "Hold_OneHand", holding, 0);
                Connect(library, "Carry_TwoHand", holding, 1);
                swing = Connect(library, "Swing_OneHand", melee, 0);
                thrust = Connect(library, "Thrust_OneHand", melee, 1);
                swing.SetSpeed(0);
                thrust.SetSpeed(0);
                upperBody = MakeUpperBodyMask(animator.transform);
                graph.Connect(locomotion, 0, layers, 0);
                graph.Connect(holding, 0, layers, 1);
                graph.Connect(melee, 0, layers, 2);
                layers.SetLayerMaskFromAvatarMask(1, upperBody);
                layers.SetLayerMaskFromAvatarMask(2, upperBody);
                layers.SetInputWeight(0, 1);
                locomotion.SetInputWeight(0, 1);
                AnimationPlayableOutput.Create(graph, "Authored avatar", animator).SetSourcePlayable(layers);
                graph.Play();
                graph.Evaluate(0);
            }
            catch { Dispose(); throw; }
        }

        AnimationClipPlayable Connect(ModelLibrary library, string name, AnimationMixerPlayable mixer, int port)
        {
            var clip = library.FindClip(WordboundVisualResolver.AvatarKey, name);
            if (!clip || clip.legacy || clip.isHumanMotion)
                throw new InvalidOperationException("Required Generic avatar clip is missing: " + name);
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            graph.Connect(playable, 0, mixer, port);
            return playable;
        }

        internal void ObserveMelee(long epoch, long tick, MatchActorState actor, string equippedItem,
            ItemDefinition item, bool available)
        {
            if (epoch < observedEpoch || epoch == observedEpoch && tick < observedTick) return;
            if (epoch != observedEpoch)
            {
                StopMelee(); retiredStart = 0; observedTick = -1; observedEpoch = epoch;
            }
            bool advanced = tick > observedTick;
            observedTick = tick;
            bool supported = item?.Melee != null &&
                (item.Family == HandlingFamily.MeleeSwing || item.Family == HandlingFamily.MeleeThrust);
            if (!available || actor == null || actor.Health <= 0 || actor.StunnedUntilTick > tick ||
                actor.Defense != null || !supported || string.IsNullOrEmpty(equippedItem))
            { StopMelee(); return; }
            var attack = actor.Attack;
            if (attack != null)
            {
                int cycle = WordboundCombatPolicy.CycleTicks(item.Melee);
                int windup = WordboundCombatPolicy.WindupTicks(item.Melee);
                int active = WordboundCombatPolicy.ActiveTicks(item.Melee);
                if (attack.WeaponEntityId != equippedItem || attack.StartedTick > tick ||
                    attack.StartedTick <= retiredStart || tick - attack.StartedTick >= windup + active ||
                    actor.NextAttackTick - attack.StartedTick != cycle)
                { StopMelee(); return; }
                if (attackStart != attack.StartedTick || attackWeapon != attack.WeaponEntityId)
                {
                    StopMelee();
                    attackStart = attack.StartedTick; attackEnd = actor.NextAttackTick;
                    attackWeapon = attack.WeaponEntityId;
                    windupTicks = windup; activeTicks = active; cycleTicks = cycle;
                    thrusting = item.Family == HandlingFamily.MeleeThrust;
                    interpolation = 0;
                }
            }
            else if (attackStart == 0 || tick - attackStart < windupTicks + activeTicks)
            { StopMelee(); return; }
            if (attackWeapon != equippedItem || actor.NextAttackTick != attackEnd || tick >= attackEnd)
            { StopMelee(); return; }
            if (advanced) interpolation = 0;
            confirmedAge = tick - attackStart;
        }

        internal void StopMelee()
        {
            retiredStart = Math.Max(retiredStart, attackStart);
            attackStart = 0; attackEnd = 0; attackWeapon = null;
            confirmedAge = 0; interpolation = 0; presentedAge = 0;
            if (layers.IsValid()) layers.SetInputWeight(2, 0);
        }

        public void Step(float deltaTime, bool active, bool carrying, bool twoHanded) =>
            Step(deltaTime, active, carrying, twoHanded, true);

        internal void Step(float deltaTime, bool active, bool carrying, bool twoHanded, bool actionRunning)
        {
            if (!graph.IsValid() || !root) return;
            float delta = Mathf.Clamp(deltaTime, 0, .1f);
            var position = root.position;
            var displacement = position - previousPosition;
            float nextSpeed = sampled && active && delta > 0 ? new Vector2(displacement.x, displacement.z).magnitude / delta : 0;
            if (nextSpeed > 20) nextSpeed = 0;
            previousPosition = position; sampled = active;
            speed = Mathf.MoveTowards(speed, nextSpeed, delta * 24);
            float moving = Mathf.Clamp01(speed / .35f);
            float running = Mathf.Clamp01((speed - 2.2f) / 1.6f);
            locomotion.SetInputWeight(0, 1 - moving);
            locomotion.SetInputWeight(1, moving * (1 - running));
            locomotion.SetInputWeight(2, moving * running);
            walk.SetSpeed(PlayerAppearance.CyclesPerSecond(false, speed));
            run.SetSpeed(PlayerAppearance.CyclesPerSecond(true, speed));
            holdWeight = Mathf.MoveTowards(holdWeight, carrying ? 1 : 0, delta * 8);
            layers.SetInputWeight(1, holdWeight);
            holding.SetInputWeight(0, twoHanded ? 0 : 1);
            holding.SetInputWeight(1, twoHanded ? 1 : 0);
            if (!active) StopMelee();
            else if (attackStart != 0)
            {
                if (actionRunning) interpolation = Math.Min(.1, interpolation + delta);
                double age = Math.Min(cycleTicks, Math.Max(presentedAge, confirmedAge + interpolation * ModeDefinition.TickRate));
                presentedAge = age;
                float endFrame = thrusting ? 27 : 33;
                float frame = age < windupTicks && windupTicks > 0 ? (float)(age / windupTicks) * 7 :
                    age < windupTicks + activeTicks ? 7 + (float)((age - windupTicks) / activeTicks) * 5 :
                    12 + (float)((age - windupTicks - activeTicks) / Math.Max(1, cycleTicks - windupTicks - activeTicks)) * (endFrame - 12);
                var clip = thrusting ? thrust : swing;
                clip.SetTime(clip.GetAnimationClip().length * Mathf.Clamp01(frame / endFrame));
                melee.SetInputWeight(0, thrusting ? 0 : 1);
                melee.SetInputWeight(1, thrusting ? 1 : 0);
                float fadeTicks = Math.Max(1, Math.Min(4, cycleTicks * .15f));
                layers.SetInputWeight(2, Mathf.Clamp01((float)Math.Min(age, cycleTicks - age) / fadeTicks));
            }
            if (active) graph.Evaluate(delta);
        }

        static AvatarMask MakeUpperBodyMask(Transform animator)
        {
            var bones = animator.GetComponentsInChildren<Transform>(true);
            var mask = new AvatarMask { name = "Wordbound upper body", transformCount = bones.Length };
            for (int i = 0; i < bones.Length; i++)
            {
                string path = "";
                bool upper = false;
                for (var at = bones[i]; at && at != animator; at = at.parent)
                {
                    path = path.Length == 0 ? at.name : at.name + "/" + path;
                    if (at.name == "spine") upper = true;
                }
                mask.SetTransformPath(i, path);
                mask.SetTransformActive(i, upper);
            }
            return mask;
        }

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
            if (upperBody)
            {
                if (Application.isPlaying) Object.Destroy(upperBody); else Object.DestroyImmediate(upperBody);
                upperBody = null;
            }
        }
    }
}
