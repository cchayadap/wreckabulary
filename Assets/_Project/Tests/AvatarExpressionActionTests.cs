using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Art;

namespace Wreckabulary.Tests
{
    public sealed class AvatarExpressionActionTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = Vector3.down * .5f;
            ground.transform.localScale = new Vector3(40, 1, 40);
        }

        [UnityTearDown]
        public IEnumerator TearDown() => TestScenes.Reset();

        [UnityTest]
        public IEnumerator ActualSpellingAndCraftingDriveFocusAndCancelOrDisableReleasesIt()
        {
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, new ScriptedBinding()); player.Respawn(Vector3.zero);
            var appearance = player.GetComponent<PlayerAppearance>();
            var expressions = (AvatarExpressionDriver)typeof(PlayerAppearance)
                .GetField("expressions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(appearance);
            Assert.IsNotNull(expressions); Assert.IsTrue(expressions.IsSupported);
            var summoner = player.Summoner;
            summoner.Typed = true;
            var inventory = player.GetComponent<LetterInventory>();
            inventory.Set("");
            var recipe = summoner.Recipe("BAT"); Assert.IsNotNull(recipe);
            yield return TestScenes.WaitUntil(() => player.Grounded && expressions.State == AvatarExpression.Idle,
                2f, "grounded idle before spelling");

            Assert.IsFalse(summoner.BeginCraft(recipe), "Rejected recipes must not create an expression channel.");
            yield return new WaitForSeconds(.05f);
            Assert.AreEqual(AvatarExpression.Idle, expressions.State);
            inventory.Set("BAT");
            summoner.Open(); Assert.IsTrue(summoner.IsSpelling);
            yield return new WaitForSeconds(.05f);
            Assert.AreEqual(AvatarExpression.Focus, expressions.State);
            summoner.Close();
            yield return new WaitForSeconds(.05f);
            Assert.AreEqual(AvatarExpression.Idle, expressions.State);

            Assert.IsTrue(summoner.BeginCraft(recipe)); Assert.IsTrue(summoner.IsCrafting);
            yield return new WaitForSeconds(.05f);
            Assert.AreEqual(AvatarExpression.Focus, expressions.State);
            summoner.CancelCraft();
            yield return new WaitForSeconds(.05f);
            Assert.AreEqual(AvatarExpression.Idle, expressions.State);

            Assert.IsTrue(summoner.BeginCraft(recipe));
            yield return new WaitForSeconds(.05f);
            Assert.AreEqual(AvatarExpression.Focus, expressions.State);
            summoner.enabled = false;
            Assert.IsFalse(summoner.IsCrafting); Assert.IsFalse(summoner.IsSpelling);
            yield return new WaitForSeconds(.05f);
            Assert.AreEqual(AvatarExpression.Idle, expressions.State);
        }
    }
}
