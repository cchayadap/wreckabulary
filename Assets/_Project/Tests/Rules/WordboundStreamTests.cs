using System;
using GameStudioKit.Determinism;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundStreamTests
    {
        const int Streams = 32;
        const int Draws = 10000;
        static readonly uint[] bounds = { 1, 2, 3, 4, 7, 1000, 65537, 0x80000000u, 0x80000001u, 0xC0000001u, 0xFFFFFFFFu };

        static ulong Seed(int stream) => Hash64.Combine(0x5eed5eed5eed5eedUL, (ulong)stream);

        [Test]
        public void NamedStreamsMatchPcg32BitForBitIncludingBoundedRejection()
        {
            for (int s = 0; s < Streams; s++)
            {
                string purpose = s % 2 == 0 ? "story.deck" : "story.actor." + s;
                var reference = Pcg32.ForStream(Seed(s), purpose, s - 7);
                var stream = WordboundStream.ForStream(Seed(s), purpose, s - 7);
                for (int i = 0; i < Draws; i++)
                {
                    uint bound = bounds[(i + s) % bounds.Length];
                    bool bounded = i % 3 != 0;
                    uint expected = bounded ? reference.NextUInt(bound) : reference.NextUInt();
                    uint actual = bounded ? stream.NextUInt(bound) : stream.NextUInt();
                    if (expected != actual) Assert.Fail($"stream {s} draw {i}: expected {expected} but was {actual}");
                }
            }
        }

        [Test]
        public void ReferenceSeedingMatchesPcg32Constructor()
        {
            for (int s = 0; s < Streams; s++)
            {
                var reference = new Pcg32(Seed(s), (ulong)s * 0x9E3779B97F4A7C15UL);
                var stream = new WordboundStream(Seed(s), (ulong)s * 0x9E3779B97F4A7C15UL);
                Assert.IsTrue((stream.Increment & 1UL) == 1UL);
                for (int i = 0; i < Draws; i++)
                    if (reference.NextUInt() != stream.NextUInt()) Assert.Fail($"stream {s} draw {i} differs");
            }
        }

        [Test]
        public void BoundedDrawsRejectTheBiasedZoneLikePcg32()
        {
            const uint bound = 0x80000001u;
            var reference = Pcg32.ForStream(Seed(3), "story.jobs");
            var stream = WordboundStream.ForStream(Seed(3), "story.jobs");
            int rejected = 0;
            for (int i = 0; i < Draws; i++)
            {
                var probe = stream;
                uint value = stream.NextUInt(bound);
                Assert.IsTrue(value < bound);
                Assert.AreEqual(reference.NextUInt(bound), value);
                int steps = 0;
                while (probe.State != stream.State && steps < 64) { probe.NextUInt(); steps++; }
                Assert.IsTrue(probe.State == stream.State);
                if (steps > 1) rejected++;
            }
            Assert.Greater(rejected, Draws / 4);
        }

        [Test]
        public void BelowMatchesUnsignedBoundAndRejectsEmptyRanges()
        {
            var reference = Pcg32.ForStream(Seed(9), "story.deck");
            var stream = WordboundStream.ForStream(Seed(9), "story.deck");
            for (int i = 0; i < Draws; i++) Assert.AreEqual((int)reference.NextUInt(1000), stream.Below(1000));
            Assert.Throws<ArgumentOutOfRangeException>(() => stream.Below(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => stream.Below(-4));
            Assert.Throws<ArgumentOutOfRangeException>(() => stream.NextUInt(0));
        }

        [Test]
        public void StateRoundTripsThroughMatchRandomStateMidSequence()
        {
            for (int s = 0; s < Streams; s++)
            {
                var stream = WordboundStream.ForStream(Seed(s), "story.actor.0", s);
                for (int i = 0; i < 1000 + s * 37; i++) stream.NextUInt(1000);
                var saved = stream.Capture("story.actor.0");
                Assert.AreEqual("story.actor.0", saved.StreamId);
                Assert.IsTrue(saved.State == stream.State && saved.Increment == stream.Increment);
                var restored = WordboundStream.Restore(new MatchRandomState(saved.StreamId, saved.State, saved.Increment));
                Assert.IsTrue(restored.State == stream.State && restored.Increment == stream.Increment);
                for (int i = 0; i < Draws; i++)
                    if (stream.NextUInt(bounds[i % bounds.Length]) != restored.NextUInt(bounds[i % bounds.Length]))
                        Assert.Fail($"stream {s} diverged after restore at draw {i}");
            }
        }

        [Test]
        public void RestoreKeepsHighBitStatesAndRefusesEvenIncrements()
        {
            var stream = WordboundStream.FromState(ulong.MaxValue, ulong.MaxValue);
            var copy = WordboundStream.Restore(stream.Capture("story.deck"));
            Assert.IsTrue(copy.State == ulong.MaxValue && copy.Increment == ulong.MaxValue);
            Assert.AreEqual(stream.NextUInt(), copy.NextUInt());
            Assert.Throws<ArgumentException>(() => WordboundStream.FromState(1UL, 2UL));
            Assert.Throws<ArgumentNullException>(() => WordboundStream.Restore(null));
        }

        [Test]
        public void CopiesAdvanceIndependently()
        {
            var stream = WordboundStream.ForStream(Seed(1), "story.deck");
            var copy = stream;
            uint first = stream.NextUInt();
            Assert.AreEqual(first, copy.NextUInt());
            Assert.IsTrue(stream.State == copy.State);
            stream.NextUInt();
            Assert.IsFalse(stream.State == copy.State);
        }
    }
}
