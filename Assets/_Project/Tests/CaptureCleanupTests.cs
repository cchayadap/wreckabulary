#if UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor;

namespace Wreckabulary.Tests
{
    public class CaptureCleanupTests
    {
        bool originalValue;
        CaptureTests fixture;

        [SetUp]
        public void RememberOriginalSetting()
        {
            originalValue = EditorSettings.asyncShaderCompilation;
        }

        [TearDown]
        public void RestoreOriginalSetting()
        {
            fixture?.RestoreShaderCompilation();
            EditorSettings.asyncShaderCompilation = originalValue;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CompletionRestoresThePriorSetting(bool priorValue)
        {
            var step = new object();
            bool sequenceDisposed = false;
            var routine = Start(priorValue, YieldOnce(step, () => sequenceDisposed = true));
            try
            {
                Assert.IsTrue(routine.MoveNext());
                Assert.AreSame(step, routine.Current, "the capture's original yield is preserved");
                Assert.IsFalse(EditorSettings.asyncShaderCompilation);
                Assert.IsFalse(routine.MoveNext());
                Assert.IsTrue(sequenceDisposed);
                Assert.AreEqual(priorValue, EditorSettings.asyncShaderCompilation);
            }
            finally
            {
                ((IDisposable)routine).Dispose();
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExceptionRestoresThePriorSetting(bool priorValue)
        {
            bool sequenceDisposed = false;
            var routine = Start(priorValue, FailAfterYield(() => sequenceDisposed = true));
            try
            {
                Assert.IsTrue(routine.MoveNext());
                Assert.IsFalse(EditorSettings.asyncShaderCompilation);
                Assert.Throws<InvalidOperationException>(() => routine.MoveNext());
                Assert.IsTrue(sequenceDisposed);
                Assert.AreEqual(priorValue, EditorSettings.asyncShaderCompilation);
            }
            finally
            {
                ((IDisposable)routine).Dispose();
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisposingAnInterruptedIteratorRestoresThePriorSetting(bool priorValue)
        {
            bool sequenceDisposed = false;
            var routine = Start(priorValue, YieldOnce(null, () => sequenceDisposed = true));
            try
            {
                Assert.IsTrue(routine.MoveNext());
                Assert.IsFalse(EditorSettings.asyncShaderCompilation);
            }
            finally
            {
                ((IDisposable)routine).Dispose();
            }

            Assert.IsTrue(sequenceDisposed, "cancellation also disposes the capture sequence");
            Assert.AreEqual(priorValue, EditorSettings.asyncShaderCompilation);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TearDownRestoresAnIteratorAbandonedByTheRunner(bool priorValue)
        {
            bool sequenceDisposed = false;
            var routine = Start(priorValue, YieldOnce(null, () => sequenceDisposed = true));
            try
            {
                Assert.IsTrue(routine.MoveNext());
                Assert.IsFalse(EditorSettings.asyncShaderCompilation);

                fixture.RestoreShaderCompilation();
                Assert.AreEqual(priorValue, EditorSettings.asyncShaderCompilation);

                EditorSettings.asyncShaderCompilation = !priorValue;
                ((IDisposable)routine).Dispose();
                Assert.IsTrue(sequenceDisposed);
                Assert.AreEqual(!priorValue, EditorSettings.asyncShaderCompilation);
            }
            finally
            {
                ((IDisposable)routine).Dispose();
            }
        }

        IEnumerator Start(bool priorValue, IEnumerator sequence)
        {
            EditorSettings.asyncShaderCompilation = priorValue;
            fixture = new CaptureTests();
            return fixture.RunWithSynchronousShaders(sequence);
        }

        static IEnumerator YieldOnce(object step, Action onDisposed)
        {
            try
            {
                yield return step;
            }
            finally
            {
                onDisposed();
            }
        }

        static IEnumerator FailAfterYield(Action onDisposed)
        {
            try
            {
                yield return null;
                throw new InvalidOperationException("capture sequence failed");
            }
            finally
            {
                onDisposed();
            }
        }
    }
}
#endif
