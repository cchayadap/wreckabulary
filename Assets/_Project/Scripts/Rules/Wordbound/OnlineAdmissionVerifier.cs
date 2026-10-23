using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class OnlineAdmissionVerifier
    {
        readonly string issuer;
        readonly Dictionary<string, RSAParameters> keys = new Dictionary<string, RSAParameters>(StringComparer.Ordinal);

        public OnlineAdmissionVerifier(string expectedIssuer, string trustedJwksJson)
            : this(expectedIssuer, OnlineJson.Parse(trustedJwksJson)) { }

        public OnlineAdmissionVerifier(string expectedIssuer, JsonNode jwks)
        {
            if (string.IsNullOrWhiteSpace(expectedIssuer) || expectedIssuer.Length > 256)
                throw new ArgumentException("Pin the coordinator issuer.");
            issuer = expectedIssuer;
            OnlineJson.Fields(jwks, "keys");
            var supplied = jwks["keys"].Items;
            if (supplied.Count < 1 || supplied.Count > 4) throw new FormatException("Unsupported signing key count.");
            foreach (var key in supplied)
            {
                OnlineJson.Fields(key, "kty", "n", "e", "kid", "use", "alg");
                if (key["kty"].String() != "RSA" || key["use"].String() != "sig" || key["alg"].String() != "RS256")
                    throw new FormatException("Unsupported signing key.");
                string id = OnlineJson.Text(key, "kid", "^[A-Za-z0-9_-]{1,96}$");
                byte[] modulus = OnlineJson.DecodeBase64Url(key["n"].String(), 512);
                byte[] exponent = OnlineJson.DecodeBase64Url(key["e"].String(), 4);
                if (modulus.Length < 256 || modulus[0] < 128 || key["e"].String() != "AQAB" || keys.ContainsKey(id))
                    throw new FormatException("Invalid or duplicate RSA signing key.");
                var parameters = new RSAParameters { Modulus = modulus, Exponent = exponent };
                using var rsa = RSA.Create();
                rsa.ImportParameters(parameters);
                keys.Add(id, parameters);
            }
        }

        JsonNode Verify(string token, string audience, long nowSeconds, params string[] fields)
        {
            if (token == null || token.Length > 24576 || nowSeconds < 0) throw new FormatException("Invalid signed token.");
            var segments = token.Split('.');
            if (segments.Length != 3) throw new FormatException("Invalid JWT encoding.");
            var header = OnlineJson.Parse(OnlineJson.DecodeUtf8(OnlineJson.DecodeBase64Url(segments[0], 1024)));
            OnlineJson.Fields(header, "alg", "typ", "kid");
            if (header["alg"].String() != "RS256" || header["typ"].String() != "JWT" ||
                !keys.TryGetValue(header["kid"].String(), out var key)) throw new FormatException("Untrusted JWT header.");
            byte[] signature = OnlineJson.DecodeBase64Url(segments[2], 512);
            using var rsa = RSA.Create();
            rsa.ImportParameters(key);
            if (signature.Length != key.Modulus.Length ||
                !rsa.VerifyData(Encoding.ASCII.GetBytes(segments[0] + "." + segments[1]), signature,
                    HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                throw new FormatException("Invalid JWT signature.");
            var claims = OnlineJson.Parse(OnlineJson.DecodeUtf8(OnlineJson.DecodeBase64Url(segments[1], 16384)));
            OnlineJson.Fields(claims, fields.Concat(new[] { "iss", "aud", "iat", "exp" }).ToArray());
            if (claims["iss"].String() != issuer || claims["aud"].String() != audience)
                throw new FormatException("Wrong JWT issuer or audience.");
            long issued = OnlineJson.Integer(claims, "iat"), expires = OnlineJson.Integer(claims, "exp");
            if (issued > nowSeconds || expires <= nowSeconds || expires <= issued || expires - issued > 90 ||
                OnlineJson.Integer(claims, "version", 1, 1) != 1)
            {
                var error = new FormatException("Expired, future or unsupported token.");
                error.Data["issuedOffsetSeconds"] = issued - nowSeconds;
                error.Data["expiresOffsetSeconds"] = expires - nowSeconds;
                error.Data["tokenLifetimeSeconds"] = expires - issued;
                throw error;
            }
            return claims;
        }

        static void Bind(JsonNode claims, OnlineMatchExpectation expected)
        {
            if (OnlineJson.Text(claims, "matchId", OnlineJson.Uuid) != expected.MatchId ||
                OnlineJson.Text(claims, "reservationGeneration", OnlineJson.Counter) != expected.Generation ||
                OnlineJson.Text(claims, "rosterHash", OnlineJson.HashPattern) != expected.RosterHash)
                throw new FormatException("Match identity mismatch.");
        }

        public VerifiedOnlineMatch VerifyStart(string certificate, OnlineMatchExpectation expected, long nowSeconds)
        {
            if (expected == null) throw new ArgumentNullException(nameof(expected));
            var claims = Verify(certificate, "wordbound-match-start", nowSeconds,
                "version", "matchId", "reservationGeneration", "startedAt", "settings", "content", "rosterHash", "roster");
            Bind(claims, expected);
            var settings = OnlineSettings.Read(claims["settings"]); var content = OnlineContent.Read(claims["content"]);
            if (OnlineJson.Hash(settings.ToValue()) != OnlineJson.Hash(expected.Settings.ToValue()) ||
                OnlineJson.Hash(content.ToValue()) != OnlineJson.Hash(expected.Content.ToValue()))
                throw new FormatException("Content or settings mismatch.");
            var roster = claims["roster"].Items.Select(node => new OnlineSeat(node)).ToArray();
            if (roster.Length < 1 || roster.Length > 8 ||
                roster.Select(s => s.PlayerId).Distinct(StringComparer.Ordinal).Count() != roster.Length ||
                roster.Select(s => (s.TeamId, s.Slot)).Distinct().Count() != roster.Length ||
                roster.Any(s => s.TeamId >= settings.TeamCount || s.Slot >= settings.TeamCapacity))
                throw new FormatException("Invalid fixed roster.");
            foreach (var team in roster.GroupBy(s => s.TeamId))
                if (!team.Select(s => s.Slot).OrderBy(s => s).SequenceEqual(Enumerable.Range(0, team.Count())))
                    throw new FormatException("Team slots must be contiguous.");
            foreach (var party in roster.GroupBy(s => s.PartyId, StringComparer.Ordinal))
                if (party.Count() > 4 || party.Select(s => s.TeamId).Distinct().Count() != 1 ||
                    party.Select(s => s.AttemptId).Distinct(StringComparer.Ordinal).Count() != 1 ||
                    party.Select(s => s.PartyRevision).Distinct(StringComparer.Ordinal).Count() != 1)
                    throw new FormatException("Party roster was split or revised.");
            if (roster.GroupBy(s => s.AttemptId, StringComparer.Ordinal).Any(group =>
                group.Select(s => s.PartyId).Distinct(StringComparer.Ordinal).Count() != 1))
                throw new FormatException("One attempt cannot claim multiple parties.");
            if (((settings.Mode == "TeamRace" || settings.Mode == "BrawlRace") && roster.Select(s => s.TeamId).Distinct().Count() != 2) ||
                (settings.Mode == "FreeForAll" && roster.Length < 2))
                throw new FormatException("The roster cannot start this mode.");
            if (OnlineJson.Hash(roster.Select(s => s.ToValue()).ToArray()) != expected.RosterHash)
                throw new FormatException("Roster commitment mismatch.");
            long started = OnlineJson.Integer(claims, "startedAt");
            if (started / 1000 > nowSeconds) throw new FormatException("Match start is in the future.");
            return new VerifiedOnlineMatch(expected, roster, OnlineJson.Integer(claims, "exp"), started);
        }

        public VerifiedOnlineAdmission VerifyAdmission(string grant, VerifiedOnlineMatch verifiedMatch,
            string authenticatedPhotonPlayerId, string connectionSecret, long nowSeconds)
        {
            if (verifiedMatch == null) throw new ArgumentNullException(nameof(verifiedMatch));
            if (nowSeconds >= verifiedMatch.ExpiresAtSeconds) throw new FormatException("Refresh the match certificate.");
            var claims = Verify(grant, "wordbound-admission", nowSeconds,
                "version", "sub", "playerId", "partyId", "partyRevision", "attemptId", "matchId", "reservationGeneration",
                "teamId", "seat", "protocolVersion", "modeHash", "courseHash", "manifestHash", "catalogHash", "rosterHash", "connectionSecretHash");
            Bind(claims, verifiedMatch.Match);
            string player = OnlineJson.Text(claims, "playerId", OnlineJson.Uuid);
            if (claims["sub"].String() != player || player != authenticatedPhotonPlayerId)
                throw new FormatException("Transport identity does not match the admission.");
            var seat = verifiedMatch.Roster.SingleOrDefault(s => s.PlayerId == player);
            if (seat == null || claims["partyId"].String() != seat.PartyId || claims["partyRevision"].String() != seat.PartyRevision ||
                claims["attemptId"].String() != seat.AttemptId || OnlineJson.Integer(claims, "teamId", 0, 7) != seat.TeamId ||
                OnlineJson.Integer(claims, "seat", 0, 7) != seat.Slot)
                throw new FormatException("Seat assignment mismatch.");
            var expected = verifiedMatch.Match;
            if (claims["protocolVersion"].String() != expected.Settings.ProtocolVersion ||
                claims["modeHash"].String() != expected.Content.ModeHash || claims["courseHash"].String() != expected.Content.CourseHash ||
                claims["manifestHash"].String() != expected.Content.ManifestHash || claims["catalogHash"].String() != expected.Content.CatalogHash ||
                claims["connectionSecretHash"].String() != seat.ConnectionSecretHash)
                throw new FormatException("Admission content mismatch.");
            if (connectionSecret == null || OnlineJson.DecodeBase64Url(connectionSecret, 32).Length != 32 ||
                !ConstantEqual(OnlineJson.Hash(connectionSecret), seat.ConnectionSecretHash))
                throw new FormatException("Wrong connection secret.");
            return new VerifiedOnlineAdmission(seat, OnlineJson.Integer(claims, "exp"));
        }

        public OnlineConnectionLease VerifyConnection(string authorization, VerifiedOnlineMatch match,
            string authenticatedPhotonPlayerId, string authenticatedHostId, string connectionId, long nowMilliseconds)
        {
            if (string.IsNullOrEmpty(authenticatedHostId)) throw new FormatException("Authenticated host identity is required.");
            return VerifyConnectionCore(authorization, match, authenticatedPhotonPlayerId, authenticatedHostId, connectionId, nowMilliseconds);
        }
        public OnlineConnectionLease VerifyHostAuthorization(string authorization, VerifiedOnlineMatch match,
            string authenticatedPlayerId, string connectionId, long nowMilliseconds) =>
            VerifyConnectionCore(authorization, match, authenticatedPlayerId, null, connectionId, nowMilliseconds);

        OnlineConnectionLease VerifyConnectionCore(string authorization, VerifiedOnlineMatch match,
            string playerId, string hostId, string connectionId, long nowMilliseconds)
        {
            if (match == null || nowMilliseconds < 0) throw new ArgumentException("A verified match and wall clock are required.");
            var claims = Verify(authorization, "wordbound-connection", nowMilliseconds / 1000,
                "version", "matchId", "reservationGeneration", "rosterHash", "playerId", "hostId", "connectionId",
                "connectionEpoch", "leaseExpiresAt", "connectionRevision");
            Bind(claims, match.Match);
            OnlineJson.Text(claims, "connectionRevision", OnlineJson.Counter);
            var lease = new OnlineConnectionLease(claims, authorization);
            if (lease.PlayerId != playerId || (hostId != null && lease.HostId != hostId) ||
                lease.ConnectionId != connectionId || lease.ExpiresAtMilliseconds <= nowMilliseconds ||
                lease.ExpiresAtMilliseconds / 1000 > OnlineJson.Integer(claims, "exp") ||
                OnlineJson.Integer(claims, "exp") - OnlineJson.Integer(claims, "iat") > 15 ||
                !match.Roster.Any(s => s.PlayerId == lease.PlayerId) || !match.Roster.Any(s => s.PlayerId == lease.HostId))
            {
                var error = new FormatException("Connection authorization is expired or bound to another transport.");
                error.Data["leaseRemainingMilliseconds"] = lease.ExpiresAtMilliseconds - nowMilliseconds;
                error.Data["tokenLifetimeSeconds"] = OnlineJson.Integer(claims, "exp") - OnlineJson.Integer(claims, "iat");
                throw error;
            }
            return lease;
        }

        static bool ConstantEqual(string left, string right)
        {
            int difference = left.Length ^ right.Length;
            for (int i = 0; i < Math.Min(left.Length, right.Length); i++) difference |= left[i] ^ right[i];
            return difference == 0;
        }
    }
}
