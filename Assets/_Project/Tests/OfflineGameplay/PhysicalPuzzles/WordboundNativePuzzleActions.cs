using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.OfflineGameplayTests
{
    public sealed partial class WordboundNativePuzzleFlow
    {
        IEnumerator ReachObjective(ResolvedCoursePhysicalSite site)
        {
            var route = content.Course.Laps[0];
            int target = route.Checkpoints.ToList().FindIndex(c => c.Id == site.CheckpointId);
            Assert.GreaterOrEqual(target, Team.NextCheckpoint);
            while (Team.NextCheckpoint < target)
            {
                var checkpoint = route.Checkpoints[Team.NextCheckpoint];
                Assert.IsTrue(checkpoint.PuzzleIds.All(Team.CompletedPuzzles.Contains),
                    "Fixture room positioning cannot skip an unsolved objective.");
                var anchor = content.Course.RecoveryAnchors.Single(r => r.Id == checkpoint.RecoveryAnchorId);
                int before = Team.NextCheckpoint;
                yield return SetPlayer(Metres(anchor.Position) + Vector3.up * .03f, 0);
                yield return Until(() => Team.NextCheckpoint > before, "Observe real checkpoint support/volume", 8);
            }
            Assert.AreEqual(site.CheckpointId, route.Checkpoints[Team.NextCheckpoint].Id);
            Assert.AreEqual(MatchPhysicalStage.Waiting, Mechanism(site).Physical.Stage);
        }

        IEnumerator CompleteSite(ResolvedCoursePhysicalSite site)
        {
            var puzzle = content.Course.Puzzles.Single(p => p.Id == site.PuzzleId);
            string windReceiver = "";
            if (puzzle.PresetId == "CollectLetters")
            {
                yield return CollectSupply(site.ToolRecoverySupplyId);
                yield return Craft(site.ToolRecipeId);
                int toolSlot = Array.IndexOf(Inventory.Slots, Item(lastCrafted).Id);
                yield return Command(WordboundAction.Equip, slot: toolSlot);
                string marked = PlacementBody(site.PropSourceZoneId);
                Assert.AreNotEqual(ItemState.Destroyed, Item(marked).State);
                Assert.AreEqual(MatchPhysicalStage.Waiting, Mechanism(site).Physical.Stage);
                yield return SmashMarkedProp(marked);
                Assert.AreEqual(ItemState.Destroyed, Item(marked).State);
                var fragments = Frame.Bodies.Where(b => b.Kind == WorldBodyKind.Tile &&
                    b.SourceId == Body(marked).SourceId).Select(b => b.Id).ToArray();
                CollectionAssert.AreEquivalent(puzzle.RequiredLetters.ToCharArray(), fragments.Select(id =>
                    Frame.Economy.Tiles.Single(t => "tile:" + t.Key == id).Value));
                foreach (string fragment in fragments) yield return Collect(fragment);
            }
            else yield return CollectSupply(site.RecoverySupplyId);
            yield return Craft(puzzle.RecipeId);
            string recipe = lastCrafted;
            if (puzzle.PresetId == "RedirectWind")
            {
                windReceiver = PlacementBody(site.WindReceiverZoneId);
                yield return SetBody(windReceiver, Metres(site.Position) + Vector3.right * 3 + Vector3.up * .03f, site.Yaw);
            }
            if (puzzle.PresetId == "PlaceObject" || puzzle.PresetId == "BalanceWeight")
            {
                yield return Deploy(recipe, site, Vector3.right);
                yield return Command(WordboundAction.UsePuzzle, site.PuzzleId, value: recipe, expected: "MechanismIncomplete");
                Assert.AreEqual(MatchPhysicalStage.Waiting, Mechanism(site).Physical.Stage);
                yield return Command(WordboundAction.PickUp, recipe, Item(recipe).Revision);
            }
            yield return Deploy(recipe, site, Vector3.zero);
            if (puzzle.PresetId == "RedirectWind")
            {
                yield return Command(WordboundAction.UsePuzzle, site.PuzzleId, value: recipe, expected: "MechanismIncomplete");
                var placement = content.Course.Placements.Single(p => p.ZoneId == site.WindReceiverZoneId && p.TeamIndex == 0);
                var start = Metres(placement.Position) + Vector3.back * .15f + Vector3.up * .02f;
                yield return SetBody(windReceiver, start, placement.Yaw);
                yield return Until(() => Mechanism(site).Physical.Stage == MatchPhysicalStage.Complete,
                    "FAN physically transports the authored receiver into its arrival volume", 12);
                Assert.AreEqual(windReceiver, Mechanism(site).Physical.AuxiliaryBodyId);
                Assert.Greater(Body(windReceiver).Position.DistanceSquared(new WorldVector(start.x, start.y, start.z)), .16f);
                Assert.IsTrue(observedStages.Contains(site.PuzzleId + "/" + MatchPhysicalStage.Armed));
            }
            else if (puzzle.PresetId == "TimedMechanism")
            {
                Assert.AreEqual(Actor, Team.KeepsakeCarrier);
                yield return Command(WordboundAction.UsePuzzle, site.PuzzleId, value: recipe, expected: "MechanismIncomplete");
                yield return Until(() => slowedTicks.TryGetValue(site.PuzzleId, out int count) && count >= 3,
                    "CLOCK changes the real sorting arm's canonical motion rate", 5);
                var fixture = site.Fixtures.Single();
                var arm = controller.Bodies[CourseFixtureMotion.BodyId(fixture.Id, 0)];
                Assert.IsTrue(arm.Rigidbody.isKinematic);
                Assert.IsTrue(arm.GetComponent<BoxCollider>().enabled);
                Assert.Less((arm.Rigidbody.position - Vector(CourseFixtureMotion.Position(fixture,
                    Mechanism(site).Physical.MotionUnits))).sqrMagnitude, .000001f);
                yield return MoveIntoArrival(site, "Cross slowed native sorting arm with the keepsake", 8);
                Assert.AreEqual("keepsake:0", Mechanism(site).Physical.AuxiliaryBodyId);
            }
            else if (puzzle.PresetId == "BuildRoute")
            {
                yield return Command(WordboundAction.UsePuzzle, site.PuzzleId, value: recipe, expected: "MechanismIncomplete");
                var pad = controller.Bodies[recipe];
                float height = pad.GetComponent<BoxCollider>().size.y;
                float launchStrength = content.Items.Get(puzzle.RecipeId).Deploy.Strength;
                yield return SetPlayer(pad.Rigidbody.position + Vector3.up * (height + .02f), site.Yaw);
                yield return Until(() => Player.Velocity.Y > launchStrength * .6f &&
                    Mechanism(site).Physical.Stage != MatchPhysicalStage.Waiting,
                    "Catalogue BED pad applies a real upward launch", 5);
                yield return MoveIntoArrival(site, "Move the launched actor into the raised arrival volume", 8);
                Assert.IsTrue(observedStages.Contains(site.PuzzleId + "/" + MatchPhysicalStage.Armed));
            }
            else
                yield return Until(() => Mechanism(site).Physical.Stage == MatchPhysicalStage.Complete,
                    puzzle.PresetId + " accumulates actual host-observed support/coverage", 8);
            var completed = Mechanism(site);
            Assert.AreEqual(MatchPhysicalStage.Complete, completed.Physical.Stage);
            Assert.AreEqual(site.Hash, completed.Physical.SiteHash);
            Assert.AreEqual(Actor, completed.Physical.ActorId);
            Assert.AreEqual(recipe, completed.ItemEntityId);
            Assert.IsTrue(observedStages.Contains(site.PuzzleId + "/" + MatchPhysicalStage.Complete));
            if (puzzle.PresetId == "BalanceWeight")
            {
                Assert.AreEqual(2, site.FloorPadBounds.Count);
                Assert.GreaterOrEqual(completed.ProgressTicks, site.HoldTicks);
                var collider = controller.Bodies[recipe].GetComponent<BoxCollider>();
                foreach (var pad in site.FloorPadBounds)
                    foreach (float x in new[] { pad.Min.X / 1000f, pad.Max.X / 1000f })
                        foreach (float z in new[] { pad.Min.Z / 1000f, pad.Max.Z / 1000f })
                        {
                            var point = new Vector3(x, pad.Max.Y / 1000f, z);
                            Assert.LessOrEqual((collider.ClosestPoint(point) - point).sqrMagnitude, .0001f);
                        }
            }
            if (puzzle.PresetId == "BuildRoute")
                yield return SetPlayer(Metres(site.Position) + Vector3.back * 1.5f + Vector3.up * .02f, site.Yaw);
            yield return Until(() => controller.PhysicsWorld.HasLineOfSight(Actor, Player.Position, Body(recipe).Position),
                "Reach the completed physical item through unobstructed native geometry", 5);
            yield return Command(WordboundAction.UsePuzzle, site.PuzzleId, value: recipe);
            Assert.IsTrue(Mechanism(site).Solved);
            Assert.IsTrue(Team.CompletedPuzzles.Contains(site.PuzzleId));
            Assert.AreEqual(ItemState.Destroyed, Item(recipe).State);
            Debug.Log("WORDBOUND_PHYSICAL_FAMILY: " + puzzle.PresetId + " " + site.PuzzleId +
                " completed at tick " + Frame.Progress.Tick + " with site hash " + site.Hash);
        }

        IEnumerator SmashMarkedProp(string bodyId)
        {
            var definition = content.Items.Get("HAMMER");
            int strikes = (int)Math.Ceiling(Item(bodyId).Durability / definition.Melee.BreakPower) + 2;
            for (int attempt = 0; attempt < strikes && Item(bodyId).State != ItemState.Destroyed; attempt++)
            {
                yield return Until(() => Frame.Progress.Tick >= Frame.Actors.Single(a => a.PlayerId == Actor).NextAttackTick,
                    "Respect the real HAMMER cooldown", 4);
                var at = Vector(Body(bodyId).Position);
                yield return SetPlayer(at + Vector3.back * 1.05f, 0);
                float previous = Item(bodyId).Durability;
                yield return Command(WordboundAction.Attack);
                yield return Until(() => Item(bodyId).Durability < previous, "Strike the marked native prop", 4);
                Assert.AreEqual(Math.Max(0, previous - definition.Melee.BreakPower), Item(bodyId).Durability, .001f);
            }
            Assert.AreEqual(ItemState.Destroyed, Item(bodyId).State, "Only actual melee may destroy the marked prop.");
        }

        IEnumerator CollectSupply(string sourceId)
        {
            var source = content.Course.Supplies.Single(s => s.SourceId == sourceId && s.TeamIndex == 0);
            yield return Until(() => Frame.Bodies.Count(b => b.Kind == WorldBodyKind.Tile && b.SourceId == source.Id) ==
                source.Letters.Length, "Expose the current authored recovery batch", 12);
            var ids = Frame.Bodies.Where(b => b.Kind == WorldBodyKind.Tile && b.SourceId == source.Id)
                .Select(b => b.Id).ToArray();
            foreach (string id in ids) yield return Collect(id);
        }

        IEnumerator Collect(string id)
        {
            var at = Vector(Body(id).Position);
            yield return SetPlayer(at + Vector3.back * .8f, 0);
            yield return Command(WordboundAction.CollectLetter, id);
            Assert.IsFalse(Frame.Bodies.Any(b => b.Id == id));
        }

        IEnumerator Craft(string recipeId)
        {
            var required = content.Items.Get(recipeId).Letters;
            Assert.IsTrue(Inventory.Letters.Contains(required));
            int prior = Frame.Economy.NextItemId;
            yield return Command(WordboundAction.BeginCraft, value: recipeId);
            yield return Until(() => Frame.Economy.Items.Any(i => i.Id >= prior && i.Word == recipeId &&
                i.Origin == ItemOrigin.Crafted && i.HolderId == 0 && i.State == ItemState.Held),
                "Complete the unchanged " + recipeId + " craft timer", 20);
            lastCrafted = "item:" + Frame.Economy.Items.Single(i => i.Id >= prior && i.Word == recipeId &&
                i.HolderId == 0 && i.State == ItemState.Held).Id;
            Assert.IsTrue(Inventory.Reserved.IsEmpty);
        }

        IEnumerator Deploy(string id, ResolvedCoursePhysicalSite site, Vector3 offset)
        {
            Vector3 forward = Quaternion.Euler(0, site.Yaw, 0) * Vector3.forward;
            yield return SetPlayer(Metres(site.Position) + offset - forward * 1.5f + Vector3.up * .02f, site.Yaw);
            yield return Ticks(3);
            int slot = Array.IndexOf(Inventory.Slots, Item(id).Id);
            Assert.GreaterOrEqual(slot, 0);
            yield return Command(WordboundAction.Deploy, slot: slot);
            Assert.AreEqual(ItemState.Deployed, Item(id).State);
            Assert.IsTrue(controller.Bodies[id].Rigidbody.isKinematic);
        }

        IEnumerator MoveIntoArrival(ResolvedCoursePhysicalSite site, string operation, double seconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            bool entered = false;
            while (Mechanism(site).Physical.Stage != MatchPhysicalStage.Complete &&
                Time.realtimeSinceStartupAsDouble < deadline && !lifetime.IsCancellationRequested)
            {
                Healthy();
                Vector3 at = Vector(Player.Position), target = Metres(site.Target);
                entered |= Inside(site.ArrivalBounds, at);
                Vector2 delta = new Vector2(target.x - at.x, target.z - at.z);
                Vector2 direction = delta.magnitude <= .08f ? Vector2.zero :
                    delta.normalized * Mathf.Clamp(delta.magnitude * .65f, .12f, .8f);
                movement = new WordboundMovementInput { MoveX = direction.x, MoveZ = direction.y };
                yield return null;
            }
            movement = default;
            entered |= Inside(site.ArrivalBounds, Vector(Player.Position));
            Assert.AreEqual(MatchPhysicalStage.Complete, Mechanism(site).Physical.Stage, operation);
            Assert.IsTrue(entered, operation + " must include an observed actor position inside the authored volume.");
        }

        IEnumerator SetBody(string id, Vector3 at, float yaw)
        {
            bool applied = false;
            long prior = Frame.Progress.Tick;
            setup.Enqueue(() => {
                var body = controller.Bodies[id].Rigidbody;
                Assert.IsFalse(body.isKinematic);
                body.position = at; body.rotation = Quaternion.Euler(0, yaw, 0);
                body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
                body.WakeUp(); applied = true;
            });
            yield return Until(() => applied && Frame.Progress.Tick > prior, "Set up the authored receiver before physical transport", 5);
        }

        IEnumerator SaveAndCheck(ResolvedCoursePhysicalSite site)
        {
            long completed = Frame.Progress.Tick;
            yield return Wait(session.PublishCheckpoint(), "Publish actual physical completion checkpoint", 8);
            Assert.GreaterOrEqual(session.Committed.Tick, completed);
            var imported = MatchSnapshotCodec.Import(session.CommittedJson, Frame.Binding, content.Mode,
                content.Course, content.Items, content.Rules);
            Assert.IsTrue(imported.Success, imported.Error);
            Assert.AreEqual(imported.Hash, session.Committed.StateHash);
            var mechanism = imported.Snapshot.Mechanisms.Single(m => m.PuzzleId == site.PuzzleId && m.Team == 0 && m.Lap == 0);
            Assert.IsTrue(mechanism.Solved);
            Assert.AreEqual(MatchPhysicalStage.Complete, mechanism.Physical.Stage);
            Assert.AreEqual(site.Hash, mechanism.Physical.SiteHash);
            Assert.LessOrEqual(imported.Snapshot.Bodies.Count, WordboundEntityBudget.MaximumBodies);
        }

        string PlacementBody(string zone)
        {
            var placement = content.Course.Placements.Single(p => p.ZoneId == zone && p.TeamIndex == 0);
            return Frame.Bodies.Single(b => b.Kind == WorldBodyKind.Item && b.SourceId == placement.Id).Id;
        }
    }
}
