using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Wreckabulary.Art;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;

namespace Wreckabulary.Workshop
{
    public sealed partial class WordboundWorkshop
    {
        WordboundWorkshopViewport view;
        WorkshopTransformDrag gesture;
        WorkshopPlacementDrag placementGesture;
        CoursePoint gestureStart, gesturePivot;
        Vector2 lastPointer;
        bool orbiting, controllerTools, controllerMoving, controllerAdd;
        float startAngle, controllerRepeat;
        int controllerAction, controllerX, controllerZ;
        readonly string[] controllerActions =
        {
            "Next module", "Previous module", "Rotate", "Duplicate", "Undo", "Redo",
            "Frame selection", "Preview", "Playtest", "Add module", "Next floor", "Share",
            "Next physical puzzle", "Next puzzle control", "Edit modules", "Assigned course"
        };
        public Func<string, Transform, GameObject> SpawnAssetVisual { get; set; }
        public Func<WordboundRoomStyleProfile> RoomStyleProvider { get; set; }
        public Func<System.Collections.Generic.IReadOnlyList<WordboundModuleDressingEntry>> ModuleDressingProvider { get; set; }
        public Func<string, Texture> AssetIcon { get; set; }
        string assetSearch = "";
        GUIStyle objectTileName;
        CourseZone SelectedFixed => display?.Zones.FirstOrDefault(item => item.Id == zoneId && item.Placement == "Fixed" &&
            History.Current.Selection.Contains(item.ModuleId));
        bool CanShowHandles => !History.Current.HiddenLayers.Contains(History.Current.ActiveLayer) &&
            !History.Current.LockedLayers.Contains(History.Current.ActiveLayer) &&
            display.Modules.Any(item => item.LayerId == History.Current.ActiveLayer && History.Current.Selection.Contains(item.Id));

        void EnsureView()
        {
            if (view != null) return;
            view = new WordboundWorkshopViewport(transform);
            view.SpawnAssetVisual = (id, parent) => SpawnAssetVisual != null
                ? SpawnAssetVisual(id, parent) : ModelVisual.Spawn("Items/" + id, parent);
            view.RoomStyleProvider = () => RoomStyleProvider?.Invoke();
            view.ModuleDressingProvider = () => ModuleDressingProvider?.Invoke();
            view.Sync(History.Current, Catalogue);
            view.Frame();
        }
        void OnDestroy() { try { view?.Dispose(); } finally { view = null; ui.Dispose(); } }
        void Frame()
        {
            if (History == null || Catalogue == null) return;
            EnsureView(); view.Sync(History.Current, Catalogue); view.Frame();
        }
        void FrameSelection()
        {
            EnsureView();
            if (SelectedSite != null) { view.Focus(SiteHandles(SelectedSite).FirstOrDefault(h => h.part == sitePart).point); return; }
            if (SelectedFixed != null) { view.Focus(SelectedFixed.FixedPosition); return; }
            var chosen = display.Modules.Where(module => History.Current.Selection.Contains(module.Id)).ToArray();
            if (chosen.Length > 0) view.Frame(chosen); else view.Frame();
        }
        int ActiveHeight => display.Layers.FirstOrDefault(layer => layer.Id == History.Current.ActiveLayer)?.HeightMm ?? 0;
        CoursePoint WorldPoint(Vector2 point)
        {
            if (!view.Surface(point, viewport, ActiveHeight, out var world)) throw new InvalidOperationException("Point at the active floor.");
            return world;
        }
        Vector2 ScreenPoint(CoursePoint point) => view.Project(point, viewport);
        Rect ScreenBox(CourseBox box)
        {
            var corners = Corners(box).Select(ScreenPoint).ToArray();
            return Rect.MinMaxRect(corners.Min(p => p.x), corners.Min(p => p.y), corners.Max(p => p.x), corners.Max(p => p.y));
        }
        CoursePoint SelectionPivot()
        {
            if (SelectedFixed != null) return SelectedFixed.FixedPosition;
            var chosen = display.Modules.Where(module => History.Current.Selection.Contains(module.Id)).ToList();
            return chosen.Count == 0 ? default : Pivot(chosen);
        }
        CoursePoint HandleEnd(CoursePoint pivot, WorkshopHandle handle)
        {
            int length = (int)(Mathf.Clamp(view.Distance * .06f, 1, 18) * 1000);
            return handle == WorkshopHandle.X ? new CoursePoint(pivot.X + length, pivot.Y, pivot.Z) :
                new CoursePoint(pivot.X, pivot.Y, pivot.Z + length);
        }
        void WorldView()
        {
            EnsureView(); view.Resize((int)viewport.width, (int)viewport.height);
            GUI.DrawTexture(viewport, view.Texture, ScaleMode.StretchToFill, false);
            GUI.BeginGroup(viewport);
            try
            {
                BeginOverlay();
                var mouse = Event.current.mousePosition;
                bool hovering = LocalViewport.Contains(mouse) && !labelSpace[0].Contains(mouse) && !orbiting && !panning;
                string hoveredModule = hovering ? view.Pick(mouse, viewport) : null;
                string hoveredZone = hovering ? view.PickFixed(mouse, viewport) : null;
                if (hovering && hoveredZone == null && view.Surface(mouse, viewport, ActiveHeight, out var hit))
                    hoveredZone = display.Zones.FirstOrDefault(zone => zone.Placement != "Fixed" && zone.LayerIds.Contains(History.Current.ActiveLayer) &&
                        hit.X >= zone.Bounds.Min.X && hit.X <= zone.Bounds.Max.X && hit.Z >= zone.Bounds.Min.Z && hit.Z <= zone.Bounds.Max.Z)?.Id;
                foreach (var module in display.Modules.Where(module => module.LayerId == History.Current.ActiveLayer &&
                    !History.Current.HiddenLayers.Contains(module.LayerId)))
                {
                    bool selected = History.Current.Selection.Contains(module.Id);
                    if (!selected && module.Id != hoveredModule) continue;
                    AddViewLabel(module.Position, RoomCaption(module.Id), module.Id +
                        (History.Current.LockedLayers.Contains(module.LayerId) ? " · locked floor" : ""), selected ? 3 : module.Id == hoveredModule ? 2 : 0);
                }
                foreach (var zone in display.Zones.Where(item => item.LayerIds.Contains(History.Current.ActiveLayer) &&
                    !History.Current.HiddenLayers.Contains(History.Current.ActiveLayer)))
                {
                    var bounds = zone.Id == resizingZone ? ResizeBounds(zone.Bounds, resizeAnchor, resizePoint) : zone.Bounds;
                    bool issue = validation.Issues.Any(item => item.EntityId == zone.Id);
                    if (zone.Id != zoneId && zone.Id != hoveredZone && !issue) continue;
                    var corners = Corners(bounds);
                    var color = issue ? new Color(1, .5f, .35f) : new Color(.4f, 1, .8f);
                    for (int i = 0; i < 4; i++) DrawWorldLine(corners[i], corners[(i + 1) % 4], color);
                    if (zone.Id == zoneId || zone.Id == hoveredZone)
                    {
                        string caption = zone.Placement == "Fixed" && zone.Pool.Count == 1 ? ObjectLabel(zone.Pool[0].AssetId) :
                            "Random · " + zone.MinCount + (zone.MinCount == zone.MaxCount ? "" : "–" + zone.MaxCount);
                        AddViewLabel(zone.Placement == "Fixed" ? zone.FixedPosition : bounds.Min, caption, zone.Id, zone.Id == zoneId ? 4 : 2);
                    }
                    if (zone.Id == zoneId && zone.Placement != "Fixed")
                        foreach (var corner in corners)
                            if (view.TryProject(corner, viewport, out var at)) OverlayBox(new Rect(at.x - 6, at.y - 6, 12, 12), "");
                }
                if (CanShowHandles && !drawingZone && placing == null && SelectedSite == null) Handles();
                PhysicalSiteOverlay();
                DrawViewLabels();
                if (marquee) GUI.Box(DragRect(pointerStart, Event.current.mousePosition), "", selectedBox);
            }
            finally { GUI.EndGroup(); }
            try { if (!HandleSitePointer()) HandlePointer(); }
            catch (Exception error)
            {
                status = error.Message; CancelGesture();
                if (Event.current.isMouse || Event.current.isKey) Event.current.Use();
            }
        }
        void Handles()
        {
            var pivot = SelectionPivot();
            var x = HandleEnd(pivot, WorkshopHandle.X); var z = HandleEnd(pivot, WorkshopHandle.Z);
            DrawWorldLine(pivot, x, new Color(1, .45f, .4f)); DrawWorldLine(pivot, z, new Color(.4f, .65f, 1));
            if (view.TryProject(x, viewport, out var screenX)) ViewHandle(screenX, "X", "Move on X", 18, 17);
            if (view.TryProject(z, viewport, out var screenZ)) ViewHandle(screenZ, "Z", "Move on Z", 18, 17);
            if (view.TryProject(pivot, viewport, out var at))
            {
                ViewHandle(at, "+", "Move on the floor", 18, 17);
                ViewHandle(at + new Vector2(0, -44), "R", "Rotate", 20, 14);
            }
        }
        WorkshopHandle? HandleAt(Vector2 point)
        {
            if (!CanShowHandles || SelectedSite != null) return null;
            var pivot = SelectionPivot(); var at = ScreenPoint(pivot);
            if (new Rect(at.x - 14, at.y - 58, 28, 28).Contains(point)) return WorkshopHandle.Rotate;
            if (Vector2.Distance(point, ScreenPoint(HandleEnd(pivot, WorkshopHandle.X))) < 17) return WorkshopHandle.X;
            if (Vector2.Distance(point, ScreenPoint(HandleEnd(pivot, WorkshopHandle.Z))) < 17) return WorkshopHandle.Z;
            return Vector2.Distance(point, at) < 17 ? WorkshopHandle.Plane : (WorkshopHandle?)null;
        }
        void StartGesture(WorkshopHandle handle, Vector2 mouse)
        {
            sitePuzzle = "";
            gesturePivot = SelectionPivot();
            if (SelectedFixed != null) placementGesture = new WorkshopPlacementDrag(History, SelectedFixed.Id);
            else gesture = new WorkshopTransformDrag(History, handle, gesturePivot);
            gestureStart = WorldPoint(mouse);
            var offset = mouse - ScreenPoint(gesturePivot);
            startAngle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            dragging = true; activeHandle = handle;
        }
        WorkshopHandle activeHandle;
        void CancelGesture()
        {
            siteDrag = null;
            gesture = null; placementGesture = null; dragging = marquee = panning = drawingZone = orbiting = controllerMoving = false;
            resizingZone = null; placing = null; view?.TransformPreview(null, default); view?.PlacementPreview(null);
        }
        void HandlePointer()
        {
            if (sharing || controllerMoving) return;
            var input = Event.current; Vector2 mouse = input.mousePosition - viewport.position;
            bool inside = viewport.Contains(input.mousePosition) &&
                !ToolbarReserved.Contains(mouse);
            if (input.type == EventType.ScrollWheel && inside) { view.Zoom(input.delta.y); input.Use(); }
            if (input.type == EventType.MouseDown && inside && (input.button == 1 || input.button == 2))
            {
                orbiting = input.button == 1; panning = input.button == 2; lastPointer = mouse;
                GUI.FocusControl(null); input.Use();
            }
            if (input.type == EventType.MouseDrag && (panning || orbiting))
            {
                if (panning) view.Pan(mouse - lastPointer); else view.Orbit(mouse - lastPointer);
                lastPointer = mouse; input.Use();
            }
            if (input.type == EventType.MouseUp && (panning || orbiting)) { panning = orbiting = false; input.Use(); }
            if (input.type == EventType.MouseDown && inside && input.button == 0)
            {
                GUI.FocusControl(null); pointerStart = mouse; addSelection = input.shift;
                var zone = display.Zones.FirstOrDefault(item => item.Id == zoneId && item.Placement != "Fixed" &&
                    item.LayerIds.Contains(History.Current.ActiveLayer) && !History.Current.HiddenLayers.Contains(History.Current.ActiveLayer));
                if (zone != null && !drawingZone && placing == null)
                {
                    var corners = Corners(zone.Bounds);
                    for (int i = 0; i < corners.Length; i++)
                        if (Vector2.Distance(mouse, ScreenPoint(corners[i])) <= 12)
                        {
                            resizingZone = zone.Id; resizeAnchor = corners[(i + 2) % 4]; resizePoint = corners[i];
                            input.Use(); return;
                        }
                }
                if (placing != null)
                {
                    string definition = placing;
                    Edit("Place module", draft => WorkshopCommands.AddModule(draft, definition, WorldPoint(mouse)));
                    input.Use(); return;
                }
                if (drawingZone) marquee = true;
                else
                {
                    var handle = HandleAt(mouse);
                    if (handle.HasValue) StartGesture(handle.Value, mouse);
                    else
                    {
                        string fixedHit = view.PickFixed(mouse, viewport);
                        if (fixedHit != null)
                        {
                            var fixedZone = display.Zones.Single(item => item.Id == fixedHit); SelectZone(fixedZone);
                            History.Select(new[] { fixedZone.ModuleId }); Refresh(); StartGesture(WorkshopHandle.Plane, mouse);
                        }
                        else
                        {
                            string hit = view.Pick(mouse, viewport);
                            if (hit != null)
                            {
                                zoneId = ""; sitePuzzle = ""; ShowInspectorSection(InspectorSection.Room);
                                if (!History.Current.Selection.Contains(hit) || addSelection) History.Select(new[] { hit }, addSelection);
                                Refresh(); StartGesture(WorkshopHandle.Plane, mouse);
                            }
                            else marquee = true;
                        }
                    }
                }
                input.Use();
            }
            if (input.type == EventType.MouseDrag && resizingZone != null)
            {
                var point = WorldPoint(mouse);
                resizePoint = new CoursePoint(WorkshopCommands.Snap(point.X, History.Current.GridMm), point.Y, WorkshopCommands.Snap(point.Z, History.Current.GridMm));
                input.Use();
            }
            if (input.type == EventType.MouseUp && input.button == 0 && resizingZone != null)
            {
                string id = resizingZone;
                Edit("Resize zone", draft =>
                {
                    var zone = draft.Course.Zones.Single(item => item.Id == id);
                    zone.Bounds = ResizeBounds(zone.Bounds, resizeAnchor, resizePoint);
                    WorkshopCommands.SetZone(draft, zone, Catalogue);
                });
                resizingZone = null; input.Use();
            }
            if (input.type == EventType.MouseDrag && dragging && (gesture != null || placementGesture != null))
            {
                if (activeHandle == WorkshopHandle.Rotate)
                {
                    var offset = mouse - ScreenPoint(gesturePivot);
                    float degrees = Mathf.DeltaAngle(startAngle, Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg);
                    if (placementGesture != null) placementGesture.Rotate(degrees); else gesture.Rotate(degrees);
                }
                else
                {
                    var point = WorldPoint(mouse);
                    int x = activeHandle == WorkshopHandle.Z ? 0 : point.X - gestureStart.X;
                    int z = activeHandle == WorkshopHandle.X ? 0 : point.Z - gestureStart.Z;
                    if (placementGesture != null) placementGesture.Move(x, z); else gesture.Move(x, z);
                }
                UpdateGesturePreview(); input.Use();
            }
            if (input.type == EventType.MouseUp && input.button == 0 && (dragging || marquee))
            {
                if (dragging && (gesture != null || placementGesture != null)) CommitGesture();
                else if (drawingZone) DrawZone(WorldPoint(pointerStart), WorldPoint(mouse));
                else
                {
                    var area = DragRect(pointerStart, mouse);
                    History.Select(display.Modules.Where(module => module.LayerId == History.Current.ActiveLayer &&
                        !History.Current.HiddenLayers.Contains(module.LayerId) && !History.Current.LockedLayers.Contains(module.LayerId) &&
                        Catalogue.Modules.ContainsKey(module.DefinitionId) && area.Overlaps(ScreenBox(Catalogue.Bounds(module))))
                        .Select(module => module.Id), addSelection);
                    Refresh();
                }
                gesture = null; placementGesture = null; dragging = marquee = drawingZone = false;
                view.TransformPreview(null, default); view.PlacementPreview(null); input.Use();
            }
            if (input.type == EventType.KeyDown && input.keyCode == KeyCode.Escape)
            { CancelGesture(); input.Use(); }
            else if (input.type == EventType.KeyDown && GUIUtility.keyboardControl == 0) KeyboardAction(input);
        }
        void KeyboardAction(Event input)
        {
            if (input.control && input.keyCode == KeyCode.Z) { CancelGesture(); if (input.shift) History.Redo(); else History.Undo(); Refresh(); }
            else if (input.control && input.keyCode == KeyCode.Y) { CancelGesture(); History.Redo(); Refresh(); }
            else if (input.control && input.keyCode == KeyCode.D) DuplicateSelection();
            else if (input.keyCode == KeyCode.R) RotateSelection();
            else if (input.keyCode == KeyCode.Delete) DeleteSelection();
            else if (input.keyCode == KeyCode.F) FrameSelection();
            else if (input.keyCode == KeyCode.Tab) StepSelection(input.shift ? -1 : 1);
            else return;
            input.Use();
        }
        void RotateSelection()
        {
            if (SelectedSite != null)
            {
                var copy = WorkshopPhysicalSites.Copy(SelectedSite); copy.Yaw = (copy.Yaw + 90) % 360;
                Edit("Rotate puzzle socket", draft => WorkshopPhysicalSites.Set(draft, copy));
            }
            else if (SelectedFixed != null)
            {
                var rotate = new WorkshopPlacementDrag(History, SelectedFixed.Id);
                rotate.Rotate(90); rotate.Commit(History, Catalogue); Refresh();
            }
            else Edit("Rotate", draft => WorkshopCommands.RotateSelection(draft, SelectionPivot(), 1));
        }
        void DuplicateSelection()
        {
            if (SelectedSite != null) { status = "Use Duplicate module to copy a puzzle and all its linked controls."; return; }
            if (SelectedFixed == null)
            {
                Edit("Duplicate", draft => WorkshopCommands.Duplicate(draft, new CoursePoint(draft.GridMm * 2, 0, draft.GridMm * 2)));
                return;
            }
            string originalId = SelectedFixed.Id;
            Attempt(() =>
            {
                string nextId = WorkshopCommands.Unique("object", display.Zones.Select(zone => zone.Id));
                History.Execute("Duplicate object", draft =>
                {
                    var copy = draft.Course.Clone().Zones.Single(zone => zone.Id == originalId);
                    copy.Id = nextId;
                    copy.FixedPosition = new CoursePoint(copy.FixedPosition.X + draft.GridMm, copy.FixedPosition.Y, copy.FixedPosition.Z + draft.GridMm);
                    WorkshopCommands.SetZone(draft, copy, Catalogue);
                });
                zoneId = nextId; Refresh();
            });
        }
        void StepSelection(int direction)
        {
            string next = WorkshopSelection.Step(History.Current, direction);
            if (next == null) return;
            CancelGesture(); zoneId = ""; sitePuzzle = ""; ShowInspectorSection(InspectorSection.Room);
            History.Select(new[] { next }, controllerAdd); Refresh(); FrameSelection();
        }
        void ControllerPanel()
        {
            GUILayout.Space(8);
            if (Button(controllerTools ? "Controller tools · on" : "Controller tools", "Start opens tools. D-pad changes action; A applies it."))
                controllerTools = !controllerTools;
            if (controllerTools)
            {
                GUILayout.Label("D-pad: action · A: apply · B: back");
                if (Button("‹ " + controllerActions[controllerAction] + " ›")) Attempt(ApplyControllerAction);
            }
            GUILayout.Label("Pad: D-pad selects · Stick moves\nRight stick orbits · Triggers zoom\nX rotates · Y multiselect · LB/RB undo/redo");
            if (SelectedSite != null) GUILayout.Label("Puzzle controls: " + sitePart + "\nD-pad changes control · Stick moves it · X turns socket");
        }
        void ControllerInput(bool focused)
        {
            var pad = Gamepad.current;
            if (!workshopPadGate.CanRead(pad, focused && Visible && !sharing && !OnlineStarting,
                workshopTyping || DesktopBinding.Typing || KeyBindings.GameplayBlocked))
            { if (controllerMoving || controllerTools) CancelGesture(); controllerTools = false; return; }
            try
            {
                if (pad.startButton.wasPressedThisFrame) { controllerTools = !controllerTools; CancelGesture(); }
                if (pad.buttonEast.wasPressedThisFrame) { controllerTools = false; CancelGesture(); return; }
                EnsureView();
                view.Orbit(pad.rightStick.ReadValue() * new Vector2(1, -1) * Time.unscaledDeltaTime * 110);
                float zoomInput = pad.leftTrigger.ReadValue() - pad.rightTrigger.ReadValue();
                if (Mathf.Abs(zoomInput) > .05f) view.Zoom(zoomInput * Time.unscaledDeltaTime * 10);
                if (controllerTools)
                {
                    if (pad.dpad.down.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame) controllerAction = (controllerAction + 1) % controllerActions.Length;
                    if (pad.dpad.up.wasPressedThisFrame || pad.dpad.left.wasPressedThisFrame) controllerAction = (controllerAction + controllerActions.Length - 1) % controllerActions.Length;
                    if (pad.buttonSouth.wasPressedThisFrame) ApplyControllerAction();
                    return;
                }
                if (pad.dpad.right.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame)
                { if (SelectedSite != null) StepPhysicalControl(1); else StepSelection(1); }
                if (pad.dpad.left.wasPressedThisFrame || pad.dpad.up.wasPressedThisFrame)
                { if (SelectedSite != null) StepPhysicalControl(-1); else StepSelection(-1); }
                if (pad.buttonNorth.wasPressedThisFrame) { controllerAdd = !controllerAdd; status = controllerAdd ? "Multiselect on" : "Multiselect off"; }
                if (pad.buttonSouth.wasPressedThisFrame) FrameSelection();
                if (pad.buttonWest.wasPressedThisFrame) RotateSelection();
                if (pad.leftShoulder.wasPressedThisFrame) { CancelGesture(); History.Undo(); Refresh(); }
                if (pad.rightShoulder.wasPressedThisFrame) { CancelGesture(); History.Redo(); Refresh(); }
                var move = pad.leftStick.ReadValue();
                if (move.magnitude > .5f && History.Current.Selection.Count > 0)
                {
                    if (!controllerMoving)
                    {
                        CancelGesture(); controllerMoving = true; controllerX = controllerZ = 0;
                        gesturePivot = SelectionPivot();
                        if (SelectedSite != null) siteDrag = new WorkshopSiteDrag(History, siteLap, sitePuzzle, sitePart, siteFixtureIndex);
                        else if (SelectedFixed != null) placementGesture = new WorkshopPlacementDrag(History, SelectedFixed.Id);
                        else gesture = new WorkshopTransformDrag(History, WorkshopHandle.Plane, gesturePivot);
                        controllerRepeat = 0;
                    }
                    if (Time.unscaledTime >= controllerRepeat)
                    {
                        controllerX += Mathf.Abs(move.x) > .5f ? Math.Sign(move.x) * History.Current.GridMm : 0;
                        controllerZ += Mathf.Abs(move.y) > .5f ? Math.Sign(move.y) * History.Current.GridMm : 0;
                        if (siteDrag != null) siteDrag.Move(controllerX, controllerZ);
                        else if (placementGesture != null) placementGesture.Move(controllerX, controllerZ); else gesture.Move(controllerX, controllerZ);
                        UpdateGesturePreview();
                        controllerRepeat = Time.unscaledTime + .14f;
                    }
                }
                else if (controllerMoving)
                {
                    CommitGesture(); CancelGesture();
                }
            }
            catch (Exception error) { status = error.Message; CancelGesture(); }
        }
        void ApplyControllerAction()
        {
            switch (controllerAction)
            {
                case 0: StepSelection(1); break;
                case 1: StepSelection(-1); break;
                case 2: RotateSelection(); break;
                case 3: DuplicateSelection(); break;
                case 4: CancelGesture(); History.Undo(); Refresh(); break;
                case 5: CancelGesture(); History.Redo(); Refresh(); break;
                case 6: FrameSelection(); break;
                case 7: Preview(); break;
                case 8: RequestPlaytest(); break;
                case 9:
                    var definition = Catalogue.Modules.Keys.OrderBy(id => id, StringComparer.Ordinal).FirstOrDefault();
                    if (definition != null)
                        Edit("Place module", draft => WorkshopCommands.AddModule(draft, definition,
                            new CoursePoint((int)(view.Target.x * 1000), ActiveHeight, (int)(view.Target.z * 1000))));
                    break;
                case 10:
                    var layers = display.Layers.OrderBy(layer => layer.HeightMm).ToList();
                    int next = (layers.FindIndex(layer => layer.Id == History.Current.ActiveLayer) + 1) % layers.Count;
                    SelectFloor(layers[next].Id); break;
                case 11: shareText = History.Current.CourseJsonText; sharing = true; break;
                case 12: StepPhysicalSite(); break;
                case 13: StepPhysicalControl(1); break;
                case 14: CancelGesture(); sitePuzzle = ""; status = "Module editing selected"; break;
                case 15: QueueControllerSourceChoice(); break;
            }
        }
        void UpdateGesturePreview()
        {
            if (siteDrag != null) return;
            if (placementGesture != null) view.PlacementPreview(placementGesture);
            else view.TransformPreview(gesture, gesturePivot);
        }
        void CommitGesture()
        {
            if (siteDrag != null) siteDrag.Commit(History);
            else if (placementGesture != null) placementGesture.Commit(History, Catalogue); else gesture.Commit(History);
            Refresh();
        }
        void DeleteSelection()
        {
            if (SelectedSite != null)
            {
                var selected = SelectedSite;
                Edit("Delete physical controls", draft => WorkshopPhysicalSites.Delete(draft, selected.Lap, selected.PuzzleId));
                return;
            }
            string fixedId = SelectedFixed?.Id;
            if (fixedId == null) Edit("Delete", WorkshopCommands.Delete);
            else Edit("Delete object", draft =>
            {
                var zone = draft.Course.Zones.Single(item => item.Id == fixedId);
                var module = draft.Course.Modules.Single(item => item.Id == zone.ModuleId);
                if (draft.LockedLayers.Contains(module.LayerId)) throw new InvalidOperationException("Unlock the floor first.");
                draft.Course.Zones.Remove(zone);
            });
        }
        void FixedInspector()
        {
            var zone = SelectedFixed;
            GUILayout.Label(ObjectLabel(zone.Pool[0].AssetId), heading);
            GUILayout.BeginHorizontal();
            if (ToolButton("Rotate object", LobbyIcons.Turn, "Rotate object · R")) Attempt(RotateSelection);
            if (ToolButton("Duplicate object", LobbyIcons.Duplicate)) DuplicateSelection();
            if (ToolButton("Delete object", LobbyIcons.Delete)) DeleteSelection();
            GUILayout.EndHorizontal();
            if (Button("Select room")) { zoneId = ""; ShowInspectorSection(InspectorSection.Room); }
            GUILayout.Label("Drag the object or its X / Z handles.\nR rotates in quarter-turns.");
        }
        void SelectZone(CourseZone zone)
        {
            CancelGesture(); ShowInspectorSection(InspectorSection.Objects); placementPickerOpen = false; zoneId = zone.Id; LoadPlacementFields(zone);
        }
        void PlacementAssetPicker()
        {
            assetSearch = Field("Find object", assetSearch);
            var assets = Catalogue.Assets.Keys.Where(id => id.IndexOf(assetSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(id => id, StringComparer.Ordinal).Take(12).ToArray();
            GUILayout.Label(zoneMode == 2 ? "Choose pool members" : "Choose one object");
            for (int i = 0; i < assets.Length; i++)
            {
                if (i % 2 == 0) GUILayout.BeginHorizontal();
                string id = assets[i];
                var entries = pool.Split(',').Select(entry => entry.Trim()).Where(entry => entry.Length > 0).ToList();
                bool included = entries.Any(entry => entry.Split(':')[0].Trim() == id);
                if (PlacementAssetButton(id, included))
                {
                    if (zoneMode != 2) pool = id;
                    else
                    {
                        entries.RemoveAll(entry => entry.Split(':')[0].Trim() == id);
                        if (!included) entries.Add(id + ":1");
                        pool = string.Join(", ", entries);
                    }
                }
                if (i % 2 == 1 || i == assets.Length - 1) GUILayout.EndHorizontal();
            }
            if (zoneMode == 2) PoolWeightRows();
            else GUILayout.Label("Selected: " + ObjectLabel(pool.Split(',')[0].Split(':')[0]));
        }

        Texture ObjectIcon(string id) => AssetIcon?.Invoke(id) ?? UI.OriginalItemArt.Find(id);

        static string ObjectLabel(string id)
        {
            if (string.IsNullOrEmpty(id)) return "Object";
            string words = System.Text.RegularExpressions.Regex.Replace(id, "(?<=[a-z0-9])(?=[A-Z])", " ").Replace('_', ' ');
            if (words.StartsWith("Props ", StringComparison.OrdinalIgnoreCase)) words = words.Substring(6);
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(words.ToLowerInvariant());
        }

        bool PlacementAssetButton(string id, bool included)
        {
            var icon = ObjectIcon(id);
            string name = ObjectLabel(id), caption = (included ? "✓ " : "") + name;
            GUI.SetNextControlName("Choose " + id);
            if (!icon) return ui.Button(new GUIContent(caption, "Select " + name), included ? ui.Selected : null,
                GUILayout.MinHeight(ui.RowHeight), GUILayout.ExpandWidth(true));
            bool clicked = ui.Button(new GUIContent("", "Select " + name), included ? ui.Selected : null,
                GUILayout.Height(102), GUILayout.ExpandWidth(true));
            if (Event.current.type != EventType.Repaint) return clicked;
            var tile = GUILayoutUtility.GetLastRect();
            var picture = new Rect(tile.x + 8, tile.y + 6, Mathf.Max(1, tile.width - 16), 64);
            GUI.DrawTexture(picture, icon, ScaleMode.ScaleToFit, true);
            objectTileName ??= new GUIStyle(ui.SingleLine) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
            GUI.Label(new Rect(tile.x + 4, tile.yMax - 28, Mathf.Max(1, tile.width - 8), 24), caption, objectTileName);
            return clicked;
        }
    }
}
