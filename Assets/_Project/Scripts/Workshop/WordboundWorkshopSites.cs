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
        int siteLap = 1, siteRevision = -1, siteFixtureIndex;
        string sitePuzzle = "", siteEditKey = "", siteChoiceOpen = "", siteChoiceSearch = "";
        CoursePhysicalSite siteEdit;
        WorkshopHistory siteEditHistory;
        WorkshopSitePart sitePart;
        WorkshopSiteDrag siteDrag;
        CoursePoint siteDragStart;
        readonly Dictionary<string, string> siteFields = new Dictionary<string, string>();
        CoursePhysicalSite SelectedSite => display?.PhysicalSites.FirstOrDefault(s =>
            s.Lap == siteLap && s.PuzzleId == sitePuzzle &&
            display.Puzzles.Any(p => p.Id == s.PuzzleId && History.Current.Selection.Contains(p.ModuleId)));

        public void SelectPhysicalSite(int lap, string id)
        {
            var course = History.Current.Course();
            var site = course.PhysicalSites.Single(s => s.Lap == lap && s.PuzzleId == id);
            var owner = course.Modules.Single(m => m.Id == course.Puzzles.Single(p => p.Id == id).ModuleId);
            CancelGesture();
            if (History.Current.ActiveLayer != owner.LayerId || History.Current.HiddenLayers.Contains(owner.LayerId))
                History.Execute("Show puzzle floor", draft => { draft.ActiveLayer = owner.LayerId; draft.HiddenLayers.Remove(owner.LayerId); });
            History.Select(new[] { owner.Id }, false, false);
            siteLap = lap; sitePuzzle = id; puzzleId = id; zoneId = ""; sitePart = WorkshopSitePart.Socket;
            siteRevision = -1; Refresh(); view?.Focus(site.Position);
        }

        void LoadSiteEditor(CoursePhysicalSite selected)
        {
            string key = selected.Lap + "/" + selected.PuzzleId;
            if (ReferenceEquals(siteEditHistory, History) && siteEditKey == key && siteRevision == History.Revision) return;
            siteEditHistory = History; siteEditKey = key; siteRevision = History.Revision;
            siteEdit = WorkshopPhysicalSites.Copy(selected);
            siteFields.Clear(); siteChoiceOpen = ""; siteChoiceSearch = "";
            siteFixtureIndex = Math.Min(siteFixtureIndex, Math.Max(0, siteEdit.Fixtures.Count - 1));
        }

        void PhysicalSiteInspector(CourseModule module)
        {
            GUILayout.Label("Physical puzzle", heading);
            foreach (var site in display.PhysicalSites.Where(s => display.Puzzles.Any(p => p.Id == s.PuzzleId && p.ModuleId == module.Id)))
                if (Button($"Lap {site.Lap} · {site.PuzzleId}")) Attempt(() => SelectPhysicalSite(site.Lap, site.PuzzleId));
            var selected = SelectedSite;
            if (selected == null)
            {
                GUILayout.Label("Choose a puzzle above, then add its physical controls to an existing checkpoint.");
                siteLap = GUILayout.Toolbar(siteLap - 1, new[] { "Lap 1", "Lap 2", "Lap 3" }) + 1;
                GUI.enabled = display.Puzzles.Any(p => p.Id == puzzleId && p.ModuleId == module.Id);
                if (Button("Add physical controls"))
                    Edit("Add physical controls", draft =>
                    {
                        var created = WorkshopPhysicalSites.Create(draft, Catalogue, siteLap, puzzleId);
                        sitePuzzle = created.PuzzleId; siteRevision = -1;
                    });
                GUI.enabled = true;
                return;
            }
            LoadSiteEditor(selected);
            var puzzle = display.Puzzles.Single(p => p.Id == selected.PuzzleId);
            GUILayout.Label(puzzle.PresetId + " · " + puzzle.RecipeId);
            GUILayout.Label("Drag a marked control in the viewport. Numeric positions use metres.");
            if (Button("Back to module controls")) { CancelGesture(); sitePuzzle = ""; return; }
            siteEdit.CheckpointId = SiteChoice("Checkpoint", siteEdit.CheckpointId,
                display.Checkpoints.Where(c => c.ModuleId == module.Id && c.PuzzleIds.Contains(puzzle.Id)).Select(c => c.Id));
            siteEdit.RecoverySupplyId = SiteChoice("Recipe letters", siteEdit.RecoverySupplyId, puzzle.RecoverySupplyIds);
            SitePoint("Socket", siteEdit.Position);
            siteEdit.Yaw = GUILayout.Toolbar(siteEdit.Yaw / 90, new[] { "0°", "90°", "180°", "270°" }) * 90;
            SiteText("Socket radius", Metres(siteEdit.SocketRadiusMm));
            SiteText("Yaw tolerance °", siteEdit.YawTolerance.ToString(CultureInfo.InvariantCulture));
            SiteText("Hold seconds", Seconds(siteEdit.HoldTicks));
            if (!WorkshopPhysicalSites.Empty(siteEdit.ArrivalBounds)) SiteBox("Arrival", siteEdit.ArrivalBounds);
            if (puzzle.PresetId == "CollectLetters")
            {
                siteEdit.PropSourceZoneId = SiteChoice("Breakable source", siteEdit.PropSourceZoneId,
                    display.Zones.Where(z => z.ModuleId == module.Id && z.Purpose == "Props").Select(z => z.Id));
                if (siteEdit.ToolRecoverySupplyId.Length > 0)
                {
                    siteEdit.ToolRecoverySupplyId = SiteChoice("Tool letters", siteEdit.ToolRecoverySupplyId, puzzle.RecoverySupplyIds);
                    siteEdit.ToolRecipeId = SiteChoice("Tool recipe", siteEdit.ToolRecipeId, Catalogue.Assets.Keys);
                }
            }
            if (puzzle.PresetId == "RedirectWind")
            {
                siteEdit.WindReceiverZoneId = SiteChoice("Wind receiver", siteEdit.WindReceiverZoneId,
                    display.Zones.Where(z => z.ModuleId == module.Id && z.Purpose == "Props").Select(z => z.Id));
                siteEdit.ReceiverRecoverySupplyId = SiteChoice("Receiver letters", siteEdit.ReceiverRecoverySupplyId, puzzle.RecoverySupplyIds);
                SiteText("Receiver recipe", siteEdit.WindReceiverRecipeId);
                SiteBox("Receiver start", siteEdit.ReceiverStartBounds);
            }
            for (int i = 0; i < siteEdit.FloorPadBounds.Count; i++) SiteBox("Switch " + (i + 1), siteEdit.FloorPadBounds[i]);
            if (siteEdit.Fixtures.Count > 0)
            {
                SiteText("Active seconds", Seconds(siteEdit.ActiveWindowTicks));
                if (siteEdit.Fixtures.Count > 1 && Button("Moving arm " + (siteFixtureIndex + 1)))
                    siteFixtureIndex = (siteFixtureIndex + 1) % siteEdit.Fixtures.Count;
                for (int i = 0; i < siteEdit.Fixtures.Count; i++)
                {
                    if (i != siteFixtureIndex) continue;
                    var fixture = siteEdit.Fixtures[i]; string prefix = "Arm " + i;
                    SitePoint(prefix + " start", fixture.From); SitePoint(prefix + " end", fixture.To);
                    SitePoint(prefix + " size", fixture.SizeMm);
                    SiteText(prefix + " period", Seconds(fixture.PeriodTicks));
                    SiteText(prefix + " phase", Seconds(fixture.PhaseTicks));
                }
            }
            if (Button("Apply physical controls")) ApplySiteFields();
            if (Button("Delete physical controls"))
                Edit("Delete physical controls", draft => WorkshopPhysicalSites.Delete(draft, selected.Lap, selected.PuzzleId));
        }

        void SiteText(string label, string value)
        {
            if (!siteFields.ContainsKey(label)) siteFields[label] = value;
            siteFields[label] = Field(label, siteFields[label]);
        }
        void SitePoint(string label, CoursePoint value)
        {
            GUILayout.Label(label); GUILayout.BeginHorizontal();
            SiteText(label + " X", Metres(value.X)); SiteText(label + " Y", Metres(value.Y)); SiteText(label + " Z", Metres(value.Z));
            GUILayout.EndHorizontal();
        }
        void SiteBox(string label, CourseBox value)
        {
            SitePoint(label + " centre", WorkshopPhysicalSites.Centre(value));
            SitePoint(label + " size", new CoursePoint(value.Max.X - value.Min.X, value.Max.Y - value.Min.Y, value.Max.Z - value.Min.Z));
        }
        string SiteChoice(string label, string value, IEnumerable<string> source)
        {
            var ids = source.Distinct(StringComparer.Ordinal).ToArray();
            GUILayout.Label(label);
            if (Button((value.Length == 0 ? "Choose…" : value) + " ▾"))
            { siteChoiceOpen = siteChoiceOpen == label ? "" : label; siteChoiceSearch = ""; }
            if (siteChoiceOpen == label)
            {
                if (ids.Length > 8) siteChoiceSearch = Field("Find " + label.ToLowerInvariant(), siteChoiceSearch);
                foreach (var id in ids.Where(id => id.IndexOf(siteChoiceSearch, StringComparison.OrdinalIgnoreCase) >= 0))
                    if (Button(id)) { siteChoiceOpen = ""; return id; }
                if (ids.Length == 0) GUILayout.Label("No matching authored sources yet.");
            }
            return value;
        }
        CoursePoint ReadSitePoint(string key) => new CoursePoint(Millimetres(siteFields[key + " X"]),
            Millimetres(siteFields[key + " Y"]), Millimetres(siteFields[key + " Z"]));
        CourseBox ReadSiteBox(string key)
        {
            var c = ReadSitePoint(key + " centre"); var size = ReadSitePoint(key + " size");
            if (size.X <= 0 || size.Y <= 0 || size.Z <= 0) throw new ArgumentException(key + " size must be positive.");
            var min = new CoursePoint(checked(c.X - size.X / 2), checked(c.Y - size.Y / 2), checked(c.Z - size.Z / 2));
            return new CourseBox(min, new CoursePoint(checked(min.X + size.X), checked(min.Y + size.Y), checked(min.Z + size.Z)));
        }
        static string Seconds(int ticks) => (ticks / 60d).ToString("0.###", CultureInfo.InvariantCulture);
        int SiteTicks(string key) => checked((int)Math.Round(Millimetres(siteFields[key]) * .06, MidpointRounding.AwayFromZero));
        void ApplySiteFields()
        {
            Attempt(() =>
            {
                var next = WorkshopPhysicalSites.Copy(siteEdit);
                next.Position = ReadSitePoint("Socket"); next.SocketRadiusMm = Millimetres(siteFields["Socket radius"]);
                next.YawTolerance = int.Parse(siteFields["Yaw tolerance °"], CultureInfo.InvariantCulture); next.HoldTicks = SiteTicks("Hold seconds");
                if (!WorkshopPhysicalSites.Empty(next.ArrivalBounds))
                {
                    var oldCentre = WorkshopPhysicalSites.Centre(next.ArrivalBounds);
                    next.ArrivalBounds = ReadSiteBox("Arrival"); var centre = WorkshopPhysicalSites.Centre(next.ArrivalBounds);
                    next.Target = new CoursePoint(checked(next.Target.X + centre.X - oldCentre.X),
                        checked(next.Target.Y + centre.Y - oldCentre.Y), checked(next.Target.Z + centre.Z - oldCentre.Z));
                }
                if (!WorkshopPhysicalSites.Empty(next.ReceiverStartBounds))
                {
                    next.ReceiverStartBounds = ReadSiteBox("Receiver start");
                    next.WindReceiverRecipeId = siteFields["Receiver recipe"].Trim();
                }
                for (int i = 0; i < next.FloorPadBounds.Count; i++) next.FloorPadBounds[i] = ReadSiteBox("Switch " + (i + 1));
                if (next.Fixtures.Count > 0) next.ActiveWindowTicks = SiteTicks("Active seconds");
                for (int i = 0; i < next.Fixtures.Count; i++)
                {
                    string prefix = "Arm " + i;
                    if (!siteFields.ContainsKey(prefix + " start X")) continue;
                    var arm = next.Fixtures[i]; arm.From = ReadSitePoint(prefix + " start"); arm.To = ReadSitePoint(prefix + " end");
                    arm.SizeMm = ReadSitePoint(prefix + " size"); arm.PeriodTicks = SiteTicks(prefix + " period"); arm.PhaseTicks = SiteTicks(prefix + " phase");
                }
                History.Execute("Edit physical controls", draft =>
                {
                    var previous = draft.Course.PhysicalSites.Single(s => s.Lap == next.Lap && s.PuzzleId == next.PuzzleId);
                    if (!WorkshopPhysicalSites.Empty(next.ReceiverStartBounds) &&
                        next.WindReceiverZoneId == previous.WindReceiverZoneId)
                    {
                        var a = WorkshopPhysicalSites.Centre(previous.ReceiverStartBounds);
                        var b = WorkshopPhysicalSites.Centre(next.ReceiverStartBounds);
                        WorkshopPhysicalSites.MovePart(draft, next.Lap, next.PuzzleId, WorkshopSitePart.ReceiverStart,
                            new CoursePoint(checked(b.X - a.X), checked(b.Y - a.Y), checked(b.Z - a.Z)), snap: false);
                    }
                    WorkshopPhysicalSites.Set(draft, next);
                });
                status = "Physical controls saved · preview checks the complete course";
            });
        }

        IEnumerable<(WorkshopSitePart part, CoursePoint point, string label)> SiteHandles(CoursePhysicalSite site)
        {
            yield return (WorkshopSitePart.Socket, site.Position, "Socket");
            if (!WorkshopPhysicalSites.Empty(site.ArrivalBounds)) yield return (WorkshopSitePart.Arrival, WorkshopPhysicalSites.Centre(site.ArrivalBounds), "Arrival");
            if (!WorkshopPhysicalSites.Empty(site.ReceiverStartBounds)) yield return (WorkshopSitePart.ReceiverStart, WorkshopPhysicalSites.Centre(site.ReceiverStartBounds), "Receiver");
            for (int i = 0; i < site.FloorPadBounds.Count; i++)
                yield return (i == 0 ? WorkshopSitePart.FirstPad : WorkshopSitePart.SecondPad, WorkshopPhysicalSites.Centre(site.FloorPadBounds[i]), "Switch " + (i + 1));
            if (siteFixtureIndex < site.Fixtures.Count)
            {
                yield return (WorkshopSitePart.FixtureStart, site.Fixtures[siteFixtureIndex].From, "Arm start");
                yield return (WorkshopSitePart.FixtureEnd, site.Fixtures[siteFixtureIndex].To, "Arm end");
            }
        }
        void StepPhysicalSite()
        {
            var sites = display.PhysicalSites.Where(s => display.Puzzles.Any(p => p.Id == s.PuzzleId &&
                History.Current.Selection.Contains(p.ModuleId))).OrderBy(s => s.Lap).ThenBy(s => s.PuzzleId, StringComparer.Ordinal).ToArray();
            if (sites.Length == 0) { status = "Select a module with physical controls first."; return; }
            int index = Array.FindIndex(sites, s => s.Lap == siteLap && s.PuzzleId == sitePuzzle);
            var next = sites[(index + 1) % sites.Length]; SelectPhysicalSite(next.Lap, next.PuzzleId);
        }
        void StepPhysicalControl(int direction)
        {
            if (SelectedSite == null) { StepPhysicalSite(); return; }
            var handles = SiteHandles(SelectedSite).ToArray();
            int index = Array.FindIndex(handles, h => h.part == sitePart);
            CancelGesture(); sitePart = handles[(index + (direction < 0 ? -1 : 1) + handles.Length) % handles.Length].part;
            status = "Selected puzzle control: " + sitePart; FrameSelection();
        }
        bool SiteVisible(CoursePhysicalSite site) => site != null && display.Puzzles.Any(p => p.Id == site.PuzzleId &&
            display.Modules.Any(m => m.Id == p.ModuleId && m.LayerId == History.Current.ActiveLayer)) &&
            !History.Current.HiddenLayers.Contains(History.Current.ActiveLayer);
        void PhysicalSiteOverlay()
        {
            var selected = siteDrag?.Preview ?? SelectedSite;
            if (!SiteVisible(selected)) return;
            foreach (var box in new[] { selected.ArrivalBounds, selected.ReceiverStartBounds }.Concat(selected.FloorPadBounds))
            {
                if (WorkshopPhysicalSites.Empty(box)) continue;
                var lower = Corners(box);
                var upper = lower.Select(p => new CoursePoint(p.X, box.Max.Y, p.Z)).ToArray();
                for (int i = 0; i < 4; i++)
                {
                    DrawWorldLine(lower[i], lower[(i + 1) % 4], Color.cyan);
                    DrawWorldLine(upper[i], upper[(i + 1) % 4], Color.cyan);
                    DrawWorldLine(lower[i], upper[i], Color.cyan);
                }
            }
            foreach (var arm in selected.Fixtures) DrawWorldLine(arm.From, arm.To, Color.yellow);
            foreach (var handle in SiteHandles(selected))
            {
                if (!view.TryProject(handle.point, viewport, out var at)) continue;
                OverlayBox(new Rect(at.x - 8, at.y - 8, 16, 16), "", handle.part == sitePart ? selectedBox : normalBox);
                AddViewLabel(handle.point, handle.label, handle.label + " · " + selected.PuzzleId,
                    handle.part == sitePart ? 6 : 5);
            }
        }
        bool HandleSitePointer()
        {
            var input = Event.current;
            if (controllerMoving) return false;
            if (siteDrag != null && input.type == EventType.KeyDown && input.keyCode == KeyCode.Escape)
            { siteDrag = null; input.Use(); return true; }
            var selected = SelectedSite;
            if (!SiteVisible(selected) || drawingZone || placing != null || sharing) return false;
            var mouse = input.mousePosition - viewport.position;
            bool inside = viewport.Contains(input.mousePosition) && !(mouse.x < Mathf.Min(448, viewport.width - 8) && mouse.y < 106);
            if (siteDrag == null && input.type == EventType.MouseDown && input.button == 0 && inside)
            {
                foreach (var handle in SiteHandles(selected))
                {
                    if (Vector2.Distance(mouse, ScreenPoint(handle.point)) > 14) continue;
                    CancelGesture(); sitePart = handle.part;
                    if (!view.Surface(mouse, viewport, handle.point.Y, out siteDragStart)) return false;
                    siteDrag = new WorkshopSiteDrag(History, selected.Lap, selected.PuzzleId, sitePart, siteFixtureIndex);
                    GUI.FocusControl(null); input.Use(); return true;
                }
            }
            if (siteDrag == null) return false;
            if (input.type == EventType.MouseDrag)
            {
                if (view.Surface(mouse, viewport, siteDragStart.Y, out var point))
                    siteDrag.Move(point.X - siteDragStart.X, point.Z - siteDragStart.Z);
                input.Use(); return true;
            }
            if (input.type == EventType.MouseUp && input.button == 0)
            {
                siteDrag.Commit(History); siteDrag = null; Refresh();
                status = "Puzzle control moved · undo available"; input.Use(); return true;
            }
            return input.isMouse;
        }
    }
}
