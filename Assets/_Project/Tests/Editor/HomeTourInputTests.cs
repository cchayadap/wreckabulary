using NUnit.Framework;
using UnityEngine;

namespace Wreckabulary.Tests
{
    public class HomeTourInputTests
    {
        bool enabled, overlay;
        string overlayId;
        [SetUp] public void SaveTouchState()
        {
            enabled=TouchBinding.Shared.Enabled; overlay=TouchBinding.Shared.OverlayDesktop; overlayId=TouchBinding.Shared.OverlayBindingId;
            TouchBinding.Shared.ReleaseAll(); TouchBinding.Shared.Enabled=false;
        }
        [TearDown] public void RestoreTouchState()
        {
            TouchBinding.Shared.ReleaseAll(); TouchBinding.Shared.Enabled=enabled;
            TouchBinding.Shared.OverlayDesktop=overlay; TouchBinding.Shared.OverlayBindingId=overlayId;
        }
        [Test] public void OriginalCombatCommandsCannotReachAPeacefulPlayer()
        {
            var source=new FullBinding(); var binding=HomeTourDirector.CreateBinding(source); var commands=default(PlayerCommands);
            binding.Read(ref commands);
            Assert.AreNotEqual(source.Id,binding.Id);
            Assert.AreEqual(new Vector2(.25f,.5f),commands.move); Assert.AreEqual(new Vector2(.5f,0),commands.look);
            Assert.IsTrue(commands.jump); Assert.IsTrue(commands.aimAtPointer); Assert.AreEqual(new Vector2(30,40),commands.pointer);
            AssertPeaceful(commands); Assert.IsFalse(binding.StartPressed()); Assert.IsFalse(binding.JoinPressed());
        }
        [Test] public void DesktopTouchMovementIsMergedBeforeCombatIsFilteredAndCannotMergeAgain()
        {
            var source=new FullBinding(); var binding=HomeTourDirector.CreateBinding(source);
            TouchBinding.Shared.Enabled=true; TouchBinding.Shared.OverlayBindingId=source.Id;
            TouchBinding.Shared.SetMove(new Vector2(-.5f,.75f)); TouchBinding.Shared.Pulse(TouchAction.Dodge);
            TouchBinding.Shared.Pulse(TouchAction.Attack); TouchBinding.Shared.SetCraftOpen(true);
            var commands=default(PlayerCommands); binding.Read(ref commands);
            Assert.AreEqual(new Vector2(-.5f,.75f),commands.move); AssertPeaceful(commands);
            Assert.IsFalse(TouchBinding.Shared.IsOverlayFor(binding.Id),"PlayerController cannot append unfiltered overlay commands");
        }
        [Test] public void ADirectTouchSourceStillWalksAndJumpsWithoutCraftOrDodge()
        {
            TouchBinding.Shared.Enabled=true; TouchBinding.Shared.SetMove(Vector2.up);
            TouchBinding.Shared.Pulse(TouchAction.Jump); TouchBinding.Shared.Pulse(TouchAction.Dodge); TouchBinding.Shared.SetCraftOpen(true);
            var binding=HomeTourDirector.CreateBinding(TouchBinding.Shared); var commands=default(PlayerCommands); binding.Read(ref commands);
            Assert.AreEqual(Vector2.up,commands.move); Assert.IsTrue(commands.jump); AssertPeaceful(commands);
        }
        [Test] public void TheTourLooksWithItsSource()
        {
            var source=new LookingBinding(); var binding=HomeTourDirector.CreateBinding(source); var commands=default(PlayerCommands);
            binding.Read(ref commands);
            Assert.IsTrue(binding.CanLook); Assert.IsTrue(binding.ReadsMouse);
            Assert.AreEqual(new Vector2(.1f,-.05f),commands.lookDelta);
            Assert.IsFalse(HomeTourDirector.CreateBinding(new FullBinding()).CanLook,"a source that can't look keeps the overhead tour");
        }
        static void AssertPeaceful(PlayerCommands c)
        {
            Assert.IsFalse(c.attack || c.grab || c.grabHeld || c.blockHeld || c.dodge || c.drop || c.swap);
            Assert.IsFalse(c.spellDown || c.spellHeld || c.spellUp || c.up || c.down || c.start);
        }
        sealed class LookingBinding : InputBinding
        {
            public override string Id=>"test-look";
            public override bool CanLook=>true;
            public override bool ReadsMouse=>true;
            public override void Read(ref PlayerCommands c)=>c.lookDelta=new Vector2(.1f,-.05f);
            public override bool JoinPressed()=>false;
            public override bool StartPressed()=>false;
        }
        sealed class FullBinding : InputBinding
        {
            public override string Id=>"test-source";
            public override void Read(ref PlayerCommands c)
            {
                c.move=new Vector2(.25f,.5f); c.look=new Vector2(.5f,0); c.pointer=new Vector2(30,40); c.aimAtPointer=true;
                c.attack=c.grab=c.grabHeld=c.blockHeld=c.jump=c.dodge=c.drop=c.swap=true;
                c.spellDown=c.spellHeld=c.spellUp=c.up=c.down=c.start=true;
            }
            public override bool JoinPressed()=>true;
            public override bool StartPressed()=>true;
        }
    }
}
