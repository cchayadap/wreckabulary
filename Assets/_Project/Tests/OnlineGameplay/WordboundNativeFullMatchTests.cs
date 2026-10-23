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
        const string FullPuzzle = "DeliverySocket";
        static readonly WorldVector FullSocketApproach = new WorldVector(0, 0, .5f);
        static readonly WorldVector FullCheckpoint = new WorldVector(-2, 0, -1);
        static readonly WorldVector FullFinish = new WorldVector(2, 0, -2);
        static readonly WorldVector FullHostParking = new WorldVector(4, 0, -2);

        [UnityTest, Category("ExternalPhotonFullMatch"), Timeout(600000)]
        public IEnumerator TwoAuthenticatedPeersCompleteCompactAuthoredTimeRunAndConfirmDurableResult()
        {
            if (Environment.GetEnvironmentVariable("WORDBOUND_EXTERNAL_FULL_MATCH") != "1")
                Assert.Ignore("Set WORDBOUND_EXTERNAL_FULL_MATCH=1 for compact private authored three-lap pipeline coverage.");
            return Run(2, false, authoredWorkshop: true, authoredContentFactory: CreateCompactTimeRun,
                afterStarted: CompleteCompactTimeRun);
        }

        [UnityTest, Category("ExternalPhotonFullMatch"), Timeout(600000)]
        public IEnumerator TwoAuthenticatedPublicFfaIndividualsReachScoredWinAndConfirmDurableResult()
        {
            if (Environment.GetEnvironmentVariable("WORDBOUND_EXTERNAL_FULL_MATCH") != "1")
                Assert.Ignore("Set WORDBOUND_EXTERNAL_FULL_MATCH=1 for public FFA scored-finish coverage.");
            return PublicEntry("FreeForAll", RaceFormat.Solo, 2, CompletePublicFfa);
        }

        IEnumerator CompletePublicFfa(Peer host)
        {
            var victim = peers.Single(peer => peer != host);
            Assert.AreEqual("FreeForAll", live.Mode.Id);
            Assert.IsTrue(live.PublicQueue); Assert.IsFalse(live.Settings.Fill);
            Assert.AreEqual(10, live.Mode.KnockoutTarget);
            Assert.AreEqual(100, live.Rules.MaxHealth);
            var stats = live.Items.Get("HAMMER").Melee;
            Assert.AreEqual(28, stats.Damage);
            Assert.AreEqual(30, live.Items.Get("HAMMER").Durability);
            Assert.AreEqual(77, WordboundCombatPolicy.CycleTicks(stats));
            Assert.AreNotEqual(FfaPlayer(host.Controller.Frame, host).Team, FfaPlayer(host.Controller.Frame, victim).Team);
            Assert.IsTrue(host.Controller.Frame.Progress.Players.All(player => player.Knockouts == 0));
            string document = CourseJson.ExportDocument(live.Course.CopyDocument());
            string contentHash = live.Course.Hash;
            var scores = new Dictionary<int, long> { [0] = host.Controller.Frame.Progress.Tick };
            var respawns = new Dictionary<int, (long Tick, long ProtectedUntil)>();
            int victimSeat = WordboundGameplayPlan.Seat(host.Controller.Frame.Binding, victim.Actor);
            var spawn = live.Course.Spawns.Where(value => value.Kind == "Start").ElementAt(victimSeat).Position;
            var recovery = new WorldVector(spawn.X / 1000f, spawn.Y / 1000f, spawn.Z / 1000f);
            var prior = host.Controller.Frame;
            string observationError = "";
            double nextMemorySample = 0;
            bool memorySamples = Environment.GetEnvironmentVariable("WORDBOUND_PROFILE_NATIVE") == "1";
            void SampleMemory()
            {
                if (!memorySamples || Time.realtimeSinceStartupAsDouble < nextMemorySample) return;
                nextMemorySample = Time.realtimeSinceStartupAsDouble + 5;
                foreach (var peer in peers)
                {
                    if (peer.Runner == null || !peer.Runner.IsRunning) continue;
                    bool available = peer.Runner.TryGetFusionStatistics(out var statistics);
                    var memory = available ? statistics.MemorySnapshot : default;
                    var objects = memory.ObjectAllocatorMemorySnapshot;
                    var general = memory.GeneralAllocatorMemorySnapshot;
                    Debug.Log($"WORDBOUND_FFA_MEMORY: peer={peers.IndexOf(peer)},host={peer.Runner.IsServer}," +
                        $"tick={peer.Runner.Tick.Raw},matchTick={host.Controller.Frame.Progress.Tick}," +
                        $"networkObjects={peer.Runner.GetAllNetworkObjects().Count},statisticsAvailable={available}," +
                        $"objectUsed={objects.TotalBytesUsed},objectFree={objects.TotalBytesFree}," +
                        $"objectBlocks={objects.TotalBlocks},objectFreeBlocks={objects.TotalFreeBlocks}," +
                        $"generalUsed={general.TotalBytesUsed},generalFree={general.TotalBytesFree}");
                }
            }
            void Observe(MatchSnapshot frame)
            {
                SampleMemory();
                var scorer = FfaPlayer(frame, host); var target = FfaPlayer(frame, victim);
                if (target.Knockouts != 0) observationError = "The remote victim must never earn an unrelated knockout.";
                if (frame.Progress.Result != null && scorer.Knockouts != 10) observationError = "FFA ended before the ten-point transition.";
                if (frame.Progress.Tick <= prior.Progress.Tick) return;
                var oldScorer = FfaPlayer(prior, host); var oldTarget = FfaPlayer(prior, victim);
                if (scorer.Knockouts != oldScorer.Knockouts)
                {
                    if (scorer.Knockouts != oldScorer.Knockouts + 1 || !target.Eliminated ||
                        FfaActor(frame, victim).Health != 0 || PlayerPose(host, victim).Active ||
                        target.RespawnAt != frame.Progress.Tick + 2 * ModeDefinition.TickRate)
                        observationError = "A point must be one real lethal transition with an automatic respawn deadline.";
                    scores[scorer.Knockouts] = frame.Progress.Tick;
                }
                if (target.RecoveryGeneration != oldTarget.RecoveryGeneration)
                {
                    if (target.RecoveryGeneration != oldTarget.RecoveryGeneration + 1 || !oldTarget.Eliminated ||
                        target.Eliminated || target.RespawnAt != -1 || FfaActor(frame, victim).Health != live.Rules.MaxHealth ||
                        target.ProtectedUntil != frame.Progress.Tick + ModeDefinition.RecoveryProtectionSeconds * ModeDefinition.TickRate ||
                        !PlayerPose(host, victim).Active)
                        observationError = "The real respawn must restore health, activity and one generation with protection.";
                    var pose = frame.Bodies.Single(body => body.Kind == WorldBodyKind.Player && body.SourceId == victim.Actor);
                    var native = host.Controller.Bodies[pose.Id].Rigidbody.position;
                    if (!(pose.Position.DistanceSquared(recovery) <= .001f * .001f) ||
                        !((native - new Vector3(recovery.X, recovery.Y, recovery.Z)).sqrMagnitude <= .001f * .001f))
                        observationError = "The respawn transition must restore both canonical and native poses within 1 mm of the bound Start.";
                    respawns[target.RecoveryGeneration] = (frame.Progress.Tick, target.ProtectedUntil);
                }
                prior = frame;
            }
            host.Controller.FrameChanged += Observe;
            try
            {
                var weapons = new List<int>();
                var collection = live.Course.Supplies.Where(source => source.SourceId.StartsWith("ArenaKit0Tile", StringComparison.Ordinal) ||
                    source.SourceId.StartsWith("ArenaKit1Tile", StringComparison.Ordinal)).ToArray();
                Assert.Greater(collection.Length, 0);
                var waiting = new WorldVector(recovery.X, recovery.Y, recovery.Z - .75f);
                var bounds = live.Course.Geometry.Single(module => module.Id == "Start").Bounds;
                Assert.Greater(waiting.X, bounds.Min.X / 1000f + .5f);
                Assert.Less(waiting.X, bounds.Max.X / 1000f - .5f);
                Assert.Greater(waiting.Z, bounds.Min.Z / 1000f + .5f);
                Assert.Less(waiting.Z, collection.Min(source => source.Position.Z / 1000f) - 2);
                yield return FullWalk(host, victim, () => waiting, .04f,
                    "Park the remote player south of the public collection lanes through admitted movement");
                Assert.LessOrEqual(PlayerPose(host, victim).Position.DistanceSquared(waiting), .25f * .25f);
                var parked = host.Controller.Bodies["player:" + victimSeat].Rigidbody.position;
                Assert.LessOrEqual((parked - new Vector3(waiting.X, waiting.Y, waiting.Z)).sqrMagnitude, .25f * .25f);
                Assert.Less(parked.z, collection.Min(source => source.Position.Z / 1000f) - 1.75f);
                yield return FfaCraftHammer(host, 0, weapons);
                yield return FfaCraftHammer(host, 1, weapons);
                Assert.AreEqual(2, weapons.Distinct().Count());
                yield return FullWalk(host, victim, () => recovery, .04f, "Reach the real FFA recovery position before combat");
                yield return FullWalk(host, host, () => new WorldVector(recovery.X - .95f, recovery.Y, recovery.Z), .04f,
                    "Stand in the clear southern FFA contact lane before scoring");
                host.InteractionInput = new WordboundMovementInput { HasFacing = true, FacingYaw = 90 };
                int weaponId = weapons[0];
                yield return Command(host, host, WordboundAction.Equip, slot: Array.IndexOf(Inventory(host, host).Slots, weaponId));
                yield return CommittedState(host, frame => WordboundCombatPolicy.EquippedItemId(frame, host.Actor) == "item:" + weaponId,
                    "Acknowledge the first genuinely crafted FFA hammer");
                PlayerCommand replay = null;
                for (int knockout = 1; knockout <= 10; knockout++)
                {
                    if (knockout == 6)
                    {
                        weaponId = weapons[1];
                        yield return Command(host, host, WordboundAction.Equip, slot: Array.IndexOf(Inventory(host, host).Slots, weaponId));
                        yield return CommittedState(host, frame => WordboundCombatPolicy.EquippedItemId(frame, host.Actor) == "item:" + weaponId,
                            "Acknowledge the second held hammer without replenishing durability");
                    }
                    for (int hit = 0; hit < 4; hit++)
                    {
                        yield return FullWalk(host, victim, () => {
                            var origin = PlayerPose(host, host).Position;
                            return new WorldVector(origin.X + .95f, origin.Y, origin.Z);
                        }, .04f, "Return the zero-score victim to native FFA contact through admitted movement");
                        yield return FfaStrike(host, victim, weaponId, false, command => replay ??= command);
                        Assert.AreEqual(hit == 3 ? knockout : knockout - 1, FfaPlayer(host.Controller.Frame, host).Knockouts);
                        Assert.IsEmpty(observationError);
                        if (knockout == 1 && hit == 0)
                        {
                            float health = FfaActor(host.Controller.Frame, victim).Health;
                            long nextAttack = FfaActor(host.Controller.Frame, host).NextAttackTick;
                            host.Session.SubmitCommand(WordboundGameplayPlan.Command(replay));
                            yield return Until(() => host.CommandObservations.TryGetValue(Key(host.Actor, replay.Sequence), out int seen) && seen >= 2,
                                "Observe delivery of the original FFA attack command twice", 15);
                            Assert.AreEqual(1, host.Controller.Frame.Commands.Receipts.Count(receipt => receipt.ActorId == host.Actor &&
                                receipt.Sequence == replay.Sequence && receipt.PayloadHash == replay.PayloadHash));
                            Assert.AreEqual(health, FfaActor(host.Controller.Frame, victim).Health);
                            Assert.AreEqual(nextAttack, FfaActor(host.Controller.Frame, host).NextAttackTick);
                            Assert.IsNull(FfaActor(host.Controller.Frame, host).Attack);
                            Assert.AreEqual(0, FfaPlayer(host.Controller.Frame, host).Knockouts);
                        }
                    }
                    if (knockout == 10) break;
                    yield return Until(() => respawns.ContainsKey(knockout), "Observe automatic FFA respawn generation " + knockout, 10);
                    var respawn = respawns[knockout];
                    Assert.AreEqual(scores[knockout] + 2 * ModeDefinition.TickRate, respawn.Tick);
                    Assert.AreEqual(knockout, FfaPlayer(host.Controller.Frame, victim).RecoveryGeneration);
                    if (knockout == 1) yield return FfaStrike(host, victim, weaponId, true);
                    yield return Until(() => host.Controller.Frame.Progress.Tick >= respawn.ProtectedUntil,
                        "Wait for the actual FFA protection window to expire", 10);
                    Assert.AreEqual(live.Rules.MaxHealth, FfaActor(host.Controller.Frame, victim).Health);
                    Assert.AreEqual(knockout, FfaPlayer(host.Controller.Frame, host).Knockouts);
                    Assert.IsNull(host.Controller.Frame.Progress.Result);
                }
                yield return Until(() => peers.All(peer => peer.Controller.Frame.Progress.Result?.Confirmed == true),
                    "Confirm the natural ten-knockout result on host and replica", 25);
                long resultTick = scores[10];
                foreach (var peer in peers) AssertFfaResult(peer.Controller.Frame, host, victim, resultTick);
                CollectionAssert.AreEquivalent(Enumerable.Range(0, 11), scores.Keys);
                CollectionAssert.AreEquivalent(Enumerable.Range(1, 9), respawns.Keys);
                yield return VerifyFfaDurableResult(host, victim, resultTick);
                Assert.IsEmpty(observationError);
                Assert.AreEqual(contentHash, live.Course.Hash);
                Assert.AreEqual(document, CourseJson.ExportDocument(live.Course.CopyDocument()));
                Assert.AreEqual(0, victim.Advanced);
                Assert.Greater(victim.MovingInputReads, 0);
                Debug.Log("WORDBOUND_PUBLIC_FFA_FINISH_OBSERVED: two authenticated scripted individuals; hostScorer=true; remoteVictim=true; " +
                    "two crafted hammers; ten native knockouts; nine automatic respawns; one protected active-contact block; " +
                    "confirmed individual Win and exact downloaded durable checkpoint ACK; remoteAttackSubmission=false; cleanup pending.");
            }
            finally
            {
                host.Controller.FrameChanged -= Observe;
                foreach (var peer in peers) { peer.ScriptedMovement = null; peer.InteractionInput = default; }
            }
        }

        IEnumerator FfaCraftHammer(Peer host, int kit, List<int> weapons)
        {
            Assert.IsTrue(Inventory(host, host).Letters.IsEmpty);
            var claimed = new HashSet<string>(StringComparer.Ordinal);
            foreach (char letter in "HAMMER")
            {
                var source = live.Course.Supplies.First(value => value.SourceId.StartsWith("ArenaKit" + kit + "Tile", StringComparison.Ordinal) &&
                    value.Letters == letter.ToString() && !claimed.Contains(value.Id));
                Assert.IsTrue(claimed.Add(source.Id));
                string tile = host.Controller.Frame.Bodies.Single(body => body.Kind == WorldBodyKind.Tile && body.SourceId == source.Id).Id;
                yield return FullWalk(host, host, () => Pose(host, tile).Position, 1.1f, "Collect the actual public arena hammer letter");
                yield return FfaCollectLetter(host, tile);
            }
            Assert.IsTrue(Inventory(host, host).Letters.Equals(LetterBag.FromWord("HAMMER")));
            int seat = WordboundGameplayPlan.Seat(host.Controller.Frame.Binding, host.Actor);
            yield return Command(host, host, WordboundAction.BeginCraft, value: "HAMMER");
            yield return Until(() => host.Controller.Frame.Economy.Items.Any(item => item.Word == "HAMMER" &&
                item.Origin == ItemOrigin.Crafted && item.CrafterId == seat && item.State == ItemState.Held && !weapons.Contains(item.Id)),
                "Complete the real FFA hammer craft timer", 20);
            int id = host.Controller.Frame.Economy.Items.Single(item => item.Word == "HAMMER" &&
                item.Origin == ItemOrigin.Crafted && item.CrafterId == seat && item.State == ItemState.Held && !weapons.Contains(item.Id)).Id;
            weapons.Add(id);
            Assert.IsTrue(Inventory(host, host).Letters.IsEmpty); Assert.IsTrue(Inventory(host, host).Reserved.IsEmpty);
            yield return CommittedState(host, frame => frame.Economy.Items.Any(item => item.Id == id && item.State == ItemState.Held) &&
                frame.Economy.Players[seat].Slots.Contains(id), "Acknowledge the actually crafted public FFA item on both peers");
        }

        IEnumerator FfaCollectLetter(Peer host, string tile)
        {
            var before = FfaCollectOpportunity(host, tile);
            if (!before.Eligible) Debug.Log("WORDBOUND_FFA_COLLECT: phase=precondition; " + before.Facts);
            Assert.IsTrue(before.Eligible, "A fresh collection command requires actual host range, line of sight, active tile and ownership.");
            var command = host.Controller.Submit(WordboundAction.CollectLetter, tile);
            yield return Until(() => host.Controller.Frame.Commands.Frontiers[host.Actor] >= command.Sequence,
                "Apply admitted CollectLetter", 15);
            var receipt = host.Controller.Frame.Commands.Receipts.Single(value => value.ActorId == host.Actor && value.Sequence == command.Sequence);
            if (receipt.Effect.Code != "Ok")
            {
                Debug.Log("WORDBOUND_FFA_COLLECT: phase=before-submit; " + before.Facts);
                try { Debug.Log("WORDBOUND_FFA_COLLECT: phase=rejected-receipt; " + FfaCollectOpportunity(host, tile).Facts); }
                catch (Exception) { Debug.Log("WORDBOUND_FFA_COLLECT: phase=rejected-receipt; observationAvailable=false"); }
            }
            Assert.AreEqual(command.PayloadHash, receipt.PayloadHash);
            Assert.AreEqual("Ok", receipt.Effect.Code, "CollectLetter must succeed through real authority validation without retry.");
        }

        (bool Eligible, string Facts) FfaCollectOpportunity(Peer host, string tileId)
        {
            var frame = host.Controller.Frame;
            var origin = PlayerPose(host, host);
            var tile = Pose(host, tileId);
            int team = frame.Binding.Seats.Single(seat => seat.PlayerId == host.Actor).Team;
            bool ownership = live.Mode.CanInteract(team, tile.OwnerTeam, tile.Protected);
            float distance = Mathf.Sqrt(origin.Position.DistanceSquared(tile.Position));
            bool sight = host.Controller.PhysicsWorld.HasLineOfSight(host.Actor, origin.Position, tile.Position);
            var nativeOrigin = host.Controller.Bodies[origin.Id].Rigidbody;
            var nativeTile = host.Controller.Bodies[tile.Id].Rigidbody;
            float nativeDistance = Vector3.Distance(nativeOrigin.position, nativeTile.position);
            bool nativeSight = host.Controller.PhysicsWorld.HasLineOfSight(host.Actor,
                new WorldVector(nativeOrigin.position.x, nativeOrigin.position.y, nativeOrigin.position.z),
                new WorldVector(nativeTile.position.x, nativeTile.position.y, nativeTile.position.z));
            bool eligible = origin.Active && tile.Kind == WorldBodyKind.Tile && tile.Active && ownership &&
                distance <= 3 && nativeDistance <= 3 && sight && nativeSight;
            string facts = FormattableString.Invariant($"tick={frame.Progress.Tick}; activeActor={origin.Active}; activeTile={tile.Active}; tileKind={(int)tile.Kind}; ownership={ownership}; canonicalDistance={distance:F6}; nativeDistance={nativeDistance:F6}; canonicalLos={sight}; nativeLos={nativeSight}; actor={nativeOrigin.position.x:F6}/{nativeOrigin.position.y:F6}/{nativeOrigin.position.z:F6}; tile={nativeTile.position.x:F6}/{nativeTile.position.y:F6}/{nativeTile.position.z:F6}; actorSpeed={nativeOrigin.linearVelocity.magnitude:F6}; tileSpeed={nativeTile.linearVelocity.magnitude:F6}");
            return (eligible, facts);
        }

        IEnumerator FfaStrike(Peer host, Peer victim, int weapon, bool protectedContact, Action<PlayerCommand> submitted = null)
        {
            var stats = live.Items.Get("HAMMER").Melee;
            int windup = WordboundCombatPolicy.WindupTicks(stats), active = WordboundCombatPolicy.ActiveTicks(stats);
            yield return Until(() => {
                var frame = host.Controller.Frame; var actor = FfaActor(frame, host); var target = FfaPlayer(frame, victim);
                return Inventory(host, host).CanAct && Inventory(host, host).Craft == null && actor.Attack == null &&
                    actor.NextAttackTick <= frame.Progress.Tick && WordboundCombatPolicy.EquippedItemId(frame, host.Actor) == "item:" + weapon &&
                    !target.Eliminated && FfaActor(frame, victim).Health > 0 && FfaActor(frame, victim).Defense == null &&
                    FfaActor(frame, victim).DodgeUntilTick <= frame.Progress.Tick &&
                    (protectedContact ? frame.Progress.Tick + 1 + windup + active <= target.ProtectedUntil : target.ProtectedUntil <= frame.Progress.Tick) &&
                    FfaContact(host, victim, frame, stats);
            }, protectedContact ? "Reach a complete active attack opportunity inside real spawn protection" : "Reach an eligible native FFA hammer attack", 10);
            var before = host.Controller.Frame;
            float health = FfaActor(before, victim).Health, durability = Item(host, "item:" + weapon).Durability;
            Assert.Greater(durability, 0);
            float expected = protectedContact ? health : Math.Max(0, health - stats.Damage);
            long started = -1, damageTick = -1;
            var activeTicks = new HashSet<long>();
            int geometricSamples = 0;
            string error = "";
            var prior = before;
            void Observe(MatchSnapshot frame)
            {
                var attack = FfaActor(frame, host).Attack;
                if (attack != null)
                {
                    if (attack.WeaponEntityId != "item:" + weapon || started >= 0 && started != attack.StartedTick)
                        error = "The admitted command changed its weapon or started more than one attack.";
                    started = attack.StartedTick;
                }
                long tick = frame.Progress.Tick;
                if (started >= 0 && tick >= started + windup && tick < started + windup + active)
                {
                    activeTicks.Add(tick);
                    if (FfaContact(host, victim, frame, stats)) geometricSamples++;
                    if (protectedContact && (FfaPlayer(frame, victim).ProtectedUntil <= tick || FfaActor(frame, victim).Health != health))
                        error = "The protected active interval must retain health and actual protection.";
                }
                if (tick > prior.Progress.Tick && FfaActor(frame, victim).Health < FfaActor(prior, victim).Health)
                {
                    var eligible = FfaPlayer(prior, victim);
                    if (protectedContact || damageTick >= 0 || started < 0 || tick < started + windup || tick >= started + windup + active ||
                        eligible.Eliminated || !eligible.Connected || eligible.ProtectedUntil > tick || FfaActor(prior, victim).Defense != null ||
                        FfaActor(prior, victim).DodgeUntilTick > tick || FfaActor(frame, victim).Health != expected || !FfaContact(host, victim, frame, stats))
                        error = "Health changed without one eligible native/canonical active hammer contact.";
                    damageTick = tick;
                }
                prior = frame;
            }
            host.Controller.FrameChanged += Observe;
            try
            {
                var command = host.Controller.Submit(WordboundAction.Attack);
                submitted?.Invoke(command);
                yield return Until(() => host.Controller.Frame.Commands.Frontiers[host.Actor] >= command.Sequence,
                    "Apply the real FFA attack command", 15);
                var receipt = host.Controller.Frame.Commands.Receipts.Single(value => value.ActorId == host.Actor && value.Sequence == command.Sequence);
                Assert.AreEqual(command.PayloadHash, receipt.PayloadHash); Assert.AreEqual("Ok", receipt.Effect.Code);
                yield return Until(() => started >= 0 && (host.Controller.Frame.Progress.Result != null ||
                    host.Controller.Frame.Progress.Tick >= started + WordboundCombatPolicy.CycleTicks(stats)),
                    "Observe the real hammer attack interval or its terminal knockout", 10);
                Assert.IsEmpty(error);
                Assert.Greater(activeTicks.Count, 0); Assert.Greater(geometricSamples, 0);
                Assert.AreEqual(durability - 1, Item(host, "item:" + weapon).Durability);
                if (protectedContact)
                {
                    Assert.AreEqual(active, activeTicks.Count);
                    Assert.LessOrEqual(started + windup + active, FfaPlayer(before, victim).ProtectedUntil);
                    Assert.AreEqual(-1, damageTick);
                    Assert.AreEqual(health, FfaActor(host.Controller.Frame, victim).Health);
                }
                else Assert.GreaterOrEqual(damageTick, started + windup);
                if (host.Controller.Frame.Progress.Result != null)
                {
                    Assert.AreEqual(damageTick, host.Controller.Frame.Progress.Result.Tick);
                    Assert.Less(damageTick, started + windup + active);
                }
            }
            finally { host.Controller.FrameChanged -= Observe; }
        }

        static PlayerProgress FfaPlayer(MatchSnapshot frame, Peer peer) => frame.Progress.Players.Single(player => player.Id == peer.Actor);
        static MatchActorState FfaActor(MatchSnapshot frame, Peer peer) => frame.Actors.Single(actor => actor.PlayerId == peer.Actor);
        static bool FfaContact(Peer host, Peer victim, MatchSnapshot frame, MeleeStats stats)
        {
            var origin = frame.Bodies.Single(body => body.Kind == WorldBodyKind.Player && body.SourceId == host.Actor);
            var target = frame.Bodies.Single(body => body.Kind == WorldBodyKind.Player && body.SourceId == victim.Actor);
            var q = origin.RotationXyz;
            var delta = target.Position;
            if (origin.Position.DistanceSquared(delta) > stats.Reach * stats.Reach ||
                !Geometry.InFrontArc(2 * (q.X * q.Z + origin.RotationW * q.Y), 1 - 2 * (q.X * q.X + q.Y * q.Y),
                    delta.X - origin.Position.X, delta.Z - origin.Position.Z, stats.ArcDegrees)) return false;
            var nativeOrigin = host.Controller.Bodies[origin.Id].Rigidbody; var nativeTarget = host.Controller.Bodies[target.Id].Rigidbody;
            Vector3 distance = nativeTarget.position - nativeOrigin.position, forward = nativeOrigin.rotation * Vector3.forward;
            return distance.sqrMagnitude <= stats.Reach * stats.Reach && Geometry.InFrontArc(forward.x, forward.z, distance.x, distance.z, stats.ArcDegrees) &&
                host.Controller.PhysicsWorld.HasLineOfSight(host.Actor, origin.Position, target.Position) &&
                host.Controller.PhysicsWorld.HasLineOfSight(host.Actor,
                    new WorldVector(nativeOrigin.position.x, nativeOrigin.position.y, nativeOrigin.position.z),
                    new WorldVector(nativeTarget.position.x, nativeTarget.position.y, nativeTarget.position.z));
        }

        static void AssertFfaResult(MatchSnapshot frame, Peer scorer, Peer victim, long tick)
        {
            Assert.NotNull(frame.Progress.Result); Assert.IsTrue(frame.Progress.Result.Confirmed);
            Assert.AreEqual("Win", frame.Progress.Result.Outcome); Assert.AreEqual(tick, frame.Progress.Result.Tick);
            CollectionAssert.AreEqual(new[] { FfaPlayer(frame, scorer).Team }, frame.Progress.Result.WinningTeams);
            Assert.AreEqual(10, FfaPlayer(frame, scorer).Knockouts); Assert.AreEqual(0, FfaPlayer(frame, victim).Knockouts);
            Assert.AreEqual(9, FfaPlayer(frame, victim).RecoveryGeneration);
        }
        IEnumerator VerifyFfaDurableResult(Peer host, Peer victim, long tick)
        {
            int index = live.Clients.FindIndex(value => value.Credentials.PlayerId == victim.Actor);
            var service = new OnlineCheckpointService(live.Clients[index], live.Expected, host.Controller.Authority.IdentityHash);
            var head = service.Head(lifetime.Token);
            yield return Wait(head, "Read the exact public FFA terminal checkpoint head", 20);
            Assert.NotNull(head.Result.Head);
            var manifest = head.Result.Head;
            Assert.AreEqual(tick, manifest.Mark.Tick);
            var download = service.Download(manifest, lifetime.Token);
            yield return Wait(download, "Independently download the real FFA winning snapshot", 20);
            var imported = MatchSnapshotCodec.Import(download.Result, host.Controller.Frame.Binding, live.Mode, live.Course, live.Items, live.Rules);
            Assert.IsTrue(imported.Success, imported.Error); Assert.AreEqual(manifest.Mark.StateHash, imported.Hash);
            AssertFfaResult(imported.Snapshot, host, victim, tick);
            yield return Until(() => victim.Received.Values.Any(mark => mark.Same(manifest.Mark)) &&
                host.Acknowledged.TryGetValue(Key(victim.Actor, manifest.Mark.Sequence), out var ack) && ack.Same(manifest.Mark),
                "Acknowledge the exact independently downloaded FFA result mark", 25);
        }

        static WordboundClientContent CreateCompactTimeRun(string projectRoot)
        {
            string folder = Path.Combine(projectRoot, "Assets/_Project/Data/Config");
            string items = File.ReadAllText(Path.Combine(folder, "items.json"));
            string rules = File.ReadAllText(Path.Combine(folder, "rules.json"));
            var geometry = WordboundFlagshipContent.Create(ItemCatalogue.FromJson(items)).Geometry;
            var document = new CourseDocument {
                Schema = 2, Id = "native-full-timerun-" + Guid.NewGuid().ToString("N").Substring(0, 12),
                Title = "Compact native Time Run pipeline proof", SupportedModes = new List<string> { "TimeRun" }
            };
            document.Layers.Add(new CourseLayer { Id = "Ground" });
            document.Modules.Add(new CourseModule { Id = "Room", DefinitionId = "FlagshipRoom", LayerId = "Ground" });
            for (int seat = 0; seat < 4; seat++)
                document.Spawns.Add(new CourseSpawn {
                    Id = "Start" + seat, ModuleId = "Room", Position = new CoursePoint(-3000 + seat * 2000, 0, -4000)
                });
            document.RecoveryAnchors.Add(new CourseRecovery {
                Id = "SocketCheckpoint", ModuleId = "Room", Position = new CoursePoint(-2000, 0, -1000), RadiusMm = 800
            });
            document.RecoveryAnchors.Add(new CourseRecovery {
                Id = "DeliveryCheckpoint", ModuleId = "Room", Position = new CoursePoint(2000, 0, -2000), RadiusMm = 800
            });
            document.Supplies.Add(new CourseSupply {
                Id = "TableLetters", ModuleId = "Room", Position = new CoursePoint(-3000, 0, 1000), Letters = "TABLE"
            });
            document.Puzzles.Add(new CoursePuzzle {
                Id = FullPuzzle, ModuleId = "Room", PresetId = "PlaceObject", RecipeId = "TABLE", RequiredLetters = "TABLE",
                RecoverySupplyIds = new List<string> { "TableLetters" }, ConsumeLetters = true
            });
            document.Checkpoints.Add(new CourseCheckpoint {
                Id = "SocketDone", ModuleId = "Room", RecoveryAnchorId = "SocketCheckpoint", PuzzleIds = new List<string> { FullPuzzle }
            });
            document.Checkpoints.Add(new CourseCheckpoint {
                Id = "Delivery", ModuleId = "Room", RecoveryAnchorId = "DeliveryCheckpoint"
            });
            document.RouteSections.Add(new CourseRouteSection {
                Id = "Circuit", EntryModuleId = "Room", ExitModuleId = "Room",
                Alternatives = new List<CourseAlternative> { new CourseAlternative {
                    Id = "GroundRoute", CheckpointIds = new List<string> { "SocketDone", "Delivery" }
                } }
            });
            string[] stories = { "FindAddress", "WrongDelivery", "RestoreHome" };
            for (int lap = 1; lap <= 3; lap++)
            {
                document.Laps.Add(new CourseLap {
                    Number = lap, StartModuleId = "Room", FinishModuleId = "Room", NarrativeId = stories[lap - 1],
                    SectionIds = new List<string> { "Circuit" }
                });
                document.PhysicalSites.Add(new CoursePhysicalSite {
                    Lap = lap, PuzzleId = FullPuzzle, CheckpointId = "SocketDone", RecoverySupplyId = "TableLetters",
                    Position = new CoursePoint(0, 0, 2000), Target = new CoursePoint(0, 0, 2000),
                    SocketRadiusMm = 500, YawTolerance = 15, HoldTicks = 30
                });
            }
            var mode = ModeDefinition.For("TimeRun");
            var resolved = CourseResolver.Resolve(document, geometry,
                new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(mode)));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            return WordboundClientContent.FromAuthored(CourseJson.ExportDocument(document), CourseJson.ExportManifest(resolved.Manifest),
                geometry, items, rules, mode.Id, mode.Format.ToString(), resolved.Manifest.Seed);
        }

        IEnumerator CompleteCompactTimeRun(Peer host)
        {
            var carrierPeer = peers.Single(peer => peer != host);
            Assert.AreEqual("TimeRun", live.Mode.Id);
            Assert.AreEqual(3, live.Mode.Laps);
            Assert.AreEqual(1500, live.Mode.TimeLimitSeconds);
            Assert.IsFalse(live.PublicQueue);
            Assert.AreEqual("Workshop", live.Verified.Match.Content.Kind);
            Assert.AreEqual(1, live.Course.Geometry.Count);
            Assert.AreEqual(3, live.Course.PhysicalSites.Count);
            Assert.AreEqual(2, live.Verified.Roster.Count);
            Assert.IsTrue(live.Verified.Roster.All(seat => seat.TeamId == 0));
            Assert.AreEqual(1, live.Verified.Roster.Select(seat => seat.PartyId).Distinct().Count());
            string document = CourseJson.ExportDocument(live.Course.CopyDocument());
            var observedLaps = new Dictionary<int, long>();
            var observedObjectives = new HashSet<int>();
            var solvedItems = new HashSet<string>(StringComparer.Ordinal);
            long lastTick = host.Controller.Frame.Progress.Tick;
            int lastLap = 0, lastObjectives = 0;
            string observationError = "";
            void Observe(MatchSnapshot frame)
            {
                var team = frame.Progress.Teams.Single();
                if (frame.Progress.Tick < lastTick || team.Lap < lastLap || team.Lap > lastLap + 1 ||
                    team.OrderedObjectives < lastObjectives || team.OrderedObjectives > lastObjectives + 1)
                    observationError = "Canonical race progression skipped or regressed.";
                observedObjectives.Add(team.OrderedObjectives);
                if (team.Lap > lastLap)
                {
                    var player = frame.Bodies.Single(body => body.Kind == WorldBodyKind.Player && body.SourceId == carrierPeer.Actor);
                    if (team.KeepsakeCarrier != carrierPeer.Actor || player.Position.DistanceSquared(FullFinish) > .8f * .8f)
                        observationError = "Lap transition lacked the actual keepsake carrier at the authored finish.";
                    observedLaps[team.Lap] = frame.Progress.Tick;
                }
                lastTick = frame.Progress.Tick; lastLap = team.Lap; lastObjectives = team.OrderedObjectives;
            }
            Observe(host.Controller.Frame);
            host.Controller.FrameChanged += Observe;
            try
            {
                Assert.AreEqual(0, FullTeam(host).Lap);
                Assert.AreEqual(0, FullTeam(host).NextCheckpoint);
                Assert.IsNull(host.Controller.Frame.Progress.Result);
                yield return FullWalk(host, carrierPeer, () => Pose(host, "keepsake:0").Position, .8f, "Reach the native keepsake");
                yield return Command(host, carrierPeer, WordboundAction.TakeKeepsake, "keepsake:0");
                Assert.AreEqual(carrierPeer.Actor, FullTeam(host).KeepsakeCarrier);
                yield return FullWalk(host, carrierPeer, () => new WorldVector(-4, 0, -3), .25f, "Carry the keepsake clear of the workbench");

                for (int lap = 0; lap < 3; lap++)
                {
                    yield return FullWalk(host, carrierPeer, () => new WorldVector(-4, 0, -3), .25f,
                        "Carry the keepsake away from the preceding lap's finish");
                    var crafter = lap == 1 ? carrierPeer : host;
                    int seat = WordboundGameplayPlan.Seat(host.Controller.Frame.Binding, crafter.Actor);
                    Assert.AreEqual(lap, FullTeam(host).Lap);
                    Assert.AreEqual(0, FullTeam(host).NextCheckpoint);
                    Assert.IsNull(host.Controller.Frame.Progress.Result);
                    var source = live.Course.Supplies.Single(supply => supply.SourceId == "TableLetters" && supply.TeamIndex == 0);
                    yield return Until(() => host.Controller.Frame.Bodies.Count(body => body.Kind == WorldBodyKind.Tile &&
                        body.SourceId == source.Id) == 5, "Expose the current lap's real TABLE letters", 15);
                    var letters = host.Controller.Frame.Bodies.Where(body => body.Kind == WorldBodyKind.Tile && body.SourceId == source.Id)
                        .Select(body => body.Id).ToArray();
                    foreach (string id in letters)
                    {
                        yield return FullWalk(host, crafter, () => Pose(host, id).Position, .85f, "Reach each native letter through admitted input");
                        yield return Command(host, crafter, WordboundAction.CollectLetter, id);
                    }
                    Assert.IsTrue(Inventory(host, crafter).Letters.Equals(LetterBag.FromWord("TABLE")));
                    int firstItem = host.Controller.Frame.Economy.NextItemId;
                    yield return Command(host, crafter, WordboundAction.BeginCraft, value: "TABLE");
                    yield return Until(() => host.Controller.Frame.Economy.Items.Any(item => item.Id >= firstItem && item.Word == "TABLE" &&
                        item.Origin == ItemOrigin.Crafted && item.HolderId == seat && item.State == ItemState.Held),
                        "Complete the unchanged TABLE crafting timer", 20);
                    var table = host.Controller.Frame.Economy.Items.Single(item => item.Id >= firstItem && item.Word == "TABLE" &&
                        item.Origin == ItemOrigin.Crafted && item.HolderId == seat && item.State == ItemState.Held);
                    string tableId = "item:" + table.Id;
                    Assert.IsTrue(solvedItems.Add(tableId), "Every lap must craft a fresh consumed objective item.");
                    yield return FullWalk(host, crafter, () => FullSocketApproach, .08f, "Approach the authored socket without repositioning");
                    crafter.InteractionInput = new WordboundMovementInput { HasFacing = true, FacingYaw = 0 };
                    yield return Until(() => FullFacesSocket(host, crafter), "Turn toward the socket through admitted facing input", 10);
                    int slot = Array.IndexOf(Inventory(host, crafter).Slots, table.Id);
                    Assert.GreaterOrEqual(slot, 0);
                    yield return Command(host, crafter, WordboundAction.Deploy, slot: slot);
                    crafter.InteractionInput = default;
                    Assert.AreEqual(ItemState.Deployed, Item(host, tableId).State);
                    Assert.IsTrue(host.Controller.Bodies[tableId].Rigidbody.isKinematic);
                    int currentLap = lap;
                    yield return Until(() => FullMechanism(host, currentLap).Physical.Stage == MatchPhysicalStage.Complete,
                        "Hold the real native TABLE collider inside the socket", 15);
                    var mechanism = FullMechanism(host, lap);
                    Assert.AreEqual(tableId, mechanism.ItemEntityId);
                    Assert.GreaterOrEqual(mechanism.ProgressTicks, 30);
                    Assert.AreEqual(live.Course.PhysicalSites.Single(site => site.Lap == lap + 1).Hash, mechanism.Physical.SiteHash);
                    yield return Command(host, crafter, WordboundAction.UsePuzzle, FullPuzzle, value: tableId);
                    Assert.IsTrue(FullMechanism(host, lap).Solved);
                    Assert.AreEqual(tableId, FullMechanism(host, lap).ItemEntityId);
                    var consumedFrame = host.Controller.Frame;
                    Assert.IsTrue(consumedFrame.Economy.Items.Single(item => item.Id == table.Id).IsGone);
                    Assert.IsFalse(consumedFrame.Actors.Any(actor => actor.Attack != null && actor.Attack.HitBodyIds.Contains(tableId)));
                    Assert.IsFalse(consumedFrame.ThrownFlights.Any(flight => flight.ItemBodyId == tableId || flight.HitBodyIds.Contains(tableId)));
                    Assert.IsFalse(consumedFrame.Mechanisms.Any(state => state.Physical.AuxiliaryBodyId == tableId ||
                        !state.Solved && state.ItemEntityId == tableId), "This consumed table must have no live body-retention reference.");
                    Assert.IsFalse(consumedFrame.Bodies.Any(body => body.Id == tableId), "The solved-only ledger must not retain a physical table body.");
                    Assert.IsFalse(host.Controller.Bodies.ContainsKey(tableId), "The command-bearing host frame must reconcile the consumed native body.");
                    yield return FullWalk(host, crafter, () => FullCheckpoint, .25f, "Cross the supported ordered checkpoint");
                    yield return Until(() => FullTeam(host).NextCheckpoint == 1, "Observe the native first checkpoint", 10);

                    yield return FullWalk(host, host, () => FullFinish, .25f, "Reach the finish without the keepsake carrier");
                    yield return Until(() => FullTeam(host).NextCheckpoint == 2, "Observe the native delivery checkpoint", 10);
                    long waitFrom = host.Controller.Frame.Progress.Tick;
                    yield return Until(() => host.Controller.Frame.Progress.Tick >= waitFrom + 60,
                        "Keep the non-carrier at the finish for sixty ordinary ticks", 10);
                    Assert.AreEqual(lap, FullTeam(host).Lap, "A teammate without the keepsake must not complete the lap.");
                    Assert.IsNull(host.Controller.Frame.Progress.Result);
                    yield return FullWalk(host, host, () => FullHostParking, .25f, "Leave the finish clear for the keepsake carrier");
                    yield return FullWalk(host, carrierPeer, () => FullFinish, .4f,
                        "Deliver the keepsake through native movement and support", () => FullTeam(host).Lap == currentLap + 1);
                    yield return Until(() => FullTeam(host).Lap == currentLap + 1, "Observe the natural keepsake lap transition", 10);
                    yield return CommittedState(host, frame => frame.Progress.Teams.Single().Lap == currentLap + 1,
                        "Commit and acknowledge the completed lap on both authenticated peers");
                    Assert.IsEmpty(observationError);
                    Debug.Log("WORDBOUND_COMPACT_LAP_OBSERVED: lap=" + (lap + 1) + "; tick=" + observedLaps[lap + 1] +
                        "; orderedObjectives=" + FullTeam(host).OrderedObjectives + "; nativeSocket=true; nativeCarrierFinish=true; cleanupPending=true");
                }

                yield return Until(() => peers.All(peer => peer.Controller.Frame.Progress.Result?.Confirmed == true),
                    "Confirm the natural terminal result on host and replica", 25);
                var result = host.Controller.Frame.Progress.Result;
                CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, observedLaps.Keys);
                CollectionAssert.AreEquivalent(Enumerable.Range(0, 10), observedObjectives);
                Assert.AreEqual(9, FullTeam(host).OrderedObjectives);
                Assert.AreEqual(3, solvedItems.Count);
                foreach (var peer in peers) AssertFullResult(peer.Controller.Frame, result.Tick);
                yield return VerifyFullDurableResult(host, carrierPeer, result.Tick);
                Assert.IsEmpty(observationError);
                Assert.AreEqual(document, CourseJson.ExportDocument(live.Course.CopyDocument()));
                Assert.Greater(host.PhysicalSteps, 0); Assert.Greater(host.PhysicalObservations, 0);
                Assert.Greater(host.MovingInputReads, 0); Assert.Greater(carrierPeer.MovingInputReads, 0);
                Assert.AreEqual(0, carrierPeer.Advanced);
                Debug.Log("WORDBOUND_COMPACT_FULL_MATCH_OBSERVED: TimeRun; private authored map; two authenticated scripted clients; " +
                    "three fresh crafted/consumed TABLE objectives; three native carrier deliveries; confirmed Win on both peers; " +
                    "exact durable checkpoint downloaded and imported; no traversal teleports or direct progress injection; " +
                    "not public flagship or human play acceptance; cleanup pending.");
            }
            finally
            {
                host.Controller.FrameChanged -= Observe;
                foreach (var peer in peers) { peer.ScriptedMovement = null; peer.InteractionInput = default; }
            }
        }

        IEnumerator FullWalk(Peer host, Peer actor, Func<WorldVector> target, float radius, string operation,
            Func<bool> delivered = null)
        {
            int samples = 0;
            float minimumInput = float.PositiveInfinity, maximumInput = 0, lastInput = 0;
            actor.ScriptedMovement = () => {
                samples++; lastInput = 0;
                if (delivered?.Invoke() == true) return default;
                var body = host.Controller.Bodies["player:" + WordboundGameplayPlan.Seat(host.Controller.Frame.Binding, actor.Actor)];
                var at = body.Rigidbody.position; var goal = target();
                var delta = new Vector2(goal.X - at.x, goal.Z - at.z);
                if (delta.magnitude <= radius) return default;
                var value = delta.normalized * Mathf.Clamp((delta.magnitude - radius * .5f) * .07f, .06f, .22f);
                lastInput = value.magnitude;
                minimumInput = Mathf.Min(minimumInput, lastInput); maximumInput = Mathf.Max(maximumInput, lastInput);
                return new WordboundMovementInput { MoveX = value.x, MoveZ = value.y };
            };
            try
            {
                yield return Until(() => delivered?.Invoke() == true || HorizontalDistance(PlayerPose(host, actor).Position, target()) <= radius,
                    operation, 35);
            }
            finally
            {
                actor.ScriptedMovement = null; actor.InteractionInput = default;
                try
                {
                    var body = host.Controller.Bodies["player:" + WordboundGameplayPlan.Seat(host.Controller.Frame.Binding, actor.Actor)].Rigidbody;
                    var position = body.position; var velocity = body.linearVelocity; var goal = target();
                    float distance = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(goal.X, goal.Z));
                    bool canMove = host.Controller.PhysicsWorld.CanMove(actor.Actor, out float moveScale);
                    Debug.Log(FormattableString.Invariant($"WORDBOUND_COMPACT_WALK: operation={operation}; phase=steering-end; tick={host.Controller.Frame.Progress.Tick}; ") +
                        FormattableString.Invariant($"hostPosition={position.x:F6}/{position.y:F6}/{position.z:F6}; hostVelocity={velocity.x:F6}/{velocity.y:F6}/{velocity.z:F6}; ") +
                        FormattableString.Invariant($"target={goal.X:F3}/{goal.Y:F3}/{goal.Z:F3}; distance={distance:F6}; radius={radius:F3}; inside={distance <= radius}; ") +
                        FormattableString.Invariant($"delivered={delivered?.Invoke() == true}; samples={samples}; nonzeroObserved={maximumInput > 0}; minNonzeroInput={(float.IsPositiveInfinity(minimumInput) ? 0 : minimumInput):F6}; ") +
                        FormattableString.Invariant($"maxInput={maximumInput:F6}; lastInput={lastInput:F6}; canMove={canMove}; moveScale={moveScale:F6}; kinematic={body.isKinematic}; ") +
                        FormattableString.Invariant($"paused={host.Controller.IsSimulationPaused}; lifetimeCancelled={lifetime.IsCancellationRequested}; poseSource=host-native; inputs=requested-not-delivery-proof"));
                }
                catch (Exception) { Debug.Log("WORDBOUND_COMPACT_WALK_UNAVAILABLE: observation-failed"); }
            }
            if (host.Controller.Frame.Progress.Result == null)
                yield return Until(() => {
                    var velocity = PlayerPose(host, actor).Velocity;
                    return velocity.X * velocity.X + velocity.Z * velocity.Z < .01f;
                }, "Stop through normal player motor and friction", 10);
            Assert.IsTrue(delivered?.Invoke() == true ||
                HorizontalDistance(PlayerPose(host, actor).Position, target()) <= radius + .2f,
                "Ordinary input must settle near the requested point or trigger the actual carrier delivery sensor.");
            Assert.AreEqual(0, host.Controller.Frame.Progress.Players.Single(player => player.Id == actor.Actor).Knockouts);
        }

        static float HorizontalDistance(WorldVector a, WorldVector b) => new Vector2(a.X - b.X, a.Z - b.Z).magnitude;
        static TeamProgress FullTeam(Peer host) => host.Controller.Frame.Progress.Teams.Single();
        static MatchMechanismState FullMechanism(Peer host, int lap) => host.Controller.Frame.Mechanisms.Single(
            mechanism => mechanism.Team == 0 && mechanism.Lap == lap && mechanism.PuzzleId == FullPuzzle);
        static bool FullFacesSocket(Peer host, Peer actor)
        {
            var body = host.Controller.Bodies["player:" + WordboundGameplayPlan.Seat(host.Controller.Frame.Binding, actor.Actor)];
            return Vector3.Dot(body.Rigidbody.rotation * Vector3.forward, Vector3.forward) > .999f &&
                new Vector2(body.Rigidbody.linearVelocity.x, body.Rigidbody.linearVelocity.z).sqrMagnitude < .01f;
        }
        static void AssertFullResult(MatchSnapshot frame, long resultTick)
        {
            Assert.NotNull(frame.Progress.Result);
            Assert.AreEqual("Win", frame.Progress.Result.Outcome);
            CollectionAssert.AreEqual(new[] { 0 }, frame.Progress.Result.WinningTeams);
            Assert.AreEqual(resultTick, frame.Progress.Result.Tick);
            Assert.IsTrue(frame.Progress.Result.Confirmed);
            Assert.AreEqual(3, frame.Progress.Teams.Single().Lap);
            Assert.IsTrue(frame.Progress.Teams.Single().Finished);
            Assert.AreEqual(9, frame.Progress.Teams.Single().OrderedObjectives);
            Assert.AreEqual(3, frame.Mechanisms.Count(mechanism => mechanism.Solved));
        }
        IEnumerator VerifyFullDurableResult(Peer host, Peer client, long tick)
        {
            int index = live.Clients.FindIndex(value => value.Credentials.PlayerId == client.Actor);
            var service = new OnlineCheckpointService(live.Clients[index], live.Expected, host.Controller.Authority.IdentityHash);
            var head = service.Head(lifetime.Token);
            yield return Wait(head, "Read the real durable terminal checkpoint head", 20);
            Assert.NotNull(head.Result.Head);
            var manifest = head.Result.Head;
            Assert.GreaterOrEqual(manifest.Mark.Tick, tick);
            var download = service.Download(manifest, lifetime.Token);
            yield return Wait(download, "Independently download the durable terminal checkpoint", 20);
            var imported = MatchSnapshotCodec.Import(download.Result, host.Controller.Frame.Binding, live.Mode, live.Course, live.Items, live.Rules);
            Assert.IsTrue(imported.Success, imported.Error);
            Assert.AreEqual(manifest.Mark.StateHash, imported.Hash);
            AssertFullResult(imported.Snapshot, tick);
            yield return Until(() => client.Received.Values.Any(mark => mark.Same(manifest.Mark)) &&
                host.Acknowledged.TryGetValue(Key(client.Actor, manifest.Mark.Sequence), out var ack) && ack.Same(manifest.Mark),
                "Acknowledge the exact downloaded terminal checkpoint on the replica", 25);
        }
    }
}
