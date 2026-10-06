using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    [Explicit, Category("Capture")]
    public sealed class BatAttachmentTests
    {
        readonly CaptureTests shaders = new CaptureTests();
        BatAttachmentProbeRecorder recorder;
        PlayerController player;
        PlayerAppearance appearance;
        ScriptedBinding input;
        [TearDown] public void Restore() { try { if (recorder) recorder.Finish(); } finally { shaders.RestoreShaderCompilation(); } }
        [UnityTearDown] public IEnumerator Reset() { SummonedThing.ClearAll(); World.ClearTransient(); yield return TestScenes.Reset(); }

        IEnumerator JoinedPlayer()
        {
            yield return TestScenes.Reset(); yield return TestScenes.Load(Session.DibsScene);
            input = new ScriptedBinding();
            player = UnityEngine.Object.FindFirstObjectByType<PlayerJoinManager>().Join(input);
            appearance = player.GetComponent<PlayerAppearance>();
            yield return TestScenes.WaitUntil(() => player.CanAct && player.Grounded && appearance.IsAnimationReady, 15, "native production player ready");
            yield return null;
        }
        HeldWeapon NewBat(RigidbodyInterpolation mode)
        {
            var bat = CatalogGear.Create(GameConfig.Current.Items.Get("BAT"));
            bat.GetComponent<Rigidbody>().interpolation = mode;
            Assert.IsTrue(player.Combat.TryEquip(bat));
            Assert.AreEqual(RigidbodyInterpolation.None, bat.GetComponent<Rigidbody>().interpolation);
            return bat;
        }

        [UnityTest] public IEnumerator BatTracksAnimatedHandAtIdleWalkStopAndTurns() => shaders.RunWithSynchronousShaders(Motion());
        IEnumerator Motion()
        {
            Assert.AreNotEqual(GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType);
            yield return JoinedPlayer(); yield return new WaitForSeconds(4.2f);
            var playerInterpolation = player.Body.interpolation;
            var bat = NewBat(RigidbodyInterpolation.Interpolate);
            yield return TestScenes.WaitUntil(() => appearance.CurrentAnimationClip && appearance.CurrentAnimationClip.name == "Hold_OneHand", 3, "normal pickup/hold");
            string root = Environment.GetEnvironmentVariable("WRECK_CAPTURE_DIR");
            Assert.IsFalse(string.IsNullOrEmpty(root));
            string directory = Path.Combine(root,"motion");
            Assert.IsFalse(Directory.Exists(directory)); Directory.CreateDirectory(directory);
            recorder = new GameObject("QA settled BAT attachment observer").AddComponent<BatAttachmentProbeRecorder>();
            recorder.Initialize(Camera.main, player, appearance, directory);
            recorder.Phase = "warmup-new-hold-pose"; yield return new WaitForSeconds(.5f);
            recorder.Phase = "hierarchy-owned-idle"; yield return new WaitForSeconds(.6f);
            var start = player.transform.position;
            recorder.Phase = "hierarchy-owned-walk"; input.Next.move = Vector2.right*.2f; yield return new WaitForSeconds(1f);
            Assert.Greater(new Vector2(player.Body.linearVelocity.x,player.Body.linearVelocity.z).magnitude,.2f);
            recorder.Phase = "hierarchy-owned-stop"; input.Next.move = Vector2.zero; yield return new WaitForSeconds(.6f);
            Assert.Less(new Vector2(player.Body.linearVelocity.x,player.Body.linearVelocity.z).magnitude,.2f);
            Assert.Greater(Vector3.Distance(start,player.transform.position),.5f);
            recorder.Phase = "hierarchy-owned-turn-back"; input.Next.look = Vector2.down; yield return new WaitForSeconds(.6f);
            Assert.Less(Vector3.Angle(player.Facing,Vector3.back),3);
            recorder.Phase = "hierarchy-owned-turn-forward"; input.Next.look = Vector2.up; yield return new WaitForSeconds(.6f);
            Assert.Less(Vector3.Angle(player.Facing,Vector3.forward),3); input.Next.look = Vector2.zero;
            recorder.Finish();
            Assert.IsNull(recorder.Failure,recorder.Failure);
            Assert.GreaterOrEqual(recorder.HierarchySamples,20);
            foreach(string phase in new[]{"idle","walk","stop","turn-back","turn-forward"})
                Assert.Greater(recorder.PhaseSamples["hierarchy-owned-"+phase],0,"observed phase "+phase);
            Assert.Less(recorder.MaximumHierarchyGap,.005f,"native-hand anchor within 5mm across settled idle/walk/stop/turn");
            Assert.Less(recorder.MaximumHierarchyAngle,.25f,"authored proxy/hold orientation within .25 degrees");
            Assert.Less(recorder.MaximumPreRenderHierarchyGap,.005f);
            Assert.Less(recorder.MaximumPreRenderHierarchyAngle,.25f);
            Assert.AreSame(player.handR,bat.transform.parent);
            Assert.AreEqual(RigidbodyInterpolation.None,bat.GetComponent<Rigidbody>().interpolation);
            Assert.AreEqual(playerInterpolation,player.Body.interpolation);
            Assert.AreEqual(AnimatorCullingMode.CullUpdateTransforms,appearance.AvatarModel.GetComponentInChildren<Animator>(true).cullingMode);
        }

        [UnityTest] public IEnumerator NewPickupAndFirstHoldPoseRemainAlignedBeforeRender() => shaders.RunWithSynchronousShaders(Transition());
        IEnumerator Transition()
        {
            Assert.AreNotEqual(GraphicsDeviceType.Null,SystemInfo.graphicsDeviceType);
            yield return JoinedPlayer(); yield return new WaitForSeconds(4.2f);
            string root = Environment.GetEnvironmentVariable("WRECK_CAPTURE_DIR"); Assert.IsFalse(string.IsNullOrEmpty(root));
            string directory = Path.Combine(root,"transition"); Assert.IsFalse(Directory.Exists(directory)); Directory.CreateDirectory(directory);
            recorder = new GameObject("QA pickup/first-hold pose observer").AddComponent<BatAttachmentProbeRecorder>();
            recorder.Initialize(Camera.main,player,appearance,directory,true);
            Assert.IsTrue(recorder.SoleCalibrationReady,"fixed native sole bands calibrated from Idle before pickup");
            Assert.GreaterOrEqual(recorder.LeftSoleVertexCount,3);
            Assert.GreaterOrEqual(recorder.RightSoleVertexCount,3);
            Assert.Greater(recorder.IdleRendererCount,0,"all enabled native renderers have calibrated signed Idle scales");
            recorder.Phase = "empty-hand-camera-ready"; yield return new WaitForSeconds(.3f);
            recorder.Phase = "hierarchy-owned-pickup-and-first-hold";
            NewBat(RigidbodyInterpolation.Interpolate);
            yield return TestScenes.WaitUntil(() => appearance.CurrentAnimationClip && appearance.CurrentAnimationClip.name == "Hold_OneHand",3,"first normal hold pose");
            yield return new WaitForSeconds(.2f);
            recorder.Finish();
            Assert.IsNull(recorder.Failure,recorder.Failure);
            Assert.GreaterOrEqual(recorder.HierarchySamples,5);
            Assert.Greater(recorder.ClipSamples["Pickup"],0,"observed actual Pickup frames");
            Assert.Greater(recorder.ClipSamples["Hold_OneHand"],0,"observed first Hold frames");
            Assert.Greater(recorder.PickupSoleSamples,0,"actual Pickup samples with complete floor hits for both fixed sole bands");
            Assert.Greater(recorder.FirstHoldSoleSamples,0,"actual first Hold samples with complete floor hits for both fixed sole bands");
            Assert.That(recorder.MinimumLeftSoleGap,Is.InRange(-.005f,.005f),"left retained sole-band minimum remains within 5mm of actual floor");
            Assert.That(recorder.MaximumLeftSoleGap,Is.InRange(-.005f,.005f),"left retained sole-band maximum remains within 5mm of actual floor");
            Assert.That(recorder.MinimumRightSoleGap,Is.InRange(-.005f,.005f),"right retained sole-band minimum remains within 5mm of actual floor");
            Assert.That(recorder.MaximumRightSoleGap,Is.InRange(-.005f,.005f),"right retained sole-band maximum remains within 5mm of actual floor");
            Assert.That(recorder.MinimumRendererIdleRatio,Is.InRange(.5f,1.5f),"native renderer signed scale remains stable through Pickup and first Hold");
            Assert.That(recorder.MaximumRendererIdleRatio,Is.InRange(.5f,1.5f));
            Assert.LessOrEqual(recorder.MaximumRendererIdleDeviation,.5f);
            Assert.Less(recorder.MaximumHierarchyGap,.005f,"pickup and first hold frame keep the anchor at the evaluated native hand");
            Assert.Less(recorder.MaximumHierarchyAngle,.25f,"pickup and first hold retain authored socket orientation");
            Assert.Less(recorder.MaximumPreRenderHierarchyGap,.005f,"native anchor aligned before the render request");
            Assert.Less(recorder.MaximumPreRenderHierarchyAngle,.25f,"authored orientation aligned before the render request");
        }

        [UnityTest] public IEnumerator NativeVisualSpringStaysBoundedAcrossRenderingHitches()
        {
            yield return JoinedPlayer();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var displacement = typeof(PlayerController).GetField("squash",flags);
            var velocity = typeof(PlayerController).GetField("squashVel",flags);
            var advance = typeof(PlayerController).GetMethod("AdvanceSquash",flags);
            foreach(float impulse in new[]{-4f,6f,-7f})
                foreach(float elapsed in new[]{1f/120f,.12f,.16f,1f/3f})
                {
                    displacement.SetValue(player,0f); velocity.SetValue(player,impulse);
                    float simulated = 0f;
                    while(simulated<5f)
                    {
                        advance.Invoke(player,new object[]{elapsed}); simulated+=elapsed;
                        float squash = (float)displacement.GetValue(player);
                        Assert.IsFalse(float.IsNaN(squash)||float.IsInfinity(squash));
                        Assert.That(1f+squash,Is.InRange(.5f,1.5f),"native vertical scale under a render hitch");
                        Assert.That(1f-.5f*squash,Is.InRange(.5f,1.5f),"native horizontal scale under a render hitch");
                    }
                    Assert.Less(Mathf.Abs((float)displacement.GetValue(player)),.001f,"spring returns to authored rest scale");
                }
        }

        [UnityTest] public IEnumerator StoredBatRestoresOriginalModeAfterSwitchDropThrowAndRepick()
        {
            yield return JoinedPlayer();
            var first = NewBat(RigidbodyInterpolation.Interpolate);
            var second = NewBat(RigidbodyInterpolation.Extrapolate);
            Assert.IsFalse(first.gameObject.activeSelf);
            Assert.AreEqual(RigidbodyInterpolation.None,first.GetComponent<Rigidbody>().interpolation);
            Assert.IsTrue(player.Combat.SwitchGear()); Assert.AreSame(first,player.Combat.Weapon);
            Assert.IsTrue(player.Combat.SwitchGear()); Assert.AreSame(second,player.Combat.Weapon);
            Assert.IsTrue(player.Combat.SwitchGear()); Assert.AreSame(first,player.Combat.Weapon);
            player.Combat.Drop();
            Assert.AreEqual(RigidbodyInterpolation.Interpolate,first.GetComponent<Rigidbody>().interpolation);
            Assert.IsFalse(first.GetComponent<Rigidbody>().isKinematic);
            Assert.AreSame(World.Transient,first.transform.parent);
            yield return new WaitForFixedUpdate(); yield return null;
            Assert.Greater(Vector3.Distance(first.transform.position,player.handR.position),.1f,"released BAT is no longer bound to hand");
            first.GetComponent<Rigidbody>().interpolation = RigidbodyInterpolation.Extrapolate;
            Assert.IsTrue(player.Combat.TryEquip(first));
            Assert.AreEqual(RigidbodyInterpolation.None,first.GetComponent<Rigidbody>().interpolation);
            player.Combat.Throw();
            Assert.AreEqual(RigidbodyInterpolation.Extrapolate,first.GetComponent<Rigidbody>().interpolation);
            Assert.IsFalse(first.GetComponent<Rigidbody>().isKinematic);
            Assert.AreSame(World.Transient,first.transform.parent);
            yield return new WaitForFixedUpdate(); yield return null;
            Assert.Greater(first.GetComponent<Rigidbody>().linearVelocity.sqrMagnitude,1f);
            Assert.IsTrue(player.Combat.SwitchGear()); Assert.AreSame(second,player.Combat.Weapon);
            player.Combat.Drop();
            Assert.AreEqual(RigidbodyInterpolation.Extrapolate,second.GetComponent<Rigidbody>().interpolation);
            first.OnReleased(); Assert.AreEqual(RigidbodyInterpolation.Extrapolate,first.GetComponent<Rigidbody>().interpolation);
        }

        [UnityTest] public IEnumerator KnockoutRestoresBothHeldAndStoredBatModes()
        {
            yield return JoinedPlayer();
            var first = NewBat(RigidbodyInterpolation.None);
            var second = NewBat(RigidbodyInterpolation.Interpolate);
            player.Health.Eliminate();
            Assert.IsFalse(player.Combat.IsHolding);
            foreach(var bat in new[]{first,second})
            {
                Assert.IsTrue(bat.gameObject.activeSelf);
                Assert.IsFalse(bat.GetComponent<Rigidbody>().isKinematic);
                Assert.AreSame(World.Transient,bat.transform.parent);
            }
            Assert.AreEqual(RigidbodyInterpolation.None,first.GetComponent<Rigidbody>().interpolation);
            Assert.AreEqual(RigidbodyInterpolation.Interpolate,second.GetComponent<Rigidbody>().interpolation);
            yield return new WaitForFixedUpdate();
        }
    }
}
