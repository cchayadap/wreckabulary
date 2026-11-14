using System;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class WordboundDefensePoseTests
    {
        [TestCase("SHIELD", 0f)]
        [TestCase("PLATE", 90f)]
        public void DefaultsKeepGripContactAndPresentTheEquipmentFace(string key, float pitch)
        {
            var pose = new WordboundDefensePose();
            Assert.IsTrue(pose.TryGet(key, out var offset, out var euler));
            Assert.AreEqual(Vector3.zero, offset);
            Assert.AreEqual(new Vector3(pitch, 0, 0), euler);
            pose.Validate();
        }

        [TestCase(0f)]
        [TestCase(.5f)]
        [TestCase(1f)]
        public void EveryBlendPivotsAroundTheAuthoredGripWithoutChangingScale(float blend)
        {
            var actor = new GameObject("Defense pose actor");
            var hand = new GameObject("Defense pose hand");
            var item = new GameObject("Defense pose item");
            try
            {
                actor.transform.SetPositionAndRotation(new Vector3(4, 2, 6), Quaternion.Euler(0, 65, 0));
                hand.transform.SetPositionAndRotation(new Vector3(4.3f, 3.1f, 6.2f), Quaternion.Euler(20, 30, 15));
                item.transform.localScale = Vector3.one * .6f;
                var grip = new Vector3(.1f, .2f, -.08f);
                WordboundDefensePose.PlaceAroundGrip(item.transform, grip, hand.transform,
                    actor.transform, Vector3.zero, new Vector3(90, 0, 0), blend);
                Assert.That(Vector3.Distance(hand.transform.position, item.transform.TransformPoint(grip)), Is.LessThan(.00001f));
                Assert.AreEqual(Vector3.one * .6f, item.transform.localScale);
                var target = Quaternion.Slerp(hand.transform.rotation, actor.transform.rotation * Quaternion.Euler(90, 0, 0), blend);
                Assert.That(Quaternion.Angle(target, item.transform.rotation), Is.LessThan(.001f));
                Assert.AreEqual(0, item.GetComponentsInChildren<Collider>().Length);
            }
            finally { Object.DestroyImmediate(item); Object.DestroyImmediate(hand); Object.DestroyImmediate(actor); }
        }

        [TestCase(9, false)]
        [TestCase(10, true)]
        [TestCase(39, true)]
        [TestCase(40, false)]
        public void CanonicalLeaseControlsRaiseAndLowerAtTheExactBoundary(long tick, bool expected)
        {
            var defense = new MatchDefenseState("item:1", 10, 40);
            Assert.AreEqual(expected, WordboundDefensePose.IsActive(defense, "item:1", tick));
            Assert.IsFalse(WordboundDefensePose.IsActive(null, "item:1", tick));
            Assert.IsFalse(WordboundDefensePose.IsActive(defense, "item:2", tick));
        }

        [Test]
        public void UnrelatedItemsDoNotReceiveShieldPoses()
        {
            var pose = new WordboundDefensePose();
            foreach (var key in new[] { "BAT", "SOFA", "keepsake", "player", "" })
                Assert.IsFalse(pose.TryGet(key, out _, out _));
        }

        [Test]
        public void InvalidPersistentPoseValuesFailBeforePresentation()
        {
            var pose = new WordboundDefensePose { ShieldOffset = new Vector3(.251f, 0, 0) };
            Assert.Throws<ArgumentException>(() => pose.Validate());
            pose = new WordboundDefensePose { PlateEuler = new Vector3(float.NaN, 0, 0) };
            Assert.Throws<ArgumentException>(() => pose.Validate());
            pose = new WordboundDefensePose { BlendSeconds = float.PositiveInfinity };
            Assert.Throws<ArgumentException>(() => pose.Validate());
        }
    }
}
