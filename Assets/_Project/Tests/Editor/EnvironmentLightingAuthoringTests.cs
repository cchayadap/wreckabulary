using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.EditorTools;

namespace Wreckabulary.EditorTests
{
    public sealed class EnvironmentLightingAuthoringTests
    {
        [TestCase("Hub")]
        [TestCase("LivingRoom")]
        [TestCase("MovingDay")]
        [TestCase("Tutorial")]
        public void SavedLightingIsPersistentAndUpgradesPreserveArtistEdits(string name)
        {
            var scene = EditorSceneManager.OpenPreviewScene(SceneWorkspace.SceneFolder + name + ".unity");
            try
            {
                var lighting = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EnvironmentLighting>(true)).Single();
                Assert.IsTrue(EditorUtility.IsPersistent(lighting.Profile));
                Assert.IsTrue(EditorUtility.IsPersistent(lighting.Profile.Skybox));
                Assert.AreEqual("Skybox/Procedural", lighting.Profile.Skybox.shader.name);
                Assert.IsNotNull(lighting.Sun);
                Assert.IsNotNull(lighting.Fill);
                lighting.Sun.transform.rotation = Quaternion.Euler(21f, 44f, 0f);
                lighting.Sun.intensity = 2.1f;
                var before = lighting.Sun.transform.rotation;
                Assert.IsFalse(EnvironmentLightingAuthoring.AddMissing(scene, lighting.Profile));
                Assert.AreEqual(2.1f, lighting.Sun.intensity);
                Assert.AreEqual(before, lighting.Sun.transform.rotation);
                Assert.AreEqual(1, scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EnvironmentLighting>(true)).Count());
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
