using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.EditorTools;

namespace Wreckabulary.EditorTests
{
    public sealed class WinterAudioImportTests
    {
        [TestCase("bell-01", .34f), TestCase("bell-02", .39f), TestCase("paper-01", .19f),
         TestCase("paper-02", .22f), TestCase("snow-01", .24f), TestCase("snow-02", .28f)]
        public void ImportedClipPreservesPcmHeadroomAndFades(string id, float duration)
        {
            string path = WinterAudioImporter.AudioDirectory + id + ".wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            Assert.IsNotNull(clip, "Run WinterAudioImporter.Build before native validation.");
            Assert.AreEqual(44100, clip.frequency); Assert.AreEqual(1, clip.channels);
            Assert.AreEqual(duration, clip.length, 1f / 44100);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, importer.defaultSampleSettings.loadType);
            Assert.AreEqual(AudioCompressionFormat.PCM, importer.defaultSampleSettings.compressionFormat);
            Assert.AreEqual(AudioSampleRateSetting.PreserveSampleRate, importer.defaultSampleSettings.sampleRateSetting);
            var samples = new float[clip.samples];
            Assert.IsTrue(clip.GetData(samples, 0));
            double peak = 0, sum = 0, energy = 0;
            foreach (float sample in samples)
            {
                Assert.IsFalse(float.IsNaN(sample) || float.IsInfinity(sample));
                peak = Math.Max(peak, Math.Abs(sample)); sum += sample; energy += sample * sample;
            }
            Assert.LessOrEqual(peak, Math.Pow(10, -3.0 / 20));
            Assert.Greater(energy / samples.Length, .0001);
            Assert.Less(Math.Abs(sum / samples.Length), .0001);
            Assert.AreEqual(0, samples[0]); Assert.AreEqual(0, samples[samples.Length - 1]);
        }

        [Test]
        public void SavedBankContainsTwoDistinctPersistentVariantsPerSupportedCue()
        {
            var bank = AssetDatabase.LoadAssetAtPath<GameSoundBank>(WinterAudioImporter.BankPath);
            Assert.IsNotNull(bank);
            foreach (var cue in new[] { GameCue.Pickup, GameCue.Craft, GameCue.Dodge })
            {
                Assert.IsTrue(bank.TryNext(cue, -1, out var first, out int firstIndex));
                Assert.IsTrue(bank.TryNext(cue, firstIndex, out var second, out int secondIndex));
                Assert.AreNotSame(first, second);
                Assert.IsTrue(EditorUtility.IsPersistent(first) && EditorUtility.IsPersistent(second));
                Assert.IsTrue(bank.TryNext(cue, secondIndex, out var again, out _));
                Assert.AreSame(first, again);
            }
            Assert.IsFalse(bank.TryNext(GameCue.Blast, -1, out _, out _));
        }
    }
}
