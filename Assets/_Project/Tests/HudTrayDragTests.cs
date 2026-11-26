using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class HudTrayDragTests
    {
        const float Slow = 10f, NearPlayer = 4f, SourceAlpha = .35f;
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
            if (keys != null && keys.added) InputSystem.RemoveDevice(keys);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            InputSystem.settings.editorInputBehaviorInPlayMode = route;
            InputSystem.settings.backgroundBehavior = focus;
            Time.timeScale = 1f;
            DesktopBinding.Typing = false;
            TouchBinding.Shared.ReleaseAll();
            SummonedThing.ClearAll();
            World.ClearTransient();
            yield return TestScenes.Reset();
        }

        static PlayerController Spawn(int maxLetters = 0)
        {
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, DesktopBinding.Shared);
            if (maxLetters > 0)
            {
                var rules = Match.Rules.Clone();
                rules.MaxLetters = maxLetters;
                player.Health.UseRules(rules);
            }
            player.Respawn(Vector3.zero);
            player.Inventory.Collects = false;
            return player;
        }

        static IEnumerator ShowTray(PlayerController player, string letters, System.Action<GameHud, RectTransform> ready)
        {
            var hud = new GameObject("Tray drag HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            yield return new WaitForSecondsRealtime(.12f);
            player.Inventory.Set(letters);
            var safe = (RectTransform)hud.transform.Find("Safe HUD");
            string first = letters.OrderBy(c => c).First().ToString();
            yield return TestScenes.WaitUntil(() => hud.LocalPlayer == player && TileText(safe, 1) == first, Slow, "the tray to show the letters");
            Assert.IsFalse(hud.BagOpen, "the bag stays shut; this is the always-visible tray");
            yield return new WaitForSecondsRealtime(.1f);
            Canvas.ForceUpdateCanvases();
            ready(hud, safe);
        }

        static string TileText(RectTransform safe, int cell) =>
            safe.Find($"Letter bag/Letter {cell}/Face/Letter").GetComponent<TMPro.TMP_Text>().text;

        static Transform Tile(RectTransform safe, int cell) => safe.Find($"Letter bag/Letter {cell}");

        static Transform Slot(RectTransform safe, int hand) => safe.Find($"Letter bag/Gear slot {hand}");

        static Vector2 Centre(Transform t)
        {
            var rt = (RectTransform)t;
            return RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
        }

        static Vector2 TrayFloor(RectTransform safe)
        {
            var tray = (RectTransform)safe.Find("Letter bag");
            var rect = tray.rect;
            return RectTransformUtility.WorldToScreenPoint(null, tray.TransformPoint(new Vector3(rect.xMin + 100f, rect.yMin + 20f, 0f)));
        }

        static Vector2 Away => new(Screen.width * .3f, Screen.height * .62f);

        static string Order(GameHud hud) => new(hud.BagLetterOrder.ToArray());

        static LetterTile LooseTile(char letter) => Object.FindObjectsByType<LetterTile>()
            .FirstOrDefault(tile => tile && tile.gameObject.activeInHierarchy && tile.Letter == letter);

        static float Flat(Vector3 a, Vector3 b) => Vector3.Distance(new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, b.z));

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

        IEnumerator Wheel(Vector2 at, float scroll)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = at, scroll = new Vector2(0f, scroll) });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = at });
            yield return null;
        }

        static Transform Arrow(RectTransform safe, string which) => safe.Find($"Letter bag/Letter scroll/{which}");

        static bool Shown(RectTransform safe, int cell) => Tile(safe, cell).gameObject.activeInHierarchy;

        IEnumerator Click(Vector2 at)
        {
            yield return Point(at, false);
            yield return Point(at, true);
            yield return Point(at, false);
        }

        [UnityTest]
        public IEnumerator DraggingATrayLetterOutDropsItAtThePlayer()
        {
            var player = Spawn();
            GameHud hud = null; RectTransform safe = null;
            yield return ShowTray(player, "BAT", (h, s) => { hud = h; safe = s; });
            Assert.AreEqual("ABT", Order(hud));
            var ghost = safe.Find("Tray drag ghost");
            Assert.IsNotNull(ghost, "the tray has its own drag ghost");
            Assert.IsFalse(ghost.gameObject.activeSelf);
            Assert.IsNull(LooseTile('T'));

            yield return Press(Centre(Tile(safe, 3)), Away);
            Assert.IsTrue(hud.TrayDragging, "pressing a tray tile and moving drags it");
            Assert.IsTrue(ghost.gameObject.activeSelf, "the tile follows the pointer");
            Assert.AreEqual("T", ghost.Find("Letter/Face/Letter").GetComponent<TMPro.TMP_Text>().text);
            Assert.IsTrue(hud.TrayDropShown, "outside the tray the bin says letting go drops");
            Assert.AreEqual(SourceAlpha, Tile(safe, 3).GetComponent<CanvasGroup>().alpha, 1e-3f, "the source tile dims");
            Assert.AreEqual(3, player.Inventory.Count, "nothing drops before the release");

            yield return Point(Centre(Tile(safe, 1)), true);
            Assert.IsFalse(hud.TrayDropShown, "back over the tray there is no bin");
            yield return Point(Away, true);
            yield return Point(Away, false);
            yield return TestScenes.WaitUntil(() => player.Inventory.Count == 2, Slow, "the letter to leave the inventory");
            Assert.AreEqual("AB", Order(hud));
            Assert.IsFalse(ghost.gameObject.activeSelf, "the ghost is gone");
            LetterTile tile = null;
            yield return TestScenes.WaitUntil(() => (tile = LooseTile('T')) != null, Slow, "a T pickup in the world");
            yield return new WaitForSeconds(.6f);
            Assert.Less(Flat(tile.transform.position, player.transform.position), NearPlayer, "the T lands at the player");
            var cam = Camera.main;
            if (cam && new Plane(Vector3.up, Vector3.zero).Raycast(cam.ScreenPointToRay(Away), out float far))
            {
                var pointed = cam.ScreenPointToRay(Away).GetPoint(far);
                if (Flat(pointed, player.transform.position) > 2f * NearPlayer)
                    Assert.Greater(Flat(tile.transform.position, pointed), NearPlayer, "not where the pointer was let go");
            }
            Assert.IsFalse(hud.BagOpen);
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator DraggingAHeldItemOffTheTrayDropsIt()
        {
            var player = Spawn();
            GameHud hud = null; RectTransform safe = null;
            yield return ShowTray(player, "BATXYZ", (h, s) => { hud = h; safe = s; });
            Assert.IsTrue(player.Summoner.Summon("BAT"));
            var bat = player.Combat.Weapon;
            Assert.IsNotNull(bat);
            yield return new WaitForSecondsRealtime(.3f);

            yield return Press(Centre(Slot(safe, 1)), Away);
            Assert.IsTrue(hud.TrayDragging, "the item in hand can be dragged off the tray");
            Assert.IsTrue(safe.Find("Tray drag ghost/Item").gameObject.activeSelf, "its picture follows the pointer");
            yield return Point(Away, false);
            yield return TestScenes.WaitUntil(() => !player.Combat.IsHolding && !bat.IsHeld, Slow, "the bat to be dropped");
            Assert.IsNull(player.Combat.GearIn(0));
            Assert.IsTrue(bat.gameObject.activeInHierarchy, "dropped on the ground, not deleted");
            Assert.Less(Flat(bat.transform.position, player.transform.position), NearPlayer, "at the player");
            Assert.AreEqual(3, player.Inventory.Count, "the letters stay");
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator AReleaseBackOnTheTrayDropsNothing()
        {
            var player = Spawn();
            GameHud hud = null; RectTransform safe = null;
            yield return ShowTray(player, "BAT", (h, s) => { hud = h; safe = s; });

            yield return Drag(Centre(Tile(safe, 1)), TrayFloor(safe));
            Assert.AreEqual("ABT", Order(hud), "off a tile but inside the tray goes back");
            Assert.AreEqual(3, player.Inventory.Count);
            Assert.AreEqual(1f, Tile(safe, 1).GetComponent<CanvasGroup>().alpha, 1e-3f, "the tile looks full again");

            yield return Drag(Centre(Tile(safe, 3)), Centre(Tile(safe, 1)));
            yield return TestScenes.WaitUntil(() => Order(hud) == "TAB", Slow, "a tile dropped on another tile reorders, as in the bag");
            yield return TestScenes.WaitUntil(() => TileText(safe, 1) == "T", Slow, "the tray shows the new order");
            Assert.AreEqual(3, player.Inventory.Count, "reordering keeps every letter");
            yield return new WaitForSecondsRealtime(.3f);
            Assert.IsNull(LooseTile('T'));
            Assert.IsNull(LooseTile('A'));
            Assert.IsFalse(hud.BagOpen);
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator APressOnTheTrayNeverAttacks()
        {
            var player = Spawn();
            GameHud hud = null; RectTransform safe = null;
            yield return ShowTray(player, "BAT", (h, s) => { hud = h; safe = s; });

            yield return Click(Centre(Tile(safe, 4)));
            yield return new WaitForSeconds(.3f);
            Assert.IsFalse(player.Combat.IsChanneling, "a click on an empty tray tile does not punch");
            yield return Click(Centre(Tile(safe, 1)));
            yield return new WaitForSeconds(.3f);
            Assert.IsFalse(player.Combat.IsChanneling, "nor on a letter");
            Assert.AreEqual(3, player.Inventory.Count, "and a click is not a drop");

            player.Inventory.Set("BALLXY");
            Assert.IsTrue(player.Summoner.Summon("BALL"));
            var ball = player.Combat.Weapon;
            Assert.IsNotNull(ball);
            int thrown = 0;
            player.Combat.Thrown += _ => thrown++;
            yield return TestScenes.WaitUntil(() => TileText(safe, 1) == "X", Slow, "the tray to show X and Y");

            var from = Centre(Tile(safe, 1));
            yield return Point(from, false);
            yield return Point(from, true);
            yield return new WaitForSeconds(DesktopScheme.HoldSeconds + .2f);
            Assert.IsFalse(player.Combat.IsAiming, "holding on the tray does not aim");
            for (int i = 1; i <= DragSteps; i++) yield return Point(Vector2.Lerp(from, Away, i / (float)DragSteps), true);
            yield return new WaitForSeconds(DesktopScheme.HoldSeconds + .2f);
            Assert.IsFalse(player.Combat.IsAiming, "nor once the drag leaves the tray");
            Assert.IsFalse(player.Combat.IsBlocking);
            yield return Point(Away, false);
            yield return TestScenes.WaitUntil(() => player.Inventory.Count == 1, Slow, "the X to drop");
            yield return new WaitForSeconds(.3f);
            Assert.AreEqual(0, thrown, "letting go of a tray drag never throws");
            Assert.AreSame(ball, player.Combat.Weapon, "the BALL stays in hand");

            yield return Click(Away);
            yield return TestScenes.WaitUntil(() => thrown == 1, 2f, "a click in the world still throws");
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator APlainClickOnAnItemSlotStillSelectsThatHand()
        {
            var player = Spawn();
            GameHud hud = null; RectTransform safe = null;
            yield return ShowTray(player, "BATBLADEXY", (h, s) => { hud = h; safe = s; });
            Assert.IsTrue(player.Summoner.Summon("BAT"));
            var bat = player.Combat.Weapon;
            Assert.IsTrue(player.Summoner.Summon("BLADE"));
            Assert.AreEqual(1, player.Combat.ActiveSlot);
            yield return new WaitForSecondsRealtime(.3f);

            yield return Click(Centre(Slot(safe, 1)));
            yield return TestScenes.WaitUntil(() => player.Combat.ActiveSlot == 0, Slow, "a click on slot 1 selects that hand");
            Assert.AreSame(bat, player.Combat.Weapon);
            Assert.IsNotNull(player.Combat.GearIn(1), "nothing was dropped");
            Assert.IsFalse(hud.TrayDragging);
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator DroppingIsRefusedWhereTheDropKeyIsRefused()
        {
            var player = Spawn();
            GameHud hud = null; RectTransform safe = null;
            yield return ShowTray(player, "BAT", (h, s) => { hud = h; safe = s; });
            Assert.IsTrue(player.Summoner.Summon("BAT"));
            var bat = player.Combat.Weapon;
            player.Inventory.Set("XY");
            yield return TestScenes.WaitUntil(() => TileText(safe, 1) == "X", Slow, "the tray to show X and Y");

            player.Frozen = true;
            yield return Press(Centre(Tile(safe, 1)), Away);
            Assert.IsTrue(hud.TrayDragging, "the tile can still be picked up");
            Assert.IsFalse(hud.TrayDropShown, "but no bin shows while the player cannot drop");
            yield return Point(Away, false);
            yield return Drag(Centre(Slot(safe, 1)), Away);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(2, player.Inventory.Count, "a frozen player keeps the letter, as with the drop key");
            Assert.IsNull(LooseTile('X'));
            Assert.AreSame(bat, player.Combat.Weapon, "and the item");
            Assert.IsTrue(bat.IsHeld);

            player.Frozen = false;
            yield return Drag(Centre(Tile(safe, 1)), Away);
            yield return TestScenes.WaitUntil(() => player.Inventory.Count == 1, Slow, "the same drag drops once the player can act");
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator WithTheSpellBoxOpenTheTrayDoesNotDrag()
        {
            var player = Spawn();
            GameHud hud = null; RectTransform safe = null;
            yield return ShowTray(player, "BAT", (h, s) => { hud = h; safe = s; });
            player.Summoner.Open();
            yield return TestScenes.WaitUntil(() => hud.ComposerOpen, Slow, "the spell box to open");

            yield return Press(Centre(Tile(safe, 1)), Away);
            Assert.IsFalse(hud.TrayDragging, "the letters belong to the word being spelled");
            Assert.IsFalse(safe.Find("Tray drag ghost").gameObject.activeSelf);
            yield return Point(Away, false);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(3, player.Inventory.Count, "nothing drops while spelling");
            Assert.IsNull(LooseTile('A'));
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator ALetterInTheThirdRowDragsOutLikeAnyOther()
        {
            var player = Spawn();
            Assert.IsTrue(player.Inventory.AddBonusRow());
            GameHud hud = null; RectTransform safe = null;
            yield return ShowTray(player, "ABCDEFGHIJKL", (h, s) => { hud = h; safe = s; });
            yield return TestScenes.WaitUntil(() => hud.TrayRows == 3, Slow, "the third row");
            yield return new WaitForSecondsRealtime(.5f);
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(12, player.Inventory.Count);
            Assert.AreEqual("L", TileText(safe, 12));
            Assert.IsNull(safe.Find("Letter bag/Spell key"), "the hint chips are gone, so the row has room");
            yield return Drag(Centre(Tile(safe, 12)), Away);
            yield return TestScenes.WaitUntil(() => player.Inventory.Count == 11, Slow, "the third-row letter to drop");
            Assert.AreEqual("ABCDEFGHIJK", Order(hud));
            Assert.IsNotNull(LooseTile('L'));
            player.Inventory.ClearBonusRows();
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator ATwentyCellBagShowsThreeRowsAndScrollsWithTheWheelAndTheArrows()
        {
            var player = Spawn(20);
            GameHud hud = null; RectTransform safe = null;
            yield return ShowTray(player, "ABCDEFGHIJKLMNOPQR", (h, s) => { hud = h; safe = s; });
            yield return TestScenes.WaitUntil(() => hud.TrayBagRows == 4, Slow, "a twenty-slot bag");
            Assert.AreEqual(3, hud.TrayRows, "three rows at most");
            Assert.AreEqual(0, hud.TrayTop);
            Assert.AreEqual(BagRows.TrayHeight(3), ((RectTransform)safe.Find("Letter bag")).sizeDelta.y, .5f);
            Assert.IsTrue(Shown(safe, 15));
            Assert.IsFalse(Shown(safe, 16), "row four waits below the window");
            Assert.IsTrue(Arrow(safe, "Up").gameObject.activeInHierarchy && Arrow(safe, "Down").gameObject.activeInHierarchy, "two arrows beside the grid");
            Assert.AreEqual(TrayWindow.ArrowDimmed, Arrow(safe, "Up").GetComponent<CanvasGroup>().alpha, 1e-3f, "up is dimmed at the top");
            Assert.AreEqual(1f, Arrow(safe, "Down").GetComponent<CanvasGroup>().alpha, 1e-3f);
            var handAt = ((RectTransform)Slot(safe, 1)).anchoredPosition;

            yield return Wheel(Centre(Tile(safe, 8)), -120f);
            yield return TestScenes.WaitUntil(() => hud.TrayTop == 1, Slow, "the wheel to scroll down a row");
            yield return new WaitForSecondsRealtime(.1f);
            Assert.IsTrue(Shown(safe, 16));
            Assert.AreEqual("P", TileText(safe, 16));
            Assert.IsFalse(Shown(safe, 5), "row one scrolled out");
            Assert.AreEqual(TrayWindow.ArrowDimmed, Arrow(safe, "Down").GetComponent<CanvasGroup>().alpha, 1e-3f, "down is dimmed at the bottom");
            yield return Wheel(Centre(Tile(safe, 8)), -120f);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.AreEqual(1, hud.TrayTop, "the wheel stops at the last row");
            yield return Wheel(Away, 120f);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.AreEqual(1, hud.TrayTop, "the wheel away from the grid does nothing");

            yield return Click(Centre(Arrow(safe, "Up")));
            yield return TestScenes.WaitUntil(() => hud.TrayTop == 0, Slow, "the up arrow to scroll back");
            yield return Click(Centre(Arrow(safe, "Down")));
            yield return TestScenes.WaitUntil(() => hud.TrayTop == 1, Slow, "the down arrow to scroll on");
            yield return new WaitForSeconds(.3f);
            Assert.IsFalse(player.Combat.IsChanneling, "a click on an arrow does not punch");
            Assert.AreEqual(18, player.Inventory.Count, "scrolling never drops a letter");
            Assert.AreEqual(handAt, ((RectTransform)Slot(safe, 1)).anchoredPosition, "the item slots never move");
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator ALetterAddedToRowFourScrollsIntoViewAndSpendingScrollsBack()
        {
            var player = Spawn(20);
            GameHud hud = null; RectTransform safe = null;
            yield return ShowTray(player, "ABCDEFGHIJKLMNO", (h, s) => { hud = h; safe = s; });
            yield return TestScenes.WaitUntil(() => hud.TrayBagRows == 4, Slow, "a twenty-slot bag");
            Assert.AreEqual(0, hud.TrayTop);
            Assert.IsTrue(player.Inventory.TryAdd('Z'));
            yield return TestScenes.WaitUntil(() => hud.TrayTop == 1, Slow, "the window to follow the Z into row four");
            yield return TestScenes.WaitUntil(() => Shown(safe, 16) && TileText(safe, 16) == "Z", Slow, "the Z in view");
            player.Inventory.Set("AB");
            yield return TestScenes.WaitUntil(() => hud.TrayTop == 0, Slow, "the window to come back to the letters left");
            Assert.IsTrue(Shown(safe, 1));
            Assert.AreEqual("A", TileText(safe, 1));
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator ADragFromTheVisibleThirdRowDropsTheRightLetterAfterScrolling()
        {
            var player = Spawn(20);
            GameHud hud = null; RectTransform safe = null;
            yield return ShowTray(player, "ABCDEFGHIJKLMNOPQR", (h, s) => { hud = h; safe = s; });
            yield return TestScenes.WaitUntil(() => hud.TrayBagRows == 4, Slow, "a twenty-slot bag");
            Assert.IsTrue(hud.ScrollTray(1));
            yield return new WaitForSecondsRealtime(.2f);
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(1, hud.TrayTop);
            Assert.IsFalse(Shown(safe, 3), "a hidden cell takes no pointer, so no drag starts on it");
            Assert.AreEqual("Q", TileText(safe, 17));
            yield return Drag(Centre(Tile(safe, 17)), Away);
            yield return TestScenes.WaitUntil(() => player.Inventory.Count == 17, Slow, "the third visible row's letter to drop");
            Assert.AreEqual("ABCDEFGHIJKLMNOPR", Order(hud), "the Q, the letter under the pointer, is the one dropped");
            LetterTile tile = null;
            yield return TestScenes.WaitUntil(() => (tile = LooseTile('Q')) != null, Slow, "a Q pickup in the world");
            Assert.AreEqual(1, hud.TrayTop, "the window keeps its place while filled rows show");
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator TheItemSlotWearBarShowsShrinksAndHides()
        {
            var player = Spawn();
            GameHud hud = null; RectTransform safe = null;
            yield return ShowTray(player, "BATXYZ", (h, s) => { hud = h; safe = s; });
            Assert.IsTrue(player.Summoner.Summon("BAT"));
            var bat = player.Combat.Weapon;
            Assert.IsNotNull(bat);
            int most = bat.Definition.Durability;
            Assert.Greater(most, 0);
            var wear = Slot(safe, 1).Find("Wear");
            Assert.IsNotNull(wear, "the slot has a wear bar");
            var fill = wear.Find("Left").GetComponent<Image>();
            yield return TestScenes.WaitUntil(() => wear.gameObject.activeInHierarchy, Slow, "the bar to show under the BAT");
            Assert.AreEqual(1f, fill.fillAmount, 1e-3f, "a new item is at full durability");
            Assert.IsFalse(Slot(safe, 2).Find("Wear").gameObject.activeInHierarchy, "an empty slot has no bar");
            var full = fill.color;
            var bar = (RectTransform)wear;
            var icon = (RectTransform)Slot(safe, 1).Find("Icon");
            var word = (RectTransform)Slot(safe, 1).Find("Word");
            var corners = new Vector3[4];
            bar.GetWorldCorners(corners);
            float barTop = corners[1].y;
            icon.GetWorldCorners(corners);
            Assert.GreaterOrEqual(corners[0].y, barTop - .5f, "the icon sits above the bar");
            word.GetWorldCorners(corners);
            Assert.GreaterOrEqual(corners[0].y, barTop - .5f, "the word sits above the bar");

            Assert.IsFalse(bat.Wear(most * .5f, player.Combat));
            yield return TestScenes.WaitUntil(() => Mathf.Abs(fill.fillAmount - .5f) < .02f, Slow, "the bar to shrink after a hit wears the BAT");
            Assert.AreEqual(full, fill.color, "half worn is not yet a warning");
            Assert.IsFalse(bat.Wear(most * .3f, player.Combat));
            yield return TestScenes.WaitUntil(() => fill.fillAmount < SlotWear.LowFraction, Slow, "the bar to shrink again");
            yield return TestScenes.WaitUntil(() => fill.color != full, Slow, "a nearly worn item turns the bar to the warning colour");

            hud.ToggleBag();
            yield return TestScenes.WaitUntil(() => hud.BagOpen, Slow, "the bag panel");
            yield return new WaitForSecondsRealtime(.2f);
            var bagWear = safe.Find("Bag panel/Bag/Hands/Bag hand 1/Wear");
            Assert.IsTrue(bagWear.gameObject.activeInHierarchy, "the bag panel's hand card keeps its bar");
            Assert.AreEqual(fill.fillAmount, bagWear.Find("Left").GetComponent<Image>().fillAmount, .02f);
            hud.ToggleBag();
            yield return TestScenes.WaitUntil(() => !hud.BagOpen, Slow, "the bag panel to close");

            player.Combat.Drop();
            yield return TestScenes.WaitUntil(() => !wear.gameObject.activeInHierarchy, Slow, "the bar to hide when the slot empties");
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }
    }
}
