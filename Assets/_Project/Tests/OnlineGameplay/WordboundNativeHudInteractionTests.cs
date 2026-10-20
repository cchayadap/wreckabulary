using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.OnlineGameplayTests
{
    public sealed partial class WordboundNativeGameplayTests
    {
        [UnityTest, Timeout(600000), Category("ExternalPhotonGameplay")]
        public IEnumerator HostAndRemoteHudChooseVisibleLetterAcrossActualWallOcclusion() =>
            Run(2, false, hudInteractions: true);

        sealed class HudIdleBinding : InputBinding
        {
            public override string Id => "native-hud-observation";
            public override void Read(ref PlayerCommands commands) { }
            public override bool JoinPressed() => false;
            public override bool StartPressed() => false;
        }

        static CourseDocument HudCourse()
        {
            var document = CourseTemplates.FreestyleWorld();
            document.Id = "native-hud-proof";
            document.Title = "Native HUD interaction proof";
            document.Zones.Clear();
            document.Supplies.Clear();
            for (int lane = 0; lane < 2; lane++)
            {
                int z = lane == 0 ? -5000 : 5000;
                document.Supplies.Add(new CourseSupply {
                    Id = "HudHidden" + lane, ModuleId = "Garden", Position = new CoursePoint(10600, 0, z),
                    Letters = "B", Renewable = false, Protected = false
                });
                document.Supplies.Add(new CourseSupply {
                    Id = "HudVisible" + lane, ModuleId = "Start", Position = new CoursePoint(8000, 0, z + 2200),
                    Letters = "A", Renewable = false, Protected = false
                });
            }
            return document;
        }

        IEnumerator HudInteractions(Peer host)
        {
            Assert.AreEqual(2, peers.Count);
            Assert.IsNull(peers.Single(p => p != host).Controller.PhysicsWorld,
                "The remote HUD must work without creating an authoritative physics world.");
            for (int lane = 0; lane < peers.Count; lane++)
            {
                var actor = peers[lane];
                float z = lane == 0 ? -5 : 5;
                yield return WalkTo(host, actor, () => new WorldVector(8.8f, 0, z), .08f,
                    "Walk to the inside of the real shared room wall");
                string hiddenSource = live.Course.Supplies.Single(s => s.SourceId == "HudHidden" + lane).Id;
                string visibleSource = live.Course.Supplies.Single(s => s.SourceId == "HudVisible" + lane).Id;
                string hiddenId = host.Controller.Frame.Bodies.Single(b => b.Kind == WorldBodyKind.Tile && b.SourceId == hiddenSource).Id;
                string visibleId = host.Controller.Frame.Bodies.Single(b => b.Kind == WorldBodyKind.Tile && b.SourceId == visibleSource).Id;
                yield return Until(() => actor.Controller.PresentedBodies.Count() == actor.Controller.Frame.Bodies.Count,
                    "Expose the exact bound Runner's replicated presentation bodies", 15);
                var player = Bodies(actor).Single(b => b.ActorId.ToString() == actor.Actor);
                var hidden = Bodies(actor).Single(b => b.BodyId.ToString() == hiddenId);
                var visible = Bodies(actor).Single(b => b.BodyId.ToString() == visibleId);
                var playerPose = PlayerPose(host, actor);
                Assert.Less(Pose(host, hiddenId).Position.DistanceSquared(playerPose.Position),
                    Pose(host, visibleId).Position.DistanceSquared(playerPose.Position),
                    "The occluded letter must be closer so a distance-only selector would choose incorrectly.");
                Assert.IsFalse(host.Controller.PhysicsWorld.HasLineOfSight(actor.Actor, playerPose.Position, Pose(host, hiddenId).Position));
                Assert.IsTrue(host.Controller.PhysicsWorld.HasLineOfSight(actor.Actor, playerPose.Position, Pose(host, visibleId).Position));

                var ui = new GameObject(Prefix + "HUD-" + lane);
                ui.transform.SetParent(actor.Controller.transform, false);
                var cameraRoot = new GameObject("Native HUD camera");
                cameraRoot.transform.SetParent(ui.transform, false);
                var camera = cameraRoot.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true; camera.orthographicSize = 4;
                camera.aspect = 16f / 9f;
                camera.transform.position = player.Rigidbody.position + new Vector3(0, 3, -5);
                camera.transform.LookAt((hidden.Rigidbody.position + visible.Rigidbody.position) * .5f + Vector3.up * .35f);
                var view = ui.AddComponent<WordboundPlayerView>();
                view.Initialize(actor.Controller, camera, new HudIdleBinding());
                view.LocalPlayerChanged(player);
                view.enabled = false;
                var hud = ui.AddComponent<WordboundMatchHud>();
                hud.Initialize(actor.Controller, view, live.Items, live.Mode, live.Course, actor.Actor);
                hud.enabled = false;
                try
                {
                    foreach (var candidate in new[] { hidden, visible })
                    {
                        var screen = camera.WorldToViewportPoint(candidate.Rigidbody.position + Vector3.up * .35f);
                        Assert.IsTrue(screen.z > 0 && screen.x >= 0 && screen.x <= 1 && screen.y >= 0 && screen.y <= 1,
                            "Both candidates must be in view; actual wall occlusion must decide the target.");
                    }
                    Assert.AreEqual(visibleId, ((WordboundNetworkBody)InvokeHud(hud, "Target")).BodyId.ToString());
                    long sequence = host.Controller.Frame.Commands.Frontiers[actor.Actor] + 1;
                    var expected = new PlayerCommand(sequence, actor.Controller.Frame.Epoch, WordboundAction.CollectLetter, visibleId);
                    InvokeHud(hud, "ReadCommands", new PlayerCommands { grab = true });
                    yield return Until(() => host.Controller.Frame.Commands.Frontiers[actor.Actor] >= sequence,
                        "Apply the HUD-selected authenticated grab on the real host", 15);
                    var receipt = host.Controller.Frame.Commands.Receipts.Single(r => r.ActorId == actor.Actor && r.Sequence == sequence);
                    Assert.AreEqual(expected.PayloadHash, receipt.PayloadHash, "HUD must submit the visible target, not a test-side replacement command.");
                    Assert.AreEqual("Ok", receipt.Effect.Code);
                    int seat = WordboundGameplayPlan.Seat(host.Controller.Frame.Binding, actor.Actor);
                    yield return CommittedState(host, frame => !frame.Bodies.Any(b => b.Id == visibleId) &&
                        frame.Bodies.Any(b => b.Id == hiddenId && b.Active) &&
                        frame.Economy.Players[seat].Letters.Equals(LetterBag.FromWord("A")),
                        "Commit the HUD-selected letter and leave the wall-occluded letter untouched on both peers");
                }
                finally { UnityEngine.Object.Destroy(ui); }
                yield return null;
            }
            Debug.Log("WORDBOUND_HUD_OBSERVED: actual host and remote HUD chose the farther visible tile over the nearer wall-occluded tile; authenticated Ok receipts and exact replicated letter effects; cleanup pending.");
        }

        static object InvokeHud(WordboundMatchHud hud, string method, params object[] arguments)
        {
            try { return typeof(WordboundMatchHud).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, arguments); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
    }
}
