using System;
using NUnit.Framework;
using Wreckabulary.Online;

namespace Wreckabulary.OnlineTests
{
    public sealed class WordboundRecoveryWindowTests
    {
        [Test, Category("Wordbound")]
        public void RepeatedTransportCallbacksCannotExtendTheFirstLossDeadline()
        {
            var window = new WordboundRecoveryWindow();
            Assert.IsTrue(window.Begin(1000));
            Assert.IsFalse(window.Begin(65000));
            Assert.AreEqual(121000, window.Deadline);
            window.RequireTime(120999);
            Assert.Throws<TimeoutException>(() => window.RequireTime(121000));
        }

        [Test, Category("Wordbound")]
        public void CoordinatorSeatDeadlineCanOnlyShortenTheWindow()
        {
            var window = new WordboundRecoveryWindow();
            window.Begin(1000);
            window.Narrow(90000, 2000);
            window.Narrow(150000, 3000);
            Assert.AreEqual(90000, window.Deadline);
            Assert.Throws<TimeoutException>(() => window.Narrow(3000, 3000));
        }

        [Test, Category("Wordbound")]
        public void RejectedOrExpiredSeatCannotReceiveAnotherAttempt()
        {
            var window = new WordboundRecoveryWindow();
            window.Begin(1000);
            Assert.Throws<TimeoutException>(() => window.Narrow(999, 1000));
            Assert.Throws<TimeoutException>(() => window.NextAttempt(1000));
        }

        [Test, Category("Wordbound")]
        public void ExplicitCancellationIsTerminalEvenAfterACompletionCallback()
        {
            var window = new WordboundRecoveryWindow();
            window.Begin(1000);
            window.Cancel();
            window.Complete();
            Assert.IsFalse(window.Begin(2000));
            Assert.Throws<TimeoutException>(() => window.NextAttempt(2000));
            Assert.IsTrue(window.Cancelled);
        }

        [Test, Category("Wordbound")]
        public void BackoffIsBoundedAndNeverChangesTheAbsoluteDeadline()
        {
            var window = new WordboundRecoveryWindow();
            window.Begin(1000);
            int[] expected = { 250, 500, 1000, 2000, 2000, 2000 };
            foreach (int delay in expected) Assert.AreEqual(delay, window.NextAttempt(1000));
            Assert.AreEqual(121000, window.Deadline);
            Assert.Throws<TimeoutException>(() => window.NextAttempt(121000));
        }

        [Test, Category("Wordbound")]
        public void OnlyACompletedRecoveryAllowsAFreshWindowForTheNextLoss()
        {
            var window = new WordboundRecoveryWindow();
            window.Begin(1000);
            window.NextAttempt(1000);
            window.Complete();
            Assert.IsFalse(window.Active);
            Assert.IsTrue(window.Begin(50000));
            Assert.AreEqual(170000, window.Deadline);
            Assert.AreEqual(0, window.Attempts);
        }

        [TestCase(-1L), TestCase(long.MaxValue), Category("Wordbound")]
        public void InvalidClockCannotOverflowADeadline(long now)
            => Assert.Throws<ArgumentOutOfRangeException>(() => new WordboundRecoveryWindow().Begin(now));
    }
}
