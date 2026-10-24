using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wreckabulary.Online;
using Wreckabulary.Online.Editor;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class WordboundAuthoringPersistenceTests
    {
        const string SessionPrefix = "Wordbound.Authoring.Playtest.";
        string folder;
        [SetUp] public void CreateFolder()
        {
            Assert.IsFalse(EditorApplication.isPlayingOrWillChangePlaymode);
            Assert.IsFalse(SessionState.GetBool(SessionPrefix + "active", false), "Close the authoring playtest before running persistence checks.");
            folder = "Assets/__WordboundAuthoring_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
        }
        [TearDown] public void DeleteFolder()
        {
            if (folder != null && folder.StartsWith("Assets/__WordboundAuthoring_", StringComparison.Ordinal))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:WordboundCourseAsset", new[] { folder }))
                    WordboundCourseDraftStore.Clear(AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(AssetDatabase.GUIDToAssetPath(guid)));
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void SerializedThemeColoursMetricsAndOriginalFontReferencesSurviveSaveAndReload()
        {
            WordboundUiTheme theme = null;
            try
            {
                theme = ScriptableObject.CreateInstance<WordboundUiTheme>();
                string path = folder + "/CustomInterface.asset";
                AssetDatabase.CreateAsset(theme, path);
                var body = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Resources/Fonts/Nunito-Black.ttf");
                var heading = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Resources/Fonts/Nunito-ExtraBold.ttf");
                Assert.IsNotNull(body); Assert.IsNotNull(heading);
                using (var serialized = new SerializedObject(theme))
                {
                    serialized.FindProperty("BodyFont").objectReferenceValue = body;
                    serialized.FindProperty("HeadingFont").objectReferenceValue = heading;
                    serialized.FindProperty("BodySize").intValue = 17;
                    serialized.FindProperty("HeadingSize").intValue = 25;
                    serialized.FindProperty("Padding").intValue = 20;
                    serialized.FindProperty("Gap").intValue = 8;
                    serialized.FindProperty("RowHeight").intValue = 40;
                    serialized.FindProperty("ModuleHeight").intValue = 70;
                    serialized.FindProperty("MenuWidth").intValue = 740;
                    serialized.FindProperty("CornerRadius").intValue = 8;
                    serialized.FindProperty("Panel").colorValue = new Color(.91f, .86f, .72f, 1);
                    serialized.FindProperty("Text").colorValue = new Color(.09f, .22f, .18f, 1);
                    serialized.FindProperty("Focus").colorValue = new Color(.9f, .3f, .1f, 1);
                    Assert.IsTrue(serialized.ApplyModifiedProperties());
                }
                theme.Validate(); EditorUtility.SetDirty(theme); AssetDatabase.SaveAssetIfDirty(theme);
                string guid = AssetDatabase.AssetPathToGUID(path);
                theme.BodyFont = null; theme.HeadingFont = null; theme.Panel = Color.black; theme.RowHeight = 1;
                Resources.UnloadAsset(theme);
                theme = AssetDatabase.LoadAssetAtPath<WordboundUiTheme>(path);
                Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path));
                Assert.AreSame(body, theme.BodyFont); Assert.AreSame(heading, theme.HeadingFont);
                Assert.AreEqual(17, theme.BodySize); Assert.AreEqual(25, theme.HeadingSize);
                Assert.AreEqual(20, theme.Padding); Assert.AreEqual(8, theme.Gap);
                Assert.AreEqual(40, theme.RowHeight); Assert.AreEqual(70, theme.ModuleHeight);
                Assert.AreEqual(740, theme.MenuWidth); Assert.AreEqual(8, theme.CornerRadius);
                Assert.AreEqual(new Color(.91f, .86f, .72f, 1), theme.Panel);
                Assert.AreEqual(new Color(.09f, .22f, .18f, 1), theme.Text);
                Assert.AreEqual(new Color(.9f, .3f, .1f, 1), theme.Focus);
                Assert.DoesNotThrow(theme.Validate);
            }
            finally
            {
                if (theme && !AssetDatabase.Contains(theme)) Object.DestroyImmediate(theme);
                DeleteFolder(); folder = null;
            }
        }

        [Test]
        public void AuthoringSetupPreservesReloadedCustomThemeAndExistingDefaultThenFillsOnlyAnEmptySlot()
        {
            WordboundLaunchAssets launch = null;
            WordboundUiTheme custom = null;
            const string productionLaunch = "Assets/_Project/Resources/Wordbound/LaunchAssets.asset";
            byte[] launchBefore = File.ReadAllBytes(productionLaunch);
            byte[] themeBefore = File.Exists(WordboundAuthoringSetup.ThemePath) ? File.ReadAllBytes(WordboundAuthoringSetup.ThemePath) : null;
            try
            {
                string launchPath = folder + "/Launch.asset", customPath = folder + "/Custom.asset";
                string defaults = folder + "/Defaults", defaultThemePath = defaults + "/Interface.asset";
                launch = ScriptableObject.CreateInstance<WordboundLaunchAssets>();
                AssetDatabase.CreateAsset(launch, launchPath);
                custom = ScriptableObject.CreateInstance<WordboundUiTheme>(); custom.MenuWidth = 700;
                AssetDatabase.CreateAsset(custom, customPath);
                launch.UiTheme = custom;
                EditorUtility.SetDirty(custom); AssetDatabase.SaveAssetIfDirty(custom);
                EditorUtility.SetDirty(launch); AssetDatabase.SaveAssetIfDirty(launch);

                InvokeAuthoringSetup(launch, defaults);
                Assert.AreSame(custom, launch.UiTheme);
                var generated = AssetDatabase.LoadAssetAtPath<WordboundUiTheme>(defaultThemePath);
                Assert.IsNotNull(generated);
                Assert.AreEqual("Assets/_Project/Resources/Fonts/Nunito-ExtraBold.ttf", AssetDatabase.GetAssetPath(generated.BodyFont));
                Assert.AreEqual("Assets/_Project/Resources/Fonts/Nunito-Black.ttf", AssetDatabase.GetAssetPath(generated.HeadingFont));
                Assert.AreEqual(defaults + "/PlayerCamera.asset", AssetDatabase.GetAssetPath(launch.CameraProfile));
                Assert.AreEqual(defaults + "/Presentation.asset", AssetDatabase.GetAssetPath(launch.VisualProfile));
                Assert.AreEqual(defaults + "/FlagshipCourse.asset", AssetDatabase.GetAssetPath(launch.WorkshopCourse));
                string generatedGuid = AssetDatabase.AssetPathToGUID(defaultThemePath);
                generated.Padding = 18; generated.MenuWidth = 710;
                EditorUtility.SetDirty(generated); AssetDatabase.SaveAssetIfDirty(generated);
                launch.UiTheme = null; custom.MenuWidth = 480;
                Resources.UnloadAsset(launch); Resources.UnloadAsset(custom); Resources.UnloadAsset(generated);
                launch = AssetDatabase.LoadAssetAtPath<WordboundLaunchAssets>(launchPath);
                custom = AssetDatabase.LoadAssetAtPath<WordboundUiTheme>(customPath);
                Assert.AreSame(custom, launch.UiTheme); Assert.AreEqual(700, custom.MenuWidth);

                InvokeAuthoringSetup(launch, defaults);
                Assert.AreSame(custom, launch.UiTheme);
                generated = AssetDatabase.LoadAssetAtPath<WordboundUiTheme>(defaultThemePath);
                Assert.AreEqual(generatedGuid, AssetDatabase.AssetPathToGUID(defaultThemePath));
                Assert.AreEqual(18, generated.Padding); Assert.AreEqual(710, generated.MenuWidth);
                Resources.UnloadAsset(launch);
                launch = AssetDatabase.LoadAssetAtPath<WordboundLaunchAssets>(launchPath);
                Assert.AreEqual(customPath, AssetDatabase.GetAssetPath(launch.UiTheme));

                launch.UiTheme = null; EditorUtility.SetDirty(launch); AssetDatabase.SaveAssetIfDirty(launch);
                InvokeAuthoringSetup(launch, defaults);
                Assert.AreSame(generated, launch.UiTheme);
                Resources.UnloadAsset(launch);
                launch = AssetDatabase.LoadAssetAtPath<WordboundLaunchAssets>(launchPath);
                Assert.AreEqual(defaultThemePath, AssetDatabase.GetAssetPath(launch.UiTheme));
                Assert.AreEqual(710, launch.UiTheme.MenuWidth);
                CollectionAssert.AreEqual(launchBefore, File.ReadAllBytes(productionLaunch));
                if (themeBefore == null) Assert.IsFalse(File.Exists(WordboundAuthoringSetup.ThemePath));
                else CollectionAssert.AreEqual(themeBefore, File.ReadAllBytes(WordboundAuthoringSetup.ThemePath));
            }
            finally
            {
                if (launch && !AssetDatabase.Contains(launch)) Object.DestroyImmediate(launch);
                if (custom && !AssetDatabase.Contains(custom)) Object.DestroyImmediate(custom);
                DeleteFolder(); folder = null;
            }
        }

        static void InvokeAuthoringSetup(WordboundLaunchAssets launch, string destination)
        {
            var method = typeof(WordboundAuthoringSetup).GetMethod("CreateDefaultsAt", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Use the exact full setup implementation called by the public menu.");
            try { method.Invoke(null, new object[] { launch, destination }); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        [Test]
        public void RepeatedDraftRecoveryPreservesExactValidZoneCollections()
        {
            var asset = CreateDraftAsset();
            var document = asset.ReadDocument();
            document.Title = "Recovered without collection defaults";
            Assert.That(document.Zones.Count, Is.GreaterThan(0));
            Assert.IsTrue(document.Zones.Any(zone => zone.Placement == "Fixed"));
            string expected = CourseJson.ExportDocument(document);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                WordboundCourseDraftStore.Save(asset, asset.SourceHash, document);
                Assert.IsFalse(WordboundCourseDraftStore.IsMemoryOnly(asset));
                document = WordboundCourseDraftStore.Load(asset).Document;
                Assert.AreEqual(expected, CourseJson.ExportDocument(document), "Recovery pass " + attempt);
            }
            asset.SaveDocument(document);
            Assert.AreEqual(expected, asset.DocumentJson);
        }

        [Test]
        public void IdenticalDraftBytesAvoidReplacementButChangedLockedContentRemainsRecoverable()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
                Assert.Ignore("This native file-sharing regression requires the Windows Editor.");
            var asset = CreateDraftAsset();
            var document = asset.ReadDocument();
            WordboundCourseDraftStore.Save(asset, asset.SourceHash, document);
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            string path = Path.Combine("Library", "Wordbound", "InspectorDrafts", guid + ".json");
            byte[] original = File.ReadAllBytes(path);
            byte[] replacement;
            string savedAsset = asset.DocumentJson;
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                WordboundCourseDraftStore.Save(asset, asset.SourceHash, document);
                Assert.IsFalse(WordboundCourseDraftStore.IsMemoryOnly(asset));
                CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
                document.Title = "New pending content while the old disk file is read-shared";
                var failure = Assert.Throws<IOException>(() => WordboundCourseDraftStore.Save(asset, asset.SourceHash, document));
                TestContext.WriteLine("Changed locked draft save HRESULT: 0x" + failure.HResult.ToString("X8"));
                Assert.IsFalse(WordboundCourseDraftStore.IsMemoryOnly(asset), "The failed publication now has a complete committed recovery copy.");
                Assert.AreEqual(document.Title, WordboundCourseDraftStore.Load(asset).Document.Title);
                CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
                Assert.AreEqual(savedAsset, asset.DocumentJson);
                replacement = File.ReadAllBytes(path + ".publish");
            }
            SaveDraftWithFailureFacts("store_released", asset, document, path, original, replacement);
            Assert.IsFalse(WordboundCourseDraftStore.IsMemoryOnly(asset));
            Assert.AreEqual(document.Title, WordboundCourseDraftStore.Load(asset).Document.Title);
            Assert.IsFalse(File.Exists(path + ".publish"));
            byte[] exact = File.ReadAllBytes(path);
            var withBom = new byte[exact.Length + 3];
            withBom[0] = 0xef; withBom[1] = 0xbb; withBom[2] = 0xbf;
            Buffer.BlockCopy(exact, 0, withBom, 3, exact.Length);
            File.WriteAllBytes(path, withBom);
            SaveDraftWithFailureFacts("store_bom", asset, document, path, withBom, exact);
            CollectionAssert.AreEqual(exact, File.ReadAllBytes(path), "Text equality must not accept a different UTF-8 byte representation.");
            Assert.IsFalse(WordboundCourseDraftStore.IsMemoryOnly(asset));
            Assert.AreEqual(savedAsset, asset.DocumentJson);
        }

        [Test]
        public void ReadOnlyPrimaryRejectsPublicationButRetainsTheExactCommittedDraftAfterMemoryLoss()
        {
            RequireWindowsDraftIo();
            var asset = CreateDraftAsset(); var document = asset.ReadDocument();
            string path = DraftPath(asset), savedAsset = asset.DocumentJson;
            WordboundCourseDraftStore.Save(asset, asset.SourceHash, document);
            byte[] original = File.ReadAllBytes(path), committed;
            FileAttributes attributes = File.GetAttributes(path);
            try
            {
                File.SetAttributes(path, attributes | FileAttributes.ReadOnly);
                document.Title = "Committed edit while primary is read-only";
                var failure = Assert.Throws<IOException>(() => WordboundCourseDraftStore.Save(asset, asset.SourceHash, document));
                Assert.AreEqual(unchecked((int)0x80070005), failure.HResult, "Windows must preserve access-denied as a failed publication.");
                CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
                Assert.IsFalse(WordboundCourseDraftStore.IsMemoryOnly(asset));
                Assert.AreEqual(1, PendingFiles(asset).Length);
                committed = File.ReadAllBytes(PendingFiles(asset).Single());
                CollectionAssert.AreEqual(committed, File.ReadAllBytes(path + ".publish"));
                ForgetMemoryDraft(asset);
                Assert.AreEqual(document.Title, WordboundCourseDraftStore.Load(asset).Document.Title);
                Assert.AreEqual(savedAsset, asset.DocumentJson);
            }
            finally { if (File.Exists(path)) File.SetAttributes(path, attributes); }
            WordboundCourseDraftStore.Save(asset, asset.SourceHash, document);
            CollectionAssert.AreEqual(committed, File.ReadAllBytes(path));
            Assert.IsFalse(File.Exists(path + ".publish"));
            Assert.AreEqual(0, PendingFiles(asset).Length);
            ForgetMemoryDraft(asset);
            Assert.AreEqual(document.Title, WordboundCourseDraftStore.Load(asset).Document.Title);
            Assert.AreEqual(savedAsset, asset.DocumentJson);
        }

        static void SaveDraftWithFailureFacts(string stage, WordboundCourseAsset asset, CourseDocument document,
            string path, byte[] previous, byte[] expected)
        {
            try { WordboundCourseDraftStore.Save(asset, asset.SourceHash, document); }
            catch (Exception error)
            {
                try
                {
                    TestContext.WriteLine("WORDBOUND_DRAFT_STORE_FAILURE: stage=" + stage + "; type=" + error.GetType().Name +
                        "; hresult=0x" + error.HResult.ToString("X8") + "; memoryOnly=" + WordboundCourseDraftStore.IsMemoryOnly(asset));
                    ObserveDraftFile(stage, "target", path, previous, expected);
                    ObserveDraftFile(stage, "temporary", path + ".publish", previous, expected);
                }
                catch (Exception) { }
                throw;
            }
        }

        static void ObserveDraftFile(string stage, string role, string path, byte[] previous, byte[] expected)
        {
            try
            {
                var info = new FileInfo(path);
                bool exists = info.Exists;
                long length = exists ? info.Length : -1;
                byte[] bytes = exists && length <= 4 * 1024 * 1024 ? File.ReadAllBytes(path) : null;
                TestContext.WriteLine("WORDBOUND_DRAFT_FILE: stage=" + stage + "; role=" + role + "; exists=" + exists +
                    "; length=" + length + "; attributes=" + (exists ? (int)info.Attributes : -1) +
                    "; equalsPrevious=" + (bytes != null && bytes.SequenceEqual(previous)) +
                    "; equalsExpected=" + (bytes != null && bytes.SequenceEqual(expected)));
            }
            catch (Exception error)
            {
                TestContext.WriteLine("WORDBOUND_DRAFT_FILE: stage=" + stage + "; role=" + role + "; readType=" +
                    error.GetType().Name + "; hresult=0x" + error.HResult.ToString("X8"));
            }
            try
            {
                using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                TestContext.WriteLine("WORDBOUND_DRAFT_EXCLUSIVE_OPEN: stage=" + stage + "; role=" + role + "; success=True");
            }
            catch (Exception error)
            {
                TestContext.WriteLine("WORDBOUND_DRAFT_EXCLUSIVE_OPEN: stage=" + stage + "; role=" + role + "; success=False; type=" +
                    error.GetType().Name + "; hresult=0x" + error.HResult.ToString("X8"));
            }
        }

        [Test]
        public void NativeDraftSequenceRewritesRetainedTemporaryAfterComparisonRead()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
                Assert.Ignore("This native draft sequence diagnostic requires the Windows Editor.");
            var asset = CreateDraftAsset();
            var document = asset.ReadDocument();
            WordboundCourseDraftStore.Save(asset, asset.SourceHash, document);
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            Assert.AreEqual(32, guid.Length);
            string root = Path.GetFullPath(Path.Combine("Library", "Wordbound", "InspectorDrafts")) + Path.DirectorySeparatorChar;
            string path = Path.Combine("Library", "Wordbound", "InspectorDrafts", guid + ".json");
            Assert.IsTrue(Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase));
            string temporary = path + ".publish";
            byte[] original = File.ReadAllBytes(path), replacement;
            string savedAsset = asset.DocumentJson;
            try
            {
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    WordboundCourseDraftStore.Save(asset, asset.SourceHash, document);
                    Assert.IsFalse(WordboundCourseDraftStore.IsMemoryOnly(asset));
                    document.Title = "New pending content while the old disk file is read-shared";
                    var failure = Assert.Throws<IOException>(() => WordboundCourseDraftStore.Save(asset, asset.SourceHash, document));
                    TestContext.WriteLine("WORDBOUND_DRAFT_SEQUENCE: stage=store_locked; type=" + failure.GetType().Name +
                        "; hresult=0x" + failure.HResult.ToString("X8"));
                    Assert.AreEqual(unchecked((int)0x80070020), failure.HResult);
                    Assert.IsFalse(WordboundCourseDraftStore.IsMemoryOnly(asset), "The pending revision is durable before primary publication.");
                    Assert.AreEqual(document.Title, WordboundCourseDraftStore.Load(asset).Document.Title);
                    CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
                    Assert.AreEqual(savedAsset, asset.DocumentJson);
                    replacement = File.ReadAllBytes(temporary);
                }
                var comparison = typeof(WordboundCourseDraftStore).GetMethod("SameBytes", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.IsNotNull(comparison, "Exercise the actual Store comparison handle lifetime without exposing a new production API.");
                Assert.IsFalse((bool)comparison.Invoke(null, new object[] { path, replacement }));
                File.WriteAllBytes(temporary, replacement);
                var result = ObserveReplacement("actual_payload_compare_rewrite_released", temporary, path, null, original, replacement, false);
                if (result.ErrorType != null)
                {
                    ObserveDraftFile("direct_released", "target", path, original, replacement);
                    ObserveDraftFile("direct_released", "temporary", temporary, original, replacement);
                }
                Assert.IsNull(result.ErrorType, "Direct Store IO sequence HRESULT=0x" + result.HResult.ToString("X8"));
                Assert.IsTrue(result.TargetReplacement && !result.SourceExists);
                Assert.IsTrue(HasMemoryDraft(asset), "The direct IO control does not run Store success bookkeeping.");
                Assert.AreEqual(savedAsset, asset.DocumentJson);
            }
            finally
            {
                Assert.IsTrue(Path.GetFullPath(temporary).StartsWith(root, StringComparison.OrdinalIgnoreCase));
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        [Test]
        public void NativeReplacementPublicationReportsNullAndNamedBackupFileState()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
                Assert.Ignore("This native replacement diagnostic requires the Windows Editor.");
            string probeRoot = Path.GetFullPath(Path.Combine("Library", "Wordbound", "InspectorDraftProbes"));
            string probeFolder = Path.GetFullPath(Path.Combine(probeRoot, Guid.NewGuid().ToString("N")));
            string prefix = probeRoot + Path.DirectorySeparatorChar;
            Assert.IsTrue(probeFolder.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            byte[] original = System.Text.Encoding.UTF8.GetBytes(new string('a', 2315));
            byte[] replacement = System.Text.Encoding.UTF8.GetBytes(new string('b', 2336));
            var observations = new System.Collections.Generic.List<ReplacementObservation>();
            try
            {
                Directory.CreateDirectory(probeFolder);
                foreach (bool namedBackup in new[] { false, true })
                {
                    string label = namedBackup ? "named" : "null";
                    string path = Path.Combine(probeFolder, label + ".json");
                    string temporary = path + ".tmp", backup = namedBackup ? path + ".bak" : null;
                    File.WriteAllBytes(path, original); File.WriteAllBytes(temporary, replacement);
                    observations.Add(ObserveReplacement(label + "_fresh", temporary, path, backup, original, replacement, false));
                    File.Delete(path); File.Delete(temporary);
                    if (backup != null) File.Delete(backup);
                    File.WriteAllBytes(path, original); File.WriteAllBytes(temporary, replacement);
                    using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                        observations.Add(ObserveReplacement(label + "_locked", temporary, path, backup, original, replacement, true));
                    observations.Add(ObserveReplacement(label + "_released", temporary, path, backup, original, replacement, false));
                }
            }
            finally
            {
                Assert.IsTrue(Path.GetFullPath(probeFolder).StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                if (Directory.Exists(probeFolder)) Directory.Delete(probeFolder, true);
            }
            Assert.AreEqual(6, observations.Count);
            foreach (var result in observations)
            {
                if (result.Locked)
                {
                    Assert.AreEqual(typeof(IOException), result.ErrorType, result.Label);
                    Assert.AreEqual(unchecked((int)0x80070020), result.HResult, result.Label);
                    Assert.IsTrue(result.TargetOriginal && result.SourceReplacement && !result.BackupExists, result.Label);
                }
                else
                {
                    Assert.IsNull(result.ErrorType, result.Label + " HRESULT=0x" + result.HResult.ToString("X8"));
                    Assert.IsTrue(result.TargetReplacement && !result.SourceExists, result.Label);
                    Assert.AreEqual(result.NamedBackup, result.BackupExists, result.Label);
                    if (result.NamedBackup) Assert.IsTrue(result.BackupOriginal, result.Label);
                }
            }
        }

        sealed class ReplacementObservation
        {
            public string Label;
            public bool Locked, NamedBackup, TargetOriginal, TargetReplacement, SourceExists, SourceReplacement, BackupExists, BackupOriginal;
            public Type ErrorType;
            public int HResult;
        }

        static ReplacementObservation ObserveReplacement(string label, string temporary, string path, string backup,
            byte[] original, byte[] replacement, bool locked)
        {
            var result = new ReplacementObservation { Label = label, Locked = locked, NamedBackup = backup != null };
            try { File.Replace(temporary, path, backup); }
            catch (Exception error) { result.ErrorType = error.GetType(); result.HResult = error.HResult; }
            result.TargetOriginal = File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(original);
            result.TargetReplacement = File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(replacement);
            result.SourceExists = File.Exists(temporary);
            result.SourceReplacement = result.SourceExists && File.ReadAllBytes(temporary).SequenceEqual(replacement);
            result.BackupExists = backup != null && File.Exists(backup);
            result.BackupOriginal = result.BackupExists && File.ReadAllBytes(backup).SequenceEqual(original);
            TestContext.WriteLine("WORDBOUND_DRAFT_REPLACE: stage=" + label + "; type=" + (result.ErrorType?.Name ?? "none") +
                "; hresult=0x" + result.HResult.ToString("X8") + "; targetOld=" + result.TargetOriginal +
                "; targetNew=" + result.TargetReplacement + "; sourceExists=" + result.SourceExists +
                "; sourceNew=" + result.SourceReplacement + "; backupExists=" + result.BackupExists +
                "; backupOld=" + result.BackupOriginal);
            return result;
        }

        [Test]
        public void InvalidPendingFormSurvivesClosingInspectorAndRejectedApplyWithoutChangingSavedCourse()
        {
            var asset = CreateDraftAsset();
            string original = asset.DocumentJson;
            var inspector = UnityEditor.Editor.CreateEditor(asset, typeof(WordboundCourseAssetEditor));
            try
            {
                var pending = EditorField<CourseDocument>(inspector, "draft");
                pending.Title = "Still editing";
                pending.Modules[0].Position = new CoursePoint(int.MaxValue, -100, 31);
                pending.Zones[0].Yaws.Clear();
                pending.Zones[0].Yaws.AddRange(new[] { 90, 90, 13 });
                pending.ReservedVolumes.Add(new CourseReservedVolume {
                    Id = "unfinished", Bounds = new CourseBox(new CoursePoint(900, 300, 100), new CoursePoint(-50, 0, -10))
                });
                InvokeEditor(inspector, "MarkDirty");
                Object.DestroyImmediate(inspector);
                inspector = UnityEditor.Editor.CreateEditor(asset, typeof(WordboundCourseAssetEditor));
                pending = EditorField<CourseDocument>(inspector, "draft");
                Assert.IsTrue(EditorField<bool>(inspector, "dirty"));
                Assert.AreEqual("Still editing", pending.Title);
                Assert.AreEqual(int.MaxValue, pending.Modules[0].Position.X);
                Assert.AreEqual(-50, pending.ReservedVolumes.Last().Bounds.Max.X);
                CollectionAssert.AreEqual(new[] { 90, 90, 13 }, pending.Zones[0].Yaws,
                    "Recovery must preserve unfinished values, including duplicates and invalid angles.");
                Assert.Throws<ArgumentException>(() => InvokeEditor(inspector, "Save"));
                Assert.AreEqual(original, asset.DocumentJson);
                Assert.AreEqual("Still editing", WordboundCourseDraftStore.Load(asset).Document.Title);
            }
            finally { if (inspector) Object.DestroyImmediate(inspector); }
        }

        [Test]
        public void ChangedSavedCourseRequiresExplicitRecoveryChoiceAndSuccessfulApplyClearsDraft()
        {
            var asset = CreateDraftAsset();
            var pending = asset.ReadDocument(); pending.Title = "Pending title";
            WordboundCourseDraftStore.Save(asset, asset.SourceHash, pending);
            var saved = asset.ReadDocument(); saved.Title = "Changed elsewhere";
            asset.SaveDocument(saved); EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset);
            var inspector = UnityEditor.Editor.CreateEditor(asset, typeof(WordboundCourseAssetEditor));
            try
            {
                Assert.AreEqual("Changed elsewhere", EditorField<CourseDocument>(inspector, "draft").Title);
                Assert.AreEqual("Pending title", EditorField<WordboundCourseDraftStore.PendingDraft>(inspector, "conflictingDraft").Document.Title);
                Assert.DoesNotThrow(() => InvokeEditor(inspector, "SceneGUI", new object[] { null }));
                Assert.Throws<InvalidOperationException>(() => InvokeEditor(inspector, "Save"));
                Assert.AreEqual("Changed elsewhere", asset.ReadDocument().Title);
                InvokeEditor(inspector, "RestorePending");
                InvokeEditor(inspector, "Save");
                Assert.AreEqual("Pending title", asset.ReadDocument().Title);
                Assert.IsNull(WordboundCourseDraftStore.Load(asset));
                Assert.IsFalse(EditorField<bool>(inspector, "dirty"));
            }
            finally { Object.DestroyImmediate(inspector); }
        }

        [Test]
        public void LockedRecoveryFileKeepsNewestEditsThroughSavedChangesAndInspectorRecreation()
        {
            var asset = CreateDraftAsset();
            WordboundCourseDraftStore.Save(asset, asset.SourceHash, asset.ReadDocument());
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            string path = Path.Combine("Library", "Wordbound", "InspectorDrafts", guid + ".json");
            var inspector = UnityEditor.Editor.CreateEditor(asset, typeof(WordboundCourseAssetEditor));
            try
            {
                EditorField<CourseDocument>(inspector, "draft").Title = "Newest edits";
                InvokeEditor(inspector, "MarkDirty");
                var external = asset.ReadDocument(); external.Title = "Saved elsewhere"; asset.SaveDocument(external);
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("^Wordbound pending form could not be saved:"));
                    InvokeEditor(inspector, "ReloadAfterUndo");
                    Assert.AreEqual("Newest edits", EditorField<WordboundCourseDraftStore.PendingDraft>(inspector, "conflictingDraft").Document.Title);
                    Assert.AreEqual("Saved elsewhere", asset.ReadDocument().Title);
                    InvokeEditor(inspector, "RestorePending");
                    LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("^Wordbound pending form could not be saved:"));
                    Object.DestroyImmediate(inspector);
                    inspector = UnityEditor.Editor.CreateEditor(asset, typeof(WordboundCourseAssetEditor));
                    Assert.AreEqual("Newest edits", EditorField<CourseDocument>(inspector, "draft").Title);
                    Assert.IsTrue(EditorField<bool>(inspector, "dirty"));
                    Assert.IsNotEmpty(EditorField<string>(inspector, "recoveryWarning"));
                    Assert.IsFalse(double.IsPositiveInfinity(EditorField<double>(inspector, "saveDraftAt")));
                }
                InvokeEditor(inspector, "SavePendingDraft");
                Assert.AreEqual("", EditorField<string>(inspector, "recoveryWarning"));
                Object.DestroyImmediate(inspector);
                inspector = UnityEditor.Editor.CreateEditor(asset, typeof(WordboundCourseAssetEditor));
                Assert.AreEqual("Newest edits", EditorField<CourseDocument>(inspector, "draft").Title);
                InvokeEditor(inspector, "Save");
                Assert.AreEqual("Newest edits", asset.ReadDocument().Title);
                Assert.IsNull(WordboundCourseDraftStore.Load(asset));
            }
            finally { if (inspector) Object.DestroyImmediate(inspector); }
        }

        [Test]
        public void DamagedPendingFileIsRetainedWhenInspectorCloses()
        {
            var asset = CreateDraftAsset();
            WordboundCourseDraftStore.Save(asset, asset.SourceHash, asset.ReadDocument());
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            string path = Path.Combine("Library", "Wordbound", "InspectorDrafts", guid + ".json");
            File.WriteAllText(path, "interrupted local draft");
            var inspector = UnityEditor.Editor.CreateEditor(asset, typeof(WordboundCourseAssetEditor));
            try
            {
                Assert.IsTrue(EditorField<bool>(inspector, "recoveryFailed"));
                Assert.Throws<InvalidOperationException>(() => InvokeEditor(inspector, "Save"));
            }
            finally { Object.DestroyImmediate(inspector); }
            Assert.AreEqual("interrupted local draft", File.ReadAllText(path));
        }

        [Test]
        public void FailedPublicationRecoversLatestCommittedEditAfterMemoryLossAndPartialLaterWrite()
        {
            RequireWindowsDraftIo();
            var asset = CreateDraftAsset(); var document = asset.ReadDocument();
            string saved = asset.DocumentJson, path = DraftPath(asset);
            WordboundCourseDraftStore.Save(asset, asset.SourceHash, document);
            byte[] previous = File.ReadAllBytes(path);
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                document.Title = "First failed publication";
                Assert.Throws<IOException>(() => WordboundCourseDraftStore.Save(asset, asset.SourceHash, document));
                document.Title = "Latest complete pending edit";
                Assert.Throws<IOException>(() => WordboundCourseDraftStore.Save(asset, asset.SourceHash, document));
                string[] pending = PendingFiles(asset);
                Assert.AreEqual(1, pending.Length, "A new complete revision reclaims the older failed candidate.");
                byte[] committed = File.ReadAllBytes(pending[0]);
                Assert.Throws<IOException>(() => WordboundCourseDraftStore.Save(asset, asset.SourceHash, document));
                CollectionAssert.AreEqual(pending, PendingFiles(asset), "Identical retries reuse the committed revision.");
                CollectionAssert.AreEqual(committed, File.ReadAllBytes(pending[0]));
                File.WriteAllText(path + ".stage", "partial next write");
                ForgetMemoryDraft(asset);
                Assert.AreEqual(document.Title, WordboundCourseDraftStore.Load(asset).Document.Title);
                var editor = UnityEditor.Editor.CreateEditor(asset, typeof(WordboundCourseAssetEditor));
                try
                {
                    Assert.AreEqual(document.Title, EditorField<CourseDocument>(editor, "draft").Title);
                    Assert.IsFalse(WordboundCourseDraftStore.IsMemoryOnly(asset));
                    Assert.IsNotEmpty(EditorField<string>(editor, "recoveryWarning"));
                    Assert.AreEqual("partial next write", File.ReadAllText(path + ".stage"));
                    LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("^Wordbound pending form could not be saved:"));
                }
                finally { Object.DestroyImmediate(editor); }
                CollectionAssert.AreEqual(previous, File.ReadAllBytes(path));
                Assert.AreEqual(saved, asset.DocumentJson);
            }
            WordboundCourseDraftStore.Save(asset, asset.SourceHash, document);
            ForgetMemoryDraft(asset);
            Assert.AreEqual(document.Title, WordboundCourseDraftStore.Load(asset).Document.Title);
            Assert.AreEqual(0, PendingFiles(asset).Length);
            Assert.IsFalse(File.Exists(path + ".stage"));
            Assert.AreEqual(saved, asset.DocumentJson);
        }

        [Test]
        public void CommittedOrderingIgnoresTimestampsAndRevertingToOldPrimaryIsANewEdit()
        {
            RequireWindowsDraftIo();
            var asset = CreateDraftAsset(); var document = asset.ReadDocument();
            string path = DraftPath(asset), firstTitle = document.Title;
            WordboundCourseDraftStore.Save(asset, asset.SourceHash, document);
            byte[] first = File.ReadAllBytes(path);
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                document.Title = "Second revision";
                Assert.Throws<IOException>(() => WordboundCourseDraftStore.Save(asset, asset.SourceHash, document));
                File.SetLastWriteTimeUtc(PendingFiles(asset).Single(), new DateTime(2001, 1, 1));
                ForgetMemoryDraft(asset);
                Assert.AreEqual(document.Title, WordboundCourseDraftStore.Load(asset).Document.Title);
                document.Title = firstTitle;
                Assert.Throws<IOException>(() => WordboundCourseDraftStore.Save(asset, asset.SourceHash, document));
                ForgetMemoryDraft(asset);
                Assert.AreEqual(firstTitle, WordboundCourseDraftStore.Load(asset).Document.Title);
                Assert.AreEqual(3, ReadRevision(PendingFiles(asset).Single(), asset));
                CollectionAssert.AreEqual(first, File.ReadAllBytes(path));
            }
            WordboundCourseDraftStore.Save(asset, asset.SourceHash, document);
            File.WriteAllBytes(PendingPath(asset, 1), first);
            File.SetLastWriteTimeUtc(PendingPath(asset, 1), new DateTime(2099, 1, 1));
            ForgetMemoryDraft(asset);
            Assert.AreEqual(3, ReadRevision(path, asset));
            Assert.AreEqual(firstTitle, WordboundCourseDraftStore.Load(asset).Document.Title);
        }

        [TestCase("checksum")]
        [TestCase("guid")]
        [TestCase("source")]
        [TestCase("utf8")]
        [TestCase("oversize")]
        [TestCase("missing_format")]
        [TestCase("missing_payload_format")]
        public void InvalidCommittedNewestRecordBlocksSilentRollbackAndPreservesBytes(string damage)
        {
            var asset = CreateDraftAsset(); var document = asset.ReadDocument();
            WordboundCourseDraftStore.Save(asset, asset.SourceHash, document);
            string path = DraftPath(asset), pending = PendingPath(asset, 2), saved = asset.DocumentJson;
            byte[] original = File.ReadAllBytes(path);
            object record = ReadDraftRecord(path, asset);
            SetRecord(record, "Revision", 2L);
            if (damage == "guid") SetRecord(record, "AssetGuid", new string('a', 32));
            if (damage == "source") ((WordboundCourseDraftStore.PendingDraft)GetRecord(record, "Draft")).SavedSourceHash = new string('z', 64);
            byte[] bytes = EncodeDraftRecord(record);
            if (damage == "checksum") bytes[bytes.Length - 3] = bytes[bytes.Length - 3] == (byte)'a' ? (byte)'b' : (byte)'a';
            if (damage == "utf8") bytes = new byte[] { 0xff, 0xfe, 0x7b, 0 };
            if (damage == "oversize") bytes = new byte[4 * 1024 * 1024 + 1];
            if (damage == "missing_format") bytes = System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(bytes)
                .Replace("\"Format\":\"wordbound.inspector-recovery-v2\",", ""));
            if (damage == "missing_payload_format") bytes = System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(bytes)
                .Replace("\"Format\":\"wordbound.inspector-draft-v1\",", ""));
            File.WriteAllBytes(pending, bytes); ForgetMemoryDraft(asset);
            Assert.Catch(() => WordboundCourseDraftStore.Load(asset));
            var editor = UnityEditor.Editor.CreateEditor(asset, typeof(WordboundCourseAssetEditor));
            try { Assert.IsTrue(EditorField<bool>(editor, "recoveryFailed")); }
            finally { Object.DestroyImmediate(editor); }
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(pending));
            CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
            Assert.AreEqual(saved, asset.DocumentJson);
        }

        [Test]
        public void SameRevisionDisagreementFailsClosedAndEquivalentPublicationResidueIsHarmless()
        {
            var asset = CreateDraftAsset(); var document = asset.ReadDocument();
            WordboundCourseDraftStore.Save(asset, asset.SourceHash, document);
            string path = DraftPath(asset), pending = PendingPath(asset, 1);
            byte[] original = File.ReadAllBytes(path);
            File.WriteAllBytes(pending, original); ForgetMemoryDraft(asset);
            Assert.AreEqual(document.Title, WordboundCourseDraftStore.Load(asset).Document.Title);
            object record = ReadDraftRecord(path, asset);
            ((WordboundCourseDraftStore.PendingDraft)GetRecord(record, "Draft")).Document.Title = "Conflicting same revision";
            byte[] conflict = EncodeDraftRecord(record); File.WriteAllBytes(pending, conflict);
            Assert.Throws<ArgumentException>(() => WordboundCourseDraftStore.Load(asset));
            CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
            CollectionAssert.AreEqual(conflict, File.ReadAllBytes(pending));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void LegacyOrphanNeedsExplicitChoiceAndCannotAutosaveOverCurrentDraft(bool restore)
        {
            var asset = CreateDraftAsset(); var document = asset.ReadDocument();
            string saved = asset.DocumentJson, path = DraftPath(asset);
            WordboundCourseDraftStore.Save(asset, asset.SourceHash, document);
            string current = document.Title; document.Title = "Legacy temporary edit";
            string legacy = LegacyDraftJson(asset, document); File.WriteAllText(path + ".tmp", legacy);
            ForgetMemoryDraft(asset);
            Assert.Throws<WordboundCourseDraftStore.LegacyDraftChoiceException>(() => WordboundCourseDraftStore.Load(asset));
            var editor = UnityEditor.Editor.CreateEditor(asset, typeof(WordboundCourseAssetEditor));
            try
            {
                Assert.IsTrue(EditorField<bool>(editor, "legacyChoice"));
                InvokeEditor(editor, "SavePendingDraft");
                Assert.AreEqual(legacy, File.ReadAllText(path + ".tmp"));
                InvokeEditor(editor, "ResolveLegacy", restore);
                Assert.IsFalse(EditorField<bool>(editor, "legacyChoice"));
                Assert.AreEqual(restore ? document.Title : current, EditorField<CourseDocument>(editor, "draft").Title);
                Assert.AreEqual(saved, asset.DocumentJson);
                Assert.IsFalse(File.Exists(path + ".tmp"));
            }
            finally { Object.DestroyImmediate(editor); }
        }

        [Test]
        public void LegacyPrimaryStillLoadsAndMigratesWithoutChangingCourseOrUnfinishedValues()
        {
            var asset = CreateDraftAsset(); string saved = asset.DocumentJson;
            var document = asset.ReadDocument(); document.Title = "Legacy pending"; document.Zones[0].Yaws.Add(13);
            string path = DraftPath(asset); Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, LegacyDraftJson(asset, document));
            Assert.AreEqual(0, ReadRevision(path, asset));
            WordboundCourseDraftStore.Save(asset, asset.SourceHash, WordboundCourseDraftStore.Load(asset).Document);
            ForgetMemoryDraft(asset);
            Assert.AreEqual(1, ReadRevision(path, asset));
            CollectionAssert.AreEqual(document.Zones[0].Yaws, WordboundCourseDraftStore.Load(asset).Document.Zones[0].Yaws);
            Assert.AreEqual(saved, asset.DocumentJson);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CommittedDiscardOrApplySurvivesCleanupFailureAndInspectorCloseWithoutResurrectingEdits(bool apply)
        {
            RequireWindowsDraftIo();
            var asset = CreateDraftAsset(); string path = DraftPath(asset), saved = asset.DocumentJson;
            var editor = UnityEditor.Editor.CreateEditor(asset, typeof(WordboundCourseAssetEditor));
            try
            {
                EditorField<CourseDocument>(editor, "draft").Title = "Intentionally discarded";
                string expectedSaved = apply ? CourseJson.ExportDocument(EditorField<CourseDocument>(editor, "draft")) : saved;
                InvokeEditor(editor, "MarkDirty"); InvokeEditor(editor, "SavePendingDraft");
                byte[] old = File.ReadAllBytes(path);
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    InvokeEditor(editor, apply ? "Save" : "DiscardPending");
                    Assert.IsFalse(EditorField<bool>(editor, "dirty"));
                    Assert.IsNotEmpty(EditorField<string>(editor, "recoveryWarning"));
                    Assert.IsNull(WordboundCourseDraftStore.Load(asset));
                    CollectionAssert.AreEqual(old, File.ReadAllBytes(path));
                    string discard = PendingFiles(asset).Single(); byte[] committed = File.ReadAllBytes(discard);
                    Object.DestroyImmediate(editor); editor = null; ForgetMemoryDraft(asset);
                    editor = UnityEditor.Editor.CreateEditor(asset, typeof(WordboundCourseAssetEditor));
                    Assert.IsFalse(EditorField<bool>(editor, "dirty"));
                    Assert.AreEqual(asset.ReadDocument().Title, EditorField<CourseDocument>(editor, "draft").Title);
                    CollectionAssert.AreEqual(committed, File.ReadAllBytes(discard));
                }
                Assert.AreEqual(expectedSaved, asset.DocumentJson);
                WordboundCourseDraftStore.Clear(asset); ForgetMemoryDraft(asset);
                Assert.IsNull(WordboundCourseDraftStore.Load(asset)); Assert.AreEqual(0, PendingFiles(asset).Length);
            }
            finally { if (editor) Object.DestroyImmediate(editor); }
        }

        [Test]
        public void RecoveryNoticeDoesNotReparseAnExplicitlyDiscardedCorruptPrimary()
        {
            RequireWindowsDraftIo();
            var asset = CreateDraftAsset(); var document = asset.ReadDocument();
            string path = DraftPath(asset), saved = asset.DocumentJson;
            WordboundCourseDraftStore.Save(asset, asset.SourceHash, document);
            File.WriteAllText(path, "known corrupt old primary");
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                WordboundCourseDraftStore.Clear(asset);
                document.Title = "New edit after explicit discard";
                Assert.Throws<IOException>(() => WordboundCourseDraftStore.Save(asset, asset.SourceHash, document));
                ForgetMemoryDraft(asset);
                Assert.AreEqual(document.Title, WordboundCourseDraftStore.Load(asset).Document.Title);
                var editor = UnityEditor.Editor.CreateEditor(asset, typeof(WordboundCourseAssetEditor));
                try
                {
                    Assert.IsFalse(EditorField<bool>(editor, "recoveryFailed"));
                    Assert.IsTrue(EditorField<bool>(editor, "dirty"));
                    Assert.AreEqual(document.Title, EditorField<CourseDocument>(editor, "draft").Title);
                    Assert.IsNotEmpty(EditorField<string>(editor, "recoveryWarning"));
                    LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("^Wordbound pending form could not be saved:"));
                }
                finally { Object.DestroyImmediate(editor); }
                Assert.AreEqual("known corrupt old primary", File.ReadAllText(path));
            }
            WordboundCourseDraftStore.Save(asset, asset.SourceHash, document);
            ForgetMemoryDraft(asset);
            Assert.AreEqual(document.Title, WordboundCourseDraftStore.Load(asset).Document.Title);
            Assert.AreEqual(saved, asset.DocumentJson);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReleasedResidueLocksAllowSaveOrDiscardWithoutLosingTheLatestCommittedCandidate(bool discardAfterRelease)
        {
            RequireWindowsDraftIo();
            var asset = CreateDraftAsset(); var document = asset.ReadDocument();
            WordboundCourseDraftStore.Save(asset, asset.SourceHash, document);
            string path = DraftPath(asset), saved = asset.DocumentJson;
            byte[] primary = File.ReadAllBytes(path);
            object record = ReadDraftRecord(path, asset);
            for (long revision = 2; revision <= 9; revision++)
            {
                SetRecord(record, "Revision", revision);
                ((WordboundCourseDraftStore.PendingDraft)GetRecord(record, "Draft")).Document.Title = "Retained " + revision;
                File.WriteAllBytes(PendingPath(asset, revision), EncodeDraftRecord(record));
            }
            string[] files = PendingFiles(asset); byte[] latest = File.ReadAllBytes(files.Last());
            var locks = files.Select(file => new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read)).ToArray();
            try
            {
                document.Title = "New edit after cleanup unlocks";
                Assert.Throws<IOException>(() => WordboundCourseDraftStore.Save(asset, asset.SourceHash, document));
                Assert.IsTrue(WordboundCourseDraftStore.IsMemoryOnly(asset));
                Assert.AreEqual(document.Title, WordboundCourseDraftStore.Load(asset).Document.Title);
                CollectionAssert.AreEqual(files, PendingFiles(asset));
                CollectionAssert.AreEqual(latest, File.ReadAllBytes(files.Last()));
                CollectionAssert.AreEqual(primary, File.ReadAllBytes(path));
            }
            finally { foreach (var held in locks) held.Dispose(); }
            if (discardAfterRelease)
            {
                WordboundCourseDraftStore.Clear(asset); ForgetMemoryDraft(asset);
                Assert.IsNull(WordboundCourseDraftStore.Load(asset));
                Assert.IsFalse(File.Exists(path));
            }
            else
            {
                WordboundCourseDraftStore.Save(asset, asset.SourceHash, document); ForgetMemoryDraft(asset);
                Assert.AreEqual(10, ReadRevision(path, asset));
                Assert.AreEqual(document.Title, WordboundCourseDraftStore.Load(asset).Document.Title);
            }
            Assert.AreEqual(0, PendingFiles(asset).Length);
            Assert.AreEqual(saved, asset.DocumentJson);
        }

        [Test]
        public void CorruptNewestAtCapacityCannotPruneTheSoleValidRecoveryBeforeDiscardCommits()
        {
            var asset = CreateDraftAsset(); var document = asset.ReadDocument();
            WordboundCourseDraftStore.Save(asset, asset.SourceHash, document);
            string path = DraftPath(asset); object record = ReadDraftRecord(path, asset);
            byte[] primary = File.ReadAllBytes(path);
            for (long revision = 2; revision <= 9; revision++)
            {
                SetRecord(record, "Revision", revision);
                File.WriteAllBytes(PendingPath(asset, revision), revision == 2 ? EncodeDraftRecord(record) : new byte[] { 0xff });
            }
            string[] files = PendingFiles(asset); byte[] sole = File.ReadAllBytes(PendingPath(asset, 2));
            try
            {
                Assert.Throws<IOException>(() => WordboundCourseDraftStore.Clear(asset));
                CollectionAssert.AreEqual(files, PendingFiles(asset));
                CollectionAssert.AreEqual(sole, File.ReadAllBytes(PendingPath(asset, 2)));
                CollectionAssert.AreEqual(primary, File.ReadAllBytes(path));
            }
            finally
            {
                foreach (string file in files.Where(file => file != PendingPath(asset, 2))) File.Delete(file);
            }
        }

        [Test]
        public void FailedPendingCommitPreservesPriorDraftAndUncommittedStageIsNotRecovered()
        {
            RequireWindowsDraftIo();
            var asset = CreateDraftAsset(); var document = asset.ReadDocument();
            WordboundCourseDraftStore.Save(asset, asset.SourceHash, document);
            string path = DraftPath(asset), blocked = PendingPath(asset, 3), saved = asset.DocumentJson;
            byte[] prior = File.ReadAllBytes(path);
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                document.Title = "Latest committed pending edit";
                Assert.Throws<IOException>(() => WordboundCourseDraftStore.Save(asset, asset.SourceHash, document));
            }
            string pending = PendingPath(asset, 2); byte[] newest = File.ReadAllBytes(pending);
            Directory.CreateDirectory(blocked);
            try
            {
                document.Title = "Not committed";
                var failure = Assert.Catch(() => WordboundCourseDraftStore.Save(asset, asset.SourceHash, document));
                Assert.IsTrue(failure is IOException || failure is UnauthorizedAccessException);
                Assert.IsTrue(WordboundCourseDraftStore.IsMemoryOnly(asset));
                CollectionAssert.AreEqual(prior, File.ReadAllBytes(path));
                CollectionAssert.AreEqual(newest, File.ReadAllBytes(pending));
                Assert.IsTrue(File.Exists(path + ".stage"));
                ForgetMemoryDraft(asset);
                Assert.AreEqual("Latest committed pending edit", WordboundCourseDraftStore.Load(asset).Document.Title);
                Assert.AreEqual(saved, asset.DocumentJson);
            }
            finally { Directory.Delete(blocked); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OversizedLegacyBytesAndExcessMatchingNamesFailBeforeRecoveryMutation(bool excessNames)
        {
            var asset = CreateDraftAsset(); var document = asset.ReadDocument();
            WordboundCourseDraftStore.Save(asset, asset.SourceHash, document);
            string path = DraftPath(asset); byte[] primary = File.ReadAllBytes(path);
            string[] extra = excessNames ? Enumerable.Range(0, 9).Select(i => path + ".pending.invalid" + i).ToArray() : new[] { path + ".tmp" };
            try
            {
                foreach (string file in extra)
                {
                    using var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write);
                    stream.SetLength(excessNames ? 1 : 4 * 1024 * 1024 + 1);
                }
                Assert.Catch(() => WordboundCourseDraftStore.Load(asset));
                Assert.IsTrue(extra.All(File.Exists));
                Assert.AreEqual(excessNames ? 1 : 4 * 1024 * 1024 + 1, new FileInfo(extra[0]).Length);
                CollectionAssert.AreEqual(primary, File.ReadAllBytes(path));
                Assert.IsFalse(HasMemoryDraft(asset));
            }
            finally { foreach (string file in extra) if (File.Exists(file)) File.Delete(file); }
        }

        static void RequireWindowsDraftIo()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor) Assert.Ignore("Requires actual Windows file-sharing semantics.");
        }
        static string DraftGuid(WordboundCourseAsset asset) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
        static string DraftPath(WordboundCourseAsset asset) => Path.Combine("Library", "Wordbound", "InspectorDrafts", DraftGuid(asset) + ".json");
        static string PendingPath(WordboundCourseAsset asset, long revision) => DraftPath(asset) + ".pending." + revision.ToString("D20", System.Globalization.CultureInfo.InvariantCulture);
        static string[] PendingFiles(WordboundCourseAsset asset) => Directory.GetFiles(Path.GetDirectoryName(DraftPath(asset)), Path.GetFileName(DraftPath(asset)) + ".pending.*").OrderBy(p => p, StringComparer.Ordinal).ToArray();
        static System.Collections.IDictionary MemoryDrafts => (System.Collections.IDictionary)typeof(WordboundCourseDraftStore)
            .GetField("unsavedRecovery", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        static bool HasMemoryDraft(WordboundCourseAsset asset) => MemoryDrafts.Contains(DraftGuid(asset));
        static void ForgetMemoryDraft(WordboundCourseAsset asset) => MemoryDrafts.Remove(DraftGuid(asset));
        static object StoreMethod(string name, params object[] args)
        {
            try { return typeof(WordboundCourseDraftStore).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        static object ReadDraftRecord(string path, WordboundCourseAsset asset) => StoreMethod("ReadRecord", path, DraftGuid(asset));
        static object GetRecord(object record, string field) => record.GetType().GetField(field).GetValue(record);
        static void SetRecord(object record, string field, object value) => record.GetType().GetField(field).SetValue(record, value);
        static long ReadRevision(string path, WordboundCourseAsset asset) => (long)GetRecord(ReadDraftRecord(path, asset), "Revision");
        static byte[] EncodeDraftRecord(object record)
        {
            SetRecord(record, "Checksum", StoreMethod("ContentHash", record));
            return (byte[])StoreMethod("Encode", record);
        }
        static string LegacyDraftJson(WordboundCourseAsset asset, CourseDocument document) => (string)StoreMethod("DraftJson",
            new WordboundCourseDraftStore.PendingDraft { AssetGuid = DraftGuid(asset), SavedSourceHash = asset.SourceHash, Document = document });

        WordboundCourseAsset CreateDraftAsset()
        {
            var asset = ScriptableObject.CreateInstance<WordboundCourseAsset>();
            asset.Initialize(CourseTemplates.FreestyleWorld(), CourseTemplates.ProofCatalogue());
            AssetDatabase.CreateAsset(asset, folder + "/Draft.asset");
            return asset;
        }
        static T EditorField<T>(UnityEditor.Editor editor, string name) =>
            (T)typeof(WordboundCourseAssetEditor).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
        static void InvokeEditor(UnityEditor.Editor editor, string name, params object[] arguments)
        {
            try { typeof(WordboundCourseAssetEditor).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, arguments); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        [Test]
        public void SavedCourseProfilesPrefabAndMaterialsReloadIntoTheExactPreviewAndPlayContent()
        {
            var camera = ScriptableObject.CreateInstance<WordboundCameraProfile>();
            camera.FieldOfView = 67; camera.Distance = 4.5f; camera.Validate();
            AssetDatabase.CreateAsset(camera, folder + "/Camera.asset");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.IsNotNull(shader, "The project URP shader must be imported before this native fixture.");
            var material = new Material(shader) { color = new Color(.12f, .47f, .81f) };
            AssetDatabase.CreateAsset(material, folder + "/Blue.mat");
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject prefab;
            try
            {
                Object.DestroyImmediate(model.GetComponent<Collider>());
                model.GetComponent<Renderer>().sharedMaterial = material;
                var grip = new GameObject("Grip_R"); grip.transform.SetParent(model.transform, false);
                grip.transform.localPosition = new Vector3(.1f, 0, .1f);
                prefab = PrefabUtility.SaveAsPrefabAsset(model, folder + "/Model.prefab");
            }
            finally { Object.DestroyImmediate(model); }
            var visual = ScriptableObject.CreateInstance<WordboundVisualProfile>();
            visual.Overrides.Add(new WordboundVisualOverride { VisualKey = "STOOL", Model = prefab, Materials = new[] { material } });
            visual.Validate(); AssetDatabase.CreateAsset(visual, folder + "/Visual.asset");
            var course = ScriptableObject.CreateInstance<WordboundCourseAsset>();
            course.Initialize(CourseTemplates.FreestyleWorld(), CourseTemplates.ProofCatalogue());
            course.Items = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Config/items.json");
            course.RuleBook = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Config/rules.json");
            course.CameraProfile = camera; course.VisualProfile = visual; course.Seed = "0123456789abcdef";
            var document = course.ReadDocument(); document.Title = "Saved Inspector course"; course.SaveDocument(document);
            AssetDatabase.CreateAsset(course, folder + "/Course.asset");
            foreach (var saved in new Object[] { camera, material, visual, course }) { EditorUtility.SetDirty(saved); AssetDatabase.SaveAssetIfDirty(saved); }
            string inputHash = course.InputHash, sourceHash = course.SourceHash;
            var resolved = course.Resolve();
            string manifestJson = CourseJson.ExportManifest(resolved), manifestHash = resolved.Hash;
            string contentHash = course.CreateContent(resolved).CatalogueHash;
            Assert.That(new FileInfo(folder + "/Course.asset").Length, Is.GreaterThan(0));

            document.Title = "UNSAVED runtime mutation"; course.SaveDocument(document); camera.FieldOfView = 39;
            Resources.UnloadAsset(course); Resources.UnloadAsset(visual); Resources.UnloadAsset(camera);
            course = AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(folder + "/Course.asset");
            Assert.AreEqual("Saved Inspector course", course.ReadDocument().Title);
            Assert.AreEqual(sourceHash, course.SourceHash); Assert.AreEqual(inputHash, course.InputHash);
            Assert.AreEqual(67, course.CameraProfile.FieldOfView); Assert.AreEqual(4.5f, course.CameraProfile.Distance);
            Assert.AreEqual(folder + "/Visual.asset", AssetDatabase.GetAssetPath(course.VisualProfile));
            Assert.AreEqual(folder + "/Model.prefab", AssetDatabase.GetAssetPath(course.VisualProfile.Overrides.Single().Model));
            Assert.AreEqual(folder + "/Blue.mat", AssetDatabase.GetAssetPath(course.VisualProfile.Overrides.Single().Materials.Single()));
            var imported = CourseJson.ImportManifest(manifestJson, course.Catalogue());
            Assert.IsTrue(imported.Success, string.Join("\n", imported.Issues));
            var playContent = course.CreateContent(imported.Manifest);
            Assert.AreEqual(manifestHash, playContent.Course.Hash); Assert.AreEqual(contentHash, playContent.CatalogueHash);
            Assert.AreEqual(manifestHash, course.Resolve().Hash, "Reload must resolve the same saved seed and inputs.");

            var preview = ScriptableObject.CreateInstance<WordboundCoursePreview>();
            Component previewWorld = null;
            try
            {
                typeof(WordboundCoursePreview).GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(preview, new object[] { course, imported.Manifest });
                Assert.AreEqual("", typeof(WordboundCoursePreview).GetField("error", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(preview));
                previewWorld = (Component)typeof(WordboundCoursePreview).GetField("world", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(preview);
                Assert.IsNotNull(previewWorld, "The actual preview must construct the saved manifest's geometry.");
                Assert.AreEqual(manifestHash, previewWorld.GetType().GetProperty("ManifestHash").GetValue(previewWorld));
                Assert.That(previewWorld.GetComponentsInChildren<Collider>(true).Length, Is.GreaterThan(0));
            }
            finally { Object.DestroyImmediate(preview); }
            Assert.IsTrue(previewWorld == null, "Closing the preview must dispose its generated world.");

            var parent = new GameObject("Reloaded visual check");
            try
            {
                Assert.IsTrue(course.VisualProfile.TryInstantiate("STOOL", parent.transform, out var created));
                Assert.AreEqual(course.VisualProfile.Overrides[0].Materials[0], created.GetComponent<Renderer>().sharedMaterial);
                Assert.AreEqual(0, created.GetComponentsInChildren<Collider>(true).Length);
                Assert.IsNotNull(created.transform.Find("Grip_R"));
            }
            finally { Object.DestroyImmediate(parent); }
            var before = EditorSceneManager.GetSceneManagerSetup();
            Assert.Throws<ArgumentException>(() => WordboundCoursePlaytest.Start(course, imported.Manifest, new string('0', 64)));
            CollectionAssert.AreEqual(before.Select(s => s.path).ToArray(), EditorSceneManager.GetSceneManagerSetup().Select(s => s.path).ToArray());
            Assert.IsFalse(EditorApplication.isPlayingOrWillChangePlaymode);
            Assert.IsFalse(SessionState.GetBool(SessionPrefix + "active", false), "Rejected play setup must not claim the editor session.");
        }

        [Test]
        public void RestorationRetainsSavedSceneBesideUntitledAdditiveSceneAndOriginalActiveSelection()
        {
            var previous = EditorSceneManager.GetSceneManagerSetup();
            var originalScenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            var originalRoots = originalScenes.Select(scene => scene.isLoaded
                ? scene.GetRootGameObjects().OrderBy(root => root.transform.GetSiblingIndex()).ToArray()
                : Array.Empty<GameObject>()).ToArray();
            var clearDirtiness = typeof(EditorSceneManager).GetMethod("ClearSceneDirtiness", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(clearDirtiness, "The native fixture must be able to restore clean ambient scene flags.");
            foreach (var scene in originalScenes)
                Assert.IsFalse(scene.isDirty, "Run scene restoration checks from a clean editor setup.");
            string oldScenes = SessionState.GetString(SessionPrefix + "scenes", "");
            var parking = EditorSceneManager.NewPreviewScene();
            bool replacedSetup = false;
            try
            {
                foreach (var roots in originalRoots)
                    foreach (var root in roots) SceneManager.MoveGameObjectToScene(root, parking);
                replacedSetup = true;
                var saved = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                string path = folder + "/Saved.unity";
                Assert.IsTrue(EditorSceneManager.SaveScene(saved, path));
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(saved);
                string records = "{\"scenes\":[{\"path\":\"" + path + "\",\"loaded\":true,\"active\":true}," +
                    "{\"path\":\"\",\"loaded\":true,\"active\":false}]}";
                SessionState.SetString(SessionPrefix + "scenes", records);
                SessionState.SetBool(SessionPrefix + "active", true);
                InvokeRestore();
                Assert.AreEqual(2, SceneManager.sceneCount);
                Assert.AreEqual(path, SceneManager.GetSceneAt(0).path);
                Assert.AreEqual("", SceneManager.GetSceneAt(1).path);
                Assert.AreEqual(path, SceneManager.GetActiveScene().path);
                Assert.IsFalse(SessionState.GetBool(SessionPrefix + "active", false));
            }
            finally
            {
                try
                {
                    if (replacedSetup)
                    {
                        SessionState.SetString(SessionPrefix + "scenes", JsonUtility.ToJson(new SavedSetup {
                            scenes = previous.Select(s => new SavedScene { path = s.path, loaded = s.isLoaded, active = s.isActive }).ToArray() }));
                        SessionState.SetBool(SessionPrefix + "active", true);
                        InvokeRestore();
                    }
                    for (int i = 0; i < originalScenes.Length; i++)
                    {
                        var scene = replacedSetup ? SceneManager.GetSceneAt(i) : originalScenes[i];
                        if (!scene.isLoaded) continue;
                        if (replacedSetup)
                            foreach (var generated in scene.GetRootGameObjects()) Object.DestroyImmediate(generated);
                        for (int index = 0; index < originalRoots[i].Length; index++)
                        {
                            var root = originalRoots[i][index];
                            SceneManager.MoveGameObjectToScene(root, scene);
                            root.transform.SetSiblingIndex(index);
                        }
                    }
                    for (int i = 0; i < originalScenes.Length; i++)
                    {
                        var scene = replacedSetup ? SceneManager.GetSceneAt(i) : originalScenes[i];
                        if (!scene.isLoaded) continue;
                        clearDirtiness.Invoke(null, new object[] { scene });
                        CollectionAssert.AreEqual(originalRoots[i], scene.GetRootGameObjects().OrderBy(root => root.transform.GetSiblingIndex()).ToArray());
                        Assert.IsFalse(scene.isDirty);
                    }
                    CollectionAssert.AreEqual(previous.Select(s => s.path), EditorSceneManager.GetSceneManagerSetup().Select(s => s.path));
                    CollectionAssert.AreEqual(previous.Select(s => s.isLoaded), EditorSceneManager.GetSceneManagerSetup().Select(s => s.isLoaded));
                    CollectionAssert.AreEqual(previous.Select(s => s.isActive), EditorSceneManager.GetSceneManagerSetup().Select(s => s.isActive));
                }
                finally
                {
                    SessionState.SetString(SessionPrefix + "scenes", oldScenes);
                    SessionState.EraseBool(SessionPrefix + "active");
                    if (parking.IsValid() && parking.rootCount == 0) EditorSceneManager.ClosePreviewScene(parking);
                }
            }
        }

        [Test]
        public void MissingSavedSceneKeepsCurrentSetupAndRecoveryRecord()
        {
            var previous = EditorSceneManager.GetSceneManagerSetup();
            string oldScenes = SessionState.GetString(SessionPrefix + "scenes", "");
            string missing = "{\"scenes\":[{\"path\":\"" + folder + "/Missing.unity\",\"loaded\":true,\"active\":true}]}";
            try
            {
                SessionState.SetString(SessionPrefix + "scenes", missing);
                SessionState.SetBool(SessionPrefix + "active", true);
                Assert.Throws<InvalidOperationException>(InvokeRestore);
                CollectionAssert.AreEqual(previous.Select(s => s.path).ToArray(), EditorSceneManager.GetSceneManagerSetup().Select(s => s.path).ToArray());
                CollectionAssert.AreEqual(previous.Select(s => s.isActive).ToArray(), EditorSceneManager.GetSceneManagerSetup().Select(s => s.isActive).ToArray());
                Assert.AreEqual(missing, SessionState.GetString(SessionPrefix + "scenes", ""));
                Assert.IsTrue(SessionState.GetBool(SessionPrefix + "active", false));
            }
            finally
            {
                SessionState.SetString(SessionPrefix + "scenes", oldScenes);
                SessionState.EraseBool(SessionPrefix + "active");
            }
        }
        [Serializable] sealed class SavedSetup { public SavedScene[] scenes; }
        [Serializable] sealed class SavedScene { public string path; public bool loaded, active; }
        static void InvokeRestore()
        {
            try { typeof(WordboundCoursePlaytest).GetMethod("RestoreScenes", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
    }
}
