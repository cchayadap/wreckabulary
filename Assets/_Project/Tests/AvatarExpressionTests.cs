using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Art;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class AvatarExpressionTests
    {
        static AvatarExpressionSample Live(long tick = 1) => new AvatarExpressionSample { Available = true, Health = 100, Tick = tick };

        [Test]
        public void InjuryOverridesFocusAndRecoveryCannotHideDownedState()
        {
            var timeline = new AvatarExpressionTimeline();
            var state = Live(); state.Focus = true; timeline.Observe(state);
            Assert.AreEqual(AvatarExpression.Focus, timeline.State);
            state.Tick++; state.Health = 80; timeline.Observe(state);
            Assert.AreEqual(AvatarExpression.Hurt, timeline.State);
            state.Tick++; state.Downed = true; state.RecoveryGeneration++; timeline.Observe(state);
            Assert.AreEqual(AvatarExpression.Downed, timeline.State);
            state.Tick++; state.Downed = state.Focus = false; state.Health = 100; timeline.Advance(1); timeline.Observe(state);
            Assert.AreEqual(AvatarExpression.Recover, timeline.State);
            timeline.Advance(1); Assert.AreEqual(AvatarExpression.Idle, timeline.State);
        }

        [Test]
        public void FrozenTickVictoryConfirmationIsBoundedAndDuplicateFramesDoNotReplayIt()
        {
            var timeline = new AvatarExpressionTimeline(); var state = Live(30); timeline.Observe(state);
            state.Celebrate = true; timeline.Observe(state);
            Assert.AreEqual(AvatarExpression.Celebrate, timeline.State);
            timeline.Advance(1); timeline.Observe(state); timeline.Advance(1); timeline.Observe(state);
            Assert.AreEqual(AvatarExpression.Idle, timeline.State);
            state.Tick = 29; state.Health = 1; Assert.IsFalse(timeline.Observe(state));
            Assert.AreEqual(AvatarExpression.Idle, timeline.State);
        }

        [Test]
        public void MigrationDisconnectAndNewEpochClearOldInjuryWithoutInventingRecovery()
        {
            var timeline = new AvatarExpressionTimeline(); var state = Live(); timeline.Observe(state);
            state.Tick++; state.Health = 50; timeline.Observe(state); Assert.AreEqual(AvatarExpression.Hurt, timeline.State);
            state.Tick++; state.Available = false; timeline.Observe(state); Assert.IsFalse(timeline.Available);
            state.Epoch++; state.Tick = 0; state.Available = true; state.Health = 20; timeline.Observe(state);
            Assert.IsTrue(timeline.Available); Assert.AreEqual(AvatarExpression.Idle, timeline.State);
            state.Epoch--; state.Tick = 500; state.Downed = true; Assert.IsFalse(timeline.Observe(state));
            Assert.AreEqual(AvatarExpression.Idle, timeline.State);
            timeline.Reset(); Assert.IsFalse(timeline.Available);
        }

        [Test]
        public void DriverOverridesClipWeightsThenRestoresOnlyItsTwoOriginalChannels()
        {
            var root = new GameObject("expression-test"); var mesh = new Mesh(); AvatarExpressionDriver driver = null;
            var profile = ScriptableObject.CreateInstance<AvatarExpressionProfile>();
            try
            {
                var child = new GameObject("SK_Head"); child.transform.SetParent(root.transform);
                var skin = child.AddComponent<SkinnedMeshRenderer>();
                mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }; mesh.triangles = new[] { 0, 1, 2 };
                var delta = new[] { Vector3.zero, Vector3.zero, Vector3.zero };
                mesh.AddBlendShapeFrame("Blink", 100, delta, delta, delta);
                mesh.AddBlendShapeFrame("BrowRelax", 100, delta, delta, delta);
                mesh.AddBlendShapeFrame("Other", 100, delta, delta, delta); skin.sharedMesh = mesh;
                skin.SetBlendShapeWeight(0, 17); skin.SetBlendShapeWeight(1, 23); skin.SetBlendShapeWeight(2, 41);
                driver = new AvatarExpressionDriver(root, profile); Assert.IsTrue(driver.IsSupported);
                driver.Observe(Live()); driver.Step(1);
                Assert.That(skin.GetBlendShapeWeight(1), Is.GreaterThan(50));
                var down = Live(2); down.Downed = true; driver.Observe(down);
                skin.SetBlendShapeWeight(0, 0); skin.SetBlendShapeWeight(1, 100); driver.Step(1);
                Assert.That(skin.GetBlendShapeWeight(0), Is.GreaterThan(70));
                Assert.That(skin.GetBlendShapeWeight(1), Is.LessThan(20));
                Assert.AreEqual(41, skin.GetBlendShapeWeight(2));
                driver.Reset(); Assert.AreEqual(17, skin.GetBlendShapeWeight(0)); Assert.AreEqual(23, skin.GetBlendShapeWeight(1));
                Assert.AreEqual(41, skin.GetBlendShapeWeight(2));
                Assert.AreSame(mesh, skin.sharedMesh);
                driver.Dispose(); driver.Dispose(); Assert.IsFalse(driver.IsSupported);
            }
            finally { driver?.Dispose(); Object.DestroyImmediate(root); Object.DestroyImmediate(mesh); Object.DestroyImmediate(profile); }
        }

        [Test]
        public void ProfileRejectsDuplicateStatesAndNonFiniteWeights()
        {
            var profile = ScriptableObject.CreateInstance<AvatarExpressionProfile>();
            try
            {
                profile.Validate(); profile.Poses[1] = profile.Poses[0];
                Assert.Throws<System.ArgumentException>(() => profile.Validate());
                profile.Poses[1] = AvatarExpressionProfile.Default(AvatarExpression.Moving);
                var pose = profile.Poses[0]; pose.Blink = float.NaN; profile.Poses[0] = pose;
                Assert.Throws<System.ArgumentException>(() => profile.Validate());
            }
            finally { Object.DestroyImmediate(profile); }
        }
    }
}
