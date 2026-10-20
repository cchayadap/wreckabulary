using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundCombatTests
    {
        static WordboundAuthorityTests.Fixture Create(string mode = "FreeForAll", IWordboundWorldQuery world = null)
        {
            var f = WordboundAuthorityTests.Create(mode);
            var document = mode == "Freestyle" ? CourseTemplates.FreestyleWorld() :
                mode == "FreeForAll" ? CourseTemplates.CombatArena() : CourseTemplates.RaceCircuit();
            document.Supplies.Add(new CourseSupply { Id = "CombatSupply", ModuleId = "Start",
                Position = new CoursePoint(0, 0, -4000), Letters = "BATBROOM" });
            var resolved = CourseResolver.Resolve(document, CourseTemplates.ProofCatalogue(),
                new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(f.Source.Mode)));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            f.Source.Course = resolved.Manifest;
            f.Source.Binding = new MatchBinding("combat-match", MatchSnapshotCodec.ProtocolVersion, f.Source.Mode.Hash,
                f.Source.Course.SourceHash, f.Source.Course.Hash, new string('a', 64), new string('b', 64), f.Source.Binding.Seats);
            f.Match = new AuthoritativeMatch(f.Source.Binding, f.Source.Mode, f.Source.Course, CourseTemplates.ProofCatalogue(),
                f.Source.Catalogue, f.Source.Rules, world ?? f.World);
            f.Advance(121);
            return f;
        }

        static MatchActorState Actor(WordboundAuthorityTests.Fixture f, string id = "a") => f.State.Actors.Single(a => a.PlayerId == id);
        static ItemInstance Item(WordboundAuthorityTests.Fixture f, string id) => f.State.Economy.Items.Single(i => "item:" + i.Id == id);
        static string Code(WordboundAuthorityTests.Fixture f, WordboundAction action, int slot = -1) =>
            f.Step(f.Command("a", action, slot: slot)).Receipts.Single().Effect.Code;
        static void Near(WordboundAuthorityTests.Fixture f, float x = 0, float z = 1, float y = 0) =>
            f.Step(poses: new[] { f.Pose("a", default), f.Pose("b", new WorldVector(x, y, z)) });
        static void Active(WordboundAuthorityTests.Fixture f, MeleeStats stats = null) =>
            f.Advance(WordboundCombatPolicy.WindupTicks(stats ?? f.Source.Rules.Unarmed));
        static void Cooldown(WordboundAuthorityTests.Fixture f)
        {
            long remaining = Actor(f).NextAttackTick - f.Match.Tick;
            if (remaining > 0) f.Advance((int)remaining);
        }
        static string Craft(WordboundAuthorityTests.Fixture f, string word)
        {
            var supply = f.Source.Course.Supplies.First(s => s.SourceId == "CombatSupply" && s.TeamIndex <= 0);
            var needed = word.ToList();
            foreach (var tile in f.State.Bodies.Where(b => b.SourceId == supply.Id).ToArray())
            {
                char letter = f.State.Economy.Tiles.Single(t => "tile:" + t.Key == tile.Id).Value;
                if (!needed.Remove(letter)) continue;
                Assert.AreEqual("Ok", f.Collect("a", tile.Id).Effect.Code);
            }
            Assert.AreEqual(0, needed.Count);
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.BeginCraft, value: word)).Receipts.Single().Effect.Code);
            f.Advance(90);
            return "item:" + f.State.Economy.Items.Single(i => i.Origin == ItemOrigin.Crafted && i.Word == word).Id;
        }

        [Test]
        public void UnarmedSwingHitsOnceAfterWindupAndCommandReplayDoesNotRestartIt()
        {
            var f = Create(); Near(f);
            var command = f.Command("a", WordboundAction.Attack);
            Assert.AreEqual("Ok", f.Step(command).Receipts.Single().Effect.Code);
            long start = Actor(f).Attack.StartedTick, cooldown = Actor(f).NextAttackTick;
            Assert.AreEqual(f.Source.Rules.MaxHealth, Actor(f, "b").Health);
            f.Step(command);
            Assert.AreEqual(start, Actor(f).Attack.StartedTick);
            f.Advance(WordboundCombatPolicy.WindupTicks(f.Source.Rules.Unarmed) - 1);
            Assert.AreEqual(f.Source.Rules.MaxHealth - f.Source.Rules.Unarmed.Damage, Actor(f, "b").Health);
            Assert.AreEqual(1, Actor(f).Attack.HitBodyIds.Count);
            f.Ack();
            f.Advance(WordboundCombatPolicy.ActiveTicks(f.Source.Rules.Unarmed));
            Assert.IsNull(Actor(f).Attack);
            Assert.AreEqual(cooldown, Actor(f).NextAttackTick);
            Assert.AreEqual("Cooldown", Code(f, WordboundAction.Attack));
            Assert.AreEqual(f.Source.Rules.MaxHealth - f.Source.Rules.Unarmed.Damage, Actor(f, "b").Health);
        }

        [TestCase(0, 2, 0, true), TestCase(0, -1, 0, true), TestCase(0, 0, 2, true), TestCase(0, 1, 0, false)]
        public void HostRangeArcHeightAndLineOfSightRejectInvalidHits(float x, float z, float y, bool visible)
        {
            var f = Create(); Near(f, x, z, y); f.World.Visible = visible;
            Code(f, WordboundAction.Attack); Active(f);
            Assert.AreEqual(f.Source.Rules.MaxHealth, Actor(f, "b").Health);
            Assert.AreEqual(0, Actor(f).Attack.HitBodyIds.Count);
        }

        [TestCase("TeamRace"), TestCase("TimeRun"), TestCase("Freestyle")]
        public void CleanModesNeverDamageAPlayerEvenWhenSwingConnects(string mode)
        {
            var f = Create(mode); Near(f); Code(f, WordboundAction.Attack); Active(f);
            Assert.AreEqual(f.Source.Rules.MaxHealth, Actor(f, "b").Health);
            f.Ack();
        }

        [TestCase("target"), TestCase("slot"), TestCase("revision"), TestCase("value")]
        public void AttackRejectsClientTargetAndDamageOrAimPayload(string extra)
        {
            var f = Create(); Near(f);
            var command = f.Command("a", WordboundAction.Attack, target: extra == "target" ? "player:1" : "",
                slot: extra == "slot" ? 0 : -1, revision: extra == "revision" ? 1000 : 0, value: extra == "value" ? "1000" : "");
            Assert.AreEqual("InvalidPayload", f.Step(command).Receipts.Single().Effect.Code);
            Assert.IsNull(Actor(f).Attack);
        }

        [Test]
        public void SelectionChoosesExactlyOneHeldWeaponAndEachSwingWearsItOnlyOnce()
        {
            var f = Create();
            string bat = Craft(f, "BAT"), broom = Craft(f, "BROOM");
            Assert.AreEqual("", WordboundCombatPolicy.EquippedItemId(f.State, "a"));
            Assert.AreEqual("EmptySlot", Code(f, WordboundAction.Equip, 7));
            Assert.AreEqual("Ok", Code(f, WordboundAction.Equip, 1));
            Assert.AreEqual(broom, WordboundCombatPolicy.EquippedItemId(f.State, "a"));
            Near(f);
            var stats = f.Source.Catalogue.Get("BROOM").Melee;
            float durability = Item(f, broom).Durability;
            Code(f, WordboundAction.Attack); Active(f, stats);
            Assert.AreEqual(f.Source.Rules.MaxHealth - stats.Damage, Actor(f, "b").Health);
            Assert.AreEqual(durability - 1, Item(f, broom).Durability);
            f.Advance(WordboundCombatPolicy.ActiveTicks(stats));
            Assert.AreEqual(durability - 1, Item(f, broom).Durability);
            Assert.AreEqual(f.Source.Catalogue.Get("BAT").Durability, Item(f, bat).Durability);
            Assert.AreEqual("Ok", Code(f, WordboundAction.Equip, -1));
            Assert.AreEqual("", WordboundCombatPolicy.EquippedItemId(f.State, "a"));
            f.Ack();
        }

        [Test]
        public void CarriedMapFurnitureHolstersSelectedWeaponAndBlocksHandAttacks()
        {
            var f = Create("Freestyle"); Craft(f, "BAT"); Code(f, WordboundAction.Equip, 0);
            var prop = f.State.Bodies.First(b => b.Kind == WorldBodyKind.Item && Item(f, b.Id).Origin == ItemOrigin.Map);
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.PickUp, prop.Id, Item(f, prop.Id).Revision),
                new[] { f.Pose("a", prop.Position) }).Receipts.Single().Effect.Code);
            Assert.AreEqual(-1, Actor(f).EquippedSlot);
            Assert.AreEqual("", WordboundCombatPolicy.EquippedItemId(f.State, "a"));
            Assert.AreEqual("HandsFull", Code(f, WordboundAction.Attack));
            Assert.AreEqual("HandsFull", Code(f, WordboundAction.Equip, 0));
            Assert.AreEqual("Ok", Code(f, WordboundAction.Drop));
            Assert.AreEqual("Ok", Code(f, WordboundAction.Equip, 0));
            f.Ack();
        }

        [TestCase("drop"), TestCase("equip"), TestCase("disconnect"), TestCase("return"), TestCase("stun")]
        public void LosingAbilityOrChangingEquipmentCancelsSwingWithoutResettingCooldown(string operation)
        {
            var f = Create("BrawlRace"); Craft(f, "BAT"); Code(f, WordboundAction.Equip, 0); Near(f);
            Code(f, WordboundAction.Attack); long cooldown = Actor(f).NextAttackTick;
            switch (operation)
            {
                case "drop": Code(f, WordboundAction.Drop, 0); break;
                case "equip": Code(f, WordboundAction.Equip, -1); break;
                case "disconnect": f.Match.Disconnect("a", f.State.Progress.WallClockMs + 1); break;
                case "return": f.Match.Return("a", f.State.Progress.WallClockMs + 1); break;
                default: f.Step(damage: new[] { new HostDamage("b", "player:0", 1, 10) }); break;
            }
            Assert.IsNull(Actor(f).Attack);
            Assert.AreEqual(cooldown, Actor(f).NextAttackTick);
            f.Ack();
        }

        [Test]
        public void SmashingFurnitureReturnsItsLettersExactlyOnceUsingBreakPower()
        {
            var f = Create("Freestyle");
            var prop = f.State.Bodies.First(b => b.Kind == WorldBodyKind.Item);
            string word = Item(f, prop.Id).Word;
            int count = f.State.Economy.Tiles.Count;
            int swings = (int)Math.Ceiling(Item(f, prop.Id).Durability / f.Source.Rules.Unarmed.BreakPower);
            for (int i = 0; i < swings; i++)
            {
                f.Step(poses: new[] { f.Pose("a", new WorldVector(prop.Position.X, prop.Position.Y, prop.Position.Z - 1)) });
                Code(f, WordboundAction.Attack); Active(f);
                if (i + 1 < swings) Cooldown(f);
            }
            Assert.AreEqual(ItemState.Destroyed, Item(f, prop.Id).State);
            Assert.AreEqual(count + word.Length, f.State.Economy.Tiles.Count);
            f.Advance(WordboundCombatPolicy.ActiveTicks(f.Source.Rules.Unarmed));
            Assert.AreEqual(count + word.Length, f.State.Economy.Tiles.Count);
            f.Ack();
        }

        [TestCase(false), TestCase(true)]
        public void RecoveryPreservesWindupOrHitLedgerAndCannotDuplicateImpact(bool afterHit)
        {
            var f = Create(); Near(f); Code(f, WordboundAction.Attack);
            if (afterHit) Active(f);
            var stamp = f.Match.PublishSnapshot(out string json); f.Match.Acknowledge(stamp);
            var imported = f.Source.Import(json); Assert.IsTrue(imported.Success, imported.Error);
            var restored = new AuthoritativeMatch(f.Source.Binding, f.Source.Mode, f.Source.Course,
                CourseTemplates.ProofCatalogue(), f.Source.Catalogue, f.Source.Rules, f.World);
            long now = f.State.Progress.WallClockMs + 30000;
            restored.RestoreCommitted(imported.Snapshot, stamp, 1, now);
            int ticks = WordboundCombatPolicy.WindupTicks(f.Source.Rules.Unarmed) + WordboundCombatPolicy.ActiveTicks(f.Source.Rules.Unarmed);
            for (int i = 1; i <= ticks; i++) restored.Advance(now + i * 17);
            var state = restored.Capture(1);
            Assert.AreEqual(f.Source.Rules.MaxHealth - f.Source.Rules.Unarmed.Damage, state.Actors.Single(a => a.PlayerId == "b").Health);
            Assert.IsTrue(f.Source.Import(MatchSnapshotCodec.Export(state)).Success);
        }

        [TestCase("future"), TestCase("cooldown"), TestCase("foreign-weapon"), TestCase("early-hit"), TestCase("missing-target"), TestCase("slot")]
        public void CanonicalImportRejectsForgedCombatStateEvenAfterRehashing(string mutation)
        {
            var f = Create(); Near(f); Code(f, WordboundAction.Attack);
            var original = f.State; var a = Actor(f); var attack = a.Attack;
            var forged = new MatchActorState(a.PlayerId, a.Health, a.Stamina,
                mutation == "cooldown" ? a.NextAttackTick + 1 : a.NextAttackTick,
                a.StunnedUntilTick, a.DodgeUntilTick, a.LastInputSequence, a.Appearance,
                mutation == "slot" ? 0 : -1,
                new MatchAttackState(mutation == "future" ? f.Match.Tick + 1 : attack.StartedTick,
                    mutation == "foreign-weapon" ? "item:1" : "",
                    mutation == "early-hit" ? new[] { "player:1" } : mutation == "missing-target" ? new[] { "item:999999" } : null));
            var state = new MatchSnapshot(original.Binding, original.Epoch, original.Sequence, original.NextWorldEntityId,
                original.Progress, original.Economy, original.Commands, original.Actors.Select(x => x.PlayerId == "a" ? forged : x),
                original.Bodies, original.Resources, original.Mechanisms, original.RandomStreams, original.NarrativeFlags);
            Assert.IsFalse(f.Source.Import(MatchSnapshotCodec.Export(state)).Success, mutation);
        }

        [Test]
        public void SelectedFurnitureCannotSilentlyTurnIntoAnUnarmedAttack()
        {
            var f = WordboundAuthorityTests.Create();
            f.CraftBed(); Code(f, WordboundAction.Equip, 0);
            Assert.AreEqual("NoMelee", Code(f, WordboundAction.Attack));
            Assert.IsNull(Actor(f).Attack);
            Assert.AreEqual("Ok", Code(f, WordboundAction.Equip, -1));
            Assert.AreEqual("Ok", Code(f, WordboundAction.Attack));
        }

        [Test]
        public void ActiveWindowCanHitALateArrivalButNeverTheSameTargetTwice()
        {
            var f = Create(); Near(f, 0, 4);
            Code(f, WordboundAction.Attack); Active(f);
            Assert.AreEqual(0, Actor(f).Attack.HitBodyIds.Count);
            Near(f);
            Assert.AreEqual(1, Actor(f).Attack.HitBodyIds.Count);
            float health = Actor(f, "b").Health;
            Near(f);
            Assert.AreEqual(health, Actor(f, "b").Health);
        }

        [Test]
        public void HitStunImmunityPreventsRepeatedHitsExtendingOneStagger()
        {
            var f = Create(); Near(f); Code(f, WordboundAction.Attack); Active(f);
            long stun = Actor(f, "b").StunnedUntilTick, immunity = Actor(f, "b").StaggerImmuneUntilTick;
            float health = Actor(f, "b").Health;
            f.Step(damage: new[] { new HostDamage("a", "player:1", 1, 60) });
            Assert.AreEqual(health - 1, Actor(f, "b").Health);
            Assert.AreEqual(stun, Actor(f, "b").StunnedUntilTick);
            Assert.AreEqual(immunity, Actor(f, "b").StaggerImmuneUntilTick);
            Assert.AreEqual("NotAlive", f.Step(f.Command("b", WordboundAction.Attack)).Receipts.Single().Effect.Code);
            f.Ack();
        }

        [Test]
        public void LastDurabilitySwingBreaksWeaponAndReturnsLettersOnce()
        {
            var f = Create("Freestyle");
            f.Source.Catalogue.Get("BAT").Durability = 1;
            string weapon = Craft(f, "BAT");
            Code(f, WordboundAction.Equip, 0);
            int tiles = f.State.Economy.Tiles.Count;
            Code(f, WordboundAction.Attack); Active(f, f.Source.Catalogue.Get("BAT").Melee);
            Assert.AreEqual(ItemState.Destroyed, Item(f, weapon).State);
            Assert.AreEqual(tiles + 3, f.State.Economy.Tiles.Count);
            Assert.AreEqual(-1, Actor(f).EquippedSlot);
            Assert.IsNull(Actor(f).Attack);
            f.Advance(10);
            Assert.AreEqual(tiles + 3, f.State.Economy.Tiles.Count);
            f.Ack();
        }

        sealed class FailingWorld : IWordboundWorldQuery
        {
            public int Calls, ThrowAt;
            public bool HasLineOfSight(string actorId, WorldVector from, WorldVector to)
            {
                if (++Calls == ThrowAt) throw new InvalidOperationException("Host physics query failed.");
                return true;
            }
            public bool CanDeploy(string actorId, string recipeId, WorldVector position) => false;
            public bool PuzzleSatisfied(string actorId, ResolvedCoursePuzzle puzzle, WorldBodyState placedItem) => false;
        }

        [Test]
        public void FailedHostQueryRollsBackEarlierImpactAndCanRetryTheSameTick()
        {
            var world = new FailingWorld(); var f = Create(world: world);
            var prop = f.State.Bodies.First(b => b.Kind == WorldBodyKind.Item);
            Near(f);
            f.Step(poses: new[] { prop.At(new WorldVector(0, 0, .75f)) });
            Code(f, WordboundAction.Attack);
            f.Advance(WordboundCombatPolicy.WindupTicks(f.Source.Rules.Unarmed) - 1);
            string before = MatchSnapshotCodec.Export(f.State);
            float durability = Item(f, prop.Id).Durability;
            world.Calls = 0; world.ThrowAt = 2;
            Assert.Throws<InvalidOperationException>(() => f.Step());
            Assert.AreEqual(before, MatchSnapshotCodec.Export(f.State));
            world.ThrowAt = 0;
            f.Step();
            Assert.AreEqual(durability - f.Source.Rules.Unarmed.BreakPower, Item(f, prop.Id).Durability);
            Assert.AreEqual(f.Source.Rules.MaxHealth - f.Source.Rules.Unarmed.Damage, Actor(f, "b").Health);
            f.Ack();
        }

        [Test]
        public void WeaponRecoveryAfterFirstActiveTickPreservesWearAndSelection()
        {
            var f = Create(); string weapon = Craft(f, "BAT"); Code(f, WordboundAction.Equip, 0); Near(f);
            Code(f, WordboundAction.Attack); Active(f, f.Source.Catalogue.Get("BAT").Melee);
            float durability = Item(f, weapon).Durability, health = Actor(f, "b").Health;
            var stamp = f.Match.PublishSnapshot(out string json); f.Match.Acknowledge(stamp);
            var imported = f.Source.Import(json); Assert.IsTrue(imported.Success, imported.Error);
            var restored = new AuthoritativeMatch(f.Source.Binding, f.Source.Mode, f.Source.Course,
                CourseTemplates.ProofCatalogue(), f.Source.Catalogue, f.Source.Rules, f.World);
            long now = f.State.Progress.WallClockMs + 30000;
            restored.RestoreCommitted(imported.Snapshot, stamp, 1, now);
            restored.Advance(now + 17);
            var state = restored.Capture(1);
            Assert.AreEqual(weapon, WordboundCombatPolicy.EquippedItemId(state, "a"));
            Assert.AreEqual(durability, state.Economy.Items.Single(i => "item:" + i.Id == weapon).Durability);
            Assert.AreEqual(health, state.Actors.Single(a => a.PlayerId == "b").Health);
            Assert.IsTrue(f.Source.Import(MatchSnapshotCodec.Export(state)).Success);
        }

        [Test]
        public void RivalCriticalResourcesRemainProtectedFromBrawlMelee()
        {
            var f = Create("BrawlRace");
            var prop = f.State.Bodies.First(b => b.Kind == WorldBodyKind.Tile && b.Protected && b.OwnerTeam == 1);
            int tileCount = f.State.Economy.Tiles.Count;
            f.Step(poses: new[] { f.Pose("a", new WorldVector(prop.Position.X, prop.Position.Y, prop.Position.Z - 1)) });
            Code(f, WordboundAction.Attack); Active(f);
            Assert.AreEqual(tileCount, f.State.Economy.Tiles.Count);
            Assert.IsTrue(f.State.Bodies.Single(b => b.Id == prop.Id).Active);
            Assert.IsFalse(Actor(f).Attack.HitBodyIds.Contains(prop.Id));
            f.Ack();
        }

        [Test]
        public void ImpactVelocityStaysInsideTheCanonicalWorldBounds()
        {
            var f = Create(); Near(f);
            f.Step(poses: new[] { f.State.Bodies.Single(b => b.Id == "player:1").At(new WorldVector(0, 0, 1),
                new WorldVector(0, 1000, 1000)) });
            Code(f, WordboundAction.Attack); Active(f);
            var body = f.State.Bodies.Single(b => b.Id == "player:1");
            Assert.AreEqual(1000, body.Velocity.Y);
            Assert.AreEqual(1000, body.Velocity.Z);
            f.Ack();
        }
    }
}
