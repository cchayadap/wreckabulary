using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundPuzzleGuideViewTests
    {
        Scene scene;
        GameObject anchor;
        WordboundPuzzleGuideView view;
        ResolvedCourse course;

        [OneTimeSetUp]
        public void Content()
        {
            var content = WordboundFlagshipContent.Create(WordboundPuzzleGuidePlanTests.Items());
            course = content.Resolve("0123456789abcdef",
                MatchStateValidation.Requirements(ModeDefinition.For("TimeRun"))).Manifest;
            Assert.IsNotNull(course);
        }
        [SetUp]
        public void Setup()
        {
            scene = EditorSceneManager.NewPreviewScene();
            anchor = new GameObject("Guide owned course");
            SceneManager.MoveGameObjectToScene(anchor, scene);
            anchor.transform.SetPositionAndRotation(new Vector3(11, 3, -7), Quaternion.Euler(0, 90, 0));
            view = new WordboundPuzzleGuideView(anchor.transform);
        }
        [TearDown]
        public void Cleanup()
        {
            view?.Dispose();
            if (anchor) Object.DestroyImmediate(anchor);
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
        }

        [TestCase("CollectLetters"), TestCase("PlaceObject"), TestCase("RedirectWind")]
        [TestCase("TimedMechanism"), TestCase("BuildRoute"), TestCase("BalanceWeight")]
        public void AuthoredFamilyRendersInOwnedSceneWithoutPhysicsOrDuplicateRefresh(string family)
        {
            var progress = WordboundPuzzleGuidePlanTests.Progress(course, ModeDefinition.For("TimeRun"));
            WordboundPuzzleGuidePlanTests.SeekFamily(progress, course, family);
            var plan = WordboundPuzzleGuidePlan.Build(course, progress.Capture(), "a");
            view.Show(plan);
            Assert.AreEqual(plan.Count, view.MarkerCount);
            Assert.AreEqual(scene, view.Root.scene);
            Assert.AreEqual(anchor.transform, view.Root.transform.parent);
            Assert.IsEmpty(view.Root.GetComponentsInChildren<Collider>(true));
            Assert.IsEmpty(view.Root.GetComponentsInChildren<Rigidbody>(true));
            var lines = view.Root.GetComponentsInChildren<LineRenderer>(true);
            Assert.IsNotEmpty(lines); Assert.IsTrue(lines.All(l => !l.useWorldSpace && l.positionCount >= 2));
            var labels = view.Root.GetComponentsInChildren<TextMesh>(true);
            CollectionAssert.AreEquivalent(plan.Select(p => p.Label), labels.Select(l => l.text));
            Assert.IsTrue(labels.All(l => !l.richText && l.font != null));
            int children = view.Root.transform.childCount;
            view.Show(plan);
            Assert.AreEqual(children, view.Root.transform.childCount);
            var first = plan[0];
            var point = new Vector3(first.Position.X / 1000f, first.Position.Y / 1000f, first.Position.Z / 1000f);
            var firstLine = view.Root.transform.GetChild(0).GetComponentInChildren<LineRenderer>();
            Vector3 expected = anchor.transform.TransformPoint(point + new Vector3(0, .035f, first.RadiusMm / 1000f));
            Assert.Less(Vector3.Distance(expected, firstLine.transform.TransformPoint(firstLine.GetPosition(0))), .001f);
        }

        [Test]
        public void EmptyPlanAndDisposeHideAndDestroyOnlyOwnedGraphics()
        {
            var unrelated = new GameObject("Unrelated course content");
            unrelated.transform.SetParent(anchor.transform, false);
            var progress = WordboundPuzzleGuidePlanTests.Progress(course, ModeDefinition.For("TimeRun"));
            view.Show(WordboundPuzzleGuidePlan.Build(course, progress.Capture(), "a"));
            view.Show(System.Array.Empty<WordboundGuideMarker>());
            Assert.IsFalse(view.Root.activeSelf); Assert.AreEqual(0, view.MarkerCount);
            view.Dispose(); view.Dispose();
            Assert.AreEqual(1, anchor.transform.childCount);
            Assert.AreEqual(unrelated.transform, anchor.transform.GetChild(0));
        }
    }
}
