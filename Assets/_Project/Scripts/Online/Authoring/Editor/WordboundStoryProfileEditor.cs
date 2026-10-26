using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online.Editor
{
    [CustomEditor(typeof(WordboundStoryProfile))]
    public sealed class WordboundStoryProfileEditor : UnityEditor.Editor
    {
        WordboundFlagshipContent content;
        string error = "";
        int preview;
        WordboundStoryProfile Profile => (WordboundStoryProfile)target;

        void OnEnable()
        {
            try { content = LoadContent(); error = ""; }
            catch (Exception exception) { error = exception.Message; }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Presentation only. Clue IDs, route triggers, team progress and match results stay with the course.", MessageType.Info);
            if (content == null) { EditorGUILayout.HelpBox(error, MessageType.Error); return; }
            using (new EditorGUI.DisabledScope(Profile.Chapters == null || Profile.Notices == null ||
                Profile.Chapters.Count != 0 || Profile.Notices.Count != 0))
            {
                if (GUILayout.Button("Populate existing Lost Address copy"))
                {
                    Undo.RecordObject(Profile, "Populate story profile");
                    Profile.PopulateDefaults(content);
                    EditorUtility.SetDirty(Profile);
                    serializedObject.Update();
                }
            }
            try { Profile.Validate(content); error = ""; }
            catch (Exception exception) { error = exception.Message; }
            if (error.Length != 0) { EditorGUILayout.HelpBox(error, MessageType.Error); return; }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Copy preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Preview includes all canonical clues, including optional route branches. It does not award or simulate progress.", MessageType.None);
            var ids = content.Story.Select(b => b.Lap + ":" + b.CheckpointId).ToArray();
            preview = EditorGUILayout.Popup("Clue", Mathf.Clamp(preview, 0, ids.Length - 1), ids);
            var beat = content.Story[preview];
            EditorGUILayout.LabelField(Profile.Title.Resolve(), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(Profile.Chapter(beat.EventId), EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("In-world cue", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(Profile.Cue(ids[preview]), EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Optional journal", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(Profile.Clue(ids[preview], beat.Clue), EditorStyles.wordWrappedLabel);
        }

        [MenuItem("Assets/Create/Wreckabulary/Wordbound/Story profile with defaults")]
        static void CreateDefaultProfile()
        {
            var profile = CreateInstance<WordboundStoryProfile>();
            try
            {
                profile.PopulateDefaults(LoadContent());
                ProjectWindowUtil.CreateAsset(profile, "LostAddressStory.asset");
            }
            catch { DestroyImmediate(profile); throw; }
        }

        static WordboundFlagshipContent LoadContent()
        {
            var items = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Config/items.json");
            if (!items) throw new ArgumentException("The project item catalogue is unavailable.");
            return WordboundFlagshipContent.Create(ItemCatalogue.FromJson(items.text));
        }
    }
}
