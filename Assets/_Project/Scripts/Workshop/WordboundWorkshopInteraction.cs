using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Wreckabulary.Art;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Workshop
{
    public sealed partial class WordboundWorkshop
    {
        WordboundWorkshopViewport view;
        WorkshopTransformDrag gesture;
        CoursePoint gestureStart, gesturePivot;
        Vector2 lastPointer;
        bool orbiting, controllerTools, controllerMoving, controllerAdd;
        float startAngle, controllerRepeat;
        int controllerAction, controllerX, controllerZ;
        readonly string[] controllerActions =
        {
            "Next module", "Previous module", "Rotate", "Duplicate", "Undo", "Redo",
            "Frame selection", "Preview", "Playtest", "Add module", "Next floor", "Share"
        };
        public Func<string, Transform, GameObject> SpawnAssetVisual { get; set; }

        void EnsureView()
        {
            if (view != null) return;
            view = new WordboundWorkshopViewport(transform);
            view.SpawnAssetVisual = (id, parent) => SpawnAssetVisual != null
                ? SpawnAssetVisual(id, parent) : ModelVisual.Spawn("Items/" + id, parent);
            view.Sync(History.Current, Catalogue);
            view.Frame();
        }
        void OnDestroy() { view?.Dispose(); view = null; }
        void Frame()
        {
            if (History == null || Catalogue == null) return;
            EnsureView(); view.Sync(History.Current, Catalogue); view.Frame();
        }
        void FrameSelection()
        {
            EnsureView();
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
            foreach (var module in display.Modules.Where(module => module.LayerId == History.Current.ActiveLayer &&
                !History.Current.HiddenLayers.Contains(module.LayerId)))
            {
                var at = ScreenPoint(module.Position);
                GUI.Label(new Rect(at.x - 75, at.y - 22, 150, 20), module.Id +
                    (History.Current.LockedLayers.Contains(module.LayerId) ? " · locked" : ""),
                    History.Current.Selection.Contains(module.Id) ? selectedBox : normalBox);
            }
            foreach (var zone in display.Zones.Where(item => item.LayerIds.Contains(History.Current.ActiveLayer) &&
                !History.Current.HiddenLayers.Contains(History.Current.ActiveLayer)))
            {
                var bounds = zone.Id == resizingZone ? ResizeBounds(zone.Bounds, resizeAnchor, resizePoint) : zone.Bounds;
                var corners = Corners(bounds);
                var color = validation.Issues.Any(issue => issue.EntityId == zone.Id) ? new Color(1, .5f, .35f) : new Color(.4f, 1, .8f);
                for (int i = 0; i < 4; i++) DrawLine(ScreenPoint(corners[i]), ScreenPoint(corners[(i + 1) % 4]), color);
                var at = ScreenPoint(zone.Placement == "Fixed" ? zone.FixedPosition : bounds.Min);
                GUI.Label(new Rect(at.x, at.y, 160, 22), zone.Id);
                if (zone.Id == zoneId && zone.Placement != "Fixed")
                    foreach (var corner in corners)
                    { at = ScreenPoint(corner); GUI.Box(new Rect(at.x - 6, at.y - 6, 12, 12), ""); }
            }
            if (History.Current.Selection.Count > 0 && !drawingZone && placing == null) Handles();
            if (marquee) GUI.Box(DragRect(pointerStart, Event.current.mousePosition), "", selectedBox);
            GUI.EndGroup();
            try { HandlePointer(); }
            catch (Exception error) { status = error.Message; CancelGesture(); }
        }
        void Handles()
        {
            var pivot = SelectionPivot(); var at = ScreenPoint(pivot);
            var x = ScreenPoint(HandleEnd(pivot, WorkshopHandle.X));
            var z = ScreenPoint(HandleEnd(pivot, WorkshopHandle.Z));
            DrawLine(at, x, new Color(1, .45f, .4f)); DrawLine(at, z, new Color(.4f, .65f, 1));
            GUI.Box(new Rect(x.x - 12, x.y - 12, 24, 24), "X");
            GUI.Box(new Rect(z.x - 12, z.y - 12, 24, 24), "Z");
            GUI.Box(new Rect(at.x - 12, at.y - 12, 24, 24), "↔");
            GUI.Box(new Rect(at.x - 14, at.y - 58, 28, 28), "R");
        }
        WorkshopHandle? HandleAt(Vector2 point)
        {
            if (History.Current.Selection.Count == 0) return null;
            var pivot = SelectionPivot(); var at = ScreenPoint(pivot);
            if (new Rect(at.x - 14, at.y - 58, 28, 28).Contains(point)) return WorkshopHandle.Rotate;
            if (Vector2.Distance(point, ScreenPoint(HandleEnd(pivot, WorkshopHandle.X))) < 17) return WorkshopHandle.X;
            if (Vector2.Distance(point, ScreenPoint(HandleEnd(pivot, WorkshopHandle.Z))) < 17) return WorkshopHandle.Z;
            return Vector2.Distance(point, at) < 17 ? WorkshopHandle.Plane : (WorkshopHandle?)null;
        }
        void StartGesture(WorkshopHandle handle, Vector2 mouse)
        {
            gesturePivot = SelectionPivot();
            gesture = new WorkshopTransformDrag(History, handle, gesturePivot);
            gestureStart = WorldPoint(mouse);
            var offset = mouse - ScreenPoint(gesturePivot);
            startAngle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            dragging = true; activeHandle = handle;
        }
        WorkshopHandle activeHandle;
        void CancelGesture()
        {
            gesture = null; dragging = marquee = panning = drawingZone = orbiting = controllerMoving = false;
            resizingZone = null; placing = null; view?.TransformPreview(null, default);
        }
        void HandlePointer()
        {
            if (sharing) return;
            var input = Event.current; Vector2 mouse = input.mousePosition - viewport.position;
            bool inside = viewport.Contains(input.mousePosition) &&
                !(mouse.x < Mathf.Min(448, viewport.width - 8) && mouse.y < 106);
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
                        string hit = view.Pick(mouse, viewport);
                        if (hit != null)
                        {
                            if (!History.Current.Selection.Contains(hit) || addSelection) History.Select(new[] { hit }, addSelection);
                            Refresh(); StartGesture(WorkshopHandle.Plane, mouse);
                        }
                        else marquee = true;
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
            if (input.type == EventType.MouseDrag && dragging && gesture != null)
            {
                if (activeHandle == WorkshopHandle.Rotate)
                {
                    var offset = mouse - ScreenPoint(gesturePivot);
                    gesture.Rotate(Mathf.DeltaAngle(startAngle, Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg));
                }
                else
                {
                    var point = WorldPoint(mouse);
                    gesture.Move(point.X - gestureStart.X, point.Z - gestureStart.Z);
                }
                view.TransformPreview(gesture, gesturePivot); input.Use();
            }
            if (input.type == EventType.MouseUp && input.button == 0 && (dragging || marquee))
            {
                if (dragging && gesture != null) { gesture.Commit(History); Refresh(); }
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
                gesture = null; dragging = marquee = drawingZone = false;
                view.TransformPreview(null, default); input.Use();
            }
            if (input.type == EventType.KeyDown && input.keyCode == KeyCode.Escape)
            { CancelGesture(); input.Use(); }
            else if (input.type == EventType.KeyDown && GUIUtility.keyboardControl == 0) KeyboardAction(input);
        }
        void KeyboardAction(Event input)
        {
            if (input.control && input.keyCode == KeyCode.Z) { if (input.shift) History.Redo(); else History.Undo(); Refresh(); }
            else if (input.control && input.keyCode == KeyCode.Y) { History.Redo(); Refresh(); }
            else if (input.control && input.keyCode == KeyCode.D) DuplicateSelection();
            else if (input.keyCode == KeyCode.R) RotateSelection();
            else if (input.keyCode == KeyCode.Delete) Edit("Delete", WorkshopCommands.Delete);
            else if (input.keyCode == KeyCode.F) FrameSelection();
            else if (input.keyCode == KeyCode.Tab) StepSelection(input.shift ? -1 : 1);
            else return;
            input.Use();
        }
        void RotateSelection() => Edit("Rotate", draft => WorkshopCommands.RotateSelection(draft, SelectionPivot(), 1));
        void DuplicateSelection() => Edit("Duplicate", draft => WorkshopCommands.Duplicate(draft, new CoursePoint(draft.GridMm * 2, 0, draft.GridMm * 2)));
        void StepSelection(int direction)
        {
            string next = WorkshopSelection.Step(History.Current, direction);
            if (next == null) return;
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
        }
        void ControllerInput()
        {
            var pad = Gamepad.current;
            if (pad == null) return;
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
                if (pad.dpad.right.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame) StepSelection(1);
                if (pad.dpad.left.wasPressedThisFrame || pad.dpad.up.wasPressedThisFrame) StepSelection(-1);
                if (pad.buttonNorth.wasPressedThisFrame) { controllerAdd = !controllerAdd; status = controllerAdd ? "Multiselect on" : "Multiselect off"; }
                if (pad.buttonSouth.wasPressedThisFrame) FrameSelection();
                if (pad.buttonWest.wasPressedThisFrame) RotateSelection();
                if (pad.leftShoulder.wasPressedThisFrame) { History.Undo(); Refresh(); }
                if (pad.rightShoulder.wasPressedThisFrame) { History.Redo(); Refresh(); }
                var move = pad.leftStick.ReadValue();
                if (move.magnitude > .5f && History.Current.Selection.Count > 0)
                {
                    if (!controllerMoving)
                    {
                        CancelGesture(); controllerMoving = true; controllerX = controllerZ = 0;
                        gesturePivot = SelectionPivot(); gesture = new WorkshopTransformDrag(History, WorkshopHandle.Plane, gesturePivot);
                        controllerRepeat = 0;
                    }
                    if (Time.unscaledTime >= controllerRepeat)
                    {
                        controllerX += Mathf.Abs(move.x) > .5f ? Math.Sign(move.x) * History.Current.GridMm : 0;
                        controllerZ += Mathf.Abs(move.y) > .5f ? Math.Sign(move.y) * History.Current.GridMm : 0;
                        gesture.Move(controllerX, controllerZ); view.TransformPreview(gesture, gesturePivot);
                        controllerRepeat = Time.unscaledTime + .14f;
                    }
                }
                else if (controllerMoving)
                {
                    gesture.Commit(History); Refresh(); CancelGesture();
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
                case 4: History.Undo(); Refresh(); break;
                case 5: History.Redo(); Refresh(); break;
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
                    Edit("Select floor", draft => draft.ActiveLayer = layers[next].Id); break;
                case 11: shareText = History.Current.CourseJsonText; sharing = true; break;
            }
        }
    }
}
