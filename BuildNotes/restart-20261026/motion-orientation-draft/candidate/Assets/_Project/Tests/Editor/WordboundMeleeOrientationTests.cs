using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Wreckabulary.EditorTests
{
    public sealed class WordboundMeleeOrientationTests
    {
        static readonly Color32[] Reference = {
            new Color32(10, 20, 30, 255), new Color32(40, 50, 60, 255),
            new Color32(70, 80, 90, 255), new Color32(100, 110, 120, 255),
            new Color32(130, 140, 150, 255), new Color32(160, 170, 180, 255) };

        static object Invoke(string name, params object[] args)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Wreckabulary.Qa.WordboundMeleeCapture"))
                .SingleOrDefault(t => t != null);
            Assert.IsNotNull(type, "The editor must load the existing Wordbound QA assembly.");
            var method = type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return method.Invoke(null, args);
        }

        static string Select(Color32[] pixels, Color32[] reference) =>
            (string)Invoke("MatchOrientation", pixels, reference, 2, 3, null);

        [Test]
        public void UprightReferenceSelectsIdentityAndPreservesPixels()
        {
            var pixels = Reference.ToArray();
            Assert.AreEqual("identity", Select(pixels, Reference));
            Invoke("OrientRows", pixels, 2, 3, "identity");
            CollectionAssert.AreEqual(Reference, pixels);
        }

        [Test]
        public void VerticalRowsAreMeasuredAndRestoredIncludingOddMiddleRow()
        {
            var pixels = new[] { Reference[4], Reference[5], Reference[2], Reference[3], Reference[0], Reference[1] };
            Assert.AreEqual("vertical_rows", Select(pixels, Reference));
            Invoke("OrientRows", pixels, 2, 3, "vertical_rows");
            CollectionAssert.AreEqual(Reference, pixels);
        }

        [Test]
        public void SymmetricImageCannotChooseAnOrientation()
        {
            var pixels = Enumerable.Repeat(new Color32(80, 90, 100, 255), 6).ToArray();
            Assert.AreEqual("", Select(pixels, pixels));
        }

        [Test]
        public void ColorTransferMismatchCannotBeHiddenByAFlip()
        {
            var pixels = Reference.Select(p => new Color32((byte)(p.r + 10), p.g, p.b, p.a)).ToArray();
            Assert.AreEqual("", Select(pixels, Reference));
        }

        [Test]
        public void UnmeasuredOrientationAndIncompleteArraysFailClosed()
        {
            var failure = Assert.Throws<TargetInvocationException>(() => Invoke("OrientRows", Reference.ToArray(), 2, 3, "unverified"));
            Assert.IsInstanceOf<ArgumentException>(failure.InnerException);
            failure = Assert.Throws<TargetInvocationException>(() => Select(new Color32[4], Reference));
            Assert.IsInstanceOf<ArgumentException>(failure.InnerException);
        }
    }
}
