using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.OnlineGameplayTests
{
    sealed class WordboundLiveMatch
    {
        public const string Origin = "https://wreckabulary-wordbound-coordinator.pagna.workers.dev";
        public const string AppId = "f6fb7f4c-b8a9-44a3-ac63-bee1c5f23552";
        const string Fingerprint = "43ea939c715a181912177bff3e98c647d5690ec2b0b40085a8bb6c047ce41dce";
        public readonly ModeDefinition Mode = ModeDefinition.For("Freestyle");
        public readonly CourseCatalogue Geometry = CourseTemplates.ProofCatalogue();
        public readonly List<OnlineCoordinatorClient> Clients = new List<OnlineCoordinatorClient>();
        readonly List<WordboundHttpTransport> transports = new List<WordboundHttpTransport>();
        public ItemCatalogue Items { get; }
        public GameRules Rules { get; }
        public ResolvedCourse Course { get; }
        public OnlineContent Content { get; }
        public OnlineSettings Settings { get; }
        public OnlineAdmissionVerifier Verifier { get; }
        public OnlineMatchExpectation Expected { get; private set; }
        public VerifiedOnlineMatch Verified { get; private set; }
        sealed class PartyGroup
        {
            public string Id;
            public readonly List<OnlineCoordinatorClient> Members = new List<OnlineCoordinatorClient>();
        }
        readonly List<PartyGroup> parties = new List<PartyGroup>();

        public WordboundLiveMatch(string projectRoot)
        {
            var trust = OnlineJson.Parse(File.ReadAllText(Path.Combine(projectRoot, "OnlineServices/production-client.json")));
            if (trust["origin"].String() != Origin || trust["photonAppId"].String() != AppId ||
                trust["issuer"].String() != "wreckabulary-wordbound" || trust["keyId"].String() != "wordbound-rs256-20261020-01" ||
                trust["publicKeyFingerprint"].String() != Fingerprint || trust["jwks"]["keys"].Items.Count != 1)
                throw new InvalidOperationException("The native proof requires the independently pinned production trust.");
            var key = trust["jwks"]["keys"].Items.Single();
            if (OnlineJson.Hash(new Dictionary<string, object> {
                ["e"] = key["e"].String(), ["kty"] = key["kty"].String(), ["n"] = key["n"].String() }) != Fingerprint ||
                key["kid"].String() != trust["keyId"].String())
                throw new InvalidOperationException("The production public key fingerprint does not match.");
            Verifier = new OnlineAdmissionVerifier(trust["issuer"].String(), trust["jwks"]);
            string folder = Path.Combine(projectRoot, "Assets/_Project/Data/Config");
            string itemJson = File.ReadAllText(Path.Combine(folder, "items.json"));
            string ruleJson = File.ReadAllText(Path.Combine(folder, "rules.json"));
            Items = ItemCatalogue.FromJson(itemJson, "native-proof-items");
            Rules = Mode.CreateGameplayRules(RuleBook.FromJson(ruleJson, "native-proof-rules").For("Dibs"));
            var document = CourseTemplates.FreestyleWorld();
            var resolved = CourseResolver.Resolve(document, Geometry,
                new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(Mode)));
            if (!resolved.Success) throw new InvalidOperationException("The native proof course did not resolve.");
            Course = resolved.Manifest;
            string catalogueHash = OnlineJson.Hash(new Dictionary<string, object> {
                ["geometry"] = Geometry.Hash, ["items"] = WordboundIdentity.Sha256(itemJson),
                ["rules"] = WordboundIdentity.Sha256(ruleJson) });
            Content = new OnlineContent("Workshop", document.Id, Course.SourceHash, Course.Hash, Mode.Hash, catalogueHash);
            Settings = new OnlineSettings(Mode.Id, "Solo", false, "asia",
                "native-" + Guid.NewGuid().ToString("N").Substring(0, 12), "gameplay-proof-v1");
        }

        public async Task Prepare(int count, CancellationToken ct)
        {
            if (count != 2 && count != 4 && count != 8) throw new ArgumentOutOfRangeException(nameof(count));
            for (int i = 0; i < count; i++)
            {
                var transport = new WordboundHttpTransport(new Uri(Origin));
                transports.Add(transport);
                var client = new OnlineCoordinatorClient(transport);
                Clients.Add(client);
                await client.CreateGuest("NativeGameplay" + i, ct);
            }
            string privateCode = null;
            foreach (var offset in Enumerable.Range(0, (count + 3) / 4).Select(i => i * 4))
            {
                var members = Clients.Skip(offset).Take(4).ToArray();
                var party = await members[0].CreateParty(Settings, Key(), ct);
                var group = new PartyGroup { Id = party["id"].String() };
                group.Members.Add(members[0]); parties.Add(group);
                foreach (var member in members.Skip(1))
                {
                    party = await member.JoinParty(group.Id, party["invite"].String(), Key(), ct);
                    group.Members.Add(member);
                }
                foreach (var member in group.Members)
                    party = await member.Ready(group.Id, party["revision"].String(), true, Key(), ct);
                await members[0].Queue(group.Id, party["revision"].String(),
                    privateCode == null ? "PrivateCreate" : "PrivateJoin", Key(),
                    privateCode == null ? Content : null, privateCode, ct);
                privateCode = (await members[0].Party(group.Id, ct))["assignment"]["privateCode"].String();
            }
            var assignment = (await Clients[0].Party(parties[0].Id, ct))["assignment"];
            if (assignment["state"].String() != "FORMING" || assignment["roster"].Items.Count != count)
                throw new InvalidOperationException("The private native proof must reserve exactly its original seats.");
            string rosterHash = RosterHash(assignment);
            Expected = new OnlineMatchExpectation(assignment["matchId"].String(),
                assignment["reservationGeneration"].String(), rosterHash, Settings, Content);
            var launched = await Clients[0].MatchAction(Expected, "launch", ct);
            if (launched["state"].String() != "ADMITTING" || launched["rosterHash"].String() != rosterHash ||
                launched["reservationGeneration"].String() != Expected.Generation)
                throw new InvalidOperationException("Launching changed the expected private content or roster.");
            foreach (var client in Clients)
            {
                await client.MatchAction(Expected, "admission", ct);
                await client.AcceptMatch(Expected, ct);
            }
            var certificate = await Clients[0].MatchAction(Expected, "certificate", ct);
            Verified = Verifier.VerifyStart(certificate["certificate"].String(), Expected, Now() / 1000);
            if (Verified.Roster.Count != count ||
                !Verified.Roster.Select(s => s.PlayerId).OrderBy(x => x, StringComparer.Ordinal)
                    .SequenceEqual(Clients.Select(c => c.Credentials.PlayerId).OrderBy(x => x, StringComparer.Ordinal)))
                throw new InvalidOperationException("The signed roster does not contain exactly the authenticated guests.");
        }

        public async Task<WordboundFusionJoin> FreshJoin(int seat, CancellationToken ct)
        {
            var client = Clients[seat];
            var certificate = await client.MatchAction(Expected, "certificate", ct);
            var admission = await client.MatchAction(Expected, "admission", ct);
            var ticket = await client.PhotonTicket(ct);
            return new WordboundFusionJoin(client.Credentials.PlayerId, certificate["certificate"].String(),
                admission["grant"].String(), admission["connectionSecret"].String(), ticket);
        }

        public OnlineConnectionService Connections(int seat) =>
            new OnlineConnectionService(Clients[seat], Verifier, Expected);

        static string RosterHash(JsonNode assignment) => OnlineJson.Hash(assignment["roster"].Items.Select(s =>
            new Dictionary<string, object> {
                ["playerId"] = s["playerId"].String(), ["partyId"] = s["partyId"].String(),
                ["partyRevision"] = s["partyRevision"].String(), ["attemptId"] = s["attemptId"].String(),
                ["teamId"] = (int)s["teamId"].Number(), ["seat"] = (int)s["seat"].Number(),
                ["connectionSecretHash"] = s["connectionSecretHash"].String() }).ToArray());

        public async Task<int> Cleanup(CancellationToken ct)
        {
            int failures = 0;
            if (Expected != null)
                foreach (var client in Clients)
                    try { await client.MatchAction(Expected, "return", ct); }
                    catch { failures++; }
            foreach (var group in parties)
            {
                try
                {
                    var party = await group.Members[0].Party(group.Id, ct);
                    await group.Members[0].Cancel(group.Id, party["revision"].String(), Key(), ct);
                }
                catch { failures++; }
                foreach (var client in group.Members.AsEnumerable().Reverse())
                {
                    try
                    {
                        var profile = await client.Profile(ct);
                        if (!profile.Has("partyId")) continue;
                        if (profile["partyId"].String() != group.Id) throw new InvalidOperationException("Cleanup membership changed.");
                        var party = await client.Party(group.Id, ct);
                        await client.Leave(group.Id, party["revision"].String(), Key(), ct);
                        if ((await client.Profile(ct)).Has("partyId")) throw new InvalidOperationException("Cleanup left a party membership.");
                    }
                    catch { failures++; }
                }
            }
            foreach (var client in Clients) client.ClearCredentials();
            foreach (var transport in transports) transport.Dispose();
            return failures;
        }
        static string Key() => Guid.NewGuid().ToString("N");
        public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
