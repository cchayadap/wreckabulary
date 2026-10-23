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
    sealed partial class WordboundLiveMatch
    {
        public const string Origin = "https://wreckabulary-wordbound-coordinator.pagna.workers.dev";
        public const string AppId = "f6fb7f4c-b8a9-44a3-ac63-bee1c5f23552";
        const string Fingerprint = "43ea939c715a181912177bff3e98c647d5690ec2b0b40085a8bb6c047ce41dce";
        public readonly ModeDefinition Mode;
        public readonly CourseCatalogue Geometry;
        public readonly List<OnlineCoordinatorClient> Clients = new List<OnlineCoordinatorClient>();
        readonly List<WordboundHttpTransport> transports = new List<WordboundHttpTransport>();
        public ItemCatalogue Items { get; }
        public GameRules Rules { get; }
        public ResolvedCourse Course { get; }
        public OnlineContent Content { get; }
        public OnlineSettings Settings { get; }
        public OnlineAdmissionVerifier Verifier { get; }
        public WordboundClientContent ProductionContent { get; }
        public bool PublicQueue { get; }
        public string ApprovedRelease { get; }
        public IWordboundPuzzleContent PhysicalPuzzles => ProductionContent?.PhysicalPuzzles;
        public OnlineMatchExpectation Expected { get; private set; }
        public VerifiedOnlineMatch Verified { get; private set; }
        sealed class PartyGroup
        {
            public string Id;
            public readonly List<OnlineCoordinatorClient> Members = new List<OnlineCoordinatorClient>();
        }
        readonly List<PartyGroup> parties = new List<PartyGroup>();

        public WordboundLiveMatch(string projectRoot, CourseDocument proofCourse = null,
            WordboundClientContent productionContent = null, WordboundPublicCourseCatalog publicCatalogue = null)
        {
            if (proofCourse != null && productionContent != null)
                throw new ArgumentException("Choose an exact production course or the existing proof document.");
            ProductionContent = productionContent;
            PublicQueue = publicCatalogue != null;
            if (PublicQueue && (productionContent == null || !productionContent.Mode.PublicQueue ||
                !new WordboundPublicCourseRegistry(publicCatalogue.Json).Allows(productionContent) ||
                publicCatalogue.Find(productionContent.Mode.Id, productionContent.Mode.Format.ToString(), productionContent.Seed) == null))
                throw new ArgumentException("The public proof requires an exact native-exported approved course.");
            ApprovedRelease = publicCatalogue?.MapPoolVersion;
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
            string installedFolder = Path.Combine(projectRoot, "Assets/_Project/Data/Config");
            installedItems = File.ReadAllText(Path.Combine(installedFolder, "items.json"));
            installedRules = File.ReadAllText(Path.Combine(installedFolder, "rules.json"));
            if (productionContent != null)
            {
                Mode = productionContent.Mode; Geometry = productionContent.Geometry;
                Items = productionContent.Items; Rules = productionContent.Rules; Course = productionContent.Course;
                Content = PublicQueue ? productionContent.PublicBinding : productionContent.PrivateBinding;
                if (!productionContent.Matches(Content) || ((Mode.IsRace || Mode.Id == "Freestyle") &&
                    (PhysicalPuzzles == null || !PhysicalPuzzles.BindsRules || Course.PhysicalSites.Count == 0)))
                    throw new ArgumentException("The production fixture requires its exact installed content and physical plan where applicable.");
            }
            else
            {
                Mode = ModeDefinition.For("Freestyle"); Geometry = CourseTemplates.ProofCatalogue();
                string itemJson = installedItems;
                string ruleJson = installedRules;
                Items = ItemCatalogue.FromJson(itemJson, "native-proof-items");
                Rules = Mode.CreateGameplayRules(RuleBook.FromJson(ruleJson, "native-proof-rules").For("Dibs"));
                var document = proofCourse ?? CourseTemplates.FreestyleWorld();
                var resolved = CourseResolver.Resolve(document, Geometry,
                    new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(Mode)));
                if (!resolved.Success) throw new InvalidOperationException("The native proof course did not resolve.");
                Course = resolved.Manifest;
                string catalogueHash = OnlineJson.Hash(new Dictionary<string, object> {
                    ["geometry"] = Geometry.Hash, ["items"] = WordboundIdentity.Sha256(itemJson),
                    ["rules"] = WordboundIdentity.Sha256(ruleJson) });
                Content = new OnlineContent("Workshop", document.Id, Course.SourceHash, Course.Hash, Mode.Hash, catalogueHash);
            }
            Settings = new OnlineSettings(Mode.Id, Mode.Format.ToString(), PublicQueue && Mode.Id == "Freestyle", "asia",
                MatchSnapshotCodec.ProtocolVersion, (PublicQueue ? "pub-" : "native-") + Guid.NewGuid().ToString("N").Substring(0, 12));
        }

        public async Task Prepare(int count, CancellationToken ct)
        {
            if (ProductionContent?.WorkshopBundle != null) { await PrepareWorkshop(count, ct); return; }
            if (count != 2 && count != 4 && count != 8) throw new ArgumentOutOfRangeException(nameof(count));
            if (count > Mode.MaximumPlayers) throw new ArgumentException("The requested peers exceed the selected mode.");
            for (int i = 0; i < count; i++)
            {
                var transport = new WordboundHttpTransport(new Uri(Origin));
                transports.Add(transport);
                var client = new OnlineCoordinatorClient(new ReconnectFaultTransport(this, i, transport));
                Clients.Add(client);
                await client.CreateGuest("NativeGameplay" + i, ct);
            }
            string privateCode = null;
            int partySize = Mode.HasRaceFormat ? count / 2 : Mode.Id == "FreeForAll" || (PublicQueue && Mode.Id == "Freestyle") ? 1 : 4;
            foreach (var offset in Enumerable.Range(0, (count + partySize - 1) / partySize).Select(i => i * partySize))
            {
                var members = Clients.Skip(offset).Take(partySize).ToArray();
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
                    PublicQueue ? "QuickPlay" : privateCode == null ? "PrivateCreate" : "PrivateJoin", Key(),
                    PublicQueue || privateCode == null ? Content : null, privateCode, ct);
                if (!PublicQueue)
                    privateCode = (await members[0].Party(group.Id, ct))["assignment"]["privateCode"].String();
            }
            var assignment = (await Clients[0].Party(parties[0].Id, ct))["assignment"];
            if (PublicQueue) {
                long deadline = Now() + 45000;
                while (assignment["state"].String() == "FORMING" && Now() < deadline) {
                    await Task.Delay(500, ct);
                    assignment = (await Clients[0].Party(parties[0].Id, ct))["assignment"];
                }
            }
            string phase = assignment["state"].String();
            if ((phase != "FORMING" && phase != "ADMITTING") || assignment["roster"].Items.Count != count)
                throw new InvalidOperationException("The private native proof must reserve exactly its original seats.");
            if (PublicQueue && (phase != "ADMITTING" || assignment.Has("privateCode") ||
                OnlineJson.Hash(OnlineContent.Read(assignment["content"]).ToValue()) != OnlineJson.Hash(Content.ToValue()) ||
                OnlineJson.Hash(OnlineSettings.Read(assignment["settings"]).ToValue()) != OnlineJson.Hash(Settings.ToValue())))
                throw new InvalidOperationException("The public queue must lock the exact approved content and isolated pool without a private code.");
            string rosterHash = RosterHash(assignment);
            Expected = new OnlineMatchExpectation(assignment["matchId"].String(),
                assignment["reservationGeneration"].String(), rosterHash, Settings, Content);
            var launched = phase == "FORMING" ? await Clients[0].MatchAction(Expected, "launch", ct) : assignment;
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
            foreach (var group in parties) {
                var signed = Verified.Roster.Where(seat => seat.PartyId == group.Id).ToArray();
                if (signed.Length != group.Members.Count || signed.Select(seat => seat.TeamId).Distinct().Count() != 1 ||
                    !new HashSet<string>(signed.Select(seat => seat.PlayerId), StringComparer.Ordinal)
                        .SetEquals(group.Members.Select(member => member.Credentials.PlayerId)))
                    throw new InvalidOperationException("The signed party membership differs from its original authenticated members.");
            }
            if (Mode.HasRaceFormat)
            {
                var teams = Verified.Roster.GroupBy(s => s.TeamId).OrderBy(g => g.Key).ToArray();
                if (teams.Length != 2 || teams[0].Key != 0 || teams[1].Key != 1 ||
                    teams.Any(g => g.Count() != count / 2 || g.Select(s => s.PartyId).Distinct().Count() != 1) ||
                    teams.Select(g => g.First().PartyId).Distinct().Count() != 2 || parties.Count != 2)
                    throw new InvalidOperationException("The signed race roster must preserve the two exact whole parties.");
                foreach (var group in parties)
                {
                    var signed = Verified.Roster.Where(seat => seat.PartyId == group.Id).ToArray();
                    if (signed.Length != group.Members.Count || signed.Select(seat => seat.TeamId).Distinct().Count() != 1 ||
                        !new HashSet<string>(signed.Select(seat => seat.PlayerId), StringComparer.Ordinal)
                            .SetEquals(group.Members.Select(member => member.Credentials.PlayerId)))
                        throw new InvalidOperationException("The signed race party membership differs from its original authenticated members.");
                }
            }
        }

        public async Task<WordboundFusionJoin> FreshJoin(int seat, CancellationToken ct)
        {
            if (workshopLaunches != null) return await workshopLaunches[seat].FreshJoin(ct);
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
            foreach (var coordinator in workshopCoordinators) coordinator.Dispose();
            foreach (var transport in transports) transport.Dispose();
            return failures;
        }
        static string Key() => Guid.NewGuid().ToString("N");
        public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
