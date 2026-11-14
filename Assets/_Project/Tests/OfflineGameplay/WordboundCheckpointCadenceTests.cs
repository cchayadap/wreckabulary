using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Wreckabulary.OfflineGameplayTests
{
    public sealed class WordboundCheckpointCadenceTests
    {
        sealed class DeferredSession : IWordboundGameplaySession
        {
            public readonly TaskCompletionSource<bool> Pending = new TaskCompletionSource<bool>();
            public int Calls, Stops, Polls;
            public NetworkRunner Runner => null;
            public bool RunnerStarted => false;
            public bool GameplayReady => false;
            public string Failure => "";
            public OnlineSnapshotMark Committed => null;
            public event Action GameplayStarted { add { } remove { } }
            public event Action<string, OnlineSnapshotMark> ReplicaReceived { add { } remove { } }
            public event Action<string> Failed { add { } remove { } }
            public void ConfigureScene(SceneRef scene) { }
            public void MarkLocalReady() { }
            public void Poll() { Polls++; }
            public void SubmitCommand(Dictionary<string, object> command) => throw new NotSupportedException();
            public bool TryGetAdmittedActor(PlayerRef peer, out string actorId) { actorId = null; return false; }
            public Task<bool> PublishCheckpoint(CancellationToken cancellationToken = default) { Calls++; return Pending.Task; }
            public Task Stop() { Stops++; return Task.CompletedTask; }
        }
        sealed class Fixture : IDisposable
        {
            readonly GameObject owner = new GameObject("Wordbound cadence test");
            public readonly WordboundGameplayController Controller;
            public readonly DeferredSession Session = new DeferredSession();
            public long Now = 1000;
            public Fixture(int interval)
            {
                Controller = owner.AddComponent<WordboundGameplayController>();
                Set("session", Session);
                Set("options", new WordboundGameplayOptions { Clock = () => Now, CheckpointMilliseconds = interval });
            }
            public void Set(string name, object value) => typeof(WordboundGameplayController)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Controller, value);
            public T Get<T>(string name) => (T)typeof(WordboundGameplayController)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Controller);
            public Task Publish() => (Task)typeof(WordboundGameplayController)
                .GetMethod("PublishCheckpoint", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Controller, null);
            public NetworkRunner Replacement()
            {
                var child = new GameObject("Owned replacement runner"); child.transform.SetParent(owner.transform, false);
                return child.AddComponent<NetworkRunner>();
            }
            public void Dispose()
            {
                Session.Pending.TrySetResult(false);
                Set("runner", null);
                Object.DestroyImmediate(owner);
            }
        }
        static IEnumerator Completed(Task task)
        {
            for (int frame = 0; frame < 120 && !task.IsCompleted; frame++) yield return null;
            Assert.IsTrue(task.IsCompleted, "Deferred checkpoint completion did not resume.");
            Assert.AreEqual(TaskStatus.RanToCompletion, task.Status);
        }

        [UnityTest]
        public IEnumerator FastSuccessfulPublicationKeepsTheConfiguredStartToStartCadence()
        {
            foreach (int interval in new[] { 500, 1000 })
            {
                using var f = new Fixture(interval);
                var pending = f.Publish(); Assert.IsFalse(pending.IsCompleted);
                f.Now = 1100; f.Session.Pending.SetResult(true); yield return Completed(pending);
                Assert.AreEqual(1000L + interval, f.Get<long>("nextCheckpoint"));
                Assert.Greater(f.Get<long>("nextCheckpoint"), f.Now);
                Assert.IsFalse(f.Get<bool>("publishing")); Assert.AreEqual(1, f.Session.Calls);
            }
        }

        [UnityTest]
        public IEnumerator SlowSuccessfulPublicationBecomesDueWithoutSchedulingAnotherRequestOrOverlapping()
        {
            foreach (int interval in new[] { 500, 1000 })
            {
                using var f = new Fixture(interval);
                var pending = f.Publish();
                var duplicate = f.Publish();
                Assert.AreEqual(TaskStatus.RanToCompletion, duplicate.Status);
                Assert.AreEqual(1, f.Session.Calls); Assert.IsTrue(f.Get<bool>("publishing"));
                f.Now = 1000 + interval + 600;
                f.Session.Pending.SetResult(true); yield return Completed(pending);
                Assert.AreEqual(1000L + interval, f.Get<long>("nextCheckpoint"));
                Assert.LessOrEqual(f.Get<long>("nextCheckpoint"), f.Now);
                Assert.IsFalse(f.Get<bool>("publishing"));
                yield return null; yield return null;
                Assert.AreEqual(1, f.Session.Calls, "Completion must not create its own publication loop.");
            }
        }

        [UnityTest]
        public IEnumerator DeclinedPublicationRetainsTheFullCompletionBasedBackoff()
        {
            foreach (int interval in new[] { 500, 1000 })
            {
                using var f = new Fixture(interval);
                var pending = f.Publish(); f.Now = 2600;
                f.Session.Pending.SetResult(false); yield return Completed(pending);
                Assert.AreEqual(2600L + interval, f.Get<long>("nextCheckpoint"));
                Assert.IsFalse(f.Get<bool>("publishing")); Assert.AreEqual(1, f.Session.Calls);
            }
        }

        [UnityTest]
        public IEnumerator ThrowingPublicationStillFaultsAndStopsWithNoAutomaticRetry()
        {
            using var f = new Fixture(500);
            var pending = f.Publish(); f.Now = 2600;
            f.Session.Pending.SetException(new IOException("Simulated checkpoint failure"));
            yield return Completed(pending);
            Assert.AreEqual("IOException: Simulated checkpoint failure", f.Controller.Fault);
            Assert.AreEqual(1, f.Session.Stops); Assert.AreEqual(3100L, f.Get<long>("nextCheckpoint"));
            f.Set("initialized", true);
            Assert.IsTrue(f.Get<bool>("initialized")); Assert.IsTrue(f.Get<bool>("stopping"));
            Assert.IsNotEmpty(f.Controller.Fault);
            f.Now = 10000; f.Controller.Poll();
            Assert.AreEqual(0, f.Session.Polls, "A ready controller must stop before polling its session after a publication fault.");
            Assert.AreEqual(1, f.Session.Calls);
        }

        [UnityTest]
        public IEnumerator OldRunnerCompletionCannotRescheduleOrUnlockTheReplacement()
        {
            using var f = new Fixture(500);
            var pending = f.Publish();
            f.Set("runner", f.Replacement()); f.Set("nextCheckpoint", 987654L); f.Set("publishing", true);
            f.Now = 2600; f.Session.Pending.SetResult(true); yield return Completed(pending);
            Assert.AreEqual(987654L, f.Get<long>("nextCheckpoint"));
            Assert.IsTrue(f.Get<bool>("publishing")); Assert.AreEqual(1, f.Session.Calls);
        }

        [UnityTest]
        public IEnumerator StopDuringPublicationPreventsAnyLaterPollDispatch()
        {
            using var f = new Fixture(500);
            var pending = f.Publish(); var stop = f.Controller.Stop(); yield return Completed(stop);
            f.Now = 2600; f.Session.Pending.SetResult(true); yield return Completed(pending);
            f.Set("initialized", true);
            Assert.IsTrue(f.Get<bool>("initialized")); Assert.IsTrue(f.Get<bool>("stopping"));
            Assert.AreEqual("", f.Controller.Fault);
            f.Now = 10000; f.Controller.Poll();
            Assert.AreEqual(0, f.Session.Polls, "A ready stopped controller must stop before polling its session.");
            Assert.AreEqual(1, f.Session.Calls); Assert.AreEqual(1, f.Session.Stops);
            Assert.IsTrue(f.Controller.IsSimulationPaused);
        }
    }
}
