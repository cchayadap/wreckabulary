#if UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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

        [TestCase("complete", false)]
        [TestCase("complete", true)]
        [TestCase("exception", false)]
        [TestCase("exception", true)]
        [TestCase("dispose", false)]
        [TestCase("dispose", true)]
        [TestCase("teardown", false)]
        [TestCase("teardown", true)]
        public void LobbyPreferencesRestorePresenceAndExactValues(string exit, bool existed)
        {
            string[] keys = { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey, LobbyThemes.PreferenceKey };
            string[] originals = Array.ConvertAll(keys, key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null);
            string[] expected = { "", "authored-outfit", "custom-hint", "candy" };
            IEnumerator routine = null;
            try
            {
                for (int i = 0; i < keys.Length; i++)
                    if (existed) PlayerPrefs.SetString(keys[i], expected[i]); else PlayerPrefs.DeleteKey(keys[i]);
                PlayerPrefs.Save();
                fixture = new CaptureTests();
                routine = fixture.RunWithLobbyPreferences(ChangeLobbyPreferences(keys, exit == "exception"));
                Assert.IsTrue(routine.MoveNext());
                foreach (string key in keys) Assert.AreEqual("capture-value", PlayerPrefs.GetString(key));
                if (exit == "exception") Assert.Throws<InvalidOperationException>(() => routine.MoveNext());
                else if (exit == "complete") Assert.IsFalse(routine.MoveNext());
                else if (exit == "dispose") ((IDisposable)routine).Dispose();
                else fixture.RestoreShaderCompilation();
                for (int i = 0; i < keys.Length; i++)
                {
                    Assert.AreEqual(existed, PlayerPrefs.HasKey(keys[i]), keys[i]);
                    if (existed) Assert.AreEqual(expected[i], PlayerPrefs.GetString(keys[i]), keys[i]);
                }
                PlayerPrefs.SetString(LobbyMenu.TurnHintKey, "after-cleanup");
                ((IDisposable)routine).Dispose();
                fixture.RestoreShaderCompilation();
                Assert.AreEqual("after-cleanup", PlayerPrefs.GetString(LobbyMenu.TurnHintKey), "late disposal must not restore twice");
            }
            finally
            {
                try { (routine as IDisposable)?.Dispose(); fixture?.RestoreShaderCompilation(); }
                finally
                {
                    for (int i = 0; i < keys.Length; i++)
                        if (originals[i] == null) PlayerPrefs.DeleteKey(keys[i]); else PlayerPrefs.SetString(keys[i], originals[i]);
                    PlayerPrefs.Save();
                    LobbyThemes.Restore();
                }
            }
        }

        static IEnumerator ChangeLobbyPreferences(string[] keys, bool fail)
        {
            foreach (string key in keys) PlayerPrefs.SetString(key, "capture-value");
            PlayerPrefs.Save();
            yield return null;
            if (fail) throw new InvalidOperationException("lobby capture failed");
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
