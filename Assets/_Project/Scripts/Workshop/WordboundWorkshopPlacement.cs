using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Workshop
{
    public sealed class WordboundWorkshopPlacement
    {
        public int Mode, MinCount = 1, MaxCount = 3, ChancePermille = 1000, SpacingMm = 500;
        public List<CourseWeightedAsset> Pool = new List<CourseWeightedAsset>();
        public List<int> Yaws;
        public List<string> LayerIds;

        public static List<CourseWeightedAsset> ParsePool(string text)
        {
            var entries = (text ?? "").Split(',').Where(entry => !string.IsNullOrWhiteSpace(entry)).Select(entry =>
            {
                var parts = entry.Split(':');
                int weight = 1;
                if (parts.Length > 2 || (parts.Length == 2 && !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out weight)))
                    throw new ArgumentException("Enter a whole-number weight for each object.");
                return new CourseWeightedAsset { AssetId = parts[0].Trim(), Weight = weight };
            }).ToList();
            if (entries.Count == 0) throw new ArgumentException("Choose at least one object.");
            if (entries.Any(entry => entry.Weight < 1 || entry.Weight > 10000))
                throw new ArgumentException("Each weight must be 1–10000.");
            if (entries.Select(entry => entry.AssetId).Distinct(StringComparer.Ordinal).Count() != entries.Count)
                throw new ArgumentException("Choose each object once.");
            return entries;
        }

        public string Apply(WorkshopDraft draft, CourseCatalogue catalogue, string moduleId, string zoneId,
            CourseBox? bounds = null, CoursePoint? position = null)
        {
            if (Mode < 0 || Mode > 2) throw new ArgumentException("Choose Fixed, Zone or Pool.");
            if (Pool == null || Pool.Count == 0 || Pool.Any(entry => entry == null || entry.Weight < 1 || entry.Weight > 10000))
                throw new ArgumentException("Choose objects with weights from 1–10000.");
            if (Pool.Select(entry => entry.AssetId).Distinct(StringComparer.Ordinal).Count() != Pool.Count)
                throw new ArgumentException("Choose each object once.");
            if (Mode != 2 && Pool.Count != 1) throw new ArgumentException("Choose one object, or use Pool.");
            if (Mode != 0 && (MinCount < 0 || MaxCount < MinCount || MaxCount > 256 ||
                ChancePermille < 0 || ChancePermille > 1000 || (MinCount > 0 && ChancePermille != 1000)))
                throw new ArgumentException("Use 0–256 objects, max ≥ min, and 100% chance when min is above zero.");
            if (SpacingMm < 0 || SpacingMm > 10000) throw new ArgumentException("Spacing must be 0–10 metres.");
            var module = draft.Course.Modules.Single(item => item.Id == moduleId);
            var existing = string.IsNullOrEmpty(zoneId) ? null : draft.Course.Zones.Single(item => item.Id == zoneId);
            if (existing != null && existing.ModuleId != moduleId) throw new ArgumentException("Select the placement's room first.");
            if (Mode != 0 && Yaws != null && (Yaws.Count == 0 || Yaws.Count > 4 ||
                Yaws.Any(yaw => yaw != 0 && yaw != 90 && yaw != 180 && yaw != 270) || Yaws.Distinct().Count() != Yaws.Count))
                throw new ArgumentException("Choose at least one distinct quarter-turn rotation.");
            if (Mode != 0 && LayerIds != null && (LayerIds.Count == 0 || !LayerIds.Contains(module.LayerId) ||
                LayerIds.Distinct(StringComparer.Ordinal).Count() != LayerIds.Count ||
                LayerIds.Any(id => !draft.Course.Layers.Any(layer => layer.Id == id))))
                throw new ArgumentException("Allowed floors must include this room's floor and only existing floors.");
            var zone = existing == null ? new CourseZone
            {
                Id = WorkshopCommands.Unique("zone", draft.Course.Zones.Select(item => item.Id)),
                ModuleId = moduleId, Bounds = catalogue.Bounds(module), FixedPosition = module.Position,
                GridMm = draft.GridMm, LayerIds = new List<string> { module.LayerId }
            } : draft.Course.Clone().Zones.Single(item => item.Id == zoneId);
            if (Mode == 0 && zone.Placement != "Fixed")
            {
                zone.FixedPosition = module.Position;
                zone.Bounds = catalogue.Bounds(module);
            }
            if (bounds.HasValue)
            {
                var box = bounds.Value;
                if (box.Min.X > box.Max.X || box.Min.Y > box.Max.Y || box.Min.Z > box.Max.Z)
                    throw new ArgumentException("Each range minimum must be at or below its maximum.");
                zone.Bounds = box;
            }
            if (position.HasValue) zone.FixedPosition = position.Value;
            if (Mode == 0) zone.Yaws = new List<int> { zone.FixedYaw };
            else if (zone.Placement == "Fixed") zone.Yaws = new List<int> { 0, 90, 180, 270 };
            if (Mode != 0 && Yaws != null) zone.Yaws = Yaws.ToList();
            if (Mode != 0 && LayerIds != null) zone.LayerIds = LayerIds.ToList();
            zone.Placement = Mode == 0 ? "Fixed" : "Random";
            zone.MinCount = Mode == 0 ? 1 : MinCount; zone.MaxCount = Mode == 0 ? 1 : MaxCount;
            zone.ChancePermille = Mode == 0 ? 1000 : ChancePermille; zone.SpacingMm = SpacingMm;
            zone.Pool = Pool.Select(entry => new CourseWeightedAsset { AssetId = entry.AssetId, Weight = entry.Weight }).ToList();
            WorkshopCommands.SetZone(draft, zone, catalogue);
            return zone.Id;
        }
    }
}
