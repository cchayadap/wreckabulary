using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class LobbyPlayGridTests
    {
        static readonly string[] Keys = { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey, QuickPlayChoices.StorageKey, FriendRooms.StorageKey,
            Wreckabulary.Rules.LastPlayChoice.StorageKey };
        static readonly Vector2[] Screens = { new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(1680, 1050), new Vector2(1024, 768), new Vector2(1248, 576) };
        const float Gutter = 12f, Slack = .5f, ReferenceHeight = 1080f, PageInset = 60f, Viewport = 760f, SmallestLabel = 12f;
        const int PlaceRow = 3;
        string[] saved;
        bool hadTutorial;
        int tutorial;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saved = Keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            hadTutorial = PlayerPrefs.HasKey(TutorialDirector.DoneKey); tutorial = PlayerPrefs.GetInt(TutorialDirector.DoneKey, 0);
            yield return TestScenes.Reset();
            foreach (string key in Keys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.SetInt(TutorialDirector.DoneKey, 1);
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.WaitUntil(() => LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning, 3f, "lobby ready");
            LobbyMenu.Instance.Open(LobbyMenu.Play);
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.Reset();
            for (int i = 0; i < Keys.Length; i++)
                if (saved[i] == null) PlayerPrefs.DeleteKey(Keys[i]); else PlayerPrefs.SetString(Keys[i], saved[i]);
            if (hadTutorial) PlayerPrefs.SetInt(TutorialDirector.DoneKey, tutorial); else PlayerPrefs.DeleteKey(TutorialDirector.DoneKey);
            PlayerPrefs.Save();
            WordboundSoloEntry.Clear();
        }

        [Test]
        public void EveryMapRowSharesTheHouseFootprintWhateverItsCount()
        {
            foreach (var screen in Screens)
            {
                float available = screen.x / screen.y * ReferenceHeight - PageInset;
                var (houseColumns, house) = LobbyPlayCardGrid.Layout(available, Gutter, 264, PlaceRow, Viewport, 5, 5);
                foreach (int count in new[] { 1, 3, 5, 6 })
                {
                    string where = screen + " with " + count + " maps";
                    var (columns, cell) = LobbyPlayCardGrid.Layout(available, Gutter, 264, PlaceRow, Viewport, count, 5);
                    Assert.AreEqual(house, cell, where + ": one footprint");
                    Assert.AreEqual(Mathf.Min(count, houseColumns), columns, where + ": the same columns, left-aligned");
                }
            }
        }

        [Test]
        public void TheMapRowFillsEveryScreenWithBalancedRowsAndWholePictures()
        {
            foreach (var screen in Screens)
                foreach (int count in new[] { 1, 2, 3, 5, 6, 7, 10 })
                {
                    float available = screen.x / screen.y * ReferenceHeight - PageInset;
                    string where = screen + " with " + count + " maps";
                    var (columns, cell) = LobbyPlayCardGrid.Layout(available, Gutter, 264, 6, Viewport, count);
                    int rows = LobbyPlayCardGrid.Rows(count, columns);
                    Assert.LessOrEqual(columns, count, where + ": never an empty column");
                    Assert.LessOrEqual(columns * cell.x + (columns - 1) * Gutter, available + Slack, where + ": the row fits the page");
                    Assert.GreaterOrEqual(cell.x, LobbyPlayCardGrid.ShortestCard * .5f, where);
                    Assert.LessOrEqual(cell.x, LobbyPlayCardGrid.PreferredWidth + Slack, where + ": cards stop at their widest");
                    if (rows > 1)
                    {
                        int last = count - (rows - 1) * columns;
                        Assert.GreaterOrEqual(last * 2, columns, where + ": no orphan card on the last row");
                        Assert.Greater(last, 0, where);
                    }
                    if (cell.x < LobbyPlayCardGrid.PreferredWidth - Slack && cell.y < Viewport - LobbyPlayCardGrid.ViewportMargin - Slack)
                        Assert.That(columns * cell.x + (columns - 1) * Gutter, Is.EqualTo(available).Within(1f), where + ": a row that can grow fills the width");
                    Assert.That((cell.x - LobbyPlayCardGrid.Inset * 2) / (cell.y - LobbyPlayCardGrid.CaptionHeight - LobbyPlayCardGrid.Inset),
                        Is.EqualTo(LobbyPlayCardGrid.PictureAspect).Within(.01f), where + ": the picture stays 16:9");
                }
            Assert.AreEqual(4, LobbyPlayCardGrid.Balanced(7, 6), "seven maps take four and three, not six and one");
            Assert.AreEqual(3, LobbyPlayCardGrid.Balanced(5, 4), "five maps on a narrow page take three and two");
            Assert.AreEqual(5, LobbyPlayCardGrid.Balanced(5, 6));
        }

        [UnityTest]
        public IEnumerator HouseAndMapStepsFillTheRowAtFiveScreenSizes()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Dibs", "pinwheel");
            menu.Page<PlayPage>().WordboundSolo = false; menu.Open(LobbyMenu.Play);
            yield return null;
            using var viewport = new ScreenSize(menu);
            foreach (var screen in Screens)
            {
                viewport.Set(screen.x, screen.y);
                Pick(PlayPage.ModeButton("Dibs"));
                yield return null;
                Next("GO");
                yield return TestScenes.WaitUntil(() => Cards("Map ").Length == GameConfig.Current.Houses.Count, 3f, screen + " houses");
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var houses = Cards("Map ");
                AssertRows(houses, screen + " houses");
                AssertFillsRow(houses, screen + " houses");
                Pick(PlayPage.ModeButton("TimeRun"));
                yield return null;
                Next("Solo GO");
                yield return TestScenes.WaitUntil(() => Cards("Solo map ").Length == PlayPage.SoloMaps.Length, 3f, screen + " courses");
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var courses = Cards("Solo map ");
                AssertRows(courses, screen + " courses");
                CollectionAssert.AreEqual(PlayPage.SoloMaps.Select(id => "Solo map " + id).ToArray(), courses.Select(card => card.name).ToArray());
                AssertFillsRow(courses, screen + " courses");
                foreach (string id in PlayPage.SoloMaps)
                    Assert.AreEqual(LobbyMapSnapshots.Trait(id), courses.Single(card => card.name == "Solo map " + id).GetComponentsInChildren<TMP_Text>()
                        .Single(text => text.name == "Map detail").text, screen + " " + id + " shows its floors line");
                Pick(PlayPage.ModeButton("Dibs"));
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ModeTilesFitEveryCategoryAtFiveScreenSizes()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Dibs", "pinwheel");
            menu.Page<PlayPage>().WordboundSolo = false; menu.Open(LobbyMenu.Play);
            yield return null;
            using var viewport = new ScreenSize(menu);
            foreach (var screen in Screens)
            {
                viewport.Set(screen.x, screen.y);
                foreach (var family in PlayPage.FamilyOrder)
                {
                    string first = Wreckabulary.Rules.HouseModes.ModesIn(family).First();
                    Pick(PlayPage.ModeButton(first));
                    yield return null; yield return null; yield return null;
                    Canvas.ForceUpdateCanvases();
                    string where = screen + " " + family;
                    var step = Page.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == PlayPage.ModeTiles);
                    var tiles = step.GetComponentsInChildren<Button>();
                    CollectionAssert.AreEqual(Wreckabulary.Rules.HouseModes.ModesIn(family).Select(PlayPage.ModeButton).ToArray(), tiles.Select(tile => tile.name).ToArray(),
                        where + ": one tile per mode of the open category");
                    var scroll = Page.GetComponentInChildren<LobbyPlayPageView>().Choices.GetComponentInChildren<ScrollRect>();
                    AssertTilesFit(tiles, InPage(scroll.viewport), where);
                    var categories = Page.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == PlayPage.CategoryRow).GetComponentsInChildren<Button>();
                    AssertCardsFit(categories, where + " categories");
                }
            }
        }

        static void AssertTilesFit(Button[] tiles, Rect view, string where)
        {
            var bounds = tiles.Select(tile => InPage((RectTransform)tile.transform)).ToArray();
            for (int i = 0; i < tiles.Length; i++)
            {
                string name = where + " " + tiles[i].name;
                Assert.That(bounds[i].width, Is.EqualTo(bounds[0].width).Within(Slack), name + ": one tile width");
                Assert.That(bounds[i].height, Is.EqualTo(bounds[0].height).Within(Slack), name + ": one tile height");
                Assert.GreaterOrEqual(bounds[i].xMin, view.xMin - Slack, name); Assert.LessOrEqual(bounds[i].xMax, view.xMax + Slack, name);
                for (int j = i + 1; j < tiles.Length; j++) Assert.IsFalse(bounds[i].Overlaps(Shrunk(bounds[j])), name + " overlaps " + tiles[j].name);
                foreach (var text in tiles[i].GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate();
                    Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, name + ": " + text.text);
                    Assert.GreaterOrEqual(text.fontSize, 15f - .01f, name + ": " + text.text);
                }
            }
        }

        static void AssertFillsRow(Button[] cards, string where)
        {
            var grid = cards[0].transform.parent.GetComponent<GridLayoutGroup>();
            var fit = grid.GetComponent<LobbyPlayCardGrid>();
            Assert.IsTrue(fit.FillPage, where + ": the place step sizes its cards to the page");
            var view = InPage(Page.GetComponentInChildren<LobbyPlayPageView>().Choices.GetComponentInChildren<ScrollRect>().viewport);
            var row = InPage((RectTransform)grid.transform);
            bool shown = cards.All(card => { var at = InPage((RectTransform)card.transform); return at.yMin >= view.yMin - Slack && at.yMax <= view.yMax + Slack; });
            Assert.That(grid.cellSize.y, Is.EqualTo(LobbyPlayCardGrid.HeightFor(grid.cellSize.x)).Within(Slack), where + ": a whole 16:9 picture and its caption");
            if (!shown)
            {
                Assert.LessOrEqual(grid.cellSize.x, LobbyPlayCardGrid.PreferredWidth + Slack, where + ": a page too short for every card keeps the steady footprint and scrolls");
                return;
            }
            int across = grid.constraintCount;
            float filled = Mathf.Min(Wreckabulary.Rules.PlaceGrid.CappedWidth(row.width, grid.spacing.x), (row.width - (across - 1) * grid.spacing.x) / across);
            Assert.That(grid.cellSize.x, Is.EqualTo(filled).Within(Slack), where + ": the cards fill the row, or stop at the three-column width");
            Assert.GreaterOrEqual(grid.cellSize.x, fit.MinimumWidth - Slack, where + ": cards stay readable");
        }

        static void AssertCardsFit(Button[] cards, string where)
        {
            foreach (var card in cards)
                foreach (var text in card.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate();
                    Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, where + " " + card.name + ": " + text.text);
                }
        }

        static RectTransform Page => LobbyMenu.Instance.Page<PlayPage>().Root;

        static Button[] Cards(string prefix) => Page.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith(prefix)).ToArray();

        static Button Present(string name) => Page.GetComponentsInChildren<Button>().FirstOrDefault(each => each.name == name);

        static void Press(Button button)
        {
            Assert.IsNotNull(button);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Canvas.ForceUpdateCanvases();
        }

        static void Pick(string name)
        {
            if (!Present(name) && Present(PlayPage.BackToModes)) Press(Present(PlayPage.BackToModes));
            if (!Present(name))
            {
                var family = Wreckabulary.Rules.HouseModes.FamilyOf(name.Substring(name.IndexOf(' ') + 1));
                Press(Present(PlayPage.CategoryCard + Wreckabulary.Rules.HouseModes.FamilyName(family)));
            }
            Press(Present(name));
        }

        static void Next(string name)
        {
            var next = Present(name);
            Assert.IsNotNull(next, name);
            Assert.IsTrue(next.IsInteractable(), name);
            Press(next);
            Assert.IsNotNull(Present(PlayPage.BackToModes), name + " opens the place step");
        }

        static void AssertRows(Button[] cards, string where)
        {
            Assert.IsNotEmpty(cards, where);
            var grid = cards[0].transform.parent.GetComponent<GridLayoutGroup>();
            Assert.IsNotNull(grid, where + ": map cards share one grid");
            Assert.LessOrEqual(grid.constraintCount, cards.Length, where + ": no empty column");
            var scroll = Page.GetComponentInChildren<LobbyPlayPageView>().Choices.GetComponentInChildren<ScrollRect>();
            var view = InPage(scroll.viewport);
            var bounds = cards.Select(card => InPage((RectTransform)card.transform)).ToArray();
            var first = bounds[0];
            int columns = grid.constraintCount;
            for (int i = 0; i < cards.Length; i++)
            {
                var at = bounds[i];
                string name = where + " " + cards[i].name;
                Assert.That(at.width, Is.EqualTo(first.width).Within(Slack), name + ": one card width");
                Assert.That(at.height, Is.EqualTo(first.height).Within(Slack), name + ": one row height");
                Assert.GreaterOrEqual(at.xMin, view.xMin - Slack, name + ": not off the left"); Assert.LessOrEqual(at.xMax, view.xMax + Slack, name + ": not off the right");
                int row = i / columns, column = i % columns;
                Assert.That(at.xMin, Is.EqualTo(first.xMin + column * (first.width + grid.spacing.x)).Within(1f), name + ": on the column grid");
                Assert.That(at.yMax, Is.EqualTo(first.yMax - row * (first.height + grid.spacing.y)).Within(1f), name + ": on the row grid");
                for (int j = i + 1; j < cards.Length; j++)
                    Assert.IsFalse(at.Overlaps(Shrunk(bounds[j])), name + " overlaps " + cards[j].name);
                if (column + 1 < columns && i + 1 < cards.Length)
                    Assert.AreSame(cards[i + 1], cards[i].FindSelectableOnRight(), name + ": right moves to the next map");
                if (i + columns < cards.Length)
                    Assert.AreSame(cards[i + columns], cards[i].FindSelectableOnDown(), name + ": down moves to the map below");
                foreach (var text in cards[i].GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate();
                    Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, name + ": " + text.text);
                    Assert.GreaterOrEqual(text.fontSize, SmallestLabel - .01f, name + ": " + text.text);
                }
            }
            var picture = cards[0].GetComponentsInChildren<RectTransform>().FirstOrDefault(part => part.name == "Picture frame" || part.name == "Map fallback");
            Assert.IsNotNull(picture, where);
            var shape = InPage(picture);
            Assert.That(shape.width / shape.height, Is.EqualTo(LobbyPlayCardGrid.PictureAspect).Within(.02f), where + ": 16:9 picture");
        }

        static Rect Shrunk(Rect rect) => Rect.MinMaxRect(rect.xMin + Slack, rect.yMin + Slack, rect.xMax - Slack, rect.yMax - Slack);

        static Rect InPage(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var min = Page.InverseTransformPoint(corners[0]);
            var max = Page.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        sealed class ScreenSize : System.IDisposable
        {
            readonly Canvas canvas;
            readonly CanvasScaler scaler;
            readonly Camera camera;
            readonly Vector2 size;
            readonly float scale, aspect;
            readonly bool scaled;
            readonly RectTransform safe;

            public ScreenSize(LobbyMenu menu)
            {
                canvas = menu.GetComponent<Canvas>(); scaler = menu.GetComponent<CanvasScaler>(); camera = menu.Stage.Camera;
                safe = (RectTransform)menu.transform.Find("Safe area");
                size = safe.sizeDelta; scale = canvas.scaleFactor; scaled = scaler.enabled; aspect = camera.aspect;
                scaler.enabled = false; canvas.scaleFactor = Screen.height / ReferenceHeight;
                Canvas.ForceUpdateCanvases();
            }

            public void Set(float width, float height)
            {
                var requested = new Vector2(width / height * ReferenceHeight, ReferenceHeight);
                safe.sizeDelta += requested - safe.rect.size;
                camera.aspect = width / height;
                Canvas.ForceUpdateCanvases();
            }

            public void Dispose()
            {
                if (safe) safe.sizeDelta = size;
                if (camera) camera.aspect = aspect;
                if (canvas) canvas.scaleFactor = scale;
                if (scaler) scaler.enabled = scaled;
                Canvas.ForceUpdateCanvases();
            }
        }
    }
}
