// Game Studio Kit (MIT). Built-in fallback for the smoke_frontend scenario: a normal start reaches the first scene,
// renders frames for a few seconds without console errors, and leaves a front-end screenshot. A project registers its
// own "smoke_frontend" (menus, host a lobby, enter the first level) with QaScenarios.Register, which replaces this one.
#if GSK_QA || UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using GameStudioKit.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameStudioKit.Qa
{
    public sealed class SmokeFrontendScenario : IQaScenario
    {
        public const string ScenarioName = "smoke_frontend";
        public string Name => ScenarioName;

        /// <summary>Seconds to stay on the front end (-qa-smoke-seconds overrides).</summary>
        public float IdleSeconds = 5f;

        int _frameAtIdleStart;
        float _timeAtIdleStart;

        public IEnumerable<QaStep> BuildSteps(QaContext ctx)
        {
            float idle = KitArgs.Current.GetFloat("-qa-smoke-seconds", IdleSeconds);

            yield return QaStep.Named("boot")
                .Timeout(90f)
                .When(c => SceneManager.sceneCount > 0 && SceneManager.GetActiveScene().isLoaded && Time.frameCount > 30)
                .Measure(c => new Dictionary<string, object>
                {
                    { "active_scene", SceneManager.GetActiveScene().name },
                    { "frame_count", Time.frameCount },
                    { "cameras_enabled", Camera.allCamerasCount },
                })
                .Expect(QaAssert.AtLeast("cameras_enabled", 1));

            yield return QaStep.Named("front_end_idle")
                .Timeout(idle + 30f)
                .Do(c => Idle(idle))
                .Measure(c =>
                {
                    float elapsed = Mathf.Max(0.001f, Time.realtimeSinceStartup - _timeAtIdleStart);
                    return new Dictionary<string, object>
                    {
                        { "seconds", elapsed },
                        { "frames", Time.frameCount - _frameAtIdleStart },
                        { "fps_avg", (Time.frameCount - _frameAtIdleStart) / elapsed },
                    };
                })
                .Expect(QaAssert.AtLeast("fps_avg", 1));

            yield return QaStep.Named("front_end_screenshot")
                .Timeout(30f)
                .Do(c => Application.isBatchMode ? Skip(c) : c.CaptureScreen("front_end"));
        }

        IEnumerator Idle(float seconds)
        {
            _frameAtIdleStart = Time.frameCount;
            _timeAtIdleStart = Time.realtimeSinceStartup;
            float end = _timeAtIdleStart + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        static IEnumerator Skip(QaContext ctx)
        {
            ctx.Note("screenshot", "skipped: batchmode has no presented frame");
            yield break;
        }
    }
}
#endif
