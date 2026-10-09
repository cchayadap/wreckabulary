using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class ShopCollectionsTests
    {
        static readonly string[] Keys = { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey, LobbyThemes.PreferenceKey };
        string[] saved;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saved = Keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            yield return TestScenes.Reset();
            foreach (string key in Keys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.SetString(MatchTally.CareerKey, new Career { Coins = 1000 }.Serialize());
            LobbyThemes.Restore();
            MatchTally.LastResult = null;
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.WaitUntil(() => LobbyMenu.Instance && !GameHud.Active.StateController.IsTransitioning, 2f, "lobby ready");
            LobbyMenu.Instance.Open(LobbyMenu.Shop);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.Reset();
            for (int i = 0; i < Keys.Length; i++)
            {
                if (saved[i] == null) PlayerPrefs.DeleteKey(Keys[i]);
                else PlayerPrefs.SetString(Keys[i], saved[i]);
            }
            PlayerPrefs.Save();
            LobbyThemes.Restore();
        }

        static void Click(string name)
        {
            var button = LobbyMenu.Instance.GetComponentsInChildren<Button>().FirstOrDefault(candidate => candidate.name == name);
            Assert.IsNotNull(button, name);
            Assert.IsTrue(button.interactable, name);
            button.onClick.Invoke();
        }

        static IEnumerator WaitForParty(LobbyMenu menu, System.Func<bool> ready, string action, System.Func<string> diagnostics = null)
        {
            float until = Time.realtimeSinceStartup + 2f;
            while (!ready() && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(ready(), action + "; seats=" + string.Join(",", menu.Party.Select(binding => binding.Id)) +
                "; page=" + menu.Current + "; blocked=" + KeyBindings.GameplayBlocked + "; starting=" + menu.Starting +
                "; keyboard=" + Keyboard.current?.deviceId + "; state=" + GameHud.Active.StateController.CurrentState +
                (diagnostics == null ? "" : "\n" + diagnostics()));
        }

        [UnityTest]
        public IEnumerator LobbyControlsBlockDirectPartyPollingAndReleaseJoiningAfterClose()
        {
            var route = InputSystem.settings.editorInputBehaviorInPlayMode;
            var focus = InputSystem.settings.backgroundBehavior;
            Keyboard keyboard = null;
            Gamepad seatedPad = null, joiningPad = null;
            var observations = new System.Collections.Generic.List<string>();
            bool observeKeyboard = false;
            System.Action observeInput = () =>
            {
                if (!observeKeyboard || observations.Count >= 8 || InputState.currentUpdateType != InputUpdateType.Dynamic) return;
                var lobby = LobbyMenu.Instance;
                observations.Add("frame=" + Time.frameCount + ", enabled=" + keyboard.enabled + ", held=" + keyboard.periodKey.isPressed +
                    ", press=" + keyboard.periodKey.wasPressedThisFrame + ", join=" + new KeyboardBinding(KeyboardBinding.Side.Right).JoinPressed() +
                    ", blocked=" + KeyBindings.GameplayBlocked + ", listening=" + KeyBindings.Listening +
                    ", current=" + Keyboard.current?.deviceId + ", menuActive=" + (lobby && lobby.isActiveAndEnabled) +
                    ", seats=" + string.Join(",", lobby.Party.Select(binding => binding.Id)));
            };
            InputSystem.onAfterUpdate += observeInput;
            try
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                // Device creation applies the active focus policy; configure it before adding the test keyboard.
                keyboard = InputSystem.AddDevice<Keyboard>();
                seatedPad = InputSystem.AddDevice<Gamepad>();
                joiningPad = InputSystem.AddDevice<Gamepad>();
                Assert.IsTrue(keyboard.enabled, "synthetic keyboard accepts game input before events are queued");
                keyboard.MakeCurrent();
                var menu = LobbyMenu.Instance;
                menu.Open(LobbyMenu.Home);
                InputSystem.QueueStateEvent(seatedPad, new GamepadState().WithButton(GamepadButton.Start));
                yield return TestScenes.WaitUntil(() => menu.Party.Count == 1, 1f, "initial controller joins through LobbyMenu.PollParty");
                string seatedId = new GamepadBinding(seatedPad).Id;
                Assert.AreEqual(seatedId, menu.Party[0].Id);
                InputSystem.QueueStateEvent(seatedPad, new GamepadState());
                yield return null;

                menu.Open(LobbyMenu.Settings);
                menu.Page<SettingsPage>().ShowTab("controls");
                yield return null;
                Assert.IsTrue(KeyBindings.GameplayBlocked);
                var before = menu.Party.Select(binding => binding.Id).ToArray();
                InputSystem.QueueStateEvent(seatedPad, new GamepadState().WithButton(GamepadButton.Select));
                InputSystem.QueueStateEvent(joiningPad, new GamepadState().WithButton(GamepadButton.Start).WithButton(GamepadButton.RightShoulder));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Period));
                yield return null; yield return null;
                CollectionAssert.AreEqual(before, menu.Party.Select(binding => binding.Id).ToArray(),
                    "Start cannot join, Select cannot leave, and the second keyboard cannot join while Controls owns input");
                Assert.AreEqual(LobbyMenu.Settings, menu.Current, "shoulders cannot switch the page under the controls editor");
                Assert.IsTrue(menu.GetComponentInChildren<ControlsPanel>().isActiveAndEnabled);

                InputSystem.QueueStateEvent(seatedPad, new GamepadState());
                InputSystem.QueueStateEvent(joiningPad, new GamepadState());
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.IsFalse(seatedPad.selectButton.isPressed);
                Assert.IsFalse(joiningPad.startButton.isPressed);
                Assert.IsFalse(keyboard.periodKey.isPressed);
                menu.Close();
                yield return null; yield return null; yield return null;
                Assert.IsFalse(KeyBindings.GameplayBlocked);
                CollectionAssert.AreEqual(before, menu.Party.Select(binding => binding.Id).ToArray(), "closing Controls preserves the existing seat");
                string joiningId = new GamepadBinding(joiningPad).Id;
                InputSystem.QueueStateEvent(joiningPad, new GamepadState().WithButton(GamepadButton.Start));
                yield return WaitForParty(menu, () => menu.Party.Any(binding => binding.Id == joiningId), "controller joins after Controls closes");
                InputSystem.QueueStateEvent(joiningPad, new GamepadState());
                yield return null;
                observeKeyboard = true;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Period));
                yield return null;
                Assert.IsTrue(keyboard.periodKey.isPressed, "the queued physical Period key reaches the game keyboard\n" + string.Join("\n", observations));
                yield return WaitForParty(menu, () => menu.Party.Count == 3, "right keyboard joins after Controls closes",
                    () => string.Join("\n", observations));
                CollectionAssert.AreEquivalent(new[] { seatedId, joiningId,
                    new KeyboardBinding(KeyboardBinding.Side.Right).Id }, menu.Party.Select(binding => binding.Id).ToArray());
                InputSystem.QueueStateEvent(seatedPad, new GamepadState().WithButton(GamepadButton.Select));
                yield return TestScenes.WaitUntil(() => menu.Party.All(binding => binding.Id != seatedId), 1f,
                    "the existing controller can leave after Controls closes");
            }
            finally
            {
                InputSystem.onAfterUpdate -= observeInput;
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (seatedPad != null && seatedPad.added) InputSystem.RemoveDevice(seatedPad);
                if (joiningPad != null && joiningPad.added) InputSystem.RemoveDevice(joiningPad);
                InputSystem.settings.editorInputBehaviorInPlayMode = route;
                InputSystem.settings.backgroundBehavior = focus;
            }
        }

        [UnityTest]
        public IEnumerator ThemePreviewDoesNotPurchaseAndEquipPersistsAcrossLobbyReload()
        {
            var menu = LobbyMenu.Instance;
            string outfit = menu.Outfit.Serialize();
            Click("Offer theme:candy");
            Assert.AreEqual("candy", LobbyThemes.Current.Id);
            Assert.IsFalse(PlayerPrefs.HasKey(LobbyThemes.PreferenceKey));
            Assert.AreEqual(1000, menu.Career.Coins);
            Assert.AreEqual(outfit, menu.Outfit.Serialize());
            menu.Open(LobbyMenu.Home);
            Assert.AreEqual("sunroom", LobbyThemes.Current.Id, "leaving restores the equipped theme");
            menu.Open(LobbyMenu.Shop);
            Click("Buy theme:candy");
            yield return null;
            Click("Wear theme:candy");
            Assert.AreEqual(700, menu.Career.Coins);
            Assert.AreEqual("candy", PlayerPrefs.GetString(LobbyThemes.PreferenceKey));
            Click("Offer theme:lantern");
            Assert.AreEqual("lantern", LobbyThemes.Current.Id);
            menu.Open(LobbyMenu.Home);
            Assert.AreEqual("candy", LobbyThemes.Current.Id);
            yield return TestScenes.Load(Session.HubScene);
            yield return null;
            Assert.AreEqual("candy", LobbyThemes.Current.Id);
            Assert.IsTrue(LobbyMenu.Instance.Career.Owns("theme", "candy"));
            Assert.AreEqual(700, LobbyMenu.Instance.Career.Coins);
        }

        [UnityTest]
        public IEnumerator RecipeSkinUsesNativePreviewRequiresOwnershipAndChangesOnlySelectedGear()
        {
            var menu = LobbyMenu.Instance;
            var shop = menu.Page<ShopPage>();
            var outfit = menu.Outfit.Clone();
            outfit.ItemSkins["BAT"] = "Classic";
            menu.Wear(outfit);
            Assert.IsTrue(shop.SelectRecipe("SHIELD"));
            Click("Offer skin:Arcade");
            Assert.IsNotNull(menu.Stage.ItemPreview, "real native item preview exists");
            Assert.IsTrue(menu.Stage.ItemPreview.GetComponentsInChildren<Renderer>().Any(renderer => renderer.enabled));
            var offer = Career.Shop.First(item => item.Id == "skin:Arcade");
            shop.Wear(offer);
            Assert.IsNull(menu.Outfit.SkinFor("SHIELD"), "locked preview is not an equip bypass");
            Click("Buy skin:Arcade");
            yield return null;
            Click("Wear skin:Arcade");
            Assert.AreEqual("Arcade", menu.Outfit.SkinFor("SHIELD"));
            Assert.AreEqual("Classic", menu.Outfit.SkinFor("BAT"));
            var savedOutfit = Outfit.Deserialize(PlayerPrefs.GetString(LobbyMenu.OutfitKey));
            Assert.AreEqual("Arcade", savedOutfit.SkinFor("SHIELD"));
            Assert.AreEqual("Classic", savedOutfit.SkinFor("BAT"));
            menu.Open(LobbyMenu.Home);
            Assert.IsNull(menu.Stage.ItemPreview, "page exit releases the miniature");
        }

        [UnityTest]
        public IEnumerator LockerShowsMixedRecipesUntilApplyToAllIsExplicitlyChosen()
        {
            var menu = LobbyMenu.Instance;
            Click("Buy skin:Candy");
            yield return null;
            var mixed = menu.Outfit.Clone();
            mixed.ItemSkins.Clear();
            mixed.ItemSkins["BAT"] = "Candy";
            menu.Wear(mixed);
            string before = mixed.Serialize();
            menu.Open(LobbyMenu.Loadout);
            yield return null;
            var locker = menu.Page<LoadoutPage>();
            var state = locker.Root.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Gear finish state");
            Assert.IsTrue(state.text.StartsWith("Mixed recipe finishes"));
            foreach (var card in locker.Root.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Finish ")))
                Assert.IsFalse(card.GetComponentsInChildren<TMP_Text>().Any(text => text.text == "WEARING"), card.name + " is not the uniform finish");
            Assert.AreEqual(before, menu.Outfit.Serialize(), "opening the locker preserves per-recipe choices");
            Click("Finish Candy");
            yield return null;
            Assert.IsTrue(GameConfig.Current.Items.Enabled.All(item => menu.Outfit.SkinFor(item.Id) == "Candy"));
            Assert.AreEqual(menu.Outfit.Serialize(), Outfit.Deserialize(PlayerPrefs.GetString(LobbyMenu.OutfitKey)).Serialize());
            var selected = locker.Root.GetComponentsInChildren<Button>().Single(button => button.name == "Finish Candy");
            Assert.IsTrue(selected.GetComponentsInChildren<TMP_Text>().Any(text => text.text == "WEARING"));
            Assert.AreEqual("All recipes: Candy", locker.Root.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Gear finish state").text);
        }

        [UnityTest]
        public IEnumerator CollectionCheckoutCreditsOwnedPartsAndAccessoriesStayFree()
        {
            var menu = LobbyMenu.Instance;
            Click("Buy colour:bubblegum");
            yield return null;
            Click("Buy look:candy");
            yield return null;
            Assert.AreEqual(600, menu.Career.Coins, "150 owned colour + 250 remaining finish");
            Click("Wear look:candy");
            yield return null;
            Assert.AreEqual("bubblegum", menu.Outfit.ColourOf("Top"));
            Assert.AreEqual("Candy", menu.Outfit.SkinFor("BAT"));
            Assert.IsTrue(GameConfig.Current.Wardrobe.Fits(menu.Outfit));
            Click("Accessory toggle Hood");
            yield return null;
            Assert.AreEqual("Hood", menu.Outfit.PieceIn("Headwear"));
            Assert.AreEqual("Hoodie", menu.Outfit.PieceIn("Top"));
            Assert.AreEqual(600, menu.Career.Coins);
            Click("Accessory toggle Glasses");
            yield return null;
            Assert.IsNull(menu.Outfit.PieceIn("Face"));
            Assert.AreEqual(menu.Outfit.Serialize(), Outfit.Deserialize(PlayerPrefs.GetString(LobbyMenu.OutfitKey)).Serialize());
            Assert.IsTrue(MatchTally.LoadCareer().Owns("look", "candy"));
        }
    }
}
