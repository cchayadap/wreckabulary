using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Playables;
using Wreckabulary.Art;

namespace Wreckabulary.Tests
{
    public sealed class PlayerAppearanceTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = Vector3.down * .5f;
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
        }

        [UnityTearDown]
        public IEnumerator TearDown() => TestScenes.Reset();

        [UnityTest]
        public IEnumerator AuthoredAvatarIsReusedWithoutReplacingItsHierarchy()
        {
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            var appearance = player.GetComponent<PlayerAppearance>();
            Assert.IsNotNull(appearance, "Upgrade the player prefab before authoring or testing.");
            var authored = appearance.AuthoredAvatar;
            Assert.IsNotNull(authored);
            int meshCount = player.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
            Assert.That(meshCount, Is.GreaterThan(0));
            var wrapper = authored.transform.parent;
            var artistOffset = new Vector3(.03f, .01f, -.02f);
            wrapper.localPosition = artistOffset;
            var accessory = new GameObject("Artist scarf clasp").AddComponent<MeshRenderer>();
            accessory.transform.SetParent(player.visual, false);
            accessory.gameObject.AddComponent<MeshFilter>().sharedMesh = GameAssets.I.blockMesh;
            accessory.sharedMaterial = authored.GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMaterial;
            accessory.transform.localScale = Vector3.one * .03f;

            player.Setup(0, new ScriptedBinding());
            player.Respawn(Vector3.zero);
            Assert.AreSame(authored, appearance.AvatarModel);
            Assert.IsTrue(appearance.Initialize(player));
            yield return new WaitForSeconds(.2f);

            Assert.AreSame(authored, appearance.AvatarModel);
            Assert.That(player.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, Is.EqualTo(meshCount));
            Assert.That(Vector3.Distance(wrapper.localPosition, artistOffset), Is.LessThan(.0001f));
            Assert.IsTrue(accessory.enabled, "Authored accessories outside the imported skeleton must not be treated as obsolete geometry.");
            Assert.AreEqual("Idle", appearance.LocomotionClip);
        }

        [UnityTest]
        public IEnumerator ScenePreviewDisablesItselfBeforeGameplay()
        {
            var preview = new GameObject("Editor preview");
            preview.AddComponent<EditorScenePreview>();
            Assert.IsFalse(preview.activeSelf);
            yield return null;
            Assert.IsFalse(preview.activeSelf);
            Object.Destroy(preview);
        }

        [UnityTest]
        public IEnumerator CarryingUsesMovingLegsAndKeepsTheMiniatureInTheAnimatedHand()
        {
            var input = new ScriptedBinding();
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, input);
            player.Respawn(Vector3.zero);
            var appearance = player.GetComponent<PlayerAppearance>();
            Assert.IsNotNull(appearance.AvatarModel);
            var animator = appearance.AvatarModel.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator);
            Assert.AreEqual(AnimatorCullingMode.AlwaysAnimate, animator.cullingMode,
                "The headless/offscreen simulation must keep gameplay sockets animated.");
            var table = CatalogGear.Create(GameConfig.Current.Items.Get("TABLE"));
            Assert.IsTrue(player.Combat.TryEquip(table));
            var probe = player.gameObject.AddComponent<AppearanceFrameProbe>();
            probe.Configure(player, appearance, table);
            input.Next.move = Vector2.up;
            yield return new WaitForSeconds(.6f);

            Assert.AreEqual("Run_InPlace", appearance.LocomotionClip,
                "Carrying must not replace the entire moving body with a stationary carry clip.");
            Assert.That(appearance.UpperBodyWeight, Is.GreaterThan(.95f));
            Assert.That(appearance.UpperBodyClip, Is.EqualTo("Carry_TwoHand").Or.EqualTo("Hold_OneHand"));
            Assert.IsTrue(probe.Sampled);
            var before = probe.ThighRotation;
            yield return new WaitForSeconds(.13f);
            Assert.That(Quaternion.Angle(before, probe.ThighRotation), Is.GreaterThan(2f), "The leg actually animates under the carry mask.");
            Assert.That(probe.GripSeparation, Is.LessThan(.01f), "Sample the completed animation/socket frame, after LateUpdate.");
            Assert.That(probe.ItemGripSeparation, Is.LessThan(.035f), "The miniature's authored grip must meet the mitten.");
            Assert.That(table.transform.localScale.x, Is.EqualTo(table.Definition.HeldScale).Within(.001f));
            var renderers = table.GetComponentsInChildren<Renderer>();
            Assert.That(renderers.Length, Is.GreaterThan(0));
            foreach (var renderer in renderers)
            {
                Assert.IsTrue(renderer.enabled && renderer.gameObject.activeInHierarchy && !renderer.forceRenderingOff,
                    renderer.name + " must remain visible in the hand.");
                Assert.That(Vector3.Distance(renderer.bounds.center, appearance.RightGrip.position), Is.LessThan(.7f),
                    renderer.name + " miniature must stay within the animated grip's reach.");
            }
        }

        [UnityTest]
        public IEnumerator RaisedShieldAndSwingKeepTheirAuthoredGripInTheMitten()
        {
            var input = new ScriptedBinding();
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, input);
            player.Respawn(Vector3.zero);
            var look = player.GetComponent<PlayerAppearance>();
            var shield = CatalogGear.Create(GameConfig.Current.Items.Get("SHIELD"));
            Assert.IsTrue(player.Combat.TryEquip(shield));
            var probe = player.gameObject.AddComponent<AppearanceFrameProbe>();
            probe.Configure(player, look, shield);
            yield return new WaitForSeconds(.35f);
            float restingHeight = look.RightGrip.position.y;
            input.Next.blockHeld = true;
            yield return new WaitForSeconds(.55f);
            Assert.IsTrue(player.Combat.IsBlocking);
            Assert.That(probe.ItemGripSeparation, Is.LessThan(.035f), "Rotating a guard keeps the item's authored grip in the mitten.");
            Assert.Greater(look.RightGrip.position.y, restingHeight + .08f, "Raised protection visibly lifts the arm.");
            Assert.Greater(Vector3.Dot(shield.transform.up, Vector3.up), .8f, "The shield stays upright.");
            input.Next.blockHeld = false;
            yield return new WaitForSeconds(.2f);
            Assert.That(probe.ItemGripSeparation, Is.LessThan(.035f));
            player.Combat.ResetForRound();
            var broom = CatalogGear.Create(GameConfig.Current.Items.Get("BROOM"));
            Assert.IsTrue(player.Combat.TryEquip(broom));
            probe.Configure(player, look, broom);
            yield return new WaitForSeconds(.35f);
            broom.Use(player.Combat);
            yield return new WaitForSeconds(broom.Definition.Melee.Windup + .05f);
            Assert.That(probe.ItemGripSeparation, Is.LessThan(.035f), "Swing pivots around the authored handle.");
        }

        [UnityTest]
        public IEnumerator ShortDrinkFitsItsChannelAndConsumptionDoesNotPlayAThrow()
        {
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, new ScriptedBinding());
            player.Respawn(Vector3.zero);
            var water = CatalogGear.Create(GameConfig.Current.Items.Get("WATER"));
            Assert.IsTrue(player.Combat.TryEquip(water));
            yield return new WaitForSeconds(.35f);
            var look = player.GetComponent<PlayerAppearance>();
            water.Use(player.Combat);
            yield return null;
            Assert.IsTrue(look.IsUsingItemPose);
            Assert.AreEqual("Drink_Consumable", look.UpperBodyClip);
            Assert.That(look.CurrentPlayable.GetSpeed(), Is.EqualTo(
                look.CurrentAnimationClip.length / water.Definition.Use.ChannelSeconds).Within(.01f));
            yield return new WaitForSeconds(.4f);
            Assert.IsFalse(player.Combat.Weapon);
            Assert.IsFalse(look.IsUsingItemPose);
            Assert.AreNotEqual("Throw_OneHand", look.UpperBodyClip,
                "Eating or drinking spends the held item without a phantom throwing gesture.");
        }

        [UnityTest]
        public IEnumerator FoodPoseFreezesWithPauseAndCancellationKeepsTheHeldGrip()
        {
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, new ScriptedBinding());
            player.Respawn(Vector3.zero);
            var cake = CatalogGear.Create(GameConfig.Current.Items.Get("CAKE"));
            Assert.IsTrue(player.Combat.TryEquip(cake));
            var look = player.GetComponent<PlayerAppearance>();
            var probe = player.gameObject.AddComponent<AppearanceFrameProbe>();
            probe.Configure(player, look, cake);
            yield return new WaitForSeconds(.35f);
            cake.Use(player.Combat);
            yield return new WaitForSeconds(.2f);
            Assert.IsTrue(look.IsUsingItemPose);
            Time.timeScale = 0f;
            yield return null;
            double paused = look.CurrentPlayable.GetTime();
            var pausedItemPosition = cake.transform.position;
            var pausedItemRotation = cake.transform.rotation;
            var pausedGripPosition = look.RightGrip.position;
            var pausedGripRotation = look.RightGrip.rotation;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(look.CurrentPlayable.GetTime(), Is.EqualTo(paused).Within(.001));
            Assert.That(Vector3.Distance(cake.transform.position, pausedItemPosition), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(cake.transform.rotation, pausedItemRotation), Is.LessThan(.1f));
            Assert.That(Vector3.Distance(look.RightGrip.position, pausedGripPosition), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(look.RightGrip.rotation, pausedGripRotation), Is.LessThan(.1f));
            Assert.IsTrue(cake && cake.IsUsing);
            Time.timeScale = 1f;
            cake.CancelUse();
            yield return new WaitForSeconds(.2f);
            Assert.IsFalse(look.IsUsingItemPose);
            Assert.AreSame(cake, player.Combat.Weapon);
            Assert.IsFalse(cake.IsSpent);
            Assert.That(probe.ItemGripSeparation, Is.LessThan(.035f));
        }

        [UnityTest]
        public IEnumerator AuthoredFaceMorphsAreAvailableAndRespondToCalmIdle()
        {
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, new ScriptedBinding());
            var appearance = player.GetComponent<PlayerAppearance>();
            var head = ModelVisual.FindNamed(appearance.AvatarModel, "SK_Head").GetComponent<SkinnedMeshRenderer>();
            int blink = -1, brow = -1;
            for (int i = 0; i < head.sharedMesh.blendShapeCount; i++)
            {
                string name = head.sharedMesh.GetBlendShapeName(i);
                if (name.EndsWith("Blink")) blink = i;
                if (name.EndsWith("BrowRelax")) brow = i;
            }
            Assert.That(blink, Is.GreaterThanOrEqualTo(0), "The avatar importer must preserve its authored blink.");
            Assert.That(brow, Is.GreaterThanOrEqualTo(0), "The avatar importer must preserve its authored expression.");
            yield return new WaitForSeconds(.4f);
            Assert.That(head.GetBlendShapeWeight(brow), Is.GreaterThan(20f));
        }
    }

    /// <summary>PlayMode coroutines resume before LateUpdate; sample the actual completed skeletal pose explicitly.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class AppearanceFrameProbe : MonoBehaviour
    {
        PlayerController player;
        PlayerAppearance appearance;
        Transform thigh, itemGrip;
        public bool Sampled { get; private set; }
        public Quaternion ThighRotation { get; private set; }
        public float GripSeparation { get; private set; }
        public float ItemGripSeparation { get; private set; }

        public void Configure(PlayerController owner, PlayerAppearance look, HeldWeapon item)
        {
            player = owner;
            appearance = look;
            thigh = ModelVisual.FindNamed(look.AvatarModel, "thigh_L");
            itemGrip = ModelVisual.FindNamed(item.gameObject, "Grip_R");
            Assert.IsNotNull(thigh);
            Assert.IsNotNull(itemGrip);
        }

        void LateUpdate()
        {
            if (!player || !appearance || !thigh || !itemGrip) return;
            Sampled = true;
            ThighRotation = thigh.localRotation;
            GripSeparation = Vector3.Distance(player.handR.position, appearance.RightGrip.position);
            ItemGripSeparation = Vector3.Distance(itemGrip.position, appearance.RightGrip.position);
        }
    }
}
