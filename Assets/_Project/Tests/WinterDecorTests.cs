using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    public class WinterDecorTests
    {
        string preview;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            preview = LobbyThemes.Current.Id;
            yield return TestScenes.Reset();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.Reset();
            LobbyThemes.Preview(preview);
        }

        [UnityTest]
        public IEnumerator NativeWinterCollectionAppearsOnlyInItsArtworkShowroomAndReusesOneInstance()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return null;
            var menu = LobbyMenu.Instance;
            var stage = menu.Stage;
            var decor = stage.GetComponent<LobbyCollectionDecor>();
            Assert.IsNotNull(decor, "the actual lobby attaches the native collection renderer");
            Assert.IsTrue(LobbyThemes.Preview("winter"));
            decor.Refresh();
            Assert.IsTrue(decor.Visible);
            var instance = decor.Instance;
            Assert.IsTrue(instance.GetComponentsInChildren<Renderer>().Any(renderer => renderer.enabled));
            Assert.IsEmpty(instance.GetComponentsInChildren<Collider>(true));
            Assert.IsEmpty(instance.GetComponentsInChildren<Light>(true));
            Vector3 avatar = stage.Avatar.position;
            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(LobbyThemes.Preview("sunroom"));
                decor.Refresh(); Assert.IsFalse(decor.Visible);
                Assert.IsTrue(LobbyThemes.Preview("winter"));
                decor.Refresh(); Assert.AreSame(instance, decor.Instance); Assert.IsTrue(decor.Visible);
            }
            stage.ShowArtwork(false);
            decor.Refresh(); Assert.IsFalse(decor.Visible, "map selection has its own authored geometry");
            stage.ShowArtwork(true);
            decor.Refresh(); Assert.IsTrue(decor.Visible);
            stage.Release();
            decor.Refresh(); Assert.IsFalse(decor.Visible, "release cannot leave seasonal props in gameplay");
            stage.TakeCamera();
            decor.Refresh(); Assert.AreSame(instance, decor.Instance); Assert.IsTrue(decor.Visible);
            Assert.AreEqual(avatar, stage.Avatar.position);
        }

        [UnityTest]
        public IEnumerator NarrowShopKeepsItsNativeItemViewportClearAndDecorFollowsMapChanges()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return null;
            var menu = LobbyMenu.Instance;
            var stage = menu.Stage;
            var decor = stage.GetComponent<LobbyCollectionDecor>();
            Assert.IsNotNull(decor);
            Assert.IsTrue(LobbyThemes.Preview("winter"));
            decor.Refresh();
            var display = decor.Instance.transform.Find("Wreath display");
            Assert.IsTrue(display.gameObject.activeSelf, "centered Home shows the wreath on its actual stand");
            menu.Open(LobbyMenu.Shop);
            Assert.IsTrue(LobbyThemes.Preview("winter"));
            yield return new WaitForSecondsRealtime(.6f);
            Assert.IsTrue(decor.Visible);
            Assert.IsFalse(display.gameObject.activeSelf, "the stand does not compete with the shop and miniature preview");
            var original = decor.Instance;
            stage.SetMap("terrace");
            yield return null;
            decor.Refresh();
            Assert.AreSame(original, decor.Instance);
            Assert.That(Vector3.Distance(stage.Spot, decor.Instance.transform.position), Is.LessThan(.001f));
            decor.enabled = false;
            Assert.IsFalse(decor.Visible);
        }
    }
}
