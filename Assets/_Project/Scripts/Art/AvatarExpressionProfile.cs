using System;
using UnityEngine;

namespace Wreckabulary.Art
{
    public enum AvatarExpression { Idle, Moving, Equipped, Focus, Hurt, Downed, Falling, Recover, Celebrate }

    [Serializable]
    public struct AvatarExpressionPose
    {
        public AvatarExpression State;
        [Range(0, 100)] public float BrowRelax;
        [Range(0, 100)] public float Blink;
        public bool NaturalBlink;
        public AvatarExpressionPose(AvatarExpression state, float brow, float blink, bool natural)
        { State = state; BrowRelax = brow; Blink = blink; NaturalBlink = natural; }
    }

    [CreateAssetMenu(menuName = "Wreckabulary/Art/Avatar Expressions", fileName = "AvatarExpressions")]
    public sealed class AvatarExpressionProfile : ScriptableObject
    {
        public const string ResourcePath = "Art/AvatarExpressions";
        [Tooltip("Original mouthless avatar: only Blink and BrowRelax are driven. Higher BrowRelax softens the brows; this does not add smile topology.")]
        public AvatarExpressionPose[] Poses = {
            Default(AvatarExpression.Idle), Default(AvatarExpression.Moving), Default(AvatarExpression.Equipped),
            Default(AvatarExpression.Focus), Default(AvatarExpression.Hurt), Default(AvatarExpression.Downed),
            Default(AvatarExpression.Falling), Default(AvatarExpression.Recover), Default(AvatarExpression.Celebrate)
        };
        [Range(.02f, .5f)] public float BlendSeconds = .12f;
        [Range(1, 8)] public float BlinkInterval = 3f;
        [Range(.08f, .3f)] public float BlinkSeconds = .16f;
        [Range(.1f, 1)] public float HurtSeconds = .32f;
        [Range(.1f, 1.5f)] public float RecoverSeconds = .55f;
        [Range(.2f, 3)] public float CelebrateSeconds = 1.5f;

        public AvatarExpressionPose Get(AvatarExpression state)
        {
            if (Poses != null) for (int i = 0; i < Poses.Length; i++)
                if (Poses[i].State == state)
                {
                    var pose = Poses[i]; pose.BrowRelax = Bounded(pose.BrowRelax, 0, 100, 55);
                    pose.Blink = Bounded(pose.Blink, 0, 100, 0); return pose;
                }
            return Default(state);
        }

        public static AvatarExpressionPose Default(AvatarExpression state) => state switch {
            AvatarExpression.Moving => new AvatarExpressionPose(state, 42, 0, true),
            AvatarExpression.Equipped => new AvatarExpressionPose(state, 38, 0, true),
            AvatarExpression.Focus => new AvatarExpressionPose(state, 8, 0, false),
            AvatarExpression.Hurt => new AvatarExpressionPose(state, 18, 60, false),
            AvatarExpression.Downed => new AvatarExpressionPose(state, 12, 78, false),
            AvatarExpression.Falling => new AvatarExpressionPose(state, 24, 0, false),
            AvatarExpression.Recover => new AvatarExpressionPose(state, 62, 10, true),
            AvatarExpression.Celebrate => new AvatarExpressionPose(state, 85, 0, true),
            _ => new AvatarExpressionPose(AvatarExpression.Idle, 75, 0, true)
        };

        public static float Bounded(float value, float min, float max, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);

        public void Validate()
        {
            if (Poses == null || Poses.Length != 9) throw new ArgumentException("Keep one pose for each of the nine expression states.");
            int seen = 0;
            foreach (var pose in Poses)
            {
                int bit = 1 << (int)pose.State;
                if (!Enum.IsDefined(typeof(AvatarExpression), pose.State) || (seen & bit) != 0 ||
                    !FiniteRange(pose.BrowRelax, 0, 100) || !FiniteRange(pose.Blink, 0, 100))
                    throw new ArgumentException("Expression states must be unique and weights finite within 0–100.");
                seen |= bit;
            }
            if (!FiniteRange(BlendSeconds, .02f, .5f) || !FiniteRange(BlinkInterval, 1, 8) ||
                !FiniteRange(BlinkSeconds, .08f, .3f) || !FiniteRange(HurtSeconds, .1f, 1) ||
                !FiniteRange(RecoverSeconds, .1f, 1.5f) || !FiniteRange(CelebrateSeconds, .2f, 3))
                throw new ArgumentException("Expression timing must stay within the displayed Inspector ranges.");
        }

        static bool FiniteRange(float value, float min, float max) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
    }
}
