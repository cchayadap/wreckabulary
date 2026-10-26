using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Art;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class AvatarSurfaceRuntimeTests
    {
        [UnitySetUp] public IEnumerator SetUp() => TestScenes.Reset();
        [UnityTearDown] public IEnumerator TearDown() => TestScenes.Reset();

        [UnityTest]
        public IEnumerator ExistingAuthoredPlayerIsCorrectedDuringActualAppearanceInitialization()
        {
            var profile = AvatarSurfaceProfile.Load();
            Assert.IsNotNull(profile);
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            var appearance = player.GetComponent<PlayerAppearance>();
            var authored = appearance.AuthoredAvatar;
            Assert.IsNotNull(authored);
            var head = authored.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(renderer => renderer.name == "SK_Head");
            head.sharedMesh = profile.SourceMesh;
            var wrapper = authored.transform.parent;
            var offset = new Vector3(.03f, .01f, -.02f); wrapper.localPosition = offset;
            int rendererCount = player.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
            player.Setup(0, new ScriptedBinding());
            player.Respawn(Vector3.zero);
            Assert.IsTrue(appearance.Initialize(player));
            yield return null;
            Assert.AreSame(authored, appearance.AvatarModel);
            Assert.AreSame(profile.CorrectedMesh, head.sharedMesh);
            Assert.AreEqual(rendererCount, player.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length);
            Assert.That(Vector3.Distance(offset, wrapper.localPosition), Is.LessThan(.0001f));
            Assert.IsTrue(appearance.Initialize(player));
            Assert.AreSame(profile.CorrectedMesh, head.sharedMesh);
            Assert.AreSame(profile.SourceMesh, profile.SourceAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Single(renderer => renderer.name == "SK_Head").sharedMesh);
        }
    }
}
