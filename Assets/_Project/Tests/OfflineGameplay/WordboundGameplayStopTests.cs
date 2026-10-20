using System;
using System.Collections;
using System.Collections.Generic;
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
    public sealed class WordboundGameplayStopTests
    {
        sealed class DeferredSession : IWordboundGameplaySession
        {
            public readonly TaskCompletionSource<bool> Shutdown = new TaskCompletionSource<bool>();
            public int StopCalls;
            public Action OnStop;
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
            public void Poll() { }
            public void SubmitCommand(Dictionary<string, object> command) => throw new NotSupportedException();
            public bool TryGetAdmittedActor(PlayerRef peer, out string actorId) { actorId = null; return false; }
            public Task<bool> PublishCheckpoint(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Stop()
            {
                StopCalls++;
                OnStop?.Invoke();
                return Shutdown.Task;
            }
        }

        sealed class Fixture : IDisposable
        {
            readonly GameObject owner = new GameObject("Wordbound shutdown test");
            public readonly WordboundGameplayController Controller;
            public readonly DeferredSession Session = new DeferredSession();
            public Fixture()
            {
                Controller = owner.AddComponent<WordboundGameplayController>();
                Set("session", Session);
            }
            void Set(string name, object value) => typeof(WordboundGameplayController)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Controller, value);
            public void ThrowDuringCleanup(Exception failure)
            {
                var body = new GameObject("Unspawned local body");
                body.SetActive(false);
                body.transform.SetParent(owner.transform, false);
                body.AddComponent<BoxCollider>();
                Set("localBody", body.AddComponent<WordboundNetworkBody>());
                Set("options", new WordboundGameplayOptions { LocalPlayerChanged = _ => throw failure });
            }
            public void Dispose()
            {
                Session.Shutdown.TrySetResult(true);
                Object.DestroyImmediate(owner);
            }
        }

        static IEnumerator Completed(Task task)
        {
            for (int frame = 0; frame < 120 && !task.IsCompleted; frame++) yield return null;
            Assert.IsTrue(task.IsCompleted, "Controller shutdown did not complete after its session.");
        }

        [UnityTest]
        public IEnumerator ConcurrentStopsSharePendingShutdownAndCompletion()
        {
            using var fixture = new Fixture();
            Task first = fixture.Controller.Stop(), second = fixture.Controller.Stop();
            Assert.AreSame(first, second);
            Assert.AreEqual(1, fixture.Session.StopCalls);
            Assert.IsFalse(first.IsCompleted);
            Assert.IsTrue(fixture.Controller.IsSimulationPaused);
            fixture.Session.Shutdown.SetResult(true);
            yield return Completed(first);
            Assert.AreEqual(TaskStatus.RanToCompletion, first.Status);
            Assert.AreSame(first, fixture.Controller.Stop());
            Assert.AreEqual(1, fixture.Session.StopCalls);
        }

        [UnityTest]
        public IEnumerator SessionShutdownReentrySeesTheAlreadyPublishedTask()
        {
            using var fixture = new Fixture();
            Task reentered = null;
            fixture.Session.OnStop = () => reentered = fixture.Controller.Stop();
            Task first = fixture.Controller.Stop();
            Assert.AreSame(first, reentered);
            Assert.AreEqual(1, fixture.Session.StopCalls);
            Assert.IsFalse(first.IsCompleted);
            fixture.Session.Shutdown.SetResult(true);
            yield return Completed(first);
            Assert.AreEqual(TaskStatus.RanToCompletion, first.Status);
        }

        [UnityTest]
        public IEnumerator FailureAndReturnAwaitTheSameShutdownWithoutDuplicateFaultEvents()
        {
            using var fixture = new Fixture();
            Task faultHandlerStop = null;
            int notifications = 0;
            fixture.Controller.Faulted += reason => {
                notifications++;
                Assert.AreEqual("InvalidOperationException: presentation failed", reason);
                Assert.AreEqual(0, fixture.Session.StopCalls);
                faultHandlerStop = fixture.Controller.Stop();
            };
            typeof(WordboundGameplayController).GetMethod("Fail", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(fixture.Controller, new object[] { new InvalidOperationException("presentation failed") });
            Task returnedStop = fixture.Controller.Stop();
            Assert.AreSame(faultHandlerStop, returnedStop);
            Assert.AreEqual(1, fixture.Session.StopCalls);
            Assert.AreEqual(1, notifications);
            Assert.IsFalse(returnedStop.IsCompleted);
            fixture.Session.Shutdown.SetResult(true);
            yield return Completed(returnedStop);
            Assert.AreEqual(TaskStatus.RanToCompletion, returnedStop.Status);
            Assert.AreEqual(1, notifications);
        }

        [UnityTest]
        public IEnumerator ShutdownErrorIsSharedAndDoesNotStartAnotherShutdown()
        {
            using var fixture = new Fixture();
            Task first = fixture.Controller.Stop(), second = fixture.Controller.Stop();
            var failure = new InvalidOperationException("shutdown failed");
            fixture.Session.Shutdown.SetException(failure);
            yield return Completed(first);
            Assert.AreSame(first, second);
            Assert.IsTrue(first.IsFaulted);
            Assert.AreSame(failure, first.Exception.InnerException);
            Assert.AreSame(first, fixture.Controller.Stop());
            Assert.AreEqual(1, fixture.Session.StopCalls);
        }

        [UnityTest]
        public IEnumerator ShutdownCancellationIsSharedAndDoesNotReportSuccessfulReturn()
        {
            using var fixture = new Fixture();
            Task first = fixture.Controller.Stop(), second = fixture.Controller.Stop();
            fixture.Session.Shutdown.SetCanceled();
            yield return Completed(first);
            Assert.AreSame(first, second);
            Assert.IsTrue(first.IsCanceled);
            Assert.AreSame(first, fixture.Controller.Stop());
            Assert.AreEqual(1, fixture.Session.StopCalls);
        }

        [UnityTest]
        public IEnumerator CleanupCallbackFailureStillAwaitsRunnerShutdown()
        {
            using var fixture = new Fixture();
            var cleanupFailure = new InvalidOperationException("local player cleanup failed");
            fixture.ThrowDuringCleanup(cleanupFailure);
            Task first = fixture.Controller.Stop();
            Assert.AreEqual(1, fixture.Session.StopCalls);
            Assert.IsFalse(first.IsCompleted);
            Assert.AreSame(first, fixture.Controller.Stop());
            fixture.Session.Shutdown.SetResult(true);
            yield return Completed(first);
            Assert.IsTrue(first.IsFaulted);
            Assert.AreSame(cleanupFailure, first.Exception.InnerException);
            Assert.AreEqual(1, fixture.Session.StopCalls);
        }

        [UnityTest]
        public IEnumerator CleanupAndShutdownFailuresArePreservedAfterShutdownCompletes()
        {
            using var fixture = new Fixture();
            var cleanupFailure = new InvalidOperationException("local player cleanup failed");
            var shutdownFailure = new InvalidOperationException("runner shutdown failed");
            fixture.ThrowDuringCleanup(cleanupFailure);
            Task first = fixture.Controller.Stop();
            Assert.AreEqual(1, fixture.Session.StopCalls);
            Assert.IsFalse(first.IsCompleted);
            fixture.Session.Shutdown.SetException(shutdownFailure);
            yield return Completed(first);
            var combined = first.Exception?.InnerException as AggregateException;
            Assert.IsNotNull(combined);
            CollectionAssert.AreEqual(new[] { cleanupFailure, shutdownFailure }, combined.InnerExceptions);
            Assert.AreSame(first, fixture.Controller.Stop());
            Assert.AreEqual(1, fixture.Session.StopCalls);
        }

        [Test]
        public void UninitializedControllerHasOneCompletedStopTask()
        {
            var owner = new GameObject("Uninitialized Wordbound shutdown test");
            try
            {
                var controller = owner.AddComponent<WordboundGameplayController>();
                Task first = controller.Stop();
                Assert.AreEqual(TaskStatus.RanToCompletion, first.Status);
                Assert.AreSame(first, controller.Stop());
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
