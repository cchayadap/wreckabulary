using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Online;

namespace Wreckabulary.Tests
{
    public sealed class WordboundBodyMotionTests
    {
        [Test]
        public void ThrownBodyUsesContinuousDetectionAndSafeFreezeResumeTransitions()
        {
            var obj = new GameObject("Thrown body transition");
            try
            {
                obj.AddComponent<BoxCollider>();
                var body = obj.AddComponent<Rigidbody>();
                WordboundBodyMotion.Apply(body, false, true);
                Assert.AreEqual(CollisionDetectionMode.ContinuousDynamic, body.collisionDetectionMode);
                Assert.IsFalse(body.isKinematic);
                WordboundBodyMotion.Apply(body, true, true);
                Assert.AreEqual(CollisionDetectionMode.Discrete, body.collisionDetectionMode);
                Assert.IsTrue(body.isKinematic);
                WordboundBodyMotion.Apply(body, false, true);
                Assert.AreEqual(CollisionDetectionMode.ContinuousDynamic, body.collisionDetectionMode);
                Assert.IsFalse(body.isKinematic);
                WordboundBodyMotion.Apply(body, false, false);
                Assert.AreEqual(CollisionDetectionMode.Discrete, body.collisionDetectionMode);
                Assert.IsFalse(body.isKinematic);
                WordboundBodyMotion.Apply(body, true, false);
                Assert.AreEqual(CollisionDetectionMode.Discrete, body.collisionDetectionMode);
                Assert.IsTrue(body.isKinematic);
            }
            finally { Object.DestroyImmediate(obj); }
        }
    }
}
