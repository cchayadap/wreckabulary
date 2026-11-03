using System;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class FlexibleWeaponTests
    {
        static readonly FlexibleWeaponDefinition Belt = FlexibleWeaponDefinition.Belt();

        [Test]
        public void BeltPrototypeIsValid()
        {
            Assert.IsEmpty(Belt.Validate());
            var item = Belt.ToItem();
            var catalogue = new ItemCatalogue();
            item.Enabled = true;
            catalogue.Add(item);
            Assert.IsEmpty(catalogue.Validate(TestData.RulesFor("Dibs").MaxLetters), "the prototype also satisfies the shared item validator");
        }

        [Test]
        public void BeltIsNotInTheSharedCatalogue()
        {
            var shared = TestData.Catalogue();
            Assert.IsFalse(shared.TryGet("BELT", out _), "BELT stays out of items.json, recipes, Shop and public bindings");
            Assert.IsFalse(Belt.ToItem().Enabled);
            StringAssert.StartsWith("prototype/BELT/v1|", Belt.Identity());
        }

        [Test]
        public void BeltStaysInsideTheShippedMeleeEnvelope()
        {
            var swings = TestData.Catalogue().All.Where(i => i.Enabled && i.Melee != null && i.Family == HandlingFamily.MeleeSwing).ToList();
            CollectionAssert.IsSupersetOf(swings.Select(i => i.Id), new[] { "BAT", "BLADE", "BROOM", "HAMMER" });
            foreach (var item in swings)
            {
                var m = item.Melee;
                Assert.LessOrEqual(MeleeFairness.DamagePerSecond(m), MeleeFairness.MaxDamagePerSecond, item.Id);
                Assert.LessOrEqual(MeleeFairness.Threat(m), MeleeFairness.MaxThreat, item.Id);
                Assert.LessOrEqual(m.Reach, MeleeFairness.MaxSwingReach, item.Id);
                Assert.LessOrEqual(m.ArcDegrees, MeleeFairness.MaxSwingArc, item.Id);
            }
            var bat = TestData.Catalogue().Get("BAT").Melee;
            Assert.Less(MeleeFairness.DamagePerSecond(Belt.Melee), MeleeFairness.DamagePerSecond(bat), "reach is paid for with damage");
            Assert.Less(Belt.Melee.BreakPower, bat.BreakPower, "a strap breaks furniture worse than a bat");
            Assert.Greater(Belt.Melee.Reach, bat.Reach);
            Assert.Less(MeleeFairness.DamagePerSecond(Belt.BuckleMelee), MeleeFairness.DamagePerSecond(bat), "even buckle hits stay below BAT");
            Assert.Less(Belt.BuckleMelee.BreakPower, bat.BreakPower);
            Assert.Greater(Belt.BuckleMelee.Damage, Belt.Melee.Damage, "the metal buckle hits harder than the strap");
            Assert.AreSame(Belt.BuckleMelee, Belt.StatsFor(StrikePart.Buckle));
            Assert.AreSame(Belt.Melee, Belt.StatsFor(StrikePart.Strap));
        }

        [Test]
        public void ValidationRejectsUnfairOrUnreadableBelts()
        {
            var fast = FlexibleWeaponDefinition.Belt();
            fast.Melee.Windup = 0.02f;
            Assert.IsNotEmpty(fast.Validate());
            var long_ = FlexibleWeaponDefinition.Belt();
            long_.Melee.Reach = 3f;
            Assert.IsNotEmpty(long_.Validate());
            var strong = FlexibleWeaponDefinition.Belt();
            strong.Melee.Damage = 26f;
            Assert.IsTrue(strong.Validate().Any(p => p.Contains("damage per second") || p.Contains("threat")));
            var mismatched = FlexibleWeaponDefinition.Belt();
            mismatched.BuckleMelee.Windup = 0.3f;
            Assert.IsTrue(mismatched.Validate().Any(p => p.Contains("share one swing")));
            var whip = FlexibleWeaponDefinition.Belt();
            whip.Swing.LeadDegrees = 40f;
            Assert.IsTrue(whip.Validate().Any(p => p.Contains("contact arc")));
        }

        [Test]
        public void MatchesTheBuiltModel()
        {
            CollectionAssert.AreEqual(new[] { 0.112f, 1.2f, 0.072f }, Belt.Size);
            CollectionAssert.AreEqual(new[] { 0f, 0.16f, 0f }, Belt.Grip);
            Assert.AreEqual(0.5f, Belt.HeldScale);
            Assert.AreEqual(0.6f, Belt.Size[1] * Belt.HeldScale, 1e-4f, "held length matches BAT's 0.6 m");
            Assert.AreEqual(Belt.InHandLength, Belt.Length, 0.02f);
            var stretched = FlexibleWeaponDefinition.Belt();
            stretched.Length = 0.9f;
            Assert.IsTrue(stretched.Validate().Any(p => p.Contains("must match the model")));
        }

        [Test]
        public void OuterEndIsTheBuckle()
        {
            float reach = Belt.Melee.Reach;
            Assert.AreEqual(StrikePart.Buckle, Belt.PartAt(reach));
            Assert.AreEqual(StrikePart.Buckle, Belt.PartAt(reach - 0.05f));
            Assert.AreEqual(StrikePart.Strap, Belt.PartAt(reach - 0.5f));
            Assert.AreEqual(StrikePart.Strap, Belt.PartAt(0.1f));
        }

        [Test]
        public void StrapLagsInWindupWhipsThroughContactAndSettles()
        {
            var m = Belt.Melee;
            var swing = Belt.Swing;
            Assert.AreEqual(0f, swing.TipBend(m, -0.1));
            Assert.Greater(swing.TipBend(m, m.Windup * 0.9), swing.MaxLagDegrees * 0.8f);
            Assert.LessOrEqual(swing.ContactDeviation(m), swing.ContactToleranceDegrees);
            float settled = Math.Abs(swing.TipBend(m, m.Windup + m.Active + m.Recovery * 0.95));
            Assert.Less(settled, 2f);
            Assert.AreEqual(0f, swing.TipBend(m, m.Cycle + 0.01));
            for (double t = 0; t < m.Cycle; t += 0.005)
                Assert.LessOrEqual(Math.Abs(swing.TipBend(m, t)), swing.MaxLagDegrees + 0.01f, $"bend at {t:0.000}s stays bounded");
        }

        [Test]
        public void BendIsDistributedTowardTheTipAndSumsToTheTipBend()
        {
            var swing = Belt.Swing;
            float sum = 0f;
            for (int i = 0; i < swing.Segments; i++)
            {
                sum += swing.Weight(i);
                if (i > 0) Assert.Greater(swing.Weight(i), swing.Weight(i - 1));
            }
            Assert.AreEqual(1f, sum, 1e-5f);
            Assert.AreEqual(0f, swing.Weight(swing.Segments));
            Assert.AreEqual(0f, swing.IdleBend(0.3, 0f, -1));
        }

        [Test]
        public void SwingPhasesFollowTheStatsTiming()
        {
            var m = Belt.Melee;
            Assert.AreEqual(SwingPhase.Windup, FlexibleSwing.PhaseAt(m, 0.01));
            Assert.AreEqual(SwingPhase.Active, FlexibleSwing.PhaseAt(m, m.Windup + 0.01));
            Assert.AreEqual(SwingPhase.Recovery, FlexibleSwing.PhaseAt(m, m.Windup + m.Active + 0.01));
            Assert.AreEqual(SwingPhase.Ready, FlexibleSwing.PhaseAt(m, m.Cycle + 0.01));
        }

        [Test]
        public void ContactClassificationSeparatesHitsGlancesAndMisses()
        {
            var m = Belt.Melee;
            Assert.AreEqual(ImpactResult.Hit, MeleeFairness.Classify(0f, m.ArcDegrees, 1.2f, m.Reach));
            Assert.AreEqual(ImpactResult.Glance, MeleeFairness.Classify(m.ArcDegrees * 0.45f, m.ArcDegrees, 1.2f, m.Reach));
            Assert.AreEqual(ImpactResult.Hit, MeleeFairness.Classify(0f, m.ArcDegrees, m.Reach * 0.98f, m.Reach), "the buckle tip is a full hit");
            Assert.AreEqual(ImpactResult.Miss, MeleeFairness.Classify(m.ArcDegrees * 0.6f, m.ArcDegrees, 1.2f, m.Reach));
            Assert.AreEqual(ImpactResult.Miss, MeleeFairness.Classify(0f, m.ArcDegrees, m.Reach + 0.1f, m.Reach));
        }
    }
}
