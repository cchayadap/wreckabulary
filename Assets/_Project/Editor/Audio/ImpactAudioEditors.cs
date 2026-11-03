using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Audio;
using Wreckabulary.Rules;

namespace Wreckabulary.EditorTools
{
    static class ImpactAudition
    {
        static readonly Type AudioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        static int next;

        public static bool Available => Method("PlayPreviewClip") != null;

        public static void Play(ImpactSound sound)
        {
            if (sound == null || !sound.HasClips) return;
            AudioClip clip = null;
            for (int i = 0; i < sound.clips.Length && !clip; i++) clip = sound.clips[(next + i) % sound.clips.Length];
            next++;
            if (!clip) return;
            Method("StopAllPreviewClips")?.Invoke(null, null);
            var play = Method("PlayPreviewClip");
            if (play == null) return;
            var args = play.GetParameters().Length == 3 ? new object[] { clip, 0, false } : new object[] { clip };
            play.Invoke(null, args);
        }

        static MethodInfo Method(string name) => AudioUtil?.GetMethod(name, BindingFlags.Static | BindingFlags.Public);

        public static void Row(string label, ImpactSound sound)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, sound != null && sound.HasClips ? $"{sound.Count} variant(s)" : "no clips");
                using (new EditorGUI.DisabledScope(sound == null || !sound.HasClips || !Available))
                    if (GUILayout.Button("Play", GUILayout.Width(52))) Play(sound);
            }
        }
    }

    [CustomEditor(typeof(WeaponImpactSounds))]
    public sealed class WeaponImpactSoundsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Swing plays at the start of every attack. Contacts play only after a confirmed hit; break sounds belong to the struck surface.", MessageType.None);
            DrawDefaultInspector();
            var set = (WeaponImpactSounds)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Audition", EditorStyles.boldLabel);
            ImpactAudition.Row("Swing", set.swing);
            foreach (var contact in set.contacts) ImpactAudition.Row($"{contact.part} on {contact.surface}", contact.sound);
        }
    }

    [CustomEditor(typeof(SurfaceDamageSounds))]
    public sealed class SurfaceDamageSoundsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Damaged plays when a hit cracks or dents without breaking. Broken plays only for a confirmed break.", MessageType.None);
            DrawDefaultInspector();
            var set = (SurfaceDamageSounds)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Audition", EditorStyles.boldLabel);
            ImpactAudition.Row("Damaged", set.damaged);
            ImpactAudition.Row("Broken", set.broken);
        }
    }

    [CustomEditor(typeof(ImpactAudioLibrary))]
    public sealed class ImpactAudioLibraryEditor : Editor
    {
        string weapon = "BELT";
        StrikePart part = StrikePart.Buckle;
        SurfaceMaterial surface = SurfaceMaterial.Glass;
        ImpactResult result = ImpactResult.Hit;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var library = (ImpactAudioLibrary)target;
            library.Rebuild();
            var problems = library.Validate();
            EditorGUILayout.Space();
            if (problems.Count == 0) EditorGUILayout.HelpBox("Every listed sound is valid.", MessageType.Info);
            else EditorGUILayout.HelpBox(string.Join("\n", problems), MessageType.Warning);

            EditorGUILayout.LabelField("Try a hit", EditorStyles.boldLabel);
            weapon = EditorGUILayout.TextField("Weapon", weapon).Trim().ToUpperInvariant();
            part = (StrikePart)EditorGUILayout.EnumPopup("Part", part);
            surface = (SurfaceMaterial)EditorGUILayout.EnumPopup("Surface", surface);
            result = (ImpactResult)EditorGUILayout.EnumPopup("Result", result);
            var plan = ImpactCues.Plan(new ImpactEvent("audition", weapon, part, surface, result, 0.8f), library.Has);
            if (plan.Count == 0) EditorGUILayout.LabelField(result == ImpactResult.Miss ? "Miss: swing only." : "No matching sound.");
            foreach (var cue in plan) ImpactAudition.Row($"{cue.Layer}: {cue.Key}", library.Find(cue.Key));
            if (!ImpactAudition.Available) EditorGUILayout.HelpBox("Audio preview is unavailable in this editor version; use Play mode.", MessageType.None);
        }
    }
}
