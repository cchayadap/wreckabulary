#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Art;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class AvatarExpressionCapture
    {
        const string Folder = "BuildNotes/character-experience-20261026/emotion/capture-01";
        const string ComparisonFolder = "BuildNotes/character-experience-20261026/emotion/capture-02";
        const string TunedFolder = "BuildNotes/character-experience-20261026/emotion/capture-03";
        [Serializable] sealed class FileProof { public string path, before, after; }
        [Serializable] sealed class Frame
        {
            public string state, view, clip, image, sha256;
            public float clipTime, blink, brow, orthographicSize, targetBrow, targetBlink;
            public string[] shapeBindings;
            public Vector3 cameraPosition, cameraEuler;
            public bool ownedObjectsReleased;
        }
        [Serializable] sealed class Report
        {
            public string status = "IN PROGRESS", error = "", utc, unity;
            public string temporaryCleanupError = "";
            public bool filesPreserved, scenesPreserved, globalsPreserved, profilePreserved, temporaryProfileReleased;
            public List<FileProof> inputs = new List<FileProof>();
            public List<Frame> frames = new List<Frame>();
        }

        public static void Run() => RunCapture(Folder, false);
        public static void RunBrowComparison() => RunCapture(ComparisonFolder, true);
        public static void RunTunedProfile() => RunCapture(TunedFolder, false);

        static void RunCapture(string folder, bool compareBrows)
        {
            if (Directory.Exists(folder)) throw new IOException("Preserve the existing expression capture; choose a reviewed new run path.");
            var library = ModelLibrary.Load(); var source = library ? library.Find("Avatar/Avatar") : null;
            var profile = AssetDatabase.LoadAssetAtPath<AvatarExpressionProfile>(AvatarExpressionSetup.Path);
            var surface = AvatarSurfaceCorrectionSetup.VerifyExistingProfile(true);
            if (!source || !profile || !surface) throw new InvalidOperationException("Saved avatar, expression and surface profiles are required.");
            profile.Validate();
            string profileJson = EditorJsonUtility.ToJson(profile);
            AvatarExpressionProfile temporary = null;
            Exception runFailure = null;
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion };
            string[] explicitInputs = { AssetDatabase.GetAssetPath(source), AssetDatabase.GetAssetPath(library), AvatarExpressionSetup.Path,
                AvatarSurfaceCorrectionSetup.ProfilePath, AvatarSurfaceCorrectionSetup.MeshPath,
                "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset" };
            foreach (var path in explicitInputs) if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new IOException("Required input missing: " + path);
            var paths = AssetDatabase.GetDependencies(explicitInputs, true).Concat(explicitInputs).Distinct(StringComparer.Ordinal).ToArray();
            foreach (var path in paths)
            {
                if (!File.Exists(path)) { if (path.StartsWith("Assets/", StringComparison.Ordinal)) throw new IOException("Missing saved dependency: " + path); continue; }
                report.inputs.Add(new FileProof { path = path, before = Hash(path) });
                if (File.Exists(path + ".meta")) report.inputs.Add(new FileProof { path = path + ".meta", before = Hash(path + ".meta") });
            }
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var active = SceneManager.GetActiveScene(); bool activeDirty = active.isDirty;
            var renderTarget = RenderTexture.active; bool ssao = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");
            var meshes = source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s => s.sharedMesh)
                .Concat(new[] { surface.CorrectedMesh }).Where(m => m).Distinct().ToArray();
            var meshHashes = meshes.Select(m => AvatarSurfaceMeshData.Fingerprint(m)).ToArray();
            var materials = source.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m).Distinct().ToArray();
            if (materials.Any(m => !m.shader || !m.shader.isSupported || m.shader.name.Contains("InternalErrorShader")))
                throw new InvalidOperationException("Original material shader unavailable.");
            var materialJson = materials.Select(EditorJsonUtility.ToJson).ToArray();
            Directory.CreateDirectory(folder);
            try
            {
                float size = 0;
                if (compareBrows)
                {
                    temporary = ScriptableObject.CreateInstance<AvatarExpressionProfile>();
                    EditorJsonUtility.FromJsonOverwrite(profileJson, temporary);
                    temporary.hideFlags = HideFlags.HideAndDontSave;
                    temporary.BlendSeconds = .02f;
                    foreach (float brow in new[] { 55f, 75f, 95f })
                    {
                        for (int i = 0; i < temporary.Poses.Length; i++)
                            if (temporary.Poses[i].State == AvatarExpression.Idle)
                                temporary.Poses[i] = new AvatarExpressionPose(AvatarExpression.Idle, brow, 0, false);
                        temporary.Validate();
                        Vector3? center = report.frames.Count == 0 ? (Vector3?)null : report.frames[0].cameraPosition - Vector3.back * 5;
                        report.frames.Add(Capture(source, library, temporary, AvatarExpression.Idle, "front", ref size,
                            folder, "idle-brow" + ((int)brow).ToString(), center));
                    }
                    foreach (var pose in new[] { new Vector2(4, 38), new Vector2(18, 60) })
                    {
                        for (int i = 0; i < temporary.Poses.Length; i++)
                            if (temporary.Poses[i].State == AvatarExpression.Hurt)
                                temporary.Poses[i] = new AvatarExpressionPose(AvatarExpression.Hurt, pose.x, pose.y, false);
                        temporary.Validate();
                        Vector3 center = report.frames[0].cameraPosition - Vector3.back * 5;
                        report.frames.Add(Capture(source, library, temporary, AvatarExpression.Hurt, "front", ref size,
                            folder, "hurt-brow" + ((int)pose.x).ToString() + "-blink" + ((int)pose.y).ToString(), center));
                    }
                }
                else
                    foreach (string view in new[] { "front", "three-quarter" })
                        foreach (var state in new[] { AvatarExpression.Idle, AvatarExpression.Focus, AvatarExpression.Hurt, AvatarExpression.Downed, AvatarExpression.Recover })
                            report.frames.Add(Capture(source, library, profile, state, view, ref size, folder));
                report.status = "CAPTURED; HUMAN FACIAL REVIEW AND RUNTIME TRANSITIONS PENDING";
            }
            catch (Exception error) { runFailure = error; report.status = "FAIL"; report.error = error.ToString(); throw; }
            finally
            {
                var cleanupErrors = new List<Exception>();
                Cleanup(() => { if (temporary) Object.DestroyImmediate(temporary); }, cleanupErrors);
                report.temporaryCleanupError = string.Join("\n", cleanupErrors.Select(error => error.ToString()));
                report.temporaryProfileReleased = !temporary;
                report.profilePreserved = EditorJsonUtility.ToJson(profile) == profileJson;
                foreach (var proof in report.inputs) proof.after = File.Exists(proof.path) ? Hash(proof.path) : "MISSING";
                report.filesPreserved = report.inputs.All(p => p.before == p.after) &&
                    meshes.Select(m => AvatarSurfaceMeshData.Fingerprint(m)).SequenceEqual(meshHashes) &&
                    materials.Select(EditorJsonUtility.ToJson).SequenceEqual(materialJson);
                var after = EditorSceneManager.GetSceneManagerSetup();
                report.scenesPreserved = setup.Length == after.Length && setup.Select(s => s.path + ":" + s.isLoaded + ":" + s.isActive)
                    .SequenceEqual(after.Select(s => s.path + ":" + s.isLoaded + ":" + s.isActive)) &&
                    SceneManager.GetActiveScene() == active && active.isDirty == activeDirty;
                report.globalsPreserved = RenderTexture.active == renderTarget && Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION") == ssao;
                if (cleanupErrors.Count > 0 || !report.filesPreserved || !report.scenesPreserved || !report.globalsPreserved || !report.profilePreserved || !report.temporaryProfileReleased) report.status = "FAIL: preservation";
                File.WriteAllText(folder + "/receipt.json", JsonUtility.ToJson(report, true));
                if (cleanupErrors.Count > 0) { if (runFailure != null) cleanupErrors.Insert(0, runFailure); throw new AggregateException("Temporary expression profile cleanup failed.", cleanupErrors); }
                if (!report.filesPreserved || !report.scenesPreserved || !report.globalsPreserved || !report.profilePreserved || !report.temporaryProfileReleased) throw new InvalidOperationException("Expression capture changed protected input/state.");
            }
        }

        static Frame Capture(GameObject source, ModelLibrary library, AvatarExpressionProfile profile, AvatarExpression state, string view, ref float scale,
            string folder, string label = null, Vector3? centerOverride = null)
        {
            PreviewRenderUtility preview = null; Texture2D image = null; Mesh baked = null; GameObject clone = null;
            AvatarExpressionDriver driver = null; Scene scene = default; bool begun = false;
            Camera ownedCamera = null; Light[] ownedLights = null; Frame result = null; Exception failure = null;
            var active = RenderTexture.active; bool ssao = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");
            try
            {
                preview = new PreviewRenderUtility(); scene = preview.camera.gameObject.scene;
                ownedCamera = preview.camera; ownedLights = preview.lights.ToArray();
                if (!EditorSceneManager.IsPreviewScene(scene)) throw new InvalidOperationException("Expected owned preview scene.");
                clone = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
                AvatarSurfaceProfile.ApplyDefault("Avatar/Avatar", source, clone);
                var skins = clone.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                PlayerAppearance.Dress(skins, PlayerAppearance.DefaultPresentationOutfit());
                var head = skins.Single(s => s.name == "SK_Head");
                string clipName = state == AvatarExpression.Focus ? "Swing_OneHand" : state == AvatarExpression.Hurt || state == AvatarExpression.Downed ? "Hit_Reaction" : "Idle";
                var clip = library.FindClip("Avatar/Avatar", clipName);
                if (!clip) throw new InvalidOperationException("Actual avatar clip missing: " + clipName);
                float clipTime = state == AvatarExpression.Downed ? clip.length * .95f : clip.length * .25f;
                clip.SampleAnimation(clone, clipTime);
                clone.transform.rotation = Quaternion.Euler(0, view == "front" ? 180 : 145, 0);
                driver = new AvatarExpressionDriver(clone, profile);
                if (!driver.IsSupported) throw new InvalidOperationException("Actual avatar must expose exact Blink and BrowRelax channels.");
                var sample = new AvatarExpressionSample { Available = true, Health = 100, Epoch = 0, Tick = 1 };
                if (state == AvatarExpression.Recover) sample.Downed = true;
                driver.Observe(sample); driver.Step(.6f);
                sample.Tick = 2; sample.Focus = state == AvatarExpression.Focus; sample.Hurt = state == AvatarExpression.Hurt;
                sample.Downed = state == AvatarExpression.Downed; driver.Observe(sample); driver.Step(state == AvatarExpression.Recover ? .25f : .35f);
                baked = new Mesh(); head.BakeMesh(baked);
                var bounds = baked.bounds; Vector3 center = head.transform.TransformPoint(bounds.center);
                if (centerOverride.HasValue) center = centerOverride.Value;
                float radius = Vector3.Scale(bounds.extents, head.transform.lossyScale).magnitude;
                if (scale == 0) scale = radius * 1.25f;
                if (radius <= 0 || float.IsNaN(radius) || float.IsInfinity(radius) || scale <= 0 || float.IsNaN(scale) || float.IsInfinity(scale) ||
                    !AvatarSurfaceMeshData.Finite(center) || !AvatarSurfaceMeshData.Finite(bounds.size))
                    throw new InvalidOperationException("Invalid head crop.");
                var camera = preview.camera; camera.orthographic = true; camera.orthographicSize = scale;
                camera.nearClipPlane = .001f; camera.farClipPlane = 30; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.97f, .94f, .85f, 1); camera.allowHDR = false;
                camera.transform.position = center + Vector3.back * 5; camera.transform.LookAt(center);
                preview.ambientColor = new Color(.45f, .45f, .45f);
                preview.lights[0].color = preview.lights[1].color = Color.white;
                preview.lights[0].intensity = .7f; preview.lights[1].intensity = .35f;
                preview.lights[0].transform.rotation = camera.transform.rotation * Quaternion.Euler(25, -25, 0);
                preview.lights[1].transform.rotation = camera.transform.rotation * Quaternion.Euler(10, 35, 0);
                preview.BeginStaticPreview(new Rect(0, 0, 512, 512)); begun = true;
                preview.Render(true); preview.Render(true); image = preview.EndStaticPreview(); begun = false;
                if (!image || image.width != 512 || image.height != 512) throw new InvalidOperationException("Native image missing or wrong dimensions.");
                if (image.GetPixels32().Select(c => c.r << 16 | c.g << 8 | c.b).Distinct().Take(128).Count() < 64)
                    throw new InvalidOperationException("Native face capture has insufficient image variation.");
                string path = folder + "/" + (label ?? state.ToString().ToLowerInvariant()) + "-" + view + ".png";
                using (var file = new FileStream(path, FileMode.CreateNew)) { var bytes = image.EncodeToPNG(); file.Write(bytes, 0, bytes.Length); }
                result = new Frame { state = label ?? state.ToString(), view = view, clip = clipName, clipTime = clipTime,
                    image = path, sha256 = Hash(path), orthographicSize = scale,
                    targetBrow = profile.Get(state).BrowRelax, targetBlink = profile.Get(state).Blink,
                    blink = head.GetBlendShapeWeight(head.sharedMesh.GetBlendShapeIndex("Blink")),
                    brow = head.GetBlendShapeWeight(head.sharedMesh.GetBlendShapeIndex("BrowRelax")),
                    shapeBindings = AnimationUtility.GetCurveBindings(clip).Where(b => b.propertyName.StartsWith("blendShape.", StringComparison.Ordinal))
                        .Select(b => b.path + ":" + b.propertyName).ToArray(),
                    cameraPosition = camera.transform.position, cameraEuler = camera.transform.eulerAngles };
                return result;
            }
            catch (Exception error) { failure = error; throw; }
            finally
            {
                var errors = new List<Exception>();
                Cleanup(() => driver?.Dispose(), errors);
                Cleanup(() => { if (begun) { var pending = preview.EndStaticPreview(); if (pending) Object.DestroyImmediate(pending); } }, errors);
                Cleanup(() => { if (image) Object.DestroyImmediate(image); }, errors);
                Cleanup(() => { if (baked) Object.DestroyImmediate(baked); }, errors);
                Cleanup(() => { if (clone) Object.DestroyImmediate(clone); }, errors);
                Cleanup(() => preview?.Cleanup(), errors);
                Cleanup(() => { if (scene.IsValid() && scene.isLoaded && EditorSceneManager.IsPreviewScene(scene)) EditorSceneManager.ClosePreviewScene(scene); }, errors);
                Cleanup(() => RenderTexture.active = active, errors);
                Cleanup(() => { if (ssao) Shader.EnableKeyword("_SCREEN_SPACE_OCCLUSION"); else Shader.DisableKeyword("_SCREEN_SPACE_OCCLUSION"); }, errors);
                bool released = !image && !baked && !clone && !ownedCamera && (ownedLights == null || ownedLights.All(light => !light)) && (!scene.IsValid() || !scene.isLoaded);
                if (result != null) result.ownedObjectsReleased = released;
                if (!released) errors.Add(new InvalidOperationException("Owned expression preview objects/scene remain after cleanup."));
                if (errors.Count > 0) { if (failure != null) errors.Insert(0, failure); throw new AggregateException("Expression preview cleanup failed.", errors); }
            }
        }

        static void Cleanup(Action action, List<Exception> errors) { try { action(); } catch (Exception error) { errors.Add(error); } }
        static string Hash(string path) => AvatarSurfaceMeshData.Hash(File.ReadAllBytes(path));
    }
}
#endif
