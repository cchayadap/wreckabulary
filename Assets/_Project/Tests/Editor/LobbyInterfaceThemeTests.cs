using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Wreckabulary.EditorTools;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests.Editor
{
    public sealed class LobbyInterfaceThemeTests
    {
        [Test]
        public void ColdPaletteReadsDoNotLoadUnityResourcesOnSerializationThreads()
        {
            typeof(LobbyInterfaceTheme).GetMethod("ResetRuntime", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            try
            {
                var read = Task.Run(() => LobbyKit.Navy);
                Assert.IsTrue(read.Wait(TimeSpan.FromSeconds(5)), "A colour lookup must not wait for Unity's main thread.");
                Assert.AreEqual(new LobbyInterfacePalette().Text, read.Result);
            }
            finally { LobbyInterfaceTheme.LoadSaved(); }
        }

        [TestCase(float.NaN, 1f)]
        [TestCase(float.PositiveInfinity, 1f)]
        [TestCase(-.01f, 1f)]
        [TestCase(1.01f, 1f)]
        [TestCase(.5f, .5f)]
        public void SharedPaletteRejectsInvalidOrTransparentChannels(float red, float alpha)
        {
            var palette = new LobbyInterfacePalette();
            Assert.IsTrue(palette.TryValidate(out _));
            palette.Card = new Color(red, .2f, .3f, alpha);
            Assert.IsFalse(palette.TryValidate(out var reason));
            StringAssert.Contains("Card", reason);
        }

        [Test]
        public void FrameAppliesExplicitRolesWithoutTintingBorrowedArtOrChangingSelectionVisibility()
        {
            var root = new GameObject("Theme test", typeof(RectTransform));
            var theme = ScriptableObject.CreateInstance<LobbyInterfaceTheme>();
            var texture = new Texture2D(2, 2);
            try
            {
                var panel = LobbyKit.Rect(root.transform, "Panel").gameObject.AddComponent<Image>();
                var gradient = panel.gameObject.AddComponent<LobbyGradient>();
                var ring = LobbyKit.Rect(root.transform, "Ring").gameObject.AddComponent<Image>();
                ring.enabled = false;
                var art = LobbyKit.Rect(root.transform, "Art").gameObject.AddComponent<RawImage>();
                art.texture = texture; art.color = Color.magenta; art.uvRect = new Rect(.1f, .2f, .6f, .7f);
                var adapter = root.AddComponent<LobbyInterfaceFrame>();
                adapter.Theme = theme;
                adapter.Bindings = new[] {
                    new LobbyInterfaceFrame.Binding { Target = panel, Role = LobbyColourRole.Panel, Gradient = gradient },
                    new LobbyInterfaceFrame.Binding { Target = ring, Role = LobbyColourRole.Selection }
                };
                adapter.Apply();
                Assert.AreEqual(Color.white, panel.color);
                Assert.AreEqual(theme.Colours.Panel, gradient.From); Assert.AreEqual(theme.Colours.Panel, gradient.To);
                Assert.AreEqual(theme.Colours.Selection, ring.color); Assert.IsFalse(ring.enabled);
                Assert.AreSame(texture, art.texture); Assert.AreEqual(Color.magenta, art.color);
                Assert.AreEqual(new Rect(.1f, .2f, .6f, .7f), art.uvRect);
                theme.Colours.Panel = Color.blue; adapter.Apply();
                Assert.AreEqual(Color.blue, gradient.From);
                Assert.AreSame(texture, art.texture); Assert.IsTrue(texture);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(theme); Object.DestroyImmediate(texture); }
        }

        [Test]
        public void FrameRejectsArtworkAndDuplicateTargetsBeforeAnyColourChanges()
        {
            var root = new GameObject("Theme test", typeof(RectTransform));
            var theme = ScriptableObject.CreateInstance<LobbyInterfaceTheme>();
            try
            {
                var face = LobbyKit.Rect(root.transform, "Body").gameObject.AddComponent<Image>();
                face.color = Color.red;
                var art = LobbyKit.Rect(root.transform, "Art").gameObject.AddComponent<RawImage>();
                var adapter = root.AddComponent<LobbyInterfaceFrame>(); adapter.Theme = theme;
                adapter.Bindings = new[] {
                    new LobbyInterfaceFrame.Binding { Target = face, Role = LobbyColourRole.Card },
                    new LobbyInterfaceFrame.Binding { Target = art, Role = LobbyColourRole.Card }
                };
                Assert.Throws<InvalidOperationException>(adapter.Apply); Assert.AreEqual(Color.red, face.color);
                adapter.Bindings[1].Target = face;
                Assert.Throws<InvalidOperationException>(adapter.Apply); Assert.AreEqual(Color.red, face.color);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(theme); }
        }

        [Test]
        public void MigrationPlanRejectsUnknownVisibleGraphicsAndDoesNotModifyThem()
        {
            var root = new GameObject("Theme test", typeof(RectTransform));
            try
            {
                var image = LobbyKit.Rect(root.transform, "Unreviewed decoration").gameObject.AddComponent<Image>();
                image.color = Color.magenta;
                var method = typeof(LobbySharedThemeSetup).GetMethod("PlanBindings", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.IsNotNull(method);
                var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] { root }));
                Assert.IsInstanceOf<InvalidOperationException>(error.InnerException);
                Assert.AreEqual(Color.magenta, image.color); Assert.IsNull(root.GetComponent<LobbyInterfaceFrame>());
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ApplyingOnlineRolesPreservesSpacingAndBackdropSettings()
        {
            var shared = ScriptableObject.CreateInstance<LobbyInterfaceTheme>();
            var target = ScriptableObject.CreateInstance<WordboundUiTheme>();
            try
            {
                target.Padding = 20; target.Gap = 9; target.MenuWidth = 700;
                target.OnlineBackgroundShade = .42f; target.OnlineUseLobbyBackground = false;
                shared.ApplyOnline(target);
                Assert.AreEqual(shared.Colours.Panel, target.Panel);
                Assert.AreEqual(shared.Colours.OnPrimary, target.PrimaryText);
                Assert.AreEqual(20, target.Padding); Assert.AreEqual(9, target.Gap); Assert.AreEqual(700, target.MenuWidth);
                Assert.AreEqual(.42f, target.OnlineBackgroundShade); Assert.IsFalse(target.OnlineUseLobbyBackground);
                Assert.DoesNotThrow(target.Validate);
            }
            finally { Object.DestroyImmediate(target); Object.DestroyImmediate(shared); }
        }
    }
}
