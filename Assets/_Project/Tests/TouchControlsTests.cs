using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    public class TouchControlsTests
    {
        TouchBinding touch;

        [SetUp]
        public void SetUp()
        {
            touch = TouchBinding.Shared;
            touch.ReleaseAll();
            touch.Enabled = true;
            touch.OverlayDesktop = false;
            touch.OverlayBindingId = null;
        }

        [TearDown]
        public void TearDown()
        {
            touch.ReleaseAll();
            touch.Enabled = touch.OverlayDesktop = false;
            touch.OverlayBindingId = null;
        }

        [Test]
        public void MovementAimAndHeldSkillCoexistWithoutRepeatedPresses()
        {
            touch.SetMove(new Vector2(5f, 5f));
            touch.SetLook(Vector2.left);
            touch.SetHeld(TouchAction.Block, true);
            touch.SetHeld(TouchAction.Attack, true);
            var command = default(PlayerCommands);
            touch.Read(ref command);
            Assert.That(command.move.magnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.AreEqual(Vector2.left, command.look);
            Assert.IsTrue(command.attack);
            Assert.IsTrue(command.blockHeld);
            command = default;
            touch.Read(ref command);
            Assert.IsFalse(command.attack, "A held press must not fire every frame.");
            Assert.IsTrue(command.blockHeld, "Another button must not release the shield.");
            Assert.AreEqual(Vector2.left, command.look);
            touch.SetHeld(TouchAction.Block, false);
            command = default;
            touch.Read(ref command);
            Assert.IsFalse(command.blockHeld);
        }

        [Test]
        public void ReleaseAllClearsBothSticksHeldReviveAndCraftMenu()
        {
            touch.SetMove(Vector2.up);
            touch.SetLook(Vector2.right);
            touch.SetHeld(TouchAction.Grab, true);
            touch.SetHeld(TouchAction.Block, true);
            touch.SetCraftOpen(true);
            touch.ReleaseAll();
            var command = default(PlayerCommands);
            touch.Read(ref command);
            Assert.AreEqual(Vector2.zero, command.move);
            Assert.AreEqual(Vector2.zero, command.look);
            Assert.IsFalse(command.grab || command.grabHeld || command.blockHeld);
            Assert.IsFalse(command.spellDown || command.spellHeld || command.spellUp);
        }

        [Test]
        public void CraftMenuStaysOpenAfterTapAndCancellingDoesNotConfirm()
        {
            touch.SetCraftOpen(true);
            var command = default(PlayerCommands);
            touch.Read(ref command);
            Assert.IsTrue(command.spellDown && command.spellHeld);
            command = default;
            touch.Read(ref command);
            Assert.IsFalse(command.spellDown);
            Assert.IsTrue(command.spellHeld);
            touch.SetCraftOpen(false);
            command = default;
            touch.Read(ref command);
            Assert.IsFalse(command.spellHeld || command.spellUp, "Touch BUILD is explicit; cancelling cannot spend letters.");
        }

        [Test]
        public void TouchOverlayPreservesKeyboardCommandsAndOverridesOnlyActiveSticks()
        {
            var command = new PlayerCommands { move = Vector2.up, attack = true, aimAtPointer = true };
            touch.SetHeld(TouchAction.Jump, true);
            touch.Merge(ref command);
            Assert.AreEqual(Vector2.up, command.move);
            Assert.IsTrue(command.attack && command.jump && command.aimAtPointer);
            touch.SetLook(Vector2.right);
            touch.Merge(ref command);
            Assert.AreEqual(Vector2.right, command.look);
            Assert.IsFalse(command.aimAtPointer);
        }

        [Test]
        public void StartSignalSurvivesThePlayersReadForTheRoundDirector()
        {
            touch.Pulse(TouchAction.Start);
            var command = default(PlayerCommands);
            touch.Read(ref command);
            Assert.IsTrue(command.start);
            Assert.IsTrue(touch.StartPressed());
        }

        [UnityTest]
        public IEnumerator DropRequiresAQuarterSecondHoldAndFiresOnlyOnce()
        {
            touch.SetHeld(TouchAction.Drop, true);
            var command = default(PlayerCommands);
            touch.Read(ref command);
            Assert.IsFalse(command.drop);
            yield return new WaitForSecondsRealtime(InputBinding.DropHoldSeconds + 0.05f);
            command = default;
            touch.Read(ref command);
            Assert.IsTrue(command.drop);
            command = default;
            touch.Read(ref command);
            Assert.IsFalse(command.drop);
            touch.SetHeld(TouchAction.Drop, false);
            touch.Read(ref command);
            touch.SetHeld(TouchAction.Drop, true);
            command = default;
            touch.Read(ref command);
            Assert.IsFalse(command.drop, "A new press starts a new hold.");
        }

        [Test]
        public void SwapHasMatchingDesktopAndScriptedEdges()
        {
            Assert.IsNull(DesktopBinding.Shared.Map.FindAction("Place"));
            Assert.AreEqual("<Keyboard>/1", DesktopBinding.Shared.Hand1.bindings[0].path);
            Assert.AreEqual("<Keyboard>/2", DesktopBinding.Shared.Hand2.bindings[0].path);
            Assert.AreEqual("<Keyboard>/tab", DesktopBinding.Shared.Bag.bindings[0].path);
            Assert.AreEqual("<Keyboard>/escape", DesktopBinding.Shared.Pause.bindings[0].path);
            var scripted = new ScriptedBinding { Next = new PlayerCommands { attack = true, swap = true, slot = 2 } };
            var command = default(PlayerCommands);
            scripted.Read(ref command);
            Assert.IsTrue(command.attack && command.swap && command.slot == 2);
            scripted.Read(ref command);
            Assert.IsFalse(command.attack || command.swap || command.slot != 0);
        }

        [Test]
        public void DisabledTouchCannotIssueCommands()
        {
            touch.Enabled = false;
            touch.SetMove(Vector2.right);
            touch.SetHeld(TouchAction.Attack, true);
            var command = default(PlayerCommands);
            touch.Read(ref command);
            Assert.AreEqual(Vector2.zero, command.move);
            Assert.IsFalse(command.attack);
        }

        [UnityTest]
        public IEnumerator FrozenTypewriterPlayerKeepsDedicatedTouchNavigationAndExit()
        {
            yield return TestScenes.Reset();
            var typewriter = new GameObject("Touch typewriter").AddComponent<Typewriter>();
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, touch);
            var hud = new GameObject("Touch HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            hud.ShowTouchControls(true);
            typewriter.Open(player);
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.IsTrue(player.Frozen);
            var menu = hud.transform.Find("Safe HUD/Typewriter touch menu");
            Assert.IsNotNull(menu);
            Assert.IsTrue(menu.gameObject.activeSelf, "A frozen mode-select player still needs touch controls.");
            Assert.IsNotNull(menu.Find("Previous mode"));
            Assert.IsNotNull(menu.Find("Next mode"));
            Assert.IsNotNull(menu.Find("Choose mode"));
            Assert.IsNotNull(menu.Find("Leave typewriter"));
            Assert.IsNotNull(hud.transform.Find("Safe HUD/Brand").GetComponent<UnityEngine.UI.Button>(), "The brand tile opens the pause card...");
            Assert.IsNotNull(hud.transform.Find("Safe HUD/Pause/Pause card/Pause home"), "...which has the way home.");
            int selected = typewriter.Selected;
            touch.Pulse(TouchAction.Down);
            yield return null;
            yield return null;
            Assert.AreNotEqual(selected, typewriter.Selected, "Menu navigation must read input while the player is frozen.");
            typewriter.Close();
            Object.Destroy(hud.gameObject);
            Object.Destroy(typewriter.gameObject);
            Object.Destroy(player.gameObject);
            yield return TestScenes.Reset();
        }
    }
}
