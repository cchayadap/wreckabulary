// Game Studio Kit (MIT). The QA scenario API: ordered steps with a precondition, an action coroutine, world-space
// probes, asserts, a timeout and story-camera captures (research_qa_harness §1.4). Compiled only in the editor and in
// builds that define GSK_QA (the Release-QA and dev profiles), so shipped players carry no autoplay entry point
// (OWNER_DEFAULTS C-16).
#if GSK_QA || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace GameStudioKit.Qa
{
    /// <summary>
    /// A QA scenario. Register it with <see cref="QaScenarios.Register"/> from a
    /// [RuntimeInitializeOnLoadMethod] (survives IL2CPP stripping), or mark the class with [QaScenario("name")].
    /// Drive the game through the same input path a player uses; assert progress, not movement.
    /// </summary>
    [RequireImplementors]
    public interface IQaScenario
    {
        /// <summary>Scenario name: letters, digits, _ . - only (goldpath, smoke_frontend, soak_30min, coop_4p, perf_path…).</summary>
        string Name { get; }

        /// <summary>Builds the ordered steps. Called once per run, after the runner set up the context.</summary>
        IEnumerable<QaStep> BuildSteps(QaContext ctx);
    }

    /// <summary>Marks a scenario class for discovery by name. The class needs a public parameterless constructor.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class QaScenarioAttribute : PreserveAttribute
    {
        public string Name { get; }
        public QaScenarioAttribute(string name) { Name = name; }
    }

    /// <summary>One step. Build it fluently: QaStep.Named("enter_door_01").When(...).Do(...).Measure(...).Expect(...).</summary>
    public sealed class QaStep
    {
        public string Name;

        /// <summary>Seconds allowed for the precondition wait plus the action. Exceeding it fails the step.</summary>
        public float TimeoutSeconds = 60f;

        /// <summary>Waited on (polled once per frame) before the action runs.</summary>
        public Func<QaContext, bool> Precondition;

        /// <summary>When true, a precondition that never becomes true marks the step SKIP instead of FAIL.</summary>
        public bool Optional;

        /// <summary>The action coroutine. It may yield nested IEnumerators, YieldInstructions or null.</summary>
        public Func<QaContext, IEnumerator> Act;

        /// <summary>World-space facts after the action (name to value), written to the step's "probes".</summary>
        public Func<QaContext, IDictionary<string, object>> Probe;

        /// <summary>Asserts over the probe facts: return null to pass or a failure message.</summary>
        public readonly List<Func<IDictionary<string, object>, string>> Asserts = new List<Func<IDictionary<string, object>, string>>();

        /// <summary>Story-camera names (cameras.json) captured after the step.</summary>
        public readonly List<string> CaptureAt = new List<string>();

        public static QaStep Named(string name) => new QaStep { Name = name };

        public QaStep When(Func<QaContext, bool> precondition, bool optional = false)
        {
            Precondition = precondition;
            Optional = optional;
            return this;
        }

        public QaStep Do(Func<QaContext, IEnumerator> act) { Act = act; return this; }
        public QaStep Measure(Func<QaContext, IDictionary<string, object>> probe) { Probe = probe; return this; }
        public QaStep Expect(Func<IDictionary<string, object>, string> assert) { Asserts.Add(assert); return this; }
        public QaStep Timeout(float seconds) { TimeoutSeconds = seconds; return this; }
        public QaStep Capture(params string[] cameras) { CaptureAt.AddRange(cameras); return this; }
    }

    /// <summary>Common asserts over probe facts. Each returns null on pass or a readable failure.</summary>
    public static class QaAssert
    {
        public static Func<IDictionary<string, object>, string> AtMost(string key, double max) => facts =>
            TryNumber(facts, key, out double v) ? (v <= max + 1e-9 ? null : $"{key} = {v} > {max}") : $"{key} missing or not a number";

        public static Func<IDictionary<string, object>, string> AtLeast(string key, double min) => facts =>
            TryNumber(facts, key, out double v) ? (v >= min - 1e-9 ? null : $"{key} = {v} < {min}") : $"{key} missing or not a number";

        public static Func<IDictionary<string, object>, string> Near(string key, double expected, double tolerance) => facts =>
            TryNumber(facts, key, out double v) ? (Math.Abs(v - expected) <= tolerance ? null : $"{key} = {v}, expected {expected} ± {tolerance}") : $"{key} missing or not a number";

        public static Func<IDictionary<string, object>, string> IsTrue(string key) => facts =>
            facts != null && facts.TryGetValue(key, out var v) && v is bool b && b ? null : $"{key} is not true";

        public static bool TryNumber(IDictionary<string, object> facts, string key, out double value)
        {
            value = 0;
            if (facts == null || !facts.TryGetValue(key, out var v)) return false;
            switch (v)
            {
                case double d: value = d; return true;
                case float f: value = f; return true;
                case int i: value = i; return true;
                case long l: value = l; return true;
                default: return false;
            }
        }
    }

    /// <summary>Name → scenario factory registry. Explicit registrations win over [QaScenario] discovery.</summary>
    public static class QaScenarios
    {
        static readonly Dictionary<string, Func<IQaScenario>> Registry = new Dictionary<string, Func<IQaScenario>>(StringComparer.Ordinal);

        // Domain reload may be off: forget last session's registrations before the new session registers again.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Registry.Clear();

        public static void Register(string name, Func<IQaScenario> factory)
        {
            if (string.IsNullOrEmpty(name) || factory == null) throw new ArgumentException("QaScenarios.Register needs a name and a factory");
            Registry[name] = factory;
        }

        public static bool IsRegistered(string name) => name != null && Registry.ContainsKey(name);

        public static IEnumerable<string> RegisteredNames => Registry.Keys;

        /// <summary>Resolves a scenario: the registry first, then [QaScenario] classes, then the kit's built-ins.</summary>
        public static IQaScenario Create(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Registry.TryGetValue(name, out var f)) return f();
            var t = FindAttributed(name);
            if (t != null) return (IQaScenario)Activator.CreateInstance(t);
            if (name == SmokeFrontendScenario.ScenarioName) return new SmokeFrontendScenario();
            return null;
        }

        static Type FindAttributed(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types; }
                catch (Exception) { continue; }
                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract || !typeof(IQaScenario).IsAssignableFrom(t)) continue;
                    var attrs = t.GetCustomAttributes(typeof(QaScenarioAttribute), false);
                    if (attrs.Length > 0 && ((QaScenarioAttribute)attrs[0]).Name == name && t.GetConstructor(Type.EmptyTypes) != null) return t;
                }
            }
            return null;
        }

        /// <summary>Every known scenario name (for error messages).</summary>
        public static List<string> KnownNames()
        {
            var names = new List<string>(Registry.Keys);
            if (!names.Contains(SmokeFrontendScenario.ScenarioName)) names.Add(SmokeFrontendScenario.ScenarioName);
            names.Sort(StringComparer.Ordinal);
            return names;
        }
    }
}
#endif
