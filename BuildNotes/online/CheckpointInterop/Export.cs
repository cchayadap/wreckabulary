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
        Require(MatchSnapshot.Schema == 3 && MatchSnapshotCodec.CanonicalVersion == 3 &&
            MatchSnapshotCodec.ProtocolVersion == "wordbound-v3", "Use the actual green schema3 Rules assembly.");
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
            a.EquippedSlot, a.Attack, a.StaggerImmuneUntilTick, a.Defense)).ToArray();
        var final = new MatchSnapshot(snapshot.Binding, snapshot.Epoch, snapshot.Sequence, snapshot.NextWorldEntityId,
            snapshot.Progress, snapshot.Economy, snapshot.Commands, actors, snapshot.Bodies, snapshot.Resources,
            snapshot.Mechanisms, snapshot.RandomStreams, snapshot.NarrativeFlags, snapshot.ThrownFlights, snapshot.Practice);
        string json = MatchSnapshotCodec.Export(final);
        var imported = MatchSnapshotCodec.Import(json, binding, mode, course, items, rules);
        Require(imported.Success, imported.Error);
        Require(MatchSnapshotCodec.Export(imported.Snapshot) == json, "Canonical C# roundtrip must be byte exact.");
        Require(!MatchSnapshotCodec.Import(json.Replace("\"physical\":", "\"legacyPhysical\":"), binding, mode, course, items, rules).Success,
            "The current codec must reject the missing physical field.");
        Require(!MatchSnapshotCodec.Import(json.Replace("\"thrownFlights\":", "\"legacyFlights\":"), binding, mode, course, items, rules).Success,
            "The current codec must reject the missing thrown-flight field.");
        Require(!MatchSnapshotCodec.Import(json.Replace("\"practice\":", "\"legacyPractice\":"), binding, mode, course, items, rules).Success,
            "The current codec must reject the missing practice field.");
        Require(snapshot.Practice.SiteHash == "" && snapshot.Practice.SelectedTick == -1,
            "A race snapshot must retain explicit empty practice state.");
        var data = PacketData(json, binding);
        var practiceData = ExportPractice(geometry, itemJson, ruleJson, items);
        var defenseData = ExportDefense(geometry, itemJson, ruleJson, items);
        File.WriteAllText(target, "export const schema3Fixture = " + OnlineJson.Encode(data) + ";\n" +
            "export const schema3PracticeFixture = " + OnlineJson.Encode(practiceData) + ";\n" +
            "export const schema3DefenseFixture = " + OnlineJson.Encode(defenseData) + ";\n");
        Console.WriteLine("REAL_SCHEMA3_CSHARP_EXPORT_AND_IMPORT_PASS bytes=" + data["rawLength"] +
            " physical=Complete flightHits=1 tick=" + snapshot.Progress.Tick + " practice=Selected defense=Raised");
    }

    static Dictionary<string, object> ExportPractice(CourseCatalogue geometry, string itemJson,
        string ruleJson, ItemCatalogue items)
    {
        var mode = ModeDefinition.For("Freestyle");
        var document = CourseTemplates.RaceCircuit();
        document.Schema = 2;
        document.SupportedModes.Add("Freestyle");
        foreach (var connection in document.Connections) connection.GatePuzzleId = "";
        document.Puzzles.Single(p => p.Id == "BedBridge").PresetId = "PlaceObject";
        document.PhysicalSites.Add(new CoursePhysicalSite {
            Lap = 1, PuzzleId = "BedBridge", CheckpointId = "Begin", RecoverySupplyId = "BedSupply",
            Position = new CoursePoint(0, 0, 0)
        });
        var resolution = CourseResolver.Resolve(document, geometry,
            new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(mode)));
        Require(resolution.Success, "The authored Freestyle practice course must resolve.");
        var course = resolution.Manifest;
        var rules = mode.CreateGameplayRules(RuleBook.FromJson(ruleJson).For("Dibs"));
        var binding = new MatchBinding("44444444-4444-4444-a444-444444444444", MatchSnapshotCodec.ProtocolVersion,
            mode.Hash, course.SourceHash, course.Hash, new string('e', 64),
            OnlineJson.Hash(new Dictionary<string, object> {
                ["geometry"] = geometry.Hash, ["items"] = WordboundIdentity.Sha256(itemJson),
                ["rules"] = WordboundIdentity.Sha256(ruleJson), ["course"] = course.Hash
            }),
            new[] { new WordboundSeat(Actor, 0, 0), new WordboundSeat(Other, 0, 1) });
        var match = new AuthoritativeMatch(binding, mode, course, geometry, items, rules, new World());
        var site = course.PhysicalSites.Single();
        var player = match.Capture(1).Bodies.Single(b => b.Kind == WorldBodyKind.Player && b.SourceId == Actor);
        var position = new WorldVector(site.Position.X / 1000f, site.Position.Y / 1000f, site.Position.Z / 1000f);
        var result = match.Advance(17, new[] {
            new AuthenticatedCommand(Actor, new PlayerCommand(1, 0, WordboundAction.PracticePuzzle, site.Hash))
        }, new[] { player.At(position) });
        Require(result.Advanced && result.Receipts.Single().Effect.Code == "Ok", "The real practice selection must succeed.");
        var snapshot = match.Capture(1);
        Require(snapshot.Practice.SiteHash == site.Hash && snapshot.Practice.SelectedTick == 1 &&
            snapshot.Progress.Tick == 1, "Practice selection must retain its exact causal frontier.");
        Require(snapshot.Mechanisms.Count == 1 && snapshot.Mechanisms[0].Physical.SiteHash == site.Hash,
            "Practice must refer to the actual authored mechanism.");
        string json = MatchSnapshotCodec.Export(snapshot);
        var imported = MatchSnapshotCodec.Import(json, binding, mode, course, items, rules);
        Require(imported.Success, imported.Error);
        Require(MatchSnapshotCodec.Export(imported.Snapshot) == json, "Practice canonical roundtrip must be exact.");
        Require(!MatchSnapshotCodec.Import(json.Replace("\"practice\":", "\"legacyPractice\":"),
            binding, mode, course, items, rules).Success, "Missing practice state must fail strict import.");
        return PacketData(json, binding);
    }

    static Dictionary<string, object> ExportDefense(CourseCatalogue geometry, string itemJson,
        string ruleJson, ItemCatalogue items)
    {
        var mode = ModeDefinition.For("FreeForAll");
        var resolution = CourseResolver.Resolve(CourseTemplates.CombatArena(), geometry,
            new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(mode)));
        Require(resolution.Success, "The installed arena must resolve for defense export.");
        var course = resolution.Manifest;
        var rules = mode.CreateGameplayRules(RuleBook.FromJson(ruleJson).For("Dibs"));
        var binding = new MatchBinding("55555555-5555-4555-a555-555555555555", MatchSnapshotCodec.ProtocolVersion,
            mode.Hash, course.SourceHash, course.Hash, new string('e', 64),
            OnlineJson.Hash(new Dictionary<string, object> {
                ["geometry"] = geometry.Hash, ["items"] = WordboundIdentity.Sha256(itemJson),
                ["rules"] = WordboundIdentity.Sha256(ruleJson), ["course"] = course.Hash
            }), new[] { new WordboundSeat(Actor, 0, 0), new WordboundSeat(Other, 1, 0) });
        var match = new AuthoritativeMatch(binding, mode, course, geometry, items, rules, new World());
        long sequence = 0;
        MatchSnapshot State() => match.Capture(1);
        void Step(WordboundAction? action = null, string targetId = "", int slot = -1,
            string value = "", WorldVector? position = null)
        {
            if (match.Tick % 50 == 0)
            {
                var stamp = match.PublishSnapshot(out string checkpoint);
                Require(MatchSnapshotCodec.Import(checkpoint, binding, mode, course, items, rules).Success,
                    "Defense intermediate snapshot must strictly import.");
                Require(match.Acknowledge(stamp), "Defense acknowledgement must match.");
            }
            var player = State().Bodies.Single(b => b.Kind == WorldBodyKind.Player && b.SourceId == Actor);
            var result = match.Advance(State().Progress.WallClockMs + 17,
                action.HasValue ? new[] { new AuthenticatedCommand(Actor,
                    new PlayerCommand(++sequence, match.Epoch, action.Value, targetId, slot: slot, value: value)) } : null,
                position.HasValue ? new[] { player.At(position.Value) } : null);
            Require(result.Advanced, "Defense export cannot silently pause.");
            if (action.HasValue) Require(result.Receipts.Single().Effect.Code == "Ok", "Defense command must succeed.");
        }
        foreach (char letter in "PLATE")
        {
            var state = State();
            var tiles = state.Economy.Tiles.ToDictionary(t => "tile:" + t.Key, t => t.Value);
            var tile = state.Bodies.First(b => b.Kind == WorldBodyKind.Tile && tiles[b.Id] == letter);
            Step(WordboundAction.CollectLetter, tile.Id, position: tile.Position);
        }
        Step(WordboundAction.BeginCraft, value: "PLATE");
        int ticks = (int)Math.Ceiling(rules.CraftSeconds(items.Get("PLATE")) * ModeDefinition.TickRate) + 2;
        for (int i = 0; i < ticks; i++) Step();
        Step(WordboundAction.Equip, slot: 0);
        Step(WordboundAction.Block, value: "Hold");
        for (int i = 0; i < WordboundDefensePolicy.RaiseTicks(items.Get("PLATE").Shield); i++) Step();
        var snapshot = State();
        var defense = snapshot.Actors.Single(a => a.PlayerId == Actor).Defense;
        Require(WordboundDefensePolicy.Raised(defense, items.Get("PLATE").Shield, snapshot.Progress.Tick),
            "The real crafted PLATE must retain active raised defense.");
        string json = MatchSnapshotCodec.Export(snapshot);
        var imported = MatchSnapshotCodec.Import(json, binding, mode, course, items, rules);
        Require(imported.Success, imported.Error);
        Require(MatchSnapshotCodec.Export(imported.Snapshot) == json, "Defense canonical roundtrip must be exact.");
        Require(!MatchSnapshotCodec.Import(json.Replace("\"defense\":", "\"legacyDefense\":"),
            binding, mode, course, items, rules).Success, "Missing defense state must fail strict import.");
        return PacketData(json, binding);
    }

    static Dictionary<string, object> PacketData(string json, MatchBinding binding)
    {
        var packet = OnlineSnapshotPacket.Compress(json);
        return new Dictionary<string, object> {
            ["json"] = json, ["identityHash"] = binding.Hash, ["wireHash"] = packet.WireHash,
            ["rawLength"] = packet.RawLength, ["totalBytes"] = packet.Length,
            ["chunks"] = Enumerable.Range(0, packet.ChunkCount).Select(i => (object)OnlineJson.Base64Url(packet.Chunk(i))).ToArray()
        };
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
