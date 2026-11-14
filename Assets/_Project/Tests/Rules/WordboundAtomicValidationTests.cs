using System;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Tests;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundAtomicValidationTests
    {
        static bool Apply(WordboundSnapshotCodecTests.Fixture f, MatchSnapshot snapshot, GameRules finalRules,
            Func<bool> prepare, Action apply) => MatchStateValidation.ReconcileAndApply(snapshot, f.Binding, f.Mode,
                f.Course, f.Catalogue, f.Rules, finalRules, prepare, apply);

        static string Check(WordboundSnapshotCodecTests.Fixture f, MatchSnapshot snapshot, GameRules rules) =>
            MatchStateValidation.Validate(snapshot, f.Binding, f.Mode, f.Course, f.Catalogue, rules);

        static void RejectFresh(WordboundSnapshotCodecTests.Fixture f, MatchSnapshot snapshot, GameRules finalRules, Action mutate)
        {
            Assert.AreEqual("", Check(f, snapshot, f.Rules));
            bool prepared = false, applied = false;
            string expected = null;
            var error = Assert.Throws<ArgumentException>(() => Apply(f, snapshot, finalRules, () => {
                prepared = true;
                mutate();
                expected = Check(f, snapshot, finalRules);
                Assert.IsNotEmpty(expected, "The mutation must fail the complete reference validator.");
                return true;
            }, () => applied = true));
            Assert.IsTrue(prepared);
            Assert.IsFalse(applied);
            Assert.AreEqual(expected, error.Message);
        }

        [TestCase("TimeRun"), TestCase("TeamRace"), TestCase("BrawlRace"), TestCase("FreeForAll"), TestCase("Freestyle")]
        public void EachModeValidatesAndAppliesTheSameSnapshotOnce(string mode)
        {
            var f = WordboundSnapshotCodecTests.Create(mode);
            var snapshot = f.Snapshot();
            string before = MatchSnapshotCodec.Export(snapshot);
            int prepared = 0, applied = 0;
            Assert.IsTrue(Apply(f, snapshot, f.Rules.Clone(), () => { prepared++; return true; }, () => applied++));
            Assert.AreEqual(1, prepared); Assert.AreEqual(1, applied);
            Assert.AreEqual(before, MatchSnapshotCodec.Export(snapshot));
        }

        [Test]
        public void InitialInvalidSnapshotPrecedesPreparationAndLaterRuleErrors()
        {
            var f = WordboundSnapshotCodecTests.Create("TimeRun");
            f.Resources.RemoveAt(0);
            var snapshot = f.Snapshot();
            string expected = Check(f, snapshot, f.Rules);
            Assert.IsNotEmpty(expected);
            var finalRules = f.Rules.Clone(); finalRules.MaxCarried = 3;
            bool prepared = false, applied = false;
            var error = Assert.Throws<ArgumentException>(() => Apply(f, snapshot, finalRules,
                () => { prepared = true; return true; }, () => applied = true));
            Assert.AreEqual(expected, error.Message);
            Assert.IsFalse(prepared); Assert.IsFalse(applied);
        }

        [Test]
        public void AbortSkipsApplyAndDoesNotAuthorizeALaterInvalidAttempt()
        {
            var f = WordboundSnapshotCodecTests.Create();
            var snapshot = f.Snapshot(); var finalRules = f.Rules.Clone();
            int applied = 0;
            Assert.IsFalse(Apply(f, snapshot, finalRules, () => { finalRules.MaxHealth = 1; return false; }, () => applied++));
            Assert.Throws<ArgumentException>(() => Apply(f, snapshot, finalRules, () => true, () => applied++));
            finalRules.MaxHealth = f.Rules.MaxHealth;
            Assert.IsTrue(Apply(f, snapshot, finalRules, () => true, () => applied++));
            Assert.AreEqual(1, applied);
        }

        [TestCase(false, "argument"), TestCase(false, "invalid-operation"), TestCase(false, "cancelled")]
        [TestCase(true, "argument"), TestCase(true, "invalid-operation"), TestCase(true, "cancelled")]
        public void CallbackExceptionsKeepTheirOriginalInstanceAndType(bool applying, string kind)
        {
            var f = WordboundSnapshotCodecTests.Create(); var snapshot = f.Snapshot();
            Exception failure = kind == "argument" ? new ArgumentException("callback argument") :
                kind == "cancelled" ? new OperationCanceledException("callback cancellation") :
                new InvalidOperationException("callback state");
            int applied = 0;
            var caught = Assert.Catch(() => Apply(f, snapshot, f.Rules.Clone(), () => {
                if (!applying) throw failure;
                return true;
            }, () => { applied++; throw failure; }));
            Assert.AreSame(failure, caught);
            Assert.AreEqual(applying ? 1 : 0, applied);
            Assert.IsTrue(Apply(f, snapshot, f.Rules.Clone(), () => true, () => { }));
        }

        [TestCase("health"), TestCase("stamina"), TestCase("inventory"), TestCase("stagger")]
        public void CurrentSeparatePhysicsRulesAreCheckedAfterPreparation(string field)
        {
            var f = WordboundSnapshotCodecTests.Create(); var snapshot = f.Snapshot();
            var finalRules = f.Rules.Clone();
            RejectFresh(f, snapshot, finalRules, () => {
                if (field == "health") finalRules.MaxHealth = 99;
                else if (field == "stamina") finalRules.MaxStamina = 99;
                else if (field == "inventory") finalRules.MaxCarried++;
                else finalRules.StaggerImmunitySeconds = float.NaN;
            });
            Assert.AreEqual("", Check(f, snapshot, f.Rules), "The initial rule clone must remain valid and distinct.");
        }

        [TestCase(false), TestCase(true)]
        public void PendingCraftUsesCurrentRecipeEnabledStateAndLetters(bool letters)
        {
            var f = WordboundSnapshotCodecTests.Create();
            TestData.GiveLetters(f.Economy, 0, "BAT");
            Assert.IsTrue(f.Economy.BeginCraft(0, "BAT", 0).Ok);
            var recipe = f.Catalogue.Get("BAT");
            RejectFresh(f, f.Snapshot(), f.Rules.Clone(), () => {
                if (letters) recipe.Letters = LetterBag.FromWord("BED");
                else recipe.Enabled = false;
            });
        }

        [TestCase("enabled"), TestCase("letters"), TestCase("capacity")]
        public void ValidationOnlyEconomyRechecksCallbackMutationsAndRecoversWithoutChangingTheSnapshot(string mutation)
        {
            var f = WordboundSnapshotCodecTests.Create();
            TestData.GiveLetters(f.Economy, 0, "BAT");
            Assert.IsTrue(f.Economy.BeginCraft(0, "BAT", 0).Ok);
            var snapshot = f.Snapshot();
            string before = MatchSnapshotCodec.Export(snapshot);
            var finalRules = f.Rules.Clone();
            var recipe = f.Catalogue.Get("BAT");
            var letters = recipe.Letters;
            int prepared = 0, applied = 0;
            string expected = null;
            Assert.AreEqual("", Check(f, snapshot, f.Rules));
            var failure = Assert.Throws<ArgumentException>(() => Apply(f, snapshot, finalRules, () => {
                prepared++;
                if (mutation == "enabled") recipe.Enabled = false;
                else if (mutation == "letters") recipe.Letters = LetterBag.FromWord("BED");
                else finalRules.MaxCarried++;
                var reference = new Economy(f.Catalogue, finalRules);
                for (int i = 0; i < f.Binding.Seats.Count; i++) reference.AddPlayer(i, f.Binding.Seats[i].Team);
                expected = Assert.Throws<ArgumentException>(() => reference.Restore(snapshot.Economy)).Message;
                finalRules.MaxHealth = 1;
                return true;
            }, () => applied++));
            Assert.AreEqual(expected, failure.Message);
            Assert.AreEqual(1, prepared);
            Assert.AreEqual(0, applied);
            Assert.AreEqual(before, MatchSnapshotCodec.Export(snapshot));
            Assert.AreEqual(before, MatchSnapshotCodec.Export(f.Snapshot()));
            recipe.Enabled = true;
            recipe.Letters = letters;
            finalRules.MaxCarried = f.Rules.MaxCarried;
            finalRules.MaxHealth = f.Rules.MaxHealth;
            Assert.IsTrue(Apply(f, snapshot, finalRules, () => { prepared++; return true; }, () => applied++));
            Assert.AreEqual(2, prepared);
            Assert.AreEqual(1, applied);
            Assert.AreEqual(before, MatchSnapshotCodec.Export(snapshot));
        }

        [TestCase(false), TestCase(true)]
        public void DeployedEquipmentUsesCurrentLifetimeAndPlayerCapacity(bool capacity)
        {
            var authority = WordboundAuthorityTests.Create();
            authority.CraftBed();
            Assert.AreEqual("Ok", authority.Step(authority.Command("a", WordboundAction.Deploy, slot: 0)).Receipts.Single().Effect.Code);
            var f = authority.Source; var finalRules = f.Rules.Clone();
            Assert.IsTrue(authority.State.Economy.Items.Any(item => item.State == ItemState.Deployed));
            RejectFresh(f, authority.State, finalRules, () => {
                if (capacity) finalRules.MaxDeployed = 0;
                else f.Catalogue.Get("BED").Deploy.LifetimeSeconds = -1;
            });
        }

        [TestCase(false), TestCase(true)]
        public void ThrownFlightUsesCurrentCatalogueDefinitionAndStats(bool disabled)
        {
            var f = WordboundSnapshotCodecTests.Create();
            TestData.GiveLetters(f.Economy, 0, "BALL");
            Assert.IsTrue(f.Economy.BeginCraft(0, "BALL", 0).Ok);
            Assert.IsTrue(f.Economy.CompleteCraft(0, 100).Ok);
            var item = f.Economy.Throw(0, 0, Skin.Standard).Item;
            Assert.IsNotNull(item);
            f.Progress.Advance(1, 16);
            string id = "item:" + item.Id;
            f.Bodies.Add(new WorldBodyState(id, WorldBodyKind.Item, default, velocity: new WorldVector(0, 0, 16)));
            var original = f.Snapshot();
            var snapshot = new MatchSnapshot(original.Binding, original.Epoch, original.Sequence, original.NextWorldEntityId,
                original.Progress, original.Economy, original.Commands, original.Actors, original.Bodies, original.Resources,
                original.Mechanisms, original.RandomStreams, original.NarrativeFlags,
                new[] { new MatchThrownFlight(id, item.Revision, 1, "a") }, original.Practice);
            RejectFresh(f, snapshot, f.Rules.Clone(), () => {
                if (disabled) f.Catalogue.Get("BALL").Enabled = false;
                else f.Catalogue.Get("BALL").Thrown.Damage = -1;
            });
        }

        [Test]
        public void FreshMutableFailuresKeepCompleteValidatorOrdering()
        {
            var f = WordboundSnapshotCodecTests.Create(); var finalRules = f.Rules.Clone();
            RejectFresh(f, f.Snapshot(), finalRules, () => { finalRules.MaxCarried++; finalRules.MaxHealth = 1; });
        }

        [Test]
        public void ValidMutableChangesStillApplyWithoutRequiringRuleCloneEquality()
        {
            var f = WordboundSnapshotCodecTests.Create(); var finalRules = f.Rules.Clone();
            bool applied = false;
            Assert.IsTrue(Apply(f, f.Snapshot(), finalRules, () => {
                finalRules.MaxHealth = 150;
                f.Catalogue.Get("BAT").Durability++;
                return true;
            }, () => applied = true));
            Assert.IsTrue(applied);
            Assert.AreNotEqual(f.Rules.MaxHealth, finalRules.MaxHealth);
        }
    }
}
