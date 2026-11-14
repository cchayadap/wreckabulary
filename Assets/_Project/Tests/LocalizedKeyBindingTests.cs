using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Wreckabulary.Tests
{
    public sealed class LocalizedKeyBindingTests
    {
        const string LayoutName = "WreckabularyLocalizedPromptDevice";
        InputDevice device;

        [SetUp]
        public void SetUp()
        {
            InputSystem.RegisterLayout(@"{
                ""name"": ""WreckabularyLocalizedPromptDevice"",
                ""controls"": [
                    { ""name"": ""q"", ""layout"": ""Button"", ""displayName"": ""木"" },
                    { ""name"": ""e"", ""layout"": ""Button"", ""displayName"": ""水"" },
                    { ""name"": ""s"", ""layout"": ""Button"", ""displayName"": ""火"" },
                    { ""name"": ""a"", ""layout"": ""Button"", ""displayName"": ""土"" },
                    { ""name"": ""d"", ""layout"": ""Button"", ""displayName"": ""金"" }
                ]
            }");
            device = InputSystem.AddDevice(LayoutName);
        }

        [TearDown]
        public void TearDown()
        {
            if (device != null && device.added) InputSystem.RemoveDevice(device);
            InputSystem.RemoveLayout(LayoutName);
        }

        [Test]
        public void LocalizedSingleKeyPromptsFollowTheEffectiveBinding()
        {
            using var action = new InputAction("Jump", binding: $"<{LayoutName}>/q");
            action.ApplyBindingOverride(0, $"<{LayoutName}>/e");

            Assert.IsFalse(KeyBindings.IsLatin(action.GetBindingDisplayString(0)), "The fixture must exercise the physical-key fallback.");
            Assert.AreEqual("E", KeyBindings.KeyName(action, 0));
            Assert.AreEqual("E", KeyBindings.Display(action, 0));
            Assert.AreEqual("E", KeyBindings.Display(action));

            action.RemoveBindingOverride(0);
            Assert.AreEqual("Q", KeyBindings.KeyName(action, 0));
            Assert.AreEqual("Q", KeyBindings.Display(action));
        }

        [Test]
        public void LocalizedCompositePromptsFollowReboundParts()
        {
            using var action = new InputAction("Move");
            action.AddCompositeBinding("2DVector")
                .With("Up", $"<{LayoutName}>/q")
                .With("Down", $"<{LayoutName}>/s")
                .With("Left", $"<{LayoutName}>/a")
                .With("Right", $"<{LayoutName}>/d");
            action.ApplyBindingOverride(1, $"<{LayoutName}>/e");

            Assert.IsFalse(KeyBindings.IsLatin(action.GetBindingDisplayString()), "The fixture must exercise the composite fallback.");
            Assert.AreEqual("E", KeyBindings.KeyName(action, 1));
            Assert.AreEqual("E", KeyBindings.Display(action, 1));
            Assert.AreEqual("E/S/A/D", KeyBindings.Display(action));

            action.RemoveBindingOverride(1);
            Assert.AreEqual("Q/S/A/D", KeyBindings.Display(action));
        }
    }
}
