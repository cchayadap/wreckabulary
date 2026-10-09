using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class ProductionHouseTests
    {
        [Test]
        public void EveryShippedMapHasValidPlayableObjectivesAndSafeClearOuts()
        {
            string folder = Path.Combine(TestData.Root, "Assets/_Project/Data/Config");
            var catalogue = TestData.Catalogue();
            var rules = TestData.LoadRuleBook();
            foreach (string file in Directory.GetFiles(folder, "house_*.json"))
            {
                var house = HouseLayout.FromJson(File.ReadAllText(file), Path.GetFileName(file));
                Assert.IsEmpty(house.Validate(catalogue), house.Name);
                Assert.IsTrue(house.Spawns.Count >= 4, house.Name + " needs four seats");
                Assert.IsTrue(house.MovingDay.Count >= 4, house.Name + " needs a Moving Day objective");
                Assert.IsTrue(house.Keepsakes.Count >= 3, house.Name + " needs keepsakes across the house");
                foreach (var objective in house.MovingDay)
                    Assert.IsTrue(catalogue.Get(objective.Word).Enabled, objective.Word);
                foreach (var spawn in house.Spawns)
                    foreach (var recipe in catalogue.Enabled.Where(i => i.Tier == ItemTier.Core))
                        Assert.IsTrue(house.LettersIn(spawn.Room).Contains(recipe.Letters),
                            house.Name + ": " + spawn.Room + " cannot make " + recipe.Id);
                var available = new LetterBag();
                foreach (var room in house.Rooms) available.Add(house.LettersIn(room.Name));
                foreach (var recipe in catalogue.Enabled)
                    Assert.IsTrue(available.Contains(recipe.Letters), house.Name + " cannot supply " + recipe.Id);
                foreach (string mode in rules.Modes.Where(m => rules.For(m).ClearOutEnabled))
                    Assert.IsTrue(house.ClearOutOrders.ContainsKey(mode), house.Name + " lacks " + mode);
                Assert.IsFalse(house.ClearOutOrders["MovingOut"].Contains(house.ExtractionRoom), house.Name + " closes the van");
            }
        }

        [Test]
        public void BrokenCooperativeObjectivesAreReportedBeforeAMatch()
        {
            var house = TestData.House();
            house.MovingDay.Add(new HouseObjective { Word = "APPLE", Room = "Imaginary" });
            house.Keepsakes.Add(new SpawnPoint { Room = "Bedroom", X = 100, Z = 100 });
            house.ExtractionX = 100;
            var errors = house.Validate(TestData.Catalogue());
            Assert.IsTrue(errors.Any(e => e.Contains("APPLE")));
            Assert.IsTrue(errors.Any(e => e.Contains("keepsake")));
            Assert.IsTrue(errors.Any(e => e.Contains("extraction point")));
        }
    }
}
