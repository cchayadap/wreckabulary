using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    public class MouseKeysTests
    {
        InputSettings.EditorInputBehaviorInPlayMode route;
        InputSettings.BackgroundBehavior focus;
        Keyboard keys;
        Mouse mouse;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Test Ground";
            ground.transform.position = Vector3.down * 0.5f;
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            TilePool.Ensure();
            route = InputSystem.settings.editorInputBehaviorInPlayMode;
            focus = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keys = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            keys.MakeCurrent();
            mouse.MakeCurrent();
            KeyBindings.ResetToDefaults();
            DesktopBinding.Typing = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (keys != null && keys.added) InputSystem.RemoveDevice(keys);
            InputSystem.settings.editorInputBehaviorInPlayMode = route;
            InputSystem.settings.backgroundBehavior = focus;
            DesktopBinding.Typing = false;
            SummonedThing.ClearAll();
            World.ClearTransient();
            yield return TestScenes.Reset();
        }

        static PlayerController SpawnDesktop()
        {
            var p = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            p.Setup(0, DesktopBinding.Shared);
            p.Respawn(Vector3.zero);
            p.Inventory.Collects = false;
            return p;
        }

        static HeldWeapon Craft(PlayerController p, string word)
        {
            p.Inventory.Set(word);
            Assert.IsTrue(p.Summoner.Summon(word), word + " is spelled into the hand");
            Assert.IsNotNull(p.Combat.Weapon, word + " is in hand");
            p.FaceTowards(Vector3.forward);
            return p.Combat.Weapon;
        }

        void Left(bool down) => InputSystem.QueueStateEvent(mouse, down ? new MouseState().WithButton(MouseButton.Left) : new MouseState());

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator AQuickLeftClickHits()
        {
            var p = SpawnDesktop();
            yield return new WaitForSeconds(.2f);
            Left(true);
            yield return TestScenes.WaitUntil(() => p.Combat.IsChanneling, 1f, "the press starts a punch at once");
            Left(false);
            Assert.IsFalse(p.Combat.IsAiming, "a fist never aims");
            yield return TestScenes.WaitUntil(() => !p.Combat.IsChanneling, 2f, "the punch finishes");
            yield return new WaitForSeconds(.2f);
            Assert.IsFalse(p.Combat.IsChanneling, "letting go does not swing again");
        }

        [UnityTest]
        public IEnumerator AQuickLeftClickThrowsAThrownItemOnRelease()
        {
            var p = SpawnDesktop();
            yield return new WaitForSeconds(.2f);
            Craft(p, "BALL");
            int thrown = 0;
            p.Combat.Thrown += _ => thrown++;
            Left(true);
            yield return Frames(2);
            Assert.AreEqual(0, thrown, "a thrown item waits for the button to come up");
            Left(false);
            yield return TestScenes.WaitUntil(() => thrown == 1, 2f, "letting go of a quick click throws");
            Assert.IsFalse(p.Combat.IsAiming);
            Assert.IsNull(p.Combat.Weapon, "the BALL left the hand");
        }

        [UnityTest]
        public IEnumerator HoldingLeftClickWithAThrownItemAimsAndLettingGoThrows()
        {
            var p = SpawnDesktop();
            yield return new WaitForSeconds(.2f);
            Craft(p, "BALL");
            int thrown = 0;
            p.Combat.Thrown += _ => thrown++;
            Left(true);
            yield return TestScenes.WaitUntil(() => p.Combat.IsAiming, 2f, "holding the left button aims");
            yield return new WaitForSeconds(.3f);
            Assert.IsTrue(p.Combat.IsAiming, "it keeps aiming while held");
            Assert.AreEqual(0, thrown, "holding never throws");
            Assert.IsNotNull(p.Combat.Weapon);
            Left(false);
            yield return TestScenes.WaitUntil(() => thrown == 1, 2f, "letting go throws");
            yield return null;
            Assert.IsFalse(p.Combat.IsAiming, "the aim ends with the throw");
        }

        [UnityTest]
        public IEnumerator HoldingLeftClickWithAShieldRaisesIt()
        {
            var p = SpawnDesktop();
            yield return new WaitForSeconds(.2f);
            var plate = Craft(p, "PLATE");
            Assert.IsNotNull(plate.Shield, "PLATE is a shield");
            Left(true);
            yield return TestScenes.WaitUntil(() => p.Combat.IsBlocking, 2f, "holding the left button raises the shield");
            Assert.IsFalse(p.Combat.IsAiming, "a shield never aims");
            Left(false);
            yield return TestScenes.WaitUntil(() => !p.Combat.IsBlocking, 2f, "letting go lowers it");
            Assert.AreSame(plate, p.Combat.Weapon, "the shield stays in hand");
        }

        [UnityTest]
        public IEnumerator TheRightButtonNoLongerAimsInHouseModes()
        {
            var p = SpawnDesktop();
            yield return new WaitForSeconds(.2f);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
            yield return TestScenes.WaitUntil(() => p.Summoner.IsSpelling, 2f, "the right button opens the spell box");
            Assert.IsFalse(p.Commands.aimHeld, "house modes never aim with the right button");
            p.Summoner.Close();
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return Frames(2);
            var course = default(PlayerCommands);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
            yield return Frames(2);
            DesktopBinding.Shared.Read(ref course);
            Assert.IsTrue(course.aimHeld, "the course read still aims with the right button");
            Assert.IsFalse(course.spellHeld, "and courses keep their own spell key");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return Frames(2);
        }
    }
}
