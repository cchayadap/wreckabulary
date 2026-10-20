using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Wreckabulary.Art;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online
{
    public sealed class WordboundAvatarMotion : IDisposable
    {
        PlayableGraph graph;
        AnimationMixerPlayable locomotion, holding;
        AnimationLayerMixerPlayable layers;
        AnimationClipPlayable walk, run;
        AvatarMask upperBody;
        readonly Transform root;
        Vector3 previousPosition;
        bool sampled;
        float speed, holdWeight;

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
                layers = AnimationLayerMixerPlayable.Create(graph, 2);
                Connect(library, "Idle", locomotion, 0);
                walk = Connect(library, "Walk_InPlace", locomotion, 1);
                run = Connect(library, "Run_InPlace", locomotion, 2);
                Connect(library, "Hold_OneHand", holding, 0);
                Connect(library, "Carry_TwoHand", holding, 1);
                upperBody = MakeUpperBodyMask(animator.transform);
                graph.Connect(locomotion, 0, layers, 0);
                graph.Connect(holding, 0, layers, 1);
                layers.SetLayerMaskFromAvatarMask(1, upperBody);
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

        public void Step(float deltaTime, bool active, bool carrying, bool twoHanded)
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
