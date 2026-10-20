using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Wreckabulary.EditorTools
{
    public static class WordboundPhotonSetup
    {
        public static void CreateProofScene()
        {
            const string path = "Assets/_Project/Tests/Photon/WordboundPhotonProof.unity";
            if (File.Exists(path))
            {
                if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(path))
                    throw new InvalidOperationException("The existing proof scene could not be imported.");
                return;
            }
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Create the proof scene through a cold batch gate to preserve interactive scenes.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, path))
                throw new InvalidOperationException("Could not save the native Photon proof scene.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Debug.Log("[Wreckabulary] Created native empty Photon proof scene.");
        }

        public static void ImportFusion()
        {
            string package = Environment.GetEnvironmentVariable("WORDBOUND_FUSION_PACKAGE");
            string expected = Environment.GetEnvironmentVariable("WORDBOUND_FUSION_SHA256");
            if (string.IsNullOrEmpty(package) || !File.Exists(package) || string.IsNullOrEmpty(expected))
                throw new InvalidOperationException("Supply the inspected Fusion package and its SHA-256 through the environment.");
            using (var stream = File.OpenRead(package))
            using (var sha = SHA256.Create())
            {
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (actual != expected) throw new InvalidOperationException("Fusion package hash differs from the inspected package.");
            }
            UnityEditor.AssetPackage.Package.Import(Path.GetFullPath(package), false);
            Debug.Log("[Wreckabulary] Queued inspected Fusion package import; wait for the completion event before verification.");
        }
    }
}
