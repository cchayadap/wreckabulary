using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Art;

namespace Wreckabulary.EditorTests
{
    [TestFixture]
    public sealed class TactileMaterialsLifecycleTests
    {
        readonly List<GameObject> roots = new List<GameObject>();
        Material source;
        int baselineScopes;

        [SetUp]
        public void Prepare()
        {
            TactileMaterials.CollectUnused();
            baselineScopes = TactileMaterials.RegisteredScopeCount;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.IsNotNull(shader, "URP must import before native material tests.");
            source = new Material(shader) { name = "wood_Classic" };
            source.SetFloat("_Smoothness", .38f);
            source.SetFloat("_Metallic", 0f);
        }

        GameObject Prop(bool active)
        {
            var root = new GameObject("Tactile lifecycle test");
            root.SetActive(active);
            root.AddComponent<MeshRenderer>().sharedMaterial = source;
            roots.Add(root);
            TactileMaterials.Apply(root);
            Assert.AreNotSame(source, root.GetComponent<Renderer>().sharedMaterial,
                "The inspected prop family must actually receive a cached variant.");
            return root;
        }

        [Test]
        public void FailedNeverActivatedStageCannotKeepADeadOwner()
        {
            var stage = Prop(false);
            Assert.AreEqual(baselineScopes + 1, TactileMaterials.RegisteredScopeCount);
            Object.DestroyImmediate(stage);
            TactileMaterials.CollectUnused();
            Assert.AreEqual(baselineScopes, TactileMaterials.RegisteredScopeCount);
            if (baselineScopes == 0)
            {
                Assert.AreEqual(0, TactileMaterials.SharedMaterialCount);
                Assert.AreEqual(0, TactileMaterials.GeneratedTextureCount);
            }
        }

        [Test]
        public void AbandonedInactiveStageCannotDisposeDetailsUsedByALiveSibling()
        {
            var live = Prop(true);
            var stage = Prop(false);
            var material = live.GetComponent<Renderer>().sharedMaterial;
            var normal = material.GetTexture("_DetailNormalMap");
            Assert.AreSame(material, stage.GetComponent<Renderer>().sharedMaterial,
                "Repeated props must share one enriched material.");
            Object.DestroyImmediate(stage);
            TactileMaterials.CollectUnused();
            Assert.AreEqual(baselineScopes + 1, TactileMaterials.RegisteredScopeCount);
            Assert.IsTrue(material && normal, "A live owner's material and detail texture must survive.");
            Assert.AreSame(material, live.GetComponent<Renderer>().sharedMaterial);
            Assert.AreSame(normal, material.GetTexture("_DetailNormalMap"));
            Assert.AreEqual(.38f, source.GetFloat("_Smoothness"), .00001f, "The authoritative material remains unchanged.");
        }

        [Test]
        public void ExplicitStageReleaseRestoresOriginalMaterialsBeforeDisposal()
        {
            var stage = Prop(false);
            TactileMaterials.Release(stage);
            Assert.IsFalse(stage.activeSelf, "Releasing surface ownership must not activate a stage.");
            Assert.AreSame(source, stage.GetComponent<Renderer>().sharedMaterial);
            Assert.AreEqual(baselineScopes, TactileMaterials.RegisteredScopeCount);
            TactileMaterials.Refresh(stage);
            Assert.AreSame(source, stage.GetComponent<Renderer>().sharedMaterial,
                "A released scope cannot silently reclaim disposed resources on a later skin refresh.");
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var root in roots)
                if (root) { TactileMaterials.Release(root); Object.DestroyImmediate(root); }
            roots.Clear();
            if (source) Object.DestroyImmediate(source);
            TactileMaterials.CollectUnused();
        }
    }
}
