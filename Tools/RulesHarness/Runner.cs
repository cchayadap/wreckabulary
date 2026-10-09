using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Wreckabulary.RulesHarness
{
    public static class Runner
    {
        public static int Main(string[] args)
        {
            string filter = args.Length > 0 ? args[0] : null;
            int passed = 0;
            var failures = new List<string>();
            var clock = Stopwatch.StartNew();
            var fixtures = typeof(Runner).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.GetMethods().Any(IsTest))
                .OrderBy(t => t.FullName, StringComparer.Ordinal);
            foreach (var type in fixtures)
            {
                var setUp = type.GetMethods().Where(m => m.GetCustomAttribute<SetUpAttribute>() != null).ToList();
                foreach (var method in type.GetMethods().Where(IsTest).OrderBy(m => m.Name, StringComparer.Ordinal))
                {
                    var cases = method.GetCustomAttributes<TestCaseAttribute>().Select(c => c.Arguments).ToList();
                    if (cases.Count == 0) cases.Add(Array.Empty<object>());
                    foreach (var arguments in cases)
                    {
                        string name = $"{type.Name}.{method.Name}" + (arguments.Length > 0 ? $"({string.Join(", ", arguments.Select(Show))})" : "");
                        if (filter != null && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        try
                        {
                            object fixture = Activator.CreateInstance(type);
                            foreach (var s in setUp) s.Invoke(fixture, null);
                            method.Invoke(fixture, Convert(arguments, method.GetParameters()));
                            passed++;
                        }
                        catch (Exception e)
                        {
                            var inner = e is TargetInvocationException t && t.InnerException != null ? t.InnerException : e;
                            string why = inner is AssertionException ? inner.Message : $"{inner.GetType().Name}: {inner.Message}\n{inner.StackTrace}";
                            failures.Add($"FAIL {name}\n  {why}");
                        }
                    }
                }
            }
            foreach (string f in failures) Console.WriteLine(f);
            Console.WriteLine($"RULES_HARNESS passed={passed} failed={failures.Count} seconds={clock.Elapsed.TotalSeconds:0.00}");
            if (passed + failures.Count == 0)
            {
                Console.WriteLine("No tests matched.");
                return 2;
            }
            return failures.Count == 0 ? 0 : 1;
        }

        static bool IsTest(MethodInfo m) =>
            m.GetCustomAttribute<TestAttribute>() != null || m.GetCustomAttributes<TestCaseAttribute>().Any();

        static string Show(object o) => o == null ? "null" : o is string s ? $"\"{s}\"" : o.ToString();

        static object[] Convert(object[] arguments, ParameterInfo[] parameters)
        {
            if (arguments.Length != parameters.Length)
                throw new ArgumentException($"test case has {arguments.Length} arguments but the method takes {parameters.Length}");
            var result = new object[arguments.Length];
            for (int i = 0; i < arguments.Length; i++)
            {
                var target = parameters[i].ParameterType;
                var a = arguments[i];
                result[i] = a == null || target.IsInstanceOfType(a) ? a
                    : target.IsEnum ? Enum.ToObject(target, a)
                    : System.Convert.ChangeType(a, target, System.Globalization.CultureInfo.InvariantCulture);
            }
            return result;
        }
    }
}
