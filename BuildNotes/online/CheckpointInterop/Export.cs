using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

sealed class Export
{
    const string Actor = "22222222-2222-4222-a222-222222222222";
    const string Other = "33333333-3333-4333-a333-333333333333";
    sealed class World : IWordboundWorldQuery, IWordboundReleaseQuery
    {
        public bool HasLineOfSight(string id, WorldVector from, WorldVector to) => true;
        public bool CanDeploy(string id, string recipe, WorldVector position) => true;
        public bool PuzzleSatisfied(string id, ResolvedCoursePuzzle puzzle, WorldBodyState item) => false;
        public bool TryRelease(WorldBodyState actor, WorldBodyState item, bool throwing, out WorldVector at)
        {
            at = new WorldVector(actor.Position.X, actor.Position.Y + (throwing ? .75f : .08f), actor.Position.Z + 1.5f);
            return true;
        }
    }

    static void Main(string[] args)
    {
        string project = args[0], target = args[1];
        var mode = ModeDefinition.For("BrawlRace");
        var geometry = CourseTemplates.ProofCatalogue();
        var document = CourseTemplates.RaceCircuit();
        document.Puzzles.Single(p => p.Id == "BedBridge").PresetId = "PlaceObject";
        document.PhysicalSites.Add(new CoursePhysicalSite {
            Lap = 1, PuzzleId = "BedBridge", CheckpointId = "Begin", RecoverySupplyId = "BedSupply",
            Position = new CoursePoint(0, 0, 0)
        });
        document.Supplies.Add(new CourseSupply {
            Id = "BallSupply", ModuleId = "Start", Position = new CoursePoint(0, 0, -4000), Letters = "BALL"
        });
        var resolution = CourseResolver.Resolve(document, geometry,
            new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(mode)));
        Require(resolution.Success, "Authored interop course must resolve.");
        var course = resolution.Manifest;
        string itemJson = File.ReadAllText(Path.Combine(project, "Assets/_Project/Data/Config/items.json"));
        string ruleJson = File.ReadAllText(Path.Combine(project, "Assets/_Project/Data/Config/rules.json"));
        var items = ItemCatalogue.FromJson(itemJson);
        var rules = mode.CreateGameplayRules(RuleBook.FromJson(ruleJson).For("Dibs"));
        var binding = new MatchBinding("11111111-1111-4111-a111-111111111111", MatchSnapshotCodec.ProtocolVersion,
            mode.Hash, course.SourceHash, course.Hash, new string('e', 64),
            OnlineJson.Hash(new Dictionary<string, object> {
                ["geometry"] = geometry.Hash, ["items"] = WordboundIdentity.Sha256(itemJson),
                ["rules"] = WordboundIdentity.Sha256(ruleJson), ["course"] = course.Hash
            }),
            new[] { new WordboundSeat(Actor, 0, 0), new WordboundSeat(Other, 1, 0) });
        var match = new AuthoritativeMatch(binding, mode, course, geometry, items, rules, new World());
        long sequence = 0;
        MatchSnapshot State() => match.Capture(1);
        WorldBodyState Pose(string actor, WorldVector at) => State().Bodies
            .Single(b => b.Kind == WorldBodyKind.Player && b.SourceId == actor).At(at);
        AuthenticatedCommand Command(WordboundAction action, string targetId = "", int slot = -1, string value = "") =>
            new AuthenticatedCommand(Actor, new PlayerCommand(++sequence, match.Epoch, action, targetId, slot: slot, value: value));
        void Ack()
        {
            var stamp = match.PublishSnapshot(out string checkpoint);
            Require(MatchSnapshotCodec.Import(checkpoint, binding, mode, course, items, rules).Success, "Intermediate snapshot must import.");
            Require(match.Acknowledge(stamp), "Intermediate acknowledgement must match.");
        }
        void Step(AuthenticatedCommand command = null, IEnumerable<WorldBodyState> poses = null,
            IEnumerable<HostMechanismObservation> mechanisms = null, IEnumerable<HostThrownContact> contacts = null)
        {
            if (match.Tick % 50 == 0) Ack();
            var result = match.Advance(State().Progress.WallClockMs + 17,
                command == null ? null : new[] { command }, poses, hostMechanisms: mechanisms, hostThrownContacts: contacts);
            Require(result.Advanced, "Interop simulation cannot silently pause.");
            if (command != null) Require(result.Receipts.Single().Effect.Code == "Ok", "Interop command must succeed.");
        }
        void Wait(int ticks) { for (int i = 0; i < ticks; i++) Step(); }
        string Craft(string word, string sourceId)
        {
            string supply = course.Supplies.Single(s => s.SourceId == sourceId && s.TeamIndex == 0).Id;
            foreach (var tile in State().Bodies.Where(b => b.SourceId == supply).ToArray())
                Step(Command(WordboundAction.CollectLetter, tile.Id), new[] { Pose(Actor, tile.Position) });
            Step(Command(WordboundAction.BeginCraft, value: word));
            Wait((int)Math.Ceiling(rules.CraftSeconds(items.Get(word)) * ModeDefinition.TickRate) + 2);
            return "item:" + State().Economy.Items.Single(i => i.Word == word && i.Origin == ItemOrigin.Crafted).Id;
        }

        Wait(121);
        string bed = Craft("BED", "BedSupply");
        Step(Command(WordboundAction.Deploy, slot: 0), new[] { Pose(Actor, new WorldVector(0, 0, -1.5f)) });
        var site = course.PhysicalSites.Single();
        long phase = match.Tick + 1;
        for (int i = 0; i < site.HoldTicks; i++)
        {
            bool complete = i == site.HoldTicks - 1;
            var evidence = new MatchPhysicalEvidence(complete ? MatchPhysicalStage.Complete : MatchPhysicalStage.Armed,
                Actor, "", complete ? match.Tick + 1 : phase, site.Hash);
            Step(mechanisms: new[] { new HostMechanismObservation(site.PuzzleId, 0, 0, true, phase, i + 1, bed, evidence) });
        }
        string ball = Craft("BALL", "BallSupply");
        Step(Command(WordboundAction.Throw, slot: 0),
            new[] { Pose(Actor, default), Pose(Other, new WorldVector(0, 0, 2)) });
        var flight = State().ThrownFlights.Single();
        Require(flight.ItemBodyId == ball, "The flight must be the crafted BALL.");
        Step(contacts: new[] { new HostThrownContact(ball, flight.LaunchRevision, "player:1",
            State().Bodies.Single(b => b.Id == ball).Position, items.Get("BALL").Thrown.Speed, match.Tick + 1) });

        var snapshot = State();
        Require(snapshot.Mechanisms.Any(m => m.Physical.Stage == MatchPhysicalStage.Complete), "Physical evidence cannot be empty.");
        Require(snapshot.ThrownFlights.Single().HitBodyIds.SequenceEqual(new[] { "player:1" }), "The flight must retain a real resolved contact.");
        Require(snapshot.Actors.Single(a => a.PlayerId == Other).Health < rules.MaxHealth, "The contact must apply catalogue damage.");
        var actors = snapshot.Actors.Select(a => new MatchActorState(a.PlayerId, a.Health, a.Stamina,
            a.NextAttackTick, a.StunnedUntilTick, a.DodgeUntilTick, a.LastInputSequence, "coat\nnavy\t\b\u0001 🧸",
            a.EquippedSlot, a.Attack, a.StaggerImmuneUntilTick)).ToArray();
        var final = new MatchSnapshot(snapshot.Binding, snapshot.Epoch, snapshot.Sequence, snapshot.NextWorldEntityId,
            snapshot.Progress, snapshot.Economy, snapshot.Commands, actors, snapshot.Bodies, snapshot.Resources,
            snapshot.Mechanisms, snapshot.RandomStreams, snapshot.NarrativeFlags, snapshot.ThrownFlights);
        string json = MatchSnapshotCodec.Export(final);
        var imported = MatchSnapshotCodec.Import(json, binding, mode, course, items, rules);
        Require(imported.Success, imported.Error);
        Require(MatchSnapshotCodec.Export(imported.Snapshot) == json, "Canonical C# roundtrip must be byte exact.");
        Require(!MatchSnapshotCodec.Import(json.Replace("\"physical\":", "\"legacyPhysical\":"), binding, mode, course, items, rules).Success,
            "The current codec must reject the missing physical field.");
        Require(!MatchSnapshotCodec.Import(json.Replace("\"thrownFlights\":", "\"legacyFlights\":"), binding, mode, course, items, rules).Success,
            "The current codec must reject the missing thrown-flight field.");
        var packet = OnlineSnapshotPacket.Compress(json);
        var data = new Dictionary<string, object> {
            ["json"] = json, ["identityHash"] = binding.Hash, ["wireHash"] = packet.WireHash,
            ["rawLength"] = packet.RawLength, ["totalBytes"] = packet.Length,
            ["chunks"] = Enumerable.Range(0, packet.ChunkCount).Select(i => (object)OnlineJson.Base64Url(packet.Chunk(i))).ToArray()
        };
        File.WriteAllText(target, "export const schema2Fixture = " + OnlineJson.Encode(data) + ";\n");
        Console.WriteLine("REAL_SCHEMA2_CSHARP_EXPORT_AND_IMPORT_PASS bytes=" + packet.RawLength +
            " chunks=" + packet.ChunkCount + " physical=Complete flightHits=1 tick=" + snapshot.Progress.Tick);
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
