using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    /// <summary>Co-op Moving Day: deliveries, spelling furniture, placing it in the right room, stars and time outs.</summary>
    public class MovingDayTests
    {
        MovingDayDirector director;
        PlayerJoinManager joins;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            yield return TestScenes.Load(Session.MovingDayScene);
            director = Object.FindAnyObjectByType<MovingDayDirector>();
            joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            joins.Join(new ScriptedBinding());
        }

        [UnityTearDown]
        public IEnumerator TearDown() => TestScenes.Reset();

        IEnumerator UntilPlaying() =>
            TestScenes.WaitUntil(() => director.Current == MovingDayDirector.State.Playing, 5f, "level start");

        /// <summary>Builds an item straight into a room, as if spelled there, and lets it settle.</summary>
        Smashable PlaceIn(string word, string room, float offset = 0f)
        {
            var at = director.RoomNamed(room).Centre + new Vector3(0f, 0.3f, offset);
            return FurnitureCatalog.Spawn(word, at, 0f, World.Transient);
        }

        [UnityTest]
        public IEnumerator BoxesForTheChecklistArrive()
        {
            yield return UntilPlaying();
            yield return new WaitForSeconds(director.CurrentLevel.items.Length * 1.2f + 0.5f);
            var boxes = Object.FindObjectsByType<Smashable>().Where(s => !s.GetComponent<LetterBuilt>()).Select(s => s.Word).ToList();
            foreach (var item in director.CurrentLevel.items)
                CollectionAssert.Contains(boxes, item.word, $"a box labelled {item.word}");
        }

        [UnityTest]
        public IEnumerator SpellingAChecklistWordBuildsFurniture()
        {
            var p = joins.Join(new ScriptedBinding());
            yield return UntilPlaying();
            Assert.AreEqual(0, p.Inventory.Count, "Moving Day starts empty-handed");
            p.Inventory.Set("BEDX");

            Assert.IsTrue(p.Summoner.Summon("BED"));
            Assert.IsFalse(p.Summoner.Summon("AXE"), "disabled words remain unavailable in co-op");
            yield return null;
            Assert.IsTrue(Object.FindObjectsByType<LetterBuilt>().Any(b => b.word == "BED"));
        }

        [UnityTest]
        public IEnumerator TheModeOffersAllEnabledRecipesAndSeparatesToolsFromChecklistFurniture()
        {
            yield return UntilPlaying();
            var p = joins.Players[0];
            p.Inventory.Collects = false;
            CollectionAssert.AreEquivalent(GameConfig.Current.Items.Enabled.Select(i => i.Id).ToArray(),
                p.Summoner.WordsOverride.Select(w => w.word).ToArray());
            CollectionAssert.AreEquivalent(director.CurrentLevel.items.Select(i => i.word).Distinct().ToArray(),
                p.Summoner.ChecklistPlacementWords.ToArray());
            p.Inventory.Set("MAT");
            Assert.IsTrue(p.Summoner.Summon("MAT"));
            Assert.AreEqual("MAT", p.Combat.Weapon.word);
            p.Combat.ResetForRound();
            p.Inventory.Set("LAMP");
            Assert.IsTrue(p.Summoner.Summon("LAMP"));
            Assert.IsFalse(p.Combat.Weapon);
            Assert.IsTrue(Object.FindObjectsByType<Smashable>().Any(s => s.Word == "LAMP" && s.GetComponent<LetterBuilt>()));
        }

        [UnityTest]
        public IEnumerator CancellingAToolRefundsItsLettersAndSpentSoapCannotStrandTheChecklist()
        {
            yield return UntilPlaying();
            director.StopAllCoroutines();
            foreach (var supply in Object.FindObjectsByType<Smashable>()) Object.Destroy(supply.gameObject);
            yield return null;
            var p = joins.Players[0];
            p.Inventory.Collects = false;
            p.Inventory.Set("LAMPSO");
            Assert.IsTrue(p.Summoner.BeginCraft(GameAssets.I.words.Find("SOAP")));
            Assert.AreEqual(4, p.Inventory.ReservedCount);
            p.Summoner.CancelCraft();
            Assert.AreEqual("ALMOPS", new string(p.Inventory.Letters.OrderBy(c => c).ToArray()));
            Assert.AreEqual(0, p.Inventory.ReservedCount);
            Assert.IsTrue(p.Summoner.Summon("SOAP"));
            Assert.AreEqual("SOAP", p.Combat.Weapon.word);
            Assert.IsTrue(p.Combat.DeployHeld());
            yield return TestScenes.WaitUntil(() => !p.Combat.IsDeploying, 1f, "co-op SOAP channel");
            Assert.IsFalse(p.Combat.Weapon);
            Assert.AreEqual("LM", new string(p.Inventory.Letters.OrderBy(c => c).ToArray()), "A and P were spent on SOAP");
            typeof(MovingDayDirector).GetField("nextResupply", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(director, 0f);
            typeof(MovingDayDirector).GetMethod("Resupply", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(director, null);
            Assert.AreEqual(1, Object.FindObjectsByType<Smashable>().Count(s => s.Word == "LAMP" && !s.GetComponent<LetterBuilt>()),
                "replacement delivery restores the missing objective letters after creative consumable use");
        }

        [UnityTest]
        public IEnumerator FurnitureOnlyCountsInItsRoom()
        {
            yield return UntilPlaying();
            PlaceIn("BED", "LivingRoom");
            yield return new WaitForSeconds(1.5f);
            Assert.IsTrue(director.Remaining.Any(i => i.word == "BED"), "a BED in the living room doesn't count");

            PlaceIn("BED", "Bedroom");
            yield return TestScenes.WaitUntil(() => director.Remaining.All(i => i.word != "BED"), 3f, "BED placed in the bedroom");
        }

        [UnityTest]
        public IEnumerator ResupplyDoesNotDuplicateARecipeReservedByAnActiveCraft()
        {
            yield return UntilPlaying();
            yield return TestScenes.WaitUntil(() => Object.FindObjectsByType<Smashable>().Any(s => s.Word == "BED"), 3f, "first BED delivery");
            foreach (var box in Object.FindObjectsByType<Smashable>().Where(s => s.Word == "BED")) Object.Destroy(box.gameObject);
            yield return null;
            var p = joins.Players[0];
            p.Inventory.Set("BED");
            Assert.IsTrue(p.Summoner.BeginCraft(new WordEntry { word = "BED", category = WordCategory.Furniture }));
            Assert.AreEqual(0, p.Inventory.Count);
            Assert.IsTrue(p.Summoner.IsCrafting);
            typeof(MovingDayDirector).GetField("nextResupply", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(director, 0f);
            typeof(MovingDayDirector).GetMethod("Resupply", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(director, null);
            Assert.IsFalse(Object.FindObjectsByType<Smashable>().Any(s => s.Word == "BED"), "reserved BED letters count as an allocated recipe");
        }

        [UnityTest]
        public IEnumerator PlacedFurnitureIsLockedInPlace()
        {
            yield return UntilPlaying();
            var lamp = PlaceIn("LAMP", "Bedroom");
            yield return TestScenes.WaitUntil(() => director.Remaining.All(i => i.word != "LAMP"), 3f, "LAMP placed");
            Assert.IsTrue(lamp.GetComponent<Rigidbody>().isKinematic);
            lamp.TakeHit(999f);
            Assert.IsFalse(lamp.IsBroken, "placed furniture can't be wrecked");
        }

        [UnityTest]
        public IEnumerator FinishingQuicklyEarnsThreeStarsAndCanRestart()
        {
            yield return UntilPlaying();
            float z = -3f;
            foreach (var item in director.CurrentLevel.items)
            {
                PlaceIn(item.word, item.room, z);
                z += 1.8f;
            }
            yield return TestScenes.WaitUntil(() => director.Current == MovingDayDirector.State.Complete, 5f, "level complete");
            Assert.AreEqual(3, director.Stars);
            Assert.AreEqual(3, Session.MovingDayStars[0]);

            director.Retry();
            yield return null;
            Assert.AreEqual(0, director.LevelIndex);
            Assert.AreEqual(MovingDayDirector.State.Countdown, director.Current);
            Assert.AreEqual(director.CurrentLevel.items.Length, director.Remaining.Count);
        }

        [UnityTest]
        public IEnumerator RunningOutOfTimeRestartsTheLevel()
        {
            yield return UntilPlaying();
            PlaceIn("SOFA", "LivingRoom");
            director.TimeLeft = 0.2f;
            yield return TestScenes.WaitUntil(() => director.Current == MovingDayDirector.State.OutOfTime, 2f, "out of time");
            Assert.AreEqual(0, director.Stars);
            var hud = Object.FindAnyObjectByType<GameHud>();
            Assert.IsTrue(hud.ResultShown, "the result card says the truck is leaving");
            hud.UiCanvas.transform.Find("Safe HUD/Result/Result card/Next").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return TestScenes.WaitUntil(() => director.Current == MovingDayDirector.State.Countdown, 2f, "retry");
            Assert.AreEqual(0, director.LevelIndex);
            Assert.AreEqual(director.CurrentLevel.items.Length, director.Remaining.Count, "checklist resets");
        }

        [UnityTest]
        public IEnumerator TypewriterStartsMovingDaySolo()
        {
            yield return TestScenes.Reset();
            yield return TestScenes.Load(Session.HubScene);
            Object.FindAnyObjectByType<PlayerJoinManager>().Join(new ScriptedBinding());
            var typewriter = Object.FindAnyObjectByType<Typewriter>();
            int index = typewriter.Modes.Select((m, i) => (m, i)).First(x => x.m.scene == Session.MovingDayScene).i;

            Assert.IsTrue(typewriter.Choose(index), "one roommate can play Moving Day");
            yield return TestScenes.WaitForActive(Session.MovingDayScene);
            var p = Object.FindAnyObjectByType<PlayerJoinManager>().Players.Single();
            Assert.AreEqual(12, p.Summoner.WordsOverride.Count, "the word wheel offers objectives and creative tools");
        }
    }
}
