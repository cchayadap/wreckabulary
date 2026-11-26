using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class BagPanelDragTests
    {
        const float Slow = 10f;
        const int DragSteps = 6;

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
            var hud = new GameObject("Bag drag HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            yield return new WaitForSecondsRealtime(.12f);
            player.Inventory.Set(letters);
            var safe = (RectTransform)hud.transform.Find("Safe HUD");
            hud.ToggleBag();
            yield return TestScenes.WaitUntil(() => hud.BagOpen, Slow, "the bag");
            yield return new WaitForSecondsRealtime(.2f);
            Canvas.ForceUpdateCanvases();
            ready(hud, safe);
        }

        static Vector2 Centre(Transform t)
        {
            var rt = (RectTransform)t;
            return RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
        }

        IEnumerator Point(Vector2 at, bool down)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = at }.WithButton(MouseButton.Left, down));
            yield return null;
            yield return null;
        }

        IEnumerator Press(Vector2 from, Vector2 to)
        {
            yield return Point(from, false);
            yield return Point(from, true);
            for (int i = 1; i <= DragSteps; i++) yield return Point(Vector2.Lerp(from, to, i / (float)DragSteps), true);
        }

        IEnumerator Drag(Vector2 from, Vector2 to)
        {
            yield return Press(from, to);
            yield return Point(to, false);
            yield return new WaitForSecondsRealtime(.1f);
        }

        const string LeftColumn = "Bag panel/Drop column left", RightColumn = "Bag panel/Drop column right";

        static string Order(GameHud hud) => new(hud.BagLetterOrder.ToArray());

        static string CellText(RectTransform safe, int cell) =>
            safe.Find($"Bag panel/Bag/Letters/Big letter {cell}/Face/Letter").GetComponent<TMPro.TMP_Text>().text;

        static LetterTile LooseTile(char letter) => Object.FindObjectsByType<LetterTile>()
            .FirstOrDefault(tile => tile && tile.gameObject.activeInHierarchy && tile.Letter == letter);

        [UnityTest]
        public IEnumerator DraggingALetterToTheDropColumnDropsItInFrontOfThePlayer()
        {
            var player = Spawn();
            GameHud hud = null; RectTransform safe = null;
            yield return OpenBag(player, "BAT", (h, s) => { hud = h; safe = s; });
            Assert.AreEqual("ABT", Order(hud));
            var right = safe.Find(RightColumn);
            var left = safe.Find(LeftColumn);
            Assert.IsNotNull(right, "the bag has a drop column on the right");
            Assert.IsNotNull(left, "and one on the left");
            Assert.IsNotNull(right.Find("Trash"), "marked with the bin icon");
            Assert.IsNotNull(left.Find("Trash"));
            Assert.IsNull(LooseTile('T'));

            yield return Drag(Centre(safe.Find("Bag panel/Bag/Letters/Big letter 3")), Centre(right));
            yield return TestScenes.WaitUntil(() => player.Inventory.Count == 2, Slow, "the letter to leave the bag");
            Assert.AreEqual("AB", Order(hud));
            yield return Drag(Centre(safe.Find("Bag panel/Bag/Letters/Big letter 1")), Centre(left));
            yield return TestScenes.WaitUntil(() => player.Inventory.Count == 1, Slow, "the left column to drop a letter too");
            Assert.AreEqual("B", Order(hud));
            yield return TestScenes.WaitUntil(() => LooseTile('A') != null, Slow, "an A pickup in the world");
            LetterTile tile = null;
            yield return TestScenes.WaitUntil(() => (tile = LooseTile('T')) != null, Slow, "a T pickup in the world");
            yield return TestScenes.WaitUntil(() => Vector3.Dot(tile.transform.position - player.transform.position, player.Facing) > .2f, Slow, "the T to land in front of the player");
            Assert.IsTrue(hud.BagOpen, "the bag stays open");
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator DraggingAnItemToTheDropColumnDropsIt()
        {
            var player = Spawn();
            GameHud hud = null; RectTransform safe = null;
            yield return OpenBag(player, "BAT", (h, s) => { hud = h; safe = s; });
            Assert.IsTrue(player.Summoner.Summon("BAT"));
            var bat = player.Combat.Weapon;
            Assert.IsNotNull(bat);
            yield return new WaitForSecondsRealtime(.2f);

            yield return Drag(Centre(safe.Find("Bag panel/Bag/Hands/Bag hand 1")), Centre(safe.Find(LeftColumn)));
            yield return TestScenes.WaitUntil(() => !player.Combat.IsHolding && !bat.IsHeld, Slow, "the bat to be dropped on the left column");
            Assert.IsNull(player.Combat.GearIn(0));
            Assert.IsTrue(bat.gameObject.activeInHierarchy, "dropped on the ground, not deleted");
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator DraggingAStoredItemToTheDropColumnDropsThatOne()
        {
            var player = Spawn();
            GameHud hud = null; RectTransform safe = null;
            yield return OpenBag(player, "BATBLADE", (h, s) => { hud = h; safe = s; });
            Assert.IsTrue(player.Summoner.Summon("BAT"));
            var bat = player.Combat.Weapon;
            Assert.IsTrue(player.Summoner.Summon("BLADE"));
            var blade = player.Combat.Weapon;
            Assert.AreEqual(1, player.Combat.ActiveSlot);
            yield return new WaitForSecondsRealtime(.2f);

            yield return Drag(Centre(safe.Find("Bag panel/Bag/Hands/Bag hand 1")), Centre(safe.Find(RightColumn)));
            yield return TestScenes.WaitUntil(() => !bat.IsHeld && player.Combat.GearIn(0) == null, Slow, "the bat to be dropped on the right column");
            Assert.AreSame(blade, player.Combat.GearIn(1), "the other item stays");
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator DraggingALetterOntoAnotherSlotReordersTheBag()
        {
            var player = Spawn();
            GameHud hud = null; RectTransform safe = null;
            yield return OpenBag(player, "BAT", (h, s) => { hud = h; safe = s; });
            Assert.AreEqual("ABT", Order(hud));

            yield return Drag(Centre(safe.Find("Bag panel/Bag/Letters/Big letter 3")), Centre(safe.Find("Bag panel/Bag/Letters/Big letter 1")));
            yield return TestScenes.WaitUntil(() => Order(hud) == "TAB", Slow, "T to move to the front");
            yield return TestScenes.WaitUntil(() => CellText(safe, 1) == "T" && CellText(safe, 3) == "B", Slow, "the tiles to show the new order");
            Assert.AreEqual(3, player.Inventory.Count, "reordering keeps every letter");
            Assert.IsNull(LooseTile('T'));

            player.Inventory.TryAdd('L');
            yield return TestScenes.WaitUntil(() => Order(hud) == "TABL", Slow, "a new letter to join the end");
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator DraggingAnItemOntoTheOtherSlotSwapsThem()
        {
            var player = Spawn();
            GameHud hud = null; RectTransform safe = null;
            yield return OpenBag(player, "BATBLADE", (h, s) => { hud = h; safe = s; });
            Assert.IsTrue(player.Summoner.Summon("BAT"));
            var bat = player.Combat.Weapon;
            Assert.IsTrue(player.Summoner.Summon("BLADE"));
            var blade = player.Combat.Weapon;
            yield return new WaitForSecondsRealtime(.2f);

            yield return Drag(Centre(safe.Find("Bag panel/Bag/Hands/Bag hand 1")), Centre(safe.Find("Bag panel/Bag/Hands/Bag hand 2")));
            yield return TestScenes.WaitUntil(() => player.Combat.GearIn(0) == blade, Slow, "the items to swap slots");
            Assert.AreSame(bat, player.Combat.GearIn(1));
            Assert.AreSame(blade, player.Combat.Weapon, "swapping slots keeps the same item in hand");
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator ADragReleasedOutsideGoesBack()
        {
            var player = Spawn();
            GameHud hud = null; RectTransform safe = null;
            yield return OpenBag(player, "BAT", (h, s) => { hud = h; safe = s; });
            var cell = safe.Find("Bag panel/Bag/Letters/Big letter 1");

            yield return Drag(Centre(cell), Centre(safe.Find("Bag panel/House/Map area/Big map")));
            Assert.AreEqual("ABT", Order(hud), "nothing moved");
            Assert.AreEqual(3, player.Inventory.Count, "nothing dropped");
            Assert.IsNull(LooseTile('A'));
            Assert.IsFalse(safe.Find("Bag panel/Drag ghost").gameObject.activeSelf, "the dragged tile is gone");
            Assert.AreEqual(1f, cell.GetComponent<CanvasGroup>().alpha, 1e-3f, "and the slot looks full again");
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator AClickWithoutMovementOnARecipeCardStillSpells()
        {
            var player = Spawn();
            GameHud hud = null; RectTransform safe = null;
            yield return OpenBag(player, "BAT", (h, s) => { hud = h; safe = s; });
            var card = safe.Find("Bag panel/Recipe book/View/Cards/BAT");
            Assert.IsTrue(card.GetComponent<Button>().interactable);
            var at = Centre(card);
            yield return Point(at, false);
            yield return Point(at, true);
            yield return Point(at, false);
            yield return TestScenes.WaitUntil(() => !hud.BagOpen, Slow, "spelling from the book to close the bag");
            Assert.IsTrue(player.Summoner.IsSpelling || player.Summoner.IsCrafting, "the click spelled BAT");
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator EachDropColumnLightsUpWhileSomethingIsDraggedOverIt()
        {
            var player = Spawn();
            GameHud hud = null; RectTransform safe = null;
            yield return OpenBag(player, "BAT", (h, s) => { hud = h; safe = s; });
            var columns = new[] { safe.Find(LeftColumn), safe.Find(RightColumn) };
            var faces = columns.Select(c => c.GetComponent<Image>()).ToArray();
            var icons = columns.Select(c => c.Find("Trash").GetComponent<Image>()).ToArray();
            Color idleFace = faces[0].color, idleIcon = icons[0].color;
            Assert.AreEqual(idleFace, faces[1].color, "both columns look the same");
            Assert.AreEqual(idleIcon, icons[1].color);
            Assert.AreEqual(1f, idleIcon.a, 1e-3f, "the idle icon is solid, not faint");
            Assert.Greater(idleFace.a, .1f, "the idle column has a visible fill");
            foreach (var column in columns) Assert.AreEqual(Vector3.one, column.localScale, "at rest the column keeps its size");

            var from = Centre(safe.Find("Bag panel/Bag/Letters/Big letter 2"));
            for (int side = 0; side < columns.Length; side++)
            {
                int other = 1 - side;
                yield return Press(from, Centre(columns[side]));
                Assert.IsTrue(hud.BagDragging, "a drag is under way");
                Assert.IsTrue(safe.Find("Bag panel/Drag ghost").gameObject.activeSelf, "the tile follows the pointer");
                Assert.AreNotEqual(idleFace, faces[side].color, columns[side].name + " lights up under the drag");
                Assert.AreNotEqual(idleIcon, icons[side].color, "its bin turns gold");
                Assert.AreEqual(idleFace, faces[other].color, "the other column stays idle");
                yield return TestScenes.WaitUntil(() => columns[0].localScale.x > 1.01f && columns[1].localScale.x > 1.01f, Slow, "both columns to grow while dragging");

                yield return Point(Centre(safe.Find("Bag panel/House/Map area/Big map")), true);
                Assert.AreEqual(idleFace, faces[side].color, "it dims when the drag leaves it");
                yield return Point(Centre(safe.Find("Bag panel/House/Map area/Big map")), false);
                Assert.IsFalse(hud.BagDragging);
                Assert.AreEqual(idleIcon, icons[side].color);
                yield return TestScenes.WaitUntil(() => columns[0].localScale.x < 1.001f && columns[1].localScale.x < 1.001f, Slow, "the columns to settle back");
            }
            Assert.AreEqual(3, player.Inventory.Count);
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator TheDropKeyDropsTheFocusedLetterNotTheItemInHand()
        {
            var player = Spawn();
            GameHud hud = null; RectTransform safe = null;
            yield return OpenBag(player, "BATXY", (h, s) => { hud = h; safe = s; });
            Assert.IsTrue(player.Summoner.Summon("BAT"));
            var bat = player.Combat.Weapon;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.AreEqual("XY", Order(hud));
            var line = safe.Find("Bag panel/Recipe book/Book line").GetComponent<TMPro.TMP_Text>();

            yield return Point(Centre(safe.Find("Bag panel/Bag/Letters/Big letter 2")), false);
            string key = ControlHints.For(player.Binding, PlayerControl.Drop);
            yield return TestScenes.WaitUntil(() => line.text == BagDrag.Hint("Y", key, false), Slow, "the hint line to name the drop key");

            var dropKey = DesktopBinding.Shared.Drop.controls.OfType<KeyControl>().FirstOrDefault(k => k.device == keys);
            Assert.IsNotNull(dropKey, "the drop action has a key on this keyboard");
            InputSystem.QueueStateEvent(keys, new KeyboardState(dropKey.keyCode));
            yield return new WaitForSecondsRealtime(InputBinding.DropHoldSeconds + .25f);
            InputSystem.QueueStateEvent(keys, new KeyboardState());
            yield return TestScenes.WaitUntil(() => Order(hud) == "X", Slow, "the focused Y to drop");
            Assert.AreSame(bat, player.Combat.Weapon, "the item in hand stays");
            Assert.IsTrue(bat.IsHeld);
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator TheMapColumnBookAndBothDropColumnsFitAtLaptopAndPhoneSizes()
        {
            var player = Spawn();
            GameHud hud = null; RectTransform safe = null;
            yield return OpenBag(player, "BALL", (h, s) => { hud = h; safe = s; });
            var book = (RectTransform)safe.Find("Bag panel/Recipe book");
            var left = (RectTransform)safe.Find(LeftColumn);
            var right = (RectTransform)safe.Find(RightColumn);
            var mapArea = (RectTransform)safe.Find("Bag panel/House/Map area");
            var map = (RectTransform)mapArea.Find("Big map");
            var inventory = (RectTransform)safe.Find("Bag panel/Bag");
            var hands = (RectTransform)inventory.Find("Hands");
            var close = (RectTransform)safe.Find("Bag panel/Close bag");
            var grid = book.Find("View/Cards").GetComponent<GridLayoutGroup>();

            var expectedMap = new System.Collections.Generic.Dictionary<float, float> { [1920f] = 514f, [2119.6f] = 447.5f, [1440f] = 325f, [2520f] = 519.2f };
            foreach (var size in new[] { new Vector2(1920f, 1080f), new Vector2(2119.6f, 978.3f), new Vector2(1440f, 1080f), new Vector2(2520f, 1080f) })
            {
                safe.anchorMin = safe.anchorMax = safe.pivot = Vector2.zero;
                safe.anchoredPosition = Vector2.zero;
                safe.sizeDelta = size;
                yield return new WaitForSecondsRealtime(.12f);
                Canvas.ForceUpdateCanvases();
                float scale = hud.UiCanvas.scaleFactor, slack = .5f * scale;
                var leftBox = OnScreen(left); var rightBox = OnScreen(right); var bookBox = OnScreen(book);
                var mapBox = OnScreen(mapArea); var frameBox = OnScreen(map); var safeBox = OnScreen(safe);
                var inventoryBox = OnScreen(inventory); var handsBox = OnScreen(hands);
                foreach (var box in new[] { leftBox, rightBox, bookBox, mapBox, inventoryBox, handsBox })
                {
                    Assert.GreaterOrEqual(box.xMin, safeBox.xMin - slack, size + " on screen");
                    Assert.LessOrEqual(box.xMax, safeBox.xMax + slack, size + " on screen");
                    Assert.GreaterOrEqual(box.yMin, safeBox.yMin - slack, size + " on screen");
                    Assert.LessOrEqual(box.yMax, safeBox.yMax + slack, size + " on screen");
                }
                Assert.AreEqual(left.rect.width, right.rect.width, .01f, size + " both drop columns share one width");
                Assert.GreaterOrEqual(right.rect.width, BagDrag.ColumnMin - .5f);
                Assert.AreEqual(bookBox.height, leftBox.height, slack, size + " the left column runs the bag's full height");
                Assert.AreEqual(bookBox.height, rightBox.height, slack, size + " and so does the right one");
                Assert.AreEqual(leftBox.xMin - safeBox.xMin, safeBox.xMax - rightBox.xMax, slack, size + " mirrored at the two edges");
                Assert.Less(leftBox.xMax, mapBox.xMin, size + " the left column sits left of the map column");
                Assert.Less(mapBox.xMax, bookBox.xMin, size + " the map column sits left of the book");
                Assert.Less(bookBox.xMax, rightBox.xMin, size + " the right column sits right of the book");
                Assert.AreEqual(mapBox.center.x, inventoryBox.center.x, slack, size + " the inventory is centred under the map");
                Assert.AreEqual(Mathf.Max(mapBox.width, GameHud.BagInventoryCellWidth * scale), inventoryBox.width, slack, size + " and is as wide as the map");
                Assert.AreEqual(mapBox.center.x, handsBox.center.x, slack, size + " the item slots stay centred under the map");
                var firstLetter = OnScreen((RectTransform)inventory.Find("Letters/Big letter 1"));
                var lastLetter = OnScreen((RectTransform)inventory.Find("Letters/Big letter 10"));
                float letterScale = BagDrag.LetterScale(inventory.rect.width, GameHud.BagLetterMargin, GameHud.BagLetterGridWide);
                Assert.AreEqual(inventoryBox.xMax, lastLetter.xMax, slack, size + " the letters reach the right edge of the column");
                Assert.AreEqual(GameHud.BagLetterWide * letterScale * scale, firstLetter.width, slack, size + " the letter tiles grow with the column");
                Assert.GreaterOrEqual(firstLetter.width, GameHud.BagLetterWide * scale - slack, size + " never smaller than before");
                Assert.Greater(lastLetter.yMin, handsBox.yMax, size + " the letters stay above the item slots");
                float footLeft = Mathf.Min(mapBox.xMin, inventoryBox.xMin), footRight = Mathf.Max(mapBox.xMax, inventoryBox.xMax);
                Assert.AreEqual(footLeft - leftBox.xMax, bookBox.xMin - footRight, slack, size + " equal gaps either side of the map column");
                Assert.Greater(footLeft, leftBox.xMax, size + " clear of the left bin");
                Assert.Greater(bookBox.xMin, footRight, size + " clear of the book");
                Assert.AreEqual(expectedMap[size.x], map.rect.width, .5f, size + " the map keeps its size unless the height says otherwise");
                Assert.AreEqual(GameHud.BagInventoryGap * scale, mapBox.yMin - inventoryBox.yMax, slack, size + " one gap between map and inventory");
                Assert.LessOrEqual(handsBox.yMax, mapBox.yMin, size + " the item slots sit under the map");
                Assert.AreEqual(bookBox.yMax, mapBox.yMax, slack, size + " the map column starts level with the book");
                Assert.GreaterOrEqual(map.rect.width, 325f, size + " the map keeps its floor");
                Assert.AreEqual(map.rect.width, map.rect.height, .5f);
                Assert.IsTrue(mapBox.Contains(frameBox.center) && frameBox.width <= mapBox.width + slack, size + " the map sits in its column");
                foreach (var (column, box) in new[] { (left, leftBox), (right, rightBox) })
                {
                    var icon = (RectTransform)column.Find("Trash");
                    var iconBox = OnScreen(icon);
                    Assert.IsTrue(box.Contains(iconBox.min) && box.Contains(iconBox.max), size + " the bin fits its column");
                    Assert.GreaterOrEqual(icon.rect.width, BagDrag.IconMin - .5f, size + " the bin is big enough to read");
                    Assert.AreEqual(BagDrag.IconSize(column.rect.width), icon.rect.width, .5f, size + " the bin follows the column width");
                }
                Assert.AreEqual(bookBox.xMax - GameHud.BookCloseInset * scale, OnScreen(close).xMax, slack, size + " the x keeps to the book's corner");
                Assert.GreaterOrEqual(grid.cellSize.x, 188f, size.ToString());
                Assert.Greater(hud.LongestRecipe, 0);
                Assert.GreaterOrEqual(grid.cellSize.x, GameHud.BookCardFloor(hud.LongestRecipe) - .01f, size + " every card holds its longest word's tiles");
                Assert.GreaterOrEqual(grid.constraintCount, 3, size.ToString());
                Debug.Log($"[Wreckabulary] bag layout {size}: drop columns {right.rect.width:0.#}, map {map.rect.width:0.#}, book {book.rect.width:0.#} x {grid.constraintCount}");
            }
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        static Rect OnScreen(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
