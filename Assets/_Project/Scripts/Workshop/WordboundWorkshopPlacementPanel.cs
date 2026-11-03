using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;

namespace Wreckabulary.Workshop
{
    public sealed partial class WordboundWorkshop
    {
        string rangeMinX = "0", rangeMaxX = "0", rangeMinY = "0", rangeMaxY = "0", rangeMinZ = "0", rangeMaxZ = "0";
        List<int> rangeYaws = new List<int>();
        List<string> rangeLayers = new List<string>();
        bool advancedPool, placementLimits, placementHelp;
        float placementRangeY, placementFloorsY;
        static readonly int[] PlacementYaws = { 0, 90, 180, 270 };

        void FloorPanel()
        {
            var style = RoomStyleProvider?.Invoke();
            foreach (var layer in display.Layers.OrderBy(item => item.HeightMm))
            {
                bool active = History.Current.ActiveLayer == layer.Id;
                string size = FloorSize(layer.Id), state = (History.Current.HiddenLayers.Contains(layer.Id) ? " · hidden" : "") +
                    (History.Current.LockedLayers.Contains(layer.Id) ? " · locked" : "");
                GUI.SetNextControlName("Floor " + layer.Id);
                if (ui.Button(new GUIContent((active ? "● " : "") + layer.Id + "\n" + Metres(layer.HeightMm) + " m · " + size + state,
                    "Floor " + layer.Id + " · height " + Metres(layer.HeightMm) + " m · rooms cover " + size +
                    (style && style.Enabled ? " · room style swatch is read-only, from the visual profile" : "")),
                    active ? ui.Selected : null, GUILayout.MinHeight(ui.ModuleHeight), GUILayout.Width(RowWidth)))
                    SelectFloor(layer.Id);
                if (style && style.Enabled && Event.current.type == EventType.Repaint) FloorSwatch(GUILayoutUtility.GetLastRect(), style.ForLayer(layer.Id));
            }
            if (WideButton("+ Floor", "Add a floor at " + layerHeight + " m, just above the top floor's rooms · change it in Advanced"))
            {
                CancelGesture();
                Edit("Add floor", draft => WorkshopCommands.AddLayer(draft, Millimetres(layerHeight)));
            }
        }

        void FloorAdvanced()
        {
            var current = display.Layers.FirstOrDefault(item => item.Id == History.Current.ActiveLayer);
            GUILayout.Label("Floor " + History.Current.ActiveLayer, rowLabel, GUILayout.Width(RowWidth), GUILayout.MinHeight(ui.RowHeight));
            GUILayout.BeginHorizontal(GUILayout.Width(RowWidth));
            if (ui.Button(new GUIContent(History.Current.HiddenLayers.Contains(History.Current.ActiveLayer) ? "Show floor" : "Hide floor",
                "Show or hide the current floor"), null, GUILayout.MinHeight(ui.RowHeight), GUILayout.ExpandWidth(true)))
            {
                CancelGesture();
                Edit("Floor visibility", draft => Toggle(draft.HiddenLayers, draft.ActiveLayer));
            }
            if (ui.Button(new GUIContent(History.Current.LockedLayers.Contains(History.Current.ActiveLayer) ? "Unlock floor" : "Lock floor",
                "Lock the current floor so nothing on it moves"), null, GUILayout.MinHeight(ui.RowHeight), GUILayout.ExpandWidth(true)))
            {
                CancelGesture();
                Edit("Floor lock", draft => Toggle(draft.LockedLayers, draft.ActiveLayer));
            }
            GUILayout.EndHorizontal();
            if (current != null && !History.Current.LockedLayers.Contains(current.Id))
            {
                var (below, above) = WorkshopCommands.LayerRange(display, current.Id);
                bool adjusting = scrubbing == "Floor height";
                floorHeight = Field("Height m", floorHeight, GridMetres, .1, "Floor height", (below + 100) / 1000d, (above - 100) / 1000d);
                if (adjusting) status = "Floor " + current.Id + " · " + floorHeight + " m · release to apply";
                bool apply = ScrubReleased("Floor height");
                if (!adjusting && floorHeight != Metres(current.HeightMm))
                    apply |= WideButton("Set height " + floorHeight + " m", "Move this floor and all its rooms to the typed height");
                if (apply) SetFloorHeight(current.Id);
            }
            layerHeight = Field("New floor at m", layerHeight, GridMetres, .1, "New floor height (m)",
                -CourseLimits.VerticalMm / 1000d, CourseLimits.VerticalMm / 1000d);
        }

        static readonly string[] PlacementKinds = { "One object", "Scatter area", "Random mix" };
        static readonly string[] PlacementHints = { "One object at an exact spot", "Several of one object scattered in an area",
            "A random mix of objects scattered in an area" };

        void ZoneInspector(CourseModule module)
        {
            var editing = display.Zones.FirstOrDefault(item => item.Id == zoneId && item.ModuleId == module.Id);
            GUI.SetNextControlName("Workshop placement kind");
            int nextMode = GUILayout.Toolbar(zoneMode, PlacementKinds.Select((label, index) => new GUIContent(label, PlacementHints[index])).ToArray(),
                GUILayout.MinHeight(ui.RowHeight), GUILayout.Width(RowWidth));
            if (nextMode != zoneMode)
            {
                CancelGesture();
                zoneMode = nextMode;
                if (zoneMode != 2) pool = pool.Split(',')[0].Split(':')[0].Trim();
            }
            if (editing == null) { if (Section("picker", "Objects", true)) PlacementAssetPicker(); }
            else if (Section(null, "Objects · " + ZoneCaption(editing), ref placementPickerOpen)) PlacementAssetPicker();
            if (zoneMode != 0) Pair("How many", "Object count", "min", ref minCount, "max", ref maxCount, 1, 1, 0, 256);
            if (Advanced(ref fillAdvanced))
            {
                if (zoneMode != 0)
                {
                    probability = Field("Chance %", probability, 1, .1, "Chance % · a minimum above 0 needs 100", 0, 100);
                    spacing = Field("Spacing m", spacing, .25, .05, "Spacing (metres)", 0, 10);
                }
                if (zoneMode == 2 && Section("weights", "Mix amounts · " + PoolCount(pool) + " objects", true)) PoolWeightRows();
                if (editing != null && zoneMode != 0) PlacementRangeFields(module);
                if (zoneMode == 0) GUILayout.Label("Exact position and turns are below the object once it is placed.", ui.Wrapped, GUILayout.Width(RowWidth));
            }
            if (editing != null)
            {
                GUILayout.BeginHorizontal(GUILayout.Width(RowWidth));
                if (ui.Button(new GUIContent("Apply", "Apply these settings"), null, GUILayout.MinHeight(ui.RowHeight), GUILayout.ExpandWidth(true)))
                    ApplyPlacementSettings(module.Id);
                if (ui.Button(new GUIContent("Cancel", "Discard these changes"), null, GUILayout.MinHeight(ui.RowHeight), GUILayout.ExpandWidth(true)))
                    CancelPlacementSettings();
                GUILayout.EndHorizontal();
            }
            if (WideButton(zoneMode == 0 ? editing == null ? "Place object" : "Reposition object" : editing == null ? "Draw area" : "Redraw area", null, ui.Primary))
            {
                CancelGesture(); drawingZone = true;
                status = zoneMode == 0 ? "Click where the object goes · Esc cancels" : "Drag across the room to draw the area · Esc cancels";
            }
            var zones = display.Zones.Where(item => item.ModuleId == module.Id).ToList();
            if (Section("placements", "In this room · " + zones.Count, false))
                foreach (var zone in zones)
                {
                    GUILayout.BeginHorizontal();
                    GUI.SetNextControlName("workshop-choice:Placement:" + zone.Id);
                    if (ui.Button(new GUIContent(ZoneCaption(zone), zone.Id), zone.Id == zoneId ? ui.Selected : null,
                        GUILayout.Width(Mathf.Max(40, RowWidth - ui.RowHeight - 10)), GUILayout.MinHeight(ui.RowHeight))) SelectZone(zone);
                    if (Button("×", "Delete " + ZoneCaption(zone), ui.RowHeight)) Edit("Delete objects", draft =>
                    {
                        if (draft.LockedLayers.Contains(module.LayerId)) throw new InvalidOperationException("Unlock the floor first.");
                        draft.Course.Zones.RemoveAll(item => item.Id == zone.Id);
                    });
                    GUILayout.EndHorizontal();
                }
            if (!string.IsNullOrEmpty(zoneId) && WideButton("New placement")) { CancelGesture(); zoneId = ""; }
        }
        static int PoolCount(string text) => text.Split(',').Count(entry => entry.Trim().Length > 0);

        void SetFloorHeight(string id)
        {
            CancelGesture();
            Edit("Floor height", draft => WorkshopCommands.SetLayerHeight(draft, id, Millimetres(floorHeight)));
            if (status == "Floor height") floorHeight = Metres(display.Layers.Single(layer => layer.Id == id).HeightMm);
        }

        string FloorSize(string id)
        {
            var box = WorkshopCommands.LayerFootprint(display, Catalogue, id);
            return box == null ? "empty" : Metres(box.Value.Max.X - box.Value.Min.X) + " × " + Metres(box.Value.Max.Z - box.Value.Min.Z) + " m";
        }

        static void FloorSwatch(Rect button, WordboundRoomPalette palette)
        {
            var tint = GUI.color;
            try
            {
                GUI.color = palette.FloorMaterial ? palette.FloorMaterial.color : palette.Floor;
                GUI.DrawTexture(new Rect(button.xMax - 26, button.y + 8, 9, 9), Texture2D.whiteTexture);
                GUI.color = palette.WallMaterial ? palette.WallMaterial.color : palette.Wall;
                GUI.DrawTexture(new Rect(button.xMax - 15, button.y + 8, 9, 9), Texture2D.whiteTexture);
            }
            finally { GUI.color = tint; }
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
            bool open = Section("range", "Area range, turns & floors", ref placementLimits);
            if (Event.current.type == EventType.Repaint) placementRangeY = GUILayoutUtility.GetLastRect().y;
            if (!open) return;
            Pair("X m", "X range", "min", ref rangeMinX, "max", ref rangeMaxX, GridMetres, .1);
            Pair("Z m", "Z range", "min", ref rangeMinZ, "max", ref rangeMaxZ, GridMetres, .1);
            Pair("Height m", "Height range", "min", ref rangeMinY, "max", ref rangeMaxY, .25, .05);
            placementHelp = GUILayout.Toggle(placementHelp, "Help", GUILayout.Width(RowWidth));
            if (placementHelp)
            {
                GUILayout.Label("World metres. Height is the bottom of the object.", ui.Wrapped, GUILayout.Width(RowWidth));
                GUILayout.Label("The whole object must fit in this room.", ui.Wrapped, GUILayout.Width(RowWidth));
            }
            GUILayout.Label("Turns", rowLabel, GUILayout.Width(RowWidth));
            GUILayout.BeginHorizontal(GUILayout.Width(RowWidth));
            foreach (int yaw in PlacementYaws)
            {
                bool picked = rangeYaws.Contains(yaw);
                bool next = GUILayout.Toggle(picked, yaw + "°", "Button");
                if (next != picked) { if (next) rangeYaws.Add(yaw); else rangeYaws.Remove(yaw); }
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(new GUIContent("Allowed floors", "Objects stay in this room. Height is world-space; this room's floor is required."),
                rowLabel, GUILayout.Width(RowWidth));
            if (Event.current.type == EventType.Repaint) placementFloorsY = GUILayoutUtility.GetLastRect().y;
            GUILayout.Label("Objects always appear in this room.", ui.Wrapped, GUILayout.Width(RowWidth));
            foreach (var layer in display.Layers.OrderBy(item => item.HeightMm))
            {
                bool picked = rangeLayers.Contains(layer.Id), enabled = GUI.enabled;
                GUI.enabled = enabled && !(picked && layer.Id == module.LayerId);
                bool next = GUILayout.Toggle(picked, new GUIContent(layer.Id + (layer.Id == module.LayerId ? " · room" : ""),
                    "Limits the allowed floors. Objects still stay in this room."), "Button", GUILayout.Width(RowWidth));
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
            GUILayout.Label("Bigger amount = more likely · drag a name to adjust", ui.Wrapped, GUILayout.Width(RowWidth));
            advancedPool = GUILayout.Toggle(advancedPool, "Edit as text", GUILayout.Width(RowWidth));
            if (advancedPool) { pool = Field("Mix", pool, name: "ID:amount, ID:amount"); return; }
            var entries = pool.Split(',').Select(entry => entry.Trim()).Where(entry => entry.Length > 0).ToList();
            int remove = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                var parts = entries[i].Split(':');
                string id = parts[0].Trim(), weight = parts.Length > 1 ? parts[1].Trim() : "1";
                GUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent(inspectorAdvancedIds ? id : ObjectLabel(id), "Amount of " + ObjectLabel(id) + " · drag sideways to adjust"),
                    rowLabel, GUILayout.Width(Mathf.Max(40, RowWidth - 94)), GUILayout.MinHeight(25));
                weight = Scrub(GUILayoutUtility.GetLastRect(), "Weight " + id, weight, 1, 1, 1, 10000);
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
