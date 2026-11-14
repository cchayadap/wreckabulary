#if UNITY_EDITOR || GSK_QA
using System;
using System.Diagnostics;
using System.Globalization;
using GameStudioKit.Qa;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Wreckabulary.Online;
using Debug = UnityEngine.Debug;

namespace Wreckabulary.Qa
{
    public sealed class WordboundFrameObservation : MonoBehaviour
    {
        struct Sample
        {
            public int Frame, RenderedDelta;
            public long Ticks;
            public double Gap;
        }

        readonly Sample[] worst = new Sample[16];
        QaContext context;
        WordboundGameplayController gameplay;
        Camera expected;
        bool active, pendingCamera, previousSnapshot;
        long startStamp, previousStamp, cameraStamp, firstTick, firstEpoch, previousTick, previousEpoch;
        long positiveTicks, maximumTicks, cameraElapsed, maximumCameraElapsed;
        int firstFrame, previousFrame, firstRendered, previousRendered, updates, zeroTickUpdates;
        int missingSnapshots, unpairedSnapshots, epochChanges, regressions, duplicateUpdates, unfocusedUpdates, worstCount;
        int beginCount, endCount, beginFrames, endFrames, lastBeginFrame = -1, lastEndFrame = -1;
        int pendingFrame, cameraPairs, unmatchedBegins, unmatchedEnds, crossedFramePairs;
        double gapTotal, maximumGap;
        string error = "";

        public static WordboundFrameObservation StartIfEnabled(WordboundLauncher owner,
            WordboundGameplayController gameplay, Camera camera, QaContext context)
        {
            if (Environment.GetEnvironmentVariable("WORDBOUND_PROFILE_FRAMES") != "1" || !QaRunner.IsRunning) return null;
            WordboundFrameObservation observation = null;
            try
            {
                if (!owner || !gameplay || !camera || context == null || context.CurrentStepName != "exact_playtest")
                    throw new InvalidOperationException();
                observation = owner.gameObject.AddComponent<WordboundFrameObservation>();
                observation.hideFlags = HideFlags.DontSave;
                observation.context = context; observation.gameplay = gameplay; observation.expected = camera;
                observation.Begin();
                return observation;
            }
            catch (Exception failure)
            {
                Debug.Log("WORDBOUND_FRAME_UNAVAILABLE: start-" + failure.GetType().Name);
                if (observation) observation.Finish("start-failed");
                return null;
            }
        }

        void Begin()
        {
            RecordSettings("begin");
            var frame = gameplay.Frame;
            if (frame == null) throw new InvalidOperationException();
            firstTick = previousTick = frame.Progress.Tick; firstEpoch = previousEpoch = frame.Epoch;
            previousSnapshot = true;
            firstFrame = previousFrame = Time.frameCount; firstRendered = previousRendered = Time.renderedFrameCount;
            startStamp = previousStamp = Stopwatch.GetTimestamp();
            active = true;
            RenderPipelineManager.beginCameraRendering += PipelineBegin;
            RenderPipelineManager.endCameraRendering += PipelineEnd;
            Camera.onPreRender += BuiltinBegin;
            Camera.onPostRender += BuiltinEnd;
        }

        void Update()
        {
            if (!active) return;
            if (!QaRunner.IsRunning || context == null || context.CurrentStepName != "exact_playtest" || !gameplay || !expected)
            { Finish("step-abandoned"); return; }
            try
            {
                if (Time.frameCount == previousFrame) { duplicateUpdates++; return; }
                long now = Stopwatch.GetTimestamp();
                double gap = (now - previousStamp) / (double)Stopwatch.Frequency;
                long delta = -1;
                var frame = gameplay.Frame;
                if (frame == null) { missingSnapshots++; previousSnapshot = false; }
                else
                {
                    if (!previousSnapshot) unpairedSnapshots++;
                    else if (frame.Epoch != previousEpoch) epochChanges++;
                    else if (frame.Progress.Tick < previousTick) regressions++;
                    else
                    {
                        delta = frame.Progress.Tick - previousTick;
                        positiveTicks += delta; maximumTicks = Math.Max(maximumTicks, delta);
                        if (delta == 0) zeroTickUpdates++;
                    }
                    previousEpoch = frame.Epoch; previousTick = frame.Progress.Tick; previousSnapshot = true;
                }
                updates++; gapTotal += gap; maximumGap = Math.Max(maximumGap, gap);
                if (!Application.isFocused) unfocusedUpdates++;
                KeepWorst(new Sample { Frame = Time.frameCount, RenderedDelta = Time.renderedFrameCount - previousRendered,
                    Ticks = delta, Gap = gap });
                previousStamp = now; previousFrame = Time.frameCount; previousRendered = Time.renderedFrameCount;
            }
            catch (Exception failure) { error = failure.GetType().Name; Finish("observation-failed"); }
        }

        void KeepWorst(Sample sample)
        {
            if (worstCount < worst.Length) { worst[worstCount++] = sample; return; }
            int smallest = 0;
            for (int i = 1; i < worst.Length; i++) if (worst[i].Gap < worst[smallest].Gap) smallest = i;
            if (sample.Gap > worst[smallest].Gap) worst[smallest] = sample;
        }

        void PipelineBegin(ScriptableRenderContext _, Camera camera) { if (GraphicsSettings.currentRenderPipeline) BeginCamera(camera); }
        void PipelineEnd(ScriptableRenderContext _, Camera camera) { if (GraphicsSettings.currentRenderPipeline) EndCamera(camera); }
        void BuiltinBegin(Camera camera) { if (!GraphicsSettings.currentRenderPipeline) BeginCamera(camera); }
        void BuiltinEnd(Camera camera) { if (!GraphicsSettings.currentRenderPipeline) EndCamera(camera); }

        void BeginCamera(Camera camera)
        {
            if (!active || camera != expected) return;
            beginCount++;
            if (lastBeginFrame != Time.frameCount) { beginFrames++; lastBeginFrame = Time.frameCount; }
            if (pendingCamera) unmatchedBegins++;
            pendingCamera = true; pendingFrame = Time.frameCount; cameraStamp = Stopwatch.GetTimestamp();
        }

        void EndCamera(Camera camera)
        {
            if (!active || camera != expected) return;
            endCount++;
            if (lastEndFrame != Time.frameCount) { endFrames++; lastEndFrame = Time.frameCount; }
            if (!pendingCamera) { unmatchedEnds++; return; }
            pendingCamera = false;
            if (pendingFrame != Time.frameCount) { crossedFramePairs++; return; }
            long elapsed = Stopwatch.GetTimestamp() - cameraStamp;
            cameraPairs++; cameraElapsed += elapsed; maximumCameraElapsed = Math.Max(maximumCameraElapsed, elapsed);
        }

        public void Finish(string outcome)
        {
            Stop(outcome);
            Destroy(this);
        }

        void Stop(string outcome)
        {
            if (!active) return;
            long stopped = Stopwatch.GetTimestamp();
            active = false;
            RenderPipelineManager.beginCameraRendering -= PipelineBegin;
            RenderPipelineManager.endCameraRendering -= PipelineEnd;
            Camera.onPreRender -= BuiltinBegin;
            Camera.onPostRender -= BuiltinEnd;
            try
            {
                var frame = gameplay ? gameplay.Frame : null;
                double elapsed = (stopped - startStamp) / (double)Stopwatch.Frequency;
                Debug.Log("WORDBOUND_FRAME_WINDOW: outcome=" + outcome + "; error=" + error + "; seconds=" + F(elapsed) +
                    "; updateSamples=" + updates + "; firstFrame=" + firstFrame + "; lastFrame=" + Time.frameCount +
                    "; renderedFrameDelta=" + (Time.renderedFrameCount - firstRendered) +
                    "; firstEpoch=" + firstEpoch + "; lastEpoch=" + (frame?.Epoch ?? -1) +
                    "; firstTick=" + firstTick + "; lastTick=" + (frame?.Progress.Tick ?? -1) +
                    "; observedTickDelta=" + positiveTicks + "; maxTicksPerUpdate=" + maximumTicks +
                    "; zeroTickUpdates=" + zeroTickUpdates + "; meanUpdateGapMs=" + F(updates == 0 ? 0 : gapTotal * 1000 / updates) +
                    "; maxUpdateGapMs=" + F(maximumGap * 1000) + "; unsampledTailMs=" + F((stopped - previousStamp) * 1000d / Stopwatch.Frequency) +
                    "; missingSnapshots=" + missingSnapshots + "; unpairedSnapshots=" + unpairedSnapshots +
                    "; epochChanges=" + epochChanges + "; tickRegressions=" + regressions +
                    "; duplicateUpdates=" + duplicateUpdates + "; unfocusedUpdates=" + unfocusedUpdates + "; scope=unity-updates-not-presented-fps");
                Debug.Log("WORDBOUND_FRAME_CAMERA: beginCallbacks=" + beginCount + "; endCallbacks=" + endCount +
                    "; distinctBeginFrames=" + beginFrames + "; distinctEndFrames=" + endFrames +
                    "; lastBeginFrame=" + lastBeginFrame + "; lastEndFrame=" + lastEndFrame + "; matchedPairs=" + cameraPairs +
                    "; unmatchedBegins=" + unmatchedBegins + "; unmatchedEnds=" + unmatchedEnds + "; inFlight=" + pendingCamera +
                    "; crossFramePairs=" + crossedFramePairs + "; callbackElapsedMs=" + F(cameraElapsed * 1000d / Stopwatch.Frequency) +
                    "; maxCallbackElapsedMs=" + F(maximumCameraElapsed * 1000d / Stopwatch.Frequency) + "; scope=assigned-camera-callback-wall-time-not-gpu-or-present");
                for (int i = 0; i < worstCount; i++)
                    Debug.Log("WORDBOUND_FRAME_WORST: frame=" + worst[i].Frame + "; gapMs=" + F(worst[i].Gap * 1000) +
                        "; canonicalDelta=" + worst[i].Ticks + "; renderedCounterDelta=" + worst[i].RenderedDelta);
                RecordSettings("end");
                if (context != null && context.CurrentStepName == "exact_playtest")
                {
                    context.Note("frame_observation_outcome", outcome); context.Note("frame_observation_error", error);
                    context.Note("frame_observation_updates", updates); context.Note("frame_observation_seconds", elapsed);
                    context.Note("frame_observation_camera_end_frames", endFrames);
                }
            }
            catch (Exception failure) { Debug.Log("WORDBOUND_FRAME_UNAVAILABLE: report-" + failure.GetType().Name); }
        }

        void RecordSettings(string boundary)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            int quality = QualitySettings.GetQualityLevel();
            string[] names = QualitySettings.names;
            Debug.Log("WORDBOUND_FRAME_SETTINGS: boundary=" + boundary + "; quality=" + quality +
                "; qualityName=" + (quality >= 0 && quality < names.Length ? names[quality] : "unknown") +
                "; vsync=" + QualitySettings.vSyncCount + "; targetFPS=" + Application.targetFrameRate +
                "; renderInterval=" + OnDemandRendering.renderFrameInterval + "; refreshHz=" + F(Screen.currentResolution.refreshRateRatio.value) +
                "; screen=" + Screen.width + "x" + Screen.height + "; fullscreen=" + Screen.fullScreenMode +
                "; focused=" + Application.isFocused + "; background=" + Application.runInBackground +
                "; timeScale=" + F(Time.timeScale) + "; frameTimingEnabled=" + FrameTimingManager.IsFeatureEnabled() +
                "; selectedCap=" + GraphicsOptions.FrameCap + "; selectedScale=" + F(GraphicsOptions.RenderScale) +
                "; selectedAA=" + GraphicsOptions.Smoothing + "; selectedShadows=" + GraphicsOptions.Shadows +
                "; pipeline=" + (GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.GetType().Name : "builtin") +
                "; urpScale=" + F(pipeline ? pipeline.renderScale : -1) + "; urpMSAA=" + (pipeline ? pipeline.msaaSampleCount : -1) +
                "; shadowDistance=" + F(pipeline ? pipeline.shadowDistance : -1) +
                "; shadowCascades=" + (pipeline ? pipeline.shadowCascadeCount : -1) +
                "; shadowMap=" + (pipeline ? pipeline.mainLightShadowmapResolution : -1));
            if (!expected) { Debug.Log("WORDBOUND_FRAME_CAMERA_SETTINGS: boundary=" + boundary + "; camera=missing"); return; }
            expected.TryGetComponent<UniversalAdditionalCameraData>(out var data);
            Debug.Log("WORDBOUND_FRAME_CAMERA_SETTINGS: boundary=" + boundary + "; active=" + expected.isActiveAndEnabled +
                "; pixelWidth=" + expected.pixelWidth + "; pixelHeight=" + expected.pixelHeight +
                "; targetTexture=" + (expected.targetTexture != null) + "; display=" + expected.targetDisplay +
                "; cullingMask=" + expected.cullingMask + "; occlusion=" + expected.useOcclusionCulling +
                "; hdr=" + expected.allowHDR + "; msaa=" + expected.allowMSAA +
                "; fov=" + F(expected.fieldOfView) + "; orthographic=" + expected.orthographic +
                "; near=" + F(expected.nearClipPlane) + "; far=" + F(expected.farClipPlane) +
                "; urpCameraData=" + (data != null) + "; postProcessing=" + (data && data.renderPostProcessing) +
                "; cameraAA=" + (data ? data.antialiasing.ToString() : "unavailable"));
        }

        static string F(double value) => value.ToString("F3", CultureInfo.InvariantCulture);
        void OnDisable() => Stop("disabled");
        void OnDestroy() => Stop("destroyed");
    }
}
#endif
