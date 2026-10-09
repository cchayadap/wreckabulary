using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TMPro;

namespace Wreckabulary.Tests
{
    public class LetterPickupViewTests
    {
        [UnitySetUp] public IEnumerator SetUp() => TestScenes.Reset();
        [UnityTearDown] public IEnumerator TearDown() => TestScenes.Reset();

        [UnityTest]
        public IEnumerator FloatingPickupPreservesPhysicsAndResetsWhenReused()
        {
            var camera = new GameObject("Pickup camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 1.5f, -3f);
            camera.transform.LookAt(Vector3.zero);
            var pool = TilePool.Ensure();
            var tile = pool.Get('A');
            tile.Body.isKinematic = true;
            var collider = tile.GetComponent<BoxCollider>();
            var position = tile.transform.position;
            var rotation = tile.transform.rotation;
            var size = collider.size;
            var center = collider.center;
            var art = tile.transform.Find("ImportedTile");
            Assert.IsNotNull(art, "The generated imported art library must be available.");
            var restPosition = art.localPosition;
            var restRotation = art.localRotation;
            var probe = tile.gameObject.AddComponent<LetterPickupFrameProbe>();
            probe.Configure(art, camera, tile.GetComponentsInChildren<TextMeshPro>(true));

            yield return new WaitForSeconds(.8f);
            Assert.Greater(art.position.y, tile.transform.position.y + .04f);
            Assert.IsTrue(probe.Sampled);
            Assert.Greater(probe.GlyphTowardCamera, .01f, "The authored dark glyph must be in front of the wood, not hidden on its back.");
            Assert.Less(probe.GlyphVerticalSeparation, .08f, "Glyph and wood are sampled together after the pickup's LateUpdate.");
            Assert.AreEqual(0, probe.VisibleFallbacks, "Flat underside TMP must not remain detached on the floor while imported art floats.");
            Assert.AreEqual(position, tile.transform.position);
            Assert.AreEqual(rotation, tile.transform.rotation);
            Assert.AreEqual(size, collider.size);
            Assert.AreEqual(center, collider.center);
            Assert.IsTrue(tile.Body.isKinematic);

            camera.transform.position = new Vector3(0f, 1.5f, -30f);
            yield return new WaitForSeconds(.3f);
            Assert.AreEqual(restPosition, art.localPosition);
            Assert.Less(Quaternion.Angle(restRotation, art.localRotation), .01f);
            Assert.IsFalse(tile.transform.Find("Letter rarity halo").GetComponent<LineRenderer>().enabled);
            Assert.AreEqual(1, probe.VisibleFallbacks, "The underside fallback returns when the art returns to the physical tile.");

            pool.Release(tile);
            var reused = pool.Get('A');
            Assert.AreSame(tile, reused);
            Assert.AreEqual(restPosition, art.localPosition);
            Assert.Less(Quaternion.Angle(restRotation, art.localRotation), .01f);
            Assert.AreEqual(1, reused.GetComponents<LetterPickupView>().Length);
        }
    }

    [DefaultExecutionOrder(1000)]
    public sealed class LetterPickupFrameProbe : MonoBehaviour
    {
        Transform meshTransform, view;
        Vector3 glyphCenter, woodCenter;
        TextMeshPro[] fallbackLabels;
        public bool Sampled { get; private set; }
        public float GlyphTowardCamera { get; private set; }
        public float GlyphVerticalSeparation { get; private set; }
        public int VisibleFallbacks { get; private set; }

        public void Configure(Transform art, Camera camera, TextMeshPro[] labels)
        {
            fallbackLabels = labels;
            view = camera.transform;
            foreach (var filter in art.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<Renderer>();
                if (!renderer || !filter.sharedMesh) continue;
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length && i < filter.sharedMesh.subMeshCount; i++)
                {
                    if (!materials[i] || !materials[i].name.StartsWith("wood_dark")) continue;
                    meshTransform = filter.transform;
                    glyphCenter = filter.sharedMesh.GetSubMesh(i).bounds.center;
                    woodCenter = filter.sharedMesh.bounds.center;
                    break;
                }
            }
            Assert.IsNotNull(meshTransform, "Imported alphabet art must retain its authored dark glyph submesh.");
        }

        void LateUpdate()
        {
            if (!meshTransform || !view) return;
            var separation = meshTransform.TransformPoint(glyphCenter) - meshTransform.TransformPoint(woodCenter);
            GlyphTowardCamera = Vector3.Dot(separation, -view.forward);
            GlyphVerticalSeparation = Mathf.Abs(Vector3.Dot(separation, view.up));
            VisibleFallbacks = 0;
            foreach (var label in fallbackLabels)
                if (label && label.isActiveAndEnabled && label.renderer.enabled) VisibleFallbacks++;
            Sampled = true;
        }
    }
}
