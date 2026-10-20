using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class OnlineConnectionLease
    {
        public string PlayerId { get; }
        public string HostId { get; }
        public string ConnectionId { get; }
        public string Epoch { get; }
        public long ExpiresAtMilliseconds { get; internal set; }
        internal OnlineConnectionLease(JsonNode claims)
        {
            PlayerId = OnlineJson.Text(claims, "playerId", OnlineJson.Uuid);
            HostId = OnlineJson.Text(claims, "hostId", OnlineJson.Uuid);
            ConnectionId = OnlineJson.Text(claims, "connectionId", OnlineJson.Uuid);
            Epoch = OnlineJson.Text(claims, "connectionEpoch", OnlineJson.Counter);
            if (Epoch == "0") throw new FormatException("Connection epoch must be positive.");
            ExpiresAtMilliseconds = OnlineJson.Integer(claims, "leaseExpiresAt");
        }
    }
    public sealed class OnlineConnectionSeat
    {
        public string PlayerId { get; }
        public string HostId { get; }
        public string ConnectionId { get; }
        public string Epoch { get; }
        public bool Returned { get; }
        public long ExpiresAtMilliseconds { get; }
        public long ReconnectDeadline { get; }
        internal OnlineConnectionSeat(JsonNode node)
        {
            string[] fields = { "playerId", "returned", "connectionEpoch", "connectionId", "hostId", "leaseExpiresAt", "reconnectDeadline" };
            if (!node.IsObject || node.Keys.Count() != fields.Length || fields.Any(name => !node.Keys.Contains(name)) ||
                fields.Where(name => name != "connectionId" && name != "hostId").Any(name => !node.Has(name)))
                throw new FormatException("Unexpected or missing connection state fields.");
            PlayerId = OnlineJson.Text(node, "playerId", OnlineJson.Uuid);
            HostId = node["hostId"].IsNull ? null : OnlineJson.Text(node, "hostId", OnlineJson.Uuid);
            ConnectionId = node["connectionId"].IsNull ? null : OnlineJson.Text(node, "connectionId", OnlineJson.Uuid);
            Epoch = OnlineJson.Text(node, "connectionEpoch", OnlineJson.Counter);
            Returned = node["returned"].Bool(); ExpiresAtMilliseconds = OnlineJson.Integer(node, "leaseExpiresAt");
            ReconnectDeadline = OnlineJson.Integer(node, "reconnectDeadline");
        }
    }
    public sealed class OnlineConnectionService
    {
        readonly OnlineCoordinatorClient client;
        readonly OnlineAdmissionVerifier verifier;
        readonly OnlineMatchExpectation expected;
        readonly Func<long> clock;
        public string LocalPlayerId => client.Credentials?.PlayerId ?? throw new InvalidOperationException("Guest authentication is required.");
        public OnlineConnectionService(OnlineCoordinatorClient coordinator, OnlineAdmissionVerifier admissionVerifier,
            OnlineMatchExpectation expectation, Func<long> wallClock = null)
        {
            client = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            verifier = admissionVerifier ?? throw new ArgumentNullException(nameof(admissionVerifier));
            expected = expectation ?? throw new ArgumentNullException(nameof(expectation));
            clock = wallClock ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }
        public async Task<string> Ticket(CancellationToken ct)
        {
            var result = await client.ConnectionTicket(expected, ct).ConfigureAwait(false);
            OnlineJson.Fields(result, "ticket", "expiresAt");
            if (OnlineJson.Integer(result, "expiresAt") <= clock()) throw new FormatException("Connection ticket expired.");
            return OnlineJson.Text(result, "ticket", "^[A-Za-z0-9_-]{43}$");
        }
        public async Task<OnlineConnectionLease> Authorize(VerifiedOnlineMatch match, string playerId, string ticket,
            string connectionSecret, string connectionId, CancellationToken ct)
        {
            var response = await client.AuthorizeConnection(expected, playerId, ticket, connectionSecret, connectionId, ct).ConfigureAwait(false);
            OnlineJson.Fields(response, "authorization", "claims");
            return verifier.VerifyConnection(response["authorization"].String(), match, playerId, LocalPlayerId, connectionId, clock());
        }
        public async Task<IReadOnlyList<OnlineConnectionSeat>> Renew(VerifiedOnlineMatch match,
            IEnumerable<OnlineConnectionLease> leases, CancellationToken ct)
        {
            var current = leases.ToArray();
            var response = await client.ConnectionState(expected, current, ct).ConfigureAwait(false);
            OnlineJson.Fields(response, "matchId", "reservationGeneration", "rosterHash", "revision", "serverTime", "seats");
            if (response["matchId"].String() != expected.MatchId || response["reservationGeneration"].String() != expected.Generation ||
                response["rosterHash"].String() != expected.RosterHash) throw new FormatException("Connection state binding mismatch.");
            OnlineJson.Text(response, "revision", OnlineJson.Counter);
            long serverTime = OnlineJson.Integer(response, "serverTime");
            if (Math.Abs(serverTime - clock()) > 5000) throw new FormatException("Coordinator clock is outside the supported tolerance.");
            var seats = response["seats"].Items.Select(n => new OnlineConnectionSeat(n)).ToArray();
            if (seats.Length != match.Roster.Count || seats.Select(s => s.PlayerId).Distinct().Count() != seats.Length ||
                seats.Any(s => !match.Roster.Any(r => r.PlayerId == s.PlayerId) ||
                    (s.HostId != null && !match.Roster.Any(r => r.PlayerId == s.HostId)) ||
                    s.ExpiresAtMilliseconds > serverTime + 15000 || (s.Returned && s.ExpiresAtMilliseconds != 0)))
                throw new FormatException("Connection state roster mismatch.");
            foreach (var lease in current)
            {
                var seat = seats.SingleOrDefault(s => s.PlayerId == lease.PlayerId);
                if (seat != null && !seat.Returned && seat.HostId == LocalPlayerId && seat.ConnectionId == lease.ConnectionId &&
                    seat.Epoch == lease.Epoch) lease.ExpiresAtMilliseconds = seat.ExpiresAtMilliseconds;
            }
            return seats;
        }
    }
}
