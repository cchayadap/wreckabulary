using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public enum GameCue { Pickup, Break, Craft, Hit, Block, Jump, Dodge, Swing, Out }

    public static class GameFeedback
    {
        static AudioSource speaker;
        static readonly Dictionary<GameCue, AudioClip> clips = new();
        static readonly Dictionary<GameCue, float> last = new();

        public static bool Muted
        {
            get => PlayerPrefs.GetInt("wv.muted", 0) != 0;
            set { PlayerPrefs.SetInt("wv.muted", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            foreach (var clip in clips.Values) if (clip) UnityEngine.Object.Destroy(clip);
            clips.Clear(); last.Clear(); speaker = null;
        }

        public static void Play(GameCue cue)
        {
            if (Muted) return;
            if (last.TryGetValue(cue, out float at) && Time.unscaledTime - at < 0.055f) return;
            last[cue] = Time.unscaledTime;
            if (!speaker)
            {
                var root = new GameObject("House sound cues");
                UnityEngine.Object.DontDestroyOnLoad(root);
                speaker = root.AddComponent<AudioSource>();
                speaker.playOnAwake = false; speaker.spatialBlend = 0f; speaker.volume = 0.13f;
            }
            if (!clips.TryGetValue(cue, out var clip)) clips[cue] = clip = Make(cue);
            speaker.PlayOneShot(clip);
        }

        static AudioClip Make(GameCue cue)
        {
            const int rate = 16000;
            float seconds = cue == GameCue.Craft ? 0.28f : cue == GameCue.Out ? 0.3f : 0.12f;
            float low = cue switch
            {
                GameCue.Pickup => 670, GameCue.Craft => 430, GameCue.Break => 140,
                GameCue.Hit => 170, GameCue.Block => 520, GameCue.Jump => 310,
                GameCue.Dodge => 260, GameCue.Out => 400, _ => 230
            };
            float high = cue switch
            {
                GameCue.Pickup => 1050, GameCue.Craft => 1050, GameCue.Jump => 750,
                GameCue.Dodge => 680, GameCue.Out => 130, _ => low * 0.45f
            };
            var samples = new float[Mathf.CeilToInt(rate * seconds)];
            var noise = new System.Random(19 + (int)cue);
            float phase = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / samples.Length;
                phase += Mathf.Lerp(low, high, t) * Mathf.PI * 2f / rate;
                float envelope = Mathf.Min(1f, t * 35f) * Mathf.Pow(1f - t, 2.5f);
                float wave = Mathf.Sin(phase) * 0.7f + Mathf.Sin(phase * 2.01f) * 0.15f;
                if (cue is GameCue.Break or GameCue.Hit or GameCue.Swing)
                    wave += ((float)noise.NextDouble() * 2f - 1f) * 0.24f;
                samples[i] = wave * envelope;
            }
            var clip = AudioClip.Create("House " + cue, samples.Length, 1, rate, false);
            clip.SetData(samples, 0); return clip;
        }

        public static void Burst(string model, Vector3 position, float size = 0.4f)
        {
            var library = ModelLibrary.Load();
            string key = "VFX/" + model;
            if (!library || !library.Find(key)) return;
            var root = new GameObject(model + " cue");
            root.transform.SetParent(World.Transient, false);
            root.transform.position = position;
            var copy = ModelVisual.Spawn(key, root.transform);
            var bounds = ModelVisual.BoundsIn(root.transform, copy);
            float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float scale = size / Mathf.Max(0.01f, longest);
            root.transform.localScale = Vector3.one * scale;
            copy.transform.localPosition -= bounds.center;
            root.AddComponent<FeedbackBurst>();
        }
    }

}
