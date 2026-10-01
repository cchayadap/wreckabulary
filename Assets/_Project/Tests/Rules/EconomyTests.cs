using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    /// <summary>Letters and items (brief §5 and §9): nothing is ever lost or duplicated.</summary>
    [TestFixture]
    public class EconomyTests
    {
        [Test]
        public void ContestedPickupGoesToExactlyOnePlayer()
        {
            var e = TestData.NewEconomy();
            var bat = TestData.Craft(e, 0, "BAT");
            Assert.IsTrue(e.Drop(0, 0).Ok);
            int seen = bat.Revision;

            var first = e.PickUp(1, bat.Id, seen);
            var second = e.PickUp(0, bat.Id, seen);

            Assert.IsTrue(first.Ok);
            Assert.AreEqual(Refusal.StaleRevision, second.Refusal);
            Assert.AreEqual(1, bat.HolderId);
            Assert.AreEqual(bat.Id, e.Player(1).Slots[0]);
            Assert.AreEqual(-1, e.Player(0).Slots[0]);
            TestData.AssertConserved(e);
        }

        [Test]
        public void CraftingReservesTheLettersAndCancellingReturnsThemAll()
        {
            var e = TestData.NewEconomy();
            TestData.GiveLetters(e, 0, "BATXL");
            var p = e.Player(0);

            Assert.IsTrue(e.BeginCraft(0, "BAT", 10).Ok);
            Assert.AreEqual("LX", p.Letters.ToString());
            Assert.AreEqual("ABT", p.Reserved.ToString());
            Assert.AreEqual(5, p.LetterCount, "reserved letters still count towards the bag");
            TestData.AssertConserved(e, "while crafting");

            Assert.IsTrue(e.CancelCraft(0).Ok);
            Assert.AreEqual("ABLTX", p.Letters.ToString());
            Assert.IsTrue(p.Reserved.IsEmpty);
            Assert.IsFalse(p.IsCrafting);
            Assert.AreEqual(Refusal.NotCrafting, e.CancelCraft(0).Refusal);
            TestData.AssertConserved(e, "after cancelling");
        }

        [Test]
        public void CraftingTakesTimeByLetterCount()
        {
            var e = TestData.NewEconomy();
            TestData.GiveLetters(e, 0, "BAT");
            Assert.IsTrue(e.BeginCraft(0, "BAT", 10).Ok);
            double ready = e.Player(0).Craft.ReadyAt;
            Assert.AreEqual(10 + 0.6 + 3 * 0.12, ready, 1e-6);
            Assert.AreEqual(Refusal.TooEarly, e.CompleteCraft(0, ready - 0.01).Refusal);
            var done = e.CompleteCraft(0, ready);
            Assert.IsTrue(done.Ok);
            Assert.AreEqual("BAT", done.Item.Word);
            Assert.AreEqual(ItemState.Held, done.Item.State);
            Assert.AreEqual(ItemOrigin.Crafted, done.Item.Origin);
            Assert.AreEqual(0, done.Item.CrafterId);
            Assert.IsTrue(e.Player(0).Letters.IsEmpty && e.Player(0).Reserved.IsEmpty);
            TestData.AssertConserved(e);
        }

        [Test]
        public void CraftRefusalsChangeNothing()
        {
            var e = TestData.NewEconomy();
            TestData.GiveLetters(e, 0, "BAL");
            var p = e.Player(0);
            Assert.AreEqual(Refusal.MissingLetters, e.BeginCraft(0, "BALL", 0).Refusal);
            Assert.AreEqual(Refusal.UnknownRecipe, e.BeginCraft(0, "LAB", 0).Refusal);
            TestData.GiveLetters(e, 0, "HMER");
            Assert.AreEqual(Refusal.RecipeDisabled, e.BeginCraft(0, "HAMMER", 0).Refusal, "modelled but not built yet");
            Assert.AreEqual("ABEHLMR", p.Letters.ToString());
            Assert.IsTrue(p.Reserved.IsEmpty);
            Assert.AreEqual(Refusal.AlreadyCrafting, Second(e));
            TestData.AssertConserved(e);
        }

        static Refusal Second(Economy e)
        {
            TestData.GiveLetters(e, 0, "T");
            Assert.IsTrue(e.BeginCraft(0, "BAT", 0).Ok);
            return e.BeginCraft(0, "BAT", 0).Refusal;
        }

        [Test]
        public void TwoSlotsThenHandsAreFull()
        {
            var e = TestData.NewEconomy();
            TestData.Craft(e, 0, "BAT");
            TestData.GiveLetters(e, 0, "LAMP");
            Assert.IsTrue(e.BeginCraft(0, "LAMP", 0).Ok);
            TestData.GiveLetters(e, 0, "BALL");
            Assert.AreEqual(Refusal.AlreadyCrafting, e.BeginCraft(0, "BALL", 0).Refusal);
            Assert.IsTrue(e.CompleteCraft(0, 50).Ok);
            Assert.AreEqual(Refusal.HandsFull, e.BeginCraft(0, "BALL", 60).Refusal);
            Assert.AreEqual("ABLL", e.Player(0).Letters.ToString(), "a refused craft keeps the letters");
            TestData.AssertConserved(e);
        }

        [Test]
        public void TheSlotBeingCraftedIntoIsHeld()
        {
            var e = TestData.NewEconomy();
            TestData.Craft(e, 0, "BAT");
            var ball = TestData.Craft(e, 1, "BALL");
            Assert.IsTrue(e.Drop(1, 0).Ok);
            TestData.GiveLetters(e, 0, "MAT");
            Assert.IsTrue(e.BeginCraft(0, "MAT", 0).Ok);
            Assert.AreEqual(Refusal.HandsFull, e.PickUp(0, ball.Id, ball.Revision).Refusal);
            TestData.AssertConserved(e);
        }

        [Test]
        public void TheBagHoldsEighteenLetters()
        {
            var e = TestData.NewEconomy();
            TestData.GiveLetters(e, 0, "ABCDEFGHIJKLMNOPQR");
            Assert.AreEqual(18, e.Player(0).LetterCount);
            var extra = e.MintTiles("S").Tiles[0];
            Assert.AreEqual(Refusal.BagFull, e.CollectTile(0, extra.TileId).Refusal);
            Assert.IsTrue(e.Tiles.ContainsKey(extra.TileId), "the tile stays on the floor");
            Assert.IsTrue(e.CollectTile(1, extra.TileId).Ok);
            Assert.AreEqual(Refusal.NoSuchTile, e.CollectTile(1, extra.TileId).Refusal, "a tile is collected once");
            TestData.AssertConserved(e);
        }

        [Test]
        public void EachPlayerMayDeployTwoThings()
        {
            var e = TestData.NewEconomy();
            var table = TestData.Craft(e, 0, "TABLE");
            Assert.IsTrue(e.Deploy(0, 0).Ok);
            Assert.AreEqual(ItemState.Deployed, table.State);
            TestData.Craft(e, 0, "SOFA");
            Assert.IsTrue(e.Deploy(0, 0).Ok);
            var mat = TestData.Craft(e, 0, "MAT");
            Assert.AreEqual(Refusal.DeployLimit, e.Deploy(0, 0).Refusal);
            Assert.AreEqual(ItemState.Held, mat.State, "a refused deploy keeps the item in hand");
            Assert.AreEqual(2, e.DeployedCount(0));

            Assert.IsTrue(e.PickUp(1, table.Id, table.Revision).Ok, "anyone can take deployed gear");
            Assert.AreEqual(-1, table.DeployerId);
            Assert.AreEqual(1, e.DeployedCount(0));
            Assert.IsTrue(e.Deploy(0, 0).Ok);
            Assert.AreEqual(Refusal.NotDeployable, DeployBat(e));
            TestData.AssertConserved(e);
        }

        static Refusal DeployBat(Economy e)
        {
            TestData.Craft(e, 1, "BAT");
            return e.Deploy(1, 1).Refusal;
        }

        [Test]
        public void ConsumablesSpendTheirLettersAndRefundNothing()
        {
            var e = TestData.NewEconomy();
            var foam = TestData.Craft(e, 0, "FOAM");
            Assert.IsTrue(e.Consume(0, 0).Ok);
            Assert.IsTrue(foam.IsGone);
            Assert.AreEqual(4, e.Spent);
            Assert.AreEqual(0, e.Break(foam.Id).Count, "a used FOAM can't be broken for letters");
            TestData.AssertConserved(e, "FOAM");

            var bomb = TestData.Craft(e, 0, "BOMB");
            Assert.IsTrue(e.Throw(0, 0, "Candy").Ok);
            Assert.IsTrue(bomb.Spent, "throwing lights the fuse");
            Assert.AreEqual(0, bomb.ActivatorId);
            Assert.IsTrue(e.Settle(bomb.Id).Ok);
            Assert.AreEqual(ItemState.Armed, bomb.State);
            Assert.AreEqual(0, e.DeployedCount(0), "a thrown BOMB doesn't use a deploy");
            Assert.AreEqual(0, e.Break(bomb.Id).Count);
            TestData.AssertConserved(e, "BOMB");

            var soap = TestData.Craft(e, 0, "SOAP");
            Assert.IsTrue(e.Deploy(0, 0).Ok);
            Assert.AreEqual(ItemState.Armed, soap.State);
            Assert.AreEqual(1, e.DeployedCount(0), "a SOAP puddle uses a deploy while it lasts");
            Assert.IsTrue(e.Expire(soap.Id).Ok);
            Assert.AreEqual(0, e.DeployedCount(0));
            Assert.AreEqual(Refusal.NoSuchItem, e.Expire(soap.Id).Refusal);
            Assert.AreEqual(4 + 4 + 4, e.Spent);
            TestData.AssertConserved(e, "SOAP");
        }

        [Test]
        public void OnlyUsableConsumablesCanBeConsumed()
        {
            var e = TestData.NewEconomy();
            TestData.Craft(e, 0, "BAT");
            TestData.Craft(e, 0, "BOMB");
            Assert.AreEqual(Refusal.NotConsumable, e.Consume(0, 0).Refusal);
            Assert.AreEqual(Refusal.NotConsumable, e.Consume(0, 1).Refusal, "a BOMB is thrown or placed, not eaten");
            Assert.AreEqual(Refusal.EmptySlot, e.Consume(1, 0).Refusal);
            TestData.AssertConserved(e);
        }

        [Test]
        public void AThrownBallLandsAndCanBePickedUpAgain()
        {
            var e = TestData.NewEconomy();
            var ball = TestData.Craft(e, 0, "BALL");
            Assert.IsTrue(e.Throw(0, 0, "Arcade").Ok);
            Assert.IsFalse(ball.Spent);
            Assert.AreEqual("Arcade", ball.ThrowerSkin);
            Assert.AreEqual(Refusal.WrongState, e.PickUp(1, ball.Id, ball.Revision).Refusal, "can't catch it mid-air");
            Assert.IsTrue(e.Settle(ball.Id).Ok);
            Assert.AreEqual(ItemState.World, ball.State);
            Assert.IsNull(ball.ThrowerSkin);
            Assert.IsTrue(e.PickUp(1, ball.Id, ball.Revision).Ok);
            TestData.AssertConserved(e);
        }

        [Test]
        public void TheSameInstanceSurvivesEveryHandOver()
        {
            var e = TestData.NewEconomy("Duos", 4, new[] { 0, 1, 0, 1 });
            var bat = TestData.Craft(e, 0, "BAT");
            int id = bat.Id;
            Assert.IsTrue(e.Damage(id, 3).Count == 0);
            Assert.IsTrue(e.Transfer(0, 2, 0).Ok);
            Assert.IsTrue(e.Throw(2, 0, "Candy").Ok);
            Assert.IsTrue(e.Settle(id).Ok);
            Assert.IsTrue(e.PickUp(1, id, bat.Revision).Ok);
            Assert.IsTrue(e.Drop(1, 0).Ok);
            Assert.AreSame(bat, e.Item(id));
            Assert.AreEqual(0, bat.CrafterId);
            Assert.AreEqual(bat.MaxDurability - 3, bat.Durability, 1e-6, "wear carries over");
            TestData.AssertConserved(e);
        }

        [Test]
        public void GearAndLettersOnlyGoToTeammates()
        {
            var e = TestData.NewEconomy("Duos", 4, new[] { 0, 1, 0, 1 });
            TestData.Craft(e, 0, "BAT");
            Assert.AreEqual(Refusal.NotTeammate, e.Transfer(0, 1, 0).Refusal);
            Assert.AreEqual(Refusal.NotTeammate, e.Transfer(0, 0, 0).Refusal);
            Assert.IsTrue(e.Transfer(0, 2, 0).Ok);
            Assert.AreEqual(2, e.Item(e.Player(2).Slots[0]).HolderId);

            TestData.GiveLetters(e, 0, "Q");
            Assert.AreEqual(Refusal.NotTeammate, e.GiveLetter(0, 3, 'Q').Refusal);
            Assert.AreEqual(Refusal.MissingLetters, e.GiveLetter(0, 2, 'Z').Refusal);
            Assert.IsTrue(e.GiveLetter(0, 2, 'Q').Ok);
            Assert.AreEqual(1, e.Player(2).Letters['Q']);
            TestData.AssertConserved(e);
        }

        [Test]
        public void BreakingIsIdempotent()
        {
            var e = TestData.NewEconomy();
            var bat = TestData.Craft(e, 0, "BAT");
            var tiles = e.Break(bat.Id);
            Assert.AreEqual("ABT", new string(tiles.Tiles.Select(t => t.Letter).OrderBy(c => c).ToArray()));
            Assert.AreEqual(-1, e.Player(0).Slots[0], "the slot is empty again");
            Assert.AreEqual(0, e.Break(bat.Id).Count);
            Assert.AreEqual(0, e.Damage(bat.Id, 100).Count);
            TestData.AssertConserved(e);
        }

        [Test]
        public void WearBreaksGearIntoItsLetters()
        {
            var e = TestData.NewEconomy();
            var bat = TestData.Craft(e, 0, "BAT");
            Assert.AreEqual(20f, bat.Durability);
            Assert.AreEqual(0, e.Damage(bat.Id, 19).Count);
            Assert.AreEqual(3, e.Damage(bat.Id, 5).Count);
            Assert.IsTrue(bat.IsGone);
            TestData.AssertConserved(e);
        }

        [Test]
        public void OriginalFurnitureBreaksIntoItsWord()
        {
            var e = TestData.NewEconomy();
            var sofa = e.PlaceFurniture("SOFA");
            Assert.AreEqual(1f + 0.6f * 4, sofa.Durability, 1e-5);
            Assert.AreEqual(4, e.Minted);
            for (int i = 0; i < 3; i++) Assert.AreEqual(0, e.Damage(sofa.Id, 1f).Count, "punch " + (i + 1));
            var tiles = e.Damage(sofa.Id, 1f);
            Assert.AreEqual("AFOS", new string(tiles.Tiles.Select(t => t.Letter).OrderBy(c => c).ToArray()));
            TestData.AssertConserved(e);
        }

        [Test]
        public void FurnitureIsCarriedInBothHandsAndThrown()
        {
            var e = TestData.NewEconomy();
            var chair = e.PlaceFurniture("CHAIR");
            Assert.IsTrue(e.PickUp(0, chair.Id, chair.Revision).Ok);
            Assert.AreEqual(ItemState.Carried, chair.State);
            Assert.AreEqual(chair.Id, e.Player(0).CarriedFurniture);
            TestData.GiveLetters(e, 0, "BAT");
            Assert.AreEqual(Refusal.HandsFull, e.BeginCraft(0, "BAT", 0).Refusal, "no crafting with a chair in your arms");
            Assert.IsTrue(e.Throw(0, -1, "Candy").Ok);
            Assert.IsNull(chair.ThrowerSkin, "original furniture has no skins");
            Assert.AreEqual(-1, e.Player(0).CarriedFurniture);
            Assert.IsTrue(e.Settle(chair.Id).Ok);
            Assert.AreEqual(ItemState.World, chair.State);
            TestData.AssertConserved(e);
        }

        [Test]
        public void EliminationDropsLettersAsTilesAndGearIntact()
        {
            // Player 0 holds a BAT and is half-way through a LAMP; player 1 is carrying a chair.
            // (Carrying furniture and crafting can't overlap: both need your hands.)
            var e = TestData.NewEconomy();
            var bat = TestData.Craft(e, 0, "BAT");
            TestData.GiveLetters(e, 0, "XYZLAMP");
            Assert.IsTrue(e.BeginCraft(0, "LAMP", 0).Ok, "crafting");
            var chair = e.PlaceFurniture("CHAIR");
            Assert.IsTrue(e.PickUp(1, chair.Id, chair.Revision).Ok, "lifting the chair");
            int before = e.Audit().Total;

            var dropped = new List<ItemInstance>();
            var tiles = e.Eliminate(0, dropped);

            Assert.AreEqual("ALMPXYZ", new string(tiles.Tiles.Select(t => t.Letter).OrderBy(c => c).ToArray()), "the half-made LAMP drops as letters");
            CollectionAssert.AreEquivalent(new[] { bat.Id }, dropped.Select(i => i.Id));
            Assert.AreEqual(ItemState.World, bat.State);
            Assert.AreEqual(3, bat.LockedLetterCount, "the BAT keeps its letters inside; they are not also dropped");
            Assert.IsFalse(e.Player(0).CanAct);
            Assert.IsTrue(e.Player(0).Letters.IsEmpty && e.Player(0).Reserved.IsEmpty, "nothing left on the player");
            Assert.AreEqual(before, e.Audit().Total);
            Assert.AreEqual(Refusal.NotAlive, e.CollectTile(0, tiles.Tiles[0].TileId).Refusal);

            dropped.Clear();
            Assert.AreEqual(0, e.Eliminate(1, dropped).Count);
            CollectionAssert.AreEquivalent(new[] { chair.Id }, dropped.Select(i => i.Id));
            Assert.AreEqual(ItemState.World, chair.State, "carried furniture is set down");
            Assert.AreEqual(-1, e.Player(1).CarriedFurniture);
            TestData.AssertConserved(e);
        }

        [Test]
        public void KnockLooseTakesTheCommonestLettersAndSparesTheCraft()
        {
            var e = TestData.NewEconomy();
            TestData.GiveLetters(e, 0, "LLLBAT");
            Assert.IsTrue(e.BeginCraft(0, "BAT", 0).Ok);
            var tiles = e.KnockLoose(0, 2);
            Assert.AreEqual("LL", new string(tiles.Tiles.Select(t => t.Letter).ToArray()));
            Assert.AreEqual("ABT", e.Player(0).Reserved.ToString());
            Assert.AreEqual(1, e.KnockLoose(0, 5).Count);
            TestData.AssertConserved(e);
        }

        [Test]
        public void EachChangeRaisesOneEventWithTheItemReady()
        {
            var e = TestData.NewEconomy();
            int changes = 0;
            e.ItemChanged += i =>
            {
                changes++;
                if (i.State == ItemState.Held) Assert.AreEqual(0, i.HolderId, "the event never shows a half-made item");
            };
            var bat = TestData.Craft(e, 0, "BAT");
            Assert.AreEqual(1, changes, "one change: the craft");
            int rev = bat.Revision;
            e.Drop(0, 0);
            Assert.AreEqual(rev + 1, bat.Revision);
            Assert.AreEqual(2, changes);
        }

        /// <summary>
        /// Thousands of random requests from 4 players, including impossible and out-of-date ones.
        /// After every one, every letter must be accounted for and no item may be in two places.
        /// </summary>
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void RandomPlayNeverLosesOrDuplicatesAnything(int seed)
        {
            var e = TestData.NewEconomy("Duos", 4, new[] { 0, 1, 0, 1 });
            var rng = new Random(seed);
            var recipes = e.Catalogue.Enabled.Select(i => i.Id).ToArray();
            string[] furniture = { "BED", "SOFA", "TABLE", "LAMP", "CHAIR", "DESK", "SHELF", "PLANT", "CRATE", "BOX" };
            string[] skins = { "Classic", "Candy", "Arcade", "Gold", null };
            double now = 0;
            var counts = new Dictionary<string, int>();

            // How often each action is tried: building up (collect, craft) is weighted above
            // tearing down (damage, break), so there is always gear around to deploy, throw and use.
            string[] plan =
            {
                "place", "collect", "collect", "collect", "begin", "begin", "begin", "complete", "complete", "complete",
                "cancel", "pickup", "pickup", "drop", "throw", "throw", "settle", "deploy", "deploy", "consume", "consume",
                "expire", "damage", "break", "transfer", "transfer", "give", "toss", "carry", "spill",
            };
            for (int step = 0; step < 10000; step++)
            {
                now += 0.25;
                string op = plan[rng.Next(plan.Length)];
                // Mostly aim at a target the action can work on, sometimes at anything at all,
                // so both the successes and the refusals get exercised.
                var items = e.Items.ToList();
                var fitting = items.Where(i => Fits(e, op, i)).ToList();
                var item = fitting.Count > 0 && rng.Next(4) > 0 ? fitting[rng.Next(fitting.Count)] : items.Count > 0 ? items[rng.Next(items.Count)] : null;
                int p = rng.Next(4);
                int other = rng.Next(4);
                int slot = rng.Next(-1, 3);
                if (item != null && item.State == ItemState.Held && rng.Next(4) > 0)
                {
                    p = item.HolderId;
                    slot = Array.IndexOf(e.Player(p).Slots, item.Id);
                }
                var me = e.Player(p);
                var affordable = recipes.Where(r => me.Letters.Contains(e.Catalogue.Get(r).Letters)).ToList();
                string recipe = affordable.Count > 0 && rng.Next(4) > 0 ? affordable[rng.Next(affordable.Count)] : recipes[rng.Next(recipes.Length)];
                bool ok;
                switch (op)
                {
                    case "place": e.PlaceFurniture(furniture[rng.Next(furniture.Length)]); ok = true; break;
                    case "spill": ok = e.MintTiles(recipes[rng.Next(recipes.Length)]).Count > 0; break;
                    case "collect":
                        var tileIds = e.Tiles.Keys.ToList();
                        ok = tileIds.Count > 0 && e.CollectTile(p, tileIds[rng.Next(tileIds.Count)]).Ok;
                        break;
                    case "begin": ok = e.BeginCraft(p, recipe, now).Ok; break;
                    case "complete": ok = e.CompleteCraft(p, now).Ok; break;
                    case "cancel": ok = e.CancelCraft(p).Ok; break;
                    case "pickup": ok = item != null && e.PickUp(p, item.Id, item.Revision - (rng.Next(8) == 0 ? 1 : 0)).Ok; break;
                    case "drop": ok = e.Drop(p, slot).Ok; break;
                    case "throw": ok = e.Throw(p, slot, skins[rng.Next(skins.Length)]).Ok; break;
                    case "settle": ok = item != null && e.Settle(item.Id).Ok; break;
                    case "deploy": ok = e.Deploy(p, slot).Ok; break;
                    case "consume": ok = e.Consume(p, slot).Ok; break;
                    case "expire": ok = item != null && e.Expire(item.Id).Ok; break;
                    case "damage": ok = item != null && e.Damage(item.Id, (float)rng.NextDouble() * 6f).Count >= 0; break;
                    case "break": ok = item != null && e.Break(item.Id).Count > 0; break;
                    case "transfer": ok = e.Transfer(p, other, slot).Ok; break;
                    case "give": ok = e.GiveLetter(p, other, (char)('A' + rng.Next(26))).Ok; break;
                    case "toss": ok = e.TossLetter(p, (char)('A' + rng.Next(26))).Count > 0 || e.KnockLoose(p, 1).Count > 0; break;
                    default:
                        ok = e.DropCarried(p).Ok;
                        if (rng.Next(200) == 0)
                        {
                            op = "eliminate";
                            e.Eliminate(p);
                            e.Player(p).CanAct = true; // back in for the next round of the test
                            ok = true;
                        }
                        break;
                }
                if (ok) counts[op] = counts.TryGetValue(op, out int n) ? n + 1 : 1;
                TestData.AssertConserved(e, $"seed {seed} step {step} ({op}):");
            }

            string tally = string.Join(", ", counts.OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + kv.Value));
            foreach (string op in new[] { "collect", "begin", "complete", "pickup", "throw", "deploy", "break", "consume", "transfer", "expire" })
                Assert.GreaterOrEqual(counts.TryGetValue(op, out int n) ? n : 0, 3, $"the fuzz rarely managed a successful {op}, so it isn't testing much ({tally})");
        }

        /// <summary>Whether <paramref name="op"/> in the random-play test has a chance of working on the item.</summary>
        static bool Fits(Economy e, string op, ItemInstance i)
        {
            e.Catalogue.TryGet(i.Word, out var def);
            switch (op)
            {
                case "pickup": return i.State == ItemState.World || i.State == ItemState.Deployed;
                case "settle": return i.State == ItemState.Thrown;
                case "expire": return i.State == ItemState.Armed;
                case "drop":
                case "throw":
                case "transfer": return i.State == ItemState.Held;
                case "deploy": return i.State == ItemState.Held && def != null && (def.CanDeploy || def.Consumable);
                case "consume": return i.State == ItemState.Held && def != null && def.Consumable && def.Use != null;
                default: return true;
            }
        }
    }
}
