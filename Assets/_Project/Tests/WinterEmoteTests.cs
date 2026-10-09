using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    public sealed class WinterEmoteTests
    {
        const string ClipPath = "Assets/_Project/Art/Seasonal/Winter/Animations/WinterShuffle.fbx";
        LobbyStage stage;
        AnimationClip clip;
        float previousTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            yield return TestScenes.Reset();
            var camera = new GameObject("Winter emote test camera", typeof(Camera));
            camera.tag = "MainCamera";
            stage = new GameObject("Winter emote test stage").AddComponent<LobbyStage>();
            stage.Show(null, PlayerAppearance.DefaultPresentationOutfit());
            clip = AssetDatabase.LoadAllAssetsAtPath(ClipPath).OfType<AnimationClip>().Single(c => c.name == "WinterShuffle");
            yield return null;
            Assert.IsTrue(stage.IsPresenting);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.Reset();
            Time.timeScale = previousTimeScale;
        }

        static PlayableGraph Graph(LobbyStage value) => (PlayableGraph)typeof(LobbyStage)
            .GetField("graph", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);

        [UnityTest]
        public IEnumerator ImportedEmoteMovesHandsAndKneesWithGroundedFeetWhileGameTimeIsPaused()
        {
            yield return new WaitForSecondsRealtime(.15f);
            var probe = stage.Avatar.gameObject.AddComponent<WinterEmotePoseProbe>();
            probe.Configure(stage.Avatar);
            yield return null;
            Vector3 origin = stage.Avatar.position;
            Quaternion orientation = stage.Avatar.rotation;
            float idleHand = probe.HandHeight;
            var animator = stage.Avatar.GetComponentInChildren<Animator>();
            Assert.IsFalse(animator.applyRootMotion);
            Time.timeScale = 0f;
            Assert.IsTrue(stage.PlayEmote(clip));
            probe.Measuring = true;
            yield return TestScenes.WaitUntil(() => !stage.IsEmoting, 4f, "unscaled WinterShuffle completion");
            probe.Measuring = false;
            Assert.Greater(probe.Frames, 10);
            Assert.Greater(probe.HighestHand, idleHand + .12f, "Native curves raise the existing avatar's mittens.");
            Assert.Greater(probe.KneeMotion, 8f, "The side step bends the native rig, not just the stage pivot.");
            Assert.Greater(probe.FootLift, .015f);
            Assert.Less(probe.SupportFloorError, .008f, "At least one boot remains grounded through each step.");
            Assert.Less(probe.RootDrift, .0005f);
            Assert.That(Vector3.Distance(origin, stage.Avatar.position), Is.LessThan(.0005f));
            Assert.That(Quaternion.Angle(orientation, stage.Avatar.rotation), Is.LessThan(.01f));
            yield return new WaitForSecondsRealtime(.2f);
            Assert.Less(Mathf.Abs(probe.HandHeight - idleHand), .04f, "Completion restores the original Idle pose.");
            Assert.AreEqual(2, Graph(stage).GetPlayableCount(), "Only the original Idle and mixer remain.");
        }

        [UnityTest]
        public IEnumerator ReplayingAndDressReleaseDisableDisposeTheOneShotWithoutAccumulatingPlayables()
        {
            Assert.AreEqual(2, Graph(stage).GetPlayableCount());
            for (int i = 0; i < 10; i++)
            {
                Assert.IsTrue(stage.PlayEmote(clip));
                Assert.AreEqual(3, Graph(stage).GetPlayableCount());
                yield return null;
            }
            Assert.IsFalse(stage.PlayEmote(null));
            Assert.IsTrue(stage.IsEmoting, "Invalid preview requests do not interrupt a valid dance.");
            stage.Dress(PlayerAppearance.DefaultPresentationOutfit());
            Assert.IsFalse(stage.IsEmoting);
            Assert.AreEqual(2, Graph(stage).GetPlayableCount());
            Assert.IsTrue(stage.PlayEmote(clip));
            stage.Release();
            Assert.IsFalse(stage.IsPresenting);
            Assert.IsFalse(stage.IsEmoting);
            Assert.IsFalse(stage.PlayEmote(clip));
            stage.TakeCamera();
            Assert.IsTrue(stage.PlayEmote(clip));
            stage.enabled = false;
            Assert.IsFalse(stage.IsPresenting);
            Assert.IsFalse(stage.IsEmoting);
            Assert.AreEqual(2, Graph(stage).GetPlayableCount());
            var graph = Graph(stage);
            Object.Destroy(stage.gameObject);
            yield return null;
            Assert.IsFalse(graph.IsValid(), "Scene exit disposes the entire avatar graph.");
        }
    }

    [DefaultExecutionOrder(10000)]
    public sealed class WinterEmotePoseProbe : MonoBehaviour
    {
        Transform avatar, root, hand, shin, leftFoot, rightFoot;
        SkinnedMeshRenderer boots;
        Mesh baked;
        readonly List<Vector3> vertices = new();
        Vector3 rootOrigin;
        Quaternion kneeOrigin;
        float floor, restingFoot;
        public bool Measuring;
        public int Frames;
        public float HandHeight, HighestHand, KneeMotion, FootLift, SupportFloorError, RootDrift;

        public void Configure(Transform model)
        {
            avatar = model;
            Transform Bone(string name) => model.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
            root = Bone("root"); hand = Bone("hand_R"); shin = Bone("shin_L");
            leftFoot = Bone("foot_L"); rightFoot = Bone("foot_R");
            boots = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "SK_Boots");
            baked = new Mesh();
            rootOrigin = root.position; kneeOrigin = shin.localRotation;
            restingFoot = Mathf.Min(leftFoot.position.y, rightFoot.position.y);
            floor = Sole();
        }

        float Sole()
        {
            boots.BakeMesh(baked);
            baked.GetVertices(vertices);
            float lowest = float.PositiveInfinity;
            foreach (var vertex in vertices) lowest = Mathf.Min(lowest, boots.transform.TransformPoint(vertex).y);
            return lowest;
        }

        void LateUpdate()
        {
            if (!avatar) return;
            HandHeight = avatar.InverseTransformPoint(hand.position).y;
            if (!Measuring) return;
            Frames++;
            HighestHand = Mathf.Max(HighestHand, HandHeight);
            KneeMotion = Mathf.Max(KneeMotion, Quaternion.Angle(kneeOrigin, shin.localRotation));
            FootLift = Mathf.Max(FootLift, Mathf.Max(leftFoot.position.y, rightFoot.position.y) - restingFoot);
            SupportFloorError = Mathf.Max(SupportFloorError, Mathf.Abs(Sole() - floor));
            RootDrift = Mathf.Max(RootDrift, Vector3.Distance(rootOrigin, root.position));
        }

        void OnDestroy() { if (baked) Object.Destroy(baked); }
    }
}
