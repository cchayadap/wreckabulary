using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;

namespace Wreckabulary.EditorTests
{
    public sealed class WordboundWorkshopOverlayTests
    {
        [TestCase(-500, 300, 1800, 300)]
        [TestCase(400, -700, 400, 2000)]
        [TestCase(-900, -600, 1800, 1200)]
        [TestCase(1800, 1200, -900, -600)]
        [TestCase(10, 10, 790, 590)]
        public void ClippedLineAndItsFullWidthStayInsideViewport(float x0, float y0, float x1, float y1)
        {
            var bounds = new Rect(0, 0, 800, 600);
            var a = new Vector2(x0, y0); var b = new Vector2(x1, y1);
            Assert.IsTrue(WordboundWorkshopOverlay.ClipLine(bounds, ref a, ref b));
            var delta = (b - a).normalized;
            var normal = new Vector2(-delta.y, delta.x) * WordboundWorkshopOverlay.LineWidth * .5f;
            foreach (var corner in new[] { a + normal, a - normal, b + normal, b - normal })
                Assert.IsTrue(bounds.Contains(corner), corner.ToString("F4"));
        }

        [TestCase(-30, -20, -5, 200)]
        [TestCase(0, 0, 800, 0)]
        [TestCase(20, 20, 20, 20)]
        [TestCase(float.NaN, 20, 300, 300)]
        [TestCase(10, float.PositiveInfinity, 300, 300)]
        public void NonvisibleDegenerateAndNonfiniteLinesDoNotDraw(float x0, float y0, float x1, float y1)
        {
            var a = new Vector2(x0, y0); var b = new Vector2(x1, y1);
            Assert.IsFalse(WordboundWorkshopOverlay.ClipLine(new Rect(0, 0, 800, 600), ref a, ref b));
        }

        [Test]
        public void DenseLabelsRespectEarlierPriorityControlsAndViewportBounds()
        {
            var bounds = new Rect(0, 0, 736, 580);
            var reserved = new List<Rect> { new Rect(0, 0, 448, 106), new Rect(350, 270, 24, 24) };
            var labels = new List<Rect>();
            for (int i = 0; i < 30; i++)
                if (WordboundWorkshopOverlay.PlaceLabel(new Vector2(362, 282), new Vector2(130, 22), bounds, reserved, out var rect))
                {
                    Assert.IsTrue(bounds.Contains(rect.min)); Assert.IsTrue(bounds.Contains(rect.max));
                    Assert.IsFalse(labels.Any(earlier => earlier.Overlaps(rect)));
                    Assert.IsFalse(rect.Overlaps(reserved[0])); Assert.IsFalse(rect.Overlaps(reserved[1]));
                    labels.Add(rect);
                }
            Assert.Greater(labels.Count, 0);
            Assert.Less(labels.Count, 30, "Overview labels must be culled when no free anchored position remains.");
            Assert.IsFalse(WordboundWorkshopOverlay.PlaceLabel(new Vector2(-1, 100), new Vector2(130, 22), bounds, reserved, out _));
        }

        [Test]
        public void FrameUsesActiveVisibleFloorAndFloorSwitchKeepsCourseUnchanged()
        {
            var snapshotSource = new WorkshopSnapshot(CourseTemplates.RaceCircuit());
            var catalogue = CourseTemplates.ProofCatalogue();
            var owner = new GameObject("Workshop framing regression");
            try
            {
                using (var view = new WordboundWorkshopViewport(owner.transform))
                {
                    view.Resize(736, 580); view.Sync(snapshotSource, catalogue); view.Frame();
                    Assert.That(view.Target, Is.EqualTo(new Vector3(0, 2, 0)));
                    var draft = snapshotSource.Edit(); draft.ActiveLayer = "Loft";
                    var snapshot = new WorkshopSnapshot(draft); string before = snapshot.RecoveryJson;
                    view.Sync(snapshot, catalogue);
                    Assert.That(view.Target, Is.EqualTo(new Vector3(10, 8, 0)));
                    foreach (var module in snapshot.Course().Modules.Where(m => m.LayerId == "Loft"))
                    {
                        var box = catalogue.Bounds(module);
                        foreach (int x in new[] { box.Min.X, box.Max.X })
                            foreach (int y in new[] { box.Min.Y, box.Max.Y })
                                foreach (int z in new[] { box.Min.Z, box.Max.Z })
                                {
                                    Assert.IsTrue(view.TryProject(new CoursePoint(x, y, z), new Rect(0, 0, 736, 580), out var screen));
                                    Assert.IsTrue(new Rect(0, 0, 736, 580).Contains(screen));
                                }
                    }
                    Assert.AreEqual(before, snapshot.RecoveryJson);
                    draft.HiddenLayers.Add("Loft"); view.Sync(new WorkshopSnapshot(draft), catalogue); view.Frame();
                    Assert.That(view.Target, Is.EqualTo(new Vector3(0, 2, 0)), "Hidden geometry must not expand the view.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }

        [Test]
        public void NearPlaneCrossingIsClippedAndBehindCameraSegmentsAreRejected()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var owner = new GameObject("Workshop near-plane regression");
            try
            {
                using (var view = new WordboundWorkshopViewport(owner.transform))
                {
                    view.Sync(preset.Snapshot, preset.Catalogue); view.Frame();
                    var camera = owner.GetComponentInChildren<Camera>();
                    var bounds = new Rect(0, 0, 800, 600);
                    CoursePoint Point(Vector3 local)
                    {
                        var position = camera.transform.localPosition + camera.transform.localRotation * local;
                        return new CoursePoint((int)Math.Round(position.x * 1000), (int)Math.Round(position.y * 1000), (int)Math.Round(position.z * 1000));
                    }
                    var behind = Point(new Vector3(-5, 0, -1));
                    Assert.IsFalse(view.TryProject(behind, bounds, out _));
                    Assert.IsFalse(view.ProjectSegment(behind, Point(new Vector3(5, 0, -2)), bounds, out _, out _));
                    Assert.IsTrue(view.ProjectSegment(behind, Point(new Vector3(1, 0, 5)), bounds, out var a, out var b));
                    Assert.IsTrue(bounds.Contains(a)); Assert.IsTrue(bounds.Contains(b));
                    Assert.IsTrue(WordboundWorkshopOverlay.Finite(a)); Assert.IsTrue(WordboundWorkshopOverlay.Finite(b));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }
    }
}
