using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public static class WordboundArrivalVariant
    {
        public const string CourseId = "wordbound-parcel-hall-arrival-v2";
        public const string EntryModuleId = "Packing_ParcelGate";
        public const int FurnitureCount = 18;

        public static CourseDocument Create(CourseDocument source, CourseCatalogue catalogue)
        {
            if (source == null || catalogue == null) throw new ArgumentNullException();
            if (source.Id != WordboundFlagshipContent.CourseId)
                throw new ArgumentException("Create this local variant from a Lost Address course copy.");
            var module = source.Modules.SingleOrDefault(m => m.Id == EntryModuleId);
            if (module == null || source.Zones.Any(z => z.ModuleId == EntryModuleId))
                throw new ArgumentException("The original empty Parcel Gate is required; existing furniture is never replaced.");
            foreach (string id in new[] { "TABLE", "SOFA", "STOOL" })
                if (!catalogue.Assets.ContainsKey(id)) throw new ArgumentException("The original furniture catalogue is missing " + id);
            var result = source.Clone(); result.Id = CourseId; result.Title = "The Lost Address — Parcel Hall";
            Add(result, module, catalogue, "SortingTable", "TABLE", -8000, -9000, 0);
            Add(result, module, catalogue, "WaitingSofa", "SOFA", 8000, -9000, 180);
            Add(result, module, catalogue, "ReturnTable", "TABLE", -8000, 9000, 180);
            Add(result, module, catalogue, "ReturnSofa", "SOFA", 8000, 9000, 0);
            Add(result, module, catalogue, "WestStool", "STOOL", -10000, -5000, 0);
            Add(result, module, catalogue, "EastStool", "STOOL", 10000, 5000, 180);
            Add(result, module, catalogue, "WestSortingNorth", "TABLE", -8000, 3000, 90);
            Add(result, module, catalogue, "WestSortingSouth", "TABLE", -8000, -3000, 90);
            Add(result, module, catalogue, "EastWaitingNorth", "SOFA", 8000, 3000, 270);
            Add(result, module, catalogue, "EastWaitingSouth", "SOFA", 8000, -3000, 270);
            Add(result, module, catalogue, "NorthDeskLeft", "TABLE", -4000, 9000, 180);
            Add(result, module, catalogue, "NorthDeskRight", "TABLE", 4000, 9000, 180);
            Add(result, module, catalogue, "SouthDeskLeft", "TABLE", -4000, -9000, 0);
            Add(result, module, catalogue, "SouthDeskRight", "TABLE", 4000, -9000, 0);
            Add(result, module, catalogue, "NorthSeatLeft", "STOOL", -4000, 7200, 180);
            Add(result, module, catalogue, "NorthSeatRight", "STOOL", 4000, 7200, 180);
            Add(result, module, catalogue, "SouthSeatLeft", "STOOL", -4000, -7200, 0);
            Add(result, module, catalogue, "SouthSeatRight", "STOOL", 4000, -7200, 0);
            result.Supplies.Add(new CourseSupply { Id = "Arrival_PracticeTools", ModuleId = module.Id,
                Position = Offset(module.Position, -5000, -10500), Letters = "HAMMER", Renewable = true, Protected = true });
            return result;
        }

        static void Add(CourseDocument document, CourseModule module, CourseCatalogue catalogue,
            string id, string assetId, int x, int z, int yaw)
        {
            var at = Offset(module.Position, x, z); var asset = catalogue.Assets[assetId];
            bool sideways = yaw == 90 || yaw == 270;
            int halfX = ((sideways ? asset.DepthMm : asset.WidthMm) + 1) / 2 + 250;
            int halfZ = ((sideways ? asset.WidthMm : asset.DepthMm) + 1) / 2 + 250;
            document.Zones.Add(new CourseZone { Id = "Arrival_" + id, ModuleId = module.Id, Purpose = "Props",
                Placement = "Fixed", FixedPosition = at, FixedYaw = yaw,
                Bounds = new CourseBox(Offset(at, -halfX, -halfZ), Offset(at, halfX, halfZ)),
                MinCount = 1, MaxCount = 1, GridMm = 250, SpacingMm = 500,
                Yaws = new List<int> { yaw }, LayerIds = new List<string> { module.LayerId },
                Pool = new List<CourseWeightedAsset> { new CourseWeightedAsset { AssetId = assetId } } });
        }
        static CoursePoint Offset(CoursePoint value, int x, int z) => new CoursePoint(value.X + x, value.Y, value.Z + z);
    }
}
