using System;
using UnityEngine;

namespace Wreckabulary.Art
{
    [CreateAssetMenu(menuName = "Wreckabulary/Art/Avatar Surface Correction", fileName = "AvatarSurfaceCorrection")]
    public sealed class AvatarSurfaceProfile : ScriptableObject
    {
        public const string ResourcePath = "Art/AvatarSurfaceCorrection";
        [SerializeField] bool enabledCorrection = true;
        [SerializeField] GameObject sourceAvatar;
        [SerializeField] Mesh sourceMesh, correctedMesh;
        [SerializeField] string rendererPath = "SK_Head";
        [SerializeField, TextArea] string sourceFingerprint;
        static AvatarSurfaceProfile loaded;

        public GameObject SourceAvatar => sourceAvatar;
        public Mesh SourceMesh => sourceMesh;
        public Mesh CorrectedMesh => correctedMesh;
        public string RendererPath => rendererPath;
        public string SourceFingerprint => sourceFingerprint;

        public static AvatarSurfaceProfile Load()
        {
            if (!loaded) loaded = Resources.Load<AvatarSurfaceProfile>(ResourcePath);
            return loaded;
        }

        public static bool ApplyDefault(string key, GameObject source, GameObject instance)
        {
            if (key != "Avatar/Avatar") return false;
            var profile = Load();
            return profile && profile.TryApply(source, instance);
        }

        public void Initialize(GameObject avatar, Mesh original, Mesh corrected, string path, string fingerprint)
        {
            sourceAvatar = avatar; sourceMesh = original; correctedMesh = corrected;
            rendererPath = path; sourceFingerprint = fingerprint;
        }

        public bool TryValidate(out string reason)
        {
            reason = "";
            if (!sourceAvatar || !sourceMesh || !correctedMesh || sourceMesh == correctedMesh)
                reason = "Assign the original avatar, original head and a separate corrected mesh.";
            else if (string.IsNullOrEmpty(rendererPath) || rendererPath.StartsWith("/", StringComparison.Ordinal)
                || rendererPath.Contains("..") || string.IsNullOrEmpty(sourceFingerprint))
                reason = "The exact renderer path and source fingerprint are required.";
            else if (sourceMesh.vertexCount != correctedMesh.vertexCount || sourceMesh.subMeshCount != correctedMesh.subMeshCount
                || sourceMesh.blendShapeCount != correctedMesh.blendShapeCount)
                reason = "The correction must preserve vertex, submesh and blendshape counts.";
            if (reason.Length == 0)
            {
                var bound = sourceAvatar.transform.Find(rendererPath);
                var renderer = bound ? bound.GetComponent<SkinnedMeshRenderer>() : null;
                if (!renderer || renderer.sharedMesh != sourceMesh) reason = "The source renderer no longer matches the bound original mesh.";
                for (int i = 0; reason.Length == 0 && i < sourceMesh.blendShapeCount; i++)
                    if (sourceMesh.GetBlendShapeName(i) != correctedMesh.GetBlendShapeName(i)
                        || sourceMesh.GetBlendShapeFrameCount(i) != correctedMesh.GetBlendShapeFrameCount(i))
                        reason = "Blendshape names, ordering and frame counts must match the source.";
            }
            return reason.Length == 0;
        }

        public bool TryApply(GameObject source, GameObject instance)
        {
            if (!enabledCorrection || !source || !instance || source != sourceAvatar || instance == sourceAvatar) return false;
            if (!TryValidate(out string reason)) throw new InvalidOperationException("Avatar surface correction: " + reason);
            var target = instance.transform.Find(rendererPath);
            var skin = target ? target.GetComponent<SkinnedMeshRenderer>() : null;
            if (!skin || skin.sharedMesh != sourceMesh) return false;
            var weights = new float[sourceMesh.blendShapeCount];
            for (int i = 0; i < weights.Length; i++) weights[i] = skin.GetBlendShapeWeight(i);
            var bounds = skin.localBounds;
            skin.sharedMesh = correctedMesh;
            for (int i = 0; i < weights.Length; i++) skin.SetBlendShapeWeight(i, weights[i]);
            skin.localBounds = bounds;
            return true;
        }

        public bool MatchesOnlyBoundHeadReplacement(GameObject source, GameObject instance, Mesh[] expected, Mesh[] actual)
        {
            if (!source || !instance || source != sourceAvatar || !TryValidate(out _)
                || expected == null || actual == null || expected.Length != actual.Length) return false;
            var target = instance.transform.Find(rendererPath);
            var skin = target ? target.GetComponent<SkinnedMeshRenderer>() : null;
            if (!skin || skin.sharedMesh != correctedMesh) return false;
            int replacements = 0;
            for (int i = 0; i < expected.Length; i++)
            {
                if (!expected[i] || !actual[i]) return false;
                if (expected[i] == actual[i]) continue;
                if (expected[i] != sourceMesh || actual[i] != correctedMesh || ++replacements > 1) return false;
            }
            return replacements == 1;
        }
    }
}
