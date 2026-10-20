using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class MatchSnapshotImport
    {
        public bool Success => Snapshot != null && Error == "";
        public MatchSnapshot Snapshot { get; internal set; }
        public string Hash { get; internal set; } = "";
        public string Error { get; internal set; } = "";
    }

    public static class MatchSnapshotCodec
    {
        public const int CanonicalVersion = 2;
        public const string ProtocolVersion = "wordbound-v2";
        public const int MaximumBytes = 8388608;
        static readonly Regex Signed = new Regex(@"\A(?:0|[1-9][0-9]*|-1)\z");
        static readonly Regex Unsigned = new Regex(@"\A(?:0|[1-9][0-9]*)\z");

        public static string Export(MatchSnapshot snapshot)
        {
            var envelope = Envelope(snapshot);
            envelope["hash"] = CourseJson.Sha256(CourseJson.Encode(envelope));
            string json = CourseJson.Encode(envelope);
            CourseJson.Preflight(json, MaximumBytes);
            return json;
        }

        public static string ComputeHash(MatchSnapshot snapshot)
        {
            string canonical = CourseJson.Encode(Envelope(snapshot));
            CourseJson.Preflight(canonical, MaximumBytes);
            return CourseJson.Sha256(canonical);
        }

        public static MatchSnapshotImport Import(string json, MatchBinding expectedBinding, ModeDefinition mode,
            ResolvedCourse course, ItemCatalogue catalogue, GameRules rules)
        {
            var result = new MatchSnapshotImport();
            try
            {
                CourseJson.Preflight(json, MaximumBytes);
                var root = Json.Parse(json, "match-snapshot.json");
                Keys(root, "format", "schema", "canonicalVersion", "hash", "payload");
                if (Text(root["format"]) != "wreckabulary.match-snapshot" || Int(root["schema"]) != MatchSnapshot.Schema ||
                    Int(root["canonicalVersion"]) != CanonicalVersion || !WordboundIdentity.IsHash(Text(root["hash"])))
                    throw root.Error("unsupported snapshot format or version");
                var snapshot = ReadSnapshot(root["payload"]);
                string hash = ComputeHash(snapshot);
                if (hash != Text(root["hash"])) throw root.Error("snapshot hash does not match canonical state");
                string error = MatchStateValidation.Validate(snapshot, expectedBinding, mode, course, catalogue, rules);
                if (error != "") throw root.Error(error);
                result.Snapshot = snapshot; result.Hash = hash;
            }
            catch (Exception e) when (e is FormatException || e is ArgumentException || e is OverflowException ||
                e is InvalidOperationException || e is KeyNotFoundException)
            { result.Error = e.Message; }
            return result;
        }

        static Dictionary<string, object> Envelope(MatchSnapshot s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            return Map("format", "wreckabulary.match-snapshot", "schema", MatchSnapshot.Schema, "canonicalVersion", CanonicalVersion, "payload", WriteSnapshot(s));
        }
        static Dictionary<string, object> Map(params object[] fields)
        {
            var map = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < fields.Length; i += 2) map.Add((string)fields[i], fields[i + 1]);
            return map;
        }
        static object[] Optional(object value) => value == null ? System.Array.Empty<object>() : new[] { value };
        static string Long(long value) => value.ToString(CultureInfo.InvariantCulture);
        static string ULong(ulong value) => value.ToString(CultureInfo.InvariantCulture);
        static string Float(float value)
        {
            if (!WorldVector.Finite(value)) throw new ArgumentException("Snapshot floats must be finite.");
            return unchecked((uint)BitConverter.SingleToInt32Bits(value)).ToString("x8", CultureInfo.InvariantCulture);
        }
        static string Double(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentException("Snapshot doubles must be finite.");
            return unchecked((ulong)BitConverter.DoubleToInt64Bits(value)).ToString("x16", CultureInfo.InvariantCulture);
        }
        static object Vector(WorldVector v) => new object[] { Float(v.X), Float(v.Y), Float(v.Z) };

        static object WriteSnapshot(MatchSnapshot s) => Map(
            "binding", WriteBinding(s.Binding), "epoch", Long(s.Epoch), "sequence", Long(s.Sequence), "nextWorldEntityId", Long(s.NextWorldEntityId),
            "progress", WriteProgress(s.Progress), "economy", WriteEconomy(s.Economy), "commands", WriteCommands(s.Commands),
            "actors", s.Actors.Select(a => (object)Map("playerId", a.PlayerId, "health", Float(a.Health), "stamina", Float(a.Stamina),
                "nextAttackTick", Long(a.NextAttackTick), "stunnedUntilTick", Long(a.StunnedUntilTick), "dodgeUntilTick", Long(a.DodgeUntilTick),
                "lastInputSequence", Long(a.LastInputSequence), "appearance", a.Appearance,
                "equippedSlot", a.EquippedSlot, "staggerImmuneUntilTick", Long(a.StaggerImmuneUntilTick),
                "attack", a.Attack == null ? new object[0] : new[] { Map("startedTick", Long(a.Attack.StartedTick),
                    "weaponEntityId", a.Attack.WeaponEntityId, "hitBodyIds", a.Attack.HitBodyIds) })).ToArray(),
            "bodies", s.Bodies.Select(b => (object)Map("id", b.Id, "kind", b.Kind.ToString(), "position", Vector(b.Position),
                "rotationXyz", Vector(b.RotationXyz), "rotationW", Float(b.RotationW), "velocity", Vector(b.Velocity),
                "angularVelocity", Vector(b.AngularVelocity), "sleeping", b.Sleeping, "kinematic", b.Kinematic, "active", b.Active,
                "ownerTeam", b.OwnerTeam, "protected", b.Protected, "sourceId", b.SourceId, "generation", b.RecoveryGeneration)).ToArray(),
            "resources", s.Resources.Select(r => (object)Map("sourceId", r.SourceId, "team", r.Team, "lap", r.Lap,
                "generation", r.Generation, "recoveryAtTick", Long(r.RecoveryAtTick))).ToArray(),
            "mechanisms", s.Mechanisms.Select(m => (object)Map("puzzleId", m.PuzzleId, "team", m.Team, "lap", m.Lap, "solved", m.Solved,
                "activatedTick", Long(m.ActivatedTick), "phaseTick", Long(m.PhaseTick), "progressTicks", m.ProgressTicks, "itemEntityId", m.ItemEntityId,
                "physical", Map("stage", m.Physical.Stage.ToString(), "actorId", m.Physical.ActorId, "auxiliaryBodyId", m.Physical.AuxiliaryBodyId,
                    "stageTick", Long(m.Physical.StageTick), "siteHash", m.Physical.SiteHash, "motionUnits", Long(m.Physical.MotionUnits)))).ToArray(),
            "randomStreams", s.RandomStreams.Select(r => (object)Map("streamId", r.StreamId, "state", ULong(r.State), "increment", ULong(r.Increment))).ToArray(),
            "narrativeFlags", s.NarrativeFlags,
            "thrownFlights", s.ThrownFlights.Select(f => (object)Map("itemBodyId", f.ItemBodyId, "launchRevision", f.LaunchRevision,
                "startedTick", Long(f.StartedTick), "throwerId", f.ThrowerId, "hitBodyIds", f.HitBodyIds)).ToArray());

        static object WriteBinding(MatchBinding b) => Map("matchId", b.MatchId, "protocolVersion", b.ProtocolVersion,
            "modeHash", b.ModeHash, "courseHash", b.CourseHash, "manifestHash", b.ManifestHash, "rosterHash", b.RosterHash,
            "catalogueHash", b.CatalogueHash, "seats", b.Seats.Select(s => (object)Map("playerId", s.PlayerId, "team", s.Team, "slot", s.Slot)).ToArray());

        static object WriteProgress(ProgressSnapshot s) => Map("definitionHash", s.DefinitionHash, "tick", Long(s.Tick),
            "elapsedTicks", Long(s.ElapsedTicks), "wallClockMs", Long(s.WallClockMs), "migrating", s.Migrating,
            "teams", s.Teams.Select(t => (object)Map("team", t.Team, "lap", t.Lap, "nextCheckpoint", t.NextCheckpoint,
                "orderedObjectives", t.OrderedObjectives, "progressTick", Long(t.ProgressTick), "safeCheckpoint", t.SafeCheckpoint,
                "keepsakeCarrier", Optional(t.KeepsakeCarrier), "keepsakeLocation", t.KeepsakeLocation,
                "keepsakeRecoveryAt", Long(t.KeepsakeRecoveryAt), "generation", t.RecoveryGeneration,
                "finished", t.Finished, "completedPuzzles", t.CompletedPuzzles)).ToArray(),
            "players", s.Players.Select(p => (object)Map("id", p.Id, "team", p.Team, "slot", p.Slot, "knockouts", p.Knockouts,
                "connected", p.Connected, "returned", p.Returned, "reconnectDeadlineMs", Long(p.ReconnectDeadlineMs), "protectedUntil", Long(p.ProtectedUntil),
                "respawnAt", Long(p.RespawnAt), "eliminated", p.Eliminated, "generation", p.RecoveryGeneration)).ToArray(),
            "result", Optional(s.Result == null ? null : Map("outcome", s.Result.Outcome, "winners", s.Result.WinningTeams,
                "tick", Long(s.Result.Tick), "confirmed", s.Result.Confirmed)));

        static object WriteEconomy(EconomySnapshot s) => Map("nextItemId", s.NextItemId, "nextTileId", s.NextTileId, "minted", s.Minted, "spent", s.Spent,
            "players", s.PlayerStates.Select(p => (object)Map("playerId", p.PlayerId, "team", p.Team, "letters", p.Letters.ToString(),
                "reserved", p.Reserved.ToString(), "slots", p.Slots, "carriedFurniture", p.CarriedFurniture, "canAct", p.CanAct,
                "craft", Optional(p.Craft == null ? null : Map("itemId", p.Craft.ItemId, "startedAt", Double(p.Craft.StartedAt),
                    "readyAt", Double(p.Craft.ReadyAt), "slot", p.Craft.Slot)))).ToArray(),
            "items", s.ItemStates.Select(i => (object)Map("id", i.Id, "word", i.Word, "origin", i.Origin.ToString(), "state", i.State.ToString(),
                "holderId", i.HolderId, "crafterId", i.CrafterId, "deployerId", i.DeployerId, "deploymentTick", Long(i.DeploymentTick), "throwerId", i.ThrowerId,
                "throwerSkin", Optional(i.ThrowerSkin), "activatorId", i.ActivatorId, "durability", Float(i.Durability),
                "maxDurability", Float(i.MaxDurability), "spent", i.Spent, "revision", i.Revision)).ToArray(),
            "tiles", s.TileStates.Select(t => (object)Map("id", t.Key, "letter", t.Value.ToString())).ToArray());

        static object WriteCommands(CommandJournalSnapshot s) => Map("identityHash", s.IdentityHash, "epoch", Long(s.Epoch),
            "frontiers", s.Frontiers.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => (object)Map("actorId", p.Key, "sequence", Long(p.Value))).ToArray(),
            "receipts", s.Receipts.Select(r => (object)Map("actorId", r.ActorId, "sequence", Long(r.Sequence), "payloadHash", r.PayloadHash,
                "tick", Long(r.Tick), "code", r.Effect.Code, "entityId", r.Effect.EntityId)).ToArray());

        static MatchSnapshot ReadSnapshot(JsonNode n)
        {
            Keys(n, "binding", "epoch", "sequence", "nextWorldEntityId", "progress", "economy", "commands", "actors", "bodies", "resources", "mechanisms", "randomStreams", "narrativeFlags", "thrownFlights");
            return new MatchSnapshot(ReadBinding(n["binding"]), Counter(n["epoch"]), Counter(n["sequence"]), Counter(n["nextWorldEntityId"]),
                ReadProgress(n["progress"]), ReadEconomy(n["economy"]), ReadCommands(n["commands"]),
                Array(n["actors"], 8).Select(ReadActor).ToArray(), Array(n["bodies"], 8192).Select(ReadBody).ToArray(),
                Array(n["resources"], 8192).Select(ReadResource).ToArray(), Array(n["mechanisms"], 8192).Select(ReadMechanism).ToArray(),
                Array(n["randomStreams"], 256).Select(ReadRandom).ToArray(), Array(n["narrativeFlags"], 1024).Select(x => Text(x, 128)).ToArray(),
                Array(n["thrownFlights"], 8192).Select(ReadThrown).ToArray());
        }
        static MatchThrownFlight ReadThrown(JsonNode n)
        {
            Keys(n, "itemBodyId", "launchRevision", "startedTick", "throwerId", "hitBodyIds");
            return new MatchThrownFlight(Text(n["itemBodyId"]), Int(n["launchRevision"]), Counter(n["startedTick"]),
                Text(n["throwerId"]), Array(n["hitBodyIds"], 8192).Select(x => Text(x)).ToArray());
        }
        static MatchBinding ReadBinding(JsonNode n)
        {
            Keys(n, "matchId", "protocolVersion", "modeHash", "courseHash", "manifestHash", "rosterHash", "catalogueHash", "seats");
            var seats = Array(n["seats"], 8).Select(s => {
                Keys(s, "playerId", "team", "slot");
                return new WordboundSeat(Text(s["playerId"]), Int(s["team"]), Int(s["slot"]));
            }).ToArray();
            return new MatchBinding(Text(n["matchId"]), Text(n["protocolVersion"]), Text(n["modeHash"]), Text(n["courseHash"]),
                Text(n["manifestHash"]), Text(n["rosterHash"]), Text(n["catalogueHash"]), seats);
        }
        static ProgressSnapshot ReadProgress(JsonNode n)
        {
            Keys(n, "definitionHash", "tick", "elapsedTicks", "wallClockMs", "migrating", "teams", "players", "result");
            var teams = Array(n["teams"], 8).Select(t => {
                Keys(t, "team", "lap", "nextCheckpoint", "orderedObjectives", "progressTick", "safeCheckpoint", "keepsakeCarrier",
                    "keepsakeLocation", "keepsakeRecoveryAt", "generation", "finished", "completedPuzzles");
                var team = new TeamProgress { Team = Int(t["team"]), Lap = Int(t["lap"]), NextCheckpoint = Int(t["nextCheckpoint"]),
                    OrderedObjectives = Int(t["orderedObjectives"]), ProgressTick = Counter(t["progressTick"]),
                    SafeCheckpoint = Text(t["safeCheckpoint"]), KeepsakeCarrier = NullableText(t["keepsakeCarrier"]),
                    KeepsakeLocation = Text(t["keepsakeLocation"]), KeepsakeRecoveryAt = Counter(t["keepsakeRecoveryAt"], true),
                    RecoveryGeneration = Int(t["generation"]), Finished = t["finished"].Bool() };
                foreach (var id in Array(t["completedPuzzles"], 1024).Select(x => Text(x)))
                    if (!team.AddPuzzle(id)) throw t.Error("duplicate completed puzzle");
                return team;
            }).ToArray();
            var players = Array(n["players"], 8).Select(p => {
                Keys(p, "id", "team", "slot", "knockouts", "connected", "returned", "reconnectDeadlineMs", "protectedUntil", "respawnAt", "eliminated", "generation");
                return new PlayerProgress { Id = Text(p["id"]), Team = Int(p["team"]), Slot = Int(p["slot"]), Knockouts = Int(p["knockouts"]),
                    Connected = p["connected"].Bool(), Returned = p["returned"].Bool(), ReconnectDeadlineMs = Counter(p["reconnectDeadlineMs"], true),
                    ProtectedUntil = Counter(p["protectedUntil"]), RespawnAt = Counter(p["respawnAt"], true),
                    Eliminated = p["eliminated"].Bool(), RecoveryGeneration = Int(p["generation"]) };
            }).ToArray();
            var resultNode = Array(n["result"], 1);
            MatchResult result = null;
            if (resultNode.Count == 1)
            {
                var r = resultNode[0]; Keys(r, "outcome", "winners", "tick", "confirmed");
                result = new MatchResult(Text(r["outcome"]), Array(r["winners"], 8).Select(Int), Counter(r["tick"])) { Confirmed = r["confirmed"].Bool() };
            }
            return new ProgressSnapshot(Text(n["definitionHash"]), Counter(n["tick"]), Counter(n["elapsedTicks"]),
                Counter(n["wallClockMs"]), n["migrating"].Bool(), teams, players, result);
        }
        static EconomySnapshot ReadEconomy(JsonNode n)
        {
            Keys(n, "nextItemId", "nextTileId", "minted", "spent", "players", "items", "tiles");
            var players = Array(n["players"], 8).Select(p => {
                Keys(p, "playerId", "team", "letters", "reserved", "slots", "carriedFurniture", "canAct", "craft");
                var player = new PlayerInventory { PlayerId = Int(p["playerId"]), Team = Int(p["team"]),
                    Slots = Array(p["slots"], 8).Select(Int).ToArray(), CarriedFurniture = Int(p["carriedFurniture"]), CanAct = p["canAct"].Bool() };
                player.Letters.Add(LetterBag.FromWord(Text(p["letters"], 1024)));
                player.Reserved.Add(LetterBag.FromWord(Text(p["reserved"], 1024)));
                var craft = Array(p["craft"], 1);
                if (craft.Count == 1)
                {
                    var c = craft[0]; Keys(c, "itemId", "startedAt", "readyAt", "slot");
                    player.Craft = new CraftJob { ItemId = Text(c["itemId"]), StartedAt = ReadDouble(c["startedAt"]), ReadyAt = ReadDouble(c["readyAt"]), Slot = Int(c["slot"]) };
                }
                return player;
            }).ToArray();
            var items = Array(n["items"], 8192).Select(i => {
                Keys(i, "id", "word", "origin", "state", "holderId", "crafterId", "deployerId", "deploymentTick", "throwerId", "throwerSkin", "activatorId", "durability", "maxDurability", "spent", "revision");
                return new ItemInstance { Id = Int(i["id"]), Word = Text(i["word"]), Origin = EnumValue<ItemOrigin>(i["origin"]),
                    State = EnumValue<ItemState>(i["state"]), HolderId = Int(i["holderId"]), CrafterId = Int(i["crafterId"]),
                    DeployerId = Int(i["deployerId"]), DeploymentTick = Counter(i["deploymentTick"], true),
                    ThrowerId = Int(i["throwerId"]), ThrowerSkin = NullableText(i["throwerSkin"]),
                    ActivatorId = Int(i["activatorId"]), Durability = ReadFloat(i["durability"]), MaxDurability = ReadFloat(i["maxDurability"]),
                    Spent = i["spent"].Bool(), Revision = Int(i["revision"]) };
            }).ToArray();
            var tiles = Array(n["tiles"], 8192).Select(t => {
                Keys(t, "id", "letter"); string letter = Text(t["letter"], 1);
                if (letter.Length != 1 || !LetterBag.IsLetter(letter[0])) throw t.Error("invalid letter tile");
                return new KeyValuePair<int, char>(Int(t["id"]), letter[0]);
            }).ToArray();
            return new EconomySnapshot(Int(n["nextItemId"]), Int(n["nextTileId"]), Int(n["minted"]), Int(n["spent"]), players, items, tiles);
        }
        static CommandJournalSnapshot ReadCommands(JsonNode n)
        {
            Keys(n, "identityHash", "epoch", "frontiers", "receipts");
            var frontiers = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var f in Array(n["frontiers"], 8))
            { Keys(f, "actorId", "sequence"); frontiers.Add(Text(f["actorId"]), Counter(f["sequence"])); }
            var receipts = Array(n["receipts"], 1024).Select(r => {
                Keys(r, "actorId", "sequence", "payloadHash", "tick", "code", "entityId");
                return new CommandReceipt(Text(r["actorId"]), Counter(r["sequence"]), Text(r["payloadHash"]), Counter(r["tick"]),
                    new CommandEffect(Text(r["code"]), Text(r["entityId"])));
            }).ToArray();
            return new CommandJournalSnapshot(Text(n["identityHash"]), Counter(n["epoch"]), frontiers, receipts);
        }
        static MatchActorState ReadActor(JsonNode n)
        {
            Keys(n, "playerId", "health", "stamina", "nextAttackTick", "stunnedUntilTick", "dodgeUntilTick", "lastInputSequence", "appearance",
                "equippedSlot", "staggerImmuneUntilTick", "attack");
            var attackNode = Array(n["attack"], 1);
            MatchAttackState attack = null;
            if (attackNode.Count == 1)
            {
                var a = attackNode[0]; Keys(a, "startedTick", "weaponEntityId", "hitBodyIds");
                attack = new MatchAttackState(Counter(a["startedTick"]), Text(a["weaponEntityId"]),
                    Array(a["hitBodyIds"], 8192).Select(id => Text(id)));
            }
            return new MatchActorState(Text(n["playerId"]), ReadFloat(n["health"]), ReadFloat(n["stamina"]), Counter(n["nextAttackTick"]),
                Counter(n["stunnedUntilTick"]), Counter(n["dodgeUntilTick"]), Counter(n["lastInputSequence"]), Text(n["appearance"], 2048),
                Int(n["equippedSlot"]), attack, Counter(n["staggerImmuneUntilTick"]));
        }
        static WorldBodyState ReadBody(JsonNode n)
        {
            Keys(n, "id", "kind", "position", "rotationXyz", "rotationW", "velocity", "angularVelocity", "sleeping", "kinematic", "active", "ownerTeam", "protected", "sourceId", "generation");
            return new WorldBodyState(Text(n["id"]), EnumValue<WorldBodyKind>(n["kind"]), ReadVector(n["position"]),
                ReadVector(n["rotationXyz"]), ReadFloat(n["rotationW"]), ReadVector(n["velocity"]), ReadVector(n["angularVelocity"]),
                n["sleeping"].Bool(), n["kinematic"].Bool(), n["active"].Bool(), Int(n["ownerTeam"]),
                n["protected"].Bool(), Text(n["sourceId"]), Int(n["generation"]));
        }
        static MatchResourceState ReadResource(JsonNode n)
        {
            Keys(n, "sourceId", "team", "lap", "generation", "recoveryAtTick");
            return new MatchResourceState(Text(n["sourceId"]), Int(n["team"]), Int(n["lap"]), Int(n["generation"]), Counter(n["recoveryAtTick"], true));
        }
        static MatchMechanismState ReadMechanism(JsonNode n)
        {
            Keys(n, "puzzleId", "team", "lap", "solved", "activatedTick", "phaseTick", "progressTicks", "itemEntityId", "physical");
            var p = n["physical"];
            Keys(p, "stage", "actorId", "auxiliaryBodyId", "stageTick", "siteHash", "motionUnits");
            if (!Enum.TryParse(Text(p["stage"]), out MatchPhysicalStage stage) || stage.ToString() != Text(p["stage"]))
                throw p.Error("Invalid physical stage.");
            return new MatchMechanismState(Text(n["puzzleId"]), Int(n["team"]), Int(n["lap"]), n["solved"].Bool(),
                Counter(n["activatedTick"], true), Counter(n["phaseTick"]), Int(n["progressTicks"]), Text(n["itemEntityId"]),
                new MatchPhysicalEvidence(stage, Text(p["actorId"]), Text(p["auxiliaryBodyId"]), Counter(p["stageTick"]),
                    Text(p["siteHash"]), Counter(p["motionUnits"])));
        }
        static MatchRandomState ReadRandom(JsonNode n)
        {
            Keys(n, "streamId", "state", "increment");
            return new MatchRandomState(Text(n["streamId"]), UnsignedCounter(n["state"]), UnsignedCounter(n["increment"]));
        }
        static void Keys(JsonNode n, params string[] keys) => CourseJson.Keys(n, keys);
        static IReadOnlyList<JsonNode> Array(JsonNode n, int max)
        { if (!n.IsArray || n.Items.Count > max) throw n.Error("array exceeds its bound"); return n.Items; }
        static string Text(JsonNode n, int max = 64)
        {
            string text = n.String(); CourseJson.ValidateText(text);
            if (text.Length > max) throw n.Error("string exceeds its bound");
            return text;
        }
        static string NullableText(JsonNode n)
        { var values = Array(n, 1); return values.Count == 0 ? null : Text(values[0]); }
        static int Int(JsonNode n)
        {
            double number = n.Number();
            if (number < int.MinValue || number > int.MaxValue || Math.Truncate(number) != number || double.IsNaN(number))
                throw n.Error("integer exceeds its range");
            return (int)number;
        }
        static long Counter(JsonNode n, bool minusOne = false)
        {
            string text = Text(n, 20);
            if (!Signed.IsMatch(text) || !long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value) ||
                value < (minusOne ? -1 : 0)) throw n.Error("invalid canonical decimal counter");
            return value;
        }
        static ulong UnsignedCounter(JsonNode n)
        {
            string text = Text(n, 20);
            if (!Unsigned.IsMatch(text) || !ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value))
                throw n.Error("invalid canonical unsigned counter");
            return value;
        }
        static ulong Bits(JsonNode n, int width)
        {
            string text = Text(n, width);
            if (text.Length != width || text.Any(c => !((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) ||
                !ulong.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong value))
                throw n.Error("invalid lowercase IEEE bit string");
            return value;
        }
        static float ReadFloat(JsonNode n)
        {
            float value = BitConverter.Int32BitsToSingle(unchecked((int)(uint)Bits(n, 8)));
            if (!WorldVector.Finite(value)) throw n.Error("non-finite float");
            return value;
        }
        static double ReadDouble(JsonNode n)
        {
            double value = BitConverter.Int64BitsToDouble(unchecked((long)Bits(n, 16)));
            if (double.IsNaN(value) || double.IsInfinity(value)) throw n.Error("non-finite double");
            return value;
        }
        static WorldVector ReadVector(JsonNode n)
        {
            var values = Array(n, 3);
            if (values.Count != 3) throw n.Error("vector needs exactly three values");
            return new WorldVector(ReadFloat(values[0]), ReadFloat(values[1]), ReadFloat(values[2]));
        }
        static T EnumValue<T>(JsonNode n) where T : struct
        {
            string text = Text(n);
            if (!Enum.TryParse(text, out T value) || !Enum.IsDefined(typeof(T), value) || value.ToString() != text)
                throw n.Error("unknown enum identifier");
            return value;
        }
    }
}
