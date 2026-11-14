using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Wreckabulary.Online.Editor
{
    public static class WordboundHoldingPoseSetup
    {
        [MenuItem("Tools/Wordbound/Save missing held-item pose settings")]
        public static void SaveMissingDefaults()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Save held-item pose settings in Edit Mode.");
            string path = WordboundAuthoringSetup.VisualPath;
            var profile = AssetDatabase.LoadAssetAtPath<WordboundVisualProfile>(path);
            if (!profile || !File.Exists(path)) throw new InvalidOperationException("Create the existing Inspector authoring assets first.");
            if (HasSavedField(path))
            {
                profile.Validate();
                Debug.Log("[Wreckabulary] Saved held-item pose settings already exist; custom values and asset bytes are unchanged.");
                return;
            }
            if (EditorUtility.IsDirty(profile))
                throw new InvalidOperationException("Save or discard your pending Presentation asset edits before adding the missing held-item settings.");
            Undo.RecordObject(profile, "Save held-item pose settings");
            if (profile.HoldingPose == null) profile.HoldingPose = new WordboundHoldingPose();
            profile.Validate();
            string expected = EditorJsonUtility.ToJson(profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            if (!HasSavedField(path) || EditorUtility.IsDirty(profile) || EditorJsonUtility.ToJson(profile) != expected)
                throw new InvalidOperationException("Target-only held-item settings save could not be verified.");
            Debug.Log("[Wreckabulary] Saved missing held-item settings on " + path + "; existing profile values retained, target-only save.");
        }

        static bool HasSavedField(string path) => File.ReadAllText(path).Replace("\r\n", "\n")
            .Contains("\n  HoldingPose:");
    }
}
