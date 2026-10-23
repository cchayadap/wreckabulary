using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Wreckabulary.Tests
{
    public sealed class WordboundStoryReadabilityTests
    {
        [TestCase(6f), TestCase(12f), TestCase(18f), TestCase(20f)]
        public void ActualNoticeFacingPreservesAnchorAndWordsWithBoundedReadableScale(float distance)
        {
            var root = new GameObject("Story text projection test");
            var cameraRoot = new GameObject("Story text camera");
            try
            {
                root.transform.SetPositionAndRotation(new Vector3(11, 3, -7), Quaternion.Euler(0, 90, 0));
                var signRoot = new GameObject("Actual notice text");
                signRoot.transform.SetParent(root.transform, false);
                var sign = signRoot.AddComponent<TextMesh>();
                sign.text = "Packing Yard\nThe bell rings for an address that nobody wrote.";
                var camera = cameraRoot.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(new Vector3(3, 4, -7), Quaternion.Euler(20, 25, 0));
                sign.transform.position = camera.transform.position + camera.transform.forward * distance + camera.transform.right * 2;
                Vector3 position = sign.transform.position;
                string text = sign.text;
                typeof(Wreckabulary.Online.WordboundStoryView).GetMethod("FaceNotice", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { sign, camera });
                Assert.AreEqual(position, sign.transform.position);
                Assert.AreEqual(text, sign.text);
                float separation = Vector3.Distance(position, camera.transform.position);
                Assert.AreEqual(separation < 20, sign.gameObject.activeSelf);
                if (!sign.gameObject.activeSelf) return;
                Assert.Less(Quaternion.Angle(camera.transform.rotation, sign.transform.rotation), .001f);
                Assert.AreEqual(Mathf.Clamp(separation / 8, .8f, 2), sign.transform.localScale.x, .0001f);
                var left = camera.WorldToViewportPoint(position - sign.transform.right);
                var right = camera.WorldToViewportPoint(position + sign.transform.right);
                Assert.AreEqual(left.y, right.y, .00001f, "An off-centre notice must retain a screen-horizontal text baseline.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraRoot);
            }
        }
    }
}
