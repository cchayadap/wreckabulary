// Game Studio Kit (MIT). The folder rule file read by KitAnimPostprocessor: kit_anim_rules.json, placed in any folder
// under Assets/; it applies to models in that folder and below (the nearest file wins). Engine-free so the kit's tests
// can check the parsing and the clip classification under plain .NET.
//
// {
//   "schema": 1,
//   "rig": "humanoid" | "generic",
//   "role": "reference" | "clips" | "model",      reference = the one model whose avatar every clip copies
//   "reference_model": "Assets/Characters/Knight/Knight_Ref.fbx",   (role "clips": copy this model's avatar)
//   "root_node": "Root",                           (generic: the motion node)
//   "fps": 30,                                     (clips at another rate get an import warning)
//   "global_scale": 1,
//   "strip_prefixes": ["Armature|"],
//   "default_class": "oneshot",
//   "clips": { "Idle*": "standing", "Walk*": "locomotion", "Run*": "locomotion", "Death*": "oneshot" },
//   "rotation_offsets": { "Walk_Fwd": 3.5 },       (degrees, e.g. from GaitProbe heading error)
//   "require_t_pose": true, "t_pose_tolerance_deg": 15,
//   "enforce": true                                (re-apply on every import; false = first import only)
// }
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using GameStudioKit.Common;

namespace GameStudioKit.Editor.Anim
{
    public enum ClipClass
    {
        /// <summary>Walk, run, strafe loops: root motion is not baked into the pose (the game moves the body).</summary>
        Locomotion,
        /// <summary>Idle and other standing loops: root XZ, rotation and height are baked into the pose (D039).</summary>
        Standing,
        /// <summary>Attacks, deaths, emotes: no loop.</summary>
        OneShot,
    }

    public sealed class AnimImportRules
    {
        public const string FileName = "kit_anim_rules.json";

        public bool Humanoid = true;
        public string Role = "clips";
        public string ReferenceModel;
        public string RootNode;
        public double Fps = 30;
        public double GlobalScale = 1;
        public readonly List<string> StripPrefixes = new List<string> { "Armature|" };
        public ClipClass DefaultClass = ClipClass.OneShot;
        public readonly List<KeyValuePair<Regex, ClipClass>> ClipPatterns = new List<KeyValuePair<Regex, ClipClass>>();
        public readonly Dictionary<string, double> RotationOffsets = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        public bool RequireTPose = true;
        public double TPoseToleranceDeg = 15;
        public bool Enforce = true;
        public readonly List<string> Warnings = new List<string>();

        public bool IsReference => Role == "reference";
        public bool IsClips => Role == "clips";

        public static AnimImportRules Parse(string json)
        {
            var root = KitJson.AsObject(KitJson.Parse(json)) ?? throw new FormatException(FileName + ": the root is not an object");
            var r = new AnimImportRules();
            string rig = KitJson.GetString(root, "rig", "humanoid");
            if (rig != "humanoid" && rig != "generic") r.Warnings.Add("rig must be humanoid or generic; using humanoid");
            r.Humanoid = rig != "generic";
            r.Role = KitJson.GetString(root, "role", "clips");
            if (r.Role != "reference" && r.Role != "clips" && r.Role != "model")
            {
                r.Warnings.Add("role must be reference, clips or model; using clips");
                r.Role = "clips";
            }
            r.ReferenceModel = KitJson.GetString(root, "reference_model");
            r.RootNode = KitJson.GetString(root, "root_node");
            r.Fps = KitJson.GetDouble(root, "fps", 30);
            r.GlobalScale = KitJson.GetDouble(root, "global_scale", 1);
            if (KitJson.Get(root, "strip_prefixes") != null)
            {
                r.StripPrefixes.Clear();
                r.StripPrefixes.AddRange(KitJson.GetStringList(root, "strip_prefixes"));
            }
            if (TryClass(KitJson.GetString(root, "default_class"), out var dc)) r.DefaultClass = dc;
            var clips = KitJson.GetObject(root, "clips");
            if (clips != null)
            {
                foreach (var kv in clips)
                {
                    if (!(kv.Value is string s) || !TryClass(s, out var cls))
                    {
                        r.Warnings.Add("clips." + kv.Key + " must be locomotion, standing or oneshot");
                        continue;
                    }
                    r.ClipPatterns.Add(new KeyValuePair<Regex, ClipClass>(GlobToRegex(kv.Key), cls));
                }
            }
            var offsets = KitJson.GetObject(root, "rotation_offsets");
            if (offsets != null)
                foreach (var kv in offsets)
                    if (KitJson.IsNumber(kv.Value)) r.RotationOffsets[kv.Key] = KitJson.ToDouble(kv.Value);
            r.RequireTPose = KitJson.GetBool(root, "require_t_pose", true);
            r.TPoseToleranceDeg = KitJson.GetDouble(root, "t_pose_tolerance_deg", 15);
            r.Enforce = KitJson.GetBool(root, "enforce", true);
            if (r.IsClips && r.Humanoid && string.IsNullOrEmpty(r.ReferenceModel))
                r.Warnings.Add("humanoid clips need reference_model (the enforced T-pose avatar every clip copies)");
            return r;
        }

        static bool TryClass(string s, out ClipClass cls)
        {
            switch ((s ?? "").ToLowerInvariant())
            {
                case "locomotion": cls = ClipClass.Locomotion; return true;
                case "standing": cls = ClipClass.Standing; return true;
                case "oneshot": case "one_shot": cls = ClipClass.OneShot; return true;
                default: cls = ClipClass.OneShot; return false;
            }
        }

        /// <summary>First matching pattern in file order, else the default class.</summary>
        public ClipClass ClassFor(string clipName)
        {
            foreach (var kv in ClipPatterns) if (kv.Key.IsMatch(clipName ?? "")) return kv.Value;
            return DefaultClass;
        }

        /// <summary>Removes every configured prefix ("Armature|Walk" → "Walk").</summary>
        public string CleanName(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return clipName;
            string n = clipName;
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var p in StripPrefixes)
                {
                    if (!string.IsNullOrEmpty(p) && n.StartsWith(p, StringComparison.Ordinal) && n.Length > p.Length)
                    {
                        n = n.Substring(p.Length);
                        changed = true;
                    }
                }
            }
            return n;
        }

        public double RotationOffsetFor(string clipName) =>
            clipName != null && RotationOffsets.TryGetValue(clipName, out double d) ? d : 0;

        /// <summary>'*' and '?' globs, case-insensitive, whole name.</summary>
        public static Regex GlobToRegex(string glob)
        {
            string pattern = "^" + Regex.Escape(glob ?? "").Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
