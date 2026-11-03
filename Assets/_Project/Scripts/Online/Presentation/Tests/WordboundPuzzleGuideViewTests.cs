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

        [TestCase(false), TestCase(true)]
        public void CheckpointAndFinishCaptionsStayAboveTheirDecorativePost(bool finish)
        {
            var progress = WordboundPuzzleGuidePlanTests.Progress(course, ModeDefinition.For("TimeRun"));
            if (finish)
                while (progress.Team(0).NextCheckpoint < course.Laps[0].Checkpoints.Count)
                    WordboundPuzzleGuidePlanTests.StepCheckpoint(progress, course);
            Assert.IsTrue(progress.TakeKeepsake("a"));
            var plan = WordboundPuzzleGuidePlan.Build(course, progress.Capture(), "a");
            Assert.AreEqual(finish ? WordboundGuideKind.Finish : WordboundGuideKind.Checkpoint, plan[0].Kind);
            view.Show(plan);
            var node = view.Root.transform.GetChild(0);
            var label = node.GetComponentInChildren<TextMesh>();
            var post = node.GetComponentsInChildren<LineRenderer>().Single(line => line.positionCount == 2);
            var at = new Vector3(plan[0].Position.X / 1000f, plan[0].Position.Y / 1000f, plan[0].Position.Z / 1000f);
            Assert.AreEqual(TextAnchor.LowerCenter, label.anchor);
            StringAssert.Contains("\n", label.text);
            Assert.Less(Vector3.Distance(at + Vector3.up, label.transform.localPosition), .0001f);
            Assert.Less(Vector3.Distance(at + Vector3.up * .7f, post.GetPosition(1)), .0001f);
            var cameraRoot = new GameObject("Guide caption projection");
            SceneManager.MoveGameObjectToScene(cameraRoot, scene);
            try
            {
                var camera = cameraRoot.AddComponent<Camera>();
                camera.fieldOfView = 60; camera.aspect = 1280f / 720f;
                foreach (var angle in new[] { new Vector2(15, 0), new Vector2(35, 45), new Vector2(55, 120) })
                {
                    camera.transform.rotation = Quaternion.Euler(angle.x, angle.y, 0);
                    camera.transform.position = label.transform.position - camera.transform.forward * 8;
                    view.FaceLabels(camera);
                    var textAnchor = camera.WorldToViewportPoint(label.transform.position);
                    var postTop = camera.WorldToViewportPoint(post.transform.TransformPoint(post.GetPosition(1)));
                    Assert.Greater(textAnchor.z, 0);
                    Assert.Greater(textAnchor.y - postTop.y, .01f,
                        "Lower-anchored text must start above the post in the sampled camera projection.");
                }
            }
            finally { Object.DestroyImmediate(cameraRoot); }
            Assert.IsEmpty(view.Root.GetComponentsInChildren<Collider>(true));
        }
    }
}
