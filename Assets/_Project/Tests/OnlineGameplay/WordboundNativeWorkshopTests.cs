using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        [UnityTest, Category("ExternalPhotonWorkshopGameplay"), Timeout(600000)]
        public IEnumerator TwoAuthenticatedPeersDownloadAndPlayExactAuthoredWorkshopMap()
        {
            if (Environment.GetEnvironmentVariable("WORDBOUND_EXTERNAL_WORKSHOP_GAMEPLAY") != "1")
                Assert.Ignore("Set WORDBOUND_EXTERNAL_WORKSHOP_GAMEPLAY=1 after deploying verified workshop bundle support.");
            return Run(2, false, authoredWorkshop: true);
        }

        [UnityTest, Category("ExternalPhotonWorkshopGameplay"), Timeout(600000)]
        public IEnumerator TwoAuthenticatedPeersShareFreestyleStationRewardAndPreserveInventory()
        {
            if (Environment.GetEnvironmentVariable("WORDBOUND_EXTERNAL_WORKSHOP_GAMEPLAY") != "1")
                Assert.Ignore("Set WORDBOUND_EXTERNAL_WORKSHOP_GAMEPLAY=1 for compact private shared Freestyle behavior.");
            return Run(2, false, authoredWorkshop: true, authoredContentFactory: CreateCompactFreestyle,
                afterStarted: SharedFreestyleStation);
        }

        static WordboundClientContent CreateCompactFreestyle(string projectRoot)
        {
            var original = CreateCompactTimeRun(projectRoot);
            string originalDocument = CourseJson.ExportDocument(original.Course.CopyDocument());
            var document = original.Course.CopyDocument();
            document.Id = "native-shared-freestyle-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            document.Title = "Compact native shared Freestyle proof";
            document.SupportedModes = new List<string> { "Freestyle" };
            document.Puzzles.Single(puzzle => puzzle.Id == FullPuzzle).RewardLetters = "GIFT";
            var second = document.PhysicalSites.Single(site => site.Lap == 2);
            second.Position = new CoursePoint(3000, 0, 2000);
            second.Target = second.Position;
            for (int seat = 4; seat < 8; seat++)
                document.Spawns.Add(new CourseSpawn {
                    Id = "Start" + seat, ModuleId = "Room", Position = new CoursePoint(-3000 + (seat - 4) * 2000, 0, 4000)
                });
            var mode = ModeDefinition.For("Freestyle");
            var resolved = CourseResolver.Resolve(document, original.Geometry,
                new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(mode)));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            Assert.AreEqual(originalDocument, CourseJson.ExportDocument(original.Course.CopyDocument()));
            string folder = Path.Combine(projectRoot, "Assets/_Project/Data/Config");
            return WordboundClientContent.FromAuthored(CourseJson.ExportDocument(document), CourseJson.ExportManifest(resolved.Manifest),
                original.Geometry, File.ReadAllText(Path.Combine(folder, "items.json")), File.ReadAllText(Path.Combine(folder, "rules.json")),
                mode.Id, mode.Format.ToString(), resolved.Manifest.Seed);
        }

        IEnumerator SharedFreestyleStation(Peer host)
        {
            var solver = peers.Single(peer => peer != host);
            Assert.AreEqual("Freestyle", live.Mode.Id);
            Assert.IsFalse(live.PublicQueue);
            Assert.AreEqual("Workshop", live.Verified.Match.Content.Kind);
            Assert.AreEqual(2, live.Verified.Roster.Count);
            Assert.IsTrue(live.Verified.Roster.All(seat => seat.TeamId == 0));
            Assert.AreEqual(1, live.Verified.Roster.Select(seat => seat.PartyId).Distinct().Count());
            Assert.IsEmpty(live.Course.Laps);
            Assert.AreEqual("GIFT", live.Course.Puzzles.Single(puzzle => puzzle.Id == FullPuzzle).RewardLetters);
            var first = live.Course.PhysicalSites.Single(site => site.Lap == 1);
            var second = live.Course.PhysicalSites.Single(site => site.Lap == 2);
            string document = CourseJson.ExportDocument(live.Course.CopyDocument());
            long fromTick = host.Controller.Frame.Progress.Tick;
            string raceError = "";
            void ObservePractice(MatchSnapshot frame)
            {
                if (!FreestyleWithoutRaceProgress(frame)) raceError = "Freestyle emitted race progress, narrative, or a result.";
            }
            ObservePractice(host.Controller.Frame);
            host.Controller.FrameChanged += ObservePractice;
            try
            {
                yield return FullWalk(host, host, () => FullSocketApproach, .08f, "Reach the first shared practice station");
                yield return Command(host, host, WordboundAction.PracticePuzzle, first.Hash);
                long selectedTick = host.Controller.Frame.Practice.SelectedTick;
                yield return CommittedState(host, frame => frame.Practice.SiteHash == first.Hash && frame.Practice.SelectedTick == selectedTick,
                    "Commit the host's shared station selection on both peers");
                yield return FullWalk(host, host, () => FullHostParking, .25f, "Leave the shared socket clear for the teammate");

                var source = live.Course.Supplies.Single(supply => supply.SourceId == "TableLetters");
                yield return Until(() => host.Controller.Frame.Bodies.Count(body => body.Kind == WorldBodyKind.Tile &&
                    body.SourceId == source.Id) == 5, "Expose the selected station's real TABLE letters", 15);
                string[] letters = host.Controller.Frame.Bodies.Where(body => body.Kind == WorldBodyKind.Tile && body.SourceId == source.Id)
                    .Select(body => body.Id).ToArray();
                foreach (string id in letters)
                {
                    yield return FullWalk(host, solver, () => Pose(host, id).Position, .85f, "Teammate reaches each practice letter through input");
                    yield return Command(host, solver, WordboundAction.CollectLetter, id);
                }
                Assert.IsTrue(Inventory(host, solver).Letters.Equals(LetterBag.FromWord("TABLE")));
                int firstItem = host.Controller.Frame.Economy.NextItemId;
                int solverSeat = WordboundGameplayPlan.Seat(host.Controller.Frame.Binding, solver.Actor);
                yield return Command(host, solver, WordboundAction.BeginCraft, value: "TABLE");
                yield return Until(() => host.Controller.Frame.Economy.Items.Any(item => item.Id >= firstItem && item.Word == "TABLE" &&
                    item.Origin == ItemOrigin.Crafted && item.HolderId == solverSeat && item.State == ItemState.Held),
                    "Complete the teammate's unchanged TABLE crafting timer", 20);
                var table = host.Controller.Frame.Economy.Items.Single(item => item.Id >= firstItem && item.Word == "TABLE" &&
                    item.Origin == ItemOrigin.Crafted && item.HolderId == solverSeat && item.State == ItemState.Held);
                string tableId = "item:" + table.Id;
                yield return FullWalk(host, solver, () => FullSocketApproach, .08f, "Teammate approaches the shared authored socket");
                solver.InteractionInput = new WordboundMovementInput { HasFacing = true, FacingYaw = 0 };
                yield return Until(() => FullFacesSocket(host, solver), "Face the real practice socket through admitted input", 10);
                int slot = Array.IndexOf(Inventory(host, solver).Slots, table.Id);
                Assert.GreaterOrEqual(slot, 0);
                yield return Command(host, solver, WordboundAction.Deploy, slot: slot);
                solver.InteractionInput = default;
                Assert.AreEqual(-1, Pose(host, tableId).OwnerTeam);
                Assert.IsTrue(host.Controller.Bodies[tableId].Rigidbody.isKinematic);
                yield return Until(() => FullMechanism(host, 0).Physical.Stage == MatchPhysicalStage.Complete,
                    "Complete the selected station using the teammate's actual native TABLE collider", 15);
                Assert.AreEqual(solver.Actor, FullMechanism(host, 0).Physical.ActorId);
                Assert.AreEqual(first.Hash, FullMechanism(host, 0).Physical.SiteHash);
                Assert.AreEqual(tableId, FullMechanism(host, 0).ItemEntityId);
                Assert.GreaterOrEqual(FullMechanism(host, 0).ProgressTicks, 30);

                int firstReward = host.Controller.Frame.Economy.NextTileId;
                int minted = host.Controller.Frame.Economy.Minted;
                var solve = solver.Controller.Submit(WordboundAction.UsePuzzle, FullPuzzle, value: tableId);
                yield return FreestyleReceipt(host, solver, solve, "Ok");
                AssertFreestyleReward(host.Controller.Frame, firstReward, minted);
                Assert.IsTrue(Item(host, tableId).IsGone);
                solver.Session.SubmitCommand(WordboundGameplayPlan.Command(solve));
                yield return Until(() => host.CommandObservations.TryGetValue(Key(solver.Actor, solve.Sequence), out int seen) && seen >= 2,
                    "Deliver the exact successful solve command again", 25);
                Assert.AreEqual(1, host.Controller.Frame.Commands.Receipts.Count(receipt => receipt.ActorId == solver.Actor && receipt.Sequence == solve.Sequence));
                AssertFreestyleReward(host.Controller.Frame, firstReward, minted);
                var repeated = solver.Controller.Submit(WordboundAction.UsePuzzle, FullPuzzle, value: tableId);
                yield return FreestyleReceipt(host, solver, repeated, "AlreadySolved");
                AssertFreestyleReward(host.Controller.Frame, firstReward, minted);
                yield return CommittedState(host, frame => frame.Mechanisms.Single(state => state.PuzzleId == FullPuzzle && state.Lap == 0).Solved &&
                    frame.Economy.NextTileId == firstReward + 4 && frame.Commands.Receipts.Any(receipt => receipt.ActorId == solver.Actor &&
                        receipt.Sequence == repeated.Sequence && receipt.PayloadHash == repeated.PayloadHash && receipt.Effect.Code == "AlreadySolved"),
                    "Acknowledge shared completion, exact reward, and rejected repeat on both peers");
                foreach (var peer in peers) AssertFreestyleReward(peer.Controller.Frame, firstReward, minted);

                string rewardId = "tile:" + firstReward;
                yield return FullWalk(host, solver, () => Pose(host, rewardId).Position, .85f, "Reach one earned shared reward letter");
                yield return Command(host, solver, WordboundAction.CollectLetter, rewardId);
                Assert.AreEqual(1, Inventory(host, solver).Letters.Count);
                var inventories = host.Controller.Frame.Economy.Players;
                yield return FullWalk(host, solver, () => new WorldVector(-3, 0, -2), .2f, "Preserve the reward while leaving the next station clear");
                yield return FullWalk(host, host, () => new WorldVector(3, 0, .5f), .08f, "Reach the distinct second practice station");
                yield return Command(host, host, WordboundAction.PracticePuzzle, second.Hash);
                long switchedTick = host.Controller.Frame.Practice.SelectedTick;
                Assert.Greater(switchedTick, selectedTick);
                yield return CommittedState(host, frame => frame.Practice.SiteHash == second.Hash && frame.Practice.SelectedTick == switchedTick &&
                    frame.Mechanisms.Single(state => state.PuzzleId == FullPuzzle && state.Lap == 0).Solved &&
                    !frame.Mechanisms.Single(state => state.PuzzleId == FullPuzzle && state.Lap == 1).Solved &&
                    frame.Bodies.Count(body => body.Kind == WorldBodyKind.Tile && body.SourceId == source.Id) == 5,
                    "Switch the shared station and expose its real recovery letters on both peers");
                foreach (var peer in peers) AssertFreestyleInventories(inventories, peer.Controller.Frame.Economy.Players);

                yield return FreestyleFriendlyAttack(host, solver);
                long observedThrough = host.Controller.Frame.Progress.Tick;
                yield return CommittedState(host, frame => frame.Progress.Tick >= observedThrough && FreestyleWithoutRaceProgress(frame),
                    "Acknowledge the observed Freestyle interval without a race result");
                Assert.Greater(observedThrough, fromTick);
                Assert.IsEmpty(raceError);
                Assert.AreEqual(document, CourseJson.ExportDocument(live.Course.CopyDocument()));
                Assert.Greater(host.MovingInputReads, 0); Assert.Greater(solver.MovingInputReads, 0);
                Assert.AreEqual(0, solver.Advanced);
                Debug.Log("WORDBOUND_SHARED_FREESTYLE_OBSERVED: two authenticated scripted clients; private compact authored course; " +
                    "host selection and teammate native TABLE solve; one four-letter GIFT reward with replay/fresh-repeat checks; " +
                    "collected reward preserved across station switch; admitted nearby unarmed attack caused no teammate damage; " +
                    "no race result during ticks=" + fromTick + ".." + observedThrough + "; not public flagship/human/hazard/recovery acceptance; cleanup pending.");
            }
            finally
            {
                host.Controller.FrameChanged -= ObservePractice;
                foreach (var peer in peers) { peer.ScriptedMovement = null; peer.InteractionInput = default; }
            }
        }

        IEnumerator FreestyleReceipt(Peer host, Peer actor, PlayerCommand command, string expectedCode)
        {
            yield return Until(() => host.Controller.Frame.Commands.Frontiers[actor.Actor] >= command.Sequence,
                "Apply the admitted Freestyle " + command.Action + " command", 15);
            var receipt = host.Controller.Frame.Commands.Receipts.Single(value => value.ActorId == actor.Actor && value.Sequence == command.Sequence);
            Assert.AreEqual(command.PayloadHash, receipt.PayloadHash);
            Assert.AreEqual(expectedCode, receipt.Effect.Code);
        }

        static void AssertFreestyleReward(MatchSnapshot frame, int firstTile, int mintedBefore)
        {
            Assert.AreEqual(firstTile + 4, frame.Economy.NextTileId);
            Assert.AreEqual(mintedBefore + 4, frame.Economy.Minted);
            var reward = frame.Economy.Tiles.Where(tile => tile.Key >= firstTile).ToArray();
            CollectionAssert.AreEqual(Enumerable.Range(firstTile, 4), reward.Select(tile => tile.Key));
            CollectionAssert.AreEquivalent("GIFT".ToCharArray(), reward.Select(tile => tile.Value));
            foreach (var tile in reward)
            {
                var body = frame.Bodies.Single(value => value.Id == "tile:" + tile.Key);
                Assert.AreEqual(WorldBodyKind.Tile, body.Kind); Assert.AreEqual(-1, body.OwnerTeam);
                Assert.AreEqual("", body.SourceId); Assert.IsTrue(body.Active);
            }
        }

        static bool FreestyleWithoutRaceProgress(MatchSnapshot frame) => frame.Progress.Result == null && frame.NarrativeFlags.Count == 0 &&
            frame.Progress.Teams.All(team => team.Lap == 0 && team.NextCheckpoint == 0 && team.OrderedObjectives == 0 &&
                team.CompletedPuzzles.Count == 0 && !team.Finished);

        static void AssertFreestyleInventories(IReadOnlyList<PlayerInventory> before, IReadOnlyList<PlayerInventory> after)
        {
            Assert.AreEqual(before.Count, after.Count);
            for (int index = 0; index < before.Count; index++)
            {
                Assert.AreEqual(before[index].PlayerId, after[index].PlayerId);
                Assert.IsTrue(before[index].Letters.Equals(after[index].Letters));
                Assert.IsTrue(before[index].Reserved.Equals(after[index].Reserved));
                CollectionAssert.AreEqual(before[index].Slots, after[index].Slots);
                Assert.AreEqual(before[index].CarriedFurniture, after[index].CarriedFurniture);
                Assert.IsNull(before[index].Craft); Assert.IsNull(after[index].Craft);
                Assert.AreEqual(before[index].CanAct, after[index].CanAct);
            }
        }

        IEnumerator FreestyleFriendlyAttack(Peer host, Peer victim)
        {
            yield return FullWalk(host, victim, () => new WorldVector(3, 0, -2), .04f, "Reach the unobstructed teammate attack position");
            yield return FullWalk(host, host, () => new WorldVector(2, 0, -2), .04f, "Stand within actual unarmed teammate reach");
            host.InteractionInput = new WordboundMovementInput { HasFacing = true, FacingYaw = 90 };
            var stats = live.Rules.Unarmed;
            Assert.Greater(stats.Damage, 0);
            yield return Until(() => FreestyleAttackOpportunity(host, victim, host.Controller.Frame, stats),
                "Face the unprotected teammate in native and canonical reach with clear line of sight", 10);
            var before = host.Controller.Frame.Actors.Single(actor => actor.PlayerId == victim.Actor);
            int knockouts = host.Controller.Frame.Progress.Players.Single(player => player.Id == victim.Actor).Knockouts;
            long started = -1;
            int activeSamples = 0, qualifyingSamples = 0;
            string damageError = "";
            void Observe(MatchSnapshot frame)
            {
                var target = frame.Actors.Single(actor => actor.PlayerId == victim.Actor);
                if (target.Health != before.Health || target.StunnedUntilTick != before.StunnedUntilTick ||
                    frame.Progress.Players.Single(player => player.Id == victim.Actor).Knockouts != knockouts)
                    damageError = "The teammate changed health, stun, or knockouts during the admitted attack.";
                var attack = frame.Actors.Single(actor => actor.PlayerId == host.Actor).Attack;
                if (attack == null) return;
                if (attack.WeaponEntityId != "") damageError = "The test attack must use the actual unarmed rules.";
                if (started >= 0 && started != attack.StartedTick) damageError = "The single command unexpectedly started multiple attacks.";
                started = attack.StartedTick;
                long activeAt = started + WordboundCombatPolicy.WindupTicks(stats);
                if (frame.Progress.Tick < activeAt || frame.Progress.Tick >= activeAt + WordboundCombatPolicy.ActiveTicks(stats)) return;
                activeSamples++;
                if (FreestyleAttackOpportunity(host, victim, frame, stats)) qualifyingSamples++;
            }
            host.Controller.FrameChanged += Observe;
            try
            {
                var attack = host.Controller.Submit(WordboundAction.Attack);
                yield return FreestyleReceipt(host, host, attack, "Ok");
                yield return Until(() => started >= 0 && host.Controller.Frame.Progress.Tick >= started + WordboundCombatPolicy.CycleTicks(stats) &&
                    host.Controller.Frame.Actors.Single(actor => actor.PlayerId == host.Actor).Attack == null,
                    "Observe the admitted unarmed windup, active interval, and recovery on real ticks", 10);
                Assert.Greater(activeSamples, 0, "An accepted command alone does not establish an active attack.");
                Assert.Greater(qualifyingSamples, 0, "No-damage evidence requires an actual reachable unprotected target during the active interval.");
                Assert.IsEmpty(damageError);
                long through = host.Controller.Frame.Progress.Tick;
                yield return CommittedState(host, frame => frame.Progress.Tick >= through &&
                    frame.Actors.Single(actor => actor.PlayerId == victim.Actor).Health == before.Health &&
                    frame.Actors.Single(actor => actor.PlayerId == victim.Actor).StunnedUntilTick == before.StunnedUntilTick &&
                    frame.Progress.Players.Single(player => player.Id == victim.Actor).Knockouts == knockouts &&
                    frame.Commands.Receipts.Any(receipt => receipt.ActorId == host.Actor && receipt.Sequence == attack.Sequence &&
                        receipt.PayloadHash == attack.PayloadHash && receipt.Effect.Code == "Ok"),
                    "Acknowledge the accepted attack and unchanged teammate state on both peers");
                Assert.IsEmpty(damageError);
                Debug.Log("WORDBOUND_FREESTYLE_ATTACK_OBSERVED: unarmed=true; activeSamples=" + activeSamples +
                    "; nativeAndCanonicalOpportunitySamples=" + qualifyingSamples + "; startedTick=" + started +
                    "; throughTick=" + through + "; rangeFacingLineOfSightCanAct=true; protectionDodgeDefense=false; " +
                    "unchangedHealthStunKnockouts=true; hazardStimulus=false");
            }
            finally
            {
                host.Controller.FrameChanged -= Observe;
                host.InteractionInput = default;
            }
        }

        static bool FreestyleAttackOpportunity(Peer host, Peer victim, MatchSnapshot frame, MeleeStats stats)
        {
            long tick = frame.Progress.Tick;
            var attacker = frame.Actors.Single(actor => actor.PlayerId == host.Actor);
            var target = frame.Actors.Single(actor => actor.PlayerId == victim.Actor);
            var player = frame.Progress.Players.Single(value => value.Id == victim.Actor);
            var inventory = frame.Economy.Players[WordboundGameplayPlan.Seat(frame.Binding, host.Actor)];
            if (attacker.EquippedSlot != -1 || !inventory.CanAct || inventory.Craft != null || inventory.CarriedFurniture >= 0 ||
                attacker.StunnedUntilTick > tick || attacker.Defense != null || target.Defense != null || target.DodgeUntilTick > tick ||
                target.Health <= 0 || player.ProtectedUntil > tick || player.Eliminated || player.Returned || !player.Connected) return false;
            var origin = frame.Bodies.Single(body => body.Kind == WorldBodyKind.Player && body.SourceId == host.Actor);
            var destination = frame.Bodies.Single(body => body.Kind == WorldBodyKind.Player && body.SourceId == victim.Actor);
            if (!origin.Active || !destination.Active || origin.Position.DistanceSquared(destination.Position) > stats.Reach * stats.Reach) return false;
            var q = origin.RotationXyz;
            if (!Geometry.InFrontArc(2 * (q.X * q.Z + origin.RotationW * q.Y), 1 - 2 * (q.X * q.X + q.Y * q.Y),
                destination.Position.X - origin.Position.X, destination.Position.Z - origin.Position.Z, stats.ArcDegrees)) return false;
            var nativeOrigin = host.Controller.Bodies[origin.Id].Rigidbody;
            var nativeTarget = host.Controller.Bodies[destination.Id].Rigidbody;
            Vector3 delta = nativeTarget.position - nativeOrigin.position;
            Vector3 forward = nativeOrigin.rotation * Vector3.forward;
            if (delta.sqrMagnitude > stats.Reach * stats.Reach ||
                !Geometry.InFrontArc(forward.x, forward.z, delta.x, delta.z, stats.ArcDegrees)) return false;
            return host.Controller.PhysicsWorld.HasLineOfSight(host.Actor, origin.Position, destination.Position) &&
                host.Controller.PhysicsWorld.HasLineOfSight(host.Actor,
                    new WorldVector(nativeOrigin.position.x, nativeOrigin.position.y, nativeOrigin.position.z),
                    new WorldVector(nativeTarget.position.x, nativeTarget.position.y, nativeTarget.position.z));
        }

        static WordboundClientContent CreateAuthoredWorkshop(string projectRoot)
        {
            string folder = Path.Combine(projectRoot, "Assets/_Project/Data/Config");
            string items = File.ReadAllText(Path.Combine(folder, "items.json"));
            string rules = File.ReadAllText(Path.Combine(folder, "rules.json"));
            var flagship = WordboundFlagshipContent.Create(ItemCatalogue.FromJson(items));
            var document = flagship.CopyDocument();
            document.Id = "native-authored-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            document.Title = "Native authored transfer proof";
            var mode = ModeDefinition.For("Freestyle");
            var preview = CourseResolver.Resolve(document, flagship.Geometry,
                new CourseResolveRequest("fedcba9876543210", MatchStateValidation.Requirements(mode)));
            Assert.IsTrue(preview.Success, "The edited draft must validate before creating any guest or party.");
            return WordboundClientContent.FromAuthored(CourseJson.ExportDocument(document), CourseJson.ExportManifest(preview.Manifest),
                flagship.Geometry, items, rules, mode.Id, mode.Format.ToString(), preview.Manifest.Seed);
        }

        void AssertDownloadedWorkshop()
        {
            Assert.IsFalse(live.PublicQueue);
            Assert.AreEqual(2, peers.Count);
            Assert.AreNotSame(live.ContentFor(0), live.ContentFor(1));
            Assert.AreNotSame(live.ContentFor(0).Course, live.ContentFor(1).Course);
            foreach (var peer in peers)
            {
                int index = live.Clients.FindIndex(client => client.Credentials.PlayerId == peer.Actor);
                var content = live.ContentFor(index);
                Assert.NotNull(content.WorkshopBundle);
                Assert.AreEqual(live.Course.Hash, content.Course.Hash);
                Assert.AreEqual(live.Course.SourceHash, content.Course.SourceHash);
                Assert.AreEqual(CourseJson.ExportDocument(live.Course.CopyDocument()), CourseJson.ExportDocument(content.Course.CopyDocument()));
                Assert.AreEqual(live.Course.Hash, peer.Controller.CourseWorld.ManifestHash);
                Assert.AreEqual(live.Course.Hash, peer.Controller.Frame.Binding.ManifestHash);
                Assert.AreEqual(live.Course.SourceHash, peer.Controller.Frame.Binding.CourseHash);
                Assert.AreEqual(live.Content.CatalogHash, peer.Controller.Frame.Binding.CatalogueHash);
                Assert.AreEqual(WordboundRulesFingerprint.Compute(live.Rules), WordboundRulesFingerprint.Compute(content.Rules));
            }
            Debug.Log("WORDBOUND_AUTHORED_TRANSFER_OBSERVED: two authenticated peers downloaded and composed source=" +
                live.Course.SourceHash + " manifest=" + live.Course.Hash + "; real bodies/input/checkpoint verified; cleanup pending.");
        }
    }
}
