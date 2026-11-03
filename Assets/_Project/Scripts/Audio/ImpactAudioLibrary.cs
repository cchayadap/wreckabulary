using System;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary.Audio
{
    [CreateAssetMenu(menuName = "Wreckabulary/Audio/Impact Audio Library", fileName = "ImpactAudioLibrary")]
    public sealed class ImpactAudioLibrary : ScriptableObject
    {
        public const string ResourcePath = "Audio/ImpactAudioLibrary";

        public List<WeaponImpactSounds> weapons = new List<WeaponImpactSounds>();
        public List<SurfaceDamageSounds> surfaces = new List<SurfaceDamageSounds>();

        [Tooltip("Off: material sounds play only in scenes with an Impact Audio Scope, such as the BELT range. On: also in every House mode, replacing the generic swing, hit and break cues there.")]
        public bool useInHouseModes;

        [Header("Mix")]
        [Range(0f, 1f)] public float volume = 0.8f;
        [Min(1)] public int voices = 16;
        [Min(1)] public int voicesPerSound = 3;
        [Min(0.1f)] public float minDistance = 1.5f;
        [Min(1f)] public float maxDistance = 28f;
        [Range(0f, 1f)] public float spatialBlend = 1f;

        [Header("Occlusion")]
        public LayerMask occluders = 1;
        [Range(0f, 1f)] public float occludedVolume = 0.45f;
        [Range(500f, 22000f)] public float occludedCutoff = 1800f;

        Dictionary<string, ImpactSound> index;

        void OnEnable() => index = null;
        void OnValidate() => index = null;

        public bool Has(string key) => Find(key) != null;

        public ImpactSound Find(string key)
        {
            if (index == null) Rebuild();
            return key != null && index.TryGetValue(key, out var sound) ? sound : null;
        }

        public IEnumerable<string> Keys
        {
            get
            {
                if (index == null) Rebuild();
                return index.Keys;
            }
        }

        public void Rebuild()
        {
            index = new Dictionary<string, ImpactSound>(StringComparer.Ordinal);
            foreach (var set in weapons)
            {
                if (!set) continue;
                if (set.swing != null && set.swing.HasClips) index[ImpactCues.SwingKey(set.weapon)] = set.swing;
                foreach (var contact in set.contacts)
                    if (contact?.sound != null && contact.sound.HasClips)
                        index[ImpactCues.ContactKey(set.weapon, contact.part.ToString(), contact.surface)] = contact.sound;
            }
            foreach (var set in surfaces)
            {
                if (!set) continue;
                if (set.damaged != null && set.damaged.HasClips) index[ImpactCues.DamageKey(set.surface)] = set.damaged;
                if (set.broken != null && set.broken.HasClips) index[ImpactCues.BreakKey(set.surface)] = set.broken;
            }
        }

        public List<string> Validate()
        {
            var problems = new List<string>();
            var weaponIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var set in weapons)
            {
                if (!set) { problems.Add("A weapon entry is empty."); continue; }
                if (!weaponIds.Add(set.weapon)) problems.Add($"{set.weapon} is listed twice.");
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var contact in set.contacts)
                {
                    string key = ImpactCues.ContactKey(set.weapon, contact.part.ToString(), contact.surface);
                    if (!seen.Add(key)) problems.Add($"{key} is listed twice.");
                    Check(problems, key, contact.sound);
                }
                Check(problems, ImpactCues.SwingKey(set.weapon), set.swing);
            }
            if (!weaponIds.Contains(ImpactCues.DefaultWeapon)) problems.Add("Add a Default weapon so every weapon has contact sounds.");
            var surfaceIds = new HashSet<SurfaceMaterial>();
            foreach (var set in surfaces)
            {
                if (!set) { problems.Add("A surface entry is empty."); continue; }
                if (!surfaceIds.Add(set.surface)) problems.Add($"{set.surface} damage sounds are listed twice.");
                Check(problems, ImpactCues.DamageKey(set.surface), set.damaged);
                Check(problems, ImpactCues.BreakKey(set.surface), set.broken);
            }
            if (voicesPerSound > voices) problems.Add("Voices per sound cannot exceed total voices.");
            if (maxDistance <= minDistance) problems.Add("Max distance must be greater than min distance.");
            return problems;
        }

        static void Check(List<string> problems, string key, ImpactSound sound)
        {
            if (sound == null) return;
            if (sound.pitchMax < sound.pitchMin) problems.Add($"{key}: pitch max is below pitch min.");
            if (sound.clips != null)
                for (int i = 0; i < sound.clips.Length; i++)
                    if (!sound.clips[i]) problems.Add($"{key}: variant {i + 1} has no clip.");
        }
    }
}
