using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wreckabulary.Audio;
using Wreckabulary.EditorTools;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTests
{
    public class BeltRangeSetupTests
    {
        [Test]
        public void PrototypeIsEditableAndValid()
        {
            var profile = AssetDatabase.LoadAssetAtPath<FlexibleWeaponProfile>(BeltRangeSetup.ProfilePath);
            Assert.IsNotNull(profile);
            CollectionAssert.IsEmpty(profile.Validate());
            var visual = profile.visual;
            Assert.AreEqual(BeltRangeSetup.VisualPath, AssetDatabase.GetAssetPath(visual));
            var skin = visual.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.AreEqual(8, skin.bones.Length, "root, six strap joints, tip");
            CollectionAssert.AreEqual(new[] { "leather_Classic", "gold_Classic" }, skin.sharedMaterials.Select(m => m.name).ToArray());
            Assert.LessOrEqual(skin.sharedMesh.triangles.Length / 3, 5500);
            Assert.IsEmpty(visual.GetComponentsInChildren<Collider>(true), "visual is render-only");
            Assert.IsEmpty(visual.GetComponentsInChildren<MonoBehaviour>(true), "visual is render-only");
            CollectionAssert.AreEqual(new[] { "leather_Classic", "leather_Candy", "leather_Arcade" }, profile.leather.Select(m => m.name).ToArray());
        }

        [Test]
        public void BeltStaysOutOfSharedCatalogues()
        {
            string items = File.ReadAllText("Assets/_Project/Data/Config/items.json");
            StringAssert.DoesNotContain("\"BELT\"", items);
            Assert.IsFalse(ItemCatalogue.FromJson(items).TryGet("BELT", out _));
            var models = Resources.Load<Wreckabulary.Art.ModelLibrary>(Wreckabulary.Art.ModelLibrary.ResourcePath);
            Assert.IsFalse(models.Entries.Any(e => e.key.Contains("BELT")), "BELT is not a public model");
            var item = FlexibleWeaponDefinition.Belt().ToItem();
            Assert.IsFalse(item.Enabled);
            StringAssert.StartsWith("prototype/BELT/v1|", FlexibleWeaponDefinition.Belt().Identity());
        }

        [Test]
        public void EveryWeaponHasASoundForEveryMaterial()
        {
            var library = AssetDatabase.LoadAssetAtPath<ImpactAudioLibrary>(BeltRangeSetup.LibraryPath);
            Assert.IsNotNull(library);
            Assert.IsFalse(library.useInHouseModes, "House modes keep their original cues until the owner turns this on");
            CollectionAssert.IsEmpty(library.Validate());
            foreach (string weapon in new[] { "BELT", "BAT", "HAMMER", "BROOM", "BLADE", "SPEAR", "PLATE", null })
            {
                Assert.IsTrue(library.Has(ImpactCues.SwingKey(weapon)) || library.Has(ImpactCues.SwingKey(ImpactCues.DefaultWeapon)), weapon);
                foreach (StrikePart part in Enum.GetValues(typeof(StrikePart)))
                    foreach (SurfaceMaterial surface in Enum.GetValues(typeof(SurfaceMaterial)))
                        Assert.IsNotNull(ImpactCues.Resolve(ImpactCues.ContactFallbacks(weapon, part, surface), library.Has), $"{weapon}/{part}/{surface}");
            }
            foreach (SurfaceMaterial surface in Enum.GetValues(typeof(SurfaceMaterial)))
            {
                if (surface == SurfaceMaterial.Body) continue;
                Assert.IsTrue(library.Has(ImpactCues.DamageKey(surface)), surface + " crack");
                Assert.IsTrue(library.Has(ImpactCues.BreakKey(surface)), surface + " break");
            }
            Assert.AreNotEqual(library.Find("contact/BELT/Strap/Glass").clips[0], library.Find("contact/BELT/Buckle/Glass").clips[0]);
        }

        [Test]
        public void SoundClipsAreMonoAndKeepTheirSampleRate()
        {
            var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { BeltRangeSetup.AudioRoot + "/Clips" });
            Assert.AreEqual(189, guids.Length);
            foreach (var guid in guids)
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsTrue(importer.forceToMono, importer.assetPath);
                Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, importer.defaultSampleSettings.loadType, importer.assetPath);
            }
        }

        [Test]
        public void RangeSceneIsInTheBuildAndWired()
        {
            Assert.IsTrue(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == BeltRangeSetup.ScenePath));
            Assert.AreEqual("Assets/_Project/Scenes/Hub.unity", EditorBuildSettings.scenes.First(s => s.enabled).path, "the build still starts in the Hub");
            var scene = EditorSceneManager.OpenPreviewScene(BeltRangeSetup.ScenePath);
            try
            {
                var roots = scene.GetRootGameObjects();
                var director = roots.SelectMany(r => r.GetComponentsInChildren<BeltRangeDirector>(true)).Single();
                Assert.IsNotNull(director.GetComponent<ImpactAudioScope>());
                Assert.IsEmpty(roots.SelectMany(r => r.GetComponentsInChildren<TutorialDirector>(true)));
                Assert.IsNotNull(director.Joins);
                Assert.AreEqual(BeltRangeSetup.ProfilePath, AssetDatabase.GetAssetPath(director.Belt));
                CollectionAssert.AreEquivalent(Enum.GetValues(typeof(SurfaceMaterial)).Cast<SurfaceMaterial>().Where(m => m != SurfaceMaterial.Body),
                    director.Targets.Select(t => t.material));
                Assert.IsTrue(director.Targets.All(t => t.spot && t.health > 0f));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void SetupRunsAgainWithoutChangingAnything()
        {
            string[] paths =
            {
                BeltRangeSetup.ProfilePath, BeltRangeSetup.VisualPath, BeltRangeSetup.LibraryPath, BeltRangeSetup.ScenePath,
                "ProjectSettings/EditorBuildSettings.asset", BeltRangeSetup.AudioRoot + "/Profiles/Weapons/BELT.asset",
            };
            var before = paths.Select(File.ReadAllBytes).ToArray();
            BeltRangeSetup.CreateOrVerify();
            for (int i = 0; i < paths.Length; i++) CollectionAssert.AreEqual(before[i], File.ReadAllBytes(paths[i]), paths[i]);
        }
    }
}
