using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class OnlineRequest
    {
        public string Method { get; }
        public string Path { get; }
        public string JsonBody { get; }
        public string AccessToken { get; }
        public string IdempotencyKey { get; }
        internal OnlineRequest(string method, string path, object body, string accessToken, string key)
        {
            Method = method; Path = path; JsonBody = body == null ? null : OnlineJson.Encode(body);
            AccessToken = accessToken; IdempotencyKey = key;
            if (JsonBody != null && System.Text.Encoding.UTF8.GetByteCount(JsonBody) > 16384)
                throw new ArgumentException("Coordinator request exceeds its limit.");
        }
    }

    public sealed class OnlineResponse
    {
        public int Status { get; }
        public string Json { get; }
        public OnlineResponse(int status, string json) { Status = status; Json = json; }
    }

    public interface IOnlineHttpTransport
    {
        Task<OnlineResponse> Send(OnlineRequest request, CancellationToken cancellationToken);
    }

    public sealed class OnlineServiceException : Exception
    {
        public int Status { get; }
        public string Code { get; }
        public OnlineServiceException(int status, string code) : base("Coordinator request failed: " + code)
        { Status = status; Code = code; }
    }

    public sealed class OnlineCredentials
    {
        public string PlayerId { get; }
        public string AccessToken { get; }
        public string RefreshToken { get; }
        public long ExpiresIn { get; }
        internal OnlineCredentials(JsonNode node)
        {
            OnlineJson.Fields(node, "playerId", "accessToken", "refreshToken", "expiresIn");
            PlayerId = OnlineJson.Text(node, "playerId", OnlineJson.Uuid);
            AccessToken = OnlineJson.Text(node, "accessToken", "^[A-Za-z0-9_.-]{1,4096}$");
            RefreshToken = node["refreshToken"].String();
            if (!RefreshToken.StartsWith(PlayerId + ".", StringComparison.Ordinal) ||
                OnlineJson.DecodeBase64Url(RefreshToken.Substring(PlayerId.Length + 1), 32).Length != 32)
                throw new FormatException("Invalid refresh credential.");
            ExpiresIn = OnlineJson.Integer(node, "expiresIn", 900, 900);
        }
    }

    public sealed class OnlinePhotonTicket
    {
        public string Ticket { get; }
        public string AppId { get; }
        public long ExpiresAtMilliseconds { get; }
        internal OnlinePhotonTicket(JsonNode node)
        {
            OnlineJson.Fields(node, "ticket", "appId", "expiresAt");
            Ticket = OnlineJson.Text(node, "ticket", "^[A-Za-z0-9_.-]{1,4096}$");
            AppId = OnlineJson.Text(node, "appId", OnlineJson.Uuid);
            ExpiresAtMilliseconds = OnlineJson.Integer(node, "expiresAt");
        }
        public Dictionary<string, object> PhotonPostData() => new Dictionary<string, object> { ["ticket"] = Ticket };
    }

    public sealed class OnlineCoordinatorClient
    {
        readonly IOnlineHttpTransport transport;
        readonly object authenticationGate = new object();
        int authenticationGeneration;
        public OnlineCredentials Credentials { get; private set; }
        public OnlineCoordinatorClient(IOnlineHttpTransport transport) { this.transport = transport ?? throw new ArgumentNullException(nameof(transport)); }
        static Dictionary<string, object> Data() => new Dictionary<string, object>();
        static string Valid(string value, string pattern)
        {
            var match = Regex.Match(value ?? "", pattern, RegexOptions.CultureInvariant);
            if (!match.Success || match.Length != value.Length) throw new ArgumentException("Invalid coordinator argument.");
            return value;
        }
        static string PartyPath(string id) => "/v1/parties/" + Valid(id, OnlineJson.Uuid);
        static string MatchPath(OnlineMatchExpectation match) => "/v1/pools/" + Uri.EscapeDataString(match.Settings.PoolKey) +
            "/matches/" + match.MatchId;
        async Task<JsonNode> Send(string method, string path, object body, bool authenticated, string key, CancellationToken ct)
        {
            var credentials = Credentials;
            if (authenticated && credentials == null) throw new InvalidOperationException("Guest authentication is required.");
            if (key != null) Valid(key, "^[A-Za-z0-9_-]{8,96}$");
            string token = authenticated ? credentials.AccessToken : null;
            var response = await transport.Send(new OnlineRequest(method, path, body, token, key), ct).ConfigureAwait(false);
            var json = OnlineJson.Parse(response.Json);
            if (response.Status < 200 || response.Status >= 300)
            {
                OnlineJson.Fields(json, "error");
                OnlineJson.Fields(json["error"], "code");
                throw new OnlineServiceException(response.Status, OnlineJson.Text(json["error"], "code", "^[A-Z0-9_]{1,64}$"));
            }
            return json;
        }
        public async Task<OnlineCredentials> CreateGuest(string displayName, CancellationToken ct = default)
        {
            if (displayName == null || displayName.Trim().Length < 1 || displayName.Length > 32) throw new ArgumentException("Invalid display name.");
            int generation = Interlocked.Increment(ref authenticationGeneration);
            var result = new OnlineCredentials(await Send("POST", "/v1/guests",
                new Dictionary<string, object> { ["displayName"] = displayName.Trim() }, false, null, ct).ConfigureAwait(false));
            SetCredentials(result, generation); return result;
        }
        public async Task<OnlineCredentials> Refresh(string protectedRefreshToken, CancellationToken ct = default)
        {
            Valid(protectedRefreshToken, "^[a-f0-9-]{36}\\.[A-Za-z0-9_-]{43}$");
            int generation = Interlocked.Increment(ref authenticationGeneration);
            var result = new OnlineCredentials(await Send("POST", "/v1/auth/refresh",
                new Dictionary<string, object> { ["refreshToken"] = protectedRefreshToken }, false, null, ct).ConfigureAwait(false));
            SetCredentials(result, generation); return result;
        }
        void SetCredentials(OnlineCredentials result, int generation)
        {
            lock (authenticationGate)
            {
                if (Volatile.Read(ref authenticationGeneration) != generation)
                    throw new OperationCanceledException("Authentication was superseded.");
                Credentials = result;
            }
        }
        public void ClearCredentials()
        {
            lock (authenticationGate) { Interlocked.Increment(ref authenticationGeneration); Credentials = null; }
        }
        public Task<JsonNode> Keys(CancellationToken ct = default) => Send("GET", "/v1/keys", null, false, null, ct);
        public Task<JsonNode> Profile(CancellationToken ct = default) => Send("GET", "/v1/me", null, true, null, ct);
        public async Task<OnlinePhotonTicket> PhotonTicket(CancellationToken ct = default) =>
            new OnlinePhotonTicket(await Send("POST", "/v1/photon/tickets", Data(), true, null, ct).ConfigureAwait(false));
        public Task<JsonNode> CreateParty(OnlineSettings settings, string key, CancellationToken ct = default) =>
            Send("POST", "/v1/parties", new Dictionary<string, object> { ["settings"] = settings.ToValue() }, true, RequiredKey(key), ct);
        public Task<JsonNode> JoinParty(string partyId, string invite, string key, CancellationToken ct = default) =>
            Send("POST", "/v1/party-joins", new Dictionary<string, object> {
                ["partyId"] = Valid(partyId, OnlineJson.Uuid), ["invite"] = Valid(invite, "^[a-f0-9]{16}$") }, true, RequiredKey(key), ct);
        public Task<JsonNode> Party(string partyId, CancellationToken ct = default) => Send("GET", PartyPath(partyId), null, true, null, ct);
        public Task<JsonNode> SetSettings(string partyId, string revision, OnlineSettings settings, string key, CancellationToken ct = default) =>
            Send("PATCH", PartyPath(partyId), new Dictionary<string, object> {
                ["expectedRevision"] = Valid(revision, OnlineJson.Counter), ["settings"] = settings.ToValue() }, true, RequiredKey(key), ct);
        public Task<JsonNode> Ready(string partyId, string revision, bool ready, string key, CancellationToken ct = default) =>
            Send("POST", PartyPath(partyId) + "/ready", new Dictionary<string, object> {
                ["expectedRevision"] = Valid(revision, OnlineJson.Counter), ["ready"] = ready }, true, RequiredKey(key), ct);
        public Task<JsonNode> Queue(string partyId, string revision, string channel, string key,
            OnlineContent content = null, string privateCode = null, CancellationToken ct = default)
        {
            Valid(channel, "^(QuickPlay|CustomFill|PrivateCreate|PrivateJoin)$");
            bool wantsContent = channel == "QuickPlay" || channel == "PrivateCreate";
            if (wantsContent != (content != null) || (channel == "PrivateJoin") != (privateCode != null) ||
                (channel == "QuickPlay" && content.Kind != "BuiltIn")) throw new ArgumentException("Invalid queue content or private code.");
            var body = new Dictionary<string, object> { ["expectedRevision"] = Valid(revision, OnlineJson.Counter), ["channel"] = channel };
            if (content != null) body["content"] = content.ToValue();
            if (privateCode != null) body["code"] = Valid(privateCode, "^[a-f0-9]{16}$");
            return Send("POST", PartyPath(partyId) + "/queue", body, true, RequiredKey(key), ct);
        }
        public Task<JsonNode> Cancel(string partyId, string revision, string key, CancellationToken ct = default) =>
            PartyAction(partyId, revision, key, "cancel", ct);
        public Task<JsonNode> Leave(string partyId, string revision, string key, CancellationToken ct = default) =>
            PartyAction(partyId, revision, key, "leave", ct);
        Task<JsonNode> PartyAction(string partyId, string revision, string key, string action, CancellationToken ct) =>
            Send("POST", PartyPath(partyId) + "/" + action, new Dictionary<string, object> {
                ["expectedRevision"] = Valid(revision, OnlineJson.Counter) }, true, RequiredKey(key), ct);
        public Task<JsonNode> MatchAction(OnlineMatchExpectation match, string action, CancellationToken ct = default) =>
            Send("POST", MatchPath(match) + "/" + Valid(action, "^(admission|certificate|reconnect|launch|return)$"), Data(), true, null, ct);
        public Task<JsonNode> AcceptMatch(OnlineMatchExpectation match, CancellationToken ct = default) =>
            Send("POST", MatchPath(match) + "/ready", new Dictionary<string, object> {
                ["reservationGeneration"] = match.Generation, ["manifestHash"] = match.Content.ManifestHash,
                ["rosterHash"] = match.RosterHash }, true, null, ct);
        public Task<JsonNode> Presence(OnlineMatchExpectation match, bool connected, CancellationToken ct = default) =>
            Send("POST", MatchPath(match) + "/presence", new Dictionary<string, object> { ["connected"] = connected }, true, null, ct);
        static string RequiredKey(string key) => Valid(key, "^[A-Za-z0-9_-]{8,96}$");
    }
}
