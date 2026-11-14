using System;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary.Audio
{
    public readonly struct ImpactPlayback
    {
        public readonly string EventId;
        public readonly ImpactLayer Layer;
        public readonly string Key;
        public readonly string Clip;
        public readonly float Volume;
        public readonly float Pitch;
        public readonly bool Occluded;
        public readonly Vector3 Position;

        public ImpactPlayback(string eventId, ImpactLayer layer, string key, string clip, float volume, float pitch, bool occluded, Vector3 position)
        {
            EventId = eventId;
            Layer = layer;
            Key = key;
            Clip = clip;
            Volume = volume;
            Pitch = pitch;
            Occluded = occluded;
            Position = position;
        }
    }

    public sealed class ImpactAudioPlayer : MonoBehaviour
    {
        static ImpactAudioPlayer instance;
        public static event Action<ImpactPlayback> Played;

        ImpactAudioLibrary library;
        AudioSource[] sources;
        AudioLowPassFilter[] filters;
        ImpactVoiceBudget budget;
        readonly ImpactEventLedger ledger = new ImpactEventLedger(1024);
        readonly Dictionary<string, ImpactContactCooldown> cooldowns = new Dictionary<string, ImpactContactCooldown>(StringComparer.Ordinal);
        readonly Dictionary<string, int> lastVariant = new Dictionary<string, int>(StringComparer.Ordinal);
        Transform listener;

        public ImpactAudioLibrary Library => library;
        public static bool Ready => instance && instance.library;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            Played = null;
        }

        public static ImpactAudioPlayer Instance
        {
            get
            {
                if (instance) return instance;
                var library = Resources.Load<ImpactAudioLibrary>(ImpactAudioLibrary.ResourcePath);
                if (!library) return null;
                var root = new GameObject("Impact sounds");
                DontDestroyOnLoad(root);
                instance = root.AddComponent<ImpactAudioPlayer>();
                instance.Use(library);
                return instance;
            }
        }

        public void Use(ImpactAudioLibrary next)
        {
            library = next;
            library.Rebuild();
            int voices = Mathf.Max(1, library.voices);
            if (sources != null) foreach (var s in sources) if (s) Destroy(s.gameObject);
            sources = new AudioSource[voices];
            filters = new AudioLowPassFilter[voices];
            for (int i = 0; i < voices; i++)
            {
                var voice = new GameObject("Voice " + (i + 1));
                voice.transform.SetParent(transform, false);
                var source = sources[i] = voice.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.dopplerLevel = 0f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                filters[i] = voice.AddComponent<AudioLowPassFilter>();
                filters[i].enabled = false;
            }
            budget = new ImpactVoiceBudget(voices, Mathf.Clamp(library.voicesPerSound, 1, voices));
        }

        public static bool Silent => GameFeedback.Muted || AudioListener.volume <= 0f || AudioListener.pause;

        public static bool Swing(string weapon, string swingId, Vector3 at)
        {
            var player = Instance;
            if (!player || !player.ledger.Admit("swing|" + swingId) || Silent) return false;
            string key = ImpactCues.SwingKey(weapon);
            if (!player.library.Has(key)) key = ImpactCues.SwingKey(ImpactCues.DefaultWeapon);
            return player.Play(swingId, ImpactLayer.Swing, key, 1f, at, null, null);
        }

        public static int Impact(in ImpactEvent e, Vector3 at, string source = null, string target = null)
        {
            var player = Instance;
            if (!player) return 0;
            int played = 0;
            foreach (var cue in ImpactCues.Plan(e, player.library.Has))
                if (player.ledger.Admit(cue.Once) && !Silent && player.Play(cue.Once, cue.Layer, cue.Key, cue.Volume, at, source, target)) played++;
            return played;
        }

        public static int Break(string target, SurfaceMaterial surface, float strength, Vector3 at)
        {
            var player = Instance;
            if (!player) return 0;
            int played = 0;
            foreach (var cue in ImpactCues.BreakOnly(target, surface, strength, player.library.Has))
                if (player.ledger.Admit(cue.Once) && !Silent && player.Play(cue.Once, cue.Layer, cue.Key, cue.Volume, at, null, null)) played++;
            return played;
        }

        bool Play(string eventId, ImpactLayer layer, string key, float volume, Vector3 at, string source, string target)
        {
            var sound = library.Find(key);
            if (sound == null || !sound.HasClips) return false;
            double now = Time.unscaledTimeAsDouble;
            if (source != null && target != null && sound.cooldown > 0f)
            {
                if (!cooldowns.TryGetValue(key, out var gate)) cooldowns[key] = gate = new ImpactContactCooldown(sound.cooldown);
                if (!gate.Admit(source, target, key, now)) return false;
            }

            if (!listener || !listener.gameObject.activeInHierarchy)
            {
                var found = FindAnyObjectByType<AudioListener>();
                listener = found ? found.transform : null;
            }
            bool occluded = false;
            float distance = 0f;
            if (listener && library.spatialBlend > 0f)
            {
                Vector3 ear = listener.position;
                distance = Vector3.Distance(ear, at);
                occluded = Physics.Linecast(ear, at, out var hit, library.occluders, QueryTriggerInteraction.Ignore) &&
                    (hit.point - at).sqrMagnitude > 0.09f;
                if (ImpactCues.Audibility(distance, library.maxDistance, occluded, library.occludedVolume) < 0.02f) return false;
            }

            int previous = lastVariant.TryGetValue(key, out int p) ? p : -1;
            int variant = ImpactVariation.Pick(eventId, key, sound.clips.Length, previous);
            var clip = variant >= 0 ? sound.clips[variant] : null;
            if (!clip) return false;
            float gain = Mathf.Clamp01(volume * sound.volume * library.volume * (occluded ? library.occludedVolume : 1f));
            int slot = budget.Request(key, ImpactCues.PriorityOf(layer), gain, now, clip.length);
            if (slot < 0) return false;
            lastVariant[key] = variant;

            float pitch = ImpactVariation.Pitch(eventId, key, sound.pitchMin, sound.pitchMax);
            var voice = sources[slot];
            voice.Stop();
            voice.transform.position = at;
            voice.clip = clip;
            voice.volume = gain;
            voice.pitch = pitch;
            voice.spatialBlend = library.spatialBlend;
            voice.minDistance = library.minDistance;
            voice.maxDistance = library.maxDistance;
            voice.priority = 128 - ImpactCues.PriorityOf(layer) * 24;
            filters[slot].enabled = occluded;
            filters[slot].cutoffFrequency = library.occludedCutoff;
            voice.Play();
            Played?.Invoke(new ImpactPlayback(eventId, layer, key, clip.name, gain, pitch, occluded, at));
            return true;
        }
    }
}
