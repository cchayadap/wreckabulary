using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class CreativeWorkshop : MonoBehaviour
    {
        static readonly Color Cream = new(.97f,.94f,.86f), Ink = new(.18f,.23f,.23f), Teal = new(.13f,.48f,.48f);
        static readonly string[] Skins = { "Classic", "Candy", "Arcade" };
        public static CreativeWorkshop Instance { get; private set; }
        readonly Dictionary<string, HomeHistory> drafts = new();
        readonly List<(GameObject root, bool active)> roots = new();
        readonly List<(Behaviour component, bool enabled)> suspended = new();
        readonly List<(Renderer component, bool enabled)> hiddenRenderers = new();
        readonly List<(Collider component, bool enabled)> hiddenColliders = new();
        readonly List<(Canvas component, bool enabled)> hiddenCanvases = new();
        readonly List<BodyPause> pausedBodies = new();
        readonly List<(RectTransform rect, Vector2 at, Vector2 size)> toolRows = new();
        readonly List<(TMP_Text text, float size)> toolText = new();
        readonly List<RaycastResult> uiHits = new();
        HomeDesigner designer;
        HomeStorage storage;
        HomeHistory history;
        CreativeHomeRuntime stage;
        HomeTourDirector tour;
        InputBinding tourBinding;
        Camera lens;
        Vector3 cameraPosition;
        Quaternion cameraRotation;
        Rect cameraRect;
        bool cameraOrtho, touchEnabled, touchOverlay, captured, restored, refreshing;
        string touchId, map, selected, placing;
        float cameraSize, cameraFov, oldTimeScale;
        GameObject priorSelection;
        Canvas canvas;
        ScrollRect toolScroll;
        GameObject lastMenuSelection;
        RectTransform safe, header, dock, toolContent, tourBar, touchControls, jsonSheet;
        TMP_InputField nameInput, wordsInput, jsonInput;
        TMP_Dropdown roomInput, catalogueInput, mapInput;
        TextMeshProUGUI status, selectionLabel, jsonStatus, jsonTitle, headerTitle, tourTitle;
        Button undoButton, redoButton, saveButton, tourButton, backButton, tourBackButton, tourExitButton, jsonCopyButton, jsonApplyButton, jsonCloseButton;
        string[] mapIds, words;
        bool moving, portrait;
        HomeProp preview;
        string previewKey;

        public bool Open(string mapId, out string error)
        {
            error = null;
            if (Instance && Instance != this) { error = "A workshop is already open."; return false; }
            try
            {
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != Session.HubScene)
                    throw new InvalidOperationException("Open the workshop from the front door.");
                designer = new HomeDesigner(GameConfig.Current.Houses, GameConfig.Current.Items);
                storage = new HomeStorage(designer);
                GameConfig.Current.HouseFor(mapId);
                lens = Camera.main;
                if (!lens) throw new InvalidOperationException("The house has no main camera.");
                tourBinding = World.Players.FirstOrDefault(p => p && p.Binding is not BotBinding)?.Binding
                    ?? Session.Bindings.FirstOrDefault()
                    ?? (Gamepad.current != null ? (InputBinding)new GamepadBinding(Gamepad.current) : DesktopBinding.Shared);
                CaptureScene();
                Instance = this;
                stage = new GameObject("Creative house").AddComponent<CreativeHomeRuntime>();
                stage.transform.SetParent(transform, false);
                BuildUi();
                if (!SwitchMap(mapId, out error)) throw new InvalidOperationException(error);
                EventSystem.current?.SetSelectedGameObject(header.GetComponentInChildren<Button>().gameObject);
                return true;
            }
            catch (Exception ex) { error = ex.Message; Close(); return false; }
        }

        void CaptureScene()
        {
            cameraPosition = lens.transform.position; cameraRotation = lens.transform.rotation;
            cameraRect = lens.rect; cameraOrtho = lens.orthographic; cameraSize = lens.orthographicSize; cameraFov = lens.fieldOfView;
            oldTimeScale = Time.timeScale; Time.timeScale = 1f;
            priorSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            touchEnabled = TouchBinding.Shared.Enabled; touchOverlay = TouchBinding.Shared.OverlayDesktop;
            touchId = TouchBinding.Shared.OverlayBindingId; TouchBinding.Shared.ReleaseAll();
            captured = true;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (root == gameObject) continue;
                roots.Add((root, root.activeSelf));
                bool keep = lens.transform.IsChildOf(root.transform) || root.GetComponentInChildren<Light>(true)
                    || root.GetComponentInChildren<EventSystem>(true) || root.GetComponentInChildren<PlayerJoinManager>(true);
                if (!keep) root.SetActive(false);
                else
                {
                    foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (behaviour is EventSystem || behaviour is UnityEngine.EventSystems.BaseInputModule) continue;
                        suspended.Add((behaviour, behaviour.enabled)); behaviour.enabled = false;
                    }
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    { hiddenRenderers.Add((renderer,renderer.enabled)); renderer.enabled = false; }
                    foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                    { hiddenColliders.Add((collider,collider.enabled)); collider.enabled = false; }
                    foreach (var oldCanvas in root.GetComponentsInChildren<Canvas>(true))
                    { hiddenCanvases.Add((oldCanvas,oldCanvas.enabled)); oldCanvas.enabled = false; }
                    foreach (var body in root.GetComponentsInChildren<Rigidbody>(true))
                    { var pause = new BodyPause(body); pausedBodies.Add(pause); pause.Freeze(); }
                }
            }
        }

        bool SwitchMap(string id, out string error)
        {
            error = null;
            if (!drafts.TryGetValue(id, out var next))
            {
                var initial = designer.CreateLayout(id);
                storage.TryLoad(id, out var saved, out string loadError);
                if (saved != null) initial = saved;
                next = new HomeHistory(designer, initial); drafts[id] = next;
                if (loadError != null) SetStatus(loadError);
            }
            if (!stage.Show(next.Current, out error)) return false;
            map = id; history = next; selected = placing = null; moving = false; previewKey = null;
            Refresh(); LayoutUi();
            return true;
        }

        void BuildUi()
        {
            mapIds = GameConfig.Current.Houses.Keys.Where(HomeDesigner.Supports).OrderBy(id => id).ToArray();
            words = GameConfig.Current.Items.All.Where(i => !string.IsNullOrEmpty(i.Model)).Select(i => i.Id).OrderBy(w => w).ToArray();
            canvas = new GameObject("Workshop UI", typeof(RectTransform)).AddComponent<Canvas>();
            canvas.transform.SetParent(transform,false); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280,720); scaler.matchWidthOrHeight = .5f;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            safe = Rect(canvas.transform,"Safe area",Vector2.zero,Vector2.zero); Stretch(safe);
            header = Panel(safe,"Workshop header",Cream);
            headerTitle = Label(header,"CREATIVE WORKSHOP",new Vector2(16,-12),new Vector2(280,44),26,Ink);
            nameInput = Input(header,"My Cozy House",new Vector2(306,-14),new Vector2(350,42),48,false);
            nameInput.onEndEdit.AddListener(value =>
            {
                if (refreshing || history == null) return;
                var candidate = history.Current; candidate.Name = value.Trim(); Commit(candidate, "Home renamed.");
            });
            saveButton = MakeButton(header,"Save",new Vector2(670,-12),new Vector2(105,46),Save);
            tourButton = MakeButton(header,"Tour",new Vector2(785,-12),new Vector2(105,46),BeginTour);
            backButton = MakeButton(header,"Back",new Vector2(900,-12),new Vector2(105,46),Close);
            dock = Panel(safe,"Design tools",Cream);
            var scroll = new GameObject("Tool scroll",typeof(RectTransform)).AddComponent<ScrollRect>();
            toolScroll = scroll;
            scroll.transform.SetParent(dock,false); Stretch((RectTransform)scroll.transform);
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Panel(scroll.transform,"Viewport",Color.clear); Stretch(viewport);
            var mask = viewport.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = false;
            var content = Rect(viewport,"Tools",Vector2.zero,new Vector2(348,990));
            content.anchorMin = new Vector2(0,1); content.anchorMax = new Vector2(1,1); content.pivot = new Vector2(.5f,1);
            content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(0,990);
            scroll.viewport = viewport; scroll.content = content;
            mapInput = Dropdown(content,mapIds.Select(id => GameConfig.Current.HouseFor(id).Name).ToArray(),new Vector2(12,-12),new Vector2(320,44));
            mapInput.onValueChanged.AddListener(index =>
            {
                if (refreshing) return;
                if (!SwitchMap(mapIds[index],out var error)) { SetStatus(error); Refresh(); }
            });
            Label(content,"CHOOSE ROOM",new Vector2(12,-66),new Vector2(320,28),18,Teal);
            roomInput = Dropdown(content,Array.Empty<string>(),new Vector2(12,-98),new Vector2(320,44));
            roomInput.onValueChanged.AddListener(_ => CancelPending());
            Label(content,"TYPE OBJECT WORDS",new Vector2(12,-154),new Vector2(320,28),18,Teal);
            wordsInput = Input(content,"SOFA TABLE PLANT",new Vector2(12,-188),new Vector2(320,90),HomeDesigner.MaxTextLength,true);
            MakeButton(content,"Place words in room",new Vector2(12,-290),new Vector2(320,46),() => PlaceWords(wordsInput.text));
            Label(content,"OR CHOOSE ANY OF 40 OBJECTS",new Vector2(12,-348),new Vector2(320,28),18,Teal);
            catalogueInput = Dropdown(content,words,new Vector2(12,-382),new Vector2(320,44));
            MakeButton(content,"Preview & tap floor",new Vector2(12,-438),new Vector2(156,48),() =>
            { placing = words[catalogueInput.value]; moving = false; selected = null; RefreshSelection(); SetStatus("Tap a safe grid position in " + RoomName + "."); });
            MakeButton(content,"Auto-place",new Vector2(178,-438),new Vector2(154,48),() => PlaceWords(words[catalogueInput.value]));
            selectionLabel = Label(content,"Tap a prop to select it",new Vector2(12,-502),new Vector2(320,58),22,Ink);
            MakeButton(content,"Select previous",new Vector2(12,-564),new Vector2(156,38),() => SelectProp(-1));
            MakeButton(content,"Select next",new Vector2(178,-564),new Vector2(154,38),() => SelectProp(1));
            MakeButton(content,"Move / tap floor",new Vector2(12,-572),new Vector2(156,46),() =>
            { if (Selected == null) { SetStatus("Select a prop first."); return; } placing = null; moving = true; EventSystem.current?.SetSelectedGameObject(null); SetStatus("Tap a safe position, or use grid arrows / controller D-pad. A confirms; B cancels."); });
            MakeButton(content,"Rotate 90°",new Vector2(178,-572),new Vector2(154,46),Rotate);
            MakeButton(content,"←",new Vector2(12,-628),new Vector2(72,46),() => Nudge(-.5,0));
            MakeButton(content,"↑",new Vector2(94,-628),new Vector2(72,46),() => Nudge(0,.5));
            MakeButton(content,"↓",new Vector2(178,-628),new Vector2(72,46),() => Nudge(0,-.5));
            MakeButton(content,"→",new Vector2(260,-628),new Vector2(72,46),() => Nudge(.5,0));
            MakeButton(content,"Change finish",new Vector2(12,-684),new Vector2(156,46),ChangeSkin);
            MakeButton(content,"Remove",new Vector2(178,-684),new Vector2(154,46),Remove);
            undoButton = MakeButton(content,"Undo",new Vector2(12,-740),new Vector2(156,46),() => HistoryStep(false));
            redoButton = MakeButton(content,"Redo",new Vector2(178,-740),new Vector2(154,46),() => HistoryStep(true));
            MakeButton(content,"Export JSON",new Vector2(12,-796),new Vector2(156,46),() => OpenJson(false));
            MakeButton(content,"Import JSON",new Vector2(178,-796),new Vector2(154,46),() => OpenJson(true));
            status = Label(content,"",new Vector2(12,-854),new Vector2(320,125),18,Ink);
            status.textWrappingMode = TextWrappingModes.Normal;
            foreach (RectTransform child in content)
                if (child.anchoredPosition.y <= -572) child.anchoredPosition += new Vector2(0,-40);
            content.sizeDelta = new Vector2(0,1030);
            toolContent = content;
            foreach (RectTransform child in content) toolRows.Add((child,child.anchoredPosition,child.sizeDelta));
            foreach (var text in content.GetComponentsInChildren<TMP_Text>(true)) toolText.Add((text,text.fontSize));
            tourBar = Panel(safe,"Peaceful tour",Cream); tourBar.gameObject.SetActive(false);
            tourTitle = Label(tourBar,"YOUR SAVED HOME  ·  peaceful tour",new Vector2(12,-12),new Vector2(580,40),22,Ink);
            tourBackButton = MakeButton(tourBar,"Back to workshop",new Vector2(610,-10),new Vector2(260,46),EndTour);
            tourExitButton = MakeButton(tourBar,"Exit",new Vector2(880,-10),new Vector2(110,46),Close);
            touchControls = Rect(safe,"Tour touch controls",Vector2.zero,Vector2.zero); Stretch(touchControls); touchControls.gameObject.SetActive(false);
            var stickRect = Panel(touchControls,"Walk",new Color(.18f,.3f,.28f,.65f));
            stickRect.anchorMin = stickRect.anchorMax = stickRect.pivot = Vector2.zero; stickRect.anchoredPosition = new Vector2(26,24); stickRect.sizeDelta = new Vector2(168,168);
            var knob = Panel(stickRect,"Stick knob",new Color(.95f,.92f,.79f,.85f)); knob.anchorMin = knob.anchorMax = knob.pivot = new Vector2(.5f,.5f);
            knob.anchoredPosition = Vector2.zero; knob.sizeDelta = new Vector2(66,66);
            var stick = stickRect.gameObject.AddComponent<TouchStick>(); stick.knob = knob; stick.radius = 62; stick.Initialise(lens);
            var jump = MakeButton(touchControls,"Jump",new Vector2(-26,24),new Vector2(138,90),() => TouchBinding.Shared.Pulse(TouchAction.Jump));
            var jumpRect = (RectTransform)jump.transform; jumpRect.anchorMin = jumpRect.anchorMax = jumpRect.pivot = new Vector2(1,0);
            jumpRect.anchoredPosition = new Vector2(-26,24);
        }

        string RoomName => stage.House.Rooms[Mathf.Clamp(roomInput.value,0,stage.House.Rooms.Count-1)].Name;
        HomeProp Selected => history?.Current.Props.FirstOrDefault(p => p.Id == selected);
        void Refresh()
        {
            refreshing = true;
            nameInput.SetTextWithoutNotify(history.Current.Name);
            mapInput.SetValueWithoutNotify(Array.IndexOf(mapIds,map));
            var names = stage.House.Rooms.Select(r => r.Name).ToArray();
            string prior = roomInput.options.Count > roomInput.value ? roomInput.options[roomInput.value].text : null;
            roomInput.ClearOptions(); roomInput.AddOptions(names.ToList()); roomInput.SetValueWithoutNotify(Math.Max(0,Array.IndexOf(names,prior)));
            undoButton.interactable = history.CanUndo; redoButton.interactable = history.CanRedo;
            refreshing = false; RefreshSelection();
        }
        void RefreshSelection()
        {
            var prop = Selected;
            selectionLabel.text = prop == null ? "Tap a prop to select it" : prop.Word + "  /  " + prop.Skin + "\n(" + prop.X + ", " + prop.Z + ")  ·  " + prop.Yaw + "°";
            stage.Highlight(prop);
        }
        void SelectProp(int direction)
        {
            CancelPending();
            var props = history.Current.Props;
            if (props.Count == 0) { SetStatus("Place an object first."); return; }
            int index = props.FindIndex(p => p.Id == selected);
            selected = props[index < 0 ? (direction > 0 ? 0 : props.Count-1) : (index + direction + props.Count) % props.Count].Id;
            RefreshSelection();
        }
        void SetStatus(string text) { if (status) status.text = text ?? ""; if (jsonStatus) jsonStatus.text = text ?? ""; }

        void PlaceWords(string text)
        {
            var result = designer.AddWords(history.Current,text,RoomName);
            if (!result.Ok) { SetStatus(string.Join("\n",result.Errors)); return; }
            string report = result.Added.Count + " object(s) placed.";
            if (result.Rejected.Count > 0) report += "\n" + string.Join("\n",result.Rejected.Select(r => r.Word + ": " + r.Reason));
            if (result.Added.Count > 0) { selected = result.Added.Last().Id; Commit(result.Layout,report); }
            else SetStatus(report);
            CancelPending(false);
        }
        bool Commit(HomeLayout candidate, string message)
        {
            var validation = designer.Validate(candidate);
            if (!validation.Ok) { SetStatus(string.Join("\n",validation.Errors)); Refresh(); return false; }
            if (!stage.Show(validation.Layout,out var error)) { SetStatus(error); Refresh(); return false; }
            if (!history.TryApply(validation.Layout,out error)) { stage.Show(history.Current,out _); SetStatus(error); return false; }
            previewKey = null; Refresh(); SetStatus(message); return true;
        }
        void EditSelected(Action<HomeProp> edit,string message)
        {
            var candidate = history.Current; var prop = candidate.Props.FirstOrDefault(p => p.Id == selected);
            if (prop == null) { SetStatus("Select a prop first."); return; }
            edit(prop); Commit(candidate,message);
        }
        void Nudge(double x,double z) => EditSelected(p => { p.X += x; p.Z += z; },"Moved on the half-metre grid.");
        void Rotate() => EditSelected(p => p.Yaw = (p.Yaw+90)%360,"Rotated 90°.");
        void ChangeSkin() => EditSelected(p => p.Skin = Skins[(Array.IndexOf(Skins,p.Skin)+1)%Skins.Length],"Finish changed.");
        void Remove()
        {
            if (Selected == null) { SetStatus("Select a prop first."); return; }
            var candidate = history.Current; candidate.Props.RemoveAll(p => p.Id == selected);
            if (Commit(candidate,"Object removed.")) { selected = null; CancelPending(); RefreshSelection(); }
        }
        void HistoryStep(bool redo)
        {
            if (!(redo ? history.Redo() : history.Undo())) return;
            if (!stage.Show(history.Current,out var error)) { if (redo) history.Undo(); else history.Redo(); SetStatus(error); return; }
            CancelPending(); Refresh(); SetStatus(redo ? "Edit restored." : "Edit undone.");
        }
        void CancelPending(bool announce = false)
        { placing = null; moving = false; preview = null; previewKey = null; stage?.ClearGhost(); if (announce) SetStatus("Placement cancelled."); }
        void Save() { if (storage.TrySave(history.Current,out var error)) SetStatus("Saved this house on this device."); else SetStatus(error); }

        void BeginTour()
        {
            CancelPending();
            if (!storage.TrySave(history.Current,out var error) || !storage.TryLoad(map,out var saved,out error))
            { SetStatus(error ?? "The saved home could not be read back."); return; }
            if (!stage.Show(saved,out error)) { SetStatus(error); return; }
            try
            {
                tour = new GameObject("Peaceful home tour").AddComponent<HomeTourDirector>(); tour.transform.SetParent(transform,false);
                TouchBinding.Shared.ReleaseAll(); TouchBinding.Shared.Enabled = true;
                TouchBinding.Shared.OverlayDesktop = true; TouchBinding.Shared.OverlayBindingId = tourBinding.Id;
                tour.Begin(stage.House,tourBinding,lens); stage.SetTallWalls(tour.ThirdPerson);
                header.gameObject.SetActive(false); dock.gameObject.SetActive(false); tourBar.gameObject.SetActive(true);
                touchControls.gameObject.SetActive(Application.isMobilePlatform || Touchscreen.current != null || tourBinding is TouchBinding);
                tourTitle.text = "YOUR SAVED HOME\nMove freely · Back or START returns";
                LayoutUi();
                EventSystem.current?.SetSelectedGameObject(null);
            }
            catch (Exception ex) { EndTour(); SetStatus("Could not start tour: " + ex.Message); }
        }
        void EndTour()
        {
            if (tour) { tour.gameObject.SetActive(false); Destroy(tour.gameObject); tour = null; }
            if (stage) stage.SetTallWalls(false);
            TouchBinding.Shared.ReleaseAll(); RestoreTouch();
            touchControls.gameObject.SetActive(false); tourBar.gameObject.SetActive(false); header.gameObject.SetActive(true); dock.gameObject.SetActive(true);
            if (!stage.Show(history.Current,out var error)) SetStatus(error);
            LayoutUi(); Refresh(); SelectDesignControl();
        }

        void OpenJson(bool importing)
        {
            CancelPending();
            if (jsonSheet) Destroy(jsonSheet.gameObject);
            jsonSheet = Panel(safe,importing ? "Import home" : "Export home",Cream);
            jsonSheet.anchorMin = new Vector2(.03f,.06f); jsonSheet.anchorMax = new Vector2(.97f,.94f); jsonSheet.offsetMin = jsonSheet.offsetMax = Vector2.zero;
            jsonTitle = Label(jsonSheet,importing ? "IMPORT HOME JSON" : "EXPORT HOME JSON",new Vector2(18,-16),new Vector2(720,38),24,Ink);
            jsonInput = Input(jsonSheet,"Paste schema-1 home JSON here",new Vector2(18,-64),new Vector2(900,330),HomeDesigner.MaxJsonLength,true);
            var inputRect = (RectTransform)jsonInput.transform; inputRect.anchorMin = new Vector2(0,.22f); inputRect.anchorMax = new Vector2(1,1); inputRect.offsetMin = new Vector2(18,0); inputRect.offsetMax = new Vector2(-18,-64);
            jsonInput.pointSize = 18;
            jsonStatus = Label(jsonSheet,"",new Vector2(18,76),new Vector2(800,68),18,Ink);
            jsonStatus.rectTransform.anchorMin = new Vector2(0,0); jsonStatus.rectTransform.anchorMax = new Vector2(1,0);
            jsonStatus.rectTransform.pivot = new Vector2(0,0); jsonStatus.rectTransform.anchoredPosition = new Vector2(18,76);
            jsonStatus.rectTransform.sizeDelta = new Vector2(-36,68); jsonStatus.textWrappingMode = TextWrappingModes.Normal;
            if (!importing)
            {
                var export = designer.Export(history.Current);
                jsonInput.SetTextWithoutNotify(export.Json ?? ""); jsonInput.readOnly = true;
            }
            var copy = MakeButton(jsonSheet,importing ? "Paste clipboard" : "Copy JSON",new Vector2(18,14),new Vector2(210,48),() =>
            {
                try { if (importing) jsonInput.text = GUIUtility.systemCopyBuffer; else GUIUtility.systemCopyBuffer = jsonInput.text; }
                catch (Exception ex) { SetStatus("Clipboard unavailable: " + ex.Message); }
            }); Bottom(copy);
            jsonCopyButton = copy;
            var apply = MakeButton(jsonSheet,importing ? "Validate & import" : "Save JSON file",new Vector2(242,14),new Vector2(240,48),() =>
            {
                if (!importing) { SetStatus(storage.TryExportFile(history.Current,out var path,out var error) ? "Exported to " + path : error); return; }
                var result = designer.Import(jsonInput.text);
                if (!result.Ok) { SetStatus(string.Join("\n",result.Errors)); return; }
                if (!stage.Show(result.Layout,out var renderError)) { SetStatus(renderError); return; }
                if (!drafts.TryGetValue(result.Layout.Map,out var target)) target = new HomeHistory(designer,designer.CreateLayout(result.Layout.Map));
                if (!target.TryApply(result.Layout,out var historyError)) { stage.Show(history.Current,out _); SetStatus(historyError); return; }
                drafts[result.Layout.Map] = target; map = result.Layout.Map; history = target; selected = null;
                Refresh(); LayoutUi(); CloseJson(); SetStatus("Imported validated draft. Save when ready.");
            }); Bottom(apply);
            jsonApplyButton = apply;
            var cancel = MakeButton(jsonSheet,"Close",new Vector2(-18,14),new Vector2(150,48),CloseJson);
            var cancelRect = (RectTransform)cancel.transform; cancelRect.anchorMin = cancelRect.anchorMax = cancelRect.pivot = new Vector2(1,0); cancelRect.anchoredPosition = new Vector2(-18,14);
            jsonCloseButton = cancel; LayoutUi();
            EventSystem.current?.SetSelectedGameObject(importing ? jsonInput.gameObject : copy.gameObject);
        }
        static void Bottom(Button button) { var r = (RectTransform)button.transform; r.anchorMin = r.anchorMax = r.pivot = Vector2.zero; }
        void CloseJson() { if (jsonSheet) Destroy(jsonSheet.gameObject); jsonSheet = null; jsonStatus = jsonTitle = null; SelectDesignControl(); }

        void SelectDesignControl()
        {
            var button = dock.GetComponentsInChildren<Button>().FirstOrDefault(b => b.interactable);
            EventSystem.current?.SetSelectedGameObject(button ? button.gameObject : header.GetComponentInChildren<Button>().gameObject);
        }

        void Update()
        {
            if (!captured || restored || history == null) return;
            LayoutUi();
            var currentSelection=EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            if (currentSelection != lastMenuSelection)
            {
                lastMenuSelection=currentSelection;
                if (currentSelection && currentSelection.transform.IsChildOf(toolContent)) RevealSelected(toolScroll,(RectTransform)currentSelection.transform);
            }
            var keyboard = Keyboard.current;
            var pad = Gamepad.current;
            bool escape = keyboard?.escapeKey.wasPressedThisFrame == true || pad?.buttonEast.wasPressedThisFrame == true;
            if (escape)
            {
                if (jsonSheet) CloseJson(); else if (tour) EndTour(); else if (moving || placing != null) CancelPending(true); else Close();
                return;
            }
            if (tour) { if (pad?.startButton.wasPressedThisFrame == true) EndTour(); return; }
            if (jsonSheet || nameInput.isFocused || wordsInput.isFocused) return;
            if (pad != null)
            {
                if (pad.startButton.wasPressedThisFrame) { BeginTour(); return; }
                if (pad.rightShoulder.wasPressedThisFrame) Rotate();
                if (pad.leftShoulder.wasPressedThisFrame) ChangeSkin();
                if (moving)
                {
                    EventSystem.current?.SetSelectedGameObject(null);
                    if (pad.dpad.left.wasPressedThisFrame) Nudge(-.5,0);
                    if (pad.dpad.right.wasPressedThisFrame) Nudge(.5,0);
                    if (pad.dpad.up.wasPressedThisFrame) Nudge(0,.5);
                    if (pad.dpad.down.wasPressedThisFrame) Nudge(0,-.5);
                    if (pad.buttonSouth.wasPressedThisFrame) { moving = false; CancelPending(); StartCoroutine(RestoreMenuSelection()); }
                }
            }
            bool pressed = false; Vector2 pointer = default; bool hasPointer = false;
            var touch = Touchscreen.current?.primaryTouch;
            if (touch != null && touch.press.isPressed) { pointer = touch.position.ReadValue(); pressed = touch.press.wasPressedThisFrame; hasPointer = true; }
            else if (Mouse.current != null) { pointer = Mouse.current.position.ReadValue(); pressed = Mouse.current.leftButton.wasPressedThisFrame; hasPointer = true; }
            if (!hasPointer || OverUi(pointer) || !lens.pixelRect.Contains(pointer)) { stage.ClearGhost(); previewKey = null; return; }
            if (moving || placing != null)
            {
                var ray = lens.ScreenPointToRay(pointer);
                var room = stage.House.Room(RoomName);
                if (!new Plane(Vector3.up,new Vector3(0,room.FloorY,0)).Raycast(ray,out float distance)) return;
                var point = ray.GetPoint(distance); var current = history.Current;
                preview = moving ? Selected?.Clone() : new HomeProp { Id = FreeId(current), Word = placing };
                if (preview == null) { CancelPending(); return; }
                preview.X = Math.Round(point.x*2,MidpointRounding.AwayFromZero)/2; preview.Z = Math.Round(point.z*2,MidpointRounding.AwayFromZero)/2;
                var check = designer.PlacementCheck(current,preview,moving ? preview.Id : null);
                bool valid = check.Ok && room.Contains((float)preview.X,(float)preview.Z);
                string key = preview.Id+"|"+preview.Word+"|"+preview.X+"|"+preview.Z+"|"+preview.Yaw+"|"+valid;
                if (key != previewKey) { stage.Preview(preview,valid); previewKey = key; }
                if (pressed)
                {
                    if (!valid) { SetStatus(check.Ok ? "Place inside the selected room." : string.Join("\n",check.Errors)); return; }
                    if (moving) current.Props[current.Props.FindIndex(p => p.Id == preview.Id)] = preview.Clone();
                    else current.Props.Add(preview.Clone());
                    selected = preview.Id;
                    if (Commit(current,moving ? "Object moved." : "Object placed.")) CancelPending();
                }
            }
            else if (pressed)
            {
                selected = Physics.RaycastAll(lens.ScreenPointToRay(pointer),200f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                    .OrderBy(hit => hit.distance).Select(hit => hit.collider.GetComponentInParent<HomePropView>()).FirstOrDefault(view => view)?.Id;
                RefreshSelection();
            }
        }
        static string FreeId(HomeLayout layout) { int n=1; while (layout.Props.Any(p => p.Id == "p"+n)) n++; return "p"+n; }
        System.Collections.IEnumerator RestoreMenuSelection() { yield return null; if (!restored && !tour && !jsonSheet) SelectDesignControl(); }
        void LateUpdate()
        {
            if (!captured || restored) return;
            foreach (var pause in pausedBodies) pause.Freeze();
            foreach (var entry in hiddenRenderers) if (entry.component) entry.component.enabled = false;
            foreach (var entry in hiddenColliders) if (entry.component) entry.component.enabled = false;
            foreach (var entry in hiddenCanvases) if (entry.component) entry.component.enabled = false;
        }
        bool OverUi(Vector2 pointer)
        {
            if (!EventSystem.current) return false;
            uiHits.Clear(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pointer },uiHits);
            return uiHits.Any(hit => hit.gameObject.transform.IsChildOf(canvas.transform));
        }

        void LayoutUi()
        {
            if (!safe) return;
            var area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin/Mathf.Max(1,Screen.width),area.yMin/Mathf.Max(1,Screen.height));
            safe.anchorMax = new Vector2(area.xMax/Mathf.Max(1,Screen.width),area.yMax/Mathf.Max(1,Screen.height));
            portrait = safe.rect.width < safe.rect.height;
            float unit = Mathf.Max(1f,Screen.dpi/160f) / Mathf.Max(.1f,canvas.scaleFactor);
            float target = Mathf.Max(48f,48f * unit);
            float headerHeight;
            header.anchorMin = header.anchorMax = header.pivot = new Vector2(0,1); header.anchoredPosition = Vector2.zero; header.sizeDelta = new Vector2(1024,70);
            header.localScale = Vector3.one;
            tourBar.anchorMin = tourBar.anchorMax = tourBar.pivot = new Vector2(0,1); tourBar.anchoredPosition = Vector2.zero; tourBar.localScale = Vector3.one;
            if (portrait)
            {
                float width = safe.rect.width, row = (width-48)/3;
                float titleHeight=44*unit, nameTop=56*unit, actionsTop=nameTop+target+12*unit;
                headerHeight = actionsTop+target+12*unit;
                header.sizeDelta = new Vector2(width,headerHeight);
                Position(headerTitle.rectTransform,new Vector2(12,-6*unit),new Vector2(width-24,titleHeight));
                Position((RectTransform)nameInput.transform,new Vector2(12,-nameTop),new Vector2(width-24,target));
                Position((RectTransform)saveButton.transform,new Vector2(12,-actionsTop),new Vector2(row,target));
                Position((RectTransform)tourButton.transform,new Vector2(24+row,-actionsTop),new Vector2(row,target));
                Position((RectTransform)backButton.transform,new Vector2(36+row*2,-actionsTop),new Vector2(row,target));
                tourBar.sizeDelta = new Vector2(width,nameTop+target+12*unit);
                Position(tourTitle.rectTransform,new Vector2(12,-6*unit),new Vector2(width-24,titleHeight));
                Position((RectTransform)tourBackButton.transform,new Vector2(12,-nameTop),new Vector2((width-36)*.7f,target));
                Position((RectTransform)tourExitButton.transform,new Vector2(24+(width-36)*.7f,-nameTop),new Vector2((width-36)*.3f,target));
                dock.anchorMin = Vector2.zero; dock.anchorMax = new Vector2(1,.44f); dock.pivot = Vector2.zero; dock.offsetMin = dock.offsetMax = Vector2.zero;
            }
            else
            {
                headerHeight = Mathf.Max(70,target+24);
                float fit = Mathf.Min(1f,safe.rect.width/1024f); header.localScale = Vector3.one*fit;
                header.sizeDelta = new Vector2(1024,headerHeight/fit);
                Position(headerTitle.rectTransform,new Vector2(16,-12),new Vector2(280,44));
                Position((RectTransform)nameInput.transform,new Vector2(306,-12),new Vector2(350,target/fit));
                Position((RectTransform)saveButton.transform,new Vector2(670,-12),new Vector2(105,target/fit));
                Position((RectTransform)tourButton.transform,new Vector2(785,-12),new Vector2(105,target/fit));
                Position((RectTransform)backButton.transform,new Vector2(900,-12),new Vector2(105,target/fit));
                tourBar.sizeDelta = new Vector2(1010,headerHeight); tourBar.localScale = Vector3.one*fit;
                Position(tourTitle.rectTransform,new Vector2(12,-12),new Vector2(580,40));
                Position((RectTransform)tourBackButton.transform,new Vector2(610,-10),new Vector2(260,target/fit));
                Position((RectTransform)tourExitButton.transform,new Vector2(880,-10),new Vector2(110,target/fit));
                dock.anchorMin = Vector2.zero; dock.anchorMax = new Vector2(0,1); dock.pivot = Vector2.zero;
                dock.offsetMin = Vector2.zero; dock.offsetMax = new Vector2(348,-headerHeight);
            }
            foreach (var button in new[] {saveButton,tourButton,backButton,tourBackButton,tourExitButton}) StretchButtonText(button);
            headerTitle.fontSize = portrait ? 26*unit : 26; tourTitle.fontSize = portrait ? 20*unit : 22;
            nameInput.pointSize = portrait ? 20*unit : 22;
            foreach (var button in new[] {saveButton,tourButton,backButton,tourBackButton,tourExitButton})
                button.GetComponentInChildren<TMP_Text>().fontSize = portrait ? 20*unit : 21;
            LayoutTools(portrait ? unit : 1f,portrait ? safe.rect.width : 348f);
            if (jsonSheet)
            {
                float width = (jsonSheet.rect.width-48)/3;
                var buttons = new[] {jsonCopyButton,jsonApplyButton,jsonCloseButton};
                for (int i=0;i<buttons.Length;i++)
                {
                    var r = (RectTransform)buttons[i].transform; r.anchorMin = r.anchorMax = r.pivot = Vector2.zero;
                    r.anchoredPosition = new Vector2(12+(width+12)*i,12*unit); r.sizeDelta = new Vector2(width,target); StretchButtonText(buttons[i]);
                    buttons[i].GetComponentInChildren<TMP_Text>().fontSize = portrait ? 18*unit : 21;
                }
                jsonInput.pointSize = portrait ? 16*unit : 18; jsonStatus.fontSize = portrait ? 16*unit : 18;
                jsonStatus.rectTransform.anchoredPosition = new Vector2(18,target+26*unit);
                jsonStatus.rectTransform.sizeDelta = new Vector2(-36,68*unit);
                jsonTitle.fontSize=portrait ? 24*unit : 24;
                Position(jsonTitle.rectTransform,new Vector2(18,-12*unit),new Vector2(jsonSheet.rect.width-36,40*unit));
                var inputRect=(RectTransform)jsonInput.transform; inputRect.anchorMin=Vector2.zero; inputRect.anchorMax=Vector2.one;
                inputRect.offsetMin=new Vector2(18,target+106*unit); inputRect.offsetMax=new Vector2(-18,-64*unit);
            }
            if (tour) { lens.rect = new Rect(0,0,1,1); return; }
            RectTransformUtility.ScreenPointToWorldPointInRectangle(safe,Vector2.zero,null,out _);
            var corners = new Vector3[4]; dock.GetWorldCorners(corners);
            float left = portrait ? area.xMin : Mathf.Max(area.xMin,corners[2].x);
            float bottom = portrait ? Mathf.Max(area.yMin,corners[2].y) : area.yMin;
            float top = area.yMax - headerHeight*canvas.scaleFactor;
            var rect = new Rect(left/Screen.width,bottom/Screen.height,Mathf.Max(.05f,(area.xMax-left)/Screen.width),Mathf.Max(.05f,(top-bottom)/Screen.height));
            if (lens.rect != rect) { lens.rect = rect; stage.Frame(lens); }
            else stage.Frame(lens);
        }

        void LayoutTools(float unit,float width)
        {
            if (!toolContent) return;
            float xFactor = Mathf.Max(.1f,(width-24)/320), y=12*unit;
            var groups = toolRows.GroupBy(row => Mathf.RoundToInt(row.at.y)).OrderByDescending(group => group.Key).ToArray();
            for (int i=0;i<groups.Length;i++)
            {
                float height=0;
                foreach (var row in groups[i])
                {
                    bool control = row.rect.GetComponent<Selectable>();
                    float h = Mathf.Max(control ? 48 : 0,row.size.y)*unit;
                    if (row.rect == status.rectTransform) h=Mathf.Max(h,status.preferredHeight+12*unit);
                    Position(row.rect,new Vector2(12+(row.at.x-12)*xFactor,-y),new Vector2(row.size.x*xFactor,h));
                    height=Mathf.Max(height,h);
                    var button=row.rect.GetComponent<Button>(); if (button) StretchButtonText(button);
                    var dropdown=row.rect.GetComponent<TMP_Dropdown>();
                    if (dropdown)
                    {
                        Stretch(dropdown.captionText.rectTransform); dropdown.captionText.rectTransform.offsetMin=new Vector2(10,4); dropdown.captionText.rectTransform.offsetMax=new Vector2(-36*unit,-4);
                        var arrow=row.rect.Find("▾") as RectTransform;
                        if (arrow) { arrow.anchorMin=arrow.anchorMax=arrow.pivot=Vector2.one; arrow.anchoredPosition=new Vector2(-8,-4); arrow.sizeDelta=new Vector2(24*unit,44*unit); }
                        dropdown.template.sizeDelta=new Vector2(0,220*unit);
                        var option=dropdown.template.GetComponentInChildren<Toggle>(true); var item=(RectTransform)option.transform;
                        item.sizeDelta=new Vector2(0,48*unit);
                        var optionsScroll=dropdown.template.GetComponent<ScrollRect>(); optionsScroll.content.sizeDelta=new Vector2(0,48*unit);
                        Stretch(dropdown.itemText.rectTransform); dropdown.itemText.rectTransform.offsetMin=new Vector2(30*unit,4); dropdown.itemText.rectTransform.offsetMax=new Vector2(-6,-4);
                    }
                }
                float sourceHeight=groups[i].Max(row => row.size.y);
                float gap=i+1<groups.Length ? groups[i].Key-groups[i+1].Key-sourceHeight : 12;
                y+=height+Mathf.Max(8,gap)*unit;
            }
            toolContent.sizeDelta=new Vector2(0,y);
            foreach (var text in toolText) text.text.fontSize=text.size*unit;
            wordsInput.pointSize=22*unit;
        }
        static void RevealSelected(ScrollRect scroll,RectTransform selected)
        {
            var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,selected);
            var position=scroll.content.anchoredPosition;
            if (bounds.min.y<scroll.viewport.rect.yMin) position.y+=scroll.viewport.rect.yMin-bounds.min.y;
            else if (bounds.max.y>scroll.viewport.rect.yMax) position.y-=bounds.max.y-scroll.viewport.rect.yMax;
            position.y=Mathf.Clamp(position.y,0,Mathf.Max(0,scroll.content.rect.height-scroll.viewport.rect.height));
            scroll.content.anchoredPosition=position;
        }

        void RestoreTouch()
        { TouchBinding.Shared.Enabled = touchEnabled; TouchBinding.Shared.OverlayDesktop = touchOverlay; TouchBinding.Shared.OverlayBindingId = touchId; }
        public void Close()
        {
            RestoreScene();
            if (gameObject) { gameObject.SetActive(false); Destroy(gameObject); }
        }
        void RestoreScene()
        {
            if (restored) return; restored = true;
            if (Instance == this) Instance = null;
            if (!captured) return;
            if (tour) tour.gameObject.SetActive(false);
            TouchBinding.Shared.ReleaseAll(); RestoreTouch();
            if (lens)
            {
                lens.transform.SetPositionAndRotation(cameraPosition,cameraRotation); lens.rect = cameraRect;
                lens.orthographic = cameraOrtho; lens.orthographicSize = cameraSize; lens.fieldOfView = cameraFov;
            }
            foreach (var entry in suspended) if (entry.component) entry.component.enabled = entry.enabled;
            foreach (var entry in hiddenRenderers) if (entry.component) entry.component.enabled = entry.enabled;
            foreach (var entry in hiddenColliders) if (entry.component) entry.component.enabled = entry.enabled;
            foreach (var entry in hiddenCanvases) if (entry.component) entry.component.enabled = entry.enabled;
            foreach (var pause in pausedBodies) pause.Restore();
            foreach (var entry in roots) if (entry.root) entry.root.SetActive(entry.active);
            Time.timeScale = oldTimeScale;
            if (EventSystem.current && priorSelection && priorSelection.activeInHierarchy) EventSystem.current.SetSelectedGameObject(priorSelection);
        }
        void OnDestroy() => RestoreScene();

        sealed class BodyPause
        {
            readonly Rigidbody body;
            readonly bool kinematic, gravity, collisions;
            readonly Vector3 velocity, angular;
            readonly PlayerController player;
            readonly LifeState? state;
            public BodyPause(Rigidbody body)
            {
                this.body = body; kinematic = body.isKinematic; gravity = body.useGravity; collisions = body.detectCollisions;
                velocity = body.linearVelocity; angular = body.angularVelocity; player = body.GetComponent<PlayerController>();
                state = player && player.Health ? player.Health.State : null;
            }
            public void Freeze() { if (body) { body.isKinematic = true; body.detectCollisions = false; } }
            public void Restore()
            {
                if (!body) return;
                bool respawned = player && player.Health && state.HasValue && player.Health.State != state.Value;
                body.isKinematic = respawned ? player.IsHeld : kinematic;
                body.useGravity = gravity; body.detectCollisions = collisions;
                if (!body.isKinematic) { body.linearVelocity = respawned ? Vector3.zero : velocity; body.angularVelocity = respawned ? Vector3.zero : angular; }
            }
        }

        static RectTransform Rect(Transform parent,string name,Vector2 at,Vector2 size)
        {
            var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,1); rect.anchoredPosition = at; rect.sizeDelta = size; return rect;
        }
        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        static void Position(RectTransform r,Vector2 at,Vector2 size)
        { r.anchorMin = r.anchorMax = r.pivot = new Vector2(0,1); r.anchoredPosition = at; r.sizeDelta = size; }
        static void StretchButtonText(Button button)
        { var text = button.GetComponentInChildren<TextMeshProUGUI>(); Stretch(text.rectTransform); text.rectTransform.offsetMin = new Vector2(8,4); text.rectTransform.offsetMax = new Vector2(-8,-4); }
        static RectTransform Panel(Transform parent,string name,Color colour)
        { var rect = Rect(parent,name,Vector2.zero,Vector2.zero); rect.gameObject.AddComponent<Image>().color = colour; return rect; }
        static TextMeshProUGUI Label(Transform parent,string text,Vector2 at,Vector2 size,float fontSize,Color colour)
        {
            var rect = Rect(parent,text,at,size); var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = GameAssets.I.font; label.text = text; label.fontSize = fontSize; label.color = colour;
            label.alignment = TextAlignmentOptions.MidlineLeft; label.raycastTarget = false; return label;
        }
        static Button MakeButton(Transform parent,string title,Vector2 at,Vector2 size,Action action)
        {
            var rect = Panel(parent,title,new Color(.84f,.89f,.82f)); rect.anchoredPosition = at; rect.sizeDelta = size;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(() => action());
            var label = Label(rect,title,new Vector2(8,-4),size-new Vector2(16,8),21,Ink); label.alignment = TextAlignmentOptions.Center;
            return button;
        }
        static TMP_InputField Input(Transform parent,string hint,Vector2 at,Vector2 size,int max,bool multiline)
        {
            var rect = Panel(parent,hint,Color.white); rect.anchoredPosition = at; rect.sizeDelta = size;
            var field = rect.gameObject.AddComponent<TMP_InputField>(); field.targetGraphic = rect.GetComponent<Image>();
            var viewport = Rect(rect,"Text area",Vector2.zero,Vector2.zero); Stretch(viewport); viewport.offsetMin = new Vector2(10,6); viewport.offsetMax = new Vector2(-10,-6);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = Label(viewport,"",Vector2.zero,Vector2.zero,22,Ink); Stretch(text.rectTransform); text.alignment = multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;
            var placeholder = Label(viewport,hint,Vector2.zero,Vector2.zero,22,new Color(.4f,.46f,.43f)); Stretch(placeholder.rectTransform);
            placeholder.alignment = text.alignment; field.textViewport = viewport; field.textComponent = text; field.placeholder = placeholder;
            field.fontAsset = GameAssets.I.font; field.pointSize = 22; field.characterLimit = max;
            field.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
            return field;
        }
        static TMP_Dropdown Dropdown(Transform parent,string[] options,Vector2 at,Vector2 size)
        {
            var rect = Panel(parent,"Choose",Color.white); rect.anchoredPosition = at; rect.sizeDelta = size;
            var dropdown = rect.gameObject.AddComponent<TMP_Dropdown>(); dropdown.targetGraphic = rect.GetComponent<Image>();
            dropdown.captionText = Label(rect,"",new Vector2(10,-4),size-new Vector2(40,8),22,Ink);
            Label(rect,"▾",new Vector2(size.x-30,-4),new Vector2(24,size.y-8),22,Teal);
            var template = Panel(rect,"Options",Cream); template.anchorMin = new Vector2(0,0); template.anchorMax = new Vector2(1,0); template.pivot = new Vector2(.5f,1);
            template.anchoredPosition = Vector2.zero; template.sizeDelta = new Vector2(0,220);
            var scroll = template.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Panel(template,"Viewport",Color.white); Stretch(viewport); viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var content = Rect(viewport,"Content",Vector2.zero,new Vector2(0,44)); content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f,1);
            var item = Panel(content,"Item",Color.white); item.anchorMin = new Vector2(0,.5f); item.anchorMax = new Vector2(1,.5f); item.pivot = new Vector2(.5f,.5f); item.sizeDelta = new Vector2(0,44);
            var toggle = item.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = item.GetComponent<Image>();
            var check = Panel(item,"Checkmark",Teal); check.anchoredPosition = new Vector2(5,-12); check.sizeDelta = new Vector2(18,18); toggle.graphic = check.GetComponent<Image>();
            dropdown.itemText = Label(item,"Option",new Vector2(30,-4),size-new Vector2(36,8),22,Ink);
            scroll.viewport = viewport; scroll.content = content; dropdown.template = template; template.gameObject.SetActive(false);
            dropdown.AddOptions(options.ToList()); return dropdown;
        }
    }
}
