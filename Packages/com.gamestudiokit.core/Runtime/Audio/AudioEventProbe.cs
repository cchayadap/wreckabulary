// Game Studio Kit (MIT). Proves every audio event fires and is audible (ARCHITECTURE §3.10). Game code reports each
// event with AudioEventProbe.Fire(id, source); while a QA run is active the probe counts fires and, one frame later,
// checks that the voice is really audible: source enabled and not muted, volume > 0, listener not paused or silent,
// inside the 3D max distance, playing, and not virtualised by the voice limit. Inert (one branch) outside QA runs.
using System;
using System.Collections.Generic;
using GameStudioKit.Common;
using GameStudioKit.Qa;
using UnityEngine;

namespace GameStudioKit.Audio
{
    public sealed class AudioEventStats
    {
        public string Id;
        public int Fired;
        public int Audible;
        public readonly Dictionary<string, int> InaudibleReasons = new Dictionary<string, int>(StringComparer.Ordinal);
    }

    public static class AudioEventProbe
    {
        /// <summary>Set by QaRunner for the length of a run (or by a test). When false, Fire does nothing.</summary>
        public static bool Enabled;

        static readonly Dictionary<string, AudioEventStats> Stats = new Dictionary<string, AudioEventStats>(StringComparer.Ordinal);
        static readonly HashSet<string> Expected = new HashSet<string>(StringComparer.Ordinal);
        static readonly List<Pending> PendingChecks = new List<Pending>();
        static AudioEventProbeRunner _runner;

        struct Pending
        {
            public string Id;
            public AudioSource Source;
            public bool OneShot;
            public int Frame;
        }

        public static int ExpectedCount => Expected.Count;

        /// <summary>True when any event fired, is waiting for its audibility check, or is expected in this run.</summary>
        public static bool HasActivity => Stats.Count > 0 || Expected.Count > 0 || PendingChecks.Count > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Enabled = false;
            Stats.Clear();
            Expected.Clear();
            PendingChecks.Clear();
            _runner = null;
            _listener = null;
        }

        /// <summary>Event ids that must fire during the run (normally the AUDIO_SHEET.md ids the scenario reaches). QaRunner
        /// resets the probe before it builds the scenario's steps, so call this from BuildSteps or a step.</summary>
        public static void Expect(IEnumerable<string> ids)
        {
            if (ids == null) return;
            foreach (var id in ids) if (!string.IsNullOrEmpty(id)) Expected.Add(id);
        }

        public static void Reset()
        {
            Stats.Clear();
            Expected.Clear();
            PendingChecks.Clear();
        }

        /// <summary>
        /// Reports that audio event <paramref name="id"/> was triggered on <paramref name="source"/>. Pass
        /// <paramref name="oneShot"/> = true for PlayOneShot (isPlaying is not checked then).
        /// </summary>
        public static void Fire(string id, AudioSource source, bool oneShot = false)
        {
            if (!Enabled || string.IsNullOrEmpty(id)) return;
            var s = GetStats(id);
            s.Fired++;
            string immediate = ImmediateReason(source, oneShot);
            if (immediate != null)
            {
                Count(s, immediate);
                return;
            }
            EnsureRunner();
            PendingChecks.Add(new Pending { Id = id, Source = source, OneShot = oneShot, Frame = Time.frameCount });
        }

        /// <summary>Snapshot of per-event statistics.</summary>
        public static List<AudioEventStats> Snapshot()
        {
            FlushAll();
            var list = new List<AudioEventStats>(Stats.Values);
            foreach (var id in Expected) if (!Stats.ContainsKey(id)) list.Add(new AudioEventStats { Id = id });
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        /// <summary>
        /// Probe results: audio_events_missing (expected ids that never fired) and audio_events_inaudible (ids that fired
        /// but were never audible).
        /// </summary>
        public static List<ProbeResult> Evaluate(ProbeConfig config)
        {
            config = config ?? ProbeConfig.Defaults();
            int missing = 0, inaudible = 0;
            foreach (var s in Snapshot())
            {
                if (s.Fired == 0 && Expected.Contains(s.Id)) missing++;
                else if (s.Fired > 0 && s.Audible == 0) inaudible++;
            }
            return new List<ProbeResult>
            {
                config.Evaluate("AudioEventProbe", "audio_events_missing", missing),
                config.Evaluate("AudioEventProbe", "audio_events_inaudible", inaudible),
            };
        }

        /// <summary>Writes audio_events.json: {schema:1, utc, events:[{id, expected, fired, audible, inaudible_reasons}]}.</summary>
        public static void WriteJson(string path)
        {
            var w = new KitJsonWriter();
            w.BeginObject().Prop("schema", 1).Prop("utc", KitIo.UtcNowIso());
            w.Name("events").BeginArray();
            foreach (var s in Snapshot())
            {
                w.BeginObject()
                    .Prop("id", s.Id)
                    .Prop("expected", Expected.Contains(s.Id))
                    .Prop("fired", s.Fired)
                    .Prop("audible", s.Audible)
                    .PropAny("inaudible_reasons", s.InaudibleReasons)
                    .EndObject();
            }
            w.EndArray().EndObject();
            KitIo.WriteAllTextAtomic(path, w + "\n");
        }

        static AudioEventStats GetStats(string id)
        {
            if (!Stats.TryGetValue(id, out var s))
            {
                s = new AudioEventStats { Id = id };
                Stats[id] = s;
            }
            return s;
        }

        static void Count(AudioEventStats s, string reason)
        {
            s.InaudibleReasons.TryGetValue(reason, out int n);
            s.InaudibleReasons[reason] = n + 1;
        }

        /// <summary>Checks that can be made at fire time; null when the voice might be audible.</summary>
        static string ImmediateReason(AudioSource source, bool oneShot)
        {
            if (source == null) return "no_source";
            if (!source.enabled || !source.gameObject.activeInHierarchy) return "source_disabled";
            if (source.mute) return "muted";
            if (source.volume <= 0.001f) return "zero_volume";
            if (!oneShot && source.clip == null) return "no_clip";
            if (AudioListener.volume <= 0.001f) return "listener_silent";
            if (AudioListener.pause && !source.ignoreListenerPause) return "listener_paused";
            if (source.spatialBlend > 0.5f)
            {
                var listener = FindListener();
                if (listener == null) return "no_listener";
                float d = Vector3.Distance(listener.transform.position, source.transform.position);
                if (d > source.maxDistance) return "out_of_range";
            }
            return null;
        }

        static AudioListener _listener;

        static AudioListener FindListener()
        {
            if (_listener != null && _listener.isActiveAndEnabled) return _listener;
            _listener = UnityEngine.Object.FindAnyObjectByType<AudioListener>();
            return _listener;
        }

        /// <summary>Runs the one-frame-later checks (virtualisation and playing state are known only after a frame).</summary>
        internal static void Flush(int currentFrame)
        {
            for (int i = PendingChecks.Count - 1; i >= 0; i--)
            {
                var p = PendingChecks[i];
                if (p.Frame >= currentFrame) continue;
                Resolve(p);
                PendingChecks.RemoveAt(i);
            }
        }

        static void FlushAll()
        {
            foreach (var p in PendingChecks) Resolve(p);
            PendingChecks.Clear();
        }

        static void Resolve(Pending p)
        {
            var s = GetStats(p.Id);
            string reason = null;
            if (p.Source == null) reason = "source_destroyed";
            else if (p.Source.isVirtual) reason = "virtualised";
            else if (!p.OneShot && !p.Source.isPlaying) reason = "not_playing";
            if (reason == null) s.Audible++;
            else Count(s, reason);
        }

        static void EnsureRunner()
        {
            if (_runner != null) return;
            var go = new GameObject("GSK AudioEventProbe");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            _runner = go.AddComponent<AudioEventProbeRunner>();
        }
    }

    [AddComponentMenu("")]
    internal sealed class AudioEventProbeRunner : MonoBehaviour
    {
        void LateUpdate() => AudioEventProbe.Flush(Time.frameCount);
    }
}
