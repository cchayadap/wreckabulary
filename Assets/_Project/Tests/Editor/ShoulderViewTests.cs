using NUnit.Framework;
using UnityEngine;

namespace Wreckabulary.Tests
{
    public class ShoulderViewTests
    {
        const float Half = Mathf.PI * .5f;

        static void Near(Vector2 want, Vector2 got) => Assert.Less(Vector2.Distance(want, got), 1e-5f, $"{want} vs {got}");
        static void Near(Vector3 want, Vector3 got) => Assert.Less(Vector3.Distance(want, got), 1e-5f, $"{want} vs {got}");

        [Test]
        public void MovingTurnsWithTheView()
        {
            Near(Vector2.up, ShoulderView.CameraRelative(Vector2.up, 0f));
            Near(Vector2.right, ShoulderView.CameraRelative(Vector2.right, 0f));
            Near(new Vector2(1f, 0f), ShoulderView.CameraRelative(Vector2.up, Half));
            Near(new Vector2(0f, -1f), ShoulderView.CameraRelative(Vector2.right, Half));
            Near(new Vector2(-1f, 0f), ShoulderView.CameraRelative(Vector2.down, Half));
            Near(new Vector2(0f, -1f), ShoulderView.CameraRelative(Vector2.up, Mathf.PI));
            Assert.AreEqual(.6f, ShoulderView.CameraRelative(new Vector2(.6f, 0f), 1.1f).magnitude, 1e-5f, "turning keeps the stick's tilt");
        }

        [Test]
        public void HoldingAimMovesInToTheShoulderAndBack()
        {
            var go = new GameObject("Aim camera", typeof(Camera));
            try
            {
                var lens = go.GetComponent<Camera>();
                var view = new ShoulderView();
                var feet = new Vector3(0f, -500f, 0f);
                view.Place(lens, feet, 0f, ShoulderView.DefaultPitch, .02f);
                Assert.AreEqual(ShoulderView.FieldOfView, lens.fieldOfView, 1e-3f);
                var wide = go.transform.position;
                for (int i = 0; i < 60; i++) view.Place(lens, feet, 0f, ShoulderView.DefaultPitch, .02f, true);
                Assert.Greater(view.Aim, .99f);
                Assert.AreEqual(ShoulderView.AimFieldOfView, lens.fieldOfView, .2f);
                Assert.AreEqual(ShoulderView.AimDistance, view.CurrentDistance, .05f);
                Assert.Greater(go.transform.position.x, wide.x + .4f, "over the right shoulder");
                Assert.Greater(go.transform.position.z, wide.z + .8f, "closer behind the back");
                for (int i = 0; i < 120; i++) view.Place(lens, feet, 0f, ShoulderView.DefaultPitch, .02f);
                Assert.Less(view.Aim, .01f);
                Assert.AreEqual(ShoulderView.FieldOfView, lens.fieldOfView, .2f);
                Assert.AreEqual(ShoulderView.Distance, view.CurrentDistance, .05f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ForwardFollowsYawAndPitch()
        {
            Near(Vector3.forward, ShoulderView.Forward(0f, 0f));
            Near(Vector3.right, ShoulderView.Forward(Half, 0f));
            Near(new Vector3(0f, -Mathf.Sin(.16f), Mathf.Cos(.16f)), ShoulderView.Forward(0f, .16f));
            Assert.AreEqual(1f, ShoulderView.Forward(.7f, -.3f).magnitude, 1e-5f);
        }

        [Test]
        public void TheCursorIsCapturedOnlyForAFocusedMouseLookWithNothingOpen()
        {
            for (int i = 0; i < 8; i++)
            {
                bool mouse = (i & 1) != 0, pointer = (i & 2) != 0, focused = (i & 4) != 0;
                Assert.AreEqual(mouse && !pointer && focused, CursorPolicy.WantsLock(mouse, pointer, focused), $"mouse {mouse} pointer {pointer} focused {focused}");
            }
        }

        [Test]
        public void WallsChangeHeightButNotTheirCollider()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var wall = go.AddComponent<TallWall>();
                wall.FloorY = 1f;
                wall.Height = 3.3f;
                wall.Outside = true;
                foreach (var (tall, want) in new[] { (true, TallWall.Exterior), (false, TallWall.Low) })
                {
                    wall.Apply(tall);
                    var t = go.transform;
                    var box = go.GetComponent<BoxCollider>();
                    Assert.AreEqual(want, t.localScale.y, 1e-5f);
                    Assert.AreEqual(1f + want * .5f, t.position.y, 1e-5f);
                    float bottom = t.position.y + (box.center.y - box.size.y * .5f) * t.localScale.y;
                    float top = t.position.y + (box.center.y + box.size.y * .5f) * t.localScale.y;
                    Assert.AreEqual(1f, bottom, 1e-4f, "the collider starts at the floor");
                    Assert.AreEqual(4.3f, top, 1e-4f, "and stops under the floor above");
                }
                wall.Outside = false;
                Assert.AreEqual(TallWall.Interior, wall.VisualHeight(true), 1e-5f);
                wall.Height = 2.2f;
                Assert.AreEqual(2.2f, wall.VisualHeight(true), 1e-5f, "never into the floor above");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
