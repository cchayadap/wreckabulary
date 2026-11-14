// Game Studio Kit (MIT). Finds the bones the embodiment probes read: Humanoid through the avatar, Generic rigs through
// explicit names or a name heuristic ("mixamorig:LeftFoot", "foot.L", "Foot_L", "LeftAnkle"…).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameStudioKit.Qa.Probes
{
    public sealed class BodyBones
    {
        public Transform Root;
        public Transform Hips;
        public Transform Head;
        public Transform LeftFoot;
        public Transform RightFoot;
        public Transform LeftHand;
        public Transform RightHand;
        public bool IsHumanoid;

        public bool HasFeet => LeftFoot != null && RightFoot != null;
        public bool HasSpine => Hips != null && Head != null;

        /// <summary>Humanoid animators map through the avatar; other animators fall back to <see cref="Guess"/>.</summary>
        public static BodyBones FromAnimator(Animator animator)
        {
            if (animator == null) return null;
            if (animator.isHuman && animator.avatar != null)
            {
                return new BodyBones
                {
                    Root = animator.transform,
                    Hips = animator.GetBoneTransform(HumanBodyBones.Hips),
                    Head = animator.GetBoneTransform(HumanBodyBones.Head),
                    LeftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot),
                    RightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot),
                    LeftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand),
                    RightHand = animator.GetBoneTransform(HumanBodyBones.RightHand),
                    IsHumanoid = true,
                };
            }
            return Guess(animator.transform);
        }

        /// <summary>Explicit bone names (exact, or the part after a ':' or '|' namespace prefix).</summary>
        public static BodyBones FromNames(Transform root, string hips, string head, string leftFoot, string rightFoot,
            string leftHand = null, string rightHand = null)
        {
            return new BodyBones
            {
                Root = root,
                Hips = FindDeep(root, hips),
                Head = FindDeep(root, head),
                LeftFoot = FindDeep(root, leftFoot),
                RightFoot = FindDeep(root, rightFoot),
                LeftHand = FindDeep(root, leftHand),
                RightHand = FindDeep(root, rightHand),
            };
        }

        /// <summary>Name heuristic for Generic rigs. Check HasFeet before trusting it.</summary>
        public static BodyBones Guess(Transform root)
        {
            var b = new BodyBones { Root = root };
            if (root == null) return b;
            var all = root.GetComponentsInChildren<Transform>(true);
            b.Hips = FirstMatch(all, 0, "hips", "pelvis", "hip");
            b.Head = FirstMatch(all, 0, "head");
            b.LeftFoot = FirstMatch(all, -1, "foot", "ankle");
            b.RightFoot = FirstMatch(all, 1, "foot", "ankle");
            b.LeftHand = FirstMatch(all, -1, "hand", "wrist");
            b.RightHand = FirstMatch(all, 1, "hand", "wrist");
            return b;
        }

        public static Transform FindDeep(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            Transform suffixHit = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
                if (suffixHit == null && StripNamespace(t.name) == name) suffixHit = t;
            }
            return suffixHit;
        }

        static string StripNamespace(string n)
        {
            int i = Math.Max(n.LastIndexOf(':'), n.LastIndexOf('|'));
            return i >= 0 ? n.Substring(i + 1) : n;
        }

        /// <summary>First transform whose name contains a keyword and the side marker (-1 left, 1 right, 0 none).
        /// Toe, finger and twist bones are skipped.</summary>
        static Transform FirstMatch(IList<Transform> all, int side, params string[] keywords)
        {
            foreach (var kw in keywords)
            {
                foreach (var t in all)
                {
                    string n = StripNamespace(t.name).ToLowerInvariant();
                    if (!n.Contains(kw)) continue;
                    if (n.Contains("toe") || n.Contains("finger") || n.Contains("thumb") || n.Contains("twist") || n.Contains("end") || n.Contains("ik")) continue;
                    if (side == 0 || SideOf(n) == side) return t;
                }
            }
            return null;
        }

        /// <summary>-1 left, 1 right, 0 unknown, from common naming schemes (Left*, *_L, *.l, l_*, *Left).</summary>
        public static int SideOf(string lowerName)
        {
            string n = lowerName;
            if (n.StartsWith("left") || n.Contains("left") || n.EndsWith("_l") || n.EndsWith(".l") || n.EndsWith(" l") || n.StartsWith("l_") || n.StartsWith("l.") || n.EndsWith("-l")) return -1;
            if (n.StartsWith("right") || n.Contains("right") || n.EndsWith("_r") || n.EndsWith(".r") || n.EndsWith(" r") || n.StartsWith("r_") || n.StartsWith("r.") || n.EndsWith("-r")) return 1;
            return 0;
        }

        /// <summary>Ground height under <paramref name="point"/>, ignoring colliders under <paramref name="self"/>;
        /// <paramref name="fallback"/> when nothing is hit.</summary>
        public static float GroundHeight(Vector3 point, Transform self, LayerMask mask, float fallback, RaycastHit[] buffer, float up = 0.5f, float down = 3f)
        {
            var origin = point + Vector3.up * up;
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, buffer, up + down, mask, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            float bestDist = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                var h = buffer[i];
                if (self != null && h.collider.transform.IsChildOf(self)) continue;
                if (h.distance < bestDist)
                {
                    bestDist = h.distance;
                    best = h.point.y;
                }
            }
            return float.IsNegativeInfinity(best) ? fallback : best;
        }

        /// <summary>Planar (XZ) distance.</summary>
        public static float Planar(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
