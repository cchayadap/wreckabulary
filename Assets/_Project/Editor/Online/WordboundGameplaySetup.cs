using System;
using System.IO;
using System.Linq;
using Fusion;
using Fusion.Addons.Physics;
using Fusion.Editor;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Online;

namespace Wreckabulary.EditorTools
{
    public static class WordboundGameplaySetup
    {
        public const string Folder = "Assets/_Project/Prefabs/Wordbound/Network";

        public static void CreateBodyPrefabs()
        {
            foreach (var type in new[] { typeof(WordboundNetworkBody), typeof(WordboundGameplayBody) })
                if (!type.GetCustomAttributes(false).Any(a => a.GetType().Name == "NetworkBehaviourWeavedAttribute"))
                    throw new InvalidOperationException(type.Name + " needs native Fusion weaving before baking.");
            EnsureFolder(Folder);
            Create("WordboundPlayerBody", true, false);
            Create("WordboundItemBody", false, false);
            Create("WordboundPlatformBody", false, true);
            NetworkProjectConfigUtilities.RebuildPrefabTable();
            AssetDatabase.SaveAssets();
            foreach (string name in new[] { "WordboundPlayerBody", "WordboundItemBody", "WordboundPlatformBody" })
                if (!NetworkProjectConfigUtilities.TryGetPrefabId(Folder + "/" + name + ".prefab", out var id) || !id.IsValid)
                    throw new InvalidOperationException(name + " is missing from the native prefab table.");
            Debug.Log("[Wreckabulary] Three native gameplay body prefabs baked and registered.");
        }

        static void Create(string name, bool player, bool platform)
        {
            string path = Folder + "/" + name + ".prefab";
            bool existing = File.Exists(path);
            bool loadedContents = existing;
            GameObject root = existing ? PrefabUtility.LoadPrefabContents(path) : new GameObject(name);
            try
            {
                if (!existing)
                {
                    root.AddComponent<NetworkObject>();
                    var rigidbody = root.AddComponent<Rigidbody>();
                    rigidbody.mass = player ? 70 : platform ? 100 : 5;
                    rigidbody.isKinematic = platform;
                    rigidbody.useGravity = !platform;
                    rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
                    if (player)
                    {
                        rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                        var capsule = root.AddComponent<CapsuleCollider>();
                        capsule.center = Vector3.up * .9f;
                        capsule.radius = .3f;
                        capsule.height = 1.8f;
                    }
                    else
                    {
                        var box = root.AddComponent<BoxCollider>();
                        box.center = Vector3.up * .5f;
                        box.size = Vector3.one;
                    }
                    var visual = new GameObject("Visual");
                    visual.transform.SetParent(root.transform, false);
                    var networkBody = root.AddComponent<NetworkRigidbody>();
                    networkBody.InterpolationTarget = visual.transform;
                    networkBody.SyncParent = false;
                    networkBody.SyncScale = false;
                    root.AddComponent<WordboundNetworkBody>();
                    root.AddComponent<WordboundGameplayBody>();
                    if (!PrefabUtility.SaveAsPrefabAsset(root, path))
                        throw new InvalidOperationException("Could not create " + path);
                    UnityEngine.Object.DestroyImmediate(root);
                    root = null;
                    root = PrefabUtility.LoadPrefabContents(path);
                    loadedContents = true;
                }
                var interpolation = root.GetComponent<NetworkRigidbody>()?.InterpolationTarget;
                if (!root.GetComponent<NetworkObject>() || !root.GetComponent<Rigidbody>() ||
                    !root.GetComponent<WordboundNetworkBody>() || !root.GetComponent<WordboundGameplayBody>() ||
                    root.GetComponent<NetworkTransform>() || interpolation == null ||
                    interpolation.parent != root.transform || root.transform.localScale != Vector3.one ||
                    root.GetComponentsInChildren<Rigidbody>(true).Length != 1 ||
                    root.GetComponentsInChildren<Collider>(true).Length != 1 ||
                    player && !root.GetComponent<CapsuleCollider>() || !player && !root.GetComponent<BoxCollider>())
                    throw new InvalidOperationException(path + " differs from the approved native body contract; preserve for inspection.");
                new NetworkObjectBakerEditTime().Bake(root);
                if (!PrefabUtility.SaveAsPrefabAsset(root, path))
                    throw new InvalidOperationException("Could not save " + path);
            }
            finally
            {
                if (root != null)
                {
                    if (loadedContents) PrefabUtility.UnloadPrefabContents(root);
                    else UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
