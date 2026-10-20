using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class MatchBinding
    {
        public string MatchId { get; }
        public string ProtocolVersion { get; }
        public string ModeHash { get; }
        public string CourseHash { get; }
        public string ManifestHash { get; }
        public string RosterHash { get; }
        public string CatalogueHash { get; }
        public IReadOnlyList<WordboundSeat> Seats { get; }
        public string Hash { get; }
        public MatchBinding(string matchId, string protocolVersion, string modeHash, string courseHash,
            string manifestHash, string rosterHash, string catalogueHash, IEnumerable<WordboundSeat> seats)
        {
            var roster = seats?.Take(9).OrderBy(s => s?.PlayerId, StringComparer.Ordinal).ToArray() ?? Array.Empty<WordboundSeat>();
            if (!WordboundIdentity.IsId(matchId) || !WordboundIdentity.IsId(protocolVersion) || protocolVersion.Length > 24 ||
                new[] { modeHash, courseHash, manifestHash, rosterHash, catalogueHash }.Any(hash => !WordboundIdentity.IsHash(hash)) ||
                roster.Length < 1 || roster.Length > 8 || roster.Any(s => s == null || !WordboundIdentity.IsId(s.PlayerId) ||
                    s.Team < 0 || s.Team > 7 || s.Slot < 0 || s.Slot > 7) ||
                roster.Select(s => s.PlayerId).Distinct(StringComparer.Ordinal).Count() != roster.Length ||
                roster.Select(s => (s.Team, s.Slot)).Distinct().Count() != roster.Length)
                throw new ArgumentException("Match binding contains invalid content or seats.");
            MatchId = matchId; ProtocolVersion = protocolVersion; ModeHash = modeHash; CourseHash = courseHash;
            ManifestHash = manifestHash; RosterHash = rosterHash; CatalogueHash = catalogueHash;
            Seats = Array.AsReadOnly(roster);
            Hash = CourseJson.Sha256(CourseJson.Encode(new Dictionary<string, object>
            {
                ["matchId"] = MatchId, ["protocol"] = ProtocolVersion, ["mode"] = ModeHash,
                ["course"] = CourseHash, ["manifest"] = ManifestHash, ["roster"] = RosterHash,
                ["catalogue"] = CatalogueHash, ["tickRate"] = ModeDefinition.TickRate,
                ["seats"] = Seats.Select(s => (object)new object[] { s.PlayerId, s.Team, s.Slot }).ToArray()
            }));
        }
    }

    public readonly struct WorldVector
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public WorldVector(float x, float y, float z)
        {
            if (!Finite(x) || !Finite(y) || !Finite(z)) throw new ArgumentException("World vectors must be finite.");
            X = x; Y = y; Z = z;
        }
        public float DistanceSquared(WorldVector other)
        {
            float x = X - other.X, y = Y - other.Y, z = Z - other.Z;
            return x * x + y * y + z * z;
        }
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public enum WorldBodyKind { Player, Item, Tile, Keepsake, Platform }

    public sealed class WorldBodyState
    {
        public string Id { get; }
        public WorldBodyKind Kind { get; }
        public WorldVector Position { get; }
        public WorldVector RotationXyz { get; }
        public float RotationW { get; }
        public WorldVector Velocity { get; }
        public WorldVector AngularVelocity { get; }
        public bool Sleeping { get; }
        public bool Kinematic { get; }
        public bool Active { get; }
        public int OwnerTeam { get; }
        public bool Protected { get; }
        public string SourceId { get; }
        public int RecoveryGeneration { get; }

        public WorldBodyState(string id, WorldBodyKind kind, WorldVector position,
            WorldVector rotationXyz = default, float rotationW = 1, WorldVector velocity = default,
            WorldVector angularVelocity = default, bool sleeping = false, bool kinematic = false,
            bool active = true, int ownerTeam = -1, bool protectedResource = false,
            string sourceId = "", int recoveryGeneration = 0)
        {
            float norm = rotationXyz.DistanceSquared(default) + rotationW * rotationW;
            if (!WordboundIdentity.IsId(id) || !Enum.IsDefined(typeof(WorldBodyKind), kind) ||
                !WorldVector.Finite(rotationW) || norm < .99f || norm > 1.01f ||
                ownerTeam < -1 || ownerTeam > 7 || sourceId == null ||
                (sourceId != "" && !WordboundIdentity.IsId(sourceId)) || recoveryGeneration < 0)
                throw new ArgumentException("Invalid world body identity or orientation.");
            Id = id; Kind = kind; Position = position; RotationXyz = rotationXyz; RotationW = rotationW;
            Velocity = velocity; AngularVelocity = angularVelocity; Sleeping = sleeping;
            Kinematic = kinematic; Active = active; OwnerTeam = ownerTeam; Protected = protectedResource;
            SourceId = sourceId; RecoveryGeneration = recoveryGeneration;
        }

        public WorldBodyState At(WorldVector position, WorldVector velocity = default, bool? active = null,
            bool? kinematic = null) => new WorldBodyState(Id, Kind, position, RotationXyz, RotationW,
                velocity, AngularVelocity, Sleeping, kinematic ?? Kinematic, active ?? Active, OwnerTeam,
                Protected, SourceId, RecoveryGeneration);
    }

    public sealed class MatchActorState
    {
        public string PlayerId { get; }
        public float Health { get; }
        public float Stamina { get; }
        public long NextAttackTick { get; }
        public long StunnedUntilTick { get; }
        public long DodgeUntilTick { get; }
        public long LastInputSequence { get; }
        public string Appearance { get; }
        public int EquippedSlot { get; }
        public MatchAttackState Attack { get; }
        public long StaggerImmuneUntilTick { get; }
        public MatchActorState(string playerId, float health, float stamina, long nextAttackTick = 0,
            long stunnedUntilTick = 0, long dodgeUntilTick = 0, long lastInputSequence = 0, string appearance = "",
            int equippedSlot = -1, MatchAttackState attack = null, long staggerImmuneUntilTick = 0)
        {
            if (!WordboundIdentity.IsId(playerId) || !WorldVector.Finite(health) || health < 0 ||
                !WorldVector.Finite(stamina) || stamina < 0 || nextAttackTick < 0 || stunnedUntilTick < 0 ||
                dodgeUntilTick < 0 || lastInputSequence < 0 || appearance == null || appearance.Length > 2048 ||
                equippedSlot < -1 || equippedSlot > 7 || staggerImmuneUntilTick < 0)
                throw new ArgumentException("Invalid authoritative actor state.");
            CourseJson.ValidateText(appearance);
            PlayerId = playerId; Health = health; Stamina = stamina; NextAttackTick = nextAttackTick;
            StunnedUntilTick = stunnedUntilTick; DodgeUntilTick = dodgeUntilTick;
            LastInputSequence = lastInputSequence; Appearance = appearance;
            EquippedSlot = equippedSlot; Attack = attack; StaggerImmuneUntilTick = staggerImmuneUntilTick;
        }
    }

    public sealed class MatchResourceState
    {
        public string SourceId { get; }
        public int Team { get; }
        public int Lap { get; }
        public int Generation { get; }
        public long RecoveryAtTick { get; }
        public MatchResourceState(string sourceId, int team, int lap, int generation = 0, long recoveryAtTick = -1)
        {
            if (!WordboundIdentity.IsId(sourceId) || team < -1 || team > 7 || lap < 0 || lap > 2 ||
                generation < 0 || recoveryAtTick < -1) throw new ArgumentException("Invalid recovery source state.");
            SourceId = sourceId; Team = team; Lap = lap; Generation = generation; RecoveryAtTick = recoveryAtTick;
        }
    }

    public sealed class MatchMechanismState
    {
        public string PuzzleId { get; }
        public int Team { get; }
        public int Lap { get; }
        public bool Solved { get; }
        public long ActivatedTick { get; }
        public long PhaseTick { get; }
        public int ProgressTicks { get; }
        public string ItemEntityId { get; }
        public MatchMechanismState(string puzzleId, int team, int lap, bool solved = false,
            long activatedTick = -1, long phaseTick = 0, int progressTicks = 0, string itemEntityId = "")
        {
            if (!WordboundIdentity.IsId(puzzleId) || team < 0 || team > 7 || lap < 0 || lap > 2 ||
                activatedTick < -1 || phaseTick < 0 || progressTicks < 0 || itemEntityId == null ||
                (itemEntityId != "" && !WordboundIdentity.IsId(itemEntityId)))
                throw new ArgumentException("Invalid mechanism state.");
            PuzzleId = puzzleId; Team = team; Lap = lap; Solved = solved; ActivatedTick = activatedTick;
            PhaseTick = phaseTick; ProgressTicks = progressTicks; ItemEntityId = itemEntityId;
        }
    }

    public sealed class MatchRandomState
    {
        public string StreamId { get; }
        public ulong State { get; }
        public ulong Increment { get; }
        public MatchRandomState(string streamId, ulong state, ulong increment)
        {
            if (!WordboundIdentity.IsId(streamId) || (increment & 1) == 0)
                throw new ArgumentException("Invalid PCG stream state.");
            StreamId = streamId; State = state; Increment = increment;
        }
    }

    public sealed class MatchSnapshot
    {
        public const int Schema = 2;
        public MatchBinding Binding { get; }
        public long Epoch { get; }
        public long Sequence { get; }
        public long NextWorldEntityId { get; }
        public ProgressSnapshot Progress { get; }
        public EconomySnapshot Economy { get; }
        public CommandJournalSnapshot Commands { get; }
        public IReadOnlyList<MatchActorState> Actors { get; }
        public IReadOnlyList<WorldBodyState> Bodies { get; }
        public IReadOnlyList<MatchResourceState> Resources { get; }
        public IReadOnlyList<MatchMechanismState> Mechanisms { get; }
        public IReadOnlyList<MatchRandomState> RandomStreams { get; }
        public IReadOnlyList<string> NarrativeFlags { get; }
        public IReadOnlyList<MatchThrownFlight> ThrownFlights { get; }

        public MatchSnapshot(MatchBinding binding, long epoch, long sequence, long nextWorldEntityId,
            ProgressSnapshot progress, EconomySnapshot economy, CommandJournalSnapshot commands,
            IEnumerable<MatchActorState> actors, IEnumerable<WorldBodyState> bodies,
            IEnumerable<MatchResourceState> resources, IEnumerable<MatchMechanismState> mechanisms,
            IEnumerable<MatchRandomState> randomStreams, IEnumerable<string> narrativeFlags,
            IEnumerable<MatchThrownFlight> thrownFlights = null)
        {
            Binding = binding ?? throw new ArgumentNullException(nameof(binding));
            if (epoch < 0 || sequence < 1 || nextWorldEntityId < 1 || progress == null || economy == null || commands == null)
                throw new ArgumentException("Snapshot has invalid counters or missing sections.");
            Epoch = epoch; Sequence = sequence; NextWorldEntityId = nextWorldEntityId;
            Progress = progress; Economy = economy; Commands = commands;
            Actors = Freeze(actors, actor => actor.PlayerId);
            Bodies = Freeze(bodies, body => body.Id);
            Resources = Freeze(resources, resource => resource.SourceId + "/" + resource.Team + "/" + resource.Lap);
            Mechanisms = Freeze(mechanisms, mechanism => mechanism.PuzzleId + "/" + mechanism.Team + "/" + mechanism.Lap);
            RandomStreams = Freeze(randomStreams, stream => stream.StreamId);
            ThrownFlights = Freeze(thrownFlights ?? Array.Empty<MatchThrownFlight>(), flight => flight.ItemBodyId);
            var flags = narrativeFlags?.Take(1025).ToArray() ?? throw new ArgumentNullException(nameof(narrativeFlags));
            if (flags.Length > 1024 || flags.Any(flag => !WordboundIdentity.IsId(flag)) ||
                flags.Distinct(StringComparer.Ordinal).Count() != flags.Length)
                throw new ArgumentException("Narrative flags exceed bounds or contain invalid identities.");
            NarrativeFlags = Array.AsReadOnly(flags.OrderBy(flag => flag, StringComparer.Ordinal).ToArray());
        }

        static IReadOnlyList<T> Freeze<T>(IEnumerable<T> values, Func<T, string> key) where T : class
        {
            var array = values?.Take(8193).ToArray() ?? throw new ArgumentNullException(nameof(values));
            if (array.Length > 8192 || array.Any(value => value == null)) throw new ArgumentException("Snapshot section exceeds bounds.");
            return Array.AsReadOnly(array.OrderBy(key, StringComparer.Ordinal).ToArray());
        }
    }
}
