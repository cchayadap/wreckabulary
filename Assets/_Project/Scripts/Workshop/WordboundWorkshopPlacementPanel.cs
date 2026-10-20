using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Workshop
{
    public sealed partial class WordboundWorkshop
    {
        string rangeMinX = "0", rangeMaxX = "0", rangeMinZ = "0", rangeMaxZ = "0";
        bool advancedPool;

        void FloorPanel()
        {
            GUILayout.Label("Floors", heading);
            foreach (var layer in display.Layers.OrderBy(item => item.HeightMm))
                if (Button((History.Current.ActiveLayer == layer.Id ? "● " : "") + layer.Id + " · " + Metres(layer.HeightMm) + " m"))
                    SelectFloor(layer.Id);
            GUILayout.BeginHorizontal();
            if (Button(History.Current.HiddenLayers.Contains(History.Current.ActiveLayer) ? "Show floor" : "Hide floor"))
            {
                CancelGesture();
                Edit("Floor visibility", draft => Toggle(draft.HiddenLayers, draft.ActiveLayer));
            }
            if (Button(History.Current.LockedLayers.Contains(History.Current.ActiveLayer) ? "Unlock" : "Lock"))
            {
                CancelGesture();
                Edit("Floor lock", draft => Toggle(draft.LockedLayers, draft.ActiveLayer));
            }
            GUILayout.EndHorizontal();
            layerHeight = Field("New floor height (m)", layerHeight);
            if (Button("+ Floor"))
            {
                CancelGesture();
                Edit("Add floor", draft => WorkshopCommands.AddLayer(draft, Millimetres(layerHeight)));
            }
        }

        void SelectFloor(string id)
        {
            CancelGesture(); zoneId = "";
            Edit("Select floor", draft => draft.ActiveLayer = id);
        }

        void LoadPlacementFields(CourseZone zone)
        {
            zoneMode = zone.Placement == "Fixed" ? 0 : zone.Pool.Count > 1 ? 2 : 1;
            pool = string.Join(", ", zone.Pool.Select(item => item.AssetId + ":" + item.Weight.ToString(CultureInfo.InvariantCulture)));
            minCount = zone.MinCount.ToString(CultureInfo.InvariantCulture); maxCount = zone.MaxCount.ToString(CultureInfo.InvariantCulture);
            probability = (zone.ChancePermille / 10m).ToString(CultureInfo.InvariantCulture); spacing = Metres(zone.SpacingMm);
            rangeMinX = Metres(zone.Bounds.Min.X); rangeMaxX = Metres(zone.Bounds.Max.X);
            rangeMinZ = Metres(zone.Bounds.Min.Z); rangeMaxZ = Metres(zone.Bounds.Max.Z);
        }

        void PlacementRangeFields()
        {
            GUILayout.Label("World range · metres");
            GUILayout.BeginHorizontal();
            rangeMinX = Field("X min", rangeMinX); rangeMaxX = Field("X max", rangeMaxX);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            rangeMinZ = Field("Z min", rangeMinZ); rangeMaxZ = Field("Z max", rangeMaxZ);
            GUILayout.EndHorizontal();
        }

        WordboundWorkshopPlacement PlacementSettings()
        {
            if (!decimal.TryParse(zoneMode == 0 ? "100" : probability, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) ||
                percent < 0 || percent > 100 || percent * 10 != decimal.Truncate(percent * 10))
                throw new ArgumentException("Chance is 0–100%, in steps of 0.1%.");
            return new WordboundWorkshopPlacement
            {
                Mode = zoneMode, Pool = WordboundWorkshopPlacement.ParsePool(pool),
                MinCount = zoneMode == 0 ? 1 : int.Parse(minCount, CultureInfo.InvariantCulture),
                MaxCount = zoneMode == 0 ? 1 : int.Parse(maxCount, CultureInfo.InvariantCulture),
                ChancePermille = (int)(percent * 10), SpacingMm = Millimetres(spacing)
            };
        }

        void ApplyPlacementSettings(string moduleId)
        {
            CancelGesture();
            Attempt(() =>
            {
                var settings = PlacementSettings();
                History.Execute("Placement settings", draft =>
                {
                    var zone = draft.Course.Zones.Single(item => item.Id == zoneId);
                    CourseBox? bounds = zoneMode == 0 ? (CourseBox?)null : new CourseBox(
                        new CoursePoint(Millimetres(rangeMinX), zone.Bounds.Min.Y, Millimetres(rangeMinZ)),
                        new CoursePoint(Millimetres(rangeMaxX), zone.Bounds.Max.Y, Millimetres(rangeMaxZ)));
                    settings.Apply(draft, Catalogue, moduleId, zoneId, bounds);
                });
                status = "Placement settings applied · undo available";
            });
        }

        void PoolWeightRows()
        {
            GUILayout.Label("Weights · higher means more likely");
            advancedPool = GUILayout.Toggle(advancedPool, "Advanced pool text");
            if (advancedPool) { pool = Field("ID:weight, ID:weight", pool); return; }
            var entries = pool.Split(',').Select(entry => entry.Trim()).Where(entry => entry.Length > 0).ToList();
            int remove = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                var parts = entries[i].Split(':');
                string id = parts[0].Trim(), weight = parts.Length > 1 ? parts[1].Trim() : "1";
                GUILayout.BeginHorizontal();
                GUILayout.Label(id, GUILayout.ExpandWidth(true));
                GUI.SetNextControlName("Weight " + id);
                weight = GUILayout.TextField(weight, 5, GUILayout.Width(52), GUILayout.MinHeight(25));
                if (GUILayout.Button(new GUIContent("×", "Remove " + id), GUILayout.Width(28), GUILayout.Height(25))) remove = i;
                GUILayout.EndHorizontal();
                if (parts.Length <= 2) entries[i] = id + ":" + weight;
            }
            if (remove >= 0) entries.RemoveAt(remove);
            pool = string.Join(", ", entries);
        }
    }
}
