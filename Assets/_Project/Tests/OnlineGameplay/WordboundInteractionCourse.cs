using System.Collections.Generic;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.OnlineGameplayTests
{
    static class WordboundInteractionCourse
    {
        public static CourseDocument Create()
        {
            var document = CourseTemplates.FreestyleWorld();
            document.Id = "native-interaction-proof";
            document.Title = "Native Interaction Proof";
            document.Zones.Clear();
            for (int lane = 0; lane < 2; lane++)
            {
                int x = lane == 0 ? -4000 : 4000;
                document.Zones.Add(Stool("Carry" + lane, x, -4000));
                document.Zones.Add(Stool("Target" + lane, x, 4000));
                document.Supplies.Add(new CourseSupply {
                    Id = "BatLetters" + lane, ModuleId = "Start", Position = new CoursePoint(x, 0, 0),
                    Letters = "BAT", Renewable = false, Protected = false
                });
            }
            return document;
        }
        static CourseZone Stool(string id, int x, int z) => new CourseZone {
            Id = id, ModuleId = "Start", Purpose = "Props", Placement = "Fixed",
            FixedPosition = new CoursePoint(x, 0, z), FixedYaw = 0,
            Bounds = new CourseBox(new CoursePoint(x - 1000, 0, z - 1000), new CoursePoint(x + 1000, 1000, z + 1000)),
            MinCount = 1, MaxCount = 1, Yaws = new List<int> { 0 }, LayerIds = new List<string> { "Ground" },
            Pool = new List<CourseWeightedAsset> { new CourseWeightedAsset { AssetId = "STOOL" } }
        };
    }
}
