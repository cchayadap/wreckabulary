using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
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
                UpdateView(view);
                Assert.AreEqual(71, camera.fieldOfView);
                Assert.AreEqual(.15f, camera.nearClipPlane);

                profile.FieldOfView = float.NaN;
                profile.NearClip = .05f;
                LogAssert.Expect(LogType.Warning, "Wordbound camera profile: FieldOfView is outside its supported camera range.");
                UpdateView(view);
                Assert.AreEqual(71, camera.fieldOfView);
                Assert.AreEqual(.15f, camera.nearClipPlane);

                profile.FieldOfView = 65;
                UpdateView(view);
                Assert.AreEqual(65, camera.fieldOfView);
                Assert.AreEqual(.05f, camera.nearClipPlane);
                view.Profile = null;
                UpdateView(view);
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

        [Test]
        public void RuntimeOwnedCameraReceivesTheOriginalGameLookAndCurrentSmoothing()
        {
            var root = new GameObject("Owned camera look test");
            var previousLook = GraphicsOptions.Look;
            Camera camera = null;
            try
            {
                var view = root.AddComponent<WordboundPlayerView>();
                view.Initialize(root.AddComponent<WordboundGameplayController>(), controls: new IdleControls());
                camera = view.Camera;
                var data = camera.GetComponent<UniversalAdditionalCameraData>();
                Assert.IsNotNull(data, "A camera created after scene load must receive its own rendering setup.");
                Assert.IsTrue(data.renderPostProcessing);
                Assert.IsTrue(data.dithering);
                Assert.IsTrue(camera.allowHDR);
                Assert.IsTrue(GraphicsOptions.Look);
                Assert.IsTrue(GraphicsOptions.Look.sharedProfile.TryGet<Tonemapping>(out var tone));
                Assert.AreEqual(TonemappingMode.ACES, tone.mode.value);
                var smoothing = GraphicsOptions.Smoothing;
                Assert.AreEqual(smoothing.StartsWith("MSAA", System.StringComparison.Ordinal), camera.allowMSAA);
                Assert.AreEqual(smoothing == "FXAA" ? AntialiasingMode.FastApproximateAntialiasing :
                    smoothing == "SMAA" ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None,
                    data.antialiasing);
                Assert.AreEqual(GraphicsOptions.Shadows != "Off", data.renderShadows);
            }
            finally
            {
                if (camera) Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(root);
                if (!previousLook && GraphicsOptions.Look)
                {
                    Object.DestroyImmediate(GraphicsOptions.Look.sharedProfile);
                    Object.DestroyImmediate(GraphicsOptions.Look.gameObject);
                }
            }
        }

        [Test]
        public void CallerSuppliedCameraRetainsItsRenderingConfiguration()
        {
            var root = new GameObject("Caller camera look test");
            try
            {
                var camera = root.AddComponent<Camera>();
                camera.allowHDR = false;
                camera.allowMSAA = false;
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                data.dithering = false;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                var view = root.AddComponent<WordboundPlayerView>();
                view.Initialize(root.AddComponent<WordboundGameplayController>(), camera, new IdleControls());
                Assert.AreSame(camera, view.Camera);
                Assert.IsFalse(camera.allowHDR);
                Assert.IsFalse(camera.allowMSAA);
                Assert.IsFalse(data.renderPostProcessing);
                Assert.IsFalse(data.dithering);
                Assert.AreEqual(AntialiasingMode.SubpixelMorphologicalAntiAliasing, data.antialiasing);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void UpdateView(WordboundPlayerView view) => typeof(WordboundPlayerView)
            .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
    }
}
