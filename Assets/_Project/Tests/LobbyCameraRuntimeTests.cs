using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class LobbyCameraRuntimeTests
    {
        LobbyStage stage;
        LobbyCameraProfile profile;
        Camera camera;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            camera = new GameObject("Lobby framing test camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            profile = ScriptableObject.CreateInstance<LobbyCameraProfile>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.Reset();
            if (profile) Object.DestroyImmediate(profile);
        }

        void Open()
        {
            stage = new GameObject("Lobby framing test stage").AddComponent<LobbyStage>();
            stage.CameraProfile = profile;
            stage.Show(GameConfig.Current.Houses.Keys.First(), PlayerAppearance.DefaultPresentationOutfit());
        }

        [UnityTest]
        public IEnumerator AuthoredFramingIsSnapshottedAndTheOriginalCameraIsRestored()
        {
            var position = new Vector3(8, 9, 10);
            var rotation = Quaternion.Euler(11, 23, 7);
            var background = new Color(.2f, .3f, .4f, 1);
            camera.transform.SetPositionAndRotation(position, rotation);
            camera.orthographic = true; camera.fieldOfView = 57; camera.nearClipPlane = .37f;
            camera.clearFlags = CameraClearFlags.Skybox; camera.backgroundColor = background;
            profile.Distance = 1.5f; profile.EyeHeight = 1.2f; profile.LookHeight = .7f; profile.FieldOfView = 36;
            string originalProfile = JsonUtility.ToJson(profile);
            Open();
            yield return null;
            Assert.IsTrue(stage.IsPresenting);
            Assert.AreEqual(36, camera.fieldOfView);
            Assert.AreEqual(1.2f, camera.transform.position.y - stage.Spot.y, .0001f);
            Assert.LessOrEqual(Mathf.Abs(camera.transform.position.z - stage.Spot.z), 1.5001f);
            var expected = Quaternion.LookRotation(stage.Spot + Vector3.up * .7f - camera.transform.position);
            Assert.Less(Quaternion.Angle(expected, camera.transform.rotation), .01f);
            Assert.AreEqual(originalProfile, JsonUtility.ToJson(profile), "Runtime must not edit the profile.");
            profile.EyeHeight = 2; profile.FieldOfView = 48;
            yield return null;
            Assert.AreEqual(36, camera.fieldOfView, "An active stage keeps its valid startup snapshot.");
            Assert.AreEqual(1.2f, camera.transform.position.y - stage.Spot.y, .0001f);
            stage.Release();
            Assert.AreEqual(position, camera.transform.position);
            Assert.Less(Quaternion.Angle(rotation, camera.transform.rotation), .001f);
            Assert.IsTrue(camera.orthographic); Assert.AreEqual(57, camera.fieldOfView);
            Assert.AreEqual(.37f, camera.nearClipPlane); Assert.AreEqual(CameraClearFlags.Skybox, camera.clearFlags);
            Assert.AreEqual(background, camera.backgroundColor);
            Assert.IsTrue(profile, "The stage does not own or destroy its assigned profile.");
        }

        [UnityTest]
        public IEnumerator InvalidOverrideUsesTheUnchangedDefaultFramingWithoutRepairingIt()
        {
            profile.Distance = float.NaN;
            LogAssert.Expect(LogType.Warning, new Regex("Lobby camera profile is invalid; using the existing default framing"));
            Open();
            yield return null;
            Assert.AreEqual(30, camera.fieldOfView);
            Assert.AreEqual(.95f, camera.transform.position.y - stage.Spot.y, .0001f);
            Assert.LessOrEqual(Mathf.Abs(camera.transform.position.z - stage.Spot.z), 4.2001f);
            var expected = Quaternion.LookRotation(stage.Spot + Vector3.up * .5f - camera.transform.position);
            Assert.Less(Quaternion.Angle(expected, camera.transform.rotation), .01f);
            Assert.IsTrue(float.IsNaN(profile.Distance));
        }

        [UnityTest]
        public IEnumerator ANewStageReadsEditedValuesWithoutAStaticProfileCache()
        {
            profile.FieldOfView = 31;
            Open();
            yield return null;
            Assert.AreEqual(31, camera.fieldOfView);
            stage.Release(); Object.Destroy(stage.gameObject);
            yield return null;
            profile.FieldOfView = 44;
            string saved = JsonUtility.ToJson(profile);
            Open();
            yield return null;
            Assert.AreEqual(44, camera.fieldOfView);
            Assert.AreEqual(saved, JsonUtility.ToJson(profile));
        }
    }
}
