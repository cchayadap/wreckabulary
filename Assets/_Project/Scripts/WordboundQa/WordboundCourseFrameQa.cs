#if UNITY_EDITOR || GSK_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameStudioKit.Qa;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Qa
{
    static class WordboundCourseFrameQa
    {
        public const string LowTierVariable = "WRECKABULARY_QA_LOW_TIER";
        const string LowPreset = "Low", FixtureSeat = "qa-fixture-seat-", FixtureMatch = "qa-seat-strip-";
        const int SampleFrames = 240, WarmupFrames = 30, StripFrames = 4, TeamCount = 8;
        const float PhaseSeconds = 12f, WarmupSeconds = 1.5f, Percentile = .95f, FallbackHealth = 100f;
        const int StripWidth = 1280, StripHeight = 720;
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        static readonly (string Name, bool Town, bool Skyline)[] Phases =
        {
            ("all_on", true, true), ("skyline_off", true, false), ("backdrop_off", false, true), ("all_on_again", true, true)
        };
        static readonly int[][] SeatTeams = { new[] { 0, 1 }, new[] { 0, 1, 2 }, new[] { 0, 1, 0, 1 } };
        static readonly float[] SeatHealth = { 1f, .64f, .3f, 0f };

        public static bool LowTierRequested => Environment.GetEnvironmentVariable(LowTierVariable) == "1";

        public sealed class LowTier : IDisposable
        {
            LobbyExperienceQa.Preference[] saved;

            public bool Forced { get; private set; }

            public static LowTier Begin(QaContext context, string tag)
            {
                var tier = new LowTier();
                if (!LowTierRequested) return tier;
                string before = GraphicsOptions.Preset;
                tier.saved = GraphicsOptions.Keys.Select(key => LobbyExperienceQa.Preference.Text(key)).ToArray();
                tier.Forced = true;
                GraphicsOptions.UsePreset(LowPreset);
                if (!MapSurroundings.UseLowTier)
                {
                    tier.Dispose();
                    throw new QaFailException("The QA low-tier switch did not select the Low course town.");
                }
                context.Note("qa_low_tier_" + tag, new Dictionary<string, object> {
                    ["variable"] = LowTierVariable, ["preset_before"] = before, ["preset"] = GraphicsOptions.Preset,
                    ["render_scale"] = GraphicsOptions.RenderScale, ["smoothing"] = GraphicsOptions.Smoothing,
                    ["shadows"] = GraphicsOptions.Shadows, ["use_low_tier"] = MapSurroundings.UseLowTier,
                    ["scope"] = "QA only: the scenario applies the player's own Low graphics preset before this course and restores the saved graphics preferences after it. No shipping code reads the variable." });
                return tier;
            }

            public void Dispose()
            {
                if (saved == null) return;
                var values = saved;
                saved = null;
                foreach (var value in values) value.Restore();
                PlayerPrefs.Save();
                GraphicsOptions.Load();
            }
        }

        sealed class Sample
        {
            public readonly List<double> Frames = new();
            public double Cpu, Gpu, Seconds;
            public int Timed;
        }

        public static IEnumerator FrameTimes(QaContext context, string tag, Transform course, Camera game, bool forcedLow)
        {
            var backdrop = Object.FindObjectsByType<WordboundCourseBackdrop>(FindObjectsSortMode.None)
                .FirstOrDefault(item => item.World && item.World.transform == course && item.Root && item.Layout != null);
            if (!backdrop || !game || !game.isActiveAndEnabled)
                throw new QaFailException("Frame timing needs the built course town and the active game camera.");
            var root = backdrop.Root;
            var layout = backdrop.Layout;
            var skyline = root.transform.Find(CourseSkylineMesh.RootName);
            bool timing = FrameTimingManager.IsFeatureEnabled();
            var timings = new FrameTiming[1];
            var facts = new Dictionary<string, object> {
                ["course"] = layout.CourseId, ["tier"] = layout.Tier.ToString(), ["forced_low"] = forcedLow,
                ["preset"] = GraphicsOptions.Preset, ["render_scale"] = GraphicsOptions.RenderScale,
                ["town_renderers"] = layout.Renderers, ["town_triangles"] = layout.Triangles,
                ["skyline_renderers"] = layout.Skyline.Renderers, ["skyline_triangles"] = layout.Skyline.Triangles,
                ["skyline_child_found"] = skyline != null, ["camera"] = game.name,
                ["camera_position"] = Vector(game.transform.position), ["camera_forward"] = Vector(game.transform.forward),
                ["screen"] = Screen.width + "x" + Screen.height, ["vsync"] = QualitySettings.vSyncCount,
                ["target_frame_rate"] = Application.targetFrameRate, ["frame_timing_stats"] = timing,
                ["gpu"] = SystemInfo.graphicsDeviceName, ["focused"] = Application.isFocused,
                ["sample_frames"] = SampleFrames, ["phase_seconds_cap"] = PhaseSeconds };
            try
            {
                foreach (var phase in Phases)
                {
                    root.SetActive(phase.Town);
                    if (skyline) skyline.gameObject.SetActive(phase.Skyline);
                    yield return Warm();
                    var (renderers, triangles) = Visible();
                    var sample = new Sample();
                    yield return Measure(sample, timing, timings);
                    Record(facts, phase.Name, sample, renderers, triangles, root);
                }
            }
            finally
            {
                if (skyline) skyline.gameObject.SetActive(true);
                if (root) root.SetActive(true);
            }
            facts["scope"] = "Frame time from the game camera behind the idle local player, measured with the whole course town and skyline on, the skyline alone off, the whole town off, then everything on again. Wall-clock frames on a shared machine; vsync and the frame cap are recorded, GPU and CPU times only when frame timing stats are on.";
            context.Note("solo_" + tag + "_frame_times", facts);
        }

        static IEnumerator Warm()
        {
            double start = Time.realtimeSinceStartupAsDouble;
            int first = Time.frameCount;
            while (Time.frameCount - first < WarmupFrames && Time.realtimeSinceStartupAsDouble - start < WarmupSeconds) yield return null;
        }

        static IEnumerator Measure(Sample into, bool timing, FrameTiming[] timings)
        {
            double start = Time.realtimeSinceStartupAsDouble;
            yield return null;
            while (into.Frames.Count < SampleFrames && Time.realtimeSinceStartupAsDouble - start < PhaseSeconds)
            {
                into.Frames.Add(Time.unscaledDeltaTime * 1000.0);
                if (timing)
                {
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].cpuFrameTime > 0)
                    { into.Cpu += timings[0].cpuFrameTime; into.Gpu += timings[0].gpuFrameTime; into.Timed++; }
                }
                yield return null;
            }
            into.Seconds = Time.realtimeSinceStartupAsDouble - start;
        }

        static void Record(Dictionary<string, object> facts, string name, Sample sample, int renderers, long triangles, GameObject root)
        {
            var sorted = sample.Frames.OrderBy(value => value).ToArray();
            int count = sorted.Length;
            facts[name + "_frames"] = count;
            facts[name + "_seconds"] = Math.Round(sample.Seconds, 3);
            facts[name + "_avg_ms"] = count > 0 ? Math.Round(sorted.Average(), 3) : (object)"not recorded";
            facts[name + "_p95_ms"] = count > 0 ? Math.Round(sorted[Mathf.Clamp(Mathf.CeilToInt(count * Percentile) - 1, 0, count - 1)], 3) : (object)"not recorded";
            facts[name + "_max_ms"] = count > 0 ? Math.Round(sorted[count - 1], 3) : (object)"not recorded";
            facts[name + "_cpu_avg_ms"] = sample.Timed > 0 ? Math.Round(sample.Cpu / sample.Timed, 3) : (object)"not recorded";
            facts[name + "_gpu_avg_ms"] = sample.Timed > 0 ? Math.Round(sample.Gpu / sample.Timed, 3) : (object)"not recorded";
            facts[name + "_scene_renderers"] = renderers;
            facts[name + "_scene_triangles"] = triangles;
            facts[name + "_town_renderers_active"] = root.activeInHierarchy ? root.GetComponentsInChildren<Renderer>(false).Length : 0;
        }

        static (int Renderers, long Triangles) Visible()
        {
            int renderers = 0;
            long triangles = 0;
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                renderers++;
                var mesh = renderer.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
                if (!mesh) continue;
                for (int i = 0; i < mesh.subMeshCount; i++)
                    if (mesh.GetTopology(i) == MeshTopology.Triangles) triangles += (long)mesh.GetIndexCount(i) / 3;
            }
            return (renderers, triangles);
        }

        static string Vector(Vector3 value) => $"{value.x:0.00},{value.y:0.00},{value.z:0.00}";

        public static IEnumerator SeatStrip(QaContext context, string tag, WordboundGameplayController gameplay,
            WordboundMatchHud hud, Func<string, IEnumerator> shot)
        {
            var frameField = typeof(WordboundMatchHud).GetField("frame", Hidden);
            var healthField = typeof(WordboundMatchHud).GetField("healthMax", Hidden);
            if (frameField == null || healthField == null || !gameplay || !hud)
                throw new QaFailException("The seat strip fixture needs the live match HUD.");
            float stored = (float)healthField.GetValue(hud);
            float max = stored > 0f ? stored : FallbackHealth;
            int[] teams = null;
            void Feed(MatchSnapshot next)
            {
                if (teams != null && next != null && hud) frameField.SetValue(hud, Fixture(next, teams, max));
            }
            gameplay.FrameChanged += Feed;
            try
            {
                foreach (var arrangement in SeatTeams)
                {
                    var real = gameplay.Frame;
                    if (real == null || real.Binding.Seats.Count != 1)
                        throw new QaFailException("The seat strip fixture starts from the real one-seat local frame.");
                    teams = arrangement;
                    frameField.SetValue(hud, Fixture(real, teams, max));
                    yield return context.WaitFrames(StripFrames);
                    var shown = frameField.GetValue(hud) as MatchSnapshot;
                    if (shown == null || shown.Binding.Seats.Count != arrangement.Length)
                        throw new QaFailException("The HUD did not keep the fixture seats.");
                    if (gameplay.Frame.Binding.Seats.Count != 1 || gameplay.Frame.Actors.Count != real.Actors.Count)
                        throw new QaFailException("The seat strip fixture reached the authoritative frame.");
                    string name = "solo_" + tag + "_seats" + arrangement.Length;
                    yield return shot(name);
                    var strip = (Rect)typeof(WordboundMatchHud).GetField("seatStripRect", Hidden).GetValue(hud);
                    context.Note(name, new Dictionary<string, object> {
                        ["seats"] = arrangement.Length, ["teams"] = string.Join(",", arrangement),
                        ["fixture_health"] = string.Join(",", arrangement.Select((_, i) => i == 0 ? "real local actor" : (max * SeatHealth[i]).ToString("0"))),
                        ["hud_seat_count"] = typeof(WordboundMatchHud).GetField("seatCount", Hidden).GetValue(hud),
                        ["hud_seat_split"] = typeof(WordboundMatchHud).GetField("seatSplit", Hidden).GetValue(hud),
                        ["hud_two_teams"] = typeof(WordboundMatchHud).GetField("seatTwoTeams", Hidden).GetValue(hud),
                        ["strip_rect"] = $"{strip.x:0},{strip.y:0},{strip.width:0},{strip.height:0}",
                        ["screen"] = Screen.width + "x" + Screen.height,
                        ["at_1280x720"] = Screen.width == StripWidth && Screen.height == StripHeight,
                        ["authoritative_seats"] = gameplay.Frame.Binding.Seats.Count,
                        ["real"] = "The running local Single-runner course, its world, camera, local player, HUD code, fonts and layout, the local seat's health and the course clock.",
                        ["fixture"] = "Only the HUD's private frame is replaced by a real MatchSnapshot/MatchBinding built from the live frame with extra seats and actor health for the strip. The simulation, physics, network and other seats' bodies do not exist; this does not prove online play." });
                }
            }
            finally
            {
                teams = null;
                gameplay.FrameChanged -= Feed;
                if (hud && gameplay) frameField.SetValue(hud, gameplay.Frame);
            }
            yield return context.WaitFrames(2);
        }

        static MatchSnapshot Fixture(MatchSnapshot real, int[] teams, float max)
        {
            var binding = real.Binding;
            var local = binding.Seats.Single();
            var seats = new List<WordboundSeat>();
            var actors = real.Actors.ToList();
            for (int i = 0; i < teams.Length; i++)
            {
                int side = (local.Team + teams[i]) % TeamCount, slot = teams.Take(i).Count(team => team == teams[i]);
                string id = i == 0 ? local.PlayerId : FixtureSeat + (i + 1);
                seats.Add(new WordboundSeat(id, side, slot));
                if (i > 0) actors.Add(new MatchActorState(id, max * SeatHealth[i], max));
            }
            var fixture = new MatchBinding(FixtureMatch + teams.Length, binding.ProtocolVersion, binding.ModeHash, binding.CourseHash,
                binding.ManifestHash, binding.RosterHash, binding.CatalogueHash, seats);
            return new MatchSnapshot(fixture, real.Epoch, real.Sequence, real.NextWorldEntityId, real.Progress, real.Economy, real.Commands,
                actors, real.Bodies, real.Resources, real.Mechanisms, real.RandomStreams, real.NarrativeFlags, real.ThrownFlights, real.Practice);
        }
    }
}
#endif
