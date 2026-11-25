using System.Collections.Generic;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class DesktopSchemeTests
    {
        const float Frame = 1f / 60f;

        static List<PressStep> Run(ClickHold hold, float pressedFor, float after = .1f)
        {
            var steps = new List<PressStep>();
            float t = 0f;
            for (; t < pressedFor; t += Frame) steps.Add(hold.Update(true, t));
            for (float end = t + after; t < end; t += Frame) steps.Add(hold.Update(false, t));
            return steps;
        }

        [Test]
        public void AQuickClickIsATapAndNeverAims()
        {
            var steps = Run(new ClickHold(), .1f);
            Assert.AreEqual(PressStep.Down, steps[0]);
            CollectionAssert.DoesNotContain(steps, PressStep.AimStart);
            CollectionAssert.DoesNotContain(steps, PressStep.Release);
            Assert.AreEqual(1, steps.FindAll(s => s == PressStep.Tap).Count);
        }

        [Test]
        public void HoldingPastTheThresholdAimsThenLettingGoReleases()
        {
            var hold = new ClickHold();
            Assert.AreEqual(PressStep.Down, hold.Update(true, 0f));
            Assert.AreEqual(PressStep.Holding, hold.Update(true, DesktopScheme.HoldSeconds - .01f));
            Assert.IsFalse(hold.Aiming);
            Assert.AreEqual(PressStep.AimStart, hold.Update(true, DesktopScheme.HoldSeconds));
            Assert.IsTrue(hold.Aiming);
            Assert.AreEqual(PressStep.Aiming, hold.Update(true, 1f));
            Assert.AreEqual(PressStep.Release, hold.Update(false, 1.1f));
            Assert.IsFalse(hold.Aiming);
            Assert.AreEqual(PressStep.None, hold.Update(false, 1.2f));
        }

        [Test]
        public void ACancelledHoldWaitsForAFreshPress()
        {
            var hold = new ClickHold();
            hold.Update(true, 0f);
            hold.Update(true, .5f);
            hold.Cancel(true);
            Assert.IsFalse(hold.Aiming);
            Assert.AreEqual(PressStep.None, hold.Update(true, .6f), "still held after the cancel: nothing");
            Assert.AreEqual(PressStep.None, hold.Update(false, .7f), "letting go after a cancel never throws");
            Assert.AreEqual(PressStep.Down, hold.Update(true, .8f), "a new press starts again");
        }

        [Test]
        public void ThrowsFireOnLettingGoAndEverythingElseOnThePress()
        {
            foreach (AttackKind kind in System.Enum.GetValues(typeof(AttackKind)))
            {
                bool release = kind == AttackKind.Throw;
                Assert.AreEqual(release, DesktopScheme.FiresOnRelease(kind), kind.ToString());
                Assert.AreEqual(!release, DesktopScheme.Fires(kind, PressStep.Down), kind + " on the press");
                Assert.AreEqual(release, DesktopScheme.Fires(kind, PressStep.Tap), kind + " on a quick click's release");
                Assert.AreEqual(release, DesktopScheme.Fires(kind, PressStep.Release), kind + " on letting go after aiming");
                Assert.IsFalse(DesktopScheme.Fires(kind, PressStep.Holding));
                Assert.IsFalse(DesktopScheme.Fires(kind, PressStep.Aiming));
            }
        }

        [Test]
        public void OnlyThrowsAndTheBowAimWhileHeld()
        {
            Assert.IsTrue(DesktopScheme.AimsWhileHeld(AttackKind.Throw));
            Assert.IsTrue(DesktopScheme.AimsWhileHeld(AttackKind.Draw));
            Assert.IsFalse(DesktopScheme.AimsWhileHeld(AttackKind.Swing), "holding a melee click does nothing extra");
            Assert.IsFalse(DesktopScheme.AimsWhileHeld(AttackKind.Shield), "holding with a shield raises it instead");
        }

        [Test]
        public void HouseAndCourseRowsKeepTheirOwnKeys()
        {
            Assert.AreEqual(SchemeScope.House, DesktopScheme.Scope(DesktopScheme.SpellBox));
            Assert.AreEqual(SchemeScope.House, DesktopScheme.Scope(DesktopScheme.Dodge));
            Assert.AreEqual(SchemeScope.Course, DesktopScheme.Scope(DesktopScheme.Aim));
            Assert.AreEqual(SchemeScope.Course, DesktopScheme.Scope(DesktopScheme.CourseSpell));
            Assert.AreEqual(SchemeScope.Both, DesktopScheme.Scope("Attack"));
            Assert.IsTrue(DesktopScheme.ShowsIn(DesktopScheme.SpellBox, false));
            Assert.IsFalse(DesktopScheme.ShowsIn(DesktopScheme.SpellBox, true));
            Assert.IsFalse(DesktopScheme.ShowsIn(DesktopScheme.Aim, false), "houses aim by holding the attack button");
            Assert.IsTrue(DesktopScheme.ShowsIn(DesktopScheme.Aim, true));
            Assert.IsTrue(DesktopScheme.ShowsIn("Jump", false) && DesktopScheme.ShowsIn("Jump", true));
            Assert.IsFalse(DesktopScheme.Shares(DesktopScheme.SpellBox, DesktopScheme.Aim), "RMB is the house spell box and the course aim");
            Assert.IsFalse(DesktopScheme.Shares(DesktopScheme.SpellBox, DesktopScheme.CourseSpell));
            Assert.IsTrue(DesktopScheme.Shares(DesktopScheme.SpellBox, "Attack"), "a house key still swaps with a shared key");
            Assert.IsTrue(DesktopScheme.Shares(DesktopScheme.Aim, DesktopScheme.CourseSpell));
            Assert.IsTrue(DesktopScheme.Shares("Jump", "Interact"));
        }

        [Test]
        public void SpellBoxLinesNameTheLiveKey()
        {
            Assert.AreEqual("BAT is ready. Press RMB to spell it.", DesktopScheme.ReadyLine("BAT", "RMB"));
            Assert.AreEqual("Type a word you can make from your letters. RMB closes.", DesktopScheme.EmptyLine("RMB"));
            Assert.AreEqual("That isn't a recipe. Close, then check the book on TAB.", DesktopScheme.UnknownLine("TAB"));
            foreach (string line in new[] { DesktopScheme.ReadyLine("BAT", "Q"), DesktopScheme.EmptyLine("Q"), DesktopScheme.UnknownLine("TAB") })
            {
                StringAssert.DoesNotContain("—", line);
                StringAssert.DoesNotContain("–", line);
                StringAssert.DoesNotContain("right-click", line.ToLowerInvariant());
                StringAssert.DoesNotContain("Tab or", line);
            }
        }
    }
}
