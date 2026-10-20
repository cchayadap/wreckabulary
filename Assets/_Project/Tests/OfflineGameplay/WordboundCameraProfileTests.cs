using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Online;
using Object = UnityEngine.Object;

namespace Wreckabulary.OfflineGameplayTests
{
    public sealed class WordboundCameraProfileTests
    {
        sealed class IdleControls : InputBinding
        {
            public override string Id => "camera-profile-test";
            public override void Read(ref PlayerCommands commands) { }
            public override bool JoinPressed() => false;
            public override bool StartPressed() => false;
        }

        [Test]
        public void InspectorEditsReachActualCameraAndInvalidEditsRetainLastValidSettings()
        {
            var root = new GameObject("Camera profile native test");
            var profile = ScriptableObject.CreateInstance<WordboundCameraProfile>();
            try
            {
                var camera = root.AddComponent<Camera>();
                var gameplay = root.AddComponent<WordboundGameplayController>();
                var view = root.AddComponent<WordboundPlayerView>();
                profile.FieldOfView = 62;
                profile.FarClip = 1600;
                view.Initialize(gameplay, camera, new IdleControls(), profile);
                Assert.AreEqual(62, camera.fieldOfView);
                Assert.AreEqual(1600, camera.farClipPlane);

                profile.FieldOfView = 71;
                profile.NearClip = .15f;
                view.SendMessage("Update");
                Assert.AreEqual(71, camera.fieldOfView);
                Assert.AreEqual(.15f, camera.nearClipPlane);

                profile.FieldOfView = float.NaN;
                profile.NearClip = .05f;
                LogAssert.Expect(LogType.Warning, "Wordbound camera profile: FieldOfView is outside its supported camera range.");
                view.SendMessage("Update");
                Assert.AreEqual(71, camera.fieldOfView);
                Assert.AreEqual(.15f, camera.nearClipPlane);

                profile.FieldOfView = 65;
                view.SendMessage("Update");
                Assert.AreEqual(65, camera.fieldOfView);
                Assert.AreEqual(.05f, camera.nearClipPlane);
                view.Profile = null;
                view.SendMessage("Update");
                Assert.AreEqual(ShoulderView.FieldOfView, camera.fieldOfView);
                Assert.AreEqual(ShoulderView.NearClip, camera.nearClipPlane);
                Assert.AreEqual(1800, camera.farClipPlane);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(profile);
            }
        }
    }
}
