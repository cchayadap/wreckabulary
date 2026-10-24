using System;
using NUnit.Framework;
using UpdateTimeMode = Fusion.SimulationConfig.SimulationTimeMode;

namespace Wreckabulary.OnlineGameplayTests
{
    public sealed class WordboundNativeTickFrameTests
    {
        [Test]
        public void ActualDeltaThresholdIsStrictAndKeepsSdkFrontiersDistinct()
        {
            var frames = new WordboundNativeTickBatches.FrameDeltas();
            Record(frames, 0, 1);
            Record(frames, .5f, 2);
            Record(frames, 1, 3);
            frames.Record(1.0001f, UpdateTimeMode.UnscaledDeltaTime, 4, 1268, 1249, 1467, 0);
            Assert.AreEqual(4, frames.Samples);
            Assert.AreEqual(1, frames.AboveOneSecond);
            Assert.AreEqual((double)1.0001f, frames.MaxUnscaledDelta);
            Assert.AreEqual(1, frames.Retained);
            var sample = frames.At(0);
            Assert.AreEqual(4, sample.Frame);
            Assert.AreEqual(1268, sample.SdkTick);
            Assert.AreEqual(1249, sample.InputTick);
            Assert.AreEqual(1467, sample.LatestServerTick);
            Assert.AreEqual(0, sample.TicksExecuted);
            Assert.AreEqual(UpdateTimeMode.UnscaledDeltaTime, sample.Mode);
        }

        [Test]
        public void ExceptionalTailKeepsOnlyLatestSixteenWithoutLosingTotalsOrMode()
        {
            var frames = new WordboundNativeTickBatches.FrameDeltas();
            Assert.Throws<ArgumentOutOfRangeException>(() => frames.At(0));
            for (int i = 0; i < 21; i++)
                frames.Record(2 + i, i % 2 == 0 ? UpdateTimeMode.UnscaledDeltaTime : UpdateTimeMode.DeltaTime,
                    i, 100 + i, 80 + i, 120 + i, i);
            Record(frames, .25f, 99);
            Assert.AreEqual(22, frames.Samples);
            Assert.AreEqual(21, frames.AboveOneSecond);
            Assert.AreEqual(22, frames.MaxUnscaledDelta);
            Assert.AreEqual(16, frames.Retained);
            for (int i = 0; i < frames.Retained; i++)
            {
                var sample = frames.At(i);
                int expected = i + 5;
                Assert.AreEqual(expected, sample.Frame);
                Assert.AreEqual(expected + 2, sample.UnscaledDelta);
                Assert.AreEqual(expected + 100, sample.SdkTick);
                Assert.AreEqual(expected + 80, sample.InputTick);
                Assert.AreEqual(expected + 120, sample.LatestServerTick);
                Assert.AreEqual(expected, sample.TicksExecuted);
                Assert.AreEqual(expected % 2 == 0 ? UpdateTimeMode.UnscaledDeltaTime : UpdateTimeMode.DeltaTime, sample.Mode);
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => frames.At(16));
            Assert.Throws<ArgumentOutOfRangeException>(() => frames.At(-1));
        }

        [Test]
        public void InvalidAndUnavailableReadsDoNotInventLongFramesOrExposeMessages()
        {
            var frames = new WordboundNativeTickBatches.FrameDeltas();
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -.1f })
                Record(frames, invalid, 1);
            frames.Unavailable(new InvalidOperationException("Private diagnostic payload must not be retained."));
            Record(frames, .5f, 2);
            Assert.AreEqual(5, frames.Samples);
            Assert.AreEqual(4, frames.InvalidDeltas);
            Assert.AreEqual(1, frames.UnavailableReads);
            Assert.AreEqual(nameof(InvalidOperationException), frames.LastUnavailableType);
            Assert.AreEqual(.5, frames.MaxUnscaledDelta);
            Assert.AreEqual(0, frames.AboveOneSecond);
            Assert.AreEqual(0, frames.Retained);
        }

        [Test]
        public void StopFreezesCountersAndRetainedFramesAgainstLateCallbacks()
        {
            var frames = new WordboundNativeTickBatches.FrameDeltas();
            Record(frames, 2, 7);
            frames.Stop(); frames.Stop();
            Record(frames, 100, 8);
            Record(frames, float.NaN, 9);
            frames.Unavailable(new ObjectDisposedException("late"));
            Assert.AreEqual(1, frames.Samples);
            Assert.AreEqual(1, frames.AboveOneSecond);
            Assert.AreEqual(2, frames.MaxUnscaledDelta);
            Assert.AreEqual(0, frames.InvalidDeltas);
            Assert.AreEqual(0, frames.UnavailableReads);
            Assert.IsNull(frames.LastUnavailableType);
            Assert.AreEqual(7, frames.At(0).Frame);
        }

        static void Record(WordboundNativeTickBatches.FrameDeltas frames, float delta, int frame) =>
            frames.Record(delta, UpdateTimeMode.UnscaledDeltaTime, frame, 0, 0, 0, 0);
    }
}
