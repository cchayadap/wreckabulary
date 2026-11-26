using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public class LetterDropsRuntimeTests
    {
        const int Smashes = 24;
        string modeBefore;
        System.Random randomBefore;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Test Ground";
            ground.transform.position = Vector3.down * 0.5f;
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            TilePool.Ensure();
            modeBefore = Match.ModeOverride;
            randomBefore = Smashable.DropRandom;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Match.ModeOverride = modeBefore;
            Smashable.DropRandom = randomBefore;
            if (TilePool.Instance) TilePool.Instance.ReleaseAll();
            World.ClearTransient();
            yield return TestScenes.Reset();
        }

        static string Sorted(IEnumerable<char> letters) => new string(letters.OrderBy(c => c).ToArray());

        static bool FromWord(string word, string letters)
        {
            var spare = word.ToList();
            return letters.All(spare.Remove);
        }

        static IEnumerator SmashOne(string word, int seed, System.Action<Smashable> prepare, System.Action<string, string> dropped)
        {
            TilePool.Instance.ReleaseAll();
            Smashable.DropRandom = new System.Random(seed);
            var piece = FurnitureCatalog.Spawn(word, new Vector3(0f, .05f, 0f), 0f, World.Transient);
            Assert.IsNotNull(piece, word + " spawns");
            Assert.AreEqual(word, piece.Word);
            piece.GetComponent<Rigidbody>().isKinematic = true;
            prepare?.Invoke(piece);
            yield return null;
            piece.Break();
            yield return null;
            dropped(Sorted(TilePool.Instance.Active.Where(t => t && t.gameObject.activeSelf).Select(t => t.Letter)), Sorted(piece.Dropped));
        }

        [UnityTest]
        public IEnumerator ASmashedTableInAHouseModeDropsThreeToFiveOfItsOwnLetters()
        {
            Match.ModeOverride = HouseModes.Dibs;
            var counts = new HashSet<int>();
            for (int seed = 0; seed < Smashes; seed++)
            {
                string tiles = null, recorded = null;
                yield return SmashOne("TABLE", seed, null, (t, r) => { tiles = t; recorded = r; });
                Assert.That(tiles.Length, Is.InRange(3, 5), $"seed {seed} dropped '{tiles}'");
                Assert.IsTrue(FromWord("TABLE", tiles), $"seed {seed} dropped '{tiles}', which is not from TABLE");
                Assert.AreEqual(recorded, tiles, "the piece records exactly the letters that fell");
                counts.Add(tiles.Length);
            }
            Assert.Greater(counts.Count, 1, "the count changes from smash to smash");
            Assert.IsTrue(counts.Any(c => c < 5), "some smashes drop part of the word");
        }

        [UnityTest]
        public IEnumerator ASeedDropsTheSameLettersEveryTime()
        {
            Match.ModeOverride = HouseModes.Duos;
            string first = null, second = null;
            yield return SmashOne("CABINET", 7, null, (t, _) => first = t);
            yield return SmashOne("CABINET", 7, null, (t, _) => second = t);
            Assert.AreEqual(first, second);
            Assert.That(first.Length, Is.InRange(5, 7));
        }

        [UnityTest]
        public IEnumerator TheTutorialAndScenesOutsideHousePlayDropEveryLetter()
        {
            foreach (string mode in new[] { HouseModes.Tutorial, "Default", "Hub" })
            {
                Match.ModeOverride = mode;
                for (int seed = 0; seed < 6; seed++)
                {
                    string tiles = null;
                    yield return SmashOne("TABLE", seed, null, (t, _) => tiles = t);
                    Assert.AreEqual("ABELT", tiles, mode + " drops every letter of TABLE");
                }
            }
        }

        [UnityTest]
        public IEnumerator SpelledThingsAndMarkedPiecesDropEveryLetter()
        {
            Match.ModeOverride = HouseModes.Dibs;
            for (int seed = 0; seed < 6; seed++)
            {
                string whole = null, spelled = null;
                yield return SmashOne("TABLE", seed, p => p.DropsWhole = true, (t, _) => whole = t);
                Assert.AreEqual("ABELT", whole, "a piece marked to drop whole keeps every letter");
                yield return SmashOne("TABLE", seed, p => SummonedThing.Attach(p.gameObject, "TABLE", null, 999f), (t, _) => spelled = t);
                Assert.AreEqual("ABELT", spelled, "letters a player spelled come back in full");
            }
        }

        [UnityTest]
        public IEnumerator LettersAGoalStillNeedsAlwaysDrop()
        {
            Match.ModeOverride = HouseModes.Dibs;
            System.Func<IEnumerable<string>> goal = () => new[] { "BAT" };
            Smashable.AddDropGoals(goal);
            try
            {
                for (int seed = 0; seed < Smashes; seed++)
                {
                    string tiles = null;
                    yield return SmashOne("TABLE", seed, null, (t, _) => tiles = t);
                    Assert.IsTrue(FromWord(tiles, "BAT"), $"seed {seed} dropped '{tiles}' while BAT was still needed");
                }
                for (int seed = 0; seed < 6; seed++)
                {
                    string tiles = null;
                    yield return SmashOne("LAMP", seed, p => p.MustDrop = "MP", (t, _) => tiles = t);
                    Assert.IsTrue(FromWord(tiles, "MP"), $"a piece's own must-drop letters fall: '{tiles}'");
                    Assert.That(tiles.Length, Is.InRange(3, 4));
                }
            }
            finally
            {
                Smashable.RemoveDropGoals(goal);
            }
            var counts = new HashSet<int>();
            for (int seed = 0; seed < Smashes; seed++)
            {
                string tiles = null;
                yield return SmashOne("TABLE", seed, null, (t, _) => tiles = t);
                counts.Add(tiles.Length);
            }
            Assert.IsTrue(counts.Any(c => c < 5), "once the goal is gone the drop is random again");
        }
    }
}
