using System;
using System.Collections;
using System.Linq;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class TestFixtureAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TestAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class SetUpAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class TestCaseAttribute : Attribute
    {
        public object[] Arguments { get; }
        public TestCaseAttribute(params object[] arguments) { Arguments = arguments ?? new object[] { null }; }
    }

    public sealed class AssertionException : Exception
    {
        public AssertionException(string message) : base(message) { }
    }

    public static class Assert
    {
        static string With(string message, string detail) => string.IsNullOrEmpty(message) ? detail : message + "\n  " + detail;

        public static void Fail(string message = null) => throw new AssertionException(message ?? "failed");

        public static void Pass() { }

        public static void IsTrue(bool condition, string message = null)
        {
            if (!condition) throw new AssertionException(With(message, "expected true"));
        }

        public static void IsFalse(bool condition, string message = null)
        {
            if (condition) throw new AssertionException(With(message, "expected false"));
        }

        public static void IsNull(object value, string message = null)
        {
            if (value != null) throw new AssertionException(With(message, $"expected null but was <{value}>"));
        }

        public static void IsNotNull(object value, string message = null)
        {
            if (value == null) throw new AssertionException(With(message, "expected a value but was null"));
        }

        public static void IsEmpty(IEnumerable values, string message = null)
        {
            var list = values.Cast<object>().ToList();
            if (list.Count != 0) throw new AssertionException(With(message, "expected empty but had:\n    " + string.Join("\n    ", list)));
        }

        public static void IsNotEmpty(IEnumerable values, string message = null)
        {
            if (!values.Cast<object>().Any()) throw new AssertionException(With(message, "expected at least one item"));
        }

        static bool Same(object expected, object actual)
        {
            if (expected == null || actual == null) return expected == null && actual == null;
            if (IsNumber(expected) && IsNumber(actual)) return Convert.ToDouble(expected) == Convert.ToDouble(actual);
            return expected.Equals(actual);
        }

        static bool IsNumber(object o) => o is int || o is long || o is float || o is double || o is short || o is byte || o is ushort || o is uint || o is ulong;

        public static void AreEqual(object expected, object actual, string message = null)
        {
            if (!Same(expected, actual)) throw new AssertionException(With(message, $"expected <{expected}> but was <{actual}>"));
        }

        public static void AreEqual(double expected, double actual, double delta, string message = null)
        {
            if (Math.Abs(expected - actual) > delta) throw new AssertionException(With(message, $"expected <{expected}> +/- {delta} but was <{actual}>"));
        }

        public static void AreNotEqual(object expected, object actual, string message = null)
        {
            if (Same(expected, actual)) throw new AssertionException(With(message, $"expected anything but <{expected}>"));
        }

        public static void AreSame(object expected, object actual, string message = null)
        {
            if (!ReferenceEquals(expected, actual)) throw new AssertionException(With(message, "expected the same object"));
        }

        public static void Greater(double a, double b, string message = null)
        {
            if (!(a > b)) throw new AssertionException(With(message, $"expected {a} > {b}"));
        }

        public static void GreaterOrEqual(double a, double b, string message = null)
        {
            if (!(a >= b)) throw new AssertionException(With(message, $"expected {a} >= {b}"));
        }

        public static void Less(double a, double b, string message = null)
        {
            if (!(a < b)) throw new AssertionException(With(message, $"expected {a} < {b}"));
        }

        public static void LessOrEqual(double a, double b, string message = null)
        {
            if (!(a <= b)) throw new AssertionException(With(message, $"expected {a} <= {b}"));
        }

        public static T Throws<T>(TestDelegate code, string message = null) where T : Exception
        {
            try { code(); }
            catch (T e) when (e.GetType() == typeof(T)) { return e; }
            catch (Exception e) { throw new AssertionException(With(message, $"expected {typeof(T).Name} but got {e.GetType().Name}: {e.Message}")); }
            throw new AssertionException(With(message, $"expected {typeof(T).Name} but nothing was thrown"));
        }

        public static void DoesNotThrow(TestDelegate code, string message = null)
        {
            try { code(); }
            catch (Exception e) { throw new AssertionException(With(message, $"expected no exception but got {e.GetType().Name}: {e.Message}")); }
        }
    }

    public delegate void TestDelegate();

    public static class CollectionAssert
    {
        public static void AreEqual(IEnumerable expected, IEnumerable actual, string message = null)
        {
            var e = expected.Cast<object>().ToList();
            var a = actual.Cast<object>().ToList();
            if (e.Count != a.Count || e.Where((x, i) => !Equals(x, a[i])).Any())
                throw new AssertionException((message == null ? "" : message + "\n  ") +
                    $"expected [{string.Join(", ", e)}] but was [{string.Join(", ", a)}]");
        }

        public static void AreEquivalent(IEnumerable expected, IEnumerable actual, string message = null)
        {
            var e = expected.Cast<object>().Select(x => x?.ToString()).OrderBy(x => x, StringComparer.Ordinal).ToList();
            var a = actual.Cast<object>().Select(x => x?.ToString()).OrderBy(x => x, StringComparer.Ordinal).ToList();
            if (!e.SequenceEqual(a))
                throw new AssertionException((message == null ? "" : message + "\n  ") +
                    $"expected the same items as [{string.Join(", ", e)}] but was [{string.Join(", ", a)}]");
        }

        public static void Contains(IEnumerable collection, object item, string message = null)
        {
            if (!collection.Cast<object>().Contains(item))
                throw new AssertionException((message == null ? "" : message + "\n  ") + $"expected the collection to contain <{item}>");
        }

        public static void DoesNotContain(IEnumerable collection, object item, string message = null)
        {
            if (collection.Cast<object>().Contains(item))
                throw new AssertionException((message == null ? "" : message + "\n  ") + $"expected the collection not to contain <{item}>");
        }
    }
}
