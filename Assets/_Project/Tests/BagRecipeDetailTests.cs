using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class BagRecipeDetailTests
    {
        const float Slow = 10f;
        const int DragSteps = 6;
        const string Detail = "Bag panel/" + GameHud.BookDetailName, Cards = "Bag panel/Recipe book/View/Cards", RightColumn = "Bag panel/Drop column right";

        InputSettings.EditorInputBehaviorInPlayMode route;
        InputSettings.BackgroundBehavior focus;
        Mouse mouse;
        Keyboard keys;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
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
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (keys != null && keys.added) InputSystem.RemoveDevice(keys);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            InputSystem.settings.editorInputBehaviorInPlayMode = route;
            InputSystem.settings.backgroundBehavior = focus;
            Time.timeScale = 1f;
            DesktopBinding.Typing = false;
            TouchBinding.Shared.ReleaseAll();
            SummonedThing.ClearAll();
            yield return TestScenes.Reset();
        }

        static PlayerController Spawn()
        {
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, DesktopBinding.Shared);
            player.Respawn(Vector3.zero);
            player.Inventory.Collects = false;
            return player;
        }

        static IEnumerator OpenBag(PlayerController player, string letters, System.Action<GameHud, RectTransform> ready)
        {
            var hud = new GameObject("Bag detail HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            yield return new WaitForSecondsRealtime(.12f);
            player.Inventory.Set(letters);
            var safe = (RectTransform)hud.transform.Find("Safe HUD");
            safe.Find("Letter bag/Bag link").GetComponent<Button>().onClick.Invoke();
            yield return TestScenes.WaitUntil(() => hud.BagOpen, Slow, "the bag");
            yield return new WaitForSecondsRealtime(.2f);
            Canvas.ForceUpdateCanvases();
            ready(hud, safe);
        }

        static Rect OnScreen(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        static Vector2 Centre(Transform t) => OnScreen((RectTransform)t).center;

        static RectTransform Card(RectTransform safe, int index) => (RectTransform)safe.Find(Cards).GetChild(index);

        static int Columns(RectTransform safe) => safe.Find(Cards).GetComponent<GridLayoutGroup>().constraintCount;

        static bool Overlap(Rect a, Rect b, float slack) => a.xMin < b.xMax - slack && b.xMin < a.xMax - slack && a.yMin < b.yMax - slack && b.yMin < a.yMax - slack;

        IEnumerator Point(Vector2 at, bool down)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = at }.WithButton(MouseButton.Left, down));
            yield return null;
            yield return null;
        }

        IEnumerator Hover(GameHud hud, RectTransform card)
        {
            yield return Point(Centre(card), false);
            yield return TestScenes.WaitUntil(() => hud.BookDetailWord == card.name, Slow, "the detail panel for " + card.name);
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        static void AssertBeside(GameHud hud, RectTransform safe, RectTransform card, bool right, string where)
        {
            var panel = (RectTransform)safe.Find(Detail);
            float slack = .5f * hud.UiCanvas.scaleFactor;
            var box = OnScreen(panel); var cardBox = OnScreen(card); var safeBox = OnScreen(safe);
            Assert.IsTrue(panel.gameObject.activeInHierarchy, where);
            if (right) Assert.GreaterOrEqual(box.xMin, cardBox.xMax - slack, where + ": opens on the card's right");
            else Assert.LessOrEqual(box.xMax, cardBox.xMin + slack, where + ": opens on the card's left");
            Assert.IsFalse(Overlap(box, cardBox, slack), where + ": never over the card it describes");
            Assert.GreaterOrEqual(box.xMin, safeBox.xMin - slack, where + ": inside the safe area");
            Assert.LessOrEqual(box.xMax, safeBox.xMax + slack, where + ": inside the safe area");
            Assert.GreaterOrEqual(box.yMin, safeBox.yMin - slack, where + ": inside the safe area");
            Assert.LessOrEqual(box.yMax, safeBox.yMax + slack, where + ": inside the safe area");
            foreach (var text in panel.Find(card.name).GetComponentsInChildren<TMPro.TMP_Text>())
                Assert.GreaterOrEqual(text.enableAutoSizing ? text.fontSizeMin : text.fontSize, RecipeSheet.MinFont - .01f, where + ": " + text.name + " reads at 15 pt or more");
        }

        [UnityTest]
        public IEnumerator HoveringALeftCardOpensItsDetailOnTheRightAndALastColumnCardOnTheLeft()
        {
            var player = Spawn();
            GameHud hud = null; RectTransform safe = null;
            yield return OpenBag(player, "BAT", (h, s) => { hud = h; safe = s; });
            var panel = (RectTransform)safe.Find(Detail);
            Assert.IsNotNull(panel, "the bag has one recipe detail panel");
            Assert.IsFalse(panel.gameObject.activeSelf, "hidden until a card is pointed at");
            var group = panel.GetComponent<CanvasGroup>();
            Assert.IsFalse(group.blocksRaycasts, "it never takes clicks or drags");
            Assert.IsFalse(group.interactable);

            var first = Card(safe, 0);
            Assert.Greater(GameHud.BookDetailDelay, 0f, "a passing pointer does not flash the panel");
            Assert.LessOrEqual(GameHud.BookDetailDelay, .25f);
            yield return Hover(hud, first);
            AssertBeside(hud, safe, first, true, "first column");
            Assert.IsFalse(panel.GetComponentsInChildren<Graphic>(true).Any(g => g.raycastTarget), "no part of the panel catches the pointer");
            var sheet = panel.Find(first.name);
            Assert.AreEqual(first.name, sheet.Find("Name").GetComponent<TMPro.TMP_Text>().text);
            Assert.IsNotNull(sheet.Find("Picture"));
            Assert.IsNotNull(sheet.Find("Effect 1"));
            Assert.AreEqual(first.name.Length, sheet.Cast<Transform>().Count(t => t.name.StartsWith("Tile ")), "one tile a letter");
            Assert.AreEqual(1, panel.Cast<Transform>().Count(t => t.gameObject.activeSelf && t.name != "Edge"), "one sheet at a time");

            var last = Card(safe, Columns(safe) - 1);
            yield return Hover(hud, last);
            AssertBeside(hud, safe, last, false, "last column");
            Assert.AreEqual(1, panel.Cast<Transform>().Count(t => t.gameObject.activeSelf && t.name != "Edge"), "the old sheet went away");

            yield return Point(Centre(safe.Find("Bag panel/House/Map area/Big map")), false);
            yield return TestScenes.WaitUntil(() => hud.BookDetailWord == null, Slow, "the panel to go when the pointer leaves the card");
            Assert.IsFalse(panel.gameObject.activeSelf);
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator ThePanelFitsBesideTheCardAtLaptopPhoneAndNarrowWindows()
        {
            var player = Spawn();
            GameHud hud = null; RectTransform safe = null;
            yield return OpenBag(player, "BALL", (h, s) => { hud = h; safe = s; });
            foreach (var size in new[] { new Vector2(1920f, 1080f), new Vector2(2119.6f, 978.3f), new Vector2(1440f, 1080f) })
            {
                safe.anchorMin = safe.anchorMax = safe.pivot = Vector2.zero;
                safe.anchoredPosition = Vector2.zero;
                safe.sizeDelta = size;
                yield return new WaitForSecondsRealtime(.12f);
                Canvas.ForceUpdateCanvases();
                foreach (var (index, right) in new[] { (0, true), (Columns(safe) - 1, false) })
                {
                    var card = Card(safe, index);
                    ExecuteEvents.Execute(card.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                    yield return TestScenes.WaitUntil(() => hud.BookDetailWord == card.name, Slow, $"{size} detail for {card.name}");
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    AssertBeside(hud, safe, card, right, $"{size} {card.name}");
                    ExecuteEvents.Execute(card.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
                    yield return TestScenes.WaitUntil(() => hud.BookDetailWord == null, Slow, $"{size} panel to hide");
                }
            }
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator StartingADragOrScrollingTheBookTakesThePanelAway()
        {
            var player = Spawn();
            GameHud hud = null; RectTransform safe = null;
            yield return OpenBag(player, "BAT", (h, s) => { hud = h; safe = s; });
            var first = Card(safe, 0);
            yield return Hover(hud, first);

            var letter = Centre(safe.Find("Bag panel/Bag/Letters/Big letter 1"));
            yield return Point(letter, false);
            yield return Point(letter, true);
            var over = Centre(first);
            for (int i = 1; i <= DragSteps; i++) yield return Point(Vector2.Lerp(letter, over, i / (float)DragSteps), true);
            Assert.IsTrue(hud.BagDragging, "a letter is being dragged across the book");
            yield return new WaitForSecondsRealtime(GameHud.BookDetailDelay * 3f);
            Assert.IsNull(hud.BookDetailWord, "no detail panel while a drag is under way");
            yield return Point(over, false);
            Assert.IsFalse(hud.BagDragging);

            yield return Hover(hud, first);
            var scroll = safe.Find("Bag panel/Recipe book/View").GetComponent<ScrollRect>();
            Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height, "the book is long enough to scroll");
            scroll.verticalNormalizedPosition = 0f;
            yield return TestScenes.WaitUntil(() => hud.BookDetailWord != first.name, Slow, "the panel to leave a card that scrolled away");
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (hud.BookDetailWord != null)
                AssertBeside(hud, safe, (RectTransform)safe.Find(Cards + "/" + hud.BookDetailWord), OnScreen((RectTransform)safe.Find(Detail)).center.x > OnScreen((RectTransform)safe.Find(Cards + "/" + hud.BookDetailWord)).center.x, "the card now under the pointer");
            scroll.verticalNormalizedPosition = 1f;
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator AClickOnAReadyCardStillSpellsWithThePanelOpen()
        {
            var player = Spawn();
            GameHud hud = null; RectTransform safe = null;
            yield return OpenBag(player, "BAT", (h, s) => { hud = h; safe = s; });
            var card = (RectTransform)safe.Find(Cards + "/BAT");
            Assert.IsTrue(card.GetComponent<Button>().interactable);
            yield return Hover(hud, card);
            var sheet = safe.Find(Detail + "/BAT");
            Assert.AreEqual(RecipeSheet.LettersLine(0, false), sheet.Find("Letters").GetComponent<TMPro.TMP_Text>().text, "held letters are counted");
            var at = Centre(card);
            yield return Point(at, true);
            yield return Point(at, false);
            yield return TestScenes.WaitUntil(() => !hud.BagOpen, Slow, "spelling from the book to close the bag");
            Assert.IsTrue(player.Summoner.IsSpelling || player.Summoner.IsCrafting, "the click spelled BAT");
            Assert.IsNull(hud.BookDetailWord, "the panel closes with the bag");
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator ThePanelNeverBlocksADragToABin()
        {
            var player = Spawn();
            GameHud hud = null; RectTransform safe = null;
            yield return OpenBag(player, "BALX", (h, s) => { hud = h; safe = s; });
            var first = Card(safe, 0);
            yield return Hover(hud, first);
            var sheet = safe.Find(Detail + "/" + first.name);
            Assert.AreEqual(RecipeSheet.LettersLine(RecipeSheet.Missing(RecipeSheet.Held(first.name, player.Inventory.Letters, false)), false),
                sheet.Find("Letters").GetComponent<TMPro.TMP_Text>().text, "the missing count follows the bag");
            int before = player.Inventory.Count;
            var letter = Centre(safe.Find("Bag panel/Bag/Letters/Big letter 4"));
            var bin = Centre(safe.Find(RightColumn));
            yield return Point(letter, false);
            yield return Point(letter, true);
            for (int i = 1; i <= DragSteps; i++) yield return Point(Vector2.Lerp(letter, bin, i / (float)DragSteps), true);
            yield return Point(bin, false);
            yield return TestScenes.WaitUntil(() => player.Inventory.Count == before - 1, Slow, "the letter to reach the bin past the panel");
            Assert.IsTrue(hud.BagOpen);
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }
    }
}
