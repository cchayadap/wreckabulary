using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Online;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class WordboundNativeReleaseOwnershipTests
    {
        readonly List<GameObject> owned = new List<GameObject>();
        bool enteredPlay;

        [UnityTest, Category("NativePhysics"), Timeout(60000)]
        public IEnumerator DisabledCarriedColliderRetainsExactRootOwnershipAndRejectsForeignOrActiveFallbacks()
        {
            Assert.IsFalse(Application.isPlaying);
            enteredPlay = true;
            yield return new EnterPlayMode();
            Assert.IsTrue(Application.isPlaying);
            var root = Create("Owned release root");
            var body = root.AddComponent<Rigidbody>(); body.useGravity = false; body.isKinematic = true;
            var box = root.AddComponent<BoxCollider>();
            yield return null;
            Assert.AreSame(body, box.attachedRigidbody);
            Assert.IsTrue(Matches(box, body));

            box.enabled = false; body.detectCollisions = false;
            yield return null;
            Assert.IsNull(box.attachedRigidbody, "Reproduce the shipped inactive carried-body attachment lifetime.");
            Assert.AreSame(body, box.GetComponent<Rigidbody>());
            Assert.IsTrue(Matches(box, body), "Exact disabled root ownership must survive the transient engine attachment query.");

            var other = Create("Unrelated release body").AddComponent<Rigidbody>();
            other.useGravity = false; other.isKinematic = true; other.detectCollisions = false;
            Assert.IsFalse(Matches(box, other));
            Assert.IsFalse(Matches(null, body)); Assert.IsFalse(Matches(box, null));

            var child = Create("Nested collider"); child.transform.SetParent(root.transform, false);
            var childBox = child.AddComponent<BoxCollider>(); childBox.enabled = false;
            Assert.IsFalse(Matches(childBox, body), "A descendant collider cannot substitute for the exact registered root collider.");
            var childBody = child.AddComponent<Rigidbody>(); childBody.useGravity = false;
            childBody.isKinematic = true; childBody.detectCollisions = false;
            Assert.IsFalse(Matches(childBox, body), "A nested independent body must never be admitted as parent ownership.");

            body.detectCollisions = true;
            yield return null;
            if (box.attachedRigidbody == null)
                Assert.IsFalse(Matches(box, body), "Null attachment is not sufficient when collisions are still enabled.");
            else Assert.AreSame(body, box.attachedRigidbody);
            body.detectCollisions = false; body.isKinematic = false;
            yield return null;
            Assert.IsFalse(Matches(box, body), "A dynamic item cannot use the inactive-kinematic fallback.");
            body.isKinematic = true; box.enabled = true;
            yield return null;
            if (box.attachedRigidbody == null)
                Assert.IsFalse(Matches(box, body), "An enabled collider with no physics attachment cannot use the fallback.");
            else Assert.AreSame(body, box.attachedRigidbody);

            body.detectCollisions = true;
            yield return null;
            Assert.AreSame(body, box.attachedRigidbody);
            Assert.IsTrue(Matches(box, body), "Normal attached-body validation stays valid when the item returns to the world.");
            Debug.Log("WORDBOUND_RELEASE_OWNERSHIP: active root, inactive null attachment, unrelated body, child/nested body, collision-enabled and dynamic fallback checks passed; no authority or collision-sweep bypass.");
        }

        GameObject Create(string name) { var value = new GameObject(name); owned.Add(value); return value; }
        static bool Matches(BoxCollider collider, Rigidbody body)
        {
            var method = typeof(WordboundPhysicsWorld).GetMethod("ReleaseBodyMatches", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return (bool)method.Invoke(null, new object[] { collider, body });
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var value in owned)
                if (value) { if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
            if (Application.isPlaying) yield return null;
            Assert.IsTrue(owned.TrueForAll(value => !value), "Release regression must clean only its owned transient objects.");
            owned.Clear();
            if (Application.isPlaying && enteredPlay) yield return new ExitPlayMode();
            enteredPlay = false;
        }
    }
}
