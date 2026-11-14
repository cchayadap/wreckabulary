#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;

namespace Wreckabulary.EditorTools
{
    public static class AvatarExpressionSetup
    {
        public const string Path = "Assets/_Project/Resources/Art/AvatarExpressions.asset";
        [Serializable] sealed class TuningReceipt
        {
            public string status = "IN PROGRESS", error = "", utc, profile = Path;
            public string oldJson = "", newJson = "", beforeHash, afterHash, metaBeforeHash, metaAfterHash;
            public string oldJsonHash, newJsonHash;
            public bool saveAttempted, reloadedVerified, idempotent, metaPreserved;
        }
        [MenuItem("Wreckabulary/Art/Create Missing Avatar Expression Profile")]
        public static void Run()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AvatarExpressionProfile>(Path);
            if (existing) { existing.Validate(); return; }
            if (File.Exists(Path) || File.Exists(Path + ".meta")) throw new InvalidOperationException("Preserve the existing expression asset/path; resolve its type first.");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources/Art")) throw new InvalidOperationException("Expected the existing Art resources folder.");
            var profile = ScriptableObject.CreateInstance<AvatarExpressionProfile>();
            try { profile.Validate(); AssetDatabase.CreateAsset(profile, Path); }
            catch { if (!AssetDatabase.Contains(profile)) UnityEngine.Object.DestroyImmediate(profile); throw; }
        }

        [MenuItem("Wreckabulary/Art/Apply Reviewed Calm Idle and Hurt")]
        public static void ApplyReviewedTuning()
        {
            string folder = "BuildNotes/character-experience-20261026/emotion/tuning-" +
                DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            if (Directory.Exists(folder)) throw new IOException("Preserve an existing tuning receipt.");
            Directory.CreateDirectory(folder);
            var receipt = new TuningReceipt { utc = DateTime.UtcNow.ToString("O"),
                beforeHash = HashOrMissing(Path), metaBeforeHash = HashOrMissing(Path + ".meta") };
            AvatarExpressionProfile profile = null;
            try
            {
                profile = AssetDatabase.LoadAssetAtPath<AvatarExpressionProfile>(Path);
                if (!profile || receipt.beforeHash == "MISSING" || receipt.metaBeforeHash == "MISSING")
                    throw new InvalidOperationException("The saved expression profile and metadata must already exist.");
                receipt.oldJson = EditorJsonUtility.ToJson(profile);
                if (EditorUtility.IsDirty(profile)) throw new InvalidOperationException("Preserve unsaved expression edits; save or resolve them before applying this bounded tuning.");
                profile.Validate();
                int idle = Index(profile, AvatarExpression.Idle), hurt = Index(profile, AvatarExpression.Hurt);
                bool preceding = Matches(profile.Poses[idle], 55, 0, true) && Matches(profile.Poses[hurt], 4, 38, false);
                bool reviewed = Matches(profile.Poses[idle], 75, 0, true) && Matches(profile.Poses[hurt], 18, 60, false);
                if (!preceding && !reviewed) throw new InvalidOperationException("Idle/Hurt contain custom or partially applied values; preserve them instead of overwriting.");
                receipt.idempotent = reviewed;
                if (preceding)
                {
                    profile.Poses[idle] = new AvatarExpressionPose(AvatarExpression.Idle, 75, 0, true);
                    profile.Poses[hurt] = new AvatarExpressionPose(AvatarExpression.Hurt, 18, 60, false);
                    profile.Validate();
                }
                string expected = EditorJsonUtility.ToJson(profile);
                if (preceding)
                {
                    EditorUtility.SetDirty(profile); receipt.saveAttempted = true;
                    AssetDatabase.SaveAssetIfDirty(profile);
                }
                AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var reloaded = AssetDatabase.LoadAssetAtPath<AvatarExpressionProfile>(Path);
                receipt.newJson = reloaded ? EditorJsonUtility.ToJson(reloaded) : "MISSING";
                receipt.reloadedVerified = reloaded && !EditorUtility.IsDirty(reloaded) && receipt.newJson == expected;
                if (!receipt.reloadedVerified) throw new InvalidOperationException("Reloaded expression data differs from the exact two-pose update.");
                if (reviewed && HashOrMissing(Path) != receipt.beforeHash) throw new InvalidOperationException("Idempotent verification changed saved profile bytes.");
                receipt.status = reviewed ? "ALREADY APPLIED; VERIFIED WITHOUT SAVE" : "APPLIED; RELOADED AND VERIFIED";
            }
            catch (Exception error) { receipt.status = "FAIL"; receipt.error = error.ToString(); throw; }
            finally
            {
                receipt.afterHash = HashOrMissing(Path); receipt.metaAfterHash = HashOrMissing(Path + ".meta");
                receipt.metaPreserved = receipt.metaBeforeHash == receipt.metaAfterHash;
                if (receipt.newJson.Length == 0 && profile) receipt.newJson = EditorJsonUtility.ToJson(profile);
                receipt.oldJsonHash = AvatarSurfaceMeshData.Hash(System.Text.Encoding.UTF8.GetBytes(receipt.oldJson));
                receipt.newJsonHash = AvatarSurfaceMeshData.Hash(System.Text.Encoding.UTF8.GetBytes(receipt.newJson));
                if (!receipt.metaPreserved) { receipt.status = "FAIL"; receipt.error += "\nExpression metadata changed."; }
                using (var file = new FileStream(folder + "/receipt.json", FileMode.CreateNew))
                using (var writer = new StreamWriter(file)) writer.Write(JsonUtility.ToJson(receipt, true));
                Debug.Log("[Wreckabulary] Expression tuning: " + receipt.status + "; receipt=" + folder + "/receipt.json");
                if (!receipt.metaPreserved) throw new InvalidOperationException("Expression metadata changed; inspect the tuning receipt.");
            }
        }

        static int Index(AvatarExpressionProfile profile, AvatarExpression state)
        {
            for (int i = 0; i < profile.Poses.Length; i++) if (profile.Poses[i].State == state) return i;
            throw new InvalidOperationException("Required expression state is missing.");
        }

        static bool Matches(AvatarExpressionPose pose, float brow, float blink, bool natural) =>
            pose.BrowRelax == brow && pose.Blink == blink && pose.NaturalBlink == natural;
        static string HashOrMissing(string path) => File.Exists(path) ? AvatarSurfaceMeshData.Hash(File.ReadAllBytes(path)) : "MISSING";
    }
}
#endif
