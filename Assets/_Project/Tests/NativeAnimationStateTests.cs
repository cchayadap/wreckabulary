using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using Wreckabulary.Art;

namespace Wreckabulary.Tests
{
    public class NativeAnimationStateTests
    {
        const string AvatarKey = "Avatar/Avatar";
        Collider testFloor;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = Vector3.down * .5f;
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            testFloor = ground.GetComponent<Collider>();
            new GameObject("Animation test listener").AddComponent<AudioListener>();
            TilePool.Ensure();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SummonedThing.ClearAll();
            World.ClearTransient();
            yield return TestScenes.Reset();
        }

        static PlayerController Spawn(out ScriptedBinding input)
        {
            input = new ScriptedBinding();
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, input);
            player.Respawn(Vector3.zero);
            return player;
        }

        static T Observe<T>(PlayerAppearance appearance, string field) =>
            (T)typeof(PlayerAppearance).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(appearance);

        static bool Playing(PlayerAppearance appearance, string name) =>
            appearance.IsAnimationReady && appearance.CurrentAnimationClip == ModelLibrary.Load().FindClip(AvatarKey, name);

        [UnityTest]
        public IEnumerator PickupKeepsBothSolesAndBatGripAlignedAcrossFullClipAndFirstHold()
        {
            var player = Spawn(out _);
            var appearance = player.GetComponent<PlayerAppearance>();
            yield return TestScenes.WaitUntil(() => player.Grounded && Playing(appearance, "Idle"), 2f, "grounded reference Idle");
            var graph = Observe<PlayableGraph>(appearance, "graph");
            var animator = appearance.AvatarModel.GetComponentInChildren<Animator>(true);
            var originalCulling = animator.cullingMode;
            var originalUpdateMode = graph.GetTimeUpdateMode();
            bool originallyPlaying = graph.IsPlaying();
            var bakedBoots = new Mesh();
            var vertices = new List<Vector3>();
            try
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                appearance.Play("Idle", 1f);
                Observe<AnimationClipPlayable>(appearance, "currentPlayable").SetTime(0);
                graph.Evaluate(0f);
                var renderers = appearance.AvatarModel.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(renderer => renderer.enabled).ToArray();
                Assert.IsNotEmpty(renderers);
                var referenceScales = renderers.Select(renderer => renderer.transform.lossyScale).ToArray();
                var boots = renderers.Single(renderer => renderer.name == "SK_Boots");
                boots.BakeMesh(bakedBoots, false);
                bakedBoots.GetVertices(vertices);
                var referenceVertices = vertices.Select(vertex => boots.transform.TransformPoint(vertex)).ToArray();
                Assert.IsNotEmpty(referenceVertices);
                Assert.IsTrue(referenceVertices.All(point => Finite(point.x) && Finite(point.y) && Finite(point.z)), "finite reference boot vertices");
                var leftFoot = ModelVisual.FindNamed(appearance.AvatarModel, "foot_L");
                var rightFoot = ModelVisual.FindNamed(appearance.AvatarModel, "foot_R");
                Assert.IsNotNull(leftFoot); Assert.IsNotNull(rightFoot);
                Assert.Greater(Vector3.Distance(leftFoot.position, rightFoot.position), .01f);
                var leftVertices = Enumerable.Range(0, referenceVertices.Length).Where(index =>
                    (referenceVertices[index] - leftFoot.position).sqrMagnitude <=
                    (referenceVertices[index] - rightFoot.position).sqrMagnitude).ToArray();
                var rightVertices = Enumerable.Range(0, referenceVertices.Length).Except(leftVertices).ToArray();
                var leftSole = SoleBand(referenceVertices, leftVertices, "left");
                var rightSole = SoleBand(referenceVertices, rightVertices, "right");
                Assert.IsEmpty(leftSole.Intersect(rightSole));
                Assert.IsNotNull(testFloor);
                var copyGrips = typeof(PlayerAppearance).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(copyGrips);
                var bat = CatalogGear.Create(GameConfig.Current.Items.Get("BAT"));
                Assert.IsTrue(player.Combat.TryEquip(bat));
                var body = bat.GetComponent<Rigidbody>();
                var authoredGrip = ModelVisual.FindNamed(bat.gameObject, "Grip_R");
                Assert.IsNotNull(authoredGrip);
                Assert.IsNotNull(appearance.RightGrip);
                var grip = bat.Definition.Grip;
                var catalogueGrip = new Vector3(grip[0], grip[1], grip[2]);
                var pickup = ModelLibrary.Load().FindClip(AvatarKey, "Pickup");
                Assert.IsNotNull(pickup);
                Assert.That(pickup.length, Is.EqualTo(.9f).Within(.001f), "retain the full authored Pickup duration");
                Assert.Greater(pickup.frameRate, 0f);
                Assert.IsTrue(Finite(pickup.frameRate));
                Assert.IsFalse(pickup.isLooping);

                void AssertPose(string clipName, float time)
                {
                    var playable = Observe<AnimationClipPlayable>(appearance, "currentPlayable");
                    Assert.AreEqual(clipName, playable.GetAnimationClip().name);
                    playable.SetTime(time);
                    graph.Evaluate(0f);
                    copyGrips.Invoke(appearance, null);
                    string context = clipName + " time=" + time.ToString("F6");
                    boots.BakeMesh(bakedBoots, false);
                    bakedBoots.GetVertices(vertices);
                    Assert.AreEqual(referenceVertices.Length, vertices.Count, context + " vertex identities remain stable");
                    AssertSoleBand(testFloor, boots, vertices, leftSole, context + " left");
                    AssertSoleBand(testFloor, boots, vertices, rightSole, context + " right");
                    AssertRendererScales(renderers, referenceScales, context);
                    Assert.AreSame(player.handR, bat.transform.parent, context);
                    Assert.AreEqual(RigidbodyInterpolation.None, body.interpolation, context);
                    float authoredGap = Vector3.Distance(authoredGrip.position, appearance.RightGrip.position);
                    float catalogueGap = Vector3.Distance(bat.transform.TransformPoint(catalogueGrip), appearance.RightGrip.position);
                    Assert.IsTrue(Finite(authoredGap) && Finite(catalogueGap), context + " finite BAT grip gaps");
                    Assert.LessOrEqual(authoredGap, .005f,
                        context + " authored BAT grip follows the evaluated native hand");
                    Assert.LessOrEqual(catalogueGap, .005f,
                        context + " catalogue BAT grip follows the evaluated native hand");
                }

                appearance.Play("Pickup", pickup.length + 1f);
                var times = new SortedSet<float> { 0f, .28f - .0001f, .28f, .28f + .0001f, pickup.length };
                int halfFrames = Mathf.CeilToInt(pickup.length * pickup.frameRate * 2f);
                for (int frame = 0; frame <= halfFrames; frame++)
                    times.Add(Mathf.Min(pickup.length, frame / (pickup.frameRate * 2f)));
                foreach (float time in times) AssertPose("Pickup", time);
                appearance.Play("Hold_OneHand", 1f);
                var hold = appearance.CurrentAnimationClip;
                Assert.Greater(hold.frameRate, 0f); Assert.IsTrue(Finite(hold.frameRate));
                foreach (float time in new[] { 0f, 1f / hold.frameRate, .1f, .2f }) AssertPose("Hold_OneHand", time);
            }
            finally
            {
                animator.cullingMode = originalCulling;
                if (graph.IsValid())
                {
                    graph.SetTimeUpdateMode(originalUpdateMode);
                    if (originallyPlaying) graph.Play(); else graph.Stop();
                }
                Object.Destroy(bakedBoots);
            }
            yield return null;
        }

        static int[] SoleBand(Vector3[] referenceVertices, int[] footVertices, string foot)
        {
            Assert.IsNotEmpty(footVertices, foot + " boot geometry attributed to the named foot");
            float minimum = footVertices.Min(index => referenceVertices[index].y);
            var sole = footVertices.Where(index => referenceVertices[index].y <= minimum + .002f).ToArray();
            Assert.Greater(sole.Length, 2, foot + " fixed 2mm sole band contains multiple vertices");
            return sole;
        }

        static void AssertSoleBand(Collider floor, SkinnedMeshRenderer boots, List<Vector3> vertices, int[] sole, string context)
        {
            float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
            int hits = 0;
            Vector3 minimumPoint = Vector3.zero, maximumPoint = Vector3.zero;
            Vector3 minimumFloor = Vector3.zero, maximumFloor = Vector3.zero;
            foreach (int index in sole)
            {
                var point = boots.transform.TransformPoint(vertices[index]);
                Assert.IsTrue(Finite(point.x) && Finite(point.y) && Finite(point.z), context + " finite sole vertex " + index);
                Assert.IsTrue(floor.Raycast(new Ray(point + Vector3.up * 2f, Vector3.down), out var hit, 4f),
                    context + " actual floor=" + floor.name + " hit for vertex " + index + " at " + point.ToString("F6"));
                hits++;
                float gap = point.y - hit.point.y;
                if (gap < minimum) { minimum = gap; minimumPoint = point; minimumFloor = hit.point; }
                if (gap > maximum) { maximum = gap; maximumPoint = point; maximumFloor = hit.point; }
            }
            string detail = context + " count=" + sole.Length + " hits=" + hits + "/" + sole.Length + " floor=" + floor.name
                + " minGap=" + minimum.ToString("F6") + " point=" + minimumPoint.ToString("F6") + " floorPoint=" + minimumFloor.ToString("F6")
                + " maxGap=" + maximum.ToString("F6") + " point=" + maximumPoint.ToString("F6") + " floorPoint=" + maximumFloor.ToString("F6");
            Debug.Log("PICKUP_SOLE " + detail);
            Assert.That(minimum, Is.InRange(-.005f, .005f), detail);
            Assert.That(maximum, Is.InRange(-.005f, .005f), detail);
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static void AssertRendererScales(SkinnedMeshRenderer[] renderers, Vector3[] reference, string context)
        {
            for (int index = 0; index < renderers.Length; index++)
            {
                Assert.IsTrue(renderers[index].enabled, context + " renderer remains enabled: " + renderers[index].name);
                var current = renderers[index].transform.lossyScale;
                for (int axis = 0; axis < 3; axis++)
                {
                    float baseline = reference[index][axis];
                    Assert.IsFalse(float.IsNaN(baseline) || float.IsInfinity(baseline));
                    Assert.Greater(Mathf.Abs(baseline), .000001f, context + " nonzero reference scale");
                    float ratio = current[axis] / baseline;
                    Assert.IsFalse(float.IsNaN(ratio) || float.IsInfinity(ratio));
                    Assert.That(ratio, Is.InRange(.5f, 1.5f), context + " lossyScale ratio " + renderers[index].name + " axis=" + axis);
                }
            }
        }

        [UnityTest]
        public IEnumerator ReadyGraphPlaysAllFifteenExistingRequestsAndMovesTheImportedBones()
        {
            var player = Spawn(out _);
            var appearance = player.GetComponent<PlayerAppearance>();
            Assert.IsTrue(Playing(appearance, "Idle"), "successful initialization starts the actual Idle clip");
            var graph = Observe<PlayableGraph>(appearance, "graph");
            Assert.IsTrue(graph.IsValid());
            Assert.IsTrue(graph.IsPlaying());
            Assert.AreEqual(DirectorUpdateMode.GameTime, graph.GetTimeUpdateMode());
            var bones = appearance.AvatarModel.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .SelectMany(mesh => mesh.bones).Distinct().ToArray();
            Assert.AreEqual(22, bones.Length);
            var animator = appearance.AvatarModel.GetComponentInChildren<Animator>(true);
            var originalCulling = animator.cullingMode;
            try
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (string name in new[] { "Idle", "Walk_InPlace", "Run_InPlace", "Jump_Preview",
                    "Hold_OneHand", "Carry_TwoHand", "Block_Plate", "Swing_OneHand", "Thrust_OneHand",
                    "Throw_OneHand", "Hit_Reaction", "Celebrate", "Drink_Consumable", "Pickup", "Place" })
                {
                    appearance.Play(name, 1f);
                    Assert.IsTrue(Playing(appearance, name), name + " resolves the imported clip");
                    Assert.IsTrue(graph.IsPlaying(), name + " keeps the production graph playing");
                    Assert.AreEqual(DirectorUpdateMode.GameTime, graph.GetTimeUpdateMode());
                    graph.Evaluate(0f);
                    var positions = bones.Select(bone => bone.localPosition).ToArray();
                    var rotations = bones.Select(bone => bone.localRotation).ToArray();
                    graph.Evaluate(.2f);
                    var playable = Observe<AnimationClipPlayable>(appearance, "currentPlayable");
                    Assert.Greater(playable.GetTime(), .1d, name + " advances through the connected graph");
                    if (name == "Walk_InPlace" || name == "Run_InPlace")
                        Assert.IsTrue(bones.Where((bone, i) => (bone.localPosition - positions[i]).sqrMagnitude > .00000001f
                            || Quaternion.Angle(bone.localRotation, rotations[i]) > .01f).Any(),
                            name + " changes local skeletal poses, independently of whole-body wobble");
                }
            }
            finally
            {
                animator.cullingMode = originalCulling;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExistingMovementSelectorAndActionLockChooseTheirBoundClips()
        {
            var player = Spawn(out var input);
            var appearance = player.GetComponent<PlayerAppearance>();
            yield return TestScenes.WaitUntil(() => player.Grounded && Playing(appearance, "Idle"), 2f, "grounded Idle");
            player.MoveScale = .2f;
            input.Next.move = Vector2.right;
            yield return TestScenes.WaitUntil(() => Playing(appearance, "Walk_InPlace"), 2f, "walking clip");
            player.MoveScale = 1f;
            yield return TestScenes.WaitUntil(() => Playing(appearance, "Run_InPlace"), 2f, "running clip");
            appearance.Play("Celebrate", .3f);
            yield return new WaitForSeconds(.1f);
            Assert.IsTrue(Playing(appearance, "Celebrate"), "the existing action lock overrides locomotion");
            yield return TestScenes.WaitUntil(() => Playing(appearance, "Run_InPlace"), 2f, "locomotion after action lock expires");
            input.Next.move = Vector2.zero;
            yield return TestScenes.WaitUntil(() => Playing(appearance, "Idle"), 2f, "stopped Idle");
            input.Next.jump = true;
            yield return TestScenes.WaitUntil(() => Playing(appearance, "Jump_Preview"), 2f, "jump event clip");
        }

        [UnityTest]
        public IEnumerator MissingRequiredBindingKeepsLegacyVisualsAndCanRetry()
        {
            var library = ModelLibrary.Load();
            var original = library.Entries.ToArray();
            PlayerController player = null;
            PlayerAppearance appearance = null;
            try
            {
                var incomplete = original.Select(entry =>
                {
                    if (entry.key == AvatarKey)
                        entry.clips = entry.clips.Where(clip => clip != library.FindClip(AvatarKey, "Idle")).ToArray();
                    return entry;
                }).ToArray();
                library.Set(incomplete);
                player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
                appearance = player.GetComponent<PlayerAppearance>() ?? player.gameObject.AddComponent<PlayerAppearance>();
                var legacy = player.visual.GetComponentsInChildren<Renderer>(true);
                var enabled = legacy.Select(renderer => renderer.enabled).ToArray();
                Assert.IsTrue(enabled.Any(value => value), "the fallback has visible supplied renderers");
                Assert.IsFalse(appearance.Initialize(player));
                Assert.IsFalse(appearance.IsAnimationReady);
                Assert.IsNull(appearance.AvatarModel);
                CollectionAssert.AreEqual(enabled, legacy.Select(renderer => renderer.enabled), "failure leaves fallback visibility intact");
            }
            finally
            {
                library.Set(original);
            }
            Assert.IsTrue(appearance.Initialize(player), "a valid binding set allows retry");
            Assert.IsTrue(Playing(appearance, "Idle"));
            yield return null;
        }
    }
}
