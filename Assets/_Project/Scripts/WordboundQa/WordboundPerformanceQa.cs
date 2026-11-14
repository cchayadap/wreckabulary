#if UNITY_EDITOR || GSK_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameStudioKit.Qa;
using UnityEngine;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;
using Object = UnityEngine.Object;

namespace Wreckabulary.Qa
{
    public sealed class WordboundPerformanceQa : IQaScenario
    {
        const string Scenario = "wordbound_synthetic8_low", Seed = "0314159265358979";
        const float SampleSeconds = 60;
        WordboundLauncher launcher;
        WordboundWorkshop workshop;
        WordboundLocalMatchClient client;
        WordboundGameplayPresentation presentation;
        WordboundPerformanceWorkload workload;
        string manifest;

        public string Name => Scenario;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register() => QaScenarios.Register(Scenario, () => new WordboundPerformanceQa());

        public IEnumerable<QaStep> BuildSteps(QaContext context)
        {
            yield return QaStep.Named("prepare_exact_local_flagship").Timeout(150).Do(Prepare);
            yield return QaStep.Named("synthetic_render_measurement").Timeout(120).Do(Measure);
            yield return QaStep.Named("restore_settings_and_close_local_match").Timeout(45).Do(Cleanup);
        }

        IEnumerator Prepare(QaContext context)
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer || Application.isBatchMode ||
                PerfProbe.BuildKind != "release-qa")
                throw new QaFailException("Use a presented release-QA Windows player; Editor and headless timings are not accepted.");
            var root = new GameObject("Wordbound synthetic performance workload");
            Object.DontDestroyOnLoad(root);
            workload = root.AddComponent<WordboundPerformanceWorkload>();
            yield return context.WaitUntil(() => Object.FindFirstObjectByType<LobbyMenu>() != null, 45, "the real lobby");
            Object.FindFirstObjectByType<LobbyMenu>().OpenWordbound();
            yield return context.WaitUntil(() => {
                launcher = Object.FindFirstObjectByType<WordboundLauncher>();
                return launcher && launcher.GetComponent<WordboundPartyMenu>()?.Coordinator != null;
            }, 45, "the Wordbound launcher");
            workload.ConfigureDisplay();
            yield return context.WaitUntil(() => Screen.width == 1920 && Screen.height == 1080, 15, "the actual 1080p output");
            string folder = Path.GetFullPath(Path.Combine(context.OutDir, "synthetic-draft-" + Guid.NewGuid().ToString("N")));
            launcher.ConfigureQaWorkshopStorage(folder);
            context.Note("workshop_storage_folder", folder);
            launcher.SendMessage("OpenWorkshop", SendMessageOptions.RequireReceiver);
            workshop = launcher.GetComponent<WordboundWorkshop>();
            if (!workshop) throw new QaFailException("The launcher did not open the workshop.");
            if (workshop.RecoveryChoicePending) throw new QaFailException("The isolated QA workshop unexpectedly requires recovery.");
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            var items = ItemCatalogue.FromJson(data.items.text);
            workshop.OpenFlagship(WordboundFlagshipContent.Create(items));
            workshop.History.Execute("QA exact synthetic workload seed", draft => { draft.Seed = Seed; draft.SeedLocked = true; });
            var preview = workshop.Preview();
            if (!preview.Success) throw new QaFailException("The exact flagship draft failed validation.");
            manifest = preview.Manifest.Hash;
            workshop.RequestPlaytest();
            yield return context.WaitUntil(() => {
                client = launcher.GetComponentInChildren<WordboundLocalMatchClient>();
                return client && client.Gameplay && client.Gameplay.CourseWorld && client.Gameplay.Frame != null &&
                    !client.Gameplay.IsSimulationPaused;
            }, 60, "the running exact local map");
            RequireLocalWorld();
            presentation = client.GetComponent<WordboundGameplayPresentation>();
            if (!presentation) throw new QaFailException("The local world presentation is missing.");
            workload.Prepare(client, items);
            workload.RequireDisplay();
            context.Note("workload_kind", "synthetic-render-actors-with-held-equipment; not network or active-combat acceptance");
            context.Note("seed", Seed); context.Note("manifest_hash", manifest);
            context.Note("catalogue_hash", client.Gameplay.Frame.Binding.CatalogueHash);
            context.Note("mode_hash", client.Gameplay.Frame.Binding.ModeHash);
            context.Note("world_actor_count", client.Gameplay.Frame.Actors.Count);
            context.Note("world_player_body_count", client.Gameplay.Frame.Bodies.Count(body => body.Kind == WorldBodyKind.Player));
            context.Note("world_body_count", client.Gameplay.Frame.Bodies.Count);
            context.Note("synthetic_actor_count", workload.SyntheticActors); context.Note("held_item_count", workload.HeldItems);
            context.Note("total_world_and_synthetic_actors", client.Gameplay.Frame.Actors.Count + workload.SyntheticActors);
            context.Note("remote_network_peers", 0); context.Note("local_session_count", 1);
            context.Note("equipment", string.Join(",", WordboundPerformanceWorkload.Equipment));
            context.Note("artwork_resolver", "same initialized instance as the running local world");
            context.Note("base_artwork_source", workload.BaseArtworkSource);
            context.Note("visual_profile", workload.VisualProfileName);
            context.Note("held_item_artwork", workload.Artwork);
            context.Note("synthetic_renderer_count", workload.RendererCount); context.Note("held_item_triangles", workload.ItemTriangles);
            context.Note("camera_module", workload.ModuleId); context.Note("width", Screen.width); context.Note("height", Screen.height);
            context.Note("camera_path", "fixed room center; height6.5m, z=-11.5m, x=0.8*sin(t*0.18); FOV60");
            context.Note("gameplay_hud_enabled", false);
            context.Note("quality_preset", GraphicsOptions.Preset); context.Note("render_scale", GraphicsOptions.RenderScale);
            context.Note("cpu", SystemInfo.processorType); context.Note("gpu", SystemInfo.graphicsDeviceName);
            context.Note("graphics_api", SystemInfo.graphicsDeviceType.ToString());
            context.Note("system_memory_mb", SystemInfo.systemMemorySize); context.Note("gpu_memory_mb", SystemInfo.graphicsMemorySize);
            yield return context.WaitFrames(2);
            if (workload.ActorsInCamera() != 8) throw new QaFailException("All eight synthetic avatars must be in the measured camera frustum.");
            yield return context.CaptureScreen("synthetic_start");
        }

        IEnumerator Measure(QaContext context)
        {
            if (!workload || !workload.Ready) throw new QaFailException("Synthetic setup did not complete.");
            float warmupStart = Time.realtimeSinceStartup;
            int warmupFrame = Time.frameCount;
            while (Time.realtimeSinceStartup - warmupStart < 3 || Time.frameCount - warmupFrame < 120)
            { RequireHealthy(); yield return null; }
            context.Note("warmup_seconds", Time.realtimeSinceStartup - warmupStart);
            context.Note("warmup_frames", Time.frameCount - warmupFrame);
            context.Note("sample_start_pose_seconds", workload.Elapsed);
            var probe = PerfProbe.GetOrCreate();
            string startedUtc = DateTime.UtcNow.ToString("O");
            probe.Begin(Scenario, 0);
            float sampleStart = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - sampleStart < SampleSeconds)
            { RequireHealthy(); yield return null; }
            float sampleEnd = Time.realtimeSinceStartup;
            string endedUtc = DateTime.UtcNow.ToString("O");
            workload.Animate = false;
            var report = probe.Finish(true, context.OutDir);
            RequireLocalWorld();
            context.Note("sample_start_utc", startedUtc); context.Note("sample_end_utc", endedUtc);
            context.Note("sample_start_realtime_seconds", sampleStart); context.Note("sample_end_realtime_seconds", sampleEnd);
            context.Note("measured_seconds", sampleEnd - sampleStart);
            context.Note("sampled_frames", report.Frames); context.Note("frame_ms_p50", report.FrameMsP50);
            context.Note("frame_ms_p95", report.FrameMsP95); context.Note("frame_ms_p99", report.FrameMsP99);
            context.Note("worst_frame_ms", report.WorstFrameMs); context.Note("sample_end_pose_seconds", workload.Elapsed);
            int visible = workload.ActorsInCamera();
            context.Note("actors_in_camera_at_end", visible);
            if (visible != 8) context.Fail("All eight synthetic actors must remain in the camera at the end of measurement.");
            if (report.Frames < 120 || double.IsNaN(report.FrameMsP95) || report.FrameMsP95 > 16.6)
                context.Fail("Synthetic render workload exceeds the 16.6 ms p95 target or collected too few frames.");
            yield return context.CaptureScreen("synthetic_end");
            workload.SetPose(30, 1f / 60);
            yield return context.WaitFrames(2);
            context.Note("reference_pose_capture", "30-second pose replayed after measurement; screenshot capture time is excluded");
            yield return context.CaptureScreen("synthetic_reference_pose");
        }

        void RequireLocalWorld()
        {
            if (!client || !client.Gameplay || client.Gameplay.Fault != "" || client.Gameplay.IsSimulationPaused ||
                client.Gameplay.CourseWorld.ManifestHash != manifest || client.Gameplay.Frame.Actors.Count != 1 ||
                client.Gameplay.Frame.Actors[0].PlayerId != WordboundLocalMatchClient.LocalActor ||
                Object.FindObjectsByType<WordboundLocalMatchClient>(FindObjectsSortMode.None).Length != 1 ||
                Object.FindFirstObjectByType<WordboundMatchClient>() != null)
                throw new QaFailException("The measured scene must remain the exact one-actor local world with no online match client.");
        }

        void RequireHealthy()
        {
            workload.RequireDisplay();
            workload.RequireArtwork();
            if (!Application.isFocused) throw new QaFailException("The presented performance player lost focus.");
            if (!client || !client.Gameplay || client.Gameplay.Fault != "" || client.Gameplay.IsSimulationPaused ||
                !client.Gameplay.CourseWorld || client.Gameplay.Frame == null ||
                client.Gameplay.CourseWorld.ManifestHash != manifest || client.Gameplay.Frame.Actors.Count != 1 ||
                !presentation || presentation.Fault != "")
                throw new QaFailException("The actual local world or presentation changed or faulted during measurement.");
        }

        IEnumerator Cleanup(QaContext context)
        {
            if (workload) workload.Restore();
            if (client)
            {
                var close = client.ReturnToWorkshop();
                yield return context.WaitUntil(() => close.IsCompleted, 30, "local gameplay shutdown");
                if (close.IsFaulted || close.IsCanceled) context.Fail("The local performance playtest did not shut down cleanly.");
            }
            if (workload) Object.Destroy(workload.gameObject);
            yield return context.WaitFrames(2);
            context.Note("settings_restored", true);
            if (client != null) context.Fail("The local performance client remains after shutdown.");
        }
    }
}
#endif
