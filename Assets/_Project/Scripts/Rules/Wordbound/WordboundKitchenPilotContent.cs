using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class WordboundKitchenPilotContent
    {
        public const string CourseId = "wordbound-lost-address-kitchen-v1";
        public const int FurnitureCount = 4;
        public const int FurnitureLetters = 25;
        readonly string documentJson;
        public CourseCatalogue Geometry { get; }
        public CourseDocument CopyDocument() => CourseJson.ImportDocument(documentJson).Document;
        public static WordboundKitchenPilotContent Create(ItemCatalogue items) => new WordboundKitchenPilotContent(items);

        public CourseResolution Resolve(string seed, CoursePlayRequirements play)
        {
            return CourseResolver.Resolve(CopyDocument(), Geometry, new CourseResolveRequest(seed, play));
        }

        WordboundKitchenPilotContent(ItemCatalogue items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            var kitchen = new[] {
                RequireFurniture(items, "FRIDGE", "Environment/Kitchen_Fridge"),
                RequireFurniture(items, "COUNTER", "Environment/Kitchen_Counter"),
                RequireFurniture(items, "STOVE", "Environment/Kitchen_Stove")
            };
            var original = WordboundFlagshipContent.Create(items);
            Geometry = new CourseCatalogue(original.Geometry.Modules.Values,
                original.Geometry.Assets.Values.Concat(kitchen), original.Geometry.PuzzlePresets,
                original.Geometry.Themes, original.Geometry.NarrativeEvents);
            var document = original.CopyDocument();
            document.Id = CourseId;
            document.Title = "The Lost Address — Kitchen Works";
            var room = document.Modules.Single(module => module.Id == "Kitchen_ServiceLanding");
            AddFurniture(document, room, "KitchenPilot_Fridge", "FRIDGE", -8900);
            AddFurniture(document, room, "KitchenPilot_CounterA", "COUNTER", -7650);
            AddFurniture(document, room, "KitchenPilot_Stove", "STOVE", -6400);
            AddFurniture(document, room, "KitchenPilot_CounterB", "COUNTER", -5150);
            documentJson = CourseJson.ExportDocument(document);
        }

        static CourseAssetDefinition RequireFurniture(ItemCatalogue items, string id, string model)
        {
            if (!items.TryGet(id, out var item) || item.Enabled || item.Consumable ||
                item.Category != ItemCategory.Furniture || item.Family != HandlingFamily.None || item.Hands != 2 ||
                item.Model != model || item.Skins == null || item.Skins.Count != 1 || item.Skins[0] != Skin.Standard ||
                item.Melee != null || item.Thrown != null || item.Shield != null || item.Deploy != null || item.Use != null ||
                !Finite(item.HeldScale) || item.HeldScale <= 0 || item.HeldScale > 1 ||
                item.Size == null || item.Size.Length != 3 || item.Size.Any(value => !Finite(value) || value < .1f || value > 3) ||
                item.Grip == null || item.Grip.Length != 3 || item.Grip.Any(value => !Finite(value)))
                throw new ArgumentException("Kitchen Works requires complete disabled original furniture: " + id);
            if (Math.Abs(item.Grip[0]) > item.Size[0] / 2 || item.Grip[1] < 0 || item.Grip[1] > item.Size[1] ||
                Math.Abs(item.Grip[2]) > item.Size[2] / 2)
                throw new ArgumentException("Kitchen Works grip must lie inside the original furniture bounds: " + id);
            return new CourseAssetDefinition(id, id, WordboundFlagshipEquipment.Mm(item.Size[0]),
                WordboundFlagshipEquipment.Mm(item.Size[2]), WordboundFlagshipEquipment.Mm(item.Size[1]), false);
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        void AddFurniture(CourseDocument document, CourseModule room, string id, string assetId, int localX)
        {
            var asset = Geometry.Assets[assetId];
            var at = new CoursePoint(room.Position.X + localX, room.Position.Y, room.Position.Z + 11100);
            int halfWidth = (asset.WidthMm + 1) / 2 + 100, halfDepth = (asset.DepthMm + 1) / 2 + 100;
            document.Zones.Add(new CourseZone {
                Id = id, ModuleId = room.Id, Purpose = "Props", Placement = "Fixed", FixedPosition = at,
                FixedYaw = 180, MinCount = 1, MaxCount = 1, ChancePermille = 1000, SpacingMm = 200, GridMm = 100,
                Bounds = new CourseBox(new CoursePoint(at.X - halfWidth, at.Y, at.Z - halfDepth),
                    new CoursePoint(at.X + halfWidth, at.Y, at.Z + halfDepth)),
                Yaws = new List<int> { 180 }, LayerIds = new List<string> { room.LayerId },
                Pool = new List<CourseWeightedAsset> { new CourseWeightedAsset { AssetId = assetId } }
            });
        }
    }
}
