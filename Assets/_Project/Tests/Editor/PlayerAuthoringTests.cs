using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.EditorTools;

namespace Wreckabulary.EditorTests
{
    public sealed class PlayerAuthoringTests
    {
        [Test]
        public void PlayerPrefabContainsTheEditableAvatarAndHidesLegacyGeometry()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerAuthoring.PlayerPrefabPath);
            var player = prefab.GetComponent<PlayerController>();
            var appearance = prefab.GetComponent<PlayerAppearance>();
            Assert.IsNotNull(appearance);
            Assert.IsNotNull(appearance.AuthoredAvatar);
            Assert.That(appearance.AuthoredAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, Is.GreaterThan(0));
            foreach (var renderer in player.visual.GetComponentsInChildren<Renderer>(true))
                if (!renderer.transform.IsChildOf(appearance.AuthoredAvatar.transform))
                    Assert.IsFalse(renderer.enabled, renderer.name + " is obsolete primitive geometry.");
        }

        [Test]
        public void ReapplyingUpgradePreservesArtistTransformsMeshesAndMaterials()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerAuthoring.PlayerPrefabPath);
            try
            {
                var appearance = root.GetComponent<PlayerAppearance>();
                Assert.IsNotNull(appearance.AuthoredAvatar);
                var avatar = appearance.AuthoredAvatar;
                var renderer = avatar.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var mesh = renderer.sharedMesh;
                var material = renderer.sharedMaterial;
                var offset = new Vector3(.06f, .02f, -.04f);
                avatar.transform.localPosition = offset;
                avatar.transform.localScale *= .93f;
                var scale = avatar.transform.localScale;
                renderer.enabled = !renderer.enabled;
                bool enabled = renderer.enabled;

                Assert.IsFalse(PlayerAuthoring.UpgradePlayer(root.GetComponent<PlayerController>()));
                Assert.AreSame(avatar, appearance.AuthoredAvatar);
                Assert.AreEqual(offset, avatar.transform.localPosition);
                Assert.AreEqual(scale, avatar.transform.localScale);
                Assert.AreSame(mesh, renderer.sharedMesh);
                Assert.AreSame(material, renderer.sharedMaterial);
                Assert.AreEqual(enabled, renderer.enabled);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void ScenePreviewIsVisualOnlyAndExcludedFromBuilds()
        {
            var parent = new GameObject("Preview test");
            try
            {
                var preview = PlayerAuthoring.CreatePreview(parent.transform);
                Assert.IsTrue(preview.CompareTag("EditorOnly"));
                Assert.IsEmpty(preview.GetComponentsInChildren<PlayerController>(true));
                Assert.IsEmpty(preview.GetComponentsInChildren<Rigidbody>(true));
                Assert.That(preview.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, Is.GreaterThan(0));
                preview.transform.localPosition = Vector3.right;
                Assert.AreSame(preview, PlayerAuthoring.CreatePreview(parent.transform));
                Assert.AreEqual(Vector3.right, preview.transform.localPosition);
            }
            finally { Object.DestroyImmediate(parent); }
        }
    }
}
