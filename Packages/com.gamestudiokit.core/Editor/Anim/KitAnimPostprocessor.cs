// Game Studio Kit (MIT). Turns the Humanoid/Generic import lessons into code (ARCHITECTURE §3.8, extract_animation §7.3):
// models under a folder with kit_anim_rules.json get
//  - Humanoid or Generic, one reference avatar (role "reference": create from this model and check its T-pose; role
//    "clips": copy the reference model's avatar, so every clip shares one rest pose);
//  - animation compression off, no curve resampling, scale 1, the fps check;
//  - per-clip root rules: height from the feet for all; locomotion loops keep root motion out of the pose (the game
//    moves the body); standing loops bake root XZ, rotation and height into the pose (weight shifts stay over planted
//    feet, WoO D039); one-shots do not loop; "Armature|" style prefixes stripped; optional rotation offsets.
// Folders without a rule file are untouched. Rules apply on every import when "enforce" is true (default), otherwise
// only on the first import, so hand-tuned settings survive.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameStudioKit.Editor.Anim
{
    public sealed class KitAnimPostprocessor : AssetPostprocessor
    {
        const uint Version = 3;

        public override uint GetVersion() => Version;
        public override int GetPostprocessOrder() => 10;

        /// <summary>The nearest kit_anim_rules.json at or above the asset's folder (inside Assets/), or null.</summary>
        public static string FindRulesPath(string assetPath)
        {
            string dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            while (!string.IsNullOrEmpty(dir) && (dir == "Assets" || dir.StartsWith("Assets/", StringComparison.Ordinal)))
            {
                string candidate = dir + "/" + AnimImportRules.FileName;
                if (File.Exists(candidate)) return candidate;
                int slash = dir.LastIndexOf('/');
                if (slash < 0) break;
                dir = dir.Substring(0, slash);
            }
            return null;
        }

        AnimImportRules LoadRules(bool registerDependency)
        {
            string path = FindRulesPath(assetPath);
            if (path == null) return null;
            if (registerDependency) context.DependsOnSourceAsset(path);
            try
            {
                var rules = AnimImportRules.Parse(File.ReadAllText(path));
                foreach (var w in rules.Warnings) context.LogImportWarning(path + ": " + w);
                return rules;
            }
            catch (Exception e)
            {
                context.LogImportError(path + ": " + e.Message);
                return null;
            }
        }

        bool ShouldApply(AnimImportRules rules) => rules != null && (rules.Enforce || assetImporter.importSettingsMissing);

        void OnPreprocessModel()
        {
            var rules = LoadRules(true);
            if (!ShouldApply(rules)) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = (float)rules.GlobalScale;
            mi.animationCompression = ModelImporterAnimationCompression.Off;
            mi.resampleCurves = false;
            if (rules.Role == "model") return;
            mi.importAnimation = true;
            mi.animationType = rules.Humanoid ? ModelImporterAnimationType.Human : ModelImporterAnimationType.Generic;
            if (!rules.Humanoid && !string.IsNullOrEmpty(rules.RootNode)) mi.motionNodeName = rules.RootNode;

            if (rules.IsReference || string.IsNullOrEmpty(rules.ReferenceModel))
            {
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                return;
            }
            if (string.Equals(rules.ReferenceModel, assetPath, StringComparison.OrdinalIgnoreCase))
            {
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                return;
            }
            context.DependsOnArtifact(rules.ReferenceModel);
            var avatar = LoadAvatar(rules.ReferenceModel);
            if (avatar == null)
            {
                context.LogImportWarning(assetPath + ": reference model " + rules.ReferenceModel + " has no avatar yet; creating one from this model until it imports");
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                return;
            }
            mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            mi.sourceAvatar = avatar;
        }

        static Avatar LoadAvatar(string modelPath)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                if (o is Avatar a) return a;
            return null;
        }

        void OnPreprocessAnimation()
        {
            var rules = LoadRules(false);
            if (!ShouldApply(rules) || rules.Role == "model") return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.clipAnimations;
            if (clips == null || clips.Length == 0) clips = mi.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            foreach (var clip in clips)
            {
                clip.name = rules.CleanName(clip.name);
                var cls = rules.ClassFor(clip.name);
                clip.heightFromFeet = true;
                clip.keepOriginalPositionY = false;
                switch (cls)
                {
                    case ClipClass.Locomotion:
                        clip.loopTime = true;
                        clip.loopPose = true;
                        clip.lockRootRotation = false;
                        clip.lockRootPositionXZ = false;
                        clip.lockRootHeightY = false;
                        break;
                    case ClipClass.Standing:
                        clip.loopTime = true;
                        clip.loopPose = true;
                        clip.lockRootRotation = true;
                        clip.lockRootPositionXZ = true;
                        clip.lockRootHeightY = true;
                        break;
                    default:
                        clip.loopTime = false;
                        clip.loopPose = false;
                        break;
                }
                double offset = rules.RotationOffsetFor(clip.name);
                if (Math.Abs(offset) > 1e-6) clip.rotationOffset = (float)offset;
            }
            mi.clipAnimations = clips;
        }

        void OnPostprocessModel(GameObject root)
        {
            var rules = LoadRules(false);
            if (rules == null || rules.Role == "model") return;
            var mi = (ModelImporter)assetImporter;

            // A 100× transform is the classic FBX unit mistake; flag it at import time.
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var s = t.localScale;
                if (Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)) >= 50f)
                {
                    context.LogImportWarning(assetPath + ": " + t.name + " has scale " + s + " (FBX units?)");
                    break;
                }
            }

            if (!rules.Humanoid || !rules.IsReference || !rules.RequireTPose) return;
            RestPose.UpperArmAngles(root.transform, mi.humanDescription, out float left, out float right);
            float worst = Mathf.Max(float.IsNaN(left) ? 0f : left, float.IsNaN(right) ? 0f : right);
            if (float.IsNaN(left) && float.IsNaN(right))
                context.LogImportWarning(assetPath + ": cannot find the upper arms to check the T-pose; run AvatarProbe");
            else if (worst > rules.TPoseToleranceDeg)
                context.LogImportError(assetPath + ": the reference model is not in a T-pose (upper arms " + worst.ToString("0.0") +
                                       "° from horizontal, limit " + rules.TPoseToleranceDeg + "°). Enforce the T-pose on the avatar before any clip copies it.");
        }

        void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            var rules = LoadRules(false);
            if (rules == null || rules.Role == "model" || rules.Fps <= 0) return;
            if (Mathf.Abs(clip.frameRate - (float)rules.Fps) > 0.01f)
                context.LogImportWarning(assetPath + ": clip " + clip.name + " is " + clip.frameRate + " fps; the rule is " + rules.Fps + " fps");
        }
    }
}
