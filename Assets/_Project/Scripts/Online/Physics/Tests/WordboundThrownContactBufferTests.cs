using System;
using NUnit.Framework;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundThrownContactBufferTests
    {
        static HostThrownContact Contact(string target = "player:1", float speed = 5, long tick = 7, int revision = 2) =>
            new HostThrownContact("item:1", revision, target, default, speed, tick);

        [Test]
        public void MultipleColliderCallbacksProduceOneStrongestContactAndStableTargetOrder()
        {
            var buffer = new WordboundThrownContactBuffer();
            buffer.Begin(3, 7);
            buffer.Add(3, Contact("player:2", 4));
            buffer.Add(3, Contact("player:1", 2));
            buffer.Add(3, Contact("player:2", 6));
            buffer.Add(3, Contact("player:2", 3));
            buffer.Complete(3, 7);
            var result = buffer.Read(3, 7);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("player:1", result[0].TargetBodyId);
            Assert.AreEqual(6, result[1].RelativeSpeed);
        }

        [Test]
        public void PreviousEpochOrTickCannotEnterOrReadAReplacementHostStep()
        {
            var buffer = new WordboundThrownContactBuffer();
            buffer.Begin(3, 7);
            Assert.Throws<ArgumentException>(() => buffer.Add(2, Contact()));
            Assert.Throws<ArgumentException>(() => buffer.Add(3, Contact(tick: 6)));
            Assert.Throws<InvalidOperationException>(() => buffer.Read(3, 7));
            Assert.Throws<ArgumentException>(() => buffer.Complete(3, 8));
            buffer.Complete(3, 7);
            Assert.Throws<InvalidOperationException>(() => buffer.Read(4, 7));
            Assert.Throws<ArgumentException>(() => buffer.Add(3, Contact()));
            buffer.Begin(4, 7);
            buffer.Complete(4, 7);
            Assert.AreEqual(0, buffer.Read(4, 7).Count);
        }

        [Test]
        public void OneSimulationStepCannotMixRelaunchedItemIdentities()
        {
            var buffer = new WordboundThrownContactBuffer();
            buffer.Begin(3, 7); buffer.Add(3, Contact());
            Assert.Throws<ArgumentException>(() => buffer.Add(3, Contact(revision: 3)));
        }

        [Test]
        public void SaturationFailsTheStepInsteadOfSilentlyDroppingADamagingContact()
        {
            var buffer = new WordboundThrownContactBuffer();
            buffer.Begin(3, 7);
            for (int i = 0; i < WordboundThrownContactBuffer.MaximumContacts; i++)
                buffer.Add(3, Contact("item:" + (i + 2)));
            Assert.Throws<InvalidOperationException>(() => buffer.Add(3, Contact("item:9999")));
        }
    }
}
