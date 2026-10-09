using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    [Explicit, Category("WinterCapture")]
    public sealed class WinterAudioDspTests
    {
        const int Window = 4096;
        const float SilencePeak = 0.000001f, SignalRms = 0.0001f;
        readonly float[] sourceSamples = new float[Window], listenerSamples = new float[Window];
        readonly List<(GameCue cue, AudioClip clip)> played = new();
        readonly Dictionary<AudioSource, bool> sourceMutes = new();
        AudioSource speaker;
        Evidence evidence;
        double flushSeconds, blockSeconds;

        [Serializable]
        sealed class ClipEvidence
        {
            public string cue, clip;
            public float duration, baselinePeak, sourceRms, listenerRms, sourcePeak, listenerPeak;
            public double dspSeconds, maximumObservationGap;
            public int observations, signalWindows;
            public bool passed;
        }

        [Serializable]
        sealed class Evidence
        {
            public string engine, utc, speakerMode, status = "audio backend unverified";
            public int sampleRate, dspBufferLength, dspBufferCount, sampleWindow = Window;
            public double flushSeconds;
            public bool backendReady, mutedGate, masterGate, pausedGate, settingsRestored, completed;
            public bool physicalSpeakerListeningVerified;
            public List<ClipEvidence> clips = new();
        }

        [UnityTest]
        public IEnumerator AllSixWinterVariantsReachTheNativeMixerAndSuppressionRemainsSilent()
        {
            bool hadMute = PlayerPrefs.HasKey("wv.muted"), hadPack = PlayerPrefs.HasKey(GameSoundPacks.PreferenceKey);
            int mute = PlayerPrefs.GetInt("wv.muted", 0);
            string pack = PlayerPrefs.GetString(GameSoundPacks.PreferenceKey, "");
            float volume = AudioListener.volume, capture = Time.captureDeltaTime, timeScale = Time.timeScale;
            bool paused = AudioListener.pause;
            evidence = new Evidence { engine = Application.unityVersion, utc = DateTime.UtcNow.ToString("O"),
                sampleRate = AudioSettings.outputSampleRate, speakerMode = AudioSettings.GetConfiguration().speakerMode.ToString() };
            AudioSettings.GetDSPBufferSize(out evidence.dspBufferLength, out evidence.dspBufferCount);
            GameFeedback.Played += Record;
            try
            {
                Assert.IsFalse(Application.isBatchMode, "Run this explicit test in normal Unity with its audio backend enabled.");
                Time.captureDeltaTime = 0f;
                yield return TestScenes.Reset();
                Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(listener => listener.isActiveAndEnabled));
                GameFeedback.Muted = false;
                AudioListener.pause = false; AudioListener.volume = 1f;
                GameSoundPacks.Select("winter");
                Assert.Greater(evidence.sampleRate, 0);
                blockSeconds = (double)evidence.dspBufferLength / evidence.sampleRate;
                flushSeconds = Math.Max(.15, blockSeconds * (evidence.dspBufferCount + 2) + (double)Window / evidence.sampleRate);
                evidence.flushSeconds = flushSeconds;
                double start = AudioSettings.dspTime, deadline = Time.realtimeSinceStartupAsDouble + 2;
                while (AudioSettings.dspTime <= start + blockSeconds && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.Greater(AudioSettings.dspTime, start + blockSeconds,
                    "Audio backend did not advance. Mixer output is unverified; check the editor audio device and mute state.");
                evidence.backendReady = true; evidence.status = "verification incomplete";

                foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                {
                    sourceMutes[source] = source.mute;
                    if (source.name != "House sound cues") source.mute = true;
                }
                yield return new WaitForSecondsRealtime(.07f);
                Assert.IsTrue(GameFeedback.Preview(GameCue.Pickup, "winter"));
                speaker = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Single(source => source.name == "House sound cues");
                if (!sourceMutes.ContainsKey(speaker)) sourceMutes[speaker] = speaker.mute;
                speaker.mute = false;
                Assert.AreEqual(.13f, speaker.volume);
                Assert.AreEqual(0f, speaker.spatialBlend);
                Assert.IsNull(speaker.outputAudioMixerGroup, "The fixture measures the production direct-to-master route.");
                speaker.Stop();
                speaker.GetOutputData(sourceSamples, 0);
                AudioListener.GetOutputData(listenerSamples, 0);
                played.Clear();

                var bank = Resources.Load<GameSoundBank>("AudioPacks/Winter");
                Assert.IsNotNull(bank);
                foreach (var cue in new[] { GameCue.Pickup, GameCue.Craft, GameCue.Dodge })
                {
                    Assert.IsTrue(bank.TryNext(cue, -1, out var first, out int index));
                    Assert.IsTrue(bank.TryNext(cue, index, out var second, out _));
                    Assert.AreNotSame(first, second);
                    var heard = new List<AudioClip>();
                    for (int variant = 0; variant < 2; variant++)
                    {
                        var result = new ClipEvidence { cue = cue.ToString() };
                        evidence.clips.Add(result);
                        yield return Silence(result);
                        int count = played.Count;
                        GameFeedback.Play(cue);
                        Assert.AreEqual(count + 1, played.Count, cue + " accepts exactly one playback");
                        var actual = played[count];
                        Assert.AreEqual(cue, actual.cue);
                        Assert.That(actual.clip == first || actual.clip == second, cue + " uses an imported Winter variant");
                        heard.Add(actual.clip);
                        result.clip = actual.clip.name; result.duration = actual.clip.length;
                        yield return Measure(result, count + 1);
                    }
                    CollectionAssert.AreEquivalent(new[] { first, second }, heard, cue + " reaches both variants without repetition");
                }
                CollectionAssert.AreEquivalent(new[] { "bell-01", "bell-02", "paper-01", "paper-02", "snow-01", "snow-02" },
                    evidence.clips.Select(clip => clip.clip));

                yield return Silence(null);
                int accepted = played.Count;
                GameFeedback.Muted = true;
                Assert.IsFalse(GameFeedback.Preview(GameCue.Pickup, "winter"));
                yield return RemainSilent(accepted);
                evidence.mutedGate = true;
                GameFeedback.Muted = false;
                AudioListener.volume = 0f;
                Assert.IsFalse(GameFeedback.Preview(GameCue.Pickup, "winter"));
                yield return RemainSilent(accepted);
                evidence.masterGate = true;
                AudioListener.volume = 1f;
                yield return Silence(null);
                AudioListener.pause = true;
                Assert.IsFalse(GameFeedback.Preview(GameCue.Pickup, "winter"));
                yield return RemainSilent(accepted);
                evidence.pausedGate = true;
                evidence.completed = true; evidence.status = "passed";
            }
            finally
            {
                GameFeedback.Played -= Record;
                if (speaker) speaker.Stop();
                foreach (var entry in sourceMutes) if (entry.Key) entry.Key.mute = entry.Value;
                if (hadMute) PlayerPrefs.SetInt("wv.muted", mute); else PlayerPrefs.DeleteKey("wv.muted");
                if (hadPack) PlayerPrefs.SetString(GameSoundPacks.PreferenceKey, pack); else PlayerPrefs.DeleteKey(GameSoundPacks.PreferenceKey);
                AudioListener.volume = volume; AudioListener.pause = paused;
                Time.captureDeltaTime = capture; Time.timeScale = timeScale;
                PlayerPrefs.Save();
                evidence.settingsRestored = AudioListener.volume == volume && AudioListener.pause == paused &&
                    Time.captureDeltaTime == capture && Time.timeScale == timeScale &&
                    PlayerPrefs.HasKey("wv.muted") == hadMute && PlayerPrefs.GetInt("wv.muted", 0) == mute &&
                    PlayerPrefs.HasKey(GameSoundPacks.PreferenceKey) == hadPack && PlayerPrefs.GetString(GameSoundPacks.PreferenceKey, "") == pack;
                string path = Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../docs/reviews/evidence/unity-winter-2026-10-09/audio-dsp.json"));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(evidence, true));
            }
            Assert.IsTrue(evidence.settingsRestored);
        }

        void Record(GameCue cue, AudioClip clip) => played.Add((cue, clip));

        void CheckIsolation()
        {
            foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                Assert.IsTrue(source == speaker || !source.isActiveAndEnabled || source.mute || source.volume <= 0f,
                    "Unrelated unmuted source would contaminate mixer evidence: " + source.name);
        }

        IEnumerator Silence(ClipEvidence result)
        {
            speaker.Stop();
            double start = AudioSettings.dspTime, next = start + flushSeconds;
            double deadline = Time.realtimeSinceStartupAsDouble + 3;
            int clean = 0;
            while (clean < 2 && Time.realtimeSinceStartupAsDouble < deadline)
            {
                CheckIsolation();
                if (AudioSettings.dspTime >= next)
                {
                    AudioListener.GetOutputData(listenerSamples, 0);
                    Stats(listenerSamples, out _, out float peak);
                    if (result != null) result.baselinePeak = peak;
                    clean = peak <= SilencePeak ? clean + 1 : 0;
                    next = AudioSettings.dspTime + blockSeconds;
                }
                yield return null;
            }
            Assert.AreEqual(2, clean, "Mixer must reach silence before the next isolated cue.");
        }

        IEnumerator Measure(ClipEvidence result, int expectedEvents)
        {
            double start = AudioSettings.dspTime, previous = start, next = start + blockSeconds;
            double end = start + result.duration + flushSeconds, deadline = Time.realtimeSinceStartupAsDouble + 3;
            while (AudioSettings.dspTime < end && Time.realtimeSinceStartupAsDouble < deadline)
            {
                CheckIsolation();
                double now = AudioSettings.dspTime;
                if (now >= next)
                {
                    speaker.GetOutputData(sourceSamples, 0);
                    AudioListener.GetOutputData(listenerSamples, 0);
                    Stats(sourceSamples, out float sourceRms, out float sourcePeak);
                    Stats(listenerSamples, out float listenerRms, out float listenerPeak);
                    result.sourceRms = Mathf.Max(result.sourceRms, sourceRms);
                    result.listenerRms = Mathf.Max(result.listenerRms, listenerRms);
                    result.sourcePeak = Mathf.Max(result.sourcePeak, sourcePeak);
                    result.listenerPeak = Mathf.Max(result.listenerPeak, listenerPeak);
                    result.maximumObservationGap = Math.Max(result.maximumObservationGap, now - previous);
                    result.observations++;
                    if (listenerRms > SignalRms) result.signalWindows++;
                    previous = now; next = now + blockSeconds;
                }
                yield return null;
            }
            result.dspSeconds = AudioSettings.dspTime - start;
            Assert.AreEqual(expectedEvents, played.Count, "Unexpected cue during isolated mixer observation.");
            Assert.GreaterOrEqual(AudioSettings.dspTime, end, "DSP stopped during " + result.clip);
            Assert.GreaterOrEqual(result.signalWindows, 2, result.clip + " must produce real master output in multiple fresh DSP windows.");
            Assert.Greater(result.listenerRms, SignalRms, result.clip);
            Assert.Less(result.listenerPeak, .95f, result.clip + " must retain mixer headroom.");
            result.passed = true;
        }

        IEnumerator RemainSilent(int accepted)
        {
            double until = Time.realtimeSinceStartupAsDouble + flushSeconds;
            while (Time.realtimeSinceStartupAsDouble < until)
            {
                CheckIsolation();
                AudioListener.GetOutputData(listenerSamples, 0);
                Stats(listenerSamples, out _, out float peak);
                Assert.LessOrEqual(peak, SilencePeak, "Suppressed playback cannot reach the master output.");
                Assert.AreEqual(accepted, played.Count, "Suppressed playback cannot advance the accepted cue stream.");
                yield return null;
            }
        }

        static void Stats(float[] samples, out float rms, out float peak)
        {
            double square = 0; peak = 0;
            bool finite = true;
            foreach (float sample in samples)
            {
                finite &= !float.IsNaN(sample) && !float.IsInfinity(sample);
                square += (double)sample * sample;
                peak = Mathf.Max(peak, Mathf.Abs(sample));
            }
            Assert.IsTrue(finite, "Mixer samples must remain finite.");
            rms = (float)Math.Sqrt(square / samples.Length);
        }
    }
}
