using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class WordboundExpressionActionTests
    {
        GameObject root;
        WordboundGameplayPresentation presentation;
        AvatarExpressionProfile profile;
        AvatarExpressionDriver first, second;
        Economy economy;
        MatchProgress progress;
        MatchBinding binding;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Expression snapshot mapping");
            presentation = root.AddComponent<WordboundGameplayPresentation>();
            profile = ScriptableObject.CreateInstance<AvatarExpressionProfile>();
            var mode = ModeDefinition.For("Freestyle");
            var seats = new[] { new WordboundSeat("a", 0, 0), new WordboundSeat("b", 0, 1) };
            binding = new MatchBinding("expression-map", MatchSnapshotCodec.ProtocolVersion, mode.Hash,
                new string('a', 64), new string('b', 64), new string('c', 64), new string('d', 64), seats);
            progress = new MatchProgress(mode, seats);
            economy = new Economy(GameConfig.Current.Items, new GameRules());
            economy.AddPlayer(0, 0); economy.AddPlayer(1, 0);
            first = AddView("a"); second = AddView("b");
        }

        AvatarExpressionDriver AddView(string actor)
        {
            var type = typeof(WordboundGameplayPresentation);
            var viewType = type.GetNestedType("View", BindingFlags.NonPublic);
            Assert.IsNotNull(viewType);
            var view = Activator.CreateInstance(viewType, true);
            var driver = new AvatarExpressionDriver(null, profile);
            viewType.GetField("Actor").SetValue(view, actor);
            viewType.GetField("Expression").SetValue(view, driver);
            ((IList)type.GetField("views", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presentation)).Add(view);
            return driver;
        }

        [TearDown]
        public void TearDown()
        {
            if (root) Object.DestroyImmediate(root);
            if (profile) Object.DestroyImmediate(profile);
        }

        void Apply(float firstHealth = 100, bool migrate = false)
        {
            if (migrate) progress.BeginMigration();
            else progress.Advance(progress.Tick + 1, (progress.Tick + 1) * 17);
            var frame = new MatchSnapshot(binding, 0, progress.Tick + 1, 1, progress.Capture(), economy.Capture(),
                new CommandJournal(binding.Hash, new[] { "a", "b" }).Capture(),
                new[] { new MatchActorState("a", firstHealth, 100), new MatchActorState("b", 100, 100) },
                new[] { new WorldBodyState("player:0", WorldBodyKind.Player, default, sourceId: "a"),
                    new WorldBodyState("player:1", WorldBodyKind.Player, default, sourceId: "b") },
                Array.Empty<MatchResourceState>(), Array.Empty<MatchMechanismState>(), Array.Empty<MatchRandomState>(), Array.Empty<string>());
            string before = MatchSnapshotCodec.Export(frame);
            presentation.ApplyFrame(frame);
            Assert.AreEqual(before, MatchSnapshotCodec.Export(frame), "Facial mapping must leave the received snapshot unchanged.");
        }

        [Test]
        public void FurnitureOnlyCarryMapsToItsOwnerAndReleaseClearsItWithoutInventingAnAttack()
        {
            Apply(); Assert.AreEqual(AvatarExpression.Idle, first.State); Assert.AreEqual(AvatarExpression.Idle, second.State);
            var furniture = economy.PlaceFurniture("TABLE");
            Assert.IsTrue(economy.PickUp(1, furniture.Id, furniture.Revision).Ok);
            Apply();
            Assert.AreEqual(AvatarExpression.Idle, first.State);
            Assert.AreEqual(AvatarExpression.Equipped, second.State, "Furniture has no selected inventory slot.");
            Assert.IsTrue(economy.DropCarried(1).Ok);
            Apply();
            Assert.AreEqual(AvatarExpression.Idle, second.State, "A drop is not inferred to be an attack or throw.");
        }

        [Test]
        public void OnlyAcceptedInProgressCraftFocusesItsOwnerAndCancellationOrMigrationClearsIt()
        {
            Apply();
            Assert.IsFalse(economy.BeginCraft(0, "BAT", 0).Ok);
            Apply(); Assert.AreEqual(AvatarExpression.Idle, first.State);
            foreach (var tile in economy.MintTiles("BAT").Tiles)
                Assert.IsTrue(economy.CollectTile(0, tile.TileId).Ok);
            Assert.IsTrue(economy.BeginCraft(0, "BAT", 0).Ok);
            Apply();
            Assert.AreEqual(AvatarExpression.Focus, first.State); Assert.AreEqual(AvatarExpression.Idle, second.State);
            Assert.IsTrue(economy.CancelCraft(0).Ok);
            Apply(); Assert.AreEqual(AvatarExpression.Idle, first.State);
            Assert.IsTrue(economy.BeginCraft(0, "BAT", 0).Ok);
            Apply(); Assert.AreEqual(AvatarExpression.Focus, first.State);
            Apply(80); Assert.AreEqual(AvatarExpression.Hurt, first.State, "Damage retains priority over crafting.");
            Apply(80, true); Assert.AreEqual(AvatarExpression.Idle, first.State, "Migration clears presentation even if a retained inventory has Craft state.");
        }
    }
}
