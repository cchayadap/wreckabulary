using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed class WordboundFusionAuthority : IWordboundFusionAuthority
    {
        readonly AuthoritativeMatch authority;
        readonly ResolvedCourse course;
        readonly ItemCatalogue catalogue;
        readonly GameRules rules;
        readonly List<AuthenticatedCommand> commands = new List<AuthenticatedCommand>();
        bool ready, host;
        public string IdentityHash => authority.Binding.Hash;
        public long Epoch => authority.Epoch;
        public AuthoritativeMatch Match => authority;
        public WordboundFusionAuthority(AuthoritativeMatch match, VerifiedOnlineMatch online, ResolvedCourse resolvedCourse,
            ItemCatalogue itemCatalogue, GameRules gameRules)
            : this(match, resolvedCourse, itemCatalogue, gameRules)
        {
            if (online == null) throw new ArgumentNullException(nameof(online));
            var expected = online.Match;
            var binding = new MatchBinding(expected.MatchId, expected.Settings.ProtocolVersion, expected.Content.ModeHash,
                expected.Content.CourseHash, expected.Content.ManifestHash, expected.RosterHash, expected.Content.CatalogHash,
                online.Roster.Select(s => new WordboundSeat(s.PlayerId, s.TeamId, s.Slot)));
            if (binding.Hash != match.Binding.Hash) throw new ArgumentException("Authority does not match the signed original roster and content.");
        }
        WordboundFusionAuthority(AuthoritativeMatch match, ResolvedCourse resolvedCourse, ItemCatalogue itemCatalogue, GameRules gameRules)
        {
            authority = match ?? throw new ArgumentNullException(nameof(match));
            course = resolvedCourse ?? throw new ArgumentNullException(nameof(resolvedCourse));
            catalogue = itemCatalogue ?? throw new ArgumentNullException(nameof(itemCatalogue));
            rules = gameRules?.Clone() ?? throw new ArgumentNullException(nameof(gameRules));
        }
        public static WordboundFusionAuthority CreateLocal(AuthoritativeMatch match, ResolvedCourse resolvedCourse,
            ItemCatalogue itemCatalogue, GameRules gameRules, string localPlayerId)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            WordboundLocalSession.ValidateBinding(match.Binding, match.Mode, localPlayerId);
            var bridge = new WordboundFusionAuthority(match, resolvedCourse, itemCatalogue, gameRules);
            var snapshot = match.Capture(1);
            bridge.ValidateSnapshot(MatchSnapshotCodec.Export(snapshot), ToMark(new SnapshotStamp(match.Binding.MatchId,
                match.Binding.Hash, snapshot.Epoch, snapshot.Sequence, snapshot.Progress.Tick, MatchSnapshotCodec.ComputeHash(snapshot))));
            return bridge;
        }
        public void SetTransportReady(bool transportReady, bool isHost) { ready = transportReady; host = isHost; if (!ready) commands.Clear(); }
        public void AcceptCommand(string authenticatedActor, JsonNode body)
        {
            if (!ready || !host || commands.Count >= 128 || !authority.Binding.Seats.Any(s => s.PlayerId == authenticatedActor))
                throw new InvalidOperationException("Authority is not accepting this command.");
            var command = new PlayerCommand(long.Parse(body["sequence"].String(), CultureInfo.InvariantCulture),
                long.Parse(body["epoch"].String(), CultureInfo.InvariantCulture),
                (WordboundAction)Enum.Parse(typeof(WordboundAction), body["action"].String(), false), body["target"].String(),
                checked((int)body["revision"].Number()), checked((int)body["slot"].Number()), body["value"].String());
            commands.Add(new AuthenticatedCommand(authenticatedActor, command));
        }
        public MatchTickResult Advance(long wallClockMilliseconds, IEnumerable<WorldBodyState> trustedHostBodies = null,
            IEnumerable<ProgressEvent> trustedHostObservations = null, IEnumerable<HostDamage> trustedHostDamage = null,
            IEnumerable<HostMechanismObservation> trustedHostMechanisms = null,
            IEnumerable<HostThrownContact> trustedHostThrownContacts = null)
        {
            if (!ready || !host) throw new InvalidOperationException("Only the admitted host advances authoritative simulation.");
            var batch = commands.ToArray();
            var result = authority.Advance(wallClockMilliseconds, batch, trustedHostBodies, trustedHostObservations,
                trustedHostDamage, trustedHostMechanisms, trustedHostThrownContacts);
            if (result.Advanced) commands.RemoveRange(0, batch.Length);
            return result;
        }
        public OnlineSnapshotMark Publish(out string json) => ToMark(authority.PublishSnapshot(out json));
        static OnlineSnapshotMark ToMark(SnapshotStamp s) => new OnlineSnapshotMark(s.MatchId, s.IdentityHash, s.Epoch, s.Sequence, s.Tick, s.StateHash);
        static SnapshotStamp ToStamp(OnlineSnapshotMark s) => new SnapshotStamp(s.MatchId, s.IdentityHash, s.Epoch, s.Sequence, s.Tick, s.StateHash);
        MatchSnapshot Import(string json, OnlineSnapshotMark mark)
        {
            var imported = MatchSnapshotCodec.Import(json, authority.Binding, authority.Mode, course, catalogue, rules);
            if (!imported.Success || imported.Hash != mark.StateHash || mark.IdentityHash != IdentityHash ||
                mark.MatchId != authority.Binding.MatchId || imported.Snapshot.Epoch != mark.Epoch ||
                imported.Snapshot.Sequence != mark.Sequence || imported.Snapshot.Progress.Tick != mark.Tick)
                throw new FormatException("Snapshot failed complete authority validation.");
            return imported.Snapshot;
        }
        public void ValidateSnapshot(string json, OnlineSnapshotMark mark) => Import(json, mark);
        public bool AcknowledgeDurable(OnlineSnapshotMark mark) => authority.Acknowledge(ToStamp(mark));
        public void PauseForMigration() { ready = false; commands.Clear(); authority.PauseForMigration(); }
        public void RestoreCommitted(string json, OnlineSnapshotMark mark, long nextEpoch, long wallClockMilliseconds)
        {
            var snapshot = Import(json, mark);
            authority.RestoreCommitted(snapshot, ToStamp(mark), nextEpoch, wallClockMilliseconds);
            foreach (var player in snapshot.Progress.Players.Where(p => p.Connected))
                authority.Disconnect(player.Id, wallClockMilliseconds);
            commands.Clear(); ready = false;
        }
        public IReadOnlyList<OnlineSeatPresence> Presence() => authority.Capture(1).Progress.Players
            .Select(p => new OnlineSeatPresence(p.Id, p.Connected || p.ReconnectDeadlineMs < 0 ? 0 : p.ReconnectDeadlineMs, p.Returned)).ToArray();
        public void Disconnect(string actor, long wallClockMilliseconds) => authority.Disconnect(actor, wallClockMilliseconds);
        public bool Reconnect(string actor, long wallClockMilliseconds) => authority.Reconnect(actor, wallClockMilliseconds);
        public void Return(string actor, long wallClockMilliseconds) => authority.Return(actor, wallClockMilliseconds);
    }
}
