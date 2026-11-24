using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Wreckabulary.Tests
{
    public class HudParityTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = Vector3.down * 0.5f;
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            TilePool.Ensure();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            TouchBinding.Shared.ReleaseAll();
            TouchBinding.Shared.Enabled = TouchBinding.Shared.OverlayDesktop = false;
            TouchBinding.Shared.OverlayBindingId = null;
            SummonedThing.ClearAll();
            yield return TestScenes.Reset();
        }

        static PlayerController Spawn(InputBinding binding)
        {
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, binding);
            player.Respawn(Vector3.zero);
            player.Inventory.Collects = false;
            return player;
        }

        [UnityTest]
        public IEnumerator HandsKeepTheirSlotsAndOneOrTwoPicksAHand()
        {
            var binding = new ScriptedBinding();
            var player = Spawn(binding);
            yield return null;
            player.Inventory.Set("BATBLADE");
            Assert.IsTrue(player.Summoner.Summon("BAT"));
            var bat = player.Combat.Weapon;
            Assert.AreEqual(0, player.Combat.ActiveSlot);
            Assert.IsTrue(player.Summoner.Summon("BLADE"));
            var blade = player.Combat.Weapon;
            Assert.AreEqual(1, player.Combat.ActiveSlot, "New gear goes in the other hand; the old gear keeps its hand.");
            Assert.AreSame(bat, player.Combat.GearIn(0));
            Assert.AreSame(blade, player.Combat.GearIn(1));

            Assert.IsTrue(player.Combat.SelectSlot(0));
            Assert.AreSame(bat, player.Combat.Weapon);
            Assert.AreSame(blade, player.Combat.GearIn(1), "Switching hands doesn't reorder the slots.");
            Assert.IsFalse(player.Combat.SelectSlot(0), "Picking the hand already in use does nothing.");

            binding.Next.slot = 2;
            yield return null;
            yield return null;
            Assert.AreEqual(1, player.Combat.ActiveSlot);
            Assert.AreSame(blade, player.Combat.Weapon);
            Assert.AreSame(bat, player.Combat.GearIn(0));
        }

        [UnityTest]
        public IEnumerator HudFollowsTheBrowserLayout()
        {
            var touch = TouchBinding.Shared;
            touch.Enabled = true;
            ControlTips.Reset();
            var player = Spawn(touch);
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            hud.ShowTouchControls(false);
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.AreSame(player, hud.LocalPlayer);
            var safe = hud.transform.Find("Safe HUD");

            var tray = safe.Find("Letter bag");
            Assert.IsTrue(tray.gameObject.activeSelf);
            for (int i = 1; i <= 10; i++) Assert.IsNotNull(tray.Find($"Letter {i}"), $"tray cell {i}");
            Assert.IsNull(tray.Find("Letter 11"), "Two rows of five, like the browser bag.");
            player.Inventory.Set("");
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.IsTrue(tray.Find("Letter 10/Face/Outline").GetComponent<Image>().enabled, "An empty cell keeps a dark edge over light floors.");
            var spellGlyph = tray.Find("Spell key/Glyph");
            Assert.IsNull(spellGlyph.GetComponent<RawImage>(), "Q no longer uses the bag glyph that reads as a padlock.");
            Assert.AreEqual("A", spellGlyph.Find("Letter").GetComponent<TMPro.TMP_Text>().text, "Q shows a letter tile.");
            foreach (var shortcut in new[] { "Spell key", "Bag link" })
            {
                var cap = tray.Find(shortcut + "/Key cap").GetComponent<Image>();
                Assert.Less(cap.color.r, .2f, shortcut + " key sits on a dark pill");
                Assert.Greater(cap.color.a, .7f, shortcut);
            }
            Assert.IsNotNull(safe.Find("Side column/Minimap"));
            Assert.IsNotNull(safe.Find("Side column/Timer"));
            Assert.IsNotNull(safe.Find("Side column/Alive/Count"), "A head count sits before the timer, as on the web.");
            Assert.IsNotNull(safe.Find("Side column/Objective"));
            Assert.IsNull(safe.Find("Side column/Pause"), "The brand tile is the pause button.");
            Assert.IsNull(safe.Find("Return home"), "The way home is in the pause card.");
            Assert.IsNotNull(safe.Find("Letter bag/Gear slot 1"));
            Assert.IsNotNull(safe.Find("Letter bag/Gear slot 2"));
            Assert.IsNull(safe.Find("Vitals").GetComponent<Image>(), "Vitals sit straight on the game, with no card.");
            Assert.AreEqual("100", safe.Find("Vitals/Health track/Number").GetComponent<TMPro.TMP_Text>().text, "Alone, health is a bar with its number inside, like a seat card.");
            Assert.IsNull(safe.Find("Vitals/HP"), "No more '+ 100 HP' words.");
            Assert.IsTrue(safe.Find("Desktop controls").gameObject.activeSelf, "A new desktop player gets a control tip.");
            Assert.IsNull(safe.Find("Desktop controls").GetComponent<Image>(), "Tips sit straight on the game, with no box.");
            var tip = safe.Find("Desktop controls/Keys").GetComponent<TMPro.TMP_Text>();
            StringAssert.Contains("move", tip.text, "Moving comes first.");
            ControlTips.Learn(ControlTips.Tip.Move);
            yield return new WaitForSecondsRealtime(0.5f);
            StringAssert.Contains("smash, throw, place, block", tip.text, "Once you've moved, the next tip: left click does it all.");
            StringAssert.DoesNotContain("RMB", tip.text, "Right click aims only in the over-the-shoulder view, so its tip waits.");
            Assert.IsFalse(safe.Find("Touch controls").gameObject.activeSelf, "On-screen buttons are for touch only.");

            safe.Find("Letter bag/Gear slot 2").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(1, player.Combat.ActiveSlot, "Tapping a hand slot picks that hand.");

            Assert.IsFalse(hud.BagOpen);
            tray.Find("Bag link").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(hud.BagOpen, "The bag link pins the bag and map panel.");
            Assert.IsNotNull(safe.Find("Bag panel/House/Map area/Big map"));
            tray.Find("Bag link").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(hud.BagOpen);
            yield return TheBagIsTheWebsDarkGlass(hud, player);

            safe.Find("Brand").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(hud.Paused);
            Assert.AreEqual(0f, Time.timeScale);
            hud.transform.Find("Pause/Pause card/Resume").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(hud.Paused);
            Assert.AreEqual(1f, Time.timeScale);

            hud.ShowTouchControls(true);
            Assert.IsFalse(safe.Find("Desktop controls").gameObject.activeSelf);
            Assert.IsNull(safe.Find("Touch controls/SWAP"), "Touch players tap a hand slot instead of a swap button.");
            Assert.IsNotNull(safe.Find("Touch controls/SPELL"));
            Assert.IsNotNull(safe.Find("Touch controls/HOLD DROP"));
            foreach (Transform child in safe.Find("Touch controls"))
                Assert.IsFalse(child.name.StartsWith("PLACE"), "No separate place button: " + child.name);
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
            ControlTips.Reset();
        }

        [UnityTest]
        public IEnumerator ControllerTrayShowsPadButtonsAndHoldingRbOpensTheBag()
        {
            var route = InputSystem.settings.editorInputBehaviorInPlayMode;
            var focus = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var pad = InputSystem.AddDevice<Gamepad>();
            ControlTips.Reset();
            try
            {
                var binding = new GamepadBinding(pad);
                var player = Spawn(binding);
                var hud = new GameObject("Pad HUD", typeof(Canvas)).AddComponent<GameHud>();
                yield return null; yield return null;
                Assert.AreSame(player, hud.LocalPlayer);
                yield return new WaitForSecondsRealtime(0.9f);
                var tip = hud.transform.Find("Safe HUD/Desktop controls/Keys").GetComponent<TMPro.TMP_Text>();
                Assert.IsTrue(tip.gameObject.activeInHierarchy, "A pad player gets control tips too.");
                Assert.AreEqual(ControlTips.Text(ControlTips.Tip.Move, binding), tip.text);
                StringAssert.DoesNotContain("WASD", tip.text, "One key per device: the pad's own.");
                var tray = hud.transform.Find("Safe HUD/Letter bag");
                Assert.AreEqual("Y", tray.Find("Spell key").GetComponentInChildren<TMPro.TMP_Text>().text);
                Assert.AreEqual("RB", tray.Find("Bag link").GetComponentInChildren<TMPro.TMP_Text>().text);
                Assert.AreEqual("‹", tray.Find("Gear slot 1/Key").GetComponent<TMPro.TMP_Text>().text);
                Assert.AreEqual("›", tray.Find("Gear slot 2/Key").GetComponent<TMPro.TMP_Text>().text);
                Assert.IsFalse(hud.BagOpen);
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.RightShoulder));
                yield return null; yield return null;
                Assert.IsTrue(hud.BagOpen, "Holding RB shows the bag and map.");
                Assert.AreEqual("‹", hud.transform.Find("Safe HUD/Bag panel").GetComponentsInChildren<TMPro.TMP_Text>(true)
                    .First(text => text.name == "Key").text, "The bag's hand cards name the pad buttons too.");
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return null; yield return null;
                Assert.IsFalse(hud.BagOpen);
                Object.Destroy(hud.gameObject);
                Object.Destroy(player.gameObject);
            }
            finally
            {
                if (pad.added) InputSystem.RemoveDevice(pad);
                InputSystem.settings.editorInputBehaviorInPlayMode = route;
                InputSystem.settings.backgroundBehavior = focus;
                ControlTips.Reset();
            }
        }

        [UnityTest]
        public IEnumerator StartPausesForASeatedPadButNeverOverTheResultCard()
        {
            var route = InputSystem.settings.editorInputBehaviorInPlayMode;
            var focus = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var seated = InputSystem.AddDevice<Gamepad>();
            var stranger = InputSystem.AddDevice<Gamepad>();
            try
            {
                var player = Spawn(new GamepadBinding(seated));
                var hud = new GameObject("Pad pause HUD", typeof(Canvas)).AddComponent<GameHud>();
                new GameObject("Back to hub").AddComponent<BackToHub>();
                hud.StateController.FadeDuration = 0f;
                yield return null; yield return null;

                IEnumerator Press(Gamepad pad)
                {
                    InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.Start));
                    yield return null; yield return null;
                    InputSystem.QueueStateEvent(pad, new GamepadState());
                    yield return null; yield return null;
                }

                yield return Press(stranger);
                Assert.IsFalse(hud.Paused, "Start on a pad without a seat joins; it never pauses.");
                yield return Press(seated);
                Assert.IsTrue(hud.Paused, "Start pauses for a seated pad.");
                yield return Press(seated);
                Assert.IsFalse(hud.Paused, "Start again resumes.");
                hud.ShowResult(new HudResult { Round = 1, Heading = "AI 2 called dibs!" }, () => { });
                yield return Press(seated);
                Assert.IsFalse(hud.Paused, "Start over a result card is left to the card.");
                hud.HideResult();
                Object.Destroy(hud.gameObject);
                Object.Destroy(player.gameObject);
            }
            finally
            {
                if (seated.added) InputSystem.RemoveDevice(seated);
                if (stranger.added) InputSystem.RemoveDevice(stranger);
                InputSystem.settings.editorInputBehaviorInPlayMode = route;
                InputSystem.settings.backgroundBehavior = focus;
            }
        }

        static IEnumerator TheBagIsTheWebsDarkGlass(GameHud hud, PlayerController player)
        {
            var safe = hud.transform.Find("Safe HUD");
            var link = safe.Find("Letter bag/Bag link").GetComponent<Button>();
            player.Inventory.Set("BALL");
            link.onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.IsTrue(hud.BagOpen);

            var backdrop = safe.Find("Bag panel/Backdrop").GetComponent<Image>();
            var corners = new Vector3[4];
            backdrop.rectTransform.GetWorldCorners(corners);
            Assert.LessOrEqual(corners[0].x, 0.5f); Assert.LessOrEqual(corners[0].y, 0.5f);
            Assert.GreaterOrEqual(corners[2].x, Screen.width - 0.5f); Assert.GreaterOrEqual(corners[2].y, Screen.height - 0.5f);
            Assert.IsTrue(backdrop.raycastTarget, "Clicks on the scrim don't reach the game.");
            var scrim = backdrop.sprite.texture;
            Assert.AreEqual(.55f, scrim.GetPixel(64, 64).a, .02f, "The web's gradient: light in the middle...");
            Assert.AreEqual(.85f, scrim.GetPixel(0, 0).a, .02f, "...and darker at the edges.");
            Assert.AreEqual(1f, safe.Find("Bag panel").GetComponent<CanvasGroup>().alpha, 1e-3f, "Faded in.");

            foreach (var part in new[] { "Side column", "Letter bag", "Vitals", "Desktop controls", "Player bar" })
            {
                var group = safe.Find(part).GetComponent<CanvasGroup>();
                Assert.AreEqual(0f, group.alpha, part + " steps aside under the bag");
                Assert.IsFalse(group.blocksRaycasts, part);
            }
            Assert.IsTrue(safe.Find("Brand").gameObject.activeInHierarchy, "The brand stays, as on the web.");

            for (int i = 1; i <= 10; i++) Assert.IsNotNull(safe.Find($"Bag panel/Bag/Letters/Big letter {i}"), $"bag cell {i}");
            Assert.IsNull(safe.Find("Bag panel/Bag/Wearing"), "The in-match bag shows no cosmetic slots.");
            var ballTile = safe.Find("Bag panel/Recipe book/View/Cards/BALL/Tile 1");
            Assert.Greater(ballTile.GetComponent<Image>().color.a, .9f, "An owned letter is a filled tile.");
            Assert.IsFalse(ballTile.Find("Rim").GetComponent<Image>().enabled);
            Assert.IsNotNull(safe.Find("Bag panel/Bag/Hands/Bag hand 1/Open hand"), "An empty hand shows an open hand.");
            var map = (RectTransform)safe.Find("Bag panel/House/Map area/Big map");
            Assert.GreaterOrEqual(map.rect.width, 325f, "The house map is the panel's centrepiece.");
            Assert.AreEqual(map.rect.width, map.rect.height, .5f, "and square");

            var cards = safe.Find("Bag panel/Recipe book/View/Cards");
            Assert.AreEqual(GameConfig.Current.Items.All.Count(i => i.Enabled), cards.childCount, "A card per recipe.");
            var ball = cards.Find("BALL").GetComponent<Button>();
            Assert.IsTrue(ball.interactable, "Holding B, A, L, L lights BALL up.");
            Assert.IsTrue(cards.Find("BALL/Glow").GetComponent<Image>().enabled);
            Assert.IsFalse(cards.Find("SOFA").GetComponent<Button>().interactable, "A word you can't spell isn't a button.");
            ball.onClick.Invoke();
            Assert.IsFalse(hud.BagOpen, "Spelling from the book closes the bag, so you see it made.");
            Assert.IsTrue(player.Summoner.IsCrafting);
            Assert.AreEqual("BALL", player.Summoner.CraftWord);
            yield return null;
            foreach (var part in new[] { "Side column", "Letter bag", "Vitals" })
            {
                var group = safe.Find(part).GetComponent<CanvasGroup>();
                Assert.AreEqual(1f, group.alpha, part + " is back");
                Assert.IsTrue(group.blocksRaycasts, part);
            }
            player.Summoner.CancelCraft();

            link.onClick.Invoke();
            Assert.IsTrue(hud.BagOpen);
            safe.Find("Bag panel/Close bag").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(hud.BagOpen, "The x closes it.");
        }

        [UnityTest]
        public IEnumerator RecipeBookSaysWhatTheFocusedItemDoesInOneLine()
        {
            Spawn(TouchBinding.Shared);
            var hud = new GameObject("Recipe line HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            var safe = (RectTransform)hud.transform.Find("Safe HUD");
            safe.Find("Letter bag/Bag link").GetComponent<Button>().onClick.Invoke();
            var book = safe.Find("Bag panel/Recipe book");
            var line = book.Find("Book line").GetComponent<TMPro.TMP_Text>();
            var cards = book.Find("View/Cards");
            Assert.AreEqual("", line.text, "quiet until a card is focused");
            var legend = book.Find("Legend");
            Assert.IsTrue(legend.gameObject.activeSelf, "An idle book explains its stars.");
            Assert.AreEqual(Wreckabulary.Rules.RecipeStars.Legend, legend.Find("Text").GetComponent<TMPro.TMP_Text>().text);
            int Stars(string word) => cards.Find(word).Cast<Transform>().Count(child => child.name == "Star");
            if (GameConfig.Current.Tuning != null)
            {
                Assert.AreEqual(Wreckabulary.Rules.RecipeStars.Count(Wreckabulary.Rules.RecipeDifficulty.TierOf("HAMMER")), Stars("HAMMER"));
                Assert.Greater(Stars("HAMMER"), Stars("BAT"), "A harder word shows more stars.");
                Assert.AreEqual(0, Stars("BAT"));
            }
            var batTile = cards.Find("BAT/Tile 1");
            Assert.AreEqual(0f, batTile.GetComponent<Image>().color.a, .01f, "A missing letter is hollow...");
            Assert.IsTrue(batTile.Find("Rim").GetComponent<Image>().enabled, "...with an outline.");
            UnityEngine.EventSystems.ExecuteEvents.Execute(cards.Find("HAMMER").gameObject,
                new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current), UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
            StringAssert.StartsWith("HAMMER · ", line.text);
            StringAssert.Contains("every 3rd hit crushes", line.text);
            Assert.GreaterOrEqual(line.fontSize, 23f);
            Assert.IsTrue(legend.gameObject.activeSelf, "The star key stays on its own line while a card is focused.");
            UnityEngine.EventSystems.ExecuteEvents.Execute(cards.Find("HAMMER").gameObject,
                new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current), UnityEngine.EventSystems.ExecuteEvents.pointerExitHandler);
            Assert.AreEqual("", line.text);
            UnityEngine.EventSystems.ExecuteEvents.Execute(cards.Find("BAT").gameObject,
                new UnityEngine.EventSystems.BaseEventData(UnityEngine.EventSystems.EventSystem.current), UnityEngine.EventSystems.ExecuteEvents.selectHandler);
            Assert.AreEqual("BAT · no extra effect", line.text, "a pad selection says it too");
            Object.Destroy(hud.gameObject);
        }

        [UnityTest]
        public IEnumerator RecipeBookKeepsItsStarKeyUnderTheTitleAndItsCloseOnTheCorner()
        {
            var you = Spawn(TouchBinding.Shared);
            var hud = new GameObject("Recipe key HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            var safe = (RectTransform)hud.transform.Find("Safe HUD");
            safe.Find("Letter bag/Bag link").GetComponent<Button>().onClick.Invoke();
            yield return TestScenes.WaitUntil(() => hud.BagOpen, 10f, "the bag");
            yield return new WaitForSecondsRealtime(0.12f);
            Canvas.ForceUpdateCanvases();
            var book = (RectTransform)safe.Find("Bag panel/Recipe book");
            var legend = (RectTransform)book.Find("Legend");
            var close = (RectTransform)safe.Find("Bag panel/Close bag");
            Assert.IsNotNull(close, "The x keeps its name and its place in the bag panel.");
            Assert.IsTrue(legend.gameObject.activeSelf, "The star key always shows.");
            float scale = hud.UiCanvas.scaleFactor, slack = 0.5f * scale;
            var bookBox = OnScreen(book);
            var lineBox = OnScreen((RectTransform)book.Find("Book line"));
            var keyBox = OnScreen(legend);
            var closeBox = OnScreen(close);
            Assert.LessOrEqual(keyBox.yMax, lineBox.yMin + slack, "The key sits under the title line.");
            Assert.LessOrEqual(OnScreen((RectTransform)book.Find("View")).yMax, keyBox.yMin + slack, "The recipes start under the key.");
            Assert.AreEqual(bookBox.xMax - GameHud.BookCloseInset * scale, closeBox.xMax, slack, "The x sits on the book's right edge...");
            Assert.AreEqual(bookBox.yMax - GameHud.BookTitleTop * scale, closeBox.yMax, slack, "...at its top, on the title row.");
            Assert.IsFalse(closeBox.Overlaps(keyBox), "The x never covers the key...");
            Assert.IsFalse(closeBox.Overlaps(lineBox), "...nor the title line.");
            var key = legend.Find("Text").GetComponent<TMPro.TMP_Text>();
            Assert.AreEqual(Wreckabulary.Rules.RecipeStars.Legend, key.text);
            Assert.GreaterOrEqual(key.enableAutoSizing ? key.fontSizeMin : key.fontSize, 15f);
            Assert.Less(OnScreen((RectTransform)legend.Find("Star")).xMax, OnScreen(key.rectTransform).xMin + slack, "The star leads the key.");
            close.GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(hud.BagOpen, "The x closes the bag.");
            Object.Destroy(hud.gameObject);
            Object.Destroy(you.gameObject);
        }

        [UnityTest]
        public IEnumerator TheResultCardClearsTheBagHandsAndHealth()
        {
            var you = Spawn(TouchBinding.Shared);
            var hud = new GameObject("Card HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            hud.ShowTouchControls(false);
            var safe = (RectTransform)hud.transform.Find("Safe HUD");
            var tray = safe.Find("Letter bag").gameObject;
            var vitals = safe.Find("Vitals").gameObject;
            yield return TestScenes.WaitUntil(() => hud.AcceptsGameplayInput && tray.activeSelf && vitals.activeSelf, 10f, "the bag, hands and health in play");
            Assert.IsFalse(hud.FullCardShown);
            hud.ShowResult(new HudResult { Round = 1, Heading = "Round over" }, () => { });
            Assert.IsTrue(hud.FullCardShown);
            yield return TestScenes.WaitUntil(() => !tray.activeSelf && !vitals.activeSelf, 10f, "the result card to clear the bag, hands and health");
            Assert.IsFalse(safe.Find("Letter bag/Gear slot 1").gameObject.activeInHierarchy, "The hands go with the bag.");
            hud.HideResult();
            Assert.IsFalse(hud.FullCardShown);
            yield return TestScenes.WaitUntil(() => tray.activeSelf && vitals.activeSelf, 10f, "the bag, hands and health back after the card");
            Object.Destroy(hud.gameObject);
            Object.Destroy(you.gameObject);
        }

        [UnityTest]
        public IEnumerator RecipeBookReflowsWithoutShrinkingLettersOrOverlappingTheMap()
        {
            var player = Spawn(TouchBinding.Shared);
            var hud = new GameObject("Responsive recipe HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            yield return new WaitForSecondsRealtime(.12f);
            player.Inventory.Set("BALL");
            var safe = (RectTransform)hud.transform.Find("Safe HUD");
            safe.Find("Letter bag/Bag link").GetComponent<Button>().onClick.Invoke();
            var book = (RectTransform)safe.Find("Bag panel/Recipe book");
            var view = (RectTransform)book.Find("View");
            var cards = (RectTransform)view.Find("Cards");
            var grid = cards.GetComponent<GridLayoutGroup>();
            var scroll = view.GetComponent<ScrollRect>();
            var mapArea = (RectTransform)safe.Find("Bag panel/House/Map area");
            var map = (RectTransform)mapArea.Find("Big map");

            foreach (var size in new[] { new Vector2(1920f, 1080f), new Vector2(2520f, 1080f), new Vector2(1440f, 1080f) })
            {
                safe.anchorMin = safe.anchorMax = safe.pivot = Vector2.zero;
                safe.anchoredPosition = Vector2.zero;
                safe.sizeDelta = size;
                yield return new WaitForSecondsRealtime(.12f);
                Canvas.ForceUpdateCanvases();
                Assert.AreEqual(size.x >= 1920f ? 4 : 3, grid.constraintCount, size.ToString());
                Assert.GreaterOrEqual(grid.cellSize.x, 188f, "Fewer columns keep the recipe cells readable.");
                Assert.GreaterOrEqual(grid.cellSize.y, 200f);
                Assert.IsTrue(scroll.vertical);
                Assert.IsFalse(scroll.horizontal);
                Assert.Greater(cards.rect.height, view.rect.height, "All recipes remain reachable by scrolling.");
                Assert.GreaterOrEqual(map.rect.width, 325f);
                Assert.AreEqual(map.rect.width, map.rect.height, .5f);
                AssertRectInside(safe, book);
                AssertRectInside(safe, mapArea);
                AssertRectInside(mapArea, map);
                var mapCorners = new Vector3[4]; var bookCorners = new Vector3[4];
                mapArea.GetWorldCorners(mapCorners); book.GetWorldCorners(bookCorners);
                Assert.Less(mapCorners[2].x, bookCorners[0].x, "The map and recipe column have separate space.");

                foreach (Transform card in cards)
                {
                    Assert.GreaterOrEqual(((RectTransform)card.Find("Picture")).rect.width, 112f);
                    foreach (var text in card.GetComponentsInChildren<TMPro.TMP_Text>())
                    {
                        Assert.GreaterOrEqual(text.fontSize, 23f, card.name);
                        AssertRectInside((RectTransform)card, (RectTransform)text.transform.parent);
                    }
                }
                scroll.verticalNormalizedPosition = 0f;
                Canvas.ForceUpdateCanvases();
                AssertRectInside(view, (RectTransform)cards.GetChild(cards.childCount - 1));
                scroll.verticalNormalizedPosition = 1f;
            }
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        static void AssertRectInside(RectTransform outer, RectTransform inner)
        {
            var corners = new Vector3[4];
            inner.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var local = outer.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(outer.rect.xMin - .5f, outer.rect.xMax + .5f), inner.name + " horizontal fit");
                Assert.That(local.y, Is.InRange(outer.rect.yMin - .5f, outer.rect.yMax + .5f), inner.name + " vertical fit");
            }
        }

        [UnityTest]
        public IEnumerator AnnouncementsAreToastsAndTheRoundChipCountsDown()
        {
            var you = Spawn(TouchBinding.Shared);
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            Assert.IsNull(hud.transform.Find("Safe HUD/Title plaque"), "No big title plaque: the web announces with a toast.");
            hud.SetTitle("ROUND 1", "Dibs on the living room");
            Assert.AreEqual("ROUND 1 · Dibs on the living room", hud.ToastText);
            hud.SetTitle("", "");
            Assert.AreEqual("ROUND 1 · Dibs on the living room", hud.ToastText, "No words don't replace a toast.");
            yield return new WaitForSecondsRealtime(3.1f);
            Assert.AreEqual("", hud.ToastText, "A toast goes by itself.");

            hud.ShowCountdown(3, "ROUND 1 · LIVING ROOM");
            Assert.IsTrue(hud.CountdownShown);
            Assert.AreEqual("3", hud.CountdownText);
            hud.ShowCountdown(2, "ROUND 1 · LIVING ROOM");
            Assert.AreEqual("2", hud.CountdownText);
            hud.ShowGo();
            Assert.AreEqual("GO", hud.CountdownText);
            yield return new WaitForSecondsRealtime(1f);
            Assert.IsFalse(hud.CountdownShown, "GO goes a moment later.");

            hud.ShowBanner("Round 1 · Last One Standing", "Knock out everyone else to win the round · First to 3");
            Assert.IsTrue(hud.BannerShown);
            Assert.AreEqual("Round 1 · Last One Standing · Knock out everyone else to win the round · First to 3", hud.BannerText);
            var banner = (RectTransform)hud.transform.Find("Safe HUD/Round banner");
            Assert.GreaterOrEqual(banner.Find("Title").GetComponent<TMPro.TMP_Text>().fontSize, 40f, "The round banner is large.");
            Assert.IsFalse(banner.GetComponent<CanvasGroup>().blocksRaycasts, "It never blocks a click.");
            hud.Toast("Spelling BAT…");
            Assert.AreEqual("", hud.ToastText, "No toast on top of the banner: it says the rule once.");
            hud.ToastAfterBanner("Break furniture");
            Assert.AreEqual("", hud.ToastText, "A first-move tip waits for the banner.");
            yield return new WaitForSecondsRealtime(2.4f);
            Assert.IsTrue(hud.BannerShown, "The banner stays through the countdown and GO.");
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.IsFalse(hud.BannerShown, "The banner goes by itself after 3.2 s.");
            Assert.AreEqual("Break furniture", hud.ToastText, "and the waiting tip follows it.");
            Object.Destroy(hud.gameObject);
            Object.Destroy(you.gameObject);
        }

        [UnityTest]
        public IEnumerator PauseHelpAndResultCardsFollowTheWeb()
        {
            var you = Spawn(TouchBinding.Shared);
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            var safe = hud.transform.Find("Safe HUD");
            safe.Find("Brand").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(hud.Paused);
            var card = hud.transform.Find("Pause/Pause card");
            Assert.AreEqual("The mess can wait.", card.Find("Heading").GetComponent<TMPro.TMP_Text>().text);
            StringAssert.Contains("First to", card.Find("Blurb").GetComponent<TMPro.TMP_Text>().text, "The pause card says what the mode is.");
            var leave = card.Find("Pause home");
            Assert.AreEqual("Leave match", leave.Find("Body/Label").GetComponent<TMPro.TMP_Text>().text);
            leave.GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(hud.LeaveAsked, "Leaving mid-match asks first.");
            Assert.IsTrue(hud.Paused);
            hud.transform.Find("Leave match/Leave card/Keep playing").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(hud.LeaveAsked);
            Assert.IsTrue(hud.Paused, "Keep playing goes back to the pause card.");
            leave.GetComponent<Button>().onClick.Invoke();
            hud.TogglePause();
            Assert.IsFalse(hud.LeaveAsked, "Back or Start closes the question.");
            Assert.IsTrue(hud.Paused);
            card.Find("Pause how to play").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(hud.HelpShown);
            Assert.IsFalse(card.gameObject.activeSelf, "Help takes the pause card's place.");
            StringAssert.Contains(" HP.", hud.transform.Find("Pause/Pause help/Fine print").GetComponent<TMPro.TMP_Text>().text);
            var spellStep = hud.transform.Find("Pause/Pause help/Steps/Step 02/Text").GetComponent<TMPro.TMP_Text>().text;
            StringAssert.Contains("type a word, press Enter", spellStep, "Keyboard players are told to type, not to hold.");
            StringAssert.Contains("click a lit card", spellStep, "...or to use the book.");
            StringAssert.DoesNotContain("craft", spellStep);
            StringAssert.Contains("Harder words carry effects.", hud.transform.Find("Pause/Pause help/Steps/Step 03/Text").GetComponent<TMPro.TMP_Text>().text);
            hud.transform.Find("Pause/Pause help/Help done").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(hud.Paused, "GOT IT. LET'S PLAY. goes straight back to the game.");
            Assert.AreEqual(1f, Time.timeScale);

            int nexts = 0;
            hud.ShowResult(new HudResult { Round = 2, Won = true, Heading = "You called dibs!", Broken = 4, Crafted = 2, Damage = 37 }, () => nexts++);
            Assert.IsTrue(hud.ResultShown);
            Assert.IsTrue(hud.NeedsPointer, "The result card wants the mouse.");
            var result = safe.Find("Result/Result card");
            Assert.AreEqual("ROUND 2 COMPLETE", result.Find("Eyebrow").GetComponent<TMPro.TMP_Text>().text);
            Assert.AreEqual("You called dibs!", result.Find("Heading").GetComponent<TMPro.TMP_Text>().text);
            Assert.AreEqual("4", result.Find("Stats/OBJECTS WRECKED/Number").GetComponent<TMPro.TMP_Text>().text);
            Assert.AreEqual("37", result.Find("Stats/DAMAGE DEALT/Number").GetComponent<TMPro.TMP_Text>().text);
            Assert.IsFalse(result.Find("Reward").gameObject.activeSelf, "A round in the middle of a match pays nothing yet.");
            StringAssert.Contains("NEXT ROUND", result.Find("Next/Body/Label").GetComponent<TMPro.TMP_Text>().text);
            safe.Find("Brand").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(hud.Paused, "No pause over the result card.");
            var next = result.Find("Next").GetComponent<Button>();
            next.onClick.Invoke();
            next.onClick.Invoke();
            Assert.AreEqual(1, nexts, "NEXT ROUND acts once.");
            Assert.IsFalse(hud.ResultShown);

            hud.ShowResult(new HudResult { Final = true, Heading = "One more word. One more chance.",
                Reward = new Wreckabulary.Rules.MatchRecord { Coins = 12, Score = 70 } }, () => nexts++);
            Assert.AreEqual("HOUSE PARTY COMPLETE", result.Find("Eyebrow").GetComponent<TMPro.TMP_Text>().text);
            Assert.IsTrue(result.Find("Reward").gameObject.activeSelf, "The match's pay shows at the end.");
            Assert.AreEqual("+12", result.Find("Reward/Coins").GetComponent<TMPro.TMP_Text>().text);
            StringAssert.Contains("PLAY AGAIN", result.Find("Next/Body/Label").GetComponent<TMPro.TMP_Text>().text);
            hud.HideResult();
            Assert.AreEqual(1, nexts, "Hiding the card isn't a choice.");
            Object.Destroy(hud.gameObject);
            Object.Destroy(you.gameObject);
        }

        [UnityTest]
        public IEnumerator TheResultNamesTheWinnerAndOnlyAMidMatchLeaveAsks()
        {
            var you = Spawn(TouchBinding.Shared);
            var hud = new GameObject("Result HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            var result = hud.transform.Find("Safe HUD/Result/Result card");
            string Text(string path) => result.Find(path).GetComponent<TMPro.TMP_Text>().text;
            int nexts = 0, homes = 0;
            hud.ShowResult(new HudResult { Round = 2, Heading = "AI 3 called dibs!", Eyebrow = "ROUND 2 · AI 3 WINS", Flavour = "2 more round wins to take the house.",
                Winner = "AI 3", Lines = new[] { "You · 1/3 wins · wrecked", "<b>AI 3 · 1/3 wins · 40 HP</b>" } }, () => nexts++, () => homes++);
            Assert.AreEqual("ROUND 2 · AI 3 WINS", Text("Eyebrow"));
            Assert.AreEqual("2 more round wins to take the house.", Text("Flavour"));
            StringAssert.Contains("AI 3 · 1/3 wins · 40 HP", Text("Players"), "One line per seat, bots included.");
            StringAssert.Contains("You · 1/3 wins · wrecked", Text("Players"));
            Assert.AreEqual("Leave match", Text("Result home/Body/Label"));
            Assert.IsTrue(result.Find("Stats").gameObject.activeSelf);
            result.Find("Result home").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(hud.LeaveAsked, "Leaving between rounds asks first.");
            hud.ConfirmResult();
            Assert.AreEqual(0, nexts, "NEXT ROUND waits while the question is open.");
            hud.transform.Find("Leave match/Leave card/Leave").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(1, homes);
            Assert.IsFalse(hud.ResultShown);

            hud.ShowResult(new HudResult { Final = true, Heading = "You're ready!", Eyebrow = "TUTORIAL COMPLETE", NextLabel = "PLAY A MATCH NOW →",
                HomeLabel = "Back to the lobby", Stats = false }, () => nexts++, () => homes++);
            Assert.AreEqual("TUTORIAL COMPLETE", Text("Eyebrow"));
            Assert.AreEqual("PLAY A MATCH NOW →", Text("Next/Body/Label"));
            Assert.AreEqual("Back to the lobby", Text("Result home/Body/Label"));
            Assert.IsFalse(result.Find("Stats").gameObject.activeSelf, "Stats = false hides the stats row.");
            Assert.IsFalse(result.Find("Players").gameObject.activeSelf, "No seat lines without a roster.");
            result.Find("Result home").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(hud.LeaveAsked, "The final card leaves at once.");
            Assert.AreEqual(2, homes);
            Object.Destroy(hud.gameObject);
            Object.Destroy(you.gameObject);
        }

        [UnityTest]
        public IEnumerator TabOrRightClickClosesTheComposer()
        {
            var route = InputSystem.settings.editorInputBehaviorInPlayMode;
            var focus = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keys = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            keys.MakeCurrent();
            mouse.MakeCurrent();
            try
            {
                var player = Spawn(DesktopBinding.Shared);
                var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
                yield return null;
                yield return new WaitForSecondsRealtime(0.12f);
                player.Inventory.Set("BAT");
                player.Summoner.Open();
                yield return null;
                Assert.IsTrue(hud.ComposerOpen);

                InputSystem.QueueStateEvent(keys, new KeyboardState(Key.Tab));
                yield return null;
                yield return null;
                Assert.IsFalse(player.Summoner.IsSpelling, "Tab closes the composer");
                Assert.IsFalse(hud.ComposerOpen);
                Assert.IsFalse(hud.BagOpen, "the Tab that closed it doesn't open the bag");
                Assert.IsTrue(DesktopBinding.Typing, "held keys stay swallowed");
                InputSystem.QueueStateEvent(keys, new KeyboardState());
                yield return null;
                yield return null;
                Assert.IsFalse(DesktopBinding.Typing);

                player.Summoner.Open();
                yield return null;
                Assert.IsTrue(hud.ComposerOpen);
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
                yield return null;
                yield return null;
                Assert.IsFalse(hud.ComposerOpen, "right-click closes it too");
                Assert.IsTrue(DesktopBinding.Typing, "the right-click doesn't reach the player as a block");
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return null;
                yield return null;
                Assert.IsFalse(DesktopBinding.Typing);
                Object.Destroy(hud.gameObject);
                Object.Destroy(player.gameObject);
            }
            finally
            {
                InputSystem.RemoveDevice(keys);
                InputSystem.RemoveDevice(mouse);
                InputSystem.settings.editorInputBehaviorInPlayMode = route;
                InputSystem.settings.backgroundBehavior = focus;
                DesktopBinding.Typing = false;
            }
        }

        [UnityTest]
        public IEnumerator EscapeBacksOutOfTheComposerWithoutPausing()
        {
            var route = InputSystem.settings.editorInputBehaviorInPlayMode;
            var focus = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keys = InputSystem.AddDevice<Keyboard>();
            keys.MakeCurrent();
            try
            {
                var player = Spawn(DesktopBinding.Shared);
                var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
                yield return null;
                yield return new WaitForSecondsRealtime(0.12f);
                player.Inventory.Set("BAT");
                player.Summoner.Open();
                yield return null;
                Assert.IsTrue(hud.ComposerOpen);

                InputSystem.QueueStateEvent(keys, new KeyboardState(Key.Escape));
                yield return null;
                yield return null;
                Assert.IsFalse(player.Summoner.IsSpelling, "Esc backs out of the composer");
                Assert.IsFalse(hud.ComposerOpen);
                Assert.IsFalse(hud.Paused, "the Esc that closed it doesn't open the menu");
                Assert.AreEqual("", hud.ComposerHint);
                InputSystem.QueueStateEvent(keys, new KeyboardState());
                yield return null;
                yield return null;
                Object.Destroy(hud.gameObject);
                Object.Destroy(player.gameObject);
            }
            finally
            {
                InputSystem.RemoveDevice(keys);
                InputSystem.settings.editorInputBehaviorInPlayMode = route;
                InputSystem.settings.backgroundBehavior = focus;
                DesktopBinding.Typing = false;
            }
        }

        [UnityTest]
        public IEnumerator KeyboardPlayersTypeTheirWordLikeTheWeb()
        {
            var player = Spawn(DesktopBinding.Shared);
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.AreSame(player, hud.LocalPlayer);
            player.Inventory.Set("BA");
            player.Summoner.Open();
            yield return null;
            Assert.IsTrue(hud.ComposerOpen, "Spell opens the composer.");
            Assert.IsTrue(DesktopBinding.Typing, "Letters go to the word, not to the player.");
            Assert.IsTrue(hud.ComposerLettersOnly, "Typing shows only the typed letters.");
            StringAssert.Contains("<b>ENTER</b> spell", hud.ComposerHint, "One short line under the letters says how to spell.");
            StringAssert.Contains($"<b>{ControlHints.KeyOf(DesktopBinding.Shared.Pause)}</b> back", hud.ComposerHint, "It also says how to go back.");
            Assert.AreEqual("Type a word you can make from your letters. Tab or right-click closes.", hud.ComposerStatus);
            hud.TypeWord("ba");
            yield return null;
            Assert.AreEqual("Keep going…", hud.ComposerStatus);
            hud.TypeWord("bat");
            yield return null;
            Assert.AreEqual("BAT", hud.ComposerText);
            Assert.AreEqual("BAT needs T. Smash more furniture.", hud.ComposerStatus);
            hud.SubmitComposer();
            yield return null;
            Assert.AreEqual("You still need some letters.", hud.ComposerStatus);
            Assert.IsFalse(player.Summoner.IsCrafting);
            hud.TypeWord("zzz");
            yield return null;
            StringAssert.StartsWith("That isn't a recipe.", hud.ComposerStatus);
            hud.SubmitComposer();
            yield return null;
            Assert.AreEqual("That recipe is not available.", hud.ComposerStatus);

            player.Inventory.Set("BAT");
            hud.TypeWord("bat");
            yield return null;
            Assert.AreEqual("BAT is ready. Press Enter!", hud.ComposerStatus);
            Assert.IsTrue(player.Summoner.IsSpelling, "Typing never spells by itself.");
            hud.SubmitComposer();
            Assert.IsTrue(player.Summoner.IsCrafting, "Enter spells it.");
            Assert.AreEqual("Spelling BAT…", hud.ToastText);
            yield return null;
            Assert.IsFalse(hud.ComposerOpen);
            Assert.IsFalse(DesktopBinding.Typing);
            player.Summoner.CancelCraft();
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
            yield return null;
            Assert.IsFalse(DesktopBinding.Typing);
        }

        [UnityTest]
        public IEnumerator EverySeatGetsACardWithWinsHealthAndReviveMe()
        {
            var touch = TouchBinding.Shared;
            touch.Enabled = true;
            var you = Spawn(touch);
            var mate = Spawn(new ScriptedBinding());
            mate.Name = "P2";
            mate.transform.position = new Vector3(3f, 0f, 0f);
            var bot = Spawn(new BotBinding());
            bot.Name = "AI 3";
            bot.transform.position = new Vector3(-3f, 0f, 0f);
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            hud.SetScoreboard(new[] { you, mate, bot }, p => p == bot ? 2 : 0, 3, true);
            mate.Health.Eliminate();
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.AreSame(you, hud.LocalPlayer);
            var safe = (RectTransform)hud.transform.Find("Safe HUD");
            string Text(string path) => safe.Find("Player bar/" + path).GetComponent<TMPro.TMP_Text>().text;
            Assert.IsNull(safe.Find("Roommate 1"), "One roster only: the old top-left rows are gone.");
            var bar = (RectTransform)safe.Find("Player bar");
            Assert.IsTrue(bar.gameObject.activeSelf, "Two or more seats get the bar.");
            Assert.AreEqual(new Vector2(.5f, 1f), bar.anchorMin, "Anchored top-centre, like CS2.");
            Assert.AreEqual(new Vector2(.5f, 1f), bar.anchorMax);
            Assert.AreEqual(0f, bar.anchoredPosition.x, .01f, "Horizontally centred.");
            Assert.AreEqual(-Wreckabulary.Rules.PlayerBar.Top, bar.anchoredPosition.y, .01f, "10 px under the top of the safe area.");
            StringAssert.StartsWith("P1", Text("Card 1/Name"), "You're on the bar too, named by seat on a couch.");
            StringAssert.StartsWith("P2", Text("Card 2/Name"));
            Assert.AreEqual("", Text("Card 2/Status"), "A wrecked seat says nothing in its portrait...");
            Assert.IsTrue(bar.Find("Card 2/Cross").GetComponent<Image>().enabled, "...it gets a cross over a grey portrait.");
            Assert.IsFalse(bar.Find("Card 1/Cross").GetComponent<Image>().enabled);
            Assert.IsFalse(bar.Find("Card 3/Cross").GetComponent<Image>().enabled);
            Assert.IsTrue(bar.Find("Card 3").gameObject.activeSelf, "Bots get a card.");
            StringAssert.StartsWith("AI 3", Text("Card 3/Name"));
            Assert.AreEqual("2/3", Text("Card 3/Score"), "Every card shows its wins.");
            Assert.AreEqual("0/3", Text("Card 1/Score"));
            Assert.IsFalse(bar.Find("Card 4").gameObject.activeSelf);
            var first = (RectTransform)bar.Find("Card 1");
            var third = (RectTransform)bar.Find("Card 3");
            Assert.AreEqual(0f, first.anchoredPosition.x + third.anchoredPosition.x, .01f, "The cards are centred as a row.");
            Assert.Less(first.anchoredPosition.x, ((RectTransform)bar.Find("Card 2")).anchoredPosition.x, "Seat order runs left to right.");
            var cardName = bar.Find("Card 1/Name").GetComponent<TMPro.TMP_Text>();
            Assert.AreEqual(Wreckabulary.Rules.PlayerBar.NameSize, cardName.fontSizeMax, "Names start at the name size...");
            Assert.That(cardName.fontSize, Is.InRange(Wreckabulary.Rules.PlayerBar.TextMin, Wreckabulary.Rules.PlayerBar.NameSize), "...and shrink no smaller than 15 pt to fit the card.");
            Assert.IsTrue((bar.Find("Card 1/Name").GetComponent<TMPro.TMP_Text>().fontStyle & TMPro.FontStyles.Bold) != 0, "Names are bold.");
            Assert.Greater(bar.Find("Card 1/Name").GetComponent<TMPro.TMP_Text>().outlineWidth, 0f, "Names carry a dark outline over light floors.");
            Assert.IsFalse(cardName.gameObject.activeSelf, "Names are not drawn under the cards; the portrait and its colour say who it is.");
            for (int i = 1; i <= 3; i++)
                Assert.IsNull(bar.Find($"Card {i}/You"), "No white rim on any card, your own included; the seat colour says who it is.");
            var frame = bar.Find("Card 1/Frame").GetComponent<Image>();
            Color.RGBToHSV(frame.color, out _, out float saturation, out float value);
            Assert.GreaterOrEqual(value, .94f, "The seat frame is a brightened seat colour.");
            Assert.GreaterOrEqual(saturation, .55f);
            var wreckedFrame = bar.Find("Card 2/Frame").GetComponent<Image>();
            Color.RGBToHSV(wreckedFrame.color, out _, out float grey, out _);
            Assert.Less(grey, .2f, "A wrecked seat's frame turns grey.");
            Assert.IsTrue(bar.Find("Card 2/Shade").GetComponent<Image>().enabled, "...and its portrait darkens.");
            Assert.IsFalse(bar.Find("Card 3/Shade").GetComponent<Image>().enabled);
            var health = (RectTransform)bar.Find("Card 1/Health track");
            Assert.AreEqual(health.rect.width, ((RectTransform)health.Find("Health")).rect.width, .5f, "Full health fills the bar.");
            Assert.AreEqual("100", health.Find("Number").GetComponent<TMPro.TMP_Text>().text, "The number sits in the bar, like CS2.");
            Assert.AreEqual("", bar.Find("Card 2/Health track/Number").GetComponent<TMPro.TMP_Text>().text, "A wrecked seat shows no number...");
            Assert.AreEqual(0f, ((RectTransform)bar.Find("Card 2/Health track/Health")).rect.width, .5f, "...and an empty bar.");
            Assert.IsFalse(bar.Find("Card 2/Stamina track").gameObject.activeSelf, "...and no stamina.");
            var portrait = bar.Find("Card 1/Portrait").GetComponent<RawImage>();
            var letter = bar.Find("Card 1/Letter").GetComponent<TMPro.TMP_Text>();
            Assert.IsTrue(portrait.enabled && portrait.texture || letter.enabled && letter.text == you.Initial.ToString(),
                "Each card shows the player's face, or their seat letter when nothing can be drawn.");
            if (SeatPortrait.CanRender && you.GetComponent<PlayerAppearance>().AvatarModel)
            {
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.IsTrue(portrait.enabled, "The live avatar is drawn onto the card.");
                Assert.AreEqual(SeatPortrait.Size, portrait.texture.width);
                int renders = you.GetComponent<SeatPortrait>().Renders;
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.AreEqual(renders, you.GetComponent<SeatPortrait>().Renders, "A portrait is drawn once, not every frame.");
                var appearance = you.GetComponent<PlayerAppearance>();
                string key = "wv.outfit." + you.Index;
                string saved = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
                try
                {
                    appearance.ApplyOutfit(appearance.CurrentOutfit);
                    yield return new WaitForSecondsRealtime(0.3f);
                    Assert.AreEqual(renders + 1, you.GetComponent<SeatPortrait>().Renders, "A new outfit redraws it.");
                }
                finally
                {
                    if (saved != null) PlayerPrefs.SetString(key, saved); else PlayerPrefs.DeleteKey(key);
                    PlayerPrefs.Save();
                }
            }
            var plate = (RectTransform)bar.Find("Backing");
            Assert.IsNotNull(plate, "One dark plate sits behind the whole bar, like CS2's strip.");
            Assert.AreEqual(0, plate.GetSiblingIndex(), "The plate is drawn behind every card.");
            var plateImage = plate.GetComponent<Image>();
            Assert.Less(Mathf.Max(plateImage.color.r, plateImage.color.g, plateImage.color.b), .2f, "dark");
            Assert.That(plateImage.color.a, Is.InRange(.6f, .85f), "translucent");
            Assert.IsFalse(plateImage.raycastTarget);
            for (int i = 1; i <= 3; i++)
            {
                var card = (RectTransform)bar.Find($"Card {i}");
                AssertEncloses(plate, card, Wreckabulary.Rules.PlayerBar.PlatePad - 1f, "The plate pads every card");
                foreach (var part in new[] { "Frame", "Score", "Health track", "Stamina track" })
                    AssertEncloses(plate, (RectTransform)card.Find(part), 1f, $"Card {i} {part} sits on the plate");
            }
            var label = (RectTransform)safe.Find("Match label");
            AssertApart(label, plate, "The bar keeps clear of the match label.");
            AssertApart((RectTransform)safe.Find("Side column/Minimap"), plate, "...and of the minimap.");
            Assert.AreEqual("2/3", safe.Find("Side column/Alive/Count").GetComponent<TMPro.TMP_Text>().text);
            Assert.IsFalse(safe.Find("Side column/Alive").gameObject.activeSelf, "The portraits already show who is up, so the head count steps aside.");
            Assert.IsFalse(safe.Find("Side column/Objective").gameObject.activeSelf, "No filler objective when there's nothing to do.");

            Object.Destroy(mate.gameObject);
            hud.SetScoreboard(new[] { you, bot }, _ => 0, 3, true);
            yield return new WaitForSecondsRealtime(0.12f);
            StringAssert.StartsWith("You", Text("Card 1/Name"), "The only human is You.");
            hud.SetScoreboard(new[] { you, bot }, _ => 0, 0, false);
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.IsFalse(bar.Find("Card 1/Score").gameObject.activeSelf, "A mode without scores hides them.");
            hud.SetScoreboard(new[] { you }, _ => 0, 3, true);
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.IsFalse(bar.gameObject.activeSelf, "Alone, your stats stay bottom left.");
            Object.Destroy(hud.gameObject);
            foreach (var p in new[] { you, bot }) Object.Destroy(p.gameObject);
        }

        static Rect OnScreen(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        static void AssertEncloses(RectTransform outer, RectTransform inner, float pad, string message)
        {
            float scale = outer.lossyScale.x;
            Rect o = OnScreen(outer), i = OnScreen(inner);
            Assert.LessOrEqual(o.xMin, i.xMin - pad * scale + .5f, $"{message}: left {o} vs {i}");
            Assert.GreaterOrEqual(o.xMax, i.xMax + pad * scale - .5f, $"{message}: right {o} vs {i}");
            Assert.LessOrEqual(o.yMin, i.yMin - pad * scale + .5f, $"{message}: bottom {o} vs {i}");
            Assert.GreaterOrEqual(o.yMax, i.yMax + pad * scale - .5f, $"{message}: top {o} vs {i}");
        }

        static void AssertApart(RectTransform a, RectTransform b, string message)
        {
            var corners = new Vector3[4];
            a.GetWorldCorners(corners);
            var ra = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
            b.GetWorldCorners(corners);
            var rb = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
            Assert.IsFalse(ra.Overlaps(rb), $"{message} {a.name} {ra} vs {b.name} {rb}");
        }

        static readonly (string name, Vector2 units)[] PhoneWindows = { ("1280x720", new Vector2(1920f, 1080f)), ("1248x576", new Vector2(2119.6f, 978.3f)) };

        static float Luma(Color c) => .2126f * c.r + .7152f * c.g + .0722f * c.b;

        static void AssertNumberFits(RectTransform track, string message)
        {
            foreach (var box in new[] { (RectTransform)track.Find("Number"), (RectTransform)track.Find("Health/Number") })
            {
                var text = box.GetComponent<TMPro.TMP_Text>();
                text.ForceMeshUpdate();
                Assert.GreaterOrEqual(text.fontSizeMin, Wreckabulary.Rules.PlayerBar.NumberMin, message);
                Assert.GreaterOrEqual(text.fontSize, 16f, message + ": bar numbers read at 1248x576");
                Assert.IsFalse(text.isTextTruncated, message + ": " + text.text);
                Assert.LessOrEqual(text.GetPreferredValues(text.text).x, box.rect.width + .5f, message + ": " + text.text + " fits its bar");
                AssertRectInside(track, box);
            }
        }

        static IEnumerator HurtTo(PlayerController p, float share)
        {
            for (int i = 0; i < 60 && p.Health.IsAlive && p.Health.Fraction > share; i++)
            {
                p.Health.ApplyDamage(Hits.Of(null, Vector3.forward, Wreckabulary.Rules.HitSource.Melee, 5f, 0f, hitStun: 0f));
                yield return null;
            }
            Assert.IsTrue(p.Health.IsAlive, p.Name + " stays up");
            Assert.LessOrEqual(p.Health.Fraction, share, p.Name + " is hurt enough");
        }

        static Wreckabulary.Rules.GameRules TestRules()
        {
            var rules = Match.Rules.Clone();
            rules.SpawnProtectionSeconds = 0f; rules.DodgeCooldown = 0f; rules.StaminaRegenDelay = 60f;
            return rules;
        }

        [UnityTest]
        public IEnumerator SeatCardsShowHealthAsABarWithItsNumberAndStaminaUnderIt()
        {
            var touch = TouchBinding.Shared;
            touch.Enabled = true;
            var you = Spawn(touch);
            var mate = Spawn(new ScriptedBinding());
            mate.Name = "P2";
            mate.transform.position = new Vector3(3f, 0f, 0f);
            var bot = Spawn(new BotBinding());
            bot.Name = "AI 3";
            bot.transform.position = new Vector3(-3f, 0f, 0f);
            foreach (var p in new[] { you, mate }) p.Health.UseRules(TestRules());
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            hud.ShowTouchControls(false);
            hud.SetScoreboard(new[] { you, mate, bot }, _ => 0, 3, true);
            yield return TestScenes.WaitUntil(() => hud.AcceptsGameplayInput && hud.BarSeated, 10f, "the gameplay HUD and its bar");
            yield return new WaitForSecondsRealtime(0.2f);
            var safe = (RectTransform)hud.transform.Find("Safe HUD");
            var bar = (RectTransform)safe.Find("Player bar");
            var plate = (RectTransform)bar.Find("Backing");
            RectTransform Track(int card) => (RectTransform)bar.Find($"Card {card}/Health track");
            string Number(int card) => Track(card).Find("Number").GetComponent<TMPro.TMP_Text>().text;
            float Filled(int card) => ((RectTransform)Track(card).Find("Health")).rect.width;
            Color Fill(int card) => Track(card).Find("Health").GetComponent<Image>().color;
            for (int card = 1; card <= 3; card++)
            {
                Assert.AreEqual("100", Number(card), $"Card {card}");
                Assert.AreEqual("100", Track(card).Find("Health/Number").GetComponent<TMPro.TMP_Text>().text, "Both copies of the number agree.");
                Assert.AreEqual(Wreckabulary.Rules.PlayerBar.HealthWidth(3), Track(card).rect.width, .5f, "The bar spans the card but for a hairline, like CS2.");
                Assert.AreEqual(Wreckabulary.Rules.PlayerBar.HealthHeight, Track(card).rect.height, .5f);
                Assert.AreEqual(Track(card).rect.width, Filled(card), .5f, "Full health fills the bar.");
                var light = Fill(card);
                Assert.GreaterOrEqual(Mathf.Min(light.r, light.g, light.b), .9f, "A healthy bar is light.");
                AssertNumberFits(Track(card), $"Card {card}");
                AssertEncloses(plate, Track(card), 1f, $"Card {card} health sits on the plate");
            }
            Assert.IsNotNull(Track(1).Find("Health").GetComponent<RectMask2D>(), "The dark number shows only on the fill...");
            Assert.Less(Luma(Track(1).Find("Health/Number").GetComponent<TMPro.TMP_Text>().color), .3f, "...dark on light...");
            Assert.Greater(Luma(Track(1).Find("Number").GetComponent<TMPro.TMP_Text>().color), .9f, "...and light over the empty track.");

            yield return HurtTo(mate, Wreckabulary.Rules.SeatVitals.HurtShare);
            yield return TestScenes.WaitUntil(() => Number(2) == Mathf.CeilToInt(mate.Health.Current).ToString(), 10f, "P2's number to follow the hits");
            Assert.AreEqual(Track(2).rect.width * mate.Health.Fraction, Filled(2), .5f, "The fill shrinks with health.");
            var amber = Fill(2);
            Assert.IsTrue(amber.r > .9f && amber.g > .6f && amber.b < .5f, "Half health or less turns the bar amber: " + amber);
            AssertNumberFits(Track(2), "Hurt card");

            yield return HurtTo(mate, Wreckabulary.Rules.SeatVitals.CriticalShare);
            yield return TestScenes.WaitUntil(() => Number(2) == Mathf.CeilToInt(mate.Health.Current).ToString(), 10f, "P2's number to follow the hits");
            Assert.AreEqual(Track(2).rect.width * mate.Health.Fraction, Filled(2), .5f);
            var red = Fill(2);
            Assert.IsTrue(red.r > .7f && red.g < .5f, "A quarter or less turns it red: " + red);
            AssertNumberFits(Track(2), "Critical card");
            Assert.AreEqual("100", Number(1), "Other seats keep their own health.");

            if (Wreckabulary.Rules.PlayerBar.StaminaShown)
            {
                var stamina = (RectTransform)bar.Find("Card 1/Stamina track");
                var breath = (RectTransform)stamina.Find("Stamina");
                Assert.IsTrue(stamina.gameObject.activeSelf);
                Assert.AreEqual(Wreckabulary.Rules.PlayerBar.StaminaWidth(3), stamina.rect.width, .5f, "Stamina is a little shorter than health...");
                Assert.AreEqual(OnScreen(Track(1)).xMin, OnScreen(stamina).xMin, .5f, "...starts at the health bar's left end...");
                Assert.Greater(OnScreen(Track(1)).xMax, OnScreen(stamina).xMax + .5f, "...and stops just short of its right end.");
                Assert.Less(stamina.rect.height, Track(1).rect.height / 2f, "Stamina stays a slim second line.");
                Assert.LessOrEqual(OnScreen(stamina).yMax, OnScreen(Track(1)).yMin + .5f, "It sits under the health bar.");
                Assert.AreEqual(stamina.rect.width, breath.rect.width, .5f, "Full stamina fills its bar.");
                Assert.Greater(breath.GetComponent<Image>().color.g, .9f, "Stamina is mint.");
                AssertEncloses(plate, stamina, 1f, "Stamina sits on the plate");
                Assert.IsNull(stamina.GetComponentInChildren<TMPro.TMP_Text>(true), "Stamina has no number.");
                Assert.IsTrue(you.Health.Dodge());
                yield return TestScenes.WaitUntil(() => Mathf.Abs(breath.rect.width - stamina.rect.width * you.Health.StaminaFraction) < .5f && you.Health.StaminaFraction < .99f, 10f, "the stamina bar to drop by one dodge");
                Assert.Greater(breath.GetComponent<Image>().color.g, .9f, "One dodge spent, another still fits.");
                Assert.IsTrue(you.Health.Dodge());
                Assert.IsTrue(you.Health.Dodge());
                yield return TestScenes.WaitUntil(() => breath.GetComponent<Image>().color.b < .5f, 10f, "the stamina bar to turn amber once a dodge no longer fits");
            }

            foreach (var (name, units) in PhoneWindows)
            {
                safe.anchorMin = safe.anchorMax = safe.pivot = Vector2.zero;
                safe.anchoredPosition = Vector2.zero;
                safe.sizeDelta = units;
                yield return new WaitForSecondsRealtime(.2f);
                Canvas.ForceUpdateCanvases();
                AssertRectInside(safe, plate);
                for (int card = 1; card <= 3; card++)
                {
                    AssertNumberFits(Track(card), $"{name} card {card}");
                    AssertEncloses(plate, Track(card), 1f, $"{name} card {card}");
                }
            }
            Object.Destroy(hud.gameObject);
            foreach (var p in new[] { you, mate, bot }) Object.Destroy(p.gameObject);
        }

        [UnityTest]
        public IEnumerator EightSnugSeatsKeepWholeHealthNumbersAtPhoneSizes()
        {
            var touch = TouchBinding.Shared;
            touch.Enabled = true;
            var seats = new PlayerController[Wreckabulary.Rules.PlayerBar.MostSeats];
            seats[0] = Spawn(touch);
            for (int i = 1; i < seats.Length; i++)
            {
                seats[i] = Spawn(new ScriptedBinding());
                seats[i].Name = "P" + (i + 1);
                seats[i].transform.position = new Vector3(i * 2f - 7f, 0f, 3f);
            }
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            hud.ShowTouchControls(false);
            hud.SetScoreboard(seats, _ => 0, 3, true);
            yield return TestScenes.WaitUntil(() => hud.AcceptsGameplayInput && hud.BarSeated, 10f, "the gameplay HUD and its bar");
            var safe = (RectTransform)hud.transform.Find("Safe HUD");
            var bar = (RectTransform)safe.Find("Player bar");
            var plate = (RectTransform)bar.Find("Backing");
            foreach (var (name, units) in PhoneWindows)
            {
                safe.anchorMin = safe.anchorMax = safe.pivot = Vector2.zero;
                safe.anchoredPosition = Vector2.zero;
                safe.sizeDelta = units;
                yield return new WaitForSecondsRealtime(.2f);
                Canvas.ForceUpdateCanvases();
                AssertRectInside(safe, plate);
                for (int card = 1; card <= seats.Length; card++)
                {
                    var track = (RectTransform)bar.Find($"Card {card}/Health track");
                    Assert.AreEqual(Wreckabulary.Rules.PlayerBar.HealthWidth(seats.Length), track.rect.width, .5f, $"{name} card {card}: a snug bar spans its card");
                    Assert.AreEqual("100", track.Find("Number").GetComponent<TMPro.TMP_Text>().text, $"{name} card {card}");
                    AssertNumberFits(track, $"{name} card {card}");
                    AssertEncloses(plate, track, 1f, $"{name} card {card}");
                    if (Wreckabulary.Rules.PlayerBar.StaminaShown) AssertEncloses(plate, (RectTransform)bar.Find($"Card {card}/Stamina track"), 1f, $"{name} card {card} stamina");
                }
            }
            Object.Destroy(hud.gameObject);
            foreach (var p in seats) Object.Destroy(p.gameObject);
        }

        [UnityTest]
        public IEnumerator AloneHealthIsTheSameBarBottomLeftAndItStepsAsideForTheSeatBar()
        {
            var touch = TouchBinding.Shared;
            touch.Enabled = true;
            var you = Spawn(touch);
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            hud.ShowTouchControls(false);
            var safe = (RectTransform)hud.transform.Find("Safe HUD");
            var vitals = (RectTransform)safe.Find("Vitals");
            yield return TestScenes.WaitUntil(() => hud.AcceptsGameplayInput && vitals.gameObject.activeSelf, 10f, "the bottom-left health bar");
            Assert.IsFalse(hud.HealthOnBar, "Alone there is no seat card.");
            var track = (RectTransform)vitals.Find("Health track");
            yield return TestScenes.WaitUntil(() => track.Find("Number").GetComponent<TMPro.TMP_Text>().text == "100", 10f, "the solo health number");
            Assert.IsNull(vitals.Find("HP"), "No more '+ 100 HP' words.");
            Assert.IsNull(vitals.Find("Stamina"), "No more '100 STA' words.");
            Assert.AreEqual(Wreckabulary.Rules.PlayerBar.SoloWidth, track.rect.width, .5f);
            Assert.Greater(track.rect.width, Wreckabulary.Rules.PlayerBar.RoomyPortrait, "Alone, the bar is bigger than a seat card's...");
            Assert.Greater(track.Find("Number").GetComponent<TMPro.TMP_Text>().fontSizeMax, Wreckabulary.Rules.PlayerBar.NumberSize, "...with a bigger number.");
            Assert.AreEqual(track.rect.width, ((RectTransform)track.Find("Health")).rect.width, .5f, "Full health fills it.");
            AssertNumberFits(track, "solo");
            AssertRectInside(safe, vitals);
            foreach (var part in new[] { "Health track", "Health track/Health", "Stamina track", "Stamina track/Stamina" })
                Assert.IsNull(vitals.Find(part).GetComponent<Image>().sprite, $"The alone {part} is square like the seat bars.");
            var stamina = (RectTransform)vitals.Find("Stamina track");
            Assert.AreEqual(Wreckabulary.Rules.PlayerBar.StaminaShown, stamina.gameObject.activeSelf, "The slim stamina bar sits under it.");
            if (Wreckabulary.Rules.PlayerBar.StaminaShown)
            {
                Assert.LessOrEqual(OnScreen(stamina).yMax, OnScreen(track).yMin + .5f, "under the health bar");
                Assert.Less(stamina.rect.height, track.rect.height / 2f, "slim");
                Assert.AreEqual(track.rect.width, stamina.rect.width, .5f);
            }
            var effects = (RectTransform)safe.Find("Effects");
            Assert.IsFalse(effects.gameObject.activeSelf, "Nothing to say, nothing shown.");
            you.Slow(.5f, 60f);
            yield return TestScenes.WaitUntil(() => effects.gameObject.activeSelf, 10f, "the slowed line");
            StringAssert.Contains("SLOWED", effects.GetComponent<TMPro.TMP_Text>().text);
            Assert.GreaterOrEqual(effects.GetComponent<TMPro.TMP_Text>().fontSize, 15f);
            Assert.GreaterOrEqual(OnScreen(effects).yMin, OnScreen(vitals).yMax - .5f, "Alone, status words sit just above the health bar.");

            var bot = Spawn(new BotBinding());
            bot.Name = "AI 2";
            bot.transform.position = new Vector3(3f, 0f, 0f);
            hud.SetScoreboard(new[] { you, bot }, _ => 0, 3, true);
            yield return TestScenes.WaitUntil(() => hud.HealthOnBar && !vitals.gameObject.activeSelf, 10f, "health to move onto the seat cards");
            Assert.IsTrue(effects.gameObject.activeSelf, "Status words stay bottom left beside the seat bar.");
            AssertRectInside(safe, effects);
            AssertApart(effects, (RectTransform)safe.Find("Letter bag"), "Status words keep clear of the letter bag");
            you.Inventory.Set("BAT");
            Assert.IsTrue(you.Summoner.Summon("BAT"));
            var word = safe.Find("Letter bag/Gear slot 1/Word").GetComponent<TMPro.TMP_Text>();
            yield return TestScenes.WaitUntil(() => word.text == "BAT", 10f, "the hands to keep updating with the bottom-left block hidden");

            hud.SetScoreboard(new[] { you }, _ => 0, 3, true);
            yield return TestScenes.WaitUntil(() => vitals.gameObject.activeSelf && !hud.HealthOnBar, 10f, "the bottom-left bar back when alone");
            Object.Destroy(hud.gameObject);
            Object.Destroy(you.gameObject);
            Object.Destroy(bot.gameObject);
        }

        [UnityTest]
        public IEnumerator FurnitureNamesKnowWhereTheHudSits()
        {
            var you = Spawn(TouchBinding.Shared);
            var bot = Spawn(new BotBinding());
            bot.Name = "AI 2";
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            hud.ShowTouchControls(false);
            hud.SetScoreboard(new[] { you, bot }, _ => 0, 3, true);
            yield return TestScenes.WaitUntil(() => hud.AcceptsGameplayInput && hud.BarSeated, 10f, "the gameplay HUD and its bar");
            yield return new WaitForSecondsRealtime(0.25f);
            var safe = (RectTransform)hud.transform.Find("Safe HUD");
            var rects = new System.Collections.Generic.List<Rect>();
            hud.CollectHudRects(rects);
            bool Covered(RectTransform part)
            {
                var middle = OnScreen(part).center;
                foreach (var rect in rects) if (rect.Contains(middle)) return true;
                return false;
            }
            Assert.IsTrue(Covered((RectTransform)safe.Find("Player bar/Backing")), "The bar's plate keeps names off it.");
            Assert.IsTrue(Covered((RectTransform)safe.Find("Letter bag/Letter 1")), "So do the letter tiles...");
            Assert.IsTrue(Covered((RectTransform)safe.Find("Letter bag/Gear slot 2")), "...the hand slots...");
            Assert.IsTrue(Covered((RectTransform)safe.Find("Brand")), "...and the mode title.");
            Assert.IsTrue(hud.HealthOnBar);
            Assert.IsFalse(safe.Find("Vitals").gameObject.activeSelf, "With the seat bar, health sits on the cards under the plate, so the bottom-left block steps aside.");
            var centre = new Vector2(Screen.width * .5f, Screen.height * .5f);
            Assert.IsFalse(rects.Exists(rect => rect.Contains(centre)), "The middle of the screen stays free for names.");
            Assert.IsFalse(rects.Exists(rect => rect.width <= 0f || rect.height <= 0f), "Only real areas.");
            Object.Destroy(hud.gameObject);
            Object.Destroy(you.gameObject);
            Object.Destroy(bot.gameObject);
        }

        [UnityTest]
        public IEnumerator GoalListsSitOnAPlateWithItemPicturesAndTicks()
        {
            var you = Spawn(TouchBinding.Shared);
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            hud.SetChecklist("<b>KEEPSAKES</b>\nBAT • Kitchen\nLAMP packed");
            hud.SetGoals("KEEPSAKES", new[]
            {
                new Wreckabulary.Rules.HudGoal("BAT", "Kitchen", false),
                new Wreckabulary.Rules.HudGoal("LAMP", "packed", true),
                new Wreckabulary.Rules.HudGoal("", "P2 is idle", false),
            });
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.IsTrue(hud.GoalPlateShown, "The goals sit on a plate.");
            Assert.AreEqual("KEEPSAKES", hud.GoalHeading);
            Assert.AreEqual(3, hud.GoalCount);
            Assert.AreEqual("BAT", hud.GoalWord(0));
            Assert.IsTrue(hud.GoalPictured(0), "An item goal shows the item.");
            Assert.IsFalse(hud.GoalTicked(0));
            Assert.IsTrue(hud.GoalTicked(1), "A finished goal is ticked.");
            Assert.IsFalse(hud.GoalPictured(2), "A plain line has no picture.");
            var text = hud.transform.Find("Safe HUD/Side column/Objective/Objective text").GetComponent<TMPro.TMP_Text>();
            Assert.IsFalse(text.enabled, "The plain list steps aside for the plate.");
            StringAssert.Contains("BAT", text.text, "Its words stay for anything that reads them.");
            Assert.IsTrue(hud.GoalPlate.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), "The plate never takes a click.");
            foreach (var label in hud.GoalPlate.GetComponentsInChildren<TMPro.TMP_Text>(true))
                Assert.GreaterOrEqual(Mathf.Min(label.fontSize, label.enableAutoSizing ? label.fontSizeMin : label.fontSize), 15f, label.name);
            yield return TestScenes.WaitUntil(() => hud.AcceptsGameplayInput, 10f, "the gameplay HUD");
            var holder = (RectTransform)text.transform.parent;
            yield return TestScenes.WaitUntil(() => Mathf.Abs(holder.sizeDelta.y - hud.GoalPlate.sizeDelta.y) < 0.5f, 10f, "the goal area to take the plate's size");
            Assert.AreEqual(hud.GoalPlate.sizeDelta.x, holder.sizeDelta.x, 0.5f);
            Assert.Greater(holder.sizeDelta.y, text.rectTransform.sizeDelta.y, "The plate is taller than the plain list it replaces.");
            Canvas.ForceUpdateCanvases();
            var rects = new System.Collections.Generic.List<Rect>();
            hud.CollectHudRects(rects);
            var plateBox = OnScreen(hud.GoalPlate);
            var plateFoot = new Vector2(plateBox.center.x, plateBox.yMin + 1f);
            Assert.IsTrue(rects.Exists(rect => rect.Contains(plateFoot)), "Furniture names keep clear of the whole plate.");
            hud.SetGoals("", null);
            Assert.IsFalse(hud.GoalPlateShown);
            Assert.IsTrue(text.enabled, "Without goals the plain text is back.");
            yield return TestScenes.WaitUntil(() => Mathf.Abs(holder.sizeDelta.y - text.rectTransform.sizeDelta.y) < 0.5f, 10f, "the goal area to fit the plain list again");
            Object.Destroy(hud.gameObject);
            Object.Destroy(you.gameObject);
        }

        [UnityTest]
        public IEnumerator TopCentreNoticesSitBelowThePlayerBar()
        {
            var you = Spawn(TouchBinding.Shared);
            var bot = Spawn(new BotBinding());
            bot.Name = "AI 2";
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            hud.ShowTouchControls(false);
            var safe = (RectTransform)hud.transform.Find("Safe HUD");
            hud.SetScoreboard(new[] { you, bot }, _ => 0, 3, true);
            hud.ShowCountdown(3, "ROUND 1 · LIVING ROOM");
            hud.Toast("Spelling BAT…");
            hud.ShowBanner("Round 1 · Last One Standing", "Knock out everyone else to win the round · First to 3");
            hud.SetInstruction("Smash furniture", "Spell items from the letters");
            hud.ShowModeActions(true, "START WITH AI →", () => { });
            yield return new WaitForSecondsRealtime(0.25f);
            var bar = (RectTransform)safe.Find("Player bar");
            Assert.IsTrue(bar.gameObject.activeSelf);
            Assert.AreEqual(Wreckabulary.Rules.PlayerBar.Shift, hud.TopInset, .01f, "Everything at the top centre moves down by the bar, its plate and 12 px.");
            var corners = new Vector3[4];
            ((RectTransform)bar.Find("Backing")).GetWorldCorners(corners);
            float barBottom = corners[0].y;
            foreach (var name in new[] { "Round chip", "Round banner", "Toast", "Status", "Mode actions" })
            {
                var part = (RectTransform)safe.Find(name);
                Assert.IsTrue(part.gameObject.activeSelf, name);
                part.GetWorldCorners(corners);
                Assert.Less(corners[1].y, barBottom - 11f * hud.UiCanvas.scaleFactor, name + " sits below the player bar's plate.");
            }
            safe.Find("Letter bag/Bag link").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.AreEqual(0f, bar.GetComponent<CanvasGroup>().alpha, "The bar steps aside under the bag.");
            safe.Find("Letter bag/Bag link").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.AreEqual(1f, bar.GetComponent<CanvasGroup>().alpha);
            hud.SetScoreboard(new[] { you }, _ => 0, 3, true);
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.AreEqual(0f, hud.TopInset, "Without the bar the notices go back up.");
            Assert.AreEqual(-25f, ((RectTransform)safe.Find("Round chip")).anchoredPosition.y, .01f);
            hud.ShowModeActions(false);
            Object.Destroy(hud.gameObject);
            Object.Destroy(you.gameObject);
            Object.Destroy(bot.gameObject);
        }

        [UnityTest]
        public IEnumerator TouchButtonsAtTheTopPushThePlayerBarUnderThem()
        {
            var you = Spawn(TouchBinding.Shared);
            var bot = Spawn(new BotBinding());
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            hud.SetScoreboard(new[] { you, bot }, _ => 0, 3, true);
            hud.ShowTouchControls(true);
            yield return new WaitForSecondsRealtime(0.25f);
            var safe = (RectTransform)hud.transform.Find("Safe HUD");
            var bar = (RectTransform)safe.Find("Player bar");
            var plate = (RectTransform)bar.Find("Backing");
            var view = safe.Find("Touch controls").GetComponent<TouchLayoutView>();
            for (var part = Wreckabulary.Rules.TouchPart.Move; part <= Wreckabulary.Rules.TouchPart.Eye; part++)
            {
                var rt = view.Part(part);
                if (rt && rt.gameObject.activeInHierarchy) AssertApart(rt, plate, "Touch buttons never cover the bar or its plate.");
            }
            Assert.GreaterOrEqual(hud.TopInset, Wreckabulary.Rules.PlayerBar.Shift - .01f);
            hud.ShowTouchControls(false);
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.AreEqual(-Wreckabulary.Rules.PlayerBar.Top, bar.anchoredPosition.y, .01f, "Back at the top once the touch buttons go.");
            Object.Destroy(hud.gameObject);
            Object.Destroy(you.gameObject);
            Object.Destroy(bot.gameObject);
        }

        [UnityTest]
        public IEnumerator TouchButtonsWaitForATouchScreen()
        {
            ControlTips.Reset();
            var you = Spawn(TouchBinding.Shared);
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            var safe = hud.transform.Find("Safe HUD");
            Assert.AreEqual(Application.isMobilePlatform, safe.Find("Touch controls").gameObject.activeSelf,
                "A desktop starts with a control tip, even with a touchscreen plugged in.");
            Assert.AreEqual(!Application.isMobilePlatform, safe.Find("Desktop controls").gameObject.activeSelf);
            Object.Destroy(hud.gameObject);
            Object.Destroy(you.gameObject);
        }

        [Test]
        public void SpellHintNamesTheBestReadyWordThenTheNearestMiss()
        {
            static WordEntry W(string word, bool hidden = false) => new WordEntry { word = word, hidden = hidden };
            var words = new[] { W("BALL"), W("BAT"), W("AXE"), W("SECRET", true) };
            Assert.Greater(LetterScores.ScoreOf("AXE"), LetterScores.ScoreOf("BAT"));
            Assert.AreEqual("BAT ready · Q", GameHud.SpellHintText(words, "TAB".ToCharArray(), 10, "Q"));
            Assert.AreEqual("AXE ready · Q", GameHud.SpellHintText(words, "BATAXE".ToCharArray(), 10, "Q"), "The highest-scoring ready word wins.");
            Assert.AreEqual("BAT ready", GameHud.SpellHintText(words, "BAT".ToCharArray(), 10, ""));
            Assert.AreEqual("BAT needs T", GameHud.SpellHintText(words, "BA".ToCharArray(), 10, "Q"), "BALL is two letters away, BAT only one.");
            Assert.AreEqual("", GameHud.SpellHintText(words, "BA".ToCharArray(), 2, "Q"), "A word that cannot fit the bag is no hint.");
            Assert.AreEqual("", GameHud.SpellHintText(words, "SECRE".ToCharArray(), 10, "Q"), "Hidden words are never hinted.");
            Assert.AreEqual("SECRET ready · Q", GameHud.SpellHintText(words, "SECRET".ToCharArray(), 10, "Q"));
            Assert.AreEqual("", GameHud.SpellHintText(words, "QZ".ToCharArray(), 10, "Q"));
            Assert.AreEqual("", GameHud.SpellHintText(words, new char[0], 10, "Q"));
            Assert.AreEqual("Y", GameHud.SpellKeyLabel(new GamepadBinding(null)));
            Assert.AreEqual("SPELL", GameHud.SpellKeyLabel(TouchBinding.Shared));
            Assert.AreEqual("K", GameHud.SpellKeyLabel(new KeyboardBinding(KeyboardBinding.Side.Left)));
        }

        [UnityTest]
        public IEnumerator SpellHintSitsQuietlyAboveTheTrayAndHidesWhenNothingHelps()
        {
            var touch = TouchBinding.Shared;
            touch.Enabled = true;
            var player = Spawn(touch);
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            hud.ShowTouchControls(false);
            player.Inventory.Set("");
            yield return new WaitForSecondsRealtime(0.2f);
            var tray = hud.transform.Find("Safe HUD/Letter bag");
            var hint = tray.Find("Spell hint").GetComponent<TMPro.TMP_Text>();
            Assert.IsFalse(hint.gameObject.activeSelf, "An empty bag has nothing useful to say.");
            Assert.IsNull(hint.GetComponent<Image>(), "The hint is a single line of text, not a card.");
            Assert.IsNotNull(hint.GetComponent<Shadow>());
            Assert.GreaterOrEqual(hint.fontSize, 23f);
            var hintRect = (RectTransform)hint.transform;
            Assert.GreaterOrEqual(hintRect.anchoredPosition.y, 0f, "The line sits above the letter tiles.");

            player.Inventory.Set("BAT");
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.IsTrue(hint.gameObject.activeSelf);
            Assert.AreEqual(GameHud.SpellHintText(GameAssets.I.words.Words, player.Inventory.Letters, player.Inventory.Capacity, "SPELL"), hint.text);
            StringAssert.EndsWith(" ready · SPELL", hint.text);

            player.Inventory.Set("BA");
            yield return new WaitForSecondsRealtime(0.2f);
            string expected = GameHud.SpellHintText(GameAssets.I.words.Words, player.Inventory.Letters, player.Inventory.Capacity, "SPELL");
            Assert.AreEqual(expected, hint.text);
            Assert.AreEqual(expected.Length > 0, hint.gameObject.activeSelf);

            player.Inventory.Set("BATBAT");
            Assert.IsTrue(player.Summoner.BeginCraft(new WordEntry { word = "BAT" }));
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.IsTrue(player.Summoner.IsCrafting);
            Assert.IsFalse(hint.gameObject.activeSelf, "The hint steps aside while a word is being spelled.");
            player.Summoner.CancelCraft();
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [Test]
        public void HouseHeadingsUseTheSameModeNamesAsTheLobby()
        {
            foreach (string mode in new[] { "Dibs", "Duos", "MovingOut", "MovingDay", "Escape" })
                Assert.AreEqual(LobbyPlayModeLabels.Name(mode), GameHud.MatchHeading(mode).name, mode);
            Assert.AreEqual("Duos", GameHud.MatchHeading("Duos").name);
            Assert.AreEqual("Pack the Van", GameHud.MatchHeading("MovingOut").name);
            Assert.AreEqual("CO-OP", GameHud.MatchHeading("Escape").tag);
            Assert.AreEqual("The house", GameHud.MatchHeading("Hub").name);
        }
    }
}
