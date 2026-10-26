using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Workshop
{
    public sealed partial class WordboundWorkshop
    {
        string rangeMinX = "0", rangeMaxX = "0", rangeMinY = "0", rangeMaxY = "0", rangeMinZ = "0", rangeMaxZ = "0";
        List<int> rangeYaws = new List<int>();
        List<string> rangeLayers = new List<string>();
        bool advancedPool, placementLimits, placementHelp;
        static readonly int[] PlacementYaws = { 0, 90, 180, 270 };

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
            rangeMinY = Metres(zone.Bounds.Min.Y); rangeMaxY = Metres(zone.Bounds.Max.Y);
            rangeYaws = zone.Yaws.ToList(); rangeLayers = zone.LayerIds.ToList();
        }

        void PlacementRangeFields(CourseModule module)
        {
            GUILayout.Label("World range · metres");
            GUILayout.BeginHorizontal();
            rangeMinX = Field("X min", rangeMinX); rangeMaxX = Field("X max", rangeMaxX);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            rangeMinZ = Field("Z min", rangeMinZ); rangeMaxZ = Field("Z max", rangeMaxZ);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            rangeMinY = Field("Height min", rangeMinY); rangeMaxY = Field("Height max", rangeMaxY);
            GUILayout.EndHorizontal();
            placementHelp = GUILayout.Toggle(placementHelp, "Range help");
            if (placementHelp)
            {
                GUILayout.Label("Height = object base, in world metres.", ui.Wrapped);
                GUILayout.Label("The whole object must fit in this room.", ui.Wrapped);
            }
            placementLimits = GUILayout.Toggle(placementLimits, "Rotation & floors");
            if (!placementLimits) return;
            GUILayout.Label("Rotation");
            GUILayout.BeginHorizontal();
            foreach (int yaw in PlacementYaws)
            {
                bool picked = rangeYaws.Contains(yaw);
                bool next = GUILayout.Toggle(picked, yaw + "°", "Button");
                if (next != picked) { if (next) rangeYaws.Add(yaw); else rangeYaws.Remove(yaw); }
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(new GUIContent("Allowed floors", "Objects stay in this room. Height is world-space; this room's floor is required."));
            GUILayout.Label("Spawns stay in this room.", ui.Wrapped);
            foreach (var layer in display.Layers.OrderBy(item => item.HeightMm))
            {
                bool picked = rangeLayers.Contains(layer.Id), enabled = GUI.enabled;
                GUI.enabled = enabled && !(picked && layer.Id == module.LayerId);
                bool next = GUILayout.Toggle(picked, new GUIContent(layer.Id + (layer.Id == module.LayerId ? " · room" : ""),
                    "Limits the allowed floors. Objects still stay in this room."), "Button");
                GUI.enabled = enabled;
                if (next != picked) { if (next) rangeLayers.Add(layer.Id); else rangeLayers.Remove(layer.Id); }
            }
        }

        void CancelPlacementSettings()
        {
            CancelGesture();
            var zone = History.Current.Course().Zones.FirstOrDefault(item => item.Id == zoneId);
            if (zone != null) LoadPlacementFields(zone);
            status = "Changes discarded";
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
                if (zoneMode != 0) { settings.Yaws = rangeYaws.ToList(); settings.LayerIds = rangeLayers.ToList(); }
                bool changed = History.Execute("Placement settings", draft =>
                {
                    CourseBox? bounds = zoneMode == 0 ? (CourseBox?)null : new CourseBox(
                        new CoursePoint(Millimetres(rangeMinX), Millimetres(rangeMinY), Millimetres(rangeMinZ)),
                        new CoursePoint(Millimetres(rangeMaxX), Millimetres(rangeMaxY), Millimetres(rangeMaxZ)));
                    settings.Apply(draft, Catalogue, moduleId, zoneId, bounds);
                });
                status = changed ? "Applied" : "No changes";
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
