#if UNITY_EDITOR || GSK_QA
using System;
using GameStudioKit.Qa;
using UnityEngine;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Qa
{
    public sealed class WordboundPhaseObservation : MonoBehaviour
    {
        QaContext context;
        WordboundLauncher launcher;
        WordboundPhaseCapture capture;
        WordboundPhaseBoundary first;
        string window = "startup";

        public static WordboundPhaseObservation StartIfEnabled(WordboundLauncher launcher, QaContext context)
        {
            if (Environment.GetEnvironmentVariable("WORDBOUND_PROFILE_PHASES") != "1") return null;
            var observation = launcher.gameObject.AddComponent<WordboundPhaseObservation>();
            observation.hideFlags = HideFlags.DontSave;
            observation.context = context; observation.launcher = launcher;
            observation.first = new WordboundPhaseBoundary(null);
            observation.capture = WordboundPhaseCapture.TryStart();
            if (observation.capture == null)
            {
                Debug.Log("WORDBOUND_PHASE_UNAVAILABLE: qa-capture-already-owned");
                Destroy(observation);
                return null;
            }
            return observation;
        }

        public void Ready(MatchSnapshot frame)
        {
            if (capture == null) return;
            var boundary = new WordboundPhaseBoundary(frame);
            Stop(boundary, "local-ready");
            first = boundary; window = "ready";
            capture = WordboundPhaseCapture.TryStart();
            if (capture == null) Debug.Log("WORDBOUND_PHASE_UNAVAILABLE: qa-ready-capture-already-owned");
        }

        void Update()
        {
            if (capture != null && (!QaRunner.IsRunning || context == null ||
                context.CurrentStepName != "exact_playtest" || !launcher)) Finish("step-abandoned");
        }

        public void Finish(string outcome)
        {
            if (capture == null) return;
            var boundary = new WordboundPhaseBoundary(null);
            try
            {
                var client = launcher ? launcher.GetComponentInChildren<WordboundLocalMatchClient>() : null;
                boundary = new WordboundPhaseBoundary(client && client.Gameplay ? client.Gameplay.Frame : null);
            }
            catch (Exception error) { Debug.Log("WORDBOUND_PHASE_UNAVAILABLE: qa-boundary-" + error.GetType().Name); }
            finally { Stop(boundary, outcome); }
            Destroy(this);
        }

        void Stop(WordboundPhaseBoundary last, string outcome)
        {
            var completed = capture; capture = null;
            if (completed == null) return;
            completed.Dispose();
            try
            {
                foreach (string line in completed.Report(window, outcome, first, last)) Debug.Log(line);
                if (context != null && context.CurrentStepName == "exact_playtest")
                {
                    context.Note("phase_" + window + "_generation", completed.Generation);
                    context.Note("phase_" + window + "_invalid", completed.Invalid.ToString());
                    context.Note("phase_" + window + "_inflight", completed.InFlight);
                    context.Note("phase_" + window + "_outcome", outcome);
                }
            }
            catch (Exception error) { Debug.Log("WORDBOUND_PHASE_UNAVAILABLE: qa-report-" + error.GetType().Name); }
        }

        void OnDisable() => Finish("disabled");
        void OnDestroy() => Finish("destroyed");
    }
}
#endif
