using System;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary.Audio
{
    public enum ContactPart { Any, Body, Strap, Buckle }

    [Serializable]
    public sealed class ImpactSound
    {
        [Tooltip("Variants; one is picked per event without repeating the previous one.")]
        public AudioClip[] clips = Array.Empty<AudioClip>();
        [Range(0f, 1.5f)] public float volume = 1f;
        [Range(0.5f, 1.5f)] public float pitchMin = 0.96f;
        [Range(0.5f, 1.5f)] public float pitchMax = 1.04f;
        [Tooltip("Seconds before the same source may play this sound on the same target again.")]
        [Min(0f)] public float cooldown = 0.06f;

        public bool HasClips
        {
            get
            {
                if (clips == null) return false;
                foreach (var clip in clips) if (clip) return true;
                return false;
            }
        }

        public int Count => clips?.Length ?? 0;
    }

    [Serializable]
    public sealed class ContactSound
    {
        public ContactPart part = ContactPart.Any;
        public SurfaceMaterial surface = SurfaceMaterial.Wood;
        public ImpactSound sound = new ImpactSound();
    }
}
