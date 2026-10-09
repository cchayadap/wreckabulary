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
            Assert.IsNotNull(safe.Find("Side column/Minimap"));
            Assert.IsNotNull(safe.Find("Side column/Timer"));
            Assert.IsNotNull(safe.Find("Side column/Alive/Count"), "A head count sits before the timer, as on the web.");
            Assert.IsNotNull(safe.Find("Side column/Objective"));
            Assert.IsNull(safe.Find("Side column/Pause"), "The brand tile is the pause button.");
            Assert.IsNull(safe.Find("Return home"), "The way home is in the pause card.");
            Assert.IsNotNull(safe.Find("Letter bag/Gear slot 1"));
            Assert.IsNotNull(safe.Find("Letter bag/Gear slot 2"));
            Assert.IsNull(safe.Find("Vitals").GetComponent<Image>(), "Vitals sit straight on the game, with no card.");
            Assert.IsNull(safe.Find("Vitals/Health track"));
            StringAssert.Contains("HP", safe.Find("Vitals/HP").GetComponent<TMPro.TMP_Text>().text);
            Assert.IsTrue(safe.Find("Desktop controls").gameObject.activeSelf, "Desktop shows the key bar.");
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
            var keys = safe.Find("Desktop controls/Keys").GetComponent<TMPro.TMP_Text>().text;
            StringAssert.Contains("smash, throw, place, block", keys, "Left click does it all, blocking with a PLATE too.");
            StringAssert.Contains("mouse aim", keys, "Under the overhead camera the mouse aims.");
            StringAssert.DoesNotContain(">F<", keys);
            StringAssert.DoesNotContain("RMB", keys, "Right click aims only in the over-the-shoulder view.");
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
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

            foreach (var part in new[] { "Side column", "Letter bag", "Vitals", "Desktop controls" })
            {
                var group = safe.Find(part).GetComponent<CanvasGroup>();
                Assert.AreEqual(0f, group.alpha, part + " steps aside under the bag");
                Assert.IsFalse(group.blocksRaycasts, part);
            }
            Assert.IsTrue(safe.Find("Brand").gameObject.activeInHierarchy, "The brand stays, as on the web.");

            for (int i = 1; i <= 10; i++) Assert.IsNotNull(safe.Find($"Bag panel/Bag/Letters/Big letter {i}"), $"bag cell {i}");
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
                // Exercise logical canvas sizes without changing the editor's saved Game View resolutions.
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
            card.Find("Pause how to play").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(hud.HelpShown);
            Assert.IsFalse(card.gameObject.activeSelf, "Help takes the pause card's place.");
            StringAssert.Contains(" HP.", hud.transform.Find("Pause/Pause help/Fine print").GetComponent<TMPro.TMP_Text>().text);
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
        public IEnumerator OnlyCouchRoommatesGetCardsAndTheCountShowsWhoIsUp()
        {
            var touch = TouchBinding.Shared;
            touch.Enabled = true;
            var you = Spawn(touch);
            var mate = Spawn(new ScriptedBinding());
            mate.transform.position = new Vector3(3f, 0f, 0f);
            var bot = Spawn(new BotBinding());
            bot.transform.position = new Vector3(-3f, 0f, 0f);
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            hud.SetScoreboard(new[] { you, mate, bot }, _ => 0, 3, false);
            mate.Health.Eliminate();
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.AreSame(you, hud.LocalPlayer);
            var safe = hud.transform.Find("Safe HUD");
            Assert.IsTrue(safe.Find("Roommate 1").gameObject.activeSelf, "A couch roommate keeps a card.");
            StringAssert.Contains("WRECKED", safe.Find("Roommate 1/Status").GetComponent<TMPro.TMP_Text>().text);
            Assert.IsFalse(safe.Find("Roommate 2").gameObject.activeSelf, "AI housemates get no card, as on the web.");
            Assert.AreEqual("2/3", safe.Find("Side column/Alive/Count").GetComponent<TMPro.TMP_Text>().text);
            Assert.IsFalse(safe.Find("Side column/Objective").gameObject.activeSelf, "No filler objective when there's nothing to do.");
            Object.Destroy(hud.gameObject);
            foreach (var p in new[] { you, mate, bot }) Object.Destroy(p.gameObject);
        }

        [UnityTest]
        public IEnumerator TouchButtonsWaitForATouchScreen()
        {
            var you = Spawn(TouchBinding.Shared);
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            var safe = hud.transform.Find("Safe HUD");
            Assert.AreEqual(Application.isMobilePlatform, safe.Find("Touch controls").gameObject.activeSelf,
                "A desktop starts with the key bar, even with a touchscreen plugged in.");
            Assert.AreEqual(!Application.isMobilePlatform, safe.Find("Desktop controls").gameObject.activeSelf);
            Object.Destroy(hud.gameObject);
            Object.Destroy(you.gameObject);
        }
    }
}
