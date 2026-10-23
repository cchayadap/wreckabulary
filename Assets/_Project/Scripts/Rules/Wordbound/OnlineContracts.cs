using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class OnlineSettings
    {
        public string Mode { get; }
        public string Format { get; }
        public bool Fill { get; }
        public string Region { get; }
        public string ProtocolVersion { get; }
        public string MapPoolVersion { get; }
        public string PoolKey => string.Join(":", Region, ProtocolVersion, MapPoolVersion, Mode, Format);
        public int TeamCapacity => Mode == "TimeRun" ? 4 : Mode == "Freestyle" ? 8 : Mode == "FreeForAll" ? 1 :
            Format == "Squad" ? 4 : Format == "Duo" ? 2 : 1;
        public int TeamCount => Mode == "TimeRun" || Mode == "Freestyle" ? 1 : Mode == "FreeForAll" ? 8 : 2;
        public OnlineSettings(string mode, string format, bool fill, string region, string protocolVersion, string mapPoolVersion)
        {
            Mode = mode; Format = format; Fill = fill; Region = region; ProtocolVersion = protocolVersion; MapPoolVersion = mapPoolVersion;
            Validate(ToJson());
        }
        static void Validate(JsonNode node)
        {
            OnlineJson.Fields(node, "mode", "format", "fill", "region", "protocolVersion", "mapPoolVersion");
            string mode = OnlineJson.Text(node, "mode", "^(TimeRun|TeamRace|BrawlRace|FreeForAll|Freestyle)$");
            string format = OnlineJson.Text(node, "format", "^(Solo|Duo|Squad)$");
            bool fill = node["fill"].Bool();
            OnlineJson.Text(node, "region", "^(asia|eu|us)$");
            OnlineJson.Text(node, "protocolVersion", OnlineJson.Identifier);
            OnlineJson.Text(node, "mapPoolVersion", OnlineJson.Identifier);
            if ((mode != "TeamRace" && mode != "BrawlRace" && format != "Solo") ||
                (mode == "FreeForAll" && fill)) throw new FormatException("Unsupported online mode settings.");
        }
        public static OnlineSettings Read(JsonNode node)
        {
            Validate(node);
            return new OnlineSettings(node["mode"].String(), node["format"].String(), node["fill"].Bool(),
                node["region"].String(), node["protocolVersion"].String(), node["mapPoolVersion"].String());
        }
        public Dictionary<string, object> ToValue() => new Dictionary<string, object> {
            ["mode"] = Mode, ["format"] = Format, ["fill"] = Fill, ["region"] = Region,
            ["protocolVersion"] = ProtocolVersion, ["mapPoolVersion"] = MapPoolVersion };
        JsonNode ToJson() => OnlineJson.Parse(OnlineJson.Encode(ToValue()));
    }

    public sealed class OnlineContent
    {
        public string Kind { get; }
        public string CourseId { get; }
        public string CourseHash { get; }
        public string ManifestHash { get; }
        public string ModeHash { get; }
        public string CatalogHash { get; }
        public OnlineContent(string kind, string courseId, string courseHash, string manifestHash, string modeHash, string catalogHash)
        {
            Kind = kind; CourseId = courseId; CourseHash = courseHash; ManifestHash = manifestHash; ModeHash = modeHash; CatalogHash = catalogHash;
            Validate(OnlineJson.Parse(OnlineJson.Encode(ToValue())));
        }
        static void Validate(JsonNode node)
        {
            OnlineJson.Fields(node, "kind", "courseId", "courseHash", "manifestHash", "modeHash", "catalogHash");
            OnlineJson.Text(node, "kind", "^(BuiltIn|Workshop)$");
            OnlineJson.Text(node, "courseId", "^[A-Za-z0-9_-]{1,64}$");
            foreach (string field in new[] { "courseHash", "manifestHash", "modeHash", "catalogHash" })
                OnlineJson.Text(node, field, OnlineJson.HashPattern);
        }
        public static OnlineContent Read(JsonNode node)
        {
            Validate(node);
            return new OnlineContent(node["kind"].String(), node["courseId"].String(), node["courseHash"].String(),
                node["manifestHash"].String(), node["modeHash"].String(), node["catalogHash"].String());
        }
        public Dictionary<string, object> ToValue() => new Dictionary<string, object> {
            ["kind"] = Kind, ["courseId"] = CourseId, ["courseHash"] = CourseHash,
            ["manifestHash"] = ManifestHash, ["modeHash"] = ModeHash, ["catalogHash"] = CatalogHash };
    }

    public sealed class OnlineSeat
    {
        public string PlayerId { get; }
        public string PartyId { get; }
        public string PartyRevision { get; }
        public string AttemptId { get; }
        public int TeamId { get; }
        public int Slot { get; }
        public string ConnectionSecretHash { get; }
        internal OnlineSeat(JsonNode node)
        {
            OnlineJson.Fields(node, "playerId", "partyId", "partyRevision", "attemptId", "teamId", "seat", "connectionSecretHash");
            PlayerId = OnlineJson.Text(node, "playerId", OnlineJson.Uuid);
            PartyId = OnlineJson.Text(node, "partyId", OnlineJson.Uuid);
            PartyRevision = OnlineJson.Text(node, "partyRevision", OnlineJson.Counter);
            AttemptId = OnlineJson.Text(node, "attemptId", OnlineJson.Uuid);
            TeamId = (int)OnlineJson.Integer(node, "teamId", 0, 7);
            Slot = (int)OnlineJson.Integer(node, "seat", 0, 7);
            ConnectionSecretHash = OnlineJson.Text(node, "connectionSecretHash", OnlineJson.HashPattern);
        }
        public Dictionary<string, object> ToValue() => new Dictionary<string, object> {
            ["playerId"] = PlayerId, ["partyId"] = PartyId, ["partyRevision"] = PartyRevision, ["attemptId"] = AttemptId,
            ["teamId"] = TeamId, ["seat"] = Slot, ["connectionSecretHash"] = ConnectionSecretHash };
    }

    public sealed class OnlineMatchExpectation
    {
        public string MatchId { get; }
        public string Generation { get; }
        public string RosterHash { get; }
        public OnlineSettings Settings { get; }
        public OnlineContent Content { get; }
        public OnlineMatchExpectation(string matchId, string generation, string rosterHash, OnlineSettings settings, OnlineContent content)
        {
            var data = OnlineJson.Parse(OnlineJson.Encode(new Dictionary<string, object> {
                ["matchId"] = matchId, ["generation"] = generation, ["rosterHash"] = rosterHash }));
            MatchId = OnlineJson.Text(data, "matchId", OnlineJson.Uuid);
            Generation = OnlineJson.Text(data, "generation", OnlineJson.Counter);
            RosterHash = OnlineJson.Text(data, "rosterHash", OnlineJson.HashPattern);
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Content = content ?? throw new ArgumentNullException(nameof(content));
        }
    }

    public sealed class VerifiedOnlineMatch
    {
        public OnlineMatchExpectation Match { get; }
        public IReadOnlyList<OnlineSeat> Roster { get; }
        public long ExpiresAtSeconds { get; }
        public long StartedAtMilliseconds { get; }
        internal VerifiedOnlineMatch(OnlineMatchExpectation match, OnlineSeat[] roster, long expires, long started)
        { Match = match; Roster = Array.AsReadOnly(roster); ExpiresAtSeconds = expires; StartedAtMilliseconds = started; }
    }

    public sealed class VerifiedOnlineAdmission
    {
        public string PlayerId { get; }
        public OnlineSeat Seat { get; }
        public long ExpiresAtSeconds { get; }
        internal VerifiedOnlineAdmission(OnlineSeat seat, long expires)
        { PlayerId = seat.PlayerId; Seat = seat; ExpiresAtSeconds = expires; }
    }
}
