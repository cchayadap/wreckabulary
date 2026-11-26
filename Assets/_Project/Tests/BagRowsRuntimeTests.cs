using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public class BagRowsRuntimeTests
    {
        const string Sack = "QASACK";
        const float Slow = 10f;
        string modeBefore;

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
            Match.ModeOverride = HouseModes.Dibs;
            var items = GameConfig.Current.Items;
            if (!items.TryGet(Sack, out var item))
            {
                item = new ItemDefinition
                {
                    Id = Sack, Category = ItemCategory.Utilities, Tier = ItemTier.Expanded, Consumable = true,
                    Family = HandlingFamily.Helper, Hands = 1, Model = "Items/" + Sack, HeldScale = .8f,
                    Grip = new[] { 0f, .1f, 0f }, Size = new[] { .4f, .36f, .22f },
                };
                items.Add(item);
            }
            item.Enabled = true;
            GameConfig.Current.BagRows.Remove(Sack);
            GameConfig.Current.BagRows.Add(new BagRowSpec { Id = Sack, Rows = 1 });
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameConfig.Current.BagRows.Remove(Sack);
            if (GameConfig.Current.Items.TryGet(Sack, out var item)) item.Enabled = false;
            Match.ModeOverride = modeBefore;
            LetterInventory.ClearAllBonusRows();
            SummonedThing.ClearAll();
            World.ClearTransient();
            yield return TestScenes.Reset();
        }

        static PlayerController Spawn(int maxLetters = 10)
        {
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, DesktopBinding.Shared);
            var rules = Match.Rules.Clone();
            rules.SpawnProtectionSeconds = 0f;
            rules.LettersDroppedPerHit = 0;
            rules.MaxLetters = maxLetters;
            player.Health.UseRules(rules);
            player.Respawn(Vector3.zero);
            player.Inventory.Collects = false;
            player.Summoner.WordsOverride = new List<WordEntry>
            {
                new WordEntry { word = Sack, category = WordCategory.Movement },
                new WordEntry { word = "BAT", category = WordCategory.Weapon },
            };
            return player;
        }

        static void FillBothHands(PlayerController player)
        {
            Assert.IsTrue(player.Combat.TryEquip(CatalogGear.Create(GameConfig.Current.Items.Get("BAT"))));
            Assert.IsTrue(player.Combat.TryEquip(CatalogGear.Create(GameConfig.Current.Items.Get("BLADE"))));
            Assert.IsFalse(player.Combat.HasFreeGearSlot, "both hands are full");
        }

        static IEnumerator Hud(PlayerController player, System.Action<GameHud, RectTransform> ready)
        {
            var hud = new GameObject("Bag rows HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            yield return TestScenes.WaitUntil(() => hud.LocalPlayer == player, Slow, "the HUD to follow the player");
            yield return new WaitForSecondsRealtime(.12f);
            ready(hud, (RectTransform)hud.transform.Find("Safe HUD"));
        }

        static bool Shown(RectTransform safe, string path) { var t = safe.Find(path); return t && t.gameObject.activeInHierarchy; }

        [UnityTest]
        public IEnumerator SpellingTheBagWithBothHandsFullAddsFiveSlotsAndMakesNoGear()
        {
            var player = Spawn();
            yield return null;
            FillBothHands(player);
            var first = player.Combat.GearIn(0);
            var second = player.Combat.GearIn(1);
            int gearBefore = Object.FindObjectsByType<HeldWeapon>().Length;
            player.Inventory.Set(Sack + "XYZ");
            GameHud hud = null; RectTransform safe = null;
            yield return Hud(player, (h, s) => { hud = h; safe = s; });
            Assert.AreEqual(2, hud.TrayRows);
            Assert.IsFalse(Shown(safe, "Letter bag/Letter 11"));
            int gained = 0;
            System.Action<LetterInventory> onRows = inv => { if (inv == player.Inventory) gained++; };
            LetterInventory.RowsChanged += onRows;
            try
            {
                Assert.IsTrue(player.Summoner.CanCraft(player.Summoner.Recipe(Sack)), "a bag needs no free hand");
                Assert.IsTrue(player.Summoner.BeginCraft(player.Summoner.Recipe(Sack)));
                Assert.AreEqual(3, player.Inventory.Count, "its letters are spent");
                yield return TestScenes.WaitUntil(() => !player.Summoner.IsCrafting, Slow, "the bag to be spelled");
                Assert.AreEqual(1, player.Inventory.BonusRows);
                Assert.AreEqual(15, player.Inventory.Capacity);
                Assert.AreEqual(1, gained);
                Assert.AreEqual("XYZ", new string(new List<char>(player.Inventory.Letters).ToArray()));
                Assert.AreSame(first, player.Combat.GearIn(0), "nothing is dropped or swapped");
                Assert.AreSame(second, player.Combat.GearIn(1));
                Assert.AreEqual(gearBefore, Object.FindObjectsByType<HeldWeapon>().Length, "no gear object is made");
                yield return TestScenes.WaitUntil(() => hud.TrayRows == 3, Slow, "the third row to appear");
                Assert.IsTrue(Shown(safe, "Letter bag/Letter 15"));
                Assert.IsFalse(Shown(safe, "Letter bag/Letter 16"));
                Assert.AreEqual(BagRows.TrayHeight(3), ((RectTransform)safe.Find("Letter bag")).sizeDelta.y, .5f);
                Assert.IsNull(safe.Find("Letter bag/Spell key"));
                Assert.IsNull(safe.Find("Letter bag/Bag link"));
                hud.ToggleBag();
                yield return TestScenes.WaitUntil(() => hud.BagOpen, Slow, "the bag panel");
                yield return new WaitForSecondsRealtime(.2f);
                Assert.IsTrue(Shown(safe, "Bag panel/Bag/Letters/Big letter 15"));
                Assert.IsFalse(Shown(safe, "Bag panel/Bag/Letters/Big letter 16"));
                Assert.AreEqual("3/15", safe.Find("Bag panel/Bag/Letters/Count").GetComponent<TMPro.TMP_Text>().text);
                Assert.AreEqual(3, hud.BagRowsShown);
            }
            finally { LetterInventory.RowsChanged -= onRows; }
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator ASecondBagIsRefusedBeforeAnyLetterIsSpent()
        {
            var player = Spawn();
            yield return null;
            player.Inventory.Set(Sack);
            Assert.IsTrue(player.Summoner.Summon(Sack), "the first bag is spelled at once");
            Assert.AreEqual(0, player.Inventory.Count, "its letters are spent");
            Assert.AreEqual(1, player.Inventory.BonusRows);
            player.Inventory.Set(Sack);
            Assert.IsFalse(SummonEffects.CanApply(player, player.Summoner.Recipe(Sack)));
            Assert.IsTrue(SummonEffects.RefusesBagRow(player, player.Summoner.Recipe(Sack)), "the refusal is the row limit");
            Assert.IsFalse(player.Summoner.BeginCraft(player.Summoner.Recipe(Sack)));
            Assert.IsFalse(player.Summoner.Summon(Sack));
            Assert.AreEqual(Sack.Length, player.Inventory.Count, "letters kept");
            Assert.AreEqual(1, player.Inventory.BonusRows);
            Assert.AreEqual(15, player.Inventory.Capacity);
            Assert.IsFalse(player.Summoner.IsCrafting);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator TheRowSurvivesAKnockoutAndARespawnAndEndsWithTheRound()
        {
            var player = Spawn();
            yield return null;
            Assert.IsTrue(player.Inventory.AddBonusRow());
            player.Health.ApplyDamage(Hits.Of(null, Vector3.forward, HitSource.Melee, 1000f, 1f));
            yield return TestScenes.WaitUntil(() => player.IsKnockedOut || player.Health.IsDowned, Slow, "the knockout");
            Assert.AreEqual(1, player.Inventory.BonusRows, "a knockout keeps the row");
            player.Health.ResetForRound();
            player.Respawn(Vector3.zero);
            player.Inventory.Set(Match.Rules.StarterLetters ?? "");
            yield return null;
            Assert.AreEqual(1, player.Inventory.BonusRows, "a respawn keeps the row");
            player.Inventory.Set("ABCDEFGHIJKLM");
            Assert.AreEqual(13, player.Inventory.Count);
            World.FreezeTransient();
            Assert.AreEqual(0, player.Inventory.BonusRows, "the round end clears it");
            Assert.AreEqual(10, player.Inventory.Capacity);
            Assert.AreEqual(13, player.Inventory.Count, "no letter is destroyed when the bag shrinks");
            Assert.IsTrue(player.Inventory.IsFull);
            Assert.IsFalse(player.Inventory.TryAdd('Z'), "nothing more fits until there is room");
            Assert.AreEqual(13, player.Inventory.ShownCells, "every kept letter is still shown");
            Assert.AreEqual(3, player.Inventory.ShownRows);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator ABagOutsideAHouseModeIsNotOffered()
        {
            Match.ModeOverride = "Hub";
            var player = Spawn();
            yield return null;
            player.Inventory.Set(Sack);
            Assert.IsFalse(SummonEffects.CanApply(player, player.Summoner.Recipe(Sack)));
            Assert.IsFalse(player.Summoner.Summon(Sack));
            Assert.AreEqual(0, player.Inventory.BonusRows);
            Assert.AreEqual(Sack.Length, player.Inventory.Count);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator AnEscapeSizedBagShowsFourRows()
        {
            var player = Spawn(20);
            yield return null;
            player.Inventory.Set("ABCDEFGHIJKL");
            GameHud hud = null; RectTransform safe = null;
            yield return Hud(player, (h, s) => { hud = h; safe = s; });
            yield return TestScenes.WaitUntil(() => hud.TrayRows == 4, Slow, "four rows for twenty letters");
            Assert.IsTrue(Shown(safe, "Letter bag/Letter 20"));
            Assert.IsFalse(Shown(safe, "Letter bag/Letter 21"));
            Assert.AreEqual("L", safe.Find("Letter bag/Letter 12/Face/Letter").GetComponent<TMPro.TMP_Text>().text);
            hud.ToggleBag();
            yield return TestScenes.WaitUntil(() => hud.BagOpen, Slow, "the bag panel");
            yield return new WaitForSecondsRealtime(.2f);
            Canvas.ForceUpdateCanvases();
            Assert.IsTrue(Shown(safe, "Bag panel/Bag/Letters/Big letter 20"));
            Assert.IsFalse(Shown(safe, "Bag panel/Bag/Letters/Big letter 21"));
            Assert.AreEqual("12/20", safe.Find("Bag panel/Bag/Letters/Count").GetComponent<TMPro.TMP_Text>().text);
            var last = (RectTransform)safe.Find("Bag panel/Bag/Letters/Big letter 20");
            var hands = (RectTransform)safe.Find("Bag panel/Bag/Hands");
            var corners = new Vector3[4];
            last.GetWorldCorners(corners);
            float lastBottom = corners[0].y;
            hands.GetWorldCorners(corners);
            Assert.Greater(lastBottom, corners[1].y - .5f, "the fourth row stays above the hand cards");
            Assert.IsTrue(player.Inventory.AddBonusRow(), "Escape grows to 25");
            yield return TestScenes.WaitUntil(() => hud.TrayRows == 5, Slow, "five rows");
            Assert.IsTrue(Shown(safe, "Letter bag/Letter 25"));
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }
    }
}
