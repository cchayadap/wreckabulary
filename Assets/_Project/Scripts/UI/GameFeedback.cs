using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    public enum GameCue { Pickup, Break, Craft, Hit, Block, Jump, Dodge, Swing, Out, Protect, Boost, Blast, Heal }

    public static class GameFeedback
    {
        static AudioSource speaker;
        static readonly Dictionary<GameCue, AudioClip> clips = new();
        static readonly Dictionary<GameCue, float> last = new();
        public static event System.Action<GameCue, AudioClip> Played;

        public static bool Muted
        {
            get => PlayerPrefs.GetInt("wv.muted", 0) != 0;
            set { PlayerPrefs.SetInt("wv.muted", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            foreach (var clip in clips.Values) if (clip) UnityEngine.Object.Destroy(clip);
            clips.Clear(); last.Clear(); speaker = null; Played = null;
        }

        public static void Play(GameCue cue)
            => PlayInternal(cue, GameSoundPacks.Selected, false);

        public static bool Preview(GameCue cue, string pack)
            => (pack == "default" || pack == "winter") && PlayInternal(cue, pack, true);

        static bool PlayInternal(GameCue cue, string pack, bool preview)
        {
            if (Muted || AudioListener.volume <= 0f || AudioListener.pause) return false;
            if (last.TryGetValue(cue, out float at) && Time.unscaledTime - at < 0.055f) return false;
            if (!speaker)
            {
                var root = new GameObject("House sound cues");
                UnityEngine.Object.DontDestroyOnLoad(root);
                speaker = root.AddComponent<AudioSource>();
                speaker.playOnAwake = false; speaker.spatialBlend = 0f; speaker.volume = 0.13f;
            }
            bool bankClip = GameSoundPacks.TryPick(cue, pack, preview, out var clip, out int variant);
            if (!bankClip && !clips.TryGetValue(cue, out clip)) clips[cue] = clip = Make(cue);
            speaker.PlayOneShot(clip);
            last[cue] = Time.unscaledTime;
            if (bankClip) GameSoundPacks.Played(cue, preview, variant);
            Played?.Invoke(cue, clip);
            return true;
        }

        static AudioClip Make(GameCue cue)
        {
            const int rate = 16000;
            float seconds = cue is GameCue.Craft or GameCue.Heal ? 0.28f : cue == GameCue.Out ? 0.3f : 0.12f;
            float low = cue switch
            {
                GameCue.Pickup => 670, GameCue.Craft => 430, GameCue.Break => 140,
                GameCue.Hit => 170, GameCue.Block => 520, GameCue.Jump => 310,
                GameCue.Dodge => 260, GameCue.Out => 400, GameCue.Protect => 760,
                GameCue.Boost => 340, GameCue.Blast => 85, GameCue.Heal => 540, _ => 230
            };
            float high = cue switch
            {
                GameCue.Pickup => 1050, GameCue.Craft => 1050, GameCue.Jump => 750,
                GameCue.Dodge => 680, GameCue.Out => 130, GameCue.Protect => 1150,
                GameCue.Boost => 980, GameCue.Heal => 1080, _ => low * 0.45f
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
                if (cue is GameCue.Break or GameCue.Hit or GameCue.Swing or GameCue.Blast)
                    wave += ((float)noise.NextDouble() * 2f - 1f) * 0.24f;
                samples[i] = wave * envelope;
            }
            var clip = AudioClip.Create("House " + cue, samples.Length, 1, rate, false);
            clip.SetData(samples, 0); return clip;
        }

        public static void Burst(string model, Vector3 position, float size = 0.4f)
        {
            FeedbackBurst.Play(model, position, size);
        }

        public static void Burst(string model, Vector3 position, float size, Color tint, float seconds = 0.45f) =>
            FeedbackBurst.Play(model, position, size, tint, seconds);

        public static Color SkillColor(string word) => word switch
        {
            "FOAM" => new Color(.43f, .88f, 1f),
            "SOAP" => new Color(.40f, .84f, .91f),
            "BED" => new Color(.74f, .60f, 1f),
            "MAT" => new Color(.48f, .92f, .62f),
            "BOMB" => new Color(1f, .42f, .22f),
            "PLATE" => new Color(1f, .83f, .42f),
            "SHIELD" => new Color(1f, .83f, .42f),
            "APPLE" => new Color(.53f, .96f, .46f),
            "WATER" => new Color(.35f, .83f, 1f),
            "CAKE" => new Color(1f, .60f, .77f),
            "SODA" => new Color(.75f, 1f, .35f),
            "FAN" => new Color(.64f, .97f, 1f),
            "CLOCK" => new Color(.76f, .59f, 1f),
            "PIE" => new Color(1f, .91f, .69f),
            "STOOL" => new Color(.87f, .67f, 1f),
            _ => new Color(1f, .77f, .37f)
        };
    }

}
