using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;
using Wreckabulary.Workshop;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class WordboundCourseMaterialTests
    {
        [Test]
        public void ExistingProfileDefaultsRemainOptionalAndRemapsRejectAmbiguousRows()
        {
            var profile = ScriptableObject.CreateInstance<WordboundVisualProfile>();
            var style = ScriptableObject.CreateInstance<WordboundRoomStyleProfile>();
            var source = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var replacement = new Material(source);
            try
            {
                EditorJsonUtility.FromJsonOverwrite("{\"BaseArtwork\":0,\"Overrides\":[]}", profile);
                Assert.IsNull(profile.RoomStyle); Assert.IsEmpty(profile.MaterialRemaps); profile.Validate();
                profile.RoomStyle = style; style.Palettes[0].Wall = new Color(float.NaN, 0, 0, 1);
                Assert.Throws<ArgumentException>(profile.Validate);
                style.Palettes[0].Wall = Color.white;
                profile.MaterialRemaps.Add(new WordboundMaterialRemap { VisualKey = "DESK", Source = source, Replacement = replacement });
                profile.Validate();
                profile.MaterialRemaps.Add(new WordboundMaterialRemap { VisualKey = "DESK", Source = source, Replacement = replacement });
                Assert.Throws<ArgumentException>(profile.Validate);
                profile.MaterialRemaps[1].VisualKey = "SHELF"; profile.Validate();
                profile.MaterialRemaps[1].Replacement = null;
                Assert.Throws<ArgumentException>(profile.Validate);
            }
            finally { Object.DestroyImmediate(profile); Object.DestroyImmediate(style); Object.DestroyImmediate(source); Object.DestroyImmediate(replacement); }
        }

        [Test]
        public void ExactOriginalRemapPreservesMeshesGripsOtherSlotsSkinsAndSourceMaterials()
        {
            var profile = ScriptableObject.CreateInstance<WordboundVisualProfile>();
            var anchor = new GameObject("Owned course material test");
            var original = MaterialLibrary.Load().Find("wood_Classic");
            Assert.IsNotNull(original);
            string before = EditorJsonUtility.ToJson(original);
            var replacement = new Material(original) { name = "wood_Classic", color = new Color(.65f, .12f, .06f) };
            profile.MaterialRemaps.Add(new WordboundMaterialRemap { VisualKey = "TABLE", Source = original, Replacement = replacement });
            try
            {
                var items = GameConfig.Current.Items;
                var item = items.Get("TABLE");
                var size = new Vector3(item.Size[0], item.Size[1], item.Size[2]);
                var baseline = new WordboundVisualResolver(items, false);
                var changed = new WordboundVisualResolver(items, false, profile: profile);
                using var first = baseline.Create("TABLE", size, anchor.transform);
                using var next = changed.Create("TABLE", size, anchor.transform);
                CollectionAssert.AreEqual(Meshes(first.Model), Meshes(next.Model));
                Assert.AreEqual(first.Grip, next.Grip); Assert.AreEqual(first.HeldScale, next.HeldScale);
                Assert.AreEqual(first.TwoHanded, next.TwoHanded);
                var oldSlots = Slots(first.Model); var newSlots = Slots(next.Model);
                Assert.AreEqual(oldSlots.Length, newSlots.Length);
                int changes = 0;
                for (int i = 0; i < oldSlots.Length; i++)
                    if (oldSlots[i].name == "wood_Classic")
                    {
                        changes++; Assert.AreEqual(replacement.color, newSlots[i].color);
                        Assert.AreSame(original.mainTexture, newSlots[i].mainTexture);
                        Assert.IsNotNull(newSlots[i].GetTexture("_DetailNormalMap"), "Normal tactile enhancement still runs after material selection.");
                    }
                    else Assert.AreSame(oldSlots[i], newSlots[i], "Unmatched renderer material slots must remain unchanged.");
                Assert.Greater(changes, 0, "The installed original table must exercise the exact material match.");
                var otherSkin = ModelVisual.Spawn("Items/TABLE", anchor.transform, "Candy", false);
                var mappedSkin = ModelVisual.Spawn("Items/TABLE", anchor.transform, "Candy", false,
                    model => profile.ApplyMaterialRemaps("TABLE", model));
                CollectionAssert.AreEqual(Slots(otherSkin), Slots(mappedSkin), "Classic-only remapping must not repaint another selected skin.");
                Assert.IsTrue(Slots(otherSkin).Any(m => m.name.EndsWith("_Candy", StringComparison.Ordinal)));
                var otherKey = ModelVisual.Spawn("Items/TABLE", anchor.transform, "Classic", false,
                    model => profile.ApplyMaterialRemaps("DESK", model));
                CollectionAssert.AreEqual(oldSlots, Slots(otherKey), "Unmatched visual keys must retain original appearance.");
                Assert.AreEqual(before, EditorJsonUtility.ToJson(original));
            }
            finally
            {
                TactileMaterials.Release(anchor); Object.DestroyImmediate(anchor);
                Object.DestroyImmediate(profile); Object.DestroyImmediate(replacement);
            }
        }

        [Test]
        public void RoomPaletteChangesOnlyAppearanceAndBorrowsExplicitFloorMaterials()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var exact = new WorkshopHistory(preset.Snapshot).Preview(preset.Catalogue, preset.Play);
            Assert.IsTrue(exact.Success);
            var style = ScriptableObject.CreateInstance<WordboundRoomStyleProfile>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Color.magenta };
            style.Palettes[0].FloorMaterial = material;
            WordboundCourseWorld first = null, second = null;
            try
            {
                first = WordboundCourseWorld.Build(exact.Manifest, preset.Catalogue,
                    options: new WordboundCourseWorldOptions { EnableRoomArchitecture = false });
                second = WordboundCourseWorld.Build(exact.Manifest, preset.Catalogue,
                    options: new WordboundCourseWorldOptions { RoomStyle = style });
                Assert.AreEqual(first.ManifestHash, second.ManifestHash);
                CollectionAssert.AreEqual(first.Surfaces.Select(Shape).ToArray(), second.Surfaces.Select(Shape).ToArray());
                var floor = second.Surfaces.First(c => c.GetComponent<WordboundCourseNode>().Kind == WordboundCourseNodeKind.Floor);
                Assert.AreSame(material, floor.GetComponent<Renderer>().sharedMaterials[0]);
            }
            finally
            {
                first?.Dispose(); second?.Dispose();
                Assert.IsTrue(material, "Explicit source materials are borrowed, never owned by the world.");
                Object.DestroyImmediate(style); Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void WorkshopSwitchesCourseFloorsWithoutChangingDraftOrDestroyingBorrowedMaterials()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var owner = new GameObject("Owned workshop floor binding");
            var style = ScriptableObject.CreateInstance<WordboundRoomStyleProfile>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Color.magenta };
            style.Palettes[0].FloorMaterial = material;
            WordboundRoomStyleProfile selected = style;
            string before = preset.Snapshot.RecoveryJson;
            var originalFloor = Surfaces.Get(Surfaces.Kind.Planks, Color.white);
            string originalFloorBefore = EditorJsonUtility.ToJson(originalFloor);
            try
            {
                using (var view = new WordboundWorkshopViewport(owner.transform))
                {
                    view.RoomStyleProvider = () => selected;
                    view.Sync(preset.Snapshot, preset.Catalogue);
                    var rendered = owner.GetComponentsInChildren<MeshRenderer>().Where(r => r.name == "Floor").ToArray();
                    Assert.IsTrue(rendered.Any(r => r.sharedMaterial == material));
                    style.Palettes[0].FloorMaterial = null;
                    view.Sync(preset.Snapshot, preset.Catalogue);
                    var derived = owner.GetComponentsInChildren<MeshRenderer>().Where(r => r.name == "Floor")
                        .Select(r => r.sharedMaterial).First(m => m.color == style.Palettes[0].Floor);
                    Assert.IsNotNull(derived.mainTexture);
                    Assert.AreSame(originalFloor.shader, derived.shader, "Opaque floors must preserve the original Lit shader.");
                    Assert.AreNotSame(originalFloor, derived, "The course tint must be applied to an owned clone.");
                    CollectionAssert.AreEquivalent(originalFloor.shaderKeywords, derived.shaderKeywords);
                    foreach (string property in originalFloor.GetTexturePropertyNames())
                    {
                        Assert.AreSame(originalFloor.GetTexture(property), derived.GetTexture(property), property);
                        Assert.AreEqual(originalFloor.GetTextureScale(property), derived.GetTextureScale(property), property);
                        Assert.AreEqual(originalFloor.GetTextureOffset(property), derived.GetTextureOffset(property), property);
                    }
                    selected = null; view.Sync(preset.Snapshot, preset.Catalogue);
                    Assert.IsTrue(derived == null, "Obsolete owned floor variants must be released on source change.");
                    Assert.IsTrue(material); Assert.AreEqual(before, preset.Snapshot.RecoveryJson);
                }
                Assert.IsTrue(material);
                Assert.AreEqual(originalFloorBefore, EditorJsonUtility.ToJson(originalFloor), "Original surface properties must not be edited by the viewport.");
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(style); Object.DestroyImmediate(material); }
        }

        static string Shape(Collider c) => c.GetComponent<WordboundCourseNode>().Kind + "/" + c.transform.position + "/" +
            c.transform.rotation + "/" + c.transform.localScale + "/" + ((BoxCollider)c).center + "/" + ((BoxCollider)c).size;
        static Mesh[] Meshes(GameObject model) => model.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh)
            .Concat(model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.sharedMesh)).ToArray();
        static Material[] Slots(GameObject model) => model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).ToArray();
    }
}
