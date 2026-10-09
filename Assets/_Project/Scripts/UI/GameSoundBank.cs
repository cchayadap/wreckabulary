using System;
using UnityEngine;

namespace Wreckabulary
{
    [CreateAssetMenu(menuName = "Wreckabulary/Sound bank")]
    public sealed class GameSoundBank : ScriptableObject
    {
        [SerializeField] AudioClip[] pickup = Array.Empty<AudioClip>();
        [SerializeField] AudioClip[] craft = Array.Empty<AudioClip>();
        [SerializeField] AudioClip[] dodge = Array.Empty<AudioClip>();

        public void Configure(AudioClip[] pickupClips, AudioClip[] craftClips, AudioClip[] dodgeClips)
        {
            pickup = pickupClips ?? Array.Empty<AudioClip>();
            craft = craftClips ?? Array.Empty<AudioClip>();
            dodge = dodgeClips ?? Array.Empty<AudioClip>();
        }

        public bool TryNext(GameCue cue, int previous, out AudioClip clip, out int index)
        {
            var variants = cue switch { GameCue.Pickup => pickup, GameCue.Craft => craft, GameCue.Dodge => dodge, _ => null };
            clip = null; index = -1;
            if (variants == null || variants.Length == 0) return false;
            for (int offset = 1; offset <= variants.Length; offset++)
            {
                int candidate = (Mathf.Max(-1, previous) + offset) % variants.Length;
                if (!variants[candidate]) continue;
                clip = variants[candidate]; index = candidate; return true;
            }
            return false;
        }
    }
}
