using System;
using UnityEngine;

namespace Wreckabulary.Art
{
    public sealed class AvatarExpressionDriver : IDisposable
    {
        readonly SkinnedMeshRenderer face;
        readonly Mesh boundMesh;
        readonly int blink = -1, brow = -1;
        readonly float originalBlink, originalBrow, blinkOffset;
        readonly AvatarExpressionProfile profile;
        readonly AvatarExpressionTimeline timeline = new AvatarExpressionTimeline();
        float clock, blinkWeight, browWeight;
        bool disposed;
        public bool IsSupported => !disposed && face && face.sharedMesh == boundMesh && blink >= 0 && brow >= 0;
        public AvatarExpression State => timeline.State;

        public AvatarExpressionDriver(GameObject model, AvatarExpressionProfile settings = null, int seat = 0)
        {
            profile = settings ? settings : Resources.Load<AvatarExpressionProfile>(AvatarExpressionProfile.ResourcePath);
            blinkOffset = Mathf.Abs(seat % 8) * .23f;
            if (!model) return;
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skins.Length; i++)
                if (skins[i].name == "SK_Head") { face = skins[i]; break; }
            if (!face || !face.sharedMesh) return;
            boundMesh = face.sharedMesh;
            blink = boundMesh.GetBlendShapeIndex("Blink"); brow = boundMesh.GetBlendShapeIndex("BrowRelax");
            if (!IsSupported) return;
            originalBlink = blinkWeight = face.GetBlendShapeWeight(blink);
            originalBrow = browWeight = face.GetBlendShapeWeight(brow);
        }

        public void Observe(AvatarExpressionSample sample) => timeline.Observe(sample, profile);

        public void Step(float seconds, bool advance = true)
        {
            if (!IsSupported) return;
            if (!timeline.Available) { Restore(); return; }
            float dt = advance ? AvatarExpressionProfile.Bounded(seconds, 0, 1, 0) : 0;
            var pose = profile ? profile.Get(timeline.State) : AvatarExpressionProfile.Default(timeline.State);
            float interval = AvatarExpressionProfile.Bounded(profile ? profile.BlinkInterval : 3, 1, 8, 3) + blinkOffset;
            float duration = AvatarExpressionProfile.Bounded(profile ? profile.BlinkSeconds : .16f, .08f, .3f, .16f);
            clock = (clock + dt) % (interval + duration);
            float pulse = pose.NaturalBlink && clock >= interval ? Mathf.Sin((clock - interval) / duration * Mathf.PI) * 100 : 0;
            float blend = AvatarExpressionProfile.Bounded(profile ? profile.BlendSeconds : .12f, .02f, .5f, .12f);
            float k = 1 - Mathf.Exp(-dt / blend);
            browWeight = Mathf.Lerp(browWeight, pose.BrowRelax, k);
            blinkWeight = Mathf.Lerp(blinkWeight, pose.Blink, k);
            face.SetBlendShapeWeight(brow, browWeight);
            face.SetBlendShapeWeight(blink, Mathf.Max(blinkWeight, pulse));
            timeline.Advance(dt);
        }

        void Restore()
        {
            clock = 0; blinkWeight = originalBlink; browWeight = originalBrow;
            if (!IsSupported) return;
            face.SetBlendShapeWeight(blink, originalBlink); face.SetBlendShapeWeight(brow, originalBrow);
        }

        public void Reset() { timeline.Reset(); Restore(); }
        public void Dispose() { if (disposed) return; Reset(); disposed = true; }
    }
}
