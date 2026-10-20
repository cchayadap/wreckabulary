using System;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Arena = Wreckabulary.Tests.WordboundArenaResourceTests.Arena;

namespace Wreckabulary.Tests
{
    public sealed class WordboundDefenseAuthorityTests
    {
        static Arena Create(string recipe = "PLATE")
        {
            var f = new Arena();
            foreach (char letter in recipe) f.Collect(f.CacheLetter(letter));
            f.Craft(recipe);
            Assert.AreEqual("Ok", f.Step(WordboundAction.Equip, slot: 0).Receipts.Single().Effect.Code);
            if (f.Match.Tick < 121) f.Advance((int)(121 - f.Match.Tick));
            f.Step(at: default(WorldVector));
            f.Step(actor: 1, at: new WorldVector(0, 0, 1));
            return f;
        }
        static MatchActorState Actor(Arena f) => f.State.Actors.Single(a => a.PlayerId == "p0");
        static ItemInstance Shield(Arena f) => f.State.Economy.Items.Single(i =>
            i.Origin == ItemOrigin.Crafted && f.Items.Get(i.Word).Shield != null);
        static CommandReceipt Hold(Arena f) => f.Step(WordboundAction.Block, value: "Hold").Receipts.Single();
        static void Raise(Arena f)
        {
            Assert.AreEqual("Ok", Hold(f).Effect.Code);
            f.Advance(WordboundDefensePolicy.RaiseTicks(f.Items.Get(Shield(f).Word).Shield));
        }
        static void Hit(Arena f, float amount = 20, string actor = "p1") =>
            f.Step(damage: new[] { new HostDamage(actor, "player:0", amount, 6) });

        [TestCase("PLATE")]
        [TestCase("SHIELD")]
        public void RaisedShieldBlocksHealthAndStaggerAndWearsExactlyBlockedDamage(string recipe)
        {
            var f = Create(recipe); Raise(f);
            float health = Actor(f).Health, durability = Shield(f).Durability;
            Hit(f);
            Assert.AreEqual(health, Actor(f).Health);
            Assert.AreEqual(0, Actor(f).StunnedUntilTick);
            Assert.AreEqual(durability - 20, Shield(f).Durability);
            Assert.IsNotNull(Actor(f).Defense);
            f.Acknowledge();
        }

        [TestCase("PLATE", 80)]
        [TestCase("SHIELD", 100)]
        public void RearAttackRespectsTheActualCatalogueArc(string recipe, float expectedHealth)
        {
            var f = Create(recipe); Raise(f);
            f.Step(actor: 1, at: new WorldVector(0, 0, -1));
            Hit(f);
            Assert.AreEqual(expectedHealth, Actor(f).Health);
            f.Acknowledge();
        }

        [Test]
        public void BeforeRaiseAndUnblockableHazardsKeepNormalDamage()
        {
            var f = Create(); Hold(f); Hit(f);
            Assert.AreEqual(80, Actor(f).Health);
            Assert.AreEqual(60, Shield(f).Durability);
            Assert.IsNull(Actor(f).Defense);
            f.Advance(7);
            Raise(f);
            Hit(f, actor: "");
            Assert.AreEqual(60, Actor(f).Health);
            Assert.AreEqual(60, Shield(f).Durability);
            f.Acknowledge();
        }

        [Test]
        public void BreakingHitRemainsBlockedAndReleasedLettersAreConserved()
        {
            var f = Create(); Raise(f);
            int minted = f.State.Economy.Minted;
            int tiles = f.State.Economy.Tiles.Count;
            Hit(f, 60);
            Assert.AreEqual(100, Actor(f).Health);
            Assert.IsNull(Actor(f).Defense);
            Assert.AreEqual(-1, Actor(f).EquippedSlot);
            Assert.AreEqual(minted, f.State.Economy.Minted);
            Assert.AreEqual(tiles + 5, f.State.Economy.Tiles.Count);
            Assert.IsFalse(f.State.Economy.Items.Any(i => i.Origin == ItemOrigin.Crafted && !i.IsGone));
            f.Acknowledge();
        }

        [Test]
        public void ReplayedHoldDoesNotExtendLeaseButFreshRefreshPreservesRaiseStart()
        {
            var f = Create();
            var receipt = Hold(f);
            var original = Actor(f).Defense;
            f.Advance(5);
            var replay = new AuthenticatedCommand("p0", new PlayerCommand(receipt.Sequence, f.Match.Epoch,
                WordboundAction.Block, value: "Hold"));
            f.Match.Advance(f.State.Progress.WallClockMs + 17, new[] { replay });
            Assert.AreEqual(original.UntilTick, Actor(f).Defense.UntilTick);
            Hold(f);
            Assert.AreEqual(original.StartedTick, Actor(f).Defense.StartedTick);
            Assert.Greater(Actor(f).Defense.UntilTick, original.UntilTick);
            long until = Actor(f).Defense.UntilTick;
            f.Advance((int)(until - f.Match.Tick - 1));
            Assert.IsNotNull(Actor(f).Defense);
            f.Step();
            Assert.IsNull(Actor(f).Defense);
            f.Acknowledge();
        }

        [TestCase("Release")]
        [TestCase("Equip")]
        [TestCase("Drop")]
        [TestCase("Disconnect")]
        [TestCase("Return")]
        [TestCase("Fall")]
        [TestCase("Craft")]
        public void IncompatibleTransitionsCannotLeaveDefenseRaised(string transition)
        {
            var f = Create(); Raise(f);
            if (transition == "Release") f.Step(WordboundAction.Block, value: "Release");
            if (transition == "Equip") f.Step(WordboundAction.Equip, slot: -1);
            if (transition == "Drop")
                Assert.AreEqual("Ok", f.Step(WordboundAction.Drop, slot: 0).Receipts.Single().Effect.Code);
            if (transition == "Disconnect") Assert.IsTrue(f.Match.Disconnect("p0", f.State.Progress.WallClockMs));
            if (transition == "Return") Assert.IsTrue(f.Match.Return("p0", f.State.Progress.WallClockMs));
            if (transition == "Fall") f.Step(observations: new[] { new ProgressEvent(ProgressEventKind.Fell, "p0") });
            if (transition == "Craft")
            {
                foreach (char letter in "BAT")
                {
                    var state = f.State;
                    var letters = state.Economy.Tiles.ToDictionary(t => "tile:" + t.Key, t => t.Value);
                    f.Collect(state.Bodies.First(b => b.Kind == WorldBodyKind.Tile && letters[b.Id] == letter));
                }
                Assert.AreEqual("Ok", f.Step(WordboundAction.BeginCraft, value: "BAT").Receipts.Single().Effect.Code);
            }
            Assert.IsNull(Actor(f).Defense);
            f.Acknowledge();
        }

        [Test]
        public void DefenseRejectsAttacksAndInvalidPayloadsWithoutExtendingTheLease()
        {
            var f = Create(); Hold(f);
            long until = Actor(f).Defense.UntilTick;
            Assert.AreEqual("Blocking", f.Step(WordboundAction.Attack).Receipts.Single().Effect.Code);
            Assert.AreEqual("InvalidPayload", f.Step(WordboundAction.Block, target: "item:1", value: "Hold").Receipts.Single().Effect.Code);
            Assert.AreEqual("InvalidPayload", f.Step(WordboundAction.Block, value: "Forever").Receipts.Single().Effect.Code);
            Assert.AreEqual(until, Actor(f).Defense.UntilTick);
            f.Acknowledge();
        }

        [Test]
        public void DurableRecoveryPreservesTheExactLeaseAndItsOriginalExpiry()
        {
            var f = Create(); Raise(f);
            var before = Actor(f).Defense;
            var stamp = f.Match.PublishSnapshot(out string json);
            Assert.IsTrue(f.Match.Acknowledge(stamp));
            var loaded = MatchSnapshotCodec.Import(json, f.Binding, f.Mode, f.Course, f.Items, f.Rules);
            Assert.IsTrue(loaded.Success, loaded.Error);
            f.Match.RestoreCommitted(loaded.Snapshot, stamp, f.Match.Epoch + 1, f.State.Progress.WallClockMs);
            Assert.AreEqual(before.StartedTick, Actor(f).Defense.StartedTick);
            Assert.AreEqual(before.UntilTick, Actor(f).Defense.UntilTick);
            f.Advance((int)(before.UntilTick - f.Match.Tick));
            Assert.IsNull(Actor(f).Defense);
            f.Acknowledge();
        }

        [TestCase("Future")]
        [TestCase("Expired")]
        [TestCase("Overlong")]
        [TestCase("OtherItem")]
        [TestCase("Unselected")]
        public void RehashedImpossibleDefenseIsRejected(string mutation)
        {
            var f = Create(); Hold(f); f.Step();
            var saved = f.State;
            var actor = Actor(f);
            var d = actor.Defense;
            var defense = new MatchDefenseState(mutation == "OtherItem" ? "item:999" : d.ItemEntityId,
                mutation == "Future" ? saved.Progress.Tick + 1 : d.StartedTick,
                mutation == "Expired" ? saved.Progress.Tick : mutation == "Overlong" ? saved.Progress.Tick + 31 : d.UntilTick);
            var replacement = new MatchActorState(actor.PlayerId, actor.Health, actor.Stamina, actor.NextAttackTick,
                actor.StunnedUntilTick, actor.DodgeUntilTick, actor.LastInputSequence, actor.Appearance,
                mutation == "Unselected" ? -1 : actor.EquippedSlot, actor.Attack, actor.StaggerImmuneUntilTick, defense);
            var changed = new MatchSnapshot(saved.Binding, saved.Epoch, saved.Sequence, saved.NextWorldEntityId,
                saved.Progress, saved.Economy, saved.Commands, saved.Actors.Select(a => a.PlayerId == "p0" ? replacement : a),
                saved.Bodies, saved.Resources, saved.Mechanisms, saved.RandomStreams, saved.NarrativeFlags, saved.ThrownFlights, saved.Practice);
            var result = MatchSnapshotCodec.Import(MatchSnapshotCodec.Export(changed), f.Binding, f.Mode, f.Course, f.Items, f.Rules);
            Assert.IsFalse(result.Success, mutation);
        }

        [Test]
        public void NoSelectedShieldCannotCreateDefense()
        {
            var f = new Arena();
            Assert.AreEqual("NoShield", Hold(f).Effect.Code);
            Assert.IsNull(Actor(f).Defense);
            f.Acknowledge();
        }

        [Test]
        public void ActualMeleeUsesReducedHorizontalImpulseAndNoUpwardKickWhenBlocked()
        {
            var f = Create(); Raise(f);
            var facing = new WorldBodyState("player:1", WorldBodyKind.Player, new WorldVector(0, 0, 1),
                rotationXyz: new WorldVector(0, 1, 0), rotationW: 0, ownerTeam: 1, sourceId: "p1");
            f.Match.Advance(f.State.Progress.WallClockMs + 17, hostBodyStates: new[] { facing });
            Assert.AreEqual("Ok", f.Step(WordboundAction.Attack, actor: 1).Receipts.Single().Effect.Code);
            f.Advance(WordboundCombatPolicy.WindupTicks(f.Rules.Unarmed));
            var victim = f.State.Bodies.Single(b => b.Id == "player:0");
            Assert.AreEqual(100, Actor(f).Health);
            Assert.AreEqual(0, victim.Velocity.Y);
            Assert.That(victim.Velocity.Z, Is.EqualTo(-f.Rules.Unarmed.Knockback * 2 * .3f).Within(.0001f));
            Assert.AreEqual(60 - f.Rules.Unarmed.Damage, Shield(f).Durability);
            f.Acknowledge();
        }

        [TestCase(1, true)]
        [TestCase(-1, false)]
        public void BallBlockUsesTheIncomingProjectileSideRatherThanTheThrower(float incomingZ, bool blocked)
        {
            var f = Create();
            foreach (char letter in "BALL")
            {
                var state = f.State;
                var letters = state.Economy.Tiles.ToDictionary(t => "tile:" + t.Key, t => t.Value);
                f.Collect(state.Bodies.First(b => b.Kind == WorldBodyKind.Tile && letters[b.Id] == letter), 1);
            }
            f.Craft("BALL", 1);
            f.Step(actor: 1, at: new WorldVector(0, 0, -incomingZ * 3));
            Assert.AreEqual("Ok", f.Step(WordboundAction.Throw, actor: 1, slot: 0).Receipts.Single().Effect.Code);
            Raise(f);
            var flight = f.State.ThrownFlights.Single();
            var ball = f.State.Bodies.Single(b => b.Id == flight.ItemBodyId);
            var pose = ball.At(new WorldVector(0, .1f, incomingZ), new WorldVector(0, 0, -incomingZ * 6));
            var contact = new HostThrownContact(flight.ItemBodyId, flight.LaunchRevision, "player:0",
                new WorldVector(0, .2f, incomingZ * .5f), 6, f.Match.Tick + 1);
            var result = f.Match.Advance(f.State.Progress.WallClockMs + 17,
                hostBodyStates: new[] { pose }, hostThrownContacts: new[] { contact });
            Assert.IsTrue(result.Advanced);
            float damage = f.Items.Get("BALL").Thrown.Damage;
            Assert.AreEqual(blocked ? 100 : 100 - damage, Actor(f).Health);
            Assert.AreEqual(blocked ? 60 - damage : 60, Shield(f).Durability);
            Assert.Contains("player:0", f.State.ThrownFlights.Single().HitBodyIds.ToArray());
            f.Acknowledge();
        }
    }
}
