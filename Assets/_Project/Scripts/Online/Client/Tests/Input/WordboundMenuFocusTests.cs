using NUnit.Framework;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundMenuFocusTests
    {
        [Test]
        public void InventoryAndRecipesWrapInVisibleOrderAndConfirmationIsConsumedOnce()
        {
            var menu = new WordboundMenuFocus();
            menu.BeginLayout();
            foreach (var id in new[] { "resume", "fists", "slot:1", "recipe:BAT", "return" }) menu.Register(id);
            menu.EndLayout();
            Assert.AreEqual("resume", menu.Selected);
            menu.Move(-1);
            Assert.AreEqual("return", menu.Selected);
            menu.Move(1); menu.Move(1); menu.Move(1); menu.Move(1);
            Assert.AreEqual("recipe:BAT", menu.Selected);
            menu.Confirm();
            Assert.IsFalse(menu.Consume("return"));
            Assert.IsTrue(menu.Consume("recipe:BAT"));
            Assert.IsFalse(menu.Consume("recipe:BAT"));
        }

        [Test]
        public void RecipeBecomingUnavailableCancelsPendingConfirmationInsteadOfActivatingAnotherControl()
        {
            var menu = new WordboundMenuFocus();
            menu.Reset("recipe:BAT");
            menu.BeginLayout(); menu.Register("resume"); menu.Register("recipe:BAT"); menu.Register("return"); menu.EndLayout();
            menu.Confirm();
            menu.BeginLayout(); menu.Register("resume"); menu.Register("return"); menu.EndLayout();
            Assert.AreEqual("resume", menu.Selected);
            Assert.IsFalse(menu.Consume("recipe:BAT"));
            Assert.IsFalse(menu.Consume("resume"));
        }

        [Test]
        public void ReturnAndReopenCannotReplayPendingInput()
        {
            var menu = new WordboundMenuFocus();
            menu.BeginLayout(); menu.Register("return"); menu.EndLayout(); menu.Confirm();
            menu.Reset();
            menu.BeginLayout(); menu.Register("resume"); menu.Register("return"); menu.EndLayout();
            Assert.AreEqual("resume", menu.Selected);
            Assert.IsFalse(menu.Consume("return"));
        }

        [Test]
        public void PointerTakingOverCancelsQueuedControllerActionWithoutLosingSelection()
        {
            var menu = new WordboundMenuFocus();
            menu.BeginLayout(); menu.Register("recipe:BAT"); menu.EndLayout(); menu.Confirm();
            menu.CancelConfirmation();
            Assert.AreEqual("recipe:BAT", menu.Selected);
            Assert.IsFalse(menu.Consume("recipe:BAT"));
        }
    }
}
