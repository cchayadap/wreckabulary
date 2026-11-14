#if UNITY_EDITOR || GSK_QA
using GameStudioKit.Qa;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Wreckabulary.Online;

namespace Wreckabulary.Qa
{
    public sealed class WordboundPartyFrameObservation : MonoBehaviour
    {
        QaContext context;
        WordboundPartyMenu menu;
        Camera expected;
        bool subscribed, afterSplash;
        int repaintBaseline, cameraFrames, lastCameraFrame = -1;
        float nextRecord;
        string cameraCallback = "none";

        public bool Ready => afterSplash && SplashScreen.isFinished && menu && menu.isActiveAndEnabled && menu.Visible &&
            menu.Coordinator != null && !menu.Coordinator.MatchActive &&
            expected && expected.isActiveAndEnabled && expected.targetTexture == null && expected.targetDisplay == 0 &&
            menu.QaCompletedRepaintCount - repaintBaseline >= 2 && cameraFrames >= 2 &&
            Time.frameCount - menu.QaLastRepaintFrame <= 1 && Time.frameCount - lastCameraFrame <= 1;

        public void Begin(QaContext owner, WordboundPartyMenu party, Camera camera)
        {
            context = owner; menu = party; expected = camera;
            hideFlags = HideFlags.DontSave;
            RenderPipelineManager.endCameraRendering += OnPipelineCamera;
            Camera.onPostRender += OnBuiltinCamera;
            subscribed = true;
            Record();
        }

        void Update()
        {
            if (context == null) return;
            if (!QaRunner.IsRunning || context.CurrentStepName != "open_wordbound" || !menu || !menu.isActiveAndEnabled)
            { Dispose(); return; }
            if (!afterSplash && SplashScreen.isFinished)
            {
                afterSplash = true;
                repaintBaseline = menu.QaCompletedRepaintCount;
            }
            if (Time.realtimeSinceStartup >= nextRecord)
            { nextRecord = Time.realtimeSinceStartup + 1; Record(); }
        }

        void OnPipelineCamera(ScriptableRenderContext _, Camera camera)
        {
            if (GraphicsSettings.currentRenderPipeline != null) Observe(camera, "endCameraRendering");
        }

        void OnBuiltinCamera(Camera camera)
        {
            if (GraphicsSettings.currentRenderPipeline == null) Observe(camera, "onPostRender");
        }

        void Observe(Camera camera, string callback)
        {
            if (!afterSplash || camera != expected || lastCameraFrame == Time.frameCount) return;
            lastCameraFrame = Time.frameCount; cameraFrames++; cameraCallback = callback;
        }

        public void Record()
        {
            if (context == null || context.CurrentStepName != "open_wordbound") return;
            context.Note("party_frame_scene", SceneManager.GetActiveScene().name);
            context.Note("party_frame_screen", $"{Screen.width}x{Screen.height}");
            context.Note("party_frame_focused", Application.isFocused);
            context.Note("party_frame_splash_finished", SplashScreen.isFinished);
            context.Note("party_frame_current", Time.frameCount);
            context.Note("party_frame_rendered", Time.renderedFrameCount);
            context.Note("party_frame_repaints", menu ? menu.QaCompletedRepaintCount : -1);
            context.Note("party_frame_repaints_after_splash", menu && afterSplash ? menu.QaCompletedRepaintCount - repaintBaseline : 0);
            context.Note("party_frame_last_repaint", menu ? menu.QaLastRepaintFrame : -1);
            context.Note("party_frame_menu_active", menu && menu.isActiveAndEnabled);
            context.Note("party_frame_menu_visible", menu && menu.Visible);
            context.Note("party_frame_camera", expected ? expected.name : "missing");
            context.Note("party_frame_camera_active", expected && expected.isActiveAndEnabled);
            context.Note("party_frame_camera_display", expected ? expected.targetDisplay : -1);
            context.Note("party_frame_camera_target", expected && expected.targetTexture ? expected.targetTexture.name : "none");
            context.Note("party_frame_camera_callbacks", cameraFrames);
            context.Note("party_frame_last_camera", lastCameraFrame);
            context.Note("party_frame_camera_callback", cameraCallback);
            context.Note("party_frame_ready", Ready);
        }

        void Unsubscribe()
        {
            if (!subscribed) return;
            RenderPipelineManager.endCameraRendering -= OnPipelineCamera;
            Camera.onPostRender -= OnBuiltinCamera;
            subscribed = false;
        }

        public void Dispose() { Unsubscribe(); Destroy(this); }
        void OnDisable() => Unsubscribe();
        void OnDestroy() => Unsubscribe();
    }
}
#endif
