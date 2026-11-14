using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class WordboundHoldingPoseTests
    {
        GameObject anchor;
        WordboundVisualProfile profile;

        [SetUp]
        public void SetUp()
        {
            anchor = new GameObject("Held socket test");
            profile = ScriptableObject.CreateInstance<WordboundVisualProfile>();
        }

        [TearDown]
        public void TearDown()
        {
            if (anchor) Object.DestroyImmediate(anchor);
            if (profile) Object.DestroyImmediate(profile);
        }

        [TestCase(true), TestCase(false)]
        public void AuthoredSocketAlignsUnderRotatedScaledParentsAndNonidentitySocket(bool miniature)
        {
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.transform.SetParent(anchor.transform, false);
            Object.DestroyImmediate(model.GetComponent<Collider>());
            model.transform.localRotation = Quaternion.Euler(13, 27, -19);
            model.transform.localScale = new Vector3(1.2f, .8f, .6f);
            var socket = new GameObject("Grip_R").transform;
            socket.SetParent(model.transform, false);
            socket.localPosition = new Vector3(.17f, -.29f, .13f);
            socket.localRotation = Quaternion.Euler(47, -31, 23);
            profile.Overrides.Add(new WordboundVisualOverride { VisualKey = "BAT", Model = model });
            profile.HoldingPose.Items[0].RotationTrim = new Vector3(12, -18, 9);
            anchor.transform.SetPositionAndRotation(new Vector3(3, 2, -4), Quaternion.Euler(17, 39, -21));
            anchor.transform.localScale = new Vector3(1.25f, .8f, 1.4f);
            var hand = new GameObject("hand").transform;
            hand.SetParent(anchor.transform, false);
            hand.SetPositionAndRotation(new Vector3(-2, 4, 3), Quaternion.Euler(-15, 72, 28));
            var resolver = new WordboundVisualResolver(WordboundTestItems.Raw(), profile: profile);
            using var visual = resolver.Create("BAT", new Vector3(.172f, 1.018f, .172f), anchor.transform);
            visual.PlaceAtHand(anchor.transform, hand, miniature);
            var actual = ModelVisual.FindNamed(visual.Model, "Grip_R");
            Assert.That(Vector3.Distance(hand.position, actual.position), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(hand.rotation * Quaternion.Euler(profile.HoldingPose.Items[0].RotationTrim), actual.rotation), Is.LessThan(.02f));
            Assert.That(Vector3.Distance(Vector3.one * (miniature ? visual.HeldScale : 1), visual.Root.transform.localScale), Is.LessThan(.0001f));
            Assert.AreSame(model.GetComponent<MeshFilter>().sharedMesh, visual.Model.GetComponent<MeshFilter>().sharedMesh);
            Assert.AreEqual(new Vector3(.17f, -.29f, .13f), socket.localPosition);
            Assert.That(Quaternion.Angle(Quaternion.Euler(47, -31, 23), socket.localRotation), Is.LessThan(.01f));
        }

        [TestCase("BAT"), TestCase("HAMMER")]
        public void OriginalMeleeMeshesKeepSocketContactAndOrientationThroughEveryCanonicalPhase(string word)
        {
            var item = WordboundTestItems.Raw().Get(word);
            var source = ModelLibrary.Load().Find(item.Model);
            var originals = source.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).ToArray();
            var sourceSocket = ModelVisual.FindNamed(source, "Grip_R");
            var sourcePosition = sourceSocket.localPosition;
            var sourceRotation = sourceSocket.localRotation;
            var resolver = new WordboundVisualResolver(WordboundTestItems.Raw(), profile: profile);
            using var avatar = resolver.Create("player", new Vector3(.6f, 1.8f, .6f), anchor.transform);
            using var held = resolver.Create(word, new Vector3(item.Size[0], item.Size[1], item.Size[2]), anchor.transform);
            using var motion = new WordboundAvatarMotion(avatar);
            motion.Step(.1f, true, true, false); motion.Step(.1f, true, true, false);
            int cycle = WordboundCombatPolicy.CycleTicks(item.Melee);
            int attackEnd = WordboundCombatPolicy.WindupTicks(item.Melee) + WordboundCombatPolicy.ActiveTicks(item.Melee);
            var observe = typeof(WordboundAvatarMotion).GetMethod("ObserveMelee", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int age = 0; age <= cycle; age++)
            {
                var actor = new MatchActorState("actor-a", 100, 100, nextAttackTick: 100 + cycle, equippedSlot: 0,
                    attack: age < attackEnd ? new MatchAttackState(100, "item:1") : null);
                observe.Invoke(motion, new object[] { 0L, 100L + age, actor, "item:1", item, true });
                motion.Step(0, true, true, false);
                held.PlaceAtHand(anchor.transform, avatar.RightHand, true);
                var socket = ModelVisual.FindNamed(held.Model, "Grip_R");
                Assert.That(Vector3.Distance(avatar.RightHand.position, socket.position), Is.LessThan(.0001f), "age=" + age);
                Assert.That(Quaternion.Angle(avatar.RightHand.rotation, socket.rotation), Is.LessThan(.02f), "age=" + age);
                Assert.That(Vector3.Distance(Vector3.one * item.HeldScale, held.Root.transform.localScale), Is.LessThan(.0001f));
            }
            CollectionAssert.AreEqual(originals, held.Model.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).ToArray());
            Assert.AreEqual(sourcePosition, sourceSocket.localPosition);
            Assert.AreEqual(sourceRotation, sourceSocket.localRotation);
        }

        [TestCase("BROOM"), TestCase("SPEAR"), TestCase("SHIELD"), TestCase("PLATE")]
        [TestCase("SOFA"), TestCase("TABLE"), TestCase("BED"), TestCase("STOOL"), TestCase("FAN"), TestCase("CLOCK")]
        public void UnreviewedItemFamiliesRetainTheirRootOrientation(string word)
        {
            Assert.AreEqual(Quaternion.identity, profile.HoldingPose.RootRotationInHand(word, true, Quaternion.Euler(43, 17, 91)));
        }

        [Test]
        public void ExplicitOptOutOrAbsentSocketRetainsExistingPlacement()
        {
            var rotation = Quaternion.Euler(43, 17, 91);
            Assert.AreEqual(Quaternion.identity, profile.HoldingPose.RootRotationInHand("BAT", false, rotation));
            profile.HoldingPose.Items[0].AlignGrip = false;
            Assert.AreEqual(Quaternion.identity, profile.HoldingPose.RootRotationInHand("BAT", true, rotation));
            profile.HoldingPose.Items[0].AlignGrip = true;
            profile.HoldingPose.Enabled = false;
            Assert.AreEqual(Quaternion.identity, profile.HoldingPose.RootRotationInHand("BAT", true, rotation));
        }

        [Test]
        public void InvalidOrDuplicateInspectorTuningFailsValidation()
        {
            profile.HoldingPose.Items[0].RotationTrim = new Vector3(float.NaN, 0, 0);
            Assert.Throws<ArgumentException>(() => profile.Validate());
            profile.HoldingPose.Items[0].RotationTrim = Vector3.zero;
            profile.HoldingPose.Items.Add(new WordboundItemHold { VisualKey = "BAT" });
            Assert.Throws<ArgumentException>(() => profile.Validate());
        }
    }
}
