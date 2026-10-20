using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online.Editor
{
    [CustomEditor(typeof(WordboundCourseAsset))]
    public sealed class WordboundCourseAssetEditor : UnityEditor.Editor
    {
        readonly WordboundCourseFields fields = new WordboundCourseFields();
        static readonly string[] sections = typeof(CourseDocument).GetFields().Where(f => f.FieldType.IsGenericType)
            .Select(f => f.Name).ToArray();
        CourseDocument draft;
        ResolvedCourse preview;
        string error = "", status = "", lastSaved = "", previewInput = "";
        int section, module;
        bool dirty, settings, handles = true;
        WordboundCourseAsset Asset => (WordboundCourseAsset)target;
        void OnEnable() { Reload(); Undo.undoRedoPerformed += Reload; SceneView.duringSceneGui += SceneGUI; }
        void OnDisable()
        {
            Undo.undoRedoPerformed -= Reload; SceneView.duringSceneGui -= SceneGUI;
            if (dirty) Debug.LogWarning("Wordbound course form edits were not applied. Reopen the asset and re-enter them; saved course data was preserved.", target);
        }
        void Reload()
        {
            try { draft = Asset.HasDocument ? Asset.ReadDocument() : null; lastSaved = Asset.DocumentJson; dirty = false; preview = null; error = ""; }
            catch (Exception e) { error = e.Message; }
            Repaint();
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            if (!dirty && lastSaved != Asset.DocumentJson) Reload();
            EditorGUILayout.HelpBox("Edit a saved course, validate, then preview the exact seed. Local play uses the verified preview. Public matchmaking keeps its separately approved content catalogue.", MessageType.Info);
            settings = EditorGUILayout.Foldout(settings, "Mode, seed, gameplay assets, geometry and presentation", true);
            if (settings)
            {
                EditorGUI.BeginChangeCheck();
                DrawPropertiesExcluding(serializedObject, "m_Script", "documentJson");
                if (EditorGUI.EndChangeCheck()) { serializedObject.ApplyModifiedProperties(); preview = null; }
            }
            if (draft == null)
            {
                if (GUILayout.Button("Start from Freestyle proof")) Run(() => Initialize(CourseTemplates.FreestyleWorld(), CourseTemplates.ProofCatalogue(), "Freestyle"));
                if (GUILayout.Button("Start from full authored flagship")) Run(() => {
                    if (!Asset.Items) throw new ArgumentException("Assign the item catalogue TextAsset first.");
                    var content = WordboundFlagshipContent.Create(ItemCatalogue.FromJson(Asset.Items.text));
                    Initialize(content.CopyDocument(), content.Geometry, "TimeRun");
                });
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                draft.Id = EditorGUILayout.DelayedTextField("Course ID", draft.Id);
                draft.Title = EditorGUILayout.DelayedTextField("Title", draft.Title);
                draft.ThemeId = EditorGUILayout.DelayedTextField("Theme", draft.ThemeId);
                EditorGUILayout.LabelField("Document schema", draft.Schema.ToString());
                section = EditorGUILayout.Popup("Edit section", section, sections.Select(ObjectNames.NicifyVariableName).ToArray());
                fields.DrawSection(draft, sections[section]);
                if (draft.PhysicalSites.Count != 0) draft.Schema = 2;
                if (EditorGUI.EndChangeCheck()) { dirty = true; preview = null; SceneView.RepaintAll(); }
                DrawModuleTools();
                using (new EditorGUI.DisabledScope(!dirty))
                    if (GUILayout.Button("Apply form edits and save asset")) Run(Save);
                if (dirty) EditorGUILayout.HelpBox("Form changes are pending. Apply before selecting another asset. Invalid form changes never replace the last saved course.", MessageType.Warning);
                using (new EditorGUI.DisabledScope(dirty))
                {
                    if (GUILayout.Button("Validate and preview exact seed")) Run(() => {
                        preview = Asset.Resolve();
                        var content = Asset.CreateContent(preview);
                        previewInput = Asset.InputHash;
                        status = "Validated source " + preview.SourceHash + "\nManifest " + preview.Hash + "\nContent " + content.CatalogueHash;
                        WordboundCoursePreview.Show(Asset, preview);
                    });
                    using (new EditorGUI.DisabledScope(preview == null || EditorApplication.isPlayingOrWillChangePlaymode))
                        if (GUILayout.Button("Play exact preview locally")) Run(() => WordboundCoursePlaytest.Start(Asset, preview, previewInput));
                    if (GUILayout.Button("Export course JSON")) Run(() => {
                        string path = EditorUtility.SaveFilePanel("Export course", "", Asset.name + ".json", "json");
                        if (path != "") File.WriteAllText(path, Asset.DocumentJson);
                    });
                }
            }
            if (GUILayout.Button("Import course JSON")) Run(() => {
                string path = EditorUtility.OpenFilePanel("Import course", "", "json");
                if (path == "") return;
                if (new FileInfo(path).Length > CourseLimits.DocumentBytes) throw new ArgumentException("Course file exceeds 2 MiB.");
                string json = File.ReadAllText(path);
                var loaded = CourseJson.ImportDocument(json);
                if (!loaded.Success) throw new ArgumentException(string.Join("\n", loaded.Issues));
                Undo.RecordObject(Asset, "Import Wordbound course"); Asset.ImportDocument(json); Persist(); Reload();
            });
            if (error != "") EditorGUILayout.HelpBox(error, MessageType.Error);
            if (status != "") EditorGUILayout.HelpBox(status, MessageType.Info);
            serializedObject.ApplyModifiedProperties();
        }
        void DrawModuleTools()
        {
            if (draft.Modules.Count == 0) return;
            handles = EditorGUILayout.Toggle("Show Scene handles", handles);
            module = EditorGUILayout.Popup("Scene module", Mathf.Clamp(module, 0, draft.Modules.Count - 1), draft.Modules.Select(m => m.Id).ToArray());
            var current = draft.Modules[module];
            int currentLayer = draft.Layers.FindIndex(l => l.Id == current.LayerId);
            int next = EditorGUILayout.Popup("Move to layer", currentLayer, draft.Layers.Select(l => l.Id).ToArray());
            if (next >= 0 && next != currentLayer) Run(() => { draft = WordboundCourseEditing.ChangeLayer(draft, current.Id, draft.Layers[next].Id); dirty = true; preview = null; });
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Rotate module 90°")) Run(() => { draft = WordboundCourseEditing.RotateModule(draft, current.Id, 1); dirty = true; preview = null; });
            if (GUILayout.Button("Frame module")) SceneView.lastActiveSceneView?.Frame(new Bounds(ToVector(current.Position), Vector3.one * 24), false);
            EditorGUILayout.EndHorizontal();
        }
        void SceneGUI(SceneView view)
        {
            if (!target || !handles || draft == null || draft.Modules.Count == 0 || EditorApplication.isPlaying) return;
            CourseCatalogue catalogue;
            try { catalogue = Asset.Catalogue(); } catch { return; }
            foreach (var m in draft.Modules)
            {
                if (!catalogue.Modules.ContainsKey(m.DefinitionId)) continue;
                var b = catalogue.Bounds(m);
                Handles.color = m == draft.Modules[Mathf.Clamp(module, 0, draft.Modules.Count - 1)] ? Color.cyan : Color.gray;
                var min = ToVector(b.Min); var max = ToVector(b.Max);
                Handles.DrawWireCube((min + max) / 2, max - min); Handles.Label(ToVector(m.Position), m.Id);
            }
            foreach (var s in draft.Spawns) { Handles.color = Color.green; Handles.DrawWireDisc(ToVector(s.Position), Vector3.up, .4f); }
            foreach (var s in draft.PhysicalSites) { Handles.color = Color.yellow; Handles.DrawLine(ToVector(s.Position), ToVector(s.Target)); Handles.Label(ToVector(s.Position), s.PuzzleId); }
            var selected = draft.Modules[Mathf.Clamp(module, 0, draft.Modules.Count - 1)];
            Vector3 old = ToVector(selected.Position);
            EditorGUI.BeginChangeCheck();
            Vector3 next = Handles.PositionHandle(old, Quaternion.identity); next.y = old.y;
            if (EditorGUI.EndChangeCheck()) Run(() => {
                var moved = new CoursePoint(checked((int)Math.Round(next.x * 1000)), selected.Position.Y, checked((int)Math.Round(next.z * 1000)));
                draft = WordboundCourseEditing.MoveModule(draft, selected.Id, moved); dirty = true; preview = null;
                Repaint();
            });
        }
        void Initialize(CourseDocument document, CourseCatalogue catalogue, string mode)
        {
            Undo.RecordObject(Asset, "Initialize Wordbound course"); Asset.Initialize(document, catalogue); Asset.ModeId = mode; Persist(); Reload();
        }
        void Save() { string json = CourseJson.ExportDocument(draft); Undo.RecordObject(Asset, "Edit Wordbound course"); Asset.ImportDocument(json); Persist(); Reload(); }
        void Persist() { EditorUtility.SetDirty(Asset); AssetDatabase.SaveAssetIfDirty(Asset); }
        void Run(Action action) { try { action(); error = ""; } catch (Exception e) { error = e.Message; } }
        static Vector3 ToVector(CoursePoint p) => new Vector3(p.X, p.Y, p.Z) / 1000f;
    }
}
