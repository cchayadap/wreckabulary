using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class ImpactAudioTests
    {
        static ImpactEvent E(string weapon, StrikePart part, SurfaceMaterial surface, ImpactResult result, float strength = 0.6f, string id = "m1:t10:p0:s1:glass")
            => new ImpactEvent(id, weapon, part, surface, result, strength);

        [Test]
        public void BeltStrapOnIntactGlassIsOnlyAStrapContact()
        {
            var cues = ImpactCues.Plan(E("BELT", StrikePart.Strap, SurfaceMaterial.Glass, ImpactResult.Hit));
            Assert.AreEqual(1, cues.Count);
            Assert.AreEqual(ImpactLayer.Contact, cues[0].Layer);
            Assert.AreEqual("contact/BELT/Strap/Glass", cues[0].Key);
            Assert.IsFalse(cues.Any(c => c.Layer == ImpactLayer.Break || c.Layer == ImpactLayer.Damage));
        }

        [Test]
        public void BuckleAndStrapUseDifferentContactSounds()
        {
            var strap = ImpactCues.Plan(E("BELT", StrikePart.Strap, SurfaceMaterial.Wood, ImpactResult.Hit))[0].Key;
            var buckle = ImpactCues.Plan(E("BELT", StrikePart.Buckle, SurfaceMaterial.Wood, ImpactResult.Hit))[0].Key;
            Assert.AreNotEqual(strap, buckle);
            Assert.AreEqual("contact/BELT/Buckle/Wood", buckle);
        }

        [Test]
        public void BreakLayerOnlyFollowsAConfirmedBreak()
        {
            var cracked = ImpactCues.Plan(E("BAT", StrikePart.Body, SurfaceMaterial.Glass, ImpactResult.Damaged));
            CollectionAssert.AreEqual(new[] { ImpactLayer.Contact, ImpactLayer.Damage }, cracked.Select(c => c.Layer));
            var broken = ImpactCues.Plan(E("BAT", StrikePart.Body, SurfaceMaterial.Glass, ImpactResult.Broken));
            CollectionAssert.AreEqual(new[] { ImpactLayer.Contact, ImpactLayer.Break }, broken.Select(c => c.Layer));
            Assert.AreEqual("break/Glass", broken[1].Key);
        }

        [Test]
        public void MissPlaysNoContactOrBreak()
        {
            Assert.IsEmpty(ImpactCues.Plan(E("BELT", StrikePart.Buckle, SurfaceMaterial.Glass, ImpactResult.Miss)));
            Assert.AreEqual("swing/BELT", ImpactCues.SwingKey("BELT"));
        }

        [Test]
        public void GlancingAndWeakHitsAreQuieterThanFullHits()
        {
            float full = ImpactCues.Plan(E("BAT", StrikePart.Body, SurfaceMaterial.Wood, ImpactResult.Hit, 1f))[0].Volume;
            float glance = ImpactCues.Plan(E("BAT", StrikePart.Body, SurfaceMaterial.Wood, ImpactResult.Glance, 1f))[0].Volume;
            float weak = ImpactCues.Plan(E("BAT", StrikePart.Body, SurfaceMaterial.Wood, ImpactResult.Hit, 0.1f))[0].Volume;
            Assert.Less(glance, full);
            Assert.Less(weak, glance);
            Assert.That(full, Is.InRange(0f, 1f));
        }

        [Test]
        public void ContactFallsBackFromExactPartToWeaponThenDefault()
        {
            var available = new HashSet<string> { "contact/BELT/Any/Ceramic", "contact/Default/Any/Stone" };
            var ceramic = ImpactCues.Plan(E("BELT", StrikePart.Buckle, SurfaceMaterial.Ceramic, ImpactResult.Hit), available.Contains);
            Assert.AreEqual("contact/BELT/Any/Ceramic", ceramic[0].Key);
            var stone = ImpactCues.Plan(E("BROOM", StrikePart.Body, SurfaceMaterial.Stone, ImpactResult.Hit), available.Contains);
            Assert.AreEqual("contact/Default/Any/Stone", stone[0].Key);
            Assert.IsEmpty(ImpactCues.Plan(E("BROOM", StrikePart.Body, SurfaceMaterial.Metal, ImpactResult.Hit), available.Contains));
        }

        [Test]
        public void MissingBreakClipNeverSubstitutesAnotherMaterial()
        {
            var available = new HashSet<string> { "contact/BAT/Body/Wood", "break/Glass" };
            var cues = ImpactCues.Plan(E("BAT", StrikePart.Body, SurfaceMaterial.Wood, ImpactResult.Broken), available.Contains);
            Assert.AreEqual(1, cues.Count);
            Assert.AreEqual(ImpactLayer.Contact, cues[0].Layer);
        }

        [Test]
        public void StrengthFollowsAdmittedDamageAndBreakPower()
        {
            var catalogue = TestData.Catalogue();
            float hammer = ImpactCues.Strength(catalogue.Get("HAMMER").Melee, 28f);
            float bat = ImpactCues.Strength(catalogue.Get("BAT").Melee, 14f);
            float belt = ImpactCues.Strength(FlexibleWeaponDefinition.Belt().Melee, 11f);
            Assert.Greater(hammer, bat);
            Assert.Greater(bat, belt);
        }

        [Test]
        public void LedgerPlaysEachConfirmedEventOnceAcrossReplayAndReconnect()
        {
            var ledger = new ImpactEventLedger(4);
            Assert.IsTrue(ledger.Admit("m1:t10:p0:s1:vase"));
            Assert.IsFalse(ledger.Admit("m1:t10:p0:s1:vase"), "prediction then confirmation");
            Assert.IsFalse(ledger.Admit("m1:t10:p0:s1:vase"), "resimulated tick");
            Assert.IsTrue(ledger.Admit("m1:t11:p0:s1:vase"));
            Assert.IsFalse(ledger.Admit(""));
            for (int i = 0; i < 10; i++) ledger.Admit("x" + i);
            Assert.AreEqual(4, ledger.Count, "bounded memory");
        }

        [Test]
        public void RepeatedColliderContactsAreCooledDown()
        {
            var cooldown = new ImpactContactCooldown(0.12);
            Assert.IsTrue(cooldown.Admit("BELT#4", "vase", "contact", 1.0));
            Assert.IsFalse(cooldown.Admit("BELT#4", "vase", "contact", 1.05));
            Assert.IsTrue(cooldown.Admit("BELT#4", "lamp", "contact", 1.05));
            Assert.IsTrue(cooldown.Admit("BELT#4", "vase", "contact", 1.2));
        }

        [Test]
        public void EightPlayerOverlapKeepsVoicesBoundedAndBreaksAudible()
        {
            var voices = new ImpactVoiceBudget(12, 3);
            int started = 0;
            for (int player = 0; player < 8; player++)
                for (int hit = 0; hit < 4; hit++)
                    if (voices.Request("contact/BAT/Body/Wood", 1, 0.5f + hit * 0.1f, 0, 0.5) >= 0) started++;
            Assert.AreEqual(3, voices.ActiveFor("contact/BAT/Body/Wood", 0.1));
            for (int i = 0; i < 12; i++) voices.Request("contact/BELT/Strap/" + i, 1, 0.4f, 0, 0.5);
            Assert.AreEqual(12, voices.Active(0.1));
            Assert.GreaterOrEqual(voices.Request("break/Glass", 3, 0.9f, 0.1, 1.0), 0, "a break steals a contact voice");
            Assert.Less(voices.Request("swing/BELT", 0, 1f, 0.1, 0.3), 0, "a swing never steals a contact voice");
            Assert.AreEqual(12, voices.Active(0.2));
            Assert.AreEqual(0, voices.Active(2.0));
            Assert.LessOrEqual(started, 32);
        }

        [Test]
        public void VariationAvoidsImmediateRepeatsAndIsDeterministic()
        {
            int previous = -1;
            for (int i = 0; i < 50; i++)
            {
                string id = "event" + i;
                int pick = ImpactVariation.Pick(id, "contact/BAT/Body/Wood", 4, previous);
                Assert.AreNotEqual(previous, pick);
                Assert.AreEqual(pick, ImpactVariation.Pick(id, "contact/BAT/Body/Wood", 4, previous));
                previous = pick;
            }
            float pitch = ImpactVariation.Pitch("e", "k", 0.94f, 1.06f);
            Assert.That(pitch, Is.InRange(0.94f, 1.06f));
            Assert.AreEqual(0, ImpactVariation.Pick("e", "k", 1, 0));
        }

        [Test]
        public void PropWordsMapToTheirMaterial()
        {
            Assert.AreEqual(SurfaceMaterial.Ceramic, SurfaceMaterials.ForWord("Smashable VASE (Clone)"));
            Assert.AreEqual(SurfaceMaterial.Glass, SurfaceMaterials.ForWord("Window_Pane"));
            Assert.AreEqual(SurfaceMaterial.Metal, SurfaceMaterials.ForWord("FRIDGE"));
            Assert.AreEqual(SurfaceMaterial.Soft, SurfaceMaterials.ForWord("sofa"));
            Assert.AreEqual(SurfaceMaterial.Stone, SurfaceMaterials.ForWord("Garden GNOME"));
            Assert.IsFalse(SurfaceMaterials.TryForWord("Unnamed prop", out var fallback));
            Assert.AreEqual(SurfaceMaterial.Wood, fallback);
        }

        [Test]
        public void DistanceAndOcclusionReduceAudibility()
        {
            float near = ImpactCues.Audibility(2f, 30f, false, 0.4f);
            float far = ImpactCues.Audibility(25f, 30f, false, 0.4f);
            float occluded = ImpactCues.Audibility(2f, 30f, true, 0.4f);
            Assert.Greater(near, far);
            Assert.Greater(near, occluded);
            Assert.AreEqual(0f, ImpactCues.Audibility(40f, 30f, false, 0.4f));
        }
    }
}
