using System;
using System.Linq;
using NUnit.Framework;
using static Wreckabulary.Rules.Tests.HelperTestSpecs;

namespace Wreckabulary.Rules.Tests
{
    public class HelperCapsTests
    {
        static void Broken(HelperSpec spec, string because)
        {
            var problems = HelperCaps.Check(spec);
            Assert.IsNotEmpty(problems, spec.Id + " must be refused: " + because);
        }

        static HelperSpec With(HelperSpec spec, Action<HelperSpec> change)
        {
            change(spec);
            return spec;
        }

        [Test]
        public void ThePlannedBatchFitsItsTierCaps()
        {
            Assert.AreEqual(RecipeTier.Uncommon, RecipeDifficulty.TierOf("ROBOT"));
            Assert.AreEqual(RecipeTier.Rare, RecipeDifficulty.TierOf("RABBIT"));
            Assert.AreEqual(RecipeTier.Epic, RecipeDifficulty.TierOf("CHOMPER"));
            Assert.AreEqual(RecipeTier.Common, RecipeDifficulty.TierOf("TRAP"));
            foreach (var spec in BatchOne().Append(Cracker()))
                CollectionAssert.IsEmpty(HelperCaps.Check(spec), spec.Id);
            CollectionAssert.IsEmpty(HelperCaps.Inversions(BatchOne()).ToList());
        }

        [Test]
        public void StrikeDamageIsCappedByTier()
        {
            Broken(With(Trap(), s => s.Damage = 9f), "Common strikes at most 8");
            Broken(With(Robot(), s => { s.Damage = 11f; s.Cooldown = 3f; s.Strikes = 2; }), "Uncommon strikes at most 10");
        }

        [Test]
        public void DamageASecondStaysWellUnderAHeldWeapon()
        {
            Broken(With(Robot(), s => s.Cooldown = .6f), "8 / 1.05 is over Uncommon 7");
            Assert.Less(HelperCaps.MaxDamagePerSecond(RecipeTier.Epic), RecipeDifficulty.MaxMeleeDps(RecipeTier.Common) * .5f, "an Epic helper hits slower than half a Common weapon");
        }

        [Test]
        public void TotalDamageStaysBelowABomb()
        {
            Broken(With(Robot(), s => s.Strikes = 4), "8 x 4 = 32 is over Uncommon 26");
            Assert.Less(HelperCaps.MaxTotalDamage(RecipeTier.Epic, HelperBite.Enemies), 45f, "below BOMB");
        }

        [Test]
        public void AnyoneHelpersGetAQuarterMoreTotalButNoMoreStrikeOrRate()
        {
            Assert.AreEqual(42f * 1.25f, HelperCaps.MaxTotalDamage(RecipeTier.Epic, HelperBite.Anyone), 1e-4f);
            var chomper = Chomper();
            Assert.Greater(chomper.TotalDamage, HelperCaps.MaxTotalDamage(RecipeTier.Epic, HelperBite.Enemies));
            CollectionAssert.IsEmpty(HelperCaps.Check(chomper), "50 fits the Anyone bonus");
            Broken(With(Chomper(), s => s.Bites = HelperBite.Enemies), "the same numbers are too strong for a safe helper");
            Broken(With(Chomper(), s => s.Damage = 15f), "the bonus never raises strike damage");
            Broken(With(Chomper(), s => s.Cooldown = .6f), "nor damage a second");
        }

        [Test]
        public void BurstCountsOncePerTargetForUpToFour()
        {
            var cracker = Cracker();
            Assert.AreEqual(cracker.Damage * HelperSpec.BurstTargets, cracker.TotalDamage, 1e-4f);
            Broken(With(Cracker(), s => s.Strikes = 2), "a burst strikes once");
            Broken(With(Cracker(), s => s.Bites = HelperBite.Enemies), "48 is over the safe Epic 42");
        }

        [Test]
        public void LifeIsCappedForMoversAndStayers()
        {
            Broken(With(Robot(), s => s.Seconds = 11f), "Uncommon movers live 10 s");
            Broken(With(Blaster(), s => s.Seconds = 19f), "Rare stayers live 18 s");
            Broken(With(Robot(), s => s.Seconds = 0f), "a helper always lives a little");
            Assert.Greater(HelperCaps.MaxLife(RecipeTier.Common, false), HelperCaps.MaxLife(RecipeTier.Common, true));
        }

        [Test]
        public void EveryoneCanOutrunAChaser()
        {
            Broken(With(Robot(), s => s.Speed = 3.5f), "Uncommon movers go at most 3.4 m/s");
            Assert.Less(HelperCaps.MaxSpeed(RecipeTier.Epic), 6.5f, "slower than a running player");
            Broken(With(Robot(), s => s.Speed = 0f), "a mover moves");
            Broken(With(Blaster(), s => s.Speed = 1f), "a stayer stays");
        }

        [Test]
        public void SightLeashAndRadiusAreCapped()
        {
            Broken(With(Robot(), s => s.Sight = 9f), "Uncommon sight 8");
            Broken(With(Robot(), s => s.Leash = 11f), "Uncommon leash 10");
            Broken(With(Blaster(), s => { s.Radius = 4f; s.Range = 3.5f; }), "Rare radius 3.5");
            Broken(With(Robot(), s => s.Range = 2.5f), "a swing reaches at most a held weapon's reach");
            Broken(With(Blaster(), s => s.Range = 4f), "it cannot shoot past what it senses");
        }

        [Test]
        public void HealthHasATierBand()
        {
            Broken(With(Trap(), s => s.Health = 11f), "a Common helper has at least 12");
            Broken(With(Trap(), s => s.Health = 21f), "and at most 20");
        }

        [Test]
        public void ShotsAreSlowEnoughToSideStep()
        {
            Broken(With(Blaster(), s => s.ShotSpeed = 12.5f), "Rare shots fly at most 12 m/s");
            Broken(With(Blaster(), s => s.ShotSpeed = 0f), "a shot needs a speed");
            Broken(With(Robot(), s => s.ShotSpeed = 5f), "only shots have a shot speed");
        }

        [Test]
        public void EveryStrikeIsTelegraphedAndSpaced()
        {
            Broken(With(Robot(), s => { s.Windup = .3f; s.Cooldown = 1f; }), "a swing winds up at least .35 s");
            Broken(With(Blaster(), s => { s.Windup = .2f; s.Cooldown = 1.2f; }), "a shot winds up at least .35 s");
            Broken(With(Trap(), s => s.Windup = .1f), "even a snap winds up .15 s");
            Broken(With(Rabbit(), s => { s.Cooldown = .5f; s.Damage = 4f; }), "cooldown at least .6 s");
        }

        [Test]
        public void KnockbackAndSlowsCannotStunLock()
        {
            Broken(With(Robot(), s => s.Knockback = 7f), "knockback at most 6");
            Broken(With(Trap(), s => s.Slow = .4f), "slows to at least half speed");
            Broken(With(Trap(), s => s.SlowSeconds = 2f), "for at most 1.5 s");
            Broken(With(Trap(), s => s.Slow = 1f), "a slow must slow");
            var trap = Trap();
            Assert.IsFalse(HelperCaps.SlowReady(trap, trap.SlowSeconds), "the same player is not slowed again straight away");
            Assert.IsTrue(HelperCaps.SlowReady(trap, trap.SlowSeconds + trap.Cooldown));
            Assert.IsFalse(HelperCaps.SlowReady(Robot(), 99f), "a helper without a slow never slows");
        }

        [Test]
        public void StrikesAreBounded()
        {
            Broken(With(Trap(), s => s.Strikes = 0), "at least one strike");
            Broken(With(new HelperSpec
            {
                Id = "ANTS", Body = HelperBody.Stays, Pick = HelperPick.Stepper, Bites = HelperBite.Anyone, Attack = HelperAttack.Touch,
                Damage = 1f, Range = 1.2f, Windup = .15f, Cooldown = .6f, Radius = 1.2f, Seconds = 12f, Strikes = 9, Health = 16f,
            }, _ => { }), "at most eight strikes");
        }

        [Test]
        public void TrapsStayAndReachLittle()
        {
            Broken(With(Spikes(), s => { s.Radius = 1.5f; s.Range = 1.2f; }), "a trap reaches at most 1.2 m");
            Broken(With(Rabbit(), s => s.Pick = HelperPick.Stepper), "a trap does not walk");
        }

        [Test]
        public void HelpersWithoutAnAttackDoNoDamage()
        {
            var decoy = new HelperSpec { Id = "CLONE", Body = HelperBody.Stays, Pick = HelperPick.Nearest, Attack = HelperAttack.None, Radius = 2f, Seconds = 15f, Health = 20f };
            CollectionAssert.IsEmpty(HelperCaps.Check(decoy));
            decoy.Knockback = 3f;
            Assert.IsNotEmpty(HelperCaps.Check(decoy), "no knockback without an attack");
        }

        [Test]
        public void AHarderWordNeverMakesAWeakerHelper()
        {
            var weakRabbit = With(Rabbit(), s => { s.Damage = 5f; s.Strikes = 4; });
            var inversions = HelperCaps.Inversions(new[] { Robot(), weakRabbit }).ToList();
            Assert.AreEqual(1, inversions.Count, "Rare RABBIT at 20 is weaker than Uncommon ROBOT at 24");
            StringAssert.StartsWith("RABBIT", inversions[0]);
            CollectionAssert.IsEmpty(HelperCaps.Inversions(new[] { With(Robot(), s => s.Strikes = 3), With(Trap(), s => s.Id = "SPIKES") }).ToList(), "movers and stayers are compared apart");
        }

        [Test]
        public void WallsAlwaysEndAndAHarderWallIsNeverWeaker()
        {
            ItemDefinition Wall(string id, float life, int durability) => new ItemDefinition
            {
                Id = id, Enabled = true, Family = HandlingFamily.DeployCover, Durability = durability,
                Deploy = new DeployStats { Effect = DeployEffect.Cover, FootprintX = 1f, FootprintZ = .3f, LifetimeSeconds = life },
            };
            CollectionAssert.IsEmpty(HelperCaps.CheckWall(Wall("CONE", 8f, 30)));
            CollectionAssert.IsEmpty(HelperCaps.CheckWall(Wall("BLOCKS", 15f, 120)));
            Assert.IsNotEmpty(HelperCaps.CheckWall(Wall("CONE", 9f, 30)), "Common walls last at most 8 s");
            Assert.IsNotEmpty(HelperCaps.CheckWall(Wall("CONE", 8f, 0)), "a wall can be smashed");
            Assert.IsNotEmpty(HelperCaps.WallInversions(new[] { Wall("CONE", 8f, 30), Wall("BLOCKS", 2f, 100) }).ToList(), "BLOCKS 200 below CONE 240");
            CollectionAssert.IsEmpty(HelperCaps.WallInversions(new[] { Wall("CONE", 8f, 30), Wall("BLOCKS", 15f, 120) }).ToList());
        }

        [Test]
        public void CountsLimitMoversCrowdsAndTheMatch()
        {
            Assert.IsTrue(HelperCaps.Admits(0, true));
            Assert.IsFalse(HelperCaps.Admits(HelperCaps.MaxMoversPerPlayer, true), "one moving helper per player");
            Assert.IsTrue(HelperCaps.Admits(HelperCaps.MaxMoversPerPlayer, false), "a guard or trap may join a mover");
            Assert.IsFalse(HelperCaps.RetiresOldest(HelperCaps.MaxInPlay - 1));
            Assert.IsTrue(HelperCaps.RetiresOldest(HelperCaps.MaxInPlay), "a seventh helper retires the oldest");
            Assert.IsTrue(HelperCaps.Crowded(HelperCaps.MaxPerTarget));
            Assert.AreEqual(6, HelperCaps.MaxInPlay);
            Assert.AreEqual(2, HelperCaps.MaxPerTarget);
        }
    }
}
