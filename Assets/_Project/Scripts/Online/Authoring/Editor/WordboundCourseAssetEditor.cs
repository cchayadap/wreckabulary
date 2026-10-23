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
        WordboundCourseDraftStore.PendingDraft conflictingDraft;
        ResolvedCourse preview;
        string error = "", status = "", lastSaved = "", previewInput = "";
        string recoveryWarning = "", lastStorageError = "";
        int section, module;
        bool dirty, settings, handles = true, recoveryFailed;
        double saveDraftAt = double.PositiveInfinity;
        WordboundCourseAsset Asset => (WordboundCourseAsset)target;
        void OnEnable()
        {
            Reload(); Undo.undoRedoPerformed += ReloadAfterUndo; SceneView.duringSceneGui += SceneGUI;
            EditorApplication.update += SaveDraftWhenDue; EditorApplication.quitting += SavePendingDraft;
            AssemblyReloadEvents.beforeAssemblyReload += SavePendingDraft;
        }
        void OnDisable()
        {
            SavePendingDraft();
            Undo.undoRedoPerformed -= ReloadAfterUndo; SceneView.duringSceneGui -= SceneGUI;
            EditorApplication.update -= SaveDraftWhenDue; EditorApplication.quitting -= SavePendingDraft;
            AssemblyReloadEvents.beforeAssemblyReload -= SavePendingDraft;
        }
        void Reload()
        {
            try
            {
                draft = Asset.HasDocument ? Asset.ReadDocument() : null; lastSaved = Asset.DocumentJson;
                dirty = false; preview = null; error = ""; recoveryWarning = ""; lastStorageError = ""; conflictingDraft = null; recoveryFailed = false;
                saveDraftAt = double.PositiveInfinity;
                if (AssetDatabase.Contains(Asset))
                {
                    var pending = WordboundCourseDraftStore.Load(Asset);
                    if (pending != null)
                    {
                        if (pending.SavedSourceHash == CourseJson.Sha256(lastSaved))
                        { draft = pending.Document; dirty = true; status = "Recovered your unapplied form edits. Apply to save them to the course."; }
                        else conflictingDraft = pending;
                        if (WordboundCourseDraftStore.IsMemoryOnly(Asset))
                        {
                            recoveryWarning = "Recovery is in memory only. Resolve the file error before reloading scripts or closing Unity. Saving will retry automatically.";
                            saveDraftAt = EditorApplication.timeSinceStartup + 5;
                        }
                    }
                }
            }
            catch (Exception e) { recoveryFailed = true; error = "Pending form recovery: " + e.Message; }
            Repaint();
        }
        void ReloadAfterUndo()
        {
            if (lastSaved != Asset.DocumentJson) ReloadPreservingPending();
            else if (!dirty && conflictingDraft == null) Reload();
            Repaint();
        }
        void ReloadPreservingPending()
        {
            if (dirty && draft != null)
            {
                TrySavePendingDraft();
                conflictingDraft = new WordboundCourseDraftStore.PendingDraft {
                    Document = draft, SavedSourceHash = CourseJson.Sha256(lastSaved)
                };
                dirty = false;
            }
            if (conflictingDraft != null)
            {
                draft = Asset.HasDocument ? Asset.ReadDocument() : null;
                lastSaved = Asset.DocumentJson; preview = null;
            }
            else Reload();
        }
        void MarkDirty() { dirty = true; preview = null; saveDraftAt = EditorApplication.timeSinceStartup + .5; }
        void SaveDraftWhenDue()
        {
            if (EditorApplication.timeSinceStartup >= saveDraftAt) SavePendingDraft();
        }
        void SavePendingDraft() => TrySavePendingDraft();
        bool TrySavePendingDraft()
        {
            if (!target || recoveryFailed) return false;
            if ((!dirty && conflictingDraft == null) || !AssetDatabase.Contains(Asset)) return true;
            try
            {
                WordboundCourseDraftStore.Save(Asset, conflictingDraft?.SavedSourceHash ?? CourseJson.Sha256(lastSaved),
                    conflictingDraft?.Document ?? draft);
                saveDraftAt = double.PositiveInfinity;
                recoveryWarning = "";
                if (error == lastStorageError) error = "";
                lastStorageError = "";
                return true;
            }
            catch (Exception e)
            {
                if (error != e.Message) Debug.LogWarning("Wordbound pending form could not be saved: " + e.Message, target);
                error = lastStorageError = e.Message; saveDraftAt = EditorApplication.timeSinceStartup + 5;
                recoveryWarning = "Recovery is in memory only. Resolve the file error before reloading scripts or closing Unity. Saving will retry automatically.";
                return false;
            }
        }
        bool DrawRecoveryChoice()
        {
            if (conflictingDraft == null && !recoveryFailed) return false;
            EditorGUILayout.HelpBox(recoveryFailed ? error + " The recovery file has been preserved." :
                "This course was saved or changed elsewhere while form edits were pending. Choose which version to edit.", MessageType.Warning);
            if (conflictingDraft != null && GUILayout.Button("Restore pending edits into the form")) RestorePending();
            if (GUILayout.Button("Discard pending form and use saved course")) Run(() => {
                WordboundCourseDraftStore.Clear(Asset); Reload();
            });
            return true;
        }
        void RestorePending()
        {
            if (conflictingDraft == null) return;
            draft = conflictingDraft.Document; conflictingDraft = null; lastSaved = Asset.DocumentJson;
            MarkDirty(); error = "";
            status = "Pending edits restored. Review them, then Apply to replace the saved course.";
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            if (lastSaved != Asset.DocumentJson) ReloadPreservingPending();
            if (recoveryWarning != "") EditorGUILayout.HelpBox(recoveryWarning, MessageType.Warning);
            if (DrawRecoveryChoice()) return;
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
                if (EditorGUI.EndChangeCheck()) { MarkDirty(); SceneView.RepaintAll(); }
                DrawModuleTools();
                using (new EditorGUI.DisabledScope(!dirty))
                {
                    if (GUILayout.Button("Apply form edits and save asset")) Run(Save);
                    if (GUILayout.Button("Discard unapplied form edits")) Run(() => { WordboundCourseDraftStore.Clear(Asset); Reload(); });
                }
                if (dirty) EditorGUILayout.HelpBox("Form changes are pending and kept locally for recovery. Apply to update the saved course. Invalid edits do not replace saved data.", MessageType.Info);
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
            using (new EditorGUI.DisabledScope(dirty))
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
            if (next >= 0 && next != currentLayer) Run(() => { draft = WordboundCourseEditing.ChangeLayer(draft, current.Id, draft.Layers[next].Id); MarkDirty(); });
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Rotate module 90°")) Run(() => { draft = WordboundCourseEditing.RotateModule(draft, current.Id, 1); MarkDirty(); });
            if (GUILayout.Button("Frame module")) SceneView.lastActiveSceneView?.Frame(new Bounds(ToVector(current.Position), Vector3.one * 24), false);
            EditorGUILayout.EndHorizontal();
        }
        void SceneGUI(SceneView view)
        {
            if (!target || !handles || recoveryFailed || conflictingDraft != null || draft == null || draft.Modules.Count == 0 || EditorApplication.isPlaying) return;
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
                draft = WordboundCourseEditing.MoveModule(draft, selected.Id, moved); MarkDirty();
                Repaint();
            });
        }
        void Initialize(CourseDocument document, CourseCatalogue catalogue, string mode)
        {
            Undo.RecordObject(Asset, "Initialize Wordbound course"); Asset.Initialize(document, catalogue); Asset.ModeId = mode; Persist(); Reload();
        }
        void Save()
        {
            if (recoveryFailed || conflictingDraft != null)
                throw new InvalidOperationException("Resolve the pending form recovery choice before applying.");
            if (lastSaved != Asset.DocumentJson)
            {
                ReloadPreservingPending();
                throw new InvalidOperationException("The saved course changed. Review the pending form recovery choice before applying.");
            }
            if (!TrySavePendingDraft()) throw new InvalidOperationException("Pending edits could not be backed up. Resolve the recovery-file error, then apply again.");
            string json = CourseJson.ExportDocument(draft);
            Undo.RecordObject(Asset, "Edit Wordbound course"); Asset.ImportDocument(json); Persist();
            WordboundCourseDraftStore.Clear(Asset); Reload();
        }
        void Persist() { EditorUtility.SetDirty(Asset); AssetDatabase.SaveAssetIfDirty(Asset); }
        void Run(Action action) { try { action(); error = ""; } catch (Exception e) { error = e.Message; } }
        static Vector3 ToVector(CoursePoint p) => new Vector3(p.X, p.Y, p.Z) / 1000f;
    }
}
