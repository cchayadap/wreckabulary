using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Online;
using Wreckabulary.Online.Editor;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class WordboundCoursePreviewNavigationTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void KitchenFramingAndReturnToWholeMapPreserveTheExactSavedPlayableCourse()
        {
            string path = WordboundAuthoringSetup.FlagshipPath;
            var asset = AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(path);
            Assert.IsNotNull(asset);
            var bytes = File.ReadAllBytes(path);
            var exact = asset.Resolve();
            string input = asset.InputHash;
            var window = ScriptableObject.CreateInstance<WordboundCoursePreview>();
            var type = typeof(WordboundCoursePreview);
            try
            {
                type.GetMethod("Initialize", Private).Invoke(window, new object[] { asset, exact });
                Assert.AreEqual("", type.GetField("error", Private).GetValue(window));
                var names = (string[])type.GetField("viewNames", Private).GetValue(window);
                int kitchen = System.Array.IndexOf(names, "Kitchen ServiceLanding");
                Assert.Greater(kitchen, 0, "The actual kitchen must be reachable from the room selector.");
                var wholeCenter = (Vector3)type.GetField("center", Private).GetValue(window);
                float wholeDistance = (float)type.GetField("distance", Private).GetValue(window);
                type.GetField("selectedView", Private).SetValue(window, kitchen);
                type.GetMethod("FrameSelection", Private).Invoke(window, null);
                var center = (Vector3)type.GetField("center", Private).GetValue(window);
                Assert.Less(Vector3.Distance(center, new Vector3(-36, 11, -12)), .001f);
                Assert.Less((float)type.GetField("distance", Private).GetValue(window), wholeDistance);
                type.GetField("selectedView", Private).SetValue(window, 0);
                type.GetMethod("FrameSelection", Private).Invoke(window, null);
                Assert.AreEqual(wholeCenter, type.GetField("center", Private).GetValue(window));
                Assert.AreEqual(wholeDistance, type.GetField("distance", Private).GetValue(window));
                Assert.AreEqual(input, asset.InputHash);
                Assert.AreEqual(exact.Hash, asset.CreateContent(exact).Course.Hash);
                Assert.AreEqual(exact.Spawns.First(s => s.Kind == "Start").Position,
                    asset.Resolve().Spawns.First(s => s.Kind == "Start").Position);
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path));
                Assert.IsFalse(EditorUtility.IsDirty(asset));
            }
            finally { Object.DestroyImmediate(window); }
        }
    }
}
