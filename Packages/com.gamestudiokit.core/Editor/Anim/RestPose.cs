// Game Studio Kit (MIT). Rest-pose measurements shared by KitAnimPostprocessor and AvatarProbe: upper-arm angle from
// horizontal (T-pose ≈ 0°, A-pose ≈ 30–45°; a non-T reference avatar is what made WoO's warrior lean back 25–30°),
// and foot bone height above the lowest point of the mesh.
using System.Collections.Generic;
using UnityEngine;

namespace GameStudioKit.Editor.Anim
{
    public static class RestPose
    {
        /// <summary>Finds a bone by exact name, or by the part after a ':' or '|' prefix.</summary>
        public static Transform Find(Transform root, string boneName)
        {
            if (root == null || string.IsNullOrEmpty(boneName)) return null;
            Transform suffix = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == boneName) return t;
                int i = Mathf.Max(t.name.LastIndexOf(':'), t.name.LastIndexOf('|'));
                if (suffix == null && i >= 0 && t.name.Substring(i + 1) == boneName) suffix = t;
            }
            return suffix;
        }

        /// <summary>Human bone name (e.g. "LeftUpperArm") → transform, through the avatar's human description.</summary>
        public static Transform HumanBone(Transform root, HumanDescription desc, string humanName)
        {
            if (desc.human == null) return null;
            foreach (var hb in desc.human)
                if (hb.humanName == humanName) return Find(root, hb.boneName);
            return null;
        }

        /// <summary>Angle in degrees of upper→lower arm from the horizontal plane; NaN when a bone is missing.</summary>
        public static float ArmAngle(Transform upper, Transform lower)
        {
            if (upper == null || lower == null) return float.NaN;
            Vector3 d = lower.position - upper.position;
            if (d.sqrMagnitude < 1e-10f) return float.NaN;
            return Mathf.Asin(Mathf.Clamp(Mathf.Abs(d.normalized.y), 0f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>Left and right upper-arm angles through a human description, with common-name fallbacks.</summary>
        public static void UpperArmAngles(Transform root, HumanDescription desc, out float left, out float right)
        {
            var lu = HumanBone(root, desc, "LeftUpperArm") ?? FirstOf(root, "LeftArm", "LeftUpperArm", "upperarm_l", "UpperArm.L", "upper_arm.L");
            var ll = HumanBone(root, desc, "LeftLowerArm") ?? FirstOf(root, "LeftForeArm", "LeftLowerArm", "lowerarm_l", "LowerArm.L", "forearm.L");
            var ru = HumanBone(root, desc, "RightUpperArm") ?? FirstOf(root, "RightArm", "RightUpperArm", "upperarm_r", "UpperArm.R", "upper_arm.R");
            var rl = HumanBone(root, desc, "RightLowerArm") ?? FirstOf(root, "RightForeArm", "RightLowerArm", "lowerarm_r", "LowerArm.R", "forearm.R");
            left = ArmAngle(lu, ll);
            right = ArmAngle(ru, rl);
        }

        /// <summary>
        /// Poses <paramref name="root"/>'s hierarchy in an avatar's rest pose: every SkeletonBone whose name matches a
        /// transform below the root gets its local position, rotation and scale. The skeleton's root entry (named like
        /// the model or the avatar's source model) sets only the root's rotation and scale, so the model stays at the
        /// origin. Returns the number of bones applied (0 when nothing matched).
        /// </summary>
        public static int ApplySkeleton(Transform root, SkeletonBone[] skeleton, string modelName = null, string sourceModelName = null)
        {
            if (root == null || skeleton == null || skeleton.Length == 0) return 0;
            var byName = new Dictionary<string, Transform>(System.StringComparer.Ordinal);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root) continue;
                if (!byName.ContainsKey(t.name)) byName[t.name] = t;
            }
            int applied = 0;
            for (int i = 0; i < skeleton.Length; i++)
            {
                var bone = skeleton[i];
                if (string.IsNullOrEmpty(bone.name)) continue;
                bool isRootEntry = i == 0 && !byName.ContainsKey(bone.name)
                                   && (bone.name == modelName || bone.name == sourceModelName || bone.name == root.name || root.name == bone.name + "(Clone)");
                if (isRootEntry)
                {
                    root.localRotation = bone.rotation;
                    root.localScale = bone.scale;
                    continue;
                }
                if (!byName.TryGetValue(bone.name, out var t)) continue;
                t.localPosition = bone.position;
                t.localRotation = bone.rotation;
                t.localScale = bone.scale;
                applied++;
            }
            return applied;
        }

        public static Transform FirstOf(Transform root, params string[] names)
        {
            foreach (var n in names)
            {
                var t = Find(root, n);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>Lowest point of every renderer under <paramref name="root"/> (baked skinned meshes; bounds for
        /// others). Returns false when there is no renderer.</summary>
        public static bool LowestPoint(Transform root, out float y)
        {
            y = float.PositiveInfinity;
            var verts = new List<Vector3>(4096);
            Mesh baked = null;
            try
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                    {
                        if (baked == null) baked = new Mesh();
                        smr.BakeMesh(baked, true);
                        baked.GetVertices(verts);
                        var t = smr.transform;
                        foreach (var v in verts) y = Mathf.Min(y, (t.position + t.rotation * v).y);
                    }
                    else y = Mathf.Min(y, r.bounds.min.y);
                }
            }
            finally
            {
                if (baked != null) Object.DestroyImmediate(baked);
            }
            return !float.IsPositiveInfinity(y);
        }
    }
}
