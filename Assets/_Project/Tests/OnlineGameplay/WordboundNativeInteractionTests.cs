using System;
using System.Collections;
using System.Linq;
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
        [UnityTest, Category("ExternalPhotonGameplay"), Timeout(600000)]
        public IEnumerator AuthenticatedPeersJumpCarryCraftEquipAndStrikeProps() => Run(2, false, true);

        IEnumerator Interactions(Peer host)
        {
            Assert.IsFalse(live.Mode.EnemyDamage, "This fixture proves prop melee in Freestyle.");
            for (int lane = 0; lane < peers.Count; lane++)
            {
                var actor = peers[lane];
                float x = lane == 0 ? -4 : 4;
                yield return JumpAndLand(host, actor);
                string carryId = PlacementBody(host, "Carry" + lane);
                yield return WalkTo(host, actor, () => Pose(host, carryId).Position, 1.1f, "Walk to carry stool");
                var stool = Item(host, carryId);
                yield return Command(host, actor, WordboundAction.PickUp, carryId, stool.Revision);
                int seat = WordboundGameplayPlan.Seat(host.Controller.Frame.Binding, actor.Actor);
                yield return CommittedState(host, frame => frame.Economy.Players[seat].CarriedFurniture == stool.Id &&
                    frame.Economy.Items.Single(i => i.Id == stool.Id).State == ItemState.Carried &&
                    !frame.Bodies.Single(b => b.Id == carryId).Active, "Replicate carried furniture state");
                yield return Until(() => peers.All(p => Bodies(p).Single(b => b.BodyId.ToString() == carryId)
                    .GetComponent<WordboundGameplayBody>().CarrierSeat == seat), "Replicate native carrier seat", 15);
                var beforeCarryWalk = PlayerPose(host, actor).Position;
                yield return WalkTo(host, actor, () => new WorldVector(x + (lane == 0 ? -2 : 2), 0, -4), .15f, "Walk while carrying");
                Assert.Greater(PlayerPose(host, actor).Position.DistanceSquared(beforeCarryWalk), .5f);
                Assert.AreEqual(stool.Id, Inventory(host, actor).CarriedFurniture);
                yield return Command(host, actor, WordboundAction.Drop);
                yield return CommittedState(host, frame => frame.Economy.Players[seat].CarriedFurniture == -1 &&
                    frame.Economy.Items.Single(i => i.Id == stool.Id).State == ItemState.World &&
                    frame.Bodies.Single(b => b.Id == carryId).Active, "Commit validated furniture release");

                string supply = live.Course.Supplies.Single(s => s.SourceId == "BatLetters" + lane).Id;
                var tiles = host.Controller.Frame.Bodies.Where(b => b.Kind == WorldBodyKind.Tile && b.SourceId == supply)
                    .Select(b => b.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
                Assert.AreEqual(3, tiles.Length);
                foreach (string tile in tiles)
                {
                    yield return WalkTo(host, actor, () => Pose(host, tile).Position, 1.1f, "Walk to physical letter");
                    yield return Command(host, actor, WordboundAction.CollectLetter, tile);
                }
                Assert.IsTrue(Inventory(host, actor).Letters.Equals(LetterBag.FromWord("BAT")));
                yield return Command(host, actor, WordboundAction.BeginCraft, value: "BAT");
                yield return Until(() => host.Controller.Frame.Economy.Items.Any(i => i.Word == "BAT" &&
                    i.CrafterId == seat && i.Origin == ItemOrigin.Crafted && i.State == ItemState.Held),
                    "Complete the real craft timer", 20);
                var bat = host.Controller.Frame.Economy.Items.Single(i => i.Word == "BAT" && i.CrafterId == seat);
                int slot = Array.IndexOf(Inventory(host, actor).Slots, bat.Id);
                Assert.GreaterOrEqual(slot, 0);
                Assert.IsTrue(Inventory(host, actor).Letters.IsEmpty);
                Assert.IsTrue(Inventory(host, actor).Reserved.IsEmpty);
                yield return Command(host, actor, WordboundAction.Equip, slot: slot);
                yield return CommittedState(host, frame => frame.Actors.Single(a => a.PlayerId == actor.Actor).EquippedSlot == slot &&
                    WordboundCombatPolicy.EquippedItemId(frame, actor.Actor) == "item:" + bat.Id,
                    "Commit selected equipment and crafted body");

                string targetId = PlacementBody(host, "Target" + lane);
                yield return WalkTo(host, actor, () => Pose(host, targetId).Position,
                    1.1f, "Approach and face the real melee target");
                float priorDurability = Item(host, targetId).Durability;
                float priorWeaponDurability = Item(host, "item:" + bat.Id).Durability;
                yield return Command(host, actor, WordboundAction.Attack);
                yield return Until(() => Item(host, targetId).Durability < priorDurability,
                    "Resolve host-observed melee geometry and prop damage", 10);
                float remaining = Item(host, targetId).Durability;
                Assert.AreEqual(priorDurability - live.Items.Get("BAT").Melee.BreakPower, remaining);
                Assert.AreEqual(priorWeaponDurability - 1, Item(host, "item:" + bat.Id).Durability);
                yield return CommittedState(host, frame => frame.Economy.Items.Single(i => "item:" + i.Id == targetId).Durability == remaining &&
                    frame.Economy.Items.Single(i => i.Id == bat.Id).Durability == priorWeaponDurability - 1,
                    "Commit melee effect and weapon wear to both peers");
                yield return Until(() => peers.All(ExactBodies), "Reconcile the crafted body set on every peer", 15);
                AssertBodies(host, true);
                foreach (var client in peers.Where(p => p != host))
                {
                    Assert.AreEqual(0, client.Advanced);
                    Assert.AreEqual(0, client.Controller.Authority.Match.Tick);
                    AssertBodies(client, false);
                }
            }
            Debug.Log("WORDBOUND_GAMEPLAY_OBSERVED: both admitted peers jumped, carried, crafted, equipped and struck native props; cleanup pending.");
        }

        IEnumerator JumpAndLand(Peer host, Peer actor)
        {
            yield return Until(() => Math.Abs(PlayerPose(host, actor).Velocity.Y) < .1f,
                "Settle on native floor before jump", 10);
            float floor = PlayerPose(host, actor).Position.Y;
            actor.InteractionInput = new WordboundMovementInput { Jump = true };
            yield return Until(() => PlayerPose(host, actor).Position.Y > floor + .3f,
                "Apply admitted jump input in host physics", 8);
            actor.InteractionInput = default;
            yield return Until(() => PlayerPose(host, actor).Position.Y < floor + .12f &&
                Math.Abs(PlayerPose(host, actor).Velocity.Y) < .1f, "Land through native collision", 10);
        }
        IEnumerator WalkTo(Peer host, Peer actor, Func<WorldVector> target, float distance, string operation)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 25;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                Healthy();
                var at = PlayerPose(host, actor).Position;
                var goal = target();
                Vector2 delta = new Vector2(goal.X - at.X, goal.Z - at.Z);
                if (delta.magnitude <= distance) break;
                float speed = Mathf.Clamp((delta.magnitude - distance) * .7f, .08f, .65f);
                var input = delta.normalized * speed;
                actor.InteractionInput = new WordboundMovementInput { MoveX = input.x, MoveZ = input.y };
                yield return null;
            }
            actor.InteractionInput = default;
            var final = PlayerPose(host, actor).Position;
            var destination = target();
            Assert.LessOrEqual(new Vector2(destination.X - final.X, destination.Z - final.Z).magnitude, distance,
                operation + " did not reach its target through normal movement.");
            yield return Until(() => {
                var velocity = PlayerPose(host, actor).Velocity;
                return velocity.X * velocity.X + velocity.Z * velocity.Z < .01f;
            }, "Stop through ordinary friction", 5);
        }
        IEnumerator Command(Peer host, Peer actor, WordboundAction action, string target = "", int revision = 0, int slot = -1, string value = "")
        {
            var command = actor.Controller.Submit(action, target, revision, slot, value);
            yield return Until(() => host.Controller.Frame.Commands.Frontiers[actor.Actor] >= command.Sequence,
                "Apply admitted " + action, 15);
            var receipt = host.Controller.Frame.Commands.Receipts.Single(r => r.ActorId == actor.Actor && r.Sequence == command.Sequence);
            Assert.AreEqual(command.PayloadHash, receipt.PayloadHash);
            Assert.AreEqual("Ok", receipt.Effect.Code, action + " must succeed through real authority validation.");
        }
        IEnumerator CommittedState(Peer host, Func<MatchSnapshot, bool> state, string operation)
        {
            long tick = host.Controller.Frame.Progress.Tick;
            yield return Until(() => state(host.Controller.Frame) && host.Session.Committed != null &&
                host.Session.Committed.Tick >= tick && peers.Where(p => p != host).All(p => state(p.Controller.Frame) &&
                    p.Received.Values.Any(mark => mark.Tick >= tick && host.Acknowledged.TryGetValue(Key(p.Actor, mark.Sequence), out var ack) &&
                        ack.Same(mark))), operation, 25);
        }
        static WorldBodyState Pose(Peer host, string id) => host.Controller.Frame.Bodies.Single(b => b.Id == id);
        static WorldBodyState PlayerPose(Peer host, Peer actor) => host.Controller.Frame.Bodies.Single(b =>
            b.Kind == WorldBodyKind.Player && b.SourceId == actor.Actor);
        static PlayerInventory Inventory(Peer host, Peer actor) => host.Controller.Frame.Economy.Players[
            WordboundGameplayPlan.Seat(host.Controller.Frame.Binding, actor.Actor)];
        static ItemInstance Item(Peer host, string bodyId) => host.Controller.Frame.Economy.Items.Single(i => "item:" + i.Id == bodyId);
        string PlacementBody(Peer host, string zone)
        {
            string source = live.Course.Placements.Single(p => p.ZoneId == zone).Id;
            return host.Controller.Frame.Bodies.Single(b => b.Kind == WorldBodyKind.Item && b.SourceId == source).Id;
        }
    }
}
