using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Art;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class AvatarSurfaceCorrectionSetup
    {
        public const string AvatarPath = "Assets/_Project/Art/Imported/Avatar/Avatar.fbx";
        public const string MeshPath = "Assets/_Project/Art/Corrections/Avatar/SK_Head_Surface.asset";
        public const string ProfilePath = "Assets/_Project/Resources/Art/AvatarSurfaceCorrection.asset";
        public const string ExpectedSource = "997ebb6a6441b01f9a673683405a7b99678b6c272099068ed786a134d5cfe65f";
        const string ExpectedFbx = "f1199f15a333de765e49aa4b6773a1c71c3347c9ca4a5169c93ab54190a1c6c3";
        const string ExpectedMeta = "5019b95427453cefa10c0ccabdf5ff6bb77435948908132120ef5fc20cef9417";

        sealed class Frame
        {
            public string name;
            public float weight;
            public Vector3[] delta;
        }

        [MenuItem("Wreckabulary/Art/Create or Verify Avatar Surface Correction")]
        public static void CreateOrVerify()
        {
            var profile = EnsureDefaults();
            Selection.activeObject = profile; EditorGUIUtility.PingObject(profile);
        }

        public static AvatarSurfaceProfile EnsureDefaults()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play Mode.");
            var library = ModelLibrary.Load();
            var avatar = library ? library.Find("Avatar/Avatar") : null;
            if (!avatar || AssetDatabase.GetAssetPath(avatar) != AvatarPath)
                throw new InvalidOperationException("Expected the original imported avatar entry; no library rewrite is performed.");
            var head = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "SK_Head");
            var source = head.sharedMesh;
            ValidateSource(source);
            string before = AvatarSurfaceMeshData.Fingerprint(source);
            bool dirty = EditorUtility.IsDirty(source);
            Mesh createdMesh = null; AvatarSurfaceProfile createdProfile = null;
            try
            {
                var profileObject = AssetDatabase.LoadMainAssetAtPath(ProfilePath);
                var meshObject = AssetDatabase.LoadMainAssetAtPath(MeshPath);
                if (profileObject && profileObject is not AvatarSurfaceProfile) throw new IOException("Profile path is occupied.");
                if (meshObject && meshObject is not Mesh) throw new IOException("Mesh path is occupied.");
                var profile = profileObject as AvatarSurfaceProfile;
                var corrected = meshObject as Mesh;
                string path = AnimationUtility.CalculateTransformPath(head.transform, avatar.transform);
                if (profile)
                {
                    if (!profile.TryValidate(out string reason) || profile.SourceAvatar != avatar || profile.SourceMesh != source
                        || profile.CorrectedMesh != corrected || profile.RendererPath != path || profile.SourceFingerprint != ExpectedSource)
                        throw new InvalidOperationException("Preserve the existing authored profile and inspect its references: " + reason);
                    ValidateCorrected(source, corrected);
                    return profile;
                }
                RequireVacant(ProfilePath);
                if (!corrected)
                {
                    RequireVacant(MeshPath);
                    createdMesh = CreateCorrectedMesh(source);
                    createdMesh.name = "SK_Head Surface";
                    EnsureFolder("Assets/_Project/Art/Corrections/Avatar");
                    RequireVacant(MeshPath);
                    AssetDatabase.CreateAsset(createdMesh, MeshPath);
                    AssetDatabase.SaveAssetIfDirty(createdMesh);
                    corrected = createdMesh;
                }
                else ValidateCorrected(source, corrected);
                EnsureFolder("Assets/_Project/Resources/Art");
                createdProfile = ScriptableObject.CreateInstance<AvatarSurfaceProfile>();
                createdProfile.name = "Avatar Surface Correction";
                createdProfile.Initialize(avatar, source, corrected, path, ExpectedSource);
                if (!createdProfile.TryValidate(out string invalid)) throw new InvalidOperationException(invalid);
                RequireVacant(ProfilePath);
                AssetDatabase.CreateAsset(createdProfile, ProfilePath);
                AssetDatabase.SaveAssetIfDirty(createdProfile);
                return createdProfile;
            }
            finally
            {
                if (createdMesh && !EditorUtility.IsPersistent(createdMesh)) Object.DestroyImmediate(createdMesh);
                if (createdProfile && !EditorUtility.IsPersistent(createdProfile)) Object.DestroyImmediate(createdProfile);
                if (AvatarSurfaceMeshData.Fingerprint(source) != before || EditorUtility.IsDirty(source) != dirty)
                    throw new InvalidOperationException("Original source memory changed; no automatic repair attempted.");
                ValidateSource(source);
            }
        }

        public static AvatarSurfaceProfile VerifyExistingProfile(bool required = false)
        {
            var stored = AssetDatabase.LoadMainAssetAtPath(ProfilePath);
            if (!stored)
            {
                if (required || File.Exists(ProfilePath) || File.Exists(ProfilePath + ".meta"))
                    throw new InvalidOperationException("Create or repair the saved avatar surface profile before this operation.");
                return null;
            }
            if (stored is not AvatarSurfaceProfile profile) throw new InvalidOperationException("Invalid saved avatar surface profile.");
            return VerifyProfile(profile, required);
        }

        public static AvatarSurfaceProfile VerifyProfile(AvatarSurfaceProfile profile, bool required = false)
        {
            if (!profile) throw new InvalidOperationException("Avatar surface profile is missing.");
            if (!profile.EnabledCorrection)
            {
                if (required) throw new InvalidOperationException("This operation requires the saved avatar surface correction to be enabled.");
                return null;
            }
            if (!profile.TryValidate(out _)) throw new InvalidOperationException("Invalid saved avatar surface profile.");
            var library = ModelLibrary.Load();
            var avatar = library ? library.Find("Avatar/Avatar") : null;
            if (!avatar || AssetDatabase.GetAssetPath(avatar) != AvatarPath || profile.SourceAvatar != avatar
                || profile.SourceFingerprint != ExpectedSource
                || AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath) != profile.CorrectedMesh)
                throw new InvalidOperationException("Avatar correction references no longer match the reviewed saved assets.");
            var heads = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(renderer => renderer.name == "SK_Head").ToArray();
            if (heads.Length != 1 || heads[0].sharedMesh != profile.SourceMesh
                || AnimationUtility.CalculateTransformPath(heads[0].transform, avatar.transform) != profile.RendererPath)
                throw new InvalidOperationException("The exact original avatar head binding changed.");
            ValidateSource(profile.SourceMesh);
            ValidateCorrected(profile.SourceMesh, profile.CorrectedMesh);
            return profile;
        }

        public static void RequireReviewedMeshContent(Mesh mesh)
        {
            if (!mesh || AvatarSurfaceMeshData.Fingerprint(mesh) != ExpectedSource)
                throw new InvalidOperationException("Avatar source content changed, even if its GUID and vertex counts match. Review a new correction version.");
        }

        public static void ValidateSource(Mesh source)
        {
            if (!source || !EditorUtility.IsPersistent(source) || AssetDatabase.GetAssetPath(source) != AvatarPath
                || source.name != "SK_Head" || source.vertexCount != 6630 || source.subMeshCount != 3)
                throw new InvalidOperationException("This correction is bound to the reviewed original SK_Head only.");
            if (AvatarSurfaceMeshData.Hash(File.ReadAllBytes(AvatarPath)) != ExpectedFbx
                || AvatarSurfaceMeshData.Hash(File.ReadAllBytes(AvatarPath + ".meta")) != ExpectedMeta)
                throw new InvalidOperationException("Avatar source changed; preserve it and review a new correction version.");
            RequireReviewedMeshContent(source);
            var face = Measure(source, 0);
            if (face.invalid != 0 || face.zero != 0 || face.opposed != face.samples || face.meanDot > -.97)
                throw new InvalidOperationException("The reviewed face-normal inversion is no longer present; do not apply this recipe.");
        }

        public static Mesh CreateCorrectedMesh(Mesh source)
        {
            ValidateSource(source);
            var frames = ReadFrames(source);
            Mesh corrected = null, frameMesh = null;
            try
            {
                corrected = Object.Instantiate(source);
                corrected.ClearBlendShapes();
                corrected.RecalculateNormals(); corrected.RecalculateTangents();
                RequireSurfaceArrays(corrected);
                var baseVertices = source.vertices;
                var baseNormals = corrected.normals;
                var baseTangents = corrected.tangents;
                frameMesh = Object.Instantiate(corrected);
                foreach (var frame in frames)
                {
                    var vertices = new Vector3[baseVertices.Length];
                    for (int i = 0; i < vertices.Length; i++) vertices[i] = baseVertices[i] + frame.delta[i];
                    frameMesh.vertices = vertices;
                    frameMesh.RecalculateNormals(); frameMesh.RecalculateTangents();
                    RequireSurfaceArrays(frameMesh);
                    var normals = frameMesh.normals; var tangents = frameMesh.tangents;
                    var deltaNormals = new Vector3[vertices.Length]; var deltaTangents = new Vector3[vertices.Length];
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        if (Mathf.Sign(baseTangents[i].w) != Mathf.Sign(tangents[i].w))
                            throw new InvalidOperationException("Blendshape changes tangent handedness at " + frame.name + "/" + frame.weight + "/" + i
                                + "; a three-component tangent delta cannot represent this safely.");
                        deltaNormals[i] = normals[i] - baseNormals[i];
                        deltaTangents[i] = new Vector3(tangents[i].x - baseTangents[i].x,
                            tangents[i].y - baseTangents[i].y, tangents[i].z - baseTangents[i].z);
                    }
                    corrected.AddBlendShapeFrame(frame.name, frame.weight, frame.delta, deltaNormals, deltaTangents);
                }
                ValidateCorrected(source, corrected);
                var result = corrected; corrected = null; return result;
            }
            finally
            {
                if (frameMesh) Object.DestroyImmediate(frameMesh);
                if (corrected) Object.DestroyImmediate(corrected);
            }
        }

        static List<Frame> ReadFrames(Mesh source)
        {
            var frames = new List<Frame>();
            for (int shape = 0; shape < source.blendShapeCount; shape++)
                for (int frame = 0; frame < source.GetBlendShapeFrameCount(shape); frame++)
                {
                    var delta = new Vector3[source.vertexCount];
                    source.GetBlendShapeFrameVertices(shape, frame, delta, null, null);
                    frames.Add(new Frame { name = source.GetBlendShapeName(shape), weight = source.GetBlendShapeFrameWeight(shape, frame), delta = delta });
                }
            return frames;
        }

        public static void ValidateCorrected(Mesh source, Mesh corrected)
        {
            if (!corrected || source == corrected || AvatarSurfaceMeshData.Fingerprint(source, false) != AvatarSurfaceMeshData.Fingerprint(corrected, false))
                throw new InvalidOperationException("Correction changed geometry, UVs, skinning, shape positions or other protected values.");
            RequireSurfaceArrays(corrected);
            var face = Measure(corrected, 0);
            if (face.invalid != 0 || face.zero != 0 || face.opposed != 0 || face.meanDot < .97)
                throw new InvalidOperationException("Corrected face normals do not follow the outward face geometry.");
        }

        static void RequireSurfaceArrays(Mesh mesh)
        {
            var normals = mesh.normals; var tangents = mesh.tangents;
            if (normals.Length != mesh.vertexCount || tangents.Length != mesh.vertexCount || mesh.uv.Length != mesh.vertexCount)
                throw new InvalidOperationException("Expected complete UV0, normals and tangents.");
            for (int i = 0; i < mesh.vertexCount; i++)
            {
                var tangent = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                if (!AvatarSurfaceMeshData.Finite(normals[i]) || !AvatarSurfaceMeshData.Finite(tangent)
                    || normals[i].sqrMagnitude < .5f || tangent.sqrMagnitude < .5f
                    || float.IsNaN(tangents[i].w) || float.IsInfinity(tangents[i].w) || Mathf.Abs(tangents[i].w) < .9f)
                    throw new InvalidOperationException("Invalid generated surface basis at " + i);
            }
        }

        public readonly struct SurfaceMeasure
        {
            public readonly int samples, opposed, invalid, zero;
            public readonly double meanDot;
            public SurfaceMeasure(int count, int negative, int bad, int tiny, double mean)
            { samples = count; opposed = negative; invalid = bad; zero = tiny; meanDot = mean; }
        }

        public static SurfaceMeasure Measure(Mesh mesh, int submesh)
        {
            if (mesh.GetTopology(submesh) != MeshTopology.Triangles) throw new InvalidOperationException("Expected triangle topology.");
            var vertices = mesh.vertices; var normals = mesh.normals; var indices = mesh.GetIndices(submesh);
            if (normals.Length != vertices.Length || indices.Length % 3 != 0) throw new InvalidOperationException("Incomplete mesh data.");
            int samples = 0, opposed = 0, invalid = 0, zero = 0; double weighted = 0, total = 0;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                Vector3 geometric = Vector3.Cross(b - a, c - a); float area2 = geometric.magnitude;
                if (!AvatarSurfaceMeshData.Finite(geometric) || float.IsInfinity(area2)) { invalid++; continue; }
                if (area2 < 1e-12f) { zero++; continue; }
                geometric /= area2;
                for (int corner = 0; corner < 3; corner++)
                {
                    samples++; var normal = normals[indices[i + corner]];
                    if (!AvatarSurfaceMeshData.Finite(normal)) { invalid++; continue; }
                    if (normal.sqrMagnitude < 1e-10f) { zero++; continue; }
                    float dot = Vector3.Dot(geometric, normal.normalized);
                    if (dot < -.05f) opposed++;
                    weighted += dot * area2; total += area2;
                }
            }
            return new SurfaceMeasure(samples, opposed, invalid, zero, total > 0 ? weighted / total : 0);
        }

        [MenuItem("Wreckabulary/Art/Apply Avatar Surface Correction to Selected Instance")]
        public static void ApplyToSelectedInstance()
        {
            var instance = Selection.activeGameObject;
            if (!instance || EditorUtility.IsPersistent(instance) || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Select a scene or prefab-stage avatar root outside Play Mode.");
            var profile = EnsureDefaults();
            var target = instance.transform.Find(profile.RendererPath);
            var skin = target ? target.GetComponent<SkinnedMeshRenderer>() : null;
            if (!skin || skin.sharedMesh != profile.SourceMesh) throw new InvalidOperationException("Selected root does not use the bound original head.");
            Undo.RecordObject(skin, "Apply saved avatar surface correction");
            if (!profile.TryApply(profile.SourceAvatar, instance)) throw new InvalidOperationException("Correction is disabled or no longer matches.");
            PrefabUtility.RecordPrefabInstancePropertyModifications(skin);
            EditorUtility.SetDirty(skin); EditorSceneManager.MarkSceneDirty(instance.scene);
        }

        static void RequireVacant(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) || File.Exists(path) || File.Exists(path + ".meta") || Directory.Exists(path))
                throw new IOException("Preserve occupied asset path: " + path);
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            if (File.Exists(folder) || File.Exists(folder + ".meta") || Directory.Exists(folder)) throw new IOException("Unrecognized folder path: " + folder);
            int slash = folder.LastIndexOf('/');
            if (slash < 0) throw new IOException("Assets folder unavailable.");
            EnsureFolder(folder.Substring(0, slash));
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1))))
                throw new IOException("Could not create folder: " + folder);
        }
    }

    [CustomEditor(typeof(AvatarSurfaceProfile))]
    public sealed class AvatarSurfaceProfileEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (!((AvatarSurfaceProfile)target).TryValidate(out string reason)) EditorGUILayout.HelpBox(reason, MessageType.Error);
            EditorGUILayout.HelpBox("One saved head mesh is shared by new avatars. Original FBX, clips and materials remain the source. Existing scene avatars can use Art > Apply Avatar Surface Correction to Selected Instance; save that scene or prefab afterward.", MessageType.Info);
        }
    }
}
