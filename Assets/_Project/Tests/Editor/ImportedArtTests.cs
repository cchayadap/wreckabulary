using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.EditorTools;

namespace Wreckabulary.EditorTests
{
    [TestFixture]
    public class ImportedArtTests
    {
        List<ImportedArtSettings.ReportFile> report;
        MaterialLibrary library;

        [OneTimeSetUp]
        public void Load()
        {
            report = ImportedArtSettings.ReadReport();
            library = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(ImportedArtSettings.LibraryAssetPath);
        }

        [Test]
        public void EveryModelInTheReportIsImported()
        {
            Assert.AreEqual(98, report.Count, "the pipeline converts 98 files");
            foreach (var file in report)
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(file.Output), file.Output);
        }

        [Test]
        public void ImportedBoundsMatchBlender()
        {
            var wrong = new List<string>();
            foreach (var file in report)
            {
                var bounds = BoundsOf(file.Output);
                var min = new Vector3(file.BoundsMin[0], file.BoundsMin[1], file.BoundsMin[2]);
                var max = new Vector3(file.BoundsMax[0], file.BoundsMax[1], file.BoundsMax[2]);
                float tolerance = file.Kind == "avatar" ? 0.05f : 0.005f + 0.005f * (max - min).magnitude;
                if (!Near(bounds.min, min, tolerance) || !Near(bounds.max, max, tolerance))
                    wrong.Add($"{file.Name}: imported {bounds.min.ToString("F3")} to {bounds.max.ToString("F3")}, Blender {min.ToString("F3")} to {max.ToString("F3")}");
                if (file.Kind == "item" && Mathf.Abs(bounds.min.y) > 0.005f)
                    wrong.Add($"{file.Name}: bottom at {bounds.min.y:F3} m, not on the floor");
            }
            Assert.IsEmpty(wrong, string.Join("\n", wrong));
        }

        [Test]
        public void TheAvatarFacesForwardInEveryClip()
        {
            var avatar = report.Single(f => f.Kind == "avatar");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(avatar.Output);
            var copy = Object.Instantiate(model);
            try
            {
                var glasses = copy.GetComponentsInChildren<Renderer>(true).Single(r => r.name == "SK_Glasses");
                var head = Find(copy.transform, "head");
                Assert.Greater(glasses.bounds.center.z, head.position.z + 0.05f, "glasses in front of the head");
                AssertStandingForward(copy, "bind pose");

                var clips = AssetDatabase.LoadAllAssetsAtPath(avatar.Output).OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview__"));
                foreach (var clip in clips)
                    foreach (float t in new[] { 0f, 0.5f, 1f })
                    {
                        clip.SampleAnimation(copy, clip.length * t);
                        AssertStandingForward(copy, $"{clip.name} at {t:P0}");
                    }
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }

        static void AssertStandingForward(GameObject avatar, string when)
        {
            Vector3 At(string bone) => Find(avatar.transform, bone).position;
            Assert.Greater(At("upper_arm_R").x, At("upper_arm_L").x + 0.1f, when + ": right shoulder on +X");
            Assert.Greater(At("head").y, At("pelvis").y + 0.1f, when + ": head above pelvis");
            var pelvis = At("pelvis");
            Assert.Less(new Vector2(pelvis.x, pelvis.z).magnitude, 0.3f, when + ": stays in place");
        }

        [Test]
        public void EveryItemHasAGrip()
        {
            foreach (var file in report.Where(f => f.Kind == "item"))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(file.Output);
                Assert.IsNotNull(Find(model.transform, "Grip_R"), $"{file.Name} has no Grip_R");
            }
        }

        [Test]
        public void NoCamerasOrLightsCameThrough()
        {
            foreach (var file in report)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(file.Output);
                Assert.IsEmpty(model.GetComponentsInChildren<Camera>(true), file.Name);
                Assert.IsEmpty(model.GetComponentsInChildren<Light>(true), file.Name);
            }
        }

        [Test]
        public void EveryRendererUsesTheMaterialLibrary()
        {
            Assert.IsNotNull(library, "run Wreckabulary → Art → Set Up Imported Art");
            var wrong = new List<string>();
            foreach (var file in report)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(file.Output);
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        if (m == null || library.Find(m.name) != m)
                            wrong.Add($"{file.Name}/{r.name}: {(m == null ? "no material" : AssetDatabase.GetAssetPath(m) + " " + m.name)}");
            }
            Assert.IsEmpty(wrong, string.Join("\n", wrong.Take(20)));
        }

        [Test]
        public void TheLibraryMatchesMaterialsJson()
        {
            var specs = ImportedArtSettings.ReadMaterials();
            Assert.AreEqual(specs.Count, library.Materials.Count);
            foreach (var spec in specs)
            {
                var m = library.Find(spec.Name);
                Assert.IsNotNull(m, spec.Name);
                Assert.AreEqual("Universal Render Pipeline/Lit", m.shader.name, spec.Name);
                var linear = m.GetColor("_BaseColor").linear;
                Assert.AreEqual(spec.LinearBaseColor.r, linear.r, 0.01f, spec.Name);
                Assert.AreEqual(spec.LinearBaseColor.g, linear.g, 0.01f, spec.Name);
                Assert.AreEqual(spec.LinearBaseColor.b, linear.b, 0.01f, spec.Name);
                bool transparent = spec.AlphaMode == "BLEND";
                Assert.AreEqual(transparent, m.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.Transparent, spec.Name + " render queue");
                Assert.AreEqual(spec.NormalMap != null, m.IsKeywordEnabled("_NORMALMAP"), spec.Name + " normal map");
            }
        }

        [Test]
        public void SkinsSwapWithinAFamily()
        {
            var classic = library.Materials.First(m => m.name.EndsWith("_Classic"));
            string family = MaterialLibrary.FamilyOf(classic.name);
            Assert.AreEqual(family + "_Candy", library.ForSkin(classic, "Candy").name);
            Assert.AreEqual(family + "_Arcade", library.ForSkin(classic, "Arcade").name);
            Assert.AreSame(classic, library.ForSkin(classic, "Gold"), "an unknown skin falls back to the standard look");
            Assert.AreSame(classic, library.ForSkin(library.ForSkin(classic, "Candy"), "Classic"));
            var avatarMaterial = library.Find("fabric_main");
            Assert.AreSame(avatarMaterial, library.ForSkin(avatarMaterial, "Candy"), "materials without skins don't change");
        }

        [Test]
        public void TheAvatarHasItsRigAndClips()
        {
            var avatar = report.Single(f => f.Kind == "avatar");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(avatar.Output);
            foreach (string bone in avatar.Bones)
                Assert.IsNotNull(Find(model.transform, bone), "bone " + bone);

            var clips = AssetDatabase.LoadAllAssetsAtPath(avatar.Output).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToList();
            CollectionAssert.AreEquivalent(avatar.Clips, clips.Select(c => c.name).ToList());
            foreach (var clip in clips)
                Assert.AreEqual(ImportedArtSettings.LoopingClips.Contains(clip.name), clip.isLooping, clip.name + " looping");
            var idle = clips.Single(c => c.name == "Idle");
            Assert.AreEqual(72f / 30f, idle.length, 0.05f, "Idle is 72 frames at 30 fps");
        }

        [Test]
        public void NormalMapsAreImportedAsNormalMaps()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ImportedArtSettings.Root + "Textures" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                bool normal = path.EndsWith("_NormalGL.png");
                Assert.AreEqual(normal ? TextureImporterType.NormalMap : TextureImporterType.Default, ti.textureType, path);
                Assert.AreEqual(TextureImporterShape.Texture2D, ti.textureShape, path);
                Assert.IsFalse(ImportedArtSettings.AppliesGammaDecoding(ti), path + " gamma decoding");
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Texture2D>(path), path + " loads as a 2D texture");
                Assert.AreEqual(!normal, ti.sRGBTexture, path);
                Assert.LessOrEqual(ti.maxTextureSize, 512, path);
            }
        }

        static Bounds BoundsOf(string path)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var copy = Object.Instantiate(model);
            try
            {
                var renderers = copy.GetComponentsInChildren<Renderer>(true);
                var bounds = renderers[0].bounds;
                foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
                return bounds;
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }

        static bool Near(Vector3 a, Vector3 b, float tolerance) =>
            Mathf.Abs(a.x - b.x) <= tolerance && Mathf.Abs(a.y - b.y) <= tolerance && Mathf.Abs(a.z - b.z) <= tolerance;

        static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = Find(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
