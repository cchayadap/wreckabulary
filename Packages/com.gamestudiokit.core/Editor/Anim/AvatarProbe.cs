// Game Studio Kit (MIT). Exec entry point GameStudioKit.Editor.Anim.AvatarProbe.Run (CONTRACTS §7.9): checks one
// imported model's avatar (valid, Humanoid), its rest pose (upper arms within the T-pose tolerance of horizontal),
// its foot bone height above the sole, and lists its clips.
// The avatar is the one the model really uses: its own Avatar sub-asset, else the importer's source avatar (a clips
// model set to Copy From Other Avatar has no avatar of its own), else the prefab Animator's avatar. The rest pose is the
// avatar's skeleton (the SkeletonBone rest transforms that retargeting and Enforce T-Pose use) applied to the model's
// hierarchy, not the file's bind pose: an A-pose FBX that copies a T-posed reference avatar is in a T-pose for Unity.
// Args: {"asset": "Assets/…/Model.fbx", "expect": "humanoid"|"generic", "tpose_tolerance_deg": 15,
//        "left_foot": "…", "right_foot": "…" (Generic rigs), "out_dir": "Docs/qa/runs/<run_id>" (also writes avatar_<asset>.json)}
// Result: {schema:1, asset, avatar_source, avatar_asset, isValid, isHuman,
//          rest_pose:{source, upper_arm_deg:{left, right}, bind_pose_upper_arm_deg:{left, right}, skeleton_bones_applied},
//          foot_height_m, clips:[{name, length_s, loop, root_motion}], verdict, failures[]}
using System;
using System.Collections.Generic;
using System.IO;
using GameStudioKit.Common;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameStudioKit.Editor.Anim
{
    public static class AvatarProbe
    {
        public static void Run()
        {
            var args = KitExec.Args();
            string asset = KitJson.GetString(args, "asset");
            if (string.IsNullOrEmpty(asset)) throw new ArgumentException("AvatarProbe needs {\"asset\": \"Assets/…\"}");
            var result = Probe(asset, KitJson.GetString(args, "expect"), KitJson.GetDouble(args, "tpose_tolerance_deg", 15),
                KitJson.GetString(args, "left_foot"), KitJson.GetString(args, "right_foot"));
            string outDir = KitExec.ResolvePath(KitJson.GetString(args, "out_dir"));
            if (outDir != null)
            {
                string file = Path.Combine(outDir, "avatar_" + KitIo.SafeFileName(Path.GetFileNameWithoutExtension(asset)) + ".json");
                KitIo.WriteJsonAtomic(file, result);
            }
            KitExec.Result(result);
        }

        /// <summary>
        /// The avatar a model uses and where it came from: "sub_asset", "importer_source_avatar" (Copy From Other
        /// Avatar), "animator" (the prefab's Animator), or "none".
        /// </summary>
        public static Avatar EffectiveAvatar(string assetPath, GameObject prefab, Object[] subAssets, out string source)
        {
            if (subAssets != null)
            {
                foreach (var o in subAssets)
                {
                    if (o is Avatar a)
                    {
                        source = "sub_asset";
                        return a;
                    }
                }
            }
            if (AssetImporter.GetAtPath(assetPath) is ModelImporter mi && mi.sourceAvatar != null)
            {
                source = "importer_source_avatar";
                return mi.sourceAvatar;
            }
            var animator = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;
            if (animator != null && animator.avatar != null)
            {
                source = "animator";
                return animator.avatar;
            }
            source = "none";
            return null;
        }

        public static Dictionary<string, object> Probe(string assetPath, string expect, double toleranceDeg, string leftFootName = null, string rightFootName = null)
        {
            var failures = new List<object>();
            var subs = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            var clips = new List<object>();
            foreach (var o in subs)
            {
                if (o is AnimationClip c && !c.name.StartsWith("__preview__", StringComparison.Ordinal))
                {
                    clips.Add(new Dictionary<string, object>
                    {
                        { "name", c.name },
                        { "length_s", (double)c.length },
                        { "loop", c.isLooping },
                        { "root_motion", c.hasMotionCurves || c.hasRootCurves },
                    });
                }
            }
            var avatar = EffectiveAvatar(assetPath, prefab, subs, out string avatarSource);
            string avatarAsset = avatar != null ? AssetDatabase.GetAssetPath(avatar) : null;
            if (prefab == null) failures.Add("no model at " + assetPath);
            bool isValid = avatar != null && avatar.isValid;
            bool isHuman = avatar != null && avatar.isHuman;
            if (avatar == null) failures.Add("the model has no avatar: none of its own, no source avatar (Copy From Other Avatar) and none on its Animator");
            else if (!isValid) failures.Add("the avatar is not valid (check the bone mapping)");
            if (expect == "humanoid" && !isHuman) failures.Add("expected a Humanoid avatar");
            if (expect == "generic" && isHuman) failures.Add("expected a Generic avatar");

            float bindLeft = float.NaN, bindRight = float.NaN, restLeft = float.NaN, restRight = float.NaN;
            int applied = 0;
            string restSource = "none";
            object footHeight = null;
            if (prefab != null)
            {
                var go = (GameObject)Object.Instantiate(prefab);
                go.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    var desc = avatar != null ? avatar.humanDescription : default;
                    Transform lf = isHuman ? RestPose.HumanBone(go.transform, desc, "LeftFoot") : null;
                    Transform rf = isHuman ? RestPose.HumanBone(go.transform, desc, "RightFoot") : null;
                    if (lf == null) lf = RestPose.Find(go.transform, leftFootName) ?? RestPose.FirstOf(go.transform, "LeftFoot", "foot.L", "Foot_L", "foot_l", "LeftAnkle");
                    if (rf == null) rf = RestPose.Find(go.transform, rightFootName) ?? RestPose.FirstOf(go.transform, "RightFoot", "foot.R", "Foot_R", "foot_r", "RightAnkle");
                    if (lf != null && rf != null && RestPose.LowestPoint(go.transform, out float lowest))
                        footHeight = Math.Round(0.5 * (lf.position.y + rf.position.y) - lowest, 4);

                    if (isHuman)
                    {
                        RestPose.UpperArmAngles(go.transform, desc, out bindLeft, out bindRight);
                        restSource = "instance_bind_pose";
                        string sourceModelName = string.IsNullOrEmpty(avatarAsset) ? null : Path.GetFileNameWithoutExtension(avatarAsset);
                        applied = RestPose.ApplySkeleton(go.transform, desc.skeleton, prefab.name, sourceModelName);
                        if (applied > 0)
                        {
                            RestPose.UpperArmAngles(go.transform, desc, out restLeft, out restRight);
                            if (!float.IsNaN(restLeft) && !float.IsNaN(restRight)) restSource = "avatar_skeleton";
                        }
                    }
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }
            float left = restSource == "avatar_skeleton" ? restLeft : bindLeft;
            float right = restSource == "avatar_skeleton" ? restRight : bindRight;
            if (isHuman)
            {
                if (float.IsNaN(left) || float.IsNaN(right)) failures.Add("cannot find both upper arms to check the rest pose");
                else if (Math.Max(left, right) > toleranceDeg)
                    failures.Add("rest pose (" + restSource + ") is not a T-pose: upper arms " + Math.Round(Math.Max(left, right), 1) + "° from horizontal (limit " + toleranceDeg + "°)");
            }

            return new Dictionary<string, object>
            {
                { "schema", 1 },
                { "asset", assetPath },
                { "avatar_source", avatarSource },
                { "avatar_asset", avatarAsset },
                { "isValid", isValid },
                { "isHuman", isHuman },
                {
                    "rest_pose", new Dictionary<string, object>
                    {
                        { "source", restSource },
                        { "upper_arm_deg", new Dictionary<string, object> { { "left", Angle(left) }, { "right", Angle(right) } } },
                        { "bind_pose_upper_arm_deg", new Dictionary<string, object> { { "left", Angle(bindLeft) }, { "right", Angle(bindRight) } } },
                        { "skeleton_bones_applied", applied },
                    }
                },
                { "foot_height_m", footHeight },
                { "clips", clips },
                { "verdict", failures.Count == 0 ? "PASS" : "FAIL" },
                { "failures", failures },
            };
        }

        static object Angle(float v) => float.IsNaN(v) ? null : (object)Math.Round(v, 2);
    }
}
