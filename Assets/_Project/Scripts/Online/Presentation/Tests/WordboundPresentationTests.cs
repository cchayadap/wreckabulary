using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Wreckabulary.Art;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class WordboundPresentationTests
    {
        GameObject anchor;
        WordboundVisualResolver resolver;

        [SetUp]
        public void SetUp()
        {
            anchor = new GameObject("Wordbound presentation test");
            resolver = new WordboundVisualResolver(WordboundTestItems.Raw());
        }
        [TearDown]
        public void TearDown() { if (anchor) Object.DestroyImmediate(anchor); }

        [Test]
        public void WrappedPlayerAdmitsOnlyTheExactSavedHeadCorrection()
        {
            var correction = AvatarSurfaceProfile.Load();
            Assert.IsNotNull(correction, "Create the saved avatar surface correction before this native test.");
            var source = ModelLibrary.Load().Find(WordboundVisualResolver.AvatarKey);
            using var visual = resolver.Create("player", new Vector3(.7f, 1.8f, .7f), anchor.transform);
            Mesh[] Read(GameObject root) => root.GetComponentsInChildren<MeshFilter>(true).Select(filter => filter.sharedMesh)
                .Concat(root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(renderer => renderer.sharedMesh)).ToArray();
            var expected = Read(source);
            Assert.AreNotSame(visual.Root, visual.Model);
            Assert.IsTrue(visual.Model.transform.IsChildOf(visual.Root.transform));
            Assert.IsTrue(correction.MatchesOnlyBoundHeadReplacement(source, visual.Root, expected, Read(visual.Root)));
            var head = visual.Model.transform.Find(correction.RendererPath).GetComponent<SkinnedMeshRenderer>();
            var custom = Object.Instantiate(head.sharedMesh);
            try
            {
                head.sharedMesh = custom;
                Assert.IsFalse(correction.MatchesOnlyBoundHeadReplacement(source, visual.Root, expected, Read(visual.Root)));
                head.sharedMesh = correction.CorrectedMesh;
                var other = visual.Model.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(renderer => renderer != head);
                var originalOther = other.sharedMesh;
                other.sharedMesh = custom;
                Assert.IsFalse(correction.MatchesOnlyBoundHeadReplacement(source, visual.Root, expected, Read(visual.Root)));
                other.sharedMesh = originalOther;
                var impostor = new GameObject("Unrelated head"); impostor.transform.SetParent(visual.Root.transform, false);
                impostor.AddComponent<MeshFilter>().sharedMesh = correction.CorrectedMesh;
                Assert.IsFalse(correction.MatchesOnlyBoundHeadReplacement(source, visual.Root, expected, Read(visual.Root)));
                Object.DestroyImmediate(impostor);
                Assert.IsTrue(correction.MatchesOnlyBoundHeadReplacement(source, visual.Root, expected, Read(visual.Root)));
            }
            finally { head.sharedMesh = correction.CorrectedMesh; Object.DestroyImmediate(custom); }
            Assert.AreSame(correction.SourceMesh, source.transform.Find(correction.RendererPath).GetComponent<SkinnedMeshRenderer>().sharedMesh);
        }

        [TestCase("BAT"), TestCase("BROOM"), TestCase("HAMMER"), TestCase("SPEAR")]
        [TestCase("SHIELD"), TestCase("PLATE"), TestCase("TABLE"), TestCase("SOFA")]
        [TestCase("BED"), TestCase("STOOL"), TestCase("FAN"), TestCase("CLOCK")]
        public void DefaultPresentationUsesOriginalMeshesWithCanonicalBoundsAndGrip(string id)
        {
            var profile = AssetDatabase.LoadAssetAtPath<WordboundVisualProfile>("Assets/_Project/Data/Wordbound/Authoring/Presentation.asset");
            Assert.IsNotNull(profile, "The actual launch presentation profile must exist.");
            Assert.AreEqual(WordboundBaseArtwork.OriginalWreckabulary, profile.BaseArtwork);
            var item = WordboundTestItems.Raw().Get(id);
            var source = ModelLibrary.Load().Find(item.Model);
            Assert.IsNotNull(source, "The original imported model must exist: " + item.Model);
            var target = Vector3.Scale(new Vector3(item.Size[0], item.Size[1], item.Size[2]), new Vector3(.8f, 1.1f, .9f));
            var originals = new WordboundVisualResolver(WordboundTestItems.Raw(), profile: profile);
            using var visual = originals.Create(id, target, anchor.transform);
            SameMeshes(source, visual.Model);
            var bounds = VertexBounds(visual.Model, anchor.transform);
            Near(target, bounds.size);
            Near(new Vector3(0, target.y / 2, 0), bounds.center);
            Assert.IsEmpty(visual.Root.GetComponentsInChildren<Collider>(true));
            Assert.IsEmpty(visual.Root.GetComponentsInChildren<Rigidbody>(true));
            Assert.IsEmpty(visual.Root.GetComponentsInChildren<Collider2D>(true));
            Assert.IsEmpty(visual.Root.GetComponentsInChildren<Rigidbody2D>(true));
            Assert.AreEqual(item.HeldScale, visual.HeldScale); Assert.AreEqual(item.IsTwoHanded, visual.TwoHanded);
            var grip = ModelVisual.FindNamed(visual.Model, "Grip_R");
            var itemGrip = new Vector3(item.Grip[0], item.Grip[1], item.Grip[2]);
            Vector3 contact = grip ? grip.position : visual.Model.transform.parent.TransformPoint(itemGrip);
            Near(visual.Root.transform.InverseTransformPoint(contact), visual.Grip);
            var hand = new GameObject("Original artwork hand").transform;
            hand.SetParent(anchor.transform, false);
            hand.SetPositionAndRotation(new Vector3(2, 3, 4), Quaternion.Euler(17, 43, 71));
            bool miniature = item.Category == ItemCategory.Combat;
            visual.PlaceAtHand(anchor.transform, hand, miniature);
            Near(hand.position, grip ? grip.position : visual.Model.transform.parent.TransformPoint(itemGrip));
            Near(Vector3.one * (miniature ? item.HeldScale : 1), visual.Root.transform.localScale);
        }

        [TestCase("BAT"), TestCase("BROOM"), TestCase("HAMMER"), TestCase("SPEAR")]
        [TestCase("SHIELD"), TestCase("PLATE"), TestCase("TABLE"), TestCase("SOFA")]
        [TestCase("BED"), TestCase("STOOL"), TestCase("FAN"), TestCase("CLOCK")]
        public void CollectionArtworkFitsCanonicalBoundsWithoutPhysicsAndKeepsItsMaterials(string id)
        {
            var collection = WordboundEquipmentCollection.Load();
            Assert.IsNotNull(collection, "Import Wordbound equipment before these native tests.");
            var entry = collection.Find(id);
            var target = Vector3.Scale(entry.size, new Vector3(.8f, 1.1f, .9f));
            var profile = ScriptableObject.CreateInstance<WordboundVisualProfile>();
            profile.BaseArtwork = WordboundBaseArtwork.WordboundEquipment;
            try
            {
                var selected = new WordboundVisualResolver(WordboundTestItems.Raw(), profile: profile);
                using var visual = selected.Create(id, target, anchor.transform);
                SameMeshes(entry.model, visual.Model);
                var bounds = VertexBounds(visual.Model, anchor.transform);
                Near(target, bounds.size);
                Assert.That(Mathf.Abs(bounds.min.y), Is.LessThan(.002f));
                Assert.That(Mathf.Abs(bounds.center.x) + Mathf.Abs(bounds.center.z), Is.LessThan(.002f));
                Assert.IsEmpty(visual.Root.GetComponentsInChildren<Collider>(true));
                Assert.IsEmpty(visual.Root.GetComponentsInChildren<Rigidbody>(true));
                var originals = new HashSet<Material>(entry.model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials));
                Assert.IsTrue(visual.Model.GetComponentsInChildren<Renderer>(true).All(r => r.sharedMaterials.All(originals.Contains)));
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [TestCase(false, false), TestCase(false, true)]
        [TestCase(true, false), TestCase(true, true)]
        public void CollectionOptInRespectsCallerFlagAndNullProfileKeepsItsLegacySelection(bool withProfile, bool allowCollection)
        {
            var profile = withProfile ? ScriptableObject.CreateInstance<WordboundVisualProfile>() : null;
            if (profile) profile.BaseArtwork = WordboundBaseArtwork.WordboundEquipment;
            try
            {
                var selected = new WordboundVisualResolver(WordboundTestItems.Raw(), allowCollection, profile: profile);
                var expected = allowCollection ? WordboundEquipmentCollection.Load().Find("BAT").model : ModelLibrary.Load().Find("Items/BAT");
                using var visual = selected.Create("BAT", new Vector3(.2f, 1f, .2f), anchor.transform);
                SameMeshes(expected, visual.Model);
            }
            finally { if (profile) Object.DestroyImmediate(profile); }
        }

        [Test]
        public void NewProfileDefaultsToOriginalArtworkAndRejectsUnknownChoices()
        {
            var profile = ScriptableObject.CreateInstance<WordboundVisualProfile>();
            try
            {
                Assert.AreEqual(WordboundBaseArtwork.OriginalWreckabulary, profile.BaseArtwork);
                var serialized = new SerializedObject(profile);
                var choice = serialized.FindProperty("BaseArtwork");
                Assert.AreEqual(SerializedPropertyType.Enum, choice.propertyType);
                Assert.AreEqual(0, choice.enumValueIndex);
                CollectionAssert.AreEqual(new[] { "Original Wreckabulary", "Wordbound Equipment" }, choice.enumDisplayNames);
                profile.BaseArtwork = (WordboundBaseArtwork)99;
                Assert.Throws<ArgumentException>(() => new WordboundVisualResolver(WordboundTestItems.Raw(), profile: profile));
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [TestCase("BAT", true), TestCase("TABLE", false)]
        [TestCase("SHIELD", true), TestCase("SOFA", false), TestCase("CLOCK", true)]
        public void HeldArtworkAlignsAuthoredGripAndOnlyWeaponsUseMiniatureScale(string id, bool miniature)
        {
            var item = WordboundTestItems.Raw().Get(id);
            var size = new Vector3(item.Size[0], item.Size[1], item.Size[2]);
            var hand = new GameObject("Authored hand test").transform;
            hand.SetParent(anchor.transform, false);
            hand.SetPositionAndRotation(new Vector3(2, 3, 4), Quaternion.Euler(17, 43, 71));
            using var visual = resolver.Create(id, size, anchor.transform);
            visual.PlaceAtHand(anchor.transform, hand, miniature);
            var grip = ModelVisual.FindNamed(visual.Model, "Grip_R");
            Assert.IsNotNull(grip);
            Near(hand.position, grip.position);
            Near(Vector3.one * (miniature ? item.HeldScale : 1), visual.Root.transform.localScale);
            visual.Show(false);
            Assert.IsFalse(visual.Root.activeSelf);
            visual.Show(true);
            Assert.IsTrue(visual.Root.activeSelf);
        }

        [TestCase("letter:A"), TestCase("letter:Z"), TestCase("platform"), TestCase("keepsake")]
        public void NonItemBodiesResolveExistingArtworkAndGroundTheirBounds(string key)
        {
            var size = key.StartsWith("letter:", StringComparison.Ordinal) ? new Vector3(.3f, .12f, .3f) :
                key == "platform" ? new Vector3(2, .24f, 2) : Vector3.one * .45f;
            using var visual = resolver.Create(key, size, anchor.transform);
            var bounds = ModelVisual.BoundsIn(anchor.transform, visual.Model);
            Near(size, bounds.size);
            Assert.That(Mathf.Abs(bounds.min.y), Is.LessThan(.002f));
        }

        [Test]
        public void WardrobeVisibilitySurvivesBodyRendererRefreshWithoutChangingSavedOutfits()
        {
            string saved = PlayerPrefs.GetString("wv.outfit.0", "");
            using var visual = resolver.Create("player", new Vector3(.6f, 1.8f, .6f), anchor.transform);
            visual.Dress(PlayerAppearance.DefaultPresentationOutfit(WordboundWardrobe.Current).Serialize());
            var renderers = visual.Model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var hidden = renderers.Where(r => r.forceRenderingOff).ToArray();
            Assert.IsNotEmpty(hidden, "The authored avatar must contain unselected wardrobe pieces.");
            foreach (var renderer in renderers) renderer.enabled = true;
            visual.Show(false); visual.Show(true);
            Assert.IsTrue(hidden.All(r => r.forceRenderingOff));
            Assert.IsTrue(renderers.Any(r => !r.forceRenderingOff));
            Assert.AreEqual(saved, PlayerPrefs.GetString("wv.outfit.0", ""));
            using var motion = new WordboundAvatarMotion(visual);
            motion.Step(1f / 60, true, true, false);
            Assert.IsNotNull(visual.RightHand);
            Assert.IsNotNull(visual.LeftHand);
        }

        [Test]
        public void MissingMappingsNeverBecomePrimitiveSubstitutesAndCourseAliasesAreExplicit()
        {
            Assert.Throws<ArgumentException>(() => resolver.Create("unmapped", Vector3.one, anchor.transform));
            Assert.AreEqual(0, anchor.transform.childCount);
            Assert.Throws<ArgumentException>(() => resolver.ModelKey("letter:a"));
            var aliases = new WordboundVisualResolver(WordboundTestItems.Raw(), courseModels:
                new Dictionary<string, string> { ["authored-table"] = "Items/TABLE" });
            Assert.AreEqual("Items/TABLE", aliases.ModelKey("authored-table"));
            Assert.Throws<ArgumentException>(() => new WordboundVisualResolver(WordboundTestItems.Raw(),
                courseModels: new Dictionary<string, string> { ["player"] = "Items/TABLE" }));
        }

        [TestCase("BAT", false, WordboundBaseArtwork.OriginalWreckabulary), TestCase("BAT", true, WordboundBaseArtwork.OriginalWreckabulary)]
        [TestCase("custom-inspector-prop", false, WordboundBaseArtwork.OriginalWreckabulary), TestCase("custom-inspector-prop", true, WordboundBaseArtwork.OriginalWreckabulary)]
        [TestCase("BAT", false, WordboundBaseArtwork.WordboundEquipment), TestCase("BAT", true, WordboundBaseArtwork.WordboundEquipment)]
        [TestCase("custom-inspector-prop", false, WordboundBaseArtwork.WordboundEquipment), TestCase("custom-inspector-prop", true, WordboundBaseArtwork.WordboundEquipment)]
        public void InspectorModelOverrideUsesItsOwnBoundsAndGripInsteadOfOriginalMetadata(string visualKey, bool authoredGrip, WordboundBaseArtwork baseArtwork)
        {
            var profile = ScriptableObject.CreateInstance<WordboundVisualProfile>();
            profile.BaseArtwork = baseArtwork;
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.transform.SetParent(anchor.transform, false);
            Object.DestroyImmediate(model.GetComponent<Collider>());
            model.transform.localPosition = new Vector3(4, 2, -3);
            model.transform.localScale = new Vector3(2, 3, 4);
            if (authoredGrip)
            {
                var grip = new GameObject("Grip_R").transform;
                grip.SetParent(model.transform, false);
                grip.localPosition = new Vector3(.2f, -.3f, .1f);
            }
            profile.Overrides.Add(new WordboundVisualOverride { VisualKey = visualKey, Model = model });
            try
            {
                var custom = new WordboundVisualResolver(WordboundTestItems.Raw(), profile: profile);
                var size = new Vector3(.4f, 1.4f, .3f);
                using var visual = custom.Create(visualKey, size, anchor.transform);
                var bounds = VertexBounds(visual.Model, anchor.transform);
                Near(size, bounds.size);
                Near(new Vector3(0, size.y / 2, 0), bounds.center);
                var hand = new GameObject("Override hand").transform;
                hand.SetParent(anchor.transform, false);
                hand.SetPositionAndRotation(new Vector3(-2, 3, 4), Quaternion.Euler(23, 48, -11));
                visual.PlaceAtHand(anchor.transform, hand, true);
                Vector3 actual = authoredGrip ? ModelVisual.FindNamed(visual.Model, "Grip_R").position :
                    visual.Model.GetComponent<MeshRenderer>().bounds.center;
                Near(hand.position, actual);
                Assert.AreSame(model.GetComponent<MeshRenderer>().sharedMaterial,
                    visual.Model.GetComponent<MeshRenderer>().sharedMaterial);
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [TestCase("BAT"), TestCase("SPEAR")]
        public void ConfirmedMeleeMovesAuthoredHandWhilePreservingLocomotionAndHeldGrip(string word)
        {
            var item = WordboundTestItems.Raw().Get(word);
            using var avatar = resolver.Create("player", new Vector3(.6f, 1.8f, .6f), anchor.transform);
            using var control = resolver.Create("player", new Vector3(.6f, 1.8f, .6f), anchor.transform);
            using var motion = new WordboundAvatarMotion(avatar);
            using var idle = new WordboundAvatarMotion(control);
            motion.Step(.1f, true, true, false); idle.Step(.1f, true, true, false);
            anchor.transform.position += Vector3.forward * .3f;
            long tick = 100 + WordboundCombatPolicy.WindupTicks(item.Melee);
            ObserveMelee(motion, 0, tick, MeleeActor(item), item);
            motion.Step(.03f, true, true, false); idle.Step(.03f, true, true, false);
            Assert.That(Vector3.Distance(avatar.RightHand.position, control.RightHand.position), Is.GreaterThan(.01f));
            foreach (string bone in new[] { "root", "pelvis", "thigh_L", "shin_L", "foot_L", "thigh_R", "shin_R", "foot_R" })
            {
                var a = ModelVisual.FindNamed(avatar.Model, bone);
                var b = ModelVisual.FindNamed(control.Model, bone);
                Near(b.position, a.position);
                Assert.That(Quaternion.Angle(a.rotation, b.rotation), Is.LessThan(.01f), bone);
            }
            using var held = resolver.Create(word, new Vector3(item.Size[0], item.Size[1], item.Size[2]), anchor.transform);
            held.PlaceAtHand(anchor.transform, avatar.RightHand, true);
            Near(avatar.RightHand.position, ModelVisual.FindNamed(held.Model, "Grip_R").position);
        }

        [Test]
        public void DuplicateAndRegressiveMeleeObservationsDoNotRestartTheClip()
        {
            var item = WordboundTestItems.Raw().Get("BAT");
            using var avatar = resolver.Create("player", new Vector3(.6f, 1.8f, .6f), anchor.transform);
            using var control = resolver.Create("player", new Vector3(.6f, 1.8f, .6f), anchor.transform);
            using var motion = new WordboundAvatarMotion(avatar);
            using var reference = new WordboundAvatarMotion(control);
            long tick = 100 + WordboundCombatPolicy.WindupTicks(item.Melee);
            ObserveMelee(motion, 0, tick, MeleeActor(item), item);
            ObserveMelee(reference, 0, tick, MeleeActor(item), item);
            motion.Step(.02f, true, true, false); reference.Step(.02f, true, true, false);
            ObserveMelee(motion, 0, tick, MeleeActor(item), item);
            ObserveMelee(motion, 0, tick - 1, MeleeActor(item), item);
            motion.Step(.02f, true, true, false); reference.Step(.02f, true, true, false);
            Assert.AreEqual(MeleeTime(reference), MeleeTime(motion), .00001);
            Near(control.RightHand.position, avatar.RightHand.position);
        }

        [Test]
        public void LateConfirmedMeleeUsesBoundWeaponPhaseInsteadOfReplayingFromZero()
        {
            var item = new ItemDefinition { Family = HandlingFamily.MeleeSwing, Melee = new MeleeStats
                { Damage = 1, Reach = 1, ArcDegrees = 90, Windup = .8f, Active = .2f, Recovery = .5f } };
            using var avatar = resolver.Create("player", new Vector3(.6f, 1.8f, .6f), anchor.transform);
            using var motion = new WordboundAvatarMotion(avatar);
            ObserveMelee(motion, 0, 100 + WordboundCombatPolicy.WindupTicks(item.Melee), MeleeActor(item), item);
            motion.Step(0, true, true, false);
            var clip = ModelLibrary.Load().FindClip(WordboundVisualResolver.AvatarKey, "Swing_OneHand");
            Assert.AreEqual(clip.length * 7d / 33d, MeleeTime(motion), .00001);
            Assert.That(MeleeWeight(motion), Is.GreaterThan(.9f));
        }

        [Test]
        public void RecoveryRequiresAnObservedAttackAndEndsAtItsConfirmedCooldown()
        {
            var item = WordboundTestItems.Raw().Get("BAT");
            using var avatar = resolver.Create("player", new Vector3(.6f, 1.8f, .6f), anchor.transform);
            using var motion = new WordboundAvatarMotion(avatar);
            long recovery = 100 + WordboundCombatPolicy.WindupTicks(item.Melee) + WordboundCombatPolicy.ActiveTicks(item.Melee);
            ObserveMelee(motion, 0, recovery, MeleeActor(item, attack: false), item);
            motion.Step(0, true, true, false);
            Assert.AreEqual(0, MeleeWeight(motion), "Cooldown alone is not acceptance evidence.");
            ObserveMelee(motion, 1, recovery - 1, MeleeActor(item), item);
            motion.Step(0, true, true, false);
            Assert.That(MeleeWeight(motion), Is.GreaterThan(0));
            ObserveMelee(motion, 1, recovery, MeleeActor(item, attack: false), item);
            motion.Step(0, true, true, false);
            Assert.That(MeleeWeight(motion), Is.GreaterThan(0));
            ObserveMelee(motion, 1, MeleeActor(item).NextAttackTick, MeleeActor(item, attack: false), item);
            motion.Step(0, true, true, false);
            Assert.AreEqual(0, MeleeWeight(motion));
        }

        [TestCase("unavailable"), TestCase("downed"), TestCase("stunned")]
        [TestCase("unequipped"), TestCase("cancelled"), TestCase("defending")]
        public void InterruptedMeleeStopsAndCannotReplayTheSameAttack(string reason)
        {
            var item = WordboundTestItems.Raw().Get("BAT");
            using var avatar = resolver.Create("player", new Vector3(.6f, 1.8f, .6f), anchor.transform);
            using var motion = new WordboundAvatarMotion(avatar);
            ObserveMelee(motion, 0, 101, MeleeActor(item), item);
            motion.Step(0, true, true, false);
            Assert.That(MeleeWeight(motion), Is.GreaterThan(0));
            var actor = new MatchActorState("actor-a", reason == "downed" ? 0 : 100, 100,
                nextAttackTick: MeleeActor(item).NextAttackTick, stunnedUntilTick: reason == "stunned" ? 120 : 0,
                equippedSlot: 0, attack: reason == "cancelled" ? null : new MatchAttackState(100, "item:1"),
                defense: reason == "defending" ? new MatchDefenseState("item:1", 100, 120) : null);
            ObserveMelee(motion, 0, 102, actor, item, reason != "unavailable", reason == "unequipped" ? "item:2" : "item:1");
            motion.Step(0, true, true, false);
            Assert.AreEqual(0, MeleeWeight(motion));
            ObserveMelee(motion, 0, 102, MeleeActor(item), item);
            motion.Step(0, true, true, false);
            Assert.AreEqual(0, MeleeWeight(motion));
        }

        [Test]
        public void PausedMeleeAndStalledSnapshotsDoNotAdvanceWithoutBound()
        {
            var item = WordboundTestItems.Raw().Get("HAMMER");
            using var avatar = resolver.Create("player", new Vector3(.6f, 1.8f, .6f), anchor.transform);
            using var motion = new WordboundAvatarMotion(avatar);
            ObserveMelee(motion, 0, 105, MeleeActor(item), item);
            motion.Step(0, true, true, false);
            double initial = MeleeTime(motion);
            var pausedStep = typeof(WordboundAvatarMotion).GetMethod("Step", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 20; i++) pausedStep.Invoke(motion, new object[] { .1f, true, true, false, false });
            Assert.AreEqual(initial, MeleeTime(motion), .00001);
            motion.Step(.1f, true, true, false);
            double bounded = MeleeTime(motion);
            for (int i = 0; i < 20; i++) motion.Step(.1f, true, true, false);
            Assert.AreEqual(bounded, MeleeTime(motion), .00001);
            motion.Step(0, false, true, false);
            Assert.AreEqual(0, MeleeWeight(motion));
        }

        [Test]
        public void EpochRestoreSamplesItsConfirmedPhaseAndRejectsThePriorEpoch()
        {
            var item = WordboundTestItems.Raw().Get("HAMMER");
            using var avatar = resolver.Create("player", new Vector3(.6f, 1.8f, .6f), anchor.transform);
            using var motion = new WordboundAvatarMotion(avatar);
            ObserveMelee(motion, 0, 110, MeleeActor(item), item);
            motion.Step(0, true, true, false);
            double later = MeleeTime(motion);
            ObserveMelee(motion, 1, 105, MeleeActor(item), item);
            motion.Step(0, true, true, false);
            double restored = MeleeTime(motion);
            Assert.That(restored, Is.GreaterThan(0));
            Assert.That(restored, Is.LessThan(later));
            ObserveMelee(motion, 0, 110, MeleeActor(item), item);
            motion.Step(0, true, true, false);
            Assert.AreEqual(restored, MeleeTime(motion), .00001);
        }

        static MatchActorState MeleeActor(ItemDefinition item, bool attack = true) =>
            new MatchActorState("actor-a", 100, 100,
                nextAttackTick: 100 + WordboundCombatPolicy.CycleTicks(item.Melee), equippedSlot: 0,
                attack: attack ? new MatchAttackState(100, "item:1") : null);

        static void ObserveMelee(WordboundAvatarMotion motion, long epoch, long tick, MatchActorState actor,
            ItemDefinition item, bool available = true, string equipped = "item:1") =>
            typeof(WordboundAvatarMotion).GetMethod("ObserveMelee", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(motion, new object[] { epoch, tick, actor, equipped, item, available });

        static float MeleeWeight(WordboundAvatarMotion motion) =>
            ((AnimationLayerMixerPlayable)typeof(WordboundAvatarMotion).GetField("layers", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(motion)).GetInputWeight(2);

        static double MeleeTime(WordboundAvatarMotion motion) =>
            ((AnimationClipPlayable)typeof(WordboundAvatarMotion).GetField("swing", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(motion)).GetTime();

        static void Near(Vector3 expected, Vector3 actual) =>
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(.002f), $"Expected {expected}, got {actual}");

        static void SameMeshes(GameObject expected, GameObject actual)
        {
            Mesh[] Meshes(GameObject model) => model.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh)
                .Concat(model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.sharedMesh)).ToArray();
            var source = Meshes(expected);
            Assert.IsNotEmpty(source);
            CollectionAssert.AreEquivalent(source, Meshes(actual), "Spawn the selected imported mesh assets, not merely a matching object name.");
        }

        static Bounds VertexBounds(GameObject model, Transform space)
        {
            var bounds = new Bounds();
            bool initialized = false;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                using var data = MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh);
                using var vertices = new NativeArray<Vector3>(data[0].vertexCount, Allocator.Temp);
                data[0].GetVertices(vertices);
                var matrix = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (var vertex in vertices)
                {
                    var point = matrix.MultiplyPoint3x4(vertex);
                    if (initialized) bounds.Encapsulate(point);
                    else { initialized = true; bounds = new Bounds(point, Vector3.zero); }
                }
            }
            Assert.IsTrue(initialized);
            return bounds;
        }
    }
}
